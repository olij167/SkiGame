using UnityEngine;

/// <summary>
/// Minimal, probe-bound snow VFX driver.
///
/// Goals:
/// - Keep code small/clean (no legacy list/single-system fallbacks).
/// - Each ski can drive three ParticleSystems (Front/Mid/Rear) that are positioned to the SkiContact
///   probe hits and oriented to spray up and back relative to motion, with a slight outward bias.
/// - Impact particles fire on air->ground transitions and are oriented based on pre-impact velocity.
/// - Continuous ski and pole particles are suppressed while walking and while attached to a lift.
///   (Impact particles remain enabled; lift drip is optional.)
/// </summary>
[DisallowMultipleComponent]
public class SkiSnowParticles : MonoBehaviour
{
    // ----------------------------------------------------------------------
    // References
    // ----------------------------------------------------------------------

    [Header("Core References")]
    [Tooltip("If assigned/enabled, provides velocity and ground normal.")]
    [SerializeField] private SkiController skiController;

    [Tooltip("Optional: used to suppress continuous particles while walking.")]
    [SerializeField] private WalkingController walkingController;

    [Tooltip("Optional: used to suppress continuous particles while riding lifts.")]
    [SerializeField] private LiftRider liftRider;

    [Tooltip("If SkiController is disabled (walking), velocity is read from this Rigidbody.")]
    [SerializeField] private Rigidbody fallbackRigidbody;

    [SerializeField] private SkiContact leftSkiContact;
    [SerializeField] private SkiContact rightSkiContact;

    [SerializeField] private PoleContact leftPoleContact;
    [SerializeField] private PoleContact rightPoleContact;

    // ----------------------------------------------------------------------
    // Probe-bound ski spray
    // ----------------------------------------------------------------------

    [System.Serializable]
    private class SkiProbeEmitterSet
    {
        [Header("Front / Mid / Rear")]
        [Tooltip("Front (tip) spray system. Moved to the FRONT probe contact point.")]
        public ParticleSystem front;

        [Tooltip("Mid (binding/base) spray system. Moved to the MID probe contact point.")]
        public ParticleSystem mid;

        [Tooltip("Rear (tail) spray system. Moved to the REAR probe contact point.")]
        public ParticleSystem rear;

        [Header("Per-emitter intensity")]
        [Range(0f, 3f)] public float frontRateMultiplier = 1f;
        [Range(0f, 3f)] public float midRateMultiplier = 1f;
        [Range(0f, 3f)] public float rearRateMultiplier = 1f;

        public bool HasAny() => front != null || mid != null || rear != null;
    }

    [Header("Ski Spray Emitters (probe-bound)")]
    [SerializeField] private SkiProbeEmitterSet leftSki = new SkiProbeEmitterSet();
    [SerializeField] private SkiProbeEmitterSet rightSki = new SkiProbeEmitterSet();

    [Tooltip("Small offset along the probe normal to keep emitters above the surface.")]
    [SerializeField] private float probeSurfaceOffset = 0.02f;

    [Header("Ski Spray Intensity")]
    [Tooltip("Minimum planar speed before continuous ski spray starts.")]
    [SerializeField] private float minSkiSpeedForSnow = 2f;

    [Tooltip("Planar speed at which ski spray reaches full strength.")]
    [SerializeField] private float maxSkiSpeedForMaxSnow = 20f;

    [Tooltip("Base emission rate (rateOverTime) when speed + carve + alignment are maxed.")]
    [SerializeField] private float skiBaseRateOverTime = 150f;

    [Tooltip("How strongly base contact alignment modulates spray (0 = ignore, 1 = full influence).")]
    [SerializeField, Range(0f, 1f)] private float baseAlignmentInfluence = 0.6f;

    [Tooltip("Sideways speed (m/s) relative to ski direction needed for full carve spray.")]
    [SerializeField] private float skiSidewaysSpeedForMaxCarve = 6f;

    [Header("Ski Spray Direction")]
    [Tooltip("Spray direction bias along the surface normal.")]
    [SerializeField] private float sprayUpBias = 1.0f;

