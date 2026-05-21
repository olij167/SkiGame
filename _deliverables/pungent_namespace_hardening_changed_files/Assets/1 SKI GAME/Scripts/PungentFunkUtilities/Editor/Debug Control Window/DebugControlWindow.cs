using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
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
    /// Open via: Tools/Utilities/Debug/Debug Control Center
    /// </summary>
    public sealed class DebugControlWindow : EditorWindow
    {
        private enum GroupMode
        {
            ComponentType,
            SceneObject
        }

        private enum ConditionMode
        {
            Always,
            PlayModeOnly,
            SelectedTarget,
            TargetEnabled,
            BoolMemberTrue,
            BoolMemberFalse
        }

        private enum BoolToggleCategory
        {
            Any,
            Debug,
            Log,
            Gizmo,
            Diagnostic,
            Other
        }

        [Serializable]
        private sealed class ScheduledCall
        {
            public UnityEngine.Object target;
            public string targetGlobalId;
            public string targetScenePath;
            public string targetTypeName;
            public string methodName;
            public string displayName;
            public bool enabled = true;
            public bool foldout = true;
            public float intervalSeconds = 5f;
            public double nextRunTime;
            public ConditionMode conditionMode = ConditionMode.PlayModeOnly;
            public UnityEngine.Object conditionTarget;
            public string conditionTargetGlobalId;
            public string conditionScenePath;
            public string conditionTargetTypeName;
            public string conditionMemberName;
            public int fireCount;
            public string lastResult = "Never";
        }

        [Serializable]
        private sealed class ScheduledCallSaveData
        {
            public List<ScheduledCallSave> calls = new List<ScheduledCallSave>();
        }

        [Serializable]
        private sealed class ScheduledCallSave
        {
            public string targetGlobalId;
            public string targetScenePath;
            public string targetTypeName;
            public string methodName;
            public string displayName;
            public bool enabled;
            public bool foldout;
            public float intervalSeconds;
            public int conditionMode;
            public string conditionTargetGlobalId;
            public string conditionScenePath;
            public string conditionTargetTypeName;
            public string conditionMemberName;
            public int fireCount;
            public string lastResult;
        }

        private sealed class DebugComponentInfo
        {
            public MonoBehaviour component;
            public string objectPath;
            public List<FieldInfo> boolToggles = new List<FieldInfo>();
            public List<MethodInfo> contextActions = new List<MethodInfo>();
        }

        private sealed class StaticDebugFieldInfo
        {
            public Type declaringType;
            public FieldInfo field;
        }

        private struct ToggleStats
        {
            public int total;
            public int onCount;

            public int offCount
            {
                get { return total - onCount; }
            }

            public bool hasAny
            {
                get { return total > 0; }
            }

            public bool allOn
            {
                get { return total > 0 && onCount == total; }
            }

            public bool mixed
            {
                get { return total > 0 && onCount > 0 && onCount < total; }
            }
        }

        private readonly List<DebugComponentInfo> _components = new List<DebugComponentInfo>();
        private readonly List<StaticDebugFieldInfo> _staticBoolToggles = new List<StaticDebugFieldInfo>();
        private readonly List<ScheduledCall> _scheduledCalls = new List<ScheduledCall>();
        private readonly Dictionary<string, bool> _typeFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<int, bool> _instanceFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<string, bool> _sceneObjectFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _groupBoolFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<int, bool> _boolSectionFoldouts = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _actionSectionFoldouts = new Dictionary<int, bool>();

        private Vector2 _scroll;
        private Vector2 _scheduleScroll;
        private Vector2 _routerScroll;
        private Vector2 _staticScroll;
        private Vector2 _componentsScroll;
        private string _search = string.Empty;
        private bool _includeInactive = true;
        private bool _selectedHierarchyOnly;
        private bool _showOnlyDebuggable = true;
        private bool _showStaticDebugFields = true;
        private bool _showContextActions = true;
        private bool _showBoolToggles = true;
        private bool _showSnapshotButtons = true;
        private GroupMode _groupMode = GroupMode.ComponentType;
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
        private float _routerPanelHeight = 180f;
        private float _scheduledPanelHeight = 220f;
        private float _staticPanelHeight = 190f;
        private float _componentsPanelHeight = 520f;
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
        private const string RouterHeightPrefsKey = "GenericDebugControlWindow.RouterPanelHeight";
        private const string RouterPanelOpenPrefsKey = "GenericDebugControlWindow.RouterPanelOpen";
        private const string RouterChannelsOpenPrefsKey = "GenericDebugControlWindow.RouterChannelsOpen";
        private const string RouterSignalsOpenPrefsKey = "GenericDebugControlWindow.RouterSignalsOpen";
        private const string RouterStatesOpenPrefsKey = "GenericDebugControlWindow.RouterStatesOpen";
        private const string RouterSourcesOpenPrefsKey = "GenericDebugControlWindow.RouterSourcesOpen";
        private const string ScheduleHeightPrefsKey = "GenericDebugControlWindow.SchedulePanelHeight.PreRouter";
        private const string StaticHeightPrefsKey = "GenericDebugControlWindow.StaticPanelHeight.PreRouter";
        private const string ComponentsHeightPrefsKey = "GenericDebugControlWindow.ComponentsPanelHeight.PreRouter";
        private const double ScheduleTickInterval = 0.20;
        private const double RouterRefreshInterval = 0.50;
        private const double RepaintThrottleInterval = 0.20;
        private const double AutoSceneRefreshInterval = 2.00;

        [MenuItem("Tools/Utilities/Debug/Debug Control Center")]
        public static void Open()
        {
            GetWindow<DebugControlWindow>("Debug Control Center");
        }

        [MenuItem("Tools/Debug/Debug Control Center", priority = 9000)]
        public static void OpenLegacy()
        {
            Open();
        }

        private void OnEnable()
        {
            _routerPanelHeight = UtilityWindowPrefs.GetFloat(RouterHeightPrefsKey, 180f);
            _routerPanelOpen = UtilityWindowPrefs.GetBool(RouterPanelOpenPrefsKey, true);
            _routerChannelsOpen = UtilityWindowPrefs.GetBool(RouterChannelsOpenPrefsKey, true);
            _routerSignalsOpen = UtilityWindowPrefs.GetBool(RouterSignalsOpenPrefsKey, true);
            _routerStatesOpen = UtilityWindowPrefs.GetBool(RouterStatesOpenPrefsKey, false);
            _routerSourcesOpen = UtilityWindowPrefs.GetBool(RouterSourcesOpenPrefsKey, false);
            _scheduledPanelHeight = UtilityWindowPrefs.GetFloat(ScheduleHeightPrefsKey, 220f);
            _staticPanelHeight = UtilityWindowPrefs.GetFloat(StaticHeightPrefsKey, 190f);
            _componentsPanelHeight = UtilityWindowPrefs.GetFloat(ComponentsHeightPrefsKey, 520f);
            LoadScheduledCalls();
            ResolveScheduledReferences();
            EditorApplication.update += OnEditorUpdate;
            _scanCacheDirty = true;
            RefreshRouterSnapshots();
            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            UtilityWindowPrefs.SetFloat(RouterHeightPrefsKey, _routerPanelHeight);
            UtilityWindowPrefs.SetBool(RouterPanelOpenPrefsKey, _routerPanelOpen);
            UtilityWindowPrefs.SetBool(RouterChannelsOpenPrefsKey, _routerChannelsOpen);
            UtilityWindowPrefs.SetBool(RouterSignalsOpenPrefsKey, _routerSignalsOpen);
            UtilityWindowPrefs.SetBool(RouterStatesOpenPrefsKey, _routerStatesOpen);
            UtilityWindowPrefs.SetBool(RouterSourcesOpenPrefsKey, _routerSourcesOpen);
            UtilityWindowPrefs.SetFloat(ScheduleHeightPrefsKey, _scheduledPanelHeight);
            UtilityWindowPrefs.SetFloat(StaticHeightPrefsKey, _staticPanelHeight);
            UtilityWindowPrefs.SetFloat(ComponentsHeightPrefsKey, _componentsPanelHeight);
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

            if (_routerPanelOpen && now >= _nextRouterRefreshTime)
            {
                _nextRouterRefreshTime = now + RouterRefreshInterval;
                RefreshRouterSnapshots();
                RequestRepaintThrottled();
            }

            if (now < _nextScheduleTickTime)
                return;

            _nextScheduleTickTime = now + ScheduleTickInterval;
            TickScheduledActions(now);
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            DrawRouterPanel();
            DrawSchedulePanel();
            DrawStaticDebugFields();
            DrawDiscoveredComponentsPanel();
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

        private static void DrawToolbarToggle(ref bool value, string label, Color tint)
        {
            UtilityWindowTheme.ToolbarToggle(ref value, label, tint);
        }

        private static void DrawStatusPill(string text)
        {
            EditorGUILayout.LabelField(text, _statusPillStyle, GUILayout.MinWidth(180f));
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

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(_toolbarPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Debug Control Center", _titleStyle);
                    GUILayout.FlexibleSpace();
                    DrawStatusPill(_status);
                }

                EditorGUILayout.LabelField(
                    "Central control for scene debug/log/gizmo bools, [ContextMenu] debug actions, persistent scheduled diagnostics, and plain-text component snapshots.",
                    _subTitleStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawTintedButton("Refresh", UtilityWindowTheme.Amber, GUILayout.Width(90f)))
                        Refresh();

                    DrawToolbarToggle(ref _autoRefreshSceneScan, "Auto refresh", UtilityWindowTheme.Cyan);

                    if (DrawTintedButton("Expand All", UtilityWindowTheme.Blue, GUILayout.Width(90f)))
                        SetAllFoldouts(true);

                    if (DrawTintedButton("Collapse All", UtilityWindowTheme.Neutral, GUILayout.Width(95f)))
                        SetAllFoldouts(false);

                    GUILayout.FlexibleSpace();
                    DrawCountPill(_scanCacheDirty ? "STALE" : "LIVE", _scanCacheDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 54f);
                    DrawCountPill($"Components: {_components.Count}", UtilityWindowTheme.Blue);
                    DrawCountPill($"Static: {_staticBoolToggles.Count}", UtilityWindowTheme.Purple);
                    DrawCountPill($"Scheduled: {_scheduledCalls.Count}", UtilityWindowTheme.Amber);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search", _categoryLabelStyle, GUILayout.Width(54f));
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48f)))
                        _search = string.Empty;
                    _groupMode = (GroupMode)EditorGUILayout.EnumPopup(_groupMode, GUILayout.Width(148f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawToolbarToggle(ref _includeInactive, "Include inactive", UtilityWindowTheme.Neutral);
                    DrawToolbarToggle(ref _selectedHierarchyOnly, "Selected hierarchy", UtilityWindowTheme.Blue);
                    DrawToolbarToggle(ref _showOnlyDebuggable, "Only debuggable", UtilityWindowTheme.Purple);
                    DrawToolbarToggle(ref _showBoolToggles, "Bool toggles", UtilityWindowTheme.Cyan);
                    DrawToolbarToggle(ref _showContextActions, "Context actions", UtilityWindowTheme.Amber);
                    DrawToolbarToggle(ref _showSnapshotButtons, "Snapshots", UtilityWindowTheme.Teal);
                    DrawToolbarToggle(ref _showStaticDebugFields, "Static toggles", UtilityWindowTheme.Purple);
                }
            }

            DrawMasterControls();
        }

        private void DrawRouterPanel()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(_routerPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool nextOpen = EditorGUILayout.Foldout(_routerPanelOpen, "Debug Router", true, _sectionHeaderStyle);
                    if (nextOpen != _routerPanelOpen)
                    {
                        _routerPanelOpen = nextOpen;
                        UtilityWindowPrefs.SetBool(RouterPanelOpenPrefsKey, _routerPanelOpen);
                        if (_routerPanelOpen)
                        {
                            RefreshRouterSnapshots();
                            _nextRouterRefreshTime = EditorApplication.timeSinceStartup + RouterRefreshInterval;
                        }
                    }

                    GUILayout.FlexibleSpace();
                    DrawCountPill($"Channels: {_routerChannels.Count}", UtilityWindowTheme.Cyan, 92f);
                    DrawCountPill($"Signals: {_routerSignals.Count}", UtilityWindowTheme.Purple, 82f);
                    DrawCountPill($"States: {_routerStates.Count}", UtilityWindowTheme.Blue, 78f);
                    DrawCountPill($"Sources: {_routerSources.Count}", UtilityWindowTheme.Teal, 82f);
                }

                if (!_routerPanelOpen)
                    return;

                DrawRouterTopControls();

                float contentHeight = EstimateRouterContentHeight();
                float listHeight = Mathf.Min(_routerPanelHeight, Mathf.Max(72f, contentHeight));
                listHeight = Mathf.Clamp(listHeight, 72f, 560f);

                _routerScroll = EditorGUILayout.BeginScrollView(_routerScroll, GUILayout.MinHeight(72f), GUILayout.Height(listHeight));
                DrawRouterChannels();
                DrawRouterSignals();
                DrawRouterStates();
                DrawRouterSources();
                EditorGUILayout.EndScrollView();

                if (contentHeight > 100f)
                {
                    UtilityWindowTheme.VerticalResizeHandle(
                        ref _routerPanelHeight,
                        90f,
                        560f,
                        () => UtilityWindowPrefs.SetFloat(RouterHeightPrefsKey, _routerPanelHeight),
                        "Drag to resize the Debug Router list area");
                }
            }
        }

        private void DrawRouterTopControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                bool globalEnabled = DrawRouterToolbarToggle(DebugRouter.GlobalEnabled, "Global Enabled", UtilityWindowTheme.Green, 108f);
                bool prefixMessages = DrawRouterToolbarToggle(DebugRouter.PrefixMessages, "Prefix Messages", UtilityWindowTheme.Cyan, 118f);
                bool signalsEnabled = DrawRouterToolbarToggle(DebugRouter.SignalsEnabled, "Signals Enabled", UtilityWindowTheme.Purple, 112f);
                if (EditorGUI.EndChangeCheck())
                {
                    DebugRouter.GlobalEnabled = globalEnabled;
                    DebugRouter.PrefixMessages = prefixMessages;
                    DebugRouter.SignalsEnabled = signalsEnabled;
                    _status = "Updated DebugRouter top-level controls.";
                    RefreshRouterSnapshots();
                }

                GUILayout.FlexibleSpace();

                if (DrawTintedButton("Refresh Router", UtilityWindowTheme.Blue, GUILayout.Width(112f)))
                {
                    RefreshRouterSnapshots();
                    _nextRouterRefreshTime = EditorApplication.timeSinceStartup + RouterRefreshInterval;
                    _status = "Refreshed DebugRouter snapshots.";
                }

                if (DrawTintedButton("Clear Runtime Data", UtilityWindowTheme.Red, GUILayout.Width(132f)))
                {
                    DebugRouter.ClearRuntimeData();
                    RefreshRouterSnapshots();
                    _status = "Cleared DebugRouter runtime data.";
                }
            }
        }

        private static bool DrawRouterToolbarToggle(bool value, string label, Color activeTint, float width)
        {
            Color inactive = EditorGUIUtility.isProSkin ? new Color(0.42f, 0.43f, 0.48f) : new Color(0.75f, 0.76f, 0.80f);
            using (new GuiBackgroundScope(value ? activeTint : inactive))
                return GUILayout.Toggle(value, label, EditorStyles.toolbarButton, GUILayout.Width(width));
        }

        private void DrawRouterChannels()
        {
            bool open = DrawRouterFoldout(ref _routerChannelsOpen, RouterChannelsOpenPrefsKey, $"Channels ({FilteredRouterChannels().Count})", UtilityWindowTheme.Cyan);
            if (!open)
                return;

            EditorGUILayout.LabelField("Channels appear after DebugRouter.Log(...) or explicit registration.", _mutedMiniLabelStyle);
            List<DebugRouter.ChannelSnapshot> rows = FilteredRouterChannels();
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching router channels yet.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.ChannelSnapshot row = rows[i];
                string channel = row.channel ?? "General";
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    EditorGUI.BeginChangeCheck();
                    bool next = DrawRouterSmallToggle(row.enabled, row.enabled ? "On" : "Off", UtilityWindowTheme.Cyan, 46f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        DebugRouter.SetChannelEnabled(channel, next);
                        RefreshRouterSnapshots();
                        _status = $"Set DebugRouter channel '{channel}' = {next}.";
                    }

                    EditorGUILayout.LabelField(new GUIContent(Shorten(channel, 42), channel), _categoryLabelStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.sourceCount} src", UtilityWindowTheme.Teal, 58f);
                    DrawCountPill($"{row.logCount} logs", UtilityWindowTheme.Blue, 64f);
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastOwner, 18), row.lastOwner), _pathLabelStyle, GUILayout.Width(110f));
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastMessage, 42), row.lastMessage), _pathLabelStyle, GUILayout.Width(210f));
                }
            }
        }

        private void DrawRouterSignals()
        {
            bool open = DrawRouterFoldout(ref _routerSignalsOpen, RouterSignalsOpenPrefsKey, $"Signals ({FilteredRouterSignals().Count})", UtilityWindowTheme.Purple);
            if (!open)
                return;

            EditorGUILayout.LabelField("Signals appear after DebugRouter.Signal(...) or explicit signal registration.", _mutedMiniLabelStyle);
            List<DebugRouter.SignalSnapshot> rows = FilteredRouterSignals();
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching router signals yet.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.SignalSnapshot row = rows[i];
                string signalName = row.signalName ?? "Signal";
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    EditorGUI.BeginChangeCheck();
                    bool next = DrawRouterSmallToggle(row.consoleEnabled, row.consoleEnabled ? "Console" : "Muted", UtilityWindowTheme.Purple, 64f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        DebugRouter.SetSignalConsoleEnabled(signalName, next);
                        RefreshRouterSnapshots();
                        _status = $"Set DebugRouter signal console '{signalName}' = {next}.";
                    }

                    EditorGUILayout.LabelField(new GUIContent(Shorten(signalName, 44), signalName), _categoryLabelStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.fireCount} fires", UtilityWindowTheme.Purple, 70f);
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastOwner, 18), row.lastOwner), _pathLabelStyle, GUILayout.Width(110f));
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastDetails, 44), row.lastDetails), _pathLabelStyle, GUILayout.Width(220f));
                }
            }
        }

        private void DrawRouterStates()
        {
            bool open = DrawRouterFoldout(ref _routerStatesOpen, RouterStatesOpenPrefsKey, $"States ({FilteredRouterStates().Count})", UtilityWindowTheme.Blue);
            if (!open)
                return;

            EditorGUILayout.LabelField("States appear after DebugRouter.SetState(...).", _mutedMiniLabelStyle);
            List<DebugRouter.StateSnapshot> rows = FilteredRouterStates();
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching router states yet.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.StateSnapshot row = rows[i];
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    DrawCountPill(row.value ? "True" : "False", row.value ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 54f);
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.stateName, 40), row.stateName), _categoryLabelStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.changeCount} changes", UtilityWindowTheme.Blue, 86f);
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.ownerName, 18), row.ownerName), _pathLabelStyle, GUILayout.Width(110f));
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.details, 44), row.details), _pathLabelStyle, GUILayout.Width(220f));
                }
            }
        }

        private void DrawRouterSources()
        {
            bool open = DrawRouterFoldout(ref _routerSourcesOpen, RouterSourcesOpenPrefsKey, $"Sources ({FilteredRouterSources().Count})", UtilityWindowTheme.Teal);
            if (!open)
                return;

            EditorGUILayout.LabelField("Sources appear when an owner object logs/registers against a channel.", _mutedMiniLabelStyle);
            List<DebugRouter.SourceSnapshot> rows = FilteredRouterSources();
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching router sources yet.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DebugRouter.SourceSnapshot row = rows[i];
                UnityEngine.Object owner = row.ownerInstanceId != 0 ? EditorUtility.InstanceIDToObject(row.ownerInstanceId) : null;
                string channel = row.channel ?? "General";
                string ownerLabel = string.IsNullOrEmpty(row.ownerName) ? "Global" : row.ownerName;

                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    using (new EditorGUI.DisabledScope(owner == null))
                    {
                        EditorGUI.BeginChangeCheck();
                        bool next = DrawRouterSmallToggle(row.enabled, row.enabled ? "On" : "Off", UtilityWindowTheme.Teal, 46f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            DebugRouter.SetSourceEnabled(owner, channel, next);
                            RefreshRouterSnapshots();
                            _status = $"Set DebugRouter source '{ownerLabel}' / '{channel}' = {next}.";
                        }
                    }

                    string main = $"{ownerLabel} / {channel}";
                    EditorGUILayout.LabelField(new GUIContent(Shorten(main, 48), main), _categoryLabelStyle, GUILayout.MinWidth(140f));
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{row.logCount} logs", UtilityWindowTheme.Teal, 68f);
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.hierarchyPath, 28), row.hierarchyPath), _pathLabelStyle, GUILayout.Width(170f));
                    EditorGUILayout.LabelField(new GUIContent(Shorten(row.lastMessage, 34), row.lastMessage), _pathLabelStyle, GUILayout.Width(180f));
                    using (new EditorGUI.DisabledScope(owner == null))
                    {
                        if (DrawTintedButton("Ping", UtilityWindowTheme.Blue, GUILayout.Width(44f)))
                            EditorGUIUtility.PingObject(owner);
                    }
                }
            }
        }

        private bool DrawRouterFoldout(ref bool foldout, string prefsKey, string label, Color tint)
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.05f, 5, 3)))
            {
                bool next = EditorGUILayout.Foldout(foldout, label, true, _sectionHeaderStyle);
                if (next != foldout)
                {
                    foldout = next;
                    UtilityWindowPrefs.SetBool(prefsKey, foldout);
                }

                GUILayout.FlexibleSpace();
                DrawCountPill(foldout ? "Open" : "Closed", tint, 62f);
            }

            return foldout;
        }

        private static bool DrawRouterSmallToggle(bool value, string label, Color activeTint, float width)
        {
            Color tint = value ? activeTint : WithValue(activeTint, 0.55f);
            using (new GuiBackgroundScope(tint))
                return GUILayout.Toggle(value, label, EditorStyles.miniButton, GUILayout.Width(width));
        }

        private void RefreshRouterSnapshots()
        {
            try
            {
                _routerChannels = DebugRouter.GetChannels() ?? new List<DebugRouter.ChannelSnapshot>();
                _routerSources = DebugRouter.GetSources() ?? new List<DebugRouter.SourceSnapshot>();
                _routerStates = DebugRouter.GetStates() ?? new List<DebugRouter.StateSnapshot>();
                _routerSignals = DebugRouter.GetSignals() ?? new List<DebugRouter.SignalSnapshot>();
            }
            catch (Exception ex)
            {
                _routerChannels.Clear();
                _routerSources.Clear();
                _routerStates.Clear();
                _routerSignals.Clear();
                _status = "DebugRouter snapshot refresh failed.";
                Debug.LogWarning("[DebugControlWindow] DebugRouter snapshot refresh failed. " + ex.Message);
            }
        }

        private float EstimateRouterContentHeight()
        {
            float height = 8f;
            height += EstimateRouterSectionHeight(_routerChannelsOpen, FilteredRouterChannels().Count, 24f);
            height += EstimateRouterSectionHeight(_routerSignalsOpen, FilteredRouterSignals().Count, 24f);
            height += EstimateRouterSectionHeight(_routerStatesOpen, FilteredRouterStates().Count, 24f);
            height += EstimateRouterSectionHeight(_routerSourcesOpen, FilteredRouterSources().Count, 24f);
            return Mathf.Clamp(height, 72f, 1800f);
        }

        private static float EstimateRouterSectionHeight(bool open, int rowCount, float rowHeight)
        {
            if (!open)
                return 32f;

            return 58f + Mathf.Max(1, rowCount) * rowHeight;
        }

        private List<DebugRouter.ChannelSnapshot> FilteredRouterChannels()
        {
            List<DebugRouter.ChannelSnapshot> result = new List<DebugRouter.ChannelSnapshot>();
            for (int i = 0; i < _routerChannels.Count; i++)
            {
                DebugRouter.ChannelSnapshot row = _routerChannels[i];
                if (row != null && RouterMatchesSearch(row.channel, row.lastOwner, row.lastMessage))
                    result.Add(row);
            }
            return result;
        }

        private List<DebugRouter.SignalSnapshot> FilteredRouterSignals()
        {
            List<DebugRouter.SignalSnapshot> result = new List<DebugRouter.SignalSnapshot>();
            for (int i = 0; i < _routerSignals.Count; i++)
            {
                DebugRouter.SignalSnapshot row = _routerSignals[i];
                if (row != null && RouterMatchesSearch(row.signalName, row.lastOwner, row.lastDetails))
                    result.Add(row);
            }
            return result;
        }

        private List<DebugRouter.StateSnapshot> FilteredRouterStates()
        {
            List<DebugRouter.StateSnapshot> result = new List<DebugRouter.StateSnapshot>();
            for (int i = 0; i < _routerStates.Count; i++)
            {
                DebugRouter.StateSnapshot row = _routerStates[i];
                if (row != null && RouterMatchesSearch(row.stateName, row.ownerName, row.hierarchyPath, row.details))
                    result.Add(row);
            }
            return result;
        }

        private List<DebugRouter.SourceSnapshot> FilteredRouterSources()
        {
            List<DebugRouter.SourceSnapshot> result = new List<DebugRouter.SourceSnapshot>();
            for (int i = 0; i < _routerSources.Count; i++)
            {
                DebugRouter.SourceSnapshot row = _routerSources[i];
                if (row != null && RouterMatchesSearch(row.channel, row.ownerName, row.ownerType, row.hierarchyPath, row.lastMessage))
                    result.Add(row);
            }
            return result;
        }

        private bool RouterMatchesSearch(params string[] parts)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string needle = _search.Trim();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (!string.IsNullOrEmpty(part) && part.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string Shorten(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (maxChars < 4 || text.Length <= maxChars)
                return text;

            return text.Substring(0, maxChars - 1) + "…";
        }

        private void DrawSchedulePanel()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(_schedulePanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _scheduledPanelOpen = EditorGUILayout.Foldout(_scheduledPanelOpen, $"Scheduled Debug Actions ({_scheduledCalls.Count})", true, _sectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    DrawCountPill(_scheduledCalls.Count == 0 ? "No scheduled actions" : $"{_scheduledCalls.Count} queued", UtilityWindowTheme.Amber);

                    using (new EditorGUI.DisabledScope(_scheduledCalls.Count == 0))
                    {
                        if (DrawTintedButton("Start All", UtilityWindowTheme.Green, GUILayout.Width(76f)))
                        {
                            foreach (var call in _scheduledCalls)
                                if (call != null)
                                    call.enabled = true;
                            SaveScheduledCalls();
                        }

                        if (DrawTintedButton("Stop All", UtilityWindowTheme.Red, GUILayout.Width(72f)))
                        {
                            foreach (var call in _scheduledCalls)
                                if (call != null)
                                    call.enabled = false;
                            SaveScheduledCalls();
                        }

                        if (DrawTintedButton("Resolve", UtilityWindowTheme.Blue, GUILayout.Width(64f)))
                            ResolveScheduledReferences();

                        if (DrawTintedButton("Clear Null", UtilityWindowTheme.Neutral, GUILayout.Width(78f)))
                        {
                            _scheduledCalls.RemoveAll(c => c == null || c.target == null || string.IsNullOrEmpty(c.methodName));
                            SaveScheduledCalls();
                        }
                    }
                }

                if (!_scheduledPanelOpen)
                    return;

                if (_scheduledCalls.Count == 0)
                {
                    EditorGUILayout.HelpBox("Use the Schedule button beside a [ContextMenu] action to run it repeatedly. Conditions can reference any bool field/property on the action target or another component instance.", MessageType.None);
                    return;
                }

                float contentHeight = EstimateScheduleContentHeight();
                float scheduleHeight = Mathf.Clamp(Mathf.Min(_scheduledPanelHeight, contentHeight), 120f, Mathf.Max(120f, _scheduledPanelHeight));
                _scheduleScroll = EditorGUILayout.BeginScrollView(_scheduleScroll, GUILayout.MinHeight(110f), GUILayout.Height(scheduleHeight));
                for (int i = 0; i < _scheduledCalls.Count; i++)
                {
                    ScheduledCall call = _scheduledCalls[i];
                    if (call == null)
                        continue;

                    Color rowTint = call.enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral;
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(rowTint, 0.14f, 0.07f, 6, 4)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new GuiBackgroundScope(call.enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Red))
                                call.enabled = EditorGUILayout.Toggle(call.enabled, GUILayout.Width(18f));

                            call.foldout = EditorGUILayout.Foldout(call.foldout, call.displayName ?? call.methodName, true);
                            GUILayout.FlexibleSpace();

                            DrawCountPill($"#{call.fireCount}", call.enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 48f);
                            if (DrawTintedButton("Run", UtilityWindowTheme.Blue, GUILayout.Width(48f)) && call.target != null)
                            {
                                call.lastResult = $"{DateTime.Now:HH:mm:ss} - {InvokeContextAction(call.target, call.methodName)}";
                                call.fireCount++;
                                SaveScheduledCalls();
                            }
                            if (DrawTintedButton("X", UtilityWindowTheme.Red, GUILayout.Width(24f)))
                            {
                                _scheduledCalls.RemoveAt(i);
                                SaveScheduledCalls();
                                i--;
                                continue;
                            }
                        }

                        if (!call.foldout)
                        {
                            EditorGUILayout.LabelField($"Every {call.intervalSeconds:0.##}s | {call.conditionMode} | Last: {call.lastResult}", _mutedMiniLabelStyle);
                            continue;
                        }

                        EditorGUI.BeginChangeCheck();
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            call.target = EditorGUILayout.ObjectField("Target", call.target, typeof(UnityEngine.Object), true);
                            call.intervalSeconds = Mathf.Max(0.1f, EditorGUILayout.FloatField("Interval", call.intervalSeconds, GUILayout.MaxWidth(220f)));
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            call.conditionMode = (ConditionMode)EditorGUILayout.EnumPopup("Condition", call.conditionMode);
                            if (call.conditionMode == ConditionMode.BoolMemberTrue || call.conditionMode == ConditionMode.BoolMemberFalse)
                            {
                                call.conditionTarget = EditorGUILayout.ObjectField(call.conditionTarget != null ? call.conditionTarget : call.target, typeof(UnityEngine.Object), true, GUILayout.MaxWidth(180f));
                                UnityEngine.Object conditionTarget = call.conditionTarget != null ? call.conditionTarget : call.target;
                                DrawBoolMemberPopup(conditionTarget, ref call.conditionMemberName, GUILayout.MaxWidth(220f));
                            }
                        }

                        if (EditorGUI.EndChangeCheck())
                        {
                            CaptureScheduleIdentity(call);
                            call.nextRunTime = EditorApplication.timeSinceStartup + Mathf.Max(0.1f, call.intervalSeconds);
                            SaveScheduledCalls();
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Last: {call.lastResult}", _mutedMiniLabelStyle);
                            GUILayout.FlexibleSpace();
                            double remaining = Math.Max(0.0, call.nextRunTime - EditorApplication.timeSinceStartup);
                            DrawCountPill(call.enabled ? $"Next: {remaining:0.0}s" : "Paused", call.enabled ? UtilityWindowTheme.Green : UtilityWindowTheme.Red, 92f);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                if (contentHeight > 130f)
                {
                    UtilityWindowTheme.VerticalResizeHandle(
                        ref _scheduledPanelHeight,
                        120f,
                        560f,
                        () => UtilityWindowPrefs.SetFloat(ScheduleHeightPrefsKey, _scheduledPanelHeight),
                        "Drag to resize the scheduled action list area");
                }
            }
        }

        private void DrawStaticDebugFields()
        {
            if (!_showStaticDebugFields || _staticBoolToggles.Count == 0)
                return;

            List<StaticDebugFieldInfo> filtered = _staticBoolToggles
                .Where(info => info != null && info.field != null && MatchesSearch($"{info.declaringType.Name}.{info.field.Name}"))
                .OrderBy(info => info.declaringType.Name)
                .ThenBy(info => info.field.Name)
                .ToList();

            if (filtered.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(_staticPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _staticFieldsPanelOpen = EditorGUILayout.Foldout(_staticFieldsPanelOpen, $"Static debug/log toggles ({filtered.Count})", true, _sectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    DrawStaticScopeToggle("All", BoolToggleCategory.Any, 52f);
                    DrawStaticScopeToggle("Debug", BoolToggleCategory.Debug, 62f);
                    DrawStaticScopeToggle("Logs", BoolToggleCategory.Log, 56f);
                    DrawStaticScopeToggle("Gizmos", BoolToggleCategory.Gizmo, 64f);
                }

                if (!_staticFieldsPanelOpen)
                    return;

                float contentHeight = Mathf.Clamp(34f + filtered.Count * 24f, 70f, 900f);
                float height = Mathf.Clamp(Mathf.Min(_staticPanelHeight, contentHeight), 70f, Mathf.Max(70f, _staticPanelHeight));
                _staticScroll = EditorGUILayout.BeginScrollView(_staticScroll, GUILayout.Height(height));
                foreach (StaticDebugFieldInfo info in filtered)
                {
                    string label = $"{info.declaringType.Name}.{info.field.Name}";
                    BoolToggleCategory category = ClassifyBoolField(info.field);
                    bool oldValue = false;
                    try { oldValue = (bool)info.field.GetValue(null); }
                    catch { }

                    using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                    {
                        DrawCategoryPill(category, 78f);
                        EditorGUILayout.LabelField(label, _categoryLabelStyle);
                        using (new GuiBackgroundScope(oldValue ? DebugTint(category) : WithValue(DebugTint(category), 0.55f)))
                        {
                            bool newValue = GUILayout.Toggle(oldValue, oldValue ? "On" : "Off", EditorStyles.miniButton, GUILayout.Width(52f));
                            if (newValue != oldValue)
                            {
                                info.field.SetValue(null, newValue);
                                _status = $"Set {label} = {newValue}";
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                if (contentHeight > 90f)
                {
                    UtilityWindowTheme.VerticalResizeHandle(
                        ref _staticPanelHeight,
                        80f,
                        420f,
                        () => UtilityWindowPrefs.SetFloat(StaticHeightPrefsKey, _staticPanelHeight),
                        "Drag to resize the static debug fields list");
                }
            }
        }

        private void DrawDiscoveredComponentsPanel()
        {
            List<DebugComponentInfo> filtered = _components.Where(PassesFilters).ToList();

            using (new EditorGUILayout.VerticalScope(_componentPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _discoveredComponentsPanelOpen = EditorGUILayout.Foldout(_discoveredComponentsPanelOpen, $"Discovered Components ({filtered.Count})", true, _sectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    DrawCountPill($"{filtered.Count} visible", UtilityWindowTheme.Blue, 84f);
                    DrawCountPill($"{filtered.Sum(i => i.boolToggles.Count)} bools", UtilityWindowTheme.Cyan, 70f);
                    DrawCountPill($"{filtered.Sum(i => i.contextActions.Count)} actions", UtilityWindowTheme.Amber, 80f);
                }

                if (!_discoveredComponentsPanelOpen)
                    return;

                if (filtered.Count == 0)
                {
                    EditorGUILayout.HelpBox("No matching components were found. Adjust the search/filter controls or press Refresh after opening/changing scenes.", MessageType.None);
                    return;
                }

                float contentHeight = EstimateComponentsContentHeight(filtered);
                float height = Mathf.Clamp(Mathf.Min(_componentsPanelHeight, contentHeight), 120f, Mathf.Max(120f, _componentsPanelHeight));

                _componentsScroll = EditorGUILayout.BeginScrollView(_componentsScroll, GUILayout.MinHeight(110f), GUILayout.Height(height));
                if (_groupMode == GroupMode.ComponentType)
                    DrawGroupedByComponentType();
                else
                    DrawGroupedBySceneObject();
                EditorGUILayout.EndScrollView();

                if (contentHeight > 130f)
                {
                    UtilityWindowTheme.VerticalResizeHandle(
                        ref _componentsPanelHeight,
                        140f,
                        760f,
                        () => UtilityWindowPrefs.SetFloat(ComponentsHeightPrefsKey, _componentsPanelHeight),
                        "Drag to resize the discovered components list");
                }
            }
        }

        private float EstimateComponentsContentHeight(List<DebugComponentInfo> filtered)
        {
            if (filtered == null || filtered.Count == 0)
                return 90f;

            float height = 18f;
            if (_groupMode == GroupMode.ComponentType)
            {
                foreach (var group in filtered.GroupBy(info => info.component.GetType()))
                {
                    string key = group.Key.FullName;
                    height += 34f;
                    if (GetGroupBoolFoldout(key, false))
                        height += 34f + group.SelectMany(i => i.boolToggles).GroupBy(f => f.Name).Count() * 24f;
                    if (GetTypeFoldout(key, false))
                        height += group.Count() * 40f;
                }
            }
            else
            {
                foreach (var group in filtered.GroupBy(info => info.objectPath))
                {
                    height += 34f;
                    if (GetSceneObjectFoldout(group.Key, false))
                        height += group.Count() * 40f;
                }
            }

            return Mathf.Clamp(height, 90f, 2400f);
        }

        private void DrawGroupedByComponentType()
        {
            IEnumerable<IGrouping<Type, DebugComponentInfo>> groups = _components
                .Where(PassesFilters)
                .GroupBy(c => c.component.GetType())
                .OrderBy(g => g.Key.Name);

            foreach (IGrouping<Type, DebugComponentInfo> group in groups)
            {
                List<DebugComponentInfo> groupList = group.ToList();
                string key = group.Key.FullName;
                bool open = GetTypeFoldout(key, false);
                using (new EditorGUILayout.VerticalScope(_componentPanelStyle))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        open = EditorGUILayout.Foldout(open, $"{group.Key.Name} ({groupList.Count})", true);
                        SetTypeFoldout(key, open);
                        GUILayout.FlexibleSpace();
                        DrawScopeToggle("All", groupList, BoolToggleCategory.Any, 50f);
                        DrawScopeToggle("Debug", groupList, BoolToggleCategory.Debug, 62f);
                        DrawScopeToggle("Logs", groupList, BoolToggleCategory.Log, 56f);
                        DrawScopeToggle("Gizmos", groupList, BoolToggleCategory.Gizmo, 64f);
                        DrawScopeToggle("Other", groupList, BoolToggleCategory.Other, 58f);

                        bool fieldOpen = GetGroupBoolFoldout(key, false);
                        bool nextFieldOpen;
                        using (new GuiBackgroundScope(fieldOpen ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral))
                            nextFieldOpen = GUILayout.Toggle(fieldOpen, "Fields", EditorStyles.toolbarButton, GUILayout.Width(58f));
                        if (nextFieldOpen != fieldOpen)
                            SetGroupBoolFoldout(key, nextFieldOpen);
                    }

                    if (GetGroupBoolFoldout(key, false))
                        DrawGroupFieldControls(key, groupList);

                    if (!open)
                        continue;

                    foreach (DebugComponentInfo info in groupList.OrderBy(i => i.objectPath))
                        DrawComponentInfo(info);
                }
            }
        }

        private void DrawGroupedBySceneObject()
        {
            IEnumerable<IGrouping<string, DebugComponentInfo>> groups = _components
                .Where(PassesFilters)
                .GroupBy(c => c.objectPath)
                .OrderBy(g => g.Key);

            foreach (IGrouping<string, DebugComponentInfo> group in groups)
            {
                List<DebugComponentInfo> groupList = group.ToList();
                bool open = GetSceneObjectFoldout(group.Key, false);
                using (new EditorGUILayout.VerticalScope(_componentPanelStyle))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        open = EditorGUILayout.Foldout(open, $"{group.Key} ({groupList.Count} components)", true);
                        SetSceneObjectFoldout(group.Key, open);
                        GUILayout.FlexibleSpace();
                        DrawScopeToggle("All", groupList, BoolToggleCategory.Any, 50f);
                        DrawScopeToggle("Debug", groupList, BoolToggleCategory.Debug, 62f);
                        DrawScopeToggle("Logs", groupList, BoolToggleCategory.Log, 56f);
                        DrawScopeToggle("Gizmos", groupList, BoolToggleCategory.Gizmo, 64f);
                    }

                    if (!open)
                        continue;

                    foreach (DebugComponentInfo info in groupList.OrderBy(i => i.component.GetType().Name))
                        DrawComponentInfo(info);
                }
            }
        }

        private void DrawComponentInfo(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return;

            int id = info.component.GetInstanceID();
            bool open = GetInstanceFoldout(id, false);
            using (new EditorGUILayout.VerticalScope(_instancePanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    open = EditorGUILayout.Foldout(open, $"{info.component.GetType().Name} - {info.objectPath}", true);
                    SetInstanceFoldout(id, open);
                    GUILayout.FlexibleSpace();
                    DrawScopeToggle("All", new[] { info }, BoolToggleCategory.Any, 48f);
                    DrawScopeToggle("Debug", new[] { info }, BoolToggleCategory.Debug, 60f);
                    DrawScopeToggle("Logs", new[] { info }, BoolToggleCategory.Log, 54f);
                    DrawScopeToggle("Gizmos", new[] { info }, BoolToggleCategory.Gizmo, 62f);
                    if (DrawTintedButton("Ping", UtilityWindowTheme.Blue, GUILayout.Width(44f)))
                        EditorGUIUtility.PingObject(info.component);
                    if (DrawTintedButton("Select", UtilityWindowTheme.Teal, GUILayout.Width(54f)))
                        Selection.activeObject = info.component.gameObject;
                }

                if (!open)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(info.component, typeof(MonoBehaviour), true);
                }

                if (_showSnapshotButtons)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (DrawTintedButton("Copy Serialized Vars", UtilityWindowTheme.Cyan, GUILayout.Width(145f)))
                            CopySerializedSnapshot(info.component, debugOnly: false);
                        if (DrawTintedButton("Copy Debug Vars", DebugTint(BoolToggleCategory.Debug), GUILayout.Width(125f)))
                            CopySerializedSnapshot(info.component, debugOnly: true);
                        if (DrawTintedButton("Copy Full Field Snapshot", UtilityWindowTheme.Teal, GUILayout.Width(165f)))
                            CopyReflectionSnapshot(info.component);
                        if (DrawTintedButton("Copy Object Snapshot", UtilityWindowTheme.Purple, GUILayout.Width(150f)))
                            CopyObjectSnapshot(info.component.gameObject);
                    }
                }

                if (_showBoolToggles && info.boolToggles.Count > 0)
                {
                    bool boolsOpen = GetBoolSectionFoldout(id, true);
                    boolsOpen = EditorGUILayout.Foldout(boolsOpen, $"Debug / log / gizmo bools ({info.boolToggles.Count})", true, _sectionHeaderStyle);
                    SetBoolSectionFoldout(id, boolsOpen);
                    if (boolsOpen)
                        DrawBoolToggles(info);
                }

                if (_showContextActions && info.contextActions.Count > 0)
                {
                    bool actionsOpen = GetActionSectionFoldout(id, true);
                    actionsOpen = EditorGUILayout.Foldout(actionsOpen, $"Context actions ({info.contextActions.Count})", true, _sectionHeaderStyle);
                    SetActionSectionFoldout(id, actionsOpen);
                    if (actionsOpen)
                        DrawContextActions(info);
                }
            }
        }

        private void DrawBoolToggles(DebugComponentInfo info)
        {
            foreach (FieldInfo field in info.boolToggles.OrderBy(GetBoolSortKey))
            {
                bool oldValue = false;
                try { oldValue = (bool)field.GetValue(info.component); }
                catch { continue; }

                BoolToggleCategory category = ClassifyBoolField(field);
                using (new EditorGUILayout.HorizontalScope(_fieldPanelStyle))
                {
                    DrawCategoryPill(category, 78f);
                    EditorGUILayout.LabelField(new GUIContent(field.Name, field.DeclaringType.Name + "." + field.Name), _categoryLabelStyle);
                    using (new GuiBackgroundScope(oldValue ? DebugTint(category) : WithValue(DebugTint(category), 0.55f)))
                    {
                        bool next = GUILayout.Toggle(oldValue, oldValue ? "On" : "Off", EditorStyles.miniButton, GUILayout.Width(52f));
                        if (next != oldValue)
                        {
                            Undo.RecordObject(info.component, $"Set {field.Name}");
                            field.SetValue(info.component, next);
                            EditorUtility.SetDirty(info.component);
                            _status = $"Set {info.component.GetType().Name}.{field.Name} = {next}";
                        }
                    }
                }
            }
        }

        private void DrawContextActions(DebugComponentInfo info)
        {
            foreach (MethodInfo method in info.contextActions.OrderBy(GetContextMenuName))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = GetContextMenuName(method);
                    EditorGUILayout.LabelField(label, _categoryLabelStyle);
                    if (DrawTintedButton("Run", UtilityWindowTheme.Blue, GUILayout.Width(58f)))
                        _status = InvokeContextAction(info.component, method.Name);
                    if (DrawTintedButton("Schedule", UtilityWindowTheme.Amber, GUILayout.Width(78f)))
                        AddScheduledCall(info.component, method);
                }
            }
        }

        private void DrawMasterControls()
        {
            IEnumerable<DebugComponentInfo> scope = _masterControlsAffectFiltered ? _components.Where(PassesFilters) : _components;
            List<DebugComponentInfo> list = scope.Where(i => i != null && i.component != null).ToList();

            using (new EditorGUILayout.VerticalScope(_masterPanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(_masterControlsAffectFiltered ? $"Master toggles - filtered scope ({list.Count})" : $"Master toggles - whole scene ({list.Count})", _categoryLabelStyle, GUILayout.Width(240f));
                    using (new GuiBackgroundScope(_masterControlsAffectFiltered ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral))
                        _masterControlsAffectFiltered = GUILayout.Toggle(_masterControlsAffectFiltered, "Filtered only", EditorStyles.toolbarButton, GUILayout.Width(92f));
                    GUILayout.FlexibleSpace();
                    DrawScopeToggle("All", list, BoolToggleCategory.Any, 58f);
                    DrawScopeToggle("Debug", list, BoolToggleCategory.Debug, 70f);
                    DrawScopeToggle("Logs", list, BoolToggleCategory.Log, 62f);
                    DrawScopeToggle("Gizmos", list, BoolToggleCategory.Gizmo, 72f);
                    DrawScopeToggle("Other", list, BoolToggleCategory.Other, 66f);
                }
            }
        }

        private void DrawScopeToggle(string label, IEnumerable<DebugComponentInfo> infos, BoolToggleCategory category, float width)
        {
            List<DebugComponentInfo> list = infos != null ? infos.Where(i => i != null && i.component != null).ToList() : new List<DebugComponentInfo>();
            ToggleStats stats = GetToggleStats(list, category, null);
            using (new EditorGUI.DisabledScope(!stats.hasAny))
            {
                bool current = stats.allOn;
                EditorGUI.showMixedValue = stats.mixed;
                EditorGUI.BeginChangeCheck();
                Color tint = stats.mixed ? UtilityWindowTheme.Amber : current ? DebugTint(category) : WithValue(DebugTint(category), 0.5f);
                bool next;
                using (new GuiBackgroundScope(tint))
                    next = GUILayout.Toggle(current, new GUIContent(label, BuildToggleTooltip(category, stats)), EditorStyles.toolbarButton, GUILayout.Width(width));
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (changed)
                    SetBoolToggles(list, category, null, next);
            }
        }

        private void DrawStaticScopeToggle(string label, BoolToggleCategory category, float width)
        {
            ToggleStats stats = GetStaticToggleStats(category);
            using (new EditorGUI.DisabledScope(!stats.hasAny))
            {
                bool current = stats.allOn;
                EditorGUI.showMixedValue = stats.mixed;
                EditorGUI.BeginChangeCheck();
                Color tint = stats.mixed ? UtilityWindowTheme.Amber : current ? DebugTint(category) : WithValue(DebugTint(category), 0.5f);
                bool next;
                using (new GuiBackgroundScope(tint))
                    next = GUILayout.Toggle(current, new GUIContent(label, BuildToggleTooltip(category, stats)), EditorStyles.toolbarButton, GUILayout.Width(width));
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (changed)
                    SetStaticBoolToggles(category, next);
            }
        }

        private void DrawGroupFieldControls(string groupKey, List<DebugComponentInfo> groupList)
        {
            if (groupList == null || groupList.Count == 0)
                return;

            List<FieldInfo> fields = groupList
                .SelectMany(g => g.boolToggles)
                .GroupBy(f => f.Name)
                .Select(g => g.First())
                .OrderBy(GetBoolSortKey)
                .ToList();

            if (fields.Count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(_fieldPanelStyle))
            {
                EditorGUILayout.LabelField("Per-field controls for every visible instance of this component type", EditorStyles.miniBoldLabel);

                for (int i = 0; i < fields.Count; i++)
                {
                    FieldInfo field = fields[i];
                    ToggleStats stats = GetToggleStats(groupList, BoolToggleCategory.Any, field);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        BoolToggleCategory category = ClassifyBoolField(field);
                        DrawCategoryPill(category, 78f);
                        EditorGUI.showMixedValue = stats.mixed;
                        EditorGUI.BeginChangeCheck();
                        bool next;
                        Color tint = stats.mixed ? UtilityWindowTheme.Amber : stats.allOn ? DebugTint(category) : WithValue(DebugTint(category), 0.55f);
                        using (new GuiBackgroundScope(tint))
                            next = EditorGUILayout.ToggleLeft(new GUIContent(field.Name, BuildToggleTooltip(BoolToggleCategory.Any, stats)), stats.allOn);
                        bool changed = EditorGUI.EndChangeCheck();
                        EditorGUI.showMixedValue = false;

                        GUILayout.FlexibleSpace();
                        DrawCountPill($"{stats.onCount}/{stats.total} on", tint, 78f);

                        if (changed)
                            SetBoolToggles(groupList, BoolToggleCategory.Any, field, next);
                    }
                }
            }
        }

        private static string BuildToggleTooltip(BoolToggleCategory category, ToggleStats stats)
        {
            return $"{category}: {stats.onCount}/{stats.total} on. Click to apply this value to all matching toggles in this scope.";
        }

        private ToggleStats GetToggleStats(IEnumerable<DebugComponentInfo> infos, BoolToggleCategory category, FieldInfo specificField)
        {
            ToggleStats stats = new ToggleStats();
            if (infos == null)
                return stats;

            foreach (DebugComponentInfo info in infos)
            {
                if (info == null || info.component == null)
                    continue;

                foreach (FieldInfo field in info.boolToggles)
                {
                    if (!FieldMatches(field, category, specificField))
                        continue;

                    try
                    {
                        stats.total++;
                        object value = field.GetValue(info.component);
                        if (value is bool b && b)
                            stats.onCount++;
                    }
                    catch
                    {
                        // Ignore inaccessible/invalid fields; the serialized setter will be the source of truth when applying.
                    }
                }
            }

            return stats;
        }

        private ToggleStats GetStaticToggleStats(BoolToggleCategory category)
        {
            ToggleStats stats = new ToggleStats();
            foreach (StaticDebugFieldInfo info in _staticBoolToggles)
            {
                if (info == null || info.field == null || !FieldMatches(info.field, category, null))
                    continue;

                stats.total++;
                try
                {
                    if ((bool)info.field.GetValue(null))
                        stats.onCount++;
                }
                catch
                {
                    stats.total--;
                }
            }
            return stats;
        }

        private void SetBoolToggles(IEnumerable<DebugComponentInfo> infos, BoolToggleCategory category, FieldInfo specificField, bool value)
        {
            int changedCount = 0;
            if (infos == null)
                return;

            foreach (DebugComponentInfo info in infos)
            {
                if (info == null || info.component == null)
                    continue;

                bool undoRecorded = false;
                foreach (FieldInfo field in info.boolToggles)
                {
                    if (!FieldMatches(field, category, specificField))
                        continue;

                    try
                    {
                        bool current = (bool)field.GetValue(info.component);
                        if (current == value)
                            continue;

                        if (!undoRecorded)
                        {
                            Undo.RecordObject(info.component, $"Set {category} debug bools on {info.component.GetType().Name}");
                            undoRecorded = true;
                        }

                        field.SetValue(info.component, value);
                        changedCount++;
                    }
                    catch
                    {
                        // Ignore inaccessible/invalid fields.
                    }
                }

                if (undoRecorded)
                    EditorUtility.SetDirty(info.component);
            }

            _status = specificField != null
                ? $"Set {specificField.Name} = {value} on {changedCount} instances."
                : $"Set {category} toggles = {value} on {changedCount} fields.";
        }

        private void SetStaticBoolToggles(BoolToggleCategory category, bool value)
        {
            int changedCount = 0;
            foreach (StaticDebugFieldInfo info in _staticBoolToggles)
            {
                if (info == null || info.field == null || !FieldMatches(info.field, category, null))
                    continue;

                try
                {
                    if ((bool)info.field.GetValue(null) == value)
                        continue;

                    info.field.SetValue(null, value);
                    changedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DebugControlCenter] Could not set static toggle {info.declaringType.Name}.{info.field.Name}: {ex.Message}");
                }
            }

            _status = $"Set static {category} toggles = {value} on {changedCount} fields.";
        }

        private static bool FieldMatches(FieldInfo field, BoolToggleCategory category, FieldInfo specificField)
        {
            if (field == null)
                return false;

            if (specificField != null && !string.Equals(field.Name, specificField.Name, StringComparison.Ordinal))
                return false;

            if (category == BoolToggleCategory.Any)
                return true;

            return ClassifyBoolField(field) == category;
        }

        private void Refresh()
        {
            _components.Clear();
            _staticBoolToggles.Clear();

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                GameObject go = behaviour.gameObject;
                if (go == null)
                    continue;

                if (EditorUtility.IsPersistent(go))
                    continue;

                if (!_includeInactive && !go.activeInHierarchy)
                    continue;

                Type type = behaviour.GetType();
                var info = new DebugComponentInfo
                {
                    component = behaviour,
                    objectPath = GetTransformPath(go.transform)
                };

                info.boolToggles.AddRange(GetDebugBoolFields(type));
                info.contextActions.AddRange(GetContextMenuMethods(type));

                if (!_showOnlyDebuggable || info.boolToggles.Count > 0 || info.contextActions.Count > 0)
                    _components.Add(info);
            }

            ScanStaticDebugFields();
            ResolveScheduledReferences();

            _lastRefreshTime = EditorApplication.timeSinceStartup;
            _scanCacheDirty = false;
            _status = $"Found {_components.Count} components, {_staticBoolToggles.Count} static toggles.";
            RequestRepaintThrottled();
        }

        private bool PassesFilters(DebugComponentInfo info)
        {
            if (info == null || info.component == null)
                return false;

            if (!_includeInactive && !info.component.gameObject.activeInHierarchy)
                return false;

            if (_selectedHierarchyOnly && !IsUnderCurrentSelection(info.component.gameObject))
                return false;

            if (_showOnlyDebuggable && info.boolToggles.Count == 0 && info.contextActions.Count == 0)
                return false;

            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string s = _search.Trim();
            return info.component.GetType().Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                   || info.objectPath.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                   || info.boolToggles.Any(f => f.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 || ClassifyBoolField(f).ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                   || info.contextActions.Any(m => GetContextMenuName(m).IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void AddScheduledCall(UnityEngine.Object target, MethodInfo method)
        {
            if (target == null || method == null)
                return;

            CaptureObjectIdentity(target, out string targetId, out string targetPath, out string targetType);
            ScheduledCall existing = _scheduledCalls.FirstOrDefault(c => c != null && c.targetGlobalId == targetId && c.methodName == method.Name);
            if (existing != null)
            {
                existing.target = target;
                existing.enabled = true;
                existing.foldout = true;
                existing.displayName = $"{target.name}.{GetContextMenuName(method)}";
                existing.nextRunTime = EditorApplication.timeSinceStartup + Mathf.Max(0.1f, existing.intervalSeconds);
                SaveScheduledCalls();
                return;
            }

            ScheduledCall call = new ScheduledCall
            {
                target = target,
                targetGlobalId = targetId,
                targetScenePath = targetPath,
                targetTypeName = targetType,
                methodName = method.Name,
                displayName = $"{target.name}.{GetContextMenuName(method)}",
                intervalSeconds = 5f,
                nextRunTime = EditorApplication.timeSinceStartup + 5f,
                conditionMode = ConditionMode.PlayModeOnly
            };
            _scheduledCalls.Add(call);
            SaveScheduledCalls();
        }


        private void TickScheduledActions(double now)
        {
            if (_scheduledCalls.Count == 0)
                return;

            bool changed = false;
            for (int i = 0; i < _scheduledCalls.Count; i++)
            {
                ScheduledCall call = _scheduledCalls[i];
                if (call == null || !call.enabled || string.IsNullOrEmpty(call.methodName))
                    continue;

                if (call.target == null)
                    TryResolveScheduledCall(call);

                if (call.target == null)
                    continue;

                if (call.nextRunTime <= 0)
                    call.nextRunTime = now + Mathf.Max(0.1f, call.intervalSeconds);

                if (now < call.nextRunTime)
                    continue;

                call.nextRunTime = now + Mathf.Max(0.1f, call.intervalSeconds);

                if (!EvaluateCondition(call))
                    continue;

                string result = InvokeContextAction(call.target, call.methodName);
                call.fireCount++;
                call.lastResult = $"{DateTime.Now:HH:mm:ss} - {result}";
                changed = true;
            }

            if (changed)
            {
                SaveScheduledCalls();
                RequestRepaintThrottled();
            }
        }

        private float EstimateScheduleContentHeight()
        {
            float height = 8f;
            foreach (ScheduledCall call in _scheduledCalls)
            {
                if (call == null)
                    continue;

                height += call.foldout ? 150f : 46f;
            }
            return Mathf.Clamp(height, 120f, 1200f);
        }

        private bool MatchesSearch(string text)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            return !string.IsNullOrEmpty(text) && text.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadScheduledCalls()
        {
            _scheduledCalls.Clear();
            string json = UtilityWindowPrefs.GetString(SchedulePrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                ScheduledCallSaveData data = JsonUtility.FromJson<ScheduledCallSaveData>(json);
                if (data == null || data.calls == null)
                    return;

                foreach (ScheduledCallSave saved in data.calls)
                {
                    if (saved == null || string.IsNullOrEmpty(saved.methodName))
                        continue;

                    ScheduledCall call = new ScheduledCall
                    {
                        targetGlobalId = saved.targetGlobalId,
                        targetScenePath = saved.targetScenePath,
                        targetTypeName = saved.targetTypeName,
                        methodName = saved.methodName,
                        displayName = saved.displayName,
                        enabled = saved.enabled,
                        foldout = saved.foldout,
                        intervalSeconds = Mathf.Max(0.1f, saved.intervalSeconds),
                        conditionMode = (ConditionMode)Mathf.Clamp(saved.conditionMode, 0, Enum.GetValues(typeof(ConditionMode)).Length - 1),
                        conditionTargetGlobalId = saved.conditionTargetGlobalId,
                        conditionScenePath = saved.conditionScenePath,
                        conditionTargetTypeName = saved.conditionTargetTypeName,
                        conditionMemberName = saved.conditionMemberName,
                        fireCount = saved.fireCount,
                        lastResult = string.IsNullOrEmpty(saved.lastResult) ? "Never" : saved.lastResult,
                        nextRunTime = EditorApplication.timeSinceStartup + Mathf.Max(0.1f, saved.intervalSeconds)
                    };
                    _scheduledCalls.Add(call);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DebugControlWindow] Failed to load scheduled debug actions. " + ex.Message);
            }
        }

        private void SaveScheduledCalls()
        {
            ScheduledCallSaveData data = new ScheduledCallSaveData();
            foreach (ScheduledCall call in _scheduledCalls)
            {
                if (call == null || string.IsNullOrEmpty(call.methodName))
                    continue;

                CaptureScheduleIdentity(call);
                data.calls.Add(new ScheduledCallSave
                {
                    targetGlobalId = call.targetGlobalId,
                    targetScenePath = call.targetScenePath,
                    targetTypeName = call.targetTypeName,
                    methodName = call.methodName,
                    displayName = call.displayName,
                    enabled = call.enabled,
                    foldout = call.foldout,
                    intervalSeconds = call.intervalSeconds,
                    conditionMode = (int)call.conditionMode,
                    conditionTargetGlobalId = call.conditionTargetGlobalId,
                    conditionScenePath = call.conditionScenePath,
                    conditionTargetTypeName = call.conditionTargetTypeName,
                    conditionMemberName = call.conditionMemberName,
                    fireCount = call.fireCount,
                    lastResult = call.lastResult
                });
            }

            UtilityWindowPrefs.SetString(SchedulePrefsKey, JsonUtility.ToJson(data));
        }

        private void CaptureScheduleIdentity(ScheduledCall call)
        {
            if (call == null)
                return;

            if (call.target != null)
                CaptureObjectIdentity(call.target, out call.targetGlobalId, out call.targetScenePath, out call.targetTypeName);

            if (call.conditionTarget != null)
                CaptureObjectIdentity(call.conditionTarget, out call.conditionTargetGlobalId, out call.conditionScenePath, out call.conditionTargetTypeName);
        }

        private static void CaptureObjectIdentity(UnityEngine.Object obj, out string globalId, out string scenePath, out string typeName)
        {
            globalId = string.Empty;
            scenePath = string.Empty;
            typeName = string.Empty;

            if (obj == null)
                return;

            typeName = obj.GetType().AssemblyQualifiedName;
            Component component = obj as Component;
            GameObject go = obj as GameObject;
            Transform transform = component != null ? component.transform : (go != null ? go.transform : null);
            if (transform != null)
                scenePath = GetTransformPath(transform);

            try
            {
                globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            }
            catch
            {
                globalId = string.Empty;
            }
        }

        private void ResolveScheduledReferences()
        {
            foreach (ScheduledCall call in _scheduledCalls)
                TryResolveScheduledCall(call);
        }

        private bool TryResolveScheduledCall(ScheduledCall call)
        {
            if (call == null)
                return false;

            if (call.target == null)
                call.target = ResolveObject(call.targetGlobalId, call.targetScenePath, call.targetTypeName);

            if (call.conditionTarget == null)
                call.conditionTarget = ResolveObject(call.conditionTargetGlobalId, call.conditionScenePath, call.conditionTargetTypeName);

            return call.target != null;
        }

        private static UnityEngine.Object ResolveObject(string globalId, string scenePath, string typeName)
        {
            if (!string.IsNullOrEmpty(globalId))
            {
                try
                {
                    GlobalObjectId parsed;
                    if (GlobalObjectId.TryParse(globalId, out parsed))
                    {
                        UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed);
                        if (obj != null)
                            return obj;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(scenePath))
            {
                GameObject go = FindSceneObjectByPath(scenePath);
                if (go != null)
                {
                    Type type = ResolveType(typeName);
                    if (type == null || type == typeof(GameObject))
                        return go;

                    Component component = go.GetComponent(type);
                    if (component != null)
                        return component;
                }
            }

            return null;
        }

        private static GameObject FindSceneObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string[] parts = path.Split('/');
            if (parts.Length == 0)
                return null;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    if (roots[r].name != parts[0])
                        continue;

                    Transform current = roots[r].transform;
                    bool matched = true;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        Transform child = current.Find(parts[i]);
                        if (child == null)
                        {
                            matched = false;
                            break;
                        }
                        current = child;
                    }

                    if (matched)
                        return current.gameObject;
                }
            }

            return null;
        }

        private static Type ResolveType(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
                return null;

            Type type = Type.GetType(assemblyQualifiedName);
            if (type != null)
                return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(assemblyQualifiedName);
                if (type != null)
                    return type;
            }
            return null;
        }

        private bool EvaluateCondition(ScheduledCall call)
        {
            if (call == null)
                return false;

            switch (call.conditionMode)
            {
                case ConditionMode.Always:
                    return true;
                case ConditionMode.PlayModeOnly:
                    return EditorApplication.isPlaying;
                case ConditionMode.SelectedTarget:
                    return IsSelected(call.target);
                case ConditionMode.TargetEnabled:
                    return IsObjectEnabled(call.target);
                case ConditionMode.BoolMemberTrue:
                    return TryGetBoolMember(call.conditionTarget != null ? call.conditionTarget : call.target, call.conditionMemberName, out bool vTrue) && vTrue;
                case ConditionMode.BoolMemberFalse:
                    return TryGetBoolMember(call.conditionTarget != null ? call.conditionTarget : call.target, call.conditionMemberName, out bool vFalse) && !vFalse;
                default:
                    return true;
            }
        }

        private static bool IsSelected(UnityEngine.Object target)
        {
            if (target == null)
                return false;

            GameObject targetGo = null;
            if (target is GameObject go)
                targetGo = go;
            else if (target is Component c)
                targetGo = c.gameObject;

            if (targetGo == null || Selection.activeGameObject == null)
                return false;

            return Selection.activeGameObject == targetGo || Selection.activeGameObject.transform.IsChildOf(targetGo.transform) || targetGo.transform.IsChildOf(Selection.activeGameObject.transform);
        }

        private static bool IsUnderCurrentSelection(GameObject go)
        {
            if (go == null)
                return false;

            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
                return false;

            for (int i = 0; i < selected.Length; i++)
            {
                GameObject selectedGo = selected[i];
                if (selectedGo == null)
                    continue;

                if (go == selectedGo)
                    return true;

                if (go.transform.IsChildOf(selectedGo.transform))
                    return true;
            }

            return false;
        }

        private static bool IsObjectEnabled(UnityEngine.Object target)
        {
            if (target == null)
                return false;

            if (target is Behaviour b)
                return b.enabled && b.gameObject.activeInHierarchy;

            if (target is Component c)
                return c.gameObject.activeInHierarchy;

            if (target is GameObject go)
                return go.activeInHierarchy;

            return true;
        }

        private static string InvokeContextAction(UnityEngine.Object target, string methodName)
        {
            if (target == null)
                return "Target missing";

            MethodInfo method = FindMethod(target.GetType(), methodName);
            if (method == null)
                return $"Method not found: {methodName}";

            if (method.GetParameters().Length != 0)
                return $"Skipped parameterized method: {methodName}";

            try
            {
                Undo.RecordObject(target, $"Run {methodName}");
                method.Invoke(target, null);
                EditorUtility.SetDirty(target);
                return $"Ran {methodName}";
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogException(ex.InnerException ?? ex, target);
                return $"Exception: {(ex.InnerException != null ? ex.InnerException.Message : ex.Message)}";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, target);
                return $"Exception: {ex.Message}";
            }
        }

        private static MethodInfo FindMethod(Type type, string methodName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo method = t.GetMethod(methodName, InstanceFlags);
                if (method != null)
                    return method;
            }

            return null;
        }

        private static List<FieldInfo> GetDebugBoolFields(Type type)
        {
            var fields = new List<FieldInfo>();
            foreach (FieldInfo field in EnumerateFields(type, includeStatic: false))
            {
                if (field.FieldType != typeof(bool))
                    continue;

                if (!IsUnitySerializedField(field))
                    continue;

                if (LooksLikeDebugToggle(field))
                    fields.Add(field);
            }

            return fields;
        }

        private static List<MethodInfo> GetContextMenuMethods(Type type)
        {
            var methods = new List<MethodInfo>();
            foreach (MethodInfo method in EnumerateMethods(type, includeStatic: false))
            {
                if (method.GetParameters().Length != 0)
                    continue;

                if (method.GetCustomAttributes(typeof(ContextMenu), true).Length > 0)
                    methods.Add(method);
            }

            return methods;
        }

        private void ScanStaticDebugFields()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!IsProjectRuntimeAssembly(assembly))
                    continue;

                foreach (Type type in GetTypesSafely(assembly))
                {
                    if (type == null || type.IsGenericTypeDefinition)
                        continue;

                    foreach (FieldInfo field in type.GetFields(StaticFlags))
                    {
                        if (field.FieldType != typeof(bool))
                            continue;

                        if (!LooksLikeStaticDebugToggle(type, field))
                            continue;

                        _staticBoolToggles.Add(new StaticDebugFieldInfo { declaringType = type, field = field });
                    }
                }
            }

            _staticBoolToggles.Sort((a, b) => string.Compare($"{a.declaringType.Name}.{a.field.Name}", $"{b.declaringType.Name}.{b.field.Name}", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsProjectRuntimeAssembly(Assembly assembly)
        {
            string name = assembly != null ? assembly.GetName().Name : string.Empty;
            return string.Equals(name, "Assembly-CSharp", StringComparison.Ordinal)
                   || string.Equals(name, "Assembly-CSharp-firstpass", StringComparison.Ordinal);
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static IEnumerable<FieldInfo> EnumerateFields(Type type, bool includeStatic)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                BindingFlags flags = includeStatic ? StaticFlags : InstanceFlags;
                foreach (FieldInfo field in t.GetFields(flags | BindingFlags.DeclaredOnly))
                    yield return field;
            }
        }

        private static IEnumerable<MethodInfo> EnumerateMethods(Type type, bool includeStatic)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                BindingFlags flags = includeStatic ? StaticFlags : InstanceFlags;
                foreach (MethodInfo method in t.GetMethods(flags | BindingFlags.DeclaredOnly))
                    yield return method;
            }
        }

        private static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsLiteral || field.IsInitOnly)
                return false;

            if (field.IsNotSerialized)
                return false;

            if (field.IsPublic)
                return true;

            return field.GetCustomAttributes(typeof(SerializeField), true).Length > 0;
        }

        private static bool LooksLikeDebugToggle(FieldInfo field)
        {
            string name = field.Name ?? string.Empty;
            if (ContainsDebugToken(name))
                return true;

            if (name.IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (name.IndexOf("show", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (object attr in field.GetCustomAttributes(true))
            {
                if (attr is HeaderAttribute header && !string.IsNullOrEmpty(header.header) && ContainsDebugToken(header.header))
                    return true;

                if (attr is TooltipAttribute tooltip && !string.IsNullOrEmpty(tooltip.tooltip) && ContainsDebugToken(tooltip.tooltip))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeStaticDebugToggle(Type type, FieldInfo field)
        {
            if (field == null || field.FieldType != typeof(bool))
                return false;

            string full = $"{type.Name}.{field.Name}";
            return ContainsDebugToken(full) || field.Name.Equals("LogEvents", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsDebugToken(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("verbose", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("diagnostic", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("trace", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static BoolToggleCategory ClassifyBoolField(FieldInfo field)
        {
            if (field == null)
                return BoolToggleCategory.Other;

            string text = BuildFieldSearchText(field);
            if (text.IndexOf("gizmo", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Gizmo;

            if (text.IndexOf("draw", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Gizmo;

            if (text.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("verbose", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("trace", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Log;

            if (text.IndexOf("diagnostic", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("probe", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Diagnostic;

            if (text.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0)
                return BoolToggleCategory.Debug;

            return BoolToggleCategory.Other;
        }

        private static string BuildFieldSearchText(FieldInfo field)
        {
            var sb = new StringBuilder(field.Name ?? string.Empty);
            foreach (object attr in field.GetCustomAttributes(true))
            {
                if (attr is HeaderAttribute header && !string.IsNullOrEmpty(header.header))
                    sb.Append(' ').Append(header.header);
                else if (attr is TooltipAttribute tooltip && !string.IsNullOrEmpty(tooltip.tooltip))
                    sb.Append(' ').Append(tooltip.tooltip);
            }
            return sb.ToString();
        }

        private static string GetBoolSortKey(FieldInfo field)
        {
            BoolToggleCategory category = ClassifyBoolField(field);
            int categorySort = category == BoolToggleCategory.Gizmo ? 3 : category == BoolToggleCategory.Log ? 2 : category == BoolToggleCategory.Debug ? 1 : category == BoolToggleCategory.Diagnostic ? 4 : 5;
            return categorySort.ToString("00") + ":" + (field != null ? field.Name : string.Empty);
        }

        private static string GetContextMenuName(MethodInfo method)
        {
            if (method == null)
                return string.Empty;

            object[] attrs = method.GetCustomAttributes(typeof(ContextMenu), true);
            if (attrs.Length > 0 && attrs[0] is ContextMenu menu && !string.IsNullOrEmpty(menu.menuItem))
                return menu.menuItem;

            return ObjectNames.NicifyVariableName(method.Name);
        }

        private void DrawBoolMemberPopup(UnityEngine.Object target, ref string memberName, params GUILayoutOption[] options)
        {
            string[] names = GetBoolMemberNames(target).ToArray();
            if (names.Length == 0)
            {
                memberName = EditorGUILayout.TextField(memberName, options);
                return;
            }

            int index = Array.IndexOf(names, memberName);
            if (index < 0)
                index = 0;

            int next = EditorGUILayout.Popup(index, names, options);
            memberName = names[Mathf.Clamp(next, 0, names.Length - 1)];
        }

        private static IEnumerable<string> GetBoolMemberNames(UnityEngine.Object target)
        {
            if (target == null)
                yield break;

            Type type = target.GetType();
            var seen = new HashSet<string>();

            foreach (FieldInfo field in EnumerateFields(type, includeStatic: false))
            {
                if (field.FieldType != typeof(bool))
                    continue;

                if (seen.Add(field.Name))
                    yield return field.Name;
            }

            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (PropertyInfo property in t.GetProperties(InstanceFlags | BindingFlags.DeclaredOnly))
                {
                    if (property.PropertyType != typeof(bool))
                        continue;

                    if (property.GetIndexParameters().Length != 0)
                        continue;

                    MethodInfo getter = property.GetGetMethod(nonPublic: true);
                    if (getter == null)
                        continue;

                    if (seen.Add(property.Name))
                        yield return property.Name;
                }
            }
        }

        private static bool TryGetBoolMember(UnityEngine.Object target, string memberName, out bool value)
        {
            value = false;
            if (target == null || string.IsNullOrWhiteSpace(memberName))
                return false;

            Type type = target.GetType();

            foreach (FieldInfo field in EnumerateFields(type, includeStatic: false))
            {
                if (field.FieldType == typeof(bool) && string.Equals(field.Name, memberName, StringComparison.Ordinal))
                {
                    value = (bool)field.GetValue(target);
                    return true;
                }
            }

            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                PropertyInfo property = t.GetProperty(memberName, InstanceFlags | BindingFlags.DeclaredOnly);
                if (property == null || property.PropertyType != typeof(bool) || property.GetIndexParameters().Length != 0)
                    continue;

                MethodInfo getter = property.GetGetMethod(nonPublic: true);
                if (getter == null)
                    continue;

                value = (bool)getter.Invoke(target, null);
                return true;
            }

            return false;
        }

        private void CopySerializedSnapshot(UnityEngine.Object target, bool debugOnly)
        {
            if (target == null)
                return;

            string text = BuildSerializedSnapshot(target, debugOnly);
            EditorGUIUtility.systemCopyBuffer = text;
            _status = debugOnly ? $"Copied debug serialized vars for {target.name}." : $"Copied serialized vars for {target.name}.";
            Debug.Log($"[DebugControlCenter] Copied {(debugOnly ? "debug " : string.Empty)}serialized snapshot for {target.name} to clipboard.", target);
        }

        private void CopyReflectionSnapshot(UnityEngine.Object target)
        {
            if (target == null)
                return;

            string text = BuildReflectionSnapshot(target);
            EditorGUIUtility.systemCopyBuffer = text;
            _status = $"Copied full field snapshot for {target.name}.";
            Debug.Log($"[DebugControlCenter] Copied full field snapshot for {target.name} to clipboard.", target);
        }

        private void CopyObjectSnapshot(GameObject go)
        {
            if (go == null)
                return;

            var sb = new StringBuilder(8192);
            sb.AppendLine($"GameObject snapshot: {go.name}");
            sb.AppendLine($"Path: {GetTransformPath(go.transform)}");
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"ActiveSelf: {go.activeSelf}");
            sb.AppendLine($"ActiveInHierarchy: {go.activeInHierarchy}");
            sb.AppendLine($"Layer: {LayerMask.LayerToName(go.layer)} ({go.layer})");
            sb.AppendLine($"Tag: {go.tag}");
            sb.AppendLine();

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    sb.AppendLine($"Component[{i}] = <missing script>");
                    continue;
                }

                sb.AppendLine($"--- Component[{i}]: {component.GetType().Name} ---");
                if (component is MonoBehaviour mb)
                    sb.AppendLine(BuildSerializedSnapshot(mb, debugOnly: false));
                else
                    sb.AppendLine(component.ToString());
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            _status = $"Copied GameObject snapshot for {go.name}.";
            Debug.Log($"[DebugControlCenter] Copied GameObject snapshot for {go.name} to clipboard.", go);
        }

        private static string BuildSerializedSnapshot(UnityEngine.Object target, bool debugOnly)
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine($"Serialized snapshot: {target.name} ({target.GetType().Name})");
            if (target is Component component)
                sb.AppendLine($"Path: {GetTransformPath(component.transform)}");
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(debugOnly ? "Mode: debug/log/gizmo-looking serialized properties only" : "Mode: all visible serialized properties");
            sb.AppendLine();

            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyPath == "m_Script")
                    continue;

                if (debugOnly && !ContainsDebugToken(prop.displayName) && !ContainsDebugToken(prop.name) && !ContainsDebugToken(prop.propertyPath))
                    continue;

                sb.AppendLine($"{prop.propertyPath} = {SerializedPropertyToString(prop)}");
            }

            return sb.ToString();
        }

        private static string BuildReflectionSnapshot(UnityEngine.Object target)
        {
            var sb = new StringBuilder(8192);
            sb.AppendLine($"Full field snapshot: {target.name} ({target.GetType().Name})");
            if (target is Component component)
                sb.AppendLine($"Path: {GetTransformPath(component.transform)}");
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("Mode: instance fields via reflection. Static fields, compiler fields, delegates, and events are skipped.");
            sb.AppendLine();

            foreach (FieldInfo field in EnumerateFields(target.GetType(), includeStatic: false).OrderBy(f => f.MetadataToken))
            {
                if (field.IsStatic || field.IsLiteral)
                    continue;

                if (field.Name.Contains("k__BackingField"))
                    continue;

                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    continue;

                try
                {
                    object value = field.GetValue(target);
                    string prefix = field.IsPublic ? "public" : field.IsFamily ? "protected" : "private";
                    string serialized = IsUnitySerializedField(field) ? "serialized" : "runtime";
                    sb.AppendLine($"{prefix} {serialized} {field.FieldType.Name} {field.Name} = {FormatValue(value)}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"{field.FieldType.Name} {field.Name} = <error: {ex.Message}>");
                }
            }

            return sb.ToString();
        }

        private static string SerializedPropertyToString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("0.#####");
                case SerializedPropertyType.String:
                    return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})" : "null";
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString("F3");
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString("F3");
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString("F3");
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Character:
                    return prop.intValue.ToString();
                case SerializedPropertyType.AnimationCurve:
                    return $"AnimationCurve(keys={prop.animationCurveValue?.keys.Length ?? 0})";
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.eulerAngles.ToString("F3");
                case SerializedPropertyType.ExposedReference:
                    return prop.exposedReferenceValue != null ? prop.exposedReferenceValue.name : "null";
                case SerializedPropertyType.FixedBufferSize:
                    return prop.fixedBufferSize.ToString();
                case SerializedPropertyType.Vector2Int:
                    return prop.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return prop.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return prop.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return prop.boundsIntValue.ToString();
                default:
                    if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
                        return $"Array(size={prop.arraySize})";
                    return $"<{prop.propertyType}>";
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string s)
                return $"\"{s}\"";

            if (value is UnityEngine.Object obj)
                return obj != null ? $"{obj.name} ({obj.GetType().Name})" : "null UnityObject";

            if (value is Vector2 v2)
                return v2.ToString("F3");

            if (value is Vector3 v3)
                return v3.ToString("F3");

            if (value is Vector4 v4)
                return v4.ToString("F3");

            if (value is Quaternion q)
                return q.eulerAngles.ToString("F3");

            if (value is Color c)
                return c.ToString();

            if (value is IDictionary dict)
                return $"Dictionary(count={dict.Count})";

            if (value is IEnumerable enumerable && !(value is string))
            {
                var items = new List<string>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count < 8)
                        items.Add(FormatValueBrief(item));
                    count++;
                }
                return $"Enumerable(count={count}, first=[{string.Join(", ", items)}])";
            }

            return value.ToString();
        }

        private static string FormatValueBrief(object value)
        {
            if (value == null)
                return "null";

            if (value is UnityEngine.Object obj)
                return obj != null ? obj.name : "null UnityObject";

            if (value is Vector3 v3)
                return v3.ToString("F2");

            if (value is Vector2 v2)
                return v2.ToString("F2");

            string text = value.ToString();
            if (text.Length > 48)
                text = text.Substring(0, 48) + "…";
            return text;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<no transform>";

            var names = new Stack<string>();
            Transform t = transform;
            while (t != null)
            {
                names.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private bool GetTypeFoldout(string key, bool defaultValue)
        {
            if (!_typeFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _typeFoldouts[key] = value;
            }
            return value;
        }

        private void SetTypeFoldout(string key, bool value)
        {
            _typeFoldouts[key] = value;
        }

        private bool GetInstanceFoldout(int key, bool defaultValue)
        {
            if (!_instanceFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _instanceFoldouts[key] = value;
            }
            return value;
        }

        private void SetInstanceFoldout(int key, bool value)
        {
            _instanceFoldouts[key] = value;
        }

        private bool GetSceneObjectFoldout(string key, bool defaultValue)
        {
            if (!_sceneObjectFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _sceneObjectFoldouts[key] = value;
            }
            return value;
        }

        private void SetSceneObjectFoldout(string key, bool value)
        {
            _sceneObjectFoldouts[key] = value;
        }

        private bool GetGroupBoolFoldout(string key, bool defaultValue)
        {
            if (!_groupBoolFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _groupBoolFoldouts[key] = value;
            }
            return value;
        }

        private void SetGroupBoolFoldout(string key, bool value)
        {
            _groupBoolFoldouts[key] = value;
        }

        private bool GetBoolSectionFoldout(int key, bool defaultValue)
        {
            if (!_boolSectionFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _boolSectionFoldouts[key] = value;
            }
            return value;
        }

        private void SetBoolSectionFoldout(int key, bool value)
        {
            _boolSectionFoldouts[key] = value;
        }

        private bool GetActionSectionFoldout(int key, bool defaultValue)
        {
            if (!_actionSectionFoldouts.TryGetValue(key, out bool value))
            {
                value = defaultValue;
                _actionSectionFoldouts[key] = value;
            }
            return value;
        }

        private void SetActionSectionFoldout(int key, bool value)
        {
            _actionSectionFoldouts[key] = value;
        }

        private void RequestRepaintThrottled()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextAllowedRepaintTime)
                return;

            _nextAllowedRepaintTime = now + RepaintThrottleInterval;
            Repaint();
        }

        private void SetAllFoldouts(bool value)
        {
            foreach (DebugComponentInfo info in _components)
            {
                if (info == null || info.component == null)
                    continue;

                _typeFoldouts[info.component.GetType().FullName] = value;
                _instanceFoldouts[info.component.GetInstanceID()] = value;
                _sceneObjectFoldouts[info.objectPath] = value;
                _groupBoolFoldouts[info.component.GetType().FullName] = value;
                _boolSectionFoldouts[info.component.GetInstanceID()] = value;
                _actionSectionFoldouts[info.component.GetInstanceID()] = value;
            }

            _typeFoldouts["__STATIC_DEBUG_FIELDS__"] = value;
            _routerPanelOpen = value;
            _routerChannelsOpen = value;
            _routerSignalsOpen = value;
            _routerStatesOpen = value;
            _routerSourcesOpen = value;
            _scheduledPanelOpen = value;
            _staticFieldsPanelOpen = value;
            _discoveredComponentsPanelOpen = value;
            foreach (ScheduledCall call in _scheduledCalls)
            {
                if (call != null)
                    call.foldout = value;
            }
        }
    }
    #endif

}