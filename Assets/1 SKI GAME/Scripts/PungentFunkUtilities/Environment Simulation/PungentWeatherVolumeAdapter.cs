using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    public enum PungentWeatherMetric
    {
        Temperature,
        PrecipitationChance,
        CloudCoverage,
        CloudDensity,
        Storminess,
        FogStrength,
        Gloom,
        WindSpeed,
        GustStrength,
        Wetness,
        Snowiness,
        SkyCloudAlpha,
        SkyCloudPower,
        SkyCloudSoftness,
        CloudCoverageBias,
        CloudTurbulence,
        CloudWarpStrength,
        TerrainCloudShadowStrength,
        VolumeDensity
    }

    [Serializable]
    public sealed class PungentWeatherVolumeFloatBinding
    {
        public string volumeComponentType = "Fog";
        public string parameterName = "density";
        public PungentWeatherMetric metric = PungentWeatherMetric.FogStrength;
        public Vector2 inputRange = new Vector2(0f, 1f);
        public Vector2 outputRange = new Vector2(0f, 1f);
        public bool forceOverrideState = true;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Weather Volume Adapter")]
    public sealed class PungentWeatherVolumeAdapter : MonoBehaviour
    {
        [SerializeField] private PungentWeatherController weather;
        [SerializeField] private Component volume;
        [SerializeField] private bool applyContinuously = true;
        [SerializeField] private PungentWeatherVolumeFloatBinding[] floatBindings =
        {
            new PungentWeatherVolumeFloatBinding { volumeComponentType = "Fog", parameterName = "density", metric = PungentWeatherMetric.FogStrength, outputRange = new Vector2(0f, 1f) },
            new PungentWeatherVolumeFloatBinding { volumeComponentType = "VolumetricClouds", parameterName = "densityMultiplier", metric = PungentWeatherMetric.VolumeDensity, outputRange = new Vector2(0f, 1f) },
            new PungentWeatherVolumeFloatBinding { volumeComponentType = "ColorAdjustments", parameterName = "postExposure", metric = PungentWeatherMetric.Gloom, outputRange = new Vector2(0f, -1.5f) }
        };

        private void OnEnable()
        {
            if (weather == null)
                weather = FindAnyObjectByType<PungentWeatherController>();

            if (weather != null)
                weather.WeatherChanged += Apply;
        }

        private void OnDisable()
        {
            if (weather != null)
                weather.WeatherChanged -= Apply;
        }

        private void Update()
        {
            if (applyContinuously && weather != null)
                Apply(weather.CurrentState);
        }

        public void Apply(PungentWeatherState state)
        {
            object profile = GetVolumeProfile();
            if (profile == null || floatBindings == null)
                return;

            for (int i = 0; i < floatBindings.Length; i++)
                ApplyFloatBinding(profile, floatBindings[i], state);
        }

        private object GetVolumeProfile()
        {
            if (volume == null)
                return null;

            Type type = volume.GetType();
            PropertyInfo profile = type.GetProperty("profile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object value = profile != null ? profile.GetValue(volume, null) : null;
            if (value != null)
                return value;

            PropertyInfo sharedProfile = type.GetProperty("sharedProfile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return sharedProfile != null ? sharedProfile.GetValue(volume, null) : null;
        }

        private static void ApplyFloatBinding(object profile, PungentWeatherVolumeFloatBinding binding, PungentWeatherState state)
        {
            if (profile == null || binding == null || string.IsNullOrWhiteSpace(binding.volumeComponentType) || string.IsNullOrWhiteSpace(binding.parameterName))
                return;

            object component = FindVolumeComponent(profile, binding.volumeComponentType);
            object parameter = component != null ? GetMemberValue(component, binding.parameterName) : null;
            if (parameter == null)
                return;

            float normalized = Mathf.InverseLerp(binding.inputRange.x, binding.inputRange.y, ReadMetric(binding.metric, state));
            float output = Mathf.Lerp(binding.outputRange.x, binding.outputRange.y, normalized);
            SetParameterValue(parameter, output, binding.forceOverrideState);
        }

        private static object FindVolumeComponent(object profile, string typeName)
        {
            object components = GetMemberValue(profile, "components");
            if (!(components is IEnumerable enumerable))
                return null;

            foreach (object component in enumerable)
            {
                if (component == null)
                    continue;

                Type type = component.GetType();
                if (string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                    type.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return component;
            }

            return null;
        }

        private static object GetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            Type type = target.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(target);

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static void SetParameterValue(object parameter, float value, bool forceOverrideState)
        {
            Type type = parameter.GetType();
            PropertyInfo valueProperty = type.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (valueProperty != null && valueProperty.CanWrite)
                valueProperty.SetValue(parameter, Convert.ChangeType(value, valueProperty.PropertyType), null);

            if (!forceOverrideState)
                return;

            PropertyInfo overrideState = type.GetProperty("overrideState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (overrideState != null && overrideState.CanWrite)
                overrideState.SetValue(parameter, true, null);
        }

        private static float ReadMetric(PungentWeatherMetric metric, PungentWeatherState state)
        {
            switch (metric)
            {
                case PungentWeatherMetric.Temperature: return state.temperature;
                case PungentWeatherMetric.PrecipitationChance: return state.precipitationChance;
                case PungentWeatherMetric.CloudCoverage: return state.cloudCoverage;
                case PungentWeatherMetric.CloudDensity: return state.cloudDensity;
                case PungentWeatherMetric.Storminess: return state.storminess;
                case PungentWeatherMetric.FogStrength: return state.fogStrength;
                case PungentWeatherMetric.Gloom: return state.gloom;
                case PungentWeatherMetric.WindSpeed: return state.windSpeed;
                case PungentWeatherMetric.GustStrength: return state.gustStrength;
                case PungentWeatherMetric.Wetness: return state.wetness;
                case PungentWeatherMetric.Snowiness: return state.snowiness;
                case PungentWeatherMetric.SkyCloudAlpha: return state.skyCloudAlpha;
                case PungentWeatherMetric.SkyCloudPower: return state.skyCloudPower;
                case PungentWeatherMetric.SkyCloudSoftness: return state.skyCloudSoftness;
                case PungentWeatherMetric.CloudCoverageBias: return state.cloudCoverageBias;
                case PungentWeatherMetric.CloudTurbulence: return state.cloudTurbulence;
                case PungentWeatherMetric.CloudWarpStrength: return state.cloudWarpStrength;
                case PungentWeatherMetric.TerrainCloudShadowStrength: return state.terrainCloudShadowStrength;
                case PungentWeatherMetric.VolumeDensity: return state.volumeDensity;
                default: return 0f;
            }
        }
    }
}
