using UnityEngine;

/// <summary>
/// Pushes Unity Cloth with the global wind field.
/// </summary>
[RequireComponent(typeof(Cloth))]
public class ClothWindAdapter : MonoBehaviour
{
    [Tooltip("How strongly the steady wind pushes this cloth.")]
    public float steadyAccelerationMultiplier = 1f;

    [Tooltip("How strongly turbulence affects this cloth.")]
    public float turbulenceAccelerationMultiplier = 1f;

    Cloth cloth;

    void Awake()
    {
        cloth = GetComponent<Cloth>();
    }

    void LateUpdate()
    {
        if (WindController.Instance == null || cloth == null)
            return;

        var sample = WindController.Instance.SampleWind(transform.position);

        // Cloth expects accelerations (roughly m/s^2)
        cloth.externalAcceleration = sample.steady * steadyAccelerationMultiplier;
        cloth.randomAcceleration = sample.turbulence * turbulenceAccelerationMultiplier;
    }
}
