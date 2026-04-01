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
    [SerializeField] private float acceleration = 28f;
    [SerializeField] private float reverseAcceleration = 16f;
    [SerializeField] private float maxForwardSpeed = 22f;
    [SerializeField] private float maxReverseSpeed = 8f;
    [SerializeField] private float steerSpeed = 85f;
    [SerializeField] private float drag = 1.5f;

    [SerializeField] private float groundedDownforce = 35f;
    [SerializeField] private float hoverRayLength = 1.8f;
    [SerializeField] private float rideHeight = 0.9f;
    [SerializeField] private float suspensionStrength = 90f;
    [SerializeField] private float suspensionDamping = 12f;
    [SerializeField] private float airSteerMultiplier = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    [SerializeField] private float uphillAssistForce = 30f;
    [SerializeField] private float downhillBrakeFactor = 0.15f;
    [SerializeField] private float activeThrottleDrag = 0.35f;
    [SerializeField] private float coastDrag = 1.1f;
    [SerializeField] private float reverseGripScale = 0.7f;

    [Header("Ground Probes")]
    [SerializeField] private Transform frontLeftProbe;
    [SerializeField] private Transform frontRightProbe;
    [SerializeField] private Transform rearLeftProbe;
    [SerializeField] private Transform rearRightProbe;

    [Header("Tread Visuals")]
    [SerializeField] private Transform leftTreadRoot;
    [SerializeField] private Transform rightTreadRoot;
    [SerializeField] private Transform leftTreadVisual;
    [SerializeField] private Transform rightTreadVisual;
    [SerializeField] private float treadVisualYOffset = 0.02f;
    [SerializeField] private float treadSpinDegreesPerMeter = 360f;
    [SerializeField] private float treadSteerVisualYaw = 12f;
    [SerializeField] private float treadAlignLerpSpeed = 12f;

    [Header("Vehicle Grounding")]
    [SerializeField] private float probeRayLength = 2.2f;
    [SerializeField] private float suspensionRestDistance = 0.9f;
    [SerializeField] private float suspensionStrengthPerProbe = 55f;
    [SerializeField] private float suspensionDampingPerProbe = 9f;
    [SerializeField] private float maxClimbAngle = 65f;
    [SerializeField] private float tractionForce = 36f;
    [SerializeField] private float slopeStickForce = 4f;
    [SerializeField] private float lateralGrip = 6f;
    [SerializeField] private float alignToGroundSpeed = 10f;
    [SerializeField] private float airborneGravityMultiplier = 1.4f;
    [SerializeField] private float groundedSteerMultiplier = 1f;

    [SerializeField] private float idleGroundBrake = 8f;
    [SerializeField] private float maxSpringAccelPerProbe = 18f;
    [SerializeField] private float maxDownforcePerProbe = 12f;
    [SerializeField] private float maxAlignDegreesPerSecond = 180f;
    [SerializeField] private float groundedSpeedDrag = 2.2f;
    [SerializeField] private float unmountedBrakeDrag = 6f;

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

    private Quaternion _leftTreadBaseLocalRot;
    private Quaternion _rightTreadBaseLocalRot;
    private float _leftTreadSpin;
    private float _rightTreadSpin;

    private int _groundedProbeCount;
    private Vector3 _averagedGroundNormal = Vector3.up;
    private Vector3 _averagedGroundPoint = Vector3.zero;

    private Vector3 _leftTreadBaseLocalPos;
    private Vector3 _rightTreadBaseLocalPos;
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
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.useGravity = true;

        _rb.mass = Mathf.Max(180f, _rb.mass);
        _rb.linearDamping = 1.25f;
        _rb.angularDamping = 4.5f;
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
            ApplyPassiveStabilization();
            ApplyGroundConform(false, 0f);
            UpdateTreadVisuals(0f, 0f, 0f);
            return;
        }

        if (moveAction == null || moveAction.action == null)
            return;

        Vector2 move = moveAction.action.ReadValue<Vector2>();
        float throttle = move.y;
        float steer = move.x;

        ApplyGroundConform(true, steer);

        Vector3 driveForward = GetDriveForward();
        Vector3 planarVel = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        float forwardSpeed = Vector3.Dot(planarVel, driveForward);

        // Strong projected traction.
        float baseAccel = throttle >= 0f ? acceleration : reverseAcceleration;
        float throttleAbs = Mathf.Abs(throttle);

        if (throttleAbs > 0.001f)
        {
            _rb.AddForce(driveForward * (throttle * baseAccel), ForceMode.Acceleration);

            if (_isGrounded)
            {
                float tractionScale = throttle >= 0f ? 1f : reverseGripScale;
                _rb.AddForce(driveForward * (throttle * tractionForce * tractionScale), ForceMode.Acceleration);

                // Extra uphill assist so the snowmobile feels meaningfully better than skis.
                float uphill01 = Mathf.Clamp01(Vector3.Dot(driveForward, Vector3.up));
                if (throttle > 0f && uphill01 > 0f)
                    _rb.AddForce(driveForward * (uphillAssistForce * uphill01 * throttle), ForceMode.Acceleration);
            }
        }

        // Strong lateral grip while grounded so it does not feel like a sled drifting sideways.
        if (_isGrounded)
        {
            Vector3 lateral = planarVel - driveForward * forwardSpeed;
            float gripScale = throttle >= 0f ? 1f : reverseGripScale;
            _rb.AddForce(-lateral * lateralGrip * gripScale, ForceMode.Acceleration);
        }

        // Passive braking only when not really accelerating.
        float appliedBrake = 0f;
        if (_isGrounded)
        {
            if (throttleAbs < 0.05f)
            {
                _rb.AddForce(-planarVel * idleGroundBrake, ForceMode.Acceleration);
                appliedBrake = idleGroundBrake;
            }
            else if (throttle < 0f && forwardSpeed > 0.5f)
            {
                // Reverse input while still moving forward behaves like a brake.
                float reverseBrake = idleGroundBrake * (1f + downhillBrakeFactor);
                _rb.AddForce(-driveForward * reverseBrake, ForceMode.Acceleration);
                appliedBrake = reverseBrake;
            }
        }

        // Recompute after forces for clamping and drag shaping.
        planarVel = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        forwardSpeed = Vector3.Dot(planarVel, driveForward);

        float maxForward = maxForwardSpeed;
        float maxReverse = maxReverseSpeed;
        float clampedForwardSpeed = Mathf.Clamp(forwardSpeed, -maxReverse, maxForward);

        Vector3 lateralAfterGrip = planarVel - driveForward * forwardSpeed;

        float dragFactor = throttleAbs > 0.05f ? activeThrottleDrag : coastDrag;
        Vector3 dampedLateral = lateralAfterGrip * Mathf.Clamp01(1f - dragFactor * Time.fixedDeltaTime);

        Vector3 newPlanar = driveForward * clampedForwardSpeed + dampedLateral;

        Vector3 vertical = _rb.linearVelocity - Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        _rb.linearVelocity = newPlanar + vertical;

        UpdateTreadVisuals(throttle, steer, clampedForwardSpeed);
    }

    private void HandleMountInput()
    {
        if (interactAction == null || interactAction.action == null)
            return;

        bool pressed = interactAction.action.IsPressed();

        if (!_enterArmed)
        {
            if (!pressed) _enterArmed = true;
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

        // Put the player into walk mode so skis/poles go to the back and ski systems are disabled.
        if (_mountedWalk != null)
            _mountedWalk.ForceEnterWalkMode();
        else if (_mountedSki != null)
            _mountedSki.enabled = false;

        // Remove rider physics influence while mounted.
        if (_mountedPlayerRb != null)
        {
            _mountedPlayerRb.linearVelocity = Vector3.zero;
            _mountedPlayerRb.angularVelocity = Vector3.zero;
            _mountedPlayerRb.isKinematic = true;
            _mountedPlayerRb.detectCollisions = false;
        }

        // Disable the rider colliders so they do not clip into the snowmobile / ground.
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

        // Return the player to ski mode when leaving the snowmobile.
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

    private void UpdateGrounding()
    {
        _isGrounded = false;
        _groundNormal = Vector3.up;
        _averagedGroundNormal = Vector3.up;
        _averagedGroundPoint = transform.position;
        _groundedProbeCount = 0;

        Vector3 normalSum = Vector3.zero;
        Vector3 pointSum = Vector3.zero;

        SampleProbe(frontLeftProbe, ref normalSum, ref pointSum);
        SampleProbe(frontRightProbe, ref normalSum, ref pointSum);
        SampleProbe(rearLeftProbe, ref normalSum, ref pointSum);
        SampleProbe(rearRightProbe, ref normalSum, ref pointSum);

        if (_groundedProbeCount > 0)
        {
            _isGrounded = true;
            _averagedGroundNormal = normalSum.normalized;
            _groundNormal = _averagedGroundNormal;
            _averagedGroundPoint = pointSum / _groundedProbeCount;
        }
        else
        {
            _rb.AddForce(Physics.gravity * (airborneGravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }


    private Vector3 GetDriveForward()
    {
        Vector3 up = _isGrounded ? _groundNormal : Vector3.up;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        forward.Normalize();

        return forward;
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

        // IMPORTANT:
        // Do NOT move or rotate the tread root in world space.
        // Only animate the tread visual locally.
        float steerYaw = steer * treadSteerVisualYaw * (isLeft ? 1f : -1f) * 0.35f;
        spinDegrees += (forwardSpeed * treadSpinDegreesPerMeter) * Time.fixedDeltaTime;

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

    private void SampleProbe(Transform probe, ref Vector3 normalSum, ref Vector3 pointSum)
    {
        if (probe == null)
            return;

        Vector3 origin = probe.position + Vector3.up * 0.2f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeRayLength, groundMask, QueryTriggerInteraction.Ignore))
            return;

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (slopeAngle > maxClimbAngle)
            return;

        _groundedProbeCount++;
        normalSum += hit.normal;
        pointSum += hit.point;
    }

    private void ApplyGroundConform(bool allowSteer, float steerInput)
    {
        if (!_isGrounded)
            return;

        Vector3 up = _groundNormal;

        Vector3 forwardOnGround = Vector3.ProjectOnPlane(transform.forward, up);
        if (forwardOnGround.sqrMagnitude < 0.0001f)
            forwardOnGround = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        forwardOnGround.Normalize();

        if (allowSteer)
        {
            float steerYaw = steerInput * steerSpeed * 0.35f * Time.fixedDeltaTime;
            forwardOnGround = Quaternion.AngleAxis(steerYaw, up) * forwardOnGround;
            forwardOnGround.Normalize();
        }

        Quaternion targetRot = Quaternion.LookRotation(forwardOnGround, up);
        Quaternion nextRot = Quaternion.RotateTowards(_rb.rotation, targetRot, maxAlignDegreesPerSecond * Time.fixedDeltaTime);
        _rb.MoveRotation(nextRot);

        // No vertical lift correction here.
        // Just add a small stick-to-ground force so it stays planted.
        _rb.AddForce(-up * slopeStickForce, ForceMode.Acceleration);
    }

    private void ApplyPassiveStabilization()
    {
        Vector3 planar = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        _rb.AddForce(-planar * Mathf.Max(0f, unmountedBrakeDrag), ForceMode.Acceleration);

        if (_isGrounded)
            ApplyGroundConform(false, 0f);
    }

    
    private void OnTriggerEnter(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        _enterArmed = false;
        _playerRootInTrigger = root;
    }

    private void OnTriggerExit(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null)
            return;

        if (_playerRootInTrigger == root)
            _playerRootInTrigger = null;
    }

    private static GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null)
            return null;

        // Never allow NPCs to claim player interactions.
        var t = other.transform;
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

        var pi = other.GetComponentInParent<PlayerInput>();
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