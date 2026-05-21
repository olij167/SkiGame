using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    [CreateAssetMenu(menuName = "PungentFunk Utilities/Asset Placement/Rule Set", fileName = "Placement Rule Set")]
    public sealed class PungentPlacementRuleSetSO : ScriptableObject
    {
        [Header("Surface Sampling")]
        public LayerMask surfaceMask = ~0;
        public float raycastStartHeight = 100f;
        public float raycastDistance = 250f;
        public bool requireSurfaceHit = true;
        public float surfaceOffset = 0f;

        [Header("Height / Slope")]
        public bool restrictHeight;
        public Vector2 heightRange = new Vector2(-1000f, 1000f);
        public bool restrictSlope;
        public Vector2 slopeRange = new Vector2(0f, 45f);

        [Header("Spacing / Overlap")]
        [Min(0f)] public float minimumSpacing = 1f;
        public bool enforceMinimumSpacing = true;
        public bool rejectColliderOverlap = false;
        public LayerMask overlapMask = ~0;
        public Vector3 overlapPadding = Vector3.zero;
        public bool ignoreTriggerColliders = true;

        [Header("Heatmap / Mask")]
        public Texture2D heatmap;
        public bool useHeatmap;
        public bool invertHeatmap;
        [Range(0f, 1f)] public float heatmapThreshold = 0.15f;
        [Range(0f, 1f)] public float heatmapProbabilityInfluence = 1f;
        public Rect heatmapWorldRect = new Rect(-25f, -25f, 50f, 50f);

        [Header("Orientation")]
        public bool useEntrySurfaceAlignment = true;
        public PungentPlacementSurfaceAlignment fallbackSurfaceAlignment = PungentPlacementSurfaceAlignment.PreserveYawAlignUpToSurfaceNormal;

        [Header("Output")]
        public bool addPlacementMarker = true;
        public bool createGroupObject = true;
        public string generatedObjectPrefix = "Placed";

        public bool HeightAllowed(float y)
        {
            if (!restrictHeight)
                return true;

            float min = Mathf.Min(heightRange.x, heightRange.y);
            float max = Mathf.Max(heightRange.x, heightRange.y);
            return y >= min && y <= max;
        }

        public bool SlopeAllowed(Vector3 normal)
        {
            if (!restrictSlope)
                return true;

            Vector3 n = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            float slope = Vector3.Angle(n, Vector3.up);
            float min = Mathf.Min(slopeRange.x, slopeRange.y);
            float max = Mathf.Max(slopeRange.x, slopeRange.y);
            return slope >= min && slope <= max;
        }

        public float SampleHeatmap01(Vector3 worldPosition)
        {
            if (heatmap == null)
                return 1f;

            Rect rect = heatmapWorldRect.width == 0f || heatmapWorldRect.height == 0f
                ? new Rect(-25f, -25f, 50f, 50f)
                : heatmapWorldRect;

            float u = Mathf.InverseLerp(rect.xMin, rect.xMax, worldPosition.x);
            float v = Mathf.InverseLerp(rect.yMin, rect.yMax, worldPosition.z);
            Color pixel = heatmap.GetPixelBilinear(Mathf.Clamp01(u), Mathf.Clamp01(v));
            float value = pixel.grayscale;
            return invertHeatmap ? 1f - value : value;
        }
    }

}