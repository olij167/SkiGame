using System.Collections.Generic;
using UnityEngine;
using SkiGame.Runs;

[DisallowMultipleComponent]
public class NpcSkierBrain : MonoBehaviour
{
    public enum SpawnContextHint
    {
        None,
        Run,
        LiftBottom,
        LiftTop
    }

    private enum BrainState
    {
        Uninitialized,
        Waiting,
        SkiingAmbient,
        SkiingRun,
        WalkingSupport,
        BoardingLift,
        RidingLift,
        Recovering
    }

    private enum IntentKind
    {
        None,
        LiftThenRun,
        DirectRun,
        AmbientCruise,
        HubLoiter
    }

    private enum LiftNpcBoardingPhase
    {
        None,
        MovingToStage,
        MovingToQueueEntry,
        JoiningQueue,
        WaitingInQueue,
        BoardingCarrier,
        Riding,
        Finished
    }

    [Header("References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private LiftRider liftRider;
    [SerializeField] private NpcSkierProfile profile;
    [SerializeField] private NpcSkierRunFollower locomotion;
    [SerializeField] private NpcSkierAppearanceGenerator appearanceGenerator;
    [SerializeField] private NpcAstarPathAgent pathAgent;
    [SerializeField] private SorenessMeter sorenessMeter;
    [SerializeField] private Transform playerFocus;
    [SerializeField] private Rigidbody body;

    [Header("Scene Sources")]
    [SerializeField] private bool autoFindRunsInScene = true;
    [SerializeField] private bool autoFindLiftsInScene = true;
    [SerializeField] private List<SkiRunLine> availableRuns = new();
    [SerializeField] private List<LiftLine> availableLifts = new();

    [Header("Lifecycle")]
    [SerializeField] private bool randomizeProfileOnStart = true;
    [SerializeField] private bool randomizeAppearanceOnStart = true;

    [Header("Intent")]
    [SerializeField] private float waitMin = 1f;
    [SerializeField] private float waitMax = 3f;
    [SerializeField] private float broadLiftApproachRadius = 40f;
    [SerializeField] private float directRunJoinRadius = 26f;
    [SerializeField] private float walkingSupportMaxDistance = 25f;
    [SerializeField] private float walkArrivalDistance = 2.25f;
    [SerializeField] private float liftBoardDistance = 5f;
    [SerializeField] private float liftDetachDistance = 6f;
    [SerializeField] private float boardingTimeout = 12f;
    [SerializeField] private float respawnCooldown = 1.25f;

    [Header("Generic Activity Lifecycle")]
    [SerializeField] private bool useGenericActivityLifecycle = true;
    [SerializeField] private Vector2 skiRunIntentDurationRange = new Vector2(35f, 160f);
    [SerializeField] private Vector2 liftIntentDurationRange = new Vector2(45f, 220f);
    [SerializeField] private Vector2 traverseIntentDurationRange = new Vector2(18f, 70f);
    [SerializeField] private float genericIntentRepeatPenalty = 0.35f;
    [SerializeField] private float genericAnchorRepeatPenalty = 0.15f;
    [SerializeField] private float socialConversationReactionMin = 2f;
    [SerializeField] private float socialConversationReactionMax = 8f;
    [SerializeField] private float flowThroughMovementBiasAfterLoiter = 0.35f;

    [Header("Hub Loiter")]
    [SerializeField] private float hubDetectRadius = 65f;
    [SerializeField] private float hubLoiterChance = 0.6f;
    [SerializeField] private float hubWanderRadius = 10f;
    [SerializeField] private float hubPauseMin = 1.5f;
    [SerializeField] private float hubPauseMax = 4f;
    [SerializeField] private float hubShuffleMinDelay = 2.5f;
    [SerializeField] private float hubShuffleMaxDelay = 6.5f;
    [SerializeField] private float hubMinShuffleDistance = 2.25f;
    [SerializeField] private float lookAroundIntervalMin = 1.2f;
    [SerializeField] private float lookAroundIntervalMax = 3.4f;

    [Header("Spectator Crowd")]
    [SerializeField] private float spectatorShuffleRadius = 1.75f;
    [SerializeField] private float spectatorShuffleMinDelay = 1.25f;
    [SerializeField] private float spectatorShuffleMaxDelay = 3.25f;
    [SerializeField] private float spectatorIdleLookMinDelay = 0.9f;
    [SerializeField] private float spectatorIdleLookMaxDelay = 2.2f;
    [SerializeField] private float spectatorWalkStrength = 0.55f;
    [SerializeField] private float spectatorCelebrateHopHeight = 0.22f;
    [SerializeField] private float spectatorCelebrateHopFrequency = 2.2f;
    [SerializeField] private float spectatorCelebrateSwayDegrees = 12f;

    [Header("Lift Boarding")]
    [SerializeField] private float liftStageDistance = 3.5f;
    [SerializeField] private float liftStageArrivalDistance = 2.2f;
    [SerializeField] private float liftBoardCommitDistance = 2.2f;
    [SerializeField] private float liftRetryOffsetRadius = 4f;
    [SerializeField] private float liftQueueTriggerSlack = 1.5f;
    [SerializeField] private int maxBoardingRetries = 2;

    [Header("Lift Commitment")]
    [SerializeField] private float minimumLiftVerticalGain = 20f;
    [SerializeField] private float maxLiftTopToRunTargetDistance = 120f;
    [SerializeField] private float abandonLiftDistance = 45f;
    [SerializeField] private float postLiftRunJoinGraceSeconds = 8f;

    [Header("Recovery Restraint")]
    [SerializeField] private float disturbedRespawnSuppressSeconds = 7f;
    [SerializeField] private float preferredWalkingRecoverySeconds = 5f;
    [SerializeField] private float hardRespawnRetryDelay = 3.5f;
    [SerializeField] private float hardRespawnOnlyBeyondPlayerDistance = 550f;
    [SerializeField] private float localFailureRagdollSeverity = 0.18f;
    [SerializeField] private float localFailureRecoveryDelay = 3f;
    [SerializeField] private float softWalkFailurePauseMin = 0.9f;
    [SerializeField] private float softWalkFailurePauseMax = 2f;
    [SerializeField] private LayerMask npcGroundSnapLayers = 0;
    [SerializeField] private float skiModeStrictAirborneGrace = 1.1f;
    [SerializeField] private float skiModeFloatingGroundGap = 3f;

    [Header("Walk Stability")]
    [SerializeField] private float walkUprightLerpSpeed = 10f;
    [SerializeField] private float walkSnapTiltDegrees = 22f;
    [SerializeField] private float spectatorKeepSkisMaxSlopeDeg = 9f;

    [Header("Run Planning")]
    [SerializeField] private int recentRunMemory = 3;
    [SerializeField] private float preferredDifficultyScore = 2.4f;
    [SerializeField] private float travelDistanceScore = 1.2f;
    [SerializeField] private float liftAccessScore = 1f;
    [SerializeField] private float repeatPenaltyScore = 1.4f;
    [SerializeField] private float varietyScore = 0.7f;
    [SerializeField] private float elevationDropScore = 0.65f;
    [SerializeField] private float scenicLengthScore = 0.5f;

    private BrainState _state = BrainState.Uninitialized;
    private IntentKind _intentKind = IntentKind.None;
    private NpcGenericActivityIntent _currentGenericIntent = NpcGenericActivityIntent.None;
    private NpcGenericActivityIntent _previousGenericIntent = NpcGenericActivityIntent.None;

    private float _stateUntil;
    private float _currentIntentStartedAt;
    private float _currentIntentMinDuration;
    private float _currentIntentMaxDuration;
    private float _currentIntentExpiresAt;
    private Vector3 _currentIntentTargetPosition;
    private UnityEngine.Object _currentIntentTargetObject;
    private NpcSocialAnchor _currentIntentTargetAnchor;
    private NpcSocialAnchor _previousAnchor;
    private float _previousAnchorCooldownUntil;
    private string _lastDecisionReason = "Uninitialized";
    private bool _suppressNextIntentRepeatPenalty;
    private float _boardingAbortTime;
    private float _nextLiftInputTime;
    private float _nextRespawnAllowedTime;
    private float _postLiftRunJoinUntil;
    private float _respawnSuppressedUntil;
    private float _walkingRecoveryUntil;
    private float _nextLookAroundTime;
    private float _nextHubShuffleTime;
    private float _skiModeStrictAirborneSince = -1f;
    private readonly RaycastHit[] _groundSnapHits = new RaycastHit[16];
    private static int _npcLayer = -2;
    private static int _playerLayer = -2;

    private SkiRunLine _currentRun;
    private LiftLine _currentLift;
    private Vector3 _currentBroadTarget;
    private Vector3 _currentLiftApproachPoint;
    private Vector3 _currentLiftStagePoint;
    private LiftBoardGate _preferredBoardGate;
    private LiftNpcBoardingPhase _liftBoardingPhase = LiftNpcBoardingPhase.None;
    private string _lastBoardingFailureReason = "none";
    private SkiRunLine _lastRun;
    private readonly List<SkiRunLine> _recentRuns = new();
    private readonly List<Transform> _hubAnchors = new();
    private Transform _currentHubAnchor;
    private bool _liftStageReached;
    private int _boardingRetryCount;
    private bool _initialized;
    private bool _spectatorCrowdActive;
    private bool _socialLoiterActive;
    private float _socialLoiterUntil;
    private NpcSocialAnchor _socialLoiterAnchor;
    private NpcSocialGroupSO _socialLoiterGroup;
    private Vector3 _spectatorAnchorPosition;
    private Quaternion _spectatorAnchorRotation;
    private Vector3 _spectatorForward;
    private Vector3 _spectatorMoveTarget;
    private float _nextSpectatorShuffleTime;
    private float _nextSpectatorLookTime;
    private float _spectatorCrowdPhase;

    private float _visibleFastForwardMultiplier = 1f;
    private Renderer[] _cachedRenderers;

    private SpawnContextHint _pendingSpawnHint = SpawnContextHint.None;
    private Vector3 _pendingHintWorldPoint;
    private readonly List<LiftBoardGate> _liftGateBuffer = new();
    private readonly List<GenericIntentCandidate> _genericIntentCandidates = new();
    private readonly List<NpcSocialAnchor> _genericAnchorCandidates = new();
    private float _lastPoolSleepTime = -999f;
    private Vector3 _lastPoolSleepPosition;

    private bool _genericBehaviourPausedByDefinedCharacter;

    public void PauseGenericBehaviourForDefinedCharacter()
    {
        _genericBehaviourPausedByDefinedCharacter = true;

        pathAgent?.Stop();
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();
        walkingController?.ClearExternalMove();

        _currentRun = null;
        _currentLift = null;
        _preferredBoardGate = null;
        _liftBoardingPhase = LiftNpcBoardingPhase.None;
        _lastBoardingFailureReason = "Defined character paused generic behaviour";
        _intentKind = IntentKind.None;
        ClearGenericIntent("Defined character paused generic behaviour", rememberPrevious: false);
        _state = BrainState.Waiting;
    }

    public float LastPoolSleepTime => _lastPoolSleepTime;
    public Vector3 LastPoolSleepPosition => _lastPoolSleepPosition;
    public bool IsSpectatorCrowdActive => _spectatorCrowdActive;
    public bool IsSocialLoitering => _socialLoiterActive;
    public bool IsGenericIntentActive => _currentGenericIntent != NpcGenericActivityIntent.None;
    public NpcGenericActivityIntent CurrentGenericIntent => _currentGenericIntent;
    public NpcGenericActivityIntent PreviousGenericIntent => _previousGenericIntent;
    public NpcSocialAnchor PreviousGenericIntentTargetAnchor => _previousAnchor;
    public float PreviousGenericIntentAnchorCooldownUntil => _previousAnchorCooldownUntil;

    private struct GenericIntentCandidate
    {
        public NpcGenericActivityIntent intent;
        public float weight;
        public NpcSocialAnchor anchor;
        public UnityEngine.Object targetObject;
        public Vector3 targetPosition;
        public string reason;
    }

    public bool CanBeBorrowedForSocialPopulation()
    {
        if (TryGetComponent(out NpcDefinedCharacterBehaviour definedBehaviour))
            return definedBehaviour.AllowPopulationBorrowing;

        if (TryGetComponent(out NpcIdentity identity) && identity.IsAuthored)
            return false;

        return true;
    }

    public bool CanBeginSocialLoiter()
    {
        if (TryGetComponent(out NpcDefinedCharacterBehaviour definedBehaviour))
            return definedBehaviour.AllowSocialLoiter;

        return true;
    }

    [ContextMenu("Print NPC Movement Debug State")]
    public void PrintNpcMovementDebugState()
    {
        CacheRefs();

        Vector3 velocity = body != null ? body.linearVelocity : (skiController != null ? skiController.Velocity : Vector3.zero);
        Vector3 groundNormal = skiController != null && skiController.GroundNormal.sqrMagnitude > 0.0001f
            ? skiController.GroundNormal.normalized
            : Vector3.up;
        float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
        Vector3 target = ResolveCurrentDebugTarget();
        float targetDistance = target.sqrMagnitude > 0.0001f
            ? Vector3.Distance(transform.position, target)
            : -1f;
        string skiGroundGap = skiController != null ? skiController.LastMeasuredGroundGap.ToString("0.00") : "n/a";
        string skiGroundRefresh = skiController != null ? skiController.LastGroundRefreshSource : "n/a";

        Debug.Log(
            $"[{nameof(NpcSkierBrain)}] Movement Debug: {name}\n" +
            $"state={_state} intent={_intentKind} genericIntent={_currentGenericIntent} previousGeneric={_previousGenericIntent} initialized={_initialized} active={gameObject.activeSelf}\n" +
            $"genericTarget={(_currentIntentTargetAnchor != null ? _currentIntentTargetAnchor.DisplayName : _currentIntentTargetObject != null ? _currentIntentTargetObject.name : _currentIntentTargetPosition.ToString())} genericExpiresIn={Mathf.Max(0f, _currentIntentExpiresAt - Time.time):0.0}s decision={_lastDecisionReason}\n" +
            $"liftPhase={_liftBoardingPhase} preferredGate={(_preferredBoardGate != null ? _preferredBoardGate.name : "none")} boardingRetries={_boardingRetryCount} lastBoardingFailure={_lastBoardingFailureReason}\n" +
            $"socialLoiter={_socialLoiterActive} socialAnchor={(_socialLoiterAnchor != null ? _socialLoiterAnchor.DisplayName : "none")} socialGroup={(_socialLoiterGroup != null ? _socialLoiterGroup.DisplayName : "none")} spectator={_spectatorCrowdActive}\n" +
            $"run={(_currentRun != null ? _currentRun.name : "none")} lift={(_currentLift != null ? _currentLift.name : "none")} hub={(_currentHubAnchor != null ? _currentHubAnchor.name : "none")}\n" +
            $"skiGrounded={(skiController != null && skiController.IsRiderGrounded)} skiPhysicsGrounded={(skiController != null && skiController.IsPhysicsGrounded)} stacked={(skiController != null && skiController.IsStacked)} grinding={(skiController != null && skiController.IsGrinding)}\n" +
            $"bodyNearGround={(skiController != null && skiController.IsBodyNearGround)} skiPlausible={(skiController != null && skiController.IsSkiContactPlausibleForBody)} groundGap={skiGroundGap} groundRefresh={skiGroundRefresh}\n" +
            $"walkMode={(walkingController != null && walkingController.IsWalkingMode)} walkGrounded={(walkingController != null && walkingController.IsWalkGrounded)} riderPose={(walkingController != null && walkingController.IsRiderPoseActive)}\n" +
            $"groundNormal={groundNormal} slope={slopeAngle:0.0} velocity={velocity} speed={velocity.magnitude:0.00}\n" +
            $"locomotionInput={(locomotion != null && locomotion.InputEnabled)} wantsWalkRecovery={(locomotion != null && locomotion.WantsWalkingRecovery)} pathHasDestination={(pathAgent != null && pathAgent.HasDestination)} pathStuck={(pathAgent != null && pathAgent.IsStuck)}\n" +
            $"target={target} targetDistance={(targetDistance >= 0f ? targetDistance.ToString("0.0") : "none")} currentBroadTarget={_currentBroadTarget} liftApproach={_currentLiftApproachPoint}",
            this);
    }

    private void Awake()
    {
        CacheCharacterLayers();
        CacheRefs();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        WarnIfLayerMaskContains(npcGroundSnapLayers, "Default", "NpcSkierBrain.npcGroundSnapLayers should only include Ground.");
        WarnIfLayerMaskContains(npcGroundSnapLayers, "NPC", "NpcSkierBrain.npcGroundSnapLayers should not include NPC.");
        WarnIfLayerMaskContains(npcGroundSnapLayers, "Player", "NpcSkierBrain.npcGroundSnapLayers should not include Player.");
    }

    private void WarnIfLayerMaskContains(LayerMask mask, string layerName, string message)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0 && (mask.value & (1 << layer)) != 0)
            Debug.LogWarning($"[{nameof(NpcSkierBrain)}] {message}", this);
    }
#endif

