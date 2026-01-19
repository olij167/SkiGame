using UnityEngine;
using TimeWeather;

/// <summary>
/// Global wind field. Other components sample this to know the wind at a position.
/// Supports altitude-based speed, gusts over time, and local turbulence.
/// Optionally can be driven by TimeWeather.WeatherController/TimeController without modifying them.
/// </summary>
public class WindController : MonoBehaviour
{
    public static WindController Instance { get; private set; }

    public enum WindInputMode
    {
        Manual = 0,
        FromTimeWeather = 1
    }

    [Header("Input Mode")]
    [Tooltip("Manual: use the values below as-is.\nFromTimeWeather: derive effective wind from WeatherController/TimeController (without modifying them).")]
    public WindInputMode inputMode = WindInputMode.Manual;

    [Tooltip("Optional explicit reference. If null, will use WeatherController.instance if available.")]
    public WeatherController weatherController;

    [Tooltip("Optional explicit reference. If null, will use TimeController.instance if available.")]
    public TimeController timeController;

    [Header("Base Wind (Manual Baseline)")]
    [Tooltip("Base direction of the wind in world space (used in Manual mode, or as fallback in TimeWeather mode).")]
    public Vector3 baseDirection = new Vector3(1f, 0f, 0f);

    [Tooltip("Base wind speed (arbitrary units, treat like m/s). Used in Manual mode, or as fallback in TimeWeather mode.")]
    public float baseSpeed = 5f;

    [Header("From TimeWeather (Mapping)")]
    [Tooltip("If true, wind direction is generated deterministically from TimeController (stable per time block). If false, uses baseDirection.")]
    public bool deriveDirectionFromTime = true;

    [Tooltip("How many in-game hours before direction can change (when deriveDirectionFromTime = true).")]
    [Range(1, 24)]
    public int directionStepHours = 6;

    [Tooltip("If WeatherController.windSpeed is > 0, use it (scaled) as the base speed.")]
    public bool useWeatherWindSpeedIfPresent = true;

    [Tooltip("Multiplier to convert WeatherController.windSpeed into WindController baseSpeed units.")]
    public float weatherWindSpeedMultiplier = 1f;

    [Tooltip("Extra speed added when raining (in WindController units).")]
    public float rainingSpeedBoost = 2f;

    [Tooltip("Extra speed added at 100% rain chance (in WindController units).")]
    public float rainChanceSpeedBoost = 3f;

    [Tooltip("Scales gust amplitude as rain chance increases.")]
    public float rainChanceToGustMultiplier = 1.0f;

    [Tooltip("Scales turbulence strength as rain chance increases.")]
    public float rainChanceToTurbulenceMultiplier = 1.5f;

    [Header("Altitude Influence")]
    [Tooltip("Min world Y considered for altitude normalization.")]
    public float minAltitude = 0f;

    [Tooltip("Max world Y considered for altitude normalization.")]
    public float maxAltitude = 200f;

    [Tooltip("Speed multiplier by normalized altitude (0 = minAltitude, 1 = maxAltitude).")]
    public AnimationCurve speedByAltitude = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Gusts (slow global fluctuations)")]
    [Tooltip("How strong gusts can deviate from 1. 0.5 = +/-50% speed.")]
    [Range(0f, 1f)]
    public float gustAmplitude = 0.4f;

    [Tooltip("How fast gusts change over time.")]
    public float gustFrequency = 0.1f;

    [Header("Turbulence (local, noisy variation)")]
    [Tooltip("Overall strength of small-scale turbulence.")]
    public float turbulenceStrength = 2f;

    [Tooltip("Spatial scale of turbulence noise. Larger = smoother over distance.")]
    public float turbulenceSpatialScale = 0.05f;

    [Tooltip("How fast turbulence changes over time.")]
    public float turbulenceTimeScale = 1f;

    [Header("Auto Discovery (optional)")]
    [Tooltip("On start, automatically attach adapter components to Cloth, ParticleSystem, and WindZone objects.")]
    public bool autoAttachAdaptersOnStart = true;

    [Tooltip("Also search inactive objects when auto-attaching adapters.")]
    public bool alsoAffectInactiveObjects = false;

