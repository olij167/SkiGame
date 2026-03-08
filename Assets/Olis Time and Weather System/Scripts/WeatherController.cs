using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
//using UnityEditor;
namespace TimeWeather
{
#if UNITY_EDITOR
    [RequireComponent(typeof(WeatherDisplay))]
#endif
    public class WeatherController : MonoBehaviour
    {
        public static WeatherController instance;
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        [System.Serializable]
        public class WeatherData
        {
            [Tooltip("The name of the weather condition used for identification. \n Ensure each condition has a unique name to avoid errors")]
            public string weatherCondition;
            [Header("Requirements for Weather Selection")]
            [Tooltip("The temperature range required for this condition. \n This can be used to differentiate between weather conditions")]
            public Vector2 tempRange;
            [Tooltip("The chance of rain range required for this condition. This can be used to differentiate between weather conditions")]
            public Vector2 rainRange;
            [Tooltip("Whether this condition requires it to be raining \n This can be used to differentiate between weather conditions")]
            public bool isRaining;
            [Space(10)]
            [Header("Weather Condition Effects")]
            [Tooltip("The cloud planes which are active during this weather condition")]
            public List<Renderer> activeClouds;
            //[Tooltip("The strength range of the clouds when this condition is active \n 0 = full cloud coverage, 5 = no clouds")]
            public Vector2 cloudPowerRange;
            public Vector2 cloudAlphaRange;

            [Tooltip("The strength of the fog when this condition is active")]
            [Range(0f, 1f)] public float fogStrength;
            //[Tooltip("The colour of the fog when this condition is active, depending on the time of day")]
            //public Gradient fogColour;
            [Tooltip("The level of surface wetness for materials with the 'Wet' or 'WetAndSnowy' shader during this condition")]
            public float wetness;
            [Tooltip("The level of surface snowiness for materials with the 'Snowy' or 'WetAndSnowy' shader during this condition")]
            public float snowiness;
            [Tooltip("The particles which are active during this condition")]
            public WeatherParticles[] weatherParticles;
            [Tooltip("an array of ambient audio clips to play during this condition, picked randomly")]
            public AudioClip[] clips;
            [Tooltip("The pitch to play the clip at")]
            public float pitch;
            [Tooltip("The volume to play the clip at")]
            public float volume;
        }

        [System.Serializable]
        public class WeatherParticles
        {
            [Tooltip("The particle system to play")]
            public ParticleSystem particleSystem;
            public ParticleSystem.EmissionModule particleEmission;
            [Tooltip("The number of particles to spawn - the particle system's emission rate")]
            [Range(0f, 5000f)] public float particleAmount;
            public ParticleSystem.NoiseModule particleNoise;
            [Tooltip("the strength of noise applied to the particles direction - the particle system's noise strength")]
            [Range(0f, 10f)] public float noiseStrength;
        }

        [HideInInspector] public TimeController timeController;

        [Header("Current Weather")]
        #region
        [Tooltip("The weather preset that is currently selected")]
        [field: ReadOnlyField] public WeatherData currentWeatherPreset;
        private string weatherCondition;
        [field: ReadOnlyField] public float rainRandomiser;

        [Tooltip("The current temperature")]
        public float temperature;
        [Tooltip("The current chance of rain")]
        [Range(0f, 100f)] public float rainChance;

        //[Tooltip("The current cloud power \n 0 = full cloud coverage, 5 = clear sky")]
        //[Range(0f, 5f)] public float cloudPower;
        //public float cloudAlpha;
        [Tooltip("The speed which fog changes based on the current weather preset")]
        [Range(0f, 1f)] public float fogSpeed;
        private float desiredWindSpeed;
        [Space(5f)]

        [Tooltip("The current surface wetness. \n Only applied to materials with the 'Wet' or 'WetAndSnowy' shader")]
        [Range(0f, 5f)] public float wetness;
        [SerializeField, Range(0f, 5f)] private float desiredWetness;
        [Tooltip("The current surface snowiness. \n Only applied to materials with the 'Snowy' or 'WetAndSnowy' shader")]
        [Range(0f, 1f)] public float snowiness;
        [SerializeField, Range(0f, 1f)] private float desiredSnowiness;
        [Space(5f)]

        [Header("Surface Condition Variables")]
        [Tooltip("The speed which surface wetness increases based on the current weather preset")]
        [Range(0f, 1f)] public float wetnessSpeed;
        [Tooltip("The speed which surface wetness decreases based on the current weather preset")]
        [Range(0f, 1f)] public float evaporationSpeed;
        [Space(5f)]

        [Tooltip("The speed which surface snowiness increases based on the current weather preset")]
        [Range(0f, 1f)] public float snowCoverSpeed;
        [Tooltip("The speed which surface snowiness decreases based on the current weather preset")]
        [Range(0f, 1f)] public float meltingSpeed;
        #endregion
        //[Space(10)]
        [Tooltip("The season that is currently selected")]
        [ReadOnlyField] public SeasonConditions currentSeasonConditions;

        [Header("Hourly Forcast")]
        [Tooltip("The hourly forcast for the day. It is generated at midnight and on start based on the currentSeasonConditions")]
        [field: ReadOnlyField] public List<HourlyWeather> hourlyWeather;

