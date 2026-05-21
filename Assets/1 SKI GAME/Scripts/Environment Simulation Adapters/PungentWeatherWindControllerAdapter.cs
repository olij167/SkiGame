using PungentFunk.Utilities.EnvironmentSimulation;
using UnityEngine;

/// <summary>
/// SkiGame-only bridge from the generic Pungent weather state to the existing WindController.
/// This intentionally lives outside the PungentFunk package so the generic utilities do not depend on SkiGame classes.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SkiGame/Environment Simulation/Pungent Weather Wind Controller Adapter")]
public sealed class PungentWeatherWindControllerAdapter : MonoBehaviour
{
    [SerializeField] private PungentWeatherController weatherController;
    [SerializeField] private WindController windController;
    [SerializeField] private bool driveWindController = true;
    [SerializeField] private float speedMultiplier = 5f;
    [SerializeField] private float gustMultiplier = 0.65f;
    [SerializeField] private float turbulenceMultiplier = 2f;

    private void OnEnable()
    {
        ResolveReferences();
        if (weatherController != null)
            weatherController.WeatherChanged += HandleWeatherChanged;

        Apply(weatherController != null ? weatherController.CurrentState : new PungentWeatherState());
    }

    private void OnDisable()
    {
        if (weatherController != null)
            weatherController.WeatherChanged -= HandleWeatherChanged;
    }

    private void Update()
    {
        if (weatherController != null)
            Apply(weatherController.CurrentState);
    }

    private void OnValidate()
    {
        speedMultiplier = Mathf.Max(0f, speedMultiplier);
        gustMultiplier = Mathf.Max(0f, gustMultiplier);
        turbulenceMultiplier = Mathf.Max(0f, turbulenceMultiplier);
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (weatherController == null)
            weatherController = FindObjectOfType<PungentWeatherController>();
        if (windController == null)
            windController = WindController.Instance != null ? WindController.Instance : FindObjectOfType<WindController>();
    }

    private void HandleWeatherChanged(PungentWeatherState state)
    {
        Apply(state);
    }

    private void Apply(PungentWeatherState state)
    {
        if (!driveWindController)
            return;

        if (windController == null)
            ResolveReferences();
        if (windController == null)
            return;

        windController.inputMode = WindController.WindInputMode.Manual;
        Vector3 direction = state.WindDirection;
        if (direction.sqrMagnitude > 0.0001f)
            windController.baseDirection = direction;

        windController.baseSpeed = Mathf.Max(0f, state.windSpeed * speedMultiplier);
        windController.gustAmplitude = Mathf.Clamp01(state.gustStrength * gustMultiplier);
        windController.turbulenceStrength = Mathf.Max(0f, state.storminess * turbulenceMultiplier);
    }
}
