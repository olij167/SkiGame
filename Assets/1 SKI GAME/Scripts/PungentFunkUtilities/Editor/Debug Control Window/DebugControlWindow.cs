using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Debugging;

namespace PungentFunk.Utilities.Editor.Debugging
{
#if UNITY_EDITOR
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Generic editor utility for discovering and controlling scene debug toggles, gizmo toggles,
    /// ContextMenu debug actions, persistent scheduled debug actions, and plain-text component snapshots.
    ///
    /// Suggested path: Assets/Scripts/Editor/DebugControlWindow.cs
    /// Open via: Tools/PungentFunk/Debug/Debug Control Center
    /// </summary>
    public sealed partial class DebugControlWindow : EditorWindow
    {

        private readonly List<DebugComponentInfo> _components = new List<DebugComponentInfo>();
        private readonly List<StaticDebugFieldInfo> _staticBoolToggles = new List<StaticDebugFieldInfo>();
        private readonly List<ScheduledCall> _scheduledCalls = new List<ScheduledCall>();
        private readonly Dictionary<string, bool> _typeFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<int, bool> _instanceFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<string, bool> _sceneObjectFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _groupBoolFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<int, bool> _boolSectionFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _actionSectionFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _snapshotSectionFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _targetSectionFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<int, ComponentCapabilityFilterState> _componentCapabilityFilters = new Dictionary<int, ComponentCapabilityFilterState>();
        private readonly Dictionary<string, double> _hoverLabelStartTimes = new Dictionary<string, double>();

        private Vector2 _scroll;
        private Vector2 _scheduleScroll;
        private Vector2 _routerScroll;
        private Vector2 _staticScroll;
        private Vector2 _componentsScroll;
        private Vector2 _supportScroll;
        private Vector2 _routerPanelScroll;
        private Vector2 _schedulePanelScroll;
        private Vector2 _staticPanelScroll;
        private Vector2 _bulkPanelScroll;
        private string _search = string.Empty;
        private bool _includeInactive = true;
        private bool _selectedHierarchyOnly;
        private bool _showOnlyDebuggable = true;
        private bool _showStaticDebugFields = true;
        private bool _showContextActions = true;
        private bool _showBoolToggles = true;
        private bool _showSnapshotButtons = true;
        private GroupMode _groupMode = GroupMode.ComponentType;
        private bool _helpFoldout = false;
        private bool _bulkControlsOpen;
        private bool _routerAdvancedOpen;
        private bool _schedulerCleanupOpen;
        private bool _drawingSupportColumn;
        private bool _stretchWorkbench;
        private bool _initialOpenRefreshQueued;
        private bool _masterControlsAffectFiltered;
        private bool _routerPanelOpen = true;
        private bool _routerChannelsOpen = true;
        private bool _routerSignalsOpen = true;
        private bool _routerStatesOpen = false;
        private bool _routerSourcesOpen = false;
        private bool _scheduledPanelOpen = true;
        private bool _staticFieldsPanelOpen = true;
        private bool _discoveredComponentsPanelOpen = true;
        private bool _autoRefreshSceneScan;
        private bool _scanCacheDirty = true;
        private float _supportColumnWidth = 330f;
        private float _primaryWorkbenchWidth;
        private float _workbenchHeight;
        private float _routerPanelHeight = 260f;
        private float _schedulePanelHeight = 230f;
        private float _staticPanelHeight = 190f;
        private float _bulkPanelHeight = 180f;
        private double _lastRefreshTime;
        private double _nextScheduleTickTime;
        private double _nextRouterRefreshTime;
        private double _nextAutoSceneRefreshTime;
        private double _nextAllowedRepaintTime;
        private string _status = "Press Refresh to scan the open scene.";

        private List<DebugRouter.ChannelSnapshot> _routerChannels = new List<DebugRouter.ChannelSnapshot>();
        private List<DebugRouter.SourceSnapshot> _routerSources = new List<DebugRouter.SourceSnapshot>();
        private List<DebugRouter.StateSnapshot> _routerStates = new List<DebugRouter.StateSnapshot>();
        private List<DebugRouter.SignalSnapshot> _routerSignals = new List<DebugRouter.SignalSnapshot>();
        private bool _routerScriptAuditPerformed;
        private int _routerAuditLogCalls;
        private int _routerAuditSignalCalls;
        private int _routerAuditStateCalls;
        private int _routerAuditRegisterCalls;
        private readonly List<string> _routerAuditFiles = new List<string>();

