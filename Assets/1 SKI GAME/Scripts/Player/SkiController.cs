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

    public enum AerialStyleMode
    {
        None = 0,
        Stylish = 1,
        Tweaked = 2,
        Stretched = 3
    }

    public enum AerialPoseFamily
    {
        None = 0,
        Neutral = 1,
        Left = 2,
        Right = 3,
        Spread = 4
    }

    public enum AerialPoseShape
    {
        None = 0,
        Neutral = 1,
        Compact = 2,
        Driving = 3,
        LaidOut = 4
    }

    public enum AerialOrientationModifier
    {
        None = 0,
        Switch = 1,
        Inverted = 2,
        Sideways = 3,
        Rising = 4,
        Diving = 5,
        OnSide = 6,
        ChestDown = 7,
        ChestUp = 8
    }

    public enum GrindSurfaceKind
    {
        None = 0,
        ImplicitSurface = 1,
        LiftLine = 2,
        FencePath = 3
    }

    public enum GrindStance
    {
        Centered = 0,
        Nose = 1,
        Tail = -1
    }

    public enum SkiEndSlideState
    {
        None,
        LeftNose,
        RightNose,
        BothNose,
        LeftTail,
        RightTail,
        BothTail,
        Mixed
    }

    public readonly struct GrindStateInfo
    {
        public readonly bool active;
        public readonly float distance;
        public readonly float time;
        public readonly float strength01;
        public readonly int dominantEndSign;
        public readonly GrindStance stance;
        public readonly GrindSurfaceKind surfaceKind;
        public readonly string sourceName;
        public readonly Vector3 tangent;
        public readonly Vector3 supportUp;

        public GrindStateInfo(
            bool active,
            float distance,
            float time,
            float strength01,
            int dominantEndSign,
            GrindStance stance,
            GrindSurfaceKind surfaceKind,
            string sourceName,
            Vector3 tangent,
            Vector3 supportUp)
        {
            this.active = active;
            this.distance = distance;
            this.time = time;
            this.strength01 = strength01;
            this.dominantEndSign = dominantEndSign;
            this.stance = stance;
            this.surfaceKind = surfaceKind;
            this.sourceName = sourceName;
            this.tangent = tangent;
            this.supportUp = supportUp;
        }
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

    private struct TrickPoseIntentSnapshot
    {
        public bool valid;
        public float capturedTime;
        public bool poseButtonHeld;
        public AerialPoseFamily family;
        public AerialPoseShape shape;
        public AerialOrientationModifier orientation;
        public TrickPoseVerticalOrientationRequirement verticalOrientation;
        public TrickPoseHorizontalOrientationRequirement horizontalOrientation;
        public TrickPoseTravelFacingRequirement travelFacing;

        public TrickPoseMotionStateRequirement motionState;
        public string poseName;
        public int spinDirectionSign;
        public int flipDirectionSign;
        public Vector3 entryEulerAngles;
    }

    private struct TrickPoseCandidateScore
    {
        public TrickPoseEntry entry;
        public int specificity;
        public float rawScore;
        public float totalScore;
        public float intentBias;
    }


    // ----------------------------------------------------------------------
    // REFERENCES
    // ----------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Optional visual body root that leans independently of the skis.")]
    [SerializeField] private Transform bodyTransform;

    [SerializeField] private Transform bodyMotionTransform;
    [SerializeField] private Transform bodyScaleTransform;

    [SerializeField] private Transform headAnchorTransform;

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
    [SerializeField]
    private bool acceptPlayerInput = true;

    public bool AcceptPlayerInput
    {
        get => acceptPlayerInput;
        set
        {
            acceptPlayerInput = value;

            if (!acceptPlayerInput)
                ClearRawInputState(clearJumpState: true);
        }
    }

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

    [SerializeField] private InputActionReference tuckAction;

    [SerializeField] private InputActionReference poseAction;

    [Header("External Input")]
    [Tooltip("Optional component implementing ISkiInputSource. When assigned and active, it overrides player input and can drive this skier as an NPC.")]
    [SerializeField] private MonoBehaviour externalInputSourceBehaviour;

    private ISkiInputSource _externalInputSource;

    private bool HasExternalInputSource =>
        externalInputSourceBehaviour != null &&
        externalInputSourceBehaviour.isActiveAndEnabled &&
        _externalInputSource != null &&
        _externalInputSource.HasInput();

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
    private bool _rawTuckPressed;
    private bool _rawPosePressed;

    // Smoothed lean & stance
    private float _forwardLean;    // [-1..1]
    private float _sideLean;       // [-1..1] (visual only)
    private float _leftOut;        // [0..1]
    private float _rightOut;       // [0..1]
    private float _tuck01;

    // Body visual base pose cache
    private Vector3 _bodyBaseLocalPos;
    private Quaternion _bodyBaseLocalRot = Quaternion.identity;
    private Vector3 _bodyBaseLocalScale = Vector3.one;

    private Vector3 _bodyMotionBaseLocalPos;
    private Quaternion _bodyMotionBaseLocalRot = Quaternion.identity;
    private Vector3 _bodyScaleBaseLocalScale = Vector3.one;

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

    private bool HasAnySkiEndGroundContact =>
    (leftSkiContact != null &&
     leftSkiContact.HasEndContact &&
     leftSkiContact.EndContactSign != 0 &&
     IsRideableNormal(leftSkiContact.ContactNormal)) ||
    (rightSkiContact != null &&
     rightSkiContact.HasEndContact &&
     rightSkiContact.EndContactSign != 0 &&
     IsRideableNormal(rightSkiContact.ContactNormal));

    private bool HasAnyBaseSkiProbeContact()
    {
        return (leftSkiContact != null && leftSkiContact.HasBaseSupportContact) ||
               (rightSkiContact != null && rightSkiContact.HasBaseSupportContact);
    }

    private bool HasRealSkiGroundContact()
    {
        return HasAnySkiCollisionContact;
    }

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

    private Vector3 _resolvedSkiSupportNormal = Vector3.up;
    private float _lastResolvedSkiSupportNormalTime = -999f;
    private string _lastResolvedSkiSupportNormalSource = "none";

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

    // Visual-only stack overrides pushed by SkierLimbLineVisual so the skis
    // can loosely follow the simulated feet while stacked without affecting
    // core locomotion or recovery physics.
    private bool _leftStackSkiVisualOverrideActive;
    private bool _rightStackSkiVisualOverrideActive;
    private Vector3 _leftStackSkiVisualOverrideWorldPos;
    private Vector3 _rightStackSkiVisualOverrideWorldPos;
    private Quaternion _leftStackSkiVisualOverrideWorldRot = Quaternion.identity;
    private Quaternion _rightStackSkiVisualOverrideWorldRot = Quaternion.identity;
    private float _leftStackSkiVisualOverrideWeight;
    private float _rightStackSkiVisualOverrideWeight;

    // Stack state
    private bool _stacked;
    private bool _stackRecoveryExternallyLocked;
    private float _stackedAtTime = -999f;
    private bool _hasPendingStackImpact;
    private Vector3 _pendingStackImpactNormal;
    private Vector3 _pendingStackImpactVelocity;
    private Vector3 _pendingStackImpactPoint;
    private SkiEndSlideState _skiEndSlideState = SkiEndSlideState.None;
    private float _skiEndSlide01;
    private float _lastSkiEndSlideTime = -999f;
    private float _lastStackedSecondaryImpactTime = -999f;

    private float _endContactFlattenRecoveryUntil = -999f;
    private float _endContactFlattenRecoveryStartedAt = -999f;
    private Vector3 _endContactFlattenRecoveryNormal = Vector3.up;
    private int _endContactFlattenRecoverySign;

    private bool _landingEvaluationConsumedForCurrentAirborne;
    private float _lastEndContactFlattenRecoveryProbeDistance = float.PositiveInfinity;
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
    private float _lastNonSkiGroundContactTime = -999f;

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
    private float _lastSkiJumpCharge01;

    private float _airborneStartTime;
    private float _airbornePeakY;
    private Vector3 _lastAirPlanarVel; // planar w.r.t world-up while airborne

    // Air-entry smoothing: capture takeoff lean so small airtime doesn't instantly pitch the skier.
    private float _airEntryTime;
    private float _airLeanBaseline;

    // Air rotation state (degrees/second around local axes)
    private Vector3 _airAngularVelocity;
    [SerializeField, HideInInspector] private TrickPoseRigSnapshot _authoredBaseRigSnapshot;
    [SerializeField, HideInInspector] private bool _authoredBaseRigSnapshotCaptured;
    private TrickPoseRigSnapshot _trueDefaultRigSnapshot;
    private TrickPoseRigSnapshot _defaultRigSnapshot;
    private TrickPoseEntry _activeTrickPoseEntry;
    private float _activeTrickPoseBlend;
    private float _activeTrickPoseScore;
    private float _activeTrickPoseCommittedAt = -999f;
    private float _activeTrickPoseLastValidTime = -999f;
    private TrickPoseEntry _bestCompetingTrickPoseEntry;
    private float _bestCompetingTrickPoseScore;
    private TrickPoseIntentSnapshot _trickPoseIntentSnapshot;
    private string _lastTrickPoseDecisionReason = string.Empty;
    private bool _trickPoseWasAirborneLastFrame;
    private bool _trickPoseWasPoseButtonHeldLastFrame;
    private bool _poseRigDefaultsCaptured;
    private bool _truePoseRigDefaultsCaptured;
    private SkierLimbLineVisual _skierLimbLineVisual;

    // ----------------------------------------------------------------------
    // GRINDING runtime state
    // ----------------------------------------------------------------------

    private bool _grindActive;
    private float _grindTime;
    private float _grindDistance;
    private float _grindStrengthSmoothed;
    private int _grindDominantEndSign;
    private int _grindRawEndSign;
    private float _grindStanceTimer;
    private Collider _grindActiveCollider;
    private GrindSurfaceKind _grindSurfaceKind;
    private GrindStance _grindStance;
    private Vector3 _lastGrindClosestPoint;

    // Cached best grind frame data (debug + detach impulse direction)
    private Vector3 _grindClosestPoint;
    private Vector3 _grindTangent;
    private Vector3 _grindDeltaToRail;
    private string _dbgGrindSource;
    private Vector3 _grindNormal = Vector3.up;
    private Vector3 _grindHybridAngularVelocity;

    private readonly Collider[] _grindOverlapBuffer = new Collider[32];
    private readonly RaycastHit[] _grindHitBuffer = new RaycastHit[32];
    private string _dbgGrindCandidateSource = "none";
    private string _dbgGrindRejectReason = "none";

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

    [Header("Tuck")]
    [Tooltip("How quickly tuck blends in/out.")]
    [SerializeField] private float tuckLerpSpeed = 10f;

    [Tooltip("Extra reduction to forward friction while tucked.")]
    [SerializeField, Range(0f, 0.8f)] private float tuckFrictionReductionExtra = 0.22f;

    [Tooltip("How much tuck reduces ground turn/carve authority.")]
    [SerializeField, Range(0f, 0.75f)] private float tuckTurnReduction = 0.18f;

    [Tooltip("How much tuck reduces quick-stop braking authority.")]
    [SerializeField, Range(0f, 0.75f)] private float tuckQuickStopReduction = 0.25f;

    [Header("Tuck Visuals")]
    [Tooltip("Extra body pitch applied while tucked.")]
    [SerializeField] private float tuckBodyPitchExtra = 18f;

    [Tooltip("How far the body visual lowers toward the skis while tucked.")]
    [SerializeField] private float tuckBodyDrop = 0.22f;

    [Tooltip("Body Y scale while fully tucked.")]
    [SerializeField] private float tuckBodyYScale = 0.78f;

    [Tooltip("Body Z scale while fully tucked.")]
    [SerializeField] private float tuckBodyZScale = 0.92f;

    [Header("Tuck Head")]
    [SerializeField] private float tuckHeadDown = 0.08f;
    [SerializeField] private float tuckHeadForward = 0.04f;

    private Vector3 _headAnchorBaseLocalPos;
    private Quaternion _headAnchorBaseLocalRot = Quaternion.identity;

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

    [SerializeField] private float maxSkiOnlyGroundedBodyGap = 1.25f;

    [Header("Ground Detection - Ski Fallback")]
    [SerializeField] private bool useSkiTransformGroundFallback = true;
    [SerializeField] private float skiTransformFallbackProbeUp = 0.65f;
    [SerializeField] private float skiTransformFallbackProbeDown = 1.35f;
    [SerializeField] private float skiTransformFallbackRadius = 0.06f;
    [SerializeField] private float skiTransformFallbackMaxGap = 0.35f;
    [SerializeField] private float skiTransformFallbackCoyoteTime = 0.08f;
    [SerializeField] private float skiEndFallbackMaxGap = 0.55f;
    [SerializeField] private float skiEndFallbackProbeUp = 0.85f;
    [SerializeField] private float skiEndFallbackProbeDown = 1.75f;
    [SerializeField] private float skiEndFallbackRadius = 0.08f;
    [SerializeField] private float endContactFlattenAngularDamp = 8f;

    private bool _bodyNearGround;
    private bool _skiContactPlausibleForBody;
    private float _lastMeasuredGroundGap = float.PositiveInfinity;
    private string _lastGroundRefreshSource = "none";
    private Collider _lastGroundProbeAcceptedCollider;
    private Collider _lastGroundProbeIgnoredCollider;
    private string _lastGroundProbeIgnoredReason = "none";
    private float _lastGroundProbeAcceptedDistance = float.PositiveInfinity;
    private Vector3 _lastGroundProbeAcceptedNormal = Vector3.up;
    private float _lastSkiTransformFallbackTime = -999f;
    private Vector3 _lastSkiTransformFallbackNormal = Vector3.up;
    private float _lastSkiTransformFallbackGap = float.PositiveInfinity;
    private bool _lastBodyProbeHit;
    private float _lastBodyProbeDistance = float.PositiveInfinity;
    private float _lastBodyProbeGap = float.PositiveInfinity;
    private float _lastBodyProbeAllowedGap;
    private bool _lastBodyProbeAcceptedButTooFar;
    private bool _lastSkiFallbackFreshHit;
    private string _lastSkiFallbackSource = "none";
    private float _lastSkiFallbackFreshTime = -999f;
    private string _lastNoseTailLandingSource = "none";
    private string _lastGroundedFrom = "none";
    private float _lastRealSupportTime = -999f;
    private bool _probeOnlyGrounded;
    private bool _probeOnlyEndGrounded;
    private float _probeOnlyGroundGap = float.PositiveInfinity;
    private string _probeOnlyGroundSource = "none";
    private bool _dbgGroundForceRanLastFixed;
    private bool _dbgSkateRanLastFixed;
    private bool _dbgPoleRanLastFixed;
    private bool _dbgPoseInputActive;

    private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[16];
    private static int _npcLayer = -2;
    private static int _playerLayer = -2;
    public bool IsBodyNearGround => _bodyNearGround;
    public bool IsSkiContactPlausibleForBody => _skiContactPlausibleForBody;
    public float LastMeasuredGroundGap => _lastMeasuredGroundGap;
    public string LastGroundRefreshSource => _lastGroundRefreshSource;

    [Header("Ground Normal Smoothing")]
    [Tooltip("How quickly the detected ground normal smooths toward new values when near ground. Higher = snappier, lower = less jitter.")]
    [SerializeField] private float groundNormalSmoothSpeed = 12f;

    [Tooltip("How long (seconds) after leaving ground we still allow 'grounded controls' (turning/stance) to feel responsive.")]
    [SerializeField] private float controlCoyoteTime = 0.08f;

    [Header("Slope Normal Authority")]
    [Tooltip("How quickly the controller resolves ski support normals from probe/terrain normals while ski collision is active.")]
    [SerializeField] private float skiSupportNormalResolveSpeed = 22f;

    [Tooltip("How long to retain the last resolved slope normal when collision flickers during landing or one-ski contact.")]
    [SerializeField] private float skiSupportNormalCacheTime = 0.12f;

    [Tooltip("If ski contact normals are nearly world-up but the terrain ray normal is sloped by at least this many degrees, prefer the terrain ray normal for alignment.")]
    [SerializeField] private float terrainRayNormalOverrideAngle = 3f;

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


    [Header("Tip / End Contact Stability")]
    [Tooltip("Fraction of grounded skis that must be end-weighted before tip/tail instability can start building toward a stack. Higher values make accidental tip digs more forgiving.")]
    [SerializeField, Range(0f, 1f)]
    private float tipContactStackFraction = 0.8f;

    [Tooltip("Minimum planar speed before prolonged nose/tail-heavy contact starts counting as a true instability instead of a harmless slow-speed wobble.")]
    [SerializeField]
    private float tipContactMinSpeed = 3.5f;

    [Tooltip("How long sustained end-weighted contact can persist before it becomes a stack when the skis are not recovering back onto their bases.")]
    [SerializeField]
    private float tipContactStackTime = 0.38f;

    [Tooltip("Minimum ski base alignment required before a ski contact is trusted to refresh the shared ground normal. Lower values make edge-heavy contacts influence grounding more strongly.")]
    [SerializeField, Range(0f, 1f)]
    private float minBaseAlignForGroundNormal = 0.18f;

    [Tooltip("If a ski's base alignment falls below this while grounded, it is treated as an immediate severe edge/tip failure unless a stronger recovery rule saves it.")]
    [SerializeField, Range(0f, 1f)]
    private float severeEdgeAlignment = 0.02f;

    [Tooltip("Low-speed base-alignment threshold used before stacking unstable edge/tip contact. Higher values make slow-speed end digs fail sooner instead of flattening out.")]
    [SerializeField, Range(0f, 1f)]
    private float lowSpeedEdgeStackAlignment = 0.28f;

    [Header("Nose / Tail Slide")]
    [Tooltip("Enables explicit high-speed nose/tail slide qualification so valid presses can be ridden without immediately flattening back to the snow.")]
    [SerializeField] private bool enableNoseTailSlides = true;

    [Tooltip("Minimum planar speed required to intentionally remain balanced on ski tips/tails without flattening immediately.")]
    [SerializeField] private float noseTailSlideMinSpeed = 4.0f;

    [Tooltip("Speed where nose/tail slide support is strongest.")]
    [SerializeField] private float noseTailSlideFullSpeed = 9.0f;

    [Tooltip("Below this speed, nose/tail slides are forced to flatten back toward normal skiing.")]
    [SerializeField] private float noseTailSlideFlattenBelowSpeed = 3.0f;

    [Tooltip("Minimum lean match required to hold a nose/tail slide. Forward lean holds nose slides, backward lean holds tail slides.")]
    [SerializeField, Range(0f, 1f)] private float noseTailSlideLeanThreshold = 0.25f;

    [Tooltip("Minimum base alignment allowed during a nose/tail slide before it becomes too unstable and stacks.")]
    [SerializeField, Range(-0.1f, 0.4f)] private float noseTailSlideMinBaseAlignment = 0.02f;

    [Tooltip("How much to reduce pitch/roll flattening while a valid nose/tail slide is active.")]
    [SerializeField, Range(0f, 1f)] private float noseTailSlideAlignmentSuppression = 0.7f;

    [Tooltip("How quickly the tracked nose/tail slide state fades after the contact no longer qualifies.")]
    [SerializeField] private float noseTailSlideStateGrace = 0.16f;

    [Tooltip("Extra grace time multiplier applied to tip-heavy contact when the lean matches the active nose/tail balance.")]
    [SerializeField] private float tipBalanceGraceMultiplier = 1.8f;

    [Tooltip("Maximum grounded pitch bias in degrees while intentionally balancing on ski tips/tails.")]
    [SerializeField] private float tipBalancePitchBias = 9f;

    [Header("Nose / Tail Landing Recovery")]
    [Tooltip("When a tip/tail landing is too slow or too poorly matched to become a slide, briefly keep ski controls active while forcing the skier to flatten/recover instead of falling into floaty airborne limbo.")]
    [SerializeField] private bool enableEndContactFlattenRecovery = true;

    [Tooltip("How long a low-speed tip/tail landing can use the explicit flatten/recovery control state.")]
    [SerializeField] private float endContactFlattenRecoveryDuration = 0.24f;

    [Tooltip("Downward acceleration applied during low-speed tip/tail flatten recovery. This pulls the skier back toward real ski collision instead of hovering on end probes.")]
    [SerializeField] private float endContactFlattenRecoveryDownAccel = 22f;

    [Tooltip("Damps Rigidbody angular velocity during low-speed tip/tail flatten recovery.")]
    [SerializeField] private float endContactFlattenRecoveryAngularDamp = 14f;

    [Tooltip("Extra damping applied to velocity away from the terrain normal during flatten recovery.")]
    [SerializeField] private float endContactFlattenRecoveryAwayDamp = 8f;

    [Tooltip("How much of normal landing velocity projection to apply when a low-speed tip/tail contact is forced to flatten instead of slide.")]
    [SerializeField, Range(0f, 1f)] private float endContactFlattenRecoveryProjection = 0.35f;

    [Tooltip("Minimum time since leaving ground before tip/tail flatten recovery is allowed. Prevents jump takeoff from being mistaken for a failed landing.")]
    [SerializeField] private float endContactFlattenRecoveryMinAirTime = 0.10f;

    [Tooltip("If velocity along the contact normal is above this, the skier is moving away from the snow, so flatten recovery is blocked.")]
    [SerializeField] private float endContactFlattenRecoveryMaxRisingSpeed = 0.05f;

    [Tooltip("After a real jump, suppress tip/tail flatten recovery so it cannot pull the player back down.")]
    [SerializeField] private float endContactFlattenRecoveryPostJumpSuppressTime = 0.30f;

    [Tooltip("Allows an active end-contact flatten recovery to count as jumpable support. The recovery is cancelled as soon as the jump fires.")]
    [SerializeField] private bool endContactFlattenRecoveryAllowsJump = true;

    [Tooltip("Maximum current tip/tail probe distance allowed for flatten recovery. Larger hits are prediction only and must not create grounded-style recovery.")]
    [SerializeField] private float endContactFlattenRecoveryMaxEndProbeDistance = 0.22f;

    [Tooltip("Maximum body probe gap allowed for flatten recovery when the body probe has a valid terrain hit. Prevents 'landing on air' far above the terrain.")]
    [SerializeField] private float endContactFlattenRecoveryMaxBodyGap = 0.70f;

    [Tooltip("When true, flatten recovery only applies corrective damping/downforce and never enables full ground/skate/pole forces.")]
    [SerializeField] private bool endContactFlattenRecoveryCorrectiveOnly = true;

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

    [Header("Air Style")]
    [Tooltip("How quickly the style modifier blends in/out while airborne.")]
    [SerializeField] private float airStyleBlendSpeed = 10f;

    [Tooltip("Extra body yaw used for tweaked / stylish aerial poses.")]
    [SerializeField] private float airStyleBodyYaw = 14f;

    [Tooltip("Extra body roll used for tweaked / stylish aerial poses.")]
    [SerializeField] private float airStyleBodyRoll = 10f;

    [Tooltip("How much the stretched pose opens the body back up in the air.")]
    [SerializeField] private float airStyleOpenPitch = 10f;

    private float _airStyle01;

    [Tooltip("How much the body drops during aerial style poses.")]
    [SerializeField] private float airStyleBodyDrop = 0.06f;

    [Tooltip("How far the body shifts sideways during aerial style poses.")]
    [SerializeField] private float airStyleBodySideOffset = 0.05f;

    [Tooltip("How far the body shifts back during aerial style poses.")]
    [SerializeField] private float airStyleBodyBackOffset = 0.04f;

    [Tooltip("Extra head lift/opening during stretched aerial style poses.")]
    [SerializeField] private float airStyleHeadUp = 0.03f;

    [Header("Authored Trick Poses")]
    [Tooltip("Authored trick-pose profile used for snapshot application, recognition, and trick-pose rig assist data.")]
    [SerializeField] private TrickPoseProfileSO trickPoseProfile;
    [Tooltip("Prints authored trick-pose matching, switching, and retention decisions so pose-recognition tuning is easier in Play Mode.")]
    [SerializeField] private bool debugAuthoredTrickPose;

    [Header("Authored Trick Recognition")]
    [Tooltip("Minimum time a recognized authored pose must remain valid before the runtime treats it as committed. Higher values reduce pose switching noise.")]
    [SerializeField] private float trickPoseCommitMinHoldTime = 0.22f;
    [Tooltip("How much better a competing pose score must be before the runtime switches away from the current authored pose. Higher values make pose selection more stable.")]
    [SerializeField] private float trickPoseSwitchScoreMargin = 2f;
    [Tooltip("Grace window that keeps the current authored pose alive after it briefly stops matching. Higher values smooth recognition through noisy transitions.")]
    [SerializeField] private float trickPoseRetentionGraceTime = 0.18f;
    [Tooltip("How long after takeoff we preserve a snapshot of the rider's intent for authored pose matching. Larger values make takeoff input matter longer.")]
    [SerializeField] private float trickPoseIntentSnapshotWindow = 0.2f;
    [Tooltip("Strength of the intent-bias bonus applied when a candidate authored pose matches the rider's captured takeoff intent.")]
    [SerializeField, Range(0f, 3f)] private float trickPoseIntentBiasStrength = 1f;
    [Tooltip("Shows authored trick recognition decisions and score comparisons in debug output.")]
    [SerializeField] private bool debugTrickPoseRecognition;

    [Header("Passive Air Drift")]
    [Tooltip("Adds subtle velocity-driven ski drift while airborne so skis feel lighter and less rigid in the air.")]
    [SerializeField] private bool enablePassiveAirDrift = true;
    [Tooltip("Maximum visual ski position offset added by passive air drift. Higher values make skis float farther from their anchors.")]
    [SerializeField] private float passiveAirDriftMaxPosition = 0.05f;
    [Tooltip("Maximum visual ski rotation added by passive air drift. Higher values make airborne skis flutter more.")]
    [SerializeField] private float passiveAirDriftMaxRotation = 7f;
    [Tooltip("How strongly local air velocity drives passive ski drift. Higher values respond more aggressively to airspeed.")]
    [SerializeField] private float passiveAirDriftVelocityInfluence = 0.06f;
    [Tooltip("How quickly passive air drift reacts to velocity changes. Higher values are snappier, lower values feel floatier.")]
    [SerializeField] private float passiveAirDriftLerpSpeed = 6f;
    [Tooltip("Multiplier applied to passive air drift while an authored pose is active. Lower values keep posed skis cleaner and less windblown.")]
    [SerializeField, Range(0f, 1f)] private float passiveAirDriftWhilePoseMultiplier = 0.2f;

    [Header("Grinding")]
    [Tooltip("When enabled, grinding is tracked from ski contact with grindable layers. Grinding is a slick movement modifier, not a sticky rail/path constraint.")]
    [SerializeField] private bool useSimpleContactGrinding = true;

    [Tooltip("Layers that count as grindable when touched by either ski. These can also be ground layers if large grindable objects should be rideable.")]
    [SerializeField] private LayerMask grindableLayers = 0;

    [Tooltip("Minimum planar speed required before grind contact is tracked as an active grind.")]
    [SerializeField] private float grindMinPlanarSpeed = 0.5f;

    [Tooltip("Radius used to search around each ski/probe for nearby grindable surfaces when direct collision callbacks are not enough.")]
    [SerializeField] private float grindContactProbeRadius = 0.28f;

    [Tooltip("How far above each ski/probe point the grindable surface spherecast starts.")]
    [SerializeField] private float grindContactProbeUp = 0.45f;

    [Tooltip("How far below each ski/probe point the grindable surface spherecast searches.")]
    [SerializeField] private float grindContactProbeDown = 1.25f;

    [Tooltip("When true, standing or moving very slowly on a grindable surface still counts as grind support/control, but trick distance only accumulates while moving.")]
    [SerializeField] private bool grindAllowsStationaryContactSupport = true;

    [Tooltip("When true, grind detection searches around ski base/tip/tail probe points, not just cached collision contacts.")]
    [SerializeField] private bool useNearbyGrindableProbeSearch = true;

    [Tooltip("Priority bonus for grindable colliders found near ski probe points. Higher values prefer nearby rails/props over broad terrain contacts.")]
    [SerializeField] private float nearbyGrindableProbePriority = 0.18f;

    [Tooltip("How quickly the grind movement modifier ramps while contact is present or absent.")]
    [SerializeField] private float grindStrengthResponse = 20f;

    [Header("Grinding - Movement Modifier")]
    [Tooltip("Multiplier applied to ski friction while grinding. Lower values retain more speed and create a slicker surface.")]
    [SerializeField, Range(0f, 1f)] private float grindFrictionMultiplier = 0.55f;

    [Tooltip("Multiplier applied to carve steering while grinding. Lower values make velocity direction follow momentum/gravity more than facing direction.")]
    [SerializeField, Range(0f, 1f)] private float grindCarveSteerMultiplier = 0.65f;

    [Tooltip("Multiplier applied to quick-stop braking while grinding.")]
    [SerializeField, Range(0f, 1f)] private float grindQuickStopMultiplier = 0.45f;

    [Tooltip("Multiplier applied to wedge braking while grinding.")]
    [SerializeField, Range(0f, 1f)] private float grindWedgeBrakeMultiplier = 0.55f;

    [Tooltip("Multiplier applied to slope-parallel gravity while grinding. Slightly above 1 makes grindable surfaces feel slicker without forcing a path.")]
    [SerializeField, Range(0f, 2f)] private float grindSlopeGravityMultiplier = 1.05f;

    [Header("Grinding - Ice Traction")]
    [Tooltip("How long it takes for player-applied traction to build after entering a grind. Higher values feel icier and less instantly responsive.")]
    [SerializeField] private float grindTractionBuildTime = 0.65f;

    [Tooltip("Minimum traction available immediately after entering a grind.")]
    [SerializeField, Range(0f, 1f)] private float grindMinTractionFactor = 0.18f;

    [Tooltip("Multiplier applied to skating impulses while grinding. Lower values make grindables feel slick and low-traction.")]
    [SerializeField, Range(0f, 1.5f)] private float grindSkateImpulseMultiplier = 0.38f;

    [Tooltip("Multiplier applied to pole push acceleration while grinding.")]
    [SerializeField, Range(0f, 1.5f)] private float grindPolePushMultiplier = 0.45f;

    [Tooltip("Multiplier applied to pole drag/braking while grinding. Lower values make it take longer to stop.")]
    [SerializeField, Range(0f, 1f)] private float grindPoleBrakeMultiplier = 0.25f;

    [Tooltip("Small forward traction assist while grinding at low speed. This helps the player start moving on large flat grindables without making grindables feel like normal snow.")]
    [SerializeField] private float grindLowSpeedDriveAccel = 1.2f;

    [Tooltip("Low-speed grind drive fades out by this planar speed.")]
    [SerializeField] private float grindLowSpeedDriveMaxSpeed = 4f;

    [Header("Grinding - Hybrid Orientation Control")]
    [Tooltip("Extra yaw/orientation authority while grinding. Higher values let the player spin/turn the body more easily while sliding.")]
    [SerializeField, Range(0f, 40f)] private float grindYawTorque = 18f;

    [Tooltip("Extra pitch authority while grinding. Higher values make nose/tail balancing and presses more responsive.")]
    [SerializeField, Range(0f, 40f)] private float grindPitchTorque = 16f;

    [Tooltip("Extra roll authority while grinding. Higher values make edge/side lean more responsive.")]
    [SerializeField, Range(0f, 40f)] private float grindRollTorque = 12f;

    [Tooltip("Additional multiplier applied to grind orientation control while tucked.")]
    [SerializeField, Range(0f, 3f)] private float grindTuckControlBonus = 0.9f;

    [Tooltip("Maximum angular velocity contribution from grind hybrid control.")]
    [SerializeField, Range(0f, 25f)] private float grindMaxHybridAngularSpeed = 10f;

    [Header("Grinding - Nose / Tail Support")]
    [Tooltip("Multiplier applied to nose/tail slide minimum speed while grinding. Lower values make presses easier to hold.")]
    [SerializeField, Range(0.1f, 1f)] private float grindNoseTailMinSpeedMultiplier = 0.5f;

    [Tooltip("Multiplier applied to nose/tail slide lean threshold while grinding. Lower values require less lean to hold a press.")]
    [SerializeField, Range(0.1f, 1f)] private float grindNoseTailLeanThresholdMultiplier = 0.55f;

    [Tooltip("Extra alignment suppression added to valid nose/tail slides while grinding.")]
    [SerializeField, Range(0f, 0.5f)] private float grindNoseTailAlignmentSuppressionBonus = 0.25f;

    [Tooltip("Minimum front/rear probe distance advantage used to classify a grind as nose or tail weighted.")]
    [SerializeField] private float grindEndProbeBias = 0.035f;

    [Tooltip("Forward/back lean threshold required to commit from a centered grind into a nose or tail press.")]
    [SerializeField, Range(0f, 1f)] private float grindStanceLeanThreshold = 0.25f;

    [Tooltip("How long a valid nose/tail press must be held before the grind stance commits.")]
    [SerializeField] private float grindStanceHoldTime = 0.06f;

    [Tooltip("How quickly a nose/tail grind returns to centered when press conditions are lost.")]
    [SerializeField] private float grindStanceReleaseTime = 0.05f;

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

    [Header("Landing: Visual Response")]
    [Tooltip("How long the procedural ski landing crouch pose lasts.")]
    [SerializeField] private float skiLandingVisualDuration = 0.22f;

    [Tooltip("Fall height where ski landing visual response starts.")]
    [SerializeField] private float skiLandingVisualMinFallHeight = 0.35f;

    [Tooltip("Fall height where ski landing visual response reaches full intensity.")]
    [SerializeField] private float skiLandingVisualMaxFallHeight = 6f;

    [Tooltip("Downward speed where ski landing visual response starts.")]
    [SerializeField] private float skiLandingVisualMinDownwardSpeed = 1.75f;

    [Tooltip("Downward speed where ski landing visual response reaches full intensity.")]
    [SerializeField] private float skiLandingVisualMaxDownwardSpeed = 15f;

    [Tooltip("Minimum intensity before a ski landing visual response is shown.")]
    [SerializeField, Range(0f, 1f)] private float skiLandingVisualMinVisibleIntensity = 0.05f;

    [Tooltip("Logs ski landing visual trigger diagnostics.")]
    [SerializeField] private bool logSkiLandingVisualDebug = false;

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

    [Header("Stack Recovery")]
    [Tooltip("Minimum time the skier must remain stacked before auto-recovery can begin.")]
    [SerializeField] private float stackMinimumRecoveryDelay = 0.65f;

    [Tooltip("If enabled, both skis must be grounded/aligned before recovering from a stack.")]
    [SerializeField] private bool requireBothSkisForStackRecovery = true;

    [Tooltip("Minimum ski base-alignment required before that ski can contribute to stack recovery.")]
    [SerializeField] private float stackRecoveryMinSkiBaseAlignment = 0.35f;

    [Header("Stack Impact")]
    [Tooltip("Non-ground obstacle layers that can trigger an impact stack/wipeout.")]
    [SerializeField] private LayerMask stackImpactLayers = ~0;

    [Tooltip("If enabled, obstacle-impact stacking ignores colliders that are also in groundLayers.")]
    [SerializeField] private bool ignoreGroundLayerForImpactStack = true;

    [Tooltip("Minimum planar speed required before obstacle collisions can trigger a stack.")]
    [SerializeField] private float obstacleImpactMinPlanarSpeed = 4.5f;

    [Tooltip("Minimum speed into the obstacle normal required before obstacle collisions trigger a stack.")]
    [SerializeField] private float obstacleImpactMinIntoSpeed = 2.75f;

    [Tooltip("Into-obstacle speed that maps to full stack severity.")]
    [SerializeField] private float obstacleImpactFullSpeed = 13f;

    [Tooltip("Fraction of tangential slide retained after an obstacle-triggered stack.")]
    [SerializeField, Range(0f, 1f)] private float stackImpactSlideRetention = 0.35f;

    [Tooltip("Low bounce applied away from the obstacle during an impact-triggered stack.")]
    [SerializeField, Range(0f, 0.5f)] private float stackImpactBounce = 0.08f;

    [Tooltip("Maximum allowed upward velocity after an impact-triggered stack.")]
    [SerializeField] private float stackImpactMaxUpwardVelocity = 1.25f;

    [Header("Stacked Impact Response")]
    [Tooltip("Minimum collision relative speed while already stacked before a secondary flail/impact event is fired.")]
    [SerializeField] private float stackedSecondaryImpactMinSpeed = 3.0f;

    [Tooltip("Cooldown between secondary stacked impact events.")]
    [SerializeField] private float stackedSecondaryImpactCooldown = 0.12f;

    [Tooltip("Angular damping applied while stacked to reduce ski-wheel pinwheeling down slopes.")]
    [SerializeField] private float stackedAngularDamping = 1.8f;

    [Tooltip("Extra damping applied to sideways spin while stacked and skis are contacting snow.")]
    [SerializeField] private float stackedSkiWheelDamping = 3.0f;

    [Tooltip("Gravity multiplier while stacked. Keep at 1 for physically normal gravity.")]
    [SerializeField] private float stackedGravityScale = 1.0f;

    [Tooltip("Linear damping while stacked and grounded. This helps stacked players settle instead of sliding/bouncing forever.")]
    [SerializeField] private float stackedGroundLinearDamping = 0.85f;

    [Tooltip("Linear damping while stacked and airborne. Keep low so the player still falls naturally.")]
    [SerializeField] private float stackedAirLinearDamping = 0.02f;

    [Tooltip("Damps upward bounce along the current ground normal while stacked and grounded.")]
    [SerializeField] private float stackedGroundNormalBounceDamping = 8.0f;

    [Tooltip("Maximum upward speed allowed along the ground normal while stacked and grounded.")]
    [SerializeField] private float stackedMaxGroundUpwardSpeed = 0.45f;

    private enum StackEquipmentColliderMode
    {
        None,
        MakeTrigger,
        Disable
    }

    [Header("Stack Equipment Collision")]
    [Tooltip("How ski/pole equipment colliders behave while stacked. Use MakeTrigger first. Disable is safer for problematic mesh colliders.")]
    [SerializeField] private StackEquipmentColliderMode stackEquipmentColliderMode = StackEquipmentColliderMode.MakeTrigger;

    [Tooltip("If enabled, pole colliders are also suppressed while stacked. This prevents pole tips from injecting bounce into the player Rigidbody.")]
    [SerializeField] private bool includePoleCollidersInStackCollisionSuppression = true;

    [Tooltip("Non-convex MeshColliders cannot reliably become triggers. If enabled, those colliders are disabled while stacked instead.")]
    [SerializeField] private bool disableNonConvexMeshCollidersInsteadOfTrigger = true;

    private struct StackEquipmentColliderState
    {
        public bool enabled;
        public bool isTrigger;

        public StackEquipmentColliderState(Collider collider)
        {
            enabled = collider != null && collider.enabled;
            isTrigger = collider != null && collider.isTrigger;
        }
    }

    private readonly Dictionary<Collider, StackEquipmentColliderState> _stackEquipmentColliderStates = new Dictionary<Collider, StackEquipmentColliderState>();
    private bool _stackEquipmentCollidersSuppressed;

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

    [Header("Stack Recovery Gizmo")]
    [Tooltip("Draws a red/green local-down landing line while selected. Green means the current stack recovery conditions would pass.")]
    [SerializeField] private bool drawStackRecoveryGizmo = true;

    [Tooltip("Length of the stack recovery local-down gizmo ray.")]
    [SerializeField] private float stackRecoveryGizmoLength = 1.35f;

    [Tooltip("Extra ground probe distance used by the recovery gizmo when the player is not currently grounded.")]
    [SerializeField] private float stackRecoveryGizmoGroundProbeDistance = 2.5f;

    [SerializeField] private Color stackRecoveryGizmoRecoverColor = new Color(0.1f, 1f, 0.25f, 1f);
    [SerializeField] private Color stackRecoveryGizmoContinueColor = new Color(1f, 0.1f, 0.05f, 1f);

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

    private float _lastSkiLandingVisualTime = -999f;
    private float _lastSkiLandingVisualIntensity01;

    private string _dbgLastStackReason = "";

    // ----------------------------------------------------------------------
    // PROPERTIES / FLAGS
    // ----------------------------------------------------------------------

    public SkiContact LeftSkiContactRef => leftSkiContact;
    public SkiContact RightSkiContactRef => rightSkiContact;

    private bool HasLegInputs =>
        HasExternalInputSource ||
        (leftSkiAction != null && leftSkiAction.action != null &&
         rightSkiAction != null && rightSkiAction.action != null);

    private bool HasLeanInput =>
        HasExternalInputSource ||
        (leanAction != null && leanAction.action != null);

    private bool HasPolesInput =>
        HasExternalInputSource ||
        (polesAction != null && polesAction.action != null);

    private bool HasJumpInput =>
        HasExternalInputSource ||
        (jumpAction != null && jumpAction.action != null);

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
    public event System.Action<StackEventInfo> OnStackImpact;

    public readonly struct StackRecoveryEventInfo
    {
        public readonly Vector3 position;
        public readonly Vector3 forward;
        public readonly bool wasGrounded;

        public StackRecoveryEventInfo(Vector3 position, Vector3 forward, bool wasGrounded)
        {
            this.position = position;
            this.forward = forward;
            this.wasGrounded = wasGrounded;
        }
    }

    public event System.Action<StackRecoveryEventInfo> OnRecoveredFromStack;

    // Stroke progress 0..1 within the CURRENT phase (Entry / FollowThrough).
    public float PoleStrokeT => _poleStrokeT;

    // Current stroke phase (Idle, Entry, Drag, FollowThrough).
    public PoleStrokePhase CurrentPolePhase => _polePhase;

    // Is the pole input button currently held?
    public bool IsPoleInputHeld => _rawPolesPressed;

    public Vector3 GroundNormal => _groundNormal;
    public Vector3 Velocity => _rb.linearVelocity;
    public Vector3 SkiForwardOnPlane => _skiForward;
    public bool IsRiderGrounded => IsGroundedForControls;
    public bool IsPhysicsGrounded => _isGrounded;
    public SkiEndSlideState CurrentSkiEndSlideState => _skiEndSlideState;
    public float CurrentSkiEndSlide01 => _skiEndSlide01;

    public bool IsExplicitNoseTailSlide =>
        _skiEndSlideState != SkiEndSlideState.None &&
        _skiEndSlideState != SkiEndSlideState.Mixed;

    public int CurrentSkiEndSlideSign
    {
        get
        {
            switch (_skiEndSlideState)
            {
                case SkiEndSlideState.LeftNose:
                case SkiEndSlideState.RightNose:
                case SkiEndSlideState.BothNose:
                    return 1;

                case SkiEndSlideState.LeftTail:
                case SkiEndSlideState.RightTail:
                case SkiEndSlideState.BothTail:
                    return -1;

                default:
                    return 0;
            }
        }
    }

    public float RawLeanInput => _rawLeanInput;
    public float ForwardLeanInput => _forwardLean;
    public float LeftLegInput => _rawLeftLegInput;
    public float RightLegInput => _rawRightLegInput;
    public bool IsAirborne => !IsGroundedForControls && !_grindActive && !_stacked;

    public bool IsGrinding => _grindActive;
    public float GrindStrength01 => Mathf.Clamp01(_grindStrengthSmoothed);
    public float CurrentGrindDistance => _grindDistance;
    public int CurrentGrindEndContactSign => _grindDominantEndSign;
    public int CurrentRawGrindEndContactSign => _grindRawEndSign;
    public GrindStance CurrentGrindStance => _grindStance;
    public string CurrentGrindSourceName => _dbgGrindSource ?? string.Empty;
    public GrindSurfaceKind CurrentGrindSurfaceKind => _grindSurfaceKind;
    public GrindStateInfo CurrentGrindState => new GrindStateInfo(
        _grindActive,
        _grindDistance,
        _grindTime,
        Mathf.Clamp01(_grindStrengthSmoothed),
        _grindDominantEndSign,
        _grindStance,
        _grindSurfaceKind,
        _dbgGrindSource ?? string.Empty,
        _grindTangent,
        _grindNormal);

    public bool IsTucking => _tuck01 > 0.5f;
    public float Tuck01 => _tuck01;

    public bool IsSkiJumpCharging => _jumpQueued && _jumpHeld && !_jumpReleaseQueued && !_stacked;

    public float SkiJumpCharge01
    {
        get
        {
            if (!IsSkiJumpCharging)
                return 0f;

            return maxJumpChargeTime > 0f
                ? Mathf.Clamp01((Time.time - _lastJumpPressedTime) / maxJumpChargeTime)
                : 1f;
        }
    }

    public float SkiJumpReleaseVisual01
    {
        get
        {
            const float visualDuration = 0.18f;
            float age = Time.time - _lastJumpTime;
            if (age < 0f || age > visualDuration)
                return 0f;

            return Mathf.Clamp01(1f - age / visualDuration) * Mathf.Clamp01(_lastSkiJumpCharge01);
        }
    }

    public float SkiLandingVisualIntensity01 => _lastSkiLandingVisualIntensity01;

    public float SkiLandingPose01
    {
        get
        {
            float duration = Mathf.Max(0.01f, skiLandingVisualDuration);
            float age = Time.time - _lastSkiLandingVisualTime;

            if (age < 0f || age > duration)
                return 0f;

            float t = Mathf.Clamp01(age / duration);
            float decay = 1f - Mathf.SmoothStep(0f, 1f, t);

            return Mathf.Clamp01(_lastSkiLandingVisualIntensity01 * decay);
        }
    }
    public bool IsAirStyleActive => _airStyle01 > 0.5f;

    public AerialStyleMode CurrentAerialStyleMode => ResolveAerialStyleMode();

    public float AirStyle01 => _airStyle01;

    public bool IsAirPoseActive => _airStyle01 > 0.5f;
    public bool IsPoseButtonHeld => _rawPosePressed;
    public bool IsAuthoredPoseAirborne => IsAirborne;

    public AerialPoseFamily CurrentPoseFamily => ResolvePoseFamily();

    public AerialPoseShape CurrentPoseShape => ResolvePoseShape();

    public AerialOrientationModifier CurrentPoseOrientationModifier => ResolvePoseOrientationModifier();
    public TrickPoseVerticalOrientationRequirement CurrentPoseVerticalOrientation => ResolvePoseVerticalOrientation();
    public TrickPoseHorizontalOrientationRequirement CurrentPoseHorizontalOrientation => ResolvePoseHorizontalOrientation();
    public TrickPoseTravelFacingRequirement CurrentPoseTravelFacing => ResolvePoseTravelFacing();

    public TrickPoseMotionStateRequirement CurrentPoseMotionState => ResolvePoseMotionState();

    public string CurrentPoseName => ResolvePoseName();
    public string CurrentTrackedPoseName => ResolveCommittedTrackedPoseName();
    public string CurrentPresentedPoseName => BuildPoseDescriptorLabel(CurrentTrackedPoseName, CurrentPoseOrientationModifier);
    public AerialPoseFamily CurrentCommittedPoseFamily => ResolveCommittedPoseFamily();
    public AerialPoseShape CurrentCommittedPoseShape => ResolveCommittedPoseShape();
    public float CurrentYawAngularVelocity => _airAngularVelocity.y;
    public float CurrentPitchAngularVelocity => _airAngularVelocity.x;
    public float CurrentRollAngularVelocity => _airAngularVelocity.z;
    public float CurrentTotalAngularSpeed => _airAngularVelocity.magnitude;
    public int CurrentSpinDirectionSign => CurrentYawAngularVelocity > 1f ? 1 : (CurrentYawAngularVelocity < -1f ? -1 : 0);
    public int CurrentFlipDirectionSign => CurrentPitchAngularVelocity > 1f ? 1 : (CurrentPitchAngularVelocity < -1f ? -1 : 0);
    public bool HasTrickPoseEntrySnapshot => _trickPoseIntentSnapshot.valid;
    public AerialPoseFamily EntryPoseFamily => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.family : AerialPoseFamily.None;
    public AerialPoseShape EntryPoseShape => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.shape : AerialPoseShape.None;
    public AerialOrientationModifier EntryPoseOrientationModifier => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.orientation : AerialOrientationModifier.None;
    public TrickPoseVerticalOrientationRequirement EntryPoseVerticalOrientation => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.verticalOrientation : TrickPoseVerticalOrientationRequirement.Any;
    public TrickPoseHorizontalOrientationRequirement EntryPoseHorizontalOrientation => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.horizontalOrientation : TrickPoseHorizontalOrientationRequirement.Any;
    public TrickPoseTravelFacingRequirement EntryPoseTravelFacing => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.travelFacing : TrickPoseTravelFacingRequirement.Any;

    public TrickPoseMotionStateRequirement EntryPoseMotionState => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.motionState : TrickPoseMotionStateRequirement.Any;
    public string EntryPoseName => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.poseName : string.Empty;
    public Vector3 EntryEulerAngles => _trickPoseIntentSnapshot.valid ? _trickPoseIntentSnapshot.entryEulerAngles : CurrentSignedEulerAngles;
    public TrickPoseEntry ActiveTrickPoseEntry => _activeTrickPoseEntry;
    public float ActiveTrickPoseBlend => _activeTrickPoseBlend;
    public TrickPoseEntry BestCompetingTrickPoseEntry => _bestCompetingTrickPoseEntry;
    public string CurrentTrickPoseDecisionReason => _lastTrickPoseDecisionReason;
    public TrickPoseProfileSO TrickPoseProfile => trickPoseProfile;
    public Transform BodyPoseTransform => GetBodyPoseTransform();
    public Transform HeadPoseTransform => headAnchorTransform;
    public Transform LeftSkiTransform => leftSki;
    public Transform RightSkiTransform => rightSki;

    /// <summary>
    /// Current runtime left ski visual. This follows gear swaps, so stack visuals do not keep targeting
    /// the destroyed/default ski model.
    /// </summary>
    public Transform LeftSkiVisualTransform => GetCurrentSkiVisualTransform(true);

    /// <summary>
    /// Current runtime right ski visual. This follows gear swaps, so stack visuals do not keep targeting
    /// the destroyed/default ski model.
    /// </summary>
    public Transform RightSkiVisualTransform => GetCurrentSkiVisualTransform(false);

    private Transform GetCurrentSkiVisualTransform(bool left)
    {
        Transform runtimeVisual = left ? _leftSkiVisual : _rightSkiVisual;
        if (runtimeVisual != null)
            return runtimeVisual;

        Transform assignedDefaultVisual = left ? leftSkiVisual : rightSkiVisual;
        if (assignedDefaultVisual != null)
            return assignedDefaultVisual;

        Transform skiRoot = left ? leftSki : rightSki;
        if (skiRoot == null)
            return null;

        // Conservative fallback: only used if inspector/runtime refs are missing.
        // Prefer the first child with renderers, rather than the logical ski root itself.
        for (int i = 0; i < skiRoot.childCount; i++)
        {
            Transform child = skiRoot.GetChild(i);
            if (child != null && child.GetComponentInChildren<Renderer>(true) != null)
                return child;
        }

        return null;
    }

    private Vector3 CurrentSignedEulerAngles => NormalizeEulerAngles(transform.rotation.eulerAngles);

    public float RawLeftLegInput => _rawLeftLegInput;

    public float RawRightLegInput => _rawRightLegInput;

    private bool HasPendingJumpIntent =>
    _jumpQueued || _jumpHeld || _jumpReleaseQueued;

    private bool IsRecentlyJumpedForEndRecovery()
    {
        float suppressTime = Mathf.Max(
            jumpStickSuppressionTime,
            endContactFlattenRecoveryPostJumpSuppressTime);

        return (Time.time - _lastJumpTime) <= suppressTime;
    }

    private void CancelEndContactFlattenRecovery()
    {
        _endContactFlattenRecoveryUntil = -999f;
        _endContactFlattenRecoveryStartedAt = -999f;
        _endContactFlattenRecoveryNormal = Vector3.up;
        _endContactFlattenRecoverySign = 0;
        _lastEndContactFlattenRecoveryProbeDistance = float.PositiveInfinity;
    }

    private bool TryGetEndContactFlattenRecoveryCandidate(
        out Vector3 normal,
        out float bestDistance,
        out int endSign)
    {
        normal = Vector3.zero;
        bestDistance = float.PositiveInfinity;
        endSign = 0;

        bool found = false;

        ConsiderEndContactFlattenRecoveryCandidate(leftSkiContact, ref normal, ref bestDistance, ref endSign, ref found);
        ConsiderEndContactFlattenRecoveryCandidate(rightSkiContact, ref normal, ref bestDistance, ref endSign, ref found);

        if (!found)
        {
            normal = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
            return false;
        }

        normal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : (_groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up);

        return true;
    }

    private void ConsiderEndContactFlattenRecoveryCandidate(
        SkiContact contact,
        ref Vector3 normal,
        ref float bestDistance,
        ref int endSign,
        ref bool found)
    {
        if (contact == null || !contact.HasEndContact || contact.EndContactSign == 0)
            return;

        float d = float.PositiveInfinity;

        if (contact.EndContactSign > 0 && contact.ProbeTipHit)
            d = contact.ProbeTipDistance;
        else if (contact.EndContactSign < 0 && contact.ProbeTailHit)
            d = contact.ProbeTailDistance;

        // Fallback for mixed/noisy frames where the sign is valid but the signed probe
        // was not the shortest valid probe in the current frame.
        if (float.IsInfinity(d))
        {
            if (contact.ProbeTipHit)
                d = Mathf.Min(d, contact.ProbeTipDistance);
            if (contact.ProbeTailHit)
                d = Mathf.Min(d, contact.ProbeTailDistance);
        }

        if (float.IsInfinity(d))
            return;

        if (d >= bestDistance)
            return;

        Vector3 n = contact.ContactNormal.sqrMagnitude > 0.0001f
            ? contact.ContactNormal.normalized
            : (_groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up);

        bestDistance = d;
        normal = n;
        endSign = contact.EndContactSign;
        found = true;
    }

    private bool CanStartEndContactFlattenRecovery(Vector3 contactNormal)
    {
        if (!enableEndContactFlattenRecovery || _rb == null || _stacked || _grindActive)
            return false;

        if (HasAnySkiCollisionContact || _hasNonSkiGroundContact)
            return false;

        if (HasPendingJumpIntent || IsRecentlyJumpedForEndRecovery())
            return false;

        float airTime = Time.time - _airborneStartTime;
        if (airTime < Mathf.Max(0f, endContactFlattenRecoveryMinAirTime))
            return false;

        if (_lastBodyProbeHit &&
            _lastBodyProbeGap > Mathf.Max(0f, endContactFlattenRecoveryMaxBodyGap))
            return false;

        if (!TryGetEndContactFlattenRecoveryCandidate(out Vector3 n, out float bestDistance, out _))
            return false;

        _lastEndContactFlattenRecoveryProbeDistance = bestDistance;

        if (bestDistance > Mathf.Max(0.01f, endContactFlattenRecoveryMaxEndProbeDistance))
            return false;

        if (contactNormal.sqrMagnitude > 0.0001f)
            n = contactNormal.normalized;

        float normalSpeed = Vector3.Dot(_rb.linearVelocity, n);

        // Positive normal speed means the player is moving away from the surface.
        // That is jump/takeoff behaviour, not landing recovery.
        if (normalSpeed > endContactFlattenRecoveryMaxRisingSpeed)
            return false;

        return true;
    }

    private void ValidateEndContactFlattenRecoveryState()
    {
        if (_endContactFlattenRecoveryStartedAt < 0f)
            return;

        if (!enableEndContactFlattenRecovery ||
            _stacked ||
            _grindActive ||
            IsRecentlyJumpedForEndRecovery() ||
            HasAnySkiCollisionContact ||
            _hasNonSkiGroundContact ||
            Time.time > _endContactFlattenRecoveryUntil)
        {
            CancelEndContactFlattenRecovery();
            return;
        }

        if (_lastBodyProbeHit &&
            _lastBodyProbeGap > Mathf.Max(0f, endContactFlattenRecoveryMaxBodyGap))
        {
            CancelEndContactFlattenRecovery();
            return;
        }

        if (!TryGetEndContactFlattenRecoveryCandidate(out Vector3 n, out float bestDistance, out int sign))
        {
            CancelEndContactFlattenRecovery();
            return;
        }

        _lastEndContactFlattenRecoveryProbeDistance = bestDistance;

        if (bestDistance > Mathf.Max(0.01f, endContactFlattenRecoveryMaxEndProbeDistance))
        {
            CancelEndContactFlattenRecovery();
            return;
        }

        _endContactFlattenRecoveryNormal = n;
        _endContactFlattenRecoverySign = sign;
    }

    private bool IsEndContactFlattenRecoveryActive
    {
        get
        {
            if (!enableEndContactFlattenRecovery)
                return false;

            if (_stacked || _grindActive)
                return false;

            if (IsRecentlyJumpedForEndRecovery())
                return false;

            if (HasAnySkiCollisionContact || _hasNonSkiGroundContact)
                return false;

            return _endContactFlattenRecoveryStartedAt > 0f &&
                   Time.time <= _endContactFlattenRecoveryUntil;
        }
    }

    /// <summary>
    /// Grounded test used for *controls* (when to use skiing vs air inputs).
    /// This is intentionally more forgiving than the strict physics grounding:
    /// - Strict physics grounding or active grinding counts as grounded.
    /// - A short coyote window after losing ground keeps you in ski controls
    ///   so tiny gaps / tip chatter don't instantly flip you into air mode.
    /// </summary>
    private bool IsGroundedForControls
    {
        get
        {
            if (_stacked)
                return false;

            // If we are actively grinding, we want the same control path as grounded skiing.
            if (_grindActive)
                return true;

            if (_skiEndSlideState != SkiEndSlideState.None && HasAnySkiEndGroundContact)
                return true;

            // End-contact flatten recovery is corrective only.
            // It must not grant full ground/skate/pole force authority.
            if (!endContactFlattenRecoveryCorrectiveOnly && IsEndContactFlattenRecoveryActive)
                return true;

            // Physics-based grounding from our casts.
            if (_isGrounded)
                return true;

            // If we're scraping a wall while airborne, do NOT treat this as grounded for controls.
            // Grounded/ski-plausible contact wins above, so uneven snow cannot be demoted by a wall-ish ski contact.
            if (Time.time < _wallContactUntil)
                return false;

            // Soft "control coyote": keep ski controls briefly after leaving ground.
            return (Time.time - _lastGroundedTime) <= controlCoyoteTime;
        }
    }

    private struct SkiGroundFallbackResult
    {
        public bool found;
        public Vector3 point;
        public Vector3 normal;
        public float gap;
        public Collider collider;
        public bool isEndContact;
        public int endSign;
        public string source;
        public float hitDistance;
        public bool freshHit;
    }

    private static string DescribeColliderBrief(Collider c)
    {
        if (c == null)
            return "(none)";

        int layer = c.gameObject.layer;
        string layerName = LayerMask.LayerToName(layer);
        return $"{c.name}/{(string.IsNullOrEmpty(layerName) ? layer.ToString() : layerName)}";
    }

    private static string DescribeSkiProbeState(SkiContact contact)
    {
        if (contact == null)
            return "missing";

        return $"stable={contact.HasStableSupportContact} end={contact.HasEndContact}:{contact.EndContactSign} " +
               $"baseSupport={contact.HasBaseSupportContact} " +
               $"base={contact.ProbeBaseHit}:{DescribeColliderBrief(contact.ProbeBaseCollider)} " +
               $"baseDist={contact.ProbeBaseDistance:0.000} " +
               $"tip={contact.ProbeTipHit}:{DescribeColliderBrief(contact.ProbeTipCollider)} " +
               $"tipDist={contact.ProbeTipDistance:0.000} " +
               $"tail={contact.ProbeTailHit}:{DescribeColliderBrief(contact.ProbeTailCollider)} " +
               $"tailDist={contact.ProbeTailDistance:0.000} " +
               $"collisionEnd={contact.HasCollisionEndContact}:{contact.CollisionEndSign}:{DescribeColliderBrief(contact.CollisionEndCollider)} " +
               $"lastAge={(contact.LastContactTime > 0f ? Time.time - contact.LastContactTime : float.PositiveInfinity):0.000}s";
    }

    [ContextMenu("Print Grounding Debug State")]
    public void PrintGroundingDebugState()
    {
        Debug.Log(
            $"[SkiController] Grounding Debug: {name}\n" +
            $"mode={_movementMode} stacked={_stacked} grind={_grindActive} physicsGrounded={_isGrounded} controlsGrounded={IsGroundedForControls} airborne={IsAirborne}\n" +
            $"groundedFrom={_lastGroundedFrom} leftCollisionContact={(leftSkiContact != null && leftSkiContact.HasCollisionContact)} rightCollisionContact={(rightSkiContact != null && rightSkiContact.HasCollisionContact)} bodyCollisionContact={_hasNonSkiGroundContact} bodyCollisionAge={(Time.time - _lastNonSkiGroundContactTime):0.000}s collisionCoyoteAge={(Time.time - _lastRealSupportTime):0.000}s explicitSlide={_skiEndSlideState != SkiEndSlideState.None} endFlattenRecovery={IsEndContactFlattenRecoveryActive} endFlattenRemaining={Mathf.Max(0f, _endContactFlattenRecoveryUntil - Time.time):0.000}s endFlattenProbeDist={_lastEndContactFlattenRecoveryProbeDistance:0.000}\n" +
            $"groundForceRanLastFixed={_dbgGroundForceRanLastFixed} skateRanLastFixed={_dbgSkateRanLastFixed} poleRanLastFixed={_dbgPoleRanLastFixed} poseInputActive={_dbgPoseInputActive} probeOnlyAffectsControl=False\n" +
            $"bodyNearGround={_bodyNearGround} skiPlausible={_skiContactPlausibleForBody} nearGroundForJump={_nearGroundForJump} groundGap={_lastMeasuredGroundGap:0.000} refresh={_lastGroundRefreshSource}\n" +
            $"bodyProbeHit={_lastBodyProbeHit} bodyProbeDistance={_lastBodyProbeDistance:0.000} bodyProbeGap={_lastBodyProbeGap:0.000} bodyProbeAllowedGap={_lastBodyProbeAllowedGap:0.000} bodyProbeAcceptedButTooFar={_lastBodyProbeAcceptedButTooFar}\n" +
            $"probeOnlyGrounded={_probeOnlyGrounded} probeOnlyEndGrounded={_probeOnlyEndGrounded} probeOnlyGap={_probeOnlyGroundGap:0.000} probeOnlySource={_probeOnlyGroundSource} hasStableSkiSupport={HasRealSkiGroundContact()} baseProbe={HasAnyBaseSkiProbeContact()} hasEndContact={HasAnySkiEndGroundContact}\n" +
            $"groundProbe accepted={DescribeColliderBrief(_lastGroundProbeAcceptedCollider)} acceptedDistance={_lastGroundProbeAcceptedDistance:0.000} acceptedNormal={_lastGroundProbeAcceptedNormal.ToString("F3")} ignored={DescribeColliderBrief(_lastGroundProbeIgnoredCollider)} ignoredReason={_lastGroundProbeIgnoredReason}\n" +
            $"skiFallback enabled={useSkiTransformGroundFallback} freshHit={_lastSkiFallbackFreshHit} source={_lastSkiFallbackSource} freshAge={(Time.time - _lastSkiFallbackFreshTime):0.000}s lastAge={(Time.time - _lastSkiTransformFallbackTime):0.000}s lastGap={_lastSkiTransformFallbackGap:0.000} lastNormal={_lastSkiTransformFallbackNormal.ToString("F3")}\n" +
            $"noseTailLandingSource={_lastNoseTailLandingSource} leftCollisionEnd={(leftSkiContact != null && leftSkiContact.HasCollisionEndContact)} leftCollisionEndSign={(leftSkiContact != null ? leftSkiContact.CollisionEndSign : 0)} leftCollisionEndCollider={DescribeColliderBrief(leftSkiContact != null ? leftSkiContact.CollisionEndCollider : null)} rightCollisionEnd={(rightSkiContact != null && rightSkiContact.HasCollisionEndContact)} rightCollisionEndSign={(rightSkiContact != null ? rightSkiContact.CollisionEndSign : 0)} rightCollisionEndCollider={DescribeColliderBrief(rightSkiContact != null ? rightSkiContact.CollisionEndCollider : null)}\n" +
            $"hasSkiContact={HasAnySkiContact} hasSkiCollision={HasAnySkiCollisionContact} hasSkiWall={HasAnySkiWallContact} wallSuppressRemaining={Mathf.Max(0f, _wallContactUntil - Time.time):0.000}s wallNormal={_wallScrapeNormal}\n" +
            $"leftGrounded={(leftSkiContact != null && leftSkiContact.IsGrounded)} leftCollision={(leftSkiContact != null && leftSkiContact.HasCollisionContact)} leftWall={(leftSkiContact != null && leftSkiContact.HasWallContact)} leftNormal={(leftSkiContact != null ? leftSkiContact.ContactNormal.ToString("F3") : "none")} leftWallNormal={(leftSkiContact != null ? leftSkiContact.WallContactNormal.ToString("F3") : "none")}\n" +
            $"leftProbes={DescribeSkiProbeState(leftSkiContact)}\n" +
            $"rightGrounded={(rightSkiContact != null && rightSkiContact.IsGrounded)} rightCollision={(rightSkiContact != null && rightSkiContact.HasCollisionContact)} rightWall={(rightSkiContact != null && rightSkiContact.HasWallContact)} rightNormal={(rightSkiContact != null ? rightSkiContact.ContactNormal.ToString("F3") : "none")} rightWallNormal={(rightSkiContact != null ? rightSkiContact.WallContactNormal.ToString("F3") : "none")}\n" +
            $"rightProbes={DescribeSkiProbeState(rightSkiContact)}",
            this);
    }

    [ContextMenu("Print Down Raycast Ground Debug")]
    public void PrintDownRaycastGroundDebug()
    {
        Vector3 origin = (_rb != null ? _rb.position : transform.position) + Vector3.up * 3f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHitBuffer, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (count <= 0)
        {
            Debug.Log($"[SkiController] Down Raycast Ground Debug: {name}\nno hit from origin={origin}", this);
            return;
        }

        RaycastHit nearestRaw = default;
        float nearestRawDistance = float.PositiveInfinity;
        RaycastHit nearestAccepted = default;
        float nearestAcceptedDistance = float.PositiveInfinity;
        System.Text.StringBuilder rejected = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _groundHitBuffer[i];
            if (hit.collider == null)
                continue;

            if (hit.distance < nearestRawDistance)
            {
                nearestRaw = hit;
                nearestRawDistance = hit.distance;
            }

            string reason = GetGroundHitRejectReason(hit.collider, hit.normal);
            if (reason == "accepted")
            {
                if (hit.distance < nearestAcceptedDistance)
                {
                    nearestAccepted = hit;
                    nearestAcceptedDistance = hit.distance;
                }
            }
            else
            {
                rejected.AppendLine($"rejected={DescribeColliderBrief(hit.collider)} distance={hit.distance:0.000} normal={hit.normal.ToString("F3")} reason={reason}");
            }
        }

        if (nearestRaw.collider == null)
        {
            Debug.Log($"[SkiController] Down Raycast Ground Debug: {name}\nno valid collider in {count} hits from origin={origin}", this);
            return;
        }

        string acceptedLine = nearestAccepted.collider != null
            ? $"nearestAccepted={DescribeColliderBrief(nearestAccepted.collider)} distance={nearestAccepted.distance:0.000} point={nearestAccepted.point.ToString("F3")} normal={nearestAccepted.normal.ToString("F3")}"
            : "nearestAccepted=(none)";

        Debug.Log(
            $"[SkiController] Down Raycast Ground Debug: {name}\n" +
            $"origin={origin} hitCount={count}\n" +
            $"nearestRaw={DescribeColliderBrief(nearestRaw.collider)} distance={nearestRaw.distance:0.000} point={nearestRaw.point.ToString("F3")} normal={nearestRaw.normal.ToString("F3")} rejectReason={GetGroundHitRejectReason(nearestRaw.collider, nearestRaw.normal)}\n" +
            $"{acceptedLine}\n" +
            rejected.ToString(),
            this);
    }

    // Expose pole contacts for audio / VFX.
    public PoleContact LeftPoleContact => leftPoleContact;
    public PoleContact RightPoleContact => rightPoleContact;

    public void SetStackSkiVisualOverride(bool left, Vector3 worldPosition, Quaternion worldRotation, float weight)
    {
        weight = Mathf.Clamp01(weight);

        // Stack ski overrides are transform-driven visual motion.
        // If the ski/pole colliders are still solid, these visual transforms can inject physics impulses
        // into the player Rigidbody. Suppress them defensively any time a stack override becomes active.
        if (weight > 0.001f && _stacked && !_stackEquipmentCollidersSuppressed)
            SetStackEquipmentCollidersSuppressed(true);

        if (left)
        {
            _leftStackSkiVisualOverrideActive = weight > 0.001f;
            _leftStackSkiVisualOverrideWorldPos = worldPosition;
            _leftStackSkiVisualOverrideWorldRot = worldRotation;
            _leftStackSkiVisualOverrideWeight = weight;
        }
        else
        {
            _rightStackSkiVisualOverrideActive = weight > 0.001f;
            _rightStackSkiVisualOverrideWorldPos = worldPosition;
            _rightStackSkiVisualOverrideWorldRot = worldRotation;
            _rightStackSkiVisualOverrideWeight = weight;
        }
    }

    public void ClearStackSkiVisualOverride(bool left)
    {
        if (left)
        {
            _leftStackSkiVisualOverrideActive = false;
            _leftStackSkiVisualOverrideWeight = 0f;
        }
        else
        {
            _rightStackSkiVisualOverrideActive = false;
            _rightStackSkiVisualOverrideWeight = 0f;
        }
    }

    public void ClearAllStackSkiVisualOverrides()
    {
        ClearStackSkiVisualOverride(true);
        ClearStackSkiVisualOverride(false);
    }

    public void ResetVisualPoseForWalkModeHandoff()
    {
        // Clear visual-only state that can leave the body compressed when the SkiController
        // is disabled immediately after a stack.
        _forwardLean = 0f;
        _sideLean = 0f;
        _leftOut = 0f;
        _rightOut = 0f;
        _tuck01 = 0f;
        _airStyle01 = 0f;

        _activeTrickPoseEntry = null;
        _activeTrickPoseBlend = 0f;
        _activeTrickPoseScore = 0f;
        _activeTrickPoseCommittedAt = -999f;
        _activeTrickPoseLastValidTime = -999f;

        ClearAllStackSkiVisualOverrides();

        Transform bodyPoseTransform = GetBodyPoseTransform();
        if (bodyPoseTransform != null)
        {
            bool usesBodyMotion = bodyPoseTransform == bodyMotionTransform;

            bodyPoseTransform.localPosition = usesBodyMotion
                ? _bodyMotionBaseLocalPos
                : _bodyBaseLocalPos;

            bodyPoseTransform.localRotation = usesBodyMotion
                ? _bodyMotionBaseLocalRot
                : _bodyBaseLocalRot;
        }

        // If both bodyTransform and bodyMotionTransform exist, reset both. The active pose transform
        // may be one of them, but stale compression on the other can still affect child anchors.
        if (bodyTransform != null && bodyTransform != transform && bodyTransform != bodyPoseTransform)
        {
            bodyTransform.localPosition = _bodyBaseLocalPos;
            bodyTransform.localRotation = _bodyBaseLocalRot;
            bodyTransform.localScale = _bodyBaseLocalScale;
        }

        if (bodyMotionTransform != null && bodyMotionTransform != transform && bodyMotionTransform != bodyPoseTransform)
        {
            bodyMotionTransform.localPosition = _bodyMotionBaseLocalPos;
            bodyMotionTransform.localRotation = _bodyMotionBaseLocalRot;
        }

        if (bodyScaleTransform != null)
            bodyScaleTransform.localScale = _bodyScaleBaseLocalScale;

        if (headAnchorTransform != null)
        {
            headAnchorTransform.localPosition = _headAnchorBaseLocalPos;
            headAnchorTransform.localRotation = _headAnchorBaseLocalRot;
        }

        if (leftPoleContact != null)
        {
            leftPoleContact.ClearAuthoredPoseOverride();
            leftPoleContact.ClearStackVisualOverride();
        }

        if (rightPoleContact != null)
        {
            rightPoleContact.ClearAuthoredPoseOverride();
            rightPoleContact.ClearStackVisualOverride();
        }

        SkierLimbLineVisual limbVisual = GetSkierLimbLineVisual();
        if (limbVisual != null)
            limbVisual.SetRuntimeJointPoseEntry(null, 0f);

        Physics.SyncTransforms();
    }

    public void ReleaseStackSkiVisualOverridesAndSnap(bool snapToNeutralPose)
    {
        ClearAllStackSkiVisualOverrides();

        if (!snapToNeutralPose)
            return;

        _leftSkiOffsetCurrent = Vector3.zero;
        _rightSkiOffsetCurrent = Vector3.zero;
        _leftSkiYawCurrent = 0f;
        _rightSkiYawCurrent = 0f;

        if (leftSki != null)
        {
            leftSki.localPosition = _leftSkiLocalBasePos;
            leftSki.localRotation = _leftSkiLocalBaseRot;
        }

        if (rightSki != null)
        {
            rightSki.localPosition = _rightSkiLocalBasePos;
            rightSki.localRotation = _rightSkiLocalBaseRot;
        }

        if (leftSkiContact != null)
        {
            leftSkiContact.ResetContactState();
            leftSkiContact.ManualSampleGround();
        }

        if (rightSkiContact != null)
        {
            rightSkiContact.ResetContactState();
            rightSkiContact.ManualSampleGround();
        }
    }

    public void ApplyStackSkiVisualOverrideWorldImmediate(
        bool left,
        Vector3 worldPosition,
        Quaternion worldRotation,
        float weight)
    {
        weight = Mathf.Clamp01(weight);
        SetStackSkiVisualOverride(left, worldPosition, worldRotation, weight);

        Transform skiTransform = left ? leftSki : rightSki;
        if (skiTransform == null || weight <= 0.001f)
            return;

        if (weight >= 0.999f)
        {
            skiTransform.SetPositionAndRotation(worldPosition, worldRotation);
            return;
        }

        skiTransform.SetPositionAndRotation(
            Vector3.Lerp(skiTransform.position, worldPosition, weight),
            Quaternion.Slerp(skiTransform.rotation, worldRotation, weight));
    }

    private static void ApplyStackSkiVisualOverride(
     Transform skiTransform,
     ref Vector3 targetLocalPos,
     ref Quaternion targetLocalRot,
     bool active,
     Vector3 worldPosition,
     Quaternion worldRotation,
     float weight)
    {
        if (!active || skiTransform == null || weight <= 0.001f)
            return;

        Transform parent = skiTransform.parent;

        Vector3 overrideLocalPos = parent != null
            ? parent.InverseTransformPoint(worldPosition)
            : worldPosition;

        Quaternion overrideLocalRot = parent != null
            ? Quaternion.Inverse(parent.rotation) * worldRotation
            : worldRotation;

        // The caller now pre-blends the anchor target. Do not double-soften the override here,
        // otherwise skis can visibly separate from the simulated foot endpoint.
        weight = Mathf.Clamp01(weight);
        targetLocalPos = Vector3.Lerp(targetLocalPos, overrideLocalPos, weight);
        targetLocalRot = Quaternion.Slerp(targetLocalRot, overrideLocalRot, weight);
    }

    public void EnsurePoseRigDefaultsCaptured()
    {
        EnsureTrueDefaultRigSnapshotCaptured();
        if (_poseRigDefaultsCaptured)
            return;

        _defaultRigSnapshot = _trueDefaultRigSnapshot?.Clone();
        _poseRigDefaultsCaptured = _defaultRigSnapshot != null;
    }

    public void EnsureTrueDefaultRigSnapshotCaptured()
    {
        if (_truePoseRigDefaultsCaptured)
            return;

        _trueDefaultRigSnapshot = CaptureAuthoredBaseRigSnapshot();
        _truePoseRigDefaultsCaptured = _trueDefaultRigSnapshot != null;
        if (_truePoseRigDefaultsCaptured)
            RestoreBaseCachesFromTrueDefault(_trueDefaultRigSnapshot);
    }

    public void RebuildTrueDefaultPoseFromAuthoredSource(bool restorePose = true)
    {
        _authoredBaseRigSnapshot = null;
        _authoredBaseRigSnapshotCaptured = false;
        _trueDefaultRigSnapshot = CaptureAuthoredBaseRigSnapshot();
        _truePoseRigDefaultsCaptured = _trueDefaultRigSnapshot != null;
        _defaultRigSnapshot = _trueDefaultRigSnapshot?.Clone();
        _poseRigDefaultsCaptured = _defaultRigSnapshot != null;

        if (restorePose)
            RestoreTrueDefaultPose(true);
    }

    [System.Obsolete("Mutable default recapture is disabled. Use RebuildTrueDefaultPoseFromAuthoredSource or RestoreTrueDefaultPose.")]
    public void RecapturePoseRigDefaultsFromCurrent()
    {
        EnsureTrueDefaultRigSnapshotCaptured();
        _defaultRigSnapshot = _trueDefaultRigSnapshot?.Clone();
        _poseRigDefaultsCaptured = _defaultRigSnapshot != null;
    }

    public TrickPoseRigSnapshot CaptureTrueDefaultRigSnapshot()
    {
        EnsureTrueDefaultRigSnapshotCaptured();
        return _trueDefaultRigSnapshot?.Clone();
    }

    public TrickPoseRigSnapshot CaptureCurrentRigSnapshot()
    {
        SkierLimbLineVisual limbVisual = GetSkierLimbLineVisual();
        return new TrickPoseRigSnapshot
        {
            body = TrickPoseRigSnapshot.PartState.FromTransform(GetBodyPoseTransform()),
            head = TrickPoseRigSnapshot.PartState.FromTransform(headAnchorTransform),
            leftSki = TrickPoseRigSnapshot.PartState.FromTransform(leftSki),
            rightSki = TrickPoseRigSnapshot.PartState.FromTransform(rightSki),
            leftPole = leftPoleContact != null ? leftPoleContact.CaptureBasePoseSnapshot() : default,
            rightPole = rightPoleContact != null ? rightPoleContact.CaptureBasePoseSnapshot() : default,
            leftElbow = CaptureJointPartState(limbVisual, SkierLimbJoint.LeftElbow),
            rightElbow = CaptureJointPartState(limbVisual, SkierLimbJoint.RightElbow),
            leftKnee = CaptureJointPartState(limbVisual, SkierLimbJoint.LeftKnee),
            rightKnee = CaptureJointPartState(limbVisual, SkierLimbJoint.RightKnee)
        };
    }

    private TrickPoseRigSnapshot CaptureAuthoredBaseRigSnapshot()
    {
        if (_authoredBaseRigSnapshotCaptured && _authoredBaseRigSnapshot != null)
            return _authoredBaseRigSnapshot.Clone();

#if UNITY_EDITOR
        if (TryCapturePrefabAuthoredBaseRigSnapshot(out TrickPoseRigSnapshot prefabSnapshot))
        {
            _authoredBaseRigSnapshot = prefabSnapshot.Clone();
            _authoredBaseRigSnapshotCaptured = true;
            return prefabSnapshot;
        }

        if (!Application.isPlaying)
            return null;
#endif

        TrickPoseRigSnapshot baseSnapshot = CaptureRigSnapshotFromBaseCaches();
        _authoredBaseRigSnapshot = baseSnapshot?.Clone();
        _authoredBaseRigSnapshotCaptured = baseSnapshot != null;
        return baseSnapshot;
    }