        [Header("Current Wind")]
        [Tooltip("controls the speed of the clouds")]
        public float windMultiplier = 1e-08f; // you should keep this low
        [Tooltip("The current wind direction controlling the clouds movement")]
        [field: ReadOnlyField] public Vector2 wind = new Vector2();
        [Tooltip("The speed of the wind")]
        [field: ReadOnlyField] public float windSpeed;
        float desiredAlpha;
        float desiredPower;
        float rand;

        [Header("Weather Presets")]
        [Tooltip("The list of seasons and their conditions. \n At least 1 season is required to function")]
        public List<SeasonConditions> seasonConditions;

        [Tooltip("An array of all potential weather conditions, their requirements and their effects")]
        public WeatherData[] weatherDataPresets;
        [Tooltip("The renderer attached to the cloud plane")]
        public HourlyClouds[] cloudRenderer;
        [Tooltip("A CrossFadeAudio component, \n this plays weather audio clips and allows for smoother audio transitions between clips")]
        public CrossFadeAudio weatherAudio;

        public ParticleSystem.EmissionModule particleEmission;
        public ParticleSystem.NoiseModule particleNoise;


        [Header("UI")]
        [Tooltip("Toggle whether to show the temperature UI")]
        public bool toggleTempUI;
        [Tooltip("The TextMeshPro element to display the current temperature")]
        public TextMeshProUGUI tempText;
        [Tooltip("Toggle whether to show the rain chance UI")]
        public bool toggleRainUI;
        [Tooltip("The TextMeshPro element to display the current chance of rain")]
        public TextMeshProUGUI rainText;
        [Tooltip("Toggle whether to show the weather condition UI")]
        public bool toggleWeatherUI;
        [Tooltip("The TextMeshPro element to display the current weather condition")]
        public TextMeshProUGUI weatherConditionText;

        public bool showDebugLogs;

        private static readonly int ID_CloudAlpha = Shader.PropertyToID("_CloudAlpha");
        private static readonly int ID_CloudPower = Shader.PropertyToID("_CloudPower");
        private static readonly int ID_CloudSpeed = Shader.PropertyToID("_CloudSpeed");
        static readonly int ID_CloudSeed = Shader.PropertyToID("_CloudSeed");

        private Vector2 _lastCloudSpeed;

        // --- CPU/GC optimisations ---
        // Avoid per-frame List.Contains checks on active cloud renderers.
        private readonly HashSet<Renderer> _activeCloudSet = new HashSet<Renderer>();

        // Avoid per-frame O(n) scans to find the preset by name.
        private Dictionary<string, WeatherData> _presetByName;

        // Stop/start particles only when the preset changes (avoid per-frame allocations).
        private WeatherData _lastAppliedPreset;
        private readonly HashSet<ParticleSystem> _presetParticleSet = new HashSet<ParticleSystem>();


        private void OnEnable()
        {
            timeController = TimeController.instance;
        }


        private void Start()
        {
            timeController = TimeController.instance;

            SetSeasonalConditions();
            SetDailyConditions();

            // Build preset lookup once (CPU optimisation)
            if (_presetByName == null)
                _presetByName = new Dictionary<string, WeatherData>(System.StringComparer.Ordinal);
            else
                _presetByName.Clear();

            if (weatherDataPresets != null)
            {
                for (int i = 0; i < weatherDataPresets.Length; i++)
                {
                    var p = weatherDataPresets[i];
                    if (p == null) continue;
                    if (string.IsNullOrEmpty(p.weatherCondition)) continue;

                    if (_presetByName.ContainsKey(p.weatherCondition))
                    {
                        Debug.LogWarning($"Duplicate weatherCondition name '{p.weatherCondition}' found in weatherDataPresets. Names must be unique.");
                        continue;
                    }
                    _presetByName.Add(p.weatherCondition, p);
                }
            }

            // If a preset is assigned in the inspector, prime the active-cloud set now
            // so cloud planes don’t all fade out on the very first update.
            if (currentWeatherPreset != null)
            {
                _lastAppliedPreset = currentWeatherPreset;
                _activeCloudSet.Clear();
                if (currentWeatherPreset.activeClouds != null)
                {
                    for (int a = 0; a < currentWeatherPreset.activeClouds.Count; a++)
                    {
                        var r = currentWeatherPreset.activeClouds[a];
                        if (r != null) _activeCloudSet.Add(r);
                    }
                }
            }

            foreach (WeatherData data in weatherDataPresets)
            {
                for (int w = 0; w < data.weatherParticles.Length; w++)
                {
                    if (data.weatherParticles[w].particleSystem != null && data.weatherParticles[w].particleSystem.isPlaying)
                    {
                        data.weatherParticles[w].particleSystem.Stop();
                    }
                }
            }

            rainChance = hourlyWeather[timeController.timeHours].rainChance;
            wetness = currentWeatherPreset.wetness;
            snowiness = currentWeatherPreset.snowiness;

            ToggleUI();

            for (int i = 0; i < cloudRenderer.Length; i++)
            {
                if (cloudRenderer[i].cloudRenderer == null) continue;

                if (cloudRenderer[i].mpb == null)
                    cloudRenderer[i].mpb = new MaterialPropertyBlock();

                // Use sharedMaterial to avoid forcing a unique material instance
                var mat = cloudRenderer[i].cloudRenderer.sharedMaterial;
                if (mat != null)
                    cloudRenderer[i].baseAlpha = mat.GetFloat(ID_CloudAlpha);
                else
                    cloudRenderer[i].baseAlpha = 1f;

                // Seed the property block from current renderer state
                cloudRenderer[i].cloudRenderer.GetPropertyBlock(cloudRenderer[i].mpb);
                cloudRenderer[i].mpb.SetFloat(ID_CloudAlpha, cloudRenderer[i].baseAlpha);
                cloudRenderer[i].mpb.SetFloat(ID_CloudPower, cloudRenderer[i].cloudPower);
                cloudRenderer[i].cloudRenderer.SetPropertyBlock(cloudRenderer[i].mpb);
            }

        }

