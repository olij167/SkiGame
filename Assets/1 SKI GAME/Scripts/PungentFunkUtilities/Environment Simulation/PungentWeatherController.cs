using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [Serializable] public sealed class PungentWeatherStateEvent : UnityEvent<PungentWeatherState> { }
    [Serializable] public sealed class PungentForecastEvent : UnityEvent { }

    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Pungent Weather Controller")]
    public sealed class PungentWeatherController : MonoBehaviour
    {
        [Serializable]
        public sealed class HourlyWeather
        {
            [Range(0, 23)] public int hour;
            public PungentWeatherState state;
            public bool manualOverride;
        }

        [SerializeField] private PungentWeatherProfile weatherProfile;
        [SerializeField] private PungentClockController clock;
        [SerializeField] private bool regenerateForecastOnDayChange = true;
        [SerializeField] private bool preserveManualForecastOverridesOnRegenerate = true;
        [SerializeField] private bool useDeltaTimeController = true;
        [SerializeField] private PungentDeltaTimeController deltaTimeController;
        [SerializeField, Min(0f)] private float interpolationSpeed = 2f;
        [SerializeField] private PungentWeatherOutputApplier outputApplier;
        [SerializeField] private List<HourlyWeather> forecast = new List<HourlyWeather>(24);
        [SerializeField] private PungentWeatherState currentState;
        [SerializeField] private PungentWeatherStateEvent onWeatherChanged = new PungentWeatherStateEvent();
        [SerializeField] private PungentForecastEvent onForecastRegenerated = new PungentForecastEvent();

        public event Action<PungentWeatherState> WeatherChanged;
        public event Action ForecastRegenerated;

        public PungentWeatherProfile WeatherProfile => weatherProfile;
        public IReadOnlyList<HourlyWeather> Forecast => forecast;
        public PungentWeatherState CurrentState => currentState;
        public int ManualOverrideCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < forecast.Count; i++)
                {
                    if (forecast[i] != null && forecast[i].manualOverride)
                        count++;
                }

                return count;
            }
        }
        public PungentWeatherStateEvent OnWeatherChanged => onWeatherChanged;
        public PungentForecastEvent OnForecastRegenerated => onForecastRegenerated;

        private void OnEnable()
        {
            if (clock == null)
                clock = FindObjectOfType<PungentClockController>();

            if (clock != null)
            {
                clock.HourChanged += HandleClockHourChanged;
                clock.DayChanged += HandleClockDayChanged;
            }

            if (forecast.Count == 0)
                GenerateDailyForecast();
        }

        private void OnDisable()
        {
            if (clock != null)
            {
                clock.HourChanged -= HandleClockHourChanged;
                clock.DayChanged -= HandleClockDayChanged;
            }
        }

        private void Update()
        {
            if (forecast.Count == 0)
                return;

            float scale = 1f;
            if (useDeltaTimeController)
            {
                if (deltaTimeController == null)
                    deltaTimeController = PungentDeltaTimeController.Instance;
                if (deltaTimeController != null)
                    scale = deltaTimeController.GetScale(PungentTimeChannel.Weather);
            }

            int hour = clock != null ? clock.Snapshot.hour : DateTime.Now.Hour;
            int nextHour = (hour + 1) % 24;
            float t = clock != null ? Mathf.Clamp01(clock.Snapshot.minute / 60f + clock.Snapshot.second / 3600f) : 0f;
            PungentWeatherState target = LerpState(GetForecastState(hour), GetForecastState(nextHour), t);
            currentState = LerpState(currentState, target, Mathf.Clamp01(Time.unscaledDeltaTime * scale * interpolationSpeed));
            ApplyOutputs();
        }

        public void GenerateDailyForecast()
        {
            Dictionary<int, HourlyWeather> manualOverrides = preserveManualForecastOverridesOnRegenerate
                ? CaptureManualOverrides()
                : null;
            forecast.Clear();
            string season = clock != null ? clock.GetCurrentSeason() : "Default";
            PungentWeatherProfile.SeasonWeatherRule rule = weatherProfile != null ? weatherProfile.FindSeasonRule(season) : null;
            int daySeed = (weatherProfile != null ? weatherProfile.Seed : 1024) + (clock != null ? clock.Snapshot.date.year * 397 + clock.Snapshot.date.dayOfYear : 0);
            System.Random rng = new System.Random(daySeed);

            for (int hour = 0; hour < 24; hour++)
            {
                float temp = rule != null
                    ? Mathf.Lerp(rule.temperatureRange.x, rule.temperatureRange.y, DailyTemperatureCurve(hour, rng))
                    : Mathf.Lerp(-2f, 18f, DailyTemperatureCurve(hour, rng));
                float rainChance = rule != null
                    ? Mathf.Lerp(rule.precipitationChanceRange.x, rule.precipitationChanceRange.y, (float)rng.NextDouble())
                    : Mathf.Lerp(0f, 60f, (float)rng.NextDouble());
                PungentWeatherPreset preset = PickPreset(season, temp, rainChance, rng);
                if (manualOverrides != null && manualOverrides.TryGetValue(hour, out HourlyWeather manualOverride))
                {
                    forecast.Add(new HourlyWeather
                    {
                        hour = hour,
                        state = manualOverride.state,
                        manualOverride = true
                    });
                    continue;
                }

                forecast.Add(new HourlyWeather
                {
                    hour = hour,
                    state = preset != null ? preset.BuildState(daySeed + hour * 31, temp, rainChance, season) : new PungentWeatherState { displayName = "Default", temperature = temp, precipitationChance = rainChance }
                });
            }

            currentState = GetForecastState(clock != null ? clock.Snapshot.hour : 0);
            RaiseForecastRegenerated();
            RaiseWeatherChanged(currentState);
            ApplyOutputs();
        }

        public void ClearManualOverrides()
        {
            bool changed = false;
            for (int i = 0; i < forecast.Count; i++)
            {
                HourlyWeather entry = forecast[i];
                if (entry == null || !entry.manualOverride)
                    continue;

                entry.manualOverride = false;
                changed = true;
            }

            if (changed)
                RaiseForecastRegenerated();
        }

        public void SetHourlyWeather(int hour, PungentWeatherState state, bool manualOverride = true)
        {
            EnsureForecastSize();
            int index = Mathf.Clamp(hour, 0, 23);
            forecast[index].hour = index;
            forecast[index].state = state;
            forecast[index].manualOverride = manualOverride;
            if (clock != null && clock.Snapshot.hour == index)
            {
                currentState = state;
                RaiseWeatherChanged(currentState);
                ApplyOutputs();
            }
        }

        public void PreviewPreset(int presetIndex)
        {
            if (weatherProfile == null || weatherProfile.Presets == null || weatherProfile.Presets.Length == 0)
                return;

            presetIndex = Mathf.Clamp(presetIndex, 0, weatherProfile.Presets.Length - 1);
            PungentWeatherPreset preset = weatherProfile.Presets[presetIndex];
            currentState = preset.BuildState(weatherProfile.Seed + presetIndex, Mathf.Lerp(preset.temperatureRange.x, preset.temperatureRange.y, 0.5f), Mathf.Lerp(preset.precipitationChanceRange.x, preset.precipitationChanceRange.y, 0.5f), clock != null ? clock.GetCurrentSeason() : string.Empty);
            RaiseWeatherChanged(currentState);
            ApplyOutputs();
        }

        private void HandleClockHourChanged(PungentClockSnapshot snapshot)
        {
            PungentWeatherState state = GetForecastState(snapshot.hour);
            if (!string.Equals(currentState.presetId, state.presetId, StringComparison.OrdinalIgnoreCase))
                RaiseWeatherChanged(state);
        }

        private void HandleClockDayChanged(PungentClockSnapshot snapshot)
        {
            if (regenerateForecastOnDayChange)
                GenerateDailyForecast();
        }

        private PungentWeatherPreset PickPreset(string season, float temperature, float precipitationChance, System.Random rng)
        {
            if (weatherProfile == null || weatherProfile.Presets == null || weatherProfile.Presets.Length == 0)
                return null;

            List<PungentWeatherPreset> matches = new List<PungentWeatherPreset>();
            for (int i = 0; i < weatherProfile.Presets.Length; i++)
            {
                PungentWeatherPreset preset = weatherProfile.Presets[i];
                if (preset == null)
                    continue;

                bool seasonMatches = string.IsNullOrWhiteSpace(preset.season) || string.Equals(preset.season, season, StringComparison.OrdinalIgnoreCase);
                if (!seasonMatches)
                    continue;
                if (temperature < preset.temperatureRange.x || temperature > preset.temperatureRange.y)
                    continue;
                if (precipitationChance < preset.precipitationChanceRange.x || precipitationChance > preset.precipitationChanceRange.y)
                    continue;
                matches.Add(preset);
            }

            if (matches.Count == 0)
                return weatherProfile.Presets[0];

            return matches[rng.Next(0, matches.Count)];
        }

        private PungentWeatherState GetForecastState(int hour)
        {
            EnsureForecastSize();
            return forecast[Mathf.Clamp(hour, 0, forecast.Count - 1)].state;
        }

        private void EnsureForecastSize()
        {
            while (forecast.Count < 24)
                forecast.Add(new HourlyWeather { hour = forecast.Count });
        }

        private Dictionary<int, HourlyWeather> CaptureManualOverrides()
        {
            Dictionary<int, HourlyWeather> overrides = new Dictionary<int, HourlyWeather>();
            for (int i = 0; i < forecast.Count; i++)
            {
                HourlyWeather entry = forecast[i];
                if (entry == null || !entry.manualOverride)
                    continue;

                int hour = Mathf.Clamp(entry.hour, 0, 23);
                overrides[hour] = entry;
            }

            return overrides;
        }

        private static float DailyTemperatureCurve(int hour, System.Random rng)
        {
            float dayWarmth = Mathf.InverseLerp(5f, 15f, hour <= 15 ? hour : 30 - hour);
            return Mathf.Clamp01(dayWarmth * 0.8f + (float)rng.NextDouble() * 0.2f);
        }

        private static PungentWeatherState LerpState(PungentWeatherState a, PungentWeatherState b, float t)
        {
            t = Mathf.Clamp01(t);
            return new PungentWeatherState
            {
                presetId = t < 0.5f ? a.presetId : b.presetId,
                displayName = t < 0.5f ? a.displayName : b.displayName,
                glyph = t < 0.5f ? a.glyph : b.glyph,
                temperature = Mathf.Lerp(a.temperature, b.temperature, t),
                precipitationChance = Mathf.Lerp(a.precipitationChance, b.precipitationChance, t),
                isPrecipitating = t < 0.5f ? a.isPrecipitating : b.isPrecipitating,
                cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t),
                cloudDensity = Mathf.Lerp(a.cloudDensity, b.cloudDensity, t),
                cloudSoftness = Mathf.Lerp(a.cloudSoftness, b.cloudSoftness, t),
                storminess = Mathf.Lerp(a.storminess, b.storminess, t),
                fogStrength = Mathf.Lerp(a.fogStrength, b.fogStrength, t),
                gloom = Mathf.Lerp(a.gloom, b.gloom, t),
                windSpeed = Mathf.Lerp(a.windSpeed, b.windSpeed, t),
                windDirectionDegrees = Mathf.LerpAngle(a.windDirectionDegrees, b.windDirectionDegrees, t),
                gustStrength = Mathf.Lerp(a.gustStrength, b.gustStrength, t),
                wetness = Mathf.Lerp(a.wetness, b.wetness, t),
                snowiness = Mathf.Lerp(a.snowiness, b.snowiness, t),
                atmosphereTint = Color.Lerp(a.atmosphereTint, b.atmosphereTint, t),
                cloudTint = Color.Lerp(a.cloudTint, b.cloudTint, t),
                cloudShadowTint = Color.Lerp(a.cloudShadowTint, b.cloudShadowTint, t),
                skyCloudAlpha = Mathf.Lerp(a.skyCloudAlpha, b.skyCloudAlpha, t),
                skyCloudPower = Mathf.Lerp(a.skyCloudPower, b.skyCloudPower, t),
                skyCloudSoftness = Mathf.Lerp(a.skyCloudSoftness, b.skyCloudSoftness, t),
                cloudCoverageBias = Mathf.Lerp(a.cloudCoverageBias, b.cloudCoverageBias, t),
                cloudTurbulence = Mathf.Lerp(a.cloudTurbulence, b.cloudTurbulence, t),
                cloudWarpStrength = Mathf.Lerp(a.cloudWarpStrength, b.cloudWarpStrength, t),
                terrainCloudShadowStrength = Mathf.Lerp(a.terrainCloudShadowStrength, b.terrainCloudShadowStrength, t),
                volumeDensity = Mathf.Lerp(a.volumeDensity, b.volumeDensity, t)
            };
        }

        private void ApplyOutputs()
        {
            if (outputApplier != null)
                outputApplier.Apply(currentState);
        }

        private void RaiseWeatherChanged(PungentWeatherState state)
        {
            WeatherChanged?.Invoke(state);
            onWeatherChanged.Invoke(state);
        }

        private void RaiseForecastRegenerated()
        {
            ForecastRegenerated?.Invoke();
            onForecastRegenerated.Invoke();
        }
    }
}
