using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static SkiGame.Progression.ProgressionDirector;
using SkiGame.Map;
using SkiGame.Map.UI;
using SkiGame.Runs;
using SkiGame.POI;
using TimeWeather;

namespace SkiGame.Progression
{
    public class PhoneHUDController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;

        [Header("Input (New Input System)")]
        [Tooltip("Bind this to your Tab action (button). Tap cycles watch screens; hold opens/closes the phone.")]
        [SerializeField] private InputActionReference tabAction;

        [Tooltip("Optional: bind to mouse scroll or a UI scroll action (Vector2). Only Y is used.")]
        [SerializeField] private InputActionReference scrollAction;

        [Header("Interaction Tuning")]
        [SerializeField, Range(0.15f, 0.8f)]
        private float holdToOpenSeconds = 0.35f;

        [SerializeField, Range(10f, 1200f)]
        private float watchScrollSpeed = 260f;

        [Header("Start State")]
        [SerializeField] private bool startPhoneOpen = false;

        [Header("Map")]
        [SerializeField] private MapData mapData;
        [SerializeField] private Camera mapReferenceCamera;
        [SerializeField] private MapUIStyleSettings mapStyle;

        private PhoneMapPageUI _mapPageUI;

        private VisualElement _mapInfoActionsRow;
        private Button _mapInfoViewRunBtn;

        // Embedded minimaps
        private PhoneMapPageUI _watchMiniMapUI;
        private PhoneMapPageUI _homeTileMiniMapUI;

        // Map info panel (bottom overlay on map page)
        private VisualElement _mapInfoPanel;
        private Label _mapInfoTitle;
        private Label _mapInfoBody;
        private Button _mapInfoCloseBtn;

        // Run attempt viewer (shown only for run selections)
        private VisualElement _mapAttemptBox;
        private Button _mapAttemptPrevBtn;
        private Button _mapAttemptNextBtn;
        private Label _mapAttemptHeader;
        private Label _mapAttemptDetails;

        private VisualElement _mapInfoRunNavRow;

        private string _mapInfoActiveRunId;
        private int _mapInfoActiveAttemptIndex = -1;

        private RunRecordEntry _activeRunRecord;
        private int _activeAttemptIndex = -1;

        private VisualElement _mapBottomDock;

        enum MapEntityType { Run, Lift, POI, Unknown }

        struct MapSelection
        {
            public MapEntityType Type;
            public string Id;          // runId / liftId / poiId (string IDs are fine)
            public string DisplayName; // fallback if no metadata
        }

        private VisualElement _root;

        // Watch
        private VisualElement _watchCollapsed;
        private Label _watchTitle;
        private VisualElement _watchDots;
        private readonly List<VisualElement> _dotElems = new();
        private VisualElement _watchPagesRoot;
        private readonly List<ScrollView> _watchPages = new();
        private ScrollView _watchPageMap;

        private int _watchIndex = 0;

        // Watch labels (frequent updates)
        private Label _wSpeed;                 // big gauge text (already exists)
        private Label _wHomeTime;              // top-left minimal time on Home page
        private Label _wHomeWeather;           // top-right condition + temp on Home page
        private VisualElement _wRunBox;        // container used to tint/grey air-time
        private Label _wRunTime;               // air-time value
        private Label _wRunName;               // "LAST AIR" / "IN-AIR"
        private VisualElement _wRunTrackerCard;

        // Dedicated Run Tracker watch page
        private Label _wRunPageName;
        private Label _wRunPageTime;
        private Label _wRunPageProgress;
        private Label _wRunPageTopSpeed;
        private Label _wRunPageStacks;
        private VisualElement _wRunPageFill;
        private VisualElement _runTrackerFill;

        private VisualElement _watchTasksList;

        // Phone
        private VisualElement _phoneExpanded;
        private VisualElement _btnClosePhone;
        private VisualElement _btnBack;
        private Label _pageTitle;
        private Label _navCurrency;

        private readonly Dictionary<string, ScrollView> _phonePages = new();
        private readonly Stack<string> _navStack = new();

        // Home tiles
        private VisualElement _tileTime;
        private VisualElement _tileWeather;
        private VisualElement _tileStats;
        private VisualElement _tileTasks;
        private VisualElement _tileAchievements;
        private VisualElement _tileMap;
        private VisualElement _tileRunTracker;
        private VisualElement _tileSave;
        private VisualElement _tileSettings;
        private VisualElement _tileSOS;
        private VisualElement _tileStatus;

        // Home tile labels
        private Label _tileTimeValue;
        private Label _tileDateValue;
        private Label _tileTempValue;
        private Label _tileWeatherValue;
        private Label _tileRainValue;

        private Label _tileStats1;
        private Label _tileStats2;
        private Label _tileStats3;

        private VisualElement _tileTasksList;

        private VisualElement _tileTasksProgressFill;
        private Label _tileTasksProgress;

        private Label _tileAch1;
        private Label _tileAch2;
        private Label _tileSunTimes;

        // Phone page bodies
        private Label _statsSessionBody;
        private Label _statsLifetimeBody;
        // Stats page UI (tabs + metric grid)
        private VisualElement _btnStatsTabSession;
        private VisualElement _btnStatsTabLifetime;
        private VisualElement _panelStatsSession;
        private VisualElement _panelStatsLifetime;

        // Stats page (Lifetime dashboard)
        private VisualElement _statsDashSession;
        private VisualElement _statsDashLifetime;

        // Stats page (Day/Weeks calendar + selected-day dashboard)
        private VisualElement _statsWeeksToolbar;
        private VisualElement _btnStatsWeeksBack;
        private VisualElement _btnStatsWeeksUnused;
        private Label _lblStatsWeeksTitle;

        private VisualElement _statsWeeksList;
        private VisualElement _statsDayPanel;
        private VisualElement _btnStatsPrevDay;
        private VisualElement _btnStatsNextDay;
        private Label _lblStatsDay;
        private VisualElement _statsDashDay;

        private int _statsExpandedWeekIndex = -1;
        private int _statsCurrentWeekIndex = -1;

        private DayKey? _statsSelectedDay;   // currently viewed day (detail panel)
        private DayState _statsDayState;     // live + persisted baseline/max tracking
        // “First day” / bounds
        private const string Pref_StatsBaseDay = "skigame.stats.calendarBaseDay.v1";
        private int _statsBaseDayOfYear = 0;
        private bool _statsBaseLoaded = false;

        // Daily archive (persisted)
        private const string Pref_StatsDailyArchive = "skigame.stats.dailyArchive.v1";
        [Serializable] private sealed class DayStatsArchive { public List<DayStatsRecord> days = new List<DayStatsRecord>(); }
        [Serializable] private struct DayStatsRecord { public int year; public int monthIndex; public int dayOfMonth; public DayStatsSnapshot stats; }

        private struct DayStatsSnapshot
        {
            public float topSpeedMps;
            public float avgSpeedMps;
            public float distanceMeters;

            public float verticalAscentMeters;
            public float verticalDescentMeters;

            public float airTimeSeconds;
            public float airDistanceMeters;

            public float grindTimeSeconds;
            public float grindDistanceMeters;

            public int runsCompleted;
            public int runsCompletedClean;
            public float topRunSpeedMps;

            public int liftsUsed;
            public int stacks;

            public int runsVisited;
            public int landmarksVisited;

            public static implicit operator DaySnapshot(DayStatsSnapshot s)
            {
                return new DaySnapshot
                {
                    topSpeedMps = s.topSpeedMps,
                    avgSpeedMps = s.avgSpeedMps,
                    distanceMeters = s.distanceMeters,

                    verticalAscentMeters = s.verticalAscentMeters,
                    verticalDescentMeters = s.verticalDescentMeters,

                    airTimeSeconds = s.airTimeSeconds,
                    airDistanceMeters = s.airDistanceMeters,

                    grindTimeSeconds = s.grindTimeSeconds,
                    grindDistanceMeters = s.grindDistanceMeters,

                    runsVisited = s.runsVisited,
                    runsCompleted = s.runsCompleted,
                    runsCompletedClean = s.runsCompletedClean,

                    topRunSpeedMps = s.topRunSpeedMps,

                    liftsUsed = s.liftsUsed,
                    stacks = s.stacks,

                    landmarksVisited = s.landmarksVisited
                };
            }

        }

        private struct SessionSnapshot
        {
            public float topSpeedMps;
            public float avgSpeedMps;
            public float distanceMeters;

            public float verticalAscentMeters;
            public float verticalDescentMeters;

            public float airTimeSeconds;
            public float airDistanceMeters;

            public float grindTimeSeconds;
            public float grindDistanceMeters;

            public int runsCompleted;
            public int runsCompletedClean;
            public float topRunSpeedMps;

            public int liftsUsed;
            public int stacks;

            public int runsVisited;
            public int landmarksVisited;
        }

        private bool _dailyArchiveLoaded = false;
        private DayStatsArchive _dailyArchive = new DayStatsArchive();
        private readonly Dictionary<long, DayStatsSnapshot> _dailyArchiveMap = new Dictionary<long, DayStatsSnapshot>(128);

        // Live “today” tracking (turn old session into a daily slice)
        private bool _liveDayKeyValid = false;
        private DayKey _liveDayKey;
        private SessionSnapshot _liveDaySessionBaseline;
        private float _liveDayStartUnscaled = 0f;
        private float _liveDayMaxSpeedMps = 0f;
        private float _liveDayMaxRunSpeedMps = 0f;

        private bool _statsWeeksDirty = true;
        private readonly List<StatCardRefs> _statCardsDay = new List<StatCardRefs>(32);

        private sealed class StatCardRefs
        {
            // Existing usage
            public Label value;
            public Func<PlayerStatsProfile, string> valueProvider;

            // Compatibility for newer code that references these names
            public string label;
            public Func<PlayerStatsProfile, string> provider;
            public Func<DaySnapshot, string> dayProvider;
        }

        private readonly List<StatCardRefs> _statCardsSession = new();
        private readonly List<StatCardRefs> _statCardsLifetime = new();

        private bool _statsShowSession = true;

        // --- Stats: Day/Calendar UI (shim fields to satisfy compiler) ---
        private VisualElement _statsWeeksPanel;     // container for the day list / weeks list (if you added one)
        private VisualElement _btnStatsDayBack;     // back from day detail to list
        private VisualElement _btnStatsDayPrev;     // previous day
        private VisualElement _btnStatsDayNext;     // next day
        private Label _lblStatsDayTitle;            // title for selected day (e.g. "Mon 12 Jan")

        // Active day selection (nullable so we can gate comparisons safely)
        private DayKey? _statsActiveDay;

        private Label _tasksBody;

        // Tasks page (daily matrix)
        private VisualElement _tasksInfoPanel;
        private Label _tasksInfoTitle;
        private Label _tasksInfoBody;
        private Button _tasksClaimAllBtn;
        private Label _tasksClaimAllValue;
        private VisualElement _tasksMatrix;

        private string _selectedDailyLadderId = null;
        private int _selectedDailyTierIndex = -1;

        // Claim-all hover + summary
        private bool _claimAllHoverActive = false;
        private readonly List<string> _claimAllSummaryLines = new();
        private readonly HashSet<string> _claimAllClaimedBefore = new();
        private bool _showClaimAllSummary = false;

        // Last-claim feedback (used to flash the claimed cell + show a brief toast in the info panel)
        private string _lastClaimLadderId = null;
        private int _lastClaimTierIndex = -1;
        private int _lastClaimRewardGranted = 0;
        private bool _lastClaimRowBonusGranted = false;
        private int _lastClaimRowBonusAmount = 0;
        private float _lastClaimTimeUnscaled = -999f;

        private readonly List<ProgressionDirector.DailyTierDisplay> _dailyTierBuffer = new();

        private Label _achievementsBody;
        private Label _timeWeatherBody; // legacy fallback

        private readonly List<AchievementDefinitionSO> _achTileCatalog = new(256);

        // Time & Weather page (new layout)
        private Label _twTime;
        private Label _twDayOfWeek;
        private Label _twDate;

        private Label _twCurrentTemp;
        private Label _twCurrentCond;
        private Label _twCurrentRain;
        private Label _twCurrentWind;
        private Label _twSunrise;
        private Label _twSunset;

        private ScrollView _twHourlyScroll;
        private VisualElement _twHourlyRow;

        private ScrollView _pageTimeWeather;

        // How many cards fit without scrolling (computed from layout)
        [SerializeField] private int _twMaxVisibleHours = 6;

        // Hour window bookkeeping (for “near midnight” backfill + snap-to-now)
        private int _twHourlyBuiltStartHour = -1;
        private bool _twHourlySnapToNowPending = false;

        // Layout constants (must match USS)
        private const int TW_CARD_WIDTH = 118; // wider so labels never clip
        private const int TW_CARD_GAP = 6;

        private sealed class HourCardRefs
        {
            public VisualElement root;
            public Label hour;
            public Label cond;
            public Label rain;
            public VisualElement rainFill;
            public Label sunMarker;
        }

        private readonly List<HourCardRefs> _twHourCards = new();
        private int _twLastHourBuilt = -1;

        // Data sources
        private PlayerStatsManager _statsManager;
        private PlayerStatsProfile _profile;
        private Rigidbody _playerRb;
        private SkiController _skiController;
        private ProgressionDirector _progression;
        private RunProgressTracker _runProgressTracker;

        // Time/Weather
        private TimeWeather.TimeController _time;
        private TimeWeather.WeatherController _weather;
        private WindController _wind;


        // Home tile: run tracker (current run / quick entry to runs page)
        private Label _runTrackerLine1;
        private Label _runTrackerLine2;
        private Label _runTrackerLine3;

        // Runs page
        private RunsPageUI _runsPageUI;

        // Input state
        private bool _tabHeld;
        private float _tabDownAt;
        private bool _phoneOpen;

        /// <summary>
        /// Read-only external access for systems that need to know whether the phone UI is open
        /// (eg. camera input gating, player input gating, etc.).
        /// </summary>
        public bool IsPhoneOpen => _phoneOpen;

        // --- Performance throttles (Editor/runtime) ---
        [SerializeField, Range(0.02f, 0.5f)]
        private float uiRefreshIntervalSeconds = 0.10f;

        [SerializeField, Range(0.10f, 2.0f)]
        private float sceneRefPollIntervalSeconds = 0.50f;

        private float _nextUiRefreshTime;
        private float _nextSceneRefPollTime;
        // Prevent rebuilding the task matrix while the user is interacting with it.
        // Otherwise elements can be destroyed between PointerDown/PointerUp and clicks feel “random”.
        private float _suppressTasksMatrixRebuildUntil = 0f;

        // --- Ski Pass (Home tile) ---
        private VisualElement _tileSkiPass;
        private Label _lblTileSkiPassLevel;

        // --- Ski Pass (Page) ---
        private VisualElement _skiPassTimeFill;
        private Label _lblSkiPassCurrentName;
        private Label _lblSkiPassCurrentExpiry;
        private Label _lblSkiPassTimeRemaining;

        private VisualElement _listSkiPassAvailableLifts;

        private VisualElement _gridSkiPassCards;
        private Label _lblSkiPassSelectedName;
        private Label _lblSkiPassLevel;
        private Label _lblSkiPassPrice;
        private VisualElement _rowSkiPassDurations;
        private VisualElement _listSkiPassSelectedLifts;
        private Button _btnSkiPassPurchase;
        private Label _lblSkiPassResult;

        // Ski Pass runtime
        private SkiPassManager _skiPassMgr;
        private int _skiPassSelectedLevel = -1;
        private int _skiPassSelectedDurationIndex = 0;
        private bool _skiPassCardsBuilt = false;

        [Serializable]
        private sealed class SkiPassCardMeta
        {
            public int level;
            public Color mapColor;
            public bool lockedBelow;
        }

