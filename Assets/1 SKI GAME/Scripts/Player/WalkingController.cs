using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Minimal walk controller + ski toggle:
/// - Hold Player/Interact for toggleHoldDuration to switch between ski & walk mode.
/// - In walk mode: simple walk/run movement.
/// - In ski mode: movement comes from SkiController only.
/// - Skis & poles are reparented to "back" parents in walk mode, and restored to
///   their original parents/poses in ski mode.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class WalkingController : MonoBehaviour
{
    // ----------------------------------------------------------------------
    // REFERENCES
    // ----------------------------------------------------------------------

    [Header("References")]
    [Tooltip("SkiController on the same player. Will be enabled/disabled when switching modes.")]
    [SerializeField] private SkiController skiController;

    [Tooltip("Optional limb visual controller. Used to clear stale stack limb endpoints when switching to walk mode from a stack.")]
    [SerializeField] private SkierLimbLineVisual skierLimbLineVisual;

    [Tooltip("Optional SkiContact scripts for each ski (will be disabled in walk mode).")]
    [SerializeField] private SkiContact leftSkiContact;
    [SerializeField] private SkiContact rightSkiContact;

    [Tooltip("Optional PoleContact scripts for each pole (will be disabled in walk mode).")]
    [SerializeField] private PoleContact leftPoleContact;
    [SerializeField] private PoleContact rightPoleContact;

    [Header("Ski & Pole Transforms")]
    [Tooltip("Visual transform of the left ski.")]
    [SerializeField] private Transform leftSki;
    [Tooltip("Visual transform of the right ski.")]
    [SerializeField] private Transform rightSki;

    [Tooltip("Root/pivot transform for the left pole model (usually poleRoot from PoleContact).")]
    [SerializeField] private Transform leftPoleRoot;
    [Tooltip("Root/pivot transform for the right pole model.")]
    [SerializeField] private Transform rightPoleRoot;

    [Header("On-Back Parents")]
    [Tooltip("Empty GameObject where the left ski should live when 'on back'.")]
    [SerializeField] private Transform leftSkiBackParent;
    [Tooltip("Empty GameObject where the right ski should live when 'on back'.")]
    [SerializeField] private Transform rightSkiBackParent;

    [Tooltip("Empty GameObject where the left pole should live when 'on back'.")]
    [SerializeField] private Transform leftPoleBackParent;
    [Tooltip("Empty GameObject where the right pole should live when 'on back'.")]
    [SerializeField] private Transform rightPoleBackParent;

    [Tooltip("Optional camera transform used for camera-relative walking. If null, uses player forward/right.")]
    [SerializeField] private Transform cameraTransform;

    // ----------------------------------------------------------------------
    // INPUT
    // ----------------------------------------------------------------------

    [Header("Input (New Input System)")]
    [Tooltip("Hold this action for 'toggleHoldDuration' seconds to take off / put on skis (Player/Interact).")]
    [SerializeField] private float toggleHoldDuration = 1.0f;

    private InputSystem_Actions _input;
    private InputSystem_Actions.PlayerActions _player;

    // ----------------------------------------------------------------------
    // WALKING MOVEMENT SETTINGS
    // ----------------------------------------------------------------------

    [Header("Movement")]
    [Tooltip("Maximum walk speed (m/s).")]
    [SerializeField] private float walkSpeed = 3f;

    [Tooltip("Maximum run/sprint speed (m/s).")]
    [SerializeField] private float runSpeed = 6f;

    [Tooltip("Horizontal acceleration (m/s^2).")]
    [SerializeField] private float acceleration = 25f;

    [Tooltip("Horizontal deceleration when no input (m/s^2).")]
    [SerializeField] private float deceleration = 30f;

    [Header("Air Control")]
    [Tooltip("Multiplier applied to walking/running max speed while airborne.")]
    [SerializeField, Range(0f, 1f)] private float airControlSpeedMultiplier = 0.65f;

    [Tooltip("Multiplier applied to horizontal acceleration while airborne.")]
    [SerializeField, Range(0f, 1f)] private float airControlAccelerationMultiplier = 0.35f;

    [Tooltip("If true, horizontal input can rotate the player while airborne, but at a softer rate than grounded walking.")]
    [SerializeField] private bool allowAirTurn = true;

    [Tooltip("Fraction of grounded horizontal control that remains while airborne in walk mode.")]
    [SerializeField, Range(0f, 1f)] private float airControlPercent = 0.35f;

    [Tooltip("Rotation follow speed while airborne.")]
    [SerializeField] private float airTurnSpeed = 3.5f;

    [Header("Jump")]
    [Tooltip("Minimum upward jump speed for a quick tap in walk mode.")]
    [SerializeField] private float minJumpForce = 3.2f;

    [Tooltip("Maximum upward jump speed after a full hold/charge in walk mode.")]
    [SerializeField] private float jumpForce = 6.5f;

    [Tooltip("Seconds required to reach full walk-jump charge.")]
    [SerializeField] private float maxJumpChargeTime = 0.45f;

    [Tooltip("How long a released jump remains buffered while waiting for valid ground.")]
    [SerializeField] private float jumpBufferTime = 0.16f;

    [Tooltip("How long after leaving ground a walking jump is still accepted.")]
    [SerializeField] private float jumpCoyoteTime = 0.12f;

    [Tooltip("How long the limb visual should hold the post-release stretch pose.")]
    [SerializeField] private float jumpReleaseVisualDuration = 0.18f;

    [Tooltip("How long after a walk jump launch to ignore walk grounded checks. Prevents the character from being immediately re-grounded.")]
    [SerializeField] private float jumpGroundIgnoreDuration = 0.12f;

    [Tooltip("Small upward position lift applied at jump launch to break sticky ground contact.")]
    [SerializeField] private float jumpLaunchSeparation = 0.04f;

    [Tooltip("Distance below the player to check for ground when walking.")]
    [SerializeField] private float groundCheckDistance = 0.75f;

    [Tooltip("Radius of the ground check sphere.")]
    [SerializeField] private float groundCheckRadius = 0.3f;

    [Tooltip("Minimum upward normal dot required for walk-mode ground. Prevents walls/self hits being treated as valid ground.")]
    [SerializeField, Range(0f, 1f)] private float walkGroundMinUpDot = 0.25f;

    [Tooltip("Logs exactly why a walk jump did or did not launch.")]
    [SerializeField] private bool logWalkJumpDebug = false;

    [Header("Landing Animation")]
    [Tooltip("Minimum airborne time before landing animation can trigger. Prevents tiny ground flickers from causing crouches.")]
    [SerializeField] private float landingMinAirTime = 0.12f;

    [Tooltip("How long the landing crouch animation lasts.")]
    [SerializeField] private float landingVisualDuration = 0.22f;

    [Tooltip("Downward speed where landing animation starts becoming visible.")]
    [SerializeField] private float landingMinDownwardSpeed = 2.0f;

    [Tooltip("Downward speed that produces maximum landing animation intensity.")]
    [SerializeField] private float landingMaxDownwardSpeed = 11.0f;

    [Tooltip("Fall distance where landing animation starts becoming visible.")]
    [SerializeField] private float landingMinFallDistance = 0.35f;

    [Tooltip("Fall distance that produces maximum landing animation intensity.")]
    [SerializeField] private float landingMaxFallDistance = 4.5f;

    [Tooltip("Minimum intensity required before the landing animation fires.")]
    [SerializeField, Range(0f, 1f)] private float landingMinVisibleIntensity = 0.08f;

    [Tooltip("Minimum pose strength used once a walk landing is confirmed. This keeps small valid landings visible.")]
    [SerializeField, Range(0f, 1f)] private float landingMinimumPoseIntensity = 0.16f;

    [Tooltip("Downward speed required before walk landing is allowed to complete. Prevents early ground reacquisition during jump ascent.")]
    [SerializeField] private float landingDescentConfirmSpeed = 0.25f;

    [Tooltip("Height drop from airborne peak required to confirm descent even if velocity is noisy.")]
    [SerializeField] private float landingDescentConfirmDistance = 0.06f;

    [Tooltip("Logs walk landing intensity diagnostics.")]
    [SerializeField] private bool logWalkLandingDebug = false;

    [Header("Ski Re-Equip")]
    [Tooltip("Layers to consider as ground when nudging player up for skis.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Minimum clearance above the ground when putting skis back on.")]
    [SerializeField] private float skiGroundClearance = 0.05f;

    [Tooltip("Ray start height above the player when nudging up for skis.")]
    [SerializeField] private float skiGroundRayHeight = 2f;

    [Tooltip("Maximum raycast distance down when nudging up for skis.")]
    [SerializeField] private float skiGroundRayDistance = 5f;

    [Header("Walk Ground Alignment")]
    [Tooltip("When entering walk mode, lift the player root if the authored walk feet would be below the terrain.")]
    [SerializeField] private bool alignWalkFeetToGroundOnEnter = true;

    [Tooltip("Small clearance to keep walk foot targets just above the terrain after switching from stack/ski mode.")]
    [SerializeField] private float walkFootGroundClearance = 0.035f;

    [Tooltip("Ray start height above each walk foot target when solving walk ground clearance.")]
    [SerializeField] private float walkFootGroundRayHeight = 1.25f;

    [Tooltip("Maximum raycast distance below each walk foot target when solving walk ground clearance.")]
    [SerializeField] private float walkFootGroundRayDistance = 3.0f;

    [Tooltip("Number of FixedUpdate frames after entering walk mode to keep correcting foot-ground alignment. This catches one-frame visual/parenting updates.")]
    [SerializeField] private int walkGroundSnapFramesAfterModeChange = 3;

    [Tooltip("Draws walk foot ground snap probes while this object is selected.")]
    [SerializeField] private bool drawWalkFootGroundSnapGizmos = true;

    // ----------------------------------------------------------------------
    // INTERNAL STATE
    // ----------------------------------------------------------------------

    private Rigidbody _rb;

    // Mode flag
    private bool _skisOn = true;

    /// <summary>
    /// True when the player is currently in ski mode (SkiController-driven).
    /// Exposed for VFX/audio systems that need to suppress ski-specific effects while walking.
    /// </summary>
    public bool SkisOn => _skisOn;

    /// <summary>
    /// True when the player is currently walking (WalkingController-driven).
    /// </summary>
    public bool IsWalkingMode => !_skisOn;

    // Toggle/hold state
    private bool _toggleHeld;
    private bool _toggleTriggeredThisHold;
    private float _toggleHoldStartTime;

    // Original parents / transforms for skis & poles
    private Transform _leftSkiOriginalParent;
    private Transform _rightSkiOriginalParent;
    private Transform _leftPoleOriginalParent;
    private Transform _rightPoleOriginalParent;

    private Vector3 _leftSkiOriginalLocalPos;
    private Vector3 _rightSkiOriginalLocalPos;
    private Vector3 _leftPoleOriginalLocalPos;
    private Vector3 _rightPoleOriginalLocalPos;

    private Quaternion _leftSkiOriginalLocalRot;
    private Quaternion _rightSkiOriginalLocalRot;
    private Quaternion _leftPoleOriginalLocalRot;
    private Quaternion _rightPoleOriginalLocalRot;

    // Grounded / jump state (walk mode only)
    private bool _isGrounded;
    private bool _jumpQueued;
    private bool _jumpCharging;
    private bool _jumpReleaseQueued;

    private float _lastGroundedTime = -999f;
    private float _lastJumpPressedTime = -999f;
    private float _lastJumpReleasedTime = -999f;
    private float _lastJumpExecutedTime = -999f;
    private float _lastJumpCharge01;
    private float _ignoreWalkGroundingUntilTime = -999f;
    private float _ignoreWalkGroundSnapUntilTime = -999f;
    private float _walkCollisionGroundedUntilTime = -999f;
    private Vector3 _lastWalkGroundNormal = Vector3.up;

    // Landing animation state
    private bool _airborneTrackingActive;
    private bool _suppressNextWalkLanding;
    private bool _walkAirborneSawDescent;

    private float _airborneStartTime = -999f;
    private float _airborneStartY;
    private float _highestAirborneY;
    private float _maxDownwardSpeedWhileAirborne;
    private float _lastLandingTime = -999f;
    private float _lastLandingIntensity01;

    // --- Raw user input capture (ignores external auto-walk overrides) ---
    private Vector2 _lastUserMoveRaw;
    private bool _lastUserSprintRaw;

    private bool _runtimeSetupComplete;
    private readonly Dictionary<Collider, bool> _equipmentColliderEnabledStates = new Dictionary<Collider, bool>();

    private bool _walkPresentationKeepsSkisEquipped;
    private int _pendingWalkGroundSnapFrames;

    private bool _riderPoseActive;
    private float _walkMoveBlend01;
    private float _walkRunBlend01;
    private float _walkAirMoveBlend01;

    public Vector2 LastUserMoveRaw => _lastUserMoveRaw;
    public float LastUserMoveMagnitude => _lastUserMoveRaw.magnitude;
    public bool LastUserSprintRaw => _lastUserSprintRaw;

    public bool IsWalkGrounded => !_skisOn && _isGrounded;
    public bool IsWalkAirborne => !_skisOn && !_isGrounded && !_riderPoseActive;
    public bool IsWalkJumpCharging => !_skisOn && _jumpCharging;
    public bool IsRiderPoseActive => _riderPoseActive;

    public float WalkMoveBlend01 => _walkMoveBlend01;
    public float WalkRunBlend01 => _walkRunBlend01;
    public float WalkAirMoveBlend01 => _walkAirMoveBlend01;

    public Vector3 WalkVelocity
    {
        get
        {
            if (_skisOn || _rb == null)
                return Vector3.zero;

            return _rb.linearVelocity;
        }
    }

    public float WalkJumpCharge01
    {
        get
        {
            if (!_jumpCharging || _skisOn)
                return 0f;

            return maxJumpChargeTime > 0f
                ? Mathf.Clamp01((Time.time - _lastJumpPressedTime) / maxJumpChargeTime)
                : 1f;
        }
    }

    public float WalkJumpReleaseVisual01
    {
        get
        {
            float duration = Mathf.Max(0.01f, jumpReleaseVisualDuration);
            float age = Time.time - _lastJumpExecutedTime;
            if (age < 0f || age > duration)
                return 0f;

            return Mathf.Clamp01(1f - age / duration) * Mathf.Clamp01(_lastJumpCharge01);
        }
    }

    public float WalkLandingIntensity01 => _lastLandingIntensity01;

    public float WalkLandingPose01
    {
        get
        {
            float duration = Mathf.Max(0.01f, landingVisualDuration);
            float age = Time.time - _lastLandingTime;

            if (age < 0f || age > duration)
                return 0f;

            float normalizedAge = Mathf.Clamp01(age / duration);

            // Fast attack, smooth decay. The landing should feel like a quick compress-and-recover.
            float decay = 1f - Mathf.SmoothStep(0f, 1f, normalizedAge);

            return Mathf.Clamp01(_lastLandingIntensity01 * decay);
        }
    }

    /// <summary>
    /// Returns true if the player is currently providing movement input (raw input, not external override).
    /// </summary>
    public bool IsUserTryingToMove(float threshold = 0.15f)
    {
        return _lastUserMoveRaw.sqrMagnitude >= (threshold * threshold);
    }

    [Header("Runtime Overrides")]
    [SerializeField] private bool controlsEnabled = true;

    private bool _externalMoveActive;
    private Vector2 _externalMove;
    private bool _externalSprint;
    private bool _externalWorldMoveActive;
    private Vector3 _externalWorldMoveDirection;
    private float _externalWorldMoveStrength = 1f;
    private float _externalWorldMoveFacingGateDot = -1f;

    public bool ControlsEnabled
    {
        get => controlsEnabled;
        set
        {
            controlsEnabled = value;

            if (!controlsEnabled)
            {
                ClearExternalMove();
                ClearWalkInputVisualState();
                ClearWalkJumpState(clearReleaseVisual: false);
            }
        }
    }

    public bool ExternalMoveActive => _externalMoveActive;

    public void SetExternalMove(Vector2 move01, bool sprint = false)
    {
        _externalMoveActive = true;
        _externalWorldMoveActive = false;
        _externalMove = Vector2.ClampMagnitude(move01, 1f);
        _externalSprint = sprint;
    }

    public void SetExternalMoveToward(Vector3 worldDirection, float strength01 = 1f, bool sprint = false, float facingGateDot = 0.35f)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            ClearExternalMove();
            return;
        }

        _externalMoveActive = false;
        _externalWorldMoveActive = true;
        _externalWorldMoveDirection = planarDirection.normalized;
        _externalWorldMoveStrength = Mathf.Clamp01(strength01);
        _externalWorldMoveFacingGateDot = Mathf.Clamp(facingGateDot, -1f, 0.999f);
        _externalSprint = sprint;
    }

    public void ClearExternalMove()
    {
        _externalMoveActive = false;
        _externalWorldMoveActive = false;
        _externalMove = Vector2.zero;
        _externalWorldMoveDirection = Vector3.zero;
        _externalWorldMoveStrength = 1f;
        _externalWorldMoveFacingGateDot = -1f;
        _externalSprint = false;
    }

    public void SetRiderPoseActive(bool active)
    {
        _riderPoseActive = active;

        if (skierLimbLineVisual != null)
            skierLimbLineVisual.SetSeatedPoseOverride(active);

        if (active)
        {
            ClearExternalMove();
            ClearWalkInputVisualState();
            ClearWalkJumpState(clearReleaseVisual: true);
        }
    }

    private void ClearWalkInputVisualState()
    {
        _lastUserMoveRaw = Vector2.zero;
        _lastUserSprintRaw = false;
        _walkMoveBlend01 = 0f;
        _walkRunBlend01 = 0f;
        _walkAirMoveBlend01 = 0f;
    }

    private void ClearWalkJumpState(bool clearReleaseVisual)
    {
        _jumpQueued = false;
        _jumpCharging = false;
        _jumpReleaseQueued = false;
        _lastJumpPressedTime = -999f;
        _lastJumpReleasedTime = -999f;

        // Keep the last charge value after an actual launch so WalkJumpReleaseVisual01
        // can still drive the release/stretch pose.
        if (clearReleaseVisual)
        {
            _lastJumpCharge01 = 0f;
            _lastJumpExecutedTime = -999f;
        }
    }

    private void ResetWalkLandingTracking(bool suppressNextLanding)
    {
        _airborneTrackingActive = false;
        _suppressNextWalkLanding = suppressNextLanding;
        _walkAirborneSawDescent = false;

        _airborneStartTime = -999f;
        _airborneStartY = transform.position.y;
        _highestAirborneY = transform.position.y;
        _maxDownwardSpeedWhileAirborne = 0f;
        _lastLandingIntensity01 = 0f;
    }

    private void BeginWalkAirborneTracking(bool allowLandingAnimation)
    {
        float y = _rb != null ? _rb.position.y : transform.position.y;
        float verticalVelocity = _rb != null ? _rb.linearVelocity.y : 0f;
        float downwardSpeed = Mathf.Max(0f, -verticalVelocity);

        _airborneTrackingActive = true;
        _airborneStartTime = Time.time;
        _airborneStartY = y;
        _highestAirborneY = y;
        _maxDownwardSpeedWhileAirborne = downwardSpeed;

        _walkAirborneSawDescent =
            downwardSpeed >= Mathf.Max(0.01f, landingDescentConfirmSpeed);

        if (allowLandingAnimation)
            _suppressNextWalkLanding = false;
    }

    private void UpdateWalkAirborneMetrics(float currentY, float downwardSpeed)
    {
        if (!_airborneTrackingActive)
            return;

        _highestAirborneY = Mathf.Max(_highestAirborneY, currentY);
        _maxDownwardSpeedWhileAirborne = Mathf.Max(_maxDownwardSpeedWhileAirborne, downwardSpeed);

        if (downwardSpeed >= Mathf.Max(0.01f, landingDescentConfirmSpeed))
            _walkAirborneSawDescent = true;

        if (_highestAirborneY - currentY >= Mathf.Max(0.001f, landingDescentConfirmDistance))
            _walkAirborneSawDescent = true;
    }

    private bool ShouldSuppressPrematureWalkGrounding(float currentY, float verticalVelocity)
    {
        if (!_airborneTrackingActive)
            return false;

        if (_walkAirborneSawDescent)
            return false;

        float downwardSpeed = Mathf.Max(0f, -verticalVelocity);
        float dropFromPeak = Mathf.Max(0f, _highestAirborneY - currentY);

        // The current bug is exactly this case:
        // ground reacquires after jump ignore expires, but the body has not actually
        // fallen yet, so this is not a landing.
        return downwardSpeed < Mathf.Max(0.01f, landingDescentConfirmSpeed) &&
               dropFromPeak < Mathf.Max(0.001f, landingDescentConfirmDistance);
    }

    private void UpdateWalkLandingTracking(bool wasGrounded, bool isGroundedNow)
    {
        if (_skisOn || _riderPoseActive)
        {
            ResetWalkLandingTracking(suppressNextLanding: true);
            return;
        }

        float currentY = _rb != null ? _rb.position.y : transform.position.y;
        float verticalVelocity = _rb != null ? _rb.linearVelocity.y : 0f;
        float downwardSpeed = Mathf.Max(0f, -verticalVelocity);

        // Stable grounded frame with no active airborne tracking.
        if (isGroundedNow && !_airborneTrackingActive)
        {
            _suppressNextWalkLanding = false;
            return;
        }

        if (!isGroundedNow)
        {
            if (!_airborneTrackingActive)
                BeginWalkAirborneTracking(allowLandingAnimation: true);

            UpdateWalkAirborneMetrics(currentY, downwardSpeed);
            return;
        }

        if (!_airborneTrackingActive)
            return;

        // Update one final time on the grounded candidate frame.
        // Velocity is often already solved low/zero by this point, but this still
        // captures drop-from-peak and confirms the landing state.
        UpdateWalkAirborneMetrics(currentY, downwardSpeed);

        float airTime = Mathf.Max(0f, Time.time - _airborneStartTime);
        float fallDistance = Mathf.Max(0f, _highestAirborneY - currentY);

        bool hasConfirmedDescent =
            _walkAirborneSawDescent ||
            fallDistance >= Mathf.Max(0.001f, landingDescentConfirmDistance) ||
            _maxDownwardSpeedWhileAirborne >= Mathf.Max(0.01f, landingDescentConfirmSpeed) ||
            downwardSpeed >= Mathf.Max(0.01f, landingDescentConfirmSpeed);

        if (!wasGrounded && isGroundedNow && !hasConfirmedDescent)
        {
            if (logWalkLandingDebug)
            {
                Debug.Log(
                    $"[{nameof(WalkingController)}] Ignored premature walk landing candidate. " +
                    $"airTime={airTime:0.00}, fallDistance={fallDistance:0.00}, " +
                    $"velY={verticalVelocity:0.00}, maxDownSpeed={_maxDownwardSpeedWhileAirborne:0.00}",
                    this);
            }

            _isGrounded = false;
            return;
        }

        if (!wasGrounded && isGroundedNow)
        {
            float speed01 = Mathf.InverseLerp(
                landingMinDownwardSpeed,
                Mathf.Max(landingMinDownwardSpeed + 0.01f, landingMaxDownwardSpeed),
                Mathf.Max(_maxDownwardSpeedWhileAirborne, downwardSpeed));

            float distance01 = Mathf.InverseLerp(
                landingMinFallDistance,
                Mathf.Max(landingMinFallDistance + 0.01f, landingMaxFallDistance),
                fallDistance);

            float rawIntensity01 = Mathf.Clamp01(Mathf.Max(speed01, distance01));

            bool timingValid = airTime >= Mathf.Max(0f, landingMinAirTime);

            // Confirmed landings should fire even if the normalized intensity is tiny.
            // The previous version rejected valid walk landings because ordinary walk-jump
            // impacts normalize to 0.02-0.03 against the hard-landing max values.
            bool shouldFire =
                !_suppressNextWalkLanding &&
                timingValid &&
                hasConfirmedDescent;

            if (shouldFire)
            {
                float outputIntensity01 = Mathf.Clamp01(Mathf.Max(
                    rawIntensity01,
                    landingMinimumPoseIntensity,
                    landingMinVisibleIntensity));

                _lastLandingIntensity01 = outputIntensity01;
                _lastLandingTime = Time.time;

                if (logWalkLandingDebug)
                {
                    Debug.Log(
                        $"[{nameof(WalkingController)}] Walk landing animation fired. " +
                        $"airTime={airTime:0.00}, fallDistance={fallDistance:0.00}, " +
                        $"maxDownSpeed={_maxDownwardSpeedWhileAirborne:0.00}, " +
                        $"currentDownSpeed={downwardSpeed:0.00}, rawIntensity={rawIntensity01:0.00}, " +
                        $"outputIntensity={outputIntensity01:0.00}",
                        this);
                }
            }
            else if (logWalkLandingDebug)
            {
                Debug.Log(
                    $"[{nameof(WalkingController)}] Walk landing ignored. " +
                    $"suppress={_suppressNextWalkLanding}, timingValid={timingValid}, " +
                    $"confirmedDescent={hasConfirmedDescent}, sawDescent={_walkAirborneSawDescent}, " +
                    $"airTime={airTime:0.00}, fallDistance={fallDistance:0.00}, " +
                    $"maxDownSpeed={_maxDownwardSpeedWhileAirborne:0.00}, " +
                    $"currentDownSpeed={downwardSpeed:0.00}, rawIntensity={rawIntensity01:0.00}",
                    this);
            }
        }

        _airborneTrackingActive = false;
        _suppressNextWalkLanding = false;
        _walkAirborneSawDescent = false;
    }

    private void PrepareRigidbodyForWalkPhysics()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        if (_rb == null)
            return;

        bool changed = false;

        if (_rb.isKinematic)
        {
            _rb.isKinematic = false;
            changed = true;
        }

        if (!_rb.detectCollisions)
        {
            _rb.detectCollisions = true;
            changed = true;
        }

        if (!_rb.useGravity)
        {
            _rb.useGravity = true;
            changed = true;
        }

        RigidbodyConstraints constraints = _rb.constraints;

        if ((constraints & RigidbodyConstraints.FreezePositionY) != 0)
        {
            constraints &= ~RigidbodyConstraints.FreezePositionY;
            _rb.constraints = constraints;
            changed = true;
        }

        _rb.WakeUp();

        if (changed && logWalkJumpDebug)
        {
            Debug.Log(
                $"[{nameof(WalkingController)}] Prepared Rigidbody for walk physics. " +
                $"isKinematic={_rb.isKinematic}, detectCollisions={_rb.detectCollisions}, " +
                $"useGravity={_rb.useGravity}, constraints={_rb.constraints}",
                this);
        }
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundLayers.value & (1 << layer)) != 0;
    }

    private bool IsOwnCollider(Collider c)
    {
        return c != null && c.transform != null && c.transform.IsChildOf(transform);
    }

    private bool IsValidWalkGroundNormal(Vector3 normal)
    {
        if (normal.sqrMagnitude <= 0.0001f)
            return false;

        return Vector3.Dot(normal.normalized, Vector3.up) >= walkGroundMinUpDot;
    }

    private bool TryWalkGroundSphereCast(
        Vector3 origin,
        float radius,
        float distance,
        out RaycastHit bestHit)
    {
        bestHit = default;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, radius),
            Vector3.down,
            Mathf.Max(0.01f, distance),
            groundLayers,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            // This is important. The current ground mask defaults to Everything, so the old
            // SphereCast can treat player/equipment/body colliders as ground.
            if (IsOwnCollider(hit.collider))
                continue;

            Vector3 normal = hit.normal.sqrMagnitude > 0.0001f
                ? hit.normal.normalized
                : Vector3.up;

            if (!IsValidWalkGroundNormal(normal))
                continue;

            if (hit.distance < bestDistance)
            {
                found = true;
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        return found;
    }

    public void ForceEnterWalkMode()
    {
        EnsureRuntimeSetup();

        if (_skisOn)
            EnterWalkMode();
        else
            RefreshEquipmentPresentation();
    }

    public void ForceEnterSkiMode()
    {
        EnsureRuntimeSetup();

        SetRiderPoseActive(false);
        ClearWalkJumpState(clearReleaseVisual: true);

        if (!_skisOn)
            EnterSkiMode();
        else
            RefreshEquipmentPresentation();
    }

    public void SetWalkPresentationKeepsSkisEquipped(bool keepSkisEquipped)
    {
        EnsureRuntimeSetup();
        _walkPresentationKeepsSkisEquipped = keepSkisEquipped;
        RefreshEquipmentPresentation();
    }

    public void GetMoveBasis(out Vector3 forward, out Vector3 right)
    {
        if (cameraTransform != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (camForward.sqrMagnitude < 0.0001f) camForward = transform.forward;
            camForward.Normalize();

            Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up);
            if (camRight.sqrMagnitude < 0.0001f) camRight = transform.right;
            camRight.Normalize();

            forward = camForward;
            right = camRight;
        }
        else
        {
            forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
        }
    }

    // ----------------------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------------------

    private void Awake()
    {
        EnsureRuntimeSetup();
        ApplyModeInitial();
    }

    private void EnsureRuntimeSetup()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        if (skierLimbLineVisual == null)
            skierLimbLineVisual = GetComponentInChildren<SkierLimbLineVisual>(true);

        if (_input == null)
        {
            _input = new InputSystem_Actions();
            _player = _input.Player;

        }
        if (_runtimeSetupComplete)
            return;

        if (leftSki != null)
        {
            _leftSkiOriginalParent = leftSki.parent;
            _leftSkiOriginalLocalPos = leftSki.localPosition;
            _leftSkiOriginalLocalRot = leftSki.localRotation;
        }

        if (rightSki != null)
        {
            _rightSkiOriginalParent = rightSki.parent;
            _rightSkiOriginalLocalPos = rightSki.localPosition;
            _rightSkiOriginalLocalRot = rightSki.localRotation;
        }

        if (leftPoleRoot != null)
        {
            _leftPoleOriginalParent = leftPoleRoot.parent;
            _leftPoleOriginalLocalPos = leftPoleRoot.localPosition;
            _leftPoleOriginalLocalRot = leftPoleRoot.localRotation;
        }

        if (rightPoleRoot != null)
        {
            _rightPoleOriginalParent = rightPoleRoot.parent;
            _rightPoleOriginalLocalPos = rightPoleRoot.localPosition;
            _rightPoleOriginalLocalRot = rightPoleRoot.localRotation;
        }

        _skisOn = skiController == null || skiController.enabled;
        _runtimeSetupComplete = true;
    }

    private void OnEnable()
    {
        EnsureRuntimeSetup();
        _input.Enable();
    }

    private void OnDisable()
    {
        if (_input != null)
            _input.Disable();
    }

    private void Update()
    {
        if (!controlsEnabled)
            return;

        HandleToggleInput();
        HandleJumpInput();
    }

    private void FixedUpdate()
    {
        if (!controlsEnabled)
            return;

        if (!_skisOn)
        {
            PrepareRigidbodyForWalkPhysics();

            if (_pendingWalkGroundSnapFrames > 0)
            {
                NudgeUpForWalkFeet(resetDownwardVelocity: true);
                _pendingWalkGroundSnapFrames--;
            }

            bool wasGrounded = _isGrounded;

            UpdateGroundedState();
            TryProcessJump();

            // Must run after TryProcessJump(), because TryProcessJump() is the method
            // that actually flips _isGrounded false on successful jump launch.
            UpdateWalkLandingTracking(wasGrounded, _isGrounded);

            ApplyWalkMovement();
        }
    }

    // ----------------------------------------------------------------------
    // MODE TOGGLING
    // ----------------------------------------------------------------------

    private void HandleToggleInput()
    {
        // We use Player/Interact as the "take off / put on skis" hold action.
        bool interactPressed = _player.EquipSkis.IsPressed();

        if (interactPressed)
        {
            if (!_toggleHeld)
            {
                _toggleHeld = true;
                _toggleTriggeredThisHold = false;
                _toggleHoldStartTime = Time.time;
            }

            if (!_toggleTriggeredThisHold &&
                Time.time - _toggleHoldStartTime >= toggleHoldDuration)
            {
                ToggleSkisMode();
                _toggleTriggeredThisHold = true;
            }
        }
        else
        {
            _toggleHeld = false;
            _toggleTriggeredThisHold = false;
        }
    }

    private void ToggleSkisMode()
    {
        _walkPresentationKeepsSkisEquipped = false;
        SetRiderPoseActive(false);
        ClearWalkJumpState(clearReleaseVisual: true);

        if (_skisOn)
            EnterWalkMode();
        else
            EnterSkiMode();
    }

    private void ApplyModeInitial()
    {
        if (_skisOn)
        {
            NudgeUpForSkis();
            EnableSkiSystems(true);
        }
        else
        {
            PrepareRigidbodyForWalkPhysics();
            EnableSkiSystems(false);
        }

        RefreshEquipmentPresentation();
    }

    private void EnterWalkMode()
    {
        bool wasStacked = skiController != null && skiController.IsStacked;

        if (skiController != null)
        {
            skiController.ResetStackStateSilently(snapUpright: true);
            skiController.ReleaseStackSkiVisualOverridesAndSnap(snapToNeutralPose: true);
            skiController.ResetVisualPoseForWalkModeHandoff();
        }

        if (leftPoleContact != null)
            leftPoleContact.ClearStackVisualOverride();

        if (rightPoleContact != null)
            rightPoleContact.ClearStackVisualOverride();

        PrepareRigidbodyForWalkPhysics();

        _skisOn = false;
        ClearWalkJumpState(clearReleaseVisual: true);
        ResetWalkLandingTracking(suppressNextLanding: true);

        Vector3 vel = _rb.linearVelocity;
        vel.x = 0f;
        vel.z = 0f;
        _rb.linearVelocity = vel;

        EnableSkiSystems(false);
        RefreshEquipmentPresentation();

        if (skierLimbLineVisual != null)
            skierLimbLineVisual.PrepareForWalkModeHandoff(wasStacked);

        _ignoreWalkGroundingUntilTime = -999f;
        _ignoreWalkGroundSnapUntilTime = -999f;
        _walkCollisionGroundedUntilTime = -999f;

        QueueWalkGroundSnap();
        NudgeUpForWalkFeet(resetDownwardVelocity: true);
    }

    private void EnterSkiMode()
    {
        _skisOn = true;
        _walkPresentationKeepsSkisEquipped = false;

        SetRiderPoseActive(false);
        ClearWalkInputVisualState();
        ClearWalkJumpState(clearReleaseVisual: true);

        if (_rb.useGravity == true) _rb.useGravity = false;

        // Restore skis/poles to their real ski rig parents first so any
        // ground-clearance solve samples the actual ski positions, not the
        // "on back" presentation transforms.
        RefreshEquipmentPresentation();

        NudgeUpForSkis();

        EnableSkiSystems(true);

        if (skiController != null)
            skiController.SnapToGroundClearance(resetDownwardVelocity: true);
    }

    private void EnableSkiSystems(bool enabled)
    {
        SetEquipmentCollidersEnabled(enabled);

        if (skiController != null)
            skiController.enabled = enabled;

        if (leftSkiContact != null)
            leftSkiContact.enabled = enabled;

        if (rightSkiContact != null)
            rightSkiContact.enabled = enabled;

        if (leftPoleContact != null)
            leftPoleContact.enabled = enabled;

        if (rightPoleContact != null)
            rightPoleContact.enabled = enabled;
    }

    public void RefreshEquipmentPresentation()
    {
        EnsureRuntimeSetup();

        if (_skisOn)
        {
            ForceSkiPose();
            return;
        }

        if (_walkPresentationKeepsSkisEquipped)
            ForceSkiPose();
        else
            MoveSkisToBack();
    }

    private void SetEquipmentCollidersEnabled(bool enabled)
    {
        SetCollidersEnabledForRoot(leftSki, enabled);
        SetCollidersEnabledForRoot(rightSki, enabled);
        SetCollidersEnabledForRoot(leftPoleRoot, enabled);
        SetCollidersEnabledForRoot(rightPoleRoot, enabled);
    }

    private void SetCollidersEnabledForRoot(Transform root, bool enabled)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null)
                continue;

            if (!_equipmentColliderEnabledStates.ContainsKey(c))
                _equipmentColliderEnabledStates[c] = c.enabled;

            if (enabled)
            {
                if (_equipmentColliderEnabledStates.TryGetValue(c, out bool wasEnabled))
                    c.enabled = wasEnabled;
                else
                    c.enabled = true;
            }
            else
            {
                c.enabled = false;
            }
        }
    }

    // Gently nudge the player up so skis are not embedded in the ground.
    // This version considers BOTH ski positions (much more robust on slopes and during transitions).
    private void NudgeUpForSkis()
    {
        // If skis are missing, fall back to body-based ray.
        bool hasAnySki = (leftSki != null) || (rightSki != null);

        float requiredRootY = transform.position.y;
        bool foundGround = false;

        // Helper to compute a root Y that keeps a given point (ski) above ground + clearance.
        void ConsiderPoint(Transform t)
        {
            if (t == null) return;

            Vector3 rayOrigin = t.position + Vector3.up * skiGroundRayHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, skiGroundRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                foundGround = true;

                // We want: t.position.y >= hit.point.y + clearance
                // RootY must shift by the same delta as the ski point.
                float skiToRootOffsetY = t.position.y - transform.position.y;
                float desiredRootYForThisSki = (hit.point.y + skiGroundClearance) - skiToRootOffsetY;

                if (desiredRootYForThisSki > requiredRootY)
                    requiredRootY = desiredRootYForThisSki;
            }
        }

        if (hasAnySki)
        {
            ConsiderPoint(leftSki);
            ConsiderPoint(rightSki);
        }

        // Fallback: body-based ray if no skis or no hits.
        if (!foundGround)
        {
            Vector3 origin = transform.position + Vector3.up * skiGroundRayHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, skiGroundRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                requiredRootY = Mathf.Max(requiredRootY, hit.point.y + skiGroundClearance);
                foundGround = true;
            }
        }

        if (foundGround)
        {
            Vector3 pos = _rb != null ? _rb.position : transform.position;
            if (pos.y < requiredRootY)
            {
                pos.y = requiredRootY;

                if (_rb != null)
                    _rb.MovePosition(pos);
                else
                    transform.position = pos;
            }
        }
        else
        {
            // Last resort: small upward nudge.
            Vector3 pos = _rb != null ? _rb.position : transform.position;
            pos += Vector3.up * skiGroundClearance;

            if (_rb != null)
                _rb.MovePosition(pos);
            else
                transform.position = pos;
        }
    }

    private void QueueWalkGroundSnap()
    {
        if (!alignWalkFeetToGroundOnEnter)
            return;

        _pendingWalkGroundSnapFrames = Mathf.Max(_pendingWalkGroundSnapFrames, walkGroundSnapFramesAfterModeChange);
    }

    private void NudgeUpForWalkFeet(bool resetDownwardVelocity)
    {
        if (Time.time < _ignoreWalkGroundSnapUntilTime)
            return;

        if (!alignWalkFeetToGroundOnEnter)
            return;

        if (skierLimbLineVisual == null || !skierLimbLineVisual.HasValidWalkFootTargets())
            return;

        float requiredRootY = _rb != null ? _rb.position.y : transform.position.y;
        bool foundGround = false;

        ConsiderWalkFoot(skierLimbLineVisual.GetWalkFootTargetPosition(true), ref requiredRootY, ref foundGround);
        ConsiderWalkFoot(skierLimbLineVisual.GetWalkFootTargetPosition(false), ref requiredRootY, ref foundGround);

        if (!foundGround)
            return;

        Vector3 pos = _rb != null ? _rb.position : transform.position;
        if (pos.y >= requiredRootY - 0.001f)
            return;

        pos.y = requiredRootY;

        if (_rb != null)
        {
            _rb.position = pos;

            if (resetDownwardVelocity)
            {
                Vector3 v = _rb.linearVelocity;
                if (v.y < 0f)
                {
                    v.y = 0f;
                    _rb.linearVelocity = v;
                }
            }
        }
        else
        {
            transform.position = pos;
        }

        Physics.SyncTransforms();
    }

    private void ConsiderWalkFoot(Vector3 footWorldPosition, ref float requiredRootY, ref bool foundGround)
    {
        if (footWorldPosition == Vector3.zero)
            return;

        Vector3 rayOrigin = footWorldPosition + Vector3.up * Mathf.Max(0.01f, walkFootGroundRayHeight);
        float rayDistance = Mathf.Max(0.01f, walkFootGroundRayHeight + walkFootGroundRayDistance);

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        foundGround = true;

        float rootY = _rb != null ? _rb.position.y : transform.position.y;
        float footToRootOffsetY = footWorldPosition.y - rootY;
        float desiredRootYForFoot = (hit.point.y + Mathf.Max(0f, walkFootGroundClearance)) - footToRootOffsetY;

        if (desiredRootYForFoot > requiredRootY)
            requiredRootY = desiredRootYForFoot;
    }

    // ----------------------------------------------------------------------
    // SKI / POLE PLACEMENT
    // ----------------------------------------------------------------------

    private void MoveSkisToBack()
    {
        // Reparent skis and poles to their "on back" parents.
        if (leftSki != null && leftSkiBackParent != null)
        {
            leftSki.SetParent(leftSkiBackParent, worldPositionStays: false);
            leftSki.localPosition = Vector3.zero;
            leftSki.localRotation = Quaternion.identity;
        }

        if (rightSki != null && rightSkiBackParent != null)
        {
            rightSki.SetParent(rightSkiBackParent, worldPositionStays: false);
            rightSki.localPosition = Vector3.zero;
            rightSki.localRotation = Quaternion.identity;
        }

        if (leftPoleRoot != null && leftPoleBackParent != null)
        {
            leftPoleRoot.SetParent(leftPoleBackParent, worldPositionStays: false);
            leftPoleRoot.localPosition = Vector3.zero;
            leftPoleRoot.localRotation = Quaternion.identity;
        }

        if (rightPoleRoot != null && rightPoleBackParent != null)
        {
            rightPoleRoot.SetParent(rightPoleBackParent, worldPositionStays: false);
            rightPoleRoot.localPosition = Vector3.zero;
            rightPoleRoot.localRotation = Quaternion.identity;
        }
    }

    private void ForceSkiPose()
    {
        // Restore original parents and their cached idle local pose.
        if (leftSki != null && _leftSkiOriginalParent != null)
        {
            leftSki.SetParent(_leftSkiOriginalParent, worldPositionStays: false);
            leftSki.localPosition = _leftSkiOriginalLocalPos;
            leftSki.localRotation = _leftSkiOriginalLocalRot;
        }

        if (rightSki != null && _rightSkiOriginalParent != null)
        {
            rightSki.SetParent(_rightSkiOriginalParent, worldPositionStays: false);
            rightSki.localPosition = _rightSkiOriginalLocalPos;
            rightSki.localRotation = _rightSkiOriginalLocalRot;
        }

        if (leftPoleRoot != null && _leftPoleOriginalParent != null)
        {
            leftPoleRoot.SetParent(_leftPoleOriginalParent, worldPositionStays: false);
            leftPoleRoot.localPosition = _leftPoleOriginalLocalPos;
            leftPoleRoot.localRotation = _leftPoleOriginalLocalRot;
        }

        if (rightPoleRoot != null && _rightPoleOriginalParent != null)
        {
            rightPoleRoot.SetParent(_rightPoleOriginalParent, worldPositionStays: false);
            rightPoleRoot.localPosition = _rightPoleOriginalLocalPos;
            rightPoleRoot.localRotation = _rightPoleOriginalLocalRot;
        }

        if (skiController != null)
            skiController.ReleaseStackSkiVisualOverridesAndSnap(snapToNeutralPose: true);

        if (leftPoleContact != null)
            leftPoleContact.ClearStackVisualOverride();

        if (rightPoleContact != null)
            rightPoleContact.ClearStackVisualOverride();

        if (skierLimbLineVisual != null)
            skierLimbLineVisual.ForceClearStackVisualState(refreshCurrentModeAnchors: true);
    }

    // ----------------------------------------------------------------------
    // WALK MOVEMENT
    // ----------------------------------------------------------------------

    private void ApplyWalkMovement()
    {
        Vector2 rawInput = controlsEnabled ? _player.Move.ReadValue<Vector2>() : Vector2.zero;
        bool rawSprint = controlsEnabled && _player.Sprint.IsPressed();

        _lastUserMoveRaw = rawInput;
        _lastUserSprintRaw = rawSprint;

        if (_riderPoseActive)
        {
            _walkMoveBlend01 = 0f;
            _walkRunBlend01 = 0f;
            _walkAirMoveBlend01 = 0f;
            return;
        }

        bool usingExternalWorldMove = _externalWorldMoveActive &&
                                      _externalWorldMoveDirection.sqrMagnitude > 0.0001f;

        Vector3 desiredDir;
        float inputMagnitude;
        bool sprintHeld;

        if (usingExternalWorldMove)
        {
            desiredDir = _externalWorldMoveDirection;
            inputMagnitude = _externalWorldMoveStrength;

            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (currentForward.sqrMagnitude > 0.0001f)
            {
                currentForward.Normalize();
                float facingDot = Vector3.Dot(currentForward, desiredDir);
                float facing01 = Mathf.InverseLerp(_externalWorldMoveFacingGateDot, 1f, facingDot);
                inputMagnitude *= Mathf.Clamp01(facing01);
            }

            sprintHeld = _externalSprint;
        }
        else
        {
            Vector2 moveInput = _externalMoveActive ? _externalMove : rawInput;
            sprintHeld = _externalMoveActive ? _externalSprint : rawSprint;

            GetMoveBasis(out Vector3 forward, out Vector3 right);

            desiredDir = forward * moveInput.y + right * moveInput.x;
            inputMagnitude = desiredDir.magnitude;
            if (inputMagnitude > 1f)
                desiredDir /= inputMagnitude;
        }

        bool groundedForWalk = _isGrounded;

        // Visual channels:
        // Grounded input drives walk/run cycle.
        // Airborne input is available separately but should not play walk/run animation.
        _walkMoveBlend01 = groundedForWalk ? Mathf.Clamp01(inputMagnitude) : 0f;
        _walkRunBlend01 = groundedForWalk && sprintHeld && inputMagnitude > 0.1f ? 1f : 0f;
        _walkAirMoveBlend01 = !groundedForWalk ? Mathf.Clamp01(inputMagnitude) : 0f;

        float baseSpeed = sprintHeld ? runSpeed : walkSpeed;
        float targetSpeed = baseSpeed * inputMagnitude;

        float accelLimit;
        float turnSpeed;

        if (groundedForWalk)
        {
            accelLimit = targetSpeed > 0.01f ? acceleration : deceleration;
            turnSpeed = 10f;
        }
        else
        {
            targetSpeed *= Mathf.Clamp01(airControlSpeedMultiplier);

            // Preserve existing air momentum when no input is held.
            accelLimit = inputMagnitude > 0.01f
                ? acceleration * Mathf.Clamp01(airControlAccelerationMultiplier)
                : 0f;

            turnSpeed = allowAirTurn ? Mathf.Max(0f, airTurnSpeed) : 0f;
        }

        Vector3 vel = _rb.linearVelocity;
        Vector3 velHorizontal = new Vector3(vel.x, 0f, vel.z);
        Vector3 targetVel = desiredDir * targetSpeed;
        Vector3 velDelta = targetVel - velHorizontal;

        if (accelLimit > 0.001f)
        {
            Vector3 accelStep = velDelta / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float accelMag = accelStep.magnitude;

            if (accelMag > accelLimit)
                accelStep = accelStep.normalized * accelLimit;

            _rb.AddForce(new Vector3(accelStep.x, 0f, accelStep.z), ForceMode.Acceleration);
        }

        if (turnSpeed > 0f && desiredDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }
    }

    // ----------------------------------------------------------------------
    // JUMP MOVEMENT
    // ----------------------------------------------------------------------

    private void HandleJumpInput()
    {
        if (_skisOn || _riderPoseActive)
        {
            ClearWalkJumpState(clearReleaseVisual: false);
            return;
        }

        if (_player.Jump.WasPressedThisFrame())
        {
            _jumpQueued = true;
            _jumpCharging = true;
            _jumpReleaseQueued = false;
            _lastJumpPressedTime = Time.time;
            _lastJumpCharge01 = 0f;
        }

        if (_jumpCharging)
        {
            _lastJumpCharge01 = WalkJumpCharge01;

            if (_player.Jump.WasReleasedThisFrame() || !_player.Jump.IsPressed())
            {
                float heldDuration = Time.time - _lastJumpPressedTime;
                _lastJumpCharge01 = maxJumpChargeTime > 0f
                    ? Mathf.Clamp01(heldDuration / maxJumpChargeTime)
                    : 1f;

                _jumpCharging = false;
                _jumpReleaseQueued = true;
                _lastJumpReleasedTime = Time.time;
            }
        }

        if (_jumpQueued &&
            !_jumpCharging &&
            !_jumpReleaseQueued &&
            Time.time - _lastJumpPressedTime > jumpBufferTime)
        {
            ClearWalkJumpState(clearReleaseVisual: false);
        }
    }

    private void UpdateGroundedState()
    {
        bool wasGrounded = _isGrounded;

        if (Time.time < _ignoreWalkGroundingUntilTime)
        {
            _isGrounded = false;
            return;
        }

        _isGrounded = false;

        Vector3 normal = Vector3.up;

        // Body/root probe. This replaces the raw SphereCast so we can ignore self-colliders.
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float castDistance = Mathf.Max(0.05f, groundCheckDistance);

        if (TryWalkGroundSphereCast(origin, groundCheckRadius, castDistance, out RaycastHit bodyHit))
        {
            _isGrounded = true;
            normal = bodyHit.normal.sqrMagnitude > 0.0001f
                ? bodyHit.normal.normalized
                : Vector3.up;
        }

        // Foot-target fallback. This mirrors what the walk visual is actually placing on the floor.
        if (!_isGrounded && skierLimbLineVisual != null && skierLimbLineVisual.HasValidWalkFootTargets())
        {
            bool leftGrounded = TryCheckWalkFootGround(
                skierLimbLineVisual.GetWalkFootTargetPosition(true),
                out RaycastHit leftHit);

            bool rightGrounded = TryCheckWalkFootGround(
                skierLimbLineVisual.GetWalkFootTargetPosition(false),
                out RaycastHit rightHit);

            if (leftGrounded || rightGrounded)
            {
                _isGrounded = true;

                RaycastHit hit;

                if (leftGrounded && rightGrounded)
                    hit = leftHit.distance <= rightHit.distance ? leftHit : rightHit;
                else
                    hit = leftGrounded ? leftHit : rightHit;

                normal = hit.normal.sqrMagnitude > 0.0001f
                    ? hit.normal.normalized
                    : Vector3.up;
            }
        }

        // Collision fallback. Ski mode already benefits from contact-based grounding;
        // walk mode needs the same safety net.
        if (!_isGrounded && Time.time <= _walkCollisionGroundedUntilTime)
        {
            _isGrounded = true;
            normal = _lastWalkGroundNormal.sqrMagnitude > 0.0001f
                ? _lastWalkGroundNormal.normalized
                : Vector3.up;
        }

        if (_isGrounded)
        {
            float currentY = _rb != null ? _rb.position.y : transform.position.y;
            float verticalVelocity = _rb != null ? _rb.linearVelocity.y : 0f;

            if (ShouldSuppressPrematureWalkGrounding(currentY, verticalVelocity))
            {
                if (logWalkLandingDebug)
                {
                    Debug.Log(
                        $"[{nameof(WalkingController)}] Suppressed premature walk grounding. " +
                        $"airTime={Time.time - _airborneStartTime:0.00}, " +
                        $"velY={verticalVelocity:0.00}, dropFromPeak={Mathf.Max(0f, _highestAirborneY - currentY):0.00}, " +
                        $"maxDownSpeed={_maxDownwardSpeedWhileAirborne:0.00}",
                        this);
                }

                _isGrounded = false;
                return;
            }

            _lastGroundedTime = Time.time;
            _lastWalkGroundNormal = normal;
        }
    }

    private bool TryCheckWalkFootGround(Vector3 footWorldPosition, out RaycastHit hit)
    {
        hit = default;

        if (footWorldPosition == Vector3.zero)
            return false;

        Vector3 origin = footWorldPosition + Vector3.up * 0.25f;
        float distance = 0.45f + Mathf.Max(0f, walkFootGroundClearance);

        return TryWalkGroundSphereCast(
            origin,
            Mathf.Max(0.03f, groundCheckRadius * 0.45f),
            distance,
            out hit);
    }

    private void TryProcessJump()
    {
        if (!_jumpQueued || !_jumpReleaseQueued)
            return;

        bool canJump =
            _isGrounded ||
            Time.time - _lastGroundedTime <= Mathf.Max(0f, jumpCoyoteTime) ||
            Time.time <= _walkCollisionGroundedUntilTime;

        if (!canJump)
        {
            if (Time.time - _lastJumpReleasedTime > jumpBufferTime)
            {
                if (logWalkJumpDebug)
                {
                    Debug.LogWarning(
                        $"[{nameof(WalkingController)}] Walk jump release expired without launch. " +
                        $"isGrounded={_isGrounded}, timeSinceGrounded={Time.time - _lastGroundedTime:0.000}, " +
                        $"collisionGroundedUntilDelta={_walkCollisionGroundedUntilTime - Time.time:0.000}, " +
                        $"groundMask={groundLayers.value}",
                        this);
                }

                ClearWalkJumpState(clearReleaseVisual: false);
            }

            return;
        }

        PrepareRigidbodyForWalkPhysics();

        float charge01 = maxJumpChargeTime > 0f
            ? Mathf.Clamp01((_lastJumpReleasedTime - _lastJumpPressedTime) / maxJumpChargeTime)
            : 1f;

        charge01 = Mathf.Clamp01(charge01);
        _lastJumpCharge01 = charge01;

        float minForce = Mathf.Min(minJumpForce, jumpForce);
        float maxForce = Mathf.Max(minJumpForce, jumpForce);
        float launchSpeed = Mathf.Lerp(minForce, maxForce, Mathf.SmoothStep(0f, 1f, charge01));

        Vector3 groundNormal = _lastWalkGroundNormal.sqrMagnitude > 0.0001f
            ? _lastWalkGroundNormal.normalized
            : Vector3.up;

        Vector3 jumpDir = Vector3.Slerp(groundNormal, Vector3.up, 0.75f).normalized;

        Vector3 velocityBefore = _rb.linearVelocity;
        Vector3 velocity = velocityBefore;

        float existingAlongJump = Vector3.Dot(velocity, jumpDir);
        if (existingAlongJump < 0f)
            velocity -= jumpDir * existingAlongJump;

        float currentAlongJump = Vector3.Dot(velocity, jumpDir);
        if (currentAlongJump < launchSpeed)
            velocity += jumpDir * (launchSpeed - currentAlongJump);

        float requiredVerticalSpeed = Mathf.Max(minForce, launchSpeed * 0.75f);
        if (velocity.y < requiredVerticalSpeed)
            velocity.y = requiredVerticalSpeed;

        _rb.linearVelocity = velocity;

        float separation = Mathf.Max(0f, jumpLaunchSeparation);
        if (separation > 0f)
        {
            // Use direct RB position here, not MovePosition. MovePosition can be smoothed/solved
            // back into contact depending on interpolation/contact state.
            _rb.position += Vector3.up * separation;
            Physics.SyncTransforms();
        }

        float ignoreDuration = Mathf.Max(0.02f, jumpGroundIgnoreDuration);
        _ignoreWalkGroundingUntilTime = Time.time + ignoreDuration;
        _ignoreWalkGroundSnapUntilTime = Time.time + ignoreDuration;
        _walkCollisionGroundedUntilTime = -999f;
        _pendingWalkGroundSnapFrames = 0;

        _isGrounded = false;
        _lastGroundedTime = -999f;
        _lastJumpExecutedTime = Time.time;

        // The jump itself is a confirmed airborne transition.
        // Do this here so landing animation works even during the ground-ignore window.
        BeginWalkAirborneTracking(allowLandingAnimation: true);

        // A jump begins by moving upward. Do not allow walk grounding to be consumed
        // as a landing until the character has actually started descending.
        _walkAirborneSawDescent = false;

        if (logWalkJumpDebug)
        {
            Debug.Log(
                $"[{nameof(WalkingController)}] Walk jump LAUNCHED. " +
                $"charge={charge01:0.00}, launchSpeed={launchSpeed:0.00}, " +
                $"velBefore={velocityBefore}, velAfter={_rb.linearVelocity}, rbPos={_rb.position}, " +
                $"constraints={_rb.constraints}, isKinematic={_rb.isKinematic}, " +
                $"detectCollisions={_rb.detectCollisions}, useGravity={_rb.useGravity}",
                this);
        }

        ClearWalkJumpState(clearReleaseVisual: false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateWalkGroundCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateWalkGroundCollision(collision);
    }

    private void EvaluateWalkGroundCollision(Collision collision)
    {
        if (_skisOn || collision == null || collision.contactCount <= 0)
            return;

        if (!IsGroundLayer(collision.gameObject.layer))
            return;

        float bestUpDot = -1f;
        Vector3 bestNormal = Vector3.up;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            Vector3 normal = contact.normal.sqrMagnitude > 0.0001f
                ? contact.normal.normalized
                : Vector3.up;

            float upDot = Vector3.Dot(normal, Vector3.up);
            if (upDot < walkGroundMinUpDot)
                continue;

            if (upDot > bestUpDot)
            {
                bestUpDot = upDot;
                bestNormal = normal;
            }
        }

        if (bestUpDot < 0f)
            return;

        _walkCollisionGroundedUntilTime = Time.time + 0.08f;
        _lastGroundedTime = Time.time;
        _lastWalkGroundNormal = bestNormal;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawWalkFootGroundSnapGizmos)
            return;

        if (skierLimbLineVisual == null)
            skierLimbLineVisual = GetComponentInChildren<SkierLimbLineVisual>(true);

        if (skierLimbLineVisual == null || !skierLimbLineVisual.HasValidWalkFootTargets())
            return;

        DrawWalkFootGroundProbe(skierLimbLineVisual.GetWalkFootTargetPosition(true));
        DrawWalkFootGroundProbe(skierLimbLineVisual.GetWalkFootTargetPosition(false));
    }

    private void DrawWalkFootGroundProbe(Vector3 footWorldPosition)
    {
        if (footWorldPosition == Vector3.zero)
            return;

        Vector3 rayOrigin = footWorldPosition + Vector3.up * Mathf.Max(0.01f, walkFootGroundRayHeight);
        float rayDistance = Mathf.Max(0.01f, walkFootGroundRayHeight + walkFootGroundRayDistance);

        bool hitGround = Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        float clearance = Mathf.Max(0f, walkFootGroundClearance);
        bool footIsBelowGroundClearance = hitGround && footWorldPosition.y < hit.point.y + clearance;

        Gizmos.color = footIsBelowGroundClearance ? Color.red : Color.green;
        Gizmos.DrawWireSphere(footWorldPosition, 0.055f);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * rayDistance);

        if (hitGround)
        {
            Gizmos.DrawWireSphere(hit.point, 0.045f);
            Gizmos.DrawLine(hit.point, hit.point + hit.normal.normalized * 0.25f);
        }
    }
}
