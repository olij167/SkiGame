using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simplified SkiController.
///
/// Design goals:
/// - Preserve existing visuals (body lean, ski stance/yaw visuals, pole animation API).
/// - Reduce inspector clutter by exposing a small set of high-leverage controls.
/// - Keep behaviour deterministic and stable on terrain by leaning on per-ski SkiContact sampling.
/// - Reduce duplicated / competing systems (multiple ground normals, multiple friction knobs, etc.).
///
/// Notes:
/// - This script is intended to live alongside the current SkiController so you can A/B test.
/// - It keeps the same public API used by PoleContact (GroundNormal, Velocity, SkiForwardOnPlane,
///   IsRiderGrounded, CurrentPolePhase, PoleStrokeT).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class SkiControllerSimplified : MonoBehaviour
{
    public enum PoleStrokePhase
    {
        Idle,
        Entry,
        Drag,
        FollowThrough
    }

    private enum Mode
    {
        Ground,
        Air,
        Stacked
    }

    // ---------------------------------------------------------------------
    // References
    // ---------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Optional visual body root that leans independently of the skis.")]
    [SerializeField] private Transform bodyTransform;

    [Tooltip("Left ski transform.")]
    [SerializeField] private Transform leftSki;
    [Tooltip("SkiContact for the left ski (auto-assigned from leftSki if null).")]
    [SerializeField] private SkiContact leftSkiContact;

    [Tooltip("Right ski transform.")]
    [SerializeField] private Transform rightSki;
    [Tooltip("SkiContact for the right ski (auto-assigned from rightSki if null).")]
    [SerializeField] private SkiContact rightSkiContact;

    [Tooltip("Optional PoleContact for left pole visuals.")]
    [SerializeField] private PoleContact leftPoleContact;

    [Tooltip("Optional PoleContact for right pole visuals.")]
    [SerializeField] private PoleContact rightPoleContact;

    [Header("Input Actions")]
    [Tooltip("Float action for left leg stance/edge (0..1).")]
    [SerializeField] private InputActionReference leftSkiAction;
    [Tooltip("Float action for right leg stance/edge (0..1).")]
    [SerializeField] private InputActionReference rightSkiAction;

    [Tooltip("Optional float action for forward/back lean (-1..1).")]
    [SerializeField] private InputActionReference leanAction;

    [Tooltip("Optional button action for poles. Hold = drag/brake, release = follow-through.")]
    [SerializeField] private InputActionReference polesAction;

    [Tooltip("Optional button action for jump. Hold to charge, release to jump.")]
    [SerializeField] private InputActionReference jumpAction;


    // ---------------------------------------------------------------------
    // High-level tuning (minimal inspector)
    // ---------------------------------------------------------------------

    [Header("Feel")]
    [Tooltip("How 'slippery' the skis feel along their length. Higher = more glide (less speed loss).")]
    [SerializeField, Range(0f, 1f)] private float glide = 0.65f;

    [Tooltip("How strongly the skis resist sideways slip. Higher = stronger edge hold.")]
    [SerializeField, Range(0f, 1f)] private float edgeHold = 0.60f;

    [Tooltip("Scales downhill acceleration while grounded (slope-parallel gravity).")]
    [SerializeField, Range(0.5f, 2.5f)] private float downhillGravityScale = 1.25f;

    [Tooltip("How responsive turning feels while grounded (yaw rate toward ski direction).")]
    [SerializeField, Range(0f, 1f)] private float turnResponsiveness = 0.65f;

    [Tooltip("How strongly velocity is 'carved' toward ski direction at speed. Higher = more carve lock.")]
    [SerializeField, Range(0f, 1f)] private float carveLock = 0.55f;

    [Tooltip("How strongly we damp micro-bounces into the ground (keeps you glued to snow).")]
    [SerializeField, Range(0f, 1f)] private float stickiness = 0.55f;

    [Tooltip("Extra assistance to stand still while traversing across the fall line. 0 disables.")]
    [SerializeField, Range(0f, 1f)] private float traverseHold = 0.65f;

    [Tooltip("How strong lean affects speed: forward lean preserves speed; back lean brakes.")]
    [SerializeField, Range(0f, 1f)] private float leanSpeedEffect = 0.65f;


    [Header("Stacking / Safety")]
    [Tooltip("Maximum allowed tilt angle (deg) between rider up and ground normal. Exceeding while grounded stacks you.")]
    [SerializeField, Range(5f, 85f)] private float maxSafeTiltAngle = 55f;

    [Tooltip("If velocity is strongly into the slope at impact, stack. Larger = more forgiving.")]
    [SerializeField] private float maxVelocityIntoSlope = 9f;

    [Tooltip("Minimum planar speed for stack checks to apply.")]
    [SerializeField] private float minSpeedForStackChecks = 4.5f;

    [Tooltip("Seconds after landing where we boost alignment to settle quickly.")]
    [SerializeField] private float landingAssistTime = 0.22f;

    [Tooltip("How much to project impact velocity onto the slope at landing. Higher retains momentum instead of slamming.")]
    [SerializeField, Range(0f, 1f)] private float landingProjection = 0.65f;


    [Header("Jump")]
    [Tooltip("Min/Max velocity change applied along the jump direction.")]
    [SerializeField] private Vector2 jumpForceRange = new Vector2(3f, 8f);

    [Tooltip("How long holding jump builds to full charge.")]
    [SerializeField] private float maxJumpChargeTime = 0.5f;

    [Tooltip("Grace time after leaving ground where jump can still fire.")]
    [SerializeField] private float jumpCoyoteTime = 0.15f;

    [Tooltip("Buffer time where a jump press will trigger upon next ground contact.")]
    [SerializeField] private float jumpBufferTime = 0.10f;

    [Tooltip("Seconds after a jump during which stickiness will not damp upward motion.")]
    [SerializeField] private float jumpStickSuppressionTime = 0.2f;


    [Header("Air Control")]
    [Tooltip("Yaw turn speed in the air (deg/sec) from leg difference.")]
    [SerializeField] private float airYawTurnSpeed = 300f;

    [Tooltip("Pitch turn speed in the air (deg/sec) from lean.")]
    [SerializeField] private float airPitchTurnSpeed = 300f;

    [Tooltip("How quickly air rotation responds to input.")]
    [SerializeField] private float airAngularAccel = 720f;

    [Tooltip("Air angular damping when there is little/no input.")]
    [SerializeField] private float airAngularDamp = 360f;


    [Header("Skating")]
    [Tooltip("Lean required to generate a skate push.")]
    [SerializeField] private float minForwardLeanForPush = 0.12f;

    [Tooltip("Velocity change per valid push.")]
    [SerializeField] private float skateImpulse = 2.5f;

    [Tooltip("Seconds between pushes.")]
    [SerializeField] private float skateCooldown = 0.2f;

    [Tooltip("Max speed where skate pushes remain effective.")]
    [SerializeField] private float skateMaxEffectiveSpeed = 6f;

    [Tooltip("How hard skating is penalized uphill (0 = none, 1 = strong).")]
    [SerializeField, Range(0f, 1f)] private float uphillSkatePenalty = 0.65f;


    [Header("Poles")]
    [Tooltip("Base stroke speed when stationary.")]
    [SerializeField] private float basePoleStrokeSpeed = 2f;

    [Tooltip("Impulse applied during propulsion phase.")]
    [SerializeField] private float poleImpulse = 3f;

    [Tooltip("Braking strength while poles are held in drag.")]
    [SerializeField] private float poleBrakeStrength = 10f;

    [Tooltip("Max speed where pole effects are strong.")]
    [SerializeField] private float poleMaxSpeed = 8f;


    [Header("Visuals")]
    [Tooltip("How quickly forward/back lean smooths toward input.")]
    [SerializeField] private float forwardLeanLerpSpeed = 5f;

    [Tooltip("How quickly stance values smooth toward leg inputs.")]
    [SerializeField] private float stanceLerpSpeed = 10f;

    [Tooltip("How fast ski models interpolate toward target yaw/offset.")]
    [SerializeField] private float skiVisualLerpSpeed = 12f;

    [Tooltip("Max lateral offset per ski from stance.")]
    [SerializeField] private float maxSkiOffset = 0.3f;

    [Tooltip("Max inward yaw (deg) when a ski is fully edged at speed.")]
    [SerializeField] private float maxSkiEdgeAngle = 20f;

    [Tooltip("Max visual forward lean angle (deg) on the body transform.")]
    [SerializeField] private float maxForwardLeanAngle = 25f;

    [Tooltip("Max visual side lean angle (deg) on the body transform.")]
    [SerializeField] private float maxSideLeanAngle = 15f;


    [Header("Grounding")]
    [Tooltip("Ground layers for fallback near-ground sphere cast and non-ski collision checks.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Fallback spherecast radius under the rider root (used only when ski contacts are missing).")]
    [SerializeField] private float groundProbeRadius = 0.25f;

    [Tooltip("Fallback spherecast height above root.")]
    [SerializeField] private float groundProbeHeight = 0.6f;

    [Tooltip("Fallback spherecast distance below the probe origin.")]
    [SerializeField] private float groundProbeDistance = 1.2f;

    [Tooltip("Max slope angle treated as ground.")]
    [SerializeField, Range(0f, 90f)] private float maxGroundSlopeAngle = 80f;

    [Tooltip("How quickly the ground normal smooths while grounded.")]
    [SerializeField] private float groundNormalSmoothSpeed = 12f;

    [Tooltip("Controls remain 'ground-like' for this long after losing contact.")]
    [SerializeField] private float controlCoyoteTime = 0.08f;


    // ---------------------------------------------------------------------
    // Public API (used by PoleContact and debugging)
    // ---------------------------------------------------------------------

    public bool IsStacked => _mode == Mode.Stacked;
    public float PoleStrokeT => _poleStrokeT;
    public PoleStrokePhase CurrentPolePhase => _polePhase;
    public bool IsPoleInputHeld => _rawPolesHeld;
    public Vector3 GroundNormal => _groundNormal;
    public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    public Vector3 SkiForwardOnPlane => _skiForwardOnPlane;
    public bool IsRiderGrounded => _isGrounded;

    public PoleContact LeftPoleContact => leftPoleContact;
    public PoleContact RightPoleContact => rightPoleContact;


    // ---------------------------------------------------------------------
    // Internal state
    // ---------------------------------------------------------------------

    private Rigidbody _rb;

    // Raw inputs
    private float _rawLeft;
    private float _rawRight;
    private float _rawLean;
    private bool _rawPolesHeld;

    // Smoothed
    private float _lean;
    private float _leftOut;
    private float _rightOut;

    // Grounding
    private bool _isGrounded;
    private bool _wasGrounded;
    private float _lastGroundedTime;
    private Vector3 _groundNormal = Vector3.up;
    private Vector3 _skiForwardOnPlane = Vector3.forward;

    // Mode + stack
    private Mode _mode = Mode.Air;
    private float _landingAssistUntil;

    // Non-ski contact cache (anti "slide on head")
    private readonly System.Collections.Generic.HashSet<Collider> _skiColliders = new System.Collections.Generic.HashSet<Collider>();
    private bool _hasNonSkiGroundContact;
    private float _nonSkiUpDot;

    // Skating
    private float _lastSkateInput;
    private float _lastPushTime;

    // Poles
    private float _poleStrokeT;
    private PoleStrokePhase _polePhase = PoleStrokePhase.Idle;

    // Jump
    private bool _jumpHeld;
    private bool _jumpQueued;
    private bool _jumpReleaseQueued;
    private float _jumpPressedTime;
    private float _airborneStartTime;
    private float _airbornePeakY;
    private float _lastJumpTime;

    // Air rotation
    private Vector3 _airAngularVelocity; // deg/sec

    // Visual base transforms
    private Vector3 _leftSkiBaseLocalPos;
    private Vector3 _rightSkiBaseLocalPos;
    private Quaternion _leftSkiBaseLocalRot;
    private Quaternion _rightSkiBaseLocalRot;
    private Vector3 _leftSkiOffsetCur;
    private Vector3 _rightSkiOffsetCur;
    private float _leftSkiYawCur;
    private float _rightSkiYawCur;


    // ---------------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------------

    private bool HasLegInputs => leftSkiAction != null && leftSkiAction.action != null && rightSkiAction != null && rightSkiAction.action != null;
    private bool HasLeanInput => leanAction != null && leanAction.action != null;
    private bool HasPolesInput => polesAction != null && polesAction.action != null;
    private bool HasJumpInput => jumpAction != null && jumpAction.action != null;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (leftSki != null && leftSkiContact == null) leftSkiContact = leftSki.GetComponent<SkiContact>();
        if (rightSki != null && rightSkiContact == null) rightSkiContact = rightSki.GetComponent<SkiContact>();

        CacheSkiColliders();
        CacheSkiVisualBases();

        // Reasonable physics defaults.
        _rb.useGravity = false; // we apply our own gravity for determinism
    }

    private void OnEnable()
    {
        if (leftSkiAction != null) leftSkiAction.action.Enable();
        if (rightSkiAction != null) rightSkiAction.action.Enable();
        if (leanAction != null) leanAction.action.Enable();
        if (polesAction != null) polesAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        if (leftSkiAction != null) leftSkiAction.action.Disable();
        if (rightSkiAction != null) rightSkiAction.action.Disable();
        if (leanAction != null) leanAction.action.Disable();
        if (polesAction != null) polesAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
    }

    private void Update()
    {
        ReadInputs();
        UpdatePoleState();
        SmoothInputs();
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        // Deterministic per-ski sampling.
        if (leftSkiContact != null) leftSkiContact.ManualSampleGround();
        if (rightSkiContact != null) rightSkiContact.ManualSampleGround();

        _wasGrounded = _isGrounded;
        UpdateGrounding();

        // Mode changes + landing bookkeeping
        if (_wasGrounded && !_isGrounded)
        {
            _airborneStartTime = Time.time;
            _airbornePeakY = transform.position.y;
        }
        if (!_isGrounded)
            _airbornePeakY = Mathf.Max(_airbornePeakY, transform.position.y);

        if (_mode == Mode.Stacked)
        {
            // Lightweight auto-recover: once largely upright and slow, return to ground mode.
            TryAutoRecover();
            if (_mode == Mode.Stacked) return;
        }

        TryConsumeJump();

        if (IsGroundedForControls())
        {
            _mode = Mode.Ground;
            ApplyGroundForces();
            ApplySkatePushes();
            ApplyPoleForces();
            AlignToGroundAndSkis();
            CheckStackConditions();
        }
        else
        {
            _mode = Mode.Air;
            ApplyAirForces();
        }
    }


    // ---------------------------------------------------------------------
    // Collisions (non-ski body contact)
    // ---------------------------------------------------------------------

    private void OnCollisionStay(Collision collision)
    {
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        // Ignore ski colliders themselves.
        bool anyNonSki = false;
        float bestUpDot = -1f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint c = collision.GetContact(i);

            // If the contact involves any ski collider, ignore.
            if (c.thisCollider != null && _skiColliders.Contains(c.thisCollider))
                continue;

            float upDot = Vector3.Dot(c.normal, Vector3.up);
            if (upDot > bestUpDot)
                bestUpDot = upDot;

            anyNonSki = true;
        }

        if (anyNonSki)
        {
            _hasNonSkiGroundContact = true;
            _nonSkiUpDot = bestUpDot;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        _hasNonSkiGroundContact = false;
    }


    // ---------------------------------------------------------------------
    // Input + smoothing
    // ---------------------------------------------------------------------

    private void ReadInputs()
    {
        _rawLeft = HasLegInputs ? Mathf.Clamp01(leftSkiAction.action.ReadValue<float>()) : 0f;
        _rawRight = HasLegInputs ? Mathf.Clamp01(rightSkiAction.action.ReadValue<float>()) : 0f;
        _rawLean = HasLeanInput ? Mathf.Clamp(leanAction.action.ReadValue<float>(), -1f, 1f) : 0f;
        _rawPolesHeld = HasPolesInput && polesAction.action.ReadValue<float>() > 0.5f;

        HandleJumpInput();
    }

    private void SmoothInputs()
    {
        float dt = Time.deltaTime;

        _lean = Mathf.Lerp(_lean, _rawLean, 1f - Mathf.Exp(-forwardLeanLerpSpeed * dt));
        _leftOut = Mathf.Lerp(_leftOut, _rawLeft, 1f - Mathf.Exp(-stanceLerpSpeed * dt));
        _rightOut = Mathf.Lerp(_rightOut, _rawRight, 1f - Mathf.Exp(-stanceLerpSpeed * dt));

        if (leftSkiContact != null) leftSkiContact.StanceOut = _leftOut;
        if (rightSkiContact != null) rightSkiContact.StanceOut = _rightOut;

        // Cache last skate input for alternating leg pushes.
        _lastSkateInput = _rawLeft - _rawRight; // sign indicates which leg is pushing more
    }


    // ---------------------------------------------------------------------
    // Grounding
    // ---------------------------------------------------------------------

    private bool HasAnySkiContact =>
        (leftSkiContact != null && leftSkiContact.IsGrounded) ||
        (rightSkiContact != null && rightSkiContact.IsGrounded);

    private bool IsGroundedForControls()
    {
        if (_isGrounded) return true;
        return (Time.time - _lastGroundedTime) <= controlCoyoteTime;
    }

    private void UpdateGrounding()
    {
        // Primary: trust per-ski contacts.
        bool grounded = HasAnySkiContact;
        Vector3 n = Vector3.up;

        if (grounded)
        {
            Vector3 sum = Vector3.zero;
            float w = 0f;

            if (leftSkiContact != null && leftSkiContact.IsGrounded)
            {
                float wl = Mathf.Clamp01(leftSkiContact.BaseContactAlignment);
                sum += leftSkiContact.ContactNormal * (0.25f + wl);
                w += (0.25f + wl);
            }

            if (rightSkiContact != null && rightSkiContact.IsGrounded)
            {
                float wr = Mathf.Clamp01(rightSkiContact.BaseContactAlignment);
                sum += rightSkiContact.ContactNormal * (0.25f + wr);
                w += (0.25f + wr);
            }

            if (w > 0.0001f) n = (sum / w).normalized;
        }
        else
        {
            // Fallback: single spherecast under the rider root. This prevents "full air" when the
            // skis briefly miss contact on seams.
            Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
            if (Physics.SphereCast(origin, groundProbeRadius, Vector3.down, out RaycastHit hit, groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                if (slopeAngle <= maxGroundSlopeAngle)
                {
                    grounded = true;
                    n = hit.normal;
                }
            }
        }

        // Filter out walls.
        float ang = Vector3.Angle(n, Vector3.up);
        if (ang > maxGroundSlopeAngle) grounded = false;

        _isGrounded = grounded;
        if (_isGrounded) _lastGroundedTime = Time.time;

        // Smooth ground normal.
        float t = 1f - Mathf.Exp(-groundNormalSmoothSpeed * Time.fixedDeltaTime);
        _groundNormal = Vector3.Slerp(_groundNormal, grounded ? n : Vector3.up, t);
        if (_groundNormal.sqrMagnitude > 0.0001f) _groundNormal.Normalize();

        // Cache combined ski forward on plane (for forces + visuals + pole context).
        _skiForwardOnPlane = GetCombinedSkiForwardOnPlane(_groundNormal);
        if (_skiForwardOnPlane.sqrMagnitude < 0.0001f)
            _skiForwardOnPlane = Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;
    }

    private Vector3 GetCombinedSkiForwardOnPlane(Vector3 planeNormal)
    {
        Vector3 f = Vector3.zero;
        int c = 0;

        if (leftSki != null)
        {
            Vector3 lf = Vector3.ProjectOnPlane(leftSki.forward, planeNormal);
            if (lf.sqrMagnitude > 0.0001f) { f += lf.normalized; c++; }
        }

        if (rightSki != null)
        {
            Vector3 rf = Vector3.ProjectOnPlane(rightSki.forward, planeNormal);
            if (rf.sqrMagnitude > 0.0001f) { f += rf.normalized; c++; }
        }

        if (c == 0) return Vector3.zero;
        return (f / c).normalized;
    }


    // ---------------------------------------------------------------------
    // Forces
    // ---------------------------------------------------------------------

    private void ApplyGroundForces()
    {
        float dt = Time.fixedDeltaTime;

        // Custom gravity: apply only slope-parallel gravity while grounded.
        Vector3 gPlane = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal) * downhillGravityScale;
        _rb.AddForce(gPlane, ForceMode.Acceleration);

        Vector3 v = _rb.linearVelocity;
        Vector3 vPlane = Vector3.ProjectOnPlane(v, _groundNormal);
        float speed = vPlane.magnitude;

        // Derive friction scalars from high-level knobs.
        float forwardFriction = Mathf.Lerp(0.55f, 0.10f, glide); // higher glide => less loss
        float sideFriction = Mathf.Lerp(0.65f, 2.5f, edgeHold);

        // Lean modifies forward friction (tuck / brake).
        float leanT = Mathf.Clamp01(_lean);
        float brakeT = Mathf.Clamp01(-_lean);

        float tuckMul = 1f - (leanSpeedEffect * 0.55f) * leanT;
        float brakeMul = 1f + (leanSpeedEffect * 1.25f) * brakeT;
        float leanFrictionMul = tuckMul * brakeMul;

        // Apply anisotropic damping in the ground plane.
        if (speed > 0.001f)
        {
            Vector3 fwd = _skiForwardOnPlane.sqrMagnitude > 0.0001f ? _skiForwardOnPlane.normalized : vPlane.normalized;
            Vector3 right = Vector3.Cross(_groundNormal, fwd).normalized;

            float vF = Vector3.Dot(vPlane, fwd);
            float vS = Vector3.Dot(vPlane, right);

            // Exponential decay, stable across dt.
            float kF = forwardFriction * leanFrictionMul;
            float kS = sideFriction;

            vF *= Mathf.Exp(-kF * dt);
            vS *= Mathf.Exp(-kS * dt);

            vPlane = fwd * vF + right * vS;
        }

        // Stickiness: damp velocity into the ground normal (micro bounces).
        if ((Time.time - _lastJumpTime) > jumpStickSuppressionTime)
        {
            float normalKill = Mathf.Lerp(0f, 10f, stickiness);
            float vN = Vector3.Dot(v, _groundNormal);

            // Only kill small upward bumps; allow real jumps.
            if (vN > 0f && vN < 2.0f)
            {
                v -= _groundNormal * (vN * (1f - Mathf.Exp(-normalKill * dt)));
            }
        }

        // Traverse hold: when strongly across fall-line, damp fall-line drift.
        if (traverseHold > 0.001f && gPlane.sqrMagnitude > 0.0001f)
        {
            Vector3 fallDir = gPlane.normalized;
            float across = 1f - Mathf.Abs(Vector3.Dot(_skiForwardOnPlane, fallDir));
            float acrossGate = Mathf.InverseLerp(0.65f, 1f, across);
            float vFall = Vector3.Dot(vPlane, fallDir);
            float speedGate = 1f - Mathf.Clamp01(Mathf.Abs(vFall) / 1.0f);
            float hold = traverseHold * acrossGate * speedGate * (1f - Mathf.Clamp01(_lean));

            if (hold > 0f)
            {
                float damp = Mathf.Lerp(0f, 10f, hold);
                vPlane -= fallDir * (vFall * (1f - Mathf.Exp(-damp * dt)));
            }
        }

        // Turning: rotate planar velocity toward ski direction (carve lock) and yaw the body.
        if (speed > 0.05f && _skiForwardOnPlane.sqrMagnitude > 0.0001f)
        {
            Vector3 fwd = _skiForwardOnPlane.normalized;
            Vector3 vDir = vPlane.normalized;

            float steer = Mathf.Lerp(1.5f, 7.0f, carveLock);
            float steerDt = 1f - Mathf.Exp(-steer * dt);
            Vector3 steered = Vector3.Slerp(vDir, fwd, steerDt).normalized;
            vPlane = steered * speed;

            float yawRate = Mathf.Lerp(5f, 14f, turnResponsiveness);
            Quaternion targetYaw = Quaternion.LookRotation(fwd, _groundNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetYaw, 1f - Mathf.Exp(-yawRate * dt));
        }

        // Write back.
        _rb.linearVelocity = vPlane + _groundNormal * Vector3.Dot(v, _groundNormal);

        // Landing assist window: on first grounded frame after air, project velocity.
        if (!_wasGrounded && _isGrounded)
        {
            _landingAssistUntil = Time.time + landingAssistTime;
            ApplyLandingProjection();
        }
    }

    private void ApplyLandingProjection()
    {
        Vector3 v = _rb.linearVelocity;
        Vector3 vPlane = Vector3.ProjectOnPlane(v, _groundNormal);
        float vN = Vector3.Dot(v, _groundNormal);

        // Remove aggressive "into ground" component and keep planar momentum.
        if (vN < 0f)
        {
            vN = Mathf.Lerp(vN, 0f, landingProjection);
        }

        _rb.linearVelocity = vPlane + _groundNormal * vN;
    }

    private void AlignToGroundAndSkis()
    {
        float dt = Time.fixedDeltaTime;

        // During landing assist, align a bit faster.
        float boost = (Time.time <= _landingAssistUntil) ? 1.35f : 1f;
        float alignRate = Mathf.Lerp(10f, 26f, stickiness) * boost;

        // Maintain the yaw already steered; only align pitch/roll via up vector.
        Vector3 fwdPlane = _skiForwardOnPlane.sqrMagnitude > 0.0001f ? _skiForwardOnPlane : Vector3.ProjectOnPlane(transform.forward, _groundNormal);
        if (fwdPlane.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(fwdPlane.normalized, _groundNormal);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-alignRate * dt));
    }

    private void ApplyAirForces()
    {
        float dt = Time.fixedDeltaTime;

        // Full gravity in air.
        _rb.AddForce(Physics.gravity, ForceMode.Acceleration);

        // Air control uses lean + leg difference.
        float yawInput = Mathf.Clamp(_rawLeft - _rawRight, -1f, 1f);
        float pitchInput = Mathf.Clamp(_rawLean, -1f, 1f);

        // Optionally tuck spin while holding poles.
        float tuckMul = _rawPolesHeld ? 1.25f : 1f;

        Vector3 target = new Vector3(
            pitchInput * airPitchTurnSpeed,
            yawInput * airYawTurnSpeed,
            0f);

        // Smooth angular velocity.
        _airAngularVelocity = Vector3.MoveTowards(_airAngularVelocity, target, airAngularAccel * dt * tuckMul);

        // Dampen when no input.
        if (Mathf.Abs(yawInput) < 0.05f && Mathf.Abs(pitchInput) < 0.05f)
        {
            _airAngularVelocity = Vector3.MoveTowards(_airAngularVelocity, Vector3.zero, airAngularDamp * dt);
        }

        // Apply.
        Quaternion dq = Quaternion.Euler(_airAngularVelocity * dt);
        transform.rotation = dq * transform.rotation;
    }


    // ---------------------------------------------------------------------
    // Skating + poles
    // ---------------------------------------------------------------------

    private void ApplySkatePushes()
    {
        if (!_isGrounded) return;

        float dt = Time.fixedDeltaTime;
        Vector3 vPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        float speed = vPlane.magnitude;

        if (Time.time < _lastPushTime + skateCooldown) return;
        if (_lean < minForwardLeanForPush) return;

        // Detect a push: leg difference crosses sign with sufficient magnitude.
        float diff = _rawLeft - _rawRight;
        float prev = _lastSkateInput;
        _lastSkateInput = diff;

        if (Mathf.Abs(diff) < 0.25f) return;
        if (Mathf.Sign(diff) == Mathf.Sign(prev)) return;

        // Reduce effectiveness at high speed.
        float speedT = 1f - Mathf.Clamp01(speed / Mathf.Max(0.01f, skateMaxEffectiveSpeed));
        float impulse = skateImpulse * (0.35f + 0.65f * speedT);

        // Uphill penalty.
        Vector3 fallDir = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal).normalized;
        float uphill = Vector3.Dot(_skiForwardOnPlane, -fallDir); // 1 when pointing uphill
        float uphillMul = 1f - uphillSkatePenalty * Mathf.Clamp01((uphill + 1f) * 0.5f);

        _rb.AddForce(_skiForwardOnPlane.normalized * (impulse * uphillMul), ForceMode.VelocityChange);
        _lastPushTime = Time.time;
    }

    private void ApplyPoleForces()
    {
        if (!HasPolesInput || !_isGrounded) return;

        Vector3 vPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        float speed = vPlane.magnitude;
        float speedFactor = 1f - Mathf.Clamp01(speed / Mathf.Max(0.01f, poleMaxSpeed));

        // Drag phase while held: braking.
        if (_polePhase == PoleStrokePhase.Drag && _rawPolesHeld)
        {
            if (speed > 0.05f)
            {
                Vector3 dir = vPlane.normalized;
                _rb.AddForce(-dir * (poleBrakeStrength * speedFactor), ForceMode.Acceleration);
            }
        }

        // Follow-through: small propulsion.
        if (_polePhase == PoleStrokePhase.FollowThrough)
        {
            Vector3 dir = _skiForwardOnPlane.sqrMagnitude > 0.0001f ? _skiForwardOnPlane.normalized : transform.forward;
            _rb.AddForce(dir * (poleImpulse * speedFactor), ForceMode.Acceleration);
        }
    }


    // ---------------------------------------------------------------------
    // Pole state machine (kept compatible with PoleContact)
    // ---------------------------------------------------------------------

    private void UpdatePoleState()
    {
        float dt = Time.deltaTime;

        if (!HasPolesInput)
        {
            _polePhase = PoleStrokePhase.Idle;
            _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 0f, basePoleStrokeSpeed * dt);
            return;
        }

        bool grounded = _isGrounded;
        bool pressed = _rawPolesHeld && grounded;

        float strokeSpeed = basePoleStrokeSpeed;

        switch (_polePhase)
        {
            case PoleStrokePhase.Idle:
                _poleStrokeT = 0f;
                if (pressed)
                {
                    _polePhase = PoleStrokePhase.Entry;
                    _poleStrokeT = 0f;
                }
                break;

            case PoleStrokePhase.Entry:
                _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 1f, strokeSpeed * dt);
                if (_poleStrokeT >= 0.999f)
                {
                    _polePhase = PoleStrokePhase.Drag;
                    _poleStrokeT = 0f;
                }
                break;

            case PoleStrokePhase.Drag:
                _poleStrokeT = 0f;
                if (!grounded)
                {
                    _polePhase = PoleStrokePhase.Idle;
                    _poleStrokeT = 0f;
                }
                else if (!pressed)
                {
                    _polePhase = PoleStrokePhase.FollowThrough;
                    _poleStrokeT = 0f;
                }
                break;

            case PoleStrokePhase.FollowThrough:
                _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 1f, strokeSpeed * dt);
                if (_poleStrokeT >= 0.999f || !grounded)
                {
                    _polePhase = PoleStrokePhase.Idle;
                    _poleStrokeT = 0f;
                }
                break;
        }

        if (!grounded && _polePhase != PoleStrokePhase.Idle)
        {
            _polePhase = PoleStrokePhase.Idle;
            _poleStrokeT = Mathf.MoveTowards(_poleStrokeT, 0f, strokeSpeed * dt);
        }
    }


    // ---------------------------------------------------------------------
    // Jumping
    // ---------------------------------------------------------------------

    private void HandleJumpInput()
    {
        if (!HasJumpInput)
        {
            _jumpHeld = false;
            _jumpQueued = false;
            _jumpReleaseQueued = false;
            return;
        }

        var a = jumpAction.action;

        if (a.WasPressedThisFrame())
        {
            _jumpHeld = true;
            _jumpQueued = true;
            _jumpPressedTime = Time.time;
        }

        if (a.WasReleasedThisFrame())
        {
            _jumpHeld = false;
            _jumpReleaseQueued = true;
        }

        // Expire buffer.
        if (_jumpQueued && (Time.time - _jumpPressedTime) > jumpBufferTime)
            _jumpQueued = false;
    }

    private void TryConsumeJump()
    {
        if (!_jumpQueued || !_jumpReleaseQueued) return;

        bool canJump = _isGrounded || (Time.time - _lastGroundedTime) <= jumpCoyoteTime;
        if (!canJump) return;

        float chargeT = Mathf.Clamp01((Time.time - _jumpPressedTime) / Mathf.Max(0.01f, maxJumpChargeTime));
        float force = Mathf.Lerp(jumpForceRange.x, jumpForceRange.y, chargeT);

        // Jump direction: blend between ground normal and world up on very steep slopes.
        float slope = Vector3.Angle(_groundNormal, Vector3.up);
        float upBlend = Mathf.InverseLerp(55f, 80f, slope);
        Vector3 dir = Vector3.Slerp(_groundNormal, Vector3.up, Mathf.Clamp01(upBlend)).normalized;

        // Remove into-ground velocity and apply jump.
        Vector3 v = _rb.linearVelocity;
        float vN = Vector3.Dot(v, dir);
        if (vN < 0f) v -= dir * vN;
        _rb.linearVelocity = v;

        _rb.AddForce(dir * force, ForceMode.VelocityChange);

        _jumpQueued = false;
        _jumpReleaseQueued = false;
        _lastJumpTime = Time.time;
        _airborneStartTime = Time.time;
        _mode = Mode.Air;
    }


    // ---------------------------------------------------------------------
    // Stack rules (simplified and deterministic)
    // ---------------------------------------------------------------------

    private void CheckStackConditions()
    {
        if (!_isGrounded) return;

        Vector3 vPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        float speed = vPlane.magnitude;
        if (speed < minSpeedForStackChecks) return;

        // 1) Over-tilt relative to ground.
        float tilt = Vector3.Angle(transform.up, _groundNormal);
        if (tilt > maxSafeTiltAngle)
        {
            Stack("Tilt");
            return;
        }

        // 2) Non-ski body contact with decent up-dot (head/torso scraping the ground)
        if (_hasNonSkiGroundContact && _nonSkiUpDot > 0.25f)
        {
            Stack("Non-ski contact");
            return;
        }

        // 3) Landing / slam: if velocity is strongly into slope while still moving.
        if (!_wasGrounded && _isGrounded)
        {
            float vInto = -Vector3.Dot(_rb.linearVelocity, _groundNormal);
            if (vInto > maxVelocityIntoSlope)
            {
                Stack("Impact into slope");
                return;
            }

            // Also consider fall height (optional, robust).
            float fallH = Mathf.Max(0f, _airbornePeakY - transform.position.y);
            if (fallH > 6f && vInto > (maxVelocityIntoSlope * 0.75f))
            {
                Stack("Hard landing");
                return;
            }
        }
    }

    private void Stack(string reason)
    {
        _mode = Mode.Stacked;

        // Simple stack outcome: damp velocity and let physics take over.
        Vector3 v = _rb.linearVelocity;
        v *= 0.35f;
        _rb.linearVelocity = v;

        // Small random yaw nudges read well visually.
        transform.rotation = Quaternion.AngleAxis(Random.Range(-25f, 25f), _groundNormal) * transform.rotation;
    }

    private void TryAutoRecover()
    {
        // Recover when mostly upright and slow.
        float tilt = Vector3.Angle(transform.up, _groundNormal);
        float speed = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal).magnitude;

        if (tilt < (maxSafeTiltAngle * 0.65f) && speed < 2.0f)
        {
            _mode = IsGroundedForControls() ? Mode.Ground : Mode.Air;
        }
    }


    // ---------------------------------------------------------------------
    // Visuals
    // ---------------------------------------------------------------------

    private void CacheSkiVisualBases()
    {
        if (leftSki != null)
        {
            _leftSkiBaseLocalPos = leftSki.localPosition;
            _leftSkiBaseLocalRot = leftSki.localRotation;
        }

        if (rightSki != null)
        {
            _rightSkiBaseLocalPos = rightSki.localPosition;
            _rightSkiBaseLocalRot = rightSki.localRotation;
        }
    }

    private void UpdateVisuals()
    {
        float dt = Time.deltaTime;

        // Visual stance offsets.
        Vector3 leftTargetOffset = Vector3.left * (maxSkiOffset * _leftOut);
        Vector3 rightTargetOffset = Vector3.right * (maxSkiOffset * _rightOut);

        float yawTargetLeft = -maxSkiEdgeAngle * _leftOut;
        float yawTargetRight = maxSkiEdgeAngle * _rightOut;

        _leftSkiOffsetCur = Vector3.Lerp(_leftSkiOffsetCur, leftTargetOffset, 1f - Mathf.Exp(-skiVisualLerpSpeed * dt));
        _rightSkiOffsetCur = Vector3.Lerp(_rightSkiOffsetCur, rightTargetOffset, 1f - Mathf.Exp(-skiVisualLerpSpeed * dt));

        _leftSkiYawCur = Mathf.Lerp(_leftSkiYawCur, yawTargetLeft, 1f - Mathf.Exp(-skiVisualLerpSpeed * dt));
        _rightSkiYawCur = Mathf.Lerp(_rightSkiYawCur, yawTargetRight, 1f - Mathf.Exp(-skiVisualLerpSpeed * dt));

        if (leftSki != null)
        {
            leftSki.localPosition = _leftSkiBaseLocalPos + _leftSkiOffsetCur;
            leftSki.localRotation = _leftSkiBaseLocalRot * Quaternion.Euler(0f, _leftSkiYawCur, 0f);
        }

        if (rightSki != null)
        {
            rightSki.localPosition = _rightSkiBaseLocalPos + _rightSkiOffsetCur;
            rightSki.localRotation = _rightSkiBaseLocalRot * Quaternion.Euler(0f, _rightSkiYawCur, 0f);
        }

        // Body lean (visual only).
        if (bodyTransform != null)
        {
            float pitch = -_lean * maxForwardLeanAngle;

            // Side lean from leg difference (visual only). This is intentionally not used in physics.
            float side = Mathf.Clamp((_rightOut - _leftOut), -1f, 1f);
            float roll = side * maxSideLeanAngle;

            Quaternion target = Quaternion.Euler(pitch, 0f, roll);
            bodyTransform.localRotation = Quaternion.Slerp(bodyTransform.localRotation, target, 1f - Mathf.Exp(-forwardLeanLerpSpeed * dt));
        }
    }


    // ---------------------------------------------------------------------
    // Utility
    // ---------------------------------------------------------------------

    private void CacheSkiColliders()
    {
        _skiColliders.Clear();
        if (leftSki != null)
        {
            foreach (var c in leftSki.GetComponentsInChildren<Collider>(true))
                _skiColliders.Add(c);
        }
        if (rightSki != null)
        {
            foreach (var c in rightSki.GetComponentsInChildren<Collider>(true))
                _skiColliders.Add(c);
        }
    }
}