        private void Reset()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }

        private bool _uiBound;
        private bool _uiBindScheduled;
        private int _uiBindAttempts;

        private void OnEnable()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return;

            _root = document.rootVisualElement;
            if (_root == null) return;

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                _root.styleSheets.Add(styleSheet);

            // IMPORTANT:
            // UIDocument may not have cloned the UXML yet when THIS component's OnEnable runs.
            // So we schedule binding for the next UI tick, and retry a few times if needed.
            ScheduleUiBind();
        }

        private void ScheduleUiBind()
        {
            if (_root == null) return;
            if (_uiBindScheduled) return;

            _uiBindScheduled = true;

            _root.schedule.Execute(() =>
            {
                _uiBindScheduled = false;
                if (_root == null) return;

                // If the UXML tree hasn't been cloned into the root yet, key elements won't exist.
                // Retry a few times (cheap) instead of binding null references.
                var home = _root.Q<VisualElement>("Page_Home");
                var stats = _root.Q<VisualElement>("Page_Stats");

                if ((home == null && stats == null) && _uiBindAttempts < 10)
                {
                    _uiBindAttempts++;
                    ScheduleUiBind();
                    return;
                }

                _uiBindAttempts = 0;

                CacheSceneReferences();
                BindUI();
                BindInput();

                SetPhoneOpen(startPhoneOpen);
                SetWatchPage(Mathf.Clamp(_watchIndex, 0, _watchPages.Count - 1), force: true);
                NavigateTo("Page_Home", clearStack: true);

                // Ensure consistent visuals on start.
                RefreshAllUI(force: true);

                _uiBound = true;
            }).StartingIn(0);
        }

        private void OnDisable()
        {
            _uiBound = false;
            _uiBindAttempts = 0;
            _uiBindScheduled = false;

            UnbindInput();

            // Persist current day stats so the user can browse today's numbers after a restart.
            if (_dailyArchiveLoaded)
                PersistLiveDayToArchive();
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            // Late-bind in case controllers spawn after UI (throttled).
            if (now >= _nextSceneRefPollTime)
            {
                _nextSceneRefPollTime = now + sceneRefPollIntervalSeconds;

                if (_statsManager == null) _statsManager = FindObjectOfType<PlayerStatsManager>();
                if (_statsManager != null) _profile = _statsManager.Profile;

                if (_playerRb == null || _skiController == null || _runProgressTracker == null)
                {
                    var tracker = FindObjectOfType<PlayerStatsTracker>();
                    if (tracker != null)
                    {
                        if (_playerRb == null) _playerRb = tracker.GetComponent<Rigidbody>();
                        if (_skiController == null) _skiController = tracker.GetComponent<SkiController>();
                        if (_runProgressTracker == null) _runProgressTracker = tracker.GetComponent<RunProgressTracker>();
                    }

                    // Fallback (in case Tracker isn't on the same object as SkiController)
                    if (_skiController == null) _skiController = FindObjectOfType<SkiController>();
                    if (_runProgressTracker == null) _runProgressTracker = FindObjectOfType<RunProgressTracker>();
                }

                
                if (_time == null)
                    _time = TimeWeather.TimeController.instance != null
                        ? TimeWeather.TimeController.instance
                        : FindObjectOfType<TimeWeather.TimeController>();

                if (_weather == null)
                    _weather = TimeWeather.WeatherController.instance != null
                        ? TimeWeather.WeatherController.instance
                        : FindObjectOfType<TimeWeather.WeatherController>();

                if (_wind == null)
                    _wind = WindController.Instance != null
                        ? WindController.Instance
                        : FindObjectOfType<WindController>();

                if (_progression == null) _progression = FindObjectOfType<ProgressionDirector>();
            }

            HandleWatchScroll();
            HandleHoldToOpen();

            // Keep UI responsive but lightweight (throttled).
            if (now >= _nextUiRefreshTime)
            {
                _nextUiRefreshTime = now + uiRefreshIntervalSeconds;
                RefreshAllUI(force: false);
            }

            // Map + minimaps should update continuously so the breadcrumb is always sampling/rendering
            // regardless of which page is open.
            if (_skiController != null)
            {
                var t = _skiController.transform;
                float dt = Time.unscaledDeltaTime;

                // Authoritative sampler: map page UI
                _mapPageUI?.SetPlayer(t);
                _mapPageUI?.Tick(dt);

                // Share the same breadcrumb trail to minimaps (so they show it even if map page isn't open)
                var sharedTrail = _mapPageUI != null ? _mapPageUI.GetWorldTrailXZ() : null;

                if (_watchMiniMapUI != null)
                {
                    _watchMiniMapUI.SetPlayer(t);
                    _watchMiniMapUI.SetTrailSamplingEnabled(false);
                    if (sharedTrail != null) _watchMiniMapUI.SetWorldTrailXZ(sharedTrail);
                    _watchMiniMapUI.Tick(dt);
                }

                if (_homeTileMiniMapUI != null)
                {
                    _homeTileMiniMapUI.SetPlayer(t);
                    _homeTileMiniMapUI.SetTrailSamplingEnabled(false);
                    if (sharedTrail != null) _homeTileMiniMapUI.SetWorldTrailXZ(sharedTrail);
                    _homeTileMiniMapUI.Tick(dt);
                }
            }
        }

        private void CacheSceneReferences()
        {
            _statsManager = FindObjectOfType<PlayerStatsManager>();
            _profile = _statsManager != null ? _statsManager.Profile : null;

            var tracker = FindObjectOfType<PlayerStatsTracker>();
            if (tracker != null)
            {
                _playerRb = tracker.GetComponent<Rigidbody>();
                _skiController = tracker.GetComponent<SkiController>();
            }

            if (_skiController == null) _skiController = FindObjectOfType<SkiController>();

            _time = TimeWeather.TimeController.instance != null ? TimeWeather.TimeController.instance : FindObjectOfType<TimeWeather.TimeController>();
            _weather = TimeWeather.WeatherController.instance != null ? TimeWeather.WeatherController.instance : FindObjectOfType<TimeWeather.WeatherController>();
            _wind = WindController.Instance != null ? WindController.Instance : FindObjectOfType<WindController>();

            _progression = FindObjectOfType<ProgressionDirector>();

        }

        private void BindUI()
        {
            // Watch
            _watchCollapsed = Q<VisualElement>("WatchCollapsed");
            _watchTitle = Q<Label>("Lbl_WatchTitle");
            _watchDots = Q<VisualElement>("WatchDots");
            _watchPagesRoot = Q<VisualElement>("WatchPages");
            _wRunPageFill = Q<VisualElement>("WatchRunPageFill");
            _runTrackerFill = Q<VisualElement>("RunTrackerFill");


            _dotElems.Clear();
            if (_watchDots != null)
            {
                foreach (var child in _watchDots.Children())
                    _dotElems.Add(child);
            }

            _watchPages.Clear();

            // NOTE: We keep the underlying UXML element name as WatchPage_Speed,
            // but we display it as "Home" in the watch title.
            _watchPages.Add(Q<ScrollView>("WatchPage_Speed"));

            // Map screen (minimap)
            _watchPageMap = Q<ScrollView>("WatchPage_Map");
            _watchPages.Add(_watchPageMap);

            // Dedicated run tracker screen (after Map)
            _watchPages.Add(Q<ScrollView>("WatchPage_RunTracker"));

            _statsWeeksPanel = _root.Q<VisualElement>("StatsWeeksToolbar");     // only if you added it to UXML
            _btnStatsDayBack = _root.Q<VisualElement>("Btn_StatsWeeksBack");
            _btnStatsDayPrev = _root.Q<VisualElement>("Btn_StatsDayPrev");
            _btnStatsDayNext = _root.Q<VisualElement>("Btn_StatsDayNext");
            _lblStatsDayTitle = _root.Q<Label>("Lbl_StatsDayTitle");

            // Tasks
            _watchPages.Add(Q<ScrollView>("WatchPage_Tasks"));

            //_watchPages.Add(Q<ScrollView>("WatchPage_SOS"));

            // Watch labels (Home page)
            _wSpeed = Q<Label>("Lbl_WatchSpeed");

            // These MUST exist on the WatchPage_Speed (Home) page in UXML:
            _wHomeTime = Q<Label>("Lbl_WatchSpeedTime");
            _wHomeWeather = Q<Label>("Lbl_WatchSpeedWeather");
            _wRunBox = Q<VisualElement>("WatchAirBox");
            _wRunName = Q<Label>("Lbl_WatchRunName");
            _wRunTime = Q<Label>("Lbl_WatchRunTime");
            _wRunTrackerCard = Q<VisualElement>("WatchRunTrackerCard");

            // Dedicated Run Tracker watch page
            _wRunPageName = Q<Label>("Lbl_WatchRunPageName");
            _wRunPageTime = Q<Label>("Lbl_WatchRunPageTime");
            _wRunPageProgress = Q<Label>("Lbl_WatchRunPageProgress");
            _wRunPageTopSpeed = Q<Label>("Lbl_WatchRunPageTopSpeed");
            _wRunPageStacks = Q<Label>("Lbl_WatchRunPageStacks");

            // Ski Pass
            _tileSkiPass = Q<VisualElement>("Tile_SkiPass");
            WireTile(_tileSkiPass, () => NavigateTo("Page_SkiPass"));

            _lblTileSkiPassLevel = Q<Label>("Lbl_TileSkiPassLevel");

            // Ski Pass page
            _lblSkiPassCurrentName = Q<Label>("Lbl_SkiPassCurrentName");
            _lblSkiPassCurrentExpiry = Q<Label>("Lbl_SkiPassCurrentExpiry");
            _lblSkiPassTimeRemaining = Q<Label>("Lbl_SkiPassTimeRemaining");
            _skiPassTimeFill = Q<VisualElement>("SkiPassTimeFill");

            _listSkiPassAvailableLifts = Q<VisualElement>("List_SkiPassAvailableLifts");

            _gridSkiPassCards = Q<VisualElement>("Grid_SkiPassCards");
            _lblSkiPassSelectedName = Q<Label>("Lbl_SkiPassSelectedName");
            _lblSkiPassPrice = Q<Label>("Lbl_SkiPassPrice");
            _rowSkiPassDurations = Q<VisualElement>("Row_SkiPassDurations");
            _listSkiPassSelectedLifts = Q<VisualElement>("List_SkiPassSelectedLifts");

            _btnSkiPassPurchase = Q<Button>("Btn_SkiPassPurchase");
            _lblSkiPassResult = Q<Label>("Lbl_SkiPassResult");

            if (_btnSkiPassPurchase != null)
            {
                _btnSkiPassPurchase.clicked -= OnClickSkiPassPurchase;
                _btnSkiPassPurchase.clicked += OnClickSkiPassPurchase;
            }

            HookSkiPassManager();
            RefreshSkiPassTile();
            _skiPassCardsBuilt = false;

            // Tasks (Watch)
            _watchTasksList = Q<VisualElement>("WatchTasksList");

            // Phone
            _phoneExpanded = Q<VisualElement>("PhoneExpanded");
            _btnClosePhone = Q<VisualElement>("Btn_ClosePhone");
            _btnBack = Q<VisualElement>("Btn_Back");
            _pageTitle = Q<Label>("Lbl_PageTitle");
            _navCurrency = Q<Label>("Lbl_Currency");

            if (_btnClosePhone != null) _btnClosePhone.RegisterCallback<ClickEvent>(_ => SetPhoneOpen(false));
            if (_btnBack != null) _btnBack.RegisterCallback<ClickEvent>(_ => NavigateBack());

            _phonePages.Clear();
            RegisterPage("Page_Home");
            RegisterPage("Page_Stats");
            RegisterPage("Page_Tasks");
            RegisterPage("Page_Achievements");
            RegisterPage("Page_SkiPass");
            RegisterPage("Page_TimeWeather");
            RegisterPage("Page_Map");
            RegisterPage("Page_Runs");
            RegisterPage("Page_Save");
            RegisterPage("Page_Settings");
            RegisterPage("Page_SOS");

            // Home tiles
            //_tileTime = Q<VisualElement>("Tile_Time");
            //_tileWeather = Q<VisualElement>("Tile_Weather");
            _tileStats = Q<VisualElement>("Tile_Stats");
            _tileTasks = Q<VisualElement>("Tile_Tasks");
            _tileAchievements = Q<VisualElement>("Tile_Achievements");
            _tileMap = Q<VisualElement>("Tile_Map");
            _tileRunTracker = Q<VisualElement>("Tile_RunTracker");
            WireTile(_tileRunTracker, () => NavigateTo("Page_Runs"));

            _tileSave = Q<VisualElement>("Tile_Save");
            _tileSettings = Q<VisualElement>("Tile_Settings");
            _tileSOS = Q<VisualElement>("Tile_SOS");
            _tileStatus = Q<VisualElement>("Tile_Status");
            WireTile(_tileStatus, () => NavigateTo("Page_TimeWeather"));

            //WireTile(_tileTime, () => NavigateTo("Page_TimeWeather"));
            //WireTile(_tileWeather, () => NavigateTo("Page_TimeWeather"));
            WireTile(_tileStats, () => NavigateTo("Page_Stats"));
            WireTile(_tileTasks, () => NavigateTo("Page_Tasks"));
            WireTile(_tileAchievements, () => NavigateTo("Page_Achievements"));
            WireTile(_tileMap, () => NavigateTo("Page_Map"));
            // Watch minimap: embed inside WatchPage_Map (dedicated map watch screen)
            if (_watchPageMap != null && mapData != null)
            {
                var cc = _watchPageMap.contentContainer;

                // IMPORTANT: don't rely on absolute positioning inside ScrollView contentContainer,
                // it can collapse to 0 height and render nothing.
                cc.style.flexGrow = 1;
                cc.style.flexDirection = FlexDirection.Column;
                cc.style.overflow = Overflow.Hidden;

                var watchMiniRoot = BuildEmbeddedMapRoot("WatchMiniMapRoot");
                watchMiniRoot.style.flexGrow = 1;
                watchMiniRoot.style.position = Position.Relative;
                watchMiniRoot.style.minHeight = 1; // prevents 0-height edge cases

                cc.Clear();              // ensure no placeholder blocks layout
                cc.Add(watchMiniRoot);   // normal flow layout

                _watchMiniMapUI = new PhoneMapPageUI();
                _watchMiniMapUI.Bind(watchMiniRoot, mapData, mapReferenceCamera);
                _watchMiniMapUI.SetPlayerTracking(showMarker: true, drawTrail: true);
                _watchMiniMapUI.SetTrailSamplingEnabled(false);
                _watchMiniMapUI.SetMinimapMode(
                    enabled: true,
                    followPlayer: true,
                    lockPan: true,
                    allowZoom: true,
                    suppressSelection: true,
                    hideMarkerLabels: true
                );

                // Refresh AFTER geometry exists (prevents "tiny marker in corner" / bad centering).
                watchMiniRoot.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    _watchMiniMapUI?.Refresh();
                });

                _watchMiniMapUI.Refresh();
            }

            // Home tile minimap: embed inside Tile_Map
            if (_tileMap != null && mapData != null)
            {
                _tileMap.style.position = Position.Relative;

                var tileMiniRoot = BuildEmbeddedMapRoot("HomeTileMiniMapRoot");
                tileMiniRoot.style.position = Position.Absolute;
                tileMiniRoot.style.left = 0;
                tileMiniRoot.style.top = 0;
                tileMiniRoot.style.right = 0;
                tileMiniRoot.style.bottom = 0;
                tileMiniRoot.style.opacity = 0.95f;

                _tileMap.Add(tileMiniRoot);
                tileMiniRoot.SendToBack();

                _homeTileMiniMapUI = new PhoneMapPageUI();
                _homeTileMiniMapUI.Bind(tileMiniRoot, mapData, mapReferenceCamera);
                _homeTileMiniMapUI.SetPlayerTracking(showMarker: true, drawTrail: true);
                _homeTileMiniMapUI.SetTrailSamplingEnabled(false);
                _homeTileMiniMapUI.SetMinimapMode(enabled: true, followPlayer: true, lockPan: true, allowZoom: false, suppressSelection: true, hideMarkerLabels: true);

                // Hide the "(placeholder)" / tile subtext so the minimap reads cleanly
                _tileMap.Query<Label>(className: "tile-sub").ForEach(l => l.style.display = DisplayStyle.None);

                // Ensure the background + lines are actually built for the tile minimap.
                tileMiniRoot.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    _homeTileMiniMapUI?.Refresh();
                });
                _homeTileMiniMapUI.Refresh();

                // Hide the "(placeholder)" tile sublabel so the minimap is readable.
                var subLabels = _tileMap.Query<Label>(className: "tile-sub");
                var tmpSubs = new List<Label>();
                subLabels.ToList(tmpSubs);
                for (int i = 0; i < tmpSubs.Count; i++)
                    tmpSubs[i].style.display = DisplayStyle.None;

            }

            WireTile(_tileSave, () => NavigateTo("Page_Save"));
            WireTile(_tileSettings, () => NavigateTo("Page_Settings"));
            WireTile(_tileSOS, () => NavigateTo("Page_SOS"));

            // Home tile labels
            _tileTimeValue = Q<Label>("Lbl_TileTime");
            _tileDateValue = Q<Label>("Lbl_TileDate");
            _tileTempValue = Q<Label>("Lbl_TileTemp");
            _tileWeatherValue = Q<Label>("Lbl_TileWeather");
            _tileRainValue = Q<Label>("Lbl_TileRain");
            _tileSunTimes = Q<Label>("Lbl_TileSunTimes");

            _tileStats1 = Q<Label>("Lbl_TileStatsLine1");
            _tileStats2 = Q<Label>("Lbl_TileStatsLine2");
            _tileStats3 = Q<Label>("Lbl_TileStatsLine3");

            _tileTasksList = Q<VisualElement>("TileTasksList");

            _tileTasksProgressFill = Q<VisualElement>("TileTasksProgressFill");
            _tileTasksProgress = Q<Label>("Lbl_TileTasksProgress");

            _tileAch1 = Q<Label>("Lbl_TileAch1");
            _tileAch2 = Q<Label>("Lbl_TileAch2");

            // Phone bodies
            _statsSessionBody = Q<Label>("Lbl_StatsSession");
            _statsLifetimeBody = Q<Label>("Lbl_StatsLifetime");
            // -------------------------
            // Stats page
            // -------------------------
            _panelStatsSession = Q<VisualElement>("Panel_StatsSession");
            _panelStatsLifetime = Q<VisualElement>("Panel_StatsLifetime");

            _btnStatsTabSession = Q<VisualElement>("Btn_StatsTabSession");
            _btnStatsTabLifetime = Q<VisualElement>("Btn_StatsTabLifetime");

            if (_btnStatsTabSession != null)
                _btnStatsTabSession.RegisterCallback<ClickEvent>(_ => SetStatsTab(true));
            if (_btnStatsTabLifetime != null)
                _btnStatsTabLifetime.RegisterCallback<ClickEvent>(_ => SetStatsTab(false));

            // Lifetime dashboard
            _statsDashLifetime = Q<VisualElement>("StatsDash_Lifetime");

            // Daily stats calendar (Weeks) + selected day panel
            _statsWeeksToolbar = Q<VisualElement>("StatsWeeksToolbar");
            _btnStatsWeeksBack = Q<VisualElement>("Btn_StatsWeeksBack");
            _btnStatsWeeksUnused = Q<VisualElement>("Btn_StatsWeeksUnused");
            _lblStatsWeeksTitle = Q<Label>("Lbl_StatsWeeksTitle");

            _statsWeeksList = Q<VisualElement>("StatsCalendarGrid");
            _statsDayPanel = Q<VisualElement>("Panel_StatsDayDetail");
            _btnStatsPrevDay = Q<VisualElement>("Btn_StatsPrevDay");
            _btnStatsNextDay = Q<VisualElement>("Btn_StatsNextDay");
            _lblStatsDay = Q<Label>("Lbl_StatsDayTitle");
            _statsDashDay = Q<VisualElement>("StatsDash_Day");

            if (_btnStatsWeeksBack != null)
                _btnStatsWeeksBack.RegisterCallback<ClickEvent>(_ => CloseStatsDayDetail());

            // This right arrow is unused on Stats – hide it for clarity.
            if (_btnStatsWeeksUnused != null)
                _btnStatsWeeksUnused.style.display = DisplayStyle.None;

            if (_btnStatsPrevDay != null)
                _btnStatsPrevDay.RegisterCallback<ClickEvent>(_ => StepStatsDay(-1));
            if (_btnStatsNextDay != null)
                _btnStatsNextDay.RegisterCallback<ClickEvent>(_ => StepStatsDay(+1));

            LoadDailyStatsArchive();
            EnsureStatsBaseDayLoaded();
            BuildStatsDashboard();
            BuildStatsDayDashboard();
            RebuildStatsWeeksList();

            SetStatsTab(true, force: true);

            _tasksBody = Q<Label>("Lbl_TasksBody");

            _tasksInfoPanel = Q<VisualElement>("TasksInfoPanel");
            _tasksInfoTitle = Q<Label>("Lbl_TasksInfoTitle");
            _tasksInfoBody = Q<Label>("Lbl_TasksInfoBody");
            _tasksClaimAllBtn = Q<Button>("Btn_TasksClaimAll");
            _tasksClaimAllValue = Q<Label>("Lbl_TasksClaimAllValue");
            _tasksMatrix = Q<VisualElement>("TasksMatrix");

            if (_tasksClaimAllBtn != null)
            {
                _tasksClaimAllBtn.clicked -= OnClickClaimAllDaily;
                _tasksClaimAllBtn.clicked += OnClickClaimAllDaily;

                // Hovering "Claim All" should highlight all claimable cells.
                _tasksClaimAllBtn?.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    _claimAllHoverActive = true;
                    _tasksMatrix?.EnableInClassList("claimall-hover", true);
                });

                _tasksClaimAllBtn?.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    _claimAllHoverActive = false;
                    _tasksMatrix?.EnableInClassList("claimall-hover", false);
                });

            }

            _runTrackerLine1 = Q<Label>("Lbl_RunTrackerLine1");
            _runTrackerLine2 = Q<Label>("Lbl_RunTrackerLine2");
            _runTrackerLine3 = Q<Label>("Lbl_RunTrackerLine3");
            _runTrackerFill = Q<VisualElement>("RunTrackerFill"); // ok if null (older UXML)

            _achievementsBody = Q<Label>("Lbl_AchievementsBody");
            _timeWeatherBody = Q<Label>("Lbl_TimeWeatherBody");

            // Time & Weather (new)
            _twTime = Q<Label>("Lbl_TW_Time");
            _twDayOfWeek = Q<Label>("Lbl_TW_Day");
            _twDate = Q<Label>("Lbl_TW_Date");

            _twCurrentTemp = Q<Label>("Lbl_TW_CurrentTemp");
            _twCurrentCond = Q<Label>("Lbl_TW_CurrentCond");
            _twCurrentRain = Q<Label>("Lbl_TW_CurrentRain");
            _twCurrentWind = Q<Label>("Lbl_TW_CurrentWind");
            _twSunrise = Q<Label>("Lbl_TW_Sunrise");
            _twSunset = Q<Label>("Lbl_TW_Sunset");

            _twHourlyScroll = Q<ScrollView>("TW_HourlyScroll");
            _twHourlyRow = Q<VisualElement>("TW_HourlyRow");

            // Route wheel scrolling so this page never scrolls vertically;
            // wheel input should only move the hourly forecast strip horizontally.
            _pageTimeWeather = Q<ScrollView>("Page_TimeWeather");

            if (_pageTimeWeather != null)
            {
                // Keep the page pinned to the top (no vertical paging).
                _pageTimeWeather.scrollOffset = Vector2.zero;

                // Capture wheel events before they scroll the page.
                // If the wheel is happening over the hourly strip, let the hourly handler consume it instead.
                _pageTimeWeather.RegisterCallback<WheelEvent>(evt =>
                {
                    if (_twHourlyScroll != null && IsDescendant(evt.target as VisualElement, _twHourlyScroll))
                        return;

                    evt.StopImmediatePropagation();
                }, TrickleDown.TrickleDown);
            }

            if (_twHourlyScroll != null)
            {
                // Convert vertical wheel movement into horizontal scrolling for the hourly strip.
                _twHourlyScroll.RegisterCallback<WheelEvent>(evt =>
                {
                    var off = _twHourlyScroll.scrollOffset;

                    // Trackpads may give delta.x; mouse wheel usually uses delta.y.
                    float move = Mathf.Abs(evt.delta.x) > Mathf.Abs(evt.delta.y) ? evt.delta.x : evt.delta.y;

                    // Scroll down -> move right through the forecast.
                    off.x += move * 32f;   // tune if needed
                    off.y = 0f;            // hard-lock to horizontal

                    _twHourlyScroll.scrollOffset = off;
                    evt.StopImmediatePropagation();
                });
            }

            // Recompute how many cards fit whenever the layout size changes.
            if (_twHourlyScroll != null)
            {
                _twHourlyScroll.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    UpdateTimeWeatherHourlyCapacity();
                    EnsureTimeWeatherHourlyCards(forceRebuild: true);
                });
            }

            UpdateTimeWeatherHourlyCapacity();
            EnsureTimeWeatherHourlyCards(forceRebuild: true);

            // SOS big button
            var btnSOS = Q<VisualElement>("Btn_SOSRespawn");
            if (btnSOS != null) btnSOS.RegisterCallback<ClickEvent>(_ => TriggerRespawn());

            // Map page UI (IMPORTANT: bind to the Page_Map subtree to avoid name collisions with embedded minimaps)
            var mapPage = Q<ScrollView>("Page_Map");

            _mapPageUI = new PhoneMapPageUI();
            _mapPageUI.Bind(mapPage != null ? (VisualElement)mapPage : _root, mapData, mapReferenceCamera);
            _mapPageUI.SetDebugCompareProjections(false);

            var reg = PointOfInterestRegistry.Instance;

            _mapPageUI.ApplyStyle(mapStyle);
            _mapPageUI.SetPOIRegistry(reg);

            _watchMiniMapUI?.ApplyStyle(mapStyle);
            _watchMiniMapUI?.SetPOIRegistry(reg);

            _homeTileMiniMapUI?.ApplyStyle(mapStyle);
            _homeTileMiniMapUI?.SetPOIRegistry(reg);

            // Hook selection events -> info panel
            _mapPageUI.MarkerSelected += OnMapMarkerSelected;
            _mapPageUI.PolylineSelected += OnMapPolylineSelected;
            _mapPageUI.SelectionCleared += HideMapInfoPanel;

            // Build the map info panel (bottom overlay) once.
            BuildMapInfoPanel();

            _runsPageUI = GetComponent<RunsPageUI>();

            if (_runsPageUI != null)
            {
                _runsPageUI.SetMapData(mapData);

                _runsPageUI.NavigateToMapRequested = (runId) =>
                {
                    NavigateTo("Page_Map", true);
                    NavigateToMapAndSelectRun(runId);
                };
            }

            HookSkiPassManager();
            RefreshSkiPassTile();

        }

        private void BindInput()
        {
            if (tabAction != null && tabAction.action != null)
            {
                tabAction.action.Enable();
                tabAction.action.started += OnTabStarted;
                tabAction.action.canceled += OnTabCanceled;
            }

            if (scrollAction != null && scrollAction.action != null)
                scrollAction.action.Enable();
        }

        private void UnbindInput()
        {
            if (tabAction != null && tabAction.action != null)
            {
                tabAction.action.started -= OnTabStarted;
                tabAction.action.canceled -= OnTabCanceled;
            }
        }

        private void OnTabStarted(InputAction.CallbackContext ctx)
        {
            _tabHeld = true;
            _tabDownAt = Time.unscaledTime;
        }

        private void OnTabCanceled(InputAction.CallbackContext ctx)
        {
            if (!_tabHeld) return;

            float held = Time.unscaledTime - _tabDownAt;
            _tabHeld = false;

            // If phone is open and they release after a hold, do nothing; close is handled by hold threshold logic.
            // If phone is closed:
            // - tap cycles watch screens
            // - hold opens phone (already handled in Update)
            if (!_phoneOpen)
            {
                if (held < holdToOpenSeconds)
                    CycleWatch();
            }
            else
            {
                // If phone is open, a quick tap could optionally go "back" — for now do nothing (keeps accidental taps safe).
            }
        }

        private void HandleHoldToOpen()
        {
            if (!_tabHeld) return;

            float held = Time.unscaledTime - _tabDownAt;
            if (held < holdToOpenSeconds) return;

            // Trigger once per hold.
            _tabHeld = false;

            // Toggle phone state on hold.
            SetPhoneOpen(!_phoneOpen);
        }

        private void HandleWatchScroll()
        {
            if (_phoneOpen) return;
            if (scrollAction == null || scrollAction.action == null) return;
            if (_watchPages.Count == 0) return;

            float dy = 0f;

            // Most mouse wheel bindings deliver Vector2 with Y as scroll.
            try
            {
                var v = scrollAction.action.ReadValue<Vector2>();
                dy = v.y;
            }
            catch
            {
                // If it's bound as float, fall back.
                dy = scrollAction.action.ReadValue<float>();
            }

            // If we're on the watch Map screen, use scroll to zoom the minimap.
            if (_watchPages.Count > 0)
            {
                var activePage = _watchPages[Mathf.Clamp(_watchIndex, 0, _watchPages.Count - 1)];
                if (activePage == _watchPageMap && _watchMiniMapUI != null && Mathf.Abs(dy) > 0.0001f)
                {
                    _watchMiniMapUI.ZoomFromScroll(dy);
                    return;
                }
            }

            if (Mathf.Abs(dy) < 0.001f) return;

            var active = _watchPages[Mathf.Clamp(_watchIndex, 0, _watchPages.Count - 1)];
            if (active == null) return;

            // UI Toolkit scrollOffset increases downward.
            var off = active.scrollOffset;
            off.y -= dy * (watchScrollSpeed * Time.unscaledDeltaTime);
            off.y = Mathf.Max(0, off.y);
            active.scrollOffset = off;
        }

        private void CycleWatch()
        {
            if (_watchPages.Count == 0) return;
            _watchIndex = (_watchIndex + 1) % _watchPages.Count;
            SetWatchPage(_watchIndex, force: true);
        }

        private void SetWatchPage(int index, bool force)
        {
            if (_watchPages.Count == 0) return;

            _watchIndex = Mathf.Clamp(index, 0, _watchPages.Count - 1);

            for (int i = 0; i < _watchPages.Count; i++)
            {
                if (_watchPages[i] == null) continue;
                _watchPages[i].EnableInClassList("is-active", i == _watchIndex);
            }

            // Dots: hide any dots beyond current page count, and highlight current
            for (int i = 0; i < _dotElems.Count; i++)
            {
                bool inRange = i < _watchPages.Count;
                _dotElems[i].style.display = inRange ? DisplayStyle.Flex : DisplayStyle.None;
                if (inRange)
                    _dotElems[i].EnableInClassList("is-on", i == _watchIndex);
            }

            // Title
            if (_watchTitle != null)
                _watchTitle.text = GetWatchTitle(_watchIndex);

            // Reset scroll to top when switching pages (feels like an app screen)
            var active = _watchPages[_watchIndex];
            if (active != null) active.scrollOffset = Vector2.zero;
        }

        private string GetWatchTitle(int idx)
        {
            return idx switch
            {
                0 => "Home",
                1 => "Map",
                2 => "Run",
                3 => "Tasks",
                _ => "Watch"
            };
        }

        private void SetPhoneOpen(bool open)
        {
            _phoneOpen = open;

            if (_watchCollapsed != null)
                _watchCollapsed.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;

            if (_phoneExpanded != null)
            {
                _phoneExpanded.EnableInClassList("is-open", open);
                _phoneExpanded.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (open)
            {
                // Whenever we open phone, land on Home (feels predictable).
                NavigateTo("Page_Home", clearStack: true);
            }
        }

        private void RegisterPage(string pageName)
        {
            var page = Q<ScrollView>(pageName);
            if (page == null) return;

            _phonePages[pageName] = page;
        }

        private void NavigateTo(string pageName, bool clearStack = false)
        {
            if (!_phonePages.ContainsKey(pageName)) return;

            if (clearStack)
                _navStack.Clear();

            // push current
            string current = GetActivePhonePage();
            if (!string.IsNullOrEmpty(current) && current != pageName)
                _navStack.Push(current);

            ShowPhonePage(pageName);
        }

        private void NavigateBack()
        {
            if (_navStack.Count == 0)
            {
                ShowPhonePage("Page_Home");
                return;
            }

            var prev = _navStack.Pop();
            ShowPhonePage(prev);
        }

        private static bool IsDescendant(VisualElement target, VisualElement ancestor)
        {
            if (target == null || ancestor == null) return false;

            var ve = target;
            while (ve != null)
            {
                if (ve == ancestor) return true;
                ve = ve.parent;
            }
            return false;
        }

        private static VisualElement BuildEmbeddedMapRoot(string rootName)
        {
            // Root container that holds the map subtree.
            var root = new VisualElement { name = rootName };
            root.style.position = Position.Relative;
            root.style.flexGrow = 1;

            // Make it behave like a minimap window
            root.style.overflow = Overflow.Hidden;
            root.style.borderTopLeftRadius = 10;
            root.style.borderTopRightRadius = 10;
            root.style.borderBottomLeftRadius = 10;
            root.style.borderBottomRightRadius = 10;

            // Viewport
            var viewport = new VisualElement { name = "MapViewport" };
            viewport.style.position = Position.Absolute;
            viewport.style.left = 0;
            viewport.style.top = 0;
            viewport.style.right = 0;
            viewport.style.bottom = 0;
            viewport.style.overflow = Overflow.Hidden;

            // Content (pan/zoom transform applied here)
            // IMPORTANT: do NOT pin right/bottom; PhoneMapPageUI sets explicit width/height.
            var content = new VisualElement { name = "MapContent" };
            content.style.position = Position.Absolute;
            content.style.left = 0;
            content.style.top = 0;
            content.style.right = StyleKeyword.Auto;
            content.style.bottom = StyleKeyword.Auto;

            // Background, polylines, markers
            var bg = new VisualElement { name = "MapBackground" };
            var polys = new VisualElement { name = "MapPolylines" };
            var markers = new VisualElement { name = "MapMarkers" };

            bg.style.position = Position.Absolute;
            polys.style.position = Position.Absolute;
            markers.style.position = Position.Absolute;

            // Match PhoneHUD.uss: only left/top pinned; PhoneMapPageUI sizes these explicitly.
            bg.style.left = polys.style.left = markers.style.left = 0;
            bg.style.top = polys.style.top = markers.style.top = 0;
            bg.style.right = polys.style.right = markers.style.right = StyleKeyword.Auto;
            bg.style.bottom = polys.style.bottom = markers.style.bottom = StyleKeyword.Auto;

            // Reasonable default; PhoneMapPageUI will override to ScaleToFit when binding/refreshing.
            bg.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;

            // Make embedded maps non-interactive by default; instances can override via SetMinimapMode.
            root.pickingMode = PickingMode.Ignore;
            viewport.pickingMode = PickingMode.Ignore;
            content.pickingMode = PickingMode.Ignore;
            bg.pickingMode = PickingMode.Ignore;
            polys.pickingMode = PickingMode.Ignore;
            markers.pickingMode = PickingMode.Ignore;

            content.Add(bg);
            content.Add(polys);
            content.Add(markers);
            viewport.Add(content);
            root.Add(viewport);

            return root;
        }

        private void SetStatsTab(bool showSession, bool force = false)
        {
            if (!force && _statsShowSession == showSession) return;
            _statsShowSession = showSession;

            if (_btnStatsTabSession != null)
                _btnStatsTabSession.EnableInClassList("is-on", showSession);

            if (_btnStatsTabLifetime != null)
                _btnStatsTabLifetime.EnableInClassList("is-on", !showSession);

            if (_panelStatsSession != null)
                _panelStatsSession.EnableInClassList("is-active", showSession);

            if (_panelStatsLifetime != null)
                _panelStatsLifetime.EnableInClassList("is-active", !showSession);
        }

        private void BuildStatsDashboard()
        {
            // Always re-query after bind (in case of re-enable / document rebuild).
            _statsDashSession = _root?.Q<VisualElement>("StatsDash_Session");
            _statsDashLifetime = _root?.Q<VisualElement>("StatsDash_Lifetime");

            _statCardsSession.Clear();
            _statCardsLifetime.Clear();

            _statsDashSession?.Clear();
            _statsDashLifetime?.Clear();

            // Build SESSION if present
            if (_statsDashSession != null)
            {
                int i = 0;

                AddStatRow(true, _statsDashSession, "Top Speed", p => p != null ? $"{p.session.topSpeedMps:0.0} m/s" : "--", i++, kpi: true);
                AddStatRow(true, _statsDashSession, "Avg Speed", p => p != null ? $"{p.session.AverageSpeedMps:0.0} m/s" : "--", i++, kpi: true);
                AddStatRow(true, _statsDashSession, "Distance", p => p != null ? FormatMeters(p.session.distanceMeters) : "--", i++, kpi: true);

                AddStatRow(true, _statsDashSession, "Vertical Distance", p => p != null ? $"+{p.session.verticalAscentMeters:0} / -{p.session.verticalDescentMeters:0}" : "--", i++);
                AddStatRow(true, _statsDashSession, "Air Time", p => p != null ? $"{p.session.airTimeSeconds:0.0}s" : "--", i++);
                AddStatRow(true, _statsDashSession, "Air Distance", p => p != null ? FormatMeters(p.session.airDistanceMeters) : "--", i++);
                AddStatRow(true, _statsDashSession, "Grind Time", p => p != null ? $"{p.session.grindTimeSeconds:0.0}s" : "--", i++);
                AddStatRow(true, _statsDashSession, "Grind Distance", p => p != null ? FormatMeters(p.session.grindDistanceMeters) : "--", i++);

                AddStatRow(true, _statsDashSession, "Runs Completed", p => p != null ? $"{p.session.runsCompleted}" : "--", i++);
                AddStatRow(true, _statsDashSession, "Runs Visited", p => p != null ? $"{(p.sessionVisitedRunIds != null ? p.sessionVisitedRunIds.Count : 0)}" : "--", i++);
                AddStatRow(true, _statsDashSession, "Clean Runs", p => p != null ? $"{p.session.runsCompletedClean}" : "--", i++);
                AddStatRow(true, _statsDashSession, "Top Run Speed", p => p != null ? $"{p.session.topRunSpeedMps:0.0} m/s" : "--", i++);

                AddStatRow(true, _statsDashSession, "Lifts Used", p => p != null ? $"{p.session.liftsUsed}" : "--", i++);
                AddStatRow(true, _statsDashSession, "Stacks", p => p != null ? $"{p.session.stacks}" : "--", i++);
                AddStatRow(true, _statsDashSession, "POIs Visited", p => p != null ? $"{(p.sessionVisitedLandmarkIds != null ? p.sessionVisitedLandmarkIds.Count : 0)}" : "--", i++);
            }

            // Build LIFETIME if present
            if (_statsDashLifetime != null)
            {
                int i = 0;

                AddStatRow(false, _statsDashLifetime, "Top Speed", p => p != null ? $"{p.lifetime.topSpeedMps:0.0} m/s" : "--", i++, kpi: true);
                AddStatRow(false, _statsDashLifetime, "Avg Speed", p => p != null ? $"{p.lifetime.AverageSpeedMps:0.0} m/s" : "--", i++, kpi: true);
                AddStatRow(false, _statsDashLifetime, "Total Distance", p => p != null ? FormatMeters(p.lifetime.totalDistanceMeters) : "--", i++, kpi: true);

                AddStatRow(false, _statsDashLifetime, "Total Vertical Distance", p => p != null ? $"+{p.lifetime.totalVerticalAscentMeters:0} / -{p.lifetime.totalVerticalDescentMeters:0}" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Total Air Time", p => p != null ? $"{p.lifetime.totalAirTimeSeconds:0.0}s" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Total Air Distance", p => p != null ? FormatMeters(p.lifetime.totalAirDistanceMeters) : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Total Grind Time", p => p != null ? $"{p.lifetime.totalGrindTimeSeconds:0.0}s" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Total Grind Distance", p => p != null ? FormatMeters(p.lifetime.totalGrindDistanceMeters) : "--", i++);

                AddStatRow(false, _statsDashLifetime, "Total Runs", p => p != null ? $"{p.lifetime.totalRunsCompleted}" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Total Runs Visited", p => p != null ? $"{(p.visitedRunIds != null ? p.visitedRunIds.Count : 0)}" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Total Clean Runs", p => p != null ? $"{p.lifetime.totalRunsCompletedClean}" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Top Run Speed", p => p != null ? $"{p.lifetime.topRunSpeedMps:0.0} m/s" : "--", i++);

                AddStatRow(false, _statsDashLifetime, "Total Lifts", p => p != null ? $"{p.lifetime.totalLiftsUsed}" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "Total Stacks", p => p != null ? $"{p.lifetime.totalStacks}" : "--", i++);
                AddStatRow(false, _statsDashLifetime, "POIs Visited", p => p != null ? $"{(p.visitedLandmarkIds != null ? p.visitedLandmarkIds.Count : 0)}" : "--", i++);
            }

            // If profile isn't ready yet, rows still exist and will populate once _profile is assigned.
            RefreshDynamicStatRows();
        }

        private void AddStatRow(
            bool isSession,
            VisualElement listRoot,
            string label,
            Func<PlayerStatsProfile, string> valueProvider,
            int index,
            bool kpi = false)
        {
            if (listRoot == null) return;

            var row = new VisualElement();
            row.AddToClassList("stat-row");

            if ((index % 2) == 0) row.AddToClassList("is-even");
            if (kpi) row.AddToClassList("is-kpi");

            var lbl = new Label(label);
            lbl.AddToClassList("stat-row-label");

            var val = new Label("--");
            val.AddToClassList("stat-row-value");

            row.Add(lbl);
            row.Add(val);
            listRoot.Add(row);

            var refs = new StatCardRefs
            {
                value = val,
                valueProvider = valueProvider
            };

            if (isSession) _statCardsSession.Add(refs);
            else _statCardsLifetime.Add(refs);
        }

        private void RefreshDynamicStatRows()
        {
            if (_profile == null) return;

            // Session tab now represents "Day"
            DaySnapshot day = GetDisplayedDaySnapshot();

            for (int i = 0; i < _statCardsSession.Count; i++)
            {
                var c = _statCardsSession[i];
                if (c?.value == null) continue;

                if (c.dayProvider != null)
                    c.value.text = c.dayProvider(day);
                else if (c.valueProvider != null)
                    c.value.text = c.valueProvider(_profile);
            }

            for (int i = 0; i < _statCardsLifetime.Count; i++)
            {
                var c = _statCardsLifetime[i];
                if (c?.value == null || c.valueProvider == null) continue;
                c.value.text = c.valueProvider(_profile);
            }
        }

        private static string FormatMeters(float meters)
        {
            if (meters >= 1000f) return $"{meters / 1000f:0.0}km";
            return $"{meters:0}m";
        }

        private static string FormatMetricValue(ProgressionMetric metric, float value)
        {
            switch (metric)
            {
                case ProgressionMetric.SessionDistanceMeters:
                case ProgressionMetric.LifetimeDistanceMeters:
                case ProgressionMetric.SessionAirDistanceMeters:
                case ProgressionMetric.LifetimeAirDistanceMeters:
                case ProgressionMetric.SessionVerticalDescentMeters:
                case ProgressionMetric.LifetimeVerticalDescentMeters:
                    return FormatMeters(value);

                case ProgressionMetric.SessionTopSpeedMps:
                case ProgressionMetric.LifetimeTopSpeedMps:
                case ProgressionMetric.SessionTopRunSpeedMps:
                case ProgressionMetric.LifetimeTopRunSpeedMps:
                    return $"{value:0.0}m/s";

                case ProgressionMetric.SessionAirTimeSeconds:
                case ProgressionMetric.LifetimeAirTimeSeconds:
                    return $"{value:0.0}s";

                case ProgressionMetric.SessionStacks:
                case ProgressionMetric.LifetimeStacks:
                case ProgressionMetric.SessionRunsCompleted:
                case ProgressionMetric.LifetimeRunsCompleted:
                case ProgressionMetric.SessionLiftsUsed:
                case ProgressionMetric.LifetimeLiftsUsed:
                case ProgressionMetric.SessionRunsVisited:
                case ProgressionMetric.LifetimeRunsVisited:
                case ProgressionMetric.SessionRunsCompletedClean:
                case ProgressionMetric.LifetimeRunsCompletedClean:
                case ProgressionMetric.LifetimeRunVisited:
                case ProgressionMetric.LifetimeRunCompletedCount:
                case ProgressionMetric.LifetimeRunCompletedCleanCount:
                    return $"{Mathf.FloorToInt(value)}";

                default:
                    return $"{value:0.##}";
            }
        }

        private void ShowPhonePage(string pageName)
        {
            foreach (var kv in _phonePages)
            {
                if (kv.Value == null) continue;
                kv.Value.EnableInClassList("is-active", kv.Key == pageName);
            }

            // Title
            if (_pageTitle != null)
                _pageTitle.text = PageTitleFor(pageName);

            // Back button hidden on Home
            bool isHome = pageName == "Page_Home";
            if (_btnBack != null)
                _btnBack.style.display = isHome ? DisplayStyle.None : DisplayStyle.Flex;

            // Currency only on Home / Tasks / Achievements
            bool showCurrency =
             pageName == "Page_Home" ||
             pageName == "Page_Tasks" ||
             pageName == "Page_Achievements" ||
             pageName == "Page_SkiPass";

            if (_navCurrency != null)
            {
                _navCurrency.style.display = showCurrency ? DisplayStyle.Flex : DisplayStyle.None;

                if (showCurrency && _profile != null)
                    _navCurrency.text = $"${_profile.currency:N0}";
            }

            if (pageName == "Page_Map")
            {
                EnsureMapLayerBarAboveViewport();

                _mapPageUI?.Refresh();

                // Reliable: request a one-shot center; PhoneMapPageUI.Tick() will retry until layout is valid.
                _mapPageUI?.RequestCenterOnPlayer(keepZoom: true, minZoom: 1.0f);
            }

            if (pageName == "Page_SkiPass")
            {
                HookSkiPassManager();
                RefreshSkiPassPage(true);
            }

        }

        private void EnsureMapLayerBarAboveViewport()
        {
            var mapPage = Q<ScrollView>("Page_Map");
            if (mapPage == null) return;

            var content = mapPage.contentContainer; // unity-content-container
            if (content == null) return;

            var toolbar = mapPage.Q<VisualElement>(null, "map-toolbar");
            var layerBar = mapPage.Q<VisualElement>("MapLayerBar");
            var missing = mapPage.Q<VisualElement>("Lbl_MapMissing");
            var viewport = mapPage.Q<VisualElement>("MapViewport");

            if (toolbar == null || layerBar == null || viewport == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[PhoneHUD] EnsureMapLayerBarAboveViewport: toolbar={(toolbar != null)} layerBar={(layerBar != null)} viewport={(viewport != null)}");
#endif
                return;
            }

            // Prevent the map content from visually spilling outside the viewport and covering siblings.
            viewport.style.overflow = Overflow.Hidden;

            // Remove any residue that could make the bar behave like an overlay.
            layerBar.style.position = Position.Relative;
            layerBar.style.left = StyleKeyword.Null;
            layerBar.style.right = StyleKeyword.Null;
            layerBar.style.top = StyleKeyword.Null;
            layerBar.style.bottom = StyleKeyword.Null;

            // Enforce exact top-to-bottom order within the content container.
            int idx = 0;

            Reinsert(content, toolbar, ref idx);
            Reinsert(content, layerBar, ref idx);
            if (missing != null) Reinsert(content, missing, ref idx);
            Reinsert(content, viewport, ref idx);

            // Spacing
            layerBar.style.marginTop = 0;
            layerBar.style.marginBottom = 8;

#if UNITY_EDITOR
            Debug.Log($"[PhoneHUD] Map order enforced. toolbarIdx={content.IndexOf(toolbar)} layerBarIdx={content.IndexOf(layerBar)} viewportIdx={content.IndexOf(viewport)}");
#endif
        }

        private static void Reinsert(VisualElement parent, VisualElement child, ref int insertIndex)
        {
            if (child == null || parent == null) return;

            // Only reorder within this container; if child is elsewhere, bring it here.
            if (child.parent != parent)
                child.RemoveFromHierarchy();
            else
                parent.Remove(child);

            insertIndex = Mathf.Clamp(insertIndex, 0, parent.childCount);
            parent.Insert(insertIndex, child);
            insertIndex++;
        }

        private string GetActivePhonePage()
        {
            foreach (var kv in _phonePages)
            {
                if (kv.Value == null) continue;
                if (kv.Value.ClassListContains("is-active"))
                    return kv.Key;
            }
            return null;
        }

        private string PageTitleFor(string pageName)
        {
            return pageName switch
            {
                "Page_Home" => "Home",
                "Page_Stats" => "Stats",
                "Page_Tasks" => "Tasks",
                "Page_Achievements" => "Achievements",
                "Page_SkiPass" => "Ski Pass",
                "Page_TimeWeather" => "Time & Weather",
                "Page_Map" => "Map",
                "Page_Runs" => "Runs",
                "Page_Save" => "Save & Load",
                "Page_Settings" => "Settings",
                "Page_SOS" => "SOS",
                _ => "Menu"
            };
        }

        private void RefreshAllUI(bool force)
        {
            // Keep "Day" stats tracking current even if the Stats page isn't open.
            EnsureLiveDayTracking();

            // If you later want to throttle updates: you can add a timer here.
            RefreshTimeWeather();
            RefreshWatchSpeedGauge();
            RefreshRunTrackerTile();
            RefreshWatchRunTrackerPage();

            // Updates Watch + Home tiles + currency label.
            RefreshTasksAndAchievements();

            // IMPORTANT:
            // RefreshTasksMatrix() clears & rebuilds the visual tree, which can destroy a cell
            // between PointerDown and PointerUp, making clicks feel “random”.
            // So we skip rebuilding for a short window right after user interaction.
            if (Time.unscaledTime >= _suppressTasksMatrixRebuildUntil)
                RefreshTasksMatrix();

            // Home watch page “live” element
            RefreshWatchRunInfo();

            HookSkiPassManager();
            RefreshSkiPassTile();

            if (_phoneOpen && GetActivePhonePage() == "Page_SkiPass")
                RefreshSkiPassPage(false);

            // Live-refresh the Stats page while open
            if (_phoneOpen && GetActivePhonePage() == "Page_Stats")
            {
                TickStatsDayCalendar();
                RefreshDynamicStatRows();
            }

        }

        private void RefreshWatchHomeTopBar(string timeStr, string weatherStr)
        {
            if (_wHomeTime != null) _wHomeTime.text = timeStr;
            if (_wHomeWeather != null) _wHomeWeather.text = weatherStr;
        }

        private void RefreshWatchSpeedGauge()
        {
            if (_wSpeed == null) return;

            // Ensure we have an RB to read speed from.
            if (_playerRb == null)
            {
                if (_skiController != null)
                    _playerRb = _skiController.GetComponent<Rigidbody>();

                if (_playerRb == null)
                {
                    var tracker = FindObjectOfType<PlayerStatsTracker>();
                    if (tracker != null) _playerRb = tracker.GetComponent<Rigidbody>();
                }
            }

            float speedMps = (_playerRb != null) ? _playerRb.linearVelocity.magnitude : 0f;
            _wSpeed.text = $"{speedMps:0.0}";
        }

        private void RefreshWatchRunInfo()
        {
            if (_wRunName == null && _wRunTime == null) return;

            // Ensure we have a tracker (same logic as the phone Run Tracker tile).
            if (_runProgressTracker == null)
            {
                if (_skiController != null)
                    _runProgressTracker = _skiController.GetComponent<RunProgressTracker>();

                if (_runProgressTracker == null)
                    _runProgressTracker = FindObjectOfType<RunProgressTracker>();
            }

            // Avoid CS0170: ensure 'p' is definitely assigned on all paths.
            RunProgressTracker.ActiveRunProgress p = default;
            bool onRun = false;

            if (_runProgressTracker != null)
                onRun = _runProgressTracker.TryGetActiveProgress(out p);

            if (onRun)
            {
                // Add % covered without changing the UI structure:
                // append it to the run name line (keeps run name above timer exactly as requested).
                int pct = Mathf.Clamp(Mathf.RoundToInt(p.completion01 * 100f), 0, 100);

                if (_wRunName != null) _wRunName.text = $"{p.runName}  {pct}%";
                if (_wRunTime != null) _wRunTime.text = FormatRunTileTime(p.elapsedSeconds);
            }
            else
            {
                if (_wRunName != null) _wRunName.text = "Not on a run";
                if (_wRunTime != null) _wRunTime.text = "--:--";
            }

            // Reuse existing style hook: brighten when actively on a run.
            if (_wRunBox != null)
                _wRunBox.EnableInClassList("is-active-air", onRun);
        }

        private static string FormatHoursToClock(float hours)
        {
            if (hours < 0f) return "--:--";
            int h = Mathf.FloorToInt(hours) % 24;
            int m = Mathf.Clamp(Mathf.RoundToInt((hours - Mathf.Floor(hours)) * 60f), 0, 59);
            return $"{h:00}:{m:00}";
        }
        private static string FormatHoursToClock12(float hours)
        {
            if (hours < 0f) return "--:--";

            int h = Mathf.FloorToInt(hours) % 24;
            int m = Mathf.Clamp(Mathf.RoundToInt((hours - Mathf.Floor(hours)) * 60f), 0, 59);
            return FormatTime12(h, m);
        }

        private static string FormatTime12(int hours24, int minutes)
        {
            hours24 = ((hours24 % 24) + 24) % 24;
            minutes = Mathf.Clamp(minutes, 0, 59);

            bool pm = hours24 >= 12;
            int h12 = hours24 % 12;
            if (h12 == 0) h12 = 12;

            return $"{h12:0}:{minutes:00} {(pm ? "PM" : "AM")}";
        }
        private static string FormatHoursToClockBySetting(float hours, bool use12)
        {
            return use12 ? FormatHoursToClock12(hours) : FormatHoursToClock(hours);
        }

        private static string FormatHourLabel(int hour24, bool use12)
        {
            hour24 = ((hour24 % 24) + 24) % 24;
            if (!use12) return $"{hour24:00}:00";

            bool pm = hour24 >= 12;
            int h12 = hour24 % 12;
            if (h12 == 0) h12 = 12;
            return $"{h12:0} {(pm ? "PM" : "AM")}";
        }

        private void UpdateTimeWeatherHourlyCapacity()
        {
            if (_twHourlyScroll == null) return;

            float w = _twHourlyScroll.resolvedStyle.width;
            if (w <= 1f) return;

            // How many fixed-width cards fit in the available width?
            int fit = Mathf.FloorToInt((w + TW_CARD_GAP) / (TW_CARD_WIDTH + TW_CARD_GAP));
            _twMaxVisibleHours = Mathf.Clamp(fit, 1, 6);
        }

        private void RefreshTimeWeather()
        {
            // Use the existing in-game setting; default to 12-hour.
            bool use12 = _time == null || _time.twelveHourTime;

            // TIME (simple header)
            int hh = _time != null ? _time.timeHours : 0;
            int mm = _time != null ? Mathf.Clamp((int)_time.timeMinutes, 0, 59) : 0;

            string timeStr = use12 ? FormatTime12(hh, mm) : $"{hh:00}:{mm:00}";
            string dayOfWeek = _time != null ? _time.currentDay.ToString() : "---";
            string month = _time != null && _time.currentMonthData != null ? _time.currentMonthData.month : "Month";
            int dom = _time != null ? _time.dayOfMonth : 0;
            int year = _time != null ? _time.currentYear : 0;
            string dateStr = year > 0 ? $"{dom} {month} {year}" : $"{dom} {month}";

            if (_twTime != null) _twTime.text = timeStr;
            if (_twDayOfWeek != null) _twDayOfWeek.text = dayOfWeek;
            if (_twDate != null) _twDate.text = dateStr;

            // WEATHER (current)
            string tempStr = "--\u00B0C";
            string condStr = "---";
            float rainChance = -1f;

            if (_weather != null)
            {
                tempStr = $"{_weather.temperature:0}\u00B0C";
                condStr = _weather.currentWeatherPreset != null ? _weather.currentWeatherPreset.weatherCondition : "Weather";
                rainChance = _weather.rainChance;
            }

            // Sunrise / Sunset (also shown on the Home tile)
            string sunriseStr = _time != null ? FormatHoursToClockBySetting(_time.sunriseTime, use12) : "--:--";
            string sunsetStr = _time != null ? FormatHoursToClockBySetting(_time.sunsetTime, use12) : "--:--";

            if (_tileSunTimes != null)
                _tileSunTimes.text = $"Sunrise: {sunriseStr}  |  Sunset: {sunsetStr}";

            // Wind (authoritative display value comes from WindController)
            float windSpeed = -1f;
            if (_wind != null)
            {
                Vector3 samplePos = _playerRb != null ? _playerRb.position : _wind.transform.position;
                windSpeed = _wind.SampleWind(samplePos).steady.magnitude;
            }
            else if (_weather != null)
            {
                // Fallback if the WindController isn't in the scene.
                windSpeed = _weather.windSpeed;
            }

            // Watch Home top bar (compact)
            string watchCond = _weather != null && _weather.currentWeatherPreset != null ? _weather.currentWeatherPreset.weatherCondition : "--";
            string watchTemp = _weather != null ? $"{_weather.temperature:0}\u00B0" : "--";
            string watchWeatherStr = $"{watchCond} {watchTemp}".Trim();
            RefreshWatchHomeTopBar(timeStr, watchWeatherStr);

            // Home tile values
            if (_tileTimeValue != null) _tileTimeValue.text = timeStr;
            if (_tileDateValue != null) _tileDateValue.text = dayOfWeek;
            if (_tileTempValue != null) _tileTempValue.text = tempStr;
            if (_tileWeatherValue != null) _tileWeatherValue.text = condStr;
            if (_tileRainValue != null) _tileRainValue.text = rainChance >= 0f ? $"Rain: {rainChance:0}%" : "Rain: --%";

            // Current conditions card
            if (_twCurrentTemp != null) _twCurrentTemp.text = tempStr;
            if (_twCurrentCond != null) _twCurrentCond.text = condStr;
            if (_twCurrentRain != null) _twCurrentRain.text = rainChance >= 0f ? $"Rain chance: {rainChance:0}%" : "Rain chance: --%";
            if (_twCurrentWind != null) _twCurrentWind.text = windSpeed >= 0f ? $"Wind: {windSpeed:0.0} m/s" : "Wind: --";
            if (_twSunrise != null) _twSunrise.text = sunriseStr;
            if (_twSunset != null) _twSunset.text = sunsetStr;

            // Hourly forecast
            EnsureTimeWeatherHourlyCards();
            // If we had to prepend earlier hours (near midnight), snap scroll so "now" is still the first visible card.
            if (_twHourlySnapToNowPending && _twHourlyScroll != null)
            {
                int currentHour = _time != null ? Mathf.Clamp(_time.timeHours, 0, 23) : 0;
                int start = _twHourlyBuiltStartHour >= 0 ? _twHourlyBuiltStartHour : currentHour;

                float step = (TW_CARD_WIDTH + TW_CARD_GAP);
                float x = Mathf.Max(0f, (currentHour - start) * step);

                _twHourlyScroll.scrollOffset = new Vector2(x, 0f);
                _twHourlySnapToNowPending = false;
            }

            int sunriseHour = _time != null ? Mathf.FloorToInt(_time.sunriseTime) % 24 : -1;
            int sunsetHour = _time != null ? Mathf.FloorToInt(_time.sunsetTime) % 24 : -1;

            var hourly = _weather != null ? _weather.hourlyWeather : null;

            // Map entries by absolute hour-of-day using HourlyWeather.forcastTime
            TimeWeather.HourlyWeather[] hourlyByHour = null;
            if (hourly != null && hourly.Count > 0)
            {
                hourlyByHour = new TimeWeather.HourlyWeather[24];
                for (int k = 0; k < hourly.Count; k++)
                {
                    var hw = hourly[k];
                    if (hw == null) continue;

                    int t = ((hw.forcastTime % 24) + 24) % 24;
                    hourlyByHour[t] = hw;
                }
            }

            for (int i = 0; i < _twHourCards.Count; i++)
            {
                var c = _twHourCards[i];
                if (c == null || c.root == null) continue;

                int hour = (c.root.userData is int h24) ? h24 : i;
                hour = ((hour % 24) + 24) % 24;

                string hourLabel = FormatHourLabel(hour, use12);
                string hourCond = "---";
                float hourRain = -1f;
                bool hourRaining = false;

                var h = (hourlyByHour != null) ? hourlyByHour[hour] : null;
                if (h != null)
                {
                    hourCond = string.IsNullOrEmpty(h.weatherCondition) ? "---" : h.weatherCondition;
                    hourRain = h.rainChance;
                    hourRaining = h.isRaining;
                }

                if (c.hour != null) c.hour.text = hourLabel;
                if (c.cond != null) c.cond.text = hourCond;

                if (c.rain != null)
                    c.rain.text = hourRain >= 0f ? $"{hourRain:0}%\nrain chance" : "--%\nrain chance";

                // Current hour emphasis
                bool isNow = (hour == hh);
                if (isNow) c.root.AddToClassList("is-now");
                else c.root.RemoveFromClassList("is-now");

                // Optional: keep "is-raining" class if you want additional styling hooks
                if (hourRaining || (hourRain >= 60f)) c.root.AddToClassList("is-raining");
                else c.root.RemoveFromClassList("is-raining");

                // Sunrise / Sunset marker (left side of bottom row)
                if (c.sunMarker != null)
                {
                    if (hour == sunriseHour)
                    {
                        c.sunMarker.style.display = DisplayStyle.Flex;
                        c.sunMarker.text = $"Sunrise";
                    }
                    else if (hour == sunsetHour)
                    {
                        c.sunMarker.style.display = DisplayStyle.Flex;
                        c.sunMarker.text = $"Sunset";
                    }
                    else
                    {
                        c.sunMarker.style.display = DisplayStyle.None;
                        c.sunMarker.text = string.Empty;
                    }
                }

                // Background colour rules:
                // - Default: same as hero panel background (rgba(255,255,255,0.06))
                // - Rainy: vibrant deep royal blue (increasing with rain chance)
                // - Current hour still emphasized via .is-now border
                float rc = Mathf.Clamp(hourRain < 0f ? 0f : hourRain, 0f, 100f);
                float t = Mathf.Clamp01(rc / 100f);

                // Base matches hero panel background
                Color baseCol = new Color(1f, 1f, 1f, 0.06f);

                // Vibrant deep royal blue (tinted but readable)
                Color rainyCol = new Color(0.10f, 0.20f, 0.95f, 0.30f);

                // Interpolate by rain chance
                Color bg = Color.Lerp(baseCol, rainyCol, t);

                // Slight lift for current hour (subtle; primary emphasis is border class)
                if (isNow)
                {
                    bg.a = Mathf.Clamp(bg.a + 0.04f, 0f, 1f);
                }

                c.root.style.backgroundColor = new StyleColor(bg);

            }
        }

        private void RefreshRunTrackerTile()
        {
            if (_runTrackerLine1 == null && _runTrackerLine2 == null && _runTrackerLine3 == null && _runTrackerFill == null)
                return;

            EnsureRunProgressTracker();

            // 1) Active run (highest priority)
            if (_runProgressTracker != null && _runProgressTracker.TryGetActiveProgress(out RunProgressTracker.ActiveRunProgress p))
            {
                if (_runTrackerLine1 != null) _runTrackerLine1.text = p.runName;

                float covered01 = Mathf.Clamp01(Mathf.Abs(p.currentFraction01 - p.entryFraction01));
                if (_runTrackerLine3 != null) _runTrackerLine3.text = $"Progress {(covered01 * 100f):0}%";

                if (_runTrackerLine2 != null)
                    _runTrackerLine2.text = $"Time {FormatRunTileTime(p.elapsedSeconds)} • Max {p.topSpeedMps:0.0} m/s • Stacks {p.stacks}";

                SetRangeFill(_runTrackerFill, p.entryFraction01, p.currentFraction01, minVisiblePercent: 0.8f);
                ApplyRunExitHighlight(_tileRunTracker, highlight01: -1f);
                return;
            }

            // 2) Last attempt (after exiting run, before starting a new one)
            if (_runProgressTracker != null && _runProgressTracker.TryGetLastAttemptSummary(out var last))
            {
                float covered01 = last.CoveredFraction01;

                if (_runTrackerLine1 != null) _runTrackerLine1.text = last.runName;
                if (_runTrackerLine3 != null) _runTrackerLine3.text = $"Last {(covered01 * 100f):0}%";

                if (_runTrackerLine2 != null)
                    _runTrackerLine2.text = $"Time {FormatRunTileTime(last.elapsedSeconds)} • Max {last.topSpeedMps:0.0} m/s • Stacks {last.stacks}";

                SetRangeFill(_runTrackerFill, last.entryFraction01, last.exitFraction01, minVisiblePercent: 0.8f);

                float highlight01 = GetExitHighlight01(last.isCompletion, covered01);
                ApplyRunExitHighlight(_tileRunTracker, highlight01);
                return;
            }

            // 3) Nothing
            if (_runTrackerLine1 != null) _runTrackerLine1.text = "Not on a run";
            if (_runTrackerLine2 != null) _runTrackerLine2.text = $"Completed today: {GetRunsCompletedTodayCount()}";
            if (_runTrackerLine3 != null) _runTrackerLine3.text = "--%";

            SetRangeFill(_runTrackerFill, 0f, 0f, minVisiblePercent: 0f);
            ApplyRunExitHighlight(_tileRunTracker, highlight01: -1f);
        }

        private static string FormatRunTileTime(float seconds)
        {
            if (seconds <= 0.01f) return "--:--";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1.0) return $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}";
            return $"{ts.Minutes:00}:{ts.Seconds:00}";
        }

        private void RefreshTasksAndAchievements()
        {
            if (_profile == null)
                return;

            // -------------------------
            // Tasks (preview + progress) - DAILY MATRIX
            // -------------------------
            _dailyTierBuffer.Clear();
            if (_progression != null)
                _progression.GetDailyTierDisplays(_dailyTierBuffer);

            int total = _dailyTierBuffer.Count;
            int completed = 0;
            float overallPct = 0f;

            for (int i = 0; i < total; i++)
            {
                var d = _dailyTierBuffer[i];
                if (d.completed) completed++;
                overallPct += Mathf.Clamp01(d.pct01);
            }
            if (total > 0) overallPct /= total;

            // Build metric-level lines (one per daily metric), showing "next target" per metric.
            // Also compute "claimable metrics" as X/Y where Y is metrics tracked today.
            var order = new List<(string ladderId, ProgressionMetric metric)>();
            var seen = new HashSet<(string ladderId, ProgressionMetric metric)>();

            var bestByRow = new Dictionary<(string ladderId, ProgressionMetric metric), ProgressionDirector.DailyTierDisplay>();
            var anyClaimableByRow = new HashSet<(string ladderId, ProgressionMetric metric)>();

            for (int i = 0; i < total; i++)
            {
                var d = _dailyTierBuffer[i];
                var key = (d.ladderId, d.metric);

                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    order.Add(key);
                }

                if (d.completed && !d.claimed)
                    anyClaimableByRow.Add(key);

                // Pick the "next actionable tier" for this metric:
                // 1) Prefer completed-but-unclaimed (claimable) tiers (lowest target)
                // 2) Else prefer first not-completed tiers (lowest target)
                // 3) Else keep whatever we have (metric fully done)
                if (!bestByRow.TryGetValue(key, out var cur))
                {
                    bestByRow[key] = d;
                    continue;
                }

                bool curClaimable = cur.completed && !cur.claimed;
                bool dClaimable = d.completed && !d.claimed;

                if (dClaimable && !curClaimable)
                {
                    bestByRow[key] = d;
                }
                else if (dClaimable && curClaimable)
                {
                    if (d.target < cur.target) bestByRow[key] = d;
                }
                else if (!curClaimable)
                {
                    bool curIncomplete = !cur.completed;
                    bool dIncomplete = !d.completed;

                    if (dIncomplete && !curIncomplete)
                    {
                        bestByRow[key] = d;
                    }
                    else if (dIncomplete && curIncomplete)
                    {
                        if (d.target < cur.target) bestByRow[key] = d;
                    }
                }
            }

            int metricsTrackedToday = order.Count;
            int claimableMetrics = anyClaimableByRow.Count;

            // Aggregate tiers per metric so we can label Completed vs Ready-to-Claim accurately.
            var totalsByRow = new Dictionary<(string ladderId, ProgressionMetric metric), (int total, int completed, int claimed, bool anyClaimable)>(64);
            for (int i = 0; i < total; i++)
            {
                var d = _dailyTierBuffer[i];
                var key = (d.ladderId, d.metric);

                if (!totalsByRow.TryGetValue(key, out var agg))
                    agg = (0, 0, 0, false);

                agg.total++;
                if (d.completed) agg.completed++;
                if (d.claimed) agg.claimed++;
                if (d.completed && !d.claimed) agg.anyClaimable = true;

                totalsByRow[key] = agg;
            }

            // Render lines: one per metric (compact + status-driven)
            var metricLines = new List<string>(metricsTrackedToday);

            for (int i = 0; i < metricsTrackedToday; i++)
            {
                var key = order[i];
                var d = bestByRow[key];

                string name = GetDailyMetricDisplayName(d.ladderId, d.metric);

                totalsByRow.TryGetValue(key, out var agg);
                bool anyClaimable = agg.anyClaimable;
                bool allTargetsReached = (agg.total > 0) && (agg.completed >= agg.total) && !anyClaimable;

                string right;
                if (anyClaimable)
                {
                    right = "Ready to Claim";
                }
                else if (allTargetsReached)
                {
                    right = "Completed";
                }
                else
                {
                    // Next target progress (x/xx)
                    string progText = FormatMetricValue(d.metric, d.progress);
                    string targetText = FormatMetricValue(d.metric, d.target);
                    right = $"{progText}/{targetText}";
                }

                metricLines.Add($"{name}: {right}");
            }

            // Watch Tasks page: same stacked bars as the phone Tasks tile
            if (_watchTasksList != null)
            {
                // Reuse the same bar list we build for the phone tile (created below).
                // If you prefer fewer bars on watch later, we can clamp metricsTrackedToday here.
            }

            // Home Tasks tile: stacked progress bars with centered labels
            if (_tileTasksList != null)
            {
                // Build stacked progress bars (one per daily metric row)
                var bars = new List<TaskBarRow>(metricsTrackedToday);

                for (int i = 0; i < metricsTrackedToday; i++)
                {
                    var key = order[i];
                    var d = bestByRow[key];

                    totalsByRow.TryGetValue(key, out var agg);
                    bool anyClaimable = agg.anyClaimable;
                    bool allTargetsReached = (agg.total > 0) && (agg.completed >= agg.total) && !anyClaimable;

                    string name = GetDailyMetricDisplayName(d.ladderId, d.metric);

                    string right;
                    float pct = Mathf.Clamp01(d.pct01);

                    if (anyClaimable)
                    {
                        right = "Ready to Claim";
                        pct = 1f;
                    }
                    else if (allTargetsReached)
                    {
                        right = "Completed";
                        pct = 1f;
                    }
                    else
                    {
                        string progText = FormatMetricValue(d.metric, d.progress);
                        string targetText = FormatMetricValue(d.metric, d.target);
                        right = $"{progText}/{targetText}";
                    }

                    bars.Add(new TaskBarRow
                    {
                        label = $"{name}  {right}",
                        pct01 = pct,
                        claimable = anyClaimable
                    });
                }

                // Render into watch + phone tile with identical visuals
                if (_watchTasksList != null)
                    SetDynamicTaskBars(_watchTasksList, bars);

                if (_tileTasksList != null)
                    SetDynamicTaskBars(_tileTasksList, bars);

            }

            // Keep nav currency fresh when visible (e.g., after claiming rewards)
            if (_navCurrency != null && _navCurrency.style.display != DisplayStyle.None)
                _navCurrency.text = $"${_profile.currency:N0}";
        }

        private void SetDynamicLabelList(VisualElement root, List<string> lines, string[] classNames)
        {
            if (root == null) return;

            // Ensure we have enough Label children
            while (root.childCount < lines.Count)
            {
                var lbl = new Label();
                if (classNames != null)
                {
                    for (int i = 0; i < classNames.Length; i++)
                        lbl.AddToClassList(classNames[i]);
                }
                root.Add(lbl);
            }

            // Update labels
            for (int i = 0; i < root.childCount; i++)
            {
                var lbl = root.ElementAt(i) as Label;
                if (lbl == null) continue;

                if (i < lines.Count && !string.IsNullOrEmpty(lines[i]))
                {
                    lbl.style.display = DisplayStyle.Flex;
                    lbl.text = lines[i];
                }
                else
                {
                    lbl.style.display = DisplayStyle.None;
                    lbl.text = "";
                }
            }
        }

        private struct TaskBarRow
        {
            public string label;
            public float pct01;
            public bool claimable;
        }

        private void SetDynamicTaskBars(VisualElement root, List<TaskBarRow> rows)
        {
            if (root == null) return;

            // Ensure enough children
            while (root.childCount < rows.Count)
            {
                var bar = new VisualElement();
                bar.AddToClassList("taskbar");

                var fill = new VisualElement();
                fill.AddToClassList("taskbar-fill");
                bar.Add(fill);

                var lbl = new Label();
                lbl.AddToClassList("taskbar-label");
                bar.Add(lbl);

                root.Add(bar);
            }

            // Update
            for (int i = 0; i < root.childCount; i++)
            {
                var bar = root.ElementAt(i);
                if (bar == null) continue;

                bool active = i < rows.Count;
                bar.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                if (!active) continue;

                var r = rows[i];

                // claimable border
                bar.EnableInClassList("is-claimable", r.claimable);

                // children: [0] fill, [1] label
                var fill = bar.childCount > 0 ? bar[0] : null;
                var lbl = bar.childCount > 1 ? bar[1] as Label : null;

                float pct = Mathf.Clamp01(r.pct01);

                if (fill != null)
                {
                    fill.style.width = Length.Percent(pct * 100f);

                    // Yellow -> Green ramp
                    // start: warm yellow (high visibility)
                    // end: green (completion)
                    Color c0 = new Color(1.00f, 0.85f, 0.20f, 0.70f);
                    Color c1 = Color.forestGreen;
                    fill.style.backgroundColor = Color.Lerp(c0, c1, pct);
                }

                if (lbl != null)
                {
                    lbl.text = r.label ?? "";
                }
            }
        }

        private void TriggerRespawn()
        {
            // Placeholder hook.
            Debug.Log("[PhoneHUD] SOS Respawn requested (hook this into your respawn system).");
        }

        private T Q<T>(string name) where T : VisualElement
        {
            return _root?.Q<T>(name);
        }

        private void WireTile(VisualElement tile, Action onClick)
        {
            if (tile == null || onClick == null) return;

            tile.RegisterCallback<ClickEvent>(_ => onClick());
        }


        private void EnsureTimeWeatherHourlyCards(bool forceRebuild = false)
        {
            if (_twHourlyRow == null) return;

            int currentHour = _time != null ? Mathf.Clamp(_time.timeHours, 0, 23) : 0;

            // Primary intent: show current hour onward to midnight.
            int remaining = 24 - currentHour;

            // If we’re near midnight and there aren’t enough hours to visually “fill”,
            // prepend earlier hours to make up the difference (but we will snap scroll so “now” is first visible).
            int targetVisible = Mathf.Clamp(_twMaxVisibleHours, 1, 24);
            int backfill = Mathf.Max(0, targetVisible - remaining);
            int builtStart = Mathf.Max(0, currentHour - backfill);

            // Always build through midnight (end of day).
            int count = 24 - builtStart;

            bool needsRebuild =
                forceRebuild ||
                _twHourlyBuiltStartHour != builtStart ||
                _twHourCards.Count != count ||
                (_twHourCards.Count > 0 && (_twHourCards[0] == null || _twHourCards[0].root == null || _twHourCards[0].root.parent != _twHourlyRow));

            if (!needsRebuild) return;

            _twHourlyBuiltStartHour = builtStart;
            _twHourlySnapToNowPending = true;

            _twHourlyRow.Clear();
            _twHourCards.Clear();

            for (int hour24 = builtStart; hour24 < 24; hour24++)
            {
                var root = new VisualElement();
                root.AddToClassList("tw-hour");
                root.userData = hour24;

                // Vertical stack (no absolute positioning)
                var topRow = new VisualElement();
                topRow.AddToClassList("tw-hour-top");

                var hourLabel = new Label("--");
                hourLabel.AddToClassList("tw-hour-time");

                var sunMarker = new Label(string.Empty);
                sunMarker.AddToClassList("tw-hour-sun");
                sunMarker.style.display = DisplayStyle.None;

                topRow.Add(hourLabel);
                topRow.Add(sunMarker);

                var condLabel = new Label("---");
                condLabel.AddToClassList("tw-hour-cond");

                var bottomRow = new VisualElement();
                bottomRow.AddToClassList("tw-hour-bottom");

                var rainLabel = new Label("--%\nrain chance");
                rainLabel.AddToClassList("tw-hour-rain");

                bottomRow.Add(rainLabel);

                root.Add(topRow);
                root.Add(condLabel);
                root.Add(bottomRow);

                _twHourlyRow.Add(root);

                _twHourCards.Add(new HourCardRefs
                {
                    root = root,
                    hour = hourLabel,
                    cond = condLabel,
                    rain = rainLabel,
                    rainFill = null,
                    sunMarker = sunMarker
                });
            }
        }

        private void OnClickClaimAllDaily()
        {
            if (_progression == null || _profile == null) return;

            _profile.Sanitize();
            var daily = _profile.dailyTasks;
            if (daily == null || daily.rows == null) return;

            // Snapshot which tiers were already claimed BEFORE claiming-all.
            _claimAllClaimedBefore.Clear();
            for (int r = 0; r < daily.rows.Count; r++)
            {
                var row = daily.rows[r];
                if (row == null || row.tiers == null) continue;

                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier == null) continue;

                    if (tier.claimed)
                        _claimAllClaimedBefore.Add($"{row.ladderId}:{t}");
                }
            }

            int total = _progression.ClaimAllDailyCompleted(out _);

            if (total <= 0)
            {
                // Nothing to claim; just refresh visuals.
                RefreshTasksMatrix();
                UpdateTasksInfoPanel();
                return;
            }

            // Build summary of what was newly claimed on this click.
            _claimAllSummaryLines.Clear();
            int sumTierRewards = 0;

            for (int r = 0; r < daily.rows.Count; r++)
            {
                var row = daily.rows[r];
                if (row == null || row.tiers == null) continue;

                string metricName = GetDailyMetricDisplayName(row);

                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier == null) continue;

                    string key = $"{row.ladderId}:{t}";
                    bool wasClaimedBefore = _claimAllClaimedBefore.Contains(key);

                    if (tier.claimed && !wasClaimedBefore)
                    {
                        int rw = Mathf.Max(0, tier.lastRewardGranted);
                        sumTierRewards += rw;

                        string targetText = FormatMetricValue(row.metric, tier.target);
                        _claimAllSummaryLines.Add($"{metricName} - {targetText}: +{rw}");
                    }
                }
            }

            // If the "total" includes row bonuses, show the remainder as bonus.
            int bonus = total - sumTierRewards;
            if (bonus > 0)
                _claimAllSummaryLines.Add($"Row bonus: +{bonus}");

            // Put a total at the top.
            _claimAllSummaryLines.Insert(0, $"Total claimed: +{total}");

            // Show this summary in the info panel until user selects something else.
            _showClaimAllSummary = true;

            // Clear selection so the panel doesn't immediately snap back to a selected tier.
            _selectedDailyRow = -1;
            _selectedDailyTier = -1;
            _selectedDailyLadderId = null;
            _selectedDailyTierIndex = -1;

            // Refresh visuals + panel.
            RefreshAllUI(true);
        }

        // --- Daily Task Matrix: robust cell click wiring (prevents ScrollView drag capture issues) ---
        private void WireDailyTaskCellClickable(
     VisualElement cell,
     int rowIndex,
     int tierIndex,
     Action<int, int> onClick)
        {
            if (cell == null) return;

            // Make sure THIS element is pickable
            cell.pickingMode = PickingMode.Position;
            cell.focusable = true;

            // Ensure children don't steal the click (labels often become the picked target)
            for (int i = 0; i < cell.childCount; i++)
                cell[i].pickingMode = PickingMode.Ignore;

            // Avoid double-wiring when UI refreshes
            const string wiredTag = "__dailyTaskCellWired";
            if (cell.userData is string s && s == wiredTag)
                return;

            cell.userData = wiredTag;

            // Handle on PointerDown so we don’t rely on PointerUp (which can be missed if UI rebuilds)
            cell.RegisterCallback<PointerDownEvent>(evt =>
            {
                // Left-click / primary touch only
                if (evt.button != 0) return;

                // Suppress matrix rebuild briefly to avoid destroying elements mid-interaction
                _suppressTasksMatrixRebuildUntil = Time.unscaledTime + 0.20f;

                evt.StopImmediatePropagation();
                evt.StopPropagation();

                onClick?.Invoke(rowIndex, tierIndex);
            }, TrickleDown.TrickleDown);
        }

        private void RefreshTasksMatrix()
        {
            if (_tasksMatrix == null) return;

            _tasksMatrix.Clear();

            if (_profile == null || _progression == null)
            {
                SetTasksInfoText("No profile / progression available.");
                return;
            }

            _profile.Sanitize();

            // Claim-all UI
            int claimable = _progression.GetDailyClaimableCurrency(out _);
            if (_tasksClaimAllBtn != null)
                _tasksClaimAllBtn.style.display = claimable > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (_tasksClaimAllValue != null)
            {
                _tasksClaimAllValue.text = claimable.ToString();
                _tasksClaimAllValue.style.display = claimable > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var daily = _profile.dailyTasks;
            if (daily == null || daily.rows == null || daily.rows.Count == 0)
            {
                SetTasksInfoText("No daily tasks active.");
                return;
            }

            // Build flattened buffer for selected/closest logic
            _progression.GetDailyTierDisplays(_dailyTierBuffer);

            // Render rows
            for (int r = 0; r < daily.rows.Count; r++)
            {
                var row = daily.rows[r];
                if (row == null || row.tiers == null) continue;

                var rowRoot = new VisualElement();
                rowRoot.AddToClassList("tasks-metric-row");

                var metricLabel = new Label(GetDailyMetricDisplayName(row));
                metricLabel.AddToClassList("tasks-metric-label");
                rowRoot.Add(metricLabel);

                var tierRow = new VisualElement();
                tierRow.AddToClassList("tasks-tier-row");

                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier == null) continue;

                    var cell = new VisualElement();
                    cell.AddToClassList("task-cell");

                    bool selected = (row.ladderId == _selectedDailyLadderId && t == _selectedDailyTierIndex);
                    if (selected) cell.AddToClassList("is-selected");

                    if (tier.completed) cell.AddToClassList("is-complete");
                    // Ready-to-claim = completed but not claimed
                    if (tier.completed && !tier.claimed) cell.AddToClassList("is-ready");
                    // Claimed state (keeps green fill; border becomes faded yellow)
                    if (tier.claimed) cell.AddToClassList("is-claimed");

                    // Recent-claim flash (brief pulse to make the reward moment obvious)
                    if (_lastClaimLadderId == row.ladderId &&
                        _lastClaimTierIndex == t &&
                        (Time.unscaledTime - _lastClaimTimeUnscaled) < 0.60f)
                    {
                        cell.AddToClassList("is-claim-flash");
                    }

                    string targetText = FormatMetricValue(row.metric, tier.target);
                    var lbl = new Label(targetText);
                    lbl.AddToClassList("task-cell-label");
                    cell.Add(lbl);

                    // Yellow -> Green based on completion percentage
                    float pct = tier.target > 0f ? Mathf.Clamp01(tier.lastProgress / tier.target) : 0f;

                    Color start = new Color(1f, 0.85f, 0.12f, 0.22f);
                    Color end = new Color(0.25f, 1f, 0.45f, 0.22f);
                    Color bg = Color.Lerp(start, end, pct);

                    if (tier.completed) bg = new Color(0.25f, 1f, 0.45f, 0.28f);
                    // Do NOT wash out claimed tiers; claimed should remain green.
                    // Claimed state is represented via USS border styling (is-claimed) instead.


                    cell.style.backgroundColor = bg;

                    int rowIndex = r;
                    int tierIndex = t;
                    WireDailyTaskCellClickable(cell, rowIndex, tierIndex, OnDailyTaskCellClicked);

                    tierRow.Add(cell);
                }

                rowRoot.Add(tierRow);
                _tasksMatrix.Add(rowRoot);
            }

            UpdateTasksInfoPanel();
        }

        // -------------------------------------------------
        // Daily Tasks: selection + click-to-inspect/claim
        // -------------------------------------------------

        private int _selectedDailyRow = -1;
        private int _selectedDailyTier = -1;

        private void OnDailyTaskCellClicked(int rowIndex, int tierIndex)
        {
            _showClaimAllSummary = false;

            // Evaluate claimability BEFORE we refresh / rebuild any UI.
            bool claimable = IsDailyTierClaimable(rowIndex, tierIndex);

            // Set selection immediately.
            _selectedDailyRow = rowIndex;
            _selectedDailyTier = tierIndex;

            // Sync the "ladder selection" fields that your info panel + highlight logic uses.
            if (_profile != null &&
                _profile.dailyTasks != null &&
                _profile.dailyTasks.rows != null &&
                rowIndex >= 0 && rowIndex < _profile.dailyTasks.rows.Count)
            {
                var row = _profile.dailyTasks.rows[rowIndex];
                if (row != null)
                {
                    _selectedDailyLadderId = row.ladderId;
                    _selectedDailyTierIndex = tierIndex;
                }
                else
                {
                    _selectedDailyLadderId = null;
                    _selectedDailyTierIndex = -1;
                }
            }
            else
            {
                _selectedDailyLadderId = null;
                _selectedDailyTierIndex = -1;
            }

            // If claimable, claim immediately on this SAME click.
            if (claimable)
            {
                // PointerDown sets a short suppression window to prevent rebuild-during-click issues.
                // But for a claim, we *want* an immediate rebuild so the user sees the result right away.
                _suppressTasksMatrixRebuildUntil = 0f;

                TryClaimDailyTier(rowIndex, tierIndex);

                // Ensure selection + info are visible immediately even if claim fails (or refresh is delayed).
                RefreshTasksMatrix();
                UpdateTasksInfoPanel();
                return;
            }

            // Not claimable: just inspect/select (single click).
            RefreshTasksMatrix();
            UpdateTasksInfoPanel();
        }

        private readonly List<string> _dailyNextTierLines = new();

        private void BuildDailyNextTierLines(int maxLines)
        {
            _dailyNextTierLines.Clear();

            if (_profile == null) return;
            var daily = _profile.dailyTasks;
            if (daily == null || daily.rows == null) return;

            for (int r = 0; r < daily.rows.Count && _dailyNextTierLines.Count < maxLines; r++)
            {
                var row = daily.rows[r];
                if (row == null || row.tiers == null || row.tiers.Count == 0)
                    continue;

                // "Next tier" = first tier not yet completed; if all completed -> "Complete"
                int nextTierIndex = -1;
                for (int t = 0; t < row.tiers.Count; t++)
                {
                    var tier = row.tiers[t];
                    if (tier == null) continue;

                    if (!tier.completed)
                    {
                        nextTierIndex = t;
                        break;
                    }
                }

                string name = GetDailyMetricDisplayName(row);
                string targetText;

                if (nextTierIndex >= 0)
                {
                    float target = row.tiers[nextTierIndex].target;
                    targetText = FormatMetricValue(row.metric, target);
                }
                else
                {
                    targetText = "Complete";
                }

                _dailyNextTierLines.Add($"{name}: {targetText}");
            }
        }

        // -------------------------
        // Stats (Day / Weeks calendar)
        // -------------------------

        private void BuildStatsDayDashboard()
        {
            _statCardsDay.Clear();
            _statsDashDay?.Clear();
            if (_statsDashDay == null)
                return;

            int i = 0;

            AddDayStatRow(_statsDashDay, "Top Speed", s => $"{s.topSpeedMps:0.0} m/s", i++, kpi: true);
            AddDayStatRow(_statsDashDay, "Avg Speed", s => $"{s.avgSpeedMps:0.0} m/s", i++, kpi: true);
            AddDayStatRow(_statsDashDay, "Distance", s => FormatMeters(s.distanceMeters), i++, kpi: true);

            AddDayStatRow(_statsDashDay, "Vertical Distance", s => $"+{s.verticalAscentMeters:0} / -{s.verticalDescentMeters:0}", i++);
            AddDayStatRow(_statsDashDay, "Air Time", s => $"{s.airTimeSeconds:0.0}s", i++);
            AddDayStatRow(_statsDashDay, "Air Distance", s => FormatMeters(s.airDistanceMeters), i++);
            AddDayStatRow(_statsDashDay, "Grind Time", s => $"{s.grindTimeSeconds:0.0}s", i++);
            AddDayStatRow(_statsDashDay, "Grind Distance", s => FormatMeters(s.grindDistanceMeters), i++);

            AddDayStatRow(_statsDashDay, "Runs Completed", s => $"{s.runsCompleted}", i++);
            AddDayStatRow(_statsDashDay, "Runs Visited", s => $"{s.runsVisited}", i++);
            AddDayStatRow(_statsDashDay, "Clean Runs", s => $"{s.runsCompletedClean}", i++);
            AddDayStatRow(_statsDashDay, "Top Run Speed", s => $"{s.topRunSpeedMps:0.0} m/s", i++);
            AddDayStatRow(_statsDashDay, "Lifts Used", s => $"{s.liftsUsed}", i++);
            AddDayStatRow(_statsDashDay, "Stacks", s => $"{s.stacks}", i++);
            AddDayStatRow(_statsDashDay, "Landmarks Visited", s => $"{s.landmarksVisited}", i++);

            RefreshDayStatRows();
        }

        private void AddDayStatRow(
     VisualElement listRoot,
     string label,
     Func<DaySnapshot, string> dayProvider,
     int index,
     bool kpi = false)
        {
            if (listRoot == null) return;

            var row = new VisualElement();
            row.AddToClassList("stat-row");
            if ((index % 2) == 0) row.AddToClassList("is-even");
            if (kpi) row.AddToClassList("is-kpi");

            var lbl = new Label(label);
            lbl.AddToClassList("stat-row-label");

            var val = new Label("--");
            val.AddToClassList("stat-row-value");

            row.Add(lbl);
            row.Add(val);
            listRoot.Add(row);

            _statCardsSession.Add(new StatCardRefs
            {
                value = val,
                dayProvider = dayProvider,
                valueProvider = null
            });
        }

        private void RefreshDayStatRows()
        {
            var day = _statsActiveDay ?? GetTodayKey();

            if (!TryGetDaySnapshot(day, out var snap))
                snap = default;

            for (int i = 0; i < _statCardsDay.Count; i++)
            {
                var c = _statCardsDay[i];
                if (c.value == null || c.dayProvider == null) continue;
                c.value.text = c.dayProvider(snap) ?? "--";
            }
        }


        private int CountActiveDaysInWeek(int weekIndex)
        {
            if (_time == null) return 0;

            int startAbs = ToAbsoluteDayOfYear(weekIndex * 7);
            int count = 0;

            for (int i = 0; i < 7; i++)
            {
                int absDoy = startAbs + i;
                if (absDoy < _statsBaseDayOfYear) continue;
                if (absDoy > _time.dayCount) continue;

                if (!TryConvertDayOfYear(absDoy, out int m, out int d))
                    continue;

                var k = new DayKey(_time.currentYear, m, d);
                if (TryGetDaySnapshot(k, out var s) && HasAnyStats(s))
                    count++;
            }

            return count;
        }

        private bool HasAnyStats(DayStatsSnapshot s)
        {
            // “Active day” heuristic: any meaningful movement/activity
            if (s.distanceMeters > 5f) return true;
            if (s.runsCompleted > 0) return true;
            if (s.liftsUsed > 0) return true;
            if (s.airTimeSeconds > 0.1f) return true;
            if (s.grindTimeSeconds > 0.1f) return true;
            return false;
        }

        private VisualElement BuildStatsDayTile(DayKey day, bool enabled)
        {
            var tile = new VisualElement();
            tile.AddToClassList("runs-day-tile");

            var dayNum = new Label($"{day.dayOfMonth}");
            dayNum.AddToClassList("runs-day-num");

            var month = new Label(_time != null && _time.monthPresets != null && day.monthIndex >= 0 && day.monthIndex < _time.monthPresets.Length
                ? _time.monthPresets[day.monthIndex].month
                : "");
            month.AddToClassList("runs-day-mon");

            tile.Add(dayNum);
            tile.Add(month);

            bool isToday = false;
            if (_time != null)
            {
                var today = GetTodayKey();
                isToday = day.year == today.year && day.monthIndex == today.monthIndex && day.dayOfMonth == today.dayOfMonth;
            }

            bool hasStats = TryGetDaySnapshot(day, out var snap) && HasAnyStats(snap);

            tile.EnableInClassList("is-today", isToday);
            tile.EnableInClassList("has-runs", hasStats); // reuse Runs styling
            SetEnabledAndVisual(tile, enabled);

            if (enabled)
            {
                tile.RegisterCallback<ClickEvent>(_ =>
                {
                    OpenStatsDayDetail(day);
                });
            }

            return tile;
        }

        private string FormatDayLabel(DayKey day)
        {
            if (_time == null || _time.monthPresets == null) return "Day";
            string mon = (day.monthIndex >= 0 && day.monthIndex < _time.monthPresets.Length) ? _time.monthPresets[day.monthIndex].month : "Month";
            return $"{mon} {day.dayOfMonth}";
        }

        private static void SetEnabledAndVisual(VisualElement ve, bool enabled)
        {
            if (ve == null) return;
            ve.SetEnabled(enabled);
            ve.EnableInClassList("is-disabled", !enabled);
        }

        // -------------------------
        // Daily archive load/save
        // -------------------------

        private void LoadDailyStatsArchive()
        {
            _dailyArchiveLoaded = true;

            _dailyArchiveMap.Clear();
            _dailyArchive = new DayStatsArchive();

            string json = PlayerPrefs.GetString(Pref_StatsDailyArchive, "");
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var parsed = JsonUtility.FromJson<DayStatsArchive>(json);
                if (parsed != null && parsed.days != null)
                    _dailyArchive = parsed;
            }
            catch { /* ignore */ }

            for (int i = 0; i < _dailyArchive.days.Count; i++)
            {
                var r = _dailyArchive.days[i];
                long k = MakeArchiveKey(r.year, r.monthIndex, r.dayOfMonth);
                _dailyArchiveMap[k] = r.stats;
            }
        }

        private void SaveDailyStatsArchive()
        {
            if (!_dailyArchiveLoaded) return;

            _dailyArchive.days.Clear();
            foreach (var kv in _dailyArchiveMap)
            {
                UnpackArchiveKey(kv.Key, out int y, out int m, out int d);
                _dailyArchive.days.Add(new DayStatsRecord
                {
                    year = y,
                    monthIndex = m,
                    dayOfMonth = d,
                    stats = kv.Value
                });
            }

            string json = JsonUtility.ToJson(_dailyArchive);
            PlayerPrefs.SetString(Pref_StatsDailyArchive, json);
            PlayerPrefs.Save();
        }

        // -------------------------
        // Base day + snapshot retrieval
        // -------------------------

        private void EnsureStatsBaseDayLoaded()
        {
            if (_statsBaseLoaded) return;
            if (_time == null) return;

            if (!PlayerPrefs.HasKey(Pref_StatsBaseDay))
            {
                _statsBaseDayOfYear = _time.dayCount;
                PlayerPrefs.SetInt(Pref_StatsBaseDay, _statsBaseDayOfYear);
                PlayerPrefs.Save();
            }
            else
            {
                _statsBaseDayOfYear = PlayerPrefs.GetInt(Pref_StatsBaseDay, _time.dayCount);
            }

            _statsBaseDayOfYear = Mathf.Clamp(_statsBaseDayOfYear, 0, _time.dayCount);

            // If archive contains earlier days (same year), shift base earlier so they are reachable.
            if (_dailyArchiveLoaded && _dailyArchive != null && _dailyArchive.days != null && _dailyArchive.days.Count > 0)
            {
                int min = int.MaxValue;
                for (int i = 0; i < _dailyArchive.days.Count; i++)
                {
                    var r = _dailyArchive.days[i];
                    if (r.year != _time.currentYear) continue;

                    var k = new DayKey(r.year, r.monthIndex, r.dayOfMonth);
                    int doy = DayKeyToDayOfYear(k);
                    if (doy >= 0 && doy < min) min = doy;
                }

                if (min != int.MaxValue && min < _statsBaseDayOfYear)
                {
                    _statsBaseDayOfYear = min;
                    PlayerPrefs.SetInt(Pref_StatsBaseDay, _statsBaseDayOfYear);
                    PlayerPrefs.Save();
                }
            }

            _statsBaseLoaded = true;
        }

        private int GetRelativeDayCount()
        {
            if (_time == null) return 0;
            return Mathf.Max(0, _time.dayCount - _statsBaseDayOfYear);
        }

        private int ToAbsoluteDayOfYear(int relativeDay)
        {
            return _statsBaseDayOfYear + Mathf.Max(0, relativeDay);
        }

        private bool IsFutureDay(DayKey day)
        {
            if (_time == null) return false;
            int doy = DayKeyToDayOfYear(day);
            return doy > _time.dayCount;
        }

        private bool IsBeforeBaseDay(DayKey day)
        {
            int doy = DayKeyToDayOfYear(day);
            return doy < _statsBaseDayOfYear;
        }

        private bool TryGetDaySnapshot(DayKey day, out DayStatsSnapshot snap)
        {
            // Today should always be live (even if empty) so the "Day" tab behaves predictably.
            var today = GetTodayKey();
            if (_liveDayKeyValid && day.year == today.year && day.monthIndex == today.monthIndex && day.dayOfMonth == today.dayOfMonth)
            {
                snap = ComputeLiveDaySnapshot();
                return true;
            }

            long key = MakeArchiveKey(day.year, day.monthIndex, day.dayOfMonth);
            if (_dailyArchiveMap.TryGetValue(key, out snap))
                return true;

            snap = default;
            return false;
        }

        private void SetArchivedDaySnapshot(DayKey day, DayStatsSnapshot snap)
        {
            long key = MakeArchiveKey(day.year, day.monthIndex, day.dayOfMonth);
            _dailyArchiveMap[key] = snap;
            _statsWeeksDirty = true;
        }

        private static long MakeArchiveKey(int year, int monthIndex, int dayOfMonth)
        {
            unchecked
            {
                long k = (long)year & 0xFFFFFFFFL;
                k = (k << 16) ^ (uint)(ushort)monthIndex;
                k = (k << 16) ^ (uint)(ushort)dayOfMonth;
                return k;
            }
        }

        private static void UnpackArchiveKey(long key, out int year, out int monthIndex, out int dayOfMonth)
        {
            dayOfMonth = (int)(key & 0xFFFF);
            monthIndex = (int)((key >> 16) & 0xFFFF);
            year = (int)((key >> 32) & 0xFFFFFFFF);
        }

        // -------------------------
        // Daily stats live tracking (turn "session" into "day")
        // -------------------------

        private void EnsureLiveDayTracking()
        {
            if (_profile == null || _time == null) return;
            if (!_statsBaseLoaded) EnsureStatsBaseDayLoaded();

            var today = GetTodayKey();

            if (!_liveDayKeyValid)
            {
                StartLiveDay(today);
                return;
            }

            bool changed = today.year != _liveDayKey.year || today.monthIndex != _liveDayKey.monthIndex || today.dayOfMonth != _liveDayKey.dayOfMonth;
            if (changed)
            {
                // Finalize previous day into archive
                var prevSnap = ComputeLiveDaySnapshot();
                SetArchivedDaySnapshot(_liveDayKey, prevSnap);
                SaveDailyStatsArchive();

                // Start new day tracking
                StartLiveDay(today);
            }

            // Update max speed continuously (cheap)
            if (_playerRb != null)
                _liveDayMaxSpeedMps = Mathf.Max(_liveDayMaxSpeedMps, _playerRb.linearVelocity.magnitude);
        }

        private void StartLiveDay(DayKey day)
        {
            _liveDayKey = day;
            _liveDayKeyValid = true;
            _liveDaySessionBaseline = ReadSessionSnapshot(_profile);
            _liveDayStartUnscaled = Time.unscaledTime;
            _liveDayMaxSpeedMps = 0f;
            _liveDayMaxRunSpeedMps = 0f;
            _statsWeeksDirty = true;
        }

        private void PersistLiveDayToArchive()
        {
            if (!_liveDayKeyValid) return;
            var snap = ComputeLiveDaySnapshot();
            SetArchivedDaySnapshot(_liveDayKey, snap);
            SaveDailyStatsArchive();
        }

        private DayStatsSnapshot ComputeLiveDaySnapshot()
        {
            if (_profile == null) return default;

            var cur = ReadSessionSnapshot(_profile);
            var baseSnap = _liveDaySessionBaseline;

            float elapsed = Mathf.Max(1f, Time.unscaledTime - _liveDayStartUnscaled);

            float dist = Mathf.Max(0f, cur.distanceMeters - baseSnap.distanceMeters);
            float avg = dist / elapsed;

            int runsVisited = Mathf.Max(0, cur.runsVisited - baseSnap.runsVisited);
            int poisVisited = Mathf.Max(0, cur.landmarksVisited - baseSnap.landmarksVisited);

            return new DayStatsSnapshot
            {
                topSpeedMps = Mathf.Max(_liveDayMaxSpeedMps, 0f),
                avgSpeedMps = avg,
                distanceMeters = dist,

                verticalAscentMeters = Mathf.Max(0f, cur.verticalAscentMeters - baseSnap.verticalAscentMeters),
                verticalDescentMeters = Mathf.Max(0f, cur.verticalDescentMeters - baseSnap.verticalDescentMeters),

                airTimeSeconds = Mathf.Max(0f, cur.airTimeSeconds - baseSnap.airTimeSeconds),
                airDistanceMeters = Mathf.Max(0f, cur.airDistanceMeters - baseSnap.airDistanceMeters),

                grindTimeSeconds = Mathf.Max(0f, cur.grindTimeSeconds - baseSnap.grindTimeSeconds),
                grindDistanceMeters = Mathf.Max(0f, cur.grindDistanceMeters - baseSnap.grindDistanceMeters),

                runsCompleted = Mathf.Max(0, cur.runsCompleted - baseSnap.runsCompleted),
                runsCompletedClean = Mathf.Max(0, cur.runsCompletedClean - baseSnap.runsCompletedClean),

                topRunSpeedMps = _liveDayMaxRunSpeedMps,
                liftsUsed = Mathf.Max(0, cur.liftsUsed - baseSnap.liftsUsed),
                stacks = Mathf.Max(0, cur.stacks - baseSnap.stacks),

                runsVisited = runsVisited,
                landmarksVisited = poisVisited,
            };
        }

        private SessionSnapshot ReadSessionSnapshot(PlayerStatsProfile p)
        {
            if (p == null) return default;

            return new SessionSnapshot
            {
                topSpeedMps = p.session.topSpeedMps,
                avgSpeedMps = p.session.AverageSpeedMps,
                distanceMeters = p.session.distanceMeters,
                verticalAscentMeters = p.session.verticalAscentMeters,
                verticalDescentMeters = p.session.verticalDescentMeters,
                airTimeSeconds = p.session.airTimeSeconds,
                airDistanceMeters = p.session.airDistanceMeters,
                grindTimeSeconds = p.session.grindTimeSeconds,
                grindDistanceMeters = p.session.grindDistanceMeters,
                runsCompleted = p.session.runsCompleted,
                runsCompletedClean = p.session.runsCompletedClean,
                topRunSpeedMps = p.session.topRunSpeedMps,
                liftsUsed = p.session.liftsUsed,
                stacks = p.session.stacks,
                runsVisited = p.sessionVisitedRunIds != null ? p.sessionVisitedRunIds.Count : 0,
                landmarksVisited = p.sessionVisitedLandmarkIds != null ? p.sessionVisitedLandmarkIds.Count : 0,
            };
        }

        // -------------------------
        // Calendar conversion helpers (copied from RunsPageUI, kept local)
        // -------------------------

      
        private int DayKeyToDayOfYear(DayKey day)
        {
            if (_time == null || _time.monthPresets == null) return 0;

            int doy = 0;
            for (int i = 0; i < day.monthIndex; i++)
                doy += Mathf.Max(1, _time.monthPresets[i].daysInMonth);

            doy += Mathf.Clamp(day.dayOfMonth - 1, 0, 9999);
            return doy;
        }

        
        // -------------------------
        // Daily Tasks UI helpers
        // -------------------------

        private bool IsDailyTierClaimable(PlayerStatsProfile.DailyTaskTierState tier)
        {
            return tier != null && tier.completed && !tier.claimed;
        }

        private bool IsDailyTierClaimable(int rowIndex, int tierIndex)
        {
            if (_profile == null) return false;
            var daily = _profile.dailyTasks;
            if (daily == null || daily.rows == null) return false;
            if (rowIndex < 0 || rowIndex >= daily.rows.Count) return false;

            var row = daily.rows[rowIndex];
            if (row == null || row.tiers == null) return false;
            if (tierIndex < 0 || tierIndex >= row.tiers.Count) return false;

            return IsDailyTierClaimable(row.tiers[tierIndex]);
        }

        private void TryClaimDailyTier(int rowIndex, int tierIndex)
        {
            if (_progression == null || _profile == null) return;

            var daily = _profile.dailyTasks;
            if (daily == null || daily.rows == null) return;
            if (rowIndex < 0 || rowIndex >= daily.rows.Count) return;

            var row = daily.rows[rowIndex];
            if (row == null || row.tiers == null) return;
            if (tierIndex < 0 || tierIndex >= row.tiers.Count) return;

            var tier = row.tiers[tierIndex];
            if (tier == null) return;

            // Only claim if completed and not already claimed.
            if (!tier.completed || tier.claimed)
                return;

            // Use the REAL signature you already have in ProgressionDirector.
            if (_progression.TryClaimDailyTier(row.ladderId, tierIndex,
        out int rewardGranted, out bool rowBonusGranted, out int rowBonusAmount))
            {
                // Record "claim happened" so the matrix can flash that cell and the info panel can toast it.
                _lastClaimLadderId = row.ladderId;
                _lastClaimTierIndex = tierIndex;
                _lastClaimRewardGranted = rewardGranted;
                _lastClaimRowBonusGranted = rowBonusGranted;
                _lastClaimRowBonusAmount = rowBonusAmount;
                _lastClaimTimeUnscaled = Time.unscaledTime;

                // Full refresh so currency + tiles + badges update consistently.
                RefreshAllUI(force: true);
            }

        }

        private void RefreshDailyTaskInfoPanel()
        {
            // This routes to your real info-panel renderer
            UpdateTasksInfoPanel();
        }

        private void RefreshDailyTasksUI()
        {
            // This routes to your real matrix renderer
            RefreshTasksMatrix();
        }

        private void UpdateTasksInfoPanel()
        {
            if (_tasksInfoBody == null) return;

            // If Claim All was clicked, keep showing that summary until user selects something else.
            if (_showClaimAllSummary)
            {
                if (_tasksInfoTitle != null) _tasksInfoTitle.text = "Claim All Results";
                if (_tasksInfoBody != null) _tasksInfoBody.text = string.Join("\n", _claimAllSummaryLines);
                return;
            }

            // Selected tier details
            if (!string.IsNullOrEmpty(_selectedDailyLadderId) && _selectedDailyTierIndex >= 0)
            {
                bool found = false;
                for (int i = 0; i < _dailyTierBuffer.Count; i++)
                {
                    var d = _dailyTierBuffer[i];
                    if (d.ladderId != _selectedDailyLadderId) continue;
                    if (d.tierIndex != _selectedDailyTierIndex) continue;

                    found = true;

                    string metricName = GetFriendlyMetricName(d.metric);
                    if (TryGetSelectedDailyTier(out var selRow, out _, out _))
                        metricName = GetDailyMetricDisplayName(selRow);
                    string status = d.claimed ? "Claimed"
                        : d.completed ? "Ready to claim"
                        : "In progress";

                    string targetText = FormatMetricValue(d.metric, d.target);
                    int pctInt = Mathf.RoundToInt(d.pct01 * 100f);

                    // Title becomes: "Distance — 5km"
                    if (_tasksInfoTitle != null)
                        _tasksInfoTitle.text = $"{metricName} | {targetText}";

                    // Compact, more readable body (no extra UI elements, just better hierarchy)
                    string bar = BuildProgressBar(d.pct01, 14);
                    string progressLine = $"{bar}  {pctInt}%";
                    string numbersLine = $"{FormatMetricValue(d.metric, d.progress)} / {targetText}";

                    // Keep status + reward tight and scannable
                    string statusLine = status;
                    if (d.reward > 0) statusLine += $"   |   Reward: {d.reward}";

                    bool recentClaimToast =
                        (_selectedDailyLadderId == _lastClaimLadderId) &&
                        (_selectedDailyTierIndex == _lastClaimTierIndex) &&
                        ((Time.unscaledTime - _lastClaimTimeUnscaled) < 1.20f);

                    if (recentClaimToast)
                    {
                        // Minimal, clear reward acknowledgement without new UI elements.
                        string claimLine = _lastClaimRewardGranted > 0
                            ? $"CLAIMED  +{_lastClaimRewardGranted}"
                            : "CLAIMED";

                        if (_lastClaimRowBonusGranted && _lastClaimRowBonusAmount > 0)
                            claimLine += $"   •   BONUS +{_lastClaimRowBonusAmount}";

                        _tasksInfoBody.text = $"{claimLine}\n{progressLine}\n{numbersLine}\n{statusLine}";
                    }
                    else
                    {
                        _tasksInfoBody.text = $"{progressLine}\n{numbersLine}\n{statusLine}";
                    }

                    break;
                }

                if (!found)
                {
                    _selectedDailyLadderId = null;
                    _selectedDailyTierIndex = -1;
                }

                return;
            }

            // No selection: show closest tiers to completion (top 3)
            int shown = 0;
            if (_tasksInfoTitle != null)
                _tasksInfoTitle.text = "Tasks";

            string txt = "Closest tasks:\n";

            // Simple top-k without LINQ to keep allocations low
            for (int k = 0; k < 3; k++)
            {
                int bestIndex = -1;
                float bestPct = -1f;

                for (int i = 0; i < _dailyTierBuffer.Count; i++)
                {
                    var d = _dailyTierBuffer[i];
                    if (d.completed) continue;

                    // Avoid picking the same item multiple times
                    bool alreadyPicked = false;
                    if (bestIndex >= 0 && bestIndex == i) alreadyPicked = true;
                    if (alreadyPicked) continue;

                    if (d.pct01 > bestPct)
                    {
                        bestPct = d.pct01;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0) break;

                var best = _dailyTierBuffer[bestIndex];
                txt += $"- {GetDailyMetricDisplayName(best.ladderId, best.metric)} {FormatMetricValue(best.metric, best.progress)} / {FormatMetricValue(best.metric, best.target)} ({Mathf.RoundToInt(best.pct01 * 100f)}%)\n";
                shown++;
            }

            if (shown == 0)
                txt = "All tasks completed (or no active tiers).";

            _tasksInfoBody.text = txt;
        }

        private void SetTasksInfoText(string text)
        {
            if (_tasksInfoBody != null) _tasksInfoBody.text = text ?? string.Empty;
        }

        private static string BuildProgressBar(float pct01, int width)
        {
            pct01 = Mathf.Clamp01(pct01);
            int filled = Mathf.RoundToInt(pct01 * width);

            // Use solid blocks + light blocks for a quick “at a glance” bar.
            // (No extra UI elements required.)
            System.Text.StringBuilder sb = new System.Text.StringBuilder(width);
            for (int i = 0; i < width; i++)
                sb.Append(i < filled ? '█' : '░');

            return sb.ToString();
        }

        private string GetDailyMetricDisplayName(PlayerStatsProfile.DailyTaskRowState row)
        {
            if (row == null) return "--";

            // Prefer author-defined override from the ladder definition (not the runtime row state).
            if (_progression != null &&
                _progression.TryGetDailyLadderDefinition(row.ladderId, out var ladder) &&
                ladder != null &&
                !string.IsNullOrWhiteSpace(ladder.displayNameOverride))
            {
                return ladder.displayNameOverride;
            }

            // Otherwise fall back to friendly enum mapping.
            return GetFriendlyMetricName(row.metric);
        }

        // Convenience overload for flattened display buffer (uses ladderId + metric)
        private string GetDailyMetricDisplayName(string ladderId, ProgressionMetric metric)
        {
            if (_progression != null &&
                _progression.TryGetDailyLadderDefinition(ladderId, out var ladder) &&
                ladder != null &&
                !string.IsNullOrWhiteSpace(ladder.displayNameOverride))
            {
                return ladder.displayNameOverride;
            }

            return GetFriendlyMetricName(metric);
        }

        private bool TryGetSelectedDailyTier(out PlayerStatsProfile.DailyTaskRowState row, out PlayerStatsProfile.DailyTaskTierState tier, out int rowIndex)
        {
            row = null;
            tier = null;
            rowIndex = -1;

            if (_profile == null) return false;
            var daily = _profile.dailyTasks;
            if (daily == null || daily.rows == null) return false;
            if (string.IsNullOrEmpty(_selectedDailyLadderId) || _selectedDailyTierIndex < 0) return false;

            for (int r = 0; r < daily.rows.Count; r++)
            {
                var rr = daily.rows[r];
                if (rr == null) continue;
                if (rr.ladderId != _selectedDailyLadderId) continue;

                if (rr.tiers == null) return false;
                if (_selectedDailyTierIndex < 0 || _selectedDailyTierIndex >= rr.tiers.Count) return false;

                var tt = rr.tiers[_selectedDailyTierIndex];
                if (tt == null) return false;

                row = rr;
                tier = tt;
                rowIndex = r;
                return true;
            }

            return false;
        }

        private void TryClaimSelectedDailyTier()
        {
            if (_progression == null || _profile == null) return;

            if (!TryGetSelectedDailyTier(out var row, out var tier, out _))
                return;

            // Only claim if completed and not already claimed.
            if (!tier.completed || tier.claimed)
                return;

            // Updated signature (reward + optional row-bonus).
            if (_progression.TryClaimDailyTier(row.ladderId, _selectedDailyTierIndex,
                    out int rewardGranted, out bool rowBonusGranted, out int rowBonusAmount))
            {
                // Keep selection but refresh visuals + claimable totals.
                RefreshAllUI(force: true);
            }
        }

        private string GetFriendlyMetricName(ProgressionMetric metric)
        {
            // Keep this minimal and readable; expand as needed.
            switch (metric)
            {
                case ProgressionMetric.SessionDistanceMeters: return "Travel Distance";
                case ProgressionMetric.SessionTopSpeedMps: return "Top Speed";
                case ProgressionMetric.SessionAirTimeSeconds: return "Air Time";
                case ProgressionMetric.SessionAirDistanceMeters: return "Air Distance";
                case ProgressionMetric.SessionGrindTimeSeconds: return "Grind Time";
                case ProgressionMetric.SessionGrindDistanceMeters: return "Grind Distance";
                case ProgressionMetric.SessionRunsCompleted: return "Runs Completed";
                case ProgressionMetric.SessionLiftsUsed: return "Lifts Used";
                case ProgressionMetric.SessionPlacesVisited: return "Places Visited";

                case ProgressionMetric.LifetimeDistanceMeters: return "Distance";
                case ProgressionMetric.LifetimeTopSpeedMps: return "Top Speed";
                case ProgressionMetric.LifetimeAirTimeSeconds: return "Air Time";
                case ProgressionMetric.LifetimeAirDistanceMeters: return "Air Distance";
                case ProgressionMetric.LifetimeGrindTimeSeconds: return "Grind Time";
                case ProgressionMetric.LifetimeGrindDistanceMeters: return "Grind Distance";
                case ProgressionMetric.LifetimeRunsCompleted: return "Runs Completed";
                case ProgressionMetric.LifetimeLiftsUsed: return "Lifts Ridden";
                case ProgressionMetric.LifetimePlacesVisited: return "Places Visited";

                case ProgressionMetric.SessionRunsVisited: return "Runs Visited";
                case ProgressionMetric.SessionRunsCompletedClean: return "Clean Runs Completed";
                case ProgressionMetric.SessionTopRunSpeedMps: return "Top Run Speed";

                case ProgressionMetric.LifetimeRunsVisited: return "Runs Visited";
                case ProgressionMetric.LifetimeRunsCompletedClean: return "Clean Runs";
                case ProgressionMetric.LifetimeTopRunSpeedMps: return "Top Run Speed";

                case ProgressionMetric.LifetimeRunVisited: return "Visited Runs";
                case ProgressionMetric.LifetimeRunCompletedCount: return "Completed Runs";
                case ProgressionMetric.LifetimeRunCompletedCleanCount: return "Clean Completed Runs";


                default: return metric.ToString();
            }
        }

        private void BuildMapInfoPanel()
        {
            var mapPage = Q<ScrollView>("Page_Map");
            if (mapPage == null) return;

            // Always enforce correct placement (layer bar above viewport; info panel docked).
            // This is important because older versions may have reparented MapLayerBar into the bottom dock.
            EnforceMapLayerBarAboveViewport(mapPage);
            EnforceMapBottomDock(mapPage);

            // If we already built the info panel, just ensure it's docked correctly and exit.
            if (_mapInfoPanel != null && _mapInfoPanel.parent != null)
            {
                DockInfoPanelToBottom();
                return;
            }


            _mapInfoPanel = new VisualElement { name = "MapInfoPanel" };

            // IMPORTANT:
            // This used to be an absolute bottom overlay.
            // We now want it to be a normal layout element that lives UNDER the layer bar.
            _mapInfoPanel.style.position = Position.Relative;

            // Layout spacing (acts like previous left/right/bottom inset, but in flow layout)
            _mapInfoPanel.style.marginLeft = 8;
            _mapInfoPanel.style.marginRight = 8;
            _mapInfoPanel.style.marginTop = 6;
            _mapInfoPanel.style.marginBottom = 8;

            // Visual styling (keep your existing look)
            _mapInfoPanel.style.paddingLeft = 10;
            _mapInfoPanel.style.paddingRight = 10;
            _mapInfoPanel.style.paddingTop = 8;
            _mapInfoPanel.style.paddingBottom = 10;
            _mapInfoPanel.style.borderTopLeftRadius = 12;
            _mapInfoPanel.style.borderTopRightRadius = 12;
            _mapInfoPanel.style.borderBottomLeftRadius = 12;
            _mapInfoPanel.style.borderBottomRightRadius = 12;
            _mapInfoPanel.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.9f));
            _mapInfoPanel.style.display = DisplayStyle.None;

            // Header row
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            _mapInfoTitle = new Label("Selection");
            _mapInfoTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _mapInfoTitle.style.flexGrow = 1;
            _mapInfoTitle.style.color = Color.white;

            _mapInfoCloseBtn = new Button(() => HideMapInfoPanel()) { text = "×" };
            _mapInfoCloseBtn.AddToClassList("map-info-close");

            _mapInfoCloseBtn.style.width = 28;
            _mapInfoCloseBtn.style.height = 24;

            header.Add(_mapInfoTitle);
            header.Add(_mapInfoCloseBtn);

            _mapInfoBody = new Label();
            _mapInfoBody.style.whiteSpace = WhiteSpace.Normal;
            _mapInfoBody.style.marginTop = 6;
            _mapInfoBody.style.color = Color.white;

            _mapInfoPanel.Add(header);
            _mapInfoPanel.Add(_mapInfoBody);

            // Actions row (contextual)
            _mapInfoActionsRow = new VisualElement();
            _mapInfoActionsRow.style.flexDirection = FlexDirection.Row;
            _mapInfoActionsRow.style.marginTop = 8;
            _mapInfoActionsRow.style.display = DisplayStyle.None;

            _mapInfoViewRunBtn = new Button(() =>
            {
                if (string.IsNullOrEmpty(_mapInfoActiveRunId)) return;
                NavigateTo("Page_Runs");
                _runsPageUI?.RequestFocusRun(_mapInfoActiveRunId);
            })
            { text = "View Run History" };

            _mapInfoActionsRow.Add(_mapInfoViewRunBtn);
            _mapInfoPanel.Add(_mapInfoActionsRow);

            // Disable legacy run-history UI elements (kept as fields elsewhere, but not used anymore).
            _mapInfoActiveRunId = null;
            _mapInfoActiveAttemptIndex = -1;
            if (_mapAttemptBox != null) _mapAttemptBox.style.display = DisplayStyle.None;
            if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;

            // ----------------------------
            // Ensure the layer bar sits ABOVE the viewport (between header and viewport)
            // ----------------------------
            {
                var layerBar = mapPage.Q<VisualElement>("MapLayerBar");
                var mapViewportEl = mapPage.Q<VisualElement>("MapViewport");

                if (layerBar != null && mapViewportEl != null)
                {
                    var desiredParent = mapViewportEl.parent; // wherever the viewport lives (likely Page_Map content)
                    if (desiredParent != null)
                    {
                        // Put the layer bar immediately before the viewport.
                        int viewportIndex = desiredParent.IndexOf(mapViewportEl);

                        // If it's not already in the right place, move it.
                        if (layerBar.parent != desiredParent || desiredParent.IndexOf(layerBar) != viewportIndex)
                        {
                            layerBar.RemoveFromHierarchy();
                            desiredParent.Insert(Mathf.Max(0, viewportIndex), layerBar);
                        }

                        // Tighten spacing since it now lives in-flow above the viewport.
                        layerBar.style.marginTop = 0;
                        layerBar.style.marginBottom = 6;
                    }
                }
            }

            // ----------------------------
            // Dock (legend bar + info panel) to bottom of the scroll viewport
            // ----------------------------
            var scrollViewport = mapPage.Q<VisualElement>("unity-content-viewport");
            if (scrollViewport == null)
            {
                // Fallback: if Unity changes internals, keep old behavior.
                mapPage.Add(_mapInfoPanel);
                return;
            }

            if (_mapBottomDock == null)
            {
                _mapBottomDock = new VisualElement { name = "MapBottomDock" };
                _mapBottomDock.style.position = Position.Absolute;
                _mapBottomDock.style.left = 0;
                _mapBottomDock.style.right = 0;
                _mapBottomDock.style.bottom = 0;

                _mapBottomDock.style.flexDirection = FlexDirection.Column;

                // padding should belong to the panel now; keep dock itself minimal
                _mapBottomDock.style.paddingLeft = 0;
                _mapBottomDock.style.paddingRight = 0;
                _mapBottomDock.style.paddingTop = 0;
                _mapBottomDock.style.paddingBottom = 0;

                // Optional: remove dock backing so only the panel has a background
                _mapBottomDock.style.backgroundColor = StyleKeyword.Null;
            }

            // Ensure dock is attached to the scroll viewport (so it doesn't scroll away)
            if (_mapBottomDock.parent != scrollViewport)
            {
                _mapBottomDock.RemoveFromHierarchy();
                scrollViewport.Add(_mapBottomDock);
            }

            DockInfoPanelToBottom();
        }

        private void ShowMapInfo(string title, string body)
        {
            if (_mapInfoPanel == null) return;

            if (_mapInfoTitle != null) _mapInfoTitle.text = title ?? "Selection";
            if (_mapInfoBody != null) _mapInfoBody.text = body ?? "";

            // Default: hide run attempt viewer unless explicitly enabled by run selection.
            _activeRunRecord = null;
            _activeAttemptIndex = -1;
            if (_mapAttemptBox != null) _mapAttemptBox.style.display = DisplayStyle.None;

            _mapInfoPanel.style.display = DisplayStyle.Flex;
            _mapPageUI?.RequestViewportRecalc();

        }

        private void HideMapInfoPanel()
        {
            if (_mapInfoPanel == null) return;
            _mapInfoPanel.style.display = DisplayStyle.None;
            _mapPageUI?.RequestViewportRecalc();

        }

        private void ShowRunInfo(string runId, string displayName)
        {
            if (_profile == null)
            {
                ShowMapInfo(displayName, "No stats profile loaded.");
                return;
            }

            var record = FindRunRecord(_profile, runId);
            int completions = record != null ? record.timesCompleted : 0;

            string difficulty = TryGetRunDifficultyLabel(runId, out var diffLabel) ? diffLabel : "--";

            _mapInfoActiveRunId = runId;
            _mapInfoActiveAttemptIndex = -1;
            if (_mapAttemptBox != null) _mapAttemptBox.style.display = DisplayStyle.None;
            if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;

            string body =
                $"Difficulty: {difficulty}\n" +
                $"Completions: {completions}";

            if (_mapInfoActionsRow != null)
                _mapInfoActionsRow.style.display = DisplayStyle.Flex;

            ShowMapInfo(displayName, body);
        }

        private void ShowLiftInfo(string liftId, string displayName)
        {
            if (_profile == null)
            {
                ShowMapInfo(displayName, "No stats profile loaded.");
                return;
            }

            int ridesLife = _profile.GetLiftRideCount(liftId, session: false);

            _mapInfoActiveRunId = null;
            _mapInfoActiveAttemptIndex = -1;
            if (_mapAttemptBox != null) _mapAttemptBox.style.display = DisplayStyle.None;
            if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;

            if (_mapInfoActionsRow != null)
                _mapInfoActionsRow.style.display = DisplayStyle.None;

            ShowMapInfo(displayName, $"Rides: {ridesLife}");
        }

        private void EnforceMapLayerBarAboveViewport(ScrollView mapPage)
        {
            var layerBar = mapPage.Q<VisualElement>("MapLayerBar");
            if (layerBar == null) return;

            // If a previous version docked it, rip it out first.
            if (_mapBottomDock != null && layerBar.parent == _mapBottomDock)
                layerBar.RemoveFromHierarchy();

            // Clear any “docking” style residue.
            layerBar.style.position = Position.Relative;
            layerBar.style.left = StyleKeyword.Null;
            layerBar.style.right = StyleKeyword.Null;
            layerBar.style.top = StyleKeyword.Null;
            layerBar.style.bottom = StyleKeyword.Null;

            var viewport = mapPage.Q<VisualElement>("MapViewport");
            if (viewport == null) return;

            var desiredParent = viewport.parent;
            if (desiredParent == null) return;

            // Prefer placing it directly after the toolbar if the toolbar shares this parent.
            // Toolbar has class "map-toolbar" (no name), so query by class.
            var toolbar = mapPage.Q<VisualElement>(null, "map-toolbar");
            if (toolbar != null && toolbar.parent == desiredParent)
            {
                int toolbarIndex = desiredParent.IndexOf(toolbar);
                int desiredIndex = Mathf.Clamp(toolbarIndex + 1, 0, desiredParent.childCount);

                if (layerBar.parent != desiredParent || desiredParent.IndexOf(layerBar) != desiredIndex)
                {
                    layerBar.RemoveFromHierarchy();
                    desiredParent.Insert(desiredIndex, layerBar);
                }
            }
            else
            {
                // Fallback: immediately before MapViewport
                int viewportIndex = desiredParent.IndexOf(viewport);
                int desiredIndex = Mathf.Max(0, viewportIndex);

                if (layerBar.parent != desiredParent || desiredParent.IndexOf(layerBar) != desiredIndex)
                {
                    layerBar.RemoveFromHierarchy();
                    desiredParent.Insert(desiredIndex, layerBar);
                }
            }

            // Keep spacing tight and consistent.
            layerBar.style.marginTop = 6;
            layerBar.style.marginBottom = 6;
        }

        private void EnforceMapBottomDock(ScrollView mapPage)
        {
            var scrollViewport = mapPage.Q<VisualElement>("unity-content-viewport");
            if (scrollViewport == null) return;

            if (_mapBottomDock == null)
            {
                _mapBottomDock = new VisualElement { name = "MapBottomDock" };
                _mapBottomDock.style.position = Position.Absolute;
                _mapBottomDock.style.left = 0;
                _mapBottomDock.style.right = 0;
                _mapBottomDock.style.bottom = 0;

                _mapBottomDock.style.flexDirection = FlexDirection.Column;

                // Dock itself should be invisible; panel provides styling.
                _mapBottomDock.style.paddingLeft = 0;
                _mapBottomDock.style.paddingRight = 0;
                _mapBottomDock.style.paddingTop = 0;
                _mapBottomDock.style.paddingBottom = 0;
                _mapBottomDock.style.backgroundColor = StyleKeyword.Null;
            }

            if (_mapBottomDock.parent != scrollViewport)
            {
                _mapBottomDock.RemoveFromHierarchy();
                scrollViewport.Add(_mapBottomDock);
            }

            // If the dock still contains a layer bar from an older version, force-remove it.
            var dockedLayerBar = _mapBottomDock.Q<VisualElement>("MapLayerBar");
            if (dockedLayerBar != null)
                dockedLayerBar.RemoveFromHierarchy();
        }

        private void DockInfoPanelToBottom()
        {
            if (_mapInfoPanel == null || _mapBottomDock == null) return;

            if (_mapInfoPanel.parent != _mapBottomDock)
            {
                _mapInfoPanel.RemoveFromHierarchy();
                _mapBottomDock.Add(_mapInfoPanel);
            }

            // Tight inside dock
            _mapInfoPanel.style.marginLeft = 0;
            _mapInfoPanel.style.marginRight = 0;
            _mapInfoPanel.style.marginTop = 0;
            _mapInfoPanel.style.marginBottom = 0;
        }

        private void OnMapMarkerSelected(Map.MapMarker m)
        {
            // Always center on the selected marker.
            _mapPageUI?.CenterOnWorldPosition(m.worldPosition);

            // If this marker ID matches a baked polyline, treat it like selecting the line (run/lift markers).
            if (TryFindPolylineById(m.id, out var poly))
            {
                OnMapPolylineSelected(poly);
                return;
            }

            // Fallback: lift station markers may not share the polyline ID.
            // If the marker is backed by a LiftLine, try treat the lift as selected.
            if (m.type == SkiGame.POI.POIType.SkiLift && m.source is LiftLine ll)
            {
                if (TryFindPolylineById(ll.gameObject.name, out var liftPoly))
                {
                    OnMapPolylineSelected(liftPoly);
                    return;
                }
            }

            // Custom / general POI: minimal name + description (meta)
            string title = string.IsNullOrWhiteSpace(m.displayName) ? "Point" : m.displayName;
            string body = string.IsNullOrWhiteSpace(m.meta) ? "" : m.meta.Trim();

            _mapInfoActiveRunId = null;
            _mapInfoActiveAttemptIndex = -1;
            if (_mapAttemptBox != null) _mapAttemptBox.style.display = DisplayStyle.None;
            if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;

            if (_mapInfoActionsRow != null)
                _mapInfoActionsRow.style.display = DisplayStyle.None;

            ShowMapInfo(title, body);
        }

        private void OnMapPolylineSelected(MapPolyline p)
        {
            if (!p.IsValid) return;

            // Center on an approximate midpoint of the polyline.
            if (p.pointsWorldXZ != null && p.pointsWorldXZ.Count > 0)
            {
                int mid = p.pointsWorldXZ.Count / 2;
                Vector2 wxz = p.pointsWorldXZ[Mathf.Clamp(mid, 0, p.pointsWorldXZ.Count - 1)];
                _mapPageUI?.CenterOnWorldPosition(new Vector3(wxz.x, 0f, wxz.y));
            }

            string name = string.IsNullOrWhiteSpace(p.displayName) ? p.id : p.displayName;

            switch (p.lineType)
            {
                case MapLineType.SkiRun:
                    ShowRunInfo(p.id, name);
                    break;

                case MapLineType.SkiLift:
                    {
                        // If the click originated from a station marker, append "(Top)/(Bottom)" to the title only.
                        string suffix = _mapPageUI != null ? _mapPageUI.SelectedLiftStationSuffix : null;
                        ShowLiftInfo(p.id, name + (suffix ?? ""));
                        break;
                    }

                default:
                    ShowMapInfo(name, "");
                    break;
            }
        }

        private int GetIdCount(List<PlayerStatsProfile.IdCountEntry> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id == id)
                    return list[i].count;
            }
            return 0;
        }

        private static bool TryGetRunDifficultyLabel(string runId, out string label)
        {
            label = "--";

            var run = FindRunById(runId);
            if (run == null) return false;

            var t = run.GetType();

            // 1) Try common property/field names
            string[] names =
            {
        "Difficulty", "difficulty",
        "RunDifficulty", "runDifficulty",
        "difficultyLevel", "DifficultyLevel",
        "difficultyName", "DifficultyName"
    };

            foreach (var n in names)
            {
                var pi = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null)
                {
                    object v = pi.GetValue(run);
                    if (TryFormatDifficulty(v, out label)) return true;
                }

                var fi = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                {
                    object v = fi.GetValue(run);
                    if (TryFormatDifficulty(v, out label)) return true;
                }
            }

            // 2) Try common methods
            string[] methods = { "GetDifficultyLabel", "GetDifficultyName", "GetDifficulty" };
            foreach (var mn in methods)
            {
                var mi = t.GetMethod(mn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null && mi.GetParameters().Length == 0)
                {
                    object v = mi.Invoke(run, null);
                    if (TryFormatDifficulty(v, out label)) return true;
                }
            }

            return false;
        }

        private static bool TryFormatDifficulty(object v, out string label)
        {
            label = "--";
            if (v == null) return false;

            if (v is string s)
            {
                label = string.IsNullOrWhiteSpace(s) ? "--" : s.Trim();
                return true;
            }

            var type = v.GetType();
            if (type.IsEnum)
            {
                label = v.ToString();
                return true;
            }

            // If it’s stored as an int, map the common 0-3 scheme.
            if (v is int i)
            {
                label = i switch
                {
                    0 => "Green",
                    1 => "Blue",
                    2 => "Red",
                    3 => "Black",
                    _ => i.ToString()
                };
                return true;
            }

            label = v.ToString();
            return !string.IsNullOrWhiteSpace(label);
        }

        private RunRecordEntry FindRunRecord(PlayerStatsProfile profile, string runId)
        {
            if (profile == null || string.IsNullOrEmpty(runId) || profile.runRecords == null)
                return null;

            for (int i = 0; i < profile.runRecords.Count; i++)
            {
                var r = profile.runRecords[i];
                if (r != null && r.runId == runId)
                    return r;
            }
            return null;
        }

        private bool TryFindPolylineById(string id, out MapPolyline poly)
        {
            poly = default;
            if (mapData == null || string.IsNullOrEmpty(id) || mapData.Polylines == null) return false;

            var lines = mapData.Polylines;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].id == id)
                {
                    poly = lines[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Used by the Runs page to jump to the map and select the matching run polyline.
        /// </summary>
        public void NavigateToMapAndSelectRun(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                NavigateTo("Page_Map");
                return;
            }

            NavigateTo("Page_Map");
            _mapPageUI?.SelectPolylineById(runId, center: true, minZoom: 1.25f);
        }

        private static SkiRunLine FindRunById(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId)) return null;

