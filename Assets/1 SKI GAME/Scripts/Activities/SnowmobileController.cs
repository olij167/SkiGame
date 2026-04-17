using SkiGame.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class SnowmobileController : MonoBehaviour, IWorldInteractionPromptSource
{
    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference moveAction;

    [Header("Seat")]
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform dismountPoint;
    [SerializeField] private float riderSeatYOffset = 0.35f;
    [SerializeField] private Collider[] playerCollidersToDisableWhileMounted;
    [SerializeField] private Transform npcPassengerSeat;
    [SerializeField] private Transform towHitch;

    [Header("Driving")]
    [SerializeField] private float maxForwardSpeed = 20f;
    [SerializeField] private float maxReverseSpeed = 7f;
    [SerializeField] private float forwardAcceleration = 18f;
    [SerializeField] private float reverseAcceleration = 12f;
    [SerializeField] private float brakingDeceleration = 20f;
    [SerializeField] private float coastingDeceleration = 8f;
    [SerializeField] private float steerDegreesPerSecond = 110f;
    [SerializeField] private float standstillSteerFactor = 0.65f;
    [SerializeField] private float slopeClimbAssist = 3.5f;
    [SerializeField] private float groundedTraction = 14f;
    [SerializeField] private float groundedLateralGrip = 10f;
    [SerializeField] private float rotationSharpness = 14f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Transform frontLeftProbe;
    [SerializeField] private Transform frontRightProbe;
    [SerializeField] private Transform rearLeftProbe;
    [SerializeField] private Transform rearRightProbe;
    [SerializeField] private float probeRayLength = 2.2f;
    [SerializeField] private float rideHeight = 0.9f;
    [SerializeField] private float rideSpringStrength = 85f;
    [SerializeField] private float rideSpringDamping = 12f;
    [SerializeField] private float groundedDownforce = 10f;
    [SerializeField] private float maxGroundSampleAngle = 70f;
    [SerializeField] private float maxDriveSlopeAngle = 42f;
    [SerializeField] private int minGroundedProbes = 2;
    [SerializeField] private float frontObstacleProbeExtraHeight = 0.35f;
    [SerializeField] private float frontObstacleCheckDistance = 0.9f;
    [SerializeField] private float steepSurfaceBrakeDeceleration = 28f;

    [SerializeField] private float probeRadius = 0.2f;
    [SerializeField] private float groundNormalBlendSpeed = 10f;
    [SerializeField] private float groundPointBlendSpeed = 12f;
    [SerializeField] private float groundAdhesionForce = 8f;
    [SerializeField] private float minorObstacleHeightTolerance = 0.28f;
    [SerializeField] private float obstacleSlowdownMinMultiplier = 0.45f;
    [SerializeField] private float obstacleAngleBuffer = 8f;

    [Header("Airborne")]
    [SerializeField] private float airborneGravityMultiplier = 1.4f;
    [SerializeField] private float airbornePlanarDrag = 0.4f;
    [SerializeField] private float airborneYawControl = 35f;
    [SerializeField] private float airbornePitchAssist = 3f;

    [Header("Ground Transition")]
    [SerializeField] private int stableGroundedProbes = 3;
    [SerializeField] private float groundedEnterDelay = 0.05f;
    [SerializeField] private float groundedExitDelay = 0.08f;
    [SerializeField] private float landingSupportBlendTime = 0.18f;
    [SerializeField] private float maxVerticalSpeedForGroundSnap = 3.5f;
    [SerializeField] private float maxAngularVelocityForGroundSnap = 2.75f;

    [Header("Air Stabilisation")]
    [SerializeField] private float airborneUprightAssist = 4.5f;
    [SerializeField] private float airborneRollDamping = 2.5f;
    [SerializeField] private float airbornePitchDamping = 2f;

    [Header("Unmounted")]
    [SerializeField] private float unmountedBrakeDrag = 6f;

    [Header("Rescue Pickup")]
    [SerializeField] private Collider rescuePickupTrigger;
    [SerializeField] private bool autoPickupHealthyTargets = true;

    [Header("Tread Visuals")]
    [SerializeField] private Transform leftTreadRoot;
    [SerializeField] private Transform rightTreadRoot;
    [SerializeField] private Transform leftTreadVisual;
    [SerializeField] private Transform rightTreadVisual;
    [SerializeField] private float treadSpinDegreesPerMeter = 360f;
    [SerializeField] private float treadSteerVisualYaw = 10f;
    [SerializeField] private float treadAlignLerpSpeed = 12f;

    [Header("Prompt")]
    [SerializeField] private string mountPrompt = "Ride Snowmobile";
    [SerializeField] private string dismountPrompt = "Dismount Snowmobile";
    [SerializeField] private int promptPriority = 70;
    [SerializeField] private Collider interactionTrigger;

    private Rigidbody _rb;

    private GameObject _playerRootInTrigger;
    private GameObject _mountedPlayer;
    private SkiController _mountedSki;
    private WalkingController _mountedWalk;
    private Rigidbody _mountedPlayerRb;

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

    private RescueStretcherController _attachedStretcher;
    private Collider[] _selfColliders;

    private static readonly Collider[] _pickupResults = new Collider[24];
    private float _nextPickupScanTime;
    [SerializeField] private float pickupScanInterval = 0.05f;

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
        _rb.linearDamping = 0.25f;
        _rb.angularDamping = 6f;
        _rb.centerOfMass = new Vector3(0f, -0.45f, 0f);

        _smoothedGroundNormal = Vector3.up;
        _smoothedGroundPoint = transform.position;

        _selfColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();

        if (moveAction != null && moveAction.action != null && !moveAction.action.enabled)
            moveAction.action.Enable();
    }

    private void Update()
    {
        HandleMountInput();

        if (IsMounted && _mountedPlayer != null)
        {
            _mountedPlayer.transform.localPosition = new Vector3(0f, riderSeatYOffset, 0f);
            _mountedPlayer.transform.localRotation = Quaternion.identity;
        }

        if (_mountedNpcPassenger != null && npcPassengerSeat != null)
        {
            _mountedNpcPassenger.transform.localPosition = Vector3.zero;
            _mountedNpcPassenger.transform.localRotation = Quaternion.identity;
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

        float speed01 = Mathf.InverseLerp(0f, maxForwardSpeed, Mathf.Abs(GetPlanarForwardSpeed(currentForward)));
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
        bool movingUphill = Vector3.Dot(desiredForward, Vector3.up) > 0.01f;
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

        if (!slopeTooSteepToDrive && !frontBlocked && throttleInput > 0.01f)
        {
            float uphill01 = Mathf.Clamp01(Vector3.Dot(desiredForward, Vector3.up));
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

        _rb.AddForce(desiredForward * forwardAccel * Mathf.Lerp(0.5f, 1f, _landingSupport01), ForceMode.Acceleration);
        _rb.AddForce(-lateralVelocity * groundedLateralGrip * Mathf.Lerp(0.35f, 1f, _landingSupport01), ForceMode.Acceleration);

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

        float springAccel = heightError * rideSpringStrength - velocityAlongNormal * rideSpringDamping;
        springAccel = Mathf.Clamp(springAccel, -rideSpringStrength, rideSpringStrength);

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

        if (_mountedWalk != null)
            _mountedWalk.ForceEnterWalkMode();
        else if (_mountedSki != null)
            _mountedSki.enabled = false;

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

        if (_mountedWalk != null)
            _mountedWalk.ForceEnterSkiMode();
        else if (_mountedSki != null)
            _mountedSki.enabled = true;

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

        if (_mountedNpcPassengerWalk != null)
            _mountedNpcPassengerWalk.ForceEnterWalkMode();
        else if (_mountedNpcPassengerSki != null)
            _mountedNpcPassengerSki.enabled = false;

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

        if (_mountedNpcPassengerWalk != null)
            _mountedNpcPassengerWalk.ForceEnterWalkMode();
        else if (_mountedNpcPassengerSki != null)
            _mountedNpcPassengerSki.enabled = false;

        _mountedNpcPassenger = null;
        _mountedNpcPassengerSki = null;
        _mountedNpcPassengerWalk = null;
        _mountedNpcPassengerRb = null;
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