    float timeOffset;

    // Cached effective values (so SampleWind stays cheap and consistent across many samplers per frame)
    Vector3 effectiveDirection;
    float effectiveBaseSpeed;
    float effectiveGustAmplitude;
    float effectiveTurbulenceStrength;

    // Manual baselines (so TimeWeather mode never permanently overwrites your authored values)
    Vector3 manualBaseDirection;
    float manualBaseSpeed;
    float manualGustAmplitude;
    float manualTurbulenceStrength;

    public struct WindSample
    {
        /// <summary>Steady wind vector at this position (includes direction, speed, altitude & gusts).</summary>
        public Vector3 steady;
        /// <summary>High-frequency local variation.</summary>
        public Vector3 turbulence;
        /// <summary>0..1 gust value (raw noise, can be used for pulses).</summary>
        public float gust;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        timeOffset = Random.value * 1000f;

        // Cache manual baselines
        manualBaseDirection = baseDirection;
        manualBaseSpeed = baseSpeed;
        manualGustAmplitude = gustAmplitude;
        manualTurbulenceStrength = turbulenceStrength;

        // Init effective values
        effectiveDirection = SafeDir(manualBaseDirection);
        effectiveBaseSpeed = Mathf.Max(0f, manualBaseSpeed);
        effectiveGustAmplitude = Mathf.Clamp01(manualGustAmplitude);
        effectiveTurbulenceStrength = Mathf.Max(0f, manualTurbulenceStrength);

        if (autoAttachAdaptersOnStart)
        {
            AutoAttachAdapters();
        }
    }

    void Start()
    {
        // Soft-bind to TimeWeather singletons if user didn’t assign.
        if (weatherController == null && WeatherController.instance != null)
            weatherController = WeatherController.instance;

        if (timeController == null && TimeController.instance != null)
            timeController = TimeController.instance;
    }

    void Update()
    {
        UpdateEffectiveWindInputs();
    }

    void UpdateEffectiveWindInputs()
    {
        // Always refresh manual baselines (in case designer tweaks in play mode)
        manualBaseDirection = baseDirection;
        manualBaseSpeed = baseSpeed;
        manualGustAmplitude = gustAmplitude;
        manualTurbulenceStrength = turbulenceStrength;

        if (inputMode == WindInputMode.Manual || weatherController == null)
        {
            effectiveDirection = SafeDir(manualBaseDirection);
            effectiveBaseSpeed = Mathf.Max(0f, manualBaseSpeed);
            effectiveGustAmplitude = Mathf.Clamp01(manualGustAmplitude);
            effectiveTurbulenceStrength = Mathf.Max(0f, manualTurbulenceStrength);
            return;
        }

        // From TimeWeather
        float rainChance01 = Mathf.Clamp01(weatherController.rainChance / 100f);

        bool isRaining = false;
        if (weatherController.currentWeatherPreset != null)
            isRaining = weatherController.currentWeatherPreset.isRaining;

        // Direction
        if (deriveDirectionFromTime && timeController != null)
        {
            effectiveDirection = ComputeDeterministicDirection(timeController, directionStepHours);
        }
        else
        {
            effectiveDirection = SafeDir(manualBaseDirection);
        }

        // Speed
        float speed = manualBaseSpeed;

        if (useWeatherWindSpeedIfPresent && weatherController.windSpeed > 0.001f)
        {
            speed = weatherController.windSpeed * weatherWindSpeedMultiplier;
        }

        // Weather modulation (works even if weather windSpeed is unused/zero)
        speed += rainChance01 * rainChanceSpeedBoost;
        if (isRaining) speed += rainingSpeedBoost;

        effectiveBaseSpeed = Mathf.Max(0f, speed);

        // Gust & turbulence scaling
        effectiveGustAmplitude = Mathf.Clamp01(manualGustAmplitude * (1f + rainChance01 * rainChanceToGustMultiplier));
        effectiveTurbulenceStrength = Mathf.Max(0f, manualTurbulenceStrength * (1f + rainChance01 * rainChanceToTurbulenceMultiplier));
    }

