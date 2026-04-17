#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildingPrefabExporter
{
    [MenuItem("Tools/Buildings/Save Selected Generated Building As Prefab")]
    private static void SaveSelectedBuildingAsPrefab()
    {
        var root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Select the generated building root GameObject first.", "OK");
            return;
        }

        string prefabPath = EditorUtility.SaveFilePanelInProject(
            "Save Building Prefab",
            root.name,
            "prefab",
            "Choose where to save the generated building prefab.");

        if (string.IsNullOrEmpty(prefabPath))
            return;

        string folder = Path.GetDirectoryName(prefabPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(folder))
        {
            EditorUtility.DisplayDialog("Invalid Path", "Could not determine prefab folder.", "OK");
            return;
        }

        string meshFolder = folder + "/" + Path.GetFileNameWithoutExtension(prefabPath) + "_Meshes";
        EnsureFolderExists(meshFolder);

        // Work on a temporary duplicate so the current scene object is not mutated unexpectedly
        GameObject tempRoot = Object.Instantiate(root);
        tempRoot.name = root.name;

        try
        {
            SaveMeshesAsAssets(tempRoot, meshFolder);
            PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath, out bool success);

            if (!success)
            {
                EditorUtility.DisplayDialog("Prefab Save Failed", "Unity reported that the prefab could not be saved. Check the console.", "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", $"Saved prefab to:\n{prefabPath}", "OK");
        }
        finally
        {
            Object.DestroyImmediate(tempRoot);
        }
    }

    private static void SaveMeshesAsAssets(GameObject root, string meshFolder)
    {
        var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null)
                continue;

            Mesh sourceMesh = mf.sharedMesh;
            Mesh meshCopy = Object.Instantiate(sourceMesh);
            meshCopy.name = string.IsNullOrWhiteSpace(sourceMesh.name)
                ? mf.gameObject.name + "_Mesh"
                : sourceMesh.name;

            string safeName = SanitizeFileName(meshCopy.name);
            string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}.asset");

            AssetDatabase.CreateAsset(meshCopy, meshPath);
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        }
    }

    private static void EnsureFolderExists(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath))
            return;

        string[] parts = assetFolderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }
        return value.Replace(" ", "_");
    }

    [MenuItem("Tools/Buildings/Save Selected Generated Building As Prefab", true)]
    private static bool ValidateSaveSelectedBuildingAsPrefab()
    {
        return Selection.activeGameObject != null;
    }
}
#endif