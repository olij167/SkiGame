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
    [Tooltip("Direct reference to the DEFAULT left ski visual object in the scene (the mesh/model under LeftSki). " +
             "This object will be destroyed and replaced when equipping different skis.")]
    [SerializeField] private Transform leftSkiVisual;

    [Tooltip("Right ski transform (child of this object).")]
    [SerializeField] private Transform rightSki;
    [Tooltip("Per-ski helper for the right ski (auto-assigned from rightSki if null).")]
    [SerializeField] private SkiContact rightSkiContact;
    [Tooltip("Direct reference to the DEFAULT right ski visual object in the scene (the mesh/model under RightSki). " +
             "This object will be destroyed and replaced when equipping different skis.")]
    [SerializeField] private Transform rightSkiVisual;

    [Tooltip("Optional helper for the left pole (for contact & visuals).")]
    [SerializeField] private PoleContact leftPoleContact;

    [Tooltip("Optional helper for the right pole (for contact & visuals).")]
    [SerializeField] private PoleContact rightPoleContact;

    [Tooltip("Optional. If assigned, this controller will scale certain exertion-related forces by the soreness performance multiplier.")]
    [SerializeField] private SorenessMeter sorenessMeter;

    [Tooltip("Optional: gear loadout that provides runtime tuning multipliers for skis/poles.")]
    public SkiGearLoadout gearLoadout;

    [Header("Gear Visuals")]
    [Tooltip("If enabled, swapping equipped skis will replace the visual child object under each ski root (leftSki/rightSki).\n" +
         "This does NOT replace the ski root transforms, so SkiContact references remain valid.")]
    [SerializeField] private bool autoSwapSkiVisuals = true;

    // Cached profile so poles changes don't unnecessarily swap ski visuals.
    private SkiGearProfileSO _cachedSkisProfile;

    // Cached current visual child transforms (purely internal convenience; we still re-find safely if needed).
    private Transform _leftSkiVisual;
    private Transform _rightSkiVisual;

    private MaterialPropertyBlock _skiMpb;
    private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int _ColorId = Shader.PropertyToID("_Color");
    private static readonly int _BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");

    private Texture _skisPatternTex;
    private Texture _polesPatternTex;
    private MaterialPropertyBlock _poleMpb;

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
    private SkiGearTuning _gearTuning = SkiGearTuning.Default;

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

    // True if either ski collider is currently reporting ground contact.
    private bool HasAnySkiContact =>
        (leftSkiContact != null && leftSkiContact.IsGrounded) ||
        (rightSkiContact != null && rightSkiContact.IsGrounded);

    // True only when a ski has an actual collision contact (NOT just probe/coyote).
    private bool HasAnySkiCollisionContact =>
        (leftSkiContact != null && leftSkiContact.HasCollisionContact) ||
        (rightSkiContact != null && rightSkiContact.HasCollisionContact);

    // True when either ski has a "wall contact" (collision on groundLayers but too steep to count as ground).
    private bool HasAnySkiWallContact =>
        (leftSkiContact != null && leftSkiContact.HasWallContact) ||
        (rightSkiContact != null && rightSkiContact.HasWallContact);

    // Grounding/orientation
    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _wasControlsGrounded; // previous frame's grounded-for-controls flag
    private bool _nearGroundForJump; // body spherecast says we are within contact distance

    // Short suppression window after wall contact to prevent "ground-like" alignment/forces
    // during airborne cliff scrapes.
    private float _wallContactUntil;
    private Vector3 _wallScrapeNormal = Vector3.zero;

    private Vector3 _groundNormal = Vector3.up;
    private Vector3 _skiForward = Vector3.forward; // combined ski forward on plane

    // Cached slope info (computed once per FixedUpdate when grounded-for-controls)
    private float _slopeAngleDeg;   // 0..90
    private float _slopeT20;        // InverseLerp(minSlopeAngleForDownhill..20deg)
    private float _slopeT35;        // InverseLerp(minSlopeAngleForDownhill..35deg)

    // Cached rideable-slope threshold (avoids Vector3.Angle in ground checks)
    private float _cachedMaxGroundSlopeAngle = float.NaN;
    private float _cosMaxGroundSlope = -1f;


    // Separate from _groundNormal: used only for visual/orientation alignment (pitch/roll).
    private Vector3 _alignNormal = Vector3.up;

    // High-level locomotion state (for debugging and behaviour gating).
    private MovementMode _movementMode = MovementMode.Airborne;

    // Anti-clipping cadence state (reduces raycasts when stable).
    private int _antiClipStableFrames;
    private int _antiClipLastCheckedFrame = -999;
    private float _antiClipLastLiftNeeded;

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

    /// <summary>
    /// Used by external impact/VFX systems to ignore ski collider contacts.
    /// </summary>
    public bool IsSkiCollider(Collider c)
    {
        return c != null && _skiColliders.Contains(c);
    }

    /// <summary>
    /// Allows external systems to re-use SkiController's ground filtering.
    /// </summary>
    public LayerMask GroundLayers => groundLayers;

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
    private float _lastJumpReleasedTime;

    private float _airborneStartTime;
    private float _airbornePeakY;
    private Vector3 _lastAirPlanarVel; // planar w.r.t world-up while airborne

    // Air-entry smoothing: capture takeoff lean so small airtime doesn't instantly pitch the skier.
    private float _airEntryTime;
    private float _airLeanBaseline;

    // Air rotation state (degrees/second around local axes)
    private Vector3 _airAngularVelocity;

    // ----------------------------------------------------------------------
    // GRINDING (Air Edge Assist) runtime state
    // ----------------------------------------------------------------------

    private bool _grindActive;
    private float _grindTime;
    private float _grindStrengthSmoothed;
    private float _grindReattachCooldownUntil;

    // Cached best grind frame data (debug + detach impulse direction)
    private Vector3 _grindClosestPoint;
    private Vector3 _grindTangent;
    private Vector3 _grindDeltaToRail;
    private string _dbgGrindSource;
    private Vector3 _grindNormal = Vector3.up;

    // Per-frame cache to avoid repeated GetComponentInParent calls during probe evaluation.
    private int _grindProviderCacheFrame = -1;
    private Collider _grindProviderCacheColA;
    private Collider _grindProviderCacheColB;
    private LiftLine _grindProviderCacheLiftA;
    private LiftLine _grindProviderCacheLiftB;
    private FencePath _grindProviderCacheFenceA;
    private FencePath _grindProviderCacheFenceB;


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

    // Wall "catch" tuning (airborne cliff scrapes)
    [Header("Wall Collisions")]

    [Tooltip("Minimum impact speed INTO the wall (m/s) required before a catch can trigger. Increase to make catches rarer and only on harder hits; decrease to allow light scrapes to snag.")]
    [SerializeField] private float wallCatchMinImpactSpeed = 3.5f;

    [Tooltip("Minimum tangential speed along the wall plane (m/s) required before a catch can trigger. Higher values prevent slow 'sticky' snags; lower values allow catches even when sliding slowly down the face.")]
    [SerializeField] private float wallCatchMinTangentialSpeed = 5.0f;

    [Tooltip("Base probability that a catch will occur when thresholds are met. Actual chance is further scaled by how 'edge-like' the scrape is (ski direction aligned with slide direction). Set to 1 for always-catch (when eligible), or lower for more occasional snags.")]
    [SerializeField, Range(0f, 1f)] private float wallCatchChance = 0.65f;

    [Tooltip("How much tangential (along-wall) velocity is removed during a catch. 0 = no extra slowdown, 1 = fully stop along the wall. High values feel 'snaggy' but can look sticky; tune with min speeds + chance.")]
    [SerializeField, Range(0f, 1f)] private float wallCatchTangentialDamp = 0.55f;

    [Tooltip("Extra push away from the wall during a catch, as a fraction of impact speed. Helps the player peel off the cliff rather than magnet-slide. Too high can feel bouncy.")]
    [SerializeField] private float wallCatchPushOff = 0.10f;

    [Tooltip("Angular acceleration applied during a catch (ForceMode.Acceleration). Higher values produce stronger yaw/roll reactions on snag. If it feels spinny, lower this or lower TangentialDamp.")]
    [SerializeField] private float wallCatchTorqueStrength = 10.0f;

    [Tooltip("Maximum angular acceleration allowed during a catch (deg/s^2-like scale depending on your rigidbody). Prevents explosive spins on hard impacts. Increase if catches feel too muted; decrease if they snap too hard.")]
    [SerializeField] private float wallCatchMaxAngularAccel = 25.0f;


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

    [Tooltip("Minimum angle (deg) between current travel direction and ski direction before quick-stop engages (requires leaning backwards).")]
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

    [Tooltip("Effectiveness multiplier for pole propulsion when moving fully uphill (downhillDot = -1). " +
         "1 = same as downhill, >1 boosts uphill pole power, <1 reduces it.")]
    [SerializeField, Range(0.25f, 2f)] private float poleUphillEffectiveness = 1.25f;


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

    [Header("Soreness (Health + Stamina Combined)")]

    [Tooltip("Soreness added per skate push impulse (velocity change, m/s).")]
    [SerializeField] private float sorenessPerSkateImpulse = 0.0035f;

    [Tooltip("Soreness added per pole propulsion impulse (approx velocity change, m/s).")]
    [SerializeField] private float sorenessPerPoleImpulse = 0.0025f;

    [Tooltip("Soreness added per jump impulse (velocity change, m/s).")]
    [SerializeField] private float sorenessPerJumpImpulse = 0.0025f;

    [Tooltip("Small ongoing soreness per second while braking/dragging poles (very low by default).")]
    [SerializeField] private float sorenessPerPoleDragSecond = 0.00035f;


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

    [SerializeField, Tooltip("How long we keep grinding after losing contact (prevents 1-frame drops).")]
    private float grindCoyoteTime = 0.06f;

    private float _grindLastTouchTime;

    [Tooltip("Maximum distance (m) from skis to the rail centerline to apply assist.")]
    [SerializeField] private float grindCaptureRadius = 0.45f;

    [Tooltip("Layers searched for nearby grindables (LiftLine/FencePath) when airborne.\n" +
         "For best performance, restrict this to only your rail/cable/fence layers.\n" +
         "Use Collide triggers if your grind colliders are triggers.")]
    [SerializeField] private LayerMask grindableLayers = ~0;

    // Non-alloc buffer for overlap queries (keeps grind assist cheap).
    private Collider[] _grindOverlap = new Collider[64];

    [Tooltip("Minimum planar speed required before grind assist will engage.")]
    [SerializeField] private float grindMinPlanarSpeed = 2.0f;

    [Tooltip("How aligned the skis/planar movement must be with the rail tangent before assist engages.\n" +
         "This is a dot threshold on |dot(direction, railTangent)|. Larger = more aligned.\n" +
         "Example: 0.65 means assist begins once you're within ~49 degrees of the tangent.\n" +
         "The 'fully aligned' dot is derived from this value to reduce tuning parameters.")]
    [Range(0.0f, 0.99f)]
    [SerializeField] private float grindSidewaysDotBegin = 0.65f;

    // Derived from grindSidewaysDotBegin to avoid a second serialized threshold.
    private const float GRIND_ALIGN_FULL_FRAC = 0.65f; // fullDot = Lerp(beginDot, 1, frac)

    [Tooltip("Spring strength pulling the skis toward the rail centerline (m/s^2 per metre).")]
    [SerializeField] private float grindSpring = 55f;

    [Header("Grinding - Surface Support (Simple)")]
    [Tooltip("Vertical offset (m) above the grind path centerline that we try to keep the rider's reference point at while grinding. Helps 'sit' on cables/fences instead of falling through.")]
    [SerializeField] private float grindSupportOffset = 0.07f;

    [Tooltip("Extra damping applied to velocity along the grind support normal while grinding (m/s^2 per m/s). Reduces pogo / launchiness when landing on rails.")]
    [SerializeField] private float grindNormalDamping = 14f;

    [Tooltip("Multiplier for slope-parallel gravity while grinding (relative to grounded slope gravity). Higher = faster acceleration on rails/fences.")]
    [SerializeField, Range(0f, 3f)] private float grindSlopeGravityScale = 1.6f;

    [Tooltip("Multiplier applied to air-control yaw/pitch while grinding. 0 = no aerial control, 1 = same as air.")]
    [SerializeField, Range(0f, 1.5f)] private float grindAirControlMultiplier = 0.65f;

    [Header("Grinding - Alignment")]

    [Tooltip("How quickly grind strength ramps in/out (per second). Higher = snappier engagement.")]
    [SerializeField] private float grindStrengthResponse = 10f;

    [Tooltip("Cooldown (seconds) after detaching before grind assist can reattach.")]
    [SerializeField] private float grindReattachCooldown = 0.25f;
    // Grinding alignment is always active while grinding.
    // Reuse the main ground alignment rate (alignMaxDegreesPerSec) with a small multiplier.
    private const float GRIND_ALIGN_UP_BIAS = 0.15f;
    private const float GRIND_ALIGN_RATE_MULT = 1.25f;

    [Header("Grinding - Drive & Balance")]
    [Tooltip("Overall scale for along-rail gravity drive while grinding. 0 disables drive, 1 = physical gravity component.")]
    [SerializeField, Range(0f, 2f)] private float grindDriveScale = 1.0f;

    // Fixed boost fraction to reduce tuning params (was time-boost max).
    private const float GRIND_DRIVE_TIME_BOOST_FRAC = 0.35f;

    // Kept constant to reduce tuning params (was grindTimeToMaxBoost).
    private const float GRIND_TIME_TO_MAX_BOOST = 1.5f;

    [Tooltip("Balance gating using dot(rbUp, supportUp). Below this, grinding rapidly weakens.")]
    [SerializeField, Range(0f, 1f)] private float grindBalanceUpDotBegin = 0.55f;

    // Derived from grindBalanceUpDotBegin to avoid a second serialized threshold (was grindBalanceUpDotFull).
    private const float GRIND_BALANCE_DOT_WINDOW = 0.30f;

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

    [Tooltip("Show grinding diagnostics in the debug HUD.")]
    [SerializeField] private bool debugShowGrinding = true;

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

    public readonly struct StackEventInfo
    {
        public readonly Vector3 position;
        public readonly Vector3 velocity;
        public readonly float severity01;
        public readonly string reason;

        public StackEventInfo(Vector3 position, Vector3 velocity, float severity01, string reason)
        {
            this.position = position;
            this.velocity = velocity;
            this.severity01 = severity01;
            this.reason = reason ?? "";
        }
    }

    public event System.Action<StackEventInfo> OnStacked;


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
            //if (_stacked)
            //    return false;

            // If we are actively grinding, we want the same control path as grounded skiing.
            if (_grindActive)
                return true;

            // If we're scraping a wall while airborne, do NOT treat this as grounded for controls.
            // This prevents anti-tip-dig alignment + ground forces from firing on cliff faces.
            if (Time.time < _wallContactUntil)
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

        if (sorenessMeter == null)
            sorenessMeter = GetComponent<SorenessMeter>();

        BindGearLoadout();
    }
    private void CacheSkiColliders()
    {
        _skiColliders.Clear();

        Transform leftVis = (_leftSkiVisual != null) ? _leftSkiVisual : leftSkiVisual;
        Transform rightVis = (_rightSkiVisual != null) ? _rightSkiVisual : rightSkiVisual;

        if (leftSki != null)
        {
            foreach (var c in leftSki.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                if (leftVis != null && c.transform.IsChildOf(leftVis)) continue;
                _skiColliders.Add(c);
            }
        }

        if (rightSki != null)
        {
            foreach (var c in rightSki.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                if (rightVis != null && c.transform.IsChildOf(rightVis)) continue;
                _skiColliders.Add(c);
            }
        }
    }

    private void RefreshGearTuning()
    {
        _gearTuning = (gearLoadout != null) ? gearLoadout.GetTuning() : SkiGearTuning.Default;
    }
    private void HandleGearChanged()
    {
        RefreshGearTuning();

        if (autoSwapSkiVisuals)
            RefreshSkiVisualsIfNeeded(force: false);

        ApplySkiVisualStyle();
        ApplyPoleVisualStyle();
    }

    private void RefreshSkiVisualsIfNeeded(bool force)
    {
        if (gearLoadout == null) return;

        var skisProfile = gearLoadout.EquippedSkis;
        if (!force && skisProfile == _cachedSkisProfile) return;

        _cachedSkisProfile = skisProfile;
        if (skisProfile == null) return;

        // Swap only sides that have prefabs assigned; if null, keep the existing visual.
        if (skisProfile.skiPrefab != null)
            leftSkiVisual = SwapSkiVisual(leftSki, ref _leftSkiVisual, skisProfile.skiPrefab);

        if (skisProfile.skiPrefab != null)
            rightSkiVisual = SwapSkiVisual(rightSki, ref _rightSkiVisual, skisProfile.skiPrefab);

        CacheSkiColliders();
    }

    private void ApplySkiVisualStyle()
    {
        if (gearLoadout == null) return;

        Color c = gearLoadout.GetSkisColor();

        if (_skiMpb == null) _skiMpb = new MaterialPropertyBlock();
        _skiMpb.Clear();
        _skiMpb.SetColor(_BaseColorId, c);
        _skiMpb.SetColor(_ColorId, c);

        if (_skisPatternTex != null)
        {
            _skiMpb.SetTexture(_BaseMapId, _skisPatternTex);
            _skiMpb.SetTexture(_MainTexId, _skisPatternTex);
        }

        Transform leftVis = (_leftSkiVisual != null) ? _leftSkiVisual : leftSkiVisual;
        Transform rightVis = (_rightSkiVisual != null) ? _rightSkiVisual : rightSkiVisual;

        ApplyMpbToRenderers(leftVis, _skiMpb);
        ApplyMpbToRenderers(rightVis, _skiMpb);
    }

    public void SetSkisPatternTexture(Texture tex)
    {
        _skisPatternTex = tex;
        ApplySkiVisualStyle();
    }

    public void SetPolesPatternTexture(Texture tex)
    {
        _polesPatternTex = tex;
        ApplyPoleVisualStyle();
    }

    private void ApplyPoleVisualStyle()
    {
        Color c = (gearLoadout != null) ? gearLoadout.GetPolesColor() : Color.white;

        if (_poleMpb == null) _poleMpb = new MaterialPropertyBlock();
        _poleMpb.Clear();
        _poleMpb.SetColor(_BaseColorId, c);
        _poleMpb.SetColor(_ColorId, c);

        if (_polesPatternTex != null)
        {
            _poleMpb.SetTexture(_BaseMapId, _polesPatternTex);
            _poleMpb.SetTexture(_MainTexId, _polesPatternTex);
        }

        if (leftPoleContact != null) ApplyMpbToRenderers(leftPoleContact.transform, _poleMpb);
        if (rightPoleContact != null) ApplyMpbToRenderers(rightPoleContact.transform, _poleMpb);
    }

    private static void ApplyMpbToRenderers(Transform root, MaterialPropertyBlock mpb)
    {
        if (root == null) return;
        var rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null) continue;
            r.SetPropertyBlock(mpb);
        }
    }

    private Transform SwapSkiVisual(Transform skiRoot, ref Transform currentVisual, GameObject prefab)
    {
        if (skiRoot == null || prefab == null) return null;

        // We require an explicit current visual reference (bound from inspector defaults).
        // If it's missing, we refuse to guess, because guessing causes the “wrong child destroyed” bugs.
        if (currentVisual == null)
        {
            Debug.LogError($"[{nameof(SkiController)}] Cannot swap ski visual on '{skiRoot.name}' because currentVisual is null. " +
                             $"Assign Left/Right Ski Visual Default references in the inspector.");
            return null;
        }

        // Preserve the CURRENT visual child's local transform so the new prefab inherits the correct mount + scale.
        Vector3 keepLocalPos = currentVisual.localPosition;
        Quaternion keepLocalRot = currentVisual.localRotation;
        //Vector3 keepLocalScale = currentVisual.localScale;
        int keepSiblingIndex = currentVisual.GetSiblingIndex();

        DestroySafe(currentVisual.gameObject);
        currentVisual = null;

        var go = Instantiate(prefab);
        var t = go.transform;

        t.SetParent(skiRoot, worldPositionStays: false);
        t.localPosition = keepLocalPos;
        t.localRotation = keepLocalRot;
        //t.localScale = keepLocalScale;
        t.SetSiblingIndex(keepSiblingIndex);

        return currentVisual = t;
    }

    private static void DestroySafe(UnityEngine.Object obj)
    {
        if (obj == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    private void BindGearLoadout()
    {
        if (gearLoadout == null)
            gearLoadout = GetComponent<SkiGearLoadout>();

        // Fallback: loadout might be on a child in some prefabs/scenes
        if (gearLoadout == null)
            gearLoadout = GetComponentInChildren<SkiGearLoadout>(true);

        if (gearLoadout != null)
        {
            // Safety against double-subscribe in case this is ever re-called
            gearLoadout.OnTuningChanged -= HandleGearChanged;
            gearLoadout.OnTuningChanged += HandleGearChanged;
        }

        RefreshGearTuning();

        _leftSkiVisual = leftSkiVisual;
        _rightSkiVisual = rightSkiVisual;

        _cachedSkisProfile = null;

        if (autoSwapSkiVisuals)
            RefreshSkiVisualsIfNeeded(force: true);

        ApplySkiVisualStyle();
        ApplyPoleVisualStyle();
        CacheSkiColliders();
    }

    private void UnbindGearLoadout()
    {
        if (gearLoadout != null)
            gearLoadout.OnTuningChanged -= HandleGearChanged;
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
            leanAction.action.Disable();

        if (HasPolesInput)
            polesAction.action.Disable();

        if (HasJumpInput)
            jumpAction.action.Disable();

        // IMPORTANT:
        // Do NOT unbind gearLoadout here.
        // The shop disables SkiController to freeze gameplay, but we still need
        // gear changes (prefab/color) to update the visuals while previewing.
    }

    private void OnDestroy()
    {
        UnbindGearLoadout();
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

        UpdateWallScrapeSuppressionAndResponse();

        UpdateSlopeCache();
        _skiForward = GetCombinedSkiForwardOnPlane();

        if (!IsGroundedForControls)
        {
            _lastAirPlanarVel = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
        }

        UpdateGrinding(Time.fixedDeltaTime);

        bool grindingSurface = _grindActive && !HasAnySkiContact && !_isGrounded;

        // Do not run terrain penetration correction while grinding in-air;
        // it can fight rail motion and adds unnecessary work every physics step.
        if (preventSkiTerrainClipping && !grindingSurface && (HasAnySkiContact || _isGrounded))
            ResolveSkiTerrainPenetration(hardSnap: false);


        // Custom gravity: full gravity in air, tangential-only gravity when grounded.
        // This prevents "stalling mid-slope" and removes double-gravity ambiguity.
        if (IsGroundedForControls)
        {
            if (grindingSurface)
            {
                // While grinding we treat gravity like grounded skiing, but on the grind support normal
                // and with a stronger scale so rails/fences accelerate faster than terrain.
                Vector3 n = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;
                Vector3 gPlane = Vector3.ProjectOnPlane(Physics.gravity, n);

                float gScale = Mathf.Max(0f, grindSlopeGravityScale) * groundedSlopeGravityScale * _gearTuning.downhillAccelMul;
                gPlane *= gScale;

                _rb.AddForce(gPlane, ForceMode.Acceleration);
            }
            else
            {
                Vector3 gPlane = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal);

                float gScale = Mathf.Lerp(1f, groundedSlopeGravityScale * _gearTuning.downhillAccelMul, _slopeT35);
                gPlane *= gScale;

                // Always apply slope-parallel gravity, but optionally reduce + damp fall-line drift
                // when the player is strongly traversing and edging at low fall-line speed.
                if (enableTraverseHold && !_grindActive && gPlane.sqrMagnitude > 0.0001f)
                {

                    float slopeAngle = _slopeAngleDeg;
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
                        float hold = Mathf.Clamp01(acrossGate * speedGate) * Mathf.Clamp01(traverseHoldStrength * _gearTuning.traverseHoldMul);
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
        }
        else
        {
            _rb.AddForce(Physics.gravity, ForceMode.Acceleration);
        }

        if (IsGroundedForControls)
        {
            float t = 1f - Mathf.Exp(-alignNormalSmoothSpeed * Time.fixedDeltaTime);

            Vector3 targetUp;
            if (grindingSurface && _grindNormal.sqrMagnitude > 0.0001f)
                targetUp = _grindNormal.normalized;
            else if (_groundNormal.sqrMagnitude > 0.0001f)
                targetUp = _groundNormal.normalized;
            else
                targetUp = Vector3.up;

            _alignNormal = Vector3.Slerp(_alignNormal, targetUp, t);
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
        //
        // While grinding-in-air we intentionally skip ski steering / skate / poles / slope alignment.
        // The rail constraint (UpdateGrinding) + gravity should dominate.
        if (grindingSurface)
        {
            // No snow friction/steering, but allow hybrid yaw/pitch control.
            ApplyGroundForces(); // early-outs while grinding so no snow friction is applied
            ApplyGrindControl();
        }
        else if (_movementMode == MovementMode.Skiing ||
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
        if (jumpAction == null || jumpAction.action == null)
            return;

        var action = jumpAction.action;

        // IMPORTANT: do NOT use else-if here.
        // The Input System can report pressed+released in the same Update for quick taps.
        bool pressedThisFrame = action.WasPressedThisFrame();
        bool releasedThisFrame = action.WasReleasedThisFrame();

        if (pressedThisFrame)
        {
            // Arm a charged jump (fires on release)
            _jumpQueued = true;
            _jumpHeld = true;
            _jumpReleaseQueued = false;
            _lastJumpPressedTime = Time.time;

            // If jump was initiated while grinding, require it to fire while still grinding.
            _jumpMustFireWhileGrinding = _grindActive;
        }

        if (releasedThisFrame)
        {
            _jumpHeld = false;

            // If we have a queued jump, release will trigger it (or buffer it briefly).
            if (_jumpQueued)
                _jumpReleaseQueued = true;
        }

        // Drop stale queued jump if not held and not released for too long.
        if (_jumpQueued &&
            !_jumpHeld &&
            !_jumpReleaseQueued &&
            (Time.time - _lastJumpPressedTime) > jumpBufferTime)
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

        // Grinding-in-air is a separate constraint system (rail spring/damp + alignment + rail gravity).
        // Do not treat it as grounded locomotion or we will run ground forces/alignment that fight the rail.
        if (_grindActive && !HasAnySkiContact && !_isGrounded)
        {
            _movementMode = MovementMode.Airborne;
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
        bool haveSkiHardContact = HasAnySkiCollisionContact;

        // If we've just jumped, enforce a short ungrounded window so the jump
        // can actually leave the surface instead of instantly re-sticking.
        // We ONLY allow bypassing this window if we have a real collision contact.
        if (Time.time - _lastJumpTime < minJumpUngroundedTime && !haveSkiHardContact)
        {
            _isGrounded = false;
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

        bool IsRideable(Vector3 n) => IsRideableNormal(n);


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

        _nearGroundForJump = nearGround;

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

    private void UpdateSlopeCache()
    {
        // Cache slope info once per FixedUpdate to avoid repeated Angle/acos calls across systems.
        if (!IsGroundedForControls || _groundNormal.sqrMagnitude < 0.0001f)
        {
            _slopeAngleDeg = 0f;
            _slopeT20 = 0f;
            _slopeT35 = 0f;
            return;
        }

        Vector3 n = _groundNormal.normalized;
        float upDot = Mathf.Clamp(Vector3.Dot(n, Vector3.up), -1f, 1f);

        _slopeAngleDeg = Mathf.Acos(upDot) * Mathf.Rad2Deg;
        _slopeT20 = Mathf.Clamp01(Mathf.InverseLerp(minSlopeAngleForDownhill, 20f, _slopeAngleDeg));
        _slopeT35 = Mathf.Clamp01(Mathf.InverseLerp(minSlopeAngleForDownhill, 35f, _slopeAngleDeg));
    }

    private void UpdateWallScrapeSuppressionAndResponse()
    {
        // Only relevant when not grinding; grinding has its own constraint system.
        if (_grindActive)
            return;

        // Gather wall normals from skis if present.
        Vector3 n = Vector3.zero;

        if (leftSkiContact != null && leftSkiContact.HasWallContact)
            n += leftSkiContact.WallContactNormal;

        if (rightSkiContact != null && rightSkiContact.HasWallContact)
            n += rightSkiContact.WallContactNormal;

        bool hasWall = n.sqrMagnitude > 0.0001f;
        if (!hasWall)
            return;

        n.Normalize();

        // If the "wall" is actually rideable ground, do nothing (normal ground system will handle it).
        if (IsRideableNormal(n))
            return;

        // If our body cast says we're truly near ground, let grounding handle it;
        // this is mainly for airborne cliff face scrapes.
        if (_nearGroundForJump)
            return;

        // Suppress grounded-for-controls briefly to prevent ground alignment/forces loops.
        const float WALL_SUPPRESS_TIME = 0.20f;
        _wallContactUntil = Mathf.Max(_wallContactUntil, Time.time + WALL_SUPPRESS_TIME);
        _wallScrapeNormal = n;

        // Apply a stable airborne wall response:
        // - remove velocity INTO the wall
        // - optional small push-off
        // - slight tangential damping so it feels like scraping, not sticking
        ApplyAirWallScrapeResponse(n);
    }

    private void ApplyAirWallScrapeResponse(Vector3 wallNormal)
    {
        Vector3 v = _rb.linearVelocity;
        Vector3 n = (wallNormal.sqrMagnitude > 0.0001f) ? wallNormal.normalized : Vector3.up;

        float into = Vector3.Dot(v, n);

        // Cancel only the component INTO the wall.
        if (into < 0f)
            v -= n * into;

        float impactSpeed = -into;

        // Tangential component along wall plane (what becomes the "slide down the face")
        Vector3 tangential = Vector3.ProjectOnPlane(v, n);
        float tangentialSpeed = tangential.magnitude;

        // Base scrape damping (your current behavior)
        {
            float tangentialDamp = 0.03f; // keep your existing baseline scrape friction
            v -= tangential * tangentialDamp;
        }

        // ----------------------------------------------------------
        // NEW: wall "catch"
        // ----------------------------------------------------------
        // A catch should feel like an edge bites: tangential speed drops more,
        // body rotates, and trajectory is altered.
        if (impactSpeed >= wallCatchMinImpactSpeed && tangentialSpeed >= wallCatchMinTangentialSpeed)
        {
            // Use ski direction to decide if this scrape is "edge-like".
            // If skis are travelling roughly parallel to the wall plane, catches feel believable.
            Vector3 skiFwdOnWall = Vector3.ProjectOnPlane(_skiForward, n);
            float skiFwdLen = skiFwdOnWall.magnitude;

            float edgeLike = 0f;
            if (skiFwdLen > 0.0001f && tangentialSpeed > 0.0001f)
            {
                Vector3 skiFwdN = skiFwdOnWall / skiFwdLen;
                Vector3 tanN = tangential / tangentialSpeed;

                // 1 when aligned with tangential slide direction, 0 when perpendicular
                float align = Mathf.Abs(Vector3.Dot(skiFwdN, tanN));
                // make it "edge-like" only when fairly aligned
                edgeLike = Mathf.InverseLerp(0.55f, 0.90f, align);
            }

            // Probabilistic + geometric gating keeps it from feeling sticky/always-on.
            // (You can remove Random if you want deterministic only.)
            bool doCatch = edgeLike > 0f && (Random.value <= wallCatchChance * edgeLike);

            if (doCatch)
            {
                // Recompute tangential after baseline damp
                tangential = Vector3.ProjectOnPlane(v, n);
                tangentialSpeed = tangential.magnitude;

                if (tangentialSpeed > 0.0001f)
                {
                    Vector3 tanN = tangential / tangentialSpeed;

                    // 1) Stronger tangential damping: "edge bites"
                    float damp = Mathf.Clamp01(wallCatchTangentialDamp) * edgeLike;
                    v -= tangential * damp;

                    // 2) Small push-off: helps peel away rather than glue
                    v += n * (impactSpeed * wallCatchPushOff * edgeLike);

                    // 3) Torque: rotate body in reaction
                    // Rotate around axis that tends to swing the skier away / along the wall.
                    // Use cross of tangential direction and wall normal.
                    Vector3 torqueAxis = Vector3.Cross(tanN, n);
                    if (torqueAxis.sqrMagnitude > 0.0001f)
                    {
                        torqueAxis.Normalize();

                        // Apply angular acceleration (stable, mass-independent feel)
                        Vector3 angAccel = torqueAxis * (wallCatchTorqueStrength * edgeLike);

                        // Clamp angular acceleration
                        float a = angAccel.magnitude;
                        if (a > wallCatchMaxAngularAccel)
                            angAccel = angAccel * (wallCatchMaxAngularAccel / a);

                        _rb.AddTorque(angAccel, ForceMode.Acceleration);
                    }
                }
            }
        }

        // Mild restitution / push-off at higher impact speeds (keeps realism, avoids sticking).
        if (impactSpeed > 3.5f)
        {
            v += n * (impactSpeed * 0.12f);
        }

        _rb.linearVelocity = v;
    }

    // ----------------------------------------------------------------------
    // ANTI-CLIPPING (SKI CLEARANCE)
    // ----------------------------------------------------------------------
    private void RefreshRideableSlopeCache()
    {
        // Keep this robust for runtime tuning (inspector edits during play).
        if (!Mathf.Approximately(_cachedMaxGroundSlopeAngle, maxGroundSlopeAngle))
        {
            _cachedMaxGroundSlopeAngle = maxGroundSlopeAngle;
            _cosMaxGroundSlope = Mathf.Cos(maxGroundSlopeAngle * Mathf.Deg2Rad);
        }
    }
    // ----------------------------------------------------------------------
    // Cached slope values for grounded systems (friction, traverse hold, etc.)
    // ----------------------------------------------------------------------
    private void RefreshSlopeCache()
    {
        if (_groundNormal.sqrMagnitude < 0.0001f)
        {
            _slopeAngleDeg = 0f;
            _slopeT20 = 0f;
            _slopeT35 = 0f;
            return;
        }

        // _groundNormal is expected to be normalized, but clamp defensively.
        float y = Mathf.Clamp(_groundNormal.y, -1f, 1f);
        _slopeAngleDeg = Mathf.Acos(y) * Mathf.Rad2Deg;
        _slopeT20 = Mathf.InverseLerp(minSlopeAngleForDownhill, 20f, _slopeAngleDeg);
        _slopeT35 = Mathf.InverseLerp(minSlopeAngleForDownhill, 35f, _slopeAngleDeg);
    }

    private void RefreshMaxGroundSlopeCache()
    {
        if (float.IsNaN(_cachedMaxGroundSlopeAngle) || !Mathf.Approximately(_cachedMaxGroundSlopeAngle, maxGroundSlopeAngle))
        {
            _cachedMaxGroundSlopeAngle = maxGroundSlopeAngle;
            _cosMaxGroundSlope = Mathf.Cos(maxGroundSlopeAngle * Mathf.Deg2Rad);
        }
    }

    private bool IsRideableNormal(Vector3 n)
    {
        if (n.sqrMagnitude < 0.0001f)
            return false;

        RefreshMaxGroundSlopeCache();

        // Rideable when the normal is within maxGroundSlopeAngle of world-up.
        // Equivalent to Angle(n, up) <= maxAngle, but avoids acos/Angle cost.
        float upDot = Vector3.Dot(n.normalized, Vector3.up);
        return upDot >= _cosMaxGroundSlope;
    }

    private bool TryComputeRequiredLift(Vector3 up, out float requiredLift)
    {
        float lift = 0f;

        // Be robust: treat input as potentially un-normalized.
        if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
        else up = up.normalized;

        float dist = skiProbeUp + skiProbeDown;
        if (dist <= 0.0001f)
        {
            requiredLift = 0f;
            return false;
        }

        bool gotAnyHit = false;

        void ConsiderSki(Transform skiT)
        {
            if (skiT == null) return;

            Vector3 origin = skiT.position + up * skiProbeUp;

            if (Physics.Raycast(origin, -up, out RaycastHit hit, dist, groundLayers, QueryTriggerInteraction.Ignore))
            {
                gotAnyHit = true;

                if (!IsRideableNormal(hit.normal))
                    return;

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

        int frame = Time.frameCount;

        // Adaptive cadence: when we're stably skiing and last checks found no lift required,
        // avoid raycasting every physics step.
        if (!hardSnap)
        {
            // Only treat "stable" as Skiing. During Landing/Airborne states, never skip.
            if (_movementMode == MovementMode.Skiing)
            {
                float vIntoGround = Vector3.Dot(_rb.linearVelocity, -up); // positive = moving into ground along up
                bool highRisk = (vIntoGround > 1.0f); // impacts / steep compressions

                // Ramp up skip cadence as stability increases:
                // 0–3 stable frames: check every frame
                // 4–11 stable frames: check every 2 frames
                // 12+ stable frames: check every 3 frames
                int cadence = (_antiClipStableFrames >= 12) ? 3 : (_antiClipStableFrames >= 4) ? 2 : 1;

                if (!highRisk && _antiClipLastLiftNeeded <= 0.0001f && cadence > 1)
                {
                    if ((frame - _antiClipLastCheckedFrame) < cadence)
                        return; // Skip raycasts this step
                }
            }
            else
            {
                _antiClipStableFrames = 0;
            }
        }

        _antiClipLastCheckedFrame = frame;

        if (!TryComputeRequiredLift(up, out float liftNeeded))
        {
            _antiClipLastLiftNeeded = 0f;

            // Only count stability while actually skiing (prevents “stable” buildup while in air).
            if (!hardSnap && _movementMode == MovementMode.Skiing)
                _antiClipStableFrames = Mathf.Min(_antiClipStableFrames + 1, 60);
            else
                _antiClipStableFrames = 0;

            return;
        }

        // We have positive lift requirement.
        _antiClipLastLiftNeeded = liftNeeded;
        _antiClipStableFrames = 0;

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

            float sevHardSlam;
            {
                float sFall = Mathf.InverseLerp(hardLandingMinFallHeight, hardLandingMinFallHeight * 2.5f, fallHeight);
                float sDown = Mathf.InverseLerp(hardLandingMinDownwardSpeed, hardLandingMinDownwardSpeed * 2.5f, downwardSpeed);
                float sAng = Mathf.InverseLerp(hardLandingMinImpactAngleFromPlane, 90f, impactAngleFromPlane);
                sevHardSlam = Mathf.Clamp01(Mathf.Max(sFall, sDown) * 0.7f + sAng * 0.3f);
            }

            TriggerStack(sevHardSlam, ComputeStackTorqueAxisFromContacts());
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
                float sevNose;
                {
                    float sNose = Mathf.InverseLerp(heavyTipStackAngle, heavyTipStackAngle + 45f, tipDigAngle);
                    float sDown = Mathf.InverseLerp(hardLandingMinDownwardSpeed * 0.6f, hardLandingMinDownwardSpeed * 2.0f, downwardSpeed);
                    sevNose = Mathf.Clamp01(Mathf.Max(sNose, sDown));
                }
                _dbgLastStackReason = "Tip Dig";
                TriggerStack(sevNose, ComputeStackTorqueAxisFromContacts());
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
            float sev;
            {
                float sTilt = Mathf.InverseLerp(maxLandingTiltAngle, maxLandingTiltAngle + 60f, tiltAngle);
                float sMis = Mathf.InverseLerp(misalignLimit, misalignLimit + 90f, misalignAngle);
                float sNose = Mathf.InverseLerp(heavyTipStackAngle, heavyTipStackAngle + 45f, tipDigAngle);
                float sSpeed = Mathf.InverseLerp(minLandingSpeedForStackCheck, minLandingSpeedForStackCheck + 12f, planarSpeed);

                sev = Mathf.Clamp01(Mathf.Max(sTilt, sMis, sNose) * 0.85f + sSpeed * 0.15f);
            }
            _dbgLastStackReason = "Bad Landing";
            TriggerStack(sev, ComputeStackTorqueAxisFromContacts());
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

        // If projection is tiny (bad normal, steep face, etc.), fall back to last airborne planar intent.
        if (planarSpeed < 0.05f)
        {
            Vector3 fallback = Vector3.ProjectOnPlane(_lastAirPlanarVel, groundNormal);
            if (fallback.sqrMagnitude > 0.0001f)
            {
                planar = fallback;
                planarSpeed = planar.magnitude;
            }
        }

        if (planarSpeed < 0.05f)
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

    private void TriggerStack(Vector3 torqueAxisWorld)
    {
        TriggerStack(ComputeGenericStackSeverity01(), torqueAxisWorld);
    }

    private void TriggerStack(float severity01, Vector3 torqueAxisWorld)
    {
        if (_stacked) return;

        severity01 = Mathf.Clamp01(severity01);

        // Apply soreness impact before we freeze state.
        if (sorenessMeter != null)
            sorenessMeter.AddImpact(severity01);

        _stacked = true;
        _rb.freezeRotation = false;

        // When we stack, we want a decisive fall rather than lingering in a half-rotated state.
        _rb.angularVelocity = Vector3.zero;

        // Prevent hard impacts from forcing the rigidbody through the terrain.
        // Remove any into-ground component and keep a small amount of planar velocity so the wipeout feels natural.
        {
            Vector3 n = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
            Vector3 v = _rb.linearVelocity;
            Vector3 p0 = _hasNonSkiGroundContact ? _nonSkiGroundContactPoint : transform.position;
            OnStacked?.Invoke(new StackEventInfo(p0, n, severity01, _dbgLastStackReason));

            float into = Vector3.Dot(v, n);
            if (into < 0f)
                v -= n * into; // cancel into-ground

            float retain = Mathf.Clamp01(stackPlanarVelocityRetention);

            // Safety: never fully zero planar velocity at meaningful speeds.
            // (Prevents "random full stop" feel even if a stack triggers.)
            float planarSpeed = Vector3.ProjectOnPlane(v, n).magnitude;
            if (planarSpeed > 2.0f)
                retain = Mathf.Max(retain, 0.15f);

            v = Vector3.ProjectOnPlane(v, n) * retain;
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

        // Do not do tip/tail stability stacking unless we have real ground contact.
        // This prevents false stacks from probe coyote / wall grazing.
        if (!_isGrounded && !HasAnySkiCollisionContact)
            return;


        // Read per-ski contact state.
        bool leftGrounded = leftSkiContact != null && leftSkiContact.IsGrounded && IsRideableNormal(leftSkiContact.ContactNormal);
        bool rightGrounded = rightSkiContact != null && rightSkiContact.IsGrounded && IsRideableNormal(rightSkiContact.ContactNormal);

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
            - tuck * (tuckFrictionReduction * _gearTuning.tuckEffectMul)
            + brake * (brakeFrictionIncrease * _gearTuning.brakeEffectMul);

        // Slight extra slip when neutral leaning on slopes (feels like gravity is doing the work).
        // Only apply when we're actually on a slope.
        float slopeT = _slopeT20;

        float neutral = 1f - Mathf.Clamp01(Mathf.Abs(_forwardLean));
        frictionScale *= 1f - neutralSlipBoost * neutral * slopeT;

        float forwardFric = (forwardFriction * _gearTuning.forwardFrictionMul) * edgeFactor * frictionScale;
        float sideFric = (sideFriction * _gearTuning.sideFrictionMul) * edgeFactor * frictionScale;

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

        // While grinding (and not actually on snow), let the grind constraint + gravity drive motion.
        // This avoids snow friction / carve steering fighting a rail slide.
        if (_grindActive && !HasAnySkiContact && !_isGrounded)
            return;

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

            // -----------------------------
            // Task 4: Backwards wedge boost fix
            // -----------------------------
            const float wedgeActiveThreshold = 0.2f;
            bool leftActive = _rawLeftLegInput > wedgeActiveThreshold;
            bool rightActive = _rawRightLegInput > wedgeActiveThreshold;
            bool wedgeInput = leftActive && rightActive;

            bool movingBackwards = false;
            if (skiDir.sqrMagnitude > 0.0001f)
            {
                Vector3 skiDirNForDot = skiDir.normalized;
                float dirDot = Vector3.Dot(planeDir, skiDirNForDot); // -1 = fully backwards
                movingBackwards = dirDot < 0f;
            }

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

            // Task 4: When moving backwards OR wedge is active, do not steer velocity toward ski forward.
            // This prevents "free reorientation" that can look like a boost up-slope.
            bool allowCarveSteering = !movingBackwards && !wedgeInput;

            if (allowCarveSteering && carveFactor > 0f && skiDir.sqrMagnitude > 0.0001f)
            {
                Vector3 skiDirN = skiDir.normalized;

                // Normal carve steering (existing behaviour).
                float dt = Time.fixedDeltaTime;
                float steerAmount = carveFactor * (carveSteerStrength * _gearTuning.carveSteerMul) * dt;
                Vector3 steeredDir = Vector3.Slerp(planeDir, skiDirN, steerAmount).normalized;

                // Apply steering while preserving speed (for now).
                Vector3 normalComponent = Vector3.Project(newVel, _groundNormal);
                float newPlaneSpeed = planeSpeed;

                // --- Quick Stop: extra planar damping when turning sharply at speed ---
                // Avoid Angle/acos unless the dot test says we are past the minimum angle.
                float quickStopEff = quickStopStrength * _gearTuning.quickStopMul;
                if (_forwardLean < 0f && quickStopEff > 0f && planeSpeed >= quickStopMinSpeed)
                {
                    float dot = Mathf.Clamp(Vector3.Dot(planeDir, skiDirN), -1f, 1f);
                    float cosMin = Mathf.Cos(quickStopMinTurnAngle * Mathf.Deg2Rad);

                    // dot <= cos(minAngle)  <=>  angle >= minAngle
                    if (dot <= cosMin)
                    {
                        float turnAngle = Mathf.Acos(dot) * Mathf.Rad2Deg;
                        float angleT = Mathf.InverseLerp(quickStopMinTurnAngle, 90f, turnAngle);
                        float stopT = carveFactor * angleT;

                        // Exponential damping: speed *= exp(-k * dt)
                        float k = quickStopEff * stopT;
                        newPlaneSpeed *= Mathf.Exp(-k * dt);
                    }
                }

                newVel = steeredDir * newPlaneSpeed + normalComponent;
                _rb.linearVelocity = newVel;
            }
            else if (wedgeInput)
            {
                // Task 4: Wedge should BRAKE, not re-orient/reverse motion.
                // Apply a damping acceleration opposing planar velocity.
                // This never pushes uphill; it only reduces current sliding speed.
                float dt = Time.fixedDeltaTime;

                // Tuning: keep this conservative; scale by existing friction tuning.
                float brakeStrength = 6f * _gearTuning.sideFrictionMul;

                // Oppose planar movement.
                Vector3 brakeAccel = -planeVel * brakeStrength;

                // Optional safety: clamp very large brakes on extreme speeds.
                float maxBrakeAccel = 80f;
                if (brakeAccel.sqrMagnitude > maxBrakeAccel * maxBrakeAccel)
                    brakeAccel = brakeAccel.normalized * maxBrakeAccel;

                _rb.AddForce(brakeAccel, ForceMode.Acceleration);
            }
        }
    }

    private void TryConsumeQueuedJump()
    {
        if (!_jumpQueued)
            return;

        // We only jump on release (existing design).
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

        // Strict rule: only allow jumping when truly grounded (body spherecast / hard collision)
        // or while the rail-assist ("grind") is active.
        bool hardGrounded =
            _nearGroundForJump ||
            (leftSkiContact != null && leftSkiContact.HasCollisionContact) ||
            (rightSkiContact != null && rightSkiContact.HasCollisionContact);

        bool canJumpNow = (_isGrounded || hardGrounded || _grindActive);

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

        // If we are grinding, capture rail frame data, then detach right before applying the impulse
        // so assist forces do not dampen it.
        bool jumpedFromGrind = _grindActive;
        Vector3 grindTan = _grindTangent;
        Vector3 grindUp = _grindNormal;
        Vector3 grindDeltaToRail = _grindDeltaToRail; // closestPoint - referencePoint (points FROM rider TO rail)

        if (_grindActive)
        {
            _grindActive = false;
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

        // Scale by soreness performance (exertion affects impulse-producing actions only).
        float perfMult = GetPerformanceMult();
        jumpForce *= perfMult;

        // ------------------------------------------------------------------
        // 2. Jump direction (slope-aware)
        // ------------------------------------------------------------------
        Vector3 baseDir;
        if (jumpedFromGrind && grindUp.sqrMagnitude > 0.0001f)
            baseDir = grindUp.normalized;
        else
            baseDir = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;

        float slopeAngle = Vector3.Angle(baseDir, Vector3.up);
        float upBlendT = Mathf.InverseLerp(jumpUpBlendStartAngle, jumpUpBlendEndAngle, slopeAngle);
        Vector3 jumpDir = Vector3.Slerp(baseDir, Vector3.up, Mathf.Clamp01(upBlendT)).normalized;

        _dbgJumpSlopeAngle = slopeAngle;
        _dbgJumpUpBlendT = Mathf.Clamp01(upBlendT);
        _dbgJumpDir = jumpDir;

        // If we jumped off a rail, bias the jump slightly away from the rail and cancel "into-rail" velocity
        // to prevent immediate re-catch / damped pop.
        if (jumpedFromGrind)
        {
            // Outward = rail -> rider (opposite of deltaToRail)
            Vector3 outward = -grindDeltaToRail;

            // Remove any component along the rail tangent so the bias is genuinely "off the rail", not along it.
            if (grindTan.sqrMagnitude > 0.0001f)
                outward = Vector3.ProjectOnPlane(outward, grindTan);

            // Keep outward mostly lateral relative to the primary jump direction (so we don’t kill pop).
            outward = Vector3.ProjectOnPlane(outward, jumpDir);

            if (outward.sqrMagnitude > 0.0001f)
            {
                outward.Normalize();

                // Small constant bias (kept as code constant to avoid adding another inspector parameter).
                const float outwardBias = 0.45f;

                Vector3 biased = (jumpDir + outward * outwardBias);
                if (biased.sqrMagnitude > 0.0001f)
                    jumpDir = biased.normalized;
            }

            // Cancel velocity into the rail centerline (deltaToRail points rider->rail; positive dot means moving into rail).
            if (grindDeltaToRail.sqrMagnitude > 0.0001f)
            {
                Vector3 radialDir = grindDeltaToRail.normalized;
                Vector3 v = _rb.linearVelocity;
                float vIntoRail = Vector3.Dot(v, radialDir);
                if (vIntoRail > 0f)
                {
                    v -= radialDir * vIntoRail;
                    _rb.linearVelocity = v;
                }
            }
        }

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

        // Exertion: jumping costs soreness proportional to the impulse.
        if (sorenessMeter != null && sorenessPerJumpImpulse > 0f)
            sorenessMeter.AddExertion(jumpForce * sorenessPerJumpImpulse);

        _lastJumpTime = Time.time;
        _isGrounded = false;
        _nearGroundForJump = false;
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
        float impulse = (skateImpulse * _gearTuning.skateImpulseMul) * leanT * speedFactor;

        // ------------------------------------------------------------
        // Uphill penalty: reduce (and optionally counter) skate pushes
        // when the skier is attempting to push uphill on steep slopes.
        // ------------------------------------------------------------
        float slopeAngle = _slopeAngleDeg;

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

        // Scale by soreness performance.
        float perfMult = GetPerformanceMult();
        impulse *= perfMult;

        _rb.AddForce(pushDir * impulse, ForceMode.VelocityChange);

        // Exertion: skating costs more when pushing uphill.
        if (sorenessMeter != null && sorenessPerSkateImpulse > 0f)
        {
            float uphill01 = ComputeUphill01(pushDir);
            float uphillFactor = (0.25f + 0.75f * uphill01);
            sorenessMeter.AddExertion(impulse * sorenessPerSkateImpulse * uphillFactor);
        }

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

        // Downhill direction on the ground plane.
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
        float downhillMag = downhill.magnitude;

        float downhillDot = 0f;
        if (downhillMag > 0.0001f && velPlane.sqrMagnitude > 0.0001f)
        {
            downhill /= downhillMag;
            downhillDot = Vector3.Dot(velPlane.normalized, downhill); // +1 downhill, -1 uphill
        }

        // How effective poles are depending on downhill vs uphill.
        float downhillFactor = Mathf.Lerp(poleUphillEffectiveness, 1f, Mathf.Clamp01((downhillDot + 1f) * 0.5f));

        // Cap effectiveness at higher speeds (pushes matter less at high speed).
        float speedFactor = 1f;
        if (speed > poleMaxSpeed)
        {
            float tSpeed = Mathf.InverseLerp(poleMaxSpeed, poleMaxSpeed * 1.7f, speed);
            float minPoleSpeedFactor = 0.25f;

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

        float perfMult = GetPerformanceMult();
        float dt = Time.fixedDeltaTime;
        float uphill01 = Mathf.Clamp01(-downhillDot); // 0 downhill, 1 fully uphill

        switch (_polePhase)
        {
            // ENTRY: pressing poles forward/down before they fully bite.
            case PoleStrokePhase.Entry:
                {
                    // A small assist that grows through the entry stroke.
                    float entryStrength = phaseT;
                    float accel =
                        ((poleImpulse * _gearTuning.poleImpulseMul) * perfMult)
                        * 0.5f
                        * entryStrength
                        * (0.4f + 0.6f * leanT)
                        * downhillFactor
                        * speedFactor;

                    Vector3 skiDir = GetCombinedSkiForwardOnPlane();
                    if (skiDir.sqrMagnitude > 0.0001f)
                    {
                        _rb.AddForce(skiDir * accel, ForceMode.Acceleration);

                        if (sorenessMeter != null && sorenessPerPoleImpulse > 0f)
                        {
                            float dv = Mathf.Max(0f, accel) * dt;
                            float uphillFactor = (0.35f + 0.65f * uphill01);
                            sorenessMeter.AddExertion(dv * sorenessPerPoleImpulse * uphillFactor);
                        }
                    }
                    break;
                }

            // DRAG: poles are dug in; main braking effect, modulated by slope + speed.
            case PoleStrokePhase.Drag:
                {
                    if (speed > 0.25f && downhillMag > 0.0001f)
                    {
                        Vector3 brakeDir = -velPlane.normalized;

                        // Braking strength scales with phase + speed.
                        float brake =
                            poleBrakeStrength
                            * (0.35f + 0.65f * phaseT)
                            * Mathf.Clamp01(speed / 8f)
                            * (0.7f + 0.3f * (1f - leanT)); // leaning forward reduces brake a touch

                        _rb.AddForce(brakeDir * brake, ForceMode.Acceleration);

                        if (sorenessMeter != null && sorenessPerPoleDragSecond > 0f)
                        {
                            float uphillFactor = (0.25f + 0.75f * uphill01);
                            sorenessMeter.AddExertion(sorenessPerPoleDragSecond * dt * uphillFactor);
                        }
                    }
                    break;
                }

            // FOLLOW-THROUGH: poles release; small forward pop then return to idle.
            case PoleStrokePhase.FollowThrough:
                {
                    float followStrength = 1f - phaseT;

                    float impulse =
                        (poleImpulse * perfMult)
                        * 0.35f
                        * followStrength
                        * (0.55f + 0.45f * leanT)
                        * downhillFactor
                        * speedFactor;

                    Vector3 skiDir = GetCombinedSkiForwardOnPlane();
                    if (skiDir.sqrMagnitude > 0.0001f)
                    {
                        Vector3 push = skiDir * impulse;

                        if (speed < 2.0f)
                        {
                            _rb.AddForce(push, ForceMode.VelocityChange);

                            if (sorenessMeter != null && sorenessPerPoleImpulse > 0f)
                            {
                                float dv = push.magnitude; // VelocityChange
                                float uphillFactor = (0.35f + 0.65f * uphill01);
                                sorenessMeter.AddExertion(dv * sorenessPerPoleImpulse * uphillFactor);
                            }
                        }
                        else
                        {
                            _rb.AddForce(push, ForceMode.Acceleration);

                            if (sorenessMeter != null && sorenessPerPoleImpulse > 0f)
                            {
                                float dv = push.magnitude * dt; // Acceleration -> delta-v over dt
                                float uphillFactor = (0.35f + 0.65f * uphill01);
                                sorenessMeter.AddExertion(dv * sorenessPerPoleImpulse * uphillFactor);
                            }
                        }
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
        // Grinding owns alignment via UpdateGrinding() -> ApplyGrindAlignment().
        // Prevent slope alignment from overriding rail/cable alignment in the same tick.
        if (_grindActive)
            return;

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
        float yawSpeed = ((groundTurnSpeed * _gearTuning.turnSpeedMul) * stability) * landingBoost;

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
        GrindCandidate bestLocal = default;

        float maxSq = maxDist * maxDist;
        float bestSq = maxSq;

        bool found = false;

        // 1) Prefer actual contact colliders first (most reliable).
        // Use *any* contact (not ground-filtered) so rails/fences can be candidates.
        if (leftSkiContact != null && leftSkiContact.HasAnyCollisionContact)
        {
            if (TryResolveBestFromCollider(leftSkiContact.AnyCollisionOtherCollider, referencePoint, ref bestSq, ref bestLocal))
                found = true;
        }

        if (rightSkiContact != null && rightSkiContact.HasAnyCollisionContact)
        {
            if (TryResolveBestFromCollider(rightSkiContact.AnyCollisionOtherCollider, referencePoint, ref bestSq, ref bestLocal))
                found = true;
        }

        // 1b) Also consider any-collision/trigger contacts (not filtered by groundLayers).
        // This is critical for rails/fences/cables that should not be treated as "ground"
        // but should still be valid grind sources.
        if (leftSkiContact != null && leftSkiContact.HasAnyCollisionContact)
        {
            if (TryResolveBestFromCollider(leftSkiContact.AnyCollisionOtherCollider, referencePoint, ref bestSq, ref bestLocal))
                found = true;
        }

        if (rightSkiContact != null && rightSkiContact.HasAnyCollisionContact)
        {
            if (TryResolveBestFromCollider(rightSkiContact.AnyCollisionOtherCollider, referencePoint, ref bestSq, ref bestLocal))
                found = true;
        }


        // 2) Small allowance / air-assist: find nearby grindables even before collision.
        int count = Physics.OverlapSphereNonAlloc(
            referencePoint,
            maxDist,
            _grindOverlap,
            grindableLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = _grindOverlap[i];
            if (col == null) continue;

            if (TryResolveBestFromCollider(col, referencePoint, ref bestSq, ref bestLocal))
                found = true;
        }

        best = bestLocal;
        return found;
    }

    private void ResolveGrindProviders(Collider other, out LiftLine ll, out FencePath fp)
    {
        ll = null;
        fp = null;

        if (other == null)
            return;

        int frame = Time.frameCount;

        // Reset caches each frame (we only ever care about the current frame's repeated lookups).
        if (_grindProviderCacheFrame != frame)
        {
            _grindProviderCacheFrame = frame;
            _grindProviderCacheColA = _grindProviderCacheColB = null;
            _grindProviderCacheLiftA = _grindProviderCacheLiftB = null;
            _grindProviderCacheFenceA = _grindProviderCacheFenceB = null;
        }

        if (other == _grindProviderCacheColA)
        {
            ll = _grindProviderCacheLiftA;
            fp = _grindProviderCacheFenceA;
            return;
        }

        if (other == _grindProviderCacheColB)
        {
            ll = _grindProviderCacheLiftB;
            fp = _grindProviderCacheFenceB;
            return;
        }

        // Resolve
        ll = other.GetComponentInParent<LiftLine>();
        fp = other.GetComponentInParent<FencePath>();

        // Store (two-slot cache is enough: left + right contacts)
        if (_grindProviderCacheColA == null)
        {
            _grindProviderCacheColA = other;
            _grindProviderCacheLiftA = ll;
            _grindProviderCacheFenceA = fp;
        }
        else if (_grindProviderCacheColB == null)
        {
            _grindProviderCacheColB = other;
            _grindProviderCacheLiftB = ll;
            _grindProviderCacheFenceB = fp;
        }
    }
    private bool TryResolveBestFromCollider(Collider other, Vector3 referencePoint, ref float bestSq, ref GrindCandidate best)
    {
        if (other == null)
            return false;

        // NOTE:
        // Many grind sources (eg. pooled rope proxy colliders) may NOT be on the grindable layer,
        // even though they belong to a LiftLine/FencePath that is considered grindable.
        // So we accept a collider if:
        //  - it is on grindableLayers, OR
        //  - it resolves to a LiftLine / FencePath provider in its parent chain.
        bool layerOk = (grindableLayers.value & (1 << other.gameObject.layer)) != 0;

        ResolveGrindProviders(other, out LiftLine ll, out FencePath fp);

        if (!layerOk && ll == null && fp == null)
            return false;

        if (ll != null)
        {
            if (ll.BandLength <= 0.0001f)
                ll.RebuildAnalyticLoop();

            if (ll.TryGetClosestPointOnBand(referencePoint, out _, out Vector3 cp, out Vector3 tan))
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
                    best.sourceName = ll.name;
                }
                return true;
            }
        }

        if (fp != null)
        {
            if (fp.TryGetClosestPointOnPath(referencePoint, out _, out Vector3 cp, out Vector3 tan))
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
                return true;
            }
        }

        return false;
    }

    private bool TryResolveBestFromContact(SkiContact contact, Vector3 referencePoint, ref float bestSq, ref GrindCandidate best)
    {
        if (contact == null)
            return false;

        return TryResolveBestFromCollider(contact.CollisionOtherCollider, referencePoint, ref bestSq, ref best);
    }

    private void ApplyAirControl()
    {
        float dt = Time.fixedDeltaTime;

        // If controls consider us grounded (including grinding), do NOT apply air controls.
        // Instead gently damp any carried air angular velocity.
        if (IsGroundedForControls || HasAnySkiContact)
        {
            _airAngularVelocity = Vector3.MoveTowards(
                _airAngularVelocity,
                Vector3.zero,
                airAngularDamping * 0.75f * dt
            );
            return;
        }

        // ------------------------------------------------------------------
        // True air control (only when fully airborne)
        // ------------------------------------------------------------------

        float ramp = (airControlBlendInTime <= 0f)
            ? 1f
            : Mathf.Clamp01((Time.time - _airEntryTime) / airControlBlendInTime);

        float yawInput = HasLegInputs
            ? Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f) * ramp
            : 0f;

        float pitchAbs = HasLeanInput ? _rawLeanInput : 0f;
        float pitchDelta = HasLeanInput ? (_rawLeanInput - _airLeanBaseline) : 0f;

        float pitchInput = Mathf.Lerp(pitchAbs, pitchDelta, Mathf.Clamp01(airPitchUseDeltaFromTakeoff));

        if (Mathf.Abs(pitchInput) < airPitchDeadzone)
            pitchInput = 0f;

        if (pitchInput < 0f && pitchAbs > -airPitchDeadzone)
            pitchInput = 0f;

        pitchInput *= ramp;

        float spinMultiplier = 1f;
        if (HasPolesInput && _rawPolesPressed)
            spinMultiplier *= airTuckSpinMultiplier;

        float accel = Mathf.Max(0f, airAngularAcceleration);
        float dampingAir = Mathf.Max(0f, airAngularDamping);

        float targetYawSpeed = yawInput * airYawTurnSpeed * spinMultiplier;
        float targetPitchSpeed = pitchInput * airPitchTurnSpeed * spinMultiplier;

        _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, targetYawSpeed, accel * dt);
        _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, targetPitchSpeed, accel * dt);

        if (Mathf.Abs(yawInput) < 0.01f)
            _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, 0f, dampingAir * dt);

        if (Mathf.Abs(pitchInput) < 0.01f)
        {
            float extra = (_airAngularVelocity.x < 0f && pitchAbs > -airPitchDeadzone) ? (dampingAir * 1.75f) : dampingAir;
            _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, 0f, extra * dt);
        }

        Quaternion yawRot = Quaternion.AngleAxis(_airAngularVelocity.y * dt, Vector3.up);
        Quaternion pitchRot = Quaternion.AngleAxis(_airAngularVelocity.x * dt, transform.right);

        _rb.MoveRotation(yawRot * pitchRot * _rb.rotation);
    }

    private void ApplyGrindAirControl()
    {
        float dt = Time.fixedDeltaTime;

        // Small, forgiving air-like control while grinding.
        // This bypasses ApplyAirControl()’s grounded check.
        float yawInput = HasLegInputs
            ? Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f)
            : 0f;

        float pitchInput = HasLeanInput ? Mathf.Clamp(_rawLeanInput, -1f, 1f) : 0f;

        // Keep it looser than air to avoid fighting the rail constraint.
        float yawSpeed = airYawTurnSpeed * 0.45f;
        float pitchSpeed = airPitchTurnSpeed * 0.35f;

        Quaternion yawRot = Quaternion.AngleAxis(yawInput * yawSpeed * dt, Vector3.up);
        Quaternion pitchRot = Quaternion.AngleAxis(pitchInput * pitchSpeed * dt, transform.right);

        _rb.MoveRotation(yawRot * pitchRot * _rb.rotation);
    }

    private void ApplyGrindAlignment(float dt, Vector3 railTangent, Vector3 targetUp, float strength01)
    {
        if (dt <= 0f) return;
        if (targetUp.sqrMagnitude < 0.0001f) return;

        targetUp.Normalize();

        Quaternion current = _rb.rotation;
        Vector3 currentUp = current * Vector3.up;

        // Only correct up vector; preserve yaw freedom.
        Quaternion toUp = Quaternion.FromToRotation(currentUp, targetUp);
        Quaternion desired = toUp * current;

        float s = Mathf.Clamp01(strength01);
        float maxDeg = (alignMaxDegreesPerSec * GRIND_ALIGN_RATE_MULT) * dt * s;

        if (maxDeg > 0.0001f)
            _rb.MoveRotation(Quaternion.RotateTowards(current, desired, maxDeg));
    }

    // Align only the rider's "up" axis to the grind support normal.
    // This avoids the aggressive forward-to-tangent lock that caused flips and ejections on shallow mounts.
    private void ApplyGrindUpAlignment(float dt, Vector3 targetUp, float strength01)
    {
        if (dt <= 0f) return;
        if (targetUp.sqrMagnitude < 0.0001f) return;

        targetUp.Normalize();

        Quaternion current = _rb.rotation;

        Vector3 curUp = current * Vector3.up;
        Quaternion upFix = Quaternion.FromToRotation(curUp, targetUp);
        Quaternion desired = upFix * current;

        float s = Mathf.Clamp01(strength01);
        float maxDeg = (alignMaxDegreesPerSec * GRIND_ALIGN_RATE_MULT) * dt * s;

        if (maxDeg > 0.0001f)
            _rb.MoveRotation(Quaternion.RotateTowards(current, desired, maxDeg));
    }

    // Air-style yaw/pitch control while grinding (scaled), so the player can do emergent tricks
    // without the full "airborne" physics path taking over.
    private void ApplyGrindControl()
    {
        float dt = Time.fixedDeltaTime;

        if (dt <= 0f) return;
        if (!_grindActive) return;

        // Only apply this hybrid control when we are grinding and NOT on snow.
        if (HasAnySkiContact || _isGrounded) return;

        float mul = Mathf.Max(0f, grindAirControlMultiplier);

        float yawInput = HasLegInputs
            ? Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f)
            : 0f;

        float pitchAbs = HasLeanInput ? _rawLeanInput : 0f;
        float pitchDelta = HasLeanInput ? (_rawLeanInput - _airLeanBaseline) : 0f;

        float pitchInput = Mathf.Lerp(pitchAbs, pitchDelta, Mathf.Clamp01(airPitchUseDeltaFromTakeoff));

        if (Mathf.Abs(pitchInput) < airPitchDeadzone)
            pitchInput = 0f;

        if (pitchInput < 0f && pitchAbs > -airPitchDeadzone)
            pitchInput = 0f;

        float spinMultiplier = 1f;
        if (HasPolesInput && _rawPolesPressed)
            spinMultiplier *= airTuckSpinMultiplier;

        float accel = Mathf.Max(0f, airAngularAcceleration) * mul;
        float dampingAir = Mathf.Max(0f, airAngularDamping);

        float targetYawSpeed = yawInput * airYawTurnSpeed * spinMultiplier * mul;
        float targetPitchSpeed = pitchInput * airPitchTurnSpeed * spinMultiplier * mul;

        _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, targetYawSpeed, accel * dt);
        _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, targetPitchSpeed, accel * dt);

        if (Mathf.Abs(yawInput) < 0.01f)
            _airAngularVelocity.y = Mathf.MoveTowards(_airAngularVelocity.y, 0f, dampingAir * dt);

        if (Mathf.Abs(pitchInput) < 0.01f)
            _airAngularVelocity.x = Mathf.MoveTowards(_airAngularVelocity.x, 0f, dampingAir * dt);

        Vector3 upAxis = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;
        Quaternion yawRot = Quaternion.AngleAxis(_airAngularVelocity.y * dt, upAxis);

        // Pitch around the rider's right axis projected onto the grind plane.
        Vector3 rightAxis = Vector3.Cross(upAxis, transform.forward);
        if (rightAxis.sqrMagnitude < 0.0001f)
            rightAxis = transform.right;

        rightAxis.Normalize();
        Quaternion pitchRot = Quaternion.AngleAxis(_airAngularVelocity.x * dt, rightAxis);

        _rb.MoveRotation(yawRot * pitchRot * _rb.rotation);
    }

    private void UpdateGrinding(float dt)
    {
        if (dt <= 0f) return;

        // Simplified grinding:
        // - If we are touching ANY collider that belongs to a grind provider (LiftLine/FencePath) or is on grindableLayers,
        //   we can grind (no alignment/speed gates).
        // - Grinding provides a support spring so landing "on top" does not behave like free-fall.
        // - Controls are a hybrid: we keep air-style yaw/pitch (scaled) but we always conform "up" to the grind support normal.

        // If stacked, immediately disengage.
        if (_stacked)
        {
            _grindStrengthSmoothed = Mathf.MoveTowards(_grindStrengthSmoothed, 0f, grindStrengthResponse * dt);
            _grindActive = _grindStrengthSmoothed > 0.01f;
            if (!_grindActive) _grindTime = 0f;
            return;
        }

        bool wasActive = _grindActive;

        // Reattach cooldown to prevent chatter.
        bool canAttach = Time.time >= _grindReattachCooldownUntil;

        // Do not grind while we have valid snow/ground contact. (If you want "rail on snow" later, handle explicitly.)
        bool onSnow = HasAnySkiContact || _isGrounded;

        float targetStrength = 0f;
        GrindCandidate best = default;
        bool hasCandidate = false;

        bool touchingGrindable = false;
        Collider touchCol = null;

        if (!onSnow)
        {
            touchingGrindable = IsTouchingGrindable(out touchCol);

            if (canAttach && (touchingGrindable || _grindActive))
            {
                Vector3 p = GetGrindReferencePoint();

                float grindRadiusEff = grindCaptureRadius * _gearTuning.grindCaptureRadiusMul;

                if (TryFindBestGrindCandidate(p, grindRadiusEff, out best))
                {
                    hasCandidate = true;

                    // Normalize tangent.
                    Vector3 tan = best.tangent;
                    float tanSq = tan.sqrMagnitude;
                    if (tanSq > 0.0001f)
                    {
                        if (Mathf.Abs(tanSq - 1f) > 0.001f)
                            tan /= Mathf.Sqrt(tanSq);

                        Vector3 supportUp = ComputeGrindSupportUp(tan);

                        // Proximity (still useful for the "air capture" use-case).
                        float dist = Mathf.Sqrt(best.sqDist);
                        float prox01 = 1f - Mathf.Clamp01(dist / Mathf.Max(0.0001f, grindRadiusEff));

                        // If physically touching a grindable, engage fully. Otherwise, use proximity.
                        targetStrength = touchingGrindable ? 1f : prox01;

                        // Cache for active application.
                        if (targetStrength > 0.001f)
                        {
                            _grindClosestPoint = best.closestPoint;
                            _grindTangent = tan;
                            _grindNormal = supportUp; // used as "up" for support + alignment
                            _dbgGrindSource = best.sourceName;
                        }
                    }
                }
            }
        }

        // Smooth in/out.
        float grindResponseEff = grindStrengthResponse * _gearTuning.grindResponseMul;
        _grindStrengthSmoothed = Mathf.MoveTowards(_grindStrengthSmoothed, targetStrength, grindResponseEff * dt);
        _grindActive = _grindStrengthSmoothed > 0.01f;

        if (_grindActive && hasCandidate && !onSnow)
        {
            _grindLastTouchTime = Time.time;
            _grindTime += dt;

            // Treat as grounded-for-controls while active so jump/air logic doesn't fight it.
            _lastGroundedTime = Time.time;

            // Use the grind normal as the effective "ground normal" for other grounded helpers (slope cache, etc.).
            if (_grindNormal.sqrMagnitude > 0.0001f)
                _groundNormal = _grindNormal.normalized;

            Vector3 referencePoint = GetGrindReferencePoint();

            // Support target: sit slightly ABOVE the centerline so we don't 'fall through' when landing perfectly on it.
            Vector3 up = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal : Vector3.up;
            Vector3 desired = _grindClosestPoint + up * Mathf.Max(0f, grindSupportOffset);

            Vector3 toDesired = desired - referencePoint;
            _grindDeltaToRail = toDesired; // cache for debug/jump logic

            // Acceleration spring towards the desired support position.
            float grindSpringEff = grindSpring * _gearTuning.grindSpringMul;
            float c = ComputeGrindCriticalDamping(grindSpringEff, _grindStrengthSmoothed);

            Vector3 vel = _rb.linearVelocity;
            Vector3 axis = (toDesired.sqrMagnitude > 0.0001f) ? toDesired.normalized : up;
            Vector3 velAxis = Vector3.Project(vel, axis);

            Vector3 accel = toDesired * (grindSpringEff * _grindStrengthSmoothed) - velAxis * c;

            float grindRadiusEff = grindCaptureRadius * _gearTuning.grindCaptureRadiusMul;
            float maxAccel = grindSpringEff * grindRadiusEff * 2.0f;

            if (maxAccel > 0f && accel.sqrMagnitude > (maxAccel * maxAccel))
                accel = accel.normalized * maxAccel;

            _rb.AddForce(accel, ForceMode.Acceleration);

            // Extra damping along support normal to prevent pogo/launchiness on fences.
            float nDamp = Mathf.Max(0f, grindNormalDamping);
            if (nDamp > 0f)
            {
                float vN = Vector3.Dot(_rb.linearVelocity, up);
                _rb.AddForce(-up * (vN * nDamp), ForceMode.Acceleration);
            }

            // Strongly damp rigidbody spin while grinding to prevent exaggerated flips from shallow collisions.
            _rb.angularVelocity = Vector3.MoveTowards(_rb.angularVelocity, Vector3.zero, 25f * dt);

            // Conform "up" to the grind surface, but do NOT force forward to the tangent.
            ApplyGrindUpAlignment(dt, up, _grindStrengthSmoothed);
        }
        else
        {
            // Very short coyote time so we don't drop from a 1-frame miss.
            bool withinCoyote = wasActive && (Time.time - _grindLastTouchTime) <= grindCoyoteTime;

            if (withinCoyote && !onSnow)
            {
                _grindActive = true;
                _grindStrengthSmoothed = Mathf.Max(_grindStrengthSmoothed, 0.02f);
            }
            else
            {
                _grindActive = false;
                _grindTime = 0f;
                _dbgGrindSource = null;
            }
        }
    }

    private Vector3 ComputeGrindSupportUp(Vector3 tanN)
    {
        // Project world-up onto plane perpendicular to tangent.
        Vector3 up = Vector3.up;
        Vector3 supportUp = Vector3.ProjectOnPlane(up, tanN);

        if (supportUp.sqrMagnitude < 0.0001f)
            supportUp = Vector3.up;

        return supportUp.normalized;
    }

    // ---------------------------
    // Grinding helpers (reduced tuning params)
    // ---------------------------

    private bool IsTouchingGrindable(out Collider touchCollider)
    {
        touchCollider = null;

        if (leftSkiContact != null && leftSkiContact.HasAnyCollisionContact)
        {
            Collider c = leftSkiContact.AnyCollisionOtherCollider;
            if (c != null && (grindableLayers.value & (1 << c.gameObject.layer)) != 0)
            {
                touchCollider = c;
                return true;
            }
        }

        if (rightSkiContact != null && rightSkiContact.HasAnyCollisionContact)
        {
            Collider c = rightSkiContact.AnyCollisionOtherCollider;
            if (c != null && (grindableLayers.value & (1 << c.gameObject.layer)) != 0)
            {
                touchCollider = c;
                return true;
            }
        }

        return false;
    }

    private float ComputeGrindSideways01(float absDotDirToRail)
    {
        // Now treated as "alignment": 0 when not aligned enough, 1 when tightly aligned.
        float begin = Mathf.Clamp(grindSidewaysDotBegin, 0.0f, 0.999f);
        float full = Mathf.Lerp(begin, 1f, GRIND_ALIGN_FULL_FRAC);
        return Mathf.InverseLerp(begin, full, absDotDirToRail);
    }

    private float ComputeGrindBalance01(float upDot)
    {
        // Derive the "full balance" threshold from the begin value to avoid a second serialized parameter.
        float begin = Mathf.Clamp01(grindBalanceUpDotBegin);
        float full = Mathf.Clamp01(begin + GRIND_BALANCE_DOT_WINDOW);

        if (full <= begin + 0.0001f)
            return (upDot >= full) ? 1f : 0f;

        return Mathf.InverseLerp(begin, full, upDot);
    }

    private float ComputeGrindCriticalDamping(float spring, float strength01)
    {
        // We operate in "acceleration spring" units: x'' + c x' + k x = 0, where k = spring * strength.
        // Critical damping: c = 2*sqrt(k). This matches the previous default tuning closely.
        float k = Mathf.Max(0f, spring) * Mathf.Clamp01(strength01);
        if (k <= 0f) return 0f;
        return 2f * Mathf.Sqrt(k);
    }

    private void ApplyGrindGravity(float dt)
    {
        if (dt <= 0f) return;

        float baseScale = Mathf.Max(0f, grindDriveScale * _gearTuning.grindDriveMul);
        if (baseScale <= 0f) return;

        // If we don't have a valid tangent, don't try to drive along the rail.
        Vector3 tan = _grindTangent;
        float tanSq = tan.sqrMagnitude;
        if (tanSq < 0.0001f)
            return;

        // _grindTangent is expected to be normalized already, but avoid an unconditional Normalize()
        // every physics step.
        if (Mathf.Abs(tanSq - 1f) > 0.001f)
            tan /= Mathf.Sqrt(tanSq);

        // Gravity component along the rail tangent (correct regardless of what's under the rail).
        Vector3 gAlong = Vector3.Project(Physics.gravity, tan);

        // Time-based boost is now a fixed fraction to reduce tuning parameters.
        float t01 = (GRIND_TIME_TO_MAX_BOOST <= 0f) ? 1f : Mathf.Clamp01(_grindTime / GRIND_TIME_TO_MAX_BOOST);
        t01 = t01 * t01 * (3f - 2f * t01); // smoothstep
        float boost = 1f + (GRIND_DRIVE_TIME_BOOST_FRAC * t01);

        _rb.AddForce(gAlong * (baseScale * boost), ForceMode.Acceleration);
    }

    // ----------------------------------------------------------------------
    // SORENESS HELPERS
    // ----------------------------------------------------------------------
    private float GetPerformanceMult()
    {
        return sorenessMeter != null ? Mathf.Clamp(sorenessMeter.PerformanceMult, 0.01f, 1f) : 1f;
    }

    /// <summary>
    /// Returns 0 when moving flat/downhill, up to 1 when moving directly uphill on the current ground plane.
    /// </summary>
    private float ComputeUphill01(Vector3 dirOnPlane)
    {
        Vector3 n = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;

        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, n);
        float mag = downhill.magnitude;
        if (mag < 0.0001f)
            return 0f;

        downhill /= mag;

        Vector3 d = Vector3.ProjectOnPlane(dirOnPlane, n);
        if (d.sqrMagnitude < 0.0001f)
            return 0f;

        d.Normalize();
        return Mathf.Clamp01(Vector3.Dot(d, -downhill));
    }

    private float ComputeGenericStackSeverity01()
    {
        Vector3 n = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;
        Vector3 v = _rb != null ? _rb.linearVelocity : Vector3.zero;

        float planar = Vector3.ProjectOnPlane(v, n).magnitude;
        float down = Mathf.Max(0f, -Vector3.Dot(v, n));
        float tilt = Vector3.Angle(transform.up, n);

        float sSpeed = Mathf.InverseLerp(2f, 18f, planar + down);
        float sTilt = Mathf.InverseLerp(maxLandingTiltAngle, 120f, tilt);

        return Mathf.Clamp01(Mathf.Max(sSpeed, sTilt));
    }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

        if (debugShowGrinding)
        {
            GUILayout.Space(6);
            GUILayout.Label("=== Grinding ===");

            bool grindingSurface = _grindActive && !HasAnySkiContact && !_isGrounded;
            bool onSnow = HasAnySkiContact || _isGrounded;

            float cdLeft = Mathf.Max(0f, _grindReattachCooldownUntil - Time.time);
            float sinceTouch = Time.time - _grindLastTouchTime;

            float distToSupport = _grindDeltaToRail.magnitude;
            Vector3 tan = (_grindTangent.sqrMagnitude > 0.0001f) ? _grindTangent.normalized : Vector3.zero;
            Vector3 up = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;

            bool touchingGrindableHud = IsTouchingGrindable(out Collider touchColHud);
            string touchName = (touchColHud != null) ? touchColHud.name : "(none)";
            int touchLayer = (touchColHud != null) ? touchColHud.gameObject.layer : -1;

            float upDot = Vector3.Dot(transform.up, up);
            float vAlongTan = (tan != Vector3.zero) ? Vector3.Dot(_rb.linearVelocity, tan) : 0f;
            float vUp = Vector3.Dot(_rb.linearVelocity, up);

            GUILayout.Label($"Active: {_grindActive}    GrindingSurface: {grindingSurface}    OnSnow: {onSnow}");
            GUILayout.Label($"Strength: {_grindStrengthSmoothed:F2}    Time: {_grindTime:F2}s    SinceTouch: {sinceTouch:F2}s    ReattachCD: {cdLeft:F2}s");
            GUILayout.Label($"Source: {(_dbgGrindSource ?? "(none)")}    DistToSupport: {distToSupport:F2} m");
            GUILayout.Label($"Touching: {touchingGrindableHud}    Touch: {touchName}    Layer: {touchLayer}");
            GUILayout.Label($"UpDot(rbUp,up): {upDot:F2}    vAlongTan: {vAlongTan:F2} m/s    vUp: {vUp:F2} m/s");

            // ---- HUD-only candidate probe (helps diagnose 'never grinds') ----
            Vector3 refPt = GetGrindReferencePoint();
            float grindRadiusEff = grindCaptureRadius * _gearTuning.grindCaptureRadiusMul;

            GrindCandidate cand;
            bool candFound = TryFindBestGrindCandidate(refPt, grindRadiusEff, out cand);

            GUILayout.Space(4);
            GUILayout.Label("--- Candidate Probe ---");
            GUILayout.Label($"RefPt: {refPt}    Radius: {grindRadiusEff:F2} m");

            if (!candFound)
            {
                GUILayout.Label("Candidate: (none within radius)");
            }
            else
            {
                float dist = Mathf.Sqrt(cand.sqDist);
                float prox01 = 1f - Mathf.Clamp01(dist / Mathf.Max(0.0001f, grindRadiusEff));
                float target = touchingGrindableHud ? 1f : prox01;

                Vector3 tanC = cand.tangent;
                if (tanC.sqrMagnitude > 0.0001f) tanC.Normalize();

                GUILayout.Label($"Candidate: {cand.sourceName}    Dist: {dist:F2} m    Prox01: {prox01:F2}    TargetStrength: {target:F2}");
                GUILayout.Label($"CandPoint: {cand.closestPoint}    CandTan: {tanC}");
            }
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
#endif
}


