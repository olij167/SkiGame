using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    /// <summary>
    /// Runtime-safe bridge point for project-specific path scripts. Optional adapters can implement this
    /// without making PungentFunk Utilities compile against game-specific path classes.
    /// </summary>
    public interface IPungentPathPointProvider
    {
        /// <summary>Number of ordered world-space control/sample points available to generic path tools.</summary>
        int PointCount { get; }

        /// <summary>Returns a world-space point for the requested index.</summary>
        Vector3 GetWorldPoint(int index);
    }

    public interface IPungentPathMetadataProvider
    {
        bool TryGetPathMetadata(out PungentPathMetadata metadata);
    }

    public interface IPungentPathQueryProvider
    {
        bool TrySamplePath(float normalizedDistance, out PungentPathSample sample);
        bool TryProjectPoint(Vector3 worldPoint, out PungentPathProjectionResult result);
    }

    /// <summary>
    /// Optional provider for explicit corridor width. Return false when no real width source exists.
    /// </summary>
    public interface IPungentCorridorWidthProvider
    {
        bool TryGetWidthAt(float normalizedDistance, out float width);
    }

    /// <summary>
    /// Optional provider for checkpoint or gate volumes used by generic area previews.
    /// </summary>
    public interface IPungentCheckpointVolumeProvider
    {
        int CheckpointCount { get; }
        bool TryGetCheckpointVolume(int index, out PungentCheckpointVolume volume);
    }

    /// <summary>
    /// Optional provider for normalized path spans that should be excluded, blocked, or visually marked.
    /// </summary>
    public interface IPungentExclusionSpanProvider
    {
        int ExclusionSpanCount { get; }
        bool TryGetExclusionSpan(int index, out PungentPathSpan span);
    }

    public interface IPungentGeneratedOutputProvider
    {
        int GeneratedOutputCount { get; }
        bool TryGetGeneratedOutput(int index, out PungentGeneratedOutputDescriptor output);
    }

    public interface IPungentSpatialExportProvider
    {
        string ExportProviderId { get; }
        string ExportDisplayName { get; }
    }

    public struct PungentPathMetadata
    {
        public string PathId;
        public string DisplayName;
        public IReadOnlyList<string> Tags;
        public Color Color;
        public PungentSpatialProjectionMode Projection;
        public bool ClosedLoop;
        public int ControlPointCount;
        public int SampledPointCount;
        public float TotalLength;
        public bool HasCorridorWidth;
        public float DefaultCorridorWidth;
    }

    public struct PungentPathSample
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public float Distance;
        public float NormalizedDistance;
        public int SegmentIndex;
    }

    public struct PungentPathProjectionResult
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public float Distance;
        public float NormalizedDistance;
        public float SqrDistanceToInput;
        public int SegmentIndex;
        public bool ClosedLoop;
    }

    public struct PungentGeneratedOutputDescriptor
    {
        public string StableId;
        public string Kind;
        public string DisplayName;
        public Transform Root;
        public int ExistingObjectCount;
        public int EstimatedObjectCount;
        public bool IsPreviewOnly;
        public bool MayBeStale;
    }

    /// <summary>
    /// Normalized span along a path, suitable for project adapters without compile-time package dependencies.
    /// </summary>
    public struct PungentPathSpan
    {
        public float StartNormalized;
        public float EndNormalized;
        public string Label;

        public PungentPathSpan(float startNormalized, float endNormalized, string label = null)
        {
            StartNormalized = Mathf.Clamp01(startNormalized);
            EndNormalized = Mathf.Clamp01(endNormalized);
            Label = label ?? string.Empty;
        }
    }

    /// <summary>
    /// Generic checkpoint/gate volume description for scene authoring previews.
    /// </summary>
    public struct PungentCheckpointVolume
    {
        public Vector3 Center;
        public Vector3 Size;
        public Quaternion Rotation;
        public string Label;

        public PungentCheckpointVolume(Vector3 center, Vector3 size, Quaternion rotation, string label = null)
        {
            Center = center;
            Size = size;
            Rotation = rotation;
            Label = label ?? string.Empty;
        }
    }

    public static class PungentPathAuthoringAdapterUtility
    {
        /// <summary>Copies provider points into a stable list for editor previews and validation.</summary>
        public static List<Vector3> CopyWorldPoints(IPungentPathPointProvider provider)
        {
            List<Vector3> points = new List<Vector3>();
            if (provider == null)
                return points;

            int count = Mathf.Max(0, provider.PointCount);
            for (int i = 0; i < count; i++)
                points.Add(provider.GetWorldPoint(i));

            return points;
        }

        /// <summary>Reflection-free helper for optional component-level provider adapters.</summary>
        public static bool TryGetProvider<TProvider>(Component component, out TProvider provider) where TProvider : class
        {
            provider = null;
            if (component == null)
                return false;

            provider = component as TProvider;
            return provider != null;
        }
    }
}
