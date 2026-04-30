using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// First-pass procedural limb visualiser for skiers.
/// - Draws 3-point line-rendered arms and legs.
/// - Uses skiing equipment as limb endpoints while skiing.
/// - Switches to a simple procedural walk cycle while in walking mode.
/// - Can optionally apply a small spring-smoothed suspension offset to a visual root.
/// This is intentionally visual-only and should not drive locomotion physics.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1200)]
public sealed class SkierLimbLineVisual : MonoBehaviour
{
    public enum RiderPoseMode
    {
        None = 0,
        LiftSeated = 1,
        SnowmobileDriver = 2,
        SnowmobilePassenger = 3
    }

    private enum LimbKind
    {
        Arm,
        Leg
    }

    [Header("Core References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private Rigidbody targetRigidbody;

    [Tooltip("Optional visual root to offset slightly for ski suspension. Leave null to disable body suspension.")]
    [SerializeField] private Transform suspensionVisualRoot;

    [Header("Optional Explicit Anchors")]
    [SerializeField] private Transform leftShoulderAnchor;
    [SerializeField] private Transform rightShoulderAnchor;
    [SerializeField] private Transform leftHipAnchor;
    [SerializeField] private Transform rightHipAnchor;

    [Tooltip("Optional endpoint near the left hand / pole grip. Falls back to PoleContact.PoleRoot.")]
    [SerializeField] private Transform leftPoleGripAnchor;
    [Tooltip("Optional endpoint near the right hand / pole grip. Falls back to PoleContact.PoleRoot.")]
    [SerializeField] private Transform rightPoleGripAnchor;
    [Tooltip("Optional endpoint near the left ski binding / boot position. Falls back to the left ski transform.")]
    [SerializeField] private Transform leftSkiBindingAnchor;
    [Tooltip("Optional endpoint near the right ski binding / boot position. Falls back to the right ski transform.")]
    [SerializeField] private Transform rightSkiBindingAnchor;

    [Header("Fallback Body Anchor Offsets")]
    [SerializeField] private Vector3 leftShoulderLocalOffset = new Vector3(-0.22f, 0.18f, 0.02f);
    [SerializeField] private Vector3 rightShoulderLocalOffset = new Vector3(0.22f, 0.18f, 0.02f);
    [SerializeField] private Vector3 leftHipLocalOffset = new Vector3(-0.16f, -0.26f, 0.02f);
    [SerializeField] private Vector3 rightHipLocalOffset = new Vector3(0.16f, -0.26f, 0.02f);

    [Header("Walk Cycle Offsets")]
    [SerializeField] private float walkFootSpacing = 0.18f;
    [SerializeField] private float walkFootForwardStride = 0.20f;
    [SerializeField] private float walkFootLift = 0.07f;
    [SerializeField] private float walkHandSpacing = 0.24f;
    [SerializeField] private float walkHandForwardStride = 0.16f;
    [SerializeField] private float walkHandLift = 0.05f;
    [SerializeField] private float walkCycleSpeed = 5.5f;
    [SerializeField] private float walkIdleReturnSpeed = 10f;
    [SerializeField] private float walkMoveThreshold = 0.08f;
    [SerializeField] private float walkPlanarSpeedForFullCycle = 2.5f;

    [Header("Lift Seated Pose")]
    [SerializeField] private float liftSeatedBodyDrop = 0.26f;
    [SerializeField] private float liftSeatedFootForward = 0.34f;
    [SerializeField] private float liftSeatedFootUp = 0.08f;
    [SerializeField] private float liftSeatedHandForward = 0.14f;
    [SerializeField] private float liftSeatedHandDown = 0.06f;
    [SerializeField] private float liftSeatedLegBend = 0.34f;
    [SerializeField] private float liftSeatedArmBend = 0.16f;

    [Header("Snowmobile Driver Pose")]
    [SerializeField] private float snowmobileDriverBodyDrop = 0.22f;
    [SerializeField] private float snowmobileDriverFootForward = 0.42f;
    [SerializeField] private float snowmobileDriverFootUp = 0.05f;
    [SerializeField] private float snowmobileDriverHandForwardFallback = 0.34f;
    [SerializeField] private float snowmobileDriverHandDownFallback = 0.04f;
    [SerializeField] private float snowmobileDriverLegBend = 0.38f;
    [SerializeField] private float snowmobileDriverArmBend = 0.16f;

    [Header("Snowmobile Passenger Pose")]
    [SerializeField] private float snowmobilePassengerBodyDrop = 0.24f;
    [SerializeField] private float snowmobilePassengerFootForward = 0.30f;
    [SerializeField] private float snowmobilePassengerFootUp = 0.06f;
    [SerializeField] private float snowmobilePassengerHandForwardFallback = 0.26f;
    [SerializeField] private float snowmobilePassengerHandDownFallback = 0.02f;
    [SerializeField] private float snowmobilePassengerLegBend = 0.36f;
    [SerializeField] private float snowmobilePassengerArmBend = 0.20f;

    [Header("External Rider Hand Targets")]
    [SerializeField] private float riderHandTargetFollowSpeed = 18f;

    [Header("Run Animation Multipliers")]
    [SerializeField, Range(1f, 2.5f)] private float runStrideMultiplier = 1.45f;
    [SerializeField, Range(1f, 2.5f)] private float runCycleSpeedMultiplier = 1.25f;
    [SerializeField, Range(1f, 2.5f)] private float runArmBendMultiplier = 1.25f;
    [SerializeField, Range(1f, 2.5f)] private float runLegBendMultiplier = 1.2f;

    [Header("Jump Pose")]
    [SerializeField] private float walkJumpBodyDrop = 0.15f;
    [SerializeField] private float skiJumpBodyDrop = 0.11f;
    [SerializeField] private float jumpReleaseBodyLift = 0.08f;
    [SerializeField] private float jumpSquatFootForward = 0.05f;
    [SerializeField] private float jumpSquatHandBack = 0.04f;
    [SerializeField] private float jumpSquatHandDown = 0.04f;
    [SerializeField] private float jumpSquatLegBendAdd = 0.18f;
    [SerializeField] private float jumpSquatArmBendAdd = 0.08f;
    [SerializeField] private float jumpReleaseLegStraighten = 0.12f;
    [SerializeField] private float jumpReleaseArmOpen = 0.08f;

    [Header("Walk Airborne Pose")]
    [Tooltip("How quickly the light airborne pose blends in once walk mode leaves the ground.")]
    [SerializeField] private float walkAirPoseBlendInSpeed = 8.5f;

    [Tooltip("How quickly the light airborne pose blends back out after landing.")]
    [SerializeField] private float walkAirPoseBlendOutSpeed = 13f;

    [Tooltip("How far the walk feet move forward during takeoff / light airborne pose.")]
    [SerializeField] private float walkAirFootForward = 0.14f;

    [Tooltip("How far the walk feet move upward during takeoff / light airborne pose.")]
    [SerializeField] private float walkAirFootUp = 0.16f;

    [Tooltip("How far the walk hands move upward during takeoff / light airborne pose.")]
    [SerializeField] private float walkAirHandUp = 0.11f;

    [Tooltip("How far the walk hands move outward during takeoff / light airborne pose.")]
    [SerializeField] private float walkAirHandOutward = 0.045f;

    [Tooltip("How much hand/foot targets trail behind the player's planar air velocity.")]
    [SerializeField] private float walkAirVelocityLag = 0.065f;

    [Tooltip("Velocity magnitude where airborne limb drag reaches full effect.")]
    [SerializeField] private float walkAirVelocityForFullLag = 7f;

    [Tooltip("Extra forward bias applied to knee target hints while airborne.")]
    [SerializeField] private float walkAirKneeForward = 0.12f;

    [Tooltip("Extra upward bias applied to knee target hints while airborne.")]
    [SerializeField] private float walkAirKneeUp = 0.14f;

    [Tooltip("Extra forward bias applied to elbow target hints while airborne.")]
    [SerializeField] private float walkAirElbowForward = 0.045f;

    [Tooltip("Extra upward bias applied to elbow target hints while airborne.")]
    [SerializeField] private float walkAirElbowUp = 0.12f;

    [Tooltip("Additional arm bend used by the lightweight airborne pose.")]
    [SerializeField] private float walkAirArmBendAdd = 0.10f;

    [Tooltip("Additional leg bend used by the lightweight airborne pose.")]
    [SerializeField] private float walkAirLegBendAdd = 0.13f;

    [Tooltip("How much the jump release/stretch pose temporarily suppresses the airborne ragdoll pose.")]
    [SerializeField, Range(0f, 1f)] private float walkAirJumpReleaseSuppression = 0.45f;

    [Header("Walk Landing Pose")]
    [SerializeField] private float walkLandingBodyDrop = 0.10f;
    [SerializeField] private float walkLandingFootForward = 0.025f;
    [SerializeField] private float walkLandingHandBack = 0.02f;
    [SerializeField] private float walkLandingHandDown = 0.025f;
    [SerializeField] private float walkLandingLegBendAdd = 0.14f;
    [SerializeField] private float walkLandingArmBendAdd = 0.05f;

    [Header("Ski Landing Pose")]
    [SerializeField] private float skiLandingBodyDrop = 0.075f;
    [SerializeField] private float skiLandingLegBendAdd = 0.10f;
    [SerializeField] private float skiLandingArmBendAdd = 0.035f;

    [Header("Walk Airborne Light Ragdoll Pose")]
    [SerializeField] private float walkAirborneBlendInSpeed = 5.5f;
    [SerializeField] private float walkAirborneBlendOutSpeed = 9.0f;

    [Tooltip("Velocity magnitude where airborne limb drag reaches full strength.")]
    [SerializeField] private float walkAirborneVelocityForFullDrag = 8.0f;

    [Tooltip("Maximum hand anchor drag behind player velocity while walking airborne.")]
    [SerializeField] private float walkAirborneHandDrag = 0.18f;

    [Tooltip("Maximum foot anchor drag behind player velocity while walking airborne.")]
    [SerializeField] private float walkAirborneFootDrag = 0.12f;

    [Tooltip("Small outward spread for limbs while walking airborne.")]
    [SerializeField] private float walkAirborneSideSpread = 0.035f;

    [Tooltip("Small arm bend added while walking airborne.")]
    [SerializeField] private float walkAirborneArmBendAdd = 0.055f;

    [Tooltip("Small leg bend added while walking airborne.")]
    [SerializeField] private float walkAirborneLegBendAdd = 0.075f;

    [Tooltip("How much the jump release pose suppresses the airborne ragdoll blend at the start of the jump.")]
    [SerializeField, Range(0f, 1f)] private float walkAirborneJumpReleaseSuppression = 0.55f;

    [Header("Seated Rider Pose")]
    [SerializeField] private float seatedBodyDrop = 0.26f;
    [SerializeField] private float seatedFootForward = 0.34f;
    [SerializeField] private float seatedFootUp = 0.08f;
    [SerializeField] private float seatedHandForward = 0.20f;
    [SerializeField] private float seatedHandDown = 0.08f;
    [SerializeField] private float seatedLegBend = 0.34f;
    [SerializeField] private float seatedArmBend = 0.18f;

    [Header("Skiing Bend")]
    [SerializeField] private float armBaseBend = 0.10f;
    [SerializeField] private float armTuckBend = 0.10f;
    [SerializeField] private float armAirBend = 0.06f;
    [SerializeField] private float armPolePhaseInfluence = 0.08f;
    [SerializeField] private float legBaseBend = 0.12f;
    [SerializeField] private float legTuckBend = 0.16f;
    [SerializeField] private float legInputBend = 0.08f;
    [SerializeField] private float legAirStraighten = 0.12f;
    [SerializeField] private float limbOutwardBias = 0.05f;
    [SerializeField] private float movementForwardBias = 0.04f;

    [Header("Walk Bend")]
    [SerializeField] private float walkArmBend = 0.10f;
    [SerializeField] private float walkLegBend = 0.14f;

    [Header("Suspension")]
    [SerializeField] private bool enableSuspension = true;
    [SerializeField] private float suspensionMaxOffset = 0.14f;
    [SerializeField] private float suspensionSpeed = 10f;
    [SerializeField] private float suspensionCompressionFromTuck = 0.05f;
    [SerializeField] private float suspensionCompressionFromTerrain = 0.08f;

    [Header("Feet")]
    [SerializeField] private Transform leftFootVisual;
    [SerializeField] private Transform rightFootVisual;

    [Tooltip("Author-time ski foot anchors. Usually child anchors under the left/right ski roots.")]
    [SerializeField] private Transform leftSkiFootAnchor;
    [SerializeField] private Transform rightSkiFootAnchor;

    [Tooltip("Author-time walk foot anchors. Usually siblings of the skis under the player root.")]
    [SerializeField] private Transform leftWalkFootTarget;
    [SerializeField] private Transform rightWalkFootTarget;

    [Header("Hands")]
    [SerializeField] private Transform leftHandVisual;
    [SerializeField] private Transform rightHandVisual;

    [Tooltip("Author-time ski hand anchors. Usually near the pole grip / hand position while skiing.")]
    [SerializeField] private Transform leftSkiHandAnchor;
    [SerializeField] private Transform rightSkiHandAnchor;

    [Tooltip("Author-time walk hand anchors. Usually siblings of the skis / walk feet under the player root.")]
    [SerializeField] private Transform leftWalkHandTarget;
    [SerializeField] private Transform rightWalkHandTarget;

    [Header("Walk Body Height")]
    [SerializeField] private bool keepBodyHeightRelativeToFeetInWalkMode = true;
    [SerializeField] private float walkBodyHeightFollowSpeed = 14f;

    [Tooltip("Keeps walking legs visually the same hip-to-foot reach as the last valid ski stance.")]
    [SerializeField] private bool preserveSkiLegReachInWalkMode = true;

    [Tooltip("How strongly walk mode preserves the cached ski-mode leg reach. 1 = exact visual reach preservation.")]
    [SerializeField, Range(0f, 1f)] private float walkLegReachPreserveWeight = 1f;

    [Tooltip("Reject cached ski leg reach below this value.")]
    [SerializeField] private float minValidWalkLegReach = 0.45f;

    [Tooltip("Reject cached ski leg reach above this value.")]
    [SerializeField] private float maxValidWalkLegReach = 1.25f;

    [Tooltip("Fallback visual body height above the average walk foot target if the cached ski-mode height becomes invalid.")]
    [SerializeField] private float fallbackWalkBodyHeightFromFeet = 0.92f;

    [Tooltip("Reject cached ski-to-body heights below this. Prevents stacked/ragdoll poses from making walk limbs stubby.")]
    [SerializeField] private float minValidWalkBodyHeightFromFeet = 0.65f;

    [Tooltip("Reject cached ski-to-body heights above this. Prevents bad references from stretching walk limbs.")]
    [SerializeField] private float maxValidWalkBodyHeightFromFeet = 1.35f;

    [Header("Stack Limb Physics")]
    [SerializeField] private bool enableStackLimbPhysics = true;

    [Tooltip("How quickly limb physics blends in after stacking.")]
    [SerializeField] private float stackBlendInSpeed = 20f;

    [Tooltip("How quickly limb physics releases back to authored anchors after recovery.")]
    [SerializeField] private float stackBlendOutSpeed = 10f;

    [Tooltip("Gravity applied to loose limb endpoints while stacked.")]
    [SerializeField] private float stackEndpointGravity = 9.81f;

    [Tooltip("Velocity damping applied to simulated limb endpoints. Higher values settle faster.")]
    [SerializeField] private float stackEndpointDamping = 7.5f;

    [Tooltip("How strongly player body acceleration is inherited by loose limb endpoints.")]
    [SerializeField, Range(0f, 1f)] private float stackBodyAccelerationInfluence = 0.85f;

    [Tooltip("How strongly player angular velocity throws limb endpoints around the body.")]
    [SerializeField, Range(0f, 1f)] private float stackAngularVelocityInfluence = 0.75f;

    [Tooltip("How strongly stack impact velocity is inherited by limb endpoints when stacking begins.")]
    [SerializeField, Range(0f, 1f)] private float stackImpactVelocityInheritance = 0.85f;

    [Tooltip("Small minimum endpoint speed below which simulated limbs are put to sleep.")]
    [SerializeField] private float stackEndpointSleepSpeed = 0.045f;

    [Tooltip("Small minimum body speed below which limb endpoints are allowed to fully settle.")]
    [SerializeField] private float stackBodySleepSpeed = 0.08f;

    [Tooltip("How many constraint passes are applied to keep endpoints within limb reach.")]
    [SerializeField, Range(1, 8)] private int stackConstraintIterations = 3;

    [Header("Stack Endpoint Grounding")]
    [SerializeField] private bool constrainStackEndpointsToGround = true;

    [Tooltip("How far above the endpoint to start the ground probe.")]
    [SerializeField] private float stackEndpointGroundProbeUp = 0.35f;

    [Tooltip("How far below the endpoint to search for terrain.")]
    [SerializeField] private float stackEndpointGroundProbeDown = 0.8f;

    [SerializeField] private float stackEndpointGroundRadius = 0.035f;
    [SerializeField] private float stackEndpointGroundSkin = 0.015f;

    [Tooltip("When true, visual hand/foot objects follow the simulated stack endpoints instead of staying attached to ski/pole anchors.")]
    [SerializeField] private bool moveHandFootVisualsDuringStack = true;

    [Tooltip("Applies a mild physical outward acceleration to stacked limb endpoints so wipeouts read wider and less uniform.")]
    [SerializeField] private bool enableStackOutwardSpread = true;

    [Tooltip("Outward acceleration applied to loose arm endpoints while stacked.")]
    [SerializeField] private float stackArmOutwardAcceleration = 5.5f;

    [Tooltip("Outward acceleration applied to loose leg endpoints while stacked. Keep lower than arms so recovery remains readable.")]
    [SerializeField] private float stackLegOutwardAcceleration = 2.75f;

    [Tooltip("Adds a small upward component to outward limb spread so hands/feet do not simply scrape together on the ground.")]
    [SerializeField] private float stackOutwardLiftAcceleration = 0.25f;

    [Tooltip("Reduces outward spread once a limb is already far from its shoulder/hip anchor.")]
    [SerializeField, Range(0f, 1f)] private float stackOutwardReachFade = 0.65f;

    [Tooltip("Extra outward velocity inherited when stacking begins. This is physical initialization, not looping animation.")]
    [SerializeField] private float stackInitialOutwardVelocity = 1.15f;

    [Tooltip("How much of the shared impact/body velocity every endpoint inherits. Lower values make limbs less uniform.")]
    [SerializeField, Range(0f, 1f)] private float stackSharedImpactVelocity = 0.48f;

    [Tooltip("Extra velocity projected around each shoulder/hip anchor. This creates limb-specific flail instead of identical endpoint motion.")]
    [SerializeField] private float stackImpactTangentVelocity = 0.55f;

    [Tooltip("Impulse-like whip velocity generated from the impact direction and each limb's anchor-to-end vector.")]
    [SerializeField] private float stackImpactWhipVelocity = 1.35f;

    [Tooltip("Arms should usually react more loosely and dramatically than legs.")]
    [SerializeField] private float stackArmImpactVelocityMultiplier = 1.2f;

    [Tooltip("Legs should react, but remain more recoverable than arms/skis.")]
    [SerializeField] private float stackLegImpactVelocityMultiplier = 0.85f;

    [Tooltip("Reduces inherited impact for endpoints on the opposite side of the impact direction.")]
    [SerializeField, Range(0f, 1f)] private float stackOppositeSideImpactReduction = 0.35f;

    [Tooltip("Soft pull toward each endpoint's original shoulder/hip-relative offset. Prevents limbs collapsing into one clump.")]
    [SerializeField] private float stackAnchorSpringAcceleration = 3.5f;

    [Tooltip("Damping applied to the soft anchor spring only.")]
    [SerializeField] private float stackAnchorSpringDamping = 0.85f;

    [Header("Stack Limb Lengths")]
    [Tooltip("When enabled, stacked limb lines use captured TrickPose rig-assist segment lengths so arms/legs keep believable proportions while flailing.")]
    [SerializeField] private bool useRigAssistLengthsForStack = true;
    [Tooltip("Fallback upper arm length used for stack limb solving when no captured rig-assist lengths are available.")]
    [SerializeField] private float fallbackUpperArmLength = 0.32f;
    [Tooltip("Fallback lower arm length used for stack limb solving when no captured rig-assist lengths are available.")]
    [SerializeField] private float fallbackLowerArmLength = 0.34f;
    [Tooltip("Fallback upper leg length used for stack limb solving when no captured rig-assist lengths are available.")]
    [SerializeField] private float fallbackUpperLegLength = 0.42f;
    [Tooltip("Fallback lower leg length used for stack limb solving when no captured rig-assist lengths are available.")]
    [SerializeField] private float fallbackLowerLegLength = 0.42f;

    [Header("Stack Equipment Visuals")]
    [SerializeField] private bool moveEquipmentVisualsDuringStack = true;

    [Tooltip("How strongly ski binding anchors follow the simulated feet while stacked. Keep below poles so recovery still reads clearly.")]
    [SerializeField, Range(0f, 1f)] private float stackSkiAnchorFollow = 0.75f;

    [Tooltip("How strongly ski visuals rotate toward ragdoll orientation.")]
    [SerializeField, Range(0f, 1f)] private float stackSkiRotationFollow = 0.65f;

    [Tooltip("How strongly pole grip anchors follow the simulated hands while stacked.")]
    [SerializeField, Range(0f, 1f)] private float stackPoleAnchorFollow = 1f;

    [Tooltip("How strongly pole visuals rotate toward ragdoll orientation.")]
    [SerializeField, Range(0f, 1f)] private float stackPoleRotationFollow = 0.95f;

   
    [Tooltip("Temporary fallback for skis only. Leave false once visual-only ski roots are assigned.")]
    [SerializeField] private bool allowLogicalEquipmentRootFallback = false;

    [Tooltip("Local rotation correction applied after solving ski ragdoll orientation.")]
    [SerializeField] private Vector3 stackSkiRotationOffsetEuler;

    [Tooltip("Local rotation correction applied after solving pole ragdoll orientation.")]
    [SerializeField] private Vector3 stackPoleRotationOffsetEuler;

    [Header("Line Renderer Setup")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float armWidth = 0.035f;
    [SerializeField] private float legWidth = 0.045f;
    [SerializeField] private bool receiveShadows = false;
    [SerializeField] private bool shadowCasting = false;
    [SerializeField] private int sortingOrder = 0;

    [Header("Customization")]
    [SerializeField] private CharacterCustomizer characterCustomizer;
    [SerializeField] private bool routeLimbRenderersThroughCustomizer = true;
    [SerializeField] private string defaultJacketSecondaryChannelId = "secondary";

    [Header("Debug")]
    [SerializeField] private bool autoCreateLineRenderers = true;
    [SerializeField] private bool drawGizmos;
    [SerializeField] private bool previewInEditor = true;

    [SerializeField, HideInInspector] private LineRenderer leftArmLine;
    [SerializeField, HideInInspector] private LineRenderer rightArmLine;
    [SerializeField, HideInInspector] private LineRenderer leftLegLine;
    [SerializeField, HideInInspector] private LineRenderer rightLegLine;

    private enum LimbBindingMode
    {
        None,
        Skin,
        JacketPrimary,
        JacketSecondary
    }

    private LimbBindingMode _leftArmBindingMode = LimbBindingMode.None;
    private LimbBindingMode _rightArmBindingMode = LimbBindingMode.None;
    private LimbBindingMode _leftLegBindingMode = LimbBindingMode.None;
    private LimbBindingMode _rightLegBindingMode = LimbBindingMode.None;

    private WearableAttachment _lastRoutedJacketAttachment;
    private CustomizationOptionSO _lastRoutedJacketOption;
    private int _lastRoutingRendererSignature;
    private bool _lastWalkingMode;

    private Transform _bodyTransform;
    private Transform _headTransform;
    private Transform _leftSkiTransform;
    private Transform _rightSkiTransform;
    private PoleContact _leftPoleContact;
    private PoleContact _rightPoleContact;

    private Vector3 _suspensionBaseLocalPos;
    private Vector3 _suspensionSmoothedLocalPos;
    private float _walkCycleTime;
    private float _walkMotionBlend;
    private float _walkAirPoseBlend;

    private float _walkAirborneBlend;

    private RiderPoseMode _riderPoseMode;
    private Transform _riderLeftHandTarget;
    private Transform _riderRightHandTarget;

    private Vector3 _lastRootPosition;
    private bool _haveLastRootPosition;

    private struct StackLimbEndpointState
    {
        public bool initialized;
        public bool sleeping;
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 velocity;

        // Rest offset captured relative to the current body rotation at impact time.
        // This gives each endpoint its own shoulder/hip-relative context instead of all endpoints sharing one force path.
        public Vector3 bodyLocalRootToEndpoint;

        public float gravityMultiplier;
        public float dampingMultiplier;

        public void Reset(
            Vector3 worldPosition,
            Vector3 rootWorldPosition,
            Quaternion bodyWorldRotation,
            Vector3 inheritedVelocity,
            float gravityMul,
            float dampingMul)
        {
            initialized = true;
            sleeping = false;

            position = worldPosition;
            previousPosition = worldPosition - inheritedVelocity * Mathf.Max(Time.deltaTime, 0.016f);
            velocity = inheritedVelocity;

            bodyLocalRootToEndpoint = Quaternion.Inverse(bodyWorldRotation) * (worldPosition - rootWorldPosition);

            gravityMultiplier = Mathf.Max(0.01f, gravityMul);
            dampingMultiplier = Mathf.Max(0.01f, dampingMul);
        }

        public void Clear()
        {
            initialized = false;
            sleeping = false;
            position = Vector3.zero;
            previousPosition = Vector3.zero;
            velocity = Vector3.zero;
            bodyLocalRootToEndpoint = Vector3.zero;
            gravityMultiplier = 1f;
            dampingMultiplier = 1f;
        }
    }

    private float _stackBlend;
    private bool _stackPhysicsActive;
    private bool _stackEquipmentVisualsWereApplied;
    private Vector3 _lastBodyVelocity;
    private bool _hasLastBodyVelocity;

    private StackLimbEndpointState _leftArmStackEndpoint;
    private StackLimbEndpointState _rightArmStackEndpoint;
    private StackLimbEndpointState _leftLegStackEndpoint;
    private StackLimbEndpointState _rightLegStackEndpoint;

    private struct StackEquipmentRootPose
    {
        public Transform root;
        public bool captured;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public void Capture(Transform target)
        {
            root = target;
            captured = target != null;
            localPosition = target != null ? target.localPosition : Vector3.zero;
            localRotation = target != null ? target.localRotation : Quaternion.identity;
            localScale = target != null ? target.localScale : Vector3.one;
        }

        public void Restore()
        {
            if (!captured || root == null)
                return;

            root.localPosition = localPosition;
            root.localRotation = localRotation;
            root.localScale = localScale;
        }
    }

    private StackEquipmentRootPose _leftStackSkiBasePose;
    private StackEquipmentRootPose _rightStackSkiBasePose;
    private StackEquipmentRootPose _leftStackPoleBasePose;
    private StackEquipmentRootPose _rightStackPoleBasePose;

    private float _cachedWalkBodyHeightFromSkiMode = 0.92f;
    private bool _footVisualParentsInitialized;
    private Vector3 _leftFootVisualLocalScale = Vector3.one;
    private Vector3 _rightFootVisualLocalScale = Vector3.one;

    private bool _handVisualParentsInitialized;
    private Vector3 _leftHandVisualLocalScale = Vector3.one;
    private Vector3 _rightHandVisualLocalScale = Vector3.one;
    private bool _walkAnchorBaseLocalsInitialized;
    private Vector3 _leftWalkFootBaseLocalPosition;
    private Vector3 _rightWalkFootBaseLocalPosition;
    private Vector3 _leftWalkHandBaseLocalPosition;
    private Vector3 _rightWalkHandBaseLocalPosition;
    private Transform _cachedLeftWalkFootAnchor;
    private Transform _cachedRightWalkFootAnchor;
    private Transform _cachedLeftWalkHandAnchor;
    private Transform _cachedRightWalkHandAnchor;

    private bool _hasCachedSkiLegReachForWalk;
    private float _cachedLeftSkiLegReachForWalk;
    private float _cachedRightSkiLegReachForWalk;

    private bool _seatedPoseOverride;

    private TrickPoseRigSnapshot _previewJointSnapshot;
    private float _previewJointWeight;
    private TrickPoseEntry _runtimeJointPoseEntry;
    private float _runtimeJointPoseWeight;
    private bool _editorPreviewDirty = true;

    private static readonly Vector3[] _lineBuffer = new Vector3[3];
    private const float LinePositionChangeEpsilonSqr = 0.000001f;

    private void Reset()
    {
        AutoBind();
        EnsureLineRenderers();
        CacheSuspensionBase();
    }

    private void Awake()
    {
        AutoBind();
        EnsureLineRenderers();
        CacheSuspensionBase();
        ResetWalkAnchorBaseCache();
        CacheWalkAnchorBaseLocals();
        CacheStackEquipmentBasePoses();

        if (leftFootVisual != null)
            _leftFootVisualLocalScale = leftFootVisual.localScale;

        if (rightFootVisual != null)
            _rightFootVisualLocalScale = rightFootVisual.localScale;

        if (leftHandVisual != null)
            _leftHandVisualLocalScale = leftHandVisual.localScale;

        if (rightHandVisual != null)
            _rightHandVisualLocalScale = rightHandVisual.localScale;
    }

    private void OnEnable()
    {
        AutoBind();
        CacheSuspensionBase();
        ResetWalkAnchorBaseCache();
        CacheWalkAnchorBaseLocals();
        CacheStackEquipmentBasePoses();
        RestoreStackEquipmentVisualRoots();

        _haveLastRootPosition = false;
        _footVisualParentsInitialized = false;
        _handVisualParentsInitialized = false;
        _editorPreviewDirty = true;

        if (skiController != null)
        {
            skiController.OnStacked += HandleStacked;
            skiController.OnStackImpact += HandleStackImpact;
            skiController.OnRecoveredFromStack += HandleRecoveredFromStack;
        }
    }

    private void OnValidate()
    {
        ApplyLineSettings(leftArmLine, armWidth, "LeftArmLine");
        ApplyLineSettings(rightArmLine, armWidth, "RightArmLine");
        ApplyLineSettings(leftLegLine, legWidth, "LeftLegLine");
        ApplyLineSettings(rightLegLine, legWidth, "RightLegLine");

        _lastRoutedJacketAttachment = null;
        _lastRoutedJacketOption = null;
        _lastRoutingRendererSignature = 0;

        if (!Application.isPlaying && suspensionVisualRoot != null)
            _suspensionBaseLocalPos = suspensionVisualRoot.localPosition;

        ResetWalkAnchorBaseCache();
        CacheWalkAnchorBaseLocals();
        CacheStackEquipmentBasePoses();

        _editorPreviewDirty = true;
    }

    private void OnDisable()
    {
        if (skiController != null)
        {
            skiController.OnStacked -= HandleStacked;
            skiController.OnStackImpact -= HandleStackImpact;
            skiController.OnRecoveredFromStack -= HandleRecoveredFromStack;
        }

        _stackBlend = 0f;
        _stackPhysicsActive = false;
        _hasLastBodyVelocity = false;

        _leftArmStackEndpoint.Clear();
        _rightArmStackEndpoint.Clear();
        _leftLegStackEndpoint.Clear();
        _rightLegStackEndpoint.Clear();

        ReleaseStackEquipmentVisuals(force: true);
    }

    private void OnDestroy()
    {
        OnDisable();
    }

    [ContextMenu("Auto Bind References")]
    public void AutoBind()
    {
        if (skiController == null)
            skiController = GetComponent<SkiController>() ?? GetComponentInParent<SkiController>();

        if (walkingController == null)
            walkingController = GetComponent<WalkingController>() ?? GetComponentInParent<WalkingController>();

        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>();

        if (characterCustomizer == null)
            characterCustomizer = GetComponentInChildren<CharacterCustomizer>(true) ?? GetComponentInParent<CharacterCustomizer>();

        if (skiController != null)
        {
            _bodyTransform = skiController.BodyPoseTransform != null ? skiController.BodyPoseTransform : transform;
            _headTransform = skiController.HeadPoseTransform;
            _leftSkiTransform = skiController.LeftSkiTransform;
            _rightSkiTransform = skiController.RightSkiTransform;
            _leftPoleContact = skiController.LeftPoleContact;
            _rightPoleContact = skiController.RightPoleContact;
        }
        else
        {
            _bodyTransform = transform;
            _headTransform = null;
            _leftSkiTransform = null;
            _rightSkiTransform = null;
            _leftPoleContact = null;
            _rightPoleContact = null;
        }

        if (suspensionVisualRoot == null && _bodyTransform != null)
            suspensionVisualRoot = _bodyTransform;
    }

    public bool HasValidWalkFootTargets()
    {
        return leftWalkFootTarget != null && rightWalkFootTarget != null;
    }

    public bool TryGetBodyLocalAnchor(bool left, bool arm, out Vector3 bodyLocalPosition)
    {
        bodyLocalPosition = Vector3.zero;
        Transform body = GetBodyTransform();
        if (body == null)
            return false;

        Transform explicitAnchor = arm
            ? (left ? leftShoulderAnchor : rightShoulderAnchor)
            : (left ? leftHipAnchor : rightHipAnchor);
        if (explicitAnchor != null)
        {
            bodyLocalPosition = body.InverseTransformPoint(explicitAnchor.position);
            return true;
        }

        bodyLocalPosition = arm
            ? (left ? leftShoulderLocalOffset : rightShoulderLocalOffset)
            : (left ? leftHipLocalOffset : rightHipLocalOffset);
        return true;
    }

    public Vector3 GetWalkFootTargetPosition(bool left)
    {
        Transform t = left ? leftWalkFootTarget : rightWalkFootTarget;
        return t != null ? t.position : Vector3.zero;
    }

    public float GetAverageWalkFootSupportY()
    {
        if (leftWalkFootTarget == null && rightWalkFootTarget == null)
            return transform.position.y;

        bool hasLeft = leftWalkFootTarget != null;
        bool hasRight = rightWalkFootTarget != null;
        float leftY = GetLimbEndpointWorldPosition(true, true, LimbKind.Leg).y;
        float rightY = GetLimbEndpointWorldPosition(false, true, LimbKind.Leg).y;

        if (hasLeft && hasRight)
            return (leftY + rightY) * 0.5f;

        return hasLeft ? leftY : rightY;
    }

    public float GetAverageSkiSupportY()
    {
        Vector3 left = GetLimbEndpointWorldPosition(true, false, LimbKind.Leg);
        Vector3 right = GetLimbEndpointWorldPosition(false, false, LimbKind.Leg);
        return (left.y + right.y) * 0.5f;
    }

    [ContextMenu("Create / Refresh Line Renderers")]
    public void EnsureLineRenderers()
    {
        if (!autoCreateLineRenderers)
            return;

        leftArmLine = EnsureLineRenderer(leftArmLine, "LeftArmLine", armWidth);
        rightArmLine = EnsureLineRenderer(rightArmLine, "RightArmLine", armWidth);
        leftLegLine = EnsureLineRenderer(leftLegLine, "LeftLegLine", legWidth);
        rightLegLine = EnsureLineRenderer(rightLegLine, "RightLegLine", legWidth);

        _lastRoutedJacketAttachment = null;
        _lastRoutedJacketOption = null;
        _lastRoutingRendererSignature = 0;
    }

    public void RefreshEditorPreview()
    {
        if (Application.isPlaying || !previewInEditor)
            return;

        _editorPreviewDirty = false;
        RefreshVisualRepresentation();
    }

    public void SetPreviewJointSnapshot(TrickPoseRigSnapshot snapshot, float weight)
    {
        _previewJointSnapshot = snapshot;
        _previewJointWeight = Mathf.Clamp01(weight);
    }

    public void ClearPreviewJointSnapshot()
    {
        _previewJointSnapshot = null;
        _previewJointWeight = 0f;
    }

    public void SetRiderPoseOverride(
    RiderPoseMode mode,
    Transform leftHandTarget = null,
    Transform rightHandTarget = null)
    {
        _riderPoseMode = mode;
        _riderLeftHandTarget = leftHandTarget;
        _riderRightHandTarget = rightHandTarget;

        if (mode != RiderPoseMode.None)
        {
            _walkMotionBlend = 0f;
            _walkCycleTime = 0f;
        }

        _handVisualParentsInitialized = false;
        _footVisualParentsInitialized = false;
    }

    public void SetSeatedPoseOverride(bool active)
    {
        // Backwards-compatible helper for older call sites.
        SetRiderPoseOverride(active ? RiderPoseMode.LiftSeated : RiderPoseMode.None);
    }

    public void ClearRiderPoseOverride()
    {
        SetRiderPoseOverride(RiderPoseMode.None);
    }

    public void PrepareForWalkModeHandoff(bool enteredFromStack)
    {
        if (enteredFromStack || _stackPhysicsActive || _stackBlend > 0.001f || HasInitializedStackEndpointState())
        {
            _stackBlend = 0f;
            _stackPhysicsActive = false;
            _stackEquipmentVisualsWereApplied = false;
            _hasLastBodyVelocity = false;
            _lastBodyVelocity = Vector3.zero;

            ClearStackEndpointStates();
            ReleaseStackEquipmentVisuals(force: true);
        }

        ResetWalkAnchorBaseCache();
        CacheWalkAnchorBaseLocals();
        RestoreWalkAnchorBaseLocalsImmediate();

        // This is the important handoff: after SkiController has reset its body pose,
        // capture the actual neutral body-to-walk-foot height instead of relying on an old
        // stacked/ski cached value or a generic fallback.
        CaptureWalkBodyHeightFromCurrentWalkTargets(forceFallbackIfInvalid: true);

        if (suspensionVisualRoot != null)
        {
            _suspensionSmoothedLocalPos = _suspensionBaseLocalPos;
            suspensionVisualRoot.localPosition = _suspensionSmoothedLocalPos;
        }

        SnapWalkBodyHeightToCached();

        _footVisualParentsInitialized = false;
        _handVisualParentsInitialized = false;

        bool walkingMode = walkingController != null && walkingController.IsWalkingMode;
        RefreshFootVisualParents(walkingMode);
        RefreshHandVisualParents(walkingMode);

        _footVisualParentsInitialized = true;
        _handVisualParentsInitialized = true;
        _lastWalkingMode = walkingMode;

        DrawArm(true, walkingMode, leftArmLine);
        DrawArm(false, walkingMode, rightArmLine);
        DrawLeg(true, walkingMode, leftLegLine);
        DrawLeg(false, walkingMode, rightLegLine);
    }

    private void RestoreWalkAnchorBaseLocalsImmediate()
    {
        if (!_walkAnchorBaseLocalsInitialized)
            CacheWalkAnchorBaseLocals();

        if (leftWalkFootTarget != null)
            leftWalkFootTarget.localPosition = _leftWalkFootBaseLocalPosition;

        if (rightWalkFootTarget != null)
            rightWalkFootTarget.localPosition = _rightWalkFootBaseLocalPosition;

        if (leftWalkHandTarget != null)
            leftWalkHandTarget.localPosition = _leftWalkHandBaseLocalPosition;

        if (rightWalkHandTarget != null)
            rightWalkHandTarget.localPosition = _rightWalkHandBaseLocalPosition;

        _walkMotionBlend = 0f;
        _walkCycleTime = 0f;
    }

    private void CaptureWalkBodyHeightFromCurrentWalkTargets(bool forceFallbackIfInvalid)
    {
        Transform body = GetBodyTransform();
        if (body == null || !HasValidWalkFootTargets())
        {
            if (forceFallbackIfInvalid)
                RepairCachedWalkBodyHeight();

            return;
        }

        float supportY = GetAverageWalkFootSupportY();
        float offset = body.position.y - supportY;

        float minHeight = Mathf.Min(minValidWalkBodyHeightFromFeet, maxValidWalkBodyHeightFromFeet);
        float maxHeight = Mathf.Max(minValidWalkBodyHeightFromFeet, maxValidWalkBodyHeightFromFeet);

        if (offset >= minHeight && offset <= maxHeight)
        {
            _cachedWalkBodyHeightFromSkiMode = offset;
            fallbackWalkBodyHeightFromFeet = offset;
            return;
        }

        if (forceFallbackIfInvalid)
        {
            float fallback = Mathf.Clamp(fallbackWalkBodyHeightFromFeet, minHeight, maxHeight);
            _cachedWalkBodyHeightFromSkiMode = fallback;
        }
    }

    private void SnapWalkBodyHeightToCached()
    {
        if (suspensionVisualRoot == null)
            return;

        if (!keepBodyHeightRelativeToFeetInWalkMode)
            return;

        if (!HasValidWalkFootTargets())
            return;

        Transform parent = suspensionVisualRoot.parent;
        if (parent == null)
            return;

        float supportY = GetAverageWalkFootSupportY();
        float desiredWorldBodyY = supportY + _cachedWalkBodyHeightFromSkiMode;

        Vector3 desiredWorldPos = suspensionVisualRoot.position;
        desiredWorldPos.y = desiredWorldBodyY;

        Vector3 desiredLocalPos = parent.InverseTransformPoint(desiredWorldPos);

        Vector3 targetLocalPos = _suspensionSmoothedLocalPos;
        targetLocalPos.y = desiredLocalPos.y;

        // Immediate version of the same reach preservation used by UpdateWalkBodyHeight().
        targetLocalPos.y = ApplyWalkLegReachTargetToLocalY(parent, targetLocalPos.y);

        _suspensionSmoothedLocalPos = targetLocalPos;
        suspensionVisualRoot.localPosition = _suspensionSmoothedLocalPos;
    }

    private bool TryGetWalkLegReachSuspensionWorldY(out float targetSuspensionWorldY)
    {
        targetSuspensionWorldY = suspensionVisualRoot != null
            ? suspensionVisualRoot.position.y
            : transform.position.y;

        if (!preserveSkiLegReachInWalkMode)
            return false;

        if (suspensionVisualRoot == null)
            return false;

        if (!HasValidWalkFootTargets())
            return false;

        if (!_hasCachedSkiLegReachForWalk)
            TrySeedWalkLegReachFromFallbackSegments();

        if (!_hasCachedSkiLegReachForWalk)
            return false;

        bool hasLeft = TryGetRequiredSuspensionDeltaForWalkLegReach(
            true,
            _cachedLeftSkiLegReachForWalk,
            out float leftDelta);

        bool hasRight = TryGetRequiredSuspensionDeltaForWalkLegReach(
            false,
            _cachedRightSkiLegReachForWalk,
            out float rightDelta);

        if (!hasLeft && !hasRight)
            return false;

        float delta;
        if (hasLeft && hasRight)
        {
            // Use the higher correction so neither leg appears visually shortened.
            delta = Mathf.Max(leftDelta, rightDelta);
        }
        else
        {
            delta = hasLeft ? leftDelta : rightDelta;
        }

        targetSuspensionWorldY = suspensionVisualRoot.position.y + delta;
        return true;
    }

    private bool TryGetRequiredSuspensionDeltaForWalkLegReach(bool left, float targetReach, out float suspensionDeltaY)
    {
        suspensionDeltaY = 0f;

        float minReach = Mathf.Min(minValidWalkLegReach, maxValidWalkLegReach);
        float maxReach = Mathf.Max(minValidWalkLegReach, maxValidWalkLegReach);
        targetReach = Mathf.Clamp(targetReach, minReach, maxReach);

        Vector3 hip = GetHipPosition(left);
        Vector3 foot = GetLimbEndpointWorldPosition(left, true, LimbKind.Leg);

        Vector3 horizontal = Vector3.ProjectOnPlane(hip - foot, Vector3.up);
        float horizontalDistance = horizontal.magnitude;

        if (horizontalDistance >= targetReach)
        {
            // The walk target is too far horizontally to preserve the original reach by vertical offset alone.
            // In this case, do not push the body down or create impossible geometry.
            return false;
        }

        float requiredVerticalSeparation = Mathf.Sqrt(
            Mathf.Max(0f, targetReach * targetReach - horizontalDistance * horizontalDistance));

        float desiredHipY = foot.y + requiredVerticalSeparation;
        suspensionDeltaY = desiredHipY - hip.y;
        return true;
    }

    private void TrySeedWalkLegReachFromFallbackSegments()
    {
        GetStackSegmentLengths(LimbKind.Leg, true, out float leftUpper, out float leftLower);
        GetStackSegmentLengths(LimbKind.Leg, false, out float rightUpper, out float rightLower);

        float minReach = Mathf.Min(minValidWalkLegReach, maxValidWalkLegReach);
        float maxReach = Mathf.Max(minValidWalkLegReach, maxValidWalkLegReach);

        _cachedLeftSkiLegReachForWalk = Mathf.Clamp(leftUpper + leftLower, minReach, maxReach);
        _cachedRightSkiLegReachForWalk = Mathf.Clamp(rightUpper + rightLower, minReach, maxReach);
        _hasCachedSkiLegReachForWalk = true;
    }

    private float ApplyWalkLegReachTargetToLocalY(Transform parent, float currentTargetLocalY)
    {
        if (parent == null)
            return currentTargetLocalY;

        if (!TryGetWalkLegReachSuspensionWorldY(out float targetWorldY))
            return currentTargetLocalY;

        Vector3 targetWorldPos = suspensionVisualRoot.position;
        targetWorldPos.y = targetWorldY;

        float reachLocalY = parent.InverseTransformPoint(targetWorldPos).y;

        return Mathf.Lerp(
            currentTargetLocalY,
            reachLocalY,
            Mathf.Clamp01(walkLegReachPreserveWeight));
    }

    public void ForceClearStackVisualState(bool refreshCurrentModeAnchors = true)
    {
        _stackBlend = 0f;
        _stackPhysicsActive = false;
        _stackEquipmentVisualsWereApplied = false;
        _hasLastBodyVelocity = false;
        _lastBodyVelocity = Vector3.zero;

        ClearStackEndpointStates();
        ReleaseStackEquipmentVisuals(force: true);

        if (!refreshCurrentModeAnchors)
            return;

        bool walkingMode = walkingController != null && walkingController.IsWalkingMode;

        if (walkingMode)
        {
            RestoreWalkAnchorBaseLocalsImmediate();
            CaptureWalkBodyHeightFromCurrentWalkTargets(forceFallbackIfInvalid: true);
            SnapWalkBodyHeightToCached();
        }

        _footVisualParentsInitialized = false;
        _handVisualParentsInitialized = false;

        RefreshFootVisualParents(walkingMode);
        RefreshHandVisualParents(walkingMode);

        _footVisualParentsInitialized = true;
        _handVisualParentsInitialized = true;
        _lastWalkingMode = walkingMode;

        DrawArm(true, walkingMode, leftArmLine);
        DrawArm(false, walkingMode, rightArmLine);
        DrawLeg(true, walkingMode, leftLegLine);
        DrawLeg(false, walkingMode, rightLegLine);
    }

    public void SetRuntimeJointPoseEntry(TrickPoseEntry entry, float weight)
    {
        _runtimeJointPoseEntry = entry;
        _runtimeJointPoseWeight = Mathf.Clamp01(weight);
    }

    public bool TryCaptureJointPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation)
    {
        return TryGetResolvedJointControlLocalPose(joint, out localPosition, out localRotation);
    }


    public bool TryGetJointHandlePose(SkierLimbJoint joint, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;

        Transform body = GetBodyTransform();
        if (body == null || !TryGetResolvedJointControlLocalPose(joint, out Vector3 localPosition, out _))
            return false;

        worldPosition = body.TransformPoint(localPosition);
        worldRotation = body.rotation;
        return true;
    }

    private LineRenderer EnsureLineRenderer(LineRenderer existing, string childName, float width)
    {
        bool createdNew = false;

        if (existing == null)
        {
            Transform child = transform.Find(childName);
            if (child != null)
                existing = child.GetComponent<LineRenderer>();
        }

        if (existing == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            existing = go.AddComponent<LineRenderer>();
            createdNew = true;
        }

        ApplyLineSettings(existing, width, childName, createdNew);
        return existing;
    }

    private void ApplyLineSettings(LineRenderer lr, float width, string childName, bool forceAssignMaterial = false)
    {
        if (lr == null)
            return;

        if (lr.name != childName)
            lr.name = childName;

        if (lr.positionCount != 3)
            lr.positionCount = 3;

        if (!lr.useWorldSpace)
            lr.useWorldSpace = true;

        if (lr.alignment != LineAlignment.View)
            lr.alignment = LineAlignment.View;

        if (lr.textureMode != LineTextureMode.Stretch)
            lr.textureMode = LineTextureMode.Stretch;

        if (lr.numCapVertices != 4)
            lr.numCapVertices = 4;

        if (lr.numCornerVertices != 2)
            lr.numCornerVertices = 2;

        if (!Mathf.Approximately(lr.widthMultiplier, width))
            lr.widthMultiplier = width;

        if (!Mathf.Approximately(lr.startWidth, width))
            lr.startWidth = width;

        if (!Mathf.Approximately(lr.endWidth, width))
            lr.endWidth = width;

        if (lr.sortingOrder != sortingOrder)
            lr.sortingOrder = sortingOrder;

        if (lr.receiveShadows != receiveShadows)
            lr.receiveShadows = receiveShadows;

        UnityEngine.Rendering.ShadowCastingMode desiredShadowMode = shadowCasting ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        if (lr.shadowCastingMode != desiredShadowMode)
            lr.shadowCastingMode = desiredShadowMode;

        if (lineMaterial != null && (forceAssignMaterial || lr.sharedMaterial == null))
            lr.sharedMaterial = lineMaterial;
    }

    private void CacheSuspensionBase()
    {
        if (suspensionVisualRoot == null)
            return;

        _suspensionBaseLocalPos = suspensionVisualRoot.localPosition;
        _suspensionSmoothedLocalPos = _suspensionBaseLocalPos;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || !previewInEditor)
            return;

        if (!_editorPreviewDirty)
            return;

        RefreshEditorPreview();
#endif
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        RefreshVisualRepresentation();
    }

    private void RefreshVisualRepresentation()
    {
        if (skiController == null)
        {
            AutoBind();
            if (skiController == null)
                return;
        }

        if (autoCreateLineRenderers)
            EnsureLineRenderers();

        RefreshLimbRendererRouting();

        bool walkingMode = walkingController != null && walkingController.IsWalkingMode;
        bool walkingModeChanged = _lastWalkingMode != walkingMode;

        if (walkingModeChanged && walkingMode)
        {
            PrepareVisualsForWalkModeTransition();
        }

        if (!_footVisualParentsInitialized || !_handVisualParentsInitialized || walkingModeChanged)
        {
            RefreshFootVisualParents(walkingMode);
            RefreshHandVisualParents(walkingMode);
            _footVisualParentsInitialized = true;
            _handVisualParentsInitialized = true;
            _lastWalkingMode = walkingMode;
        }

        if (!walkingMode && CanCacheBodyHeightFromCurrentSkiSupport())
        {
            CacheBodyHeightFromCurrentSkiSupport();
            CacheSkiLegReachForWalkMode();
        }

        UpdateWalkCycle(walkingMode);
        UpdateSuspension(walkingMode);
        UpdateWalkBodyHeight(walkingMode);
        UpdateStackBlend();

        DrawArm(true, walkingMode, leftArmLine);
        DrawArm(false, walkingMode, rightArmLine);
        DrawLeg(true, walkingMode, leftLegLine);
        DrawLeg(false, walkingMode, rightLegLine);
        UpdateStackEquipmentVisuals();
    }

    private void RefreshFootVisualParents(bool walkingMode)
    {
        AttachFootVisual(leftFootVisual, GetActiveLimbAnchor(true, walkingMode, LimbKind.Leg), true);
        AttachFootVisual(rightFootVisual, GetActiveLimbAnchor(false, walkingMode, LimbKind.Leg), false);
    }

    private void AttachFootVisual(Transform footVisual, Transform anchor, bool left)
    {
        if (footVisual == null || anchor == null)
            return;

        if (footVisual.parent != anchor)
            footVisual.SetParent(anchor, false);

        footVisual.localPosition = Vector3.zero;
        footVisual.localRotation = Quaternion.identity;
        footVisual.localScale = left ? _leftFootVisualLocalScale : _rightFootVisualLocalScale;
    }

    private void RefreshHandVisualParents(bool walkingMode)
    {
        AttachHandVisual(leftHandVisual, GetActiveLimbAnchor(true, walkingMode, LimbKind.Arm), true);
        AttachHandVisual(rightHandVisual, GetActiveLimbAnchor(false, walkingMode, LimbKind.Arm), false);
    }

    private void AttachHandVisual(Transform handVisual, Transform anchor, bool left)
    {
        if (handVisual == null || anchor == null)
            return;

        if (handVisual.parent != anchor)
            handVisual.SetParent(anchor, false);

        handVisual.localPosition = Vector3.zero;
        handVisual.localRotation = Quaternion.identity;
        handVisual.localScale = left ? _leftHandVisualLocalScale : _rightHandVisualLocalScale;
    }

    private Transform GetActiveHandAnchor(bool left, bool walkingMode) => GetActiveLimbAnchor(left, walkingMode, LimbKind.Arm);

    private Transform GetActiveFootAnchor(bool left, bool walkingMode) => GetActiveLimbAnchor(left, walkingMode, LimbKind.Leg);

    private Transform GetActiveLimbAnchor(bool left, bool walkingMode, LimbKind limbKind)
    {
        if (walkingMode)
        {
            if (limbKind == LimbKind.Leg)
                return left ? leftWalkFootTarget : rightWalkFootTarget;

            return left ? leftWalkHandTarget : rightWalkHandTarget;
        }

        if (limbKind == LimbKind.Leg)
        {
            Transform skiFootAnchor = left ? leftSkiFootAnchor : rightSkiFootAnchor;
            if (skiFootAnchor != null)
                return skiFootAnchor;

            Transform skiBindingAnchor = left ? leftSkiBindingAnchor : rightSkiBindingAnchor;
            if (skiBindingAnchor != null)
                return skiBindingAnchor;

            return left ? _leftSkiTransform : _rightSkiTransform;
        }

        Transform skiHandAnchor = left ? leftSkiHandAnchor : rightSkiHandAnchor;
        if (skiHandAnchor != null)
            return skiHandAnchor;

        Transform poleGripAnchor = left ? leftPoleGripAnchor : rightPoleGripAnchor;
        if (poleGripAnchor != null)
            return poleGripAnchor;

        PoleContact pole = left ? _leftPoleContact : _rightPoleContact;
        return pole != null ? pole.PoleRoot : null;
    }

    private bool IsSeatedPoseActive()
    {
        return IsRiderPoseActive() ||
               _seatedPoseOverride ||
               (walkingController != null && walkingController.IsRiderPoseActive);
    }

    private float GetRunPose01(bool walkingMode)
    {
        if (!walkingMode || walkingController == null || IsSeatedPoseActive())
            return 0f;

        return Mathf.Clamp01(walkingController.WalkRunBlend01);
    }

    private float GetJumpCharge01(bool walkingMode)
    {
        if (IsSeatedPoseActive())
            return 0f;

        if (walkingMode && walkingController != null)
            return Mathf.Clamp01(walkingController.WalkJumpCharge01);

        if (!walkingMode && skiController != null)
            return Mathf.Clamp01(skiController.SkiJumpCharge01);

        return 0f;
    }

    private float GetJumpRelease01(bool walkingMode)
    {
        if (IsSeatedPoseActive())
            return 0f;

        if (walkingMode && walkingController != null)
            return Mathf.Clamp01(walkingController.WalkJumpReleaseVisual01);

        if (!walkingMode && skiController != null)
            return Mathf.Clamp01(skiController.SkiJumpReleaseVisual01);

        return 0f;
    }

    private float GetLandingPose01(bool walkingMode)
    {
        if (IsSeatedPoseActive())
            return 0f;

        if (walkingMode && walkingController != null)
            return Mathf.Clamp01(walkingController.WalkLandingPose01);

        if (!walkingMode && skiController != null)
            return Mathf.Clamp01(skiController.SkiLandingPose01);

        return 0f;
    }

    private float GetBodyPoseVerticalOffset(bool walkingMode)
    {
        if (IsRiderPoseActive())
            return -Mathf.Max(0f, GetRiderBodyDrop());

        if (_seatedPoseOverride || (walkingController != null && walkingController.IsRiderPoseActive))
            return -Mathf.Max(0f, seatedBodyDrop);

        float charge = GetJumpCharge01(walkingMode);
        float release = GetJumpRelease01(walkingMode);
        float landing = GetLandingPose01(walkingMode);

        float squatDrop = walkingMode ? walkJumpBodyDrop : skiJumpBodyDrop;

        float offset =
            (-Mathf.Max(0f, squatDrop) * charge) +
            (Mathf.Max(0f, jumpReleaseBodyLift) * release);

        if (walkingMode)
            offset += -Mathf.Max(0f, walkLandingBodyDrop) * landing;
        else
            offset += -Mathf.Max(0f, skiLandingBodyDrop) * landing;

        return offset;
    }

    private Vector3 ApplyWalkAnchorPoseOffset(
    Transform anchor,
    Vector3 baseTargetLocalPosition,
    bool left,
    LimbKind limbKind,
    bool walkingMode)
    {
        if (anchor == null)
            return baseTargetLocalPosition;

        if (IsRiderPoseActive())
            return ApplyRiderAnchorOffset(anchor, baseTargetLocalPosition, limbKind);

        Vector3 target = baseTargetLocalPosition;

        Vector3 localForward = GetAnchorParentLocalDirection(anchor, GetBodyForward(), Vector3.forward);
        Vector3 localUp = GetAnchorParentLocalDirection(anchor, GetBodyUp(), Vector3.up);

        if (_seatedPoseOverride || (walkingController != null && walkingController.IsRiderPoseActive))
        {
            if (limbKind == LimbKind.Leg)
            {
                target += localForward * seatedFootForward;
                target += localUp * seatedFootUp;
            }
            else
            {
                target += localForward * seatedHandForward;
                target += localUp * -seatedHandDown;
            }

            return target;
        }

        float charge = GetJumpCharge01(walkingMode);
        float release = GetJumpRelease01(walkingMode);
        float landing = GetLandingPose01(walkingMode);

        if (charge <= 0.001f && release <= 0.001f && landing <= 0.001f)
            return target;

        if (limbKind == LimbKind.Leg)
        {
            target += localForward * (jumpSquatFootForward * charge);
            target += localForward * (walkLandingFootForward * landing);
            target += localUp * (-0.03f * release);
        }
        else
        {
            target += localForward * (-jumpSquatHandBack * charge);
            target += localForward * (-walkLandingHandBack * landing);

            target += localUp * (-jumpSquatHandDown * charge);
            target += localUp * (-walkLandingHandDown * landing);

            target += localUp * (jumpReleaseBodyLift * 0.65f * release);
        }

        return target;
    }

    private float GetWalkAirTarget01(bool walkingMode)
    {
        if (!walkingMode || walkingController == null || IsSeatedPoseActive())
            return 0f;

        return walkingController.IsWalkAirborne ? 1f : 0f;
    }

    private float GetWalkAirPose01(bool walkingMode)
    {
        if (!walkingMode || IsSeatedPoseActive())
            return 0f;

        return Mathf.Clamp01(_walkAirPoseBlend);
    }

    private Vector3 GetWalkVisualVelocity()
    {
        if (walkingController != null && walkingController.IsWalkingMode)
            return walkingController.WalkVelocity;

        return GetVelocity();
    }

    private void UpdateWalkAirPoseBlend(bool walkingMode, float dt)
    {
        float target = GetWalkAirTarget01(walkingMode);

        // Let the takeoff stretch read for a moment before the loose airborne pose fully takes over.
        float release = GetJumpRelease01(walkingMode);
        if (release > 0.001f)
            target *= 1f - Mathf.Clamp01(release * walkAirJumpReleaseSuppression);

        float speed = target > _walkAirPoseBlend
            ? walkAirPoseBlendInSpeed
            : walkAirPoseBlendOutSpeed;

        float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * dt);
        _walkAirPoseBlend = Mathf.Lerp(_walkAirPoseBlend, target, t);
    }

