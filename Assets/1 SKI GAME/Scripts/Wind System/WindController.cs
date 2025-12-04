using UnityEngine;

/// <summary>
/// Global wind field. Other components sample this to know the wind at a position.
/// Supports altitude-based speed, gusts over time, and local turbulence.
/// </summary>
public class WindController : MonoBehaviour
{
    public static WindController Instance { get; private set; }

    [Header("Base Wind")]
    [Tooltip("Base direction of the wind in world space.")]
    public Vector3 baseDirection = new Vector3(1f, 0f, 0f);

    [Tooltip("Base wind speed (arbitrary units, treat like m/s).")]
    public float baseSpeed = 5f;

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

        if (autoAttachAdaptersOnStart)
        {
            AutoAttachAdapters();
        }
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
        float gustScalar = Mathf.Lerp(1f - gustAmplitude, 1f + gustAmplitude, gustNoise);

        // Steady component
        Vector3 dir = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : Vector3.zero;
        float speed = baseSpeed * altitudeFactor * gustScalar;
        Vector3 steady = dir * speed;

        // Turbulence: small, noisy, position-based vector
        float nx = Mathf.PerlinNoise(worldPos.x * turbulenceSpatialScale + t * turbulenceTimeScale,
                                     worldPos.z * turbulenceSpatialScale) * 2f - 1f;
        float nz = Mathf.PerlinNoise(worldPos.z * turbulenceSpatialScale - t * turbulenceTimeScale,
                                     worldPos.x * turbulenceSpatialScale) * 2f - 1f;
        float ny = Mathf.PerlinNoise(worldPos.x * turbulenceSpatialScale,
                                     t * turbulenceTimeScale) * 2f - 1f;

        Vector3 turbulence = new Vector3(nx, ny, nz) * turbulenceStrength;

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
        Vector3 dir = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : Vector3.right;
        Gizmos.DrawLine(origin, origin + dir * 3f);
        Gizmos.DrawSphere(origin + dir * 3f, 0.2f);
    }
#endif
}
