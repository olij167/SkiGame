using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    internal sealed class PungentStickyNoteOverlayTrayWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.StickyNotesOverlay.";
        private const string PrefFocusMode = PrefPrefix + "FocusMode";
        private const string PrefCollapsed = PrefPrefix + "Collapsed";
        private const string PrefHasPlacement = PrefPrefix + "HasPlacement";
        private const string PrefX = PrefPrefix + "X";
        private const string PrefY = PrefPrefix + "Y";
        private const string PrefWidth = PrefPrefix + "Width";
        private const string PrefHeight = PrefPrefix + "Height";
        private const double FocusGraceSeconds = 0.65d;
        private const double AttentionPulseSeconds = 0.55d;
        private const float Margin = 12f;

        internal const float HeaderHeight = 30f;

        private static readonly string[] FocusModeLabels =
        {
            "Auto Collapse",
            "Stay Open",
            "Auto Hide"
        };

        private static GUIStyle _headerStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _toolbarButtonStyle;

        private Vector2 _headerDragStartScreenPosition;
        private Rect _headerDragStartWindowRect;
        private bool _headerDragging;
        private bool _headerDragMoved;
        private double _menuGuardUntil;
        private double _attentionUntil;

        internal static PungentStickyNoteOverlayTrayWindow Instance { get; private set; }

        internal double LastInteractionTime { get; private set; }

        internal bool IsInteracting => _headerDragging || EditorApplication.timeSinceStartup < _menuGuardUntil;

        internal static bool IsMouseOver => Instance != null && EditorWindow.mouseOverWindow == Instance;

        private static MinimizedOverlayFocusMode FocusMode
        {
            get
            {
                int raw = UtilityWindowPrefs.GetInt(PrefFocusMode, (int)MinimizedOverlayFocusMode.AutoCollapse);
                return Enum.IsDefined(typeof(MinimizedOverlayFocusMode), raw)
                    ? (MinimizedOverlayFocusMode)raw
                    : MinimizedOverlayFocusMode.AutoCollapse;
            }
            set
            {
                UtilityWindowPrefs.SetInt(PrefFocusMode, (int)value);
                if (value == MinimizedOverlayFocusMode.StayOpen)
                    SetCollapsed(false);
                RefreshSize();
            }
        }

        private static bool Collapsed
        {
            get { return UtilityWindowPrefs.GetBool(PrefCollapsed, false); }
            set { SetCollapsed(value); }
        }

        internal static void ShowForActiveState(Rect activatorRect)
        {
            if (!PungentStickyNoteOverlayController.HasFloatingOverlay)
                return;

            SetCollapsed(false);
            PungentStickyNoteOverlayTrayWindow window = FindOpenInstanceAndCloseDuplicates();
            Rect rect = window != null
                ? GetRectForCurrentState(window.position)
                : GetRectForOpen(activatorRect);

            if (window == null)
            {
                window = CreateInstance<PungentStickyNoteOverlayTrayWindow>();
                window.titleContent = new GUIContent("Sticky Notes Overlay");
                window.ApplyOverlayRect(rect, true);
                window.ShowPopup();
            }

            window.ApplyOverlayRect(rect, true);
            window.FocusAndPulse(false);
            window.Repaint();
        }

        internal static void CloseIfOpen()
        {
            PungentStickyNoteOverlayTrayWindow window = Instance;
            if (window != null)
                window.Close();
        }

        internal static void RepaintIfOpen()
        {
            PungentStickyNoteOverlayTrayWindow window = Instance;
            if (window != null)
                window.Repaint();
        }

        internal static void TickFocusMode()
        {
            PungentStickyNoteOverlayTrayWindow window = Instance;
            if (window == null)
                return;

            if (!PungentStickyNoteOverlayController.HasFloatingOverlay)
            {
                window.Close();
                return;
            }

            if (IsOverlayActive(window))
                return;

            switch (FocusMode)
            {
                case MinimizedOverlayFocusMode.AutoCollapse:
                    SetCollapsed(true);
                    break;
                case MinimizedOverlayFocusMode.AutoHide:
                    PungentStickyNoteOverlayController.Close(PungentStickyNoteOverlayController.ActiveOwner);
                    break;
            }
        }

        private static PungentStickyNoteOverlayTrayWindow FindOpenInstanceAndCloseDuplicates()
        {
            PungentStickyNoteOverlayTrayWindow[] windows = Resources.FindObjectsOfTypeAll<PungentStickyNoteOverlayTrayWindow>();
            PungentStickyNoteOverlayTrayWindow primary = Instance;
            if (primary == null || Array.IndexOf(windows, primary) < 0)
                primary = windows.Length > 0 ? windows[0] : null;

            for (int i = 0; i < windows.Length; i++)
            {
                PungentStickyNoteOverlayTrayWindow window = windows[i];
                if (window != null && window != primary)
                    window.Close();
            }

            Instance = primary;
            return primary;
        }

        private void OnEnable()
        {
            Instance = this;
            wantsMouseMove = true;
            titleContent = new GUIContent("Sticky Notes Overlay");
            LastInteractionTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnGUI()
        {
            EnsureStyles();
            TrackInteraction(Event.current);

            if (!PungentStickyNoteOverlayController.ValidateFloatingOverlay())
            {
                EditorApplication.delayCall += Close;
                return;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                PungentStickyNoteOverlayController.Close(PungentStickyNoteOverlayController.ActiveOwner);
                Event.current.Use();
                GUIUtility.ExitGUI();
            }

            Rect full = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(full, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.13f, 0.985f) : new Color(0.82f, 0.82f, 0.84f, 0.985f));

            Rect headerRect = new Rect(0f, 0f, position.width, HeaderHeight);
            DrawHeader(headerRect);

            if (Collapsed)
                return;

            Rect bodyRect = new Rect(6f, headerRect.yMax + 6f, position.width - 12f, Mathf.Max(12f, position.height - HeaderHeight - 12f));
            PungentStickyNoteOverlayController.DrawFloating(bodyRect);
        }

        private void DrawHeader(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _headerStyle);
            if (EditorApplication.timeSinceStartup < _attentionUntil)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                    ? new Color(0.34f, 0.56f, 0.95f, 0.28f)
                    : new Color(0.20f, 0.43f, 0.90f, 0.24f));
                Repaint();
            }

            Rect toggleRect = new Rect(rect.x + 4f, rect.y + 4f, 28f, rect.height - 8f);
            Rect closeRect = new Rect(rect.xMax - 30f, rect.y + 4f, 26f, rect.height - 8f);
            Rect browserRect = new Rect(closeRect.x - 62f, rect.y + 4f, 58f, rect.height - 8f);
            Rect menuRect = new Rect(browserRect.x - 38f, rect.y + 4f, 34f, rect.height - 8f);
            Rect titleRect = new Rect(toggleRect.xMax + 4f, rect.y + 3f, Mathf.Max(40f, menuRect.x - toggleRect.xMax - 8f), rect.height - 6f);

            if (GUI.Button(toggleRect, new GUIContent(Collapsed ? ">" : "v", "Expand or collapse the Sticky Notes overlay."), _toolbarButtonStyle))
            {
                ToggleCollapsed();
                GUIUtility.ExitGUI();
            }

            HandleHeaderDrag(titleRect, Event.current);
            GUI.Label(titleRect, new GUIContent(PungentStickyNoteOverlayController.GetFloatingTitle(), "Drag to move. Click to collapse or expand."), _titleStyle);

            if (GUI.Button(menuRect, new GUIContent("...", "Sticky Notes overlay actions and settings."), _toolbarButtonStyle))
            {
                GuardMenuFocus();
                ShowMenu();
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(browserRect, new GUIContent("Browser", "Open the full Sticky Notes browser."), _toolbarButtonStyle))
            {
                PungentStickyNoteOverlayController.OpenFullBrowserForActive();
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(closeRect, new GUIContent("x", "Close this Sticky Notes overlay."), _toolbarButtonStyle))
            {
                PungentStickyNoteOverlayController.Close(PungentStickyNoteOverlayController.ActiveOwner);
                GUIUtility.ExitGUI();
            }
        }

        private void ShowMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent(Collapsed ? "Expand Overlay" : "Collapse To Header"), false, ToggleCollapsed);
            menu.AddItem(new GUIContent("Reset Overlay Position"), false, ResetPlacement);
            menu.AddSeparator(string.Empty);
            AddFocusMode(menu, MinimizedOverlayFocusMode.AutoCollapse, "Focus Behavior/Auto Collapse");
            AddFocusMode(menu, MinimizedOverlayFocusMode.StayOpen, "Focus Behavior/Stay Open");
            AddFocusMode(menu, MinimizedOverlayFocusMode.AutoHide, "Focus Behavior/Auto Hide");
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Full Browser"), false, PungentStickyNoteOverlayController.OpenFullBrowserForActive);

            string copyValue = PungentStickyNoteOverlayController.GetActiveCopyValue();
            if (string.IsNullOrWhiteSpace(copyValue))
                menu.AddDisabledItem(new GUIContent("Copy ID / References"));
            else
                menu.AddItem(new GUIContent("Copy ID / References"), false, () => EditorGUIUtility.systemCopyBuffer = copyValue);

            PungentAuthoringOverlayPerformance.AddMenuItems(menu);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Close"), false, () => PungentStickyNoteOverlayController.Close(PungentStickyNoteOverlayController.ActiveOwner));
            menu.ShowAsContext();
        }

        private static void AddFocusMode(GenericMenu menu, MinimizedOverlayFocusMode mode, string path)
        {
            menu.AddItem(new GUIContent(path), FocusMode == mode, () => FocusMode = mode);
        }

        private void HandleHeaderDrag(Rect rect, Event evt)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        _headerDragging = true;
                        _headerDragMoved = false;
                        _headerDragStartScreenPosition = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        _headerDragStartWindowRect = position;
                        MarkInteraction();
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId && _headerDragging)
                    {
                        Vector2 currentScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        Vector2 delta = currentScreen - _headerDragStartScreenPosition;
                        if (delta.sqrMagnitude > 9f)
                            _headerDragMoved = true;
                        ApplyOverlayRect(new Rect(_headerDragStartWindowRect.x + delta.x, _headerDragStartWindowRect.y + delta.y, _headerDragStartWindowRect.width, _headerDragStartWindowRect.height), true);
                        MarkInteraction();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && _headerDragging)
                    {
                        GUIUtility.hotControl = 0;
                        _headerDragging = false;
                        if (!_headerDragMoved)
                            ToggleCollapsed();
                        _headerDragMoved = false;
                        MarkInteraction();
                        evt.Use();
                    }
                    break;
            }
        }

        private void ApplyOverlayRect(Rect rect, bool persist)
        {
            Rect clamped = ClampRect(rect);
            position = clamped;
            minSize = new Vector2(clamped.width, clamped.height);
            maxSize = new Vector2(clamped.width, clamped.height);
            if (persist)
                SavePlacement(clamped);
        }

        private static void ToggleCollapsed()
        {
            SetCollapsed(!Collapsed);
        }

        private static void SetCollapsed(bool collapsed)
        {
            if (UtilityWindowPrefs.GetBool(PrefCollapsed, false) == collapsed)
                return;

            UtilityWindowPrefs.SetBool(PrefCollapsed, collapsed);
            RefreshSize();
        }

        private static void RefreshSize()
        {
            PungentStickyNoteOverlayTrayWindow window = Instance;
            if (window != null)
                window.ApplyOverlayRect(GetRectForCurrentState(window.position), true);
        }

        private static void ResetPlacement()
        {
            EditorPrefs.DeleteKey(PrefHasPlacement);
            EditorPrefs.DeleteKey(PrefX);
            EditorPrefs.DeleteKey(PrefY);
            EditorPrefs.DeleteKey(PrefWidth);
            EditorPrefs.DeleteKey(PrefHeight);
            RefreshSize();
        }

        private static Rect GetRectForCurrentState(Rect current)
        {
            Vector2 size = PungentStickyNoteOverlayController.GetFloatingPreferredSize(Collapsed);
            return ClampRect(new Rect(current.x, current.y, size.x, size.y));
        }

        private static Rect GetRectForOpen(Rect activatorRect)
        {
            if (TryGetSavedRect(out Rect saved))
                return GetRectForCurrentState(saved);

            Rect main = PungentUtilityMinimizer.GetMainEditorWindowRectForMinimizedUtilities();
            Vector2 size = PungentStickyNoteOverlayController.GetFloatingPreferredSize(Collapsed);
            return ClampRect(new Rect(main.xMax - size.x - 18f, main.y + 78f, size.x, size.y));
        }

        private static bool TryGetSavedRect(out Rect rect)
        {
            rect = default;
            if (!UtilityWindowPrefs.GetBool(PrefHasPlacement, false))
                return false;

            rect = new Rect(
                UtilityWindowPrefs.GetFloat(PrefX, 0f),
                UtilityWindowPrefs.GetFloat(PrefY, 0f),
                UtilityWindowPrefs.GetFloat(PrefWidth, 460f),
                UtilityWindowPrefs.GetFloat(PrefHeight, 360f));
            return rect.width > 1f && rect.height > 1f && !float.IsNaN(rect.x) && !float.IsNaN(rect.y);
        }

        private static Rect ClampRect(Rect rect)
        {
            Rect main = PungentUtilityMinimizer.GetMainEditorWindowRectForMinimizedUtilities();
            float usableLeft = main.x + Margin;
            float usableRight = main.xMax - Margin;
            float usableTop = main.y + Margin;
            float usableBottom = main.yMax - Margin;
            Vector2 desired = PungentStickyNoteOverlayController.GetFloatingPreferredSize(Collapsed);
            float width = Mathf.Min(desired.x, Mathf.Max(260f, usableRight - usableLeft));
            float height = Mathf.Min(desired.y, Mathf.Max(HeaderHeight, usableBottom - usableTop));
            float x = Mathf.Clamp(rect.x, usableLeft, Mathf.Max(usableLeft, usableRight - width));
            float y = Mathf.Clamp(rect.y, usableTop, Mathf.Max(usableTop, usableBottom - height));
            return new Rect(x, y, width, height);
        }

        private static void SavePlacement(Rect rect)
        {
            UtilityWindowPrefs.SetBool(PrefHasPlacement, true);
            UtilityWindowPrefs.SetFloat(PrefX, rect.x);
            UtilityWindowPrefs.SetFloat(PrefY, rect.y);
            UtilityWindowPrefs.SetFloat(PrefWidth, rect.width);
            UtilityWindowPrefs.SetFloat(PrefHeight, rect.height);
        }

        private static bool IsOverlayActive(PungentStickyNoteOverlayTrayWindow window)
        {
            if (window == null)
                return false;

            if (EditorWindow.focusedWindow == window || EditorWindow.mouseOverWindow == window)
                return true;

            if (window.IsInteracting)
                return true;

            return EditorApplication.timeSinceStartup - window.LastInteractionTime < FocusGraceSeconds;
        }

        private void TrackInteraction(Event evt)
        {
            if (evt == null)
                return;

            if (evt.type == EventType.MouseMove ||
                evt.type == EventType.MouseDown ||
                evt.type == EventType.MouseDrag ||
                evt.type == EventType.ScrollWheel ||
                evt.type == EventType.KeyDown)
                MarkInteraction();
        }

        private void GuardMenuFocus()
        {
            MarkInteraction();
            _menuGuardUntil = EditorApplication.timeSinceStartup + 1.25d;
        }

        private void FocusAndPulse(bool pulse = true)
        {
            Focus();
            MarkInteraction();
            if (pulse)
                _attentionUntil = EditorApplication.timeSinceStartup + AttentionPulseSeconds;
        }

        private void MarkInteraction()
        {
            LastInteractionTime = EditorApplication.timeSinceStartup;
        }

        private static void EnsureStyles()
        {
            if (_headerStyle != null)
                return;

            _headerStyle = new GUIStyle(EditorStyles.toolbar)
            {
                fixedHeight = 0f,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 0, 0)
            };
            _toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }
    }

    internal static class PungentAuthoringOverlayPerformance
    {
        private const string PrefEnabled = "PungentFunkUtilities.StickyNotesOverlay.Performance.Enabled";
        private const int MaxSamples = 48;

        private static readonly Dictionary<string, RollingSample> Samples = new Dictionary<string, RollingSample>(StringComparer.OrdinalIgnoreCase);

        private sealed class RollingSample
        {
            public readonly Queue<double> values = new Queue<double>();
            public double total;
            public double maxRecent;
            public int lastVisibleRows;

            public double Average
            {
                get { return values.Count == 0 ? 0d : total / values.Count; }
            }

            public void Add(double milliseconds, int visibleRows)
            {
                values.Enqueue(milliseconds);
                total += milliseconds;
                if (values.Count > MaxSamples)
                    total -= values.Dequeue();

                maxRecent = 0d;
                foreach (double value in values)
                    if (value > maxRecent)
                        maxRecent = value;

                lastVisibleRows = visibleRows;
            }
        }

        public static bool Enabled
        {
            get { return UtilityWindowPrefs.GetBool(PrefEnabled, false); }
            set { UtilityWindowPrefs.SetBool(PrefEnabled, value); }
        }

        public static bool DeveloperVisible
        {
            get { return PungentFunk.Utilities.Editor.Developer.PungentDeveloperMode.Available && PungentFunk.Utilities.Editor.Developer.PungentDeveloperMode.Enabled; }
        }

        public static long BeginSample()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        public static void EndSample(string name, long startTicks, int visibleRows = 0)
        {
            if (!Enabled || startTicks <= 0L || string.IsNullOrWhiteSpace(name))
                return;

            double ms = (Stopwatch.GetTimestamp() - startTicks) * 1000d / Stopwatch.Frequency;
            if (!Samples.TryGetValue(name, out RollingSample sample))
            {
                sample = new RollingSample();
                Samples[name] = sample;
            }

            sample.Add(ms, visibleRows);
        }

        public static void DrawToolbarReadout(string name, float width = 150f)
        {
            if (!Enabled || !DeveloperVisible || string.IsNullOrWhiteSpace(name))
                return;

            if (!Samples.TryGetValue(name, out RollingSample sample))
                return;

            string label = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.0}ms avg / {1:0.0}ms max",
                sample.Average,
                sample.maxRecent);
            EditorGUILayout.LabelField(new GUIContent(label, "Developer-only overlay performance sample."), UtilityWindowTheme.PathLabelStyle, GUILayout.Width(width));
        }

        public static void AddMenuItems(GenericMenu menu)
        {
            if (menu == null || !DeveloperVisible)
                return;

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Developer/Overlay Performance/Enabled"), Enabled, () => Enabled = !Enabled);
            if (!Enabled || Samples.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Developer/Overlay Performance/No Samples Yet"));
                return;
            }

            menu.AddItem(new GUIContent("Developer/Overlay Performance/Copy Summary"), false, () => EditorGUIUtility.systemCopyBuffer = Summary());
        }

        private static string Summary()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, RollingSample> pair in Samples)
            {
                RollingSample sample = pair.Value;
                builder.Append(pair.Key)
                    .Append(": avg ")
                    .Append(sample.Average.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                    .Append("ms, max ")
                    .Append(sample.maxRecent.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                    .Append("ms, visible rows ")
                    .Append(sample.lastVisibleRows)
                    .AppendLine();
            }

            return builder.ToString();
        }
    }
#endif
}
