using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkiGame.Progression
{
    /// <summary>
    /// Achievements page renderer:
    /// - Rows (vertical): ProgressionMetric
    /// - Columns (horizontal): Achievements for that metric, sorted by target (acts as "tiers")
    /// - Tier cells are textless blocks that lerp grey->green by completion and turn yellow when unlocked
    /// - Single detail panel beneath the matrix (no separate top info panel)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AchievementsPageUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private UIDocument document;

        [Tooltip("Low-frequency UI refresh cadence (unscaled time).")]
        [SerializeField, Range(0.05f, 0.5f)] private float refreshInterval = 0.15f;

        private VisualElement _root;

        // From UXML
        private ScrollView _page;
        private ScrollView _catScroll;     // legacy: hidden
        private VisualElement _catRow;     // legacy: hidden
        private Label _summary;
        private VisualElement _list;

        // Data sources
        private PlayerStatsManager _statsManager;
        private PlayerStatsProfile _profile;
        private ProgressionDirector _progression;

        // State
        private string _selectedAchievementId = null;

        private float _nextRefreshAt;
        private float _nextRebuildAt;

        private int _lastUnlockedCount = -1;
        private int _lastCatalogHash = 0;

        // Buffers
        private readonly List<AchievementDefinitionSO> _all = new();
        private readonly Dictionary<string, AchievementDefinitionSO> _byId = new(256);

        private readonly Dictionary<ProgressionMetric, List<AchievementDefinitionSO>> _byMetric = new();
        private readonly List<ProgressionMetric> _metricOrder = new();

        // UI root pieces
        private VisualElement _matrixRoot;
        private VisualElement _detailRoot;

        // Detail panel widgets
        private Label _detailTitle;
        private Label _detailSub;
        private Label _detailBody;
        private VisualElement _detailBarFill;
        private Label _detailBarText;

        // Runtime cell refs for cheap refresh
        private sealed class CellRef
        {
            public string achievementId;
            public AchievementDefinitionSO def;
            public Button button;
        }

        private readonly List<CellRef> _cells = new(256);

        // Colors: grey->green lerp; yellow when unlocked
        private static readonly Color Grey0 = new(0.22f, 0.22f, 0.22f, 0.80f);
        private static readonly Color Green1 = new(0.22f, 0.78f, 0.28f, 0.92f);
        private static readonly Color YellowUnlocked = new(0.95f, 0.85f, 0.20f, 0.95f);

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

            Bind();
        }

        private void Bind()
        {
            _page = _root.Q<ScrollView>("Page_Achievements");

            // Legacy category strip (kept in UXML; we hide it)
            _catScroll = _root.Q<ScrollView>("Ach_CategoryScroll");
            _catRow = _root.Q<VisualElement>("Ach_CategoryRow");

            _summary = _root.Q<Label>("Lbl_AchSummary");
            _list = _root.Q<VisualElement>("AchievementsList");
        }

        private void Update()
        {
            if (_root == null || _page == null) return;

            if (Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + refreshInterval;

            // Only refresh when page is visible
            if (!_page.ClassListContains("is-active")) return;

            CacheSources();
            if (_profile == null || _progression == null)
            {
                if (_summary != null) _summary.text = "(no profile)";
                return;
            }

            // Hide legacy category strip (if still present)
            if (_catScroll != null) _catScroll.style.display = DisplayStyle.None;
            if (_catRow != null) _catRow.style.display = DisplayStyle.None;

            // Pull catalog achievements (buffer reused)
            _progression.GetAllAchievements(_all);

            int unlockedCount = CountUnlocked(_all, _profile);
            int catalogHash = ComputeCatalogHash(_all);

            bool needsRebuild =
                Time.unscaledTime >= _nextRebuildAt ||
                _lastUnlockedCount != unlockedCount ||
                _lastCatalogHash != catalogHash;

            if (needsRebuild)
            {
                _lastUnlockedCount = unlockedCount;
                _lastCatalogHash = catalogHash;

                EnsureScaffold();
                BuildIndex();
                BuildGroups();

                RebuildMatrix();
                RefreshDetailPanel(); // selection + summary
                UpdateSummaryLine();

                _nextRebuildAt = Time.unscaledTime + 0.5f;
            }
            else
            {
                RefreshCellsRuntime();
                RefreshDetailPanel();
                UpdateSummaryLine();
            }
        }

        private void CacheSources()
        {
            if (_statsManager == null) _statsManager = PlayerStatsManager.Instance;
            if (_statsManager != null) _profile = _statsManager.Profile;
            if (_progression == null) _progression = ProgressionDirector.Instance;
        }

        private void EnsureScaffold()
        {
            if (_list == null) return;

            // Build once
            if (_matrixRoot != null && _detailRoot != null) return;

            _list.Clear();
            _cells.Clear();

            // Matrix root
            _matrixRoot = new VisualElement();
            _matrixRoot.AddToClassList("ach-matrix2");

            // Detail panel beneath matrix
            _detailRoot = new VisualElement();
            _detailRoot.AddToClassList("ach-detail");

            var top = new VisualElement();
            top.AddToClassList("ach-detail-top");

            _detailTitle = new Label("Achievements");
            _detailTitle.AddToClassList("ach-detail-title");

            _detailSub = new Label("");
            _detailSub.AddToClassList("ach-detail-sub");

            top.Add(_detailTitle);
            top.Add(_detailSub);

            _detailBody = new Label("");
            _detailBody.AddToClassList("ach-detail-body");

            var bar = new VisualElement();
            bar.AddToClassList("ach-detail-bar");

            _detailBarFill = new VisualElement();
            _detailBarFill.AddToClassList("ach-detail-barfill");
            bar.Add(_detailBarFill);

            _detailBarText = new Label("");
            _detailBarText.AddToClassList("ach-detail-bartext");

            _detailRoot.Add(top);
            _detailRoot.Add(_detailBody);
            _detailRoot.Add(bar);
            _detailRoot.Add(_detailBarText);

            _list.Add(_matrixRoot);
            _list.Add(_detailRoot);
        }

        private void BuildIndex()
        {
            _byId.Clear();
            for (int i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a == null || string.IsNullOrEmpty(a.id)) continue;
                _byId[a.id] = a;
            }

            if (!string.IsNullOrEmpty(_selectedAchievementId) && !_byId.ContainsKey(_selectedAchievementId))
                _selectedAchievementId = null;
        }

        private void BuildGroups()
        {
            _byMetric.Clear();
            _metricOrder.Clear();

            // Group by metric
            for (int i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a == null) continue;

                if (!_byMetric.TryGetValue(a.metric, out var list))
                {
                    list = new List<AchievementDefinitionSO>(16);
                    _byMetric[a.metric] = list;
                    _metricOrder.Add(a.metric);
                }

                list.Add(a);
            }

            // For each metric: sort by target ascending (tier order)
            for (int i = 0; i < _metricOrder.Count; i++)
            {
                var m = _metricOrder[i];
                var list = _byMetric[m];
                list.Sort((x, y) =>
                {
                    int c = x.target.CompareTo(y.target);
                    if (c != 0) return c;
                    return string.Compare(x.title, y.title, StringComparison.Ordinal);
                });
            }

            // Stable metric ordering for UX (session vs lifetime grouped, similar types nearby)
            _metricOrder.Sort((a, b) => string.Compare(MetricLabel(a), MetricLabel(b), StringComparison.Ordinal));
        }

        private void RebuildMatrix()
        {
            if (_matrixRoot == null) return;

            _matrixRoot.Clear();
            _cells.Clear();

            // Each row: metric label + row of tier-cells
            for (int r = 0; r < _metricOrder.Count; r++)
            {
                var metric = _metricOrder[r];
                var defs = _byMetric[metric];
                if (defs == null || defs.Count == 0) continue;

                var row = new VisualElement();
                row.AddToClassList("ach-metric-row");

                var label = new Label(MetricLabel(metric));
                label.AddToClassList("ach-metric-label");
                row.Add(label);

                var tiers = new VisualElement();
                tiers.AddToClassList("ach-tier-row");

                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;

                    var cell = new Button();
                    cell.AddToClassList("ach-tier-cell");
                    cell.focusable = false;

                    var cr = new CellRef
                    {
                        achievementId = def.id,
                        def = def,
                        button = cell
                    };
                    _cells.Add(cr);

                    cell.clicked += () =>
                    {
                        // Toggle select
                        _selectedAchievementId = (_selectedAchievementId == cr.achievementId) ? null : cr.achievementId;
                        _nextRebuildAt = 0f; // ensure classes update cleanly
                    };

                    tiers.Add(cell);
                }

                row.Add(tiers);
                _matrixRoot.Add(row);
            }

            RefreshCellsRuntime();
        }

        private void RefreshCellsRuntime()
        {
            if (_profile == null) return;

            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                if (c.def == null || c.button == null) continue;

                bool unlocked = _profile.HasAchievement(c.achievementId);
                float pct = unlocked ? 1f : CompletionPct(c.def, _profile);

                // background color: grey -> green lerp; unlocked -> yellow
                var col = unlocked ? YellowUnlocked : Color.Lerp(Grey0, Green1, pct);
                c.button.style.backgroundColor = new StyleColor(col);

                // subtle selection outline via class
                bool selected = (c.achievementId == _selectedAchievementId);
                c.button.EnableInClassList("is-selected", selected);
                c.button.EnableInClassList("is-unlocked", unlocked);
            }
        }

        private void RefreshDetailPanel()
        {
            if (_detailRoot == null || _profile == null) return;

            // Selected achievement
            if (!string.IsNullOrEmpty(_selectedAchievementId) &&
                _byId.TryGetValue(_selectedAchievementId, out var def) &&
                def != null)
            {
                bool unlocked = _profile.HasAchievement(def.id);
                float cur = def.ReadCurrent(_profile);
                float tgt = Mathf.Max(0.0001f, def.target);
                float pct = unlocked ? 1f : Mathf.Clamp01(cur / tgt);

                _detailTitle.text = string.IsNullOrEmpty(def.title) ? "(untitled)" : def.title;
                // Support non-metric achievements (e.g. group requirements) while keeping the row metric label.
                string req = def.GetRequirementText();
                _detailSub.text = $"{MetricLabel(def.metric)} \x95 {req}";
                _detailBody.text = string.IsNullOrEmpty(def.description) ? "" : def.description;

                _detailBarFill.style.width = Length.Percent(pct * 100f);
                _detailBarText.text = unlocked
                    ? "Completed"
                    : $"{FormatMetric(def.metric, cur)} / {FormatMetric(def.metric, tgt)}  ({pct * 100f:0}%)";

                return;
            }

            // No selection: show overall
            int total = 0;
            int unlockedCount = 0;
            AchievementDefinitionSO closest = null;
            float bestPct = -1f;

            for (int i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a == null) continue;

                total++;
                bool unlocked = _profile.HasAchievement(a.id);
                if (unlocked)
                {
                    unlockedCount++;
                    continue;
                }

                float pct = CompletionPct(a, _profile);
                if (pct > bestPct)
                {
                    bestPct = pct;
                    closest = a;
                }
            }

            _detailTitle.text = "Achievements";
            _detailSub.text = $"{unlockedCount}/{total} completed";
            _detailBody.text = "Tap a block to view details.";

            float overallPct = (total <= 0) ? 0f : Mathf.Clamp01(unlockedCount / (float)total);
            _detailBarFill.style.width = Length.Percent(overallPct * 100f);

            _detailBarText.text = (closest != null)
                ? $"Closest: {closest.title} ({bestPct * 100f:0}%)"
                : (total > 0 ? "All complete" : "");
        }

        private void UpdateSummaryLine()
        {
            if (_summary == null || _profile == null) return;

            int total = 0;
            int unlocked = 0;

            for (int i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a == null) continue;

                total++;
                if (_profile.HasAchievement(a.id)) unlocked++;
            }

            _summary.text = $"{unlocked}/{total} completed";
        }

        // ---------------------- Utility ----------------------

        private static int CountUnlocked(List<AchievementDefinitionSO> list, PlayerStatsProfile profile)
        {
            if (list == null || profile == null) return 0;
            int c = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null) continue;
                if (profile.HasAchievement(a.id)) c++;
            }
            return c;
        }

        private static int ComputeCatalogHash(List<AchievementDefinitionSO> list)
        {
            unchecked
            {
                int h = 17;
                if (list == null) return h;

                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    if (a == null) continue;
                    h = (h * 31) ^ (a.id != null ? a.id.GetHashCode() : 0);
                    h = (h * 31) ^ a.metric.GetHashCode();
                    h = (h * 31) ^ a.target.GetHashCode();
                }
                return h;
            }
        }

        private static float CompletionPct(AchievementDefinitionSO def, PlayerStatsProfile profile)
        {
            if (def == null || profile == null) return 0f;
            if (profile.HasAchievement(def.id)) return 1f;

            float tgt = Mathf.Max(0.0001f, def.target);
            float cur = def.ReadCurrent(profile);
            return Mathf.Clamp01(cur / tgt);
        }

        private static string MetricLabel(ProgressionMetric m)
        {
            // Keep labels compact; they sit at the left of the row.
            return m switch
            {
                ProgressionMetric.SessionDistanceMeters => "Session Distance",
                ProgressionMetric.LifetimeDistanceMeters => "Lifetime Distance",
                ProgressionMetric.SessionTopSpeedMps => "Session Top Speed",
                ProgressionMetric.LifetimeTopSpeedMps => "Lifetime Top Speed",
                ProgressionMetric.SessionAirTimeSeconds => "Session Air Time",
                ProgressionMetric.LifetimeAirTimeSeconds => "Lifetime Air Time",
                ProgressionMetric.SessionAirDistanceMeters => "Session Air Dist",
                ProgressionMetric.LifetimeAirDistanceMeters => "Lifetime Air Dist",
                ProgressionMetric.SessionVerticalDescentMeters => "Session Vertical",
                ProgressionMetric.LifetimeVerticalDescentMeters => "Lifetime Vertical",
                ProgressionMetric.SessionStacks => "Session Stacks",
                ProgressionMetric.LifetimeStacks => "Lifetime Stacks",

                ProgressionMetric.SessionRunsVisited=> "Session Runs Visited",
                ProgressionMetric.LifetimeRunsVisited => "Lifetime Runs Visited",                 
                ProgressionMetric.SessionRunsCompleted => "Session Completed Runs",
                ProgressionMetric.LifetimeRunsCompleted => "Lifetime Completed Runs", 
                ProgressionMetric.SessionRunsCompletedClean => "Session Clean Runs",
                ProgressionMetric.LifetimeRunsCompletedClean => "Lifetime Clean Runs",

                ProgressionMetric.SessionLiftsUsed => "Session Lifts",
                ProgressionMetric.LifetimeLiftsUsed => "Lifetime Lifts",
                // POI + Grind
                ProgressionMetric.SessionPlacesVisited => "Session Places",
                ProgressionMetric.LifetimePlacesVisited => "Lifetime Places",
                ProgressionMetric.SessionGrindTimeSeconds => "Session Grind Time",
                ProgressionMetric.LifetimeGrindTimeSeconds => "Lifetime Grind Time",
                ProgressionMetric.SessionGrindDistanceMeters => "Session Grind Dist",
                ProgressionMetric.LifetimeGrindDistanceMeters => "Lifetime Grind Dist",

                // Daily task matrix / extended metrics
                ProgressionMetric.SessionVerticalAscentMeters => "Session Ascent",
                ProgressionMetric.SessionAverageSpeedMps => "Session Avg Speed",

                ProgressionMetric.LifetimeTotalDistanceMeters => "Lifetime Distance",
                ProgressionMetric.LifetimeTotalVerticalAscentMeters => "Lifetime Ascent",
                ProgressionMetric.LifetimeTotalVerticalDescentMeters => "Lifetime Descent",
                ProgressionMetric.LifetimeAverageSpeedMps => "Lifetime Avg Speed",
                ProgressionMetric.SessionTopRunSpeedMps => "Session Top Run Speed",
                ProgressionMetric.LifetimeTopRunSpeedMps => "Lifetime Top Run Speed",

                _ => m.ToString()
            };
        }

        private static string FormatMetric(ProgressionMetric metric, float value)
        {
            switch (metric)
            {
                case ProgressionMetric.SessionDistanceMeters:
                case ProgressionMetric.LifetimeDistanceMeters:
                case ProgressionMetric.SessionAirDistanceMeters:
                case ProgressionMetric.LifetimeAirDistanceMeters:
                case ProgressionMetric.SessionVerticalDescentMeters:
                case ProgressionMetric.LifetimeVerticalDescentMeters:
                case ProgressionMetric.SessionVerticalAscentMeters:
                case ProgressionMetric.LifetimeTotalDistanceMeters:
                case ProgressionMetric.LifetimeTotalVerticalAscentMeters:
                case ProgressionMetric.LifetimeTotalVerticalDescentMeters:
                    return (value >= 1000f) ? $"{value / 1000f:0.0}km" : $"{value:0}m";

                case ProgressionMetric.SessionTopSpeedMps:
                case ProgressionMetric.LifetimeTopSpeedMps:
                case ProgressionMetric.SessionAverageSpeedMps:
                case ProgressionMetric.LifetimeAverageSpeedMps:
                case ProgressionMetric.SessionTopRunSpeedMps:
                case ProgressionMetric.LifetimeTopRunSpeedMps:
                    return $"{value:0.0}m/s";

                case ProgressionMetric.SessionAirTimeSeconds:
                case ProgressionMetric.LifetimeAirTimeSeconds:
                case ProgressionMetric.SessionGrindTimeSeconds:
                case ProgressionMetric.LifetimeGrindTimeSeconds:
                    return $"{value:0.0}s";

                case ProgressionMetric.SessionStacks:
                case ProgressionMetric.LifetimeStacks:
                case ProgressionMetric.SessionRunsCompleted:
                case ProgressionMetric.LifetimeRunsCompleted:
                case ProgressionMetric.SessionLiftsUsed:
                case ProgressionMetric.LifetimeLiftsUsed:
                case ProgressionMetric.SessionPlacesVisited:
                case ProgressionMetric.LifetimePlacesVisited:
                case ProgressionMetric.SessionRunsVisited:
                case ProgressionMetric.LifetimeRunsVisited:
                case ProgressionMetric.SessionRunsCompletedClean:
                case ProgressionMetric.LifetimeRunsCompletedClean:
                    return $"{Mathf.FloorToInt(value):0}";

                default:
                    return $"{value:0.##}";
            }
        }
    }
}
