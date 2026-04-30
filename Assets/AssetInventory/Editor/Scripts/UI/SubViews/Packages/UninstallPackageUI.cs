using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using ImpossibleRobert.Common;
using UnityEngine;
#pragma warning disable CS0618 // Type or member is obsolete
#if UNITY_6000_2_OR_NEWER
using BaseTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using BaseTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace AssetInventory
{
    public class UninstallPackageUI : BasicEditorUI
    {
        private AssetInfo _info;
        private Dictionary<string, List<string>> _usages;
        private bool _calculating;
        private bool _deleteEmptyFolders = true;

        private FileTreeViewControl _treeView;
        private BaseTreeViewState _treeViewState;

        // Async analysis fields
        private bool _analyzingUsages;
        private int _analysisProgress;
        private int _analysisTotal;
        private bool _cancellationRequested;
        private Dictionary<string, FileTreeElement> _pathToElementMap;

        public static UninstallPackageUI ShowWindow()
        {
            UninstallPackageUI window = GetWindow<UninstallPackageUI>("Uninstall Package");
            window.minSize = new Vector2(500, 400);
            return window;
        }

        public void Init(AssetInfo info, AssetInfo usageInfo = null)
        {
            _info = info;
            _calculating = true;
            _usages = new Dictionary<string, List<string>>();

            // Run analysis in background to not freeze UI
            AnalyzePackage(usageInfo);
        }

        private void AnalyzePackage(AssetInfo usageInfo)
        {
            List<string> projectFiles = new List<string>();
            List<string> packageGuids = new List<string>();

            if (usageInfo?.ChildInfos != null && usageInfo.ChildInfos.Count > 0)
            {
                // Reuse existing calculation
                foreach (AssetInfo child in usageInfo.ChildInfos)
                {
                    if (!string.IsNullOrEmpty(child.ProjectPath))
                    {
                        projectFiles.Add(child.ProjectPath);
                        if (!string.IsNullOrEmpty(child.Guid)) packageGuids.Add(child.Guid);
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Usage information not available. Please run 'Identify Used Packages' first.", "OK");
                Close();
                return;
            }

            // Build Tree Data immediately (before usage analysis)
            List<FileTreeElement> treeElements = new List<FileTreeElement>();
            FileTreeElement root = new FileTreeElement("Root", -1, 0);
            treeElements.Add(root);

            // Create temporary structure to build hierarchy
            _pathToElementMap = new Dictionary<string, FileTreeElement>();
            int idCounter = 1;

            // Sort paths to ensure folders come before files if possible, but we handle creation on demand
            foreach (string path in projectFiles.OrderBy(p => p))
            {
                string[] parts = path.Split('/');
                string currentPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (string.IsNullOrEmpty(currentPath)) currentPath = part;
                    else currentPath += "/" + part;

                    if (!_pathToElementMap.TryGetValue(currentPath, out FileTreeElement node))
                    {
                        bool isFolder = i < parts.Length - 1 || AssetDatabase.IsValidFolder(currentPath);
                        int depth = i;

                        node = new FileTreeElement(part, depth, idCounter++)
                        {
                            Path = currentPath,
                            IsFolder = isFolder
                        };

                        _pathToElementMap[currentPath] = node;
                        treeElements.Add(node);
                    }
                }
            }

            // Initialize TreeView
            if (_treeViewState == null) _treeViewState = new BaseTreeViewState();

            // Convert list to tree to set up parent/children references
            FileTreeElement rootElement = TreeElementUtility.ListToTree(treeElements);

            // Sort recursively
            SortTree(rootElement);

            // Flatten back
            TreeElementUtility.TreeToList(rootElement, treeElements);

            TreeModel<FileTreeElement> model = new TreeModel<FileTreeElement>(treeElements);
            _treeView = new FileTreeViewControl(_treeViewState, model);
            _treeView.ExpandAll();

            _calculating = false;
            Repaint();

            // Start async usage analysis
            _ = AnalyzeUsagesAsync(packageGuids);
        }

        private async Task AnalyzeUsagesAsync(List<string> packageGuids)
        {
            _analyzingUsages = true;
            _cancellationRequested = false;
            _analysisProgress = 0;

            HashSet<string> packageGuidSet = new HashSet<string>(packageGuids);

            string[] allAssetGuids = AssetDatabase.FindAssets("");
            _analysisTotal = allAssetGuids.Length;

            for (int i = 0; i < allAssetGuids.Length; i++)
            {
                if (_cancellationRequested) break;

                string guid = allAssetGuids[i];
                _analysisProgress = i + 1;

                if (packageGuidSet.Contains(guid)) continue;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                string[] deps = AssetDatabase.GetDependencies(path, false);
                foreach (string depPath in deps)
                {
                    string depGuid = AssetDatabase.AssetPathToGUID(depPath);
                    if (packageGuidSet.Contains(depGuid))
                    {
                        // Update the usages dictionary
                        if (!_usages.ContainsKey(depPath))
                        {
                            _usages[depPath] = new List<string>();
                        }
                        _usages[depPath].Add(path);

                        // Update the tree element directly
                        if (_pathToElementMap.TryGetValue(depPath, out FileTreeElement element))
                        {
                            if (_usages.TryGetValue(depPath, out List<string> usage))
                            {
                                element.Usages = usage;
                            }
                        }
                    }
                }

                // Yield control back to Unity every 50 items to keep UI responsive
                if (i % 50 == 0)
                {
                    await Task.Yield();
                }
            }

            _analyzingUsages = false;
            Repaint();
        }

        private void SortTree(TreeElement node)
        {
            if (node.Children != null && node.Children.Count > 0)
            {
                node.Children = node.Children
                    .OrderByDescending(c => c is FileTreeElement fte && fte.IsFolder)
                    .ThenBy(c => c.TreeName)
                    .ToList();

                foreach (TreeElement child in node.Children)
                {
                    SortTree(child);
                }
            }
        }

        private new void OnGUI()
        {
            if (_info == null)
            {
                Close();
                return;
            }

            EditorGUILayout.Space();
            GUILabelWithTextNoMax("Package", _info.GetDisplayName(), 70);
            EditorGUILayout.Space();

            if (_calculating)
            {
                EditorGUILayout.LabelField("Loading package files...", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (_treeView == null || _treeView.Model.NumberOfDataElements <= 1) // 1 is root
            {
                EditorGUILayout.HelpBox("No files from this package found in the project.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("Select files to delete. Files with warning icons are used by other assets.", MessageType.Info);
            EditorGUILayout.Space();

            // Show usage analysis progress
            if (_analyzingUsages)
            {
                EditorGUILayout.BeginHorizontal();
                CommonUIStyles.DrawProgressBar((float)_analysisProgress / _analysisTotal, $"Analyzing usage: {_analysisProgress:N0}/{_analysisTotal:N0} (optional)");
                if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false), GUILayout.Height(14)))
                {
                    _cancellationRequested = true;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            // Tree View
            Rect treeRect = GUILayoutUtility.GetRect(0, 10000, 0, 10000);
            _treeView.OnGUI(treeRect);

            EditorGUILayout.Space();
            _deleteEmptyFolders = EditorGUILayout.ToggleLeft("Delete Empty Folders", _deleteEmptyFolders);
            EditorGUILayout.Space();

            int countToDelete = CountSelectedFiles();
            EditorGUI.BeginDisabledGroup(countToDelete <= 0);
            if (GUILayout.Button($"Delete {countToDelete:N0} Files", CommonUIStyles.mainButton, GUILayout.Height(CommonUIStyles.BIG_BUTTON_HEIGHT)))
            {
                DeleteSelectedFiles();
            }
            EditorGUI.EndDisabledGroup();
        }

        private int CountSelectedFiles()
        {
            return _treeView.Model.GetData()
                .Count(e => e.Depth >= 0 && !e.IsFolder && e.IsSelected);
        }

        private void DeleteSelectedFiles()
        {
            List<string> filesToDelete = _treeView.Model.GetData()
                .Where(e => e.Depth >= 0 && !e.IsFolder && e.IsSelected)
                .Select(e => e.Path)
                .ToList();

            if (filesToDelete.Count == 0) return;

            // Check if usage analysis is still running
            string message = $"Are you sure you want to delete {filesToDelete.Count:N0} files?\nThis cannot be undone.";
            if (_analyzingUsages)
            {
                message = $"Usage analysis is still running. Some files may be used by other assets.\n\nAre you sure you want to delete {filesToDelete.Count:N0} files?\nThis cannot be undone.";
            }

            bool confirmed = EditorUtility.DisplayDialog("Confirm Delete", message, "Delete", "Cancel");

            if (!confirmed) return;

            // Cancel analysis if still running
            if (_analyzingUsages)
            {
                _cancellationRequested = true;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string file in filesToDelete)
                {
                    AssetDatabase.DeleteAsset(file);
                }

                if (_deleteEmptyFolders)
                {
                    CleanEmptyFolders(filesToDelete);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            AI.TriggerPackageRefresh();
            Close();
        }

        private void CleanEmptyFolders(List<string> deletedFiles)
        {
            HashSet<string> directories = new HashSet<string>();
            foreach (string file in deletedFiles)
            {
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir)) directories.Add(dir);
            }

            bool deleted;
            do
            {
                deleted = false;
                List<string> currentDirs = directories.ToList();
                directories.Clear();

                foreach (string dir in currentDirs)
                {
                    if (Directory.Exists(dir) && IOUtils.IsDirectoryEmpty(dir))
                    {
                        AssetDatabase.DeleteAsset(dir);
                        deleted = true;

                        string parent = Path.GetDirectoryName(dir);
                        if (!string.IsNullOrEmpty(parent) && parent.Contains("Assets"))
                        {
                            directories.Add(parent);
                        }
                    }
                }
            } while (deleted);
        }

        private void OnInspectorUpdate()
        {
            if (_analyzingUsages) Repaint();
        }

        private void OnDestroy()
        {
            if (_analyzingUsages)
            {
                _cancellationRequested = true;
            }
        }
    }
}