        private static GUIStyle _titleStyle;
        private static GUIStyle _subTitleStyle;
        private static GUIStyle _toolbarPanelStyle;
        private static GUIStyle _masterPanelStyle;
        private static GUIStyle _routerPanelStyle;
        private static GUIStyle _schedulePanelStyle;
        private static GUIStyle _staticPanelStyle;
        private static GUIStyle _componentPanelStyle;
        private static GUIStyle _instancePanelStyle;
        private static GUIStyle _fieldPanelStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _statusPillStyle;
        private static GUIStyle _countPillStyle;
        private static GUIStyle _mutedMiniLabelStyle;
        private static GUIStyle _categoryLabelStyle;
        private static GUIStyle _pathLabelStyle;
        private static readonly Dictionary<string, GUIStyle> _tintedBoxStyleCache = new Dictionary<string, GUIStyle>();

        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string SchedulePrefsKey = "GenericDebugControlWindow.ScheduledCalls.v2.PreRouter";
        private const string SearchPrefsKey = "GenericDebugControlWindow.Search";
        private const string IncludeInactivePrefsKey = "GenericDebugControlWindow.IncludeInactive";
        private const string SelectedHierarchyOnlyPrefsKey = "GenericDebugControlWindow.SelectedHierarchyOnly";
        private const string ShowOnlyDebuggablePrefsKey = "GenericDebugControlWindow.ShowOnlyDebuggable";
        private const string ShowStaticDebugFieldsPrefsKey = "GenericDebugControlWindow.ShowStaticDebugFields";
        private const string ShowContextActionsPrefsKey = "GenericDebugControlWindow.ShowContextActions";
        private const string ShowBoolTogglesPrefsKey = "GenericDebugControlWindow.ShowBoolToggles";
        private const string ShowSnapshotButtonsPrefsKey = "GenericDebugControlWindow.ShowSnapshotButtons";
        private const string GroupModePrefsKey = "GenericDebugControlWindow.GroupMode";
        private const string MasterFilteredPrefsKey = "GenericDebugControlWindow.MasterControlsAffectFiltered";
        private const string HelpFoldoutPrefsKey = "GenericDebugControlWindow.HelpFoldout";
        private const string BulkControlsOpenPrefsKey = "GenericDebugControlWindow.BulkControlsOpen";
        private const string RouterAdvancedOpenPrefsKey = "GenericDebugControlWindow.RouterAdvancedOpen";
        private const string SchedulerCleanupOpenPrefsKey = "GenericDebugControlWindow.SchedulerCleanupOpen";
        private const string BrowseFirstDefaultsAppliedPrefsKey = "GenericDebugControlWindow.BrowseFirstDefaultsApplied.v1";
        private const string SupportColumnWidthPrefsKey = "GenericDebugControlWindow.SupportColumnWidth";
        private const string RouterPanelHeightPrefsKey = "GenericDebugControlWindow.RouterPanelHeight";
        private const string SchedulePanelHeightPrefsKey = "GenericDebugControlWindow.SchedulePanelHeight";
        private const string StaticPanelHeightPrefsKey = "GenericDebugControlWindow.StaticPanelHeight";
        private const string BulkPanelHeightPrefsKey = "GenericDebugControlWindow.BulkPanelHeight";
        private const string RouterPanelOpenPrefsKey = "GenericDebugControlWindow.RouterPanelOpen";
        private const string RouterChannelsOpenPrefsKey = "GenericDebugControlWindow.RouterChannelsOpen";
        private const string RouterSignalsOpenPrefsKey = "GenericDebugControlWindow.RouterSignalsOpen";
        private const string RouterStatesOpenPrefsKey = "GenericDebugControlWindow.RouterStatesOpen";
        private const string RouterSourcesOpenPrefsKey = "GenericDebugControlWindow.RouterSourcesOpen";
        private const string ScheduledPanelOpenPrefsKey = "GenericDebugControlWindow.ScheduledPanelOpen";
        private const string StaticFieldsPanelOpenPrefsKey = "GenericDebugControlWindow.StaticFieldsPanelOpen";
        private const string DiscoveredComponentsPanelOpenPrefsKey = "GenericDebugControlWindow.DiscoveredComponentsPanelOpen";
        private const string TypeFoldoutPrefsPrefix = "GenericDebugControlWindow.TypeFoldout.";
        private const string SceneObjectFoldoutPrefsPrefix = "GenericDebugControlWindow.SceneObjectFoldout.";
        private const string GroupBoolFoldoutPrefsPrefix = "GenericDebugControlWindow.GroupBoolFoldout.";
        private const float TwoColumnWorkbenchBreakpoint = 1000f;
        private const float MinPrimaryWorkbenchWidth = 620f;
        private const float MinSupportColumnWidth = 340f;
        private const float PreferredSupportColumnWidth = 380f;
        private const float MaxSupportColumnWidthRatio = 0.40f;
        private const float WorkbenchResizeHandleWidth = 12f;
        private const float ComponentInternalScrollThreshold = 36f;
        private const float MinSupportPanelHeight = 96f;
        private const float MaxSupportPanelHeight = 560f;
        private const double ScheduleTickInterval = 0.20;
        private const double RouterRefreshInterval = 0.50;
        private const double RepaintThrottleInterval = 0.20;
        private const double AutoSceneRefreshInterval = 2.00;

