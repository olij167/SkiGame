using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Pungent Weather Output Applier")]
    public sealed class PungentWeatherOutputApplier : MonoBehaviour
    {
        private static readonly int ID_Wetness = Shader.PropertyToID("_Wetness");
        private static readonly int ID_SnowAmount = Shader.PropertyToID("_SnowAmount");
        private static readonly int ID_IsSnowing = Shader.PropertyToID("_isSnowing");
        private static readonly int ID_CloudAlpha = Shader.PropertyToID("_CloudAlpha");
        private static readonly int ID_CloudPower = Shader.PropertyToID("_CloudPower");
        private static readonly int ID_CloudSpeed = Shader.PropertyToID("_CloudSpeed");
        private static readonly int ID_CloudSoftness = Shader.PropertyToID("_CloudSoftness");
        private static readonly int ID_CloudColor = Shader.PropertyToID("_CloudColor");
        private static readonly int ID_FogColor = Shader.PropertyToID("_FogColor");
        private static readonly int ID_FogDensity = Shader.PropertyToID("_FogDensity");
        private static readonly int ID_WeatherCloudShadowStrength = Shader.PropertyToID("_WeatherCloudShadowStrength");
        private static readonly int ID_WeatherCloudShadowTint = Shader.PropertyToID("_WeatherCloudShadowTint");

        [SerializeField] private bool writeGlobalShaderProperties = true;
        [SerializeField] private bool writeRenderSettingsFog = true;
        [SerializeField] private float fogDensityMultiplier = 0.05f;
        [SerializeField] private Renderer[] sceneCloudRenderers;
        [SerializeField] private ParticleSystem[] precipitationParticles;
        [SerializeField] private AudioSource ambientWeatherAudio;

        private MaterialPropertyBlock _block;

        public void Apply(PungentWeatherState state)
        {
            if (writeRenderSettingsFog)
            {
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, state.atmosphereTint, Mathf.Clamp01(state.fogStrength));
                RenderSettings.fogDensity = Mathf.Max(0f, state.fogStrength * fogDensityMultiplier);
            }

            if (writeGlobalShaderProperties)
            {
                Shader.SetGlobalFloat(ID_Wetness, state.wetness);
                Shader.SetGlobalFloat(ID_SnowAmount, state.snowiness);
                Shader.SetGlobalFloat(ID_IsSnowing, state.snowiness > 0.01f && state.isPrecipitating ? 1f : 0f);
                Shader.SetGlobalFloat(ID_CloudAlpha, Mathf.Clamp01(state.skyCloudAlpha > 0f ? state.skyCloudAlpha : state.cloudCoverage));
                Shader.SetGlobalFloat(ID_CloudPower, Mathf.Max(0f, state.skyCloudPower > 0f ? state.skyCloudPower : Mathf.Lerp(5f, 0.6f, Mathf.Clamp01(state.cloudDensity))));
                Shader.SetGlobalFloat(ID_CloudSoftness, Mathf.Max(0f, state.skyCloudSoftness > 0f ? state.skyCloudSoftness : Mathf.Lerp(0.05f, 0.35f, state.cloudSoftness)));
                Shader.SetGlobalFloat(ID_WeatherCloudShadowStrength, Mathf.Clamp01(state.terrainCloudShadowStrength > 0f ? state.terrainCloudShadowStrength : state.cloudCoverage * state.cloudDensity));
                Shader.SetGlobalColor(ID_WeatherCloudShadowTint, state.cloudShadowTint);
            }

            ApplySceneClouds(state);
            ApplyParticles(state);
            ApplyAudio(state);
        }

        private void ApplySceneClouds(PungentWeatherState state)
        {
            if (sceneCloudRenderers == null)
                return;

            if (_block == null)
                _block = new MaterialPropertyBlock();

            float cloudPower = state.skyCloudPower > 0f ? state.skyCloudPower : Mathf.Lerp(5f, 0.6f, Mathf.Clamp01(state.cloudDensity));
            Vector3 direction = state.WindDirection;
            Vector4 speed = new Vector4(direction.x, direction.z, state.cloudTurbulence, state.cloudWarpStrength) * state.windSpeed * 0.001f;

            for (int i = 0; i < sceneCloudRenderers.Length; i++)
            {
                Renderer renderer = sceneCloudRenderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_block);
                _block.SetFloat(ID_CloudAlpha, Mathf.Clamp01(state.skyCloudAlpha > 0f ? state.skyCloudAlpha : state.cloudCoverage));
                _block.SetFloat(ID_CloudPower, cloudPower);
                _block.SetVector(ID_CloudSpeed, speed);
                _block.SetFloat(ID_CloudSoftness, state.skyCloudSoftness > 0f ? state.skyCloudSoftness : Mathf.Lerp(0.05f, 0.35f, state.cloudSoftness));
                _block.SetColor(ID_CloudColor, state.cloudTint);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void ApplyParticles(PungentWeatherState state)
        {
            if (precipitationParticles == null)
                return;

            for (int i = 0; i < precipitationParticles.Length; i++)
            {
                ParticleSystem ps = precipitationParticles[i];
                if (ps == null)
                    continue;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = state.isPrecipitating ? Mathf.Lerp(25f, 1200f, Mathf.Clamp01(state.precipitationChance / 100f)) : 0f;
                if (state.isPrecipitating && !ps.isPlaying)
                    ps.Play();
                else if (!state.isPrecipitating && ps.isPlaying)
                    ps.Stop();
            }
        }

        private void ApplyAudio(PungentWeatherState state)
        {
            if (ambientWeatherAudio == null)
                return;

            ambientWeatherAudio.volume = Mathf.Clamp01(state.isPrecipitating ? 0.35f + state.storminess * 0.55f : state.windSpeed * 0.08f);
            ambientWeatherAudio.pitch = Mathf.Lerp(0.9f, 1.15f, Mathf.Clamp01(state.storminess));
        }
    }

}
