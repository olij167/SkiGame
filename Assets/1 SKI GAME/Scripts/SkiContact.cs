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

        // Base / under-foot
        Vector3 baseOrigin = transform.position + worldUp * probeUpOffset;

        // Tip and tail origins in world space (if we have geometry length)
        Vector3 tipOrigin = baseOrigin;
        Vector3 tailOrigin = baseOrigin;

        if (_geometryInitialized)
        {
            Vector3 localTipPos = new Vector3(0f, 0f, _localTipZ);
            tipOrigin = transform.TransformPoint(localTipPos) + worldUp * probeUpOffset;

            Vector3 localTailPos = new Vector3(0f, 0f, _localTailZ);
            tailOrigin = transform.TransformPoint(localTailPos) + worldUp * probeUpOffset;
        }

        bool SphereDown(Vector3 origin, out RaycastHit hit)
        {
            // SphereCast is significantly more stable than a Raycast on mesh terrain.
            return Physics.SphereCast(
                origin,
                probeSphereRadius,
                worldDown,
                out hit,
                maxDist,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

        // Gather hits (base / tip / tail + collision fallback)
        var hits = new System.Collections.Generic.List<RaycastHit>(4);

        // Only count probe hits as "true contact" if they are very close.
        // This prevents the controller treating near-ground as grounded (and stops false state flips).
        float contactMaxDist = Mathf.Max(0.001f, probeUpOffset + probeContactDistance);

        if (SphereDown(baseOrigin, out RaycastHit baseHit) && baseHit.distance <= contactMaxDist) hits.Add(baseHit);
        if (SphereDown(tipOrigin, out RaycastHit tipHit) && tipHit.distance <= contactMaxDist) hits.Add(tipHit);
        if (SphereDown(tailOrigin, out RaycastHit tailHit) && tailHit.distance <= contactMaxDist) hits.Add(tailHit);

        // If we have collision contact, bias heavily toward it.
        // This makes contact normals reflect the actual physics solver rather than probe noise.
        if (_hasCollisionContact)
        {
            // Add it multiple times to increase its weight in the inverse-distance blend.
            // Distance is 0 so it already weights strongly, but this makes it dominate.
            for (int i = 0; i < 2; i++)
            {
                RaycastHit ch = new RaycastHit
                {
                    point = _collisionContactPoint,
                    normal = _collisionContactNormal,
                    distance = 0f
                };
                hits.Add(ch);
            }
        }

        // If no hits, apply coyote time (keep last contact briefly)
        if (hits.Count == 0)
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
            }

            _hasCollisionContact = false;
            return;
        }

        // Choose ONE primary contact to drive ContactPoint/ContactNormal to avoid jitter.
        // Priority:
        //  1) Collision contact (most physically truthful)
        //  2) Base probe (most stable for stance + slope normal)
        //  3) Nearest of tip/tail probes (fallback)
        bool hasPrimary = false;
        RaycastHit primaryHit = default;

        // 1) Collision dominates if present
        if (_hasCollisionContact)
        {
            primaryHit = new RaycastHit
            {
                point = _collisionContactPoint,
                normal = _collisionContactNormal,
                distance = 0f
            };
            hasPrimary = true;
        }
        else
        {
            // Prefer the base probe if we have it; it is the most stable for "am I on my base?"
            // Tip/tail probes are used for end-contact detection only.
            bool haveBase = false;
            RaycastHit best = default;

            // Re-run a base-only cast (cheap) to avoid ambiguity about which hit in the list was "base".
            if (SphereDown(baseOrigin, out RaycastHit bh) && bh.distance <= contactMaxDist)
            {
                best = bh;
                haveBase = true;
            }

            if (haveBase)
            {
                primaryHit = best;
                hasPrimary = true;
            }
            else
            {
                // Fallback: choose the closest hit by distance (stable) rather than local Z.
                float bestDist = float.PositiveInfinity;
                for (int i = 0; i < hits.Count; i++)
                {
                    if (hits[i].distance < bestDist)
                    {
                        bestDist = hits[i].distance;
                        primaryHit = hits[i];
                        hasPrimary = true;
                    }
                }
            }
        }

        if (!hasPrimary)
        {
            // Shouldn't happen because hits.Count > 0 earlier, but keep safe.
            IsGrounded = false;
            HasTipContact = false;
            EndContactSign = 0;
            EndContactLocalZ = 0f;
            BaseContactAlignment = 0f;
            _hasCollisionContact = false;
            return;
        }

        Vector3 targetPoint = primaryHit.point;
        Vector3 targetNormal = (primaryHit.normal.sqrMagnitude > 0.0001f) ? primaryHit.normal.normalized : Vector3.up;

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

        // End-region detection (tip OR tail): evaluate individual hits (more stable than testing only blended point)
        bool endish = false;
        float bestAbsEndZ = 0f;
        float bestEndZ = 0f;
        int bestEndSign = 0;

        for (int i = 0; i < hits.Count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(hits[i].point);
            float z = local.z;

            bool isEnd =
                (z > tipRegionLocalZThreshold) ||
                (z < tailRegionLocalZThreshold);

            if (!isEnd) continue;

            endish = true;

            float absZ = Mathf.Abs(z);
            if (absZ > bestAbsEndZ)
            {
                bestAbsEndZ = absZ;
                bestEndZ = z;
                bestEndSign = (z >= 0f) ? 1 : -1; // +1 = tip, -1 = tail
            }
        }

        // Legacy name: HasTipContact now means "tip OR tail contact"
        HasTipContact = endish;
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

    private void OnCollisionStay(Collision collision)
    {
        // Only care about collisions with ground layers.
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        // Use the first contact as a representative.
        if (collision.contactCount > 0)
        {
            ContactPoint cp = collision.GetContact(0);
            _hasCollisionContact = true;
            _collisionContactPoint = cp.point;
            _collisionContactNormal = cp.normal;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        // When we fully lose collision with ground, clear the cache.
        _hasCollisionContact = false;
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
