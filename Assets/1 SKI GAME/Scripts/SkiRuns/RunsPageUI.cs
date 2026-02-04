using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Map;
using SkiGame.Progression;
using TimeWeather;

namespace SkiGame.Runs
{
    public sealed class RunsPageUI : MonoBehaviour
    {
        // PhoneHUDController can hook this to route to Map + select run
        public Action<string> NavigateToMapRequested;

        private MapData _mapData;

        // UI refs
        private VisualElement _root;

        private VisualElement _tabWeeks;
        private VisualElement _tabRuns;

        private VisualElement _panelWeeks;
        private VisualElement _panelRuns;

        private VisualElement _weeksList; // RunsCalendarGrid

        // “toolbar” repurposed for Weeks + back
        private VisualElement _toolbar;
        private VisualElement _btnToolbarLeft;   // Btn_RunsPrevMonth (repurposed: back from day detail)
        private VisualElement _btnToolbarRight;  // Btn_RunsNextMonth (hidden)
        private Label _lblToolbarTitle;          // Lbl_RunsMonth (set to "Weeks")

        // Day detail
        private VisualElement _dayPanel;         // Panel_RunsDayDetail
        private VisualElement _btnPrevDay;       // Btn_RunsPrevDay
        private VisualElement _btnNextDay;       // Btn_RunsNextDay
        private Label _lblDay;                   // Lbl_RunsDay
        private VisualElement _dayList;          // RunsDayDetailList

        // Runs list
        private VisualElement _runsList;         // RunsByRunList

        // Data
        private PlayerStatsProfile _profile;
        private TimeController _time;

        private readonly Dictionary<string, RunRecordEntry> _recordsByRunId = new();
        private readonly Dictionary<int, List<AttemptRef>> _attemptsByDayOfYear = new(); // key: absolute dayOfYear (0-based)
        private int _activeDayOfYear = -1;

        // Injected “Back” button in day detail toolbar
        private VisualElement _btnDayBack;

        private readonly List<RunDef> _runs = new();

        private int _currentWeekIndex;
        private int _expandedWeekIndex = -1;

        private const string Pref_CalendarBaseDay = "skigame.runs.calendarBaseDay";
        private int _calendarBaseDayOfYear;
        private bool _calendarBaseLoaded;

        // DayCount conversion robustness (handles dayCount being 0-based or 1-based)
        private int _dayCountConversionOffset = 0;
        private VisualElement _btnBackToWeeks;

        private string _pendingFocusRunId;

        private VisualElement _activePanel;
        private Label _activeName;
        private Label _activeMeta;
        private VisualElement _activeFill;

        private RunProgressTracker _runProgressTracker;
        private bool _subscribedToRunEvents;

        [SerializeField, Tooltip("How often the Runs page checks for profile/run record changes (seconds).")]
        private float profilePollInterval = 0.5f;

        private float _nextProfilePollTime;
        private int _lastRunRecordsHash;
        private bool _isWeeksTab = true;

        private const float MinCoverageFractionToShow = 0.25f;

        // -------------------------
        // Public API (required by PhoneHUDController)
        // -------------------------

        public void SetMapData(MapData mapData)
        {
            _mapData = mapData;
            RebuildRunDefinitions();
            RebuildCaches();
            RebuildAll(force: true);
        }

        /// <summary>
        /// Called by PhoneHUDController when the Map info panel wants to jump to a run entry.
        /// </summary>
        public void RequestFocusRun(string runId)
        {
            _pendingFocusRunId = runId;
            SetTab(isWeeks: false);
            RebuildRunsList(); // ensures it exists
            TryFocusRunElement(runId);
        }

        // -------------------------
        // Unity
        // -------------------------

        private void OnEnable()
        {
            TryBindUI();
            ResolveRefs();
            BindRunEvents();

            RebuildRunDefinitions();
            RebuildCaches();

            _lastRunRecordsHash = ComputeRunRecordsHash();
            _nextProfilePollTime = Time.unscaledTime + Mathf.Max(0.1f, profilePollInterval);

            RebuildAll(force: true);

        }

        private void OnDisable()
        {
            UnbindRunEvents();
        }

        private void Update()
        {
            // Lightweight polling: if profile/time appears later, bind and refresh.
            if (_profile == null || _time == null)
            {
                ResolveRefs();
                if (_profile == null || _time == null) return;

                RebuildRunDefinitions();
                RebuildCaches();
                RebuildAll(force: true);
                return;
            }

            // If the live profile instance changed (reset/load), force a full rebuild immediately.
            var mgr = PlayerStatsManager.Instance;
            var liveProfile = mgr != null ? mgr.Profile : null;

            if (liveProfile != null && !ReferenceEquals(_profile, liveProfile))
            {
                _profile = liveProfile;

                RebuildRunDefinitions();
                RebuildCaches();

                _lastRunRecordsHash = ComputeRunRecordsHash();
                _nextProfilePollTime = Time.unscaledTime + Mathf.Max(0.1f, profilePollInterval);

                RebuildAll(force: true);
            }

            // Keep current week accurate
            int wk = GetCurrentWeekIndexRelative();
            if (wk != _currentWeekIndex)
            {
                _currentWeekIndex = wk;
                RebuildWeeksList();
            }

            // If RunProgressTracker wasn't present during OnEnable(), keep trying.
            // (Phone pages are usually shown/hidden via UI Toolkit, so OnEnable won't re-run.)
            if (!_subscribedToRunEvents)
                BindRunEvents();

            // Lightweight profile polling safety-net (catches missed events / other systems writing runRecords)
            if (Time.unscaledTime >= _nextProfilePollTime)
            {
                _nextProfilePollTime = Time.unscaledTime + Mathf.Max(0.1f, profilePollInterval);

                int hash = ComputeRunRecordsHash();
                if (hash != _lastRunRecordsHash)
                {
                    _lastRunRecordsHash = hash;

                    RebuildRunDefinitions(); // ensures any new runIds from records get a RunDef entry
                    RebuildCaches();

                    if (_isWeeksTab)
                        RebuildWeeksList();
                    else
                        RebuildRunsList();

                    if (_activeDayOfYear >= 0)
                        RebuildDayDetailList();
                }
            }

            RefreshActiveRunHeader();
            RefreshActiveRunPanel();

        }

        // -------------------------
        // Binding / refs
        // -------------------------

        private void BindRunEvents()
        {
            if (_subscribedToRunEvents) return;

            if (_runProgressTracker == null)
                _runProgressTracker = FindObjectOfType<RunProgressTracker>();

            if (_runProgressTracker == null) return;

            _runProgressTracker.OnAttemptLogged += HandleAttemptLogged;
            _runProgressTracker.OnRunCompleted += HandleRunCompleted; // keep if you still want completion-only hooks

            _subscribedToRunEvents = true;
        }

        private void UnbindRunEvents()
        {
            if (!_subscribedToRunEvents) return;

            if (_runProgressTracker != null)
            {
                _runProgressTracker.OnAttemptLogged -= HandleAttemptLogged;
                _runProgressTracker.OnRunCompleted -= HandleRunCompleted;
            }

            _subscribedToRunEvents = false;
        }

        private void HandleRunCompleted(RunProgressTracker.RunCompletedInfo info)
        {
            var mgr = PlayerStatsManager.Instance;
            Debug.Log($"[RunsPageUI] OnRunCompleted received runId={info.runId}  profileMatch={ReferenceEquals(_profile, mgr != null ? mgr.Profile : null)}");

            RebuildCaches();
            RebuildWeeksList();
            RebuildRunsList();

            if (_activeDayOfYear >= 0) RebuildDayDetailList();
        }

        private void HandleAttemptLogged(RunProgressTracker.AttemptLoggedInfo info)
        {
            // Rebuild caches + refresh whatever is visible.
            RebuildCaches();

            if (_isWeeksTab)
                RebuildWeeksList();
            else
                RebuildRunsList();

            if (_activeDayOfYear >= 0) RebuildDayDetailList();
        }

        private void TryBindUI()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;

            _root = doc.rootVisualElement;

            _tabWeeks = _root.Q<VisualElement>("Btn_RunsTabCalendar");
            _tabRuns = _root.Q<VisualElement>("Btn_RunsTabByRun");

            _panelWeeks = _root.Q<VisualElement>("Panel_RunsCalendar");
            _panelRuns = _root.Q<VisualElement>("Panel_RunsByRun");

