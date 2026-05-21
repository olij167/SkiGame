using PungentFunk.Utilities.Editor.Developer;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.Authoring;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Unified Developer Mode workbench for package, utility, category, and generated-help maintenance.
    /// Mutations are project-local and remain gated by PungentDeveloperMode.
    /// </summary>
    public sealed class PungentUtilityDeveloperToolsWindow : EditorWindow
    {
        private enum Tab
        {
            Overview,
            Metadata,
            Generation,
            Audit
        }

        private enum MetadataMode
        {
            PackageUnit,
            Category,
            FieldFocus,
            Utility
        }

        private enum MetadataField
        {
            DisplayName,
            UtilityType,
            AreaCategory,
            Module,
            Description,
            MenuPath,
            Tags,
            SortOrder,
            PackageStatus,
            PackageId,
            PackageDisplayName,
            PackageTier,
            AssetStoreUrl,
            PackageManagerId,
            ImportPackagePath,
            ProvidedCapabilities,
            RequiredPackages,
            OptionalPackages,
            RelatedUtilities,
            CategoryFacets,
            DocumentationTopicId,
            ItemKind,
            Visibility,
            ShowInUtilitiesBrowser,
            MissingDependencyMessage,
            InstallHint,
            SupportsSceneOverlay,
            SupportsContextMenu,
            SupportsSelection,
            IsLabHub,
            BrowserRole,
            BrowserPriority,
            ParentUtilityId,
            BrowserProminence,
            BrowserCardTint,
            BrowserTags,
            AccessoryUtilities,
            ActionIds
        }

        private enum PackageUnitLayout
        {
            Narrow,
            Medium,
            Wide
        }

        private const string PrefPrefix = "PungentFunkUtilities.DeveloperTools.";
        private const string PrefTab = PrefPrefix + "Tab";
        private const string PrefMode = PrefPrefix + "Mode";
        private const string PrefField = PrefPrefix + "Field";
        private const string PrefSelectedUtility = PrefPrefix + "SelectedUtility";
        private const string PrefSelectedPackage = PrefPrefix + "SelectedPackage";
        private const string PrefSelectedCategory = PrefPrefix + "SelectedCategory";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefPackageSearch = PrefPrefix + "PackageSearch";
        private const string PrefCategorySearch = PrefPrefix + "CategorySearch";
        private const string PrefIncludeHidden = PrefPrefix + "IncludeHidden";
        private const string PrefOnlyOverrides = PrefPrefix + "OnlyOverrides";
        private const string PrefFilterMissingDocs = PrefPrefix + "FilterMissingDocs";
        private const string PrefFilterNoCardTint = PrefPrefix + "FilterNoCardTint";
        private const string PrefFilterNoFacets = PrefPrefix + "FilterNoFacets";
        private const string PrefShowMetadataAncillary = PrefPrefix + "ShowMetadataAncillary";
        private const string PrefShowMetadataDocLinks = PrefPrefix + "ShowMetadataDocLinks";
        private const string PrefShowMetadataExternalLinks = PrefPrefix + "ShowMetadataExternalLinks";
        private const string PrefShowMetadataWelcome = PrefPrefix + "ShowMetadataWelcome";
        private const string PrefReviewQueue = PrefPrefix + "ReviewQueue";
        private const string PrefDocLinkSearch = PrefPrefix + "DocLinkSearch";
        private const float PackageUnitNarrowWidth = 940f;
        private const float PackageUnitWideWidth = 1180f;
        private const float PackageListMinWidth = 240f;
        private const float PackageListMaxWidth = 330f;
        private const float PackageRecordMinWidth = 300f;
        private const float PackageRecordMaxWidth = 460f;
        private const float UtilityListMinWidth = 210f;
        private const float UtilityListMaxWidth = 300f;
        private const float OverrideToggleWidth = 24f;
        private const float MetadataLabelMinWidth = 92f;
        private const float MetadataLabelMaxWidth = 116f;
        private const float PackageRecordLabelWidth = 108f;
        private const float PackageUnitRowGap = 4f;

        private Tab _tab = Tab.Metadata;
        private MetadataMode _mode = MetadataMode.PackageUnit;
        private MetadataField _field = MetadataField.CategoryFacets;
        private string _selectedUtilityId = "utilities-browser";
        private string _selectedPackageId = PungentUtilityPackageCatalog.CorePackageId;
        private string _selectedCategoryId = PungentUtilityCategories.Core;
        private string _search = string.Empty;
        private string _packageSearch = string.Empty;
        private string _categorySearch = string.Empty;
        private bool _includeHidden;
        private bool _onlyOverrides;
        private bool _filterMissingDocs;
        private bool _filterNoCardTint;
        private bool _filterNoFacets;
        private bool _packageAuditIncludeInfo = true;
        private bool _showMetadataAncillaryEditors;
        private bool _showMetadataDocLinks = true;
        private bool _showMetadataExternalLinks = true;
        private bool _showMetadataWelcomeSections = true;
        private string _docLinkSearch = string.Empty;
        private string _newPackageId = "com.pungentfunk.utilities.new-package";
        private string _duplicatePackageId = string.Empty;
        private string _newCategoryId = "custom-category";
        private string _duplicateCategoryId = string.Empty;
        private string _status = "Ready.";

        private Vector2 _overviewScroll;
        private Vector2 _metadataScroll;
        private Vector2 _packageListScroll;
        private Vector2 _packageDetailsScroll;
        private Vector2 _utilityListScroll;
        private Vector2 _categoryListScroll;
        private Vector2 _categoryDetailsScroll;
        private Vector2 _fieldScroll;
        private Vector2 _generationScroll;
        private Vector2 _reviewQueueScroll;
        private Vector2 _documentationLinksScroll;
        private Vector2 _metadataAncillaryScroll;
        private Vector2 _auditScroll;
        private PungentUtilityHelpReviewQueueKind _reviewQueue = PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics;
        private PungentUtilityDesignAudit.Report _packageAuditReport;
        private PungentUtilityHelpGenerationReport _generationReport;
        private List<PungentUtilityHelpReviewRow> _generationReviewRows = new List<PungentUtilityHelpReviewRow>();
        private bool _generationReportDirty = true;
        private List<DeveloperSurfaceScanRow> _developerSurfaceRows = new List<DeveloperSurfaceScanRow>();
        private DateTime _developerSurfaceScanUtc;
        private string _documentationTestDataSearch = string.Empty;
        private readonly HashSet<string> _documentationTestDataSelectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _metadataCheckedUtilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, PungentUtilityMetadataOverrides.UtilityMetadataOverride> _metadataDrafts =
            new Dictionary<string, PungentUtilityMetadataOverrides.UtilityMetadataOverride>(StringComparer.OrdinalIgnoreCase);

        private PungentUtilityPackageRecord _packageDraft;
        private PungentUtilityCategoryOverrides.CategoryOverride _categoryDraft;
        private string _packageUtilityIdsText = string.Empty;
        private string _packageCapabilitiesText = string.Empty;

        private sealed class DeveloperSurfaceScanRow
        {
            public string path;
            public int line;
            public string marker;
            public string classification;
            public string note;
        }

        public static void Open()
        {
            PungentUtilityDeveloperToolsWindow window = GetWindow<PungentUtilityDeveloperToolsWindow>("Developer Tools");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        public static void OpenMetadata(string utilityId = null)
        {
            Open();
            PungentUtilityDeveloperToolsWindow window = GetWindow<PungentUtilityDeveloperToolsWindow>("Developer Tools");
            window._tab = Tab.Metadata;
            window._mode = MetadataMode.PackageUnit;
            if (!string.IsNullOrWhiteSpace(utilityId))
                window.SelectUtility(utilityId);
            window.SavePrefs();
            window.Repaint();
        }

        public static void OpenCategory(string categoryId)
        {
            Open();
            PungentUtilityDeveloperToolsWindow window = GetWindow<PungentUtilityDeveloperToolsWindow>("Developer Tools");
            window._tab = Tab.Metadata;
            window._mode = MetadataMode.Category;
            window._selectedCategoryId = PungentUtilityCategories.Normalize(categoryId);
            window.SavePrefs();
            window.Repaint();
        }

        public static void OpenGeneration()
        {
            Open();
            PungentUtilityDeveloperToolsWindow window = GetWindow<PungentUtilityDeveloperToolsWindow>("Developer Tools");
            window._tab = Tab.Generation;
            window.SavePrefs();
            window.Repaint();
        }

        public static void OpenAudit()
        {
            Open();
            PungentUtilityDeveloperToolsWindow window = GetWindow<PungentUtilityDeveloperToolsWindow>("Developer Tools");
            window._tab = Tab.Audit;
            window.SavePrefs();
            window.Repaint();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Developer Tools");
            LoadPrefs();
            PungentUtilityRegistry.Changed -= OnSourcesChanged;
            PungentUtilityRegistry.Changed += OnSourcesChanged;
            PungentUtilityPackageCatalog.Changed -= OnSourcesChanged;
            PungentUtilityPackageCatalog.Changed += OnSourcesChanged;
            PungentUtilityCategoryOverrides.Changed -= OnSourcesChanged;
            PungentUtilityCategoryOverrides.Changed += OnSourcesChanged;
            PungentUtilityHelpRegistry.Changed -= OnSourcesChanged;
            PungentUtilityHelpRegistry.Changed += OnSourcesChanged;
            EnsurePackageDraft();
        }

        private void OnDisable()
        {
            SavePrefs();
            PungentUtilityRegistry.Changed -= OnSourcesChanged;
            PungentUtilityPackageCatalog.Changed -= OnSourcesChanged;
            PungentUtilityCategoryOverrides.Changed -= OnSourcesChanged;
            PungentUtilityHelpRegistry.Changed -= OnSourcesChanged;
        }

        private void OnSourcesChanged()
        {
            _metadataDrafts.Clear();
            _packageDraft = null;
            _generationReportDirty = true;
            EnsurePackageDraft();
            Repaint();
        }

        private void OnGUI()
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
            {
                UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions { UtilityId = "developer-tools", Title = "Developer Tools", Description = "Unified metadata, package, category, and generated-help workspace.", Status = "Developer Mode is disabled.", ShowHelp = true, ShowMinimizeTray = true, ShowMinimizeButton = true, Tint = UtilityWindowTheme.HeaderTint });
                EditorGUILayout.HelpBox("Developer controls are gated at the Utilities Browser root. Enable Developer Tools from Utilities Browser > Browser Settings > Developer Tools.", MessageType.Warning);
                if (GUILayout.Button(new GUIContent("Open Utilities Browser", "Open the root location for enabling Developer Tools."), GUILayout.Width(170f), GUILayout.Height(26f)))
                    PungentUtilityControlPanelWindow.Open();
                return;
            }

            string gate = PungentDeveloperMode.CanEditProjectMetadata ? "Project metadata edits enabled." : "Metadata edits locked while Unity is compiling or updating.";
            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions { UtilityId = "developer-tools", Title = "Developer Tools", Description = "Central workbench for suite developer tooling.", Status = _status + " " + gate, CompactStatus = gate, ShowHelp = true, ShowMinimizeTray = true, ShowMinimizeButton = true, Tint = UtilityWindowTheme.HeaderTint });
            DrawTabs();

            switch (_tab)
            {
                case Tab.Overview:
                    DrawOverview();
                    break;
                case Tab.Generation:
                    DrawGeneration();
                    break;
                case Tab.Audit:
                    DrawAudit();
                    break;
                default:
                    DrawMetadata();
                    break;
            }
        }

        private void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.05f, 5, 4)))
            {
                DrawTabButton(Tab.Overview, "Overview", 96f, "Map developer-only surfaces and migration targets.");
                DrawTabButton(Tab.Metadata, "Metadata", 104f, "Edit package, utility, and category metadata.");
                DrawTabButton(Tab.Generation, "Generation", 116f, "Run generated-help maintenance actions.");
                DrawTabButton(Tab.Audit, "Audit", 86f, "Run package-readiness developer diagnostics.");
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill("Developer Mode", UtilityWindowTheme.Amber, 118f);
            }
        }

        private void DrawTabButton(Tab tab, string label, float width, string tooltip)
        {
            if (GUILayout.Toggle(_tab == tab, new GUIContent(label, tooltip), EditorStyles.miniButton, GUILayout.Width(width), GUILayout.Height(24f)) && _tab != tab)
            {
                _tab = tab;
                SavePrefs();
            }
        }

        private void DrawOverview()
        {
            _overviewScroll = EditorGUILayout.BeginScrollView(_overviewScroll);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Developer Tools Workbench", UtilityWindowTheme.Blue, "single developer surface");
                EditorGUILayout.LabelField("Registry metadata, package catalogue records, category membership, generated-help maintenance, and package diagnostics now live here.", UtilityWindowTheme.BodyStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(PungentDeveloperMode.Enabled ? "Mode On" : "Mode Off", PungentDeveloperMode.Enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 90f);
                    UtilityWindowTheme.CountPill(PungentDeveloperMode.CanEditProjectMetadata ? "Metadata Writable" : "Metadata Locked", PungentDeveloperMode.CanEditProjectMetadata ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 136f);
                    UtilityWindowTheme.CountPill(PungentDeveloperMode.CanEditPresetSources ? "Source Writes On" : "Source Writes Off", PungentDeveloperMode.CanEditPresetSources ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 136f);
                    GUILayout.FlexibleSpace();
                }

                EnsureGenerationReport();
                int developerOnly = PungentUtilityRegistry.All.Count(u => u != null && (u.IsDeveloperOnly || u.ItemKind == PungentUtilityItemKind.Internal));
                int metadataOverrides = PungentUtilityMetadataOverrides.instance.Overrides.Count;
                int packageOverrides = PungentUtilityPackageCatalogSettings.instance.Records.Count;
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(PungentUtilityRegistry.All.Count + " utilities", UtilityWindowTheme.Teal, 104f);
                    UtilityWindowTheme.CountPill(PungentUtilityPackageCatalog.Records.Count + " packages", UtilityWindowTheme.Cyan, 104f);
                    UtilityWindowTheme.CountPill(developerOnly + " dev-only", developerOnly == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    UtilityWindowTheme.CountPill(metadataOverrides + " metadata overrides", metadataOverrides == 0 ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Amber, 148f);
                    UtilityWindowTheme.CountPill(packageOverrides + " package overrides", packageOverrides == 0 ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Amber, 148f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    int generatedTopics = _generationReport == null ? 0 : _generationReport.generatedDraftTopics;
                    int reviewRows = _generationReport == null ? 0 : _generationReport.generatedEntriesAwaitingReview;
                    UtilityWindowTheme.CountPill(generatedTopics + " generated topics", generatedTopics == 0 ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Purple, 138f);
                    UtilityWindowTheme.CountPill(reviewRows + " review entries", reviewRows == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 132f);
                    UtilityWindowTheme.CountPill(_developerSurfaceRows.Count + " surface scan rows", _developerSurfaceRows.Count == 0 ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Amber, 150f);
                    GUILayout.FlexibleSpace();
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Jump To Workbench Area", UtilityWindowTheme.Purple);
                DrawOverviewAction("Metadata", "Package, category, and field-focus metadata editing.", () => { _tab = Tab.Metadata; SavePrefs(); });
                DrawOverviewAction("Generation", "Generated help indexes, stubs, review queues, and stale cleanup.", () => { _tab = Tab.Generation; SavePrefs(); });
                DrawOverviewAction("Audit", "Package readiness and developer-surface consolidation scan.", () => { _tab = Tab.Audit; SavePrefs(); });
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Developer-Only Registry Items", UtilityWindowTheme.Teal, "current registry");
                HashSet<string> promoted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "documentation-links", "qa-checklist-utility", "editor-style-explorer" };
                List<PungentUtilityDescriptor> utilities = PungentUtilityRegistry.All
                    .Where(u => u != null && (u.IsDeveloperOnly || u.ItemKind == PungentUtilityItemKind.Internal))
                    .Where(u => !promoted.Contains(u.Id))
                    .OrderBy(u => u.Module, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (utilities.Count == 0)
                    EditorGUILayout.HelpBox("No developer-only utility descriptors are registered.", MessageType.Info);

                foreach (PungentUtilityDescriptor utility in utilities)
                {
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 4, 2)))
                    {
                        EditorGUILayout.LabelField(utility.DisplayName, EditorStyles.boldLabel, GUILayout.Width(210f));
                        EditorGUILayout.LabelField(utility.Module, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(140f));
                        EditorGUILayout.LabelField(utility.Description, UtilityWindowTheme.MutedMiniLabelStyle);
                        if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(54f), GUILayout.Height(20f)))
                            OpenDeveloperUtility(utility);
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Promotion Guardrails", UtilityWindowTheme.Amber);
                EditorGUILayout.LabelField("Documentation Links, Checklist Utility, and Editor Style Explorer are surface-level or theme-owned utilities, not Developer Tools entries.", UtilityWindowTheme.BodyStyle);
                EditorGUILayout.LabelField("Legacy metadata/category popups remain compatibility helpers until this workbench has been validated in Unity.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawOverviewAction(string label, string description, Action action)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(156f), GUILayout.Height(22f)))
                    action?.Invoke();
                EditorGUILayout.LabelField(description, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void OpenDeveloperUtility(PungentUtilityDescriptor utility)
        {
            if (utility == null)
                return;
            if (string.Equals(utility.Id, "utility-metadata-editor", StringComparison.OrdinalIgnoreCase))
                OpenMetadata(_selectedUtilityId);
            else if (string.Equals(utility.Id, "category-reference-membership", StringComparison.OrdinalIgnoreCase))
                OpenCategory(_selectedCategoryId);
            else if (string.Equals(utility.Id, "developer-tools", StringComparison.OrdinalIgnoreCase))
                Open();
            else
                PungentUtilityRegistry.Open(utility.Id);
        }

        private void DrawMetadata()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 5, 4)))
            {
                _mode = (MetadataMode)EditorGUILayout.EnumPopup(new GUIContent("Mode", "Choose package-first, category membership, or one-field review across utilities."), _mode, GUILayout.Width(220f));
                _search = EditorGUILayout.TextField(new GUIContent("Search", "Search utilities by ID, name, package, module, tags, or category."), _search, UtilityWindowTheme.ToolbarSearchStyle);
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(54f), GUILayout.Height(22f)))
                {
                    _search = string.Empty;
                    _packageSearch = string.Empty;
                    _categorySearch = string.Empty;
                    SavePrefs();
                }
                _includeHidden = GUILayout.Toggle(_includeHidden, new GUIContent("Hidden/Internal", "Include hidden, archived, and developer-only utilities."), EditorStyles.miniButton, GUILayout.Width(104f));
                _onlyOverrides = GUILayout.Toggle(_onlyOverrides, new GUIContent("Overrides", "Only show utilities with project-local metadata overrides."), EditorStyles.miniButton, GUILayout.Width(78f));
                _filterMissingDocs = GUILayout.Toggle(_filterMissingDocs, new GUIContent("No Docs", "Only show utilities missing a documentation topic ID."), EditorStyles.miniButton, GUILayout.Width(70f));
                _filterNoCardTint = GUILayout.Toggle(_filterNoCardTint, new GUIContent("No Tint", "Only show utilities without a project-local browser card tint override."), EditorStyles.miniButton, GUILayout.Width(68f));
                _filterNoFacets = GUILayout.Toggle(_filterNoFacets, new GUIContent("No Facets", "Only show utilities without category facets."), EditorStyles.miniButton, GUILayout.Width(78f));
            }

            SavePrefs();
            DrawMetadataWorkspace();

            DrawMetadataAncillaryEditors();
            SavePrefs();
        }

        private void DrawMetadataWorkspace()
        {
            EditorGUILayout.HelpBox(GetMetadataWorkspaceHelp(), MessageType.Info);
            if (position.width < 980f)
            {
                DrawMetadataWorkspaceSidebar(GUILayout.ExpandWidth(true), GUILayout.MinHeight(140f), GUILayout.MaxHeight(240f));
                DrawMetadataWorkspaceMain();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawMetadataWorkspaceSidebar(GUILayout.Width(GetPackageListWidth()), GUILayout.ExpandHeight(true));
                DrawMetadataWorkspaceMain();
            }
        }

        private string GetMetadataWorkspaceHelp()
        {
            switch (_mode)
            {
                case MetadataMode.Category:
                    return "Category mode uses the shared metadata workspace with categories in the sidebar, matching utilities in the list, and the selected utility's browser metadata on the right.";
                case MetadataMode.FieldFocus:
                    return "Field Focus mode uses the shared metadata workspace with metadata fields in the sidebar, matching utilities in the list, and focused-field editors on the right.";
                case MetadataMode.Utility:
                    return "Utility mode uses the shared metadata workspace with utilities in the sidebar, related/context utilities in the list, and the selected utility metadata inspector on the right.";
                default:
                    return "Package Unit mode uses the shared metadata workspace with packages in the sidebar, package utilities in the list, and browser-facing utility metadata on the right.";
            }
        }

        private void DrawMetadataWorkspaceSidebar(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(GetMetadataModeTint()), options))
            {
                DrawPackageUnitSectionTitle(GetMetadataSidebarTitle(), GetMetadataModeTint(), GetMetadataSidebarCountLabel());
                if (_mode == MetadataMode.PackageUnit)
                    _packageSearch = EditorGUILayout.TextField(_packageSearch, UtilityWindowTheme.ToolbarSearchStyle);
                else if (_mode == MetadataMode.Category)
                    _categorySearch = EditorGUILayout.TextField(_categorySearch, UtilityWindowTheme.ToolbarSearchStyle);
                else if (_mode == MetadataMode.Utility)
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);

                _packageListScroll = BeginPackageUnitVerticalScroll(_packageListScroll, GUILayout.ExpandHeight(true));
                switch (_mode)
                {
                    case MetadataMode.Category:
                        DrawMetadataCategorySidebarRows();
                        break;
                    case MetadataMode.FieldFocus:
                        DrawMetadataFieldSidebarRows();
                        break;
                    case MetadataMode.Utility:
                        DrawMetadataUtilitySidebarRows();
                        break;
                    default:
                        DrawMetadataPackageSidebarRows();
                        break;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private string GetMetadataSidebarTitle()
        {
            switch (_mode)
            {
                case MetadataMode.Category: return "Categories";
                case MetadataMode.FieldFocus: return "Fields";
                case MetadataMode.Utility: return "Utilities";
                default: return "Packages";
            }
        }

        private string GetMetadataSidebarCountLabel()
        {
            switch (_mode)
            {
                case MetadataMode.Category: return GetMetadataCategoryIds().Count() + " ids";
                case MetadataMode.FieldFocus: return Enum.GetValues(typeof(MetadataField)).Length + " fields";
                case MetadataMode.Utility: return PungentUtilityRegistry.All.Count(PassesUtilityFilters) + " utilities";
                default: return PungentUtilityPackageCatalog.Records.Count + " records";
            }
        }

        private Color GetMetadataModeTint()
        {
            switch (_mode)
            {
                case MetadataMode.Category: return UtilityWindowTheme.Purple;
                case MetadataMode.FieldFocus: return UtilityWindowTheme.Amber;
                case MetadataMode.Utility: return UtilityWindowTheme.Teal;
                default: return UtilityWindowTheme.Cyan;
            }
        }

        private void DrawMetadataPackageSidebarRows()
        {
            foreach (PungentUtilityPackageRecord record in GetFilteredPackages())
            {
                bool selected = string.Equals(record.packageId, _selectedPackageId, StringComparison.OrdinalIgnoreCase);
                using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Cyan : record.lifecycle == PungentUtilityPackageLifecycle.Archived ? UtilityWindowTheme.Neutral : UtilityWindowTheme.HeaderTint))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(record.displayName, record.packageId), EditorStyles.miniButton, GUILayout.Height(24f)))
                        {
                            _selectedPackageId = record.packageId;
                            _packageDraft = null;
                            EnsurePackageDraft();
                            SavePrefs();
                        }
                        if (record.lifecycle != PungentUtilityPackageLifecycle.Visible)
                            GUILayout.Label(record.lifecycle.ToString(), EditorStyles.miniLabel, GUILayout.Width(58f));
                    }
                }
            }
        }

        private void DrawMetadataCategorySidebarRows()
        {
            foreach (string categoryId in GetMetadataCategoryIds().Where(PassesCategorySearch))
            {
                int count = PungentUtilityRegistry.All.Count(utility => utility != null && utility.HasCategory(categoryId));
                bool selected = string.Equals(categoryId, _selectedCategoryId, StringComparison.OrdinalIgnoreCase);
                PungentUtilityCategoryLifecycle lifecycle = PungentUtilityCategoryOverrides.instance.GetLifecycle(categoryId);
                using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Purple : lifecycle == PungentUtilityCategoryLifecycle.Archived ? UtilityWindowTheme.Neutral : PungentUtilityCategories.GetTint(categoryId)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(PungentUtilityCategories.GetDisplayName(categoryId) + "  (" + count + ")", categoryId), EditorStyles.miniButton, GUILayout.Height(24f)))
                        {
                            _selectedCategoryId = categoryId;
                            _categoryDraft = null;
                            SavePrefs();
                        }
                        if (lifecycle != PungentUtilityCategoryLifecycle.Visible)
                            GUILayout.Label(lifecycle.ToString(), EditorStyles.miniLabel, GUILayout.Width(58f));
                    }
                }
            }
        }

        private void DrawMetadataUtilitySidebarRows()
        {
            foreach (PungentUtilityDescriptor utility in PungentUtilityRegistry.All.Where(PassesUtilityFilters).OrderBy(utility => utility.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                bool selected = string.Equals(utility.Id, _selectedUtilityId, StringComparison.OrdinalIgnoreCase);
                using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Teal : PungentUtilityCategories.GetTint(utility.AreaCategory)))
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, selected ? 0.16f : 0.05f, 0.02f, 4, 2)))
                {
                    if (GUILayout.Button(new GUIContent(utility.DisplayName, utility.Id), EditorStyles.miniButton, GUILayout.Height(23f)))
                    {
                        SelectUtility(utility.Id);
                        SavePrefs();
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill(utility.BrowserRole.ToString(), UtilityWindowTheme.Neutral, 72f);
                        if (PungentUtilityRegistry.HasMetadataOverride(utility.Id))
                            UtilityWindowTheme.CountPill("Override", UtilityWindowTheme.Amber, 74f);
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private void DrawMetadataFieldSidebarRows()
        {
            foreach (MetadataField field in Enum.GetValues(typeof(MetadataField)))
            {
                bool selected = _field == field;
                using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral))
                {
                    if (GUILayout.Button(ObjectNames.NicifyVariableName(field.ToString()), EditorStyles.miniButton, GUILayout.Height(24f)))
                    {
                        _field = field;
                        SavePrefs();
                    }
                }
            }
        }

        private void DrawMetadataWorkspaceMain()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawMetadataContextPanel();
                List<PungentUtilityDescriptor> utilities = GetMetadataWorkspaceUtilities();
                EnsureSelectedUtilityForWorkspace(utilities);

                if (position.width < 1180f)
                {
                    DrawMetadataUtilityList(utilities, GUILayout.ExpandWidth(true), GUILayout.MinHeight(150f), GUILayout.MaxHeight(260f));
                    DrawMetadataWorkspaceRightPane(utilities);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        DrawMetadataUtilityList(utilities, GUILayout.Width(GetUtilityListWidth()), GUILayout.ExpandHeight(true));
                        DrawMetadataWorkspaceRightPane(utilities);
                    }
                }
            }
        }

        private void DrawMetadataWorkspaceRightPane(List<PungentUtilityDescriptor> utilities)
        {
            if (_mode == MetadataMode.FieldFocus)
                DrawFieldFocusInspector(utilities);
            else
                DrawMetadataUtilityInspector(PungentUtilityRegistry.Find(_selectedUtilityId));
        }

        private void DrawMetadataContextPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(GetMetadataModeTint(), 0.08f, 0.035f, 5, 3)))
            {
                switch (_mode)
                {
                    case MetadataMode.Category:
                        DrawCategoryContextPanel();
                        break;
                    case MetadataMode.FieldFocus:
                        DrawFieldContextPanel();
                        break;
                    case MetadataMode.Utility:
                        DrawUtilityContextPanel();
                        break;
                    default:
                        DrawPackageContextPanel();
                        break;
                }
            }
        }

        private void DrawPackageContextPanel()
        {
            EnsurePackageDraft();
            if (_packageDraft == null)
            {
                EditorGUILayout.HelpBox("No package selected.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPackageUnitSectionTitle("Package Unit", UtilityWindowTheme.Cyan, _packageDraft.packageId);
                UtilityWindowTheme.CountPill(PungentUtilityPackageCatalogSettings.instance.HasOverride(_packageDraft.packageId) ? "Project Override" : "Factory", PungentUtilityPackageCatalogSettings.instance.HasOverride(_packageDraft.packageId) ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 112f);
                UtilityWindowTheme.CountPill(_packageDraft.lifecycle.ToString(), _packageDraft.lifecycle == PungentUtilityPackageLifecycle.Visible ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 86f);
            }

            DrawGateHint();
            using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _packageDraft.packageId = DrawInlineTextField("Package ID", _packageDraft.packageId);
                    _packageDraft.displayName = DrawInlineTextField("Display Name", _packageDraft.displayName);
                    _packageDraft.tier = DrawInlineEnumField("Tier", _packageDraft.tier, 170f);
                    _packageDraft.lifecycle = DrawInlineEnumField("Lifecycle", _packageDraft.lifecycle, 150f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _packageDraft.availability = DrawInlineEnumField("Availability", _packageDraft.availability, 180f);
                    _packageDraft.packageManagerId = DrawInlineTextField("UPM", _packageDraft.packageManagerId);
                    _packageDraft.assetStoreUrl = DrawInlineTextField("Asset Store", _packageDraft.assetStoreUrl);
                }

                _packageDraft.description = DrawPackageUnitTextArea(_packageDraft.description, 38f);
                DrawPackageManagementActions();
            }
        }

        private string DrawInlineTextField(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(label, UtilityWindowTheme.MutedMiniLabelStyle);
                return EditorGUILayout.DelayedTextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            }
        }

        private TEnum DrawInlineEnumField<TEnum>(string label, TEnum value, float width) where TEnum : struct
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                GUILayout.Label(label, UtilityWindowTheme.MutedMiniLabelStyle);
                return (TEnum)(object)EditorGUILayout.EnumPopup((Enum)(object)value, GUILayout.ExpandWidth(true));
            }
        }

        private void DrawPackageManagementActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Package", EditorStyles.miniButton, GUILayout.Width(104f), GUILayout.Height(22f)))
                    SavePackageRecord();
                if (GUILayout.Button("Assign Selected Utility", EditorStyles.miniButton, GUILayout.Width(138f), GUILayout.Height(22f)))
                    AssignSelectedUtilityToCurrentPackage();
                if (PungentUtilityPackageCatalogSettings.instance.HasOverride(_packageDraft.packageId) && GUILayout.Button("Remove Project Record", EditorStyles.miniButton, GUILayout.Width(142f), GUILayout.Height(22f)))
                    ClearPackageRecord();

                if (GUILayout.Button(_packageDraft.lifecycle == PungentUtilityPackageLifecycle.Archived ? "Restore" : "Archive", EditorStyles.miniButton, GUILayout.Width(74f), GUILayout.Height(22f)))
                {
                    _packageDraft.lifecycle = _packageDraft.lifecycle == PungentUtilityPackageLifecycle.Archived ? PungentUtilityPackageLifecycle.Visible : PungentUtilityPackageLifecycle.Archived;
                    SavePackageRecord();
                }
                if (GUILayout.Button(_packageDraft.lifecycle == PungentUtilityPackageLifecycle.Hidden ? "Show" : "Hide", EditorStyles.miniButton, GUILayout.Width(58f), GUILayout.Height(22f)))
                {
                    _packageDraft.lifecycle = _packageDraft.lifecycle == PungentUtilityPackageLifecycle.Hidden ? PungentUtilityPackageLifecycle.Visible : PungentUtilityPackageLifecycle.Hidden;
                    SavePackageRecord();
                }
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newPackageId = EditorGUILayout.DelayedTextField(new GUIContent("New Package", "Project-local package ID to create."), _newPackageId ?? string.Empty);
                if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(64f), GUILayout.Height(22f)))
                    CreateProjectPackage(_newPackageId);
                _duplicatePackageId = EditorGUILayout.DelayedTextField(new GUIContent("Duplicate As", "Duplicate selected package metadata into this package ID."), _duplicatePackageId ?? string.Empty);
                if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(78f), GUILayout.Height(22f)))
                    DuplicateSelectedPackage(_duplicatePackageId);
            }
        }

        private void DrawCategoryContextPanel()
        {
            EnsureCategoryDraft();
            if (_categoryDraft == null)
            {
                EditorGUILayout.HelpBox("No category selected.", MessageType.Info);
                return;
            }

            string categoryId = PungentUtilityCategories.Normalize(_categoryDraft.categoryId);
            bool isFactory = PungentUtilityCategories.IsFactoryCategory(categoryId);
            bool hasOverride = PungentUtilityCategoryOverrides.instance.HasOverride(categoryId);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPackageUnitSectionTitle("Category", UtilityWindowTheme.Purple, categoryId);
                UtilityWindowTheme.CountPill(GetCategoryUtilities(categoryId).Count + " utilities", UtilityWindowTheme.Teal, 104f);
                UtilityWindowTheme.CountPill(isFactory ? "Factory" : "Project", isFactory ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Amber, 82f);
                UtilityWindowTheme.CountPill(_categoryDraft.lifecycle.ToString(), _categoryDraft.lifecycle == PungentUtilityCategoryLifecycle.Visible ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 86f);
                UtilityWindowTheme.CountPill(hasOverride ? "Override" : "Default", hasOverride ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 86f);
            }
            DrawGateHint();

            using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(isFactory))
                        _categoryDraft.categoryId = DrawInlineTextField("Category ID", _categoryDraft.categoryId);
                    _categoryDraft.lifecycle = DrawInlineEnumField("Lifecycle", _categoryDraft.lifecycle, 150f);
                    _categoryDraft.overrideDisplayName = EditorGUILayout.ToggleLeft("Name", _categoryDraft.overrideDisplayName, GUILayout.Width(58f));
                    using (new EditorGUI.DisabledScope(!_categoryDraft.overrideDisplayName))
                        _categoryDraft.displayName = DrawInlineTextField("Display Name", _categoryDraft.displayName);
                    _categoryDraft.overrideTint = EditorGUILayout.ToggleLeft("Tint", _categoryDraft.overrideTint, GUILayout.Width(54f));
                    using (new EditorGUI.DisabledScope(!_categoryDraft.overrideTint))
                        _categoryDraft.tint = EditorGUILayout.ColorField(GUIContent.none, _categoryDraft.tint, false, false, false, GUILayout.Width(56f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save Category", EditorStyles.miniButton, GUILayout.Width(106f), GUILayout.Height(22f)))
                        SaveCategoryOverride(_categoryDraft);
                    if (hasOverride && GUILayout.Button("Clear Override", EditorStyles.miniButton, GUILayout.Width(100f), GUILayout.Height(22f)))
                        ClearCategoryOverride(categoryId);
                    if (!isFactory && hasOverride && GUILayout.Button("Remove Project Record", EditorStyles.miniButton, GUILayout.Width(142f), GUILayout.Height(22f)))
                        RemoveProjectCategory(categoryId);
                    if (GUILayout.Button(_categoryDraft.lifecycle == PungentUtilityCategoryLifecycle.Archived ? "Restore" : "Archive", EditorStyles.miniButton, GUILayout.Width(74f), GUILayout.Height(22f)))
                    {
                        _categoryDraft.lifecycle = _categoryDraft.lifecycle == PungentUtilityCategoryLifecycle.Archived ? PungentUtilityCategoryLifecycle.Visible : PungentUtilityCategoryLifecycle.Archived;
                        SaveCategoryOverride(_categoryDraft);
                    }
                    if (GUILayout.Button(_categoryDraft.lifecycle == PungentUtilityCategoryLifecycle.Hidden ? "Show" : "Hide", EditorStyles.miniButton, GUILayout.Width(58f), GUILayout.Height(22f)))
                    {
                        _categoryDraft.lifecycle = _categoryDraft.lifecycle == PungentUtilityCategoryLifecycle.Hidden ? PungentUtilityCategoryLifecycle.Visible : PungentUtilityCategoryLifecycle.Hidden;
                        SaveCategoryOverride(_categoryDraft);
                    }
                    if (GUILayout.Button("Field Focus", EditorStyles.miniButton, GUILayout.Width(86f), GUILayout.Height(22f)))
                    {
                        _mode = MetadataMode.FieldFocus;
                        _field = MetadataField.CategoryFacets;
                        SavePrefs();
                    }
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _newCategoryId = EditorGUILayout.DelayedTextField(new GUIContent("New Category", "Project-local category ID to create."), _newCategoryId ?? string.Empty);
                    if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(64f), GUILayout.Height(22f)))
                        CreateProjectCategory(_newCategoryId);
                    _duplicateCategoryId = EditorGUILayout.DelayedTextField(new GUIContent("Duplicate As", "Duplicate selected category metadata into this project-local category ID."), _duplicateCategoryId ?? string.Empty);
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(78f), GUILayout.Height(22f)))
                        DuplicateSelectedCategory(_duplicateCategoryId);
                }
            }
        }

        private void DrawUtilityContextPanel()
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(_selectedUtilityId);
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox("No utility selected.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPackageUnitSectionTitle("Utility", UtilityWindowTheme.Teal, descriptor.Id);
                UtilityWindowTheme.CountPill(descriptor.NormalizedPackageId, UtilityWindowTheme.Cyan, 150f);
                UtilityWindowTheme.CountPill(PungentUtilityCategories.GetDisplayName(descriptor.AreaCategory), PungentUtilityCategories.GetTint(descriptor.AreaCategory), 124f);
                UtilityWindowTheme.CountPill(descriptor.BrowserRole.ToString(), UtilityWindowTheme.Neutral, 96f);
                if (PungentUtilityRegistry.HasMetadataOverride(descriptor.Id))
                    UtilityWindowTheme.CountPill("Override", UtilityWindowTheme.Amber, 76f);
                GUILayout.FlexibleSpace();
            }
            DrawGateHint();
        }

        private void DrawFieldContextPanel()
        {
            DrawPackageUnitSectionTitle("Field Focus", UtilityWindowTheme.Amber, ObjectNames.NicifyVariableName(_field.ToString()));
            List<PungentUtilityDescriptor> utilities = GetMetadataWorkspaceUtilities();
            int overrides = utilities.Count(utility =>
            {
                PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(utility);
                return metadata != null && FieldHasOverride(metadata);
            });
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(utilities.Count + " utilities", UtilityWindowTheme.Teal, 96f);
                UtilityWindowTheme.CountPill(overrides + " overrides", overrides > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 96f);
                if (_field == MetadataField.CategoryFacets)
                {
                    string[] categories = GetAllCategoryIds();
                    int index = Mathf.Max(0, Array.FindIndex(categories, c => string.Equals(c, _selectedCategoryId, StringComparison.OrdinalIgnoreCase)));
                    int next = EditorGUILayout.Popup(new GUIContent("Category"), index, categories.Select(PungentUtilityCategories.GetDisplayName).ToArray(), GUILayout.Width(220f));
                    if (next >= 0 && next < categories.Length)
                        _selectedCategoryId = categories[next];
                }
                GUILayout.FlexibleSpace();
            }
        }

        private List<PungentUtilityDescriptor> GetMetadataWorkspaceUtilities()
        {
            IEnumerable<PungentUtilityDescriptor> utilities = PungentUtilityRegistry.All.Where(PassesUtilityFilters);
            switch (_mode)
            {
                case MetadataMode.Category:
                    utilities = utilities.Where(utility => utility.HasCategory(_selectedCategoryId));
                    break;
                case MetadataMode.PackageUnit:
                    string packageId = PungentUtilityPackageCatalog.NormalizePackageId(_selectedPackageId);
                    utilities = utilities.Where(utility => string.Equals(utility.NormalizedPackageId, packageId, StringComparison.OrdinalIgnoreCase));
                    break;
                case MetadataMode.Utility:
                    utilities = GetUtilityContextUtilities(PungentUtilityRegistry.Find(_selectedUtilityId)).Where(PassesUtilityFilters);
                    break;
            }

            return utilities
                .OrderBy(utility => utility.NormalizedPackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(utility => utility.BrowserPriority)
                .ThenBy(utility => utility.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void EnsureSelectedUtilityForWorkspace(List<PungentUtilityDescriptor> utilities)
        {
            if (utilities == null || utilities.Count == 0)
                return;
            if (_mode == MetadataMode.Utility && PungentUtilityRegistry.Find(_selectedUtilityId) != null)
                return;
            if (utilities.Any(utility => string.Equals(utility.Id, _selectedUtilityId, StringComparison.OrdinalIgnoreCase)))
                return;
            SelectUtility(utilities[0].Id);
        }

        private void DrawMetadataUtilityList(List<PungentUtilityDescriptor> utilities, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), options))
            {
                DrawPackageUnitSectionTitle(_mode == MetadataMode.Utility ? "Related Context" : "Utilities", UtilityWindowTheme.Teal, (utilities == null ? 0 : utilities.Count) + " shown");
                _utilityListScroll = BeginPackageUnitVerticalScroll(_utilityListScroll, GUILayout.ExpandHeight(true));
                if (utilities == null || utilities.Count == 0)
                    EditorGUILayout.HelpBox("No utilities match this metadata context and filter set.", MessageType.Info);
                else
                {
                    foreach (PungentUtilityDescriptor utility in utilities)
                        DrawMetadataUtilityListRow(utility);
                }
                EditorGUILayout.EndScrollView();
                DrawMetadataBulkActions(utilities);
            }
        }

        private IEnumerable<PungentUtilityDescriptor> GetUtilityContextUtilities(PungentUtilityDescriptor selected)
        {
            if (selected == null)
                return Enumerable.Empty<PungentUtilityDescriptor>();

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string id in selected.RelatedUtilityIds ?? new string[0])
                AddUtilityContextId(ids, id, selected.Id);
            foreach (string id in selected.AccessoryUtilityIds ?? new string[0])
                AddUtilityContextId(ids, id, selected.Id);
            foreach (string id in selected.ActionIds ?? new string[0])
                AddUtilityContextId(ids, id, selected.Id);

            foreach (PungentUtilityDescriptor utility in PungentUtilityRegistry.All)
            {
                if (utility == null || string.Equals(utility.Id, selected.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(utility.ParentUtilityId, selected.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(utility.NormalizedPackageId, selected.NormalizedPackageId, StringComparison.OrdinalIgnoreCase) ||
                    utility.HasCategory(selected.AreaCategory))
                    AddUtilityContextId(ids, utility.Id, selected.Id);
            }

            return ids
                .Select(PungentUtilityRegistry.Find)
                .Where(utility => utility != null);
        }

        private static void AddUtilityContextId(HashSet<string> ids, string id, string selectedId)
        {
            if (ids == null || string.IsNullOrWhiteSpace(id) || string.Equals(id, selectedId, StringComparison.OrdinalIgnoreCase))
                return;
            ids.Add(id);
        }

        private void DrawMetadataUtilityListRow(PungentUtilityDescriptor utility)
        {
            bool selected = string.Equals(utility.Id, _selectedUtilityId, StringComparison.OrdinalIgnoreCase);
            using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral))
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, selected ? 0.18f : 0.06f, 0.025f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool checkedNow = _metadataCheckedUtilityIds.Contains(utility.Id);
                    bool nextChecked = EditorGUILayout.Toggle(checkedNow, GUILayout.Width(18f));
                    if (nextChecked != checkedNow)
                    {
                        if (nextChecked)
                            _metadataCheckedUtilityIds.Add(utility.Id);
                        else
                            _metadataCheckedUtilityIds.Remove(utility.Id);
                    }
                    if (GUILayout.Button(new GUIContent(utility.DisplayName, utility.Id), EditorStyles.miniButton, GUILayout.Height(22f)))
                        SelectUtility(utility.Id);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(utility.BrowserRole.ToString(), UtilityWindowTheme.Neutral, 76f);
                    UtilityWindowTheme.CountPill(utility.Visibility.ToString(), utility.IsVisibleInDefaultBrowser ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 78f);
                    if (PungentUtilityRegistry.HasMetadataOverride(utility.Id))
                        UtilityWindowTheme.CountPill("Override", UtilityWindowTheme.Amber, 76f);
                    if (string.IsNullOrWhiteSpace(utility.DocumentationTopicId))
                        UtilityWindowTheme.CountPill("No Docs", UtilityWindowTheme.Amber, 68f);
                    if (!utility.HasCategoryFacets)
                        UtilityWindowTheme.CountPill("No Facets", UtilityWindowTheme.Amber, 78f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawMetadataUtilityInspector(PungentUtilityDescriptor descriptor)
        {
            DrawUtilityMetadataEditor(descriptor);
        }

        private void DrawFieldFocusInspector(List<PungentUtilityDescriptor> utilities)
        {
            List<PungentUtilityDescriptor> rows = GetFieldFocusInspectorUtilities(utilities);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawPackageUnitSectionTitle("Focused Field", UtilityWindowTheme.Amber, ObjectNames.NicifyVariableName(_field.ToString()));
                EditorGUILayout.LabelField("Showing only the selected metadata field for " + rows.Count + " utilities.", UtilityWindowTheme.MutedMiniLabelStyle);
                _fieldScroll = BeginPackageUnitVerticalScroll(_fieldScroll, GUILayout.ExpandHeight(true));
                if (rows.Count == 0)
                    EditorGUILayout.HelpBox("Check utilities in the center list, or clear checks to review every visible utility in this field context.", MessageType.Info);
                foreach (PungentUtilityDescriptor descriptor in rows)
                    DrawFocusedFieldInspectorRow(descriptor);
                EditorGUILayout.EndScrollView();
            }
        }

        private List<PungentUtilityDescriptor> GetFieldFocusInspectorUtilities(List<PungentUtilityDescriptor> utilities)
        {
            IEnumerable<PungentUtilityDescriptor> source = utilities ?? new List<PungentUtilityDescriptor>();
            if (_metadataCheckedUtilityIds.Count > 0)
                source = source.Where(utility => _metadataCheckedUtilityIds.Contains(utility.Id));
            return source
                .OrderBy(utility => utility.NormalizedPackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(utility => utility.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void DrawFocusedFieldInspectorRow(PungentUtilityDescriptor descriptor)
        {
            PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
            if (metadata == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.07f, 0.03f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(descriptor.DisplayName, descriptor.Id), EditorStyles.miniButton, GUILayout.Width(220f), GUILayout.Height(22f)))
                        SelectUtility(descriptor.Id);
                    EditorGUILayout.SelectableLabel(descriptor.Id, EditorStyles.textField, GUILayout.Height(18f), GUILayout.MinWidth(120f));
                    DrawFieldSourceChip(descriptor, metadata);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                    {
                        if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.Width(52f), GUILayout.Height(22f)))
                            SaveMetadataOverride(descriptor, metadata);
                    }
                }

                DrawFocusedFieldControl(descriptor, metadata);
            }
        }

        private void DrawMetadataBulkActions(List<PungentUtilityDescriptor> utilities)
        {
            if (utilities == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Visible", EditorStyles.miniButton, GUILayout.Width(92f), GUILayout.Height(22f)))
                {
                    _metadataCheckedUtilityIds.Clear();
                    foreach (PungentUtilityDescriptor utility in utilities)
                        _metadataCheckedUtilityIds.Add(utility.Id);
                }
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(52f), GUILayout.Height(22f)))
                    _metadataCheckedUtilityIds.Clear();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata || _metadataCheckedUtilityIds.Count == 0))
                {
                    if (_mode == MetadataMode.PackageUnit && GUILayout.Button("Assign Package", EditorStyles.miniButton, GUILayout.Width(104f), GUILayout.Height(22f)))
                        BulkAssignPackageToChecked();
                    if (_mode == MetadataMode.Category && GUILayout.Button("Add Facet", EditorStyles.miniButton, GUILayout.Width(78f), GUILayout.Height(22f)))
                        BulkAssignCategoryFacetToChecked();
                    if (GUILayout.Button("Use Category Tint", EditorStyles.miniButton, GUILayout.Width(118f), GUILayout.Height(22f)))
                        BulkApplyCategoryTintToChecked();
                }
                using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata || utilities.Count == 0))
                {
                    if (GUILayout.Button("Tint Visible", EditorStyles.miniButton, GUILayout.Width(82f), GUILayout.Height(22f)))
                        BulkApplyCategoryTintToVisibleContext(utilities);
                }
            }
        }

        private void CreateProjectPackage(string packageId)
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata)
                return;

            string normalized = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            PungentUtilityPackageRecord record = PungentUtilityPackageCatalog.CreateEditableRecord(normalized);
            record.packageId = normalized;
            if (string.IsNullOrWhiteSpace(record.displayName) || string.Equals(record.displayName, normalized, StringComparison.OrdinalIgnoreCase))
                record.displayName = ObjectNames.NicifyVariableName(normalized.Split('.').LastOrDefault() ?? normalized);
            record.lifecycle = PungentUtilityPackageLifecycle.Visible;
            PungentUtilityPackageCatalog.SaveRecordOverride(record);
            _selectedPackageId = normalized;
            _packageDraft = null;
            EnsurePackageDraft();
            _status = "Created project-local package record.";
        }

        private void DuplicateSelectedPackage(string packageId)
        {
            if (_packageDraft == null || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            string normalized = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            PungentUtilityPackageRecord copy = PungentUtilityPackageCatalog.CloneRecord(_packageDraft);
            copy.packageId = normalized;
            copy.displayName = ObjectNames.NicifyVariableName(normalized.Split('.').LastOrDefault() ?? normalized);
            PungentUtilityPackageCatalog.SaveRecordOverride(copy);
            _selectedPackageId = normalized;
            _packageDraft = null;
            EnsurePackageDraft();
            _status = "Duplicated package record.";
        }

        private void BulkAssignPackageToChecked()
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata || _metadataCheckedUtilityIds.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Assign Package", "Assign the selected utilities to " + _selectedPackageId + " using project-local metadata overrides?", "Assign", "Cancel"))
                return;

            foreach (string utilityId in _metadataCheckedUtilityIds.ToArray())
            {
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
                PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
                if (descriptor == null || metadata == null)
                    continue;
                metadata.overridePackageId = true;
                metadata.packageId = PungentUtilityPackageCatalog.NormalizePackageId(_selectedPackageId);
                SaveMetadataOverride(descriptor, metadata);
            }
            _status = "Assigned package to selected utilities.";
        }

        private void AssignSelectedUtilityToCurrentPackage()
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata)
                return;

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(_selectedUtilityId);
            PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
            if (descriptor == null || metadata == null)
                return;

            metadata.overridePackageId = true;
            metadata.packageId = PungentUtilityPackageCatalog.NormalizePackageId(_selectedPackageId);
            SaveMetadataOverride(descriptor, metadata);
            _status = "Assigned selected utility to " + metadata.packageId + ".";
        }

        private void BulkAssignCategoryFacetToChecked()
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata || _metadataCheckedUtilityIds.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Add Category Facet", "Add " + _selectedCategoryId + " as a category facet for selected utilities?", "Add", "Cancel"))
                return;

            foreach (string utilityId in _metadataCheckedUtilityIds.ToArray())
            {
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
                PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
                if (descriptor == null || metadata == null)
                    continue;
                SetCategoryMembership(metadata, _selectedCategoryId, true);
                SaveMetadataOverride(descriptor, metadata);
            }
            _status = "Added category facet to selected utilities.";
        }

        private void BulkApplyCategoryTintToChecked()
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata || _metadataCheckedUtilityIds.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Apply Category Tint", "Apply each selected utility's effective category tint as its explicit browser card tint?", "Apply", "Cancel"))
                return;

            foreach (string utilityId in _metadataCheckedUtilityIds.ToArray())
            {
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
                PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
                if (descriptor == null || metadata == null)
                    continue;
                metadata.overrideBrowserCardTint = true;
                string categoryId = metadata.overrideAreaCategory ? metadata.areaCategory : descriptor.AreaCategory;
                metadata.browserCardTint = PungentUtilityCategories.GetTint(categoryId);
                SaveMetadataOverride(descriptor, metadata);
            }
            _status = "Applied category tint to selected utilities.";
        }

        private void BulkApplyCategoryTintToVisibleContext(List<PungentUtilityDescriptor> utilities)
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata || utilities == null || utilities.Count == 0)
                return;
            if (!EditorUtility.DisplayDialog("Apply Tint To Visible Context", "Apply each visible utility's effective category tint as an explicit browser card tint override?", "Apply", "Cancel"))
                return;

            foreach (PungentUtilityDescriptor descriptor in utilities)
            {
                PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
                if (descriptor == null || metadata == null)
                    continue;
                string categoryId = metadata.overrideAreaCategory ? metadata.areaCategory : descriptor.AreaCategory;
                metadata.overrideBrowserCardTint = true;
                metadata.browserCardTint = PungentUtilityCategories.GetTint(categoryId);
                SaveMetadataOverride(descriptor, metadata);
            }

            _status = "Applied category tint to visible metadata context.";
        }

        [Obsolete("Inactive legacy layout. Metadata tab uses DrawMetadataWorkspace.", true)]
        private void DrawPackageUnitMode()
        {
            EditorGUILayout.HelpBox("Package Unit mode edits a package record as one cohesive unit, then shows the utilities assigned to that package with their project-local metadata overrides.", MessageType.Info);
            if (IsPackageUnitNarrow())
            {
                DrawPackageList(GUILayout.ExpandWidth(true), GUILayout.MinHeight(130f), GUILayout.MaxHeight(GetPackageUnitStackListHeight()));
                DrawPackageDetails(PackageUnitLayout.Narrow);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    DrawPackageList(GUILayout.Width(GetPackageListWidth()), GUILayout.ExpandHeight(true));
                    DrawPackageDetails(IsPackageUnitWide() ? PackageUnitLayout.Wide : PackageUnitLayout.Medium);
                }
            }
        }

        private void DrawMetadataAncillaryEditors()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.06f, 0.025f, 4, 2)))
            {
                _showMetadataAncillaryEditors = EditorGUILayout.Foldout(
                    _showMetadataAncillaryEditors,
                    new GUIContent("Documentation Links And External Browser Copy", "Secondary metadata editors. Keep collapsed while focusing on package-unit editing."),
                    true);
                if (_showMetadataAncillaryEditors)
                {
                    _metadataAncillaryScroll = BeginPackageUnitVerticalScroll(_metadataAncillaryScroll, GUILayout.MinHeight(150f), GUILayout.MaxHeight(Mathf.Clamp(position.height * 0.32f, 190f, 360f)));
                    if (position.width >= 1320f)
                    {
                        using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                        {
                            DrawDocumentationAssignments(GUILayout.ExpandWidth(true));
                            DrawExternalLinksEditor(GUILayout.ExpandWidth(true));
                        }
                    }
                    else
                    {
                        DrawDocumentationAssignments(GUILayout.ExpandWidth(true));
                        DrawExternalLinksEditor(GUILayout.ExpandWidth(true));
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawCategoryReferenceStrip()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.09f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Category Reference", UtilityWindowTheme.Purple, _selectedCategoryId);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Field Focus", "Review category facets across all utilities."), EditorStyles.miniButton, GUILayout.Width(104f), GUILayout.Height(22f)))
                    {
                        _mode = MetadataMode.FieldFocus;
                        _field = MetadataField.CategoryFacets;
                    }
                }

                string[] categories = GetAllCategoryIds();
                using (new EditorGUILayout.HorizontalScope())
                {
                    int selected = Mathf.Max(0, Array.FindIndex(categories, c => string.Equals(c, _selectedCategoryId, StringComparison.OrdinalIgnoreCase)));
                    int next = EditorGUILayout.Popup(selected, categories.Select(c => PungentUtilityCategories.GetDisplayName(c) + " (" + c + ")").ToArray(), GUILayout.MinWidth(220f));
                    if (next >= 0 && next < categories.Length)
                        _selectedCategoryId = categories[next];

                    PungentUtilityCategoryOverrides.CategoryOverride editable = PungentUtilityCategoryOverrides.instance.CreateEditableCopy(
                        _selectedCategoryId,
                        PungentUtilityCategories.GetFactoryDisplayName(_selectedCategoryId),
                        PungentUtilityCategories.GetFactoryTint(_selectedCategoryId));

                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                    {
                        editable.overrideDisplayName = EditorGUILayout.ToggleLeft(new GUIContent("Name", "Override the display name for this category."), editable.overrideDisplayName, GUILayout.Width(58f));
                        using (new EditorGUI.DisabledScope(!editable.overrideDisplayName))
                            editable.displayName = EditorGUILayout.DelayedTextField(editable.displayName, GUILayout.Width(160f));
                        editable.overrideTint = EditorGUILayout.ToggleLeft(new GUIContent("Colour", "Override this category colour."), editable.overrideTint, GUILayout.Width(70f));
                        using (new EditorGUI.DisabledScope(!editable.overrideTint))
                            editable.tint = EditorGUILayout.ColorField(GUIContent.none, editable.tint, false, false, false, GUILayout.Width(48f));
                        if (GUILayout.Button("Save Category", EditorStyles.miniButton, GUILayout.Width(106f), GUILayout.Height(22f)))
                            SaveCategoryOverride(editable);
                    }

                    if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(64f), GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _selectedCategoryId;
                        _status = "Copied category ID.";
                    }
                }
            }
        }

        [Obsolete("Inactive legacy layout. Metadata tab uses DrawMetadataWorkspace.", true)]
        private void DrawCategoryMode()
        {
            EditorGUILayout.HelpBox("Category mode edits category presentation and membership in one place. Primary category membership comes from Area Category; facet membership is managed through Category Facets.", MessageType.Info);

            if (position.width < 940f)
            {
                DrawCategoryList(GUILayout.ExpandWidth(true), GUILayout.MinHeight(140f), GUILayout.MaxHeight(230f));
                DrawCategoryDetails();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCategoryList(GUILayout.Width(Mathf.Clamp(position.width * 0.25f, 240f, 330f)));
                DrawCategoryDetails();
            }
        }

        private void DrawCategoryList(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple), options))
            {
                string[] categories = GetAllCategoryIds();
                UtilityWindowTheme.SectionTitle("Categories", UtilityWindowTheme.Purple, categories.Length + " ids");
                _categorySearch = EditorGUILayout.TextField(new GUIContent("Search", "Filter category IDs and display names."), _categorySearch, UtilityWindowTheme.ToolbarSearchStyle);
                _categoryListScroll = EditorGUILayout.BeginScrollView(_categoryListScroll);
                foreach (string categoryId in categories.Where(PassesCategorySearch))
                {
                    int count = PungentUtilityRegistry.All.Count(utility => utility != null && utility.HasCategory(categoryId));
                    bool selected = string.Equals(categoryId, _selectedCategoryId, StringComparison.OrdinalIgnoreCase);
                    using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Purple : UtilityWindowTheme.Neutral))
                    {
                        if (GUILayout.Button(new GUIContent(PungentUtilityCategories.GetDisplayName(categoryId) + "  (" + count + ")", categoryId), EditorStyles.miniButton, GUILayout.Height(24f)))
                        {
                            _selectedCategoryId = categoryId;
                            SavePrefs();
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCategoryDetails()
        {
            string categoryId = PungentUtilityCategories.Normalize(_selectedCategoryId);
            PungentUtilityCategoryOverrides.CategoryOverride editable = PungentUtilityCategoryOverrides.instance.CreateEditableCopy(
                categoryId,
                PungentUtilityCategories.GetFactoryDisplayName(categoryId),
                PungentUtilityCategories.GetFactoryTint(categoryId));

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Category", UtilityWindowTheme.Blue, categoryId);
                DrawGateHint();
                _categoryDetailsScroll = EditorGUILayout.BeginScrollView(_categoryDetailsScroll);

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 5, 3)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill(PungentUtilityCategoryOverrides.instance.HasOverride(categoryId) ? "Override" : "Default", PungentUtilityCategoryOverrides.instance.HasOverride(categoryId) ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 94f);
                        UtilityWindowTheme.CountPill(GetCategoryUtilities(categoryId).Count + " utilities", UtilityWindowTheme.Teal, 104f);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(70f), GUILayout.Height(22f)))
                        {
                            EditorGUIUtility.systemCopyBuffer = categoryId;
                            _status = "Copied category ID.";
                        }
                        if (GUILayout.Button(new GUIContent("Field Focus", "Review Category Facets across all utilities."), EditorStyles.miniButton, GUILayout.Width(92f), GUILayout.Height(22f)))
                        {
                            _mode = MetadataMode.FieldFocus;
                            _field = MetadataField.CategoryFacets;
                            SavePrefs();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                    {
                        editable.overrideDisplayName = EditorGUILayout.ToggleLeft(new GUIContent("Override display name", "Use a project-local category display name."), editable.overrideDisplayName);
                        using (new EditorGUI.DisabledScope(!editable.overrideDisplayName))
                            editable.displayName = EditorGUILayout.DelayedTextField("Display Name", editable.displayName ?? string.Empty);

                        editable.overrideTint = EditorGUILayout.ToggleLeft(new GUIContent("Override colour", "Use a project-local category tint."), editable.overrideTint);
                        using (new EditorGUI.DisabledScope(!editable.overrideTint))
                            editable.tint = EditorGUILayout.ColorField(new GUIContent("Colour"), editable.tint, false, false, false);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                        {
                            if (GUILayout.Button("Save Category", EditorStyles.miniButton, GUILayout.Width(112f), GUILayout.Height(24f)))
                                SaveCategoryOverride(editable);
                            if (PungentUtilityCategoryOverrides.instance.HasOverride(categoryId) && GUILayout.Button("Clear Override", EditorStyles.miniButton, GUILayout.Width(108f), GUILayout.Height(24f)))
                                ClearCategoryOverride(categoryId);
                        }
                        GUILayout.FlexibleSpace();
                    }
                }

                EditorGUILayout.Space(4f);
                UtilityWindowTheme.SectionTitle("Membership", UtilityWindowTheme.Teal, PungentUtilityCategories.GetDisplayName(categoryId));
                List<PungentUtilityDescriptor> utilities = GetCategoryUtilities(categoryId);
                if (utilities.Count == 0)
                    EditorGUILayout.HelpBox("No utilities match this category and the current metadata filters.", MessageType.Info);
                foreach (PungentUtilityDescriptor utility in utilities)
                    DrawCategoryUtilityRow(utility, categoryId);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCategoryUtilityRow(PungentUtilityDescriptor descriptor, string categoryId)
        {
            PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
            bool primary = string.Equals(descriptor.AreaCategory, categoryId, StringComparison.OrdinalIgnoreCase);
            bool included = primary || (metadata != null && metadata.overrideCategoryFacets
                ? metadata.categoryFacets != null && metadata.categoryFacets.Any(facet => string.Equals(PungentUtilityCategories.Normalize(facet), categoryId, StringComparison.OrdinalIgnoreCase))
                : descriptor.HasCategory(categoryId));

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(primary ? UtilityWindowTheme.Amber : included ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 0.07f, 0.03f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(descriptor.DisplayName, descriptor.Id), EditorStyles.miniButton, GUILayout.Width(220f), GUILayout.Height(22f)))
                    {
                        SelectUtility(descriptor.Id);
                        _mode = MetadataMode.PackageUnit;
                        SavePrefs();
                    }
                    EditorGUILayout.SelectableLabel(descriptor.Id, EditorStyles.textField, GUILayout.Height(18f), GUILayout.MinWidth(150f));
                    UtilityWindowTheme.CountPill(primary ? "Primary" : included ? "Facet" : "Off", primary ? UtilityWindowTheme.Amber : included ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 74f);
                    using (new EditorGUI.DisabledScope(primary || !PungentDeveloperMode.CanEditProjectMetadata))
                    {
                        bool next = EditorGUILayout.ToggleLeft(new GUIContent("Facet", primary ? "Primary category is controlled by Area Category." : "Toggle category facet membership."), included, GUILayout.Width(62f));
                        if (next != included)
                            SetCategoryMembership(metadata, categoryId, next);
                    }
                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata || primary))
                    {
                        if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.Width(52f), GUILayout.Height(22f)))
                            SaveMetadataOverride(descriptor, metadata);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawPackageList(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan), options))
            {
                UtilityWindowTheme.SectionTitle("Packages", UtilityWindowTheme.Cyan, PungentUtilityPackageCatalog.Records.Count + " records");
                _packageSearch = EditorGUILayout.TextField(_packageSearch, UtilityWindowTheme.ToolbarSearchStyle);
                _packageListScroll = BeginPackageUnitVerticalScroll(_packageListScroll, GUILayout.ExpandHeight(true));
                foreach (PungentUtilityPackageRecord record in GetFilteredPackages())
                {
                    bool selected = string.Equals(record.packageId, _selectedPackageId, StringComparison.OrdinalIgnoreCase);
                    using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral))
                    {
                        if (GUILayout.Button(new GUIContent(record.displayName, record.packageId), EditorStyles.miniButton, GUILayout.Height(24f)))
                        {
                            _selectedPackageId = record.packageId;
                            _packageDraft = null;
                            EnsurePackageDraft();
                            SavePrefs();
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPackageDetails(PackageUnitLayout layout)
        {
            EnsurePackageDraft();
            if (_packageDraft == null)
            {
                EditorGUILayout.HelpBox("No package is selected.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                if (layout == PackageUnitLayout.Wide)
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.Height(GetPackageUnitEditorHeight())))
                    {
                        DrawPackageRecordEditor(layout);
                        DrawPackageUtilityEditor(layout);
                    }
                }
                else
                {
                    DrawPackageRecordEditor(layout);
                    DrawPackageUtilityEditor(layout);
                }
            }
        }

        private void DrawPackageRecordEditor(PackageUnitLayout layout)
        {
            GUILayoutOption widthOption = layout == PackageUnitLayout.Wide
                ? GUILayout.Width(GetPackageRecordWidth())
                : GUILayout.ExpandWidth(true);
            GUILayoutOption heightOption = layout == PackageUnitLayout.Wide
                ? GUILayout.Height(GetPackageUnitEditorHeight())
                : GUILayout.MinHeight(GetPackageUnitEditorHeight());
            using (new EditorGUILayout.VerticalScope(widthOption, heightOption, GUILayout.ExpandHeight(layout != PackageUnitLayout.Wide)))
            {
                DrawPackageUnitSectionTitle("Package Unit", UtilityWindowTheme.Blue, _packageDraft.packageId);
                DrawGateHint();
                _packageDetailsScroll.x = 0f;
                if (layout == PackageUnitLayout.Wide)
                    _packageDetailsScroll = BeginPackageUnitVerticalScroll(_packageDetailsScroll, GUILayout.Height(GetPackageRecordScrollHeight()));
                else
                    _packageDetailsScroll = BeginPackageUnitVerticalScroll(_packageDetailsScroll, GUILayout.MinHeight(160f), GUILayout.ExpandHeight(true));

                using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                {
                    EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
                    _packageDraft.packageId = DrawPackageRecordTextField("Package ID", _packageDraft.packageId);
                    _packageDraft.displayName = DrawPackageRecordTextField("Display Name", _packageDraft.displayName);
                    _packageDraft.tier = DrawPackageRecordEnumField("Tier", _packageDraft.tier);
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Availability", EditorStyles.boldLabel);
                    _packageDraft.developerOverrideAvailability = DrawPackageRecordToggleField("Override Availability", _packageDraft.developerOverrideAvailability, "Simulate package availability in Developer Mode.");
                    using (new EditorGUI.DisabledScope(!_packageDraft.developerOverrideAvailability))
                        _packageDraft.availability = DrawPackageRecordEnumField("Availability", _packageDraft.availability);
                    DrawPackageSimulationEditor(_packageDraft.packageId);
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Distribution", EditorStyles.boldLabel);
                    _packageDraft.description = DrawPackageRecordTextArea("Description", _packageDraft.description, 54f);
                    _packageDraft.packageManagerId = DrawPackageRecordTextField("Package Manager ID", _packageDraft.packageManagerId);
                    _packageDraft.assetStoreUrl = DrawPackageRecordTextField("Asset Store URL", _packageDraft.assetStoreUrl);
                    _packageDraft.importPackagePath = DrawPackageRecordTextField("Import Package", _packageDraft.importPackagePath);
                    _packageDraft.installHint = DrawPackageRecordTextField("Install Hint", _packageDraft.installHint);
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Promotion", EditorStyles.boldLabel);
                    _packageDraft.showPromotionalPricing = DrawPackageRecordToggleField("Promotional Pricing", _packageDraft.showPromotionalPricing, "Show promotional pricing.");
                    using (new EditorGUI.DisabledScope(!_packageDraft.showPromotionalPricing))
                    {
                        _packageDraft.promotionalLabel = DrawPackageRecordTextField("Promo Label", _packageDraft.promotionalLabel);
                        _packageDraft.currencyCode = DrawPackageRecordTextField("Currency Code", _packageDraft.currencyCode);
                        _packageDraft.regularPriceText = DrawPackageRecordTextField("Regular Price", _packageDraft.regularPriceText);
                        _packageDraft.salePriceText = DrawPackageRecordTextField("Sale Price", _packageDraft.salePriceText);
                        _packageDraft.discountPercent = DrawPackageRecordSlider("Discount %", _packageDraft.discountPercent, 0, 100);
                        _packageDraft.saleEndsIsoUtc = DrawPackageRecordTextField("Sale Ends ISO UTC", _packageDraft.saleEndsIsoUtc);
                    }
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Membership", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Utility IDs", UtilityWindowTheme.MutedMiniLabelStyle);
                    _packageUtilityIdsText = DrawPackageUnitTextArea(_packageUtilityIdsText, 48f);
                    EditorGUILayout.LabelField("Capabilities", UtilityWindowTheme.MutedMiniLabelStyle);
                    _packageCapabilitiesText = DrawPackageUnitTextArea(_packageCapabilitiesText, 48f);
                }

                EditorGUILayout.EndScrollView();
                _packageDetailsScroll.x = 0f;

                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                    {
                        if (GUILayout.Button("Save Package", EditorStyles.miniButton, GUILayout.Width(104f), GUILayout.Height(24f)))
                            SavePackageRecord();
                        if (PungentUtilityPackageCatalogSettings.instance.HasOverride(_packageDraft.packageId) &&
                            GUILayout.Button("Clear Override", EditorStyles.miniButton, GUILayout.Width(108f), GUILayout.Height(24f)))
                            ClearPackageRecord();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawPackageSimulationEditor(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Package Simulation", EditorStyles.boldLabel);
            PungentUtilityPackageSimulationState current = PungentUtilityPackageSimulation.GetState(packageId);
            EditorGUI.BeginChangeCheck();
            PungentUtilityPackageSimulationState next = DrawPackageRecordEnumField("Visual State", current);
            if (EditorGUI.EndChangeCheck())
            {
                PungentUtilityPackageSimulation.SetState(packageId, next);
                _status = next == PungentUtilityPackageSimulationState.Actual ? "Cleared package simulation." : "Updated package simulation.";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(current == PungentUtilityPackageSimulationState.Actual))
                {
                    if (GUILayout.Button("Reset Package", EditorStyles.miniButton, GUILayout.Width(108f), GUILayout.Height(20f)))
                        PungentUtilityPackageSimulation.Clear(packageId);
                }
                using (new EditorGUI.DisabledScope(!PungentUtilityPackageSimulation.Active))
                {
                    if (GUILayout.Button("Clear All", EditorStyles.miniButton, GUILayout.Width(74f), GUILayout.Height(20f)))
                        PungentUtilityPackageSimulation.ClearAll();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private Vector2 BeginPackageUnitVerticalScroll(Vector2 scroll, params GUILayoutOption[] options)
        {
            scroll.x = 0f;
            Vector2 next = EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar, options);
            next.x = 0f;
            return next;
        }

        private void DrawPackageUnitSectionTitle(string title, Color tint, string pill = null)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            bool showPill = !string.IsNullOrWhiteSpace(pill) && rect.width > 230f;
            float pillWidth = showPill ? Mathf.Min(116f, rect.width * 0.36f) : 0f;
            Rect titleRect = new Rect(rect.x, rect.y, showPill ? rect.width - pillWidth - PackageUnitRowGap : rect.width, rect.height);
            EditorGUI.LabelField(titleRect, new GUIContent(title), EditorStyles.boldLabel);
            if (showPill)
            {
                Rect pillRect = new Rect(titleRect.xMax + PackageUnitRowGap, rect.y, pillWidth, rect.height);
                using (UtilityWindowTheme.Background(tint))
                    GUI.Label(pillRect, CompactPackageUnitPill(pill), EditorStyles.miniButton);
            }
        }

        private static string CompactPackageUnitPill(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            const int maxLength = 22;
            string trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength - 1) + "...";
        }

        private string DrawPackageRecordTextField(string label, string value)
        {
            Rect valueRect = DrawPackageRecordRowPrefix(label);
            return EditorGUI.DelayedTextField(valueRect, value ?? string.Empty);
        }

        private string DrawPackageRecordTextArea(string label, string value, float minHeight)
        {
            EditorGUILayout.LabelField(label, UtilityWindowTheme.MutedMiniLabelStyle);
            return DrawPackageUnitTextArea(value, minHeight);
        }

        private TEnum DrawPackageRecordEnumField<TEnum>(string label, TEnum value) where TEnum : struct
        {
            Rect valueRect = DrawPackageRecordRowPrefix(label);
            return (TEnum)(object)EditorGUI.EnumPopup(valueRect, (Enum)(object)value);
        }

        private bool DrawPackageRecordToggleField(string label, bool value, string tooltip)
        {
            Rect valueRect = DrawPackageRecordRowPrefix(label);
            valueRect.width = OverrideToggleWidth;
            return EditorGUI.Toggle(valueRect, new GUIContent(string.Empty, tooltip), value);
        }

        private int DrawPackageRecordSlider(string label, int value, int min, int max)
        {
            Rect valueRect = DrawPackageRecordRowPrefix(label);
            return EditorGUI.IntSlider(valueRect, value, min, max);
        }

        private Rect DrawPackageRecordRowPrefix(string label)
        {
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            float labelWidth = Mathf.Min(PackageRecordLabelWidth, Mathf.Max(68f, row.width * 0.38f));
            Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            Rect valueRect = new Rect(labelRect.xMax + PackageUnitRowGap, row.y, Mathf.Max(20f, row.width - labelWidth - PackageUnitRowGap), row.height);
            EditorGUI.LabelField(labelRect, new GUIContent(label));
            return valueRect;
        }

        private string DrawPackageUnitTextArea(string value, float minHeight)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, minHeight, GUILayout.ExpandWidth(true));
            return EditorGUI.TextArea(rect, value ?? string.Empty);
        }

        private void DrawPackageUtilityEditor(PackageUnitLayout layout)
        {
            GUILayoutOption heightOption = layout == PackageUnitLayout.Wide
                ? GUILayout.Height(GetPackageUnitEditorHeight())
                : GUILayout.MinHeight(GetPackageUnitEditorHeight());
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), heightOption, GUILayout.ExpandHeight(layout != PackageUnitLayout.Wide)))
            {
                PungentUtilityDescriptor selected = PungentUtilityRegistry.Find(_selectedUtilityId);
                DrawPackageUnitSectionTitle("Utilities", UtilityWindowTheme.Teal, selected == null ? _selectedPackageId : selected.DisplayName);
                if (layout == PackageUnitLayout.Wide)
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        DrawUtilityListForPackage(GUILayout.Width(GetUtilityListWidth()), GUILayout.ExpandHeight(true));
                        DrawUtilityMetadataEditor(selected);
                    }
                }
                else if (layout == PackageUnitLayout.Medium)
                {
                    DrawUtilityListForPackage(GUILayout.ExpandWidth(true), GUILayout.MinHeight(110f), GUILayout.MaxHeight(GetPackageUnitStackListHeight()));
                    DrawUtilityMetadataEditor(selected);
                }
                else
                {
                    DrawUtilityListForPackage(GUILayout.ExpandWidth(true), GUILayout.MinHeight(110f), GUILayout.MaxHeight(GetPackageUnitNarrowUtilityListHeight()));
                    DrawUtilityMetadataEditor(selected);
                }
            }
        }

        private void DrawUtilityListForPackage(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(options))
            {
                _utilityListScroll = BeginPackageUnitVerticalScroll(_utilityListScroll, GUILayout.ExpandHeight(true));
                List<PungentUtilityDescriptor> utilities = PungentUtilityRegistry.All
                    .Where(PassesUtilityFilters)
                    .Where(u => string.Equals(u.NormalizedPackageId, PungentUtilityPackageCatalog.NormalizePackageId(_selectedPackageId), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (utilities.Count == 0)
                    EditorGUILayout.HelpBox("No utilities match this package and filter set.", MessageType.Info);

                foreach (PungentUtilityDescriptor utility in utilities)
                {
                    bool selected = string.Equals(utility.Id, _selectedUtilityId, StringComparison.OrdinalIgnoreCase);
                    using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral))
                    {
                        if (GUILayout.Button(new GUIContent(utility.DisplayName, utility.Id), EditorStyles.miniButton, GUILayout.Height(22f)))
                            SelectUtility(utility.Id);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawUtilityMetadataEditor(PungentUtilityDescriptor descriptor)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                if (descriptor == null)
                {
                    EditorGUILayout.HelpBox("Select a utility to edit metadata.", MessageType.Info);
                    return;
                }

                PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
                if (metadata == null)
                    return;

                DrawMetadataPreviewCard(descriptor, metadata);
                DrawUtilityIdentity(descriptor);
                DrawGateHint();
                _metadataScroll.x = 0f;
                _metadataScroll = BeginPackageUnitVerticalScroll(_metadataScroll, GUILayout.ExpandHeight(true));
                DrawPackageUnitSectionTitle("Identity", UtilityWindowTheme.Blue);
                DrawStringOverride("Display Name", ref metadata.overrideDisplayName, ref metadata.displayName);
                DrawStringOverride("Utility Type", ref metadata.overrideUtilityType, ref metadata.utilityType);
                DrawCategoryOverride("Area Category", ref metadata.overrideAreaCategory, ref metadata.areaCategory);
                DrawStringOverride("Module", ref metadata.overrideModule, ref metadata.module);
                DrawTextOverride("Description", ref metadata.overrideDescription, ref metadata.description);
                DrawStringOverride("Menu Path", ref metadata.overrideMenuPath, ref metadata.menuPath);
                DrawListOverride("Tags", ref metadata.overrideTags, ref metadata.tags);
                DrawIntOverride("Sort Order", ref metadata.overrideSortOrder, ref metadata.sortOrder);
                DrawBrowserSurfaceEditor(descriptor, metadata);
                DrawPackageUnitSectionTitle("Package And Documentation", UtilityWindowTheme.Cyan);
                DrawPackageStatusOverride("Package Status", ref metadata.overridePackageStatus, ref metadata.packageStatus);
                DrawPackageIdOverride("Package ID", ref metadata.overridePackageId, ref metadata.packageId);
                DrawStringOverride("Package Display", ref metadata.overridePackageDisplayName, ref metadata.packageDisplayName);
                DrawEnumOverride("Package Tier", ref metadata.overridePackageTier, ref metadata.packageTier);
                DrawStringOverride("Asset Store URL", ref metadata.overrideAssetStoreUrl, ref metadata.assetStoreUrl);
                DrawStringOverride("Package Manager ID", ref metadata.overridePackageManagerId, ref metadata.packageManagerId);
                DrawStringOverride("Import Package Path", ref metadata.overrideImportPackagePath, ref metadata.importPackagePath);
                DrawTextOverride("Documentation Topic", ref metadata.overrideDocumentationTopicId, ref metadata.documentationTopicId);
                DrawPackageUnitSectionTitle("Relationships", UtilityWindowTheme.Purple);
                DrawListOverride("Provided Capabilities", ref metadata.overrideProvidedCapabilities, ref metadata.providedCapabilities);
                DrawPackageIdListOverride("Required Packages", ref metadata.overrideRequiredPackageIds, ref metadata.requiredPackageIds);
                DrawPackageIdListOverride("Optional Packages", ref metadata.overrideOptionalPackageIds, ref metadata.optionalPackageIds);
                DrawUtilityIdListOverride("Related Utilities", ref metadata.overrideRelatedUtilityIds, ref metadata.relatedUtilityIds);
                DrawCategoryFacetsOverride("Category Facets", ref metadata.overrideCategoryFacets, ref metadata.categoryFacets);
                DrawPackageUnitSectionTitle("Visibility And Behaviour", UtilityWindowTheme.Amber);
                DrawEnumOverride("Item Kind", ref metadata.overrideItemKind, ref metadata.itemKind);
                DrawEnumOverride("Visibility", ref metadata.overrideVisibility, ref metadata.visibility);
                DrawBoolOverride("Show In Browser", ref metadata.overrideShowInUtilitiesBrowser, ref metadata.showInUtilitiesBrowser);
                DrawBoolOverride("Scene Overlay", ref metadata.overrideSupportsSceneOverlay, ref metadata.supportsSceneOverlay);
                DrawBoolOverride("Context Menu", ref metadata.overrideSupportsContextMenu, ref metadata.supportsContextMenu);
                DrawBoolOverride("Selection", ref metadata.overrideSupportsSelection, ref metadata.supportsSelection);
                DrawBoolOverride("Lab Hub", ref metadata.overrideIsLabHub, ref metadata.isLabHub);
                DrawTextOverride("Missing Dependency", ref metadata.overrideMissingDependencyMessage, ref metadata.missingDependencyMessage);
                DrawTextOverride("Install Hint", ref metadata.overrideInstallHint, ref metadata.installHint);
                EditorGUILayout.EndScrollView();
                _metadataScroll.x = 0f;

                DrawMetadataFooter(descriptor, metadata);
            }
        }

        private void DrawUtilityIdentity(PungentUtilityDescriptor descriptor)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 4, 2)))
            {
                EditorGUILayout.SelectableLabel(descriptor.Id, EditorStyles.textField, GUILayout.Height(18f));
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    UtilityWindowTheme.CountPill(descriptor.ItemKind.ToString(), UtilityWindowTheme.Cyan, 86f);
                    UtilityWindowTheme.CountPill(descriptor.Visibility.ToString(), descriptor.IsDeveloperOnly ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 96f);
                    if (PungentUtilityRegistry.HasMetadataOverride(descriptor.Id))
                        UtilityWindowTheme.CountPill("Override", UtilityWindowTheme.Amber, 76f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawBrowserSurfaceEditor(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.08f, 0.035f, 5, 3)))
            {
                DrawPackageUnitSectionTitle("Browser Surface", UtilityWindowTheme.Teal, descriptor.BrowserRole + " / priority " + descriptor.BrowserPriority);
                DrawEnumOverride("Browser Role", ref metadata.overrideBrowserRole, ref metadata.browserRole);
                DrawIntOverride("Browser Priority", ref metadata.overrideBrowserPriority, ref metadata.browserPriority);
                DrawUtilityIdOverride("Parent Utility ID", ref metadata.overrideParentUtilityId, ref metadata.parentUtilityId);
                DrawEnumOverride("Prominence", ref metadata.overrideBrowserProminence, ref metadata.browserProminence);
                DrawColorOverride("Card Tint", ref metadata.overrideBrowserCardTint, ref metadata.browserCardTint);
                DrawCardTintQuickActions(descriptor, metadata);
                DrawListOverride("Browser Tags", ref metadata.overrideBrowserTags, ref metadata.browserTags);
                DrawUtilityIdListOverride("Accessory IDs", ref metadata.overrideAccessoryUtilityIds, ref metadata.accessoryUtilityIds);
                DrawUtilityIdListOverride("Action IDs", ref metadata.overrideActionIds, ref metadata.actionIds);
                DrawBrowserSurfaceWarnings(descriptor, metadata);
            }
        }

        private void DrawMetadataPreviewCard(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            string displayName = metadata.overrideDisplayName ? metadata.displayName : descriptor.DisplayName;
            string categoryId = metadata.overrideAreaCategory ? metadata.areaCategory : descriptor.AreaCategory;
            string packageId = metadata.overridePackageId ? metadata.packageId : descriptor.NormalizedPackageId;
            string packageStatus = metadata.overridePackageStatus ? metadata.packageStatus : descriptor.PackageStatus;
            PungentUtilityBrowserRole role = metadata.overrideBrowserRole ? metadata.browserRole : descriptor.BrowserRole;
            PungentUtilityBrowserProminence prominence = metadata.overrideBrowserProminence ? metadata.browserProminence : descriptor.BrowserProminence;
            PungentUtilityVisibility visibility = metadata.overrideVisibility ? metadata.visibility : descriptor.Visibility;
            Color categoryTint = PungentUtilityCategories.GetTint(categoryId);
            Color tint = metadata.overrideBrowserCardTint ? metadata.browserCardTint : descriptor.OverrideBrowserCardTint ? descriptor.BrowserCardTint : categoryTint;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.18f, 0.06f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(string.IsNullOrWhiteSpace(displayName) ? descriptor.Id : displayName, UtilityWindowTheme.TitleStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(metadata.overrideBrowserCardTint ? "Card Tint" : "Category Tint", metadata.overrideBrowserCardTint ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 96f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(PungentUtilityCategories.GetDisplayName(categoryId), categoryTint, 112f);
                    UtilityWindowTheme.CountPill(string.IsNullOrWhiteSpace(packageId) ? "No Package" : packageId, UtilityWindowTheme.Cyan, 150f);
                    UtilityWindowTheme.CountPill(string.IsNullOrWhiteSpace(packageStatus) ? "No Status" : packageStatus, UtilityWindowTheme.Amber, 108f);
                    UtilityWindowTheme.CountPill(role.ToString(), UtilityWindowTheme.Neutral, 100f);
                    UtilityWindowTheme.CountPill(prominence.ToString(), UtilityWindowTheme.Purple, 100f);
                    UtilityWindowTheme.CountPill(visibility.ToString(), visibility == PungentUtilityVisibility.Hidden || visibility == PungentUtilityVisibility.Archived ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 100f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCardTintQuickActions(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            Color categoryTint = PungentUtilityCategories.GetTint(metadata.overrideAreaCategory ? metadata.areaCategory : descriptor.AreaCategory);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(OverrideToggleWidth + PackageUnitRowGap);
                using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                {
                    if (GUILayout.Button(new GUIContent("Use Category Tint", "Enable a project-local card tint override using the current category tint."), EditorStyles.miniButton, GUILayout.Width(118f), GUILayout.Height(20f)))
                    {
                        metadata.overrideBrowserCardTint = true;
                        metadata.browserCardTint = categoryTint;
                    }

                    if (GUILayout.Button(new GUIContent("Copy Category Tint", "Copy the category tint hex value to the clipboard."), EditorStyles.miniButton, GUILayout.Width(116f), GUILayout.Height(20f)))
                        EditorGUIUtility.systemCopyBuffer = "#" + ColorUtility.ToHtmlStringRGB(categoryTint);

                    if (metadata.overrideBrowserCardTint && GUILayout.Button(new GUIContent("Clear Tint", "Clear the project-local card tint override."), EditorStyles.miniButton, GUILayout.Width(74f), GUILayout.Height(20f)))
                    {
                        metadata.overrideBrowserCardTint = false;
                        metadata.browserCardTint = categoryTint;
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawBrowserSurfaceWarnings(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            string parentId = metadata.overrideParentUtilityId ? metadata.parentUtilityId : descriptor.ParentUtilityId;
            PungentUtilityBrowserRole role = metadata.overrideBrowserRole ? metadata.browserRole : descriptor.BrowserRole;
            PungentUtilityBrowserProminence prominence = metadata.overrideBrowserProminence ? metadata.browserProminence : descriptor.BrowserProminence;

            if ((role == PungentUtilityBrowserRole.Action || role == PungentUtilityBrowserRole.AccessoryUtility) && string.IsNullOrWhiteSpace(parentId))
                EditorGUILayout.HelpBox("Action/accessory utilities should have a parent utility ID so the Browser can place them in a selected-card drawer.", MessageType.Warning);

            if (!string.IsNullOrWhiteSpace(parentId) && PungentUtilityRegistry.Find(parentId) == null)
                EditorGUILayout.HelpBox("Parent utility ID is not currently registered: " + parentId, MessageType.Warning);

            int priority = metadata.overrideBrowserPriority ? metadata.browserPriority : descriptor.BrowserPriority;
            bool duplicatePriority = PungentUtilityRegistry.All.Any(other =>
                other != null &&
                !string.Equals(other.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase) &&
                other.BrowserPriority == priority &&
                string.Equals(other.NormalizedPackageId, descriptor.NormalizedPackageId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(PungentUtilityRegistry.GetAreaCategory(other), PungentUtilityRegistry.GetAreaCategory(descriptor), StringComparison.OrdinalIgnoreCase));
            if (duplicatePriority)
                EditorGUILayout.HelpBox("Another utility in this package/category has the same browser priority. That is allowed, but ordering will fall back to category/type/name.", MessageType.Info);

            string[] related = metadata.overrideRelatedUtilityIds ? metadata.relatedUtilityIds : descriptor.RelatedUtilityIds;
            DrawMissingUtilityIdWarnings("Missing related utility ID", related);
            DrawMissingUtilityIdWarnings("Missing accessory utility ID", metadata.overrideAccessoryUtilityIds ? metadata.accessoryUtilityIds : descriptor.AccessoryUtilityIds);
            DrawMissingUtilityIdWarnings("Missing action ID", metadata.overrideActionIds ? metadata.actionIds : descriptor.ActionIds);

            if (prominence == PungentUtilityBrowserProminence.DeveloperOnly && descriptor.Visibility != PungentUtilityVisibility.DeveloperOnly)
                EditorGUILayout.HelpBox("Developer-only prominence is clearer when paired with DeveloperOnly visibility or internal role.", MessageType.Info);

            if (string.IsNullOrWhiteSpace(descriptor.DocumentationTopicId) && descriptor.IsVisibleInDefaultBrowser && role == PungentUtilityBrowserRole.CoreUtility)
                EditorGUILayout.HelpBox("Product-facing core utilities should usually have a documentation topic ID.", MessageType.Info);

            if (descriptor.IsPackageGated && string.IsNullOrWhiteSpace(descriptor.MissingDependencyMessage))
                EditorGUILayout.HelpBox("Package-gated utilities should include a missing dependency message.", MessageType.Info);
        }

        private static void DrawMissingUtilityIdWarnings(string label, IEnumerable<string> ids)
        {
            if (ids == null)
                return;

            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id) && PungentUtilityRegistry.Find(id) == null)
                    EditorGUILayout.HelpBox(label + ": " + id, MessageType.Warning);
            }
        }

        [Obsolete("Inactive legacy layout. Metadata tab uses DrawMetadataWorkspace.", true)]
        private void DrawFieldFocusMode()
        {
            EditorGUILayout.HelpBox("Field Focus mode reviews one metadata field across matching utilities. It generalizes the old category reference workflow to package IDs, docs topics, tags, relationships, visibility, capabilities, and browser metadata.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.10f, 0.04f, 5, 4)))
            {
                _field = (MetadataField)EditorGUILayout.EnumPopup(new GUIContent("Field", "Show one metadata field across matching utilities."), _field, GUILayout.Width(260f));
                if (_field == MetadataField.CategoryFacets)
                {
                    string[] categories = GetAllCategoryIds();
                    int index = Mathf.Max(0, Array.FindIndex(categories, c => string.Equals(c, _selectedCategoryId, StringComparison.OrdinalIgnoreCase)));
                    int next = EditorGUILayout.Popup(new GUIContent("Category", "Category reference context for membership editing."), index, categories.Select(c => PungentUtilityCategories.GetDisplayName(c)).ToArray(), GUILayout.Width(260f));
                    if (next >= 0 && next < categories.Length)
                        _selectedCategoryId = categories[next];
                    if (GUILayout.Button(new GUIContent("Category Mode", "Open the full category metadata and membership workflow."), EditorStyles.miniButton, GUILayout.Width(112f), GUILayout.Height(22f)))
                    {
                        _mode = MetadataMode.Category;
                        SavePrefs();
                    }
                }
                GUILayout.FlexibleSpace();
            }

            _fieldScroll = EditorGUILayout.BeginScrollView(_fieldScroll);
            List<PungentUtilityDescriptor> utilities = PungentUtilityRegistry.All
                .Where(PassesUtilityFilters)
                .OrderBy(u => u.NormalizedPackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (utilities.Count == 0)
                EditorGUILayout.HelpBox("No utilities match the current field-focus filters.", MessageType.Info);

            foreach (PungentUtilityDescriptor utility in utilities)
                DrawFieldFocusRow(utility);

            EditorGUILayout.EndScrollView();
            SavePrefs();
        }

        private void DrawFieldFocusRow(PungentUtilityDescriptor descriptor)
        {
            PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata = GetMetadataDraft(descriptor);
            if (metadata == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.07f, 0.03f, 4, 2)))
            {
                if (position.width < 980f)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(descriptor.DisplayName, descriptor.Id), EditorStyles.miniButton, GUILayout.Width(220f), GUILayout.Height(22f)))
                        {
                            SelectUtility(descriptor.Id);
                            _mode = MetadataMode.PackageUnit;
                        }
                        EditorGUILayout.SelectableLabel(descriptor.Id, EditorStyles.textField, GUILayout.Height(18f), GUILayout.MinWidth(150f));
                        DrawFieldSourceChip(descriptor, metadata);
                    }
                    DrawFocusedFieldControl(descriptor, metadata);
                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                    {
                        if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.Width(52f), GUILayout.Height(22f)))
                            SaveMetadataOverride(descriptor, metadata);
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(descriptor.DisplayName, descriptor.Id), EditorStyles.miniButton, GUILayout.Width(220f), GUILayout.Height(22f)))
                        {
                            SelectUtility(descriptor.Id);
                            _mode = MetadataMode.PackageUnit;
                        }
                        EditorGUILayout.SelectableLabel(descriptor.Id, EditorStyles.textField, GUILayout.Height(18f), GUILayout.Width(210f));
                        DrawFieldSourceChip(descriptor, metadata);
                        DrawFocusedFieldControl(descriptor, metadata);
                        using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                        {
                            if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.Width(52f), GUILayout.Height(22f)))
                                SaveMetadataOverride(descriptor, metadata);
                        }
                    }
                }
            }
        }

        private void DrawGateHint()
        {
            if (PungentDeveloperMode.CanEditProjectMetadata)
                EditorGUILayout.LabelField("Editing project-local overrides. Registry/package defaults remain the source baseline.", UtilityWindowTheme.MutedMiniLabelStyle);
            else
                EditorGUILayout.HelpBox("Metadata editing is read-only while Developer Mode is unavailable, Unity is compiling, or Unity is updating.", MessageType.Warning);
        }

        private void DrawFieldSourceChip(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            bool overridden = FieldHasOverride(metadata);
            bool missing = string.IsNullOrWhiteSpace(GetFieldValue(descriptor));
            Color tint = overridden ? UtilityWindowTheme.Amber : missing ? UtilityWindowTheme.Red : UtilityWindowTheme.Neutral;
            string label = overridden ? "Override" : missing ? "Missing" : "Default";
            if (!PungentDeveloperMode.CanEditProjectMetadata)
            {
                label += " / Locked";
                tint = UtilityWindowTheme.Neutral;
            }
            UtilityWindowTheme.CountPill(label, tint, 104f);
        }

        private bool FieldHasOverride(PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            if (metadata == null)
                return false;

            switch (_field)
            {
                case MetadataField.DisplayName: return metadata.overrideDisplayName;
                case MetadataField.UtilityType: return metadata.overrideUtilityType;
                case MetadataField.AreaCategory: return metadata.overrideAreaCategory;
                case MetadataField.Module: return metadata.overrideModule;
                case MetadataField.Description: return metadata.overrideDescription;
                case MetadataField.MenuPath: return metadata.overrideMenuPath;
                case MetadataField.Tags: return metadata.overrideTags;
                case MetadataField.SortOrder: return metadata.overrideSortOrder;
                case MetadataField.PackageStatus: return metadata.overridePackageStatus;
                case MetadataField.PackageId: return metadata.overridePackageId;
                case MetadataField.PackageDisplayName: return metadata.overridePackageDisplayName;
                case MetadataField.PackageTier: return metadata.overridePackageTier;
                case MetadataField.AssetStoreUrl: return metadata.overrideAssetStoreUrl;
                case MetadataField.PackageManagerId: return metadata.overridePackageManagerId;
                case MetadataField.ImportPackagePath: return metadata.overrideImportPackagePath;
                case MetadataField.ProvidedCapabilities: return metadata.overrideProvidedCapabilities;
                case MetadataField.RequiredPackages: return metadata.overrideRequiredPackageIds;
                case MetadataField.OptionalPackages: return metadata.overrideOptionalPackageIds;
                case MetadataField.RelatedUtilities: return metadata.overrideRelatedUtilityIds;
                case MetadataField.CategoryFacets: return metadata.overrideCategoryFacets;
                case MetadataField.DocumentationTopicId: return metadata.overrideDocumentationTopicId;
                case MetadataField.ItemKind: return metadata.overrideItemKind;
                case MetadataField.Visibility: return metadata.overrideVisibility;
                case MetadataField.ShowInUtilitiesBrowser: return metadata.overrideShowInUtilitiesBrowser;
                case MetadataField.MissingDependencyMessage: return metadata.overrideMissingDependencyMessage;
                case MetadataField.InstallHint: return metadata.overrideInstallHint;
                case MetadataField.SupportsSceneOverlay: return metadata.overrideSupportsSceneOverlay;
                case MetadataField.SupportsContextMenu: return metadata.overrideSupportsContextMenu;
                case MetadataField.SupportsSelection: return metadata.overrideSupportsSelection;
                case MetadataField.IsLabHub: return metadata.overrideIsLabHub;
                case MetadataField.BrowserRole: return metadata.overrideBrowserRole;
                case MetadataField.BrowserPriority: return metadata.overrideBrowserPriority;
                case MetadataField.ParentUtilityId: return metadata.overrideParentUtilityId;
                case MetadataField.BrowserProminence: return metadata.overrideBrowserProminence;
                case MetadataField.BrowserCardTint: return metadata.overrideBrowserCardTint;
                case MetadataField.BrowserTags: return metadata.overrideBrowserTags;
                case MetadataField.AccessoryUtilities: return metadata.overrideAccessoryUtilityIds;
                case MetadataField.ActionIds: return metadata.overrideActionIds;
                default: return false;
            }
        }

        private void DrawFocusedFieldControl(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            switch (_field)
            {
                case MetadataField.DisplayName: DrawStringOverride(string.Empty, ref metadata.overrideDisplayName, ref metadata.displayName, true); break;
                case MetadataField.UtilityType: DrawStringOverride(string.Empty, ref metadata.overrideUtilityType, ref metadata.utilityType, true); break;
                case MetadataField.AreaCategory: DrawCategoryOverride(string.Empty, ref metadata.overrideAreaCategory, ref metadata.areaCategory, true); break;
                case MetadataField.Module: DrawStringOverride(string.Empty, ref metadata.overrideModule, ref metadata.module, true); break;
                case MetadataField.Description: DrawTextOverride(string.Empty, ref metadata.overrideDescription, ref metadata.description, true); break;
                case MetadataField.MenuPath: DrawStringOverride(string.Empty, ref metadata.overrideMenuPath, ref metadata.menuPath, true); break;
                case MetadataField.Tags: DrawListOverride(string.Empty, ref metadata.overrideTags, ref metadata.tags, true); break;
                case MetadataField.SortOrder: DrawIntOverride(string.Empty, ref metadata.overrideSortOrder, ref metadata.sortOrder, true); break;
                case MetadataField.BrowserRole: DrawEnumOverride(string.Empty, ref metadata.overrideBrowserRole, ref metadata.browserRole, true); break;
                case MetadataField.BrowserPriority: DrawIntOverride(string.Empty, ref metadata.overrideBrowserPriority, ref metadata.browserPriority, true); break;
                case MetadataField.ParentUtilityId: DrawUtilityIdOverride(string.Empty, ref metadata.overrideParentUtilityId, ref metadata.parentUtilityId, true); break;
                case MetadataField.BrowserProminence: DrawEnumOverride(string.Empty, ref metadata.overrideBrowserProminence, ref metadata.browserProminence, true); break;
                case MetadataField.BrowserCardTint: DrawColorOverride(string.Empty, ref metadata.overrideBrowserCardTint, ref metadata.browserCardTint, true); break;
                case MetadataField.BrowserTags: DrawListOverride(string.Empty, ref metadata.overrideBrowserTags, ref metadata.browserTags, true); break;
                case MetadataField.AccessoryUtilities: DrawUtilityIdListOverride(string.Empty, ref metadata.overrideAccessoryUtilityIds, ref metadata.accessoryUtilityIds, true); break;
                case MetadataField.ActionIds: DrawUtilityIdListOverride(string.Empty, ref metadata.overrideActionIds, ref metadata.actionIds, true); break;
                case MetadataField.PackageStatus: DrawPackageStatusOverride(string.Empty, ref metadata.overridePackageStatus, ref metadata.packageStatus, true); break;
                case MetadataField.PackageId: DrawPackageIdOverride(string.Empty, ref metadata.overridePackageId, ref metadata.packageId, true); break;
                case MetadataField.PackageDisplayName: DrawStringOverride(string.Empty, ref metadata.overridePackageDisplayName, ref metadata.packageDisplayName, true); break;
                case MetadataField.PackageTier: DrawEnumOverride(string.Empty, ref metadata.overridePackageTier, ref metadata.packageTier, true); break;
                case MetadataField.AssetStoreUrl: DrawStringOverride(string.Empty, ref metadata.overrideAssetStoreUrl, ref metadata.assetStoreUrl, true); break;
                case MetadataField.PackageManagerId: DrawStringOverride(string.Empty, ref metadata.overridePackageManagerId, ref metadata.packageManagerId, true); break;
                case MetadataField.ImportPackagePath: DrawStringOverride(string.Empty, ref metadata.overrideImportPackagePath, ref metadata.importPackagePath, true); break;
                case MetadataField.ProvidedCapabilities: DrawListOverride(string.Empty, ref metadata.overrideProvidedCapabilities, ref metadata.providedCapabilities, true); break;
                case MetadataField.RequiredPackages: DrawPackageIdListOverride(string.Empty, ref metadata.overrideRequiredPackageIds, ref metadata.requiredPackageIds, true); break;
                case MetadataField.OptionalPackages: DrawPackageIdListOverride(string.Empty, ref metadata.overrideOptionalPackageIds, ref metadata.optionalPackageIds, true); break;
                case MetadataField.RelatedUtilities: DrawUtilityIdListOverride(string.Empty, ref metadata.overrideRelatedUtilityIds, ref metadata.relatedUtilityIds, true); break;
                case MetadataField.CategoryFacets: DrawCategoryMembershipFocus(descriptor, metadata); break;
                case MetadataField.DocumentationTopicId: DrawStringOverride(string.Empty, ref metadata.overrideDocumentationTopicId, ref metadata.documentationTopicId, true); break;
                case MetadataField.ItemKind: DrawEnumOverride(string.Empty, ref metadata.overrideItemKind, ref metadata.itemKind, true); break;
                case MetadataField.Visibility: DrawEnumOverride(string.Empty, ref metadata.overrideVisibility, ref metadata.visibility, true); break;
                case MetadataField.ShowInUtilitiesBrowser: DrawBoolOverride(string.Empty, ref metadata.overrideShowInUtilitiesBrowser, ref metadata.showInUtilitiesBrowser, true); break;
                case MetadataField.MissingDependencyMessage: DrawTextOverride(string.Empty, ref metadata.overrideMissingDependencyMessage, ref metadata.missingDependencyMessage, true); break;
                case MetadataField.InstallHint: DrawTextOverride(string.Empty, ref metadata.overrideInstallHint, ref metadata.installHint, true); break;
                case MetadataField.SupportsSceneOverlay: DrawBoolOverride(string.Empty, ref metadata.overrideSupportsSceneOverlay, ref metadata.supportsSceneOverlay, true); break;
                case MetadataField.SupportsContextMenu: DrawBoolOverride(string.Empty, ref metadata.overrideSupportsContextMenu, ref metadata.supportsContextMenu, true); break;
                case MetadataField.SupportsSelection: DrawBoolOverride(string.Empty, ref metadata.overrideSupportsSelection, ref metadata.supportsSelection, true); break;
                case MetadataField.IsLabHub: DrawBoolOverride(string.Empty, ref metadata.overrideIsLabHub, ref metadata.isLabHub, true); break;
                default: EditorGUILayout.LabelField(GetFieldValue(descriptor), UtilityWindowTheme.MutedMiniLabelStyle); break;
            }
        }

        private void DrawCategoryMembershipFocus(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            bool primaryLocked = string.Equals(descriptor.AreaCategory, _selectedCategoryId, StringComparison.OrdinalIgnoreCase);
            bool included = primaryLocked || (metadata != null && metadata.overrideCategoryFacets
                ? metadata.categoryFacets != null && metadata.categoryFacets.Any(facet => string.Equals(PungentUtilityCategories.Normalize(facet), _selectedCategoryId, StringComparison.OrdinalIgnoreCase))
                : descriptor.HasCategory(_selectedCategoryId));
            using (new EditorGUI.DisabledScope(primaryLocked || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                bool next = EditorGUILayout.ToggleLeft(new GUIContent(PungentUtilityCategories.GetDisplayName(_selectedCategoryId), primaryLocked ? "Primary category membership is controlled by Area Category." : "Toggle category facet membership."), included, GUILayout.Width(190f));
                if (next != included)
                    SetCategoryMembership(metadata, _selectedCategoryId, next);
            }
            UtilityWindowTheme.CountPill(primaryLocked ? "Primary" : included ? "Facet" : "Off", primaryLocked ? UtilityWindowTheme.Amber : included ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 70f);
        }

        private void DrawGeneration()
        {
            _generationScroll = EditorGUILayout.BeginScrollView(_generationScroll);
            EnsureGenerationReport();
            PungentUtilityHelpGenerationReport report = _generationReport;
            List<PungentUtilityHelpReviewRow> reviewRows = _generationReviewRows;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Generation Dashboard", UtilityWindowTheme.Purple, PungentUtilityHelpStorage.instance.lastGeneratedStatus);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Generated " + report.generatedDraftTopics, UtilityWindowTheme.Purple, 120f);
                    UtilityWindowTheme.CountPill("Review " + report.generatedEntriesAwaitingReview, UtilityWindowTheme.Amber, 112f);
                    UtilityWindowTheme.CountPill("Stale API " + report.staleScriptingEntries, report.staleScriptingEntries == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Red, 112f);
                    UtilityWindowTheme.CountPill("Doc Issues " + (report.documentationLinksMissingCurrentTargets + report.staleRelatedDocumentationLinkIds), UtilityWindowTheme.Cyan, 112f);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Refresh Report", "Rebuild the cached generation coverage report."), EditorStyles.miniButton, GUILayout.Width(112f), GUILayout.Height(22f)))
                        RefreshGenerationReport();
                }

                DrawGenerationActions(report);
            }

            DrawDocumentationTestData();
            DrawHelpCoverageSummary(report);
            DrawHelpReviewQueues(report, reviewRows);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Generated Topic Preview", UtilityWindowTheme.Teal, "first 40");
                List<PungentUtilityHelpTopic> topics = PungentUtilityHelpRegistry.AllTopics
                    .Where(t => t != null && (t.generated || (t.scriptingEntries != null && t.scriptingEntries.Any(e => e != null && e.generated))))
                    .OrderBy(t => t.utilityId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.topicId, StringComparer.OrdinalIgnoreCase)
                    .Take(40)
                    .ToList();

                if (topics.Count == 0)
                    EditorGUILayout.HelpBox("No generated topics are currently registered.", MessageType.Info);

                foreach (PungentUtilityHelpTopic topic in topics)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(topic.StableId, EditorStyles.miniButton, GUILayout.Width(320f), GUILayout.Height(20f)))
                            PungentUtilityHelpBrowserWindow.Open(topic.utilityId, topic.sectionId, topic.topicId);
                        EditorGUILayout.LabelField(topic.sourceOwner, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(180f));
                        int entries = topic.scriptingEntries == null ? 0 : topic.scriptingEntries.Count;
                        int stale = topic.scriptingEntries == null ? 0 : topic.scriptingEntries.Count(e => e != null && e.stale);
                        EditorGUILayout.LabelField(entries + " entries" + (stale > 0 ? ", " + stale + " stale" : string.Empty), UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Generation Rules", UtilityWindowTheme.Amber, "safe by default");
                EditorGUILayout.LabelField("Manual/curated help remains authoritative. Generated output is explicit, reviewable, and never runs from repaint.", UtilityWindowTheme.BodyStyle);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawGenerationActions(PungentUtilityHelpGenerationReport report)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.06f, 0.025f, 4, 2)))
            {
                UtilityWindowTheme.SectionTitle("Safe Actions", UtilityWindowTheme.Neutral);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Help Browser", GUILayout.Width(142f), GUILayout.Height(26f)))
                        PungentUtilityHelpBrowserWindow.Open();
                    if (GUILayout.Button("Refresh Generated Index", GUILayout.Width(172f), GUILayout.Height(26f)))
                    {
                        int count = PungentUtilityHelpAutoIndexer.RefreshGeneratedIndex(out string status);
                        _status = count == 0 ? "Generated index refreshed; no public scripting entries matched." : status;
                        PungentUtilityHelpRegistry.Rebuild();
                        RefreshGenerationReport();
                    }
                    GUILayout.FlexibleSpace();
                }
            }

            bool canWriteGeneratedHelp = CanWriteGeneratedHelp();
            using (new EditorGUI.DisabledScope(!canWriteGeneratedHelp))
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.08f, 0.035f, 4, 2)))
                {
                    UtilityWindowTheme.SectionTitle("Generated Help Writes", UtilityWindowTheme.Cyan, canWriteGeneratedHelp ? "enabled" : "locked");
                    EditorGUILayout.LabelField("These actions write generated documentation metadata or generated topic entries and should be reviewed before shipping.", UtilityWindowTheme.MutedMiniLabelStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Generate Registry Topics", EditorStyles.miniButton, GUILayout.Width(164f), GUILayout.Height(22f)) &&
                            ConfirmBroadGeneration("Generate registry overview draft topics?"))
                        {
                            int count = PungentUtilityHelpTopicGenerator.GenerateRegistryOverviewTopics(out string status);
                            _status = status + " Count: " + count + ".";
                            RefreshGenerationReport();
                        }
                        if (GUILayout.Button("Index Help Buttons", EditorStyles.miniButton, GUILayout.Width(136f), GUILayout.Height(22f)))
                        {
                            List<PungentUtilityHelpContext> contexts = PungentUtilityHelpTopicGenerator.RefreshContextualHelpButtonIndex(out string status);
                            _status = status + " Contexts: " + (contexts == null ? 0 : contexts.Count) + ".";
                            RefreshGenerationReport();
                        }
                        if (GUILayout.Button("Generate Missing Topic Stubs", EditorStyles.miniButton, GUILayout.Width(190f), GUILayout.Height(22f)) &&
                            ConfirmBroadGeneration("Generate missing contextual help topic stubs?"))
                        {
                            int count = PungentUtilityHelpTopicGenerator.GenerateMissingContextualTopicStubs(out string status);
                            _status = status + " Count: " + count + ".";
                            RefreshGenerationReport();
                        }
                        if (GUILayout.Button("Refresh Tooltip Index", EditorStyles.miniButton, GUILayout.Width(150f), GUILayout.Height(22f)))
                        {
                            int count = PungentUtilityHelpTooltipIndexer.RefreshTooltipIndex(out string status);
                            _status = status + " Count: " + count + ".";
                            RefreshGenerationReport();
                        }
                        GUILayout.FlexibleSpace();
                    }
                }

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Red, 0.08f, 0.035f, 4, 2)))
                {
                    UtilityWindowTheme.SectionTitle("Cleanup", UtilityWindowTheme.Red, report.staleScriptingEntries + " stale");
                    using (new EditorGUI.DisabledScope(report.staleScriptingEntries == 0))
                    {
                        if (GUILayout.Button("Remove Stale Entries", GUILayout.Width(154f), GUILayout.Height(26f)) &&
                            EditorUtility.DisplayDialog("Remove stale generated help?", "Remove stale generated scripting entries that no longer match source members?", "Remove", "Cancel"))
                        {
                            int removed = PungentUtilityHelpAutoIndexer.RemoveStaleGeneratedEntries(out string status);
                            _status = status + " Removed: " + removed + ".";
                            PungentUtilityHelpRegistry.Rebuild();
                            RefreshGenerationReport();
                        }
                    }
                }
            }
        }

        private void DrawDocumentationTestData()
        {
            IReadOnlyList<PungentDocumentationRecord> allRecords = PungentDocumentationTestDataRegistry.AllRecords();
            IReadOnlyList<PungentGeneratedDocumentTemplate> templates = PungentDocumentationTestDataRegistry.AllTemplates();
            HashSet<string> availableKeys = new HashSet<string>(allRecords.Select(record => record.Key), StringComparer.OrdinalIgnoreCase);
            _documentationTestDataSelectedKeys.RemoveWhere(key => !availableKeys.Contains(key));

            List<PungentDocumentationRecord> records = allRecords
                .Where(record => string.IsNullOrWhiteSpace(_documentationTestDataSearch) ||
                                 ContainsIgnoreCase(record.title, _documentationTestDataSearch) ||
                                 ContainsIgnoreCase(record.typeLabel, _documentationTestDataSearch) ||
                                 ContainsIgnoreCase(record.generatedTemplateId, _documentationTestDataSearch))
                .ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Documentation Test Data", UtilityWindowTheme.Blue, "Developer Mode");
                EditorGUILayout.LabelField("Generated test records are removable and replaceable. Locked records are skipped until explicitly unlocked.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Records " + allRecords.Count, UtilityWindowTheme.Blue, 104f);
                    UtilityWindowTheme.CountPill("Generated " + allRecords.Count(record => record.generated), UtilityWindowTheme.Purple, 116f);
                    UtilityWindowTheme.CountPill("Locked " + allRecords.Count(record => record.locked), UtilityWindowTheme.Amber, 96f);
                    UtilityWindowTheme.CountPill("Selected " + _documentationTestDataSelectedKeys.Count, UtilityWindowTheme.Cyan, 106f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(48f));
                    _documentationTestDataSearch = EditorGUILayout.TextField(_documentationTestDataSearch ?? string.Empty, GUILayout.MinWidth(180f), GUILayout.MaxWidth(320f));
                    if (GUILayout.Button("Select Visible", EditorStyles.miniButton, GUILayout.Width(96f)))
                    {
                        foreach (PungentDocumentationRecord record in records)
                            _documentationTestDataSelectedKeys.Add(record.Key);
                    }
                    if (GUILayout.Button("Clear Selection", EditorStyles.miniButton, GUILayout.Width(104f)))
                        _documentationTestDataSelectedKeys.Clear();
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUI.DisabledScope(!CanWriteGeneratedHelp()))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent("Generate Missing", "Create generated test records for templates that do not already have generated records."), EditorStyles.miniButton, GUILayout.Width(124f)) &&
                            ConfirmBroadGeneration("Generate missing documentation test records?"))
                            ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.GenerateMissing());

                        if (GUILayout.Button(new GUIContent("Regenerate Unlocked", "Replace generated records for all templates. Locked records are skipped."), EditorStyles.miniButton, GUILayout.Width(136f)) &&
                            ConfirmBroadGeneration("Regenerate all unlocked documentation test records?"))
                            ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.RegenerateUnlocked());

                        if (GUILayout.Button("By Type", EditorStyles.miniButton, GUILayout.Width(74f)))
                            ShowDocumentationTestDataTypeMenu(false);

                        if (GUILayout.Button("Regenerate Type", EditorStyles.miniButton, GUILayout.Width(112f)))
                            ShowDocumentationTestDataTypeMenu(true);

                        if (GUILayout.Button("Templates", EditorStyles.miniButton, GUILayout.Width(82f)))
                            ShowDocumentationTestDataTemplateMenu(templates);

                        GUILayout.FlexibleSpace();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool hasSelection = _documentationTestDataSelectedKeys.Count > 0;
                        using (new EditorGUI.DisabledScope(!hasSelection))
                        {
                            if (GUILayout.Button("Lock Selected", EditorStyles.miniButton, GUILayout.Width(104f)))
                                ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.SetLocked(_documentationTestDataSelectedKeys.ToList(), true));
                            if (GUILayout.Button("Unlock Selected", EditorStyles.miniButton, GUILayout.Width(114f)))
                                ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.SetLocked(_documentationTestDataSelectedKeys.ToList(), false));
                            if (GUILayout.Button("Adopt Selected", EditorStyles.miniButton, GUILayout.Width(112f)) &&
                                ConfirmBroadGeneration("Mark selected documentation records as generated test data?"))
                                ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.AdoptAsGenerated(_documentationTestDataSelectedKeys.ToList()));
                            if (GUILayout.Button("Remove Selected", EditorStyles.miniButton, GUILayout.Width(118f)) &&
                                EditorUtility.DisplayDialog("Remove Selected Documentation Test Records", "Remove selected unlocked records? Locked records will be skipped.", "Remove", "Cancel"))
                                ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.RemoveRecords(_documentationTestDataSelectedKeys.ToList(), false));
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove All Generated Unlocked", EditorStyles.miniButton, GUILayout.Width(196f)) &&
                            EditorUtility.DisplayDialog("Remove Generated Documentation Test Records", "Remove every unlocked record marked as generated documentation test data?", "Remove Generated", "Cancel"))
                            ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.RemoveAllGeneratedUnlocked());
                    }
                }

                if (PungentDocumentationTestDataRegistry.AllProviders.Count == 0)
                {
                    EditorGUILayout.HelpBox("No documentation test-data providers are registered.", MessageType.Info);
                    return;
                }

                foreach (PungentDocumentationRecord record in records.Take(80))
                    DrawDocumentationTestDataRecord(record);

                if (records.Count > 80)
                    EditorGUILayout.HelpBox("Showing the first 80 matching records. Use search or type actions to narrow the list.", MessageType.Info);
            }
        }

        private void DrawDocumentationTestDataRecord(PungentDocumentationRecord record)
        {
            if (record == null)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool selected = _documentationTestDataSelectedKeys.Contains(record.Key);
                bool next = EditorGUILayout.Toggle(selected, GUILayout.Width(20f));
                if (next != selected)
                {
                    if (next)
                        _documentationTestDataSelectedKeys.Add(record.Key);
                    else
                        _documentationTestDataSelectedKeys.Remove(record.Key);
                }

                EditorGUILayout.LabelField(record.typeLabel, EditorStyles.miniLabel, GUILayout.Width(96f));
                EditorGUILayout.LabelField(record.title, EditorStyles.boldLabel, GUILayout.MinWidth(160f));
                if (record.generated)
                    UtilityWindowTheme.CountPill("Generated", UtilityWindowTheme.Purple, 92f);
                if (record.locked)
                    UtilityWindowTheme.CountPill("Locked", UtilityWindowTheme.Amber, 76f);
                if (record.archived)
                    UtilityWindowTheme.CountPill("Archived", UtilityWindowTheme.Neutral, 84f);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(record.generatedTemplateId, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(220f));
            }
        }

        private void ShowDocumentationTestDataTypeMenu(bool regenerate)
        {
            GenericMenu menu = new GenericMenu();
            foreach (IPungentDocumentationTestDataProvider provider in PungentDocumentationTestDataRegistry.AllProviders)
            {
                string providerId = provider.ProviderId;
                string label = (regenerate ? "Regenerate/" : "Generate Missing/") + provider.DocumentType;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (regenerate)
                    {
                        if (ConfirmBroadGeneration("Regenerate unlocked " + provider.DocumentType + " test records?"))
                            ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.RegenerateUnlocked(providerId));
                    }
                    else
                    {
                        ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.GenerateMissing(providerId));
                    }
                });
            }

            menu.ShowAsContext();
        }

        private void ShowDocumentationTestDataTemplateMenu(IReadOnlyList<PungentGeneratedDocumentTemplate> templates)
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentGeneratedDocumentTemplate template in templates ?? new List<PungentGeneratedDocumentTemplate>())
            {
                string providerId = template.providerId;
                string templateId = template.templateId;
                menu.AddItem(new GUIContent("Generate/" + template.documentType + "/" + template.title), false, () =>
                    ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.GenerateTemplate(providerId, templateId, false)));
                menu.AddItem(new GUIContent("Regenerate/" + template.documentType + "/" + template.title), false, () =>
                {
                    if (ConfirmBroadGeneration("Regenerate '" + template.title + "' documentation test record?"))
                        ApplyDocumentationTestDataResult(PungentDocumentationTestDataRegistry.GenerateTemplate(providerId, templateId, true));
                });
            }

            menu.ShowAsContext();
        }

        private void ApplyDocumentationTestDataResult(PungentDocumentationManagementResult result)
        {
            _status = result == null ? "Documentation test data action did not return a result." : result.ToStatus();
            Repaint();
        }

        private static bool ContainsIgnoreCase(string source, string search)
        {
            return (source ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureGenerationReport()
        {
            if (!_generationReportDirty && _generationReport != null && _generationReviewRows != null)
                return;

            _generationReport = PungentUtilityHelpTopicGenerator.BuildReport();
            _generationReviewRows = PungentUtilityHelpReviewQueue.BuildRows(_generationReport);
            _generationReportDirty = false;
        }

        private void RefreshGenerationReport()
        {
            _generationReportDirty = true;
            EnsureGenerationReport();
        }

        private static bool CanWriteGeneratedHelp()
        {
            return PungentDeveloperMode.CanEditProjectMetadata;
        }

        private static bool ConfirmBroadGeneration(string message)
        {
            return EditorUtility.DisplayDialog("Run generated-help action?", message + "\n\nGenerated help remains draft/review content until curated.", "Run", "Cancel");
        }

        private void DrawHelpCoverageSummary(PungentUtilityHelpGenerationReport report)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Help Coverage", UtilityWindowTheme.Blue, report.coverageRows.Count + " utilities");
                int complete = report.coverageRows.Count(row => row != null && row.coverageStatus == PungentUtilityHelpCoverageStatus.Complete);
                int acceptable = report.coverageRows.Count(row => row != null && row.coverageStatus == PungentUtilityHelpCoverageStatus.Acceptable);
                int missingHeader = report.coverageRows.Count(row => row != null && row.missingHeaderIsActionable);
                int missingSection = report.coverageRows.Count(row => row != null && row.missingSectionIsActionable);
                int missingTooltips = report.coverageRows.Count(row => row != null && row.missingTooltipIsActionable);
                int missingScripting = report.coverageRows.Count(row => row != null && row.missingScriptingIsActionable);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Complete " + complete, UtilityWindowTheme.Green, 106f);
                    UtilityWindowTheme.CountPill("Acceptable " + acceptable, UtilityWindowTheme.Teal, 116f);
                    UtilityWindowTheme.CountPill("Headers " + missingHeader, missingHeader == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 96f);
                    UtilityWindowTheme.CountPill("Sections " + missingSection, missingSection == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    UtilityWindowTheme.CountPill("Tooltips " + missingTooltips, missingTooltips == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    UtilityWindowTheme.CountPill("API " + missingScripting, missingScripting == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 82f);
                    GUILayout.FlexibleSpace();
                }

                foreach (PungentUtilityHelpCoverageRow row in report.coverageRows
                    .Where(r => r != null && r.reviewPriority > 0)
                    .OrderByDescending(r => r.reviewPriority)
                    .ThenBy(r => r.displayName, StringComparer.OrdinalIgnoreCase)
                    .Take(12))
                {
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 4, 2)))
                    {
                        EditorGUILayout.LabelField(row.displayName, EditorStyles.boldLabel, GUILayout.Width(190f));
                        EditorGUILayout.LabelField(row.coverageStatus.ToString(), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(130f));
                        EditorGUILayout.LabelField(row.coverageNotes, UtilityWindowTheme.MutedMiniLabelStyle);
                        using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                        {
                            if (GUILayout.Button(new GUIContent("Create Stubs", "Create overview or Controls & Tooltips placeholders where this row needs coverage."), EditorStyles.miniButton, GUILayout.Width(92f), GUILayout.Height(20f)))
                                GenerateForCoverageRow(row);
                        }
                    }
                }
            }
        }

        private void DrawHelpReviewQueues(PungentUtilityHelpGenerationReport report, List<PungentUtilityHelpReviewRow> rows)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Generated Review Queues", UtilityWindowTheme.Amber, rows.Count + " rows");
                    GUILayout.FlexibleSpace();
                    _reviewQueue = (PungentUtilityHelpReviewQueueKind)EditorGUILayout.EnumPopup(new GUIContent("Queue", "Choose a generated-help review queue."), _reviewQueue, GUILayout.Width(310f));
                }

                List<PungentUtilityHelpReviewRow> visibleRows = rows
                    .Where(row => row != null && row.queueKind == _reviewQueue)
                    .OrderByDescending(row => row.priority)
                    .ThenBy(row => row.displayName, StringComparer.OrdinalIgnoreCase)
                    .Take(80)
                    .ToList();

                _reviewQueueScroll = EditorGUILayout.BeginScrollView(_reviewQueueScroll, GUILayout.MinHeight(160f), GUILayout.MaxHeight(360f));
                if (visibleRows.Count == 0)
                    EditorGUILayout.HelpBox("No rows in this review queue.", MessageType.Info);
                foreach (PungentUtilityHelpReviewRow row in visibleRows)
                    DrawHelpReviewRow(row);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHelpReviewRow(PungentUtilityHelpReviewRow row)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.displayName) ? row.utilityId : row.displayName, EditorStyles.boldLabel, GUILayout.Width(190f));
                    UtilityWindowTheme.CountPill(row.lifecycleState.ToString(), UtilityWindowTheme.Amber, 142f);
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.entryLabel) ? row.topicStableId : row.entryLabel, UtilityWindowTheme.MutedMiniLabelStyle);
                }
                if (!string.IsNullOrWhiteSpace(row.message))
                    EditorGUILayout.LabelField(row.message, UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Open Topic", "Open the public Help Browser to this topic."), EditorStyles.miniButton, GUILayout.Width(82f), GUILayout.Height(20f)))
                        OpenReviewRowTopic(row);
                    using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                    {
                        if (row.featureEntry != null && GUILayout.Button("Mark Shippable", EditorStyles.miniButton, GUILayout.Width(108f), GUILayout.Height(20f)))
                            PungentUtilityHelpDeveloperTools.MarkFeatureShippable(row.topic, row.featureEntry);
                        if (row.featureEntry != null && GUILayout.Button("Needs Wording", EditorStyles.miniButton, GUILayout.Width(104f), GUILayout.Height(20f)))
                            PungentUtilityHelpDeveloperTools.MarkFeatureNeedsBetterWording(row.topic, row.featureEntry);
                        if (row.featureEntry != null && GUILayout.Button("Ignore", EditorStyles.miniButton, GUILayout.Width(58f), GUILayout.Height(20f)))
                            PungentUtilityHelpDeveloperTools.IgnoreFeatureFalsePositive(row.topic, row.featureEntry);
                        if (row.scriptingEntry != null && GUILayout.Button("Mark Shippable", EditorStyles.miniButton, GUILayout.Width(108f), GUILayout.Height(20f)))
                            PungentUtilityHelpDeveloperTools.MarkShippable(row.topic, row.scriptingEntry);
                        if (row.scriptingEntry != null && GUILayout.Button("Hide", EditorStyles.miniButton, GUILayout.Width(54f), GUILayout.Height(20f)))
                            PungentUtilityHelpDeveloperTools.HideFromShippable(row.topic, row.scriptingEntry);
                        if (row.coverageRow != null && GUILayout.Button("Create Stubs", EditorStyles.miniButton, GUILayout.Width(92f), GUILayout.Height(20f)))
                            GenerateForCoverageRow(row.coverageRow);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void OpenReviewRowTopic(PungentUtilityHelpReviewRow row)
        {
            if (row == null)
                return;
            if (row.topic != null)
                PungentUtilityHelpBrowserWindow.Open(row.topic.utilityId, row.topic.sectionId, row.topic.topicId);
            else if (!string.IsNullOrWhiteSpace(row.utilityId))
                PungentUtilityHelpBrowserWindow.Open(row.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
        }

        private void GenerateForCoverageRow(PungentUtilityHelpCoverageRow row)
        {
            if (row == null || !CanWriteGeneratedHelp())
                return;

            if (!ConfirmBroadGeneration("Create generated help stubs for " + (string.IsNullOrWhiteSpace(row.displayName) ? row.utilityId : row.displayName) + "?"))
                return;

            if (row.missingHeaderIsActionable || row.missingSectionIsActionable)
                PungentUtilityHelpRegistry.CreateTopicStub(row.utilityId, PungentUtilityHelpIds.DefaultSection, PungentUtilityHelpIds.DefaultTopic);
            if (!row.hasTooltipTopic && row.missingTooltipIsActionable)
                PungentUtilityHelpRegistry.CreateTopicStub(row.utilityId, "controls-tooltips", "controls-tooltips");
            _status = "Prepared help stubs for " + (string.IsNullOrWhiteSpace(row.displayName) ? row.utilityId : row.displayName) + ".";
            PungentUtilityHelpStorage.instance.Persist();
            PungentUtilityHelpRegistry.Rebuild();
            RefreshGenerationReport();
        }

        private void DrawDocumentationAssignments(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan), options))
            {
                _showMetadataDocLinks = EditorGUILayout.Foldout(_showMetadataDocLinks, new GUIContent("Documentation Link Assignments", "Assign Documentation Links entries to the selected utility."), true);
                if (!_showMetadataDocLinks)
                    return;

                UtilityWindowTheme.SectionTitle("Documentation Link Assignments", UtilityWindowTheme.Cyan, "moved from Documentation Links");
                using (new EditorGUILayout.HorizontalScope())
                {
                    _docLinkSearch = EditorGUILayout.TextField(new GUIContent("Search", "Filter links and utilities."), _docLinkSearch, UtilityWindowTheme.ToolbarSearchStyle);
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(54f), GUILayout.Height(22f)))
                        _docLinkSearch = string.Empty;
                }

                string selectedUtilityId = string.IsNullOrWhiteSpace(_selectedUtilityId) ? "utilities-browser" : _selectedUtilityId;
                PungentUtilityDescriptor selectedUtility = PungentUtilityRegistry.Find(selectedUtilityId);
                EditorGUILayout.LabelField("Selected Utility: " + (selectedUtility == null ? selectedUtilityId : selectedUtility.DisplayName), UtilityWindowTheme.MutedMiniLabelStyle);

                List<PungentUtilityDocumentationLinks.DocumentationLink> links = PungentUtilityDocumentationLinks.instance.GetAll()
                    .Where(link => link != null && MatchesDocumentationSearch(link, selectedUtility))
                    .OrderBy(link => link.order)
                    .ThenBy(PungentUtilityDocumentationLinks.GetDisplayName, StringComparer.OrdinalIgnoreCase)
                    .Take(80)
                    .ToList();

                _documentationLinksScroll = EditorGUILayout.BeginScrollView(_documentationLinksScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
                if (links.Count == 0)
                    EditorGUILayout.HelpBox("No documentation links match the current workbench search.", MessageType.Info);
                foreach (PungentUtilityDocumentationLinks.DocumentationLink link in links)
                    DrawDocumentationAssignmentRow(link, selectedUtilityId);
                EditorGUILayout.EndScrollView();
            }
        }

        private bool MatchesDocumentationSearch(PungentUtilityDocumentationLinks.DocumentationLink link, PungentUtilityDescriptor selectedUtility)
        {
            string query = (_docLinkSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return Contains(PungentUtilityDocumentationLinks.GetDisplayName(link), query) ||
                   Contains(link.description, query) ||
                   Contains(link.category, query) ||
                   Contains(link.externalPath, query) ||
                   (selectedUtility != null && Contains(selectedUtility.DisplayName, query)) ||
                   (link.utilityIds != null && link.utilityIds.Any(id => Contains(id, query)));
        }

        private void DrawDocumentationAssignmentRow(PungentUtilityDocumentationLinks.DocumentationLink link, string utilityId)
        {
            bool assigned = link.utilityIds != null && link.utilityIds.Any(id => string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase));
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.035f, 4, 2)))
            {
                using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                {
                    bool nextAssigned = EditorGUILayout.Toggle(assigned, GUILayout.Width(20f));
                    if (nextAssigned != assigned)
                    {
                        Undo.RecordObject(PungentUtilityDocumentationLinks.instance, "Assign Documentation Link");
                        if (nextAssigned)
                            PungentUtilityDocumentationLinks.instance.AssignLinkToUtility(link.id, utilityId);
                        else
                            PungentUtilityDocumentationLinks.instance.UnassignLinkFromUtility(link.id, utilityId);
                        _status = "Updated documentation assignment.";
                    }
                }
                EditorGUILayout.LabelField(PungentUtilityDocumentationLinks.GetDisplayName(link), EditorStyles.boldLabel, GUILayout.Width(220f));
                EditorGUILayout.LabelField(status.kindLabel, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(100f));
                EditorGUILayout.LabelField(status.message, UtilityWindowTheme.MutedMiniLabelStyle);
                if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(54f), GUILayout.Height(20f)))
                    DocumentationLinkEditorPopup.OpenForLink(link.id, utilityId);
            }
        }

        private void DrawExternalLinksEditor(params GUILayoutOption[] options)
        {
            PungentUtilityExternalLinksSettings settings = PungentUtilityExternalLinksSettings.instance;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), options))
            {
                UtilityWindowTheme.SectionTitle("Welcome And External Links", UtilityWindowTheme.Teal, "project-local");
                using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                {
                    _showMetadataExternalLinks = EditorGUILayout.Foldout(_showMetadataExternalLinks, new GUIContent("External Links", "Project-local browser, support, contact, and publisher links."), true);
                    if (_showMetadataExternalLinks)
                    {
                        DrawExternalUrlField(settings, "Support", settings.supportDevelopmentUrl, value => settings.supportDevelopmentUrl = value);
                        DrawExternalUrlField(settings, "Website", settings.websiteUrl, value => settings.websiteUrl = value);
                        DrawExternalUrlField(settings, "Documentation", settings.documentationUrl, value => settings.documentationUrl = value);
                        DrawExternalUrlField(settings, "Community", settings.communityUrl, value => settings.communityUrl = value);
                        DrawExternalUrlField(settings, "Contact", settings.contactUrl, value => settings.contactUrl = value);
                        DrawExternalUrlField(settings, "Bug Report", settings.bugReportUrl, value => settings.bugReportUrl = value);
                        DrawExternalUrlField(settings, "Feature Request", settings.featureRequestUrl, value => settings.featureRequestUrl = value);
                        DrawExternalUrlField(settings, "Publisher / Asset Store", settings.publisherPageUrl, value => settings.publisherPageUrl = value);
                    }

                    _showMetadataWelcomeSections = EditorGUILayout.Foldout(_showMetadataWelcomeSections, new GUIContent("Welcome Sections", "Project-local custom welcome sections shown by the Utilities Browser."), true);
                    if (!_showMetadataWelcomeSections)
                        return;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Custom Welcome Sections", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Add Section", EditorStyles.miniButton, GUILayout.Width(94f), GUILayout.Height(22f)))
                        {
                            Undo.RecordObject(settings, "Add Welcome Section");
                            if (settings.welcomeSections == null)
                                settings.welcomeSections = new List<PungentUtilityWelcomeSection>();
                            settings.welcomeSections.Add(new PungentUtilityWelcomeSection());
                            settings.SaveStore();
                        }
                    }

                    if (settings.welcomeSections != null)
                    {
                        for (int i = 0; i < settings.welcomeSections.Count; i++)
                            DrawWelcomeSectionEditor(settings, i);
                    }
                }
            }
        }

        private void DrawExternalUrlField(PungentUtilityExternalLinksSettings settings, string label, string value, Action<string> assign)
        {
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.TextField(new GUIContent(label, "Project-local absolute URL."), value ?? string.Empty);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(settings, "Edit External Link");
            assign(next);
            settings.SaveStore();
            _status = label + " link updated.";
        }

        private void DrawWelcomeSectionEditor(PungentUtilityExternalLinksSettings settings, int index)
        {
            if (settings.welcomeSections == null || index < 0 || index >= settings.welcomeSections.Count)
                return;

            PungentUtilityWelcomeSection section = settings.welcomeSections[index];
            if (section == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.06f, 0.03f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    section.enabled = EditorGUILayout.ToggleLeft("Enabled", section.enabled, GUILayout.Width(82f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(70f), GUILayout.Height(20f)))
                    {
                        Undo.RecordObject(settings, "Remove Welcome Section");
                        settings.welcomeSections.RemoveAt(index);
                        settings.SaveStore();
                        return;
                    }
                }

                EditorGUI.BeginChangeCheck();
                section.title = EditorGUILayout.TextField("Title", section.title ?? string.Empty);
                EditorGUILayout.LabelField("Body", UtilityWindowTheme.MutedMiniLabelStyle);
                section.body = EditorGUILayout.TextArea(section.body ?? string.Empty, GUILayout.MinHeight(34f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(settings, "Edit Welcome Section");
                    settings.SaveStore();
                }
            }
        }

        private void DrawAudit()
        {
            _auditScroll = EditorGUILayout.BeginScrollView(_auditScroll);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Package Readiness Audit", UtilityWindowTheme.Purple, _packageAuditReport == null ? "not run" : _packageAuditReport.generatedUtc.ToLocalTime().ToShortTimeString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    _packageAuditIncludeInfo = GUILayout.Toggle(_packageAuditIncludeInfo, new GUIContent("Include Info", "Include informational package-readiness rows."), EditorStyles.miniButton, GUILayout.Width(96f), GUILayout.Height(22f));
                    if (GUILayout.Button("Run Package Audit", GUILayout.Width(142f), GUILayout.Height(26f)))
                    {
                        _packageAuditReport = PungentUtilityDesignAudit.Run(_packageAuditIncludeInfo);
                        _status = "Package audit complete: " + _packageAuditReport.ErrorCount + " errors, " + _packageAuditReport.WarningCount + " warnings.";
                    }
                    using (new EditorGUI.DisabledScope(_packageAuditReport == null))
                    {
                        if (GUILayout.Button("Copy Summary", EditorStyles.miniButton, GUILayout.Width(112f), GUILayout.Height(22f)))
                            EditorGUIUtility.systemCopyBuffer = _packageAuditReport.ToMarkdownSummary();
                        if (GUILayout.Button("Copy Next Pass", EditorStyles.miniButton, GUILayout.Width(122f), GUILayout.Height(22f)))
                            EditorGUIUtility.systemCopyBuffer = PungentUtilityReleaseReadiness.BuildNextPassBrief(_packageAuditReport);
                    }
                    GUILayout.FlexibleSpace();
                }
            }

            if (_packageAuditReport == null)
            {
                EditorGUILayout.HelpBox("Run Package Audit to review package structure, registry metadata, menu aliases, performance hotspots, release readiness, and package findings.", MessageType.Info);
                DrawDeveloperSurfaceScan();
                DrawFutureMetadataNotes();
                EditorGUILayout.EndScrollView();
                return;
            }

            PungentUtilityReleaseReadiness.Assessment assessment = PungentUtilityReleaseReadiness.Assess(_packageAuditReport);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(assessment.tint)))
            {
                UtilityWindowTheme.SectionTitle("Release Readiness", assessment.tint, _packageAuditReport.developerReleaseBlockerCount + " blockers");
                EditorGUILayout.LabelField(assessment.headline, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(assessment.details, UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_packageAuditReport.ErrorCount + " errors", _packageAuditReport.ErrorCount == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Red, 92f);
                    UtilityWindowTheme.CountPill(_packageAuditReport.WarningCount + " warnings", _packageAuditReport.WarningCount == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 110f);
                    UtilityWindowTheme.CountPill(_packageAuditReport.InfoCount + " info", UtilityWindowTheme.Cyan, 82f);
                    GUILayout.FlexibleSpace();
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Structure And Registry", UtilityWindowTheme.Blue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_packageAuditReport.scriptCount + " scripts", UtilityWindowTheme.Blue, 94f);
                    UtilityWindowTheme.CountPill(_packageAuditReport.asmdefCount + " asmdefs", _packageAuditReport.asmdefCount == 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 104f);
                    UtilityWindowTheme.CountPill(_packageAuditReport.descriptorCount + " descriptors", UtilityWindowTheme.Teal, 124f);
                    UtilityWindowTheme.CountPill(_packageAuditReport.legacyMenuAliasCount + " aliases", _packageAuditReport.legacyMenuAliasCount == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    UtilityWindowTheme.CountPill(_packageAuditReport.largeEditorWindowCount + " large windows", _packageAuditReport.largeEditorWindowCount == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 130f);
                    GUILayout.FlexibleSpace();
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Package Findings", UtilityWindowTheme.Amber, _packageAuditReport.issues.Count + " rows");
                foreach (PungentUtilityDesignAudit.Issue issue in _packageAuditReport.issues.OrderByDescending(i => i.severity).ThenBy(i => i.area).Take(80))
                {
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(issue.severity == PungentUtilityDesignAudit.Severity.Error ? UtilityWindowTheme.Red : issue.severity == PungentUtilityDesignAudit.Severity.Warning ? UtilityWindowTheme.Amber : UtilityWindowTheme.Cyan, 0.08f, 0.035f, 4, 2)))
                    {
                        EditorGUILayout.LabelField(issue.severity + " - " + issue.area + " - " + issue.target, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(issue.message, UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
            }
            DrawDeveloperSurfaceScan();
            DrawFutureMetadataNotes();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDeveloperSurfaceScan()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Developer Surface Scan", UtilityWindowTheme.Teal, _developerSurfaceRows.Count == 0 ? "not run" : _developerSurfaceRows.Count + " rows");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Scan Source", "Scan C# sources for visible developer-surface markers outside the unified workbench."), GUILayout.Width(126f), GUILayout.Height(24f)))
                        RunDeveloperSurfaceScan();
                    if (_developerSurfaceScanUtc != default)
                        EditorGUILayout.LabelField("Last scan: " + _developerSurfaceScanUtc.ToLocalTime().ToShortTimeString(), UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                }

                if (_developerSurfaceRows.Count == 0)
                {
                    EditorGUILayout.HelpBox("Run the scan to review compatibility routes, suspicious normal-window developer controls, and allowed theme-accessory exceptions.", MessageType.Info);
                    return;
                }

                foreach (DeveloperSurfaceScanRow row in _developerSurfaceRows.Take(90))
                {
                    Color tint = string.Equals(row.classification, "Suspicious", StringComparison.OrdinalIgnoreCase) ? UtilityWindowTheme.Amber :
                        string.Equals(row.classification, "Compatibility", StringComparison.OrdinalIgnoreCase) ? UtilityWindowTheme.Cyan :
                        string.Equals(row.classification, "Theme Accessory", StringComparison.OrdinalIgnoreCase) ? UtilityWindowTheme.Green :
                        UtilityWindowTheme.Neutral;

                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.08f, 0.035f, 4, 2)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            UtilityWindowTheme.CountPill(row.classification, tint, 118f);
                            EditorGUILayout.LabelField(row.marker, EditorStyles.boldLabel, GUILayout.Width(180f));
                            EditorGUILayout.LabelField(row.path + ":" + row.line, UtilityWindowTheme.PathLabelStyle);
                        }
                        EditorGUILayout.LabelField(row.note, UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
            }
        }

        private void RunDeveloperSurfaceScan()
        {
            _developerSurfaceRows = BuildDeveloperSurfaceScan();
            _developerSurfaceScanUtc = DateTime.UtcNow;
            int suspicious = _developerSurfaceRows.Count(row => row != null && string.Equals(row.classification, "Suspicious", StringComparison.OrdinalIgnoreCase));
            _status = "Developer surface scan complete: " + suspicious + " suspicious row(s).";
        }

        private static List<DeveloperSurfaceScanRow> BuildDeveloperSurfaceScan()
        {
            List<DeveloperSurfaceScanRow> rows = new List<DeveloperSurfaceScanRow>();
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Assets").Replace('\\', '/');
            if (!Directory.Exists(root))
                return rows;

            string[] markers =
            {
                "Developer Mode",
                "Developer Tools",
                "CanEditProjectMetadata",
                "PungentDeveloperMode",
                "OpenGeneration",
                "OpenMetadata",
                "OpenCategory",
                "DrawDeveloper",
                "Refresh Generated Index",
                "Generate Missing Topic Stubs",
                "Edit Package Metadata",
                "category metadata"
            };

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (!normalized.Contains("/PungentFunkUtilities/Editor/Utilities Core/"))
                    continue;

                string relative = normalized.Substring(root.Length + 1);
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string marker = markers.FirstOrDefault(item => line.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (marker == null)
                        continue;

                    string classification = ClassifyDeveloperSurface(relative, line);
                    if (classification == "Ignore")
                        continue;

                    rows.Add(new DeveloperSurfaceScanRow
                    {
                        path = relative,
                        line = i + 1,
                        marker = marker,
                        classification = classification,
                        note = BuildDeveloperSurfaceNote(relative, line, classification)
                    });
                }
            }

            return rows
                .OrderByDescending(row => string.Equals(row.classification, "Suspicious", StringComparison.OrdinalIgnoreCase))
                .ThenBy(row => row.classification, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.line)
                .Take(180)
                .ToList();
        }

        private static string ClassifyDeveloperSurface(string relativePath, string line)
        {
            string path = relativePath.Replace('\\', '/');

            if (path.Contains("/DeveloperMode/") || path.EndsWith("PungentUtilityDeveloperToolsWindow.cs", StringComparison.OrdinalIgnoreCase))
                return "Compatibility";

            if (path.Contains("PungentEditorStyleExplorerWindow.cs") ||
                (path.Contains("UtilityWindowThemeCustomizer.cs") && line.IndexOf("Style Explorer", StringComparison.OrdinalIgnoreCase) >= 0))
                return "Theme Accessory";

            if (path.Contains("PungentUtilityControlPanelWindow.cs") &&
                (line.IndexOf("Open Developer Tools", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("Enable Developer Tools", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 line.IndexOf("Developer Tools", StringComparison.OrdinalIgnoreCase) >= 0))
                return "Allowed Access";

            if (path.Contains("PungentUtilityDesignAudit.cs") || path.Contains("PungentUtilityDescriptor.cs") || path.Contains("PungentUtilityHelpTopic.cs"))
                return "Model";

            if (line.IndexOf("=> false", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Disabled";

            return "Suspicious";
        }

        private static string BuildDeveloperSurfaceNote(string relativePath, string line, string classification)
        {
            if (classification == "Theme Accessory")
                return "Allowed: Style Explorer belongs to Theme Editor/Appearance, not Developer Tools.";
            if (classification == "Allowed Access")
                return "Allowed: Utilities Browser settings is the visible Developer Tools access point.";
            if (classification == "Compatibility")
                return "Allowed when it routes into the unified Developer Tools workbench or remains a compatibility shim.";
            if (classification == "Disabled")
                return "Dormant code path appears disabled; keep or remove after Unity validation.";
            if (classification == "Model")
                return "Model or audit metadata, not a visible developer control by itself.";
            return "Review this marker and confirm it is hidden, routed to Developer Tools, or intentionally non-developer.";
        }

        private void DrawFutureMetadataNotes()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                UtilityWindowTheme.SectionTitle("Future Metadata Tooling", UtilityWindowTheme.Neutral);
                EditorGUILayout.LabelField("Good next candidates: metadata completeness matrix, diff view across defaults/overrides/catalog/docs, bulk preview/apply, package split readiness, capability validation, docs-topic coverage, and relationship graph.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private IEnumerable<PungentUtilityPackageRecord> GetFilteredPackages()
        {
            string q = (_packageSearch ?? string.Empty).Trim();
            IEnumerable<PungentUtilityPackageRecord> source = PungentUtilityPackageCatalog.Records;
            if (!_includeHidden)
                source = source.Where(record => record.lifecycle == PungentUtilityPackageLifecycle.Visible);
            if (!string.IsNullOrWhiteSpace(q))
                source = source.Where(record => PungentUtilityPackageCatalog.MatchesRecord(record, q));
            return source;
        }

        private IEnumerable<string> GetMetadataCategoryIds()
        {
            IEnumerable<string> source = PungentUtilityCategories.CurrentCategories
                .Concat(new[] { PungentUtilityCategories.Other })
                .Concat(PungentUtilityCategoryOverrides.instance.Overrides.Select(item => item.categoryId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(PungentUtilityCategories.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            if (!_includeHidden)
                source = source.Where(id => PungentUtilityCategoryOverrides.instance.GetLifecycle(id) == PungentUtilityCategoryLifecycle.Visible);

            return source
                .OrderBy(PungentUtilityCategories.SortKey)
                .ThenBy(id => id, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsPackageUnitNarrow()
        {
            return position.width < PackageUnitNarrowWidth;
        }

        private bool IsPackageUnitWide()
        {
            return position.width >= PackageUnitWideWidth;
        }

        private float GetPackageListWidth()
        {
            return Mathf.Clamp(position.width * 0.22f, PackageListMinWidth, PackageListMaxWidth);
        }

        private float GetPackageRecordWidth()
        {
            return Mathf.Clamp(position.width * 0.30f, PackageRecordMinWidth, PackageRecordMaxWidth);
        }

        private float GetUtilityListWidth()
        {
            return Mathf.Clamp(position.width * 0.18f, UtilityListMinWidth, UtilityListMaxWidth);
        }

        private float GetPackageUnitStackListHeight()
        {
            return Mathf.Clamp(position.height * 0.20f, 130f, 240f);
        }

        private float GetPackageUnitNarrowUtilityListHeight()
        {
            return Mathf.Clamp(position.height * 0.18f, 110f, 200f);
        }

        private float GetPackageUnitEditorHeight()
        {
            return Mathf.Max(220f, position.height - 210f);
        }

        private float GetPackageRecordScrollHeight()
        {
            return Mathf.Max(140f, GetPackageUnitEditorHeight() - 78f);
        }

        private bool PassesCategorySearch(string categoryId)
        {
            string q = (_categorySearch ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(q) ||
                   Contains(categoryId, q) ||
                   Contains(PungentUtilityCategories.GetDisplayName(categoryId), q);
        }

        private List<PungentUtilityDescriptor> GetCategoryUtilities(string categoryId)
        {
            string normalized = PungentUtilityCategories.Normalize(categoryId);
            return PungentUtilityRegistry.All
                .Where(PassesUtilityFilters)
                .Where(utility => utility.HasCategory(normalized))
                .OrderByDescending(utility => string.Equals(utility.AreaCategory, normalized, StringComparison.OrdinalIgnoreCase))
                .ThenBy(utility => utility.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool PassesUtilityFilters(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return false;
            if (!_includeHidden && (descriptor.IsHiddenOrInternal || descriptor.IsArchived || descriptor.IsDeveloperOnly))
                return false;
            if (_onlyOverrides && !PungentUtilityRegistry.HasMetadataOverride(descriptor.Id))
                return false;
            if (_filterMissingDocs && !string.IsNullOrWhiteSpace(descriptor.DocumentationTopicId))
                return false;
            if (_filterNoCardTint && descriptor.OverrideBrowserCardTint)
                return false;
            if (_filterNoFacets && descriptor.HasCategoryFacets)
                return false;

            string q = (_search ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(q))
                return true;

            return Contains(descriptor.Id, q) ||
                   Contains(descriptor.DisplayName, q) ||
                   Contains(descriptor.Description, q) ||
                   Contains(descriptor.Module, q) ||
                   Contains(descriptor.AreaCategory, q) ||
                   Contains(descriptor.NormalizedPackageId, q) ||
                   Contains(descriptor.DocumentationTopicId, q) ||
                   (descriptor.Tags != null && descriptor.Tags.Any(tag => Contains(tag, q))) ||
                   (descriptor.CategoryFacets != null && descriptor.CategoryFacets.Any(facet => Contains(facet, q)));
        }

        private void SelectUtility(string utilityId)
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            if (descriptor == null)
                return;

            _selectedUtilityId = descriptor.Id;
            _selectedPackageId = descriptor.NormalizedPackageId;
            _packageDraft = null;
            EnsurePackageDraft();
            _status = "Selected " + descriptor.DisplayName + ".";
        }

        private void EnsurePackageDraft()
        {
            string normalized = PungentUtilityPackageCatalog.NormalizePackageId(_selectedPackageId);
            if (_packageDraft != null && string.Equals(_packageDraft.packageId, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            _selectedPackageId = normalized;
            _packageDraft = PungentUtilityPackageCatalog.CreateEditableRecord(_selectedPackageId);
            if (_packageDraft == null)
                return;
            _packageUtilityIdsText = FormatCsv(_packageDraft.includedUtilityIds);
            _packageCapabilitiesText = FormatCsv(_packageDraft.includedCapabilities);
        }

        private void EnsureCategoryDraft()
        {
            string normalized = PungentUtilityCategories.Normalize(_selectedCategoryId);
            if (_categoryDraft != null && string.Equals(_categoryDraft.categoryId, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            _selectedCategoryId = normalized;
            _categoryDraft = PungentUtilityCategoryOverrides.instance.CreateEditableCopy(
                normalized,
                PungentUtilityCategories.GetFactoryDisplayName(normalized),
                PungentUtilityCategories.GetFactoryTint(normalized));
        }

        private PungentUtilityMetadataOverrides.UtilityMetadataOverride GetMetadataDraft(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return null;

            if (!_metadataDrafts.TryGetValue(descriptor.Id, out PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata) || metadata == null)
            {
                metadata = PungentUtilityMetadataOverrides.instance.CreateEditableCopy(descriptor);
                _metadataDrafts[descriptor.Id] = metadata;
            }
            return metadata;
        }

        private void SavePackageRecord()
        {
            if (_packageDraft == null || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            _packageDraft.includedUtilityIds = ParseCsv(_packageUtilityIdsText);
            _packageDraft.includedCapabilities = ParseCsv(_packageCapabilitiesText);
            PungentUtilityPackageCatalog.SaveRecordOverride(_packageDraft);
            _selectedPackageId = PungentUtilityPackageCatalog.NormalizePackageId(_packageDraft.packageId);
            _packageDraft = null;
            EnsurePackageDraft();
            _status = "Saved package catalogue override.";
        }

        private void ClearPackageRecord()
        {
            if (_packageDraft == null || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            string packageId = _packageDraft.packageId;
            PungentUtilityPackageCatalog.ClearRecordOverride(packageId);
            _packageDraft = null;
            EnsurePackageDraft();
            _status = "Cleared package catalogue override.";
        }

        private void ClearCategoryOverride(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId) || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Clear Utility Category Override");
            PungentUtilityCategoryOverrides.instance.RemoveOverride(categoryId);
            _categoryDraft = null;
            EnsureCategoryDraft();
            _status = "Cleared category metadata for " + PungentUtilityCategories.Normalize(categoryId) + ".";
        }

        private void CreateProjectCategory(string categoryId)
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata)
                return;

            string normalized = PungentUtilityCategories.Normalize(categoryId);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Create Utility Category");
            string displayName = ObjectNames.NicifyVariableName(normalized.Replace('-', ' '));
            PungentUtilityCategoryOverrides.instance.CreateProjectLocalCategory(normalized, displayName, UtilityWindowTheme.Teal);
            _selectedCategoryId = normalized;
            _categoryDraft = null;
            EnsureCategoryDraft();
            _status = "Created project-local category " + normalized + ".";
        }

        private void DuplicateSelectedCategory(string categoryId)
        {
            if (_categoryDraft == null || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            string normalized = PungentUtilityCategories.Normalize(categoryId);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            PungentUtilityCategoryOverrides.CategoryOverride copy = PungentUtilityCategoryOverrides.instance.CreateEditableCopy(
                normalized,
                ObjectNames.NicifyVariableName(normalized.Replace('-', ' ')),
                _categoryDraft.overrideTint ? _categoryDraft.tint : PungentUtilityCategories.GetTint(_categoryDraft.categoryId));
            copy.projectLocal = true;
            copy.lifecycle = PungentUtilityCategoryLifecycle.Visible;
            copy.overrideDisplayName = true;
            copy.displayName = ObjectNames.NicifyVariableName(normalized.Replace('-', ' '));
            copy.overrideTint = true;
            copy.tint = _categoryDraft.overrideTint ? _categoryDraft.tint : PungentUtilityCategories.GetTint(_categoryDraft.categoryId);

            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Duplicate Utility Category");
            PungentUtilityCategoryOverrides.instance.SetOverride(copy);
            _selectedCategoryId = normalized;
            _categoryDraft = null;
            EnsureCategoryDraft();
            _status = "Duplicated category metadata into " + normalized + ".";
        }

        private void RemoveProjectCategory(string categoryId)
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata)
                return;

            string normalized = PungentUtilityCategories.Normalize(categoryId);
            if (PungentUtilityCategories.IsFactoryCategory(normalized))
                return;
            if (!EditorUtility.DisplayDialog("Remove Project Category", "Remove the project-local category record for " + normalized + "? Utilities that reference this ID will keep their metadata until reassigned.", "Remove", "Cancel"))
                return;

            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Remove Utility Category");
            PungentUtilityCategoryOverrides.instance.RemoveOverride(normalized);
            _selectedCategoryId = PungentUtilityCategories.Core;
            _categoryDraft = null;
            _status = "Removed project-local category " + normalized + ".";
        }

        private void SaveMetadataOverride(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            if (descriptor == null || metadata == null || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            PungentUtilityRegistry.SaveMetadataOverride(metadata);
            _metadataDrafts.Remove(descriptor.Id);
            _status = "Saved metadata override for " + descriptor.Id + ".";
        }

        private void DrawMetadataFooter(PungentUtilityDescriptor descriptor, PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                {
                    if (GUILayout.Button("Save Utility Metadata", EditorStyles.miniButton, GUILayout.Width(150f), GUILayout.Height(24f)))
                        SaveMetadataOverride(descriptor, metadata);
                    if (PungentUtilityRegistry.HasMetadataOverride(descriptor.Id) && GUILayout.Button("Clear Override", EditorStyles.miniButton, GUILayout.Width(104f), GUILayout.Height(24f)))
                    {
                        PungentUtilityRegistry.ClearMetadataOverride(descriptor.Id);
                        _metadataDrafts.Remove(descriptor.Id);
                        _status = "Cleared metadata override for " + descriptor.Id + ".";
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void SaveCategoryOverride(PungentUtilityCategoryOverrides.CategoryOverride editable)
        {
            if (editable == null || !PungentDeveloperMode.CanEditProjectMetadata)
                return;

            Undo.RecordObject(PungentUtilityCategoryOverrides.instance, "Save Utility Category Override");
            PungentUtilityCategoryOverrides.instance.SetOverride(editable);
            _selectedCategoryId = PungentUtilityCategories.Normalize(editable.categoryId);
            _categoryDraft = null;
            EnsureCategoryDraft();
            _status = "Saved category metadata for " + editable.categoryId + ".";
        }

        private void SetCategoryMembership(PungentUtilityMetadataOverrides.UtilityMetadataOverride metadata, string categoryId, bool shouldInclude)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(categoryId))
                return;

            string normalized = PungentUtilityCategories.Normalize(categoryId);
            List<string> facets = (metadata.categoryFacets ?? new string[0])
                .Select(PungentUtilityCategories.Normalize)
                .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, PungentUtilityCategories.Other, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (shouldInclude && !facets.Any(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase)))
                facets.Add(normalized);
            if (!shouldInclude)
                facets.RemoveAll(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase));

            metadata.overrideCategoryFacets = true;
            metadata.categoryFacets = facets.ToArray();
        }

        private string GetFieldValue(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return string.Empty;

            switch (_field)
            {
                case MetadataField.DisplayName: return descriptor.DisplayName;
                case MetadataField.UtilityType: return descriptor.UtilityType;
                case MetadataField.AreaCategory: return descriptor.AreaCategory;
                case MetadataField.Module: return descriptor.Module;
                case MetadataField.Description: return descriptor.Description;
                case MetadataField.MenuPath: return descriptor.MenuPath;
                case MetadataField.Tags: return FormatCsv(descriptor.Tags);
                case MetadataField.SortOrder: return descriptor.SortOrder.ToString();
                case MetadataField.PackageStatus: return descriptor.PackageStatus;
                case MetadataField.PackageId: return descriptor.NormalizedPackageId;
                case MetadataField.PackageDisplayName: return descriptor.PackageDisplayName;
                case MetadataField.PackageTier: return descriptor.PackageTier.ToString();
                case MetadataField.AssetStoreUrl: return descriptor.AssetStoreUrl;
                case MetadataField.PackageManagerId: return descriptor.PackageManagerId;
                case MetadataField.ImportPackagePath: return descriptor.ImportPackagePath;
                case MetadataField.ProvidedCapabilities: return FormatCsv(descriptor.ProvidedCapabilities);
                case MetadataField.RequiredPackages: return FormatCsv(descriptor.RequiredPackageIds);
                case MetadataField.OptionalPackages: return FormatCsv(descriptor.OptionalPackageIds);
                case MetadataField.RelatedUtilities: return FormatCsv(descriptor.RelatedUtilityIds);
                case MetadataField.CategoryFacets: return FormatCsv(descriptor.CategoryFacets);
                case MetadataField.DocumentationTopicId: return descriptor.DocumentationTopicId;
                case MetadataField.ItemKind: return descriptor.ItemKind.ToString();
                case MetadataField.Visibility: return descriptor.Visibility.ToString();
                case MetadataField.ShowInUtilitiesBrowser: return descriptor.ShowInUtilitiesBrowser.ToString();
                case MetadataField.MissingDependencyMessage: return descriptor.MissingDependencyMessage;
                case MetadataField.InstallHint: return descriptor.InstallHint;
                case MetadataField.SupportsSceneOverlay: return descriptor.SupportsSceneOverlay.ToString();
                case MetadataField.SupportsContextMenu: return descriptor.SupportsContextMenu.ToString();
                case MetadataField.SupportsSelection: return descriptor.SupportsSelection.ToString();
                case MetadataField.IsLabHub: return descriptor.IsLabHub.ToString();
                case MetadataField.BrowserRole: return descriptor.BrowserRole.ToString();
                case MetadataField.BrowserPriority: return descriptor.BrowserPriority.ToString();
                case MetadataField.ParentUtilityId: return descriptor.ParentUtilityId;
                case MetadataField.BrowserProminence: return descriptor.BrowserProminence.ToString();
                case MetadataField.BrowserCardTint: return descriptor.OverrideBrowserCardTint ? "#" + ColorUtility.ToHtmlStringRGB(descriptor.BrowserCardTint) : "#" + ColorUtility.ToHtmlStringRGB(PungentUtilityCategories.GetTint(descriptor.AreaCategory));
                case MetadataField.BrowserTags: return FormatCsv(descriptor.BrowserTags);
                case MetadataField.AccessoryUtilities: return FormatCsv(descriptor.AccessoryUtilityIds);
                case MetadataField.ActionIds: return FormatCsv(descriptor.ActionIds);
                default: return descriptor.Id;
            }
        }

        private void DrawOverrideToggleOnly(ref bool enabled, string tooltip, bool compact)
        {
            using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                enabled = EditorGUILayout.Toggle(new GUIContent(string.Empty, tooltip), enabled, GUILayout.Width(compact ? 18f : OverrideToggleWidth));
        }

        private void DrawOverrideLabel(string label, bool compact)
        {
            if (!compact)
                EditorGUILayout.LabelField(label, GUILayout.Width(GetMetadataLabelWidth()));
        }

        private float GetMetadataLabelWidth()
        {
            return Mathf.Clamp(position.width * 0.055f, MetadataLabelMinWidth, MetadataLabelMaxWidth);
        }

        private Rect DrawOverrideRowPrefix(string label, ref bool enabled, string tooltip, bool compact)
        {
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            float toggleWidth = compact ? 18f : OverrideToggleWidth;
            Rect toggleRect = new Rect(row.x, row.y, toggleWidth, row.height);
            using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                enabled = EditorGUI.Toggle(toggleRect, new GUIContent(string.Empty, tooltip), enabled);

            float valueX = toggleRect.xMax + PackageUnitRowGap;
            if (!compact && !string.IsNullOrWhiteSpace(label))
            {
                float labelWidth = Mathf.Min(GetMetadataLabelWidth(), Mathf.Max(58f, row.width * 0.28f));
                Rect labelRect = new Rect(valueX, row.y, labelWidth, row.height);
                EditorGUI.LabelField(labelRect, label);
                valueX = labelRect.xMax + PackageUnitRowGap;
            }

            return new Rect(valueX, row.y, Mathf.Max(20f, row.xMax - valueX), row.height);
        }

        private void DrawOverrideStackHeader(string label, ref bool enabled, string tooltip, bool compact)
        {
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            float toggleWidth = compact ? 18f : OverrideToggleWidth;
            Rect toggleRect = new Rect(row.x, row.y, toggleWidth, row.height);
            using (new EditorGUI.DisabledScope(!PungentDeveloperMode.CanEditProjectMetadata))
                enabled = EditorGUI.Toggle(toggleRect, new GUIContent(string.Empty, tooltip), enabled);

            if (!compact && !string.IsNullOrWhiteSpace(label))
            {
                Rect labelRect = new Rect(toggleRect.xMax + PackageUnitRowGap, row.y, Mathf.Max(20f, row.xMax - toggleRect.xMax - PackageUnitRowGap), row.height);
                EditorGUI.LabelField(labelRect, label);
            }
        }

        private void DrawStringOverride(string label, ref bool enabled, ref string value, bool compact = false)
        {
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
                value = EditorGUI.DelayedTextField(valueRect, value ?? string.Empty);
        }

        private void DrawTextOverride(string label, ref bool enabled, ref string value, bool compact = false)
        {
            DrawOverrideStackHeader(label, ref enabled, "Enable project-local override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
                value = DrawPackageUnitTextArea(value, compact ? 34f : 46f);
        }

        private void DrawCategoryOverride(string label, ref bool enabled, ref string value, bool compact = false)
        {
            string[] categories = GetAllCategoryIds();
            string normalizedValue = PungentUtilityCategories.Normalize(value);
            int index = Mathf.Max(0, Array.FindIndex(categories, c => string.Equals(c, normalizedValue, StringComparison.OrdinalIgnoreCase)));
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local category override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                int next = EditorGUI.Popup(valueRect, index, categories.Select(c => PungentUtilityCategories.GetDisplayName(c) + " (" + c + ")").ToArray());
                if (next >= 0 && next < categories.Length)
                    value = categories[next];
            }
        }

        private void DrawArrayOverride(string label, ref bool enabled, ref string[] values, bool compact = false)
        {
            DrawListOverride(label, ref enabled, ref values, compact);
        }

        private void DrawListOverride(string label, ref bool enabled, ref string[] values, bool compact = false)
        {
            DrawOverrideStackHeader(label, ref enabled, "Enable project-local list override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                string text = DrawPackageUnitTextArea(FormatLines(values), compact ? 34f : 48f);
                values = ParseCsv(text);
            }
        }

        private void DrawPackageStatusOverride(string label, ref bool enabled, ref string value, bool compact = false)
        {
            string[] statuses = PungentUtilityPackageStatus.All;
            string normalized = PungentUtilityPackageStatus.Normalize(value);
            int index = Array.FindIndex(statuses, status => string.Equals(status, normalized, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                statuses = new[] { normalized }.Concat(statuses).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                index = 0;
            }
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local package status override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                int next = EditorGUI.Popup(valueRect, index, statuses);
                if (next >= 0 && next < statuses.Length)
                    value = statuses[next];
            }
        }

        private void DrawPackageIdOverride(string label, ref bool enabled, ref string value, bool compact = false)
        {
            string[] packages = new[] { string.Empty }.Concat(GetPackageIds()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string normalized = PungentUtilityPackageCatalog.NormalizePackageId(value);
            if (packages.Length == 0)
                packages = new[] { normalized };
            int index = Array.FindIndex(packages, packageId => string.Equals(packageId, normalized, StringComparison.OrdinalIgnoreCase));
            if (index < 0 && !string.IsNullOrWhiteSpace(normalized))
            {
                packages = new[] { normalized }.Concat(packages).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                index = 0;
            }
            index = Mathf.Max(0, index);
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local package ID override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                int next = EditorGUI.Popup(valueRect, index, packages.Select(DisplayOptionalId).ToArray());
                if (next >= 0 && next < packages.Length)
                    value = packages[next];
            }
        }

        private void DrawPackageIdListOverride(string label, ref bool enabled, ref string[] values, bool compact = false)
        {
            DrawPickerListOverride(label, ref enabled, ref values, GetPackageIds(), "Add package", compact);
        }

        private void DrawUtilityIdOverride(string label, ref bool enabled, ref string value, bool compact = false)
        {
            string[] utilityIds = new[] { string.Empty }.Concat(GetUtilityIds()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string selectedValue = value ?? string.Empty;
            if (utilityIds.Length == 0)
                utilityIds = new[] { selectedValue };
            int index = Array.FindIndex(utilityIds, id => string.Equals(id, selectedValue, StringComparison.OrdinalIgnoreCase));
            if (index < 0 && !string.IsNullOrWhiteSpace(selectedValue))
            {
                utilityIds = new[] { selectedValue.Trim() }.Concat(utilityIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                index = 0;
            }
            index = Mathf.Max(0, index);
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local utility ID override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                int next = EditorGUI.Popup(valueRect, index, utilityIds.Select(DisplayOptionalId).ToArray());
                if (next >= 0 && next < utilityIds.Length)
                    value = utilityIds[next];
            }
        }

        private void DrawUtilityIdListOverride(string label, ref bool enabled, ref string[] values, bool compact = false)
        {
            DrawPickerListOverride(label, ref enabled, ref values, GetUtilityIds(), "Add utility", compact);
            DrawMissingUtilityIdWarnings("Missing utility ID", values);
        }

        private void DrawPickerListOverride(string label, ref bool enabled, ref string[] values, string[] candidates, string addLabel, bool compact)
        {
            DrawOverrideStackHeader(label, ref enabled, "Enable project-local list override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                string text = DrawPackageUnitTextArea(FormatLines(values), compact ? 34f : 48f);
                values = ParseCsv(text);
                string[] popup = new[] { addLabel }.Concat(candidates ?? new string[0]).ToArray();
                Rect popupRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                int next = EditorGUI.Popup(popupRect, 0, popup);
                if (next > 0 && next < popup.Length)
                    values = AppendDistinct(values, popup[next]);
            }
        }

        private void DrawCategoryFacetsOverride(string label, ref bool enabled, ref string[] values, bool compact = false)
        {
            string[] categories = GetAllCategoryIds();
            DrawOverrideStackHeader(label, ref enabled, "Enable project-local category facet override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
            {
                string[] currentValues = values ?? new string[0];
                foreach (string category in categories)
                {
                    Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                    bool included = currentValues.Any(value => string.Equals(PungentUtilityCategories.Normalize(value), category, StringComparison.OrdinalIgnoreCase));
                    bool next = EditorGUI.ToggleLeft(row, PungentUtilityCategories.GetDisplayName(category) + " (" + category + ")", included);
                    if (next != included)
                        values = next ? AppendDistinct(values, category) : RemoveValue(values, category);
                }
            }
        }

        private void DrawBoolOverride(string label, ref bool enabled, ref bool value, bool compact = false)
        {
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local boolean override.", compact);
            valueRect.width = Mathf.Min(36f, valueRect.width);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
                value = EditorGUI.Toggle(valueRect, value);
        }

        private void DrawColorOverride(string label, ref bool enabled, ref Color value, bool compact = false)
        {
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local color override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
                value = EditorGUI.ColorField(valueRect, GUIContent.none, value, false, false, false);
        }

        private void DrawIntOverride(string label, ref bool enabled, ref int value, bool compact = false)
        {
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local integer override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
                value = EditorGUI.IntField(valueRect, value);
        }

        private void DrawEnumOverride(string label, ref bool enabled, ref PungentUtilityPackageTier value, bool compact = false)
        {
            DrawEnumPopup(label, ref enabled, ref value, compact);
        }

        private void DrawEnumOverride(string label, ref bool enabled, ref PungentUtilityItemKind value, bool compact = false)
        {
            DrawEnumPopup(label, ref enabled, ref value, compact);
        }

        private void DrawEnumOverride(string label, ref bool enabled, ref PungentUtilityVisibility value, bool compact = false)
        {
            DrawEnumPopup(label, ref enabled, ref value, compact);
        }

        private void DrawEnumOverride(string label, ref bool enabled, ref PungentUtilityBrowserRole value, bool compact = false)
        {
            DrawEnumPopup(label, ref enabled, ref value, compact);
        }

        private void DrawEnumOverride(string label, ref bool enabled, ref PungentUtilityBrowserProminence value, bool compact = false)
        {
            DrawEnumPopup(label, ref enabled, ref value, compact);
        }

        private void DrawEnumPopup<TEnum>(string label, ref bool enabled, ref TEnum value, bool compact) where TEnum : struct
        {
            Rect valueRect = DrawOverrideRowPrefix(label, ref enabled, "Enable project-local enum override.", compact);
            using (new EditorGUI.DisabledScope(!enabled || !PungentDeveloperMode.CanEditProjectMetadata))
                value = (TEnum)(object)EditorGUI.EnumPopup(valueRect, (Enum)(object)value);
        }

        private void LoadPrefs()
        {
            _tab = (Tab)Mathf.Clamp(EditorPrefs.GetInt(PrefTab, (int)Tab.Metadata), 0, Enum.GetValues(typeof(Tab)).Length - 1);
            _mode = (MetadataMode)Mathf.Clamp(EditorPrefs.GetInt(PrefMode, (int)MetadataMode.PackageUnit), 0, Enum.GetValues(typeof(MetadataMode)).Length - 1);
            _field = (MetadataField)Mathf.Clamp(EditorPrefs.GetInt(PrefField, (int)MetadataField.CategoryFacets), 0, Enum.GetValues(typeof(MetadataField)).Length - 1);
            _selectedUtilityId = EditorPrefs.GetString(PrefSelectedUtility, "utilities-browser");
            _selectedPackageId = EditorPrefs.GetString(PrefSelectedPackage, PungentUtilityPackageCatalog.CorePackageId);
            _selectedCategoryId = EditorPrefs.GetString(PrefSelectedCategory, PungentUtilityCategories.Core);
            _search = EditorPrefs.GetString(PrefSearch, string.Empty);
            _packageSearch = EditorPrefs.GetString(PrefPackageSearch, string.Empty);
            _categorySearch = EditorPrefs.GetString(PrefCategorySearch, string.Empty);
            _includeHidden = EditorPrefs.GetBool(PrefIncludeHidden, false);
            _onlyOverrides = EditorPrefs.GetBool(PrefOnlyOverrides, false);
            _filterMissingDocs = EditorPrefs.GetBool(PrefFilterMissingDocs, false);
            _filterNoCardTint = EditorPrefs.GetBool(PrefFilterNoCardTint, false);
            _filterNoFacets = EditorPrefs.GetBool(PrefFilterNoFacets, false);
            _showMetadataAncillaryEditors = EditorPrefs.GetBool(PrefShowMetadataAncillary, false);
            _showMetadataDocLinks = EditorPrefs.GetBool(PrefShowMetadataDocLinks, true);
            _showMetadataExternalLinks = EditorPrefs.GetBool(PrefShowMetadataExternalLinks, true);
            _showMetadataWelcomeSections = EditorPrefs.GetBool(PrefShowMetadataWelcome, true);
            _reviewQueue = LoadEnum(PrefReviewQueue, PungentUtilityHelpReviewQueueKind.GeneratedDraftTopics);
            _docLinkSearch = EditorPrefs.GetString(PrefDocLinkSearch, string.Empty);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PrefTab, (int)_tab);
            EditorPrefs.SetInt(PrefMode, (int)_mode);
            EditorPrefs.SetInt(PrefField, (int)_field);
            EditorPrefs.SetString(PrefSelectedUtility, _selectedUtilityId ?? string.Empty);
            EditorPrefs.SetString(PrefSelectedPackage, _selectedPackageId ?? PungentUtilityPackageCatalog.CorePackageId);
            EditorPrefs.SetString(PrefSelectedCategory, _selectedCategoryId ?? PungentUtilityCategories.Core);
            EditorPrefs.SetString(PrefSearch, _search ?? string.Empty);
            EditorPrefs.SetString(PrefPackageSearch, _packageSearch ?? string.Empty);
            EditorPrefs.SetString(PrefCategorySearch, _categorySearch ?? string.Empty);
            EditorPrefs.SetBool(PrefIncludeHidden, _includeHidden);
            EditorPrefs.SetBool(PrefOnlyOverrides, _onlyOverrides);
            EditorPrefs.SetBool(PrefFilterMissingDocs, _filterMissingDocs);
            EditorPrefs.SetBool(PrefFilterNoCardTint, _filterNoCardTint);
            EditorPrefs.SetBool(PrefFilterNoFacets, _filterNoFacets);
            EditorPrefs.SetBool(PrefShowMetadataAncillary, _showMetadataAncillaryEditors);
            EditorPrefs.SetBool(PrefShowMetadataDocLinks, _showMetadataDocLinks);
            EditorPrefs.SetBool(PrefShowMetadataExternalLinks, _showMetadataExternalLinks);
            EditorPrefs.SetBool(PrefShowMetadataWelcome, _showMetadataWelcomeSections);
            EditorPrefs.SetString(PrefReviewQueue, _reviewQueue.ToString());
            EditorPrefs.SetString(PrefDocLinkSearch, _docLinkSearch ?? string.Empty);
        }

        private static TEnum LoadEnum<TEnum>(string key, TEnum fallback) where TEnum : struct
        {
            string value = EditorPrefs.GetString(key, fallback.ToString());
            return Enum.TryParse(value, out TEnum parsed) ? parsed : fallback;
        }

        private static string[] GetAllCategoryIds()
        {
            return PungentUtilityCategories.CurrentCategories
                .Concat(new[] { PungentUtilityCategories.Other })
                .Concat(PungentUtilityCategoryOverrides.instance.Overrides.Select(item => item.categoryId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PungentUtilityCategories.SortKey)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(query) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] ParseCsv(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new string[0];

            return text.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string FormatCsv(IEnumerable<string> values)
        {
            return values == null ? string.Empty : string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string FormatLines(IEnumerable<string> values)
        {
            return values == null ? string.Empty : string.Join("\n", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string[] AppendDistinct(IEnumerable<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return values == null ? new string[0] : values.ToArray();

            return (values ?? new string[0])
                .Concat(new[] { value.Trim() })
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] RemoveValue(IEnumerable<string> values, string value)
        {
            string normalized = PungentUtilityCategories.Normalize(value);
            return (values ?? new string[0])
                .Where(item => !string.Equals(PungentUtilityCategories.Normalize(item), normalized, StringComparison.OrdinalIgnoreCase))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] GetPackageIds()
        {
            return PungentUtilityPackageCatalog.Records
                .Select(record => PungentUtilityPackageCatalog.NormalizePackageId(record.packageId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] GetUtilityIds()
        {
            return PungentUtilityRegistry.All
                .Select(utility => utility.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string DisplayOptionalId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }
    }
#endif
}
