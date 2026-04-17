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
            [Tooltip("Glyph shown in UI for this weather preset, e.g. ☀, ☁, ❄, 🌧, ⛈")]
            public string weatherGlyph = "◌";

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

            public Vector2 cloudPowerRange;
            public Vector2 cloudAlphaRange = new Vector2(0.25f, 1f);

            [Header("Dynamic Cloud Motion")]
            [Tooltip("Base wind speed range for this weather preset. This drives cloud drift.")]
            public Vector2 windSpeedRange = new Vector2(0.2f, 1f);
            [Tooltip("Wind direction in degrees. Use a small range for stable weather and a wide range for chaotic weather.")]
            public Vector2 windDirectionRange = new Vector2(0f, 360f);
            [Tooltip("How much the wind gusts around the base speed.")]
            public Vector2 gustStrengthRange = new Vector2(0.05f, 0.2f);

            [Header("Skybox Cloud Shaping")]
            public Vector2 skyCloudAlphaRange = new Vector2(0.15f, 0.75f);
            public Vector2 skyCloudPowerRange = new Vector2(0.5f, 4.5f);
            [Range(0.01f, 0.5f)] public float cloudSoftness = 0.14f;
            [Range(-0.5f, 0.5f)] public float cloudCoverageBias = 0f;
            [Range(0f, 1f)] public float cloudTurbulence = 0.35f;
            [Range(0f, 1f)] public float cloudWarpStrength = 0.16f;
            public Color cloudTint = Color.white;
            [Range(0f, 1f)] public float cloudShadowStrength = 0.65f;
            [Range(0f, 1f)] public float cloudGreyStrength = 0.55f;
            [Range(0f, 1f)] public float cloudSunLightStrength = 0.60f;

            [Header("Scene Cloud Layer Controls")]
            public Vector2 sceneCloudAlphaRange = new Vector2(0.40f, 0.95f);
            public Vector2 sceneCloudPowerRange = new Vector2(1.2f, 2.4f);

            [Range(0.01f, 0.5f)] public float sceneCloudSoftness = 0.18f;
            [Range(-0.5f, 0.5f)] public float sceneCloudCoverageBias = 0.04f;
            [Range(0f, 1f)] public float sceneCloudTurbulence = 0.42f;
            [Range(0f, 1f)] public float sceneCloudWarpStrength = 0.18f;

            public Color sceneCloudTint = new Color(0.94f, 0.96f, 0.98f, 1f);
            public Color sceneCloudShadowTint = new Color(0.58f, 0.63f, 0.70f, 1f);

            [Range(0f, 1f)] public float planeEdgeFade = 0.10f;
            [Range(0f, 1f)] public float planeRadialFade = 0.16f;
            [Range(0f, 1f)] public float planeHeightFade = 0.10f;

            [Range(0f, 8f)] public float sceneCloudFresnelFade = 1.75f;
            [Range(0f, 2f)] public float sceneCloudMacroRoundness = 1.10f;
            [Range(0f, 2f)] public float sceneCloudDetailStrength = 0.60f;
            [Range(0.5f, 3f)] public float sceneCloudDensityContrast = 1.45f;

            [Range(0f, 2f)] public float sceneCloudErosionStrength = 0.55f;
            [Range(0f, 2f)] public float sceneCloudSecondLayerStrength = 0.60f;
            [Range(0f, 2f)] public float sceneCloudSilverLiningStrength = 0.30f;
            [Range(0f, 2f)] public float sceneCloudBottomDarkening = 0.85f;

            [Range(0.25f, 2f)] public float sceneCloudDriftMultiplier = 1.0f;

            [Header("Terrain Cloud Shadow Controls")]
            [Range(0f, 1f)] public float terrainCloudShadowStrength = 0.22f;
            [Range(20f, 1200f)] public float terrainCloudShadowScale = 320f;
            [Range(0.01f, 0.5f)] public float terrainCloudShadowSoftness = 0.14f;
            [Range(-0.5f, 0.5f)] public float terrainCloudShadowBias = 0.04f;
            [Range(0.25f, 2f)] public float terrainCloudShadowDriftMultiplier = 1.0f;
            public Color terrainCloudShadowTint = new Color(0.80f, 0.84f, 0.90f, 1f);

            [Header("Volumetric Cloud Controls")]
            [Range(0f, 2f)] public float volumeDensity = 0.65f;
            [Range(0f, 4f)] public float volumeAbsorption = 1.15f;
            [Range(0f, 2f)] public float volumeLightingStrength = 0.75f;
            [Range(0f, 2f)] public float volumeAlphaMultiplier = 1.0f;

            [Range(10f, 600f)] public float volumeClusterScale = 180f;
            [Range(0f, 2f)] public float volumeClusterDensity = 0.72f;
            [Range(0f, 2f)] public float volumeDetailStrength = 0.85f;
            [Range(0.02f, 0.45f)] public float volumeEdgeFade = 0.22f;

            [Tooltip("The strength of the fog when this condition is active")]
            [Range(0f, 1f)] public float fogStrength;

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
        private static readonly int ID_CloudSoftness = Shader.PropertyToID("_CloudSoftness");
        private static readonly int ID_CloudCoverageBias = Shader.PropertyToID("_CloudCoverageBias");
        private static readonly int ID_CloudTurbulence = Shader.PropertyToID("_CloudTurbulence");
        private static readonly int ID_CloudWarpStrength = Shader.PropertyToID("_CloudWarpStrength");
        private static readonly int ID_EdgeFade = Shader.PropertyToID("_EdgeFade");
        private static readonly int ID_RadialFade = Shader.PropertyToID("_RadialFade");
        private static readonly int ID_CloudColour = Shader.PropertyToID("_CloudColour");
        static readonly int ID_CloudSeed = Shader.PropertyToID("_CloudSeed");

        private static readonly int ID_VolCloudColour = Shader.PropertyToID("_VolCloudColour");
        private static readonly int ID_VolShadowColour = Shader.PropertyToID("_VolShadowColour");
        private static readonly int ID_VolDensity = Shader.PropertyToID("_VolDensity");
        private static readonly int ID_VolAbsorption = Shader.PropertyToID("_VolAbsorption");
        private static readonly int ID_VolLightingStrength = Shader.PropertyToID("_VolLightingStrength");
        private static readonly int ID_VolCloudSpeed = Shader.PropertyToID("_VolCloudSpeed");
        private static readonly int ID_VolCloudSoftness = Shader.PropertyToID("_VolCloudSoftness");
        private static readonly int ID_VolCoverageBias = Shader.PropertyToID("_VolCoverageBias");
        private static readonly int ID_VolTurbulence = Shader.PropertyToID("_VolTurbulence");
        private static readonly int ID_VolWarpStrength = Shader.PropertyToID("_VolWarpStrength");
        private static readonly int ID_VolAlphaMultiplier = Shader.PropertyToID("_VolAlphaMultiplier");
        private static readonly int ID_VolClusterScale = Shader.PropertyToID("_VolClusterScale");
        private static readonly int ID_VolClusterDensity = Shader.PropertyToID("_VolClusterDensity");
        private static readonly int ID_VolDetailStrength = Shader.PropertyToID("_VolDetailStrength");
        private static readonly int ID_VolEdgeFade = Shader.PropertyToID("_VolEdgeFade");
        private static readonly int ID_VolFieldOffset = Shader.PropertyToID("_VolFieldOffset");

        private static readonly int ID_ShadowColour = Shader.PropertyToID("_ShadowColour");
        private static readonly int ID_HeightFade = Shader.PropertyToID("_HeightFade");
        private static readonly int ID_FresnelFade = Shader.PropertyToID("_FresnelFade");
        private static readonly int ID_MacroRoundness = Shader.PropertyToID("_MacroRoundness");
        private static readonly int ID_DetailStrength = Shader.PropertyToID("_DetailStrength");
        private static readonly int ID_DensityContrast = Shader.PropertyToID("_DensityContrast");
        private static readonly int ID_ErosionStrength = Shader.PropertyToID("_ErosionStrength");
        private static readonly int ID_SecondLayerStrength = Shader.PropertyToID("_SecondLayerStrength");
        private static readonly int ID_SilverLiningStrength = Shader.PropertyToID("_SilverLiningStrength");
        private static readonly int ID_BottomDarkening = Shader.PropertyToID("_BottomDarkening");

        private static readonly int ID_WeatherCloudShadowStrength = Shader.PropertyToID("_WeatherCloudShadowStrength");
        private static readonly int ID_WeatherCloudShadowScale = Shader.PropertyToID("_WeatherCloudShadowScale");
        private static readonly int ID_WeatherCloudShadowSoftness = Shader.PropertyToID("_WeatherCloudShadowSoftness");
        private static readonly int ID_WeatherCloudShadowBias = Shader.PropertyToID("_WeatherCloudShadowBias");
        private static readonly int ID_WeatherCloudShadowOffset = Shader.PropertyToID("_WeatherCloudShadowOffset");
        private static readonly int ID_WeatherCloudShadowTint = Shader.PropertyToID("_WeatherCloudShadowTint");

        private Vector2 _lastCloudSpeed;

        private Vector2 _globalVolumetricFieldOffset;
        private Vector2 _globalTerrainCloudShadowOffset;

        [Header("Cloud Drift Multipliers")]
        [Tooltip("Extra drift multiplier applied to the skybox cloud speed.")]
        public float skyboxCloudDriftScale = 3.0f;

        [Tooltip("Extra drift multiplier applied to scene cloud planes.")]
        public float sceneCloudDriftScale = 1.0f;

        [Tooltip("Minimum drift applied to the skybox so clouds never look static.")]
        public float minimumSkyboxDrift = 0.0015f;

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
                        ApplyWeatherProfileToHour(weatherDataPresets[idx], hourly, i);

                        if (weatherDataPresets[idx].clips != null && weatherDataPresets[idx].clips.Length > 0)
                            hourly.weatherAudio = weatherDataPresets[idx].clips[Random.Range(0, weatherDataPresets[idx].clips.Length)];
                    }
                    else
                    {
                        hourly.weatherCondition = string.Empty;
                        hourly.cloudPower = 5f;
                        hourly.cloudAlphaTarget = 0f;
                        hourly.skyCloudAlpha = 0f;
                        hourly.skyCloudPower = 5f;
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
                ApplyWeatherProfileToHour(weatherDataPresets[idx], hourlyWeather[hour], hour);

                if (weatherDataPresets[idx].clips != null && weatherDataPresets[idx].clips.Length > 0)
                    hourlyWeather[hour].weatherAudio = weatherDataPresets[idx].clips[Random.Range(0, weatherDataPresets[idx].clips.Length)];
            }
            else
            {
                hourlyWeather[hour].weatherCondition = string.Empty;
                hourlyWeather[hour].cloudPower = 5f;
                hourlyWeather[hour].cloudAlphaTarget = 0f;
                hourlyWeather[hour].skyCloudAlpha = 0f;
                hourlyWeather[hour].skyCloudPower = 5f;
            }
        }

        private void ApplyWeatherProfileToHour(WeatherData preset, HourlyWeather hourly, int hourIndex)
        {
            if (preset == null || hourly == null) return;

            // Shared wind / timing
            hourly.windSpeedTarget = Random.Range(preset.windSpeedRange.x, preset.windSpeedRange.y);
            hourly.windDirection = Random.Range(preset.windDirectionRange.x, preset.windDirectionRange.y);
            hourly.gustStrength = Random.Range(preset.gustStrengthRange.x, preset.gustStrengthRange.y);

            // Skybox clouds
            hourly.skyCloudAlpha = Random.Range(preset.skyCloudAlphaRange.x, preset.skyCloudAlphaRange.y);
            hourly.skyCloudPower = Random.Range(preset.skyCloudPowerRange.x, preset.skyCloudPowerRange.y);
            hourly.cloudSoftness = preset.cloudSoftness;
            hourly.cloudCoverageBias = preset.cloudCoverageBias;
            hourly.cloudTurbulence = preset.cloudTurbulence;
            hourly.cloudWarpStrength = preset.cloudWarpStrength;
            hourly.cloudTint = preset.cloudTint;
            hourly.cloudShadowStrength = preset.cloudShadowStrength;
            hourly.cloudGreyStrength = preset.cloudGreyStrength;
            hourly.cloudSunLightStrength = preset.cloudSunLightStrength;

            // Dedicated scene cloud layer
            hourly.sceneCloudAlpha = Random.Range(preset.sceneCloudAlphaRange.x, preset.sceneCloudAlphaRange.y);
            hourly.sceneCloudPower = Random.Range(preset.sceneCloudPowerRange.x, preset.sceneCloudPowerRange.y);
            hourly.sceneCloudSoftness = preset.sceneCloudSoftness;
            hourly.sceneCloudCoverageBias = preset.sceneCloudCoverageBias;
            hourly.sceneCloudTurbulence = preset.sceneCloudTurbulence;
            hourly.sceneCloudWarpStrength = preset.sceneCloudWarpStrength;
            hourly.sceneCloudTint = preset.sceneCloudTint;
            hourly.sceneCloudShadowTint = preset.sceneCloudShadowTint;
            hourly.planeEdgeFade = preset.planeEdgeFade;
            hourly.planeRadialFade = preset.planeRadialFade;
            hourly.planeHeightFade = preset.planeHeightFade;
            hourly.sceneCloudFresnelFade = preset.sceneCloudFresnelFade;
            hourly.sceneCloudMacroRoundness = preset.sceneCloudMacroRoundness;
            hourly.sceneCloudDetailStrength = preset.sceneCloudDetailStrength;
            hourly.sceneCloudDensityContrast = preset.sceneCloudDensityContrast;
            hourly.sceneCloudErosionStrength = preset.sceneCloudErosionStrength;
            hourly.sceneCloudSecondLayerStrength = preset.sceneCloudSecondLayerStrength;
            hourly.sceneCloudSilverLiningStrength = preset.sceneCloudSilverLiningStrength;
            hourly.sceneCloudBottomDarkening = preset.sceneCloudBottomDarkening;
            hourly.sceneCloudDriftMultiplier = preset.sceneCloudDriftMultiplier;

            // Terrain cloud shadow field
            hourly.terrainCloudShadowStrength = preset.terrainCloudShadowStrength;
            hourly.terrainCloudShadowScale = preset.terrainCloudShadowScale;
            hourly.terrainCloudShadowSoftness = preset.terrainCloudShadowSoftness;
            hourly.terrainCloudShadowBias = preset.terrainCloudShadowBias;
            hourly.terrainCloudShadowDriftMultiplier = preset.terrainCloudShadowDriftMultiplier;
            hourly.terrainCloudShadowTint = preset.terrainCloudShadowTint;

            // Volumetric clouds
            hourly.volumeDensity = preset.volumeDensity;
            hourly.volumeAbsorption = preset.volumeAbsorption;
            hourly.volumeLightingStrength = preset.volumeLightingStrength;
            hourly.volumeAlphaMultiplier = preset.volumeAlphaMultiplier;
            hourly.volumeClusterScale = preset.volumeClusterScale;
            hourly.volumeClusterDensity = preset.volumeClusterDensity;
            hourly.volumeDetailStrength = preset.volumeDetailStrength;
            hourly.volumeEdgeFade = preset.volumeEdgeFade;
        }

        private static Vector2 DirectionFromDegrees(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private float InterpolateAngle(float a, float b, float t)
        {
            return a + Mathf.DeltaAngle(a, b) * t;
        }

        private float SampleGust01(float timeValue, float seed)
        {
            return Mathf.PerlinNoise(timeValue * 0.075f + seed, seed * 0.173f);
        }

        public bool TryGetCurrentVolumetricCloudState(
    out Color cloudTint,
    out float softness,
    out float coverageBias,
    out float turbulence,
    out float warpStrength,
    out float density,
    out float absorption,
    out float lightingStrength,
    out float alphaMultiplier,
    out float clusterScale,
    out float clusterDensity,
    out float detailStrength,
    out float edgeFade,
    out Vector2 drift)
        {
            cloudTint = Color.white;
            softness = 0.14f;
            coverageBias = 0f;
            turbulence = 0.35f;
            warpStrength = 0.16f;
            density = 0.65f;
            absorption = 1.15f;
            lightingStrength = 0.75f;
            alphaMultiplier = 1.0f;
            clusterScale = 180f;
            clusterDensity = 0.72f;
            detailStrength = 0.85f;
            edgeFade = 0.22f;
            drift = Vector2.zero;

            if (hourlyWeather == null || timeController == null || hourlyWeather.Count == 0)
                return false;

            int hour = Mathf.Clamp(timeController.timeHours, 0, Mathf.Max(0, hourlyWeather.Count - 2));
            float t = timeController.hourlyTimePercent;

            HourlyWeather currentHour = hourlyWeather[hour];
            HourlyWeather nextHour = hourlyWeather[Mathf.Min(hour + 1, hourlyWeather.Count - 1)];

            cloudTint = Color.Lerp(currentHour.cloudTint, nextHour.cloudTint, t);
            softness = Mathf.Lerp(currentHour.cloudSoftness, nextHour.cloudSoftness, t);
            coverageBias = Mathf.Lerp(currentHour.cloudCoverageBias, nextHour.cloudCoverageBias, t);
            turbulence = Mathf.Lerp(currentHour.cloudTurbulence, nextHour.cloudTurbulence, t);
            warpStrength = Mathf.Lerp(currentHour.cloudWarpStrength, nextHour.cloudWarpStrength, t);
            density = Mathf.Lerp(currentHour.volumeDensity, nextHour.volumeDensity, t);
            absorption = Mathf.Lerp(currentHour.volumeAbsorption, nextHour.volumeAbsorption, t);
            lightingStrength = Mathf.Lerp(currentHour.volumeLightingStrength, nextHour.volumeLightingStrength, t);
            alphaMultiplier = Mathf.Lerp(currentHour.volumeAlphaMultiplier, nextHour.volumeAlphaMultiplier, t);
            clusterScale = Mathf.Lerp(currentHour.volumeClusterScale, nextHour.volumeClusterScale, t);
            clusterDensity = Mathf.Lerp(currentHour.volumeClusterDensity, nextHour.volumeClusterDensity, t);
            detailStrength = Mathf.Lerp(currentHour.volumeDetailStrength, nextHour.volumeDetailStrength, t);
            edgeFade = Mathf.Lerp(currentHour.volumeEdgeFade, nextHour.volumeEdgeFade, t);

            Vector2 windDir = wind.sqrMagnitude > 0.0000001f ? wind.normalized : Vector2.right;
            drift = windDir * Mathf.Max(0.001f, windSpeed * 0.0035f) * sceneCloudDriftScale;

            return true;
        }

        public void SetCurrentConditions()
        {
            if (hourlyWeather == null || timeController == null || timeController.timeHours >= 23)
                return;

            float hourlyTimePercent = timeController.hourlyTimePercent;
            int hour = timeController.timeHours;

            HourlyWeather currentHour = hourlyWeather[hour];
            HourlyWeather nextHour = hourlyWeather[hour + 1];

            temperature = Mathf.Lerp(currentHour.temp, nextHour.temp, hourlyTimePercent);
            rainChance = Mathf.Lerp(currentHour.rainChance, nextHour.rainChance, hourlyTimePercent);

            if (toggleTempUI && tempText != null)
                tempText.text = temperature.ToString("00") + "°C";

            int resolvedIdx = currentHour.presetIndex >= 0
                ? currentHour.presetIndex
                : ResolvePresetIndex(temperature, rainChance, currentHour.isRaining);

            WeatherData resolvedPreset = (resolvedIdx >= 0 && resolvedIdx < weatherDataPresets.Length)
                ? weatherDataPresets[resolvedIdx]
                : null;

            if (resolvedPreset != null)
                weatherCondition = resolvedPreset.weatherCondition;

            if (toggleWeatherUI && weatherConditionText != null)
                weatherConditionText.text = weatherCondition;

            if (toggleRainUI && rainText != null)
                rainText.text = rainChance.ToString("00") + "% Rain";

            if (_lastAppliedPreset != resolvedPreset)
            {
                currentWeatherPreset = resolvedPreset;
                _lastAppliedPreset = resolvedPreset;

                _activeCloudSet.Clear();
                if (currentWeatherPreset != null && currentWeatherPreset.activeClouds != null)
                {
                    for (int a = 0; a < currentWeatherPreset.activeClouds.Count; a++)
                    {
                        var r = currentWeatherPreset.activeClouds[a];
                        if (r != null) _activeCloudSet.Add(r);
                    }
                }

                _presetParticleSet.Clear();
                if (currentWeatherPreset != null && currentWeatherPreset.weatherParticles != null)
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
                currentWeatherPreset = resolvedPreset;
            }

            if (currentWeatherPreset == null)
                return;

            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, currentWeatherPreset.fogStrength, hourlyTimePercent * fogSpeed);

            if (weatherAudio == null)
            {
                weatherAudio = GetComponentInChildren<CrossFadeAudio>(true);
                if (weatherAudio == null)
                    weatherAudio = FindObjectOfType<CrossFadeAudio>(true);
            }

            if (weatherAudio != null && currentWeatherPreset.clips != null && currentWeatherPreset.clips.Length > 0)
                weatherAudio.newSoundtrack(currentHour.weatherAudio, currentWeatherPreset.volume);

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

            float targetWindSpeed = Mathf.Lerp(currentHour.windSpeedTarget, nextHour.windSpeedTarget, hourlyTimePercent);
            float targetWindDirection = InterpolateAngle(currentHour.windDirection, nextHour.windDirection, hourlyTimePercent);
            float gustStrength = Mathf.Lerp(currentHour.gustStrength, nextHour.gustStrength, hourlyTimePercent);
            float gustNoise = (SampleGust01(Time.time, targetWindDirection * 0.01f + hour * 0.37f) * 2f) - 1f;
            float gustedWindSpeed = Mathf.Max(0f, targetWindSpeed * (1f + gustNoise * gustStrength));

            windSpeed = Mathf.Lerp(windSpeed, gustedWindSpeed, hourlyTimePercent * 0.35f);
            Vector2 windDir = DirectionFromDegrees(targetWindDirection);
            wind = Vector2.Lerp(wind, windDir * (windSpeed * windMultiplier), hourlyTimePercent * 0.35f);

            Vector2 targetCloudDrift = windDir * (windSpeed * 0.0035f);
            Vector2 smoothedCloudDrift = Vector2.Lerp(_lastCloudSpeed, targetCloudDrift, hourlyTimePercent * 0.35f);
            _lastCloudSpeed = smoothedCloudDrift;

            Vector2 sceneCloudSpeed = smoothedCloudDrift * sceneCloudDriftScale;
            Vector2 skyboxCloudSpeed = smoothedCloudDrift * skyboxCloudDriftScale;

            // Ensure the skybox always has a subtle readable drift even in calm weather.
            if (skyboxCloudSpeed.sqrMagnitude < (minimumSkyboxDrift * minimumSkyboxDrift))
            {
                Vector2 fallbackDir = windDir.sqrMagnitude > 0.0001f ? windDir.normalized : new Vector2(0.85f, 0.35f).normalized;
                skyboxCloudSpeed = fallbackDir * minimumSkyboxDrift;
            }

            // Skybox shaping (keep existing shared skybox controls)
            float skyCloudSoftness = Mathf.Lerp(currentHour.cloudSoftness, nextHour.cloudSoftness, hourlyTimePercent);
            float skyCloudCoverageBias = Mathf.Lerp(currentHour.cloudCoverageBias, nextHour.cloudCoverageBias, hourlyTimePercent);
            float skyCloudTurbulence = Mathf.Lerp(currentHour.cloudTurbulence, nextHour.cloudTurbulence, hourlyTimePercent);
            float skyCloudWarpStrength = Mathf.Lerp(currentHour.cloudWarpStrength, nextHour.cloudWarpStrength, hourlyTimePercent);
            Color skyCloudTint = Color.Lerp(currentHour.cloudTint, nextHour.cloudTint, hourlyTimePercent);

            // Dedicated scene cloud shaping
            float sceneCloudAlphaTarget = Mathf.Lerp(currentHour.sceneCloudAlpha, nextHour.sceneCloudAlpha, hourlyTimePercent);
            float sceneCloudPowerTarget = Mathf.Lerp(currentHour.sceneCloudPower, nextHour.sceneCloudPower, hourlyTimePercent);
            float sceneCloudSoftness = Mathf.Lerp(currentHour.sceneCloudSoftness, nextHour.sceneCloudSoftness, hourlyTimePercent);
            float sceneCloudCoverageBias = Mathf.Lerp(currentHour.sceneCloudCoverageBias, nextHour.sceneCloudCoverageBias, hourlyTimePercent);
            float sceneCloudTurbulence = Mathf.Lerp(currentHour.sceneCloudTurbulence, nextHour.sceneCloudTurbulence, hourlyTimePercent);
            float sceneCloudWarpStrength = Mathf.Lerp(currentHour.sceneCloudWarpStrength, nextHour.sceneCloudWarpStrength, hourlyTimePercent);
            Color sceneCloudTint = Color.Lerp(currentHour.sceneCloudTint, nextHour.sceneCloudTint, hourlyTimePercent);
            Color sceneCloudShadowTint = Color.Lerp(currentHour.sceneCloudShadowTint, nextHour.sceneCloudShadowTint, hourlyTimePercent);

            float planeEdgeFade = Mathf.Lerp(currentHour.planeEdgeFade, nextHour.planeEdgeFade, hourlyTimePercent);
            float planeRadialFade = Mathf.Lerp(currentHour.planeRadialFade, nextHour.planeRadialFade, hourlyTimePercent);
            float planeHeightFade = Mathf.Lerp(currentHour.planeHeightFade, nextHour.planeHeightFade, hourlyTimePercent);
            float sceneCloudFresnelFade = Mathf.Lerp(currentHour.sceneCloudFresnelFade, nextHour.sceneCloudFresnelFade, hourlyTimePercent);
            float sceneCloudMacroRoundness = Mathf.Lerp(currentHour.sceneCloudMacroRoundness, nextHour.sceneCloudMacroRoundness, hourlyTimePercent);
            float sceneCloudDetailStrength = Mathf.Lerp(currentHour.sceneCloudDetailStrength, nextHour.sceneCloudDetailStrength, hourlyTimePercent);
            float sceneCloudDensityContrast = Mathf.Lerp(currentHour.sceneCloudDensityContrast, nextHour.sceneCloudDensityContrast, hourlyTimePercent);
            float sceneCloudErosionStrength = Mathf.Lerp(currentHour.sceneCloudErosionStrength, nextHour.sceneCloudErosionStrength, hourlyTimePercent);
            float sceneCloudSecondLayerStrength = Mathf.Lerp(currentHour.sceneCloudSecondLayerStrength, nextHour.sceneCloudSecondLayerStrength, hourlyTimePercent);
            float sceneCloudSilverLiningStrength = Mathf.Lerp(currentHour.sceneCloudSilverLiningStrength, nextHour.sceneCloudSilverLiningStrength, hourlyTimePercent);
            float sceneCloudBottomDarkening = Mathf.Lerp(currentHour.sceneCloudBottomDarkening, nextHour.sceneCloudBottomDarkening, hourlyTimePercent);
            float sceneCloudDriftMultiplier = Mathf.Lerp(currentHour.sceneCloudDriftMultiplier, nextHour.sceneCloudDriftMultiplier, hourlyTimePercent);

            // Terrain shadow field
            float terrainCloudShadowStrength = Mathf.Lerp(currentHour.terrainCloudShadowStrength, nextHour.terrainCloudShadowStrength, hourlyTimePercent);
            float terrainCloudShadowScale = Mathf.Lerp(currentHour.terrainCloudShadowScale, nextHour.terrainCloudShadowScale, hourlyTimePercent);
            float terrainCloudShadowSoftness = Mathf.Lerp(currentHour.terrainCloudShadowSoftness, nextHour.terrainCloudShadowSoftness, hourlyTimePercent);
            float terrainCloudShadowBias = Mathf.Lerp(currentHour.terrainCloudShadowBias, nextHour.terrainCloudShadowBias, hourlyTimePercent);
            float terrainCloudShadowDriftMultiplier = Mathf.Lerp(currentHour.terrainCloudShadowDriftMultiplier, nextHour.terrainCloudShadowDriftMultiplier, hourlyTimePercent);
            Color terrainCloudShadowTint = Color.Lerp(currentHour.terrainCloudShadowTint, nextHour.terrainCloudShadowTint, hourlyTimePercent);

            // Per-system drift separation
            sceneCloudSpeed = smoothedCloudDrift * sceneCloudDriftScale * sceneCloudDriftMultiplier;
            Vector2 terrainCloudShadowSpeed = smoothedCloudDrift * sceneCloudDriftScale * terrainCloudShadowDriftMultiplier;

            // Accumulate UV-space offset for cheap projected terrain shadows
            _globalTerrainCloudShadowOffset += terrainCloudShadowSpeed * Time.deltaTime;

            for (int r = 0; r < cloudRenderer.Length; r++)
            {
                var hc = cloudRenderer[r];
                if (hc == null || hc.cloudRenderer == null) continue;
                if (hc.mpb == null) hc.mpb = new MaterialPropertyBlock();

                bool isActive = _activeCloudSet.Contains(hc.cloudRenderer);

                float desiredPlaneAlpha = isActive ? sceneCloudAlphaTarget : 0f;
                float desiredPlanePower = isActive ? sceneCloudPowerTarget : 5f;

                hc.cloudAlpha = Mathf.Lerp(hc.cloudAlpha, desiredPlaneAlpha, hourlyTimePercent * (isActive ? 0.5f : 0.1f));
                hc.cloudPower = Mathf.Lerp(hc.cloudPower, desiredPlanePower, hourlyTimePercent * (isActive ? 0.35f : 0.08f));

                hc.cloudRenderer.GetPropertyBlock(hc.mpb);
                hc.mpb.SetFloat(ID_CloudAlpha, hc.cloudAlpha);
                hc.mpb.SetFloat(ID_CloudPower, hc.cloudPower);
                hc.mpb.SetVector(ID_CloudSpeed, new Vector4(sceneCloudSpeed.x, sceneCloudSpeed.y, 0f, 0f));
                hc.mpb.SetFloat(ID_CloudSoftness, sceneCloudSoftness);
                hc.mpb.SetFloat(ID_CloudCoverageBias, sceneCloudCoverageBias);
                hc.mpb.SetFloat(ID_CloudTurbulence, sceneCloudTurbulence);
                hc.mpb.SetFloat(ID_CloudWarpStrength, sceneCloudWarpStrength);
                hc.mpb.SetFloat(ID_EdgeFade, planeEdgeFade);
                hc.mpb.SetFloat(ID_RadialFade, planeRadialFade);
                hc.mpb.SetFloat(ID_HeightFade, planeHeightFade);
                hc.mpb.SetFloat(ID_FresnelFade, sceneCloudFresnelFade);
                hc.mpb.SetFloat(ID_MacroRoundness, sceneCloudMacroRoundness);
                hc.mpb.SetFloat(ID_DetailStrength, sceneCloudDetailStrength);
                hc.mpb.SetFloat(ID_DensityContrast, sceneCloudDensityContrast);
                hc.mpb.SetFloat(ID_ErosionStrength, sceneCloudErosionStrength);
                hc.mpb.SetFloat(ID_SecondLayerStrength, sceneCloudSecondLayerStrength);
                hc.mpb.SetFloat(ID_SilverLiningStrength, sceneCloudSilverLiningStrength);
                hc.mpb.SetFloat(ID_BottomDarkening, sceneCloudBottomDarkening);
                hc.mpb.SetColor(ID_CloudColour, sceneCloudTint);
                hc.mpb.SetColor(ID_ShadowColour, sceneCloudShadowTint);
                hc.cloudRenderer.SetPropertyBlock(hc.mpb);

                cloudRenderer[r] = hc;
            }

            // Global terrain cloud-shadow controls for world-space ground shaders
            Shader.SetGlobalFloat(ID_WeatherCloudShadowStrength, terrainCloudShadowStrength);
            Shader.SetGlobalFloat(ID_WeatherCloudShadowScale, Mathf.Max(1f, terrainCloudShadowScale));
            Shader.SetGlobalFloat(ID_WeatherCloudShadowSoftness, Mathf.Max(0.01f, terrainCloudShadowSoftness));
            Shader.SetGlobalFloat(ID_WeatherCloudShadowBias, terrainCloudShadowBias);
            Shader.SetGlobalVector(ID_WeatherCloudShadowOffset, new Vector4(_globalTerrainCloudShadowOffset.x, _globalTerrainCloudShadowOffset.y, 0f, 0f));
            Shader.SetGlobalColor(ID_WeatherCloudShadowTint, terrainCloudShadowTint);

            var skyMat = RenderSettings.skybox;
            if (skyMat != null)
            {
                float skyCloudAlpha = Mathf.Clamp01(Mathf.Lerp(currentHour.skyCloudAlpha, nextHour.skyCloudAlpha, hourlyTimePercent));
                float skyCloudPower = Mathf.Clamp(Mathf.Lerp(currentHour.skyCloudPower, nextHour.skyCloudPower, hourlyTimePercent), 0f, 5f);
                float skyShadowStrength = Mathf.Lerp(currentHour.cloudShadowStrength, nextHour.cloudShadowStrength, hourlyTimePercent);
                float skyGreyStrength = Mathf.Lerp(currentHour.cloudGreyStrength, nextHour.cloudGreyStrength, hourlyTimePercent);
                float skySunLightStrength = Mathf.Lerp(currentHour.cloudSunLightStrength, nextHour.cloudSunLightStrength, hourlyTimePercent);

                if (skyMat.HasProperty(ID_CloudSeed))
                {
                    float seed = (timeController != null ? timeController.dayCount : 0) * 31.73f + resolvedIdx * 101.11f;
                    skyMat.SetFloat(ID_CloudSeed, seed);
                }

                if (skyMat.HasProperty("_CloudAlpha")) skyMat.SetFloat("_CloudAlpha", skyCloudAlpha);
                if (skyMat.HasProperty("_CloudPower")) skyMat.SetFloat("_CloudPower", skyCloudPower);
                if (skyMat.HasProperty("_CloudSpeed")) skyMat.SetVector("_CloudSpeed", new Vector4(skyboxCloudSpeed.x, skyboxCloudSpeed.y, 0f, 0f));
                if (skyMat.HasProperty("_CloudSoftness")) skyMat.SetFloat("_CloudSoftness", skyCloudSoftness);
                if (skyMat.HasProperty("_CloudCoverageBias")) skyMat.SetFloat("_CloudCoverageBias", skyCloudCoverageBias);
                if (skyMat.HasProperty("_CloudTurbulence")) skyMat.SetFloat("_CloudTurbulence", skyCloudTurbulence);
                if (skyMat.HasProperty("_CloudWarpStrength")) skyMat.SetFloat("_CloudWarpStrength", skyCloudWarpStrength);
                if (skyMat.HasProperty("_CloudColor")) skyMat.SetColor("_CloudColor", skyCloudTint);
                if (skyMat.HasProperty("_CloudShadowStrength")) skyMat.SetFloat("_CloudShadowStrength", skyShadowStrength);
                if (skyMat.HasProperty("_CloudGreyStrength")) skyMat.SetFloat("_CloudGreyStrength", skyGreyStrength);
                if (skyMat.HasProperty("_CloudSunLightStrength")) skyMat.SetFloat("_CloudSunLightStrength", skySunLightStrength);

                if (skyMat.HasProperty("_FogColor"))
                    skyMat.SetColor("_FogColor", RenderSettings.fogColor);

                if (skyMat.HasProperty("_FogDensity"))
                    skyMat.SetFloat("_FogDensity", RenderSettings.fogDensity);

                if (skyMat.HasProperty("_FogSkyStrength"))
                    skyMat.SetFloat("_FogSkyStrength", Mathf.Lerp(2.0f, 6.0f, Mathf.Clamp01(currentWeatherPreset.fogStrength)));

                if (skyMat.HasProperty("_FogHorizonBoost"))
                    skyMat.SetFloat("_FogHorizonBoost", Mathf.Lerp(2.0f, 5.0f, Mathf.Clamp01(currentWeatherPreset.fogStrength)));

                if (TryGetCurrentVolumetricCloudState(
    out Color volCloudTint,
    out float volSoftness,
    out float volCoverageBias,
    out float volTurbulence,
    out float volWarpStrength,
    out float volDensity,
    out float volAbsorption,
    out float volLightingStrength,
    out float volAlphaMultiplier,
    out float volClusterScale,
    out float volClusterDensity,
    out float volDetailStrength,
    out float volEdgeFade,
    out Vector2 volDrift))
                {
                    _globalVolumetricFieldOffset += volDrift * Time.deltaTime;

                    Color volShadowColour = Color.Lerp(
                        volCloudTint * 0.65f,
                        new Color(0.62f, 0.68f, 0.76f, 1f),
                        0.65f
                    );

                    Shader.SetGlobalColor(ID_VolCloudColour, volCloudTint);
                    Shader.SetGlobalColor(ID_VolShadowColour, volShadowColour);
                    Shader.SetGlobalFloat(ID_VolDensity, volDensity);
                    Shader.SetGlobalFloat(ID_VolAbsorption, volAbsorption);
                    Shader.SetGlobalFloat(ID_VolLightingStrength, volLightingStrength);
                    Shader.SetGlobalVector(ID_VolCloudSpeed, new Vector4(volDrift.x, volDrift.y, 0f, 0f));
                    Shader.SetGlobalFloat(ID_VolCloudSoftness, volSoftness);
                    Shader.SetGlobalFloat(ID_VolCoverageBias, volCoverageBias);
                    Shader.SetGlobalFloat(ID_VolTurbulence, volTurbulence);
                    Shader.SetGlobalFloat(ID_VolWarpStrength, volWarpStrength);
                    Shader.SetGlobalFloat(ID_VolAlphaMultiplier, volAlphaMultiplier);
                    Shader.SetGlobalFloat(ID_VolClusterScale, volClusterScale);
                    Shader.SetGlobalFloat(ID_VolClusterDensity, volClusterDensity);
                    Shader.SetGlobalFloat(ID_VolDetailStrength, volDetailStrength);
                    Shader.SetGlobalFloat(ID_VolEdgeFade, volEdgeFade);
                    Shader.SetGlobalVector(ID_VolFieldOffset, new Vector4(_globalVolumetricFieldOffset.x, _globalVolumetricFieldOffset.y, 0f, 0f));
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

            Shader.SetGlobalFloat("_isSnowing", (temperature <= 0f && currentWeatherPreset.isRaining) ? 1f : 0f);
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

        public float cloudPower;
        public float cloudAlphaTarget;
        public float windSpeedTarget;
        public float windDirection;
        public float gustStrength;
        public float skyCloudAlpha;
        public float skyCloudPower;
        public float cloudSoftness;
        public float cloudCoverageBias;
        public float cloudTurbulence;
        public float cloudWarpStrength;
        public Color cloudTint = Color.white;
        public float cloudShadowStrength;
        public float cloudGreyStrength;
        public float cloudSunLightStrength;
        public float planeEdgeFade;
        public float planeRadialFade;

        public float sceneCloudAlpha;
        public float sceneCloudPower;
        public float sceneCloudSoftness;
        public float sceneCloudCoverageBias;
        public float sceneCloudTurbulence;
        public float sceneCloudWarpStrength;
        public Color sceneCloudTint = Color.white;
        public Color sceneCloudShadowTint = new Color(0.58f, 0.63f, 0.70f, 1f);

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
        public Color terrainCloudShadowTint = Color.white;

        public float volumeDensity;
        public float volumeAbsorption;
        public float volumeLightingStrength;
        public float volumeAlphaMultiplier;

        public float volumeClusterScale;
        public float volumeClusterDensity;
        public float volumeDetailStrength;
        public float volumeEdgeFade;

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