        public void ToggleUI()
        {
            if (!toggleTempUI)
            {
                if (tempText != null) tempText.gameObject.SetActive(false);
            }
            else if (tempText != null) tempText.gameObject.SetActive(true);

            if (!toggleRainUI)
            {
                if (rainText != null) rainText.gameObject.SetActive(false);
            }
            else if (rainText != null) rainText.gameObject.SetActive(true);

            if (!toggleWeatherUI)
            {
                if (weatherConditionText != null) weatherConditionText.gameObject.SetActive(false);
            }
            else if (weatherConditionText != null) weatherConditionText.gameObject.SetActive(true);
        }

        public void SetSeasonalConditions()
        {
            for (int i = 0; i < seasonConditions.Count; i++)
            {
                if (timeController.currentMonthData.season == seasonConditions[i].season)
                {
                    currentSeasonConditions = seasonConditions[i];
                    timeController.skyData = seasonConditions[i].seasonalSkyData;
                    if (showDebugLogs) Debug.Log("It is now " + seasonConditions[i].season);
                    return;
                }
            }
        }

        public void SetDailyConditions()
        {
            // Ensure list exists and reset it deterministically each day
            if (hourlyWeather == null)
                hourlyWeather = new List<HourlyWeather>(24);

            HourlyWeather midnightCondition = null;

            // If we had a previous day, carry over the last hour as "midnight" seed (optional)
            if (hourlyWeather.Count >= 24)
            {
                midnightCondition = hourlyWeather[23];
                midnightCondition.isMidnight = true;
            }

            hourlyWeather.Clear();

            float highTemp = Random.Range(currentSeasonConditions.tempRange.x + Mathf.Abs(currentSeasonConditions.tempRange.x * 0.5f), currentSeasonConditions.tempRange.y);
            float lowTemp = Random.Range(currentSeasonConditions.tempRange.x, currentSeasonConditions.tempRange.y - Mathf.Abs(currentSeasonConditions.tempRange.y * 0.5f));

            int hottestTime = Random.Range((int)currentSeasonConditions.hottestTimeRange.x, (int)currentSeasonConditions.hottestTimeRange.y);
            int coldestMorningTime = Random.Range((int)currentSeasonConditions.coldestMorningTimeRange.x, (int)currentSeasonConditions.coldestMorningTimeRange.y);
            int coldestNightTime = Random.Range((int)currentSeasonConditions.coldestNightTimeRange.x, (int)currentSeasonConditions.coldestNightTimeRange.y);

            timeController.sunriseTime = Random.Range(currentSeasonConditions.sunriseTimeRange.x, currentSeasonConditions.sunriseTimeRange.y);
            timeController.sunsetTime = Random.Range(currentSeasonConditions.sunsetTimeRange.x, currentSeasonConditions.sunsetTimeRange.y);

            timeController.sunriseTimePercent = (timeController.sunriseTime %= 24) / 24f;
            timeController.sunsetTimePercent = (timeController.sunsetTime %= 24) / 24f;

            int sunriseHour = (int)timeController.sunriseTime;
            float sunriseTimeClamped = Mathf.Clamp((timeController.sunriseTime - sunriseHour) * 60, 0f, 59.49f);

              int sunsetHour = (int)timeController.sunsetTime;
            float sunsetTimeClamped = Mathf.Clamp((timeController.sunsetTime - sunsetHour) * 60, 0f, 59.49f);

            if (timeController.toggleSunTimeUI)
            {
                timeController.sunriseText.text = sunriseHour.ToString("00") + ":" + sunriseTimeClamped.ToString("00") + " AM \n Sunrise";
                timeController.sunsetText.text = sunsetHour.ToString("00") + ":" + sunsetTimeClamped.ToString("00") + " PM \n Sunset";
            }

            float lowestRainChance = Random.Range((int)currentSeasonConditions.rainRange.x, (int)currentSeasonConditions.rainRange.y);
            float highestRainChance = Random.Range((int)lowestRainChance, (int)currentSeasonConditions.rainRange.y);
            int heighestRainTime = Random.Range(0, 23);

            if (showDebugLogs)
            {
                Debug.Log("Highest rain chance = " + highestRainChance + ", lowest rain chance = " + lowestRainChance);
                Debug.Log("Highest temp = " + highTemp + ", lowest temp = " + lowTemp);
            }

            hourlyWeather = new List<HourlyWeather>(24);

            for (int i = 0; i < 24; i++)
            {
                HourlyWeather hourly = new HourlyWeather();

                if (midnightCondition != null && midnightCondition.isMidnight && i == 0)
                {
                    hourly.forcastTime = i;
                    hourly.temp = midnightCondition.temp;
                    hourly.rainChance = midnightCondition.rainChance;
                    hourly.weatherCondition = midnightCondition.weatherCondition;
                    hourly.cloudPower = midnightCondition.cloudPower;


                    //hourly.windSpeed = midnightCondition.windSpeed;
                    hourly.isRaining = midnightCondition.isRaining;
                    hourly.isMidnight = false;

                }
                else
                {
                    hourly.forcastTime = i;

                    if (i == coldestMorningTime) //set the temp for the coldest time in the morning
                    {
                        hourly.temp = lowTemp;
                    }
                    else if (i == hottestTime) //set the temp for the hottest time of the day
                    {
                        hourly.temp = highTemp;
                    }
                    else if (i == coldestNightTime) //set the temp for the coldest time in the night
                    {
                        hourly.temp = lowTemp;
                    }
                    else // lerp unspecified temps between the highs and lows
                    {
                        if (i < coldestMorningTime)
                        {
                            // Night -> coldest morning (approach lowTemp)
                            // If we have a previous hour, lerp from it toward lowTemp; otherwise start from lowTemp
                            float prev = (i > 0) ? hourlyWeather[i - 1].temp : lowTemp;
                            float t = 1f / Mathf.Max(1, (coldestMorningTime - i));
                            hourly.temp = Mathf.Lerp(prev, lowTemp, t);
                        }
                        else if (i > coldestMorningTime && i < hottestTime)
                        {
                            // Cold morning -> hottest time (approach highTemp)
                            float prev = hourlyWeather[i - 1].temp;
                            float t = 1f / Mathf.Max(1, (hottestTime - i));
                            hourly.temp = Mathf.Lerp(prev, highTemp, t);
                        }
                        else if (i > hottestTime && i < coldestNightTime)
                        {
                            // Hottest -> coldest night (approach lowTemp)
                            float prev = hourlyWeather[i - 1].temp;
                            float t = 1f / Mathf.Max(1, (coldestNightTime - i));
                            hourly.temp = Mathf.Lerp(prev, lowTemp, t);
                        }
                        else if (i > coldestNightTime)
                        {
                            // Coldest night -> midnight (hold toward lowTemp)
                            float prev = hourlyWeather[i - 1].temp;
                            hourly.temp = Mathf.Lerp(prev, lowTemp, 0.5f);
                        }
                        else
                        {
                            // Exactly on key hours is handled above; this is a safe fallback
                            hourly.temp = hourlyWeather[i - 1].temp;
                        }
                    }

                    // Set chance of rain for each hour
                    if (i == heighestRainTime) //Set highest chance of rain
                    {
                        hourly.rainChance = highestRainChance;

                    }
                    else if (i < heighestRainTime) // lerp the rest based on the highest time
                    {
                        if (i > 0)
                        {
                            hourly.rainChance = Mathf.Lerp(hourlyWeather[i - 1].rainChance, highestRainChance, 1f / (heighestRainTime - hourly.forcastTime));
                        }
                        else
                        {
                            hourly.rainChance = Mathf.Lerp(rainChance, highestRainChance / 2, 1f / (heighestRainTime - hourly.forcastTime));
                        }
                    }
                    else if (i > heighestRainTime)
                    {
                        hourly.rainChance = Mathf.Lerp(rainChance, Random.Range(lowestRainChance, hourlyWeather[i - 1].rainChance), 1f / (hourly.forcastTime - heighestRainTime));
                    }

                    rainRandomiser = Random.Range(0, 100);

                    if (showDebugLogs) Debug.Log(hourly.forcastTime + "o'clock Rain Chance = " + rainRandomiser);

                    //Set Weather Conditions
                    if (rainRandomiser < hourly.rainChance) // check whether it is raining
                    {
                        hourly.isRaining = true;
                    }

                    //Check required temp and chance of rain rnages, and whether it is raining
                    //Select the most appropriate weather

                    int idx = ResolvePresetIndex(hourly.temp, hourly.rainChance, hourly.isRaining);
                    hourly.presetIndex = idx;

                    if (idx >= 0)
                    {
                        hourly.weatherCondition = weatherDataPresets[idx].weatherCondition;
                        hourly.cloudPower = Random.Range(weatherDataPresets[idx].cloudPowerRange.x, weatherDataPresets[idx].cloudPowerRange.y);

                        if (weatherDataPresets[idx].clips != null && weatherDataPresets[idx].clips.Length > 0)
                            hourly.weatherAudio = weatherDataPresets[idx].clips[Random.Range(0, weatherDataPresets[idx].clips.Length)];
                    }
                    else
                    {
                        // Fallback if no preset matches
                        hourly.weatherCondition = string.Empty;
                        hourly.cloudPower = 0f;
                    }

                }

                hourlyWeather.Add(hourly);
            }

            temperature = hourlyWeather[timeController.timeHours].temp;

            rainChance = hourlyWeather[timeController.timeHours].rainChance;

            weatherCondition = hourlyWeather[timeController.timeHours].weatherCondition;
        }

