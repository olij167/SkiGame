using SkiGame.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class SnowmobileController : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Input")]
    [Tooltip("Input action used to mount, dismount, and hold-start rescue missions while mounted.")]
    [SerializeField] private InputActionReference interactAction;

    [Tooltip("2D movement input. X steers the snowmobile; Y controls throttle and reverse.")]
    [SerializeField] private InputActionReference moveAction;

    [Header("Seat")]
    [Tooltip("Parent point used to attach and align the player while riding.")]
    [SerializeField] private Transform seatPoint;

    [Tooltip("World position and rotation used when the player dismounts.")]
    [SerializeField] private Transform dismountPoint;

    [Tooltip("Vertical local offset applied to the mounted rider on the seat point.")]
    [SerializeField] private float riderSeatYOffset = 0.35f;

    [Tooltip("Player colliders disabled while mounted to prevent vehicle/self collision jitter.")]
    [SerializeField] private Collider[] playerCollidersToDisableWhileMounted;

    [Tooltip("Optional passenger seat used for healthy rescue targets or NPC passengers.")]
    [SerializeField] private Transform npcPassengerSeat;

    [Header("Snowmobile Rider Hand Targets")]
    [Tooltip("Left hand target for the driver, usually placed on or near the left handlebar grip.")]
    [SerializeField] private Transform driverLeftHandTarget;

    [Tooltip("Right hand target for the driver, usually placed on or near the right handlebar grip.")]
    [SerializeField] private Transform driverRightHandTarget;

    [Tooltip("Optional left hand target for the passenger. If unset, a runtime waist target is generated.")]
    [SerializeField] private Transform passengerLeftHandTarget;

    [Tooltip("Optional right hand target for the passenger. If unset, a runtime waist target is generated.")]
    [SerializeField] private Transform passengerRightHandTarget;

    [Tooltip("Side offset for generated passenger hand targets relative to the driver seat.")]
    [SerializeField] private float passengerHandSideOffset = 0.18f;

    [Tooltip("Vertical offset for generated passenger hand targets above the driver seat.")]
    [SerializeField] private float passengerHandsAboveDriverSeat = 0.32f;

    [Tooltip("Forward/back offset for generated passenger hand targets from the driver seat.")]
    [SerializeField] private float passengerHandForwardOffsetFromDriverSeat = -0.04f;

    [Tooltip("Tow hitch used by the rescue stretcher. Falls back to this transform if unset.")]
    [SerializeField] private Transform towHitch;

    [Header("Driving")]
    [Tooltip("Maximum forward speed while throttle is held.")]
    [SerializeField] private float maxForwardSpeed = 20f;

    [Tooltip("Maximum reverse speed while reverse input is held.")]
    [SerializeField] private float maxReverseSpeed = 7f;

    [Tooltip("Acceleration rate when driving forward on valid terrain.")]
    [SerializeField] private float forwardAcceleration = 18f;

    [Tooltip("Acceleration rate when reversing.")]
    [SerializeField] private float reverseAcceleration = 12f;

    [Tooltip("Deceleration rate when changing direction or actively braking.")]
    [SerializeField] private float brakingDeceleration = 20f;

    [Tooltip("Deceleration rate when no throttle or reverse input is held.")]
    [SerializeField] private float coastingDeceleration = 8f;

    [Tooltip("Base yaw turn rate in degrees per second before speed scaling.")]
    [SerializeField] private float steerDegreesPerSecond = 110f;

    [Tooltip("Steering strength at very low speed. Higher values turn more sharply from standstill.")]
    [SerializeField] private float standstillSteerFactor = 0.65f;

    [Tooltip("Extra forward speed assistance when climbing manageable slopes.")]
    [SerializeField] private float slopeClimbAssist = 3.5f;

    [Tooltip("Reserved traction value. Currently not read by this controller's movement solver.")]
    [SerializeField] private float groundedTraction = 14f;

    [Tooltip("Reserved lateral grip value. Currently not read by this controller's movement solver.")]
    [SerializeField] private float groundedLateralGrip = 10f;

    [Tooltip("How quickly the snowmobile rotation aligns to steering and ground normal.")]
    [SerializeField] private float rotationSharpness = 14f;

    [Tooltip("Extra acceleration applied in the uphill direction while throttling uphill.")]
    [SerializeField] private float uphillDriveForce = 12f;

    [Tooltip("Scales uphill drive assistance from subtle to full strength.")]
    [SerializeField] private float uphillThrottleAssist = 0.65f;

    [Tooltip("Lateral slide correction at low speed. Higher values reduce low-speed drifting.")]
    [SerializeField] private float lowSpeedLateralGrip = 11.5f;

    [Tooltip("Lateral slide correction at high speed. Higher values reduce high-speed drifting.")]
    [SerializeField] private float highSpeedLateralGrip = 7.25f;

    [Tooltip("Grip multiplier while accelerating. Lower values create more powered drift.")]
    [SerializeField] private float poweredDriftGripMultiplier = 0.82f;

    [Header("Grounding")]
    [Tooltip("Layers considered valid ground for suspension, slope checks, and obstacle checks.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Front-left suspension probe origin.")]
    [SerializeField] private Transform frontLeftProbe;

    [Tooltip("Front-right suspension probe origin.")]
    [SerializeField] private Transform frontRightProbe;

    [Tooltip("Rear-left suspension probe origin.")]
    [SerializeField] private Transform rearLeftProbe;

    [Tooltip("Rear-right suspension probe origin.")]
    [SerializeField] private Transform rearRightProbe;

    [Tooltip("Maximum distance each suspension probe searches downward for ground.")]
    [SerializeField] private float probeRayLength = 2.2f;

    [Tooltip("Target height maintained above the averaged ground contact point.")]
    [SerializeField] private float rideHeight = 0.9f;

    [Tooltip("Suspension lift strength used to maintain ride height.")]
    [SerializeField] private float rideSpringStrength = 85f;

    [Tooltip("Suspension damping against vertical movement. Higher values reduce bouncing.")]
    [SerializeField] private float rideSpringDamping = 12f;

    [Tooltip("Downward force applied while grounded to keep the treads planted.")]
    [SerializeField] private float groundedDownforce = 10f;

    [Tooltip("Steepest surface angle accepted as ground by suspension probes.")]
    [SerializeField] private float maxGroundSampleAngle = 70f;

    [Tooltip("Steepest uphill slope the snowmobile can actively drive up.")]
    [SerializeField] private float maxDriveSlopeAngle = 42f;

    [Tooltip("Minimum number of probes that must hit ground before support is considered valid.")]
    [SerializeField] private int minGroundedProbes = 2;

    [Tooltip("Vertical offset for forward obstacle checks above the front probes.")]
    [SerializeField] private float frontObstacleProbeExtraHeight = 0.35f;

    [Tooltip("Distance to check ahead for steep faces that should slow or block driving.")]
    [SerializeField] private float frontObstacleCheckDistance = 0.9f;

    [Tooltip("Deceleration applied when trying to drive up a slope beyond the drive limit.")]
    [SerializeField] private float steepSurfaceBrakeDeceleration = 28f;

    [Tooltip("Radius used for suspension sphere casts and front obstacle checks.")]
    [SerializeField] private float probeRadius = 0.2f;

    [Tooltip("Smoothing speed for changes in sampled ground normal.")]
    [SerializeField] private float groundNormalBlendSpeed = 10f;

    [Tooltip("Smoothing speed for changes in averaged ground contact point.")]
    [SerializeField] private float groundPointBlendSpeed = 12f;

    [Tooltip("Extra downward adhesion force while grounded. Higher values reduce small hops.")]
    [SerializeField] private float groundAdhesionForce = 8f;

    [Tooltip("Obstacle height ignored as a minor bump instead of a blocking surface.")]
    [SerializeField] private float minorObstacleHeightTolerance = 0.28f;

    [Tooltip("Lowest speed multiplier used when a front obstacle partially blocks movement.")]
    [SerializeField] private float obstacleSlowdownMinMultiplier = 0.45f;

    [Tooltip("Extra angle margin before a detected front surface counts as blocking.")]
    [SerializeField] private float obstacleAngleBuffer = 8f;

    [Tooltip("Increases suspension damping during hard downward compression or landings.")]
    [SerializeField] private float landingCompressionDampingMultiplier = 1.45f;

    [Tooltip("Maximum acceleration the suspension spring can apply in either direction.")]
    [SerializeField] private float springMaxAccel = 60f;

    [Header("Airborne")]
    [Tooltip("Gravity multiplier while airborne. Values above 1 make the snowmobile fall faster.")]
    [SerializeField] private float airborneGravityMultiplier = 1.4f;

    [Tooltip("Horizontal velocity damping while airborne.")]
    [SerializeField] private float airbornePlanarDrag = 0.4f;

    [Tooltip("Yaw torque applied from steering input while airborne.")]
    [SerializeField] private float airborneYawControl = 35f;

    [Tooltip("Reserved pitch assist value for airborne tuning. Currently not read by this controller.")]
    [SerializeField] private float airbornePitchAssist = 3f;

    [Tooltip("Upward vertical velocity damping while airborne to soften launches.")]
    [SerializeField] private float airborneVerticalDrag = 0.65f;

    [Header("Ground Transition")]
    [Tooltip("Probe count required for stable grounded state transitions and ground snap.")]
    [SerializeField] private int stableGroundedProbes = 3;

    [Tooltip("Delay before entering grounded state after stable probe contact is found.")]
    [SerializeField] private float groundedEnterDelay = 0.05f;

    [Tooltip("Grace time before leaving grounded state after stable contact is lost.")]
    [SerializeField] private float groundedExitDelay = 0.08f;

    [Tooltip("Time used to blend suspension support back in after landing.")]
    [SerializeField] private float landingSupportBlendTime = 0.18f;

    [Tooltip("Maximum vertical speed allowed when snapping into grounded state.")]
    [SerializeField] private float maxVerticalSpeedForGroundSnap = 3.5f;

    [Tooltip("Maximum angular speed allowed when snapping into grounded state.")]
    [SerializeField] private float maxAngularVelocityForGroundSnap = 2.75f;

    [Header("Air Stabilisation")]
    [Tooltip("Torque strength used to rotate the snowmobile upright while airborne.")]
    [SerializeField] private float airborneUprightAssist = 4.5f;

    [Tooltip("Damping applied to airborne roll angular velocity.")]
    [SerializeField] private float airborneRollDamping = 2.5f;

    [Tooltip("Damping applied to airborne pitch angular velocity.")]
    [SerializeField] private float airbornePitchDamping = 2f;

    [Header("Unmounted")]
    [Tooltip("Planar braking force applied when nobody is riding the snowmobile.")]
    [SerializeField] private float unmountedBrakeDrag = 6f;

    [Header("Rescue Pickup")]
    [Tooltip("Trigger volume scanned for rescue targets while the snowmobile is mounted.")]
    [SerializeField] private Collider rescuePickupTrigger;

    [Tooltip("If enabled, non-injured rescue targets can be collected directly by the snowmobile.")]
    [SerializeField] private bool autoPickupHealthyTargets = true;

    [Tooltip("Seconds between pickup overlap scans. Lower values react faster but cost more CPU.")]
    [SerializeField] private float pickupScanInterval = 0.05f;

    [Header("Tread Visuals")]
    [Tooltip("Left tread root used for terrain-relative visual alignment.")]
    [SerializeField] private Transform leftTreadRoot;

    [Tooltip("Right tread root used for terrain-relative visual alignment.")]
    [SerializeField] private Transform rightTreadRoot;

    [Tooltip("Left tread visual rotated/spun to match movement and terrain.")]
    [SerializeField] private Transform leftTreadVisual;

    [Tooltip("Right tread visual rotated/spun to match movement and terrain.")]
    [SerializeField] private Transform rightTreadVisual;

    [Tooltip("How many degrees the tread visual spins per meter of forward travel.")]
    [SerializeField] private float treadSpinDegreesPerMeter = 360f;

    [Tooltip("Visual yaw applied to treads while steering. Does not affect physics.")]
    [SerializeField] private float treadSteerVisualYaw = 10f;

    [Tooltip("How quickly tread visuals align back to their terrain-relative target pose.")]
    [SerializeField] private float treadAlignLerpSpeed = 12f;

    [Header("Prompt")]
    [Tooltip("Prompt text shown when the player can mount the snowmobile.")]
    [SerializeField] private string mountPrompt = "Ride Snowmobile";

    [Tooltip("Prompt text shown when the mounted player can dismount.")]
    [SerializeField] private string dismountPrompt = "Dismount Snowmobile";

    [Tooltip("Prompt priority used when multiple interaction prompts overlap.")]
    [SerializeField] private int promptPriority = 70;

    [Tooltip("Trigger volume used to detect players who can mount the snowmobile.")]
    [SerializeField] private Collider interactionTrigger;

    private Rigidbody _rb;


    private GameObject _playerRootInTrigger;
    private GameObject _mountedPlayer;
    private SkiController _mountedSki;
    private WalkingController _mountedWalk;
    private Rigidbody _mountedPlayerRb;

    private SkierLimbLineVisual _mountedPlayerLimbVisual;
    private bool _mountedWalkControlsWereEnabled;

    private bool _wasPressed;
    private bool _enterArmed;

    private bool _isGrounded;
    private Vector3 _groundNormal = Vector3.up;
    private Vector3 _groundPoint = Vector3.zero;
    private int _groundedProbeCount;

    private Vector3 _smoothedGroundNormal = Vector3.up;

    private Vector3 _smoothedGroundPoint = Vector3.zero;
    private float _currentSpeed;
    private Vector3 _lastGroundForward = Vector3.forward;

    private Quaternion _leftTreadBaseLocalRot;
    private Quaternion _rightTreadBaseLocalRot;
    private Vector3 _leftTreadBaseLocalPos;
    private Vector3 _rightTreadBaseLocalPos;
    private float _leftTreadSpin;
    private float _rightTreadSpin;

    private bool _frontBlockedBySteepSurface;
    private float _frontBlockedSlopeAngle;

    private GameObject _mountedNpcPassenger;
    private SkiController _mountedNpcPassengerSki;
    private WalkingController _mountedNpcPassengerWalk;
    private Rigidbody _mountedNpcPassengerRb;

    private SkierLimbLineVisual _mountedNpcPassengerLimbVisual;
    private bool _mountedNpcPassengerWalkControlsWereEnabled;

    private Transform _runtimePassengerLeftWaistHandTarget;
    private Transform _runtimePassengerRightWaistHandTarget;

    private RescueStretcherController _attachedStretcher;
    private Collider[] _selfColliders;

    private static readonly Collider[] _pickupResults = new Collider[24];
    private float _nextPickupScanTime;
    private bool _rawGrounded;
    private bool _wasGroundedLastFrame;
    private float _groundedStateTimer;
    private float _landingSupport01 = 1f;

    private MedicTentActivityHub _ownerTent;
    private float _mountedInteractHeld;
    private bool _mountedHoldConsumed;

    public bool IsMounted => _mountedPlayer != null;
    public bool HasNpcPassenger => _mountedNpcPassenger != null;
    public Transform TowHitchTransform => towHitch != null ? towHitch : transform;
    public Rigidbody VehicleRigidbody => _rb;
    public RescueStretcherController AttachedStretcher => _attachedStretcher;

    public GameObject MountedPlayerRoot => _mountedPlayer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (seatPoint == null)
            seatPoint = transform;

        if (dismountPoint == null)
            dismountPoint = transform;

        if (leftTreadVisual != null)
        {
            _leftTreadBaseLocalRot = leftTreadVisual.localRotation;
            _leftTreadBaseLocalPos = leftTreadVisual.localPosition;
        }

        if (rightTreadVisual != null)
        {
            _rightTreadBaseLocalRot = rightTreadVisual.localRotation;
            _rightTreadBaseLocalPos = rightTreadVisual.localPosition;
        }

        if (leftTreadRoot == null && leftTreadVisual != null)
            leftTreadRoot = leftTreadVisual.parent;

        if (rightTreadRoot == null && rightTreadVisual != null)
            rightTreadRoot = rightTreadVisual.parent;

        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.useGravity = true;
        _rb.mass = Mathf.Max(180f, _rb.mass);
        _rb.linearDamping = 0.4f;
        _rb.angularDamping = 7f;
        _rb.centerOfMass = new Vector3(0f, -0.45f, 0f);

        _smoothedGroundNormal = Vector3.up;
        _smoothedGroundPoint = transform.position;

        _selfColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        WorldInteractionPromptRegistry.Register(this);

        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();

        if (moveAction != null && moveAction.action != null && !moveAction.action.enabled)
            moveAction.action.Enable();
    }

    private void OnDisable()
    {
        WorldInteractionPromptRegistry.Unregister(this);
    }

    private void Update()
    {
        HandleMountInput();

        if (IsMounted && _mountedPlayer != null)
        {
            _mountedPlayer.transform.localPosition = new Vector3(0f, riderSeatYOffset, 0f);
            _mountedPlayer.transform.localRotation = Quaternion.identity;

            ApplySnowmobileRiderPose(
                _mountedWalk,
                _mountedPlayerLimbVisual,
                SkierLimbLineVisual.RiderPoseMode.SnowmobileDriver,
                driverLeftHandTarget,
                driverRightHandTarget);
        }

        if (_mountedNpcPassenger != null && npcPassengerSeat != null)
        {
            _mountedNpcPassenger.transform.localPosition = Vector3.zero;
            _mountedNpcPassenger.transform.localRotation = Quaternion.identity;

            ApplySnowmobileRiderPose(
                _mountedNpcPassengerWalk,
                _mountedNpcPassengerLimbVisual,
                SkierLimbLineVisual.RiderPoseMode.SnowmobilePassenger,
                GetPassengerHandTarget(true),
                GetPassengerHandTarget(false));
        }
    }

    private void FixedUpdate()
    {
        UpdateGrounding();

        float steerInput = 0f;
        float throttleInput = 0f;

        if (IsMounted && moveAction != null && moveAction.action != null)
        {
            Vector2 move = moveAction.action.ReadValue<Vector2>();
            steerInput = Mathf.Clamp(move.x, -1f, 1f);
            throttleInput = Mathf.Clamp(move.y, -1f, 1f);
        }

        if (!IsMounted)
        {
            HandleUnmountedPhysics();
            UpdateTreadVisuals(0f, 0f, 0f);
            return;
        }

        HandleMountedPhysics(throttleInput, steerInput);
        ProcessRescuePickupOverlap();
        UpdateTreadVisuals(throttleInput, steerInput, GetForwardSpeedForVisuals());
    }

    private void HandleMountedPhysics(float throttleInput, float steerInput)
    {
        if (_isGrounded)
            _landingSupport01 = Mathf.MoveTowards(_landingSupport01, 1f, Time.fixedDeltaTime / Mathf.Max(0.01f, landingSupportBlendTime));
        else
            _landingSupport01 = 0f;

        if (_isGrounded)
            HandleGroundedPhysics(throttleInput, steerInput);
        else
            HandleAirbornePhysics(throttleInput, steerInput);
    }

    private void HandleGroundedPhysics(float throttleInput, float steerInput)
    {
        Vector3 up = _groundNormal;
        Vector3 currentForward = GetPlanarForward(_rb.rotation * Vector3.forward, up);

        if (currentForward.sqrMagnitude < 0.0001f)
            currentForward = _lastGroundForward;

        float planarSpeedAbs = Mathf.Abs(GetPlanarForwardSpeed(currentForward));
        float speed01 = Mathf.InverseLerp(0f, maxForwardSpeed, planarSpeedAbs);

        float steerFactor = Mathf.Lerp(standstillSteerFactor, 1f, speed01);
        float steerStep = steerInput * steerDegreesPerSecond * steerFactor * Time.fixedDeltaTime;

        Vector3 desiredForward = Quaternion.AngleAxis(steerStep, up) * currentForward;
        desiredForward = GetPlanarForward(desiredForward, up);

        if (desiredForward.sqrMagnitude < 0.0001f)
            desiredForward = currentForward;

        desiredForward.Normalize();
        _lastGroundForward = desiredForward;

        float groundAlignSharpness = Mathf.Lerp(4f, rotationSharpness, _landingSupport01);
        Quaternion targetRotation = Quaternion.LookRotation(desiredForward, up);
        Quaternion nextRotation = Quaternion.Slerp(_rb.rotation, targetRotation, groundAlignSharpness * Time.fixedDeltaTime);
        _rb.MoveRotation(nextRotation);

        float slopeAngle = Vector3.Angle(up, Vector3.up);
        float uphill01 = Mathf.Clamp01(Vector3.Dot(desiredForward, Vector3.up));
        bool movingUphill = uphill01 > 0.01f;
        bool slopeTooSteepToDrive = slopeAngle > maxDriveSlopeAngle && movingUphill;
        bool frontBlocked = _frontBlockedBySteepSurface && throttleInput > 0.01f;

        float targetSpeed = throttleInput >= 0f
            ? throttleInput * maxForwardSpeed
            : throttleInput * maxReverseSpeed;

        float accel;
        if (Mathf.Abs(throttleInput) < 0.01f)
        {
            targetSpeed = 0f;
            accel = coastingDeceleration;
        }
        else if (Mathf.Sign(throttleInput) != Mathf.Sign(_currentSpeed) && Mathf.Abs(_currentSpeed) > 0.1f)
        {
            targetSpeed = 0f;
            accel = brakingDeceleration;
        }
        else
        {
            accel = throttleInput >= 0f ? forwardAcceleration : reverseAcceleration;
        }

        if (slopeTooSteepToDrive && throttleInput > 0.01f)
        {
            targetSpeed = 0f;
            accel = steepSurfaceBrakeDeceleration;
        }
        else if (frontBlocked && throttleInput > 0.01f)
        {
            float blockSeverity01 = Mathf.InverseLerp(
                maxDriveSlopeAngle + obstacleAngleBuffer,
                89f,
                _frontBlockedSlopeAngle);

            float speedMultiplier = Mathf.Lerp(1f, obstacleSlowdownMinMultiplier, Mathf.Clamp01(blockSeverity01));
            targetSpeed *= speedMultiplier;
            accel = Mathf.Min(accel, brakingDeceleration);
        }

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);

        if (!slopeTooSteepToDrive && throttleInput > 0.01f)
        {
            _currentSpeed = Mathf.Min(
                maxForwardSpeed,
                _currentSpeed + uphill01 * slopeClimbAssist * Time.fixedDeltaTime);
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, up);
        float forwardSpeed = Vector3.Dot(planarVelocity, desiredForward);
        Vector3 lateralVelocity = planarVelocity - desiredForward * forwardSpeed;

        float forwardSpeedError = _currentSpeed - forwardSpeed;
        float forwardAccel = Mathf.Clamp(
            forwardSpeedError / Mathf.Max(Time.fixedDeltaTime, 0.0001f),
            -Mathf.Max(brakingDeceleration, reverseAcceleration),
            Mathf.Max(forwardAcceleration, reverseAcceleration));

        float dynamicGrip = Mathf.Lerp(lowSpeedLateralGrip, highSpeedLateralGrip, speed01);

        if (throttleInput > 0.01f)
            dynamicGrip *= poweredDriftGripMultiplier;

        float uphillThrottle01 = Mathf.Clamp01(throttleInput) * uphill01;
        float uphillDriveAccel = uphillDriveForce * uphillThrottle01 * Mathf.Lerp(0.6f, 1f, uphillThrottleAssist);

        _rb.AddForce(desiredForward * forwardAccel * Mathf.Lerp(0.5f, 1f, _landingSupport01), ForceMode.Acceleration);
        _rb.AddForce(desiredForward * uphillDriveAccel, ForceMode.Acceleration);
        _rb.AddForce(-lateralVelocity * dynamicGrip * Mathf.Lerp(0.35f, 1f, _landingSupport01), ForceMode.Acceleration);
        _rb.AddForce(-up * groundAdhesionForce * Mathf.Lerp(0.35f, 1f, _landingSupport01), ForceMode.Acceleration);

        ApplyRideSpring(up);
    }

    private void HandleAirbornePhysics(float throttleInput, float steerInput)
    {
        Vector3 velocity = _rb.linearVelocity;
        Vector3 verticalVelocity = Vector3.Project(velocity, Vector3.up);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);

        planarVelocity = Vector3.Lerp(
            planarVelocity,
            Vector3.zero,
            airbornePlanarDrag * Time.fixedDeltaTime);

        if (verticalVelocity.y > 0f)
        {
            verticalVelocity = Vector3.Lerp(
                verticalVelocity,
                Vector3.zero,
                airborneVerticalDrag * Time.fixedDeltaTime);
        }

        _rb.linearVelocity = planarVelocity + verticalVelocity;
        _rb.AddForce(Physics.gravity * (airborneGravityMultiplier - 1f), ForceMode.Acceleration);

        if (Mathf.Abs(steerInput) > 0.001f)
        {
            _rb.AddTorque(Vector3.up * (steerInput * airborneYawControl), ForceMode.Acceleration);
        }

        Vector3 forward = _rb.rotation * Vector3.forward;
        Vector3 right = _rb.rotation * Vector3.right;

        Vector3 desiredForward = planarVelocity.sqrMagnitude > 0.25f
            ? planarVelocity.normalized
            : Vector3.ProjectOnPlane(forward, Vector3.up).normalized;

        if (desiredForward.sqrMagnitude < 0.0001f)
            desiredForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;

        Vector3 desiredUp = Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(desiredForward, desiredUp).normalized,
            desiredUp);

        Quaternion delta = targetRotation * Quaternion.Inverse(_rb.rotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);

        if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 0.0001f)
        {
            if (angleDeg > 180f)
                angleDeg -= 360f;

            Vector3 correctiveTorque = axis.normalized * (angleDeg * Mathf.Deg2Rad * airborneUprightAssist);
            _rb.AddTorque(correctiveTorque, ForceMode.Acceleration);
        }

        _rb.AddTorque(-Vector3.Project(_rb.angularVelocity, forward) * airborneRollDamping, ForceMode.Acceleration);
        _rb.AddTorque(-Vector3.Project(_rb.angularVelocity, right) * airbornePitchDamping, ForceMode.Acceleration);

        _currentSpeed = planarVelocity.magnitude;
    }

    private void ApplyRideSpring(Vector3 up)
    {
        float currentHeightAlongNormal = Vector3.Dot(_rb.position - _groundPoint, up);
        float heightError = rideHeight - currentHeightAlongNormal;
        float velocityAlongNormal = Vector3.Dot(_rb.linearVelocity, up);

        float compression01 = Mathf.Clamp01(Mathf.Max(0f, -velocityAlongNormal) / 8f);
        float compressionDamping = Mathf.Lerp(1f, landingCompressionDampingMultiplier, compression01);

        float springAccel = heightError * rideSpringStrength - velocityAlongNormal * rideSpringDamping * compressionDamping;
        springAccel = Mathf.Clamp(springAccel, -springMaxAccel, springMaxAccel);

        float supportScale = Mathf.Lerp(0.2f, 1f, _landingSupport01);

        _rb.AddForce(up * (springAccel * supportScale), ForceMode.Acceleration);
        _rb.AddForce(-up * (groundedDownforce * supportScale), ForceMode.Acceleration);
    }

    private void HandleUnmountedPhysics()
    {
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, brakingDeceleration * Time.fixedDeltaTime);

        Vector3 planar = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
        _rb.AddForce(-planar * Mathf.Max(0f, unmountedBrakeDrag), ForceMode.Acceleration);

        if (_isGrounded)
        {
            Vector3 up = _groundNormal;
            Vector3 forward = GetPlanarForward(_rb.rotation * Vector3.forward, up);
            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forward, up);
                Quaternion nextRotation = Quaternion.Slerp(_rb.rotation, targetRotation, rotationSharpness * Time.fixedDeltaTime);
                _rb.MoveRotation(nextRotation);
            }

            ApplyRideSpring(up);
        }
    }

    private void UpdateGrounding()
    {
        _rawGrounded = false;
        _groundNormal = Vector3.up;
        _groundPoint = transform.position;
        _groundedProbeCount = 0;
        _frontBlockedBySteepSurface = false;
        _frontBlockedSlopeAngle = 0f;

        Vector3 normalSum = Vector3.zero;
        Vector3 pointSum = Vector3.zero;

        SampleProbe(frontLeftProbe, ref normalSum, ref pointSum);
        SampleProbe(frontRightProbe, ref normalSum, ref pointSum);
        SampleProbe(rearLeftProbe, ref normalSum, ref pointSum);
        SampleProbe(rearRightProbe, ref normalSum, ref pointSum);

        CheckFrontObstacle(frontLeftProbe);
        CheckFrontObstacle(frontRightProbe);

        if (_groundedProbeCount >= Mathf.Max(1, minGroundedProbes))
        {
            _rawGrounded = true;

            Vector3 sampledNormal = normalSum.sqrMagnitude > 0.0001f
                ? normalSum.normalized
                : Vector3.up;

            Vector3 sampledPoint = pointSum / Mathf.Max(1, _groundedProbeCount);

            _smoothedGroundNormal = Vector3.Slerp(
                _smoothedGroundNormal,
                sampledNormal,
                groundNormalBlendSpeed * Time.fixedDeltaTime).normalized;

            _smoothedGroundPoint = Vector3.Lerp(
                _smoothedGroundPoint,
                sampledPoint,
                groundPointBlendSpeed * Time.fixedDeltaTime);

            _groundNormal = _smoothedGroundNormal;
            _groundPoint = _smoothedGroundPoint;
        }
        else
        {
            _smoothedGroundNormal = Vector3.Slerp(
                _smoothedGroundNormal,
                Vector3.up,
                Mathf.Max(1f, groundNormalBlendSpeed * 0.4f) * Time.fixedDeltaTime);

            _smoothedGroundPoint = Vector3.Lerp(
                _smoothedGroundPoint,
                transform.position,
                Mathf.Max(1f, groundPointBlendSpeed * 0.25f) * Time.fixedDeltaTime);

            _groundNormal = _smoothedGroundNormal;
            _groundPoint = _smoothedGroundPoint;
        }

        bool stableProbeContact = _groundedProbeCount >= Mathf.Max(minGroundedProbes, stableGroundedProbes);
        float verticalSpeed = Mathf.Abs(Vector3.Dot(_rb.linearVelocity, Vector3.up));
        float angularSpeed = _rb.angularVelocity.magnitude;

        bool canSnapToGround =
            stableProbeContact &&
            verticalSpeed <= maxVerticalSpeedForGroundSnap &&
            angularSpeed <= maxAngularVelocityForGroundSnap;

        if (canSnapToGround)
        {
            if (!_wasGroundedLastFrame)
                _groundedStateTimer += Time.fixedDeltaTime;

            if (_wasGroundedLastFrame || _groundedStateTimer >= groundedEnterDelay)
            {
                _isGrounded = true;

                if (!_wasGroundedLastFrame)
                    _landingSupport01 = 0f;
            }
        }
        else
        {
            if (_wasGroundedLastFrame)
            {
                _groundedStateTimer += Time.fixedDeltaTime;
                if (_groundedStateTimer >= groundedExitDelay)
                    _isGrounded = false;
            }
            else
            {
                _isGrounded = false;
            }
        }

        if (_isGrounded != _wasGroundedLastFrame)
            _groundedStateTimer = 0f;

        _wasGroundedLastFrame = _isGrounded;
    }

    private void SampleProbe(Transform probe, ref Vector3 normalSum, ref Vector3 pointSum)
    {
        if (probe == null)
            return;

        Vector3 rayOrigin = probe.position + Vector3.up * 0.2f;

        bool hitFound = Physics.SphereCast(
            rayOrigin,
            Mathf.Max(0.01f, probeRadius),
            Vector3.down,
            out RaycastHit hit,
            probeRayLength,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (!hitFound)
            return;

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (slopeAngle > maxGroundSampleAngle)
            return;

        _groundedProbeCount++;
        normalSum += hit.normal;
        pointSum += hit.point;
    }

    private void CheckFrontObstacle(Transform probe)
    {
        if (probe == null)
            return;

        Vector3 origin = probe.position + Vector3.up * frontObstacleProbeExtraHeight;
        Vector3 forward = GetPlanarForward(_rb.rotation * Vector3.forward, _groundNormal);
        if (forward.sqrMagnitude < 0.0001f)
            forward = (_rb.rotation * Vector3.forward).normalized;

        bool hitFound = Physics.SphereCast(
            origin,
            Mathf.Max(0.04f, probeRadius * 0.9f),
            forward,
            out RaycastHit hit,
            frontObstacleCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (!hitFound || hit.collider == null)
            return;

        if (IsSelfOrAttachedCollider(hit.collider))
            return;

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (slopeAngle <= maxDriveSlopeAngle + obstacleAngleBuffer)
            return;

        float hitHeightAboveProbe = hit.point.y - probe.position.y;
        if (hitHeightAboveProbe <= minorObstacleHeightTolerance)
            return;

        float relativeToGround = Vector3.Angle(hit.normal, _groundNormal);
        if (relativeToGround < obstacleAngleBuffer)
            return;

        _frontBlockedBySteepSurface = true;
        _frontBlockedSlopeAngle = Mathf.Max(_frontBlockedSlopeAngle, slopeAngle);
    }

    private static Vector3 GetPlanarForward(Vector3 rawForward, Vector3 up)
    {
        Vector3 forward = Vector3.ProjectOnPlane(rawForward, up);
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return forward.normalized;
    }

    private float GetPlanarForwardSpeed(Vector3 forward)
    {
        if (forward.sqrMagnitude < 0.0001f)
            return 0f;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        return Vector3.Dot(planarVelocity, forward.normalized);
    }

    private float GetForwardSpeedForVisuals()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, _isGrounded ? _groundNormal : Vector3.up);
        Vector3 forward = GetPlanarForward(transform.forward, _isGrounded ? _groundNormal : Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            return planarVelocity.magnitude;

        return Vector3.Dot(planarVelocity, forward);
    }

    private void HandleMountInput()
    {
        if (interactAction == null || interactAction.action == null)
            return;

        bool pressed = interactAction.action.IsPressed();

        if (!_enterArmed)
        {
            if (!pressed)
                _enterArmed = true;

            _wasPressed = false;
            _mountedInteractHeld = 0f;
            _mountedHoldConsumed = false;
            return;
        }

        if (!IsMounted)
        {
            if (!pressed)
            {
                _wasPressed = false;
                return;
            }

            if (_wasPressed)
                return;

            _wasPressed = true;

            if (_playerRootInTrigger != null)
                Mount(_playerRootInTrigger);

            return;
        }

        if (!pressed)
        {
            if (_wasPressed && !_mountedHoldConsumed)
                Dismount();

            _wasPressed = false;
            _mountedInteractHeld = 0f;
            _mountedHoldConsumed = false;
            return;
        }

        if (!_wasPressed)
        {
            _wasPressed = true;
            _mountedInteractHeld = 0f;
            _mountedHoldConsumed = false;
            return;
        }

        _mountedInteractHeld += Time.unscaledDeltaTime;

        if (_mountedHoldConsumed)
            return;

        if (CanStartMountedRescue(out MedicTentActivityHub tent))
        {
            float requiredHold = tent.MountedStartHoldSeconds;
            if (_mountedInteractHeld >= requiredHold)
            {
                if (tent.TryStartMissionFromMountedPlayer(_mountedPlayer))
                {
                    _mountedHoldConsumed = true;
                    _enterArmed = false;
                    _wasPressed = true;
                }
            }
        }
    }

    private void Mount(GameObject playerRoot)
    {
        if (playerRoot == null || seatPoint == null)
            return;

        _mountedPlayer = playerRoot;
        _mountedSki = playerRoot.GetComponentInParent<SkiController>();
        _mountedWalk = playerRoot.GetComponentInParent<WalkingController>();
        _mountedPlayerRb = playerRoot.GetComponentInParent<Rigidbody>();

        _mountedPlayerLimbVisual = playerRoot.GetComponentInChildren<SkierLimbLineVisual>(true);
        _mountedWalkControlsWereEnabled = _mountedWalk == null || _mountedWalk.ControlsEnabled;

        if (_mountedWalk != null)
        {
            _mountedWalk.enabled = true;
            _mountedWalk.ForceEnterWalkMode();
            _mountedWalk.ClearExternalMove();
            _mountedWalk.ControlsEnabled = false;
            _mountedWalk.SetRiderPoseActive(true);
        }
        else if (_mountedSki != null)
        {
            _mountedSki.enabled = false;
        }

        ApplySnowmobileRiderPose(
            _mountedWalk,
            _mountedPlayerLimbVisual,
            SkierLimbLineVisual.RiderPoseMode.SnowmobileDriver,
            driverLeftHandTarget,
            driverRightHandTarget);

        if (_mountedPlayerRb != null)
        {
            _mountedPlayerRb.linearVelocity = Vector3.zero;
            _mountedPlayerRb.angularVelocity = Vector3.zero;
            _mountedPlayerRb.isKinematic = true;
            _mountedPlayerRb.detectCollisions = false;
        }

        if (playerCollidersToDisableWhileMounted != null)
        {
            for (int i = 0; i < playerCollidersToDisableWhileMounted.Length; i++)
            {
                if (playerCollidersToDisableWhileMounted[i] != null)
                    playerCollidersToDisableWhileMounted[i].enabled = false;
            }
        }

        _mountedPlayer.transform.SetParent(seatPoint, false);
        _mountedPlayer.transform.localPosition = new Vector3(0f, riderSeatYOffset, 0f);
        _mountedPlayer.transform.localRotation = Quaternion.identity;
        GameAudio.PlayWorld(GameAudioCueId.SnowmobileMount, seatPoint.position);

        _currentSpeed = 0f;
        _playerRootInTrigger = playerRoot;
        _enterArmed = false;
        _wasPressed = true;
    }

    private void Dismount()
    {
        if (_mountedPlayer == null || dismountPoint == null)
            return;

        GameAudio.PlayWorld(GameAudioCueId.SnowmobileDismount, dismountPoint.position);
        _mountedPlayer.transform.SetParent(null, true);
        _mountedPlayer.transform.position = dismountPoint.position;
        _mountedPlayer.transform.rotation = dismountPoint.rotation;

        if (_mountedPlayerRb != null)
        {
            _mountedPlayerRb.isKinematic = false;
            _mountedPlayerRb.detectCollisions = true;
            _mountedPlayerRb.linearVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
            _mountedPlayerRb.angularVelocity = Vector3.zero;
        }

        if (playerCollidersToDisableWhileMounted != null)
        {
            for (int i = 0; i < playerCollidersToDisableWhileMounted.Length; i++)
            {
                if (playerCollidersToDisableWhileMounted[i] != null)
                    playerCollidersToDisableWhileMounted[i].enabled = true;
            }
        }

        ClearSnowmobileRiderPose(_mountedPlayerLimbVisual);

        if (_mountedWalk != null)
        {
            _mountedWalk.enabled = true;
            _mountedWalk.SetRiderPoseActive(false);
            _mountedWalk.ControlsEnabled = _mountedWalkControlsWereEnabled;
            _mountedWalk.ForceEnterSkiMode();
        }
        else if (_mountedSki != null)
        {
            _mountedSki.enabled = true;
        }

        _mountedPlayerLimbVisual = null;

        _mountedPlayer = null;
        _mountedSki = null;
        _mountedWalk = null;
        _mountedPlayerRb = null;

        _enterArmed = false;
        _wasPressed = true;
    }

    public bool HasMountedPlayer(GameObject playerRoot = null)
    {
        if (_mountedPlayer == null)
            return false;

        return playerRoot == null || _mountedPlayer == playerRoot;
    }

    public bool CanBoardNpcPassenger()
    {
        return npcPassengerSeat != null && _mountedNpcPassenger == null;
    }

    public bool TryBoardNpcPassenger(GameObject npcRoot)
    {
        if (npcRoot == null || !CanBoardNpcPassenger())
            return false;

        _mountedNpcPassenger = npcRoot;
        _mountedNpcPassengerSki = npcRoot.GetComponentInParent<SkiController>();
        _mountedNpcPassengerWalk = npcRoot.GetComponentInParent<WalkingController>();
        _mountedNpcPassengerRb = npcRoot.GetComponentInParent<Rigidbody>();

        _mountedNpcPassengerLimbVisual = npcRoot.GetComponentInChildren<SkierLimbLineVisual>(true);
        _mountedNpcPassengerWalkControlsWereEnabled = _mountedNpcPassengerWalk == null || _mountedNpcPassengerWalk.ControlsEnabled;

        if (_mountedNpcPassengerWalk != null)
        {
            _mountedNpcPassengerWalk.enabled = true;
            _mountedNpcPassengerWalk.ForceEnterWalkMode();
            _mountedNpcPassengerWalk.ClearExternalMove();
            _mountedNpcPassengerWalk.ControlsEnabled = false;
            _mountedNpcPassengerWalk.SetRiderPoseActive(true);
        }
        else if (_mountedNpcPassengerSki != null)
        {
            _mountedNpcPassengerSki.enabled = false;
        }

        ApplySnowmobileRiderPose(
            _mountedNpcPassengerWalk,
            _mountedNpcPassengerLimbVisual,
            SkierLimbLineVisual.RiderPoseMode.SnowmobilePassenger,
            GetPassengerHandTarget(true),
            GetPassengerHandTarget(false));

        if (_mountedNpcPassengerRb != null)
        {
            _mountedNpcPassengerRb.linearVelocity = Vector3.zero;
            _mountedNpcPassengerRb.angularVelocity = Vector3.zero;
            _mountedNpcPassengerRb.isKinematic = true;
            _mountedNpcPassengerRb.useGravity = false;
            _mountedNpcPassengerRb.detectCollisions = false;
        }

        _mountedNpcPassenger.transform.SetParent(npcPassengerSeat, false);
        _mountedNpcPassenger.transform.localPosition = Vector3.zero;
        _mountedNpcPassenger.transform.localRotation = Quaternion.identity;
        return true;
    }

    public void ReleaseNpcPassenger(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (_mountedNpcPassenger == null)
            return;

        _mountedNpcPassenger.transform.SetParent(null, true);
        _mountedNpcPassenger.transform.position = worldPosition;
        _mountedNpcPassenger.transform.rotation = worldRotation;

        if (_mountedNpcPassengerRb != null)
        {
            _mountedNpcPassengerRb.isKinematic = false;
            _mountedNpcPassengerRb.useGravity = true;
            _mountedNpcPassengerRb.detectCollisions = true;
            _mountedNpcPassengerRb.linearVelocity = Vector3.zero;
            _mountedNpcPassengerRb.angularVelocity = Vector3.zero;
        }

        ClearSnowmobileRiderPose(_mountedNpcPassengerLimbVisual);

        if (_mountedNpcPassengerWalk != null)
        {
            _mountedNpcPassengerWalk.enabled = true;
            _mountedNpcPassengerWalk.SetRiderPoseActive(false);
            _mountedNpcPassengerWalk.ControlsEnabled = _mountedNpcPassengerWalkControlsWereEnabled;
            _mountedNpcPassengerWalk.ForceEnterWalkMode();
        }
        else if (_mountedNpcPassengerSki != null)
        {
            _mountedNpcPassengerSki.enabled = false;
        }

        _mountedNpcPassengerLimbVisual = null;

        _mountedNpcPassenger = null;
        _mountedNpcPassengerSki = null;
        _mountedNpcPassengerWalk = null;
        _mountedNpcPassengerRb = null;
    }

    private void ApplySnowmobileRiderPose(
    WalkingController walk,
    SkierLimbLineVisual limbVisual,
    SkierLimbLineVisual.RiderPoseMode poseMode,
    Transform leftHandTarget,
    Transform rightHandTarget)
    {
        if (walk != null)
        {
            walk.ClearExternalMove();
            walk.ControlsEnabled = false;
            walk.SetRiderPoseActive(true);
        }

        if (limbVisual != null)
            limbVisual.SetRiderPoseOverride(poseMode, leftHandTarget, rightHandTarget);
    }

    private static void ClearSnowmobileRiderPose(SkierLimbLineVisual limbVisual)
    {
        if (limbVisual != null)
            limbVisual.ClearRiderPoseOverride();
    }

    private Transform GetPassengerHandTarget(bool left)
    {
        Transform explicitTarget = left ? passengerLeftHandTarget : passengerRightHandTarget;
        if (explicitTarget != null)
            return explicitTarget;

        return EnsureRuntimePassengerWaistHandTarget(left);
    }

    private Transform EnsureRuntimePassengerWaistHandTarget(bool left)
    {
        if (seatPoint == null)
            return null;

        Transform existing = left
            ? _runtimePassengerLeftWaistHandTarget
            : _runtimePassengerRightWaistHandTarget;

        if (existing != null)
            return existing;

        GameObject go = new GameObject(left
            ? "RuntimePassengerLeftWaistHandTarget"
            : "RuntimePassengerRightWaistHandTarget");

        Transform target = go.transform;
        target.SetParent(seatPoint, false);

        float side = Mathf.Abs(passengerHandSideOffset) * (left ? -1f : 1f);

        target.localPosition = new Vector3(
            side,
            riderSeatYOffset + Mathf.Max(0f, passengerHandsAboveDriverSeat),
            passengerHandForwardOffsetFromDriverSeat);

        target.localRotation = Quaternion.identity;

        if (left)
            _runtimePassengerLeftWaistHandTarget = target;
        else
            _runtimePassengerRightWaistHandTarget = target;

        return target;
    }

    public void RegisterStretcher(RescueStretcherController stretcher)
    {
        if (stretcher == null)
            return;

        _attachedStretcher = stretcher;
    }

    public void UnregisterStretcher(RescueStretcherController stretcher)
    {
        if (_attachedStretcher == stretcher)
            _attachedStretcher = null;
    }


    private bool IsSelfOrAttachedCollider(Collider c)
    {
        if (c == null)
            return false;

        Transform t = c.transform;
        while (t != null)
        {
            if (t == transform)
                return true;

            if (_attachedStretcher != null && t == _attachedStretcher.transform)
                return true;

            t = t.parent;
        }

        return false;
    }

    private void UpdateTreadVisuals(float throttle, float steer, float forwardSpeed)
    {
        UpdateSingleTreadVisual(
            leftTreadRoot,
            leftTreadVisual,
            true,
            steer,
            forwardSpeed,
            ref _leftTreadSpin,
            _leftTreadBaseLocalPos,
            _leftTreadBaseLocalRot);

        UpdateSingleTreadVisual(
            rightTreadRoot,
            rightTreadVisual,
            false,
            steer,
            forwardSpeed,
            ref _rightTreadSpin,
            _rightTreadBaseLocalPos,
            _rightTreadBaseLocalRot);
    }

    private void UpdateSingleTreadVisual(
        Transform treadRoot,
        Transform treadVisual,
        bool isLeft,
        float steer,
        float forwardSpeed,
        ref float spinDegrees,
        Vector3 baseLocalPos,
        Quaternion baseLocalRot)
    {
        if (treadRoot == null || treadVisual == null)
            return;

        Vector3 sampleNormal = _groundNormal;

        Vector3 rayOrigin = treadRoot.position + Vector3.up * 0.6f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, probeRayLength, groundMask, QueryTriggerInteraction.Ignore))
            sampleNormal = hit.normal.normalized;

        Vector3 rootForward = Vector3.ProjectOnPlane(treadRoot.forward, sampleNormal);
        if (rootForward.sqrMagnitude < 0.0001f)
            rootForward = Vector3.ProjectOnPlane(transform.forward, sampleNormal);
        if (rootForward.sqrMagnitude < 0.0001f)
            rootForward = Vector3.forward;

        rootForward.Normalize();

        float steerYaw = steer * treadSteerVisualYaw * (isLeft ? 1f : -1f) * 0.35f;
        spinDegrees += forwardSpeed * treadSpinDegreesPerMeter * Time.fixedDeltaTime;

        Quaternion terrainRelative =
            Quaternion.Inverse(treadRoot.rotation) *
            Quaternion.LookRotation(rootForward, sampleNormal);

        Quaternion localSpin = Quaternion.Euler(spinDegrees, steerYaw, 0f);
        Quaternion targetLocalRot = baseLocalRot * terrainRelative * localSpin;

        treadVisual.localPosition = Vector3.Lerp(
            treadVisual.localPosition,
            baseLocalPos,
            treadAlignLerpSpeed * Time.deltaTime);

        treadVisual.localRotation = Quaternion.Slerp(
            treadVisual.localRotation,
            targetLocalRot,
            treadAlignLerpSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        GameObject root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        _enterArmed = false;
        _playerRootInTrigger = root;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
            return;

        GameObject root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        if (_playerRootInTrigger == root)
            _playerRootInTrigger = null;
    }

    private bool CanProcessRescuePickup()
    {
        return rescuePickupTrigger != null &&
               rescuePickupTrigger.enabled &&
               IsMounted &&
               RescueService.Instance != null &&
               Time.time >= _nextPickupScanTime;
    }

    private void ProcessRescuePickupOverlap()
    {
        if (!CanProcessRescuePickup())
            return;

        _nextPickupScanTime = Time.time + pickupScanInterval;

        int hitCount = OverlapAssignedPickupTrigger(rescuePickupTrigger, _pickupResults);
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

            if (target.TargetKind == RescueTargetKind.InjuredCasualty)
            {
                if (_attachedStretcher != null)
                    RescueService.Instance.TryAutoCollectTarget(_attachedStretcher, target);

                continue;
            }

            if (autoPickupHealthyTargets)
                RescueService.Instance.TryAutoCollectTarget(this, target);
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

    private static GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null)
            return null;

        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("NPC"))
                return null;

            t = t.parent;
        }

        Transform playerTagged = null;
        t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("Player"))
            {
                playerTagged = t;
                break;
            }

            t = t.parent;
        }

        if (playerTagged == null)
            return null;

        PlayerInput pi = other.GetComponentInParent<PlayerInput>();
        if (pi != null)
            return pi.gameObject;

        return playerTagged.gameObject;
    }

    public void SetOwnerTent(MedicTentActivityHub ownerTent)
    {
        _ownerTent = ownerTent;
    }

    public bool IsPlayerMounted(GameObject playerRoot)
    {
        return playerRoot != null && _mountedPlayer == playerRoot;
    }

    public bool TryMountPlayer(GameObject playerRoot)
    {
        if (playerRoot == null || IsMounted)
            return false;

        Mount(playerRoot);
        return IsMounted;
    }

    public bool TryDismountPlayer()
    {
        if (!IsMounted)
            return false;

        Dismount();
        return !IsMounted;
    }

    public void ForceResetForMissionStart(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        if (IsMounted)
            Dismount();

        if (_mountedNpcPassenger != null)
            ReleaseNpcPassenger(worldPosition + worldRotation * Vector3.right * 1.5f, worldRotation);

        _currentSpeed = 0f;
        _wasPressed = false;
        _enterArmed = false;
        _mountedInteractHeld = 0f;
        _mountedHoldConsumed = false;
        _nextPickupScanTime = 0f;

        _rawGrounded = false;
        _wasGroundedLastFrame = false;
        _isGrounded = false;
        _groundedStateTimer = 0f;
        _landingSupport01 = 0f;
        _groundPoint = worldPosition;
        _groundNormal = Vector3.up;
        _groundedProbeCount = 0;
        _frontBlockedBySteepSurface = false;
        _frontBlockedSlopeAngle = 0f;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _rb.Sleep();
            _rb.position = worldPosition;
            _rb.rotation = worldRotation;

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            Physics.SyncTransforms();

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.Sleep();
        }
        else
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            Physics.SyncTransforms();
        }
    }

    private bool CanStartMountedRescue(out MedicTentActivityHub tent)
    {
        tent = null;

        if (!IsMounted || _mountedPlayer == null || _ownerTent == null)
            return false;

        if (!_ownerTent.CanMountedPlayerStartMission(_mountedPlayer))
            return false;

        tent = _ownerTent;
        return true;
    }

    public bool IsPromptAvailable => IsMounted || _playerRootInTrigger != null;
    public string PromptActionText => "Interact";
    public string PromptDescriptionText
    {
        get
        {
            if (!IsMounted)
                return mountPrompt;

            if (CanStartMountedRescue(out _))
                return "Tap to dismount \n Hold to start next rescue";

            return dismountPrompt;
        }
    }

    public bool PromptUsesHold => IsMounted && CanStartMountedRescue(out _);
    public float PromptHoldDuration => IsMounted && CanStartMountedRescue(out MedicTentActivityHub tent)
        ? tent.MountedStartHoldSeconds
        : 0f;
    public Vector3 PromptWorldPosition => seatPoint != null ? seatPoint.position : transform.position;
    public int PromptPriority => promptPriority;

    private void OnDrawGizmosSelected()
    {
        if (rescuePickupTrigger != null)
        {
            DrawColliderGizmo(rescuePickupTrigger, new Color(0.2f, 0.95f, 0.35f, 0.35f), new Color(0.2f, 0.95f, 0.35f, 0.95f));
        }

        if (towHitch != null)
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.95f);
            Gizmos.DrawWireSphere(towHitch.position, 0.15f);
            Gizmos.DrawLine(towHitch.position, towHitch.position + towHitch.forward * 0.75f);
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
