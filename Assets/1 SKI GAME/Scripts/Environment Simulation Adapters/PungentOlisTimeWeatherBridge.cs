using PungentFunk.Utilities.EnvironmentSimulation;
using TimeWeather;
using UnityEngine;

/// <summary>
/// SkiGame-only compatibility bridge for scenes that still contain the Olis TimeWeather controllers.
/// Keep this outside PungentFunk packages so the generic utilities never reference TimeWeather.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SkiGame/Environment Simulation/Pungent Olis Time Weather Bridge")]
public sealed class PungentOlisTimeWeatherBridge : MonoBehaviour
{
    [SerializeField] private TimeController oldTimeController;
    [SerializeField] private WeatherController oldWeatherController;
    [SerializeField] private PungentClockController pungentClock;
    [SerializeField] private PungentWeatherController pungentWeather;
    [SerializeField] private bool syncClockOnStart = true;
    [SerializeField] private bool syncWeatherOnStart = true;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        if (syncClockOnStart)
            SyncClockFromOlis();
        if (syncWeatherOnStart)
            SyncWeatherFromOlis();
    }

    [ContextMenu("Sync Clock From Olis")]
    public void SyncClockFromOlis()
    {
        ResolveReferences();
        if (oldTimeController == null || pungentClock == null)
            return;

        int monthIndex = Mathf.Max(0, oldTimeController.currentMonthIndex);
        pungentClock.SetDateTime(oldTimeController.currentYear, monthIndex, oldTimeController.dayOfMonth, oldTimeController.timeOfDay);
    }

    [ContextMenu("Sync Weather From Olis")]
    public void SyncWeatherFromOlis()
    {
        ResolveReferences();
        if (oldWeatherController == null || pungentWeather == null)
            return;

        WeatherController.WeatherData preset = oldWeatherController.currentWeatherPreset;
        PungentWeatherState state = new PungentWeatherState
        {
            presetId = preset != null ? preset.weatherCondition : "olis-weather",
            displayName = preset != null ? preset.weatherCondition : "Olis Weather",
            glyph = preset != null ? preset.weatherGlyph : string.Empty,
            temperature = oldWeatherController.temperature,
            precipitationChance = oldWeatherController.rainChance,
            isPrecipitating = preset != null && preset.isRaining,
            cloudCoverage = preset != null ? preset.simpleCloudCoverage : 0f,
            cloudDensity = preset != null ? preset.simpleCloudHeaviness : 0f,
            cloudSoftness = preset != null ? preset.simpleCloudSoftness : 0f,
            storminess = preset != null ? preset.simpleStorminess : 0f,
            fogStrength = preset != null ? preset.fogStrength : 0f,
            gloom = preset != null ? preset.simpleGloom : 0f,
            windSpeed = oldWeatherController.windSpeed,
            windDirectionDegrees = Mathf.Atan2(oldWeatherController.wind.y, oldWeatherController.wind.x) * Mathf.Rad2Deg,
            gustStrength = preset != null ? preset.simpleStorminess : 0f,
            wetness = oldWeatherController.wetness,
            snowiness = oldWeatherController.snowiness,
            atmosphereTint = preset != null ? preset.overallWeatherTint : Color.white,
            cloudTint = preset != null ? preset.cloudTint : Color.white,
            cloudShadowTint = preset != null ? preset.sceneCloudShadowTint : Color.white
        };

        int hour = pungentClock != null ? pungentClock.Snapshot.hour : Mathf.Clamp(oldTimeController != null ? oldTimeController.timeHours : 0, 0, 23);
        pungentWeather.SetHourlyWeather(hour, state, true);
    }

    private void ResolveReferences()
    {
        if (oldTimeController == null)
            oldTimeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
        if (oldWeatherController == null)
            oldWeatherController = WeatherController.instance != null ? WeatherController.instance : FindObjectOfType<WeatherController>();
        if (pungentClock == null)
            pungentClock = FindObjectOfType<PungentClockController>();
        if (pungentWeather == null)
            pungentWeather = FindObjectOfType<PungentWeatherController>();
    }
}