    [Tooltip("Spray direction bias opposite planar movement direction.")]
    [SerializeField] private float sprayBackBias = 1.15f;

    [Tooltip("Spray direction bias to the outside of each ski.")]
    [SerializeField] private float sprayOutBias = 0.25f;

    [Tooltip("How quickly emitter transforms rotate toward their desired spray rotation.")]
    [SerializeField] private float sprayRotationLerpSpeed = 18f;

    // ----------------------------------------------------------------------
    // Body impact
    // ----------------------------------------------------------------------

    [Header("Body Impact (single system)")]
    [Tooltip("Single impact particle system fired on hard landings.")]
    [SerializeField] private ParticleSystem bodyImpactSnow;

    [Tooltip("Minimum downward speed along the ground normal to trigger a body impact burst.")]
    [SerializeField] private float minBodyImpactDownwardSpeed = 5f;

    [Tooltip("Burst particle count for a softer landing.")]
    [SerializeField] private int bodyImpactBurstCountLow = 50;

    [Tooltip("Burst particle count for a heavy landing.")]
    [SerializeField] private int bodyImpactBurstCountHigh = 150;

    [Tooltip("How strongly the impact burst direction should bias up (normal) vs away from motion.")]
    [SerializeField] private float impactUpBias = 1.0f;

    [Tooltip("How strongly the impact burst direction should bias away from the incoming planar motion.")]
    [SerializeField] private float impactBackBias = 1.2f;

    // ----------------------------------------------------------------------
    // Pole snow (drag + impact)
    // ----------------------------------------------------------------------

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

    [Header("Pole Direction")]
    [Tooltip("How quickly pole particle transforms rotate toward desired drag direction.")]
    [SerializeField] private float poleRotationLerpSpeed = 18f;

    // ----------------------------------------------------------------------
    // Lift drip (optional)
    // ----------------------------------------------------------------------

    [Header("Lift Drip (optional)")]
    [Tooltip("If enabled, while attached to a lift we can intermittently emit a tiny amount of ski snow.")]
    [SerializeField] private bool enableLiftDrip = false;

    [Tooltip("Rate scale applied while on lift (relative to computed base rate).")]
    [SerializeField, Range(0f, 1f)] private float liftDripRateScale = 0.03f;

    [Tooltip("Pulse frequency (Hz) for drip while on lift. 0 = constant.")]
    [SerializeField] private float liftDripPulseHz = 0.35f;

    // ----------------------------------------------------------------------
    // Behaviour
    // ----------------------------------------------------------------------

    [Header("Behaviour")]
    [Tooltip("If true, particle systems will be automatically played when emission rate > 0.")]
    [SerializeField] private bool autoPlayWhenEmitting = true;

    [Tooltip("If true, particle systems will be stopped when emission rate = 0.")]
    [SerializeField] private bool autoStopWhenNoEmission = false;

    [Tooltip("Optional: force ski spray on whenever grounded, ignoring speed checks (debug).")]
    [SerializeField] private bool debugForceSkiSpray;

    [Tooltip("Optional: force pole drag on whenever in contact, ignoring speed checks (debug).")]
    [SerializeField] private bool debugForcePoleDrag;

    [Header("Walking / Fallback Grounding")]
    [Tooltip("If SkiController is disabled, we use this spherecast to detect ground for impact particles.")]
    [SerializeField] private float fallbackGroundCheckDistance = 0.6f;

    [Tooltip("Sphere radius used for the fallback grounded check.")]
    [SerializeField] private float fallbackGroundCheckRadius = 0.25f;

    [Tooltip("Layers considered ground for the fallback grounded check.")]
    [SerializeField] private LayerMask fallbackGroundLayers = ~0;

    // Internal state
    private bool _wasGrounded;
    private bool _wasLeftPoleInContact;
    private bool _wasRightPoleInContact;
    private Vector3 _lastVelocity;

    private void Reset()
    {
        if (!skiController)
            skiController = GetComponentInParent<SkiController>();

        if (!fallbackRigidbody)
            fallbackRigidbody = GetComponentInParent<Rigidbody>();

        if (!walkingController)
            walkingController = GetComponentInParent<WalkingController>();

        if (!liftRider)
            liftRider = GetComponentInParent<LiftRider>();
    }

