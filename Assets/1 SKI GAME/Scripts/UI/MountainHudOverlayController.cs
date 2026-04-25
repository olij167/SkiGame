using System;
using System.Collections.Generic;
using SkiGame.Map;
using SkiGame.Map.UI;
using SkiGame.Navigation;
using SkiGame.POI;
using SkiGame.Runs;
using SkiGame.UI;
using SkiGame.Activities;
using TimeWeather;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SkiGame.Progression
{
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class MountainHudOverlayController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;

        [Header("Input")]
        [Tooltip("Bind this to the action you want to use to open/close the full mountain overlay.")]
        [SerializeField] private InputActionReference toggleOverlayAction;

        [Header("Start State")]
        [SerializeField] private bool startOpen = false;

        [Header("Linked HUD")]
        [SerializeField] private MiniMountainHudController miniHudController;

        [Header("Map")]
        [SerializeField] private MapData mapData;
        [SerializeField] private MapRegionSet regionSet;
        [SerializeField] private Camera mapReferenceCamera;
        [SerializeField] private MapUIStyleSettings mapStyle;

        [Header("Optional References")]
        [SerializeField] private SkiController skiController;
        [SerializeField] private CameraController cameraController;

        [SerializeField] private RunProgressTracker runProgressTracker;
        [SerializeField] private SkiPassManager skiPassManager;
        [SerializeField] private TimeController timeController;
        [SerializeField] private WeatherController weatherController;
        [SerializeField] private PlayerStatsManager statsManager;
        [SerializeField] private ProgressionDirector progressionDirector;
        [SerializeField] private QuestDirector questDirector;
        [SerializeField] private QuestSignalBus questSignalBus;
        [SerializeField] private PlayerBlackoutTransition blackoutTransition;

        [Header("Refresh")]
        [SerializeField, Range(0.1f, 2f)] private float refreshIntervalSeconds = 0.35f;

        private PointOfInterestRegistry _appliedPoiRegistry;
        private MapUIStyleSettings _appliedMapStyle;

        private VisualElement _root;
        private PhoneMapPageUI _mapUI;

        private Button _btnCloseOverlay;
        private bool _isOpen;
        private float _ignoreToggleUntilTime;

        // Top strip
        private Label _lblTopTime;
        private Label _lblTopWeather;
        private Label _lblTopLocation;
        private Label _lblTopCurrency;
        private Label _lblTopHint;

        private VisualElement _mapSelectionPanel;
        private VisualElement _overlayActionStack;
        private VisualElement _utilityPopupPanel;
        private Label _lblUtilityPopupTitle;
        private VisualElement _achievementModalRoot;
        private Label _achievementBadge;

        // Context panel
        private Label _lblContextTitle;
        private Label _lblContextBody;
        private VisualElement _contextMetaRow;
        private VisualElement _contextHighlights;
        private Button _btnContextExpand;
        private ScrollView _contextAttemptsList;
        private VisualElement _contextDetailRow;

        private Label _lblTopWeatherIcon;

        private Label _lblContextIcon;
        private Label _lblContextStatus;
        private Label _lblContextGuidance;
        private VisualElement _contextGuidanceBox;

        // Stats
        private Button _btnStatsToday;
        private Button _btnStatsLifetime;
        private Label _lblStatsModeSummary;
        private VisualElement _statsGrid;
        private Label _lblStatsBody;
        private bool _statsShowLifetime;

        // Goals
        private Button _btnGoalsQuests;
        private Button _btnGoalsTasks;
        private ScrollView _goalsList;
        private VisualElement _questPanel;
        private ScrollView _questList;
        private Button _btnQuestCurrent;
        private Button _btnQuestCompleted;
        private string _primaryQuestId;
        private readonly HashSet<string> _expandedQuestIds = new();
        private VisualElement _achievementCategoryRow;
        private VisualElement _achievementGrid;
        private GoalsViewMode _goalsViewMode = GoalsViewMode.Tasks;
        private bool _questShowCompleted;
        private AchievementCategory _achievementCategory = AchievementCategory.Performance;

        private string _tutorialLastViewedSection = "Stats";
        private bool _tutorialVisitedStats;
        private bool _tutorialVisitedTasks;
        private bool _tutorialVisitedQuests;
        private bool _tutorialVisitedMap;

        private ScrollView _achievementScroll;
        private Button _btnGoalsAchievements;
        private Button _btnOpenStats;
        private Button _btnOpenRunHistory;
        private Button _btnOpenAchievements;
        private Button _btnOpenPasses;
        private Button _btnOpenRescue;
        private Button _btnCloseUtilityPopup;
        private Button _btnCloseAchievements;
        private ScrollView _runHistoryList;
        private UtilityPanelMode _utilityPanelMode;
        private bool _achievementsModalOpen;
        private bool _contextHistoryExpanded;
        private bool _contextHistoryAvailable;

        private Label _lblRescueDispatchRank;
        private Label _lblRescueDispatchRefresh;
        private Label _lblRescueDispatchBeacon;
        private Label _lblRescueDispatchStatus;
        private Button _btnRescueMedicalDrop;
        private Button _btnRescueBeacon;
        private Button _btnRescueSnowmobile;
        private Button _btnRescueSOS;
        private string _rescueDispatchFeedback = "Complete rescue missions to unlock mountain-wide support utilities.";

        // Pass / rescue panel
        private Button _btnPassTabInfo;
        private Button _btnPassTabRescue;
        private Button _btnPassBack;

        private Label _lblPassCurrent;
        private Label _lblPassExpiryMeta;
        private Label _lblPassPermanentSummary;
        private Label _lblPassDetailTitle;
        private Label _lblPassDetailMeta;

        private ScrollView _passOverviewList;
        private ScrollView _passLiftList;

        private VisualElement _passInfoView;
        private VisualElement _passLiftDetailView;
        private VisualElement _rescueUtilitiesView;
        private VisualElement _passExpiryBar;
        private VisualElement _passExpiryFill;
        private VisualElement _screenFade;

        private bool _passShowRescueTab;
        private bool _passShowLiftDetail;
        private string _passDetailPassId;
        private int _passDetailLevel;
        private string _passDetailDisplayName;
        private float _screenFadeOpacity;
        private Coroutine _sosRoutine;

        private readonly List<PassPanelEntry> _passPanelEntryBuffer = new();
        private readonly List<LiftLine> _passLiftBuffer = new();

        private struct PassPanelEntry
        {
            public string passId;
            public int level;
            public string displayName;
            public bool isCurrent;
            public bool isPermanent;
        }

        private float _nextRefreshTime;
        private bool _pendingInitializeAfterPreview;

        private MapWaypointManager _waypointManager;

        private float _lastWaypointClickTime = -10f;
        private string _lastClickedWaypointId;

        private readonly System.Collections.Generic.List<Vector3> _raceCheckpointMarkerBuffer = new();

        private enum SelectedMapKind
        {
            None = 0,
            Waypoint = 5,
            Run = 10,
            Lift = 20,
            POI = 30,
        }

        private enum UtilityPanelMode
        {
            None = 0,
            Stats = 10,
            RunHistory = 20,
            Passes = 30,
            Rescue = 40,
        }

        private enum AchievementCategory
        {
            Performance = 0,
            Exploration = 10,
            MountainMastery = 20,
        }

        private enum GoalsViewMode
        {
            Quests = 0,
            Tasks = 10,
        }

        private SelectedMapKind _selectedKind;
        private string _selectedId;
        private string _selectedTitle;
        private string _selectedBody;

        private readonly List<ProgressionDirector.DailyTierDisplay> _dailyTierBuffer = new();
        private readonly List<QuestRuntimeState> _questStateBuffer = new();
        private readonly List<QuestDefinitionSO> _questDefinitionBuffer = new();
        private readonly List<AchievementDefinitionSO> _achievementBuffer = new();
        private readonly HashSet<string> _achievementDedup = new();
        private readonly List<AchievementDefinitionSO> _achievementQueryBuffer = new();

        private VisualElement _achievementPanel;
        private VisualElement _achievementHeaderShell;
        private string _selectedAchievementId;

        private static readonly ProgressionMetric[] PerformanceMetrics =
        {
            ProgressionMetric.SessionDistanceMeters,
            ProgressionMetric.LifetimeDistanceMeters,
            ProgressionMetric.SessionTopSpeedMps,
            ProgressionMetric.LifetimeTopSpeedMps,
            ProgressionMetric.SessionAirTimeSeconds,
            ProgressionMetric.LifetimeAirTimeSeconds,
            ProgressionMetric.SessionAirDistanceMeters,
            ProgressionMetric.LifetimeAirDistanceMeters,
            ProgressionMetric.SessionVerticalDescentMeters,
            ProgressionMetric.LifetimeVerticalDescentMeters,
            ProgressionMetric.SessionGrindTimeSeconds,
            ProgressionMetric.LifetimeGrindTimeSeconds,
            ProgressionMetric.SessionGrindDistanceMeters,
            ProgressionMetric.LifetimeGrindDistanceMeters,
            ProgressionMetric.SessionStacks,
            ProgressionMetric.LifetimeStacks,
        };

        private static readonly ProgressionMetric[] ExplorationMetrics =
        {
            ProgressionMetric.SessionPlacesVisited,
            ProgressionMetric.LifetimePlacesVisited,
            ProgressionMetric.SessionLiftsUsed,
            ProgressionMetric.LifetimeLiftsUsed,
            ProgressionMetric.SessionRunsVisited,
            ProgressionMetric.LifetimeRunsVisited,
        };

        private static readonly ProgressionMetric[] MasteryMetrics =
        {
            ProgressionMetric.SessionRunsCompleted,
            ProgressionMetric.LifetimeRunsCompleted,
            ProgressionMetric.SessionRunsCompletedClean,
            ProgressionMetric.LifetimeRunsCompletedClean,
            ProgressionMetric.SessionTopRunSpeedMps,
            ProgressionMetric.LifetimeTopRunSpeedMps,
            ProgressionMetric.LifetimeRunVisited,
            ProgressionMetric.LifetimeRunCompletedCount,
            ProgressionMetric.LifetimeRunCompletedCleanCount,
        };

        private void Reset()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (skiController == null) skiController = FindObjectOfType<SkiController>();
            if (runProgressTracker == null) runProgressTracker = FindObjectOfType<RunProgressTracker>();
            if (skiPassManager == null) skiPassManager = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();
            if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (weatherController == null) weatherController = FindObjectOfType<WeatherController>();
            if (statsManager == null) statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();
            if (progressionDirector == null) progressionDirector = ProgressionDirector.Instance != null ? ProgressionDirector.Instance : FindObjectOfType<ProgressionDirector>();
            if (questDirector == null) questDirector = QuestDirector.Instance != null ? QuestDirector.Instance : FindObjectOfType<QuestDirector>();
            if (questSignalBus == null) questSignalBus = FindObjectOfType<QuestSignalBus>();
            if (blackoutTransition == null) blackoutTransition = FindObjectOfType<PlayerBlackoutTransition>();
        }
        private void Awake()
        {
            ResolveBootstrapReferences();

            if (document != null)
            {
                var root = document.rootVisualElement;
                if (root != null)
                {
                    root.style.display = DisplayStyle.None;
                    root.style.visibility = Visibility.Hidden;
                    root.style.opacity = 0f;
                    root.pickingMode = PickingMode.Ignore;
                }
            }
        }

        private void ResolveBootstrapReferences()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (mapData == null)
                mapData = AutoFindMapData();

            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();

            // Leave null unless explicitly assigned.
            // The map can render accurately from baked MapProjection without a live camera.
            if (mapReferenceCamera == null && mapData != null && mapData.PreferCameraProjection)
            {
                Debug.LogWarning(
                    "[MountainHudOverlayController] MapData prefers camera projection, but no explicit mapReferenceCamera is assigned. " +
                    "Falling back to baked MapProjection is recommended for stable alignment.");
            }

            if (_waypointManager == null)
                _waypointManager = MapWaypointManager.Instance != null
                    ? MapWaypointManager.Instance
                    : MapWaypointManager.EnsureInstance();
        }

        private void OnEnable()
        {
            if (RuntimeSceneLoadContext.IsMenuBackgroundPreview)
            {
                _pendingInitializeAfterPreview = true;
                RuntimeSceneLoadContext.MenuBackgroundPreviewEnded += HandleMenuPreviewEnded;
                return;
            }

            ResolveReferences();
            if (skiPassManager != null)
            {
                skiPassManager.OnPassChanged -= HandleSkiPassChanged;
                skiPassManager.OnPassChanged += HandleSkiPassChanged;
            }

            TryInitializeOverlayOrRetry();
        }

        private void HandleMenuPreviewEnded()
        {
            RuntimeSceneLoadContext.MenuBackgroundPreviewEnded -= HandleMenuPreviewEnded;

            if (!this || !isActiveAndEnabled)
                return;

            if (!_pendingInitializeAfterPreview)
                return;

            _pendingInitializeAfterPreview = false;
            ResolveReferences();
            if (skiPassManager != null)
                skiPassManager.OnPassChanged -= HandleSkiPassChanged;
            TryInitializeOverlayOrRetry();
        }

        private void TryInitializeOverlayOrRetry()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null)
            {
                Debug.LogWarning("[MountainHudOverlayController] Missing UIDocument.");
                return;
            }

            _root = document.rootVisualElement;
            if (_root == null)
            {
                StartCoroutine(RetryInitializeOverlayNextFrame());
                return;
            }

            _root.style.display = DisplayStyle.None;
            _root.style.visibility = Visibility.Hidden;
            _root.style.opacity = 0f;
            _root.pickingMode = PickingMode.Ignore;

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                _root.styleSheets.Add(styleSheet);

            if (!BindUI())
            {
                StartCoroutine(RetryInitializeOverlayNextFrame());
                return;
            }

            BindMap();

            SetOverlayOpen(startOpen, refreshNow: true);
            _ignoreToggleUntilTime = Time.unscaledTime + 0.15f;

            HookInput();
        }

        private System.Collections.IEnumerator RetryInitializeOverlayNextFrame()
        {
            yield return null;

            if (!this || !isActiveAndEnabled)
                yield break;

            ResolveReferences();

            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[MountainHudOverlayController] UIDocument root still not ready after retry.");
                yield break;
            }

            _root.style.display = DisplayStyle.None;
            _root.style.visibility = Visibility.Hidden;
            _root.style.opacity = 0f;
            _root.pickingMode = PickingMode.Ignore;

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                _root.styleSheets.Add(styleSheet);

            if (!BindUI())
            {
                Debug.LogWarning("[MountainHudOverlayController] Required UI elements still not ready after retry.");
                yield break;
            }

            BindMap();

            SetOverlayOpen(startOpen, refreshNow: true);
            _ignoreToggleUntilTime = Time.unscaledTime + 0.15f;

            HookInput();
        }

        private void Start()
        {
            // Reassert the desired starting state after all OnEnable wiring settles.
            SetOverlayOpen(startOpen, refreshNow: false);
            _ignoreToggleUntilTime = Time.unscaledTime + 0.25f;
        }
        private void Update()
        {
            if (!_isOpen)
                return;

            float dt = Time.unscaledDeltaTime;
            _mapUI?.Tick(dt);
            SyncRaceCheckpointMarkersOnMap();

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            RefreshAll(force: false);
        }

        private void OnDisable()
        {
            UnhookInput();
            UnbindMap();
            ApplyCursorState(false);
            ApplyCameraLockState(false);

            RuntimeSceneLoadContext.MenuBackgroundPreviewEnded -= HandleMenuPreviewEnded;
            _pendingInitializeAfterPreview = false;
        }

        private void HookInput()
        {
            if (toggleOverlayAction == null || toggleOverlayAction.action == null)
                return;

            toggleOverlayAction.action.performed -= OnToggleOverlayPerformed;
            toggleOverlayAction.action.performed += OnToggleOverlayPerformed;

            if (!toggleOverlayAction.action.enabled)
                toggleOverlayAction.action.Enable();
        }

        private void UnhookInput()
        {
            if (toggleOverlayAction == null || toggleOverlayAction.action == null)
                return;

            toggleOverlayAction.action.performed -= OnToggleOverlayPerformed;
        }

        private void OnToggleOverlayPerformed(InputAction.CallbackContext ctx)
        {
            if (Time.unscaledTime < _ignoreToggleUntilTime)
                return;

            ToggleOverlay();
        }

        public string GetOverlayToggleBindingDisplay(InputActionAsset fallbackInputActions = null)
        {
            if (toggleOverlayAction != null && toggleOverlayAction.action != null)
                return InputPromptResolver.GetBindingDisplay(toggleOverlayAction.action);

            return fallbackInputActions != null
                ? InputPromptResolver.GetBindingDisplay(fallbackInputActions, "Toggle")
                : "-";
        }

        private void SyncRaceCheckpointMarkersOnMap()
        {
            if (_mapUI == null)
                return;

            var mgr = MountainActivityManager.Instance;
            if (mgr != null &&
                mgr.ActiveKind == MountainActivityKind.Race &&
                mgr.ActiveSource is RaceCourseLine race &&
                race.GeneratedCheckpoints != null &&
                race.GeneratedCheckpoints.Count > 0)
            {
                _raceCheckpointMarkerBuffer.Clear();

                for (int i = 0; i < race.GeneratedCheckpoints.Count; i++)
                    _raceCheckpointMarkerBuffer.Add(race.GeneratedCheckpoints[i].worldPos);

                _mapUI.SetActivityCheckpointMarkers(_raceCheckpointMarkerBuffer, race.CurrentCheckpointIndex, hideBaseMarkers: true);
            }
            else
            {
                _mapUI.ClearActivityCheckpointMarkers();
            }
        }

        private void ResolveReferences()
        {
            ResolveBootstrapReferences();

            if (skiController == null) skiController = FindObjectOfType<SkiController>();
            if (runProgressTracker == null) runProgressTracker = FindObjectOfType<RunProgressTracker>();
            if (skiPassManager == null) skiPassManager = SkiPassManager.Instance != null ? SkiPassManager.Instance : FindObjectOfType<SkiPassManager>();
            if (timeController == null) timeController = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();
            if (weatherController == null) weatherController = FindObjectOfType<WeatherController>();
            if (statsManager == null) statsManager = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance : FindObjectOfType<PlayerStatsManager>();
            if (progressionDirector == null) progressionDirector = ProgressionDirector.Instance != null ? ProgressionDirector.Instance : FindObjectOfType<ProgressionDirector>();
            if (questDirector == null) questDirector = QuestDirector.Instance != null ? QuestDirector.Instance : FindObjectOfType<QuestDirector>();
            if (questSignalBus == null) questSignalBus = FindObjectOfType<QuestSignalBus>();

            if (blackoutTransition == null) blackoutTransition = FindObjectOfType<PlayerBlackoutTransition>();
        }

        public bool IsOpen => _isOpen;

        public string TutorialLastViewedSection => _tutorialLastViewedSection;
        public bool TutorialVisitedStats => _tutorialVisitedStats;
        public bool TutorialVisitedTasks => _tutorialVisitedTasks;
        public bool QuestTabViewed => _tutorialVisitedQuests;
        public bool TutorialVisitedMap => _tutorialVisitedMap;

        public void ResetTutorialVisitedSections()
        {
            _tutorialVisitedStats = false;
            _tutorialVisitedTasks = false;
            _tutorialVisitedQuests = false;
            _tutorialVisitedMap = false;
            _tutorialLastViewedSection = "Stats";
        }

        private void CloseOpenKiosks()
        {
            var skiPassKiosks = FindObjectsOfType<SkiPassKioskUI>(true);
            for (int i = 0; i < skiPassKiosks.Length; i++)
            {
                if (skiPassKiosks[i] != null && skiPassKiosks[i].IsOpen)
                    skiPassKiosks[i].Close();
            }

            var raceKiosks = FindObjectsOfType<RaceSignupKioskUI>(true);
            for (int i = 0; i < raceKiosks.Length; i++)
            {
                if (raceKiosks[i] != null && raceKiosks[i].IsOpen)
                    raceKiosks[i].Close();
            }
        }
        public void ToggleOverlay()
        {
            SetOverlayOpen(!_isOpen);
        }

        public void SetOverlayOpen(bool open, bool refreshNow = true)
        {
            bool wasOpen = _isOpen;

            if (open)
                CloseOpenKiosks();

            _isOpen = open;

            if (_isOpen)
            {
                _tutorialLastViewedSection = "Stats";
                _tutorialVisitedStats = true;
            }

            if (_isOpen)
                MarkMapVisitedForTutorial();

            if (open && !wasOpen)
            {
                RaiseQuestUiEvent("ui.overlay.opened");
                RaiseQuestUiEvent("ui.overlay.stats_viewed");
                RaiseQuestUiEvent("ui.overlay.map_viewed");

                if (_goalsViewMode == GoalsViewMode.Quests)
                    RaiseQuestUiEvent("ui.overlay.quests_viewed");
                else if (_goalsViewMode == GoalsViewMode.Tasks)
                    RaiseQuestUiEvent("ui.overlay.tasks_viewed");
            }

            if (_root != null)
            {
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                _root.style.visibility = open ? Visibility.Visible : Visibility.Hidden;
                _root.style.opacity = open ? 1f : 0f;
                _root.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }

            ApplyCursorState(open);
            ApplyCameraLockState(open);
            GameplayModalMovementLock.SetLocked(this, open);

            if (miniHudController != null)
                miniHudController.SetVisible(!open);

            if (open)
            {
                if (refreshNow)
                    RefreshAll(force: false);

                ScheduleMapRecenterOnOpen();
            }
        }

        private void MarkMapVisitedForTutorial()
        {
            _tutorialLastViewedSection = "Map";
            _tutorialVisitedMap = true;
        }

        private void ApplyCursorState(bool overlayOpen)
        {
            if (overlayOpen)
                GameCursorService.Request(this, GameCursorMode.VisibleUnlocked, priority: 800);
            else
                GameCursorService.Release(this);
        }

        private void ApplyCameraLockState(bool overlayOpen)
        {
            if (cameraController == null)
                cameraController = FindObjectOfType<CameraController>();

            if (cameraController != null)
                cameraController.SetExternalUiLookLock(overlayOpen);
        }

        private void ScheduleMapRecenterOnOpen()
        {
            if (_root == null || _mapUI == null)
                return;

            _root.schedule.Execute(() =>
            {
                _mapUI.Refresh();

                // Reset to a clean fit first so the actual rendered map image is centered,
                // then center on the player from that valid baseline.
                _mapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 2.4f);
            }).ExecuteLater(0);

            _root.schedule.Execute(() =>
            {
                _mapUI.Refresh();
                _mapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 2.4f);
            }).ExecuteLater(40);
        }

        private bool BindUI()
        {
            if (_root == null)
                return false;

            _lblTopTime = _root.Q<Label>("Lbl_TopTime");
            _lblTopWeather = _root.Q<Label>("Lbl_TopWeather");
            _lblTopLocation = _root.Q<Label>("Lbl_TopLocation");
            _lblTopCurrency = _root.Q<Label>("Lbl_TopCurrency");
            _lblTopHint = _root.Q<Label>("Lbl_TopHint");
            _btnCloseOverlay = _root.Q<Button>("Btn_CloseOverlay");
            _mapSelectionPanel = _root.Q<VisualElement>("MapSelectionPanel");
            _overlayActionStack = _root.Q<VisualElement>("OverlayActionStack");
            _utilityPopupPanel = _root.Q<VisualElement>("UtilityPopupPanel");
            _lblUtilityPopupTitle = _root.Q<Label>("Lbl_UtilityPopupTitle");
            _achievementModalRoot = _root.Q<VisualElement>("AchievementModalRoot");
            _achievementBadge = _root.Q<Label>("Badge_Achievements");

            _lblTopWeatherIcon = _root.Q<Label>("Lbl_TopWeatherIcon");

            _lblContextIcon = _root.Q<Label>("Lbl_ContextIcon");
            _lblContextStatus = _root.Q<Label>("Lbl_ContextStatus");
            _lblContextGuidance = _root.Q<Label>("Lbl_ContextGuidance");
            _contextGuidanceBox = _root.Q<VisualElement>("ContextGuidanceBox");
            _lblRescueDispatchRank = _root.Q<Label>("Lbl_RescueDispatchRank");
            _lblRescueDispatchRefresh = _root.Q<Label>("Lbl_RescueDispatchRefresh");
            _lblRescueDispatchBeacon = _root.Q<Label>("Lbl_RescueDispatchBeacon");
            _lblRescueDispatchStatus = _root.Q<Label>("Lbl_RescueDispatchStatus");
            _btnRescueMedicalDrop = _root.Q<Button>("Btn_RescueMedicalDrop");
            _btnRescueBeacon = _root.Q<Button>("Btn_RescueBeacon");
            _btnRescueSnowmobile = _root.Q<Button>("Btn_RescueSnowmobile");
            _btnRescueSOS = _root.Q<Button>("Btn_RescueSOS");

            _lblContextTitle = _root.Q<Label>("Lbl_ContextTitle");
            _lblContextBody = _root.Q<Label>("Lbl_ContextBody");
            _contextMetaRow = _root.Q<VisualElement>("ContextMetaRow");
            _contextHighlights = _root.Q<VisualElement>("ContextHighlights");
            _contextDetailRow = _root.Q<VisualElement>("ContextDetailRow");

            _btnStatsToday = _root.Q<Button>("Btn_StatsToday");
            _btnStatsLifetime = _root.Q<Button>("Btn_StatsLifetime");
            _lblStatsModeSummary = _root.Q<Label>("Lbl_StatsModeSummary");
            _statsGrid = _root.Q<VisualElement>("StatsGrid");
            _lblStatsBody = _root.Q<Label>("Lbl_StatsBody");
            _btnOpenStats = _root.Q<Button>("Btn_OpenStats");
            _btnOpenRunHistory = _root.Q<Button>("Btn_OpenRunHistory");
            _btnOpenAchievements = _root.Q<Button>("Btn_OpenAchievements");
            _btnOpenPasses = _root.Q<Button>("Btn_OpenPasses");
            _btnOpenRescue = _root.Q<Button>("Btn_OpenRescue");
            _btnCloseUtilityPopup = _root.Q<Button>("Btn_CloseUtilityPopup");
            _btnCloseAchievements = _root.Q<Button>("Btn_CloseAchievements");
            _runHistoryList = _root.Q<ScrollView>("RunHistoryList");

            _btnGoalsQuests = _root.Q<Button>("Btn_GoalsQuests");
            _btnGoalsTasks = _root.Q<Button>("Btn_GoalsTasks");
            _goalsList = _root.Q<ScrollView>("GoalsList");
            _questPanel = _root.Q<VisualElement>("QuestPanel");
            _questList = _root.Q<ScrollView>("QuestList");
            _btnQuestCurrent = _root.Q<Button>("Btn_QuestCurrent");
            _btnQuestCompleted = _root.Q<Button>("Btn_QuestCompleted");
            _achievementCategoryRow = _root.Q<VisualElement>("AchievementCategoryRow");
            _achievementScroll = _root.Q<ScrollView>("AchievementScroll");
            _achievementGrid = _root.Q<VisualElement>("AchievementGrid");

            _achievementPanel = _root.Q<VisualElement>("AchievementPanel");
            _achievementHeaderShell = _root.Q<VisualElement>("AchievementHeaderShell");

            _btnPassTabInfo = _root.Q<Button>("Btn_PassTabInfo");
            _btnPassTabRescue = _root.Q<Button>("Btn_PassTabRescue");
            _btnPassBack = _root.Q<Button>("Btn_PassBack");

            _lblPassCurrent = _root.Q<Label>("Lbl_PassCurrent");
            _lblPassExpiryMeta = _root.Q<Label>("Lbl_PassExpiryMeta");
            _lblPassPermanentSummary = _root.Q<Label>("Lbl_PassPermanentSummary");
            _lblPassDetailTitle = _root.Q<Label>("Lbl_PassDetailTitle");
            _lblPassDetailMeta = _root.Q<Label>("Lbl_PassDetailMeta");

            _passOverviewList = _root.Q<ScrollView>("PassOverviewList");
            _passLiftList = _root.Q<ScrollView>("PassLiftList");

            _passInfoView = _root.Q<VisualElement>("PassInfoView");
            _passLiftDetailView = _root.Q<VisualElement>("PassLiftDetailView");
            _rescueUtilitiesView = _root.Q<VisualElement>("RescueUtilitiesView");
            _passExpiryBar = _root.Q<VisualElement>("PassExpiryBar");
            _passExpiryFill = _root.Q<VisualElement>("PassExpiryFill");
            _screenFade = _root.Q<VisualElement>("OverlayScreenFade");

            bool hasRequiredUi =
                _btnGoalsQuests != null &&
                _btnGoalsTasks != null &&
                _goalsList != null &&
                _questPanel != null &&
                _questList != null &&
                _achievementCategoryRow != null &&
                _achievementScroll != null &&
                _achievementGrid != null &&
                _btnStatsToday != null &&
                _btnStatsLifetime != null &&
                _statsGrid != null &&
                _btnOpenStats != null &&
                _btnOpenRunHistory != null &&
                _btnOpenAchievements != null &&
                _btnOpenPasses != null &&
                _btnOpenRescue != null &&
                _btnCloseUtilityPopup != null &&
                _btnCloseAchievements != null &&
                _runHistoryList != null;

            if (!hasRequiredUi)
            {
                Debug.LogWarning(
                    "[MountainHudOverlayController] Required UI elements are missing. " +
                    $"Btn_GoalsQuests={_btnGoalsQuests != null}, " +
                    $"Btn_GoalsTasks={_btnGoalsTasks != null}, " +
                    $"GoalsList={_goalsList != null}, " +
                    $"QuestPanel={_questPanel != null}, " +
                    $"QuestList={_questList != null}, " +
                    $"AchievementCategoryRow={_achievementCategoryRow != null}, " +
                    $"AchievementScroll={_achievementScroll != null}, " +
                    $"AchievementGrid={_achievementGrid != null}, " +
                    $"Btn_StatsToday={_btnStatsToday != null}, " +
                    $"Btn_StatsLifetime={_btnStatsLifetime != null}, " +
                    $"StatsGrid={_statsGrid != null}");
                return false;
            }

            if (_achievementScroll != null)
            {
                _achievementScroll.mode = ScrollViewMode.Vertical;
                _achievementScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _achievementScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }

            if (_btnCloseOverlay != null)
            {
                _btnCloseOverlay.clicked -= CloseOverlayFromButton;
                _btnCloseOverlay.clicked += CloseOverlayFromButton;
            }

            if (_btnStatsToday != null)
            {
                _btnStatsToday.clicked -= ShowTodayStats;
                _btnStatsToday.clicked += ShowTodayStats;
            }

            if (_btnStatsLifetime != null)
            {
                _btnStatsLifetime.clicked -= ShowLifetimeStats;
                _btnStatsLifetime.clicked += ShowLifetimeStats;
            }

            if (_btnGoalsQuests != null)
            {
                _btnGoalsQuests.clicked -= ShowQuestGoals;
                _btnGoalsQuests.clicked += ShowQuestGoals;
            }

            if (_btnGoalsTasks != null)
            {
                _btnGoalsTasks.clicked -= ShowTasksGoals;
                _btnGoalsTasks.clicked += ShowTasksGoals;
            }

            if (_btnQuestCurrent != null)
            {
                _btnQuestCurrent.clicked -= ShowCurrentQuests;
                _btnQuestCurrent.clicked += ShowCurrentQuests;
            }

            if (_btnQuestCompleted != null)
            {
                _btnQuestCompleted.clicked -= ShowCompletedQuests;
                _btnQuestCompleted.clicked += ShowCompletedQuests;
            }

            if (_btnPassTabInfo != null)
            {
                _btnPassTabInfo.clicked -= ShowPassInfoTab;
                _btnPassTabInfo.clicked += ShowPassInfoTab;
            }

            if (_btnPassTabRescue != null)
            {
                _btnPassTabRescue.clicked -= ShowPassRescueTab;
                _btnPassTabRescue.clicked += ShowPassRescueTab;
            }

            if (_btnPassBack != null)
            {
                _btnPassBack.clicked -= ShowPassOverview;
                _btnPassBack.clicked += ShowPassOverview;
            }

            if (_btnOpenStats != null)
            {
                _btnOpenStats.clicked -= OpenStatsUtility;
                _btnOpenStats.clicked += OpenStatsUtility;
            }

            if (_btnOpenRunHistory != null)
            {
                _btnOpenRunHistory.clicked -= OpenRunHistoryUtility;
                _btnOpenRunHistory.clicked += OpenRunHistoryUtility;
            }

            if (_btnOpenAchievements != null)
            {
                _btnOpenAchievements.clicked -= OpenAchievementsModal;
                _btnOpenAchievements.clicked += OpenAchievementsModal;
            }

            if (_btnOpenPasses != null)
            {
                _btnOpenPasses.clicked -= OpenPassesUtility;
                _btnOpenPasses.clicked += OpenPassesUtility;
            }

            if (_btnOpenRescue != null)
            {
                _btnOpenRescue.clicked -= OpenRescueUtility;
                _btnOpenRescue.clicked += OpenRescueUtility;
            }

            if (_btnCloseUtilityPopup != null)
            {
                _btnCloseUtilityPopup.clicked -= CloseUtilityPopup;
                _btnCloseUtilityPopup.clicked += CloseUtilityPopup;
            }

            if (_btnCloseAchievements != null)
            {
                _btnCloseAchievements.clicked -= CloseAchievementsModal;
                _btnCloseAchievements.clicked += CloseAchievementsModal;
            }

            if (_btnRescueMedicalDrop != null)
            {
                _btnRescueMedicalDrop.clicked -= RequestMedicalSupplyDrop;
                _btnRescueMedicalDrop.clicked += RequestMedicalSupplyDrop;
            }

            if (_btnRescueBeacon != null)
            {
                _btnRescueBeacon.clicked -= ToggleRescueBeacon;
                _btnRescueBeacon.clicked += ToggleRescueBeacon;
            }

            if (_btnRescueSnowmobile != null)
            {
                _btnRescueSnowmobile.clicked -= RequestSnowmobileDispatch;
                _btnRescueSnowmobile.clicked += RequestSnowmobileDispatch;
            }

            if (_btnRescueSOS != null)
            {
                _btnRescueSOS.clicked -= RequestEmergencySOS;
                _btnRescueSOS.clicked += RequestEmergencySOS;
            }

            SetScreenFadeOpacity(0f);
            ApplyPassPanelViewState();

            BuildAchievementCategoryButtons();
            ApplyOverlayPanelState();
            RefreshAchievementUnclaimedBadge();

            return true;
        }

        private void CloseOverlayFromButton()
        {
            SetOverlayOpen(false);
        }

        private void ShowTodayStats()
        {
            _statsShowLifetime = false;
            _tutorialVisitedStats = true;
            RaiseQuestUiEvent("ui.overlay.stats_viewed");
            RefreshStatsPanel();
        }

        private void ShowLifetimeStats()
        {
            _statsShowLifetime = true;
            _tutorialVisitedStats = true;
            RaiseQuestUiEvent("ui.overlay.stats_viewed");
            RefreshStatsPanel();
        }

        private void ShowQuestGoals()
        {
            _goalsViewMode = GoalsViewMode.Quests;
            _tutorialLastViewedSection = "Quests";
            _tutorialVisitedQuests = true;
            RaiseQuestUiEvent("ui.overlay.quests_viewed");
            RefreshGoalsPanel();
        }

        private void ShowTasksGoals()
        {
            _goalsViewMode = GoalsViewMode.Tasks;
            _tutorialLastViewedSection = "Tasks";
            _tutorialVisitedTasks = true;
            RaiseQuestUiEvent("ui.overlay.tasks_viewed");
            RefreshGoalsPanel();
        }

        private void OpenStatsUtility()
        {
            ToggleUtilityPanel(UtilityPanelMode.Stats);
        }

        private void OpenRunHistoryUtility()
        {
            ToggleUtilityPanel(UtilityPanelMode.RunHistory);
        }

        private void OpenPassesUtility()
        {
            ToggleUtilityPanel(UtilityPanelMode.Passes);
        }

        private void OpenRescueUtility()
        {
            ToggleUtilityPanel(UtilityPanelMode.Rescue);
        }

        private void ShowCurrentQuests()
        {
            _questShowCompleted = false;
            RefreshGoalsPanel();
        }

        private void ShowCompletedQuests()
        {
            _questShowCompleted = true;
            RefreshGoalsPanel();
        }

        private void ToggleUtilityPanel(UtilityPanelMode mode)
        {
            if (_utilityPanelMode == mode)
            {
                CloseUtilityPopup();
                return;
            }

            _utilityPanelMode = mode;

            if (mode == UtilityPanelMode.Passes)
            {
                _passShowRescueTab = false;
                _passShowLiftDetail = false;
            }
            else if (mode == UtilityPanelMode.Rescue)
            {
                _passShowRescueTab = true;
                _passShowLiftDetail = false;
            }

            if (mode == UtilityPanelMode.RunHistory)
                RaiseQuestUiEvent("ui.overlay.run_history_viewed");
            else if (mode == UtilityPanelMode.Stats)
                RaiseQuestUiEvent("ui.overlay.stats_viewed");

            RefreshPassPanel();
            RefreshRunHistoryPanel();
            ApplyOverlayPanelState();
        }

        private void CloseUtilityPopup()
        {
            _utilityPanelMode = UtilityPanelMode.None;
            ApplyOverlayPanelState();
        }

        private void OpenAchievementsModal()
        {
            _achievementsModalOpen = true;
            _selectedAchievementId = null;
            RefreshAchievementsView();
            RefreshAchievementUnclaimedBadge();
            ApplyOverlayPanelState();
        }

        private void CloseAchievementsModal()
        {
            _achievementsModalOpen = false;
            ApplyOverlayPanelState();
        }

        private void ApplyOverlayPanelState()
        {
            if (_mapSelectionPanel != null)
            {
                bool showSelection = _selectedKind != SelectedMapKind.None && !string.IsNullOrWhiteSpace(_selectedId);
                _mapSelectionPanel.style.display = showSelection ? DisplayStyle.Flex : DisplayStyle.None;
                _mapSelectionPanel.pickingMode = showSelection ? PickingMode.Position : PickingMode.Ignore;
            }

            if (_utilityPopupPanel != null)
            {
                bool showPopup = _utilityPanelMode != UtilityPanelMode.None;
                _utilityPopupPanel.style.display = showPopup ? DisplayStyle.Flex : DisplayStyle.None;
                _utilityPopupPanel.pickingMode = showPopup ? PickingMode.Position : PickingMode.Ignore;
            }

            if (_achievementModalRoot != null)
            {
                _achievementModalRoot.style.display = _achievementsModalOpen ? DisplayStyle.Flex : DisplayStyle.None;
                _achievementModalRoot.pickingMode = _achievementsModalOpen ? PickingMode.Position : PickingMode.Ignore;
            }

            if (_lblUtilityPopupTitle != null)
            {
                _lblUtilityPopupTitle.text = _utilityPanelMode switch
                {
                    UtilityPanelMode.Stats => "Stats",
                    UtilityPanelMode.RunHistory => "Run History",
                    UtilityPanelMode.Passes => "Ski Passes",
                    UtilityPanelMode.Rescue => "Rescue",
                    _ => "Utility"
                };
            }

            if (_statsGrid != null && _statsGrid.parent != null)
            {
                var statsPanel = _statsGrid.parent;
                if (statsPanel != null)
                    statsPanel.style.display = _utilityPanelMode == UtilityPanelMode.Stats ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_runHistoryList != null && _runHistoryList.parent != null)
            {
                var runHistoryPanel = _runHistoryList.parent;
                runHistoryPanel.style.display = _utilityPanelMode == UtilityPanelMode.RunHistory ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_passInfoView != null && _passInfoView.parent != null)
            {
                var passPanel = _passInfoView.parent;
                bool showPassPanel = _utilityPanelMode == UtilityPanelMode.Passes || _utilityPanelMode == UtilityPanelMode.Rescue;
                passPanel.style.display = showPassPanel ? DisplayStyle.Flex : DisplayStyle.None;
            }

            SetToggleState(_btnOpenStats, _utilityPanelMode == UtilityPanelMode.Stats);
            SetToggleState(_btnOpenRunHistory, _utilityPanelMode == UtilityPanelMode.RunHistory);
            SetToggleState(_btnOpenAchievements, _achievementsModalOpen);
            SetToggleState(_btnOpenPasses, _utilityPanelMode == UtilityPanelMode.Passes);
            SetToggleState(_btnOpenRescue, _utilityPanelMode == UtilityPanelMode.Rescue);
        }

        private void RefreshAchievementUnclaimedBadge()
        {
            if (_achievementBadge == null)
                return;

            if (progressionDirector == null)
            {
                _achievementBadge.style.display = DisplayStyle.None;
                return;
            }

            _achievementQueryBuffer.Clear();
            progressionDirector.GetAllAchievements(_achievementQueryBuffer);

            var profile = Profile;
            int unclaimed = 0;
            _achievementDedup.Clear();

            for (int i = 0; i < _achievementQueryBuffer.Count; i++)
            {
                var def = _achievementQueryBuffer[i];
                if (def == null || string.IsNullOrWhiteSpace(def.id) || !_achievementDedup.Add(def.id))
                    continue;

                if (profile != null && profile.HasAchievement(def.id) && !profile.HasClaimedAchievement(def.id))
                    unclaimed++;
            }

            _achievementBadge.text = unclaimed > 9 ? "9+" : unclaimed.ToString();
            _achievementBadge.style.display = unclaimed > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BindMap()
        {
            if (mapData == null)
            {
                Debug.LogWarning("[MountainHudOverlayController] Missing MapData.");
                return;
            }

            UnbindMap();

            var overlayRoot = _root.Q<VisualElement>("OverlayRoot") ?? _root;
            var host = overlayRoot.Q<VisualElement>("MapHost");

            if (host == null)
            {
                Debug.LogWarning("[MountainHudOverlayController] MapHost placeholder not found.");
                return;
            }

            host.Clear();

            host.style.position = Position.Absolute;
            host.pickingMode = PickingMode.Position;

            VisualElement runtimeMapRoot = MountainHudMapRootFactory.BuildOverlayMapRoot("OverlayRuntimeMapRoot");
            host.Add(runtimeMapRoot);

            // Critical: the host must be behind all overlay panels.
            host.SendToBack();

            var topStrip = overlayRoot.Q<VisualElement>("TopStrip");
            var goalsPanel = overlayRoot.Q<VisualElement>("GoalsPanel");
            var actionStack = overlayRoot.Q<VisualElement>("OverlayActionStack");
            var utilityPopup = overlayRoot.Q<VisualElement>("UtilityPopupPanel");
            var selectionPanel = overlayRoot.Q<VisualElement>("MapSelectionPanel");
            var achievementsModal = overlayRoot.Q<VisualElement>("AchievementModalRoot");
            var screenFade = overlayRoot.Q<VisualElement>("OverlayScreenFade");

            topStrip?.BringToFront();
            goalsPanel?.BringToFront();
            actionStack?.BringToFront();
            utilityPopup?.BringToFront();
            selectionPanel?.BringToFront();
            achievementsModal?.BringToFront();
            screenFade?.BringToFront();

            _mapUI = new PhoneMapPageUI();
            _mapUI.Bind(runtimeMapRoot, mapData, mapReferenceCamera);
            _mapUI.SetWaypointManager(_waypointManager);

            var resolvedRegionSet = ResolveRegionSet();
            if (resolvedRegionSet != null)
                _mapUI.SetRegionSet(resolvedRegionSet);

            if (skiController != null)
                _mapUI.SetPlayer(skiController.transform);

            _mapUI.SetPlayerTracking(showMarker: true, drawTrail: true);
            _mapUI.MarkerSelected += OnMapMarkerSelected;
            _mapUI.MarkerDoubleClicked += OnMapMarkerDoubleClicked;
            _mapUI.MarkerRightDoubleClicked += OnMapMarkerRightDoubleClicked;
            _mapUI.PolylineSelected += OnMapPolylineSelected;
            _mapUI.BackgroundWorldClicked += OnMapBackgroundWorldClicked;
            _mapUI.BackgroundWorldDoubleClicked += OnMapBackgroundWorldDoubleClicked;
            _mapUI.SelectionCleared += OnMapSelectionCleared;
            _mapUI.SetMinimapMode(false, followPlayer: false, lockPan: false, allowZoom: true, suppressSelection: false, hideMarkerLabels: false);

            _mapUI.WaypointClicked += OnMapWaypointClicked;
            _mapUI.WaypointDoubleClicked += OnMapWaypointDoubleClicked;
            _mapUI.WaypointDeleteRequested += OnMapWaypointDeleteRequested;
            _mapUI.WaypointLabelEditRequested += OnMapWaypointLabelEditRequested;

            var viewport = runtimeMapRoot.Q<VisualElement>("MapViewport");
            if (viewport != null)
            {
                viewport.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    _mapUI?.Refresh();
                });
            }

            RefreshMapDependencies(forceRefresh: true);
            _mapUI.Refresh();
        }

        private void UnbindMap()
        {
            if (_mapUI == null)
                return;

            _mapUI.MarkerSelected -= OnMapMarkerSelected;
            _mapUI.MarkerDoubleClicked -= OnMapMarkerDoubleClicked;
            _mapUI.MarkerRightDoubleClicked -= OnMapMarkerRightDoubleClicked;
            _mapUI.PolylineSelected -= OnMapPolylineSelected;
            _mapUI.BackgroundWorldClicked -= OnMapBackgroundWorldClicked;
            _mapUI.SelectionCleared -= OnMapSelectionCleared;
            _mapUI.BackgroundWorldDoubleClicked -= OnMapBackgroundWorldDoubleClicked;

            _mapUI.WaypointClicked -= OnMapWaypointClicked;
            _mapUI.WaypointDoubleClicked -= OnMapWaypointDoubleClicked;
            _mapUI.WaypointDeleteRequested -= OnMapWaypointDeleteRequested;
            _mapUI.WaypointLabelEditRequested -= OnMapWaypointLabelEditRequested;
        }

        private void RefreshMapDependencies(bool forceRefresh)
        {
            if (_mapUI == null)
                return;

            // Auto-find the style asset if it was not assigned on the new HUD.
            if (mapStyle == null)
                mapStyle = AutoFindMapStyle();

            if (mapStyle != null && (_appliedMapStyle != mapStyle || forceRefresh))
            {
                _appliedMapStyle = mapStyle;
                _mapUI.ApplyStyle(mapStyle);
            }

            var reg = PointOfInterestRegistry.Instance;

            // The new HUD initializes very early, so registry may not exist yet during BindMap().
            // Keep retrying until it does.
            if (reg != null)
            {
                if (_appliedPoiRegistry != reg || forceRefresh)
                {
                    _appliedPoiRegistry = reg;

                    // Make sure the registry has an up-to-date cache before the map consumes it.
                    reg.Refresh();

                    _mapUI.SetPOIRegistry(reg);
                    _mapUI.Refresh();
                }
            }
        }

        private void RefreshAll(bool force)
        {
            ResolveReferences();
            RefreshMapDependencies(forceRefresh: false);
            RefreshTopStrip();
            RefreshContextPanel();
            RefreshStatsPanel();
            RefreshGoalsPanel();
            RefreshPassPanel();
            RefreshRunHistoryPanel();
            RefreshAchievementUnclaimedBadge();
            ApplyOverlayPanelState();

            if (force)
                ScheduleMapRecenterOnOpen();
        }

        private PlayerStatsProfile Profile => statsManager != null ? statsManager.Profile : null;

        private void RefreshTopStrip()
        {
            if (_lblTopTime != null)
                _lblTopTime.text = FormatTime();

            if (_lblTopWeather != null)
                _lblTopWeather.text = FormatWeather();

            if (_lblTopWeatherIcon != null)
                _lblTopWeatherIcon.text = GetWeatherGlyph();

            if (_lblTopLocation != null)
                _lblTopLocation.text = BuildLocationLine();

            if (_lblTopCurrency != null)
                _lblTopCurrency.text = $"${Mathf.Max(0, Profile != null ? Profile.currency : 0):N0}";

            if (_lblTopHint != null)
                _lblTopHint.text = BuildHintLine();
        }

        private void RefreshContextPanel()
        {
            if (_lblContextTitle == null || _lblContextBody == null)
                return;

            _contextMetaRow?.Clear();
            _contextHighlights?.Clear();
            ApplySelectionTraySectionVisibility();

            if (_selectedKind == SelectedMapKind.None || string.IsNullOrWhiteSpace(_selectedId))
            {
                ApplyOverlayPanelState();
                return;
            }

            _lblContextTitle.text = _selectedTitle;
            _lblContextBody.text = _selectedBody;

            switch (_selectedKind)
            {
                case SelectedMapKind.Run:
                    SetContextPresentation(
                        icon: "Run",
                        status: "Run Selected",
                        guidance: "Double click its marker to place or cycle a linked waypoint. Use Run History for the full travel log.");
                    break;

                case SelectedMapKind.Lift:
                    SetContextPresentation(
                        icon: "Lift",
                        status: "Lift Selected",
                        guidance: "Review pass access here, then open Ski Passes if you want the full access list.");
                    break;

                case SelectedMapKind.Waypoint:
                    SetContextPresentation(
                        icon: "WP",
                        status: "Waypoint Selected",
                        guidance: "Double click the waypoint to cycle its color, or use the map actions to rename or remove it.");
                    break;

                case SelectedMapKind.POI:
                    SetContextPresentation(
                        icon: "POI",
                        status: "Point of Interest",
                        guidance: "Use landmarks to orient yourself, then place a custom waypoint if you want a beacon.");
                    break;

                default:
                    SetContextPresentation(
                        icon: "Info",
                        status: "Selection",
                        guidance: "Inspect the map, then act from here.");
                    break;
            }

            PopulateSelectedContextMeta();
            PopulateSelectedContextHighlights();
            AppendSelectedRunVisitSummary();
            ApplySelectionTraySectionVisibility();
            ApplyOverlayPanelState();
            return;

#if false
            if (!string.IsNullOrWhiteSpace(_selectedId))
            {
                _lblContextTitle.text = _selectedTitle;
                _lblContextBody.text = _selectedBody;

                switch (_selectedKind)
                {
                    case SelectedMapKind.Run:
                        SetContextPresentation(
                            icon: "🎿",
                            status: "Run Selected",
                            guidance: "Single click inspects this route. Double click its marker to place or cycle a linked waypoint.");
                        break;

                    case SelectedMapKind.Lift:
                        SetContextPresentation(
                            icon: "🚡",
                            status: "Lift Selected",
                            guidance: "Review access before you commit. Open the ski pass panel to inspect unlocked passes and focus eligible lifts on the map.");
                        break;

                    case SelectedMapKind.POI:
                        SetContextPresentation(
                            icon: "📍",
                            status: "Point of Interest",
                            guidance: "Use landmarks to orient yourself, then place a custom waypoint if you want a navigation beacon.");
                        break;

                    default:
                        SetContextPresentation(
                            icon: "🔎",
                            status: "Selection",
                            guidance: "Inspect the map, then act from here.");
                        break;
                }

                PopulateSelectedContextMeta();
                PopulateSelectedContextHighlights();
                RefreshContextAttemptsForSelection();
                return;
            }

            if (runProgressTracker != null && runProgressTracker.TryGetActiveProgress(out var active))
            {
                _lblContextTitle.text = active.runName;
                _lblContextBody.text = "Current run in progress. Stay composed through the line and protect the clean finish.";

                SetContextPresentation(
                    icon: "⛷",
                    status: "Live Run",
                    guidance: "Keep momentum, avoid stacks, and finish clean. Open history to compare this run against previous attempts.");

                AddContextMetaChip("State", "On Run", accent: true);
                AddContextMetaChip("Progress", $"{Mathf.RoundToInt(active.completion01 * 100f)}%");
                AddContextMetaChip("Time", FormatSeconds(active.elapsedSeconds));
                AddContextMetaChip("Top Speed", FormatSpeed(active.topSpeedMps));
                AddContextMetaChip("Stacks", active.stacks.ToString());

                if (_contextHighlights != null)
                {
                    _contextHighlights.Add(MakeContextHighlightCard(
                        "Run Snapshot",
                        $"{Mathf.RoundToInt(active.completion01 * 100f)}% complete",
                        $"{FormatSeconds(active.elapsedSeconds)} elapsed • {FormatSpeed(active.topSpeedMps)} top speed"));

                    var cleanCard = MakeContextHighlightCard(
                        "Clean Finish",
                        active.stacks <= 0 ? "Still clean" : $"{active.stacks} stack{(active.stacks == 1 ? "" : "s")}",
                        active.stacks <= 0
                            ? "You are still on track for a clean completion."
                            : "Avoid more falls to salvage the attempt.");

                    cleanCard.AddToClassList(active.stacks <= 0 ? "is-positive" : "is-warning");
                    _contextHighlights.Add(cleanCard);
                }

                RefreshContextAttemptsForActiveRun(active.runId);
                return;
            }

            BuildNearbyRunsContext();
#endif
        }

        private void AppendSelectedRunVisitSummary()
        {
            if (_selectedKind != SelectedMapKind.Run ||
                _contextHighlights == null ||
                Profile == null ||
                string.IsNullOrWhiteSpace(_selectedId))
            {
                return;
            }

            if (!Profile.TryGetRunRecord(_selectedId, out var record) ||
                record == null ||
                record.attempts == null ||
                record.attempts.Count == 0)
            {
                return;
            }

            RunAttemptEntry latest = null;

            for (int i = record.attempts.Count - 1; i >= 0; i--)
            {
                if (record.attempts[i] != null)
                {
                    latest = record.attempts[i];
                    break;
                }
            }

            if (latest == null)
                return;

            string range = BuildAttemptRangeText(latest);
            string covered = latest.isCompletion
                ? "Completed"
                : $"{Mathf.RoundToInt(GetAttemptCoveredFraction01(latest) * 100f)}% covered";

            _contextHighlights.Add(MakeContextHighlightCard(
                "Latest Visit",
                FormatClockTime(latest.gameTimeOfDay),
                $"{range} • {covered}"));
        }

        private string BuildAttemptRangeText(RunAttemptEntry attempt)
        {
            if (attempt == null)
                return "Route logged";

            int entry = Mathf.RoundToInt(Mathf.Clamp01(attempt.entryFraction01) * 100f);
            int exit = Mathf.RoundToInt(Mathf.Clamp01(attempt.exitFraction01) * 100f);

            if (entry == exit)
                return $"Around {entry}%";

            return $"{entry}% → {exit}%";
        }

        private void SetContextPresentation(string icon, string status, string guidance)
        {
            if (_lblContextIcon != null)
                _lblContextIcon.text = string.IsNullOrWhiteSpace(icon) ? "🗺" : icon;

            if (_lblContextStatus != null)
                _lblContextStatus.text = string.IsNullOrWhiteSpace(status) ? "Overview" : status;

            if (_lblContextGuidance != null)
                _lblContextGuidance.text = string.IsNullOrWhiteSpace(guidance)
                    ? "Use the map to inspect routes, plan a waypoint, or find your next descent."
                    : guidance;

            if (_contextGuidanceBox != null)
                _contextGuidanceBox.style.display = DisplayStyle.Flex;
        }

        private void ApplySelectionTraySectionVisibility()
        {
            bool hasMeta = _contextMetaRow != null && _contextMetaRow.childCount > 0;
            bool hasHighlights = _contextHighlights != null && _contextHighlights.childCount > 0;
            bool hasDetails = hasMeta || hasHighlights;

            if (_contextMetaRow != null)
            {
                _contextMetaRow.style.display = hasMeta ? DisplayStyle.Flex : DisplayStyle.None;
                _contextMetaRow.pickingMode = hasMeta ? PickingMode.Position : PickingMode.Ignore;
            }

            if (_contextHighlights != null)
            {
                _contextHighlights.style.display = hasHighlights ? DisplayStyle.Flex : DisplayStyle.None;
                _contextHighlights.pickingMode = hasHighlights ? PickingMode.Position : PickingMode.Ignore;
            }

            if (_contextDetailRow != null)
            {
                _contextDetailRow.style.display = hasDetails ? DisplayStyle.Flex : DisplayStyle.None;
                _contextDetailRow.pickingMode = hasDetails ? PickingMode.Position : PickingMode.Ignore;
            }

            if (_mapSelectionPanel != null)
            {
                _mapSelectionPanel.EnableInClassList("has-context-meta", hasMeta);
                _mapSelectionPanel.EnableInClassList("has-context-highlights", hasHighlights);
                _mapSelectionPanel.EnableInClassList("has-only-context-identity", !hasDetails);

                // USS-safe replacements for unsupported :not(...) selectors.
                _mapSelectionPanel.EnableInClassList("has-only-context-meta", hasMeta && !hasHighlights);
                _mapSelectionPanel.EnableInClassList("has-only-context-highlights", hasHighlights && !hasMeta);
            }
        }

        private void PopulateSelectedContextHighlights()
        {
            if (_contextHighlights == null)
                return;

            var profile = Profile;
            if (profile == null || string.IsNullOrWhiteSpace(_selectedId))
                return;

            switch (_selectedKind)
            {
                case SelectedMapKind.Run:
                    {
                        // Run history is appended separately by AppendSelectedRunVisitSummary().
                        // Avoid duplicating attempts, completions, visited state, or best speed here.
                        break;
                    }

                case SelectedMapKind.Lift:
                    {
                        var lift = FindLiftById(_selectedId);
                        bool canUse = skiPassManager == null || (lift != null && skiPassManager.CanUseLift(lift));
                        string requirementText = lift != null ? lift.GetRequiredPassDisplayName() : "Unknown pass";

                        var accessCard = MakeContextHighlightCard(
                            "Access",
                            canUse ? "Available" : $"Needs {requirementText}",
                            canUse
                                ? "Current pass allows this lift."
                                : "Open Ski Passes for access.");

                        accessCard.AddToClassList(canUse ? "is-positive" : "is-warning");
                        _contextHighlights.Add(accessCard);
                        break;
                    }

                case SelectedMapKind.POI:
                    {
                        // Intentionally empty.
                        // No generic Discovery/Visited card in the compact horizontal panel.
                        break;
                    }

                case SelectedMapKind.Waypoint:
                    {
                        if (_waypointManager != null && _waypointManager.TryGetWaypoint(_selectedId, out var waypoint))
                        {
                            _contextHighlights.Add(MakeContextHighlightCard(
                                "Coordinates",
                                $"{waypoint.worldPosition.x:0}, {waypoint.worldPosition.z:0}",
                                "Map reference point."));
                        }

                        break;
                    }
            }
        }

        private string GetWeatherGlyph()
        {
            if (weatherController == null || weatherController.currentWeatherPreset == null)
                return "◌";

            string glyph = weatherController.currentWeatherPreset.weatherGlyph;

            if (!string.IsNullOrWhiteSpace(glyph))
                return glyph;

            return "◌";
        }

        private void PopulateSelectedContextMeta()
        {
            var profile = Profile;
            if (profile == null)
                return;

            switch (_selectedKind)
            {
                case SelectedMapKind.Run:
                    {
                        if (!string.IsNullOrWhiteSpace(_selectedId) &&
                            profile.TryGetRunRecord(_selectedId, out var record) &&
                            record != null)
                        {
                            int attempts = record.attempts != null ? record.attempts.Count : 0;

                            if (attempts > 0)
                                AddContextMetaChip("Attempts", attempts.ToString());

                            if (record.timesCompleted > 0)
                                AddContextMetaChip("Completed", record.timesCompleted.ToString());

                            if (record.bestTopSpeedMps > 0f)
                                AddContextMetaChip("Best Speed", FormatSpeed(record.bestTopSpeedMps));
                        }

                        break;
                    }

                case SelectedMapKind.Lift:
                    {
                        var lift = FindLiftById(_selectedId);
                        if (lift != null)
                            AddContextMetaChip("Pass", lift.GetRequiredPassDisplayName());

                        int rides = !string.IsNullOrWhiteSpace(_selectedId)
                            ? profile.GetLiftRideCount(_selectedId, session: false)
                            : 0;

                        if (rides > 0)
                            AddContextMetaChip("Rides", rides.ToString());

                        break;
                    }

                case SelectedMapKind.POI:
                    {
                        break;
                    }

                case SelectedMapKind.Waypoint:
                    {
                        if (_waypointManager != null && _waypointManager.TryGetWaypoint(_selectedId, out var waypoint))
                        {
                            AddContextMetaChip("Mode", waypoint.kind.ToString());
                            AddContextMetaChip("Color", $"#{ColorUtility.ToHtmlStringRGB(waypoint.color)}");
                        }

                        break;
                    }
            }
        }

        private void RefreshRescueDispatchPanel()
        {
            if (_rescueUtilitiesView == null)
                return;

            RescueDispatchController.EnsureInstance();

            int rescueRank = RaceRescueProgression.GetRescueCareerRank();
            int completedMissions = RaceRescueProgression.GetRescueMissionCompletionCount();
            int medicalUses = RaceRescueProgression.GetRescueUtilityUsesRemaining(RaceRescueProgression.RescueDispatchUtility.MedicalSupplyDrop);
            int beaconUses = RaceRescueProgression.GetRescueUtilityUsesRemaining(RaceRescueProgression.RescueDispatchUtility.RespawnBeacon);
            int snowmobileUses = RaceRescueProgression.GetRescueUtilityUsesRemaining(RaceRescueProgression.RescueDispatchUtility.SnowmobileDispatch);

            if (_lblRescueDispatchRank != null)
                _lblRescueDispatchRank.text = $"Responder rank {rescueRank}  •  Missions completed {completedMissions}";

            if (_lblRescueDispatchRefresh != null)
            {
                DateTime refreshUtc = RaceRescueProgression.GetRescueDispatchRefreshUtc().ToDateTimeUtc();
                _lblRescueDispatchRefresh.text = $"Refresh: {refreshUtc:dd MMM HH:mm} UTC";
            }

            bool hasBeacon = RaceRescueProgression.TryGetActiveRescueBeacon(out _, out _);

            if (_lblRescueDispatchBeacon != null)
                _lblRescueDispatchBeacon.text = hasBeacon
                    ? "Beacon active"
                    : "No active beacon";

            ConfigureDispatchButton(
                _btnRescueMedicalDrop,
                RaceRescueProgression.IsRescueUtilityUnlocked(RaceRescueProgression.RescueDispatchUtility.MedicalSupplyDrop),
                medicalUses,
                $"Medical Drop ({medicalUses})",
                "Medical Drop (Rank 2)");

            if (_btnRescueBeacon != null)
            {
                bool unlocked = RaceRescueProgression.IsRescueUtilityUnlocked(RaceRescueProgression.RescueDispatchUtility.RespawnBeacon);
                _btnRescueBeacon.text = unlocked
                    ? (hasBeacon ? "Recall to Beacon" : $"Place Beacon ({beaconUses})")
                    : "Beacon (Rank 3)";
                _btnRescueBeacon.SetEnabled(unlocked && (hasBeacon || beaconUses > 0));
            }

            ConfigureDispatchButton(
                _btnRescueSnowmobile,
                RaceRescueProgression.IsRescueUtilityUnlocked(RaceRescueProgression.RescueDispatchUtility.SnowmobileDispatch),
                snowmobileUses,
                $"Dispatch Snowmobile ({snowmobileUses})",
                "Dispatch Snowmobile (Rank 4)");

            if (_btnRescueSOS != null)
            {
                _btnRescueSOS.text = _sosRoutine == null ? "SOS Return to Resort" : "SOS In Progress...";
                _btnRescueSOS.SetEnabled(_sosRoutine == null);
            }

            if (_lblRescueDispatchStatus != null)
                _lblRescueDispatchStatus.text = _rescueDispatchFeedback;
        }

        private static void ConfigureDispatchButton(Button button, bool unlocked, int usesRemaining, string readyText, string lockedText)
        {
            if (button == null)
                return;

            button.text = unlocked ? readyText : lockedText;
            button.SetEnabled(unlocked && usesRemaining > 0);
        }

        private void RequestMedicalSupplyDrop()
        {
            RescueDispatchController dispatch = RescueDispatchController.EnsureInstance();
            string message = string.Empty;
            bool success = dispatch != null && dispatch.TryRequestMedicalSupplyDrop(out message);
            _rescueDispatchFeedback = success ? message : (string.IsNullOrWhiteSpace(message) ? "Medical drop failed." : message);
            RefreshRescueDispatchPanel();
        }

        private void ToggleRescueBeacon()
        {
            RescueDispatchController dispatch = RescueDispatchController.EnsureInstance();
            if (dispatch == null)
            {
                _rescueDispatchFeedback = "Rescue dispatch controller unavailable.";
                RefreshRescueDispatchPanel();
                return;
            }

            bool hasBeacon = RaceRescueProgression.TryGetActiveRescueBeacon(out _, out _);
            bool success;
            string message;

            if (hasBeacon)
                success = dispatch.TryRecallToRespawnBeacon(out message);
            else
                success = dispatch.TryPlaceRespawnBeacon(out message);

            _rescueDispatchFeedback = success ? message : (string.IsNullOrWhiteSpace(message) ? "Beacon request failed." : message);
            RefreshRescueDispatchPanel();
        }

        private void RequestSnowmobileDispatch()
        {
            RescueDispatchController dispatch = RescueDispatchController.EnsureInstance();
            string message = string.Empty;
            bool success = dispatch != null && dispatch.TryRequestSnowmobileDispatch(out message);
            _rescueDispatchFeedback = success ? message : (string.IsNullOrWhiteSpace(message) ? "Snowmobile dispatch failed." : message);
            RefreshRescueDispatchPanel();
        }

        private void RefreshRunHistoryPanel()
        {
            if (_runHistoryList == null)
                return;

            _runHistoryList.contentContainer.Clear();

            var profile = Profile;
            if (profile == null || profile.runRecords == null || profile.runRecords.Count == 0)
            {
                _runHistoryList.Add(new Label("No travel log entries yet."));
                return;
            }

            var entries = new List<(string runId, string runName, RunAttemptEntry attempt)>();
            foreach (var record in profile.runRecords)
            {
                if (record?.attempts == null)
                    continue;

                string runId = record.runId;
                string runName = ResolveRunDisplayName(runId);
                for (int i = 0; i < record.attempts.Count; i++)
                {
                    var attempt = record.attempts[i];
                    if (attempt == null)
                        continue;

                    entries.Add((runId, runName, attempt));
                }
            }

            if (entries.Count == 0)
            {
                _runHistoryList.Add(new Label("No travel log entries yet."));
                return;
            }

            entries.Sort((a, b) => CompareRunHistoryEntries(a.attempt, b.attempt));

            string lastDayKey = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string dayKey = BuildRunHistoryDayKey(entry.attempt);
                if (!string.Equals(dayKey, lastDayKey, StringComparison.Ordinal))
                {
                    var header = new Label(dayKey);
                    header.AddToClassList("run-history-day");
                    _runHistoryList.Add(header);
                    lastDayKey = dayKey;
                }

                _runHistoryList.Add(MakeRunHistoryRow(entry.runId, entry.runName, entry.attempt));
            }
        }

        private VisualElement MakeRunHistoryRow(string runId, string runName, RunAttemptEntry attempt)
        {
            var row = new Button(() => FocusRunHistoryEntry(runId))
            {
                focusable = true
            };
            row.AddToClassList("run-history-row");

            var title = new Label(runName);
            title.AddToClassList("run-history-title");
            row.Add(title);

            var segment = new Label($"Entered {Mathf.RoundToInt(Mathf.Clamp01(attempt.entryFraction01) * 100f)}% → exited {Mathf.RoundToInt(Mathf.Clamp01(attempt.exitFraction01) * 100f)}%");
            segment.AddToClassList("run-history-meta");
            row.Add(segment);

            float covered01 = GetAttemptCoveredFraction01(attempt);
            var meta = new Label($"{FormatClockTime(attempt.gameTimeOfDay)} • {FormatDurationLong(attempt.timeSeconds)} • {Mathf.RoundToInt(covered01 * 100f)}% covered");
            meta.AddToClassList("run-history-meta");
            row.Add(meta);

            string secondaryText = BuildRunHistorySecondaryLine(attempt);
            if (!string.IsNullOrWhiteSpace(secondaryText))
            {
                var secondary = new Label(secondaryText);
                secondary.AddToClassList("run-history-meta");
                row.Add(secondary);
            }

            return row;
        }

        private void FocusRunHistoryEntry(string runId)
        {
            if (_mapUI == null || string.IsNullOrWhiteSpace(runId))
                return;

            _mapUI.SelectPolylineById(runId, center: false, minZoom: -1f);
            _mapUI.FramePolylineById(runId, paddingPx: 28f, minZoom: 0.85f);
        }

        private void RefreshStatsPanel()
        {
            SetToggleState(_btnStatsToday, !_statsShowLifetime);
            SetToggleState(_btnStatsLifetime, _statsShowLifetime);

            if (_lblStatsModeSummary != null)
            {
                _lblStatsModeSummary.text = _statsShowLifetime
                    ? "Lifetime totals across all recorded sessions."
                    : "Session totals for the current outing.";
            }

            if (_statsGrid != null)
                _statsGrid.Clear();

            var p = Profile;
            if (p == null)
            {
                if (_statsGrid != null)
                    _statsGrid.Add(MakeStatTile("Profile", "Unavailable", accent: true));
                else if (_lblStatsBody != null)
                    _lblStatsBody.text = "No profile loaded.";
                return;
            }

            if (_statsShowLifetime)
            {
                var l = p.lifetime;

                if (_statsGrid != null)
                {
                    _statsGrid.Add(MakeStatTile("Lifetime Distance", FormatMeters(l.totalDistanceMeters), accent: true));
                    _statsGrid.Add(MakeStatTile("Lifetime Vertical", FormatMeters(l.totalVerticalDescentMeters)));
                    _statsGrid.Add(MakeStatTile("Runs Completed", l.totalRunsCompleted.ToString()));
                    _statsGrid.Add(MakeStatTile("Lift Rides", l.totalLiftsUsed.ToString()));
                    _statsGrid.Add(MakeStatTile("Best Speed", FormatSpeed(l.topSpeedMps)));
                    _statsGrid.Add(MakeStatTile("Best Run Speed", FormatSpeed(l.topRunSpeedMps)));
                }
                else if (_lblStatsBody != null)
                {
                    _lblStatsBody.text =
                        $"Lifetime\n" +
                        $"Distance: {FormatMeters(l.totalDistanceMeters)}\n" +
                        $"Vertical: {FormatMeters(l.totalVerticalDescentMeters)}\n" +
                        $"Runs: {l.totalRunsCompleted}\n" +
                        $"Lifts: {l.totalLiftsUsed}\n" +
                        $"Top Speed: {FormatSpeed(l.topSpeedMps)}\n" +
                        $"Top Run Speed: {FormatSpeed(l.topRunSpeedMps)}";
                }
            }
            else
            {
                var s = p.session;

                if (_statsGrid != null)
                {
                    _statsGrid.Add(MakeStatTile("Today's Distance", FormatMeters(s.distanceMeters), accent: true));
                    _statsGrid.Add(MakeStatTile("Today's Vertical", FormatMeters(s.verticalDescentMeters)));
                    _statsGrid.Add(MakeStatTile("Runs Today", s.runsCompleted.ToString()));
                    _statsGrid.Add(MakeStatTile("Lift Rides Today", s.liftsUsed.ToString()));
                    _statsGrid.Add(MakeStatTile("Top Speed Today", FormatSpeed(s.topSpeedMps)));
                    _statsGrid.Add(MakeStatTile("Top Run Today", FormatSpeed(s.topRunSpeedMps)));
                }
                else if (_lblStatsBody != null)
                {
                    _lblStatsBody.text =
                        $"Today\n" +
                        $"Distance: {FormatMeters(s.distanceMeters)}\n" +
                        $"Vertical: {FormatMeters(s.verticalDescentMeters)}\n" +
                        $"Runs: {s.runsCompleted}\n" +
                        $"Lifts: {s.liftsUsed}\n" +
                        $"Top Speed: {FormatSpeed(s.topSpeedMps)}\n" +
                        $"Top Run Speed: {FormatSpeed(s.topRunSpeedMps)}";
                }
            }
        }

        private void RefreshGoalsPanel()
        {
            bool showQuests = _goalsViewMode == GoalsViewMode.Quests;
            bool showTasks = _goalsViewMode == GoalsViewMode.Tasks;

            SetToggleState(_btnGoalsQuests, showQuests);
            SetToggleState(_btnGoalsTasks, showTasks);

            if (_goalsList == null || _achievementCategoryRow == null || _achievementGrid == null)
                return;

            if (_goalsList != null)
                _goalsList.style.display = showTasks ? DisplayStyle.Flex : DisplayStyle.None;

            if (_questPanel != null)
                _questPanel.style.display = showQuests ? DisplayStyle.Flex : DisplayStyle.None;

            if (_achievementPanel != null)
                _achievementPanel.style.display = _achievementsModalOpen ? DisplayStyle.Flex : DisplayStyle.None;
            else
            {
                if (_achievementHeaderShell != null)
                    _achievementHeaderShell.style.display = _achievementsModalOpen ? DisplayStyle.Flex : DisplayStyle.None;

                if (_achievementScroll != null)
                    _achievementScroll.style.display = _achievementsModalOpen ? DisplayStyle.Flex : DisplayStyle.None;
                else if (_achievementGrid != null)
                    _achievementGrid.style.display = _achievementsModalOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (showQuests)
            {
                RefreshQuestView();
            }
            else
            {
                RefreshTasksView();
            }

            if (_achievementsModalOpen)
            {
                BuildAchievementCategoryButtons();
                RefreshAchievementsView();
            }
        }

        private void RefreshTasksView()
        {
            if (_goalsList == null)
                return;

            _goalsList.contentContainer.Clear();

            var p = Profile;
            if (p == null || progressionDirector == null)
            {
                _goalsList.Add(new Label("No tasks available."));
                return;
            }

            progressionDirector.GetDailyTierDisplays(_dailyTierBuffer);

            if (_dailyTierBuffer.Count == 0)
            {
                _goalsList.Add(new Label("No daily tasks generated yet."));
                return;
            }

            var activeByLadder = new Dictionary<string, ProgressionDirector.DailyTierDisplay>();

            for (int i = 0; i < _dailyTierBuffer.Count; i++)
            {
                var t = _dailyTierBuffer[i];
                if (string.IsNullOrEmpty(t.ladderId))
                    continue;

                if (!activeByLadder.TryGetValue(t.ladderId, out var existing))
                {
                    activeByLadder[t.ladderId] = t;
                    continue;
                }

                // Show the lowest tier that is not yet claimed.
                // If all tiers are claimed, keep the highest claimed tier as the row summary.
                bool tUnclaimed = !t.claimed;
                bool eUnclaimed = !existing.claimed;

                if (tUnclaimed && !eUnclaimed)
                {
                    activeByLadder[t.ladderId] = t;
                    continue;
                }

                if (tUnclaimed && eUnclaimed && t.tierIndex < existing.tierIndex)
                {
                    activeByLadder[t.ladderId] = t;
                    continue;
                }

                if (!tUnclaimed && !eUnclaimed && t.tierIndex > existing.tierIndex)
                {
                    activeByLadder[t.ladderId] = t;
                    continue;
                }
            }

            foreach (var pair in activeByLadder)
            {
                var tier = pair.Value;

                string title = pair.Key;
                if (progressionDirector.TryGetDailyLadderDefinition(pair.Key, out var ladder) && ladder != null)
                {
                    title = !string.IsNullOrWhiteSpace(ladder.displayNameOverride)
                        ? ladder.displayNameOverride.Trim()
                        : pair.Key;
                }

                _goalsList.Add(MakeTaskCard(title, tier));
            }
        }

        private void RefreshQuestView()
        {
            if (_questList == null)
                return;

            _questList.contentContainer.Clear();

            SetToggleState(_btnQuestCurrent, !_questShowCompleted);
            SetToggleState(_btnQuestCompleted, _questShowCompleted);

            if (questDirector == null)
            {
                _questList.Add(MakeQuestEmptyState("Quest tracking is not available in this scene yet."));
                return;
            }

            if (_questShowCompleted)
                questDirector.GetCompletedQuestStates(_questStateBuffer);
            else
                questDirector.GetActiveQuestStates(_questStateBuffer);

            SyncPrimaryQuestSelectionFromMiniHud();

            if (_questStateBuffer.Count == 0)
            {
                _questList.Add(MakeQuestEmptyState(_questShowCompleted
                    ? "No completed quests yet."
                    : "No active quests yet."));
                return;
            }

            if (!_questShowCompleted)
                EnsureQuestSelectionIsValid(_questStateBuffer);

            if (!_questShowCompleted)
                _questStateBuffer.Sort(CompareQuestStatesForHud);

            for (int i = 0; i < _questStateBuffer.Count; i++)
            {
                var state = _questStateBuffer[i];
                var definition = questDirector.GetQuestDefinition(state.questId);
                if (definition == null)
                    continue;

                _questList.Add(MakeQuestCard(definition, state));
            }
        }

        private void SyncPrimaryQuestSelectionFromMiniHud()
        {
            if (miniHudController == null)
                return;

            string miniFocusedQuestId = miniHudController.GetFocusedQuestId();
            if (!string.IsNullOrWhiteSpace(miniFocusedQuestId))
            {
                _primaryQuestId = miniFocusedQuestId;
                _expandedQuestIds.Add(miniFocusedQuestId);
            }
        }

        private void EnsureQuestSelectionIsValid(List<QuestRuntimeState> questStates)
        {
            if (questStates == null || questStates.Count == 0)
            {
                _primaryQuestId = null;
                return;
            }

            for (int i = 0; i < questStates.Count; i++)
            {
                var state = questStates[i];
                if (state != null && string.Equals(state.questId, _primaryQuestId, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            if (!_questShowCompleted)
            {
                for (int i = 0; i < questStates.Count; i++)
                {
                    var state = questStates[i];
                    if (state == null)
                        continue;

                    if (questDirector != null && questDirector.IsQuestTracked(state.questId))
                    {
                        _primaryQuestId = state.questId;
                        _expandedQuestIds.Add(state.questId);
                        return;
                    }
                }
            }

            _primaryQuestId = questStates[0]?.questId;
            if (!string.IsNullOrWhiteSpace(_primaryQuestId))
                _expandedQuestIds.Add(_primaryQuestId);
        }

        private void RefreshAchievementsView()
        {
            if (_achievementGrid == null)
                return;

            _achievementGrid.Clear();
            _achievementBuffer.Clear();

            AppendAchievementsForCategory(_achievementCategory, _achievementBuffer);

            var profile = Profile;
            _achievementBuffer.Sort((a, b) => CompareAchievementsForHud(a, b, profile));

            if (_achievementBuffer.Count == 0)
            {
                var empty = new Label("No authored achievements match this tab yet.");
                empty.AddToClassList("achievement-empty-state");
                _achievementGrid.Add(empty);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedAchievementId))
            {
                bool found = false;
                for (int i = 0; i < _achievementBuffer.Count; i++)
                {
                    if (_achievementBuffer[i] != null && _achievementBuffer[i].id == _selectedAchievementId)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    _selectedAchievementId = null;
            }

            for (int i = 0; i < _achievementBuffer.Count; i++)
            {
                var def = _achievementBuffer[i];
                if (def == null)
                    continue;

                bool completed = profile != null && profile.HasAchievement(def.id);
                bool claimed = profile != null && profile.HasClaimedAchievement(def.id);
                bool claimable = completed && !claimed;
                bool selected = _selectedAchievementId == def.id;

                float progress01 = profile != null ? Mathf.Clamp01(def.GetProgress01(profile)) : 0f;
                int progressPercent = Mathf.RoundToInt(progress01 * 100f);

                string progressText = profile != null
                    ? def.GetProgressText(profile)
                    : $"0/{Mathf.Max(1f, def.target):0}";

                string requirementText = def.GetRequirementText();
                string titleText = string.IsNullOrWhiteSpace(def.title) ? def.id : def.title;

                string subtitleText = requirementText;
                if (completed && profile != null && profile.TryGetAchievementUnlockedUtc(def.id, out var dt))
                    subtitleText = claimed ? $"Claimed • {dt.ToDateTimeUtc():dd MMM yyyy}" : $"Completed • {dt.ToDateTimeUtc():dd MMM yyyy}";
                else if (completed)
                    subtitleText = claimed ? "Reward claimed" : "Ready to claim";

                var hudCategory = def.GetMountainHudCategory();

                var card = new VisualElement();
                card.AddToClassList("achievement-card");
                card.AddToClassList($"is-{AchievementCategoryToCss(hudCategory)}");
                card.AddToClassList(selected ? "is-expanded" : "is-compact");

                if (claimable)
                    card.AddToClassList("is-complete-unclaimed");
                else if (claimed)
                    card.AddToClassList("is-complete-claimed");
                else if (progress01 >= 0.8f)
                    card.AddToClassList("is-close");

                string clickedId = def.id;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    _selectedAchievementId = _selectedAchievementId == clickedId ? null : clickedId;
                    RefreshAchievementsView();
                });

                var header = new VisualElement();
                header.AddToClassList("achievement-card-header");

                var icon = new Label(def.GetMountainHudGlyph());
                icon.AddToClassList("achievement-card-icon");
                header.Add(icon);

                var titleBlock = new VisualElement();
                titleBlock.AddToClassList("achievement-card-titleblock");

                var title = new Label(titleText);
                title.AddToClassList("achievement-card-title");
                titleBlock.Add(title);

                if (selected)
                {
                    var subtitle = new Label(subtitleText);
                    subtitle.AddToClassList("achievement-card-subtitle");
                    titleBlock.Add(subtitle);
                }

                header.Add(titleBlock);

                var state = new Label(
                    claimable ? "Claim" :
                    claimed ? "Claimed" :
                    $"{progressPercent}%");

                state.AddToClassList("achievement-card-state");

                if (claimable)
                    state.AddToClassList("is-claimable");
                else if (claimed)
                    state.AddToClassList("is-claimed");

                header.Add(state);

                var expandHint = new Label(selected ? "▴" : "▾");
                expandHint.AddToClassList("achievement-card-expandhint");
                header.Add(expandHint);

                card.Add(header);

                var progressTrack = new VisualElement();
                progressTrack.AddToClassList("achievement-progress-track");

                var progressFill = new VisualElement();
                progressFill.AddToClassList("achievement-progress-fill");
                progressFill.style.width = Length.Percent(progressPercent);
                progressTrack.Add(progressFill);

                card.Add(progressTrack);

                if (selected)
                {
                    if (!string.IsNullOrWhiteSpace(def.description))
                    {
                        var description = new Label(def.description);
                        description.AddToClassList("achievement-card-description");
                        card.Add(description);
                    }

                    var rewardLabel = new Label(def.GetRewardSummary());
                    rewardLabel.AddToClassList("achievement-card-reward");
                    card.Add(rewardLabel);
                }

                var footer = new VisualElement();
                footer.AddToClassList("achievement-card-footer");

                var progressLabel = new Label(claimed ? "Reward claimed" : (completed ? "Completed" : progressText));
                progressLabel.AddToClassList("achievement-card-progress");
                footer.Add(progressLabel);

                var categoryLabel = new Label(GetAchievementHudCategoryLabel(hudCategory));
                categoryLabel.AddToClassList("achievement-card-category");
                footer.Add(categoryLabel);

                if (selected && claimable)
                {
                    var claimButton = new Button(() =>
                    {
                        if (progressionDirector != null && progressionDirector.TryClaimAchievement(def.id, out _))
                        {
                            RefreshGoalsPanel();
                            RefreshPassPanel();
                            RefreshAchievementUnclaimedBadge();
                        }
                    })
                    {
                        text = "Claim Reward"
                    };
                    claimButton.AddToClassList("achievement-claim-button");
                    footer.Add(claimButton);
                }

                card.Add(footer);
                _achievementGrid.Add(card);
            }
        }


        private static string AchievementCategoryToCss(AchievementDefinitionSO.MountainHudCategory category)
        {
            return category switch
            {
                AchievementDefinitionSO.MountainHudCategory.Exploration => "exploration",
                AchievementDefinitionSO.MountainHudCategory.MountainMastery => "mastery",
                _ => "performance"
            };
        }

        private static string GetAchievementHudCategoryLabel(AchievementDefinitionSO.MountainHudCategory category)
        {
            return category switch
            {
                AchievementDefinitionSO.MountainHudCategory.Exploration => "Explore",
                AchievementDefinitionSO.MountainHudCategory.MountainMastery => "Mastery",
                _ => "Performance"
            };
        }

        private void RefreshPassPanel()
        {
            ApplyPassPanelViewState();

            if (skiPassManager == null || skiPassManager.Config == null)
            {
                if (_lblPassCurrent != null)
                    _lblPassCurrent.text = "No ski pass system found.";

                if (_lblPassExpiryMeta != null)
                    _lblPassExpiryMeta.text = string.Empty;

                if (_lblPassPermanentSummary != null)
                    _lblPassPermanentSummary.text = string.Empty;

                if (_passExpiryBar != null)
                    _passExpiryBar.style.display = DisplayStyle.None;

                if (_passOverviewList != null)
                    _passOverviewList.Clear();

                if (_passLiftList != null)
                    _passLiftList.Clear();

                return;
            }

            RefreshPassSummary();
            RebuildPassOverviewList();

            if (_passShowLiftDetail)
                RefreshPassLiftDetail();

            if (_passShowRescueTab)
                RefreshRescueDispatchPanel();
        }

        private void HandleSkiPassChanged()
        {
            RefreshPassPanel();
        }

        private void ShowPassInfoTab()
        {
            _passShowRescueTab = false;
            _passShowLiftDetail = false;
            ApplyPassPanelViewState();
            RefreshPassPanel();
        }

        private void ShowPassRescueTab()
        {
            _passShowRescueTab = true;
            _passShowLiftDetail = false;
            ApplyPassPanelViewState();
            RefreshPassPanel();
        }

        private void ShowPassOverview()
        {
            _passShowRescueTab = false;
            _passShowLiftDetail = false;
            ApplyPassPanelViewState();
            RefreshPassPanel();
        }

        private void ApplyPassPanelViewState()
        {
            SetToggleState(_btnPassTabInfo, !_passShowRescueTab);
            SetToggleState(_btnPassTabRescue, _passShowRescueTab);

            if (_passInfoView != null)
                _passInfoView.style.display = (!_passShowRescueTab && !_passShowLiftDetail) ? DisplayStyle.Flex : DisplayStyle.None;

            if (_passLiftDetailView != null)
                _passLiftDetailView.style.display = (!_passShowRescueTab && _passShowLiftDetail) ? DisplayStyle.Flex : DisplayStyle.None;

            if (_rescueUtilitiesView != null)
                _rescueUtilitiesView.style.display = _passShowRescueTab ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshPassSummary()
        {
            if (_lblPassCurrent != null)
                _lblPassCurrent.text = skiPassManager.GetCurrentPassDisplayName();

            bool hasTimedPass = skiPassManager.HasTimedPass;

            if (_lblPassExpiryMeta != null)
                _lblPassExpiryMeta.text = hasTimedPass
                    ? skiPassManager.GetRemainingTimeString()
                    : "No timed expiry on the current pass";

            if (_passExpiryBar != null)
                _passExpiryBar.style.display = hasTimedPass ? DisplayStyle.Flex : DisplayStyle.None;

            if (_passExpiryFill != null && hasTimedPass)
            {
                float fraction = skiPassManager.GetRemainingFraction01();
                _passExpiryFill.style.width = Length.Percent(Mathf.RoundToInt(fraction * 100f));
            }

            if (_lblPassPermanentSummary != null)
                _lblPassPermanentSummary.text = BuildPermanentPassSummary();
        }

        private string BuildPermanentPassSummary()
        {
            BuildPassPanelEntries(_passPanelEntryBuffer);

            List<string> names = new List<string>();
            for (int i = 0; i < _passPanelEntryBuffer.Count; i++)
            {
                var entry = _passPanelEntryBuffer[i];
                if (!entry.isPermanent)
                    continue;

                names.Add(entry.displayName);
            }

            if (names.Count == 0)
                return "No permanent passes earned yet.";

            return "Permanent passes: " + string.Join(", ", names);
        }

        private void RebuildPassOverviewList()
        {
            if (_passOverviewList == null)
                return;

            _passOverviewList.Clear();
            BuildPassPanelEntries(_passPanelEntryBuffer);

            if (_passPanelEntryBuffer.Count == 0)
            {
                _passOverviewList.Add(new Label("No passes available."));
                return;
            }

            for (int i = 0; i < _passPanelEntryBuffer.Count; i++)
            {
                PassPanelEntry entry = _passPanelEntryBuffer[i];

                string meta = entry.isCurrent && entry.isPermanent
                    ? "Current • Permanent"
                    : entry.isCurrent
                        ? "Current access"
                        : entry.isPermanent
                            ? "Permanent unlock"
                            : "Pass";

                var button = new Button(() => OpenPassLiftDetail(entry.passId, entry.level, entry.displayName))
                {
                    text = $"{entry.displayName}\n{meta}"
                };

                button.AddToClassList("pass-entry-button");

                if (entry.isCurrent)
                    button.AddToClassList("is-current");

                if (entry.isPermanent)
                    button.AddToClassList("is-permanent");

                _passOverviewList.Add(button);
            }
        }

        private void OpenPassLiftDetail(string passId, int level, string displayName)
        {
            _passDetailPassId = passId;
            _passDetailLevel = Mathf.Max(0, level);
            _passDetailDisplayName = string.IsNullOrWhiteSpace(displayName) ? passId : displayName;

            _passShowRescueTab = false;
            _passShowLiftDetail = true;

            ApplyPassPanelViewState();
            RefreshPassLiftDetail();
        }

        private void RefreshPassLiftDetail()
        {
            if (_passLiftList == null)
                return;

            _passLiftList.Clear();

            if (_lblPassDetailTitle != null)
                _lblPassDetailTitle.text = _passDetailDisplayName;

            _passLiftBuffer.Clear();

            LiftLine[] lifts = FindObjectsOfType<LiftLine>(includeInactive: false);
            if (lifts != null)
            {
                for (int i = 0; i < lifts.Length; i++)
                {
                    LiftLine lift = lifts[i];
                    if (lift == null)
                        continue;

                    if (PassGrantsLiftAccess(_passDetailPassId, _passDetailLevel, lift))
                        _passLiftBuffer.Add(lift);
                }
            }

            _passLiftBuffer.Sort((a, b) => string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase));

            if (_lblPassDetailMeta != null)
                _lblPassDetailMeta.text = $"{_passLiftBuffer.Count} lifts";

            if (_passLiftBuffer.Count == 0)
            {
                _passLiftList.Add(new Label("No lifts are mapped to this pass yet."));
                return;
            }

            for (int i = 0; i < _passLiftBuffer.Count; i++)
            {
                LiftLine capturedLift = _passLiftBuffer[i];

                var button = new Button(() => FocusLiftOnMap(capturedLift))
                {
                    text = capturedLift != null ? capturedLift.name : "Lift"
                };

                button.AddToClassList("pass-lift-button");
                _passLiftList.Add(button);
            }
        }

        private void BuildPassPanelEntries(List<PassPanelEntry> dst)
        {
            dst.Clear();

            if (skiPassManager == null || skiPassManager.Config == null)
                return;

            var cfg = skiPassManager.Config;

            AddOrUpdatePassPanelEntry(
                dst,
                cfg.GetPassIdForLevel(skiPassManager.CurrentLevel),
                skiPassManager.CurrentLevel,
                skiPassManager.GetCurrentPassDisplayName(),
                isCurrent: true,
                isPermanent: skiPassManager.IsPassPermanentlyUnlocked(skiPassManager.CurrentLevel));

            var profile = Profile;
            if (profile != null && profile.permanentlyUnlockedPassLevels != null)
            {
                for (int i = 0; i < profile.permanentlyUnlockedPassLevels.Count; i++)
                {
                    int level = Mathf.Max(0, profile.permanentlyUnlockedPassLevels[i]);
                    string passId = cfg.GetPassIdForLevel(level);
                    string displayName = cfg.Get(level) != null ? cfg.Get(level).displayName : $"Pass {level}";
                    AddOrUpdatePassPanelEntry(dst, passId, level, displayName, isCurrent: level == skiPassManager.CurrentLevel, isPermanent: true);
                }
            }
        }

        private static void AddOrUpdatePassPanelEntry(List<PassPanelEntry> dst, string passId, int level, string displayName, bool isCurrent, bool isPermanent)
        {
            for (int i = 0; i < dst.Count; i++)
            {
                if (string.Equals(dst[i].passId, passId, StringComparison.OrdinalIgnoreCase))
                {
                    PassPanelEntry existing = dst[i];
                    existing.isCurrent |= isCurrent;
                    existing.isPermanent |= isPermanent;

                    if (!string.IsNullOrWhiteSpace(displayName))
                        existing.displayName = displayName;

                    existing.level = Mathf.Max(existing.level, level);
                    dst[i] = existing;
                    return;
                }
            }

            dst.Add(new PassPanelEntry
            {
                passId = passId,
                level = Mathf.Max(0, level),
                displayName = displayName,
                isCurrent = isCurrent,
                isPermanent = isPermanent
            });
        }

        private bool PassGrantsLiftAccess(string passId, int passLevel, LiftLine lift)
        {
            if (lift == null || skiPassManager == null || skiPassManager.Config == null)
                return false;

            if (!string.IsNullOrWhiteSpace(lift.RequiredPassId) && !string.IsNullOrWhiteSpace(passId))
                return skiPassManager.Config.PassGrantsAccessTo(passId, lift.RequiredPassId);

            return passLevel >= Mathf.Max(0, lift.RequiredPassLevel);
        }

        private void FocusLiftOnMap(LiftLine lift)
        {
            if (_mapUI == null || lift == null || !TryGetLiftPolylineId(lift, out string polylineId))
                return;

            _mapUI.SelectPolylineById(polylineId, center: false, minZoom: -1f);
            _mapUI.FramePolylineById(polylineId, paddingPx: 28f, minZoom: 0.85f);
        }

        private bool TryGetLiftPolylineId(LiftLine lift, out string polylineId)
        {
            polylineId = null;
            if (lift == null)
                return false;

            if (mapData != null && mapData.Polylines != null)
            {
                for (int i = 0; i < mapData.Polylines.Count; i++)
                {
                    var poly = mapData.Polylines[i];
                    if (!poly.IsValid || poly.lineType != MapLineType.SkiLift)
                        continue;

                    if (string.Equals(poly.id, lift.name, StringComparison.Ordinal) ||
                        string.Equals(poly.displayName, lift.name, StringComparison.OrdinalIgnoreCase))
                    {
                        polylineId = poly.id;
                        return true;
                    }
                }
            }

            polylineId = lift.gameObject.name;
            return !string.IsNullOrWhiteSpace(polylineId);
        }

        private void RequestEmergencySOS()
        {
            if (_sosRoutine != null)
                return;

            _sosRoutine = StartCoroutine(CoEmergencySOS());
        }

        private System.Collections.IEnumerator CoEmergencySOS()
        {
            ResolveReferences();

            if (blackoutTransition == null)
            {
                _rescueDispatchFeedback = "No blackout transition component was found.";
                RefreshPassPanel();
                _sosRoutine = null;
                yield break;
            }

            bool success = blackoutTransition.TryRespawnAtNearestResort(out string message);
            _rescueDispatchFeedback = success
                ? message
                : (string.IsNullOrWhiteSpace(message) ? "SOS return failed." : message);

            RefreshPassPanel();

            if (success)
            {
                while (blackoutTransition != null && blackoutTransition.IsTransitionActive)
                    yield return null;
            }

            RefreshPassPanel();
            _sosRoutine = null;
        }

        private bool TryFindBestResortExit(Vector3 fromPosition, out Transform exitPoint)
        {
            exitPoint = null;

            SkiResortZone[] zones = FindObjectsOfType<SkiResortZone>(includeInactive: false);
            float bestSqr = float.PositiveInfinity;

            for (int i = 0; i < zones.Length; i++)
            {
                SkiResortZone zone = zones[i];
                if (zone == null || zone.exitPoint == null)
                    continue;

                float sqr = (zone.exitPoint.position - fromPosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    exitPoint = zone.exitPoint;
                }
            }

            return exitPoint != null;
        }

        private void SetScreenFadeOpacity(float opacity)
        {
            _screenFadeOpacity = Mathf.Clamp01(opacity);

            if (_screenFade == null)
                return;

            _screenFade.style.opacity = _screenFadeOpacity;
            _screenFade.style.display = _screenFadeOpacity > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private System.Collections.IEnumerator FadeScreenTo(float targetOpacity, float duration)
        {
            float start = _screenFadeOpacity;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            SetScreenFadeOpacity(start);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetScreenFadeOpacity(Mathf.Lerp(start, targetOpacity, t));
                yield return null;
            }

            SetScreenFadeOpacity(targetOpacity);
        }

        private void OnMapPolylineSelected(MapPolyline poly)
        {
            if (!poly.IsValid)
                return;

            if (poly.lineType == MapLineType.RaceCourse && TryFindRaceByPolylineId(poly.id, out var race))
            {
                _selectedKind = SelectedMapKind.POI;
                _selectedId = poly.id;
                _selectedTitle = race.RaceName;
                _selectedBody = BuildSelectedRaceBody(race);
                RefreshContextPanel();
                return;
            }

            _selectedId = poly.id;
            _selectedTitle = string.IsNullOrWhiteSpace(poly.displayName) ? poly.id : poly.displayName;

            if (poly.lineType == MapLineType.SkiRun)
            {
                _selectedKind = SelectedMapKind.Run;
                _selectedBody = BuildSelectedRunBody(poly.id, _selectedTitle);
            }
            else if (poly.lineType == MapLineType.SkiLift)
            {
                _selectedKind = SelectedMapKind.Lift;
                _selectedBody = BuildSelectedLiftBody(poly.id, _selectedTitle);
            }
            else
            {
                _selectedKind = SelectedMapKind.None;
                _selectedBody = string.Empty;
            }

            RefreshContextPanel();
        }

        private void OnMapMarkerSelected(MapMarker marker)
        {
            if (!marker.IsValid)
                return;

            _selectedId = marker.id;
            _selectedTitle = string.IsNullOrWhiteSpace(marker.displayName) ? marker.id : marker.displayName;

            switch (marker.type)
            {
                case POIType.SkiRun:
                    _selectedKind = SelectedMapKind.Run;
                    _selectedBody = BuildSelectedRunBody(marker.id, _selectedTitle);
                    break;

                case POIType.SkiLift:
                    _selectedKind = SelectedMapKind.Lift;
                    _selectedBody = BuildSelectedLiftBody(marker.id, _selectedTitle);
                    break;

                default:
                    _selectedKind = SelectedMapKind.POI;
                    _selectedBody = BuildSelectedPoiBody(marker.id, marker.meta);
                    break;
            }

            RefreshContextPanel();
        }

        private void OnMapMarkerDoubleClicked(MapMarker marker)
        {
            if (_waypointManager == null || !marker.IsValid)
                return;

            NavigationTargetKind kind = marker.type switch
            {
                POIType.SkiLift => NavigationTargetKind.Lift,
                POIType.SkiRun => NavigationTargetKind.PointOfInterest,
                _ => NavigationTargetKind.PointOfInterest
            };

            string sourceKey = $"marker:{marker.id}";
            string label = string.IsNullOrWhiteSpace(marker.displayName) ? marker.id : marker.displayName;
            Vector3 waypointWorld = ResolveWaypointWorldPositionForMarker(marker);

            Color waypointColour = ResolveMarkerWaypointColour(marker);

            _waypointManager.ToggleOrCycleSourceWaypoint(
                sourceKey,
                label,
                waypointWorld,
                kind,
                waypointColour,
                setActive: true);
        }

        private Vector3 ResolveWaypointWorldPositionForMarker(MapMarker marker)
        {
            if (!marker.IsValid)
                return marker.worldPosition;

            // For ski run markers, use the start of the run polyline so the
            // map connector / dotted line targets the actual run marker anchor,
            // not a midpoint-like fallback position.
            if (marker.type == POIType.SkiRun && mapData != null && mapData.Polylines != null)
            {
                string polylineId = marker.id;

                int suffixIndex = polylineId.LastIndexOf("__", StringComparison.Ordinal);
                if (suffixIndex > 0)
                    polylineId = polylineId.Substring(0, suffixIndex);

                var polylines = mapData.Polylines;
                for (int i = 0; i < polylines.Count; i++)
                {
                    var poly = polylines[i];
                    if (!poly.IsValid || !string.Equals(poly.id, polylineId, StringComparison.Ordinal))
                        continue;

                    if (poly.Has3DPoints && poly.pointsWorld != null && poly.pointsWorld.Count > 0)
                        return poly.pointsWorld[0];

                    if (poly.pointsWorldXZ != null && poly.pointsWorldXZ.Count > 0)
                    {
                        Vector2 p = poly.pointsWorldXZ[0];
                        return new Vector3(p.x, marker.worldPosition.y, p.y);
                    }

                    break;
                }
            }

            return marker.worldPosition;
        }

        private static Color ResolveMarkerWaypointColour(MapMarker marker)
        {
            Color c = marker.color;

            // Guard against empty / uninitialized authored colours.
            if (c.a <= 0.001f)
            {
                switch (marker.category)
                {
                    case POICategory.Resort:
                        return new Color(0.25f, 0.75f, 1f, 1f);

                    case POICategory.Shop:
                        return new Color(1f, 0.45f, 0.75f, 1f);

                    case POICategory.Kiosk:
                        return new Color(1f, 0.75f, 0.2f, 1f);

                    case POICategory.Service:
                        return new Color(0.45f, 1f, 0.45f, 1f);

                    case POICategory.Landmark:
                        return new Color(0.8f, 0.8f, 1f, 1f);

                    case POICategory.Race:
                        return new Color(1.00f, 0.55f, 0.20f, 1f);

                    case POICategory.Medical:
                        return new Color(0.20f, 1.00f, 1.00f, 1f);

                    case POICategory.Vehicle:
                        return new Color(1.00f, 0.90f, 0.25f, 1f);
                }
            }

            return c;
        }

        private void OnMapMarkerRightDoubleClicked(MapMarker marker)
        {
            if (_waypointManager == null || !marker.IsValid)
                return;

            string sourceKey = $"marker:{marker.id}";
            _waypointManager.RemoveWaypointBySourceKey(sourceKey);
        }

        private void OnMapBackgroundWorldClicked(Vector3 worldPosition)
        {
            // Intentionally left blank.
            // Waypoint selection / colour cycling / deletion is now marker-driven in PhoneMapPageUI.
        }

        private void OnMapBackgroundWorldDoubleClicked(Vector3 worldPosition)
        {
            if (_waypointManager == null)
                return;

            _waypointManager.AddCustomWaypoint(worldPosition, "Custom Waypoint", setActive: true);
        }

        private void OnMapWaypointClicked(string waypointId)
        {
            if (_waypointManager == null)
                return;

            _waypointManager.SelectWaypoint(waypointId, setActive: true);

            if (_waypointManager.TryGetWaypoint(waypointId, out var waypoint))
            {
                _selectedKind = SelectedMapKind.Waypoint;
                _selectedId = waypoint.id;
                _selectedTitle = string.IsNullOrWhiteSpace(waypoint.displayName) ? "Waypoint" : waypoint.displayName;
                _selectedBody = $"Navigation point • {waypoint.kind} • {waypoint.worldPosition.x:0}, {waypoint.worldPosition.z:0}";
                RefreshContextPanel();
            }
        }

        private void OnMapWaypointDoubleClicked(string waypointId)
        {
            if (_waypointManager == null)
                return;

            if (_waypointManager.IsWaypointSelected(waypointId))
                _waypointManager.CycleWaypointColour(waypointId);
            else
                _waypointManager.SelectWaypoint(waypointId, setActive: true);
        }

        private void OnMapWaypointDeleteRequested(string waypointId)
        {
            _waypointManager?.RemoveWaypoint(waypointId);

            if (string.Equals(_selectedId, waypointId, StringComparison.Ordinal))
                OnMapSelectionCleared();
        }

        private void OnMapWaypointLabelEditRequested(string waypointId)
        {
            // Inline rename is now handled directly inside PhoneMapPageUI.
        }

        private void OnMapSelectionCleared()
        {
            _selectedKind = SelectedMapKind.None;
            _selectedId = null;
            _selectedTitle = null;
            _selectedBody = null;
            RefreshContextPanel();
        }

        private string BuildSelectedRunBody(string runId, string displayName)
        {
            var p = Profile;
            if (p == null)
                return "No profile loaded.";

            string difficulty = "--";
            if (PointOfInterestRegistry.Instance != null &&
                PointOfInterestRegistry.Instance.TryGetById(runId, out var poi) &&
                poi.source is SkiRunLine line)
            {
                difficulty = FormatRunDifficulty(line.Difficulty);
            }

            var summary = PhoneHUDMountainSummaryBuilder.BuildRunMapSummary(p, runId, displayName, difficulty);
            return summary.body;
        }

        private string BuildSelectedLiftBody(string liftId, string displayName)
        {
            var p = Profile;
            if (p == null)
                return "No profile loaded.";

            LiftLine lift = FindLiftById(liftId);
            if (lift == null)
                return $"Lift\nRides: {p.GetLiftRideCount(liftId, session: false)}";

            return PhoneHUDMountainSummaryBuilder.BuildLiftMapSummary(p, lift, displayName, skiPassManager);
        }

        private string BuildSelectedPoiBody(string poiId, string fallbackMeta)
        {
            return PhoneHUDMountainSummaryBuilder.BuildPoiMapSummary(Profile, poiId, fallbackMeta);
        }

        private void RefreshContextAttemptsForSelection()
        {
            if (_selectedKind != SelectedMapKind.Run || _contextAttemptsList == null)
            {
                _contextHistoryAvailable = false;
                ApplyContextHistoryVisibility();
                return;
            }

            RefreshContextAttemptsForActiveRun(_selectedId);
        }

        private void RefreshContextAttemptsForActiveRun(string runId)
        {
            if (_contextAttemptsList == null || Profile == null || string.IsNullOrWhiteSpace(runId))
            {
                _contextHistoryAvailable = false;
                ApplyContextHistoryVisibility();
                return;
            }

            _contextAttemptsList.contentContainer.Clear();

            if (!Profile.TryGetRunRecord(runId, out var record) || record == null || record.attempts == null || record.attempts.Count == 0)
            {
                _contextHistoryAvailable = false;
                ApplyContextHistoryVisibility();
                return;
            }

            _contextHistoryAvailable = true;

            for (int i = record.attempts.Count - 1; i >= 0; i--)
            {
                var a = record.attempts[i];
                if (a == null)
                    continue;

                string title = a.isCompletion ? "Completion" : "Attempt";
                string body =
                    $"{title} • {FormatSeconds(a.timeSeconds)} • {FormatSpeed(a.topSpeedMps)}\n" +
                    $"{(a.isCompletion ? "Complete" : $"Progress {Mathf.RoundToInt(a.coveredFraction01 * 100f)}%")} • Stacks {a.stacks}";

                var row = new VisualElement();
                row.AddToClassList("attempt-row");

                var lbl = new Label(body);
                lbl.AddToClassList("attempt-row-label");
                row.Add(lbl);

                _contextAttemptsList.Add(row);
            }

            ApplyContextHistoryVisibility();
        }

        private void ToggleContextExpand()
        {
            if (!_contextHistoryAvailable)
                return;

            _contextHistoryExpanded = !_contextHistoryExpanded;
            ApplyContextHistoryVisibility();
        }

        private void ApplyContextHistoryVisibility()
        {
            if (_contextAttemptsList != null)
            {
                _contextAttemptsList.style.display =
                    (_contextHistoryAvailable && _contextHistoryExpanded)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            if (_btnContextExpand != null)
            {
                _btnContextExpand.SetEnabled(_contextHistoryAvailable);
                _btnContextExpand.text = !_contextHistoryAvailable
                    ? "No History"
                    : (_contextHistoryExpanded ? "Hide History" : "Show History");
            }
        }

        private void BuildNearbyRunsContext()
        {
            var reg = PointOfInterestRegistry.Instance;
            var playerT = skiController != null ? skiController.transform : null;

            if (reg == null || playerT == null)
            {
                _lblContextTitle.text = "Mountain";
                _lblContextBody.text = "Use the overlay to inspect routes, check conditions, and plan your next move.";
                SetContextPresentation(
                    icon: "🔎",
                    status: "Overview",
                    guidance: "Select a run, lift, or landmark to focus the panel and reveal more specific stats.");
                _contextMetaRow?.Clear();
                _contextHighlights?.Clear();
                _contextHistoryAvailable = false;
                ApplyContextHistoryVisibility();
                return;
            }

            float bestA = float.MaxValue, bestB = float.MaxValue, bestC = float.MaxValue;
            string runA = null, runB = null, runC = null;

            for (int i = 0; i < reg.Current.Count; i++)
            {
                var poi = reg.Current[i];
                if (poi.type != POIType.SkiRun) continue;

                float d = Vector3.SqrMagnitude(poi.position - playerT.position);
                string name = string.IsNullOrWhiteSpace(poi.displayName) ? poi.id : poi.displayName;

                if (d < bestA)
                {
                    bestC = bestB; runC = runB;
                    bestB = bestA; runB = runA;
                    bestA = d; runA = name;
                }
                else if (d < bestB)
                {
                    bestC = bestB; runC = runB;
                    bestB = d; runB = name;
                }
                else if (d < bestC)
                {
                    bestC = d; runC = name;
                }
            }

            _lblContextTitle.text = "Nearby Runs";
            _lblContextBody.text = "You are currently free-roaming. Pick a nearby descent, inspect a lift, or drop a waypoint to start navigating with intent.";

            SetContextPresentation(
                icon: "🔎",
                status: "Exploring",
                guidance: "Single click to inspect. Double click markers to create or cycle linked waypoints. Double click empty map space for a custom waypoint.");

            _contextMetaRow?.Clear();
            _contextHighlights?.Clear();

            AddContextMetaChip("Status", "Exploring", accent: true);
            AddContextMetaChip("Runs Nearby", CountValidStrings(runA, runB, runC).ToString());

            if (_contextHighlights != null)
            {
                if (!string.IsNullOrWhiteSpace(runA))
                    _contextHighlights.Add(MakeContextHighlightCard(runA, $"{Mathf.Sqrt(bestA):0} m away", "Closest route from your current position"));

                if (!string.IsNullOrWhiteSpace(runB))
                    _contextHighlights.Add(MakeContextHighlightCard(runB, $"{Mathf.Sqrt(bestB):0} m away", "Alternative nearby descent"));

                if (!string.IsNullOrWhiteSpace(runC))
                    _contextHighlights.Add(MakeContextHighlightCard(runC, $"{Mathf.Sqrt(bestC):0} m away", "Another option within quick reach"));
            }

            _contextHistoryAvailable = false;
            ApplyContextHistoryVisibility();
        }

        private static int CompareAchievementsForHud(AchievementDefinitionSO a, AchievementDefinitionSO b, PlayerStatsProfile profile)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            bool aCompleted = profile != null && profile.HasAchievement(a.id);
            bool bCompleted = profile != null && profile.HasAchievement(b.id);

            bool aClaimed = profile != null && profile.HasClaimedAchievement(a.id);
            bool bClaimed = profile != null && profile.HasClaimedAchievement(b.id);

            int aBucket = aCompleted ? (aClaimed ? 2 : 0) : 1;
            int bBucket = bCompleted ? (bClaimed ? 2 : 0) : 1;

            int bucketCompare = aBucket.CompareTo(bBucket);
            if (bucketCompare != 0)
                return bucketCompare;

            float aPct = profile != null ? Mathf.Clamp01(a.GetProgress01(profile)) : 0f;
            float bPct = profile != null ? Mathf.Clamp01(b.GetProgress01(profile)) : 0f;

            int pctCompare = bPct.CompareTo(aPct);
            if (pctCompare != 0)
                return pctCompare;

            int targetCompare = a.target.CompareTo(b.target);
            if (targetCompare != 0)
                return targetCompare;

            string aTitle = string.IsNullOrWhiteSpace(a.title) ? a.id : a.title;
            string bTitle = string.IsNullOrWhiteSpace(b.title) ? b.id : b.title;
            return string.Compare(aTitle, bTitle, StringComparison.OrdinalIgnoreCase);
        }

        private void BuildAchievementCategoryButtons()
        {
            if (_achievementCategoryRow == null)
                return;

            _achievementCategoryRow.Clear();

            AddAchievementCategoryButton(BuildAchievementCategoryLabel(AchievementCategory.Performance), AchievementCategory.Performance);
            AddAchievementCategoryButton(BuildAchievementCategoryLabel(AchievementCategory.Exploration), AchievementCategory.Exploration);
            AddAchievementCategoryButton(BuildAchievementCategoryLabel(AchievementCategory.MountainMastery), AchievementCategory.MountainMastery);
        }

        private string BuildAchievementCategoryLabel(AchievementCategory category)
        {
            if (progressionDirector == null)
                return category switch
                {
                    AchievementCategory.Exploration => "Explore\n-- / --",
                    AchievementCategory.MountainMastery => "Mastery\n-- / --",
                    _ => "Performance\n-- / --"
                };

            _achievementQueryBuffer.Clear();
            progressionDirector.GetAllAchievements(_achievementQueryBuffer);

            var profile = Profile;
            var wantedCategory = ToDefinitionHudCategory(category);

            int total = 0;
            int unlocked = 0;
            _achievementDedup.Clear();

            for (int i = 0; i < _achievementQueryBuffer.Count; i++)
            {
                var def = _achievementQueryBuffer[i];
                if (def == null || string.IsNullOrWhiteSpace(def.id))
                    continue;

                if (def.GetMountainHudCategory() != wantedCategory)
                    continue;

                if (!_achievementDedup.Add(def.id))
                    continue;

                total++;

                if (profile != null && profile.HasAchievement(def.id))
                    unlocked++;
            }

            string baseLabel = category switch
            {
                AchievementCategory.Exploration => "Explore",
                AchievementCategory.MountainMastery => "Mastery",
                _ => "Performance"
            };

            return $"{baseLabel}\n{unlocked} / {total}";
        }
        private void AddAchievementCategoryButton(string label, AchievementCategory category)
        {
            var btn = new Button(() =>
            {
                _achievementCategory = category;
                RefreshGoalsPanel();
            })
            {
                text = label
            };

            btn.AddToClassList("goal-subtab-button");
            btn.AddToClassList("achievement-category-button");

            if (_achievementCategory == category)
                btn.AddToClassList("is-selected");

            _achievementCategoryRow.Add(btn);
        }

        private void AppendAchievementsForCategory(AchievementCategory category, List<AchievementDefinitionSO> buffer)
        {
            buffer.Clear();
            _achievementDedup.Clear();
            _achievementQueryBuffer.Clear();

            if (progressionDirector == null)
                return;

            var wantedCategory = ToDefinitionHudCategory(category);

            progressionDirector.GetAllAchievements(_achievementQueryBuffer);

            for (int i = 0; i < _achievementQueryBuffer.Count; i++)
            {
                var def = _achievementQueryBuffer[i];
                if (def == null || string.IsNullOrWhiteSpace(def.id))
                    continue;

                if (def.GetMountainHudCategory() != wantedCategory)
                    continue;

                if (_achievementDedup.Add(def.id))
                    buffer.Add(def);
            }
        }

        private void AddContextMetaChip(string label, string value, bool accent = false)
        {
            if (_contextMetaRow == null || string.IsNullOrWhiteSpace(value))
                return;

            var chip = new VisualElement();
            chip.AddToClassList("context-meta-chip");
            chip.AddToClassList("context-meta-chip--compact");

            if (accent)
                chip.AddToClassList("is-accent");

            chip.tooltip = string.IsNullOrWhiteSpace(label)
                ? value
                : $"{label}: {value}";

            var labelEl = new Label(string.IsNullOrWhiteSpace(label) ? string.Empty : $"{label}:");
            labelEl.AddToClassList("context-meta-chip-label");
            chip.Add(labelEl);

            var valueEl = new Label(value);
            valueEl.AddToClassList("context-meta-chip-value");
            chip.Add(valueEl);

            _contextMetaRow.Add(chip);
        }

        private VisualElement MakeContextHighlightCard(string title, string value, string detail)
        {
            var card = new VisualElement();
            card.AddToClassList("context-highlight-card");
            card.AddToClassList("context-highlight-card--compact");

            if (!string.IsNullOrWhiteSpace(detail))
                card.tooltip = $"{title}: {value}\n{detail}";
            else
                card.tooltip = $"{title}: {value}";

            var titleEl = new Label(string.IsNullOrWhiteSpace(title) ? string.Empty : $"{title}:");
            titleEl.AddToClassList("context-highlight-title");
            card.Add(titleEl);

            var valueEl = new Label(value);
            valueEl.AddToClassList("context-highlight-value");
            card.Add(valueEl);

            if (!string.IsNullOrWhiteSpace(detail))
            {
                var detailEl = new Label(detail);
                detailEl.AddToClassList("context-highlight-detail");
                card.Add(detailEl);
            }

            return card;
        }

        private VisualElement MakeStatTile(string label, string value, bool accent = false)
        {
            var tile = new VisualElement();
            tile.AddToClassList("stat-tile");
            if (accent)
                tile.AddToClassList("is-accent");

            var labelEl = new Label(label);
            labelEl.AddToClassList("stat-tile-label");
            tile.Add(labelEl);

            var valueEl = new Label(value);
            valueEl.AddToClassList("stat-tile-value");
            tile.Add(valueEl);

            return tile;
        }

        private VisualElement MakeQuestEmptyState(string text)
        {
            var label = new Label(text);
            label.AddToClassList("quest-empty-state");
            return label;
        }

        private VisualElement MakeQuestCard(QuestDefinitionSO definition, QuestRuntimeState state)
        {
            var card = new VisualElement();
            card.AddToClassList("quest-card");

            Color accent = QuestVisualUtility.GetQuestAccent(definition.SafeId);
            bool tracked = questDirector != null && questDirector.IsQuestTracked(definition.SafeId);
            bool isPrimary = !_questShowCompleted && string.Equals(_primaryQuestId, definition.SafeId, StringComparison.OrdinalIgnoreCase);
            bool expanded = _expandedQuestIds.Contains(definition.SafeId);
            if (tracked)
                card.AddToClassList("is-tracked");
            if (isPrimary)
                card.AddToClassList("is-primary");

            if (state.completed)
                card.AddToClassList("is-completed");

            card.tooltip = state.completed || _questShowCompleted
                ? "Click to expand or collapse."
                : (isPrimary ? "Click to expand. Double-click to unpin." : "Click to expand. Double-click to pin.");

            card.AddToClassList(expanded ? "is-expanded" : "is-collapsed");
            card.RegisterCallback<ClickEvent>(evt =>
            {
                if (!state.completed && !_questShowCompleted && evt.clickCount >= 2)
                {
                    TogglePrimaryQuest(definition.SafeId);
                    RefreshQuestView();
                    evt.StopPropagation();
                    return;
                }

                ToggleQuestExpanded(definition.SafeId);
                RefreshQuestView();
            });

            var accentBar = new VisualElement();
            accentBar.AddToClassList("quest-card-accent");
            accentBar.style.backgroundColor = new StyleColor(accent);
            card.Add(accentBar);

            var header = new VisualElement();
            header.AddToClassList("quest-card-header");

            var titleBlock = new VisualElement();
            titleBlock.AddToClassList("quest-card-titleblock");

            var title = new Label(string.IsNullOrWhiteSpace(definition.title) ? definition.SafeId : definition.title);
            title.AddToClassList("quest-card-title");
            titleBlock.Add(title);

            string stageText = state.completed
                ? "Completed"
                : BuildQuestStageLabel(definition, state);
            string statusText = state.completed ? "Complete" : (isPrimary ? "Pinned" : (tracked ? "Tracking" : "Active"));

            var subtitle = new Label($"{stageText} • {statusText}");
            subtitle.AddToClassList("quest-card-subtitle");
            titleBlock.Add(subtitle);

            if (isPrimary)
            {
                var pinIndicator = new Label("PINNED");
                pinIndicator.AddToClassList("quest-card-pin-indicator");
                titleBlock.Add(pinIndicator);
            }

            header.Add(titleBlock);

            var headerActions = new VisualElement();
            headerActions.AddToClassList("quest-card-header-actions");

            var stage = definition.GetStage(state.currentStageIndex);
            TryResolveNextQuestObjective(definition, state, out var nextObjectiveDefinition, out var nextObjectiveState);

            if (!state.completed)
            {
                var primaryButton = new Button(() =>
                {
                    TogglePrimaryQuest(definition.SafeId);
                    RefreshQuestView();
                })
                {
                    text = isPrimary ? "Pinned" : "Pin"
                };
                primaryButton.AddToClassList("quest-primary-button");
                if (isPrimary)
                    primaryButton.AddToClassList("is-active");
                primaryButton.tooltip = isPrimary ? "Unpin this quest." : "Pin this quest to the top.";
                SwallowClick(primaryButton);
                headerActions.Add(primaryButton);

                var trackButton = new Button(() =>
                {
                    if (questDirector != null)
                    {
                        bool newTracked = !questDirector.IsQuestTracked(definition.SafeId);
                        questDirector.SetQuestTracked(definition.SafeId, newTracked);

                        if (!newTracked && string.Equals(_primaryQuestId, definition.SafeId, StringComparison.OrdinalIgnoreCase))
                            ClearPrimaryQuest(definition.SafeId);

                        RefreshQuestView();
                    }
                })
                {
                    text = tracked ? "Tracking" : "Track"
                };
                trackButton.AddToClassList("quest-track-button");
                if (tracked)
                    trackButton.AddToClassList("is-active");
                trackButton.tooltip = tracked ? "Stop showing this quest in the HUD." : "Show this quest in the HUD.";
                SwallowClick(trackButton);
                headerActions.Add(trackButton);
            }

            header.Add(headerActions);
            card.Add(header);

            string promptText = nextObjectiveDefinition != null ? BuildQuestObjectivePrompt(nextObjectiveDefinition) : string.Empty;
            if (!string.IsNullOrWhiteSpace(promptText))
            {
                var prompt = new Label(promptText);
                prompt.AddToClassList("quest-card-prompt");
                prompt.style.backgroundColor = new StyleColor(new Color(accent.r, accent.g, accent.b, 0.12f));
                card.Add(prompt);
            }

            float questProgress01 = GetQuestProgress01(state);
            string questProgressSummary = BuildQuestProgressSummary(state);

            var questProgressRow = new VisualElement();
            questProgressRow.AddToClassList("quest-card-progress-row");

            var questProgressLabel = new Label(questProgressSummary);
            questProgressLabel.AddToClassList("quest-card-progress-label");
            questProgressRow.Add(questProgressLabel);

            var questProgressPercent = new Label($"{Mathf.RoundToInt(questProgress01 * 100f)}%");
            questProgressPercent.AddToClassList("quest-card-progress-percent");
            questProgressRow.Add(questProgressPercent);
            card.Add(questProgressRow);

            var questTrack = new VisualElement();
            questTrack.AddToClassList("quest-card-progress-track");

            var questFill = new VisualElement();
            questFill.AddToClassList("quest-card-progress-fill");
            questFill.style.width = Length.Percent(Mathf.RoundToInt(questProgress01 * 100f));
            questFill.style.backgroundColor = new StyleColor(accent);
            questTrack.Add(questFill);
            card.Add(questTrack);

            if (!expanded)
                return card;

            var body = new VisualElement();
            body.AddToClassList("quest-card-body");

            bool showQuestDescription = !string.IsNullOrWhiteSpace(definition.description) &&
                (state.completed || stage == null || string.IsNullOrWhiteSpace(stage.description));

            if (showQuestDescription)
            {
                var description = new Label(definition.description.Trim());
                description.AddToClassList("quest-card-description");
                body.Add(description);
            }

            if (!state.completed && stage != null && !string.IsNullOrWhiteSpace(stage.description))
            {
                var stageDescription = new Label(stage.description.Trim());
                stageDescription.AddToClassList("quest-card-stage-description");
                body.Add(stageDescription);
            }

            var objectiveList = new VisualElement();
            objectiveList.AddToClassList("quest-objective-list");

            int objectiveCount = 0;
            if (state.objectiveStates != null)
            {
                for (int i = 0; i < state.objectiveStates.Count; i++)
                {
                    var objectiveState = state.objectiveStates[i];
                    if (objectiveState == null)
                        continue;

                    var objectiveDef = FindObjectiveDefinition(stage, objectiveState.objectiveId);
                    if (objectiveDef == null)
                        continue;

                    objectiveList.Add(MakeQuestObjectiveRow(objectiveDef, objectiveState, accent));
                    objectiveCount++;
                }
            }

            if (objectiveCount > 0)
            {
                body.Add(objectiveList);
            }
            else if (state.completed)
            {
                var completedSummary = new Label("All objectives completed.");
                completedSummary.AddToClassList("quest-card-stage-description");
                body.Add(completedSummary);
            }

            card.Add(body);
            return card;
        }

        private void ToggleQuestExpanded(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            if (!_expandedQuestIds.Add(questId))
                _expandedQuestIds.Remove(questId);
        }

        private void SetPrimaryQuest(string questId, bool expandCard)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            _primaryQuestId = questId;

            if (expandCard)
                _expandedQuestIds.Add(questId);

            miniHudController?.SetFocusedQuest(questId, expandSummary: true);
            ScrollQuestListToTop();
        }

        private void TogglePrimaryQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            if (string.Equals(_primaryQuestId, questId, StringComparison.OrdinalIgnoreCase))
            {
                ClearPrimaryQuest(questId);
                ScrollQuestListToTop();
                return;
            }

            if (questDirector != null && !questDirector.IsQuestTracked(questId))
                questDirector.SetQuestTracked(questId, true);

            SetPrimaryQuest(questId, expandCard: true);
        }

        private void ClearPrimaryQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) ||
                !string.Equals(_primaryQuestId, questId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _primaryQuestId = null;
        }

        private static string BuildQuestObjectivePrompt(QuestObjectiveDefinition objectiveDefinition)
        {
            if (objectiveDefinition == null)
                return string.Empty;

            string prompt = objectiveDefinition.BuildPromptLabel();
            return string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt.Trim();
        }

        private int CompareQuestStatesForHud(QuestRuntimeState a, QuestRuntimeState b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            bool aPrimary = string.Equals(a.questId, _primaryQuestId, StringComparison.OrdinalIgnoreCase);
            bool bPrimary = string.Equals(b.questId, _primaryQuestId, StringComparison.OrdinalIgnoreCase);
            if (aPrimary != bPrimary)
                return aPrimary ? -1 : 1;

            bool aTracked = questDirector != null && questDirector.IsQuestTracked(a.questId);
            bool bTracked = questDirector != null && questDirector.IsQuestTracked(b.questId);
            if (aTracked != bTracked)
                return aTracked ? -1 : 1;

            var aDef = questDirector != null ? questDirector.GetQuestDefinition(a.questId) : null;
            var bDef = questDirector != null ? questDirector.GetQuestDefinition(b.questId) : null;
            string aTitle = aDef != null && !string.IsNullOrWhiteSpace(aDef.title) ? aDef.title : a.questId;
            string bTitle = bDef != null && !string.IsNullOrWhiteSpace(bDef.title) ? bDef.title : b.questId;
            return string.Compare(aTitle, bTitle, StringComparison.OrdinalIgnoreCase);
        }

        private void ScrollQuestListToTop()
        {
            if (_questList == null)
                return;

            _questList.scrollOffset = Vector2.zero;
            _questList.schedule.Execute(() => _questList.scrollOffset = Vector2.zero);
        }

        private static bool TryResolveNextQuestObjective(
            QuestDefinitionSO definition,
            QuestRuntimeState state,
            out QuestObjectiveDefinition objectiveDefinition,
            out QuestObjectiveRuntimeState objectiveState)
        {
            objectiveDefinition = null;
            objectiveState = null;

            var stage = definition != null ? definition.GetStage(state.currentStageIndex) : null;
            if (stage?.objectives == null || state?.objectiveStates == null)
                return false;

            for (int i = 0; i < state.objectiveStates.Count; i++)
            {
                var candidateState = state.objectiveStates[i];
                if (candidateState == null || candidateState.completed)
                    continue;

                var candidateDefinition = FindObjectiveDefinition(stage, candidateState.objectiveId);
                if (candidateDefinition == null)
                    continue;

                objectiveState = candidateState;
                objectiveDefinition = candidateDefinition;
                return true;
            }

            return false;
        }

        private VisualElement MakeQuestObjectiveRow(QuestObjectiveDefinition objectiveDef, QuestObjectiveRuntimeState objectiveState, Color accent)
        {
            var row = new VisualElement();
            row.AddToClassList("quest-objective-row");
            if (objectiveState.completed)
                row.AddToClassList("is-complete");

            var check = new Label(objectiveState.completed ? "✓" : "•");
            check.AddToClassList("quest-objective-check");
            row.Add(check);

            var copy = new VisualElement();
            copy.AddToClassList("quest-objective-copy");

            var title = new Label(string.IsNullOrWhiteSpace(objectiveDef.title) ? objectiveDef.BuildAuthoringSummary() : objectiveDef.title);
            title.AddToClassList("quest-objective-title");
            copy.Add(title);

            var progressTrack = new VisualElement();
            progressTrack.AddToClassList("quest-objective-progress-track");

            var progressFill = new VisualElement();
            progressFill.AddToClassList("quest-objective-progress-fill");
            progressFill.style.width = Length.Percent(Mathf.RoundToInt(Mathf.Clamp01(objectiveState.progress01) * 100f));
            progressFill.style.backgroundColor = new StyleColor(accent);
            progressTrack.Add(progressFill);
            copy.Add(progressTrack);

            if (!string.IsNullOrWhiteSpace(objectiveState.progressText))
            {
                var progress = new Label(objectiveState.progressText);
                progress.AddToClassList("quest-objective-progress");
                copy.Add(progress);
            }

            row.Add(copy);
            return row;
        }

        private static void SwallowClick(VisualElement element)
        {
            if (element == null)
                return;

            element.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            element.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            element.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
        }

        private static string BuildQuestStageLabel(QuestDefinitionSO definition, QuestRuntimeState state)
        {
            var stage = definition != null ? definition.GetStage(state.currentStageIndex) : null;
            if (stage == null)
                return "Quest in progress";

            string stageTitle = string.IsNullOrWhiteSpace(stage.title) ? $"Stage {state.currentStageIndex + 1}" : stage.title.Trim();
            return stageTitle;
        }

        private static QuestObjectiveDefinition FindObjectiveDefinition(QuestStageDefinition stage, string objectiveId)
        {
            if (stage?.objectives == null || string.IsNullOrWhiteSpace(objectiveId))
                return null;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var objective = stage.objectives[i];
                if (objective != null && string.Equals(objective.id, objectiveId, StringComparison.OrdinalIgnoreCase))
                    return objective;
            }

            return null;
        }

        private void RaiseQuestUiEvent(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (questSignalBus == null)
                questSignalBus = FindObjectOfType<QuestSignalBus>();

            questSignalBus?.RaiseEvent(key);
        }

        private static float GetQuestProgress01(QuestRuntimeState state)
        {
            if (state == null)
                return 0f;

            if (state.completed)
                return 1f;

            if (state.objectiveStates == null || state.objectiveStates.Count == 0)
                return 0f;

            float total = 0f;
            int count = 0;
            for (int i = 0; i < state.objectiveStates.Count; i++)
            {
                var objective = state.objectiveStates[i];
                if (objective == null)
                    continue;

                total += Mathf.Clamp01(objective.progress01);
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        private static string BuildQuestProgressSummary(QuestRuntimeState state)
        {
            if (state == null)
                return "No progress yet";

            if (state.completed)
                return "Quest complete";

            int completed = 0;
            int total = 0;
            if (state.objectiveStates != null)
            {
                for (int i = 0; i < state.objectiveStates.Count; i++)
                {
                    var objective = state.objectiveStates[i];
                    if (objective == null)
                        continue;

                    total++;
                    if (objective.completed)
                        completed++;
                }
            }

            return total > 0
                ? $"{completed} of {total} objectives completed"
                : "No active objectives";
        }

        private VisualElement MakeTaskCard(string title, ProgressionDirector.DailyTierDisplay tier)
        {
            string statusText;
            if (tier.completed && !tier.claimed)
                statusText = "Ready to claim";
            else if (tier.claimed)
                statusText = "Claimed";
            else
                statusText = $"{Mathf.RoundToInt(tier.progress):0}/{Mathf.RoundToInt(tier.target):0}";

            var card = new VisualElement();
            card.AddToClassList("task-card");
            if (tier.completed && !tier.claimed)
                card.AddToClassList("is-actionable");
            if (tier.claimed)
                card.AddToClassList("is-claimed");

            var topRow = new VisualElement();
            topRow.AddToClassList("task-card-toprow");

            var titleEl = new Label(title);
            titleEl.AddToClassList("task-card-title");
            topRow.Add(titleEl);

            var tierEl = new Label($"Tier {tier.tierIndex + 1}");
            tierEl.AddToClassList("task-card-tier");
            topRow.Add(tierEl);

            card.Add(topRow);

            var statusEl = new Label(statusText);
            statusEl.AddToClassList("task-card-status");
            card.Add(statusEl);

            var track = new VisualElement();
            track.AddToClassList("task-progress-track");

            var fill = new VisualElement();
            fill.AddToClassList("task-progress-fill");
            fill.style.width = Length.Percent(Mathf.RoundToInt(tier.pct01 * 100f));
            track.Add(fill);

            card.Add(track);

            var bottomRow = new VisualElement();
            bottomRow.AddToClassList("task-card-bottomrow");

            var rewardEl = new Label($"+${tier.reward}");
            rewardEl.AddToClassList("task-card-reward");
            bottomRow.Add(rewardEl);

            var actionButton = new Button(() =>
            {
                if (!tier.completed || tier.claimed)
                    return;

                if (progressionDirector.TryClaimDailyTier(
                    tier.ladderId,
                    tier.tierIndex,
                    out int rewardGranted,
                    out bool rowBonusGranted,
                    out int rowBonusAmount))
                {
                    if (statsManager != null)
                        statsManager.Save();

                    questSignalBus?.RaiseEvent(
                        "ui.tasks.claimed",
                        QuestSignalData.Create()
                            .WithTag("ladderId", tier.ladderId ?? string.Empty)
                            .WithTag("tierIndex", tier.tierIndex.ToString()));

                    RefreshGoalsPanel();
                    RefreshPassPanel();
                }
            });

            actionButton.AddToClassList("task-action-button");
            actionButton.text = tier.claimed ? "Claimed" : (tier.completed ? "Claim" : "In Progress"); actionButton.SetEnabled(tier.completed && !tier.claimed);

            bottomRow.Add(actionButton);
            card.Add(bottomRow);

            return card;
        }

        private static int CountValidStrings(params string[] values)
        {
            int count = 0;
            if (values == null)
                return 0;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    count++;
            }

            return count;
        }

        private static AchievementDefinitionSO.MountainHudCategory ToDefinitionHudCategory(AchievementCategory category)
        {
            return category switch
            {
                AchievementCategory.Exploration => AchievementDefinitionSO.MountainHudCategory.Exploration,
                AchievementCategory.MountainMastery => AchievementDefinitionSO.MountainHudCategory.MountainMastery,
                _ => AchievementDefinitionSO.MountainHudCategory.Performance
            };
        }

        private VisualElement MakeListRow(string title, string value, bool clickable, Action onClick)
        {
            var btn = new Button(() => onClick?.Invoke());
            btn.AddToClassList("list-row");
            if (clickable) btn.AddToClassList("is-actionable");

            var titleLbl = new Label(title);
            titleLbl.AddToClassList("list-row-title");
            btn.Add(titleLbl);

            var valueLbl = new Label(value);
            valueLbl.AddToClassList("list-row-value");
            btn.Add(valueLbl);

            if (!clickable)
                btn.SetEnabled(false);

            return btn;
        }

        private static void SetToggleState(Button btn, bool selected)
        {
            if (btn == null) return;
            btn.EnableInClassList("is-selected", selected);
        }

        private string BuildLocationLine()
        {
            if (!string.IsNullOrWhiteSpace(_selectedTitle))
                return _selectedTitle;

            if (runProgressTracker != null && runProgressTracker.TryGetActiveProgress(out var active))
                return $"On {active.runName}";

            return "Mountain Overview";
        }

        private string BuildHintLine()
        {
            var profile = Profile;
            if (profile == null)
                return "No profile loaded";

            int completed = profile.lifetime.totalRunsCompleted;
            int discovered = profile.visitedRunIds != null ? profile.visitedRunIds.Count : 0;
            int pois = profile.visitedLandmarkIds != null ? profile.visitedLandmarkIds.Count : 0;
            return $"Runs {completed} complete • {discovered} discovered • {pois} landmarks";
        }

        private static string FormatRunDifficulty(SkiRunDifficulty difficulty)
        {
            string raw = difficulty.ToString();
            if (string.IsNullOrWhiteSpace(raw))
                return "--";

            var sb = new System.Text.StringBuilder(raw.Length + 8);
            sb.Append(raw[0]);

            for (int i = 1; i < raw.Length; i++)
            {
                char c = raw[i];
                char prev = raw[i - 1];

                if (char.IsUpper(c) && !char.IsWhiteSpace(prev))
                    sb.Append(' ');

                sb.Append(c);
            }

            return sb.ToString();
        }

        private string FormatTime()
        {
            if (timeController == null)
                return "--:--";

            return $"{Mathf.Clamp(timeController.timeHours, 0, 23):00}:{Mathf.Clamp((int)timeController.timeMinutes, 0, 59):00}";
        }

        private string FormatWeather()
        {
            if (weatherController == null || weatherController.currentWeatherPreset == null)
                return "Weather unavailable";

            string cond = weatherController.currentWeatherPreset.weatherCondition;
            return string.IsNullOrWhiteSpace(cond) ? "Weather" : cond;
        }

        private static string FormatSeconds(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int total = Mathf.RoundToInt(seconds);
            int mins = total / 60;
            int secs = total % 60;
            return $"{mins:00}:{secs:00}";
        }

        private static string FormatDurationLong(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int total = Mathf.RoundToInt(seconds);
            int hours = total / 3600;
            int mins = (total % 3600) / 60;
            int secs = total % 60;

            return hours > 0
                ? $"{hours}h {mins:00}m {secs:00}s"
                : $"{mins}m {secs:00}s";
        }

        private static string FormatClockTime(float gameTimeOfDay)
        {
            float wrapped = gameTimeOfDay;
            while (wrapped < 0f) wrapped += 24f;
            while (wrapped >= 24f) wrapped -= 24f;

            int hours = Mathf.FloorToInt(wrapped);
            int minutes = Mathf.Clamp(Mathf.RoundToInt((wrapped - hours) * 60f), 0, 59);
            return $"{hours:00}:{minutes:00}";
        }

        private static float GetAttemptCoveredFraction01(RunAttemptEntry attempt)
        {
            if (attempt == null)
                return 0f;

            if (attempt.isCompletion)
                return 1f;

            float covered = attempt.coveredFraction01;
            if (covered <= 0f)
                covered = Mathf.Abs(attempt.exitFraction01 - attempt.entryFraction01);

            return Mathf.Clamp01(covered);
        }

        private static string BuildRunHistorySecondaryLine(RunAttemptEntry attempt)
        {
            if (attempt == null)
                return string.Empty;

            string speed = attempt.topSpeedMps > 0f ? $"Top speed {FormatSpeed(attempt.topSpeedMps)}" : null;
            string stacks = attempt.stacks > 0 ? $"{attempt.stacks} stack{(attempt.stacks == 1 ? "" : "s")}" : null;

            if (!string.IsNullOrWhiteSpace(speed) && !string.IsNullOrWhiteSpace(stacks))
                return $"{speed} • {stacks}";

            return speed ?? stacks ?? string.Empty;
        }

        private static int CompareRunHistoryEntries(RunAttemptEntry a, RunAttemptEntry b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int year = b.gameYear.CompareTo(a.gameYear);
            if (year != 0) return year;

            int month = b.gameMonthIndex.CompareTo(a.gameMonthIndex);
            if (month != 0) return month;

            int day = b.gameDayOfMonth.CompareTo(a.gameDayOfMonth);
            if (day != 0) return day;

            return b.gameTimeOfDay.CompareTo(a.gameTimeOfDay);
        }

        private static string BuildRunHistoryDayKey(RunAttemptEntry attempt)
        {
            if (attempt == null)
                return "Unknown day";

            if (attempt.gameDayOfMonth <= 0)
                return $"Day {Mathf.Max(1, attempt.gameDayOfWeek + 1)}";

            return $"Day {attempt.gameDayOfMonth} • Month {attempt.gameMonthIndex + 1} • Year {Mathf.Max(1, attempt.gameYear)}";
        }

        private static string FormatMeters(float meters)
        {
            if (meters >= 1000f)
                return $"{meters / 1000f:0.0} km";
            return $"{meters:0} m";
        }

        private static string FormatSpeed(float mps)
        {
            return $"{(mps * 3.6f):0} km/h";
        }

        private static string FormatRunDistanceLine(string name, float sqrDist)
        {
            if (string.IsNullOrWhiteSpace(name) || sqrDist == float.MaxValue)
                return "--";

            return $"{name} • {Mathf.Sqrt(sqrDist):0} m";
        }

        private string ResolveRunDisplayName(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
                return "Run";

            if (PointOfInterestRegistry.Instance != null &&
                PointOfInterestRegistry.Instance.TryGetById(runId, out var poi) &&
                !string.IsNullOrWhiteSpace(poi.displayName))
            {
                return poi.displayName;
            }

            if (mapData != null && mapData.Polylines != null)
            {
                for (int i = 0; i < mapData.Polylines.Count; i++)
                {
                    var poly = mapData.Polylines[i];
                    if (poly.IsValid && string.Equals(poly.id, runId, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(poly.displayName))
                        return poly.displayName;
                }
            }

            return runId;
        }

        private static LiftLine FindLiftById(string liftId)
        {
            if (string.IsNullOrWhiteSpace(liftId)) return null;

            var lifts = FindObjectsOfType<LiftLine>();
            for (int i = 0; i < lifts.Length; i++)
            {
                var lift = lifts[i];
                if (lift == null) continue;
                if (string.Equals(lift.gameObject.name, liftId, StringComparison.Ordinal))
                    return lift;
            }

            return null;
        }

        private static MapUIStyleSettings AutoFindMapStyle()
        {
            var all = Resources.FindObjectsOfTypeAll<MapUIStyleSettings>();
            if (all == null || all.Length == 0)
                return null;

            MapUIStyleSettings fallback = null;

            for (int i = 0; i < all.Length; i++)
            {
                var style = all[i];
                if (style == null) continue;

                if (fallback == null)
                    fallback = style;

                if (string.Equals(style.name, "MapUIStyleSettings", StringComparison.OrdinalIgnoreCase))
                    return style;
            }

            return fallback;
        }

        private static MapData AutoFindMapData()
        {
            var all = Resources.FindObjectsOfTypeAll<MapData>();
            if (all == null || all.Length == 0)
                return null;

            MapData fallback = null;

            for (int i = 0; i < all.Length; i++)
            {
                var data = all[i];
                if (data == null) continue;

                if (fallback == null)
                    fallback = data;

                if (string.Equals(data.name, "MapData", StringComparison.OrdinalIgnoreCase))
                    return data;
            }

            return fallback;
        }

        private MapRegionSet ResolveRegionSet()
        {
            if (regionSet != null)
            {
                if (regionSet.MapData == null && mapData != null)
                    regionSet.SetMapData(mapData);

                return regionSet;
            }

            var tracker = FindObjectOfType<PlayerMapRegionTracker>();
            if (tracker != null)
            {
                // We only need the region asset reference path here, not the current face.
            }

            var all = Resources.FindObjectsOfTypeAll<MapRegionSet>();
            if (all == null || all.Length == 0)
                return null;

            for (int i = 0; i < all.Length; i++)
            {
                var set = all[i];
                if (set == null)
                    continue;

                if (mapData == null || set.MapData == mapData)
                {
                    if (set.MapData == null && mapData != null)
                        set.SetMapData(mapData);

                    return set;
                }
            }

            return all[0];
        }

        private static Camera AutoFindMapReferenceCamera()
        {
            var cameras = Resources.FindObjectsOfTypeAll<Camera>();
            if (cameras == null || cameras.Length == 0)
                return null;

            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null) continue;
                if (!cam.gameObject.scene.IsValid()) continue;

                string n = cam.name ?? string.Empty;
                if (n.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return cam;
                }
            }

            // No trustworthy map reference camera found.
            // Return null so PhoneMapPageUI uses MapProjection instead of a wrong camera.
            return null;
        }

        private bool TryFindRaceByPolylineId(string polylineId, out RaceCourseLine race)
        {
            race = null;
            if (string.IsNullOrWhiteSpace(polylineId))
                return false;

            var races = FindObjectsOfType<RaceCourseLine>(includeInactive: false);
            for (int i = 0; i < races.Length; i++)
            {
                var candidate = races[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.RaceId, polylineId, StringComparison.Ordinal) ||
                    string.Equals(candidate.RaceName, polylineId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.gameObject.name, polylineId, StringComparison.Ordinal))
                {
                    race = candidate;
                    return true;
                }

                if (TryGetRacePolylineId(candidate, out string racePolylineId) &&
                    string.Equals(racePolylineId, polylineId, StringComparison.Ordinal))
                {
                    race = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetRacePolylineId(RaceCourseLine race, out string polylineId)
        {
            polylineId = null;

            if (race == null || mapData == null || mapData.Polylines == null)
                return false;

            string raceId = race.RaceId;
            string raceName = race.RaceName;
            string objectName = race.gameObject.name;

            for (int i = 0; i < mapData.Polylines.Count; i++)
            {
                var poly = mapData.Polylines[i];
                if (!poly.IsValid)
                    continue;

                if ((!string.IsNullOrWhiteSpace(raceId) && string.Equals(poly.id, raceId, StringComparison.Ordinal)) ||
                    string.Equals(poly.id, objectName, StringComparison.Ordinal) ||
                    string.Equals(poly.id, raceName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(poly.displayName, raceName, StringComparison.OrdinalIgnoreCase))
                {
                    polylineId = poly.id;
                    return true;
                }
            }

            return false;
        }

        private string BuildSelectedRaceBody(RaceCourseLine race)
        {
            if (race == null)
                return "Race unavailable.";

            int selectedLeague = Mathf.Max(1, race.SelectedLeagueNumber);
            bool unlocked = race.IsLeagueUnlocked(selectedLeague);
            bool completed = race.HasCompletedLeague(selectedLeague);
            int bestPlacement = race.GetBestPlacement(selectedLeague);
            float bestTime = race.GetBestTimeSeconds(selectedLeague);
            var league = race.GetLeague(selectedLeague);

            string description = league != null && !string.IsNullOrWhiteSpace(league.description)
                ? league.description
                : "Race the course against the current league field.";

            string lockText = unlocked
                ? (completed ? "Completed" : "Unlocked")
                : $"Locked - win {race.GetLeagueDisplayName(race.GetPreviousLeagueNumber(selectedLeague))}";

            string bestPlacementText = bestPlacement > 0 ? $"{bestPlacement} place" : "--";
            string bestTimeText = bestTime >= 0f ? $"{bestTime:0.00}s" : "--";

            return
                $"{race.RaceName}\n" +
                $"{race.GetLeagueDisplayName(selectedLeague)} • {lockText}\n" +
                $"{description}\n" +
                $"Best Placement: {bestPlacementText}\n" +
                $"Best Time: {bestTimeText}\n" +
                $"Reward: ${race.CompletionReward}";
        }
    }
}