        public static void Open()
        {
            GetWindow<DebugControlWindow>("Debug Control Center");
        }

        //// Legacy compatibility alias. Keep until the final public menu root is locked for release.
        //public static void OpenLegacy()
        //{
        //    Open();
        //}

        private void OnEnable()
        {
            LoadWindowPrefs();
            ApplyBrowseFirstDefaultsIfNeeded();
            _routerPanelOpen = UtilityWindowPrefs.GetBool(RouterPanelOpenPrefsKey, true);
            _routerChannelsOpen = UtilityWindowPrefs.GetBool(RouterChannelsOpenPrefsKey, true);
            _routerSignalsOpen = UtilityWindowPrefs.GetBool(RouterSignalsOpenPrefsKey, true);
            _routerStatesOpen = UtilityWindowPrefs.GetBool(RouterStatesOpenPrefsKey, false);
            _routerSourcesOpen = UtilityWindowPrefs.GetBool(RouterSourcesOpenPrefsKey, false);
            _scheduledPanelOpen = UtilityWindowPrefs.GetBool(ScheduledPanelOpenPrefsKey, true);
            _staticFieldsPanelOpen = UtilityWindowPrefs.GetBool(StaticFieldsPanelOpenPrefsKey, true);
            _discoveredComponentsPanelOpen = UtilityWindowPrefs.GetBool(DiscoveredComponentsPanelOpenPrefsKey, true);
            _supportColumnWidth = UtilityWindowPrefs.GetFloat(SupportColumnWidthPrefsKey, PreferredSupportColumnWidth);
            _routerPanelHeight = UtilityWindowPrefs.GetFloat(RouterPanelHeightPrefsKey, _routerPanelHeight);
            _schedulePanelHeight = UtilityWindowPrefs.GetFloat(SchedulePanelHeightPrefsKey, _schedulePanelHeight);
            _staticPanelHeight = UtilityWindowPrefs.GetFloat(StaticPanelHeightPrefsKey, _staticPanelHeight);
            _bulkPanelHeight = UtilityWindowPrefs.GetFloat(BulkPanelHeightPrefsKey, _bulkPanelHeight);
            ClampSupportPanelHeights();
            LoadScheduledCalls();
            ResolveScheduledReferences();
            EditorApplication.update += OnEditorUpdate;
            _scanCacheDirty = true;

            RefreshRouterSnapshots();
            QueueInitialRefreshIfNeeded();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.delayCall -= RefreshOnOpen;
            SaveWindowPrefs();
            UtilityWindowPrefs.SetBool(RouterPanelOpenPrefsKey, _routerPanelOpen);
            UtilityWindowPrefs.SetBool(RouterChannelsOpenPrefsKey, _routerChannelsOpen);
            UtilityWindowPrefs.SetBool(RouterSignalsOpenPrefsKey, _routerSignalsOpen);
            UtilityWindowPrefs.SetBool(RouterStatesOpenPrefsKey, _routerStatesOpen);
            UtilityWindowPrefs.SetBool(RouterSourcesOpenPrefsKey, _routerSourcesOpen);
            UtilityWindowPrefs.SetBool(ScheduledPanelOpenPrefsKey, _scheduledPanelOpen);
            UtilityWindowPrefs.SetBool(StaticFieldsPanelOpenPrefsKey, _staticFieldsPanelOpen);
            UtilityWindowPrefs.SetBool(DiscoveredComponentsPanelOpenPrefsKey, _discoveredComponentsPanelOpen);
            UtilityWindowPrefs.SetFloat(SupportColumnWidthPrefsKey, _supportColumnWidth);
            UtilityWindowPrefs.SetFloat(RouterPanelHeightPrefsKey, _routerPanelHeight);
            UtilityWindowPrefs.SetFloat(SchedulePanelHeightPrefsKey, _schedulePanelHeight);
            UtilityWindowPrefs.SetFloat(StaticPanelHeightPrefsKey, _staticPanelHeight);
            UtilityWindowPrefs.SetFloat(BulkPanelHeightPrefsKey, _bulkPanelHeight);
            SaveScheduledCalls();
        }

        private void OnHierarchyChange()
        {
            _scanCacheDirty = true;
            _status = "Hierarchy changed. Press Refresh, or enable Auto Refresh.";
            RequestRepaintThrottled();
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;

            if (_autoRefreshSceneScan && _scanCacheDirty && now >= _nextAutoSceneRefreshTime)
            {
                _nextAutoSceneRefreshTime = now + AutoSceneRefreshInterval;
                Refresh();
            }

            if (ShouldRefreshRouterForVisibleContent() && now >= _nextRouterRefreshTime)
            {
                _nextRouterRefreshTime = now + RouterRefreshInterval;
                if (RefreshRouterSnapshots())
                    RequestRepaintThrottled();
            }

            if (now < _nextScheduleTickTime)
                return;

            _nextScheduleTickTime = now + ScheduleTickInterval;
            TickScheduledActions(now);
        }

