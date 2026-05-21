using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    public abstract class PungentDeltaTimeAdapterBase : MonoBehaviour
    {
        [SerializeField] protected PungentDeltaTimeController deltaTime;
        [SerializeField] protected PungentTimeChannel channel = PungentTimeChannel.Gameplay;
        [SerializeField] protected string customChannel;
        [SerializeField] protected bool applyContinuously = true;

        protected virtual void OnEnable()
        {
            if (deltaTime == null)
                deltaTime = PungentDeltaTimeController.Instance != null ? PungentDeltaTimeController.Instance : FindAnyObjectByType<PungentDeltaTimeController>();
        }

        protected virtual void LateUpdate()
        {
            if (applyContinuously)
                Apply();
        }

        public void Apply()
        {
            if (deltaTime == null)
                return;

            ApplyScale(deltaTime.GetScale(channel, customChannel));
        }

        protected abstract void ApplyScale(float scale);
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Delta Time Animator Adapter")]
    public sealed class PungentDeltaTimeAnimatorAdapter : PungentDeltaTimeAdapterBase
    {
        [SerializeField] private Animator[] animators;
        [SerializeField] private bool captureBaseSpeeds = true;

        private float[] _baseSpeeds;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (animators == null || !captureBaseSpeeds)
                return;

            _baseSpeeds = new float[animators.Length];
            for (int i = 0; i < animators.Length; i++)
                _baseSpeeds[i] = animators[i] != null ? animators[i].speed : 1f;
        }

        protected override void ApplyScale(float scale)
        {
            if (animators == null)
                return;

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                float baseSpeed = _baseSpeeds != null && i < _baseSpeeds.Length ? _baseSpeeds[i] : 1f;
                animators[i].speed = Mathf.Max(0f, baseSpeed * scale);
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Delta Time Audio Adapter")]
    public sealed class PungentDeltaTimeAudioAdapter : PungentDeltaTimeAdapterBase
    {
        [SerializeField] private AudioSource[] audioSources;
        [SerializeField] private bool scalePitch = true;
        [SerializeField] private bool muteWhenPaused;
        [SerializeField] private bool captureBasePitch = true;

        private float[] _basePitches;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (audioSources == null || !captureBasePitch)
                return;

            _basePitches = new float[audioSources.Length];
            for (int i = 0; i < audioSources.Length; i++)
                _basePitches[i] = audioSources[i] != null ? audioSources[i].pitch : 1f;
        }

        protected override void ApplyScale(float scale)
        {
            if (audioSources == null)
                return;

            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource source = audioSources[i];
                if (source == null)
                    continue;

                if (scalePitch)
                {
                    float basePitch = _basePitches != null && i < _basePitches.Length ? _basePitches[i] : 1f;
                    source.pitch = Mathf.Max(0f, basePitch * scale);
                }

                if (muteWhenPaused)
                    source.mute = scale <= 0.0001f;
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Delta Time Particle Adapter")]
    public sealed class PungentDeltaTimeParticleAdapter : PungentDeltaTimeAdapterBase
    {
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private bool captureBaseSimulationSpeed = true;

        private float[] _baseSimulationSpeeds;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (particleSystems == null || !captureBaseSimulationSpeed)
                return;

            _baseSimulationSpeeds = new float[particleSystems.Length];
            for (int i = 0; i < particleSystems.Length; i++)
                _baseSimulationSpeeds[i] = particleSystems[i] != null ? particleSystems[i].main.simulationSpeed : 1f;
        }

        protected override void ApplyScale(float scale)
        {
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                float baseSpeed = _baseSimulationSpeeds != null && i < _baseSimulationSpeeds.Length ? _baseSimulationSpeeds[i] : 1f;
                main.simulationSpeed = Mathf.Max(0f, baseSpeed * scale);
            }
        }
    }
}
