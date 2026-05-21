using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    public static class PungentPlacementApplyUtility
    {
        public static GameObject ApplyResult(PungentPlacementResult result, PungentPlacementContext context, string groupName, bool selectedOnly = false, IReadOnlyList<int> selectedIndices = null)
        {
            if (result == null || context == null)
                return null;

            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Transform parent = context.outputParent;
            PungentPlacementRuleSetSO rules = context.ruleSet;

            GameObject groupObject = null;
            if (rules == null || rules.createGroupObject)
            {
                groupObject = new GameObject(string.IsNullOrWhiteSpace(groupName) ? $"Placement Group {id}" : groupName);
                Undo.RegisterCreatedObjectUndo(groupObject, "Create Placement Group");
                if (parent != null)
                    groupObject.transform.SetParent(parent, false);

                PungentPlacementGroup group = Undo.AddComponent<PungentPlacementGroup>(groupObject);
                group.groupId = id;
                group.displayName = groupObject.name;
                group.moduleId = "area-scatter";
                group.seed = context.seed;
                group.assetSet = context.assetSet;
                group.ruleSet = context.ruleSet;
                parent = groupObject.transform;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Asset Placement");
            int applied = 0;

            for (int i = 0; i < result.candidates.Count; i++)
            {
                PungentPlacementCandidate candidate = result.candidates[i];
                if (candidate == null || !candidate.Accepted || candidate.prefab == null)
                    continue;

                if (selectedOnly && (selectedIndices == null || !ContainsIndex(selectedIndices, i)))
                    continue;

                GameObject instance = InstantiatePrefab(candidate.prefab, candidate.position, candidate.rotation, parent);
                if (instance == null)
                    continue;

                Undo.RegisterCreatedObjectUndo(instance, "Place Asset");
                instance.name = BuildObjectName(rules, candidate, applied);
                instance.transform.position = candidate.position;
                instance.transform.rotation = candidate.rotation;
                instance.transform.localScale = candidate.scale;

                if (rules == null || rules.addPlacementMarker)
                {
                    PungentPlacedAssetMarker marker = instance.GetComponent<PungentPlacedAssetMarker>();
                    if (marker == null)
                        marker = Undo.AddComponent<PungentPlacedAssetMarker>(instance);

                    marker.groupId = id;
                    marker.moduleId = "area-scatter";
                    marker.seed = context.seed;
                    marker.sourcePrefab = candidate.prefab;
                    marker.originalEstimatedBounds = candidate.estimatedBounds;
                    marker.generatedAtUtc = DateTime.UtcNow.ToString("u");
                }

                applied++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (groupObject != null && applied == 0)
                Undo.DestroyObjectImmediate(groupObject);

            if (applied > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return groupObject;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject instance = null;
            UnityEngine.Object prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            if (prefabAsset == null)
                prefabAsset = prefab;

            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;
            }
            catch
            {
                instance = null;
            }

            if (instance == null)
                instance = UnityEngine.Object.Instantiate(prefab, parent);

            instance.transform.position = position;
            instance.transform.rotation = rotation;
            return instance;
        }

        private static bool ContainsIndex(IReadOnlyList<int> indices, int index)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] == index)
                    return true;
            }

            return false;
        }

        private static string BuildObjectName(PungentPlacementRuleSetSO rules, PungentPlacementCandidate candidate, int appliedIndex)
        {
            string prefix = rules != null && !string.IsNullOrWhiteSpace(rules.generatedObjectPrefix) ? rules.generatedObjectPrefix : "Placed";
            string baseName = candidate.assetEntry != null ? candidate.assetEntry.DisplayName : candidate.prefab.name;
            return $"{prefix}_{baseName}_{appliedIndex:000}";
        }
    }
    #endif

}