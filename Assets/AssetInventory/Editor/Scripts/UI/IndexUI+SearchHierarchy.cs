using System;
using System.Collections.Generic;
using System.Linq;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
#pragma warning disable CS0618 // Type or member is obsolete
#if UNITY_6000_2_OR_NEWER
using BaseTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using BaseTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace AssetInventory
{
    public partial class IndexUI
    {
        private Vector2 _leftSidebarScrollPos;

        private static readonly string[] _hierarchyTypes = {"File Path", "Category", "Publisher", "Package", "File Type"};
        private BaseTreeViewState _hierarchyTreeState;
        private HierarchyTreeViewControl _hierarchyTreeView;
        private TreeModel<HierarchyTreeElement> _hierarchyTreeModel;
        private bool _requireHierarchyRebuild;
        private string _activeHierarchyFilter;
        private string _activeHierarchyFilterValue;

        private void DrawLeftHierarchySidebar()
        {
            GUILayout.BeginVertical("box", GUILayout.Width(UIStyles.INSPECTOR_WIDTH));

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            AI.Config.searchLeftSideBarHierarchy = EditorGUILayout.Popup(AI.Config.searchLeftSideBarHierarchy, _hierarchyTypes, EditorStyles.toolbarPopup);
            if (EditorGUI.EndChangeCheck())
            {
                AI.SaveConfig();
                _requireHierarchyRebuild = true;
            }

            if (!string.IsNullOrEmpty(_activeHierarchyFilter))
            {
                if (GUILayout.Button(CommonUIStyles.Content("✕", "Clear hierarchy filter"), EditorStyles.toolbarButton, GUILayout.Width(22)))
                {
                    ClearHierarchyFilter();
                }
            }
            GUILayout.EndHorizontal();

            if (_hierarchyTreeView == null || _requireHierarchyRebuild)
            {
                InitHierarchyTree();
                _requireHierarchyRebuild = false;
            }

            _leftSidebarScrollPos = GUILayout.BeginScrollView(_leftSidebarScrollPos, false, false);
            if (_hierarchyTreeView != null && _hierarchyTreeModel != null && _hierarchyTreeModel.Root != null)
            {
                Rect treeRect = GUILayoutUtility.GetRect(0, 10000, 0, 10000);
                _hierarchyTreeView.OnGUI(treeRect);
            }
            else
            {
                EditorGUILayout.HelpBox("No hierarchy data available. Perform a search first.", MessageType.Info);
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void InitHierarchyTree()
        {
            if (_hierarchyTreeState == null)
            {
                _hierarchyTreeState = new BaseTreeViewState();
            }

            List<HierarchyTreeElement> elements = BuildHierarchyElements();

            _hierarchyTreeModel = new TreeModel<HierarchyTreeElement>(elements);
            _hierarchyTreeView = new HierarchyTreeViewControl(_hierarchyTreeState, _hierarchyTreeModel);
            _hierarchyTreeView.OnSelectionChanged += OnHierarchySelectionChanged;
        }

        private List<HierarchyTreeElement> BuildHierarchyElements()
        {
            List<HierarchyTreeElement> elements = new List<HierarchyTreeElement>();

            HierarchyTreeElement root = new HierarchyTreeElement("Root", -1, 0);
            elements.Add(root);

            if (_files == null || _files.Count == 0)
            {
                return elements;
            }

            int idCounter = 1;

            switch (AI.Config.searchLeftSideBarHierarchy)
            {
                case 0:
                    BuildPathHierarchy(elements, ref idCounter);
                    break;
                case 1:
                    BuildCategoryHierarchy(elements, ref idCounter);
                    break;
                case 2:
                    BuildPublisherHierarchy(elements, ref idCounter);
                    break;
                case 3:
                    BuildPackageHierarchy(elements, ref idCounter);
                    break;
                case 4:
                    BuildTypeHierarchy(elements, ref idCounter);
                    break;
            }

            return elements;
        }

        private void BuildPathHierarchy(List<HierarchyTreeElement> elements, ref int idCounter)
        {
            Dictionary<string, int> pathCounts = new Dictionary<string, int>();
            Dictionary<string, string> pathNames = new Dictionary<string, string>();
            HashSet<string> allPaths = new HashSet<string>();

            foreach (AssetInfo file in _files)
            {
                if (string.IsNullOrEmpty(file.Path)) continue;

                string[] parts = file.Path.Split('/');
                string currentPath = "";

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string part = parts[i];
                    if (string.IsNullOrEmpty(part)) continue;

                    currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;

                    if (!pathCounts.ContainsKey(currentPath))
                    {
                        pathCounts[currentPath] = 0;
                        pathNames[currentPath] = part;
                    }
                    allPaths.Add(currentPath);
                    pathCounts[currentPath]++;
                }
            }

            // Build tree using depth-first traversal
            AddHierarchyElementsDepthFirst(elements, allPaths, pathNames, pathCounts, "Path", ref idCounter);
        }

        private void BuildCategoryHierarchy(List<HierarchyTreeElement> elements, ref int idCounter)
        {
            Dictionary<string, int> categoryCounts = new Dictionary<string, int>();
            Dictionary<string, string> categoryNames = new Dictionary<string, string>();
            HashSet<string> allCategories = new HashSet<string>();

            foreach (AssetInfo file in _files)
            {
                string category = file.DisplayCategory ?? "Uncategorized";

                // Count at leaf level
                if (!categoryCounts.ContainsKey(category))
                {
                    categoryCounts[category] = 0;
                }
                categoryCounts[category]++;

                // Create all path segments
                string[] parts = category.Split('/');
                string currentPath = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? parts[i] : currentPath + "/" + parts[i];
                    if (!categoryNames.ContainsKey(currentPath))
                    {
                        categoryNames[currentPath] = parts[i];
                    }
                    allCategories.Add(currentPath);
                }
            }

            // Build tree using depth-first traversal
            AddHierarchyElementsDepthFirst(elements, allCategories, categoryNames, categoryCounts, "Category", ref idCounter);
        }

        private void AddHierarchyElementsDepthFirst(List<HierarchyTreeElement> elements, HashSet<string> allPaths,
            Dictionary<string, string> pathNames, Dictionary<string, int> pathCounts, string filterKey, ref int idCounter)
        {
            // Build parent-children relationship
            Dictionary<string, List<string>> childrenMap = new Dictionary<string, List<string>>();
            childrenMap[""] = new List<string>(); // root's children

            foreach (string path in allPaths)
            {
                int lastSlash = path.LastIndexOf('/');
                string parent = lastSlash > 0 ? path.Substring(0, lastSlash) : "";

                if (!childrenMap.ContainsKey(parent))
                {
                    childrenMap[parent] = new List<string>();
                }
                childrenMap[parent].Add(path);
            }

            // Sort children at each level
            foreach (List<string> children in childrenMap.Values)
            {
                children.Sort(StringComparer.OrdinalIgnoreCase);
            }

            // Depth-first traversal
            TraverseHierarchy(elements, "", 0, childrenMap, pathNames, pathCounts, filterKey, ref idCounter);
        }

        private const int MAX_HIERARCHY_DEPTH = 100;

        private void TraverseHierarchy(List<HierarchyTreeElement> elements, string parentPath, int depth,
            Dictionary<string, List<string>> childrenMap, Dictionary<string, string> pathNames,
            Dictionary<string, int> pathCounts, string filterKey, ref int idCounter)
        {
            if (depth > MAX_HIERARCHY_DEPTH) return;
            if (!childrenMap.ContainsKey(parentPath)) return;

            foreach (string path in childrenMap[parentPath])
            {
                string name = pathNames.ContainsKey(path) ? pathNames[path] : path;
                int count = pathCounts.ContainsKey(path) ? pathCounts[path] : 0;

                // For categories, sum up all descendant counts
                if (filterKey == "Category")
                {
                    count = pathCounts.Where(kvp => kvp.Key == path || kvp.Key.StartsWith(path + "/")).Sum(kvp => kvp.Value);
                }

                elements.Add(new HierarchyTreeElement(name, depth, idCounter++, filterKey, path, path, count));

                // Recurse to children
                TraverseHierarchy(elements, path, depth + 1, childrenMap, pathNames, pathCounts, filterKey, ref idCounter);
            }
        }

        private void BuildPublisherHierarchy(List<HierarchyTreeElement> elements, ref int idCounter)
        {
            Dictionary<string, int> publisherCounts = new Dictionary<string, int>();

            foreach (AssetInfo file in _files)
            {
                string publisher = file.DisplayPublisher ?? "Unknown";
                if (!publisherCounts.ContainsKey(publisher))
                {
                    publisherCounts[publisher] = 0;
                }
                publisherCounts[publisher]++;
            }

            foreach (KeyValuePair<string, int> kvp in publisherCounts.OrderByDescending(x => x.Value))
            {
                elements.Add(new HierarchyTreeElement(kvp.Key, 0, idCounter++, "Publisher", kvp.Key, kvp.Key, kvp.Value));
            }
        }

        private void BuildPackageHierarchy(List<HierarchyTreeElement> elements, ref int idCounter)
        {
            Dictionary<string, int> packageCounts = new Dictionary<string, int>();

            foreach (AssetInfo file in _files)
            {
                string package = file.DisplayName ?? file.SafeName ?? "Unknown";
                if (!packageCounts.ContainsKey(package))
                {
                    packageCounts[package] = 0;
                }
                packageCounts[package]++;
            }

            foreach (KeyValuePair<string, int> kvp in packageCounts.OrderByDescending(x => x.Value))
            {
                elements.Add(new HierarchyTreeElement(kvp.Key, 0, idCounter++, "Package", kvp.Key, kvp.Key, kvp.Value));
            }
        }

        private void BuildTypeHierarchy(List<HierarchyTreeElement> elements, ref int idCounter)
        {
            Dictionary<string, int> typeCounts = new Dictionary<string, int>();

            foreach (AssetInfo file in _files)
            {
                string type = file.Type ?? "Unknown";
                if (!typeCounts.ContainsKey(type))
                {
                    typeCounts[type] = 0;
                }
                typeCounts[type]++;
            }

            foreach (KeyValuePair<string, int> kvp in typeCounts.OrderByDescending(x => x.Value))
            {
                elements.Add(new HierarchyTreeElement(kvp.Key, 0, idCounter++, "Type", kvp.Key, kvp.Key, kvp.Value));
            }
        }

        private void OnHierarchySelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0) return;

            HierarchyTreeElement element = _hierarchyTreeModel.Find(selectedIds[0]);
            if (element == null || string.IsNullOrEmpty(element.FilterKey)) return;

            ApplyHierarchyFilter(element);
        }

        private void ApplyHierarchyFilter(HierarchyTreeElement element)
        {
            _activeHierarchyFilter = element.FilterKey;
            _activeHierarchyFilterValue = element.FilterValue;

            switch (element.FilterKey)
            {
                case "Path":
                    _searchPhrase = $"=Path like '{element.FilterValue}%'";
                    _previousSearchPhrase = _searchPhrase;
                    break;

                case "Category":
                    _selectedCategory = FindIndexByValue(_categoryNames, element.FilterValue, splitPath: false);
                    break;

                case "Publisher":
                    _selectedPublisher = FindIndexByValue(_publisherNames, element.FilterValue, splitPath: true);
                    break;

                case "Package":
                    _selectedAsset = FindIndexByValue(_assetNames, element.FilterValue, splitPath: true);
                    break;

                case "Type":
                    int typeIdx = Array.FindIndex(_types, t => t.Equals(element.FilterValue, StringComparison.OrdinalIgnoreCase) ||
                        t.EndsWith("/" + element.FilterValue, StringComparison.OrdinalIgnoreCase));
                    if (typeIdx >= 0) AI.Config.searchType = typeIdx;
                    break;
            }

            _requireSearchUpdate = true;
            _curPage = 1;
        }

        private void ClearHierarchyFilter()
        {
            _activeHierarchyFilter = null;
            _activeHierarchyFilterValue = null;

            ResetSearch(true, false);
            _requireSearchUpdate = true;
        }
    }
}
