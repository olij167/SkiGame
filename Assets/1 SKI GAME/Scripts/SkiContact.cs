using UnityEngine;

/// <summary>
/// Per-ski helper used by SkiController to reason about each ski's direction
/// and stance. This does NOT have its own Rigidbody; it just lives on the
/// left/right ski transforms.
/// </summary>
[DisallowMultipleComponent]
public class SkiContact : MonoBehaviour
{
    [Tooltip("True if this is the left ski; false for right. Only for clarity / debugging.")]
    public bool isLeftSki = true;

    [SerializeField, Range(0f, 1f)]
    private float stanceOut;

    [Header("Contact Sensing")]
    [Tooltip("Layers considered 'snow/ground' for ski contacts.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Local Z (forward) threshold above which a contact point counts as 'tip region' (0 = at binding, 1 = very tip).")]
    [SerializeField, Range(-1f, 1f)] private float tipRegionLocalZThreshold = 0.3f;

    [Header("Ground Probe (Raycast)")]
    [Tooltip("How far above the ski to start the ground ray (along local +up).")]
    [SerializeField] private float probeUpOffset = 0.05f;

    [Tooltip("Max distance the ray will check for ground from the probe origin.")]
    [SerializeField] private float probeDistance = 0.4f;

    [Header("Contact Smoothing")]
    [Tooltip("How quickly the contact point/normal lerp toward new hits. Higher = snappier, Lower = smoother.")]
    [SerializeField] private float contactSmoothSpeed = 20f;

    [Tooltip("Small grace period where, if rays miss for a frame or two, we keep the last contact instead of dropping to 'air'.")]
    [SerializeField] private float contactCoyoteTime = 0.05f;

    /// <summary>True if this ski currently has any ground contact this frame.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>True if the last ground contact was predominantly on the tip region of the ski.</summary>
    public bool HasTipContact { get; private set; }

    /// <summary>Last ground contact normal for this ski.</summary>
    public Vector3 ContactNormal { get; private set; } = Vector3.up;

    /// <summary>Last ground contact point for this ski.</summary>
    public Vector3 ContactPoint { get; private set; }

    /// <summary>Time of the last ground contact update.</summary>
    public float LastContactTime { get; private set; }

    /// <summary>
    /// How well the ground normal aligns with the ski's 'up' direction.
    /// 1 = flat on the base, 0 = contacting mostly on the side/tip, -1 = on the top.
    /// Used by SkiController to detect unstable tip/tail stands.
    /// </summary>
    public float BaseContactAlignment { get; private set; }

    /// <summary>
    /// Current normalized stance for this ski (0 = under body, 1 = fully out).
    /// Set by SkiController every frame.
    /// </summary>
    public float StanceOut
    {
        get => stanceOut;
        set => stanceOut = Mathf.Clamp01(value);
    }

    private void FixedUpdate()
    {
        SampleGround();
    }

    private float _localTipZ;
    private bool _geometryInitialized;

