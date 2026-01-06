using UnityEngine;

/// <summary>
/// Drives snow VFX for:
/// - Continuous ski spray (left/right).
/// - Single body impact burst on heavy landings.
/// - Per-pole snow (drag + impact) using ONE ParticleSystem per pole.
///
/// Attach to the same GameObject as SkiController (or a parent).
/// Wire references in the inspector.
/// </summary>
[DisallowMultipleComponent]
public class SkiSnowParticles : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private SkiContact leftSkiContact;
    [SerializeField] private SkiContact rightSkiContact;

    [SerializeField] private PoleContact leftPoleContact;
    [SerializeField] private PoleContact rightPoleContact;

    [Header("Ski Spray (continuous)")]
    [Tooltip("Continuous spray from under the left ski.")]
    [SerializeField] private ParticleSystem leftSkiSnow;

    [Tooltip("Continuous spray from under the right ski.")]
    [SerializeField] private ParticleSystem rightSkiSnow;

    [Tooltip("Minimum planar speed before we start emitting ski snow.")]
    [SerializeField] private float minSkiSpeedForSnow = 2f;

    [Tooltip("Planar speed at which ski spray reaches full strength.")]
    [SerializeField] private float maxSkiSpeedForMaxSnow = 20f;

    [Tooltip("Base emission rate for ski spray when speed + carve + alignment are maxed.")]
    [SerializeField] private float skiBaseRateOverTime = 150f;

    [Tooltip("How strongly base contact alignment modulates spray (0 = ignore alignment, 1 = full influence).")]
    [SerializeField, Range(0f, 1f)]
    private float baseAlignmentInfluence = 0.6f;

    [Tooltip("Sideways speed (m/s) relative to ski direction needed for full carve spray.")]
    [SerializeField] private float skiSidewaysSpeedForMaxCarve = 6f;

    [Header("Body Impact (single system)")]
    [Tooltip("Single impact particle system fired on hard landings.")]
    [SerializeField] private ParticleSystem bodyImpactSnow;

    [Tooltip("Minimum downward speed along ground normal to trigger a body impact burst.")]
    [SerializeField] private float minBodyImpactDownwardSpeed = 5f;

    [Tooltip("Burst particle count for a softer landing.")]
    [SerializeField] private int bodyImpactBurstCountLow = 50;

    [Tooltip("Burst particle count for a heavy landing.")]
    [SerializeField] private int bodyImpactBurstCountHigh = 150;

    [Header("Pole Snow (drag + impact in one PS per pole)")]
    [Tooltip("Snow particles from the LEFT pole tip (drag + impact).")]
    [SerializeField] private ParticleSystem leftPoleSnow;

    [Tooltip("Snow particles from the RIGHT pole tip (drag + impact).")]
    [SerializeField] private ParticleSystem rightPoleSnow;

    [Tooltip("Planar speed at which pole drag begins to be visible.")]
    [SerializeField] private float poleMinSpeedForDrag = 1.5f;

    [Tooltip("Planar speed at which pole drag reaches full strength.")]
    [SerializeField] private float poleMaxSpeedForDrag = 12f;

    [Tooltip("Base emission rate for pole drag when fully dragging (speed).")]
    [SerializeField] private float poleDragBaseRate = 80f;

    [Tooltip("Minimum downward speed along the contact normal to trigger a pole impact burst.")]
    [SerializeField] private float poleMinImpactDownwardSpeed = 2.5f;

    [Tooltip("Burst count for a light pole tap.")]
    [SerializeField] private int poleImpactBurstCountLow = 10;

    [Tooltip("Burst count for a heavy pole plant / dig.")]
    [SerializeField] private int poleImpactBurstCountHigh = 40;

    [Header("Behaviour / Debug")]
    [Tooltip("If true, particle systems will be automatically played when emission rate > 0.")]
    [SerializeField] private bool autoPlayWhenEmitting = true;

    [Tooltip("If true, particle systems will be stopped when emission rate = 0.")]
    [SerializeField] private bool autoStopWhenNoEmission = false;

    [Tooltip("Optional: force ski spray on whenever grounded, ignoring speed checks (for debugging).")]
    [SerializeField] private bool debugForceSkiSpray;

    [Tooltip("Optional: force pole drag on whenever in contact, ignoring speed checks (for debugging).")]
    [SerializeField] private bool debugForcePoleDrag;

    // Internal state
    private bool _wasRiderGrounded;
    private bool _wasLeftPoleInContact;
    private bool _wasRightPoleInContact;
    private Vector3 _lastVelocity;

    private void Reset()
    {
        if (!skiController)
            skiController = GetComponentInParent<SkiController>();
    }

    private void Awake()
    {
        if (!skiController)
            skiController = GetComponentInParent<SkiController>();
    }

    private void Update()
    {
        if (!skiController || !skiController.isActiveAndEnabled)
            return;

        Vector3 velocity = skiController.Velocity;
        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        // Planar velocity used for skis & poles
        Vector3 velPlane = Vector3.ProjectOnPlane(velocity, groundNormal);
        float planarSpeed = velPlane.magnitude;

        // --- SKI SPRAY (continuous) ---------------------------------------
        UpdateSkiSnow(leftSkiSnow, leftSkiContact, groundNormal, velPlane, planarSpeed);
        UpdateSkiSnow(rightSkiSnow, rightSkiContact, groundNormal, velPlane, planarSpeed);

        // --- BODY IMPACT ---------------------------------------------------
        UpdateBodyImpact(bodyImpactSnow, groundNormal, velocity);

        // --- POLE SNOW (drag + impact, single PS per pole) ----------------
        UpdatePoleSnow(
            leftPoleSnow,
            leftPoleContact,
            ref _wasLeftPoleInContact,
            groundNormal,
            velPlane,
            planarSpeed);

        UpdatePoleSnow(
            rightPoleSnow,
            rightPoleContact,
            ref _wasRightPoleInContact,
            groundNormal,
            velPlane,
            planarSpeed);

        _lastVelocity = velocity;
        _wasRiderGrounded = skiController.IsRiderGrounded;
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private void ApplyRateAndPlayback(ParticleSystem ps, float rate)
    {
        if (ps == null)
            return;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;

        if (autoPlayWhenEmitting && rate > 0f && !ps.isPlaying)
            ps.Play();

        if (autoStopWhenNoEmission && rate <= 0f && ps.isPlaying)
            ps.Stop();
    }

    // ----------------------------------------------------------------------
    // SKI SPRAY
    // ----------------------------------------------------------------------

    private void UpdateSkiSnow(
        ParticleSystem ps,
        SkiContact contact,
        Vector3 groundNormal,
        Vector3 velPlane,
        float planarSpeed)
    {
        if (ps == null)
            return;

        if (contact == null)
        {
            ApplyRateAndPlayback(ps, 0f);
            return;
        }

        bool grounded = contact.IsGrounded;

        // Simple early-out: no contact → no spray
        if (!grounded)
        {
            ApplyRateAndPlayback(ps, 0f);
            return;
        }

        // For debugging, you can force spray whenever grounded
        if (debugForceSkiSpray)
        {
            ApplyRateAndPlayback(ps, skiBaseRateOverTime * 0.5f);
            return;
        }

        if (planarSpeed < minSkiSpeedForSnow)
        {
            ApplyRateAndPlayback(ps, 0f);
            return;
        }

        // Base alignment factor 0..1 (how much we're on the flat base vs side/tip)
        float rawBaseAlign = Mathf.Clamp01(contact.BaseContactAlignment * 0.5f + 0.5f); // map [-1..1] -> [0..1]
        float baseAlignFactor = Mathf.Lerp(1f, rawBaseAlign, baseAlignmentInfluence);

        // Ski local directions projected onto ground plane
        Vector3 skiForward = contact.GetForwardOnPlane(groundNormal);
        Vector3 skiRight = Vector3.Cross(groundNormal, skiForward);
        if (skiRight.sqrMagnitude > 0.0001f)
            skiRight.Normalize();

        float sidewaysSpeed = 0f;
        if (skiRight.sqrMagnitude > 0.0001f)
            sidewaysSpeed = Mathf.Abs(Vector3.Dot(velPlane, skiRight));

        float speedFactor = Mathf.InverseLerp(minSkiSpeedForSnow, maxSkiSpeedForMaxSnow, planarSpeed);
        float carveFactor = skiSidewaysSpeedForMaxCarve > 0.01f
            ? Mathf.InverseLerp(0f, skiSidewaysSpeedForMaxCarve, sidewaysSpeed)
            : 0f;

        // Combine factors
        float emissionFactor =
            speedFactor *
            Mathf.Lerp(0.4f, 1f, carveFactor) *
            baseAlignFactor;

        emissionFactor = Mathf.Clamp01(emissionFactor);

        float rate = skiBaseRateOverTime * emissionFactor;
        ApplyRateAndPlayback(ps, rate);
    }

    // ----------------------------------------------------------------------
    // BODY IMPACT
    // ----------------------------------------------------------------------

    private void UpdateBodyImpact(
        ParticleSystem ps,
        Vector3 groundNormal,
        Vector3 velocity)
    {
        if (ps == null)
            return;

        bool groundedNow = skiController.IsRiderGrounded;

        // Only care about air -> ground transitions
        if (groundedNow && !_wasRiderGrounded)
        {
            // Use last frame's velocity to estimate pre-impact downward speed.
            float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(_lastVelocity, groundNormal));

            if (downwardSpeed >= minBodyImpactDownwardSpeed)
            {
                float t = Mathf.InverseLerp(
                    minBodyImpactDownwardSpeed,
                    minBodyImpactDownwardSpeed * 4f,
                    downwardSpeed);

                int count = Mathf.RoundToInt(
                    Mathf.Lerp(bodyImpactBurstCountLow, bodyImpactBurstCountHigh, t));

                if (count > 0)
                    ps.Emit(count);
            }
        }
    }

    // ----------------------------------------------------------------------
    // POLE SNOW (drag + impact in one PS)
    // ----------------------------------------------------------------------

    private void UpdatePoleSnow(
        ParticleSystem ps,
        PoleContact contact,
        ref bool wasInContact,
        Vector3 groundNormal,
        Vector3 velPlane,
        float planarSpeed)
    {
        if (ps == null)
            return;

        if (contact == null)
        {
            ApplyRateAndPlayback(ps, 0f);
            wasInContact = false;
            return;
        }

        bool contactNow = contact.IsInContact;
        bool riderGrounded = skiController.IsRiderGrounded;

        // --- Drag: continuous emission while dragging ---------------------

        // For now, define drag purely as "tip in contact + some speed".
        // This makes the effect easy to see; we can add stroke-phase nuances later.
        bool isDragging =
            contactNow &&
            riderGrounded &&
            (debugForcePoleDrag || planarSpeed >= poleMinSpeedForDrag);

        float dragRate = 0f;

        if (isDragging)
        {
            float speedFactor = Mathf.InverseLerp(poleMinSpeedForDrag, poleMaxSpeedForDrag, planarSpeed);
            dragRate = poleDragBaseRate * speedFactor;
        }

        ApplyRateAndPlayback(ps, dragRate);

        // --- Impact: burst on first contact -------------------------------

        if (contactNow && !wasInContact)
        {
            // Approximate downward speed of the tip using skier velocity
            // relative to the contact normal.
            Vector3 contactNormal = contact.ContactNormal.sqrMagnitude > 0.0001f
                ? contact.ContactNormal.normalized
                : groundNormal;

            float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(_lastVelocity, contactNormal));

            if (downwardSpeed >= poleMinImpactDownwardSpeed)
            {
                float t = Mathf.InverseLerp(
                    poleMinImpactDownwardSpeed,
                    poleMinImpactDownwardSpeed * 3f,
                    downwardSpeed);

                int count = Mathf.RoundToInt(
                    Mathf.Lerp(poleImpactBurstCountLow, poleImpactBurstCountHigh, t));

                if (count > 0)
                    ps.Emit(count);
            }
        }

        wasInContact = contactNow;
    }
}
