using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SkiGame.UI;
using SkiGame.Map;
using SkiGame.Map.UI;
using SkiGame.POI;
using SkiGame.Runs;
using TimeWeather;

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
        private Label _lblTopHint;

        // Context panel
        private Label _lblContextTitle;
        private Label _lblContextBody;
        private VisualElement _contextMetaRow;
        private VisualElement _contextHighlights;
        private Button _btnContextExpand;
        private ScrollView _contextAttemptsList;

        // Stats
        private Button _btnStatsToday;
        private Button _btnStatsLifetime;
        private Label _lblStatsModeSummary;
        private VisualElement _statsGrid;
        private Label _lblStatsBody;
        private bool _statsShowLifetime;

        // Goals
        private Button _btnGoalsTasks;
        private Button _btnGoalsAchievements;
        private ScrollView _goalsList;
        private VisualElement _achievementCategoryRow;
        private VisualElement _achievementGrid;
        private bool _goalsShowAchievements;
        private AchievementCategory _achievementCategory = AchievementCategory.Performance;

        private string _tutorialLastViewedSection = "Stats";
        private bool _tutorialVisitedStats;
        private bool _tutorialVisitedTasks;
        private bool _tutorialVisitedMap;

        private ScrollView _achievementScroll;
        private bool _contextHistoryExpanded;
        private bool _contextHistoryAvailable;

        // Pass
        private Button _btnPassCurrent;
        private Label _lblPassCurrent;
        private VisualElement _passLevelsList;
        private VisualElement _passPurchaseList;
        private int _previewPassLevel = -1;

        private float _nextRefreshTime;
        private bool _pendingInitializeAfterPreview;

        private enum SelectedMapKind
        {
            None = 0,
            Run = 10,
            Lift = 20,
            POI = 30,
        }

        private enum AchievementCategory
        {
            Performance = 0,
            Exploration = 10,
            MountainMastery = 20,
        }

        private SelectedMapKind _selectedKind;
        private string _selectedId;
        private string _selectedTitle;
        private string _selectedBody;

        private readonly List<ProgressionDirector.DailyTierDisplay> _dailyTierBuffer = new();
        private readonly List<AchievementDefinitionSO> _achievementBuffer = new();
        private readonly HashSet<string> _achievementDedup = new();
        private readonly List<AchievementDefinitionSO> _achievementQueryBuffer = new();

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

            BindUI();
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

            BindUI();
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

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            RefreshAll(force: false);
        }

        private void OnDisable()
        {
            UnhookInput();
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
        }

        public bool IsOpen => _isOpen;

        public string TutorialLastViewedSection => _tutorialLastViewedSection;
        public bool TutorialVisitedStats => _tutorialVisitedStats;
        public bool TutorialVisitedTasks => _tutorialVisitedTasks;
        public bool TutorialVisitedMap => _tutorialVisitedMap;

        public void ResetTutorialVisitedSections()
        {
            _tutorialVisitedStats = false;
            _tutorialVisitedTasks = false;
            _tutorialVisitedMap = false;
            _tutorialLastViewedSection = "Stats";
        }

        public void ToggleOverlay()
        {
            SetOverlayOpen(!_isOpen);
        }

        public void SetOverlayOpen(bool open, bool refreshNow = true)
        {
            _isOpen = open;

            if (_isOpen)
            {
                _tutorialLastViewedSection = "Stats";
                _tutorialVisitedStats = true;
            }

            if (_isOpen)
                MarkMapVisitedForTutorial();

            if (_root != null)
            {
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                _root.style.visibility = open ? Visibility.Visible : Visibility.Hidden;
                _root.style.opacity = open ? 1f : 0f;
                _root.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }

            ApplyCursorState(open);
            ApplyCameraLockState(open);

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
                _mapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.0f);
            }).ExecuteLater(0);

            _root.schedule.Execute(() =>
            {
                _mapUI.Refresh();
                _mapUI.RequestCenterOnPlayer(keepZoom: false, minZoom: 1.0f);
            }).ExecuteLater(40);
        }

        private void BindUI()
        {
            _lblTopTime = _root.Q<Label>("Lbl_TopTime");
            _lblTopWeather = _root.Q<Label>("Lbl_TopWeather");
            _lblTopLocation = _root.Q<Label>("Lbl_TopLocation");
            _lblTopHint = _root.Q<Label>("Lbl_TopHint");
            _btnCloseOverlay = _root.Q<Button>("Btn_CloseOverlay");

            _lblContextTitle = _root.Q<Label>("Lbl_ContextTitle");
            _lblContextBody = _root.Q<Label>("Lbl_ContextBody");
            _contextMetaRow = _root.Q<VisualElement>("ContextMetaRow");
            _contextHighlights = _root.Q<VisualElement>("ContextHighlights");
            _btnContextExpand = _root.Q<Button>("Btn_ContextExpand");
            _contextAttemptsList = _root.Q<ScrollView>("ContextAttemptsList");

            _btnStatsToday = _root.Q<Button>("Btn_StatsToday");
            _btnStatsLifetime = _root.Q<Button>("Btn_StatsLifetime");
            _lblStatsModeSummary = _root.Q<Label>("Lbl_StatsModeSummary");
            _statsGrid = _root.Q<VisualElement>("StatsGrid");
            _lblStatsBody = _root.Q<Label>("Lbl_StatsBody");

            _btnGoalsTasks = _root.Q<Button>("Btn_GoalsTasks");
            _btnGoalsAchievements = _root.Q<Button>("Btn_GoalsAchievements");
            _goalsList = _root.Q<ScrollView>("GoalsList");
            _achievementCategoryRow = _root.Q<VisualElement>("AchievementCategoryRow");
            _achievementScroll = _root.Q<ScrollView>("AchievementScroll");
            _achievementGrid = _root.Q<VisualElement>("AchievementGrid");

            if (_achievementScroll != null)
            {
                _achievementScroll.mode = ScrollViewMode.Vertical;
                _achievementScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _achievementScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }

            _btnPassCurrent = _root.Q<Button>("Btn_PassCurrent");
            _lblPassCurrent = _root.Q<Label>("Lbl_PassCurrent");
            _passLevelsList = _root.Q<VisualElement>("PassLevelsList");
            _passPurchaseList = _root.Q<VisualElement>("PassPurchaseList");

            if (_btnContextExpand != null)
                _btnContextExpand.clicked += ToggleContextExpand;

            if (_btnCloseOverlay != null)
                _btnCloseOverlay.clicked += () => SetOverlayOpen(false);

            if (_btnStatsToday != null)
                _btnStatsToday.clicked += () => { _statsShowLifetime = false; RefreshStatsPanel(); };

            if (_btnStatsLifetime != null)
                _btnStatsLifetime.clicked += () => { _statsShowLifetime = true; RefreshStatsPanel(); };

            _btnGoalsTasks.clicked += () =>
            {
                _goalsShowAchievements = false;
                _tutorialLastViewedSection = "Tasks";
                _tutorialVisitedTasks = true;
                RefreshGoalsPanel();
            };

            _btnGoalsAchievements.clicked += () =>
            {
                _goalsShowAchievements = true;
                _tutorialLastViewedSection = "Achievements";
                RefreshGoalsPanel();
            };

            if (_btnPassCurrent != null)
                _btnPassCurrent.clicked += () =>
                {
                    int level = skiPassManager != null ? skiPassManager.CurrentLevel : 0;
                    SetPassPreview(level);
                };

            BuildAchievementCategoryButtons();

            if (_contextAttemptsList != null)
                _contextAttemptsList.style.display = DisplayStyle.None;

            _contextHistoryExpanded = false;
            _contextHistoryAvailable = false;
            ApplyContextHistoryVisibility();
        }

        private void BindMap()
        {
            if (mapData == null)
            {
                Debug.LogWarning("[MountainHudOverlayController] Missing MapData.");
                return;
            }

            var overlayRoot = _root.Q<VisualElement>("OverlayRoot") ?? _root;
            var host = overlayRoot.Q<VisualElement>("MapHost");

            if (host == null)
            {
                Debug.LogWarning("[MountainHudOverlayController] MapHost placeholder not found.");
                return;
            }

            host.Clear();

            // Respect the authored UXML placement so the map stays centered under the overlay,
            // rather than stretching edge-to-edge behind the side rails.
            host.style.position = Position.Absolute;
            host.pickingMode = PickingMode.Position;

            VisualElement runtimeMapRoot = MountainHudMapRootFactory.BuildOverlayMapRoot("OverlayRuntimeMapRoot");
            host.Add(runtimeMapRoot);

            // Critical: the host must be behind all overlay panels.
            host.SendToBack();

            var topStrip = overlayRoot.Q<VisualElement>("TopStrip");
            var leftRail = overlayRoot.Q<VisualElement>("LeftRail");
            var rightRail = overlayRoot.Q<VisualElement>("RightRail");

            topStrip?.BringToFront();
            leftRail?.BringToFront();
            rightRail?.BringToFront();

            _mapUI = new PhoneMapPageUI();
            _mapUI.Bind(runtimeMapRoot, mapData, mapReferenceCamera);
            _mapUI.MarkerSelected += OnMapMarkerSelected;
            _mapUI.PolylineSelected += OnMapPolylineSelected;
            _mapUI.SelectionCleared += OnMapSelectionCleared;
            _mapUI.SetMinimapMode(false, followPlayer: false, lockPan: false, allowZoom: true, suppressSelection: false, hideMarkerLabels: false);

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

            if (_lblTopLocation != null)
                _lblTopLocation.text = BuildLocationLine();

            if (_lblTopHint != null)
                _lblTopHint.text = BuildHintLine();
        }

        private void RefreshContextPanel()
        {
            if (_lblContextTitle == null || _lblContextBody == null)
                return;

            _contextMetaRow?.Clear();
            _contextHighlights?.Clear();

            if (!string.IsNullOrWhiteSpace(_selectedId))
            {
                _lblContextTitle.text = _selectedTitle;
                _lblContextBody.text = _selectedBody;

                PopulateSelectedContextMeta();
                RefreshContextAttemptsForSelection();
                return;
            }

            if (runProgressTracker != null && runProgressTracker.TryGetActiveProgress(out var active))
            {
                _lblContextTitle.text = active.runName;
                _lblContextBody.text =
                    "Current run in progress. Stay clean and keep your speed through the line.";

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

                    _contextHighlights.Add(MakeContextHighlightCard(
                        "Clean Finish Goal",
                        active.stacks <= 0 ? "Still clean" : $"{active.stacks} stack{(active.stacks == 1 ? "" : "s")}",
                        active.stacks <= 0
                            ? "You are on track for a clean completion."
                            : "Avoid further falls to preserve the attempt."));
                }

                RefreshContextAttemptsForActiveRun(active.runId);
                return;
            }

            BuildNearbyRunsContext();
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
                        AddContextMetaChip("Type", "Run", accent: true);

                        bool visited = !string.IsNullOrWhiteSpace(_selectedId) && profile.HasVisitedRun(_selectedId);
                        AddContextMetaChip("Visited", visited ? "Yes" : "No");

                        if (!string.IsNullOrWhiteSpace(_selectedId) && profile.TryGetRunRecord(_selectedId, out var record) && record != null)
                        {
                            int attempts = record.attempts != null ? record.attempts.Count : 0;
                            AddContextMetaChip("Attempts", attempts.ToString());
                            AddContextMetaChip("Best Speed", FormatSpeed(record.bestTopSpeedMps));
                        }
                        break;
                    }

                case SelectedMapKind.Lift:
                    {
                        AddContextMetaChip("Type", "Lift", accent: true);

                        int rides = !string.IsNullOrWhiteSpace(_selectedId)
                            ? profile.GetLiftRideCount(_selectedId, session: false)
                            : 0;

                        AddContextMetaChip("Rides", rides.ToString());
                        break;
                    }

                case SelectedMapKind.POI:
                    {
                        AddContextMetaChip("Type", "Landmark", accent: true);

                        bool visited = !string.IsNullOrWhiteSpace(_selectedId) && profile.HasVisitedLandmark(_selectedId);
                        AddContextMetaChip("Visited", visited ? "Yes" : "No");
                        break;
                    }
            }
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
            SetToggleState(_btnGoalsTasks, !_goalsShowAchievements);
            SetToggleState(_btnGoalsAchievements, _goalsShowAchievements);

            if (_goalsList == null || _achievementCategoryRow == null || _achievementGrid == null)
                return;

            if (_goalsList != null)
                _goalsList.style.display = _goalsShowAchievements ? DisplayStyle.None : DisplayStyle.Flex;

            if (_achievementCategoryRow != null)
                _achievementCategoryRow.style.display = _goalsShowAchievements ? DisplayStyle.Flex : DisplayStyle.None;

            if (_achievementScroll != null)
                _achievementScroll.style.display = _goalsShowAchievements ? DisplayStyle.Flex : DisplayStyle.None;
            else if (_achievementGrid != null)
                _achievementGrid.style.display = _goalsShowAchievements ? DisplayStyle.Flex : DisplayStyle.None;

            if (_goalsShowAchievements)
            {
                BuildAchievementCategoryButtons();
                RefreshAchievementsView();
            }
            else
            {
                RefreshTasksView();
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

        private void RefreshAchievementsView()
        {
            if (_achievementGrid == null)
                return;

            _achievementGrid.Clear();
            _achievementBuffer.Clear();

            AppendAchievementsForCategory(_achievementCategory, _achievementBuffer);

            if (_achievementBuffer.Count == 0)
            {
                var empty = new Label("No authored achievements match this tab yet.");
                empty.AddToClassList("achievement-empty-state");
                _achievementGrid.Add(empty);
                return;
            }

            var profile = Profile;

            for (int i = 0; i < _achievementBuffer.Count; i++)
            {
                var def = _achievementBuffer[i];
                if (def == null)
                    continue;

                bool unlocked = profile != null && profile.HasAchievement(def.id);
                float progress01 = profile != null ? def.GetProgress01(profile) : 0f;
                string progressText = profile != null
                    ? def.GetProgressText(profile)
                    : $"0/{Mathf.Max(1f, def.target):0}";
                string requirementText = def.GetRequirementText();

                string status = unlocked ? "Completed" : "In Progress";
                if (unlocked && profile != null && profile.TryGetAchievementUnlockedUtc(def.id, out var dt))
                    status = $"Completed {dt.ToDateTimeUtc():dd MMM yyyy}";

                var card = new VisualElement();
                card.AddToClassList("achievement-card");

                if (unlocked)
                    card.AddToClassList("is-complete");

                var topRow = new VisualElement();
                topRow.AddToClassList("achievement-card-toprow");

                var icon = new Label(def.GetMountainHudGlyph());
                icon.AddToClassList("achievement-card-icon");
                topRow.Add(icon);

                var state = new Label(status);
                state.AddToClassList("achievement-card-state");
                if (unlocked)
                    state.AddToClassList("is-complete");
                topRow.Add(state);

                card.Add(topRow);

                var title = new Label(string.IsNullOrWhiteSpace(def.title) ? def.id : def.title);
                title.AddToClassList("achievement-card-title");
                card.Add(title);

                if (!string.IsNullOrWhiteSpace(def.description))
                {
                    var description = new Label(def.description);
                    description.AddToClassList("achievement-card-description");
                    card.Add(description);
                }

                var requirement = new Label(requirementText);
                requirement.AddToClassList("achievement-card-requirement");
                card.Add(requirement);

                var progressTrack = new VisualElement();
                progressTrack.AddToClassList("achievement-progress-track");

                var progressFill = new VisualElement();
                progressFill.AddToClassList("achievement-progress-fill");
                progressFill.style.width = Length.Percent(Mathf.RoundToInt(progress01 * 100f));
                progressTrack.Add(progressFill);

                card.Add(progressTrack);

                var progressLabel = new Label(unlocked ? "Complete" : progressText);
                progressLabel.AddToClassList("achievement-card-progress");
                card.Add(progressLabel);

                _achievementGrid.Add(card);
            }
        }

        private void RefreshPassPanel()
        {
            if (_lblPassCurrent == null || _passLevelsList == null || _passPurchaseList == null)
                return;

            if (skiPassManager == null || skiPassManager.Config == null)
            {
                _lblPassCurrent.text = "No ski pass system found.";
                return;
            }

            _lblPassCurrent.text = $"{skiPassManager.GetCurrentPassDisplayName()} • {skiPassManager.GetRemainingTimeString()}";

            _passLevelsList.Clear();
            _passPurchaseList.Clear();

            var cfg = skiPassManager.Config;
            if (_previewPassLevel < 0)
                _previewPassLevel = skiPassManager.CurrentLevel;

            for (int i = 0; i < cfg.levels.Length; i++)
            {
                var level = cfg.levels[i];
                if (level == null) continue;

                int levelIndex = level.levelIndex;

                var btn = new Button(() =>
                {
                    SetPassPreview(levelIndex);
                })
                {
                    text = $"{level.displayName} (L{levelIndex})"
                };

                btn.AddToClassList("pass-level-button");
                if (levelIndex == _previewPassLevel)
                    btn.AddToClassList("is-selected");

                _passLevelsList.Add(btn);
            }

            for (int d = 0; d < cfg.durations.Length; d++)
            {
                var dur = cfg.durations[d];
                if (dur == null) continue;

                int durationIndex = d;

                if (!skiPassManager.TryQuotePurchase(_previewPassLevel, durationIndex, out var quote, out string reason))
                {
                    var locked = new Label(reason);
                    locked.AddToClassList("pass-quote-label");
                    _passPurchaseList.Add(locked);
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("pass-purchase-row");

                var label = new Label($"{dur.label}\n{quote.finalCost}");
                label.AddToClassList("pass-quote-label");
                row.Add(label);

                var buyBtn = new Button(() =>
                {
                    bool ok = skiPassManager.TryPurchase(
                        _previewPassLevel,
                        durationIndex,
                        TrySpendCurrency,
                        out var purchasedQuote,
                        out string failReason);

                    if (ok)
                    {
                        if (statsManager != null)
                            statsManager.Save();

                        SetPassPreview(skiPassManager.CurrentLevel);
                        RefreshPassPanel();
                    }
                    else
                    {
                        Debug.LogWarning($"[MountainHudOverlay] Pass purchase failed: {failReason}");
                    }
                })
                {
                    text = quote.isUpgrade ? "Upgrade" : (quote.isExtend ? "Extend" : "Buy")
                };

                buyBtn.AddToClassList("pass-buy-button");
                row.Add(buyBtn);
                _passPurchaseList.Add(row);
            }
        }

        private bool TrySpendCurrency(int amount)
        {
            var mgr = statsManager != null ? statsManager : PlayerStatsManager.Instance;
            if (mgr == null || mgr.Profile == null) return false;
            if (amount < 0) return false;
            if (mgr.Profile.currency < amount) return false;

            mgr.Profile.currency -= amount;
            mgr.Save();
            return true;
        }

        private void SetPassPreview(int passLevel)
        {
            _previewPassLevel = Mathf.Max(0, passLevel);
            _mapUI?.PreviewLiftAccessForPassLevel(_previewPassLevel);
            RefreshPassPanel();
        }

        private void OnMapPolylineSelected(MapPolyline poly)
        {
            if (!poly.IsValid)
                return;

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
                _lblContextBody.text = "Explore the map.";
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
            _lblContextBody.text = "Choose a nearby line to start a new descent or continue exploring the mountain.";

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

        private void BuildAchievementCategoryButtons()
        {
            if (_achievementCategoryRow == null)
                return;

            _achievementCategoryRow.Clear();
            AddAchievementCategoryButton("Performance", AchievementCategory.Performance);
            AddAchievementCategoryButton("Exploration", AchievementCategory.Exploration);
            AddAchievementCategoryButton("Mastery", AchievementCategory.MountainMastery);
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
            if (accent)
                chip.AddToClassList("is-accent");

            var labelEl = new Label(label);
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

            var titleEl = new Label(title);
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

            var rewardEl = new Label($"+{tier.reward} currency");
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
    }
}