namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Core.Help;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Movable overlay tray for minimized PungentFunk utility windows.
    /// </summary>
    public sealed class PungentMinimizedUtilitiesOverlayWindow : EditorWindow
    {
        private static readonly string[] FocusModeLabels =
        {
            "Auto Collapse",
            "Stay Open",
            "Auto Hide"
        };

        private static GUIStyle _headerStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static GUIStyle _rowStyle;

        private const double AttentionPulseSeconds = 0.55d;

        private Vector2 _scroll;
        private Vector2 _headerDragStartScreenPosition;
        private Rect _headerDragStartWindowRect;
        private bool _headerDragging;
        private bool _headerDragMoved;
        private double _menuGuardUntil;
        private double _attentionUntil;

        public static PungentMinimizedUtilitiesOverlayWindow Instance { get; private set; }

        internal double LastInteractionTime { get; private set; }

        internal bool IsInteracting => _headerDragging || EditorApplication.timeSinceStartup < _menuGuardUntil;

        internal static PungentMinimizedUtilitiesOverlayWindow FindOpenInstanceAndCloseDuplicates()
        {
            PungentMinimizedUtilitiesOverlayWindow[] windows = Resources.FindObjectsOfTypeAll<PungentMinimizedUtilitiesOverlayWindow>();
            PungentMinimizedUtilitiesOverlayWindow primary = Instance;

            if (primary == null || Array.IndexOf(windows, primary) < 0)
                primary = windows.Length > 0 ? windows[0] : null;

            for (int i = 0; i < windows.Length; i++)
            {
                PungentMinimizedUtilitiesOverlayWindow window = windows[i];
                if (window == null || window == primary)
                    continue;

                window.Close();
            }

            Instance = primary;
            return primary;
        }

        internal static void ShowOrReposition(Rect rect)
        {
            PungentMinimizedUtilitiesOverlayWindow window = FindOpenInstanceAndCloseDuplicates();
            if (window == null)
            {
                window = CreateInstance<PungentMinimizedUtilitiesOverlayWindow>();
                window.titleContent = new GUIContent("Minimized Utilities");
                window.ApplyOverlayRect(rect, true);
                window.ShowPopup();
            }

            window.ApplyOverlayRect(rect, true);
            window.FocusAndPulse(false);
            window.Repaint();
        }

        internal void FocusAndPulse(bool pulse = true)
        {
            Focus();
            MarkInteraction();

            if (pulse)
                _attentionUntil = EditorApplication.timeSinceStartup + AttentionPulseSeconds;

            Repaint();
        }

        internal void ApplyOverlayRect(Rect rect, bool persist)
        {
            Rect clamped = PungentUtilityMinimizer.ClampOverlayRect(rect);
            position = clamped;
            minSize = new Vector2(clamped.width, clamped.height);
            maxSize = new Vector2(clamped.width, clamped.height);

            if (persist)
                PungentUtilityMinimizer.SaveOverlayPlacement(clamped);
        }

        private void OnEnable()
        {
            Instance = this;
            wantsMouseMove = true;
            titleContent = new GUIContent("Minimized Utilities");
            LastInteractionTime = EditorApplication.timeSinceStartup;
            PungentUtilityMinimizer.EntriesChanged += OnEntriesChanged;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;

            PungentUtilityMinimizer.EntriesChanged -= OnEntriesChanged;
        }

        private void OnGUI()
        {
            EnsureStyles();
            TrackInteraction(Event.current);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                PungentUtilityMinimizer.HideOverlayTray();
                Event.current.Use();
                GUIUtility.ExitGUI();
            }

            Rect full = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(full, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.13f, 0.98f) : new Color(0.82f, 0.82f, 0.84f, 0.98f));

            Rect headerRect = new Rect(0f, 0f, position.width, PungentUtilityMinimizer.GetOverlayHeaderHeight());
            DrawHeader(headerRect);

            if (PungentUtilityMinimizer.OverlayCollapsed)
                return;

            Rect bodyRect = new Rect(7f, headerRect.yMax + 6f, position.width - 14f, Mathf.Max(0f, position.height - headerRect.height - 12f));
            GUILayout.BeginArea(bodyRect);
            DrawExpandedContent();
            GUILayout.EndArea();
        }

        private void DrawHeader(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _headerStyle);
            if (EditorApplication.timeSinceStartup < _attentionUntil)
            {
                Color pulse = EditorGUIUtility.isProSkin
                    ? new Color(0.34f, 0.56f, 0.95f, 0.28f)
                    : new Color(0.20f, 0.43f, 0.90f, 0.24f);
                EditorGUI.DrawRect(rect, pulse);
                Repaint();
            }

            float x = rect.x + 4f;
            Rect toggleRect = new Rect(x, rect.y + 4f, 28f, rect.height - 8f);
            x = toggleRect.xMax + 4f;

            Rect closeRect = new Rect(rect.xMax - 30f, rect.y + 4f, 26f, rect.height - 8f);
            Rect minRect = new Rect(closeRect.x - 38f, rect.y + 4f, 34f, rect.height - 8f);
            Rect menuRect = new Rect(minRect.x - 38f, rect.y + 4f, 34f, rect.height - 8f);
            Rect titleRect = new Rect(x, rect.y + 3f, Mathf.Max(40f, menuRect.x - x - 4f), rect.height - 6f);

            string toggleLabel = PungentUtilityMinimizer.OverlayCollapsed ? ">" : "v";
            if (GUI.Button(toggleRect, new GUIContent(toggleLabel, "Expand or collapse the minimized utilities overlay."), _toolbarButtonStyle))
            {
                PungentUtilityMinimizer.ToggleOverlayCollapsed();
                GUIUtility.ExitGUI();
            }

            HandleHeaderDrag(titleRect, Event.current);
            GUI.Label(titleRect, GetTitleContent(), _titleStyle);

            if (GUI.Button(menuRect, new GUIContent("...", "Open minimized utilities actions and settings."), _toolbarButtonStyle))
            {
                GuardMenuFocus();
                PungentMinimizedUtilitiesMenu.ShowOverlayTrayDropdownAsContext();
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(minRect, new GUIContent("Min", "Minimize focused editor window to tray."), _toolbarButtonStyle))
            {
                PungentUtilityMinimizer.MinimizeLastEditorWindowFromStrip();
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(closeRect, new GUIContent("x", "Hide this overlay tray."), _toolbarButtonStyle))
            {
                PungentUtilityMinimizer.HideOverlayTray();
                GUIUtility.ExitGUI();
            }
        }

        private GUIContent GetTitleContent()
        {
            int count = PungentUtilityMinimizer.MinimizedCount;
            string title = count == 1 ? "Minimized 1" : "Minimized " + count;
            return new GUIContent(title, "Drag to move. Click to collapse or expand.");
        }

        private void DrawExpandedContent()
        {
            PungentMinimizedUtilityEntry[] entries = PungentUtilityMinimizer.MinimizedEntries.ToArray();
            float entriesHeight = PungentUtilityMinimizer.GetOverlayBodyHeight(entries.Length);

            if (entries.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No windows are minimized.\n\nUse Minimize Focused, a PungentFunk utility header minimize button, or the shortcut to send an eligible editor window here.",
                    MessageType.Info,
                    true);
                GUILayout.Space(Mathf.Max(0f, entriesHeight - 92f));
            }
            else
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(entriesHeight));
                for (int i = 0; i < entries.Length; i++)
                    DrawEntryRow(entries[i]);
                EditorGUILayout.EndScrollView();
            }

            GUILayout.Space(6f);
            DrawFooter(entries.Length);
        }

        private void DrawEntryRow(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null)
                return;

            using (new EditorGUILayout.HorizontalScope(_rowStyle, GUILayout.Height(PungentUtilityMinimizer.GetOverlayRowHeight())))
            {
                string displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.UtilityId : entry.DisplayName;
                string kind = PungentUtilityMinimizer.GetEntryKindLabel(entry);
                PungentMinimizedEntryRestoreAvailability availability = PungentUtilityMinimizer.GetRestoreAvailability(entry);
                string rowTooltip = availability.CanRestore
                    ? $"{kind}: {displayName}"
                    : $"{kind}: {displayName}\nUnavailable: {availability.Reason}";

                GUILayout.Label(new GUIContent(displayName, rowTooltip), EditorStyles.label, GUILayout.MinWidth(120f));
                GUILayout.Label(new GUIContent(availability.CanRestore ? kind : "Unavailable", rowTooltip), EditorStyles.miniLabel, GUILayout.Width(84f));
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!availability.CanRestore))
                {
                    if (GUILayout.Button(new GUIContent("Restore", availability.Tooltip), EditorStyles.miniButtonLeft, GUILayout.Width(64f), GUILayout.Height(22f)))
                    {
                        string id = entry.UtilityId;
                        EditorApplication.delayCall += () => PungentUtilityMinimizer.TryRestore(id);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button(new GUIContent("x", "Close this minimized entry without restoring the window."), EditorStyles.miniButtonRight, GUILayout.Width(28f), GUILayout.Height(22f)))
                {
                    PungentUtilityMinimizer.CloseMinimizedEntry(entry.UtilityId);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawFooter(int count)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Minimize Focused", "Minimize focused editor window to tray."), EditorStyles.miniButtonLeft, GUILayout.Height(23f)))
                {
                    PungentUtilityMinimizer.MinimizeLastEditorWindowFromCommand();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Utilities", "Open the Utilities Browser focused on Minimized Utilities."), EditorStyles.miniButtonMid, GUILayout.Height(23f)))
                {
                    PungentUtilityControlPanelWindow.OpenUtilityCard("utility-tray");
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Help", "Open the PungentFunk Help Browser."), EditorStyles.miniButtonRight, GUILayout.Height(23f)))
                {
                    PungentUtilityHelpBrowserWindow.Open();
                    GUIUtility.ExitGUI();
                }
            }

            GUILayout.Space(5f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Collapse Header", "Collapse this overlay to its header toolbar."), EditorStyles.miniButton, GUILayout.Height(22f)))
                {
                    PungentUtilityMinimizer.SetOverlayCollapsed(true);
                    GUIUtility.ExitGUI();
                }

                MinimizedOverlayFocusMode mode = PungentUtilityMinimizer.OverlayFocusMode;
                int nextMode = EditorGUILayout.Popup((int)mode, FocusModeLabels, GUILayout.Height(22f));
                if (nextMode != (int)mode &&
                    Enum.IsDefined(typeof(MinimizedOverlayFocusMode), nextMode))
                {
                    PungentUtilityMinimizer.OverlayFocusMode = (MinimizedOverlayFocusMode)nextMode;
                    GUIUtility.ExitGUI();
                }
            }

            bool bottomStrip = PungentUtilityMinimizer.BottomStripEnabled;
            bool nextBottomStrip = EditorGUILayout.ToggleLeft(
                new GUIContent("Show minimized tab panel", "Show the compact direct-restore tab panel."),
                bottomStrip,
                GUILayout.Height(19f));

            if (nextBottomStrip != bottomStrip)
                PungentUtilityMinimizer.BottomStripEnabled = nextBottomStrip;

            bool allowGeneric = PungentUtilityMinimizer.AllowGenericEditorWindowMinimization;
            bool nextAllowGeneric = EditorGUILayout.ToggleLeft(
                new GUIContent("Allow non-PungentFunk editor windows", "Allows the menu/shortcut command to minimize most Unity and third-party EditorWindow panels."),
                allowGeneric,
                GUILayout.Height(19f));

            if (nextAllowGeneric != allowGeneric)
                PungentUtilityMinimizer.AllowGenericEditorWindowMinimization = nextAllowGeneric;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Reset Overlay", "Reset this overlay's saved position."), EditorStyles.miniButtonLeft, GUILayout.Height(22f)))
                {
                    PungentUtilityMinimizer.ResetOverlayPlacement();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Reset Tabs", "Reset the minimized tab panel position."), EditorStyles.miniButtonRight, GUILayout.Height(22f)))
                    PungentUtilityMinimizer.ResetStripPlacement();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Restore All", "Restore all minimized windows."), EditorStyles.miniButtonLeft, GUILayout.Height(23f)))
                    {
                        EditorApplication.delayCall += PungentUtilityMinimizer.RestoreAll;
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button(new GUIContent("Clear All", "Clear minimized entries without restoring windows."), EditorStyles.miniButtonRight, GUILayout.Height(23f)))
                    {
                        PungentUtilityMinimizer.ClearAll();
                        GUIUtility.ExitGUI();
                    }
                }
            }
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

                        Rect next = new Rect(
                            _headerDragStartWindowRect.x + delta.x,
                            _headerDragStartWindowRect.y + delta.y,
                            _headerDragStartWindowRect.width,
                            _headerDragStartWindowRect.height);
                        ApplyOverlayRect(next, false);
                        MarkInteraction();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && _headerDragging)
                    {
                        Vector2 currentScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        Vector2 delta = currentScreen - _headerDragStartScreenPosition;
                        Rect next = new Rect(
                            _headerDragStartWindowRect.x + delta.x,
                            _headerDragStartWindowRect.y + delta.y,
                            _headerDragStartWindowRect.width,
                            _headerDragStartWindowRect.height);

                        GUIUtility.hotControl = 0;
                        _headerDragging = false;

                        if (_headerDragMoved)
                        {
                            ApplyOverlayRect(next, true);
                        }
                        else
                        {
                            ApplyOverlayRect(_headerDragStartWindowRect, false);
                            PungentUtilityMinimizer.ToggleOverlayCollapsed();
                        }

                        _headerDragMoved = false;
                        MarkInteraction();
                        evt.Use();
                    }
                    break;
            }
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
            {
                MarkInteraction();
            }
        }

        private void GuardMenuFocus()
        {
            MarkInteraction();
            _menuGuardUntil = EditorApplication.timeSinceStartup + 1.25d;
        }

        private void MarkInteraction()
        {
            LastInteractionTime = EditorApplication.timeSinceStartup;
        }

        private void OnLostFocus()
        {
            MarkInteraction();
        }

        private void OnEntriesChanged()
        {
            ApplyOverlayRect(PungentUtilityMinimizer.GetOverlayRectForCurrentState(position), true);
            Repaint();
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

            _rowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(5, 5, 4, 4),
                margin = new RectOffset(0, 0, 2, 2)
            };
        }
    }
#endif
}
