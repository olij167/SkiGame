using System;
using System.Collections.Generic;
using SkiGame.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using SkiGame.UI;
using SkiGame.Activities;
using SkiGame.Runs;
using SkiGame.Progression;
using SkiGame.Tricks;
using TimeWeather;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
//[ExecuteAlways]
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
        [Range(1, 99)] public int requiredPlacementToClear = 99;
        [Min(0)] public int optionalTrickScoreThreshold = 0;
        public TrickScoreEmphasis trickScoreEmphasis = TrickScoreEmphasis.Balanced;
        public TrickRequirementDefinition requiredTrickRule = new TrickRequirementDefinition();

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

    [SerializeField] private string regionId;

    [Header("Championship Progression")]
    [SerializeField] private bool isRegionalChampionship = false;
    [Min(1)][SerializeField] private int championshipRequiredLeagueNumber = 3;

    // New preferred reward path.
    [SerializeField] private string championshipPermanentPassIdReward = "";

    // Legacy fallback reward path.
    [Min(0)][SerializeField] private int championshipPermanentPassLevelReward = -1;

    [Header("Prompt / Start")]
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private bool requireHold = false;
    [SerializeField] private float holdSeconds = 0.15f;
    [SerializeField] private string promptText = "Start Race";
    [Min(1)][SerializeField] private int defaultLeagueNumber = 1;
    [SerializeField] private int promptPriority = 60;

    [SerializeField] private InputActionReference previousLeagueAction;
    [SerializeField] private InputActionReference nextLeagueAction;
    [SerializeField] private bool tapInteractCyclesNextLeague = true;
    [SerializeField] private bool allowDebugKeyboardLeagueFallback = false;

    [Header("Authoring")]
    [SerializeField] private List<Vector3> pointsWorld = new List<Vector3>();

    [Header("Map Visuals")]
    [SerializeField] private bool overrideMapLineColor;
    [SerializeField] private Color mapLineColor = new Color(0.27f, 0.76f, 1f, 0.95f);
    [SerializeField] private bool syncHostRendererColorToMapLine = true;
    [SerializeField] private List<Renderer> mapColorSyncRenderers = new List<Renderer>();

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
    [SerializeField][Min(0f)] private float playerStartBackOffsetMeters = 6f;
    [SerializeField][Min(0.01f)] private float leagueSelectionCooldownSeconds = 0.12f;

    private float _lastLeagueSelectionTime = -999f;

    [Header("Rules")]
    [SerializeField] private bool failIfTooFarOffCourse = true;
    [SerializeField] private float offCourseGraceSeconds = 2f;
    [SerializeField] private float skisOffGraceSeconds = 4f;
    [SerializeField] private bool failIfProgressSkipsUntouchedCheckpoint = true;
    [SerializeField] private float skipUntouchedCheckpointMeters = 50f;
    [SerializeField] private bool failOnStackDefault = false;
    [SerializeField] private float defaultTimeLimitSeconds = 0f;
    [SerializeField] private float startStackRecoveryGraceSeconds = 1f;
    [SerializeField] private LayerMask safeStartGroundMask = ~0;
    [SerializeField][Min(0.05f)] private float safeStartGroundProbeUp = 4f;
    [SerializeField][Min(0.25f)] private float safeStartGroundProbeDown = 12f;
    [SerializeField][Min(0f)] private float safeStartHoverHeight = 0.35f;

    [Header("NPC")]
    [SerializeField] private bool hasNpcOpponents = true;
    [SerializeField] private int npcOpponentCount = 3;

    [SerializeField] private RaceCourseNpcRacer npcRacerPrefab;
    [SerializeField] private float npcLaneSpacingMeters = 3f;
    [SerializeField] private float npcRowSpacingMeters = 5f;
    [SerializeField][Min(0f)] private float npcStartLeadFromPlayerMeters = 8f;
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

    [Header("Rewards")]
    [SerializeField][Min(0)] private int completionReward = 75;
    [SerializeField][Min(0)] private int firstTimeLeagueCompletionBonus = 50;
    [SerializeField][Min(0)] private int firstPlaceBonus = 50;
    [SerializeField][Min(0)] private int secondPlaceBonus = 25;
    [SerializeField][Min(0)] private int thirdPlaceBonus = 10;

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

#if UNITY_EDITOR
    private const double CheckpointVisualRefreshDebounceSeconds = 0.15d;
    [NonSerialized] private double _checkpointVisualRefreshRequestedAt;
    [NonSerialized] private int _lastCheckpointVisualHash;
    [NonSerialized] private bool _hasCheckpointVisualHash;