    /// <summary>
    /// Actively probes for ground under this ski using one or two vertical
    /// (world-down) rays. We deliberately avoid using transform.up so we still
    /// see the ground when the ski is pitched onto its tips or tails.
    /// </summary>
    private void SampleGround()
    {
        EnsureGeometry();

        Vector3 worldUp = Vector3.up;
        Vector3 worldDown = Vector3.down;

        float maxDist = probeDistance + probeUpOffset;

        // Base / under-foot origin (near the binding / ski root).
        Vector3 baseOrigin = transform.position + worldUp * probeUpOffset;

        // Tip origin: forward along local +Z to the tip of the ski mesh.
        Vector3 tipOrigin = baseOrigin;
        if (_geometryInitialized)
        {
            Vector3 localTipPos = new Vector3(0f, 0f, _localTipZ);
            tipOrigin = transform.TransformPoint(localTipPos) + worldUp * probeUpOffset;
        }

        bool hitSomething = false;
        RaycastHit bestHit = default;
        float bestDist = float.MaxValue;

        // Ray 1: under-foot
        if (Physics.Raycast(baseOrigin, worldDown, out RaycastHit baseHit, maxDist,
                            groundLayers, QueryTriggerInteraction.Ignore))
        {
            hitSomething = true;
            bestHit = baseHit;
            bestDist = baseHit.distance;
        }

        // Ray 2: tip – we keep whichever hit is closer to the origin.
        if (Physics.Raycast(tipOrigin, worldDown, out RaycastHit tipHit, maxDist,
                            groundLayers, QueryTriggerInteraction.Ignore))
        {
            float d = tipHit.distance;
            if (!hitSomething || d < bestDist)
            {
                hitSomething = true;
                bestHit = tipHit;
                bestDist = d;
            }
        }

        // ---------------------------------------------
        // No hit: use a small coyote window to keep the
        // previous contact instead of dropping instantly.
        // ---------------------------------------------
        if (!hitSomething)
        {
            bool keepAsGrounded =
                IsGrounded &&
                (Time.time - LastContactTime) <= contactCoyoteTime;

            if (keepAsGrounded)
            {
                // Don't change ContactPoint/Normal/HasTipContact;
                // just keep last frame's data to avoid jitter.
                return;
            }

            IsGrounded = false;
            HasTipContact = false;
            BaseContactAlignment = 0f;
            return;
        }

        // We have a valid hit: mark grounded and smooth toward it.
        IsGrounded = true;

        Vector3 hitPoint = bestHit.point;
        Vector3 hitNormal = bestHit.normal;

        // First contact or after a long time in the air: snap.
        if (LastContactTime <= 0f || !IsGrounded)
        {
            ContactPoint = hitPoint;
            ContactNormal = hitNormal.normalized;
        }
        else
        {
            float t = 1f - Mathf.Exp(-contactSmoothSpeed * Time.fixedDeltaTime);
            ContactPoint = Vector3.Lerp(ContactPoint, hitPoint, t);
            ContactNormal = Vector3.Slerp(ContactNormal, hitNormal.normalized, t);
        }

        LastContactTime = Time.time;

        // 1 = flat on base, 0 = side/tip, -1 = base fully upside-down.
        BaseContactAlignment = Vector3.Dot(ContactNormal, transform.up);

        // Classify tip vs under-foot using local Z of the *hit* point.
        Vector3 local = transform.InverseTransformPoint(bestHit.point);
        HasTipContact = Mathf.Abs(local.z) > tipRegionLocalZThreshold;
    }

    private void EnsureGeometry()
    {
        if (_geometryInitialized)
            return;

        _geometryInitialized = true;
        _localTipZ = 0.5f; // sensible fallback if we can't find a collider

        // Try to infer the ski's local tip position from its collider bounds.
        Collider col = GetComponentInChildren<Collider>();
        if (col == null)
            return;

        Bounds b = col.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        // Build the 8 corners of the bounds, transform to local, and find
        // the furthest point along local +Z (tip).
        float maxLocalZ = float.NegativeInfinity;

        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 worldCorner = new Vector3(
                        c.x + ix * e.x,
                        c.y + iy * e.y,
                        c.z + iz * e.z);

                    Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                    if (localCorner.z > maxLocalZ)
                        maxLocalZ = localCorner.z;
                }
            }
        }

        if (!float.IsNegativeInfinity(maxLocalZ))
        {
            _localTipZ = maxLocalZ;
        }
    }

    /// <summary>
    /// Returns this ski's forward direction projected onto the given ground plane.
    /// Falls back to the parent transform's forward if this transform's forward
    /// is degenerate relative to the plane.
    /// </summary>
    public Vector3 GetForwardOnPlane(Vector3 groundNormal)
    {
        Vector3 f = transform.forward;
        f = Vector3.ProjectOnPlane(f, groundNormal);

        if (f.sqrMagnitude < 0.0001f)
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                f = Vector3.ProjectOnPlane(parent.forward, groundNormal);
            }
            else
            {
                f = Vector3.ProjectOnPlane(Vector3.forward, groundNormal);
            }
        }

        return f.normalized;
    }

    /// <summary>
    /// Clears cached contact state. Can be called by the controller if needed.
    /// </summary>
    public void ResetContactState()
    {
        IsGrounded = false;
        HasTipContact = false;
    }



#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        // Ski forward projected onto a flat plane (world up).
        Vector3 up = Vector3.up;
        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, up);
        if (forwardOnPlane.sqrMagnitude < 0.0001f)
            forwardOnPlane = transform.forward;

        forwardOnPlane.Normalize();

        // Colour: left = cyan, right = magenta.
        Color baseColor = isLeftSki ? Color.cyan : Color.magenta;
        Gizmos.color = baseColor;
        Gizmos.DrawRay(pos, forwardOnPlane * 0.5f);

        // Stance: show how far this ski is pushed out from center.
        // Left ski draws to its left, right ski to its right.
        Vector3 side = Vector3.Cross(up, forwardOnPlane).normalized;
        float sideSign = isLeftSki ? -1f : 1f;

        Vector3 stanceVec = side * sideSign * stanceOut * 0.4f;
        Gizmos.color = Color.Lerp(Color.gray, baseColor, stanceOut);
        Gizmos.DrawRay(pos, stanceVec);

        // Tiny sphere at ski position for reference.
        Gizmos.DrawWireSphere(pos, 0.02f);
    }
#endif
}
