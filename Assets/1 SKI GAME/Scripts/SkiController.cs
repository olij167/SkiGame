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

    /// <summary>
    /// High level locomotion state. This is mainly for reasoning about
    /// behaviour (ground vs air vs landing) and for gating certain effects
    /// like ground stickiness.
    /// </summary>
    private enum MovementMode
    {
        Skiing,
        JumpRising,
        Airborne,
        Landing,
        Stacked
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

    // True if either ski collider is currently reporting ground contact.
    private bool HasAnySkiContact =>
        (leftSkiContact != null && leftSkiContact.IsGrounded) ||
        (rightSkiContact != null && rightSkiContact.IsGrounded);

    // Grounding/orientation
    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _wasControlsGrounded; // previous frame's grounded-for-controls flag
    private Vector3 _groundNormal = Vector3.up;
    private Vector3 _skiForward = Vector3.forward; // combined ski forward on plane

    // High-level locomotion state (for debugging and behaviour gating).
    private MovementMode _movementMode = MovementMode.Airborne;

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

    // Accumulated time spent riding mostly on ski tips while grounded.
    private float _tipContactAccumTime;

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
    private bool _jumpReleaseQueued;
    private float _lastJumpPressedTime;
    private float _lastGroundedTime;
    private float _lastJumpTime;
    private float _airborneStartTime;

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

    [Tooltip("Vertical gap from the cast origin to count as 'in contact' with the ground.")]
    [SerializeField] private float groundContactDistance = 0.25f;
    
    [Tooltip("Maximum slope angle (deg) considered 'rideable ground' for grounding & landing logic. Steeper surfaces are treated as walls, not ground.")]
    [SerializeField, Range(0f, 90f)]
    private float maxGroundSlopeAngle = 80f;

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

    [Tooltip("lower = more edge hold, higher = more sliding, even when misaligned with the slope")]
    [SerializeField] private float minSlideFactor = 0.3f; // tweakable: 
    
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

    // Tip-contact stability tuning (kept as code constants to avoid more inspector clutter).
    // If you want to expose these in the inspector later, just turn them into [SerializeField] fields.
    private const float TipContactStackFraction = 0.7f;  // fraction of grounded skis whose last contact is in the tip region
    private const float TipContactMinSpeed = 2.5f;    // planar m/s before tip checks matter
    private const float TipContactStackTime = 0.18f;   // seconds of tip-heavy contact before we stack

    [Header("Landing / Stack")]
    [Tooltip("Max allowed tilt angle (deg) between skier up and ground normal to count as a safe landing.")]
    [SerializeField] private float maxLandingTiltAngle = 50f;

    [Tooltip("Max allowed misalignment (deg) between combined ski direction and planar velocity at landing.")]
    [SerializeField] private float maxLandingMisalignmentAngle = 75f;

    [Tooltip("Minimum planar speed where misalignment is considered for stacking.")]
    [SerializeField] private float minLandingSpeedForStackCheck = 5f;

    [Tooltip("Air time below this (seconds) is treated as a 'micro' landing for landing/stack logic.")]
    [SerializeField] private float minLandingAirTime = 0.12f;

    [Tooltip("Downward speed below this (m/s) is treated as a 'micro' landing for landing/stack logic.")]
    [SerializeField] private float minLandingDownwardSpeed = 2f;

    [Tooltip("Nose /tip dig thresholds in degrees.")]
    [SerializeField] private float heavyTipStackAngle = 25f;    // big nose-dig on a real landing → stack

    [Header("Landing Projection")]
    [Tooltip("Base strength of velocity projection onto slope at landing (0 = none, 1 = full).")]
    [Range(0f, 1f)]
    [SerializeField] private float landingProjectionStrength = 0.6f;

    [Tooltip("Fraction of planar speed to keep when projecting onto slope.")]
    [Range(0f, 1f)]
    [SerializeField] private float landingVelocityRetention = 0.9f;

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
    [Tooltip("Min/Max velocity change applied along the ground normal when jumping.")]
    [SerializeField] private Vector2 jumpForceRange = new Vector2(3f, 8f);

    [Tooltip("Time window after leaving the ground during which a jump press will still be accepted (seconds).")]
    [SerializeField] private float jumpCoyoteTime = 0.15f;

    [Tooltip("Time window a jump press is buffered so it can fire on the next valid ground contact (seconds).")]
    [SerializeField] private float jumpBufferTime = 0.10f;

    [Tooltip("How long holding jump builds up to full charge (seconds).")]
    [SerializeField] private float maxJumpChargeTime = 0.5f;

    [Tooltip("Time after a jump during which ground 'stickiness' will not damp upward motion (seconds).")]
    [SerializeField] private float jumpStickSuppressionTime = 0.2f;

    [Tooltip("Minimum time after performing a jump before ground checks will consider the rider grounded again.\n" +
             "Prevents tiny hops from instantly re-sticking on steep slopes.")]
    [SerializeField] private float minJumpUngroundedTime = 0.12f;

    [Tooltip("Multiplier applied to jump force when at or above the speed below. " +
         "1 = no extra boost, 1.5 = 50% stronger jumps at high speed.")]
    [SerializeField] private float jumpSpeedForceMultiplier = 1.5f;

    [Tooltip("Planar speed (m/s) at which the jump speed multiplier reaches its maximum effect.")]
    [SerializeField] private float jumpSpeedForMaxMultiplier = 12f;

    [Header("Debug")]
    [SerializeField] private bool showDebugHUD = true;

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

    /// <summary>
    /// Grounded test used for *controls* (when to use skiing vs air inputs).
    /// This is intentionally more forgiving than the strict physics grounding:
    /// - Any ski collider contact counts as grounded.
    /// - A short coyote window after losing ground keeps you in ski controls
    ///   so tiny gaps / tip chatter don't instantly flip you into air mode.
    /// </summary>
    private bool IsGroundedForControls
    {
        get
        {
            if (_stacked)
                return false;

            // Hard grounding: any ski collider currently touching snow.
            if (HasAnySkiContact)
                return true;

            // Physics-based grounding from our casts.
            if (_isGrounded)
                return true;

            // Soft "control coyote": keep ski controls briefly after leaving ground.
            const float controlCoyoteTime = 0.08f;
            return (Time.time - _lastGroundedTime) <= controlCoyoteTime;
        }
    }

    // Expose pole contacts for audio / VFX.
    public PoleContact LeftPoleContact => leftPoleContact;
    public PoleContact RightPoleContact => rightPoleContact;


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
        bool wasControlsGrounded = _wasControlsGrounded;

        CheckGround();
        UpdateTipContactStability();

        // Track when we leave the ground for jump/landing severity.
        if (_wasGrounded && !_isGrounded)
        {
            _airborneStartTime = Time.time;
        }

        if (_stacked)
        {
            TryAutoRecoverFromStack();
            if (_stacked)
            {
                _movementMode = MovementMode.Stacked;
                return;
            }
        }

        TryConsumeQueuedJump();

        // Decide what mode we're in this frame (Skiing, Airborne, Landing, etc),
        // using the more forgiving grounded-for-controls test.
        UpdateMovementMode(wasControlsGrounded);

        // Ground modes: normal skiing and the first landing frame share the
        // same core force application. The *difference* is how we treat
        // stickiness inside ApplyGroundForces (see below).
        if (_movementMode == MovementMode.Skiing ||
            _movementMode == MovementMode.Landing)
        {
            ApplyGroundForces();
            DetectAndApplySkatePushes();
            ApplyPoleForces();
            AlignToSkisAndSlope();
        }
        else
        {
            // JumpRising / Airborne
            ApplyAirControl();
        }

        // Remember control grounding state for next frame's transition detection.
        _wasControlsGrounded = IsGroundedForControls;
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
            _jumpReleaseQueued = false;
            return;
        }

        var action = jumpAction.action;
        bool pressedThisFrame = action.WasPressedThisFrame();
        bool releasedThisFrame = action.WasReleasedThisFrame();
        bool isPressed = action.IsPressed();

        if (pressedThisFrame)
        {
            // Start a new charge window.
            _jumpHeld = true;
            _lastJumpPressedTime = Time.time;
            _jumpQueued = true;
            _jumpReleaseQueued = false;
        }
        else if (releasedThisFrame)
        {
            // We only care about a release if we still have a queued jump.
            _jumpHeld = false;

            if (_jumpQueued)
            {
                // Mark that we're ready to actually perform the jump
                // as soon as we hit a valid jump state (ground / coyote).
                _jumpReleaseQueued = true;
            }
        }

        // Drop the queued jump if it's too old and the player isn't actively
        // holding it OR waiting on a release to fire.
        if (_jumpQueued &&
            !_jumpHeld &&
            !_jumpReleaseQueued &&
            (Time.time - _lastJumpPressedTime > jumpBufferTime))
        {
            _jumpQueued = false;
        }
    }

    /// <summary>
    /// Updates the high-level locomotion mode based on grounded state,
    /// jump timing, and stacking.
    /// </summary>
    /// <summary>
    /// Updates the high-level locomotion mode based on grounded state,
    /// jump timing, and stacking. Uses a more forgiving grounded-for-controls
    /// test so we don't flicker into air controls while still in ski contact.
    /// </summary>
    private void UpdateMovementMode(bool wasControlsGrounded)
    {
        if (_stacked)
        {
            _movementMode = MovementMode.Stacked;
            return;
        }

        bool groundedForControls = IsGroundedForControls;

        if (groundedForControls)
        {
            // First frame we regain ground for controls => treat as a landing frame
            // so we stay in ski controls and get a clean transition out of air.
            if (!wasControlsGrounded)
            {
                _movementMode = MovementMode.Landing;
            }
            else
            {
                _movementMode = MovementMode.Skiing;
            }

            return;
        }

        // Controls consider us airborne. Use *physics* state + jump timing to
        // choose between jump-rising and general airborne.
        bool recentlyJumped = (Time.time - _lastJumpTime) <= jumpStickSuppressionTime;
        bool movingUp = Vector3.Dot(_rb.linearVelocity, Vector3.up) > 0.01f;

        if (recentlyJumped && movingUp)
        {
            _movementMode = MovementMode.JumpRising;
        }
        else
        {
            _movementMode = MovementMode.Airborne;
        }
    }

    // ----------------------------------------------------------------------
    // GROUNDING & LANDING
    // ----------------------------------------------------------------------

    private void CheckGround()
    {
        bool haveSkiContact = HasAnySkiContact;

        // If we've just jumped, enforce a short ungrounded window so the jump
        // can actually leave the surface instead of instantly re-sticking on
        // steep slopes. BUT: if a ski is physically colliding (tips or base),
        // we trust that and allow a landing.
        if (Time.time - _lastJumpTime < minJumpUngroundedTime && !haveSkiContact)
        {
            _isGrounded = false;
            _leftGrounded = false;
            _rightGrounded = false;
            _groundNormal = Vector3.up;
            return;
        }

        bool nearGround = false;
        Vector3 sumNormals = Vector3.zero;

        _leftGrounded = false;
        _rightGrounded = false;

        Vector3 centerOrigin = transform.position + Vector3.up * groundCheckHeight;
        float maxDist = groundCheckHeight + groundCheckDistance;

        // Helper: only treat a normal as "ground" if it's not too steep.
        bool IsRideable(Vector3 n)
        {
            if (n.sqrMagnitude < 0.0001f) return false;
            float slopeAngle = Vector3.Angle(n.normalized, Vector3.up);
            return slopeAngle <= maxGroundSlopeAngle;
        }

        // ------------------------------------------------------------------
        // 1) Body sphere cast
        // ------------------------------------------------------------------
        if (Physics.SphereCast(centerOrigin, groundCheckRadius, Vector3.down,
                               out RaycastHit centerHit, maxDist, groundLayers,
                               QueryTriggerInteraction.Ignore))
        {
            float contactGap = Mathf.Max(0f, centerHit.distance - groundCheckHeight);
            if (contactGap <= groundContactDistance && IsRideable(centerHit.normal))
            {
                nearGround = true;
                sumNormals += centerHit.normal;
            }
        }

        // ------------------------------------------------------------------
        // 2) Downward rays from each ski root
        // ------------------------------------------------------------------

        // Left ski ray
        if (leftSki != null)
        {
            Vector3 leftOrigin = leftSki.position + Vector3.up * groundCheckHeight;
            if (Physics.Raycast(leftOrigin, Vector3.down, out RaycastHit leftHit, maxDist,
                                groundLayers, QueryTriggerInteraction.Ignore))
            {
                float contactGap = Mathf.Max(0f, leftHit.distance - groundCheckHeight);
                if (contactGap <= groundContactDistance && IsRideable(leftHit.normal))
                {
                    nearGround = true;
                    sumNormals += leftHit.normal;
                    _leftGrounded = true;
                    _leftHit = leftHit;
                }
            }
        }

        // Right ski ray
        if (rightSki != null)
        {
            Vector3 rightOrigin = rightSki.position + Vector3.up * groundCheckHeight;
            if (Physics.Raycast(rightOrigin, Vector3.down, out RaycastHit rightHit, maxDist,
                                groundLayers, QueryTriggerInteraction.Ignore))
            {
                float contactGap = Mathf.Max(0f, rightHit.distance - groundCheckHeight);
                if (contactGap <= groundContactDistance && IsRideable(rightHit.normal))
                {
                    nearGround = true;
                    sumNormals += rightHit.normal;
                    _rightGrounded = true;
                    _rightHit = rightHit;
                }
            }
        }

        // ------------------------------------------------------------------
        // 3) Integrate SkiContact collisions (primary source for skis)
        // ------------------------------------------------------------------
        if (leftSkiContact != null && leftSkiContact.IsGrounded && IsRideable(leftSkiContact.ContactNormal))
        {
            nearGround = true;
            sumNormals += leftSkiContact.ContactNormal;
            _leftGrounded = true;

            _leftHit.point = leftSkiContact.ContactPoint;
            _leftHit.normal = leftSkiContact.ContactNormal;
        }

        if (rightSkiContact != null && rightSkiContact.IsGrounded && IsRideable(rightSkiContact.ContactNormal))
        {
            nearGround = true;
            sumNormals += rightSkiContact.ContactNormal;
            _rightGrounded = true;

            _rightHit.point = rightSkiContact.ContactPoint;
            _rightHit.normal = rightSkiContact.ContactNormal;
        }

        bool wasGroundedBefore = _isGrounded;

        // ------------------------------------------------------------------
        // 4) Final grounded state + smoothed ground normal
        // ------------------------------------------------------------------
        if (nearGround && sumNormals.sqrMagnitude > 0.0001f)
        {
            _isGrounded = true;

            Vector3 rawNormal = sumNormals.normalized;
            _lastGroundedTime = Time.time;

            const float GroundNormalSmoothSpeed = 18f;

            if (!wasGroundedBefore || _groundNormal.sqrMagnitude < 0.0001f)
            {
                _groundNormal = rawNormal;
            }
            else
            {
                float lerp = 1f - Mathf.Exp(-GroundNormalSmoothSpeed * Time.fixedDeltaTime);
                _groundNormal = Vector3.Slerp(_groundNormal, rawNormal, lerp);
            }

            if (!wasGroundedBefore)
            {
                // Only treat this as a "real landing" if we've actually been
                // in the air for a bit. Tiny chatter -> just stick to slope.
                float airTime = Time.time - _airborneStartTime;
                if (airTime > minLandingAirTime * 0.5f)
                {
                    _airAngularVelocity = Vector3.zero;
                    EvaluateLanding();
                }
                else
                {
                    _movementMode = MovementMode.Skiing;
                }
            }
        }
        else
        {
            _isGrounded = false;
            _groundNormal = Vector3.up;
        }

        // Safety: if any ski is reporting contact but our casts decided we're not grounded,
        // snap to grounded using the ski normals so controls / landing logic don't go airborne.
        if (!_isGrounded && HasAnySkiContact)
        {
            _isGrounded = true;

            Vector3 n = Vector3.zero;
            if (leftSkiContact != null && leftSkiContact.IsGrounded && IsRideable(leftSkiContact.ContactNormal))
                n += leftSkiContact.ContactNormal;
            if (rightSkiContact != null && rightSkiContact.IsGrounded && IsRideable(rightSkiContact.ContactNormal))
                n += rightSkiContact.ContactNormal;

            if (n.sqrMagnitude > 0.0001f)
                _groundNormal = n.normalized;

            _lastGroundedTime = Time.time;
        }
    }

    /// <summary>
    /// Returns the largest "nose-dig" angle (deg) among the skis, based on how much
    /// their forward vectors are pointing into the ground *and* are contacting near
    /// the tip region.
    /// </summary>
    private float ComputeTipDigAngle()
    {
        if (_groundNormal.sqrMagnitude < 0.0001f)
            return 0f;

        Vector3 groundNormal = _groundNormal.normalized;
        float maxDig = 0f;

        maxDig = Mathf.Max(
            maxDig,
            ComputeTipDigAngleForSki(leftSki, leftSkiContact, groundNormal));

        maxDig = Mathf.Max(
            maxDig,
            ComputeTipDigAngleForSki(rightSki, rightSkiContact, groundNormal));

        return maxDig;
    }

    /// <summary>
    /// Computes the nose-dig angle for a single ski, or 0 if it's not a tip-heavy contact.
    /// </summary>
    private float ComputeTipDigAngleForSki(
        Transform ski,
        SkiContact contact,
        Vector3 groundNormal)
    {
        // Use SkiContact as the primary source of truth for tip digs.
        // If we don't have an actual tip contact on this ski, treat it as no nose-dig.
        if (contact == null || !contact.IsGrounded || !contact.HasTipContact)
            return 0f;

        // Fallback: if the ski transform isn't provided for some reason,
        // use the contact's transform.
        if (ski == null)
            ski = contact.transform;

        if (groundNormal.sqrMagnitude < 0.0001f)
            return 0f;

        Vector3 f = ski.forward;

        // Only consider when the ski is pointing at least slightly into the surface.
        float intoGround = Mathf.Max(0f, -Vector3.Dot(f, groundNormal));
        if (intoGround <= 0f)
            return 0f;

        // Forward direction flattened onto the slope plane.
        Vector3 fOnPlane = Vector3.ProjectOnPlane(f, groundNormal);
        if (fOnPlane.sqrMagnitude < 0.0001f)
            return 0f;

        fOnPlane.Normalize();

        // Angle between actual forward and its flattened version = nose-dig amount.
        return Vector3.Angle(fOnPlane, f);
    }

    void EvaluateLanding()
    {
        float now = Time.time;
        float airTime = Mathf.Max(0f, now - _airborneStartTime);

        Vector3 vel = _rb.linearVelocity;

        // Use the current ground normal for "up vs down" and planar projections.
        Vector3 groundNormal = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        // Slope steepness in world-space (0° = flat, 90° = vertical).
        float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

        // Downward speed along the ground normal (positive when moving into the ground).
        float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(vel, groundNormal));

        // Single tip-dig measure reused for all checks.
        float tipDigAngle = ComputeTipDigAngle();

        // Planar components for tilt / sideways checks.
        Vector3 velOnPlane = Vector3.ProjectOnPlane(vel, groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        // Tilt relative to the ground.
        float tiltAngle = Vector3.Angle(transform.up, groundNormal);

        // Misalignment between where we're travelling on the slope and where we're facing.
        float misalignAngle = 0f;
        if (planarSpeed > 0.1f)
        {
            Vector3 forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            if (forwardOnPlane.sqrMagnitude > 0.0001f)
            {
                forwardOnPlane.Normalize();
                velOnPlane.Normalize();

                float rawAngle = Vector3.Angle(forwardOnPlane, velOnPlane);

                // Treat backwards landings (180°) as "aligned" with forwards (0°):
                // only sideways landings should really punish you.
                misalignAngle = Mathf.Min(rawAngle, 180f - rawAngle);
            }
        }

        // ----- 1. Micro vs non-micro landings -----

        bool isTinyLanding =
            airTime < minLandingAirTime ||
            downwardSpeed < minLandingDownwardSpeed;

        // For tiny landings, always stick and gently glue to the slope.
        if (isTinyLanding)
        {
            float microProjection = Mathf.Clamp01(landingProjectionStrength * 0.5f);
            PreserveLandingVelocityOnSlope(microProjection);
            SnapOrientationToGround();

            _movementMode = MovementMode.Skiing;
            return;
        }

        // ----- 2. Slope-aware thresholds -----

        const float SteepSlopeStart = 40f; // deg
        const float VerySteepSlope = 70f;  // deg

        float steepT = Mathf.InverseLerp(SteepSlopeStart, VerySteepSlope, slopeAngle);
        steepT = Mathf.Clamp01(steepT);

        // Tilt & sideways limits loosen as slopes get steeper.
        float tiltLimit = maxLandingTiltAngle + 15f * steepT;
        float misalignLimit = maxLandingMisalignmentAngle + 15f * steepT;

        // Projection strength is slightly softer on very steep slopes so we don't
        // over-damp velocity when dropping onto a face.
        float baseProjection = Mathf.Clamp01(landingProjectionStrength);
        float projectionOnSteep = Mathf.Lerp(baseProjection, baseProjection * 0.7f, steepT);

        // ----- 3. Very steep slopes: only hard nose-digs stack -----

        if (slopeAngle >= VerySteepSlope)
        {
            if (tipDigAngle > heavyTipStackAngle)
            {
                TriggerStack();
            }
            else
            {
                PreserveLandingVelocityOnSlope(projectionOnSteep);
                SnapOrientationToGround();
                _movementMode = MovementMode.Skiing;
            }
            return;
        }

        // ----- 4. Regular non-micro landing: stick vs stack -----

        bool tooTilted =
            tiltAngle > tiltLimit;

        bool tooMisaligned =
            planarSpeed > minLandingSpeedForStackCheck &&
            misalignAngle > misalignLimit;

        bool tooNoseDown =
            tipDigAngle > heavyTipStackAngle;

        if (tooTilted || tooMisaligned || tooNoseDown)
        {
            TriggerStack();
            return;
        }

        PreserveLandingVelocityOnSlope(projectionOnSteep);
        SnapOrientationToGround();

        _movementMode = MovementMode.Skiing;
    }

    private void PreserveLandingVelocityOnSlope(float projectionFactor)
    {
        // 0 = keep current velocity, 1 = fully project to slope target.
        projectionFactor = Mathf.Clamp01(projectionFactor);
        if (projectionFactor <= 0f)
            return;

        Vector3 vel = _rb.linearVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f)
            return;

        if (_groundNormal.sqrMagnitude < 0.0001f)
            return;

        Vector3 groundNormal = _groundNormal.normalized;
        Vector3 planar = Vector3.ProjectOnPlane(vel, groundNormal);
        float planarSpeed = planar.magnitude;

        if (planarSpeed < 0.01f)
            return;

        // Keep only a fraction of our along-slope speed.
        float retainedSpeed = planarSpeed * Mathf.Clamp01(landingVelocityRetention);
        Vector3 targetVel = planar.normalized * retainedSpeed;

        // Blend from current velocity toward the along-slope target based on
        // how severe the landing was.
        Vector3 newVel = Vector3.Lerp(vel, targetVel, projectionFactor);
        _rb.linearVelocity = newVel;
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

    /// <summary>
    /// Uses per-ski contact data to detect when we're effectively "riding on the tips"
    /// or front/back faces. If we spend too long with most contact in the tip region
    /// OR if neither ski has a reasonably flat base contact, we stack.
    /// </summary>
    private void UpdateTipContactStability()
    {
        // If we're stacked or not currently treated as grounded for controls,
        // tip stability doesn't apply.
        if (_stacked || !IsGroundedForControls)
        {
            _tipContactAccumTime = 0f;
            return;
        }

        // We need at least one SkiContact to reason about tips.
        if (leftSkiContact == null && rightSkiContact == null)
        {
            _tipContactAccumTime = 0f;
            return;
        }

        // Read per-ski contact state.
        bool leftGrounded = leftSkiContact != null && leftSkiContact.IsGrounded;
        bool rightGrounded = rightSkiContact != null && rightSkiContact.IsGrounded;

        if (!leftGrounded && !rightGrounded)
        {
            _tipContactAccumTime = 0f;
            return;
        }

        int groundedCount = 0;
        int tipCount = 0;

        if (leftGrounded)
        {
            groundedCount++;
            if (leftSkiContact.HasTipContact)
                tipCount++;
        }

        if (rightGrounded)
        {
            groundedCount++;
            if (rightSkiContact.HasTipContact)
                tipCount++;
        }

        // Fraction of grounded skis whose last contact is in the tip/tail region.
        float tipFraction = groundedCount > 0 ? (float)tipCount / groundedCount : 0f;

        // How "flat" are we on our bases? (1 = flat base, 0 = side, <0 = upside down).
        float leftBaseAlign = leftGrounded ? leftSkiContact.BaseContactAlignment : 0f;
        float rightBaseAlign = rightGrounded ? rightSkiContact.BaseContactAlignment : 0f;

        // Threshold for "this is still the bottom face of the ski".
        // Values near 1 are flat on the base, near 0 are side/tip contacts.
        const float MinBaseAlignmentForStableStance = 0.2f; // ~78° from perfect; generous for carving

        bool leftBaseStable = leftGrounded && leftBaseAlign >= MinBaseAlignmentForStableStance;
        bool rightBaseStable = rightGrounded && rightBaseAlign >= MinBaseAlignmentForStableStance;

        bool anyStableBase = leftBaseStable || rightBaseStable;

        // If NEITHER ski has a reasonably flat base contact, we're effectively on
        // edges/tips on both skis – this is not a stable stance. Stack immediately.
        if (!anyStableBase)
        {
            TriggerStack();
            _tipContactAccumTime = 0f;
            return;
        }

        // At least one ski has a decent base contact. Now check whether we're
        // "riding on the tips" on the dominant contacts.
        if (tipFraction >= TipContactStackFraction)
        {
            // Heavily tip/tail-weighted contact: accumulate time.
            _tipContactAccumTime += Time.fixedDeltaTime;

            // Leaning strongly forward (positive _forwardLean) makes tip-stands even less stable:
            // bias the required time down.
            float forwardLeanFactor = 1f;
            if (_forwardLean > 0f)
            {
                // forwardLean in 0..1 => reduce required time by up to ~50%.
                forwardLeanFactor = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(_forwardLean));
            }

            if (_tipContactAccumTime * forwardLeanFactor >= TipContactStackTime)
            {
                TriggerStack();
                _tipContactAccumTime = 0f;
            }
        }
        else
        {
            // Back on our bases / not predominantly on tips: decay timer so short
            // tip touches don't instantly stack us.
            _tipContactAccumTime = Mathf.Max(0f, _tipContactAccumTime - Time.fixedDeltaTime);
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

        // Push stance into SkiContact once
        if (leftSkiContact != null)
            leftSkiContact.StanceOut = _leftOut;

        if (rightSkiContact != null)
            rightSkiContact.StanceOut = _rightOut;
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
        else if (normalDot > 0f && maxStickUpwardSpeed > 0f &&
         (_movementMode == MovementMode.Skiing || _movementMode == MovementMode.Landing))
        {
            // If we've just jumped, don't damp upward velocity at all;
            // we want small pops to fully leave the snow.
            bool suppressStickiness =
                (Time.time - _lastJumpTime) <= jumpStickSuppressionTime;

            if (!suppressStickiness)
            {
                float upSpeed = normalDot; // since _groundNormal is unit-length

                if (upSpeed <= maxStickUpwardSpeed)
                {
                    float t = upSpeed / maxStickUpwardSpeed;        // 0..1
                    float strength = normalKillStrength * (1f - t); // high at low speed

                    velocity -= velAlongNormal * Mathf.Clamp01(strength * Time.fixedDeltaTime);
                }
            }
        }

        // Commit any normal-axis changes back to the rigidbody before we compute planar motion.
        _rb.linearVelocity = velocity;

        // Planar velocity on slope
        Vector3 velOnPlane = Vector3.ProjectOnPlane(velocity, _groundNormal);

        // --- Downhill acceleration driven by ski direction ---
        _skiForward = GetCombinedSkiForwardOnPlane();

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

            // ------------------------------------------------------------------
            // 1. Baseline downhill slide (always some slip)
            //
            // Even if we're traversing (alignment ~ 0), we still want to slide
            // down the hill a bit instead of "sticking" and losing all momentum.
            // slideFactor ∈ [minSlideFactor, 1].
            // ------------------------------------------------------------------
            float slideAlign = Mathf.Clamp01(alignAbs);
            float slideFactor = Mathf.Lerp(minSlideFactor, 1f, slideAlign);

            Vector3 baselineAccel = downhillDir * (downhillAccelMin * slideFactor);
            _rb.AddForce(baselineAccel, ForceMode.Acceleration);

            // ------------------------------------------------------------------
            // 2. Lean-based drive & brake (requires some alignment)
            //
            // Extra acceleration/braking only kicks in when we're at least
            // somewhat aligned with the fall line.
            // ------------------------------------------------------------------
            float driveAlign = Mathf.InverseLerp(minAlignmentForDownhill, 1f, alignAbs);
            if (driveAlign > 0f)
            {
                float leanAbs = Mathf.Clamp01(Mathf.Abs(_forwardLean));
                if (leanAbs > 0.001f)
                {
                    // Facing direction on the slope.
                    Vector3 facingOnPlane = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
                    if (facingOnPlane.sqrMagnitude > 0.0001f)
                        facingOnPlane.Normalize();
                    else
                        facingOnPlane = skiDir;

                    // World-space lean direction on slope.
                    Vector3 leanDirOnPlane = facingOnPlane * Mathf.Sign(_forwardLean);
                    Vector3 leanDir = Vector3.ProjectOnPlane(leanDirOnPlane, _groundNormal);
                    if (leanDir.sqrMagnitude > 0.0001f)
                        leanDir.Normalize();
                    else
                        leanDir = downhillDir;

                    float leanSlopeDot = Vector3.Dot(leanDir, downhillDir);

                    // Extra accel budget for drive/brake.
                    float extraAccelMag = Mathf.Lerp(
                        0f,
                        downhillAccelMax - downhillAccelMin,
                        leanAbs
                    ) * driveAlign;

                    if (leanSlopeDot > 0.001f)
                    {
                        // Leaning toward descending slope: gain momentum.
                        float scale = leanSlopeDot;
                        Vector3 drive = downhillDir * (extraAccelMag * scale);
                        _rb.AddForce(drive, ForceMode.Acceleration);
                    }
                    else if (leanSlopeDot < -0.001f)
                    {
                        velOnPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
                        if (velOnPlane.sqrMagnitude > 0.0001f)
                        {
                            // Leaning toward ascending slope: SOFT brake opposite planar velocity.
                            float scale = -leanSlopeDot;

                            Vector3 velDir = velOnPlane.normalized;
                            float planarSpeed = velOnPlane.magnitude;

                            float speedFactor = Mathf.InverseLerp(0f, skateMaxEffectiveSpeed, planarSpeed);
                            const float brakeSoftness = 0.5f;

                            float brakeAccelMag = extraAccelMag * scale * speedFactor * brakeSoftness;
                            Vector3 brake = -velDir * brakeAccelMag;
                            _rb.AddForce(brake, ForceMode.Acceleration);
                        }
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

        // We only actually perform the jump once the player has released
        // the button, so they can hold to "wind up" and then choose when
        // to pop.
        if (!_jumpReleaseQueued)
            return;

        // Can't jump while stacked.
        if (_stacked)
        {
            _jumpQueued = false;
            _jumpReleaseQueued = false;
            return;
        }

        // Allow jump when grounded, or shortly after leaving the ground (coyote time).
        bool withinCoyote = !_isGrounded && (Time.time - _lastGroundedTime <= jumpCoyoteTime);
        bool canJumpNow = _isGrounded || withinCoyote;

        if (!canJumpNow)
        {
            // We have a release queued, but we're not in a valid state yet.
            // Keep it buffered until we land or it expires.
            if (Time.time - _lastJumpPressedTime > jumpBufferTime)
            {
                _jumpQueued = false;
                _jumpReleaseQueued = false;
            }
            return;
        }

        // ------------------------------------------------------------------
        // 1. Compute charge-based jump strength (base force magnitude).
        // ------------------------------------------------------------------
        float holdDuration = Mathf.Max(0f, Time.time - _lastJumpPressedTime);
        float chargeT = maxJumpChargeTime > 0f
            ? Mathf.Clamp01(holdDuration / maxJumpChargeTime)
            : 1f;
        Debug.Log($"[Jump] Hold duration: {holdDuration:F3} s (chargeT = {chargeT:F2})");

        float minForce = Mathf.Min(jumpForceRange.x, jumpForceRange.y);
        float maxForce = Mathf.Max(jumpForceRange.x, jumpForceRange.y);
        float jumpForce = Mathf.Lerp(minForce, maxForce, chargeT);

        // ------------------------------------------------------------------
        // 1b. Scale jump force based on current planar speed.
        //     - At low speeds, multiplier ~1 (no change).
        //     - At high speeds, multiplier -> jumpSpeedForceMultiplier.
        // ------------------------------------------------------------------
        Vector3 velocity = _rb.linearVelocity;

        // Planar speed relative to the current ground; ignores vertical velocity.
        Vector3 velOnPlane = Vector3.ProjectOnPlane(velocity, _groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        float speedT = jumpSpeedForMaxMultiplier > 0f
            ? Mathf.Clamp01(planarSpeed / jumpSpeedForMaxMultiplier)
            : 0f;

        float speedMultiplier = Mathf.Lerp(1f, jumpSpeedForceMultiplier, speedT);

        // Apply speed multiplier on top of charge-based force.
        jumpForce *= speedMultiplier;

        // ------------------------------------------------------------------
        // 2. Jump direction = pure ground normal (slope "up").
        //
        // Flat ground   -> (0,1,0)  = straight up.
        // 45° slope     -> normal tilted 45° out from world up.
        // 90° wall      -> normal pointing straight out from wall.
        // ------------------------------------------------------------------
        Vector3 jumpDir = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        // We do NOT add any explicit forward component here.
        // Existing _rb.linearVelocity (whatever speed/direction you had)
        // plus this jump impulse is all that defines the arc.

        // ------------------------------------------------------------------
        // 3. Apply the impulse.
        // ------------------------------------------------------------------
        _rb.AddForce(jumpDir * jumpForce, ForceMode.VelocityChange);

        // Record that we just jumped so:
        //  - ground stickiness won't damp this upward motion
        //  - CheckGround() will respect minJumpUngroundedTime.
        _lastJumpTime = Time.time;
        _isGrounded = false;

        // Start airborne timing from the moment we actually leave the ground.
        _airborneStartTime = _lastJumpTime;

        // ------------------------------------------------------------------
        // 4. Seed aerial rotation based on current inputs ("wind-up").
        // ------------------------------------------------------------------
        if (HasLegInputs || HasLeanInput)
        {
            float yawInput = HasLegInputs
                ? Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f)
                : 0f;

            float pitchInput = HasLeanInput ? _rawLeanInput : 0f;

            _airAngularVelocity.y = yawInput * airYawTurnSpeed * 0.5f;
            _airAngularVelocity.x = pitchInput * airPitchTurnSpeed * 0.5f;
        }

        _jumpQueued = false;
        _jumpReleaseQueued = false;
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
        float dt = Time.fixedDeltaTime;

        // SAFETY: if we're still grounded or any ski is in contact, do NOT apply
        // aerial spin/flip controls. Instead, gently damp out any carried air spin.
        if (_isGrounded || HasAnySkiContact)
        {
            float damping = Mathf.Max(0f, airAngularDamping);
            _airAngularVelocity = Vector3.MoveTowards(
                _airAngularVelocity,
                Vector3.zero,
                damping * dt);

            return;
        }

        // ------------------------------------------------------------------
        // True air control (only when fully airborne)
        // ------------------------------------------------------------------

        // Leg difference controls yaw (spin), lean controls pitch (flip).
        float yawInput = HasLegInputs
            ? Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f)
            : 0f;

        // Invert lean so forward lean pitches you slightly back (feels more natural for flips).
        float pitchInput = _rawLeanInput;

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
        float dampingAir = Mathf.Max(0f, airAngularDamping);

        // Accelerate toward target spin speeds.
        _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, targetYawSpeed, accel * dt);
        _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, targetPitchSpeed, accel * dt);

        // Apply damping toward zero when there is little/no input so we don't spin forever.
        if (Mathf.Abs(yawInput) < 0.01f)
        {
            _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, 0f, dampingAir * dt);
        }
        if (Mathf.Abs(pitchInput) < 0.01f)
        {
            _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, 0f, dampingAir * dt);
        }

        // Apply rotation based on current angular velocity.
        Quaternion yawRot = Quaternion.AngleAxis(_airAngularVelocity.y * dt, Vector3.up);
        Quaternion pitchRot = Quaternion.AngleAxis(_airAngularVelocity.x * dt, transform.right);

        _rb.MoveRotation(yawRot * pitchRot * _rb.rotation);
    }

    private void OnGUI()
    {
        if (!showDebugHUD)
            return;

        const int width = 420;
        const int height = 220;

        // Top-left corner HUD
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, width, height), GUI.skin.box);

        GUILayout.Label($"Mode:              {_movementMode}");
        GUILayout.Label($"isGrounded (casts): {_isGrounded}");
        GUILayout.Label($"IsGroundedForCtrl:  {IsGroundedForControls}");
        GUILayout.Label($"HasAnySkiContact:   {HasAnySkiContact}");
        GUILayout.Label($"Stacked:            {_stacked}");

        Vector3 velPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        GUILayout.Label($"PlanarSpeed:        {velPlane.magnitude:F2} m/s");
        GUILayout.Label($"ForwardLean:        {_forwardLean:F2}");
        GUILayout.Label($"TipAccumTime:       {_tipContactAccumTime:F3}s");

        if (leftSkiContact != null)
        {
            GUILayout.Space(4f);
            GUILayout.Label($"LEFT: grounded={leftSkiContact.IsGrounded}, tip={leftSkiContact.HasTipContact}, baseAlign={leftSkiContact.BaseContactAlignment:F2}");
        }

        if (rightSkiContact != null)
        {
            GUILayout.Label($"RIGHT: grounded={rightSkiContact.IsGrounded}, tip={rightSkiContact.HasTipContact}, baseAlign={rightSkiContact.BaseContactAlignment:F2}");
        }

        GUILayout.EndArea();
    }

}


