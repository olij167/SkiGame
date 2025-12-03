using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ski controller driven purely by per-ski inputs:
/// - LeftSki / RightSki actions control stance/edge per leg.
/// - Optional Lean action controls forward/back lean.
/// - Optional Poles action controls pole pushes/brakes (tap = push, hold = brake).
/// - Skis define forward direction and friction rails.
/// - Downhill speed comes from slope + ski direction + lean.
/// - Turning emerges from ski direction, anisotropic friction,
///   and a gentle carve steering step (no direct yaw from Move.x).
/// - Skating uses forward lean + alternating leg pushes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class SkiController : MonoBehaviour
{
    public enum PoleStrokePhase
    {
        Idle,
        Entry,
        Drag,
        FollowThrough
    }
    // ----------------------------------------------------------------------
    // REFERENCES
    // ----------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Optional visual body root that leans independently of the skis.")]
    [SerializeField] private Transform bodyTransform;

    [Tooltip("Left ski transform (child of this object).")]
    [SerializeField] private Transform leftSki;
    [Tooltip("Per-ski helper for the left ski (auto-assigned from leftSki if null).")]
    [SerializeField] private SkiContact leftSkiContact;

    [Tooltip("Right ski transform (child of this object).")]
    [SerializeField] private Transform rightSki;
    [Tooltip("Per-ski helper for the right ski (auto-assigned from rightSki if null).")]
    [SerializeField] private SkiContact rightSkiContact;

    [Tooltip("Optional helper for the left pole (for contact & visuals).")]
    [SerializeField] private PoleContact leftPoleContact;

    [Tooltip("Optional helper for the right pole (for contact & visuals).")]
    [SerializeField] private PoleContact rightPoleContact;

    [Header("Input (New Input System)")]
    [Tooltip("Float action for left leg stance/edge (0..1).")]
    [SerializeField] private InputActionReference leftSkiAction;

    [Tooltip("Float action for right leg stance/edge (0..1).")]
    [SerializeField] private InputActionReference rightSkiAction;

    [Tooltip("Optional float action for forward/back lean (-1..1). If omitted, lean is neutral.")]
    [SerializeField] private InputActionReference leanAction;

    [Tooltip("Optional button action for poles. Hold = stroke/drag, Release = follow-through.")]
    [SerializeField] private InputActionReference polesAction;

    [Tooltip("Optional button action for jump / hop (hold to charge, release to jump).")]
    [SerializeField] private InputActionReference jumpAction;


    // ----------------------------------------------------------------------
    // INTERNAL STATE
    // ----------------------------------------------------------------------

    private Rigidbody _rb;

    // Raw inputs
    private float _rawLeftLegInput;
    private float _rawRightLegInput;
    private float _rawLeanInput;   // -1..1
    private bool _rawPolesPressed; // button down/held this frame

    // Smoothed lean & stance
    private float _forwardLean;    // [-1..1]
    private float _sideLean;       // [-1..1] (visual only)
    private float _leftOut;        // [0..1]   "edge/spread" factor
    private float _rightOut;       // [0..1]

    // Fields
    private bool _leftGrounded;
    private bool _rightGrounded;
    private RaycastHit _leftHit;
    private RaycastHit _rightHit;


    // Previous stance for skate detection
    private float _leftOutLastPhysics;
    private float _rightOutLastPhysics;

    // Grounding/orientation
    private bool _isGrounded;
    private bool _wasGrounded;
    private Vector3 _groundNormal = Vector3.up;
    private Vector3 _skiForward = Vector3.forward; // combined ski forward on plane

    // Skis base transforms (for offset & yaw)
    private Vector3 _leftSkiLocalBasePos;
    private Vector3 _rightSkiLocalBasePos;
    private Quaternion _leftSkiLocalBaseRot;
    private Quaternion _rightSkiLocalBaseRot;

    // Visual smoothing for skis (offset + yaw)
    private Vector3 _leftSkiOffsetCurrent;
    private Vector3 _rightSkiOffsetCurrent;
    private float _leftSkiYawCurrent;
    private float _rightSkiYawCurrent;

    // Stack state
    private bool _stacked;

    // Skating
    private float _lastPushTime;
    private float _lastSkateInput;

    // Poles
    // _poleStrokeT is 0..1 within the CURRENT phase, not a full 0..1 loop.
    private float _poleStrokeT;
    private PoleStrokePhase _polePhase = PoleStrokePhase.Idle;

    // Jump
    private bool _jumpQueued;
    private bool _jumpHeld;
    private float _lastJumpPressedTime;
    private float _lastGroundedTime;

    // Air rotation state (degrees/second around local axes)
    private Vector3 _airAngularVelocity;

    // ----------------------------------------------------------------------
    // PARAMETERS
    // ----------------------------------------------------------------------

    [Header("Lean & Stance")]
    [SerializeField] private float forwardLeanLerpSpeed = 5f;
    [SerializeField] private float stanceLerpSpeed = 10f;

    [Tooltip("How fast the visual ski models interpolate toward their target yaw/offset.")]
    [SerializeField] private float skiVisualLerpSpeed = 12f;

    [Tooltip("How fast the follower ski catches up to the lead ski stance when only one leg is active.")]
    [SerializeField] private float followerStanceLerpSpeed = 4f;

    [Tooltip("Max lateral offset (m) each ski can be pushed out from center.")]
    [SerializeField] private float maxSkiOffset = 0.3f;

    [Tooltip("Max inward yaw (degrees) when ski is fully edged/angled at high speed.")]
    [SerializeField] private float maxSkiEdgeAngle = 20f;

    [Tooltip("Max visual forward lean angle for the body (degrees).")]
    [SerializeField] private float maxForwardLeanAngle = 25f;

    [Tooltip("Max visual side lean angle for the body (degrees).")]
    [SerializeField] private float maxSideLeanAngle = 15f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckHeight = 0.6f;
    [SerializeField] private float groundCheckDistance = 1.2f;

    [Header("Ski Suspension")]
    [SerializeField] private float skiHeightOffset = 0.03f; // how much the ski hovers above the hit point
    [SerializeField] private float skiSuspensionLerp = 12f; // how fast skis follow the ground


    [Header("Downhill")]
    [Tooltip("Min downhill acceleration when leaning fully back.")]
    [SerializeField] private float downhillAccelMin = 2f;

    [Tooltip("Max downhill acceleration when leaning fully forward.")]
    [SerializeField] private float downhillAccelMax = 12f;

    [Tooltip("Minimum slope angle (deg) before we consider it a slope where lean affects speed.")]
    [SerializeField] private float minSlopeAngleForDownhill = 1f;

    [Tooltip("Minimum alignment (dot) between ski direction and fall line before gravity meaningfully pulls you downhill. 0 = perpendicular (90°), 1 = perfectly aligned.")]
    [SerializeField, Range(0f, 1f)]
    private float minAlignmentForDownhill = 0.25f; // ~75° from fall line

    [Header("Friction (Per-Ski, Anisotropic)")]
    [Tooltip("Base friction along ski direction (low = more glide).")]
    [SerializeField] private float forwardFriction = 0.25f;

    [Tooltip("Base friction across ski direction (higher = stronger edge / carve).")]
    [SerializeField] private float sideFriction = 1.2f;

    [Tooltip("Strength of killing any into-ground velocity component.")]
    [SerializeField] private float normalKillStrength = 8f;

    [Tooltip("Maximum upward speed along the ground normal that will be damped while grounded.\n" +
             "Small upward speeds (typical of riding over small bumps/crests) are reduced so you stay 'glued' to the snow.\n" +
             "Larger upward impulses (e.g. explicit jumps) are left alone.")]
    [SerializeField] private float maxStickUpwardSpeed = 2f;

    [Header("Carve Steering")]
    [Tooltip("How strongly velocity is rotated toward ski direction when skis are parallel and edged.")]
    [SerializeField] private float carveSteerStrength = 4f;

    [Header("Carve vs Skate Balance")]
    [Tooltip("Below this planar speed, ski yaw is heavily reduced so leg inputs feel more like skating than carving.")]
    [SerializeField] private float minCarveSpeed = 3f;

    [Tooltip("Above this planar speed, ski yaw reaches full maxSkiEdgeAngle.")]
    [SerializeField] private float maxCarveSpeed = 12f;

    [Tooltip("Max ski yaw (deg) when moving very slowly (used at or below MinCarveSpeed).")]
    [SerializeField] private float lowSpeedMaxYaw = 6f;

    [Tooltip("Minimum planar speed before we apply waddle yaw from skate pushes.")]
    [SerializeField] private float minSpeedForSkateYaw = 1.5f;

    [Header("Skating")]
    [Tooltip("Minimum forward lean required to generate a skate push.")]
    [SerializeField] private float minForwardLeanForPush = 0.1f;

    [Tooltip("Velocity change applied along ski direction for each valid push.")]
    [SerializeField] private float skateImpulse = 2.5f;

    [Tooltip("Cooldown between pushes (seconds).")]
    [SerializeField] private float skateCooldown = 0.2f;

    [Tooltip("Max speed at which skate pushes are fully effective. Above this they taper off.")]
    [SerializeField] private float skateMaxEffectiveSpeed = 6f;

    [Tooltip("Yaw degrees applied per skate push (small waddle twist).")]
    [SerializeField] private float skateYawPerPush = 4f;

    [Header("Poles")]
    [Tooltip("Baseline speed at which the pole stroke value (0..1) moves when the player is stationary.")]
    [SerializeField] private float basePoleStrokeSpeed = 2f;

    [Tooltip("Extra stroke speed added as the player's planar speed approaches 'strokeSpeedBoostAtSpeed'.")]
    [SerializeField] private float maxPoleStrokeSpeedBoost = 2f;

    [Tooltip("Planar speed (m/s) at which the pole stroke reaches maximum speed boost.")]
    [SerializeField] private float strokeSpeedBoostAtSpeed = 10f;

    [Tooltip("Impulse magnitude applied by a full effective pole stroke (used during drag/follow-through).")]
    [SerializeField] private float poleImpulse = 3f;

    [Tooltip("Above this planar speed, pole stroke propulsion/drag tapers off.")]
    [SerializeField] private float poleMaxSpeed = 8f;

    [Tooltip("Drag strength applied along the direction of motion while poles are dug in.")]
    [SerializeField] private float poleBrakeStrength = 10f;

    [Tooltip("Minimum effectiveness of pole strokes at or above poleMaxSpeed (0 = no effect, 1 = full effect).")]
    [Range(0f, 1f)]
    [SerializeField] private float minPoleSpeedFactor = 0.1f;

    [Header("Landing / Stack")]
    [Tooltip("Max allowed tilt angle (deg) between skier up and ground normal to count as a safe landing.")]
    [SerializeField] private float maxLandingTiltAngle = 50f;

    [Tooltip("Max allowed misalignment (deg) between combined ski direction and planar velocity at landing.")]
    [SerializeField] private float maxLandingMisalignmentAngle = 75f;

    [Tooltip("Minimum planar speed where misalignment is considered for stacking.")]
    [SerializeField] private float minLandingSpeedForStackCheck = 5f;

    [Tooltip("Torque impulse applied when stacking (to topple the skier).")]
    [SerializeField] private float stackTorqueImpulse = 30f;

    [Header("Orientation")]
    [Tooltip("How fast the root rotates toward the desired direction on the slope.")]
    [SerializeField] private float groundTurnSpeed = 8f;

    [Header("Air Control (optional)")]
    [Tooltip("Maximum yaw turn speed in the air (deg/sec), using leg difference as input.")]
    [SerializeField] private float airYawTurnSpeed = 360f;

    [Tooltip("Maximum pitch turn speed in the air (deg/sec), using lean as input.")]
    [SerializeField] private float airPitchTurnSpeed = 360f;

    [Tooltip("How quickly air rotation responds to input (deg/sec^2). Higher = snappier spins/flips.")]
    [SerializeField] private float airAngularAcceleration = 720f;

    [Tooltip("Extra damping applied to air angular velocity when there is little/no input (deg/sec^2).")]
    [SerializeField] private float airAngularDamping = 360f;

    [Tooltip("Multiplier applied to air spin responsiveness while poles input is held (tuck).")]
    [SerializeField] private float airTuckSpinMultiplier = 1.5f;

    [Header("Jump")]
    [Tooltip("Min/Max vertical velocity change applied when jumping.")]
    [SerializeField] private Vector2 jumpForceRange = new Vector2(3f, 8f);

    [Tooltip("Time window after leaving the ground during which a jump press will still be accepted (seconds).")]
    [SerializeField] private float jumpCoyoteTime = 0.15f;

    [Tooltip("Time window a jump press is buffered so it can fire on the next valid ground contact (seconds).")]
    [SerializeField] private float jumpBufferTime = 0.10f;

    // ----------------------------------------------------------------------
    // PROPERTIES / FLAGS
    // ----------------------------------------------------------------------

    private bool HasLegInputs =>
     leftSkiAction != null && leftSkiAction.action != null &&
     rightSkiAction != null && rightSkiAction.action != null;

    private bool HasLeanInput =>
        leanAction != null && leanAction.action != null;

    private bool HasPolesInput =>
        polesAction != null && polesAction.action != null;

    private bool HasJumpInput =>
        jumpAction != null && jumpAction.action != null;

    public bool IsStacked => _stacked;

    // Stroke progress 0..1 within the CURRENT phase (Entry / FollowThrough).
    public float PoleStrokeT => _poleStrokeT;

    // Current stroke phase (Idle, Entry, Drag, FollowThrough).
    public PoleStrokePhase CurrentPolePhase => _polePhase;

    // Is the pole input button currently held?
    public bool IsPoleInputHeld => _rawPolesPressed;

    public Vector3 GroundNormal => _groundNormal;
    public Vector3 Velocity => _rb.linearVelocity;
    public Vector3 SkiForwardOnPlane => _skiForward;
    public bool IsRiderGrounded => _isGrounded;


    // ----------------------------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------------------------

    public void RecoverFromStack(Vector3 forwardHint)
    {
        if (!_stacked) return;

        _stacked = false;
        _rb.freezeRotation = true;
        _rb.angularVelocity = Vector3.zero;

        Vector3 up = Vector3.up;
        Vector3 f = Vector3.ProjectOnPlane(forwardHint, up);
        if (f.sqrMagnitude < 0.0001f) f = transform.forward;
        transform.rotation = Quaternion.LookRotation(f.normalized, up);
    }

    // ----------------------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------------------

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        if (leftSki != null)
        {
            _leftSkiLocalBasePos = leftSki.localPosition;
            _leftSkiLocalBaseRot = leftSki.localRotation;

            if (leftSkiContact == null)
                leftSkiContact = leftSki.GetComponent<SkiContact>();
        }

        if (rightSki != null)
        {
            _rightSkiLocalBasePos = rightSki.localPosition;
            _rightSkiLocalBaseRot = rightSki.localRotation;

            if (rightSkiContact == null)
                rightSkiContact = rightSki.GetComponent<SkiContact>();
        }

        _skiForward = transform.forward;
    }

    private void OnEnable()
    {
        if (HasLegInputs)
        {
            leftSkiAction.action.Enable();
            rightSkiAction.action.Enable();
        }

        if (HasLeanInput)
        {
            leanAction.action.Enable();
        }

        if (HasPolesInput)
        {
            polesAction.action.Enable();
        }

        if (HasJumpInput)
        {
            jumpAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (HasLegInputs)
        {
            leftSkiAction.action.Disable();
            rightSkiAction.action.Disable();
        }

        if (HasLeanInput)
        {
            leanAction.action.Disable();
        }

        if (HasPolesInput)
        {
            polesAction.action.Disable();
        }

        if (HasJumpInput)
        {
            jumpAction.action.Disable();
        }
    }

    private void Update()
    {
        ReadInputs();
        UpdatePoleState();
        UpdateLeanAndStance();
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        _wasGrounded = _isGrounded;

        CheckGround();

        if (_stacked)
        {
            TryAutoRecoverFromStack();
            if (_stacked)
                return;
        }

        TryConsumeQueuedJump();

        if (_isGrounded)
        {
            ApplyGroundForces();
            DetectAndApplySkatePushes();
            ApplyPoleForces();
            AlignToSkisAndSlope();
        }
        else
        {
            ApplyAirControl();
        }

        _leftOutLastPhysics = _leftOut;
        _rightOutLastPhysics = _rightOut;
    }

    // ----------------------------------------------------------------------
    // INPUT
    // ----------------------------------------------------------------------
    private void ReadInputs()
    {
        if (HasLegInputs)
        {
            _rawLeftLegInput = Mathf.Clamp01(leftSkiAction.action.ReadValue<float>());
            _rawRightLegInput = Mathf.Clamp01(rightSkiAction.action.ReadValue<float>());
        }
        else
        {
            _rawLeftLegInput = 0f;
            _rawRightLegInput = 0f;
        }

        if (HasLeanInput)
        {
            _rawLeanInput = Mathf.Clamp(leanAction.action.ReadValue<float>(), -1f, 1f);
        }
        else
        {
            _rawLeanInput = 0f;
        }

        if (HasPolesInput)
        {
            _rawPolesPressed = polesAction.action.ReadValue<float>() > 0.5f;
        }
        else
        {
            _rawPolesPressed = false;
        }

        HandleJumpInput();
    }

    private void UpdatePoleState()
    {
        float dt = Time.deltaTime;

        // If we have no poles bound at all, always relax back to idle.
        if (!HasPolesInput)
        {
            _polePhase = PoleStrokePhase.Idle;
            _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 0f, basePoleStrokeSpeed * dt);
            return;
        }

        bool grounded = _isGrounded;
        bool pressed = _rawPolesPressed && grounded;

        // Planar speed on the current ground, used to modulate stroke speed.
        Vector3 velPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        float speed = velPlane.magnitude;

        float strokeSpeed = basePoleStrokeSpeed;

        // Boost stroke speed with player speed so strokes feel snappier at speed.
        if (strokeSpeedBoostAtSpeed > 0.01f && maxPoleStrokeSpeedBoost > 0f)
        {
            float speedT = Mathf.Clamp01(speed / strokeSpeedBoostAtSpeed);
            strokeSpeed += maxPoleStrokeSpeedBoost * speedT;
        }

        // Phase flow:
        // Idle --(press)--> Entry --(t completes)--> Drag --(release)--> FollowThrough --(t completes)--> Idle
        switch (_polePhase)
        {
            case PoleStrokePhase.Idle:
                _poleStrokeT = 0f;

                if (pressed && grounded)
                {
                    _polePhase = PoleStrokePhase.Entry;
                    _poleStrokeT = 0f;
                }
                break;

            case PoleStrokePhase.Entry:
                // Entry always runs to completion once started,
                // regardless of whether the button is still held.
                _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 1f, strokeSpeed * dt);

                if (_poleStrokeT >= 0.999f)
                {
                    // Entry completed -> always visit Drag next.
                    _polePhase = PoleStrokePhase.Drag;
                    _poleStrokeT = 0f;
                }
                break;

            case PoleStrokePhase.Drag:
                // In Drag we "live" here while grounded; no strokeT progression.
                _poleStrokeT = 0f;

                if (!grounded)
                {
                    // If we leave the ground mid-drag, cancel back to idle.
                    _polePhase = PoleStrokePhase.Idle;
                    _poleStrokeT = 0f;
                    break;
                }

                if (!pressed)
                {
                    // Release from drag -> follow-through.
                    _polePhase = PoleStrokePhase.FollowThrough;
                    _poleStrokeT = 0f;
                }
                break;

            case PoleStrokePhase.FollowThrough:
                // Follow-through runs 0..1 after release.
                _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 1f, strokeSpeed * dt);

                if (_poleStrokeT >= 0.999f || !grounded)
                {
                    // End of follow-through or we left the ground: back to idle.
                    _polePhase = PoleStrokePhase.Idle;
                    _poleStrokeT = 0f;
                }
                break;
        }

        // Safety: if we become ungrounded at any time, fade back to idle.
        if (!grounded && _polePhase != PoleStrokePhase.Idle)
        {
            _polePhase = PoleStrokePhase.Idle;
            _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 0f, strokeSpeed * dt);
        }
    }

    private void HandleJumpInput()
    {
        if (!HasJumpInput)
        {
            _jumpQueued = false;
            _jumpHeld = false;
            return;
        }

        var action = jumpAction.action;
        bool pressedThisFrame = action.WasPressedThisFrame();
        bool isPressed = action.IsPressed();

        _jumpHeld = isPressed;

        if (pressedThisFrame)
        {
            // Buffer the jump so it can fire on the next valid ground / coyote frame.
            _lastJumpPressedTime = Time.time;
            _jumpQueued = true;
        }

        // If the button is NOT held anymore and the buffer window has elapsed,
        // drop the queued jump. If the button IS still held, we keep the
        // queued jump alive so it can auto-fire on the next valid frame.
        if (!isPressed && _jumpQueued && (Time.time - _lastJumpPressedTime > jumpBufferTime))
        {
            _jumpQueued = false;
        }
    }

    // ----------------------------------------------------------------------
    // GROUNDING & LANDING
    // ----------------------------------------------------------------------

    private void CheckGround()
    {
        bool hitSomething = false;
        Vector3 sumNormals = Vector3.zero;

        Vector3 centerOrigin = transform.position + Vector3.up * groundCheckHeight;
        float maxDist = groundCheckHeight + groundCheckDistance;

        if (Physics.SphereCast(centerOrigin, groundCheckRadius, Vector3.down,
                               out RaycastHit centerHit, maxDist, groundLayers,
                               QueryTriggerInteraction.Ignore))
        {
            hitSomething = true;
            sumNormals += centerHit.normal;
        }

        _leftGrounded = false;
        _rightGrounded = false;

        // Left ski
        if (leftSki != null)
        {
            Vector3 leftOrigin = leftSki.position + Vector3.up * groundCheckHeight;
            if (Physics.Raycast(leftOrigin, Vector3.down, out RaycastHit leftHit, maxDist,
                                groundLayers, QueryTriggerInteraction.Ignore))
            {
                hitSomething = true;
                sumNormals += leftHit.normal;
                _leftGrounded = true;
                _leftHit = leftHit;
            }
        }

        // Right ski
        if (rightSki != null)
        {
            Vector3 rightOrigin = rightSki.position + Vector3.up * groundCheckHeight;
            if (Physics.Raycast(rightOrigin, Vector3.down, out RaycastHit rightHit, maxDist,
                                groundLayers, QueryTriggerInteraction.Ignore))
            {
                hitSomething = true;
                sumNormals += rightHit.normal;
                _rightGrounded = true;
                _rightHit = rightHit;
            }
        }

        if (hitSomething)
        {
            _isGrounded = true;
            _groundNormal = sumNormals.normalized;

            // Track timing for coyote jumps and reset air spin on landing.
            _lastGroundedTime = Time.time;

            if (!_wasGrounded)
            {
                // Just landed this frame.
                _airAngularVelocity = Vector3.zero;
                EvaluateLanding();
            }
        }
        else
        {
           
            _isGrounded = false;
            _groundNormal = Vector3.up;
        }

    }

    private void EvaluateLanding()
    {
        float tiltAngle = Vector3.Angle(transform.up, _groundNormal);

        Vector3 velOnPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        float misalignAngle = 0f;
        if (planarSpeed > 0.01f)
        {
            Vector3 skiDir = GetCombinedSkiForwardOnPlane();
            if (skiDir.sqrMagnitude > 0.0001f)
            {
                Vector3 velDir = velOnPlane.normalized;

                // Raw angle between ski forward and planar velocity
                float rawAngle = Vector3.Angle(skiDir, velDir);

                // Treat 0° and 180° as "aligned" and 90° as most misaligned.
                // This allows landing while sliding backwards along your skis
                // without being flagged as a sideways crash.
                misalignAngle = Mathf.Min(rawAngle, 180f - rawAngle);
            }
        }

        bool tooTilted = tiltAngle > maxLandingTiltAngle;
        bool tooMisaligned = planarSpeed > minLandingSpeedForStackCheck &&
                             misalignAngle > maxLandingMisalignmentAngle;

        if (tooTilted || tooMisaligned)
        {
            TriggerStack();
        }
        else
        {
            PreserveLandingVelocityOnSlope();
            SnapOrientationToGround();
        }
    }

    private void PreserveLandingVelocityOnSlope()
    {
        Vector3 vel = _rb.linearVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f)
            return;

        Vector3 planar = Vector3.ProjectOnPlane(vel, _groundNormal);
        if (planar.sqrMagnitude < 0.0001f)
            return;

        _rb.linearVelocity = planar.normalized * speed;
    }

    private void TriggerStack()
    {
        if (_stacked) return;

        _stacked = true;
        _rb.freezeRotation = false;

        Vector3 randomAxis = Random.onUnitSphere;
        _rb.AddTorque(randomAxis * stackTorqueImpulse, ForceMode.Impulse);
    }

    private void SnapOrientationToGround()
    {
        Vector3 skiDir = GetCombinedSkiForwardOnPlane();
        if (skiDir.sqrMagnitude < 0.0001f)
        {
            skiDir = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
            if (skiDir.sqrMagnitude < 0.0001f)
            {
                skiDir = Vector3.ProjectOnPlane(Vector3.forward, _groundNormal);
            }
        }

        Quaternion targetRot = Quaternion.LookRotation(skiDir.normalized, _groundNormal);
        transform.rotation = targetRot;

        _rb.angularVelocity = Vector3.zero;
        _rb.freezeRotation = true;
    }
    private void TryAutoRecoverFromStack()
    {
        if (!_stacked || !_isGrounded)
            return;

        float tiltAngle = Vector3.Angle(transform.up, _groundNormal);

        // Once we're within the normal landing tilt limit again, treat this as
        // having "gotten back onto our feet" and recover.
        float recoverThreshold = maxLandingTiltAngle;

        if (tiltAngle <= recoverThreshold)
        {
            Vector3 forwardHint = GetCombinedSkiForwardOnPlane();
            if (forwardHint.sqrMagnitude < 0.0001f)
            {
                forwardHint = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
            }

            RecoverFromStack(forwardHint);
        }
    }


    // ----------------------------------------------------------------------
    // LEAN & STANCE (INCLUDING LEAD/FOLLOW)
    // ----------------------------------------------------------------------

    private void UpdateLeanAndStance()
    {
        // Forward lean: from lean input if present, else neutral (0).
        float targetForwardLean = Mathf.Clamp(_rawLeanInput, -1f, 1f);
        _forwardLean = Mathf.MoveTowards(_forwardLean, targetForwardLean,
                                         forwardLeanLerpSpeed * Time.deltaTime);

        float targetLeftOut = 0f;
        float targetRightOut = 0f;

        if (!_stacked && HasLegInputs)
        {
            float leftRaw = Mathf.Clamp01(_rawLeftLegInput);
            float rightRaw = Mathf.Clamp01(_rawRightLegInput);

            const float activeThreshold = 0.2f;
            bool leftActive = leftRaw > activeThreshold;
            bool rightActive = rightRaw > activeThreshold;

            if (leftActive && !rightActive)
            {
                // Left leg leads, right conforms to it.
                targetLeftOut = leftRaw;
                float followTarget = leftRaw;
                targetRightOut = Mathf.MoveTowards(_rightOut, followTarget,
                                                   followerStanceLerpSpeed * Time.deltaTime);
            }
            else if (rightActive && !leftActive)
            {
                // Right leg leads, left conforms to it.
                targetRightOut = rightRaw;
                float followTarget = rightRaw;
                targetLeftOut = Mathf.MoveTowards(_leftOut, followTarget,
                                                  followerStanceLerpSpeed * Time.deltaTime);
            }
            else if (leftActive && rightActive)
            {
                // Both held: respect both -> wedge or parallel as per user input.
                targetLeftOut = leftRaw;
                targetRightOut = rightRaw;
            }
            else
            {
                targetLeftOut = 0f;
                targetRightOut = 0f;
            }
        }
        else
        {
            targetLeftOut = 0f;
            targetRightOut = 0f;
        }

        _leftOut = Mathf.MoveTowards(_leftOut, targetLeftOut, stanceLerpSpeed * Time.deltaTime);
        _rightOut = Mathf.MoveTowards(_rightOut, targetRightOut, stanceLerpSpeed * Time.deltaTime);

        _leftOut = Mathf.Clamp01(_leftOut);
        _rightOut = Mathf.Clamp01(_rightOut);

        float targetSideLean = Mathf.Clamp(_rightOut - _leftOut, -1f, 1f);
        _sideLean = Mathf.MoveTowards(_sideLean, targetSideLean, forwardLeanLerpSpeed * Time.deltaTime);
    }

    // ----------------------------------------------------------------------
    // VISUALS
    // ----------------------------------------------------------------------

    private void UpdateVisuals()
    {
        // ------------------------------
        // Body lean
        // ------------------------------
        if (bodyTransform != null)
        {
            float pitch = _forwardLean * maxForwardLeanAngle;
            float roll = -_sideLean * maxSideLeanAngle;
            bodyTransform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        // ------------------------------
        // Decide: carve vs wedge
        // ------------------------------
        const float activeThreshold = 0.2f;
        bool leftActive = _rawLeftLegInput > activeThreshold;
        bool rightActive = _rawRightLegInput > activeThreshold;
        bool bothActive = leftActive && rightActive;

        bool leftLead = leftActive && !rightActive;
        bool rightLead = rightActive && !leftActive;

        // Scale how much the skis are allowed to yaw based on speed:
        // - At low speeds, yaw is clamped so leg pumps mostly act like skating.
        // - As you speed up, you get the full edge angle for carving.
        float planarSpeed = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal).magnitude;

        float carveT = 0f;
        if (maxCarveSpeed > minCarveSpeed)
        {
            carveT = Mathf.InverseLerp(minCarveSpeed, maxCarveSpeed, planarSpeed);
        }
        float effectiveMaxYaw = Mathf.Lerp(lowSpeedMaxYaw, maxSkiEdgeAngle, carveT);

        float leftYaw = 0f;
        float rightYaw = 0f;

        // dominantSign: -1 = carving "left", +1 = carving "right"
        float dominantSign = 0f;
        if (!bothActive)
        {
            if (leftLead)
                dominantSign = -1f;
            else if (rightLead)
                dominantSign = +1f;
        }

        if (bothActive)
        {
            // WEDGE: toes-in toward the center.
            leftYaw = +effectiveMaxYaw * _leftOut;
            rightYaw = -effectiveMaxYaw * _rightOut;
        }
        else if (Mathf.Abs(dominantSign) > 0f)
        {
            // CARVE: both skis yaw the SAME way, so they are parallel.
            float carveFactor = Mathf.Clamp01(Mathf.Max(_leftOut, _rightOut));
            float carveYaw = effectiveMaxYaw * carveFactor * dominantSign;

            leftYaw = carveYaw;
            rightYaw = carveYaw;
        }
        else
        {
            // Neutral stance: skis flat forwards.
            leftYaw = 0f;
            rightYaw = 0f;
        }

        // ------------------------------
        // Offsets for each ski (lead moves out, follower stays inner)
        // ------------------------------
        Vector3 leftOffset = Vector3.zero;
        Vector3 rightOffset = Vector3.zero;

        if (bothActive)
        {
            // WEDGE: both legs outwards
            leftOffset = Vector3.right * (-_leftOut * maxSkiOffset);
            rightOffset = Vector3.right * (_rightOut * maxSkiOffset);
        }
        else if (leftLead)
        {
            // CARVE LEFT: only LEFT leg moves to outer position,
            // right leg stays in its inner/base position but shares yaw.
            leftOffset = Vector3.right * (-_leftOut * maxSkiOffset);
            rightOffset = Vector3.zero;
        }
        else if (rightLead)
        {
            // CARVE RIGHT: only RIGHT leg moves to outer position,
            // left leg stays in its inner/base position but shares yaw.
            rightOffset = Vector3.right * (_rightOut * maxSkiOffset);
            leftOffset = Vector3.zero;
        }
        else
        {
            // Neutral: both inner/base positions
            leftOffset = Vector3.zero;
            rightOffset = Vector3.zero;
        }

        // Smoothly blend the visual skis toward their target pose so they
        // don't snap when you change inputs or transition in/out of a wedge.
        _leftSkiOffsetCurrent = Vector3.Lerp(_leftSkiOffsetCurrent, leftOffset, skiVisualLerpSpeed * Time.deltaTime);
        _rightSkiOffsetCurrent = Vector3.Lerp(_rightSkiOffsetCurrent, rightOffset, skiVisualLerpSpeed * Time.deltaTime);
        _leftSkiYawCurrent = Mathf.Lerp(_leftSkiYawCurrent, leftYaw, skiVisualLerpSpeed * Time.deltaTime);
        _rightSkiYawCurrent = Mathf.Lerp(_rightSkiYawCurrent, rightYaw, skiVisualLerpSpeed * Time.deltaTime);

        if (leftSki != null)
        {
            leftSki.localPosition = _leftSkiLocalBasePos + _leftSkiOffsetCurrent;
            leftSki.localRotation = _leftSkiLocalBaseRot * Quaternion.Euler(0f, _leftSkiYawCurrent, 0f);
        }

        if (rightSki != null)
        {
            rightSki.localPosition = _rightSkiLocalBasePos + _rightSkiOffsetCurrent;
            rightSki.localRotation = _rightSkiLocalBaseRot * Quaternion.Euler(0f, _rightSkiYawCurrent, 0f);
        }

        // Push stance into SkiContact
        if (leftSkiContact != null)
            leftSkiContact.StanceOut = _leftOut;

        if (rightSkiContact != null)
            rightSkiContact.StanceOut = _rightOut;

        // We’ll move skis in world space based on hits, then convert to local.
        if (leftSki != null && _leftGrounded)
        {
            Vector3 targetPos = _leftHit.point + _leftHit.normal * skiHeightOffset;

            // Forward on slope from SkiContact, or fallback to projected transform forward
            Vector3 fwd = leftSkiContact != null
                ? leftSkiContact.GetForwardOnPlane(_groundNormal)
                : Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;

            if (fwd.sqrMagnitude < 0.0001f)
                fwd = transform.forward;

            // Add our visual yaw on top of the slope-aligned forward
            Quaternion yawRot = Quaternion.AngleAxis(_leftSkiYawCurrent, _groundNormal);
            Quaternion targetRot = Quaternion.LookRotation(fwd, _leftHit.normal) * yawRot;

            leftSki.position = Vector3.Lerp(leftSki.position, targetPos, skiSuspensionLerp * Time.deltaTime);
            leftSki.rotation = Quaternion.Slerp(leftSki.rotation, targetRot, skiSuspensionLerp * Time.deltaTime);
        }

        if (rightSki != null && _rightGrounded)
        {
            Vector3 targetPos = _rightHit.point + _rightHit.normal * skiHeightOffset;

            Vector3 fwd = rightSkiContact != null
                ? rightSkiContact.GetForwardOnPlane(_groundNormal)
                : Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = transform.forward;

            Quaternion yawRot = Quaternion.AngleAxis(_rightSkiYawCurrent, _groundNormal);
            Quaternion targetRot = Quaternion.LookRotation(fwd, _rightHit.normal) * yawRot;

            rightSki.position = Vector3.Lerp(rightSki.position, targetPos, skiSuspensionLerp * Time.deltaTime);
            rightSki.rotation = Quaternion.Slerp(rightSki.rotation, targetRot, skiSuspensionLerp * Time.deltaTime);
        }

    }

    // ----------------------------------------------------------------------
    // SKI HELPERS
    // ----------------------------------------------------------------------

    private Vector3 GetCombinedSkiForwardOnPlane()
    {
        Vector3 forwardSum = Vector3.zero;

        if (leftSkiContact != null)
            forwardSum += leftSkiContact.GetForwardOnPlane(_groundNormal);

        if (rightSkiContact != null)
            forwardSum += rightSkiContact.GetForwardOnPlane(_groundNormal);

        if (forwardSum.sqrMagnitude < 0.0001f)
        {
            Vector3 fallback = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
            if (fallback.sqrMagnitude < 0.0001f)
            {
                fallback = Vector3.ProjectOnPlane(Vector3.forward, _groundNormal);
            }

            forwardSum = fallback;
        }

        return forwardSum.normalized;
    }

    private void AccumulateSkiFriction(SkiContact contact, Vector3 velOnPlane, ref Vector3 friction, ref int skiCount)
    {
        if (contact == null) return;

        Vector3 skiForward = contact.GetForwardOnPlane(_groundNormal);
        if (skiForward.sqrMagnitude < 0.0001f) return;

        Vector3 skiRight = Vector3.Cross(_groundNormal, skiForward);
        float stance = Mathf.Clamp01(contact.StanceOut);

        float vAlong = Vector3.Dot(velOnPlane, skiForward);
        float vAcross = Vector3.Dot(velOnPlane, skiRight);

        Vector3 vAlongVec = skiForward * vAlong;
        Vector3 vAcrossVec = skiRight * vAcross;

        // Edge factor: when stance is 0, skis are nearly flat => lower effective friction.
        // When stance is 1, skis are fully edged => stronger carve & stability.
        float edgeFactor = Mathf.Lerp(0.4f, 1f, stance);

        float forwardFric = forwardFriction * edgeFactor;
        float sideFric = sideFriction * edgeFactor;

        friction += -vAlongVec * forwardFric;
        friction += -vAcrossVec * sideFric;

        skiCount++;
    }

    // ----------------------------------------------------------------------
    // GROUND PHYSICS (DOWNHILL, FRICTION, CARVE STEERING)
    // ----------------------------------------------------------------------

    private void ApplyGroundForces()
    {
        Vector3 velocity = _rb.linearVelocity;

        // ------------------------------------------------------------------
        // Normal-axis velocity management (ground stickiness)
        //
        // 1) Kill any velocity INTO the ground so we don't burrow.
        // 2) Gently damp SMALL upward velocity along the ground normal so we
        //    stay "glued" to the surface when riding over small bumps/crests.
        //    Larger upward impulses (e.g. explicit jumps) are left alone.
        // ------------------------------------------------------------------
        Vector3 velAlongNormal = Vector3.Project(velocity, _groundNormal);
        float normalDot = Vector3.Dot(velAlongNormal, _groundNormal);

        if (normalDot < 0f)
        {
            // Into the ground: kill aggressively.
            velocity -= velAlongNormal * Mathf.Clamp01(normalKillStrength * Time.fixedDeltaTime);
        }
        else if (normalDot > 0f && maxStickUpwardSpeed > 0f)
        {
            // Away from the ground: if this is a small upward speed (typical of
            // riding over ripples or gentle crests rather than an explicit jump),
            // damp it so we don't get lots of tiny unintended hops.
            float upSpeed = normalDot; // since _groundNormal is unit-length

            if (upSpeed <= maxStickUpwardSpeed)
            {
                // Strongest damping at very small upSpeed, fading out as we
                // approach the stick threshold.
                float t = upSpeed / maxStickUpwardSpeed;        // 0..1
                float strength = normalKillStrength * (1f - t); // high at low speed

                velocity -= velAlongNormal * Mathf.Clamp01(strength * Time.fixedDeltaTime);
            }
        }

        // Commit any normal-axis changes back to the rigidbody before we compute planar motion.
        _rb.linearVelocity = velocity;

        // Planar velocity on slope
        Vector3 velOnPlane = Vector3.ProjectOnPlane(velocity, _groundNormal);

        // Combined ski-forward defines our primary "rail" direction.
        _skiForward = GetCombinedSkiForwardOnPlane();

        // --- Downhill acceleration driven by ski direction ---
        //
        // We treat the skis as rails on the slope:
        // - Project gravity onto the slope to get the fall line.
        // - Measure alignment between ski direction and fall line.
        // - Only when reasonably aligned do we let gravity pull us downhill,
        //   and even then we accelerate ALONG the skis (not pure fall line).
        Vector3 fallLine = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal);
        float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
        bool onSlope = fallLine.sqrMagnitude > 0.0001f && slopeAngle >= minSlopeAngleForDownhill;

        if (onSlope && _skiForward.sqrMagnitude > 0.0001f)
        {
            Vector3 downhillDir = fallLine.normalized;
            Vector3 skiDir = _skiForward.normalized;

            // How much are we pointing along the fall line? (-1..1)
            float alignment = Vector3.Dot(skiDir, downhillDir);
            float alignAbs = Mathf.Abs(alignment); // facing downhill OR uphill both count

            // If we are essentially traversing across the slope (alignment ~ 0),
            // don't let gravity drag us downhill. You'll just coast and friction
            // will bleed speed until you stop, like a strong edge hold.
            if (alignAbs > minAlignmentForDownhill)
            {
                // --------------------------------------------------------------
                // 1. Baseline downhill pull from gravity along the fall line.
                //    This is always downhill; lean never reverses it.
                // --------------------------------------------------------------
                float alignFactor = Mathf.InverseLerp(minAlignmentForDownhill, 1f, alignAbs);
                Vector3 baselineAccel = downhillDir * (downhillAccelMin * alignFactor);
                _rb.AddForce(baselineAccel, ForceMode.Acceleration);

                // --------------------------------------------------------------
                // 2. Lean-based drive & brake
                //
                // Lean direction on the slope:
                // - If it points toward DESCENDING slope => add downhill accel.
                // - If it points toward ASCENDING slope => apply a SOFT brake,
                //   opposite current planar velocity.
                //
                // Lean never pushes you uphill. It only adds extra downhill
                // or shaves speed off your current motion.
                // --------------------------------------------------------------
                float leanAbs = Mathf.Clamp01(Mathf.Abs(_forwardLean));
                if (leanAbs > 0.001f)
                {
                    // Facing direction on the slope.
                    Vector3 facingOnPlane = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
                    if (facingOnPlane.sqrMagnitude > 0.0001f)
                        facingOnPlane.Normalize();
                    else
                        facingOnPlane = skiDir;

                    // World-space lean direction: forward lean = facing direction,
                    // backward lean = opposite of facing direction.
                    Vector3 leanDirOnPlane = facingOnPlane * Mathf.Sign(_forwardLean);
                    Vector3 leanDir = Vector3.ProjectOnPlane(leanDirOnPlane, _groundNormal);
                    if (leanDir.sqrMagnitude > 0.0001f)
                        leanDir.Normalize();
                    else
                        leanDir = downhillDir;

                    // > 0  : leaning toward descending slope
                    // < 0  : leaning toward ascending slope
                    float leanSlopeDot = Vector3.Dot(leanDir, downhillDir);

                    // Extra accel budget we can use for either driving or braking.
                    float extraAccelMag = Mathf.Lerp(
                        0f,
                        downhillAccelMax - downhillAccelMin,
                        leanAbs
                    ) * alignFactor;

                    if (leanSlopeDot > 0.001f)
                    {
                        // Leaning toward the descending slope: gain momentum.
                        float scale = leanSlopeDot;
                        Vector3 drive = downhillDir * (extraAccelMag * scale);
                        _rb.AddForce(drive, ForceMode.Acceleration);
                    }
                    else if (leanSlopeDot < -0.001f && velOnPlane.sqrMagnitude > 0.0001f)
                    {
                        // Leaning toward the ascending slope: SOFT brake.
                        // We brake opposite current planar velocity, scaled:
                        //  - by how "uphill" the lean is
                        //  - by current speed (less brake at low speeds)
                        //  - by a softness factor so it moderates rather than stops.
                        float scale = -leanSlopeDot;

                        Vector3 velDir = velOnPlane.normalized;
                        float planarSpeed = velOnPlane.magnitude;

                        // 0 at standstill, ~1 around typical cruise speed.
                        float speedFactor = Mathf.InverseLerp(0f, skateMaxEffectiveSpeed, planarSpeed);

                        const float brakeSoftness = 0.5f; // < 1 => softer than full drive

                        float brakeAccelMag = extraAccelMag * scale * speedFactor * brakeSoftness;
                        Vector3 brake = -velDir * brakeAccelMag;
                        _rb.AddForce(brake, ForceMode.Acceleration);
                    }
                }
            }
        }

        // --- Per-ski anisotropic friction ---
        Vector3 friction = Vector3.zero;
        int skiCount = 0;

        AccumulateSkiFriction(leftSkiContact, velOnPlane, ref friction, ref skiCount);
        AccumulateSkiFriction(rightSkiContact, velOnPlane, ref friction, ref skiCount);

        if (skiCount > 0)
        {
            friction /= skiCount;
            _rb.AddForce(friction, ForceMode.Acceleration);
        }

        // --- Carve steering: rotate velocity toward ski forward instead of killing side speed ---
        Vector3 newVel = _rb.linearVelocity;
        Vector3 planeVel = Vector3.ProjectOnPlane(newVel, _groundNormal);
        float planeSpeed = planeVel.magnitude;

        if (planeSpeed > 0.1f)
        {
            Vector3 planeDir = planeVel.normalized;
            Vector3 skiDir = _skiForward;

            // Parallel factor between skis (0 = wedge, 1 = parallel).
            float parallel = 1f;
            if (leftSkiContact != null && rightSkiContact != null)
            {
                Vector3 fl = leftSkiContact.GetForwardOnPlane(_groundNormal);
                Vector3 fr = rightSkiContact.GetForwardOnPlane(_groundNormal);
                if (fl.sqrMagnitude > 0.0001f && fr.sqrMagnitude > 0.0001f)
                {
                    parallel = Mathf.Clamp01((Vector3.Dot(fl, fr) + 1f) * 0.5f);
                }
            }

            float edge = Mathf.Clamp01((_leftOut + _rightOut) * 0.5f);
            float carveFactor = parallel * edge;

            if (carveFactor > 0f && skiDir.sqrMagnitude > 0.0001f)
            {
                float steerAmount = carveFactor * carveSteerStrength * Time.fixedDeltaTime;
                Vector3 steeredDir = Vector3.Slerp(planeDir, skiDir.normalized, steerAmount);
                steeredDir.Normalize();

                newVel = steeredDir * planeSpeed + Vector3.Project(newVel, _groundNormal);
                _rb.linearVelocity = newVel;
            }
        }
    }

    private void TryConsumeQueuedJump()
    {
        if (!_jumpQueued)
            return;

        // If the button is no longer held AND the buffer window has elapsed,
        // discard the queued jump.
        if (!_jumpHeld && (Time.time - _lastJumpPressedTime > jumpBufferTime))
        {
            _jumpQueued = false;
            return;
        }

        if (_stacked)
            return;

        // Allow jump when grounded, or shortly after leaving the ground (coyote time).
        bool withinCoyote = !_isGrounded && (Time.time - _lastGroundedTime <= jumpCoyoteTime);
        if (!_isGrounded && !withinCoyote)
        {
            // Still buffered, but not in a valid state to jump yet.
            return;
        }

        // ------------------------------------------------------------------
        // Jump force: small hops at low speed, full pop at higher speeds.
        // ------------------------------------------------------------------
        float minForce = Mathf.Min(jumpForceRange.x, jumpForceRange.y);
        float maxForce = Mathf.Max(jumpForceRange.x, jumpForceRange.y);

        // Use planar speed on the current ground plane to determine how much
        // of the full jump force we should apply.
        Vector3 velocity = _rb.linearVelocity;
        Vector3 velOnPlane = Vector3.ProjectOnPlane(velocity, _groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        // Speed at which we reach full jump force.
        const float speedForMaxJump = 8f;

        float speedT = speedForMaxJump > 0f
            ? Mathf.Clamp01(planarSpeed / speedForMaxJump)
            : 1f;

        // Stationary / slow: closer to minForce (small hop).
        // Fast: closer to maxForce (big pop).
        float force = Mathf.Lerp(minForce, maxForce, speedT);

        Vector3 jumpDir = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        _rb.AddForce(jumpDir * force, ForceMode.VelocityChange);

        _isGrounded = false;
        _jumpQueued = false;
    }

    // ----------------------------------------------------------------------
    // SKATE / WADDLE IMPULSES
    // ----------------------------------------------------------------------

    private void DetectAndApplySkatePushes()
    {
        if (!_isGrounded || _stacked || !HasLegInputs)
        {
            _lastSkateInput = GetCurrentSkateInput();
            return;
        }

        float now = Time.time;
        if (now - _lastPushTime < skateCooldown)
        {
            _lastSkateInput = GetCurrentSkateInput();
            return;
        }

        if (_forwardLean < minForwardLeanForPush)
        {
            _lastSkateInput = GetCurrentSkateInput();
            return;
        }

        float current = GetCurrentSkateInput();
        float prev = _lastSkateInput;

        bool strongNow = Mathf.Abs(current) > 0.5f;
        bool nearNeutralBefore = Mathf.Abs(prev) <= 0.2f;
        bool signFlip = current * prev < 0f && Mathf.Abs(prev) > 0.2f;

        bool shouldPush = strongNow && (nearNeutralBefore || signFlip);

        _lastSkateInput = current;

        if (!shouldPush)
            return;

        float speed = _rb.linearVelocity.magnitude;
        float speedFactor = 1f;
        if (speed > skateMaxEffectiveSpeed)
        {
            speedFactor = skateMaxEffectiveSpeed / Mathf.Max(speed, 0.0001f);
        }
        if (speedFactor <= 0f)
            return;

        _lastPushTime = now;

        Vector3 pushDir = GetCombinedSkiForwardOnPlane();
        if (pushDir.sqrMagnitude < 0.0001f)
        {
            pushDir = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
            if (pushDir.sqrMagnitude < 0.0001f)
                pushDir = Vector3.ProjectOnPlane(Vector3.forward, _groundNormal);
        }
        pushDir.Normalize();

        float leanT = Mathf.Clamp01((_forwardLean + 1f) * 0.5f);
        float impulse = skateImpulse * leanT * speedFactor;

        //// If a pole stroke is in its entry phase as we push, treat this as a combined stride.
        //if (_polePhase == PoleStrokePhase.Entry)
        //{
        //    impulse *= 1.15f; // mild boost so it feels like legs + poles working together
        //}

        _rb.AddForce(pushDir * impulse, ForceMode.VelocityChange);

        // Small waddle yaw, but only if we're moving at least a bit.
        float planeSpeed = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal).magnitude;
        if (planeSpeed > minSpeedForSkateYaw && Mathf.Abs(skateYawPerPush) > 0.01f)
        {
            int pushSign = current < 0f ? -1 : +1;
            float yawDelta = skateYawPerPush * pushSign;
            transform.Rotate(0f, yawDelta, 0f, Space.World);
        }
    }

    private float GetCurrentSkateInput()
    {
        if (!HasLegInputs) return 0f;
        return Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f);
    }

    // ----------------------------------------------------------------------
    // POLE FORCES
    // ----------------------------------------------------------------------
    void ApplyPoleForces()
    {
        // Only apply pole forces when the rider is grounded and poles are available.
        if (!_isGrounded || !HasPolesInput)
            return;

        // No forces from idle.
        if (_polePhase == PoleStrokePhase.Idle)
            return;

        Vector3 vel = _rb.linearVelocity;
        Vector3 velPlane = Vector3.ProjectOnPlane(vel, _groundNormal);
        float speed = velPlane.magnitude;

        // Combined ski forward on the plane (our "intended" travel direction).
        Vector3 skiDir = GetCombinedSkiForwardOnPlane();
        if (skiDir.sqrMagnitude < 0.0001f)
        {
            skiDir = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
            if (skiDir.sqrMagnitude < 0.0001f)
                return;
        }
        skiDir.Normalize();

        // Downhill direction on the surface.
        Vector3 downhillDir = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
        if (downhillDir.sqrMagnitude > 0.0001f)
            downhillDir.Normalize();
        else
            downhillDir = skiDir;

        // Is any pole actually in contact with the snow?
        bool anyPoleContact = false;
        if (leftPoleContact != null && leftPoleContact.IsInContact)
            anyPoleContact = true;
        if (!anyPoleContact && rightPoleContact != null && rightPoleContact.IsInContact)
            anyPoleContact = true;

        // How much our skis point down the hill (1) vs uphill (-1).
        float downhillDot = Vector3.Dot(skiDir, downhillDir);

        // Use this to tune strength of push/brake by slope angle.
        float downhillFactor = Mathf.InverseLerp(-0.3f, 0.7f, downhillDot);

        // Poles are less effective at very high speeds.
        float speedFactor = 1f;
        if (poleMaxSpeed > 0.01f)
        {
            float tSpeed = Mathf.Clamp01(speed / poleMaxSpeed);

            // Curved falloff: 1 at low speeds, then easing down as we approach max.
            float falloff = 1f - tSpeed * tSpeed; // quadratic falloff

            // Blend between full effectiveness (1) and a minimum factor at high speed.
            speedFactor = Mathf.Lerp(minPoleSpeedFactor, 1f, falloff);
        }

        if (speedFactor <= 0f)
            return;

        // Lean: -1 back, +1 forward -> clamp to 0..1 for pole use.
        float leanT = Mathf.Clamp01((_forwardLean + 1f) * 0.5f);

        float phaseT = Mathf.Clamp01(_poleStrokeT);

        switch (_polePhase)
        {
            // ENTRY: pressing poles forward/down before they fully bite.
            case PoleStrokePhase.Entry:
                {
                    // A small assist that grows through the entry stroke.
                    float entryStrength = phaseT;
                    float accel =
                        poleImpulse
                        * 0.5f
                        * entryStrength
                        * (0.4f + 0.6f * leanT)
                        * downhillFactor
                        * speedFactor;

                    _rb.AddForce(skiDir * accel, ForceMode.Acceleration);
                    break;
                }

            // DRAG: poles are dug in; main braking effect, modulated by slope + speed.
            case PoleStrokePhase.Drag:
                {
                    if (!anyPoleContact)
                        break;

                    Vector3 vAlong = Vector3.Project(velPlane, skiDir);
                    if (vAlong.sqrMagnitude > 0.0001f)
                    {
                        Vector3 brakeDir = -vAlong.normalized;

                        // More drag when travelling fast, and when aiming more uphill.
                        float uphillBias = Mathf.Clamp01(-downhillDot); // 0 downhill, 1 fully uphill

                        float brake =
                            poleBrakeStrength
                            * (0.3f + 0.7f * uphillBias)
                            * (0.3f + 0.7f * leanT)
                            * (0.4f + 0.6f * Mathf.Clamp01(speed / poleMaxSpeed));

                        _rb.AddForce(brakeDir * brake, ForceMode.Acceleration);
                    }

                    break;
                }

            // FOLLOW-THROUGH: release input while poles are still biting – forward shove.
            case PoleStrokePhase.FollowThrough:
                {
                    // Normally we want planted-pole behaviour (require contact),
                    // but from low speeds we'll allow a small "kick" even if the
                    // raycast misses, so you can get moving.
                    bool useContact = anyPoleContact;

                    if (!useContact && speed < 2f)
                    {
                        useContact = true;
                        // At low speed with no clear downhill, bias slightly forward.
                        downhillFactor = Mathf.Max(downhillFactor, 0.5f);
                    }

                    if (!useContact)
                        break;

                    // Strongest push at the start of follow-through, fades toward the end.
                    float pushProfile = 1f - phaseT;

                    float impulse =
                        poleImpulse
                        * pushProfile
                        * (0.4f + 0.6f * leanT)
                        * downhillFactor
                        * speedFactor;

                    Vector3 push = skiDir * Mathf.Max(0f, impulse);

                    // From low speeds, treat this as a direct velocity kick
                    // so you clearly feel the pole helping you get moving.
                    if (speed < 2f)
                    {
                        _rb.AddForce(push, ForceMode.VelocityChange);
                    }
                    else
                    {
                        _rb.AddForce(push, ForceMode.Acceleration);
                    }

                    break;
                }
        }
    }

    // ----------------------------------------------------------------------
    // ORIENTATION (SKIS DRIVE FORWARD)
    // ----------------------------------------------------------------------

    private void AlignToSkisAndSlope()
    {
        // We always face along the combined ski direction projected onto the slope.
        // The slope and current velocity DO NOT auto-correct your heading; they
        // only influence speed via gravity and friction so your carve feels earned.
        Vector3 skiDir = _skiForward;
        if (skiDir.sqrMagnitude < 0.0001f)
        {
            skiDir = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
            if (skiDir.sqrMagnitude < 0.0001f)
            {
                skiDir = Vector3.ProjectOnPlane(Vector3.forward, _groundNormal);
            }
        }
        skiDir.Normalize();

        Vector3 desiredForward = skiDir;
        if (desiredForward.sqrMagnitude < 0.0001f)
        {
            desiredForward = transform.forward;
        }

        Quaternion targetRot = Quaternion.LookRotation(desiredForward, _groundNormal);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                              groundTurnSpeed * Time.fixedDeltaTime);
    }

    // ----------------------------------------------------------------------
    // AIR CONTROL
    // ----------------------------------------------------------------------
    private void ApplyAirControl()
    {
        // Leg difference controls yaw (spin), lean controls pitch (flip).
        float yawInput = HasLegInputs
            ? Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f)
            : 0f;

        // Invert lean so forward lean pitches you slightly back (feels more natural for flips).
        float pitchInput = _rawLeanInput;

        float dt = Time.fixedDeltaTime;

        // Tuck (poles held) increases spin responsiveness.
        float spinMultiplier = 1f;
        if (HasPolesInput && _rawPolesPressed)
        {
            spinMultiplier *= airTuckSpinMultiplier;
        }

        // Desired angular velocities based on input.
        float targetYawSpeed = yawInput * airYawTurnSpeed * spinMultiplier;
        float targetPitchSpeed = pitchInput * airPitchTurnSpeed * spinMultiplier;

        float accel = Mathf.Max(0f, airAngularAcceleration);
        float damping = Mathf.Max(0f, airAngularDamping);

        // Accelerate current air angular velocity toward target values.
        _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, targetYawSpeed, accel * dt);
        _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, targetPitchSpeed, accel * dt);

        // Apply damping toward zero when there is little/no input so we don't spin forever.
        if (Mathf.Abs(yawInput) < 0.01f)
        {
            _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, 0f, damping * dt);
        }
        if (Mathf.Abs(pitchInput) < 0.01f)
        {
            _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, 0f, damping * dt);
        }

        // Apply rotation based on current angular velocity.
        Quaternion yawRot = Quaternion.AngleAxis(_airAngularVelocity.y * dt, Vector3.up);
        Quaternion pitchRot = Quaternion.AngleAxis(_airAngularVelocity.x * dt, transform.right);

        transform.rotation = yawRot * pitchRot * transform.rotation;
    }
}


