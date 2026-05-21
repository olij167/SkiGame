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
    private const string ProbeDebugVersion = "SkiContact Probe Debug v3 - explicit-source";

    private enum ProbeGeometrySource
    {
        ExplicitTransform,
        ManualLocalZ,
        InferredColliderBounds,
        FallbackDefault
    }

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

    [Tooltip("Max additional distance for front/rear ski-end probes. Higher than base contact so pitched tip/tail landings can reacquire snow.")]
    [SerializeField] private float endProbeContactDistance = 0.35f;

    [Tooltip("Extra tolerance for front/rear probes when the high-origin recovery probe is used.")]
    [SerializeField] private float endProbeRecoveryExtraContactDistance = 0.45f;

    [Tooltip("Tip/tail probes only count as dominant end contact when they are this much closer than base support.")]
    [SerializeField] private float endDominanceMargin = 0.04f;

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

    [Header("Probe Geometry Overrides")]
    [SerializeField] private Transform explicitTipProbe;
    [SerializeField] private Transform explicitTailProbe;
    [SerializeField] private Transform explicitBaseProbe;
    [SerializeField] private Transform explicitTipBottomProbe;
    [SerializeField] private Transform explicitTailBottomProbe;
    [SerializeField] private bool useBottomEndProbes = true;
    [SerializeField] private float bottomEndProbeLocalYOffset = -0.08f;
    [SerializeField] private bool useManualLocalProbeZ;
    [SerializeField] private float manualTipLocalZ = 0.85f;
    [SerializeField] private float manualTailLocalZ = -0.85f;
    [SerializeField] private float fallbackSkiHalfLength = 1.45f;

    [Tooltip("Small expansion used when polling this ski's existing colliders for Ground contact. This is collision discovery, not probe support.")]
    [SerializeField] private float collisionPollSkin = 0.025f;

    public Transform ExplicitTipBottomProbe => explicitTipBottomProbe;
    public Transform ExplicitTailBottomProbe => explicitTailBottomProbe;
    public bool UseBottomEndProbes => useBottomEndProbes;

    /// <summary>True if this ski currently has any ground contact this frame.</summary>
    public bool IsGrounded { get; private set; }

    public bool HasBaseSupport { get; private set; }
    public bool HasGroundCollision => _hasCollisionContact;
    public bool HasStableSupport => _hasCollisionContact;
    public bool HasEndContact { get; private set; }
    public bool HasAnyProbeContact => _probeBaseHit || _probeTipHit || _probeTailHit;

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
        return GetProbeWorldPositionWithSource(region, out _);
    }

    private Vector3 GetProbeWorldPositionWithSource(SkiProbeRegion region, out ProbeGeometrySource source)
    {
        if (!_geometryInitialized)
            EnsureGeometry();

        switch (region)
        {
            case SkiProbeRegion.Front:
                if (explicitTipProbe != null)
                {
                    source = ProbeGeometrySource.ExplicitTransform;
                    return explicitTipProbe.position;
                }
                break;
            case SkiProbeRegion.Rear:
                if (explicitTailProbe != null)
                {
                    source = ProbeGeometrySource.ExplicitTransform;
                    return explicitTailProbe.position;
                }
                break;
            case SkiProbeRegion.Mid:
                if (explicitBaseProbe != null)
                {
                    source = ProbeGeometrySource.ExplicitTransform;
                    return explicitBaseProbe.position;
                }
                break;
        }

        float z = 0f;
        switch (region)
        {
            case SkiProbeRegion.Front:
                source = useManualLocalProbeZ ? ProbeGeometrySource.ManualLocalZ : (_inferredGeometryValid ? ProbeGeometrySource.InferredColliderBounds : ProbeGeometrySource.FallbackDefault);
                z = useManualLocalProbeZ ? manualTipLocalZ : _localTipZ;
                break;
            case SkiProbeRegion.Rear:
                source = useManualLocalProbeZ ? ProbeGeometrySource.ManualLocalZ : (_inferredGeometryValid ? ProbeGeometrySource.InferredColliderBounds : ProbeGeometrySource.FallbackDefault);
                z = useManualLocalProbeZ ? manualTailLocalZ : _localTailZ;
                break;
            case SkiProbeRegion.Mid:
            default:
                source = ProbeGeometrySource.FallbackDefault;
                z = 0f;
                break; // bindings/base region
        }

        return transform.TransformPoint(new Vector3(0f, 0f, z));
    }

    private ProbeGeometrySource GetProbeSource(SkiProbeRegion region)
    {
        GetProbeWorldPositionWithSource(region, out ProbeGeometrySource source);
        return source;
    }

    private string DescribeProbeSource(SkiProbeRegion region)
    {
        Transform explicitTransform = null;
        switch (region)
        {
            case SkiProbeRegion.Front:
                explicitTransform = explicitTipProbe;
                break;
            case SkiProbeRegion.Rear:
                explicitTransform = explicitTailProbe;
                break;
            case SkiProbeRegion.Mid:
                explicitTransform = explicitBaseProbe;
                break;
        }

        Vector3 world = GetProbeWorldPositionWithSource(region, out ProbeGeometrySource source);
        Vector3 local = transform.InverseTransformPoint(world);
        string explicitPath = explicitTransform != null ? GetTransformPath(explicitTransform) : "(none)";
        bool active = explicitTransform != null && explicitTransform.gameObject.activeInHierarchy;
        bool child = explicitTransform != null && explicitTransform.IsChildOf(transform);
        bool far = local.magnitude > 8f;
        return $"source={source} transform={explicitPath} active={active} childOfSki={child} far={far} local={local.ToString("F3")} world={world.ToString("F3")} localZ={local.z:0.000}";
    }

    public Vector3 GetBottomEndProbeWorldPosition(SkiProbeRegion region)
    {
        if (region == SkiProbeRegion.Front && explicitTipBottomProbe != null)
            return explicitTipBottomProbe.position;

        if (region == SkiProbeRegion.Rear && explicitTailBottomProbe != null)
            return explicitTailBottomProbe.position;

        Vector3 endPoint = GetProbeWorldPosition(region);
        return endPoint + (-transform.up * Mathf.Abs(bottomEndProbeLocalYOffset));
    }

    private string DescribeBottomProbeSource(SkiProbeRegion region)
    {
        Transform explicitTransform = region == SkiProbeRegion.Front ? explicitTipBottomProbe : explicitTailBottomProbe;
        Vector3 world = GetBottomEndProbeWorldPosition(region);
        Vector3 local = transform.InverseTransformPoint(world);
        string source = explicitTransform != null ? ProbeGeometrySource.ExplicitTransform.ToString() : $"DerivedFrom{GetProbeSource(region)}";
        string explicitPath = explicitTransform != null ? GetTransformPath(explicitTransform) : "(none)";
        bool active = explicitTransform != null && explicitTransform.gameObject.activeInHierarchy;
        bool child = explicitTransform != null && explicitTransform.IsChildOf(transform);
        bool far = local.magnitude > 8f;
        return $"source={source} transform={explicitPath} active={active} childOfSki={child} far={far} local={local.ToString("F3")} world={world.ToString("F3")} localZ={local.z:0.000}";
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
    private float _probeBaseDistance = float.PositiveInfinity;
    private float _probeTipDistance = float.PositiveInfinity;
    private float _probeTailDistance = float.PositiveInfinity;

    public bool ProbeBaseHit => _probeBaseHit;
    public bool ProbeTipHit => _probeTipHit;
    public bool ProbeTailHit => _probeTailHit;
    public Collider ProbeBaseCollider => _probeBaseCollider;
    public Collider ProbeTipCollider => _probeTipCollider;
    public Collider ProbeTailCollider => _probeTailCollider;
    public float ProbeBaseDistance => _probeBaseDistance;
    public float ProbeTipDistance => _probeTipDistance;
    public float ProbeTailDistance => _probeTailDistance;

    public Vector3 ProbeBaseNormal => _probeBaseNormal.sqrMagnitude > 0.0001f ? _probeBaseNormal.normalized : Vector3.up;
    public Vector3 ProbeTipNormal => _probeTipNormal.sqrMagnitude > 0.0001f ? _probeTipNormal.normalized : Vector3.up;
    public Vector3 ProbeTailNormal => _probeTailNormal.sqrMagnitude > 0.0001f ? _probeTailNormal.normalized : Vector3.up;
    public Vector3 CollisionContactNormal => _collisionContactNormal.sqrMagnitude > 0.0001f ? _collisionContactNormal.normalized : Vector3.up;

    // NOTE: Do not sample in this script's FixedUpdate.
    // SkiController will call this deterministically at the start of its FixedUpdate.
    public void ManualSampleGround()
    {
        SampleGround();
        RefreshCollisionContactFromOwnColliders();
    }

    private float _localTipZ;
    private float _localTailZ;
    private bool _geometryInitialized;
    private bool _inferredGeometryValid;
    private bool _usingFallbackGeometry;
    private string _geometryWarning;

    // Collision-based contact cache (used when rays miss but colliders touch).
    private bool _hasCollisionContact;
    private Vector3 _collisionContactPoint;
    private Vector3 _collisionContactNormal;
    private float _lastCollisionContactTime = -999f;
    private string _lastCollisionSource = "none";
    private Collider _lastCollisionThisCollider;
    private string _lastCollisionPollRejectReason = "none";

    // NEW: which collider we are actually touching (so SkiController can detect grindables without global searches)
    private Collider _collisionOtherCollider;

    // Wall-contact cache (collision on groundLayers but too steep to count as ground).
    // Used by SkiController to detect airborne cliff scrapes and suppress "ground-like" alignment/forces.
    private bool _hasWallContact;
    private Vector3 _wallContactPoint;
    private Vector3 _wallContactNormal;
    private Collider _wallOtherCollider;

    private bool _collisionEndContact;
    private int _collisionEndSign;
    private float _collisionEndLocalZ;
    private Vector3 _collisionEndPoint;
    private Vector3 _collisionEndNormal = Vector3.up;
    private Collider _collisionEndCollider;

    private Rigidbody _ownerRigidbody;
    private Transform _ownerRoot;
    private Collider[] _ownContactColliders;
    private static int _npcLayer = -2;
    private static int _playerLayer = -2;


    private readonly RaycastHit[] _probeHitBuffer = new RaycastHit[12];
    private readonly Collider[] _collisionOverlapBuffer = new Collider[16];
    /// <summary>True if we have a collision contact on groundLayers that is too steep to be considered ground.</summary>
    public bool HasWallContact => _hasWallContact;

    /// <summary>Last cached wall contact normal (unit-ish). Meaningful when HasWallContact is true.</summary>
    public Vector3 WallContactNormal => _wallContactNormal;

    /// <summary>Last cached wall contact point. Meaningful when HasWallContact is true.</summary>
    public Vector3 WallContactPoint => _wallContactPoint;

    /// <summary>The collider we are scraping (can be null).</summary>
    public Collider WallOtherCollider => _wallOtherCollider;

    public bool HasCollisionEndContact => _collisionEndContact;
    public int CollisionEndSign => _collisionEndSign;
    public float CollisionEndLocalZ => _collisionEndLocalZ;
    public Vector3 CollisionEndPoint => _collisionEndPoint;
    public Vector3 CollisionEndNormal => _collisionEndNormal;
    public Collider CollisionEndCollider => _collisionEndCollider;

    /// <summary>True if we have an active collision-derived ground contact.</summary>
    public bool HasCollisionContact => _hasCollisionContact;
    public float LastCollisionContactTime => _lastCollisionContactTime;
    public string LastCollisionSource => _lastCollisionSource;
    public Collider LastCollisionThisCollider => _lastCollisionThisCollider;
    public string LastCollisionPollRejectReason => _lastCollisionPollRejectReason;

    /// <summary>
    /// True when this ski has stable physical support from an actual valid ground collision.
    /// </summary>
    public bool HasStableSupportContact => HasStableSupport;

    /// <summary>True when this ski's base/under-foot probe sees ground. Probe info only; not physical support.</summary>
    public bool HasBaseSupportContact => HasBaseSupport;

    /// <summary>True when this ski has collision-derived ground support.</summary>
    public bool HasCollisionSupportContact => _hasCollisionContact;

    /// <summary>
    /// True when only an end-region probe/contact is active. This is useful for
    /// landing classification, nose/tail slides, and temporary recovery, but it
    /// should not be treated as full skiing support by SkiController.
    /// </summary>
    public bool HasProbeOnlyEndContact =>
        !HasStableSupport &&
        HasEndContact &&
        EndContactSign != 0;

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
    private void Awake()
    {
        _ownerRigidbody = GetComponentInParent<Rigidbody>();
        _ownerRoot = _ownerRigidbody != null ? _ownerRigidbody.transform : transform.root;
        RefreshOwnContactColliders();
        CacheCharacterLayers();
    }

    private void RefreshOwnContactColliders()
    {
        _ownContactColliders = GetComponentsInChildren<Collider>(true);
    }

    private static void CacheCharacterLayers()
    {
        if (_npcLayer == -2)
            _npcLayer = LayerMask.NameToLayer("NPC");

        if (_playerLayer == -2)
            _playerLayer = LayerMask.NameToLayer("Player");
    }

    private bool IsOwnCollider(Collider c)
    {
        if (c == null)
            return false;

        if (_ownerRigidbody != null && c.attachedRigidbody == _ownerRigidbody)
            return true;

        if (_ownerRoot != null && c.transform.IsChildOf(_ownerRoot))
            return true;

        return false;
    }

    private bool IsCharacterLayer(Collider c)
    {
        if (c == null)
            return false;

        CacheCharacterLayers();
        int layer = c.gameObject.layer;
        return (_npcLayer >= 0 && layer == _npcLayer) ||
               (_playerLayer >= 0 && layer == _playerLayer);
    }

    private bool IsValidExternalGroundCollider(Collider c)
    {
        if (c == null)
            return false;

        if (IsOwnCollider(c) || IsCharacterLayer(c))
            return false;

        return (groundLayers.value & (1 << c.gameObject.layer)) != 0;
    }

    private bool IsValidExternalContactCollider(Collider c)
    {
        return c != null && !IsOwnCollider(c) && !IsCharacterLayer(c);
    }

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
        float baseContactMaxDist = Mathf.Max(0.001f, probeUpOffset + probeContactDistance + poseContactExtra);
        float endContactMaxDist = Mathf.Max(0.001f, probeUpOffset + endProbeContactDistance + poseContactExtra);

        // Base / under-foot
        Vector3 baseOrigin = GetProbeWorldPosition(SkiProbeRegion.Mid) + worldUp * probeUpOffset;

        // Tip and tail origins
        Vector3 tipOrigin = GetProbeWorldPosition(SkiProbeRegion.Front) + worldUp * probeUpOffset;
        Vector3 tailOrigin = GetProbeWorldPosition(SkiProbeRegion.Rear) + worldUp * probeUpOffset;

        bool IsProbeRideable(RaycastHit h)
        {
            Vector3 n = GetUpwardNormal(h.normal);
            return Vector3.Dot(n, Vector3.up) >= minProbeUpDot;
        }

        bool TrySphereDown(Vector3 origin, float extraMaxDistance, out RaycastHit bestHit)
        {
            bestHit = default;

            int count = Physics.SphereCastNonAlloc(
                origin,
                probeSphereRadius,
                worldDown,
                _probeHitBuffer,
                maxDist + extraMaxDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _probeHitBuffer[i];

                if (hit.collider == null)
                    continue;

                if (!IsValidExternalGroundCollider(hit.collider))
                    continue;

                if (!IsProbeRideable(hit))
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        bool TryProbeBest(Vector3 origin, bool isEndProbe, out RaycastHit bestHit)
        {
            bool found = false;
            bestHit = default;

            bool ConsiderProbeFromOrigin(Vector3 probeOrigin, float extraMaxDistance, ref RaycastHit currentBest, ref bool hasBest)
            {
                if (!TrySphereDown(probeOrigin, extraMaxDistance, out RaycastHit hit))
                    return false;

                float allowedContactDistance = isEndProbe ? endContactMaxDist : baseContactMaxDist;
                allowedContactDistance += Mathf.Max(0f, extraMaxDistance);
                if (isEndProbe && extraMaxDistance > 0f)
                    allowedContactDistance += Mathf.Max(0f, endProbeRecoveryExtraContactDistance);
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
        HasBaseSupport = false;
        HasEndContact = false;
        _probeBaseCollider = null;
        _probeTipCollider = null;
        _probeTailCollider = null;
        _probeBaseDistance = float.PositiveInfinity;
        _probeTipDistance = float.PositiveInfinity;
        _probeTailDistance = float.PositiveInfinity;

        // Probe hits
        bool gotBase = TryProbeBest(baseOrigin, false, out RaycastHit baseHit);
        bool gotTip = TryProbeBest(tipOrigin, true, out RaycastHit tipHit);
        bool gotTail = TryProbeBest(tailOrigin, true, out RaycastHit tailHit);
        bool gotFrontMid = false;
        bool gotRearMid = false;
        RaycastHit frontMidHit = default;
        RaycastHit rearMidHit = default;

        if (!gotBase && useIntermediateLengthProbes)
        {
            float probeFraction = Mathf.Clamp(intermediateProbeFraction, 0.15f, 0.85f);
            Vector3 frontMidOrigin = Vector3.Lerp(baseOrigin, tipOrigin, probeFraction);
            Vector3 rearMidOrigin = Vector3.Lerp(baseOrigin, tailOrigin, probeFraction);

            gotFrontMid = TryProbeBest(frontMidOrigin, true, out frontMidHit);
            gotRearMid = TryProbeBest(rearMidOrigin, true, out rearMidHit);

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

        if (useBottomEndProbes)
        {
            if (!gotTip)
            {
                Vector3 tipBottomOrigin = GetBottomEndProbeWorldPosition(SkiProbeRegion.Front) + worldUp * probeUpOffset;
                gotTip = TryProbeBest(tipBottomOrigin, true, out tipHit);
            }

            if (!gotTail)
            {
                Vector3 tailBottomOrigin = GetBottomEndProbeWorldPosition(SkiProbeRegion.Rear) + worldUp * probeUpOffset;
                gotTail = TryProbeBest(tailBottomOrigin, true, out tailHit);
            }
        }

        if (gotBase)
        {
            _probeBaseHit = true;
            _probeBasePoint = baseHit.point;
            _probeBaseNormal = (baseHit.normal.sqrMagnitude > 0.0001f) ? baseHit.normal.normalized : Vector3.up;
            _probeBaseCollider = baseHit.collider;
            _probeBaseDistance = baseHit.distance;
        }

        if (gotTip)
        {
            _probeTipHit = true;
            _probeTipPoint = tipHit.point;
            _probeTipNormal = (tipHit.normal.sqrMagnitude > 0.0001f) ? tipHit.normal.normalized : Vector3.up;
            _probeTipCollider = tipHit.collider;
            _probeTipDistance = tipHit.distance;
        }

        if (gotTail)
        {
            _probeTailHit = true;
            _probeTailPoint = tailHit.point;
            _probeTailNormal = (tailHit.normal.sqrMagnitude > 0.0001f) ? tailHit.normal.normalized : Vector3.up;
            _probeTailCollider = tailHit.collider;
            _probeTailDistance = tailHit.distance;
        }

        HasBaseSupport = gotBase;
        bool anyProbeHit = gotBase || gotTip || gotTail;
        bool anyHit = _hasCollisionContact || anyProbeHit;

        // If no fresh hit exists, clear the ski support immediately. Control coyote
        // lives in SkiController; SkiContact should report current physical/probe state.
        if (!anyHit)
        {
            IsGrounded = false;
            HasBaseSupport = false;
            HasEndContact = false;
            HasTipContact = false;
            EndContactSign = 0;
            EndContactLocalZ = 0f;
            BaseContactAlignment = 0f;

            _probeBaseHit = false;
            _probeTipHit = false;
            _probeTailHit = false;
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

        IsGrounded = HasStableSupport;
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

        // End-region detection: tip/tail only dominate when base support is absent
        // or the end probe is meaningfully closer than the base.
        bool endish = false;
        float bestAbsEndZ = 0f;
        float bestEndZ = 0f;
        int bestEndSign = 0;

        bool tipDominatesBase = gotTip && (!HasBaseSupport || tipHit.distance + endDominanceMargin < baseHit.distance);
        bool tailDominatesBase = gotTail && (!HasBaseSupport || tailHit.distance + endDominanceMargin < baseHit.distance);

        if (gotTip && tipDominatesBase)
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

        if (gotTail && tailDominatesBase)
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

        if (!endish && _hasCollisionContact && _collisionEndContact && !HasBaseSupport)
        {
            endish = true;
            bestEndZ = _collisionEndLocalZ;
            bestEndSign = _collisionEndSign;
        }

        HasTipContact = endish; // legacy name: "tip OR tail"
        HasEndContact = endish;
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
        _inferredGeometryValid = false;
        _usingFallbackGeometry = false;
        _geometryWarning = string.Empty;

        if (useManualLocalProbeZ)
        {
            _localTipZ = manualTipLocalZ;
            _localTailZ = manualTailLocalZ;
            return;
        }

        // Sensible fallbacks if we can't inspect a collider.
        float fallbackHalfLength = Mathf.Max(0.05f, fallbackSkiHalfLength);
        _localTipZ = fallbackHalfLength;
        _localTailZ = -fallbackHalfLength;
        _usingFallbackGeometry = true;

        // Try to infer the ski's local tip & tail positions from its collider bounds.
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
            return;

        float maxLocalZ = float.NegativeInfinity;
        float minLocalZ = float.PositiveInfinity;
        bool usedSolidCollider = false;

        void ConsiderCollider(Collider col)
        {
            if (col == null)
                return;

            Bounds b = col.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;

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
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            ConsiderCollider(col);
            usedSolidCollider = true;
        }

        if (!usedSolidCollider)
        {
            for (int i = 0; i < colliders.Length; i++)
                ConsiderCollider(colliders[i]);
        }

        if (!float.IsNegativeInfinity(maxLocalZ) && !float.IsPositiveInfinity(minLocalZ))
        {
            float length = Mathf.Abs(maxLocalZ - minLocalZ);
            bool sane =
                length >= 0.25f &&
                length <= 6f &&
                Mathf.Abs(maxLocalZ) <= 4f &&
                Mathf.Abs(minLocalZ) <= 4f &&
                maxLocalZ > 0f &&
                minLocalZ < 0f;

            if (sane)
            {
                _localTipZ = maxLocalZ;
                _localTailZ = minLocalZ;
                _inferredGeometryValid = true;
                _usingFallbackGeometry = false;
            }
            else
            {
                _geometryWarning = $"Rejected inferred probe bounds tip={maxLocalZ:0.000} tail={minLocalZ:0.000} length={length:0.000}; using fallback +/-{fallbackHalfLength:0.000}.";
            }
        }
        else
        {
            _geometryWarning = $"No collider bounds available for probe inference; using fallback +/-{fallbackHalfLength:0.000}.";
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

        ClearCollisionEndContact();

        // Cache *any* collision contact (NOT layer-filtered) so systems like grinding
        // can detect rails/props that are not part of groundLayers.
        {
            ContactPoint cp0 = collision.GetContact(0);
            AddAnyCollisionOtherCollider(cp0.otherCollider);
        }

        // Only treat as "ground collision contact" if the other object is on groundLayers.
        // Pick the "most ground-like" contact (highest up-dot).
        float bestUpDot = -1f;
        Vector3 bestPoint = default;
        Vector3 bestNormal = default;
        Collider bestOther = null;
        Collider bestThis = null;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint cp = collision.GetContact(i);
            Vector3 n = GetUpwardNormal(cp.normal);

            if (cp.thisCollider != null && !cp.thisCollider.transform.IsChildOf(transform))
                continue;

            if (!IsValidExternalGroundCollider(cp.otherCollider))
                continue;

            float upDot = Vector3.Dot(n, Vector3.up);
            if (upDot > bestUpDot)
            {
                bestUpDot = upDot;
                bestPoint = cp.point;
                bestNormal = n;
                bestOther = cp.otherCollider;
                bestThis = cp.thisCollider;
            }
        }

        if (bestOther == null)
            return;

        // If it's too steep to be ground, cache as a "wall contact" instead of grounding.
        if (bestUpDot < minCollisionUpDot)
        {
            _hasCollisionContact = false;
            _collisionOtherCollider = null;
            ClearCollisionEndContact();
            _hasWallContact = true;
            _wallContactPoint = bestPoint;
            _wallContactNormal = bestNormal;
            _wallOtherCollider = bestOther;
            return;
        }

        CacheGroundCollisionContact(bestPoint, bestNormal, bestOther, bestThis, "collision-callback");
    }

    private void CacheGroundCollisionContact(Vector3 point, Vector3 normal, Collider other, Collider thisCollider, string source)
    {
        _hasWallContact = false;
        _wallOtherCollider = null;

        Vector3 resolvedNormal = ResolveGroundNormalForCollisionSupport(other, point, normal);

        _hasCollisionContact = true;
        _collisionContactPoint = point;
        _collisionContactNormal = resolvedNormal.sqrMagnitude > 0.0001f ? resolvedNormal.normalized : Vector3.up;
        _collisionOtherCollider = other;
        _lastCollisionContactTime = Time.time;
        _lastCollisionSource = string.IsNullOrEmpty(source) ? "unknown" : source;
        _lastCollisionThisCollider = thisCollider;
        _lastCollisionPollRejectReason = "accepted";

        float localZ = transform.InverseTransformPoint(point).z;
        bool isTip = localZ >= tipRegionLocalZThreshold;
        bool isTail = localZ <= tailRegionLocalZThreshold;

        _collisionEndContact = isTip || isTail;
        _collisionEndSign = isTip ? 1 : isTail ? -1 : 0;
        _collisionEndLocalZ = _collisionEndContact ? localZ : 0f;
        _collisionEndPoint = point;
        _collisionEndNormal = _collisionContactNormal;
        _collisionEndCollider = other;

        // Keep the public contact normal slope-aware even when support came from overlap polling.
        // This is support classification + terrain-normal resolution, not probe-based grounding.
        ContactNormal = _collisionContactNormal;
        ContactPoint = point;
        LastContactTime = Time.time;
    }

    private Vector3 ResolveGroundNormalForCollisionSupport(Collider expectedGroundCollider, Vector3 supportPoint, Vector3 fallbackNormal)
    {
        Vector3 weighted = Vector3.zero;
        float totalWeight = 0f;

        void AddProbeNormal(bool hit, Collider collider, Vector3 normal, float distance, float baseWeight)
        {
            if (!hit || collider == null)
                return;

            if (expectedGroundCollider != null && collider != expectedGroundCollider)
                return;

            Vector3 n = GetUpwardNormal(normal);
            if (n.sqrMagnitude <= 0.0001f)
                return;

            float upDot = Vector3.Dot(n.normalized, Vector3.up);
            if (upDot < minProbeUpDot)
                return;

            // Closer probes should be trusted more. Clamp so tiny distances do not explode.
            float distanceWeight = 1f / Mathf.Max(0.08f, distance);
            float w = Mathf.Max(0.01f, baseWeight) * distanceWeight;

            weighted += n.normalized * w;
            totalWeight += w;
        }

        // Prefer actual terrain normals from ski probes. The overlap-poll support normal
        // is often world-up because it is derived from ClosestPoint/bounds separation.
        AddProbeNormal(_probeBaseHit, _probeBaseCollider, _probeBaseNormal, _probeBaseDistance, 2.5f);
        AddProbeNormal(_probeTipHit, _probeTipCollider, _probeTipNormal, _probeTipDistance, 1.4f);
        AddProbeNormal(_probeTailHit, _probeTailCollider, _probeTailNormal, _probeTailDistance, 1.4f);

        if (totalWeight > 0.0001f && weighted.sqrMagnitude > 0.0001f)
            return weighted.normalized;

        // If probes missed this frame, ray/spherecast near the support point to recover
        // the terrain normal. This still does not grant support; it only resolves normal.
        if (TryResolveGroundNormalNearPoint(supportPoint, expectedGroundCollider, out Vector3 sampledNormal))
            return sampledNormal;

        Vector3 fallback = GetUpwardNormal(fallbackNormal);
        if (fallback.sqrMagnitude > 0.0001f && Vector3.Dot(fallback.normalized, Vector3.up) >= minCollisionUpDot)
            return fallback.normalized;

        if (ContactNormal.sqrMagnitude > 0.0001f && Vector3.Dot(ContactNormal.normalized, Vector3.up) >= minCollisionUpDot)
            return ContactNormal.normalized;

        return Vector3.up;
    }

    private bool TryResolveGroundNormalNearPoint(Vector3 supportPoint, Collider expectedGroundCollider, out Vector3 normal)
    {
        normal = Vector3.up;

        Vector3 origin = supportPoint + Vector3.up * Mathf.Max(0.08f, probeUpOffset + 0.08f);
        float maxDistance = Mathf.Max(
            0.25f,
            probeUpOffset + probeDistance + ComputePoseProbeExtraDistance() + 0.2f);

        int count = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, probeSphereRadius),
            Vector3.down,
            _probeHitBuffer,
            maxDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        Vector3 bestNormal = Vector3.up;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _probeHitBuffer[i];

            if (hit.collider == null)
                continue;

            if (expectedGroundCollider != null && hit.collider != expectedGroundCollider)
                continue;

            if (!IsValidExternalGroundCollider(hit.collider))
                continue;

            Vector3 n = GetUpwardNormal(hit.normal);
            if (n.sqrMagnitude <= 0.0001f)
                continue;

            if (Vector3.Dot(n.normalized, Vector3.up) < minProbeUpDot)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestNormal = n.normalized;
                found = true;
            }
        }

        if (!found)
            return false;

        normal = bestNormal;
        return true;
    }

    private void RefreshCollisionContactFromOwnColliders()
    {
        if (_ownContactColliders == null || _ownContactColliders.Length == 0)
            RefreshOwnContactColliders();

        if (_ownContactColliders == null || _ownContactColliders.Length == 0)
            return;

        float bestUpDot = -1f;
        Vector3 bestPoint = default;
        Vector3 bestNormal = Vector3.up;
        Collider bestOther = null;
        Collider bestThis = null;
        bool sawCandidate = false;
        _lastCollisionPollRejectReason = "no-overlap";

        for (int i = 0; i < _ownContactColliders.Length; i++)
        {
            Collider own = _ownContactColliders[i];
            if (own == null || !own.enabled || own.isTrigger || !own.transform.IsChildOf(transform))
                continue;

            Bounds b = own.bounds;
            Vector3 halfExtents = b.extents + Vector3.one * Mathf.Max(0.001f, collisionPollSkin);
            int count = Physics.OverlapBoxNonAlloc(
                b.center,
                halfExtents,
                _collisionOverlapBuffer,
                Quaternion.identity,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            if (count <= 0)
                continue;

            sawCandidate = true;
            for (int h = 0; h < count; h++)
            {
                Collider other = _collisionOverlapBuffer[h];
                if (other == null)
                    continue;

                if (!IsValidExternalGroundCollider(other))
                {
                    _lastCollisionPollRejectReason = "invalid-ground-collider";
                    continue;
                }

                Vector3 point = other.ClosestPoint(b.center);
                Vector3 n = b.center - point;
                if (n.sqrMagnitude <= 0.000001f)
                    n = Vector3.up;
                else
                    n.Normalize();

                n = GetUpwardNormal(n);
                float upDot = Vector3.Dot(n, Vector3.up);
                if (upDot < minCollisionUpDot)
                {
                    _lastCollisionPollRejectReason = "non-rideable-normal";
                    continue;
                }

                if (upDot > bestUpDot)
                {
                    bestUpDot = upDot;
                    bestPoint = point;
                    bestNormal = n;
                    bestOther = other;
                    bestThis = own;
                }
            }
        }

        if (bestOther != null)
        {
            CacheGroundCollisionContact(bestPoint, bestNormal, bestOther, bestThis, "collider-overlap-poll");
            return;
        }

        if (!sawCandidate)
            _lastCollisionPollRejectReason = "no-ground-overlap";

        if (_hasCollisionContact && Time.time - _lastCollisionContactTime > Time.fixedDeltaTime * 2.5f)
        {
            _hasCollisionContact = false;
            _collisionOtherCollider = null;
            _lastCollisionSource = "expired";
            _lastCollisionThisCollider = null;
            ClearCollisionEndContact();
        }
    }

    private void ClearCollisionEndContact()
    {
        _collisionEndContact = false;
        _collisionEndSign = 0;
        _collisionEndLocalZ = 0f;
        _collisionEndPoint = default;
        _collisionEndNormal = Vector3.up;
        _collisionEndCollider = null;
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

        if (other != null && other == _collisionEndCollider)
            ClearCollisionEndContact();

        // Only clear the "ground collision" cache if this was a ground-layer collision.
        if (!IsValidExternalGroundCollider(other))
            return;

        if (_collisionOtherCollider != null && other != _collisionOtherCollider)
            return;

        _hasCollisionContact = false;
        _collisionOtherCollider = null;
        ClearCollisionEndContact();
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
        HasBaseSupport = false;
        HasEndContact = false;
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
        _probeBaseDistance = float.PositiveInfinity;
        _probeTipDistance = float.PositiveInfinity;
        _probeTailDistance = float.PositiveInfinity;

        _hasCollisionContact = false;
        _collisionContactPoint = default;
        _collisionContactNormal = Vector3.up;
        _collisionOtherCollider = null;
        _lastCollisionContactTime = -999f;
        _lastCollisionSource = "reset";
        _lastCollisionThisCollider = null;
        _lastCollisionPollRejectReason = "reset";
        ClearCollisionEndContact();

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
        RefreshOwnContactColliders();
        ResetContactState();
    }

    [ContextMenu("Print Ski Probe Geometry Debug")]
    public void PrintSkiProbeGeometryDebug()
    {
        EnsureGeometry();

        Debug.Log(
            $"[{nameof(SkiContact)}] Probe Geometry Debug: {name}\n" +
            $"version={ProbeDebugVersion}\n" +
            $"localTipZ={_localTipZ:0.000} localTailZ={_localTailZ:0.000} manual={useManualLocalProbeZ} inferredValid={_inferredGeometryValid} usingFallback={_usingFallbackGeometry} warning={(_geometryWarning ?? string.Empty)}\n" +
            $"baseProbe: {DescribeProbeSource(SkiProbeRegion.Mid)}\n" +
            $"tipProbe: {DescribeProbeSource(SkiProbeRegion.Front)}\n" +
            $"tailProbe: {DescribeProbeSource(SkiProbeRegion.Rear)}\n" +
            $"tipBottomAssigned={explicitTipBottomProbe != null} tailBottomAssigned={explicitTailBottomProbe != null} useBottomEndProbes={useBottomEndProbes} bottomEndProbeLocalYOffset={bottomEndProbeLocalYOffset:0.000}\n" +
            $"bottomTipProbe: {DescribeBottomProbeSource(SkiProbeRegion.Front)}\n" +
            $"bottomTailProbe: {DescribeBottomProbeSource(SkiProbeRegion.Rear)}\n" +
            $"probeUpOffset={probeUpOffset:0.000} probeDistance={probeDistance:0.000} probeContactDistance={probeContactDistance:0.000} endProbeContactDistance={endProbeContactDistance:0.000} poseExtra={ComputePoseProbeExtraDistance():0.000}\n" +
            $"baseHit={_probeBaseHit} baseCollider={DescribeCollider(_probeBaseCollider)} baseDistance={_probeBaseDistance:0.000}\n" +
            $"tipHit={_probeTipHit} tipCollider={DescribeCollider(_probeTipCollider)} tipDistance={_probeTipDistance:0.000}\n" +
            $"tailHit={_probeTailHit} tailCollider={DescribeCollider(_probeTailCollider)} tailDistance={_probeTailDistance:0.000}\n" +
            $"hasCollisionContact={_hasCollisionContact} lastCollisionAge={(Time.time - _lastCollisionContactTime):0.000}s collisionCollider={DescribeCollider(_collisionOtherCollider)} collisionSource={_lastCollisionSource} thisCollider={DescribeCollider(_lastCollisionThisCollider)} pollReject={_lastCollisionPollRejectReason}\n" +
            $"collisionEndContact={_collisionEndContact} collisionEndSign={_collisionEndSign} collisionEndLocalZ={_collisionEndLocalZ:0.000} collisionEndCollider={DescribeCollider(_collisionEndCollider)} collisionEndNormal={_collisionEndNormal.ToString("F3")} collisionEndPoint={_collisionEndPoint.ToString("F3")}",
            this);
    }

    private static string DescribeCollider(Collider c)
    {
        if (c == null)
            return "(none)";

        int layer = c.gameObject.layer;
        string layerName = LayerMask.LayerToName(layer);
        return $"{c.name}/{(string.IsNullOrEmpty(layerName) ? layer.ToString() : layerName)}";
    }

    private static string GetTransformPath(Transform t)
    {
        if (t == null)
            return "(none)";

        string path = t.name;
        Transform current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    [ContextMenu("Validate Ski Probe Setup")]
    private void ValidateSkiProbeSetup()
    {
        _geometryInitialized = false;
        EnsureGeometry();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool pass = true;

        void CheckProbe(string label, Transform probe, bool required)
        {
            if (probe == null)
            {
                if (required)
                {
                    pass = false;
                    sb.AppendLine($"{label}: missing");
                }
                return;
            }

            Vector3 local = transform.InverseTransformPoint(probe.position);
            bool child = probe.IsChildOf(transform);
            bool far = local.magnitude > 8f;
            if (!child || far)
                pass = false;

            sb.AppendLine($"{label}: {GetTransformPath(probe)} local={local.ToString("F3")} childOfSki={child} far={far} active={probe.gameObject.activeInHierarchy}");
        }

        CheckProbe("BaseProbe", explicitBaseProbe, false);
        CheckProbe("TipProbe", explicitTipProbe, false);
        CheckProbe("TailProbe", explicitTailProbe, false);
        CheckProbe("TipBottomProbe", explicitTipBottomProbe, false);
        CheckProbe("TailBottomProbe", explicitTailBottomProbe, false);

        Vector3 baseLocal = transform.InverseTransformPoint(GetProbeWorldPosition(SkiProbeRegion.Mid));
        Vector3 tipLocal = transform.InverseTransformPoint(GetProbeWorldPosition(SkiProbeRegion.Front));
        Vector3 tailLocal = transform.InverseTransformPoint(GetProbeWorldPosition(SkiProbeRegion.Rear));
        Vector3 tipBottomLocal = transform.InverseTransformPoint(GetBottomEndProbeWorldPosition(SkiProbeRegion.Front));
        Vector3 tailBottomLocal = transform.InverseTransformPoint(GetBottomEndProbeWorldPosition(SkiProbeRegion.Rear));

        bool orderOk = tipLocal.z > baseLocal.z && baseLocal.z > tailLocal.z;
        bool bottomOk = tipBottomLocal.y <= tipLocal.y + 0.05f && tailBottomLocal.y <= tailLocal.y + 0.05f;
        bool explicitLongitudinal = explicitTipProbe != null && explicitTailProbe != null;
        pass &= orderOk && bottomOk && (explicitLongitudinal || !_usingFallbackGeometry);

        sb.AppendLine($"orderOk={orderOk} bottomOk={bottomOk} explicitLongitudinal={explicitLongitudinal} inferredValid={_inferredGeometryValid} usingFallback={_usingFallbackGeometry}");
        if (!string.IsNullOrEmpty(_geometryWarning))
            sb.AppendLine(_geometryWarning);
        sb.AppendLine("Recommended cube ski local positions:");
        sb.AppendLine("BaseProbe=(0,-0.035,0), TipProbe=(0,0,1.45), TailProbe=(0,0,-1.45), TipBottomProbe=(0,-0.035,1.45), TailBottomProbe=(0,-0.035,-1.45)");

        Debug.Log($"[{nameof(SkiContact)}] Validate Ski Probe Setup: {(pass ? "PASS" : "CHECK")}\n{sb}", this);
    }

    private void AddAnyCollisionOtherCollider(Collider other)
    {
        if (other == null)
            return;

        if (!IsValidExternalContactCollider(other))
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
    private void OnValidate()
    {
        _geometryInitialized = false;
        WarnIfLayerMaskContains(groundLayers, "Default", "SkiContact.groundLayers should not include Default. Default props can be grindable, but are not terrain ground.");
        WarnIfLayerMaskContains(groundLayers, "NPC", "SkiContact.groundLayers should not include NPC.");
        WarnIfLayerMaskContains(groundLayers, "Player", "SkiContact.groundLayers should not include Player.");
    }

    private void WarnIfLayerMaskContains(LayerMask mask, string layerName, string message)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0 && (mask.value & (1 << layer)) != 0)
            Debug.LogWarning($"[{nameof(SkiContact)}] {message}", this);
    }
#endif

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        EnsureGeometry();

        DrawProbeGizmo(SkiProbeRegion.Mid, _probeBaseHit ? Color.green : Color.gray);
        DrawProbeGizmo(SkiProbeRegion.Front, _probeTipHit ? Color.cyan : Color.yellow);
        DrawProbeGizmo(SkiProbeRegion.Rear, _probeTailHit ? Color.magenta : Color.yellow);
        DrawBottomProbeGizmo(SkiProbeRegion.Front, Color.blue);
        DrawBottomProbeGizmo(SkiProbeRegion.Rear, Color.red);

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

    private void DrawProbeGizmo(SkiProbeRegion region, Color color)
    {
        Vector3 basePoint = GetProbeWorldPosition(region);
        Vector3 origin = basePoint + Vector3.up * probeUpOffset;
        float radius = Mathf.Max(0.005f, probeSphereRadius);
        float length = Mathf.Max(0.01f, probeDistance + probeUpOffset + ComputePoseProbeExtraDistance());

        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawLine(origin, origin + Vector3.down * length);
    }

    private void DrawBottomProbeGizmo(SkiProbeRegion region, Color color)
    {
        if (!useBottomEndProbes)
            return;

        Vector3 basePoint = GetBottomEndProbeWorldPosition(region);
        Vector3 origin = basePoint + Vector3.up * probeUpOffset;
        float radius = Mathf.Max(0.005f, probeSphereRadius * 0.75f);
        float length = Mathf.Max(0.01f, probeDistance + probeUpOffset + ComputePoseProbeExtraDistance());

        Gizmos.color = color;
        Gizmos.DrawWireCube(origin, Vector3.one * radius * 2f);
        Gizmos.DrawLine(origin, origin + Vector3.down * length);
    }
#endif
}
