using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static SkiGame.Progression.ProgressionDirector;

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

        private VisualElement _root;

        // Watch
        private VisualElement _watchCollapsed;
        private Label _watchTitle;
        private VisualElement _watchDots;
        private readonly List<VisualElement> _dotElems = new();
        private VisualElement _watchPagesRoot;
        private readonly List<ScrollView> _watchPages = new();
        private int _watchIndex = 0;

        // Watch labels (frequent updates)
        private Label _wSpeed;                 // big gauge text (already exists)
        private Label _wHomeTime;              // top-left minimal time on Home page
        private Label _wHomeWeather;           // top-right condition + temp on Home page
        private VisualElement _wAirBox;        // container used to tint/grey air-time
        private Label _wAirTime;               // air-time value

        private Label _wSessTopSpeed;
        private Label _wSessDist;
        private Label _wSessVert;
        private Label _wSessStacks;

        private Label _wLifeTopSpeed;
        private Label _wLifeDist;
        private Label _wLifeVert;
        private Label _wLifeStacks;

        private Label _wTask1;
        private Label _wTask2;
        private Label _wTask3;

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
        private Label _tileTask1;
        private Label _tileTask2;
        private Label _tileTask3;
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

        private bool _statsShowSession = true;

        private Label _tasksBody;
        // Tasks page (card list)
        private VisualElement _tasksList;
        // Tasks header (runtime-created)
        private VisualElement _tasksHeaderBar;
        private Label _tasksCurrencyLabel;
        private Button _tasksClaimAllBtn;

        private sealed class TaskCardRefs
        {
            public VisualElement root;
            public Label title;
            public Label badge;
            public Button claimBtn;

            public VisualElement fill;
            public Label progressText;
            public Label hint;
        }

        private readonly List<TaskCardRefs> _taskCards = new();
        private readonly List<ProgressionDirector.TaskDisplay> _taskDisplayBuffer = new();

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
            // Late-bind in case controllers spawn after UI.
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

            if (_time == null) _time = TimeWeather.TimeController.instance != null ? TimeWeather.TimeController.instance : FindObjectOfType<TimeWeather.TimeController>();
            if (_weather == null) _weather = TimeWeather.WeatherController.instance != null ? TimeWeather.WeatherController.instance : FindObjectOfType<TimeWeather.WeatherController>();
            if (_wind == null) _wind = WindController.Instance != null ? WindController.Instance : FindObjectOfType<WindController>();

            if (_progression == null) _progression = FindObjectOfType<ProgressionDirector>();

            HandleWatchScroll();
            HandleHoldToOpen();

            // Keep UI responsive but lightweight.
            RefreshAllUI(force: false);
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

            // Removed from watch cycle (redundant now):
            // _watchPages.Add(Q<ScrollView>("WatchPage_Distance"));
            // _watchPages.Add(Q<ScrollView>("WatchPage_TimeWeather"));

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

            // Session / Lifetime / Tasks / Achievements
            _wSessTopSpeed = Q<Label>("Lbl_WatchSessTopSpeed");
            _wSessDist = Q<Label>("Lbl_WatchSessDist");
            _wSessVert = Q<Label>("Lbl_WatchSessVert");
            _wSessStacks = Q<Label>("Lbl_WatchSessStacks");

            _wLifeTopSpeed = Q<Label>("Lbl_WatchLifeTopSpeed");
            _wLifeDist = Q<Label>("Lbl_WatchLifeDist");
            _wLifeVert = Q<Label>("Lbl_WatchLifeVert");
            _wLifeStacks = Q<Label>("Lbl_WatchLifeStacks");

            _wTask1 = Q<Label>("Lbl_WatchTask1");
            _wTask2 = Q<Label>("Lbl_WatchTask2");
            _wTask3 = Q<Label>("Lbl_WatchTask3");

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

            _wTask1 = Q<Label>("Lbl_WatchTask1");
            _wTask2 = Q<Label>("Lbl_WatchTask2");

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

            _tileTask1 = Q<Label>("Lbl_TileTask1");
            _tileTask2 = Q<Label>("Lbl_TileTask2");
            _tileTask3 = Q<Label>("Lbl_TileTask3");
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

            // Default tab
            SetStatsTab(true, force: true);

            _tasksBody = Q<Label>("Lbl_TasksBody"); // may be null now (we replaced it in UXML)
            _tasksList = Q<VisualElement>("TasksList");
            EnsureTasksHeaderBar();

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
            // Because we removed Distance + Time&Weather from the watch cycle,
            // the indices shift down.
            return idx switch
            {
                0 => "Home",
                1 => "Session",
                2 => "Lifetime",
                3 => "Tasks",
                4 => "Achievements",
                5 => "SOS",
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

            // Updates the Tasks page card list (this is why the Tasks page was empty).
            RefreshTasksCards();

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

            if (Q<Label>("Lbl_WatchAirHint") != null) // optional if you cached it; better to cache as _wAirHint
            {
                // While airborne show "AIR", while grounded show "LAST"
                var hint = Q<Label>("Lbl_WatchAirHint");
                hint.text = grounded ? "LAST AIR" : "IN-AIR";
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
            // Tasks (preview + progress)
            // -------------------------
            _taskDisplayBuffer.Clear();
            if (_progression != null)
                _progression.GetSessionTaskDisplays(_profile, _taskDisplayBuffer);

            int total = _taskDisplayBuffer.Count;
            int done = 0;
            float overallPct = 0f;

            for (int i = 0; i < total; i++)
            {
                var d = _taskDisplayBuffer[i];
                if (d.completed) done++;
                overallPct += Mathf.Clamp01(d.pct01);
            }
            if (total > 0) overallPct /= total;

            string PreviewText(TaskDisplay d)
            {
                if (d.claimed) return $"[CLAIMED] {d.title}";
                if (d.completed) return $"[READY] {d.title}";
                return $"{d.title} ({d.current:0}/{d.target:0})";
            }

            void SetPreview(Label lbl, int index)
            {
                if (lbl == null) return;

                if (index < total)
                {
                    lbl.style.display = DisplayStyle.Flex;
                    lbl.text = PreviewText(_taskDisplayBuffer[index]);
                }
                else
                {
                    lbl.style.display = DisplayStyle.None;
                    lbl.text = "";
                }
            }

            // Watch previews
            SetPreview(_wTask1, 0);
            SetPreview(_wTask2, 1);
            SetPreview(_wTask3, 2);

            // Home tile previews
            SetPreview(_tileTask1, 0);
            SetPreview(_tileTask2, 1);
            SetPreview(_tileTask3, 2);

            if (_tileTasksProgress != null)
                _tileTasksProgress.text = total <= 0 ? "0/0" : $"{done}/{total}";

            if (_tileTasksProgressFill != null)
                _tileTasksProgressFill.style.width = Length.Percent(Mathf.RoundToInt(overallPct * 100f));

            // Optional: keep the legacy Tasks page body label updated if it still exists (safe no-op if null)
            if (_tasksBody != null)
            {
                if (total == 0) _tasksBody.text = "(no tasks)";
                else
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < total; i++)
                        sb.AppendLine(PreviewText(_taskDisplayBuffer[i]));
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

        private void EnsureTaskCardCount(int count)
        {
            if (_tasksList == null) return;

            while (_taskCards.Count < count)
                _taskCards.Add(CreateTaskCard(_tasksList));

            // Hide extras (pool)
            for (int i = 0; i < _taskCards.Count; i++)
                _taskCards[i].root.style.display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private TaskCardRefs CreateTaskCard(VisualElement parent)
        {
            var card = new VisualElement();
            card.AddToClassList("task-card");

            // Top row: title + (badge + claim button)
            var top = new VisualElement();
            top.AddToClassList("task-row-top");

            var title = new Label("Task");
            title.AddToClassList("task-title");

            var right = new VisualElement();
            right.AddToClassList("task-row-right");

            var badge = new Label("In progress");
            badge.AddToClassList("task-badge");

            var claimBtn = new Button();
            claimBtn.text = "Turn In";
            claimBtn.AddToClassList("task-claim");
            claimBtn.style.display = DisplayStyle.None; // only shown when claimable

            right.Add(badge);
            right.Add(claimBtn);

            top.Add(title);
            top.Add(right);

            // Progress bar
            var bar = new VisualElement();
            bar.AddToClassList("task-progress-bar");

            var fill = new VisualElement();
            fill.AddToClassList("task-progress-fill");
            bar.Add(fill);

            // Bottom row: progress text + hint
            var bottom = new VisualElement();
            bottom.AddToClassList("task-row-bottom");

            var progressText = new Label("0/0 (0%)");
            progressText.AddToClassList("task-progress-text");

            var hint = new Label("");
            hint.AddToClassList("task-hint");

            bottom.Add(progressText);
            bottom.Add(hint);

            card.Add(top);
            card.Add(bar);
            card.Add(bottom);

            parent.Add(card);

            return new TaskCardRefs
            {
                root = card,
                title = title,
                badge = badge,
                claimBtn = claimBtn,
                fill = fill,
                progressText = progressText,
                hint = hint
            };

        }

        private void RefreshTasksCards()
        {
            EnsureTasksHeaderBar();

            if (_profile == null || _progression == null)
            {
                if (_tasksCurrencyLabel != null) _tasksCurrencyLabel.text = "0";
                if (_tasksClaimAllBtn != null) _tasksClaimAllBtn.SetEnabled(false);
                return;
            }

            _progression.GetSessionTaskDisplays(_profile, _taskDisplayBuffer);
            EnsureTaskCardCount(_taskDisplayBuffer.Count);

            int claimableCount = 0;
            int claimableSum = 0;

            for (int i = 0; i < _taskDisplayBuffer.Count; i++)
            {
                var d = _taskDisplayBuffer[i];
                var c = _taskCards[i];

                bool claimable = d.completed && !d.claimed;
                if (claimable)
                {
                    claimableCount++;
                    claimableSum += d.reward;
                }

                c.root.EnableInClassList("is-complete", d.completed);
                c.root.EnableInClassList("is-claimed", d.claimed);
                c.root.EnableInClassList("is-claimable", claimable);

                if (c.title != null) c.title.text = d.title;

                if (c.badge != null)
                {
                    if (d.claimed) c.badge.text = "Claimed";
                    else if (d.completed) c.badge.text = "Complete";
                    else c.badge.text = $"{(d.pct01 * 100f):0}%";
                }

                if (c.fill != null)
                    c.fill.style.width = new Length(d.pct01 * 100f, LengthUnit.Percent);

                if (c.progressText != null)
                {
                    c.progressText.text = d.completed
                        ? $"{d.target:0}/{d.target:0} (100%)"
                        : $"{d.current:0}/{d.target:0} ({(d.pct01 * 100f):0}%)";
                }

                if (c.hint != null)
                {
                    if (claimable) c.hint.text = $"Turn in for +{d.reward}";
                    else if (d.claimed) c.hint.text = "Done.";
                    else c.hint.text = "Keep going";
                }

                if (c.claimBtn != null)
                {
                    c.claimBtn.style.display = claimable ? DisplayStyle.Flex : DisplayStyle.None;
                    c.claimBtn.SetEnabled(claimable);
                    c.claimBtn.text = claimable ? $"+{d.reward}" : "Turn In";

                    c.claimBtn.userData = d.id;

                    if (c.claimBtn.ClassListContains("has-handler") == false)
                    {
                        c.claimBtn.AddToClassList("has-handler");
                        c.claimBtn.clicked += () =>
                        {
                            if (_progression == null || _profile == null) return;

                            string id = c.claimBtn.userData as string;
                            if (string.IsNullOrEmpty(id)) return;

                            if (_progression.TryClaimTask(id, out int rewardGranted))
                            {
                                RefreshTasksAndAchievements();
                                RefreshTasksCards(); // keep header + buttons correct immediately
                                Debug.Log($"[PhoneHUD] Claimed task {id} for {rewardGranted} currency.");
                            }
                        };
                    }
                }
            }

            // This label is now "claimable total" (not wallet currency).
            if (_tasksCurrencyLabel != null)
                _tasksCurrencyLabel.text = $"{claimableSum}";

            if (_tasksClaimAllBtn != null)
            {
                _tasksClaimAllBtn.SetEnabled(claimableCount > 0);
                _tasksClaimAllBtn.text = claimableCount > 0 ? $"Claim All ({claimableCount})" : "Claim All";
            }
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

        private void EnsureTasksHeaderBar()
        {
            if (_tasksList == null) return;

            // Avoid duplicates if BindUI runs again.
            if (_tasksHeaderBar != null && _tasksHeaderBar.parent != null)
                return;

            // Try find by name if it already exists.
            _tasksHeaderBar = _tasksList.Q<VisualElement>("TasksHeaderBar");
            if (_tasksHeaderBar != null) return;

            _tasksHeaderBar = new VisualElement { name = "TasksHeaderBar" };
            _tasksHeaderBar.AddToClassList("tasks-headerbar");

            _tasksCurrencyLabel = new Label("0");
            _tasksCurrencyLabel.name = "Lbl_TasksCurrency";
            _tasksCurrencyLabel.AddToClassList("tasks-currency");

            _tasksClaimAllBtn = new Button();
            _tasksClaimAllBtn.name = "Btn_ClaimAll";
            _tasksClaimAllBtn.text = "Claim All";
            _tasksClaimAllBtn.AddToClassList("tasks-claimall");

            _tasksClaimAllBtn.clicked += () =>
            {
                if (_progression == null || _profile == null) return;

                int total = _progression.ClaimAllCompleted();
                RefreshTasksAndAchievements();
                RefreshTasksCards(); // ensure the claimable sum becomes 0 immediately

                if (total > 0)
                    Debug.Log($"[PhoneHUD] Claimed {total} currency from completed tasks.");
            };

            // Layout: currency on left, claim-all on right.
            _tasksHeaderBar.Add(_tasksCurrencyLabel);
            _tasksHeaderBar.Add(_tasksClaimAllBtn);

            // Insert at top of list (above cards).
            _tasksList.Insert(0, _tasksHeaderBar);
        }

    }
}
