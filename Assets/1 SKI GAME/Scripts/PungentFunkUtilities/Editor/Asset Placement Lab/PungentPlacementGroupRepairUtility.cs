using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal static class PungentPlacementGroupRepairUtility
    {
        public static string RevalidateSelectedGroup()
        {
            PungentPlacementGroup group = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<PungentPlacementGroup>() : null;
            if (group == null)
                return "No selected PungentPlacementGroup.";

            PungentPlacedAssetMarker[] markers = group.GetComponentsInChildren<PungentPlacedAssetMarker>(true);
            int missingPrefab = 0;
            int locked = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null)
                    continue;
                if (markers[i].sourcePrefab == null)
                    missingPrefab++;
                if (markers[i].locked)
                    locked++;
            }

            return $"Group '{group.name}': {markers.Length} marker(s), {missingPrefab} missing source prefab(s), {locked} locked member(s). Full regeneration requires richer per-candidate metadata, so repair actions are conservative.";
        }

        public static string RemoveOrphanMarkers()
        {
            PungentPlacedAssetMarker[] markers = Object.FindObjectsOfType<PungentPlacedAssetMarker>();
            int removed = 0;
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                PungentPlacedAssetMarker marker = markers[i];
                if (marker == null)
                    continue;
                bool orphan = marker.GetComponentInParent<PungentPlacementGroup>() == null && string.IsNullOrWhiteSpace(marker.groupId);
                if (!orphan)
                    continue;
                Undo.DestroyObjectImmediate(marker);
                removed++;
            }

            return $"Removed {removed} orphan marker component(s). Objects were preserved.";
        }

        public static string SelectGeneratedChildren()
        {
            PungentPlacementGroup group = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<PungentPlacementGroup>() : null;
            if (group == null)
                return "No selected PungentPlacementGroup.";

            PungentPlacedAssetMarker[] markers = group.GetComponentsInChildren<PungentPlacedAssetMarker>(true);
            List<Object> objects = new List<Object>();
            for (int i = 0; i < markers.Length; i++)
                if (markers[i] != null)
                    objects.Add(markers[i].gameObject);
            Selection.objects = objects.ToArray();
            return $"Selected {objects.Count} generated child object(s).";
        }

        public static string FindMissingSourcePrefabs()
        {
            PungentPlacedAssetMarker[] markers = Selection.gameObjects.Length > 0
                ? GetSelectedMarkers()
                : Object.FindObjectsOfType<PungentPlacedAssetMarker>();
            int missing = 0;
            for (int i = 0; i < markers.Length; i++)
                if (markers[i] != null && markers[i].sourcePrefab == null)
                    missing++;
            return $"Checked {markers.Length} marker(s): {missing} missing source prefab reference(s).";
        }

        public static string ReplaceSourcePrefabForSelection()
        {
            GameObject replacement = Selection.activeObject as GameObject;
            if (replacement == null)
                return "Select a prefab asset as the active object before replacing source prefab references.";

            PungentPlacedAssetMarker[] markers = GetSelectedMarkers();
            int changed = 0;
            int skipped = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null || markers[i].locked)
                {
                    skipped++;
                    continue;
                }
                Undo.RecordObject(markers[i], "Replace Placement Source Prefab");
                markers[i].sourcePrefab = replacement;
                EditorUtility.SetDirty(markers[i]);
                changed++;
            }

            return $"Updated {changed} marker source prefab reference(s); skipped {skipped} locked/missing marker(s).";
        }

        public static string RealignSelectedMarkersToSurface(PungentPlacementRuleSetSO ruleSet)
        {
            PungentPlacedAssetMarker[] markers = GetSelectedMarkers();
            int changed = 0;
            int failed = 0;
            int skipped = 0;
            LayerMask mask = ruleSet != null ? ruleSet.surfaceMask : ~0;
            float start = ruleSet != null ? ruleSet.raycastStartHeight : 50f;
            float distance = ruleSet != null ? ruleSet.raycastDistance : 150f;
            float offset = ruleSet != null ? ruleSet.surfaceOffset : 0f;

            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null || markers[i].locked)
                {
                    skipped++;
                    continue;
                }

                Transform t = markers[i].transform;
                if (Physics.Raycast(t.position + Vector3.up * start, Vector3.down, out RaycastHit hit, distance, mask))
                {
                    Undo.RecordObject(t, "Re-align Placement Marker To Surface");
                    t.position = hit.point + hit.normal * offset;
                    changed++;
                }
                else
                {
                    failed++;
                }
            }

            return $"Re-aligned {changed} marker(s) to surface; {failed} failed, {skipped} locked/missing marker(s) skipped.";
        }

        private static PungentPlacedAssetMarker[] GetSelectedMarkers()
        {
            List<PungentPlacedAssetMarker> markers = new List<PungentPlacedAssetMarker>();
            HashSet<PungentPlacedAssetMarker> seen = new HashSet<PungentPlacedAssetMarker>();
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null)
                    continue;

                PungentPlacedAssetMarker marker = selected[i].GetComponent<PungentPlacedAssetMarker>();
                if (marker != null && seen.Add(marker))
                    markers.Add(marker);

                PungentPlacedAssetMarker[] children = selected[i].GetComponentsInChildren<PungentPlacedAssetMarker>(true);
                for (int child = 0; child < children.Length; child++)
                    if (children[child] != null && seen.Add(children[child]))
                        markers.Add(children[child]);
            }

            return markers.ToArray();
        }
    }
#endif
}