            _toolbar = _root.Q<VisualElement>("RunsMonthToolbar");
            _btnToolbarLeft = _root.Q<VisualElement>("Btn_RunsPrevMonth");
            _btnToolbarRight = _root.Q<VisualElement>("Btn_RunsNextMonth");
            _lblToolbarTitle = _root.Q<Label>("Lbl_RunsMonth");

            _weeksList = _root.Q<VisualElement>("RunsCalendarGrid");

            _dayPanel = _root.Q<VisualElement>("Panel_RunsDayDetail");
            _btnPrevDay = _root.Q<VisualElement>("Btn_RunsPrevDay");
            _btnNextDay = _root.Q<VisualElement>("Btn_RunsNextDay");
            _lblDay = _root.Q<Label>("Lbl_RunsDay");
            _dayList = _root.Q<VisualElement>("RunsDayDetailList");

            _runsList = _root.Q<VisualElement>("RunsByRunList");

            _activePanel = _root.Q<VisualElement>("RunsActivePanel");
            _activeName = _root.Q<Label>("Lbl_RunsActiveName");
            _activeMeta = _root.Q<Label>("Lbl_RunsActiveMeta");
            _activeFill = _root.Q<VisualElement>("RunsActiveFill");

            // Tab clicks
            if (_tabWeeks != null) _tabWeeks.RegisterCallback<PointerDownEvent>(_ => SetTab(isWeeks: true));
            if (_tabRuns != null) _tabRuns.RegisterCallback<PointerDownEvent>(_ => SetTab(isWeeks: false));

            // Weeks toolbar: use left button as “Back” when day panel open
            if (_btnToolbarLeft != null) _btnToolbarLeft.RegisterCallback<PointerDownEvent>(_ => CloseDayDetail());
            if (_btnToolbarRight != null) _btnToolbarRight.style.display = DisplayStyle.None; // hide right arrow

            if (_lblToolbarTitle != null) _lblToolbarTitle.text = "Weeks";

            // Day nav
            if (_btnPrevDay != null) _btnPrevDay.RegisterCallback<PointerDownEvent>(_ => StepDay(-1));
            if (_btnNextDay != null) _btnNextDay.RegisterCallback<PointerDownEvent>(_ => StepDay(+1));

            // Default view
            SetTab(isWeeks: true);
        }

        private void ResolveRefs()
        {
            // Profile can be replaced at runtime (reset/load). Always re-check reference.
            var mgr = PlayerStatsManager.Instance;
            var liveProfile = mgr != null ? mgr.Profile : null;

            if (liveProfile != null && !ReferenceEquals(_profile, liveProfile))
            {
                _profile = liveProfile;

                // Force caches/UI refresh on next Update tick.
                _lastRunRecordsHash = 0;
                _nextProfilePollTime = 0f;
            }

            if (_time == null)
                _time = TimeController.instance != null ? TimeController.instance : FindObjectOfType<TimeController>();

            if (_time != null)
            {
                // Robustly detect whether dayCount needs -1 to match month/day.
                _dayCountConversionOffset = DetermineDayCountConversionOffset();

                if (!_calendarBaseLoaded)
                {
                    _calendarBaseDayOfYear = PlayerPrefs.GetInt(Pref_CalendarBaseDay, 0);
                    _calendarBaseDayOfYear = Mathf.Clamp(_calendarBaseDayOfYear, 0, _time.dayCount);
                    _calendarBaseLoaded = true;
                }

                _currentWeekIndex = GetCurrentWeekIndexRelative();

                // If never expanded yet, default to current week so Weeks doesn't look empty.
                if (_expandedWeekIndex < 0)
                    _expandedWeekIndex = _currentWeekIndex;
            }
        }

        // -------------------------
        // Tabs / rebuild
        // -------------------------

        private void SetTab(bool isWeeks)
        {
            _isWeeksTab = isWeeks;

            _panelWeeks?.EnableInClassList("is-hidden", !isWeeks);
            _panelRuns?.EnableInClassList("is-hidden", isWeeks);

            _tabWeeks?.EnableInClassList("is-on", isWeeks);
            _tabRuns?.EnableInClassList("is-on", !isWeeks);

            if (isWeeks)
            {
                // If nothing is expanded yet, expand the current week so the tab doesn't look empty.
                if (_expandedWeekIndex < 0 && _time != null)
                    _expandedWeekIndex = Mathf.Max(0, _time.dayCount / 7);

                CloseDayDetail();
                RebuildWeeksList();
            }
            else
            {
                RebuildRunsList();
                if (!string.IsNullOrEmpty(_pendingFocusRunId))
                {
                    TryFocusRunElement(_pendingFocusRunId);
                    _pendingFocusRunId = null;
                }
            }
        }

        private void RebuildAll(bool force)
        {
            if (_weeksList == null || _runsList == null) return;
            RebuildWeeksList();
            RebuildRunsList();
        }

        // -------------------------
        // Run definitions (ALL runs, even if never completed)
        // -------------------------

        private void RebuildRunDefinitions()
        {
            _runs.Clear();

            // Prefer ordering from MapData (this is your “runs version” ordering in practice)
            if (_mapData != null)
            {
                int order = 0;
                foreach (var p in _mapData.Polylines)
                {
                    if (!p.IsValid) continue;
                    if (p.lineType != MapLineType.SkiRun) continue;

                    _runs.Add(new RunDef
                    {
                        runId = p.id,
                        name = string.IsNullOrWhiteSpace(p.displayName) ? "Run" : p.displayName,
                        order = order++
                    });
                }
            }

            // Merge in any scene SkiRunLines that might not be baked
#if UNITY_2023_1_OR_NEWER
            var sceneRuns = FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
            var sceneRuns = FindObjectsOfType<SkiRunLine>();
#endif
            if (sceneRuns != null)
            {
                foreach (var r in sceneRuns)
                {
                    if (r == null) continue;
                    if (string.IsNullOrWhiteSpace(r.RunId)) continue;

                    if (_runs.Any(x => x.runId == r.RunId))
                        continue;

                    _runs.Add(new RunDef
                    {
                        runId = r.RunId,
                        name = string.IsNullOrWhiteSpace(r.RunName) ? "Run" : r.RunName,
                        order = 100000 + _runs.Count
                    });
                }
            }

            // Stable ordering
            _runs.Sort((a, b) =>
            {
                int o = a.order.CompareTo(b.order);
                if (o != 0) return o;
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
        }

        // -------------------------
        // Cache attempts by in-game day (TimeController fields)
        // -------------------------

        private int GetRelativeDayCount()
        {
            if (_time == null) return 0;
            return Mathf.Max(0, _time.dayCount - _calendarBaseDayOfYear);
        }

        private int GetCurrentWeekIndexRelative()
        {
            return Mathf.Max(0, GetRelativeDayCount() / 7);
        }

        private int ToAbsoluteDayOfYear(int relativeDayOfYear)
        {
            return _calendarBaseDayOfYear + Mathf.Max(0, relativeDayOfYear);
        }

        private bool TryGetDayOfYear(GameDayKey key, out int dayOfYear)
        {
            dayOfYear = 0;

            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0)
                return false;

            int m = Mathf.Clamp(key.monthIndex, 0, _time.monthPresets.Length - 1);
            int d = Mathf.Max(1, key.dayOfMonth);

            int sum = 0;
            for (int i = 0; i < m; i++)
                sum += Mathf.Max(1, _time.monthPresets[i].daysInMonth);

            dayOfYear = sum + (d - 1);
            return true;
        }

        private bool IsFutureDay(GameDayKey key)
        {
            if (_time == null) return false;
            if (!TryGetDayOfYear(key, out int doy)) return false;
            return doy > _time.dayCount;
        }

        private static void SetEnabledAndVisual(VisualElement ve, bool enabled)
        {
            if (ve == null) return;
            ve.SetEnabled(enabled);
            ve.EnableInClassList("is-disabled", !enabled);
            ve.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
        }

        private bool IsFutureDayOfYear(int dayOfYear)
        {
            if (_time == null) return false;
            return dayOfYear > _time.dayCount;
        }

        private GameDayKey ResolveAttemptDayKey(RunAttemptEntry a)
        {
            // If attempt has a valid in-game date, use it
            if (a != null && a.gameYear > 0 && a.gameMonthIndex >= 0 && a.gameDayOfMonth > 0)
                return new GameDayKey(a.gameYear, a.gameMonthIndex, a.gameDayOfMonth);

            // Otherwise: coerce undated attempts into the CURRENT day so Weeks fills reliably.
            if (_time != null && TryConvertDayOfYear(_time.dayCount, out int m, out int d))
                return new GameDayKey(_time.currentYear, m, d);

            return new GameDayKey(1, 0, 1);
        }

        private void RebuildCaches()
        {
            _recordsByRunId.Clear();
            _attemptsByDayOfYear.Clear();

            if (_profile == null || _profile.runRecords == null)
                return;

            for (int r = 0; r < _profile.runRecords.Count; r++)
            {
                var rec = _profile.runRecords[r];
                if (rec == null || string.IsNullOrEmpty(rec.runId))
                    continue;

                _recordsByRunId[rec.runId] = rec;

                if (rec.attempts == null || rec.attempts.Count == 0)
                    continue;

                for (int i = 0; i < rec.attempts.Count; i++)
                {
                    var a = rec.attempts[i];
                    if (a == null) continue;

                    int doy = ResolveAttemptDayOfYear(a);

                    if (!_attemptsByDayOfYear.TryGetValue(doy, out var list) || list == null)
                    {
                        list = new List<AttemptRef>(4);
                        _attemptsByDayOfYear[doy] = list;
                    }

                    list.Add(new AttemptRef(rec.runId, a));
                }
            }

            // Sort each day list by time-of-day (ascending)
            foreach (var kv in _attemptsByDayOfYear)
                kv.Value.Sort((x, y) => x.attempt.gameTimeOfDay.CompareTo(y.attempt.gameTimeOfDay));
        }

        private int ResolveAttemptDayOfYear(RunAttemptEntry a)
        {
            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0)
                return 0;

            // Preferred: use the attempt’s stored date
            if (a != null && a.gameMonthIndex >= 0 && a.gameDayOfMonth > 0)
            {
                var key = new GameDayKey(0, a.gameMonthIndex, a.gameDayOfMonth);
                if (TryGetDayOfYear(key, out int doy))
                    return doy;
            }

            // Fallback: coerce undated attempts into current day
            return Mathf.Clamp(_time.dayCount, 0, 366);
        }

