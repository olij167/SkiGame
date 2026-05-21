using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public static class PungentPlacementAssetSetBuilder
    {
        public static PungentPlacementAssetSetSO CreateAssetSetFromSelection(string assetPath)
        {
            GameObject[] prefabs = Selection.gameObjects;
            if (prefabs == null || prefabs.Length == 0)
            {
                EditorUtility.DisplayDialog("Asset Placement Lab", "Select one or more prefab assets first.", "OK");
                return null;
            }

            PungentPlacementAssetSetSO set = ScriptableObject.CreateInstance<PungentPlacementAssetSetSO>();
            set.displayName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabs[i]) as GameObject;
                if (prefab == null && PrefabUtility.IsPartOfPrefabAsset(prefabs[i]))
                    prefab = prefabs[i];
                if (prefab == null)
                    continue;

                set.AddPrefab(prefab);
                PungentPlacementAssetEntry entry = set.entries[set.entries.Count - 1];
                AutoPopulateFootprint(entry);
            }

            string unique = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(set, unique);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);
            return set;
        }

        public static void AddSelectedPrefabs(PungentPlacementAssetSetSO set)
        {
            if (set == null)
                return;

            Undo.RecordObject(set, "Add Selected Prefabs To Placement Asset Set");
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(selected[i]) as GameObject;
                if (prefab == null && PrefabUtility.IsPartOfPrefabAsset(selected[i]))
                    prefab = selected[i];
                if (prefab == null)
                    continue;

                int before = set.entries.Count;
                set.AddPrefab(prefab);
                if (set.entries.Count > before)
                    AutoPopulateFootprint(set.entries[set.entries.Count - 1]);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        public static void AutoPopulateFootprint(PungentPlacementAssetEntry entry)
        {
            if (entry == null || entry.prefab == null)
                return;

            Bounds bounds = PungentPlacementBoundsUtility.CalculateHierarchyBounds(entry.prefab, true);
            float cell = Mathf.Max(0.25f, entry.footprint != null ? entry.footprint.SafeCellSize : 1f);
            int x = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cell));
            int z = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / cell));

            if (entry.footprint == null)
                entry.footprint = new PungentPlacementFootprint();
            entry.footprint.size = new Vector2Int(x, z);
            entry.footprint.cellSize = cell;
            entry.footprint.useFootprint = true;
        }
    }
    #endif

}