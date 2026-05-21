using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentGizmoBrowserWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.SceneGizmoBrowser.";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefShowActive = PrefPrefix + "ShowActive";
        private const string PrefShowInactive = PrefPrefix + "ShowInactive";
        private const string PrefShowVisible = PrefPrefix + "ShowVisible";
        private const string PrefShowHidden = PrefPrefix + "ShowHidden";
        private const string PrefInvalidOnly = PrefPrefix + "InvalidOnly";
        private const string PrefSelectedOnly = PrefPrefix + "SelectedOnly";
        private const string PrefPresetCategory = PrefPrefix + "PresetCategory";
        private const string PrefReplacePresetRules = PrefPrefix + "ReplacePresetRules";
        private const string PrefProviderFilter = PrefPrefix + "ProviderFilter";
        private const string PrefViewTab = PrefPrefix + "ViewTab";
        private const string PrefExpandedRows = PrefPrefix + "ExpandedRows";
        private const string PrefPresetStateFilter = PrefPrefix + "PresetStateFilter";
        private const string PrefDesignBlockCategory = PrefPrefix + "DesignBlockCategory";
        private const string PrefDesignPaletteSearch = PrefPrefix + "DesignPaletteSearch";
        private const string PrefDesignLeftWidth = PrefPrefix + "DesignLeftWidth";
        private const string PrefDesignRightWidth = PrefPrefix + "DesignRightWidth";
        private const string PrefDesignSelectedRuleIndex = PrefPrefix + "DesignSelectedRuleIndex";
        private const string PrefDesignNarrowTab = PrefPrefix + "DesignNarrowTab";

        private const float DesignNarrowBreakpoint = 820f;
        private const float MinDesignPaletteWidth = 190f;
        private const float MinDesignWorkspaceWidth = 300f;
        private const float MinDesignPropertiesWidth = 240f;
        private const float DesignSplitterReserve = 18f;

        private enum BrowserViewTab
        {
            Overview,
            Design,
            Manage,
            Presets,
            Performance
        }

        private enum PresetStateFilter
        {
            All,
            Linked,
            Base,
            Customized,
            Outdated,
            Locked,
            Mixed,
            Unlinked
        }

        private enum DesignNarrowTab
        {
            Blocks,
            Composition,
            Properties
        }

        private enum ProviderTypeFilter
        {
            All,
            Sources,
            Beacons,
            CollisionSensors,
            TriggerSensors,
            Trajectories,
            Spatial
        }

        private enum RecordKind
        {
            Source,
            Beacon,
            CollisionSensor,
            TriggerSensor,
            Trajectory,
            Spatial,
            Provider
        }

        private sealed class BrowserRecord
        {
            public PungentSceneGizmoSource source;
            public Component component;
            public GameObject gameObject;
            public RecordKind kind;
            public int instanceId;
            public bool active;
            public bool visible;
            public bool selectedOnly;
            public bool alwaysVisible;
            public int totalRules;
            public int enabledRules;
            public int invalidRuleCount;
            public int expensiveWarningCount;
            public int estimatedDrawOperations;
            public int estimatedLabels;
            public int estimatedTrajectorySamples;
            public string presetCategories;
            public PungentSceneGizmoPresetDiffStatus presetStatus;
            public string warning;
            public string searchText;
            public string DisplayName => gameObject != null ? gameObject.name : (component != null ? component.name : "Missing Gizmo");
        }

        private readonly List<BrowserRecord> _records = new List<BrowserRecord>();
        private readonly HashSet<int> _selectedRecordIds = new HashSet<int>();
        private readonly HashSet<int> _expandedRecordIds = new HashSet<int>();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _showActive = true;
        private bool _showInactive = true;
        private bool _showVisible = true;
        private bool _showHidden = true;
        private bool _invalidOnly;
        private bool _selectedSceneObjectOnly;
        private bool _replacePresetRules;
        private string _presetCategory = "All";
        private BrowserViewTab _viewTab = BrowserViewTab.Overview;
        private PresetStateFilter _presetStateFilter = PresetStateFilter.All;
        private ProviderTypeFilter _providerTypeFilter = ProviderTypeFilter.All;
        private PungentSceneGizmoPreset _assetPreset;
        private string _designBlockCategory = "All";
        private string _designPaletteSearch = string.Empty;
        private float _designLeftWidth = 230f;
        private float _designRightWidth = 310f;
        private int _designSelectedRuleIndex = -1;
        private DesignNarrowTab _designNarrowTab = DesignNarrowTab.Composition;
        private bool _designShowAdvancedRuleFields;
        private Vector2 _designBlockScroll;
        private Vector2 _designCompositionScroll;
        private Vector2 _designPropertiesScroll;
        private string _simulationState = "Preview";
        private Vector3 _simulationDirection = Vector3.forward;
        private float _simulationSpeed = 10f;
        private bool _simulationShowPrimitiveContext = true;
        private string _status = "Ready.";
        private bool _cacheDirty = true;
        private double _nextAllowedWindowRepaintTime;
        private double _nextAllowedSceneRepaintTime;

        private int _sourceCount;
        private int _enabledRuleCount;
        private int _providerCount;
        private int _beaconCount;
        private int _collisionSensorCount;
        private int _triggerSensorCount;
        private int _trajectoryCount;
        private int _spatialCount;
        private int _invalidRuleCount;
        private int _expensiveWarningCount;
        private int _estimatedDrawOperations;
        private int _estimatedLabels;
        private int _estimatedTrajectorySamples;
        private int _activeVisibleCount;

        public static void Open()
        {
            PungentGizmoBrowserWindow window = GetWindow<PungentGizmoBrowserWindow>("Scene Gizmos");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        public static void OpenDesign(PungentSceneGizmoSource source)
        {
            PungentGizmoBrowserWindow window = GetWindow<PungentGizmoBrowserWindow>("Scene Gizmos");
            window.minSize = new Vector2(520f, 360f);
            window._viewTab = BrowserViewTab.Design;
            if (source != null)
            {
                window._designSelectedRuleIndex = Mathf.Clamp(window._designSelectedRuleIndex, -1, source.rules != null ? source.rules.Count - 1 : -1);
                Selection.activeObject = source;
            }
            window.MarkCacheDirty();
            window.Show();
            window.Repaint();
        }

        private void OnEnable()
        {
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _showActive = UtilityWindowPrefs.GetBool(PrefShowActive, true);
            _showInactive = UtilityWindowPrefs.GetBool(PrefShowInactive, true);
            _showVisible = UtilityWindowPrefs.GetBool(PrefShowVisible, true);
            _showHidden = UtilityWindowPrefs.GetBool(PrefShowHidden, true);
            _invalidOnly = UtilityWindowPrefs.GetBool(PrefInvalidOnly, false);
            _selectedSceneObjectOnly = UtilityWindowPrefs.GetBool(PrefSelectedOnly, false);
            _presetCategory = UtilityWindowPrefs.GetString(PrefPresetCategory, "All");
            _replacePresetRules = UtilityWindowPrefs.GetBool(PrefReplacePresetRules, false);
            _providerTypeFilter = (ProviderTypeFilter)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefProviderFilter, 0), 0, Enum.GetValues(typeof(ProviderTypeFilter)).Length - 1);
            _viewTab = (BrowserViewTab)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefViewTab, 0), 0, Enum.GetValues(typeof(BrowserViewTab)).Length - 1);
            _presetStateFilter = (PresetStateFilter)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefPresetStateFilter, 0), 0, Enum.GetValues(typeof(PresetStateFilter)).Length - 1);
            _designBlockCategory = UtilityWindowPrefs.GetString(PrefDesignBlockCategory, "All");
            _designPaletteSearch = UtilityWindowPrefs.GetString(PrefDesignPaletteSearch, string.Empty);
            _designLeftWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefDesignLeftWidth, _designLeftWidth), MinDesignPaletteWidth, 420f);
            _designRightWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefDesignRightWidth, _designRightWidth), MinDesignPropertiesWidth, 480f);
            _designSelectedRuleIndex = UtilityWindowPrefs.GetInt(PrefDesignSelectedRuleIndex, -1);
            _designNarrowTab = (DesignNarrowTab)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefDesignNarrowTab, (int)DesignNarrowTab.Composition), 0, Enum.GetValues(typeof(DesignNarrowTab)).Length - 1);
            RestoreExpandedRows();
            EditorApplication.hierarchyChanged += MarkCacheDirty;
            Selection.selectionChanged += RequestWindowRepaint;
            RebuildCache();
        }

        private void OnDisable()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetBool(PrefShowActive, _showActive);
            UtilityWindowPrefs.SetBool(PrefShowInactive, _showInactive);
            UtilityWindowPrefs.SetBool(PrefShowVisible, _showVisible);
            UtilityWindowPrefs.SetBool(PrefShowHidden, _showHidden);
            UtilityWindowPrefs.SetBool(PrefInvalidOnly, _invalidOnly);
            UtilityWindowPrefs.SetBool(PrefSelectedOnly, _selectedSceneObjectOnly);
            UtilityWindowPrefs.SetString(PrefPresetCategory, _presetCategory);
            UtilityWindowPrefs.SetBool(PrefReplacePresetRules, _replacePresetRules);
            UtilityWindowPrefs.SetInt(PrefProviderFilter, (int)_providerTypeFilter);
            UtilityWindowPrefs.SetInt(PrefViewTab, (int)_viewTab);
            UtilityWindowPrefs.SetInt(PrefPresetStateFilter, (int)_presetStateFilter);
            UtilityWindowPrefs.SetString(PrefDesignBlockCategory, _designBlockCategory);
            SaveDesignPrefs();
            StoreExpandedRows();
            EditorApplication.hierarchyChanged -= MarkCacheDirty;
            Selection.selectionChanged -= RequestWindowRepaint;
        }

        private void OnGUI()
        {
            PungentEditorPerformanceUtility.RecordWindowRepaint(this);
            if (_cacheDirty)
                RebuildCache();

            List<BrowserRecord> visibleRecords = FilterRecords();
            DrawHeader();
            DrawPrimaryToolbar(visibleRecords);
            DrawTabStrip();

            switch (_viewTab)
            {
                case BrowserViewTab.Design:
                    DrawDesignTab(visibleRecords);
                    break;
                case BrowserViewTab.Manage:
                    DrawManageTab(visibleRecords);
                    break;
                case BrowserViewTab.Presets:
                    DrawPresetsTab(visibleRecords);
                    break;
                case BrowserViewTab.Performance:
                    DrawPerformanceTab(visibleRecords);
                    break;
                default:
                    DrawOverviewTab(visibleRecords);
                    break;
            }
        }

        private void DrawHeader()
        {
            string conciseStatus = $"{_activeVisibleCount} active, {_estimatedDrawOperations} draw ops, {_estimatedLabels} labels";
            if (!string.IsNullOrWhiteSpace(_status))
                conciseStatus += " - " + _status;

            UtilityWindowTheme.Header(
                "Scene Gizmo Browser",
                "Dashboard for live gizmos and workspace for reusable preset design.",
                conciseStatus);
        }

        private void DrawPrimaryToolbar(List<BrowserRecord> visibleRecords)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Refresh", UtilityWindowTheme.Green, GUILayout.Width(86f)))
                        RebuildCache();

                    using (new EditorGUI.DisabledScope(Selection.gameObjects == null || Selection.gameObjects.Length == 0))
                    {
                        if (GUILayout.Button(new GUIContent("Add To Selection...", "Add a Scene Gizmo Source or provider component to the selected GameObject(s)."), GUILayout.Width(138f)))
                            ShowAddToSelectionMenu();

                        if (GUILayout.Button(new GUIContent("Apply Preset...", "Apply a built-in scene gizmo preset to the selected GameObject(s)."), GUILayout.Width(116f)))
                            ShowApplyPresetMenu();
                    }

                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawCompactCountPills();
                    GUILayout.FlexibleSpace();
                }

                DrawWarningBannerIfNeeded();
                DrawCompactFilterSummary(visibleRecords);
            }
        }

        private void DrawCompactCountPills()
        {
            UtilityWindowTheme.CountPill("Shown " + _activeVisibleCount, UtilityWindowTheme.Cyan, 88f);
            UtilityWindowTheme.CountPill("Invalid " + _invalidRuleCount, _invalidRuleCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 88f);
            UtilityWindowTheme.CountPill("High Cost " + _expensiveWarningCount, _expensiveWarningCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 106f);
        }

        private void DrawWarningBannerIfNeeded()
        {
            if (PungentSceneGizmoPerformancePolicy.ShouldShowWarning(_activeVisibleCount, _enabledRuleCount, _providerCount, _estimatedTrajectorySamples, _estimatedLabels, _estimatedDrawOperations))
                EditorGUILayout.HelpBox(PungentSceneGizmoPerformancePolicy.BuildBrowserWarningText(_activeVisibleCount, _estimatedDrawOperations), MessageType.Warning);
            else if (PungentSceneGizmoPerformancePolicy.ShouldShowInfo(_activeVisibleCount, _enabledRuleCount))
                EditorGUILayout.HelpBox("Gizmo density is elevated. Filter, hide, or switch low-priority gizmos to Selected-only drawing if Scene View repainting feels sluggish.", MessageType.Info);
        }

        private void DrawCompactFilterSummary(List<BrowserRecord> visibleRecords)
        {
            int visibleCount = visibleRecords != null ? visibleRecords.Count : 0;
            int hiddenByFilters = Mathf.Max(0, _records.Count - visibleCount);
            int selectedVisible = visibleRecords != null ? visibleRecords.Count(r => _selectedRecordIds.Contains(r.instanceId)) : 0;
            string filterSummary = $"{visibleCount}/{_records.Count} rows shown. {selectedVisible} selected visible / {_selectedRecordIds.Count} selected total. {hiddenByFilters} hidden by filters.";

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(filterSummary, "Provider discovery is cached. Refresh or hierarchy changes rebuild the source/provider cache; normal repaint does not."), UtilityWindowTheme.MutedMiniLabelStyle);
                if (_viewTab != BrowserViewTab.Overview && GUILayout.Button("Edit Filters", EditorStyles.miniButton, GUILayout.Width(82f)))
                    _viewTab = BrowserViewTab.Overview;
            }
        }

        private void DrawTabStrip()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawTabButton(BrowserViewTab.Overview, "Overview");
                DrawTabButton(BrowserViewTab.Design, "Design");
                DrawTabButton(BrowserViewTab.Manage, "Manage");
                DrawTabButton(BrowserViewTab.Presets, "Presets");
                DrawTabButton(BrowserViewTab.Performance, "Performance");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawTabButton(BrowserViewTab tab, string label)
        {
            bool selected = _viewTab == tab;
            bool next = GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(92f));
            if (next && !selected)
                _viewTab = tab;
        }

        private void DrawOverviewTab(List<BrowserRecord> visibleRecords)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Overview", UtilityWindowTheme.Teal, "Find and inspect scene gizmos");
                DrawCounts();
                DrawFilters();
            }

            DrawRecordList(visibleRecords, "Sources & Providers", showHighCostOnly: false);
        }

        private void DrawDesignTab(List<BrowserRecord> visibleRecords)
        {
            PungentSceneGizmoSource source = ResolveDesignSource(visibleRecords);
            PungentSceneGizmoPresetDiffStatus status = source != null
                ? PungentSceneGizmoPresetDiffUtility.GetStatus(source)
                : default;

            DrawDesignToolbar(source, status);

            if (source == null)
            {
                DrawDesignEmptyState();
                DrawDesignStatusStrip(null);
                return;
            }

            ClampDesignSelectedRule(source);

            if (position.width < DesignNarrowBreakpoint)
                DrawDesignNarrowLayout(source, status, visibleRecords);
            else
                DrawDesignWorkbenchLayout(source, status, visibleRecords);

            DrawDesignStatusStrip(source);
        }

        private void DrawDesignToolbar(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Design", UtilityWindowTheme.Blue, source != null ? source.name : "select source");
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(source, typeof(PungentSceneGizmoSource), true);
                    if (source != null)
                    {
                        DrawPresetStateChip(status, 82f);
                        UtilityWindowTheme.CountPill("Rules " + (source.rules != null ? source.rules.Count : 0), UtilityWindowTheme.Cyan, 82f);
                        UtilityWindowTheme.CountPill("Draw Ops " + source.EstimateDrawOperations(), UtilityWindowTheme.Teal, 104f);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Add Source", Selection.gameObjects != null && Selection.gameObjects.Length > 0, "Select scene GameObject(s) first.", AddSourceToSelection, 92f);
                    DrawActionButton("Focus Scene", source != null, "Select or create a Scene Gizmo Source first.", () => FocusDesignSource(source), 94f);
                    DrawActionButton("Apply Preset", source != null, "Select a Scene Gizmo Source first.", () => ShowApplyPresetMenuForSource(source), 104f);
                    DrawActionButton("Save As New", source != null && status.canSaveAsNewPreset, source == null ? "Select a source first." : "Add at least one rule before saving.", () =>
                    {
                        if (PungentSceneGizmoCommandService.SaveSourceAsNewPreset(source, out PungentSceneGizmoPreset preset))
                        {
                            _assetPreset = preset;
                            _status = "Saved and linked new preset " + preset.displayName + ".";
                            MarkCacheDirty();
                        }
                    }, 102f);
                    DrawActionButton("Update", source != null && status.canUpdateFromPreset, "No linked preset update is available.", () =>
                    {
                        if (PungentSceneGizmoCommandService.UpdateSourceFromLinkedPreset(source, out PungentSceneGizmoPresetApplySummary summary))
                        {
                            _status = "Updated from preset. " + summary;
                            MarkCacheDirty();
                        }
                    }, 72f);
                    DrawActionButton("Revert", source != null && status.canRevertToPreset, "No linked preset changes to revert.", () =>
                    {
                        if (!EditorUtility.DisplayDialog("Revert Scene Gizmo Source", "Replace this source's rules with the linked preset?", "Revert", "Cancel"))
                            return;

                        if (PungentSceneGizmoCommandService.RevertSourceToLinkedPreset(source, out PungentSceneGizmoPresetApplySummary summary))
                        {
                            _status = "Reverted to preset. " + summary;
                            MarkCacheDirty();
                        }
                    }, 68f);
                    DrawActionButton("Overwrite", source != null && status.canOverwritePreset, "Only changed linked preset assets can be overwritten.", () =>
                    {
                        if (!EditorUtility.DisplayDialog("Overwrite Scene Gizmo Preset", "Overwrite the linked preset asset with this source's current rules? Scene references will be stripped from the asset.", "Overwrite", "Cancel"))
                            return;

                        if (PungentSceneGizmoCommandService.OverwriteLinkedPreset(source, out PungentSceneGizmoPreset preset))
                        {
                            _status = "Overwrote preset " + preset.displayName + ".";
                            MarkCacheDirty();
                            MaybeOfferAutoUpdateLinkedInstances(source, preset);
                        }
                    }, 86f);
                    DrawActionButton("Inspector", source != null, "Select a source first.", () =>
                    {
                        Selection.activeObject = source;
                        EditorGUIUtility.PingObject(source);
                    }, 82f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDesignEmptyState()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.HelpBox("Select a Scene Gizmo Source row or a GameObject with a source component to start designing a reusable gizmo preset.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Add Source To Selection", Selection.gameObjects != null && Selection.gameObjects.Length > 0, "Select scene GameObject(s) first.", AddSourceToSelection, 158f);
                    if (GUILayout.Button("Open Presets", GUILayout.Width(104f)))
                        _viewTab = BrowserViewTab.Presets;
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDesignWorkbenchLayout(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status, List<BrowserRecord> visibleRecords)
        {
            ClampDesignPanelWidths();
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                DrawDesignBlocksPane(source, GUILayout.Width(_designLeftWidth), GUILayout.ExpandHeight(true));
                UtilityWindowTheme.HorizontalResizeHandle(ref _designLeftWidth, MinDesignPaletteWidth, MaxDesignLeftWidth(), SaveDesignPrefs, "Drag to resize the block palette.");
                DrawDesignCompositionPane(source, status, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                UtilityWindowTheme.HorizontalResizeHandle(ref _designRightWidth, MinDesignPropertiesWidth, MaxDesignRightWidth(), SaveDesignPrefs, "Drag to resize properties.", true);
                DrawDesignPropertiesPane(source, status, visibleRecords, GUILayout.Width(_designRightWidth), GUILayout.ExpandHeight(true));
            }
        }

        private void DrawDesignNarrowLayout(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status, List<BrowserRecord> visibleRecords)
        {
            DrawDesignNarrowTabStrip();
            switch (_designNarrowTab)
            {
                case DesignNarrowTab.Blocks:
                    DrawDesignBlocksPane(source, GUILayout.ExpandHeight(true));
                    break;
                case DesignNarrowTab.Properties:
                    DrawDesignPropertiesPane(source, status, visibleRecords, GUILayout.ExpandHeight(true));
                    break;
                default:
                    DrawDesignCompositionPane(source, status, GUILayout.ExpandHeight(true));
                    break;
            }
        }

        private void DrawDesignNarrowTabStrip()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawDesignNarrowTabButton(DesignNarrowTab.Blocks, "Blocks");
                DrawDesignNarrowTabButton(DesignNarrowTab.Composition, "Composition");
                DrawDesignNarrowTabButton(DesignNarrowTab.Properties, "Properties");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawDesignNarrowTabButton(DesignNarrowTab tab, string label)
        {
            bool selected = _designNarrowTab == tab;
            bool next = GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(tab == DesignNarrowTab.Composition ? 106f : 86f));
            if (next && !selected)
            {
                _designNarrowTab = tab;
                SaveDesignPrefs();
            }
        }

        private void DrawDesignBlocksPane(PungentSceneGizmoSource source, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green), options))
            {
                UtilityWindowTheme.SectionTitle("Blocks", UtilityWindowTheme.Green, "palette");
                _designPaletteSearch = EditorGUILayout.TextField(new GUIContent("Search", "Filter block names, categories, and summaries."), _designPaletteSearch);
                DrawDesignCategoryChips();

                List<PungentSceneGizmoBlockDefinition> blocks = GetDesignBlocks().ToList();
                _designBlockScroll = EditorGUILayout.BeginScrollView(_designBlockScroll, GUILayout.ExpandHeight(true));
                if (blocks.Count == 0)
                {
                    EditorGUILayout.HelpBox("No blocks match the current palette filters.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < blocks.Count; i++)
                        DrawDesignBlockTile(source, blocks[i]);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDesignCategoryChips()
        {
            List<string> categories = new List<string> { "All" };
            categories.AddRange(PungentSceneGizmoBlockLibrary.Categories);
            int perRow = _designLeftWidth < 250f || position.width < DesignNarrowBreakpoint ? 2 : 3;
            for (int i = 0; i < categories.Count; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = i; c < Mathf.Min(i + perRow, categories.Count); c++)
                    {
                        string category = categories[c];
                        bool selected = string.Equals(_designBlockCategory, category, StringComparison.OrdinalIgnoreCase);
                        bool next = GUILayout.Toggle(selected, category, EditorStyles.toolbarButton, GUILayout.MinWidth(68f));
                        if (next && !selected)
                        {
                            _designBlockCategory = category;
                            SaveDesignPrefs();
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private IEnumerable<PungentSceneGizmoBlockDefinition> GetDesignBlocks()
        {
            IEnumerable<PungentSceneGizmoBlockDefinition> blocks = PungentSceneGizmoBlockLibrary.InCategory(_designBlockCategory);
            if (string.IsNullOrWhiteSpace(_designPaletteSearch))
                return blocks;

            string query = _designPaletteSearch.Trim();
            return blocks.Where(block =>
                block != null &&
                ((block.DisplayName != null && block.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                 (block.Category != null && block.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                 (block.EffectSummary != null && block.EffectSummary.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                 (block.Description != null && block.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)));
        }

        private void DrawDesignBlockTile(PungentSceneGizmoSource source, PungentSceneGizmoBlockDefinition block)
        {
            if (block == null)
                return;

            bool canApply = source != null && source.gameObject != null && !EditorUtility.IsPersistent(source.gameObject);
            string disabledReason = source == null ? "Select a source first." : "Blocks can only be applied to scene objects.";
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(block.Tint, 0.10f, 0.04f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(block.IconText, block.Tint, 38f);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(block.DisplayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(block.EffectSummary, UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!canApply))
                    {
                        if (GUILayout.Button(new GUIContent("Add", canApply ? block.Description : disabledReason), GUILayout.Width(48f)))
                            ApplyBlockToDesignSource(source, block);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(block.Category, UtilityWindowTheme.Neutral, 92f);
                    if (block.EstimatedRuleCount >= 0)
                        UtilityWindowTheme.CountPill("Rules " + block.EstimatedRuleCount, UtilityWindowTheme.Cyan, 82f);
                    if (block.EstimatedProviderCount > 0)
                        UtilityWindowTheme.CountPill("Provider", UtilityWindowTheme.Purple, 86f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDesignCompositionPane(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), options))
            {
                int ruleCount = source.rules != null ? source.rules.Count : 0;
                UtilityWindowTheme.SectionTitle("Composition", UtilityWindowTheme.Teal, ruleCount + " rule" + (ruleCount == 1 ? string.Empty : "s"));
                _designCompositionScroll = EditorGUILayout.BeginScrollView(_designCompositionScroll, GUILayout.ExpandHeight(true));
                DrawDesignCompositionHeader(source, status);
                DrawDesignSchematicCanvas(source);
                DrawDesignRuleStack(source);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDesignCompositionHeader(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.07f, 0.025f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(source != null ? source.name : "No Source", EditorStyles.boldLabel);
                    DrawPresetStateChip(status, 82f);
                    UtilityWindowTheme.CountPill("Labels " + (source != null ? source.EstimateLabelCount() : 0), UtilityWindowTheme.Purple, 88f);
                    GUILayout.FlexibleSpace();
                }

                if (!string.IsNullOrWhiteSpace(status.message))
                    EditorGUILayout.LabelField(status.message, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawDesignSchematicCanvas(PungentSceneGizmoSource source)
        {
            Rect rect = GUILayoutUtility.GetRect(120f, 210f, GUILayout.ExpandWidth(true));
            Color background = EditorGUIUtility.isProSkin
                ? new Color(0.08f, 0.10f, 0.12f, 1f)
                : new Color(0.88f, 0.92f, 0.96f, 1f);
            EditorGUI.DrawRect(rect, background);

            Rect inner = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);
            Handles.BeginGUI();
            Color previous = Handles.color;
            DrawSchematicGrid(inner);

            PungentSceneGizmoSource.GizmoRule selectedRule = GetSelectedRule(source);
            IReadOnlyList<PungentSceneGizmoSource.GizmoRule> focusRules = BuildSchematicFocusRules(source, selectedRule);
            Color focusColor = selectedRule != null ? selectedRule.color : UtilityWindowTheme.Cyan;
            Vector2 origin = new Vector2(inner.x + inner.width * 0.28f, inner.y + inner.height * 0.58f);
            Vector2 target = new Vector2(inner.x + inner.width * 0.74f, inner.y + inner.height * 0.42f);

            DrawSchematicVolumeIfNeeded(inner, focusRules, focusColor);
            DrawSchematicTargetLinkIfNeeded(focusRules, origin, target);
            DrawSchematicVectorsIfNeeded(focusRules, origin, focusColor);
            DrawSchematicTrajectoryIfNeeded(focusRules, origin, focusColor);
            DrawSchematicOrigin(origin, focusColor);
            DrawSchematicTargetIfNeeded(focusRules, target);
            DrawSchematicLabelIfNeeded(focusRules, selectedRule, origin);
            Handles.color = previous;
            Handles.EndGUI();

            DrawSchematicStateStrip(rect, selectedRule);
        }

        private void DrawDesignRuleStack(PungentSceneGizmoSource source)
        {
            if (source == null || source.rules == null || source.rules.Count == 0)
            {
                EditorGUILayout.HelpBox("Add a block or apply a preset to start composing this source.", MessageType.Info);
                return;
            }

            List<PungentSceneGizmoSource.RuleStatus> statuses = source.GetRuleStatuses();
            for (int i = 0; i < source.rules.Count; i++)
            {
                PungentSceneGizmoSource.RuleStatus status = i < statuses.Count ? statuses[i] : null;
                DrawDesignRuleRow(source, i, status);
            }
        }

        private void DrawDesignRuleRow(PungentSceneGizmoSource source, int index, PungentSceneGizmoSource.RuleStatus status)
        {
            PungentSceneGizmoSource.GizmoRule rule = source.rules[index];
            bool selected = index == _designSelectedRuleIndex;
            Color tint = selected ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral;
            if (status != null && status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Error)
                tint = UtilityWindowTheme.Red;
            else if (status != null && status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Warning)
                tint = UtilityWindowTheme.Amber;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.15f : 0.09f, selected ? 0.07f : 0.035f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool nextSelected = GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(18f));
                    if (nextSelected && !selected)
                    {
                        _designSelectedRuleIndex = index;
                        SaveDesignPrefs();
                    }

                    bool nextEnabled = EditorGUILayout.Toggle(rule.enabled, GUILayout.Width(18f));
                    if (nextEnabled != rule.enabled && PungentSceneGizmoCommandService.SetRuleEnabled(source, index, nextEnabled))
                    {
                        _status = (nextEnabled ? "Enabled " : "Disabled ") + RuleDisplayName(rule) + ".";
                        MarkCacheDirty();
                    }

                    if (GUILayout.Button(new GUIContent(RuleDisplayName(rule), "Select this rule for contextual editing."), EditorStyles.label, GUILayout.MinWidth(120f)))
                    {
                        _designSelectedRuleIndex = index;
                        SaveDesignPrefs();
                    }

                    UtilityWindowTheme.CountPill(rule.shape.ToString(), UtilityWindowTheme.Cyan, 96f);
                    PungentSceneGizmoRuleEditorUtility.DrawStatusChip(status, 66f);
                    UtilityWindowTheme.CountPill("Ops " + (status != null ? status.estimatedDrawOperations : 0), UtilityWindowTheme.Teal, 64f);
                    UtilityWindowTheme.CountPill("Labels " + (status != null ? status.labelCountEstimate : 0), UtilityWindowTheme.Purple, 78f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Up", index > 0, "This rule is already first.", () => MoveDesignRule(source, index, index - 1), 42f);
                    DrawActionButton("Down", source.rules != null && index < source.rules.Count - 1, "This rule is already last.", () => MoveDesignRule(source, index, index + 1), 54f);
                    DrawActionButton("Dup", true, "Duplicate this rule.", () => DuplicateDesignRule(source, index), 46f);
                    DrawActionButton("Remove", true, "Remove this rule. Undo is available.", () => RemoveDesignRule(source, index), 66f);
                    DrawActionButton("Focus", true, "Select and frame the source object in Scene View.", () => FocusDesignSource(source), 54f);
                    if (status != null && status.messages != null && status.messages.Count > 0 && !string.Equals(status.messages[0], "Ready.", StringComparison.Ordinal))
                        EditorGUILayout.LabelField(status.messages[0], UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDesignPropertiesPane(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status, List<BrowserRecord> visibleRecords, params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple), options))
            {
                string title = _designSelectedRuleIndex >= 0 ? "Rule Properties" : "Source Properties";
                UtilityWindowTheme.SectionTitle("Properties", UtilityWindowTheme.Purple, title);
                _designPropertiesScroll = EditorGUILayout.BeginScrollView(_designPropertiesScroll, GUILayout.ExpandHeight(true));
                if (_designSelectedRuleIndex >= 0)
                    DrawDesignSelectedRuleProperties(source);
                else
                    DrawDesignSourceProperties(source, status, visibleRecords);

                DrawDesignSimulationCard(source);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDesignSelectedRuleProperties(PungentSceneGizmoSource source)
        {
            if (source == null || source.rules == null || _designSelectedRuleIndex < 0 || _designSelectedRuleIndex >= source.rules.Count)
            {
                _designSelectedRuleIndex = -1;
                DrawDesignSourceProperties(source, PungentSceneGizmoPresetDiffUtility.GetStatus(source), _records);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Editing " + RuleDisplayName(source.rules[_designSelectedRuleIndex]), EditorStyles.boldLabel);
                if (GUILayout.Button("Source", EditorStyles.miniButton, GUILayout.Width(62f)))
                {
                    _designSelectedRuleIndex = -1;
                    SaveDesignPrefs();
                    GUIUtility.ExitGUI();
                }
            }

            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty rules = serializedSource.FindProperty("rules");
            SerializedProperty rule = rules != null && _designSelectedRuleIndex < rules.arraySize
                ? rules.GetArrayElementAtIndex(_designSelectedRuleIndex)
                : null;

            if (rule == null)
            {
                EditorGUILayout.HelpBox("Selected rule could not be resolved.", MessageType.Warning);
                return;
            }

            _designShowAdvancedRuleFields = EditorGUILayout.Foldout(_designShowAdvancedRuleFields, "Advanced Rule Fields", true);
            PungentSceneGizmoRuleEditorUtility.DrawCompactRuleProperties(rule, source.GetRuleStatus(_designSelectedRuleIndex), _designShowAdvancedRuleFields);
            if (serializedSource.ApplyModifiedProperties())
            {
                source.InvalidateChildBoundsCache();
                EditorUtility.SetDirty(source);
                _status = "Updated rule properties.";
                MarkCacheDirty();
                RepaintActiveSceneView();
            }
        }

        private void DrawDesignSourceProperties(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status, List<BrowserRecord> visibleRecords)
        {
            if (source == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 4, 2)))
            {
                UtilityWindowTheme.SectionTitle("Source Defaults", UtilityWindowTheme.Teal);
                SerializedObject serializedSource = new SerializedObject(source);
                serializedSource.Update();
                EditorGUILayout.PropertyField(serializedSource.FindProperty("drawInScene"));
                EditorGUILayout.PropertyField(serializedSource.FindProperty("drawLabels"));
                EditorGUILayout.PropertyField(serializedSource.FindProperty("drawOnlyWhenComponentEnabled"));
                EditorGUILayout.PropertyField(serializedSource.FindProperty("cacheChildRendererBounds"));
                SerializedProperty cacheChildBounds = serializedSource.FindProperty("cacheChildRendererBounds");
                using (new EditorGUI.DisabledScope(cacheChildBounds != null && !cacheChildBounds.boolValue))
                    EditorGUILayout.PropertyField(serializedSource.FindProperty("childBoundsRefreshInterval"));
                if (serializedSource.ApplyModifiedProperties())
                {
                    source.InvalidateChildBoundsCache();
                    EditorUtility.SetDirty(source);
                    _status = "Updated source defaults.";
                    MarkCacheDirty();
                }
            }

            DrawDesignPresetLink(source, status, visibleRecords);
        }

        private void DrawDesignSimulationCard(PungentSceneGizmoSource source)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.10f, 0.04f, 5, 3)))
            {
                UtilityWindowTheme.SectionTitle("Scene Preview Inputs", UtilityWindowTheme.Amber, "2D guide");
                EditorGUILayout.LabelField("These inputs only drive the schematic and Scene View refresh helpers.", UtilityWindowTheme.MutedMiniLabelStyle);
                _simulationState = EditorGUILayout.TextField("State", _simulationState);
                _simulationDirection = EditorGUILayout.Vector3Field("Direction", _simulationDirection);
                _simulationSpeed = EditorGUILayout.Slider("Speed", Mathf.Max(0f, _simulationSpeed), 0f, 100f);
                _simulationShowPrimitiveContext = EditorGUILayout.Toggle("Primitive Context", _simulationShowPrimitiveContext);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Focus Source", source != null, "Select a source first.", () => FocusDesignSource(source), 104f);
                    DrawActionButton("Refresh Scene", source != null, "Select a source first.", RepaintActiveSceneView, 108f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDesignStatusStrip(PungentSceneGizmoSource source)
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.07f, 0.025f, 4, 2)))
            {
                EditorGUILayout.LabelField(_status, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(180f));
                GUILayout.FlexibleSpace();
                if (source != null)
                {
                    UtilityWindowTheme.CountPill("Rules " + source.EstimateEnabledRuleCount(), UtilityWindowTheme.Cyan, 82f);
                    UtilityWindowTheme.CountPill("Draw Ops " + source.EstimateDrawOperations(), UtilityWindowTheme.Teal, 104f);
                    UtilityWindowTheme.CountPill("Labels " + source.EstimateLabelCount(), UtilityWindowTheme.Purple, 86f);
                }
                UtilityWindowTheme.CountPill(_cacheDirty ? "Cache Dirty" : "Cache Ready", _cacheDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 104f);
            }
        }

        private void ClampDesignPanelWidths()
        {
            float available = DesignAvailableWidth();
            float maxLeft = Mathf.Max(MinDesignPaletteWidth, available - MinDesignWorkspaceWidth - MinDesignPropertiesWidth - DesignSplitterReserve);
            _designLeftWidth = Mathf.Clamp(_designLeftWidth, MinDesignPaletteWidth, Mathf.Min(420f, maxLeft));

            float maxRight = Mathf.Max(MinDesignPropertiesWidth, available - _designLeftWidth - MinDesignWorkspaceWidth - DesignSplitterReserve);
            _designRightWidth = Mathf.Clamp(_designRightWidth, MinDesignPropertiesWidth, Mathf.Min(480f, maxRight));
        }

        private float DesignAvailableWidth()
        {
            return Mathf.Max(MinDesignPaletteWidth + MinDesignWorkspaceWidth + MinDesignPropertiesWidth + DesignSplitterReserve, position.width - 24f);
        }

        private float MaxDesignLeftWidth()
        {
            return Mathf.Max(MinDesignPaletteWidth, DesignAvailableWidth() - _designRightWidth - MinDesignWorkspaceWidth - DesignSplitterReserve);
        }

        private float MaxDesignRightWidth()
        {
            return Mathf.Max(MinDesignPropertiesWidth, DesignAvailableWidth() - _designLeftWidth - MinDesignWorkspaceWidth - DesignSplitterReserve);
        }

        private void SaveDesignPrefs()
        {
            UtilityWindowPrefs.SetString(PrefDesignBlockCategory, _designBlockCategory ?? "All");
            UtilityWindowPrefs.SetString(PrefDesignPaletteSearch, _designPaletteSearch ?? string.Empty);
            UtilityWindowPrefs.SetFloat(PrefDesignLeftWidth, _designLeftWidth);
            UtilityWindowPrefs.SetFloat(PrefDesignRightWidth, _designRightWidth);
            UtilityWindowPrefs.SetInt(PrefDesignSelectedRuleIndex, _designSelectedRuleIndex);
            UtilityWindowPrefs.SetInt(PrefDesignNarrowTab, (int)_designNarrowTab);
        }

        private void ClampDesignSelectedRule(PungentSceneGizmoSource source)
        {
            int count = source != null && source.rules != null ? source.rules.Count : 0;
            if (count == 0)
                _designSelectedRuleIndex = -1;
            else
                _designSelectedRuleIndex = Mathf.Clamp(_designSelectedRuleIndex, -1, count - 1);
        }

        private PungentSceneGizmoSource.GizmoRule GetSelectedRule(PungentSceneGizmoSource source)
        {
            if (source == null || source.rules == null || _designSelectedRuleIndex < 0 || _designSelectedRuleIndex >= source.rules.Count)
                return null;

            return source.rules[_designSelectedRuleIndex];
        }

        private static string RuleDisplayName(PungentSceneGizmoSource.GizmoRule rule)
        {
            return rule != null && !string.IsNullOrWhiteSpace(rule.name) ? rule.name : "Gizmo Rule";
        }

        private void FocusDesignSource(PungentSceneGizmoSource source)
        {
            if (source == null || source.gameObject == null)
                return;

            Selection.activeObject = source.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
            _status = "Focused " + source.gameObject.name + " in Scene View.";
            RepaintActiveSceneView();
        }

        private void MoveDesignRule(PungentSceneGizmoSource source, int fromIndex, int toIndex)
        {
            if (PungentSceneGizmoCommandService.MoveRule(source, fromIndex, toIndex))
            {
                _designSelectedRuleIndex = toIndex;
                _status = "Moved rule.";
                SaveDesignPrefs();
                MarkCacheDirty();
                GUIUtility.ExitGUI();
            }
        }

        private void DuplicateDesignRule(PungentSceneGizmoSource source, int index)
        {
            if (PungentSceneGizmoCommandService.DuplicateRule(source, index, out int newIndex))
            {
                _designSelectedRuleIndex = newIndex;
                _status = "Duplicated rule.";
                SaveDesignPrefs();
                MarkCacheDirty();
                GUIUtility.ExitGUI();
            }
        }

        private void RemoveDesignRule(PungentSceneGizmoSource source, int index)
        {
            if (PungentSceneGizmoCommandService.RemoveRule(source, index))
            {
                int count = source != null && source.rules != null ? source.rules.Count : 0;
                _designSelectedRuleIndex = count == 0 ? -1 : Mathf.Clamp(index, 0, count - 1);
                _status = "Removed rule. Undo is available.";
                SaveDesignPrefs();
                MarkCacheDirty();
                GUIUtility.ExitGUI();
            }
        }

        private void ShowApplyPresetMenuForSource(PungentSceneGizmoSource source)
        {
            GenericMenu menu = new GenericMenu();
            if (source == null)
            {
                menu.AddDisabledItem(new GUIContent("Select a source first"));
                menu.ShowAsContext();
                return;
            }

            PungentSceneGizmoPresetLibrary.AddBuiltInPresetMenu(menu, preset => ApplyBuiltInPresetToDesignSource(source, preset));
            if (_assetPreset != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Apply Assigned Asset/" + _assetPreset.displayName), false, () => ApplyPresetToDesignSource(source, _assetPreset, _assetPreset.displayName));
            }
            menu.ShowAsContext();
        }

        private void ApplyBuiltInPresetToDesignSource(PungentSceneGizmoSource source, PungentSceneGizmoBuiltInPreset builtIn)
        {
            if (source == null || builtIn == null)
                return;

            PungentSceneGizmoPreset preset = PungentSceneGizmoPresetLibrary.CreateTransientPreset(builtIn);
            if (preset == null)
                return;

            try
            {
                ApplyPresetToDesignSource(source, preset, builtIn.DisplayName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        private void ApplyPresetToDesignSource(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset, string displayName)
        {
            if (PungentSceneGizmoCommandService.ApplyPresetToSource(source, preset, _replacePresetRules, out PungentSceneGizmoPresetApplySummary summary))
            {
                _status = "Applied " + displayName + ". " + summary;
                _designSelectedRuleIndex = -1;
                SaveDesignPrefs();
                MarkCacheDirty();
            }
            else
            {
                _status = "Preset '" + displayName + "' made no changes.";
            }
        }

        private static IReadOnlyList<PungentSceneGizmoSource.GizmoRule> BuildSchematicFocusRules(PungentSceneGizmoSource source, PungentSceneGizmoSource.GizmoRule selectedRule)
        {
            if (selectedRule != null)
                return new[] { selectedRule };
            if (source == null || source.rules == null)
                return Array.Empty<PungentSceneGizmoSource.GizmoRule>();
            return source.rules.Where(rule => rule != null && rule.enabled).Take(8).ToArray();
        }

        private static void DrawSchematicGrid(Rect rect)
        {
            Color grid = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.08f);
            for (float x = rect.x; x <= rect.xMax; x += 24f)
                DrawSchematicLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax), grid, 1f);
            for (float y = rect.y; y <= rect.yMax; y += 24f)
                DrawSchematicLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y), grid, 1f);
        }

        private static void DrawSchematicOrigin(Vector2 origin, Color color)
        {
            Color fill = new Color(color.r, color.g, color.b, 0.90f);
            Handles.color = fill;
            Handles.DrawSolidDisc(origin, Vector3.forward, 7f);
            Handles.color = Color.white;
            Handles.DrawWireDisc(origin, Vector3.forward, 10f);
            DrawSchematicText(new Rect(origin.x - 38f, origin.y + 12f, 76f, 18f), "Origin");
        }

        private static void DrawSchematicTargetIfNeeded(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules, Vector2 target)
        {
            if (!RulesNeedTarget(rules))
                return;

            Handles.color = UtilityWindowTheme.Amber;
            Handles.DrawWireDisc(target, Vector3.forward, 9f);
            DrawSchematicText(new Rect(target.x - 36f, target.y + 12f, 72f, 18f), "Target");
        }

        private static void DrawSchematicTargetLinkIfNeeded(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules, Vector2 origin, Vector2 target)
        {
            if (!RulesNeedTarget(rules))
                return;

            DrawSchematicLine(origin, target, UtilityWindowTheme.Amber, 2f);
            DrawSchematicText(new Rect((origin.x + target.x) * 0.5f - 40f, (origin.y + target.y) * 0.5f - 18f, 80f, 18f), "Distance");
        }

        private void DrawSchematicVectorsIfNeeded(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules, Vector2 origin, Color color)
        {
            if (!RulesNeedVector(rules))
                return;

            bool fan = RulesLookLikeProbeFan(rules);
            Vector2 direction = SchematicDirectionVector();
            DrawSchematicArrow(origin, origin + direction * 74f, color, 3f);
            if (fan)
            {
                DrawSchematicArrow(origin, origin + Rotate(direction, -25f) * 62f, new Color(color.r, color.g, color.b, 0.65f), 2f);
                DrawSchematicArrow(origin, origin + Rotate(direction, 25f) * 62f, new Color(color.r, color.g, color.b, 0.65f), 2f);
            }
        }

        private void DrawSchematicTrajectoryIfNeeded(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules, Vector2 origin, Color color)
        {
            if (!RulesNeedTrajectory(rules))
                return;

            Vector3 dir3 = _simulationDirection.sqrMagnitude > 0.001f ? _simulationDirection.normalized : Vector3.forward;
            float speed = Mathf.Clamp(_simulationSpeed, 1f, 100f);
            Vector2 direction = new Vector2(dir3.x, -dir3.z);
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;
            direction.Normalize();

            Vector3[] points = new Vector3[16];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                Vector2 p = origin + direction * (t * Mathf.Lerp(80f, 150f, speed / 100f));
                p.y -= Mathf.Sin(t * Mathf.PI) * 54f;
                points[i] = p;
            }
            Handles.color = new Color(color.r, color.g, color.b, 0.85f);
            Handles.DrawAAPolyLine(3f, points);
            DrawSchematicText(new Rect(points[points.Length - 1].x - 36f, points[points.Length - 1].y - 22f, 72f, 18f), "Trajectory");
        }

        private static void DrawSchematicVolumeIfNeeded(Rect rect, IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules, Color color)
        {
            if (!RulesNeedVolume(rules))
                return;

            Rect volume = new Rect(rect.x + rect.width * 0.58f, rect.y + rect.height * 0.48f, 86f, 52f);
            Color fill = new Color(color.r, color.g, color.b, 0.12f);
            Color outline = new Color(color.r, color.g, color.b, 0.85f);
            Handles.DrawSolidRectangleWithOutline(volume, fill, outline);
            DrawSchematicText(new Rect(volume.x, volume.yMax + 3f, volume.width, 18f), "Volume");
        }

        private static void DrawSchematicLabelIfNeeded(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules, PungentSceneGizmoSource.GizmoRule selectedRule, Vector2 origin)
        {
            if (!RulesNeedLabel(rules))
                return;

            string label = selectedRule != null && !string.IsNullOrWhiteSpace(selectedRule.label) ? selectedRule.label : "Label";
            DrawSchematicText(new Rect(origin.x - 22f, origin.y - 36f, 92f, 20f), label);
        }

        private static void DrawSchematicStateStrip(Rect rect, PungentSceneGizmoSource.GizmoRule selectedRule)
        {
            if (selectedRule == null || !selectedRule.useStateColorMap || selectedRule.stateColorMap == null || selectedRule.stateColorMap.entries == null || selectedRule.stateColorMap.entries.Count == 0)
                return;

            Rect strip = new Rect(rect.x + 12f, rect.yMax - 26f, rect.width - 24f, 14f);
            int count = Mathf.Min(6, selectedRule.stateColorMap.entries.Count);
            float width = strip.width / count;
            for (int i = 0; i < count; i++)
            {
                PungentSceneGizmoStateColorEntry entry = selectedRule.stateColorMap.entries[i];
                Color color = entry != null ? entry.color : UtilityWindowTheme.Neutral;
                EditorGUI.DrawRect(new Rect(strip.x + width * i, strip.y, width - 2f, strip.height), color);
            }
            DrawSchematicText(new Rect(strip.x, strip.y - 18f, strip.width, 16f), "State Color Map");
        }

        private Vector2 SchematicDirectionVector()
        {
            Vector3 direction = _simulationDirection.sqrMagnitude > 0.001f ? _simulationDirection.normalized : Vector3.forward;
            Vector2 vector = new Vector2(direction.x, -direction.z);
            if (vector.sqrMagnitude < 0.001f)
                vector = Vector2.right;
            return vector.normalized;
        }

        private static void DrawSchematicArrow(Vector2 start, Vector2 end, Color color, float width)
        {
            DrawSchematicLine(start, end, color, width);
            Vector2 direction = (end - start).normalized;
            Vector2 left = Rotate(-direction, -28f) * 12f;
            Vector2 right = Rotate(-direction, 28f) * 12f;
            DrawSchematicLine(end, end + left, color, width);
            DrawSchematicLine(end, end + right, color, width);
        }

        private static void DrawSchematicLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(width, new Vector3(start.x, start.y, 0f), new Vector3(end.x, end.y, 0f));
        }

        private static void DrawSchematicText(Rect rect, string text)
        {
            Color previous = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            GUI.Label(rect, text, EditorStyles.centeredGreyMiniLabel);
            GUI.color = previous;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        private static bool RulesNeedTarget(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules)
        {
            return rules != null && rules.Any(rule =>
                rule != null &&
                (rule.shape == PungentSceneGizmoSource.GizmoShape.DistanceBetween ||
                 rule.positionMode == PungentSceneGizmoSource.PositionMode.SecondaryTransform ||
                 rule.positionMode == PungentSceneGizmoSource.PositionMode.MidpointToSecondary ||
                 rule.secondaryTransform != null));
        }

        private static bool RulesNeedVector(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules)
        {
            return rules != null && rules.Any(rule =>
                rule != null &&
                (PungentSceneGizmoRuleEditorUtility.ShapeUsesDirection(rule.shape) ||
                 ContainsIgnoreCase(rule.name, "probe") ||
                 ContainsIgnoreCase(rule.name, "direction") ||
                 ContainsIgnoreCase(rule.name, "velocity")));
        }

        private static bool RulesNeedVolume(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules)
        {
            return rules != null && rules.Any(rule =>
                rule != null &&
                (rule.shape == PungentSceneGizmoSource.GizmoShape.ColliderBounds ||
                 rule.shape == PungentSceneGizmoSource.GizmoShape.Bounds ||
                 rule.shape == PungentSceneGizmoSource.GizmoShape.Cube ||
                 rule.shape == PungentSceneGizmoSource.GizmoShape.WireCube ||
                 rule.shape == PungentSceneGizmoSource.GizmoShape.ChildBounds));
        }

        private static bool RulesNeedLabel(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules)
        {
            return rules != null && rules.Any(rule =>
                rule != null &&
                (PungentSceneGizmoRuleEditorUtility.ShapeCanShowLabel(rule.shape) ||
                 !string.IsNullOrWhiteSpace(rule.label) ||
                 !string.IsNullOrWhiteSpace(rule.labelFieldPath)));
        }

        private static bool RulesNeedTrajectory(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules)
        {
            return rules != null && rules.Any(rule => rule != null && ContainsIgnoreCase(rule.name, "trajectory"));
        }

        private static bool RulesLookLikeProbeFan(IReadOnlyList<PungentSceneGizmoSource.GizmoRule> rules)
        {
            if (rules == null)
                return false;
            int probeCount = rules.Count(rule => rule != null && ContainsIgnoreCase(rule.name, "probe"));
            return probeCount >= 2 || rules.Any(rule => rule != null && ContainsIgnoreCase(rule.name, "fan"));
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(query) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawDesignSourceHeader(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Selected Source", UtilityWindowTheme.Teal, status.stateLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(source, typeof(PungentSceneGizmoSource), true);
                    DrawPresetStateChip(status, 86f);
                    UtilityWindowTheme.CountPill("Rules " + (source.rules != null ? source.rules.Count : 0), UtilityWindowTheme.Cyan, 82f);
                }

                if (!string.IsNullOrWhiteSpace(status.message))
                    EditorGUILayout.HelpBox(status.message, status.state == PungentSceneGizmoPresetInstanceState.Base || status.state == PungentSceneGizmoPresetInstanceState.Unlinked ? MessageType.Info : MessageType.Warning);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select", GUILayout.Width(70f)))
                        Selection.activeObject = source.gameObject;
                    if (GUILayout.Button("Frame In Scene", GUILayout.Width(108f)))
                    {
                        Selection.activeObject = source.gameObject;
                        if (SceneView.lastActiveSceneView != null)
                            SceneView.lastActiveSceneView.FrameSelected();
                    }
                    if (GUILayout.Button("Open Inspector", GUILayout.Width(112f)))
                    {
                        Selection.activeObject = source;
                        EditorGUIUtility.PingObject(source);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDesignPresetLink(PungentSceneGizmoSource source, PungentSceneGizmoPresetDiffStatus status, List<BrowserRecord> visibleRecords)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Preset Link & Propagation", UtilityWindowTheme.Purple, status.hasLinkedPreset ? status.linkedPresetId : "custom");

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Linked Preset", source.linkedPreset, typeof(PungentSceneGizmoPreset), false);
                    DrawActionButton("Clear Link", status.hasLinkedPreset, "This source is not linked.", () =>
                    {
                        PungentSceneGizmoCommandService.ClearPresetLink(source);
                        _status = "Cleared preset link.";
                        MarkCacheDirty();
                    }, 82f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Preset ID", "Stable built-in or asset preset ID."), GUILayout.Width(64f));
                    EditorGUILayout.SelectableLabel(string.IsNullOrWhiteSpace(source.linkedPresetId) ? "Unlinked" : source.linkedPresetId, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool nextLocked = GUILayout.Toggle(source.lockPresetPropagation, new GUIContent("Lock From Preset Updates", "Locked sources are skipped by batch preset updates."), EditorStyles.toolbarButton, GUILayout.Width(176f));
                    if (nextLocked != source.lockPresetPropagation && PungentSceneGizmoCommandService.SetPresetLock(source, nextLocked))
                    {
                        _status = nextLocked ? "Locked source from preset updates." : "Unlocked source for preset updates.";
                        MarkCacheDirty();
                    }

                    EditorGUI.BeginChangeCheck();
                    PungentSceneGizmoPresetPropagationMode nextMode = (PungentSceneGizmoPresetPropagationMode)EditorGUILayout.EnumPopup(source.presetPropagationMode, GUILayout.MaxWidth(178f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(source, "Change Scene Gizmo Preset Propagation");
                        source.presetPropagationMode = nextMode;
                        EditorUtility.SetDirty(source);
                        _status = "Updated propagation mode.";
                        MarkCacheDirty();
                    }
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Update From Preset", status.canUpdateFromPreset, status.hasLinkedPreset ? "Already matches or source is locked." : "Link a preset first.", () =>
                    {
                        if (PungentSceneGizmoCommandService.UpdateSourceFromLinkedPreset(source, out PungentSceneGizmoPresetApplySummary summary))
                        {
                            _status = "Updated from preset. " + summary;
                            MarkCacheDirty();
                        }
                    }, 132f);

                    DrawActionButton("Revert", status.canRevertToPreset, "No linked preset changes to revert.", () =>
                    {
                        if (!EditorUtility.DisplayDialog("Revert Scene Gizmo Source", "Replace this source's rules with the linked preset?", "Revert", "Cancel"))
                            return;

                        if (PungentSceneGizmoCommandService.RevertSourceToLinkedPreset(source, out PungentSceneGizmoPresetApplySummary summary))
                        {
                            _status = "Reverted to preset. " + summary;
                            MarkCacheDirty();
                        }
                    }, 72f);

                    DrawActionButton("Overwrite Preset", status.canOverwritePreset, "Only linked preset assets can be overwritten.", () =>
                    {
                        if (!EditorUtility.DisplayDialog("Overwrite Scene Gizmo Preset", "Overwrite the linked preset asset with this source's current rules? Scene references will be stripped from the asset.", "Overwrite", "Cancel"))
                            return;

                        if (PungentSceneGizmoCommandService.OverwriteLinkedPreset(source, out PungentSceneGizmoPreset preset))
                        {
                            _status = "Overwrote preset " + preset.displayName + ".";
                            MarkCacheDirty();
                            MaybeOfferAutoUpdateLinkedInstances(source, preset);
                        }
                    }, 126f);

                    DrawActionButton("Save As New", status.canSaveAsNewPreset, "Add at least one rule before saving a preset.", () =>
                    {
                        if (PungentSceneGizmoCommandService.SaveSourceAsNewPreset(source, out PungentSceneGizmoPreset preset))
                        {
                            _assetPreset = preset;
                            _status = "Saved and linked new preset " + preset.displayName + ".";
                            MarkCacheDirty();
                        }
                    }, 94f);
                    GUILayout.FlexibleSpace();
                }

                DrawLinkedScopeActions(source, visibleRecords);
            }
        }

        private void DrawLinkedScopeActions(PungentSceneGizmoSource source, List<BrowserRecord> visibleRecords)
        {
            List<PungentSceneGizmoSource> selectedLinked = LinkedSourcesForDesignSource(source, GetSelectedRecords(visibleRecords, visibleOnly: false));
            List<PungentSceneGizmoSource> visibleLinked = LinkedSourcesForDesignSource(source, visibleRecords);
            List<PungentSceneGizmoSource> allLinked = LinkedSourcesForDesignSource(source, _records);

            EditorGUILayout.LabelField("Batch Update Linked Sources", UtilityWindowTheme.MutedMiniLabelStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawActionButton("Selected Linked", selectedLinked.Count > 0, "No selected rows share this preset link.", () => ConfirmAndUpdateLinkedScope(source, selectedLinked, "selected linked sources"), 116f);
                DrawActionButton("Visible Linked", visibleLinked.Count > 0, "No visible rows share this preset link.", () => ConfirmAndUpdateLinkedScope(source, visibleLinked, "visible linked sources"), 106f);
                DrawActionButton("All Open Linked", allLinked.Count > 0, "No open-scene rows share this preset link.", () => ConfirmAndUpdateLinkedScope(source, allLinked, "all linked open-scene sources"), 116f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawDesignBlockPalette(PungentSceneGizmoSource source)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("Building Blocks", UtilityWindowTheme.Green, "modular rules + providers");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Category", GUILayout.Width(62f));
                    _designBlockCategory = DrawBlockCategoryPopup(_designBlockCategory);
                    GUILayout.FlexibleSpace();
                }

                IEnumerable<PungentSceneGizmoBlockDefinition> blocks = PungentSceneGizmoBlockLibrary.InCategory(_designBlockCategory);
                foreach (PungentSceneGizmoBlockDefinition block in blocks)
                    DrawBlockButton(source, block);
            }
        }

        private void DrawBlockButton(PungentSceneGizmoSource source, PungentSceneGizmoBlockDefinition block)
        {
            if (block == null)
                return;

            bool compact = position.width < 620f;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(block.CreatesProvider ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 0.08f, 0.03f, 4, 2)))
            {
                if (compact)
                {
                    if (GUILayout.Button(new GUIContent(block.DisplayName, block.Description)))
                        ApplyBlockToDesignSource(source, block);
                    EditorGUILayout.LabelField(block.Description, UtilityWindowTheme.MutedMiniLabelStyle);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(block.DisplayName, block.Description), GUILayout.Width(162f)))
                            ApplyBlockToDesignSource(source, block);
                        EditorGUILayout.LabelField(block.Description, UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
            }
        }

        private void DrawDesignSimulationControls(PungentSceneGizmoSource source)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Simulation Inputs", UtilityWindowTheme.Amber, "stage 1");
                EditorGUILayout.LabelField("Workspace-only preview inputs. No preview objects are created or saved in this pass.", UtilityWindowTheme.MutedMiniLabelStyle);
                _simulationState = EditorGUILayout.TextField("State", _simulationState);
                _simulationDirection = EditorGUILayout.Vector3Field("Direction", _simulationDirection);
                _simulationSpeed = Mathf.Max(0f, EditorGUILayout.FloatField("Speed", _simulationSpeed));
                _simulationShowPrimitiveContext = EditorGUILayout.Toggle("Primitive Context", _simulationShowPrimitiveContext);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Focus Source", GUILayout.Width(104f)))
                    {
                        Selection.activeObject = source.gameObject;
                        if (SceneView.lastActiveSceneView != null)
                            SceneView.lastActiveSceneView.FrameSelected();
                    }
                    if (GUILayout.Button("Refresh Scene View", GUILayout.Width(126f)))
                        RepaintActiveSceneView();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawDesignSourceRules(PungentSceneGizmoSource source)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                int count = source.rules != null ? source.rules.Count : 0;
                UtilityWindowTheme.SectionTitle("Source Rules", UtilityWindowTheme.Neutral, count + " rule" + (count == 1 ? string.Empty : "s"));
                if (count == 0)
                {
                    EditorGUILayout.HelpBox("Add a block or apply a preset to start composing this source.", MessageType.Info);
                    return;
                }

                List<PungentSceneGizmoSource.RuleStatus> statuses = source.GetRuleStatuses();
                int shown = Mathf.Min(count, 8);
                for (int i = 0; i < shown; i++)
                {
                    PungentSceneGizmoSource.GizmoRule rule = source.rules[i];
                    PungentSceneGizmoSource.RuleStatus status = i < statuses.Count ? statuses[i] : null;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(rule != null && !string.IsNullOrWhiteSpace(rule.name) ? rule.name : "Rule " + (i + 1), GUILayout.MinWidth(120f));
                        if (status != null)
                            UtilityWindowTheme.CountPill(status.IsValid ? "Ready" : "Invalid", status.IsValid ? UtilityWindowTheme.Green : UtilityWindowTheme.Red, 72f);
                        if (rule != null && !string.IsNullOrWhiteSpace(rule.presetCategory))
                            UtilityWindowTheme.CountPill(rule.presetCategory, UtilityWindowTheme.Cyan, 92f);
                        GUILayout.FlexibleSpace();
                    }
                }

                if (count > shown)
                    EditorGUILayout.LabelField((count - shown) + " more rule(s). Open the inspector for full raw rule editing.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private PungentSceneGizmoSource ResolveDesignSource(List<BrowserRecord> visibleRecords)
        {
            BrowserRecord selectedVisible = visibleRecords != null
                ? visibleRecords.FirstOrDefault(r => r != null && r.kind == RecordKind.Source && r.source != null && _selectedRecordIds.Contains(r.instanceId))
                : null;
            if (selectedVisible != null)
                return selectedVisible.source;

            BrowserRecord selectedAny = _records.FirstOrDefault(r => r != null && r.kind == RecordKind.Source && r.source != null && _selectedRecordIds.Contains(r.instanceId));
            if (selectedAny != null)
                return selectedAny.source;

            if (Selection.activeObject is PungentSceneGizmoSource selectedSource)
                return selectedSource;

            if (Selection.activeGameObject != null)
            {
                PungentSceneGizmoSource sourceOnSelection = Selection.activeGameObject.GetComponent<PungentSceneGizmoSource>();
                if (sourceOnSelection != null)
                    return sourceOnSelection;
            }

            BrowserRecord firstVisible = visibleRecords != null ? visibleRecords.FirstOrDefault(r => r != null && r.kind == RecordKind.Source && r.source != null) : null;
            return firstVisible != null ? firstVisible.source : null;
        }

        private string DrawBlockCategoryPopup(string current)
        {
            List<string> categories = new List<string> { "All" };
            categories.AddRange(PungentSceneGizmoBlockLibrary.Categories);
            int index = Mathf.Max(0, categories.FindIndex(c => string.Equals(c, current, StringComparison.OrdinalIgnoreCase)));
            index = EditorGUILayout.Popup(index, categories.ToArray(), GUILayout.MaxWidth(180f));
            return categories[Mathf.Clamp(index, 0, categories.Count - 1)];
        }

        private void ApplyBlockToDesignSource(PungentSceneGizmoSource source, PungentSceneGizmoBlockDefinition block)
        {
            int oldCount = source != null && source.rules != null ? source.rules.Count : 0;
            if (PungentSceneGizmoBlockLibrary.ApplyBlock(source, block, out PungentSceneGizmoBlockApplySummary summary))
            {
                _status = $"Added {block.DisplayName} block. {summary}";
                if (summary.rulesAdded > 0)
                {
                    _designSelectedRuleIndex = oldCount;
                    SaveDesignPrefs();
                }
                MarkCacheDirty();
            }
            else
            {
                _status = block != null ? $"Block '{block.DisplayName}' made no changes." : "No block selected.";
            }
        }

        private void ConfirmAndUpdateLinkedScope(PungentSceneGizmoSource designSource, List<PungentSceneGizmoSource> sources, string scopeLabel)
        {
            if (designSource == null || sources == null || sources.Count == 0)
                return;

            if (!PungentSceneGizmoPresetDiffUtility.TryResolveLinkedPreset(designSource, out PungentSceneGizmoPreset preset, out bool destroyWhenDone))
            {
                _status = "Could not resolve linked preset.";
                return;
            }

            try
            {
                if (!EditorUtility.DisplayDialog("Update Linked Scene Gizmos", $"Update {sources.Count} {scopeLabel} from '{preset.displayName}'? Locked sources will be skipped.", "Update", "Cancel"))
                    return;

                if (PungentSceneGizmoCommandService.UpdateOpenSceneLinkedInstances(preset, sources, includeLocked: false, out PungentSceneGizmoPresetApplySummary summary))
                {
                    _status = $"Updated {scopeLabel}. {summary}";
                    MarkCacheDirty();
                }
                else
                {
                    _status = $"No {scopeLabel} needed updating.";
                }
            }
            finally
            {
                if (destroyWhenDone && preset != null)
                    UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        private void MaybeOfferAutoUpdateLinkedInstances(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset)
        {
            if (source == null || preset == null || source.presetPropagationMode != PungentSceneGizmoPresetPropagationMode.AutoOnWorkspaceSave)
                return;

            List<PungentSceneGizmoSource> linked = LinkedSourcesForDesignSource(source, _records);
            if (linked.Count <= 1)
                return;

            if (!EditorUtility.DisplayDialog("Update Linked Instances", $"Auto On Workspace Save is enabled. Update {linked.Count} linked open-scene source(s) from the overwritten preset now?", "Update Linked", "Skip"))
                return;

            if (PungentSceneGizmoCommandService.UpdateOpenSceneLinkedInstances(preset, linked, includeLocked: false, out PungentSceneGizmoPresetApplySummary summary))
            {
                _status = "Updated linked instances after preset overwrite. " + summary;
                MarkCacheDirty();
            }
        }

        private List<PungentSceneGizmoSource> LinkedSourcesForDesignSource(PungentSceneGizmoSource source, IEnumerable<BrowserRecord> records)
        {
            List<PungentSceneGizmoSource> sources = new List<PungentSceneGizmoSource>();
            if (source == null || records == null)
                return sources;

            foreach (BrowserRecord record in records)
            {
                if (record == null || record.source == null || record.kind != RecordKind.Source)
                    continue;

                if (SamePresetLink(source, record.source) && !sources.Contains(record.source))
                    sources.Add(record.source);
            }

            return sources;
        }

        private static bool SamePresetLink(PungentSceneGizmoSource a, PungentSceneGizmoSource b)
        {
            if (a == null || b == null)
                return false;

            bool hasLink = a.linkedPreset != null || !string.IsNullOrWhiteSpace(a.linkedPresetGuid) || !string.IsNullOrWhiteSpace(a.linkedPresetId);
            if (!hasLink)
                return false;

            if (a == b)
                return true;

            if (a.linkedPreset != null && a.linkedPreset == b.linkedPreset)
                return true;

            if (!string.IsNullOrWhiteSpace(a.linkedPresetGuid) && string.Equals(a.linkedPresetGuid, b.linkedPresetGuid, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(a.linkedPresetId) && string.Equals(a.linkedPresetId, b.linkedPresetId, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawManageTab(List<BrowserRecord> visibleRecords)
        {
            List<BrowserRecord> selectedVisible = GetSelectedRecords(visibleRecords, visibleOnly: true);
            List<BrowserRecord> selectedAll = GetSelectedRecords(visibleRecords, visibleOnly: false);
            List<BrowserRecord> highCostVisible = visibleRecords.Where(IsHighCostRecord).ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Manage", UtilityWindowTheme.Purple, "Scoped batch controls");
                EditorGUILayout.HelpBox("Use Selected Visible for rows currently shown, All Selected for selected rows hidden by filters too, and Visible Rows for the whole filtered list.", MessageType.Info);
                DrawScopedActionGroup("Selected Visible", selectedVisible, "Rows selected in this filtered view.");
                DrawScopedActionGroup("All Selected", selectedAll, "Every checked row, including rows hidden by the current filters.");
                DrawScopedActionGroup("Visible Rows", visibleRecords, "Every row currently visible after filters.");
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Cleanup", UtilityWindowTheme.Amber, "Low-frequency performance actions");
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Disable Trajectories", visibleRecords != null && visibleRecords.Any(r => r.component is PungentTrajectoryVisualizer), "No visible trajectory visualizers.", () =>
                    {
                        if (PungentSceneGizmoPresetActions.DisableTrajectories(VisibleTrajectories(visibleRecords), out PungentSceneGizmoComponentActionSummary summary))
                        {
                            _status = "Disabled trajectories. " + summary;
                            MarkCacheDirty();
                        }
                        else
                        {
                            _status = "No visible trajectories needed disabling.";
                        }
                    }, 136f);

                    DrawActionButton("Disable Labels", visibleRecords != null && visibleRecords.Any(r => r.estimatedLabels > 0), "No visible rows estimate labels.", () =>
                    {
                        if (PungentSceneGizmoPresetActions.DisableLabels(VisibleComponents(visibleRecords), out PungentSceneGizmoComponentActionSummary summary))
                        {
                            _status = "Disabled labels. " + summary;
                            MarkCacheDirty();
                        }
                        else
                        {
                            _status = "No visible labels needed disabling.";
                        }
                    }, 112f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {

                    DrawActionButton("Disable Always-Visible Beacons", visibleRecords != null && visibleRecords.Any(r => r.component is PungentSceneBeacon && r.alwaysVisible), "No visible always-visible beacons.", () =>
                    {
                        if (PungentSceneGizmoPresetActions.DisableAlwaysVisibleBeacons(VisibleBeacons(visibleRecords), out PungentSceneGizmoComponentActionSummary summary))
                        {
                            _status = "Disabled always-visible beacons. " + summary;
                            MarkCacheDirty();
                        }
                        else
                        {
                            _status = "No always-visible beacons needed disabling.";
                        }
                    }, 198f);

                    DrawActionButton("Select High Cost", highCostVisible.Count > 0, "No high-cost visible rows.", () => SelectRecords(highCostVisible, ping: false), 112f);
                }
            }

            DrawRecordList(selectedVisible.Count > 0 ? selectedVisible : visibleRecords, selectedVisible.Count > 0 ? "Selected Visible Rows" : "Visible Rows", showHighCostOnly: false);
        }

        private void DrawScopedActionGroup(string label, List<BrowserRecord> records, string tooltip)
        {
            int count = records != null ? records.Count : 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.boldLabel, GUILayout.Width(108f));
                EditorGUILayout.LabelField(count + " row" + (count == 1 ? string.Empty : "s"), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(72f));
                DrawActionButton("Show", count > 0, label + " has no rows.", () => SetDrawState(records, true), 56f);
                DrawActionButton("Hide", count > 0, label + " has no rows.", () => SetDrawState(records, false), 56f);
                DrawActionButton("Select", count > 0, label + " has no rows.", () => SelectRecords(records, ping: false), 64f);
                DrawActionButton("Ping", count > 0, label + " has no rows.", () => PingRecords(records), 56f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPresetsTab(List<BrowserRecord> visibleRecords)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("Presets", UtilityWindowTheme.Green, "Create visuals from selection");
                DrawPresetActions();
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Provider Components", UtilityWindowTheme.Teal, "Add no-code live diagnostics");
                bool hasSelection = Selection.gameObjects != null && Selection.gameObjects.Length > 0;
                if (!hasSelection)
                    EditorGUILayout.HelpBox("Select one or more scene GameObjects to add provider components.", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Add Beacon", hasSelection, "Select scene GameObject(s) first.", () => RunComponentSetup(PungentSceneGizmoPresetActions.AddBeaconToSelection, "Beacon"), 94f);
                    DrawActionButton("Add Collision", hasSelection, "Select scene GameObject(s) first.", () => RunComponentSetup(PungentSceneGizmoPresetActions.AddCollisionSensorToSelection, "Collision sensor"), 108f);
                    DrawActionButton("Add Trigger", hasSelection, "Select scene GameObject(s) first.", () => RunComponentSetup(PungentSceneGizmoPresetActions.AddTriggerSensorToSelection, "Trigger sensor"), 96f);
                    DrawActionButton("Add Trajectory", hasSelection, "Select scene GameObject(s) first.", () => RunComponentSetup(PungentSceneGizmoPresetActions.AddTrajectoryVisualizerToSelection, "Trajectory visualizer"), 112f);
                }
            }

            DrawRecordList(visibleRecords, "Current Filtered Rows", showHighCostOnly: false);
        }

        private void DrawPerformanceTab(List<BrowserRecord> visibleRecords)
        {
            List<BrowserRecord> highCostRows = visibleRecords.Where(IsHighCostRecord).ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Performance", UtilityWindowTheme.Amber, "Draw operations, labels, trajectories");
                DrawPerformanceSummary();
                DrawWarningBannerIfNeeded();
                EditorGUILayout.LabelField("Provider discovery and source validation are cached on refresh/hierarchy changes; this tab does not add scene-wide validation during repaint.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                UtilityWindowTheme.SectionTitle("High Cost", UtilityWindowTheme.Amber, $"{highCostRows.Count} visible row{(highCostRows.Count == 1 ? string.Empty : "s")}");
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawActionButton("Select High Cost", highCostRows.Count > 0, "No high-cost visible rows.", () => SelectRecords(highCostRows, ping: false), 118f);
                    DrawActionButton("Hide High Cost", highCostRows.Count > 0, "No high-cost visible rows.", () => SetDrawState(highCostRows, false), 108f);
                    DrawActionButton("Disable Labels", highCostRows.Any(r => r.estimatedLabels > 0), "High-cost rows do not estimate labels.", () =>
                    {
                        if (PungentSceneGizmoPresetActions.DisableLabels(VisibleComponents(highCostRows), out PungentSceneGizmoComponentActionSummary summary))
                        {
                            _status = "Disabled labels on high-cost rows. " + summary;
                            MarkCacheDirty();
                        }
                    }, 112f);
                    GUILayout.FlexibleSpace();
                }
            }

            DrawRecordList(highCostRows, "High-Cost Rows", showHighCostOnly: true);
        }

        private void DrawPerformanceSummary()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Active " + _activeVisibleCount, UtilityWindowTheme.Cyan, 88f);
                UtilityWindowTheme.CountPill("Rules " + _enabledRuleCount, UtilityWindowTheme.Teal, 86f);
                UtilityWindowTheme.CountPill("Providers " + _providerCount, UtilityWindowTheme.Purple, 104f);
                UtilityWindowTheme.CountPill("Draw Ops " + _estimatedDrawOperations, _estimatedDrawOperations >= PungentSceneGizmoPerformancePolicy.DefaultProviderDrawBudget ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 112f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Labels " + _estimatedLabels, _estimatedLabels >= PungentSceneGizmoPerformancePolicy.WarningActiveLabels ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 94f);
                UtilityWindowTheme.CountPill("Trajectory Samples " + _estimatedTrajectorySamples, _estimatedTrajectorySamples >= PungentSceneGizmoPerformancePolicy.WarningTrajectorySamples ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 152f);
                UtilityWindowTheme.CountPill("Invalid " + _invalidRuleCount, _invalidRuleCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 88f);
                UtilityWindowTheme.CountPill("High Cost " + _expensiveWarningCount, _expensiveWarningCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 108f);
            }
        }

        private void DrawCounts()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Sources " + _sourceCount, UtilityWindowTheme.Cyan, 92f);
                UtilityWindowTheme.CountPill("Rules " + _enabledRuleCount, UtilityWindowTheme.Teal, 88f);
                UtilityWindowTheme.CountPill("Providers " + _providerCount, UtilityWindowTheme.Purple, 104f);
                UtilityWindowTheme.CountPill("Invalid " + _invalidRuleCount, _invalidRuleCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 92f);
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("Beacons " + _beaconCount, UtilityWindowTheme.Green, 92f);
                UtilityWindowTheme.CountPill("Collision " + _collisionSensorCount, UtilityWindowTheme.Amber, 104f);
                UtilityWindowTheme.CountPill("Triggers " + _triggerSensorCount, UtilityWindowTheme.Amber, 98f);
                UtilityWindowTheme.CountPill("Traj " + _trajectoryCount, UtilityWindowTheme.Cyan, 78f);
                UtilityWindowTheme.CountPill("Spatial " + _spatialCount, UtilityWindowTheme.Teal, 92f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(52f));
                _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Type", GUILayout.Width(52f));
                _providerTypeFilter = (ProviderTypeFilter)EditorGUILayout.EnumPopup(_providerTypeFilter, GUILayout.MaxWidth(164f));
                EditorGUILayout.LabelField(new GUIContent("Preset", "Filter source rows by preset link state."), GUILayout.Width(48f));
                _presetStateFilter = (PresetStateFilter)EditorGUILayout.EnumPopup(_presetStateFilter, GUILayout.MaxWidth(128f));
                _invalidOnly = GUILayout.Toggle(_invalidOnly, "Invalid", EditorStyles.toolbarButton, GUILayout.Width(68f));
                _selectedSceneObjectOnly = GUILayout.Toggle(_selectedSceneObjectOnly, "Selected Object", EditorStyles.toolbarButton, GUILayout.Width(112f));
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _showActive = GUILayout.Toggle(_showActive, "Active", EditorStyles.toolbarButton, GUILayout.Width(62f));
                _showInactive = GUILayout.Toggle(_showInactive, "Inactive", EditorStyles.toolbarButton, GUILayout.Width(76f));
                _showVisible = GUILayout.Toggle(_showVisible, "Visible", EditorStyles.toolbarButton, GUILayout.Width(68f));
                _showHidden = GUILayout.Toggle(_showHidden, "Hidden", EditorStyles.toolbarButton, GUILayout.Width(66f));
                EditorGUILayout.LabelField("Category", GUILayout.Width(62f));
                _presetCategory = DrawCategoryPopup(_presetCategory);
            }
        }

        private string DrawCategoryPopup(string current)
        {
            List<string> categories = new List<string> { "All" };
            categories.AddRange(PungentSceneGizmoPresetLibrary.Categories);
            for (int i = 0; i < _records.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(_records[i].presetCategories))
                    continue;

                string[] parts = _records[i].presetCategories.Split(',');
                for (int p = 0; p < parts.Length; p++)
                {
                    string category = parts[p].Trim();
                    if (!string.IsNullOrWhiteSpace(category) && !categories.Contains(category, StringComparer.OrdinalIgnoreCase))
                        categories.Add(category);
                }
            }

            int index = Mathf.Max(0, categories.FindIndex(c => string.Equals(c, current, StringComparison.OrdinalIgnoreCase)));
            index = EditorGUILayout.Popup(index, categories.ToArray());
            return categories[Mathf.Clamp(index, 0, categories.Count - 1)];
        }

        private void DrawRecordList(List<BrowserRecord> records, string title, bool showHighCostOnly)
        {
            int count = records != null ? records.Count : 0;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle(title, UtilityWindowTheme.Teal, $"{count}/{_records.Count} shown");
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                if (count == 0)
                {
                    EditorGUILayout.HelpBox(showHighCostOnly ? "No visible high-cost rows match the current filters." : "No scene gizmos match the current filters.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < records.Count; i++)
                        DrawRecord(records[i]);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRecord(BrowserRecord record)
        {
            if (record == null || record.component == null)
                return;

            bool rowSelected = _selectedRecordIds.Contains(record.instanceId);
            bool expanded = rowSelected || _expandedRecordIds.Contains(record.instanceId);
            Color tint = record.visible ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral;
            if (record.invalidRuleCount > 0)
                tint = UtilityWindowTheme.Amber;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool nextSelected = EditorGUILayout.Toggle(rowSelected, GUILayout.Width(18f));
                    if (nextSelected != rowSelected)
                    {
                        if (nextSelected)
                            _selectedRecordIds.Add(record.instanceId);
                        else
                            _selectedRecordIds.Remove(record.instanceId);
                        rowSelected = nextSelected;
                        expanded = rowSelected || _expandedRecordIds.Contains(record.instanceId);
                    }

                    DrawTypeChip(record, 92f);
                    EditorGUILayout.ObjectField(record.component, typeof(Component), true);
                    DrawStateChip(record);

                    bool nextExpanded = GUILayout.Toggle(expanded, "Details", EditorStyles.miniButton, GUILayout.Width(58f));
                    if (nextExpanded != expanded)
                    {
                        if (nextExpanded)
                            _expandedRecordIds.Add(record.instanceId);
                        else
                            _expandedRecordIds.Remove(record.instanceId);
                        StoreExpandedRows();
                    }

                    if (GUILayout.Button(record.visible ? "Hide" : "Show", GUILayout.Width(52f)))
                        SetDrawState(new[] { record }, !record.visible);
                    if (GUILayout.Button("Select", GUILayout.Width(56f)))
                        SelectRecords(new[] { record }, ping: false);
                }

                if (expanded)
                    DrawRecordDetails(record);
            }
        }

        private void DrawTypeChip(BrowserRecord record, float width)
        {
            Color color = UtilityWindowTheme.Cyan;
            if (record.kind == RecordKind.Beacon)
                color = UtilityWindowTheme.Green;
            else if (record.kind == RecordKind.CollisionSensor || record.kind == RecordKind.TriggerSensor)
                color = UtilityWindowTheme.Amber;
            else if (record.kind == RecordKind.Trajectory)
                color = UtilityWindowTheme.Purple;
            else if (record.kind == RecordKind.Spatial)
                color = UtilityWindowTheme.Teal;

            UtilityWindowTheme.CountPill(GetKindLabel(record.kind), color, width);
        }

        private static string GetKindLabel(RecordKind kind)
        {
            switch (kind)
            {
                case RecordKind.CollisionSensor:
                    return "Collision";
                case RecordKind.TriggerSensor:
                    return "Trigger";
                case RecordKind.Trajectory:
                    return "Trajectory";
                default:
                    return kind.ToString();
            }
        }

        private void DrawStateChip(BrowserRecord record)
        {
            string label = record.visible ? "Shown" : "Hidden";
            Color color = record.visible ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral;
            UtilityWindowTheme.CountPill(label, color, 72f);
            if (record.kind == RecordKind.Source)
                DrawPresetStateChip(record.presetStatus, 76f);
            if (record.invalidRuleCount > 0)
                UtilityWindowTheme.CountPill("Invalid", UtilityWindowTheme.Red, 72f);
            else if (record.expensiveWarningCount > 0 || IsHighCostRecord(record))
                UtilityWindowTheme.CountPill("High Cost", UtilityWindowTheme.Amber, 86f);
        }

        private void DrawPresetStateChip(PungentSceneGizmoPresetDiffStatus status, float width)
        {
            string label = string.IsNullOrWhiteSpace(status.stateLabel) ? status.state.ToString() : status.stateLabel;
            UtilityWindowTheme.CountPill(label, PresetStateTint(status.state), width);
        }

        private static Color PresetStateTint(PungentSceneGizmoPresetInstanceState state)
        {
            switch (state)
            {
                case PungentSceneGizmoPresetInstanceState.Base:
                    return UtilityWindowTheme.Green;
                case PungentSceneGizmoPresetInstanceState.Customized:
                case PungentSceneGizmoPresetInstanceState.Mixed:
                    return UtilityWindowTheme.Amber;
                case PungentSceneGizmoPresetInstanceState.Outdated:
                case PungentSceneGizmoPresetInstanceState.MissingPreset:
                    return UtilityWindowTheme.Red;
                case PungentSceneGizmoPresetInstanceState.Locked:
                    return UtilityWindowTheme.Purple;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private void DrawRecordDetails(BrowserRecord record)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.07f, 0.025f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Draw Ops " + record.estimatedDrawOperations, record.estimatedDrawOperations >= PungentSceneGizmoPerformancePolicy.HighCostRecordDrawOps ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 112f);
                    UtilityWindowTheme.CountPill("Labels " + record.estimatedLabels, record.estimatedLabels > 0 ? UtilityWindowTheme.Purple : UtilityWindowTheme.Neutral, 90f);
                    if (record.estimatedTrajectorySamples > 0)
                        UtilityWindowTheme.CountPill("Samples " + record.estimatedTrajectorySamples, UtilityWindowTheme.Cyan, 104f);
                    if (record.kind == RecordKind.Source)
                    {
                        UtilityWindowTheme.CountPill($"Rules {record.enabledRules}/{record.totalRules}", UtilityWindowTheme.Cyan, 98f);
                        DrawPresetStateChip(record.presetStatus, 86f);
                    }
                    UtilityWindowTheme.CountPill(record.selectedOnly ? "Selected Only" : "Always/Mode", record.selectedOnly ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 112f);
                    GUILayout.FlexibleSpace();
                }

                if (!string.IsNullOrWhiteSpace(record.warning))
                    EditorGUILayout.HelpBox(record.warning, MessageType.Warning);
                else if (record.kind == RecordKind.Source && !string.IsNullOrWhiteSpace(record.presetStatus.message))
                    EditorGUILayout.HelpBox(record.presetStatus.message, record.presetStatus.state == PungentSceneGizmoPresetInstanceState.Base || record.presetStatus.state == PungentSceneGizmoPresetInstanceState.Unlinked ? MessageType.Info : MessageType.Warning);
                else if (!string.IsNullOrWhiteSpace(record.presetCategories))
                    EditorGUILayout.LabelField(record.presetCategories, UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(54f)))
                        PingRecords(new[] { record });
                    DrawActionButton("Disable Labels", record.estimatedLabels > 0, "This row has no labels to disable.", () =>
                    {
                        if (PungentSceneGizmoPresetActions.DisableLabels(new[] { record.component }, out PungentSceneGizmoComponentActionSummary summary))
                        {
                            _status = "Disabled labels. " + summary;
                            MarkCacheDirty();
                        }
                    }, 104f);
                    DrawActionButton("Disable Trajectory", record.component is PungentTrajectoryVisualizer, "This row is not a trajectory visualizer.", () =>
                    {
                        if (PungentSceneGizmoPresetActions.DisableTrajectories(VisibleTrajectories(new[] { record }), out PungentSceneGizmoComponentActionSummary summary))
                        {
                            _status = "Disabled trajectory. " + summary;
                            MarkCacheDirty();
                        }
                    }, 122f);
                    DrawActionButton("Design", record.kind == RecordKind.Source, "Only source rows can be edited in the Design workspace.", () =>
                    {
                        _selectedRecordIds.Add(record.instanceId);
                        _viewTab = BrowserViewTab.Design;
                    }, 70f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawPresetActions()
        {
            bool hasSelection = Selection.gameObjects != null && Selection.gameObjects.Length > 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                _replacePresetRules = GUILayout.Toggle(_replacePresetRules, new GUIContent("Replace rules on apply", "When enabled, preset application clears existing source rules before adding preset rules."), EditorStyles.toolbarButton, GUILayout.Width(156f));
                DrawActionButton("Apply Built-In Preset...", hasSelection, "Select scene GameObject(s) before applying presets.", ShowApplyPresetMenu, 168f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _assetPreset = (PungentSceneGizmoPreset)EditorGUILayout.ObjectField("Preset Asset", _assetPreset, typeof(PungentSceneGizmoPreset), false);
                DrawActionButton("Apply Asset", _assetPreset != null && hasSelection, _assetPreset == null ? "Assign a preset asset first." : "Select scene GameObject(s) before applying this preset asset.", () =>
                {
                    if (PungentSceneGizmoPresetActions.ApplyPresetToSelection(_assetPreset, _replacePresetRules, out PungentSceneGizmoPresetApplySummary summary))
                    {
                        _status = $"Applied {_assetPreset.displayName}. {summary}";
                        MarkCacheDirty();
                    }
                    else
                    {
                        _status = "Preset asset apply made no changes.";
                    }
                }, 96f);
            }
        }

        private void DrawActionButton(string label, bool enabled, string disabledReason, Action action, float width)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (GUILayout.Button(new GUIContent(label, enabled ? string.Empty : disabledReason), GUILayout.Width(width)))
                    action?.Invoke();
            }
        }

        private void ShowAddToSelectionMenu()
        {
            GenericMenu menu = new GenericMenu();
            bool hasSelection = Selection.gameObjects != null && Selection.gameObjects.Length > 0;
            if (!hasSelection)
            {
                menu.AddDisabledItem(new GUIContent("Select scene GameObject(s) first"));
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("Scene Gizmo Source"), false, AddSourceToSelection);
            menu.AddItem(new GUIContent("Scene Beacon"), false, () => RunComponentSetup(PungentSceneGizmoPresetActions.AddBeaconToSelection, "Beacon"));
            menu.AddItem(new GUIContent("Collision Sensor"), false, () => RunComponentSetup(PungentSceneGizmoPresetActions.AddCollisionSensorToSelection, "Collision sensor"));
            menu.AddItem(new GUIContent("Trigger Sensor"), false, () => RunComponentSetup(PungentSceneGizmoPresetActions.AddTriggerSensorToSelection, "Trigger sensor"));
            menu.AddItem(new GUIContent("Trajectory Visualizer"), false, () => RunComponentSetup(PungentSceneGizmoPresetActions.AddTrajectoryVisualizerToSelection, "Trajectory visualizer"));
            menu.ShowAsContext();
        }

        private void ShowApplyPresetMenu()
        {
            GenericMenu menu = new GenericMenu();
            bool hasSelection = Selection.gameObjects != null && Selection.gameObjects.Length > 0;
            if (!hasSelection)
            {
                menu.AddDisabledItem(new GUIContent("Select scene GameObject(s) first"));
            }
            else
            {
                PungentSceneGizmoPresetLibrary.AddBuiltInPresetMenu(menu, preset =>
                {
                    if (PungentSceneGizmoPresetActions.ApplyBuiltInPresetToSelection(preset.Id, _replacePresetRules, out PungentSceneGizmoPresetApplySummary summary))
                    {
                        _status = $"Applied {preset.DisplayName}. {summary}";
                        MarkCacheDirty();
                    }
                    else
                    {
                        _status = $"Preset '{preset.DisplayName}' made no changes.";
                    }
                });
            }

            menu.ShowAsContext();
        }

        private delegate bool ComponentSetupAction(out PungentSceneGizmoComponentActionSummary summary);

        private void RunComponentSetup(ComponentSetupAction action, string label)
        {
            if (action != null && action(out PungentSceneGizmoComponentActionSummary summary))
            {
                _status = $"Added {label}. {summary}";
                MarkCacheDirty();
            }
            else
            {
                _status = $"{label} setup made no changes.";
            }
        }

        private void AddSourceToSelection()
        {
            if (!PungentSceneGizmoCommandService.AddSourceToSelection(out PungentSceneGizmoComponentActionSummary summary))
            {
                _status = Selection.gameObjects == null || Selection.gameObjects.Length == 0
                    ? "Select scene GameObject(s) before adding gizmo sources."
                    : $"Selection already has gizmo sources. Skipped {summary.skippedCount}.";
            }
            else
            {
                _status = $"Added {summary.addedCount} gizmo source{(summary.addedCount == 1 ? string.Empty : "s")}; skipped {summary.skippedCount}.";
            }
            MarkCacheDirty();
        }

        private List<BrowserRecord> GetSelectedRecords(List<BrowserRecord> visibleRecords, bool visibleOnly)
        {
            IEnumerable<BrowserRecord> source = visibleOnly ? (IEnumerable<BrowserRecord>)visibleRecords : _records;
            if (source == null)
                return new List<BrowserRecord>();

            return source.Where(r => r != null && _selectedRecordIds.Contains(r.instanceId)).ToList();
        }

        private void MarkCacheDirty()
        {
            _cacheDirty = true;
            PungentSceneGizmoProviderCache.MarkDirty();
            PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(this, ref _nextAllowedWindowRepaintTime, 0.10d);
        }

        private void RequestWindowRepaint()
        {
            PungentEditorPerformanceUtility.RequestWindowRepaintThrottled(this, ref _nextAllowedWindowRepaintTime, 0.10d);
        }

        private void RebuildCache()
        {
            _records.Clear();
            ResetCounts();

            PungentSceneGizmoSource[] sources = Resources.FindObjectsOfTypeAll<PungentSceneGizmoSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                PungentSceneGizmoSource source = sources[i];
                if (source == null || source.gameObject == null || EditorUtility.IsPersistent(source.gameObject))
                    continue;

                BrowserRecord record = BuildSourceRecord(source);
                _records.Add(record);
                Accumulate(record);
            }

            PungentSceneGizmoProviderCache.ForceRebuild();
            IReadOnlyList<PungentSceneGizmoProviderCache.ProviderRecord> providers = PungentSceneGizmoProviderCache.GetProviders();
            for (int i = 0; i < providers.Count; i++)
            {
                BrowserRecord record = BuildProviderRecord(providers[i]);
                if (record == null)
                    continue;

                _records.Add(record);
                Accumulate(record);
            }

            _records.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            _cacheDirty = false;
            _selectedRecordIds.RemoveWhere(id => _records.All(r => r.instanceId != id));
            _expandedRecordIds.RemoveWhere(id => _records.All(r => r.instanceId != id));
            _status = $"Cache refreshed: {_sourceCount} source{(_sourceCount == 1 ? string.Empty : "s")}, {_providerCount} provider{(_providerCount == 1 ? string.Empty : "s")}.";
        }

        private void ResetCounts()
        {
            _sourceCount = 0;
            _enabledRuleCount = 0;
            _providerCount = 0;
            _beaconCount = 0;
            _collisionSensorCount = 0;
            _triggerSensorCount = 0;
            _trajectoryCount = 0;
            _spatialCount = 0;
            _invalidRuleCount = 0;
            _expensiveWarningCount = 0;
            _estimatedDrawOperations = 0;
            _estimatedLabels = 0;
            _estimatedTrajectorySamples = 0;
            _activeVisibleCount = 0;
        }

        private static BrowserRecord BuildSourceRecord(PungentSceneGizmoSource source)
        {
            BrowserRecord record = new BrowserRecord
            {
                source = source,
                component = source,
                gameObject = source.gameObject,
                kind = RecordKind.Source,
                instanceId = source.GetInstanceID(),
                active = source.isActiveAndEnabled && source.gameObject.activeInHierarchy,
                visible = source.drawInScene,
                totalRules = source.rules != null ? source.rules.Count : 0,
                enabledRules = source.EstimateEnabledRuleCount(),
                estimatedDrawOperations = source.EstimateDrawOperations(),
                estimatedLabels = source.EstimateLabelCount(),
                presetStatus = PungentSceneGizmoPresetDiffUtility.GetStatus(source)
            };

            HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> searchParts = new List<string> { source.name, source.GetType().Name, "Source" };
            if (!string.IsNullOrWhiteSpace(record.presetStatus.linkedPresetId))
                searchParts.Add(record.presetStatus.linkedPresetId);
            if (!string.IsNullOrWhiteSpace(record.presetStatus.stateLabel))
                searchParts.Add(record.presetStatus.stateLabel);
            List<PungentSceneGizmoSource.RuleStatus> statuses = source.GetRuleStatuses();
            for (int i = 0; i < statuses.Count; i++)
            {
                PungentSceneGizmoSource.RuleStatus status = statuses[i];
                if (status == null)
                    continue;

                if (!status.IsValid)
                    record.invalidRuleCount++;
                if (status.hasExpensiveRuleWarning)
                    record.expensiveWarningCount++;
                if (!string.IsNullOrWhiteSpace(status.presetCategory))
                    categories.Add(status.presetCategory);
                if (!string.IsNullOrWhiteSpace(status.presetId))
                    searchParts.Add(status.presetId);
                if (!string.IsNullOrWhiteSpace(status.ruleName))
                    searchParts.Add(status.ruleName);
            }

            record.expensiveWarningCount += PungentSceneGizmoPerformancePolicy.IsHighCost(record.estimatedDrawOperations, record.estimatedTrajectorySamples) ? 1 : 0;
            record.presetCategories = string.Join(", ", categories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray());
            searchParts.Add(record.presetCategories);
            record.searchText = string.Join(" ", searchParts.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
            record.selectedOnly = source.rules == null || source.rules.All(r => r == null || !r.enabled || r.drawWhen == PungentSceneGizmoSource.DrawWhen.Selected);
            record.alwaysVisible = source.rules != null && source.rules.Any(r => r != null && r.enabled && r.drawWhen == PungentSceneGizmoSource.DrawWhen.Always);
            return record;
        }

        private static BrowserRecord BuildProviderRecord(PungentSceneGizmoProviderCache.ProviderRecord provider)
        {
            if (provider == null || provider.component == null)
                return null;

            BrowserRecord record = new BrowserRecord
            {
                component = provider.component,
                gameObject = provider.gameObject,
                instanceId = provider.instanceId,
                active = provider.active,
                visible = provider.visible,
                selectedOnly = provider.selectedOnly,
                alwaysVisible = provider.alwaysVisible,
                invalidRuleCount = provider.invalid ? 1 : 0,
                estimatedDrawOperations = provider.estimatedDrawOperations,
                estimatedLabels = provider.estimatedLabels,
                estimatedTrajectorySamples = provider.estimatedTrajectorySamples,
                presetCategories = provider.providerCategory,
                warning = provider.warning,
                searchText = provider.searchText
            };

            if (provider.isBeacon)
                record.kind = RecordKind.Beacon;
            else if (provider.isCollisionSensor)
                record.kind = RecordKind.CollisionSensor;
            else if (provider.isTriggerSensor)
                record.kind = RecordKind.TriggerSensor;
            else if (provider.isTrajectory)
                record.kind = RecordKind.Trajectory;
            else if (provider.isSpatial)
                record.kind = RecordKind.Spatial;
            else
                record.kind = RecordKind.Provider;

            if (PungentSceneGizmoPerformancePolicy.IsHighCost(record.estimatedDrawOperations, record.estimatedTrajectorySamples))
                record.expensiveWarningCount++;
            return record;
        }

        private void Accumulate(BrowserRecord record)
        {
            if (record == null)
                return;

            if (record.kind == RecordKind.Source)
            {
                _sourceCount++;
                _enabledRuleCount += record.enabledRules;
            }
            else
            {
                _providerCount++;
                if (record.kind == RecordKind.Beacon)
                    _beaconCount++;
                if (record.kind == RecordKind.CollisionSensor)
                    _collisionSensorCount++;
                if (record.kind == RecordKind.TriggerSensor)
                    _triggerSensorCount++;
                if (record.kind == RecordKind.Trajectory)
                    _trajectoryCount++;
                if (record.kind == RecordKind.Spatial)
                    _spatialCount++;
            }

            _invalidRuleCount += record.invalidRuleCount;
            _expensiveWarningCount += record.expensiveWarningCount;
            if (record.active && record.visible)
            {
                _activeVisibleCount++;
                _estimatedDrawOperations += record.estimatedDrawOperations;
                _estimatedLabels += record.estimatedLabels;
                _estimatedTrajectorySamples += record.estimatedTrajectorySamples;
            }
        }

        private List<BrowserRecord> FilterRecords()
        {
            string q = string.IsNullOrWhiteSpace(_search) ? string.Empty : _search.Trim();
            HashSet<GameObject> selectedObjects = _selectedSceneObjectOnly
                ? new HashSet<GameObject>(Selection.gameObjects.Where(go => go != null))
                : null;

            List<BrowserRecord> results = new List<BrowserRecord>();
            for (int i = 0; i < _records.Count; i++)
            {
                BrowserRecord record = _records[i];
                if (record == null || record.component == null || record.gameObject == null)
                    continue;

                if (!PassProviderFilter(record))
                    continue;
                if (!PassPresetStateFilter(record))
                    continue;
                if (record.active && !_showActive)
                    continue;
                if (!record.active && !_showInactive)
                    continue;
                if (record.visible && !_showVisible)
                    continue;
                if (!record.visible && !_showHidden)
                    continue;
                if (_invalidOnly && record.invalidRuleCount <= 0)
                    continue;
                if (selectedObjects != null && !selectedObjects.Contains(record.gameObject))
                    continue;
                if (!string.Equals(_presetCategory, "All", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(record.presetCategories) || record.presetCategories.IndexOf(_presetCategory, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                if (!string.IsNullOrEmpty(q) && (record.searchText == null || record.searchText.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;

                results.Add(record);
            }

            return results;
        }

        private bool PassProviderFilter(BrowserRecord record)
        {
            switch (_providerTypeFilter)
            {
                case ProviderTypeFilter.Sources:
                    return record.kind == RecordKind.Source;
                case ProviderTypeFilter.Beacons:
                    return record.kind == RecordKind.Beacon;
                case ProviderTypeFilter.CollisionSensors:
                    return record.kind == RecordKind.CollisionSensor;
                case ProviderTypeFilter.TriggerSensors:
                    return record.kind == RecordKind.TriggerSensor;
                case ProviderTypeFilter.Trajectories:
                    return record.kind == RecordKind.Trajectory;
                case ProviderTypeFilter.Spatial:
                    return record.kind == RecordKind.Spatial;
                default:
                    return true;
            }
        }

        private bool PassPresetStateFilter(BrowserRecord record)
        {
            if (_presetStateFilter == PresetStateFilter.All)
                return true;

            if (record == null || record.kind != RecordKind.Source)
                return false;

            PungentSceneGizmoPresetDiffStatus status = record.presetStatus;
            switch (_presetStateFilter)
            {
                case PresetStateFilter.Linked:
                    return status.hasLinkedPreset;
                case PresetStateFilter.Base:
                    return status.state == PungentSceneGizmoPresetInstanceState.Base;
                case PresetStateFilter.Customized:
                    return status.state == PungentSceneGizmoPresetInstanceState.Customized;
                case PresetStateFilter.Outdated:
                    return status.state == PungentSceneGizmoPresetInstanceState.Outdated;
                case PresetStateFilter.Locked:
                    return status.state == PungentSceneGizmoPresetInstanceState.Locked;
                case PresetStateFilter.Mixed:
                    return status.state == PungentSceneGizmoPresetInstanceState.Mixed;
                case PresetStateFilter.Unlinked:
                    return status.state == PungentSceneGizmoPresetInstanceState.Unlinked;
                default:
                    return true;
            }
        }

        private void SetDrawState(IEnumerable<BrowserRecord> records, bool drawInScene)
        {
            List<Component> components = records != null
                ? records.Where(r => r != null && r.component != null).Select(r => r.component).ToList()
                : new List<Component>();

            PungentSceneGizmoCommandService.SetDrawState(components, drawInScene, out PungentSceneGizmoComponentActionSummary summary);
            _status = $"{(drawInScene ? "Showed" : "Hid")} {summary.changedCount} gizmo row{(summary.changedCount == 1 ? string.Empty : "s")}; skipped {summary.skippedCount}.";
            MarkCacheDirty();
            RepaintActiveSceneView();
        }

        private void SelectRecords(IEnumerable<BrowserRecord> records, bool ping)
        {
            List<UnityEngine.Object> objects = records
                .Where(r => r != null && r.gameObject != null)
                .Select(r => (UnityEngine.Object)r.gameObject)
                .Distinct()
                .ToList();

            if (objects.Count == 0)
            {
                _status = "No gizmo rows in that scope.";
                return;
            }

            Selection.objects = objects.ToArray();
            if (ping)
                EditorGUIUtility.PingObject(objects[0]);

            _status = $"{(ping ? "Pinged" : "Selected")} {objects.Count} gizmo object{(objects.Count == 1 ? string.Empty : "s")}.";
        }

        private void PingRecords(IEnumerable<BrowserRecord> records)
        {
            List<PungentSceneBeacon> beacons = VisibleBeacons(records).ToList();
            if (beacons.Count > 0 && PungentSceneGizmoPresetActions.PingSelectedBeacons(beacons, out PungentSceneGizmoComponentActionSummary summary))
            {
                _status = "Pinged beacons. " + summary;
                return;
            }

            SelectRecords(records, ping: true);
        }

        private IEnumerable<PungentTrajectoryVisualizer> VisibleTrajectories(IEnumerable<BrowserRecord> records)
        {
            if (records == null)
                yield break;

            foreach (BrowserRecord record in records)
            {
                if (record != null && record.component is PungentTrajectoryVisualizer trajectory)
                    yield return trajectory;
            }
        }

        private IEnumerable<PungentSceneBeacon> VisibleBeacons(IEnumerable<BrowserRecord> records)
        {
            if (records == null)
                yield break;

            foreach (BrowserRecord record in records)
            {
                if (record != null && record.component is PungentSceneBeacon beacon)
                    yield return beacon;
            }
        }

        private IEnumerable<Component> VisibleComponents(IEnumerable<BrowserRecord> records)
        {
            if (records == null)
                yield break;

            foreach (BrowserRecord record in records)
            {
                if (record != null && record.component != null)
                    yield return record.component;
            }
        }

        private static bool IsHighCostRecord(BrowserRecord record)
        {
            return record != null &&
                   (record.expensiveWarningCount > 0 ||
                    PungentSceneGizmoPerformancePolicy.IsHighCost(record.estimatedDrawOperations, record.estimatedTrajectorySamples) ||
                    (record.alwaysVisible && !record.selectedOnly));
        }

        private void RestoreExpandedRows()
        {
            _expandedRecordIds.Clear();
            string value = UtilityWindowPrefs.GetString(PrefExpandedRows, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                return;

            string[] parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int id))
                    _expandedRecordIds.Add(id);
            }
        }

        private void StoreExpandedRows()
        {
            if (_expandedRecordIds.Count == 0)
            {
                UtilityWindowPrefs.SetString(PrefExpandedRows, string.Empty);
                return;
            }

            UtilityWindowPrefs.SetString(PrefExpandedRows, string.Join(",", _expandedRecordIds.Select(id => id.ToString()).ToArray()));
        }

        private void RepaintActiveSceneView()
        {
            if (SceneView.lastActiveSceneView != null)
                PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref _nextAllowedSceneRepaintTime, 0.10d);
        }
    }
#endif
}