    private void Start()
    {
        InitializeNow();
    }

    private void OnDestroy()
    {
        if (skiController != null)
            skiController.OnStacked -= HandleStacked;
    }

    public void ConfigureRuns(IEnumerable<SkiRunLine> runs)
    {
        availableRuns.Clear();
        if (runs == null) return;

        foreach (var run in runs)
        {
            if (run != null)
                availableRuns.Add(run);
        }

        locomotion?.ConfigureRuns(availableRuns);
    }

    public void ConfigureLifts(IEnumerable<LiftLine> lifts)
    {
        availableLifts.Clear();
        if (lifts == null) return;

        foreach (var lift in lifts)
        {
            if (lift != null)
                availableLifts.Add(lift);
        }
    }

    public void ConfigurePlayerFocus(Transform focus)
    {
        playerFocus = focus;
    }

    public void DespawnToPool(string reason = "")
    {
        if (liftRider != null)
        {
            if (liftRider.IsAttached && liftRider.CurrentCarrier != null)
                liftRider.CurrentCarrier.DetachRider(liftRider);

            if (liftRider.NearbyBoardGate != null)
                liftRider.NearbyBoardGate.LeaveQueue(liftRider);
        }

        sorenessMeter?.ResetSoreness();
        PrepareForPoolSleep();

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void InitializeNow()
    {
        if (_initialized)
            return;

        CacheRefs();

        if (autoFindRunsInScene && availableRuns.Count == 0)
            availableRuns = new List<SkiRunLine>(FindObjectsOfType<SkiRunLine>(includeInactive: false));

        if (autoFindLiftsInScene && availableLifts.Count == 0)
            availableLifts = new List<LiftLine>(FindObjectsOfType<LiftLine>(includeInactive: false));

        CacheHubAnchors();

        locomotion?.ConfigureRuns(availableRuns);

        bool preserveAuthoredProfile = TryGetComponent(out NpcIdentity identity) && identity.IsAuthored && identity.PreserveAuthoredProfile;
        bool preserveAuthoredAppearance = identity != null && identity.IsAuthored && identity.PreserveAuthoredAppearance;

        if (randomizeProfileOnStart && profile != null && !preserveAuthoredProfile)
            profile.RandomizeProfile();

        if (randomizeAppearanceOnStart && appearanceGenerator != null && !preserveAuthoredAppearance)
            appearanceGenerator.ApplyRandomAppearance(profile);

        if (skiController != null)
        {
            if (locomotion != null)
                skiController.SetExternalInputSource(locomotion);

            skiController.AcceptPlayerInput = false;
        }

        if (walkingController != null)
        {
            walkingController.AcceptPlayerInput = false;
        }

        if (skiController != null)
            skiController.OnStacked += HandleStacked;

        _initialized = true;
        sorenessMeter?.ResetSoreness();
        EnterWaiting(Random.Range(waitMin, waitMax));
    }

    public void BeginSpectatorCrowd(Vector3 anchorPosition, Quaternion anchorRotation, Vector3 facingDirection)
    {
        if (!_initialized)
            InitializeNow();

        if (liftRider != null)
        {
            if (liftRider.IsAttached && liftRider.CurrentCarrier != null)
                liftRider.CurrentCarrier.DetachRider(liftRider);

            if (liftRider.NearbyBoardGate != null)
                liftRider.NearbyBoardGate.LeaveQueue(liftRider);
        }

        _spectatorCrowdActive = true;
        _spectatorAnchorPosition = SnapPointToGround(anchorPosition, anchorPosition.y);
        _spectatorAnchorRotation = anchorRotation;
        _spectatorForward = Vector3.ProjectOnPlane(facingDirection, Vector3.up);
        if (_spectatorForward.sqrMagnitude <= 0.0001f)
            _spectatorForward = anchorRotation * Vector3.forward;
        if (_spectatorForward.sqrMagnitude <= 0.0001f)
            _spectatorForward = transform.forward;
        _spectatorForward.Normalize();
        _spectatorMoveTarget = _spectatorAnchorPosition;
        _nextSpectatorShuffleTime = Time.time + ScaleFastForwardDelay(Random.Range(spectatorShuffleMinDelay, spectatorShuffleMaxDelay));
        _nextSpectatorLookTime = Time.time + ScaleFastForwardDelay(Random.Range(spectatorIdleLookMinDelay, spectatorIdleLookMaxDelay));
        _spectatorCrowdPhase = Random.Range(0f, Mathf.PI * 2f);

        pathAgent?.Stop();
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();
        locomotion?.SuppressRespawnRequests(0f);

        Quaternion spectatorRotation = Quaternion.LookRotation(_spectatorForward, Vector3.up);
        ResetBodyForStationaryNpcMode(_spectatorAnchorPosition, spectatorRotation, clearStack: true);

        walkingController?.ClearExternalMove();
        walkingController?.SetWalkPresentationKeepsSkisEquipped(true);
        walkingController?.ForceEnterWalkMode();
        sorenessMeter?.ResetSoreness();

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        _currentRun = null;
        _currentLift = null;
        _preferredBoardGate = null;
        _liftBoardingPhase = LiftNpcBoardingPhase.None;
        _lastBoardingFailureReason = "Pool sleep";
        _intentKind = IntentKind.None;
        _state = BrainState.Waiting;
    }

    public void EndSpectatorCrowd()
    {
        ResumeFromSpectatorCrowd(immediateIntent: false);
    }

    public void BeginSocialLoiter(NpcSocialAnchor anchor, NpcSocialGroupSO group, float duration)
    {
        if (anchor == null || !CanBeginSocialLoiter())
            return;

        Vector3 center = anchor.Center != null ? anchor.Center.position : anchor.transform.position;
        Vector3 facing = playerFocus != null
            ? Vector3.ProjectOnPlane(playerFocus.position - center, Vector3.up)
            : transform.forward;
        if (facing.sqrMagnitude <= 0.0001f)
            facing = transform.forward;

        _socialLoiterActive = true;
        _socialLoiterUntil = Time.time + Mathf.Max(1f, duration);
        _socialLoiterAnchor = anchor;
        _socialLoiterGroup = group;
        StampGenericIntent(
            NpcGenericActivityIntent.SocialLoiter,
            Mathf.Min(duration, Mathf.Max(1f, duration * 0.25f)),
            Mathf.Max(1f, duration),
            anchor.Center.position,
            anchor,
            anchor,
            $"Social loiter at {anchor.DisplayName}");
        BeginSpectatorCrowd(center, Quaternion.LookRotation(facing.normalized, Vector3.up), facing.normalized);
    }

    public void NotifySocialConversationCompleted(NpcSocialAnchor anchor, NpcSocialGroupSO group, bool forceLeave)
    {
        if (!_socialLoiterActive || anchor == null || _socialLoiterAnchor != anchor)
            return;

        _previousAnchor = anchor;
        _previousAnchorCooldownUntil = Time.time + Mathf.Max(0f, anchor.RevisitCooldownSeconds);

        if (!CanUseGenericLifecycle())
            return;

        float reaction = Random.Range(
            Mathf.Max(0.1f, socialConversationReactionMin),
            Mathf.Max(socialConversationReactionMin, socialConversationReactionMax));

        if (forceLeave)
        {
            _socialLoiterUntil = Mathf.Min(_socialLoiterUntil, Time.time + ScaleFastForwardDelay(reaction));
            _lastDecisionReason = $"Conversation completed at {anchor.DisplayName}; leaving after reaction pause.";
        }
        else
        {
            _socialLoiterUntil = Mathf.Min(_socialLoiterUntil, Time.time + ScaleFastForwardDelay(reaction + Random.Range(4f, 12f)));
            _lastDecisionReason = $"Conversation completed at {anchor.DisplayName}; staying briefly.";
        }
    }

    private Vector3 ResolveCurrentDebugTarget()
    {
        if (_socialLoiterActive && _socialLoiterAnchor != null)
            return _socialLoiterAnchor.Center.position;

        if (pathAgent != null && pathAgent.HasDestination)
            return pathAgent.Destination;

        if (_intentKind == IntentKind.LiftThenRun)
            return _currentLiftApproachPoint;

        if (_currentBroadTarget.sqrMagnitude > 0.0001f)
            return _currentBroadTarget;

        if (_currentRun != null)
            return GetBroadRunTarget(_currentRun);

        if (_currentHubAnchor != null)
            return _currentHubAnchor.position;

        return Vector3.zero;
    }

    public void ResumeFromSocialLoiter()
    {
        if (!_socialLoiterActive)
            return;

        NpcSocialAnchor completedAnchor = _socialLoiterAnchor;

        _socialLoiterActive = false;
        _socialLoiterAnchor = null;
        _socialLoiterGroup = null;

        // Clear the stationary spectator wrapper only.
        ResumeFromSpectatorCrowd(immediateIntent: false);

        CompleteCurrentIntent(completedAnchor != null
            ? $"Social loiter finished at {completedAnchor.DisplayName}"
            : "Social loiter finished");
    }

    public void ResumeFromSpectatorCrowd(bool immediateIntent = true)
    {
        if (!_spectatorCrowdActive)
            return;

        _spectatorCrowdActive = false;
        if (_socialLoiterActive)
        {
            _socialLoiterActive = false;
            _socialLoiterAnchor = null;
            _socialLoiterGroup = null;
        }
        walkingController?.ClearExternalMove();
        walkingController?.SetWalkPresentationKeepsSkisEquipped(false);

        if (immediateIntent)
            PickNextIntent();
        else
            EnterWaiting(Random.Range(waitMin * 0.4f, waitMax * 0.8f));
    }

    public void PrepareForPoolSleep()
    {
        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        walkingController?.SetWalkPresentationKeepsSkisEquipped(false);
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();
        locomotion?.SuppressRespawnRequests(0f);

        _currentRun = null;
        _currentLift = null;
        _intentKind = IntentKind.None;
        _pendingSpawnHint = SpawnContextHint.None;
        ClearGenericIntent("Pool sleep", rememberPrevious: false);
        _state = BrainState.Waiting;
        _respawnSuppressedUntil = 0f;
        _walkingRecoveryUntil = 0f;
        _liftStageReached = false;
        _boardingRetryCount = 0;
        _currentHubAnchor = null;
        _spectatorCrowdActive = false;
        _socialLoiterActive = false;
        _socialLoiterAnchor = null;
        _socialLoiterGroup = null;
        _lastPoolSleepTime = Time.time;
        _lastPoolSleepPosition = transform.position;
        _skiModeStrictAirborneSince = -1f;
    }

    public void SetSpawnContextHint(SpawnContextHint hint, Vector3 worldPoint)
    {
        _pendingSpawnHint = hint;
        _pendingHintWorldPoint = worldPoint;
    }

    public void WakeFromPool(Vector3 position, Quaternion rotation)
    {
        CacheRefs();

        pathAgent?.Stop();
        walkingController?.ClearExternalMove();

        if (walkingController != null)
            walkingController.SetWalkPresentationKeepsSkisEquipped(false);

        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();
        locomotion?.SuppressRespawnRequests(0f);

        sorenessMeter?.ResetSoreness();

        _spectatorCrowdActive = false;
        _socialLoiterActive = false;
        _socialLoiterAnchor = null;
        _socialLoiterGroup = null;

        _currentRun = null;
        _currentLift = null;
        _preferredBoardGate = null;
        _liftBoardingPhase = LiftNpcBoardingPhase.None;
        _lastBoardingFailureReason = "Wake from pool";
        _currentHubAnchor = null;
        ClearGenericIntent("Wake from pool", rememberPrevious: false);

        _currentBroadTarget = Vector3.zero;
        _currentLiftApproachPoint = Vector3.zero;
        _currentLiftStagePoint = Vector3.zero;

        _intentKind = IntentKind.None;
        _state = BrainState.Waiting;

        _liftStageReached = false;
        _boardingRetryCount = 0;
        _postLiftRunJoinUntil = 0f;
        _walkingRecoveryUntil = 0f;
        _respawnSuppressedUntil = Time.time + ScaleFastForwardDelay(1.5f);
        _skiModeStrictAirborneSince = -1f;

        if (skiController != null)
        {
            skiController.TeleportToSpawn(position, rotation, snapToGround: true);

            if (locomotion != null)
                skiController.SetExternalInputSource(locomotion);

            skiController.AcceptPlayerInput = false;
        }
        else
        {
            ResetBodyForStationaryNpcMode(position, rotation, clearStack: true);
        }

        if (walkingController != null)
            walkingController.AcceptPlayerInput = false;

        if (!_initialized)
            InitializeNow();
        else
            EnterWaiting(Random.Range(waitMin * 0.35f, waitMax * 0.65f));
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (_genericBehaviourPausedByDefinedCharacter)
            return;

        if (_socialLoiterActive && Time.time >= _socialLoiterUntil)
        {
            ResumeFromSocialLoiter();
            return;
        }

        if (_spectatorCrowdActive)
        {
            TickSpectatorCrowd();
            return;
        }

        if (ShouldRerollIntent())
        {
            ExpireCurrentIntent("Generic intent expired or stalled");
            return;
        }

        if (CheckStrictSkiModeAirborneGuard())
            return;

        switch (_state)
        {
            case BrainState.Waiting:
                TickWaiting();
                break;

            case BrainState.SkiingAmbient:
                TickSkiingAmbient();
                break;

            case BrainState.SkiingRun:
                TickSkiingRun();
                break;

            case BrainState.WalkingSupport:
                TickWalkingSupport();
                break;

            case BrainState.BoardingLift:
                TickBoardingLift();
                break;

            case BrainState.RidingLift:
                TickRidingLift();
                break;

            case BrainState.Recovering:
                TickRecovering();
                break;
        }
    }

    private void CacheRefs()
    {
        if (skiController == null) skiController = GetComponent<SkiController>();
        if (walkingController == null) walkingController = GetComponent<WalkingController>();
        if (liftRider == null) liftRider = GetComponent<LiftRider>();
        if (profile == null) profile = GetComponent<NpcSkierProfile>();
        if (locomotion == null) locomotion = GetComponent<NpcSkierRunFollower>();
        if (appearanceGenerator == null) appearanceGenerator = GetComponent<NpcSkierAppearanceGenerator>();
        if (pathAgent == null) pathAgent = GetComponent<NpcAstarPathAgent>();
        if (sorenessMeter == null) sorenessMeter = GetComponent<SorenessMeter>();
        if (body == null) body = GetComponent<Rigidbody>();
        if (playerFocus == null && Camera.main != null)
            playerFocus = Camera.main.transform;
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

        if (body != null && c.attachedRigidbody == body)
            return true;

        if (c.transform == transform || c.transform.IsChildOf(transform))
            return true;

        return false;
    }

    private bool IsCharacterLayer(Collider c)
    {
        if (c == null)
            return false;

        CacheCharacterLayers();
        int layer = c.gameObject.layer;
        return (_npcLayer >= 0 && layer == _npcLayer) ||
               (_playerLayer >= 0 && layer == _playerLayer);
    }

    private LayerMask EffectiveNpcGroundSnapLayers()
    {
        if (npcGroundSnapLayers.value != 0)
            return npcGroundSnapLayers;

        int groundLayer = LayerMask.NameToLayer("Ground");
        return groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)0;
    }

