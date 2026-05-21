using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [Serializable]
    public struct PungentSpatialBackgroundBakeMetadata
    {
        public string sourceSummary;
        public string outputAssetPath;
        public Vector2Int textureSize;
        public Bounds captureBounds;
        public bool safeAlignedToProjection;
        public long unixTimeSeconds;
    }

    [CreateAssetMenu(menuName = "PungentFunk Utilities/Scene Authoring/Spatial Authoring Asset", fileName = "PungentSpatialAuthoringAsset")]
    public class PungentSpatialAuthoringAsset : ScriptableObject,
        IPungentSpatialItemProvider,
        IPungentSpatialPathProvider,
        IPungentSpatialAreaProvider,
        IPungentSpatialRegionProvider,
        IPungentSpatialProjectionProvider,
        IPungentSpatialBackgroundProvider
    {
        public const int CurrentSchemaVersion = 1;

        public PungentSpatialObjectMetadata metadata = new PungentSpatialObjectMetadata();
        public PungentSpatialProjection projection = PungentSpatialProjection.DefaultTopDownXZ;
        public Texture2D backgroundTexture;
        public Vector2 backgroundUvMin = Vector2.zero;
        public Vector2 backgroundUvMax = Vector2.one;
        public List<PungentSpatialLayer> layers = new List<PungentSpatialLayer>();
        public List<PungentSpatialPath> paths = new List<PungentSpatialPath>();
        public List<PungentSpatialArea> areas = new List<PungentSpatialArea>();
        public List<PungentSpatialRegionSet> regionSets = new List<PungentSpatialRegionSet>();
        public List<PungentSpatialMarker> markers = new List<PungentSpatialMarker>();
        public int schemaVersion = CurrentSchemaVersion;
        public PungentSpatialBackgroundBakeMetadata lastBackgroundBake;

        public int SpatialItemCount => SafeCount(paths) + SafeCount(areas) + SafeCount(regionSets) + SafeCount(markers) + (backgroundTexture != null ? 1 : 0);
        public int SpatialPathCount => SafeCount(paths);
        public int SpatialAreaCount => SafeCount(areas);
        public int SpatialRegionSetCount => SafeCount(regionSets);

        private void OnValidate()
        {
            Normalize();
        }

        public void Normalize()
        {
            if (metadata == null)
                metadata = new PungentSpatialObjectMetadata();
            metadata.Normalize(name);
            projection.Normalize();
            schemaVersion = Mathf.Max(1, schemaVersion);

            if (layers == null)
                layers = new List<PungentSpatialLayer>();
            if (paths == null)
                paths = new List<PungentSpatialPath>();
            if (areas == null)
                areas = new List<PungentSpatialArea>();
            if (regionSets == null)
                regionSets = new List<PungentSpatialRegionSet>();
            if (markers == null)
                markers = new List<PungentSpatialMarker>();

            for (int i = 0; i < paths.Count; i++)
                paths[i]?.Normalize($"Path {i + 1}");
            for (int i = 0; i < areas.Count; i++)
                areas[i]?.Normalize($"Area {i + 1}");
            for (int i = 0; i < regionSets.Count; i++)
                regionSets[i]?.Normalize($"Regions {i + 1}");
            for (int i = 0; i < markers.Count; i++)
                markers[i]?.Normalize($"Marker {i + 1}");
        }

        public bool TryGetSpatialProjection(out PungentSpatialProjection spatialProjection)
        {
            spatialProjection = projection;
            return projection.IsValid;
        }

        public bool TryGetSpatialBackground(out PungentSpatialBackgroundSnapshot background)
        {
            background = new PungentSpatialBackgroundSnapshot
            {
                Texture = backgroundTexture,
                UvMin = backgroundUvMin,
                UvMax = backgroundUvMax,
                Projection = projection,
                BakeSummary = lastBackgroundBake.sourceSummary,
                MayBeStale = backgroundTexture == null || !projection.IsValid
            };
            return backgroundTexture != null;
        }

        public bool TryGetSpatialPath(int index, out PungentSpatialPath path)
        {
            path = null;
            if (paths == null || index < 0 || index >= paths.Count)
                return false;
            path = paths[index];
            return path != null;
        }

        public bool TryGetSpatialArea(int index, out PungentSpatialArea area)
        {
            area = null;
            if (areas == null || index < 0 || index >= areas.Count)
                return false;
            area = areas[index];
            return area != null;
        }

        public bool TryGetSpatialRegionSet(int index, out PungentSpatialRegionSet regionSet)
        {
            regionSet = null;
            if (regionSets == null || index < 0 || index >= regionSets.Count)
                return false;
            regionSet = regionSets[index];
            return regionSet != null;
        }

        public bool TryGetSpatialItem(int index, out PungentSpatialItemSnapshot item)
        {
            item = default;
            Normalize();

            int cursor = 0;
            if (TryGetPathSnapshot(index, ref cursor, out item))
                return true;
            if (TryGetAreaSnapshot(index, ref cursor, out item))
                return true;
            if (TryGetRegionSnapshot(index, ref cursor, out item))
                return true;
            if (TryGetMarkerSnapshot(index, ref cursor, out item))
                return true;

            if (backgroundTexture != null && index == cursor)
            {
                item = new PungentSpatialItemSnapshot
                {
                    StableId = metadata.StableId + "-background",
                    DisplayName = backgroundTexture.name,
                    Kind = PungentSpatialItemKind.Background,
                    SourceObject = this,
                    Visible = true,
                    Locked = true,
                    WarningCount = projection.IsValid ? 0 : 1,
                    EstimatedDrawOperations = 1,
                    PointCount = 4,
                    Layer = "Background",
                    Category = "Background",
                    Color = Color.white,
                    Bounds = projection.GetWorldBounds()
                };
                return true;
            }

            return false;
        }

        private bool TryGetPathSnapshot(int index, ref int cursor, out PungentSpatialItemSnapshot item)
        {
            item = default;
            for (int i = 0; i < SafeCount(paths); i++, cursor++)
            {
                if (index != cursor || paths[i] == null)
                    continue;

                PungentSpatialPath path = paths[i];
                path.TryGetBounds(out Bounds bounds);
                item = new PungentSpatialItemSnapshot
                {
                    StableId = path.StableId,
                    DisplayName = path.DisplayName,
                    Kind = PungentSpatialItemKind.Path,
                    SourceObject = this,
                    Visible = path.visible,
                    Locked = path.locked,
                    WarningCount = path.worldPoints == null || path.worldPoints.Count < 2 ? 1 : 0,
                    EstimatedDrawOperations = Mathf.Max(1, path.worldPoints == null ? 0 : path.worldPoints.Count * 2),
                    PointCount = path.worldPoints == null ? 0 : path.worldPoints.Count,
                    Layer = path.Metadata.layer.Name,
                    Category = path.Metadata.semanticProfile.ToString(),
                    Color = path.Metadata.color,
                    Bounds = bounds
                };
                return true;
            }

            return false;
        }

        private bool TryGetAreaSnapshot(int index, ref int cursor, out PungentSpatialItemSnapshot item)
        {
            item = default;
            for (int i = 0; i < SafeCount(areas); i++, cursor++)
            {
                if (index != cursor || areas[i] == null)
                    continue;

                PungentSpatialArea area = areas[i];
                area.TryGetBounds(out Bounds bounds);
                int points = area.GetWorldPolygon()?.Length ?? 0;
                item = new PungentSpatialItemSnapshot
                {
                    StableId = area.StableId,
                    DisplayName = area.DisplayName,
                    Kind = PungentSpatialItemKind.Area,
                    SourceObject = this,
                    Visible = area.visible,
                    Locked = area.locked,
                    WarningCount = area.shape == PungentSpatialAreaShapeMode.Polygon && points < 3 ? 1 : 0,
                    EstimatedDrawOperations = Mathf.Max(1, points * 2),
                    PointCount = points,
                    Layer = area.Metadata.layer.Name,
                    Category = area.role.ToString(),
                    Color = area.borderColor,
                    Bounds = bounds
                };
                return true;
            }

            return false;
        }

        private bool TryGetRegionSnapshot(int index, ref int cursor, out PungentSpatialItemSnapshot item)
        {
            item = default;
            for (int i = 0; i < SafeCount(regionSets); i++, cursor++)
            {
                if (index != cursor || regionSets[i] == null)
                    continue;

                PungentSpatialRegionSet regionSet = regionSets[i];
                item = new PungentSpatialItemSnapshot
                {
                    StableId = regionSet.Metadata.StableId,
                    DisplayName = regionSet.Metadata.DisplayNameOrFallback($"Region Set {i + 1}"),
                    Kind = PungentSpatialItemKind.RegionSet,
                    SourceObject = this,
                    Visible = regionSet.visible,
                    Locked = regionSet.locked,
                    WarningCount = regionSet.faces == null || regionSet.faces.Count == 0 ? 1 : 0,
                    EstimatedDrawOperations = Mathf.Max(1, regionSet.vertices == null ? 0 : regionSet.vertices.Count * 2),
                    PointCount = regionSet.vertices == null ? 0 : regionSet.vertices.Count,
                    Layer = regionSet.Metadata.layer.Name,
                    Category = PungentSpatialProfile.MapRegion.ToString(),
                    Color = regionSet.Metadata.color,
                    Bounds = projection.GetWorldBounds()
                };
                return true;
            }

            return false;
        }

        private bool TryGetMarkerSnapshot(int index, ref int cursor, out PungentSpatialItemSnapshot item)
        {
            item = default;
            for (int i = 0; i < SafeCount(markers); i++, cursor++)
            {
                if (index != cursor || markers[i] == null)
                    continue;

                PungentSpatialMarker marker = markers[i];
                item = new PungentSpatialItemSnapshot
                {
                    StableId = marker.Metadata.StableId,
                    DisplayName = marker.Metadata.DisplayNameOrFallback($"Marker {i + 1}"),
                    Kind = PungentSpatialItemKind.Marker,
                    SourceObject = this,
                    Visible = marker.visible,
                    Locked = marker.locked,
                    WarningCount = 0,
                    EstimatedDrawOperations = 2,
                    PointCount = 1,
                    Layer = marker.Metadata.layer.Name,
                    Category = marker.Metadata.semanticProfile.ToString(),
                    Color = marker.color,
                    Bounds = new Bounds(marker.worldPosition, Vector3.one)
                };
                return true;
            }

            return false;
        }

        private static int SafeCount<T>(IList<T> list)
        {
            return list == null ? 0 : list.Count;
        }
    }
}
