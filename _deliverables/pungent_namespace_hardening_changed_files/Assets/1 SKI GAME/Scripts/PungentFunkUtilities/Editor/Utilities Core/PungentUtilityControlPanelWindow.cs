using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Central launcher for reusable PungentFunk Utilities.
    /// </summary>
    public sealed class PungentUtilityControlPanelWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.ControlPanel.";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefLab = PrefPrefix + "Lab";
        private const string PrefCategory = PrefPrefix + "Category";
        private const string PrefStatus = PrefPrefix + "Status";
        private const string PrefFavorites = PrefPrefix + "Favorites";
        private const string PrefRecents = PrefPrefix + "Recents";
        private const string PrefShowHelp = PrefPrefix + "ShowHelp";
        private const string AllFilter = "All";

        private Vector2 _scroll;
        private string _search = string.Empty;
        private string _lab = AllFilter;
        private string _category = AllFilter;
        private string _statusFilter = AllFilter;
        private bool _showHelp;
        private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _recents = new List<string>();
        private string _status = "Ready.";

        [MenuItem("Tools/Utilities/PungentFunk Control Panel", priority = -200)]
        public static void Open()
        {
            PungentUtilityControlPanelWindow window = GetWindow<PungentUtilityControlPanelWindow>("PungentFunk Utilities");
            window.minSize = new Vector2(560f, 440f);
            window.Show();
        }

        public static void OpenLab(string labName)
        {
            UtilityWindowPrefs.SetString(PrefLab, string.IsNullOrWhiteSpace(labName) ? AllFilter : PungentUtilityLabs.Normalize(labName));
            UtilityWindowPrefs.SetString(PrefCategory, AllFilter);
            Open();
        }

        public static void RecordRecentUtility(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            string encoded = UtilityWindowPrefs.GetString(PrefRecents, string.Empty);
            List<string> recents = DecodeList(encoded);
            recents.RemoveAll(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
            recents.Insert(0, id);

            while (recents.Count > 10)
                recents.RemoveAt(recents.Count - 1);

            UtilityWindowPrefs.SetString(PrefRecents, EncodeList(recents));
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("PungentFunk Utilities");
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _lab = UtilityWindowPrefs.GetString(PrefLab, AllFilter);
            _category = UtilityWindowPrefs.GetString(PrefCategory, AllFilter);
            _statusFilter = UtilityWindowPrefs.GetString(PrefStatus, AllFilter);
            _showHelp = UtilityWindowPrefs.GetBool(PrefShowHelp, true);

            _favorites.Clear();
            foreach (string id in DecodeList(UtilityWindowPrefs.GetString(PrefFavorites, string.Empty)))
                _favorites.Add(id);

            _recents.Clear();
            _recents.AddRange(DecodeList(UtilityWindowPrefs.GetString(PrefRecents, string.Empty)));
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            int total = PungentUtilityRegistry.All.Count;
            UtilityWindowTheme.Header(
                "PungentFunk Utilities",
                "Launch reusable editor utilities grouped by labs, modules, categories, and package status.",
                $"{total} registered · {_status}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawToolbar();
            DrawLabOverview();
            DrawDesignValidationCard();
            DrawFavoritesAndRecents();
            DrawUtilityList();
            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Find Utilities", UtilityWindowTheme.Blue, PungentUtilityRegistry.All.Count + " registered");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search", GUILayout.Width(52f));
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(54f)))
                        _search = string.Empty;
                }

                string[] labs = new[] { AllFilter }.Concat(PungentUtilityRegistry.Labs()).ToArray();
                int labIndex = Mathf.Max(0, Array.IndexOf(labs, _lab));
                int nextLabIndex = EditorGUILayout.Popup(new GUIContent("Lab", "Primary product area. Labs are package/navigation boundaries."), labIndex, labs);
                string nextLab = labs[Mathf.Clamp(nextLabIndex, 0, labs.Length - 1)];
                if (!string.Equals(nextLab, _lab, StringComparison.OrdinalIgnoreCase))
                {
                    _lab = nextLab;
                    _category = AllFilter;
                    _statusFilter = AllFilter;
                }

                string labFilter = _lab == AllFilter ? null : _lab;
                string[] categories = new[] { AllFilter }.Concat(PungentUtilityRegistry.Categories(labFilter)).ToArray();
                int categoryIndex = Mathf.Max(0, Array.IndexOf(categories, _category));
                int nextCategoryIndex = EditorGUILayout.Popup(new GUIContent("Category", "Secondary search/menu taxonomy."), categoryIndex, categories);
                _category = categories[Mathf.Clamp(nextCategoryIndex, 0, categories.Length - 1)];

                string[] statuses = new[] { AllFilter }.Concat(PungentUtilityRegistry.Statuses(labFilter)).ToArray();
                int statusIndex = Mathf.Max(0, Array.IndexOf(statuses, _statusFilter));
                int nextStatusIndex = EditorGUILayout.Popup(new GUIContent("Status", "Package maturity label shown in the launcher and documentation."), statusIndex, statuses);
                _statusFilter = statuses[Mathf.Clamp(nextStatusIndex, 0, statuses.Length - 1)];

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_lab == AllFilter ? "All Labs" : _lab, UtilityWindowTheme.Cyan, Mathf.Clamp((_lab.Length + 6) * 7f, 76f, 230f));
                    UtilityWindowTheme.CountPill(_category == AllFilter ? "All Categories" : _category, UtilityWindowTheme.Teal, Mathf.Clamp((_category.Length + 8) * 7f, 96f, 230f));
                    UtilityWindowTheme.CountPill(_statusFilter == AllFilter ? "All Statuses" : _statusFilter, _statusFilter == AllFilter ? UtilityWindowTheme.Neutral : PungentUtilityPackageStatus.GetTint(_statusFilter), Mathf.Clamp((_statusFilter.Length + 8) * 7f, 90f, 170f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Validate", "Open the design validation audit before adding new features."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        PungentUtilityRegistry.Open("design-validation-audit");
                    if (GUILayout.Button(new GUIContent("Docs", "Open the bundled architecture/design bible."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                        PungentUtilityDesignAudit.OpenDocumentation("Architecture_Design_Bible");
                    _showHelp = GUILayout.Toggle(_showHelp, new GUIContent("Help", "Show or hide lab descriptions and status explanations."), EditorStyles.toolbarButton, GUILayout.Width(58f));
                }
            }
        }

        private void DrawLabOverview()
        {
            if (!_showHelp)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.16f, 0.08f)))
            {
                string labName = _lab == AllFilter ? "All Labs" : _lab;
                string description = _lab == AllFilter
                    ? "PungentFunk Utilities is organised as independently useful labs with shared discovery, theming, and safe editor-workflow standards."
                    : PungentUtilityLabs.GetDescription(_lab);

                UtilityWindowTheme.SectionTitle("At a Glance", UtilityWindowTheme.Cyan, labName);
                EditorGUILayout.LabelField(description, UtilityWindowTheme.MutedMiniLabelStyle);

                if (_statusFilter != AllFilter)
                    EditorGUILayout.HelpBox(PungentUtilityPackageStatus.GetDescription(_statusFilter), MessageType.Info);
                else
                    DrawStatusSummary();
            }
        }

        private void DrawStatusSummary()
        {
            string labFilter = _lab == AllFilter ? null : _lab;
            string[] statuses = PungentUtilityRegistry.Statuses(labFilter);
            if (statuses == null || statuses.Length == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < statuses.Length; i++)
                {
                    string status = statuses[i];
                    int count = PungentUtilityRegistry.Query(labFilter, null, null, status).Count();
                    UtilityWindowTheme.CountPill(status + " " + count, PungentUtilityPackageStatus.GetTint(status), Mathf.Clamp(status.Length * 7f + 42f, 82f, 150f));
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawDesignValidationCard()
        {
            if (!_showHelp)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.15f, 0.08f)))
            {
                UtilityWindowTheme.SectionTitle("Design Validation", UtilityWindowTheme.Amber, "Gate before feature work");
                EditorGUILayout.LabelField("Use the validation audit to check registry metadata, layout signals, menu taxonomy, CreateAssetMenu roots, namespaces, and asmdef readiness before adding deeper modules.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Open Validation Audit", UtilityWindowTheme.Amber, GUILayout.Width(162f), GUILayout.Height(24f)))
                        PungentUtilityRegistry.Open("design-validation-audit");

                    if (GUILayout.Button(new GUIContent("Open Design Bible", "Opens the bundled architecture/design principles PDF if present."), GUILayout.Width(134f), GUILayout.Height(24f)))
                        PungentUtilityDesignAudit.OpenDocumentation("Architecture_Design_Bible");

                    if (GUILayout.Button(new GUIContent("Open Feature Inventory", "Opens the bundled audit and feature inventory if present."), GUILayout.Width(154f), GUILayout.Height(24f)))
                        PungentUtilityDesignAudit.OpenDocumentation("Audit_and_Feature_Inventory");

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawFavoritesAndRecents()
        {
            List<PungentUtilityDescriptor> favorites = PungentUtilityRegistry.All.Where(u => _favorites.Contains(u.Id)).ToList();
            List<PungentUtilityDescriptor> recents = _recents
                .Select(PungentUtilityRegistry.Find)
                .Where(u => u != null)
                .Where(u => !_favorites.Contains(u.Id))
                .Take(6)
                .ToList();

            if (favorites.Count == 0 && recents.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                UtilityWindowTheme.SectionTitle("Pinned & Recent", UtilityWindowTheme.Cyan);
                DrawCompactUtilityButtons(favorites, "Favourites");
                DrawCompactUtilityButtons(recents, "Recent");
            }
        }

        private void DrawCompactUtilityButtons(List<PungentUtilityDescriptor> utilities, string label)
        {
            if (utilities == null || utilities.Count == 0)
                return;

            EditorGUILayout.LabelField(label, UtilityWindowTheme.MutedMiniLabelStyle);
            int perRow = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(220f, position.width - 72f) / 150f));
            for (int i = 0; i < utilities.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int end = Mathf.Min(utilities.Count, i + perRow);
                    for (int j = i; j < end; j++)
                    {
                        PungentUtilityDescriptor utility = utilities[j];
                        if (GUILayout.Button(new GUIContent(utility.DisplayName, BuildUtilityTooltip(utility)), EditorStyles.miniButton, GUILayout.Width(142f)))
                            OpenUtility(utility);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawUtilityList()
        {
            string labFilter = _lab == AllFilter ? null : _lab;
            string categoryFilter = _category == AllFilter ? null : _category;
            string statusFilter = _statusFilter == AllFilter ? null : _statusFilter;
            List<PungentUtilityDescriptor> query = PungentUtilityRegistry.Query(labFilter, categoryFilter, _search, statusFilter).ToList();

            List<IGrouping<string, PungentUtilityDescriptor>> labGroups = query
                .GroupBy(PungentUtilityRegistry.GetLab)
                .OrderBy(g => PungentUtilityLabs.SortKey(g.Key))
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Utilities", UtilityWindowTheme.Teal, query.Count + " shown");

                if (query.Count == 0)
                {
                    EditorGUILayout.HelpBox("No utilities match the current search/filter.", MessageType.Info);
                    return;
                }

                foreach (IGrouping<string, PungentUtilityDescriptor> labGroup in labGroups)
                {
                    DrawLabHeader(labGroup.Key, labGroup.Count());

                    List<IGrouping<string, PungentUtilityDescriptor>> moduleGroups = labGroup
                        .GroupBy(PungentUtilityRegistry.GetModule)
                        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (IGrouping<string, PungentUtilityDescriptor> moduleGroup in moduleGroups)
                    {
                        EditorGUILayout.Space(3f);
                        EditorGUILayout.LabelField(moduleGroup.Key, EditorStyles.boldLabel);

                        foreach (PungentUtilityDescriptor utility in moduleGroup.OrderBy(u => u.SortOrder).ThenBy(u => u.DisplayName))
                            DrawUtilityCard(utility);
                    }
                }
            }
        }

        private void DrawLabHeader(string labName, int count)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(labName, PungentUtilityLabs.GetDescription(labName)), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(count + " tools", UtilityWindowTheme.Blue, 72f);
            }
        }

        private void DrawUtilityCard(PungentUtilityDescriptor utility)
        {
            bool isHub = utility.IsLabHub;
            Color statusTint = PungentUtilityPackageStatus.GetTint(PungentUtilityRegistry.GetStatus(utility));
            Color categoryTint = GetCategoryTint(PungentUtilityRegistry.GetCategory(utility));

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(isHub ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral, isHub ? 0.18f : 0.14f, 0.08f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isFavorite = _favorites.Contains(utility.Id);
                    if (GUILayout.Button(new GUIContent(isFavorite ? "★" : "☆", "Pin or unpin this utility."), GUILayout.Width(28f), GUILayout.Height(22f)))
                        ToggleFavorite(utility.Id);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(new GUIContent(utility.DisplayName, BuildUtilityTooltip(utility)), EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                            if (isHub)
                                UtilityWindowTheme.CountPill("Hub", UtilityWindowTheme.Cyan, 48f);
                            UtilityWindowTheme.CountPill(PungentUtilityRegistry.GetStatus(utility), statusTint, Mathf.Clamp(PungentUtilityRegistry.GetStatus(utility).Length * 7f + 18f, 70f, 140f));
                        }

                        EditorGUILayout.LabelField(utility.Description, UtilityWindowTheme.MutedMiniLabelStyle);
                        EditorGUILayout.LabelField(utility.MenuPath, UtilityWindowTheme.PathLabelStyle);
                    }

                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(88f)))
                    {
                        using (new EditorGUI.DisabledScope(!utility.CanOpen))
                        {
                            if (UtilityWindowTheme.TintedButton("Open", UtilityWindowTheme.Blue, GUILayout.Width(82f), GUILayout.Height(26f)))
                                OpenUtility(utility);
                        }

                        if (GUILayout.Button(new GUIContent("Docs", "Open the bundled design bible/inventory for this utility suite."), EditorStyles.miniButton, GUILayout.Width(82f)))
                            PungentUtilityDesignAudit.OpenDocumentation("Architecture_Design_Bible");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetadataPill(PungentUtilityRegistry.GetCategory(utility), categoryTint);
                    DrawMetadataPill(PungentUtilityRegistry.GetModule(utility), UtilityWindowTheme.Neutral);
                    DrawSupportPill(utility.SupportsSelection, "Selection");
                    DrawSupportPill(utility.SupportsContextMenu, "Context");
                    DrawSupportPill(utility.SupportsSceneOverlay, "Scene Overlay");
                    GUILayout.FlexibleSpace();
                }

                DrawRelatedUtilities(utility);
            }
        }

        private void DrawRelatedUtilities(PungentUtilityDescriptor utility)
        {
            if (utility == null || !utility.HasRelatedUtilities)
                return;

            List<PungentUtilityDescriptor> related = PungentUtilityRegistry.RelatedUtilities(utility).Take(4).ToList();
            if (related.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Related", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(50f));
                for (int i = 0; i < related.Count; i++)
                {
                    PungentUtilityDescriptor item = related[i];
                    using (new EditorGUI.DisabledScope(!item.CanOpen))
                    {
                        if (GUILayout.Button(new GUIContent(item.DisplayName, BuildUtilityTooltip(item)), EditorStyles.miniButton, GUILayout.MaxWidth(170f)))
                            OpenUtility(item);
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private static Color GetCategoryTint(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return UtilityWindowTheme.Neutral;

            switch (category.Trim().ToLowerInvariant())
            {
                case "appearance":
                case "colour":
                case "input":
                    return UtilityWindowTheme.Purple;
                case "debug":
                case "validation":
                case "core":
                    return UtilityWindowTheme.Amber;
                case "placement":
                case "scene":
                case "terrain":
                    return UtilityWindowTheme.Green;
                case "audio":
                    return UtilityWindowTheme.Cyan;
                case "texture":
                    return UtilityWindowTheme.Teal;
                case "assets":
                case "prefabs":
                    return UtilityWindowTheme.Blue;
                case "components":
                case "workflow":
                    return UtilityWindowTheme.Teal;
                case "generation":
                    return UtilityWindowTheme.Purple;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private void DrawSupportPill(bool enabled, string label)
        {
            if (!enabled)
                return;

            DrawMetadataPill(label, UtilityWindowTheme.Cyan);
        }

        private void DrawMetadataPill(string label, Color tint)
        {
            if (string.IsNullOrWhiteSpace(label))
                return;

            UtilityWindowTheme.CountPill(label, tint, Mathf.Clamp(label.Length * 7f + 20f, 58f, 150f));
        }

        private void OpenUtility(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;

            utility.Open();
            RecordRecentUtility(utility.Id);
            _recents.RemoveAll(id => string.Equals(id, utility.Id, StringComparison.OrdinalIgnoreCase));
            _recents.Insert(0, utility.Id);
            while (_recents.Count > 10)
                _recents.RemoveAt(_recents.Count - 1);
            _status = "Opened " + utility.DisplayName + ".";
            SavePrefs();
        }

        private void ToggleFavorite(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (!_favorites.Add(id))
                _favorites.Remove(id);

            SavePrefs();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetString(PrefLab, _lab);
            UtilityWindowPrefs.SetString(PrefCategory, _category);
            UtilityWindowPrefs.SetString(PrefStatus, _statusFilter);
            UtilityWindowPrefs.SetBool(PrefShowHelp, _showHelp);
            UtilityWindowPrefs.SetString(PrefFavorites, EncodeList(_favorites));
            UtilityWindowPrefs.SetString(PrefRecents, EncodeList(_recents));
        }

        private static string BuildUtilityTooltip(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return string.Empty;

            return utility.DisplayName + "\n" +
                   PungentUtilityRegistry.GetLab(utility) + " / " + PungentUtilityRegistry.GetModule(utility) + "\n" +
                   PungentUtilityRegistry.GetStatus(utility) + ": " + PungentUtilityPackageStatus.GetDescription(PungentUtilityRegistry.GetStatus(utility)) + "\n\n" +
                   utility.Description;
        }

        private static string EncodeList(IEnumerable<string> values)
        {
            return string.Join("|", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Replace("|", string.Empty)));
        }

        private static List<string> DecodeList(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return new List<string>();

            return encoded.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
    #endif

}