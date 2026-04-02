using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;
using SkiGame.Activities;
using SkiGame.Runs;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class RaceCourseLine : MonoBehaviour, IWorldInteractionPromptSource
{
    [Serializable]
    public sealed class RaceLeagueDefinition
    {
        [Min(1)] public int leagueNumber = 1;
        public string displayName = "League 1";
        [TextArea] public string description;

        [Header("NPC")]
        [Range(0.1f, 3f)] public float npcSpeedMultiplier = 1f;
        [Range(0f, 1f)] public float npcLinePrecision = 0.5f;
        [Range(0f, 1f)] public float npcMistakeChance = 0.2f;
        [Range(0f, 1f)] public float npcRecoveryQuality = 0.5f;
        [Range(0f, 1f)] public float npcOvertakeConfidence = 0.5f;

        [Header("Rules")]
        public bool overrideFailOnStack = false;
        public bool failOnStack = false;
        [Min(0f)] public float optionalTimeLimitSeconds = 0f;

        public string GetResolvedName()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            return $"League {Mathf.Max(1, leagueNumber)}";
        }
    }

    [Serializable]
    public struct GeneratedCheckpoint
    {
        public float distance;
        public Vector3 worldPos;
        public Vector3 forward;
        public float width;
        public float height;
        public float depth;
    }

    public enum RaceRuntimeState
    {
        Idle = 0,
        Countdown = 10,
        Racing = 20,
    }

    [Serializable]
    public struct CheckpointOverride
    {
        public int checkpointIndex;

        public bool overrideWorldOffset;
        public Vector3 worldOffset;

        public bool overrideYaw;
        public float yawOffsetDegrees;

        public bool overrideWidth;
        public float width;

        public bool overrideHeight;
        public float height;

        public bool overrideDepth;
        public float depth;
    }

    [Header("Identity")]
    [SerializeField] private string raceName = "New Race";
    [SerializeField] private string raceId;

    [Header("Prompt / Start")]
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private bool requireHold = false;
    [SerializeField] private float holdSeconds = 0.15f;
    [SerializeField] private string promptText = "Start Race";
    [Min(1)][SerializeField] private int defaultLeagueNumber = 1;
    [SerializeField] private int promptPriority = 60;

    [Header("Authoring")]
    [SerializeField] private List<Vector3> pointsWorld = new List<Vector3>();

    [Header("Course")]
    [SerializeField] private float courseWidthMeters = 18f;
    [SerializeField] private bool autoGenerateCheckpoints = true;
    [SerializeField] private float checkpointSpacingMeters = 35f;
    [SerializeField] private float checkpointStartOffsetMeters = 10f;
    [SerializeField] private float checkpointEndInsetMeters = 8f;
    [SerializeField] private float checkpointGateDepthMeters = 8f;
    [SerializeField] private float finishGateDepthMeters = 10f;
    [SerializeField] private float finishCompleteDistanceMeters = 8f;

    [Header("Start Flow")]
    [SerializeField] private float startCountdownSeconds = 3f;
    [SerializeField] private bool snapPlayerToStartOnBegin = true;
    [SerializeField] private bool alignPlayerToStartOnBegin = true;

    [Header("Rules")]
    [SerializeField] private bool failIfTooFarOffCourse = true;
    [SerializeField] private float offCourseGraceSeconds = 2f;
    [SerializeField] private bool failIfProgressSkipsUntouchedCheckpoint = true;
    [SerializeField] private float skipUntouchedCheckpointMeters = 50f;
    [SerializeField] private bool failOnStackDefault = false;
    [SerializeField] private float defaultTimeLimitSeconds = 0f;

    [Header("NPC")]
    [SerializeField] private bool hasNpcOpponents = true;
    [SerializeField] private int npcOpponentCount = 3;

    [SerializeField] private RaceCourseNpcRacer npcRacerPrefab;
    [SerializeField] private float npcLaneSpacingMeters = 3f;
    [SerializeField] private float npcRowSpacingMeters = 5f;
    [SerializeField] private Vector3 npcStartAreaOffset = Vector3.zero;

    [Header("Leagues")]
    [SerializeField]
    private List<RaceLeagueDefinition> leagues = new List<RaceLeagueDefinition>()
    {
        new RaceLeagueDefinition { leagueNumber = 1, displayName = "League 1" },
        new RaceLeagueDefinition { leagueNumber = 2, displayName = "League 2" },
        new RaceLeagueDefinition { leagueNumber = 3, displayName = "League 3" },
        new RaceLeagueDefinition { leagueNumber = 4, displayName = "League 4" },
    };

    [Header("Checkpoint Visuals")]
    [SerializeField] private Material checkpointMaterial;
    [SerializeField] private float checkpointHeightMeters = 3f;
    [SerializeField] private string generatedContainerName = "Checkpoints__Generated";
    [SerializeField] private bool rebuildGeneratedCheckpointsOnValidate = true;
    [SerializeField] private List<GeneratedCheckpoint> generatedCheckpoints = new List<GeneratedCheckpoint>();

    [Header("Checkpoint Overrides")]
    [SerializeField] private List<CheckpointOverride> checkpointOverrides = new List<CheckpointOverride>();

    [Header("Checkpoint State Colours")]
    [SerializeField] private Color checkpointUpcomingColor = new Color(1f, 0.92f, 0.16f, 0.30f);
    [SerializeField] private Color checkpointCurrentColor = new Color(0.12f, 0.95f, 0.25f, 0.32f);
    [SerializeField] private Color checkpointTriggeredColor = new Color(1f, 0.25f, 0.75f, 0.30f);
    [SerializeField] private Color checkpointFinalColor = new Color(1f, 0.15f, 0.15f, 0.35f);

    private readonly List<Renderer> _runtimeCheckpointRenderers = new List<Renderer>();
    private readonly List<GameObject> _runtimeCheckpointObjects = new List<GameObject>();

    private readonly List<float> _cumDistCache = new List<float>(256);
    private float _totalLengthCache;
    private bool _distCacheDirty = true;

    private GameObject _playerRootInTrigger;
    private bool _wasPressed;
    private float _held;
    private bool _enterArmed;

    private bool _attemptActive;
    private int _activeLeagueNumber;
    private int _nextCheckpointIndex;
    private float _attemptTime;
    private float _offCourseTimer;
    private GameObject _activePlayerRoot;
    private SkiController _activePlayerSkiController;

    private RaceRuntimeState _runtimeState = RaceRuntimeState.Idle;
    private float _countdownRemaining;

    private int _lastResolvedPlacement = -1;
    private int _lastResolvedEntrantCount = 0;

    private MountainActivityManager _boundActivityManager;

    public string RaceName => string.IsNullOrWhiteSpace(raceName) ? name : raceName.Trim();
    public string RaceId => raceId;
    public IReadOnlyList<Vector3> PointsWorld => pointsWorld;
    public IReadOnlyList<RaceLeagueDefinition> Leagues => leagues;
    public IReadOnlyList<GeneratedCheckpoint> GeneratedCheckpoints => generatedCheckpoints;
    public int DefaultLeagueNumber => Mathf.Max(1, defaultLeagueNumber);
    public float TotalLengthMeters => GetTotalLengthMeters();
    public bool HasNpcOpponents => hasNpcOpponents;
    public int NpcOpponentCount => Mathf.Max(0, npcOpponentCount);
    public float CourseWidthMeters => courseWidthMeters;

    public IReadOnlyList<CheckpointOverride> CheckpointOverrides => checkpointOverrides;

    public RaceRuntimeState RuntimeState => _runtimeState;
    public bool IsAttemptActive => _attemptActive;
    public bool IsCountdownActive => _attemptActive && _runtimeState == RaceRuntimeState.Countdown;
    public bool IsRaceInProgress => _attemptActive && _runtimeState == RaceRuntimeState.Racing;

    public float CountdownRemainingSeconds => Mathf.Max(0f, _countdownRemaining);
    public float AttemptTimeSeconds => _attemptTime;
    public int CurrentCheckpointIndex => _nextCheckpointIndex;
    public int CheckpointCount => generatedCheckpoints != null ? generatedCheckpoints.Count : 0;
    public int ActiveLeagueNumber => _activeLeagueNumber;
    public Vector3 StartWorldPosition => GetFirstPoint();
    public Vector3 StartForward => pointsWorld != null && pointsWorld.Count >= 2
        ? (pointsWorld[1] - pointsWorld[0]).normalized
        : transform.forward;

    public RaceCourseNpcRacer NpcRacerPrefab => npcRacerPrefab;
    public float NpcLaneSpacingMeters => npcLaneSpacingMeters;
    public float NpcRowSpacingMeters => npcRowSpacingMeters;
    public Vector3 NpcStartAreaOffset => npcStartAreaOffset;

    public int LastResolvedPlacement => _lastResolvedPlacement;
    public int LastResolvedEntrantCount => _lastResolvedEntrantCount;

    private void Awake()
    {
        EnsureRaceId();
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
    }

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();

        TryBindActivityManager();
    }

    private void OnDisable()
    {
        UnbindActivityManager();
    }

    private void Reset()
    {
        EnsureRaceId();
        MarkDistanceCacheDirty();
    }

    private void OnValidate()
    {
        EnsureRaceId();

        if (courseWidthMeters < 2f) courseWidthMeters = 2f;
        if (checkpointSpacingMeters < 5f) checkpointSpacingMeters = 5f;
        if (checkpointGateDepthMeters < 2f) checkpointGateDepthMeters = 2f;
        if (finishGateDepthMeters < 2f) finishGateDepthMeters = 2f;
        if (checkpointHeightMeters < 0.25f) checkpointHeightMeters = 0.25f;
        if (finishCompleteDistanceMeters < 1f) finishCompleteDistanceMeters = 1f;
        if (npcOpponentCount < 0) npcOpponentCount = 0;

        NormalizeLeagues();
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
        PruneInvalidCheckpointOverrides();

#if UNITY_EDITOR
        if (!Application.isPlaying && rebuildGeneratedCheckpointsOnValidate)
            QueueCheckpointVisualRefresh();
#endif
    }

    private void Update()
    {
        TryBindActivityManager();

        HandleStartInput();

        if (_attemptActive && _runtimeState == RaceRuntimeState.Countdown)
        {
            _countdownRemaining -= Time.deltaTime;
            if (_countdownRemaining <= 0f)
            {
                _countdownRemaining = 0f;
                _attemptTime = 0f;
                _runtimeState = RaceRuntimeState.Racing;
                RefreshCheckpointVisualStates();
            }

            return;
        }

        if (!_attemptActive)
            return;

        if (_activePlayerRoot == null)
        {
            FailRace("Player lost");
            return;
        }

        if (pointsWorld == null || pointsWorld.Count < 2)
        {
            FailRace("Race path invalid");
            return;
        }

        _attemptTime += Time.deltaTime;

        float resolvedTimeLimit = GetResolvedTimeLimit(_activeLeagueNumber);
        if (resolvedTimeLimit > 0f && _attemptTime > resolvedTimeLimit)
        {
            FailRace("Time limit exceeded");
            return;
        }

        if (_activePlayerSkiController != null && GetResolvedFailOnStack(_activeLeagueNumber) && _activePlayerSkiController.IsStacked)
        {
            FailRace("Stacked");
            return;
        }

        Vector3 playerPos = _activePlayerRoot.transform.position;
        CourseProjection projection = ProjectOntoCourse(playerPos);

        if (failIfTooFarOffCourse)
        {
            float halfWidth = Mathf.Max(1f, courseWidthMeters * 0.5f);
            if (projection.lateralDistance > halfWidth)
            {
                _offCourseTimer += Time.deltaTime;
                if (_offCourseTimer > offCourseGraceSeconds)
                {
                    FailRace("Went too far off course");
                    return;
                }
            }
            else
            {
                _offCourseTimer = 0f;
            }
        }

        if (_nextCheckpointIndex < generatedCheckpoints.Count)
        {
            if (failIfProgressSkipsUntouchedCheckpoint)
            {
                float checkpointDistance = generatedCheckpoints[_nextCheckpointIndex].distance;
                if (projection.distanceAlong > checkpointDistance + skipUntouchedCheckpointMeters)
                {
                    FailRace("Missed checkpoint");
                    return;
                }
            }

            
        }

        bool allCheckpointsCleared = _nextCheckpointIndex >= generatedCheckpoints.Count;
        if (allCheckpointsCleared)
        {
            float finishDistance = TotalLengthMeters;
            bool nearFinishByProgress = projection.distanceAlong >= finishDistance - finishCompleteDistanceMeters;

            if (nearFinishByProgress || IsInsideGate(playerPos, GetLastPoint(), GetLastTangent(), courseWidthMeters, finishGateDepthMeters))
            {
                CompleteRace();
            }
        }
    }

    private void HandleStartInput()
    {
        if (_playerRootInTrigger == null) return;
        if (interactAction == null || interactAction.action == null) return;
        if (pointsWorld == null || pointsWorld.Count < 2) return;

        var mgr = MountainActivityManager.Instance;
        if (mgr == null) return;

        var action = interactAction.action;
        if (!action.enabled) action.Enable();

        bool pressed = action.IsPressed();

        if (!_enterArmed)
        {
            if (!pressed) _enterArmed = true;
            _wasPressed = false;
            _held = 0f;
            return;
        }

        if (!pressed)
        {
            _wasPressed = false;
            _held = 0f;
            return;
        }

        if (!_wasPressed)
        {
            _wasPressed = true;
            _held = 0f;

            if (!requireHold)
                TriggerStart();

            return;
        }

        if (requireHold)
        {
            _held += Time.unscaledDeltaTime;
            if (_held >= holdSeconds)
            {
                _held = -999f;
                TriggerStart();
            }
        }
    }

    private void TriggerStart()
    {
        if (_playerRootInTrigger == null)
            return;

        if (_playerRootInTrigger.CompareTag("NPC"))
        {
            _playerRootInTrigger = null;
            return;
        }

        var mgr = MountainActivityManager.Instance;
        if (mgr == null)
            return;

        mgr.TryStart(MountainActivityKind.Race, this, RaceName, DefaultLeagueNumber);
    }

    private void HandleActivityStarted(MountainActivityKind kind, MonoBehaviour source, string displayName, int variantNumber)
    {
        if (kind != MountainActivityKind.Race || source != this)
            return;

        Debug.Log($"[RaceCourseLine] HandleActivityStarted received for '{RaceName}' on {name}. PlayerInTrigger={_playerRootInTrigger != null}", this);
        BeginAttempt(_playerRootInTrigger, variantNumber);
    }

    private void HandleActivityCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
    {
        if (kind == MountainActivityKind.Race && source == this)
            ResetAttemptState();
    }

    private void HandleActivityFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
    {
        if (kind == MountainActivityKind.Race && source == this)
            ResetAttemptState();
    }

    private void HandleActivityCancelled(MountainActivityKind kind, MonoBehaviour source, string displayName)
    {
        if (kind == MountainActivityKind.Race && source == this)
            ResetAttemptState();
    }

    private void BeginAttempt(GameObject playerRoot, int leagueNumber)
    {
        if (pointsWorld == null || pointsWorld.Count < 2)
        {
            FailRace("Race path invalid");
            return;
        }

        Debug.Log($"[RaceCourseLine] BeginAttempt on '{RaceName}'. Generated checkpoints: {(generatedCheckpoints != null ? generatedCheckpoints.Count : 0)}", this);

        ResolveGeneratedCheckpoints();

        _attemptActive = true;
        _activeLeagueNumber = Mathf.Max(1, leagueNumber);
        _nextCheckpointIndex = 0;
        _attemptTime = 0f;
        _offCourseTimer = 0f;
        _activePlayerRoot = playerRoot;
        _activePlayerSkiController = playerRoot != null ? playerRoot.GetComponentInParent<SkiController>() : null;

        if (snapPlayerToStartOnBegin && playerRoot != null)
            SnapPlayerToStart(playerRoot);

        _countdownRemaining = Mathf.Max(0f, startCountdownSeconds);
        _runtimeState = _countdownRemaining > 0f
            ? RaceRuntimeState.Countdown
            : RaceRuntimeState.Racing;

        if (Application.isPlaying)
        {
            EnsureRuntimeCheckpointVisuals();
            RebindGeneratedCheckpointTriggers();
            SetCheckpointVisualsVisible(true);
            SetGeneratedCheckpointTriggersEnabled(true);
            RefreshCheckpointVisualStates();

            Debug.Log($"[RaceCourseLine] Checkpoints enabled for '{RaceName}'. RuntimeState={_runtimeState}", this);
        }
    }

    private void ResetAttemptState()
    {
        _attemptActive = false;
        _runtimeState = RaceRuntimeState.Idle;
        _countdownRemaining = 0f;
        _activeLeagueNumber = 0;
        _nextCheckpointIndex = 0;
        _attemptTime = 0f;
        _offCourseTimer = 0f;
        _activePlayerRoot = null;
        _activePlayerSkiController = null;

        if (Application.isPlaying)
        {
            RefreshCheckpointVisualStates();
            SetGeneratedCheckpointTriggersEnabled(false);
            SetCheckpointVisualsVisible(false);
        }
    }

    private void TryBindActivityManager()
    {
        var mgr = MountainActivityManager.Instance;
        if (mgr == null || _boundActivityManager == mgr)
            return;

        UnbindActivityManager();

        _boundActivityManager = mgr;
        _boundActivityManager.OnActivityStarted += HandleActivityStarted;
        _boundActivityManager.OnActivityCompleted += HandleActivityCompleted;
        _boundActivityManager.OnActivityFailed += HandleActivityFailed;
        _boundActivityManager.OnActivityCancelled += HandleActivityCancelled;
    }

    private void UnbindActivityManager()
    {
        if (_boundActivityManager == null)
            return;

        _boundActivityManager.OnActivityStarted -= HandleActivityStarted;
        _boundActivityManager.OnActivityCompleted -= HandleActivityCompleted;
        _boundActivityManager.OnActivityFailed -= HandleActivityFailed;
        _boundActivityManager.OnActivityCancelled -= HandleActivityCancelled;
        _boundActivityManager = null;
    }

    private Transform FindGeneratedCheckpointContainer()
    {
        return transform.Find(generatedContainerName);
    }

    private void SetCheckpointVisualsVisible(bool visible)
    {
        Transform container = Application.isPlaying
            ? GetOrCreateGeneratedCheckpointContainerRuntime()
            : FindGeneratedCheckpointContainer();

        if (container == null)
            return;

        container.gameObject.SetActive(visible);
    }

    private void RebindGeneratedCheckpointTriggers()
    {
        var container = FindGeneratedCheckpointContainer();
        if (container == null)
            return;

        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (child == null)
                continue;

            var trigger = child.GetComponent<RaceCheckpointTrigger>();
            if (trigger == null)
                trigger = child.gameObject.AddComponent<RaceCheckpointTrigger>();

            trigger.Initialize(this, i);

            var box = child.GetComponent<BoxCollider>();
            if (box == null)
                box = child.gameObject.AddComponent<BoxCollider>();

            box.isTrigger = true;
            box.enabled = true;
        }
    }

    private void SetGeneratedCheckpointTriggersEnabled(bool enabled)
    {
        var container = FindGeneratedCheckpointContainer();
        if (container == null)
            return;

        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (child == null)
                continue;

            var box = child.GetComponent<BoxCollider>();
            if (box != null)
                box.enabled = enabled;
        }
    }

    public bool TryConsumeCheckpointTrigger(int checkpointIndex, Collider other)
    {
        if (!_attemptActive || _runtimeState != RaceRuntimeState.Racing)
            return false;

        if (checkpointIndex != _nextCheckpointIndex)
            return false;

        if (other == null || _activePlayerRoot == null)
            return false;

        if (!ColliderBelongsToActivePlayer(other))
            return false;

        _nextCheckpointIndex++;
        RefreshCheckpointVisualStates();
        return true;
    }

    private bool ColliderBelongsToActivePlayer(Collider other)
    {
        if (other == null || _activePlayerRoot == null)
            return false;

        var resolvedRoot = ResolvePlayerRoot(other);
        if (resolvedRoot != null && resolvedRoot == _activePlayerRoot)
            return true;

        if (other.transform.IsChildOf(_activePlayerRoot.transform))
            return true;

        var activeInput = _activePlayerRoot.GetComponentInParent<PlayerInput>();
        var otherInput = other.GetComponentInParent<PlayerInput>();
        if (activeInput != null && otherInput != null && activeInput == otherInput)
            return true;

        var activeRb = _activePlayerRoot.GetComponentInParent<Rigidbody>();
        if (activeRb != null && other.attachedRigidbody != null && other.attachedRigidbody == activeRb)
            return true;

        return false;
    }

    private void CompleteRace()
    {
        var mgr = MountainActivityManager.Instance;
        if (mgr != null)
            mgr.Complete(MountainActivityKind.Race, this, "Finished");
    }

    private void FailRace(string reason)
    {
        var mgr = MountainActivityManager.Instance;
        if (mgr != null)
            mgr.Fail(MountainActivityKind.Race, this, reason);
    }

    public RaceLeagueDefinition GetLeague(int leagueNumber)
    {
        leagueNumber = Mathf.Max(1, leagueNumber);

        for (int i = 0; i < leagues.Count; i++)
        {
            var l = leagues[i];
            if (l != null && l.leagueNumber == leagueNumber)
                return l;
        }

        return null;
    }

    public string GetLeagueDisplayName(int leagueNumber)
    {
        var l = GetLeague(leagueNumber);
        return l != null ? l.GetResolvedName() : $"League {Mathf.Max(1, leagueNumber)}";
    }

    public float GetResolvedTimeLimit(int leagueNumber)
    {
        var l = GetLeague(leagueNumber);
        if (l != null && l.optionalTimeLimitSeconds > 0f)
            return l.optionalTimeLimitSeconds;

        return defaultTimeLimitSeconds;
    }

    public bool GetResolvedFailOnStack(int leagueNumber)
    {
        var l = GetLeague(leagueNumber);
        if (l != null && l.overrideFailOnStack)
            return l.failOnStack;

        return failOnStackDefault;
    }

    public void AddPointWorld(Vector3 point)
    {
        if (pointsWorld == null) pointsWorld = new List<Vector3>();
        pointsWorld.Add(point);
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
    }

    public int InsertPointWorld(int index, Vector3 point)
    {
        if (pointsWorld == null) pointsWorld = new List<Vector3>();
        index = Mathf.Clamp(index, 0, pointsWorld.Count);
        pointsWorld.Insert(index, point);
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
        return index;
    }

    public int InsertPointWorldSmart(Vector3 point)
    {
        if (pointsWorld == null)
            pointsWorld = new List<Vector3>();

        int n = pointsWorld.Count;
        if (n <= 1)
            return InsertPointWorld(n, point);

        Vector2 pp = new Vector2(point.x, point.z);

        Vector3 prev3 = pointsWorld[n - 2];
        Vector3 last3 = pointsWorld[n - 1];

        Vector2 prev = new Vector2(prev3.x, prev3.z);
        Vector2 last = new Vector2(last3.x, last3.z);

        Vector2 tail = last - prev;
        float tailLen = tail.magnitude;

        if (tailLen > 0.0001f)
        {
            Vector2 tailDir = tail / tailLen;
            Vector2 fromLast = pp - last;
            float fromLastDist = fromLast.magnitude;

            if (fromLastDist > 0.0001f)
            {
                Vector2 fromLastDir = fromLast / fromLastDist;
                float forwardDot = Vector2.Dot(tailDir, fromLastDir);
                float forwardDistance = Vector2.Dot(fromLast, tailDir);
                float lateralDistance = Mathf.Sqrt(Mathf.Max(0f, fromLast.sqrMagnitude - forwardDistance * forwardDistance));

                const float minForwardDot = 0.15f;
                float appendProximityMeters = Mathf.Max(8f, tailLen * 1.75f);
                float appendLateralToleranceMeters = Mathf.Max(6f, courseWidthMeters * 0.6f);

                bool isForwardOfTail = forwardDistance > 0f && forwardDot >= minForwardDot;
                bool isNearTailEndpoint = fromLastDist <= appendProximityMeters;
                bool isReasonablyAlignedWithTail = lateralDistance <= appendLateralToleranceMeters;

                if (isForwardOfTail && isNearTailEndpoint && isReasonablyAlignedWithTail)
                    return InsertPointWorld(n, point);
            }
        }

        int bestSeg = -1;
        float bestDist = float.PositiveInfinity;
        float bestTUnclamped = 0f;

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 a3 = pointsWorld[i];
            Vector3 b3 = pointsWorld[i + 1];

            Vector2 a = new Vector2(a3.x, a3.z);
            Vector2 b = new Vector2(b3.x, b3.z);

            Vector2 ab = b - a;
            float abLen2 = ab.sqrMagnitude;

            float tUnclamped = 0f;
            if (abLen2 > 0.000001f)
                tUnclamped = Vector2.Dot(pp - a, ab) / abLen2;

            float t = Mathf.Clamp01(tUnclamped);
            Vector2 proj = a + ab * t;
            float d = Vector2.Distance(pp, proj);

            if (d < bestDist)
            {
                bestDist = d;
                bestSeg = i;
                bestTUnclamped = tUnclamped;
            }
        }

        if (bestSeg < 0)
            return InsertPointWorld(n, point);

        if (bestSeg == 0 && bestTUnclamped < 0f)
            return InsertPointWorld(0, point);

        if (bestSeg == n - 2 && bestTUnclamped > 1f)
            return InsertPointWorld(n, point);

        return InsertPointWorld(bestSeg + 1, point);
    }

    public void SetPointWorld(int index, Vector3 point)
    {
        if (pointsWorld == null || index < 0 || index >= pointsWorld.Count)
            return;

        pointsWorld[index] = point;
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
    }

    public bool RemoveLastPoint()
    {
        if (pointsWorld == null || pointsWorld.Count == 0)
            return false;

        pointsWorld.RemoveAt(pointsWorld.Count - 1);
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
        return true;
    }

    public void RemovePointAt(int index)
    {
        if (pointsWorld == null || index < 0 || index >= pointsWorld.Count)
            return;

        pointsWorld.RemoveAt(index);
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
    }

    public void ClearPoints()
    {
        if (pointsWorld == null) pointsWorld = new List<Vector3>();
        pointsWorld.Clear();
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
    }

    public void AppendRunPoints(SkiRunLine run, bool skipDuplicateStart = true)
    {
        if (run == null || run.PointsWorld == null || run.PointsWorld.Count == 0)
            return;

        if (pointsWorld == null) pointsWorld = new List<Vector3>();

        for (int i = 0; i < run.PointsWorld.Count; i++)
        {
            if (skipDuplicateStart && pointsWorld.Count > 0 && i == 0)
            {
                if ((pointsWorld[pointsWorld.Count - 1] - run.PointsWorld[i]).sqrMagnitude < 0.0001f)
                    continue;
            }

            pointsWorld.Add(run.PointsWorld[i]);
        }

        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
    }

    public void AppendRunSlicePoints(SkiRunLine run, float start01, float end01, int sampleCount = -1, bool skipDuplicateStart = true)
    {
        if (run == null || run.PointsWorld == null || run.PointsWorld.Count < 2)
            return;

        var src = run.PointsWorld;
        float total = run.GetTotalLengthMeters();
        if (total <= 0.01f)
            return;

        start01 = Mathf.Clamp01(start01);
        end01 = Mathf.Clamp01(end01);

        float startDist = total * start01;
        float endDist = total * end01;
        bool reversed = endDist < startDist;

        if (reversed)
        {
            float tmp = startDist;
            startDist = endDist;
            endDist = tmp;
        }

        float sliceLength = Mathf.Max(0f, endDist - startDist);
        if (sliceLength < 0.01f)
            return;

        if (sampleCount <= 0)
        {
            float spacing = Mathf.Max(4f, checkpointSpacingMeters * 0.5f);
            sampleCount = Mathf.Max(2, Mathf.CeilToInt(sliceLength / spacing) + 1);
        }

        if (pointsWorld == null)
            pointsWorld = new List<Vector3>();

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount <= 1 ? 0f : (i / (float)(sampleCount - 1));
            float d = Mathf.Lerp(startDist, endDist, t);
            Vector3 sample = SamplePolylineAtDistance(src, d);

            if (reversed)
            {
                int reversedIndex = sampleCount - 1 - i;
                float rt = sampleCount <= 1 ? 0f : (reversedIndex / (float)(sampleCount - 1));
                d = Mathf.Lerp(startDist, endDist, rt);
                sample = SamplePolylineAtDistance(src, d);
            }

            if (skipDuplicateStart && pointsWorld.Count > 0 && i == 0)
            {
                if ((pointsWorld[pointsWorld.Count - 1] - sample).sqrMagnitude < 0.0001f)
                    continue;
            }

            pointsWorld.Add(sample);
        }

        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
    }

    [ContextMenu("Resolve Generated Checkpoints")]
    public void ResolveGeneratedCheckpoints()
    {
        generatedCheckpoints ??= new List<GeneratedCheckpoint>();
        generatedCheckpoints.Clear();

        if (!autoGenerateCheckpoints || pointsWorld == null || pointsWorld.Count < 2)
            return;

        float total = GetTotalLengthMeters();
        float start = Mathf.Clamp(checkpointStartOffsetMeters, 0f, Mathf.Max(0f, total));
        float end = Mathf.Clamp(total - checkpointEndInsetMeters, 0f, Mathf.Max(0f, total));

        if (end <= start)
            return;

        for (float d = start; d < end; d += Mathf.Max(5f, checkpointSpacingMeters))
        {
            Vector3 pos = SamplePointAtDistance(d);
            Vector3 fwd = SampleTangentAtDistance(d);
            GeneratedCheckpoint cp = new GeneratedCheckpoint
            {
                distance = d,
                worldPos = pos,
                forward = fwd,
                width = courseWidthMeters,
                height = checkpointHeightMeters,
                depth = checkpointGateDepthMeters
            };

            ApplyCheckpointOverride(ref cp, generatedCheckpoints.Count);
            generatedCheckpoints.Add(cp);
        }

        PruneInvalidCheckpointOverrides();
    }

    private void ApplyCheckpointOverride(ref GeneratedCheckpoint checkpoint, int checkpointIndex)
    {
        if (checkpointOverrides == null || checkpointOverrides.Count == 0)
            return;

        for (int i = 0; i < checkpointOverrides.Count; i++)
        {
            var ov = checkpointOverrides[i];
            if (ov.checkpointIndex != checkpointIndex)
                continue;

            if (ov.overrideWorldOffset)
                checkpoint.worldPos += ov.worldOffset;

            if (ov.overrideYaw)
                checkpoint.forward = Quaternion.AngleAxis(ov.yawOffsetDegrees, Vector3.up) * checkpoint.forward;

            if (ov.overrideWidth)
                checkpoint.width = Mathf.Max(0.25f, ov.width);

            if (ov.overrideHeight)
                checkpoint.height = Mathf.Max(0.25f, ov.height);

            if (ov.overrideDepth)
                checkpoint.depth = Mathf.Max(0.25f, ov.depth);

            return;
        }
    }

    public bool TryGetCheckpointOverride(int checkpointIndex, out CheckpointOverride ov)
    {
        if (checkpointOverrides != null)
        {
            for (int i = 0; i < checkpointOverrides.Count; i++)
            {
                if (checkpointOverrides[i].checkpointIndex == checkpointIndex)
                {
                    ov = checkpointOverrides[i];
                    return true;
                }
            }
        }

        ov = default;
        return false;
    }

    private int FindCheckpointOverrideIndex(int checkpointIndex)
    {
        if (checkpointOverrides == null)
            return -1;

        for (int i = 0; i < checkpointOverrides.Count; i++)
        {
            if (checkpointOverrides[i].checkpointIndex == checkpointIndex)
                return i;
        }

        return -1;
    }

    public void SetCheckpointTransformOverride(int checkpointIndex, Vector3 worldOffset, bool overrideYaw, float yawOffsetDegrees)
    {
        if (checkpointOverrides == null)
            checkpointOverrides = new List<CheckpointOverride>();

        int idx = FindCheckpointOverrideIndex(checkpointIndex);
        CheckpointOverride ov = idx >= 0
            ? checkpointOverrides[idx]
            : new CheckpointOverride { checkpointIndex = checkpointIndex };

        ov.overrideWorldOffset = worldOffset.sqrMagnitude > 0.000001f;
        ov.worldOffset = worldOffset;

        ov.overrideYaw = overrideYaw || Mathf.Abs(yawOffsetDegrees) > 0.001f;
        ov.yawOffsetDegrees = yawOffsetDegrees;

        if (idx >= 0)
            checkpointOverrides[idx] = ov;
        else
            checkpointOverrides.Add(ov);

        ResolveGeneratedCheckpoints();
#if UNITY_EDITOR
        QueueCheckpointVisualRefresh();
#endif
    }

    public void SetCheckpointSizeOverride(int checkpointIndex, float width, float height, float depth)
    {
        width = Mathf.Max(0.25f, width);
        height = Mathf.Max(0.25f, height);
        depth = Mathf.Max(0.25f, depth);

        if (checkpointOverrides == null)
            checkpointOverrides = new List<CheckpointOverride>();

        int idx = FindCheckpointOverrideIndex(checkpointIndex);
        CheckpointOverride ov = idx >= 0
            ? checkpointOverrides[idx]
            : new CheckpointOverride { checkpointIndex = checkpointIndex };

        ov.overrideWidth = true;
        ov.overrideHeight = true;
        ov.overrideDepth = true;
        ov.width = width;
        ov.height = height;
        ov.depth = depth;

        if (idx >= 0)
            checkpointOverrides[idx] = ov;
        else
            checkpointOverrides.Add(ov);

        ResolveGeneratedCheckpoints();
#if UNITY_EDITOR
        QueueCheckpointVisualRefresh();
#endif
    }

    public void ClearCheckpointOverride(int checkpointIndex)
    {
        if (checkpointOverrides == null || checkpointOverrides.Count == 0)
            return;

        for (int i = checkpointOverrides.Count - 1; i >= 0; i--)
        {
            if (checkpointOverrides[i].checkpointIndex == checkpointIndex)
                checkpointOverrides.RemoveAt(i);
        }

        ResolveGeneratedCheckpoints();
#if UNITY_EDITOR
        QueueCheckpointVisualRefresh();
#endif
    }

    private void PruneInvalidCheckpointOverrides()
    {
        if (checkpointOverrides == null || checkpointOverrides.Count == 0)
            return;

        int checkpointCount = generatedCheckpoints != null ? generatedCheckpoints.Count : 0;
        for (int i = checkpointOverrides.Count - 1; i >= 0; i--)
        {
            if (checkpointOverrides[i].checkpointIndex < 0 || checkpointOverrides[i].checkpointIndex >= checkpointCount)
                checkpointOverrides.RemoveAt(i);
        }
    }

    private Transform GetOrCreateGeneratedCheckpointContainerRuntime()
    {
        var t = transform.Find(generatedContainerName);
        if (t != null)
            return t;

        var go = new GameObject(generatedContainerName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private void EnsureRuntimeCheckpointVisuals()
    {
        if (!Application.isPlaying)
            return;

        var container = GetOrCreateGeneratedCheckpointContainerRuntime();
        _runtimeCheckpointRenderers.Clear();
        _runtimeCheckpointObjects.Clear();

        int required = generatedCheckpoints != null ? generatedCheckpoints.Count : 0;

        for (int i = 0; i < required; i++)
        {
            GameObject go;
            if (i < container.childCount)
            {
                go = container.GetChild(i).gameObject;
                go.SetActive(true);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Checkpoint_{i + 1:00}";
                go.transform.SetParent(container, true);
            }

            var cp = generatedCheckpoints[i];

            Vector3 upOffset = Vector3.up * (cp.height * 0.5f);
            go.transform.position = cp.worldPos + upOffset;
            go.transform.rotation = cp.forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(cp.forward.normalized, Vector3.up)
                : Quaternion.identity;

            SetDesiredWorldScale(go.transform, new Vector3(cp.width, cp.height, cp.depth));

            var box = go.GetComponent<BoxCollider>();
            if (box == null)
                box = go.AddComponent<BoxCollider>();

            box.isTrigger = true;
            box.enabled = false;
            box.size = Vector3.one;

            var trigger = go.GetComponent<RaceCheckpointTrigger>();
            if (trigger == null)
                trigger = go.AddComponent<RaceCheckpointTrigger>();

            trigger.Initialize(this, i);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (checkpointMaterial != null)
                    renderer.material = new Material(checkpointMaterial);

                _runtimeCheckpointRenderers.Add(renderer);
            }
            else
            {
                _runtimeCheckpointRenderers.Add(null);
            }

            _runtimeCheckpointObjects.Add(go);
        }

        for (int i = container.childCount - 1; i >= required; i--)
            container.GetChild(i).gameObject.SetActive(false);

        RefreshCheckpointVisualStates();
    }

    private void SetRendererColour(Renderer renderer, Color colour)
    {
        if (renderer == null)
            return;

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_Color", colour);
        block.SetColor("_BaseColor", colour);
        renderer.SetPropertyBlock(block);
    }

    private void RefreshCheckpointVisualStates()
    {
        if (!Application.isPlaying)
            return;

        EnsureRuntimeCheckpointRendererCache();

        int checkpointCount = generatedCheckpoints != null ? generatedCheckpoints.Count : 0;
        int finalIndex = checkpointCount - 1;

        for (int i = 0; i < checkpointCount; i++)
        {
            GameObject go = i < _runtimeCheckpointObjects.Count ? _runtimeCheckpointObjects[i] : null;
            Renderer renderer = i < _runtimeCheckpointRenderers.Count ? _runtimeCheckpointRenderers[i] : null;

            bool cleared = i < _nextCheckpointIndex;

            if (go != null)
                go.SetActive(_attemptActive && !cleared);

            if (renderer == null || cleared)
                continue;

            Color c;
            if (i == finalIndex)
            {
                c = checkpointFinalColor;
            }
            else if (_attemptActive && i == _nextCheckpointIndex)
            {
                c = checkpointCurrentColor;
            }
            else
            {
                c = checkpointUpcomingColor;
            }

            SetRendererColour(renderer, c);
        }
    }

    private void EnsureRuntimeCheckpointRendererCache()
    {
        if (_runtimeCheckpointRenderers.Count > 0)
            return;

        var container = transform.Find(generatedContainerName);
        if (container == null)
            return;

        _runtimeCheckpointRenderers.Clear();
        for (int i = 0; i < container.childCount; i++)
        {
            var r = container.GetChild(i).GetComponent<Renderer>();
            if (r != null)
                _runtimeCheckpointRenderers.Add(r);
        }
    }


#if UNITY_EDITOR
    private static readonly HashSet<int> _queuedVisualRefreshIds = new HashSet<int>();
    private bool _isRefreshingCheckpointVisuals;

    [ContextMenu("Regenerate Checkpoint Visuals")]
    public void RegenerateCheckpointVisuals()
    {
        if (Application.isPlaying)
            return;

        if (_isRefreshingCheckpointVisuals)
            return;

        _isRefreshingCheckpointVisuals = true;
        try
        {
            Transform container = GetOrCreateGeneratedContainer();
            ClearGeneratedChildren(container);

            if (generatedCheckpoints == null || generatedCheckpoints.Count == 0)
                return;

            for (int i = 0; i < generatedCheckpoints.Count; i++)
            {
                var cp = generatedCheckpoints[i];

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Checkpoint_{i + 1:00}";
                Undo.RegisterCreatedObjectUndo(go, "Create Race Checkpoint Gate");

                go.transform.SetParent(container, true);

                Vector3 upOffset = Vector3.up * (cp.height * 0.5f);
                go.transform.position = cp.worldPos + upOffset;

                if (cp.forward.sqrMagnitude > 0.0001f)
                    go.transform.rotation = Quaternion.LookRotation(cp.forward.normalized, Vector3.up);
                else
                    go.transform.rotation = Quaternion.identity;

                SetDesiredWorldScale(go.transform, new Vector3(cp.width, cp.height, cp.depth));

                var collider = go.GetComponent<BoxCollider>();
                if (collider == null)
                    collider = go.AddComponent<BoxCollider>();

                collider.isTrigger = true;
                collider.size = Vector3.one; // matches the cube mesh scale

                var trigger = go.GetComponent<RaceCheckpointTrigger>();
                if (trigger == null)
                    trigger = go.AddComponent<RaceCheckpointTrigger>();

                trigger.Initialize(this, i);

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && checkpointMaterial != null)
                    renderer.sharedMaterial = checkpointMaterial;

                go.hideFlags = HideFlags.None;
            }
        }
        finally
        {
            _isRefreshingCheckpointVisuals = false;
        }
    }

    private static void SetDesiredWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        if (target == null)
            return;

        Vector3 parentLossy = Vector3.one;
        if (target.parent != null)
            parentLossy = target.parent.lossyScale;

        target.localScale = new Vector3(
            SafeDivide(desiredWorldScale.x, parentLossy.x),
            SafeDivide(desiredWorldScale.y, parentLossy.y),
            SafeDivide(desiredWorldScale.z, parentLossy.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        if (Mathf.Abs(divisor) < 0.0001f)
            return value;

        return value / divisor;
    }

    [ContextMenu("Clear Generated Checkpoint Visuals")]
    public void ClearGeneratedCheckpointVisuals()
    {
        if (Application.isPlaying)
            return;

        Transform container = transform.Find(generatedContainerName);
        if (container != null)
            ClearGeneratedChildren(container);
    }

    public void QueueCheckpointVisualRefresh()
    {
        if (Application.isPlaying)
            return;

        int id = GetInstanceID();
        if (_queuedVisualRefreshIds.Contains(id))
            return;

        _queuedVisualRefreshIds.Add(id);

        EditorApplication.delayCall += () =>
        {
            _queuedVisualRefreshIds.Remove(id);

            if (this == null)
                return;

            if (Application.isPlaying)
                return;

            RegenerateCheckpointVisuals();
            EditorUtility.SetDirty(this);
        };
    }

    private Transform GetOrCreateGeneratedContainer()
    {
        var t = transform.Find(generatedContainerName);
        if (t != null) return t;

        var go = new GameObject(generatedContainerName);
        Undo.RegisterCreatedObjectUndo(go, "Create Checkpoint Container");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static void ClearGeneratedChildren(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i);
            if (child == null) continue;

            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
#endif


    private void EnsureRaceId()
    {
        if (string.IsNullOrWhiteSpace(raceId))
            raceId = Guid.NewGuid().ToString("N");
    }

    private void NormalizeLeagues()
    {
        if (leagues == null)
            leagues = new List<RaceLeagueDefinition>();

        var used = new HashSet<int>();
        for (int i = 0; i < leagues.Count; i++)
        {
            var l = leagues[i];
            if (l == null) continue;

            l.leagueNumber = Mathf.Max(1, l.leagueNumber);
            while (used.Contains(l.leagueNumber))
                l.leagueNumber++;

            used.Add(l.leagueNumber);

            if (string.IsNullOrWhiteSpace(l.displayName))
                l.displayName = $"League {l.leagueNumber}";
        }
    }

    private void MarkDistanceCacheDirty()
    {
        _distCacheDirty = true;
    }

    private void RebuildDistanceCacheIfNeeded()
    {
        if (!_distCacheDirty)
            return;

        _distCacheDirty = false;
        _cumDistCache.Clear();
        _totalLengthCache = 0f;

        if (pointsWorld == null || pointsWorld.Count == 0)
            return;

        _cumDistCache.Add(0f);

        for (int i = 1; i < pointsWorld.Count; i++)
        {
            _totalLengthCache += Vector3.Distance(pointsWorld[i - 1], pointsWorld[i]);
            _cumDistCache.Add(_totalLengthCache);
        }
    }

    public float GetTotalLengthMeters()
    {
        RebuildDistanceCacheIfNeeded();
        return _totalLengthCache;
    }

    private Vector3 GetFirstPoint()
    {
        return pointsWorld != null && pointsWorld.Count > 0 ? pointsWorld[0] : transform.position;
    }

    private Vector3 GetLastPoint()
    {
        return pointsWorld != null && pointsWorld.Count > 0 ? pointsWorld[pointsWorld.Count - 1] : transform.position;
    }

    private Vector3 GetLastTangent()
    {
        if (pointsWorld == null || pointsWorld.Count < 2)
            return transform.forward;

        return (pointsWorld[pointsWorld.Count - 1] - pointsWorld[pointsWorld.Count - 2]).normalized;
    }

    public Vector3 SamplePointAtDistance(float distance)
    {
        RebuildDistanceCacheIfNeeded();

        if (pointsWorld == null || pointsWorld.Count == 0)
            return transform.position;

        if (pointsWorld.Count == 1)
            return pointsWorld[0];

        distance = Mathf.Clamp(distance, 0f, _totalLengthCache);

        for (int i = 1; i < _cumDistCache.Count; i++)
        {
            float aDist = _cumDistCache[i - 1];
            float bDist = _cumDistCache[i];
            if (distance <= bDist)
            {
                float segLen = Mathf.Max(0.0001f, bDist - aDist);
                float t = Mathf.Clamp01((distance - aDist) / segLen);
                return Vector3.Lerp(pointsWorld[i - 1], pointsWorld[i], t);
            }
        }

        return pointsWorld[pointsWorld.Count - 1];
    }

    public Vector3 SampleTangentAtDistance(float distance)
    {
        RebuildDistanceCacheIfNeeded();

        if (pointsWorld == null || pointsWorld.Count < 2)
            return transform.forward;

        distance = Mathf.Clamp(distance, 0f, _totalLengthCache);

        for (int i = 1; i < _cumDistCache.Count; i++)
        {
            if (distance <= _cumDistCache[i])
                return (pointsWorld[i] - pointsWorld[i - 1]).normalized;
        }

        return (pointsWorld[pointsWorld.Count - 1] - pointsWorld[pointsWorld.Count - 2]).normalized;
    }

    private struct CourseProjection
    {
        public float distanceAlong;
        public float lateralDistance;
        public Vector3 projectedPoint;
    }

    private CourseProjection ProjectOntoCourse(Vector3 worldPos)
    {
        CourseProjection best = default;
        best.lateralDistance = float.MaxValue;

        RebuildDistanceCacheIfNeeded();

        if (pointsWorld == null || pointsWorld.Count < 2)
            return best;

        float accum = 0f;

        for (int i = 1; i < pointsWorld.Count; i++)
        {
            Vector3 a = pointsWorld[i - 1];
            Vector3 b = pointsWorld[i];
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                continue;

            float t = Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / lenSq);
            Vector3 p = a + ab * t;
            float lateral = Vector3.Distance(worldPos, p);

            if (lateral < best.lateralDistance)
            {
                best.lateralDistance = lateral;
                best.projectedPoint = p;
                best.distanceAlong = accum + Vector3.Distance(a, p);
            }

            accum += Vector3.Distance(a, b);
        }

        return best;
    }

    private static bool IsInsideGate(Vector3 worldPos, Vector3 gatePos, Vector3 gateForward, float gateWidth, float gateDepth)
    {
        Quaternion rot = gateForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(gateForward.normalized, Vector3.up)
            : Quaternion.identity;

        Vector3 local = Quaternion.Inverse(rot) * (worldPos - gatePos);
        return Mathf.Abs(local.x) <= gateWidth * 0.5f &&
               Mathf.Abs(local.z) <= gateDepth * 0.5f;
    }

    private static Vector3 SamplePolylineAtDistance(IReadOnlyList<Vector3> pts, float distance)
    {
        if (pts == null || pts.Count == 0)
            return Vector3.zero;

        if (pts.Count == 1)
            return pts[0];

        float accum = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = pts[i - 1];
            Vector3 b = pts[i];
            float segLen = Vector3.Distance(a, b);
            if (distance <= accum + segLen || i == pts.Count - 1)
            {
                float t = segLen > 0.0001f ? Mathf.Clamp01((distance - accum) / segLen) : 0f;
                return Vector3.Lerp(a, b, t);
            }

            accum += segLen;
        }

        return pts[pts.Count - 1];
    }

    public bool TryProjectPointOntoCourse(Vector3 worldPos, out float distanceAlong, out float lateralDistance, out Vector3 projectedPoint)
    {
        if (pointsWorld == null || pointsWorld.Count < 2)
        {
            distanceAlong = 0f;
            lateralDistance = float.MaxValue;
            projectedPoint = worldPos;
            return false;
        }

        CourseProjection p = ProjectOntoCourse(worldPos);
        distanceAlong = p.distanceAlong;
        lateralDistance = p.lateralDistance;
        projectedPoint = p.projectedPoint;
        return true;
    }

    public Vector3 GetNpcStartSlotWorldPosition(int slotIndex)
    {
        Vector3 origin = StartWorldPosition + npcStartAreaOffset;
        Vector3 forward = StartForward.sqrMagnitude > 0.0001f ? StartForward.normalized : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        int row = slotIndex / 3;
        int col = slotIndex % 3;

        float lateral = (col - 1) * npcLaneSpacingMeters;
        float back = row * npcRowSpacingMeters;

        return origin - forward * back + right * lateral;
    }

    public Quaternion GetNpcStartSlotRotation()
    {
        Vector3 forward = StartForward.sqrMagnitude > 0.0001f ? StartForward.normalized : transform.forward;
        Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flat.sqrMagnitude <= 0.0001f)
            flat = transform.forward;

        return Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    public void SetResolvedPlacement(int placement, int entrantCount)
    {
        _lastResolvedPlacement = placement;
        _lastResolvedEntrantCount = entrantCount;
    }

    private void OnTriggerEnter(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null) return;

        _enterArmed = false;
        _playerRootInTrigger = root;
    }

    private void OnTriggerExit(Collider other)
    {
        var root = ResolvePlayerRoot(other);
        if (root == null) return;

        if (_playerRootInTrigger == root)
            _playerRootInTrigger = null;
    }

    private static GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null)
            return null;

        var t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("NPC"))
                return null;
            t = t.parent;
        }

        Transform playerTagged = null;
        t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("Player"))
            {
                playerTagged = t;
                break;
            }
            t = t.parent;
        }

        if (playerTagged == null)
            return null;

        var pi = other.GetComponentInParent<PlayerInput>();
        if (pi != null)
            return pi.gameObject;

        return playerTagged.gameObject;
    }

    private void SnapPlayerToStart(GameObject playerRoot)
    {
        if (playerRoot == null)
            return;

        Vector3 startPos = StartWorldPosition;
        Vector3 forward = StartForward.sqrMagnitude > 0.0001f ? StartForward : transform.forward;

        playerRoot.transform.position = startPos;

        if (alignPlayerToStartOnBegin)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f)
                playerRoot.transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }
    }

    public bool IsPromptAvailable
    {
        get
        {
            var mgr = MountainActivityManager.Instance;
            return _playerRootInTrigger != null &&
                   pointsWorld != null &&
                   pointsWorld.Count >= 2 &&
                   mgr != null &&
                   mgr.CanStart(MountainActivityKind.Race, this);
        }
    }

    public string PromptActionText => "Interact";
    public string PromptDescriptionText => $"{promptText} ({GetLeagueDisplayName(DefaultLeagueNumber)})";
    public bool PromptUsesHold => requireHold;
    public float PromptHoldDuration => holdSeconds;
    public Vector3 PromptWorldPosition => GetFirstPoint();
    public int PromptPriority => promptPriority;

    private void OnDrawGizmosSelected()
    {
        if (pointsWorld == null || pointsWorld.Count == 0)
            return;

        Gizmos.color = new Color(0.15f, 0.9f, 1f, 1f);
        for (int i = 1; i < pointsWorld.Count; i++)
            Gizmos.DrawLine(pointsWorld[i - 1], pointsWorld[i]);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(GetFirstPoint(), 2.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetLastPoint(), 2.5f);

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 1f);
        if (generatedCheckpoints != null)
        {
            for (int i = 0; i < generatedCheckpoints.Count; i++)
            {
                var cp = generatedCheckpoints[i];
                Quaternion rot = cp.forward.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(cp.forward.normalized, Vector3.up)
                    : Quaternion.identity;

                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(cp.worldPos, rot, Vector3.one);
                Gizmos.DrawWireCube(new Vector3(0f, cp.height * 0.5f, 0f), new Vector3(cp.width, cp.height, cp.depth));
                Gizmos.matrix = prev;
            }
        }

        var hub = GetComponent<Collider>();
        if (hub == null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawWireSphere(GetFirstPoint(), 4f);
        }
    }
}