        private int ComputeRunRecordsHash()
        {
            if (_profile == null || _profile.runRecords == null) return 0;

            unchecked
            {
                int h = _profile.runRecords.Count;

                for (int i = 0; i < _profile.runRecords.Count; i++)
                {
                    var rec = _profile.runRecords[i];
                    if (rec == null) continue;

                    h = (h * 397) ^ (rec.runId != null ? rec.runId.GetHashCode() : 0);
                    h = (h * 397) ^ rec.timesCompleted;
                    h = (h * 397) ^ rec.timesCompletedClean;

                    // Include aggregates that change over time
                    h = (h * 397) ^ rec.bestTimeSeconds.GetHashCode();
                    h = (h * 397) ^ rec.bestCleanTimeSeconds.GetHashCode();
                    h = (h * 397) ^ rec.bestTopSpeedMps.GetHashCode();
                    h = (h * 397) ^ rec.totalDistanceMeters.GetHashCode();

                    int count = (rec.attempts != null) ? rec.attempts.Count : 0;
                    h = (h * 397) ^ count;

                    // Critical: include last attempt timestamp so capped lists still trigger UI refresh
                    if (count > 0)
                    {
                        var last = rec.attempts[count - 1];
                        if (last != null)
                            h = (h * 397) ^ last.completedUtc.unixSeconds.GetHashCode();
                    }
                }

                return h;
            }
        }

        // -------------------------
        // Weeks list UI
        // -------------------------

        private void RebuildWeeksList()
        {
            if (_weeksList == null || _time == null) return;

            _weeksList.Clear();

            int relDays = GetRelativeDayCount();
            int weeksSoFar = Mathf.Max(1, (relDays / 7) + 1);

            if (_expandedWeekIndex >= weeksSoFar) _expandedWeekIndex = weeksSoFar - 1;
            if (_expandedWeekIndex < -1) _expandedWeekIndex = -1;

            for (int weekIndex = weeksSoFar - 1; weekIndex >= 0; weekIndex--)
            {
                int weekNumber = weekIndex + 1;

                var weekCard = new VisualElement();
                weekCard.AddToClassList("runs-week-card");

                var header = new VisualElement();
                header.AddToClassList("runs-week-header");

                var title = new Label($"Week {weekNumber}");
                title.AddToClassList("runs-week-title");

                int attemptCount = CountAttemptsInWeek(weekIndex);
                var badge = new Label(attemptCount.ToString());
                badge.AddToClassList("runs-week-badge");

                header.pickingMode = PickingMode.Position;
                title.pickingMode = PickingMode.Ignore;
                badge.pickingMode = PickingMode.Ignore;

                int capturedWeekIndex = weekIndex;
                header.AddManipulator(new Clickable(() =>
                {
                    _expandedWeekIndex = (_expandedWeekIndex == capturedWeekIndex) ? -1 : capturedWeekIndex;
                    RebuildWeeksList();
                }));

                header.Add(title);
                header.Add(badge);

                var daysRow = new VisualElement();
                daysRow.AddToClassList("runs-week-days");

                bool expanded = (_expandedWeekIndex == weekIndex);
                daysRow.EnableInClassList("is-hidden", !expanded);

                for (int dow = 0; dow < 7; dow++)
                {
                    int relDayOfYear = weekIndex * 7 + dow;
                    int absDayOfYear = ToAbsoluteDayOfYear(relDayOfYear);

                    if (!TryConvertDayOfYear(absDayOfYear, out int monthIndex, out int dayOfMonth))
                        continue;

                    bool isFuture = absDayOfYear > _time.dayCount;
                    bool hasRuns = _attemptsByDayOfYear.TryGetValue(absDayOfYear, out var list) && list != null && list.Count > 0;

                    var tile = BuildDayTile(dow, dayOfMonth, hasRuns, isFuture);

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
                            OpenDayDetail(absDayOfYear);
                        });
                    }

