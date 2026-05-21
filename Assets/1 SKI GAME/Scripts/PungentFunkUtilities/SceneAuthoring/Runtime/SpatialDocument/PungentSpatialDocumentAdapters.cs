using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentSpatialItemKind
    {
        Path,
        Area,
        RegionSet,
        RegionFace,
        Marker,
        Background,
        GeneratedOutput,
        Provider
    }

    public struct PungentSpatialItemSnapshot
    {
        public string StableId;
        public string DisplayName;
        public PungentSpatialItemKind Kind;
        public Object SourceObject;
        public bool Visible;
        public bool Locked;
        public int WarningCount;
        public int EstimatedDrawOperations;
        public int PointCount;
        public string Layer;
        public string Category;
        public Color Color;
        public Bounds Bounds;
    }

    public struct PungentSpatialBackgroundSnapshot
    {
        public Texture2D Texture;
        public Vector2 UvMin;
        public Vector2 UvMax;
        public PungentSpatialProjection Projection;
        public string BakeSummary;
        public bool MayBeStale;
    }

    public interface IPungentSpatialItemProvider
    {
        int SpatialItemCount { get; }
        bool TryGetSpatialItem(int index, out PungentSpatialItemSnapshot item);
    }

    public interface IPungentSpatialPathProvider
    {
        int SpatialPathCount { get; }
        bool TryGetSpatialPath(int index, out PungentSpatialPath path);
    }

    public interface IPungentSpatialAreaProvider
    {
        int SpatialAreaCount { get; }
        bool TryGetSpatialArea(int index, out PungentSpatialArea area);
    }

    public interface IPungentSpatialRegionProvider
    {
        int SpatialRegionSetCount { get; }
        bool TryGetSpatialRegionSet(int index, out PungentSpatialRegionSet regionSet);
    }

    public interface IPungentSpatialProjectionProvider
    {
        bool TryGetSpatialProjection(out PungentSpatialProjection projection);
    }

    public interface IPungentSpatialBackgroundProvider
    {
        bool TryGetSpatialBackground(out PungentSpatialBackgroundSnapshot background);
    }

    public static class PungentSpatialDocumentUtility
    {
        public static string JoinTags(IReadOnlyList<PungentSpatialTag> tags)
        {
            if (tags == null || tags.Count == 0)
                return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                string value = tags[i].Value;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(value);
            }

            return builder.ToString();
        }

        public static int CountNonEmptyTags(IReadOnlyList<PungentSpatialTag> tags)
        {
            if (tags == null)
                return 0;

            int count = 0;
            for (int i = 0; i < tags.Count; i++)
            {
                if (!tags[i].IsEmpty)
                    count++;
            }

            return count;
        }
    }
}
