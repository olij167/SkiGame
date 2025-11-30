using UnityEngine;

/// <summary>
/// Per-pole helper used to:
/// 1) Detect ground contact (via raycast) for visuals / context.
/// 2) Procedurally animate the pole around a root pivot based on SkiController
///    stroke phase (idle / entry / drag / follow-through) and carving state.
/// Attach this to the TIP transform of each pole.
/// Assign:
/// - skiController: the SkiController on the rider.
/// - poleRoot: the transform near the top/handle of this pole (pivot point).
/// </summary>
[DisallowMultipleComponent]
public class PoleContact : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("True if this is the left pole; false for right. Used to decide turn 'inside' vs 'outside'.")]
    public bool isLeftPole = true;

    [Header("Contact Settings")]
    [Tooltip("Layers considered as snow / ground for pole contact.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Length of the contact ray cast from the pole tip along -transform.up.")]
    [SerializeField] private float rayLength = 1.5f;

    [Tooltip("Small offset applied along the hit normal when storing ContactPoint, so the tip doesn't clip into the surface visually.")]
    [SerializeField] private float contactOffset = 0.02f;

    [Tooltip("How quickly the stored contact normal lerps toward the latest hit normal.")]
    [SerializeField] private float contactNormalLerpSpeed = 25f;

    [Header("Visuals / Stroke Poses")]
    [Tooltip("SkiController used as the source of pole stroke phase and movement context.")]
    [SerializeField] private SkiController skiController;

    [Tooltip("Root/pivot transform for this pole (near the top/handle). This transform will be rotated for the pole pose.")]
    [SerializeField] private Transform poleRoot;

    [Tooltip("Baseline pitch angle (around local X) in idle stance (negative = pointing slightly back).")]
    [SerializeField] private float idlePitch = -20f;

    [Tooltip("Pitch angle in the entry phase (poles swing forward/down toward the snow).")]
    [SerializeField] private float entryPitch = 0f;

    [Tooltip("Pitch angle while the poles are dug in and dragging.")]
    [SerializeField] private float dragPitch = -55f;

    [Tooltip("Pitch angle at the end of the follow-through (tips behind the rider).")]
    [SerializeField] private float followPitch = 20f;

    [Tooltip("Baseline side tilt (deg around local Z) in idle stance.")]
    [SerializeField] private float idleTilt = 0f;

    [Tooltip("Side tilt (deg) in the entry phase.")]
    [SerializeField] private float entryTilt = 0f;

    [Tooltip("Side tilt (deg) while the poles are dug in and dragging (oar-like outward tilt).")]
    [SerializeField] private float dragTilt = 12f;

    [Tooltip("Side tilt (deg) at the end of follow-through.")]
    [SerializeField] private float followTilt = 4f;

    [Header("Carve Adjustments")]
    [Tooltip("Minimum planar speed at which carve-based pole adjustments start being visible.")]
    [SerializeField] private float carveMinSpeed = 3f;

    [Tooltip("Planar speed at which carve-based pole adjustments reach full strength.")]
    [SerializeField] private float carveMaxSpeed = 20f;

    [Tooltip("Extra downward pitch (deg, negative) applied to the INSIDE pole while carving in drag phase.")]
    [SerializeField] private float insideCarvePitchDelta = -12f;

    [Tooltip("Upward pitch (deg, positive) applied to the OUTSIDE pole while carving in drag phase.")]
    [SerializeField] private float outsideCarvePitchDelta = 8f;

    [Tooltip("Additional side tilt (deg) for the INSIDE pole when carving in drag phase.")]
    [SerializeField] private float insideCarveTiltDelta = 8f;

    [Tooltip("Additional side tilt (deg) for the OUTSIDE pole when carving in drag phase.")]
    [SerializeField] private float outsideCarveTiltDelta = 4f;

    [Tooltip("Additional side tilt (deg around local Z) for the OUTSIDE pole when carving.")]
    [SerializeField] private float outsideCarveTilt = 4f;

    [Header("Slope / Movement Influence")]
    [Tooltip("Maximum extra pitch (deg) added on very steep slopes to point poles more downhill.")]
    [SerializeField] private float maxSlopePitchOffset = 10f;

    [Header("Pose Transition Speeds")]
    [Tooltip("Default blend speed for poses when the stroke phase is not changing.")]
    [SerializeField] private float poseLerpSpeed = 12f;

    [Tooltip("Blend speed used when going from Idle to Entry (lifting poles forward).")]
    [SerializeField] private float idleToEntryLerpSpeed = 14f;

    [Tooltip("Blend speed used when going from Entry to Drag (digging poles into the snow).")]
    [SerializeField] private float entryToDragLerpSpeed = 18f;

    [Tooltip("Blend speed used when going from Drag to FollowThrough.")]
    [SerializeField] private float dragToFollowLerpSpeed = 12f;

    [Tooltip("Blend speed used when going from FollowThrough back to Idle.")]
    [SerializeField] private float followToIdleLerpSpeed = 10f;

    // Public read-only contact data
    public bool IsInContact { get; private set; }
    public Vector3 ContactPoint { get; private set; }
    public Vector3 ContactNormal { get; private set; } = Vector3.up;
    public float ContactDistance { get; private set; }

    // Cached base rotation for the pole pivot
    private Quaternion _baseLocalRot;

    // Last stroke phase seen (for picking transition speeds)
    private SkiController.PoleStrokePhase _lastPhase = SkiController.PoleStrokePhase.Idle;

    private void Reset()
    {
        groundMask = Physics.DefaultRaycastLayers;
        rayLength = 1.5f;
        contactOffset = 0.02f;
        contactNormalLerpSpeed = 25f;

        // Angles: idle slightly back, entry lifted/forward, drag dug in, follow slightly behind.
        idlePitch = -20f;
        entryPitch = 0f;    // raised higher than idle for the forward "lift"
        dragPitch = -55f;  // dug into the snow
        followPitch = 20f;

        idleTilt = 0f;
        entryTilt = 0f;
        dragTilt = 12f;   // outward, like oars
        followTilt = 4f;

        carveMinSpeed = 3f;
        carveMaxSpeed = 20f;
        insideCarvePitchDelta = -12f;
        outsideCarvePitchDelta = 8f;
        insideCarveTiltDelta = 8f;
        outsideCarveTiltDelta = 4f;

        maxSlopePitchOffset = 10f;

        // Default blend speeds
        poseLerpSpeed = 12f;
        idleToEntryLerpSpeed = 14f;
        entryToDragLerpSpeed = 18f;
        dragToFollowLerpSpeed = 12f;
        followToIdleLerpSpeed = 10f;
    }

    private void Awake()
    {
        if (poleRoot != null)
        {
            _baseLocalRot = poleRoot.localRotation;
        }
    }

    private void FixedUpdate()
    {
        UpdateContact();
    }

    private void LateUpdate()
    {
        if (skiController == null || poleRoot == null)
            return;

        PosePole();
    }

    // ----------------------------------------------------------------------
    // CONTACT
    // ----------------------------------------------------------------------

    /// <summary>
    /// Performs the contact raycast and updates the cached contact state.
    /// </summary>
    public void UpdateContact()
    {
        Vector3 origin = transform.position;
        Vector3 dir = -transform.up;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            IsInContact = true;
            ContactDistance = hit.distance;

            ContactPoint = hit.point + hit.normal * contactOffset;
            ContactNormal = Vector3.Slerp(ContactNormal, hit.normal, contactNormalLerpSpeed * Time.fixedDeltaTime);
        }
        else
        {
            IsInContact = false;
            ContactDistance = rayLength;

            ContactPoint = origin + dir * rayLength;
            ContactNormal = Vector3.Slerp(ContactNormal, Vector3.up, contactNormalLerpSpeed * Time.fixedDeltaTime);
        }
    }

    // ----------------------------------------------------------------------
    // VISUAL POSE
    // ----------------------------------------------------------------------
    private void PosePole()
    {
        if (poleRoot == null || skiController == null)
            return;

        float dt = Time.deltaTime;

        var phase = skiController.CurrentPolePhase;
        float phaseTime = skiController.CurrentPolePhaseTime;
        bool grounded = skiController.IsRiderGrounded;

        // Determine slope information (how steep & direction).
        Vector3 groundNormal = skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;

        float slopeSteepness = Vector3.Angle(groundNormal, Vector3.up) / 90f; // 0 flat -> 1 very steep
        float slopePitchExtra = slopeSteepness * maxSlopePitchOffset;

        // Compute planar velocity & ski direction for carving context.
        Vector3 velPlane = Vector3.ProjectOnPlane(skiController.Velocity, groundNormal);
        float planeSpeed = velPlane.magnitude;

        Vector3 skiDirPlane = Vector3.ProjectOnPlane(skiController.SkiForwardOnPlane, groundNormal);
        if (skiDirPlane.sqrMagnitude < 0.0001f)
        {
            skiDirPlane = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        }

        float pivotSide = 0f; // -1 = carving left, +1 = carving right
        if (planeSpeed > 0.5f && skiDirPlane.sqrMagnitude > 0.0001f)
        {
            pivotSide = Mathf.Clamp(
                Vector3.SignedAngle(skiDirPlane.normalized, velPlane.normalized, groundNormal) / 45f,
                -1f, 1f);
        }

        bool leftInside = pivotSide < 0f;
        bool rightInside = pivotSide > 0f;
        bool isInside = isLeftPole ? leftInside : rightInside;
        float carveWeight = Mathf.Clamp01(Mathf.Abs(pivotSide));

        // Speed factor for carve intensity.
        float carveSpeedFactor = 0f;
        if (carveMaxSpeed > carveMinSpeed)
        {
            carveSpeedFactor = Mathf.Clamp01(
                Mathf.InverseLerp(carveMinSpeed, carveMaxSpeed, planeSpeed));
        }

        float carveIntensity = carveWeight * carveSpeedFactor;

        // ------------------------------------------------------------------
        // 1. Base pitch & tilt from stroke phase (no carve yet)
        // ------------------------------------------------------------------
        float pitch;
        float tilt;

        if (!grounded)
        {
            // In the air: blend between idle and follow-through based on speed.
            float tSpeed = Mathf.Clamp01(planeSpeed / 10f);
            pitch = Mathf.Lerp(idlePitch, followPitch, tSpeed);
            tilt = Mathf.Lerp(idleTilt, followTilt, tSpeed);
        }
        else
        {
            switch (phase)
            {
                case SkiController.PoleStrokePhase.Entry:
                    {
                        float entryDuration = Mathf.Max(skiController.PoleEntryDuration, 0.0001f);
                        float t = Mathf.Clamp01(phaseTime / entryDuration);
                        pitch = Mathf.Lerp(idlePitch, entryPitch, t);
                        tilt = Mathf.Lerp(idleTilt, entryTilt, t);
                        break;
                    }

                case SkiController.PoleStrokePhase.Drag:
                    pitch = dragPitch;
                    tilt = isLeftPole ? +dragTilt : -dragTilt; // outward, like oars
                    break;

                case SkiController.PoleStrokePhase.FollowThrough:
                    {
                        float followDuration = Mathf.Max(skiController.PoleFollowThroughDuration, 0.0001f);
                        float t = Mathf.Clamp01(phaseTime / followDuration);
                        pitch = Mathf.Lerp(dragPitch, followPitch, t);
                        tilt = Mathf.Lerp(dragTilt, followTilt, t) * (isLeftPole ? 1f : -1f);
                        break;
                    }

                case SkiController.PoleStrokePhase.Idle:
                default:
                    pitch = idlePitch;
                    tilt = idleTilt;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // 2. Add slope influence (on steeper slopes poles point more downhill)
        // ------------------------------------------------------------------
        pitch += slopePitchExtra;

        // ------------------------------------------------------------------
        // 3. Carve adjustments: only during drag, layered on top of base drag pose
        // ------------------------------------------------------------------
        if (grounded && phase == SkiController.PoleStrokePhase.Drag && carveIntensity > 0.01f)
        {
            float outwardSign = isLeftPole ? 1f : -1f;

            if (isInside)
            {
                // Inside pole digs in more.
                pitch += insideCarvePitchDelta * carveIntensity;
                tilt += outwardSign * insideCarveTiltDelta * carveIntensity;
            }
            else
            {
                // Outside pole lifts away from the snow a bit.
                pitch += outsideCarvePitchDelta * carveIntensity;
                tilt += outwardSign * outsideCarveTiltDelta * carveIntensity;
            }
        }

        // ------------------------------------------------------------------
        // 4. Build final rotation + apply transition-specific lerp speed
        // ------------------------------------------------------------------
        Quaternion pitchRot = Quaternion.AngleAxis(pitch, Vector3.right);
        Quaternion sideRot = Quaternion.AngleAxis(tilt, Vector3.forward);

        Quaternion target = _baseLocalRot * sideRot * pitchRot;

        // Choose lerp speed based on phase transition.
        float lerpSpeed = poseLerpSpeed;

        if (phase != _lastPhase)
        {
            if (_lastPhase == SkiController.PoleStrokePhase.Idle &&
                phase == SkiController.PoleStrokePhase.Entry)
            {
                lerpSpeed = idleToEntryLerpSpeed;
            }
            else if (_lastPhase == SkiController.PoleStrokePhase.Entry &&
                     phase == SkiController.PoleStrokePhase.Drag)
            {
                lerpSpeed = entryToDragLerpSpeed;
            }
            else if (_lastPhase == SkiController.PoleStrokePhase.Drag &&
                     phase == SkiController.PoleStrokePhase.FollowThrough)
            {
                lerpSpeed = dragToFollowLerpSpeed;
            }
            else if (_lastPhase == SkiController.PoleStrokePhase.FollowThrough &&
                     phase == SkiController.PoleStrokePhase.Idle)
            {
                lerpSpeed = followToIdleLerpSpeed;
            }
        }

        poleRoot.localRotation = Quaternion.Slerp(
            poleRoot.localRotation,
            target,
            lerpSpeed * dt
        );

        _lastPhase = phase;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Color c = isLeftPole ? Color.cyan : Color.magenta;
        Gizmos.color = c;

        Vector3 origin = transform.position;
        Vector3 dir = -transform.up;

        Gizmos.DrawLine(origin, origin + dir * rayLength);

        if (IsInContact)
        {
            Gizmos.DrawSphere(ContactPoint, 0.02f);
            Gizmos.DrawRay(ContactPoint, ContactNormal * 0.2f);
        }
    }
#endif
}
