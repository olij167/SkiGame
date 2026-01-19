using System.Collections.Generic;
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

    [Header("Input Actions")]
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

    //// Fields
    //private bool _leftGrounded;
    //private bool _rightGrounded;
    //private RaycastHit _leftHit;
    //private RaycastHit _rightHit;

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

    // Separate from _groundNormal: used only for visual/orientation alignment (pitch/roll).
    private Vector3 _alignNormal = Vector3.up;

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
    // ------------------------------------------------------------------
    // Non-ski body collisions (anti "slide on head")
    //
    // We deliberately keep this as minimal as possible and reuse existing
    // tuning (groundLayers + maxLandingTiltAngle) to avoid adding more
    // inspector knobs.
    // ------------------------------------------------------------------
    private readonly HashSet<Collider> _skiColliders = new HashSet<Collider>();
    private bool _hasNonSkiGroundContact;
    private Vector3 _nonSkiGroundContactPoint;
    private Vector3 _nonSkiGroundContactNormal;
    private float _nonSkiGroundContactUpDot;

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

    // If jump was pressed while grinding, only allow it to fire while still grinding.
    // Prevents surprise buffered jumps after falling off the rail/cable.
    private bool _jumpMustFireWhileGrinding;

    private float _lastJumpPressedTime;
    private float _lastGroundedTime;
    private float _lastJumpTime;
    private float _airborneStartTime;
    private float _airbornePeakY;

    // Air-entry smoothing: capture takeoff lean so small airtime doesn't instantly pitch the skier.
    private float _airEntryTime;
    private float _airLeanBaseline;

    // Air rotation state (degrees/second around local axes)
    private Vector3 _airAngularVelocity;

    // ----------------------------------------------------------------------
    // GRINDING (Air Edge Assist) runtime state
    // ----------------------------------------------------------------------

    private bool _grindActive;
    private bool _grindDetachRequested;
    private float _grindStrengthSmoothed;
    private float _grindReattachCooldownUntil;

    // Cached best grind frame data (debug + detach impulse direction)
    private Vector3 _grindClosestPoint;
    private Vector3 _grindTangent;
    private Vector3 _grindDeltaToRail;
    private string _dbgGrindSource;
    private Vector3 _grindNormal = Vector3.up;

    // ----------------------------------------------------------------------
    // PARAMETERS (Inspector order = declaration order)
    // ----------------------------------------------------------------------

    [Header("Visuals (Lean & Stance)")]
    [Tooltip("How quickly forward/back lean (visual body + tuning input) smooths toward the player's lean input. Higher = snappier.")]
    [SerializeField] private float forwardLeanLerpSpeed = 5f;

    [Tooltip("How quickly each ski stance value (edge/spread) smooths toward the player's leg inputs. Higher = snappier.")]
    [SerializeField] private float stanceLerpSpeed = 10f;

    [Tooltip("How fast the visual ski models interpolate toward their target yaw/offset. Higher = snappier visuals.")]
    [SerializeField] private float skiVisualLerpSpeed = 12f;

    [Tooltip("How fast the follower ski catches up to the lead ski stance when only one leg is actively edged. Higher = tighter coupling.")]
    [SerializeField] private float followerStanceLerpSpeed = 4f;

    [Tooltip("Max lateral offset (m) each ski can be pushed out from center by stance/edge. Larger = wider stance range.")]
    [SerializeField] private float maxSkiOffset = 0.3f;

    [Tooltip("Max inward yaw (degrees) when a ski is fully edged at speed. Larger = more aggressive looking edge angle.")]
    [SerializeField] private float maxSkiEdgeAngle = 20f;

    [Tooltip("Max visual forward lean angle (degrees) applied to the body transform. Visual only.")]
    [SerializeField] private float maxForwardLeanAngle = 25f;

    [Tooltip("Max visual side lean angle (degrees) applied to the body transform. Visual only.")]
    [SerializeField] private float maxSideLeanAngle = 15f;


    [Header("Ground Detection")]
    [Tooltip("Physics layers treated as snow/ground for grounding, landing, and non-ski body contact checks.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Radius (m) of the sphere cast used to detect near-ground under the skier root.")]
    [SerializeField] private float groundCheckRadius = 0.25f;

    [Tooltip("Height (m) above the skier root to start the ground cast from.")]
    [SerializeField] private float groundCheckHeight = 0.6f;

    [Tooltip("Distance (m) the ground cast checks downward from the cast origin.")]
    [SerializeField] private float groundCheckDistance = 1.2f;

    [Tooltip("Vertical gap (m) from the cast hit distance to count as 'in contact' with the ground.")]
    [SerializeField] private float groundContactDistance = 0.25f;

    [Tooltip("When skis are physically contacting, allow a larger body cast gap (m) to still count as near-ground. Helps prevent control snapping on steepening slopes.")]
    [SerializeField] private float groundContactDistanceWhenSkiContact = 0.55f;

    [Tooltip("Maximum slope angle (deg) treated as 'rideable ground' for grounding & landing logic. Steeper surfaces are treated as walls.")]
    [SerializeField, Range(0f, 90f)]
    private float maxGroundSlopeAngle = 80f;


    [Header("Ground Normal Smoothing")]
    [Tooltip("How quickly the detected ground normal smooths toward new values when near ground. Higher = snappier, lower = less jitter.")]
    [SerializeField] private float groundNormalSmoothSpeed = 12f;

    [Tooltip("How long (seconds) after leaving ground we still allow 'grounded controls' (turning/stance) to feel responsive.")]
    [SerializeField] private float controlCoyoteTime = 0.08f;


    [Header("Alignment (Anti Tip-Dig)")]
    [Tooltip("Smoothing rate for the visual/alignment normal (used for pitch/roll alignment). Higher = quicker response, lower = steadier.")]
    [SerializeField] private float alignNormalSmoothSpeed = 10f;

    [Tooltip("Rate-limit for alignment target changes (degrees/second). Helps prevent rapid pitch/roll flips on noisy contact normals.")]
    [SerializeField] private float alignMaxDegreesPerSec = 120f;


    [Header("Anti-Clipping (Ski Clearance)")]
    [Tooltip("If enabled, lifts the rigidbody up when ski transforms are detected below the ground plane (transitions + awkward landings).")]
    [SerializeField] private bool preventSkiTerrainClipping = true;

    [Tooltip("Minimum clearance (m) each ski probe point should remain above ground along the current up/ground normal.")]
    [SerializeField] private float skiClearance = 0.03f;

    [Tooltip("How far above each ski point (m) to start the ground probe from.")]
    [SerializeField] private float skiProbeUp = 0.35f;

    [Tooltip("How far below each ski point (m) to probe for ground.")]
    [SerializeField] private float skiProbeDown = 0.9f;

    [Tooltip("Maximum lift applied per physics step (m). Prevents large pops.")]
    [SerializeField] private float maxLiftPerFixedStep = 0.15f;

    [Tooltip("How quickly we apply lift toward the required clearance. Higher = faster correction, lower = softer.")]
    [SerializeField] private float liftResponse = 18f;


    [Header("Gravity & Slope Acceleration")]
    [Tooltip("Minimum slope angle (deg) before we treat the surface as a slope where downhill gravity/lean effects matter.")]
    [SerializeField] private float minSlopeAngleForDownhill = 1f;

    [Tooltip(
        "Scales slope-parallel gravity while grounded.\n" +
        "1 = physical gravity, >1 = faster downhill acceleration.\n" +
        "Blends in from 'minSlopeAngleForDownhill' up to ~35° slopes so flats are not over-boosted.")]
    [SerializeField, Range(0.5f, 3f)] private float groundedSlopeGravityScale = 1.25f;


    [Header("Snow Resistance (Glide & Edge)")]
    [Tooltip("Base friction along ski direction (low = more glide, higher = more speed bleed).")]
    [SerializeField] private float forwardFriction = 0.25f;

    [Tooltip("Base friction across ski direction (higher = stronger edge hold, less sideways drift).")]
    [SerializeField] private float sideFriction = 1.2f;


    [Header("Snow Adhesion (Stickiness)")]
    [Tooltip("Strength of damping into-ground velocity component while grounded (helps reduce chatter / micro-bounces).")]
    [SerializeField] private float normalKillStrength = 8f;

    [Tooltip("Maximum upward speed (m/s) along ground normal that will be damped while grounded.\n" +
             "Small upward bumps are reduced so you stay 'glued' to the snow; explicit jumps are not affected.")]
    [SerializeField] private float maxStickUpwardSpeed = 2f;


    [Header("Lean Speed Modifiers")]
    [Tooltip("How much forward lean reduces forward friction (tuck). 0 = no effect, higher = more speed retention while leaning forward.")]
    [SerializeField, Range(0f, 0.8f)] private float tuckFrictionReduction = 0.35f;

    [Tooltip("How much backward lean increases forward friction (brake). 0 = no effect, higher = stronger braking while leaning back.")]
    [SerializeField, Range(0f, 2f)] private float brakeFrictionIncrease = 0.9f;

    [Tooltip("Extra gravity-driven slip when standing neutral (no lean) on a slope. Higher = more passive sliding.")]
    [SerializeField, Range(0f, 1f)] private float neutralSlipBoost = 0.25f;


    [Header("Ground Orientation")]
    [Tooltip("How fast the root yaws toward the desired direction on the slope. Higher = faster heading changes.")]
    [SerializeField] private float groundTurnSpeed = 8f;

    [Tooltip("How fast pitch/roll aligns to the slope (separate from yaw). Higher = stronger slope alignment.")]
    [SerializeField] private float groundAlignSpeed = 18f;


    [Header("Turning & Steering (Carve vs Skate)")]
    [Tooltip("How strongly planar velocity is rotated toward ski direction when skis are parallel and edged. Higher = more 'carve lock' at speed.")]
    [SerializeField] private float carveSteerStrength = 4f;

    [Tooltip("Below this planar speed (m/s), ski yaw is heavily reduced so leg inputs feel more like skating than carving.")]
    [SerializeField] private float minCarveSpeed = 3f;

    [Tooltip("Above this planar speed (m/s), ski yaw reaches full maxSkiEdgeAngle.")]
    [SerializeField] private float maxCarveSpeed = 12f;

    [Tooltip("Max ski yaw (deg) when moving very slowly (used at or below MinCarveSpeed).")]
    [SerializeField] private float lowSpeedMaxYaw = 6f;

    [Tooltip("Minimum planar speed (m/s) before we apply small waddle yaw from skate pushes.")]
    [SerializeField] private float minSpeedForSkateYaw = 1.5f;

    [Header("Quick Stop (Sharp Turn Brake)")]
    [Tooltip(
    "Extra planar speed damping applied when the skier requests a very sharp carve at speed (hockey-stop).\n" +
    "0 disables. Higher = stronger quick-stop. This only engages when skis are parallel+edged and the turn angle is large.")]
    [SerializeField] private float quickStopStrength = 6f;

    [Tooltip("Minimum planar speed (m/s) before quick-stop can engage.")]
    [SerializeField] private float quickStopMinSpeed = 7f;

    [Tooltip("Minimum angle (deg) between current travel direction and ski direction before quick-stop engages.")]
    [SerializeField, Range(0f, 90f)] private float quickStopMinTurnAngle = 40f;

    [Header("Traverse Hold (Anti-Creep)")]
    [Tooltip("When traversing across the fall line, applies extra damping along the fall direction to help you stand sideways without slowly sliding.")]
    [SerializeField] private bool enableTraverseHold = true;

    [Tooltip("Max fall-line speed (m/s) below which traverse hold will strongly resist sliding.")]
    [SerializeField] private float traverseHoldSpeed = 1.0f;

    [Tooltip("Extra damping rate applied along the fall line while traverse-holding. Higher = stronger 'stay put'.")]
    [SerializeField] private float traverseHoldDamp = 8.0f;

    [Tooltip("Minimum slope angle (deg) before traverse hold can engage.")]
    [SerializeField] private float traverseHoldMinSlopeAngle = 8f;

    [Tooltip("How across-the-fall-line you must be (0..1) before traverse hold engages. 1 = perfectly across.")]
    [SerializeField, Range(0f, 1f)] private float traverseHoldAcrossThreshold = 0.65f;

    [Tooltip("Overall strength scalar for traverse hold (0..1).")]
    [SerializeField, Range(0f, 1f)] private float traverseHoldStrength = 1.0f;


    [Header("Skating")]
    [Tooltip("Minimum forward lean required to generate a skate push.")]
    [SerializeField] private float minForwardLeanForPush = 0.1f;

    [Tooltip("Velocity change (m/s) applied along ski direction for each valid push.")]
    [SerializeField] private float skateImpulse = 2.5f;

    [Tooltip("Cooldown between pushes (seconds).")]
    [SerializeField] private float skateCooldown = 0.2f;

    [Tooltip("Max speed (m/s) at which skate pushes are fully effective. Above this they taper off.")]
    [SerializeField] private float skateMaxEffectiveSpeed = 6f;

    [Tooltip("Slope angle (deg) where uphill skating begins to be penalized.")]
    [SerializeField, Range(0f, 89f)] private float skateUphillPenaltyStartAngle = 12f;

    [Tooltip("Slope angle (deg) where uphill skating is heavily penalized (approaches min impulse factor).")]
    [SerializeField, Range(0f, 89f)] private float skateUphillPenaltyEndAngle = 32f;

    [Tooltip("Minimum fraction of skate impulse allowed when pushing fully uphill on very steep slopes.")]
    [SerializeField, Range(0f, 1f)] private float skateUphillMinImpulseFactor = 0.08f;

    [Tooltip("Extra downhill pull applied when trying to skate uphill on steep slopes (0 disables).")]
    [SerializeField] private float skateUphillDownPull = 3.5f;

    [Tooltip("Yaw degrees applied per skate push (small waddle twist).")]
    [SerializeField] private float skateYawPerPush = 4f;


    [Header("Poles")]
    [Tooltip("Baseline speed at which the pole stroke phase progresses when the player is stationary.")]
    [SerializeField] private float basePoleStrokeSpeed = 2f;

    [Tooltip("Extra stroke speed added as planar speed approaches 'strokeSpeedBoostAtSpeed'.")]
    [SerializeField] private float maxPoleStrokeSpeedBoost = 2f;

    [Tooltip("Planar speed (m/s) at which the pole stroke reaches its maximum speed boost.")]
    [SerializeField] private float strokeSpeedBoostAtSpeed = 10f;

    [Tooltip("Impulse magnitude applied by an effective pole stroke (propulsion during drag/follow-through).")]
    [SerializeField] private float poleImpulse = 3f;

    [Tooltip("Above this planar speed (m/s), pole propulsion/drag tapers off.")]
    [SerializeField] private float poleMaxSpeed = 8f;

    [Tooltip("Drag strength applied along direction of motion while poles are dug in (braking).")]
    [SerializeField] private float poleBrakeStrength = 10f;

    [Tooltip("Minimum effectiveness of pole strokes at/above poleMaxSpeed (0 = none, 1 = full).")]
    [SerializeField, Range(0f, 1f)] private float minPoleSpeedFactor = 0.1f;


    // Tip-contact stability tuning (kept as code constants to avoid more inspector clutter).
    // If you want to expose these in the inspector later, just turn them into [SerializeField] fields.
    private const float TipContactStackFraction = 0.7f;  // fraction of grounded skis whose last contact is in the tip region
    private const float TipContactMinSpeed = 2.5f;       // planar m/s before tip checks matter
    private const float TipContactStackTime = 0.18f;     // seconds of tip-heavy contact before we stack
    private const float MinBaseAlignForGroundNormal = 0.25f;


    [Header("Jump")]
    [Tooltip("Min/Max velocity change applied along the jump direction when jumping.")]
    [SerializeField] private Vector2 jumpForceRange = new Vector2(3f, 8f);

    [Tooltip("Time window after leaving ground where a jump press will still be accepted (seconds).")]
    [SerializeField] private float jumpCoyoteTime = 0.15f;

    [Tooltip("Time window a jump press is buffered so it can fire on the next valid ground contact (seconds).")]
    [SerializeField] private float jumpBufferTime = 0.10f;

    [Tooltip("How long holding jump builds up to full charge (seconds).")]
    [SerializeField] private float maxJumpChargeTime = 0.5f;

    [Tooltip("Time after a jump during which ground stickiness will not damp upward motion (seconds).")]
    [SerializeField] private float jumpStickSuppressionTime = 0.2f;

    [Tooltip("Minimum time after performing a jump before ground checks will consider the rider grounded again.\n" +
             "Prevents tiny hops from instantly re-sticking on steep slopes.")]
    [SerializeField] private float minJumpUngroundedTime = 0.12f;

    [Tooltip("Multiplier applied to jump force at high speed. 1 = no boost, 1.5 = 50% stronger jumps at/above the speed below.")]
    [SerializeField] private float jumpSpeedForceMultiplier = 1.5f;

    [Tooltip("Planar speed (m/s) at which the jump speed multiplier reaches maximum effect.")]
    [SerializeField] private float jumpSpeedForMaxMultiplier = 12f;


    [Header("Jump Direction (Steep Slope Safety)")]
    [Tooltip("Slope angle (deg) where jump direction begins blending from ground-normal toward world-up.")]
    [SerializeField] private float jumpUpBlendStartAngle = 55f;

    [Tooltip("Slope angle (deg) where jump direction becomes fully world-up (prevents sideways launches on very steep faces).")]
    [SerializeField] private float jumpUpBlendEndAngle = 80f;


    [Header("Air Control")]
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


    [Header("Air Entry Smoothing")]
    [Tooltip("Seconds to ramp aerial input from 0 to full after leaving the ground. Prevents instant pitch/yaw on tiny airtime.")]
    [SerializeField] private float airControlBlendInTime = 0.12f;

    [Tooltip("0 = air pitch uses absolute lean; 1 = uses delta from lean at takeoff. Delta prevents pitching just because you were leaning when you left the ground.")]
    [SerializeField, Range(0f, 1f)] private float airPitchUseDeltaFromTakeoff = 1f;

    [Tooltip("Deadzone applied to air pitch input (after delta/absolute mix). Helps ignore tiny lean noise in air.")]
    [SerializeField] private float airPitchDeadzone = 0.05f;

    [Header("Grinding (Air Edge Assist)")]
    [Tooltip("If enabled, while airborne the rider can 'lock' onto nearby grindable features using air controls.\n" +
         "This is not a snap-on: it applies a spring + friction only when close and the skis are sufficiently sideways to the rail.")]
    [SerializeField] private bool grindEnabled = true;

    [Tooltip("Layer mask used for detecting nearby grindable colliders (optional). Fence segments usually work well here.\n" +
             "Lift cables do NOT require colliders (LiftLine is queried via a lightweight runtime registry).")]
    [SerializeField] private LayerMask grindLayers = 0;

    [Tooltip("Radius (m) around the skis used to search for grind candidates.")]
    [SerializeField] private float grindSearchRadius = 1.2f;

    [Tooltip("Maximum distance (m) from skis to the rail centerline to apply assist.")]
    [SerializeField] private float grindCaptureRadius = 0.45f;

    [Tooltip("Minimum planar speed required before grind assist will engage.")]
    [SerializeField] private float grindMinPlanarSpeed = 2.0f;

    [Tooltip("How sideways the skis must be to the rail tangent before assist engages.\n" +
             "This is a dot threshold on |dot(skiForward, railTangent)|. Smaller = more sideways.\n" +
             "Example: 0.45 means assist begins when skis are at least ~63 degrees sideways.")]
    [Range(0.0f, 0.95f)]
    [SerializeField] private float grindSidewaysDotBegin = 0.45f;

    [Tooltip("Dot value for 'fully sideways' where assist reaches max contribution (strong boardslide).\n" +
             "Example: 0.15 means ~81 degrees sideways.")]
    [Range(0.0f, 0.95f)]
    [SerializeField] private float grindSidewaysDotFull = 0.15f;

    [Tooltip("Spring strength pulling the skis toward the rail centerline (m/s^2 per metre).")]
    [SerializeField] private float grindSpring = 55f;

    [Tooltip("Damping against motion into/out of the rail (m/s^2 per m/s).")]
    [SerializeField] private float grindRadialDamping = 14f;

    [Tooltip("Friction damping applied to velocity OFF the rail tangent (m/s^2 per m/s). Higher = more 'locked' and less drift.")]
    [SerializeField] private float grindOffTangentFriction = 6f;

    [Tooltip("Optional friction damping applied ALONG the rail tangent (m/s^2 per m/s). Keep small to avoid killing speed.")]
    [SerializeField] private float grindAlongTangentFriction = 0.6f;
    [Header("Grinding - Friction Feel")]
    [SerializeField, Range(0f, 2f)] private float grindFrictionMultiplier = 1f; // <1 = slipperier, >1 = stickier

    [Header("Grinding - Alignment")]
    [SerializeField] private bool grindAlignEnabled = true;
    [SerializeField, Range(0f, 1f)] private float grindAlignUpBias = 0.15f; // 0 = pure rail normal, 1 = world up
    [SerializeField] private float grindAlignMaxDegreesPerSec = 720f;
    [SerializeField] private float grindAlignStrength = 1.0f; // scales with grind strength (0..1)

    [Tooltip("Extra acceleration applied ALONG the rail tangent while grinding (m/s^2). 0 disables drive.")]
    [SerializeField] private float grindDriveAcceleration = 3.0f;

    [Tooltip("Max speed along the rail that the drive term tries to approach (m/s).")]
    [SerializeField] private float grindDriveMaxSpeed = 18.0f;

    [Tooltip("Additional damping applied to aerial angular velocity while grinding (deg/sec^2).")]
    [SerializeField] private float grindAngularDamping = 520f;

    [Tooltip("How much to reduce aerial angular acceleration while grinding (0 = none, 1 = no air spin accel).")]
    [Range(0f, 1f)]
    [SerializeField] private float grindAngularAccelReduction = 0.5f;

    [Tooltip("How quickly grind strength ramps in/out (per second). Higher = snappier engagement.")]
    [SerializeField] private float grindStrengthResponse = 10f;

    [Tooltip("Cooldown (seconds) after detaching before grind assist can reattach.")]
    [SerializeField] private float grindReattachCooldown = 0.25f;

    [Tooltip("Velocity change added when detaching from a grind (upwards).")]
    [SerializeField] private float grindDetachUpVel = 2.5f;

    [Tooltip("Velocity change added when detaching from a grind (away from rail).")]
    [SerializeField] private float grindDetachAwayVel = 2.0f;

    [Header("Landing: Stabilization & Projection")]
    [Tooltip("Slope angle (deg) where landing steepness adjustments begin.")]
    [SerializeField] private float landingSteepSlopeStartAngle = 40f;

    [Tooltip("Slope angle (deg) considered 'very steep' for landing adjustments.")]
    [SerializeField] private float landingVerySteepSlopeAngle = 70f;

    [Tooltip("Duration (seconds) after landing where we temporarily boost alignment rates to settle cleanly without snapping.")]
    [SerializeField] private float landingAlignBoostDuration = 0.25f;

    [Tooltip("Multiplier applied to pitch/roll alignment + yaw alignment during the landing assist window.")]
    [SerializeField] private float landingAlignBoostMultiplier = 1.5f;

    [Tooltip("Base strength of velocity projection onto the slope at landing (0 = none, 1 = full projection).")]
    [SerializeField, Range(0f, 1f)] private float landingProjectionStrength = 0.6f;

    [Tooltip("Fraction of planar speed retained when projecting velocity onto the slope at landing.")]
    [SerializeField, Range(0f, 1f)] private float landingVelocityRetention = 0.9f;

    // Internal: short window after landing where alignment is boosted.
    private float _landingAssistUntil = 0f;


    [Header("Landing: Fail Conditions (Stack Rules)")]
    [Tooltip("Max allowed tilt angle (deg) between skier up and ground normal to count as a safe landing.")]
    [SerializeField] private float maxLandingTiltAngle = 50f;

    [Tooltip("Max allowed misalignment (deg) between combined ski direction and planar velocity at landing.")]
    [SerializeField] private float maxLandingMisalignmentAngle = 75f;

    [Tooltip("Minimum planar speed (m/s) where landing misalignment is considered for stacking.")]
    [SerializeField] private float minLandingSpeedForStackCheck = 5f;

    [Tooltip("Air time below this (seconds) is treated as a 'micro' landing for landing/stack logic.")]
    [SerializeField] private float minLandingAirTime = 0.12f;

    [Tooltip("Downward speed below this (m/s) is treated as a 'micro' landing for landing/stack logic.")]
    [SerializeField] private float minLandingDownwardSpeed = 2f;


    [Header("Hard Landing / Slam")]
    [Tooltip("Minimum fall height (m) from jump peak to landing point before we consider a slam check.")]
    [SerializeField] private float hardLandingMinFallHeight = 6f;

    [Tooltip("Minimum downward speed into the ground normal (m/s) required to trigger a slam check.")]
    [SerializeField] private float hardLandingMinDownwardSpeed = 14f;

    [Tooltip("Minimum impact angle from the slope plane (deg). 0 = perfectly along the slope, 90 = straight into the slope.\n" +
             "Higher values mean a flatter 'belly flop' impact and will stack.")]
    [SerializeField, Range(0f, 90f)] private float hardLandingMinImpactAngleFromPlane = 55f;

    [Tooltip("Nose/tip-dig threshold (deg) on landing that will trigger a stack.")]
    [SerializeField] private float heavyTipStackAngle = 25f;


    [Header("Stack Outcome")]
    [Tooltip("Torque impulse applied when stacking (to topple the skier). Higher = more dramatic wipeouts.")]
    [SerializeField] private float stackTorqueImpulse = 30f;

    [Tooltip("When stacking, we remove into-ground velocity and keep this fraction of remaining planar velocity.\n" +
             "Lower values feel 'stickier' (more abrupt wipeouts).")]
    [SerializeField, Range(0f, 1f)] private float stackPlanarVelocityRetention = 0.25f;


    [Header("Debug")]
    [Tooltip("If enabled, draws an on-screen debug panel (IMGUI) with key skiing state values.")]
    [SerializeField] private bool showDebugHUD = true;

    [Tooltip("Show raw and smoothed input values in the debug HUD.")]
    [SerializeField] private bool debugShowInputs = false;

    [Tooltip("Show traverse-hold calculations and gates in the debug HUD.")]
    [SerializeField] private bool debugShowTraverseHold = true;

    [Tooltip("Show jump / landing / stack checks in the debug HUD.")]
    [SerializeField] private bool debugShowJumpLanding = true;

    [Tooltip("Show per-ski contact diagnostics in the debug HUD.")]
    [SerializeField] private bool debugShowSkiContacts = true;

    [Tooltip("If enabled, draws gizmos when this object is selected (contact normals, directions, etc.).")]
    [SerializeField] private bool debugDrawGizmosSelected = true;

    [Tooltip("Scale multiplier for the debug HUD text size.")]
    [SerializeField, Range(0.5f, 2f)]
    private float debugHUDScale = 1f;


    // -------------------- Runtime debug cache (populated in FixedUpdate / landing / jump) --------------------
    private Vector2 _debugScroll;

    private float _dbgSlopeAngle;
    private Vector3 _dbgFallDir;
    private Vector3 _dbgTraverseGPlane;
    private Vector3 _dbgTraversePlaneVel;
    private float _dbgTraverseAcross;
    private float _dbgTraverseVFall;
    private float _dbgTraverseAcrossGate;
    private float _dbgTraverseSpeedGate;
    private float _dbgTraverseHold;

    private float _dbgJumpChargeT;
    private float _dbgJumpForce;
    private float _dbgJumpSpeedMultiplier;
    private float _dbgJumpSlopeAngle;
    private float _dbgJumpUpBlendT;
    private float _dbgJumpIntoSurface;
    private Vector3 _dbgJumpDir;

    private float _dbgLandingAirTime;
    private float _dbgLandingDownwardSpeed;
    private float _dbgLandingPlanarSpeed;
    private float _dbgLandingImpactAngleFromPlane;
    private float _dbgLandingFallHeight;
    private float _dbgLandingMisalignAngle;
    private bool _dbgLandingIsTiny;
    private bool _dbgLandingIsHardSlam;

    private string _dbgLastStackReason = "";

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

    public bool IsGrinding => _grindActive;
    public float GrindStrength01 => Mathf.Clamp01(_grindStrengthSmoothed);

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
        _rb.useGravity = false;

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
        CacheSkiColliders();

    }
    private void CacheSkiColliders()
    {
        _skiColliders.Clear();

        if (leftSki != null)
        {
            foreach (var c in leftSki.GetComponentsInChildren<Collider>(true))
            {
                if (c != null) _skiColliders.Add(c);
            }
        }

        if (rightSki != null)
        {
            foreach (var c in rightSki.GetComponentsInChildren<Collider>(true))
            {
                if (c != null) _skiColliders.Add(c);
            }
        }
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

        if (preventSkiTerrainClipping)
            SnapToGroundClearance(resetDownwardVelocity: true);

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
    // ----------------------------------------------------------------------
    // COLLISIONS (ANTI "SLIDE ON HEAD")
    // ----------------------------------------------------------------------
    // Unity will send collision callbacks to the Rigidbody's GameObject even
    // for child colliders. We filter out ski colliders so only "body" impacts
    // (head/torso/arms/etc) can trigger a stack.
    private void OnCollisionStay(Collision collision)
    {
        // Only consider collisions with ground layers.
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        if (collision.contactCount <= 0)
            return;

        // Pick the most "ground-like" non-ski contact (highest up-dot).
        // We also ignore wall-ish contacts.
        const float MinUpDot = 0.25f;

        float bestUpDot = -1f;
        Vector3 bestPoint = default;
        Vector3 bestNormal = default;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint cp = collision.GetContact(i);

            // Ignore contacts originating from either ski's collider hierarchy.
            // (Prevents double-counting ski-ground contact as a body crash.)
            if (cp.thisCollider != null && _skiColliders.Contains(cp.thisCollider))
                continue;

            Vector3 n = cp.normal.sqrMagnitude > 0.0001f ? cp.normal.normalized : Vector3.up;
            float upDot = Vector3.Dot(n, Vector3.up);
            if (upDot < MinUpDot)
                continue;

            if (upDot > bestUpDot)
            {
                bestUpDot = upDot;
                bestPoint = cp.point;
                bestNormal = n;
            }
        }

        if (bestUpDot < 0f)
            return;

        _hasNonSkiGroundContact = true;
        _nonSkiGroundContactUpDot = bestUpDot;
        _nonSkiGroundContactPoint = bestPoint;
        _nonSkiGroundContactNormal = bestNormal;
    }

    private void OnCollisionExit(Collision collision)
    {
        int otherLayer = collision.gameObject.layer;
        if ((groundLayers.value & (1 << otherLayer)) == 0)
            return;

        // Conservative: clear the cache. If we are still contacting ground via
        // another collider, OnCollisionStay will repopulate it next physics step.
        _hasNonSkiGroundContact = false;
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
        // Sample per-ski ground contact deterministically at the start of physics.
        // This prevents 'airborne' states when ski colliders are touching but the body casts miss.
        if (leftSkiContact != null) leftSkiContact.ManualSampleGround();
        if (rightSkiContact != null) rightSkiContact.ManualSampleGround();

        _wasGrounded = _isGrounded;
        bool wasControlsGrounded = _wasControlsGrounded;

        CheckGround();

        // Continuous safety: if a landing/align step forces skis below terrain, lift out smoothly.
        if (preventSkiTerrainClipping && (HasAnySkiContact || IsGroundedForControls))
            ResolveSkiTerrainPenetration(hardSnap: false);

        // Custom gravity: full gravity in air, tangential-only gravity when grounded.
        // This prevents "stalling mid-slope" and removes double-gravity ambiguity.
        if (IsGroundedForControls)
        {
            Vector3 gPlane = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal);

            // Optional "game feel" boost: scale tangential gravity on actual slopes.
            // Blends in from minSlopeAngleForDownhill so flat ground isn't affected.
            float slopeAngleForBoost = Vector3.Angle(_groundNormal, Vector3.up);
            float slopeBoostT = Mathf.InverseLerp(minSlopeAngleForDownhill, 35f, slopeAngleForBoost);
            float gScale = Mathf.Lerp(1f, groundedSlopeGravityScale, Mathf.Clamp01(slopeBoostT));
            gPlane *= gScale;

            // Always apply slope-parallel gravity, but optionally reduce + damp fall-line drift
            // when the player is strongly traversing and edging at low fall-line speed.
            if (enableTraverseHold && gPlane.sqrMagnitude > 0.0001f)
            {
                float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
                _dbgSlopeAngle = slopeAngle;
                _dbgTraverseGPlane = gPlane;
                _dbgTraverseHold = 0f; // will be overwritten if we engage

                if (slopeAngle >= traverseHoldMinSlopeAngle)
                {
                    Vector3 fallDir = gPlane.normalized;
                    _dbgFallDir = fallDir;

                    // IMPORTANT: do not rely on _skiForward being updated later in the frame
                    Vector3 skiDir = GetCombinedSkiForwardOnPlane();
                    if (skiDir.sqrMagnitude > 0.0001f) skiDir.Normalize();

                    // 1 when perfectly across slope, 0 when pointing up/down the fall line
                    float across = (skiDir.sqrMagnitude > 0.0001f)
                        ? (1f - Mathf.Abs(Vector3.Dot(skiDir, fallDir)))
                        : 0f;
                    _dbgTraverseAcross = across;

                    // Fall-line speed we want to resist (unwanted sideways slide down the hill)
                    Vector3 planeVel = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
                    float vFall = Vector3.Dot(planeVel, fallDir);
                    _dbgTraversePlaneVel = planeVel;
                    _dbgTraverseVFall = vFall;

                    // Engage only when:
                    //  - we're sufficiently across the fall line
                    //  - and fall-line speed is low (standing / slow traverse)
                    // Strength rises as we get more across + more under the speed limit.
                    float acrossGate = Mathf.InverseLerp(traverseHoldAcrossThreshold, 1f, across);
                    float speedGate = 1f - Mathf.Clamp01(Mathf.Abs(vFall) / Mathf.Max(0.01f, traverseHoldSpeed));
                    float hold = Mathf.Clamp01(acrossGate * speedGate) * Mathf.Clamp01(traverseHoldStrength);
                    _dbgTraverseAcrossGate = acrossGate;
                    _dbgTraverseSpeedGate = speedGate;
                    _dbgTraverseHold = hold;

                    // Optional: forward lean reduces holding (keeps downhill skiing lively)
                    hold *= 1f - Mathf.Clamp01(_forwardLean);

                    // 1) Apply tangential gravity, reduced while holding (but never removed entirely)
                    _rb.AddForce(gPlane * (1f - hold), ForceMode.Acceleration);

                    // 2) Extra damping along the fall line to kill slow drift down the hill
                    if (hold > 0f)
                    {
                        float damp = traverseHoldDamp * hold;
                        _rb.AddForce(-fallDir * vFall * damp, ForceMode.Acceleration);
                    }
                }
                else
                {
                    // Too flat for traverse hold to matter
                    _rb.AddForce(gPlane, ForceMode.Acceleration);
                    _dbgTraverseHold = 0f;

                }
            }
            else
            {
                // Traverse hold disabled or no tangential component
                _rb.AddForce(gPlane, ForceMode.Acceleration);
                _dbgTraverseHold = 0f;

            }
        }
        else
        {
            _rb.AddForce(Physics.gravity, ForceMode.Acceleration);
        }


        // Ensure _skiForward is available for alignment sampling even before ApplyGroundForces.
        _skiForward = GetCombinedSkiForwardOnPlane();

        if (IsGroundedForControls)
        {
            // Simplification: use the same ground plane for physics AND alignment to avoid
            // competing "up" targets that create pitch rocking on steepening slopes.
            float t = 1f - Mathf.Exp(-alignNormalSmoothSpeed * Time.fixedDeltaTime);
            _alignNormal = Vector3.Slerp(_alignNormal, _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up, t);
        }
        else
        {
            _alignNormal = Vector3.Slerp(_alignNormal, Vector3.up, 0.05f);
        }
        UpdateBodyCollisionStability();
        UpdateTipContactStability();

        // Track when we leave the ground for jump/landing severity.
        if (_wasGrounded && !_isGrounded)
        {
            _airborneStartTime = Time.time;
            _airbornePeakY = transform.position.y;
            BeginAirEntry(resetYaw: false);
        }

        // Track jump apex for hard-landing / slam detection.
        if (!_isGrounded)
            _airbornePeakY = Mathf.Max(_airbornePeakY, transform.position.y);

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
            _jumpMustFireWhileGrinding = false;
            return;
        }

        var action = jumpAction.action;
        bool pressedThisFrame = action.WasPressedThisFrame();
        bool releasedThisFrame = action.WasReleasedThisFrame();

        if (pressedThisFrame)
        {
            // If jump begins while grinding, allow it to fire only while grind remains active
            // (prevents buffered "surprise jump" after falling off the rail).
            if (_grindActive)
                _jumpMustFireWhileGrinding = true;

            _jumpHeld = true;
            _lastJumpPressedTime = Time.time;
            _jumpQueued = true;
            _jumpReleaseQueued = false;
        }
        else if (releasedThisFrame)
        {
            _jumpHeld = false;

            if (_jumpQueued)
            {
                // We jump on release (existing behavior)
                _jumpReleaseQueued = true;
            }
        }

        // Drop stale queued jumps (unless we're still holding, or waiting for release to fire).
        if (_jumpQueued &&
            !_jumpHeld &&
            !_jumpReleaseQueued &&
            (Time.time - _lastJumpPressedTime > jumpBufferTime))
        {
            _jumpQueued = false;
            _jumpMustFireWhileGrinding = false;
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
            //_leftGrounded = false;
            //_rightGrounded = false;
            _groundNormal = Vector3.up;
            return;
        }

        // ------------------------------------------------------------------
        // 2) Stable ground plane: derive physics ground normal ONLY from the body spherecast.
        //    Ski contacts keep us "grounded" for continuity, but do NOT rewrite the normal.
        // ------------------------------------------------------------------

        bool nearGround = false;
        Vector3 rawNormal = _groundNormal;

        // Reset per-ski ray grounding flags (keep for debug/UI if you like)
        //_leftGrounded = (leftSkiContact != null && leftSkiContact.IsGrounded);
        //_rightGrounded = (rightSkiContact != null && rightSkiContact.IsGrounded);

        // Spherecast from body to get the authoritative physics ground plane
        Vector3 centerOrigin = transform.position + Vector3.up * groundCheckHeight;
        float maxDist = groundCheckHeight + groundCheckDistance;

        bool IsRideable(Vector3 n)
        {
            if (n.sqrMagnitude < 0.0001f) return false;
            float slopeAngle = Vector3.Angle(n.normalized, Vector3.up);
            return slopeAngle <= maxGroundSlopeAngle;
        }

        if (Physics.SphereCast(centerOrigin, groundCheckRadius, Vector3.down,
                               out RaycastHit centerHit, maxDist, groundLayers,
                               QueryTriggerInteraction.Ignore))
        {
            float contactGap = Mathf.Max(0f, centerHit.distance - groundCheckHeight);
            float allowedGap = haveSkiContact ? groundContactDistanceWhenSkiContact : groundContactDistance;

            if (contactGap <= allowedGap && IsRideable(centerHit.normal))
            {
                nearGround = true;
                rawNormal = centerHit.normal.normalized;
                _lastGroundedTime = Time.time;
            }
        }

        bool wasGroundedBefore = _isGrounded;

        // Grounded for physics if we are near the ground by cast OR we have direct ski contact.
        // This prevents state flicker when the body cast misses for a frame.
        _isGrounded = nearGround || haveSkiContact;

        // Update physics ground normal ONLY when the body cast says we're near ground.
        // Otherwise keep the last good normal (do NOT snap to Vector3.up).
        if (nearGround)
        {
            if (!wasGroundedBefore || _groundNormal.sqrMagnitude < 0.0001f)
            {
                _groundNormal = rawNormal;
            }
            else
            {
                float lerp = 1f - Mathf.Exp(-groundNormalSmoothSpeed * Time.fixedDeltaTime);
                Vector3 smoothed = Vector3.Slerp(_groundNormal, rawNormal, lerp);

                // Rate-limit the normal change to avoid "wavy" pitch corrections on micro terrain variation.
                float maxRadians = alignMaxDegreesPerSec * Mathf.Deg2Rad * Time.fixedDeltaTime;
                _groundNormal = Vector3.RotateTowards(_groundNormal, smoothed, maxRadians, 0f);
            }

            // Landing evaluation only when we actually reacquire ground by casts (real landings)
            if (!wasGroundedBefore)
            {
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

        // If the body cast is not near ground but skis ARE contacting, keep the physics plane fresh
        // from the ski contacts. This prevents "stale ground normal" -> tangential gravity misprojection
        // -> micro hops and rocking on steepening slopes.
        if (!nearGround && haveSkiContact)
        {
            Vector3 n = Vector3.zero;

            if (leftSkiContact != null &&
                leftSkiContact.IsGrounded &&
                leftSkiContact.BaseContactAlignment >= MinBaseAlignForGroundNormal &&
                IsRideable(leftSkiContact.ContactNormal))
                n += leftSkiContact.ContactNormal;

            if (rightSkiContact != null &&
                rightSkiContact.IsGrounded &&
                rightSkiContact.BaseContactAlignment >= MinBaseAlignForGroundNormal &&
                IsRideable(rightSkiContact.ContactNormal))
                n += rightSkiContact.ContactNormal;

            if (n.sqrMagnitude > 0.0001f)
            {
                // Smooth toward the ski-derived plane so we don't introduce jitter.
                float t = 1f - Mathf.Exp(-alignNormalSmoothSpeed * Time.fixedDeltaTime);
                _groundNormal = Vector3.Slerp(_groundNormal, n.normalized, t);
            }

            _lastGroundedTime = Time.time;
        }

    }

    // ----------------------------------------------------------------------
    // ANTI-CLIPPING (SKI CLEARANCE)
    // ----------------------------------------------------------------------

    private bool IsRideableNormal(Vector3 n)
    {
        if (n.sqrMagnitude < 0.0001f) return false;
        float slopeAngle = Vector3.Angle(n.normalized, Vector3.up);
        return slopeAngle <= maxGroundSlopeAngle;
    }

    private bool TryComputeRequiredLift(Vector3 up, out float requiredLift)
    {
        float lift = 0f;

        if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
        up.Normalize();

        bool gotAnyHit = false;

        void ConsiderSki(Transform skiT)
        {
            if (skiT == null) return;

            Vector3 origin = skiT.position + up * skiProbeUp;
            float dist = skiProbeUp + skiProbeDown;

            if (Physics.Raycast(origin, -up, out RaycastHit hit, dist, groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsRideableNormal(hit.normal))
                    return;

                gotAnyHit = true;

                // Signed distance of ski point above ground along "up".
                float signedDist = Vector3.Dot(skiT.position - hit.point, up);

                // If the ski point is below desired clearance, lift is needed.
                float need = skiClearance - signedDist;
                if (need > lift)
                    lift = need;
            }
        }

        ConsiderSki(leftSki);
        ConsiderSki(rightSki);

        requiredLift = lift;
        return gotAnyHit && requiredLift > 0.0001f;
    }

    private void ResolveSkiTerrainPenetration(bool hardSnap)
    {
        if (!preventSkiTerrainClipping || _rb == null)
            return;

        // Prefer ground normal when grounded; otherwise use world up.
        Vector3 up = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;

        if (!TryComputeRequiredLift(up, out float liftNeeded))
            return;

        // Cap + smooth the correction to avoid pops.
        float dt = Time.fixedDeltaTime;
        float stepCap = hardSnap ? liftNeeded : Mathf.Min(liftNeeded, maxLiftPerFixedStep);
        float t = hardSnap ? 1f : (1f - Mathf.Exp(-liftResponse * dt));
        float lift = stepCap * t;

        _rb.MovePosition(_rb.position + up * lift);

        // Optional safety: if we're pushing upward, kill any velocity component INTO the ground.
        if (hardSnap)
        {
            Vector3 v = _rb.linearVelocity;
            float vInto = Vector3.Dot(v, -up); // positive means moving down into ground along up
            if (vInto > 0f)
                v += up * vInto;

            _rb.linearVelocity = v;
        }
    }

    /// <summary>
    /// Public entry point for other scripts (WalkingController) to ensure we start skiing above the ground.
    /// Also useful when recovering from weird landings / transitions.
    /// </summary>
    public void SnapToGroundClearance(bool resetDownwardVelocity = true, int iterations = 2)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_rb == null) return;

        // Run a few passes because lifting changes ray origins on steep slopes.
        for (int i = 0; i < Mathf.Max(1, iterations); i++)
            ResolveSkiTerrainPenetration(hardSnap: true);

        if (resetDownwardVelocity)
        {
            Vector3 up = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;

            Vector3 v = _rb.linearVelocity;
            float vInto = Vector3.Dot(v, -up);
            if (vInto > 0f)
                v += up * vInto;

            _rb.linearVelocity = v;
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
    /// Computes the nose-dig angle for a single ski, or 0 if it's not an end-heavy contact.
    /// (Uses SkiContact.HasTipContact which now means "tip OR tail".)
    /// </summary>
    private float ComputeTipDigAngleForSki(
        Transform ski,
        SkiContact contact,
        Vector3 groundNormal)
    {
        // Use SkiContact as the primary source of truth for end digs.
        if (contact == null || !contact.IsGrounded)
            return 0f;


        if (ski == null)
            ski = contact.transform;

        if (groundNormal.sqrMagnitude < 0.0001f)
            return 0f;

        // For end-stands, treat both tip (+Z) and tail (-Z) as a "dig" into the surface.
        // Tip dig uses +forward; tail dig uses -forward.
        Vector3 dir = ski.forward;
        if (contact.EndContactSign < 0)
            dir = -dir;

        // Only consider when the end direction is pointing at least slightly into the surface.
        float intoGround = Mathf.Max(0f, -Vector3.Dot(dir, groundNormal));
        if (intoGround <= 0f)
            return 0f;

        // End direction flattened onto the slope plane.
        Vector3 dirOnPlane = Vector3.ProjectOnPlane(dir, groundNormal);
        if (dirOnPlane.sqrMagnitude < 0.0001f)
            return 0f;

        dirOnPlane.Normalize();

        // Angle between actual end direction and its flattened version = dig amount.
        return Vector3.Angle(dirOnPlane, dir);
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
        bool anyEndContact =
            (leftSkiContact != null && leftSkiContact.HasTipContact) ||
            (rightSkiContact != null && rightSkiContact.HasTipContact);

        float tipDigAngle = anyEndContact ? ComputeTipDigAngle() : 0f;

        // Planar components for tilt / sideways checks.
        Vector3 velOnPlane = Vector3.ProjectOnPlane(vel, groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        // Tilt relative to the ground.
        float tiltAngle = Vector3.Angle(transform.up, groundNormal);

        // ---- Hard landing / slam detection ----
        // Measure how 'into the slope' the impact is. 0° = along the slope plane, 90° = straight into the slope.
        float impactAngleFromPlane = Mathf.Atan2(downwardSpeed, Mathf.Max(0.0001f, planarSpeed)) * Mathf.Rad2Deg;

        // Fall height from the highest point during airtime to the landing point.
        float fallHeight = Mathf.Max(0f, _airbornePeakY - transform.position.y);

        bool isHardSlam =
            fallHeight >= hardLandingMinFallHeight &&
            downwardSpeed >= hardLandingMinDownwardSpeed &&
            impactAngleFromPlane >= hardLandingMinImpactAngleFromPlane;

        if (isHardSlam)
        {
            _dbgLastStackReason = "HardSlam";
            TriggerStack(ComputeStackTorqueAxisFromContacts());
            return;
        }

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

        _dbgLandingAirTime = airTime;
        _dbgLandingDownwardSpeed = downwardSpeed;
        _dbgLandingPlanarSpeed = planarSpeed;
        _dbgLandingImpactAngleFromPlane = impactAngleFromPlane;
        _dbgLandingFallHeight = fallHeight;
        _dbgLandingMisalignAngle = misalignAngle;
        _dbgLandingIsTiny = isTinyLanding;
        _dbgLandingIsHardSlam = isHardSlam;

        // For tiny landings, always stick and gently glue to the slope.
        if (isTinyLanding)
        {
            float microProjection = Mathf.Clamp01(landingProjectionStrength * 0.5f);
            PreserveLandingVelocityOnSlope(microProjection);
            _landingAssistUntil = Time.time + landingAlignBoostDuration;
            _movementMode = MovementMode.Skiing;
            return;
        }

        // ----- 2. Slope-aware thresholds -----

        float SteepSlopeStart = landingSteepSlopeStartAngle;
        float VerySteepSlope = landingVerySteepSlopeAngle;


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
                TriggerStack(ComputeStackTorqueAxisFromContacts());
            }
            else
            {
                PreserveLandingVelocityOnSlope(projectionOnSteep);
                _landingAssistUntil = Time.time + landingAlignBoostDuration;
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
            TriggerStack(ComputeStackTorqueAxisFromContacts());
            return;
        }

        PreserveLandingVelocityOnSlope(projectionOnSteep);
        _landingAssistUntil = Time.time + landingAlignBoostDuration;
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

        // Blend from current velocity toward the along-slope target based on how severe the landing was.
        Vector3 newVel = Vector3.Lerp(vel, targetVel, projectionFactor);

        // Never keep an "into ground" component after we've decided we're landing.
        float into = Vector3.Dot(newVel, groundNormal);
        if (into < 0f)
        {
            newVel -= groundNormal * into; // cancels negative component into the surface
        }

        _rb.linearVelocity = newVel;
    }

    private void TriggerStack()
    {
        TriggerStack(ComputeStackTorqueAxisFromContacts());
    }

    private void TriggerStack(Vector3 torqueAxisWorld)
    {
        if (_stacked) return;

        _stacked = true;
        _rb.freezeRotation = false;

        // When we stack, we want a decisive fall rather than lingering in a half-rotated state.
        _rb.angularVelocity = Vector3.zero;

        // Prevent hard impacts from forcing the rigidbody through the terrain.
        // Remove any into-ground component and keep a small amount of planar velocity so the wipeout feels natural.
        {
            Vector3 n = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
            Vector3 v = _rb.linearVelocity;

            float into = Vector3.Dot(v, n);
            if (into < 0f)
                v -= n * into; // cancel into-ground

            v = Vector3.ProjectOnPlane(v, n) * stackPlanarVelocityRetention;
            _rb.linearVelocity = v;
        }

        if (torqueAxisWorld.sqrMagnitude < 0.0001f)
            torqueAxisWorld = Random.onUnitSphere;

        _rb.AddTorque(torqueAxisWorld.normalized * stackTorqueImpulse, ForceMode.Impulse);
    }

    /// <summary>
    /// If any non-ski part of the body is colliding with the ground while the
    /// skier is beyond the safe tilt limit, we immediately stack.
    ///
    /// This closes the "slide on head" loophole where only ski contacts were
    /// considered for stacking.
    /// </summary>
    private void UpdateBodyCollisionStability()
    {
        if (_stacked)
        {
            _hasNonSkiGroundContact = false;
            return;
        }

        if (!_hasNonSkiGroundContact)
            return;

        Vector3 n = _nonSkiGroundContactNormal.sqrMagnitude > 0.0001f
            ? _nonSkiGroundContactNormal.normalized
            : (_groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up);

        // Only stack when the body contact happens while we're meaningfully past
        // the safe tilt envelope (prevents false stacks from harmless capsule scrapes).
        float tilt = Vector3.Angle(transform.up, n);
        if (tilt > maxLandingTiltAngle)
        {
            // Use the contact normal for the stack impulse so we don't cancel velocity
            // against a stale ground normal from a prior frame.
            _groundNormal = n;

            TriggerStack(ComputeStackTorqueAxisFromPoint(_nonSkiGroundContactPoint));
        }

        // Consume the cache. OnCollisionStay will repopulate next physics step if still colliding.
        _hasNonSkiGroundContact = false;
    }

    /// <summary>
    /// Deterministic fall axis from a world-space point (typically a non-ski body impact).
    /// Points in front/back pitch you forward/back; points to the side roll you.
    /// </summary>
    private Vector3 ComputeStackTorqueAxisFromPoint(Vector3 pointWorld)
    {
        Vector3 local = transform.InverseTransformPoint(pointWorld);

        // Prefer pitching if the point is more forward/back than side-to-side.
        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
        {
            // Torque about player-right produces forward/back pitch.
            float sign = (local.z >= 0f) ? 1f : -1f;
            return transform.right * sign;
        }
        else
        {
            // Torque about player-forward produces left/right roll.
            float sign = (local.x >= 0f) ? 1f : -1f;
            return transform.forward * sign;
        }
    }

    /// <summary>
    /// Picks a deterministic fall axis based on the worst current ski contact.
    /// Tip/tail stands should pitch you forward/back; edge stands should roll you.
    /// </summary>
    private Vector3 ComputeStackTorqueAxisFromContacts()
    {
        SkiContact worst = null;
        float worstAlign = 999f;

        if (leftSkiContact != null && leftSkiContact.IsGrounded)
        {
            float a = leftSkiContact.BaseContactAlignment;
            if (a < worstAlign)
            {
                worst = leftSkiContact;
                worstAlign = a;
            }
        }

        if (rightSkiContact != null && rightSkiContact.IsGrounded)
        {
            float a = rightSkiContact.BaseContactAlignment;
            if (a < worstAlign)
            {
                worst = rightSkiContact;
                worstAlign = a;
            }
        }

        if (worst == null)
            return Random.onUnitSphere;

        // Prefer forward/back fall for end (tip/tail) contacts.
        if (worst.HasTipContact && worst.EndContactSign != 0)
        {
            // Torque about player-right produces forward/back pitch
            return transform.right * worst.EndContactSign;
        }

        // Otherwise, roll toward the side the ski's up is leaning to.
        float side = Vector3.Dot(worst.transform.up, transform.right);
        float sign = (side >= 0f) ? 1f : -1f;

        // Torque about player-forward produces left/right roll
        return transform.forward * sign;
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

        // Dynamic threshold for "this is still the bottom face of the ski".
        // At low speeds we require a flatter base; at high speeds we allow more edging for carving.
        Vector3 velOnPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        float speedT = Mathf.InverseLerp(0.5f, 12f, planarSpeed);
        float minBaseAlignmentForStableStance = Mathf.Lerp(0.65f, 0.35f, speedT);

        // If a ski is this low, it is essentially on its side/top. Treat as immediate fall.
        const float SevereEdgeAlignment = 0.12f;

        bool leftBaseStable = leftGrounded && leftBaseAlign >= minBaseAlignmentForStableStance;
        bool rightBaseStable = rightGrounded && rightBaseAlign >= minBaseAlignmentForStableStance;

        bool anyStableBase = leftBaseStable || rightBaseStable;

        // If NEITHER ski has a reasonably flat base contact, we're effectively on edges/tips on both skis.
        // Stack immediately.
        if (!anyStableBase)
        {
            TriggerStack(ComputeStackTorqueAxisFromContacts());
            _tipContactAccumTime = 0f;
            return;
        }

        // If we're basically stationary, don't allow balancing on a strong edge.
        if (planarSpeed <= 1.0f)
        {
            float worstBase = 1f;
            if (leftGrounded) worstBase = Mathf.Min(worstBase, leftBaseAlign);
            if (rightGrounded) worstBase = Mathf.Min(worstBase, rightBaseAlign);

            if (worstBase < 0.5f)
            {
                TriggerStack(ComputeStackTorqueAxisFromContacts());
                _tipContactAccumTime = 0f;
                return;
            }
        }

        // If any grounded ski is essentially sideways/upside-down, fall immediately.
        if ((leftGrounded && leftBaseAlign < SevereEdgeAlignment) ||
            (rightGrounded && rightBaseAlign < SevereEdgeAlignment))
        {
            TriggerStack(ComputeStackTorqueAxisFromContacts());
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
            // On steeper slopes, allow more time before stacking so normal downhill skiing
            // over steepening terrain doesn't instantly become a "tip-stand" failure.
            float slopeAngle = Vector3.Angle((_alignNormal.sqrMagnitude > 0.0001f ? _alignNormal : _groundNormal), Vector3.up);
            float slopeT = Mathf.InverseLerp(25f, 65f, slopeAngle);
            float allowedTime = Mathf.Lerp(TipContactStackTime, TipContactStackTime * 1.6f, slopeT);

            // Only accumulate tip-failure time when we're also meaningfully not on our bases.
            // This prevents steep-slope transitions from counting as a failure if base contact is still decent.
            float worstBaseAlign = 1f;
            if (leftGrounded) worstBaseAlign = Mathf.Min(worstBaseAlign, leftBaseAlign);
            if (rightGrounded) worstBaseAlign = Mathf.Min(worstBaseAlign, rightBaseAlign);

            bool trulyUnstable = worstBaseAlign < (minBaseAlignmentForStableStance * 0.85f);

            if (trulyUnstable && _tipContactAccumTime >= allowedTime)
            {
                TriggerStack(ComputeStackTorqueAxisFromContacts());
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
            // IMPORTANT:
            // bodyTransform must be a VISUAL child pivot, not the Rigidbody root.
            // If bodyTransform == transform, then forward lean will physically pitch the whole controller
            // in Update(), fighting ground alignment and allowing tip-riding.
            if (bodyTransform == transform)
            {
                // Do not apply visual lean to the physics root.
                // (Set bodyTransform to a child object that only drives the mesh/rig visuals.)
            }
            else
            {
                float pitch = _forwardLean * maxForwardLeanAngle;
                float roll = -_sideLean * maxSideLeanAngle;
                bodyTransform.localRotation = Quaternion.Euler(pitch, 0f, roll);
            }
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

        // Lean modulation:
        // - Forward lean (tuck) reduces friction, making you glide faster.
        // - Backward lean increases friction, making you scrub speed.
        // This preserves "lean moderates speed" without injecting a new acceleration vector.
        float tuck = Mathf.Clamp01(_forwardLean);          // 0..1
        float brake = Mathf.Clamp01(-_forwardLean);        // 0..1

        float frictionScale =
            1f
            - tuck * tuckFrictionReduction
            + brake * brakeFrictionIncrease;

        // Slight extra slip when neutral leaning on slopes (feels like gravity is doing the work).
        // Only apply when we're actually on a slope.
        float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
        float slopeT = Mathf.InverseLerp(minSlopeAngleForDownhill, 20f, slopeAngle);
        float neutral = 1f - Mathf.Clamp01(Mathf.Abs(_forwardLean));
        frictionScale *= 1f - neutralSlipBoost * neutral * slopeT;

        float forwardFric = forwardFriction * edgeFactor * frictionScale;
        float sideFric = sideFriction * edgeFactor * frictionScale;

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
                Vector3 skiDirN = skiDir.normalized;

                // Angle between current travel direction and where the skis are pointing.
                // Large angle + high carveFactor at speed => intentional skid/quick-stop.
                float turnAngle = Vector3.Angle(planeDir, skiDirN);

                // Normal carve steering (existing behaviour).
                float steerAmount = carveFactor * carveSteerStrength * Time.fixedDeltaTime;
                Vector3 steeredDir = Vector3.Slerp(planeDir, skiDirN, steerAmount).normalized;

                // Apply steering while preserving speed (for now).
                Vector3 normalComponent = Vector3.Project(newVel, _groundNormal);
                float newPlaneSpeed = planeSpeed;

                // --- Quick Stop: extra planar damping when turning sharply at speed ---
                // Uses exponential damping for stability across framerates.
                if (quickStopStrength > 0f && planeSpeed >= quickStopMinSpeed && turnAngle >= quickStopMinTurnAngle)
                {
                    float angleT = Mathf.InverseLerp(quickStopMinTurnAngle, 90f, turnAngle);
                    float stopT = carveFactor * angleT;

                    // Exponential damping: speed *= exp(-k * dt)
                    float k = quickStopStrength * stopT;
                    float speedMul = Mathf.Exp(-k * Time.fixedDeltaTime);
                    newPlaneSpeed *= speedMul;
                }

                newVel = steeredDir * newPlaneSpeed + normalComponent;
                _rb.linearVelocity = newVel;
            }
        }

    }

    private void TryConsumeQueuedJump()
    {
        if (!_jumpQueued)
            return;

        // We only jump on release (your existing design).
        if (!_jumpReleaseQueued)
            return;

        // Can't jump while stacked.
        if (_stacked)
        {
            _jumpQueued = false;
            _jumpReleaseQueued = false;
            _jumpMustFireWhileGrinding = false;
            return;
        }

        // Allow jump when grounded, shortly after leaving ground (coyote), OR while grinding.
        bool withinCoyote = !_isGrounded && (Time.time - _lastGroundedTime <= jumpCoyoteTime);
        bool canJumpNow = _isGrounded || withinCoyote || _grindActive;

        if (!canJumpNow)
        {
            // If this was a grind-started jump and we are no longer grinding, cancel it.
            if (_jumpMustFireWhileGrinding && !_grindActive)
            {
                _jumpQueued = false;
                _jumpReleaseQueued = false;
                _jumpMustFireWhileGrinding = false;
                return;
            }

            // Otherwise keep it buffered until we land or it expires.
            if (Time.time - _lastJumpPressedTime > jumpBufferTime)
            {
                _jumpQueued = false;
                _jumpReleaseQueued = false;
                _jumpMustFireWhileGrinding = false;
            }
            return;
        }

        // If it's been buffered too long, don't jump.
        if (Time.time - _lastJumpPressedTime > jumpBufferTime)
        {
            _jumpQueued = false;
            _jumpReleaseQueued = false;
            _jumpMustFireWhileGrinding = false;
            return;
        }

        // If we are grinding, detach right before applying the impulse so grind forces do not dampen it.
        if (_grindActive)
        {
            _grindDetachRequested = false;  // avoid double-pop if you have separate detach logic
            _grindActive = false;

            // If your grind system uses these, keep them; if not present, remove these lines:
            _grindStrengthSmoothed = 0f;
            _grindReattachCooldownUntil = Time.time + Mathf.Max(0f, grindReattachCooldown);
        }

        // ------------------------------------------------------------------
        // 1. Compute charge-based jump strength (base force magnitude).
        // ------------------------------------------------------------------
        float holdDuration = Mathf.Max(0f, Time.time - _lastJumpPressedTime);
        float chargeT = maxJumpChargeTime > 0f
            ? Mathf.Clamp01(holdDuration / maxJumpChargeTime)
            : 1f;
        _dbgJumpChargeT = chargeT;

        float minForce = Mathf.Min(jumpForceRange.x, jumpForceRange.y);
        float maxForce = Mathf.Max(jumpForceRange.x, jumpForceRange.y);
        float jumpForce = Mathf.Lerp(minForce, maxForce, chargeT);

        // ------------------------------------------------------------------
        // 1b. Scale jump force based on current planar speed.
        // ------------------------------------------------------------------
        Vector3 velocity = _rb.linearVelocity;
        Vector3 velOnPlane = Vector3.ProjectOnPlane(velocity, _groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        float speedT = jumpSpeedForMaxMultiplier > 0f
            ? Mathf.Clamp01(planarSpeed / jumpSpeedForMaxMultiplier)
            : 0f;

        float speedMultiplier = Mathf.Lerp(1f, jumpSpeedForceMultiplier, speedT);
        _dbgJumpSpeedMultiplier = speedMultiplier;

        jumpForce *= speedMultiplier;

        // ------------------------------------------------------------------
        // 2. Jump direction (slope-aware)
        // ------------------------------------------------------------------
        Vector3 baseDir = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        float slopeAngle = Vector3.Angle(baseDir, Vector3.up);
        float upBlendT = Mathf.InverseLerp(jumpUpBlendStartAngle, jumpUpBlendEndAngle, slopeAngle);
        Vector3 jumpDir = Vector3.Slerp(baseDir, Vector3.up, Mathf.Clamp01(upBlendT)).normalized;

        _dbgJumpSlopeAngle = slopeAngle;
        _dbgJumpUpBlendT = Mathf.Clamp01(upBlendT);
        _dbgJumpDir = jumpDir;

        // Cancel any into-surface component so we always "pop".
        float intoSurface = Vector3.Dot(_rb.linearVelocity, jumpDir);
        _dbgJumpIntoSurface = intoSurface;

        if (intoSurface < 0f)
            _rb.linearVelocity -= jumpDir * intoSurface;

        _dbgJumpForce = jumpForce;

        // ------------------------------------------------------------------
        // 3. Apply the impulse.
        // ------------------------------------------------------------------
        _rb.AddForce(jumpDir * jumpForce, ForceMode.VelocityChange);

        _lastJumpTime = Time.time;
        _isGrounded = false;
        _airborneStartTime = _lastJumpTime;

        BeginAirEntry(resetYaw: false);

        if (HasLegInputs)
        {
            float yawInput = Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f);
            _airAngularVelocity.y = yawInput * airYawTurnSpeed * 0.5f;
        }

        _jumpQueued = false;
        _jumpReleaseQueued = false;
        _jumpMustFireWhileGrinding = false;
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

        // ------------------------------------------------------------
        // Uphill penalty: reduce (and optionally counter) skate pushes
        // when the skier is attempting to push uphill on steep slopes.
        // ------------------------------------------------------------
        float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);

        // Downhill direction on the current ground plane.
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
        float downhillMag = downhill.magnitude;

        if (downhillMag > 0.0001f)
        {
            downhill /= downhillMag;

            // 0 = not uphill, 1 = fully uphill (directly opposite downhill).
            float uphillness = Mathf.Clamp01(Vector3.Dot(pushDir, -downhill));

            if (uphillness > 0.0001f)
            {
                float slopeT = Mathf.InverseLerp(skateUphillPenaltyStartAngle, skateUphillPenaltyEndAngle, slopeAngle);

                if (slopeT > 0f)
                {
                    // Blend impulse down toward a minimum as slope+uphillness increase.
                    float penaltyT = Mathf.Clamp01(slopeT * uphillness);
                    float minFactor = Mathf.Clamp01(skateUphillMinImpulseFactor);
                    float impulseFactor = Mathf.Lerp(1f, minFactor, penaltyT);

                    impulse *= impulseFactor;

                    // Optional extra downhill pull so very steep uphill faces "win" against skating.
                    if (skateUphillDownPull > 0.01f)
                    {
                        _rb.AddForce(downhill * (skateUphillDownPull * penaltyT), ForceMode.Acceleration);
                    }
                }
            }
        }

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
        // Desired forward comes from skis (already projected on the physics plane).
        Vector3 skiDir = _skiForward;
        if (skiDir.sqrMagnitude < 0.0001f)
        {
            skiDir = Vector3.ProjectOnPlane(transform.forward, _groundNormal);
            if (skiDir.sqrMagnitude < 0.0001f)
                skiDir = Vector3.ProjectOnPlane(Vector3.forward, _groundNormal);
        }
        skiDir.Normalize();

        Vector3 desiredUp = (_alignNormal.sqrMagnitude > 0.0001f) ? _alignNormal.normalized : _groundNormal.normalized;
        if (desiredUp.sqrMagnitude < 0.0001f) desiredUp = Vector3.up;

        // Project desired forward onto the plane defined by desiredUp (so we don't introduce pitch into "forward").
        Vector3 desiredForward = Vector3.ProjectOnPlane(skiDir, desiredUp);
        if (desiredForward.sqrMagnitude < 0.0001f)
            desiredForward = Vector3.ProjectOnPlane(transform.forward, desiredUp);
        if (desiredForward.sqrMagnitude < 0.0001f)
            desiredForward = Vector3.ProjectOnPlane(Vector3.forward, desiredUp);
        desiredForward.Normalize();

        Quaternion current = transform.rotation;

        // ------------------------------------------------------------
        // 1) Strong pitch/roll alignment: align UP to desiredUp quickly.
        //    This is what stops the skier pitching onto the tips.
        // ------------------------------------------------------------
        Quaternion upCorrection = Quaternion.FromToRotation(current * Vector3.up, desiredUp);
        Quaternion upAligned = upCorrection * current;

        // Scale pitch/roll correction by how "base-flat" our contacts are.
        // When contacts are end/edge-heavy, desiredUp becomes noisy on micro terrain,
        // so we reduce correction rate to avoid "rocking waves".
        // Always correct pitch/roll at a consistent rate.
        // Noise is handled by ground-normal smoothing/rate limiting, not by weakening correction on tip frames.
        float landingBoost = (Time.time <= _landingAssistUntil) ? landingAlignBoostMultiplier : 1f;

        float maxUpStep = (alignMaxDegreesPerSec * landingBoost) * Time.fixedDeltaTime;
        current = Quaternion.RotateTowards(current, upAligned, maxUpStep);


        // ------------------------------------------------------------
        // 2) Yaw turning: keep your stability-scaled yaw response.
        // ------------------------------------------------------------
        float baseAlign = 0f;
        if (leftSkiContact != null && leftSkiContact.IsGrounded) baseAlign = Mathf.Max(baseAlign, leftSkiContact.BaseContactAlignment);
        if (rightSkiContact != null && rightSkiContact.IsGrounded) baseAlign = Mathf.Max(baseAlign, rightSkiContact.BaseContactAlignment);

        float stability = Mathf.Clamp01((baseAlign - 0.15f) / (0.6f - 0.15f));
        float yawSpeed = (groundTurnSpeed * stability) * landingBoost;

        Quaternion yawTarget = Quaternion.LookRotation(desiredForward, desiredUp);
        float yawT = 1f - Mathf.Exp(-yawSpeed * Time.fixedDeltaTime);
        current = Quaternion.Slerp(current, yawTarget, yawT);

        _rb.MoveRotation(current);
    }

    // ----------------------------------------------------------------------
    // AIR CONTROL
    // ----------------------------------------------------------------------
    private void BeginAirEntry(bool resetYaw)
    {
        _airEntryTime = Time.time;
        _airLeanBaseline = _rawLeanInput;

        // Prevent immediate forward tipping when the player is already leaning at takeoff.
        // Pitch control in air will be driven by delta-from-baseline (see ApplyAirControl).
        _airAngularVelocity.x = 0f;

        if (resetYaw)
            _airAngularVelocity.y = 0f;
    }

    // ----------------------------------------------------------------------
    // GRINDING (candidate selection)
    // ----------------------------------------------------------------------

    private struct GrindCandidate
    {
        public Vector3 closestPoint;
        public Vector3 tangent;
        public string sourceName;
        public Vector3 delta; // closestPoint - referencePoint
        public float sqDist;
    }

    private Vector3 GetGrindReferencePoint()
    {
        // Prefer ski mid probes if available (stable near bindings).
        Vector3 lp = default;
        Vector3 rp = default;

        bool gotL = (leftSkiContact != null) &&
                    leftSkiContact.TryGetProbeContact(SkiContact.SkiProbeRegion.Mid, out lp, out _);

        bool gotR = (rightSkiContact != null) &&
                    rightSkiContact.TryGetProbeContact(SkiContact.SkiProbeRegion.Mid, out rp, out _);

        if (gotL && gotR) return (lp + rp) * 0.5f;
        if (gotL) return lp;
        if (gotR) return rp;

        // IMPORTANT: when airborne, probes often won't return contacts.
        // Fall back to actual ski transforms (still near the rail/cable if you're lining it up).
        Transform lT = (leftSkiContact != null) ? leftSkiContact.transform : null;
        Transform rT = (rightSkiContact != null) ? rightSkiContact.transform : null;

        if (lT != null && rT != null) return (lT.position + rT.position) * 0.5f;
        if (lT != null) return lT.position;
        if (rT != null) return rT.position;

        // Final fallback to rigidbody position (COM-ish).
        return _rb != null ? _rb.position : transform.position;
    }

    private bool TryFindBestGrindCandidate(Vector3 referencePoint, float maxDist, out GrindCandidate best)
    {
        best = default;

        float maxSq = maxDist * maxDist;
        float bestSq = maxSq;

        // 1) Optional collider-based detection (good for fence segments, rails with triggers, etc)
        if (grindLayers.value != 0)
        {
            // Non-alloc overlap
            int hitCount = Physics.OverlapSphereNonAlloc(referencePoint, grindSearchRadius, _grindOverlap, grindLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Collider c = _grindOverlap[i];
                if (c == null) continue;

                // Prefer provider on parent objects.
                // FencePath:
                FencePath fp = c.GetComponentInParent<FencePath>();
                if (fp != null && fp.TryGetClosestPointOnPath(referencePoint, out _, out Vector3 cp, out Vector3 tan))
                {
                    Vector3 d = cp - referencePoint;
                    float sq = d.sqrMagnitude;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        best.closestPoint = cp;
                        best.tangent = tan;
                        best.delta = d;
                        best.sqDist = sq;
                        best.sourceName = fp.name;
                    }
                }

                // LiftLine (in case you later add colliders along the cable)
                LiftLine ll = c.GetComponentInParent<LiftLine>();
                if (ll != null)
                {
                    // Ensure band exists (some lines may not have rebuilt yet in play mode).
                    if (ll.BandLength <= 0.0001f)
                        ll.RebuildAnalyticLoop();

                    if (ll.TryGetClosestPointOnBand(referencePoint, out _, out Vector3 cp2, out Vector3 tan2))
                    {
                        Vector3 d2 = cp2 - referencePoint;
                        float sq2 = d2.sqrMagnitude;
                        if (sq2 < bestSq)
                        {
                            bestSq = sq2;
                            best.closestPoint = cp2;
                            best.tangent = tan2;
                            best.delta = d2;
                            best.sqDist = sq2;
                            best.sourceName = ll.name;
                        }
                    }
                }

            }
        }

        // 2) Lift cables via registry (no colliders required)
        for (int i = 0; i < LiftLine.ActiveLiftLines.Count; i++)
        {
            LiftLine ll = LiftLine.ActiveLiftLines[i];
            if (ll == null) continue;

            // Ensure band exists (TryGetClosestPointOnBand returns false if _bandLength <= 0).
            if (ll.BandLength <= 0.0001f)
                ll.RebuildAnalyticLoop();

            if (!ll.TryGetClosestPointOnBand(referencePoint, out _, out Vector3 cp, out Vector3 tan))
                continue;

            Vector3 d = cp - referencePoint;
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best.closestPoint = cp;
                best.tangent = tan;
                best.delta = d;
                best.sqDist = sq;
                best.sourceName = ll.name;
            }
        }

        // 3) Fence paths via registry (works even if segments have no colliders)
        for (int i = 0; i < FencePath.ActiveFencePaths.Count; i++)
        {
            FencePath fp = FencePath.ActiveFencePaths[i];
            if (fp == null) continue;

            if (!fp.TryGetClosestPointOnPath(referencePoint, out _, out Vector3 cp, out Vector3 tan))
                continue;

            Vector3 d = cp - referencePoint;
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best.closestPoint = cp;
                best.tangent = tan;
                best.delta = d;
                best.sqDist = sq;
                best.sourceName = fp.name;
            }
        }

        return bestSq < maxSq;
    }

    // Non-alloc overlap cache
    private readonly Collider[] _grindOverlap = new Collider[24];

    private void ApplyAirControl()
    {
        float dt = Time.fixedDeltaTime;

        // SAFETY: if we're still grounded or any ski is in contact, do NOT apply
        // aerial spin/flip controls. Instead, gently damp out any carried air spin.
        if (_isGrounded || HasAnySkiContact)
        {
            float damping = Mathf.Max(0f, airAngularDamping);
            _airAngularVelocity = Vector3.MoveTowards(_airAngularVelocity, Vector3.zero, damping * dt);

            _grindActive = false;
            _grindStrengthSmoothed = Mathf.MoveTowards(_grindStrengthSmoothed, 0f, grindStrengthResponse * dt);
            _dbgGrindSource = null;

            return;
        }

        // ------------------------------------------------------------------
        // Air-edge assist (Grinding) — sits between airborne and grounded
        // ------------------------------------------------------------------
        Vector3 referencePoint = GetGrindReferencePoint();

        float planarSpeed = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal).magnitude;
        bool speedOk = planarSpeed >= grindMinPlanarSpeed;

        float targetGrindStrength = 0f;

        if (grindEnabled && speedOk && Time.time >= _grindReattachCooldownUntil)
        {
            if (TryFindBestGrindCandidate(referencePoint, grindCaptureRadius, out GrindCandidate cand))
            {
                Vector3 tan = cand.tangent;
                if (tan.sqrMagnitude < 0.0001f)
                    tan = Vector3.forward;
                tan.Normalize();

                Vector3 skiFwd = _skiForward;
                if (skiFwd.sqrMagnitude < 0.0001f)
                    skiFwd = transform.forward;
                skiFwd.Normalize();

                // Sideways requirement: engage only if skis are sideways to rail tangent.
                float dot = Mathf.Abs(Vector3.Dot(skiFwd, tan));
                float sideways01 = 1f - Mathf.InverseLerp(grindSidewaysDotFull, grindSidewaysDotBegin, dot);
                sideways01 = Mathf.Clamp01(sideways01);

                // Proximity: stronger when closer to the rail.
                float dist = Mathf.Sqrt(Mathf.Max(0f, cand.sqDist));
                float prox01 = 1f - Mathf.Clamp01(dist / Mathf.Max(0.0001f, grindCaptureRadius));
                // Smoothstep for stability.
                prox01 = prox01 * prox01 * (3f - 2f * prox01);

                targetGrindStrength = prox01 * sideways01;

                _grindClosestPoint = cand.closestPoint;
                _grindTangent = tan;
                _grindDeltaToRail = cand.delta;
                _dbgGrindSource = cand.sourceName;
            }
            else
            {
                _dbgGrindSource = null;
            }
        }
        else
        {
            _dbgGrindSource = null;
        }

        // Smooth engagement so it feels earned, not snapped.
        float resp = Mathf.Max(0.1f, grindStrengthResponse);
        _grindStrengthSmoothed = Mathf.MoveTowards(_grindStrengthSmoothed, targetGrindStrength, resp * dt);
        _grindActive = _grindStrengthSmoothed > 0.05f;

        // Detach request (Jump while grinding) — apply a small pop and enforce cooldown.
        if (_grindDetachRequested && _grindActive)
        {
            _grindDetachRequested = false;

            Vector3 away = (_grindDeltaToRail.sqrMagnitude > 0.0001f) ? (-_grindDeltaToRail.normalized) : -_grindTangent;
            Vector3 pop = Vector3.up * grindDetachUpVel + away * grindDetachAwayVel;

            _rb.AddForce(pop, ForceMode.VelocityChange);

            _grindReattachCooldownUntil = Time.time + Mathf.Max(0f, grindReattachCooldown);
            _grindStrengthSmoothed = 0f;
            _grindActive = false;

            // Also kill air spin a bit on detach so it doesn’t instantly re-flip.
            _airAngularVelocity = Vector3.MoveTowards(_airAngularVelocity, Vector3.zero, (airAngularDamping + grindAngularDamping) * dt);
        }

        // Apply rail constraint + friction if engaged.
        if (_grindStrengthSmoothed > 0.0001f)
        {
            float s = _grindStrengthSmoothed;

            Vector3 delta = _grindDeltaToRail;
            float dist = delta.magnitude;

            if (dist > 0.0001f)
            {
                Vector3 radialDir = delta / dist;

                // Grind "contact normal" points away from the rail toward the skier.
                _grindNormal = (-radialDir).sqrMagnitude > 0.0001f ? (-radialDir).normalized : Vector3.up;

                // Align the skier's up axis to the grind normal (sits between airborne and grounded).
                ApplyGrindAlignment(dt, _grindNormal, s);

                // Spring toward rail centerline (clamp magnitude so it can't spike).
                Vector3 clampedDelta = Vector3.ClampMagnitude(delta, grindCaptureRadius);
                Vector3 springAcc = clampedDelta * (grindSpring * s);

                // Damping into/out of the rail
                float vRad = Vector3.Dot(_rb.linearVelocity, radialDir);
                Vector3 radialDampAcc = -radialDir * (vRad * grindRadialDamping * s);

                // Tangent friction model: damp motion off the tangent strongly, tangent lightly.
                Vector3 tan = _grindTangent;
                if (tan.sqrMagnitude < 0.0001f) tan = Vector3.forward;
                tan.Normalize();

                Vector3 v = _rb.linearVelocity;
                Vector3 vTan = Vector3.Dot(v, tan) * tan;
                Vector3 vOff = v - vTan;

                float fric = Mathf.Max(0f, grindFrictionMultiplier);

                Vector3 offAcc = -vOff * (grindOffTangentFriction * fric * s);
                Vector3 tanAcc = -vTan * (grindAlongTangentFriction * fric * s);

                // Optional "drive" so grinds can build speed over time (especially on flat rails).
                Vector3 driveAcc = Vector3.zero;
                if (grindDriveAcceleration > 0.0001f && grindDriveMaxSpeed > 0.1f)
                {
                    float vTanSigned = Vector3.Dot(v, tan); // signed speed along tangent
                    float vTanAbs = Mathf.Abs(vTanSigned);

                    // Asymptotically accelerate toward grindDriveMaxSpeed (no runaway).
                    float speed01 = Mathf.Clamp01(vTanAbs / grindDriveMaxSpeed);
                    float grindAccel = grindDriveAcceleration * (1f - speed01);

                    // Keep accelerating in the current travel direction along the rail.
                    float dir = (vTanSigned >= 0f) ? 1f : -1f;
                    driveAcc = tan * (grindAccel * dir * s);
                }

                _rb.AddForce(springAcc + radialDampAcc + offAcc + tanAcc + driveAcc, ForceMode.Acceleration);

            }
        }

        // ------------------------------------------------------------------
        // True air control (only when fully airborne)
        // ------------------------------------------------------------------

        // Leg difference controls yaw (spin), lean controls pitch (flip).
        // Air-entry smoothing: ramp air control in, and use delta-from-takeoff so holding lean
        // while leaving the ground doesn't instantly pitch the skier forward.
        float ramp = (airControlBlendInTime <= 0f)
            ? 1f
            : Mathf.Clamp01((Time.time - _airEntryTime) / airControlBlendInTime);

        float yawInput = HasLegInputs
            ? Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f) * ramp
            : 0f;

        float pitchAbs = HasLeanInput ? _rawLeanInput : 0f;
        float pitchDelta = HasLeanInput ? (_rawLeanInput - _airLeanBaseline) : 0f;

        float pitchInput = Mathf.Lerp(pitchAbs, pitchDelta, Mathf.Clamp01(airPitchUseDeltaFromTakeoff));

        // Deadzone after blend.
        if (Mathf.Abs(pitchInput) < airPitchDeadzone)
            pitchInput = 0f;

        // Only allow negative pitch when actively leaning back (prevents unintended backflips).
        if (pitchInput < 0f && pitchAbs > -airPitchDeadzone)
            pitchInput = 0f;

        pitchInput *= ramp;

        // Tuck (poles held) increases spin responsiveness.
        float spinMultiplier = 1f;
        if (HasPolesInput && _rawPolesPressed)
            spinMultiplier *= airTuckSpinMultiplier;

        // Grind modifies air control by increasing damping + reducing accel (feels frictiony, not free-rotating).
        float grindS = Mathf.Clamp01(_grindStrengthSmoothed);
        float accelScale = 1f - (grindS * Mathf.Clamp01(grindAngularAccelReduction));
        float accel = Mathf.Max(0f, airAngularAcceleration) * accelScale;

        float dampingAir = Mathf.Max(0f, airAngularDamping);
        dampingAir += Mathf.Max(0f, grindAngularDamping) * grindS;

        // Desired angular velocities based on input.
        float targetYawSpeed = yawInput * airYawTurnSpeed * spinMultiplier;
        float targetPitchSpeed = pitchInput * airPitchTurnSpeed * spinMultiplier;

        // Accelerate toward target spin speeds.
        _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, targetYawSpeed, accel * dt);
        _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, targetPitchSpeed, accel * dt);

        // Apply damping toward zero when there is little/no input so we don't spin forever.
        if (Mathf.Abs(yawInput) < 0.01f)
            _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, 0f, dampingAir * dt);

        if (Mathf.Abs(pitchInput) < 0.01f)
        {
            float extra = (_airAngularVelocity.x < 0f && pitchAbs > -airPitchDeadzone) ? (dampingAir * 1.75f) : dampingAir;
            _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, 0f, extra * dt);
        }

        // Extra damping while grinding regardless of input (feels like edge friction).
        if (grindS > 0.0001f)
            _airAngularVelocity = Vector3.MoveTowards(_airAngularVelocity, Vector3.zero, dampingAir * 0.35f * dt);

        // Apply rotation based on current angular velocity.
        Quaternion yawRot = Quaternion.AngleAxis(_airAngularVelocity.y * dt, Vector3.up);
        Quaternion pitchRot = Quaternion.AngleAxis(_airAngularVelocity.x * dt, transform.right);

        _rb.MoveRotation(yawRot * pitchRot * _rb.rotation);
    }

    private void ApplyGrindAlignment(float dt, Vector3 targetUp, float strength01)
    {
        if (!grindAlignEnabled || dt <= 0f) return;

        if (targetUp.sqrMagnitude < 0.0001f) return;
        targetUp.Normalize();

        // Optionally bias toward world-up to keep it readable and less “clippy” around cables.
        if (grindAlignUpBias > 0f)
            targetUp = Vector3.Slerp(targetUp, Vector3.up, Mathf.Clamp01(grindAlignUpBias)).normalized;

        Quaternion current = _rb.rotation;
        Vector3 currentUp = current * Vector3.up;

        Quaternion toUp = Quaternion.FromToRotation(currentUp, targetUp);
        Quaternion desired = toUp * current;

        float s = Mathf.Clamp01(strength01) * Mathf.Max(0f, grindAlignStrength);
        float maxDeg = grindAlignMaxDegreesPerSec * dt * s;

        if (maxDeg > 0.0001f)
            _rb.MoveRotation(Quaternion.RotateTowards(current, desired, maxDeg));
    }

    private void OnGUI()
    {
        if (!showDebugHUD)
            return;

        const int width = 520;
        const int height = 360;

        // Scale HUD (keeps it readable at different resolutions)
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * debugHUDScale) * old;

        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, width, height), GUI.skin.box);

        _debugScroll = GUILayout.BeginScrollView(_debugScroll, false, true);

        GUILayout.Label($"Mode: {_movementMode}    Stacked: {_stacked}");
        GUILayout.Label($"isGrounded (casts): {_isGrounded}    HasAnySkiContact: {HasAnySkiContact}    IsGroundedForCtrl: {IsGroundedForControls}");

        Vector3 velPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        GUILayout.Label($"PlanarSpeed: {velPlane.magnitude:F2} m/s    ForwardLean: {_forwardLean:F2}");
        GUILayout.Label($"AirTime: {(Time.time - _airborneStartTime):F2}s    PeakY: {_airbornePeakY:F2}    LastStackReason: {_dbgLastStackReason}");

        if (debugShowTraverseHold)
        {
            GUILayout.Space(6);
            GUILayout.Label("=== Traverse Hold ===");
            GUILayout.Label($"SlopeAngle: {_dbgSlopeAngle:F1} deg");
            GUILayout.Label($"Across: {_dbgTraverseAcross:F2} (gate={_dbgTraverseAcrossGate:F2})");
            GUILayout.Label($"vFall: {_dbgTraverseVFall:F2} m/s (gate={_dbgTraverseSpeedGate:F2})");
            GUILayout.Label($"Hold: {_dbgTraverseHold:F2}    gPlaneMag: {_dbgTraverseGPlane.magnitude:F2}");
        }

        if (debugShowJumpLanding)
        {
            GUILayout.Space(6);
            GUILayout.Label("=== Jump ===");
            GUILayout.Label($"JumpHeld: {_jumpHeld}    JumpQueued: {_jumpQueued}    JumpReleaseQueued: {_jumpReleaseQueued}");
            GUILayout.Label($"ChargeT: {_dbgJumpChargeT:F2}    SpeedMult: {_dbgJumpSpeedMultiplier:F2}    Force: {_dbgJumpForce:F2}");
            GUILayout.Label($"JumpSlopeAngle: {_dbgJumpSlopeAngle:F1}    UpBlendT: {_dbgJumpUpBlendT:F2}    IntoSurface: {_dbgJumpIntoSurface:F2}");
            GUILayout.Label($"JumpDir: {_dbgJumpDir}");

            GUILayout.Space(4);
            GUILayout.Label("=== Landing / Stack Checks ===");
            GUILayout.Label($"LandingAirTime: {_dbgLandingAirTime:F2}s    DownSpeed: {_dbgLandingDownwardSpeed:F2}    Planar: {_dbgLandingPlanarSpeed:F2}");
            GUILayout.Label($"ImpactAngleFromPlane: {_dbgLandingImpactAngleFromPlane:F1} deg    FallHeight: {_dbgLandingFallHeight:F2} m");
            GUILayout.Label($"MisalignAngle: {_dbgLandingMisalignAngle:F1} deg    Tiny={_dbgLandingIsTiny}    HardSlam={_dbgLandingIsHardSlam}");
        }

        if (debugShowInputs)
        {
            GUILayout.Space(6);
            GUILayout.Label("=== Inputs ===");
            GUILayout.Label($"LeftLeg(raw): {_rawLeftLegInput:F2}    RightLeg(raw): {_rawRightLegInput:F2}");
            GUILayout.Label($"Lean(raw): {_rawLeanInput:F2}    Poles(raw): {(_rawPolesPressed ? 1f : 0f):F2}");
        }

        if (debugShowSkiContacts)
        {
            GUILayout.Space(6);
            GUILayout.Label("=== Ski Contacts ===");

            if (leftSkiContact != null)
            {
                GUILayout.Label(
                    $"LEFT: grounded={leftSkiContact.IsGrounded}  end={leftSkiContact.EndContactSign}  endZ={leftSkiContact.EndContactLocalZ:F2}  " +
                    $"tipOrTail={leftSkiContact.HasTipContact}  baseAlign={leftSkiContact.BaseContactAlignment:F2}");
                GUILayout.Label($"LEFT N: {leftSkiContact.ContactNormal}  P: {leftSkiContact.ContactPoint}");
            }

            if (rightSkiContact != null)
            {
                GUILayout.Label(
                    $"RIGHT: grounded={rightSkiContact.IsGrounded}  end={rightSkiContact.EndContactSign}  endZ={rightSkiContact.EndContactLocalZ:F2}  " +
                    $"tipOrTail={rightSkiContact.HasTipContact}  baseAlign={rightSkiContact.BaseContactAlignment:F2}");
                GUILayout.Label($"RIGHT N: {rightSkiContact.ContactNormal}  P: {rightSkiContact.ContactPoint}");
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.matrix = old;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmosSelected)
            return;

        // Ground normal / fall line from RB position (runtime-only meaningful)
        if (_rb != null)
        {
            Vector3 p = _rb.worldCenterOfMass;

            Gizmos.color = Color.green;
            Gizmos.DrawRay(p, _groundNormal.normalized * 1.0f);

            // Fall-line direction (tangential gravity)
            Vector3 gPlane = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal);
            if (gPlane.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(p, gPlane.normalized * 1.0f);
            }

            // Velocity on plane
            Gizmos.color = Color.cyan;
            Vector3 vPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
            Gizmos.DrawRay(p, vPlane * 0.1f);
        }

        // Ski contact points / normals
        if (leftSkiContact != null && leftSkiContact.IsGrounded)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftSkiContact.ContactPoint, 0.05f);
            Gizmos.DrawRay(leftSkiContact.ContactPoint, leftSkiContact.ContactNormal.normalized * 0.6f);
        }

        if (rightSkiContact != null && rightSkiContact.IsGrounded)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rightSkiContact.ContactPoint, 0.05f);
            Gizmos.DrawRay(rightSkiContact.ContactPoint, rightSkiContact.ContactNormal.normalized * 0.6f);
        }

        // Last jump direction (short window)
        if (Time.time - _lastJumpTime < 1.0f)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, _dbgJumpDir.normalized * 1.2f);
        }
    }

}


