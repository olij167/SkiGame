using PungentFunk.Utilities.Placement;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.Placement
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    internal static class PungentPlacementPathIntegrationUtility
    {
        public static void OpenPlacementLabWithPath(ModularPathSpawner path)
        {
            AssetPlacementLabWindow.OpenWithPath(path);
        }

        public static ModularPathSpawner ResolvePathSource(ModularPathSpawner explicitSource)
        {
            if (explicitSource != null)
                return explicitSource;
            return Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<ModularPathSpawner>() : null;
        }

        public static bool TryBuildPathBounds(ModularPathSpawner source, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
            ModularPathSpawner path = ResolvePathSource(source);
            if (path == null || !path.BuildSampledWorldPath(out List<Vector3> points, out _) || points.Count == 0)
                return false;

            bounds = new Bounds(points[0], Vector3.one);
            for (int i = 1; i < points.Count; i++)
                bounds.Encapsulate(points[i]);
            return true;
        }

        public static bool TryGeneratePathPreview(
            ModularPathSpawner source,
            PungentPlacementAssetSetSO assetSet,
            PungentPlacementRuleSetSO ruleSet,
            int seed,
            int requestedCount,
            out PungentPlacementResult result,
            out string status)
        {
            result = new PungentPlacementResult();
            ModularPathSpawner path = ResolvePathSource(source);
            if (path == null || !path.BuildSampledWorldPath(out List<Vector3> points, out _))
            {
                status = "Assign a ModularPathSpawner before generating path placement.";
                return false;
            }

            System.Random random = new System.Random(seed);
            int step = Mathf.Max(1, Mathf.FloorToInt(points.Count / Mathf.Max(1, requestedCount)));
            for (int i = 0, index = 0; i < points.Count && result.candidates.Count < requestedCount; i += step, index++)
            {
                PungentPlacementAssetEntry entry = assetSet != null ? assetSet.Pick(random) : null;
                if (entry == null || entry.prefab == null)
                    continue;

                Vector3 normal = Vector3.up;
                Vector3 position = points[i];
                if (ruleSet != null)
                {
                    Vector3 origin = position + Vector3.up * Mathf.Max(0.01f, ruleSet.raycastStartHeight);
                    if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, ruleSet.raycastDistance, ruleSet.surfaceMask))
                    {
                        position = hit.point + hit.normal * ruleSet.surfaceOffset;
                        normal = hit.normal;
                    }
                }

                result.candidates.Add(new PungentPlacementCandidate
                {
                    prefab = entry.prefab,
                    assetEntry = entry,
                    position = position + entry.positionOffset,
                    rotation = entry.GetRandomRotation(random, normal),
                    scale = entry.GetRandomScale(random),
                    surfaceNormal = normal,
                    state = PungentPlacementCandidateState.Accepted,
                    index = index
                });
            }

            result.Recount();
            status = "Path preview generated: " + result.summary;
            return true;
        }
    }
#endif
}