    private Vector3 GetWalkAirVelocityLagLocalOffset(
        Transform anchor,
        float amount,
        bool walkingMode)
    {
        float air01 = GetWalkAirPose01(walkingMode);
        if (air01 <= 0.001f || anchor == null || amount <= 0f)
            return Vector3.zero;

        Vector3 velocity = GetWalkVisualVelocity();
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, GetBodyUp());

        if (planarVelocity.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        float speed01 = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, walkAirVelocityForFullLag));
        Vector3 lagWorld = -planarVelocity.normalized;

        return GetAnchorParentLocalDirection(anchor, lagWorld, Vector3.back) * (amount * speed01 * air01);
    }

    private bool IsRiderPoseActive()
    {
        return _riderPoseMode != RiderPoseMode.None;
    }

    private bool IsSnowmobileRiderPose()
    {
        return _riderPoseMode == RiderPoseMode.SnowmobileDriver ||
               _riderPoseMode == RiderPoseMode.SnowmobilePassenger;
    }

    private float GetRiderBodyDrop()
    {
        switch (_riderPoseMode)
        {
            case RiderPoseMode.LiftSeated:
                return liftSeatedBodyDrop;
            case RiderPoseMode.SnowmobileDriver:
                return snowmobileDriverBodyDrop;
            case RiderPoseMode.SnowmobilePassenger:
                return snowmobilePassengerBodyDrop;
            default:
                return 0f;
        }
    }

    private float GetRiderFootForward()
    {
        switch (_riderPoseMode)
        {
            case RiderPoseMode.LiftSeated:
                return liftSeatedFootForward;
            case RiderPoseMode.SnowmobileDriver:
                return snowmobileDriverFootForward;
            case RiderPoseMode.SnowmobilePassenger:
                return snowmobilePassengerFootForward;
            default:
                return 0f;
        }
    }

    private float GetRiderFootUp()
    {
        switch (_riderPoseMode)
        {
            case RiderPoseMode.LiftSeated:
                return liftSeatedFootUp;
            case RiderPoseMode.SnowmobileDriver:
                return snowmobileDriverFootUp;
            case RiderPoseMode.SnowmobilePassenger:
                return snowmobilePassengerFootUp;
            default:
                return 0f;
        }
    }

    private float GetRiderHandForwardFallback()
    {
        switch (_riderPoseMode)
        {
            case RiderPoseMode.LiftSeated:
                return liftSeatedHandForward;
            case RiderPoseMode.SnowmobileDriver:
                return snowmobileDriverHandForwardFallback;
            case RiderPoseMode.SnowmobilePassenger:
                return snowmobilePassengerHandForwardFallback;
            default:
                return 0f;
        }
    }

    private float GetRiderHandDownFallback()
    {
        switch (_riderPoseMode)
        {
            case RiderPoseMode.LiftSeated:
                return liftSeatedHandDown;
            case RiderPoseMode.SnowmobileDriver:
                return snowmobileDriverHandDownFallback;
            case RiderPoseMode.SnowmobilePassenger:
                return snowmobilePassengerHandDownFallback;
            default:
                return 0f;
        }
    }

    private float GetRiderLegBend()
    {
        switch (_riderPoseMode)
        {
            case RiderPoseMode.LiftSeated:
                return liftSeatedLegBend;
            case RiderPoseMode.SnowmobileDriver:
                return snowmobileDriverLegBend;
            case RiderPoseMode.SnowmobilePassenger:
                return snowmobilePassengerLegBend;
            default:
                return 0f;
        }
    }

    private float GetRiderArmBend()
    {
        switch (_riderPoseMode)
        {
            case RiderPoseMode.LiftSeated:
                return liftSeatedArmBend;
            case RiderPoseMode.SnowmobileDriver:
                return snowmobileDriverArmBend;
            case RiderPoseMode.SnowmobilePassenger:
                return snowmobilePassengerArmBend;
            default:
                return 0f;
        }
    }

    private Transform GetRiderHandTarget(bool left)
    {
        return left ? _riderLeftHandTarget : _riderRightHandTarget;
    }

    private Vector3 ApplyRiderAnchorOffset(
        Transform anchor,
        Vector3 baseLocalPosition,
        LimbKind limbKind)
    {
        if (!IsRiderPoseActive() || anchor == null)
            return baseLocalPosition;

        Vector3 target = baseLocalPosition;

        Vector3 localForward = GetAnchorParentLocalDirection(anchor, GetBodyForward(), Vector3.forward);
        Vector3 localUp = GetAnchorParentLocalDirection(anchor, GetBodyUp(), Vector3.up);

        if (limbKind == LimbKind.Leg)
        {
            target += localForward * GetRiderFootForward();
            target += localUp * GetRiderFootUp();
        }
        else
        {
            target += localForward * GetRiderHandForwardFallback();
            target += localUp * -GetRiderHandDownFallback();
        }

        return target;
    }

    private float GetWalkAirborneTarget01(bool walkingMode)
    {
        if (!walkingMode || walkingController == null || IsSeatedPoseActive())
            return 0f;

        return walkingController.IsWalkAirborne ? 1f : 0f;
    }

    private float GetWalkAirbornePose01(bool walkingMode)
    {
        if (!walkingMode || IsSeatedPoseActive())
            return 0f;

        return Mathf.Clamp01(_walkAirborneBlend);
    }

    private void UpdateWalkAirborneBlend(bool walkingMode, float dt)
    {
        float target = GetWalkAirborneTarget01(walkingMode);

        // Let the jump release stretch lead for a brief moment before the airborne
        // light-ragdoll drag fully takes over.
        float release = GetJumpRelease01(walkingMode);
        if (release > 0.001f)
            target *= 1f - Mathf.Clamp01(release * walkAirborneJumpReleaseSuppression);

        float speed = target > _walkAirborneBlend
            ? walkAirborneBlendInSpeed
            : walkAirborneBlendOutSpeed;

        float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * dt);
        _walkAirborneBlend = Mathf.Lerp(_walkAirborneBlend, target, t);
    }

    private Vector3 GetWalkAirborneAnchorOffset(
        Transform anchor,
        bool left,
        LimbKind limbKind,
        bool walkingMode)
    {
        float airborne = GetWalkAirbornePose01(walkingMode);
        if (airborne <= 0.001f || anchor == null)
            return Vector3.zero;

        Vector3 velocity = GetVelocity();
        float speed = velocity.magnitude;
        if (speed <= 0.05f)
            return Vector3.zero;

        float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, walkAirborneVelocityForFullDrag));

        // Drag behind actual movement direction. This includes vertical velocity, so:
        // - while rising, limbs are pulled slightly downward/back
        // - while falling, limbs are pulled slightly upward/back
        Vector3 dragWorld = -velocity.normalized;

        Vector3 localDrag = GetAnchorParentLocalDirection(
            anchor,
            dragWorld,
            limbKind == LimbKind.Arm ? Vector3.back : Vector3.back);

        Vector3 localOutward = GetAnchorParentLocalDirection(
            anchor,
            GetBodyRight() * (left ? -1f : 1f),
            left ? Vector3.left : Vector3.right);

        float dragAmount = limbKind == LimbKind.Arm
            ? walkAirborneHandDrag
            : walkAirborneFootDrag;

        Vector3 offset = Vector3.zero;
        offset += localDrag * (dragAmount * speed01 * airborne);
        offset += localOutward * (walkAirborneSideSpread * speed01 * airborne);

        return offset;
    }

    private void UpdateWalkCycle(bool walkingMode)
    {
        if (!_walkAnchorBaseLocalsInitialized)
            CacheWalkAnchorBaseLocals();

        float dt = GetVisualDeltaTime();
        if (dt <= 0f)
            return;

        UpdateWalkAirPoseBlend(walkingMode, dt);

        bool groundedWalkVisual =
            walkingMode &&
            walkingController != null &&
            walkingController.IsWalkGrounded &&
            !IsRiderPoseActive();

        float moveAmount = 0f;

        if (groundedWalkVisual)
        {
            float inputAmount = walkingController.WalkMoveBlend01;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(GetVelocity(), Vector3.up);
            float speedAmount = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, walkPlanarSpeedForFullCycle));

            moveAmount = Mathf.Max(inputAmount, speedAmount);
        }

        float targetBlend = moveAmount > walkMoveThreshold ? moveAmount : 0f;
        float blendSpeed = targetBlend > _walkMotionBlend ? walkCycleSpeed : walkIdleReturnSpeed;
        float blendT = 1f - Mathf.Exp(-blendSpeed * dt);
        _walkMotionBlend = Mathf.Lerp(_walkMotionBlend, targetBlend, blendT);

        if (_walkMotionBlend > 0.001f && groundedWalkVisual)
        {
            float run01 = GetRunPose01(walkingMode);
            float speedMul = Mathf.Lerp(1f, runCycleSpeedMultiplier, run01);
            _walkCycleTime += dt * walkCycleSpeed * speedMul * _walkMotionBlend;
        }

        AnimateWalkAnchors(walkingMode, dt);
    }

    private void UpdateSuspension(bool walkingMode)
    {
        if (suspensionVisualRoot == null)
            return;

        Vector3 targetLocalPos = _suspensionBaseLocalPos;

        if (!walkingMode && enableSuspension)
        {
            Vector3 up = GetBodyUp();
            float compression = 0f;

            bool leftGrounded = skiController.LeftSkiContactRef != null && skiController.LeftSkiContactRef.IsGrounded;
            bool rightGrounded = skiController.RightSkiContactRef != null && skiController.RightSkiContactRef.IsGrounded;
            if (leftGrounded || rightGrounded)
            {
                float terrainCompression = Mathf.Clamp01(Vector3.Angle(GetGroundNormal(), Vector3.up) / 50f);
                compression += terrainCompression * suspensionCompressionFromTerrain;
                compression += skiController.Tuck01 * suspensionCompressionFromTuck;
            }

            compression = Mathf.Clamp(compression, -suspensionMaxOffset, suspensionMaxOffset);
            Vector3 localOffset = suspensionVisualRoot.parent != null
                ? suspensionVisualRoot.parent.InverseTransformDirection(-up * compression)
                : (-up * compression);
            targetLocalPos += localOffset;
        }

        float poseYOffset = GetBodyPoseVerticalOffset(walkingMode);
        if (Mathf.Abs(poseYOffset) > 0.0001f)
        {
            Vector3 poseOffsetWorld = GetBodyUp() * poseYOffset;
            Vector3 poseOffsetLocal = suspensionVisualRoot.parent != null
                ? suspensionVisualRoot.parent.InverseTransformDirection(poseOffsetWorld)
                : poseOffsetWorld;

            targetLocalPos += poseOffsetLocal;
        }

        float lerpT = 1f - Mathf.Exp(-suspensionSpeed * GetVisualDeltaTime());
        _suspensionSmoothedLocalPos = Vector3.Lerp(_suspensionSmoothedLocalPos, targetLocalPos, lerpT);
        suspensionVisualRoot.localPosition = _suspensionSmoothedLocalPos;
    }

    private void CacheBodyHeightFromCurrentSkiSupport()
    {
        Transform body = GetBodyTransform();
        if (body == null)
            return;

        float supportY = GetAverageSkiSupportY();
        float offset = body.position.y - supportY;

        float minHeight = Mathf.Min(minValidWalkBodyHeightFromFeet, maxValidWalkBodyHeightFromFeet);
        float maxHeight = Mathf.Max(minValidWalkBodyHeightFromFeet, maxValidWalkBodyHeightFromFeet);

        // Do not allow stacked/ragdoll/compressed ski positions to poison the walk body-height cache.
        if (offset < minHeight || offset > maxHeight)
            return;

        _cachedWalkBodyHeightFromSkiMode = offset;
    }

    private void CacheSkiLegReachForWalkMode()
    {
        if (!preserveSkiLegReachInWalkMode)
            return;

        if (walkingController != null && walkingController.IsWalkingMode)
            return;

        if (skiController != null && skiController.IsStacked)
            return;

        Vector3 leftHip = GetHipPosition(true);
        Vector3 rightHip = GetHipPosition(false);

        Vector3 leftSkiFoot = GetLimbEndpointWorldPosition(true, false, LimbKind.Leg);
        Vector3 rightSkiFoot = GetLimbEndpointWorldPosition(false, false, LimbKind.Leg);

        float leftReach = Vector3.Distance(leftHip, leftSkiFoot);
        float rightReach = Vector3.Distance(rightHip, rightSkiFoot);

        float minReach = Mathf.Min(minValidWalkLegReach, maxValidWalkLegReach);
        float maxReach = Mathf.Max(minValidWalkLegReach, maxValidWalkLegReach);

        bool validLeft = leftReach >= minReach && leftReach <= maxReach;
        bool validRight = rightReach >= minReach && rightReach <= maxReach;

        if (!validLeft && !validRight)
            return;

        if (validLeft)
            _cachedLeftSkiLegReachForWalk = leftReach;

        if (validRight)
            _cachedRightSkiLegReachForWalk = rightReach;

        if (!validLeft)
            _cachedLeftSkiLegReachForWalk = _cachedRightSkiLegReachForWalk;

        if (!validRight)
            _cachedRightSkiLegReachForWalk = _cachedLeftSkiLegReachForWalk;

        _hasCachedSkiLegReachForWalk = true;
    }

    private bool CanCacheBodyHeightFromCurrentSkiSupport()
    {
        if (skiController == null)
            return false;

        // Critical: never cache walk body height from a stacked/ragdoll pose.
        if (skiController.IsStacked)
            return false;

        // Also avoid caching during the visual blend-out after stack has been cleared.
        if (_stackPhysicsActive || _stackBlend > 0.001f || HasInitializedStackEndpointState())
            return false;

        return true;
    }

    private void PrepareVisualsForWalkModeTransition()
    {
        PrepareForWalkModeHandoff(
            enteredFromStack: _stackPhysicsActive || _stackBlend > 0.001f || HasInitializedStackEndpointState());
    }

    private void RepairCachedWalkBodyHeight()
    {
        float minHeight = Mathf.Min(minValidWalkBodyHeightFromFeet, maxValidWalkBodyHeightFromFeet);
        float maxHeight = Mathf.Max(minValidWalkBodyHeightFromFeet, maxValidWalkBodyHeightFromFeet);
        float fallback = Mathf.Clamp(fallbackWalkBodyHeightFromFeet, minHeight, maxHeight);

        if (_cachedWalkBodyHeightFromSkiMode < minHeight || _cachedWalkBodyHeightFromSkiMode > maxHeight)
            _cachedWalkBodyHeightFromSkiMode = fallback;
    }

    private bool HasInitializedStackEndpointState()
    {
        return _leftArmStackEndpoint.initialized ||
               _rightArmStackEndpoint.initialized ||
               _leftLegStackEndpoint.initialized ||
               _rightLegStackEndpoint.initialized;
    }

    private void UpdateWalkBodyHeight(bool walkingMode)
    {
        if (suspensionVisualRoot == null)
            return;

        if (!keepBodyHeightRelativeToFeetInWalkMode)
            return;

        if (!walkingMode)
            return;

        if (!HasValidWalkFootTargets())
            return;

        Transform parent = suspensionVisualRoot.parent;
        if (parent == null)
            return;

        float supportY = GetAverageWalkFootSupportY();
        float desiredWorldBodyY = supportY + _cachedWalkBodyHeightFromSkiMode + GetBodyPoseVerticalOffset(walkingMode);

        Vector3 desiredWorldPos = suspensionVisualRoot.position;
        desiredWorldPos.y = desiredWorldBodyY;

        Vector3 desiredLocalPos = parent.InverseTransformPoint(desiredWorldPos);

        Vector3 targetLocalPos = _suspensionSmoothedLocalPos;
        targetLocalPos.y = desiredLocalPos.y;

        // Preserve actual ski-mode hip-to-foot reach in walk mode.
        // This prevents walk feet from making the rendered legs slightly shorter than the ski stance.
        targetLocalPos.y = ApplyWalkLegReachTargetToLocalY(parent, targetLocalPos.y);

        float t = 1f - Mathf.Exp(-walkBodyHeightFollowSpeed * GetVisualDeltaTime());
        _suspensionSmoothedLocalPos = Vector3.Lerp(_suspensionSmoothedLocalPos, targetLocalPos, t);
        suspensionVisualRoot.localPosition = _suspensionSmoothedLocalPos;
    }

    private void DrawArm(bool left, bool walkingMode, LineRenderer lr)
    {
        if (lr == null)
            return;

        Vector3 root = GetShoulderPosition(left);
        Vector3 authoredEnd = GetLimbEndpointWorldPosition(left, walkingMode, LimbKind.Arm);
        Vector3 end = ResolveStackEndpoint(left, LimbKind.Arm, root, authoredEnd);

        if (walkingMode)
            end = ClampStackEndpointToSegmentLengths(root, end, LimbKind.Arm, left);

        Vector3 bend = ResolveJointWorldPosition(left ? SkierLimbJoint.LeftElbow : SkierLimbJoint.RightElbow, root, end, walkingMode);

        if (_stackBlend > 0.001f || walkingMode)
        {
            if (_stackBlend > 0.001f)
                end = ClampStackEndpointToSegmentLengths(root, end, LimbKind.Arm, left);

            bend = SolveStackJoint(root, end, LimbKind.Arm, left, bend);
        }

        SetLinePositions(lr, root, bend, end);

        if (moveHandFootVisualsDuringStack)
            UpdateLooseVisualEndpoint(left ? leftHandVisual : rightHandVisual, end, authoredEnd);
    }

    private void DrawLeg(bool left, bool walkingMode, LineRenderer lr)
    {
        if (lr == null)
            return;

        Vector3 root = GetHipPosition(left);
        Vector3 authoredEnd = GetLimbEndpointWorldPosition(left, walkingMode, LimbKind.Leg);
        Vector3 end = ResolveStackEndpoint(left, LimbKind.Leg, root, authoredEnd);

        if (walkingMode)
            end = ClampStackEndpointToSegmentLengths(root, end, LimbKind.Leg, left);

        Vector3 bend = ResolveJointWorldPosition(left ? SkierLimbJoint.LeftKnee : SkierLimbJoint.RightKnee, root, end, walkingMode);

        if (_stackBlend > 0.001f || walkingMode)
        {
            if (_stackBlend > 0.001f)
                end = ClampStackEndpointToSegmentLengths(root, end, LimbKind.Leg, left);

            bend = SolveStackJoint(root, end, LimbKind.Leg, left, bend);
        }

        SetLinePositions(lr, root, bend, end);

        if (moveHandFootVisualsDuringStack)
            UpdateLooseVisualEndpoint(left ? leftFootVisual : rightFootVisual, end, authoredEnd);
    }


    private void UpdateStackBlend()
    {
        if (!enableStackLimbPhysics || skiController == null)
        {
            _stackBlend = 0f;
            _stackPhysicsActive = false;
            ClearStackEndpointStates();
            return;
        }

        float dt = GetVisualDeltaTime();
        if (dt <= 0f)
            return;

        bool stacked = skiController.IsStacked;
        float target = stacked ? 1f : 0f;
        float speed = target > _stackBlend ? stackBlendInSpeed : stackBlendOutSpeed;

        _stackBlend = Mathf.MoveTowards(_stackBlend, target, speed * dt);
        _stackPhysicsActive = _stackBlend > 0.001f;

        Vector3 bodyVelocity = GetVelocity();
        Vector3 bodyAcceleration = Vector3.zero;

        if (_hasLastBodyVelocity)
            bodyAcceleration = (bodyVelocity - _lastBodyVelocity) / Mathf.Max(dt, 0.0001f);

        _lastBodyVelocity = bodyVelocity;
        _hasLastBodyVelocity = true;

        if (stacked)
        {
            EnsureStackEndpointInitialized(true, LimbKind.Arm, bodyVelocity);
            EnsureStackEndpointInitialized(false, LimbKind.Arm, bodyVelocity);
            EnsureStackEndpointInitialized(true, LimbKind.Leg, bodyVelocity);
            EnsureStackEndpointInitialized(false, LimbKind.Leg, bodyVelocity);

            SimulateStackEndpoint(true, LimbKind.Arm, bodyVelocity, bodyAcceleration, dt);
            SimulateStackEndpoint(false, LimbKind.Arm, bodyVelocity, bodyAcceleration, dt);
            SimulateStackEndpoint(true, LimbKind.Leg, bodyVelocity, bodyAcceleration, dt);
            SimulateStackEndpoint(false, LimbKind.Leg, bodyVelocity, bodyAcceleration, dt);
        }
        else if (_stackBlend <= 0.001f)
        {
            ClearStackEndpointStates();
        }
    }

    private Vector3 ResolveStackEndpoint(bool left, LimbKind limbKind, Vector3 root, Vector3 authoredEnd)
    {
        if (!_stackPhysicsActive)
            return authoredEnd;

        ref StackLimbEndpointState state = ref GetStackEndpointState(left, limbKind);
        if (!state.initialized)
        {
            state.Reset(
                authoredEnd,
                root,
                GetBodyRotation(),
                GetVelocity(),
                GetEndpointGravityMultiplier(left, limbKind),
                GetEndpointDampingMultiplier(left, limbKind));
        }

        Vector3 simulatedEnd = ClampStackEndpointToSegmentLengths(root, state.position, limbKind, left);
        return Vector3.Lerp(authoredEnd, simulatedEnd, _stackBlend);
    }

    private void EnsureStackEndpointInitialized(bool left, LimbKind limbKind, Vector3 bodyVelocity)
    {
        ref StackLimbEndpointState state = ref GetStackEndpointState(left, limbKind);
        if (state.initialized)
            return;

        Vector3 root = limbKind == LimbKind.Arm ? GetShoulderPosition(left) : GetHipPosition(left);
        Vector3 end = GetLimbEndpointWorldPosition(left, false, limbKind);

        Vector3 angularVelocity = targetRigidbody != null ? targetRigidbody.angularVelocity : Vector3.zero;
        Vector3 sourceVelocity = bodyVelocity + Vector3.Cross(angularVelocity, end - root) * stackAngularVelocityInfluence;

        Vector3 inheritedVelocity = BuildContextualStackEndpointVelocity(
            left,
            limbKind,
            transform.position,
            sourceVelocity,
            0.35f);

        state.Reset(
            end,
            root,
            GetBodyRotation(),
            inheritedVelocity,
            GetEndpointGravityMultiplier(left, limbKind),
            GetEndpointDampingMultiplier(left, limbKind));
    }

    private void SimulateStackEndpoint(bool left, LimbKind limbKind, Vector3 bodyVelocity, Vector3 bodyAcceleration, float dt)
    {
        ref StackLimbEndpointState state = ref GetStackEndpointState(left, limbKind);
        if (!state.initialized)
            return;

        Vector3 root = limbKind == LimbKind.Arm ? GetShoulderPosition(left) : GetHipPosition(left);
        Vector3 previousRoot = root - bodyVelocity * dt;

        Vector3 currentVelocity = (state.position - state.previousPosition) / Mathf.Max(dt, 0.0001f);

        bool bodyNearlyStill = bodyVelocity.magnitude <= stackBodySleepSpeed;
        bool endpointNearlyStill = currentVelocity.magnitude <= stackEndpointSleepSpeed;

        if (bodyNearlyStill && endpointNearlyStill)
        {
            state.sleeping = true;
            state.velocity = Vector3.zero;
            state.previousPosition = state.position;
            state.position = ClampStackEndpointToSegmentLengths(root, state.position, limbKind, left);
            ApplyEndpointGroundClearance(ref state.position);
            return;
        }

        state.sleeping = false;

        Vector3 acceleration =
    Vector3.down *
    Mathf.Max(0f, stackEndpointGravity) *
    Mathf.Max(0.01f, state.gravityMultiplier);

        acceleration += bodyAcceleration * Mathf.Clamp01(stackBodyAccelerationInfluence);

        if (enableStackOutwardSpread)
            acceleration += GetStackOutwardAcceleration(left, limbKind, root, state.position);

        if (targetRigidbody != null)
        {
            Vector3 angularVelocity = targetRigidbody.angularVelocity;
            Vector3 radius = state.position - root;
            acceleration += Vector3.Cross(angularVelocity, radius) * stackAngularVelocityInfluence;
        }

        if (stackAnchorSpringAcceleration > 0f)
        {
            Vector3 preferred = GetStackPreferredEndpointPosition(left, limbKind, root, ref state);
            Vector3 springOffset = preferred - state.position;

            acceleration += springOffset * stackAnchorSpringAcceleration;
            acceleration -= currentVelocity * Mathf.Max(0f, stackAnchorSpringDamping);
        }

        Vector3 velocityDamping =
            currentVelocity *
            Mathf.Max(0f, stackEndpointDamping) *
            Mathf.Max(0.01f, state.dampingMultiplier);

        acceleration -= velocityDamping;

        Vector3 next = state.position + (state.position - state.previousPosition) + acceleration * dt * dt;

        state.previousPosition = state.position;
        state.position = next;

        for (int i = 0; i < stackConstraintIterations; i++)
        {
            state.position = ClampStackEndpointToSegmentLengths(root, state.position, limbKind, left);

            // Keep the endpoint from drifting through obvious support height.
            ApplyEndpointGroundClearance(ref state.position);

            // Prevent the constraint from injecting energy forever.
            Vector3 constrainedVelocity = (state.position - state.previousPosition) / Mathf.Max(dt, 0.0001f);
            if (constrainedVelocity.magnitude < stackEndpointSleepSpeed && bodyVelocity.magnitude < stackBodySleepSpeed)
                state.previousPosition = state.position;
        }

        state.velocity = (state.position - state.previousPosition) / Mathf.Max(dt, 0.0001f);
    }

    private Quaternion GetBodyRotation()
    {
        Transform body = GetBodyTransform();
        return body != null ? body.rotation : transform.rotation;
    }

    private float GetEndpointGravityMultiplier(bool left, LimbKind limbKind)
    {
        // Slight asymmetry helps prevent mirrored limbs from settling identically.
        float sideMul = left ? 0.97f : 1.03f;
        float limbMul = limbKind == LimbKind.Arm ? 0.92f : 1.08f;
        return sideMul * limbMul;
    }

    private float GetEndpointDampingMultiplier(bool left, LimbKind limbKind)
    {
        // Arms stay looser. Legs settle a little faster so ski recovery remains readable.
        float sideMul = left ? 0.94f : 1.06f;
        float limbMul = limbKind == LimbKind.Arm ? 0.82f : 1.08f;
        return sideMul * limbMul;
    }

    private Vector3 BuildContextualStackEndpointVelocity(
        bool left,
        LimbKind limbKind,
        Vector3 impactPointWorld,
        Vector3 sourceVelocity,
        float severity01)
    {
        Vector3 root = limbKind == LimbKind.Arm ? GetShoulderPosition(left) : GetHipPosition(left);
        Vector3 end = GetLimbEndpointWorldPosition(left, false, limbKind);

        Vector3 rootToEnd = end - root;
        Vector3 rootToEndDir = SafeNormalizeOrFallback(rootToEnd, GetStackOutwardDirection(left, limbKind));

        float sourceSpeed = sourceVelocity.magnitude;
        if (sourceSpeed <= 0.001f)
            return GetStackOutwardDirection(left, limbKind) * Mathf.Max(0f, stackInitialOutwardVelocity);

        Vector3 sourceDir = sourceVelocity / sourceSpeed;
        float severity = Mathf.Clamp01(severity01);

        float limbMultiplier = limbKind == LimbKind.Arm
            ? Mathf.Max(0f, stackArmImpactVelocityMultiplier)
            : Mathf.Max(0f, stackLegImpactVelocityMultiplier);

        // Endpoints closer to / more exposed to the incoming impact get more velocity.
        float exposure = 1f;
        Vector3 impactToEndpoint = end - impactPointWorld;
        if (impactToEndpoint.sqrMagnitude > 0.0001f)
        {
            float facing = Vector3.Dot(impactToEndpoint.normalized, sourceDir);
            float facing01 = Mathf.InverseLerp(-0.25f, 0.75f, facing);
            exposure = Mathf.Lerp(1f - stackOppositeSideImpactReduction, 1f, facing01);
        }

        Vector3 shared = sourceVelocity * stackSharedImpactVelocity * limbMultiplier * exposure;

        // Tangential component around the shoulder/hip anchor. This makes left/right and arm/leg endpoints diverge.
        Vector3 tangent = Vector3.ProjectOnPlane(sourceVelocity, rootToEndDir) *
                          stackImpactTangentVelocity *
                          limbMultiplier *
                          exposure;

        // Whip component generated from impact direction and each endpoint's anchor vector.
        Vector3 whip = Vector3.zero;
        Vector3 whipAxis = Vector3.Cross(sourceDir, rootToEndDir);
        if (whipAxis.sqrMagnitude > 0.0001f)
        {
            Vector3 whipDir = Vector3.Cross(whipAxis.normalized, rootToEndDir);
            whip = SafeNormalizeOrFallback(whipDir, GetStackOutwardDirection(left, limbKind)) *
                   stackImpactWhipVelocity *
                   limbMultiplier *
                   Mathf.Lerp(0.35f, 1f, severity);
        }

        float outwardMul = limbKind == LimbKind.Arm ? 1f : 0.65f;
        Vector3 outward = GetStackOutwardDirection(left, limbKind) *
                          Mathf.Max(0f, stackInitialOutwardVelocity) *
                          outwardMul *
                          Mathf.Lerp(0.6f, 1.15f, severity);

        return shared + tangent + whip + outward;
    }

    private Vector3 GetStackPreferredEndpointPosition(
        bool left,
        LimbKind limbKind,
        Vector3 root,
        ref StackLimbEndpointState state)
    {
        if (!state.initialized)
            return root;

        Quaternion bodyRotation = GetBodyRotation();
        return root + bodyRotation * state.bodyLocalRootToEndpoint;
    }

    private Vector3 GetStackOutwardAcceleration(bool left, LimbKind limbKind, Vector3 root, Vector3 endpoint)
    {
        float baseAcceleration = limbKind == LimbKind.Arm
            ? stackArmOutwardAcceleration
            : stackLegOutwardAcceleration;

        if (baseAcceleration <= 0f)
            return Vector3.zero;

        GetStackSegmentLengths(limbKind, left, out float upperLength, out float lowerLength);
        float maxReach = Mathf.Max(0.05f, upperLength + lowerLength);

        float reach01 = Mathf.Clamp01(Vector3.Distance(root, endpoint) / maxReach);
        float reachFade = Mathf.Lerp(1f, 1f - stackOutwardReachFade, reach01);

        Vector3 outward = GetStackOutwardDirection(left, limbKind);
        Vector3 acceleration = outward * baseAcceleration;

        // Keep this tiny. The old constant upward lift made hands/feet float above the body.
        if (limbKind == LimbKind.Arm && stackOutwardLiftAcceleration > 0f)
            acceleration += GetBodyUp() * stackOutwardLiftAcceleration * 0.25f;

        return acceleration * Mathf.Clamp01(reachFade);
    }

    private Vector3 GetStackOutwardDirection(bool left, LimbKind limbKind)
    {
        Vector3 right = GetBodyRight();
        Vector3 forward = GetBodyForward();

        // Left side should bias left, right side should bias right.
        Vector3 outward = right * (left ? -1f : 1f);

        // Arms get a tiny backward bias so poles/hands read wider and looser.
        // Legs stay more lateral so skis remain recoverable.
        if (limbKind == LimbKind.Arm)
            outward = (outward - forward * 0.22f).normalized;
        else
            outward = (outward + forward * 0.08f).normalized;

        return SafeNormalizeOrFallback(outward, right * (left ? -1f : 1f));
    }

    private void HandleStacked(SkiController.StackEventInfo info)
    {
        CacheStackEquipmentBasePoses();
        _stackEquipmentVisualsWereApplied = false;

        Vector3 inherited = info.velocity * Mathf.Clamp01(stackImpactVelocityInheritance);

        InitializeStackEndpointFromImpact(true, LimbKind.Arm, info.position, inherited, info.severity01);
        InitializeStackEndpointFromImpact(false, LimbKind.Arm, info.position, inherited, info.severity01);
        InitializeStackEndpointFromImpact(true, LimbKind.Leg, info.position, inherited, info.severity01);
        InitializeStackEndpointFromImpact(false, LimbKind.Leg, info.position, inherited, info.severity01);
    }

    private void HandleStackImpact(SkiController.StackEventInfo info)
    {
        Vector3 inherited = info.velocity * Mathf.Clamp01(stackImpactVelocityInheritance);

        AddStackEndpointVelocity(true, LimbKind.Arm,
            BuildContextualStackEndpointVelocity(true, LimbKind.Arm, info.position, inherited, info.severity01));

        AddStackEndpointVelocity(false, LimbKind.Arm,
            BuildContextualStackEndpointVelocity(false, LimbKind.Arm, info.position, inherited, info.severity01));

        AddStackEndpointVelocity(true, LimbKind.Leg,
            BuildContextualStackEndpointVelocity(true, LimbKind.Leg, info.position, inherited, info.severity01));

        AddStackEndpointVelocity(false, LimbKind.Leg,
            BuildContextualStackEndpointVelocity(false, LimbKind.Leg, info.position, inherited, info.severity01));
    }

    private void HandleRecoveredFromStack(SkiController.StackRecoveryEventInfo info)
    {
        ReleaseStackEquipmentVisuals(force: true);
    }

    private void InitializeStackEndpointFromImpact(
    bool left,
    LimbKind limbKind,
    Vector3 impactPointWorld,
    Vector3 inheritedVelocity,
    float severity01)
    {
        ref StackLimbEndpointState state = ref GetStackEndpointState(left, limbKind);

        Vector3 root = limbKind == LimbKind.Arm ? GetShoulderPosition(left) : GetHipPosition(left);
        Vector3 end = GetLimbEndpointWorldPosition(left, false, limbKind);

        Vector3 contextualVelocity = BuildContextualStackEndpointVelocity(
            left,
            limbKind,
            impactPointWorld,
            inheritedVelocity,
            severity01);

        state.Reset(
            end,
            root,
            GetBodyRotation(),
            contextualVelocity,
            GetEndpointGravityMultiplier(left, limbKind),
            GetEndpointDampingMultiplier(left, limbKind));
    }

    private void AddStackEndpointVelocity(bool left, LimbKind limbKind, Vector3 velocityDelta)
    {
        ref StackLimbEndpointState state = ref GetStackEndpointState(left, limbKind);
        if (!state.initialized)
            return;

        float dt = Mathf.Max(GetVisualDeltaTime(), 0.016f);
        state.previousPosition -= velocityDelta * dt;
        state.sleeping = false;
    }

    private Vector3 ClampStackEndpointToSegmentLengths(Vector3 root, Vector3 end, LimbKind limbKind, bool left)
    {
        GetStackSegmentLengths(limbKind, left, out float upperLength, out float lowerLength);

        float totalLength = Mathf.Max(0.05f, upperLength + lowerLength);
        Vector3 toEnd = end - root;
        float distance = toEnd.magnitude;

        if (distance <= totalLength || distance <= 0.0001f)
            return end;

        return root + (toEnd / distance) * totalLength;
    }

    private Vector3 SolveStackJoint(Vector3 root, Vector3 end, LimbKind limbKind, bool left, Vector3 fallbackJoint)
    {
        GetStackSegmentLengths(limbKind, left, out float upperLength, out float lowerLength);

        Vector3 segment = end - root;
        float distance = Mathf.Max(0.0001f, segment.magnitude);
        Vector3 dir = segment / distance;

        float minReach = Mathf.Max(0.02f, Mathf.Abs(upperLength - lowerLength) + 0.01f);
        float maxReach = Mathf.Max(minReach + 0.01f, upperLength + lowerLength - 0.01f);
        float clampedDistance = Mathf.Clamp(distance, minReach, maxReach);

        if (Mathf.Abs(clampedDistance - distance) > 0.0001f)
        {
            end = root + dir * clampedDistance;
            segment = end - root;
            distance = Mathf.Max(0.0001f, segment.magnitude);
            dir = segment / distance;
        }

        float along = Mathf.Clamp(
            (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance),
            0f,
            upperLength);

        float bendHeight = Mathf.Sqrt(Mathf.Max(0.0001f, upperLength * upperLength - along * along));

        Vector3 side = Vector3.ProjectOnPlane(fallbackJoint - (root + dir * along), dir);
        if (side.sqrMagnitude < 0.0001f)
            side = limbKind == LimbKind.Arm
                ? GetBodyRight() * (left ? -1f : 1f)
                : GetBodyForward();

        side = SafeNormalizeOrFallback(side, GetBodyRight() * (left ? -1f : 1f));

        return root + dir * along + side * bendHeight;
    }

    private void GetStackSegmentLengths(LimbKind limbKind, bool left, out float upperLength, out float lowerLength)
    {
        if (limbKind == LimbKind.Arm)
        {
            upperLength = fallbackUpperArmLength;
            lowerLength = fallbackLowerArmLength;
        }
        else
        {
            upperLength = fallbackUpperLegLength;
            lowerLength = fallbackLowerLegLength;
        }

        if (!useRigAssistLengthsForStack || skiController == null)
            return;

        TrickPoseProfileSO profile = skiController.TrickPoseProfile;
        if (profile == null || profile.rigAssistSettings == null || !profile.rigAssistSettings.restSegments.captured)
            return;

        TrickPoseRigSegmentLengthSet lengths = profile.rigAssistSettings.restSegments;

        if (limbKind == LimbKind.Arm)
        {
            upperLength = Mathf.Max(0.01f, left ? lengths.leftUpperArm : lengths.rightUpperArm);
            lowerLength = Mathf.Max(0.01f, left ? lengths.leftLowerArm : lengths.rightLowerArm);
        }
        else
        {
            upperLength = Mathf.Max(0.01f, left ? lengths.leftUpperLeg : lengths.rightUpperLeg);
            lowerLength = Mathf.Max(0.01f, left ? lengths.leftLowerLeg : lengths.rightLowerLeg);
        }
    }

    private ref StackLimbEndpointState GetStackEndpointState(bool left, LimbKind limbKind)
    {
        if (limbKind == LimbKind.Arm)
            return ref (left ? ref _leftArmStackEndpoint : ref _rightArmStackEndpoint);

        return ref (left ? ref _leftLegStackEndpoint : ref _rightLegStackEndpoint);
    }

    private void ClearStackEndpointStates()
    {
        _leftArmStackEndpoint.Clear();
        _rightArmStackEndpoint.Clear();
        _leftLegStackEndpoint.Clear();
        _rightLegStackEndpoint.Clear();
    }

    private void ApplyEndpointGroundClearance(ref Vector3 position)
    {
        if (!constrainStackEndpointsToGround || skiController == null)
            return;

        Vector3 origin = position + Vector3.up * stackEndpointGroundProbeUp;
        float distance = Mathf.Max(0.01f, stackEndpointGroundProbeUp + stackEndpointGroundProbeDown);

        if (!Physics.SphereCast(
                origin,
                Mathf.Max(0.001f, stackEndpointGroundRadius),
                Vector3.down,
                out RaycastHit hit,
                distance,
                skiController.GroundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float minY = hit.point.y + stackEndpointGroundSkin;

        // Only prevent terrain penetration. Never lift limbs to the ski support height.
        if (position.y < minY)
            position.y = minY;
    }

    private void UpdateLooseVisualEndpoint(Transform visual, Vector3 simulatedWorldPosition, Vector3 authoredWorldPosition)
    {
        if (visual == null || _stackBlend <= 0.001f)
            return;

        // Blend in smoothly, then hard-attach. Persistent soft lerp made gloves/boots feel disconnected.
        float hardAttach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.75f, _stackBlend));
        visual.position = Vector3.Lerp(authoredWorldPosition, simulatedWorldPosition, hardAttach);
    }

    private void UpdateStackEquipmentVisuals()
    {
        bool stacked = skiController != null && skiController.IsStacked;

        if (!moveEquipmentVisualsDuringStack || !stacked || _stackBlend <= 0.001f || !_stackPhysicsActive)
        {
            ReleaseStackEquipmentVisuals(force: false);
            return;
        }

        _stackEquipmentVisualsWereApplied = true;

        ApplyStackSkiVisual(true);
        ApplyStackSkiVisual(false);
        ApplyStackPoleVisual(true);
        ApplyStackPoleVisual(false);
    }

    private void ReleaseStackEquipmentVisuals(bool force)
    {
        if (!force && !_stackEquipmentVisualsWereApplied)
            return;

        ClearStackEquipmentVisualOverrides();
        RestoreStackEquipmentVisualRoots();

        _stackEquipmentVisualsWereApplied = false;
    }

    private void ClearStackEquipmentVisualOverrides()
    {
        // Legacy override path from the previous pass. Keep these clears so old queued overrides cannot linger.
        if (skiController != null)
            skiController.ClearAllStackSkiVisualOverrides();

        if (_leftPoleContact != null)
            _leftPoleContact.ClearStackVisualOverride();

        if (_rightPoleContact != null)
            _rightPoleContact.ClearStackVisualOverride();
    }

    private void CacheStackEquipmentBasePoses()
    {
        _leftStackSkiBasePose.Capture(GetStackSkiVisualRoot(true));
        _rightStackSkiBasePose.Capture(GetStackSkiVisualRoot(false));

        // Poles are intentionally not cached/restored here.
        // Their regular animation is owned by PoleContact, and stack pinning is applied as a temporary override.
    }

    private void RestoreStackEquipmentVisualRoots()
    {
        RestoreStackSkiVisualRoot(ref _leftStackSkiBasePose, GetStackSkiVisualRoot(true));
        RestoreStackSkiVisualRoot(ref _rightStackSkiBasePose, GetStackSkiVisualRoot(false));

        // Do NOT restore PoleContact.PoleRoot here.
        // PoleContact owns regular pole stroke animation in LateUpdate().
        // Restoring pole roots from SkierLimbLineVisual after PoleContact runs wipes out pole input animation.
    }

    private void RestoreStackSkiVisualRoot(ref StackEquipmentRootPose cachedPose, Transform currentRoot)
    {
        if (currentRoot == null)
            return;

        if (!cachedPose.captured || cachedPose.root != currentRoot)
        {
            cachedPose.Capture(currentRoot);
            return;
        }

        cachedPose.Restore();
    }

    private void ApplyStackSkiVisual(bool left)
    {
        if (skiController == null)
            return;

        Transform skiRoot = left ? skiController.LeftSkiTransform : skiController.RightSkiTransform;
        if (skiRoot == null)
            return;

        Vector3 hip = GetHipPosition(left);
        Vector3 authoredFoot = GetLimbEndpointWorldPosition(left, false, LimbKind.Leg);
        Vector3 simulatedFoot = ResolveStackEndpoint(left, LimbKind.Leg, hip, authoredFoot);

        Transform bindingAnchor = GetStackSkiBindingAnchor(left, skiRoot);

        float follow = Mathf.Clamp01(_stackBlend * stackSkiAnchorFollow);
        Vector3 targetBindingWorld = Vector3.Lerp(authoredFoot, simulatedFoot, follow);

        Vector3 limbUp = SafeNormalizeOrFallback(hip - targetBindingWorld, GetBodyUp());

        Vector3 endpointVelocity = GetStackEndpointVelocity(left, LimbKind.Leg);
        Vector3 forward = Vector3.ProjectOnPlane(endpointVelocity, limbUp);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(GetVelocity(), limbUp);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(skiRoot.forward, limbUp);

        forward = SafeNormalizeOrFallback(forward, GetBodyForward());

        Quaternion ragdollRotation =
            Quaternion.LookRotation(forward, limbUp) *
            Quaternion.Euler(stackSkiRotationOffsetEuler);

        float rotationFollow = Mathf.Clamp01(_stackBlend * stackSkiRotationFollow);
        Quaternion targetRotation = Quaternion.Slerp(skiRoot.rotation, ragdollRotation, rotationFollow);

        Vector3 targetRootPosition = CalculateRootPositionPinnedToAnchor(
            skiRoot,
            bindingAnchor,
            targetBindingWorld,
            targetRotation);

        skiController.ApplyStackSkiVisualOverrideWorldImmediate(
            left,
            targetRootPosition,
            targetRotation,
            1f);
    }

    private void ApplyStackPoleVisual(bool left)
    {
        PoleContact pole = left ? _leftPoleContact : _rightPoleContact;
        if (pole == null || pole.PoleRoot == null)
            return;

        Transform poleRoot = pole.PoleRoot;

        Vector3 shoulder = GetShoulderPosition(left);
        Vector3 authoredHand = GetLimbEndpointWorldPosition(left, false, LimbKind.Arm);
        Vector3 simulatedHand = ResolveStackEndpoint(left, LimbKind.Arm, shoulder, authoredHand);

        // Use the same hard-attach behaviour as the hand visual so the pole handle does not lag behind.
        float handAttach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.75f, _stackBlend));
        float poleAttach = Mathf.Clamp01(Mathf.Max(handAttach, _stackBlend * stackPoleAnchorFollow));

        Vector3 targetHandleWorld = Vector3.Lerp(authoredHand, simulatedHand, poleAttach);

        // Once the hand visual is effectively attached, pin the pole to the actual visible hand position.
        Transform handVisual = left ? leftHandVisual : rightHandVisual;
        if (moveHandFootVisualsDuringStack && handVisual != null && poleAttach >= 0.98f)
            targetHandleWorld = handVisual.position;

        Transform handleAnchor = GetStackPoleHandleAnchor(left, poleRoot);

        Vector3 towardShoulder = SafeNormalizeOrFallback(shoulder - targetHandleWorld, GetBodyUp());

        Vector3 endpointVelocity = GetStackEndpointVelocity(left, LimbKind.Arm);
        Vector3 forward = Vector3.ProjectOnPlane(endpointVelocity, towardShoulder);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(GetVelocity(), towardShoulder);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(poleRoot.forward, towardShoulder);

        forward = SafeNormalizeOrFallback(forward, GetBodyForward());

        Quaternion ragdollRotation =
            Quaternion.LookRotation(forward, towardShoulder) *
            Quaternion.Euler(stackPoleRotationOffsetEuler);

        float rotationFollow = Mathf.Clamp01(_stackBlend * stackPoleRotationFollow);
        Quaternion targetRotation = Quaternion.Slerp(poleRoot.rotation, ragdollRotation, rotationFollow);

        Vector3 targetRootPosition = CalculateRootPositionPinnedToAnchor(
            poleRoot,
            handleAnchor,
            targetHandleWorld,
            targetRotation);

        // Important: route through PoleContact instead of permanently taking ownership of PoleRoot here.
        // PoleContact owns regular pole animation. During stack, this temporary override keeps the handle pinned.
        pole.ApplyStackVisualOverrideWorldImmediate(targetRootPosition, targetRotation, 1f);
    }

    private void SetRootPosePinnedToAnchor(
    Transform root,
    Transform anchor,
    Vector3 targetAnchorWorldPosition,
    Quaternion targetRootWorldRotation)
    {
        if (root == null)
            return;

        Vector3 targetRootPosition = CalculateRootPositionPinnedToAnchor(
            root,
            anchor,
            targetAnchorWorldPosition,
            targetRootWorldRotation);

        root.SetPositionAndRotation(targetRootPosition, targetRootWorldRotation);

        if (anchor == null || !IsTransformInHierarchy(anchor, root))
            return;

        // Final correction pass: exact-pin the anchor after rotation.
        // This is especially important for poles, where even a tiny handle offset is obvious.
        Vector3 correction = targetAnchorWorldPosition - anchor.position;
        root.position += correction;
    }

    private static Vector3 CalculateRootPositionPinnedToAnchor(
        Transform root,
        Transform anchor,
        Vector3 targetAnchorWorldPosition,
        Quaternion targetRootWorldRotation)
    {
        if (root == null)
            return targetAnchorWorldPosition;

        if (anchor == null || !IsTransformInHierarchy(anchor, root))
            return targetAnchorWorldPosition;

        Vector3 localAnchorOffset = root.InverseTransformPoint(anchor.position);
        return targetAnchorWorldPosition - (targetRootWorldRotation * localAnchorOffset);
    }

    private Transform GetStackSkiVisualRoot(bool left)
    {
        // Prefer the current runtime ski visual from SkiController.
        // This follows gear loadout swaps instead of holding onto the original/default model.
        if (skiController != null)
        {
            Transform runtimeVisual = left
                ? skiController.LeftSkiVisualTransform
                : skiController.RightSkiVisualTransform;

            if (runtimeVisual != null)
                return runtimeVisual;
        }

        // Fallback to the logical ski root only if allowed.
        // This keeps stack visuals functional even if the runtime visual reference is unavailable.
        if (!allowLogicalEquipmentRootFallback)
            return null;

        return left ? _leftSkiTransform : _rightSkiTransform;
    }

    private Transform GetStackSkiBindingAnchor(bool left, Transform skiRoot)
    {
        if (skiRoot == null)
            return null;

        // Prefer the normal foot anchors already used by the limb visual system.
        Transform fallback = left ? leftSkiFootAnchor : rightSkiFootAnchor;
        if (fallback != null && IsTransformInHierarchy(fallback, skiRoot))
            return fallback;

        // Then use the normal ski binding anchors.
        fallback = left ? leftSkiBindingAnchor : rightSkiBindingAnchor;
        if (fallback != null && IsTransformInHierarchy(fallback, skiRoot))
            return fallback;

        // Final fallback: pin the ski root itself.
        return skiRoot;
    }

    private Transform GetStackPoleHandleAnchor(bool left, Transform poleRoot)
    {
        if (poleRoot == null)
            return null;

        Transform fallback = left ? leftPoleGripAnchor : rightPoleGripAnchor;
        if (fallback != null && IsTransformInHierarchy(fallback, poleRoot))
            return fallback;

        fallback = left ? leftSkiHandAnchor : rightSkiHandAnchor;
        if (fallback != null && IsTransformInHierarchy(fallback, poleRoot))
            return fallback;

        return poleRoot;
    }

    private Vector3 GetStackEndpointVelocity(bool left, LimbKind limbKind)
    {
        if (!_stackPhysicsActive)
            return Vector3.zero;

        ref StackLimbEndpointState state = ref GetStackEndpointState(left, limbKind);
        return state.initialized ? state.velocity : Vector3.zero;
    }

    private static bool IsTransformInHierarchy(Transform child, Transform parent)
    {
        if (child == null || parent == null)
            return false;

        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;

            current = current.parent;
        }

        return false;
    }

    private void SetLinePositions(LineRenderer lr, Vector3 a, Vector3 b, Vector3 c)
    {
        if (lr.positionCount == 3 &&
            (lr.GetPosition(0) - a).sqrMagnitude <= LinePositionChangeEpsilonSqr &&
            (lr.GetPosition(1) - b).sqrMagnitude <= LinePositionChangeEpsilonSqr &&
            (lr.GetPosition(2) - c).sqrMagnitude <= LinePositionChangeEpsilonSqr)
        {
            if (!lr.enabled)
                lr.enabled = true;
            return;
        }

        _lineBuffer[0] = a;
        _lineBuffer[1] = b;
        _lineBuffer[2] = c;
        lr.SetPositions(_lineBuffer);
        if (!lr.enabled)
            lr.enabled = true;
    }

    private void RefreshLimbRendererRouting()
    {
        if (!routeLimbRenderersThroughCustomizer || characterCustomizer == null)
            return;

        WearableAttachment jacketAttachment = characterCustomizer.GetCurrentJacketAttachment();
        CustomizationOptionSO jacketOption = characterCustomizer.GetCurrentJacketOption();
        int currentRendererSignature = ComputeRoutingRendererSignature();

        bool routingChanged =
            jacketAttachment != _lastRoutedJacketAttachment ||
            jacketOption != _lastRoutedJacketOption ||
            currentRendererSignature != _lastRoutingRendererSignature;

        if (!routingChanged)
            return;

        ClearAllRendererRouting();

        if (jacketAttachment == null || jacketOption == null)
        {
            RouteLimbVisualGroupToDefault(true, LimbKind.Arm, ref _leftArmBindingMode);
            RouteLimbVisualGroupToDefault(false, LimbKind.Arm, ref _rightArmBindingMode);
            RouteLimbVisualGroupToDefault(true, LimbKind.Leg, ref _leftLegBindingMode);
            RouteLimbVisualGroupToDefault(false, LimbKind.Leg, ref _rightLegBindingMode);
        }
        else
        {
            RouteLimbVisualGroupToJacketOrAttachment(true, LimbKind.Arm, jacketAttachment, jacketOption, ref _leftArmBindingMode);
            RouteLimbVisualGroupToJacketOrAttachment(false, LimbKind.Arm, jacketAttachment, jacketOption, ref _rightArmBindingMode);
            RouteLimbVisualGroupToJacketOrAttachment(true, LimbKind.Leg, jacketAttachment, jacketOption, ref _leftLegBindingMode);
            RouteLimbVisualGroupToJacketOrAttachment(false, LimbKind.Leg, jacketAttachment, jacketOption, ref _rightLegBindingMode);
        }

        _lastRoutedJacketAttachment = jacketAttachment;
        _lastRoutedJacketOption = jacketOption;
        _lastRoutingRendererSignature = currentRendererSignature;
    }

    private void ClearAllRendererRouting()
    {
        UnrouteLimbVisualGroup(true, LimbKind.Arm, ref _leftArmBindingMode);
        UnrouteLimbVisualGroup(false, LimbKind.Arm, ref _rightArmBindingMode);
        UnrouteLimbVisualGroup(true, LimbKind.Leg, ref _leftLegBindingMode);
        UnrouteLimbVisualGroup(false, LimbKind.Leg, ref _rightLegBindingMode);
    }

    private void RouteLimbVisualGroupToSkin(bool left, LimbKind limbKind, ref LimbBindingMode mode)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        if (renderers.Count == 0)
            return;

        for (int i = 0; i < renderers.Count; i++)
            RouteRendererToSkin(renderers[i]);

        mode = LimbBindingMode.Skin;
    }

    private void RouteLimbVisualGroupToDefault(bool left, LimbKind limbKind, ref LimbBindingMode mode)
    {
        RouteLimbVisualGroupToSkin(left, limbKind, ref mode);
    }

    private void RouteLimbVisualGroupToJacketOrAttachment(bool left, LimbKind limbKind, WearableAttachment jacketAttachment, CustomizationOptionSO jacketOption, ref LimbBindingMode mode)
    {
        RouteLimbVisualGroup(left, limbKind, jacketAttachment, jacketOption, ref mode);
    }

    private void RouteLimbVisualGroup(bool left, LimbKind limbKind, WearableAttachment jacketAttachment, CustomizationOptionSO jacketOption, ref LimbBindingMode mode)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        if (renderers.Count == 0 || jacketAttachment == null || jacketOption == null)
            return;

        JacketLimbRouteTarget target = GetLimbRouteTarget(left, limbKind);
        LimbWearableColourSource colourSource = jacketOption.GetLimbColourSource(target);
        string secondaryChannelId = jacketOption.GetResolvedLimbSecondaryChannelId(defaultJacketSecondaryChannelId);

        for (int i = 0; i < renderers.Count; i++)
        {
            RouteRendererToJacket(renderers[i], jacketAttachment, colourSource, secondaryChannelId);
        }

        mode = colourSource == LimbWearableColourSource.Secondary
            ? LimbBindingMode.JacketSecondary
            : LimbBindingMode.JacketPrimary;
    }

    private void UnrouteLimbVisualGroup(bool left, LimbKind limbKind, ref LimbBindingMode mode)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        for (int i = 0; i < renderers.Count; i++)
            UnrouteRenderer(renderers[i]);

        mode = LimbBindingMode.None;
    }

    private List<Renderer> GetRoutedRenderers(bool left, LimbKind limbKind)
    {
        var renderers = new List<Renderer>(5);

        Renderer lineRenderer = GetLineRendererForLimb(left, limbKind);
        if (lineRenderer != null)
            renderers.Add(lineRenderer);

        Transform visualRoot = GetVisualRootForLimb(left, limbKind);
        if (visualRoot == null)
            return renderers;

        var childRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < childRenderers.Length; i++)
        {
            Renderer renderer = childRenderers[i];
            if (renderer == null)
                continue;

            // Gloves / boots now live as wearable attachments under the hand/foot visual hierarchy.
            // Exclude any renderer owned by a nested wearable so limb skin/jacket routing only affects
            // the procedural limb visuals themselves.
            var owningWearable = renderer.GetComponentInParent<WearableAttachment>();
            if (owningWearable != null)
                continue;

            if (!renderers.Contains(renderer))
                renderers.Add(renderer);
        }

        return renderers;
    }

    private int ComputeRoutingRendererSignature()
    {
        int signature = 17;
        signature = UpdateRendererSignature(signature, true, LimbKind.Arm);
        signature = UpdateRendererSignature(signature, false, LimbKind.Arm);
        signature = UpdateRendererSignature(signature, true, LimbKind.Leg);
        signature = UpdateRendererSignature(signature, false, LimbKind.Leg);
        return signature;
    }

    private int UpdateRendererSignature(int signature, bool left, LimbKind limbKind)
    {
        var renderers = GetRoutedRenderers(left, limbKind);
        unchecked
        {
            signature = (signature * 31) + renderers.Count;
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                signature = (signature * 31) + (renderer != null ? renderer.GetInstanceID() : 0);
                if (renderer != null)
                    signature = (signature * 31) + renderer.sharedMaterials.Length;
            }
        }

        return signature;
    }

    private Renderer GetLineRendererForLimb(bool left, LimbKind limbKind)
    {
        if (limbKind == LimbKind.Arm)
            return left ? leftArmLine : rightArmLine;

        return left ? leftLegLine : rightLegLine;
    }

    private Transform GetVisualRootForLimb(bool left, LimbKind limbKind)
    {
        if (limbKind == LimbKind.Arm)
            return left ? leftHandVisual : rightHandVisual;

        return left ? leftFootVisual : rightFootVisual;
    }

    private static JacketLimbRouteTarget GetLimbRouteTarget(bool left, LimbKind limbKind)
    {
        if (limbKind == LimbKind.Arm)
            return left ? JacketLimbRouteTarget.LeftArm : JacketLimbRouteTarget.RightArm;

        return left ? JacketLimbRouteTarget.LeftLeg : JacketLimbRouteTarget.RightLeg;
    }

    private void RouteRendererToSkin(Renderer renderer)
    {
        if (renderer == null || characterCustomizer == null)
            return;

        characterCustomizer.AddSkinRenderer(renderer);
    }

    private static void RouteRendererToJacket(Renderer renderer, WearableAttachment jacketAttachment, LimbWearableColourSource colourSource, string secondaryChannelId)
    {
        if (renderer == null || jacketAttachment == null)
            return;

        if (colourSource == LimbWearableColourSource.Secondary)
            jacketAttachment.AddExtraRenderer(secondaryChannelId, renderer);
        else
            jacketAttachment.AddPrimaryRenderer(renderer);
    }

    private void UnrouteRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (characterCustomizer != null)
            characterCustomizer.RemoveSkinRenderer(renderer);

        WearableAttachment jacketAttachment = _lastRoutedJacketAttachment;
        CustomizationOptionSO jacketOption = _lastRoutedJacketOption;

        if (jacketAttachment != null)
        {
            jacketAttachment.RemovePrimaryRenderer(renderer);

            string secondaryChannelId = jacketOption != null
                ? jacketOption.GetResolvedLimbSecondaryChannelId(defaultJacketSecondaryChannelId)
                : defaultJacketSecondaryChannelId;

            jacketAttachment.RemoveExtraRenderer(secondaryChannelId, renderer);
        }
    }

    private Vector3 ComputeArmJoint(bool left, Vector3 root, Vector3 end, bool walkingMode)
    {
        Vector3 up = GetBodyUp();
        Vector3 forward = GetBodyForward();
        Vector3 right = GetBodyRight();
        Vector3 dir = end - root;

        if (dir.sqrMagnitude <= 0.0001f)
            return root;

        float sideSign = left ? -1f : 1f;

        if (walkingMode)
        {
            bool groundedWalkVisual = walkingController == null || walkingController.IsWalkGrounded;
            float charge = GetJumpCharge01(walkingMode);
            float release = GetJumpRelease01(walkingMode);
            float air = GetWalkAirPose01(walkingMode);
            float airborne = Mathf.Max(release, air);

            float bendAmount = walkArmBend;
            bendAmount *= Mathf.Lerp(1f, runArmBendMultiplier, GetRunPose01(walkingMode));
            bendAmount += jumpSquatArmBendAdd * charge;
            bendAmount -= jumpReleaseArmOpen * release;
            bendAmount += walkAirArmBendAdd * airborne;
            bendAmount = Mathf.Max(0.03f, bendAmount);

            Vector3 bendAxis =
                (-forward * 0.55f) +
                (right * sideSign * 0.45f) +
                (up * (0.10f + 0.55f * airborne));

            if (groundedWalkVisual && _walkMotionBlend > 0.001f && !IsSeatedPoseActive())
            {
                float phase = Mathf.Sin(_walkCycleTime + (left ? 0f : Mathf.PI));
                bendAxis += forward * (phase * 0.35f * _walkMotionBlend);
            }

            if (IsRiderPoseActive())
            {
                bendAmount = Mathf.Max(bendAmount, GetRiderArmBend());
                bendAxis += forward * 0.45f;
                bendAxis += up * -0.2f;

                if (_riderPoseMode == RiderPoseMode.SnowmobilePassenger)
                    bendAxis += forward * 0.2f;
            }
            else if (_seatedPoseOverride || (walkingController != null && walkingController.IsRiderPoseActive))
            {
                bendAmount = Mathf.Max(bendAmount, seatedArmBend);
                bendAxis += forward * 0.45f + up * -0.25f;
            }

            bendAxis = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(bendAxis, dir), right * sideSign);

            Vector3 hint = root + dir * 0.5f + bendAxis * bendAmount;

            // Explicit airborne elbow travel.
            hint += forward * (walkAirElbowForward * airborne);
            hint += up * (walkAirElbowUp * airborne);

            Vector3 planarVelocity = Vector3.ProjectOnPlane(GetWalkVisualVelocity(), up);
            if (planarVelocity.sqrMagnitude > 0.0001f)
            {
                float speed01 = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, walkAirVelocityForFullLag));
                hint -= planarVelocity.normalized * (walkAirVelocityLag * 0.35f * air * speed01);
            }

            return SolveStackJoint(root, end, LimbKind.Arm, left, hint);
        }

        float polePhase01 = GetPolePhaseNormalized();
        float skiBendAmount = armBaseBend;
        skiBendAmount += skiController.Tuck01 * armTuckBend;
        skiBendAmount += (skiController.IsAirborne ? 1f : 0f) * armAirBend;
        skiBendAmount += polePhase01 * armPolePhaseInfluence;

        Vector3 skiBendAxis = (-forward * 0.8f) + (right * sideSign * 0.45f) + (-up * 0.2f);
        skiBendAxis = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(skiBendAxis, dir), right * sideSign);
        return root + dir * 0.5f + skiBendAxis * skiBendAmount;
    }

    private Vector3 ComputeLegJoint(bool left, Vector3 root, Vector3 end, bool walkingMode)
    {
        Vector3 forward = GetBodyForward();
        Vector3 right = GetBodyRight();
        Vector3 up = GetBodyUp();
        Vector3 dir = end - root;

        if (dir.sqrMagnitude <= 0.0001f)
            return root;

        float sideSign = left ? -1f : 1f;
        Vector3 movement = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(GetVelocity(), up), forward);

        if (walkingMode)
        {
            bool groundedWalkVisual = walkingController == null || walkingController.IsWalkGrounded;
            float charge = GetJumpCharge01(walkingMode);
            float release = GetJumpRelease01(walkingMode);
            float air = GetWalkAirPose01(walkingMode);
            float airborne = Mathf.Max(release, air);

            float bendAmount = walkLegBend;
            bendAmount *= Mathf.Lerp(1f, runLegBendMultiplier, GetRunPose01(walkingMode));

            bendAmount += jumpSquatLegBendAdd * charge;
            bendAmount -= jumpReleaseLegStraighten * release;
            bendAmount += walkAirLegBendAdd * airborne;

            Vector3 bendAxis =
                forward +
                (movement * movementForwardBias) +
                (right * sideSign * (0.18f + limbOutwardBias)) +
                (up * (0.06f + 0.28f * airborne));

            if (groundedWalkVisual && _walkMotionBlend > 0.001f && !IsSeatedPoseActive())
            {
                float phase = Mathf.Sin(_walkCycleTime + (left ? 0f : Mathf.PI));
                float absPhase = Mathf.Abs(phase);

                bendAmount += absPhase * 0.04f * _walkMotionBlend;
                bendAxis += forward * (absPhase * 0.35f * _walkMotionBlend);
                bendAxis += up * (-absPhase * 0.10f * _walkMotionBlend);
            }

            if (IsRiderPoseActive())
            {
                bendAmount = Mathf.Max(bendAmount, GetRiderLegBend());
                bendAxis += forward * 0.75f;
                bendAxis += up * -0.35f;

                if (_riderPoseMode == RiderPoseMode.SnowmobileDriver)
                    bendAxis += forward * 0.15f;
            }
            else if (_seatedPoseOverride || (walkingController != null && walkingController.IsRiderPoseActive))
            {
                bendAmount = Mathf.Max(bendAmount, seatedLegBend);
                bendAxis += forward * 0.75f + up * -0.35f;
            }

            bendAmount = Mathf.Max(0.03f, bendAmount);
            bendAxis = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(bendAxis, dir), forward);

            Vector3 hint = root + dir * 0.5f + bendAxis * bendAmount;

            // Explicit airborne knee travel. This creates the "knees up and forward" read.
            hint += forward * (walkAirKneeForward * airborne);
            hint += up * (walkAirKneeUp * airborne);

            Vector3 planarVelocity = Vector3.ProjectOnPlane(GetWalkVisualVelocity(), up);
            if (planarVelocity.sqrMagnitude > 0.0001f)
            {
                float speed01 = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, walkAirVelocityForFullLag));
                hint -= planarVelocity.normalized * (walkAirVelocityLag * 0.25f * air * speed01);
            }

            return SolveStackJoint(root, end, LimbKind.Leg, left, hint);
        }

        float legInput = left ? skiController.LeftLegInput : skiController.RightLegInput;
        float skiBendAmount = legBaseBend;
        skiBendAmount += skiController.Tuck01 * legTuckBend;
        skiBendAmount += legInput * legInputBend;
        skiBendAmount -= (skiController.IsAirborne ? 1f : 0f) * legAirStraighten;
        skiBendAmount = Mathf.Max(0.03f, skiBendAmount);

        Vector3 skiBendAxis = forward + (movement * movementForwardBias) + (right * sideSign * (0.18f + limbOutwardBias));
        skiBendAxis = SafeNormalizeOrFallback(Vector3.ProjectOnPlane(skiBendAxis, dir), forward);
        return root + dir * 0.5f + skiBendAxis * skiBendAmount;
    }

    private Vector3 ResolveJointWorldPosition(SkierLimbJoint joint, Vector3 root, Vector3 end, bool walkingMode)
    {
        if (!TryGetResolvedJointControlLocalPose(joint, out Vector3 localPosition, out _, out bool hasAuthoredOverride))
            return GetProceduralJointWorldPosition(joint, root, end, walkingMode);

        Transform body = GetBodyTransform();
        if (body == null)
            return GetProceduralJointWorldPosition(joint, root, end, walkingMode);

        Vector3 controlWorldPosition = body.TransformPoint(localPosition);
        return hasAuthoredOverride
            ? controlWorldPosition
            : GetProceduralJointWorldPosition(joint, root, end, walkingMode);
    }

    private bool TryGetResolvedJointControlLocalPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation)
    {
        return TryGetResolvedJointControlLocalPose(joint, out localPosition, out localRotation, out _);
    }

    private bool TryGetResolvedJointControlLocalPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation, out bool hasAuthoredOverride)
    {
        if (!TryBuildProceduralJointControlLocalPose(joint, out Vector3 proceduralLocalPosition, out Quaternion proceduralLocalRotation))
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            hasAuthoredOverride = false;
            return false;
        }

        localPosition = proceduralLocalPosition;
        localRotation = proceduralLocalRotation;
        hasAuthoredOverride = false;

        TrickPoseRigSnapshot.PartState previewState = GetPreviewJointState(joint);
        if (previewState.hasValue)
        {
            float t = Mathf.Clamp01(_previewJointWeight);
            localPosition = Vector3.Lerp(proceduralLocalPosition, previewState.localPosition, t);
            localRotation = Quaternion.identity;
            hasAuthoredOverride = true;
            return true;
        }

        PosePartTransformData runtimePose = GetRuntimeJointPose(joint);
        if (runtimePose != null && runtimePose.enabled)
        {
            float t = Mathf.Clamp01(_runtimeJointPoseWeight * Mathf.Clamp01(runtimePose.weight));
            localPosition = Vector3.Lerp(proceduralLocalPosition, runtimePose.localPosition, t);
            localRotation = Quaternion.identity;
            hasAuthoredOverride = true;
        }

        return true;
    }

    private bool TryBuildProceduralJointControlLocalPose(SkierLimbJoint joint, out Vector3 localPosition, out Quaternion localRotation)
    {
        Transform body = GetBodyTransform();
        if (body == null)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            return false;
        }

        bool walkingMode = walkingController != null && walkingController.IsWalkingMode;
        bool left = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.LeftKnee;
        bool arm = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.RightElbow;
        Vector3 root = arm ? GetShoulderPosition(left) : GetHipPosition(left);
        Vector3 end = GetLimbEndpointWorldPosition(left, walkingMode, arm ? LimbKind.Arm : LimbKind.Leg);
        Vector3 jointWorld = arm
            ? ComputeArmJoint(left, root, end, walkingMode)
            : ComputeLegJoint(left, root, end, walkingMode);
        Vector3 segment = end - root;
        Vector3 axis = Vector3.ProjectOnPlane(jointWorld - (root + segment * 0.5f), segment.normalized);
        if (axis.sqrMagnitude < 0.0001f)
            axis = arm ? GetBodyRight() * (left ? -1f : 1f) : GetBodyForward();

        localPosition = body.InverseTransformPoint(jointWorld);
        localRotation = Quaternion.identity;
        return true;
    }

    private Vector3 GetProceduralJointWorldPosition(SkierLimbJoint joint, Vector3 root, Vector3 end, bool walkingMode)
    {
        return joint switch
        {
            SkierLimbJoint.LeftElbow => ComputeArmJoint(true, root, end, walkingMode),
            SkierLimbJoint.RightElbow => ComputeArmJoint(false, root, end, walkingMode),
            SkierLimbJoint.LeftKnee => ComputeLegJoint(true, root, end, walkingMode),
            SkierLimbJoint.RightKnee => ComputeLegJoint(false, root, end, walkingMode),
            _ => root + (end - root) * 0.5f
        };
    }

    private Vector3 BuildJointWorldPositionFromControl(SkierLimbJoint joint, Vector3 root, Vector3 end, Vector3 controlWorldPosition, bool walkingMode)
    {
        Vector3 segment = end - root;
        float segmentLength = Mathf.Max(0.0001f, segment.magnitude);
        Vector3 segmentDir = segment / segmentLength;
        float along = Mathf.Clamp(Vector3.Dot(controlWorldPosition - root, segmentDir), 0f, segmentLength);
        Vector3 onSegment = root + segmentDir * along;

        Vector3 bendAxis = Vector3.ProjectOnPlane(controlWorldPosition - onSegment, segmentDir);
        if (bendAxis.sqrMagnitude < 0.0001f)
            bendAxis = Vector3.ProjectOnPlane(GetProceduralJointWorldPosition(joint, root, end, walkingMode) - onSegment, segmentDir);

        bool left = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.LeftKnee;
        Vector3 fallbackAxis = joint == SkierLimbJoint.LeftElbow || joint == SkierLimbJoint.RightElbow
            ? GetBodyRight() * (left ? -1f : 1f)
            : GetBodyForward();
        float bendDistance = Vector3.ProjectOnPlane(controlWorldPosition - onSegment, segmentDir).magnitude;
        if (bendDistance <= 0.0001f)
            return onSegment;

        bendAxis = SafeNormalizeOrFallback(bendAxis, fallbackAxis);
        return onSegment + bendAxis * bendDistance;
    }

    private TrickPoseRigSnapshot.PartState GetPreviewJointState(SkierLimbJoint joint)
    {
        if (_previewJointSnapshot == null)
            return default;

        return joint switch
        {
            SkierLimbJoint.LeftElbow => _previewJointSnapshot.leftElbow,
            SkierLimbJoint.RightElbow => _previewJointSnapshot.rightElbow,
            SkierLimbJoint.LeftKnee => _previewJointSnapshot.leftKnee,
            SkierLimbJoint.RightKnee => _previewJointSnapshot.rightKnee,
            _ => default
        };
    }

    private PosePartTransformData GetRuntimeJointPose(SkierLimbJoint joint)
    {
        if (_runtimeJointPoseEntry == null)
            return null;

        return joint switch
        {
            SkierLimbJoint.LeftElbow => _runtimeJointPoseEntry.leftElbowPose,
            SkierLimbJoint.RightElbow => _runtimeJointPoseEntry.rightElbowPose,
            SkierLimbJoint.LeftKnee => _runtimeJointPoseEntry.leftKneePose,
            SkierLimbJoint.RightKnee => _runtimeJointPoseEntry.rightKneePose,
            _ => null
        };
    }

    private Vector3 GetShoulderPosition(bool left)
    {
        Transform explicitAnchor = left ? leftShoulderAnchor : rightShoulderAnchor;
        if (explicitAnchor != null)
            return explicitAnchor.position;

        Transform body = GetBodyTransform();
        Vector3 fallback = left ? leftShoulderLocalOffset : rightShoulderLocalOffset;
        return body.TransformPoint(fallback);
    }

    private Vector3 GetHipPosition(bool left)
    {
        Transform explicitAnchor = left ? leftHipAnchor : rightHipAnchor;
        if (explicitAnchor != null)
            return explicitAnchor.position;

        Transform body = GetBodyTransform();
        Vector3 fallback = left ? leftHipLocalOffset : rightHipLocalOffset;
        return body.TransformPoint(fallback);
    }

    private void CacheWalkAnchorBaseLocals()
    {
        CacheWalkAnchorBaseLocal(leftWalkFootTarget, ref _cachedLeftWalkFootAnchor, ref _leftWalkFootBaseLocalPosition);
        CacheWalkAnchorBaseLocal(rightWalkFootTarget, ref _cachedRightWalkFootAnchor, ref _rightWalkFootBaseLocalPosition);
        CacheWalkAnchorBaseLocal(leftWalkHandTarget, ref _cachedLeftWalkHandAnchor, ref _leftWalkHandBaseLocalPosition);
        CacheWalkAnchorBaseLocal(rightWalkHandTarget, ref _cachedRightWalkHandAnchor, ref _rightWalkHandBaseLocalPosition);
        _walkAnchorBaseLocalsInitialized = true;
    }

    private void ResetWalkAnchorBaseCache()
    {
        _walkAnchorBaseLocalsInitialized = false;
        _cachedLeftWalkFootAnchor = null;
        _cachedRightWalkFootAnchor = null;
        _cachedLeftWalkHandAnchor = null;
        _cachedRightWalkHandAnchor = null;
    }

    private void CacheWalkAnchorBaseLocal(Transform anchor, ref Transform cachedAnchor, ref Vector3 cachedLocalPosition)
    {
        if (anchor != null && cachedAnchor != anchor)
        {
            cachedLocalPosition = anchor.localPosition;
            cachedAnchor = anchor;
        }
        else if (anchor == null)
        {
            cachedAnchor = null;
        }
    }

    private void AnimateWalkAnchors(bool walkingMode, float dt)
    {
        if (!_walkAnchorBaseLocalsInitialized)
            return;

        UpdateWalkFootAnchor(leftWalkFootTarget, _leftWalkFootBaseLocalPosition, true, walkingMode, dt);
        UpdateWalkFootAnchor(rightWalkFootTarget, _rightWalkFootBaseLocalPosition, false, walkingMode, dt);
        UpdateWalkHandAnchor(leftWalkHandTarget, _leftWalkHandBaseLocalPosition, true, walkingMode, dt);
        UpdateWalkHandAnchor(rightWalkHandTarget, _rightWalkHandBaseLocalPosition, false, walkingMode, dt);
    }

    private void UpdateWalkFootAnchor(Transform anchor, Vector3 baseLocalPosition, bool left, bool walkingMode, float dt)
    {
        if (anchor == null)
            return;

        Vector3 targetLocalPosition = baseLocalPosition;

        Vector3 localForward = GetAnchorParentLocalDirection(anchor, GetBodyForward(), Vector3.forward);
        Vector3 localUp = GetAnchorParentLocalDirection(anchor, GetBodyUp(), Vector3.up);

        bool groundedWalkVisual =
            walkingMode &&
            walkingController != null &&
            walkingController.IsWalkGrounded &&
            !IsSeatedPoseActive();

        if (groundedWalkVisual && _walkMotionBlend > 0.001f)
        {
            float run01 = GetRunPose01(walkingMode);
            float strideMul = Mathf.Lerp(1f, runStrideMultiplier, run01);

            float phase = _walkCycleTime + (left ? 0f : Mathf.PI);
            float stride = Mathf.Sin(phase);
            float lift = Mathf.Max(0f, Mathf.Sin(phase));

            targetLocalPosition += localForward * (stride * walkFootForwardStride * strideMul * _walkMotionBlend);
            targetLocalPosition += localUp * (lift * walkFootLift * strideMul * _walkMotionBlend);
        }

        if (walkingMode && !IsSeatedPoseActive())
        {
            float release01 = GetJumpRelease01(walkingMode);
            float air01 = GetWalkAirPose01(walkingMode);
            float airborne01 = Mathf.Max(release01, air01);

            // Takeoff / airborne: feet come up and forward instead of continuing a walk stride.
            targetLocalPosition += localForward * (walkAirFootForward * airborne01);
            targetLocalPosition += localUp * (walkAirFootUp * airborne01);

            // Subtle trailing behind horizontal air velocity.
            targetLocalPosition += GetWalkAirVelocityLagLocalOffset(anchor, walkAirVelocityLag, walkingMode);
        }

        targetLocalPosition = ApplyWalkAnchorPoseOffset(anchor, targetLocalPosition, left, LimbKind.Leg, walkingMode);

        float activePose01 = Mathf.Max(
            _walkMotionBlend,
            Mathf.Max(GetJumpCharge01(walkingMode), Mathf.Max(GetJumpRelease01(walkingMode), GetWalkAirPose01(walkingMode))));

        float followSpeed = walkingMode && (activePose01 > 0.001f || IsSeatedPoseActive())
            ? walkCycleSpeed
            : walkIdleReturnSpeed;

        float t = 1f - Mathf.Exp(-followSpeed * dt);
        anchor.localPosition = Vector3.Lerp(anchor.localPosition, targetLocalPosition, t);
    }

    private void UpdateWalkHandAnchor(Transform anchor, Vector3 baseLocalPosition, bool left, bool walkingMode, float dt)
    {
        if (anchor == null)
            return;

        if (IsRiderPoseActive())
        {
            Transform externalHandTarget = GetRiderHandTarget(left);

            if (externalHandTarget != null)
            {
                Vector3 externalTargetLocalPosition = anchor.parent != null
                    ? anchor.parent.InverseTransformPoint(externalHandTarget.position)
                    : externalHandTarget.position;

                float targetFollowSpeed = Mathf.Max(0.01f, riderHandTargetFollowSpeed);
                float targetT = 1f - Mathf.Exp(-targetFollowSpeed * dt);
                anchor.localPosition = Vector3.Lerp(anchor.localPosition, externalTargetLocalPosition, targetT);
                return;
            }
        }

        Vector3 targetLocalPosition = baseLocalPosition;

        Vector3 localForward = GetAnchorParentLocalDirection(anchor, GetBodyForward(), Vector3.forward);
        Vector3 localUp = GetAnchorParentLocalDirection(anchor, GetBodyUp(), Vector3.up);
        Vector3 localRight = GetAnchorParentLocalDirection(anchor, GetBodyRight(), Vector3.right);
        float sideSign = left ? -1f : 1f;

        bool groundedWalkVisual =
            walkingMode &&
            walkingController != null &&
            walkingController.IsWalkGrounded &&
            !IsSeatedPoseActive();

        if (groundedWalkVisual && _walkMotionBlend > 0.001f)
        {
            float run01 = GetRunPose01(walkingMode);
            float strideMul = Mathf.Lerp(1f, runStrideMultiplier, run01);

            float phase = _walkCycleTime + (left ? Mathf.PI : 0f);
            float stride = Mathf.Sin(phase);
            float lift = Mathf.Max(0f, Mathf.Sin(phase));

            targetLocalPosition += localForward * (stride * walkHandForwardStride * strideMul * _walkMotionBlend);
            targetLocalPosition += localUp * (lift * walkHandLift * strideMul * _walkMotionBlend);
        }

        if (walkingMode && !IsSeatedPoseActive())
        {
            float release01 = GetJumpRelease01(walkingMode);
            float air01 = GetWalkAirPose01(walkingMode);
            float airborne01 = Mathf.Max(release01, air01);

            // Arms lift and open slightly in air. This is deliberately smaller than stack ragdoll.
            targetLocalPosition += localUp * (walkAirHandUp * airborne01);
            targetLocalPosition += localRight * (sideSign * walkAirHandOutward * airborne01);
            targetLocalPosition += localForward * (walkAirFootForward * 0.20f * airborne01);

            targetLocalPosition += GetWalkAirVelocityLagLocalOffset(anchor, walkAirVelocityLag * 1.15f, walkingMode);
        }

        targetLocalPosition = ApplyWalkAnchorPoseOffset(anchor, targetLocalPosition, left, LimbKind.Arm, walkingMode);

        float activePose01 = Mathf.Max(
            _walkMotionBlend,
            Mathf.Max(GetJumpCharge01(walkingMode), Mathf.Max(GetJumpRelease01(walkingMode), GetWalkAirPose01(walkingMode))));

        float followSpeed = walkingMode && (activePose01 > 0.001f || IsSeatedPoseActive())
            ? walkCycleSpeed
            : walkIdleReturnSpeed;

        float t = 1f - Mathf.Exp(-followSpeed * dt);
        anchor.localPosition = Vector3.Lerp(anchor.localPosition, targetLocalPosition, t);
    }

    private Vector3 GetLimbEndpointWorldPosition(bool left, bool walkingMode, LimbKind limbKind)
    {
        Transform anchor = GetActiveLimbAnchor(left, walkingMode, limbKind);
        if (anchor != null)
            return anchor.position;

        return GetFallbackLimbEndpointWorldPosition(left, limbKind);
    }

    private Vector3 GetFallbackLimbEndpointWorldPosition(bool left, LimbKind limbKind)
    {
        Vector3 right = GetBodyRight();
        Vector3 up = GetBodyUp();

        if (limbKind == LimbKind.Leg)
        {
            Transform body = GetBodyTransform();
            return body.position + right * (left ? -0.12f : 0.12f) + up * -0.85f;
        }

        Transform head = _headTransform != null ? _headTransform : GetBodyTransform();
        return head.position + right * (left ? -0.18f : 0.18f) + up * -0.10f;
    }

    private Vector3 GetAnchorParentLocalDirection(Transform anchor, Vector3 worldDirection, Vector3 fallbackLocalDirection)
    {
        Transform parent = anchor != null ? anchor.parent : null;
        if (parent == null)
            return fallbackLocalDirection;

        Vector3 localDirection = parent.InverseTransformDirection(worldDirection);
        return SafeNormalizeOrFallback(localDirection, fallbackLocalDirection);
    }

    private Transform GetBodyTransform()
    {
        if (_bodyTransform == null)
            _bodyTransform = skiController != null && skiController.BodyPoseTransform != null ? skiController.BodyPoseTransform : transform;
        return _bodyTransform;
    }

    private Vector3 GetGroundNormal()
    {
        if (skiController == null)
            return Vector3.up;

        Vector3 groundNormal = skiController.GroundNormal;
        return groundNormal.sqrMagnitude > 0.0001f ? groundNormal.normalized : Vector3.up;
    }

    private Vector3 GetBodyUp()
    {
        if (skiController != null && !skiController.IsAirborne)
            return GetGroundNormal();

        Transform body = GetBodyTransform();
        return body != null ? body.up : transform.up;
    }

    private Vector3 GetBodyForward()
    {
        Transform body = GetBodyTransform();
        Vector3 up = GetBodyUp();
        Vector3 forward = body != null ? body.forward : transform.forward;
        forward = Vector3.ProjectOnPlane(forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            Vector3 velocityPlanar = Vector3.ProjectOnPlane(GetVelocity(), up);
            if (velocityPlanar.sqrMagnitude > 0.0001f)
                forward = velocityPlanar;
            else
                forward = Vector3.ProjectOnPlane(transform.forward, up);
        }

        return forward.normalized;
    }

    private Vector3 GetBodyRight()
    {
        Vector3 up = GetBodyUp();
        Vector3 forward = GetBodyForward();
        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = transform.right;
        return right.normalized;
    }

    private Vector3 GetVelocity()
    {
        if (Application.isPlaying)
        {
            if (skiController != null)
            {
                try
                {
                    return skiController.Velocity;
                }
                catch
                {
                    // Fall through to other velocity sources.
                }
            }

            if (targetRigidbody != null)
                return targetRigidbody.linearVelocity;
        }
        else
        {
            if (targetRigidbody != null)
            {
                try
                {
                    return targetRigidbody.linearVelocity;
                }
                catch
                {
                    // Ignore and fall back to transform delta.
                }
            }
        }

        Vector3 current = transform.position;
        if (!_haveLastRootPosition)
        {
            _lastRootPosition = current;
            _haveLastRootPosition = true;
            return Vector3.zero;
        }

        float dt = GetVisualDeltaTime();
        if (dt <= 0f)
            return Vector3.zero;

        Vector3 velocity = (current - _lastRootPosition) / dt;
        _lastRootPosition = current;
        return velocity;
    }

    private float GetPolePhaseNormalized()
    {
        if (skiController == null)
            return 0f;

        switch (skiController.CurrentPolePhase)
        {
            case SkiController.PoleStrokePhase.Entry:
                return 0.33f;
            case SkiController.PoleStrokePhase.Drag:
                return 0.66f;
            case SkiController.PoleStrokePhase.FollowThrough:
                return 1f;
            default:
                return 0f;
        }
    }

    private static Vector3 SafeNormalizeOrFallback(Vector3 vector, Vector3 fallback)
    {
        if (vector.sqrMagnitude > 0.0001f)
            return vector.normalized;
        if (fallback.sqrMagnitude > 0.0001f)
            return fallback.normalized;
        return Vector3.right;
    }

    private float GetVisualDeltaTime()
    {
        if (Application.isPlaying)
            return Time.deltaTime;

#if UNITY_EDITOR
        return UnityEditor.EditorApplication.isPaused ? 0f : (1f / 60f);
#else
    return 0f;
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        AutoBind();

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(GetShoulderPosition(true), 0.025f);
        Gizmos.DrawSphere(GetShoulderPosition(false), 0.025f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(GetHipPosition(true), 0.025f);
        Gizmos.DrawSphere(GetHipPosition(false), 0.025f);
    }
}
