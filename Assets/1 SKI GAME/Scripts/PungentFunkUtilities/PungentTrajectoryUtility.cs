using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public struct PungentTrajectoryCalculationResult
    {
        public bool hitFound;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public int sampleCount;
        public int estimatedDrawOperations;
        public string warning;
    }

    public static class PungentTrajectoryUtility
    {
        public const int DefaultMaxSamples = 128;
        public const int HardMaxSamples = 512;
        public const float MinTimeStep = 0.005f;
        public const float MaxDuration = 30f;

        public static PungentTrajectoryCalculationResult CalculateTrajectory(
            IList<PungentTrajectorySample> samples,
            Vector3 origin,
            Vector3 initialVelocity,
            Vector3 gravity,
            float duration,
            float timeStep,
            int maxSamples,
            TrajectoryCollisionMode collisionMode,
            LayerMask collisionMask,
            float sphereCastRadius,
            bool stopAtFirstHit)
        {
            if (samples != null)
                samples.Clear();

            PungentTrajectoryCalculationResult result = new PungentTrajectoryCalculationResult();
            if (samples == null)
            {
                result.warning = "No sample list was provided.";
                return result;
            }

            float safeDuration = Mathf.Clamp(duration, MinTimeStep, MaxDuration);
            float safeTimeStep = Mathf.Clamp(timeStep, MinTimeStep, safeDuration);
            int safeSamples = Mathf.Clamp(maxSamples, 2, HardMaxSamples);
            int targetSamples = Mathf.Clamp(Mathf.CeilToInt(safeDuration / safeTimeStep) + 1, 2, safeSamples);
            if (timeStep <= 0f)
                result.warning = "Time step was clamped to a safe value.";
            else if (maxSamples > HardMaxSamples)
                result.warning = "Max samples were clamped to the hard safety limit.";

            Vector3 previous = origin;
            Vector3 velocity = initialVelocity;
            samples.Add(new PungentTrajectorySample(previous, velocity, 0f));

            for (int i = 1; i < targetSamples; i++)
            {
                float t = Mathf.Min(safeDuration, i * safeTimeStep);
                Vector3 position = origin + initialVelocity * t + 0.5f * gravity * t * t;
                velocity = initialVelocity + gravity * t;
                bool hit = false;

                if (collisionMode != TrajectoryCollisionMode.None)
                {
                    Vector3 delta = position - previous;
                    float distance = delta.magnitude;
                    if (distance > 0.0001f)
                    {
                        RaycastHit hitInfo;
                        if (collisionMode == TrajectoryCollisionMode.SphereCast)
                            hit = Physics.SphereCast(previous, Mathf.Max(0.001f, sphereCastRadius), delta.normalized, out hitInfo, distance, collisionMask, QueryTriggerInteraction.Ignore);
                        else
                            hit = Physics.Linecast(previous, position, out hitInfo, collisionMask, QueryTriggerInteraction.Ignore);

                        if (hit)
                        {
                            position = hitInfo.point;
                            result.hitFound = true;
                            result.hitPoint = hitInfo.point;
                            result.hitNormal = hitInfo.normal;
                        }
                    }
                }

                samples.Add(new PungentTrajectorySample(position, velocity, t, hit));
                previous = position;

                if (hit && stopAtFirstHit)
                    break;
            }

            result.sampleCount = samples.Count;
            result.estimatedDrawOperations = Mathf.Max(0, samples.Count - 1);
            return result;
        }
    }
}