    private void Awake()
    {
        if (!skiController)
            skiController = GetComponentInParent<SkiController>();

        if (!fallbackRigidbody)
            fallbackRigidbody = GetComponentInParent<Rigidbody>();

        if (!walkingController)
            walkingController = GetComponentInParent<WalkingController>();

        if (!liftRider)
            liftRider = GetComponentInParent<LiftRider>();
    }

    private void Update()
    {
        // We deliberately run even if SkiController is disabled (walking), because impact particles should still work.

        bool isWalking = (walkingController != null) && walkingController.IsWalkingMode;
        bool isOnLift = (liftRider != null) && liftRider.IsAttached;

        bool canUseSkiController = (skiController != null) && skiController.enabled;

        Vector3 velocity;
        Vector3 groundNormal;
        bool groundedNow;

        if (canUseSkiController)
        {
            velocity = skiController.Velocity;
            groundNormal = (skiController.GroundNormal.sqrMagnitude > 0.0001f) ? skiController.GroundNormal.normalized : Vector3.up;
            groundedNow = skiController.IsRiderGrounded;

            // While riding a chair, LiftRider disables SkiController and makes RB kinematic.
            // For a T-bar you are still skiing, but we still treat you as "on lift" for VFX suppression.
            if (isOnLift && liftRider.IsChairMode)
                groundedNow = false;
        }
        else
        {
            velocity = (fallbackRigidbody != null) ? fallbackRigidbody.linearVelocity : Vector3.zero;

            groundedNow = FallbackGroundSample(out groundNormal);

            // If we are parented to a chair and kinematic, avoid fake "grounded" transitions.
            if (isOnLift && (liftRider == null || liftRider.IsChairMode))
                groundedNow = false;
        }

        // Planar velocity used for skis & poles
        Vector3 velPlane = Vector3.ProjectOnPlane(velocity, groundNormal);
        float planarSpeed = velPlane.magnitude;

        // Continuous emission is suppressed while walking and while on lifts.
        bool allowContinuous = !isWalking && !isOnLift;
        bool allowLiftDrip = !allowContinuous && isOnLift && enableLiftDrip;

        // --- SKI SPRAY (continuous, probe-bound) -------------------------
        if (allowContinuous || allowLiftDrip)
        {
            float leftRate = ComputeSkiSnowRate(leftSkiContact, groundNormal, velPlane, planarSpeed);
            float rightRate = ComputeSkiSnowRate(rightSkiContact, groundNormal, velPlane, planarSpeed);

            if (allowLiftDrip)
            {
                float pulse = (liftDripPulseHz <= 0.0001f) ? 1f : (0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * liftDripPulseHz));
                float dripScale = liftDripRateScale * pulse;
                leftRate *= dripScale;
                rightRate *= dripScale;
            }

            ApplyProbeBoundSkiSpray(leftSkiContact, leftSki, leftRate, velPlane, groundNormal);
            ApplyProbeBoundSkiSpray(rightSkiContact, rightSki, rightRate, velPlane, groundNormal);
        }
        else
        {
            StopProbeSet(leftSki);
            StopProbeSet(rightSki);
        }

        // --- BODY IMPACT (always allowed, including while walking) --------
        UpdateBodyImpact(bodyImpactSnow, groundNormal, velPlane, groundedNow);

        // --- POLE SNOW (drag + impact, suppressed while walking / on lift) -
        if (allowContinuous)
        {
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
        }
        else
        {
            ApplyRateAndPlayback(leftPoleSnow, 0f);
            ApplyRateAndPlayback(rightPoleSnow, 0f);
            _wasLeftPoleInContact = false;
            _wasRightPoleInContact = false;
        }