#if UNITY_2023_1_OR_NEWER
            var runs = UnityEngine.Object.FindObjectsByType<SkiRunLine>(UnityEngine.FindObjectsSortMode.None);
#else
    var runs = UnityEngine.Object.FindObjectsOfType<SkiRunLine>();
#endif
            foreach (var r in runs)
            {
                if (r != null && r.RunId == runId)
                    return r;
            }
            return null;
        }

        private void RefreshWatchRunTrackerPage()
        {
            if (_wRunPageName == null && _wRunPageTime == null && _wRunPageProgress == null &&
                _wRunPageTopSpeed == null && _wRunPageStacks == null && _wRunPageFill == null)
                return;

            // Ensure we have a tracker
            if (_runProgressTracker == null)
            {
                if (_skiController != null)
                    _runProgressTracker = _skiController.GetComponent<RunProgressTracker>();

                if (_runProgressTracker == null)
                    _runProgressTracker = FindObjectOfType<RunProgressTracker>();
            }

            // -------- 1) Active run (highest priority) --------
            RunProgressTracker.ActiveRunProgress p = default;
            bool onRun = _runProgressTracker != null && _runProgressTracker.TryGetActiveProgress(out p);

            if (onRun)
            {
                // IMPORTANT: percent should reflect entry->current coverage (consistent with the range bar)
                float covered01 = Mathf.Clamp01(Mathf.Abs(p.currentFraction01 - p.entryFraction01));
                int pct = Mathf.Clamp(Mathf.RoundToInt(covered01 * 100f), 0, 100);

                if (_wRunPageName != null) _wRunPageName.text = p.runName;
                if (_wRunPageProgress != null) _wRunPageProgress.text = $"{pct}%";
                if (_wRunPageTime != null) _wRunPageTime.text = $"Time {FormatRunTileTime(p.elapsedSeconds)}";
                if (_wRunPageTopSpeed != null) _wRunPageTopSpeed.text = $"Top {p.topSpeedMps:0.0} m/s";
                if (_wRunPageStacks != null) _wRunPageStacks.text = $"Stacks {p.stacks}";

                SetRangeFill(_wRunPageFill, p.entryFraction01, p.currentFraction01, minVisiblePercent: 0.8f);

                // Clear exit tint while actively tracking
                ApplyRunExitHighlight(_wRunTrackerCard, -1f);
                return;
            }

            // -------- 2) Not on a run: show last attempt (exit highlight) --------
            if (_runProgressTracker != null && _runProgressTracker.TryGetLastAttemptSummary(out var last))
            {
                float covered01 = last.isCompletion ? 1f : Mathf.Clamp01(Mathf.Abs(last.exitFraction01 - last.entryFraction01));
                int pct = Mathf.Clamp(Mathf.RoundToInt(covered01 * 100f), 0, 100);

                if (_wRunPageName != null) _wRunPageName.text = last.runName;
                if (_wRunPageProgress != null) _wRunPageProgress.text = $"{pct}%";
                if (_wRunPageTime != null) _wRunPageTime.text = $"Time {FormatRunTileTime(last.elapsedSeconds)}";
                if (_wRunPageTopSpeed != null) _wRunPageTopSpeed.text = $"Top {last.topSpeedMps:0.0} m/s";
                if (_wRunPageStacks != null) _wRunPageStacks.text = $"Stacks {last.stacks}";

                SetRangeFill(_wRunPageFill, last.entryFraction01, last.exitFraction01, minVisiblePercent: 0.8f);

                // Yellow -> green based on coverage relative to minimum tracked threshold
                float highlight01 = GetExitHighlight01(last.isCompletion, covered01);
                ApplyRunExitHighlight(_wRunTrackerCard, highlight01);
                return;
            }

            // -------- 3) Empty --------
            if (_wRunPageName != null) _wRunPageName.text = "Not on a run";
            if (_wRunPageProgress != null) _wRunPageProgress.text = "--%";
            if (_wRunPageTime != null) _wRunPageTime.text = "Time --:--";
            if (_wRunPageTopSpeed != null) _wRunPageTopSpeed.text = "Top --.- m/s";
            if (_wRunPageStacks != null) _wRunPageStacks.text = "Stacks 0";

            SetRangeFill(_wRunPageFill, 0f, 0f, minVisiblePercent: 0f);
            ApplyRunExitHighlight(_wRunTrackerCard, -1f);
        }

        private static void SetRangeFill(VisualElement fill, float start01, float end01, float minVisiblePercent = 0f)
        {
            if (fill == null)
                return;

            float a = Mathf.Clamp01(start01);
            float b = Mathf.Clamp01(end01);

            float lo = Mathf.Min(a, b);
            float hi = Mathf.Max(a, b);

            float leftPct = lo * 100f;
            float widthPct = (hi - lo) * 100f;

            if (minVisiblePercent > 0f && widthPct <= 0.0001f)
                widthPct = minVisiblePercent;
            else if (minVisiblePercent > 0f)
                widthPct = Mathf.Max(widthPct, minVisiblePercent);

            if (leftPct + widthPct > 100f)
                widthPct = Mathf.Max(0f, 100f - leftPct);

            fill.style.left = Length.Percent(leftPct);
            fill.style.width = Length.Percent(widthPct);
        }

        private void EnsureRunProgressTracker()
        {
            if (_runProgressTracker != null) return;

            if (_skiController != null)
                _runProgressTracker = _skiController.GetComponent<RunProgressTracker>();

            if (_runProgressTracker == null)
                _runProgressTracker = FindObjectOfType<RunProgressTracker>();
        }

        private float GetExitHighlight01(bool isCompletion, float covered01)
        {
            EnsureRunProgressTracker();
            if (_runProgressTracker == null) return -1f;

            float min = Mathf.Clamp01(_runProgressTracker.MinCoverageFractionToLog01);

            if (isCompletion) return 1f;
            if (covered01 < min) return -1f; // don’t highlight if it wouldn’t have been logged

            return Mathf.InverseLerp(min, 1f, Mathf.Clamp01(covered01));
        }

        private static void ApplyRunExitHighlight(VisualElement el, float highlight01)
        {
            if (el == null) return;

            if (highlight01 < 0f)
            {
                el.style.backgroundColor = StyleKeyword.Null; // revert to USS default
                return;
            }

            // Yellow -> Green
            Color yellow = new Color(1f, 0.85f, 0.25f, 0.35f);
            Color green = new Color(0.20f, 0.90f, 0.45f, 0.35f);

            el.style.backgroundColor = Color.Lerp(yellow, green, Mathf.Clamp01(highlight01));
        }

        private int GetRunsCompletedTodayCount()
        {
            var profile = _statsManager != null ? _statsManager.Profile : null;
            if (profile == null || profile.runRecords == null) return 0;

            var tc = FindObjectOfType<TimeController>();
            if (tc == null) return 0;

            // Resolve the current month index from monthPresets + currentMonthData.
            int monthIndex = -1;
            if (tc.monthPresets != null && tc.currentMonthData != null)
            {
                for (int i = 0; i < tc.monthPresets.Length; i++)
                {
                    if (tc.monthPresets[i] == tc.currentMonthData)
                    {
                        monthIndex = i;
                        break;
                    }
                }
            }

            // If we can't resolve month index, we can't compare attempts reliably.
            if (monthIndex < 0) return 0;

            int count = 0;

            for (int r = 0; r < profile.runRecords.Count; r++)
            {
                var rec = profile.runRecords[r];
                if (rec?.attempts == null) continue;

                for (int i = 0; i < rec.attempts.Count; i++)
                {
                    var a = rec.attempts[i];
                    if (!a.isCompletion) continue;

                    if (a.gameYear == tc.currentYear &&
                        a.gameMonthIndex == monthIndex &&
                        a.gameDayOfMonth == tc.dayOfMonth)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void HookSkiPassManager()
        {
            var inst = SkiPassManager.Instance;
            if (_skiPassMgr == inst) return;
            _skiPassMgr = inst;
            _skiPassCardsBuilt = false;
        }

        private void ResetSkiPassTileTintToUSS()
        {
            if (_tileSkiPass == null) return;

            // Revert to USS defaults (the .tile rule)
            _tileSkiPass.style.backgroundColor = StyleKeyword.Null;
            _tileSkiPass.style.borderLeftColor = StyleKeyword.Null;
            _tileSkiPass.style.borderRightColor = StyleKeyword.Null;
            _tileSkiPass.style.borderTopColor = StyleKeyword.Null;
            _tileSkiPass.style.borderBottomColor = StyleKeyword.Null;
        }

        private void ApplySkiPassTileTint(Color mapColor)
        {
            if (_tileSkiPass == null) return;

            // Keep it subtle so text remains readable.
            // Use the level’s mapColor as a tint, with stronger border cue.
            var bg = mapColor;
            bg.a = 0.20f;

            var br = mapColor;
            br.a = 0.45f;

            _tileSkiPass.style.backgroundColor = new StyleColor(bg);
            _tileSkiPass.style.borderLeftColor = new StyleColor(br);
            _tileSkiPass.style.borderRightColor = new StyleColor(br);
            _tileSkiPass.style.borderTopColor = new StyleColor(br);
            _tileSkiPass.style.borderBottomColor = new StyleColor(br);
        }

        private void RefreshSkiPassTile()
        {
            if (_lblTileSkiPassLevel == null) return;

            if (_skiPassMgr == null)
            {
                _lblTileSkiPassLevel.text = "No manager";
                ResetSkiPassTileTintToUSS();
                return;
            }

            _lblTileSkiPassLevel.text = _skiPassMgr.GetCurrentPassDisplayName();

            // Tint the tile based on the player’s ACTIVE pass level.
            var cfg = _skiPassMgr.Config;
            var level = cfg != null ? cfg.Get(_skiPassMgr.CurrentLevel) : null;

            if (level == null)
            {
                ResetSkiPassTileTintToUSS();
                return;
            }

            ApplySkiPassTileTint(level.mapColor);
        }

        private void RefreshSkiPassPage(bool forceRebuildCards)
        {
            if (_skiPassMgr == null) return;

            // Ensure selection is valid
            if (_skiPassSelectedLevel < 0)
                _skiPassSelectedLevel = _skiPassMgr.CurrentLevel;

            if (!_skiPassCardsBuilt || forceRebuildCards)
                BuildSkiPassCardsAndDurations();

            // Current pass info
            if (_lblSkiPassCurrentName != null)
                _lblSkiPassCurrentName.text = _skiPassMgr.GetCurrentPassDisplayName();

            if (_lblSkiPassCurrentExpiry != null)
                _lblSkiPassCurrentExpiry.text = _skiPassMgr.HasTimedPass ? "Active" : "Default pass";

            if (_lblSkiPassTimeRemaining != null)
                _lblSkiPassTimeRemaining.text = _skiPassMgr.GetRemainingTimeString();

            if (_skiPassTimeFill != null)
            {
                float frac = _skiPassMgr.GetRemainingFraction01();
                _skiPassTimeFill.style.width = new Length(Mathf.Clamp01(frac) * 100f, LengthUnit.Percent);
            }

            // Available lifts (current)
            RebuildLiftList(_listSkiPassAvailableLifts, _skiPassMgr.CurrentLevel, showLocked: false);

            // Selected pass details + price
            RefreshSelectedPassDetails();
        }

        private void ApplySkiPassCardBackground(VisualElement card)
        {
            if (card == null) return;

            var meta = card.userData as SkiPassCardMeta;
            if (meta == null) return;

            bool isSelected = card.ClassListContains("is-selected");

            // Use the pass level mapColor as the card background tint.
            // Keep alpha conservative so text stays readable.
            Color c = meta.mapColor;

            float a =
                meta.lockedBelow ? 0.08f :
                isSelected ? 0.32f :
                0.18f;

            c.a = a;
            card.style.backgroundColor = new StyleColor(c);
        }

        private void RefreshAllSkiPassCardBackgrounds()
        {
            if (_gridSkiPassCards == null) return;

            _gridSkiPassCards
                .Query<VisualElement>(className: "skipass-passcard")
                .ForEach(ApplySkiPassCardBackground);
        }

        private void BuildSkiPassCardsAndDurations()
        {
            _skiPassCardsBuilt = true;

            // Cards
            if (_gridSkiPassCards != null)
            {
                _gridSkiPassCards.Clear();

                var cfg = _skiPassMgr != null ? _skiPassMgr.Config : null;
                if (cfg != null && cfg.levels != null)
                {
                    for (int i = 0; i < cfg.levels.Length; i++)
                    {
                        int lvl = i;
                        var p = cfg.Get(lvl);
                        if (p == null) continue;

                        // Prevent buying below current active level by simply not showing lower cards as selectable
                        bool lockedBelow = lvl < _skiPassMgr.CurrentLevel;

                        var card = new VisualElement();
                        card.AddToClassList("skipass-passcard");
                        if (lvl == _skiPassSelectedLevel) card.AddToClassList("is-selected");
                        if (lockedBelow) card.AddToClassList("is-locked");

                        // Store pass-level UI color so we can reapply visuals on selection changes.
                        card.userData = new SkiPassCardMeta
                        {
                            level = lvl,
                            mapColor = p.mapColor,
                            lockedBelow = lockedBelow
                        };

                        // Apply initial background tint from PassLevel.mapColor.
                        ApplySkiPassCardBackground(card);

                        var title = new Label($"{p.displayName}");
                        title.AddToClassList("skipass-passcard-title");
                        card.Add(title);

                        var sub = new Label($"L{lvl} • {p.dayPrice:N0}/day");
                        sub.AddToClassList("skipass-passcard-sub");
                        card.Add(sub);

                        card.RegisterCallback<ClickEvent>(_ =>
                        {
                            if (_skiPassMgr == null) return;
                            if (lvl < _skiPassMgr.CurrentLevel) return; // hard gate
                            _skiPassSelectedLevel = lvl;

                            UpdateSkiPassPurchaseControlsVisibility();

                            // refresh selected class
                            _gridSkiPassCards.Query<VisualElement>(className: "skipass-passcard").ForEach(e => e.RemoveFromClassList("is-selected"));
                            card.AddToClassList("is-selected");

                            // Re-apply per-card background tint (selected/locked alpha changes).
                            RefreshAllSkiPassCardBackgrounds();

                            RefreshSelectedPassDetails();

                        });

                        _gridSkiPassCards.Add(card);
                    }

                    RefreshAllSkiPassCardBackgrounds();
                }
            }

            // Duration buttons
            if (_rowSkiPassDurations != null)
            {
                _rowSkiPassDurations.Clear();

                var cfg = _skiPassMgr != null ? _skiPassMgr.Config : null;
                if (cfg != null && cfg.durations != null && cfg.durations.Length > 0)
                {
                    _skiPassSelectedDurationIndex = Mathf.Clamp(_skiPassSelectedDurationIndex, 0, cfg.durations.Length - 1);

                    for (int i = 0; i < cfg.durations.Length; i++)
                    {
                        int idx = i;
                        var d = cfg.durations[i];

                        var b = new Button();
                        b.text = d != null ? d.label : $"Option {i}";
                        b.AddToClassList("skipass-duration-btn");
                        if (idx == _skiPassSelectedDurationIndex) b.AddToClassList("is-on");

                        b.clicked += () =>
                        {
                            _skiPassSelectedDurationIndex = idx;

                            _rowSkiPassDurations.Query<Button>().ForEach(bb => bb.RemoveFromClassList("is-on"));
                            b.AddToClassList("is-on");

                            RefreshSelectedPassDetails();
                        };

                        _rowSkiPassDurations.Add(b);
                    }
                }
            }

            RefreshSelectedPassDetails();
        }

        private void RefreshSelectedPassDetails()
        {
            if (_skiPassMgr == null) return;
            var cfg = _skiPassMgr.Config;
            if (cfg == null) return;

            UpdateSkiPassPurchaseControlsVisibility();

            var p = cfg.Get(_skiPassSelectedLevel);
            if (_lblSkiPassSelectedName != null)
                _lblSkiPassSelectedName.text = p != null ? $"{p.displayName} (L{_skiPassSelectedLevel})" : $"Level {_skiPassSelectedLevel}";

            // Show lifts unlocked at selected level (locked included so user sees what's gated)
            RebuildLiftList(_listSkiPassSelectedLifts, _skiPassSelectedLevel, showLocked: true);

            // Price quote
            if (_lblSkiPassPrice != null)
            {
                int freeLevel = (cfg != null) ? cfg.defaultLevelIndex : 0;

                if (_skiPassSelectedLevel == freeLevel)
                {
                    _lblSkiPassPrice.text = "Free (default pass)";
                }
                else if (_skiPassMgr.TryQuotePurchase(_skiPassSelectedLevel, _skiPassSelectedDurationIndex, out var q, out var reason))
                {
                    string creditStr = q.credit > 0 ? $" • Credit {q.credit:N0}" : "";
                    string mode = q.isUpgrade ? "Upgrade" : (q.isExtend ? "Extend" : "Purchase");
                    _lblSkiPassPrice.text = $"{mode}: {q.finalCost:N0}{creditStr}";
                }
                else
                {
                    _lblSkiPassPrice.text = reason;
                }
            }
        }

        private void UpdateSkiPassPurchaseControlsVisibility()
        {
            if (_skiPassMgr == null) return;

            var cfg = _skiPassMgr.Config;
            int freeLevel = (cfg != null) ? cfg.defaultLevelIndex : 0;

            bool isFreeSelected = (_skiPassSelectedLevel == freeLevel);

            // Hide duration buttons & purchase button for the free level
            if (_rowSkiPassDurations != null)
                _rowSkiPassDurations.style.display = isFreeSelected ? DisplayStyle.None : DisplayStyle.Flex;

            if (_btnSkiPassPurchase != null)
                _btnSkiPassPurchase.style.display = isFreeSelected ? DisplayStyle.None : DisplayStyle.Flex;

            // Optional: clear any previous result text when switching to free tier
            if (isFreeSelected && _lblSkiPassResult != null)
                _lblSkiPassResult.text = "";
        }

        private void OnClickSkiPassPurchase()
        {
            var cfg = _skiPassMgr.Config;
            if (_skiPassMgr == null || _profile == null || _skiPassSelectedLevel == cfg.defaultLevelIndex) return;

            bool TrySpend(int cost)
            {
                if (cost <= 0) return true;
                if (_profile.currency < cost) return false;
                _profile.currency -= cost;
                return true;
            }

            bool ok = _skiPassMgr.TryPurchase(_skiPassSelectedLevel, _skiPassSelectedDurationIndex, TrySpend, out var q, out var reason);

            if (_lblSkiPassResult != null)
                _lblSkiPassResult.text = ok ? "Purchased!" : reason;

            if (_navCurrency != null && _navCurrency.style.display == DisplayStyle.Flex)
                _navCurrency.text = $"${_profile.currency:N0}";

            RefreshSkiPassTile();
            RefreshSkiPassPage(forceRebuildCards: true);
        }

        private void RebuildLiftList(VisualElement container, int level, bool showLocked)
        {
            if (container == null) return;

            container.Clear();

            var lifts = FindObjectsOfType<LiftLine>(includeInactive: false);
            if (lifts == null || lifts.Length == 0)
            {
                container.Add(new Label("No lifts found."));
                return;
            }

            Array.Sort(lifts, (a, b) =>
            {
                int ar = a != null ? Mathf.Max(0, a.RequiredPassLevel) : 0;
                int br = b != null ? Mathf.Max(0, b.RequiredPassLevel) : 0;
                return ar.CompareTo(br);
            });

            for (int i = 0; i < lifts.Length; i++)
            {
                var lift = lifts[i];
                if (lift == null) continue;

                int req = Mathf.Max(0, lift.RequiredPassLevel);
                bool ok = level >= req;

                if (!showLocked && !ok) continue;

                var row = new Label(ok ? lift.name : $"{lift.name} (Requires L{req})");
                row.AddToClassList("skipass-liftrow");
                if (!ok) row.AddToClassList("is-locked");
                container.Add(row);
            }
        }

        /// <summary>
        /// Uses reflection so this compiles even if LiftLine hasn't been updated yet.
        /// Supports: int requiredPassLevel field, or int RequiredPassLevel property.
        /// Defaults to 0 (basic).
        /// </summary>
        private static int GetLiftRequiredPassLevel(LiftLine lift)
        {
            if (lift == null) return 0;

            var t = lift.GetType();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var f = t.GetField("requiredPassLevel", flags);
            if (f != null && f.FieldType == typeof(int))
                return Mathf.Max(0, (int)f.GetValue(lift));

            var p = t.GetProperty("RequiredPassLevel", flags);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead)
                return Mathf.Max(0, (int)p.GetValue(lift));

            return 0;
        }

        // =====================================================================
        // Stats "Day" calendar (Weeks + Day detail) using TimeWeather.TimeController
        // =====================================================================

        [System.Serializable]
        private struct DayKey
        {
            public int year;
            public int monthIndex;
            public int dayOfMonth;
            public int dayOfYear; // uses TimeController.dayCount for ordering

            public DayKey(int year, int monthIndex, int dayOfMonth, int dayOfYear)
            {
                this.year = year;
                this.monthIndex = monthIndex;
                this.dayOfMonth = dayOfMonth;
                this.dayOfYear = dayOfYear;
            }

            public DayKey(int year, int monthIndex, int dayOfMonth)
            {
                this.year = year;
                this.monthIndex = monthIndex;
                this.dayOfMonth = dayOfMonth;
                this.dayOfYear = -1; // unknown/not needed for all call sites
            }

            public string ToId() => $"{year}-{monthIndex}-{dayOfMonth}";
        }

        [Serializable]
        private struct DaySnapshot
        {
            // Core stats
            public float topSpeedMps;
            public float avgSpeedMps;
            public float distanceMeters;

            public float verticalAscentMeters;
            public float verticalDescentMeters;

            public float airTimeSeconds;
            public float airDistanceMeters;

            public float grindTimeSeconds;
            public float grindDistanceMeters;

            // Counts
            public int runsVisited;
            public int runsCompleted;
            public int runsCompletedClean;

            public float topRunSpeedMps;

            public int liftsUsed;
            public int stacks;

            public int landmarksVisited;
        }

        [System.Serializable]
        private struct DayBaseline
        {
            public float totalDistanceMeters;
            public float totalVerticalAscentMeters;
            public float totalVerticalDescentMeters;

            public float totalAirTimeSeconds;
            public float totalAirDistanceMeters;

            public float totalGrindTimeSeconds;
            public float totalGrindDistanceMeters;

            public int totalRunsCompleted;
            public int totalRunsCompletedClean;
            public int totalLiftsUsed;
            public int totalStacks;
        }

        [System.Serializable]
        private class DayRecord
        {
            public DayKey key;
            public DaySnapshot snapshot;
        }

        [System.Serializable]
        private class DayRecordStore
        {
            public int firstDayOfYear = -1;
            public List<DayRecord> records = new();
        }

        [System.Serializable]
        private class DayState
        {
            public bool hasActive;
            public DayKey activeKey;
            public DayBaseline baseline;
            public float activeMaxSpeedMps;

            public DayRecordStore store = new();
        }

        private const string PREF_STATS_DAY_STATE = "PhoneHUD.StatsDayState.v1";

        private void TickStatsDayCalendar()
        {
            if (_time == null || _profile == null) return;
            if (!_statsShowSession) return; // only on Day tab

            EnsureStatsDayState();

            // Update active max speed using current session max (best available without deeper hooks)
            float sessionTop = _profile.session.topSpeedMps;
            if (sessionTop > _statsDayState.activeMaxSpeedMps)
                _statsDayState.activeMaxSpeedMps = sessionTop;

            // Rebuild weeks UI if week changed (or first time)
            int wk = GetCurrentWeekIndexRelative();
            if (wk != _statsCurrentWeekIndex)
            {
                _statsCurrentWeekIndex = wk;
                if (_statsExpandedWeekIndex < 0) _statsExpandedWeekIndex = wk;
                RebuildStatsWeeksList();
            }

            // Update day title (detail panel)
            if (_statsDayPanel != null && !_statsDayPanel.ClassListContains("is-hidden"))
            {
                UpdateStatsDayTitle();
                UpdateStatsDayNavButtons();
            }

            // Persist occasionally (cheap JSON, but don’t spam: only when phone open + stats page open)
            SaveStatsDayState();
        }

        private DaySnapshot GetDisplayedDaySnapshot()
        {
            EnsureStatsDayState();

            // If day detail open, show selected day. Otherwise show today.
            DayKey key = _statsSelectedDay.HasValue ? _statsSelectedDay.Value : _statsDayState.activeKey;

            // Active day is computed live from baseline; others from records.
            if (_statsDayState.hasActive && key.ToId() == _statsDayState.activeKey.ToId())
                return ComputeActiveSnapshot();

            if (_statsDayState.store != null && _statsDayState.store.records != null)
            {
                for (int i = 0; i < _statsDayState.store.records.Count; i++)
                {
                    var r = _statsDayState.store.records[i];
                    if (r != null && r.key.ToId() == key.ToId())
                        return r.snapshot;
                }
            }

            // Fallback: empty snapshot
            return new DaySnapshot { topSpeedMps = -1f };
        }

        private void EnsureStatsDayState()
        {
            if (_statsDayState == null || _statsDayState.store == null)
                LoadStatsDayState();

            if (_time == null || _profile == null) return;

            DayKey today = GetTodayKey();

            // Initialize calendar baseline (Week 1) the first time we ever open/use the Stats Day calendar.
            if (_statsDayState.store.firstDayOfYear < 0)
            {
                int start = -1;

                if (_profile != null && _profile.playthrough != null && _profile.playthrough.calendarStartDayOfYear >= 0)
                    start = _profile.playthrough.calendarStartDayOfYear;

                if (start < 0)
                    start = Mathf.Max(0, today.dayOfYear); // default to today

                _statsDayState.store.firstDayOfYear = start;
                SaveStatsDayState();
            }

            // Migration/repair: if the saved activeKey refers to "today" by dayOfYear but has a bad month/day, fix it.
            if (_statsDayState.hasActive &&
                _statsDayState.activeKey.dayOfYear == today.dayOfYear &&
                (_statsDayState.activeKey.year != today.year ||
                 _statsDayState.activeKey.monthIndex != today.monthIndex ||
                 _statsDayState.activeKey.dayOfMonth != today.dayOfMonth))
            {
                _statsDayState.activeKey = today;
                SaveStatsDayState();
                return;
            }

            // Day rollover: finalize old day, start new day
            if (_statsDayState.activeKey.ToId() != today.ToId())
            {
                FinalizeAndStoreActiveDay();
                _statsDayState.activeKey = today;
                _statsDayState.baseline = ReadLifetimeBaseline(_profile);
                _statsDayState.activeMaxSpeedMps = _profile.session.topSpeedMps;

                SaveStatsDayState();

                // When day changes, if viewing old "today", keep them on the new today
                if (!_statsSelectedDay.HasValue)
                    RebuildStatsWeeksList();
            }
        }

        private void FinalizeAndStoreActiveDay()
        {
            DaySnapshot snap = ComputeActiveSnapshot();

            if (_statsDayState.store.records == null)
                _statsDayState.store.records = new List<DayRecord>();

            // Replace if exists, else add
            string id = _statsDayState.activeKey.ToId();
            for (int i = 0; i < _statsDayState.store.records.Count; i++)
            {
                if (_statsDayState.store.records[i] != null && _statsDayState.store.records[i].key.ToId() == id)
                {
                    _statsDayState.store.records[i].snapshot = snap;
                    return;
                }
            }

            _statsDayState.store.records.Add(new DayRecord
            {
                key = _statsDayState.activeKey,
                snapshot = snap
            });
        }

        private DaySnapshot ComputeActiveSnapshot()
        {
            var life = _profile.lifetime;

            DaySnapshot d = new DaySnapshot();

            d.distanceMeters = Mathf.Max(0f, life.totalDistanceMeters - _statsDayState.baseline.totalDistanceMeters);
            d.verticalAscentMeters = Mathf.Max(0f, life.totalVerticalAscentMeters - _statsDayState.baseline.totalVerticalAscentMeters);
            d.verticalDescentMeters = Mathf.Max(0f, life.totalVerticalDescentMeters - _statsDayState.baseline.totalVerticalDescentMeters);

            d.airTimeSeconds = Mathf.Max(0f, life.totalAirTimeSeconds - _statsDayState.baseline.totalAirTimeSeconds);
            d.airDistanceMeters = Mathf.Max(0f, life.totalAirDistanceMeters - _statsDayState.baseline.totalAirDistanceMeters);

            d.grindTimeSeconds = Mathf.Max(0f, life.totalGrindTimeSeconds - _statsDayState.baseline.totalGrindTimeSeconds);
            d.grindDistanceMeters = Mathf.Max(0f, life.totalGrindDistanceMeters - _statsDayState.baseline.totalGrindDistanceMeters);

            d.runsCompleted = Mathf.Max(0, life.totalRunsCompleted - _statsDayState.baseline.totalRunsCompleted);
            d.runsCompletedClean = Mathf.Max(0, life.totalRunsCompletedClean - _statsDayState.baseline.totalRunsCompletedClean);
            d.liftsUsed = Mathf.Max(0, life.totalLiftsUsed - _statsDayState.baseline.totalLiftsUsed);
            d.stacks = Mathf.Max(0, life.totalStacks - _statsDayState.baseline.totalStacks);

            d.topSpeedMps = Mathf.Max(0f, _statsDayState.activeMaxSpeedMps);

            return d;
        }

        private static DayBaseline ReadLifetimeBaseline(PlayerStatsProfile p)
        {
            var life = p.lifetime;
            return new DayBaseline
            {
                totalDistanceMeters = life.totalDistanceMeters,
                totalVerticalAscentMeters = life.totalVerticalAscentMeters,
                totalVerticalDescentMeters = life.totalVerticalDescentMeters,
                totalAirTimeSeconds = life.totalAirTimeSeconds,
                totalAirDistanceMeters = life.totalAirDistanceMeters,
                totalGrindTimeSeconds = life.totalGrindTimeSeconds,
                totalGrindDistanceMeters = life.totalGrindDistanceMeters,
                totalRunsCompleted = life.totalRunsCompleted,
                totalRunsCompletedClean = life.totalRunsCompletedClean,
                totalLiftsUsed = life.totalLiftsUsed,
                totalStacks = life.totalStacks
            };
        }

        private DayKey GetTodayKey()
        {
            int year = _time != null ? _time.currentYear : 0;
            int doy = _time != null ? _time.dayCount : 0;

            // Prefer deriving month/day from day-of-year using monthPresets (robust; no string matching).
            if (_time != null && TryConvertDayOfYear(doy, out int mi, out int dom))
                return new DayKey(year, mi, dom, doy);

            // Fallback (should rarely happen)
            int fallbackMonthIndex = GetCurrentMonthIndex();
            int fallbackDom = _time != null ? _time.dayOfMonth : 1;
            return new DayKey(year, fallbackMonthIndex, fallbackDom, doy);
        }

        private int GetCurrentMonthIndex()
        {
            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0 || _time.currentMonthData == null)
                return 0;

            string m = _time.currentMonthData.month;
            for (int i = 0; i < _time.monthPresets.Length; i++)
            {
                if (_time.monthPresets[i] != null && _time.monthPresets[i].month == m)
                    return i;
            }
            return 0;
        }

        private int GetCurrentWeekIndexRelative()
        {
            if (_time == null) return 0;

            int first = Mathf.Max(0, _statsDayState.store.firstDayOfYear);
            int rel = Mathf.Max(0, _time.dayCount - first);
            return rel / 7;
        }

        private void RebuildStatsWeeksList()
        {
            if (_statsWeeksList == null || _time == null) return;

            _statsWeeksList.Clear();

            int first = Mathf.Max(0, _statsDayState.store.firstDayOfYear);
            int relDays = Mathf.Max(0, _time.dayCount - first);
            int weeksSoFar = Mathf.Max(1, (relDays / 7) + 1);

            if (_statsExpandedWeekIndex >= weeksSoFar) _statsExpandedWeekIndex = weeksSoFar - 1;
            if (_statsExpandedWeekIndex < -1) _statsExpandedWeekIndex = -1;

            for (int weekIndex = weeksSoFar - 1; weekIndex >= 0; weekIndex--)
            {
                int weekNumber = weekIndex + 1;

                var weekCard = new VisualElement();
                weekCard.AddToClassList("runs-week-card");

                var header = new VisualElement();
                header.AddToClassList("runs-week-header");

                var title = new Label($"Week {weekNumber}");
                title.AddToClassList("runs-week-title");


                header.pickingMode = PickingMode.Position;
                title.pickingMode = PickingMode.Ignore;

                int capturedWeek = weekIndex;
                header.AddManipulator(new Clickable(() =>
                {
                    _statsExpandedWeekIndex = (_statsExpandedWeekIndex == capturedWeek) ? -1 : capturedWeek;
                    RebuildStatsWeeksList();
                }));

                header.Add(title);

                var daysRow = new VisualElement();
                daysRow.AddToClassList("runs-week-days");

                bool expanded = (_statsExpandedWeekIndex == weekIndex);
                daysRow.EnableInClassList("is-hidden", !expanded);

                for (int dow = 0; dow < 7; dow++)
                {
                    int relDay = weekIndex * 7 + dow;
                    int absDay = first + relDay;

                    // gate: no future days
                    bool isFuture = absDay > _time.dayCount;

                    if (!TryConvertDayOfYear(absDay, out int monthIndex, out int dayOfMonth))
                        continue;

                    var key = new DayKey(_time.currentYear, monthIndex, dayOfMonth, absDay);

                    DaySnapshot snap = (absDay == _time.dayCount) ? ComputeActiveSnapshot() : GetSnapshotIfRecorded(key);

                    bool hasStats = (snap.distanceMeters > 0.1f) || (snap.runsCompleted > 0) || (snap.liftsUsed > 0);

                    var tile = BuildStatsDayTile(dow, dayOfMonth, hasStats, isFuture);

                    if (isFuture)
                    {
                        tile.AddToClassList("is-future");
                        SetEnabledAndVisual(tile, false);
                    }
                    else
                    {
                        tile.RegisterCallback<PointerDownEvent>(evt =>
                        {
                            evt.StopPropagation();
                            OpenStatsDayDetail(key);
                        });
                    }

                    daysRow.Add(tile);
                }

                weekCard.Add(header);
                weekCard.Add(daysRow);
                _statsWeeksList.Add(weekCard);
            }
        }

        private int CountRunsCompletedInWeek(int weekIndex, int firstDayOfYear)
        {
            int count = 0;
            for (int dow = 0; dow < 7; dow++)
            {
                int abs = firstDayOfYear + (weekIndex * 7 + dow);
                if (_time != null && abs > _time.dayCount) continue;

                if (!TryConvertDayOfYear(abs, out int m, out int d)) continue;
                var key = new DayKey(_time.currentYear, m, d, abs);

                DaySnapshot s = (abs == _time.dayCount) ? ComputeActiveSnapshot() : GetSnapshotIfRecorded(key);
                count += Mathf.Max(0, s.runsCompleted);
            }
            return count;
        }

        private DaySnapshot GetSnapshotIfRecorded(DayKey key)
        {
            if (_statsDayState.store?.records == null)
                return new DaySnapshot { topSpeedMps = -1f };

            string id = key.ToId();
            for (int i = 0; i < _statsDayState.store.records.Count; i++)
            {
                var r = _statsDayState.store.records[i];
                if (r != null && r.key.ToId() == id)
                    return r.snapshot;
            }

            return new DaySnapshot { topSpeedMps = -1f };
        }

        private VisualElement BuildStatsDayTile(int dow, int dayOfMonth, bool hasStats, bool isFuture)
        {
            var tile = new VisualElement();
            tile.AddToClassList("runs-day-tile");
            if (hasStats) tile.AddToClassList("has-runs");
            if (isFuture) tile.AddToClassList("is-future");

            string dowStr = ((System.DayOfWeek)(((int)System.DayOfWeek.Monday + dow) % 7)).ToString().Substring(0, 3);

            var l1 = new Label(dowStr);
            l1.AddToClassList("runs-day-dow");

            var l2 = new Label(dayOfMonth.ToString());
            l2.AddToClassList("runs-day-dom");

            tile.Add(l1);
            tile.Add(l2);

            return tile;
        }

        private void OpenStatsDayDetail(DayKey day)
        {
            // gate: cannot open future
            if (_time != null && day.dayOfYear > _time.dayCount)
                return;

            _statsSelectedDay = day;

            _statsDayPanel?.EnableInClassList("is-hidden", false);
            _statsWeeksList?.EnableInClassList("is-hidden", true);
            // Show "Back to Weeks" only on the day detail view
            _btnStatsDayBack?.EnableInClassList("is-hidden", false);
            _statsWeeksPanel?.EnableInClassList("is-hidden", false);

            UpdateStatsDayTitle();
            UpdateStatsDayNavButtons();

            RefreshDynamicStatRows();
        }

        private void CloseStatsDayDetail()
        {
            _statsSelectedDay = null;
            _statsDayPanel?.EnableInClassList("is-hidden", true);
            _statsWeeksList?.EnableInClassList("is-hidden", false);
            // Hide "Back to Weeks" on the weeks list view
            _btnStatsDayBack?.EnableInClassList("is-hidden", true);
            _statsWeeksPanel?.EnableInClassList("is-hidden", true);

        }

        private void StepStatsDay(int delta)
        {
            if (!_statsSelectedDay.HasValue || _time == null) return;

            int next = _statsSelectedDay.Value.dayOfYear + delta;

            // gate: before first day or after today
            int first = Mathf.Max(0, _statsDayState.store.firstDayOfYear);
            if (next < first) return;
            if (next > _time.dayCount) return;

            if (!TryConvertDayOfYear(next, out int mi, out int dom))
                return;

            _statsSelectedDay = new DayKey(_time.currentYear, mi, dom, next);
            UpdateStatsDayTitle();
            UpdateStatsDayNavButtons();
            RefreshDynamicStatRows();
        }

        private void UpdateStatsDayTitle()
        {
            if (_lblStatsDayTitle == null || !_statsSelectedDay.HasValue || _time == null) return;

            var d = _statsSelectedDay.Value;
            string monthName = SafeMonthName(d.monthIndex);
            _lblStatsDayTitle.text = $"{monthName} {d.dayOfMonth}, Y{d.year}";
        }

        private void UpdateStatsDayNavButtons()
        {
            if (_btnStatsDayPrev == null && _btnStatsDayNext == null) return;
            if (_time == null || !_statsSelectedDay.HasValue) return;

            int first = Mathf.Max(0, _statsDayState.store.firstDayOfYear);

            bool canPrev = _statsSelectedDay.Value.dayOfYear > first;
            bool canNext = _statsSelectedDay.Value.dayOfYear < _time.dayCount;

            if (_btnStatsDayPrev != null) SetEnabledAndVisual(_btnStatsDayPrev, canPrev);
            if (_btnStatsDayNext != null) SetEnabledAndVisual(_btnStatsDayNext, canNext);
        }

        private string SafeMonthName(int monthIndex)
        {
            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0) return "Month";
            if (monthIndex < 0 || monthIndex >= _time.monthPresets.Length) return "Month";
            return _time.monthPresets[monthIndex] != null ? _time.monthPresets[monthIndex].month : "Month";
        }

        // Same conversion approach as RunsPageUI, but using TimeController.monthPresets + daysInMonth
        private bool TryConvertDayOfYear(int dayOfYear, out int monthIndex, out int dayOfMonth)
        {
            monthIndex = 0;
            dayOfMonth = 1;

            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0)
                return false;

            int remaining = Mathf.Max(0, dayOfYear);

            for (int i = 0; i < _time.monthPresets.Length; i++)
            {
                int dim = _time.monthPresets[i] != null ? Mathf.Max(1, _time.monthPresets[i].daysInMonth) : 30;
                if (remaining < dim)
                {
                    monthIndex = i;
                    dayOfMonth = remaining + 1;
                    return true;
                }
                remaining -= dim;
            }

            // Clamp to last month
            monthIndex = _time.monthPresets.Length - 1;
            int lastDim2 = _time.monthPresets[monthIndex] != null ? Mathf.Max(1, _time.monthPresets[monthIndex].daysInMonth) : 30;
            dayOfMonth = Mathf.Clamp(remaining + 1, 1, lastDim2);
            return true;
        }

        private void LoadStatsDayState()
        {
            _statsDayState = new DayState();

            string json = PlayerPrefs.GetString(PREF_STATS_DAY_STATE, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { _statsDayState = JsonUtility.FromJson<DayState>(json); }
                catch { _statsDayState = new DayState(); }
            }

            if (_statsDayState.store == null)
                _statsDayState.store = new DayRecordStore();
            if (_statsDayState.store.records == null)
                _statsDayState.store.records = new List<DayRecord>();
        }

        private void SaveStatsDayState()
        {
            try
            {
                string json = JsonUtility.ToJson(_statsDayState);
                PlayerPrefs.SetString(PREF_STATS_DAY_STATE, json);
            }
            catch { /* ignore */ }
        }

    }
}