                    daysRow.Add(tile);
                }

                weekCard.Add(header);
                weekCard.Add(daysRow);
                _weeksList.Add(weekCard);
            }
        }

        private int CountAttemptsInWeek(int weekIndex)
        {
            if (_time == null) return 0;

            int count = 0;
            for (int dow = 0; dow < 7; dow++)
            {
                int absDay = ToAbsoluteDayOfYear(weekIndex * 7 + dow);
                if (_attemptsByDayOfYear.TryGetValue(absDay, out var list) && list != null)
                    count += list.Count;
            }
            return count;
        }

        private VisualElement BuildDayTile(int dow, int dayOfMonth, bool hasRuns, bool isFuture)
        {
            var tile = new VisualElement();
            tile.AddToClassList("runs-day-tile");
            if (hasRuns) tile.AddToClassList("has-runs");
            if (isFuture) tile.AddToClassList("is-future");

            string dowStr = ((DayOfWeek)(((int)DayOfWeek.Monday + dow) % 7)).ToString().Substring(0, 3);

            var l1 = new Label(dowStr);
            l1.AddToClassList("runs-day-dow");

            var l2 = new Label(dayOfMonth.ToString());
            l2.AddToClassList("runs-day-dom");

            tile.Add(l1);
            tile.Add(l2);

            return tile;
        }

        private VisualElement BuildDayRunGroupCard(string runId, string runName, List<RunAttemptEntry> attemptsForRun)
        {
            // Determine summary state for header
            bool anyCompletion = false;
            float bestSeg = 0f;

            for (int i = 0; i < attemptsForRun.Count; i++)
            {
                var a = attemptsForRun[i];
                if (a == null) continue;

                if (a.isCompletion) anyCompletion = true;

                float seg = GetCoveredFraction01(a);
                if (!a.isCompletion && seg >= MinCoverageFractionToShow)
                    bestSeg = Mathf.Max(bestSeg, seg);
            }

            float completion01 = anyCompletion ? 1f : bestSeg;
            string countText = anyCompletion ? $"{attemptsForRun.Count}×" : $"{bestSeg * 100f:0}%";

            // Reuse the same run-card structure/styles as the Runs tab for cohesion
            var card = new VisualElement();
            card.AddToClassList("runs-run-card");
            card.AddToClassList("runs-day-run-card");

            var header = new VisualElement();
            header.AddToClassList("runs-run-header");
            header.AddToClassList("runs-day-run-header");

            var name = new Label(runName);
            name.AddToClassList("runs-run-name");

            // Progress bar (optional but nice: mirrors runs tab)
            var bar = new VisualElement();
            bar.AddToClassList("runs-run-progress");

            var fill = new VisualElement();
            fill.AddToClassList("runs-run-progress-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(completion01) * 100f);
            bar.Add(fill);

            var count = new Label(countText);
            count.AddToClassList("runs-run-count");
            if (anyCompletion) count.AddToClassList("is-complete");

            header.Add(name);
            header.Add(bar);
            header.Add(count);

            // Detail section (attempt list)
            var detail = new VisualElement();
            detail.AddToClassList("runs-run-detail");
            detail.EnableInClassList("is-hidden", true);

            // Sort attempts within the day (newest first feels consistent with Runs tab,
            // but you can flip to ascending if you prefer a timeline)
            attemptsForRun.Sort((a, b) => b.gameTimeOfDay.CompareTo(a.gameTimeOfDay));

            int shown = 0;
            for (int i = 0; i < attemptsForRun.Count; i++)
            {
                var a = attemptsForRun[i];
                if (a == null) continue;

                if (!a.isCompletion && GetCoveredFraction01(a) < MinCoverageFractionToShow)
                    continue;

                // In day view we already know the run, so "showDay" can be false for compactness.
                // If you want the header to show the run name only, keep showDay=false here.
                detail.Add(BuildAttemptCardOrdered(a, showDayWeek: false));
                shown++;
            }

            if (shown == 0)
            {
                var none = new Label("No visible attempts (below threshold).");
                none.AddToClassList("runs-empty");
                detail.Add(none);
            }

            // Expand/collapse like Runs tab
            header.RegisterCallback<PointerDownEvent>(evt =>
            {
                bool isHidden = detail.ClassListContains("is-hidden");
                detail.EnableInClassList("is-hidden", !isHidden);
                evt.StopPropagation();
            });

            card.Add(header);
            card.Add(detail);

            return card;
        }

        // -------------------------
        // Day detail UI (replaces weeks list)
        // -------------------------

        private void OpenDayDetail(int absDayOfYear)
        {
            if (_time != null && IsFutureDayOfYear(absDayOfYear))
                return;

            _activeDayOfYear = absDayOfYear;

            _dayPanel?.EnableInClassList("is-hidden", false);
            _weeksList?.EnableInClassList("is-hidden", true);

            EnsureDayBackButton();

            if (_lblDay != null && TryConvertDayOfYear(absDayOfYear, out int m, out int d))
                _lblDay.text = $"{SafeMonthName(m)} {d}";
            else if (_lblDay != null)
                _lblDay.text = "Day";

            RebuildDayDetailList();
            UpdateDayNavButtons();
        }

        private void RebuildDayDetailList()
        {
            if (_dayList == null) return;

            _dayList.Clear();

            if (_activeDayOfYear < 0)
                return;

            if (!_attemptsByDayOfYear.TryGetValue(_activeDayOfYear, out var list) || list == null || list.Count == 0)
            {
                var empty = new Label("No run attempts recorded on this day.");
                empty.AddToClassList("runs-empty");
                _dayList.Add(empty);
                return;
            }

            // Group attempts by runId
            var byRun = new Dictionary<string, List<RunAttemptEntry>>(16);

            for (int i = 0; i < list.Count; i++)
            {
                var ar = list[i];
                if (ar.attempt == null || string.IsNullOrEmpty(ar.runId))
                    continue;

                if (!byRun.TryGetValue(ar.runId, out var runAttempts))
                {
                    runAttempts = new List<RunAttemptEntry>(4);
                    byRun.Add(ar.runId, runAttempts);
                }

                runAttempts.Add(ar.attempt);
            }

            // Sort groups: completed runs first, then by attempt count (desc), then by name
            var groups = byRun
                .Select(kv =>
                {
                    bool anyCompletion = kv.Value.Any(a => a != null && a.isCompletion);
                    int count = kv.Value.Count;
                    string name = ResolveRunName(kv.Key);
                    return (runId: kv.Key, name, anyCompletion, count, attempts: kv.Value);
                })
                .OrderByDescending(g => g.anyCompletion)
                .ThenByDescending(g => g.count)
                .ThenBy(g => g.name)
                .ToList();

            foreach (var g in groups)
            {
                _dayList.Add(BuildDayRunGroupCard(g.runId, g.name, g.attempts));
            }

            UpdateDayNavButtons();
        }

        private void CloseDayDetail()
        {
            _activeDayOfYear = -1;
            _dayPanel?.EnableInClassList("is-hidden", true);
            _weeksList?.EnableInClassList("is-hidden", false);
        }

        private void StepDay(int delta)
        {
            if (_time == null || _activeDayOfYear < 0) return;

            int next = _activeDayOfYear + delta;
            if (next < 0) return;
            if (IsFutureDayOfYear(next)) return;

            OpenDayDetail(next);
        }

        private void UpdateDayNavButtons()
        {
            if (_time == null || _activeDayOfYear < 0) return;

            bool canPrev = _activeDayOfYear > 0;
            bool canNext = _activeDayOfYear < _time.dayCount;

            SetEnabledAndVisual(_btnPrevDay, canPrev);
            SetEnabledAndVisual(_btnNextDay, canNext);
        }

        private void EnsureDayBackButton()
        {
            if (_dayPanel == null) return;

            if (_btnDayBack != null)
                return;

            var toolbar = _dayPanel.Q<VisualElement>(null, "runs-day-toolbar");
            if (toolbar == null) return;

            var btn = new VisualElement { name = "Btn_RunsBackToWeeks" };
            btn.AddToClassList("map-toolbtn");
            btn.AddToClassList("map-toolbtn-icon");
            btn.AddToClassList("runs-day-backbtn");

            var lbl = new Label("↩");
            lbl.AddToClassList("map-toolbtn-text");

            btn.Add(lbl);
            toolbar.Insert(0, btn);

            btn.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
                CloseDayDetail();
            });

            _btnDayBack = btn;
        }

        // -------------------------
        // Runs list UI (ALL runs, expandable, includes “Navigate to Map”)
        // -------------------------

        private VisualElement BuildAttemptItem(string runName, RunAttemptEntry a)
        {
            var item = new VisualElement();
            item.AddToClassList("runs-attempt-item");

            // Head
            var head = new VisualElement();
            head.AddToClassList("runs-attempt-head");

            var timeLbl = new Label(FormatClock(a != null ? a.gameTimeOfDay : 0f));
            timeLbl.AddToClassList("runs-attempt-time");

            string status = (a != null && a.isCompletion) ? "Complete" : "Segment";
            var statusLbl = new Label(status);
            statusLbl.AddToClassList("runs-attempt-status");

            head.Add(timeLbl);
            head.Add(statusLbl);

            // Segment bar (entry -> exit), plus markers for exact start/end.
            float start01 = 0f;
            float end01 = 0f;

            if (a != null)
            {
                // Prefer entry/exit fractions (they represent where the player started/left).
                start01 = (a.entryFraction01 > 0f) ? a.entryFraction01 : a.coveredMinFraction01;
                end01 = (a.exitFraction01 > 0f) ? a.exitFraction01 : a.coveredMaxFraction01;

                // If both are 0 but meters exist, derive.
                if ((start01 <= 0f && end01 <= 0f) && a.runLengthMeters > 0.001f)
                {
                    start01 = Mathf.Clamp01(a.entryDistanceMeters / a.runLengthMeters);
                    end01 = Mathf.Clamp01(a.exitDistanceMeters / a.runLengthMeters);
                }
            }

            start01 = Mathf.Clamp01(start01);
            end01 = Mathf.Clamp01(end01);

            float lo = Mathf.Min(start01, end01);
            float hi = Mathf.Max(start01, end01);

            var bar = new VisualElement();
            bar.AddToClassList("runs-attempt-bar");

            var seg = new VisualElement();
            seg.AddToClassList("runs-attempt-seg");
            seg.style.left = Length.Percent(lo * 100f);
            seg.style.width = Length.Percent(Mathf.Max(0.5f, (hi - lo) * 100f)); // tiny min width so small segments are visible

            var mStart = new VisualElement();
            mStart.AddToClassList("runs-attempt-marker");
            mStart.AddToClassList("is-start");
            mStart.style.left = Length.Percent(start01 * 100f);

            var mEnd = new VisualElement();
            mEnd.AddToClassList("runs-attempt-marker");
            mEnd.AddToClassList("is-end");
            mEnd.style.left = Length.Percent(end01 * 100f);

            bar.Add(seg);
            bar.Add(mStart);
            bar.Add(mEnd);

            // Metrics row (compact, labeled by formatting)
            var metrics = new VisualElement();
            metrics.AddToClassList("runs-attempt-metrics");

            string duration = FormatSeconds(a != null ? a.timeSeconds : 0f);
            string speed = FormatSpeed(a != null ? a.topSpeedMps : 0f);
            string stacks = (a != null) ? $"{a.stacks}x" : "--";

            var left = new Label($"{duration}");
            left.AddToClassList("runs-attempt-metric");

            var right = new Label($"{speed} • {stacks}");
            right.AddToClassList("runs-attempt-metric");
            right.AddToClassList("is-muted");

            metrics.Add(left);
            metrics.Add(right);

            // Optional run name only if you want it in the day list (keeps runs tab minimal)
            if (!string.IsNullOrEmpty(runName))
            {
                var rn = new Label(runName);
                rn.AddToClassList("runs-attempt-runname");
                item.Add(rn);
            }

            item.Add(head);
            item.Add(bar);
            item.Add(metrics);

            return item;
        }

        private void RebuildRunsList()
        {
            if (_runsList == null) return;

            _runsList.Clear();

            // Sort runs so:
            // 1) Completed runs appear first
            // 2) Most recently attempted runs are at the top
            // 3) Never-attempted runs sink to the bottom (especially uncompleted)
            var orderedRuns = _runs
                .Select(rd =>
                {
                    RunRecordEntry rec = _recordsByRunId.TryGetValue(rd.runId, out var r) ? r : null;

                    bool hasAnyAttempt = rec != null && rec.attempts != null && rec.AttemptCount > 0;

                    bool isCompleted = false;
                    long lastAttemptKey = long.MinValue;

                    if (rec != null)
                    {
                        // Completed if timesCompleted>0 OR any completion attempt exists.
                        isCompleted = rec.timesCompleted > 0;

                        if (rec.attempts != null && rec.AttemptCount > 0)
                        {
                            for (int i = 0; i < rec.AttemptCount; i++)
                            {
                                var a = rec.attempts[i];
                                if (a == null) continue;

                                if (a.isCompletion) isCompleted = true;

                                // Build sortable key: year, day-of-year, time-of-day
                                int doy = 0;
                                if (a.gameMonthIndex >= 0 && a.gameDayOfMonth > 0)
                                {
                                    var key = new GameDayKey(a.gameYear, a.gameMonthIndex, a.gameDayOfMonth);
                                    if (!TryGetDayOfYear(key, out doy))
                                        doy = 0;
                                }

                                // timeOfDay is float hours in your system; quantize to minutes for stable sorting
                                int todMin = Mathf.RoundToInt(a.gameTimeOfDay * 60f);
                                long k = ((long)a.gameYear * 1000000L) + ((long)doy * 1000L) + (long)todMin;

                                if (k > lastAttemptKey)
                                    lastAttemptKey = k;
                            }
                        }
                    }

                    // Never-attempted should come last.
                    // Put them in a very low band.
                    if (!hasAnyAttempt)
                        lastAttemptKey = long.MinValue;

                    return new
                    {
                        rd,
                        isCompleted,
                        hasAnyAttempt,
                        lastAttemptKey
                    };
                })
                .OrderByDescending(x => x.isCompleted)         // completed first
                .ThenByDescending(x => x.lastAttemptKey)      // most recent attempt first
                .ThenByDescending(x => x.hasAnyAttempt)       // attempted before never-attempted (redundant but explicit)
                .ThenBy(x => x.rd.order)                      // stable fallback (MapData order)
                .ThenBy(x => x.rd.name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.rd)
                .ToList();

            foreach (var rd in orderedRuns)
            {
                var rec = _recordsByRunId.TryGetValue(rd.runId, out var r) ? r : null;

                int completedCount = rec != null ? Mathf.Max(0, rec.timesCompleted) : 0;
                int attemptCount = (rec != null && rec.attempts != null) ? rec.AttemptCount : 0;

                // "Completion %" for this run entry:
                // - If any completion attempt exists (or timesCompleted > 0), treat as 100%
                // - Otherwise use the best segment coverage (above the visibility threshold)
                float completion01 = 0f;

                string countText = "—";

                if (attemptCount > 0)
                {
                    bool anyCompletion = false;
                    float bestSeg = 0f;

                    for (int i = 0; i < attemptCount; i++)
                    {
                        var a = rec.attempts[i];
                        if (a == null) continue;

                        if (a.isCompletion) anyCompletion = true;

                        float c = GetCoveredFraction01(a);
                        if (!a.isCompletion && c >= MinCoverageFractionToShow)
                            bestSeg = Mathf.Max(bestSeg, c);
                    }

                    countText = anyCompletion ? $"{attemptCount}×" : $"{bestSeg * 100f:0}%";
                    completion01 = anyCompletion ? 1f : bestSeg;
                }
                else if (completedCount > 0)
                {
                    countText = $"{completedCount}×";
                    completion01 = 1f;
                }

                // Header row
                var card = new VisualElement();
                card.AddToClassList("runs-run-card");

                var header = new VisualElement();
                header.AddToClassList("runs-run-header");

                var name = new Label(rd.name);
                name.AddToClassList("runs-run-name");

                var count = new Label(countText);
                count.AddToClassList("runs-run-count");

                // Progress bar between run name and the count/percentage label.
                var bar = new VisualElement();
                bar.AddToClassList("runs-run-progress");

                var fill = new VisualElement();
                fill.AddToClassList("runs-run-progress-fill");
                fill.style.width = Length.Percent(Mathf.Clamp01(completion01) * 100f);
                bar.Add(fill);

                var mapBtn = new VisualElement();
                mapBtn.AddToClassList("runs-run-mapbtn");
                mapBtn.Add(new Label("Map") { name = "Lbl_MapBtn" });

                header.Add(name);
                header.Add(bar);
                header.Add(count);
                header.Add(mapBtn);

                // Summary line (best/last-ish)
                var summary = new Label(BuildRunSummaryText(rec));
                summary.AddToClassList("runs-run-summary");

                // Detail section (hidden until expanded)
                var detail = new VisualElement();
                detail.AddToClassList("runs-run-detail");
                detail.EnableInClassList("is-hidden", true);

                if (rec == null || rec.attempts == null || rec.attempts.Count == 0)
                {
                    var none = new Label("No history yet.");
                    none.AddToClassList("runs-empty");
                    detail.Add(none);
                }
                else
                {
                    var attempts = rec.attempts
                        .Where(a => a != null)
                        .OrderByDescending(a => a.gameYear)
                        .ThenByDescending(a => a.gameMonthIndex)
                        .ThenByDescending(a => a.gameDayOfMonth)
                        .ThenByDescending(a => a.gameTimeOfDay)
                        .ToList();

                    foreach (var a in attempts)
                    {
                        if (!a.isCompletion && GetCoveredFraction01(a) < MinCoverageFractionToShow)
                            continue;

                        detail.Add(BuildAttemptCard(rd.name, a, showRunName: false, showDayWeek: true));

                    }
                }

                // Expand/collapse on header click (except Map button)
                header.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.target is VisualElement ve && (ve == mapBtn || ve.GetFirstAncestorOfType<VisualElement>() == mapBtn))
                        return;

                    bool isHidden = detail.ClassListContains("is-hidden");
                    detail.EnableInClassList("is-hidden", !isHidden);
                });

                // Map navigation
                mapBtn.RegisterCallback<PointerDownEvent>(_ =>
                {
                    NavigateToMapRequested?.Invoke(rd.runId);
                });

                card.Add(header);
                card.Add(summary);
                card.Add(detail);

                _runsList.Add(card);
            }
        }

        private string BuildRunSummaryText(RunRecordEntry rec)
        {
            if (rec == null)
                return "Best: -- | Top: -- | Stacks: --";

            if (rec.timesCompleted <= 0)
            {
                float best = 0f;
                float top = 0f;
                int stacks = 0;

                if (rec.attempts != null && rec.AttemptCount > 0)
                {
                    for (int i = 0; i < rec.AttemptCount; i++)
                    {
                        var a = rec.attempts[i];
                        best = Mathf.Max(best, GetCoveredFraction01(a));
                        if (a != null) top = Mathf.Max(top, a.topSpeedMps);
                    }

                    var last = rec.attempts[rec.AttemptCount - 1];
                    if (last != null) stacks = last.stacks;
                }

                return $"Best segment: {best * 100f:0}% | Top: {FormatSpeed(top)} | Stacks: {stacks}";
            }
            else
            {
                // Best time exists as bestTimeSeconds; stacks is per-attempt (we’ll show last attempt stacks if available)
                int stacks = 0;
                float lastTop = rec.bestTopSpeedMps;
                float bestTime = rec.bestTimeSeconds;

                if (rec.attempts != null && rec.AttemptCount > 0)
                {
                    var last = rec.attempts[rec.AttemptCount - 1];
                    if (last != null)
                    {
                        stacks = last.stacks;
                        lastTop = Mathf.Max(lastTop, last.topSpeedMps);
                    }
                }

                return $"Best: {FormatSeconds(bestTime)} | Top: {FormatSpeed(lastTop)} | Stacks: {stacks}";
            }
        }

        private void TryFocusRunElement(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId) || _runsList == null)
                return;

            // Best-effort: expand and attempt to bring it into view
            // (UI Toolkit has limited “scroll to element” APIs; we keep it simple)
            foreach (var child in _runsList.Children())
            {
                var header = child.Q<VisualElement>(className: "runs-run-header");
                if (header == null) continue;

                var nameLabel = child.Q<Label>(className: "runs-run-name");
                if (nameLabel == null) continue;

                // Find matching RunDef
                var rd = _runs.FirstOrDefault(r => r.runId == runId);
                if (rd.runId == null) break;

                if (!string.Equals(nameLabel.text, rd.name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var detail = child.Q<VisualElement>(className: "runs-run-detail");
                if (detail != null) detail.EnableInClassList("is-hidden", false);

                break;
            }
        }

        // -------------------------
        // Date helpers (TimeController month presets)
        // -------------------------

        private int GetCurrentMonthIndexSafe()
        {
            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0)
                return 0;

            string cur = _time.currentMonthData != null ? _time.currentMonthData.month : null;
            if (string.IsNullOrEmpty(cur))
                return 0;

            for (int i = 0; i < _time.monthPresets.Length; i++)
            {
                var mp = _time.monthPresets[i];
                if (mp != null && string.Equals(mp.month, cur, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// Some projects treat dayCount as 1-based (day 1 = first day), others 0-based.
        /// We detect which mapping matches TimeController's current month/day.
        /// </summary>
        private int DetermineDayCountConversionOffset()
        {
            if (_time == null) return 0;

            int expectedMonthIndex = GetCurrentMonthIndexSafe();
            int expectedDayOfMonth = _time.dayOfMonth;

            // Try direct
            if (TryConvertDayOfYear(_time.dayCount, out int mi0, out int dom0))
            {
                if (mi0 == expectedMonthIndex && dom0 == expectedDayOfMonth)
                    return 0;
            }

            // Try dayCount-1
            if (TryConvertDayOfYear(_time.dayCount - 1, out int mi1, out int dom1))
            {
                if (mi1 == expectedMonthIndex && dom1 == expectedDayOfMonth)
                    return 1;
            }

            return 0;
        }

        private bool TryConvertDayOfYearAdjusted(int dayOfYearRaw, out int monthIndex, out int dayOfMonth)
        {
            int adjusted = Mathf.Max(0, dayOfYearRaw - _dayCountConversionOffset);
            return TryConvertDayOfYear(adjusted, out monthIndex, out dayOfMonth);
        }

        private bool TryMakeDayKeyFromAbsoluteDay(int absDayOfYearRaw, out GameDayKey key, out int dayOfMonth)
        {
            key = default;
            dayOfMonth = 1;

            if (_time == null) return false;

            if (!TryConvertDayOfYearAdjusted(absDayOfYearRaw, out int mi, out int dom))
                return false;

            dayOfMonth = dom;
            key = new GameDayKey(_time.currentYear, mi, dom);
            return true;
        }

        [ContextMenu("Reset Weeks")]
        public void ResetWeeksToStartCondition()
        {
            if (_time == null)
                ResolveRefs();

            if (_time == null)
            {
                Debug.LogWarning("[RunsPageUI] ResetWeeksToStartCondition failed: TimeController not found.");
                return;
            }

            // Baseline becomes "today", so relative day count = 0 and UI shows Week 1.
            _calendarBaseDayOfYear = _time.dayCount;
            PlayerPrefs.SetInt(Pref_CalendarBaseDay, _calendarBaseDayOfYear);
            PlayerPrefs.Save();

            _currentWeekIndex = 0;
            _expandedWeekIndex = 0;

            CloseDayDetail();
            RebuildWeeksList();

            Debug.Log($"[RunsPageUI] Weeks reset baseline set to dayOfYear={_calendarBaseDayOfYear} (Week 1).");
        }

        [ContextMenu("Clear Run History and Reset Weeks")]
        public void ClearRunHistoryAndResetCalendar()
        {
            var mgr = PlayerStatsManager.Instance;
            if (mgr == null || mgr.Profile == null)
            {
                Debug.LogWarning("[RunsPageUI] ClearRunHistoryAndResetCalendar failed: PlayerStatsProfile missing.");
                return;
            }

            int removed = mgr.Profile.ClearAllRunHistory(
                clearVisits: true,
                clearCounts: true,
                clearSessionRefs: true,
                recalcLifetimeRunAggregates: true);

            mgr.Save();

            if (_runProgressTracker == null)
                _runProgressTracker = FindObjectOfType<RunProgressTracker>();

            _runProgressTracker?.ResetTrackingState();

            // Now reset calendar baseline too (so Week 1)
            ResetWeeksToStartCondition();

            RebuildRunDefinitions();
            RebuildCaches();
            RebuildRunsList();

            if (_activeDayOfYear >= 0)
                RebuildDayDetailList();

            Debug.Log($"[RunsPageUI] Cleared run history ({removed} records). Calendar reset to Week 1.");
        }


        private bool TryConvertDayOfYear(int dayOfYear, out int monthIndex, out int dayOfMonth)
        {
            monthIndex = 0;
            dayOfMonth = 1;

            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0)
                return false;

            int remaining = dayOfYear;
            for (int i = 0; i < _time.monthPresets.Length; i++)
            {
                int dim = Mathf.Max(1, _time.monthPresets[i].daysInMonth);
                if (remaining < dim)
                {
                    monthIndex = i;
                    dayOfMonth = remaining + 1;
                    return true;
                }
                remaining -= dim;
            }

            return false;
        }

        private bool TryStepDay(GameDayKey day, int delta, out GameDayKey next)
        {
            next = day;

            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0)
                return false;

            int m = day.monthIndex;
            int d = day.dayOfMonth + delta;

            while (d < 1)
            {
                m--;
                if (m < 0) return false; // do not cross year boundary for now
                d += Mathf.Max(1, _time.monthPresets[m].daysInMonth);
            }

            while (m >= 0 && m < _time.monthPresets.Length)
            {
                int dim = Mathf.Max(1, _time.monthPresets[m].daysInMonth);
                if (d <= dim) break;
                d -= dim;
                m++;
                if (m >= _time.monthPresets.Length) return false; // do not cross year boundary
            }

            next = new GameDayKey(day.year, m, d);

            // Clamp navigation to the same range the Weeks grid can represent:
            // [calendar base day .. current dayCount]
            if (!TryGetDayOfYear(next, out int nextDoy))
                return false;

            // Prevent going earlier than the first day shown in Week 1
            if (nextDoy < _calendarBaseDayOfYear)
                return false;

            // Prevent navigating into future days (Weeks grid disables these too)
            if (_time != null && nextDoy > _time.dayCount)
                return false;

            return true;

        }

        private string SafeMonthName(int monthIndex)
        {
            if (_time == null || _time.monthPresets == null || _time.monthPresets.Length == 0)
                return "Month";

            monthIndex = Mathf.Clamp(monthIndex, 0, _time.monthPresets.Length - 1);
            return string.IsNullOrWhiteSpace(_time.monthPresets[monthIndex].month) ? "Month" : _time.monthPresets[monthIndex].month;
        }

        // -------------------------
        // Formatting
        // -------------------------

        private static float GetCoveredFraction01(RunAttemptEntry a)
        {
            if (a == null) return 0f;

            // Primary (new v6 field)
            float cov = a.coveredFraction01;

            // Fallback: derive from min/max along if available
            if (cov <= 0f && a.runLengthMeters > 0.001f && a.maxDistanceMeters > a.minDistanceMeters)
                cov = (a.maxDistanceMeters - a.minDistanceMeters) / a.runLengthMeters;

            // Fallback: approximate from travelled distance if available
            if (cov <= 0f && a.runLengthMeters > 0.001f)
            {
                float dist = Mathf.Max(0f, a.onRouteDistanceMeters + a.offRouteDistanceMeters);
                if (dist > 0f) cov = dist / a.runLengthMeters;
            }

            return Mathf.Clamp01(cov);
        }

        private static string FormatCoveredPct(RunAttemptEntry a)
        {
            float pct = GetCoveredFraction01(a) * 100f;
            if (pct <= 0.01f) return "--";
            return $"{pct:0}%";
        }

        private static string FormatCoveredRange(RunAttemptEntry a)
        {
            if (a == null) return "";

            // If we have stable fractions, show min->max interval.
            if (a.coveredMaxFraction01 > 0f || a.coveredMinFraction01 > 0f)
            {
                float lo = Mathf.Clamp01(a.coveredMinFraction01) * 100f;
                float hi = Mathf.Clamp01(a.coveredMaxFraction01) * 100f;
                if (hi > 0.01f) return $"{lo:0}–{hi:0}%";
            }

            // Fallback from meters
            if (a.runLengthMeters > 0.001f && a.maxDistanceMeters > a.minDistanceMeters)
            {
                float lo = Mathf.Clamp01(a.minDistanceMeters / a.runLengthMeters) * 100f;
                float hi = Mathf.Clamp01(a.maxDistanceMeters / a.runLengthMeters) * 100f;
                return $"{lo:0}–{hi:0}%";
            }

            return "";
        }

        private static void GetAttemptEntryExitRange01(RunAttemptEntry a, out float start01, out float end01, out bool reversed)
        {
            start01 = 0f;
            end01 = 0f;
            reversed = false;

            if (a == null) return;

            float s = a.entryFraction01;
            float e = a.exitFraction01;

            bool has = (s > 0f || e > 0f);

            if (!has && a.runLengthMeters > 0.001f && (a.entryDistanceMeters > 0f || a.exitDistanceMeters > 0f))
            {
                s = a.entryDistanceMeters / a.runLengthMeters;
                e = a.exitDistanceMeters / a.runLengthMeters;
                has = true;
            }

            if (!has && (a.coveredMinFraction01 > 0f || a.coveredMaxFraction01 > 0f))
            {
                s = a.coveredMinFraction01;
                e = a.coveredMaxFraction01;
                has = true;
            }

            if (!has && a.runLengthMeters > 0.001f && a.maxDistanceMeters > a.minDistanceMeters)
            {
                s = a.minDistanceMeters / a.runLengthMeters;
                e = a.maxDistanceMeters / a.runLengthMeters;
                has = true;
            }

            s = Mathf.Clamp01(s);
            e = Mathf.Clamp01(e);

            start01 = s;
            end01 = e;
            reversed = e < s;
        }

        private static string FormatClock12h(float hours)
        {
            if (hours < 0f) return "--:--";
            int h24 = Mathf.FloorToInt(hours) % 24;
            int m = Mathf.Clamp(Mathf.RoundToInt((hours - Mathf.Floor(hours)) * 60f), 0, 59);

            bool pm = h24 >= 12;
            int h12 = h24 % 12;
            if (h12 == 0) h12 = 12;

            return $"{h12:00}:{m:00} {(pm ? "PM" : "AM")}";
        }

        private static string FormatDurationTag(float seconds)
        {
            if (seconds <= 0f) return "--:--s";
            int s = Mathf.RoundToInt(seconds);
            int mm = Mathf.Max(0, s / 60);
            int ss = Mathf.Clamp(s % 60, 0, 59);
            return $"{mm:00}:{ss:00}s";
        }

        private static string FormatSpeedTag(float mps)
        {
            if (mps <= 0f) return "Top --";
            float kmh = mps * 3.6f;
            return $"Top {kmh:0} km/h";
        }

        private static Label BuildAttemptChip(string text)
        {
            var chip = new Label(text);
            chip.AddToClassList("runs-attempt-chip");
            return chip;
        }

        
        private static string FormatSpeed(float mps)
        {
            if (mps <= 0f) return "--";
            float kmh = mps * 3.6f;
            return $"{kmh:0} km/h";
        }

        private static string FormatSeconds(float seconds)
        {
            if (seconds <= 0f) return "--";
            int s = Mathf.RoundToInt(seconds);
            int mm = Mathf.Max(0, s / 60);
            int ss = Mathf.Clamp(s % 60, 0, 59);
            return $"{mm:00}:{ss:00}";
        }

        private static string FormatClock(float hours)
        {
            if (hours < 0f) return "--:--";
            int h = Mathf.FloorToInt(hours) % 24;
            int m = Mathf.Clamp(Mathf.RoundToInt((hours - Mathf.Floor(hours)) * 60f), 0, 59);
            return $"{h:00}:{m:00}";
        }

        private static string FormatAttemptDayWeek(RunAttemptEntry a)
        {
            if (a == null) return "--";

            string day = FormatDayOfWeekShort(a.gameDayOfWeek);

            // Week-of-month (since attempts don’t store absolute dayCount)
            int dom = Mathf.Max(1, a.gameDayOfMonth);
            int week = ((dom - 1) / 7) + 1;

            return $"{day}, Week {week}";
        }

        private static string FormatDayOfWeekShort(int dayOfWeek)
        {
            // Assumes your TimeController/currentDay uses 0=Mon ... 6=Sun.
            // If your enum is 0=Sun ... 6=Sat, swap the mapping accordingly.
            switch (dayOfWeek)
            {
                case 0: return "Mon";
                case 1: return "Tue";
                case 2: return "Wed";
                case 3: return "Thu";
                case 4: return "Fri";
                case 5: return "Sat";
                case 6: return "Sun";
                default: return "--";
            }
        }

        private string ResolveRunName(string runId)
        {
            for (int i = 0; i < _runs.Count; i++)
                if (_runs[i].runId == runId)
                    return _runs[i].name;

            return "Run";
        }

        private void RefreshActiveRunHeader()
        {
            if (_activePanel == null) return;

            if (_runProgressTracker == null)
                _runProgressTracker = FindObjectOfType<RunProgressTracker>();

            if (_runProgressTracker != null && _runProgressTracker.TryGetActiveProgress(out var p))
            {
                _activePanel.RemoveFromClassList("is-hidden");

                if (_activeName != null) _activeName.text = p.runName;

                float pct = p.completion01 * 100f;
                if (_activeMeta != null)
                    _activeMeta.text = $"Progress: {pct:0}%  |  Time {FormatTime(p.elapsedSeconds)}  |  Max {p.topSpeedMps:0.0} m/s  |  Stacks {p.stacks}";

                if (_activeFill != null)
                    _activeFill.style.width = new StyleLength(new Length(Mathf.Clamp01(p.completion01) * 100f, LengthUnit.Percent));
            }
            else
            {
                _activePanel.AddToClassList("is-hidden");
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0.01f) return "--:--";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1.0) return $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}";
            return $"{ts.Minutes:00}:{ts.Seconds:00}";
        }

        // Compatibility wrapper (older patches referenced this name)
        private VisualElement BuildAttemptCardOrdered(RunAttemptEntry a, bool showDayWeek)
        {
            // We already have the ordered/compact card implemented in BuildAttemptCard.
            return BuildAttemptCard(
                runName: null,
                a: a,
                showRunName: false,
                showDayWeek: showDayWeek
            );
        }

        private VisualElement BuildAttemptCard(string runName, RunAttemptEntry a, bool showRunName, bool showDayWeek)
        {
            var card = new VisualElement();
            card.AddToClassList("runs-attempt-card");

            // ---- Header (tight, ordered) ----
            var head = new VisualElement();
            head.AddToClassList("runs-attempt-head");

            var left = new VisualElement();
            left.AddToClassList("runs-attempt-head-left");

            if (showRunName && !string.IsNullOrWhiteSpace(runName))
            {
                var rn = new Label(runName);
                rn.AddToClassList("runs-attempt-runname");
                left.Add(rn);
            }

            string timeStr = FormatClockAmPm(a != null ? a.gameTimeOfDay : -1f);
            string whenStr = showDayWeek ? $"{FormatAttemptDayWeek(a)} • {timeStr}" : timeStr;

            var when = new Label(whenStr);
            when.AddToClassList("runs-attempt-when");
            left.Add(when);

            var right = new VisualElement();
            right.AddToClassList("runs-attempt-head-right");

            string status = (a != null && a.isCompletion) ? "COMPLETE" : "PARTIAL";
            var statusLbl = new Label(status);
            statusLbl.AddToClassList("runs-attempt-status");

            var durLbl = new Label(FormatSeconds(a != null ? a.timeSeconds : 0f));
            durLbl.AddToClassList("runs-attempt-dur");

            right.Add(statusLbl);
            right.Add(durLbl);

            head.Add(left);
            head.Add(right);

            card.Add(head);

            // ---- Progress range bar (start -> end) ----
            var bar = new VisualElement();
            bar.AddToClassList("runs-attempt-progress");

            var seg = new VisualElement();
            seg.AddToClassList("runs-attempt-progress-seg");

            GetProgressRange01(a, out float start01, out float end01);

            float lo = Mathf.Clamp01(Mathf.Min(start01, end01));
            float hi = Mathf.Clamp01(Mathf.Max(start01, end01));

            float leftPct = lo * 100f;
            float widthPct = Mathf.Max(0.8f, (hi - lo) * 100f); // always visible

            seg.style.left = Length.Percent(leftPct);
            seg.style.width = Length.Percent(widthPct);

            bar.Add(seg);
            card.Add(bar);

            // Optional numeric hint only when it’s not trivial
            if (a != null)
            {
                int sp = Mathf.RoundToInt(start01 * 100f);
                int ep = Mathf.RoundToInt(end01 * 100f);

                bool showRange = !(sp <= 0 && ep >= 100) && Mathf.Abs(ep - sp) >= 2;
                if (showRange)
                {
                    var range = new Label($"{sp}% → {ep}%");
                    range.AddToClassList("runs-attempt-range");
                    card.Add(range);
                }
            }

            // ---- Footer stats (clear identifiers, no chips) ----
            var stats = new VisualElement();
            stats.AddToClassList("runs-attempt-stats");

            var max = new Label($"Max {FormatSpeed(a != null ? a.topSpeedMps : 0f)}");
            max.AddToClassList("runs-attempt-kv");

            var stacks = new Label($"Stacks {((a != null) ? a.stacks : 0)}");
            stacks.AddToClassList("runs-attempt-kv");

            stats.Add(max);
            stats.Add(stacks);

            card.Add(stats);

            return card;
        }

        private static void GetProgressRange01(RunAttemptEntry a, out float start01, out float end01)
        {
            start01 = 0f;
            end01 = 0f;

            if (a == null) return;

            // Prefer stable fractions
            float s = a.entryFraction01;
            float e = a.exitFraction01;

            // Fallback from meters if needed
            if ((s <= 0f && a.entryDistanceMeters > 0f) && a.runLengthMeters > 0.001f)
                s = a.entryDistanceMeters / a.runLengthMeters;

            if ((e <= 0f && a.exitDistanceMeters > 0f) && a.runLengthMeters > 0.001f)
                e = a.exitDistanceMeters / a.runLengthMeters;

            start01 = Mathf.Clamp01(s);

            // Completed runs: force end to 1.0 for UI clarity
            end01 = Mathf.Clamp01(a.isCompletion ? 1f : e);
        }

        private static string FormatClockAmPm(float hours)
        {
            if (hours < 0f) return "--:--";

            int h24 = Mathf.FloorToInt(hours) % 24;
            int m = Mathf.Clamp(Mathf.RoundToInt((hours - Mathf.Floor(hours)) * 60f), 0, 59);

            int h12 = h24 % 12;
            if (h12 == 0) h12 = 12;

            string ampm = (h24 < 12) ? "AM" : "PM";
            return $"{h12:00}:{m:00} {ampm}";
        }

        private void RefreshActiveRunPanel()
        {
            if (_activePanel == null) return;

            if (_runProgressTracker == null)
                _runProgressTracker = FindObjectOfType<RunProgressTracker>();

            if (_runProgressTracker != null && _runProgressTracker.TryGetActiveProgress(out var p))
            {
                _activePanel.RemoveFromClassList("is-hidden");

                float pct = Mathf.Clamp01(p.completion01) * 100f;

                // Progress next to name (prevents meta line from overflowing)
                if (_activeName != null)
                    _activeName.text = $"{p.runName}  {pct:0}%";

                if (_activeMeta != null)
                    _activeMeta.text = $"Time {FormatTime(p.elapsedSeconds)}  |  Max {p.topSpeedMps:0.0} m/s  |  Stacks {p.stacks}";

                if (_activeFill != null)
                    _activeFill.style.width = new StyleLength(new Length(Mathf.Clamp01(p.completion01) * 100f, LengthUnit.Percent));
            }
            else
            {
                _activePanel.AddToClassList("is-hidden");
            }
        }

        [ContextMenu("Runs UI: Reset Weeks View State (Start)")]
        private void Context_ResetWeeksViewState()
        {
            CloseDayDetail();

            if (_time != null)
            {
                _currentWeekIndex = Mathf.Max(0, _time.dayCount / 7);
                _expandedWeekIndex = _currentWeekIndex; // expand current week by default
            }
            else
            {
                _expandedWeekIndex = -1;
            }

            SetTab(isWeeks: true);
            RebuildWeeksList();
        }

        [ContextMenu("Runs: Clear Run Tracking + Reset UI (Start)")]
        private void Context_ClearRunTrackingAndResetUI()
        {
            var mgr = PlayerStatsManager.Instance;
            if (mgr == null || mgr.Profile == null)
            {
                Debug.LogWarning("[RunsPageUI] No PlayerStatsProfile found.");
                return;
            }

            int removed = mgr.Profile.ClearAllRunHistory(
                clearVisits: true,
                clearCounts: true,
                clearSessionRefs: true,
                recalcLifetimeRunAggregates: true);

            mgr.Save();

            if (_runProgressTracker == null)
                _runProgressTracker = FindObjectOfType<RunProgressTracker>();

            _runProgressTracker?.ResetTrackingState();

            Debug.Log($"[RunsPageUI] Cleared run tracking. Removed {removed} run record(s).");

            RebuildCaches();
            CloseDayDetail();

            if (_time != null)
            {
                _currentWeekIndex = Mathf.Max(0, _time.dayCount / 7);
                _expandedWeekIndex = _currentWeekIndex;
            }

            RebuildWeeksList();
            RebuildRunsList();
        }

        // -------------------------
        // Small structs
        // -------------------------

        private readonly struct AttemptRef
        {
            public readonly string runId;
            public readonly RunAttemptEntry attempt;

            public AttemptRef(string runId, RunAttemptEntry attempt)
            {
                this.runId = runId;
                this.attempt = attempt;
            }
        }

        private struct RunDef
        {
            public string runId;
            public string name;
            public int order;
        }

        private readonly struct GameDayKey : IEquatable<GameDayKey>
        {
            public readonly int year;
            public readonly int monthIndex;
            public readonly int dayOfMonth;

            public GameDayKey(int year, int monthIndex, int dayOfMonth)
            {
                this.year = year;
                this.monthIndex = monthIndex;
                this.dayOfMonth = dayOfMonth;
            }

            public bool Equals(GameDayKey other)
            {
                return year == other.year && monthIndex == other.monthIndex && dayOfMonth == other.dayOfMonth;
            }

            public override bool Equals(object obj)
            {
                return obj is GameDayKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = year;
                    hash = (hash * 397) ^ monthIndex;
                    hash = (hash * 397) ^ dayOfMonth;
                    return hash;
                }
            }
        }
    }
}
