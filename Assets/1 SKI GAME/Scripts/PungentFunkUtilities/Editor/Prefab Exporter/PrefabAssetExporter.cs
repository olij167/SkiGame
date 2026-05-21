using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.PreviewExport
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Generic prefab export helper for scene/generated objects whose meshes should be saved as project assets.
    /// This replaces the old building-specific exporter while retaining its legacy menu as a compatibility alias.
    /// </summary>
    public static class PrefabAssetExporter
    {
        public static void SaveSelectedAsPrefabWithMeshAssets()
        {
            SaveSelectedWithOptions(new ExportOptions());
        }

        public static bool HasSelectedRoot
        {
            get { return Selection.activeGameObject != null; }
        }

        public static bool SaveSelectedWithOptions(ExportOptions options)
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Select the root GameObject to export first.", "OK");
                return false;
            }

            string prefabPath = EditorUtility.SaveFilePanelInProject(
                "Save Prefab",
                root.name,
                "prefab",
                "Choose where to save the prefab asset.");

            if (string.IsNullOrEmpty(prefabPath))
                return false;

            return SaveAsPrefabAsset(root, prefabPath, options, out _);
        }

        public static bool SaveAsPrefabAsset(GameObject root, string prefabPath, ExportOptions options, out ExportReport report)
        {
            if (options == null)
                options = new ExportOptions();

            report = new ExportReport
            {
                SourceName = root != null ? root.name : "none",
                PrefabPath = prefabPath
            };

            if (root == null)
            {
                report.Error = "Missing source GameObject.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                report.Error = "Missing prefab output path.";
                EditorUtility.DisplayDialog("Invalid Path", report.Error, "OK");
                return false;
            }

            string normalizedPrefabPath = prefabPath.Replace('\\', '/');
            if (!normalizedPrefabPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                report.Error = "Prefab output must be inside the Assets folder.";
                EditorUtility.DisplayDialog("Invalid Path", report.Error, "OK");
                return false;
            }

            string folder = Path.GetDirectoryName(normalizedPrefabPath);
            if (string.IsNullOrEmpty(folder))
            {
                report.Error = "Could not determine prefab folder.";
                EditorUtility.DisplayDialog("Invalid Path", report.Error, "OK");
                return false;
            }

            EnsureFolderExists(folder);

            bool connectSceneObject = options.ConnectSelectedSceneObjectToPrefab && !EditorUtility.IsPersistent(root);
            if (options.ConnectSelectedSceneObjectToPrefab && !connectSceneObject)
            {
                Debug.LogWarning("[PrefabAssetExporter] Connect Selected Scene Object was requested, but the selected object is already a persistent asset. Exporting without connecting the source object.", root);
            }

            GameObject workingRoot = connectSceneObject ? root : UnityEngine.Object.Instantiate(root);
            if (!connectSceneObject)
                workingRoot.name = root.name;

            try
            {
                if (connectSceneObject)
                    Undo.RegisterFullObjectHierarchyUndo(root, "Export Prefab With Mesh Assets");

                if (options.SaveMeshesAsAssets)
                {
                    string meshFolder = folder + "/" + Path.GetFileNameWithoutExtension(normalizedPrefabPath) + "_Meshes";
                    EnsureFolderExists(meshFolder);

                    try
                    {
                        AssetDatabase.StartAssetEditing();
                        SaveMeshesAsAssets(workingRoot, meshFolder, options, report);
                    }
                    catch (Exception ex)
                    {
                        report.Error = ex.Message;
                        Debug.LogException(ex);
                        EditorUtility.DisplayDialog("Mesh Export Failed", ex.Message, "OK");
                        return false;
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                }

                bool success;
                if (connectSceneObject)
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(root, normalizedPrefabPath, InteractionMode.UserAction, out success);
                    report.ConnectedSceneObject = success;
                }
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(workingRoot, normalizedPrefabPath, out success);
                }

                if (!success)
                {
                    report.Error = "Unity reported that the prefab could not be saved. Check the console for details.";
                    EditorUtility.DisplayDialog("Prefab Save Failed", report.Error, "OK");
                    return false;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string message =
                    "Saved prefab to:\n" + normalizedPrefabPath +
                    "\n\nMeshes duplicated: " + report.MeshesDuplicated +
                    "\nMeshes reused: " + report.MeshesReused +
                    (report.ConnectedSceneObject ? "\nConnected selected scene object to prefab." : string.Empty);

                EditorUtility.DisplayDialog("Prefab Export Complete", message, "OK");
                Debug.Log("[PrefabAssetExporter] " + message, root);
                return true;
            }
            finally
            {
                if (!connectSceneObject && workingRoot != null)
                    UnityEngine.Object.DestroyImmediate(workingRoot);
            }
        }

        private static void SaveMeshesAsAssets(GameObject root, string meshFolder, ExportOptions options, ExportReport report)
        {
            if (root == null || !options.SaveMeshesAsAssets)
                return;

            Dictionary<Mesh, Mesh> savedMeshes = new Dictionary<Mesh, Mesh>();

            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(options.IncludeDisabledChildren);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                meshFilter.sharedMesh = SaveOrReuseMesh(meshFilter.sharedMesh, meshFilter.gameObject.name, meshFolder, options, report, savedMeshes);
                EditorUtility.SetDirty(meshFilter);
            }

            if (!options.IncludeSkinnedMeshes)
                return;

            SkinnedMeshRenderer[] skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(options.IncludeDisabledChildren);
            for (int i = 0; i < skinnedMeshes.Length; i++)
            {
                SkinnedMeshRenderer skinnedMesh = skinnedMeshes[i];
                if (skinnedMesh == null || skinnedMesh.sharedMesh == null)
                    continue;

                skinnedMesh.sharedMesh = SaveOrReuseMesh(skinnedMesh.sharedMesh, skinnedMesh.gameObject.name, meshFolder, options, report, savedMeshes);
                EditorUtility.SetDirty(skinnedMesh);
            }
        }

        private static Mesh SaveOrReuseMesh(Mesh sourceMesh, string fallbackName, string meshFolder, ExportOptions options, ExportReport report, Dictionary<Mesh, Mesh> savedMeshes)
        {
            if (sourceMesh == null)
                return null;

            Mesh existingCopy;
            if (savedMeshes.TryGetValue(sourceMesh, out existingCopy))
            {
                report.MeshesReused++;
                return existingCopy;
            }

            if (options.OnlyDuplicateNonPersistentMeshes && EditorUtility.IsPersistent(sourceMesh))
            {
                savedMeshes[sourceMesh] = sourceMesh;
                report.MeshesReused++;
                return sourceMesh;
            }

            Mesh meshCopy = UnityEngine.Object.Instantiate(sourceMesh);
            meshCopy.name = string.IsNullOrWhiteSpace(sourceMesh.name) ? fallbackName + "_Mesh" : sourceMesh.name;

            string meshPath = AssetDatabase.GenerateUniqueAssetPath(meshFolder + "/" + SanitizeFileName(meshCopy.name) + ".asset");
            AssetDatabase.CreateAsset(meshCopy, meshPath);

            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Mesh finalMesh = savedMesh != null ? savedMesh : meshCopy;
            savedMeshes[sourceMesh] = finalMesh;
            report.MeshesDuplicated++;
            return finalMesh;
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (string.IsNullOrWhiteSpace(assetFolderPath))
                throw new InvalidOperationException("Asset folder path is empty.");

            string normalized = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("Output must be inside the Assets folder.");

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Mesh";

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                value = value.Replace(invalid[i], '_');

            return value.Replace(" ", "_");
        }

        public sealed class ExportOptions
        {
            public bool SaveMeshesAsAssets = true;
            public bool OnlyDuplicateNonPersistentMeshes = true;
            public bool IncludeDisabledChildren = true;
            public bool IncludeSkinnedMeshes = true;
            public bool ConnectSelectedSceneObjectToPrefab = false;
        }

        public sealed class ExportReport
        {
            public string SourceName;
            public string PrefabPath;
            public string Error;
            public int MeshesDuplicated;
            public int MeshesReused;
            public bool ConnectedSceneObject;
        }
    }
    #endif

}
