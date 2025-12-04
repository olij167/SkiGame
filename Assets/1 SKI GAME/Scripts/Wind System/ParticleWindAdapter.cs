using UnityEngine;

/// <summary>
/// Uses the global wind field to push particles via Force Over Lifetime.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticleWindAdapter : MonoBehaviour
{
    [Tooltip("How strongly the steady wind influences this particle system.")]
    public float forceMultiplier = 0.5f;

    [Tooltip("How much turbulence adds extra variation.")]
    public float turbulenceMultiplier = 0.2f;

    ParticleSystem ps;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void LateUpdate()
    {
        if (WindController.Instance == null || ps == null)
            return;

        var sample = WindController.Instance.SampleWind(transform.position);

        Vector3 totalForce = sample.steady * forceMultiplier
                           + sample.turbulence * turbulenceMultiplier;

        var fol = ps.forceOverLifetime;
        fol.enabled = true;
        fol.space = ParticleSystemSimulationSpace.World;
        fol.randomized = false; // we're already giving it a noisy vector

        fol.x = new ParticleSystem.MinMaxCurve(totalForce.x);
        fol.y = new ParticleSystem.MinMaxCurve(totalForce.y);
        fol.z = new ParticleSystem.MinMaxCurve(totalForce.z);
    }
}