    private bool TryGetGroundSnapHit(Vector3 candidate, out RaycastHit bestHit)
    {
        Vector3 rayOrigin = candidate + Vector3.up * 25f;
        LayerMask mask = EffectiveNpcGroundSnapLayers();
        int count = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _groundSnapHits, 60f, mask, QueryTriggerInteraction.Ignore);

        bestHit = default;
        float bestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _groundSnapHits[i];
            Collider c = hit.collider;
            if (c == null || IsOwnCollider(c) || IsCharacterLayer(c))
                continue;

            if ((mask.value & (1 << c.gameObject.layer)) == 0)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool CheckStrictSkiModeAirborneGuard()
    {
        if (skiController == null || walkingController == null)
            return false;

        bool skiingState = _state == BrainState.SkiingAmbient || _state == BrainState.SkiingRun;
        if (!skiingState || walkingController.IsWalkingMode || skiController.IsStacked)
        {
            _skiModeStrictAirborneSince = -1f;
            return false;
        }

        bool safelySupported = skiController.IsPhysicsGrounded || skiController.IsGrinding;
        if (safelySupported)
        {
            _skiModeStrictAirborneSince = -1f;
            return false;
        }

        float gap = skiController.LastMeasuredGroundGap;
        if ((float.IsNaN(gap) || float.IsInfinity(gap)) && TryGetGroundSnapHit(transform.position, out RaycastHit hit))
            gap = Mathf.Max(0f, transform.position.y - hit.point.y);

        bool clearlyFloating = float.IsNaN(gap) || float.IsInfinity(gap) || gap >= skiModeFloatingGroundGap;
        if (!clearlyFloating)
        {
            _skiModeStrictAirborneSince = -1f;
            return false;
        }

        if (_skiModeStrictAirborneSince < 0f)
        {
            _skiModeStrictAirborneSince = Time.time;
            return false;
        }

        if (Time.time - _skiModeStrictAirborneSince < ScaleFastForwardDelay(skiModeStrictAirborneGrace))
            return false;

        Vector3 recoveryPoint = SnapPointToGround(transform.position, transform.position.y);
        HandleMobilityFailure(recoveryPoint);
        _skiModeStrictAirborneSince = -1f;
        return true;
    }

    public void SetVisibleFastForwardMultiplier(float multiplier)
    {
        _visibleFastForwardMultiplier = Mathf.Max(1f, multiplier);

        if (locomotion != null)
            locomotion.SetVisibleFastForwardMultiplier(_visibleFastForwardMultiplier);
    }

