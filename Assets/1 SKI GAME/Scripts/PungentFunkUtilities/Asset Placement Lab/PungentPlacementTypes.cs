using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    public enum PungentPlacementScatterPattern
    {
        Random,
        RandomMinSpacing,
        Grid,
        JitteredGrid,
        HexGrid,
        Spiral,
        Ring,
        Line
    }

    public enum PungentPlacementAreaMode
    {
        ManualBounds,
        SelectionBounds,
        ModularPath
    }

    public enum PungentPlacementSurfaceAlignment
    {
        None,
        AlignUpToSurfaceNormal,
        PreserveYawAlignUpToSurfaceNormal
    }

    public enum PungentPlacementCandidateState
    {
        Pending,
        Accepted,
        Rejected
    }

    public enum PungentPlacementRejectionReason
    {
        None,
        MissingAssetSet,
        MissingPrefab,
        NoSurfaceHit,
        HeightRejected,
        SlopeRejected,
        HeatmapRejected,
        MinSpacingRejected,
        OverlapRejected,
        FootprintRejected,
        RuleRejected
    }

    [Flags]
    public enum PungentPlacementSurfaceCategory
    {
        None = 0,
        Indoor = 1 << 0,
        Outdoor = 1 << 1,
        Terrain = 1 << 2,
        Floor = 1 << 3,
        Wall = 1 << 4,
        Ceiling = 1 << 5,
        Water = 1 << 6,
        CustomA = 1 << 7,
        CustomB = 1 << 8
    }

    [Serializable]
    public sealed class PungentPlacementFootprint
    {
        public bool useFootprint;
        public Vector2Int size = Vector2Int.one;
        public Vector2Int pivot = Vector2Int.zero;
        public float cellSize = 1f;
        public bool rotateWithObject = true;
        public Vector3 boundsPadding = Vector3.zero;

        public Vector2Int SafeSize => new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        public float SafeCellSize => Mathf.Max(0.01f, cellSize);

        public Vector2Int GetRotatedSize(float yawDegrees)
        {
            Vector2Int safe = SafeSize;
            if (!useFootprint || !rotateWithObject)
                return safe;

            int quarterTurns = Mathf.RoundToInt(Mathf.Repeat(yawDegrees, 360f) / 90f) % 4;
            return quarterTurns % 2 == 0 ? safe : new Vector2Int(safe.y, safe.x);
        }

        public Bounds GetWorldBounds(Vector3 worldPosition, Quaternion rotation, Vector3 fallbackExtents)
        {
            if (!useFootprint)
                return new Bounds(worldPosition, fallbackExtents * 2f + boundsPadding);

            Vector2Int rotated = GetRotatedSize(rotation.eulerAngles.y);
            Vector3 size3 = new Vector3(rotated.x * SafeCellSize, Mathf.Max(fallbackExtents.y * 2f, SafeCellSize), rotated.y * SafeCellSize);
            size3 += boundsPadding;
            return new Bounds(worldPosition + Vector3.up * (size3.y * 0.5f), size3);
        }
    }

    [Serializable]
    public sealed class PungentPlacementTagList
    {
        public List<string> tags = new List<string>();

        public bool Contains(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
                return false;

            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    [Serializable]
    public sealed class PungentPlacementCandidate
    {
        public GameObject prefab;
        public PungentPlacementAssetEntry assetEntry;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public Vector3 surfaceNormal = Vector3.up;
        public Bounds estimatedBounds;
        public PungentPlacementCandidateState state = PungentPlacementCandidateState.Pending;
        public PungentPlacementRejectionReason rejectionReason = PungentPlacementRejectionReason.None;
        public string rejectionMessage;
        public float score = 1f;
        public int index;

        public bool Accepted => state == PungentPlacementCandidateState.Accepted;
    }

    [Serializable]
    public sealed class PungentPlacementResult
    {
        public List<PungentPlacementCandidate> candidates = new List<PungentPlacementCandidate>();
        public int acceptedCount;
        public int rejectedCount;
        public string summary = "Ready";

        public void Recount()
        {
            acceptedCount = 0;
            rejectedCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == null)
                    continue;

                if (candidates[i].state == PungentPlacementCandidateState.Accepted)
                    acceptedCount++;
                else if (candidates[i].state == PungentPlacementCandidateState.Rejected)
                    rejectedCount++;
            }

            summary = $"{acceptedCount} accepted • {rejectedCount} rejected";
        }

        public void Clear()
        {
            candidates.Clear();
            acceptedCount = 0;
            rejectedCount = 0;
            summary = "Cleared";
        }
    }

    public sealed class PungentPlacementContext
    {
        public int seed;
        public int requestedCount;
        public PungentPlacementScatterPattern scatterPattern;
        public PungentPlacementAssetSetSO assetSet;
        public PungentPlacementRuleSetSO ruleSet;
        public Bounds areaBounds;
        public Transform outputParent;
        public Texture2D heatmap;
        public Rect heatmapWorldRect;
        public bool useHeatmap;
        public bool previewRejected;
    }

}
