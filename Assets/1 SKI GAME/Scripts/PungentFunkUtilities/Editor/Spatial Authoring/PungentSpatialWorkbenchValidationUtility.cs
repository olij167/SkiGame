using System.Collections.Generic;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal struct PungentSpatialWorkbenchIssue
    {
        public MessageType Severity;
        public string Code;
        public string Message;
        public Object Context;
    }

    internal static class PungentSpatialWorkbenchValidationUtility
    {
        private const int HighPointCountWarning = 512;
        private const int HighDrawCostWarning = 1800;

        public static void ValidateAsset(PungentSpatialAuthoringAsset asset, IList<PungentSpatialWorkbenchIssue> issues)
        {
            if (issues == null)
                return;
            issues.Clear();

            if (asset == null)
            {
                Add(issues, MessageType.Info, "spatial-no-asset", "Create or assign a Spatial Authoring Asset to edit document-backed paths, areas, regions, and backgrounds.", null);
                return;
            }

            asset.Normalize();
            if (!asset.projection.IsValid)
                Add(issues, MessageType.Error, "projection-invalid", "Projection bounds are invalid. Top-down Plan and Bake workflows need non-zero X/Z bounds.", asset);
            if (asset.backgroundTexture == null)
                Add(issues, MessageType.Warning, "background-missing", "No background texture is assigned. Plan view will use a grid until a background is assigned or baked.", asset);

            Dictionary<string, Object> stableIds = new Dictionary<string, Object>(System.StringComparer.OrdinalIgnoreCase);
            ScanId(asset.metadata.StableId, "asset", asset, stableIds, issues);

            for (int i = 0; i < SafeCount(asset.paths); i++)
            {
                PungentSpatialPath path = asset.paths[i];
                if (path == null)
                    continue;
                ScanId(path.StableId, $"path {i + 1}", asset, stableIds, issues);
                int count = path.worldPoints == null ? 0 : path.worldPoints.Count;
                if (count < 2)
                    Add(issues, MessageType.Warning, "path-too-few-points", $"{path.DisplayName} needs at least two points.", asset);
                if (count >= HighPointCountWarning)
                    Add(issues, MessageType.Warning, "path-high-point-count", $"{path.DisplayName} has {count} points. Prefer selected-only labels or split heavy authoring work.", asset);
            }

            for (int i = 0; i < SafeCount(asset.areas); i++)
            {
                PungentSpatialArea area = asset.areas[i];
                if (area == null)
                    continue;
                ScanId(area.StableId, $"area {i + 1}", asset, stableIds, issues);
                Vector3[] polygon = area.GetWorldPolygon();
                int count = polygon == null ? 0 : polygon.Length;
                if (area.shape == PungentSpatialAreaShapeMode.Polygon && count < 3)
                    Add(issues, MessageType.Warning, "area-too-few-points", $"{area.DisplayName} needs at least three polygon points.", asset);
                if (area.shape == PungentSpatialAreaShapeMode.Polygon && HasSelfIntersectionXZ(polygon))
                    Add(issues, MessageType.Error, "area-self-intersection", $"{area.DisplayName} has crossing polygon edges.", asset);
                if (count >= HighPointCountWarning)
                    Add(issues, MessageType.Warning, "area-high-point-count", $"{area.DisplayName} has {count} boundary points. Rich labels/fills may be expensive.", asset);
            }

            for (int i = 0; i < SafeCount(asset.regionSets); i++)
            {
                PungentSpatialRegionSet set = asset.regionSets[i];
                if (set == null)
                    continue;
                ScanId(set.Metadata.StableId, $"region set {i + 1}", asset, stableIds, issues);
                if (set.faces == null || set.faces.Count == 0)
                    Add(issues, MessageType.Warning, "region-set-empty", $"{set.Metadata.DisplayNameOrFallback($"Region Set {i + 1}")} has no region faces.", asset);
                if (set.vertices == null || set.vertices.Count == 0)
                    Add(issues, MessageType.Warning, "region-set-no-vertices", $"{set.Metadata.DisplayNameOrFallback($"Region Set {i + 1}")} has no shared vertices.", asset);
            }
        }

        public static void ValidateCache(PungentSpatialProviderCache cache, IList<PungentSpatialWorkbenchIssue> issues)
        {
            if (cache == null || issues == null)
                return;

            if (cache.DrawCost >= HighDrawCostWarning)
                Add(issues, MessageType.Warning, "draw-density", $"Cached spatial providers estimate {cache.DrawCost} draw operations. Use selected-only drawing or hide heavy records if Scene View slows down.", null);
        }

        private static void ScanId(string stableId, string label, Object context, IDictionary<string, Object> stableIds, IList<PungentSpatialWorkbenchIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                Add(issues, MessageType.Warning, "missing-stable-id", $"Missing stable ID on {label}.", context);
                return;
            }

            if (stableIds.TryGetValue(stableId, out Object existing) && existing != context)
                Add(issues, MessageType.Warning, "duplicate-stable-id", $"Duplicate stable ID '{stableId}' found on {label}.", context);
            else
                stableIds[stableId] = context;
        }

        private static void Add(IList<PungentSpatialWorkbenchIssue> issues, MessageType severity, string code, string message, Object context)
        {
            issues.Add(new PungentSpatialWorkbenchIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                Context = context
            });
        }

        private static int SafeCount<T>(IList<T> list)
        {
            return list == null ? 0 : list.Count;
        }

        private static bool HasSelfIntersectionXZ(IReadOnlyList<Vector3> polygon)
        {
            if (polygon == null || polygon.Count < 4)
                return false;

            List<Vector2> points = new List<Vector2>(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
                points.Add(new Vector2(polygon[i].x, polygon[i].z));
            return PungentSpatialRegionUtility.HasSelfIntersection(points);
        }
    }
#endif
}
