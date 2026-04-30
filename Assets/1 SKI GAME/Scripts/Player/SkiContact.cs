using System.Collections.Generic;
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

    [Tooltip("Probe hits with an up-dot below this are ignored (prevents down-spherecasts hitting walls/vertical faces).")]
    [SerializeField, Range(0f, 1f)] private float minProbeUpDot = 0.25f;

    [Header("Ground Probe (Raycast)")]
    [Tooltip("How far above the ski to start the ground ray (along local +up).")]
    [SerializeField] private float probeUpOffset = 0.05f;

    [Tooltip("Max distance the ray will check for ground from the probe origin.")]
    [SerializeField] private float probeDistance = 0.4f;

    [Tooltip("Max additional distance (beyond probeUpOffset) that still counts as true contact. Lower = stricter grounding.")]
    [SerializeField] private float probeContactDistance = 0.12f;

    [Tooltip("Extra probe/contact distance granted when the ski is in a rolled or pitched authored pose.")]
    [SerializeField] private float posedProbeExtraContactDistance = 0.3f;

    [Tooltip("When enabled, also probes from left/right ski edges so edge-touching poses still register snow contact.")]
    [SerializeField] private bool useEdgeCompensationProbes = true;

    [Tooltip("Half-width offset used for left/right edge compensation probes.")]
    [SerializeField] private float edgeProbeHalfWidth = 0.08f;

    [Tooltip("SphereCast radius used for probing. SphereCasts reduce triangle-to-triangle normal jitter compared to rays.")]
    [SerializeField] private float probeSphereRadius = 0.04f;

    [Tooltip("If we've been in the air longer than this, snap contact on reacquire instead of lerping (prevents laggy landings).")]
    [SerializeField] private float hardResetAirTime = 0.15f;

    [Header("Contact Smoothing")]
    [Tooltip("How quickly the contact point/normal lerp toward new hits. Higher = snappier, Lower = smoother.")]
    [SerializeField] private float contactSmoothSpeed = 15f;

    [Tooltip("Small grace period where, if rays miss for a frame or two, we keep the last contact instead of dropping to 'air'.")]
    [SerializeField] private float contactCoyoteTime = 0.05f;

    [Header("Trick Pose Probe Recovery")]
    [Tooltip("When enabled, adds front-mid and rear-mid probes so partial ski-length contact can still reacquire snow when the main base probe misses.")]
    [SerializeField] private bool useIntermediateLengthProbes = true;

    [Tooltip("How far from the ski midpoint the intermediate recovery probes sit along the ski length. Higher values push them closer to the tip/tail.")]
    [SerializeField, Range(0.15f, 0.85f)]
    private float intermediateProbeFraction = 0.55f;

    [Tooltip("When enabled, retries probes from a higher origin so trick poses can reacquire terrain even when the normal probe starts too close to or slightly inside the snow.")]
    [SerializeField] private bool useHighOriginRecoveryProbe = true;

    [Tooltip("Extra upward start offset used by the high-origin recovery probe. Higher values help fix false-airborne cases but can feel stickier if overused.")]
    [SerializeField] private float recoveryProbeExtraUpOffset = 0.35f;

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

    private Collider _probeBaseCollider;
    private Collider _probeTipCollider;
    private Collider _probeTailCollider;

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

    // Wall-contact cache (collision on groundLayers but too steep to count as ground).
    // Used by SkiController to detect airborne cliff scrapes and suppress "ground-like" alignment/forces.
    private bool _hasWallContact;
    private Vector3 _wallContactPoint;
    private Vector3 _wallContactNormal;
    private Collider _wallOtherCollider;

    /// <summary>True if we have a collision contact on groundLayers that is too steep to be considered ground.</summary>
    public bool HasWallContact => _hasWallContact;

    /// <summary>Last cached wall contact normal (unit-ish). Meaningful when HasWallContact is true.</summary>
    public Vector3 WallContactNormal => _wallContactNormal;

    /// <summary>Last cached wall contact point. Meaningful when HasWallContact is true.</summary>
    public Vector3 WallContactPoint => _wallContactPoint;

    /// <summary>The collider we are scraping (can be null).</summary>
    public Collider WallOtherCollider => _wallOtherCollider;

    /// <summary>True if we have an active collision-derived ground contact.</summary>
    public bool HasCollisionContact => _hasCollisionContact;

    /// <summary>The collider we are touching for the last cached collision contact (can be null).</summary>
    public Collider CollisionOtherCollider => _collisionOtherCollider;
    // Any-collision cache (used for rails/props that are not in groundLayers, including triggers).
    private bool _hasAnyCollisionContact;
    private Collider _anyCollisionOtherCollider;
    private readonly HashSet<Collider> _anyCollisionOtherColliders = new HashSet<Collider>();

    /// <summary>True if we have *any* collision/trigger contact cached this frame (not filtered by groundLayers).</summary>
    public bool HasAnyCollisionContact => _hasAnyCollisionContact;

    /// <summary>The collider we are touching for the last cached any-collision contact (can be null).</summary>
    public Collider AnyCollisionOtherCollider => _anyCollisionOtherCollider;

    /// <summary>Enumerates all currently cached collision/trigger contacts for non-ground uses like grind detection.</summary>
    public IEnumerable<Collider> AnyCollisionOtherColliders => _anyCollisionOtherColliders;

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
        float poseContactExtra = ComputePoseProbeExtraDistance();
        float maxDist = probeDistance + probeUpOffset + poseContactExtra;
        float contactMaxDist = Mathf.Max(0.001f, probeUpOffset + probeContactDistance + poseContactExtra);

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

        bool IsProbeRideable(RaycastHit h)
        {
            Vector3 n = GetUpwardNormal(h.normal);
            return Vector3.Dot(n, Vector3.up) >= minProbeUpDot;
        }

        bool TrySphereDown(Vector3 origin, float extraMaxDistance, out RaycastHit hit)
        {
            return Physics.SphereCast(
                origin,
                probeSphereRadius,
                worldDown,
                out hit,
                maxDist + extraMaxDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

        bool TryProbeBest(Vector3 origin, out RaycastHit bestHit)
        {
            bool found = false;
            bestHit = default;

            bool ConsiderProbeFromOrigin(Vector3 probeOrigin, float extraMaxDistance, ref RaycastHit currentBest, ref bool hasBest)
            {
                if (!TrySphereDown(probeOrigin, extraMaxDistance, out RaycastHit hit))
                    return false;

                float allowedContactDistance = contactMaxDist + Mathf.Max(0f, extraMaxDistance);
                if (hit.distance > allowedContactDistance || !IsProbeRideable(hit))
                    return false;

                hit.normal = GetUpwardNormal(hit.normal);

                if (!hasBest || hit.distance < currentBest.distance)
                {
                    currentBest = hit;
                    hasBest = true;
                }

                return true;
            }

            bool foundPrimary = ConsiderProbeFromOrigin(origin, 0f, ref bestHit, ref found);

            if (!foundPrimary && useHighOriginRecoveryProbe && recoveryProbeExtraUpOffset > 0.0001f)
            {
                ConsiderProbeFromOrigin(
                    origin + worldUp * recoveryProbeExtraUpOffset,
                    recoveryProbeExtraUpOffset,
                    ref bestHit,
                    ref found);
            }

            if (useEdgeCompensationProbes && edgeProbeHalfWidth > 0.0001f)
            {
                Vector3 edgeOffset = transform.right * edgeProbeHalfWidth;
                ConsiderProbeFromOrigin(origin + edgeOffset, 0f, ref bestHit, ref found);
                ConsiderProbeFromOrigin(origin - edgeOffset, 0f, ref bestHit, ref found);

                if (!found && useHighOriginRecoveryProbe && recoveryProbeExtraUpOffset > 0.0001f)
                {
                    ConsiderProbeFromOrigin(origin + edgeOffset + worldUp * recoveryProbeExtraUpOffset, recoveryProbeExtraUpOffset, ref bestHit, ref found);
                    ConsiderProbeFromOrigin(origin - edgeOffset + worldUp * recoveryProbeExtraUpOffset, recoveryProbeExtraUpOffset, ref bestHit, ref found);
                }
            }

            return found;
        }

        // Reset probe hits each sample
        _probeBaseHit = false;
        _probeTipHit = false;
        _probeTailHit = false;
        _probeBaseCollider = null;
        _probeTipCollider = null;
        _probeTailCollider = null;

        // Probe hits
        bool gotBase = TryProbeBest(baseOrigin, out RaycastHit baseHit);
        bool gotTip = TryProbeBest(tipOrigin, out RaycastHit tipHit);
        bool gotTail = TryProbeBest(tailOrigin, out RaycastHit tailHit);
        bool gotFrontMid = false;
        bool gotRearMid = false;
        RaycastHit frontMidHit = default;
        RaycastHit rearMidHit = default;

        if (!gotBase && useIntermediateLengthProbes)
        {
            float probeFraction = Mathf.Clamp(intermediateProbeFraction, 0.15f, 0.85f);
            Vector3 frontMidOrigin = Vector3.Lerp(baseOrigin, tipOrigin, probeFraction);
            Vector3 rearMidOrigin = Vector3.Lerp(baseOrigin, tailOrigin, probeFraction);

            gotFrontMid = TryProbeBest(frontMidOrigin, out frontMidHit);
            gotRearMid = TryProbeBest(rearMidOrigin, out rearMidHit);

            if (!gotTip && gotFrontMid)
            {
                gotTip = true;
                tipHit = frontMidHit;
            }

            if (!gotTail && gotRearMid)
            {
                gotTail = true;
                tailHit = rearMidHit;
            }
        }

        if (gotBase)
        {
            _probeBaseHit = true;
            _probeBasePoint = baseHit.point;
            _probeBaseNormal = (baseHit.normal.sqrMagnitude > 0.0001f) ? baseHit.normal.normalized : Vector3.up;
            _probeBaseCollider = baseHit.collider;
        }

        if (gotTip)
        {
            _probeTipHit = true;
            _probeTipPoint = tipHit.point;
            _probeTipNormal = (tipHit.normal.sqrMagnitude > 0.0001f) ? tipHit.normal.normalized : Vector3.up;
            _probeTipCollider = tipHit.collider;
        }

        if (gotTail)
        {
            _probeTailHit = true;
            _probeTailPoint = tailHit.point;
            _probeTailNormal = (tailHit.normal.sqrMagnitude > 0.0001f) ? tailHit.normal.normalized : Vector3.up;
            _probeTailCollider = tailHit.collider;
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
            targetNormal = GetUpwardNormal(_collisionContactNormal);
        }
        else if (gotBase)
        {
            targetPoint = baseHit.point;
            targetNormal = GetUpwardNormal(baseHit.normal);
        }
        else
        {
            // Choose the closest hit by distance
            bool useTip = gotTip && (!gotTail || tipHit.distance <= tailHit.distance);
            RaycastHit h = useTip ? tipHit : tailHit;

            targetPoint = h.point;
            targetNormal = GetUpwardNormal(h.normal);
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

    private float ComputePoseProbeExtraDistance()
    {
        if (posedProbeExtraContactDistance <= 0f)
            return 0f;

        Vector3 worldUp = Vector3.up;
        float upDeviation = 1f - Mathf.Clamp01(Mathf.Abs(Vector3.Dot(transform.up, worldUp)));
        float pitchOrRoll = Mathf.Max(
            Mathf.Abs(Vector3.Dot(transform.forward, worldUp)),
            Mathf.Abs(Vector3.Dot(transform.right, worldUp)));

        float poseFactor = Mathf.Clamp01(Mathf.Max(upDeviation, pitchOrRoll));
        return posedProbeExtraContactDistance * poseFactor;
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

    private void OnCollisionEnter(Collision collision)
    {
        CacheCollisionContact(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        CacheCollisionContact(collision);
    }

    private void CacheCollisionContact(Collision collision)
    {
        if (collision == null || collision.contactCount <= 0)
            return;

        // Cache *any* collision contact (NOT layer-filtered) so systems like grinding
        // can detect rails/props that are not part of groundLayers.
        {
            ContactPoint cp0 = collision.GetContact(0);
            AddAnyCollisionOtherCollider(cp0.otherCollider);
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
            Vector3 n = GetUpwardNormal(cp.normal);

            float upDot = Vector3.Dot(n, Vector3.up);
            if (upDot > bestUpDot)
            {
                bestUpDot = upDot;
                bestPoint = cp.point;
                bestNormal = n;
                bestOther = cp.otherCollider;
            }
        }

        // If it's too steep to be ground, cache as a "wall contact" instead of grounding.
        if (bestUpDot < minCollisionUpDot)
        {
            _hasWallContact = true;
            _wallContactPoint = bestPoint;
            _wallContactNormal = bestNormal;
            _wallOtherCollider = bestOther;
            return;
        }

        // Otherwise this is valid ground-like collision contact.
        _hasWallContact = false;
        _wallOtherCollider = null;

        _hasCollisionContact = true;
        _collisionContactPoint = bestPoint;
        _collisionContactNormal = bestNormal;
        _collisionOtherCollider = bestOther;
    }

    private void OnTriggerStay(Collider other)
    {
        // Cache trigger contacts as "any collision" so grindables using triggers can be detected.
        if (other == null) return;
        AddAnyCollisionOtherCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        // Clear trigger cache if we are exiting the cached collider.
        if (other == null) return;
        RemoveAnyCollisionOtherCollider(other);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision == null) return;

        // Always clear the any-collision cache if we are exiting the cached collider.
        Collider other = collision.collider;
        RemoveAnyCollisionOtherCollider(other);

        // Clear wall-contact cache if we are exiting the cached wall collider.
        if (other != null && other == _wallOtherCollider)
        {
            _hasWallContact = false;
            _wallOtherCollider = null;
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

    public Collider PrimaryContactCollider
    {
        get
        {
            if (_collisionOtherCollider != null)
                return _collisionOtherCollider;

            if (_probeBaseCollider != null)
                return _probeBaseCollider;

            if (_probeTipCollider != null)
                return _probeTipCollider;

            if (_probeTailCollider != null)
                return _probeTailCollider;

            return null;
        }
    }

    public bool HasCurrentContactWith(Collider candidate)
    {
        if (candidate == null)
            return false;

        if (_collisionOtherCollider == candidate)
            return true;

        if (_probeBaseCollider == candidate || _probeTipCollider == candidate || _probeTailCollider == candidate)
            return true;

        return _anyCollisionOtherColliders.Contains(candidate);
    }

    public bool HasContactWith(Collider candidate)
    {
        return HasCurrentContactWith(candidate);
    }

    /// <summary>
    /// Clears cached contact state. Can be called by the controller if needed.
    /// </summary>
    public void ResetContactState()
    {
        IsGrounded = false;
        HasTipContact = false;

        ContactNormal = Vector3.up;
        ContactPoint = default;
        LastContactTime = 0f;

        EndContactSign = 0;
        EndContactLocalZ = 0f;
        BaseContactAlignment = 0f;

        _probeBaseHit = false;
        _probeTipHit = false;
        _probeTailHit = false;

        _probeBaseCollider = null;
        _probeTipCollider = null;
        _probeTailCollider = null;

        _probeBasePoint = default;
        _probeTipPoint = default;
        _probeTailPoint = default;
        _probeBaseNormal = Vector3.up;
        _probeTipNormal = Vector3.up;
        _probeTailNormal = Vector3.up;

        _hasCollisionContact = false;
        _collisionContactPoint = default;
        _collisionContactNormal = Vector3.up;
        _collisionOtherCollider = null;

        _hasWallContact = false;
        _wallContactPoint = default;
        _wallContactNormal = Vector3.up;
        _wallOtherCollider = null;

        _hasAnyCollisionContact = false;
        _anyCollisionOtherCollider = null;
        _anyCollisionOtherColliders.Clear();
    }
    public void NotifySkiModelChanged()
    {
        _geometryInitialized = false;
        ResetContactState();
    }

    private void AddAnyCollisionOtherCollider(Collider other)
    {
        if (other == null)
            return;

        _anyCollisionOtherColliders.Add(other);
        _hasAnyCollisionContact = _anyCollisionOtherColliders.Count > 0;

        if (_anyCollisionOtherCollider == null)
            _anyCollisionOtherCollider = other;
    }

    private static Vector3 GetUpwardNormal(Vector3 normal)
    {
        Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        if (Vector3.Dot(n, Vector3.up) < 0f)
            n = -n;
        return n;
    }

    private void RemoveAnyCollisionOtherCollider(Collider other)
    {
        if (other == null)
            return;

        _anyCollisionOtherColliders.Remove(other);
        _hasAnyCollisionContact = _anyCollisionOtherColliders.Count > 0;

        if (!_hasAnyCollisionContact)
        {
            _anyCollisionOtherCollider = null;
            return;
        }

        if (_anyCollisionOtherCollider == other || _anyCollisionOtherCollider == null || !_anyCollisionOtherColliders.Contains(_anyCollisionOtherCollider))
        {
            foreach (Collider candidate in _anyCollisionOtherColliders)
            {
                _anyCollisionOtherCollider = candidate;
                return;
            }
        }
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
