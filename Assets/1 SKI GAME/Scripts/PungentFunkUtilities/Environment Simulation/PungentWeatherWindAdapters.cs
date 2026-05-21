using System;
using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [Serializable]
    public struct PungentWeatherWindSample
    {
        public Vector3 direction;
        public float speed;
        public float gustStrength;
        public float storminess;
        public float turbulence;

        public static PungentWeatherWindSample FromWeather(PungentWeatherState state, float speedMultiplier, float gustMultiplier)
        {
            return new PungentWeatherWindSample
            {
                direction = state.WindDirection,
                speed = Mathf.Max(0f, state.windSpeed * speedMultiplier),
                gustStrength = Mathf.Max(0f, state.gustStrength * gustMultiplier),
                storminess = Mathf.Clamp01(state.storminess),
                turbulence = Mathf.Clamp01(Mathf.Max(state.cloudTurbulence, state.storminess))
            };
        }
    }

    public interface IPungentWeatherWindReceiver
    {
        void ApplyPungentWeatherWind(PungentWeatherWindSample sample);
    }

    public abstract class PungentWeatherWindAdapterBase : MonoBehaviour
    {
        [SerializeField] protected PungentWeatherController weather;
        [SerializeField] protected bool applyContinuously = true;
        [SerializeField, Min(0f)] protected float speedMultiplier = 1f;
        [SerializeField, Min(0f)] protected float gustMultiplier = 1f;

        protected virtual void OnEnable()
        {
            if (weather == null)
                weather = FindAnyObjectByType<PungentWeatherController>();

            if (weather != null)
                weather.WeatherChanged += Apply;
        }

        protected virtual void OnDisable()
        {
            if (weather != null)
                weather.WeatherChanged -= Apply;
        }

        protected virtual void Update()
        {
            if (applyContinuously && weather != null)
                Apply(weather.CurrentState);
        }

        public void Apply(PungentWeatherState state)
        {
            ApplySample(PungentWeatherWindSample.FromWeather(state, speedMultiplier, gustMultiplier));
        }

        protected abstract void ApplySample(PungentWeatherWindSample sample);
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Weather WindZone Adapter")]
    public sealed class PungentWeatherWindZoneAdapter : PungentWeatherWindAdapterBase
    {
        [SerializeField] private WindZone windZone;
        [SerializeField] private bool rotateToWindDirection = true;
        [SerializeField, Min(0f)] private float mainWindMultiplier = 1f;
        [SerializeField, Min(0f)] private float turbulenceMultiplier = 1f;
        [SerializeField, Min(0f)] private float pulseMagnitudeMultiplier = 1f;
        [SerializeField, Min(0f)] private float pulseFrequencyMultiplier = 1f;

        protected override void OnEnable()
        {
            if (windZone == null)
                windZone = GetComponent<WindZone>();
            base.OnEnable();
        }

        protected override void ApplySample(PungentWeatherWindSample sample)
        {
            if (windZone == null)
                return;

            windZone.windMain = sample.speed * mainWindMultiplier;
            windZone.windTurbulence = Mathf.Max(sample.turbulence, sample.storminess) * turbulenceMultiplier;
            windZone.windPulseMagnitude = sample.gustStrength * pulseMagnitudeMultiplier;
            windZone.windPulseFrequency = Mathf.Lerp(0.05f, 1.25f, Mathf.Clamp01(sample.storminess + sample.turbulence)) * pulseFrequencyMultiplier;

            if (rotateToWindDirection && sample.direction.sqrMagnitude > 0.0001f)
                windZone.transform.rotation = Quaternion.LookRotation(sample.direction.normalized, Vector3.up);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Weather Particle Wind Adapter")]
    public sealed class PungentWeatherParticleWindAdapter : PungentWeatherWindAdapterBase
    {
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private bool setForceOverLifetime = true;
        [SerializeField] private bool setSimulationSpeed;
        [SerializeField, Min(0f)] private float forceMultiplier = 1f;
        [SerializeField, Min(0f)] private float simulationSpeedFromWind = 0.15f;

        protected override void ApplySample(PungentWeatherWindSample sample)
        {
            if (particleSystems == null)
                return;

            Vector3 force = sample.direction * sample.speed * forceMultiplier;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                if (setForceOverLifetime)
                {
                    ParticleSystem.ForceOverLifetimeModule forceModule = particleSystem.forceOverLifetime;
                    forceModule.enabled = true;
                    forceModule.space = ParticleSystemSimulationSpace.World;
                    forceModule.x = new ParticleSystem.MinMaxCurve(force.x);
                    forceModule.y = new ParticleSystem.MinMaxCurve(force.y);
                    forceModule.z = new ParticleSystem.MinMaxCurve(force.z);
                }

                if (setSimulationSpeed)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.simulationSpeed = Mathf.Max(0f, 1f + sample.speed * simulationSpeedFromWind);
                }
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Weather Cloth Wind Adapter")]
    public sealed class PungentWeatherClothWindAdapter : PungentWeatherWindAdapterBase
    {
        [SerializeField] private Cloth[] clothTargets;
        [SerializeField, Min(0f)] private float externalAccelerationMultiplier = 1f;
        [SerializeField, Min(0f)] private float randomAccelerationMultiplier = 1f;

        protected override void ApplySample(PungentWeatherWindSample sample)
        {
            if (clothTargets == null)
                return;

            Vector3 external = sample.direction * sample.speed * externalAccelerationMultiplier;
            Vector3 random = Vector3.one * sample.gustStrength * randomAccelerationMultiplier;
            for (int i = 0; i < clothTargets.Length; i++)
            {
                Cloth cloth = clothTargets[i];
                if (cloth == null)
                    continue;

                cloth.externalAcceleration = external;
                cloth.randomAcceleration = random;
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Weather Wind Receiver Adapter")]
    public sealed class PungentWeatherWindReceiverAdapter : PungentWeatherWindAdapterBase
    {
        [SerializeField] private Component[] receivers;

        protected override void ApplySample(PungentWeatherWindSample sample)
        {
            if (receivers == null)
                return;

            for (int i = 0; i < receivers.Length; i++)
            {
                if (receivers[i] is IPungentWeatherWindReceiver receiver)
                    receiver.ApplyPungentWeatherWind(sample);
            }
        }
    }
}