#endif

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
    private float _skisOffTimer;
    private float _startStackRecoveryRemaining;
    private GameObject _activePlayerRoot;
    private SkiController _activePlayerSkiController;
    private WalkingController _activePlayerWalkingController;
    private SkierTrickTracker _activePlayerTrickTracker;

    private RaceRuntimeState _runtimeState = RaceRuntimeState.Idle;
    private float _countdownRemaining;
    private int _lastCountdownWholeSeconds = -1;
    private Vector3 _countdownStartPlayerPosition;
    private Quaternion _countdownStartPlayerRotation = Quaternion.identity;
    private bool _hasCountdownStartPose;

    private int _lastResolvedPlacement = -1;
    private int _lastResolvedEntrantCount = 0;

    private MountainActivityManager _boundActivityManager;

    private int _selectedLeagueNumber;

    private int _lastRewardGranted = 0;
    private bool _lastLeagueCompletedForFirstTime = false;
    private int _lastUnlockedLeagueNumber = 0;
    private bool _lastPersonalBestImproved = false;
    private bool _lastChampionshipCompletedForFirstTime = false;
    private string _lastChampionshipRewardSummary = string.Empty;
    private float _lastCompletionTimeSeconds = -1f;

    private bool _previousLeagueWasPressed;

    private int _lastAttemptLeagueNumber;
    private GameObject _lastAttemptPlayerRoot;
    private GameObject _pendingStartPlayerRoot;
    private string _lastFailureReason = string.Empty;
    private int _attemptAccumulatedTrickScore;
    private int _attemptBestTrickScore;
    private bool _attemptMatchedRequiredTrick;
    private string _lastLeagueObjectiveSummary = string.Empty;

    private bool _ownsExternalTimePause;

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
    public float OffCourseGraceSecondsRemaining => Mathf.Max(0f, offCourseGraceSeconds - _offCourseTimer);
    public float SkisOffGraceSecondsRemaining => Mathf.Max(0f, skisOffGraceSeconds - _skisOffTimer);
    public bool IsOffCourseWarningActive => _attemptActive && failIfTooFarOffCourse && _offCourseTimer > 0f;
    public bool IsSkisOffWarningActive => _attemptActive && _activePlayerWalkingController != null && !_activePlayerWalkingController.SkisOn;
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

    public int SelectedLeagueNumber => GetNormalizedSelectedLeagueNumber();
    public bool CanShowLeagueSelection => _playerRootInTrigger != null && !_attemptActive && pointsWorld != null && pointsWorld.Count >= 2;

    public int LastRewardGranted => _lastRewardGranted;
    public bool LastLeagueCompletedForFirstTime => _lastLeagueCompletedForFirstTime;
    public int LastUnlockedLeagueNumber => _lastUnlockedLeagueNumber;
    public bool LastPersonalBestImproved => _lastPersonalBestImproved;
    public bool LastChampionshipCompletedForFirstTime => _lastChampionshipCompletedForFirstTime;
    public string LastChampionshipRewardSummary => _lastChampionshipRewardSummary;
    public float LastCompletionTimeSeconds => _lastCompletionTimeSeconds;

    public int CompletionReward => completionReward;
    public int FirstTimeLeagueCompletionBonus => firstTimeLeagueCompletionBonus;
    public int FirstPlaceBonus => firstPlaceBonus;
    public int SecondPlaceBonus => secondPlaceBonus;
    public int ThirdPlaceBonus => thirdPlaceBonus;

    public int LastAttemptLeagueNumber => _lastAttemptLeagueNumber;
    public string LastFailureReason => _lastFailureReason;
    public string LastLeagueObjectiveSummary => _lastLeagueObjectiveSummary;

    public static event Action<RaceCourseLine, int> OnRaceCountdownTick;
    public static event Action<RaceCourseLine> OnRaceCountdownGo;

    public string RegionId => string.IsNullOrWhiteSpace(regionId) ? string.Empty : regionId.Trim();
    public bool IsRegionalChampionship => isRegionalChampionship;
    public int ChampionshipRequiredLeagueNumber => Mathf.Max(1, championshipRequiredLeagueNumber);
    public string ChampionshipPermanentPassIdReward => championshipPermanentPassIdReward;
    public int ChampionshipPermanentPassLevelReward => championshipPermanentPassLevelReward;
    public bool OverrideMapLineColor => overrideMapLineColor;
    public Color MapLineColor => mapLineColor;

    private void Awake()
    {
        EnsureRaceId();
        MarkDistanceCacheDirty();
        ResolveGeneratedCheckpoints();
        _selectedLeagueNumber = GetHighestUnlockedLeagueNumber();
        ApplyConfiguredMapVisuals();
    }

    private void OnEnable()
    {
        WorldInteractionPromptRegistry.Register(this);

        if (interactAction != null && interactAction.action != null && !interactAction.action.enabled)
            interactAction.action.Enable();

        TryBindActivityManager();
    }

    private void OnDisable()
    {
        WorldInteractionPromptRegistry.Unregister(this);
        UnbindActivityManager();
    }

    private void Reset()
    {
        EnsureRaceId();
        MarkDistanceCacheDirty();
        CacheDefaultMapColorSyncRenderers();
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

        _selectedLeagueNumber = GetNormalizedSelectedLeagueNumber();
        ApplyConfiguredMapVisuals();

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
            HoldPlayerAtCountdownStart();

            int countdownWholeSeconds = Mathf.CeilToInt(_countdownRemaining);
            if (countdownWholeSeconds > 0 && countdownWholeSeconds != _lastCountdownWholeSeconds)
            {
                _lastCountdownWholeSeconds = countdownWholeSeconds;
                GameAudio.PlayWorld(GameAudioCueId.RaceCountdownTick, StartWorldPosition);
                OnRaceCountdownTick?.Invoke(this, countdownWholeSeconds);
            }

            _countdownRemaining -= Time.deltaTime;
            if (_countdownRemaining <= 0f)
            {
                _countdownRemaining = 0f;
                _attemptTime = 0f;
                _runtimeState = RaceRuntimeState.Racing;

                EnsurePlayerRaceControlEnabled(_activePlayerRoot);

                GameAudio.PlayWorld(GameAudioCueId.RaceCountdownGo, StartWorldPosition);
                OnRaceCountdownGo?.Invoke(this);

                RefreshCheckpointVisualStates();
                TryConsumeCurrentCheckpointIfAlreadyOverlapping();
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
        _startStackRecoveryRemaining = Mathf.Max(0f, _startStackRecoveryRemaining - Time.deltaTime);

        float resolvedTimeLimit = GetResolvedTimeLimit(_activeLeagueNumber);
        if (resolvedTimeLimit > 0f && _attemptTime > resolvedTimeLimit)
        {
            FailRace("Time limit exceeded");
            return;
        }

        if (_activePlayerWalkingController != null && !_activePlayerWalkingController.SkisOn)
        {
            _skisOffTimer += Time.deltaTime;
            if (_skisOffTimer > skisOffGraceSeconds)
            {
                FailRace("Skis removed for too long");
                return;
            }
        }
        else
        {
            _skisOffTimer = 0f;
        }

        if (_activePlayerSkiController != null && GetResolvedFailOnStack(_activeLeagueNumber) && _activePlayerSkiController.IsStacked)
        {
            if (_startStackRecoveryRemaining > 0f)
            {
                RecoverPlayerFromStartStack();
                return;
            }

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

    public Vector3 GetPlayerStartWorldPosition()
    {
        Vector3 startPos = StartWorldPosition;
        Vector3 forward = StartForward.sqrMagnitude > 0.0001f
            ? StartForward.normalized
            : transform.forward;

        return startPos - forward * Mathf.Max(0f, playerStartBackOffsetMeters);
    }

    private void HandleStartInput()
    {
        if (_attemptActive)
            return;

        if (_playerRootInTrigger == null || pointsWorld == null || pointsWorld.Count < 2)
        {
            _wasPressed = false;
            _held = 0f;
            _enterArmed = false;
            return;
        }

        if (interactAction == null || interactAction.action == null)
            return;

        var interact = interactAction.action;
        if (!interact.enabled)
            interact.Enable();

        if (previousLeagueAction != null && previousLeagueAction.action != null && !previousLeagueAction.action.enabled)
            previousLeagueAction.action.Enable();

        bool interactPressed = interact.IsPressed();
        bool interactPressedThisFrame = interact.WasPressedThisFrame();
        bool interactReleasedThisFrame = interact.WasReleasedThisFrame();

        bool previousPressedThisFrame =
            (previousLeagueAction != null && previousLeagueAction.action != null && previousLeagueAction.action.WasPressedThisFrame()) ||
            (allowDebugKeyboardLeagueFallback && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame);

        if (!_enterArmed)
        {
            if (!interactPressed)
                _enterArmed = true;

            _wasPressed = false;
            _held = 0f;
            return;
        }

        if (previousPressedThisFrame)
        {
            SelectPreviousLeague();
            return;
        }

        if (interactPressedThisFrame)
        {
            _wasPressed = true;
            _held = 0f;
            return;
        }

        if (_wasPressed && interactPressed)
        {
            _held += Time.unscaledDeltaTime;

            if (_held >= holdSeconds)
            {
                _held = -999f;
                TriggerStart();
                return;
            }
        }

        if (interactReleasedThisFrame)
        {
            bool wasTap = _held >= 0f && _held < holdSeconds;

            _wasPressed = false;
            _held = 0f;

            if (wasTap)
                SelectNextLeague();

            return;
        }

        if (!interactPressed)
        {
            _wasPressed = false;
            _held = 0f;
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

        TryStartSelectedLeague();
    }

    public void SelectNextLeague()
    {
        TryStepLeague(1);
    }

    public void SelectPreviousLeague()
    {
        TryStepLeague(-1);
    }

    private void TryStepLeague(int direction)
    {
        if (leagues == null || leagues.Count == 0)
            return;

        if (Time.unscaledTime - _lastLeagueSelectionTime < leagueSelectionCooldownSeconds)
            return;

        int current = GetNormalizedSelectedLeagueNumber();
        int currentIndex = GetLeagueIndex(current);
        if (currentIndex < 0)
        {
            _selectedLeagueNumber = GetHighestUnlockedLeagueNumber();
            _lastLeagueSelectionTime = Time.unscaledTime;
            return;
        }

        int step = direction >= 0 ? 1 : -1;
        int nextIndex = currentIndex;

        for (int i = 0; i < leagues.Count; i++)
        {
            nextIndex += step;

            if (nextIndex < 0)
                nextIndex = leagues.Count - 1;
            else if (nextIndex >= leagues.Count)
                nextIndex = 0;

            RaceLeagueDefinition candidate = leagues[nextIndex];
            if (candidate != null && IsLeagueUnlocked(candidate.leagueNumber))
            {
                _selectedLeagueNumber = Mathf.Max(1, candidate.leagueNumber);
                _lastLeagueSelectionTime = Time.unscaledTime;
                GameAudio.PlayUi(GameAudioCueId.UiNavigate, 0.85f);
                return;
            }
        }

        _lastLeagueSelectionTime = Time.unscaledTime;
    }

    public Color GetResolvedMapLineColor(Color fallback)
    {
        return overrideMapLineColor ? mapLineColor : fallback;
    }

    private void ApplyConfiguredMapVisuals()
    {
        if (!syncHostRendererColorToMapLine)
            return;

        CacheDefaultMapColorSyncRenderers();
        ApplyColourToMapSyncRenderers(mapLineColor);
    }

    private void CacheDefaultMapColorSyncRenderers()
    {
        if (mapColorSyncRenderers == null)
            mapColorSyncRenderers = new List<Renderer>();

        for (int i = mapColorSyncRenderers.Count - 1; i >= 0; i--)
        {
            if (mapColorSyncRenderers[i] == null)
                mapColorSyncRenderers.RemoveAt(i);
        }

        if (mapColorSyncRenderers.Count > 0)
            return;

        GetComponents(mapColorSyncRenderers);
    }

    private void ApplyColourToMapSyncRenderers(Color colour)
    {
        if (mapColorSyncRenderers == null)
            return;

        for (int i = 0; i < mapColorSyncRenderers.Count; i++)
            SetRendererColour(mapColorSyncRenderers[i], colour);
    }

    public bool TryStartSelectedLeague()
    {
        int leagueNumber = GetNormalizedSelectedLeagueNumber();

        if (!CanShowLeagueSelection)
            return false;

        if (!IsLeagueUnlocked(leagueNumber))
        {
            ShowLockedLeaguePopup(leagueNumber);
            return false;
        }

        var mgr = MountainActivityManager.Instance;
        if (mgr == null || !mgr.CanStart(MountainActivityKind.Race, this))
            return false;

        _lastCountdownWholeSeconds = -1;
        return mgr.TryStart(MountainActivityKind.Race, this, RaceName, leagueNumber);
    }

    private void ShowLockedLeaguePopup(int leagueNumber)
    {
        int requiredLeague = GetPreviousLeagueNumber(leagueNumber);
        string targetLeagueName = GetLeagueDisplayName(leagueNumber);

        string body = requiredLeague > 0
            ? $"Win {GetLeagueDisplayName(requiredLeague)} before entering {targetLeagueName}"
            : $"You cannot enter {targetLeagueName} yet";

        LiftAccessPopupBus.RaiseCustomDenied("Race Locked", body);
    }

    public bool CanStartLeague(int leagueNumber)
    {
        if (!CanShowLeagueSelection)
            return false;

        if (!IsLeagueUnlocked(leagueNumber))
            return false;

        var mgr = MountainActivityManager.Instance;
        return mgr != null && mgr.CanStart(MountainActivityKind.Race, this);
    }

    public bool IsLeagueUnlocked(int leagueNumber)
    {
        leagueNumber = Mathf.Max(1, leagueNumber);

        if (IsRegionalChampionship)
            return IsChampionshipUnlocked();

        int previousLeague = GetPreviousLeagueNumber(leagueNumber);
        if (previousLeague <= 0)
            return true;

        return HasWonLeague(previousLeague);
    }

    public bool IsChampionshipUnlocked()
    {
        if (!IsRegionalChampionship)
            return true;

        RaceCourseLine[] allRaces = FindObjectsOfType<RaceCourseLine>(includeInactive: true);
        int requiredLeague = ChampionshipRequiredLeagueNumber;
        bool foundEligibleStandardRace = false;

        for (int i = 0; i < allRaces.Length; i++)
        {
            RaceCourseLine race = allRaces[i];
            if (race == null || race == this || race.IsRegionalChampionship)
                continue;

            if (!string.Equals(race.RegionId, RegionId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            foundEligibleStandardRace = true;
            if (!race.HasCompletedLeague(requiredLeague))
                return false;
        }

        return foundEligibleStandardRace;
    }

    public int GetChampionshipCompletedStandardRaceCount()
    {
        if (!IsRegionalChampionship)
            return 0;

        RaceCourseLine[] allRaces = FindObjectsOfType<RaceCourseLine>(includeInactive: true);
        int completed = 0;
        int requiredLeague = ChampionshipRequiredLeagueNumber;

        for (int i = 0; i < allRaces.Length; i++)
        {
            RaceCourseLine race = allRaces[i];
            if (race == null || race == this || race.IsRegionalChampionship)
                continue;

            if (!string.Equals(race.RegionId, RegionId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (race.HasCompletedLeague(requiredLeague))
                completed++;
        }

        return completed;
    }

    public int GetChampionshipTotalStandardRaceCount()
    {
        if (!IsRegionalChampionship)
            return 0;

        RaceCourseLine[] allRaces = FindObjectsOfType<RaceCourseLine>(includeInactive: true);
        int total = 0;

        for (int i = 0; i < allRaces.Length; i++)
        {
            RaceCourseLine race = allRaces[i];
            if (race == null || race == this || race.IsRegionalChampionship)
                continue;

            if (string.Equals(race.RegionId, RegionId, System.StringComparison.OrdinalIgnoreCase))
                total++;
        }

        return total;
    }

    public string BuildChampionshipLockReason()
    {
        if (!IsRegionalChampionship)
            return string.Empty;

        int total = GetChampionshipTotalStandardRaceCount();
        int completed = GetChampionshipCompletedStandardRaceCount();
        if (total <= 0)
            return "No qualifying regional races found.";

        if (completed >= total)
            return "Unlocked";

        return $"Complete League {ChampionshipRequiredLeagueNumber} on all regional races ({completed}/{total}).";
    }

    public bool HasCompletedLeague(int leagueNumber)
    {
        return PlayerPrefs.GetInt(GetLeagueCompletedKey(leagueNumber), 0) == 1;
    }

    public bool HasWonLeague(int leagueNumber)
    {
        return GetBestPlacement(leagueNumber) == 1;
    }

    public int GetBestPlacement(int leagueNumber)
    {
        return PlayerPrefs.GetInt(GetLeagueBestPlacementKey(leagueNumber), -1);
    }

    public float GetBestTimeSeconds(int leagueNumber)
    {
        return PlayerPrefs.GetFloat(GetLeagueBestTimeKey(leagueNumber), -1f);
    }

    public int GetPreviousLeagueNumber(int leagueNumber)
    {
        if (leagues == null || leagues.Count == 0)
            return 0;

        int idx = GetLeagueIndex(leagueNumber);
        if (idx <= 0)
            return 0;

        return Mathf.Max(1, leagues[idx - 1].leagueNumber);
    }

    public int GetNextLeagueNumber(int leagueNumber)
    {
        if (leagues == null || leagues.Count == 0)
            return 0;

        int idx = GetLeagueIndex(leagueNumber);
        if (idx < 0 || idx >= leagues.Count - 1)
            return 0;

        return Mathf.Max(1, leagues[idx + 1].leagueNumber);
    }

    private int GetNormalizedSelectedLeagueNumber()
    {
        if (leagues == null || leagues.Count == 0)
            return Mathf.Max(1, defaultLeagueNumber);

        int candidate = Mathf.Max(1, _selectedLeagueNumber);
        if (GetLeague(candidate) != null && IsLeagueUnlocked(candidate))
            return candidate;

        int fallback = GetHighestUnlockedLeagueNumber();
        if (GetLeague(fallback) != null)
            return fallback;

        return Mathf.Max(1, leagues[0].leagueNumber);
    }

    public int GetHighestUnlockedLeagueNumber()
    {
        if (leagues == null || leagues.Count == 0)
            return Mathf.Max(1, defaultLeagueNumber);

        int highestUnlocked = 0;
        for (int i = 0; i < leagues.Count; i++)
        {
            RaceLeagueDefinition league = leagues[i];
            if (league == null)
                continue;

            if (IsLeagueUnlocked(league.leagueNumber))
                highestUnlocked = Mathf.Max(highestUnlocked, Mathf.Max(1, league.leagueNumber));
        }

        if (highestUnlocked > 0)
            return highestUnlocked;

        int fallback = Mathf.Max(1, defaultLeagueNumber);
        return GetLeague(fallback) != null ? fallback : Mathf.Max(1, leagues[0].leagueNumber);
    }

    private int GetLeagueIndex(int leagueNumber)
    {
        if (leagues == null)
            return -1;

        for (int i = 0; i < leagues.Count; i++)
        {
            if (leagues[i] != null && leagues[i].leagueNumber == leagueNumber)
                return i;
        }

        return -1;
    }

    private string GetRacePrefPrefix()
    {
        return $"skigame.slot.{GameSaveSystem.ActiveSlotId}.race.{raceId}.";
    }

    private string GetLeagueCompletedKey(int leagueNumber)
    {
        return $"{GetRacePrefPrefix()}league.{Mathf.Max(1, leagueNumber)}.completed";
    }

    private string GetLeagueBestPlacementKey(int leagueNumber)
    {
        return $"{GetRacePrefPrefix()}league.{Mathf.Max(1, leagueNumber)}.bestPlacement";
    }

    private string GetLeagueBestTimeKey(int leagueNumber)
    {
        return $"{GetRacePrefPrefix()}league.{Mathf.Max(1, leagueNumber)}.bestTime";
    }

    private string GetLeagueCompletionOrderKey(int leagueNumber)
    {
        return $"{GetRacePrefPrefix()}league.{Mathf.Max(1, leagueNumber)}.completionOrder";
    }

    private static string GetRacePrefPrefixForSlot(string raceId, int slotId)
    {
        return $"skigame.slot.{Mathf.Clamp(slotId, 0, GameSaveSystem.MaxSlots - 1)}.race.{raceId}.";
    }

    private static string GetLeagueCompletionSequenceCounterKeyForSlot(int slotId)
    {
        return $"skigame.slot.{Mathf.Clamp(slotId, 0, GameSaveSystem.MaxSlots - 1)}.race.completionSequenceCounter";
    }

    private string GetLeagueCompletionSequenceCounterKey()
    {
        return GetLeagueCompletionSequenceCounterKeyForSlot(GameSaveSystem.ActiveSlotId);
    }

    private int EnsureLeagueCompletionOrderRecorded(int leagueNumber)
    {
        leagueNumber = Mathf.Max(1, leagueNumber);

        if (!HasCompletedLeague(leagueNumber))
            return 0;

        string orderKey = GetLeagueCompletionOrderKey(leagueNumber);
        int existingOrder = PlayerPrefs.GetInt(orderKey, 0);
        if (existingOrder > 0)
            return existingOrder;

        string counterKey = GetLeagueCompletionSequenceCounterKey();
        int nextOrder = Mathf.Max(1, PlayerPrefs.GetInt(counterKey, 0) + 1);
        PlayerPrefs.SetInt(counterKey, nextOrder);
        PlayerPrefs.SetInt(orderKey, nextOrder);
        PlayerPrefs.Save();
        return nextOrder;
    }

    public int GetLeagueCompletionOrder(int leagueNumber, bool createIfMissing = false)
    {
        leagueNumber = Mathf.Max(1, leagueNumber);

        if (!HasCompletedLeague(leagueNumber))
            return 0;

        return createIfMissing
            ? EnsureLeagueCompletionOrderRecorded(leagueNumber)
            : PlayerPrefs.GetInt(GetLeagueCompletionOrderKey(leagueNumber), 0);
    }

    public static void ClearPersistentProgressForSlot(int slotId)
    {
        slotId = Mathf.Clamp(slotId, 0, GameSaveSystem.MaxSlots - 1);
        PlayerPrefs.DeleteKey(GetLeagueCompletionSequenceCounterKeyForSlot(slotId));

        RaceCourseLine[] races = FindObjectsOfType<RaceCourseLine>(includeInactive: true);
        if (races == null || races.Length == 0)
        {
            PlayerPrefs.Save();
            return;
        }

        for (int i = 0; i < races.Length; i++)
        {
            RaceCourseLine race = races[i];
            if (race == null)
                continue;

            race.EnsureRaceId();
            if (string.IsNullOrWhiteSpace(race.raceId))
                continue;

            string prefix = GetRacePrefPrefixForSlot(race.raceId, slotId);

            if (race.leagues != null)
            {
                for (int l = 0; l < race.leagues.Count; l++)
                {
                    RaceLeagueDefinition league = race.leagues[l];
                    if (league == null)
                        continue;

                    int leagueNumber = Mathf.Max(1, league.leagueNumber);
                    PlayerPrefs.DeleteKey($"{prefix}league.{leagueNumber}.completed");
                    PlayerPrefs.DeleteKey($"{prefix}league.{leagueNumber}.bestPlacement");
                    PlayerPrefs.DeleteKey($"{prefix}league.{leagueNumber}.bestTime");
                    PlayerPrefs.DeleteKey($"{prefix}league.{leagueNumber}.completionOrder");
                }
            }
        }

        PlayerPrefs.Save();
    }
    private int GetPlacementBonus(int placement)
    {
        switch (placement)
        {
            case 1: return firstPlaceBonus;
            case 2: return secondPlaceBonus;
            case 3: return thirdPlaceBonus;
            default: return 0;
        }
    }

    private void ResolveCompletionRewardsAndProgression()
    {
        int placement = RaceActivityService.Instance != null
            ? RaceActivityService.Instance.FinishedNpcCount + 1
            : 1;

        int entrantCount = (RaceActivityService.Instance != null
            ? RaceActivityService.Instance.ActiveNpcCount
            : 0) + 1;

        SetResolvedPlacement(placement, entrantCount);

        _lastRewardGranted = 0;
        _lastLeagueCompletedForFirstTime = false;
        _lastUnlockedLeagueNumber = 0;
        _lastPersonalBestImproved = false;
        _lastChampionshipCompletedForFirstTime = false;
        _lastChampionshipRewardSummary = string.Empty;

        int leagueNumber = Mathf.Max(1, _activeLeagueNumber);
        bool firstTimeCompletion = !HasCompletedLeague(leagueNumber);
        bool hadWonBefore = HasWonLeague(leagueNumber);

        PlayerPrefs.SetInt(GetLeagueCompletedKey(leagueNumber), 1);
        EnsureLeagueCompletionOrderRecorded(leagueNumber);

        int previousBestPlacement = GetBestPlacement(leagueNumber);
        float previousBestTime = GetBestTimeSeconds(leagueNumber);

        bool improvedPlacement =
            previousBestPlacement <= 0 ||
            placement < previousBestPlacement ||
            (placement == previousBestPlacement && (previousBestTime < 0f || _attemptTime < previousBestTime));

        if (improvedPlacement)
        {
            PlayerPrefs.SetInt(GetLeagueBestPlacementKey(leagueNumber), placement);
            PlayerPrefs.SetFloat(GetLeagueBestTimeKey(leagueNumber), _attemptTime);
            _lastPersonalBestImproved = true;
        }

        int reward = completionReward + GetPlacementBonus(placement);
        if (firstTimeCompletion)
            reward += firstTimeLeagueCompletionBonus;

        var statsMgr = PlayerStatsManager.Instance;
        if (statsMgr != null && statsMgr.Profile != null && reward > 0)
        {
            ProgressionEventRecorder.AddCurrency(statsMgr.Profile, reward);
            statsMgr.Save();
        }

        if (statsMgr != null && statsMgr.Profile != null)
        {
            ProgressionEventRecorder.RecordRaceCompletion(
                statsMgr.Profile,
                RaceId,
                placement,
                _lastPersonalBestImproved);
            statsMgr.Save();
        }

        if (firstTimeCompletion && IsRegionalChampionship)
        {
            bool newlyCompleted = false;
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(championshipPermanentPassIdReward))
            {
                changed = RaceRescueProgression.TryMarkRaceChampionshipCompleted(
                    RaceId,
                    championshipPermanentPassIdReward,
                    out newlyCompleted);
            }
            else if (championshipPermanentPassLevelReward >= 0)
            {
                changed = RaceRescueProgression.TryMarkRaceChampionshipCompleted(
                    RaceId,
                    championshipPermanentPassLevelReward,
                    out newlyCompleted);
            }

            if (changed)
            {
                SkiPassManager passManager = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();
                if (passManager != null)
                    passManager.ApplyPermanentUnlocksFromProfile(equipBestUnlocked: true);
            }

            if (newlyCompleted)
            {
                _lastChampionshipCompletedForFirstTime = true;
                _lastChampionshipRewardSummary = GetChampionshipRewardSummary();
            }
        }

        _lastRewardGranted = reward;
        _lastLeagueCompletedForFirstTime = firstTimeCompletion;

        if (placement == 1 && !hadWonBefore)
        {
            int nextLeague = GetNextLeagueNumber(leagueNumber);
            if (nextLeague > 0)
                _lastUnlockedLeagueNumber = nextLeague;
        }

        PlayerPrefs.Save();
    }

    private void HandleActivityStarted(MountainActivityKind kind, MonoBehaviour source, string displayName, int variantNumber)
    {
        if (kind != MountainActivityKind.Race || source != this)
            return;

        GameObject startPlayerRoot = _pendingStartPlayerRoot != null
            ? _pendingStartPlayerRoot
            : _playerRootInTrigger;

        _pendingStartPlayerRoot = null;

        Debug.Log($"[RaceCourseLine] HandleActivityStarted received for '{RaceName}' on {name}. PlayerRoot={(startPlayerRoot != null ? startPlayerRoot.name : "null")}", this);
        BeginAttempt(startPlayerRoot, variantNumber);
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

        if (playerRoot == null)
        {
            FailRace("Player missing");
            return;
        }

        Debug.Log($"[RaceCourseLine] BeginAttempt on '{RaceName}'. Generated checkpoints: {(generatedCheckpoints != null ? generatedCheckpoints.Count : 0)}", this);

        ResolveGeneratedCheckpoints();

        _attemptActive = true;
        _activeLeagueNumber = Mathf.Max(1, leagueNumber);
        _lastAttemptLeagueNumber = _activeLeagueNumber;
        _nextCheckpointIndex = 0;
        _attemptTime = 0f;
        _offCourseTimer = 0f;
        _skisOffTimer = 0f;
        _startStackRecoveryRemaining = Mathf.Max(0f, startStackRecoveryGraceSeconds);
        _activePlayerRoot = playerRoot;
        _lastAttemptPlayerRoot = playerRoot;

        _activePlayerSkiController = FindPlayerComponent<SkiController>(playerRoot);
        _activePlayerWalkingController = FindPlayerComponent<WalkingController>(playerRoot);

        _attemptAccumulatedTrickScore = 0;
        _attemptBestTrickScore = 0;
        _attemptMatchedRequiredTrick = false;
        _lastLeagueObjectiveSummary = BuildLeagueObjectiveSummary(_activeLeagueNumber);
        _lastFailureReason = string.Empty;

        _lastCountdownWholeSeconds = -1;

        BindActivePlayerTrickTracker();

        ApplyRaceTimePause(true);

        if (PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Profile != null)
        {
            ProgressionEventRecorder.RecordRaceStart(PlayerStatsManager.Instance.Profile);
            PlayerStatsManager.Instance.Save();
        }

        PreparePlayerForRaceStart(playerRoot);

        if (snapPlayerToStartOnBegin)
            SnapPlayerToStart(playerRoot);

        EnsurePlayerRaceControlEnabled(playerRoot);

        _countdownStartPlayerPosition = playerRoot.transform.position;
        _countdownStartPlayerRotation = playerRoot.transform.rotation;
        _hasCountdownStartPose = true;

        _countdownRemaining = Mathf.Max(0f, startCountdownSeconds);
        _runtimeState = _countdownRemaining > 0f
            ? RaceRuntimeState.Countdown
            : RaceRuntimeState.Racing;

        if (_runtimeState == RaceRuntimeState.Racing)
        {
            EnsurePlayerRaceControlEnabled(playerRoot);
            OnRaceCountdownGo?.Invoke(this);
        }

        if (Application.isPlaying)
        {
            EnsureRuntimeCheckpointVisuals();
            RebindGeneratedCheckpointTriggers();
            SetCheckpointVisualsVisible(true);
            SetGeneratedCheckpointTriggersEnabled(true);
            RefreshCheckpointVisualStates();

            if (_runtimeState == RaceRuntimeState.Racing)
                TryConsumeCurrentCheckpointIfAlreadyOverlapping();

            Debug.Log($"[RaceCourseLine] Checkpoints enabled for '{RaceName}'. RuntimeState={_runtimeState}", this);
        }
    }

    private void ResetAttemptState()
    {
        ApplyRaceTimePause(false);
        UnbindActivePlayerTrickTracker();

        _attemptActive = false;
        _runtimeState = RaceRuntimeState.Idle;
        _countdownRemaining = 0f;
        _activeLeagueNumber = 0;
        _nextCheckpointIndex = 0;
        _attemptTime = 0f;
        _offCourseTimer = 0f;
        _skisOffTimer = 0f;
        _startStackRecoveryRemaining = 0f;
        _activePlayerRoot = null;
        _activePlayerSkiController = null;
        _activePlayerWalkingController = null;
        _attemptAccumulatedTrickScore = 0;
        _attemptBestTrickScore = 0;
        _attemptMatchedRequiredTrick = false;
        _lastLeagueObjectiveSummary = string.Empty;
        _hasCountdownStartPose = false;

        _wasPressed = false;
        _held = 0f;

        if (Application.isPlaying)
        {
            RefreshCheckpointVisualStates();
            SetGeneratedCheckpointTriggersEnabled(false);
            SetCheckpointVisualsVisible(false);
        }
    }

    private void BindActivePlayerTrickTracker()
    {
        UnbindActivePlayerTrickTracker();

        if (_activePlayerRoot == null)
            return;

        _activePlayerTrickTracker = _activePlayerRoot.GetComponentInChildren<SkierTrickTracker>(true);
        if (_activePlayerTrickTracker == null)
            _activePlayerTrickTracker = _activePlayerRoot.GetComponentInParent<SkierTrickTracker>();

        if (_activePlayerTrickTracker != null)
            _activePlayerTrickTracker.OnTrickResolved += HandleAttemptTrickResolved;
    }

    private void UnbindActivePlayerTrickTracker()
    {
        if (_activePlayerTrickTracker == null)
            return;

        _activePlayerTrickTracker.OnTrickResolved -= HandleAttemptTrickResolved;
        _activePlayerTrickTracker = null;
    }

    private void HandleAttemptTrickResolved(SkierTrickTracker.TrickResult result)
    {
        if (!_attemptActive || !result.success)
            return;

        RaceLeagueDefinition league = GetLeague(_activeLeagueNumber);
        int resolvedScore = result.comboScore;
        if (league != null)
        {
            TrickScoreBreakdown breakdown = TrickActivityRules.Score(
                TrickActivityRules.CreateDescriptor(result),
                history: null,
                emphasis: league.trickScoreEmphasis);
            resolvedScore = breakdown.finalScore;
        }

        _attemptAccumulatedTrickScore += Mathf.Max(0, resolvedScore);
        _attemptBestTrickScore = Mathf.Max(_attemptBestTrickScore, resolvedScore);

        if (league != null && league.requiredTrickRule != null && !league.requiredTrickRule.IsEmpty())
        {
            if (TrickActivityRules.Matches(league.requiredTrickRule, TrickActivityRules.CreateDescriptor(result)))
                _attemptMatchedRequiredTrick = true;
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

        if (_nextCheckpointIndex >= generatedCheckpoints.Count)
        {
            CompleteRace();
            return true;
        }

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
        int placement = RaceActivityService.Instance != null
            ? RaceActivityService.Instance.FinishedNpcCount + 1
            : 1;

        if (!TryEvaluateLeagueObjectives(placement, out string objectiveFailure))
        {
            FailRace(objectiveFailure);
            return;
        }

        _lastFailureReason = string.Empty;
        _lastCompletionTimeSeconds = _attemptTime;
        ResolveCompletionRewardsAndProgression();

        var mgr = MountainActivityManager.Instance;
        if (mgr != null)
            mgr.Complete(MountainActivityKind.Race, this, "Finished");
    }

    private void FailRace(string reason)
    {
        _lastFailureReason = string.IsNullOrWhiteSpace(reason) ? "Race failed" : reason;
        _lastCompletionTimeSeconds = -1f;

        var mgr = MountainActivityManager.Instance;
        if (mgr != null)
            mgr.Fail(MountainActivityKind.Race, this, _lastFailureReason);
    }

    public void SetSelectedLeagueExternal(int leagueNumber)
    {
        if (leagues == null || leagues.Count == 0)
            return;

        leagueNumber = Mathf.Max(1, leagueNumber);
        if (GetLeague(leagueNumber) == null)
            return;

        if (!IsLeagueUnlocked(leagueNumber))
            leagueNumber = GetHighestUnlockedLeagueNumber();

        _selectedLeagueNumber = leagueNumber;
    }

    public bool CanStartLeagueExternal(int leagueNumber)
    {
        leagueNumber = Mathf.Max(1, leagueNumber);

        if (_attemptActive)
            return false;

        if (pointsWorld == null || pointsWorld.Count < 2)
            return false;

        if (!IsLeagueUnlocked(leagueNumber))
            return false;

        var mgr = MountainActivityManager.Instance;
        return mgr != null && mgr.CanStart(MountainActivityKind.Race, this);
    }

    public bool TryStartLeagueExternal(GameObject playerRoot, int leagueNumber)
    {
        leagueNumber = Mathf.Max(1, leagueNumber);

        if (playerRoot == null)
            return false;

        if (pointsWorld == null || pointsWorld.Count < 2)
            return false;

        if (!IsLeagueUnlocked(leagueNumber))
        {
            ShowLockedLeaguePopup(leagueNumber);
            return false;
        }

        var mgr = MountainActivityManager.Instance;
        if (mgr == null || !mgr.CanStart(MountainActivityKind.Race, this))
            return false;

        _selectedLeagueNumber = leagueNumber;
        _pendingStartPlayerRoot = playerRoot;
        _lastCountdownWholeSeconds = -1;

        return mgr.TryStart(MountainActivityKind.Race, this, RaceName, leagueNumber);
    }

    public bool TryRestartLastAttempt()
    {
        if (_attemptActive)
            return false;

        int restartLeague = Mathf.Max(1, _lastAttemptLeagueNumber > 0 ? _lastAttemptLeagueNumber : SelectedLeagueNumber);

        GameObject playerRoot = _lastAttemptPlayerRoot != null
            ? _lastAttemptPlayerRoot
            : _playerRootInTrigger;

        if (playerRoot == null)
            return false;

        var mgr = MountainActivityManager.Instance;
        if (mgr == null || !mgr.CanStart(MountainActivityKind.Race, this))
            return false;

        _pendingStartPlayerRoot = playerRoot;
        return mgr.TryStart(MountainActivityKind.Race, this, RaceName, restartLeague);
    }

    private void ApplyRaceTimePause(bool paused)
    {
        var tc = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
        if (tc == null)
            return;

        if (paused)
        {
            if (_ownsExternalTimePause)
                return;

            tc.PushExternalPause();
            _ownsExternalTimePause = true;
        }
        else
        {
            if (!_ownsExternalTimePause)
                return;

            tc.PopExternalPause();
            _ownsExternalTimePause = false;
        }
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
        if (IsRegionalChampionship)
            return "Championship";

        var l = GetLeague(leagueNumber);
        return l != null ? l.GetResolvedName() : $"League {Mathf.Max(1, leagueNumber)}";
    }

    public string GetChampionshipRewardSummary()
    {
        if (!IsRegionalChampionship)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(championshipPermanentPassIdReward))
        {
            SkiPassManager mgr = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();
            SkiPassConfigSO cfg = mgr != null ? mgr.Config : null;

            if (cfg != null)
            {
                var pass = cfg.GetByPassId(championshipPermanentPassIdReward);
                if (pass != null)
                    return $"{pass.displayName} unlocked permanently";
            }

            return $"{championshipPermanentPassIdReward} unlocked permanently";
        }

        if (championshipPermanentPassLevelReward < 0)
            return string.Empty;

        return $"Pass tier L{championshipPermanentPassLevelReward} unlocked permanently";
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

    public string BuildLeagueObjectiveSummary(int leagueNumber)
    {
        RaceLeagueDefinition league = GetLeague(leagueNumber);
        if (league == null)
            return string.Empty;

        List<string> goals = new List<string>();

        if (league.requiredPlacementToClear >= 99)
            goals.Add("Finish the race");
        else if (league.requiredPlacementToClear > 1)
            goals.Add($"Place {ToOrdinal(league.requiredPlacementToClear)} or better");
        else
            goals.Add("Win the race");

        if (league.optionalTimeLimitSeconds > 0f)
            goals.Add($"Beat {league.optionalTimeLimitSeconds:0.0}s");

        if (league.optionalTrickScoreThreshold > 0)
            goals.Add($"Score {league.optionalTrickScoreThreshold}+ trick pts");

        if (league.requiredTrickRule != null && !league.requiredTrickRule.IsEmpty())
            goals.Add(league.requiredTrickRule.BuildSummary());

        return string.Join(" • ", goals);
    }

    private bool TryEvaluateLeagueObjectives(int placement, out string failureReason)
    {
        failureReason = string.Empty;
        RaceLeagueDefinition league = GetLeague(_activeLeagueNumber);
        if (league == null)
            return true;

        int requiredPlacement = Mathf.Max(1, league.requiredPlacementToClear);
        if (requiredPlacement < 99 && placement > requiredPlacement)
        {
            failureReason = requiredPlacement <= 1
                ? "League objective failed: finish in 1st."
                : $"League objective failed: place {ToOrdinal(requiredPlacement)} or better.";
            return false;
        }

        if (league.optionalTrickScoreThreshold > 0 && _attemptAccumulatedTrickScore < league.optionalTrickScoreThreshold)
        {
            failureReason = $"League objective failed: score {league.optionalTrickScoreThreshold}+ trick points.";
            return false;
        }

        if (league.requiredTrickRule != null && !league.requiredTrickRule.IsEmpty() && !_attemptMatchedRequiredTrick)
        {
            failureReason = $"League objective failed: {league.requiredTrickRule.BuildSummary()}.";
            return false;
        }

        return true;
    }

    private static string ToOrdinal(int value)
    {
        value = Mathf.Max(1, value);
        int mod100 = value % 100;
        if (mod100 is >= 11 and <= 13)
            return $"{value}th";

        return (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        };
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

    private void TryConsumeCurrentCheckpointIfAlreadyOverlapping()
    {
        if (!_attemptActive || _runtimeState != RaceRuntimeState.Racing)
            return;

        if (_nextCheckpointIndex < 0 || _nextCheckpointIndex >= _runtimeCheckpointObjects.Count)
            return;

        GameObject checkpointObject = _runtimeCheckpointObjects[_nextCheckpointIndex];
        if (checkpointObject == null || !checkpointObject.activeInHierarchy)
            return;

        BoxCollider box = checkpointObject.GetComponent<BoxCollider>();
        if (box == null || !box.enabled)
            return;

        Vector3 worldCenter = checkpointObject.transform.TransformPoint(box.center);
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, checkpointObject.transform.lossyScale);
        Quaternion orientation = checkpointObject.transform.rotation;

        Collider[] hits = Physics.OverlapBox(
            worldCenter,
            halfExtents,
            orientation,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            if (ColliderBelongsToActivePlayer(hits[i]))
            {
                Vector3 audioPosition = checkpointObject != null
                    ? checkpointObject.transform.position
                    : worldCenter;

                _nextCheckpointIndex++;
                GameAudio.PlayWorld(GameAudioCueId.RaceCheckpoint, audioPosition);
                RefreshCheckpointVisualStates();
                break;
            }
        }
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

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock _sharedPropertyBlock;

    private static void SetRendererColour(Renderer renderer, Color colour)
    {
        if (renderer == null)
            return;

        Material referenceMaterial = null;

        if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
            referenceMaterial = renderer.sharedMaterials[0];

        if (referenceMaterial == null)
            return;

        bool hasColor = referenceMaterial.HasProperty(ColorId);
        bool hasBaseColor = referenceMaterial.HasProperty(BaseColorId);

        if (!hasColor && !hasBaseColor)
            return;

        if (_sharedPropertyBlock == null)
            _sharedPropertyBlock = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(_sharedPropertyBlock);

        if (hasColor)
            _sharedPropertyBlock.SetColor(ColorId, colour);

        if (hasBaseColor)
            _sharedPropertyBlock.SetColor(BaseColorId, colour);

        renderer.SetPropertyBlock(_sharedPropertyBlock);
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
        RegenerateCheckpointVisuals(recordUndo: true);
    }

    private void RegenerateCheckpointVisuals(bool recordUndo)
    {
        if (Application.isPlaying)
            return;

        if (_isRefreshingCheckpointVisuals)
            return;

        _isRefreshingCheckpointVisuals = true;
        try
        {
            Transform container = GetOrCreateGeneratedContainer(recordUndo);
            int checkpointCount = generatedCheckpoints != null ? generatedCheckpoints.Count : 0;

            for (int i = 0; i < checkpointCount; i++)
            {
                var cp = generatedCheckpoints[i];
                GameObject go = i < container.childCount
                    ? container.GetChild(i).gameObject
                    : CreateCheckpointVisualObject(container, i, recordUndo);

                ConfigureCheckpointVisualObject(go, cp, i);
            }

            for (int i = container.childCount - 1; i >= checkpointCount; i--)
            {
                DestroyCheckpointVisualObject(container.GetChild(i).gameObject, recordUndo);
            }

            _lastCheckpointVisualHash = ComputeCheckpointVisualHash();
            _hasCheckpointVisualHash = true;
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

        _hasCheckpointVisualHash = false;
    }

    public void QueueCheckpointVisualRefresh()
    {
        if (Application.isPlaying)
            return;

        _checkpointVisualRefreshRequestedAt = EditorApplication.timeSinceStartup;

        int id = GetInstanceID();
        if (_queuedVisualRefreshIds.Contains(id))
            return;

        _queuedVisualRefreshIds.Add(id);

        EditorApplication.delayCall += () => ProcessQueuedCheckpointVisualRefresh(id);
    }

    private void ProcessQueuedCheckpointVisualRefresh(int id)
    {
        if (this == null)
        {
            _queuedVisualRefreshIds.Remove(id);
            return;
        }

        double elapsed = EditorApplication.timeSinceStartup - _checkpointVisualRefreshRequestedAt;
        if (elapsed < CheckpointVisualRefreshDebounceSeconds)
        {
            EditorApplication.delayCall += () => ProcessQueuedCheckpointVisualRefresh(id);
            return;
        }

        _queuedVisualRefreshIds.Remove(id);

        if (Application.isPlaying)
            return;

        int visualHash = ComputeCheckpointVisualHash();
        if (!NeedsCheckpointVisualRefresh(visualHash))
            return;

        RegenerateCheckpointVisuals(recordUndo: false);
        EditorUtility.SetDirty(this);
    }

    private Transform GetOrCreateGeneratedContainer(bool recordUndo)
    {
        var t = transform.Find(generatedContainerName);
        if (t != null) return t;

        var go = new GameObject(generatedContainerName);
        if (recordUndo)
            Undo.RegisterCreatedObjectUndo(go, "Create Checkpoint Container");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private GameObject CreateCheckpointVisualObject(Transform container, int checkpointIndex, bool recordUndo)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Checkpoint_{checkpointIndex + 1:00}";
        if (recordUndo)
            Undo.RegisterCreatedObjectUndo(go, "Create Race Checkpoint Gate");

        go.transform.SetParent(container, true);
        go.hideFlags = HideFlags.None;
        return go;
    }

    private void ConfigureCheckpointVisualObject(GameObject go, GeneratedCheckpoint cp, int checkpointIndex)
    {
        if (go == null)
            return;

        go.name = $"Checkpoint_{checkpointIndex + 1:00}";

        Vector3 upOffset = Vector3.up * (cp.height * 0.5f);
        go.transform.position = cp.worldPos + upOffset;
        go.transform.rotation = cp.forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(cp.forward.normalized, Vector3.up)
            : Quaternion.identity;
        SetDesiredWorldScale(go.transform, new Vector3(cp.width, cp.height, cp.depth));

        var collider = go.GetComponent<BoxCollider>();
        if (collider == null)
            collider = go.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        collider.size = Vector3.one;

        var trigger = go.GetComponent<RaceCheckpointTrigger>();
        if (trigger == null)
            trigger = go.AddComponent<RaceCheckpointTrigger>();

        trigger.Initialize(this, checkpointIndex);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && checkpointMaterial != null)
            renderer.sharedMaterial = checkpointMaterial;

        var heightNormalizer = go.GetComponent<InteractionAreaHeightNormalizer>();
        if (heightNormalizer == null)
            heightNormalizer = go.AddComponent<InteractionAreaHeightNormalizer>();

        heightNormalizer.SetContinuousUpdates(false);
    }

    private static void DestroyCheckpointVisualObject(GameObject go, bool recordUndo)
    {
        if (go == null)
            return;

        if (recordUndo)
            Undo.DestroyObjectImmediate(go);
        else
            DestroyImmediate(go);
    }

    private bool NeedsCheckpointVisualRefresh(int visualHash)
    {
        if (!_hasCheckpointVisualHash || visualHash != _lastCheckpointVisualHash)
            return true;

        Transform container = transform.Find(generatedContainerName);
        int checkpointCount = generatedCheckpoints != null ? generatedCheckpoints.Count : 0;
        if (checkpointCount == 0)
            return container != null && container.childCount > 0;

        if (container == null || container.childCount != checkpointCount)
            return true;

        for (int i = 0; i < checkpointCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null)
                return true;

            if (child.GetComponent<BoxCollider>() == null || child.GetComponent<RaceCheckpointTrigger>() == null)
                return true;
        }

        return false;
    }

    private int ComputeCheckpointVisualHash()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (checkpointMaterial != null ? checkpointMaterial.GetInstanceID() : 0);
            hash = (hash * 31) + (generatedContainerName != null ? generatedContainerName.GetHashCode() : 0);
            hash = (hash * 31) + (generatedCheckpoints != null ? generatedCheckpoints.Count : 0);

            if (generatedCheckpoints != null)
            {
                for (int i = 0; i < generatedCheckpoints.Count; i++)
                {
                    GeneratedCheckpoint checkpoint = generatedCheckpoints[i];
                    hash = (hash * 31) + checkpoint.worldPos.GetHashCode();
                    hash = (hash * 31) + checkpoint.forward.GetHashCode();
                    hash = (hash * 31) + checkpoint.width.GetHashCode();
                    hash = (hash * 31) + checkpoint.height.GetHashCode();
                    hash = (hash * 31) + checkpoint.depth.GetHashCode();
                }
            }

            return hash;
        }
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

        for (int i = leagues.Count - 1; i >= 0; i--)
        {
            if (leagues[i] == null)
                leagues.RemoveAt(i);
        }

        var used = new HashSet<int>();
        for (int i = 0; i < leagues.Count; i++)
        {
            var l = leagues[i];
            l.leagueNumber = Mathf.Max(1, l.leagueNumber);

            while (used.Contains(l.leagueNumber))
                l.leagueNumber++;

            used.Add(l.leagueNumber);

            if (string.IsNullOrWhiteSpace(l.displayName))
                l.displayName = $"League {l.leagueNumber}";
        }

        leagues.Sort((a, b) => a.leagueNumber.CompareTo(b.leagueNumber));
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

    public bool TryGetActivePlayerRaceState(out float distanceAlong, out float lateralDistance)
    {
        distanceAlong = 0f;
        lateralDistance = float.PositiveInfinity;

        if (_activePlayerRoot == null)
            return false;

        return TryProjectPointOntoCourse(_activePlayerRoot.transform.position, out distanceAlong, out lateralDistance, out _);
    }

    public Vector3 GetNpcStartSlotWorldPosition(int slotIndex)
    {
        Vector3 forward = StartForward.sqrMagnitude > 0.0001f
            ? StartForward.normalized
            : transform.forward;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude <= 0.0001f)
            right = transform.right;

        Vector3 playerStart = GetPlayerStartWorldPosition();

        // NPCs should stage down-course from the player's snapped start position,
        // not around StartWorldPosition, otherwise the first NPC row can overlap
        // the player when playerStartBackOffsetMeters is greater than 0.
        Vector3 npcGridOrigin =
            playerStart
            + forward * Mathf.Max(0f, npcStartLeadFromPlayerMeters)
            + npcStartAreaOffset;

        int row = Mathf.Max(0, slotIndex / 3);
        int col = Mathf.Max(0, slotIndex % 3);

        float lateral = (col - 1) * npcLaneSpacingMeters;

        // Slightly stagger alternate rows so racers do not form a perfectly
        // rigid 3-wide wall directly in front of the player.
        if ((row & 1) == 1)
            lateral += npcLaneSpacingMeters * 0.5f;

        float forwardOffset = row * Mathf.Max(0.01f, npcRowSpacingMeters);

        Vector3 desiredPos = npcGridOrigin + forward * forwardOffset + right * lateral;
        return ResolveSafeStartPosition(desiredPos, forward);
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

        Vector3 startPos = GetPlayerStartWorldPosition();
        Vector3 forward = StartForward.sqrMagnitude > 0.0001f ? StartForward : transform.forward;
        Vector3 safeStartPos = ResolveSafeStartPosition(startPos, forward);

        Rigidbody playerRb = FindPlayerComponent<Rigidbody>(playerRoot);
        if (playerRb != null)
        {
            playerRb.isKinematic = false;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        playerRoot.transform.position = safeStartPos;

        if (alignPlayerToStartOnBegin)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude <= 0.0001f)
                flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            if (flatForward.sqrMagnitude > 0.0001f)
                playerRoot.transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        Physics.SyncTransforms();

        if (_activePlayerSkiController != null)
        {
            _activePlayerSkiController.ResetStackStateSilently(snapUpright: true, forwardHint: forward);
            _activePlayerSkiController.SnapToGroundClearance(resetDownwardVelocity: true, iterations: 4);
        }
    }

    private void PreparePlayerForRaceStart(GameObject playerRoot)
    {
        if (playerRoot == null)
            return;

        if (_activePlayerWalkingController == null)
            _activePlayerWalkingController = FindPlayerComponent<WalkingController>(playerRoot);

        if (_activePlayerSkiController == null)
            _activePlayerSkiController = FindPlayerComponent<SkiController>(playerRoot);

        if (_activePlayerWalkingController != null)
        {
            _activePlayerWalkingController.ControlsEnabled = true;
            _activePlayerWalkingController.ClearExternalMove();
            _activePlayerWalkingController.ForceEnterSkiMode();
        }

        if (_activePlayerSkiController != null)
            _activePlayerSkiController.enabled = true;

        Vector3 forward = StartForward.sqrMagnitude > 0.0001f ? StartForward : transform.forward;
        if (_activePlayerSkiController != null)
            _activePlayerSkiController.ResetStackStateSilently(snapUpright: true, forwardHint: forward);
    }

    private void RecoverPlayerFromStartStack()
    {
        if (_activePlayerRoot == null)
            return;

        PreparePlayerForRaceStart(_activePlayerRoot);
        if (snapPlayerToStartOnBegin)
            SnapPlayerToStart(_activePlayerRoot);
    }

    private void HoldPlayerAtCountdownStart()
    {
        if (_activePlayerRoot == null || !_hasCountdownStartPose)
            return;

        _activePlayerRoot.transform.SetPositionAndRotation(_countdownStartPlayerPosition, _countdownStartPlayerRotation);

        if (_activePlayerWalkingController != null)
        {
            _activePlayerWalkingController.ControlsEnabled = true;
            _activePlayerWalkingController.ClearExternalMove();
            _activePlayerWalkingController.ForceEnterSkiMode();
        }

        if (_activePlayerSkiController != null)
        {
            _activePlayerSkiController.enabled = true;
            _activePlayerSkiController.ResetStackStateSilently(snapUpright: true, forwardHint: _countdownStartPlayerRotation * Vector3.forward);
        }

        Rigidbody playerRb = FindPlayerComponent<Rigidbody>(_activePlayerRoot);
        if (playerRb != null)
        {
            playerRb.isKinematic = false;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }
    }

    private Vector3 ResolveSafeStartPosition(Vector3 desiredStartPos, Vector3 forward)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.0001f)
            flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

        if (flatForward.sqrMagnitude <= 0.0001f)
            flatForward = Vector3.forward;

        flatForward.Normalize();

        float probeUp = Mathf.Max(safeStartGroundProbeUp, 30f);
        float probeDown = Mathf.Max(safeStartGroundProbeDown, 60f);
        float hover = Mathf.Max(0f, safeStartHoverHeight);
        float sphereRadius = Mathf.Clamp(Mathf.Max(0.15f, hover), 0.15f, 0.75f);

        Vector3 probeOrigin = new Vector3(desiredStartPos.x, desiredStartPos.y + probeUp, desiredStartPos.z);
        float probeDistance = probeUp + probeDown;

        if (Physics.SphereCast(
                probeOrigin,
                sphereRadius,
                Vector3.down,
                out RaycastHit sphereHit,
                probeDistance,
                safeStartGroundMask,
                QueryTriggerInteraction.Ignore))
        {
            Vector3 grounded = sphereHit.point + sphereHit.normal * hover;
            grounded += flatForward * 0.05f;
            return grounded;
        }

        if (Physics.Raycast(
                probeOrigin,
                Vector3.down,
                out RaycastHit rayHit,
                probeDistance,
                safeStartGroundMask,
                QueryTriggerInteraction.Ignore))
        {
            Vector3 grounded = rayHit.point + rayHit.normal * hover;
            grounded += flatForward * 0.05f;
            return grounded;
        }

        if (TrySampleTerrainHeight(desiredStartPos, out float terrainY))
            return new Vector3(desiredStartPos.x, terrainY + hover, desiredStartPos.z) + flatForward * 0.05f;

        return desiredStartPos + Vector3.up * hover;
    }

    private void EnsurePlayerRaceControlEnabled(GameObject playerRoot)
    {
        if (playerRoot == null)
            return;

        if (_activePlayerWalkingController == null)
            _activePlayerWalkingController = FindPlayerComponent<WalkingController>(playerRoot);

        if (_activePlayerSkiController == null)
            _activePlayerSkiController = FindPlayerComponent<SkiController>(playerRoot);

        if (_activePlayerWalkingController != null)
        {
            _activePlayerWalkingController.ControlsEnabled = true;
            _activePlayerWalkingController.ClearExternalMove();
            _activePlayerWalkingController.ForceEnterSkiMode();
        }

        if (_activePlayerSkiController != null)
        {
            _activePlayerSkiController.enabled = true;

            Vector3 forward = StartForward.sqrMagnitude > 0.0001f ? StartForward : playerRoot.transform.forward;
            _activePlayerSkiController.ResetStackStateSilently(snapUpright: true, forwardHint: forward);
        }

        Rigidbody rb = FindPlayerComponent<Rigidbody>(playerRoot);
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        Physics.SyncTransforms();

        if (_activePlayerSkiController != null)
            _activePlayerSkiController.SnapToGroundClearance(resetDownwardVelocity: true, iterations: 3);
    }

    private static T FindPlayerComponent<T>(GameObject playerRoot) where T : Component
    {
        if (playerRoot == null)
            return null;

        T component = playerRoot.GetComponent<T>();
        if (component != null)
            return component;

        component = playerRoot.GetComponentInParent<T>();
        if (component != null)
            return component;

        return playerRoot.GetComponentInChildren<T>(true);
    }

    private static bool TrySampleTerrainHeight(Vector3 worldPosition, out float height)
    {
        Terrain[] terrains = Terrain.activeTerrains;

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            bool insideX = worldPosition.x >= terrainPos.x && worldPosition.x <= terrainPos.x + terrainSize.x;
            bool insideZ = worldPosition.z >= terrainPos.z && worldPosition.z <= terrainPos.z + terrainSize.z;

            if (!insideX || !insideZ)
                continue;

            height = terrain.SampleHeight(worldPosition) + terrainPos.y;
            return true;
        }

        Terrain active = Terrain.activeTerrain;
        if (active != null && active.terrainData != null)
        {
            height = active.SampleHeight(worldPosition) + active.transform.position.y;
            return true;
        }

        height = worldPosition.y;
        return false;
    }

    private static string GetBindingDisplay(InputActionReference actionReference, string fallback)
    {
        if (actionReference == null || actionReference.action == null)
            return string.IsNullOrWhiteSpace(fallback) ? "-" : fallback;

        string display = InputPromptResolver.GetBindingDisplay(actionReference.action);
        if (!string.IsNullOrWhiteSpace(display))
            return display.Trim();

        return string.IsNullOrWhiteSpace(fallback) ? "-" : fallback;
    }

    private string GetInteractBindingDisplay()
    {
        return GetBindingDisplay(interactAction, "E");
    }

    private string GetPreviousLeagueBindingDisplay()
    {
        return GetBindingDisplay(previousLeagueAction, "Q");
    }

    private string GetNextLeagueBindingDisplay()
    {
        return GetBindingDisplay(nextLeagueAction, "E");
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

    public string PromptActionText => $"Hold {GetInteractBindingDisplay()} to start race";
    public string PromptDescriptionText
    {
        get
        {
            string previousBinding = GetPreviousLeagueBindingDisplay();
            string nextBinding = GetNextLeagueBindingDisplay();
            string cyclePrompt = $"Tap {previousBinding} or {nextBinding} to change leagues";

            int leagueNumber = SelectedLeagueNumber;
            string leagueName = GetLeagueDisplayName(leagueNumber);

            if (IsRegionalChampionship)
            {
                if (IsChampionshipUnlocked())
                    return cyclePrompt;

                return $"{cyclePrompt} • {BuildChampionshipLockReason()}";
            }

            if (IsLeagueUnlocked(leagueNumber))
                return cyclePrompt;

            int requiredLeague = GetPreviousLeagueNumber(leagueNumber);
            if (requiredLeague > 0)
                return $"{cyclePrompt} • {leagueName} locked - win {GetLeagueDisplayName(requiredLeague)}";

            return cyclePrompt;
        }
    }

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
