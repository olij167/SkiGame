using UnityEngine;

/// <summary>
/// Syncs a WindZone with the global WindController so built-in trees/foliage react consistently.
/// Put this on the same GameObject as your WindZone.
/// </summary>
[RequireComponent(typeof(WindZone))]
public class WindZoneAdapter : MonoBehaviour
{
    [Tooltip("Scales the steady wind magnitude into WindZone.windMain.")]
    public float mainMultiplier = 1f;

    [Tooltip("Scales turbulence into WindZone.windTurbulence.")]
    public float turbulenceMultiplier = 0.5f;

    [Tooltip("Scales gust (0..1 noise) into WindZone.windPulseMagnitude.")]
    public float pulseMultiplier = 0.5f;

    WindZone windZone;

    void Awake()
    {
        windZone = GetComponent<WindZone>();
    }

    void LateUpdate()
    {
        if (WindController.Instance == null || windZone == null)
            return;

        var sample = WindController.Instance.SampleWind(transform.position);

        // Match direction
        Vector3 dir = WindController.Instance.baseDirection;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // Magnitudes
        windZone.windMain = sample.steady.magnitude * mainMultiplier;
        windZone.windTurbulence = sample.turbulence.magnitude * turbulenceMultiplier;
        windZone.windPulseMagnitude = sample.gust * pulseMultiplier;
    }
}