        private void OnGUI()
        {
            PungentEditorPerformanceUtility.RecordWindowRepaint(this);
            EnsureStyles();

            DrawCompactHeaderStrip();
            DrawSearchAndRefineStrip();
            DrawCompactStatusStrip();
            DrawContextHelpStrip();

            if (ShouldUseTwoColumnWorkbench())
            {
                DrawWorkbenchLayout();
            }
            else
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                try
                {
                    DrawStackedWorkbenchLayout();
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }

            DrawCollapsedHelpFooter();
        }

        private static void EnsureStyles()
        {
            UtilityWindowTheme.EnsureStyles();

            _titleStyle = UtilityWindowTheme.TitleStyle;
            _subTitleStyle = UtilityWindowTheme.SubtitleStyle;
            _sectionHeaderStyle = UtilityWindowTheme.SectionHeaderStyle;
            _countPillStyle = UtilityWindowTheme.CountPillStyle;
            _mutedMiniLabelStyle = UtilityWindowTheme.MutedMiniLabelStyle;
            _pathLabelStyle = UtilityWindowTheme.PathLabelStyle;

            _toolbarPanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.HeaderTint, 0.24f, 0.12f, 8, 6);
            _masterPanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.20f, 0.10f, 7, 5);
            _routerPanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.18f, 0.09f, 7, 5);
            _schedulePanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.20f, 0.10f, 7, 5);
            _staticPanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.16f, 0.08f, 7, 5);
            _componentPanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.14f, 0.07f, 7, 4);
            _instancePanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.12f, 0.06f, 7, 4);
            _fieldPanelStyle = UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f, 6, 3);

            _statusPillStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Italic,
                normal = { textColor = UtilityWindowTheme.MutedText },
                padding = new RectOffset(6, 6, 2, 2)
            };

            _categoryLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = UtilityWindowTheme.CardText },
                clipping = TextClipping.Clip
            };
        }

        private static GUIStyle MakeTintedBoxStyle(Color tint, float alpha, int padding, int margin)
        {
            string key = $"{tint.r:0.000}:{tint.g:0.000}:{tint.b:0.000}:{alpha:0.000}:{padding}:{margin}:{EditorGUIUtility.isProSkin}";
            if (_tintedBoxStyleCache.TryGetValue(key, out GUIStyle cached))
                return cached;

            Color background = EditorGUIUtility.isProSkin
                ? new Color(tint.r, tint.g, tint.b, alpha)
                : new Color(tint.r, tint.g, tint.b, Mathf.Min(alpha * 0.65f, 0.18f));

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(padding, padding, padding, padding),
                margin = new RectOffset(margin, margin, margin, margin)
            };
            style.normal.background = MakeBackgroundTexture(background);
            _tintedBoxStyleCache[key] = style;
            return style;
        }

        private static Texture2D MakeBackgroundTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private static Color DebugTint(BoolToggleCategory category)
        {
            switch (category)
            {
                case BoolToggleCategory.Debug:
                    return UtilityWindowTheme.Blue;
                case BoolToggleCategory.Log:
                    return UtilityWindowTheme.Green;
                case BoolToggleCategory.Gizmo:
                    return UtilityWindowTheme.Purple;
                case BoolToggleCategory.Diagnostic:
                    return UtilityWindowTheme.Amber;
                case BoolToggleCategory.Other:
                    return UtilityWindowTheme.Neutral;
                case BoolToggleCategory.Any:
                default:
                    return UtilityWindowTheme.Cyan;
            }
        }

        private static Color WithValue(Color color, float value)
        {
            return new Color(color.r * value, color.g * value, color.b * value, color.a);
        }

        private static Color ReadablePillTint(Color tint)
        {
            // Count/status pills use white text, so keep their backgrounds darker
            // than normal button/category tints.
            Color.RGBToHSV(tint, out float h, out float s, out float v);

            s = Mathf.Clamp(s, 0.35f, 0.78f);

            // Slightly darker in light skin because bright editor backgrounds reduce contrast.
            float maxValue = EditorGUIUtility.isProSkin ? 0.48f : 0.40f;
            float minValue = EditorGUIUtility.isProSkin ? 0.26f : 0.22f;

            v = Mathf.Clamp(v, minValue, maxValue);

            Color result = Color.HSVToRGB(h, s, v);
            result.a = 1f;
            return result;
        }

        private static bool DrawTintedButton(string label, Color tint, params GUILayoutOption[] options)
        {
            return UtilityWindowTheme.TintedButton(label, tint, options);
        }

        private static bool DrawTintedButton(GUIContent content, Color tint, params GUILayoutOption[] options)
        {
            using (new GuiBackgroundScope(tint))
                return GUILayout.Button(content, options);
        }

        private static void DrawToolbarToggle(ref bool value, string label, Color tint)
        {
            DrawToolbarToggle(ref value, label, tint, null);
        }

        private static void DrawToolbarToggle(ref bool value, string label, Color tint, string tooltip, params GUILayoutOption[] options)
        {
            Color inactive = EditorGUIUtility.isProSkin ? new Color(0.42f, 0.43f, 0.48f) : new Color(0.75f, 0.76f, 0.80f);
            using (new GuiBackgroundScope(value ? tint : inactive))
            {
                GUIContent content = string.IsNullOrEmpty(tooltip) ? new GUIContent(label) : new GUIContent(label, tooltip);
                if (options != null && options.Length > 0)
                    value = GUILayout.Toggle(value, content, EditorStyles.toolbarButton, options);
                else
                    value = GUILayout.Toggle(value, content, EditorStyles.toolbarButton);
            }
        }

        private static void DrawStatusPill(string text)
        {
            EditorGUILayout.LabelField(new GUIContent(text, "Current Debug Control Center status."), _statusPillStyle, GUILayout.MinWidth(180f));
        }

        private static void DrawCountPill(string text, Color tint, float width = 0f)
        {
            UtilityWindowTheme.CountPill(text, tint, width);
        }

        private static void DrawCategoryPill(BoolToggleCategory category, float width)
        {
            UtilityWindowTheme.CountPill(category.ToString(), DebugTint(category), width);
        }

        private struct GuiBackgroundScope : IDisposable
        {
            private readonly Color _previous;

            public GuiBackgroundScope(Color color)
            {
                _previous = GUI.backgroundColor;
                GUI.backgroundColor = color;
            }

            public void Dispose()
            {
                GUI.backgroundColor = _previous;
            }
        }

        private void DrawCompactHeaderStrip()
        {
            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions
            {
                UtilityId = "debug-control",
                Title = position.width < 460f ? "Debug Control" : "Debug Control Center",
                Description = "Scene debug fields, router state, scheduled diagnostics, and broad debug controls.",
                Status = _status,
                CompactStatus = _status,
                HelpSectionId = "overview",
                HelpTopicId = "overview",
                ShowHelp = true,
                ShowMinimizeTray = true,
                ShowMinimizeButton = true,
                Tint = UtilityWindowTheme.HeaderTint
            });
        }

        private void DrawSearchAndRefineStrip()
        {
            using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("Search", "Filter components, object paths, fields, context actions, router rows, static toggles, and scheduled actions."), _categoryLabelStyle, GUILayout.Width(52f));
                    EditorGUI.BeginChangeCheck();
                    _search = EditorGUILayout.TextField(new GUIContent(GUIContent.none.text, "Type to filter the Debug Control Center lists."), _search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (EditorGUI.EndChangeCheck())
                        UtilityWindowPrefs.SetString(SearchPrefsKey, _search);

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                    {
                        if (GUILayout.Button(new GUIContent("Clear", "Clear the component/router/scheduler/static filter text."), EditorStyles.miniButton, GUILayout.Width(48f)))
                        {
                            _search = string.Empty;
                            UtilityWindowPrefs.SetString(SearchPrefsKey, _search);
                        }
                    }
                    PungentUtilityHelpButton.Draw("debug-control", "search-filter", "search-filter", "Open help for Debug Control Center search and filters.", "Search/filter strip");
                }

                DrawResponsiveCommandRow(
                    DrawScopeModeToggle,
                    DrawVisibilityModeToggle,
                    () => DrawDebuggableModeToggle(),
                    DrawGroupModeToggle);

                DrawFilterViewAndRefreshRow();
            }
        }

        private void DrawFilterViewAndRefreshRow()
        {
            if (position.width < 700f)
            {
                DrawResponsiveCommandRow(
                    () =>
                    {
                        EditorGUILayout.LabelField(new GUIContent("View", "Choose which component detail sections are visible."), _mutedMiniLabelStyle, GUILayout.Width(38f));
                        DrawPersistentToolbarToggle(ref _showBoolToggles, ShowBoolTogglesPrefsKey, "Bools", UtilityWindowTheme.Cyan, "Show component bool/debug/log/gizmo toggle sections.");
                    },
                    () => DrawPersistentToolbarToggle(ref _showContextActions, ShowContextActionsPrefsKey, "Actions", UtilityWindowTheme.Amber, "Show component ContextMenu diagnostic action sections."),
                    () => DrawPersistentToolbarToggle(ref _showSnapshotButtons, ShowSnapshotButtonsPrefsKey, "Snapshots", UtilityWindowTheme.Teal, "Show snapshot copy tools on expanded components."),
                    DrawBulkControlsToolbarButton,
                    DrawStaticTogglesTrayButton);

                using (new EditorGUILayout.HorizontalScope())
                    DrawRefreshControlCluster(compact: true);

                if (_bulkControlsOpen)
                    DrawMasterControls();

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("View", "Choose which component detail sections are visible."), _mutedMiniLabelStyle, GUILayout.Width(38f));
                DrawPersistentToolbarToggle(ref _showBoolToggles, ShowBoolTogglesPrefsKey, "Bools", UtilityWindowTheme.Cyan, "Show component bool/debug/log/gizmo toggle sections.");
                DrawPersistentToolbarToggle(ref _showContextActions, ShowContextActionsPrefsKey, "Actions", UtilityWindowTheme.Amber, "Show component ContextMenu diagnostic action sections.");
                DrawPersistentToolbarToggle(ref _showSnapshotButtons, ShowSnapshotButtonsPrefsKey, "Snapshots", UtilityWindowTheme.Teal, "Show snapshot copy tools on expanded components.");

                GUILayout.Space(12f);
                DrawBulkControlsToolbarButton();
                DrawStaticTogglesTrayButton();

                GUILayout.FlexibleSpace();
                DrawRefreshControlCluster(compact: false);
            }

            if (_bulkControlsOpen)
                DrawMasterControls();
        }

        private void DrawBulkControlsToolbarButton()
        {
            EditorGUI.BeginChangeCheck();
            using (new GuiBackgroundScope(_bulkControlsOpen ? UtilityWindowTheme.Purple : UtilityWindowTheme.Neutral))
                _bulkControlsOpen = GUILayout.Toggle(_bulkControlsOpen, new GUIContent("Bulk Controls", "Show broad category controls for the scanned or filtered component set."), EditorStyles.toolbarButton, GUILayout.Width(104f));
            if (EditorGUI.EndChangeCheck())
                UtilityWindowPrefs.SetBool(BulkControlsOpenPrefsKey, _bulkControlsOpen);
        }

        private void DrawStaticTogglesTrayButton()
        {
            Rect buttonRect;
            using (new GuiBackgroundScope(_showStaticDebugFields ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral))
            {
                if (GUILayout.Button(new GUIContent($"Static {_staticBoolToggles.Count}", "Open static debug/log/gizmo bool controls in a popup tray."), EditorStyles.toolbarButton, GUILayout.Width(86f)))
                {
                    buttonRect = GUILayoutUtility.GetLastRect();
                    PopupWindow.Show(buttonRect, new StaticTogglesPopup(this));
                }
            }
        }

        private void DrawRefreshControlCluster(bool compact)
        {
            DrawCountPill(_scanCacheDirty ? "Stale" : "Live", _scanCacheDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, compact ? 54f : 58f);

            if (DrawTintedButton(new GUIContent(compact ? "Refresh" : "Refresh", "Refresh the cached scene/component debug data now."), UtilityWindowTheme.Amber, GUILayout.Width(compact ? 72f : 84f)))
                Refresh();

            EditorGUI.BeginChangeCheck();
            DrawToolbarToggle(ref _autoRefreshSceneScan, compact ? "Auto" : "Auto Refresh", UtilityWindowTheme.Cyan, "Automatically refresh the cached scene/component scan when the hierarchy changes. Refresh remains throttled.");
            if (EditorGUI.EndChangeCheck())
                UtilityWindowPrefs.SetBool("GenericDebugControlWindow.AutoRefreshSceneScan", _autoRefreshSceneScan);
        }

        private void DrawCompactStatusStrip()
        {
            List<DebugComponentInfo> filtered = GetFilteredComponents();
            int boolCount = filtered.Sum(info => info.boolToggles.Count);
            int actionCount = filtered.Sum(info => info.contextActions.Count);
            int activeScheduled = _scheduledCalls.Count(call => call != null && call.enabled && call.target != null);
            string status = position.width < 700f
                ? $"Comp {filtered.Count}/{_components.Count} | Bool {boolCount} | Act {actionCount} | Static {_staticBoolToggles.Count} | Sched {activeScheduled}/{_scheduledCalls.Count} | Router {_routerChannels.Count}/{_routerSignals.Count} | Auto {(_autoRefreshSceneScan ? "on" : "off")}"
                : $"Components {filtered.Count}/{_components.Count} | Bools {boolCount} | Actions {actionCount} | Static {_staticBoolToggles.Count} | Scheduled {activeScheduled}/{_scheduledCalls.Count} | Router {_routerChannels.Count}ch/{_routerSignals.Count}sig | Auto {(_autoRefreshSceneScan ? "on" : "off")}";
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(status, "Summary of currently visible components, toggles, actions, static toggles, scheduled actions, router data, and auto-refresh state."), _mutedMiniLabelStyle);
            }
        }

        private void DrawContextHelpStrip()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 2)))
            {
                EditorGUILayout.LabelField(new GUIContent("Help", "Open contextual Debug Control Center help topics."), _mutedMiniLabelStyle, GUILayout.Width(34f));
                PungentUtilityHelpButton.Draw("debug-control", "components", "components", "Open help for discovered components.", "Components help");
                PungentUtilityHelpButton.Draw("debug-control", "component-chips", "component-chips", "Open help for component chips.", "Component chips help");
                PungentUtilityHelpButton.Draw("debug-control", "router", "router", "Open help for DebugRouter.", "Router help");
                PungentUtilityHelpButton.Draw("debug-control", "scheduler", "scheduler", "Open help for Scheduler.", "Scheduler help");
                PungentUtilityHelpButton.Draw("debug-control", "static-toggles", "static-toggles", "Open help for static toggles.", "Static toggles help");
                PungentUtilityHelpButton.Draw("debug-control", "bulk-controls", "bulk-controls", "Open help for bulk controls.", "Bulk controls help");
                PungentUtilityHelpButton.Draw("debug-control", "scripting-index", "scripting-index", "Open help for DebugRouter scripting references.", "Scripting reference help");
                GUILayout.FlexibleSpace();
            }
        }


        private void DrawStackedWorkbenchLayout()
        {
            bool previousStretch = _stretchWorkbench;
            bool previousDrawingSupportColumn = _drawingSupportColumn;
            float previousPrimaryWidth = _primaryWorkbenchWidth;

            _stretchWorkbench = false;
            _drawingSupportColumn = false;
            _primaryWorkbenchWidth = Mathf.Max(260f, position.width - 20f);

            try
            {
                DrawDiscoveredComponentsPanel();
                DrawSupportColumn();
            }
            finally
            {
                _stretchWorkbench = previousStretch;
                _drawingSupportColumn = previousDrawingSupportColumn;
                _primaryWorkbenchWidth = previousPrimaryWidth;
            }
        }

        private void DrawWorkbenchLayout()
        {
            _supportColumnWidth = GetClampedSupportColumnWidth();
            ClampSupportPanelHeights();

            bool previousStretch = _stretchWorkbench;
            bool previousDrawingSupportColumn = _drawingSupportColumn;
            float previousPrimaryWidth = _primaryWorkbenchWidth;
            float previousWorkbenchHeight = _workbenchHeight;

            _stretchWorkbench = true;
            _drawingSupportColumn = false;
            _workbenchHeight = Mathf.Max(260f, position.height - 132f);

            float availableWidth = Mathf.Max(position.width - 18f, MinPrimaryWorkbenchWidth + MinSupportColumnWidth + WorkbenchResizeHandleWidth);
            float maxSupportWidth = Mathf.Max(
                MinSupportColumnWidth,
                Mathf.Min(
                    availableWidth * MaxSupportColumnWidthRatio,
                    availableWidth - MinPrimaryWorkbenchWidth - WorkbenchResizeHandleWidth));

            _supportColumnWidth = Mathf.Clamp(_supportColumnWidth, MinSupportColumnWidth, maxSupportWidth);
            _primaryWorkbenchWidth = Mathf.Max(
                MinPrimaryWorkbenchWidth,
                availableWidth - _supportColumnWidth - WorkbenchResizeHandleWidth);

            try
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    using (new EditorGUILayout.VerticalScope(
                        GUILayout.Width(_primaryWorkbenchWidth),
                        GUILayout.ExpandHeight(true)))
                    {
                        DrawDiscoveredComponentsPanel();
                    }

                    UtilityWindowTheme.HorizontalResizeHandle(
                        ref _supportColumnWidth,
                        MinSupportColumnWidth,
                        maxSupportWidth,
                        () => UtilityWindowPrefs.SetFloat(SupportColumnWidthPrefsKey, _supportColumnWidth),
                        "Drag to resize the component browser and support controls",
                        invertDelta: true);

                    _drawingSupportColumn = true;

                    using (new EditorGUILayout.VerticalScope(
                        GUILayout.Width(_supportColumnWidth),
                        GUILayout.ExpandHeight(true)))
                    {
                        DrawSupportColumn();
                    }
                }
            }
            finally
            {
                _stretchWorkbench = previousStretch;
                _drawingSupportColumn = previousDrawingSupportColumn;
                _primaryWorkbenchWidth = previousPrimaryWidth;
                _workbenchHeight = previousWorkbenchHeight;
            }
        }

        private void DrawSupportColumn()
        {
            bool previous = _drawingSupportColumn;
            _drawingSupportColumn = true;
            try
            {
                if (_stretchWorkbench)
                {
                    DrawResizableSupportStack();
                    return;
                }

                DrawRouterPanel();
                DrawSchedulePanel();
            }
            finally
            {
                _drawingSupportColumn = previous;
            }
        }

        private void DrawResizableSupportStack()
        {
            const float handleHeight = 9f;

            float availableHeight = Mathf.Max(
                MinSupportPanelHeight * 2f + handleHeight,
                _workbenchHeight - 8f);

            float maxByRemainingSpace = Mathf.Max(
                MinSupportPanelHeight,
                availableHeight - handleHeight - MinSupportPanelHeight);

            float routerPreferredHeight = EstimateRouterPanelPreferredHeightForSupport();

            float maxRouterHeight = Mathf.Max(
                MinSupportPanelHeight,
                Mathf.Min(maxByRemainingSpace, routerPreferredHeight));

            _routerPanelHeight = Mathf.Clamp(_routerPanelHeight, MinSupportPanelHeight, maxRouterHeight);
            _schedulePanelHeight = Mathf.Max(MinSupportPanelHeight, availableHeight - _routerPanelHeight - handleHeight);

            DrawSupportPanelSlot(ref _routerPanelScroll, _routerPanelHeight, DrawRouterPanel);

            UtilityWindowTheme.VerticalResizeHandle(
                ref _routerPanelHeight,
                MinSupportPanelHeight,
                maxRouterHeight,
                SaveSupportPanelHeights,
                "Drag to resize Router and Scheduler panels");

            _routerPanelHeight = Mathf.Clamp(_routerPanelHeight, MinSupportPanelHeight, maxRouterHeight);
            _schedulePanelHeight = Mathf.Max(MinSupportPanelHeight, availableHeight - _routerPanelHeight - handleHeight);

            DrawSupportPanelSlot(ref _schedulePanelScroll, _schedulePanelHeight, DrawSchedulePanel);
        }

        private void DrawSupportPanelSlot(ref Vector2 scroll, float height, Action drawPanel)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.Height(height)))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                try
                {
                    drawPanel?.Invoke();
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void ClampSupportPanelHeights()
        {
            _routerPanelHeight = Mathf.Clamp(_routerPanelHeight, MinSupportPanelHeight, MaxSupportPanelHeight);
            _schedulePanelHeight = Mathf.Clamp(_schedulePanelHeight, MinSupportPanelHeight, MaxSupportPanelHeight);
        }

        private void SaveSupportPanelHeights()
        {
            ClampSupportPanelHeights();
            UtilityWindowPrefs.SetFloat(RouterPanelHeightPrefsKey, _routerPanelHeight);
            UtilityWindowPrefs.SetFloat(SchedulePanelHeightPrefsKey, _schedulePanelHeight);
        }

        private sealed class StaticTogglesPopup : PopupWindowContent
        {
            private readonly DebugControlWindow _owner;
            private Vector2 _scroll;

            public StaticTogglesPopup(DebugControlWindow owner)
            {
                _owner = owner;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(430f, 480f);
            }

            public override void OnGUI(Rect rect)
            {
                if (_owner == null)
                    return;

                DebugControlWindow.EnsureStyles();
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                try
                {
                    _owner.DrawStaticDebugFields();
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawCollapsedHelpFooter()
        {
            DrawHelpCard();
        }

        private void RefreshOnOpen()
        {
            _initialOpenRefreshQueued = false;
            if (this == null)
                return;

            if (_components.Count > 0)
                return;

            Refresh();
            bool hasRouterData = _routerChannels.Count + _routerSignals.Count + _routerStates.Count + _routerSources.Count > 0;
            _routerPanelOpen = hasRouterData && UtilityWindowPrefs.GetBool(RouterPanelOpenPrefsKey, true);
            bool hasActiveSchedule = _scheduledCalls.Any(call => call != null && call.enabled);
            _scheduledPanelOpen = hasActiveSchedule || UtilityWindowPrefs.GetBool(ScheduledPanelOpenPrefsKey, false);
            _staticFieldsPanelOpen = _staticBoolToggles.Count > 0 && _components.Count < 30 && UtilityWindowPrefs.GetBool(StaticFieldsPanelOpenPrefsKey, true);
            RequestRepaintThrottled();
        }

        private void QueueInitialRefreshIfNeeded()
        {
            if (_components.Count > 0 || _initialOpenRefreshQueued)
                return;

            _initialOpenRefreshQueued = true;
            EditorApplication.delayCall -= RefreshOnOpen;
            EditorApplication.delayCall += RefreshOnOpen;
        }

        private bool MatchesSearch(string text)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            return !string.IsNullOrEmpty(text) && text.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
#endif

}
