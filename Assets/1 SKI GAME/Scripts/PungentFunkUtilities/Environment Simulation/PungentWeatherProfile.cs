using System;
using UnityEngine;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [Serializable]
    public struct PungentWeatherState
    {
        public string presetId;
        public string displayName;
        public string glyph;
        public float temperature;
        public float precipitationChance;
        public bool isPrecipitating;
        public float cloudCoverage;
        public float cloudDensity;
        public float cloudSoftness;
        public float storminess;
        public float fogStrength;
        public float gloom;
        public float windSpeed;
        public float windDirectionDegrees;
        public float gustStrength;
        public float wetness;
        public float snowiness;
        public Color atmosphereTint;
        public Color cloudTint;
        public Color cloudShadowTint;
        public float skyCloudAlpha;
        public float skyCloudPower;
        public float skyCloudSoftness;
        public float cloudCoverageBias;
        public float cloudTurbulence;
        public float cloudWarpStrength;
        public float terrainCloudShadowStrength;
        public float volumeDensity;

        public Vector3 WindDirection
        {
            get
            {
                float radians = windDirectionDegrees * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)).normalized;
            }
        }
    }

    [Serializable]
    public sealed class PungentWeatherPreset
    {
        public string id = "clear";
        public string displayName = "Clear";
        public string glyph = "Clear";

        [Header("Selection")]
        public string season = string.Empty;
        public Vector2 temperatureRange = new Vector2(-5f, 30f);
        public Vector2 precipitationChanceRange = new Vector2(0f, 35f);
        public bool isPrecipitating;

        [Header("Simple Authoring")]
        public bool useAdvancedOverrides;
        [Range(0f, 1f)] public float cloudCoverage = 0.12f;
        [Range(0f, 1f)] public float cloudDensity = 0.18f;
        [Range(0f, 1f)] public float cloudSoftness = 0.55f;
        [Range(0f, 1f)] public float storminess = 0.02f;
        [Range(0f, 1f)] public float fogginess = 0.02f;
        [Range(0f, 1f)] public float gloom = 0.06f;
        [Range(0f, 1f)] public float windiness = 0.18f;
        [Range(0f, 5f)] public float wetness;
        [Range(0f, 1f)] public float snowiness;
        public Color atmosphereTint = Color.white;

        [Header("Advanced Outputs")]
        public Vector2 windSpeedRange = new Vector2(0.2f, 1f);
        public Vector2 windDirectionRange = new Vector2(0f, 360f);
        public Vector2 gustStrengthRange = new Vector2(0.02f, 0.2f);
        [Range(0f, 1f)] public float skyCloudAlpha = 0.12f;
        [Range(0f, 5f)] public float skyCloudPower = 4.4f;
        [Range(0.01f, 0.5f)] public float skyCloudSoftness = 0.18f;
        [Range(-0.5f, 0.5f)] public float cloudCoverageBias = 0.12f;
        [Range(0f, 1f)] public float cloudTurbulence = 0.2f;
        [Range(0f, 1f)] public float cloudWarpStrength = 0.08f;
        public Color cloudTint = Color.white;
        public Color cloudShadowTint = new Color(0.72f, 0.77f, 0.84f, 1f);
        [Range(0f, 1f)] public float terrainCloudShadowStrength = 0.08f;
        [Range(0f, 2f)] public float volumeDensity = 0.2f;

        public PungentWeatherState BuildState(int seed, float temperature, float precipitationChance, string activeSeason)
        {
            System.Random rng = new System.Random(seed);
            PungentWeatherResolvedProfile resolved = useAdvancedOverrides
                ? PungentWeatherResolvedProfile.FromAdvanced(this)
                : PungentWeatherResolvedProfile.FromSimple(this);

            return new PungentWeatherState
            {
                presetId = id,
                displayName = displayName,
                glyph = glyph,
                temperature = temperature,
                precipitationChance = precipitationChance,
                isPrecipitating = isPrecipitating,
                cloudCoverage = resolved.cloudCoverage,
                cloudDensity = resolved.cloudDensity,
                cloudSoftness = resolved.cloudSoftness,
                storminess = resolved.storminess,
                fogStrength = resolved.fogStrength,
                gloom = resolved.gloom,
                windSpeed = Mathf.Lerp(resolved.windSpeedRange.x, resolved.windSpeedRange.y, (float)rng.NextDouble()),
                windDirectionDegrees = Mathf.Lerp(resolved.windDirectionRange.x, resolved.windDirectionRange.y, (float)rng.NextDouble()),
                gustStrength = Mathf.Lerp(resolved.gustStrengthRange.x, resolved.gustStrengthRange.y, (float)rng.NextDouble()),
                wetness = wetness,
                snowiness = temperature <= 0f && isPrecipitating ? Mathf.Max(snowiness, 0.65f) : snowiness,
                atmosphereTint = atmosphereTint.a <= 0f ? Color.white : atmosphereTint,
                cloudTint = resolved.cloudTint,
                cloudShadowTint = resolved.cloudShadowTint,
                skyCloudAlpha = resolved.skyCloudAlpha,
                skyCloudPower = resolved.skyCloudPower,
                skyCloudSoftness = resolved.skyCloudSoftness,
                cloudCoverageBias = resolved.cloudCoverageBias,
                cloudTurbulence = resolved.cloudTurbulence,
                cloudWarpStrength = resolved.cloudWarpStrength,
                terrainCloudShadowStrength = resolved.terrainCloudShadowStrength,
                volumeDensity = resolved.volumeDensity
            };
        }
    }

    public struct PungentWeatherResolvedProfile
    {
        public float cloudCoverage;
        public float cloudDensity;
        public float cloudSoftness;
        public float storminess;
        public float fogStrength;
        public float gloom;
        public Vector2 windSpeedRange;
        public Vector2 windDirectionRange;
        public Vector2 gustStrengthRange;
        public Color cloudTint;
        public Color cloudShadowTint;
        public float skyCloudAlpha;
        public float skyCloudPower;
        public float skyCloudSoftness;
        public float cloudCoverageBias;
        public float cloudTurbulence;
        public float cloudWarpStrength;
        public float terrainCloudShadowStrength;
        public float volumeDensity;

        public static PungentWeatherResolvedProfile FromAdvanced(PungentWeatherPreset preset)
        {
            return new PungentWeatherResolvedProfile
            {
                cloudCoverage = Mathf.Clamp01(preset.skyCloudAlpha),
                cloudDensity = Mathf.Clamp01(1f - Mathf.InverseLerp(0.5f, 5f, preset.skyCloudPower)),
                cloudSoftness = Mathf.Clamp01(Mathf.InverseLerp(0.01f, 0.5f, preset.skyCloudSoftness)),
                storminess = Mathf.Clamp01(preset.cloudTurbulence * 0.6f + preset.cloudWarpStrength * 0.4f),
                fogStrength = preset.fogginess,
                gloom = preset.gloom,
                windSpeedRange = SafeRange(preset.windSpeedRange, 0f, 100f),
                windDirectionRange = SafeRange(preset.windDirectionRange, -360f, 360f),
                gustStrengthRange = SafeRange(preset.gustStrengthRange, 0f, 10f),
                cloudTint = preset.cloudTint,
                cloudShadowTint = preset.cloudShadowTint,
                skyCloudAlpha = Mathf.Clamp01(preset.skyCloudAlpha),
                skyCloudPower = Mathf.Max(0f, preset.skyCloudPower),
                skyCloudSoftness = Mathf.Clamp(preset.skyCloudSoftness, 0.01f, 0.5f),
                cloudCoverageBias = Mathf.Clamp(preset.cloudCoverageBias, -0.5f, 0.5f),
                cloudTurbulence = Mathf.Clamp01(preset.cloudTurbulence),
                cloudWarpStrength = Mathf.Clamp01(preset.cloudWarpStrength),
                terrainCloudShadowStrength = Mathf.Clamp01(preset.terrainCloudShadowStrength),
                volumeDensity = Mathf.Max(0f, preset.volumeDensity)
            };
        }

        public static PungentWeatherResolvedProfile FromSimple(PungentWeatherPreset preset)
        {
            float coverage = Mathf.Clamp01(preset.cloudCoverage);
            float density = Mathf.Clamp01(preset.cloudDensity);
            float storm = Mathf.Clamp01(preset.storminess);
            float gloom = Mathf.Clamp01(preset.gloom);
            float wind = Mathf.Clamp01(preset.windiness);
            Color tint = preset.atmosphereTint.a <= 0f ? Color.white : preset.atmosphereTint;
            return new PungentWeatherResolvedProfile
            {
                cloudCoverage = coverage,
                cloudDensity = density,
                cloudSoftness = Mathf.Clamp01(preset.cloudSoftness),
                storminess = storm,
                fogStrength = Mathf.Clamp01(preset.fogginess),
                gloom = gloom,
                windSpeedRange = new Vector2(Mathf.Lerp(0.05f, 1.25f, wind), Mathf.Lerp(0.2f, 2.3f, Mathf.Clamp01(wind * 0.7f + storm * 0.3f))),
                windDirectionRange = new Vector2(0f, Mathf.Lerp(35f, 360f, Mathf.Clamp01(storm * 0.6f + wind * 0.4f))),
                gustStrengthRange = new Vector2(Mathf.Lerp(0.01f, 0.18f, wind), Mathf.Lerp(0.06f, 0.85f, Mathf.Clamp01(storm * 0.7f + wind * 0.3f))),
                cloudTint = Color.Lerp(Color.white, tint, 0.65f),
                cloudShadowTint = Color.Lerp(new Color(0.72f, 0.77f, 0.84f, 1f), tint * 0.85f, 0.35f + gloom * 0.35f),
                skyCloudAlpha = coverage,
                skyCloudPower = Mathf.Lerp(5f, 0.65f, density),
                skyCloudSoftness = Mathf.Lerp(0.05f, 0.35f, Mathf.Clamp01(preset.cloudSoftness)),
                cloudCoverageBias = Mathf.Lerp(-0.08f, 0.28f, coverage),
                cloudTurbulence = Mathf.Clamp01(storm * 0.75f + wind * 0.25f),
                cloudWarpStrength = Mathf.Clamp01(storm * 0.6f + wind * 0.15f),
                terrainCloudShadowStrength = Mathf.Clamp01(coverage * density * (0.45f + gloom * 0.55f)),
                volumeDensity = Mathf.Clamp01(density * 0.55f + preset.fogginess * 0.45f)
            };
        }

        private static Vector2 SafeRange(Vector2 range, float min, float max)
        {
            float a = Mathf.Clamp(range.x, min, max);
            float b = Mathf.Clamp(range.y, min, max);
            return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b));
        }
    }

    [CreateAssetMenu(menuName = "PungentFunk/Environment Simulation/Weather Profile", fileName = "Pungent Weather Profile")]
    public sealed class PungentWeatherProfile : ScriptableObject
    {
        [Serializable]
        public sealed class SeasonWeatherRule
        {
            public string season = "Winter";
            public Vector2 temperatureRange = new Vector2(-10f, 8f);
            public Vector2 precipitationChanceRange = new Vector2(20f, 70f);
        }

        [SerializeField] private int seed = 1024;
        [SerializeField] private SeasonWeatherRule[] seasonRules =
        {
            new SeasonWeatherRule { season = "Winter", temperatureRange = new Vector2(-12f, 6f), precipitationChanceRange = new Vector2(25f, 75f) },
            new SeasonWeatherRule { season = "Spring", temperatureRange = new Vector2(4f, 18f), precipitationChanceRange = new Vector2(15f, 60f) },
            new SeasonWeatherRule { season = "Summer", temperatureRange = new Vector2(14f, 32f), precipitationChanceRange = new Vector2(5f, 40f) },
            new SeasonWeatherRule { season = "Autumn", temperatureRange = new Vector2(3f, 18f), precipitationChanceRange = new Vector2(20f, 70f) }
        };
        [SerializeField] private PungentWeatherPreset[] presets =
        {
            new PungentWeatherPreset { id = "clear", displayName = "Clear", glyph = "Clear", cloudCoverage = 0.06f, cloudDensity = 0.08f, windiness = 0.18f },
            new PungentWeatherPreset { id = "cloudy", displayName = "Cloudy", glyph = "Cloudy", cloudCoverage = 0.55f, cloudDensity = 0.35f, gloom = 0.22f, windiness = 0.28f, precipitationChanceRange = new Vector2(10f, 65f) },
            new PungentWeatherPreset { id = "rain", displayName = "Rain", glyph = "Rain", cloudCoverage = 0.9f, cloudDensity = 0.78f, storminess = 0.35f, fogginess = 0.35f, gloom = 0.62f, windiness = 0.58f, isPrecipitating = true, precipitationChanceRange = new Vector2(45f, 100f), wetness = 3.2f },
            new PungentWeatherPreset { id = "snow", displayName = "Snow", glyph = "Snow", cloudCoverage = 0.86f, cloudDensity = 0.72f, storminess = 0.25f, fogginess = 0.42f, gloom = 0.55f, windiness = 0.46f, isPrecipitating = true, temperatureRange = new Vector2(-40f, 2f), precipitationChanceRange = new Vector2(35f, 100f), snowiness = 0.85f }
        };

        public int Seed => seed;
        public PungentWeatherPreset[] Presets => presets;

        public SeasonWeatherRule FindSeasonRule(string season)
        {
            if (seasonRules == null)
                return null;

            for (int i = 0; i < seasonRules.Length; i++)
            {
                SeasonWeatherRule rule = seasonRules[i];
                if (rule != null && string.Equals(rule.season, season, StringComparison.OrdinalIgnoreCase))
                    return rule;
            }

            return null;
        }
    }
}
