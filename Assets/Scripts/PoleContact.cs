using UnityEngine;

/// <summary>
/// Per-pole helper used to:
/// 1) Detect ground contact (via raycast) for visuals / context.
/// 2) Procedurally animate the pole around a root pivot based on SkiController
///    continuous stroke parameter (0..1), slope, carve and speed.
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

    [Header("Visual Stroke Shape")]
    [Tooltip("SkiController used as the source of stroke parameter and movement context.")]
    [SerializeField] private SkiController skiController;

    [Tooltip("Root/pivot transform for this pole (near the top/handle). This transform will be rotated for the pole pose.")]
    [SerializeField] private Transform poleRoot;

    [Tooltip("Normalized stroke value at which entry ends and drag begins (0..1).")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float entryEnd = 0.25f;

    [Tooltip("Normalized stroke value at which drag ends and follow-through begins (entryEnd..1).")]
    [Range(0.3f, 0.95f)]
    [SerializeField] private float dragEnd = 0.7f;

    [Tooltip("Baseline pitch angle (around local X) in idle stance (negative = pointing slightly back).")]
    [SerializeField] private float idlePitch = -20f;

    [Tooltip("Pitch angle in the entry phase (poles swing forward and up).")]
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

    [Tooltip("Extra downward pitch (deg, negative) applied to the INSIDE pole while carving in drag region.")]
    [SerializeField] private float insideCarvePitchDelta = -12f;

    [Tooltip("Upward pitch (deg, positive) applied to the OUTSIDE pole while carving in drag region.")]
    [SerializeField] private float outsideCarvePitchDelta = 8f;

    [Tooltip("Additional side tilt (deg) for the INSIDE pole when carving in drag region.")]
    [SerializeField] private float insideCarveTiltDelta = 8f;

    [Tooltip("Additional side tilt (deg) for the OUTSIDE pole when carving in drag region.")]
    [SerializeField] private float outsideCarveTiltDelta = 4f;

    [Header("Slope / General Visuals")]
    [Tooltip("Maximum extra pitch (deg) added on very steep slopes to point poles more downhill.")]
    [SerializeField] private float maxSlopePitchOffset = 10f;

    [Tooltip("How quickly the pole blends toward its target pose. Timing of the stroke comes from SkiController; this only smooths jitter.")]
    [SerializeField] private float poseLerpSpeed = 12f;

    // Public read-only contact data
    public bool IsInContact { get; private set; }
    public Vector3 ContactPoint { get; private set; }
    public Vector3 ContactNormal { get; private set; } = Vector3.up;
    public float ContactDistance { get; private set; }

    // Cached base rotation for the pole pivot
    private Quaternion _baseLocalRot;

    private void Reset()
    {
        groundMask = Physics.DefaultRaycastLayers;
        rayLength = 1.5f;
        contactOffset = 0.02f;
        contactNormalLerpSpeed = 25f;

        entryEnd = 0.25f;
        dragEnd = 0.7f;

        idlePitch = -20f;
        entryPitch = 0f;
        dragPitch = -55f;
        followPitch = 20f;

        idleTilt = 0f;
        entryTilt = 0f;
        dragTilt = 12f;
        followTilt = 4f;

        carveMinSpeed = 3f;
        carveMaxSpeed = 20f;
        insideCarvePitchDelta = -12f;
        outsideCarvePitchDelta = 8f;
        insideCarveTiltDelta = 8f;
        outsideCarveTiltDelta = 4f;

        maxSlopePitchOffset = 10f;
        poseLerpSpeed = 12f;
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

        float t = Mathf.Clamp01(skiController.PoleStrokeT);
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
        // 1. Base pitch & tilt from stroke parameter (no carve yet)
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
            float tEntryEnd = Mathf.Max(0.01f, entryEnd);
            float tDragEnd = Mathf.Clamp(dragEnd, tEntryEnd + 0.05f, 0.99f);

            if (t < tEntryEnd)
            {
                float u = t / tEntryEnd;
                pitch = Mathf.Lerp(idlePitch, entryPitch, u);
                tilt = Mathf.Lerp(idleTilt, entryTilt, u);
            }
            else if (t < tDragEnd)
            {
                float u = (t - tEntryEnd) / (tDragEnd - tEntryEnd);
                pitch = Mathf.Lerp(entryPitch, dragPitch, u);
                tilt = Mathf.Lerp(entryTilt, dragTilt, u);
            }
            else
            {
                float u = (t - tDragEnd) / (1f - tDragEnd);
                pitch = Mathf.Lerp(dragPitch, followPitch, u);
                tilt = Mathf.Lerp(dragTilt, followTilt, u);
            }

            // Make drag outward tilt left/right.
            if (t >= tEntryEnd && t <= tDragEnd)
            {
                float sign = isLeftPole ? 1f : -1f;
                tilt *= sign;
            }
            else
            {
                // In non-drag regions, make outward tilt subtle or neutral.
                if (Mathf.Approximately(tilt, dragTilt))
                {
                    float sign = isLeftPole ? 1f : -1f;
                    tilt = dragTilt * sign;
                }
            }
        }

        // ------------------------------------------------------------------
        // 2. Add slope influence (on steeper slopes poles point more downhill)
        // ------------------------------------------------------------------
        pitch += slopePitchExtra;

        // ------------------------------------------------------------------
        // 3. Carve adjustments: only when grounded and in drag-ish region
        // ------------------------------------------------------------------
        if (grounded && carveIntensity > 0.01f)
        {
            float tEntryEnd = Mathf.Max(0.01f, entryEnd);
            float tDragEnd = Mathf.Clamp(dragEnd, tEntryEnd + 0.05f, 0.99f);

            if (t >= tEntryEnd && t <= tDragEnd)
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
        }

        // ------------------------------------------------------------------
        // 4. Build final rotation and smooth slightly (timing comes from SkiController)
        // ------------------------------------------------------------------
        Quaternion pitchRot = Quaternion.AngleAxis(pitch, Vector3.right);
        Quaternion sideRot = Quaternion.AngleAxis(tilt, Vector3.forward);

        Quaternion target = _baseLocalRot * sideRot * pitchRot;

        poleRoot.localRotation = Quaternion.Slerp(
            poleRoot.localRotation,
            target,
            poseLerpSpeed * dt
        );
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
