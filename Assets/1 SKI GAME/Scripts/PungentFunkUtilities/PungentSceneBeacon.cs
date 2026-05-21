using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Utilities/Scene Tools/Pungent Scene Beacon")]
    public sealed class PungentSceneBeacon : MonoBehaviour, IPungentSceneBeaconProvider, IPungentSceneGizmoPerformanceProvider
    {
        public bool drawInScene = true;
        public bool drawWhenSelectedOnly = false;
        public bool alwaysVisible = false;
        public bool pingable = true;
        public bool drawLabel = true;
        public bool drawRing = true;
        public bool drawVerticalLine = false;
        public bool drawDistanceToSceneCamera = false;
        public string label = "Beacon";
        public Color color = new Color(0.25f, 0.85f, 1f, 0.95f);
        [Min(0.01f)] public float radius = 1f;
        [Min(0f)] public float verticalLineHeight = 3f;
        [Min(0f)] public float maxDrawDistance = 0f;
        public int priority = 0;
        public Transform labelAnchor;
        public Vector3 localOffset = Vector3.zero;

        [SerializeField, HideInInspector] private double lastPingTime = -1000d;
        [SerializeField, HideInInspector] private float pingDuration = 1.4f;

        public double LastPingTime => lastPingTime;
        public float PingDuration => Mathf.Max(0.05f, pingDuration);

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            verticalLineHeight = Mathf.Max(0f, verticalLineHeight);
            maxDrawDistance = Mathf.Max(0f, maxDrawDistance);
            pingDuration = Mathf.Max(0.05f, pingDuration);
        }

        public void Ping()
        {
            if (!pingable)
                return;

            lastPingTime = Time.realtimeSinceStartup;
        }

        public bool TryGetBeaconSnapshot(out PungentSceneBeaconSnapshot snapshot)
        {
            snapshot = new PungentSceneBeaconSnapshot
            {
                owner = this,
                drawInScene = drawInScene,
                drawWhenSelectedOnly = drawWhenSelectedOnly,
                alwaysVisible = alwaysVisible,
                pingable = pingable,
                drawLabel = drawLabel,
                drawRing = drawRing,
                drawVerticalLine = drawVerticalLine,
                drawDistanceToSceneCamera = drawDistanceToSceneCamera,
                worldPosition = ResolveWorldPosition(),
                label = string.IsNullOrWhiteSpace(label) ? name : label,
                color = color,
                radius = Mathf.Max(0.01f, radius),
                verticalLineHeight = Mathf.Max(0f, verticalLineHeight),
                maxDrawDistance = Mathf.Max(0f, maxDrawDistance),
                priority = priority,
                lastPingTime = lastPingTime,
                pingDuration = PingDuration
            };

            return drawInScene && isActiveAndEnabled;
        }

        public bool TryGetPerformanceEstimate(out PungentSceneGizmoPerformanceEstimate estimate)
        {
            estimate = new PungentSceneGizmoPerformanceEstimate
            {
                owner = this,
                active = isActiveAndEnabled,
                drawInScene = drawInScene,
                selectedOnly = drawWhenSelectedOnly,
                hasLabels = drawLabel || drawDistanceToSceneCamera,
                alwaysVisible = alwaysVisible,
                estimatedDrawOperations = EstimateDrawOperations(),
                estimatedLabels = drawInScene && isActiveAndEnabled ? ((drawLabel ? 1 : 0) + (drawDistanceToSceneCamera ? 1 : 0)) : 0,
                estimatedTrajectorySamples = 0,
                priority = priority,
                providerCategory = "Beacon"
            };
            return true;
        }

        private int EstimateDrawOperations()
        {
            if (!drawInScene || !isActiveAndEnabled)
                return 0;

            int ops = 1;
            if (drawRing)
                ops++;
            if (drawVerticalLine)
                ops++;
            if (drawLabel)
                ops++;
            if (drawDistanceToSceneCamera)
                ops++;
            return ops;
        }

        private Vector3 ResolveWorldPosition()
        {
            if (labelAnchor != null)
                return labelAnchor.position;

            return transform.position + transform.TransformVector(localOffset);
        }

        private void OnDrawGizmos()
        {
            DrawFallbackGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawFallbackGizmo(true);
        }

        private void DrawFallbackGizmo(bool selected)
        {
            if (!drawInScene || !drawRing)
                return;
            if (drawWhenSelectedOnly && !selected)
                return;

            Color previous = Gizmos.color;
            Gizmos.color = color;
            Vector3 position = ResolveWorldPosition();
            Gizmos.DrawWireSphere(position, Mathf.Max(0.01f, radius));
            if (drawVerticalLine && verticalLineHeight > 0f)
                Gizmos.DrawLine(position, position + Vector3.up * verticalLineHeight);
            Gizmos.color = previous;
        }
    }
}
