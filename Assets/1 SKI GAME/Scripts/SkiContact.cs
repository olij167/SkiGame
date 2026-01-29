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

    [Tooltip("Local Z (forward) threshold below which a contact point counts as 'tail region' (0 = at binding, -1 = very tail).")]
    [SerializeField, Range(-1f, 1f)] private float tailRegionLocalZThreshold = -0.3f;

    [Tooltip("Collision contacts with an up-dot below this are ignored (prevents wall contacts counting as ground).")]
    [SerializeField, Range(0f, 1f)] private float minCollisionUpDot = 0.25f;

    [Header("Ground Probe (Raycast)")]
    [Tooltip("How far above the ski to start the ground ray (along local +up).")]
    [SerializeField] private float probeUpOffset = 0.05f;

    [Tooltip("Max distance the ray will check for ground from the probe origin.")]
    [SerializeField] private float probeDistance = 0.4f;

    [Tooltip("Max additional distance (beyond probeUpOffset) that still counts as true contact. Lower = stricter grounding.")]
    [SerializeField] private float probeContactDistance = 0.12f;

    [Tooltip("SphereCast radius used for probing. SphereCasts reduce triangle-to-triangle normal jitter compared to rays.")]
    [SerializeField] private float probeSphereRadius = 0.04f;

    [Tooltip("If we've been in the air longer than this, snap contact on reacquire instead of lerping (prevents laggy landings).")]
    [SerializeField] private float hardResetAirTime = 0.15f;

    [Header("Contact Smoothing")]
    [Tooltip("How quickly the contact point/normal lerp toward new hits. Higher = snappier, Lower = smoother.")]
    [SerializeField] private float contactSmoothSpeed = 15f;

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
    /// If HasTipContact is true, indicates whether the dominant end contact is at the tip (+1) or tail (-1).
    /// 0 when no end contact is detected.
    /// </summary>
    public int EndContactSign { get; private set; }

    /// <summary>
    /// Local-Z of the dominant end contact point (in this ski's local space). Useful for debugging.
    /// </summary>
    public float EndContactLocalZ { get; private set; }


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

    public enum SkiProbeRegion
    {
        Front, // tip probe
        Mid,   // base probe
        Rear   // tail probe
    }

    public Vector3 GetProbeWorldPosition(SkiProbeRegion region)
    {
        float z = 0f;
        switch (region)
        {
            case SkiProbeRegion.Front: z = _localTipZ; break;
            case SkiProbeRegion.Rear: z = _localTailZ; break;
            case SkiProbeRegion.Mid:
            default: z = 0f; break; // bindings/base region
        }

        return transform.TransformPoint(new Vector3(0f, 0f, z));
    }


    /// <summary>
    /// Returns the latest grounded probe contact for a specific region.
    /// If the probe didn't hit, this can optionally fall back to collision contact if present.
    /// </summary>
    public bool TryGetProbeContact(SkiProbeRegion region, out Vector3 point, out Vector3 normal)
    {
        // Prefer probe hits
        switch (region)
        {
            case SkiProbeRegion.Front:
                if (_probeTipHit) { point = _probeTipPoint; normal = _probeTipNormal; return true; }
                break;

            case SkiProbeRegion.Mid:
                if (_probeBaseHit) { point = _probeBasePoint; normal = _probeBaseNormal; return true; }
                break;

            case SkiProbeRegion.Rear:
                if (_probeTailHit) { point = _probeTailPoint; normal = _probeTailNormal; return true; }
                break;
        }

        // Fallback: collision contact (keeps particles stable when probes miss but physics contact exists)
        if (_hasCollisionContact)
        {
            point = _collisionContactPoint;
            normal = (_collisionContactNormal.sqrMagnitude > 0.0001f) ? _collisionContactNormal.normalized : Vector3.up;
            return true;
        }

        point = default;
        normal = Vector3.up;
        return false;
    }

    // Latest probe hits (within probeContactDistance threshold)
    private bool _probeBaseHit;
    private Vector3 _probeBasePoint;
    private Vector3 _probeBaseNormal;

    private bool _probeTipHit;
    private Vector3 _probeTipPoint;
    private Vector3 _probeTipNormal;

    private bool _probeTailHit;
    private Vector3 _probeTailPoint;
    private Vector3 _probeTailNormal;

    // NOTE: Do not sample in this script's FixedUpdate.
    // SkiController will call this deterministically at the start of its FixedUpdate.
    public void ManualSampleGround()
    {
        SampleGround();
    }

    private float _localTipZ;
    private float _localTailZ;
    private bool _geometryInitialized;

    // Collision-based contact cache (used when rays miss but colliders touch).
    private bool _hasCollisionContact;
    private Vector3 _collisionContactPoint;
    private Vector3 _collisionContactNormal;

    // NEW: which collider we are actually touching (so SkiController can detect grindables without global searches)
    private Collider _collisionOtherCollider;

    /// <summary>True if we have an active collision-derived ground contact.</summary>
    public bool HasCollisionContact => _hasCollisionContact;

    /// <summary>The collider we are touching for the last cached collision contact (can be null).</summary>
    public Collider CollisionOtherCollider => _collisionOtherCollider;
    // Any-collision cache (used for rails/props that are not in groundLayers, including triggers).
    private bool _hasAnyCollisionContact;
    private Collider _anyCollisionOtherCollider;

    /// <summary>True if we have *any* collision/trigger contact cached this frame (not filtered by groundLayers).</summary>
    public bool HasAnyCollisionContact => _hasAnyCollisionContact;

    /// <summary>The collider we are touching for the last cached any-collision contact (can be null).</summary>
    public Collider AnyCollisionOtherCollider => _anyCollisionOtherCollider;

    public Vector3 CollisionContactPoint => _collisionContactPoint;
    public Vector3 CollisionContactNormal => _collisionContactNormal;
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
        float contactMaxDist = Mathf.Max(0.001f, probeUpOffset + probeContactDistance);

        // Base / under-foot
        Vector3 baseOrigin = transform.position + worldUp * probeUpOffset;

        // Tip and tail origins
        Vector3 tipOrigin = baseOrigin;
        Vector3 tailOrigin = baseOrigin;

        if (_geometryInitialized)
        {
            tipOrigin = transform.TransformPoint(new Vector3(0f, 0f, _localTipZ)) + worldUp * probeUpOffset;
            tailOrigin = transform.TransformPoint(new Vector3(0f, 0f, _localTailZ)) + worldUp * probeUpOffset;
        }

        bool SphereDown(Vector3 origin, out RaycastHit hit)
        {
            return Physics.SphereCast(
                origin,
                probeSphereRadius,
                worldDown,
                out hit,
                maxDist,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

        // Reset probe hits each sample
        _probeBaseHit = false;
        _probeTipHit = false;
        _probeTailHit = false;

        // Probe hits
        bool gotBase = SphereDown(baseOrigin, out RaycastHit baseHit) && baseHit.distance <= contactMaxDist;
        bool gotTip = SphereDown(tipOrigin, out RaycastHit tipHit) && tipHit.distance <= contactMaxDist;
        bool gotTail = SphereDown(tailOrigin, out RaycastHit tailHit) && tailHit.distance <= contactMaxDist;

        if (gotBase)
        {
            _probeBaseHit = true;
            _probeBasePoint = baseHit.point;
            _probeBaseNormal = (baseHit.normal.sqrMagnitude > 0.0001f) ? baseHit.normal.normalized : Vector3.up;
        }

        if (gotTip)
        {
            _probeTipHit = true;
            _probeTipPoint = tipHit.point;
            _probeTipNormal = (tipHit.normal.sqrMagnitude > 0.0001f) ? tipHit.normal.normalized : Vector3.up;
        }

        if (gotTail)
        {
            _probeTailHit = true;
            _probeTailPoint = tailHit.point;
            _probeTailNormal = (tailHit.normal.sqrMagnitude > 0.0001f) ? tailHit.normal.normalized : Vector3.up;
        }

        bool anyProbeHit = gotBase || gotTip || gotTail;
        bool anyHit = _hasCollisionContact || anyProbeHit;

        // If no hits, apply coyote time (keep last contact briefly)
        if (!anyHit)
        {
            bool withinCoyote =
                IsGrounded &&
                (Time.time - LastContactTime) <= contactCoyoteTime;

            if (!withinCoyote)
            {
                IsGrounded = false;
                HasTipContact = false;
                EndContactSign = 0;
                EndContactLocalZ = 0f;
                BaseContactAlignment = 0f;

                _probeBaseHit = false;
                _probeTipHit = false;
                _probeTailHit = false;
            }

            _hasCollisionContact = false;
            return;
        }

        // Choose primary contact:
        //  1) Collision dominates if present
        //  2) Base probe (most stable)
        //  3) Closest of tip/tail probes
        Vector3 targetPoint;
        Vector3 targetNormal;

        if (_hasCollisionContact)
        {
            targetPoint = _collisionContactPoint;
            targetNormal = (_collisionContactNormal.sqrMagnitude > 0.0001f) ? _collisionContactNormal.normalized : Vector3.up;
        }
        else if (gotBase)
        {
            targetPoint = baseHit.point;
            targetNormal = (baseHit.normal.sqrMagnitude > 0.0001f) ? baseHit.normal.normalized : Vector3.up;
        }
        else
        {
            // Choose the closest hit by distance
            bool useTip = gotTip && (!gotTail || tipHit.distance <= tailHit.distance);
            RaycastHit h = useTip ? tipHit : tailHit;

            targetPoint = h.point;
            targetNormal = (h.normal.sqrMagnitude > 0.0001f) ? h.normal.normalized : Vector3.up;
        }

        float timeSinceLast = (LastContactTime > 0f) ? (Time.time - LastContactTime) : float.MaxValue;
        bool shouldSnap = !IsGrounded || timeSinceLast > hardResetAirTime;

        if (shouldSnap)
        {
            ContactPoint = targetPoint;
            ContactNormal = targetNormal;
        }
        else
        {
            float t = 1f - Mathf.Exp(-contactSmoothSpeed * Time.fixedDeltaTime);
            ContactPoint = Vector3.Lerp(ContactPoint, targetPoint, t);
            ContactNormal = Vector3.Slerp(ContactNormal, targetNormal, t);
        }

        IsGrounded = true;
        LastContactTime = Time.time;

        // How "flat" is the ski on the contact? 1 = base-down, 0 = on edge, -1 = upside down.
        float rawAlign = Mathf.Clamp(Vector3.Dot(transform.up, ContactNormal), -1f, 1f);

        if (shouldSnap)
        {
            BaseContactAlignment = rawAlign;
        }
        else
        {
            float tAlign = 1f - Mathf.Exp(-contactSmoothSpeed * Time.fixedDeltaTime);
            BaseContactAlignment = Mathf.Lerp(BaseContactAlignment, rawAlign, tAlign);
        }

        // End-region detection (tip OR tail) using probe hits only (stable + cheap)
        bool endish = false;
        float bestAbsEndZ = 0f;
        float bestEndZ = 0f;
        int bestEndSign = 0;

        if (gotTip)
        {
            float z = transform.InverseTransformPoint(tipHit.point).z;
            if (z > tipRegionLocalZThreshold)
            {
                endish = true;
                float absZ = Mathf.Abs(z);
                if (absZ > bestAbsEndZ)
                {
                    bestAbsEndZ = absZ;
                    bestEndZ = z;
                    bestEndSign = 1;
                }
            }
        }

        if (gotTail)
        {
            float z = transform.InverseTransformPoint(tailHit.point).z;
            if (z < tailRegionLocalZThreshold)
            {
                endish = true;
                float absZ = Mathf.Abs(z);
                if (absZ > bestAbsEndZ)
                {
                    bestAbsEndZ = absZ;
                    bestEndZ = z;
                    bestEndSign = -1;
                }
            }
        }

        HasTipContact = endish; // legacy name: "tip OR tail"
        EndContactLocalZ = endish ? bestEndZ : 0f;
        EndContactSign = endish ? bestEndSign : 0;
    }

    private void EnsureGeometry()
    {
        if (_geometryInitialized)
            return;

        _geometryInitialized = true;

        // Sensible fallbacks if we can't inspect a collider.
        _localTipZ = 0.5f;
        _localTailZ = -0.5f;

        // Try to infer the ski's local tip & tail positions from its collider bounds.
        Collider col = GetComponentInChildren<Collider>();
        if (col == null)
            return;

        Bounds b = col.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        float maxLocalZ = float.NegativeInfinity;
        float minLocalZ = float.PositiveInfinity;

        // Build the 8 corners of the bounds, transform to local, and find
        // the furthest points along local +Z (tip) and local -Z (tail).
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
                    if (localCorner.z < minLocalZ)
                        minLocalZ = localCorner.z;
                }
            }
        }

        if (!float.IsNegativeInfinity(maxLocalZ))
        {
            _localTipZ = maxLocalZ;
        }

        if (!float.IsPositiveInfinity(minLocalZ))
        {
            _localTailZ = minLocalZ;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision == null || collision.contactCount <= 0)
            return;

        // Cache *any* collision contact (NOT layer-filtered) so systems like grinding
        // can detect rails/props that are not part of groundLayers.
        {
            ContactPoint cp0 = collision.GetContact(0);
            _hasAnyCollisionContact = true;
            _anyCollisionOtherCollider = cp0.otherCollider;
        }

        // Only treat as "ground collision contact" if the other object is on groundLayers.
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        // Pick the "most ground-like" contact (highest up-dot).
        float bestUpDot = -1f;
        Vector3 bestPoint = default;
        Vector3 bestNormal = default;
        Collider bestOther = null;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint cp = collision.GetContact(i);
            Vector3 n = cp.normal.sqrMagnitude > 0.0001f ? cp.normal.normalized : Vector3.up;

            float upDot = Vector3.Dot(n, Vector3.up);
            if (upDot > bestUpDot)
            {
                bestUpDot = upDot;
                bestPoint = cp.point;
                bestNormal = n;
                bestOther = cp.otherCollider;
            }
        }

        // Ignore wall-ish contacts for *grounding*.
        if (bestUpDot < minCollisionUpDot)
            return;

        _hasCollisionContact = true;
        _collisionContactPoint = bestPoint;
        _collisionContactNormal = bestNormal;
        _collisionOtherCollider = bestOther;
    }

    private void OnTriggerStay(Collider other)
    {
        // Cache trigger contacts as "any collision" so grindables using triggers can be detected.
        if (other == null) return;
        _hasAnyCollisionContact = true;
        _anyCollisionOtherCollider = other;
    }

    private void OnTriggerExit(Collider other)
    {
        // Clear trigger cache if we are exiting the cached collider.
        if (other == null) return;
        if (other == _anyCollisionOtherCollider)
        {
            _hasAnyCollisionContact = false;
            _anyCollisionOtherCollider = null;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision == null) return;

        // Always clear the any-collision cache if we are exiting the cached collider.
        Collider other = collision.collider;
        if (other != null && other == _anyCollisionOtherCollider)
        {
            _hasAnyCollisionContact = false;
            _anyCollisionOtherCollider = null;
        }

        // Only clear the "ground collision" cache if this was a ground-layer collision.
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        _hasCollisionContact = false;
        _collisionOtherCollider = null;
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
        _collisionOtherCollider = null;

    }

    public void NotifySkiModelChanged()
    {
        _geometryInitialized = false;
        ResetContactState();
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
