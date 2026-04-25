using UnityEngine;

namespace TimeWeather
{
    public enum WeatherPresetSeedType
    {
        Clear,
        PartlyCloudy,
        Overcast,
        Rain,
        Snow,
        Blizzard,
        Storm
    }

    public static class WeatherPresetAuthoringUtility
    {
        public struct DerivedWeatherPreview
        {
            public float skyCloudAlpha;
            public float skyCloudPower;
            public float sceneCloudAlpha;
            public float sceneCloudPower;
            public float terrainShadowStrength;
            public float volumetricDensity;
            public float fogStrength;
            public float skyFogInfluence;
            public float shadowIntensity;
        }

        public struct ResolvedWeatherProfile
        {
            public Vector2 cloudPowerRange;
            public Vector2 cloudAlphaRange;
            public Vector2 windSpeedRange;
            public Vector2 windDirectionRange;
            public Vector2 gustStrengthRange;
            public Vector2 skyCloudAlphaRange;
            public Vector2 skyCloudPowerRange;
            public float cloudSoftness;
            public float cloudCoverageBias;
            public float cloudTurbulence;
            public float cloudWarpStrength;
            public Color cloudTint;
            public float cloudShadowStrength;
            public float cloudGreyStrength;
            public float cloudSunLightStrength;
            public Vector2 sceneCloudAlphaRange;
            public Vector2 sceneCloudPowerRange;
            public float sceneCloudSoftness;
            public float sceneCloudCoverageBias;
            public float sceneCloudTurbulence;
            public float sceneCloudWarpStrength;
            public Color sceneCloudTint;
            public Color sceneCloudShadowTint;
            public float planeEdgeFade;
            public float planeRadialFade;
            public float planeHeightFade;
            public float sceneCloudFresnelFade;
            public float sceneCloudMacroRoundness;
            public float sceneCloudDetailStrength;
            public float sceneCloudDensityContrast;
            public float sceneCloudErosionStrength;
            public float sceneCloudSecondLayerStrength;
            public float sceneCloudSilverLiningStrength;
            public float sceneCloudBottomDarkening;
            public float sceneCloudDriftMultiplier;
            public float terrainCloudShadowStrength;
            public float terrainCloudShadowScale;
            public float terrainCloudShadowSoftness;
            public float terrainCloudShadowBias;
            public float terrainCloudShadowDriftMultiplier;
            public Color terrainCloudShadowTint;
            public float volumeDensity;
            public float volumeAbsorption;
            public float volumeLightingStrength;
            public float volumeAlphaMultiplier;
            public float volumeClusterScale;
            public float volumeClusterDensity;
            public float volumeDetailStrength;
            public float volumeEdgeFade;
            public float fogStrength;
        }

        public static void EnsureSimpleAuthoringInitialized(WeatherController.WeatherData preset)
        {
            if (preset == null || preset.simpleAuthoringInitialized)
                return;

            InferSimpleControlsFromAdvanced(preset);
            preset.simpleAuthoringInitialized = true;
        }

