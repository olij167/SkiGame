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
    [SerializeField] private float airborneSpeedDecay = 6f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Transform frontLeftProbe;
    [SerializeField] private Transform frontRightProbe;
    [SerializeField] private Transform rearLeftProbe;
    [SerializeField] private Transform rearRightProbe;
    [SerializeField] private float probeRayLength = 2.2f;
    [SerializeField] private float rideHeight = 0.9f;
    [SerializeField] private float rideHeightSnapStrength = 12f;
    [SerializeField] private float maxGroundSampleAngle = 70f;
    [SerializeField] private float maxDriveSlopeAngle = 42f;
    [SerializeField] private float airborneGravityMultiplier = 1.4f;
    [SerializeField] private float unmountedBrakeDrag = 6f;

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

    private float _currentSpeed;
    private Vector3 _lastGroundForward = Vector3.forward;

    private Quaternion _leftTreadBaseLocalRot;
    private Quaternion _rightTreadBaseLocalRot;
    private Vector3 _leftTreadBaseLocalPos;
    private Vector3 _rightTreadBaseLocalPos;
    private float _leftTreadSpin;
    private float _rightTreadSpin;

    public bool IsMounted => _mountedPlayer != null;

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
        _rb.angularDamping = 8f;
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
    }

    private void FixedUpdate()
    {
        UpdateGrounding();

        if (!IsMounted)
        {
            HandleUnmountedPhysics();
            UpdateTreadVisuals(0f, 0f, 0f);
            return;
        }

        if (moveAction == null || moveAction.action == null)
        {
            HandleMountedPhysics(0f, 0f);
            UpdateTreadVisuals(0f, 0f, _currentSpeed);
            return;
        }

        Vector2 move = moveAction.action.ReadValue<Vector2>();
        float steerInput = Mathf.Clamp(move.x, -1f, 1f);
        float throttleInput = Mathf.Clamp(move.y, -1f, 1f);

        HandleMountedPhysics(throttleInput, steerInput);
        UpdateTreadVisuals(throttleInput, steerInput, _currentSpeed);
    }

    private void HandleMountedPhysics(float throttleInput, float steerInput)
    {
        if (_isGrounded)
        {
            Vector3 up = _groundNormal;
            Vector3 currentForward = GetPlanarForward(_rb.rotation * Vector3.forward, up);

            if (currentForward.sqrMagnitude < 0.0001f)
                currentForward = _lastGroundForward;

            float speed01 = Mathf.InverseLerp(0f, maxForwardSpeed, Mathf.Abs(_currentSpeed));
            float steerFactor = Mathf.Lerp(standstillSteerFactor, 1f, speed01);
            float steerStep = steerInput * steerDegreesPerSecond * steerFactor * Time.fixedDeltaTime;

            Vector3 desiredForward = Quaternion.AngleAxis(steerStep, up) * currentForward;
            desiredForward = GetPlanarForward(desiredForward, up);

            if (desiredForward.sqrMagnitude < 0.0001f)
                desiredForward = currentForward;

            desiredForward.Normalize();
            _lastGroundForward = desiredForward;

            Quaternion targetRotation = Quaternion.LookRotation(desiredForward, up);
            _rb.MoveRotation(targetRotation);

            float slopeAngle = Vector3.Angle(up, Vector3.up);
            bool movingUphill = Vector3.Dot(desiredForward, Vector3.up) > 0.01f;
            bool slopeTooSteepToDrive = slopeAngle > maxDriveSlopeAngle && movingUphill;

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
                accel = brakingDeceleration;
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);

            if (!slopeTooSteepToDrive && throttleInput > 0.01f)
            {
                float uphill01 = Mathf.Clamp01(Vector3.Dot(desiredForward, Vector3.up));
                _currentSpeed = Mathf.Min(
                    maxForwardSpeed,
                    _currentSpeed + uphill01 * slopeClimbAssist * Time.fixedDeltaTime);
            }

            Vector3 currentPosition = _rb.position;
            Vector3 moveDelta = desiredForward * (_currentSpeed * Time.fixedDeltaTime);

            float currentHeightAlongNormal = Vector3.Dot(currentPosition - _groundPoint, up);
            float heightError = rideHeight - currentHeightAlongNormal;
            Vector3 snapOffset = up * (heightError * Mathf.Clamp01(rideHeightSnapStrength * Time.fixedDeltaTime));

            Vector3 targetPosition = currentPosition + moveDelta + snapOffset;
            _rb.MovePosition(targetPosition);

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, airborneSpeedDecay * Time.fixedDeltaTime);

            Vector3 forward = (_rb.rotation * Vector3.forward).normalized;
            Vector3 planarVelocity = forward * _currentSpeed;
            Vector3 verticalVelocity = Vector3.Project(_rb.linearVelocity, Vector3.up);

            _rb.linearVelocity = planarVelocity + verticalVelocity;
            _rb.AddForce(Physics.gravity * (airborneGravityMultiplier - 1f), ForceMode.Acceleration);
        }
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
                _rb.MoveRotation(targetRotation);
            }
        }
    }

    private void UpdateGrounding()
    {
        _isGrounded = false;
        _groundNormal = Vector3.up;
        _groundPoint = transform.position;
        _groundedProbeCount = 0;

        Vector3 normalSum = Vector3.zero;
        Vector3 pointSum = Vector3.zero;

        SampleProbe(frontLeftProbe, ref normalSum, ref pointSum);
        SampleProbe(frontRightProbe, ref normalSum, ref pointSum);
        SampleProbe(rearLeftProbe, ref normalSum, ref pointSum);
        SampleProbe(rearRightProbe, ref normalSum, ref pointSum);

        if (_groundedProbeCount <= 0)
            return;

        _isGrounded = true;
        _groundNormal = normalSum.normalized;
        _groundPoint = pointSum / _groundedProbeCount;
    }

    private void SampleProbe(Transform probe, ref Vector3 normalSum, ref Vector3 pointSum)
    {
        if (probe == null)
            return;

        Vector3 rayOrigin = probe.position + Vector3.up * 0.2f;

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, probeRayLength, groundMask, QueryTriggerInteraction.Ignore))
            return;

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (slopeAngle > maxGroundSampleAngle)
            return;

        _groundedProbeCount++;
        normalSum += hit.normal;
        pointSum += hit.point;
    }

    private static Vector3 GetPlanarForward(Vector3 rawForward, Vector3 up)
    {
        Vector3 forward = Vector3.ProjectOnPlane(rawForward, up);
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return forward.normalized;
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
            return;
        }

        if (!pressed)
        {
            _wasPressed = false;
            return;
        }

        if (_wasPressed)
            return;

        _wasPressed = true;

        if (IsMounted)
            Dismount();
        else if (_playerRootInTrigger != null)
            Mount(_playerRootInTrigger);
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

        _currentSpeed = 0f;
        _playerRootInTrigger = playerRoot;
        _enterArmed = false;
        _wasPressed = true;
    }

    private void Dismount()
    {
        if (_mountedPlayer == null || dismountPoint == null)
            return;

        _mountedPlayer.transform.SetParent(null, true);
        _mountedPlayer.transform.position = dismountPoint.position;
        _mountedPlayer.transform.rotation = dismountPoint.rotation;

        if (_mountedPlayerRb != null)
        {
            _mountedPlayerRb.isKinematic = false;
            _mountedPlayerRb.detectCollisions = true;
            _mountedPlayerRb.linearVelocity = _rb != null ? (_rb.rotation * Vector3.forward) * _currentSpeed : Vector3.zero;
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
        GameObject root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        _enterArmed = false;
        _playerRootInTrigger = root;
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        if (_playerRootInTrigger == root)
            _playerRootInTrigger = null;
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

    public bool IsPromptAvailable => IsMounted || _playerRootInTrigger != null;
    public string PromptActionText => "Interact";
    public string PromptDescriptionText => IsMounted ? dismountPrompt : mountPrompt;
    public bool PromptUsesHold => false;
    public float PromptHoldDuration => 0f;
    public Vector3 PromptWorldPosition => seatPoint != null ? seatPoint.position : transform.position;
    public int PromptPriority => promptPriority;
}