using UnityEngine;
using SkiGame.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class RescueStretcherController : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform casualtyMountPoint;
    [SerializeField] private Transform ropeEndPoint;
    [SerializeField] private Transform stretcherTowPoint;
    [SerializeField] private LineRenderer ropeRenderer;
    [SerializeField] private SnowmobileController explicitSnowmobile;
    [SerializeField] private bool autoFindNearestSnowmobileIfUnpaired = true;

    [Header("Spawn")]
    [SerializeField] private float initialSpawnClearance = 0.35f;

    [Header("Tow Rotation")]
    [SerializeField] private float yawAlignTorque = 18f;
    [SerializeField] private float yawAngularDamping = 2.5f;
    [SerializeField] private float maxYawTorque = 45f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float probeStartHeight = 0.9f;
    [SerializeField] private float probeLength = 2.4f;
    [SerializeField] private float rideHeight = 0.15f;
    [SerializeField] private float rideSpringStrength = 70f;
    [SerializeField] private float rideSpringDamping = 10f;
    [SerializeField] private float groundStickForce = 8f;
    [SerializeField] private float alignToGroundSpeed = 8f;

    [Header("Stability")]
    [SerializeField] private float falloffSpeedThreshold = 18f;
    [SerializeField] private float falloffAngularThreshold = 120f;
    [SerializeField] private float stabilityDrainFromSpeed = 0.75f;
    [SerializeField] private float stabilityDrainFromAngular = 0.9f;
    [SerializeField] private float stabilityRecoveryPerSecond = 0.35f;

    [Header("Auto Pickup")]
    [SerializeField] private Collider pickupTrigger;
    [SerializeField] private float casualtyPickupRadius = 2.2f;

    [Header("Tow Rope")]
    [SerializeField] private float ropeLength = 3.0f;
    [SerializeField] private float ropeSlack = 0.2f;
    [SerializeField] private float ropeSpring = 900f;
    [SerializeField] private float ropeDamper = 120f;
    [SerializeField] private float ropeMaxForce = 20000f;
    [SerializeField] private float ropeTolerance = 0.03f;

    [Header("Rope Visual")]
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private float ropeWidth = 0.05f;
    [SerializeField] private int ropeSegmentCount = 8;
    [SerializeField] private float ropeSag = 0.15f;

    [Header("Loaded Casualty Pose")]
    [SerializeField] private Vector3 casualtyLocalPosition = new Vector3(0f, 0.45f, -0.05f);
    [SerializeField] private Vector3 casualtyBackEuler = new Vector3(0f, 0f, 90f);
    [SerializeField] private Vector3 casualtyLeftSideEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 casualtyRightSideEuler = new Vector3(0f, 180f, 0f);
    [SerializeField, Range(0f, 1f)] private float sideLieChance = 0.45f;

    [Header("Loaded Weight")]
    [SerializeField] private float loadedExtraMass = 10f;
    [SerializeField] private Vector3 loadedCenterOfMassOffset = new Vector3(0f, -0.08f, 0f);
    [SerializeField] private float loadedAngularDamping = 3.25f;

    private RescueService _service;
    private SnowmobileController _snowmobile;
    private Transform _towHitch;

    private RescueCasualtyTarget _loadedTarget;
    private int _loadedTargetIndex = -1;
    private float _stability = 1f;

    private bool _isGrounded;
    private Vector3 _groundNormal = Vector3.up;
    private Vector3 _groundPoint = Vector3.zero;

    private float _baseAngularDamping;
    private bool _hasBaseAngularDamping;

    private SpringJoint _towJoint;

    private Transform TowAttachmentTransform =>
    stretcherTowPoint != null
        ? stretcherTowPoint
        : (ropeEndPoint != null ? ropeEndPoint : transform);

    private static readonly Collider[] _pickupResults = new Collider[24];
    private float _nextPickupScanTime;
    [SerializeField] private float pickupScanInterval = 0.05f;

    private Vector3 _baseCenterOfMass;
    private bool _hasBaseCenterOfMass;
    
    private bool _loadedCasualtyLieOnLeft;

    public bool HasLoadedCasualty => _loadedTarget != null;
    public bool IsPaired => _snowmobile != null && _towHitch != null;
    public SnowmobileController PairedSnowmobile => _snowmobile;
    public Transform CasualtyMountPoint => casualtyMountPoint != null ? casualtyMountPoint : transform;

    public void Initialize(RescueService service, SnowmobileController snowmobile)
    {
        _service = service;
        PairToSnowmobile(snowmobile);

        transform.SetParent(null, true);

        EnsureSetup();
        SnapNearTowPoint();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        EnsureSetup();
    }

    private void Start()
    {
        if (_snowmobile == null)
        {
            if (explicitSnowmobile != null)
            {
                PairToSnowmobile(explicitSnowmobile);
            }
            else if (autoFindNearestSnowmobileIfUnpaired)
            {
                PairToSnowmobile(FindNearestSnowmobile());
            }
        }

        if (_snowmobile != null)
            SnapNearTowPoint();
    }

    private void OnDestroy()
    {
        if (_snowmobile != null)
            _snowmobile.UnregisterStretcher(this);
    }

    private void EnsureSetup()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (casualtyMountPoint == null)
            casualtyMountPoint = transform;

        if (ropeEndPoint == null)
            ropeEndPoint = transform;

        if (stretcherTowPoint == null)
            stretcherTowPoint = ropeEndPoint != null ? ropeEndPoint : transform;

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = Mathf.Max(35f, rb.mass);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0.35f;
        rb.angularDamping = 2.25f;

        if (!_hasBaseCenterOfMass)
        {
            _baseCenterOfMass = rb.centerOfMass;
            _hasBaseCenterOfMass = true;
        }

        if (!_hasBaseAngularDamping)
        {
            _baseAngularDamping = rb.angularDamping;
            _hasBaseAngularDamping = true;
        }

        EnsureRopeRenderer();
        EnsureTowJoint();
    }

    public void PairToSnowmobile(SnowmobileController snowmobile)
    {
        if (_snowmobile == snowmobile && _towHitch != null)
            return;

        if (_snowmobile != null)
            _snowmobile.UnregisterStretcher(this);

        _snowmobile = snowmobile;
        _towHitch = _snowmobile != null ? _snowmobile.TowHitchTransform : null;

        if (_snowmobile != null)
            _snowmobile.RegisterStretcher(this);

        EnsureTowJoint();
        UpdateTowJoint();
    }

    public void ClearSnowmobilePairing()
    {
        if (_snowmobile != null)
            _snowmobile.UnregisterStretcher(this);

        _snowmobile = null;
        _towHitch = null;
    }

    private SnowmobileController FindNearestSnowmobile()
    {
        SnowmobileController[] all = FindObjectsOfType<SnowmobileController>();
        if (all == null || all.Length == 0)
            return null;

        SnowmobileController best = null;
        float bestDistSqr = float.PositiveInfinity;
        Vector3 p = transform.position;

        for (int i = 0; i < all.Length; i++)
        {
            SnowmobileController s = all[i];
            if (s == null || s.TowHitchTransform == null)
                continue;

            float d = (s.TowHitchTransform.position - p).sqrMagnitude;
            if (d < bestDistSqr)
            {
                bestDistSqr = d;
                best = s;
            }
        }

        return best;
    }

    private void FixedUpdate()
    {
        if (_snowmobile == null)
        {
            if (explicitSnowmobile != null)
                PairToSnowmobile(explicitSnowmobile);
            else if (autoFindNearestSnowmobileIfUnpaired)
                PairToSnowmobile(FindNearestSnowmobile());
        }

        if (_snowmobile != null && _towHitch == null)
            _towHitch = _snowmobile.TowHitchTransform;

        UpdateGrounding();
        UpdateTowJoint();
        ApplyTowYawAlignment();

        if (_isGrounded)
            ApplyRideSpring();

        UpdateLoadedCasualtyPose();
        UpdateStability();
        ProcessPickupOverlap();
    }

    private void LateUpdate()
    {
        UpdateRopeVisual();
    }

    
    private void SnapNearTowPoint()
    {
        if (_snowmobile == null || _towHitch == null || TowAttachmentTransform == null)
            return;

        Vector3 back = Vector3.ProjectOnPlane(-_towHitch.forward, Vector3.up);
        if (back.sqrMagnitude < 0.001f)
            back = Vector3.back;

        back.Normalize();

        Vector3 desiredTowPointPos = _towHitch.position + back * ropeLength;
        desiredTowPointPos = ProjectToGround(desiredTowPointPos) + Vector3.up * (rideHeight + initialSpawnClearance);

        Vector3 desiredForward = Vector3.ProjectOnPlane(_towHitch.position - desiredTowPointPos, Vector3.up);
        if (desiredForward.sqrMagnitude < 0.001f)
            desiredForward = -back;

        Quaternion desiredRotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);

        Vector3 localTowOffset = transform.InverseTransformPoint(TowAttachmentTransform.position);
        Vector3 worldTowOffset = desiredRotation * localTowOffset;

        rb.position = desiredTowPointPos - worldTowOffset;
        rb.rotation = desiredRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        UpdateTowJoint();
    }

    private void UpdateGrounding()
    {
        _isGrounded = false;
        _groundNormal = Vector3.up;
        _groundPoint = transform.position;

        Vector3 origin = transform.position + Vector3.up * probeStartHeight;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeLength, groundMask, QueryTriggerInteraction.Ignore))
            return;

        _isGrounded = true;
        _groundNormal = hit.normal;
        _groundPoint = hit.point;
    }

    private void ApplyRideSpring()
    {
        float currentHeight = Vector3.Dot(rb.position - _groundPoint, _groundNormal);
        float heightError = rideHeight - currentHeight;
        float velocityAlongNormal = Vector3.Dot(rb.linearVelocity, _groundNormal);

        float springAccel = heightError * rideSpringStrength - velocityAlongNormal * rideSpringDamping;
        rb.AddForce(_groundNormal * springAccel, ForceMode.Acceleration);
        rb.AddForce(-_groundNormal * groundStickForce, ForceMode.Acceleration);
    }

    private void ApplyTowYawAlignment()
    {
        if (_towHitch == null || TowAttachmentTransform == null || rb == null)
            return;

        Vector3 up = _isGrounded ? _groundNormal : Vector3.up;

        Vector3 toHitch = Vector3.ProjectOnPlane(_towHitch.position - TowAttachmentTransform.position, up);
        if (toHitch.sqrMagnitude < 0.0001f)
            return;

        toHitch.Normalize();

        Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, up);
        if (currentForward.sqrMagnitude < 0.0001f)
            return;

        currentForward.Normalize();

        float signedAngle = Vector3.SignedAngle(currentForward, toHitch, up);
        float angularVelAroundUp = Vector3.Dot(rb.angularVelocity, up);

        float desiredTorque = signedAngle * yawAlignTorque - angularVelAroundUp * yawAngularDamping;
        desiredTorque = Mathf.Clamp(desiredTorque, -maxYawTorque, maxYawTorque);

        rb.AddTorque(up * desiredTorque, ForceMode.Acceleration);
    }

    public bool CanLoadCasualty(RescueCasualtyTarget target)
    {
        if (target == null || target.TargetKind != RescueTargetKind.InjuredCasualty)
            return false;

        if (_loadedTarget != null)
            return false;

        Vector3 pickupOrigin = GetCasualtyPickupOrigin();
        Vector3 closestPoint = GetClosestPickupPointOnTarget(target, pickupOrigin);

        return Vector3.Distance(pickupOrigin, closestPoint) <= casualtyPickupRadius;
    }

    public bool LoadCasualtyFromSnowmobileAssist(RescueCasualtyTarget target, int targetIndex)
    {
        if (target == null || target.TargetKind != RescueTargetKind.InjuredCasualty)
            return false;

        if (_loadedTarget != null)
            return false;

        AttachLoadedCasualty(target, targetIndex);
        return true;
    }

    private void AttachLoadedCasualty(RescueCasualtyTarget target, int targetIndex)
    {
        _loadedTarget = target;
        _loadedTargetIndex = targetIndex;
        _loadedTarget.SetTransportLocked(true);

        NpcRescueCasualtyState casualtyState = _loadedTarget.GetComponent<NpcRescueCasualtyState>();
        if (casualtyState != null)
            casualtyState.PrepareForTransport();

        _loadedCasualtyLieOnLeft = Random.value <= sideLieChance;

        Transform mount = CasualtyMountPoint;
        _loadedTarget.transform.SetParent(mount, false);
        _loadedTarget.transform.localPosition = casualtyLocalPosition;
        _loadedTarget.transform.localRotation = GetLoadedCasualtyLocalRotation();

        Rigidbody targetRb = _loadedTarget.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.linearVelocity = Vector3.zero;
            targetRb.angularVelocity = Vector3.zero;
            targetRb.isKinematic = true;
            targetRb.useGravity = false;
            targetRb.detectCollisions = false;
        }

        ForceLoadedCasualtyWalkPresentation(_loadedTarget.gameObject);
        ApplyLoadedWeightBias(true);

        _stability = 1f;
    }

    private void ForceLoadedCasualtyWalkPresentation(GameObject casualtyRoot)
    {
        if (casualtyRoot == null)
            return;

        WalkingController walk = casualtyRoot.GetComponentInParent<WalkingController>();
        SkiController ski = casualtyRoot.GetComponentInParent<SkiController>();

        if (walk != null)
            walk.ForceEnterWalkMode();
        else if (ski != null)
            ski.enabled = false;
    }

    private Vector3 GetClosestPickupPointOnTarget(RescueCasualtyTarget target, Vector3 fromPoint)
    {
        if (target == null)
            return fromPoint;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        Vector3 bestPoint = target.transform.position;
        float bestDistSqr = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger)
                continue;

            Vector3 p = c.ClosestPoint(fromPoint);
            float d = (p - fromPoint).sqrMagnitude;

            if (!found || d < bestDistSqr)
            {
                found = true;
                bestDistSqr = d;
                bestPoint = p;
            }
        }

        return found ? bestPoint : target.transform.position;
    }

    private Vector3 GetCasualtyPickupOrigin()
    {
        if (pickupTrigger != null)
            return pickupTrigger.bounds.center;

        if (casualtyMountPoint != null)
            return casualtyMountPoint.position;

        return transform.position;
    }

    public bool LoadCasualty(RescueCasualtyTarget target, int targetIndex)
    {
        if (!CanLoadCasualty(target))
            return false;

        AttachLoadedCasualty(target, targetIndex);
        return true;
    }

    public void ForceDropLoadedCasualty()
    {
        if (_loadedTarget == null)
            return;

        RescueCasualtyTarget dropped = _loadedTarget;
        int droppedIndex = _loadedTargetIndex;

        _loadedTarget = null;
        _loadedTargetIndex = -1;
        _stability = 1f;

        dropped.transform.SetParent(null, true);
        dropped.transform.position = ProjectToGround(transform.position + transform.right * 1.0f) + Vector3.up * 0.05f;
        dropped.SetTransportLocked(false);

        Rigidbody targetRb = dropped.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.isKinematic = false;
            targetRb.useGravity = true;
            targetRb.detectCollisions = true;
            targetRb.linearVelocity = Vector3.zero;
            targetRb.angularVelocity = Vector3.zero;
        }

        _service?.NotifyTargetDroppedFromTransport(droppedIndex, dropped.transform.position);

        ApplyLoadedWeightBias(false);
    }

    private void ApplyLoadedWeightBias(bool loaded)
    {
        if (rb == null || !_hasBaseCenterOfMass || !_hasBaseAngularDamping)
            return;

        if (loaded)
        {
            rb.mass = Mathf.Max(35f, rb.mass + loadedExtraMass);
            rb.centerOfMass = _baseCenterOfMass + loadedCenterOfMassOffset;
            rb.angularDamping = Mathf.Max(_baseAngularDamping, loadedAngularDamping);
        }
        else
        {
            rb.mass = Mathf.Max(35f, rb.mass - loadedExtraMass);
            rb.centerOfMass = _baseCenterOfMass;
            rb.angularDamping = _baseAngularDamping;
        }
    }

    public bool IsNearReturnPoint(Vector3 returnPoint, float maxDistance)
    {
        if (_loadedTarget == null)
            return false;

        return Vector3.Distance(transform.position, returnPoint) <= maxDistance;
    }

    private void UpdateLoadedCasualtyPose()
    {
        if (_loadedTarget == null)
            return;

        Transform mount = CasualtyMountPoint;
        _loadedTarget.transform.SetParent(mount, false);
        _loadedTarget.transform.localPosition = casualtyLocalPosition;
        _loadedTarget.transform.localRotation = GetLoadedCasualtyLocalRotation();
    }

    private Quaternion GetLoadedCasualtyLocalRotation()
    {
        Vector3 baseEuler = casualtyBackEuler;

        if (_loadedCasualtyLieOnLeft)
            baseEuler = casualtyLeftSideEuler;
        else if (sideLieChance > 0f)
            baseEuler = casualtyRightSideEuler;

        return Quaternion.Euler(baseEuler);
    }

    private void UpdateStability()
    {
        if (_loadedTarget == null)
            return;

        float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float towSpeed = rb.linearVelocity.magnitude;
        float angularSpeed = rb.angularVelocity.magnitude * Mathf.Rad2Deg;

        if (towSpeed > falloffSpeedThreshold)
            _stability -= ((towSpeed - falloffSpeedThreshold) / Mathf.Max(1f, falloffSpeedThreshold)) * stabilityDrainFromSpeed * dt;

        if (angularSpeed > falloffAngularThreshold)
            _stability -= ((angularSpeed - falloffAngularThreshold) / Mathf.Max(1f, falloffAngularThreshold)) * stabilityDrainFromAngular * dt;

        _stability += stabilityRecoveryPerSecond * dt;
        _stability = Mathf.Clamp01(_stability);

        if (_stability <= 0.001f)
            ForceDropLoadedCasualty();
    }

    private Vector3 ProjectToGround(Vector3 worldPos)
    {
        Vector3 origin = worldPos + Vector3.up * 4f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return worldPos;
    }

    private void EnsureRopeRenderer()
    {
        if (ropeRenderer != null)
        {
            ConfigureRopeRenderer(ropeRenderer);
            return;
        }

        Transform ropeChild = transform.Find("TowRope");
        if (ropeChild == null)
        {
            GameObject go = new GameObject("TowRope");
            go.layer = gameObject.layer;
            ropeChild = go.transform;
            ropeChild.SetParent(transform, false);
            ropeChild.localPosition = Vector3.zero;
            ropeChild.localRotation = Quaternion.identity;
        }

        ropeRenderer = ropeChild.GetComponent<LineRenderer>();
        if (ropeRenderer == null)
            ropeRenderer = ropeChild.gameObject.AddComponent<LineRenderer>();

        ConfigureRopeRenderer(ropeRenderer);
    }

    
    private void EnsureTowJoint()
    {
        if (rb == null)
            return;

        if (_towJoint == null)
            _towJoint = GetComponent<SpringJoint>();

        if (_towJoint == null)
            _towJoint = gameObject.AddComponent<SpringJoint>();

        _towJoint.autoConfigureConnectedAnchor = false;
        _towJoint.enableCollision = false;
        _towJoint.enablePreprocessing = true;
        _towJoint.tolerance = ropeTolerance;
        _towJoint.massScale = 1f;
        _towJoint.connectedMassScale = 1f;

        UpdateTowJoint();
    }

    private void UpdateTowJoint()
    {
        if (_towJoint == null)
            return;

        if (_snowmobile == null || _towHitch == null || _snowmobile.VehicleRigidbody == null)
        {
            _towJoint.connectedBody = null;
            _towJoint.spring = 0f;
            _towJoint.damper = 0f;
            _towJoint.maxDistance = ropeLength;
            _towJoint.minDistance = 0f;
            return;
        }

        Rigidbody vehicleRb = _snowmobile.VehicleRigidbody;

        _towJoint.connectedBody = vehicleRb;
        _towJoint.anchor = transform.InverseTransformPoint(TowAttachmentTransform.position);
        _towJoint.connectedAnchor = vehicleRb.transform.InverseTransformPoint(_towHitch.position);

        _towJoint.minDistance = Mathf.Max(0f, ropeLength - ropeSlack);
        _towJoint.maxDistance = ropeLength;
        _towJoint.spring = ropeSpring;
        _towJoint.damper = ropeDamper;
        _towJoint.tolerance = ropeTolerance;
        _towJoint.breakForce = Mathf.Infinity;
        _towJoint.breakTorque = Mathf.Infinity;
    }
    private void ConfigureRopeRenderer(LineRenderer lr)
    {
        if (lr == null)
            return;

        lr.enabled = false;
        lr.useWorldSpace = true;
        lr.widthMultiplier = ropeWidth;
        lr.startWidth = ropeWidth;
        lr.endWidth = ropeWidth;
        lr.positionCount = 0;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;

        if (ropeMaterial != null)
        {
            lr.sharedMaterial = ropeMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                Material runtimeMat = new Material(shader);
                runtimeMat.color = new Color(0.18f, 0.18f, 0.18f, 1f);
                lr.material = runtimeMat;
            }
        }
    }

    private void UpdateRopeVisual()
    {
        if (ropeRenderer == null)
            return;

        if (_towHitch == null)
        {
            ropeRenderer.enabled = false;
            ropeRenderer.positionCount = 0;
            return;
        }

        Vector3 a = _towHitch.position;
        Vector3 b = TowAttachmentTransform != null ? TowAttachmentTransform.position : transform.position;

        int seg = Mathf.Max(1, ropeSegmentCount);
        int count = seg + 1;
        ropeRenderer.enabled = true;
        ropeRenderer.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / seg;
            Vector3 p = Vector3.Lerp(a, b, t);

            if (ropeSag > 0f && seg > 1)
            {
                float mid = 1f - Mathf.Abs(2f * t - 1f);
                p += Vector3.down * (ropeSag * mid);
            }

            ropeRenderer.SetPosition(i, p);
        }
    }

    private bool CanProcessPickup()
    {
        return pickupTrigger != null &&
               pickupTrigger.enabled &&
               _service != null &&
               _snowmobile != null &&
               _snowmobile.IsMounted &&
               Time.time >= _nextPickupScanTime;
    }

    private void ProcessPickupOverlap()
    {
        if (!CanProcessPickup())
            return;

        _nextPickupScanTime = Time.time + pickupScanInterval;

        int hitCount = OverlapAssignedPickupTrigger(pickupTrigger, _pickupResults);
        if (hitCount <= 0)
            return;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _pickupResults[i];
            if (hit == null)
                continue;

            RescueCasualtyTarget target = TryGetRescueTargetFromCollider(hit);
            if (target == null || target.IsTransportLocked)
                continue;

            if (target.transform.IsChildOf(transform))
                continue;

            if (target.TargetKind != RescueTargetKind.InjuredCasualty)
                continue;

            _service.TryAutoCollectTarget(this, target);
        }
    }

    private int OverlapAssignedPickupTrigger(Collider trigger, Collider[] results)
    {
        if (trigger == null || results == null || results.Length == 0)
            return 0;

        Bounds b = trigger.bounds;
        Vector3 center = b.center;
        Vector3 halfExtents = b.extents;

        halfExtents.x = Mathf.Max(halfExtents.x, 0.05f);
        halfExtents.y = Mathf.Max(halfExtents.y, 0.05f);
        halfExtents.z = Mathf.Max(halfExtents.z, 0.05f);

        return Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            results,
            trigger.transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);
    }

    private RescueCasualtyTarget TryGetRescueTargetFromCollider(Collider other)
    {
        if (other == null)
            return null;

        RescueCasualtyTarget target = other.GetComponent<RescueCasualtyTarget>();
        if (target != null)
            return target;

        return other.GetComponentInParent<RescueCasualtyTarget>();
    }

    public bool IsPromptAvailable => false;
    public string PromptActionText => "";
    public string PromptDescriptionText => "";
    public bool PromptUsesHold => false;
    public float PromptHoldDuration => 0f;
    public Vector3 PromptWorldPosition => transform.position;
    public int PromptPriority => 0;

    private void OnDrawGizmosSelected()
    {
        if (pickupTrigger != null)
        {
            DrawColliderGizmo(pickupTrigger, new Color(1f, 0.55f, 0.15f, 0.30f), new Color(1f, 0.55f, 0.15f, 0.95f));
        }

        Vector3 pickupOrigin = Application.isPlaying ? GetCasualtyPickupOrigin() :
            (pickupTrigger != null ? pickupTrigger.bounds.center :
            (casualtyMountPoint != null ? casualtyMountPoint.position : transform.position));

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.95f);
        Gizmos.DrawWireSphere(pickupOrigin, casualtyPickupRadius);

        if (stretcherTowPoint != null)
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.95f);
            Gizmos.DrawWireSphere(stretcherTowPoint.position, 0.12f);
        }
    }

    private void DrawColliderGizmo(Collider col, Color fill, Color wire)
    {
        if (col == null)
            return;

        Matrix4x4 prev = Gizmos.matrix;

        if (col is BoxCollider box)
        {
            Matrix4x4 m = box.transform.localToWorldMatrix;
            Vector3 worldCenter = m.MultiplyPoint3x4(box.center);
            Gizmos.matrix = Matrix4x4.TRS(worldCenter, box.transform.rotation, box.transform.lossyScale);
            Gizmos.color = fill;
            Gizmos.DrawCube(Vector3.zero, box.size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(Vector3.zero, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float maxScale = Mathf.Max(
                Mathf.Abs(sphere.transform.lossyScale.x),
                Mathf.Abs(sphere.transform.lossyScale.y),
                Mathf.Abs(sphere.transform.lossyScale.z));

            Gizmos.color = fill;
            Gizmos.DrawSphere(center, sphere.radius * maxScale);
            Gizmos.color = wire;
            Gizmos.DrawWireSphere(center, sphere.radius * maxScale);
        }
        else if (col is CapsuleCollider capsule)
        {
            Bounds b = capsule.bounds;
            Gizmos.color = fill;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(b.center, b.size);
        }
        else
        {
            Bounds b = col.bounds;
            Gizmos.color = fill;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(b.center, b.size);
        }

        Gizmos.matrix = prev;
    }

}