        public static void ApplySeed(WeatherController.WeatherData preset, WeatherPresetSeedType seedType)
        {
            if (preset == null)
                return;

            EnsureSimpleAuthoringInitialized(preset);
            preset.useAdvancedOverrides = false;

            switch (seedType)
            {
                case WeatherPresetSeedType.Clear:
                    preset.simpleCloudCoverage = 0.06f;
                    preset.simpleCloudHeaviness = 0.08f;
                    preset.simpleCloudSoftness = 0.45f;
                    preset.simpleStorminess = 0.00f;
                    preset.simpleFogginess = 0.02f;
                    preset.simpleGloom = 0.05f;
                    preset.simpleWindiness = 0.18f;
                    preset.overallWeatherTint = Color.white;
                    preset.isRaining = false;
                    preset.wetness = 0f;
                    preset.snowiness = 0f;
                    break;
                case WeatherPresetSeedType.PartlyCloudy:
                    preset.simpleCloudCoverage = 0.38f;
                    preset.simpleCloudHeaviness = 0.28f;
                    preset.simpleCloudSoftness = 0.52f;
                    preset.simpleStorminess = 0.05f;
                    preset.simpleFogginess = 0.05f;
                    preset.simpleGloom = 0.16f;
                    preset.simpleWindiness = 0.26f;
                    preset.overallWeatherTint = Color.white;
                    preset.isRaining = false;
                    preset.wetness = 0f;
                    preset.snowiness = 0f;
                    break;
                case WeatherPresetSeedType.Overcast:
                    preset.simpleCloudCoverage = 0.82f;
                    preset.simpleCloudHeaviness = 0.76f;
                    preset.simpleCloudSoftness = 0.36f;
                    preset.simpleStorminess = 0.12f;
                    preset.simpleFogginess = 0.22f;
                    preset.simpleGloom = 0.58f;
                    preset.simpleWindiness = 0.34f;
                    preset.overallWeatherTint = new Color(0.94f, 0.97f, 1f, 1f);
                    preset.isRaining = false;
                    preset.wetness = 0.4f;
                    preset.snowiness = 0f;
                    break;
                case WeatherPresetSeedType.Rain:
                    preset.simpleCloudCoverage = 0.92f;
                    preset.simpleCloudHeaviness = 0.84f;
                    preset.simpleCloudSoftness = 0.28f;
                    preset.simpleStorminess = 0.46f;
                    preset.simpleFogginess = 0.42f;
                    preset.simpleGloom = 0.66f;
                    preset.simpleWindiness = 0.56f;
                    preset.overallWeatherTint = new Color(0.90f, 0.96f, 1f, 1f);
                    preset.isRaining = true;
                    preset.wetness = 3.25f;
                    preset.snowiness = 0f;
                    break;
                case WeatherPresetSeedType.Snow:
                    preset.simpleCloudCoverage = 0.88f;
                    preset.simpleCloudHeaviness = 0.74f;
                    preset.simpleCloudSoftness = 0.42f;
                    preset.simpleStorminess = 0.28f;
                    preset.simpleFogginess = 0.38f;
                    preset.simpleGloom = 0.60f;
                    preset.simpleWindiness = 0.44f;
                    preset.overallWeatherTint = new Color(0.92f, 0.97f, 1.05f, 1f);
                    preset.isRaining = true;
                    preset.wetness = 0.3f;
                    preset.snowiness = 0.62f;
                    break;
                case WeatherPresetSeedType.Blizzard:
                    preset.simpleCloudCoverage = 1.00f;
                    preset.simpleCloudHeaviness = 0.96f;
                    preset.simpleCloudSoftness = 0.20f;
                    preset.simpleStorminess = 0.82f;
                    preset.simpleFogginess = 0.86f;
                    preset.simpleGloom = 0.84f;
                    preset.simpleWindiness = 0.92f;
                    preset.overallWeatherTint = new Color(0.88f, 0.95f, 1.08f, 1f);
                    preset.isRaining = true;
                    preset.wetness = 0.2f;
                    preset.snowiness = 1.0f;
                    break;
                case WeatherPresetSeedType.Storm:
                    preset.simpleCloudCoverage = 0.96f;
                    preset.simpleCloudHeaviness = 0.94f;
                    preset.simpleCloudSoftness = 0.18f;
                    preset.simpleStorminess = 1.00f;
                    preset.simpleFogginess = 0.56f;
                    preset.simpleGloom = 0.92f;
                    preset.simpleWindiness = 0.96f;
                    preset.overallWeatherTint = new Color(0.84f, 0.92f, 1.02f, 1f);
                    preset.isRaining = true;
                    preset.wetness = 4.2f;
                    preset.snowiness = 0f;
                    break;
            }
        }

        public static DerivedWeatherPreview BuildPreview(WeatherController.WeatherData preset, float skyFogVisualStart, float skyFogVisualEnd)
        {
            ResolvedWeatherProfile resolved = BuildResolvedProfile(preset);
            float skyAlpha = Midpoint(resolved.skyCloudAlphaRange);
            float skyPower = Midpoint(resolved.skyCloudPowerRange);
            float sceneAlpha = Midpoint(resolved.sceneCloudAlphaRange);
            float scenePower = Midpoint(resolved.sceneCloudPowerRange);

            return new DerivedWeatherPreview
            {
                skyCloudAlpha = skyAlpha,
                skyCloudPower = skyPower,
                sceneCloudAlpha = sceneAlpha,
                sceneCloudPower = scenePower,
                terrainShadowStrength = resolved.terrainCloudShadowStrength,
                volumetricDensity = resolved.volumeDensity,
                fogStrength = resolved.fogStrength,
                skyFogInfluence = Mathf.InverseLerp(
                    skyFogVisualStart,
                    Mathf.Max(skyFogVisualStart + 0.0001f, skyFogVisualEnd),
                    resolved.fogStrength),
                shadowIntensity = Mathf.Clamp01((resolved.terrainCloudShadowStrength + resolved.cloudShadowStrength) * 0.5f)
            };
        }