        public void SetHourlyVariables(int hour, float temperature, float rainChance, bool isRaining)
        {
            hourlyWeather[hour].temp = temperature;
            hourlyWeather[hour].rainChance = rainChance;
            hourlyWeather[hour].isRaining = isRaining;

            int idx = ResolvePresetIndex(hourlyWeather[hour].temp, hourlyWeather[hour].rainChance, hourlyWeather[hour].isRaining);
            hourlyWeather[hour].presetIndex = idx;

            if (idx >= 0)
            {
                hourlyWeather[hour].weatherCondition = weatherDataPresets[idx].weatherCondition;
                hourlyWeather[hour].cloudPower = Random.Range(weatherDataPresets[idx].cloudPowerRange.x, weatherDataPresets[idx].cloudPowerRange.y);

                if (weatherDataPresets[idx].clips != null && weatherDataPresets[idx].clips.Length > 0)
                    hourlyWeather[hour].weatherAudio = weatherDataPresets[idx].clips[Random.Range(0, weatherDataPresets[idx].clips.Length)];
            }
            else
            {
                hourlyWeather[hour].weatherCondition = string.Empty;
                hourlyWeather[hour].cloudPower = 0f;
            }

        }

        public void SetCurrentConditions()
        {
            if (hourlyWeather != null)
            {
                if (timeController.timeHours < 23)
                {
                    float hourlyTimePercent = timeController.hourlyTimePercent;

                    temperature = Mathf.Lerp(hourlyWeather[timeController.timeHours].temp, hourlyWeather[timeController.timeHours + 1].temp, hourlyTimePercent);

                    rainChance = Mathf.Lerp(hourlyWeather[timeController.timeHours].rainChance, hourlyWeather[timeController.timeHours + 1].rainChance, hourlyTimePercent);

                    if (toggleTempUI && tempText != null)
                        tempText.text = temperature.ToString("00") + "°C";

                    // Resolve the most appropriate preset index directly (no per-frame scan).
                    int resolvedIdx = ResolvePresetIndex(temperature, rainChance, hourlyWeather[timeController.timeHours].isRaining);
                    if (resolvedIdx >= 0)
                        weatherCondition = weatherDataPresets[resolvedIdx].weatherCondition;

                    if (toggleWeatherUI && weatherConditionText != null)
                        weatherConditionText.text = weatherCondition;

                    if (toggleRainUI && rainText != null)
                        rainText.text = rainChance.ToString("00") + "% Rain";

                    for (int r = 0; r < cloudRenderer.Length; r++)
                    {
                        var hc = cloudRenderer[r];
                        if (hc == null || hc.cloudRenderer == null) continue;

                        // Ensure MPB exists (it should be created in Start(), but keep this robust)
                        if (hc.mpb == null) hc.mpb = new MaterialPropertyBlock();

                        // Hot path: use a HashSet to avoid per-frame List.Contains.
                        bool isActive = (currentWeatherPreset != null && _activeCloudSet.Contains(hc.cloudRenderer));

                        // 1) Power + alpha targets
                        if (isActive)
                        {
                            // Drive power toward the next hour’s target power (smooth hour blending)
                            desiredPower = Mathf.Lerp(hc.cloudPower, hourlyWeather[timeController.timeHours + 1].cloudPower, hourlyTimePercent);
                            hc.cloudPower = Mathf.Lerp(hc.cloudPower, desiredPower, hourlyTimePercent);

                            // Drive alpha toward a stable target within the preset’s alpha range.
                            // IMPORTANT: do NOT call Random.Range every frame; use a stable noise-based target.
                            float noise = Mathf.PerlinNoise(r * 17.123f, timeController.timePercent * 2.0f);
                            float targetAlpha = Mathf.Lerp(currentWeatherPreset.cloudAlphaRange.x, currentWeatherPreset.cloudAlphaRange.y, noise);

                            // Blend from baseAlpha toward targetAlpha (and keep smoothing gentle)
                            desiredAlpha = Mathf.Lerp(hc.baseAlpha, targetAlpha, hourlyTimePercent);
                            hc.cloudAlpha = Mathf.Lerp(hc.cloudAlpha, desiredAlpha, hourlyTimePercent * 0.5f);
                        }
                        else
                        {
                            // Fade out inactive planes
                            hc.cloudAlpha = Mathf.Lerp(hc.cloudAlpha, 0f, hourlyTimePercent * 0.05f);
                            // Optionally also ease power toward clear-sky if your shader interprets higher as clearer
                            // hc.cloudPower = Mathf.Lerp(hc.cloudPower, 5f, hourlyTimePercent * 0.05f);
                        }

                        // 2) Wind smoothing (your existing logic, but we keep it inside the same loop)
                        if (desiredWindSpeed == rand)
                            rand = Random.Range(windSpeed - 0.05f, windSpeed + 0.05f);
                        else
                            desiredWindSpeed = Mathf.Lerp(windSpeed, rand, hourlyTimePercent);

                        windSpeed = Mathf.Lerp(windSpeed, desiredWindSpeed, hourlyTimePercent * 0.5f);

                        wind.x = Mathf.Lerp(wind.x, windSpeed * windMultiplier, hourlyTimePercent * 0.5f);
                        wind.y = Mathf.Lerp(wind.y, windSpeed * windMultiplier, hourlyTimePercent * 0.5f);

                        Vector2 cloudSpeed;
                        if (timeController.timeOfDay <= 12)
                            cloudSpeed = wind * timeController.timeScale * (timeController.timeOfDay * 0.5f) * (timeController.timePercent * 25f);
                        else
                            cloudSpeed = wind * timeController.timeScale * (23 - timeController.timeOfDay) * (timeController.timePercent * 25f);

                        _lastCloudSpeed = cloudSpeed;

                        // 3) Apply to shader via MPB (no material instancing)
                        hc.cloudRenderer.GetPropertyBlock(hc.mpb);
                        hc.mpb.SetFloat(ID_CloudAlpha, hc.cloudAlpha);
                        hc.mpb.SetFloat(ID_CloudPower, hc.cloudPower);
                        hc.mpb.SetVector(ID_CloudSpeed, new Vector4(cloudSpeed.x * 0.5f, cloudSpeed.y * 0.5f, 0f, 0f));
                        hc.cloudRenderer.SetPropertyBlock(hc.mpb);


                        // Write back (because HourlyClouds is a class, this is technically not required,
                        // but leaving this makes it explicit if you ever convert it to a struct)
                        cloudRenderer[r] = hc;
                    }

                    // --- Skybox clouds (drive Skybox.shader using same params) ---
                    var skyMat = RenderSettings.skybox;
                    if (skyMat != null)
                    {
                        if (skyMat.HasProperty(ID_CloudSeed))
                        {
                            // Stable-ish seed: changes with day + weather preset index (or hash)
                            int presetIndex = currentWeatherPreset != null ? System.Array.IndexOf(weatherDataPresets, currentWeatherPreset) : 0;
                            float seed = (timeController != null ? timeController.dayCount : 0) * 31.73f + presetIndex * 101.11f;
                            skyMat.SetFloat(ID_CloudSeed, seed);
                        }

                        float alphaSum = 0f, powerSum = 0f;
                        int activeCount = 0;

                        if (currentWeatherPreset != null && currentWeatherPreset.activeClouds != null)
                        {
                            for (int r = 0; r < cloudRenderer.Length; r++)
                            {
                                var hc = cloudRenderer[r];
                                if (hc == null || hc.cloudRenderer == null) continue;

                                if (_activeCloudSet.Contains(hc.cloudRenderer))
                                {
                                    alphaSum += hc.cloudAlpha;
                                    powerSum += hc.cloudPower;
                                    activeCount++;
                                }
                            }
                        }

                        // Average cloud params from the active 3D cloud renderers
                        float avgAlpha = (activeCount > 0) ? (alphaSum / activeCount) : 0f;
                        float avgPower = (activeCount > 0) ? (powerSum / activeCount) : 5f;

                        // IMPORTANT:
                        // Shader Graph clouds use CloudAlpha in ~0..15 (per your material inspector),
                        // but the skybox shader expects CloudAlpha in 0..1.
                        // Remap and clamp so we never feed skybox alpha > 1.
                        const float PLANE_ALPHA_MAX = 15f;
                        float skyCloudAlpha = Mathf.Clamp01(avgAlpha / PLANE_ALPHA_MAX);

                        // CloudPower is already intended to be 0..5 (0=overcast, 5=clear).
                        float skyCloudPower = Mathf.Clamp(avgPower, 0f, 5f);

                        skyMat.SetFloat(ID_CloudAlpha, skyCloudAlpha);
                        skyMat.SetFloat(ID_CloudPower, skyCloudPower);


                        // IMPORTANT: use the exact same speed you used for planes
                        Vector4 skySpeedV4 = new Vector4(_lastCloudSpeed.x * 0.5f, _lastCloudSpeed.y * 0.5f, 0f, 0f);

                        if (skyMat.HasProperty("_CloudAlpha")) skyMat.SetFloat("_CloudAlpha", skyCloudAlpha);
                        if (skyMat.HasProperty("_CloudPower")) skyMat.SetFloat("_CloudPower", skyCloudPower);
                        if (skyMat.HasProperty("_CloudSpeed")) skyMat.SetVector("_CloudSpeed", skySpeedV4);
                    }

                    // --- Feed fog into skybox so fog affects the sky material ---
                    if (skyMat != null)
                    {
                        // RenderSettings fog color is already time-of-day driven in TimeController.
                        // Weather drives density via fogStrength; we mirror that into the skybox shader.
                        if (skyMat.HasProperty("_FogColor"))
                            skyMat.SetColor("_FogColor", RenderSettings.fogColor);

                        if (skyMat.HasProperty("_FogDensity"))
                            skyMat.SetFloat("_FogDensity", RenderSettings.fogDensity);

                        // Optional: push a stronger “sky fog strength” when the preset is foggy/rainy
                        // (This does NOT change which preset is chosen; only improves perception.)
                        if (skyMat.HasProperty("_FogSkyStrength"))
                        {
                            float fogSkyStrength = Mathf.Lerp(2.0f, 6.0f, Mathf.Clamp01(currentWeatherPreset != null ? currentWeatherPreset.fogStrength : 0f));
                            skyMat.SetFloat("_FogSkyStrength", fogSkyStrength);
                        }

                        if (skyMat.HasProperty("_FogHorizonBoost"))
                        {
                            float boost = Mathf.Lerp(2.0f, 5.0f, Mathf.Clamp01(currentWeatherPreset != null ? currentWeatherPreset.fogStrength : 0f));
                            skyMat.SetFloat("_FogHorizonBoost", boost);
                        }
                    }

                    if (currentWeatherPreset.snowiness < snowiness)
                    {
                        desiredSnowiness = Mathf.Lerp(snowiness, currentWeatherPreset.snowiness, hourlyTimePercent * evaporationSpeed * (1 - (temperature / 100)));
                    }
                    else if (currentWeatherPreset.snowiness > snowiness)
                    {
                        desiredSnowiness = Mathf.Lerp(snowiness, currentWeatherPreset.snowiness, hourlyTimePercent * snowCoverSpeed);
                    }

                    if (snowiness != desiredSnowiness)
                        snowiness = Mathf.Lerp(snowiness, desiredSnowiness, hourlyTimePercent * 0.5f);

                    Shader.SetGlobalFloat("_SnowAmount", snowiness);

                    if (currentWeatherPreset.wetness < wetness)
                    {
                        desiredWetness = Mathf.Lerp(wetness, currentWeatherPreset.wetness, hourlyTimePercent * evaporationSpeed * (1 - (temperature / 100)));
                    }
                    else if (currentWeatherPreset.wetness > wetness)
                    {
                        desiredWetness = Mathf.Lerp(wetness, currentWeatherPreset.wetness, hourlyTimePercent * wetnessSpeed);
                    }

                    if (wetness != desiredWetness)
                        wetness = Mathf.Lerp(wetness, desiredWetness, hourlyTimePercent * 0.5f);

                    Shader.SetGlobalFloat("_Wetness", wetness);

                    // --- Apply preset (CPU/GC optimised; no per-frame full scans or List allocations) ---
                    WeatherData resolvedPreset = null;
                    if (!string.IsNullOrEmpty(weatherCondition) && _presetByName != null)
                        _presetByName.TryGetValue(weatherCondition, out resolvedPreset);

                    if (resolvedPreset != null)
                    {
                        // Detect preset change
                        if (_lastAppliedPreset != resolvedPreset)
                        {
                            currentWeatherPreset = resolvedPreset;
                            _lastAppliedPreset = resolvedPreset;

                            // Rebuild active cloud set once per preset change
                            _activeCloudSet.Clear();
                            if (currentWeatherPreset.activeClouds != null)
                            {
                                for (int a = 0; a < currentWeatherPreset.activeClouds.Count; a++)
                                {
                                    var r = currentWeatherPreset.activeClouds[a];
                                    if (r != null) _activeCloudSet.Add(r);
                                }
                            }

                            // Stop particles that are not part of the new preset (only on change)
                            _presetParticleSet.Clear();
                            if (currentWeatherPreset.weatherParticles != null)
                            {
                                for (int w = 0; w < currentWeatherPreset.weatherParticles.Length; w++)
                                {
                                    var ps = currentWeatherPreset.weatherParticles[w].particleSystem;
                                    if (ps != null) _presetParticleSet.Add(ps);
                                }
                            }

                            if (weatherDataPresets != null)
                            {
                                for (int p = 0; p < weatherDataPresets.Length; p++)
                                {
                                    var preset = weatherDataPresets[p];
                                    if (preset == null || preset.weatherParticles == null) continue;

                                    for (int w = 0; w < preset.weatherParticles.Length; w++)
                                    {
                                        var ps = preset.weatherParticles[w].particleSystem;
                                        if (ps != null && !_presetParticleSet.Contains(ps) && ps.isPlaying)
                                            ps.Stop();
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Keep pointer up to date even if unchanged
                            currentWeatherPreset = resolvedPreset;
                        }

                        // Fog + audio (every frame, but O(1))
                        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, currentWeatherPreset.fogStrength, hourlyTimePercent * fogSpeed);

                        // Weather audio can be moved during menu->game transition.
                        // If the serialized reference was destroyed, re-acquire safely.
                        if (weatherAudio == null)
                        {
                            weatherAudio = GetComponentInChildren<CrossFadeAudio>(true);
                            if (weatherAudio == null)
                                weatherAudio = FindObjectOfType<CrossFadeAudio>(true);
                        }

                        if (weatherAudio != null && currentWeatherPreset.clips != null && currentWeatherPreset.clips.Length > 0)
                            weatherAudio.newSoundtrack(hourlyWeather[timeController.timeHours].weatherAudio, currentWeatherPreset.volume);

                        // Particles (update emission/noise; ensure playing)
                        if (currentWeatherPreset.weatherParticles != null && currentWeatherPreset.weatherParticles.Length > 0)
                        {
                            for (int w = 0; w < currentWeatherPreset.weatherParticles.Length; w++)
                            {
                                var ps = currentWeatherPreset.weatherParticles[w].particleSystem;
                                if (ps == null) continue;

                                particleEmission = ps.emission;
                                particleNoise = ps.noise;

                                particleEmission.rateOverTime = Mathf.Lerp(
                                    particleEmission.rateOverTime.constant,
                                    currentWeatherPreset.weatherParticles[w].particleAmount,
                                    hourlyTimePercent
                                );

                                particleNoise.strength = Mathf.Lerp(
                                    particleNoise.strength.constant,
                                    currentWeatherPreset.weatherParticles[w].noiseStrength,
                                    hourlyTimePercent
                                );

                                if (!ps.isPlaying) ps.Play();
                            }
                        }
                    }

                    if (temperature <= 0f && currentWeatherPreset.isRaining)
                    {
                        Shader.SetGlobalFloat("_isSnowing", 1);
                    }
                    else if (temperature > 0f && currentWeatherPreset.isRaining)
                    {
                        Shader.SetGlobalFloat("_isSnowing", 0);
                    }
                }
            }
        }

        private bool _lastToggleTempUI, _lastToggleRainUI, _lastToggleWeatherUI;

        private void Update()
        {
            SetCurrentConditions();

            // Only toggle GameObjects when toggles change
            if (_lastToggleTempUI != toggleTempUI ||
                _lastToggleRainUI != toggleRainUI ||
                _lastToggleWeatherUI != toggleWeatherUI)
            {
                _lastToggleTempUI = toggleTempUI;
                _lastToggleRainUI = toggleRainUI;
                _lastToggleWeatherUI = toggleWeatherUI;
                ToggleUI();
            }
        }

        private int ResolvePresetIndex(float temp, float rain, bool isRaining)
        {
            for (int w = 0; w < weatherDataPresets.Length; w++)
            {
                var p = weatherDataPresets[w];

                if (temp < p.tempRange.x || temp > p.tempRange.y) continue;
                if (rain < p.rainRange.x || rain > p.rainRange.y) continue;
                if (isRaining != p.isRaining) continue;

                return w;
            }
            return -1;
        }

    }

    [System.Serializable]
    public class SeasonConditions
    {
        [Tooltip("The name of this season")]
        public string season;
        [Tooltip("The temperature range of this season. \n This can be used to control which weather will be more likely to occur")]
        public Vector2 tempRange;
        [Tooltip("The chance of rain range of this season. \n This can be used to control which weather will be more likely to occur")]
        public Vector2 rainRange;
        [Tooltip("The range of time which will be the hottest in the day")]
        public Vector2 hottestTimeRange;
        [Tooltip("The range of time which will be the coldest in the morning")]
        public Vector2 coldestMorningTimeRange;
        [Tooltip("The range of time which will be the coldest at night")]
        public Vector2 coldestNightTimeRange;
        [Tooltip("The range of time which the sun will rise")]
        public Vector2 sunriseTimeRange;
        [Tooltip("The range of time which the sun will set")]
        public Vector2 sunsetTimeRange;

        [Tooltip("Sky Data for the season. \n This allows for seasonal colour changes to the skybox and differences in the sun size and intensity")]
        public TimeController.SkyData seasonalSkyData;
    }

    [System.Serializable]
    public class HourlyWeather
    {
        [Tooltip("The time of this forcast hour")]
        public int forcastTime;
        [Tooltip("The temperature at the start of this forcast hour")]
        public float temp;
        [Tooltip("The chance of rain at the start of this forcast hour")]
        public float rainChance;
        [Tooltip("The weather condition of this forcast hour")]
        public string weatherCondition;
       // [Tooltip("The cloud strength this forcast hour \n 0 = full cloud cover, 5 = clear sky")]
        public float cloudPower;
        //public float cloudAlpha;
        //public HourlyClouds[] hourlyClouds;
        //[Tooltip("The windspeed this forcast hour")]
        //public float windSpeed;
        //[Tooltip("The surface wetness of this forcast hour \n Only applied to materials with the 'Wet' or 'WetAndSnowy' shader")]
        //public float wetness;
        //[Tooltip("The surface snowiness of this forcast hour  \n Only applied to materials with the 'Snowy' or 'WetAndSnowy' shader")]
        //public float snowiness;
        [Tooltip("Whether it is raining during this hour")]
        public bool isRaining = false;
        [HideInInspector] public bool isMidnight = false;
        [Tooltip("The audio clip to play during this hours weather. \n selected randomly from 'clips' in the weather preset")]
        public AudioClip weatherAudio;
        [HideInInspector] public int presetIndex = -1;

    }

    [System.Serializable]
    public class HourlyClouds
    {
        public Renderer cloudRenderer;
        public float cloudPower;
        public float cloudAlpha;
        [field: ReadOnlyField] public float baseAlpha;
        // Add this field inside the HourlyClouds class:
        [System.NonSerialized] public MaterialPropertyBlock mpb;

    }
}