#if UNITY_EDITOR
    private bool TryCapturePrefabAuthoredBaseRigSnapshot(out TrickPoseRigSnapshot snapshot)
    {
        snapshot = null;
        SkiController prefabController = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(this);
        if (prefabController == null)
            return false;

        snapshot = CaptureRigSnapshotFromControllerReferences(prefabController);
        return snapshot != null;
    }
#endif

    private static TrickPoseRigSnapshot CaptureRigSnapshotFromControllerReferences(SkiController source)
    {
        if (source == null)
            return null;

        SkierLimbLineVisual limbVisual = source.GetSkierLimbLineVisual();
        return new TrickPoseRigSnapshot
        {
            body = TrickPoseRigSnapshot.PartState.FromTransform(source.GetBodyPoseTransform()),
            head = TrickPoseRigSnapshot.PartState.FromTransform(source.headAnchorTransform),
            leftSki = TrickPoseRigSnapshot.PartState.FromTransform(source.leftSki),
            rightSki = TrickPoseRigSnapshot.PartState.FromTransform(source.rightSki),
            leftPole = TrickPoseRigSnapshot.PartState.FromTransform(source.leftPoleContact != null ? source.leftPoleContact.PoleRoot : null),
            rightPole = TrickPoseRigSnapshot.PartState.FromTransform(source.rightPoleContact != null ? source.rightPoleContact.PoleRoot : null),
            leftElbow = source.CaptureJointPartState(limbVisual, SkierLimbJoint.LeftElbow),
            rightElbow = source.CaptureJointPartState(limbVisual, SkierLimbJoint.RightElbow),
            leftKnee = source.CaptureJointPartState(limbVisual, SkierLimbJoint.LeftKnee),
            rightKnee = source.CaptureJointPartState(limbVisual, SkierLimbJoint.RightKnee)
        };
    }

    private TrickPoseRigSnapshot CaptureRigSnapshotFromBaseCaches()
    {
        return new TrickPoseRigSnapshot
        {
            body = GetBodyPoseTransform() == bodyMotionTransform
                ? MakePartState(GetBodyPoseTransform(), _bodyMotionBaseLocalPos, _bodyMotionBaseLocalRot)
                : MakePartState(GetBodyPoseTransform(), _bodyBaseLocalPos, _bodyBaseLocalRot),
            head = MakePartState(headAnchorTransform, _headAnchorBaseLocalPos, _headAnchorBaseLocalRot),
            leftSki = MakePartState(leftSki, _leftSkiLocalBasePos, _leftSkiLocalBaseRot),
            rightSki = MakePartState(rightSki, _rightSkiLocalBasePos, _rightSkiLocalBaseRot),
            leftPole = leftPoleContact != null ? leftPoleContact.CaptureBasePoseSnapshot() : default,
            rightPole = rightPoleContact != null ? rightPoleContact.CaptureBasePoseSnapshot() : default,
            leftElbow = default,
            rightElbow = default,
            leftKnee = default,
            rightKnee = default
        };
    }

    private static TrickPoseRigSnapshot.PartState MakePartState(Transform source, Vector3 localPosition)
    {
        return MakePartState(source, localPosition, source != null ? source.localRotation : Quaternion.identity);
    }

    private static TrickPoseRigSnapshot.PartState MakePartState(Transform source, Vector3 localPosition, Quaternion localRotation)
    {
        return new TrickPoseRigSnapshot.PartState
        {
            hasValue = source != null,
            localPosition = source != null ? localPosition : Vector3.zero,
            localRotation = source != null ? localRotation : Quaternion.identity
        };
    }

    [System.Obsolete("Use CaptureTrueDefaultRigSnapshot. Default snapshots are immutable authored base pose snapshots.")]
    public TrickPoseRigSnapshot CaptureDefaultRigSnapshot()
    {
        return CaptureTrueDefaultRigSnapshot();
    }

    public TrickPoseRigSnapshot CreateRigSnapshotFromEntry(TrickPoseEntry entry)
    {
        EnsureTrueDefaultRigSnapshotCaptured();
        if (_trueDefaultRigSnapshot == null)
            return null;

        TrickPoseRigSnapshot snapshot = _trueDefaultRigSnapshot.Clone();
        ApplyEntryToSnapshot(snapshot, entry, 1f);
        return snapshot;
    }

    public TrickPoseRigSnapshot ResolveRigAssistSnapshot(TrickPoseRigSnapshot snapshot, bool previewSolve, bool includeLimbCorrection)
    {
        if (snapshot == null)
            return null;

        return snapshot;
    }

    public TrickPoseRigAssistReferenceData BuildRigAssistReferenceData()
    {
        EnsureTrueDefaultRigSnapshotCaptured();
        return TrickPoseRigAssistUtility.BuildReferenceData(
            _trueDefaultRigSnapshot,
            trickPoseProfile != null ? trickPoseProfile.rigAssistSettings : null,
            GetSkierLimbLineVisual());
    }

    public TrickPoseRigAssistReferenceData BuildRigAssistCaptureReferenceData()
    {
        EnsureTrueDefaultRigSnapshotCaptured();
        return TrickPoseRigAssistUtility.BuildCaptureReferenceData(
            _trueDefaultRigSnapshot,
            GetSkierLimbLineVisual());
    }

    public void ApplyRigSnapshot(TrickPoseRigSnapshot snapshot, bool snap, float weight = 1f)
    {
        if (snapshot == null)
            return;

        float t = snap ? 1f : Mathf.Clamp01(weight);
        ApplySnapshotPart(GetBodyPoseTransform(), snapshot.body, t);
        ApplySnapshotPart(headAnchorTransform, snapshot.head, t);
        ApplySnapshotPart(leftSki, snapshot.leftSki, t);
        ApplySnapshotPart(rightSki, snapshot.rightSki, t);
        ApplySnapshotPart(leftPoleContact != null ? leftPoleContact.PoleRoot : null, snapshot.leftPole, t);
        ApplySnapshotPart(rightPoleContact != null ? rightPoleContact.PoleRoot : null, snapshot.rightPole, t);
        ApplyJointSnapshot(snapshot, t);
    }

    [System.Obsolete("Use RestoreTrueDefaultPose. Default restores are immutable authored base pose restores.")]
    public void RestoreDefaultRigSnapshot(bool snap = true)
    {
        RestoreTrueDefaultPose(snap);
    }

    public void RestorePreviewBaselineRigSnapshot(bool snap = true)
    {
        EnsurePoseRigDefaultsCaptured();
        ApplyRigSnapshot(_defaultRigSnapshot, snap, 1f);
    }

    public void RestoreTrueDefaultPose(bool snap = true)
    {
        EnsureTrueDefaultRigSnapshotCaptured();
        _activeTrickPoseEntry = null;
        _activeTrickPoseBlend = 0f;
        _activeTrickPoseScore = 0f;
        _activeTrickPoseCommittedAt = -999f;
        _activeTrickPoseLastValidTime = -999f;
        RestoreBaseCachesFromTrueDefault(_trueDefaultRigSnapshot);
        ApplyRigSnapshot(_trueDefaultRigSnapshot, snap, 1f);
    }

    private void RestoreBaseCachesFromTrueDefault(TrickPoseRigSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        if (snapshot.leftSki.hasValue)
        {
            _leftSkiLocalBasePos = snapshot.leftSki.localPosition;
            _leftSkiLocalBaseRot = snapshot.leftSki.localRotation;
        }

        if (snapshot.rightSki.hasValue)
        {
            _rightSkiLocalBasePos = snapshot.rightSki.localPosition;
            _rightSkiLocalBaseRot = snapshot.rightSki.localRotation;
        }

        if (snapshot.body.hasValue)
        {
            _bodyBaseLocalPos = snapshot.body.localPosition;
            _bodyBaseLocalRot = snapshot.body.localRotation;
            if (GetBodyPoseTransform() == bodyMotionTransform)
            {
                _bodyMotionBaseLocalPos = snapshot.body.localPosition;
                _bodyMotionBaseLocalRot = snapshot.body.localRotation;
            }
        }

        if (snapshot.head.hasValue)
        {
            _headAnchorBaseLocalPos = snapshot.head.localPosition;
            _headAnchorBaseLocalRot = snapshot.head.localRotation;
        }

        if (leftPoleContact != null)
            leftPoleContact.RestoreBasePoseFromSnapshot(snapshot.leftPole);

        if (rightPoleContact != null)
            rightPoleContact.RestoreBasePoseFromSnapshot(snapshot.rightPole);
    }

    public void PreviewTrickPoseEntry(TrickPoseEntry entry, bool snap)
    {
        ApplyRigSnapshot(CreateRigSnapshotFromEntry(entry), snap, 1f);
    }

    public void ClearPreviewPose(bool snap)
    {
        RestoreTrueDefaultPose(snap);
    }

    public void CaptureCurrentPoseIntoEntry(TrickPoseEntry entry)
    {
        if (entry == null)
            return;

        EnsureTrueDefaultRigSnapshotCaptured();
        if (_trueDefaultRigSnapshot == null)
            return;

        CaptureCurrentPartInto(entry.bodyPose, GetBodyPoseTransform(), _trueDefaultRigSnapshot.body);
        CaptureCurrentPartInto(entry.headPose, headAnchorTransform, _trueDefaultRigSnapshot.head);
        CaptureCurrentPartInto(entry.leftSkiPose, leftSki, _trueDefaultRigSnapshot.leftSki);
        CaptureCurrentPartInto(entry.rightSkiPose, rightSki, _trueDefaultRigSnapshot.rightSki);
        CaptureCurrentPartInto(entry.leftPolePose, leftPoleContact != null ? leftPoleContact.PoleRoot : null, _trueDefaultRigSnapshot.leftPole);
        CaptureCurrentPartInto(entry.rightPolePose, rightPoleContact != null ? rightPoleContact.PoleRoot : null, _trueDefaultRigSnapshot.rightPole);
        CaptureCurrentJointInto(entry.leftElbowPose, SkierLimbJoint.LeftElbow, _trueDefaultRigSnapshot.leftElbow);
        CaptureCurrentJointInto(entry.rightElbowPose, SkierLimbJoint.RightElbow, _trueDefaultRigSnapshot.rightElbow);
        CaptureCurrentJointInto(entry.leftKneePose, SkierLimbJoint.LeftKnee, _trueDefaultRigSnapshot.leftKnee);
        CaptureCurrentJointInto(entry.rightKneePose, SkierLimbJoint.RightKnee, _trueDefaultRigSnapshot.rightKnee);
    }

    private void RefreshPoseRigBaseCaches()
    {
        if (leftSki != null)
        {
            _leftSkiLocalBasePos = leftSki.localPosition;
            _leftSkiLocalBaseRot = leftSki.localRotation;
        }

        if (rightSki != null)
        {
            _rightSkiLocalBasePos = rightSki.localPosition;
            _rightSkiLocalBaseRot = rightSki.localRotation;
        }

        if (bodyTransform != null && bodyTransform != transform)
        {
            _bodyBaseLocalPos = bodyTransform.localPosition;
            _bodyBaseLocalRot = bodyTransform.localRotation;
            _bodyBaseLocalScale = bodyTransform.localScale;
        }

        if (bodyMotionTransform != null && bodyMotionTransform != transform)
        {
            _bodyMotionBaseLocalPos = bodyMotionTransform.localPosition;
            _bodyMotionBaseLocalRot = bodyMotionTransform.localRotation;
        }

        if (bodyScaleTransform != null)
            _bodyScaleBaseLocalScale = bodyScaleTransform.localScale;

        if (headAnchorTransform != null)
        {
            _headAnchorBaseLocalPos = headAnchorTransform.localPosition;
            _headAnchorBaseLocalRot = headAnchorTransform.localRotation;
        }

        if (leftPoleContact != null)
            leftPoleContact.RecaptureBasePoseFromCurrent();

        if (rightPoleContact != null)
            rightPoleContact.RecaptureBasePoseFromCurrent();
    }


    // ----------------------------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------------------------

    public void TeleportToSpawn(Vector3 worldPosition, Quaternion worldRotation, bool snapToGround = true)
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        // Hard-reset any locomotion authority that may still be driving the skier.
        ClearExternalInputSource();

        // Restore rigidbody control state in case we are teleporting out of a stack / ragdoll-like state.
        _rb.freezeRotation = true;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Clear transient movement / grounding state.
        _stacked = false;
        _stackRecoveryExternallyLocked = false;
        ResetGrindingState();
        _isGrounded = false;
        _wasGrounded = false;
        _wasControlsGrounded = false;
        _nearGroundForJump = false;
        _bodyNearGround = false;
        _skiContactPlausibleForBody = false;
        _lastMeasuredGroundGap = float.PositiveInfinity;
        _lastGroundRefreshSource = "teleport-reset";
        _lastGroundProbeAcceptedCollider = null;
        _lastGroundProbeIgnoredCollider = null;
        _lastGroundProbeIgnoredReason = "teleport-reset";
        _lastGroundProbeAcceptedDistance = float.PositiveInfinity;
        _lastGroundProbeAcceptedNormal = Vector3.up;
        _lastSkiTransformFallbackTime = -999f;
        _lastSkiTransformFallbackNormal = Vector3.up;
        _lastSkiTransformFallbackGap = float.PositiveInfinity;
        _lastBodyProbeHit = false;
        _lastBodyProbeDistance = float.PositiveInfinity;
        _lastBodyProbeGap = float.PositiveInfinity;
        _lastBodyProbeAllowedGap = 0f;
        _lastBodyProbeAcceptedButTooFar = false;
        _lastSkiFallbackFreshHit = false;
        _lastSkiFallbackSource = "none";
        _lastSkiFallbackFreshTime = -999f;
        _lastNoseTailLandingSource = "none";
        _lastGroundedFrom = "teleport-reset";
        _lastRealSupportTime = -999f;
        _probeOnlyGrounded = false;
        _probeOnlyEndGrounded = false;
        _probeOnlyGroundGap = float.PositiveInfinity;
        _probeOnlyGroundSource = "none";

        _wallContactUntil = 0f;
        _wallScrapeNormal = Vector3.zero;

        _groundNormal = Vector3.up;
        _alignNormal = Vector3.up;
        _resolvedSkiSupportNormal = Vector3.up;
        _lastResolvedSkiSupportNormalTime = -999f;
        _lastResolvedSkiSupportNormalSource = "teleport-reset";

        CancelEndContactFlattenRecovery();
        _landingEvaluationConsumedForCurrentAirborne = false;

        _skiForward = Vector3.ProjectOnPlane(worldRotation * Vector3.forward, Vector3.up).normalized;

        if (_skiForward.sqrMagnitude < 0.0001f)
            _skiForward = Vector3.forward;

        _airAngularVelocity = Vector3.zero;
        _grindHybridAngularVelocity = Vector3.zero;
        _movementMode = MovementMode.Airborne;

        _antiClipStableFrames = 0;
        _antiClipLastCheckedFrame = -999;
        _antiClipLastLiftNeeded = 0f;

        _tipContactAccumTime = 0f;
        _hasNonSkiGroundContact = false;
        _lastNonSkiGroundContactTime = -999f;

        _lastGroundedTime = -999f;
        _lastJumpTime = -999f;
        _airborneStartTime = Time.time;
        _airbornePeakY = worldPosition.y;

        if (leftSkiContact != null) leftSkiContact.ResetContactState();
        if (rightSkiContact != null) rightSkiContact.ResetContactState();

        _rb.position = worldPosition;
        _rb.rotation = worldRotation;

        Physics.SyncTransforms();

        if (leftSkiContact != null) leftSkiContact.ManualSampleGround();
        if (rightSkiContact != null) rightSkiContact.ManualSampleGround();

        CheckGround();
        UpdateSkiSupportNormalAuthority(Time.fixedDeltaTime);

        if (snapToGround && preventSkiTerrainClipping)
        {
            SnapToGroundClearance(resetDownwardVelocity: true, iterations: 3);

            if (leftSkiContact != null) leftSkiContact.ManualSampleGround();
            if (rightSkiContact != null) rightSkiContact.ManualSampleGround();

            CheckGround();
            UpdateSkiSupportNormalAuthority(Time.fixedDeltaTime);
        }

        _wasControlsGrounded = IsGroundedForControls;
    }

    /// <summary>
    /// Forces this skier into the same stacked/ragdoll-like state used by crashes,
    /// without requiring an impact. Intended for rescue casualties, staged NPCs,
    /// and other authored non-recovery ragdoll presentations.
    /// </summary>
    public void ForceStackedRagdollState(
        bool lockRecovery = true,
        bool refreshVisualState = true,
        Vector3? inheritedVelocityWorld = null,
        Vector3? torqueAxisWorld = null,
        float severity01 = 0.35f,
        string reason = "ForcedStackedRagdoll")
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        if (_rb == null)
            return;

        severity01 = Mathf.Clamp01(severity01);

        bool wasStacked = _stacked;

        _dbgLastStackReason = string.IsNullOrWhiteSpace(reason)
            ? "ForcedStackedRagdoll"
            : reason;

        _stacked = true;
        _stackRecoveryExternallyLocked = lockRecovery;
        _stackedAtTime = Time.time;
        _hasPendingStackImpact = false;
        _movementMode = MovementMode.Stacked;

        _grindActive = false;
        _grindTime = 0f;
        _grindStrengthSmoothed = 0f;
        _tipContactAccumTime = 0f;

        _rb.freezeRotation = false;
        _rb.useGravity = false;

        if (!_rb.isKinematic)
        {
            if (inheritedVelocityWorld.HasValue)
                _rb.linearVelocity = inheritedVelocityWorld.Value;

            if (torqueAxisWorld.HasValue && torqueAxisWorld.Value.sqrMagnitude > 0.0001f)
            {
                _rb.angularVelocity = Vector3.zero;
                _rb.AddTorque(
                    torqueAxisWorld.Value.normalized * (stackTorqueImpulse * Mathf.Lerp(0.35f, 0.8f, severity01)),
                    ForceMode.Impulse);
            }
        }

        SetStackEquipmentCollidersSuppressed(true);

        if (refreshVisualState || !wasStacked)
        {
            Vector3 eventVelocity = inheritedVelocityWorld ?? _rb.linearVelocity;
            OnStacked?.Invoke(new StackEventInfo(
                transform.position,
                eventVelocity,
                severity01,
                _dbgLastStackReason));
        }
    }

    public void SetStackRecoveryExternallyLocked(bool locked)
    {
        _stackRecoveryExternallyLocked = locked;
    }

    public void TriggerImpactStack(Vector3 impactDirectionWorld, float severity01 = 1f, string reason = "SkierCollision")
    {
        if (_stacked)
            return;

        _dbgLastStackReason = string.IsNullOrWhiteSpace(reason) ? "SkierCollision" : reason;

        Vector3 impactNormal = Vector3.ProjectOnPlane(impactDirectionWorld, Vector3.up);
        if (impactNormal.sqrMagnitude < 0.0001f)
            impactNormal = -Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
        if (impactNormal.sqrMagnitude < 0.0001f)
            impactNormal = transform.forward;

        TriggerImpactStackFromPointAndNormal(
            transform.position,
            impactNormal.normalized,
            _rb.linearVelocity,
            severity01,
            reason);
    }

    public void TriggerImpactStackFromPoint(Vector3 impactPointWorld, Vector3 incomingVelocityWorld, float severity01 = 1f, string reason = "SkierCollision")
    {
        if (_stacked)
            return;

        Vector3 away = transform.position - impactPointWorld;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f)
            away = -Vector3.ProjectOnPlane(incomingVelocityWorld, Vector3.up);

        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;

        TriggerImpactStackFromPointAndNormal(impactPointWorld, away.normalized, incomingVelocityWorld, severity01, reason);
    }

    public void TriggerImpactStackFromPointAndNormal(Vector3 impactPointWorld, Vector3 impactNormalWorld, Vector3 incomingVelocityWorld, float severity01 = 1f, string reason = "SkierCollision")
    {
        if (_stacked)
            return;

        _dbgLastStackReason = string.IsNullOrWhiteSpace(reason) ? "SkierCollision" : reason;

        Vector3 impactNormal = Vector3.ProjectOnPlane(impactNormalWorld, Vector3.up);
        if (impactNormal.sqrMagnitude < 0.0001f)
            impactNormal = transform.position - impactPointWorld;
        if (impactNormal.sqrMagnitude < 0.0001f)
            impactNormal = -Vector3.ProjectOnPlane(incomingVelocityWorld, Vector3.up);
        if (impactNormal.sqrMagnitude < 0.0001f)
            impactNormal = transform.forward;
        impactNormal.Normalize();

        _hasPendingStackImpact = true;
        _pendingStackImpactNormal = impactNormal;
        _pendingStackImpactVelocity = incomingVelocityWorld;
        _pendingStackImpactPoint = impactPointWorld;

        Vector3 torqueAxis = ComputeImpactStackTorqueAxis(impactPointWorld, impactNormal, incomingVelocityWorld);
        TriggerStack(Mathf.Clamp01(severity01), torqueAxis.normalized);
    }

    public void RecoverFromStack(Vector3 forwardHint)
    {
        if (!_stacked) return;

        _stacked = false;
        _stackRecoveryExternallyLocked = false;
        _hasPendingStackImpact = false;
        _rb.freezeRotation = true;
        _rb.angularVelocity = Vector3.zero;

        SetStackEquipmentCollidersSuppressed(false);

        Vector3 up = Vector3.up;
        Vector3 f = Vector3.ProjectOnPlane(forwardHint, up);
        if (f.sqrMagnitude < 0.0001f) f = transform.forward;
        f.Normalize();

        transform.rotation = Quaternion.LookRotation(f, up);

        OnRecoveredFromStack?.Invoke(new StackRecoveryEventInfo(
            transform.position,
            f,
            _isGrounded));
    }

    public void ResetStackStateSilently(bool snapUpright = true, Vector3? forwardHint = null)
    {
        if (!_stacked)
        {
            _stackRecoveryExternallyLocked = false;
            return;
        }

        _stacked = false;
        _stackRecoveryExternallyLocked = false;
        _hasPendingStackImpact = false;
        _rb.freezeRotation = true;
        _rb.angularVelocity = Vector3.zero;
        _tipContactAccumTime = 0f;

        SetStackEquipmentCollidersSuppressed(false);

        if (snapUpright)
        {
            Vector3 up = Vector3.up;
            Vector3 f = forwardHint.HasValue
                ? Vector3.ProjectOnPlane(forwardHint.Value, up)
                : Vector3.ProjectOnPlane(transform.forward, up);

            if (f.sqrMagnitude < 0.0001f)
                f = Vector3.forward;

            f.Normalize();
            transform.rotation = Quaternion.LookRotation(f, up);
        }

        _movementMode = _isGrounded ? MovementMode.Skiing : MovementMode.Airborne;
    }

    public void SetExternalInputSource(MonoBehaviour source)
    {
        externalInputSourceBehaviour = source;
        CacheExternalInputSource();
    }

    public void ClearExternalInputSource()
    {
        externalInputSourceBehaviour = null;
        _externalInputSource = null;
    }

    private void CacheExternalInputSource()
    {
        _externalInputSource = externalInputSourceBehaviour as ISkiInputSource;

        if (externalInputSourceBehaviour != null && _externalInputSource == null)
        {
            Debug.LogError(
                $"[{nameof(SkiController)}] External input source on '{name}' does not implement ISkiInputSource.",
                this);
        }
    }

    private bool TryGetExternalInput(out SkiInputFrame input)
    {
        bool hasAssignedSource =
            externalInputSourceBehaviour != null &&
            externalInputSourceBehaviour.isActiveAndEnabled &&
            _externalInputSource != null;

        if (!hasAssignedSource)
        {
            input = SkiInputFrame.Neutral;
            return false;
        }

        if (!_externalInputSource.HasInput())
        {
            input = SkiInputFrame.Neutral;
            return false;
        }

        input = _externalInputSource.GetSkiInput();
        return true;
    }

    // ----------------------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------------------

    private void OnValidate()
    {
        CacheExternalInputSource();
#if UNITY_EDITOR
        WarnIfLayerMaskContains(groundLayers, "Default", "SkiController.groundLayers should only include Ground. Default props may be grindable, but are not terrain ground.");
        WarnIfLayerMaskContains(groundLayers, "NPC", "SkiController.groundLayers should not include NPC.");
        WarnIfLayerMaskContains(groundLayers, "Player", "SkiController.groundLayers should not include Player.");
        WarnIfLayerMaskContains(grindableLayers, "NPC", "SkiController.grindableLayers should not include NPC.");
        WarnIfLayerMaskContains(grindableLayers, "Player", "SkiController.grindableLayers should not include Player.");
#endif
    }

    private static void CacheCharacterLayers()
    {
        if (_npcLayer == -2)
            _npcLayer = LayerMask.NameToLayer("NPC");

        if (_playerLayer == -2)
            _playerLayer = LayerMask.NameToLayer("Player");
    }

    private bool IsOwnCollider(Collider c)
    {
        if (c == null)
            return false;

        if (_rb != null && c.attachedRigidbody == _rb)
            return true;

        if (c.transform == transform || c.transform.IsChildOf(transform))
            return true;

        return false;
    }

    private bool IsCharacterCollider(Collider c)
    {
        if (c == null)
            return false;

        CacheCharacterLayers();
        int layer = c.gameObject.layer;
        return (_npcLayer >= 0 && layer == _npcLayer) ||
               (_playerLayer >= 0 && layer == _playerLayer);
    }

    private bool IsValidGroundCollider(Collider c)
    {
        if (c == null)
            return false;

        if (IsOwnCollider(c) || IsCharacterCollider(c))
            return false;

        return (groundLayers.value & (1 << c.gameObject.layer)) != 0;
    }

    private string GetGroundHitRejectReason(Collider c, Vector3 normal)
    {
        if (c == null)
            return "no-hit";

        if (IsOwnCollider(c))
            return "own-collider";

        if (IsCharacterCollider(c))
            return "character-collider";

        if ((groundLayers.value & (1 << c.gameObject.layer)) == 0)
            return "not-in-groundLayers";

        if (!IsRideableNormal(normal))
            return "non-rideable-normal";

        return "accepted";
    }

    private bool TryGetBestGroundHit(Vector3 origin, float radius, Vector3 direction, float maxDistance, out RaycastHit bestHit)
    {
        bestHit = default;
        _lastGroundProbeAcceptedCollider = null;
        _lastGroundProbeIgnoredCollider = null;
        _lastGroundProbeIgnoredReason = "no-hit";
        _lastGroundProbeAcceptedDistance = float.PositiveInfinity;
        _lastGroundProbeAcceptedNormal = Vector3.up;

        int count = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            _groundHitBuffer,
            maxDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _groundHitBuffer[i];
            string rejectReason = GetGroundHitRejectReason(hit.collider, hit.normal);
            if (rejectReason != "accepted")
            {
                if (_lastGroundProbeIgnoredCollider == null)
                    _lastGroundProbeIgnoredCollider = hit.collider;

                _lastGroundProbeIgnoredReason = rejectReason;
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        if (found)
        {
            _lastGroundProbeAcceptedCollider = bestHit.collider;
            _lastGroundProbeAcceptedDistance = bestHit.distance;
            _lastGroundProbeAcceptedNormal = bestHit.normal.sqrMagnitude > 0.0001f ? bestHit.normal.normalized : Vector3.up;
            _lastGroundProbeIgnoredReason = "accepted";
        }

        return found;
    }

#if UNITY_EDITOR
    private void WarnIfLayerMaskContains(LayerMask mask, string layerName, string message)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0 && (mask.value & (1 << layer)) != 0)
            Debug.LogWarning($"[{nameof(SkiController)}] {message}", this);
    }