        public static ResolvedWeatherProfile BuildResolvedProfile(WeatherController.WeatherData preset)
        {
            EnsureSimpleAuthoringInitialized(preset);

            if (preset == null)
                return new ResolvedWeatherProfile();

            if (preset.useAdvancedOverrides)
            {
                return new ResolvedWeatherProfile
                {
                    cloudPowerRange = preset.cloudPowerRange,
                    cloudAlphaRange = preset.cloudAlphaRange,
                    windSpeedRange = preset.windSpeedRange,
                    windDirectionRange = preset.windDirectionRange,
                    gustStrengthRange = preset.gustStrengthRange,
                    skyCloudAlphaRange = preset.skyCloudAlphaRange,
                    skyCloudPowerRange = preset.skyCloudPowerRange,
                    cloudSoftness = preset.cloudSoftness,
                    cloudCoverageBias = preset.cloudCoverageBias,
                    cloudTurbulence = preset.cloudTurbulence,
                    cloudWarpStrength = preset.cloudWarpStrength,
                    cloudTint = preset.cloudTint,
                    cloudShadowStrength = preset.cloudShadowStrength,
                    cloudGreyStrength = preset.cloudGreyStrength,
                    cloudSunLightStrength = preset.cloudSunLightStrength,
                    sceneCloudAlphaRange = preset.sceneCloudAlphaRange,
                    sceneCloudPowerRange = preset.sceneCloudPowerRange,
                    sceneCloudSoftness = preset.sceneCloudSoftness,
                    sceneCloudCoverageBias = preset.sceneCloudCoverageBias,
                    sceneCloudTurbulence = preset.sceneCloudTurbulence,
                    sceneCloudWarpStrength = preset.sceneCloudWarpStrength,
                    sceneCloudTint = preset.sceneCloudTint,
                    sceneCloudShadowTint = preset.sceneCloudShadowTint,
                    planeEdgeFade = preset.planeEdgeFade,
                    planeRadialFade = preset.planeRadialFade,
                    planeHeightFade = preset.planeHeightFade,
                    sceneCloudFresnelFade = preset.sceneCloudFresnelFade,
                    sceneCloudMacroRoundness = preset.sceneCloudMacroRoundness,
                    sceneCloudDetailStrength = preset.sceneCloudDetailStrength,
                    sceneCloudDensityContrast = preset.sceneCloudDensityContrast,
                    sceneCloudErosionStrength = preset.sceneCloudErosionStrength,
                    sceneCloudSecondLayerStrength = preset.sceneCloudSecondLayerStrength,
                    sceneCloudSilverLiningStrength = preset.sceneCloudSilverLiningStrength,
                    sceneCloudBottomDarkening = preset.sceneCloudBottomDarkening,
                    sceneCloudDriftMultiplier = preset.sceneCloudDriftMultiplier,
                    terrainCloudShadowStrength = preset.terrainCloudShadowStrength,
                    terrainCloudShadowScale = preset.terrainCloudShadowScale,
                    terrainCloudShadowSoftness = preset.terrainCloudShadowSoftness,
                    terrainCloudShadowBias = preset.terrainCloudShadowBias,
                    terrainCloudShadowDriftMultiplier = preset.terrainCloudShadowDriftMultiplier,
                    terrainCloudShadowTint = preset.terrainCloudShadowTint,
                    volumeDensity = preset.volumeDensity,
                    volumeAbsorption = preset.volumeAbsorption,
                    volumeLightingStrength = preset.volumeLightingStrength,
                    volumeAlphaMultiplier = preset.volumeAlphaMultiplier,
                    volumeClusterScale = preset.volumeClusterScale,
                    volumeClusterDensity = preset.volumeClusterDensity,
                    volumeDetailStrength = preset.volumeDetailStrength,
                    volumeEdgeFade = preset.volumeEdgeFade,
                    fogStrength = preset.fogStrength
                };
            }

            float coverage = preset.simpleCloudCoverage;
            float heaviness = preset.simpleCloudHeaviness;
            float softness01 = preset.simpleCloudSoftness;
            float storm = preset.simpleStorminess;
            float fog = preset.simpleFogginess;
            float gloom = preset.simpleGloom;
            float windiness = preset.simpleWindiness;
            Color tint = preset.overallWeatherTint.a <= 0f ? Color.white : preset.overallWeatherTint;

            CloudResponse cloud = BuildUnifiedCloudResponse(coverage, heaviness, storm, fog, gloom);
            float density = cloud.density;
            float threat = cloud.threat;
            float softness = Mathf.Lerp(0.06f, 0.30f, softness01);
            float sceneSoftness = Mathf.Lerp(0.08f, 0.34f, softness01);
            float turbulence = cloud.turbulence;
            float warp = cloud.warp;
            float fogStrength = cloud.fogStrength;
            float directionalSpread = Mathf.Lerp(35f, 360f, Mathf.Clamp01(0.25f + storm * 0.35f + windiness * 0.40f));
            float baseWind = Mathf.Lerp(0.18f, 1.35f, Mathf.Clamp01(windiness * 0.75f + storm * 0.25f));
            float gust = Mathf.Lerp(0.03f, 0.65f, Mathf.Clamp01(windiness * 0.45f + storm * 0.55f));
            float shadowStrength = cloud.skyShadowStrength;
            float terrainShadowStrength = cloud.terrainShadowStrength;
            float sceneDarkening = cloud.sceneBottomDarkening;

            Color normalizedTint = ClampColor(tint);
            Color skyCloudTint = Color.Lerp(new Color(1f, 1f, 1f, 1f), normalizedTint, 0.55f);
            skyCloudTint = Color.Lerp(skyCloudTint, new Color(0.76f, 0.80f, 0.88f, 1f), gloom * 0.35f + storm * 0.20f);

            Color sceneTint = Color.Lerp(new Color(0.96f, 0.98f, 1f, 1f), normalizedTint, 0.75f);
            sceneTint = Color.Lerp(sceneTint, new Color(0.74f, 0.79f, 0.87f, 1f), gloom * 0.45f + storm * 0.20f);

            Color shadowTint = Color.Lerp(new Color(0.72f, 0.77f, 0.85f, 1f), normalizedTint * 0.82f, 0.45f);
            shadowTint = Color.Lerp(shadowTint, new Color(0.50f, 0.56f, 0.65f, 1f), gloom * 0.40f + storm * 0.25f);

            return new ResolvedWeatherProfile
            {
                cloudPowerRange = RangeAround(Mathf.Lerp(4.7f, 0.9f, density), Mathf.Lerp(0.24f, 0.65f, storm), 0f, 5f),
                cloudAlphaRange = RangeAround(Mathf.Lerp(0.04f, 0.92f, coverage), Mathf.Lerp(0.05f, 0.12f, heaviness), 0f, 1f),
                windSpeedRange = new Vector2(Mathf.Max(0.05f, baseWind * 0.65f), Mathf.Max(baseWind * 0.65f + 0.02f, baseWind * 1.15f)),
                windDirectionRange = new Vector2(0f, directionalSpread),
                gustStrengthRange = new Vector2(gust * 0.35f, gust),
                skyCloudAlphaRange = RangeAround(Mathf.Lerp(0.03f, 0.92f, coverage), Mathf.Lerp(0.04f, 0.16f, softness01), 0f, 1f),
                skyCloudPowerRange = RangeAround(Mathf.Lerp(4.8f, 0.55f, density), Mathf.Lerp(0.18f, 0.70f, threat), 0f, 5f),
                cloudSoftness = softness,
                cloudCoverageBias = Mathf.Lerp(0.22f, -0.32f, density),
                cloudTurbulence = turbulence,
                cloudWarpStrength = warp,
                cloudTint = skyCloudTint,
                cloudShadowStrength = shadowStrength,
                cloudGreyStrength = Mathf.Clamp01(0.10f + gloom * 0.65f + heaviness * 0.20f),
                cloudSunLightStrength = Mathf.Clamp01(1.0f - gloom * 0.55f - heaviness * 0.20f - storm * 0.20f),
                sceneCloudAlphaRange = RangeAround(Mathf.Lerp(0.08f, 0.96f, coverage), Mathf.Lerp(0.04f, 0.10f, heaviness), 0f, 1f),
                sceneCloudPowerRange = RangeAround(Mathf.Lerp(3.05f, 0.80f, Mathf.Clamp01(density * 0.55f + heaviness * 0.45f)), Mathf.Lerp(0.12f, 0.42f, threat), 0f, 5f),
                sceneCloudSoftness = sceneSoftness,
                sceneCloudCoverageBias = Mathf.Lerp(0.15f, -0.24f, density),
                sceneCloudTurbulence = Mathf.Clamp01(turbulence + 0.06f),
                sceneCloudWarpStrength = Mathf.Clamp01(warp + 0.04f),
                sceneCloudTint = sceneTint,
                sceneCloudShadowTint = shadowTint,
                planeEdgeFade = Mathf.Lerp(0.05f, 0.22f, softness01),
                planeRadialFade = Mathf.Lerp(0.08f, 0.30f, softness01),
                planeHeightFade = Mathf.Lerp(0.06f, 0.20f, softness01),
                sceneCloudFresnelFade = Mathf.Lerp(0.8f, 2.8f, Mathf.Clamp01(softness01 * 0.6f + heaviness * 0.4f)),
                sceneCloudMacroRoundness = Mathf.Lerp(0.7f, 1.55f, softness01),
                sceneCloudDetailStrength = Mathf.Lerp(0.35f, 1.10f, Mathf.Clamp01(heaviness * 0.55f + storm * 0.45f)),
                sceneCloudDensityContrast = Mathf.Lerp(1.0f, 2.4f, Mathf.Clamp01(heaviness * 0.55f + gloom * 0.25f + storm * 0.20f)),
                sceneCloudErosionStrength = Mathf.Lerp(0.18f, 1.25f, storm),
                sceneCloudSecondLayerStrength = Mathf.Lerp(0.15f, 1.15f, Mathf.Clamp01(coverage * 0.45f + storm * 0.55f)),
                sceneCloudSilverLiningStrength = Mathf.Lerp(0.15f, 0.90f, Mathf.Clamp01((1f - gloom) * 0.7f + softness01 * 0.3f)),
                sceneCloudBottomDarkening = sceneDarkening,
                sceneCloudDriftMultiplier = Mathf.Lerp(0.7f, 1.75f, Mathf.Clamp01(windiness * 0.6f + storm * 0.4f)),
                terrainCloudShadowStrength = terrainShadowStrength,
                terrainCloudShadowScale = Mathf.Lerp(540f, 180f, coverage),
                terrainCloudShadowSoftness = Mathf.Lerp(0.34f, 0.08f, Mathf.Clamp01(heaviness * 0.55f + storm * 0.45f)),
                terrainCloudShadowBias = Mathf.Lerp(0.10f, -0.10f, density),
                terrainCloudShadowDriftMultiplier = Mathf.Lerp(0.75f, 1.85f, Mathf.Clamp01(windiness * 0.6f + storm * 0.4f)),
                terrainCloudShadowTint = Color.Lerp(shadowTint, normalizedTint * 0.85f, 0.2f),
                volumeDensity = Mathf.Lerp(0.18f, 1.35f, Mathf.Clamp01(density * 0.55f + heaviness * 0.25f + storm * 0.20f)),
                volumeAbsorption = Mathf.Lerp(0.45f, 2.2f, Mathf.Clamp01(heaviness * 0.40f + gloom * 0.35f + storm * 0.25f)),
                volumeLightingStrength = Mathf.Lerp(1.15f, 0.35f, Mathf.Clamp01(gloom * 0.55f + storm * 0.30f + heaviness * 0.15f)),
                volumeAlphaMultiplier = Mathf.Lerp(0.45f, 1.45f, Mathf.Clamp01(density * 0.60f + storm * 0.40f)),
                volumeClusterScale = Mathf.Lerp(260f, 90f, Mathf.Clamp01(coverage * 0.45f + storm * 0.55f)),
                volumeClusterDensity = Mathf.Lerp(0.30f, 1.35f, Mathf.Clamp01(density * 0.55f + storm * 0.45f)),
                volumeDetailStrength = Mathf.Lerp(0.35f, 1.20f, Mathf.Clamp01(storm * 0.55f + heaviness * 0.45f)),
                volumeEdgeFade = Mathf.Lerp(0.34f, 0.10f, Mathf.Clamp01(heaviness * 0.40f + storm * 0.60f)),
                fogStrength = fogStrength
            };
        }

