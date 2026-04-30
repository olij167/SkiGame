using UnityEngine;

public enum RescueTargetKind
{
    InjuredCasualty = 0,
    CompanionPassenger = 1,
    StrandedPassenger = 2
}

[DisallowMultipleComponent]
public sealed class RescueCasualtyTarget : MonoBehaviour
{
    [Header("Ragdoll Presentation")]
    [SerializeField] private bool forceInjuredCasualtyStacked = true;
    [SerializeField] private bool lockStackRecovery = true;

    [Tooltip("Local torque axis used when first forcing the casualty into a stacked ragdoll state.")]
    [SerializeField] private Vector3 initialStackLocalTorqueAxis = new Vector3(1f, 0f, 0.15f);

    [SerializeField, Range(0f, 1f)] private float initialStackSeverity = 0.35f;

    private RescueService _service;
    private int _targetIndex = -1;
    private RescueTargetKind _targetKind;
    private bool _transportLocked;

    public int TargetIndex => _targetIndex;
    public RescueTargetKind TargetKind => _targetKind;
    public bool IsTransportLocked => _transportLocked;
    public RescueService Service => _service;

    public void Initialize(RescueService service, int targetIndex, RescueTargetKind targetKind)
    {
        _service = service;
        _targetIndex = targetIndex;
        _targetKind = targetKind;

        ForceStackedRagdollForRescue(mountedOnTransport: false);
    }

    private void Start()
    {
        // Covers manually placed targets and prefab previews that were not initialized by RescueService yet.
        ForceStackedRagdollForRescue(mountedOnTransport: false);
    }

    public void SetTransportLocked(bool locked)
    {
        _transportLocked = locked;

        if (locked)
            ForceStackedRagdollForRescue(mountedOnTransport: true);
    }

    public void ForceStackedRagdollForRescue(bool mountedOnTransport)
    {
        if (!forceInjuredCasualtyStacked)
            return;

        if (_targetKind != RescueTargetKind.InjuredCasualty)
            return;

        WalkingController walk = FindTargetComponent<WalkingController>();
        SkiController ski = FindTargetComponent<SkiController>();
        SkierLimbLineVisual limbVisual = FindTargetComponent<SkierLimbLineVisual>();

        if (limbVisual != null)
            limbVisual.SetSeatedPoseOverride(false);

        if (walk != null)
        {
            walk.SetRiderPoseActive(false);
            walk.ControlsEnabled = false;

            // Before pickup, make sure the casualty is in ski/stack presentation.
            // Once mounted, avoid mode handoff nudges because the stretcher owns placement.
            if (!mountedOnTransport)
                walk.ForceEnterSkiMode();

            walk.enabled = false;
        }

        if (ski != null)
        {
            ski.enabled = true;

            Vector3 torqueAxis = transform.TransformDirection(initialStackLocalTorqueAxis);
            if (torqueAxis.sqrMagnitude < 0.0001f)
                torqueAxis = transform.right;

            ski.ForceStackedRagdollState(
                lockRecovery: lockStackRecovery,
                refreshVisualState: true,
                inheritedVelocityWorld: Vector3.zero,
                torqueAxisWorld: torqueAxis,
                severity01: initialStackSeverity,
                reason: mountedOnTransport ? "RescueCasualtyMounted" : "RescueCasualtyInitial");
        }
    }

    private T FindTargetComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component != null)
            return component;

        component = GetComponentInChildren<T>(true);
        if (component != null)
            return component;

        return GetComponentInParent<T>();
    }
}