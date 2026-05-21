using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum TrajectorySourceMode
    {
        TransformForward,
        RigidbodyVelocity,
        ExplicitVelocity,
        ReflectedVector3Field,
        Provider
    }

    public enum TrajectoryGravityMode
    {
        PhysicsGravity,
        CustomGravity,
        None
    }

    public enum TrajectoryCollisionMode
    {
        None,
        Linecast,
        SphereCast
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Utilities/Scene Tools/Pungent Trajectory Visualizer")]
    public sealed class PungentTrajectoryVisualizer : MonoBehaviour, IPungentTrajectoryProvider, IPungentSceneGizmoPerformanceProvider
    {
        public bool drawInScene = true;
        public bool drawWhenSelectedOnly = true;
        public TrajectorySourceMode sourceMode = TrajectorySourceMode.TransformForward;
        public TrajectoryGravityMode gravityMode = TrajectoryGravityMode.PhysicsGravity;
        public TrajectoryCollisionMode collisionMode = TrajectoryCollisionMode.None;
        public Transform origin;
        public Rigidbody sourceRigidbody;
        public Component reflectedComponent;
        public string velocityFieldPath = "velocity";
        public Vector3 explicitVelocity = Vector3.forward * 10f;
        [Min(0f)] public float forwardSpeed = 10f;
        public Vector3 customGravity = new Vector3(0f, -9.81f, 0f);
        public LayerMask collisionMask = ~0;
        [Min(0.001f)] public float sphereCastRadius = 0.1f;
        [Min(0.01f)] public float duration = 2f;
        [Min(0.005f)] public float timeStep = 0.05f;
        [Min(2)] public int maxSamples = PungentTrajectoryUtility.DefaultMaxSamples;
        [Min(0f)] public float maxDrawDistance = 0f;
        public bool stopAtFirstHit = true;
        public bool drawSamplePoints = false;
        public bool drawHitMarker = true;
        public bool drawLabels = true;
        public Color trajectoryColor = new Color(0.2f, 0.9f, 1f, 0.95f);
        public Color hitColor = new Color(1f, 0.25f, 0.2f, 0.95f);
        public Color sampleColor = new Color(1f, 0.9f, 0.25f, 0.9f);

        private readonly List<PungentTrajectorySample> _cachedSamples = new List<PungentTrajectorySample>(PungentTrajectoryUtility.DefaultMaxSamples);
        private PungentTrajectorySnapshot _cachedSnapshot;
        private bool _dirty = true;
        private float _nextRebuildTime;
        private const float RebuildInterval = 0.10f;

        private void OnValidate()
        {
            forwardSpeed = Mathf.Max(0f, forwardSpeed);
            sphereCastRadius = Mathf.Max(0.001f, sphereCastRadius);
            duration = Mathf.Clamp(duration, PungentTrajectoryUtility.MinTimeStep, PungentTrajectoryUtility.MaxDuration);
            timeStep = Mathf.Clamp(timeStep, PungentTrajectoryUtility.MinTimeStep, duration);
            maxSamples = Mathf.Clamp(maxSamples, 2, PungentTrajectoryUtility.HardMaxSamples);
            maxDrawDistance = Mathf.Max(0f, maxDrawDistance);
            MarkDirty();
        }

        private void OnEnable()
        {
            MarkDirty();
        }

        public void RefreshPreview()
        {
            MarkDirty();
            RebuildCachedSamples();
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public bool TryGetTrajectorySnapshot(IList<PungentTrajectorySample> samples, out PungentTrajectorySnapshot snapshot)
        {
            if (sourceMode == TrajectorySourceMode.Provider && TryGetExternalProvider(out IPungentTrajectoryProvider provider))
                return provider.TryGetTrajectorySnapshot(samples, out snapshot);

            if (_dirty || Time.realtimeSinceStartup >= _nextRebuildTime)
                RebuildCachedSamples();

            if (samples != null)
            {
                samples.Clear();
                for (int i = 0; i < _cachedSamples.Count; i++)
                    samples.Add(_cachedSamples[i]);
            }

            snapshot = _cachedSnapshot;
            return drawInScene && isActiveAndEnabled && _cachedSnapshot.sampleCount > 0;
        }

        public bool TryGetPerformanceEstimate(out PungentSceneGizmoPerformanceEstimate estimate)
        {
            int safeSamples = EstimateSampleCount();
            estimate = new PungentSceneGizmoPerformanceEstimate
            {
                owner = this,
                active = isActiveAndEnabled,
                drawInScene = drawInScene,
                selectedOnly = drawWhenSelectedOnly,
                hasLabels = drawLabels,
                alwaysVisible = !drawWhenSelectedOnly,
                estimatedDrawOperations = drawInScene && isActiveAndEnabled ? Mathf.Max(0, safeSamples - 1) + (drawSamplePoints ? safeSamples : 0) + (drawHitMarker ? 1 : 0) + (drawLabels ? 1 : 0) : 0,
                estimatedLabels = drawInScene && isActiveAndEnabled && drawLabels ? 1 : 0,
                estimatedTrajectorySamples = drawInScene && isActiveAndEnabled ? safeSamples : 0,
                priority = 5,
                providerCategory = "Trajectory",
                warning = GetConfigurationWarning()
            };
            return true;
        }

        private void RebuildCachedSamples()
        {
            _dirty = false;
            _nextRebuildTime = Time.realtimeSinceStartup + RebuildInterval;

            Vector3 start = ResolveOrigin();
            Vector3 velocity = ResolveVelocity();
            Vector3 gravity = ResolveGravity();
            PungentTrajectoryCalculationResult result = PungentTrajectoryUtility.CalculateTrajectory(
                _cachedSamples,
                start,
                velocity,
                gravity,
                duration,
                timeStep,
                maxSamples,
                collisionMode,
                collisionMask,
                sphereCastRadius,
                stopAtFirstHit);

            string warning = GetConfigurationWarning();
            if (!string.IsNullOrWhiteSpace(result.warning))
                warning = string.IsNullOrWhiteSpace(warning) ? result.warning : warning + " " + result.warning;

            _cachedSnapshot = new PungentTrajectorySnapshot
            {
                owner = this,
                drawInScene = drawInScene,
                drawWhenSelectedOnly = drawWhenSelectedOnly,
                drawSamplePoints = drawSamplePoints,
                drawHitMarker = drawHitMarker,
                drawLabels = drawLabels,
                origin = start,
                initialVelocity = velocity,
                gravity = gravity,
                hitFound = result.hitFound,
                hitPoint = result.hitPoint,
                hitNormal = result.hitNormal,
                sampleCount = result.sampleCount,
                estimatedDrawOperations = result.estimatedDrawOperations,
                maxDrawDistance = Mathf.Max(0f, maxDrawDistance),
                trajectoryColor = trajectoryColor,
                hitColor = hitColor,
                sampleColor = sampleColor,
                warning = warning
            };
        }

        private Vector3 ResolveOrigin()
        {
            return origin != null ? origin.position : transform.position;
        }

        private Vector3 ResolveVelocity()
        {
            switch (sourceMode)
            {
                case TrajectorySourceMode.RigidbodyVelocity:
                    Rigidbody rb = sourceRigidbody != null ? sourceRigidbody : GetComponent<Rigidbody>();
                    return rb != null ? rb.linearVelocity : Vector3.zero;
                case TrajectorySourceMode.ExplicitVelocity:
                    return explicitVelocity;
                case TrajectorySourceMode.ReflectedVector3Field:
                    if (PungentSceneGizmoSource.TryResolveFieldValue(reflectedComponent, velocityFieldPath, PungentSceneGizmoSource.BindingValueKind.Vector3, out Vector3 reflected))
                        return reflected;
                    return Vector3.zero;
                default:
                    Transform source = origin != null ? origin : transform;
                    return source.forward * Mathf.Max(0f, forwardSpeed);
            }
        }

        private Vector3 ResolveGravity()
        {
            switch (gravityMode)
            {
                case TrajectoryGravityMode.CustomGravity:
                    return customGravity;
                case TrajectoryGravityMode.None:
                    return Vector3.zero;
                default:
                    return Physics.gravity;
            }
        }

        private int EstimateSampleCount()
        {
            float safeDuration = Mathf.Clamp(duration, PungentTrajectoryUtility.MinTimeStep, PungentTrajectoryUtility.MaxDuration);
            float safeTimeStep = Mathf.Clamp(timeStep, PungentTrajectoryUtility.MinTimeStep, safeDuration);
            int safeMaxSamples = Mathf.Clamp(maxSamples, 2, PungentTrajectoryUtility.HardMaxSamples);
            return Mathf.Clamp(Mathf.CeilToInt(safeDuration / safeTimeStep) + 1, 2, safeMaxSamples);
        }

        private string GetConfigurationWarning()
        {
            if (timeStep <= 0f)
                return "Trajectory time step must be greater than zero.";
            if (maxSamples > PungentTrajectoryUtility.HardMaxSamples)
                return "Trajectory max samples exceed the hard safety limit.";
            if (duration > PungentTrajectoryUtility.MaxDuration)
                return "Trajectory duration exceeds the safety limit.";
            if (sourceMode == TrajectorySourceMode.ReflectedVector3Field && reflectedComponent == null)
                return "Reflected trajectory mode needs a component target.";
            return string.Empty;
        }

        private bool TryGetExternalProvider(out IPungentTrajectoryProvider provider)
        {
            provider = null;
            Component[] components = GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component == this)
                    continue;

                provider = component as IPungentTrajectoryProvider;
                if (provider != null)
                    return true;
            }

            return false;
        }
    }
}