        private struct CloudResponse
        {
            public float density;
            public float threat;
            public float turbulence;
            public float warp;
            public float fogStrength;
            public float skyShadowStrength;
            public float terrainShadowStrength;
            public float sceneBottomDarkening;
        }

        private static CloudResponse BuildUnifiedCloudResponse(
            float coverage,
            float heaviness,
            float storm,
            float fog,
            float gloom)
        {
            float density = Mathf.Clamp01(coverage);
            float threat = Mathf.Clamp01(storm);
            float cloudPresence = Mathf.Clamp01(coverage);

            return new CloudResponse
            {
                density = density,
                threat = threat,
                turbulence = Mathf.Clamp01(0.18f + storm * 0.72f),
                warp = Mathf.Clamp01(0.06f + storm * 0.48f),
                fogStrength = Mathf.Clamp01(fog),
                skyShadowStrength = Mathf.Clamp01(cloudPresence * (heaviness * 0.58f + gloom * 0.30f + storm * 0.12f)),
                terrainShadowStrength = Mathf.Clamp01(cloudPresence * (heaviness * 0.50f + gloom * 0.32f + storm * 0.18f)),
                sceneBottomDarkening = Mathf.Clamp(cloudPresence * (0.35f + heaviness * 0.80f + gloom * 0.55f + storm * 0.30f), 0f, 2f)
            };
        }