    static Vector3 SafeDir(Vector3 d)
    {
        if (d.sqrMagnitude < 0.0001f) return Vector3.zero;
        return d.normalized;
    }

    static Vector3 ComputeDeterministicDirection(TimeController tc, int stepHours)
    {
        // Stable across frames; changes only per hour block, and varies per day.
        int hour = tc.timeHours;
        int block = Mathf.FloorToInt(hour / Mathf.Max(1, stepHours));
        int day = tc.dayCount;

        // Deterministic hash -> 0..1
        float seed = (day + 1) * 0.1234567f + (block + 1) * 9.876543f;
        float n = Mathf.Repeat(Mathf.Sin(seed) * 43758.5453f, 1f);

        float angle = n * 360f;
        float rad = angle * Mathf.Deg2Rad;

        // World XZ wind
        return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
    }

    void AutoAttachAdapters()
    {
        // Attach Cloth adapter
        var cloths = FindObjectsOfType<Cloth>(alsoAffectInactiveObjects);
        foreach (var cloth in cloths)
        {
            if (!cloth.TryGetComponent<ClothWindAdapter>(out _))
            {
                cloth.gameObject.AddComponent<ClothWindAdapter>();
            }
        }

        // Attach Particle adapter
        var particleSystems = FindObjectsOfType<ParticleSystem>(alsoAffectInactiveObjects);
        foreach (var ps in particleSystems)
        {
            if (!ps.TryGetComponent<ParticleWindAdapter>(out _))
            {
                ps.gameObject.AddComponent<ParticleWindAdapter>();
            }
        }

        // Attach WindZone adapter (to drive trees / foliage that already listen to WindZone)
        var windZones = FindObjectsOfType<WindZone>(alsoAffectInactiveObjects);
        foreach (var wz in windZones)
        {
            if (!wz.TryGetComponent<WindZoneAdapter>(out _))
            {
                wz.gameObject.AddComponent<WindZoneAdapter>();
            }
        }
    }

    /// <summary>
    /// Sample the wind at a world position (steady + turbulence + gust scalar).
    /// </summary>
    public WindSample SampleWind(Vector3 worldPos)
    {
        float t = Time.time + timeOffset;

        // Altitude normalized [0,1]
        float altitude = worldPos.y;
        float altitude01 = Mathf.InverseLerp(minAltitude, maxAltitude, altitude);
        float altitudeFactor = speedByAltitude.Evaluate(altitude01);

        // Gusts (slow, global scalar)
        float gustNoise = Mathf.PerlinNoise(t * gustFrequency, 0.0f); // 0..1
        float gustScalar = Mathf.Lerp(1f - effectiveGustAmplitude, 1f + effectiveGustAmplitude, gustNoise);

        // Steady component
        Vector3 dir = effectiveDirection;
        float speed = effectiveBaseSpeed * altitudeFactor * gustScalar;
        Vector3 steady = dir * speed;

        // Turbulence: small, noisy, position-based vector
        float nx = Mathf.PerlinNoise(worldPos.x * turbulenceSpatialScale + t * turbulenceTimeScale,
                                     worldPos.z * turbulenceSpatialScale) * 2f - 1f;
        float nz = Mathf.PerlinNoise(worldPos.z * turbulenceSpatialScale - t * turbulenceTimeScale,
                                     worldPos.x * turbulenceSpatialScale) * 2f - 1f;
        float ny = Mathf.PerlinNoise(worldPos.x * turbulenceSpatialScale,
                                     t * turbulenceTimeScale) * 2f - 1f;

        Vector3 turbulence = new Vector3(nx, ny, nz) * effectiveTurbulenceStrength;

        return new WindSample
        {
            steady = steady,
            turbulence = turbulence,
            gust = gustNoise
        };
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position;

        Vector3 dir = SafeDir(Application.isPlaying ? effectiveDirection : baseDirection);
        if (dir == Vector3.zero) dir = Vector3.right;

        Gizmos.DrawLine(origin, origin + dir * 3f);
        Gizmos.DrawSphere(origin + dir * 3f, 0.2f);
    }
#endif
}
