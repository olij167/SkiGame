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

        private PhoneMapPageUI _mapPageUI;

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
        private Button _mapInfoRunPrevBtn;
        private Button _mapInfoRunNextBtn;
        private Label _mapInfoRunAttemptDetails;

        private string _mapInfoActiveRunId;
        private int _mapInfoActiveAttemptIndex = -1;

        private RunRecordEntry _activeRunRecord;
        private int _activeAttemptIndex = -1;

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
        private VisualElement _wAirBox;        // container used to tint/grey air-time
        private Label _wAirTime;               // air-time value
        private Label _wAirHint;               // "LAST AIR" / "IN-AIR"

        private Label _wSessTopSpeed;
        private Label _wSessDist;
        private Label _wSessVert;
        private Label _wSessStacks;

        private Label _wLifeTopSpeed;
        private Label _wLifeDist;
        private Label _wLifeVert;
        private Label _wLifeStacks;

        private VisualElement _watchTasksList;

        private Label _wAch1;
        private Label _wAch2;

        private VisualElement _btnWatchSOS;

        // Watch air-time state (per-jump)
        private bool _watchWasGrounded = true;
        private float _watchAirStartTime = 0f;
        private float _watchLastAirDuration = 0f;
        private float _watchCurrentAirDuration = 0f;

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

        private Label _statsS_TopSpeed;
        private Label _statsS_Distance;
        private Label _statsS_Vert;
        private Label _statsS_Stacks;

        private Label _statsL_TopSpeed;
        private Label _statsL_Distance;
        private Label _statsL_Vert;
        private Label _statsL_Stacks;

        // Stats tiles (secondary)
        private Label _statsS_AirTime;
        private Label _statsS_AirDist;
        private Label _statsS_Runs;
        private Label _statsS_Lifts;

        private Label _statsL_AvgSpeed;
        private Label _statsL_AirTime;
        private Label _statsL_AirDist;
        private Label _statsL_Runs;
        private Label _statsL_Lifts;

        // Stats page (sleek dashboard)
        private VisualElement _statsDashSession;
        private VisualElement _statsDashLifetime;

        private sealed class StatCardRefs
        {
            public Label value;
            public Func<PlayerStatsProfile, string> valueProvider;
        }

        private readonly List<StatCardRefs> _statCardsSession = new();
        private readonly List<StatCardRefs> _statCardsLifetime = new();

        private bool _statsShowSession = true;

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

        // Time/Weather
        private TimeWeather.TimeController _time;
        private TimeWeather.WeatherController _weather;
        private WindController _wind;

        // Input state
        private bool _tabHeld;
        private float _tabDownAt;
        private bool _phoneOpen;

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


        private void Reset()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return;

            _root = document.rootVisualElement;
            if (_root == null) return;

            if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                _root.styleSheets.Add(styleSheet);

            CacheSceneReferences();
            BindUI();
            BindInput();

            SetPhoneOpen(startPhoneOpen);
            SetWatchPage(Mathf.Clamp(_watchIndex, 0, _watchPages.Count - 1), force: true);
            NavigateTo("Page_Home", clearStack: true);

            // Ensure consistent visuals on start.
            RefreshAllUI(force: true);
        }

        private void OnDisable()
        {
            UnbindInput();
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

                if (_playerRb == null || _skiController == null)
                {
                    var tracker = FindObjectOfType<PlayerStatsTracker>();
                    if (tracker != null)
                    {
                        if (_playerRb == null) _playerRb = tracker.GetComponent<Rigidbody>();
                        if (_skiController == null) _skiController = tracker.GetComponent<SkiController>();
                    }

                    // Fallback (in case Tracker isn't on the same object as SkiController)
                    if (_skiController == null) _skiController = FindObjectOfType<SkiController>();
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

            // Live-map updates while the map page is open.
            // Map updates
            if (_skiController != null)
            {
                var t = _skiController.transform;

                if (_phoneOpen)
                {
                    string page = GetActivePhonePage();

                    if (page == "Page_Map")
                    {
                        _mapPageUI?.SetPlayer(t);
                        _mapPageUI?.Tick(Time.unscaledDeltaTime);
                    }
                    else if (page == "Page_Home")
                    {
                        _homeTileMiniMapUI?.SetPlayer(t);
                        _homeTileMiniMapUI?.Tick(Time.unscaledDeltaTime);
                    }
                }
                else
                {
                    // Watch is visible when phone is closed
                    _watchMiniMapUI?.SetPlayer(t);
                    _watchMiniMapUI?.Tick(Time.unscaledDeltaTime);
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

            _watchPages.Add(Q<ScrollView>("WatchPage_Session"));
            _watchPages.Add(Q<ScrollView>("WatchPage_Lifetime"));
            _watchPages.Add(Q<ScrollView>("WatchPage_Tasks"));
            _watchPages.Add(Q<ScrollView>("WatchPage_Achievements"));

            //_watchPages.Add(Q<ScrollView>("WatchPage_SOS"));

            // Watch labels (Home page)
            _wSpeed = Q<Label>("Lbl_WatchSpeed");

            // These MUST exist on the WatchPage_Speed (Home) page in UXML:
            _wHomeTime = Q<Label>("Lbl_WatchSpeedTime");          // top-left
            _wHomeWeather = Q<Label>("Lbl_WatchSpeedWeather");    // top-right
            _wAirBox = Q<VisualElement>("WatchAirBox");           // bottom-left container
            _wAirTime = Q<Label>("Lbl_WatchAirTime");             // bottom-left value
            _wAirHint = Q<Label>("Lbl_WatchAirHint");             // optional label

            // Session / Lifetime / Tasks / Achievements
            _wSessTopSpeed = Q<Label>("Lbl_WatchSessTopSpeed");
            _wSessDist = Q<Label>("Lbl_WatchSessDist");
            _wSessVert = Q<Label>("Lbl_WatchSessVert");
            _wSessStacks = Q<Label>("Lbl_WatchSessStacks");

            _wLifeTopSpeed = Q<Label>("Lbl_WatchLifeTopSpeed");
            _wLifeDist = Q<Label>("Lbl_WatchLifeDist");
            _wLifeVert = Q<Label>("Lbl_WatchLifeVert");
            _wLifeStacks = Q<Label>("Lbl_WatchLifeStacks");

            _watchTasksList = Q<VisualElement>("WatchTasksList");

            _wAch1 = Q<Label>("Lbl_WatchAch1");
            _wAch2 = Q<Label>("Lbl_WatchAch2");

            _wSessTopSpeed = Q<Label>("Lbl_WatchSessTopSpeed");
            _wSessDist = Q<Label>("Lbl_WatchSessDist");
            _wSessVert = Q<Label>("Lbl_WatchSessVert");
            _wSessStacks = Q<Label>("Lbl_WatchSessStacks");

            _wLifeTopSpeed = Q<Label>("Lbl_WatchLifeTopSpeed");
            _wLifeDist = Q<Label>("Lbl_WatchLifeDist");
            _wLifeVert = Q<Label>("Lbl_WatchLifeVert");
            _wLifeStacks = Q<Label>("Lbl_WatchLifeStacks");

            _wAch1 = Q<Label>("Lbl_WatchAch1");
            _wAch2 = Q<Label>("Lbl_WatchAch2");

            _btnWatchSOS = Q<VisualElement>("Btn_WatchSOS");
            if (_btnWatchSOS != null)
                _btnWatchSOS.RegisterCallback<ClickEvent>(_ => TriggerRespawn());

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
            RegisterPage("Page_TimeWeather");
            RegisterPage("Page_Map");
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
                _watchMiniMapUI.SetPlayerTracking(showMarker: true, drawTrail: false);
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
                _homeTileMiniMapUI.SetPlayerTracking(showMarker: true, drawTrail: false);
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
            // Stats tabs + panels
            _btnStatsTabSession = Q<VisualElement>("Btn_StatsTabSession");
            _btnStatsTabLifetime = Q<VisualElement>("Btn_StatsTabLifetime");
            _panelStatsSession = Q<VisualElement>("Panel_StatsSession");
            _panelStatsLifetime = Q<VisualElement>("Panel_StatsLifetime");

            _statsS_TopSpeed = Q<Label>("Lbl_StatsS_TopSpeed");
            _statsS_Distance = Q<Label>("Lbl_StatsS_Distance");
            _statsS_Vert = Q<Label>("Lbl_StatsS_Vert");
            _statsS_Stacks = Q<Label>("Lbl_StatsS_Stacks");

            _statsL_TopSpeed = Q<Label>("Lbl_StatsL_TopSpeed");
            _statsL_Distance = Q<Label>("Lbl_StatsL_Distance");
            _statsL_Vert = Q<Label>("Lbl_StatsL_Vert");
            _statsL_Stacks = Q<Label>("Lbl_StatsL_Stacks");

            // Secondary stats tiles
            _statsS_AirTime = Q<Label>("Lbl_StatsS_AirTime");
            _statsS_AirDist = Q<Label>("Lbl_StatsS_AirDist");
            _statsS_Runs = Q<Label>("Lbl_StatsS_Runs");
            _statsS_Lifts = Q<Label>("Lbl_StatsS_Lifts");

            _statsL_AvgSpeed = Q<Label>("Lbl_StatsL_AvgSpeed");
            _statsL_AirTime = Q<Label>("Lbl_StatsL_AirTime");
            _statsL_AirDist = Q<Label>("Lbl_StatsL_AirDist");
            _statsL_Runs = Q<Label>("Lbl_StatsL_Runs");
            _statsL_Lifts = Q<Label>("Lbl_StatsL_Lifts");

            if (_btnStatsTabSession != null)
                _btnStatsTabSession.RegisterCallback<ClickEvent>(_ => SetStatsTab(true));

            if (_btnStatsTabLifetime != null)
                _btnStatsTabLifetime.RegisterCallback<ClickEvent>(_ => SetStatsTab(false));

            _statsDashSession = Q<VisualElement>("StatsDash_Session");
            _statsDashLifetime = Q<VisualElement>("StatsDash_Lifetime");

            BuildStatsDashboard();

            // Default tab
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

            // Hook selection events -> info panel
            _mapPageUI.MarkerSelected += OnMapMarkerSelected;
            _mapPageUI.PolylineSelected += OnMapPolylineSelected;
            _mapPageUI.SelectionCleared += HideMapInfoPanel;

            // Build the map info panel (bottom overlay) once.
            BuildMapInfoPanel();

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
                2 => "Session",
                3 => "Lifetime",
                4 => "Tasks",
                5 => "Achievements",
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
            var content = new VisualElement { name = "MapContent" };
            content.style.position = Position.Absolute;
            content.style.left = 0;
            content.style.top = 0;
            content.style.right = 0;
            content.style.bottom = 0;

            // Background, polylines, markers
            var bg = new VisualElement { name = "MapBackground" };
            var polys = new VisualElement { name = "MapPolylines" };
            var markers = new VisualElement { name = "MapMarkers" };

            bg.style.position = Position.Absolute;
            polys.style.position = Position.Absolute;
            markers.style.position = Position.Absolute;

            bg.style.left = polys.style.left = markers.style.left = 0;
            bg.style.top = polys.style.top = markers.style.top = 0;
            bg.style.right = polys.style.right = markers.style.right = 0;
            bg.style.bottom = polys.style.bottom = markers.style.bottom = 0;
            // Helps the baked map texture display like an actual map (not “invisible”/oddly scaled)
            bg.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;

            // Make embedded maps non-interactive by default; specific instances can override by calling SetMinimapMode.
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
            _statsDashSession ??= _root.Q<VisualElement>("StatsDash_Session");
            _statsDashLifetime ??= _root.Q<VisualElement>("StatsDash_Lifetime");

            _statCardsSession.Clear();
            _statCardsLifetime.Clear();

            _statsDashSession?.Clear();
            _statsDashLifetime?.Clear();

            if (_statsDashSession == null || _statsDashLifetime == null)
                return;

            // SESSION (dense list)
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

            // LIFETIME (dense list)
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

            for (int i = 0; i < _statCardsSession.Count; i++)
            {
                var c = _statCardsSession[i];
                if (c?.value == null || c.valueProvider == null) continue;
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
                pageName == "Page_Achievements";

            if (_navCurrency != null)
            {
                _navCurrency.style.display = showCurrency ? DisplayStyle.Flex : DisplayStyle.None;

                if (showCurrency && _profile != null)
                    _navCurrency.text = $"${_profile.currency:N0}";
            }

            if (pageName == "Page_Map")
                _mapPageUI?.Refresh();

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
                "Page_TimeWeather" => "Time & Weather",
                "Page_Map" => "Map",
                "Page_Save" => "Save & Load",
                "Page_Settings" => "Settings",
                "Page_SOS" => "SOS",
                _ => "Menu"
            };
        }

        private void RefreshAllUI(bool force)
        {
            // If you later want to throttle updates: you can add a timer here.
            RefreshTimeWeather();
            RefreshStats();

            // Updates Watch + Home tiles + currency label.
            RefreshTasksAndAchievements();

            // IMPORTANT:
            // RefreshTasksMatrix() clears & rebuilds the visual tree, which can destroy a cell
            // between PointerDown and PointerUp, making clicks feel “random”.
            // So we skip rebuilding for a short window right after user interaction.
            if (Time.unscaledTime >= _suppressTasksMatrixRebuildUntil)
                RefreshTasksMatrix();

            // Home watch page “live” element
            RefreshWatchAirTime();
        }

        private void RefreshWatchHomeTopBar(string timeStr, string weatherStr)
        {
            if (_wHomeTime != null) _wHomeTime.text = timeStr;
            if (_wHomeWeather != null) _wHomeWeather.text = weatherStr;
        }

        private void RefreshWatchAirTime()
        {
            if (_wAirTime == null) return;

            // Prefer SkiController grounded state for accuracy (air time should reflect ski-ground contact logic).
            bool grounded;

            if (_skiController != null)
            {
                grounded = _skiController.IsRiderGrounded;
            }
            else
            {
                // Fallback if SkiController isn't available yet.
                if (_playerRb == null) return;

                Vector3 origin = _playerRb.position + Vector3.up * 0.15f;
                grounded = Physics.Raycast(origin, Vector3.down, 0.55f, ~0, QueryTriggerInteraction.Ignore);
            }

            // State machine:
            // - grounded -> airborne: reset timer
            // - airborne -> grounded: freeze last airtime
            // - airborne: count up
            if (!grounded)
            {
                if (_watchWasGrounded)
                {
                    // Just left the ground: reset for this jump.
                    _watchAirStartTime = Time.time;
                    _watchCurrentAirDuration = 0f;
                }

                _watchCurrentAirDuration = Time.time - _watchAirStartTime;
                // Do NOT touch _watchLastAirDuration here (we freeze it on landing).
            }
            else
            {
                if (!_watchWasGrounded)
                {
                    // Just landed: capture the jump duration and freeze it while grounded.
                    _watchLastAirDuration = _watchCurrentAirDuration;
                }

                // While grounded, we do not count up and we keep the "last" value frozen.
                _watchCurrentAirDuration = 0f;
            }

            _watchWasGrounded = grounded;

            // Display: while grounded show last air duration; while airborne show live duration.
            float display = grounded ? _watchLastAirDuration : _watchCurrentAirDuration;
            _wAirTime.text = $"{display:0.00}s";

            if (_wAirHint != null)
            {
                _wAirHint.text = grounded ? "LAST AIR" : "IN-AIR";
            }

            // Grey out when grounded, white when airborne (active)
            if (_wAirBox != null)
                _wAirBox.EnableInClassList("is-active-air", !grounded);


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
            if (_tileDateValue != null) _tileDateValue.text = $"{dayOfWeek}, {dateStr}";
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
        private void RefreshStats()
        {
            // Current speed from RB
            float speed = 0f;
            if (_playerRb != null)
            {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
                speed = _playerRb.linearVelocity.magnitude;
#else
                speed = _playerRb.velocity.magnitude;
#endif
            }

            if (_wSpeed != null) _wSpeed.text = $"{speed:0.0}";

            if (_profile == null) return;

            var s = _profile.session;
            var l = _profile.lifetime;

            // Watch session summary
            if (_wSessTopSpeed != null) _wSessTopSpeed.text = $"Top: {s.topSpeedMps:0.0} m/s";
            if (_wSessDist != null) _wSessDist.text = $"Dist: {s.distanceMeters:0} m";
            if (_wSessVert != null) _wSessVert.text = $"Vert: +{s.verticalAscentMeters:0} / -{s.verticalDescentMeters:0}";
            if (_wSessStacks != null) _wSessStacks.text = $"Stacks: {s.stacks}";

            // Watch lifetime summary
            if (_wLifeTopSpeed != null) _wLifeTopSpeed.text = $"Top: {l.topSpeedMps:0.0} m/s";
            if (_wLifeDist != null) _wLifeDist.text = $"Dist: {l.totalDistanceMeters:0} m";
            if (_wLifeVert != null) _wLifeVert.text = $"Vert: +{l.totalVerticalAscentMeters:0} / -{l.totalVerticalDescentMeters:0}";
            if (_wLifeStacks != null) _wLifeStacks.text = $"Stacks: {l.totalStacks}";

            // Home tiles summary (Stats tile)
            if (_tileStats1 != null) _tileStats1.text = $"Top speed: {s.topSpeedMps:0.0} m/s";
            if (_tileStats2 != null) _tileStats2.text = $"Session dist: {s.distanceMeters:0} m";
            if (_tileStats3 != null) _tileStats3.text = $"Stacks: {s.stacks}";

            // Full stats page bodies
            // Stats page: metric grid (primary)
            if (_statsS_TopSpeed != null) _statsS_TopSpeed.text = $"{s.topSpeedMps:0.0} m/s";
            if (_statsS_Distance != null) _statsS_Distance.text = $"{s.distanceMeters:0} m";
            if (_statsS_Vert != null) _statsS_Vert.text = $"+{s.verticalAscentMeters:0} / -{s.verticalDescentMeters:0}";
            if (_statsS_Stacks != null) _statsS_Stacks.text = $"{s.stacks}";

            if (_statsL_TopSpeed != null) _statsL_TopSpeed.text = $"{l.topSpeedMps:0.0} m/s";
            if (_statsL_Distance != null) _statsL_Distance.text = $"{l.totalDistanceMeters:0} m";
            if (_statsL_Vert != null) _statsL_Vert.text = $"+{l.totalVerticalAscentMeters:0} / -{l.totalVerticalDescentMeters:0}";
            if (_statsL_Stacks != null) _statsL_Stacks.text = $"{l.totalStacks}";

            // Secondary tiles (Session)
            if (_statsS_AirTime != null) _statsS_AirTime.text = $"{s.airTimeSeconds:0.0}s";
            if (_statsS_AirDist != null) _statsS_AirDist.text = $"{s.airDistanceMeters:0} m";
            if (_statsS_Runs != null) _statsS_Runs.text = $"{s.runsCompleted}";
            if (_statsS_Lifts != null) _statsS_Lifts.text = $"{s.liftsUsed}";

            // Secondary tiles (Lifetime)
            if (_statsL_AvgSpeed != null) _statsL_AvgSpeed.text = $"{l.AverageSpeedMps:0.0} m/s";
            if (_statsL_AirTime != null) _statsL_AirTime.text = $"{l.totalAirTimeSeconds:0.0}s";
            if (_statsL_AirDist != null) _statsL_AirDist.text = $"{l.totalAirDistanceMeters:0} m";
            if (_statsL_Runs != null) _statsL_Runs.text = $"{l.totalRunsCompleted}";
            if (_statsL_Lifts != null) _statsL_Lifts.text = $"{l.totalLiftsUsed}";

            // Stats page: details (secondary, no longer a wall of text)
            if (_statsSessionBody != null)
            {
                _statsSessionBody.text =
                    $"Air time: {s.airTimeSeconds:0.0}s\n" +
                    $"Air dist: {s.airDistanceMeters:0}m\n" +
                    $"Runs: {s.runsCompleted}\n" +
                    $"Lifts: {s.liftsUsed}";
            }

            if (_statsLifetimeBody != null)
            {
                _statsLifetimeBody.text =
                    $"Avg speed: {l.AverageSpeedMps:0.0}m/s\n" +
                    $"Air time: {l.totalAirTimeSeconds:0.0}s\n" +
                    $"Air dist: {l.totalAirDistanceMeters:0}m\n" +
                    $"Runs: {l.totalRunsCompleted}\n" +
                    $"Lifts: {l.totalLiftsUsed}";
            }

            // Ensure panel visibility is consistent (in case UI was rebuilt)
            SetStatsTab(_statsShowSession, force: true);

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

            // Render lines: one per metric (showing its next target)
            var metricLines = new List<string>(metricsTrackedToday);
            float metricProgressAvg = 0f;

            for (int i = 0; i < metricsTrackedToday; i++)
            {
                var key = order[i];
                var d = bestByRow[key];

                string name = GetDailyMetricDisplayName(d.ladderId, d.metric);

                // Show progress/target to remove ambiguity
                string progText = FormatMetricValue(d.metric, d.progress);
                string targetText = FormatMetricValue(d.metric, d.target);
                string ratioText = $"{progText}/{targetText}";

                if (d.completed && !d.claimed)
                    metricLines.Add($"{name}: {ratioText}");
                else if (!d.completed)
                    metricLines.Add($"{name}: {ratioText}");
                else
                    metricLines.Add($"{name}: Complete");

                metricProgressAvg += Mathf.Clamp01(d.pct01);
            }

            if (metricsTrackedToday > 0)
                metricProgressAvg /= metricsTrackedToday;

            // Populate Watch list: include a top line with claimable ratio, then all metrics
            if (_watchTasksList != null)
            {
                var watchLines = new List<string>(metricLines.Count + 1);
                watchLines.Add(claimableMetrics > 0
                    ? $"Claimable: {claimableMetrics}/{metricsTrackedToday}"
                    : $"Claimable: 0/{metricsTrackedToday}");
                watchLines.AddRange(metricLines);

                SetDynamicLabelList(_watchTasksList, watchLines, new[] { "watch-kv" });
            }

            // Populate Tile list: just metrics (badge already shows ratio)
            if (_tileTasksList != null)
            {
                SetDynamicLabelList(_tileTasksList, metricLines, new[] { "tile-sub", "tile-taskline" });
            }

            // Badge becomes "claimable metrics / metrics tracked today"
            if (_tileTasksProgress != null)
                if (metricsTrackedToday <= 0)
                {
                    _tileTasksProgress.text = "";
                    _tileTasksProgress.style.display = DisplayStyle.None;
                }
                else
                {
                    _tileTasksProgress.text = $"{claimableMetrics}/{metricsTrackedToday}";
                    _tileTasksProgress.style.display = DisplayStyle.Flex;
                }

            // Progress fill uses average progress to the "next actionable tier" per metric (stable + meaningful)
            if (_tileTasksProgressFill != null)
                _tileTasksProgressFill.style.width = Length.Percent(Mathf.RoundToInt(metricProgressAvg * 100f));

            // Optional: keep the legacy Tasks body label updated if it still exists (safe no-op if null)
            if (_tasksBody != null)
            {
                if (total == 0) _tasksBody.text = "(no tasks)";
                else
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < total && i < 8; i++)
                    {
                        var d = _dailyTierBuffer[i];
                        string status = d.claimed ? "[CLAIMED]" : d.completed ? "[READY]" : $"[{Mathf.RoundToInt(d.pct01 * 100f)}%]";
                        sb.AppendLine($"{status} {GetDailyMetricDisplayName(d.ladderId, d.metric)} {FormatMetricValue(d.metric, d.target)}");
                    }
                    _tasksBody.text = sb.ToString().TrimEnd();
                }
            }

            // -------------------------
            // Achievements (top locked previews)
            // -------------------------
            string a1 = "(no achievements)";
            string a2 = "";

            if (_progression != null)
                _progression.GetTopAchievementLines(_profile, out a1, out a2);

            if (_wAch1 != null) _wAch1.text = a1;
            if (_wAch2 != null) _wAch2.text = a2;

            if (_tileAch1 != null) _tileAch1.text = a1;
            if (_tileAch2 != null) _tileAch2.text = a2;

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

            // Attach to the map page so it overlays the viewport.
            // Ensure the page is a positioning context.
            mapPage.style.position = Position.Relative;

            // If we already built it (domain reload etc), skip.
            if (_mapInfoPanel != null && _mapInfoPanel.parent != null)
                return;

            _mapInfoPanel = new VisualElement { name = "MapInfoPanel" };
            _mapInfoPanel.style.position = Position.Absolute;
            _mapInfoPanel.style.left = 8;
            _mapInfoPanel.style.right = 8;
            _mapInfoPanel.style.bottom = 8;
            _mapInfoPanel.style.paddingLeft = 10;
            _mapInfoPanel.style.paddingRight = 10;
            _mapInfoPanel.style.paddingTop = 8;
            _mapInfoPanel.style.paddingBottom = 10;
            _mapInfoPanel.style.borderTopLeftRadius = 12;
            _mapInfoPanel.style.borderTopRightRadius = 12;
            _mapInfoPanel.style.borderBottomLeftRadius = 12;
            _mapInfoPanel.style.borderBottomRightRadius = 12;
            _mapInfoPanel.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.72f));
            _mapInfoPanel.style.display = DisplayStyle.None;

            // Header row
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            _mapInfoTitle = new Label("Selection");
            _mapInfoTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _mapInfoTitle.style.flexGrow = 1;
            _mapInfoTitle.style.color = Color.white;   // ✅ white text

            _mapInfoCloseBtn = new Button(() => HideMapInfoPanel()) { text = "×" };
            _mapInfoCloseBtn.style.width = 28;
            _mapInfoCloseBtn.style.height = 24;

            header.Add(_mapInfoTitle);
            header.Add(_mapInfoCloseBtn);

            _mapInfoBody = new Label();
            _mapInfoBody.style.whiteSpace = WhiteSpace.Normal;
            _mapInfoBody.style.marginTop = 6;
            _mapInfoBody.style.color = Color.white;    // ✅ white text

            // Attempt viewer (hidden unless a run with attempts is selected)
            _mapAttemptBox = new VisualElement { name = "MapRunAttemptBox" };
            _mapAttemptBox.style.marginTop = 8;
            _mapAttemptBox.style.paddingTop = 6;
            _mapAttemptBox.style.borderTopWidth = 1;
            _mapAttemptBox.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
            _mapAttemptBox.style.display = DisplayStyle.None;

            var attemptHeaderRow = new VisualElement();
            attemptHeaderRow.style.flexDirection = FlexDirection.Row;
            attemptHeaderRow.style.alignItems = Align.Center;

            _mapAttemptPrevBtn = new Button(() => StepAttempt(-1)) { text = "◀" };
            _mapAttemptPrevBtn.style.width = 34;
            _mapAttemptPrevBtn.style.height = 24;

            _mapAttemptNextBtn = new Button(() => StepAttempt(+1)) { text = "▶" };
            _mapAttemptNextBtn.style.width = 34;
            _mapAttemptNextBtn.style.height = 24;

            _mapAttemptHeader = new Label("Attempt");
            _mapAttemptHeader.style.flexGrow = 1;
            _mapAttemptHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            _mapAttemptHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
            _mapAttemptHeader.style.color = Color.white; // ✅ white text

            attemptHeaderRow.Add(_mapAttemptPrevBtn);
            attemptHeaderRow.Add(_mapAttemptHeader);
            attemptHeaderRow.Add(_mapAttemptNextBtn);

            _mapAttemptDetails = new Label();
            _mapAttemptDetails.style.whiteSpace = WhiteSpace.Normal;
            _mapAttemptDetails.style.marginTop = 6;
            _mapAttemptDetails.style.color = Color.white; // ✅ white text

            _mapAttemptBox.Add(attemptHeaderRow);
            _mapAttemptBox.Add(_mapAttemptDetails);

            _mapInfoPanel.Add(header);
            _mapInfoPanel.Add(_mapInfoBody);
            _mapInfoPanel.Add(_mapAttemptBox);

            // Run history navigation (only shown for Ski Runs)
            _mapInfoRunNavRow = new VisualElement();
            _mapInfoRunNavRow.style.flexDirection = FlexDirection.Row;
            _mapInfoRunNavRow.style.alignItems = Align.Center;
            _mapInfoRunNavRow.style.marginTop = 8;
            _mapInfoRunNavRow.style.display = DisplayStyle.None;

            _mapInfoRunPrevBtn = new Button(() =>
            {
                if (string.IsNullOrEmpty(_mapInfoActiveRunId)) return;
                _mapInfoActiveAttemptIndex--;
                RefreshMapInfoForActiveSelection();
            })
            { text = "◀" };
            _mapInfoRunPrevBtn.style.width = 34;

            _mapInfoRunNextBtn = new Button(() =>
            {
                if (string.IsNullOrEmpty(_mapInfoActiveRunId)) return;
                _mapInfoActiveAttemptIndex++;
                RefreshMapInfoForActiveSelection();
            })
            { text = "▶" };
            _mapInfoRunNextBtn.style.width = 34;

            _mapInfoRunAttemptDetails = new Label();
            _mapInfoRunAttemptDetails.style.whiteSpace = WhiteSpace.Normal;
            _mapInfoRunAttemptDetails.style.flexGrow = 1;
            _mapInfoRunAttemptDetails.style.marginLeft = 8;

            _mapInfoRunNavRow.Add(_mapInfoRunPrevBtn);
            _mapInfoRunNavRow.Add(_mapInfoRunNextBtn);
            _mapInfoRunNavRow.Add(_mapInfoRunAttemptDetails);

            _mapInfoPanel.Add(_mapInfoRunNavRow);

            mapPage.Add(_mapInfoPanel);
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
        }

        private void HideMapInfoPanel()
        {
            if (_mapInfoPanel == null) return;
            _mapInfoPanel.style.display = DisplayStyle.None;
        }

        private void RefreshMapInfoForActiveSelection()
        {
            // Re-render current selection details (used by run history nav)
            if (string.IsNullOrEmpty(_mapInfoActiveRunId))
                return;

            // We don't know the display name here, so just show the runId if needed.
            ShowRunInfo(_mapInfoActiveRunId, _mapInfoTitle != null ? _mapInfoTitle.text : _mapInfoActiveRunId);
        }

        private void ShowRunInfo(string runId, string displayName)
        {
            if (_profile == null)
            {
                ShowMapInfo(displayName, "No stats profile loaded.");
                _mapInfoActiveRunId = null;
                _mapInfoRunNavRow.style.display = DisplayStyle.None;
                return;
            }

            var record = FindRunRecord(_profile, runId);

            int visitsLife = _profile.GetRunVisitCount(runId, session: false);
            int visitsSess = _profile.GetRunVisitCount(runId, session: true);

            int completed = record != null ? record.timesCompleted : 0;
            int completedClean = record != null ? record.timesCompletedClean : 0;

            float bestTime = record != null ? record.bestTimeSeconds : 0f;
            float bestCleanTime = record != null ? record.bestCleanTimeSeconds : 0f;

            int attemptCount = (record != null && record.attempts != null) ? record.attempts.Count : 0;

            // Default attempt index to latest
            if (_mapInfoActiveRunId != runId)
                _mapInfoActiveAttemptIndex = attemptCount - 1;

            _mapInfoActiveRunId = runId;

            string summary =
                $"Visits: {visitsLife} (session {visitsSess})\n" +
                $"Completions: {completed} (clean {completedClean})\n" +
                $"Best time: {(bestTime > 0 ? $"{bestTime:0.0}s" : "--")}\n" +
                $"Best clean time: {(bestCleanTime > 0 ? $"{bestCleanTime:0.0}s" : "--")}\n" +
                $"Attempts saved: {attemptCount}";

            ShowMapInfo(displayName, summary);

            // Attempt details + nav
            if (_mapInfoRunNavRow == null || _mapInfoRunAttemptDetails == null)
                return;

            if (attemptCount <= 0)
            {
                _mapInfoRunNavRow.style.display = DisplayStyle.Flex;
                _mapInfoRunPrevBtn.SetEnabled(false);
                _mapInfoRunNextBtn.SetEnabled(false);
                _mapInfoRunAttemptDetails.text = "No completion history yet.";
                return;
            }

            _mapInfoActiveAttemptIndex = Mathf.Clamp(_mapInfoActiveAttemptIndex, 0, attemptCount - 1);

            var a = record.attempts[_mapInfoActiveAttemptIndex];

            _mapInfoRunNavRow.style.display = DisplayStyle.Flex;
            _mapInfoRunPrevBtn.SetEnabled(_mapInfoActiveAttemptIndex > 0);
            _mapInfoRunNextBtn.SetEnabled(_mapInfoActiveAttemptIndex < attemptCount - 1);

            _mapInfoRunAttemptDetails.text =
                $"Run history: {(_mapInfoActiveAttemptIndex + 1)}/{attemptCount}\n" +
                $"Time: {a.timeSeconds:0.0}s | Avg: {a.averageSpeedMps:0.0} m/s | Top: {a.topSpeedMps:0.0} m/s\n" +
                $"Air: {a.airTimeSeconds:0.0}s ({a.airDistanceMeters:0.0}m) | Stacks: {a.stacks}\n" +
                $"On-route: {a.onRouteDistanceMeters:0.0}m | Off-route: {a.offRouteDistanceMeters:0.0}m\n" +
                $"Completion: {(a.completionFraction * 100f):0}%";
        }

        private void ShowLiftInfo(string liftId, string displayName)
        {
            if (_profile == null)
            {
                ShowMapInfo(displayName, "No stats profile loaded.");
                _mapInfoActiveRunId = null;
                if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;
                return;
            }

            int ridesLife = _profile.GetLiftRideCount(liftId, session: false);
            int ridesSess = _profile.GetLiftRideCount(liftId, session: true);

            string body =
                $"Rides: {ridesLife} (session {ridesSess})\n" +
                $"Lifetime lifts used: {_profile.lifetime.totalLiftsUsed}\n" +
                $"Session lifts used: {_profile.session.liftsUsed}";

            _mapInfoActiveRunId = null;
            if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;

            ShowMapInfo(displayName, body);
        }

        private void ShowPOIInfo(string poiId, string displayName)
        {
            if (_profile == null)
            {
                ShowMapInfo(displayName, "No stats profile loaded.");
                _mapInfoActiveRunId = null;
                if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;
                return;
            }

            int visitsLife = _profile.GetLandmarkVisitCount(poiId, session: false);
            int visitsSess = _profile.GetLandmarkVisitCount(poiId, session: true);

            string body =
                $"Visits: {visitsLife} (session {visitsSess})\n" +
                $"Unique POIs visited (lifetime): {_profile.visitedLandmarkIds?.Count ?? 0}\n" +
                $"Unique POIs visited (session): {_profile.sessionVisitedLandmarkIds?.Count ?? 0}";

            _mapInfoActiveRunId = null;
            if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;

            ShowMapInfo(displayName, body);
        }

        private void OnMapMarkerSelected(Map.MapMarker m)
        {
            if (_profile == null)
            {
                ShowMapInfo(m.displayName, "No stats profile loaded.");
                return;
            }

            // If this marker ID matches a baked polyline, treat it like selecting the line (run/lift markers).
            if (TryFindPolylineById(m.id, out var poly))
            {
                OnMapPolylineSelected(poly);
                return;
            }

            string title = string.IsNullOrWhiteSpace(m.displayName) ? "POI" : m.displayName;

            int sessVisits = GetIdCount(_profile.sessionLandmarkVisitCounts, m.id);
            int lifeVisits = GetIdCount(_profile.landmarkVisitCounts, m.id);

            // Fallback if count-lists aren't present yet (unique visit lists only)
            if (lifeVisits <= 0 && _profile.visitedLandmarkIds != null && _profile.visitedLandmarkIds.Contains(m.id)) lifeVisits = 1;
            if (sessVisits <= 0 && _profile.sessionVisitedLandmarkIds != null && _profile.sessionVisitedLandmarkIds.Contains(m.id)) sessVisits = 1;

            var lines = new List<string>
    {
        $"Type: {m.type}",
        $"Visits (session): {sessVisits}",
        $"Visits (lifetime): {lifeVisits}",
    };

            // Placeholders for future POI metrics you mentioned
            lines.Add("Other stats: (placeholder)");

            if (!string.IsNullOrWhiteSpace(m.meta))
                lines.Add($"\n{m.meta}");

            ShowMapInfo(title, string.Join("\n", lines));
        }

        private void OnMapPolylineSelected(MapPolyline p)
        {
            if (!p.IsValid) return;

            switch (p.lineType)
            {
                case MapLineType.SkiRun:
                    ShowRunInfo(p.id, string.IsNullOrWhiteSpace(p.displayName) ? p.id : p.displayName);
                    break;

                case MapLineType.SkiLift:
                    ShowLiftInfo(p.id, string.IsNullOrWhiteSpace(p.displayName) ? p.id : p.displayName);
                    break;

                default:
                    // If you ever add polylines for other types, fall back:
                    ShowMapInfo(p.displayName, $"Selected polyline '{p.id}' ({p.lineType}).");
                    _mapInfoActiveRunId = null;
                    if (_mapInfoRunNavRow != null) _mapInfoRunNavRow.style.display = DisplayStyle.None;
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

        private void ShowRunAttemptViewer(RunRecordEntry record)
        {
            if (_mapAttemptBox == null) return;

            _activeRunRecord = record;
            _activeAttemptIndex = -1;

            if (record == null || record.attempts == null || record.attempts.Count == 0)
            {
                _mapAttemptBox.style.display = DisplayStyle.None;
                return;
            }

            // Default to most recent attempt
            _activeAttemptIndex = record.attempts.Count - 1;

            _mapAttemptBox.style.display = DisplayStyle.Flex;
            RefreshAttemptViewer();
        }

        private void StepAttempt(int delta)
        {
            if (_activeRunRecord == null || _activeRunRecord.attempts == null) return;
            int n = _activeRunRecord.attempts.Count;
            if (n <= 0) return;

            _activeAttemptIndex = Mathf.Clamp(_activeAttemptIndex + delta, 0, n - 1);
            RefreshAttemptViewer();
        }

        private void RefreshAttemptViewer()
        {
            if (_mapAttemptHeader == null || _mapAttemptDetails == null || _mapAttemptPrevBtn == null || _mapAttemptNextBtn == null)
                return;

            if (_activeRunRecord == null || _activeRunRecord.attempts == null || _activeRunRecord.attempts.Count == 0)
            {
                _mapAttemptBox.style.display = DisplayStyle.None;
                return;
            }

            int n = _activeRunRecord.attempts.Count;
            _activeAttemptIndex = Mathf.Clamp(_activeAttemptIndex, 0, n - 1);

            _mapAttemptPrevBtn.SetEnabled(_activeAttemptIndex > 0);
            _mapAttemptNextBtn.SetEnabled(_activeAttemptIndex < n - 1);

            _mapAttemptHeader.text = $"Completion {_activeAttemptIndex + 1} / {n}";

            var a = _activeRunRecord.attempts[_activeAttemptIndex];
            if (a == null)
            {
                _mapAttemptDetails.text = "(missing attempt data)";
                return;
            }

            // Completed time
            string when = "";
            try
            {
                var dt = a.completedUtc.ToDateTimeUtc().ToLocalTime();
                when = dt.ToString("g");
            }
            catch { when = ""; }

            float totalDist = a.onRouteDistanceMeters + a.offRouteDistanceMeters;

            _mapAttemptDetails.text =
                (string.IsNullOrEmpty(when) ? "" : $"Completed: {when}\n") +
                $"Time: {a.timeSeconds:0.0}s\n" +
                $"Avg speed: {a.averageSpeedMps:0.0} m/s\n" +
                $"Top speed: {a.topSpeedMps:0.0} m/s\n" +
                $"Airtime: {a.airTimeSeconds:0.00}s  (air dist {a.airDistanceMeters:0} m)\n" +
                $"Distance: {totalDist:0} m  (on {a.onRouteDistanceMeters:0} / off {a.offRouteDistanceMeters:0})\n" +
                $"Stacks: {a.stacks}\n" +
                $"Completion: {a.completionFraction * 100f:0}%  (start @ {a.startDistanceMeters:0} m)\n" +
                $"Stack events: {(a.stackEvents != null ? a.stackEvents.Count : 0)}";
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

        private static bool TryGetRunAttemptSummary(PlayerStatsProfile profile, string runId, out int attempts, out int cleanAttempts, out float bestTopSpeedMps)
        {
            attempts = 0;
            cleanAttempts = 0;
            bestTopSpeedMps = 0f;

            if (profile == null || string.IsNullOrWhiteSpace(runId))
                return false;

            try
            {
                // Try method first: GetOrCreateRunRecord(string runId)
                var t = profile.GetType();
                var mi = t.GetMethod("GetOrCreateRunRecord", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object record = null;

                if (mi != null)
                {
                    var pars = mi.GetParameters();
                    if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                        record = mi.Invoke(profile, new object[] { runId });
                }

                // Fallback: search a runRecords field/property
                if (record == null)
                {
                    object runRecordsObj = null;

                    var fi = t.GetField("runRecords", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fi != null) runRecordsObj = fi.GetValue(profile);

                    if (runRecordsObj == null)
                    {
                        var pi = t.GetProperty("runRecords", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (pi != null) runRecordsObj = pi.GetValue(profile);
                    }

                    if (runRecordsObj is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var rec in enumerable)
                        {
                            if (rec == null) continue;

                            var rt = rec.GetType();

                            // try to read runId
                            string rid = null;
                            var ridField = rt.GetField("runId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (ridField != null) rid = ridField.GetValue(rec) as string;

                            if (rid == null)
                            {
                                var ridProp = rt.GetProperty("runId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (ridProp != null) rid = ridProp.GetValue(rec) as string;
                            }

                            if (rid == runId)
                            {
                                record = rec;
                                break;
                            }
                        }
                    }
                }

                if (record == null)
                    return false;

                // Pull attempts list
                var rType = record.GetType();
                object attemptsObj = null;

                var aField = rType.GetField("attempts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (aField != null) attemptsObj = aField.GetValue(record);

                if (attemptsObj == null)
                {
                    var aProp = rType.GetProperty("attempts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (aProp != null) attemptsObj = aProp.GetValue(record);
                }

                if (attemptsObj == null)
                {
                    var aProp2 = rType.GetProperty("Attempts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (aProp2 != null) attemptsObj = aProp2.GetValue(record);
                }

                if (!(attemptsObj is System.Collections.IEnumerable aEnum))
                    return false;

                foreach (var a in aEnum)
                {
                    if (a == null) continue;
                    attempts++;

                    var at = a.GetType();

                    // stacks
                    int stacks = 0;
                    var sf = at.GetField("stacks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (sf != null && sf.FieldType == typeof(int)) stacks = (int)sf.GetValue(a);
                    else
                    {
                        var sp = at.GetProperty("stacks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (sp != null && sp.PropertyType == typeof(int)) stacks = (int)sp.GetValue(a);
                    }

                    if (stacks == 0) cleanAttempts++;

                    // top speed
                    float ts = 0f;
                    var tf = at.GetField("topSpeedMps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (tf != null && tf.FieldType == typeof(float)) ts = (float)tf.GetValue(a);
                    else
                    {
                        var tp = at.GetProperty("topSpeedMps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (tp != null && tp.PropertyType == typeof(float)) ts = (float)tp.GetValue(a);
                    }

                    if (ts > bestTopSpeedMps) bestTopSpeedMps = ts;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