#endif

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        CacheCharacterLayers();
        CacheExternalInputSource();

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

        if (bodyTransform != null && bodyTransform != transform)
        {
            _bodyBaseLocalPos = bodyTransform.localPosition;
            _bodyBaseLocalRot = bodyTransform.localRotation;
            _bodyBaseLocalScale = bodyTransform.localScale;
        }

        if (bodyMotionTransform != null && bodyMotionTransform != transform)
        {
            _bodyMotionBaseLocalPos = bodyMotionTransform.localPosition;
            _bodyMotionBaseLocalRot = bodyMotionTransform.localRotation;
        }

        if (bodyScaleTransform != null)
            _bodyScaleBaseLocalScale = bodyScaleTransform.localScale;

        if (headAnchorTransform != null)
        {
            _headAnchorBaseLocalPos = headAnchorTransform.localPosition;
            _headAnchorBaseLocalRot = headAnchorTransform.localRotation;
        }

        _skiForward = transform.forward;
        CacheSkiColliders();
        EnsurePoseRigDefaultsCaptured();

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

        bool swappedAny = false;

        if (skisProfile.skiPrefab != null)
        {
            leftSkiVisual = SwapSkiVisual(leftSki, ref _leftSkiVisual, skisProfile.skiPrefab);
            swappedAny = true;
        }

        if (skisProfile.skiPrefab != null)
        {
            rightSkiVisual = SwapSkiVisual(rightSki, ref _rightSkiVisual, skisProfile.skiPrefab);
            swappedAny = true;
        }

        CacheSkiColliders();

        if (swappedAny)
        {
            if (leftSkiContact != null)
                leftSkiContact.NotifySkiModelChanged();

            if (rightSkiContact != null)
                rightSkiContact.NotifySkiModelChanged();

            // Force an immediate re-sample so grounding is valid on the same frame.
            if (leftSkiContact != null)
                leftSkiContact.ManualSampleGround();

            if (rightSkiContact != null)
                rightSkiContact.ManualSampleGround();

            CheckGround();

            if (preventSkiTerrainClipping)
                SnapToGroundClearance(resetDownwardVelocity: true, iterations: 2);

            if (leftSkiContact != null)
                leftSkiContact.ManualSampleGround();

            if (rightSkiContact != null)
                rightSkiContact.ManualSampleGround();

            CheckGround();
            _wasControlsGrounded = IsGroundedForControls;
        }
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

        if (leftPoleContact != null) ApplyMpbToRenderers(leftPoleContact.PoleRoot, _poleMpb);
        if (rightPoleContact != null) ApplyMpbToRenderers(rightPoleContact.PoleRoot, _poleMpb);

    }

    private static void ApplyMpbToRenderers(Transform root, MaterialPropertyBlock mpb)
    {
        if (root == null) return;
        var rs = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null) continue;

            // Critical: don't tint snow/FX
            if (r is ParticleSystemRenderer) continue;

            // Gloves are parented under pole roots in ski mode, but they are independent wearable slots.
            // Skip any renderer owned by a nested wearable attachment so pole styling only affects
            // the pole visual subtree itself.
            var owningWearable = r.GetComponentInParent<WearableAttachment>();
            if (owningWearable != null &&
                owningWearable.transform != root &&
                owningWearable.transform.IsChildOf(root))
            {
                continue;
            }

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
        if (leftSkiAction != null && leftSkiAction.action != null)
            leftSkiAction.action.Enable();

        if (rightSkiAction != null && rightSkiAction.action != null)
            rightSkiAction.action.Enable();

        if (leanAction != null && leanAction.action != null)
            leanAction.action.Enable();

        if (polesAction != null && polesAction.action != null)
            polesAction.action.Enable();

        if (tuckAction != null && tuckAction.action != null)
            tuckAction.action.Enable();

        if (poseAction != null && poseAction.action != null)
            poseAction.action.Enable();

        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Enable();

        if (preventSkiTerrainClipping)
            SnapToGroundClearance(resetDownwardVelocity: true);
    }

    private void OnDisable()
    {
        SetStackEquipmentCollidersSuppressed(false);

        if (leftSkiAction != null && leftSkiAction.action != null)
            leftSkiAction.action.Disable();

        if (rightSkiAction != null && rightSkiAction.action != null)
            rightSkiAction.action.Disable();

        if (leanAction != null && leanAction.action != null)
            leanAction.action.Disable();

        if (polesAction != null && polesAction.action != null)
            polesAction.action.Disable();

        if (tuckAction != null && tuckAction.action != null)
            tuckAction.action.Disable();

        if (poseAction != null && poseAction.action != null)
            poseAction.action.Disable();

        if (jumpAction != null && jumpAction.action != null)
            jumpAction.action.Disable();
    }

    private void OnDestroy()
    {
        SetStackEquipmentCollidersSuppressed(false);
        UnbindGearLoadout();
    }

    // ----------------------------------------------------------------------
    // COLLISIONS (ANTI "SLIDE ON HEAD")
    // ----------------------------------------------------------------------
    // Unity will send collision callbacks to the Rigidbody's GameObject even
    // for child colliders. We filter out ski colliders so only "body" impacts
    // (head/torso/arms/etc) can trigger a stack.
    private void OnCollisionEnter(Collision collision)
    {
        EvaluateStackedSecondaryImpactCollision(collision);
        EvaluateBodyGroundCollision(collision);
        EvaluateObstacleImpactCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateStackedSecondaryImpactCollision(collision);
        EvaluateBodyGroundCollision(collision);
        EvaluateObstacleImpactCollision(collision);
    }

    private void EvaluateBodyGroundCollision(Collision collision)
    {
        if (collision == null)
            return;

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
        _lastNonSkiGroundContactTime = Time.time;
    }

    private void EvaluateObstacleImpactCollision(Collision collision)
    {
        if (collision == null || _stacked || _rb == null || collision.contactCount <= 0)
            return;

        Collider otherCollider = collision.collider;
        if (otherCollider == null)
            return;

        int otherLayer = otherCollider.gameObject.layer;
        if ((stackImpactLayers.value & (1 << otherLayer)) == 0)
            return;

        if (ignoreGroundLayerForImpactStack && (groundLayers.value & (1 << otherLayer)) != 0)
            return;

        SkiController otherSkier = otherCollider.GetComponentInParent<SkiController>();
        if (otherSkier != null && otherSkier != this)
            return;

        Vector3 selfVelocity = _rb.linearVelocity;
        float planarSpeed = Vector3.ProjectOnPlane(selfVelocity, Vector3.up).magnitude;
        if (planarSpeed < obstacleImpactMinPlanarSpeed)
            return;

        bool foundImpact = false;
        float bestIntoSpeed = 0f;
        Vector3 bestPoint = default;
        Vector3 bestNormal = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint cp = collision.GetContact(i);
            if (cp.thisCollider != null && cp.thisCollider.isTrigger)
                continue;

            Vector3 normal = cp.normal.sqrMagnitude > 0.0001f ? cp.normal.normalized : Vector3.up;
            float intoSpeed = Mathf.Max(0f, -Vector3.Dot(selfVelocity, normal));
            if (intoSpeed <= bestIntoSpeed)
                continue;

            foundImpact = true;
            bestIntoSpeed = intoSpeed;
            bestPoint = cp.point;
            bestNormal = normal;
        }

        if (!foundImpact || bestIntoSpeed < obstacleImpactMinIntoSpeed)
            return;

        float severity = Mathf.InverseLerp(
            obstacleImpactMinIntoSpeed,
            Mathf.Max(obstacleImpactMinIntoSpeed + 0.01f, obstacleImpactFullSpeed),
            bestIntoSpeed);

        TriggerImpactStackFromPointAndNormal(bestPoint, bestNormal, selfVelocity, severity, "ObstacleImpact");
    }

    private void EvaluateStackedSecondaryImpactCollision(Collision collision)
    {
        if (!_stacked || collision == null || collision.contactCount <= 0 || _rb == null)
            return;

        if (Time.time < _lastStackedSecondaryImpactTime + stackedSecondaryImpactCooldown)
            return;

        int otherLayer = collision.gameObject.layer;
        bool groundCollision = (groundLayers.value & (1 << otherLayer)) != 0;
        bool hasNonSkiContact = false;
        Vector3 bestPoint = transform.position;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint cp = collision.GetContact(i);
            if (cp.thisCollider == null || !_skiColliders.Contains(cp.thisCollider))
                hasNonSkiContact = true;

            bestPoint = cp.point;
            if (hasNonSkiContact)
                break;
        }

        if (groundCollision && !hasNonSkiContact)
            return;

        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (relativeSpeed < stackedSecondaryImpactMinSpeed)
            return;

        float severity = Mathf.InverseLerp(
            stackedSecondaryImpactMinSpeed,
            stackedSecondaryImpactMinSpeed * 3f,
            relativeSpeed);

        OnStackImpact?.Invoke(new StackEventInfo(bestPoint, collision.relativeVelocity, severity, "StackImpact"));
        _lastStackedSecondaryImpactTime = Time.time;
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
        _dbgGroundForceRanLastFixed = false;
        _dbgSkateRanLastFixed = false;
        _dbgPoleRanLastFixed = false;

        // Sample per-ski ground contact deterministically at the start of physics.
        // This prevents 'airborne' states when ski colliders are touching but the body casts miss.
        if (leftSkiContact != null) leftSkiContact.ManualSampleGround();
        if (rightSkiContact != null) rightSkiContact.ManualSampleGround();

        _wasGrounded = _isGrounded;
        bool wasControlsGrounded = _wasControlsGrounded;

        CheckGround();
        ValidateEndContactFlattenRecoveryState();
        UpdateSkiSupportNormalAuthority(Time.fixedDeltaTime);

        if (_stacked)
        {
            ResetGrindingState();

            UpdateStackedPhysics(Time.fixedDeltaTime);
            TryAutoRecoverFromStack();

            _movementMode = MovementMode.Stacked;
            _wasControlsGrounded = false;

            return;
        }

        UpdateWallScrapeSuppressionAndResponse();

        UpdateSlopeCache();
        _skiForward = GetCombinedSkiForwardOnPlane();
        TryUpdateNoseTailSlideState();

        if (!IsGroundedForControls)
        {
            _lastAirPlanarVel = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
        }

        UpdateGrinding(Time.fixedDeltaTime);

        bool grindingSurface = _grindActive;

        if (!_isGrounded && !HasAnySkiContact && !_grindActive)
        {
            UpdateAirborneLandingDebug();
        }

        // Do not run terrain penetration correction while grinding in-air;
        // it can fight rail motion and adds unnecessary work every physics step.
        bool shouldRunSkiClearance =
            preventSkiTerrainClipping &&
            !grindingSurface &&
            (_isGrounded || _bodyNearGround || _skiContactPlausibleForBody || HasAnySkiContact);

        if (shouldRunSkiClearance)
            ResolveSkiTerrainPenetration(hardSnap: false);

        ApplyEndContactFlattenRecovery(Time.fixedDeltaTime);

        // Custom gravity: full gravity in air, tangential-only gravity when grounded.
        // This prevents "stalling mid-slope" and removes double-gravity ambiguity.
        if (IsGroundedForControls)
        {
            Vector3 gPlane = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal);

            float gScale = Mathf.Lerp(
                1f,
                groundedSlopeGravityScale * _gearTuning.downhillAccelMul,
                _slopeT35);

            float grindGravityMul = _grindActive
                ? Mathf.Lerp(1f, grindSlopeGravityMultiplier, Mathf.Clamp01(_grindStrengthSmoothed))
                : 1f;

            gPlane *= gScale * grindGravityMul;

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
        ApplyStackedTumbleDamping(Time.fixedDeltaTime);

        // Track when we leave the ground for jump/landing severity.
        if (_wasGrounded && !_isGrounded)
        {
            _airborneStartTime = Time.time;
            _airbornePeakY = _rb.position.y;
            BeginAirEntry(resetYaw: false);
            UpdateAirborneLandingDebug();
        }

        // Track jump apex for hard-landing / slam detection.
        if (!_isGrounded)
            _airbornePeakY = Mathf.Max(_airbornePeakY, transform.position.y);

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
            _dbgGroundForceRanLastFixed = true;

            ApplyGrindLowSpeedTractionAssist(Time.fixedDeltaTime);
            ApplyGrindHybridOrientationControl(Time.fixedDeltaTime);

            DetectAndApplySkatePushes();
            _dbgSkateRanLastFixed = true;
            ApplyPoleForces();
            _dbgPoleRanLastFixed = true;

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
    private void ClearRawInputState(bool clearJumpState)
    {
        _rawLeftLegInput = 0f;
        _rawRightLegInput = 0f;
        _rawLeanInput = 0f;
        _rawPolesPressed = false;
        _rawTuckPressed = false;
        _rawPosePressed = false;

        if (!clearJumpState)
            return;

        _jumpQueued = false;
        _jumpHeld = false;
        _jumpReleaseQueued = false;
        _jumpMustFireWhileGrinding = false;
    }

    private void ReadInputs()
    {
        if (TryGetExternalInput(out SkiInputFrame externalInput))
        {
            _rawLeftLegInput = Mathf.Clamp01(externalInput.leftLeg01);
            _rawRightLegInput = Mathf.Clamp01(externalInput.rightLeg01);
            _rawLeanInput = Mathf.Clamp(externalInput.lean01, -1f, 1f);
            _rawPolesPressed = externalInput.polesHeld;

            // Replace this once ISkiInputSource / SkiInputFrame exposes tuckHeld.
            _rawTuckPressed = false;
            _rawPosePressed = false;

            HandleJumpInput(
                externalInput.jumpPressedThisFrame,
                externalInput.jumpReleasedThisFrame,
                externalInput.jumpHeld);

            return;
        }

        if (!acceptPlayerInput)
        {
            ClearRawInputState(clearJumpState: true);
            return;
        }

        if (leftSkiAction != null && leftSkiAction.action != null &&
            rightSkiAction != null && rightSkiAction.action != null)
        {
            _rawLeftLegInput = Mathf.Clamp01(leftSkiAction.action.ReadValue<float>());
            _rawRightLegInput = Mathf.Clamp01(rightSkiAction.action.ReadValue<float>());
        }
        else
        {
            _rawLeftLegInput = 0f;
            _rawRightLegInput = 0f;
        }

        if (leanAction != null && leanAction.action != null)
        {
            _rawLeanInput = Mathf.Clamp(leanAction.action.ReadValue<float>(), -1f, 1f);
        }
        else
        {
            _rawLeanInput = 0f;
        }

        if (polesAction != null && polesAction.action != null)
        {
            _rawPolesPressed = polesAction.action.ReadValue<float>() > 0.5f;
        }
        else
        {
            _rawPolesPressed = false;
        }

        if (tuckAction != null && tuckAction.action != null)
        {
            _rawTuckPressed = tuckAction.action.ReadValue<float>() > 0.5f;
        }
        else
        {
            _rawTuckPressed = false;
        }

        if (poseAction != null && poseAction.action != null)
        {
            _rawPosePressed = poseAction.action.ReadValue<float>() > 0.5f;
        }
        else
        {
            _rawPosePressed = false;

        }
        if (jumpAction != null && jumpAction.action != null)
        {
            var action = jumpAction.action;
            HandleJumpInput(
                action.WasPressedThisFrame(),
                action.WasReleasedThisFrame(),
                action.IsPressed());
        }
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

        bool grounded = IsGroundedForControls;
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

    private void HandleJumpInput(bool pressedThisFrame, bool releasedThisFrame, bool isHeld)
    {
        if (pressedThisFrame)
        {
            _jumpQueued = true;
            _jumpHeld = true;
            _jumpReleaseQueued = false;
            _lastJumpPressedTime = Time.time;

            // If jump was initiated while grinding, require it to fire while still grinding.
            _jumpMustFireWhileGrinding = _grindActive;
        }

        _jumpHeld = isHeld;

        if (releasedThisFrame)
        {
            _jumpHeld = false;
            _lastJumpReleasedTime = Time.time;
            _lastSkiJumpCharge01 = SkiJumpCharge01;

            if (_jumpQueued)
                _jumpReleaseQueued = true;
        }

        if (_jumpQueued &&
            !_jumpHeld &&
            !_jumpReleaseQueued &&
            (Time.time - _lastJumpPressedTime) > jumpBufferTime)
        {
            _jumpQueued = false;
            _jumpMustFireWhileGrinding = false;
        }
    }

    private void HandleJumpInput()
    {
        if (jumpAction == null || jumpAction.action == null)
            return;

        var action = jumpAction.action;

        HandleJumpInput(
            action.WasPressedThisFrame(),
            action.WasReleasedThisFrame(),
            action.IsPressed());
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
        bool haveSkiHardContact = HasAnySkiCollisionContact;
        bool haveSkiEndGroundContact = HasAnySkiEndGroundContact;
        bool haveRideableSkiContact = TryGetRideableSkiLandingNormal(out Vector3 skiLandingNormal);

        if (_stacked)
        {
            UpdateStackedGroundingOnly(haveSkiContact, haveRideableSkiContact, skiLandingNormal);
            return;
        }

        // If we've just jumped, enforce a short ungrounded window so the jump
        // can actually leave the surface instead of instantly re-sticking.
        // We ONLY allow bypassing this window if we have a real collision contact.
        if (Time.time - _lastJumpTime < minJumpUngroundedTime && !haveSkiHardContact)
        {
            _isGrounded = false;
            _bodyNearGround = false;
            _skiContactPlausibleForBody = false;
            _probeOnlyGrounded = false;
            _probeOnlyEndGrounded = false;
            _lastGroundRefreshSource = "jump-suppressed";
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


        if (TryGetBestGroundHit(centerOrigin, groundCheckRadius, Vector3.down, maxDist, out RaycastHit centerHit))
        {
            float contactGap = Mathf.Max(0f, centerHit.distance - groundCheckHeight);
            _lastMeasuredGroundGap = contactGap;
            float allowedGap = haveSkiContact ? groundContactDistanceWhenSkiContact : groundContactDistance;
            _lastBodyProbeHit = true;
            _lastBodyProbeDistance = centerHit.distance;
            _lastBodyProbeGap = contactGap;
            _lastBodyProbeAllowedGap = allowedGap;
            _lastBodyProbeAcceptedButTooFar = contactGap > allowedGap;

            if (contactGap <= allowedGap && IsRideable(centerHit.normal))
            {
                nearGround = true;
                rawNormal = centerHit.normal.normalized;
                _lastGroundedTime = Time.time;
                _lastRealSupportTime = Time.time;
                _lastGroundedFrom = "body-collision";
                _lastGroundRefreshSource = "body-ground";
            }
        }
        else
        {
            _lastMeasuredGroundGap = float.PositiveInfinity;
            _lastBodyProbeHit = false;
            _lastBodyProbeDistance = float.PositiveInfinity;
            _lastBodyProbeGap = float.PositiveInfinity;
            _lastBodyProbeAllowedGap = haveSkiContact ? groundContactDistanceWhenSkiContact : groundContactDistance;
            _lastBodyProbeAcceptedButTooFar = false;
        }

        _nearGroundForJump = nearGround;

        bool wasGroundedBefore = _isGrounded;

        bool skiFallbackGrounded = false;
        SkiGroundFallbackResult skiFallback = default;
        bool hasAuthoritativeSkiContacts =
            (leftSkiContact != null && leftSkiContact.isActiveAndEnabled) ||
            (rightSkiContact != null && rightSkiContact.isActiveAndEnabled);

        if (!hasAuthoritativeSkiContacts && !nearGround && useSkiTransformGroundFallback && !_grindActive)
        {
            skiFallbackGrounded = TryGetSkiGroundFallback(out skiFallback);
        }
        else if (hasAuthoritativeSkiContacts)
        {
            _lastSkiFallbackFreshHit = false;
            _lastSkiFallbackSource = "ignored-ski-contact-authoritative";
        }

        bool hasBodyCollisionSupport =
            _hasNonSkiGroundContact &&
            _nonSkiGroundContactUpDot >= 0.25f &&
            (Time.time - _lastNonSkiGroundContactTime) <= Time.fixedDeltaTime * 2.5f;
        bool hasSkiCollisionSupport = HasAnySkiCollisionContact;
        bool collisionCoyoteSupport =
            !hasBodyCollisionSupport &&
            !hasSkiCollisionSupport &&
            !nearGround &&
            (Time.time - _lastRealSupportTime) <= controlCoyoteTime;

        bool hasStableSkiSupport = hasBodyCollisionSupport || hasSkiCollisionSupport || collisionCoyoteSupport;

        bool hasEndOnlySupport =
            !hasStableSkiSupport &&
            (haveSkiEndGroundContact || (skiFallbackGrounded && skiFallback.isEndContact));

        _bodyNearGround = nearGround;
        _skiContactPlausibleForBody = nearGround || hasStableSkiSupport;
        _isGrounded = nearGround || hasStableSkiSupport;

        if (_isGrounded || hasStableSkiSupport || nearGround)
        {
            CancelEndContactFlattenRecovery();
        }

        _probeOnlyGrounded = false;
        _probeOnlyEndGrounded = false;
        _probeOnlyGroundGap = skiFallbackGrounded ? skiFallback.gap : _lastMeasuredGroundGap;
        _probeOnlyGroundSource =
            skiFallbackGrounded ? skiFallback.source :
            haveSkiEndGroundContact ? "probe-end-contact" :
            "none";

        if (hasStableSkiSupport && !nearGround)
        {
            _lastGroundedTime = Time.time;
            _lastRealSupportTime = (hasBodyCollisionSupport || hasSkiCollisionSupport)
                ? Time.time
                : _lastRealSupportTime;
            _lastGroundedFrom = hasBodyCollisionSupport ? "body-collision" : hasSkiCollisionSupport ? "ski-collision" : "collision-coyote";
            _lastGroundRefreshSource = _lastGroundedFrom;
        }
        else if (!_isGrounded)
        {
            _lastGroundedFrom = hasEndOnlySupport ? "end-contact-decision" : "none";
            _lastGroundRefreshSource = _lastBodyProbeAcceptedButTooFar ? "none-body-gap-too-large" : "none-no-ground-hit";
        }

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
        else if (!wasGroundedBefore && skiFallbackGrounded && skiFallback.isEndContact)
        {
            _groundNormal = skiFallback.normal.sqrMagnitude > 0.0001f ? skiFallback.normal.normalized : Vector3.up;
            _airAngularVelocity = Vector3.MoveTowards(
                _airAngularVelocity,
                Vector3.zero,
                Mathf.Max(0f, endContactFlattenAngularDamp) * Time.fixedDeltaTime);

            if (!TryStartNoseTailLandingFromFallback(skiFallback))
                EvaluateLanding();
        }
        else if (!wasGroundedBefore && haveSkiEndGroundContact)
        {
            _groundNormal = haveRideableSkiContact && skiLandingNormal.sqrMagnitude > 0.0001f
                ? skiLandingNormal.normalized
                : Vector3.up;
            _airAngularVelocity = Vector3.MoveTowards(
                _airAngularVelocity,
                Vector3.zero,
                Mathf.Max(0f, endContactFlattenAngularDamp) * Time.fixedDeltaTime);

            if (!TryStartNoseTailLandingFromSkiContacts())
                EvaluateLanding();
        }
        else if (!wasGroundedBefore && haveRideableSkiContact)
        {
            float airTime = Time.time - _airborneStartTime;
            if (airTime > minLandingAirTime * 0.5f)
            {
                _groundNormal = skiLandingNormal;
                _airAngularVelocity = Vector3.zero;

                if (TryUpdateNoseTailSlideState())
                {
                    PreserveLandingVelocityOnSlope(Mathf.Clamp01(landingProjectionStrength * 0.75f));
                    _landingAssistUntil = Time.time + landingAlignBoostDuration;
                    _lastGroundedTime = Time.time;
                    _dbgLastStackReason = "Nose/Tail Slide Landing";
                    _movementMode = MovementMode.Landing;

                    float slideFallHeight = Mathf.Max(0f, _airbornePeakY - _rb.position.y);
                    float slideDownwardSpeed = Mathf.Max(
                        0f,
                        -Vector3.Dot(_rb.linearVelocity, skiLandingNormal.sqrMagnitude > 0.0001f ? skiLandingNormal.normalized : Vector3.up));

                    TriggerSkiLandingVisual(airTime, slideFallHeight, slideDownwardSpeed);
                }
                else
                {
                    EvaluateLanding();
                }
            }
            else
            {
                _groundNormal = skiLandingNormal;
                _lastGroundedTime = Time.time;
                _movementMode = MovementMode.Skiing;
            }
        }

        // If the body cast is not near ground but skis ARE contacting, keep the physics plane fresh
        // from the ski contacts. This prevents "stale ground normal" -> tangential gravity misprojection
        // -> micro hops and rocking on steepening slopes.
        if (!nearGround && hasStableSkiSupport)
        {
            Vector3 n = Vector3.zero;

            if (skiFallbackGrounded)
                n = skiFallback.normal;
            else if (haveRideableSkiContact)
                n = skiLandingNormal;

            if (n.sqrMagnitude > 0.0001f)
            {
                // Smooth toward the ski-derived plane so we don't introduce jitter.
                float t = 1f - Mathf.Exp(-alignNormalSmoothSpeed * Time.fixedDeltaTime);
                _groundNormal = Vector3.Slerp(_groundNormal, n.normalized, t);
            }

            if (hasStableSkiSupport)
            {
                _lastGroundedTime = Time.time;
                if (skiFallbackGrounded)
                {
                    _lastMeasuredGroundGap = skiFallback.gap;
                    _lastGroundRefreshSource = skiFallback.source;
                }
                else
                {
                    _lastGroundRefreshSource = "ski-stable-support";
                }
            }
        }

    }

    private bool IsSkiContactPlausibleForBodyHeight()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckHeight;

        float maxDistance =
            groundCheckHeight +
            Mathf.Max(groundCheckDistance, maxSkiOnlyGroundedBodyGap);

        if (!TryGetBestGroundHit(origin, groundCheckRadius, Vector3.down, maxDistance, out RaycastHit hit))
        {
            _lastMeasuredGroundGap = float.PositiveInfinity;
            return false;
        }

        float bodyGap = Mathf.Max(0f, hit.distance - groundCheckHeight);
        _lastMeasuredGroundGap = bodyGap;

        if (bodyGap > maxSkiOnlyGroundedBodyGap)
        {
            return false;
        }

        _lastGroundRefreshSource = "ski-plausible";
        return true;
    }

    private bool TryGetSkiGroundFallback(out SkiGroundFallbackResult result)
    {
        result = default;
        result.gap = float.PositiveInfinity;
        result.normal = Vector3.up;
        result.source = "none";
        _lastSkiFallbackFreshHit = false;
        _lastSkiFallbackSource = "none";

        if (!useSkiTransformGroundFallback)
            return false;

        bool found = false;
        found |= TryConsiderSkiGroundFallbackPoint(GetSkiProbePoint(leftSkiContact, leftSki, SkiContact.SkiProbeRegion.Mid), "left-mid", false, 0, ref result);
        found |= TryConsiderSkiGroundFallbackPoint(GetSkiProbePoint(rightSkiContact, rightSki, SkiContact.SkiProbeRegion.Mid), "right-mid", false, 0, ref result);
        found |= TryConsiderSkiGroundFallbackPoint(GetSkiProbePoint(leftSkiContact, leftSki, SkiContact.SkiProbeRegion.Front), "left-tip", true, 1, ref result);
        found |= TryConsiderSkiGroundFallbackPoint(GetSkiProbePoint(rightSkiContact, rightSki, SkiContact.SkiProbeRegion.Front), "right-tip", true, 1, ref result);
        found |= TryConsiderSkiGroundFallbackPoint(GetSkiProbePoint(leftSkiContact, leftSki, SkiContact.SkiProbeRegion.Rear), "left-tail", true, -1, ref result);
        found |= TryConsiderSkiGroundFallbackPoint(GetSkiProbePoint(rightSkiContact, rightSki, SkiContact.SkiProbeRegion.Rear), "right-tail", true, -1, ref result);
        if (leftSkiContact != null && leftSkiContact.UseBottomEndProbes)
        {
            found |= TryConsiderSkiGroundFallbackPoint(leftSkiContact.GetBottomEndProbeWorldPosition(SkiContact.SkiProbeRegion.Front), "left-tip-bottom", true, 1, ref result);
            found |= TryConsiderSkiGroundFallbackPoint(leftSkiContact.GetBottomEndProbeWorldPosition(SkiContact.SkiProbeRegion.Rear), "left-tail-bottom", true, -1, ref result);
        }
        if (rightSkiContact != null && rightSkiContact.UseBottomEndProbes)
        {
            found |= TryConsiderSkiGroundFallbackPoint(rightSkiContact.GetBottomEndProbeWorldPosition(SkiContact.SkiProbeRegion.Front), "right-tip-bottom", true, 1, ref result);
            found |= TryConsiderSkiGroundFallbackPoint(rightSkiContact.GetBottomEndProbeWorldPosition(SkiContact.SkiProbeRegion.Rear), "right-tail-bottom", true, -1, ref result);
        }

        if (found)
        {
            _lastSkiTransformFallbackTime = Time.time;
            _lastSkiTransformFallbackNormal = result.normal;
            _lastSkiTransformFallbackGap = result.gap;
            _lastGroundProbeAcceptedCollider = result.collider;
            _lastGroundProbeAcceptedDistance = result.hitDistance;
            _lastGroundProbeAcceptedNormal = result.normal;
            _lastGroundProbeIgnoredReason = "accepted";
            _lastSkiFallbackFreshHit = true;
            _lastSkiFallbackSource = result.source;
            _lastSkiFallbackFreshTime = Time.time;
            return true;
        }

        if ((Time.time - _lastSkiTransformFallbackTime) <= skiTransformFallbackCoyoteTime)
        {
            result.found = true;
            result.freshHit = false;
            result.source = "coyote";
            result.normal = _lastSkiTransformFallbackNormal.sqrMagnitude > 0.0001f
                ? _lastSkiTransformFallbackNormal.normalized
                : Vector3.up;
            result.gap = _lastSkiTransformFallbackGap;
            _lastSkiFallbackSource = "coyote";
            return result.gap <= skiTransformFallbackMaxGap;
        }

        return false;
    }

    private Vector3 GetSkiProbePoint(SkiContact contact, Transform fallbackTransform, SkiContact.SkiProbeRegion region)
    {
        if (contact != null)
            return contact.GetProbeWorldPosition(region);

        return fallbackTransform != null ? fallbackTransform.position : transform.position;
    }

    private bool TryConsiderSkiGroundFallbackPoint(
        Vector3 probePoint,
        string source,
        bool isEndContact,
        int endSign,
        ref SkiGroundFallbackResult best)
    {
        float probeUp = Mathf.Max(0.01f, isEndContact ? skiEndFallbackProbeUp : skiTransformFallbackProbeUp);
        float probeDown = Mathf.Max(0.01f, isEndContact ? skiEndFallbackProbeDown : skiTransformFallbackProbeDown);
        float radius = Mathf.Max(0.001f, isEndContact ? skiEndFallbackRadius : skiTransformFallbackRadius);
        float maxGap = Mathf.Max(0f, isEndContact ? skiEndFallbackMaxGap : skiTransformFallbackMaxGap);
        float maxDistance = probeUp + probeDown;

        Vector3 origin = probePoint + Vector3.up * probeUp;
        if (!TryGetBestGroundHit(origin, radius, Vector3.down, maxDistance, out RaycastHit hit))
            return false;

        float gap = Mathf.Max(0f, Vector3.Dot(probePoint - hit.point, Vector3.up));
        if (gap > maxGap || gap >= best.gap)
            return false;

        best.found = true;
        best.freshHit = true;
        best.point = hit.point;
        best.normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        best.gap = gap;
        best.collider = hit.collider;
        best.isEndContact = isEndContact;
        best.endSign = endSign;
        best.source = source;
        best.hitDistance = hit.distance;
        return true;
    }

    private bool BeginEndContactFlattenRecovery(Vector3 contactNormal, int endSign, string source)
    {
        if (!CanStartEndContactFlattenRecovery(contactNormal))
            return false;

        Vector3 n;
        float bestDistance;
        int candidateSign;

        if (TryGetEndContactFlattenRecoveryCandidate(out n, out bestDistance, out candidateSign))
        {
            endSign = candidateSign;
            _lastEndContactFlattenRecoveryProbeDistance = bestDistance;
        }
        else
        {
            n = contactNormal.sqrMagnitude > 0.0001f
                ? contactNormal.normalized
                : (_groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up);
        }

        _endContactFlattenRecoveryStartedAt = Time.time;
        _endContactFlattenRecoveryUntil = Time.time + Mathf.Max(0.02f, endContactFlattenRecoveryDuration);
        _endContactFlattenRecoveryNormal = n;
        _endContactFlattenRecoverySign = endSign;

        _skiEndSlideState = SkiEndSlideState.None;
        _skiEndSlide01 = 0f;

        _groundNormal = n;
        _alignNormal = n;

        _landingAssistUntil = Time.time + Mathf.Max(landingAlignBoostDuration, endContactFlattenRecoveryDuration);
        _lastGroundRefreshSource = source;
        _lastNoseTailLandingSource = $"{source}-flatten-recovery";

        // This is intentionally NOT a grounded state.
        // Do not update _lastGroundedTime, _lastGroundedFrom, or _movementMode here.

        _airAngularVelocity = Vector3.MoveTowards(
            _airAngularVelocity,
            Vector3.zero,
            Mathf.Max(0f, endContactFlattenAngularDamp) * Time.fixedDeltaTime);

        PreserveLandingVelocityOnSlope(
            Mathf.Clamp01(landingProjectionStrength * endContactFlattenRecoveryProjection));

        return true;
    }

    private void ApplyEndContactFlattenRecovery(float dt)
    {
        ValidateEndContactFlattenRecoveryState();

        if (!IsEndContactFlattenRecoveryActive || _rb == null)
            return;

        Vector3 n = _endContactFlattenRecoveryNormal.sqrMagnitude > 0.0001f
            ? _endContactFlattenRecoveryNormal.normalized
            : (_groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up);

        _groundNormal = Vector3.Slerp(_groundNormal, n, 1f - Mathf.Exp(-alignNormalSmoothSpeed * dt));
        _alignNormal = Vector3.Slerp(_alignNormal, n, 1f - Mathf.Exp(-alignNormalSmoothSpeed * dt));

        // If the player is charging/releasing jump, do not apply corrective force.
        if (HasPendingJumpIntent)
        {
            _lastGroundRefreshSource = "end-contact-flatten-recovery-jump-ready";
            return;
        }

        float awaySpeed = Vector3.Dot(_rb.linearVelocity, n);
        if (awaySpeed > 0f && endContactFlattenRecoveryAwayDamp > 0f)
        {
            _rb.AddForce(-n * awaySpeed * endContactFlattenRecoveryAwayDamp, ForceMode.Acceleration);
        }

        if (endContactFlattenRecoveryDownAccel > 0f)
        {
            _rb.AddForce(-n * endContactFlattenRecoveryDownAccel, ForceMode.Acceleration);
        }

        if (endContactFlattenRecoveryAngularDamp > 0f)
        {
            _rb.angularVelocity = Vector3.MoveTowards(
                _rb.angularVelocity,
                Vector3.zero,
                endContactFlattenRecoveryAngularDamp * dt);
        }

        _airAngularVelocity = Vector3.MoveTowards(
            _airAngularVelocity,
            Vector3.zero,
            Mathf.Max(0f, endContactFlattenAngularDamp) * dt);

        _lastGroundRefreshSource = "end-contact-flatten-recovery-corrective";
    }

    private bool TryStartNoseTailLandingFromFallback(SkiGroundFallbackResult fallback)
    {
        if (!fallback.found || !fallback.isEndContact || fallback.endSign == 0)
            return false;

        if (!IsRideableNormal(fallback.normal))
            return false;

        Vector3 n = fallback.normal.sqrMagnitude > 0.0001f ? fallback.normal.normalized : Vector3.up;
        float planarSpeed = _rb != null ? Vector3.ProjectOnPlane(_rb.linearVelocity, n).magnitude : 0f;

        if (planarSpeed <= noseTailSlideFlattenBelowSpeed)
        {
            return BeginEndContactFlattenRecovery(n, fallback.endSign, $"{fallback.source}-low-speed");
        }

        if (!enableNoseTailSlides || planarSpeed < noseTailSlideMinSpeed)
        {
            return false;
        }

        float leanMatch = Mathf.InverseLerp(noseTailSlideLeanThreshold, 1f, _forwardLean * fallback.endSign);
        if (leanMatch <= 0f)
        {
            return false;
        }

        float speed01 = Mathf.InverseLerp(
            noseTailSlideMinSpeed,
            Mathf.Max(noseTailSlideMinSpeed + 0.01f, noseTailSlideFullSpeed),
            planarSpeed);

        bool left = fallback.source.StartsWith("left", System.StringComparison.Ordinal);
        _skiEndSlideState = left
            ? (fallback.endSign > 0 ? SkiEndSlideState.LeftNose : SkiEndSlideState.LeftTail)
            : (fallback.endSign > 0 ? SkiEndSlideState.RightNose : SkiEndSlideState.RightTail);

        _skiEndSlide01 = Mathf.Clamp01(speed01 * leanMatch);
        _lastSkiEndSlideTime = Time.time;
        _landingAssistUntil = Time.time + landingAlignBoostDuration;
        _lastGroundRefreshSource = fallback.source;
        _lastNoseTailLandingSource = fallback.source;
        _lastGroundedFrom = "explicit-slide";
        _movementMode = MovementMode.Landing;

        PreserveLandingVelocityOnSlope(Mathf.Clamp01(landingProjectionStrength * 0.75f));
        return true;
    }

    private bool TryStartNoseTailLandingFromSkiContacts()
    {
        bool leftValid = TryGetSkiEndSlide(leftSkiContact, out int leftEndSign, out _);
        bool rightValid = TryGetSkiEndSlide(rightSkiContact, out int rightEndSign, out _);

        if (!leftValid && !rightValid)
            return false;

        int signSum = (leftValid ? leftEndSign : 0) + (rightValid ? rightEndSign : 0);

        if (leftValid && rightValid && signSum == 0)
        {
            _skiEndSlideState = SkiEndSlideState.Mixed;
            _skiEndSlide01 = 0f;
            _lastNoseTailLandingSource = "mixed-end-contact";
            return false;
        }

        int dominantEndSign = signSum >= 0 ? 1 : -1;

        Vector3 n = Vector3.zero;
        if (leftValid && leftSkiContact != null && leftSkiContact.ContactNormal.sqrMagnitude > 0.0001f)
            n += leftSkiContact.ContactNormal.normalized;
        if (rightValid && rightSkiContact != null && rightSkiContact.ContactNormal.sqrMagnitude > 0.0001f)
            n += rightSkiContact.ContactNormal.normalized;

        if (n.sqrMagnitude <= 0.0001f)
            n = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
        else
            n.Normalize();

        SkiContact sourceContact = leftValid ? leftSkiContact : rightSkiContact;
        string source = GetSkiEndContactSource(sourceContact);

        float planarSpeed = _rb != null ? Vector3.ProjectOnPlane(_rb.linearVelocity, n).magnitude : 0f;

        if (planarSpeed <= noseTailSlideFlattenBelowSpeed)
        {
            return BeginEndContactFlattenRecovery(n, dominantEndSign, $"{source}-low-speed");
        }

        if (!enableNoseTailSlides || planarSpeed < noseTailSlideMinSpeed)
        {
            return false;
        }

        float leanMatch = Mathf.InverseLerp(noseTailSlideLeanThreshold, 1f, _forwardLean * dominantEndSign);
        if (leanMatch <= 0f)
        {
            return false;
        }

        _skiEndSlideState = ResolveSkiEndSlideState(leftValid, rightValid, leftEndSign, rightEndSign);
        _skiEndSlide01 = Mathf.Clamp01(
            Mathf.InverseLerp(
                noseTailSlideMinSpeed,
                Mathf.Max(noseTailSlideMinSpeed + 0.01f, noseTailSlideFullSpeed),
                planarSpeed) * leanMatch);

        if (_skiEndSlideState == SkiEndSlideState.Mixed)
            return false;

        _lastSkiEndSlideTime = Time.time;
        _landingAssistUntil = Time.time + landingAlignBoostDuration;
        _lastNoseTailLandingSource = source;
        _lastGroundedFrom = "explicit-slide";
        _movementMode = MovementMode.Landing;

        PreserveLandingVelocityOnSlope(Mathf.Clamp01(landingProjectionStrength * 0.75f));
        return true;
    }

    private static string GetSkiEndContactSource(SkiContact contact)
    {
        if (contact == null)
            return "none";

        if (contact.HasCollisionEndContact)
            return "collision-end-contact";

        if (contact.ProbeTipHit || contact.ProbeTailHit)
            return "probe-end-contact";

        return "ski-end-contact";
    }

    private void UpdateStackedGroundingOnly(
    bool haveSkiContact,
    bool haveRideableSkiContact,
    Vector3 skiLandingNormal)
    {
        bool nearGround = false;
        Vector3 rawNormal = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        Vector3 centerOrigin = transform.position + Vector3.up * groundCheckHeight;
        float maxDist = groundCheckHeight + groundCheckDistance;

        if (TryGetBestGroundHit(centerOrigin, groundCheckRadius, Vector3.down, maxDist, out RaycastHit centerHit))
        {
            float contactGap = Mathf.Max(0f, centerHit.distance - groundCheckHeight);
            _lastMeasuredGroundGap = contactGap;
            float allowedGap = haveSkiContact
                ? groundContactDistanceWhenSkiContact
                : groundContactDistance;

            if (contactGap <= allowedGap && IsRideableNormal(centerHit.normal))
            {
                nearGround = true;
                rawNormal = centerHit.normal.normalized;
                _lastGroundedTime = Time.time;
                _lastGroundRefreshSource = "stacked-body-ground";
            }
        }
        else
        {
            _lastMeasuredGroundGap = float.PositiveInfinity;
        }

        _nearGroundForJump = nearGround;
        _bodyNearGround = nearGround;
        _skiContactPlausibleForBody = haveSkiContact && IsSkiContactPlausibleForBodyHeight();
        _isGrounded = nearGround || _skiContactPlausibleForBody;

        if (nearGround)
        {
            if (_groundNormal.sqrMagnitude < 0.0001f)
            {
                _groundNormal = rawNormal;
            }
            else
            {
                float lerp = 1f - Mathf.Exp(-groundNormalSmoothSpeed * Time.fixedDeltaTime);
                _groundNormal = Vector3.Slerp(_groundNormal, rawNormal, lerp);
            }
        }
        else if (_skiContactPlausibleForBody && haveRideableSkiContact && skiLandingNormal.sqrMagnitude > 0.0001f)
        {
            float lerp = 1f - Mathf.Exp(-alignNormalSmoothSpeed * Time.fixedDeltaTime);
            _groundNormal = Vector3.Slerp(_groundNormal, skiLandingNormal.normalized, lerp);
            _lastGroundedTime = Time.time;
            _lastGroundRefreshSource = "stacked-ski-plausible";
        }
    }

    private bool TryGetRideableSkiLandingNormal(out Vector3 normal)
    {
        normal = Vector3.zero;

        if (leftSkiContact != null &&
            (leftSkiContact.HasStableSupportContact || leftSkiContact.HasEndContact) &&
            IsRideableNormal(leftSkiContact.ContactNormal) &&
            (leftSkiContact.HasEndContact ||
             leftSkiContact.BaseContactAlignment >= minBaseAlignForGroundNormal ||
             TryGetSkiEndSlide(leftSkiContact, out _, out _)))
        {
            normal += leftSkiContact.ContactNormal;
        }

        if (rightSkiContact != null &&
            (rightSkiContact.HasStableSupportContact || rightSkiContact.HasEndContact) &&
            IsRideableNormal(rightSkiContact.ContactNormal) &&
            (rightSkiContact.HasEndContact ||
             rightSkiContact.BaseContactAlignment >= minBaseAlignForGroundNormal ||
             TryGetSkiEndSlide(rightSkiContact, out _, out _)))
        {
            normal += rightSkiContact.ContactNormal;
        }

        if (normal.sqrMagnitude <= 0.0001f)
            return false;

        normal.Normalize();
        return true;
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

        // Grounded/ski-plausible contact should never be demoted into airborne wall scrape.
        // Uneven terrain can produce a steep contact on one ski while the body or other ski is still rideable.
        if (_isGrounded || _bodyNearGround || _skiContactPlausibleForBody)
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

            if (TryGetBestGroundHit(origin, 0.01f, -up, dist, out RaycastHit hit))
            {
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

        // Prefer ground normal when grounded; use world up for approximate near-ground/probe support
        // so stale slope normals do not lift sideways while recovering clearance.
        Vector3 up = (_isGrounded && _groundNormal.sqrMagnitude > 0.0001f)
            ? _groundNormal.normalized
            : Vector3.up;

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
        bool hasLandingEventSupport =
            _isGrounded ||
            HasAnySkiCollisionContact ||
            _hasNonSkiGroundContact ||
            IsEndContactFlattenRecoveryActive ||
            _skiEndSlideState != SkiEndSlideState.None;

        // Do not repeatedly evaluate/flicker landing while only speculative probes are nearby.
        if (!hasLandingEventSupport)
            return;

        // Landing is a transition event. It should not fire every FixedUpdate while
        // we hover/coyote/recover toward true contact.
        if (_landingEvaluationConsumedForCurrentAirborne)
            return;

        _landingEvaluationConsumedForCurrentAirborne = true;

        float now = Time.time;
        float airTime = Mathf.Max(0f, now - _airborneStartTime);

        Vector3 vel = _rb.linearVelocity;

        Vector3 groundNormal = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

        // Positive when moving into the slope.
        float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(vel, groundNormal));

        bool anyEndContact =
            (leftSkiContact != null && leftSkiContact.HasTipContact) ||
            (rightSkiContact != null && rightSkiContact.HasTipContact);

        float tipDigAngle = anyEndContact ? ComputeTipDigAngle() : 0f;

        Vector3 velOnPlane = Vector3.ProjectOnPlane(vel, groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        float tiltAngle = Vector3.Angle(transform.up, groundNormal);

        // 0 = perfectly along the slope plane, 90 = straight into the slope.
        float impactAngleFromPlane = Mathf.Atan2(
            downwardSpeed,
            Mathf.Max(0.0001f, planarSpeed)) * Mathf.Rad2Deg;

        // Use RB position for physics consistency.
        float fallHeight = Mathf.Max(0f, _airbornePeakY - _rb.position.y);

        TriggerSkiLandingVisual(airTime, fallHeight, downwardSpeed);

        // Facing / travel misalignment on the slope plane.
        float misalignAngle = 0f;
        if (planarSpeed > 0.1f)
        {
            Vector3 forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            if (forwardOnPlane.sqrMagnitude > 0.0001f)
            {
                forwardOnPlane.Normalize();
                Vector3 velDir = velOnPlane.normalized;

                float rawAngle = Vector3.Angle(forwardOnPlane, velDir);

                // Treat 180 as equivalent to 0 so only sideways landings are really punished.
                misalignAngle = Mathf.Min(rawAngle, 180f - rawAngle);
            }
        }

        bool isTinyLanding =
            airTime < minLandingAirTime ||
            downwardSpeed < minLandingDownwardSpeed;

        // Debug cache
        _dbgLandingAirTime = airTime;
        _dbgLandingDownwardSpeed = downwardSpeed;
        _dbgLandingPlanarSpeed = planarSpeed;
        _dbgLandingImpactAngleFromPlane = impactAngleFromPlane;
        _dbgLandingFallHeight = fallHeight;
        _dbgLandingMisalignAngle = misalignAngle;
        _dbgLandingIsTiny = isTinyLanding;
        _dbgLandingIsHardSlam = false;

        // Tiny hops should always feel forgiving.
        if (isTinyLanding)
        {
            float microProjection = Mathf.Clamp01(landingProjectionStrength * 0.5f);
            PreserveLandingVelocityOnSlope(microProjection);
            _landingAssistUntil = Time.time + landingAlignBoostDuration;
            _movementMode = MovementMode.Skiing;
            _dbgLastStackReason = "Tiny Landing";
            return;
        }

        // ------------------------------------------------------------------
        // Severity model:
        // Bigger falls + faster into-slope speed + steeper impact angle
        // all make the landing less forgiving.
        // ------------------------------------------------------------------
        float fallSeverity = Mathf.InverseLerp(
            Mathf.Max(0.75f, hardLandingMinFallHeight * 0.35f),
            hardLandingMinFallHeight * 2.25f,
            fallHeight);

        float downSeverity = Mathf.InverseLerp(
            Mathf.Max(minLandingDownwardSpeed, hardLandingMinDownwardSpeed * 0.40f),
            hardLandingMinDownwardSpeed * 2.0f,
            downwardSpeed);

        float impactSeverity = Mathf.InverseLerp(
            Mathf.Max(10f, hardLandingMinImpactAngleFromPlane * 0.35f),
            90f,
            impactAngleFromPlane);

        float landingSeverity = Mathf.Clamp01(
            fallSeverity * 0.40f +
            downSeverity * 0.35f +
            impactSeverity * 0.25f);

        // Keep the old guaranteed "splat" path for truly brutal impacts.
        bool isHardSlam =
            fallHeight >= hardLandingMinFallHeight &&
            downwardSpeed >= hardLandingMinDownwardSpeed &&
            impactAngleFromPlane >= hardLandingMinImpactAngleFromPlane;

        _dbgLandingIsHardSlam = isHardSlam;

        if (isHardSlam)
        {
            _dbgLastStackReason = "HardSlam";

            float sFall = Mathf.InverseLerp(
                hardLandingMinFallHeight,
                hardLandingMinFallHeight * 2.5f,
                fallHeight);

            float sDown = Mathf.InverseLerp(
                hardLandingMinDownwardSpeed,
                hardLandingMinDownwardSpeed * 2.5f,
                downwardSpeed);

            float sAng = Mathf.InverseLerp(
                hardLandingMinImpactAngleFromPlane,
                90f,
                impactAngleFromPlane);

            float sevHardSlam = Mathf.Clamp01(
                Mathf.Max(sFall, sDown) * 0.7f +
                sAng * 0.3f);

            TriggerStack(sevHardSlam, ComputeStackTorqueAxisFromContacts());
            return;
        }

        // ------------------------------------------------------------------
        // Slope-aware limits.
        // Steeper slopes loosen tilt / sideways constraints a bit,
        // but big drops tighten them back up.
        // ------------------------------------------------------------------
        float steepT = Mathf.Clamp01(
            Mathf.InverseLerp(
                landingSteepSlopeStartAngle,
                landingVerySteepSlopeAngle,
                slopeAngle));

        float tiltLimit = maxLandingTiltAngle + 15f * steepT;
        float misalignLimit = maxLandingMisalignmentAngle + 15f * steepT;

        // Harder impacts require cleaner landings.
        tiltLimit -= Mathf.Lerp(0f, 18f, landingSeverity) * Mathf.Lerp(1f, 0.5f, steepT);
        misalignLimit -= Mathf.Lerp(0f, 35f, landingSeverity) * Mathf.Lerp(1f, 0.65f, steepT);

        tiltLimit = Mathf.Max(12f, tiltLimit);
        misalignLimit = Mathf.Max(15f, misalignLimit);

        // Also tighten how "flat" the impact must be as severity rises.
        float impactLimit = Mathf.Lerp(
            hardLandingMinImpactAngleFromPlane + 8f,
            Mathf.Max(18f, hardLandingMinImpactAngleFromPlane - 18f),
            landingSeverity);

        float noseLimit = Mathf.Lerp(
            heavyTipStackAngle + 6f,
            heavyTipStackAngle,
            landingSeverity);

        bool tooTilted = tiltAngle > tiltLimit;
        bool tooMisaligned =
            planarSpeed > minLandingSpeedForStackCheck &&
            misalignAngle > misalignLimit;
        bool tooNoseDown = tipDigAngle > noseLimit;
        bool tooSteepImpact =
            landingSeverity > 0.20f &&
            impactAngleFromPlane > impactLimit;

        if (tooTilted || tooMisaligned || tooNoseDown || tooSteepImpact)
        {
            float sTilt = Mathf.InverseLerp(tiltLimit, tiltLimit + 60f, tiltAngle);
            float sMis = Mathf.InverseLerp(misalignLimit, misalignLimit + 90f, misalignAngle);
            float sNose = Mathf.InverseLerp(noseLimit, noseLimit + 45f, tipDigAngle);
            float sImpact = Mathf.InverseLerp(impactLimit, 90f, impactAngleFromPlane);
            float sSpeed = Mathf.InverseLerp(minLandingSpeedForStackCheck, minLandingSpeedForStackCheck + 12f, planarSpeed);

            float sev = Mathf.Clamp01(
                Mathf.Max(sTilt, sMis, sNose, sImpact) * 0.8f +
                sSpeed * 0.1f +
                landingSeverity * 0.1f);

            if (tooNoseDown)
                _dbgLastStackReason = "Tip Dig";
            else if (tooSteepImpact)
                _dbgLastStackReason = "Hard Landing";
            else
                _dbgLastStackReason = "Bad Landing";

            TriggerStack(sev, ComputeStackTorqueAxisFromContacts());
            return;
        }

        // Safe landing: stronger projection on rougher successful landings.
        float baseProjection = Mathf.Clamp01(landingProjectionStrength);
        float projectionOnSteep = Mathf.Lerp(baseProjection, baseProjection * 0.75f, steepT);
        float projection = Mathf.Clamp01(Mathf.Lerp(projectionOnSteep * 0.75f, projectionOnSteep, landingSeverity));

        PreserveLandingVelocityOnSlope(projection);
        _landingAssistUntil = Time.time + landingAlignBoostDuration;
        _movementMode = MovementMode.Skiing;
        _dbgLastStackReason = "Landed";
    }

    private void TriggerSkiLandingVisual(float airTime, float fallHeight, float downwardSpeed)
    {
        float fall01 = Mathf.InverseLerp(
            skiLandingVisualMinFallHeight,
            Mathf.Max(skiLandingVisualMinFallHeight + 0.01f, skiLandingVisualMaxFallHeight),
            fallHeight);

        float speed01 = Mathf.InverseLerp(
            skiLandingVisualMinDownwardSpeed,
            Mathf.Max(skiLandingVisualMinDownwardSpeed + 0.01f, skiLandingVisualMaxDownwardSpeed),
            downwardSpeed);

        float intensity01 = Mathf.Clamp01(Mathf.Max(fall01, speed01));

        if (intensity01 < skiLandingVisualMinVisibleIntensity)
        {
            if (logSkiLandingVisualDebug)
            {
                Debug.Log(
                    $"[{nameof(SkiController)}] Ski landing visual ignored. " +
                    $"airTime={airTime:0.00}, fallHeight={fallHeight:0.00}, " +
                    $"downSpeed={downwardSpeed:0.00}, intensity={intensity01:0.00}",
                    this);
            }

            return;
        }

        _lastSkiLandingVisualIntensity01 = intensity01;
        _lastSkiLandingVisualTime = Time.time;

        if (logSkiLandingVisualDebug)
        {
            Debug.Log(
                $"[{nameof(SkiController)}] Ski landing visual fired. " +
                $"airTime={airTime:0.00}, fallHeight={fallHeight:0.00}, " +
                $"downSpeed={downwardSpeed:0.00}, intensity={intensity01:0.00}",
                this);
        }
    }
    private void UpdateAirborneLandingDebug()
    {
        Vector3 groundNormal = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        Vector3 vel = _rb.linearVelocity;

        float airTime = Mathf.Max(0f, Time.time - _airborneStartTime);
        float downwardSpeed = Mathf.Max(0f, -Vector3.Dot(vel, groundNormal));

        Vector3 velOnPlane = Vector3.ProjectOnPlane(vel, groundNormal);
        float planarSpeed = velOnPlane.magnitude;

        float impactAngleFromPlane = Mathf.Atan2(
            downwardSpeed,
            Mathf.Max(0.0001f, planarSpeed)) * Mathf.Rad2Deg;

        float fallHeight = Mathf.Max(0f, _airbornePeakY - _rb.position.y);

        float misalignAngle = 0f;
        if (planarSpeed > 0.1f)
        {
            Vector3 forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, groundNormal);
            if (forwardOnPlane.sqrMagnitude > 0.0001f)
            {
                forwardOnPlane.Normalize();
                Vector3 velDir = velOnPlane.normalized;
                float rawAngle = Vector3.Angle(forwardOnPlane, velDir);
                misalignAngle = Mathf.Min(rawAngle, 180f - rawAngle);
            }
        }

        _dbgLandingAirTime = airTime;
        _dbgLandingDownwardSpeed = downwardSpeed;
        _dbgLandingPlanarSpeed = planarSpeed;
        _dbgLandingImpactAngleFromPlane = impactAngleFromPlane;
        _dbgLandingFallHeight = fallHeight;
        _dbgLandingMisalignAngle = misalignAngle;

        bool isTinyLanding =
            airTime < minLandingAirTime ||
            downwardSpeed < minLandingDownwardSpeed;

        _dbgLandingIsTiny = isTinyLanding;

        _dbgLandingIsHardSlam =
            fallHeight >= hardLandingMinFallHeight &&
            downwardSpeed >= hardLandingMinDownwardSpeed &&
            impactAngleFromPlane >= hardLandingMinImpactAngleFromPlane;
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
        Vector3 preStackVelocity = _hasPendingStackImpact && _pendingStackImpactVelocity.sqrMagnitude > 0.0001f
            ? _pendingStackImpactVelocity
            : _rb.linearVelocity;

        // Apply soreness impact before we freeze state.
        if (sorenessMeter != null)
            sorenessMeter.AddImpact(severity01);

        _stacked = true;
        _stackRecoveryExternallyLocked = false;
        _stackedAtTime = Time.time;
        _rb.freezeRotation = false;

        SetStackEquipmentCollidersSuppressed(true);

        // When we stack, we want a decisive fall rather than lingering in a half-rotated state.
        _rb.angularVelocity = Vector3.zero;

        // Prevent hard impacts from forcing the rigidbody through the terrain.
        // Remove any into-ground component and keep a small amount of planar velocity so the wipeout feels natural.
        {
            Vector3 n = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
            Vector3 v = _rb.linearVelocity;
            Vector3 p0 = _hasPendingStackImpact
                ? _pendingStackImpactPoint
                : (_hasNonSkiGroundContact ? _nonSkiGroundContactPoint : transform.position);

            if (_hasPendingStackImpact && _pendingStackImpactNormal.sqrMagnitude > 0.0001f)
            {
                Vector3 impactNormal = _pendingStackImpactNormal.normalized;
                float intoObstacle = Vector3.Dot(v, impactNormal);
                if (intoObstacle < 0f)
                    v -= impactNormal * intoObstacle;

                Vector3 slide = Vector3.ProjectOnPlane(v, impactNormal);
                float retain = Mathf.Clamp01(stackImpactSlideRetention);
                float minRetain = slide.magnitude > 2f ? 0.18f : 0f;
                v = slide * Mathf.Max(retain, minRetain);
                v += impactNormal * (Mathf.Abs(intoObstacle) * Mathf.Clamp01(stackImpactBounce));
                v = ClampUpwardVelocity(v, stackImpactMaxUpwardVelocity);
            }
            else
            {
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
            }

            _rb.linearVelocity = v;
            OnStacked?.Invoke(new StackEventInfo(p0, preStackVelocity, severity01, _dbgLastStackReason));
        }

        if (torqueAxisWorld.sqrMagnitude < 0.0001f)
            torqueAxisWorld = Random.onUnitSphere;

        _rb.AddTorque(torqueAxisWorld.normalized * (stackTorqueImpulse * Mathf.Lerp(0.75f, 1.2f, severity01)), ForceMode.Impulse);
        _hasPendingStackImpact = false;
        _pendingStackImpactNormal = Vector3.zero;
        _pendingStackImpactVelocity = Vector3.zero;
        _pendingStackImpactPoint = Vector3.zero;
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

        float stackTiltThreshold = maxLandingTiltAngle;
        if (HasExternalInputSource)
            stackTiltThreshold += 18f; // NPCs need a wider tolerance before body graze = wipeout

        float planarSpeed = Vector3.ProjectOnPlane(_rb.linearVelocity, n).magnitude;
        bool meaningfulImpact = planarSpeed > 2.0f || _nonSkiGroundContactUpDot > 0.55f;

        if (tilt > stackTiltThreshold && meaningfulImpact)
        {
            _groundNormal = n;
            TriggerStack(ComputeStackTorqueAxisFromPoint(_nonSkiGroundContactPoint));
        }

        // Keep the cache through this physics step so CheckGround can consume
        // body/root collision as stable support. Expire it if callbacks stop.
        if (Time.time - _lastNonSkiGroundContactTime > Time.fixedDeltaTime * 2.5f)
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

    private Vector3 ComputeImpactStackTorqueAxis(Vector3 impactPointWorld, Vector3 impactNormalWorld, Vector3 incomingVelocityWorld)
    {
        Vector3 pointAxis = ComputeStackTorqueAxisFromPoint(impactPointWorld);
        Vector3 impactNormal = impactNormalWorld.sqrMagnitude > 0.0001f ? impactNormalWorld.normalized : Vector3.zero;
        Vector3 velocityAxis = Vector3.Cross(Vector3.up, -incomingVelocityWorld);
        Vector3 normalAxis = Vector3.Cross(Vector3.up, impactNormal);

        Vector3 axis = pointAxis;
        if (normalAxis.sqrMagnitude > 0.0001f)
            axis += normalAxis.normalized * 0.6f;
        if (velocityAxis.sqrMagnitude > 0.0001f)
            axis += velocityAxis.normalized * 0.4f;

        if (axis.sqrMagnitude < 0.0001f)
            axis = pointAxis.sqrMagnitude > 0.0001f ? pointAxis : transform.right;

        return axis.normalized;
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
        if (_stackRecoveryExternallyLocked)
            return;

        if (!EvaluateStackRecoveryNow(
                requireMinimumDelay: true,
                out Vector3 forwardHint,
                out _,
                out _,
                out _))
        {
            return;
        }

        RecoverFromStack(forwardHint);
    }

    private bool EvaluateStackRecoveryNow(
        bool requireMinimumDelay,
        out Vector3 forwardHint,
        out float tiltAngle,
        out bool skisReady,
        out bool delayReady)
    {
        forwardHint = Vector3.zero;
        tiltAngle = 180f;
        skisReady = false;
        delayReady = true;

        if (!_stacked || !_isGrounded)
            return false;

        delayReady = !requireMinimumDelay || Time.time >= _stackedAtTime + stackMinimumRecoveryDelay;
        if (!delayReady)
            return false;

        skisReady = AreSkisReadyForStackRecovery();
        if (!skisReady)
            return false;

        Vector3 recoveryNormal = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        tiltAngle = Vector3.Angle(transform.up, recoveryNormal);

        if (tiltAngle > maxLandingTiltAngle)
            return false;

        forwardHint = GetCombinedSkiForwardOnPlane();
        if (forwardHint.sqrMagnitude < 0.0001f)
            forwardHint = Vector3.ProjectOnPlane(transform.forward, recoveryNormal);

        if (forwardHint.sqrMagnitude < 0.0001f)
            forwardHint = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (forwardHint.sqrMagnitude < 0.0001f)
            forwardHint = transform.forward;

        forwardHint.Normalize();
        return true;
    }

    private bool EvaluateStackRecoveryOrientationAgainstNormal(Vector3 normal, out float tiltAngle)
    {
        normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        tiltAngle = Vector3.Angle(transform.up, normal);
        return tiltAngle <= maxLandingTiltAngle;
    }

    private bool AreSkisReadyForStackRecovery()
    {
        bool leftReady = IsSkiReadyForStackRecovery(leftSkiContact);
        bool rightReady = IsSkiReadyForStackRecovery(rightSkiContact);

        return requireBothSkisForStackRecovery
            ? leftReady && rightReady
            : leftReady || rightReady;
    }

    private bool IsSkiReadyForStackRecovery(SkiContact skiContact)
    {
        if (skiContact == null)
            return false;

        if (!skiContact.IsGrounded)
            return false;

        return skiContact.BaseContactAlignment >= stackRecoveryMinSkiBaseAlignment;
    }

    private static Vector3 ClampUpwardVelocity(Vector3 velocity, float maxUpwardVelocity)
    {
        if (velocity.y > maxUpwardVelocity)
            velocity.y = maxUpwardVelocity;
        return velocity;
    }

    private void ApplyStackedTumbleDamping(float dt)
    {
        if (!_stacked || _rb == null || dt <= 0f)
            return;

        float baseDamp = Mathf.Max(0f, stackedAngularDamping);
        if (baseDamp > 0f)
        {
            _rb.angularVelocity = Vector3.Lerp(
                _rb.angularVelocity,
                Vector3.zero,
                1f - Mathf.Exp(-baseDamp * dt));
        }

        bool skiTouching = HasAnySkiContact || _isGrounded;
        if (!skiTouching)
            return;

        Vector3 n = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
        Vector3 angular = _rb.angularVelocity;
        Vector3 spinAroundNormal = Vector3.Project(angular, n);
        Vector3 tumblingInPlane = angular - spinAroundNormal;

        float skiWheelDamp = Mathf.Max(0f, stackedSkiWheelDamping);
        tumblingInPlane = Vector3.Lerp(
            tumblingInPlane,
            Vector3.zero,
            1f - Mathf.Exp(-skiWheelDamp * dt));

        _rb.angularVelocity = spinAroundNormal + tumblingInPlane;
    }

    private void UpdateStackedPhysics(float dt)
    {
        if (!_stacked || _rb == null || dt <= 0f)
            return;

        // Because Rigidbody.useGravity is false, stacked gravity must be explicit.
        // This should be the only continuous acceleration applied while stacked.
        _rb.AddForce(Physics.gravity * Mathf.Max(0f, stackedGravityScale), ForceMode.Acceleration);

        ApplyStackedLinearDamping(dt);
        ApplyStackedTumbleDamping(dt);
    }

    private void ApplyStackedLinearDamping(float dt)
    {
        if (_rb == null || dt <= 0f)
            return;

        Vector3 velocity = _rb.linearVelocity;

        bool grounded = _isGrounded || HasAnySkiContact || _hasNonSkiGroundContact;
        Vector3 normal = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        if (grounded)
        {
            Vector3 planar = Vector3.ProjectOnPlane(velocity, normal);
            Vector3 normalVelocity = Vector3.Project(velocity, normal);

            float planarDamp = 1f - Mathf.Exp(-Mathf.Max(0f, stackedGroundLinearDamping) * dt);
            planar = Vector3.Lerp(planar, Vector3.zero, planarDamp);

            float outwardSpeed = Vector3.Dot(normalVelocity, normal);

            if (outwardSpeed > 0f)
            {
                float bounceDamp = 1f - Mathf.Exp(-Mathf.Max(0f, stackedGroundNormalBounceDamping) * dt);
                normalVelocity = Vector3.Lerp(normalVelocity, Vector3.zero, bounceDamp);

                float maxUp = Mathf.Max(0f, stackedMaxGroundUpwardSpeed);
                float clampedOutwardSpeed = Mathf.Min(Vector3.Dot(normalVelocity, normal), maxUp);
                normalVelocity = normal * clampedOutwardSpeed;
            }

            _rb.linearVelocity = planar + normalVelocity;
        }
        else
        {
            float airDamp = 1f - Mathf.Exp(-Mathf.Max(0f, stackedAirLinearDamping) * dt);
            _rb.linearVelocity = Vector3.Lerp(velocity, Vector3.zero, airDamp);
        }
    }

    private void SetStackEquipmentCollidersSuppressed(bool suppressed)
    {
        if (stackEquipmentColliderMode == StackEquipmentColliderMode.None)
        {
            if (_stackEquipmentCollidersSuppressed)
                RestoreStackEquipmentColliders();

            return;
        }

        if (suppressed == _stackEquipmentCollidersSuppressed)
            return;

        if (suppressed)
            SuppressStackEquipmentColliders();
        else
            RestoreStackEquipmentColliders();
    }

    private void SuppressStackEquipmentColliders()
    {
        _stackEquipmentColliderStates.Clear();

        foreach (Collider collider in EnumerateStackEquipmentColliders())
        {
            if (collider == null)
                continue;

            if (_stackEquipmentColliderStates.ContainsKey(collider))
                continue;

            _stackEquipmentColliderStates.Add(collider, new StackEquipmentColliderState(collider));

            if (stackEquipmentColliderMode == StackEquipmentColliderMode.Disable)
            {
                collider.enabled = false;
                continue;
            }

            if (stackEquipmentColliderMode == StackEquipmentColliderMode.MakeTrigger)
            {
                MeshCollider meshCollider = collider as MeshCollider;
                bool mustDisable =
                    disableNonConvexMeshCollidersInsteadOfTrigger &&
                    meshCollider != null &&
                    !meshCollider.convex;

                if (mustDisable)
                    collider.enabled = false;
                else
                    collider.isTrigger = true;
            }
        }

        _stackEquipmentCollidersSuppressed = true;

        if (leftSkiContact != null)
            leftSkiContact.ResetContactState();

        if (rightSkiContact != null)
            rightSkiContact.ResetContactState();

        Physics.SyncTransforms();
    }

    private void RestoreStackEquipmentColliders()
    {
        foreach (KeyValuePair<Collider, StackEquipmentColliderState> pair in _stackEquipmentColliderStates)
        {
            Collider collider = pair.Key;
            if (collider == null)
                continue;

            collider.enabled = pair.Value.enabled;
            collider.isTrigger = pair.Value.isTrigger;
        }

        _stackEquipmentColliderStates.Clear();
        _stackEquipmentCollidersSuppressed = false;

        Physics.SyncTransforms();

        if (leftSkiContact != null)
        {
            leftSkiContact.ResetContactState();
            leftSkiContact.ManualSampleGround();
        }

        if (rightSkiContact != null)
        {
            rightSkiContact.ResetContactState();
            rightSkiContact.ManualSampleGround();
        }
    }

    private IEnumerable<Collider> EnumerateStackEquipmentColliders()
    {
        if (leftSki != null)
        {
            Collider[] colliders = leftSki.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                yield return colliders[i];
        }

        if (rightSki != null)
        {
            Collider[] colliders = rightSki.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                yield return colliders[i];
        }

        if (!includePoleCollidersInStackCollisionSuppression)
            yield break;

        Transform leftPoleRoot = leftPoleContact != null ? leftPoleContact.PoleRoot : null;
        if (leftPoleRoot != null)
        {
            Collider[] colliders = leftPoleRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                yield return colliders[i];
        }

        Transform rightPoleRoot = rightPoleContact != null ? rightPoleContact.PoleRoot : null;
        if (rightPoleRoot != null)
        {
            Collider[] colliders = rightPoleRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                yield return colliders[i];
        }
    }

    private bool TryUpdateNoseTailSlideState()
    {
        SkiEndSlideState previousState = _skiEndSlideState;
        float previousStrength = _skiEndSlide01;

        void DecayPreviousState()
        {
            if (previousState != SkiEndSlideState.None && Time.time <= _lastSkiEndSlideTime + noseTailSlideStateGrace)
            {
                _skiEndSlideState = previousState;
                _skiEndSlide01 = Mathf.MoveTowards(previousStrength, 0f, Time.fixedDeltaTime / Mathf.Max(0.01f, noseTailSlideStateGrace));
            }
            else
            {
                _skiEndSlideState = SkiEndSlideState.None;
                _skiEndSlide01 = 0f;
            }
        }

        if (!enableNoseTailSlides || _stacked || !IsGroundedForControls)
        {
            DecayPreviousState();
            return false;
        }

        Vector3 groundNormal = (_groundNormal.sqrMagnitude > 0.0001f) ? _groundNormal.normalized : Vector3.up;
        float planarSpeed = Vector3.ProjectOnPlane(_rb.linearVelocity, groundNormal).magnitude;
        float grind01 = _grindActive ? Mathf.Clamp01(_grindStrengthSmoothed) : 0f;

        float effectiveMinSpeed = Mathf.Lerp(
            noseTailSlideMinSpeed,
            noseTailSlideMinSpeed * grindNoseTailMinSpeedMultiplier,
            grind01);

        float effectiveFullSpeed = Mathf.Lerp(
            noseTailSlideFullSpeed,
            Mathf.Max(effectiveMinSpeed + 0.01f, noseTailSlideFullSpeed * grindNoseTailMinSpeedMultiplier),
            grind01);

        float speed01 = Mathf.InverseLerp(
            effectiveMinSpeed,
            Mathf.Max(effectiveMinSpeed + 0.01f, effectiveFullSpeed),
            planarSpeed);
        if (speed01 <= 0f)
        {
            DecayPreviousState();
            return false;
        }

        bool leftValid = TryGetSkiEndSlide(leftSkiContact, out int leftEndSign, out float leftBaseAlignment);
        bool rightValid = TryGetSkiEndSlide(rightSkiContact, out int rightEndSign, out float rightBaseAlignment);
        if (!leftValid && !rightValid)
        {
            DecayPreviousState();
            return false;
        }

        int signSum = (leftValid ? leftEndSign : 0) + (rightValid ? rightEndSign : 0);
        if (leftValid && rightValid && signSum == 0)
        {
            _skiEndSlideState = SkiEndSlideState.Mixed;
            _skiEndSlide01 = 0f;
            return false;
        }

        int dominantEndSign = signSum > 0 ? 1 : -1;
        float weakestBase = Mathf.Min(
            leftValid ? leftBaseAlignment : float.MaxValue,
            rightValid ? rightBaseAlignment : float.MaxValue);
        if (weakestBase == float.MaxValue || weakestBase < noseTailSlideMinBaseAlignment)
        {
            DecayPreviousState();
            return false;
        }

        float effectiveLeanThreshold = Mathf.Lerp(
            noseTailSlideLeanThreshold,
            noseTailSlideLeanThreshold * grindNoseTailLeanThresholdMultiplier,
            grind01);

        float leanMatch = Mathf.Clamp01(
            Mathf.InverseLerp(effectiveLeanThreshold, 1f, _forwardLean * dominantEndSign));
        if (leanMatch <= 0f)
        {
            DecayPreviousState();
            return false;
        }

        _skiEndSlideState = ResolveSkiEndSlideState(leftValid, rightValid, leftEndSign, rightEndSign);
        _skiEndSlide01 = speed01 * leanMatch;
        _lastSkiEndSlideTime = Time.time;
        return _skiEndSlide01 > 0f && _skiEndSlideState != SkiEndSlideState.Mixed;
    }

    private bool TryGetIntentionalTipBalance(out int dominantEndSign, out float balance01, out float weakestEndBaseAlignment)
    {
        dominantEndSign = 0;
        balance01 = 0f;
        weakestEndBaseAlignment = 1f;

        bool validSlide = TryUpdateNoseTailSlideState();
        if (!validSlide)
            return false;

        switch (_skiEndSlideState)
        {
            case SkiEndSlideState.LeftNose:
            case SkiEndSlideState.RightNose:
            case SkiEndSlideState.BothNose:
                dominantEndSign = 1;
                break;

            case SkiEndSlideState.LeftTail:
            case SkiEndSlideState.RightTail:
            case SkiEndSlideState.BothTail:
                dominantEndSign = -1;
                break;

            default:
                return false;
        }

        if (leftSkiContact != null && TryGetSkiEndSlide(leftSkiContact, out _, out float leftBase))
            weakestEndBaseAlignment = Mathf.Min(weakestEndBaseAlignment, leftBase);
        if (rightSkiContact != null && TryGetSkiEndSlide(rightSkiContact, out _, out float rightBase))
            weakestEndBaseAlignment = Mathf.Min(weakestEndBaseAlignment, rightBase);

        balance01 = _skiEndSlide01;
        return true;
    }

    private bool TryGetSkiEndSlide(SkiContact contact, out int endSign, out float baseAlignment)
    {
        endSign = 0;
        baseAlignment = 0f;

        if (contact == null ||
            !contact.HasEndContact ||
            contact.EndContactSign == 0 ||
            !IsRideableNormal(contact.ContactNormal))
            return false;

        endSign = contact.EndContactSign;
        baseAlignment = contact.HasCollisionEndContact
            ? Mathf.Max(contact.BaseContactAlignment, noseTailSlideMinBaseAlignment)
            : contact.BaseContactAlignment;
        return true;
    }

    private static SkiEndSlideState ResolveSkiEndSlideState(bool leftValid, bool rightValid, int leftEndSign, int rightEndSign)
    {
        if (leftValid && rightValid)
        {
            if (leftEndSign > 0 && rightEndSign > 0) return SkiEndSlideState.BothNose;
            if (leftEndSign < 0 && rightEndSign < 0) return SkiEndSlideState.BothTail;
            return SkiEndSlideState.Mixed;
        }

        if (leftValid)
            return leftEndSign > 0 ? SkiEndSlideState.LeftNose : SkiEndSlideState.LeftTail;

        if (rightValid)
            return rightEndSign > 0 ? SkiEndSlideState.RightNose : SkiEndSlideState.RightTail;

        return SkiEndSlideState.None;
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

        bool hasIntentionalTipBalance = TryUpdateNoseTailSlideState();
        if (hasIntentionalTipBalance)
        {
            Vector3 desiredUp = (_alignNormal.sqrMagnitude > 0.0001f) ? _alignNormal.normalized : _groundNormal.normalized;
            float planarSpeedForFlatten = Vector3.ProjectOnPlane(_rb.linearVelocity, desiredUp).magnitude;
            if (planarSpeedForFlatten > noseTailSlideFlattenBelowSpeed)
            {
                _tipContactAccumTime = 0f;
                return;
            }

            _skiEndSlideState = SkiEndSlideState.None;
            _skiEndSlide01 = 0f;
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

        bool hasLegacyTipBalance = TryGetIntentionalTipBalance(out int intentionalEndSign, out float intentionalTipBalance01, out float weakestIntentionalEndBase);

        bool leftBaseStable = leftGrounded && leftBaseAlign >= minBaseAlignmentForStableStance;
        bool rightBaseStable = rightGrounded && rightBaseAlign >= minBaseAlignmentForStableStance;

        bool anyStableBase = leftBaseStable || rightBaseStable;

        // If NEITHER ski has a reasonably flat base contact, we're effectively on edges/tips on both skis.
        // Stack immediately unless the player is deliberately matching a balanced nose/tail slide.
        if (!anyStableBase)
        {
            bool canSaveWithIntentionalBalance =
                hasLegacyTipBalance &&
                weakestIntentionalEndBase >= noseTailSlideMinBaseAlignment;

            if (canSaveWithIntentionalBalance)
            {
                _tipContactAccumTime += Time.fixedDeltaTime;
                float savedTime = tipContactStackTime * Mathf.Lerp(1.25f, 1f + Mathf.Max(0f, tipBalanceGraceMultiplier), intentionalTipBalance01);
                if (_tipContactAccumTime >= savedTime)
                {
                    TriggerStack(ComputeStackTorqueAxisFromContacts());
                    _tipContactAccumTime = 0f;
                }
            }
            else
            {
                if (HasExternalInputSource)
                {
                    _tipContactAccumTime += Time.fixedDeltaTime;

                    float npcGrace = tipContactStackTime * 1.75f;
                    if (_tipContactAccumTime >= npcGrace)
                    {
                        TriggerStack(ComputeStackTorqueAxisFromContacts());
                        _tipContactAccumTime = 0f;
                    }
                }
                else
                {
                    _tipContactAccumTime += Time.fixedDeltaTime;

                    float grace = Mathf.Max(0.22f, tipContactStackTime);
                    if (_tipContactAccumTime >= grace)
                    {
                        TriggerStack(ComputeStackTorqueAxisFromContacts());
                        _tipContactAccumTime = 0f;
                    }
                }
            }

            return;
        }

        // If we're basically stationary, don't allow balancing on a strong edge.
        if (planarSpeed <= 1.0f)
        {
            float worstBase = 1f;
            if (leftGrounded) worstBase = Mathf.Min(worstBase, leftBaseAlign);
            if (rightGrounded) worstBase = Mathf.Min(worstBase, rightBaseAlign);

            float lowSpeedThreshold = HasExternalInputSource
    ? Mathf.Min(0.22f, lowSpeedEdgeStackAlignment)
    : lowSpeedEdgeStackAlignment;

            if (worstBase < lowSpeedThreshold)
            {
                TriggerStack(ComputeStackTorqueAxisFromContacts());
                _tipContactAccumTime = 0f;
                return;
            }
        }

        // If any grounded ski is essentially sideways/upside-down, fall immediately.
        bool severeLeft = leftGrounded && leftBaseAlign < severeEdgeAlignment;
        bool severeRight = rightGrounded && rightBaseAlign < severeEdgeAlignment;
        if ((severeLeft || severeRight) && !hasLegacyTipBalance)
        {
            _tipContactAccumTime += Time.fixedDeltaTime;

            if (_tipContactAccumTime >= Mathf.Max(0.18f, tipContactStackTime * 0.5f))
            {
                TriggerStack(ComputeStackTorqueAxisFromContacts());
                _tipContactAccumTime = 0f;
            }

            return;
        }

        // At least one ski has a decent base contact. Now check whether we're
        // "riding on the tips" on the dominant contacts.
        if (tipFraction >= tipContactStackFraction && planarSpeed >= tipContactMinSpeed)
        {
            // Heavily tip/tail-weighted contact: accumulate time.
            _tipContactAccumTime += Time.fixedDeltaTime;

            // On steeper slopes, allow more time before stacking so normal downhill skiing
            // over steepening terrain doesn't instantly become a "tip-stand" failure.
            float slopeAngle = Vector3.Angle((_alignNormal.sqrMagnitude > 0.0001f ? _alignNormal : _groundNormal), Vector3.up);
            float slopeT = Mathf.InverseLerp(25f, 65f, slopeAngle);
            float allowedTime = Mathf.Lerp(tipContactStackTime, tipContactStackTime * 1.6f, slopeT);

            if (hasLegacyTipBalance)
            {
                float leanBias = Mathf.Clamp(_forwardLean * intentionalEndSign, -1f, 1f);
                if (leanBias > 0f)
                {
                    float support = Mathf.Lerp(1f, 1f + Mathf.Max(0f, tipBalanceGraceMultiplier), intentionalTipBalance01);
                    allowedTime *= support;
                }
                else if (leanBias < 0f)
                {
                    allowedTime *= Mathf.Lerp(1f, 0.65f, -leanBias);
                }
            }

            // Only accumulate tip-failure time when we're also meaningfully not on our bases.
            // This prevents steep-slope transitions from counting as a failure if base contact is still decent.
            float worstBaseAlign = 1f;
            if (leftGrounded) worstBaseAlign = Mathf.Min(worstBaseAlign, leftBaseAlign);
            if (rightGrounded) worstBaseAlign = Mathf.Min(worstBaseAlign, rightBaseAlign);

            float unstableThreshold = minBaseAlignmentForStableStance * (hasLegacyTipBalance ? 0.70f : 0.85f);
            bool trulyUnstable = worstBaseAlign < unstableThreshold;

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

        bool tuckAllowed = !_stacked;
        float targetTuck = (tuckAllowed && _rawTuckPressed) ? 1f : 0f;
        _tuck01 = Mathf.MoveTowards(_tuck01, targetTuck, tuckLerpSpeed * Time.deltaTime);
        _tuck01 = Mathf.Clamp01(_tuck01);

        bool poseStyleAllowed =
            !_stacked &&
            _rawPosePressed &&
            (_grindActive || (!IsGroundedForControls && !_skiContactPlausibleForBody));

        _dbgPoseInputActive = poseStyleAllowed;
        float targetAirStyle = poseStyleAllowed ? 1f : 0f;
        _airStyle01 = Mathf.MoveTowards(_airStyle01, targetAirStyle, airStyleBlendSpeed * Time.deltaTime);
        _airStyle01 = Mathf.Clamp01(_airStyle01);
    }

    private Transform GetBodyPoseTransform()
    {
        if (bodyMotionTransform != null && bodyMotionTransform != transform)
            return bodyMotionTransform;

        if (bodyTransform != null && bodyTransform != transform)
            return bodyTransform;

        return null;
    }

    private SkierLimbLineVisual GetSkierLimbLineVisual()
    {
        if (_skierLimbLineVisual == null)
            _skierLimbLineVisual = GetComponentInChildren<SkierLimbLineVisual>(true);

        return _skierLimbLineVisual;
    }

    private static void ApplySnapshotPart(Transform target, TrickPoseRigSnapshot.PartState state, float weight)
    {
        if (target == null || !state.hasValue)
            return;

        float t = Mathf.Clamp01(weight);
        target.localPosition = Vector3.Lerp(target.localPosition, state.localPosition, t);
        target.localRotation = Quaternion.Slerp(target.localRotation, state.localRotation, t);
    }

    private void ApplyJointSnapshot(TrickPoseRigSnapshot snapshot, float weight)
    {
        SkierLimbLineVisual limbVisual = GetSkierLimbLineVisual();
        if (limbVisual == null)
            return;

        if (!Application.isPlaying)
        {
            limbVisual.SetPreviewJointSnapshot(snapshot, weight);
            limbVisual.RefreshEditorPreview();
        }
    }

    private static void CaptureCurrentPartInto(PosePartTransformData destination, Transform current, TrickPoseRigSnapshot.PartState defaults)
    {
        if (destination == null)
            return;

        if (current == null || !defaults.hasValue)
        {
            destination.Reset();
            return;
        }

        destination.CaptureOffset(current, defaults.localPosition, defaults.localRotation);
    }

    private void CaptureCurrentJointInto(PosePartTransformData destination, SkierLimbJoint joint, TrickPoseRigSnapshot.PartState defaults)
    {
        if (destination == null)
            return;

        if (!destination.enabled)
            return;

        SkierLimbLineVisual limbVisual = GetSkierLimbLineVisual();
        if (limbVisual == null || !defaults.hasValue)
        {
            destination.Reset();
            return;
        }

        if (!limbVisual.TryCaptureJointPose(joint, out Vector3 localPosition, out Quaternion localRotation))
        {
            destination.Reset();
            return;
        }

        destination.enabled = true;
        destination.localPosition = localPosition;
        destination.localEulerAngles = Vector3.zero;
        destination.weight = 1f;
    }

    private TrickPoseRigSnapshot.PartState CaptureJointPartState(SkierLimbLineVisual limbVisual, SkierLimbJoint joint)
    {
        if (limbVisual == null || !limbVisual.TryCaptureJointPose(joint, out Vector3 localPosition, out Quaternion localRotation))
        {
            return new TrickPoseRigSnapshot.PartState
            {
                hasValue = false,
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity
            };
        }

        return new TrickPoseRigSnapshot.PartState
        {
            hasValue = true,
            localPosition = localPosition,
            localRotation = localRotation
        };
    }

    private TrickPoseEntry EvaluateActiveTrickPoseEntry()
    {
        UpdateTrickPoseRecognitionLifecycle();

        if (trickPoseProfile == null || trickPoseProfile.entries == null || trickPoseProfile.entries.Count == 0)
            return ResolveCommittedCandidate(default, false);

        TrickPoseCandidateScore best = default;
        TrickPoseCandidateScore runnerUp = default;
        bool hasBest = false;

        for (int i = 0; i < trickPoseProfile.entries.Count; i++)
        {
            TrickPoseEntry candidate = trickPoseProfile.entries[i];
            if (candidate == null || !candidate.Matches(this))
                continue;

            TrickPoseCandidateScore score = ScoreTrickPoseCandidate(candidate);
            if (!hasBest || IsBetterTrickPoseCandidate(score, best))
            {
                runnerUp = best;
                best = score;
                hasBest = true;
            }
            else if (runnerUp.entry == null || IsBetterTrickPoseCandidate(score, runnerUp))
            {
                runnerUp = score;
            }
        }

        _bestCompetingTrickPoseEntry = runnerUp.entry;
        _bestCompetingTrickPoseScore = runnerUp.totalScore;

        return ResolveCommittedCandidate(best, hasBest);
    }

    private void UpdateActiveTrickPoseBlend(float deltaTime, TrickPoseEntry selectedEntry)
    {
        if (selectedEntry != null)
            _activeTrickPoseEntry = selectedEntry;

        float target = selectedEntry != null ? 1f : 0f;
        TrickPoseEntry blendSource = selectedEntry ?? _activeTrickPoseEntry;
        float speed = target > _activeTrickPoseBlend
            ? Mathf.Max(0.01f, blendSource != null ? blendSource.blendInSpeed : trickPoseProfile != null ? trickPoseProfile.defaultBlendInSpeed : 8f)
            : Mathf.Max(0.01f, blendSource != null ? blendSource.blendOutSpeed : trickPoseProfile != null ? trickPoseProfile.defaultBlendOutSpeed : 8f);

        _activeTrickPoseBlend = Mathf.MoveTowards(_activeTrickPoseBlend, target, speed * deltaTime);

        if (selectedEntry == null && _activeTrickPoseBlend <= 0.001f)
            _activeTrickPoseEntry = null;
    }

    private void UpdateTrickPoseRecognitionLifecycle()
    {
        bool airborneNow = IsAirborne;
        bool posePressedNow = IsPoseButtonHeld && !_trickPoseWasPoseButtonHeldLastFrame;
        if (airborneNow && !_trickPoseWasAirborneLastFrame)
            CaptureTrickPoseIntentSnapshot();
        else if (!airborneNow && _trickPoseWasAirborneLastFrame)
            ResetTrickPoseRecognitionIntent();

        bool inEntryWindow = GetAirborneTimeForTrickRecognition() <= trickPoseIntentSnapshotWindow;
        if (airborneNow && posePressedNow)
            CaptureTrickPoseIntentSnapshot();
        else if (airborneNow && !_trickPoseIntentSnapshot.valid && inEntryWindow)
            CaptureTrickPoseIntentSnapshot();

        _trickPoseWasAirborneLastFrame = airborneNow;
        _trickPoseWasPoseButtonHeldLastFrame = IsPoseButtonHeld;
    }

    private void CaptureTrickPoseIntentSnapshot()
    {
        _trickPoseIntentSnapshot.valid = true;
        _trickPoseIntentSnapshot.capturedTime = Time.time;
        _trickPoseIntentSnapshot.poseButtonHeld = IsPoseButtonHeld;
        _trickPoseIntentSnapshot.family = IsPoseButtonHeld ? ResolvePoseFamilyFromInputs() : ResolvePoseFamily();
        _trickPoseIntentSnapshot.shape = IsPoseButtonHeld ? ResolvePoseShapeFromInputs() : ResolvePoseShape();
        _trickPoseIntentSnapshot.orientation = IsPoseButtonHeld ? ResolvePoseOrientationModifierFromState() : ResolvePoseOrientationModifier();
        _trickPoseIntentSnapshot.verticalOrientation = ResolvePoseVerticalOrientation();
        _trickPoseIntentSnapshot.horizontalOrientation = ResolvePoseHorizontalOrientation();
        _trickPoseIntentSnapshot.travelFacing = ResolvePoseTravelFacing();

        _trickPoseIntentSnapshot.motionState = ResolvePoseMotionState();
        _trickPoseIntentSnapshot.poseName = ResolvePoseName(_trickPoseIntentSnapshot.family, _trickPoseIntentSnapshot.shape);
        _trickPoseIntentSnapshot.spinDirectionSign = CurrentSpinDirectionSign;
        _trickPoseIntentSnapshot.flipDirectionSign = CurrentFlipDirectionSign;
        _trickPoseIntentSnapshot.entryEulerAngles = CurrentSignedEulerAngles;
    }

    private void ResetTrickPoseRecognitionIntent()
    {
        _trickPoseIntentSnapshot = default;
        _bestCompetingTrickPoseEntry = null;
        _bestCompetingTrickPoseScore = 0f;
        _lastTrickPoseDecisionReason = string.Empty;
        _trickPoseWasPoseButtonHeldLastFrame = false;
    }

    private float GetAirborneTimeForTrickRecognition()
    {
        return _airborneStartTime >= 0f
            ? Mathf.Max(0f, Time.time - _airborneStartTime)
            : 0f;
    }

    private TrickPoseCandidateScore ScoreTrickPoseCandidate(TrickPoseEntry entry)
    {
        int specificity = entry.GetSpecificityScore();
        float rawScore =
            (entry.priority * 16f) +
            (specificity * 2f) +
            Mathf.Clamp01(entry.overallWeight);
        float intentBias = EvaluateIntentBias(entry) * trickPoseIntentBiasStrength;

        return new TrickPoseCandidateScore
        {
            entry = entry,
            specificity = specificity,
            rawScore = rawScore,
            totalScore = rawScore + intentBias,
            intentBias = intentBias
        };
    }

    private float EvaluateIntentBias(TrickPoseEntry entry)
    {
        if (!_trickPoseIntentSnapshot.valid || entry == null)
            return 0f;

        float bias = 0f;

        if (!string.IsNullOrWhiteSpace(entry.requiredPoseName) &&
            string.Equals(entry.requiredPoseName, _trickPoseIntentSnapshot.poseName, System.StringComparison.OrdinalIgnoreCase))
            bias += 2f;

        if (entry.requiredPoseFamily != AerialPoseFamily.None &&
            entry.requiredPoseFamily == _trickPoseIntentSnapshot.family)
            bias += 1.5f;

        if (entry.requiredPoseShape != AerialPoseShape.None &&
            entry.requiredPoseShape == _trickPoseIntentSnapshot.shape)
            bias += 1.25f;

        if (entry.requiredVerticalOrientation != TrickPoseVerticalOrientationRequirement.Any &&
            entry.requiredVerticalOrientation == _trickPoseIntentSnapshot.verticalOrientation)
            bias += 0.75f;

        if (entry.requiredHorizontalOrientation != TrickPoseHorizontalOrientationRequirement.Any &&
            entry.requiredHorizontalOrientation == _trickPoseIntentSnapshot.horizontalOrientation)
            bias += 0.75f;

        if (entry.requiredMotionState != TrickPoseMotionStateRequirement.Any &&
            entry.requiredMotionState == _trickPoseIntentSnapshot.motionState)
            bias += 0.5f;

        if (entry.requirePoseButtonHeld != TrickPoseBoolRequirement.Ignore &&
            ((entry.requirePoseButtonHeld == TrickPoseBoolRequirement.True) == _trickPoseIntentSnapshot.poseButtonHeld))
            bias += 0.75f;

        if (entry.useAdvancedModifierConditions)
        {
            if (entry.requiredSpinDirection != TrickPoseSpinDirectionRequirement.Any &&
                (int)entry.requiredSpinDirection == _trickPoseIntentSnapshot.spinDirectionSign)
                bias += 0.5f;

            if (entry.requiredFlipDirection != TrickPoseFlipDirectionRequirement.Any &&
                (int)entry.requiredFlipDirection == _trickPoseIntentSnapshot.flipDirectionSign)
                bias += 0.5f;
        }

        if (entry.requiredOrientationModifier != AerialOrientationModifier.None &&
            entry.requiredOrientationModifier == _trickPoseIntentSnapshot.orientation)
            bias += 0.25f;

        if (entry.entryPitchAngleRange.enabled && entry.entryPitchAngleRange.Contains(_trickPoseIntentSnapshot.entryEulerAngles.x))
            bias += 1f;

        if (entry.entryYawAngleRange.enabled && entry.entryYawAngleRange.Contains(_trickPoseIntentSnapshot.entryEulerAngles.y))
            bias += 1f;

        if (entry.entryRollAngleRange.enabled && entry.entryRollAngleRange.Contains(_trickPoseIntentSnapshot.entryEulerAngles.z))
            bias += 1f;

        return bias;
    }

    private static bool IsBetterTrickPoseCandidate(TrickPoseCandidateScore candidate, TrickPoseCandidateScore incumbent)
    {
        if (candidate.entry == null)
            return false;

        if (incumbent.entry == null)
            return true;

        if (!Mathf.Approximately(candidate.totalScore, incumbent.totalScore))
            return candidate.totalScore > incumbent.totalScore;

        if (candidate.entry.priority != incumbent.entry.priority)
            return candidate.entry.priority > incumbent.entry.priority;

        return candidate.specificity > incumbent.specificity;
    }

    private TrickPoseEntry ResolveCommittedCandidate(TrickPoseCandidateScore bestCandidate, bool hasBestCandidate)
    {
        if (_activeTrickPoseEntry == null)
        {
            if (hasBestCandidate)
                CommitActiveTrickPose(bestCandidate, "Initial commit");
            else
                _lastTrickPoseDecisionReason = "No authored pose matched";

            return hasBestCandidate ? bestCandidate.entry : null;
        }

        bool committedStillValid = TryScoreCurrentCommittedPose(out TrickPoseCandidateScore committedScore);
        bool hasCompetingCandidate = hasBestCandidate && bestCandidate.entry != _activeTrickPoseEntry;

        if (committedStillValid)
        {
            _activeTrickPoseScore = committedScore.totalScore;
            _activeTrickPoseLastValidTime = Time.time;

            if (!hasCompetingCandidate)
            {
                _lastTrickPoseDecisionReason = hasBestCandidate
                    ? "Committed trick remains best match"
                    : "Retaining committed trick";
                return _activeTrickPoseEntry;
            }

            bool holdElapsed = (Time.time - _activeTrickPoseCommittedAt) >= trickPoseCommitMinHoldTime;
            bool clearlySuperior = bestCandidate.totalScore >= committedScore.totalScore + trickPoseSwitchScoreMargin;
            bool strongIntentChange = HasStrongIntentChange(_activeTrickPoseEntry, bestCandidate.entry);

            if ((holdElapsed && clearlySuperior) || (strongIntentChange && bestCandidate.totalScore > committedScore.totalScore))
            {
                CommitActiveTrickPose(bestCandidate, strongIntentChange ? "Intent change switch" : "Superior candidate switch");
                return bestCandidate.entry;
            }

            _lastTrickPoseDecisionReason = holdElapsed
                ? "Retaining committed trick via hysteresis"
                : "Retaining committed trick during minimum hold";
            return _activeTrickPoseEntry;
        }

        float invalidDuration = Time.time - _activeTrickPoseLastValidTime;
        bool withinGrace = invalidDuration <= trickPoseRetentionGraceTime;

        if (hasBestCandidate)
        {
            if (!withinGrace)
            {
                CommitActiveTrickPose(bestCandidate, "Committed trick invalid; switched");
                return bestCandidate.entry;
            }

            bool strongIntentChange = HasStrongIntentChange(_activeTrickPoseEntry, bestCandidate.entry);
            bool clearlySuperior = bestCandidate.totalScore >= _activeTrickPoseScore + (trickPoseSwitchScoreMargin * 0.5f);
            if (strongIntentChange || clearlySuperior)
            {
                CommitActiveTrickPose(bestCandidate, strongIntentChange ? "Intent change during grace" : "Grace override switch");
                return bestCandidate.entry;
            }

            _lastTrickPoseDecisionReason = "Holding committed trick during grace";
            return _activeTrickPoseEntry;
        }

        if (withinGrace)
        {
            _lastTrickPoseDecisionReason = "Holding committed trick during grace";
            return _activeTrickPoseEntry;
        }

        _lastTrickPoseDecisionReason = "Committed trick lost validity";
        return null;
    }

    private bool TryScoreCurrentCommittedPose(out TrickPoseCandidateScore score)
    {
        if (_activeTrickPoseEntry != null && _activeTrickPoseEntry.Matches(this))
        {
            score = ScoreTrickPoseCandidate(_activeTrickPoseEntry);
            return true;
        }

        score = default;
        return false;
    }

    private void CommitActiveTrickPose(TrickPoseCandidateScore candidate, string reason)
    {
        _activeTrickPoseEntry = candidate.entry;
        _activeTrickPoseScore = candidate.totalScore;
        _activeTrickPoseCommittedAt = Time.time;
        _activeTrickPoseLastValidTime = Time.time;
        _lastTrickPoseDecisionReason = reason;

        if (debugTrickPoseRecognition && _activeTrickPoseEntry != null)
        {
            Debug.Log(
                $"[{nameof(SkiController)}] Trick pose commit '{ResolveTrackedPoseLabel(_activeTrickPoseEntry, ResolvePoseName())}' " +
                $"raw={candidate.rawScore:0.00} bias={candidate.intentBias:0.00} total={candidate.totalScore:0.00} reason={reason}",
                this);
        }
    }

    private bool HasStrongIntentChange(TrickPoseEntry committedEntry, TrickPoseEntry candidateEntry)
    {
        if (committedEntry == null || candidateEntry == null || committedEntry == candidateEntry)
            return false;

        bool currentFamilyLeftCommitted =
            committedEntry.requiredPoseFamily != AerialPoseFamily.None &&
            CurrentPoseFamily != committedEntry.requiredPoseFamily;
        bool candidateMatchesFamilyShift =
            candidateEntry.requiredPoseFamily != AerialPoseFamily.None &&
            candidateEntry.requiredPoseFamily == CurrentPoseFamily &&
            candidateEntry.requiredPoseFamily != committedEntry.requiredPoseFamily;

        bool currentShapeLeftCommitted =
            committedEntry.requiredPoseShape != AerialPoseShape.None &&
            CurrentPoseShape != committedEntry.requiredPoseShape;
        bool candidateMatchesShapeShift =
            candidateEntry.requiredPoseShape != AerialPoseShape.None &&
            candidateEntry.requiredPoseShape == CurrentPoseShape &&
            candidateEntry.requiredPoseShape != committedEntry.requiredPoseShape;

        bool currentNameLeftCommitted =
            !string.IsNullOrWhiteSpace(committedEntry.requiredPoseName) &&
            !string.Equals(CurrentPoseName, committedEntry.requiredPoseName, System.StringComparison.OrdinalIgnoreCase);
        bool candidateMatchesNameShift =
            !string.IsNullOrWhiteSpace(candidateEntry.requiredPoseName) &&
            string.Equals(CurrentPoseName, candidateEntry.requiredPoseName, System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidateEntry.requiredPoseName, committedEntry.requiredPoseName, System.StringComparison.OrdinalIgnoreCase);

        bool poseButtonChangedAway =
            committedEntry.requirePoseButtonHeld == TrickPoseBoolRequirement.True && !IsPoseButtonHeld ||
            committedEntry.requirePoseButtonHeld == TrickPoseBoolRequirement.False && IsPoseButtonHeld;
        bool candidateMatchesPoseButtonShift =
            candidateEntry.requirePoseButtonHeld == TrickPoseBoolRequirement.True && IsPoseButtonHeld ||
            candidateEntry.requirePoseButtonHeld == TrickPoseBoolRequirement.False && !IsPoseButtonHeld;

        return
            (currentFamilyLeftCommitted && candidateMatchesFamilyShift) ||
            (currentShapeLeftCommitted && candidateMatchesShapeShift) ||
            (currentNameLeftCommitted && candidateMatchesNameShift) ||
            (poseButtonChangedAway && candidateMatchesPoseButtonShift);
    }

    private string ResolveCommittedTrackedPoseName()
    {
        return ResolveTrackedPoseLabel(_activeTrickPoseEntry, ResolvePoseName());
    }

    private AerialPoseFamily ResolveCommittedPoseFamily()
    {
        if (_activeTrickPoseEntry != null && _activeTrickPoseEntry.requiredPoseFamily != AerialPoseFamily.None)
            return _activeTrickPoseEntry.requiredPoseFamily;

        if (_trickPoseIntentSnapshot.valid && _trickPoseIntentSnapshot.family != AerialPoseFamily.None)
            return _trickPoseIntentSnapshot.family;

        return ResolvePoseFamily();
    }

    private AerialPoseShape ResolveCommittedPoseShape()
    {
        if (_activeTrickPoseEntry != null && _activeTrickPoseEntry.requiredPoseShape != AerialPoseShape.None)
            return _activeTrickPoseEntry.requiredPoseShape;

        if (_trickPoseIntentSnapshot.valid && _trickPoseIntentSnapshot.shape != AerialPoseShape.None)
            return _trickPoseIntentSnapshot.shape;

        return ResolvePoseShape();
    }

    private static string ResolveTrackedPoseLabel(TrickPoseEntry entry, string fallbackPoseName)
    {
        if (entry != null)
        {
            // Explicit label always wins.
            if (!string.IsNullOrWhiteSpace(entry.overridePoseLabel))
                return entry.overridePoseLabel.Trim();

            // Display name should act as the default authored label.
            if (!string.IsNullOrWhiteSpace(entry.displayName) &&
                !string.Equals(entry.displayName, "New Trick Pose", System.StringComparison.OrdinalIgnoreCase))
                return entry.displayName.Trim();

            // Required pose name is now only a technical fallback, not the preferred display label.
            if (!string.IsNullOrWhiteSpace(entry.requiredPoseName))
                return entry.requiredPoseName.Trim();
        }

        return string.IsNullOrWhiteSpace(fallbackPoseName) ? string.Empty : fallbackPoseName.Trim();
    }

    private static string BuildPoseDescriptorLabel(string basePoseName, AerialOrientationModifier orientationModifier)
    {
        if (string.IsNullOrWhiteSpace(basePoseName))
            return string.Empty;

        string modifier = GetAerialOrientationModifierLabel(orientationModifier);

        return string.IsNullOrWhiteSpace(modifier)
            ? basePoseName
            : $"{modifier} {basePoseName}";
    }

    public static string GetAerialOrientationModifierLabel(AerialOrientationModifier orientationModifier)
    {
        return orientationModifier switch
        {
            AerialOrientationModifier.Switch => "Switch",
            AerialOrientationModifier.Inverted => "Inverted",
            AerialOrientationModifier.Sideways => "Travel Sideways",
            AerialOrientationModifier.Rising => "Motion Rising",
            AerialOrientationModifier.Diving => "Motion Diving",
            AerialOrientationModifier.OnSide => "On Side",
            AerialOrientationModifier.ChestDown => "Chest Down",
            AerialOrientationModifier.ChestUp => "Chest Up",
            _ => string.Empty
        };
    }

    private void ApplyEntryToSnapshot(TrickPoseRigSnapshot snapshot, TrickPoseEntry entry, float masterWeight)
    {
        if (snapshot == null || entry == null)
            return;

        float finalWeight = Mathf.Clamp01(entry.overallWeight * masterWeight);
        BlendSnapshotPart(ref snapshot.body, entry.bodyPose, finalWeight);
        BlendSnapshotPart(ref snapshot.head, entry.headPose, finalWeight);
        BlendSnapshotPart(ref snapshot.leftSki, entry.leftSkiPose, finalWeight);
        BlendSnapshotPart(ref snapshot.rightSki, entry.rightSkiPose, finalWeight);
        BlendSnapshotPart(ref snapshot.leftPole, entry.leftPolePose, finalWeight);
        BlendSnapshotPart(ref snapshot.rightPole, entry.rightPolePose, finalWeight);
        BlendSnapshotJointPart(ref snapshot.leftElbow, entry.leftElbowPose, finalWeight);
        BlendSnapshotJointPart(ref snapshot.rightElbow, entry.rightElbowPose, finalWeight);
        BlendSnapshotJointPart(ref snapshot.leftKnee, entry.leftKneePose, finalWeight);
        BlendSnapshotJointPart(ref snapshot.rightKnee, entry.rightKneePose, finalWeight);
    }

    private static void BlendSnapshotPart(ref TrickPoseRigSnapshot.PartState part, PosePartTransformData pose, float masterWeight)
    {
        if (pose == null || !pose.enabled || !part.hasValue)
            return;

        float t = Mathf.Clamp01(masterWeight * Mathf.Clamp01(pose.weight));
        Vector3 targetPos = pose.GetAbsoluteLocalPosition(part.localPosition);
        Quaternion targetRot = pose.GetAbsoluteLocalRotation(part.localRotation);

        part.localPosition = Vector3.Lerp(part.localPosition, targetPos, t);
        part.localRotation = Quaternion.Slerp(part.localRotation, targetRot, t);
    }

    private static void BlendSnapshotJointPart(ref TrickPoseRigSnapshot.PartState part, PosePartTransformData pose, float masterWeight)
    {
        if (pose == null || !pose.enabled || !part.hasValue)
            return;

        float t = Mathf.Clamp01(masterWeight * Mathf.Clamp01(pose.weight));
        part.localPosition = Vector3.Lerp(part.localPosition, pose.localPosition, t);
        part.localRotation = Quaternion.identity;
    }

    private static void BlendPosePart(ref Vector3 targetPos, ref Quaternion targetRot, TrickPoseRigSnapshot.PartState defaults, PosePartTransformData pose, float masterWeight)
    {
        if (pose == null || !pose.enabled || !defaults.hasValue)
            return;

        float t = Mathf.Clamp01(masterWeight * Mathf.Clamp01(pose.weight));
        Vector3 authoredPos = pose.GetAbsoluteLocalPosition(defaults.localPosition);
        Quaternion authoredRot = pose.GetAbsoluteLocalRotation(defaults.localRotation);

        targetPos = Vector3.Lerp(targetPos, authoredPos, t);
        targetRot = Quaternion.Slerp(targetRot, authoredRot, t);
    }

    private void ApplyPassiveAirDriftToSkis(ref Vector3 leftPos, ref Quaternion leftRot, ref Vector3 rightPos, ref Quaternion rightRot, float authoredWeight)
    {
        if (!enablePassiveAirDrift || !IsAirborne)
            return;

        Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
        float suppression = Mathf.Lerp(1f, passiveAirDriftWhilePoseMultiplier, Mathf.Clamp01(authoredWeight));
        float strength = Mathf.Clamp01(localVelocity.magnitude * passiveAirDriftVelocityInfluence) * suppression;
        if (strength <= 0.0001f)
            return;

        Vector3 driftOffset = new Vector3(
            Mathf.Clamp(localVelocity.x * 0.012f, -passiveAirDriftMaxPosition, passiveAirDriftMaxPosition),
            0f,
            Mathf.Clamp(-localVelocity.z * 0.015f, -passiveAirDriftMaxPosition, passiveAirDriftMaxPosition)) * strength;

        Vector3 leftOutward = Vector3.left * Mathf.Clamp(localVelocity.x * 0.006f, -passiveAirDriftMaxPosition * 0.75f, passiveAirDriftMaxPosition * 0.75f) * strength;
        Vector3 rightOutward = Vector3.right * Mathf.Clamp(localVelocity.x * 0.006f, -passiveAirDriftMaxPosition * 0.75f, passiveAirDriftMaxPosition * 0.75f) * strength;

        Quaternion driftRot = Quaternion.Euler(
            Mathf.Clamp(localVelocity.y * -0.5f, -passiveAirDriftMaxRotation, passiveAirDriftMaxRotation) * strength,
            Mathf.Clamp(localVelocity.x * 0.5f, -passiveAirDriftMaxRotation, passiveAirDriftMaxRotation) * strength,
            Mathf.Clamp(localVelocity.x * -0.8f, -passiveAirDriftMaxRotation, passiveAirDriftMaxRotation) * strength);

        leftPos += driftOffset + leftOutward;
        rightPos += driftOffset + rightOutward;
        leftRot = leftRot * driftRot;
        rightRot = rightRot * driftRot;
    }

    // ----------------------------------------------------------------------
    // VISUALS
    // ----------------------------------------------------------------------

    private void UpdateVisuals()
    {
        EnsurePoseRigDefaultsCaptured();
        TrickPoseEntry selectedAuthoredPose = EvaluateActiveTrickPoseEntry();
        UpdateActiveTrickPoseBlend(Time.deltaTime, selectedAuthoredPose);
        float authoredPoseWeight = _activeTrickPoseEntry != null
            ? Mathf.Clamp01(_activeTrickPoseEntry.overallWeight * _activeTrickPoseBlend)
            : 0f;

        // ------------------------------
        // Body lean / tuck
        // ------------------------------
        Transform bodyPoseTransform = GetBodyPoseTransform();
        if (bodyPoseTransform != null)
        {
            float pitch = _forwardLean * maxForwardLeanAngle;
            float roll = -_sideLean * maxSideLeanAngle;
            float yaw = 0f;

            Vector3 styleOffset = Vector3.zero;

            // Tuck adds extra forward compression both on ground and in air.
            pitch += tuckBodyPitchExtra * _tuck01;

            float spinSign = Mathf.Sign(_rawRightLegInput - _rawLeftLegInput);
            if (Mathf.Approximately(spinSign, 0f))
                spinSign = Mathf.Sign(_sideLean);

            if (_airStyle01 > 0.001f)
            {
                switch (ResolveAerialStyleMode())
                {
                    case AerialStyleMode.Tweaked:
                        yaw += airStyleBodyYaw * spinSign * _airStyle01;
                        roll += -airStyleBodyRoll * spinSign * _airStyle01;
                        pitch -= (airStyleOpenPitch * 0.35f) * _airStyle01;

                        styleOffset += Vector3.right * (airStyleBodySideOffset * spinSign * 0.85f * _airStyle01);
                        styleOffset += Vector3.down * (airStyleBodyDrop * 0.65f * _airStyle01);
                        styleOffset += Vector3.back * (airStyleBodyBackOffset * _airStyle01);
                        break;

                    case AerialStyleMode.Stretched:
                        pitch -= airStyleOpenPitch * _airStyle01;
                        roll += (-airStyleBodyRoll * 0.35f) * spinSign * _airStyle01;

                        styleOffset += Vector3.down * (airStyleBodyDrop * 0.25f * _airStyle01);
                        styleOffset += Vector3.back * (airStyleBodyBackOffset * 0.35f * _airStyle01);
                        break;

                    case AerialStyleMode.Stylish:
                        yaw += (airStyleBodyYaw * 0.55f) * spinSign * _airStyle01;
                        roll += (-airStyleBodyRoll * 0.55f) * spinSign * _airStyle01;
                        pitch -= (airStyleOpenPitch * 0.45f) * _airStyle01;

                        styleOffset += Vector3.right * (airStyleBodySideOffset * spinSign * 0.45f * _airStyle01);
                        styleOffset += Vector3.down * (airStyleBodyDrop * 0.45f * _airStyle01);
                        styleOffset += Vector3.back * (airStyleBodyBackOffset * 0.65f * _airStyle01);
                        break;
                }
            }

            Vector3 bodyBaseLocalPos = bodyPoseTransform == bodyMotionTransform ? _bodyMotionBaseLocalPos : _bodyBaseLocalPos;
            Vector3 targetBodyPos =
                bodyBaseLocalPos +
                Vector3.down * (tuckBodyDrop * _tuck01) +
                styleOffset;

            Quaternion targetBodyRot = Quaternion.Euler(pitch, yaw, roll);
            if (_defaultRigSnapshot != null)
                BlendPosePart(ref targetBodyPos, ref targetBodyRot, _defaultRigSnapshot.body, _activeTrickPoseEntry != null ? _activeTrickPoseEntry.bodyPose : null, authoredPoseWeight);

            bodyPoseTransform.localRotation = Quaternion.Slerp(
                bodyPoseTransform.localRotation,
                targetBodyRot,
                skiVisualLerpSpeed * Time.deltaTime);

            bodyPoseTransform.localPosition = Vector3.Lerp(
                bodyPoseTransform.localPosition,
                targetBodyPos,
                skiVisualLerpSpeed * Time.deltaTime);
        }

        if (bodyScaleTransform != null)
        {
            Vector3 targetScale = new Vector3(
                _bodyScaleBaseLocalScale.x,
                _bodyScaleBaseLocalScale.y * Mathf.Lerp(1f, tuckBodyYScale, _tuck01),
                _bodyScaleBaseLocalScale.z * Mathf.Lerp(1f, tuckBodyZScale, _tuck01)
            );

            bodyScaleTransform.localScale = Vector3.Lerp(
                bodyScaleTransform.localScale,
                targetScale,
                skiVisualLerpSpeed * Time.deltaTime);
        }

        if (headAnchorTransform != null)
        {
            Vector3 targetHeadPos =
                _headAnchorBaseLocalPos +
                Vector3.down * (tuckHeadDown * _tuck01) +
                Vector3.forward * (tuckHeadForward * _tuck01);
            Quaternion targetHeadRot = _defaultRigSnapshot != null && _defaultRigSnapshot.head.hasValue
                ? _defaultRigSnapshot.head.localRotation
                : headAnchorTransform.localRotation;

            if (_airStyle01 > 0.001f)
            {
                switch (ResolveAerialStyleMode())
                {
                    case AerialStyleMode.Stretched:
                        targetHeadPos += Vector3.up * (airStyleHeadUp * _airStyle01);
                        break;

                    case AerialStyleMode.Tweaked:
                        targetHeadPos += Vector3.forward * (airStyleHeadUp * 0.35f * _airStyle01);
                        break;

                    case AerialStyleMode.Stylish:
                        targetHeadPos += Vector3.up * (airStyleHeadUp * 0.2f * _airStyle01);
                        break;
                }
            }

            if (_defaultRigSnapshot != null)
                BlendPosePart(ref targetHeadPos, ref targetHeadRot, _defaultRigSnapshot.head, _activeTrickPoseEntry != null ? _activeTrickPoseEntry.headPose : null, authoredPoseWeight);

            headAnchorTransform.localPosition = Vector3.Lerp(
                headAnchorTransform.localPosition,
                targetHeadPos,
                skiVisualLerpSpeed * Time.deltaTime);
            headAnchorTransform.localRotation = Quaternion.Slerp(
                headAnchorTransform.localRotation,
                targetHeadRot,
                skiVisualLerpSpeed * Time.deltaTime);
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

        Vector3 targetLeftSkiPos = _leftSkiLocalBasePos + _leftSkiOffsetCurrent;
        Quaternion targetLeftSkiRot = _leftSkiLocalBaseRot * Quaternion.Euler(0f, _leftSkiYawCurrent, 0f);
        Vector3 targetRightSkiPos = _rightSkiLocalBasePos + _rightSkiOffsetCurrent;
        Quaternion targetRightSkiRot = _rightSkiLocalBaseRot * Quaternion.Euler(0f, _rightSkiYawCurrent, 0f);

        if (_defaultRigSnapshot != null)
        {
            BlendPosePart(ref targetLeftSkiPos, ref targetLeftSkiRot, _defaultRigSnapshot.leftSki, _activeTrickPoseEntry != null ? _activeTrickPoseEntry.leftSkiPose : null, authoredPoseWeight);
            BlendPosePart(ref targetRightSkiPos, ref targetRightSkiRot, _defaultRigSnapshot.rightSki, _activeTrickPoseEntry != null ? _activeTrickPoseEntry.rightSkiPose : null, authoredPoseWeight);
        }

        ApplyPassiveAirDriftToSkis(ref targetLeftSkiPos, ref targetLeftSkiRot, ref targetRightSkiPos, ref targetRightSkiRot, authoredPoseWeight);

        ApplyStackSkiVisualOverride(
            leftSki,
            ref targetLeftSkiPos,
            ref targetLeftSkiRot,
            _leftStackSkiVisualOverrideActive,
            _leftStackSkiVisualOverrideWorldPos,
            _leftStackSkiVisualOverrideWorldRot,
            _leftStackSkiVisualOverrideWeight);

        ApplyStackSkiVisualOverride(
            rightSki,
            ref targetRightSkiPos,
            ref targetRightSkiRot,
            _rightStackSkiVisualOverrideActive,
            _rightStackSkiVisualOverrideWorldPos,
            _rightStackSkiVisualOverrideWorldRot,
            _rightStackSkiVisualOverrideWeight);

        if (leftSki != null)
        {
            float effectiveSkiLerpSpeed = Mathf.Max(0.01f, skiVisualLerpSpeed);

            if (_leftStackSkiVisualOverrideActive)
                effectiveSkiLerpSpeed = Mathf.Max(effectiveSkiLerpSpeed, skiVisualLerpSpeed * 2.5f);

            float lerpT = 1f - Mathf.Exp(effectiveSkiLerpSpeed * -Time.deltaTime);
            leftSki.localPosition = Vector3.Lerp(leftSki.localPosition, targetLeftSkiPos, lerpT);
            leftSki.localRotation = Quaternion.Slerp(leftSki.localRotation, targetLeftSkiRot, lerpT);
        }

        if (rightSki != null)
        {
            float effectiveSkiLerpSpeed = Mathf.Max(0.01f, skiVisualLerpSpeed);

            if (_rightStackSkiVisualOverrideActive)
                effectiveSkiLerpSpeed = Mathf.Max(effectiveSkiLerpSpeed, skiVisualLerpSpeed * 2.5f);

            float lerpT = 1f - Mathf.Exp(effectiveSkiLerpSpeed * -Time.deltaTime);
            rightSki.localPosition = Vector3.Lerp(rightSki.localPosition, targetRightSkiPos, lerpT);
            rightSki.localRotation = Quaternion.Slerp(rightSki.localRotation, targetRightSkiRot, lerpT);
        }

        if (leftPoleContact != null)
        {
            if (_activeTrickPoseEntry != null && authoredPoseWeight > 0.001f)
                leftPoleContact.SetAuthoredPoseOverride(_activeTrickPoseEntry.leftPolePose, authoredPoseWeight);
            else
                leftPoleContact.ClearAuthoredPoseOverride();

            leftPoleContact.SetPassiveAirDriftContext(IsAirborne, transform.InverseTransformDirection(_rb.linearVelocity), authoredPoseWeight);
        }

        if (rightPoleContact != null)
        {
            if (_activeTrickPoseEntry != null && authoredPoseWeight > 0.001f)
                rightPoleContact.SetAuthoredPoseOverride(_activeTrickPoseEntry.rightPolePose, authoredPoseWeight);
            else
                rightPoleContact.ClearAuthoredPoseOverride();

            rightPoleContact.SetPassiveAirDriftContext(IsAirborne, transform.InverseTransformDirection(_rb.linearVelocity), authoredPoseWeight);
        }

        SkierLimbLineVisual limbVisual = GetSkierLimbLineVisual();
        if (limbVisual != null)
        {
            limbVisual.SetRuntimeJointPoseEntry(
                _activeTrickPoseEntry != null && authoredPoseWeight > 0.001f ? _activeTrickPoseEntry : null,
                authoredPoseWeight);
        }

        if (debugAuthoredTrickPose && _activeTrickPoseEntry != null)
            Debug.DrawRay(transform.position, transform.up * (0.25f + authoredPoseWeight * 0.35f), Color.yellow);

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

        frictionScale *= 1f - (_tuck01 * tuckFrictionReductionExtra);

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

    private float GetGrindTraction01()
    {
        if (!_grindActive)
            return 1f;

        float buildTime = Mathf.Max(0.01f, grindTractionBuildTime);
        float buildT = Mathf.Clamp01(_grindTime / buildTime);

        float strength = Mathf.Clamp01(_grindStrengthSmoothed);
        float traction = Mathf.Lerp(
            Mathf.Clamp01(grindMinTractionFactor),
            1f,
            buildT);

        return Mathf.Clamp01(traction * Mathf.Lerp(0.5f, 1f, strength));
    }

    private float GetGrindSkateForceScale()
    {
        if (!_grindActive)
            return 1f;

        return Mathf.Max(0f, grindSkateImpulseMultiplier) * GetGrindTraction01();
    }

    private float GetGrindPolePushScale()
    {
        if (!_grindActive)
            return 1f;

        return Mathf.Max(0f, grindPolePushMultiplier) * GetGrindTraction01();
    }

    private float GetGrindPoleBrakeScale()
    {
        if (!_grindActive)
            return 1f;

        return Mathf.Clamp01(grindPoleBrakeMultiplier);
    }

    private void ApplyGrindLowSpeedTractionAssist(float dt)
    {
        if (!_grindActive || _rb == null || _stacked || dt <= 0f)
            return;

        if (grindLowSpeedDriveAccel <= 0f || grindLowSpeedDriveMaxSpeed <= 0.01f)
            return;

        Vector3 n = _grindNormal.sqrMagnitude > 0.0001f
            ? _grindNormal.normalized
            : (_groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up);

        Vector3 planeVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, n);
        float speed = planeVelocity.magnitude;

        float speedT = 1f - Mathf.Clamp01(speed / Mathf.Max(0.01f, grindLowSpeedDriveMaxSpeed));
        if (speedT <= 0f)
            return;

        Vector3 skiDir = GetCombinedSkiForwardOnPlane();
        if (skiDir.sqrMagnitude < 0.0001f)
            skiDir = Vector3.ProjectOnPlane(transform.forward, n);

        if (skiDir.sqrMagnitude < 0.0001f)
            return;

        skiDir.Normalize();

        float leanIntent = Mathf.InverseLerp(0.05f, 0.65f, _forwardLean);
        float skateIntent = Mathf.InverseLerp(0.45f, 1f, Mathf.Abs(GetCurrentSkateInput()));
        float poleIntent = _rawPolesPressed ? 0.8f : 0f;

        float intent = Mathf.Clamp01(Mathf.Max(leanIntent, skateIntent, poleIntent));
        if (intent <= 0.001f)
            return;

        float traction = GetGrindTraction01();

        _rb.AddForce(
            skiDir * (grindLowSpeedDriveAccel * traction * speedT * intent),
            ForceMode.Acceleration);
    }

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

        float grind01 = _grindActive ? Mathf.Clamp01(_grindStrengthSmoothed) : 0f;

        // --- Per-ski anisotropic friction ---
        Vector3 friction = Vector3.zero;
        int skiCount = 0;

        AccumulateSkiFriction(leftSkiContact, velOnPlane, ref friction, ref skiCount);
        AccumulateSkiFriction(rightSkiContact, velOnPlane, ref friction, ref skiCount);

        if (skiCount > 0)
        {
            friction /= skiCount;

            if (grind01 > 0f)
            {
                float frictionMul = Mathf.Lerp(1f, grindFrictionMultiplier, grind01);
                friction *= frictionMul;
            }

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
                float tuckTurnMul = 1f - (_tuck01 * tuckTurnReduction);
                float grindSteerMul = Mathf.Lerp(1f, grindCarveSteerMultiplier, grind01);
                float steerAmount = carveFactor * (carveSteerStrength * _gearTuning.carveSteerMul) * tuckTurnMul * grindSteerMul * dt;
                Vector3 steeredDir = Vector3.Slerp(planeDir, skiDirN, steerAmount).normalized;

                // Apply steering while preserving speed (for now).
                Vector3 normalComponent = Vector3.Project(newVel, _groundNormal);
                float newPlaneSpeed = planeSpeed;

                // --- Quick Stop: extra planar damping when turning sharply at speed ---
                // Avoid Angle/acos unless the dot test says we are past the minimum angle.
                float quickStopEff =
                    quickStopStrength *
                    _gearTuning.quickStopMul *
                    (1f - (_tuck01 * tuckQuickStopReduction)) *
                    Mathf.Lerp(1f, grindQuickStopMultiplier, grind01);
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
                float brakeStrength =
                    6f *
                    _gearTuning.sideFrictionMul *
                    Mathf.Lerp(1f, grindWedgeBrakeMultiplier, grind01);

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

        bool recoveryJumpGround =
            endContactFlattenRecoveryAllowsJump &&
            IsEndContactFlattenRecoveryActive;

        bool canJumpNow = (_isGrounded || hardGrounded || _grindActive || recoveryJumpGround);

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
        bool jumpedFromGrind = false;
        Vector3 grindTan = Vector3.zero;
        Vector3 grindUp = Vector3.up;
        Vector3 grindDeltaToRail = Vector3.zero;

        if (_grindActive)
        {
            _grindActive = false;
            _grindStrengthSmoothed = 0f;
            ResetGrindingState();
        }

        // ------------------------------------------------------------------
        // 1. Compute charge-based jump strength (base force magnitude).
        // ------------------------------------------------------------------
        float holdDuration = Mathf.Max(0f, Time.time - _lastJumpPressedTime);
        float chargeT = maxJumpChargeTime > 0f
            ? Mathf.Clamp01(holdDuration / maxJumpChargeTime)
            : 1f;

        _lastSkiJumpCharge01 = chargeT;

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
        CancelEndContactFlattenRecovery();

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

    private void DetectAndApplySkatePushes(float impulseScale = 1f)
    {
        if (!IsGroundedForControls || _stacked || !HasLegInputs)
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

        impulse *= Mathf.Clamp01(impulseScale);
        if (impulse <= 0.0001f)
            return;

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

        // Grindables behave like ice: player-applied traction still works,
        // but it builds gradually and is weaker than on snow.
        if (_grindActive)
            impulse *= GetGrindSkateForceScale();

        if (impulse <= 0.0001f)
            return;

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
    void ApplyPoleForces(float forceScale = 1f)
    {
        // Only apply pole forces when the rider has grounded-style control support.
        if (!IsGroundedForControls || !HasPolesInput)
            return;

        // No forces from idle.
        if (_polePhase == PoleStrokePhase.Idle)
            return;

        forceScale = Mathf.Clamp01(forceScale);
        if (forceScale <= 0.0001f)
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
                        * speedFactor
                        * forceScale;

                    if (_grindActive)
                        accel *= GetGrindPolePushScale();

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
                             * (0.7f + 0.3f * (1f - leanT))
                             * forceScale;

                        if (_grindActive)
                            brake *= GetGrindPoleBrakeScale();

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
                        * speedFactor
                        * forceScale;

                    if (_grindActive)
                        impulse *= GetGrindPolePushScale();

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

    private void UpdateSkiSupportNormalAuthority(float dt)
    {
        if (_stacked)
            return;

        bool canUseSupportNormal =
            HasAnySkiCollisionContact ||
            _hasNonSkiGroundContact ||
            _skiEndSlideState != SkiEndSlideState.None ||
            IsEndContactFlattenRecoveryActive ||
            _isGrounded;

        if (!canUseSupportNormal)
            return;

        if (!TryResolveSkiSupportNormal(out Vector3 targetNormal, out string source))
        {
            if (Time.time - _lastResolvedSkiSupportNormalTime <= Mathf.Max(0f, skiSupportNormalCacheTime) &&
                _resolvedSkiSupportNormal.sqrMagnitude > 0.0001f)
            {
                targetNormal = _resolvedSkiSupportNormal.normalized;
                source = "cached";
            }
            else
            {
                return;
            }
        }

        if (!IsRideableNormal(targetNormal))
            return;

        float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, skiSupportNormalResolveSpeed) * Mathf.Max(0f, dt));

        if (_resolvedSkiSupportNormal.sqrMagnitude <= 0.0001f)
            _resolvedSkiSupportNormal = targetNormal.normalized;
        else
            _resolvedSkiSupportNormal = Vector3.Slerp(_resolvedSkiSupportNormal, targetNormal.normalized, t).normalized;

        _lastResolvedSkiSupportNormalTime = Time.time;
        _lastResolvedSkiSupportNormalSource = source;

        // Feed both the physics plane and visual pitch/roll plane. This is intentionally
        // separate from grounded authority: it only corrects slope alignment.
        _groundNormal = Vector3.Slerp(
            _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up,
            _resolvedSkiSupportNormal,
            t).normalized;

        _alignNormal = Vector3.Slerp(
            _alignNormal.sqrMagnitude > 0.0001f ? _alignNormal.normalized : Vector3.up,
            _resolvedSkiSupportNormal,
            t).normalized;
    }

    private bool TryResolveSkiSupportNormal(out Vector3 normal, out string source)
    {
        Vector3 weighted = Vector3.zero;
        float totalWeight = 0f;
        bool usedSki = false;
        string resolvedSource = "none";

        void AddNormal(Vector3 n, float weight, string label)
        {
            if (n.sqrMagnitude <= 0.0001f || weight <= 0f)
                return;

            n = n.normalized;
            if (!IsRideableNormal(n))
                return;

            // Overlap-poll collision normals can be world-up. Keep them as weak evidence;
            // terrain/probe normals should dominate slope alignment.
            float slopeAngle = Vector3.Angle(n, Vector3.up);
            float adjustedWeight = slopeAngle < 1f ? weight * 0.15f : weight;

            weighted += n * adjustedWeight;
            totalWeight += adjustedWeight;

            if (string.IsNullOrEmpty(resolvedSource) || resolvedSource == "none")
                resolvedSource = label;
            else if (!resolvedSource.Contains(label))
                resolvedSource += "+" + label;
        }

        void AddSkiContactNormal(SkiContact contact, string prefix)
        {
            if (contact == null)
                return;

            if (contact.HasCollisionContact)
            {
                AddNormal(contact.ContactNormal, 1.0f, prefix + "-collision");
                usedSki = true;
            }

            if (contact.ProbeBaseHit)
            {
                AddNormal(contact.ProbeBaseNormal, 3.0f, prefix + "-base-probe");
                usedSki = true;
            }

            if (contact.ProbeTipHit)
            {
                float w = contact.HasEndContact ? 2.0f : 1.0f;
                AddNormal(contact.ProbeTipNormal, w, prefix + "-tip-probe");
                usedSki = true;
            }

            if (contact.ProbeTailHit)
            {
                float w = contact.HasEndContact ? 2.0f : 1.0f;
                AddNormal(contact.ProbeTailNormal, w, prefix + "-tail-probe");
                usedSki = true;
            }
        }

        AddSkiContactNormal(leftSkiContact, "left");
        AddSkiContactNormal(rightSkiContact, "right");

        bool terrainRayAvailable =
            _lastGroundProbeAcceptedCollider != null &&
            _lastGroundProbeAcceptedNormal.sqrMagnitude > 0.0001f &&
            IsRideableNormal(_lastGroundProbeAcceptedNormal);

        Vector3 terrainRayNormal = terrainRayAvailable
            ? _lastGroundProbeAcceptedNormal.normalized
            : Vector3.up;

        if (terrainRayAvailable)
        {
            // The root/body ray often has the correct terrain normal even when ski collision
            // support came from overlap polling and ContactNormal is world-up.
            float raySlope = Vector3.Angle(terrainRayNormal, Vector3.up);
            float skiSlope = totalWeight > 0.0001f && weighted.sqrMagnitude > 0.0001f
                ? Vector3.Angle(weighted.normalized, Vector3.up)
                : 0f;

            float rayWeight = 1.25f;

            if (usedSki && raySlope >= terrainRayNormalOverrideAngle && skiSlope < terrainRayNormalOverrideAngle)
                rayWeight = 5.0f;
            else if (!usedSki)
                rayWeight = 3.0f;

            AddNormal(terrainRayNormal, rayWeight, "ground-ray");
        }

        if (totalWeight <= 0.0001f || weighted.sqrMagnitude <= 0.0001f)
        {
            normal = Vector3.up;
            source = "none";
            return false;
        }

        normal = weighted.normalized;
        source = resolvedSource;

        // Final override: if weighted normal is still basically world-up but the accepted
        // terrain ray is clearly sloped, use the terrain normal. This specifically fixes
        // overlap-poll contacts visually flattening onto world-up.
        if (terrainRayAvailable &&
            Vector3.Angle(normal, Vector3.up) < terrainRayNormalOverrideAngle &&
            Vector3.Angle(terrainRayNormal, Vector3.up) >= terrainRayNormalOverrideAngle)
        {
            normal = terrainRayNormal;
            source = "ground-ray-override";
        }

        return true;
    }
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
        float grind01 = _grindActive ? Mathf.Clamp01(_grindStrengthSmoothed) : 0f;
        float endSlideSuppress = 0f;

        if (enableNoseTailSlides && Time.time <= _lastSkiEndSlideTime + noseTailSlideStateGrace)
        {
            float effectiveSuppression = Mathf.Clamp01(
                noseTailSlideAlignmentSuppression +
                (grindNoseTailAlignmentSuppressionBonus * grind01));

            endSlideSuppress = Mathf.Clamp01(_skiEndSlide01 * effectiveSuppression);
        }

        float alignmentMul = 1f - endSlideSuppress;
        float planarSpeedForFlatten = Vector3.ProjectOnPlane(_rb.linearVelocity, desiredUp).magnitude;
        if (planarSpeedForFlatten <= noseTailSlideFlattenBelowSpeed)
        {
            alignmentMul = 1f;
            _skiEndSlideState = SkiEndSlideState.None;
            _skiEndSlide01 = 0f;
        }

        float maxUpStep = (alignMaxDegreesPerSec * landingBoost * Mathf.Max(0.05f, alignmentMul)) * Time.fixedDeltaTime;
        current = Quaternion.RotateTowards(current, upAligned, maxUpStep);


        // ------------------------------------------------------------
        // 2) Yaw turning: keep your stability-scaled yaw response.
        // ------------------------------------------------------------
        float baseAlign = 0f;
        if (leftSkiContact != null && leftSkiContact.IsGrounded) baseAlign = Mathf.Max(baseAlign, leftSkiContact.BaseContactAlignment);
        if (rightSkiContact != null && rightSkiContact.IsGrounded) baseAlign = Mathf.Max(baseAlign, rightSkiContact.BaseContactAlignment);

        float stability = Mathf.Clamp01((baseAlign - 0.15f) / (0.6f - 0.15f));
        float yawSpeed = ((groundTurnSpeed * _gearTuning.turnSpeedMul) * stability) * landingBoost;
        yawSpeed *= Mathf.Lerp(1f, 0.35f, grind01);

        Quaternion yawTarget = Quaternion.LookRotation(desiredForward, desiredUp);

        if (TryGetIntentionalTipBalance(out int tipBalanceSign, out float tipBalance01, out _))
        {
            Vector3 pitchAxis = yawTarget * Vector3.right;
            yawTarget = Quaternion.AngleAxis(tipBalancePitchBias * tipBalanceSign * tipBalance01, pitchAxis) * yawTarget;
        }

        float yawT = 1f - Mathf.Exp(-yawSpeed * Time.fixedDeltaTime);
        current = Quaternion.Slerp(current, yawTarget, yawT);

        if (_grindHybridAngularVelocity.sqrMagnitude > 0.0001f)
        {
            Quaternion yawRot = Quaternion.AngleAxis(_grindHybridAngularVelocity.y * Time.fixedDeltaTime, desiredUp);
            Quaternion pitchRot = Quaternion.AngleAxis(_grindHybridAngularVelocity.x * Time.fixedDeltaTime, current * Vector3.right);
            Quaternion rollRot = Quaternion.AngleAxis(_grindHybridAngularVelocity.z * Time.fixedDeltaTime, current * Vector3.forward);
            current = yawRot * pitchRot * rollRot * current;
        }

        _rb.MoveRotation(current);
    }

    private void ApplyGrindHybridOrientationControl(float dt)
    {
        if (_rb == null || dt <= 0f)
            return;

        if (!_grindActive || _stacked)
        {
            _grindHybridAngularVelocity = Vector3.MoveTowards(
                _grindHybridAngularVelocity,
                Vector3.zero,
                grindMaxHybridAngularSpeed * 4f * dt);
            return;
        }

        float grind01 = Mathf.Clamp01(_grindStrengthSmoothed);
        if (grind01 <= 0f)
            return;

        float tuckMul = 1f + (_tuck01 * grindTuckControlBonus);

        float yawInput = Mathf.Clamp(_rawRightLegInput - _rawLeftLegInput, -1f, 1f);
        float pitchInput = Mathf.Clamp(_forwardLean, -1f, 1f);
        float rollInput = Mathf.Clamp(_sideLean, -1f, 1f);

        Vector3 targetAngularVelocity = new Vector3(
            -pitchInput * grindPitchTorque,
            yawInput * grindYawTorque,
            -rollInput * grindRollTorque) * (grind01 * tuckMul);

        float response = Mathf.Max(grindYawTorque, Mathf.Max(grindPitchTorque, grindRollTorque));
        _grindHybridAngularVelocity = Vector3.MoveTowards(
            _grindHybridAngularVelocity,
            targetAngularVelocity,
            response * dt);

        if (_grindHybridAngularVelocity.magnitude > grindMaxHybridAngularSpeed)
            _grindHybridAngularVelocity = _grindHybridAngularVelocity.normalized * grindMaxHybridAngularSpeed;

        // The final rotation application happens inside AlignToSkisAndSlope so
        // grind control layers onto grounded alignment instead of fighting it.
    }

    // ----------------------------------------------------------------------
    // AIR CONTROL
    // ----------------------------------------------------------------------
    private void BeginAirEntry(bool resetYaw)
    {
        _airEntryTime = Time.time;
        _airLeanBaseline = _rawLeanInput;

        _landingEvaluationConsumedForCurrentAirborne = false;

        // Prevent immediate forward tipping when the player is already leaning at takeoff.
        // Pitch control in air will be driven by delta-from-baseline (see ApplyAirControl).
        _airAngularVelocity.x = 0f;

        if (resetYaw)
            _airAngularVelocity.y = 0f;
    }

    private AerialStyleMode ResolveAerialStyleMode()
    {
        if (!IsAirPoseActive)
            return AerialStyleMode.None;

        AerialPoseFamily family = ResolvePoseFamily();
        AerialPoseShape shape = ResolvePoseShape();

        if (family == AerialPoseFamily.Left || family == AerialPoseFamily.Right)
            return AerialStyleMode.Tweaked;

        if (family == AerialPoseFamily.Spread || shape == AerialPoseShape.LaidOut)
            return AerialStyleMode.Stretched;

        return AerialStyleMode.Stylish;
    }

    private AerialPoseFamily ResolvePoseFamily()
    {
        if (!IsAirPoseActive)
            return AerialPoseFamily.None;

        return ResolvePoseFamilyFromInputs();
    }

    private AerialPoseFamily ResolvePoseFamilyFromInputs()
    {
        const float activeThreshold = 0.2f;
        bool leftActive = _rawLeftLegInput > activeThreshold;
        bool rightActive = _rawRightLegInput > activeThreshold;

        if (leftActive && rightActive)
            return AerialPoseFamily.Spread;

        if (leftActive)
            return AerialPoseFamily.Left;

        if (rightActive)
            return AerialPoseFamily.Right;

        return AerialPoseFamily.Neutral;
    }

    private AerialPoseShape ResolvePoseShape()
    {
        if (!IsAirPoseActive)
            return AerialPoseShape.None;

        return ResolvePoseShapeFromInputs();
    }

    private AerialPoseShape ResolvePoseShapeFromInputs()
    {
        if (_tuck01 >= 0.55f)
            return AerialPoseShape.Compact;

        if (_rawLeanInput >= 0.35f)
            return AerialPoseShape.Driving;

        if (_rawLeanInput <= -0.35f)
            return AerialPoseShape.LaidOut;

        return AerialPoseShape.Neutral;
    }

    private AerialOrientationModifier ResolvePoseOrientationModifier()
    {
        if (!IsAirPoseActive)
            return AerialOrientationModifier.None;

        return ResolvePoseOrientationModifierFromState();
    }

    private TrickPoseVerticalOrientationRequirement ResolvePoseVerticalOrientation()
    {
        return TrickPoseOrientationUtility.DeriveVertical(transform.rotation, IsAirPoseActive);
    }

    private TrickPoseHorizontalOrientationRequirement ResolvePoseHorizontalOrientation()
    {
        return TrickPoseOrientationUtility.DeriveHorizontal(transform.rotation, IsAirPoseActive);
    }

    private TrickPoseTravelFacingRequirement ResolvePoseTravelFacing()
    {
        return TrickPoseOrientationUtility.DeriveTravelFacing(
            transform.rotation,
            _rb != null ? _rb.linearVelocity : Vector3.zero,
            _groundNormal,
            IsAirPoseActive);
    }

    private TrickPoseMotionStateRequirement ResolvePoseMotionState()
    {
        if (!IsAirPoseActive)
            return TrickPoseMotionStateRequirement.Any;

        if (_rb.linearVelocity.y > 1.75f)
            return TrickPoseMotionStateRequirement.Rising;
        if (_rb.linearVelocity.y < -2.5f)
            return TrickPoseMotionStateRequirement.Diving;

        return TrickPoseMotionStateRequirement.Any;
    }

    private AerialOrientationModifier ResolvePoseOrientationModifierFromState()
    {
        Vector3 up = transform.up;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        if (up.y < -0.2f)
            return AerialOrientationModifier.Inverted;

        if (forward.y < -0.45f)
            return AerialOrientationModifier.ChestDown;

        if (forward.y > 0.45f)
            return AerialOrientationModifier.ChestUp;

        if (Mathf.Abs(right.y) > 0.55f)
            return AerialOrientationModifier.OnSide;

        Vector3 groundNormal = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
        Vector3 velOnPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, groundNormal);
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(forward, groundNormal);

        if (velOnPlane.sqrMagnitude > 0.01f && fwdOnPlane.sqrMagnitude > 0.01f)
        {
            float rawAngle = Vector3.Angle(fwdOnPlane.normalized, velOnPlane.normalized);

            if (rawAngle > 135f)
                return AerialOrientationModifier.Switch;

            float sideDot = Mathf.Abs(Vector3.Dot(fwdOnPlane.normalized, velOnPlane.normalized));
            if (sideDot < 0.35f)
                return AerialOrientationModifier.Sideways;
        }

        if (_rb.linearVelocity.y > 1.75f)
            return AerialOrientationModifier.Rising;

        if (_rb.linearVelocity.y < -2.5f)
            return AerialOrientationModifier.Diving;

        return AerialOrientationModifier.None;
    }

    private string ResolvePoseName()
    {
        if (!IsAirPoseActive)
            return string.Empty;

        AerialPoseFamily family = ResolvePoseFamily();
        AerialPoseShape shape = ResolvePoseShape();

        return ResolvePoseName(family, shape);
    }

    private static string ResolvePoseName(AerialPoseFamily family, AerialPoseShape shape)
    {
        return (family, shape) switch
        {
            (AerialPoseFamily.Left, AerialPoseShape.Compact) => "Mantis",
            (AerialPoseFamily.Left, AerialPoseShape.Driving) => "Harpoon",
            (AerialPoseFamily.Left, AerialPoseShape.LaidOut) => "High Hook",
            (AerialPoseFamily.Left, AerialPoseShape.Neutral) => "Scarecrow",

            (AerialPoseFamily.Right, AerialPoseShape.Compact) => "Crane",
            (AerialPoseFamily.Right, AerialPoseShape.Driving) => "Stinger",
            (AerialPoseFamily.Right, AerialPoseShape.LaidOut) => "Sidearm",
            (AerialPoseFamily.Right, AerialPoseShape.Neutral) => "Crowbar",

            (AerialPoseFamily.Spread, AerialPoseShape.Compact) => "Foldover",
            (AerialPoseFamily.Spread, AerialPoseShape.Driving) => "Cathedral",
            (AerialPoseFamily.Spread, AerialPoseShape.LaidOut) => "High Wire",
            (AerialPoseFamily.Spread, AerialPoseShape.Neutral) => "Split Rail",

            (AerialPoseFamily.Neutral, AerialPoseShape.Compact) => "Deadbolt",
            (AerialPoseFamily.Neutral, AerialPoseShape.Driving) => "Dart",
            (AerialPoseFamily.Neutral, AerialPoseShape.LaidOut) => "Dead Sail",
            (AerialPoseFamily.Neutral, AerialPoseShape.Neutral) => "Drifter",

            _ => string.Empty
        };
    }

    private static Vector3 NormalizeEulerAngles(Vector3 eulerAngles)
    {
        return new Vector3(
            TrickPoseEulerAngleRange.NormalizeSignedAngle(eulerAngles.x),
            TrickPoseEulerAngleRange.NormalizeSignedAngle(eulerAngles.y),
            TrickPoseEulerAngleRange.NormalizeSignedAngle(eulerAngles.z));
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

    private Vector3 GetPreferredGrindDirection()
    {
        Vector3 vel = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
        if (vel.sqrMagnitude > 0.01f)
            return vel.normalized;

        Vector3 skiForward = GetCombinedSkiForwardOnPlane();
        if (skiForward.sqrMagnitude > 0.0001f)
            return skiForward.normalized;

        return transform.forward;
    }

    private void ApplyAirControl()
    {
        float dt = Time.fixedDeltaTime;

        // If controls consider us grounded (including grinding), do NOT apply air controls.
        // Instead gently damp any carried air angular velocity.
        if (IsGroundedForControls)
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

        float spinMultiplier = Mathf.Lerp(1f, airTuckSpinMultiplier, _tuck01);

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


    private void UpdateGrinding(float dt)
    {
        if (dt <= 0f)
            return;

        UpdateSimpleContactGrinding(dt);
    }

    private void UpdateSimpleContactGrinding(float dt)
    {
        if (dt <= 0f)
            return;

        if (_stacked)
        {
            ResetGrindingState();
            return;
        }

        Vector3 groundUp = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        Vector3 planarVelocity = _rb != null
            ? Vector3.ProjectOnPlane(_rb.linearVelocity, groundUp)
            : Vector3.zero;

        float planarSpeed = planarVelocity.magnitude;

        bool hasContact = TryGetSimpleGrindSurface(
            out Collider grindCollider,
            out Vector3 closestPoint,
            out Vector3 tangent,
            out GrindSurfaceKind surfaceKind,
            out string sourceName);

        if (!hasContact)
        {
            ResetGrindingState();
            return;
        }

        bool speedAllowsDistance = planarSpeed >= Mathf.Max(0f, grindMinPlanarSpeed);

        if (!speedAllowsDistance && !grindAllowsStationaryContactSupport)
        {
            ResetGrindingState();
            return;
        }

        bool newSurface = !_grindActive || _grindActiveCollider != grindCollider;
        if (newSurface)
        {
            _grindTime = 0f;
            _grindDistance = 0f;
            _lastGrindClosestPoint = closestPoint;
            _grindStance = GrindStance.Centered;
            _grindStanceTimer = 0f;
        }

        _grindActive = true;
        _grindStrengthSmoothed = Mathf.MoveTowards(
            _grindStrengthSmoothed,
            1f,
            grindStrengthResponse * dt);

        _grindTime += dt;

        _grindActiveCollider = grindCollider;
        _grindClosestPoint = closestPoint;
        _grindSurfaceKind = surfaceKind;
        _dbgGrindSource = sourceName;

        if (tangent.sqrMagnitude > 0.0001f)
            _grindTangent = tangent.normalized;
        else
            _grindTangent = GetPreferredGrindDirection();

        _grindNormal = groundUp;
        _grindDeltaToRail = closestPoint - GetGrindReferencePoint();

        if (speedAllowsDistance)
        {
            UpdateSimpleGrindDistance(dt, closestPoint);
        }
        else
        {
            // Allow grounded-style grind support while stationary/slow, but do not
            // inflate trick distance until the player actually moves.
            _lastGrindClosestPoint = closestPoint;
            _grindRawEndSign = ResolveCurrentGrindEndSign();
        }

        UpdateGrindStance(dt);
        _grindDominantEndSign = (int)_grindStance;
    }

    private void UpdateSimpleGrindDistance(float dt, Vector3 closestPoint)
    {
        if (_grindTime <= Mathf.Epsilon)
        {
            _lastGrindClosestPoint = closestPoint;
            return;
        }

        Vector3 delta = closestPoint - _lastGrindClosestPoint;

        if (delta.sqrMagnitude > 0.000001f)
        {
            if (_grindTangent.sqrMagnitude > 0.0001f)
                _grindDistance += Mathf.Abs(Vector3.Dot(delta, _grindTangent.normalized));
            else
                _grindDistance += delta.magnitude;
        }
        else
        {
            Vector3 n = _groundNormal.sqrMagnitude > 0.0001f ? _groundNormal.normalized : Vector3.up;
            _grindDistance += Vector3.ProjectOnPlane(_rb.linearVelocity, n).magnitude * dt;
        }

        _lastGrindClosestPoint = closestPoint;
        _grindRawEndSign = ResolveCurrentGrindEndSign();
    }

    private bool TryGetSimpleGrindSurface(
    out Collider activeCollider,
    out Vector3 closestPoint,
    out Vector3 tangent,
    out GrindSurfaceKind surfaceKind,
    out string sourceName)
    {
        activeCollider = null;
        closestPoint = GetGrindReferencePoint();
        tangent = Vector3.zero;
        surfaceKind = GrindSurfaceKind.None;
        sourceName = string.Empty;

        Transform leftRef = leftSkiContact != null ? leftSkiContact.transform : leftSki;
        Transform rightRef = rightSkiContact != null ? rightSkiContact.transform : rightSki;

        Vector3 leftReference = leftRef != null ? leftRef.position : transform.position;
        Vector3 rightReference = rightRef != null ? rightRef.position : transform.position;
        Vector3 combinedReference = (leftReference + rightReference) * 0.5f;

        Collider leftCollider = GetBestGrindableColliderFromSki(leftSkiContact, leftReference);
        Collider rightCollider = GetBestGrindableColliderFromSki(rightSkiContact, rightReference);

        Collider best = null;

        if (leftCollider != null && rightCollider != null)
        {
            if (leftCollider == rightCollider)
            {
                best = leftCollider;
            }
            else
            {
                float leftScore =
                    Vector3.Distance(leftCollider.ClosestPoint(leftReference), leftReference) +
                    Vector3.Distance(leftCollider.ClosestPoint(combinedReference), combinedReference);

                float rightScore =
                    Vector3.Distance(rightCollider.ClosestPoint(rightReference), rightReference) +
                    Vector3.Distance(rightCollider.ClosestPoint(combinedReference), combinedReference);

                best = leftScore <= rightScore ? leftCollider : rightCollider;
            }
        }
        else
        {
            best = leftCollider != null ? leftCollider : rightCollider;
        }

        if (best == null)
            return false;

        activeCollider = best;
        ResolveSimpleGrindContactData(best, combinedReference, out closestPoint, out tangent, out surfaceKind, out sourceName);
        return true;
    }

    private Collider GetBestGrindableColliderFromSki(SkiContact contact, Vector3 reference)
    {
        if (contact == null)
            return null;

        Collider best = null;
        float bestScore = float.PositiveInfinity;
        string bestSource = "none";
        _dbgGrindRejectReason = "no-candidate";

        void Consider(Collider c, float priorityBonus, Vector3 refPoint, string source)
        {
            if (c == null)
                return;

            if (!IsColliderSimpleGrindable(c))
            {
                _dbgGrindRejectReason = $"candidate-not-grindable:{c.name}";
                return;
            }

            Vector3 closest = c.ClosestPoint(refPoint);
            float dist = Vector3.Distance(closest, refPoint);
            float score = dist - priorityBonus;

            if (score < bestScore)
            {
                bestScore = score;
                best = c;
                bestSource = source;
                _dbgGrindRejectReason = "accepted";
            }
        }

        void ConsiderNearbyOverlap(Vector3 point, float priorityBonus, string source)
        {
            if (!useNearbyGrindableProbeSearch)
                return;

            float radius = Mathf.Max(0.01f, grindContactProbeRadius);

            int count = Physics.OverlapSphereNonAlloc(
                point,
                radius,
                _grindOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            if (count <= 0)
            {
                if (_dbgGrindRejectReason == "no-candidate")
                    _dbgGrindRejectReason = $"{source}:no-overlap";
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Collider c = _grindOverlapBuffer[i];
                _grindOverlapBuffer[i] = null;

                if (c == null)
                    continue;

                Consider(c, priorityBonus, point, source + "-overlap");
            }
        }

        void ConsiderNearbyCast(Vector3 point, float priorityBonus, string source)
        {
            if (!useNearbyGrindableProbeSearch)
                return;

            float radius = Mathf.Max(0.01f, grindContactProbeRadius);
            float up = Mathf.Max(0.01f, grindContactProbeUp);
            float down = Mathf.Max(0.01f, grindContactProbeDown);

            Vector3 origin = point + Vector3.up * up;
            float maxDistance = up + down;

            int count = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                _grindHitBuffer,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Collide);

            if (count <= 0)
            {
                if (_dbgGrindRejectReason == "no-candidate")
                    _dbgGrindRejectReason = $"{source}:no-cast-hit";
                return;
            }

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _grindHitBuffer[i];
                _grindHitBuffer[i] = default;

                Collider c = hit.collider;
                if (c == null)
                    continue;

                // Use the hit point as the reference so broad flat surfaces score correctly.
                Vector3 refPoint = hit.point.sqrMagnitude > 0.0001f ? hit.point : point;
                Consider(c, priorityBonus + 0.04f, refPoint, source + "-cast");
            }
        }

        void ConsiderProbePoint(Vector3 point, float priorityBonus, string source)
        {
            ConsiderNearbyOverlap(point, priorityBonus, source);
            ConsiderNearbyCast(point, priorityBonus, source);
        }

        // 1. Existing direct contact paths.
        Consider(contact.PrimaryContactCollider, 0.35f, reference, "primary-contact");

        foreach (Collider c in contact.AnyCollisionOtherColliders)
        {
            if (contact.HasCurrentContactWith(c))
                Consider(c, 0.05f, reference, "any-contact");
        }

        // 2. Probe/ski searches. Overlap catches true contact; spherecast catches the
        // large flat Default-layer surface case where the skier is visually standing
        // on a grindable but no ski snow-ground probe/collision is active.
        ConsiderProbePoint(reference, nearbyGrindableProbePriority, "ski-transform");

        ConsiderProbePoint(
            contact.GetProbeWorldPosition(SkiContact.SkiProbeRegion.Mid),
            nearbyGrindableProbePriority + 0.08f,
            "base-probe");

        ConsiderProbePoint(
            contact.GetProbeWorldPosition(SkiContact.SkiProbeRegion.Front),
            nearbyGrindableProbePriority,
            "tip-probe");

        ConsiderProbePoint(
            contact.GetProbeWorldPosition(SkiContact.SkiProbeRegion.Rear),
            nearbyGrindableProbePriority,
            "tail-probe");

        _dbgGrindCandidateSource = best != null ? bestSource : "none";
        return best;
    }

    private bool IsColliderSimpleGrindable(Collider c)
    {
        if (c == null)
            return false;

        if (IsOwnCollider(c) || IsCharacterCollider(c))
            return false;

        bool layerOk = (grindableLayers.value & (1 << c.gameObject.layer)) != 0;
        if (layerOk)
            return true;

        ResolveGrindProviders(c, out LiftLine liftLine, out FencePath fencePath);
        return liftLine != null || fencePath != null;
    }

    private void ResolveSimpleGrindContactData(
        Collider c,
        Vector3 referencePoint,
        out Vector3 closestPoint,
        out Vector3 tangent,
        out GrindSurfaceKind surfaceKind,
        out string sourceName)
    {
        closestPoint = referencePoint;
        tangent = Vector3.zero;
        surfaceKind = GrindSurfaceKind.ImplicitSurface;
        sourceName = c != null ? c.name : string.Empty;

        if (c == null)
            return;

        ResolveGrindProviders(c, out LiftLine ll, out FencePath fp);

        if (ll != null)
        {
            if (ll.TryGetClosestPointOnBand(referencePoint, out _, out Vector3 cp, out Vector3 tan))
            {
                closestPoint = cp;
                tangent = tan.sqrMagnitude > 0.0001f ? tan.normalized : GetPreferredGrindDirection();
                surfaceKind = GrindSurfaceKind.LiftLine;
                sourceName = ll.name;
                return;
            }
        }

        //if (fp != null)
        //{
        //    if (fp.TryGetClosestPointOnPath(referencePoint, out _, out Vector3 cp, out Vector3 tan))
        //    {
        //        closestPoint = cp;
        //        tangent = tan.sqrMagnitude > 0.0001f ? tan.normalized : GetPreferredGrindDirection();
        //        surfaceKind = GrindSurfaceKind.FencePath;
        //        sourceName = fp.name;
        //        return;
        //    }
        //}

        closestPoint = c.ClosestPoint(referencePoint);

        Vector3 planarVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
        if (planarVelocity.sqrMagnitude > 0.0001f)
        {
            tangent = planarVelocity.normalized;
            return;
        }

        tangent = GetPreferredGrindDirection();
    }

    private void UpdateGrindStance(float dt)
    {
        if (!_grindActive || dt <= 0f)
        {
            _grindStance = GrindStance.Centered;
            _grindStanceTimer = 0f;
            return;
        }

        int targetSign = 0;
        float lean = _forwardLean;

        if (_grindRawEndSign > 0 && lean >= grindStanceLeanThreshold)
            targetSign = 1;
        else if (_grindRawEndSign < 0 && lean <= -grindStanceLeanThreshold)
            targetSign = -1;

        if (targetSign == 0)
        {
            _grindStanceTimer = Mathf.MoveTowards(_grindStanceTimer, 0f, dt / Mathf.Max(0.01f, grindStanceReleaseTime));
            if (_grindStanceTimer <= 0.0001f)
                _grindStance = GrindStance.Centered;
            return;
        }

        if ((int)_grindStance == targetSign)
        {
            _grindStanceTimer = Mathf.Min(grindStanceHoldTime, _grindStanceTimer + dt);
            return;
        }

        if (_grindStance != GrindStance.Centered && (int)_grindStance != targetSign)
            _grindStanceTimer = 0f;

        _grindStanceTimer += dt;
        if (_grindStanceTimer >= grindStanceHoldTime)
        {
            _grindStance = targetSign > 0 ? GrindStance.Nose : GrindStance.Tail;
            _grindStanceTimer = grindStanceHoldTime;
        }
    }

    private int ResolveCurrentGrindEndSign()
    {
        int signSum = 0;
        int contributors = 0;

        if (TryGetGrindEndSignForSki(leftSkiContact, out int leftSign))
        {
            signSum += leftSign;
            contributors++;
        }

        if (TryGetGrindEndSignForSki(rightSkiContact, out int rightSign))
        {
            signSum += rightSign;
            contributors++;
        }

        if (contributors == 0 || Mathf.Abs(signSum) != contributors)
            return 0;

        return signSum > 0 ? 1 : -1;
    }

    private bool TryGetGrindEndSignForSki(SkiContact contact, out int sign)
    {
        sign = 0;
        if (contact == null || !_grindActive)
            return false;

        float midDist = ComputeDistanceToCurrentGrind(contact.GetProbeWorldPosition(SkiContact.SkiProbeRegion.Mid));
        float frontDist = ComputeDistanceToCurrentGrind(contact.GetProbeWorldPosition(SkiContact.SkiProbeRegion.Front));
        float rearDist = ComputeDistanceToCurrentGrind(contact.GetProbeWorldPosition(SkiContact.SkiProbeRegion.Rear));

        if (frontDist + grindEndProbeBias < midDist && frontDist + grindEndProbeBias < rearDist)
        {
            sign = 1;
            return true;
        }

        if (rearDist + grindEndProbeBias < midDist && rearDist + grindEndProbeBias < frontDist)
        {
            sign = -1;
            return true;
        }

        return false;
    }

    private float ComputeDistanceToCurrentGrind(Vector3 worldPoint)
    {
        if (_grindActiveCollider == null)
            return float.PositiveInfinity;

        if (_grindSurfaceKind == GrindSurfaceKind.LiftLine)
        {
            ResolveGrindProviders(_grindActiveCollider, out LiftLine ll, out _);
            if (ll != null && ll.TryGetClosestPointOnBand(worldPoint, out _, out Vector3 cp, out _))
                return Vector3.Distance(worldPoint, cp);
        }
        //else if (_grindSurfaceKind == GrindSurfaceKind.FencePath)
        //{
        //    ResolveGrindProviders(_grindActiveCollider, out _, out FencePath fp);
        //    if (fp != null && fp.TryGetClosestPointOnPath(worldPoint, out _, out Vector3 cp, out _))
        //        return Vector3.Distance(worldPoint, cp);
        //}

        Vector3 cpImplicit = _grindActiveCollider.ClosestPoint(worldPoint);
        return Vector3.Distance(worldPoint, cpImplicit);
    }

    private void ResetGrindingState()
    {
        _grindActive = false;
        _grindStrengthSmoothed = 0f;
        _grindTime = 0f;
        _grindDistance = 0f;
        _grindActiveCollider = null;
        _grindClosestPoint = Vector3.zero;
        _lastGrindClosestPoint = Vector3.zero;
        _grindTangent = Vector3.zero;
        _grindNormal = Vector3.up;
        _grindDeltaToRail = Vector3.zero;
        _grindSurfaceKind = GrindSurfaceKind.None;
        _dbgGrindSource = string.Empty;
        _grindRawEndSign = 0;
        _grindDominantEndSign = 0;
        _grindStance = GrindStance.Centered;
        _grindStanceTimer = 0f;
        _grindHybridAngularVelocity = Vector3.zero;
    }

    private bool IsTouchingGrindable(out Collider touchCollider)
    {
        touchCollider = null;

        Vector3 leftRef = leftSkiContact != null ? leftSkiContact.transform.position : transform.position;
        Vector3 rightRef = rightSkiContact != null ? rightSkiContact.transform.position : transform.position;

        Collider left = GetBestGrindableColliderFromSki(leftSkiContact, leftRef);
        Collider right = GetBestGrindableColliderFromSki(rightSkiContact, rightRef);

        if (left != null && right != null)
        {
            touchCollider = left;
            return true;
        }

        touchCollider = left != null ? left : right;
        return touchCollider != null;
    }

    private bool TryGetTouchingGrindable(SkiContact contact, out Collider touchCollider)
    {
        touchCollider = null;

        Vector3 reference = contact != null ? contact.transform.position : transform.position;
        touchCollider = GetBestGrindableColliderFromSki(contact, reference);
        return touchCollider != null;
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
    private static string DescribeColliderForDebug(Collider c)
    {
        if (c == null)
            return "(none)";

        int layer = c.gameObject.layer;
        return $"{c.name}/{LayerMask.LayerToName(layer)}";
    }

    private static string DescribeLayerMaskForDebug(LayerMask mask)
    {
        if (mask.value == 0)
            return "0 (none)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(mask.value);
        sb.Append(" (");
        bool wrote = false;
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) == 0)
                continue;

            if (wrote)
                sb.Append(", ");

            string layerName = LayerMask.LayerToName(i);
            sb.Append(string.IsNullOrEmpty(layerName) ? i.ToString() : layerName);
            wrote = true;
        }
        sb.Append(')');
        return sb.ToString();
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
        GUILayout.Label($"physicsGrounded: {_isGrounded}    controlGrounded: {IsGroundedForControls}    HasAnySkiContact: {HasAnySkiContact}");
        GUILayout.Label($"bodyNearGround: {_bodyNearGround}    skiPlausible: {_skiContactPlausibleForBody}    groundGap: {_lastMeasuredGroundGap:F2}    refresh: {_lastGroundRefreshSource}");

        Vector3 velPlane = Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal);
        GUILayout.Label($"PlanarSpeed: {velPlane.magnitude:F2} m/s    ForwardLean: {_forwardLean:F2}");
        GUILayout.Label($"EndSlide: {_skiEndSlideState}    EndSlide01: {_skiEndSlide01:F2}");
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

            bool touchingGrindableHud = IsTouchingGrindable(out Collider touchColHud);
            string touchName = touchColHud != null ? touchColHud.name : "(none)";
            string activeName = _grindActiveCollider != null ? _grindActiveCollider.name : "(none)";
            GUILayout.Label($"TouchingGrindable: {touchingGrindableHud}    IsGrinding: {_grindActive}    GroundedForControls: {IsGroundedForControls}");
            GUILayout.Label($"ActiveCollider: {activeName}    TouchCollider: {touchName}");
            GUILayout.Label($"Source: {(_dbgGrindSource ?? "(none)")}    CandidateSource: {_dbgGrindCandidateSource}    Reject: {_dbgGrindRejectReason}");
            GUILayout.Label($"Distance: {_grindDistance:F2} m    Strength: {_grindStrengthSmoothed:F2}");
            GUILayout.Label($"Stance: {_grindStance}    Time: {_grindTime:F2}s    SurfaceKind: {_grindSurfaceKind}");
            GUILayout.Label($"ClosestPoint: {_grindClosestPoint}    Tangent: {_grindTangent}");
            GUILayout.Label($"Distance: {_grindDistance:F2} m    RawEndSign: {_grindRawEndSign}    DominantEndSign: {_grindDominantEndSign}");
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
                Collider leftContact = leftSkiContact.PrimaryContactCollider;
                GUILayout.Label(
                    $"LEFT: grounded={leftSkiContact.IsGrounded}  end={leftSkiContact.EndContactSign}  endZ={leftSkiContact.EndContactLocalZ:F2}  " +
                    $"tipOrTail={leftSkiContact.HasTipContact}  baseAlign={leftSkiContact.BaseContactAlignment:F2}");
                GUILayout.Label($"LEFT N: {leftSkiContact.ContactNormal}  P: {leftSkiContact.ContactPoint}  C: {DescribeColliderForDebug(leftContact)}");
            }

            if (rightSkiContact != null)
            {
                Collider rightContact = rightSkiContact.PrimaryContactCollider;
                GUILayout.Label(
                    $"RIGHT: grounded={rightSkiContact.IsGrounded}  end={rightSkiContact.EndContactSign}  endZ={rightSkiContact.EndContactLocalZ:F2}  " +
                    $"tipOrTail={rightSkiContact.HasTipContact}  baseAlign={rightSkiContact.BaseContactAlignment:F2}");
                GUILayout.Label($"RIGHT N: {rightSkiContact.ContactNormal}  P: {rightSkiContact.ContactPoint}  C: {DescribeColliderForDebug(rightContact)}");
            }

            GUILayout.Label($"Grind: active={_grindActive} collider={DescribeColliderForDebug(_grindActiveCollider)}");
            GUILayout.Label($"GroundLayers: {DescribeLayerMaskForDebug(groundLayers)}");
            GUILayout.Label($"GrindableLayers: {DescribeLayerMaskForDebug(grindableLayers)}");
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.matrix = old;
    }

    private void DrawStackRecoveryGizmo()
    {
        if (!drawStackRecoveryGizmo)
            return;

        Vector3 origin = _rb != null
            ? _rb.worldCenterOfMass
            : transform.position;

        Vector3 localDown = -transform.up;
        if (localDown.sqrMagnitude < 0.0001f)
            localDown = Vector3.down;

        localDown.Normalize();

        float rayLength = Mathf.Max(0.05f, stackRecoveryGizmoLength);
        float probeDistance = Mathf.Max(rayLength, stackRecoveryGizmoGroundProbeDistance);

        Vector3 lineEnd = origin + localDown * rayLength;

        Vector3 evaluationNormal = _groundNormal.sqrMagnitude > 0.0001f
            ? _groundNormal.normalized
            : Vector3.up;

        bool hasProbeHit = Physics.Raycast(
            origin,
            localDown,
            out RaycastHit hit,
            probeDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        if (hasProbeHit)
        {
            evaluationNormal = hit.normal.sqrMagnitude > 0.0001f
                ? hit.normal.normalized
                : evaluationNormal;

            lineEnd = hit.point;
        }

        bool wouldRecover;

        if (_stacked && _isGrounded)
        {
            // Exact current recovery state. Ignores only the minimum delay so the gizmo communicates
            // whether the landing/orientation/ski contact are good, not whether the timer has elapsed.
            wouldRecover = EvaluateStackRecoveryNow(
                requireMinimumDelay: false,
                out _,
                out _,
                out _,
                out _);
        }
        else
        {
            // Projected orientation-only state for airborne/pre-stack visualization.
            wouldRecover = EvaluateStackRecoveryOrientationAgainstNormal(evaluationNormal, out _);
        }

        Gizmos.color = wouldRecover
            ? stackRecoveryGizmoRecoverColor
            : stackRecoveryGizmoContinueColor;

        Gizmos.DrawLine(origin, lineEnd);
        Gizmos.DrawWireSphere(lineEnd, 0.08f);

        // Draw the evaluated surface normal at the end of the ray.
        Gizmos.DrawRay(lineEnd, evaluationNormal * 0.35f);

        // Draw a small cross marker at the origin so the local-down ray is easy to read.
        Vector3 right = transform.right.sqrMagnitude > 0.0001f ? transform.right.normalized : Vector3.right;
        Vector3 forward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        float crossSize = 0.08f;

        Gizmos.DrawLine(origin - right * crossSize, origin + right * crossSize);
        Gizmos.DrawLine(origin - forward * crossSize, origin + forward * crossSize);
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

        DrawStackRecoveryGizmo();

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