    public bool TryGetVisibilityBounds(out Bounds bounds)
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            _cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);

        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            bounds = new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
            return true;
        }

        bool found = false;
        bounds = default;

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer r = _cachedRenderers[i];
            if (r == null || !r.enabled)
                continue;

            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!found)
        {
            bounds = new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
            return true;
        }

        return true;
    }

    private float ScaleFastForwardDelay(float seconds)
    {
        return Mathf.Max(0.05f, seconds / Mathf.Max(1f, _visibleFastForwardMultiplier));
    }

    private void EnterWaiting(float delay)
    {
        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();

        _intentKind = IntentKind.None;
        _state = BrainState.Waiting;
        _stateUntil = Time.time + ScaleFastForwardDelay(Mathf.Max(0.1f, delay));
        _walkingRecoveryUntil = 0f;
        _nextLookAroundTime = Time.time + ScaleFastForwardDelay(Random.Range(lookAroundIntervalMin, lookAroundIntervalMax));
        _currentHubAnchor = ResolvePreferredHubAnchor();
        _nextHubShuffleTime = _currentHubAnchor != null
            ? Time.time + ScaleFastForwardDelay(Random.Range(hubShuffleMinDelay, hubShuffleMaxDelay))
            : float.PositiveInfinity;
    }

    private void TickWaiting()
    {
        if (Time.time >= _nextLookAroundTime)
        {
            UpdateIdleFacing();
            _nextLookAroundTime = Time.time + ScaleFastForwardDelay(Random.Range(lookAroundIntervalMin, lookAroundIntervalMax));
        }

        if (_currentHubAnchor != null && Time.time >= _nextHubShuffleTime)
        {
            if (StartHubLoiterFromAnchor(_currentHubAnchor))
                return;

            _nextHubShuffleTime = Time.time + ScaleFastForwardDelay(Random.Range(hubShuffleMinDelay, hubShuffleMaxDelay));
        }

        if (Time.time < _stateUntil)
            return;

        if (TryStartHubLoiter())
            return;

        PickNextIntent();
    }

    private void TickSpectatorCrowd()
    {
        if (skiController != null && skiController.IsStacked)
        {
            Vector3 forwardHint = _spectatorForward.sqrMagnitude > 0.0001f
                ? _spectatorForward
                : transform.forward;

            skiController.ResetStackStateSilently(snapUpright: true, forwardHint: forwardHint);
            skiController.ReleaseStackSkiVisualOverridesAndSnap(snapToNeutralPose: true);
            skiController.ResetVisualPoseForWalkModeHandoff();

            if (body != null)
            {
                body.freezeRotation = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        bool keepSkisEquipped = ShouldKeepSkisEquippedForSpectator();
        walkingController?.SetWalkPresentationKeepsSkisEquipped(keepSkisEquipped);
        walkingController?.ForceEnterWalkMode();
        StabilizeWalkPosture();

        if (Time.time >= _nextSpectatorShuffleTime)
        {
            _spectatorMoveTarget = SampleSpectatorMoveTarget();
            _nextSpectatorShuffleTime = Time.time + ScaleFastForwardDelay(Random.Range(spectatorShuffleMinDelay, spectatorShuffleMaxDelay));
        }

        if (Time.time >= _nextSpectatorLookTime)
        {
            UpdateSpectatorFacing();
            _nextSpectatorLookTime = Time.time + ScaleFastForwardDelay(Random.Range(spectatorIdleLookMinDelay, spectatorIdleLookMaxDelay));
        }

        Vector3 toTarget = Vector3.ProjectOnPlane(_spectatorMoveTarget - transform.position, Vector3.up);
        if (toTarget.sqrMagnitude > 0.16f)
        {
            walkingController?.SetExternalMoveToward(toTarget.normalized, spectatorWalkStrength, sprint: false, facingGateDot: -1f);
        }
        else
        {
            walkingController?.ClearExternalMove();
        }

        ApplySpectatorCelebrationPose(toTarget);
    }

    private void ApplySpectatorCelebrationPose(Vector3 toTarget)
    {
        float time = Time.time + _spectatorCrowdPhase;
        float hop = Mathf.Max(0f, Mathf.Sin(time * Mathf.Max(0.1f, spectatorCelebrateHopFrequency))) * Mathf.Max(0.12f, spectatorCelebrateHopHeight);
        float sway = Mathf.Sin(time * Mathf.Max(0.1f, spectatorCelebrateHopFrequency * 0.65f)) * Mathf.Max(8f, spectatorCelebrateSwayDegrees);

        Vector3 desiredForward = toTarget.sqrMagnitude > 0.16f
            ? toTarget.normalized
            : _spectatorForward;

        if (desiredForward.sqrMagnitude <= 0.0001f)
            desiredForward = transform.forward;

        desiredForward = Vector3.ProjectOnPlane(desiredForward, Vector3.up).normalized;
        Quaternion desiredRotation = Quaternion.LookRotation(desiredForward, Vector3.up) * Quaternion.Euler(0f, sway, 0f);

        Vector3 grounded = SnapPointToGround(transform.position, _spectatorAnchorPosition.y);
        grounded.y += hop;

        Quaternion correctedRotation = Quaternion.Slerp(transform.rotation, desiredRotation, 10f * Time.deltaTime);

        if (body != null && !body.isKinematic)
        {
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;

            // Keep only a very small planar carry so old fall/slide velocity cannot survive into loiter mode.
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
            body.linearVelocity = planar * 0.15f;
            body.angularVelocity = Vector3.zero;

            body.position = grounded;
            body.rotation = correctedRotation;
        }

        transform.SetPositionAndRotation(grounded, correctedRotation);
    }

    private void PickNextIntent()
    {
        if (CanUseGenericLifecycle())
        {
            ChooseNextGenericIntent("Pick next activity");
            return;
        }

        BeginMountainTravelIntent();
    }

    public void ChooseNextGenericIntent(string reason)
    {
        if (!CanUseGenericLifecycle())
        {
            _lastDecisionReason = $"Generic lifecycle blocked: {reason}";
            BeginMountainTravelIntent();
            return;
        }

        BuildGenericIntentCandidates(reason);
        if (_genericIntentCandidates.Count == 0)
        {
            BeginGenericIntent(NpcGenericActivityIntent.SkiRun, null, reason + " fallback");
            return;
        }

        float total = 0f;
        for (int i = 0; i < _genericIntentCandidates.Count; i++)
            total += Mathf.Max(0f, _genericIntentCandidates[i].weight);

        if (total <= 0.001f)
        {
            BeginGenericIntent(NpcGenericActivityIntent.SkiRun, null, reason + " zero weight fallback");
            return;
        }

        float roll = Random.value * total;
        GenericIntentCandidate selected = _genericIntentCandidates[_genericIntentCandidates.Count - 1];
        for (int i = 0; i < _genericIntentCandidates.Count; i++)
        {
            roll -= Mathf.Max(0f, _genericIntentCandidates[i].weight);
            if (roll <= 0f)
            {
                selected = _genericIntentCandidates[i];
                break;
            }
        }

        BeginGenericIntent(selected.intent, selected, selected.reason);
    }

    public void BeginGenericIntent(NpcGenericActivityIntent intent, object context = null, string reason = "")
    {
        GenericIntentCandidate candidate = context is GenericIntentCandidate typed
            ? typed
            : new GenericIntentCandidate { intent = intent, weight = 1f, reason = reason };

        if (candidate.intent == NpcGenericActivityIntent.None)
            candidate.intent = intent;

        if ((candidate.intent == NpcGenericActivityIntent.TraverseToNearbyArea ||
             candidate.intent == NpcGenericActivityIntent.IdleWander) &&
            candidate.targetPosition.sqrMagnitude <= 0.0001f)
        {
            candidate.targetPosition = SampleNearbyFlowTarget();
        }

        Vector2 range = ResolveGenericIntentDurationRange(candidate.intent);
        StampGenericIntent(
            candidate.intent,
            range.x,
            range.y,
            candidate.targetPosition,
            candidate.targetObject,
            candidate.anchor,
            string.IsNullOrWhiteSpace(reason) ? candidate.reason : reason);

        switch (candidate.intent)
        {
            case NpcGenericActivityIntent.SocialLoiter:
            case NpcGenericActivityIntent.VisitKiosk:
            case NpcGenericActivityIntent.WatchRace:
            case NpcGenericActivityIntent.RestAtLodge:
            case NpcGenericActivityIntent.VisitMedic:
            case NpcGenericActivityIntent.ViewpointPause:
            case NpcGenericActivityIntent.PracticeTrick:
                if (candidate.anchor == null)
                    candidate.anchor = ChooseAnchorCandidate(ResolvePreferredAnchorType(candidate.intent));
                if (candidate.anchor != null)
                {
                    BeginSocialLoiter(candidate.anchor, null, Random.Range(range.x, range.y));
                    return;
                }
                BeginMountainTravelIntent();
                return;

            case NpcGenericActivityIntent.RideLift:
            case NpcGenericActivityIntent.QueueAtLift:
                BeginLiftIntent(candidate.intent == NpcGenericActivityIntent.QueueAtLift ? "QueueAtLift intent" : "RideLift intent");
                return;

            case NpcGenericActivityIntent.TraverseToNearbyArea:
            case NpcGenericActivityIntent.IdleWander:
                if (candidate.targetPosition.sqrMagnitude > 0.0001f)
                {
                    if (ShouldUseWalkingSupportTo(candidate.targetPosition))
                        BeginWalkingSupport(candidate.targetPosition);
                    else
                        BeginAmbientSki(candidate.targetPosition);
                    return;
                }
                BeginMountainTravelIntent();
                return;

            case NpcGenericActivityIntent.LeaveArea:
                if (NpcSkierSpawner.Instance != null && CanLeaveAreaNow())
                {
                    DespawnToPool("Generic intent LeaveArea");
                    return;
                }
                BeginGenericIntent(NpcGenericActivityIntent.TraverseToNearbyArea, null, "LeaveArea deferred because NPC is still relevant");
                return;

            case NpcGenericActivityIntent.SkiRun:
            default:
                BeginMountainTravelIntent(preferLift: false);
                return;
        }
    }

    public void CompleteCurrentIntent(string reason)
    {
        ClearGenericIntent(reason, rememberPrevious: true);

        if (_initialized && CanUseGenericLifecycle() && !_spectatorCrowdActive && !_socialLoiterActive)
            ChooseNextGenericIntent(reason);
    }

    public void ExpireCurrentIntent(string reason)
    {
        ClearGenericIntent(reason, rememberPrevious: true);

        if (_initialized && CanUseGenericLifecycle() && !_spectatorCrowdActive && !_socialLoiterActive)
            ChooseNextGenericIntent(reason);
    }

    public bool ShouldRerollIntent()
    {
        if (!CanUseGenericLifecycle() || _currentGenericIntent == NpcGenericActivityIntent.None)
            return false;

        if (_state == BrainState.Recovering || _state == BrainState.BoardingLift || _state == BrainState.RidingLift)
            return false;

        if (Time.time < _currentIntentStartedAt + ScaleFastForwardDelay(_currentIntentMinDuration))
            return false;

        if (Time.time >= _currentIntentExpiresAt)
            return true;

        return pathAgent != null && pathAgent.IsStuck;
    }

    [ContextMenu("Print Generic Intent Debug State")]
    public void PrintGenericIntentDebugState()
    {
        Debug.Log(BuildGenericIntentDebugString(), this);
    }

    [ContextMenu("Force Choose New Generic Intent")]
    public void ForceChooseNewGenericIntent()
    {
        ExpireCurrentIntent("Designer forced new generic intent");
    }

    [ContextMenu("Force Intent: SocialLoiter")]
    public void ForceIntentSocialLoiter()
    {
        BeginGenericIntent(NpcGenericActivityIntent.SocialLoiter, null, "Designer forced SocialLoiter");
    }

    [ContextMenu("Force Intent: SkiRun")]
    public void ForceIntentSkiRun()
    {
        BeginGenericIntent(NpcGenericActivityIntent.SkiRun, null, "Designer forced SkiRun");
    }

    [ContextMenu("Force Intent: VisitKiosk")]
    public void ForceIntentVisitKiosk()
    {
        BeginGenericIntent(NpcGenericActivityIntent.VisitKiosk, null, "Designer forced VisitKiosk");
    }

    [ContextMenu("Force Intent: RideLift")]
    public void ForceIntentRideLift()
    {
        BeginGenericIntent(NpcGenericActivityIntent.RideLift, null, "Designer forced RideLift");
    }

    [ContextMenu("Force Intent: QueueAtLift")]
    public void ForceIntentQueueAtLift()
    {
        BeginGenericIntent(NpcGenericActivityIntent.QueueAtLift, null, "Designer forced QueueAtLift");
    }

    [ContextMenu("Force Complete Current Intent")]
    public void ForceCompleteCurrentIntent()
    {
        CompleteCurrentIntent("Designer forced complete");
    }

    [ContextMenu("Force Replan Lift Route")]
    public void ForceReplanLiftRoute()
    {
        BeginLiftIntent("Designer forced lift route replan");
    }

    [ContextMenu("Print Lift Boarding Debug State")]
    public void PrintLiftBoardingDebugState()
    {
        Debug.Log(BuildLiftBoardingDebugString(), this);
    }

    [ContextMenu("Print Full NPC Behaviour Debug State")]
    public void PrintFullNpcBehaviourDebugState()
    {
        PrintNpcMovementDebugState();
        PrintGenericIntentDebugState();
        PrintLiftBoardingDebugState();
    }

    private void BeginMountainTravelIntent(bool preferLift = false)
    {
        if (TryBuildHintedIntent())
        {
            _pendingSpawnHint = SpawnContextHint.None;
            return;
        }

        _currentRun = ChooseRunForProfile();
        if (_currentRun == null)
        {
            EnterWaiting(Random.Range(waitMin, waitMax));
            return;
        }

        _currentBroadTarget = GetBroadRunTarget(_currentRun);
        _currentLift = ChooseLiftForRun(_currentRun, _currentBroadTarget);
        _liftStageReached = false;
        _boardingRetryCount = 0;

        bool canJoinRunDirect = CanJoinRunDirectly(_currentRun);

        if (_currentLift != null && (preferLift || ShouldPreferLiftForRun(_currentLift, _currentBroadTarget)))
        {
            _intentKind = IntentKind.LiftThenRun;
            _preferredBoardGate = ResolvePreferredBoardGate(_currentLift);
            _liftBoardingPhase = LiftNpcBoardingPhase.MovingToStage;
            _currentLiftStagePoint = ResolveLiftStagePoint(_currentLift);
            _currentLiftApproachPoint = _currentLiftStagePoint;

            if (ShouldUseWalkingSupportTo(_currentLiftApproachPoint) ||
                Vector3.Distance(transform.position, _currentLiftApproachPoint) > broadLiftApproachRadius)
                BeginWalkingSupport(_currentLiftApproachPoint);
            else
                BeginAmbientSki(_currentLiftApproachPoint);

            return;
        }

        if (canJoinRunDirect)
        {
            _intentKind = IntentKind.DirectRun;
            BeginRunSki(_currentRun);
            return;
        }

        _intentKind = IntentKind.AmbientCruise;

        if (ShouldUseWalkingSupportTo(_currentBroadTarget))
            BeginWalkingSupport(_currentBroadTarget);
        else
            BeginAmbientSki(_currentBroadTarget);
    }

    private bool CanLeaveAreaNow()
    {
        if (!CanBeBorrowedForSocialPopulation())
            return false;

        Transform focus = ResolvePlayerFocus();
        if (focus != null && Vector3.Distance(transform.position, focus.position) < 180f)
            return false;

        Camera cam = Camera.main;
        if (cam != null && TryGetVisibilityBounds(out Bounds bounds))
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            if (GeometryUtility.TestPlanesAABB(planes, bounds))
                return false;
        }

        return true;
    }

    private void BeginLiftIntent(string reason)
    {
        if (liftRider == null)
        {
            _lastBoardingFailureReason = "No LiftRider on NPC";
            BeginMountainTravelIntent(preferLift: false);
            return;
        }

        _currentLift = ChooseLiftForIntent();
        if (_currentLift == null)
        {
            _lastBoardingFailureReason = "No valid lift found for intent";
            BeginMountainTravelIntent(preferLift: false);
            return;
        }

        _preferredBoardGate = ResolvePreferredBoardGate(_currentLift);
        _currentRun = ChooseRunNearLiftTop(_currentLift);
        _currentBroadTarget = _currentRun != null ? GetBroadRunTarget(_currentRun) : (_currentLift.topStation != null ? _currentLift.topStation.position : _currentLift.bottomStation.position);
        _currentLiftStagePoint = ResolveLiftStagePoint(_currentLift);
        _currentLiftApproachPoint = _preferredBoardGate != null
            ? _preferredBoardGate.GetNpcEntryPosition()
            : ResolveLiftApproachPoint(_currentLift);
        _liftStageReached = false;
        _boardingRetryCount = 0;
        _intentKind = IntentKind.LiftThenRun;
        _liftBoardingPhase = LiftNpcBoardingPhase.MovingToStage;
        _lastBoardingFailureReason = $"Planning lift route: {reason}";
        _suppressNextIntentRepeatPenalty = true;

        if (ShouldUseWalkingSupportTo(_currentLiftStagePoint) ||
            Vector3.Distance(transform.position, _currentLiftStagePoint) > broadLiftApproachRadius)
            BeginWalkingSupport(_currentLiftStagePoint);
        else
            BeginAmbientSki(_currentLiftStagePoint);
    }

    private LiftLine ChooseLiftForIntent()
    {
        if (availableLifts == null || availableLifts.Count == 0)
            return null;

        LiftLine best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < availableLifts.Count; i++)
        {
            LiftLine lift = availableLifts[i];
            if (lift == null || lift.bottomStation == null || lift.topStation == null)
                continue;

            float verticalGain = lift.topStation.position.y - lift.bottomStation.position.y;
            if (verticalGain < minimumLiftVerticalGain * 0.35f)
                continue;

            LiftBoardGate gate = ResolvePreferredBoardGate(lift);
            float approach = Vector3.Distance(transform.position, gate != null ? gate.GetNpcEntryPosition() : lift.bottomStation.position);
            float topToRun = 0f;
            SkiRunLine run = ChooseRunNearLiftTop(lift);
            if (run != null)
                topToRun = Vector3.Distance(lift.topStation.position, GetBroadRunTarget(run));

            float gateScore = gate != null ? 60f : 0f;
            float distanceScore = Mathf.Clamp(180f - approach, -120f, 180f) * 0.25f;
            float runScore = run != null ? Mathf.Clamp(160f - topToRun, -80f, 160f) * 0.2f : -20f;
            float score = gateScore + distanceScore + runScore + verticalGain * 0.08f + Random.Range(-4f, 4f);

            if (score > bestScore)
            {
                bestScore = score;
                best = lift;
            }
        }

        return best;
    }

    private bool CanUseGenericLifecycle()
    {
        if (!useGenericActivityLifecycle || _genericBehaviourPausedByDefinedCharacter)
            return false;

        if (TryGetComponent(out NpcDefinedCharacterBehaviour definedBehaviour))
            return definedBehaviour.AllowGenericSkiBrain;

        if (TryGetComponent(out NpcIdentity identity) && identity.IsAuthored)
            return false;

        return true;
    }

    private void StampGenericIntent(
        NpcGenericActivityIntent intent,
        float minDuration,
        float maxDuration,
        Vector3 targetPosition,
        UnityEngine.Object targetObject,
        NpcSocialAnchor targetAnchor,
        string reason)
    {
        if (_currentGenericIntent != NpcGenericActivityIntent.None && _currentGenericIntent != intent)
            _previousGenericIntent = _currentGenericIntent;

        if (_currentIntentTargetAnchor != null && _currentIntentTargetAnchor != targetAnchor)
        {
            _previousAnchor = _currentIntentTargetAnchor;
            _previousAnchorCooldownUntil = Time.time + Mathf.Max(0f, _currentIntentTargetAnchor.RevisitCooldownSeconds);
        }

        _currentGenericIntent = intent;
        _currentIntentStartedAt = Time.time;
        _currentIntentMinDuration = Mathf.Max(0.1f, minDuration);
        _currentIntentMaxDuration = Mathf.Max(_currentIntentMinDuration, maxDuration);
        _currentIntentExpiresAt = Time.time + ScaleFastForwardDelay(Random.Range(_currentIntentMinDuration, _currentIntentMaxDuration));
        _currentIntentTargetPosition = targetPosition;
        _currentIntentTargetObject = targetObject;
        _currentIntentTargetAnchor = targetAnchor;
        _lastDecisionReason = string.IsNullOrWhiteSpace(reason) ? $"Started {intent}" : reason;
    }

    private void ClearGenericIntent(string reason, bool rememberPrevious)
    {
        if (rememberPrevious && _currentGenericIntent != NpcGenericActivityIntent.None)
            _previousGenericIntent = _currentGenericIntent;

        if (rememberPrevious && _currentIntentTargetAnchor != null)
        {
            _previousAnchor = _currentIntentTargetAnchor;
            _previousAnchorCooldownUntil = Time.time + Mathf.Max(0f, _currentIntentTargetAnchor.RevisitCooldownSeconds);
        }

        _currentGenericIntent = NpcGenericActivityIntent.None;
        _currentIntentStartedAt = 0f;
        _currentIntentMinDuration = 0f;
        _currentIntentMaxDuration = 0f;
        _currentIntentExpiresAt = 0f;
        _currentIntentTargetPosition = Vector3.zero;
        _currentIntentTargetObject = null;
        _currentIntentTargetAnchor = null;
        _lastDecisionReason = string.IsNullOrWhiteSpace(reason) ? "Cleared generic intent" : reason;
    }

    private void BuildGenericIntentCandidates(string reason)
    {
        _genericIntentCandidates.Clear();

        float social = profile != null ? profile.Socialness : 0.5f;
        float explore = profile != null ? profile.ExplorationWeight : 0.5f;
        float lift = profile != null ? profile.LiftUseWeight : 0.6f;
        float race = profile != null ? profile.RaceInterest : 0.25f;
        float trick = profile != null ? profile.TrickInterest : 0.25f;
        bool afterLoiter = IsLoiterLikeIntent(_previousGenericIntent);
        float movementBoost = afterLoiter ? 1f + Mathf.Max(0f, flowThroughMovementBiasAfterLoiter) : 1f;
        float loiterDamping = afterLoiter ? Mathf.Clamp01(1f - flowThroughMovementBiasAfterLoiter) : 1f;

        AddGenericIntentCandidate(NpcGenericActivityIntent.SkiRun, (1.1f + GetLiftLapBias() * 0.25f) * movementBoost, null, null, Vector3.zero, reason);
        AddGenericIntentCandidate(NpcGenericActivityIntent.RideLift, lift * movementBoost, null, null, Vector3.zero, reason);
        AddGenericIntentCandidate(NpcGenericActivityIntent.TraverseToNearbyArea, explore * 0.45f * movementBoost, null, null, SampleNearbyFlowTarget(), reason);
        AddGenericIntentCandidate(NpcGenericActivityIntent.IdleWander, (profile != null ? profile.IdleWanderWeight : 0.15f) * movementBoost, null, null, SampleNearbyFlowTarget(), reason);
        AddGenericIntentCandidate(NpcGenericActivityIntent.LeaveArea, profile != null ? profile.LeaveAreaWeight : 0.08f, null, null, Vector3.zero, reason);

        AddBestAnchorCandidate(NpcGenericActivityIntent.SocialLoiter, social * loiterDamping, null, reason);
        AddBestAnchorCandidate(NpcGenericActivityIntent.VisitKiosk, (profile != null ? profile.KioskVisitWeight : 0.2f) * loiterDamping, NpcSocialAnchorType.Kiosk, reason);
        AddBestAnchorCandidate(NpcGenericActivityIntent.WatchRace, race * loiterDamping, NpcSocialAnchorType.RaceStart, reason);
        AddBestAnchorCandidate(NpcGenericActivityIntent.RestAtLodge, (profile != null ? profile.LodgeRestWeight : 0.2f) * loiterDamping, NpcSocialAnchorType.Lodge, reason);
        AddBestAnchorCandidate(NpcGenericActivityIntent.VisitMedic, (profile != null ? profile.MedicVisitWeight : 0.05f) * loiterDamping, NpcSocialAnchorType.MedicTent, reason);
        AddBestAnchorCandidate(NpcGenericActivityIntent.ViewpointPause, (profile != null ? profile.ViewpointPauseWeight : 0.25f) * loiterDamping, NpcSocialAnchorType.Viewpoint, reason);
        AddBestAnchorCandidate(NpcGenericActivityIntent.ViewpointPause, (profile != null ? profile.ViewpointPauseWeight : 0.25f) * 0.75f * loiterDamping, NpcSocialAnchorType.RunOverlook, reason);
        AddBestAnchorCandidate(NpcGenericActivityIntent.PracticeTrick, trick * loiterDamping, NpcSocialAnchorType.TerrainPark, reason);

        _suppressNextIntentRepeatPenalty = false;
    }

    private void AddGenericIntentCandidate(
        NpcGenericActivityIntent intent,
        float baseWeight,
        NpcSocialAnchor anchor,
        UnityEngine.Object targetObject,
        Vector3 targetPosition,
        string reason)
    {
        float weight = Mathf.Max(0f, baseWeight);
        if (!_suppressNextIntentRepeatPenalty && intent == _previousGenericIntent)
            weight *= Mathf.Clamp01(genericIntentRepeatPenalty);

        if (anchor != null)
        {
            float anchorWeight = anchor.GetCandidateWeightForActor(GetComponent<NpcSocialActor>(), this);
            weight *= anchorWeight;

            if (anchor == _previousAnchor && Time.time < _previousAnchorCooldownUntil)
                weight *= Mathf.Clamp01(genericAnchorRepeatPenalty);
        }

        if (weight <= 0.001f)
            return;

        _genericIntentCandidates.Add(new GenericIntentCandidate
        {
            intent = intent,
            weight = weight,
            anchor = anchor,
            targetObject = targetObject != null ? targetObject : anchor,
            targetPosition = targetPosition.sqrMagnitude > 0.0001f ? targetPosition : anchor != null ? anchor.Center.position : Vector3.zero,
            reason = $"{reason}: {intent} weight={weight:0.00}" + (anchor != null ? $" anchor={anchor.DisplayName} crowd={anchor.GetCrowdingScore():0.00}" : string.Empty)
        });
    }

    private static bool IsLoiterLikeIntent(NpcGenericActivityIntent intent)
    {
        switch (intent)
        {
            case NpcGenericActivityIntent.SocialLoiter:
            case NpcGenericActivityIntent.VisitKiosk:
            case NpcGenericActivityIntent.WatchRace:
            case NpcGenericActivityIntent.RestAtLodge:
            case NpcGenericActivityIntent.VisitMedic:
            case NpcGenericActivityIntent.ViewpointPause:
            case NpcGenericActivityIntent.PracticeTrick:
                return true;
            default:
                return false;
        }
    }

    private void AddBestAnchorCandidate(NpcGenericActivityIntent intent, float baseWeight, NpcSocialAnchorType? requiredType, string reason)
    {
        if (baseWeight <= 0.001f)
            return;

        NpcSocialAnchor anchor = ChooseAnchorCandidate(requiredType);
        if (anchor == null)
            return;

        AddGenericIntentCandidate(intent, baseWeight, anchor, anchor, anchor.Center.position, reason);
    }

    private NpcSocialAnchorType? ResolvePreferredAnchorType(NpcGenericActivityIntent intent)
    {
        switch (intent)
        {
            case NpcGenericActivityIntent.VisitKiosk:
                return NpcSocialAnchorType.Kiosk;
            case NpcGenericActivityIntent.WatchRace:
                return NpcSocialAnchorType.RaceStart;
            case NpcGenericActivityIntent.RestAtLodge:
                return NpcSocialAnchorType.Lodge;
            case NpcGenericActivityIntent.VisitMedic:
                return NpcSocialAnchorType.MedicTent;
            case NpcGenericActivityIntent.PracticeTrick:
                return NpcSocialAnchorType.TerrainPark;
            case NpcGenericActivityIntent.ViewpointPause:
                return Random.value < 0.5f ? NpcSocialAnchorType.Viewpoint : NpcSocialAnchorType.RunOverlook;
            default:
                return null;
        }
    }

    private NpcSocialAnchor ChooseAnchorCandidate(NpcSocialAnchorType? requiredType)
    {
        _genericAnchorCandidates.Clear();
        var anchors = NpcSocialDirector.RegisteredAnchors;
        for (int i = 0; i < anchors.Count; i++)
        {
            NpcSocialAnchor anchor = anchors[i];
            if (anchor == null || !anchor.isActiveAndEnabled)
                continue;

            if (requiredType.HasValue && anchor.AnchorType != requiredType.Value)
                continue;

            if (!anchor.HasAvailableCapacity())
                continue;

            _genericAnchorCandidates.Add(anchor);
        }

        if (_genericAnchorCandidates.Count == 0)
            return null;

        NpcSocialActor actor = GetComponent<NpcSocialActor>();
        float total = 0f;
        for (int i = 0; i < _genericAnchorCandidates.Count; i++)
        {
            NpcSocialAnchor anchor = _genericAnchorCandidates[i];
            float distance = Vector3.Distance(transform.position, anchor.Center.position);
            total += anchor.GetCandidateWeightForActor(actor, this) / Mathf.Max(1f, distance / 40f);
        }

        if (total <= 0.001f)
            return null;

        float roll = Random.value * total;
        for (int i = 0; i < _genericAnchorCandidates.Count; i++)
        {
            NpcSocialAnchor anchor = _genericAnchorCandidates[i];
            float distance = Vector3.Distance(transform.position, anchor.Center.position);
            roll -= anchor.GetCandidateWeightForActor(actor, this) / Mathf.Max(1f, distance / 40f);
            if (roll <= 0f)
                return anchor;
        }

        return _genericAnchorCandidates[_genericAnchorCandidates.Count - 1];
    }

    private Vector2 ResolveGenericIntentDurationRange(NpcGenericActivityIntent intent)
    {
        switch (intent)
        {
            case NpcGenericActivityIntent.SocialLoiter:
            case NpcGenericActivityIntent.VisitKiosk:
                return profile != null ? profile.SocialLoiterDurationRange : new Vector2(20f, 90f);
            case NpcGenericActivityIntent.WatchRace:
            case NpcGenericActivityIntent.ViewpointPause:
                return profile != null ? profile.ViewpointPauseDurationRange : new Vector2(8f, 25f);
            case NpcGenericActivityIntent.RestAtLodge:
            case NpcGenericActivityIntent.VisitMedic:
                return profile != null ? profile.LodgeRestDurationRange : new Vector2(30f, 120f);
            case NpcGenericActivityIntent.PracticeTrick:
                return profile != null ? profile.PracticeTrickDurationRange : new Vector2(10f, 40f);
            case NpcGenericActivityIntent.IdleWander:
                return profile != null ? profile.IdleWanderDurationRange : new Vector2(8f, 30f);
            case NpcGenericActivityIntent.RideLift:
            case NpcGenericActivityIntent.QueueAtLift:
                return NormalizeRange(liftIntentDurationRange, 45f, 220f);
            case NpcGenericActivityIntent.TraverseToNearbyArea:
            case NpcGenericActivityIntent.LeaveArea:
                return NormalizeRange(traverseIntentDurationRange, 18f, 70f);
            default:
                return NormalizeRange(skiRunIntentDurationRange, 35f, 160f);
        }
    }

    private Vector3 SampleNearbyFlowTarget()
    {
        Transform focus = ResolvePlayerFocus();
        Vector3 origin = focus != null ? focus.position : transform.position;
        Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(45f, 180f);
        if (offset.sqrMagnitude <= 0.001f)
            offset = Vector2.right * 60f;

        return SnapPointToGround(origin + new Vector3(offset.x, 0f, offset.y), origin.y);
    }

    private string BuildGenericIntentDebugString()
    {
        float elapsed = _currentGenericIntent != NpcGenericActivityIntent.None ? Time.time - _currentIntentStartedAt : 0f;
        float expiresIn = _currentGenericIntent != NpcGenericActivityIntent.None ? Mathf.Max(0f, _currentIntentExpiresAt - Time.time) : 0f;
        string target = _currentIntentTargetAnchor != null
            ? _currentIntentTargetAnchor.DisplayName
            : _currentIntentTargetObject != null ? _currentIntentTargetObject.name : _currentIntentTargetPosition.ToString();

        return
            $"[{nameof(NpcSkierBrain)}] Generic Intent Debug: {name}\n" +
            $"genericAllowed={CanUseGenericLifecycle()} blockedByDefined={_genericBehaviourPausedByDefinedCharacter} current={_currentGenericIntent} previous={_previousGenericIntent}\n" +
            $"state={_state} lowLevelIntent={_intentKind} elapsed={elapsed:0.0}s min={_currentIntentMinDuration:0.0}s max={_currentIntentMaxDuration:0.0}s expiresIn={expiresIn:0.0}s\n" +
            $"target={target} targetPos={_currentIntentTargetPosition} previousAnchor={(_previousAnchor != null ? _previousAnchor.DisplayName : "none")} anchorCooldown={Mathf.Max(0f, _previousAnchorCooldownUntil - Time.time):0.0}s\n" +
            BuildLiftBoardingDebugString() + "\n" +
            $"lastDecision={_lastDecisionReason}";
    }

    private string BuildLiftBoardingDebugString()
    {
        LiftBoardGate gate = _preferredBoardGate != null ? _preferredBoardGate : (_currentLift != null ? ResolvePreferredBoardGate(_currentLift) : null);
        Vector3 stage = _currentLiftStagePoint;
        Vector3 queue = gate != null && liftRider != null ? gate.GetQueueTargetPosition(liftRider) : _currentLiftApproachPoint;
        Vector3 board = gate != null && gate.boardingPoint != null ? gate.boardingPoint.position : queue;

        float stageDist = stage.sqrMagnitude > 0.0001f ? Vector3.Distance(transform.position, stage) : -1f;
        float queueDist = queue.sqrMagnitude > 0.0001f ? Vector3.Distance(transform.position, queue) : -1f;
        float boardDist = board.sqrMagnitude > 0.0001f ? Vector3.Distance(transform.position, board) : -1f;
        bool queued = gate != null && liftRider != null && gate.IsQueued(liftRider);
        int queueIndex = gate != null && liftRider != null ? gate.GetQueueIndex(liftRider) : -1;
        bool inside = gate != null && liftRider != null && gate.IsRiderInsideTrigger(liftRider);
        bool carrierReady = gate != null && liftRider != null && gate.HasCarrierReadyFor(liftRider);

        return
            $"liftPhase={_liftBoardingPhase} lift={(_currentLift != null ? _currentLift.name : "none")} gate={(gate != null ? gate.name : "none")} nearbyGate={(liftRider != null && liftRider.NearbyBoardGate != null ? liftRider.NearbyBoardGate.name : "none")}\n" +
            $"stageDist={stageDist:0.00} queueDist={queueDist:0.00} boardDist={boardDist:0.00} insideTrigger={inside} queued={queued} queueIndex={queueIndex} head={(gate != null && liftRider != null && gate.IsHeadOfQueue(liftRider))}\n" +
            $"attached={(liftRider != null && liftRider.IsAttached)} carrier={(liftRider != null && liftRider.CurrentCarrier != null ? liftRider.CurrentCarrier.name : "none")} carrierReady={carrierReady} retries={_boardingRetryCount} lastBoardingFailure={_lastBoardingFailureReason}";
    }

    private static Vector2 NormalizeRange(Vector2 range, float fallbackMin, float fallbackMax)
    {
        float min = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        if (max <= 0.1f)
            return new Vector2(fallbackMin, Mathf.Max(fallbackMin, fallbackMax));

        return new Vector2(min, max);
    }

    private bool TryBuildHintedIntent()
    {
        switch (_pendingSpawnHint)
        {
            case SpawnContextHint.LiftBottom:
                {
                    LiftLine lift = FindNearestLift(_pendingHintWorldPoint, requireBottom: true);
                    if (lift == null)
                        return false;

                    _currentLift = lift;
                    _preferredBoardGate = ResolvePreferredBoardGate(lift);
                    _liftBoardingPhase = LiftNpcBoardingPhase.MovingToStage;
                    _currentLiftStagePoint = ResolveLiftStagePoint(lift);
                    _currentLiftApproachPoint = _currentLiftStagePoint;
                    _currentRun = ChooseRunNearLiftTop(lift);
                    _liftStageReached = false;
                    _boardingRetryCount = 0;

                    _intentKind = IntentKind.LiftThenRun;

                    if (ShouldUseWalkingSupportTo(_currentLiftApproachPoint) ||
                        Vector3.Distance(transform.position, _currentLiftApproachPoint) > broadLiftApproachRadius)
                        BeginWalkingSupport(_currentLiftApproachPoint);
                    else
                        BeginAmbientSki(_currentLiftApproachPoint);

                    return true;
                }

            case SpawnContextHint.LiftTop:
                {
                    LiftLine lift = FindNearestLift(_pendingHintWorldPoint, requireBottom: false);
                    _currentLift = lift;
                    _preferredBoardGate = null;
                    _liftBoardingPhase = LiftNpcBoardingPhase.Finished;
                    _currentRun = lift != null ? ChooseRunNearLiftTop(lift) : ChooseRunForProfile();
                    if (_currentRun == null)
                        return false;

                    _currentBroadTarget = GetBroadRunTarget(_currentRun);
                    _intentKind = IntentKind.DirectRun;
                    BeginAmbientSki(_currentBroadTarget);
                    _postLiftRunJoinUntil = Time.time + ScaleFastForwardDelay(postLiftRunJoinGraceSeconds);
                    return true;
                }

            case SpawnContextHint.Run:
                {
                    _currentRun = FindNearestRun(_pendingHintWorldPoint);
                    if (_currentRun == null)
                        return false;

                    _currentBroadTarget = GetBroadRunTarget(_currentRun);
                    _intentKind = IntentKind.DirectRun;
                    BeginRunSki(_currentRun);
                    return true;
                }
        }

        return false;
    }

    private void BeginAmbientSki(Vector3 target)
    {
        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        walkingController?.ForceEnterSkiMode();

        locomotion?.BeginFreeSki(target);
        locomotion?.ClearRecoveryRequests();

        _state = BrainState.SkiingAmbient;
    }

    private void BeginRunSki(SkiRunLine run)
    {
        _currentRun = run;
        RememberRun(run);

        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        walkingController?.ForceEnterSkiMode();

        locomotion?.SetRun(run);
        locomotion?.ClearRecoveryRequests();

        _lastRun = run;
        _state = BrainState.SkiingRun;
    }

    private void BeginWalkingSupport(Vector3 target)
    {
        walkingController?.SetWalkPresentationKeepsSkisEquipped(false);
        walkingController?.ForceEnterWalkMode();
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();
        pathAgent?.SetDestination(target);

        _currentBroadTarget = target;
        _state = BrainState.WalkingSupport;
        _walkingRecoveryUntil = Time.time + ScaleFastForwardDelay(preferredWalkingRecoverySeconds);
        StabilizeWalkPosture();
    }

    private bool TryStartHubLoiter()
    {
        if (_hubAnchors.Count == 0 || Random.value > hubLoiterChance)
            return false;

        Transform nearestHub = ResolvePreferredHubAnchor();
        if (nearestHub == null)
            return false;

        return StartHubLoiterFromAnchor(nearestHub);
    }

    private bool StartHubLoiterFromAnchor(Transform hubAnchor)
    {
        if (hubAnchor == null)
            return false;

        _currentHubAnchor = hubAnchor;
        _intentKind = IntentKind.HubLoiter;
        _currentRun = null;
        _currentLift = null;
        Vector3 target = SampleHubLoiterPoint(hubAnchor);

        if (Vector3.Distance(transform.position, target) < hubMinShuffleDistance * 0.65f)
        {
            EnterWaiting(Random.Range(hubPauseMin * 0.5f, hubPauseMax * 0.75f));
            return true;
        }

        if (ShouldUseWalkingSupportTo(target) || Vector3.Distance(transform.position, target) > 6f)
            BeginWalkingSupport(target);
        else
            BeginAmbientSki(target);

        return true;
    }

    private void TickSkiingAmbient()
    {
        if (locomotion == null)
        {
            EnterWaiting(Random.Range(waitMin, waitMax));
            return;
        }

        if (_intentKind == IntentKind.LiftThenRun && _currentLift != null)
        {
            if (!_liftStageReached)
            {
                _currentLiftStagePoint = ResolveLiftStagePoint(_currentLift);
                _currentLiftApproachPoint = _currentLiftStagePoint;
                locomotion.UpdateBroadTarget(_currentLiftApproachPoint);

                float stageDistance = Vector3.Distance(transform.position, _currentLiftStagePoint);
                if (stageDistance <= liftStageArrivalDistance)
                {
                    _liftStageReached = true;
                    _liftBoardingPhase = LiftNpcBoardingPhase.MovingToQueueEntry;
                    _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);
                }
            }
            else
            {
                _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);
                locomotion.UpdateBroadTarget(_currentLiftApproachPoint);
            }

            float distToLift = Vector3.Distance(transform.position, _currentLiftApproachPoint);
            if (liftRider != null &&
                !liftRider.IsAttached &&
                _liftStageReached &&
                distToLift <= liftBoardCommitDistance &&
                IsLiftBoardingReady())
            {
                EnterBoardingLift();
                return;
            }

            if (distToLift > abandonLiftDistance && ShouldUseWalkingSupportTo(_currentLiftApproachPoint))
            {
                BeginWalkingSupport(_currentLiftApproachPoint);
                return;
            }
        }
        else
        {
            locomotion.UpdateBroadTarget(_currentBroadTarget);
        }

        if (_currentRun != null)
        {
            float distToRun = DistanceToRun(_currentRun, transform.position);

            if (distToRun <= directRunJoinRadius || Time.time <= _postLiftRunJoinUntil)
            {
                BeginRunSki(_currentRun);
                return;
            }
        }

        if (locomotion.WantsRespawn)
        {
            TryRespawnOntoIntent();
            return;
        }

        if (locomotion.WantsWalkingRecovery)
            BeginWalkingSupport(locomotion.SuggestedRecoveryPoint);
    }

    private void TickSkiingRun()
    {
        if (locomotion == null)
        {
            EnterWaiting(Random.Range(waitMin, waitMax));
            return;
        }

        if (locomotion.WantsRespawn)
        {
            TryRespawnOntoIntent();
            return;
        }

        if (locomotion.WantsWalkingRecovery)
        {
            BeginWalkingSupport(locomotion.SuggestedRecoveryPoint);
            return;
        }

        if (locomotion.HasCompletedRun)
        {
            float minPause = profile != null ? profile.DesiredRestSecondsMin : waitMin;
            float maxPause = profile != null ? profile.DesiredRestSecondsMax : waitMax;

            if (profile != null)
            {
                minPause += profile.ScenicPauseBias01 * 0.6f;
                maxPause += profile.ScenicPauseBias01 * 1.5f;
            }

            ClearGenericIntent("Completed ski run", rememberPrevious: true);
            EnterWaiting(Random.Range(minPause, maxPause));
        }
    }

    private void TickWalkingSupport()
    {
        StabilizeWalkPosture();

        if (pathAgent != null && pathAgent.IsStuck)
        {
            HandleMobilityFailure(pathAgent.Destination);
            return;
        }

        Vector3 target = _currentBroadTarget;
        if (_intentKind == IntentKind.LiftThenRun && _currentLift != null)
        {
            if (!_liftStageReached)
            {
                _currentLiftStagePoint = ResolveLiftStagePoint(_currentLift);
                _currentLiftApproachPoint = _currentLiftStagePoint;
                _liftBoardingPhase = LiftNpcBoardingPhase.MovingToStage;
            }
            else
            {
                _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);
                _liftBoardingPhase = LiftNpcBoardingPhase.MovingToQueueEntry;
            }

            target = _currentLiftApproachPoint;
            pathAgent?.SetDestination(target);
        }

        float distance = Vector3.Distance(transform.position, target);
        if (distance <= walkArrivalDistance || (pathAgent != null && pathAgent.ReachedDestination))
        {
            if (_intentKind == IntentKind.HubLoiter)
            {
                EnterWaiting(Random.Range(hubPauseMin, hubPauseMax));
            }
            else if (_intentKind == IntentKind.LiftThenRun && _currentLift != null)
            {
                if (!_liftStageReached)
                {
                    _liftStageReached = true;
                    _liftBoardingPhase = LiftNpcBoardingPhase.MovingToQueueEntry;
                    _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);
                    BeginWalkingSupport(_currentLiftApproachPoint);
                }
                else if (liftRider != null && (liftRider.NearbyBoardGate != null || IsLiftBoardingReady()))
                {
                    EnterBoardingLift();
                }
                else
                {
                    _currentLiftApproachPoint = ResolveLiftRetryPoint(_currentLift, Mathf.Max(1, _boardingRetryCount + 1));
                    BeginWalkingSupport(_currentLiftApproachPoint);
                }
            }
            else if (_currentRun != null)
            {
                BeginRunSki(_currentRun);
            }
            else
            {
                BeginAmbientSki(_currentBroadTarget);
            }
        }
    }

    private void EnterBoardingLift()
    {
        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        walkingController?.ForceEnterSkiMode();
        locomotion?.SetInputEnabled(false);

        _boardingAbortTime = Time.time + ScaleFastForwardDelay(boardingTimeout);
        _nextLiftInputTime = 0f;
        _liftBoardingPhase = LiftNpcBoardingPhase.JoiningQueue;
        _state = BrainState.BoardingLift;
    }

    private void TickBoardingLift()
    {
        if (_currentLift == null || liftRider == null)
        {
            EnterWaiting(Random.Range(waitMin, waitMax));
            return;
        }

        if (liftRider.IsAttached)
        {
            liftRider.SetLiftInput(false, false);
            _state = BrainState.RidingLift;
            return;
        }

        LiftBoardGate preferredGate = ResolvePreferredBoardGate(_currentLift);
        _preferredBoardGate = preferredGate;
        LiftBoardGate gate = liftRider.NearbyBoardGate != null ? liftRider.NearbyBoardGate : preferredGate;
        if (gate == null)
        {
            _lastBoardingFailureReason = "No preferred or nearby lift gate";
            HandleBoardingFailure();
            return;
        }

        _currentLiftApproachPoint = gate.IsQueued(liftRider)
            ? gate.GetQueueTargetPosition(liftRider)
            : gate.GetNpcEntryPosition();
        bool inGateTrigger = gate.IsRiderInsideTrigger(liftRider) || liftRider.NearbyBoardGate == gate;
        bool isQueued = gate.IsQueued(liftRider);
        float distance = Vector3.Distance(transform.position, _currentLiftApproachPoint);
        float gateDistance = gate.DistanceToBoardingOrQueueTarget(liftRider);

        if (!isQueued && !inGateTrigger && gateDistance > gate.NpcQueueJoinRadius)
        {
            _liftBoardingPhase = LiftNpcBoardingPhase.MovingToQueueEntry;
            MoveTowardLiftBoardingPoint(_currentLiftApproachPoint);
            return;
        }

        if (!isQueued)
        {
            _liftBoardingPhase = LiftNpcBoardingPhase.JoiningQueue;
            isQueued = gate.TryJoinQueue(liftRider);
            if (!isQueued && (gateDistance > Mathf.Max(walkArrivalDistance, gate.NpcQueueJoinRadius) || !IsWithinGateCatchArea(gate)))
            {
                _lastBoardingFailureReason = $"TryJoinQueue failed. inTrigger={inGateTrigger} gateDistance={gateDistance:0.00}";
                MoveTowardLiftBoardingPoint(_currentLiftApproachPoint);
                return;
            }
        }

        _currentLiftApproachPoint = gate.GetQueueTargetPosition(liftRider);
        distance = Vector3.Distance(transform.position, _currentLiftApproachPoint);

        if (isQueued)
            _liftBoardingPhase = gate.IsHeadOfQueue(liftRider) ? LiftNpcBoardingPhase.BoardingCarrier : LiftNpcBoardingPhase.WaitingInQueue;

        float queueArrivalDistance = gate.IsHeadOfQueue(liftRider)
            ? Mathf.Max(walkArrivalDistance, liftBoardCommitDistance)
            : walkArrivalDistance;
        if (distance > queueArrivalDistance)
        {
            MoveTowardLiftBoardingPoint(_currentLiftApproachPoint);
            return;
        }

        if (isQueued)
            _boardingAbortTime = Mathf.Max(_boardingAbortTime, Time.time + ScaleFastForwardDelay(1.25f));

        if (Time.time >= _boardingAbortTime)
        {
            _lastBoardingFailureReason = $"Boarding timeout. queued={isQueued} head={gate.IsHeadOfQueue(liftRider)} carrierReady={gate.HasCarrierReadyFor(liftRider)} dist={distance:0.00} gateDist={gateDistance:0.00}";
            HandleBoardingFailure();
            return;
        }

        if (!liftRider.IsNpcRider() && Time.time >= _nextLiftInputTime)
        {
            _nextLiftInputTime = Time.time + ScaleFastForwardDelay(0.15f);
            liftRider.SetLiftInput(true, true);
        }
    }

    private void MoveTowardLiftBoardingPoint(Vector3 target)
    {
        if (pathAgent != null && pathAgent.HasDestination && Vector3.Distance(pathAgent.Destination, target) <= 0.25f)
            return;

        BeginWalkingSupport(target);
    }

    private void TickRidingLift()
    {
        if (_currentLift == null || _currentLift.topStation == null || liftRider == null)
        {
            EnterWaiting(Random.Range(waitMin, waitMax));
            return;
        }

        float distToTop = Vector3.Distance(transform.position, _currentLift.topStation.position);

        if (liftRider.IsAttached)
        {
            _liftBoardingPhase = LiftNpcBoardingPhase.Riding;
            bool chairMode = _currentLift.carrierPrefab != null &&
                             _currentLift.carrierPrefab.mode == LiftCarrierMode.Chair;

            if (distToTop <= liftDetachDistance)
            {
                if (chairMode) liftRider.SetLiftInput(true, true);
                else liftRider.SetLiftInput(false, false);
            }
            else
            {
                if (!chairMode) liftRider.SetLiftInput(true, false);
                else liftRider.SetLiftInput(false, false);
            }

            return;
        }

        _postLiftRunJoinUntil = Time.time + postLiftRunJoinGraceSeconds;
        _liftBoardingPhase = LiftNpcBoardingPhase.Finished;
        _lastBoardingFailureReason = "Lift ride completed";

        if (_currentRun != null)
            BeginAmbientSki(GetBroadRunTarget(_currentRun));
        else
        {
            if (_currentGenericIntent == NpcGenericActivityIntent.RideLift ||
                _currentGenericIntent == NpcGenericActivityIntent.QueueAtLift)
            {
                CompleteCurrentIntent("Rode lift");
                return;
            }

            BeginAmbientSki(_currentBroadTarget);
        }
    }

    private void HandleStacked(SkiController.StackEventInfo info)
    {
        if (!_initialized || skiController == null)
            return;

        if (_spectatorCrowdActive || _socialLoiterActive)
        {
            ResumeFromSpectatorCrowd(immediateIntent: false);
        }

        locomotion?.SetInputEnabled(false);
        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        locomotion?.SuppressRespawnRequests(ScaleFastForwardDelay(disturbedRespawnSuppressSeconds));
        _respawnSuppressedUntil = Time.time + ScaleFastForwardDelay(disturbedRespawnSuppressSeconds);

        _state = BrainState.Recovering;
        _stateUntil = Time.time + ScaleFastForwardDelay(profile != null ? profile.RecoveryDelaySeconds : 2f);
    }

    private void TickRecovering()
    {
        if (Time.time < _stateUntil)
            return;

        Vector3 forwardHint = skiController != null ? skiController.SkiForwardOnPlane : transform.forward;
        skiController?.RecoverFromStack(forwardHint);

        if (_currentRun != null)
            BeginAmbientSki(GetBroadRunTarget(_currentRun));
        else
            BeginAmbientSki(_currentBroadTarget);
    }

    private void TryRespawnOntoIntent()
    {
        if (Time.time < _nextRespawnAllowedTime || skiController == null || locomotion == null)
            return;

        if (!CanUseHardRespawn())
        {
            HandleMobilityFailure(locomotion.SuggestedRecoveryPoint);
            return;
        }

        if (Time.time < _respawnSuppressedUntil)
        {
            Vector3 recoveryPoint = locomotion.SuggestedRecoveryPoint;
            if ((recoveryPoint - Vector3.zero).sqrMagnitude > 0.0001f)
            {
                HandleMobilityFailure(recoveryPoint);
            }
            return;
        }

        if (_state == BrainState.WalkingSupport && Time.time < _walkingRecoveryUntil)
        {
            _nextRespawnAllowedTime = Time.time + ScaleFastForwardDelay(0.5f);
            return;
        }

        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        walkingController?.ForceEnterSkiMode();

        Vector3 point = locomotion.SuggestedRespawnPoint;
        Quaternion rot = locomotion.SuggestedRespawnRotation;

        if (_currentRun != null && (point - Vector3.zero).sqrMagnitude < 0.0001f)
        {
            point = GetBroadRunTarget(_currentRun) + Vector3.up * 0.35f;
            rot = Quaternion.LookRotation(GetRunForward(_currentRun), Vector3.up);
        }

        skiController.TeleportToSpawn(point, rot, snapToGround: true);
        sorenessMeter?.ResetSoreness();
        locomotion.SuppressRespawnRequests(ScaleFastForwardDelay(2f));

        if (_intentKind == IntentKind.LiftThenRun && _currentLift != null)
            BeginAmbientSki(_currentLiftApproachPoint);
        else if (_currentRun != null)
            BeginAmbientSki(GetBroadRunTarget(_currentRun));
        else
            BeginAmbientSki(_currentBroadTarget);

        _nextRespawnAllowedTime = Time.time + ScaleFastForwardDelay(Mathf.Max(respawnCooldown, hardRespawnRetryDelay));
    }

    private bool CanUseHardRespawn()
    {
        Transform focus = ResolvePlayerFocus();
        if (focus == null)
            return true;

        return Vector3.Distance(transform.position, focus.position) >= hardRespawnOnlyBeyondPlayerDistance;
    }

    private Transform ResolvePlayerFocus()
    {
        if (playerFocus != null)
            return playerFocus;

        if (Camera.main != null)
            playerFocus = Camera.main.transform;

        return playerFocus;
    }

    private void HandleMobilityFailure(Vector3 recoveryPoint)
    {
        if (locomotion != null)
            locomotion.SuppressRespawnRequests(ScaleFastForwardDelay(disturbedRespawnSuppressSeconds));

        _respawnSuppressedUntil = Time.time + ScaleFastForwardDelay(disturbedRespawnSuppressSeconds);
        _nextRespawnAllowedTime = Time.time + ScaleFastForwardDelay(1f);

        if (ShouldUseSoftWalkFailureRecovery())
        {
            pathAgent?.Stop();
            walkingController?.ClearExternalMove();
            walkingController?.SetWalkPresentationKeepsSkisEquipped(false);
            StabilizeWalkPosture(forceSnap: true);

            if ((recoveryPoint - Vector3.zero).sqrMagnitude > 0.0001f && _state != BrainState.Waiting)
            {
                BeginWalkingSupport(recoveryPoint);
                return;
            }

            EnterWaiting(Random.Range(softWalkFailurePauseMin, softWalkFailurePauseMax));
            return;
        }

        if (skiController != null && !skiController.IsStacked)
        {
            Vector3 incoming = locomotion != null
                ? -locomotion.transform.forward
                : -transform.forward;
            skiController.TriggerImpactStackFromPoint(transform.position - incoming.normalized, incoming, localFailureRagdollSeverity, "NpcMobilityFailure");
            _state = BrainState.Recovering;
            _stateUntil = Time.time + ScaleFastForwardDelay(localFailureRecoveryDelay);
            return;
        }

        if ((recoveryPoint - Vector3.zero).sqrMagnitude > 0.0001f)
        {
            BeginWalkingSupport(recoveryPoint);
            return;
        }

        EnterWaiting(Random.Range(waitMin, waitMax));
    }

    private bool ShouldUseSoftWalkFailureRecovery()
    {
        return _spectatorCrowdActive ||
               _state == BrainState.WalkingSupport ||
               _intentKind == IntentKind.HubLoiter;
    }

    private bool ShouldKeepSkisEquippedForSpectator()
    {
        if (skiController == null)
            return false;

        float slopeDeg = Vector3.Angle(
            skiController.GroundNormal.sqrMagnitude > 0.0001f ? skiController.GroundNormal.normalized : Vector3.up,
            Vector3.up);
        float planarSpeed = body != null ? Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude : 0f;
        float moveDist = Vector3.ProjectOnPlane(_spectatorMoveTarget - transform.position, Vector3.up).magnitude;

        return slopeDeg <= spectatorKeepSkisMaxSlopeDeg &&
               planarSpeed <= 0.35f &&
               moveDist <= 0.65f;
    }

    private void StabilizeWalkPosture(bool forceSnap = false)
    {
        if (body == null || body.isKinematic)
            return;

        if (skiController != null && skiController.IsStacked)
            return;

        Vector3 forwardPlanar = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forwardPlanar.sqrMagnitude <= 0.0001f)
            forwardPlanar = Vector3.forward;

        Quaternion uprightRotation = Quaternion.LookRotation(forwardPlanar.normalized, Vector3.up);
        float tilt = Quaternion.Angle(transform.rotation, uprightRotation);
        float lerpT = forceSnap || tilt >= walkSnapTiltDegrees
            ? 1f
            : Mathf.Clamp01(Time.deltaTime * walkUprightLerpSpeed);

        Quaternion corrected = Quaternion.Slerp(transform.rotation, uprightRotation, lerpT);
        transform.rotation = corrected;
        body.angularVelocity = Vector3.zero;
    }

    private bool IsLiftBoardingReady()
    {
        if (_currentLift == null || liftRider == null)
            return false;

        if (liftRider.NearbyBoardGate != null)
            return true;

        LiftBoardGate gate = ResolvePreferredBoardGate(_currentLift);
        if (gate == null)
            return false;

        Vector3 queueTarget = gate.GetQueueTargetPosition(liftRider);
        return Vector3.Distance(transform.position, queueTarget) <= Mathf.Max(liftBoardCommitDistance, walkArrivalDistance + 0.35f, gate.NpcQueueJoinRadius) ||
               gate.DistanceToBoardingOrQueueTarget(liftRider) <= gate.NpcQueueJoinRadius ||
               IsWithinGateCatchArea(gate);
    }

    private bool IsWithinGateCatchArea(LiftBoardGate gate)
    {
        if (gate == null)
            return false;

        Vector3 gatePos = gate.boardingPoint != null ? gate.boardingPoint.position : gate.transform.position;
        return Vector3.Distance(transform.position, gatePos) <= Mathf.Max(liftBoardDistance + liftQueueTriggerSlack, gate.carrierCatchRadius + 1.2f + liftQueueTriggerSlack);
    }

    private void HandleBoardingFailure()
    {
        if (liftRider != null && liftRider.NearbyBoardGate != null)
            liftRider.NearbyBoardGate.LeaveQueue(liftRider);

        if (_boardingRetryCount < maxBoardingRetries)
        {
            _boardingRetryCount++;
            _liftStageReached = true;
            _liftBoardingPhase = LiftNpcBoardingPhase.MovingToQueueEntry;
            _currentLiftApproachPoint = ResolveLiftRetryPoint(_currentLift, _boardingRetryCount);
            BeginWalkingSupport(_currentLiftApproachPoint);
            return;
        }

        _boardingRetryCount = 0;
        _liftStageReached = false;
        _liftBoardingPhase = LiftNpcBoardingPhase.None;

        if (_currentLift != null && _currentLift.bottomStation != null)
            _currentHubAnchor = _currentLift.bottomStation;

        if (_currentGenericIntent == NpcGenericActivityIntent.RideLift ||
            _currentGenericIntent == NpcGenericActivityIntent.QueueAtLift)
        {
            ExpireCurrentIntent($"Lift boarding failed: {_lastBoardingFailureReason}");
            return;
        }

        EnterWaiting(Random.Range(hubPauseMin, hubPauseMax));
    }

    private bool ShouldUseWalkingSupportTo(Vector3 target)
    {
        Vector3 toTarget = target - transform.position;
        float planarDist = Vector3.ProjectOnPlane(toTarget, Vector3.up).magnitude;
        float verticalGain = target.y - transform.position.y;

        if (planarDist > walkingSupportMaxDistance)
            return false;

        return verticalGain > 1.5f;
    }

    private bool CanJoinRunDirectly(SkiRunLine run)
    {
        Vector3 broadTarget = GetBroadRunTarget(run);
        Vector3 toTarget = broadTarget - transform.position;

        float planarDist = Vector3.ProjectOnPlane(toTarget, Vector3.up).magnitude;
        float verticalDrop = transform.position.y - broadTarget.y;

        if (planarDist > directRunJoinRadius)
            return false;

        return verticalDrop > 1f;
    }

    private bool ShouldPreferLiftForRun(LiftLine lift, Vector3 runTarget)
    {
        if (lift == null || lift.topStation == null || lift.bottomStation == null)
            return false;

        float verticalGain = lift.topStation.position.y - transform.position.y;
        if (verticalGain < minimumLiftVerticalGain)
            return false;

        float topToRun = Vector3.Distance(lift.topStation.position, runTarget);
        return topToRun <= maxLiftTopToRunTargetDistance;
    }

    private float DistanceToRun(SkiRunLine run, Vector3 worldPos)
    {
        if (run == null)
            return float.PositiveInfinity;

        if (!run.TryGetClosestPointOnCenterlineXZ(worldPos, out _, out float distXZ, out _, out _))
            return float.PositiveInfinity;

        return distXZ;
    }

    private SkiRunLine ChooseRunForProfile()
    {
        if (availableRuns == null || availableRuns.Count == 0)
            return null;
        return ChooseBestRunFromContext(transform.position, null);
    }

    private LiftLine ChooseLiftForRun(SkiRunLine run, Vector3 runTarget)
    {
        if (availableLifts == null || availableLifts.Count == 0)
            return null;

        LiftLine best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < availableLifts.Count; i++)
        {
            LiftLine lift = availableLifts[i];
            if (lift == null || lift.topStation == null || lift.bottomStation == null)
                continue;

            float topToRun = Vector3.Distance(lift.topStation.position, runTarget);
            float bottomBias = Vector3.Distance(transform.position, lift.bottomStation.position) * 0.2f;
            float score = topToRun + bottomBias;

            if (score < bestScore)
            {
                bestScore = score;
                best = lift;
            }
        }

        return best;
    }

    private LiftLine FindNearestLift(Vector3 point, bool requireBottom)
    {
        LiftLine best = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < availableLifts.Count; i++)
        {
            LiftLine lift = availableLifts[i];
            if (lift == null) continue;

            Transform station = requireBottom ? lift.bottomStation : lift.topStation;
            if (station == null) continue;

            float d = Vector3.Distance(point, station.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = lift;
            }
        }

        return best;
    }

    private SkiRunLine FindNearestRun(Vector3 point)
    {
        SkiRunLine best = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < availableRuns.Count; i++)
        {
            SkiRunLine run = availableRuns[i];
            if (run == null) continue;

            if (!run.TryGetClosestPointOnCenterlineXZ(point, out _, out float distXZ, out _, out _))
                continue;

            if (distXZ < bestDist)
            {
                bestDist = distXZ;
                best = run;
            }
        }

        return best;
    }

    private SkiRunLine ChooseRunNearLiftTop(LiftLine lift)
    {
        if (lift == null || lift.topStation == null)
            return ChooseRunForProfile();

        return ChooseBestRunFromContext(lift.topStation.position, lift);
    }

    private SkiRunLine ChooseBestRunFromContext(Vector3 origin, LiftLine preferredLift)
    {
        List<SkiRunLine> preferred = new();
        List<SkiRunLine> fallback = new();

        for (int i = 0; i < availableRuns.Count; i++)
        {
            SkiRunLine run = availableRuns[i];
            if (run == null)
                continue;

            fallback.Add(run);
            if (profile == null || profile.PrefersDifficulty(run.Difficulty))
                preferred.Add(run);
        }

        List<SkiRunLine> pool = preferred.Count > 0 ? preferred : fallback;
        if (pool.Count == 0)
            return null;

        if (_lastRun != null &&
            pool.Contains(_lastRun) &&
            profile != null &&
            Random.value <= profile.RepeatRunBias01)
        {
            return _lastRun;
        }

        SkiRunLine best = null;
        SkiRunLine second = null;
        float bestScore = float.NegativeInfinity;
        float secondScore = float.NegativeInfinity;

        for (int i = 0; i < pool.Count; i++)
        {
            SkiRunLine run = pool[i];
            float score = ScoreRun(run, origin, preferredLift);
            if (score > bestScore)
            {
                second = best;
                secondScore = bestScore;
                best = run;
                bestScore = score;
            }
            else if (score > secondScore)
            {
                second = run;
                secondScore = score;
            }
        }

        if (best == null)
            return pool[Random.Range(0, pool.Count)];

        if (second != null)
        {
            float randomness = profile != null ? Mathf.Lerp(0.08f, 0.3f, profile.LineVariation01) : 0.12f;
            if (Random.value <= randomness)
                return second;
        }

        return best;
    }

    private float ScoreRun(SkiRunLine run, Vector3 origin, LiftLine preferredLift)
    {
        if (run == null)
            return float.NegativeInfinity;

        Vector3 target = GetBroadRunTarget(run);
        float score = 0f;

        if (profile != null)
        {
            int difficultyDelta = Mathf.Abs((int)run.Difficulty - (int)profile.MaxPreferredDifficulty);
            if (profile.PrefersDifficulty(run.Difficulty))
            {
                score += preferredDifficultyScore * 1.1f;
            }
            else
            {
                score -= preferredDifficultyScore * Mathf.Lerp(0.45f, 1.1f, Mathf.Clamp01(difficultyDelta / 3f));
            }
        }

        float planarDistance = Vector3.ProjectOnPlane(target - origin, Vector3.up).magnitude;
        float normalizedDistance = Mathf.Clamp01(planarDistance / Mathf.Max(25f, maxSpawnDistanceFromBrain()));
        score += Mathf.Lerp(travelDistanceScore, -travelDistanceScore, normalizedDistance);

        float drop = Mathf.Max(0f, origin.y - target.y);
        score += Mathf.Clamp01(drop / 120f) * elevationDropScore;

        float length = run.LengthMeters > 0.001f ? run.LengthMeters : run.GetTotalLengthMeters();
        score += Mathf.Clamp01(length / 260f) * scenicLengthScore * GetScenicBias();

        if (_recentRuns.Contains(run))
        {
            int recencyIndex = _recentRuns.LastIndexOf(run);
            float penalty01 = 1f - Mathf.Clamp01(recencyIndex / Mathf.Max(1f, recentRunMemory));
            score -= repeatPenaltyScore * penalty01;
        }
        else
        {
            score += varietyScore * Mathf.Lerp(0.25f, 1f, profile != null ? profile.RoamResortBias01 : 0.35f);
        }

        if (_lastRun == run)
        {
            float repeatBias = profile != null ? profile.RepeatRunBias01 : 0.15f;
            score += Mathf.Lerp(-repeatPenaltyScore, repeatPenaltyScore * 0.35f, repeatBias);
        }

        LiftLine accessLift = preferredLift != null ? preferredLift : ChooseLiftForRun(run, target);
        if (accessLift != null && accessLift.topStation != null && accessLift.bottomStation != null)
        {
            float liftApproach = Vector3.Distance(origin, accessLift.bottomStation.position);
            float liftTopToRun = Vector3.Distance(accessLift.topStation.position, target);
            float liftAccess01 = 1f - Mathf.Clamp01((liftApproach * 0.18f + liftTopToRun) / Mathf.Max(50f, maxLiftTopToRunTargetDistance * 1.4f));
            score += liftAccess01 * liftAccessScore * GetLiftLapBias();
        }

        if (profile != null)
        {
            switch (profile.Archetype)
            {
                case NpcSkierProfile.SkierArchetype.BeginnerCruiser:
                    score += run.Difficulty == SkiRunDifficulty.Green ? 0.9f : 0f;
                    break;
                case NpcSkierProfile.SkierArchetype.ScenicWanderer:
                    score += Mathf.Clamp01((target.y - transform.position.y + 30f) / 180f) * 0.4f;
                    break;
                case NpcSkierProfile.SkierArchetype.AggressiveLocal:
                    score += (run.Difficulty >= SkiRunDifficulty.Red ? 0.55f : 0f);
                    break;
                case NpcSkierProfile.SkierArchetype.LiftLapper:
                    if (_lastRun != null && run.Difficulty == _lastRun.Difficulty)
                        score += 0.25f;
                    break;
            }
        }

        return score + Random.Range(-0.15f, 0.15f);
    }

    private void RememberRun(SkiRunLine run)
    {
        if (run == null)
            return;

        _recentRuns.Add(run);
        int maxHistory = Mathf.Max(1, recentRunMemory);
        while (_recentRuns.Count > maxHistory)
            _recentRuns.RemoveAt(0);
    }

    private float GetLiftLapBias()
    {
        if (profile == null)
            return 0.5f;

        return profile.Archetype == NpcSkierProfile.SkierArchetype.LiftLapper
            ? 1.25f
            : Mathf.Lerp(0.55f, 1f, 1f - profile.RoamResortBias01);
    }

    private float GetScenicBias()
    {
        if (profile == null)
            return 0.4f;

        return profile.Archetype == NpcSkierProfile.SkierArchetype.ScenicWanderer
            ? 1.35f
            : Mathf.Lerp(0.45f, 1f, profile.ScenicPauseBias01);
    }

    private float maxSpawnDistanceFromBrain()
    {
        return Mathf.Max(directRunJoinRadius * 2f, maxLiftTopToRunTargetDistance, 180f);
    }

    private void CacheHubAnchors()
    {
        _hubAnchors.Clear();

        SkiResortZone[] resorts = FindObjectsOfType<SkiResortZone>(includeInactive: false);
        for (int i = 0; i < resorts.Length; i++)
        {
            SkiResortZone resort = resorts[i];
            AddHubAnchor(resort != null && resort.entrancePoint != null ? resort.entrancePoint : resort != null ? resort.transform : null);
        }

        SkiPassKiosk[] kiosks = FindObjectsOfType<SkiPassKiosk>(includeInactive: false);
        for (int i = 0; i < kiosks.Length; i++)
            AddHubAnchor(kiosks[i] != null ? kiosks[i].transform : null);

        CustomizationPortal[] portals = FindObjectsOfType<CustomizationPortal>(includeInactive: false);
        for (int i = 0; i < portals.Length; i++)
            AddHubAnchor(portals[i] != null ? portals[i].transform : null);

        CustomizationShopEntryPoint[] shopEntries = FindObjectsOfType<CustomizationShopEntryPoint>(includeInactive: false);
        for (int i = 0; i < shopEntries.Length; i++)
            AddHubAnchor(shopEntries[i] != null ? shopEntries[i].transform : null);

        for (int i = 0; i < availableLifts.Count; i++)
        {
            LiftLine lift = availableLifts[i];
            if (lift == null)
                continue;

            AddHubAnchor(lift.bottomStation);
        }
    }

    private void AddHubAnchor(Transform anchor)
    {
        if (anchor == null || _hubAnchors.Contains(anchor))
            return;

        _hubAnchors.Add(anchor);
    }

    private Transform ResolvePreferredHubAnchor()
    {
        if (_currentHubAnchor != null && Vector3.Distance(transform.position, _currentHubAnchor.position) <= hubDetectRadius * 1.25f)
            return _currentHubAnchor;

        return FindNearestHub(transform.position, hubDetectRadius);
    }

    private Transform FindNearestHub(Vector3 worldPos, float maxDistance)
    {
        Transform best = null;
        float bestSq = maxDistance * maxDistance;

        for (int i = 0; i < _hubAnchors.Count; i++)
        {
            Transform anchor = _hubAnchors[i];
            if (anchor == null)
                continue;

            float sq = (anchor.position - worldPos).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = anchor;
            }
        }

        return best;
    }

    private Vector3 SampleHubLoiterPoint(Transform hubAnchor)
    {
        if (hubAnchor == null)
            return transform.position;

        Vector2 circle = Random.insideUnitCircle;
        if (circle.sqrMagnitude < 0.0001f)
            circle = Vector2.right;

        circle.Normalize();
        float radius = Random.Range(hubMinShuffleDistance, Mathf.Max(hubMinShuffleDistance + 0.1f, hubWanderRadius));
        Vector3 candidate = hubAnchor.position + new Vector3(circle.x, 0f, circle.y) * radius;
        return SnapPointToGround(candidate, hubAnchor.position.y);
    }

    private void UpdateIdleFacing()
    {
        Transform hub = ResolvePreferredHubAnchor();
        Vector3 lookPoint;

        if (hub != null && Random.value <= 0.65f)
        {
            lookPoint = hub.position;
        }
        else
        {
            Vector3 forward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;
            float yaw = Random.Range(-70f, 70f);
            lookPoint = transform.position + Quaternion.Euler(0f, yaw, 0f) * forward * Random.Range(3f, 7f);
        }

        Vector3 flatDir = Vector3.ProjectOnPlane(lookPoint - transform.position, Vector3.up);
        if (flatDir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.8f);
    }

    private void UpdateSpectatorFacing()
    {
        Vector3 desiredForward = _spectatorForward;
        if (Random.value < 0.35f)
            desiredForward = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * _spectatorForward;

        desiredForward = Vector3.ProjectOnPlane(desiredForward, Vector3.up);
        if (desiredForward.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(desiredForward.normalized, Vector3.up),
            0.85f);
    }

    private Vector3 SampleSpectatorMoveTarget()
    {
        Vector2 offset2 = Random.insideUnitCircle * spectatorShuffleRadius;
        Vector3 candidate = _spectatorAnchorPosition + new Vector3(offset2.x, 0f, offset2.y);
        return SnapPointToGround(candidate, _spectatorAnchorPosition.y);
    }

    private Vector3 ResolveLiftStagePoint(LiftLine lift)
    {
        LiftBoardGate gate = ResolvePreferredBoardGate(lift);
        if (gate != null)
        {
            Transform boardPoint = gate.boardingPoint != null ? gate.boardingPoint : gate.transform;
            Vector3 forward = Vector3.ProjectOnPlane(gate.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            Vector3 lateral = Vector3.Cross(Vector3.up, forward) * Random.Range(-0.6f, 0.6f);
            Vector3 staged = boardPoint.position - forward * liftStageDistance + lateral;
            return SnapPointToGround(staged, boardPoint.position.y);
        }

        Vector3 fallback = ResolveLiftApproachPoint(lift);
        Vector3 away = Vector3.ProjectOnPlane(fallback - transform.position, Vector3.up).normalized;
        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;

        return SnapPointToGround(fallback - away * liftStageDistance, fallback.y);
    }

    private Vector3 ResolveLiftRetryPoint(LiftLine lift, int retryIndex)
    {
        LiftBoardGate gate = ResolvePreferredBoardGate(lift);
        if (gate == null)
            return ResolveLiftStagePoint(lift);

        Transform boardPoint = gate.boardingPoint != null ? gate.boardingPoint : gate.transform;
        Vector3 forward = Vector3.ProjectOnPlane(gate.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float side = (retryIndex % 2 == 0 ? -1f : 1f) * Mathf.Lerp(1.5f, liftRetryOffsetRadius, Random.value);
        float back = liftStageDistance + (0.6f * retryIndex);
        Vector3 retryPoint = boardPoint.position - forward * back + right * side;
        return SnapPointToGround(retryPoint, boardPoint.position.y);
    }

    private Vector3 SnapPointToGround(Vector3 candidate, float fallbackY)
    {
        if (TryGetGroundSnapHit(candidate, out RaycastHit hit))
            return hit.point + Vector3.up * 0.05f;

        candidate.y = fallbackY;
        return candidate;
    }

    private Vector3 ResolveLiftApproachPoint(LiftLine lift)
    {
        if (lift == null || lift.bottomStation == null)
            return transform.position;

        LiftBoardGate gate = ResolvePreferredBoardGate(lift);
        if (gate != null)
            return liftRider != null && gate.IsQueued(liftRider)
                ? gate.GetQueueTargetPosition(liftRider)
                : gate.GetNpcEntryPosition();

        Vector3 station = lift.bottomStation.position;

        if (lift.TryGetClosestPointOnBand(station, out _, out Vector3 closestBandPoint, out Vector3 tangent))
            return closestBandPoint - tangent.normalized * 1.5f;

        return station;
    }

    private LiftBoardGate ResolvePreferredBoardGate(LiftLine lift)
    {
        if (lift == null)
            return null;

        _liftGateBuffer.Clear();
        lift.GetBoardGates(_liftGateBuffer);

        LiftBoardGate best = null;
        float bestSq = float.PositiveInfinity;
        Vector3 origin = lift.bottomStation != null ? lift.bottomStation.position : transform.position;

        for (int i = 0; i < _liftGateBuffer.Count; i++)
        {
            LiftBoardGate gate = _liftGateBuffer[i];
            if (gate == null)
                continue;

            Vector3 gatePos = gate.boardingPoint != null ? gate.boardingPoint.position : gate.transform.position;
            float sq = (gatePos - origin).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = gate;
            }
        }

        return best;
    }

    private static Vector3 GetBroadRunTarget(SkiRunLine run)
    {
        if (run == null || run.PointsWorld == null || run.PointsWorld.Count == 0)
            return Vector3.zero;

        var points = run.PointsWorld;
        if (points.Count == 1)
            return points[0];

        float remaining = 12f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float segLen = Vector3.Distance(a, b);

            if (segLen <= 0.0001f)
                continue;

            if (remaining <= segLen)
                return Vector3.LerpUnclamped(a, b, remaining / segLen);

            remaining -= segLen;
        }

        return points[points.Count - 1];
    }

    private static Vector3 GetRunForward(SkiRunLine run)
    {
        if (run == null || run.PointsWorld == null || run.PointsWorld.Count < 2)
            return Vector3.forward;

        Vector3 fwd = Vector3.ProjectOnPlane(run.PointsWorld[1] - run.PointsWorld[0], Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.forward;

        return fwd.normalized;
    }

    private void ResetBodyForStationaryNpcMode(Vector3 position, Quaternion rotation, bool clearStack = true)
    {
        CacheRefs();

        if (skiController != null && clearStack)
        {
            Vector3 forwardHint = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forwardHint.sqrMagnitude <= 0.0001f)
                forwardHint = transform.forward;

            skiController.ResetStackStateSilently(snapUpright: true, forwardHint: forwardHint.normalized);
            skiController.ReleaseStackSkiVisualOverridesAndSnap(snapToNeutralPose: true);
            skiController.ResetVisualPoseForWalkModeHandoff();
        }

        if (body == null)
            body = GetComponent<Rigidbody>();

        if (body != null)
        {
            body.isKinematic = false;
            body.detectCollisions = true;
            body.freezeRotation = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            body.WakeUp();
        }

        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }
}
