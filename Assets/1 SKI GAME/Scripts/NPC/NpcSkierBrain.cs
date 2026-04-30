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

    private float _stateUntil;
    private float _boardingAbortTime;
    private float _nextLiftInputTime;
    private float _nextRespawnAllowedTime;
    private float _postLiftRunJoinUntil;
    private float _respawnSuppressedUntil;
    private float _walkingRecoveryUntil;
    private float _nextLookAroundTime;
    private float _nextHubShuffleTime;

    private SkiRunLine _currentRun;
    private LiftLine _currentLift;
    private Vector3 _currentBroadTarget;
    private Vector3 _currentLiftApproachPoint;
    private Vector3 _currentLiftStagePoint;
    private SkiRunLine _lastRun;
    private readonly List<SkiRunLine> _recentRuns = new();
    private readonly List<Transform> _hubAnchors = new();
    private Transform _currentHubAnchor;
    private bool _liftStageReached;
    private int _boardingRetryCount;
    private bool _initialized;
    private bool _spectatorCrowdActive;
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
    private float _lastPoolSleepTime = -999f;
    private Vector3 _lastPoolSleepPosition;

    public float LastPoolSleepTime => _lastPoolSleepTime;
    public Vector3 LastPoolSleepPosition => _lastPoolSleepPosition;
    public bool IsSpectatorCrowdActive => _spectatorCrowdActive;

    private void Awake()
    {
        CacheRefs();
    }

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

        if (skiController != null && locomotion != null)
            skiController.SetExternalInputSource(locomotion);

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
        walkingController?.ClearExternalMove();
        walkingController?.SetWalkPresentationKeepsSkisEquipped(true);
        walkingController?.ForceEnterWalkMode();
        sorenessMeter?.ResetSoreness();

        _currentRun = null;
        _currentLift = null;
        _intentKind = IntentKind.None;
        _state = BrainState.Waiting;
        transform.SetPositionAndRotation(_spectatorAnchorPosition, Quaternion.LookRotation(_spectatorForward, Vector3.up));
    }

    public void EndSpectatorCrowd()
    {
        ResumeFromSpectatorCrowd(immediateIntent: false);
    }

    public void ResumeFromSpectatorCrowd(bool immediateIntent = true)
    {
        if (!_spectatorCrowdActive)
            return;

        _spectatorCrowdActive = false;
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
        _state = BrainState.Waiting;
        _respawnSuppressedUntil = 0f;
        _walkingRecoveryUntil = 0f;
        _liftStageReached = false;
        _boardingRetryCount = 0;
        _currentHubAnchor = null;
        _spectatorCrowdActive = false;
        _lastPoolSleepTime = Time.time;
        _lastPoolSleepPosition = transform.position;
    }

    public void SetSpawnContextHint(SpawnContextHint hint, Vector3 worldPoint)
    {
        _pendingSpawnHint = hint;
        _pendingHintWorldPoint = worldPoint;
    }

    public void WakeFromPool(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();
        locomotion?.SuppressRespawnRequests(0f);
        sorenessMeter?.ResetSoreness();
        _respawnSuppressedUntil = Time.time + ScaleFastForwardDelay(1.5f);
        _walkingRecoveryUntil = 0f;

        if (!_initialized)
            InitializeNow();
        else
            EnterWaiting(Random.Range(waitMin * 0.35f, waitMax * 0.65f));
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (_spectatorCrowdActive)
        {
            TickSpectatorCrowd();
            return;
        }

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

        transform.SetPositionAndRotation(grounded, Quaternion.Slerp(transform.rotation, desiredRotation, 10f * Time.deltaTime));
    }

    private void PickNextIntent()
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

        if (_currentLift != null && ShouldPreferLiftForRun(_currentLift, _currentBroadTarget))
        {
            _intentKind = IntentKind.LiftThenRun;
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
            }
            else
            {
                _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);
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
        LiftBoardGate gate = liftRider.NearbyBoardGate != null ? liftRider.NearbyBoardGate : preferredGate;
        if (gate == null)
        {
            HandleBoardingFailure();
            return;
        }

        _currentLiftApproachPoint = gate.GetQueueTargetPosition(liftRider);
        bool inGateTrigger = liftRider.NearbyBoardGate == gate;
        bool isQueued = gate.IsQueued(liftRider);
        float distance = Vector3.Distance(transform.position, _currentLiftApproachPoint);

        if (!inGateTrigger)
        {
            BeginWalkingSupport(_currentLiftApproachPoint);
            return;
        }

        if (!isQueued)
        {
            isQueued = gate.TryJoinQueue(liftRider);
            if (!isQueued && (distance > walkArrivalDistance || !IsWithinGateCatchArea(gate)))
            {
                BeginWalkingSupport(_currentLiftApproachPoint);
                return;
            }
        }

        if (distance > liftBoardDistance * 1.75f || !IsWithinGateCatchArea(gate))
        {
            BeginWalkingSupport(_currentLiftApproachPoint);
            return;
        }

        if (isQueued)
            _boardingAbortTime = Mathf.Max(_boardingAbortTime, Time.time + ScaleFastForwardDelay(1.25f));

        if (Time.time >= _boardingAbortTime)
        {
            HandleBoardingFailure();
            return;
        }

        if (Time.time >= _nextLiftInputTime)
        {
            _nextLiftInputTime = Time.time + ScaleFastForwardDelay(0.15f);
            liftRider.SetLiftInput(true, true);
        }
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

        if (_currentRun != null)
            BeginAmbientSki(GetBroadRunTarget(_currentRun));
        else
            BeginAmbientSki(_currentBroadTarget);
    }

    private void HandleStacked(SkiController.StackEventInfo info)
    {
        if (!_initialized || skiController == null)
            return;

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
        return Vector3.Distance(transform.position, queueTarget) <= Mathf.Max(liftBoardCommitDistance, walkArrivalDistance + 0.35f) &&
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
            _currentLiftApproachPoint = ResolveLiftRetryPoint(_currentLift, _boardingRetryCount);
            BeginWalkingSupport(_currentLiftApproachPoint);
            return;
        }

        _boardingRetryCount = 0;
        _liftStageReached = false;

        if (_currentLift != null && _currentLift.bottomStation != null)
            _currentHubAnchor = _currentLift.bottomStation;

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
        Vector3 rayOrigin = candidate + Vector3.up * 25f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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
            return gate.GetQueueTargetPosition(liftRider);

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
}