        _lastVelocity = velocity;
        _wasGrounded = groundedNow;
    }

    // ----------------------------------------------------------------------
    // Ski spray
    // ----------------------------------------------------------------------

    private float ComputeSkiSnowRate(
        SkiContact contact,
        Vector3 groundNormal,
        Vector3 velPlane,
        float planarSpeed)
    {
        if (contact == null || !contact.IsGrounded)
            return 0f;

        if (debugForceSkiSpray)
            return skiBaseRateOverTime * 0.5f;

        if (planarSpeed < minSkiSpeedForSnow)
            return 0f;

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
        float carveFactor = (skiSidewaysSpeedForMaxCarve > 0.01f)
            ? Mathf.InverseLerp(0f, skiSidewaysSpeedForMaxCarve, sidewaysSpeed)
            : 0f;

        float emissionFactor =
            speedFactor *
            Mathf.Lerp(0.4f, 1f, carveFactor) *
            baseAlignFactor;

        return skiBaseRateOverTime * Mathf.Clamp01(emissionFactor);
    }

    private void ApplyProbeBoundSkiSpray(
        SkiContact contact,
        SkiProbeEmitterSet set,
        float baseRate,
        Vector3 velPlane,
        Vector3 groundNormal)
    {
        if (contact == null || set == null || !set.HasAny())
        {
            StopProbeSet(set);
            return;
        }

        // Probe hits per region
        bool hasFront = contact.TryGetProbeContact(SkiContact.SkiProbeRegion.Front, out Vector3 frontP, out Vector3 frontN);
        bool hasMid = contact.TryGetProbeContact(SkiContact.SkiProbeRegion.Mid, out Vector3 midP, out Vector3 midN);
        bool hasRear = contact.TryGetProbeContact(SkiContact.SkiProbeRegion.Rear, out Vector3 rearP, out Vector3 rearN);

        if (!hasFront && !hasMid && !hasRear)
        {
            StopProbeSet(set);
            return;
        }

        // Simple weight distribution across grounded probes.
        float wFront = hasFront ? 0.33f : 0f;
        float wMid = hasMid ? 0.34f : 0f;
        float wRear = hasRear ? 0.33f : 0f;

        float sum = wFront + wMid + wRear;
        if (sum > 0.0001f)
        {
            wFront /= sum;
            wMid /= sum;
            wRear /= sum;
        }

        // Move and drive each emitter.
        DriveProbeEmitter(contact, set.front, hasFront, frontP, frontN, baseRate * wFront * Mathf.Max(0f, set.frontRateMultiplier), velPlane, groundNormal);
        DriveProbeEmitter(contact, set.mid, hasMid, midP, midN, baseRate * wMid * Mathf.Max(0f, set.midRateMultiplier), velPlane, groundNormal);
        DriveProbeEmitter(contact, set.rear, hasRear, rearP, rearN, baseRate * wRear * Mathf.Max(0f, set.rearRateMultiplier), velPlane, groundNormal);
    }

    private void DriveProbeEmitter(
        SkiContact contact,
        ParticleSystem ps,
        bool hasHit,
        Vector3 point,
        Vector3 normal,
        float rate,
        Vector3 velPlane,
        Vector3 groundNormal)
    {
        if (ps == null)
            return;

        if (!hasHit || rate <= 0f)
        {
            ApplyRateAndPlayback(ps, 0f);
            return;
        }

        Vector3 n = (normal.sqrMagnitude > 0.0001f) ? normal.normalized : ((groundNormal.sqrMagnitude > 0.0001f) ? groundNormal.normalized : Vector3.up);

        // Position on the probe contact, slightly above surface.
        ps.transform.position = point + n * probeSurfaceOffset;

        // Compute desired spray direction.
        Quaternion desiredRot = ComputeSkiSprayRotation(contact, n, velPlane, groundNormal);
        ps.transform.rotation = SmoothRotate(ps.transform.rotation, desiredRot, sprayRotationLerpSpeed);

        ApplyRateAndPlayback(ps, rate);
    }

    private Quaternion ComputeSkiSprayRotation(
        SkiContact contact,
        Vector3 surfaceNormal,
        Vector3 velPlane,
        Vector3 groundNormal)
    {
        Vector3 n = (surfaceNormal.sqrMagnitude > 0.0001f) ? surfaceNormal.normalized : Vector3.up;

        Vector3 velDir = velPlane;
        if (velDir.sqrMagnitude < 0.0001f)
        {
            // If nearly stopped, aim spray "back" relative to ski forward.
            velDir = contact != null ? contact.GetForwardOnPlane(groundNormal) : Vector3.forward;
        }
        velDir = velDir.normalized;

        // Outward direction (away from body center) based on ski right.
        Vector3 skiForward = (contact != null) ? contact.GetForwardOnPlane(groundNormal) : Vector3.forward;
        Vector3 skiRight = Vector3.Cross(groundNormal, skiForward);
        if (skiRight.sqrMagnitude < 0.0001f)
            skiRight = Vector3.right;
        skiRight.Normalize();

        float outwardSign = (contact != null && contact.isLeftSki) ? -1f : 1f;
        Vector3 outward = skiRight * outwardSign;

        Vector3 dir =
            (n * sprayUpBias) +
            (-velDir * sprayBackBias) +
            (outward * sprayOutBias);

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.ProjectOnPlane(-velDir, n);

        dir.Normalize();

        // Use surface normal as the "up" vector to keep emitters stable on slopes.
        if (Vector3.Cross(dir, n).sqrMagnitude < 0.0001f)
        {
            // If dir ~ parallel to n, choose a fallback perpendicular.
            dir = Vector3.ProjectOnPlane(-velDir, n);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.ProjectOnPlane(Vector3.forward, n);
            dir.Normalize();
        }

        return Quaternion.LookRotation(dir, n);
    }

    private void StopProbeSet(SkiProbeEmitterSet set)
    {
        if (set == null) return;
        ApplyRateAndPlayback(set.front, 0f);
        ApplyRateAndPlayback(set.mid, 0f);
        ApplyRateAndPlayback(set.rear, 0f);
    }

    // ----------------------------------------------------------------------
    // Body impact
    // ----------------------------------------------------------------------

    private void UpdateBodyImpact(
        ParticleSystem ps,
        Vector3 groundNormal,
        Vector3 velPlane,
        bool groundedNow)
    {
        if (ps == null)

            return;

        // Only care about air -> ground transitions.
        if (groundedNow && !_wasGrounded)
        {
            Vector3 n = (groundNormal.sqrMagnitude > 0.0001f) ? groundNormal.normalized : Vector3.up;
            float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(_lastVelocity, n));

            if (downwardSpeed >= minBodyImpactDownwardSpeed)
            {
                // Position the impact close to where skis contacted (if possible).
                Vector3 impactPoint = ResolveImpactPoint(n);
                ps.transform.position = impactPoint + n * probeSurfaceOffset;

                // Orient burst based on pre-impact velocity.
                Quaternion rot = ComputeImpactRotation(n, _lastVelocity, velPlane);
                ps.transform.rotation = rot;

                float t = Mathf.InverseLerp(minBodyImpactDownwardSpeed, minBodyImpactDownwardSpeed * 4f, downwardSpeed);
                int count = Mathf.RoundToInt(Mathf.Lerp(bodyImpactBurstCountLow, bodyImpactBurstCountHigh, t));
                if (count > 0)
                    ps.Emit(count);
            }
        }
    }

    private Vector3 ResolveImpactPoint(Vector3 groundNormal)
    {
        // Prefer mid probes (stable, near bindings). Average if both exist.
        Vector3 lp = default;
        Vector3 rp = default;

        bool gotL = (leftSkiContact != null) &&
                    leftSkiContact.TryGetProbeContact(SkiContact.SkiProbeRegion.Mid, out lp, out _);

        bool gotR = (rightSkiContact != null) &&
                    rightSkiContact.TryGetProbeContact(SkiContact.SkiProbeRegion.Mid, out rp, out _);

        if (gotL && gotR)
            return (lp + rp) * 0.5f;
        if (gotL)
            return lp;
        if (gotR)
            return rp;

        // Fall back to player position projected downward.
        return transform.position;
    }

    private Quaternion ComputeImpactRotation(Vector3 groundNormal, Vector3 preImpactVelocity, Vector3 velPlane)
    {
        Vector3 n = (groundNormal.sqrMagnitude > 0.0001f) ? groundNormal.normalized : Vector3.up;

        Vector3 incomingPlane = Vector3.ProjectOnPlane(preImpactVelocity, n);
        if (incomingPlane.sqrMagnitude < 0.0001f)
            incomingPlane = velPlane;

        Vector3 back = (incomingPlane.sqrMagnitude > 0.0001f) ? (-incomingPlane.normalized) : Vector3.zero;

        Vector3 dir = (n * impactUpBias) + (back * impactBackBias);
        if (dir.sqrMagnitude < 0.0001f)
            dir = n;

        dir.Normalize();

        if (Vector3.Cross(dir, n).sqrMagnitude < 0.0001f)
            return Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, n).normalized, n);

        return Quaternion.LookRotation(dir, n);
    }

    // ----------------------------------------------------------------------
    // Pole snow
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

        // --- Drag: continuous emission while dragging ---------------------
        bool isDragging =
            contactNow &&
            (debugForcePoleDrag || planarSpeed >= poleMinSpeedForDrag);

        float dragRate = 0f;

        if (isDragging)
        {
            float speedFactor = Mathf.InverseLerp(poleMinSpeedForDrag, poleMaxSpeedForDrag, planarSpeed);
            dragRate = poleDragBaseRate * speedFactor;

            // Reposition/orient to the contact point for better context.
            Vector3 n = (contact.ContactNormal.sqrMagnitude > 0.0001f) ? contact.ContactNormal.normalized : groundNormal;
            ps.transform.position = contact.ContactPoint + n * probeSurfaceOffset;

            Vector3 v = velPlane.sqrMagnitude > 0.0001f ? velPlane.normalized : Vector3.forward;
            Vector3 dir = (n * sprayUpBias) + (-v * sprayBackBias);
            if (dir.sqrMagnitude < 0.0001f) dir = n;
            dir.Normalize();

            Quaternion desired = Quaternion.LookRotation(dir, n);
            ps.transform.rotation = SmoothRotate(ps.transform.rotation, desired, poleRotationLerpSpeed);
        }

        ApplyRateAndPlayback(ps, dragRate);

        // --- Impact: burst on first contact -------------------------------
        if (contactNow && !wasInContact)
        {
            Vector3 n = (contact.ContactNormal.sqrMagnitude > 0.0001f) ? contact.ContactNormal.normalized : groundNormal;
            float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(_lastVelocity, n));

            if (downwardSpeed >= poleMinImpactDownwardSpeed)
            {
                ps.transform.position = contact.ContactPoint + n * probeSurfaceOffset;

                float t = Mathf.InverseLerp(poleMinImpactDownwardSpeed, poleMinImpactDownwardSpeed * 3f, downwardSpeed);
                int count = Mathf.RoundToInt(Mathf.Lerp(poleImpactBurstCountLow, poleImpactBurstCountHigh, t));
                if (count > 0)
                    ps.Emit(count);
            }
        }

        wasInContact = contactNow;
    }

    // ----------------------------------------------------------------------
    // Utility
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

    private Quaternion SmoothRotate(Quaternion current, Quaternion target, float lerpSpeed)
    {
        if (lerpSpeed <= 0f)
            return target;

        float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        return Quaternion.Slerp(current, target, t);
    }

    private bool FallbackGroundSample(out Vector3 groundNormal)
    {
        // Lightweight grounded + normal sample for walking mode (or if SkiController is disabled).
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (Physics.SphereCast(
            origin,
            Mathf.Max(0.01f, fallbackGroundCheckRadius),
            Vector3.down,
            out RaycastHit hit,
            Mathf.Max(0.01f, fallbackGroundCheckDistance),
            fallbackGroundLayers,
            QueryTriggerInteraction.Ignore))
        {
            groundNormal = (hit.normal.sqrMagnitude > 0.0001f) ? hit.normal.normalized : Vector3.up;
            return true;
        }

        groundNormal = Vector3.up;
        return false;
    }
}
