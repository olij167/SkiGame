using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Developer;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.ProjectAudit;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Product-facing browser for reusable PungentFunk utilities.
    /// Release-readiness and architecture diagnostics live in the Design Validation Audit instead.
    /// </summary>
    public sealed class PungentUtilityControlPanelWindow : EditorWindow
    {
        private enum BrowserViewMode
        {
            Compact,
            List
        }

        private enum BrowserItemKindFilter
        {
            UtilitiesAndActions,
            Utilities,
            Actions
        }

        private enum BrowserTab
        {
            Welcome,
            Browse,
            OtherPackages
        }

        private enum BrowserToolbarOverlay
        {
            None,
            Filters,
            Favourites,
            History,
            Settings
        }

        private enum PackageAvailabilityFilter
        {
            AllUtilities,
            OwnedUtilities,
            InstalledUtilities
        }

        private enum PackageCardPreviewMode
        {
            ActualState,
            PreviewInstalled,
            PreviewOwnedNotInstalled,
            PreviewAvailableNotOwned,
            PreviewMissingRequiredPackage,
            PreviewDisabled,
            PreviewComingSoonUnavailable
        }

        private enum PackageCardVisualState
        {
            Installed,
            OwnedNotInstalled,
            Available,
            MissingRequiredPackage,
            Disabled,
            ComingSoonUnavailable
        }

        private const string PrefPrefix = "PungentFunkUtilities.ControlPanel.";
        private const string PrefTab = PrefPrefix + "Tab";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefPackageSearch = PrefPrefix + "PackageSearch";
        private const string PrefPackageFocus = PrefPrefix + "PackageFocus";
        private const string PrefOtherPackageFocus = PrefPrefix + "OtherPackageFocus";
        private const string PrefPackageCardPreviewMode = PrefPrefix + "PackageCardPreviewMode";
        private const string PrefCategory = PrefPrefix + "AreaCategory";
        private const string PrefLegacyCategory = PrefPrefix + "Category";
        private const string PrefType = PrefPrefix + "Type";
        private const string PrefLegacyLab = PrefPrefix + "Lab";
        private const string PrefStatus = PrefPrefix + "Status";
        private const string PrefFavorites = PrefPrefix + "Favorites";
        private const string PrefRecents = PrefPrefix + "Recents";
        private const string PrefViewMode = PrefPrefix + "ViewMode";
        private const string PrefShowFilters = PrefPrefix + "ShowFilters";
        private const string PrefShowFavorites = PrefPrefix + "ShowFavorites";
        private const string PrefShowHistory = PrefPrefix + "ShowHistory";
        private const string PrefShowSettings = PrefPrefix + "ShowSettings";
        private const string PrefShowRelated = PrefPrefix + "ShowRelated";
        private const string PrefShowCapabilityTags = PrefPrefix + "ShowCapabilityTags";
        private const string PrefGroupByType = PrefPrefix + "GroupByType";
        private const string PrefSelectedUtilityId = PrefPrefix + "SelectedUtilityId";
        private const string PrefSortMode = PrefPrefix + "SortMode";
        private const string PrefIncludeAccessories = PrefPrefix + "IncludeAccessories";
        private const string PrefShowNestedActions = PrefPrefix + "ShowNestedActions";
        private const string PrefDrawerActions = PrefPrefix + "DrawerActions";
        private const string PrefDrawerAccessories = PrefPrefix + "DrawerAccessories";
        private const string PrefDrawerRelated = PrefPrefix + "DrawerRelated";
        private const string PrefDrawerHelp = PrefPrefix + "DrawerHelp";
        private const string PrefDrawerPackage = PrefPrefix + "DrawerPackage";
        private const string PrefFilterMissingDocs = PrefPrefix + "FilterMissingDocs";
        private const string PrefFilterHasDocs = PrefPrefix + "FilterHasDocs";
        private const string PrefFilterExperimental = PrefPrefix + "FilterExperimental";
        private const string PrefFilterInProgress = PrefPrefix + "FilterInProgress";
        private const string PrefFilterNoFacets = PrefPrefix + "FilterNoFacets";
        private const string PrefItemKindFilter = PrefPrefix + "ItemKindFilter";
        private const string PrefPackageAvailabilityFilter = PrefPrefix + "PackageAvailabilityFilter";
        private const string PrefShowHiddenInternal = PrefPrefix + "ShowHiddenInternal";
        private const string PrefShowArchived = PrefPrefix + "ShowArchived";
        private const string PrefSidebarWidth = PrefPrefix + "SidebarWidth";
        private const string AllFilter = "All";
        private const float NarrowBrowseBreakpoint = 820f;
        private const int CompactCullingThreshold = 40;
        private const int CompactCullingBuffer = 8;
        private const float CompactCardEstimatedHeight = 92f;
        private const float CompactDrawerBaseEstimatedHeight = 124f;
        private const float CompactDrawerRowEstimatedHeight = 34f;

        private Vector2 _categoryScroll;
        private Vector2 _browserScroll;
        private Vector2 _welcomeScroll;
        private Vector2 _packagesScroll;
        private Vector2 _favoritesScroll;
        private Vector2 _historyScroll;
        private string _search = string.Empty;
        private string _packageSearch = string.Empty;
        private string _focusedPackageId = string.Empty;
        private string _category = AllFilter;
        private string _type = AllFilter;
        private string _statusFilter = AllFilter;
        private BrowserViewMode _viewMode = BrowserViewMode.Compact;
        private BrowserItemKindFilter _itemKindFilter = BrowserItemKindFilter.UtilitiesAndActions;
        private PungentUtilityBrowserSortMode _sortMode = PungentUtilityBrowserSortMode.Recommended;
        private BrowserTab _selectedTab = BrowserTab.Browse;
        private PackageAvailabilityFilter _packageAvailabilityFilter = PackageAvailabilityFilter.AllUtilities;
        private PackageCardPreviewMode _packageCardPreviewMode = PackageCardPreviewMode.ActualState;
        private bool _showFilters;
        private bool _showFavorites;
        private bool _showHistory;
        private bool _showSettings;
        private bool _showRelated = true;
        private bool _showCapabilityTags = true;
        private bool _groupByType = true;
        private bool _includeAccessories;
        private bool _showNestedActions;
        private bool _drawerActions = true;
        private bool _drawerAccessories = true;
        private bool _drawerRelated = true;
        private bool _drawerHelp = true;
        private bool _drawerPackage = true;
        private bool _filterMissingDocs;
        private bool _filterHasDocs;
        private bool _filterExperimental;
        private bool _filterInProgress;
        private bool _filterNoFacets;
        private bool _showHiddenInternal;
        private bool _showArchived;
        private float _sidebarWidth = 184f;
        private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _recents = new List<string>();
        private readonly Dictionary<string, bool> _canRunCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PungentUtilityDescriptor> _cachedQuery = new List<PungentUtilityDescriptor>();
        private readonly List<CategoryGroupView> _cachedCategoryGroups = new List<CategoryGroupView>();
        private readonly PungentUtilityBrowserViewModel _browserViewModel = new PungentUtilityBrowserViewModel();
        private string[] _cachedCategories = new[] { AllFilter };
        private string[] _cachedTypes = new[] { AllFilter };
        private string[] _cachedStatuses = new[] { AllFilter };
        private int _cachedSourceCount;
        private bool _cacheDirty = true;
        private BrowserDirtyFlags _cacheDirtyFlags = BrowserDirtyFlags.All;
        private string _selectedUtilityId = string.Empty;
        private string _status = "Ready.";
        private double _nextAllowedRepaintTime;
        private PungentUtilityDescriptor _packageDetailUtility;
        private PungentUtilityPackageRecord _packageDetailRecord;
        private string _packageDetailMessage = string.Empty;
        private readonly Dictionary<string, PackageEditDraft> _packageEditDrafts = new Dictionary<string, PackageEditDraft>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _expandedPackageEditors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _expandedPackageUtilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _expandedPackageDetails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _focusedOtherPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _showPackageDeveloperPreview;
        private bool _showWelcomeDeveloperTools;
        private BrowserToolbarOverlay _activeBrowserOverlay = BrowserToolbarOverlay.None;
        private Rect _browserOverlayAnchorRect;
        private Rect _browserOverlayRect;
        private Vector2 _browserOverlayScroll;

        private sealed class CategoryGroupView
        {
            public string Category;
            public int Count;
            public readonly List<TypeGroupView> Types = new List<TypeGroupView>();
        }

        private sealed class TypeGroupView
        {
            public string Name;
            public readonly List<PungentUtilityDescriptor> Utilities = new List<PungentUtilityDescriptor>();
        }

        private sealed class PackageEditDraft
        {
            public string displayName;
            public PungentUtilityPackageTier tier;
            public PungentUtilityPackageAvailability availability;
            public bool developerOverrideAvailability;
            public string assetStoreUrl;
            public string packageManagerId;
            public string importPackagePath;
            public string description;
            public string includedUtilityIdsText;
            public string includedCapabilitiesText;
            public string installHint;
            public bool showPromotionalPricing;
            public string currencyCode;
            public string regularPriceText;
            public string salePriceText;
            public int discountPercent;
            public string saleEndsIsoUtc;
            public string promotionalLabel;
        }

        private sealed class PackageCardView
        {
            public PungentUtilityPackageAvailability ActualAvailability;
            public PackageCardVisualState VisualState;
            public string StateLabel;
            public string StateDescription;
            public Color Tint;
            public bool IsSimulated;
            public bool AllowsRealActions;
        }

        public static void Open()
        {
            PungentUtilityControlPanelWindow window = GetWindow<PungentUtilityControlPanelWindow>("Utilities Browser");
            window.minSize = new Vector2(720f, 460f);
            window.Show();
        }

        public static void OpenCategory(string categoryName)
        {
            string normalizedCategory = string.IsNullOrWhiteSpace(categoryName) ? AllFilter : PungentUtilityCategories.Normalize(categoryName);
            UtilityWindowPrefs.SetString(PrefCategory, normalizedCategory);
            UtilityWindowPrefs.SetString(PrefType, AllFilter);
            UtilityWindowPrefs.SetString(PrefTab, BrowserTab.Browse.ToString());
            UtilityWindowPrefs.SetString(PrefPackageFocus, string.Empty);

            PungentUtilityControlPanelWindow window = GetWindow<PungentUtilityControlPanelWindow>("Utilities Browser");
            window.minSize = new Vector2(720f, 460f);
            window._selectedTab = BrowserTab.Browse;
            window._focusedPackageId = string.Empty;
            window._category = normalizedCategory;
            window._type = AllFilter;
            window.MarkCacheDirty();
            window.Show();
        }

        public static void OpenUtilityCard(string utilityId)
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            string normalizedCategory = descriptor == null ? AllFilter : PungentUtilityRegistry.GetAreaCategory(descriptor);
            string search = descriptor == null || string.IsNullOrWhiteSpace(descriptor.DisplayName) ? utilityId ?? string.Empty : descriptor.DisplayName;

            UtilityWindowPrefs.SetString(PrefCategory, normalizedCategory);
            UtilityWindowPrefs.SetString(PrefType, AllFilter);
            UtilityWindowPrefs.SetString(PrefStatus, AllFilter);
            UtilityWindowPrefs.SetString(PrefSearch, search);
            UtilityWindowPrefs.SetString(PrefItemKindFilter, BrowserItemKindFilter.UtilitiesAndActions.ToString());
            UtilityWindowPrefs.SetString(PrefSelectedUtilityId, descriptor == null ? string.Empty : descriptor.Id);
            UtilityWindowPrefs.SetString(PrefTab, BrowserTab.Browse.ToString());
            UtilityWindowPrefs.SetString(PrefPackageFocus, string.Empty);

            PungentUtilityControlPanelWindow window = GetWindow<PungentUtilityControlPanelWindow>("Utilities Browser");
            window.minSize = new Vector2(720f, 460f);
            window._selectedTab = BrowserTab.Browse;
            window._focusedPackageId = string.Empty;
            window._category = normalizedCategory;
            window._type = AllFilter;
            window._statusFilter = AllFilter;
            window._search = search;
            window._itemKindFilter = BrowserItemKindFilter.UtilitiesAndActions;
            window._selectedUtilityId = descriptor == null ? string.Empty : descriptor.Id;
            window.MarkCacheDirty();
            window.Show();
        }

        public static void OpenOtherPackagesFocused(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                Open();
                return;
            }

            OpenOtherPackagesFocused(new[] { packageId });
        }

        public static void OpenOtherPackagesFocused(IEnumerable<string> packageIds)
        {
            string[] ids = packageIds == null
                ? new string[0]
                : packageIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(PungentUtilityPackageCatalog.NormalizePackageId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            PungentUtilityControlPanelWindow window = GetWindow<PungentUtilityControlPanelWindow>("Utilities Browser");
            window.minSize = new Vector2(720f, 460f);
            window._selectedTab = BrowserTab.OtherPackages;
            window._focusedOtherPackageIds.Clear();
            for (int i = 0; i < ids.Length; i++)
                window._focusedOtherPackageIds.Add(ids[i]);

            window._packageSearch = ids.Length == 1 ? PungentUtilityPackageCatalog.GetDisplayName(ids[0]) : string.Empty;
            if (ids.Length == 1)
            {
                window._expandedPackageDetails.Add(ids[0]);
                PungentUtilityPackageRecord record = PungentUtilityPackageCatalog.FindRecord(ids[0]) ?? PungentUtilityPackageCatalog.CreateEditableRecord(ids[0]);
                window._packageDetailRecord = PungentUtilityPackageCatalog.CloneRecord(record);
                window._packageDetailUtility = null;
                window._packageDetailMessage = "Opened from an optional integration control.";
            }

            window._status = ids.Length == 0 ? "Opened Other Packages." : "Focused Other Packages on " + ids.Length + " package(s).";
            window.SavePrefs();
            window.Show();
            window.Repaint();
        }

        [Obsolete("Use OpenCategory. The old Lab terminology is retained only for compatibility with earlier editor scripts.")]
        public static void OpenLab(string labName)
        {
            OpenCategory(labName);
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
            titleContent = new GUIContent("Utilities Browser");
            PungentUtilityRegistry.Changed -= HandleRegistryChanged;
            PungentUtilityRegistry.Changed += HandleRegistryChanged;
            PungentUtilityCategoryOverrides.Changed -= HandleCategoryOverridesChanged;
            PungentUtilityCategoryOverrides.Changed += HandleCategoryOverridesChanged;
            PungentUtilityPackageCatalog.Changed -= HandlePackageCatalogChanged;
            PungentUtilityPackageCatalog.Changed += HandlePackageCatalogChanged;
            PungentUtilityPackageSimulation.Changed -= HandlePackageSimulationChanged;
            PungentUtilityPackageSimulation.Changed += HandlePackageSimulationChanged;
            PungentUtilityAccessPolicy.Changed -= HandleUtilityAccessPolicyChanged;
            PungentUtilityAccessPolicy.Changed += HandleUtilityAccessPolicyChanged;
            _selectedTab = LoadBrowserTab();
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _packageSearch = UtilityWindowPrefs.GetString(PrefPackageSearch, string.Empty);
            _focusedPackageId = UtilityWindowPrefs.GetString(PrefPackageFocus, string.Empty);
            _focusedOtherPackageIds.Clear();
            foreach (string id in DecodeList(UtilityWindowPrefs.GetString(PrefOtherPackageFocus, string.Empty)))
                _focusedOtherPackageIds.Add(id);
            _category = UtilityWindowPrefs.GetString(PrefCategory, UtilityWindowPrefs.GetString(PrefLegacyLab, AllFilter));
            _category = _category == AllFilter ? AllFilter : PungentUtilityCategories.Normalize(_category);
            _type = UtilityWindowPrefs.GetString(PrefType, UtilityWindowPrefs.GetString(PrefLegacyCategory, AllFilter));
            _statusFilter = UtilityWindowPrefs.GetString(PrefStatus, AllFilter);
            _viewMode = LoadViewMode();
            _itemKindFilter = LoadItemKindFilter();
            _sortMode = LoadSortMode();
            _packageAvailabilityFilter = LoadPackageAvailabilityFilter();
            _packageCardPreviewMode = LoadPackageCardPreviewMode();
            _selectedUtilityId = UtilityWindowPrefs.GetString(PrefSelectedUtilityId, string.Empty);
            _showFilters = false;
            _showFavorites = false;
            _showHistory = false;
            _showSettings = false;
            _activeBrowserOverlay = BrowserToolbarOverlay.None;
            _showRelated = UtilityWindowPrefs.GetBool(PrefShowRelated, true);
            _showCapabilityTags = UtilityWindowPrefs.GetBool(PrefShowCapabilityTags, true);
            _groupByType = UtilityWindowPrefs.GetBool(PrefGroupByType, true);
            _includeAccessories = UtilityWindowPrefs.GetBool(PrefIncludeAccessories, false);
            _showNestedActions = UtilityWindowPrefs.GetBool(PrefShowNestedActions, false);
            _drawerActions = UtilityWindowPrefs.GetBool(PrefDrawerActions, true);
            _drawerAccessories = UtilityWindowPrefs.GetBool(PrefDrawerAccessories, true);
            _drawerRelated = UtilityWindowPrefs.GetBool(PrefDrawerRelated, true);
            _drawerHelp = UtilityWindowPrefs.GetBool(PrefDrawerHelp, true);
            _drawerPackage = UtilityWindowPrefs.GetBool(PrefDrawerPackage, true);
            _filterMissingDocs = UtilityWindowPrefs.GetBool(PrefFilterMissingDocs, false);
            _filterHasDocs = UtilityWindowPrefs.GetBool(PrefFilterHasDocs, false);
            _filterExperimental = UtilityWindowPrefs.GetBool(PrefFilterExperimental, false);
            _filterInProgress = UtilityWindowPrefs.GetBool(PrefFilterInProgress, false);
            _filterNoFacets = UtilityWindowPrefs.GetBool(PrefFilterNoFacets, false);
            _showHiddenInternal = UtilityWindowPrefs.GetBool(PrefShowHiddenInternal, false);
            _showArchived = UtilityWindowPrefs.GetBool(PrefShowArchived, false);
            _sidebarWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefSidebarWidth, 184f), 144f, 320f);

            _favorites.Clear();
            foreach (string id in DecodeList(UtilityWindowPrefs.GetString(PrefFavorites, string.Empty)))
                _favorites.Add(id);

            _recents.Clear();
            _recents.AddRange(DecodeList(UtilityWindowPrefs.GetString(PrefRecents, string.Empty)));
            MarkCacheDirty();
        }

        private void OnDisable()
        {
            PungentUtilityRegistry.Changed -= HandleRegistryChanged;
            PungentUtilityCategoryOverrides.Changed -= HandleCategoryOverridesChanged;
            PungentUtilityPackageCatalog.Changed -= HandlePackageCatalogChanged;
            PungentUtilityPackageSimulation.Changed -= HandlePackageSimulationChanged;
            PungentUtilityAccessPolicy.Changed -= HandleUtilityAccessPolicyChanged;
            SavePrefs();
        }

        private void HandleRegistryChanged()
        {
            _status = "Registry metadata refreshed.";
            MarkCacheDirty();
        }

        private void HandleCategoryOverridesChanged()
        {
            _status = "Category appearance refreshed.";
            MarkCacheDirty();
        }

        private void HandlePackageCatalogChanged()
        {
            _status = "Package catalog refreshed.";
            _packageEditDrafts.Clear();
            MarkCacheDirty();
        }

        private void HandlePackageSimulationChanged()
        {
            _status = PungentUtilityPackageSimulation.Active ? "Package simulation refreshed." : "Package simulation cleared.";
            MarkCacheDirty(BrowserDirtyFlags.PackageState);
            Repaint();
        }

        private void HandleUtilityAccessPolicyChanged()
        {
            _status = PungentUtilityAccessPolicy.ExperimentalUtilitiesEnabled ? "Experimental utilities enabled." : "Experimental utilities locked.";
            MarkCacheDirty();
        }

        private void OnGUI()
        {
            PungentEditorPerformanceUtility.RecordWindowRepaint(this);
            RefreshCachesIfNeeded();
            HandleBrowserOverlayInput();
            HandlePackageDetailsKeyboard();

            int total = CountSourceItemsForHeader();
            string diagnostics = PungentEditorPerformanceUtility.DiagnosticsEnabled
                ? $" · {PungentEditorPerformanceUtility.GetRecentWindowRepaintCount(this)} repaints/s"
                : string.Empty;
            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions
            {
                UtilityId = "utilities-browser",
                Title = "Utilities Browser",
                Description = "Browse, filter, open utilities, run registered actions, and discover optional packages.",
                Status = $"{_cachedQuery.Count}/{total} shown - {_status}{diagnostics}",
                CompactStatus = _status,
                HelpSectionId = "overview",
                HelpTopicId = "overview",
                ShowHelp = true,
                ShowMinimizeTray = true,
                ShowMinimizeButton = true,
                Tint = UtilityWindowTheme.HeaderTint
            });

            DrawTopLevelTabs();

            switch (_selectedTab)
            {
                case BrowserTab.Welcome:
                    DrawWelcomeTab();
                    break;
                case BrowserTab.OtherPackages:
                    DrawOtherPackagesTab();
                    break;
                default:
                    DrawBrowseTab();
                    break;
            }

            DrawBrowserOverlay();
        }

        private void HandlePackageDetailsKeyboard()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode != KeyCode.Escape || _expandedPackageDetails.Count == 0)
                return;

            ClosePackageDetails();
            current.Use();
        }

        private void DrawBrowseTab()
        {
            DrawToolbar();
            DrawPackageFocusBanner();
            DrawOwnershipFilterBanner();

            if (position.width < NarrowBrowseBreakpoint)
            {
                DrawNarrowBrowseTab();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCategorySidebar();
                UtilityWindowTheme.HorizontalResizeHandle(ref _sidebarWidth, 144f, Mathf.Min(360f, Mathf.Max(180f, position.width - 360f)), SavePrefs, "Drag to resize category sidebar");
                DrawBrowserPane();
            }
        }

        private void DrawNarrowBrowseTab()
        {
            DrawCompactCategorySelector();
            DrawBrowserPane();
        }

        private void DrawTopLevelTabs()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.05f, 5, 4)))
            {
                DrawBrowserTabButton(BrowserTab.Welcome, "Welcome", "Onboarding, help, bug-report routing, and support links.");
                DrawBrowserTabButton(BrowserTab.Browse, "Browse", "Browse, filter, open utilities, and run registered actions.");
                DrawBrowserTabButton(BrowserTab.OtherPackages, "Other Packages", "Discover optional packages and see what they contain.");
                GUILayout.FlexibleSpace();
                DrawPackageStateBadgeSummary();
            }
        }

        private void DrawBrowserTabButton(BrowserTab tab, string label, string tooltip)
        {
            bool selected = _selectedTab == tab;
            Color tint = selected ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral;
            using (UtilityWindowTheme.Background(tint))
            {
                float width = tab == BrowserTab.OtherPackages ? 126f : 94f;
                UtilityWindowTheme.PungentButtonRole role = selected ? UtilityWindowTheme.PungentButtonRole.Primary : UtilityWindowTheme.PungentButtonRole.Toolbar;
                if (UtilityWindowTheme.StyledButton(new GUIContent(label, tooltip), role, GUILayout.Width(width), GUILayout.Height(24f)))
                {
                    CloseBrowserOverlay();
                    _selectedTab = tab;
                    SavePrefs();
                    Repaint();
                }
            }
        }

        private void DrawWelcomeTab()
        {
            _welcomeScroll = EditorGUILayout.BeginScrollView(_welcomeScroll, GUILayout.ExpandHeight(true));

            DrawWelcomeHeroSection();
            DrawWelcomeStartHereSection();
            DrawWelcomeHelpLinksSection();
            DrawWelcomeFindToolsSection();
            DrawWelcomeCardReadingSection();
            DrawWelcomePackageDiscoverySection();
            DrawWelcomeFeedbackSection();
            DrawWelcomeFooterLinksSection();
            DrawWelcomeCustomSections();
            DrawWelcomeDeveloperTools();

            EditorGUILayout.EndScrollView();
        }

        private void DrawWelcomeHeroSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.16f, 0.07f, 8, 5)))
            {
                DrawWelcomeHeroTitle("Welcome");
                DrawWelcomeBody("This is the central launcher for PungentFunk editor utilities, contextual help, and optional package discovery.");
                DrawWelcomeActionRow(
                    new WelcomeAction("Browse Utilities", SwitchToBrowse, UtilityWindowTheme.Blue, 132f, UtilityWindowTheme.PungentButtonRole.Primary),
                    new WelcomeAction("Open Help Browser", OpenHelpWelcomeTopic, UtilityWindowTheme.Teal, 150f),
                    new WelcomeAction("View Other Packages", SwitchToOtherPackages, UtilityWindowTheme.Purple, 156f));
            }
        }

        private void DrawWelcomeStartHereSection()
        {
            DrawWelcomePlainSection("Start Here", () =>
            {
                DrawWelcomeStep("Browse opens core utilities and keeps actions/accessory tools contextual.");
                DrawWelcomeStep("Help Browser is the searchable documentation hub.");
                DrawWelcomeStep("Other Packages shows optional extensions, install states, and package routes.");
            });
        }

        private void DrawWelcomeHelpLinksSection()
        {
            DrawWelcomePlainSection("How Help Works", () =>
            {
                DrawWelcomeStep("[?] opens contextual help for the tool, section, card, or workflow you are viewing.");
                DrawWelcomeStep("Help can appear inline, as an overlay, or as a Help Browser topic depending on context.");
                DrawWelcomeStep("Utility cards and selected drawers include docs/help access when documentation exists.");
                DrawWelcomeActionRow(
                    new WelcomeAction("Open Help Browser", OpenHelpWelcomeTopic, UtilityWindowTheme.Teal, 150f),
                    new WelcomeAction("Learn About [?] Help", () => OpenHelpTopicSafe("utilities-browser", "overview", "overview"), UtilityWindowTheme.Neutral, 164f, UtilityWindowTheme.PungentButtonRole.Link));
            });
        }

        private void DrawWelcomeFindToolsSection()
        {
            DrawWelcomePlainSection("How To Find Tools", () =>
            {
                DrawWelcomeStep("Search can match utilities, actions, tags, package metadata, and documentation metadata.");
                DrawWelcomeStep("Categories group tools by workflow; filters narrow by type, package, state, actions, and accessories.");
                DrawWelcomeStep("Sort changes ordering, and active filter chips explain why results are narrowed.");
                DrawWelcomeActionRow(new WelcomeAction("Open Browse Tab", SwitchToBrowse, UtilityWindowTheme.Blue, 132f));
            });
        }

        private void DrawWelcomeCardReadingSection()
        {
            DrawWelcomePlainSection("How To Read Utility Cards", () =>
            {
                DrawWelcomeStep("Card titles, one-line descriptions, category chips, package/status chips, and role chips show what a utility is for.");
                DrawWelcomeStep("Pin controls keep favorites close; Open/Run is the primary action.");
                DrawWelcomeStep("Selecting a card expands actions, included tools, related workflows, help, package notes, and Developer metadata when enabled.");
            });
        }

        private void DrawWelcomePackageDiscoverySection()
        {
            DrawWelcomePlainSection("Other Packages", () =>
            {
                DrawWelcomeStep("Package cards show installed, owned-not-installed, available, missing-requirement, disabled, or coming-soon states.");
                DrawWelcomeStep("Installed cards route back to Browse; unowned cards use configured store/learn-more links.");
                DrawWelcomeStep("Optional integrations can open Other Packages focused on the required extension.");
                DrawWelcomeActionRow(new WelcomeAction("View Other Packages", SwitchToOtherPackages, UtilityWindowTheme.Purple, 156f));
            });
        }

        private void DrawWelcomeFeedbackSection()
        {
            DrawWelcomePlainSection("Support Requests", () =>
            {
                DrawWelcomeBody("Bug reports and feature requests are saved as Sticky Notes drafts, then sent through the configured Wix relay when ready.");
                DrawWelcomeActionRow(
                    new WelcomeAction("New Request", OpenBugReportPopup, UtilityWindowTheme.Teal, 124f, UtilityWindowTheme.PungentButtonRole.Warning),
                    new WelcomeAction("Open Requests", PungentSupportRequestBridge.OpenRequests, UtilityWindowTheme.Cyan, 128f));
            });
        }

        private void DrawWelcomeFooterLinksSection()
        {
            DrawWelcomePlainSection("Links", () =>
            {
                DrawSupportDevelopmentControls();
            });
        }

        private void DrawWelcomeCustomSections()
        {
            PungentUtilityExternalLinksSettings settings = PungentUtilityExternalLinksSettings.instance;
            if (settings.welcomeSections == null || settings.welcomeSections.Count == 0)
                return;

            for (int i = 0; i < settings.welcomeSections.Count; i++)
            {
                PungentUtilityWelcomeSection section = settings.welcomeSections[i];
                if (section == null || !section.enabled || !WelcomeSectionHasVisibleContent(section))
                    continue;

                DrawWelcomePlainSection(string.IsNullOrWhiteSpace(section.title) ? "Custom Section" : section.title.Trim(), () =>
                {
                    if (!string.IsNullOrWhiteSpace(section.body))
                        DrawWelcomeBody(section.body.Trim());

                    DrawWelcomeCustomLinkButtons(section);
                });
            }
        }

        private void DrawWelcomePlainSection(string title, Action drawContent)
        {
            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.055f, 0.025f, 7, 3)))
            {
                DrawWelcomeSectionTitle(title);
                EditorGUILayout.Space(2f);
                drawContent?.Invoke();
            }
            EditorGUILayout.Space(2f);
        }

        private void DrawWelcomeHeroTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return;

            EditorGUILayout.LabelField(title.Trim(), UtilityWindowTheme.WelcomeHeroTitleStyle);
        }

        private void DrawWelcomeSectionTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return;

            EditorGUILayout.LabelField(title.Trim(), UtilityWindowTheme.WelcomeSectionTitleStyle);
        }

        private void DrawWelcomeBody(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            EditorGUILayout.LabelField(text.Trim(), UtilityWindowTheme.WelcomeBodyStyle);
        }

        private void DrawWelcomeMeta(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            EditorGUILayout.LabelField(text.Trim(), UtilityWindowTheme.WelcomeMetaStyle);
        }

        private void DrawWelcomeStep(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("•", UtilityWindowTheme.WelcomeStepLabelStyle, GUILayout.Width(14f));
                EditorGUILayout.LabelField(text.Trim(), UtilityWindowTheme.WelcomeBodyStyle);
            }
        }

        private void DrawWelcomeStep(string label, string text)
        {
            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(text))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrWhiteSpace(label))
                    GUILayout.Label(label.Trim(), UtilityWindowTheme.WelcomeStepLabelStyle, GUILayout.Width(Mathf.Clamp(label.Length * 7f + 10f, 42f, 96f)));
                EditorGUILayout.LabelField((text ?? string.Empty).Trim(), UtilityWindowTheme.WelcomeBodyStyle);
            }
        }

        private struct WelcomeAction
        {
            public readonly string label;
            public readonly Action action;
            public readonly Color tint;
            public readonly float width;
            public readonly UtilityWindowTheme.PungentButtonRole role;

            public WelcomeAction(string label, Action action, Color tint, float width, UtilityWindowTheme.PungentButtonRole role = UtilityWindowTheme.PungentButtonRole.Secondary)
            {
                this.label = label;
                this.action = action;
                this.tint = tint;
                this.width = width;
                this.role = role;
            }
        }

        private void DrawWelcomeActionRow(params WelcomeAction[] actions)
        {
            bool stack = position.width < 760f;
            if (stack)
            {
                for (int i = 0; i < actions.Length; i++)
                    DrawWelcomeAction(actions[i], true);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < actions.Length; i++)
                    DrawWelcomeAction(actions[i], false);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawWelcomeAction(WelcomeAction action, bool stacked)
        {
            UtilityWindowTheme.PungentButtonRole role = action.role;
            if (UtilityWindowTheme.StyledButton(new GUIContent(action.label), role, GUILayout.Width(stacked ? Mathf.Min(action.width + 32f, 260f) : action.width), GUILayout.Height(24f)))
                action.action?.Invoke();
        }

        private void OpenFeedback(PungentFeedbackType type)
        {
            if (type == PungentFeedbackType.BugReport)
            {
                OpenBugReportPopup();
                return;
            }

            PungentUtilityExternalLinksSettings settings = PungentUtilityExternalLinksSettings.instance;
            if (settings.TryOpenFeedback(type, out string message))
                _status = message;
            else
                _status = message + " Configure it in Developer Tools > Welcome And External Links.";
        }

        private void OpenBugReportPopup()
        {
            PungentBugReportContext context = new PungentBugReportContext
            {
                utilityId = "utilities-browser",
                sectionId = "welcome",
                topicId = "bug-reporting",
                contextLabel = "Utilities Browser welcome feedback",
                contextPath = "Utilities Browser > Welcome > Bug Reports",
                sourceWindow = "Utilities Browser"
            };
            context.Normalize();
            PungentBugReportOverlay.Show(new Rect(Mathf.Max(8f, position.width - 220f), 160f, 1f, 1f), context);
            _status = "Opened support request draft in Sticky Notes.";
        }

        private void SwitchToBrowse()
        {
            _selectedTab = BrowserTab.Browse;
            SavePrefs();
            Repaint();
        }

        private void SwitchToOtherPackages()
        {
            _selectedTab = BrowserTab.OtherPackages;
            SavePrefs();
            Repaint();
        }

        private void DrawSupportDevelopmentControls()
        {
            PungentUtilityExternalLinksSettings settings = PungentUtilityExternalLinksSettings.instance;
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawConfiguredExternalLinkButton("Support Development", settings.supportDevelopmentUrl, 156f, true);
                DrawConfiguredExternalLinkButton("Website", settings.websiteUrl, 86f, false);
                DrawConfiguredExternalLinkButton("Documentation", settings.documentationUrl, 118f, false);
                DrawConfiguredExternalLinkButton("Community", settings.communityUrl, 100f, false);
                DrawConfiguredExternalLinkButton("Contact", settings.contactUrl, 80f, false);
                DrawConfiguredExternalLinkButton("Publisher", settings.publisherPageUrl, 88f, false);
                GUILayout.FlexibleSpace();
            }

            if (!PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(settings.supportDevelopmentUrl))
                DrawWelcomeMeta("Support link not configured yet.");
        }

        private void DrawConfiguredExternalLinkButton(string label, string url, float width, bool showWhenMissing)
        {
            bool configured = PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(url);
            if (!configured && !showWhenMissing)
                return;

            using (new EditorGUI.DisabledScope(!configured))
            {
                string buttonLabel = configured ? label : label + " not configured";
                float buttonWidth = configured ? width : Mathf.Max(width, 174f);
                UtilityWindowTheme.PungentButtonRole role = configured ? UtilityWindowTheme.PungentButtonRole.Link : UtilityWindowTheme.PungentButtonRole.Disabled;
                if (UtilityWindowTheme.StyledButton(new GUIContent(buttonLabel, configured ? url : "Developer Mode can configure this project-local URL."), role, GUILayout.Width(buttonWidth), GUILayout.Height(24f)))
                {
                    if (PungentUtilityExternalLinksSettings.TryOpenUrl(url, label, out string message))
                        _status = message;
                    else
                        _status = message;
                }
            }
        }

        private void DrawWelcomeCustomLinkButtons(PungentUtilityWelcomeSection section)
        {
            if (section == null || section.links == null || section.links.Count == 0)
                return;

            bool stack = position.width < 760f;
            if (stack)
            {
                for (int i = 0; i < section.links.Count; i++)
                    DrawWelcomeCustomLinkButton(section.links[i], true);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < section.links.Count; i++)
                {
                    DrawWelcomeCustomLinkButton(section.links[i], false);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawWelcomeCustomLinkButton(PungentUtilityWelcomeLink link, bool stacked)
        {
            if (link == null || !link.enabled || string.IsNullOrWhiteSpace(link.label))
                return;

            bool configured = PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(link.url);
            using (new EditorGUI.DisabledScope(!configured))
            {
                string label = configured ? link.label.Trim() : link.label.Trim() + " not configured";
                float width = stacked ? Mathf.Min(Mathf.Max(160f, position.width - 56f), 260f) : Mathf.Clamp(label.Length * 7f + 30f, 88f, 210f);
                UtilityWindowTheme.PungentButtonRole role = configured ? UtilityWindowTheme.PungentButtonRole.Link : UtilityWindowTheme.PungentButtonRole.Disabled;
                if (UtilityWindowTheme.StyledButton(new GUIContent(label, configured ? link.url : "Developer Mode can configure this URL."), role, GUILayout.Width(width), GUILayout.Height(24f)))
                {
                    if (PungentUtilityExternalLinksSettings.TryOpenUrl(link.url, link.label, out string message))
                        _status = message;
                    else
                        _status = message;
                }
            }
        }

        private void DrawWelcomeDeveloperTools()
        {
            if (!IsDeveloperModeEnabled())
                return;

            EditorGUILayout.Space(10f);
            _showWelcomeDeveloperTools = EditorGUILayout.Foldout(_showWelcomeDeveloperTools, "Developer Welcome Content", true);
            if (!_showWelcomeDeveloperTools)
                return;

            PungentUtilityExternalLinksSettings settings = PungentUtilityExternalLinksSettings.instance;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 5, 2)))
            {
                EditorGUILayout.LabelField("Custom sections are project-local and appear below Community and Support for normal users.", UtilityWindowTheme.MutedMiniLabelStyle);
                DrawDeveloperExternalLinksEditor(settings);

                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Custom Welcome Sections", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Add Section", "Add a project-local custom Welcome section."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(94f), GUILayout.Height(22f)))
                    {
                        Undo.RecordObject(settings, "Add Welcome Section");
                        if (settings.welcomeSections == null)
                            settings.welcomeSections = new List<PungentUtilityWelcomeSection>();
                        settings.welcomeSections.Add(new PungentUtilityWelcomeSection
                        {
                            title = "Community",
                            body = "Add project-specific support, community, release, or donation links here.",
                            links = new List<PungentUtilityWelcomeLink>()
                        });
                        settings.SaveStore();
                    }
                }

                if (settings.welcomeSections == null || settings.welcomeSections.Count == 0)
                {
                    EditorGUILayout.LabelField("No custom Welcome sections configured.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                for (int i = 0; i < settings.welcomeSections.Count; i++)
                    DrawWelcomeCustomSectionEditor(settings, i);
            }
        }

        private void DrawDeveloperExternalLinksEditor(PungentUtilityExternalLinksSettings settings)
        {
            EditorGUILayout.LabelField("Built-in Links", EditorStyles.boldLabel);
            DrawDeveloperUrlField(settings, "Support", settings.supportDevelopmentUrl, value => settings.supportDevelopmentUrl = value);
            DrawDeveloperUrlField(settings, "Website", settings.websiteUrl, value => settings.websiteUrl = value);
            DrawDeveloperUrlField(settings, "Documentation", settings.documentationUrl, value => settings.documentationUrl = value);
            DrawDeveloperUrlField(settings, "Community", settings.communityUrl, value => settings.communityUrl = value);
            DrawDeveloperUrlField(settings, "Contact", settings.contactUrl, value => settings.contactUrl = value);
            DrawDeveloperUrlField(settings, "Bug Report", settings.bugReportUrl, value => settings.bugReportUrl = value);
            DrawDeveloperUrlField(settings, "Feature Request", settings.featureRequestUrl, value => settings.featureRequestUrl = value);
            DrawDeveloperUrlField(settings, "Publisher / Asset Store", settings.publisherPageUrl, value => settings.publisherPageUrl = value);
        }

        private void DrawDeveloperUrlField(PungentUtilityExternalLinksSettings settings, string label, string value, Action<string> assign)
        {
            EditorGUI.BeginChangeCheck();
            string nextValue = EditorGUILayout.TextField(new GUIContent(label, "Project-local absolute URL."), value ?? string.Empty);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(settings, "Edit Welcome Link");
            assign(nextValue);
            settings.SaveStore();
            _status = PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(nextValue) || string.IsNullOrWhiteSpace(nextValue)
                ? label + " link updated."
                : label + " link is not a valid absolute URL.";
        }

        private void DrawWelcomeCustomSectionEditor(PungentUtilityExternalLinksSettings settings, int index)
        {
            if (settings == null || settings.welcomeSections == null || index < 0 || index >= settings.welcomeSections.Count)
                return;

            PungentUtilityWelcomeSection section = settings.welcomeSections[index];
            if (section == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.06f, 0.03f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool nextEnabled = EditorGUILayout.ToggleLeft(new GUIContent("Enabled", "Show this section on the Welcome tab."), section.enabled, GUILayout.Width(82f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(settings, "Toggle Welcome Section");
                        section.enabled = nextEnabled;
                        settings.SaveStore();
                    }

                    GUILayout.FlexibleSpace();
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Remove", "Remove this custom Welcome section."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(70f), GUILayout.Height(22f)))
                    {
                        Undo.RecordObject(settings, "Remove Welcome Section");
                        settings.welcomeSections.RemoveAt(index);
                        settings.SaveStore();
                        return;
                    }
                }

                EditorGUI.BeginChangeCheck();
                string nextTitle = EditorGUILayout.TextField(new GUIContent("Title", "Section heading."), section.title ?? string.Empty);
                EditorGUILayout.LabelField("Body", UtilityWindowTheme.MutedMiniLabelStyle);
                string nextBody = EditorGUILayout.TextArea(section.body ?? string.Empty, GUILayout.MinHeight(38f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(settings, "Edit Welcome Section");
                    section.title = nextTitle;
                    section.body = nextBody;
                    settings.SaveStore();
                }

                EditorGUILayout.Space(3f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Links", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Add Link", "Add a link button to this section."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(76f), GUILayout.Height(22f)))
                    {
                        Undo.RecordObject(settings, "Add Welcome Link");
                        if (section.links == null)
                            section.links = new List<PungentUtilityWelcomeLink>();
                        section.links.Add(new PungentUtilityWelcomeLink { label = "New Link" });
                        settings.SaveStore();
                    }
                }

                if (section.links == null || section.links.Count == 0)
                {
                    EditorGUILayout.LabelField("No links configured.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                for (int i = 0; i < section.links.Count; i++)
                    DrawWelcomeCustomLinkEditor(settings, section, i);
            }
        }

        private void DrawWelcomeCustomLinkEditor(PungentUtilityExternalLinksSettings settings, PungentUtilityWelcomeSection section, int index)
        {
            if (settings == null || section == null || section.links == null || index < 0 || index >= section.links.Count)
                return;

            PungentUtilityWelcomeLink link = section.links[index];
            if (link == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                bool nextEnabled = EditorGUILayout.Toggle(link.enabled, GUILayout.Width(18f));
                string nextLabel = EditorGUILayout.TextField(link.label ?? string.Empty, GUILayout.MinWidth(80f));
                string nextUrl = EditorGUILayout.TextField(link.url ?? string.Empty, GUILayout.MinWidth(160f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(settings, "Edit Welcome Link");
                    link.enabled = nextEnabled;
                    link.label = nextLabel;
                    link.url = nextUrl;
                    settings.SaveStore();
                }

                if (UtilityWindowTheme.StyledButton(new GUIContent("Remove", "Remove this link."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(62f), GUILayout.Height(22f)))
                {
                    Undo.RecordObject(settings, "Remove Welcome Link");
                    section.links.RemoveAt(index);
                    settings.SaveStore();
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(link.url) && !PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(link.url))
                EditorGUILayout.HelpBox("Link URL must be empty or an absolute http/https URL.", MessageType.Warning);
        }

        private static bool WelcomeSectionHasVisibleContent(PungentUtilityWelcomeSection section)
        {
            if (section == null)
                return false;

            if (!string.IsNullOrWhiteSpace(section.body))
                return true;

            return section.links != null && section.links.Any(link => link != null && link.enabled && !string.IsNullOrWhiteSpace(link.label));
        }

        private void OpenHelpWelcomeTopic()
        {
            if (TryOpenHelpTopic("help-browser", "welcome", "welcome"))
                return;
            if (TryOpenHelpTopic("utilities-browser", "welcome", "welcome"))
                return;
            if (TryOpenHelpTopic("help-browser", "getting-started", "getting-started"))
                return;
            if (TryOpenHelpTopic("utilities-browser", "getting-started", "getting-started"))
                return;
            if (TryOpenHelpTopic("help-browser", "overview", "welcome"))
                return;
            if (TryOpenHelpTopic("help-browser", "overview", "overview"))
                return;
            if (TryOpenHelpTopic("utilities-browser", "overview", "overview"))
                return;

            PungentUtilityHelpBrowserWindow.Open();
        }

        private bool TryOpenHelpTopic(string utilityId, string sectionId, string topicId)
        {
            if (PungentUtilityHelpRegistry.Find(utilityId, sectionId, topicId) == null)
                return false;

            PungentUtilityHelpRegistry.Open(utilityId, sectionId, topicId);
            return true;
        }

        private void DrawOtherPackagesTab()
        {
            List<PungentUtilityPackageRecord> allRecords = PungentUtilityPackageCatalog.Records
                .Where(record => IsDeveloperModeEnabled() || record.lifecycle == PungentUtilityPackageLifecycle.Visible)
                .ToList();
            IEnumerable<PungentUtilityPackageRecord> source = allRecords;
            if (_focusedOtherPackageIds.Count > 0)
                source = source.Where(record => record != null && _focusedOtherPackageIds.Contains(PungentUtilityPackageCatalog.NormalizePackageId(record.packageId)));

            List<PungentUtilityPackageRecord> searched = source
                .Where(record => PungentUtilityPackageCatalog.MatchesRecord(record, _packageSearch))
                .ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), GUILayout.ExpandHeight(true)))
            {
                DrawOtherPackagesHero(allRecords);
                DrawOtherPackageFocusBanner();
                DrawOtherPackagesSearch();
                DrawPackageDeveloperPreviewControls();
                DrawPackageDeveloperPreviewBanner();

                if (allRecords.Count == 0)
                {
                    EditorGUILayout.HelpBox("No package records are configured yet. Developer Mode can add package metadata and Asset Store links.", MessageType.Info);
                    return;
                }

                if (searched.Count == 0)
                {
                    EditorGUILayout.HelpBox("No package records match the current package search.", MessageType.Info);
                    return;
                }

                _packagesScroll = EditorGUILayout.BeginScrollView(_packagesScroll, GUILayout.ExpandHeight(true));
                DrawPackageDiscoverySections(searched);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawOtherPackagesHero(List<PungentUtilityPackageRecord> records)
        {
            int installed = records.Count(record => BuildPackageCardView(record).VisualState == PackageCardVisualState.Installed);
            int ownedNotInstalled = records.Count(record => BuildPackageCardView(record).VisualState == PackageCardVisualState.OwnedNotInstalled);
            int unowned = records.Count(record => BuildPackageCardView(record).VisualState == PackageCardVisualState.Available);
            int unavailable = records.Count(record =>
            {
                PackageCardVisualState state = BuildPackageCardView(record).VisualState;
                return state == PackageCardVisualState.MissingRequiredPackage || state == PackageCardVisualState.Disabled || state == PackageCardVisualState.ComingSoonUnavailable;
            });

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.14f, 0.06f, 8, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Expand Your Editor Toolkit", UtilityWindowTheme.Purple, unowned + " available");
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill("Installed " + installed, UtilityWindowTheme.Green, 94f);
                    UtilityWindowTheme.CountPill("Owned " + ownedNotInstalled, UtilityWindowTheme.Amber, 86f);
                    UtilityWindowTheme.CountPill("Available " + unowned, UtilityWindowTheme.Purple, 102f);
                    UtilityWindowTheme.CountPill("Later " + unavailable, UtilityWindowTheme.Neutral, 78f);
                }

                EditorGUILayout.LabelField("Browse optional PungentFunk packages by workflow, see what works with tools you already use, and jump to the configured store, import, or Browse route.", UtilityWindowTheme.MutedMiniLabelStyle);

                PungentUtilityExternalLinksSettings settings = PungentUtilityExternalLinksSettings.instance;
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawConfiguredExternalLinkButton("Publisher", settings.publisherPageUrl, 88f, false);
                    DrawConfiguredExternalLinkButton("Support", settings.supportDevelopmentUrl, 76f, false);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawOtherPackageFocusBanner()
        {
            if (_focusedOtherPackageIds.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.05f, 5, 2)))
            {
                EditorGUILayout.LabelField("Focused package view: " + string.Join(", ", _focusedOtherPackageIds.Select(PungentUtilityPackageCatalog.GetDisplayName).ToArray()), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (UtilityWindowTheme.StyledButton(new GUIContent("Show All Packages", "Clear the focused package CTA context."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(126f), GUILayout.Height(22f)))
                {
                    _focusedOtherPackageIds.Clear();
                    _packageSearch = string.Empty;
                    SavePrefs();
                    Repaint();
                }
            }
        }

        private void DrawOtherPackagesSearch()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(48f));
                string nextSearch = EditorGUILayout.TextField(_packageSearch, UtilityWindowTheme.ToolbarSearchStyle);
                if (!string.Equals(nextSearch, _packageSearch, StringComparison.Ordinal))
                {
                    _packageSearch = nextSearch;
                    SavePrefs();
                    Repaint();
                }

                if (UtilityWindowTheme.StyledButton(new GUIContent("Clear", "Clear package search."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(54f), GUILayout.Height(22f)) && !string.IsNullOrWhiteSpace(_packageSearch))
                {
                    _packageSearch = string.Empty;
                    SavePrefs();
                    Repaint();
                }
            }
        }

        private void DrawPackageDeveloperPreviewControls()
        {
            if (!IsDeveloperModeEnabled())
                return;

            EditorGUILayout.Space(2f);
            _showPackageDeveloperPreview = EditorGUILayout.Foldout(_showPackageDeveloperPreview, "Developer Preview", true);
            if (!_showPackageDeveloperPreview)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 5, 2)))
            {
                EditorGUILayout.LabelField("Visual preview only. This does not change package ownership, install state, imports, or catalog records.", UtilityWindowTheme.MutedMiniLabelStyle);
                string[] labels =
                {
                    "Actual State",
                    "Preview Installed",
                    "Preview Owned - Not Installed",
                    "Preview Available / Not Owned",
                    "Preview Missing Required Package",
                    "Preview Disabled",
                    "Preview Coming Soon / Unavailable"
                };

                int index = Mathf.Clamp((int)_packageCardPreviewMode, 0, labels.Length - 1);
                int next = EditorGUILayout.Popup(new GUIContent("Package Card State", "Preview how discovery cards look in different package states."), index, labels);
                PackageCardPreviewMode nextMode = (PackageCardPreviewMode)Mathf.Clamp(next, 0, labels.Length - 1);
                if (nextMode != _packageCardPreviewMode)
                {
                    _packageCardPreviewMode = nextMode;
                    SavePrefs();
                    Repaint();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (PungentUtilityPackageSimulation.Active)
                        UtilityWindowTheme.CountPill("Per-package simulation active", UtilityWindowTheme.Amber, 178f);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!PungentUtilityPackageSimulation.Active))
                    {
                        if (UtilityWindowTheme.StyledButton(new GUIContent("Clear All Simulation", "Clear all per-package simulated visual states."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(142f), GUILayout.Height(22f)))
                            PungentUtilityPackageSimulation.ClearAll();
                    }
                }
            }
        }

        private void DrawPackageDeveloperPreviewBanner()
        {
            if (!IsPackageCardSimulationActive())
                return;

            EditorGUILayout.HelpBox("Developer preview: package card states are simulated. Real package data is unchanged.", MessageType.Info);
        }

        private bool IsPackageCardSimulationActive()
        {
            return IsDeveloperModeEnabled() && (_packageCardPreviewMode != PackageCardPreviewMode.ActualState || PungentUtilityPackageSimulation.Active);
        }

        private void DrawPackageDiscoverySections(List<PungentUtilityPackageRecord> records)
        {
            records = records ?? new List<PungentUtilityPackageRecord>();
            List<PungentUtilityPackageRecord> available = GetPackagesByVisualState(records, PackageCardVisualState.Available);
            List<PungentUtilityPackageRecord> ownedNotInstalled = GetPackagesByVisualState(records, PackageCardVisualState.OwnedNotInstalled);
            List<PungentUtilityPackageRecord> installed = GetPackagesByVisualState(records, PackageCardVisualState.Installed);
            List<PungentUtilityPackageRecord> unavailable = GetPackagesByVisualState(records,
                PackageCardVisualState.MissingRequiredPackage,
                PackageCardVisualState.Disabled,
                PackageCardVisualState.ComingSoonUnavailable);

            bool simulation = IsPackageCardSimulationActive();
            DrawPackageDiscoverySection("Recommended Packages", available, true, "No unowned packages are configured. Add package records in Developer Mode or update the package catalog.", !simulation || available.Count > 0);
            DrawPackageDiscoverySection("Owned, Not Installed", ownedNotInstalled, false, "No owned packages are waiting to be installed.", !simulation || ownedNotInstalled.Count > 0);
            DrawPackageDiscoverySection("Installed", installed, false, "No installed package records were found.", !simulation || installed.Count > 0);
            DrawPackageDiscoverySection("Coming Soon / Unavailable", unavailable, false, "No unavailable packages are configured.", unavailable.Count > 0);
        }

        private List<PungentUtilityPackageRecord> GetPackagesByVisualState(IEnumerable<PungentUtilityPackageRecord> records, params PackageCardVisualState[] states)
        {
            if (records == null || states == null || states.Length == 0)
                return new List<PungentUtilityPackageRecord>();

            HashSet<PackageCardVisualState> acceptedStates = new HashSet<PackageCardVisualState>(states);
            return records
                .Where(record => record != null && acceptedStates.Contains(BuildPackageCardView(record).VisualState))
                .OrderBy(record => PungentUtilityPackageCatalog.PackageTierSortKey(record.tier))
                .ThenBy(record => record.displayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void DrawPackageDiscoverySection(string title, List<PungentUtilityPackageRecord> packages, bool prominent, string emptyMessage, bool showWhenEmpty = true)
        {
            packages = packages ?? new List<PungentUtilityPackageRecord>();
            if (packages.Count == 0 && !showWhenEmpty)
                return;

            EditorGUILayout.Space(5f);
            UtilityWindowTheme.SectionTitle(title, prominent ? UtilityWindowTheme.Purple : UtilityWindowTheme.Neutral, packages.Count + " packages");

            if (packages.Count == 0)
            {
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                return;
            }

            for (int i = 0; i < packages.Count; i++)
                DrawPackageDiscoveryCard(packages[i], prominent);
        }

        private void DrawPackageDiscoveryCard(PungentUtilityPackageRecord record, bool prominent)
        {
            PackageCardView view = BuildPackageCardView(record);
            List<string> utilityIds = GetPackageIncludedUtilityIds(record);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(view.Tint, prominent ? 0.16f : 0.10f, 0.045f, 8, 5)))
            {
                DrawPackageDiscoveryHeader(record, view);
                DrawPackagePitch(record);
                DrawPackageBestFor(record);
                DrawPackageCapabilityPreview(record);
                DrawPackagePromotion(record);
                DrawPackageStateExplanation(record, view);
                DrawPackageInlineDetails(record, utilityIds, view);
                DrawPackageUtilitySummary(record, utilityIds, view);
                DrawPackageDiscoveryActions(record, view);
            }
        }

        private PackageCardView BuildPackageCardView(PungentUtilityPackageRecord record)
        {
            PungentUtilityPackageAvailability actual = PungentUtilityPackageCatalog.ResolveAvailability(record);
            PackageCardVisualState state = ToVisualState(actual);
            bool simulated = false;

            if (IsDeveloperModeEnabled())
            {
                PungentUtilityPackageSimulationState packageSimulation = PungentUtilityPackageSimulation.GetState(record.packageId);
                if (packageSimulation != PungentUtilityPackageSimulationState.Actual)
                {
                    state = ToVisualState(packageSimulation);
                    simulated = true;
                }
                else
                {
                    switch (_packageCardPreviewMode)
                    {
                        case PackageCardPreviewMode.PreviewInstalled:
                            state = PackageCardVisualState.Installed;
                            simulated = true;
                            break;
                        case PackageCardPreviewMode.PreviewOwnedNotInstalled:
                            state = PackageCardVisualState.OwnedNotInstalled;
                            simulated = true;
                            break;
                        case PackageCardPreviewMode.PreviewAvailableNotOwned:
                            state = PackageCardVisualState.Available;
                            simulated = true;
                            break;
                        case PackageCardPreviewMode.PreviewMissingRequiredPackage:
                            state = PackageCardVisualState.MissingRequiredPackage;
                            simulated = true;
                            break;
                        case PackageCardPreviewMode.PreviewDisabled:
                            state = PackageCardVisualState.Disabled;
                            simulated = true;
                            break;
                        case PackageCardPreviewMode.PreviewComingSoonUnavailable:
                            state = PackageCardVisualState.ComingSoonUnavailable;
                            simulated = true;
                            break;
                    }
                }
            }

            return new PackageCardView
            {
                ActualAvailability = actual,
                VisualState = state,
                StateLabel = GetPackageVisualStateLabel(state),
                StateDescription = GetPackageVisualStateDescription(state),
                Tint = GetPackageDiscoveryTint(state),
                IsSimulated = simulated,
                AllowsRealActions = !simulated
            };
        }

        private static PackageCardVisualState ToVisualState(PungentUtilityPackageSimulationState state)
        {
            switch (state)
            {
                case PungentUtilityPackageSimulationState.OwnedNotInstalled:
                    return PackageCardVisualState.OwnedNotInstalled;
                case PungentUtilityPackageSimulationState.AvailableNotOwned:
                    return PackageCardVisualState.Available;
                case PungentUtilityPackageSimulationState.MissingRequiredPackage:
                    return PackageCardVisualState.MissingRequiredPackage;
                case PungentUtilityPackageSimulationState.Disabled:
                    return PackageCardVisualState.Disabled;
                case PungentUtilityPackageSimulationState.ComingSoonUnavailable:
                    return PackageCardVisualState.ComingSoonUnavailable;
                case PungentUtilityPackageSimulationState.Installed:
                default:
                    return PackageCardVisualState.Installed;
            }
        }

        private static PackageCardVisualState ToVisualState(PungentUtilityPackageAvailability availability)
        {
            switch (availability)
            {
                case PungentUtilityPackageAvailability.Unowned:
                    return PackageCardVisualState.Available;
                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    return PackageCardVisualState.OwnedNotInstalled;
                default:
                    return PackageCardVisualState.Installed;
            }
        }

        private static string GetPackageVisualStateLabel(PackageCardVisualState state)
        {
            switch (state)
            {
                case PackageCardVisualState.OwnedNotInstalled:
                    return "Owned - Not Installed";
                case PackageCardVisualState.Available:
                    return "Available";
                case PackageCardVisualState.MissingRequiredPackage:
                    return "Missing Required Package";
                case PackageCardVisualState.Disabled:
                    return "Disabled";
                case PackageCardVisualState.ComingSoonUnavailable:
                    return "Coming Soon";
                default:
                    return "Installed";
            }
        }

        private static string GetPackageVisualStateDescription(PackageCardVisualState state)
        {
            switch (state)
            {
                case PackageCardVisualState.OwnedNotInstalled:
                    return "Owned package. Import or install it before opening these utilities in this project.";
                case PackageCardVisualState.Available:
                    return "Optional package. Open its configured store or info route to learn more.";
                case PackageCardVisualState.MissingRequiredPackage:
                    return "A required package is missing, so these utilities cannot be used yet.";
                case PackageCardVisualState.Disabled:
                    return "This package is currently disabled for this project or preview state.";
                case PackageCardVisualState.ComingSoonUnavailable:
                    return "This package is not available yet.";
                default:
                    return "Installed and ready. Use Browse to open the included utilities.";
            }
        }

        private static Color GetPackageDiscoveryTint(PackageCardVisualState state)
        {
            switch (state)
            {
                case PackageCardVisualState.Available:
                    return UtilityWindowTheme.Purple;
                case PackageCardVisualState.OwnedNotInstalled:
                case PackageCardVisualState.MissingRequiredPackage:
                    return UtilityWindowTheme.Amber;
                case PackageCardVisualState.Disabled:
                case PackageCardVisualState.ComingSoonUnavailable:
                    return UtilityWindowTheme.Neutral;
                default:
                    return UtilityWindowTheme.Green;
            }
        }

        private void DrawPackageDiscoveryHeader(PungentUtilityPackageRecord record, PackageCardView view)
        {
            bool stack = position.width < 700f;
            if (stack)
            {
                EditorGUILayout.LabelField(new GUIContent(record.displayName, record.packageId), EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(view.StateLabel, view.Tint, Mathf.Clamp(view.StateLabel.Length * 7f + 26f, 96f, 186f));
                    DrawMetadataPill(record.tier.ToString(), UtilityWindowTheme.Neutral);
                    GUILayout.FlexibleSpace();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(record.displayName, record.packageId), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(view.StateLabel, view.Tint, Mathf.Clamp(view.StateLabel.Length * 7f + 26f, 96f, 186f));
                DrawMetadataPill(record.tier.ToString(), UtilityWindowTheme.Neutral);
            }
        }

        private void DrawPackagePitch(PungentUtilityPackageRecord record)
        {
            string pitch = string.IsNullOrWhiteSpace(record.description)
                ? "Discover additional PungentFunk utility workflows for the Unity Editor."
                : record.description.Trim();
            EditorGUILayout.LabelField(pitch, UtilityWindowTheme.BodyStyle);
        }

        private void DrawPackageBestFor(PungentUtilityPackageRecord record)
        {
            string bestFor = BuildPackageBestForLine(record);
            EditorGUILayout.LabelField("Best for: " + bestFor, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static string BuildPackageBestForLine(PungentUtilityPackageRecord record)
        {
            if (record != null && record.includedCapabilities != null && record.includedCapabilities.Length > 0)
                return string.Join(", ", record.includedCapabilities.Take(3).ToArray());

            return "expanding editor workflows with focused utility tools";
        }

        private void DrawPackageCapabilityPreview(PungentUtilityPackageRecord record)
        {
            if (record == null || record.includedCapabilities == null || record.includedCapabilities.Length == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                int count = Mathf.Min(position.width < 760f ? 2 : 5, record.includedCapabilities.Length);
                for (int i = 0; i < count; i++)
                    DrawMetadataPill(record.includedCapabilities[i], UtilityWindowTheme.Teal);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPackagePromotion(PungentUtilityPackageRecord record)
        {
            if (record == null || !record.showPromotionalPricing)
                return;

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(record.promotionalLabel))
                parts.Add(record.promotionalLabel.Trim());
            if (record.discountPercent > 0)
                parts.Add(record.discountPercent + "% off");
            if (!string.IsNullOrWhiteSpace(record.regularPriceText) && !string.IsNullOrWhiteSpace(record.salePriceText))
                parts.Add(record.regularPriceText.Trim() + " -> " + record.salePriceText.Trim());
            else if (!string.IsNullOrWhiteSpace(record.salePriceText))
                parts.Add(record.salePriceText.Trim());
            if (!string.IsNullOrWhiteSpace(record.saleEndsIsoUtc))
                parts.Add("Ends " + record.saleEndsIsoUtc.Trim());

            if (parts.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(string.Join("  ", parts.ToArray()), UtilityWindowTheme.Amber, Mathf.Clamp(parts.Sum(part => part.Length) * 7f + 38f, 120f, 360f));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPackageStateExplanation(PungentUtilityPackageRecord record, PackageCardView view)
        {
            if (view == null)
                return;

            EditorGUILayout.LabelField(view.StateDescription, UtilityWindowTheme.MutedMiniLabelStyle);

            if (view.VisualState == PackageCardVisualState.OwnedNotInstalled && record != null && string.IsNullOrWhiteSpace(record.importPackagePath) && string.IsNullOrWhiteSpace(record.packageManagerId))
                EditorGUILayout.LabelField("Install route not configured.", UtilityWindowTheme.MutedMiniLabelStyle);
            else if (view.VisualState == PackageCardVisualState.Available && record != null && string.IsNullOrWhiteSpace(record.assetStoreUrl))
                EditorGUILayout.LabelField("Asset Store link not configured.", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawPackageDiscoveryActions(PungentUtilityPackageRecord record, PackageCardView view)
        {
            if (position.width < 760f)
            {
                using (new EditorGUILayout.VerticalScope())
                    DrawPackageDiscoveryActionButtons(record, view);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                DrawPackageDiscoveryActionButtons(record, view);
            }
        }

        private void DrawPackageDiscoveryActionButtons(PungentUtilityPackageRecord record, PackageCardView view)
        {
            if (view != null && !view.AllowsRealActions)
            {
                DrawSimulatedPackagePrimaryAction(view);
                DrawPackageInfoButton(record, null);
                return;
            }

            switch (view == null ? PackageCardVisualState.Installed : view.VisualState)
            {
                case PackageCardVisualState.Available:
                    DrawPackageAssetStoreButton(record, "Open in Asset Store", 148f, true, true);
                    DrawPackageManagerButton(record, false);
                    DrawPackageInfoButton(record, null);
                    break;

                case PackageCardVisualState.OwnedNotInstalled:
                    DrawPackageImportButton(record, true);
                    DrawPackageManagerButton(record, true);
                    DrawPackageInfoButton(record, null);
                    break;

                case PackageCardVisualState.MissingRequiredPackage:
                    DrawDisabledPackageAction("Missing Required Package", "Install the required package before this package can be used.", 172f);
                    DrawPackageInfoButton(record, null);
                    break;

                case PackageCardVisualState.Disabled:
                    DrawDisabledPackageAction("Disabled", "This package is disabled for this project or preview state.", 82f);
                    DrawPackageInfoButton(record, null);
                    break;

                case PackageCardVisualState.ComingSoonUnavailable:
                    DrawDisabledPackageAction("Not Available Yet", "This package is not available yet.", 132f);
                    DrawPackageInfoButton(record, null);
                    break;

                default:
                    using (UtilityWindowTheme.Background(UtilityWindowTheme.Green))
                    {
                        if (UtilityWindowTheme.StyledButton(new GUIContent("View in Browse", "Show utilities from this installed package in Browse."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(116f), GUILayout.Height(24f)))
                            BrowsePackage(record.packageId);
                    }
                    DrawPackageAssetStoreButton(record, "Asset Store", 86f, false, false);
                    DrawPackageInfoButton(record, null);
                    break;
            }
        }

        private void DrawSimulatedPackagePrimaryAction(PackageCardView view)
        {
            string label;
            float width;
            switch (view == null ? PackageCardVisualState.Installed : view.VisualState)
            {
                case PackageCardVisualState.Installed:
                    label = "View in Browse";
                    width = 116f;
                    break;
                case PackageCardVisualState.OwnedNotInstalled:
                    label = "Import Package";
                    width = 112f;
                    break;
                case PackageCardVisualState.Available:
                    label = "Open in Asset Store";
                    width = 148f;
                    break;
                case PackageCardVisualState.MissingRequiredPackage:
                    label = "Missing Required Package";
                    width = 172f;
                    break;
                case PackageCardVisualState.Disabled:
                    label = "Disabled";
                    width = 82f;
                    break;
                default:
                    label = "Not Available Yet";
                    width = 132f;
                    break;
            }

            DrawDisabledPackageAction(label, "Disabled during Developer preview. Real package data is unchanged.", width);
        }

        private void DrawDisabledPackageAction(string label, string tooltip, float width)
        {
            using (new EditorGUI.DisabledScope(true))
                UtilityWindowTheme.StyledButton(new GUIContent(label, tooltip), UtilityWindowTheme.PungentButtonRole.Disabled, GUILayout.Width(width), GUILayout.Height(24f));
        }

        private void DrawPackageAssetStoreButton(PungentUtilityPackageRecord record, string label, float width, bool showWhenMissing, bool primary)
        {
            bool configured = record != null && PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(record.assetStoreUrl);
            if (!configured && !showWhenMissing)
                return;

            using (new EditorGUI.DisabledScope(!configured))
            {
                string buttonLabel = configured ? label : "Asset Store link not configured";
                float buttonWidth = configured ? width : 184f;
                bool clicked;
                if (primary && configured)
                {
                    using (UtilityWindowTheme.Background(UtilityWindowTheme.Purple))
                        clicked = UtilityWindowTheme.StyledButton(new GUIContent(buttonLabel, record.assetStoreUrl), UtilityWindowTheme.PungentButtonRole.Primary, GUILayout.Width(buttonWidth), GUILayout.Height(24f));
                }
                else
                {
                    clicked = UtilityWindowTheme.StyledButton(new GUIContent(buttonLabel, configured ? record.assetStoreUrl : "Asset Store link not configured."), configured ? UtilityWindowTheme.PungentButtonRole.Link : UtilityWindowTheme.PungentButtonRole.Disabled, GUILayout.Width(buttonWidth), GUILayout.Height(24f));
                }
                if (clicked)
                {
                    if (PungentUtilityPackageCatalog.TryOpenAssetStore(record, out string message))
                        _status = message;
                    else
                        _status = message;
                }
            }
        }

        private void DrawPackageManagerButton(PungentUtilityPackageRecord record, bool showWhenMissing)
        {
            bool configured = PungentUtilityPackageCatalog.HasPackageManagerRoute(record);
            if (!configured && !showWhenMissing)
                return;

            using (new EditorGUI.DisabledScope(!configured))
            {
                string label = configured ? "Open Package Manager" : "Package Manager route not configured";
                if (UtilityWindowTheme.StyledButton(new GUIContent(label, configured ? record.packageManagerId : "Package Manager route not configured."), configured ? UtilityWindowTheme.PungentButtonRole.Secondary : UtilityWindowTheme.PungentButtonRole.Disabled, GUILayout.Width(configured ? 148f : 214f), GUILayout.Height(24f)))
                    OpenPackageInstallFlow(record);
            }
        }

        private void DrawPackageImportButton(PungentUtilityPackageRecord record, bool showWhenMissing)
        {
            if ((record == null || string.IsNullOrWhiteSpace(record.importPackagePath)) && !showWhenMissing)
                return;

            bool configured = PungentUtilityPackageCatalog.HasValidImportPackagePath(record);
            using (new EditorGUI.DisabledScope(!configured))
            {
                string label = configured ? "Import Package" : "Import route not configured";
                bool clicked;
                using (UtilityWindowTheme.Background(configured ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral))
                    clicked = UtilityWindowTheme.StyledButton(new GUIContent(label, configured ? record.importPackagePath : "Import route not configured."), configured ? UtilityWindowTheme.PungentButtonRole.Warning : UtilityWindowTheme.PungentButtonRole.Disabled, GUILayout.Width(configured ? 112f : 166f), GUILayout.Height(24f));
                if (clicked)
                {
                    if (PungentUtilityPackageCatalog.OpenImportPackage(record, out string message))
                        _status = message;
                    else
                    {
                        _status = message;
                        OpenPackageDetailsInline(record, null, message);
                    }
                }
            }
        }

        private void DrawPackageInfoButton(PungentUtilityPackageRecord record, PungentUtilityDescriptor utility)
        {
            if (UtilityWindowTheme.StyledButton(new GUIContent("Info", "Expand concise package details on the package card."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(54f), GUILayout.Height(24f)))
                TogglePackageDetails(record, utility, null);
        }

        private void DrawPackageInlineDetails(PungentUtilityPackageRecord record, List<string> utilityIds, PackageCardView view)
        {
            if (record == null)
                return;

            string packageId = PungentUtilityPackageCatalog.NormalizePackageId(record.packageId);
            if (!_expandedPackageDetails.Contains(packageId))
                return;

            bool hasContext = _packageDetailRecord != null &&
                              string.Equals(_packageDetailRecord.packageId, packageId, StringComparison.OrdinalIgnoreCase);
            PungentUtilityDescriptor contextUtility = hasContext ? _packageDetailUtility : null;

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Package Details", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Close", "Collapse these package details."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(58f), GUILayout.Height(22f)))
                        CollapsePackageDetails(packageId);
                }

                EditorGUILayout.LabelField((view == null ? GetPackageVisualStateLabel(ToVisualState(PungentUtilityPackageCatalog.ResolveAvailability(record))) : view.StateLabel) + " package.", UtilityWindowTheme.MutedMiniLabelStyle);

                if (contextUtility != null)
                    EditorGUILayout.LabelField("Opened from: " + contextUtility.DisplayName, UtilityWindowTheme.MutedMiniLabelStyle);

                if (hasContext && !string.IsNullOrWhiteSpace(_packageDetailMessage))
                    EditorGUILayout.HelpBox(_packageDetailMessage, MessageType.Info);

                if (!string.IsNullOrWhiteSpace(record.installHint))
                    EditorGUILayout.LabelField("Install hint: " + record.installHint, UtilityWindowTheme.MutedMiniLabelStyle);

                if (utilityIds != null && utilityIds.Count > 0)
                    EditorGUILayout.LabelField("Includes: " + BuildIncludedUtilitiesSummary(record, PungentUtilityPackageCatalog.RegisteredUtilitiesForPackage(record.packageId)), UtilityWindowTheme.MutedMiniLabelStyle);

                if (contextUtility != null)
                {
                    string required = contextUtility.RequiredPackageIds == null || contextUtility.RequiredPackageIds.Length == 0
                        ? "Required packages: none"
                        : "Required packages: " + string.Join(", ", contextUtility.RequiredPackageIds);
                    string optional = contextUtility.OptionalPackageIds == null || contextUtility.OptionalPackageIds.Length == 0
                        ? "Optional packages: none"
                        : "Optional packages: " + string.Join(", ", contextUtility.OptionalPackageIds);
                    EditorGUILayout.LabelField(required, UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField(optional, UtilityWindowTheme.MutedMiniLabelStyle);
                }

                if (!PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(record.assetStoreUrl))
                    EditorGUILayout.LabelField("Asset Store link not configured.", UtilityWindowTheme.MutedMiniLabelStyle);

                if (IsDeveloperModeEnabled())
                {
                    EditorGUILayout.SelectableLabel(record.packageId, EditorStyles.textField, GUILayout.Height(18f));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.StyledButton(new GUIContent("Copy Package ID", "Copy the package ID to the clipboard."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(112f), GUILayout.Height(22f)))
                        {
                            EditorGUIUtility.systemCopyBuffer = record.packageId;
                            _status = "Copied package ID.";
                        }

                        if (UtilityWindowTheme.StyledButton(new GUIContent("Open Help", "Open package or utility help."), UtilityWindowTheme.PungentButtonRole.Link, GUILayout.Width(82f), GUILayout.Height(22f)))
                            OpenHelpForPackageDetail();

                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private void DrawPackageUtilitySummary(PungentUtilityPackageRecord record, List<string> utilityIds, PackageCardView view)
        {
            if (record == null)
                return;

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Included Utilities", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (utilityIds.Count > 4)
                {
                    bool expanded = _expandedPackageUtilities.Contains(record.packageId);
                    if (UtilityWindowTheme.StyledButton(new GUIContent(expanded ? "Show Less" : "View All", "Toggle the included utility list."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(82f), GUILayout.Height(22f)))
                    {
                        if (expanded)
                            _expandedPackageUtilities.Remove(record.packageId);
                        else
                            _expandedPackageUtilities.Add(record.packageId);
                    }
                }
            }

            if (utilityIds.Count == 0)
            {
                EditorGUILayout.LabelField("No included utilities are configured yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            bool showAll = _expandedPackageUtilities.Contains(record.packageId);
            int visibleCount = showAll ? utilityIds.Count : Mathf.Min(4, utilityIds.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                string utilityId = utilityIds[i];
                DrawPackageUtilityRow(record, utilityId, PungentUtilityRegistry.Find(utilityId), view);
            }

            if (!showAll && utilityIds.Count > visibleCount)
                EditorGUILayout.LabelField((utilityIds.Count - visibleCount) + " more utilities in this package.", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawPackageUtilityRow(PungentUtilityPackageRecord record, string utilityId, PungentUtilityDescriptor utility, PackageCardView view)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(160f)))
                {
                    EditorGUILayout.LabelField(utility != null ? utility.DisplayName : utilityId, EditorStyles.boldLabel);
                    string description = utility != null ? utility.Description : "Listed in package metadata, but not currently registered.";
                    if (!string.IsNullOrWhiteSpace(description))
                        EditorGUILayout.LabelField(description, UtilityWindowTheme.MutedMiniLabelStyle);
                }

                GUILayout.FlexibleSpace();
                DrawPackageUtilityRowAction(record, utilityId, utility, view);
            }
        }

        private void DrawPackageUtilityRowAction(PungentUtilityPackageRecord record, string utilityId, PungentUtilityDescriptor utility, PackageCardView view)
        {
            if (view != null && !view.AllowsRealActions)
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Info", "Actions are disabled during Developer preview. Real package data is unchanged."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(58f), GUILayout.Height(22f)))
                {
                    string message = utility == null ? "Utility " + utilityId + " is listed in package metadata, but it is not currently registered." : null;
                    OpenPackageDetailsInline(record, utility, message);
                }
                return;
            }

            PungentUtilityPackageAvailability availability = PungentUtilityPackageCatalog.ResolveAvailability(record);
            if (utility != null && !PungentUtilityAccessPolicy.CanAccess(utility))
            {
                using (new EditorGUI.DisabledScope(true))
                    UtilityWindowTheme.StyledButton(new GUIContent(PungentUtilityAccessPolicy.BuildBlockedActionLabel(utility), utility.DisabledReason), UtilityWindowTheme.PungentButtonRole.Disabled, GUILayout.Width(124f), GUILayout.Height(22f));
                return;
            }

            if (utility != null && availability == PungentUtilityPackageAvailability.OwnedInstalled)
            {
                using (new EditorGUI.DisabledScope(!IsPrimaryActionEnabled(utility)))
                {
                    if (UtilityWindowTheme.StyledButton(new GUIContent(utility.IsAction ? "Run" : "Open", BuildUtilityTooltip(utility)), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(58f), GUILayout.Height(22f)))
                        ActivateUtilityPrimaryAction(utility);
                }
                return;
            }

            if (utility != null && availability == PungentUtilityPackageAvailability.OwnedNotInstalled && PungentUtilityPackageCatalog.HasValidImportPackagePath(utility.ImportPackagePath))
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Import", utility.ImportPackagePath), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(58f), GUILayout.Height(22f)))
                {
                    if (PungentUtilityPackageCatalog.OpenImportPackage(utility.ImportPackagePath, utility.DisplayName, out string message))
                        _status = message;
                    else
                    {
                        _status = message;
                        OpenPackageDetailsInline(record, utility, message);
                    }
                }
                return;
            }

            if (UtilityWindowTheme.StyledButton(new GUIContent("Info", "Show utility/package details."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(58f), GUILayout.Height(22f)))
            {
                string message = utility == null ? "Utility " + utilityId + " is listed in package metadata, but it is not currently registered." : null;
                OpenPackageDetailsInline(record, utility, message);
            }
        }

        private List<string> GetPackageIncludedUtilityIds(PungentUtilityPackageRecord record)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (record != null && record.includedUtilityIds != null)
            {
                for (int i = 0; i < record.includedUtilityIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(record.includedUtilityIds[i]))
                        ids.Add(record.includedUtilityIds[i].Trim());
                }
            }

            if (record != null)
            {
                List<PungentUtilityDescriptor> registered = PungentUtilityPackageCatalog.RegisteredUtilitiesForPackage(record.packageId);
                for (int i = 0; i < registered.Count; i++)
                {
                    if (registered[i] != null && !string.IsNullOrWhiteSpace(registered[i].Id))
                        ids.Add(registered[i].Id);
                }
            }

            return ids
                .OrderBy(id =>
                {
                    PungentUtilityDescriptor utility = PungentUtilityRegistry.Find(id);
                    return utility == null ? id : utility.DisplayName;
                }, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void DrawPackageDeveloperControls(PungentUtilityPackageRecord record, PackageCardView view)
        {
            if (!IsDeveloperModeEnabled() || record == null)
                return;

            bool expanded = _expandedPackageEditors.Contains(record.packageId);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetadataPill("Actual: " + PungentUtilityPackageCatalog.ResolveAvailability(record), UtilityWindowTheme.Neutral);
                if (view != null && view.IsSimulated)
                    DrawMetadataPill("Preview: " + view.StateLabel, view.Tint);
                PungentUtilityPackageSimulationState simulationState = PungentUtilityPackageSimulation.GetState(record.packageId);
                if (simulationState != PungentUtilityPackageSimulationState.Actual)
                    DrawMetadataPill("Sim: " + simulationState, UtilityWindowTheme.Amber);

                if (UtilityWindowTheme.StyledButton(new GUIContent(expanded ? "Hide Package Metadata" : "Edit Package Metadata", "Edit project-local catalog metadata for this package."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(158f), GUILayout.Height(22f)))
                {
                    if (!expanded)
                        _expandedPackageEditors.Add(record.packageId);
                    else
                        _expandedPackageEditors.Remove(record.packageId);
                }

                if (PungentUtilityPackageCatalogSettings.instance.HasOverride(record.packageId))
                    DrawMetadataPill("Catalog Override", UtilityWindowTheme.Amber);

                if (UtilityWindowTheme.StyledButton(new GUIContent("Copy ID", record.packageId), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(62f), GUILayout.Height(22f)))
                {
                    EditorGUIUtility.systemCopyBuffer = record.packageId;
                    _status = "Copied package ID.";
                }

                GUILayout.FlexibleSpace();
            }

            if (!_expandedPackageEditors.Contains(record.packageId))
                return;

            PackageEditDraft draft = GetPackageEditDraft(record);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 5, 2)))
            {
                EditorGUILayout.SelectableLabel(record.packageId, EditorStyles.textField, GUILayout.Height(18f));
                DrawPackageSimulationEditor(record);
                draft.displayName = EditorGUILayout.TextField(new GUIContent("Display Name", "Package display name."), draft.displayName);
                draft.tier = (PungentUtilityPackageTier)EditorGUILayout.EnumPopup(new GUIContent("Tier", "Package tier."), draft.tier);
                draft.developerOverrideAvailability = EditorGUILayout.ToggleLeft(new GUIContent("Override availability", "Manually set package ownership/install state."), draft.developerOverrideAvailability);
                using (new EditorGUI.DisabledScope(!draft.developerOverrideAvailability))
                    draft.availability = (PungentUtilityPackageAvailability)EditorGUILayout.EnumPopup(new GUIContent("Availability", "Manual availability state."), draft.availability);
                draft.packageManagerId = EditorGUILayout.TextField(new GUIContent("Package Manager ID", "UPM package ID or scoped package identifier."), draft.packageManagerId);
                draft.assetStoreUrl = EditorGUILayout.TextField(new GUIContent("Asset Store URL", "Optional absolute URL."), draft.assetStoreUrl);
                draft.importPackagePath = EditorGUILayout.TextField(new GUIContent("Import Package Path", "Optional .unitypackage path."), draft.importPackagePath);
                draft.installHint = EditorGUILayout.TextField(new GUIContent("Install Hint", "Fallback installation instructions."), draft.installHint);
                EditorGUILayout.LabelField("Description", UtilityWindowTheme.MutedMiniLabelStyle);
                draft.description = EditorGUILayout.TextArea(draft.description ?? string.Empty, GUILayout.MinHeight(36f));
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Promotional Display", EditorStyles.boldLabel);
                draft.showPromotionalPricing = EditorGUILayout.ToggleLeft(new GUIContent("Show promotional pricing", "Display configured sale/discount metadata on Other Packages cards."), draft.showPromotionalPricing);
                using (new EditorGUI.DisabledScope(!draft.showPromotionalPricing))
                {
                    draft.promotionalLabel = EditorGUILayout.TextField(new GUIContent("Promo Label", "Optional label such as Launch discount."), draft.promotionalLabel ?? string.Empty);
                    draft.currencyCode = EditorGUILayout.TextField(new GUIContent("Currency Code", "Optional ISO currency code."), draft.currencyCode ?? string.Empty);
                    draft.regularPriceText = EditorGUILayout.TextField(new GUIContent("Regular Price Text", "Optional literal price text."), draft.regularPriceText ?? string.Empty);
                    draft.salePriceText = EditorGUILayout.TextField(new GUIContent("Sale Price Text", "Optional literal sale price text."), draft.salePriceText ?? string.Empty);
                    draft.discountPercent = EditorGUILayout.IntSlider(new GUIContent("Discount Percent", "Optional discount percent. No pricing UI appears unless configured."), draft.discountPercent, 0, 100);
                    draft.saleEndsIsoUtc = EditorGUILayout.TextField(new GUIContent("Sale Ends ISO UTC", "Optional ISO date/time text."), draft.saleEndsIsoUtc ?? string.Empty);
                }
                EditorGUILayout.LabelField("Included Utility IDs", UtilityWindowTheme.MutedMiniLabelStyle);
                draft.includedUtilityIdsText = EditorGUILayout.TextArea(draft.includedUtilityIdsText ?? string.Empty, GUILayout.MinHeight(34f));
                EditorGUILayout.LabelField("Included Capabilities", UtilityWindowTheme.MutedMiniLabelStyle);
                draft.includedCapabilitiesText = EditorGUILayout.TextArea(draft.includedCapabilitiesText ?? string.Empty, GUILayout.MinHeight(34f));
                DrawPackageDraftValidation(record, draft);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Save Package Override", UtilityWindowTheme.Green, GUILayout.Width(148f), GUILayout.Height(24f)))
                        SavePackageDraft(record, draft);

                    using (new EditorGUI.DisabledScope(!PungentUtilityPackageCatalogSettings.instance.HasOverride(record.packageId)))
                    {
                        if (UtilityWindowTheme.StyledButton(new GUIContent("Clear Override", "Remove project-local package catalog override."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(106f), GUILayout.Height(24f)))
                        {
                            PungentUtilityPackageCatalog.ClearRecordOverride(record.packageId);
                            _packageEditDrafts.Remove(record.packageId);
                            _status = "Cleared package catalog override.";
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawPackageSimulationEditor(PungentUtilityPackageRecord record)
        {
            if (record == null || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.06f, 0.03f, 4, 1)))
            {
                EditorGUILayout.LabelField("Package Simulation", EditorStyles.boldLabel);
                PungentUtilityPackageSimulationState current = PungentUtilityPackageSimulation.GetState(record.packageId);
                EditorGUI.BeginChangeCheck();
                PungentUtilityPackageSimulationState next = (PungentUtilityPackageSimulationState)EditorGUILayout.EnumPopup(new GUIContent("Visual State", "Preview package card/CTA state without changing install or ownership data."), current);
                if (EditorGUI.EndChangeCheck())
                {
                    PungentUtilityPackageSimulation.SetState(record.packageId, next);
                    _status = next == PungentUtilityPackageSimulationState.Actual ? "Cleared package simulation." : "Updated package simulation.";
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Reset Package", "Clear simulation for this package."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(108f), GUILayout.Height(22f)))
                        PungentUtilityPackageSimulation.Clear(record.packageId);
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Clear All Simulation", "Clear all package simulation states."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(132f), GUILayout.Height(22f)))
                        PungentUtilityPackageSimulation.ClearAll();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawPackageDraftValidation(PungentUtilityPackageRecord record, PackageEditDraft draft)
        {
            if (string.IsNullOrWhiteSpace(draft.displayName))
                EditorGUILayout.HelpBox("Package display name should not be blank.", MessageType.Warning);

            if (!string.IsNullOrWhiteSpace(draft.assetStoreUrl) && !PungentUtilityExternalLinksSettings.IsValidAbsoluteUrl(draft.assetStoreUrl))
                EditorGUILayout.HelpBox("Asset Store URL must be empty or an absolute http/https URL.", MessageType.Warning);

            if (!string.IsNullOrWhiteSpace(draft.importPackagePath) && !draft.importPackagePath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                EditorGUILayout.HelpBox("Import package path should point to a .unitypackage file.", MessageType.Warning);
        }

        private PackageEditDraft GetPackageEditDraft(PungentUtilityPackageRecord record)
        {
            if (record == null)
                return new PackageEditDraft();

            if (_packageEditDrafts.TryGetValue(record.packageId, out PackageEditDraft draft))
                return draft;

            draft = new PackageEditDraft
            {
                displayName = record.displayName,
                tier = record.tier,
                availability = PungentUtilityPackageCatalog.ResolveAvailability(record),
                developerOverrideAvailability = record.developerOverrideAvailability,
                assetStoreUrl = record.assetStoreUrl,
                packageManagerId = record.packageManagerId,
                importPackagePath = record.importPackagePath,
                description = record.description,
                includedUtilityIdsText = FormatLooseList(record.includedUtilityIds),
                includedCapabilitiesText = FormatLooseList(record.includedCapabilities),
                installHint = record.installHint,
                showPromotionalPricing = record.showPromotionalPricing,
                currencyCode = record.currencyCode,
                regularPriceText = record.regularPriceText,
                salePriceText = record.salePriceText,
                discountPercent = record.discountPercent,
                saleEndsIsoUtc = record.saleEndsIsoUtc,
                promotionalLabel = record.promotionalLabel
            };
            _packageEditDrafts[record.packageId] = draft;
            return draft;
        }

        private void SavePackageDraft(PungentUtilityPackageRecord source, PackageEditDraft draft)
        {
            if (source == null || draft == null)
                return;

            PungentUtilityPackageRecord record = PungentUtilityPackageCatalog.CreateEditableRecord(source.packageId);
            record.displayName = string.IsNullOrWhiteSpace(draft.displayName) ? source.packageId : draft.displayName.Trim();
            record.tier = draft.tier;
            record.availability = draft.availability;
            record.developerOverrideAvailability = draft.developerOverrideAvailability;
            record.assetStoreUrl = draft.assetStoreUrl ?? string.Empty;
            record.packageManagerId = draft.packageManagerId ?? string.Empty;
            record.importPackagePath = draft.importPackagePath ?? string.Empty;
            record.description = draft.description ?? string.Empty;
            record.includedUtilityIds = ParseLooseList(draft.includedUtilityIdsText);
            record.includedCapabilities = ParseLooseList(draft.includedCapabilitiesText);
            record.installHint = draft.installHint ?? string.Empty;
            record.showPromotionalPricing = draft.showPromotionalPricing;
            record.currencyCode = draft.currencyCode ?? string.Empty;
            record.regularPriceText = draft.regularPriceText ?? string.Empty;
            record.salePriceText = draft.salePriceText ?? string.Empty;
            record.discountPercent = draft.discountPercent;
            record.saleEndsIsoUtc = draft.saleEndsIsoUtc ?? string.Empty;
            record.promotionalLabel = draft.promotionalLabel ?? string.Empty;

            PungentUtilityPackageCatalog.SaveRecordOverride(record);
            _status = "Saved package catalog override.";
        }

        private void BrowsePackage(string packageId)
        {
            string normalizedPackageId = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            _selectedTab = BrowserTab.Browse;
            _focusedPackageId = normalizedPackageId;
            _search = string.Empty;
            _category = AllFilter;
            _type = AllFilter;
            _statusFilter = AllFilter;
            _packageAvailabilityFilter = PackageAvailabilityFilter.AllUtilities;
            MarkCacheDirty();
            SavePrefs();
        }

        private void OpenHelpTopicSafe(string utilityId, string sectionId, string topicId)
        {
            if (PungentUtilityHelpRegistry.Find(utilityId, sectionId, topicId) != null)
                PungentUtilityHelpRegistry.Open(utilityId, sectionId, topicId);
            else
                PungentUtilityHelpBrowserWindow.Open();
        }

        private void DrawPackageStateBadgeSummary()
        {
            List<PungentUtilityPackageRecord> records = PungentUtilityPackageCatalog.Records
                .Where(record => IsDeveloperModeEnabled() || record.lifecycle == PungentUtilityPackageLifecycle.Visible)
                .ToList();
            int installed = records.Count(record => PungentUtilityPackageCatalog.ResolveAvailability(record) == PungentUtilityPackageAvailability.OwnedInstalled);
            int pending = records.Count(record => PungentUtilityPackageCatalog.ResolveAvailability(record) == PungentUtilityPackageAvailability.OwnedNotInstalled);
            int unowned = records.Count(record => PungentUtilityPackageCatalog.ResolveAvailability(record) == PungentUtilityPackageAvailability.Unowned);
            UtilityWindowTheme.CountPill("Installed " + installed, UtilityWindowTheme.Green, 82f);
            UtilityWindowTheme.CountPill("Owned " + pending, UtilityWindowTheme.Amber, 74f);
            UtilityWindowTheme.CountPill("Available " + unowned, UtilityWindowTheme.Purple, 88f);
        }

        private bool PassesPackageAvailabilityFilter(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return false;

            return PassesPackageAvailabilityFilter(PungentUtilityPackageCatalog.ResolveAvailability(utility));
        }

        private bool PassesPackageAvailabilityFilter(PungentUtilityPackageRecord record)
        {
            if (record == null)
                return false;

            return PassesPackageAvailabilityFilter(PungentUtilityPackageCatalog.ResolveAvailability(record));
        }

        private bool PassesPackageAvailabilityFilter(PungentUtilityPackageAvailability availability)
        {
            switch (_packageAvailabilityFilter)
            {
                case PackageAvailabilityFilter.OwnedUtilities:
                    return availability == PungentUtilityPackageAvailability.OwnedInstalled ||
                           availability == PungentUtilityPackageAvailability.OwnedNotInstalled;
                case PackageAvailabilityFilter.InstalledUtilities:
                    return availability == PungentUtilityPackageAvailability.OwnedInstalled;
                default:
                    return true;
            }
        }

        private static string GetPackageAvailabilityFilterLabel(PackageAvailabilityFilter filter)
        {
            switch (filter)
            {
                case PackageAvailabilityFilter.OwnedUtilities:
                    return "Owned Utilities";
                case PackageAvailabilityFilter.InstalledUtilities:
                    return "Installed Utilities";
                default:
                    return "All Utilities";
            }
        }

        private static string[] GetPackageAvailabilityFilterLabels()
        {
            return new[]
            {
                "All Utilities",
                "Owned Utilities",
                "Installed Utilities"
            };
        }

        private static Color GetPackageAvailabilityFilterTint(PackageAvailabilityFilter filter)
        {
            switch (filter)
            {
                case PackageAvailabilityFilter.OwnedUtilities:
                    return UtilityWindowTheme.Cyan;
                case PackageAvailabilityFilter.InstalledUtilities:
                    return UtilityWindowTheme.Green;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static string FormatLooseList(IEnumerable<string> values)
        {
            if (values == null)
                return string.Empty;

            return string.Join("\n", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        private static string[] ParseLooseList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new string[0];

            return text
                .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void DrawPackageFocusBanner()
        {
            if (string.IsNullOrWhiteSpace(_focusedPackageId))
                return;

            string displayName = PungentUtilityPackageCatalog.GetDisplayName(_focusedPackageId);
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.05f, 5, 2)))
            {
                EditorGUILayout.LabelField("Showing utilities from " + displayName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (UtilityWindowTheme.StyledButton(new GUIContent("Clear package focus", "Return Browse to the normal utility list."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(138f), GUILayout.Height(22f)))
                    ClearPackageFocus();
            }
        }

        private void DrawOwnershipFilterBanner()
        {
            if (_packageAvailabilityFilter == PackageAvailabilityFilter.AllUtilities)
                return;

            string message = _packageAvailabilityFilter == PackageAvailabilityFilter.OwnedUtilities
                ? "Showing owned utilities"
                : "Showing installed utilities";

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.10f, 0.04f, 5, 2)))
            {
                EditorGUILayout.LabelField(message, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (UtilityWindowTheme.StyledButton(new GUIContent("Show All", "Return Browse to all visible utilities."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(72f), GUILayout.Height(22f)))
                {
                    _packageAvailabilityFilter = PackageAvailabilityFilter.AllUtilities;
                    MarkCacheDirty();
                    SavePrefs();
                }
            }
        }

        private void ClearPackageFocus()
        {
            _focusedPackageId = string.Empty;
            _search = string.Empty;
            MarkCacheDirty();
            SavePrefs();
            Repaint();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.16f, 0.07f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search", GUILayout.Width(48f));
                    GUI.SetNextControlName("PFU.Browser.Search");
                    string nextSearch = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (!string.Equals(nextSearch, _search, StringComparison.Ordinal))
                    {
                        _search = nextSearch;
                        MarkCacheDirty();
                        SavePrefs();
                    }

                    if (UtilityWindowTheme.StyledButton(new GUIContent("Clear", "Clear search text."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(54f), GUILayout.Height(22f)) && !string.IsNullOrEmpty(_search))
                    {
                        _search = string.Empty;
                        MarkCacheDirty();
                        SavePrefs();
                    }

                    UtilityWindowTheme.CountPill(_cachedQuery.Count + "/" + CountSourceItemsForHeader(), UtilityWindowTheme.Teal, 74f);
                    DrawDocsCoveragePill();
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw("utilities-browser", "overview", "overview", "Open help for the Utilities Browser.", "Utilities Browser header");
                }

                bool narrow = position.width < NarrowBrowseBreakpoint;
                using (new EditorGUILayout.HorizontalScope())
                {
                    PungentUtilityHelpButton.Draw("utilities-browser", "filters-search", "filters-search", "Open help for Utilities Browser filters and search.", "Utilities Browser filters");
                    GUILayout.Space(4f);
                    DrawToolbarOverlayToggle(BrowserToolbarOverlay.Filters, "Filters", UtilityWindowTheme.Blue, "Show secondary filter controls.");
                    DrawToolbarOverlayToggle(BrowserToolbarOverlay.Favourites, "Favourites", UtilityWindowTheme.Amber, "Show pinned utilities.");
                    DrawToolbarOverlayToggle(BrowserToolbarOverlay.History, "History", UtilityWindowTheme.Cyan, "Show recently opened utilities.");
                    DrawToolbarOverlayToggle(BrowserToolbarOverlay.Settings, "Settings", UtilityWindowTheme.Neutral, "Show browser display settings.");

                    if (narrow)
                    {
                        GUILayout.FlexibleSpace();
                    }
                    else
                    {
                        GUILayout.Space(8f);
                        DrawBrowseOwnershipToolbarPopup();
                        GUILayout.Space(6f);
                        DrawSortModePopup();
                        GUILayout.Space(6f);
                        GUILayout.FlexibleSpace();
                        DrawViewModeButton(BrowserViewMode.Compact, "Compact");
                        DrawViewModeButton(BrowserViewMode.List, "List");
                    }
                }

                DrawActiveBrowserFilterSummary();

                if (narrow)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawBrowseOwnershipToolbarPopup();
                        GUILayout.Space(6f);
                        DrawSortModePopup();
                        GUILayout.FlexibleSpace();
                        DrawViewModeButton(BrowserViewMode.Compact, "Compact");
                        DrawViewModeButton(BrowserViewMode.List, "List");
                    }
                }
            }
        }

        private void DrawActiveBrowserFilterSummary()
        {
            if (!HasActiveFilters())
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (position.width < NarrowBrowseBreakpoint)
                {
                    UtilityWindowTheme.CountPill(CountActiveBrowserFilters() + " filters active", UtilityWindowTheme.Blue, 122f);
                    GUILayout.FlexibleSpace();
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Reset", "Clear active browser filters and return to the recommended core utility view."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(64f), GUILayout.Height(22f)))
                        ResetBrowserFilters();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_search))
                    UtilityWindowTheme.CountPill("Search", UtilityWindowTheme.Blue, 68f);
                if (_category != AllFilter)
                    UtilityWindowTheme.CountPill(PungentUtilityCategories.GetDisplayName(_category), PungentUtilityCategories.GetTint(_category), Mathf.Clamp(PungentUtilityCategories.GetDisplayName(_category).Length * 7f + 22f, 72f, 150f));
                if (_type != AllFilter)
                    UtilityWindowTheme.CountPill(_type, UtilityWindowTheme.Teal, Mathf.Clamp(_type.Length * 7f + 22f, 72f, 150f));
                if (_statusFilter != AllFilter)
                    UtilityWindowTheme.CountPill(_statusFilter, PungentUtilityPackageStatus.GetTint(_statusFilter), Mathf.Clamp(_statusFilter.Length * 7f + 22f, 72f, 150f));
                if (_packageAvailabilityFilter != PackageAvailabilityFilter.AllUtilities)
                    UtilityWindowTheme.CountPill(GetPackageAvailabilityFilterLabel(_packageAvailabilityFilter), GetPackageAvailabilityFilterTint(_packageAvailabilityFilter), 120f);
                if (_includeAccessories)
                    UtilityWindowTheme.CountPill("Accessories", UtilityWindowTheme.Purple, 96f);
                if (_showNestedActions)
                    UtilityWindowTheme.CountPill("Actions", UtilityWindowTheme.Purple, 76f);
                if (!string.IsNullOrWhiteSpace(_focusedPackageId))
                    UtilityWindowTheme.CountPill("Package focus", UtilityWindowTheme.Amber, 112f);
                if (IsDeveloperModeEnabled() && (_showHiddenInternal || _showArchived || _filterMissingDocs || _filterHasDocs || _filterExperimental || _filterInProgress || _filterNoFacets))
                    UtilityWindowTheme.CountPill("Developer filters", UtilityWindowTheme.Amber, 124f);
                GUILayout.FlexibleSpace();
                if (UtilityWindowTheme.StyledButton(new GUIContent("Reset", "Clear active browser filters and return to the recommended core utility view."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(64f), GUILayout.Height(22f)))
                    ResetBrowserFilters();
            }
        }

        private int CountActiveBrowserFilters()
        {
            int count = 0;
            if (!string.IsNullOrWhiteSpace(_search))
                count++;
            if (_category != AllFilter)
                count++;
            if (_type != AllFilter)
                count++;
            if (_statusFilter != AllFilter)
                count++;
            if (_packageAvailabilityFilter != PackageAvailabilityFilter.AllUtilities)
                count++;
            if (_itemKindFilter != BrowserItemKindFilter.UtilitiesAndActions)
                count++;
            if (_includeAccessories)
                count++;
            if (_showNestedActions)
                count++;
            if (!string.IsNullOrWhiteSpace(_focusedPackageId))
                count++;
            if (IsDeveloperModeEnabled() && _showHiddenInternal)
                count++;
            if (IsDeveloperModeEnabled() && _showArchived)
                count++;
            if (IsDeveloperModeEnabled() && _filterMissingDocs)
                count++;
            if (IsDeveloperModeEnabled() && _filterHasDocs)
                count++;
            if (IsDeveloperModeEnabled() && _filterExperimental)
                count++;
            if (IsDeveloperModeEnabled() && _filterInProgress)
                count++;
            if (IsDeveloperModeEnabled() && _filterNoFacets)
                count++;
            return Mathf.Max(1, count);
        }

        private void DrawCompactCategorySelector()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.06f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Category", "Filter utilities by workflow category."), GUILayout.Width(64f));
                    string[] categories = BuildCategorySelectorValues();
                    string[] labels = categories.Select(category => string.Equals(category, AllFilter, StringComparison.OrdinalIgnoreCase) ? "All Categories" : PungentUtilityCategories.GetDisplayName(category)).ToArray();
                    int currentIndex = Mathf.Max(0, Array.FindIndex(categories, category => string.Equals(category, _category, StringComparison.OrdinalIgnoreCase)));
                    int nextIndex = EditorGUILayout.Popup(currentIndex, labels, GUILayout.MinWidth(160f));
                    if (nextIndex >= 0 && nextIndex < categories.Length && !string.Equals(categories[nextIndex], _category, StringComparison.OrdinalIgnoreCase))
                    {
                        _category = categories[nextIndex];
                        _type = AllFilter;
                        MarkCacheDirty();
                        SavePrefs();
                    }

                    UtilityWindowTheme.CountPill(_cachedQuery.Count + " shown", UtilityWindowTheme.Cyan, 84f);
                }

                if (_category != AllFilter)
                    EditorGUILayout.LabelField(PungentUtilityCategories.GetDescription(_category), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private string[] BuildCategorySelectorValues()
        {
            List<string> values = new List<string> { AllFilter };
            for (int i = 0; i < _cachedCategories.Length; i++)
            {
                string category = _cachedCategories[i];
                if (!string.Equals(category, AllFilter, StringComparison.OrdinalIgnoreCase))
                    values.Add(category);
            }

            return values.ToArray();
        }

        private void DrawBrowseOwnershipToolbarPopup()
        {
            string[] labels = GetPackageAvailabilityFilterLabels();
            int index = Mathf.Clamp((int)_packageAvailabilityFilter, 0, labels.Length - 1);
            int next = EditorGUILayout.Popup(index, labels, GUILayout.Width(position.width < 820f ? 132f : 150f), GUILayout.Height(22f));
            PackageAvailabilityFilter nextFilter = (PackageAvailabilityFilter)Mathf.Clamp(next, 0, labels.Length - 1);
            if (nextFilter == _packageAvailabilityFilter)
                return;

            _packageAvailabilityFilter = nextFilter;
            MarkCacheDirty();
            SavePrefs();
        }

        private void DrawSortModePopup()
        {
            string[] labels = Enum.GetNames(typeof(PungentUtilityBrowserSortMode));
            int index = Mathf.Clamp((int)_sortMode, 0, labels.Length - 1);
            int next = EditorGUILayout.Popup(index, labels, GUILayout.Width(position.width < 820f ? 132f : 148f), GUILayout.Height(22f));
            PungentUtilityBrowserSortMode nextMode = (PungentUtilityBrowserSortMode)Mathf.Clamp(next, 0, labels.Length - 1);
            if (nextMode == _sortMode)
                return;

            _sortMode = nextMode;
            MarkCacheDirty(BrowserDirtyFlags.Sort);
            SavePrefs();
        }

        private void DrawToolbarOverlayToggle(BrowserToolbarOverlay overlay, string label, Color tint, string tooltip)
        {
            bool active = _activeBrowserOverlay == overlay;
            if (UtilityWindowTheme.ToolbarButton(new GUIContent(label, tooltip), active, tint, GUILayout.Width(86f), GUILayout.Height(24f)))
            {
                Rect anchor = GUILayoutUtility.GetLastRect();
                if (active)
                    CloseBrowserOverlay();
                else
                    OpenBrowserOverlay(overlay, anchor);
            }
        }

        private void DrawViewModeButton(BrowserViewMode mode, string label)
        {
            bool selected = _viewMode == mode;
            if (UtilityWindowTheme.ToolbarButton(new GUIContent(label, "Switch Browser result density."), selected, UtilityWindowTheme.Teal, GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                if (_viewMode != mode)
                {
                    _viewMode = mode;
                    SavePrefs();
                    Repaint();
                }
            }
        }

        private void DrawCategorySidebar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.14f, 0.07f), GUILayout.Width(_sidebarWidth), GUILayout.ExpandHeight(true)))
            {
                UtilityWindowTheme.SectionTitle("Categories", UtilityWindowTheme.Cyan);
                _categoryScroll = EditorGUILayout.BeginScrollView(_categoryScroll, false, false, GUILayout.ExpandHeight(true));

                DrawCategoryButton(AllFilter, "All", CountSourceItemsForHeader());

                for (int i = 0; i < _cachedCategories.Length; i++)
                {
                    string category = _cachedCategories[i];
                    if (string.Equals(category, AllFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    DrawCategoryButton(category, PungentUtilityCategories.GetDisplayName(category), GetCachedCategoryCount(category));
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCategoryButton(string categoryValue, string label, int count)
        {
            bool selected = string.Equals(categoryValue, _category, StringComparison.OrdinalIgnoreCase);
            bool isAll = string.Equals(categoryValue, AllFilter, StringComparison.OrdinalIgnoreCase);
            Color tint = isAll ? UtilityWindowTheme.Neutral : PungentUtilityCategories.GetTint(categoryValue);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? tint : UtilityWindowTheme.Neutral, selected ? 0.24f : 0.10f, 0.05f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.ToolbarButton(new GUIContent(label, isAll ? "Show all utilities." : PungentUtilityCategories.GetDescription(categoryValue)), selected, tint, GUILayout.Height(24f)))
                    {
                        _category = categoryValue;
                        _type = AllFilter;
                        MarkCacheDirty();
                        SavePrefs();
                    }

                    UtilityWindowTheme.CountPill(count.ToString(), selected ? tint : UtilityWindowTheme.Neutral, 34f);
                }

            }
        }

        private int GetCachedCategoryCount(string category)
        {
            if (string.Equals(category, AllFilter, StringComparison.OrdinalIgnoreCase))
                return _cachedSourceCount;

            return _browserViewModel.CategoryCounts.TryGetValue(category, out int count) ? count : 0;
        }

        private void DrawCategoryDeveloperControls(string categoryValue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Colour", "Override the default tint used by this category."), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(44f));

                EditorGUI.BeginChangeCheck();
                Color nextTint = EditorGUILayout.ColorField(GUIContent.none, PungentUtilityCategories.GetTint(categoryValue), false, false, false, GUILayout.Width(44f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Set Utility Category Colour");
                    PungentUtilityCategoryOverrides.instance.SetTintOverride(categoryValue, nextTint);
                    _status = "Updated " + PungentUtilityCategories.GetDisplayName(categoryValue) + " colour.";
                    MarkCacheDirty();
                }

                if (UtilityWindowTheme.StyledButton(new GUIContent("Edit", "Open the unified metadata workbench focused on this category."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(48f), GUILayout.Height(20f)))
                    PungentUtilityDeveloperToolsWindow.OpenCategory(categoryValue);

                if (PungentUtilityCategories.HasAppearanceOverride(categoryValue))
                    GUILayout.Label(new GUIContent("Override", "This category has a project-local appearance override."), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(54f));

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawBrowserPane()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawUtilityBrowser();
            }
        }

        private void OpenBrowserOverlay(BrowserToolbarOverlay overlay, Rect anchor)
        {
            _activeBrowserOverlay = overlay;
            _browserOverlayAnchorRect = anchor;
            _browserOverlayScroll = Vector2.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void CloseBrowserOverlay()
        {
            _activeBrowserOverlay = BrowserToolbarOverlay.None;
            _browserOverlayAnchorRect = Rect.zero;
            _browserOverlayRect = Rect.zero;
            _browserOverlayScroll = Vector2.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void HandleBrowserOverlayInput()
        {
            Event current = Event.current;
            if (current == null)
                return;

            if (_selectedTab != BrowserTab.Browse)
            {
                if (_activeBrowserOverlay != BrowserToolbarOverlay.None)
                    CloseBrowserOverlay();
                return;
            }

            if (current.type == EventType.KeyDown)
            {
                if (_activeBrowserOverlay != BrowserToolbarOverlay.None && current.keyCode == KeyCode.Escape)
                {
                    CloseBrowserOverlay();
                    current.Use();
                    return;
                }

                bool focusSearch = (current.keyCode == KeyCode.Slash && !current.control && !current.command && !current.alt) ||
                                   ((current.control || current.command) && current.keyCode == KeyCode.F);
                if (focusSearch && !EditorGUIUtility.editingTextField)
                {
                    GUI.FocusControl("PFU.Browser.Search");
                    current.Use();
                    return;
                }
            }

            if (_activeBrowserOverlay == BrowserToolbarOverlay.None || current.type != EventType.MouseDown)
                return;

            Rect overlayRect = CalculateBrowserOverlayRect();
            if (overlayRect.Contains(current.mousePosition) || _browserOverlayAnchorRect.Contains(current.mousePosition))
            {
                return;
            }

            bool toolbarOrChromeClick = current.mousePosition.y < overlayRect.y;
            CloseBrowserOverlay();
            if (!toolbarOrChromeClick)
                current.Use();
        }

        private Rect CalculateBrowserOverlayRect()
        {
            if (_activeBrowserOverlay == BrowserToolbarOverlay.None)
            {
                _browserOverlayRect = Rect.zero;
                return _browserOverlayRect;
            }

            float width = BrowserOverlayWidth(_activeBrowserOverlay);
            float height = BrowserOverlayHeight(_activeBrowserOverlay);
            width = Mathf.Min(width, Mathf.Max(260f, position.width - 24f));
            height = Mathf.Min(height, Mathf.Max(180f, position.height - 84f));

            float x = _browserOverlayAnchorRect.width > 0f
                ? _browserOverlayAnchorRect.center.x - width * 0.5f
                : 12f;
            x = Mathf.Clamp(x, 12f, Mathf.Max(12f, position.width - width - 12f));

            float y = _browserOverlayAnchorRect.height > 0f
                ? Mathf.Max(64f, _browserOverlayAnchorRect.yMax + 5f)
                : 92f;
            y = Mathf.Min(y, Mathf.Max(64f, position.height - height - 12f));

            _browserOverlayRect = new Rect(x, y, width, height);
            return _browserOverlayRect;
        }

        private static float BrowserOverlayWidth(BrowserToolbarOverlay overlay)
        {
            switch (overlay)
            {
                case BrowserToolbarOverlay.Filters:
                    return 520f;
                case BrowserToolbarOverlay.Settings:
                    return 460f;
                default:
                    return 380f;
            }
        }

        private static float BrowserOverlayHeight(BrowserToolbarOverlay overlay)
        {
            switch (overlay)
            {
                case BrowserToolbarOverlay.Filters:
                    return 310f;
                case BrowserToolbarOverlay.Settings:
                    return 330f;
                default:
                    return 250f;
            }
        }

        private void DrawBrowserOverlay()
        {
            if (_selectedTab != BrowserTab.Browse || _activeBrowserOverlay == BrowserToolbarOverlay.None)
                return;

            Rect rect = CalculateBrowserOverlayRect();
            UtilityWindowTheme.DrawPopupChrome(rect, BrowserOverlayTint(_activeBrowserOverlay));

            GUILayout.BeginArea(rect, UtilityWindowTheme.PanelStyle(BrowserOverlayTint(_activeBrowserOverlay), 0.08f, 0.04f, 6, 0));
            DrawBrowserOverlayHeader(_activeBrowserOverlay);
            _browserOverlayScroll = EditorGUILayout.BeginScrollView(_browserOverlayScroll, false, true, GUILayout.ExpandHeight(true));
            switch (_activeBrowserOverlay)
            {
                case BrowserToolbarOverlay.Filters:
                    DrawFiltersPanel();
                    break;
                case BrowserToolbarOverlay.Favourites:
                    DrawFavoritesPanel();
                    break;
                case BrowserToolbarOverlay.History:
                    DrawHistoryPanel();
                    break;
                case BrowserToolbarOverlay.Settings:
                    DrawSettingsPanel();
                    break;
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawBrowserOverlayHeader(BrowserToolbarOverlay overlay)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(BrowserOverlayTitle(overlay), UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (UtilityWindowTheme.StyledButton(new GUIContent("Close", "Close this overlay. Escape also closes it."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(58f), GUILayout.Height(22f)))
                    CloseBrowserOverlay();
            }
        }

        private static string BrowserOverlayTitle(BrowserToolbarOverlay overlay)
        {
            switch (overlay)
            {
                case BrowserToolbarOverlay.Filters:
                    return "Filters";
                case BrowserToolbarOverlay.Favourites:
                    return "Favourites";
                case BrowserToolbarOverlay.History:
                    return "History";
                case BrowserToolbarOverlay.Settings:
                    return "Browser Settings";
                default:
                    return "Browser";
            }
        }

        private static Color BrowserOverlayTint(BrowserToolbarOverlay overlay)
        {
            switch (overlay)
            {
                case BrowserToolbarOverlay.Filters:
                    return UtilityWindowTheme.Blue;
                case BrowserToolbarOverlay.Favourites:
                    return UtilityWindowTheme.Amber;
                case BrowserToolbarOverlay.History:
                    return UtilityWindowTheme.Cyan;
                case BrowserToolbarOverlay.Settings:
                    return UtilityWindowTheme.Neutral;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private void DrawFiltersPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Filters", UtilityWindowTheme.Blue, CountSourceItemsForHeader() + " registered items");

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawItemKindPopup();
                    DrawTypePopup();
                    DrawStatusPopup();
                    DrawPackageAvailabilityPopup();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawFilterToggle("Accessories", ref _includeAccessories, "Include accessory utilities as top-level cards.", 104f);
                    DrawFilterToggle("Actions", ref _showNestedActions, "Include actions as top-level cards. Search still finds actions when this is off.", 82f);
                    GUILayout.FlexibleSpace();
                    if (HasActiveFilters() && UtilityWindowTheme.StyledButton(new GUIContent("Reset Filters", "Clear active browser filters and return to the recommended core utility view."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(94f), GUILayout.Height(22f)))
                        ResetBrowserFilters();
                }

                DrawExperimentalAccessToggle();
                DrawDeveloperFilters();

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetadataPill(_category == AllFilter ? "All Categories" : PungentUtilityCategories.GetDisplayName(_category), _category == AllFilter ? UtilityWindowTheme.Neutral : PungentUtilityCategories.GetTint(_category));
                    DrawMetadataPill(_type == AllFilter ? "All Types" : _type, UtilityWindowTheme.Teal);
                    DrawMetadataPill(_statusFilter == AllFilter ? "All Statuses" : _statusFilter, _statusFilter == AllFilter ? UtilityWindowTheme.Neutral : PungentUtilityPackageStatus.GetTint(_statusFilter));
                    DrawMetadataPill(GetPackageAvailabilityFilterLabel(_packageAvailabilityFilter), GetPackageAvailabilityFilterTint(_packageAvailabilityFilter));
                    DrawMetadataPill(_sortMode.ToString(), UtilityWindowTheme.Cyan);
                    if (_includeAccessories)
                        DrawMetadataPill("Accessories", UtilityWindowTheme.Purple);
                    if (_showNestedActions)
                        DrawMetadataPill("Actions", UtilityWindowTheme.Purple);
                    GUILayout.FlexibleSpace();
                }

                if (_category != AllFilter)
                    EditorGUILayout.LabelField(PungentUtilityCategories.GetDescription(_category), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawExperimentalAccessToggle()
        {
            bool developerBypass = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(developerBypass))
                {
                    bool enabled = PungentUtilityAccessPolicy.ExperimentalUtilitiesEnabled;
                    if (UtilityWindowTheme.ToolbarButton(new GUIContent("Enable Experimental Utilities", PungentUtilityAccessPolicy.ExperimentalDisabledMessage), enabled, UtilityWindowTheme.Amber, GUILayout.Width(176f), GUILayout.Height(22f)))
                    {
                        PungentUtilityAccessPolicy.ExperimentalUtilitiesEnabled = !enabled;
                        MarkCacheDirty();
                    }
                }

                if (!PungentUtilityAccessPolicy.ExperimentalUtilitiesEnabled)
                    EditorGUILayout.LabelField("Experimental cards stay visible but locked until this is enabled.", UtilityWindowTheme.MutedMiniLabelStyle);
                else
                    EditorGUILayout.LabelField("Experimental utilities are accessible from Browser, menus, and utility integrations.", UtilityWindowTheme.MutedMiniLabelStyle);

                GUILayout.FlexibleSpace();
            }
        }


        private void DrawItemKindPopup()
        {
            string[] labels = { "Utilities + Actions", "Utilities", "Actions" };
            int index = Mathf.Clamp((int)_itemKindFilter, 0, labels.Length - 1);
            int next = EditorGUILayout.Popup(new GUIContent("Kind", "Choose whether the browser shows utility workspaces, callable actions, or both."), index, labels);
            BrowserItemKindFilter nextFilter = (BrowserItemKindFilter)Mathf.Clamp(next, 0, labels.Length - 1);
            if (nextFilter == _itemKindFilter)
                return;

            _itemKindFilter = nextFilter;
            MarkCacheDirty();
            SavePrefs();
        }

        private void DrawTypePopup()
        {
            int typeIndex = Mathf.Max(0, Array.IndexOf(_cachedTypes, _type));
            int nextTypeIndex = EditorGUILayout.Popup(new GUIContent("Type", "Specific workflow or tool kind."), typeIndex, _cachedTypes);
            string nextType = _cachedTypes[Mathf.Clamp(nextTypeIndex, 0, _cachedTypes.Length - 1)];
            if (!string.Equals(nextType, _type, StringComparison.OrdinalIgnoreCase))
            {
                _type = nextType;
                MarkCacheDirty();
                SavePrefs();
            }
        }

        private void DrawStatusPopup()
        {
            int statusIndex = Mathf.Max(0, Array.IndexOf(_cachedStatuses, _statusFilter));
            int nextStatusIndex = EditorGUILayout.Popup(new GUIContent("Status", "Stability/maturity label."), statusIndex, _cachedStatuses);
            string nextStatus = _cachedStatuses[Mathf.Clamp(nextStatusIndex, 0, _cachedStatuses.Length - 1)];
            if (!string.Equals(nextStatus, _statusFilter, StringComparison.OrdinalIgnoreCase))
            {
                _statusFilter = nextStatus;
                MarkCacheDirty();
                SavePrefs();
            }
        }

        private void DrawPackageAvailabilityPopup()
        {
            string[] labels = GetPackageAvailabilityFilterLabels();
            int index = Mathf.Clamp((int)_packageAvailabilityFilter, 0, labels.Length - 1);
            int next = EditorGUILayout.Popup(new GUIContent("Utilities", "Filter by package ownership and install state."), index, labels);
            PackageAvailabilityFilter nextFilter = (PackageAvailabilityFilter)Mathf.Clamp(next, 0, labels.Length - 1);
            if (nextFilter == _packageAvailabilityFilter)
                return;

            _packageAvailabilityFilter = nextFilter;
            MarkCacheDirty();
            SavePrefs();
        }

        private void DrawFavoritesPanel()
        {
            List<PungentUtilityDescriptor> favorites = PungentUtilityRegistry.BrowserUtilities
                .Where(u => _favorites.Contains(u.Id))
                .OrderBy(u => PungentUtilityCategories.SortKey(PungentUtilityRegistry.GetAreaCategory(u)))
                .ThenBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Favourites", UtilityWindowTheme.Amber, favorites.Count + " pinned");
                _favoritesScroll = EditorGUILayout.BeginScrollView(_favoritesScroll, GUILayout.MaxHeight(favorites.Count > 0 ? 112f : 54f));
                DrawCompactUtilityButtons(favorites, "No pinned utilities yet. Use the star button on a utility card to pin it here.");
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHistoryPanel()
        {
            List<PungentUtilityDescriptor> recents = _recents
                .Select(PungentUtilityRegistry.Find)
                .Where(u => u != null && u.ShowInUtilitiesBrowser)
                .Take(10)
                .ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                UtilityWindowTheme.SectionTitle("History", UtilityWindowTheme.Cyan, recents.Count + " recent");
                _historyScroll = EditorGUILayout.BeginScrollView(_historyScroll, GUILayout.MaxHeight(recents.Count > 0 ? 112f : 54f));
                DrawCompactUtilityButtons(recents, "No recent utilities yet. Open a utility to build history.");
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSettingsPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                UtilityWindowTheme.SectionTitle("Browser Settings", UtilityWindowTheme.Neutral);
                EditorGUI.BeginChangeCheck();
                _groupByType = EditorGUILayout.ToggleLeft(new GUIContent("Group list results by Type", "When enabled, list mode groups utilities by their granular type."), _groupByType);
                _showCapabilityTags = EditorGUILayout.ToggleLeft(new GUIContent("Show capability tags", "Show Selection, Context, and Scene Overlay tags on utility cards."), _showCapabilityTags);
                _showRelated = EditorGUILayout.ToggleLeft(new GUIContent("Show related utility hints on cards", "Show compact related hints on selected drawers and legacy cards."), _showRelated);
                _includeAccessories = EditorGUILayout.ToggleLeft(new GUIContent("Include accessory utilities as top-level cards", "Accessory utilities normally appear inside their owning utility drawer and through search."), _includeAccessories);
                _showNestedActions = EditorGUILayout.ToggleLeft(new GUIContent("Show actions as top-level results", "Actions normally appear inside selected utility drawers and through search."), _showNestedActions);
                if (EditorGUI.EndChangeCheck())
                {
                    MarkCacheDirty(BrowserDirtyFlags.Filters | BrowserDirtyFlags.Sort);
                    SavePrefs();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Open Theme Customizer", "Open shared PFU appearance controls."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(164f), GUILayout.Height(24f)))
                        ActivateUtilityPrimaryAction(PungentUtilityRegistry.Find("theme-customizer"));

                    GUILayout.FlexibleSpace();
                }

                DrawGlobalDeveloperModeSettings();
            }
        }

        private void DrawGlobalDeveloperModeSettings()
        {
            if (!PungentDeveloperMode.Available)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Developer Tools", EditorStyles.boldLabel);
            bool enabled = PungentDeveloperMode.Enabled;
            bool next = EditorGUILayout.ToggleLeft(new GUIContent("Enable Developer Tools", PungentDeveloperMode.DeveloperModeTooltip), enabled);
            if (next != enabled)
            {
                PungentDeveloperMode.Enabled = next;
                _status = next ? "Developer tools enabled." : "Developer tools disabled.";
                MarkCacheDirty();
            }

            if (PungentDeveloperMode.Enabled)
            {
                EditorGUILayout.HelpBox(PungentDeveloperMode.WarningMessage, MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.StyledButton(new GUIContent("Open Developer Tools", "Open the unified workbench for metadata, category, package, and generated-help developer tools."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(154f), GUILayout.Height(22f)))
                        PungentUtilityDeveloperToolsWindow.Open();
                    GUILayout.FlexibleSpace();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Developer-only metadata, package, and diagnostic panels stay hidden until enabled here.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void TogglePackageDetails(PungentUtilityPackageRecord record, PungentUtilityDescriptor utility, string message)
        {
            if (record == null)
                return;

            string packageId = PungentUtilityPackageCatalog.NormalizePackageId(record.packageId);
            if (_expandedPackageDetails.Contains(packageId) && string.IsNullOrWhiteSpace(message))
            {
                CollapsePackageDetails(packageId);
                return;
            }

            ShowPackageDetailsInline(record, utility, message);
        }

        private void ShowPackageDetailsInline(PungentUtilityPackageRecord record, PungentUtilityDescriptor utility, string message)
        {
            if (record == null)
                return;

            PungentUtilityPackageRecord copy = PungentUtilityPackageCatalog.CloneRecord(record);
            string packageId = PungentUtilityPackageCatalog.NormalizePackageId(copy.packageId);
            _packageDetailRecord = copy;
            _packageDetailUtility = utility;
            _packageDetailMessage = message ?? string.Empty;
            _expandedPackageDetails.Add(packageId);

            if (_selectedTab != BrowserTab.OtherPackages)
                _selectedTab = BrowserTab.OtherPackages;

            if (!string.IsNullOrWhiteSpace(_packageSearch) && !PungentUtilityPackageCatalog.MatchesRecord(copy, _packageSearch))
                _packageSearch = string.Empty;

            _status = "Expanded package details for " + copy.displayName + ".";
            SavePrefs();
            Repaint();
        }

        private void CollapsePackageDetails(string packageId)
        {
            string normalizedPackageId = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            _expandedPackageDetails.Remove(normalizedPackageId);
            if (_packageDetailRecord != null && string.Equals(_packageDetailRecord.packageId, normalizedPackageId, StringComparison.OrdinalIgnoreCase))
            {
                _packageDetailRecord = null;
                _packageDetailUtility = null;
                _packageDetailMessage = string.Empty;
            }

            Repaint();
        }

        private string BuildIncludedUtilitiesSummary(PungentUtilityPackageRecord record, List<PungentUtilityDescriptor> registered)
        {
            List<string> labels = new List<string>();
            if (record.includedUtilityIds != null)
            {
                for (int i = 0; i < record.includedUtilityIds.Length; i++)
                {
                    string id = record.includedUtilityIds[i];
                    PungentUtilityDescriptor utility = PungentUtilityRegistry.Find(id);
                    labels.Add(utility == null ? id : utility.DisplayName);
                }
            }

            if (registered != null)
            {
                for (int i = 0; i < registered.Count; i++)
                {
                    if (!labels.Any(label => string.Equals(label, registered[i].DisplayName, StringComparison.OrdinalIgnoreCase)))
                        labels.Add(registered[i].DisplayName);
                }
            }

            if (labels.Count == 0)
                return "No registered utilities currently mapped.";

            return string.Join(", ", labels.Take(8).ToArray()) + (labels.Count > 8 ? " +" + (labels.Count - 8) + " more" : string.Empty);
        }

        private void OpenHelpForPackageDetail()
        {
            if (_packageDetailUtility != null && !string.IsNullOrWhiteSpace(_packageDetailUtility.DocumentationTopicId))
            {
                string topicId = _packageDetailUtility.DocumentationTopicId;
                if (PungentUtilityHelpRegistry.Find(_packageDetailUtility.Id, topicId, topicId) != null)
                {
                    PungentUtilityHelpRegistry.Open(_packageDetailUtility.Id, topicId, topicId);
                    return;
                }
            }

            if (_packageDetailUtility != null && PungentUtilityHelpRegistry.Find(_packageDetailUtility.Id, "overview", "overview") != null)
            {
                PungentUtilityHelpRegistry.Open(_packageDetailUtility.Id, "overview", "overview");
                return;
            }

            PungentUtilityHelpBrowserWindow.Open();
        }

        private void DrawCompactUtilityButtons(List<PungentUtilityDescriptor> utilities, string emptyMessage)
        {
            if (utilities == null || utilities.Count == 0)
            {
                EditorGUILayout.LabelField(emptyMessage, UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            int perRow = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(220f, position.width - 260f) / 150f));
            for (int i = 0; i < utilities.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int end = Mathf.Min(utilities.Count, i + perRow);
                    for (int j = i; j < end; j++)
                    {
                        PungentUtilityDescriptor utility = utilities[j];
                        using (new EditorGUI.DisabledScope(!IsPrimaryActionEnabled(utility)))
                        {
                            if (UtilityWindowTheme.StyledButton(new GUIContent(utility.DisplayName, BuildUtilityTooltip(utility)), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(142f)))
                                ActivateUtilityPrimaryAction(utility);
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawUtilityBrowser()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle(GetBrowserSectionTitle(), UtilityWindowTheme.Teal, _cachedQuery.Count + " shown");
                    GUILayout.FlexibleSpace();
                    DrawMetadataPill(_viewMode == BrowserViewMode.Compact ? "Compact" : "List", UtilityWindowTheme.Teal);
                }

                if (_cachedQuery.Count == 0)
                {
                    EditorGUILayout.HelpBox(BuildEmptyStateMessage(), MessageType.Info);
                    return;
                }

                _browserScroll = EditorGUILayout.BeginScrollView(_browserScroll, GUILayout.ExpandHeight(true));
                if (_viewMode == BrowserViewMode.Compact)
                    DrawCompactResults();
                else
                    DrawListResults();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawListResults()
        {
            foreach (CategoryGroupView categoryGroup in _cachedCategoryGroups)
            {
                DrawCategoryHeader(categoryGroup.Category, categoryGroup.Count);

                if (_groupByType)
                {
                    foreach (TypeGroupView typeGroup in categoryGroup.Types)
                    {
                        EditorGUILayout.Space(3f);
                        EditorGUILayout.LabelField(typeGroup.Name, EditorStyles.boldLabel);

                        for (int i = 0; i < typeGroup.Utilities.Count; i++)
                            DrawUtilityListCard(typeGroup.Utilities[i]);
                    }
                }
                else
                {
                    foreach (PungentUtilityDescriptor utility in categoryGroup.Types.SelectMany(t => t.Utilities).OrderBy(u => u.SortOrder).ThenBy(u => u.DisplayName))
                        DrawUtilityListCard(utility);
                }
            }
        }

        private void DrawCompactResults()
        {
            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.DrawCardList"))
            {
                if (_cachedQuery.Count < CompactCullingThreshold)
                {
                    for (int i = 0; i < _cachedQuery.Count; i++)
                        DrawUtilityCompactCard(_cachedQuery[i]);
                    return;
                }

                DrawCulledCompactResults();
            }
        }

        private void DrawCulledCompactResults()
        {
            float viewportHeight = Mathf.Max(280f, position.height - 220f);
            float top = Mathf.Max(0f, _browserScroll.y - CompactCardEstimatedHeight * CompactCullingBuffer);
            float bottom = _browserScroll.y + viewportHeight + CompactCardEstimatedHeight * CompactCullingBuffer;
            float cursor = 0f;
            int start = 0;
            int end = _cachedQuery.Count;

            for (int i = 0; i < _cachedQuery.Count; i++)
            {
                float height = EstimateCompactResultHeight(_cachedQuery[i]);
                if (cursor + height >= top)
                {
                    start = i;
                    break;
                }
                cursor += height;
            }

            float visibleCursor = cursor;
            for (int i = start; i < _cachedQuery.Count; i++)
            {
                visibleCursor += EstimateCompactResultHeight(_cachedQuery[i]);
                if (visibleCursor > bottom)
                {
                    end = Mathf.Min(_cachedQuery.Count, i + 1);
                    break;
                }
            }

            if (cursor > 0f)
                GUILayout.Space(cursor);

            for (int i = start; i < end; i++)
                DrawUtilityCompactCard(_cachedQuery[i]);

            float remaining = 0f;
            for (int i = end; i < _cachedQuery.Count; i++)
                remaining += EstimateCompactResultHeight(_cachedQuery[i]);
            if (remaining > 0f)
                GUILayout.Space(remaining);
        }

        private float EstimateCompactResultHeight(PungentUtilityDescriptor utility)
        {
            if (!IsSelectedUtility(utility))
                return CompactCardEstimatedHeight;

            PungentUtilityBrowserViewModel.Item item = utility == null ? null : _browserViewModel.GetItem(utility.Id);
            int rowCount = 0;
            if (item != null)
            {
                rowCount += item.Actions == null ? 0 : item.Actions.Count;
                rowCount += item.Accessories == null ? 0 : item.Accessories.Count;
                rowCount += item.Related == null ? 0 : item.Related.Count;
            }

            return CompactCardEstimatedHeight + CompactDrawerBaseEstimatedHeight + Mathf.Min(rowCount, 8) * CompactDrawerRowEstimatedHeight;
        }

        private void DrawCategoryHeader(string category, int count)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(PungentUtilityCategories.GetDisplayName(category), PungentUtilityCategories.GetDescription(category)), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(count + " items", PungentUtilityCategories.GetTint(category), 76f);
            }
        }

        private void DrawUtilityListCard(PungentUtilityDescriptor utility)
        {
            DrawUtilityCard(utility, 0f, false);
        }

        private void DrawUtilityCompactCard(PungentUtilityDescriptor utility)
        {
            bool isPrimary = utility.IsPrimaryUtility;
            bool selected = IsSelectedUtility(utility);
            string status = PungentUtilityRegistry.GetStatus(utility);
            Color tint = GetUtilityCardTint(utility, selected, isPrimary);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.22f : isPrimary ? 0.16f : 0.10f, selected ? 0.08f : 0.045f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isFavorite = _favorites.Contains(utility.Id);
                    if (UtilityWindowTheme.IconButton(new GUIContent(isFavorite ? "*" : "+", "Pin or unpin this utility."), isFavorite ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, GUILayout.Width(24f), GUILayout.Height(22f)))
                        ToggleFavorite(utility.Id);

                    if (UtilityWindowTheme.IconButton(new GUIContent(selected ? "v" : ">", selected ? "Collapse selected details." : "Select this utility and show actions, included tools, related workflows, help, and package notes."), selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, GUILayout.Width(24f), GUILayout.Height(22f)))
                        ToggleSelectedUtility(utility.Id);

                    EditorGUILayout.LabelField(new GUIContent(utility.DisplayName, BuildUtilityTooltip(utility)), EditorStyles.boldLabel, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    PungentUtilityBrowserViewModel.Item item = _browserViewModel.GetItem(utility.Id);
                    if (item != null)
                        UtilityWindowTheme.CountPill(item.ShortAvailabilityLabel, item.AvailabilityTint, Mathf.Clamp(item.ShortAvailabilityLabel.Length * 7f + 20f, 64f, 110f));
                    UtilityWindowTheme.CountPill(status, PungentUtilityPackageStatus.GetTint(status), Mathf.Clamp(status.Length * 7f + 18f, 64f, 120f));
                    if (DrawUtilityPrimaryActionButton(utility, true, 24f))
                        ActivateUtilityPrimaryAction(utility);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(PungentUtilityRegistry.GetModule(utility) + " / " + PungentUtilityRegistry.GetUtilityType(utility), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(180f));
                    DrawMetadataPill(PungentUtilityCategories.GetDisplayName(PungentUtilityRegistry.GetAreaCategory(utility)), PungentUtilityCategories.GetTint(PungentUtilityRegistry.GetAreaCategory(utility)));
                    DrawBrowserRolePill(utility);
                    if (IsDeveloperModeEnabled() && PungentUtilityRegistry.HasMetadataOverride(utility.Id))
                        DrawMetadataPill("Override", UtilityWindowTheme.Amber);
                    if (IsDeveloperModeEnabled() && !utility.HasCategoryFacets)
                        DrawMetadataPill("No facets", UtilityWindowTheme.Amber);
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField(new GUIContent(utility.Description, utility.Description), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinHeight(16f));
                DrawUtilityAccessNotice(utility);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (!selected)
                    {
                        DrawDocumentationButtons(utility, true);
                        DrawUtilityPackageSecondaryActions(utility, true);
                    }
                }

                if (selected)
                    DrawSelectedUtilityDrawer(utility);
            }
        }

        private void DrawUtilityCard(PungentUtilityDescriptor utility, float width, bool gridCard)
        {
            bool isPrimary = utility.IsPrimaryUtility;
            bool selected = IsSelectedUtility(utility);
            string status = PungentUtilityRegistry.GetStatus(utility);
            string utilityType = PungentUtilityRegistry.GetUtilityType(utility);
            Color statusTint = PungentUtilityPackageStatus.GetTint(status);
            GUILayoutOption[] options = width > 0f
                ? new[] { GUILayout.Width(width), GUILayout.MinHeight(gridCard ? 128f : 0f) }
                : Array.Empty<GUILayoutOption>();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(GetUtilityCardTint(utility, selected, isPrimary), selected ? 0.22f : isPrimary ? 0.16f : 0.10f, selected ? 0.08f : 0.045f, 5, 3), options))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isFavorite = _favorites.Contains(utility.Id);
                    if (UtilityWindowTheme.IconButton(new GUIContent(isFavorite ? "*" : "+", "Pin or unpin this utility."), isFavorite ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, GUILayout.Width(28f), GUILayout.Height(22f)))
                        ToggleFavorite(utility.Id);

                    if (UtilityWindowTheme.IconButton(new GUIContent(selected ? "v" : ">", selected ? "Collapse selected details." : "Select this utility and show contextual details."), selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, GUILayout.Width(24f), GUILayout.Height(22f)))
                        ToggleSelectedUtility(utility.Id);

                    EditorGUILayout.LabelField(new GUIContent(utility.DisplayName, BuildUtilityTooltip(utility)), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(status, statusTint, Mathf.Clamp(status.Length * 7f + 18f, 70f, 140f));
                }

                EditorGUILayout.LabelField(utility.Description, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinHeight(gridCard ? 34f : 16f));
                DrawUtilityAccessNotice(utility);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetadataPill(PungentUtilityCategories.GetDisplayName(PungentUtilityRegistry.GetAreaCategory(utility)), PungentUtilityCategories.GetTint(PungentUtilityRegistry.GetAreaCategory(utility)));
                    DrawMetadataPill(utilityType, UtilityWindowTheme.Teal);
                    DrawPackageAvailabilityPill(utility, false);
                    DrawItemKindPill(utility);
                    DrawVisibilityPill(utility);
                    if (isPrimary)
                        DrawMetadataPill("Workspace", UtilityWindowTheme.Cyan);
                    if (IsDeveloperModeEnabled() && PungentUtilityRegistry.HasMetadataOverride(utility.Id))
                        DrawMetadataPill("Override", UtilityWindowTheme.Amber);
                    GUILayout.FlexibleSpace();
                }

                if (_showCapabilityTags)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawSupportPill(utility.SupportsSelection, "Selection");
                        DrawSupportPill(utility.SupportsContextMenu, "Context");
                        DrawSupportPill(utility.SupportsSceneOverlay, "Scene Overlay");
                        GUILayout.FlexibleSpace();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (!selected)
                    {
                        DrawDocumentationButtons(utility, false);
                        DrawUtilityPackageSecondaryActions(utility, false);
                    }
                    if (DrawUtilityPrimaryActionButton(utility, false, 26f))
                        ActivateUtilityPrimaryAction(utility);
                }

                if (selected)
                    DrawSelectedUtilityDrawer(utility);
            }
        }

        private bool IsSelectedUtility(PungentUtilityDescriptor utility)
        {
            return utility != null && !string.IsNullOrWhiteSpace(_selectedUtilityId) && string.Equals(utility.Id, _selectedUtilityId, StringComparison.OrdinalIgnoreCase);
        }

        private static Color GetUtilityCardTint(PungentUtilityDescriptor utility, bool selected, bool isPrimary)
        {
            if (selected)
                return UtilityWindowTheme.Teal;
            if (utility != null && utility.OverrideBrowserCardTint)
                return utility.BrowserCardTint;
            if (utility != null)
                return PungentUtilityCategories.GetTint(PungentUtilityRegistry.GetAreaCategory(utility));
            return isPrimary ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral;
        }

        private void ToggleSelectedUtility(string utilityId)
        {
            _selectedUtilityId = string.Equals(_selectedUtilityId, utilityId, StringComparison.OrdinalIgnoreCase) ? string.Empty : utilityId;
            SavePrefs();
            Repaint();
        }

        private void DrawBrowserRolePill(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;

            Color tint;
            switch (utility.BrowserRole)
            {
                case PungentUtilityBrowserRole.Action:
                    tint = UtilityWindowTheme.Purple;
                    break;
                case PungentUtilityBrowserRole.AccessoryUtility:
                    tint = UtilityWindowTheme.Cyan;
                    break;
                case PungentUtilityBrowserRole.Internal:
                case PungentUtilityBrowserRole.LegacyAlias:
                    tint = UtilityWindowTheme.Amber;
                    break;
                default:
                    tint = UtilityWindowTheme.Neutral;
                    break;
            }

            DrawMetadataPill(GetBrowserRoleLabel(utility.BrowserRole), tint);
        }

        private static string GetBrowserRoleLabel(PungentUtilityBrowserRole role)
        {
            switch (role)
            {
                case PungentUtilityBrowserRole.AccessoryUtility:
                    return "Accessory";
                case PungentUtilityBrowserRole.Action:
                    return "Action";
                case PungentUtilityBrowserRole.Bridge:
                    return "Bridge";
                case PungentUtilityBrowserRole.Internal:
                    return "Internal";
                case PungentUtilityBrowserRole.LegacyAlias:
                    return "Legacy";
                default:
                    return "Core";
            }
        }

        private void DrawSelectedUtilityDrawer(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;

            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.DrawSelectedDrawer"))
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 5, 3)))
            {
                PungentUtilityBrowserViewModel.Item item = _browserViewModel.GetItem(utility.Id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetadataPill(utility.EffectivePackageDisplayName, UtilityWindowTheme.Neutral);
                    if (item != null)
                        DrawMetadataPill(item.AvailabilityLabel, item.AvailabilityTint);
                    DrawBrowserRolePill(utility);
                    GUILayout.FlexibleSpace();
                    if (DrawUtilityPrimaryActionButton(utility, true, 24f))
                        ActivateUtilityPrimaryAction(utility);
                }

                DrawDrawerSection("Actions", ref _drawerActions, item == null ? (IReadOnlyList<PungentUtilityDescriptor>)Array.Empty<PungentUtilityDescriptor>() : item.Actions, DrawActionDrawerRow, UtilityWindowTheme.Purple);
                DrawDrawerSection("Included tools", ref _drawerAccessories, item == null ? (IReadOnlyList<PungentUtilityDescriptor>)Array.Empty<PungentUtilityDescriptor>() : item.Accessories, DrawAccessoryDrawerRow, UtilityWindowTheme.Cyan);
                DrawDrawerSection("Related workflows", ref _drawerRelated, item == null ? (IReadOnlyList<PungentUtilityDescriptor>)Array.Empty<PungentUtilityDescriptor>() : item.Related, DrawRelatedDrawerRow, UtilityWindowTheme.Teal);
                DrawDrawerHelpSection(utility);
                DrawDrawerPackageSection(utility);
                DrawDrawerDeveloperSection(utility);
            }
        }

        private void DrawDrawerSection(string title, ref bool expanded, IReadOnlyList<PungentUtilityDescriptor> utilities, Action<PungentUtilityDescriptor> drawRow, Color tint)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                expanded = EditorGUILayout.Foldout(expanded, title, true);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill((utilities == null ? 0 : utilities.Count).ToString(), tint, 42f);
            }

            if (!expanded)
                return;

            if (utilities == null || utilities.Count == 0)
            {
                EditorGUILayout.LabelField("None mapped yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.DrawDrawerRelationships"))
            {
                for (int i = 0; i < utilities.Count; i++)
                    drawRow?.Invoke(utilities[i]);
            }
        }

        private void DrawActionDrawerRow(PungentUtilityDescriptor utility)
        {
            DrawDrawerUtilityRow(utility, "Run", UtilityWindowTheme.Purple);
        }

        private void DrawAccessoryDrawerRow(PungentUtilityDescriptor utility)
        {
            DrawDrawerUtilityRow(utility, "Open", UtilityWindowTheme.Cyan);
        }

        private void DrawRelatedDrawerRow(PungentUtilityDescriptor utility)
        {
            DrawDrawerUtilityRow(utility, utility != null && utility.IsAction ? "Run" : "Open", UtilityWindowTheme.Teal);
        }

        private void DrawDrawerUtilityRow(PungentUtilityDescriptor utility, string actionLabel, Color tint)
        {
            if (utility == null)
                return;

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(tint, 0.055f, 0.025f, 4, 1)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(180f)))
                {
                    EditorGUILayout.LabelField(new GUIContent(utility.DisplayName, utility.Id), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(utility.Description, UtilityWindowTheme.MutedMiniLabelStyle);
                }

                GUILayout.FlexibleSpace();
                DrawPackageAvailabilityPill(utility, true);
                using (new EditorGUI.DisabledScope(!IsPrimaryActionEnabled(utility)))
                {
                    if (UtilityWindowTheme.StyledButton(new GUIContent(actionLabel, BuildUtilityTooltip(utility)), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(58f), GUILayout.Height(22f)))
                        ActivateUtilityPrimaryAction(utility);
                }
            }
        }

        private void DrawDrawerHelpSection(PungentUtilityDescriptor utility)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _drawerHelp = EditorGUILayout.Foldout(_drawerHelp, "Help", true);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
                GUILayout.FlexibleSpace();
            }

            if (!_drawerHelp)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawDocumentationButtons(utility, true);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawDrawerPackageSection(PungentUtilityDescriptor utility)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _drawerPackage = EditorGUILayout.Foldout(_drawerPackage, "Package notes", true);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();
                GUILayout.FlexibleSpace();
            }

            if (!_drawerPackage)
                return;

            DrawUtilityPackageSecondaryActions(utility, true);
            DrawMissingIntegrationCtas(utility);
            if (!string.IsNullOrWhiteSpace(utility.MissingDependencyMessage))
                EditorGUILayout.HelpBox(utility.MissingDependencyMessage, MessageType.Info);
            else
                EditorGUILayout.LabelField("Package: " + utility.EffectivePackageDisplayName + " (" + utility.NormalizedPackageId + ")", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawMissingIntegrationCtas(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;

            string description = string.IsNullOrWhiteSpace(utility.MissingDependencyMessage)
                ? "This workflow has optional or required package integrations."
                : utility.MissingDependencyMessage;

            DrawMissingIntegrationCtaSet("Requires package", description, utility.RequiredPackageIds, utility.Id, UtilityWindowTheme.Amber);
            DrawMissingIntegrationCtaSet("Optional extension available", "Add this extension to unlock related workflow controls.", utility.OptionalPackageIds, utility.Id, UtilityWindowTheme.Purple);
        }

        private void DrawMissingIntegrationCtaSet(string label, string description, string[] packageIds, string sourceUtilityId, Color tint)
        {
            if (packageIds == null || packageIds.Length == 0)
                return;

            List<string> missing = new List<string>();
            for (int i = 0; i < packageIds.Length; i++)
            {
                string id = PungentUtilityPackageCatalog.NormalizePackageId(packageIds[i]);
                PungentUtilityPackageRecord record = PungentUtilityPackageCatalog.FindRecord(id);
                PungentUtilityPackageAvailability availability = record == null
                    ? PungentUtilityPackageAvailability.Unowned
                    : PungentUtilityPackageCatalog.ResolveAvailability(record);
                PungentUtilityPackageSimulationState simulation = PungentUtilityPackageSimulation.GetState(id);
                bool simulatedMissing = simulation == PungentUtilityPackageSimulationState.AvailableNotOwned ||
                                        simulation == PungentUtilityPackageSimulationState.OwnedNotInstalled ||
                                        simulation == PungentUtilityPackageSimulationState.MissingRequiredPackage ||
                                        simulation == PungentUtilityPackageSimulationState.Disabled ||
                                        simulation == PungentUtilityPackageSimulationState.ComingSoonUnavailable;
                if (availability != PungentUtilityPackageAvailability.OwnedInstalled || simulatedMissing)
                    missing.Add(id);
            }

            if (missing.Count == 0)
                return;

            PungentPackageIntegrationGUI.DrawIntegrationCTA(label, description, missing.ToArray(), sourceUtilityId, tint);
        }

        private void DrawDrawerDeveloperSection(PungentUtilityDescriptor utility)
        {
            if (!IsDeveloperModeEnabled() || utility == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetadataPill("Priority " + utility.BrowserPriority, UtilityWindowTheme.Neutral);
                if (!string.IsNullOrWhiteSpace(utility.ParentUtilityId))
                    DrawMetadataPill("Parent " + utility.ParentUtilityId, UtilityWindowTheme.Cyan);
                GUILayout.FlexibleSpace();
                DrawMetadataEditButton(utility, true);
            }
        }


        private void DrawItemKindPill(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;

            if (utility.IsAction)
                DrawMetadataPill("Action", UtilityWindowTheme.Purple);
            else if (utility.ItemKind == PungentUtilityItemKind.Internal)
                DrawMetadataPill("Internal", UtilityWindowTheme.Cyan);
            else if (utility.ItemKind == PungentUtilityItemKind.LegacyAlias)
                DrawMetadataPill("Legacy", UtilityWindowTheme.Amber);
        }

        private void DrawVisibilityPill(PungentUtilityDescriptor utility)
        {
            if (!IsDeveloperModeEnabled() || utility == null)
                return;

            if (utility.Visibility == PungentUtilityVisibility.DeveloperOnly)
                DrawMetadataPill("Developer", UtilityWindowTheme.Cyan);
            else if (utility.Visibility == PungentUtilityVisibility.Archived)
                DrawMetadataPill("Archived", UtilityWindowTheme.Amber);
            else if (utility.Visibility == PungentUtilityVisibility.Hidden || !utility.ShowInUtilitiesBrowser)
                DrawMetadataPill("Hidden", UtilityWindowTheme.Neutral);
        }

        private void DrawPackageAvailabilityPill(PungentUtilityDescriptor utility, bool compact)
        {
            if (utility == null)
                return;

            PungentUtilityPackageAvailability availability = PungentUtilityPackageCatalog.ResolveAvailability(utility);
            string label = compact
                ? PungentUtilityPackageCatalog.BuildShortAvailabilityLabel(availability)
                : PungentUtilityPackageCatalog.BuildAvailabilityLabel(availability);
            float width = compact ? Mathf.Clamp(label.Length * 7f + 18f, 72f, 142f) : Mathf.Clamp(label.Length * 7f + 18f, 90f, 178f);
            GUIContent content = new GUIContent(label, PungentUtilityPackageCatalog.BuildAvailabilityTooltip(utility));
            using (UtilityWindowTheme.Background(PungentUtilityPackageCatalog.GetAvailabilityTint(availability)))
                GUILayout.Label(content, UtilityWindowTheme.CountPillStyle, GUILayout.Width(width));
        }

        private void DrawUtilityAccessNotice(PungentUtilityDescriptor utility)
        {
            if (utility == null || PungentUtilityAccessPolicy.CanAccess(utility))
                return;

            if (PungentUtilityAccessPolicy.IsExperimentalBlocked(utility))
            {
                EditorGUILayout.HelpBox(PungentUtilityAccessPolicy.ExperimentalDisabledMessage, MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("Coming soon: " + PungentUtilityPackageStatus.Normalize(utility.PackageStatus), MessageType.Info);
        }

        private string GetPrimaryActionLabel(PungentUtilityDescriptor utility)
        {
            if (utility != null && !PungentUtilityAccessPolicy.CanAccess(utility))
                return PungentUtilityAccessPolicy.BuildBlockedActionLabel(utility);

            switch (PungentUtilityPackageCatalog.ResolveAvailability(utility))
            {
                case PungentUtilityPackageAvailability.Unowned:
                    return "Open in Asset Store";
                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    return "Import";
                default:
                    return utility != null && utility.IsAction ? "Run" : "Open";
            }
        }

        private bool IsPrimaryActionEnabled(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return false;

            if (!PungentUtilityAccessPolicy.CanAccess(utility))
                return false;

            PungentUtilityPackageAvailability availability = PungentUtilityPackageCatalog.ResolveAvailability(utility);
            if (availability == PungentUtilityPackageAvailability.Unowned)
                return PungentUtilityPackageCatalog.HasAssetStoreRoute(utility);
            if (availability == PungentUtilityPackageAvailability.OwnedNotInstalled)
                return PungentUtilityPackageCatalog.HasValidImportPackagePath(utility);

            return GetCachedCanRun(utility);
        }

        private bool DrawUtilityPrimaryActionButton(PungentUtilityDescriptor utility, bool compact, float height)
        {
            string label = GetPrimaryActionLabel(utility);
            float width = GetPrimaryActionWidth(label, compact);
            bool enabled = IsPrimaryActionEnabled(utility);
            Color tint = GetPrimaryActionTint(utility);
            GUILayoutOption[] options = { GUILayout.Width(width), GUILayout.Height(height) };

            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (enabled)
                    return UtilityWindowTheme.StudioButton(new GUIContent(label, BuildUtilityTooltip(utility)), tint, UtilityWindowTheme.PungentButtonRole.Primary, options);

                return UtilityWindowTheme.StyledButton(new GUIContent(label, GetPrimaryActionDisabledReason(utility)), UtilityWindowTheme.PungentButtonRole.Disabled, options);
            }
        }

        private static float GetPrimaryActionWidth(string label, bool compact)
        {
            if (string.IsNullOrWhiteSpace(label))
                return compact ? 64f : 82f;

            float minimum = compact ? 58f : 82f;
            float maximum = compact ? 190f : 220f;
            return Mathf.Clamp(label.Length * 7f + 22f, minimum, maximum);
        }

        private static Color GetPrimaryActionTint(PungentUtilityDescriptor utility)
        {
            PungentUtilityPackageAvailability availability = PungentUtilityPackageCatalog.ResolveAvailability(utility);
            if (availability == PungentUtilityPackageAvailability.Unowned)
                return UtilityWindowTheme.Purple;
            if (availability == PungentUtilityPackageAvailability.OwnedNotInstalled)
                return UtilityWindowTheme.Amber;

            return utility != null && utility.IsAction ? UtilityWindowTheme.Purple : UtilityWindowTheme.Blue;
        }

        private static string GetPrimaryActionDisabledReason(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return "No utility was selected.";

            if (!PungentUtilityAccessPolicy.CanAccess(utility, out string accessReason))
                return accessReason;

            switch (PungentUtilityPackageCatalog.ResolveAvailability(utility))
            {
                case PungentUtilityPackageAvailability.Unowned:
                    return "Asset Store link not configured.";
                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    return PungentUtilityPackageCatalog.HasPackageManagerRoute(utility)
                        ? "Import route not configured. Use Package Manager if available."
                        : "Import route not configured.";
                default:
                    return string.IsNullOrWhiteSpace(utility.DisabledReason) ? "This utility cannot run in the current context." : utility.DisabledReason;
            }
        }

        private void DrawUtilityPackageSecondaryActions(PungentUtilityDescriptor utility, bool compact)
        {
            if (utility == null)
                return;

            PungentUtilityPackageAvailability availability = PungentUtilityPackageCatalog.ResolveAvailability(utility);
            if (availability == PungentUtilityPackageAvailability.OwnedInstalled)
                return;

            if (!PungentUtilityAccessPolicy.CanAccess(utility))
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Info", "Show package details and fallback instructions."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(46f), GUILayout.Height(compact ? 24f : 26f)))
                    OpenPackageDetailsInline(utility, null);
                return;
            }

            if (availability == PungentUtilityPackageAvailability.OwnedNotInstalled && PungentUtilityPackageCatalog.HasPackageManagerRoute(utility))
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Package Manager", "Open Package Manager and copy this package ID if direct selection is unavailable."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(compact ? 120f : 124f), GUILayout.Height(compact ? 24f : 26f)))
                    OpenPackageInstallFlow(utility);
            }

            if (UtilityWindowTheme.StyledButton(new GUIContent("Info", "Show package details and fallback instructions."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(46f), GUILayout.Height(compact ? 24f : 26f)))
                OpenPackageDetailsInline(utility, null);
        }

        private void DrawRelatedToolButton(PungentUtilityDescriptor utility, bool compact)
        {
            if (utility == null || !utility.IsAction || utility.RelatedUtilityIds == null || utility.RelatedUtilityIds.Length == 0)
                return;

            PungentUtilityDescriptor related = null;
            for (int i = 0; i < utility.RelatedUtilityIds.Length; i++)
            {
                related = PungentUtilityRegistry.Find(utility.RelatedUtilityIds[i]);
                if (related != null && !related.IsAction)
                    break;
            }

            if (related == null || related.IsAction)
                return;

            using (new EditorGUI.DisabledScope(!IsPrimaryActionEnabled(related)))
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Tool", "Open related utility: " + related.DisplayName), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(compact ? 44f : 50f), GUILayout.Height(24f)))
                    ActivateUtilityPrimaryAction(related);
            }
        }

        private void DrawDeveloperVisibilityControls(PungentUtilityDescriptor utility, bool compact)
        {
            if (!IsDeveloperModeEnabled() || utility == null)
                return;

            float width = compact ? 44f : 58f;

            if (utility.ShowInUtilitiesBrowser && utility.Visibility == PungentUtilityVisibility.Visible)
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Hide", "Hide this item from the default browser without removing its registry entry."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(width), GUILayout.Height(24f)))
                    SetBrowserVisibilityOverride(utility, false, null);
            }
            else
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Show", "Restore this item to the default browser."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(width), GUILayout.Height(24f)))
                    SetBrowserVisibilityOverride(utility, true, PungentUtilityVisibility.Visible);
            }

            if (utility.IsArchived)
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent("Restore", "Restore this archived/legacy item as a visible browser item."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(compact ? 58f : 72f), GUILayout.Height(24f)))
                    SetBrowserVisibilityOverride(utility, true, PungentUtilityVisibility.Visible);
            }
            else if (UtilityWindowTheme.StyledButton(new GUIContent("Archive", "Archive this item so it only appears when Archived/Legacy is enabled."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(compact ? 62f : 72f), GUILayout.Height(24f)))
            {
                SetBrowserVisibilityOverride(utility, false, PungentUtilityVisibility.Archived);
            }
        }

        private void SetBrowserVisibilityOverride(PungentUtilityDescriptor utility, bool showInBrowser, PungentUtilityVisibility? visibility)
        {
            if (utility == null)
                return;

            PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = PungentUtilityRegistry.CreateEditableMetadataOverride(utility.Id);
            if (metadata == null)
                return;

            metadata.overrideShowInUtilitiesBrowser = true;
            metadata.showInUtilitiesBrowser = showInBrowser;
            if (visibility.HasValue)
            {
                metadata.overrideVisibility = true;
                metadata.visibility = visibility.Value;
            }

            if (showInBrowser && visibility == PungentUtilityVisibility.Visible)
            {
                metadata.overrideItemKind = true;
                metadata.itemKind = utility.IsAction ? PungentUtilityItemKind.Action : PungentUtilityItemKind.Utility;
            }

            Undo.RecordObject(PungentUtilityMetadataOverrides.instance, "Set Utility Browser Visibility");
            PungentUtilityRegistry.SaveMetadataOverride(metadata);
            _status = (showInBrowser ? "Showing " : "Hiding ") + utility.DisplayName + ".";
            MarkCacheDirty();
        }

        private void DrawDocumentationButtons(PungentUtilityDescriptor utility, bool compact)
        {
            if (utility == null)
                return;

            List<PungentUtilityDocumentationLinks.DocumentationLink> links = PungentUtilityDocumentationLinks.instance.GetLinksForUtility(utility.Id).ToList();
            bool developerMode = IsDeveloperModeEnabled();

            if (links.Count == 0)
            {
                if (developerMode)
                {
                    if (UtilityWindowTheme.StyledButton(new GUIContent("+ Docs", "Create or link documentation for this utility."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(compact ? 62f : 72f), GUILayout.Height(24f)))
                        DocumentationLinkEditorPopup.CreateForUtility(utility.Id);
                }
                return;
            }

            PungentUtilityDocumentationLinks.DocumentationLink primary = links.FirstOrDefault(link => PungentUtilityDocumentationLinks.GetTargetStatus(link).canOpen) ?? links[0];
            PungentUtilityDocumentationLinks.DocumentationTargetStatus primaryStatus = PungentUtilityDocumentationLinks.GetTargetStatus(primary);
            bool canOpenDirectly = links.Count > 1 || primaryStatus.canOpen;
            string label = links.Count > 1 ? "Docs (" + links.Count + ")" : primaryStatus.canOpen ? "Docs" : "Docs (!)";
            using (new EditorGUI.DisabledScope(!canOpenDirectly))
            {
                if (UtilityWindowTheme.StyledButton(new GUIContent(label, BuildDocumentationButtonTooltip(links, primaryStatus)), UtilityWindowTheme.PungentButtonRole.Link, GUILayout.Width(compact ? 76f : 86f), GUILayout.Height(24f)))
                {
                    if (links.Count == 1)
                    {
                        if (!PungentUtilityDocumentationLinks.instance.Open(links[0], out string error))
                            EditorUtility.DisplayDialog("Documentation Link", error, "OK");
                    }
                    else
                    {
                        GenericMenu menu = new GenericMenu();
                        foreach (PungentUtilityDocumentationLinks.DocumentationLink link in links)
                        {
                            PungentUtilityDocumentationLinks.DocumentationLink captured = link;
                            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
                            string itemLabel = PungentUtilityDocumentationLinks.GetDisplayName(link) + " [" + status.kindLabel + "]";
                            if (status.canOpen)
                            {
                                menu.AddItem(new GUIContent(itemLabel), false, () =>
                                {
                                    if (!PungentUtilityDocumentationLinks.instance.Open(captured, out string error))
                                        EditorUtility.DisplayDialog("Documentation Link", error, "OK");
                                });
                            }
                            else
                            {
                                menu.AddDisabledItem(new GUIContent(itemLabel + " - " + status.message));
                            }
                        }
                        if (developerMode)
                        {
                            menu.AddSeparator(string.Empty);
                            menu.AddItem(new GUIContent("Manage Documentation..."), false, () => DocumentationLinkEditorPopup.ManageForUtility(utility.Id));
                        }
                        menu.ShowAsContext();
                    }
                }
            }

            if (developerMode && UtilityWindowTheme.StyledButton(new GUIContent("Link", "Manage documentation assignments for this utility."), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.Width(48f), GUILayout.Height(24f)))
                DocumentationLinkEditorPopup.ManageForUtility(utility.Id);
        }

        private static string BuildDocumentationButtonTooltip(List<PungentUtilityDocumentationLinks.DocumentationLink> links, PungentUtilityDocumentationLinks.DocumentationTargetStatus primaryStatus)
        {
            if (links == null || links.Count == 0)
                return "No documentation links are assigned.";

            if (links.Count == 1)
                return "Open current documentation.\nTarget: " + primaryStatus.kindLabel + "\n" + primaryStatus.message;

            int openable = links.Count(link => PungentUtilityDocumentationLinks.GetTargetStatus(link).canOpen);
            return "Choose a documentation target.\n" + openable + "/" + links.Count + " openable targets. Primary target: " + primaryStatus.kindLabel + ".";
        }

        private void DrawMetadataEditButton(PungentUtilityDescriptor utility, bool compact)
        {
            if (!IsDeveloperModeEnabled() || utility == null)
                return;

            if (UtilityWindowTheme.StyledButton(new GUIContent("Edit", "Open the unified metadata workbench for this utility."), UtilityWindowTheme.PungentButtonRole.Secondary, GUILayout.Width(compact ? 48f : 56f), GUILayout.Height(24f)))
                PungentUtilityDeveloperToolsWindow.OpenMetadata(utility.Id);
        }

        private void DrawDeveloperFilters()
        {
            if (!IsDeveloperModeEnabled())
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFilterToggle("Show Hidden/Internal", ref _showHiddenInternal, "Include registered items hidden from the default browser and developer-only/internal tools.", 138f);
                DrawFilterToggle("Show Archived", ref _showArchived, "Include archived or legacy alias registry entries.", 112f);
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFilterToggle("Missing Docs", ref _filterMissingDocs, "Show utilities without assigned current documentation.");
                DrawFilterToggle("Has Docs", ref _filterHasDocs, "Show utilities with assigned current documentation.");
                DrawFilterToggle("Experimental", ref _filterExperimental, "Show Experimental utilities.");
                DrawFilterToggle("In Progress", ref _filterInProgress, "Show In Progress utilities.");
                DrawFilterToggle("No Facets", ref _filterNoFacets, "Show utilities without category facets.");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawFilterToggle(string label, ref bool value, string tooltip, float width = 92f)
        {
            if (!UtilityWindowTheme.ToolbarButton(new GUIContent(label, tooltip), value, value ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral, GUILayout.Width(width), GUILayout.Height(22f)))
                return;

            value = !value;
            MarkCacheDirty(BrowserDirtyFlags.Filters);
            SavePrefs();
        }

        private bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(_search) ||
                   _category != AllFilter ||
                   _type != AllFilter ||
                   _statusFilter != AllFilter ||
                   _packageAvailabilityFilter != PackageAvailabilityFilter.AllUtilities ||
                   _itemKindFilter != BrowserItemKindFilter.UtilitiesAndActions ||
                   _includeAccessories ||
                   _showNestedActions ||
                   !string.IsNullOrWhiteSpace(_focusedPackageId) ||
                   (IsDeveloperModeEnabled() && (_showHiddenInternal || _showArchived || _filterMissingDocs || _filterHasDocs || _filterExperimental || _filterInProgress || _filterNoFacets));
        }

        private void ResetBrowserFilters()
        {
            _search = string.Empty;
            _focusedPackageId = string.Empty;
            _category = AllFilter;
            _type = AllFilter;
            _statusFilter = AllFilter;
            _packageAvailabilityFilter = PackageAvailabilityFilter.AllUtilities;
            _itemKindFilter = BrowserItemKindFilter.UtilitiesAndActions;
            _includeAccessories = false;
            _showNestedActions = false;
            _showHiddenInternal = false;
            _showArchived = false;
            _filterMissingDocs = false;
            _filterHasDocs = false;
            _filterExperimental = false;
            _filterInProgress = false;
            _filterNoFacets = false;
            MarkCacheDirty(BrowserDirtyFlags.Filters | BrowserDirtyFlags.Query);
            SavePrefs();
        }

        private void DrawDocsCoveragePill()
        {
            if (!IsDeveloperModeEnabled())
                return;

            int total = CountSourceItemsForHeader();
            int linked = GetCurrentSourceItems().Count(u => PungentUtilityDocumentationLinks.instance.HasLinksForUtility(u.Id));
            UtilityWindowTheme.CountPill("Docs: " + linked + "/" + total, linked == total ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
        }

        private static bool IsDeveloperModeEnabled()
        {
            return PungentDeveloperMode.Available && PungentDeveloperMode.Enabled;
        }

        private void DrawRelatedUtilities(PungentUtilityDescriptor utility)
        {
            if (utility == null || !utility.HasRelatedUtilities)
                return;

            List<PungentUtilityDescriptor> related = PungentUtilityRegistry.RelatedUtilities(utility).Take(3).ToList();
            if (related.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Related", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(48f));
                for (int i = 0; i < related.Count; i++)
                {
                    PungentUtilityDescriptor item = related[i];
                    using (new EditorGUI.DisabledScope(!IsPrimaryActionEnabled(item)))
                    {
                        if (UtilityWindowTheme.StyledButton(new GUIContent(item.DisplayName, BuildUtilityTooltip(item)), UtilityWindowTheme.PungentButtonRole.Subtle, GUILayout.MaxWidth(132f), GUILayout.Height(22f)))
                            ActivateUtilityPrimaryAction(item);
                    }
                }
                GUILayout.FlexibleSpace();
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

        private void ActivateUtilityPrimaryAction(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;

            switch (PungentUtilityPackageCatalog.ResolveAvailability(utility))
            {
                case PungentUtilityPackageAvailability.Unowned:
                    if (PungentUtilityPackageCatalog.TryOpenAssetStore(utility, out string assetStoreMessage))
                        _status = assetStoreMessage;
                    else
                    {
                        _status = assetStoreMessage;
                        OpenPackageDetailsInline(utility, assetStoreMessage);
                    }
                    return;

                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    if (PungentUtilityPackageCatalog.OpenImportPackage(utility, out string importMessage))
                        _status = importMessage;
                    else
                    {
                        _status = importMessage;
                        OpenPackageDetailsInline(utility, importMessage);
                    }
                    return;

                case PungentUtilityPackageAvailability.OwnedInstalled:
                default:
                    OpenUtility(utility);
                    return;
            }
        }

        private void OpenPackageInstallFlow(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;

            if (PungentUtilityPackageCatalog.OpenInstallFlow(utility, out string message))
            {
                _status = message;
                return;
            }

            OpenPackageDetailsInline(utility, message);
            _status = message;
        }

        private void OpenPackageInstallFlow(PungentUtilityPackageRecord record)
        {
            if (record == null)
                return;

            if (PungentUtilityPackageCatalog.OpenInstallFlow(record, out string message))
            {
                _status = message;
                return;
            }

            OpenPackageDetailsInline(record, null, message);
            _status = message;
        }

        private void OpenPackageDetailsInline(PungentUtilityDescriptor utility, string message)
        {
            if (utility == null)
                return;

            PungentUtilityPackageRecord record = PungentUtilityPackageCatalog.FindRecord(utility.NormalizedPackageId) ??
                                                 PungentUtilityPackageCatalog.CreateEditableRecord(utility.NormalizedPackageId);
            OpenPackageDetailsInline(record, utility, message);
        }

        private void OpenPackageDetailsInline(PungentUtilityPackageRecord record, PungentUtilityDescriptor utility, string message)
        {
            if (record == null)
                return;

            ShowPackageDetailsInline(record, utility, message);
        }

        private void ClosePackageDetails()
        {
            _expandedPackageDetails.Clear();
            _packageDetailRecord = null;
            _packageDetailUtility = null;
            _packageDetailMessage = string.Empty;
            Repaint();
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
            _status = (utility.IsAction ? "Ran " : "Opened ") + utility.DisplayName + ".";
            MarkCacheDirty();
            SavePrefs();
        }

        private void ToggleFavorite(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (!_favorites.Add(id))
                _favorites.Remove(id);

            MarkCacheDirty();
            SavePrefs();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefTab, _selectedTab.ToString());
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetString(PrefPackageSearch, _packageSearch);
            UtilityWindowPrefs.SetString(PrefPackageFocus, _focusedPackageId);
            UtilityWindowPrefs.SetString(PrefOtherPackageFocus, EncodeList(_focusedOtherPackageIds));
            UtilityWindowPrefs.SetString(PrefCategory, _category);
            UtilityWindowPrefs.SetString(PrefType, _type);
            UtilityWindowPrefs.SetString(PrefStatus, _statusFilter);
            UtilityWindowPrefs.SetString(PrefFavorites, EncodeList(_favorites));
            UtilityWindowPrefs.SetString(PrefRecents, EncodeList(_recents));
            UtilityWindowPrefs.SetString(PrefViewMode, _viewMode.ToString());
            UtilityWindowPrefs.SetString(PrefItemKindFilter, _itemKindFilter.ToString());
            UtilityWindowPrefs.SetString(PrefSortMode, _sortMode.ToString());
            UtilityWindowPrefs.SetString(PrefSelectedUtilityId, _selectedUtilityId ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefPackageAvailabilityFilter, _packageAvailabilityFilter.ToString());
            UtilityWindowPrefs.SetString(PrefPackageCardPreviewMode, _packageCardPreviewMode.ToString());
            UtilityWindowPrefs.SetBool(PrefShowFilters, _showFilters);
            UtilityWindowPrefs.SetBool(PrefShowFavorites, _showFavorites);
            UtilityWindowPrefs.SetBool(PrefShowHistory, _showHistory);
            UtilityWindowPrefs.SetBool(PrefShowSettings, _showSettings);
            UtilityWindowPrefs.SetBool(PrefShowRelated, _showRelated);
            UtilityWindowPrefs.SetBool(PrefShowCapabilityTags, _showCapabilityTags);
            UtilityWindowPrefs.SetBool(PrefGroupByType, _groupByType);
            UtilityWindowPrefs.SetBool(PrefIncludeAccessories, _includeAccessories);
            UtilityWindowPrefs.SetBool(PrefShowNestedActions, _showNestedActions);
            UtilityWindowPrefs.SetBool(PrefDrawerActions, _drawerActions);
            UtilityWindowPrefs.SetBool(PrefDrawerAccessories, _drawerAccessories);
            UtilityWindowPrefs.SetBool(PrefDrawerRelated, _drawerRelated);
            UtilityWindowPrefs.SetBool(PrefDrawerHelp, _drawerHelp);
            UtilityWindowPrefs.SetBool(PrefDrawerPackage, _drawerPackage);
            UtilityWindowPrefs.SetBool(PrefFilterMissingDocs, _filterMissingDocs);
            UtilityWindowPrefs.SetBool(PrefFilterHasDocs, _filterHasDocs);
            UtilityWindowPrefs.SetBool(PrefFilterExperimental, _filterExperimental);
            UtilityWindowPrefs.SetBool(PrefFilterInProgress, _filterInProgress);
            UtilityWindowPrefs.SetBool(PrefFilterNoFacets, _filterNoFacets);
            UtilityWindowPrefs.SetBool(PrefShowHiddenInternal, _showHiddenInternal);
            UtilityWindowPrefs.SetBool(PrefShowArchived, _showArchived);
            UtilityWindowPrefs.SetFloat(PrefSidebarWidth, _sidebarWidth);
        }

        private static BrowserViewMode LoadViewMode()
        {
            string stored = UtilityWindowPrefs.GetString(PrefViewMode, BrowserViewMode.Compact.ToString());
            if (string.Equals(stored, "Grid", StringComparison.OrdinalIgnoreCase))
                return BrowserViewMode.Compact;

            if (Enum.TryParse(stored, true, out BrowserViewMode mode))
                return mode;

            return BrowserViewMode.Compact;
        }

        private static BrowserTab LoadBrowserTab()
        {
            string stored = UtilityWindowPrefs.GetString(PrefTab, BrowserTab.Browse.ToString());
            if (string.Equals(stored, "Packages", StringComparison.OrdinalIgnoreCase))
                return BrowserTab.OtherPackages;
            if (Enum.TryParse(stored, true, out BrowserTab tab))
                return tab;

            return BrowserTab.Browse;
        }


        private static BrowserItemKindFilter LoadItemKindFilter()
        {
            string stored = UtilityWindowPrefs.GetString(PrefItemKindFilter, BrowserItemKindFilter.UtilitiesAndActions.ToString());
            if (Enum.TryParse(stored, true, out BrowserItemKindFilter mode))
                return mode;

            return BrowserItemKindFilter.UtilitiesAndActions;
        }

        private static PungentUtilityBrowserSortMode LoadSortMode()
        {
            string stored = UtilityWindowPrefs.GetString(PrefSortMode, PungentUtilityBrowserSortMode.Recommended.ToString());
            if (Enum.TryParse(stored, true, out PungentUtilityBrowserSortMode mode))
                return mode;

            return PungentUtilityBrowserSortMode.Recommended;
        }

        private static PackageAvailabilityFilter LoadPackageAvailabilityFilter()
        {
            string stored = UtilityWindowPrefs.GetString(PrefPackageAvailabilityFilter, PackageAvailabilityFilter.AllUtilities.ToString());
            if (string.Equals(stored, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stored, "Unowned", StringComparison.OrdinalIgnoreCase))
                return PackageAvailabilityFilter.AllUtilities;
            if (string.Equals(stored, "OwnedNotInstalled", StringComparison.OrdinalIgnoreCase))
                return PackageAvailabilityFilter.OwnedUtilities;
            if (string.Equals(stored, "OwnedInstalled", StringComparison.OrdinalIgnoreCase))
                return PackageAvailabilityFilter.InstalledUtilities;

            if (Enum.TryParse(stored, true, out PackageAvailabilityFilter filter))
                return filter;

            return PackageAvailabilityFilter.AllUtilities;
        }

        private static PackageCardPreviewMode LoadPackageCardPreviewMode()
        {
            string stored = UtilityWindowPrefs.GetString(PrefPackageCardPreviewMode, PackageCardPreviewMode.ActualState.ToString());
            if (Enum.TryParse(stored, true, out PackageCardPreviewMode mode))
                return mode;

            return PackageCardPreviewMode.ActualState;
        }

        private static string BuildUtilityTooltip(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return string.Empty;

            string disabled = utility.CanRun ? string.Empty : "\n\nUnavailable: " + utility.DisabledReason;
            string packageInfo = "\nPackage: " + utility.EffectivePackageDisplayName +
                                 "\nPackage ID: " + utility.NormalizedPackageId +
                                 "\nAvailability: " + PungentUtilityPackageCatalog.BuildAvailabilityLabel(PungentUtilityPackageCatalog.ResolveAvailability(utility)) +
                                 (string.IsNullOrWhiteSpace(utility.InstallHint) ? string.Empty : "\nInstall hint: " + utility.InstallHint);
            return utility.DisplayName + "\n" +
                   utility.ItemKind + " · " + utility.Visibility + "\n" +
                   PungentUtilityCategories.GetDisplayName(PungentUtilityRegistry.GetAreaCategory(utility)) + " / " + PungentUtilityRegistry.GetUtilityType(utility) + "\n" +
                   PungentUtilityRegistry.GetStatus(utility) + ": " + PungentUtilityPackageStatus.GetDescription(PungentUtilityRegistry.GetStatus(utility)) + "\n\n" +
                   utility.Description + "\n" +
                   packageInfo + disabled;
        }

        private string BuildEmptyStateMessage()
        {
            if (!string.IsNullOrWhiteSpace(_focusedPackageId))
                return "No utilities or actions are currently shown for " + PungentUtilityPackageCatalog.GetDisplayName(_focusedPackageId) + ". Clear package focus to return to all utilities.";
            if (_packageAvailabilityFilter != PackageAvailabilityFilter.AllUtilities)
                return "No utilities or actions match " + GetPackageAvailabilityFilterLabel(_packageAvailabilityFilter).ToLowerInvariant() + " and the current search/category/type/status filters. Use Show All to return to every visible utility.";
            if (IsDeveloperModeEnabled() && _filterMissingDocs)
                return "No missing documentation matches the current search and filters.";
            if (IsDeveloperModeEnabled() && !_showHiddenInternal && !_showArchived)
                return "No visible items match the current filters. Enable Show Hidden/Internal or Show Archived in developer filters to inspect hidden registry entries.";
            if (_category != AllFilter && string.IsNullOrWhiteSpace(_search))
                return "No utilities or actions are currently shown in " + PungentUtilityCategories.GetDisplayName(_category) + ".";
            return "No utilities or actions match the current search or filters.";
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

        private void MarkCacheDirty()
        {
            MarkCacheDirty(BrowserDirtyFlags.All);
        }

        private void MarkCacheDirty(BrowserDirtyFlags flags)
        {
            _cacheDirty = true;
            _cacheDirtyFlags |= flags == BrowserDirtyFlags.None ? BrowserDirtyFlags.Query : flags;
            _browserViewModel.MarkDirty(flags);
            _canRunCache.Clear();
            PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(this, ref _nextAllowedRepaintTime, 0.10d);
        }

        private bool GetCachedCanRun(PungentUtilityDescriptor utility)
        {
            if (utility == null || string.IsNullOrWhiteSpace(utility.Id))
                return false;

            if (_canRunCache.TryGetValue(utility.Id, out bool cached))
                return cached;

            cached = utility.CanRun;
            _canRunCache[utility.Id] = cached;
            return cached;
        }


        private bool ShouldIncludeHiddenSource()
        {
            return IsDeveloperModeEnabled() && (_showHiddenInternal || _showArchived);
        }

        private IEnumerable<PungentUtilityDescriptor> GetCurrentSourceItems()
        {
            return PungentUtilityRegistry.Query(includeHidden: ShouldIncludeHiddenSource())
                .Where(PassesItemKindFilter)
                .Where(PassesVisibilityFilter);
        }

        private int CountSourceItemsForHeader()
        {
            return _cachedSourceCount;
        }

        private string GetBrowserSectionTitle()
        {
            switch (_itemKindFilter)
            {
                case BrowserItemKindFilter.Actions:
                    return "Actions";
                case BrowserItemKindFilter.Utilities:
                    return "Utilities";
                default:
                    return _showNestedActions ? "Utilities + Actions" : "Core Utilities";
            }
        }

        private void ApplyItemKindFilter(List<PungentUtilityDescriptor> utilities)
        {
            if (utilities == null)
                return;

            utilities.RemoveAll(u => !PassesItemKindFilter(u));
        }

        private bool PassesItemKindFilter(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return false;

            switch (_itemKindFilter)
            {
                case BrowserItemKindFilter.Actions:
                    return utility.IsAction;
                case BrowserItemKindFilter.Utilities:
                    return !utility.IsAction;
                default:
                    return true;
            }
        }

        private void ApplyVisibilityFilter(List<PungentUtilityDescriptor> utilities)
        {
            if (utilities == null)
                return;

            utilities.RemoveAll(u => !PassesVisibilityFilter(u));
        }

        private bool PassesVisibilityFilter(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return false;

            if (utility.IsVisibleInDefaultBrowser)
                return true;

            if (!IsDeveloperModeEnabled())
                return false;

            if (utility.IsArchived)
                return _showArchived;

            if (utility.IsHiddenOrInternal)
                return _showHiddenInternal;

            return false;
        }

        private void RefreshCachesIfNeeded()
        {
            if (!_cacheDirty)
                return;

            _cacheDirty = false;
            using (PungentEditorPerformanceUtility.Sample("PFU.Browser.RefreshCachesIfNeeded"))
            {
                bool includeHidden = ShouldIncludeHiddenSource();
                List<PungentUtilityDescriptor> source = PungentUtilityRegistry.Query(includeHidden: includeHidden).ToList();
                _cachedSourceCount = source.Count(PassesVisibilityFilter);
                _cachedCategories = new[] { AllFilter }.Concat(source
                    .SelectMany(u => u.GetAllCategoryIds())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Where(c => includeHidden || PungentUtilityCategoryOverrides.instance.IsVisible(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(PungentUtilityCategories.SortKey)
                    .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)).ToArray();

                if (_category != AllFilter && Array.IndexOf(_cachedCategories, _category) < 0)
                    _category = AllFilter;

                string categoryFilter = _category == AllFilter ? null : _category;
                _cachedTypes = new[] { AllFilter }.Concat(source.Where(u => string.IsNullOrEmpty(categoryFilter) || u.HasCategory(categoryFilter)).Select(PungentUtilityRegistry.GetUtilityType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase)).ToArray();
                _cachedStatuses = new[] { AllFilter }.Concat(source.Where(u => string.IsNullOrEmpty(categoryFilter) || u.HasCategory(categoryFilter)).Select(PungentUtilityRegistry.GetStatus).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(PungentUtilityPackageStatus.SortKey).ThenBy(s => s, StringComparer.OrdinalIgnoreCase)).ToArray();

                if (_type != AllFilter && Array.IndexOf(_cachedTypes, _type) < 0)
                    _type = AllFilter;
                if (_statusFilter != AllFilter && Array.IndexOf(_cachedStatuses, _statusFilter) < 0)
                    _statusFilter = AllFilter;

                _browserViewModel.MarkDirty(_cacheDirtyFlags);
                _browserViewModel.RefreshIfNeeded(
                    source,
                    _search,
                    _category,
                    _type,
                    _statusFilter,
                    _focusedPackageId,
                    PassesVisibilityFilter,
                    PassesDeveloperFilters,
                    PassesPackageAvailabilityFilter,
                    PassesItemKindFilter,
                    _includeAccessories,
                    _showNestedActions || _itemKindFilter == BrowserItemKindFilter.Actions,
                    IsDeveloperModeEnabled() && (_showHiddenInternal || _showArchived),
                    _sortMode,
                    _favorites,
                    _recents);

                _cachedQuery.Clear();
                _cachedQuery.AddRange(_browserViewModel.VisibleDescriptors);
                _cachedCategoryGroups.Clear();
                foreach (PungentUtilityBrowserViewModel.CategoryGroup group in _browserViewModel.CategoryGroups)
                {
                    CategoryGroupView categoryView = new CategoryGroupView
                    {
                        Category = group.Category,
                        Count = group.Count
                    };

                    foreach (PungentUtilityBrowserViewModel.TypeGroup typeGroup in group.Types)
                    {
                        TypeGroupView view = new TypeGroupView
                        {
                            Name = typeGroup.Name
                        };
                        view.Utilities.AddRange(typeGroup.Utilities);
                        categoryView.Types.Add(view);
                    }

                    _cachedCategoryGroups.Add(categoryView);
                }

                _cacheDirtyFlags = BrowserDirtyFlags.None;
            }
        }

        private void ApplyDeveloperFilters(List<PungentUtilityDescriptor> utilities)
        {
            if (!IsDeveloperModeEnabled() || utilities == null)
                return;

            utilities.RemoveAll(u => !PassesDeveloperFilters(u));
        }

        private bool PassesDeveloperFilters(PungentUtilityDescriptor utility)
        {
            if (!IsDeveloperModeEnabled() || utility == null)
                return utility != null;

            if (_filterMissingDocs && PungentUtilityDocumentationLinks.instance.HasLinksForUtility(utility.Id))
                return false;
            if (_filterHasDocs && !PungentUtilityDocumentationLinks.instance.HasLinksForUtility(utility.Id))
                return false;
            if (_filterExperimental && !string.Equals(PungentUtilityRegistry.GetStatus(utility), PungentUtilityPackageStatus.Experimental, StringComparison.OrdinalIgnoreCase))
                return false;
            if (_filterInProgress && !string.Equals(PungentUtilityRegistry.GetStatus(utility), PungentUtilityPackageStatus.InProgress, StringComparison.OrdinalIgnoreCase))
                return false;
            if (_filterNoFacets && utility.HasCategoryFacets)
                return false;

            return true;
        }

        private void ApplyPackageAvailabilityFilter(List<PungentUtilityDescriptor> utilities)
        {
            if (utilities == null || _packageAvailabilityFilter == PackageAvailabilityFilter.AllUtilities)
                return;

            utilities.RemoveAll(u => !PassesPackageAvailabilityFilter(u));
        }

        private void ApplyPackageFocusFilter(List<PungentUtilityDescriptor> utilities)
        {
            if (utilities == null || string.IsNullOrWhiteSpace(_focusedPackageId))
                return;

            string packageId = PungentUtilityPackageCatalog.NormalizePackageId(_focusedPackageId);
            utilities.RemoveAll(u => u == null || !string.Equals(u.NormalizedPackageId, packageId, StringComparison.OrdinalIgnoreCase));
        }
    }
#endif
}
