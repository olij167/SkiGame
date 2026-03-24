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
        AmbientCruise
    }

    [Header("References")]
    [SerializeField] private SkiController skiController;
    [SerializeField] private WalkingController walkingController;
    [SerializeField] private LiftRider liftRider;
    [SerializeField] private NpcSkierProfile profile;
    [SerializeField] private NpcSkierRunFollower locomotion;
    [SerializeField] private NpcSkierAppearanceGenerator appearanceGenerator;
    [SerializeField] private NpcAstarPathAgent pathAgent;

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

    [Header("Lift Commitment")]
    [SerializeField] private float minimumLiftVerticalGain = 20f;
    [SerializeField] private float maxLiftTopToRunTargetDistance = 120f;
    [SerializeField] private float abandonLiftDistance = 45f;
    [SerializeField] private float postLiftRunJoinGraceSeconds = 8f;

    private BrainState _state = BrainState.Uninitialized;
    private IntentKind _intentKind = IntentKind.None;

    private float _stateUntil;
    private float _boardingAbortTime;
    private float _nextLiftInputTime;
    private float _nextRespawnAllowedTime;
    private float _postLiftRunJoinUntil;

    private SkiRunLine _currentRun;
    private LiftLine _currentLift;
    private Vector3 _currentBroadTarget;
    private Vector3 _currentLiftApproachPoint;
    private SkiRunLine _lastRun;
    private bool _initialized;

    private float _visibleFastForwardMultiplier = 1f;
    private Renderer[] _cachedRenderers;

    private SpawnContextHint _pendingSpawnHint = SpawnContextHint.None;
    private Vector3 _pendingHintWorldPoint;

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

    public void InitializeNow()
    {
        if (_initialized)
            return;

        CacheRefs();

        if (autoFindRunsInScene && availableRuns.Count == 0)
            availableRuns = new List<SkiRunLine>(FindObjectsOfType<SkiRunLine>(includeInactive: false));

        if (autoFindLiftsInScene && availableLifts.Count == 0)
            availableLifts = new List<LiftLine>(FindObjectsOfType<LiftLine>(includeInactive: false));

        locomotion?.ConfigureRuns(availableRuns);

        if (randomizeProfileOnStart && profile != null)
            profile.RandomizeProfile();

        if (randomizeAppearanceOnStart && appearanceGenerator != null)
            appearanceGenerator.ApplyRandomAppearance(profile);

        if (skiController != null && locomotion != null)
            skiController.SetExternalInputSource(locomotion);

        if (skiController != null)
            skiController.OnStacked += HandleStacked;

        _initialized = true;
        EnterWaiting(Random.Range(waitMin, waitMax));
    }

    public void PrepareForPoolSleep()
    {
        pathAgent?.Stop();
        walkingController?.ClearExternalMove();
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();

        _currentRun = null;
        _currentLift = null;
        _intentKind = IntentKind.None;
        _pendingSpawnHint = SpawnContextHint.None;
        _state = BrainState.Waiting;
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

        if (!_initialized)
            InitializeNow();
        else
            EnterWaiting(Random.Range(waitMin * 0.35f, waitMax * 0.65f));
    }

    private void Update()
    {
        if (!_initialized)
            return;

        switch (_state)
        {
            case BrainState.Waiting:
                if (Time.time >= _stateUntil)
                    PickNextIntent();
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

        bool canJoinRunDirect = CanJoinRunDirectly(_currentRun);

        if (_currentLift != null && ShouldPreferLiftForRun(_currentLift, _currentBroadTarget))
        {
            _intentKind = IntentKind.LiftThenRun;
            _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);

            if (ShouldUseWalkingSupportTo(_currentLiftApproachPoint))
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
                    _currentLiftApproachPoint = ResolveLiftApproachPoint(lift);
                    _currentRun = ChooseRunNearLiftTop(lift);

                    _intentKind = IntentKind.LiftThenRun;

                    if (ShouldUseWalkingSupportTo(_currentLiftApproachPoint))
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
        walkingController?.ForceEnterWalkMode();
        locomotion?.SetInputEnabled(false);
        locomotion?.ClearRecoveryRequests();
        pathAgent?.SetDestination(target);

        _currentBroadTarget = target;
        _state = BrainState.WalkingSupport;
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
            _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);
            locomotion.UpdateBroadTarget(_currentLiftApproachPoint);

            float distToLift = Vector3.Distance(transform.position, _currentLiftApproachPoint);
            if (liftRider != null && !liftRider.IsAttached && distToLift <= liftBoardDistance)
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
        if (pathAgent != null && pathAgent.IsStuck)
        {
            TryRespawnOntoIntent();
            return;
        }

        Vector3 target =
            _intentKind == IntentKind.LiftThenRun && _currentLift != null
            ? _currentLiftApproachPoint
            : _currentBroadTarget;

        float distance = Vector3.Distance(transform.position, target);
        if (distance <= walkArrivalDistance || (pathAgent != null && pathAgent.ReachedDestination))
        {
            if (_intentKind == IntentKind.LiftThenRun && _currentLift != null)
                EnterBoardingLift();
            else if (_currentRun != null)
                BeginRunSki(_currentRun);
            else
                BeginAmbientSki(_currentBroadTarget);
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

        _currentLiftApproachPoint = ResolveLiftApproachPoint(_currentLift);
        float distance = Vector3.Distance(transform.position, _currentLiftApproachPoint);

        if (distance > liftBoardDistance * 1.75f)
        {
            BeginWalkingSupport(_currentLiftApproachPoint);
            return;
        }

        if (Time.time >= _boardingAbortTime)
        {
            BeginAmbientSki(_currentLiftApproachPoint);
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

        if (_intentKind == IntentKind.LiftThenRun && _currentLift != null)
            BeginAmbientSki(_currentLiftApproachPoint);
        else if (_currentRun != null)
            BeginAmbientSki(GetBroadRunTarget(_currentRun));
        else
            BeginAmbientSki(_currentBroadTarget);

        _nextRespawnAllowedTime = Time.time + ScaleFastForwardDelay(respawnCooldown);
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

        List<SkiRunLine> preferred = new();
        List<SkiRunLine> fallback = new();

        for (int i = 0; i < availableRuns.Count; i++)
        {
            SkiRunLine run = availableRuns[i];
            if (run == null) continue;

            fallback.Add(run);

            if (profile == null || profile.PrefersDifficulty(run.Difficulty))
                preferred.Add(run);
        }

        List<SkiRunLine> pool = preferred.Count > 0 ? preferred : fallback;
        if (pool.Count == 0)
            return null;

        if (_lastRun != null && pool.Contains(_lastRun) && profile != null && Random.value <= profile.RepeatRunBias01)
            return _lastRun;

        return pool[Random.Range(0, pool.Count)];
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

        SkiRunLine best = null;
        float bestScore = float.PositiveInfinity;
        Vector3 top = lift.topStation.position;

        for (int i = 0; i < availableRuns.Count; i++)
        {
            SkiRunLine run = availableRuns[i];
            if (run == null) continue;

            if (profile != null && !profile.PrefersDifficulty(run.Difficulty))
                continue;

            Vector3 target = GetBroadRunTarget(run);
            float score = Vector3.Distance(top, target);
            if (score < bestScore)
            {
                bestScore = score;
                best = run;
            }
        }

        return best != null ? best : ChooseRunForProfile();
    }

    private Vector3 ResolveLiftApproachPoint(LiftLine lift)
    {
        if (lift == null || lift.bottomStation == null)
            return transform.position;

        Vector3 station = lift.bottomStation.position;

        if (lift.TryGetClosestPointOnBand(station, out _, out Vector3 closestBandPoint, out Vector3 tangent))
            return closestBandPoint - tangent.normalized * 1.5f;

        return station;
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