        private static void InferSimpleControlsFromAdvanced(WeatherController.WeatherData preset)
        {
            float skyCoverage = 1f - Mathf.InverseLerp(0.5f, 4.5f, Midpoint(preset.skyCloudPowerRange));
            float skyAlpha = Mathf.InverseLerp(0f, 1f, Midpoint(preset.skyCloudAlphaRange));
            float sceneCoverage = 1f - Mathf.InverseLerp(0.8f, 3f, Midpoint(preset.sceneCloudPowerRange));
            float sceneAlpha = Mathf.InverseLerp(0f, 1f, Midpoint(preset.sceneCloudAlphaRange));

            preset.simpleCloudCoverage = Mathf.Clamp01((skyCoverage + skyAlpha + sceneCoverage + sceneAlpha) * 0.25f);
            preset.simpleCloudHeaviness = Mathf.Clamp01((preset.sceneCloudBottomDarkening / 2f) * 0.35f + preset.cloudShadowStrength * 0.35f + preset.terrainCloudShadowStrength * 0.30f);
            preset.simpleCloudSoftness = Mathf.Clamp01(Mathf.InverseLerp(0.04f, 0.34f, (preset.cloudSoftness + preset.sceneCloudSoftness) * 0.5f));
            preset.simpleStorminess = Mathf.Clamp01(preset.cloudTurbulence * 0.35f + preset.sceneCloudWarpStrength * 0.20f + Mathf.InverseLerp(0.03f, 0.65f, preset.gustStrengthRange.y) * 0.45f);
            preset.simpleFogginess = Mathf.Clamp01(preset.fogStrength);
            preset.simpleGloom = Mathf.Clamp01(preset.cloudGreyStrength * 0.45f + (1f - preset.cloudSunLightStrength) * 0.35f + preset.sceneCloudBottomDarkening * 0.20f);
            preset.simpleWindiness = Mathf.Clamp01(Mathf.InverseLerp(0.1f, 1.35f, preset.windSpeedRange.y));

            Color avgTint = (preset.cloudTint + preset.sceneCloudTint + preset.terrainCloudShadowTint) / 3f;
            preset.overallWeatherTint = ClampColor(new Color(
                Mathf.Max(0.7f, avgTint.r),
                Mathf.Max(0.7f, avgTint.g),
                Mathf.Max(0.75f, avgTint.b),
                1f));
        }

        private static Vector2 RangeAround(float center, float halfWidth)
        {
            return RangeAround(center, halfWidth, float.NegativeInfinity, float.PositiveInfinity);
        }

        private static Vector2 RangeAround(float center, float halfWidth, float minClamp, float maxClamp)
        {
            float min = center - halfWidth;
            float max = center + halfWidth;

            if (min > max)
            {
                float swap = min;
                min = max;
                max = swap;
            }

            min = Mathf.Clamp(min, minClamp, maxClamp);
            max = Mathf.Clamp(max, minClamp, maxClamp);

            if (min > max)
            {
                max = min;
            }

            return new Vector2(min, max);
        }

        private static float Midpoint(Vector2 range)
        {
            return (range.x + range.y) * 0.5f;
        }

        private static Color ClampColor(Color color)
        {
            return new Color(
                Mathf.Clamp(color.r, 0f, 1.15f),
                Mathf.Clamp(color.g, 0f, 1.15f),
                Mathf.Clamp(color.b, 0f, 1.15f),
                1f);
        }
    }
}
