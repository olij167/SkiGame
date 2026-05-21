namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Legacy popup restore list for minimized PungentFunk utility windows.
    /// Current access paths use PungentMinimizedUtilitiesOverlayWindow.
    /// </summary>
    [Obsolete("Use PungentMinimizedUtilitiesOverlayWindow / Core minimized utilities overlay instead.", false)]
    public sealed class PungentMinimizedUtilitiesPopupContent : PopupWindowContent
    {
        private const float Width = 380f;
        private const float HeaderHeight = 30f;
        private const float RowHeight = 30f;
        private const float FooterHeight = 254f;
        private const float EmptyBodyHeight = 132f;
        private const float MaxRowsHeight = 280f;
        private const float MaxHeight = 620f;

        private static readonly string[] StripPlacementLabels =
        {
            "Bottom Left",
            "Bottom Center",
            "Bottom Right"
        };

        private static readonly string[] FocusModeLabels =
        {
            "Auto Collapse",
            "Stay Open",
            "Auto Hide"
        };

        private Vector2 _scroll;
        private bool _showPlacementFallback;

        public override Vector2 GetWindowSize()
        {
            int count = PungentUtilityMinimizer.MinimizedCount;

            float bodyHeight = count == 0
                ? EmptyBodyHeight
                : Mathf.Min(MaxRowsHeight, count * RowHeight + 12f);

            float totalHeight = HeaderHeight + bodyHeight + FooterHeight;

            return new Vector2(Width, Mathf.Min(MaxHeight, totalHeight));
        }

        public override void OnOpen()
        {
            PungentUtilityMinimizer.EntriesChanged -= RepaintPopup;
            PungentUtilityMinimizer.EntriesChanged += RepaintPopup;
        }

        public override void OnClose()
        {
            PungentUtilityMinimizer.EntriesChanged -= RepaintPopup;
        }

        public override void OnGUI(Rect rect)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawHeader();

                PungentMinimizedUtilityEntry[] entries = PungentUtilityMinimizer.MinimizedEntries.ToArray();
                if (entries.Length == 0)
                    DrawEmptyState();
                else
                    DrawEntries(entries);

                GUILayout.FlexibleSpace();
                DrawFooter(entries.Length);
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                GUILayout.Label("Minimized Utilities", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(PungentUtilityMinimizer.MinimizedCount.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(28f));
            }
        }

        private static void DrawEmptyState()
        {
            EditorGUILayout.HelpBox(
                "No windows are minimized.\n\nUse the minimize button in a PungentFunk utility header, the Utilities Browser card, or Tools > PungentFunk Utilities > Minimize Focused Editor Window / the shortcut for other editor windows.",
                MessageType.Info);
        }

        private void DrawEntries(PungentMinimizedUtilityEntry[] entries)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(84f), GUILayout.MaxHeight(260f));
            for (int i = 0; i < entries.Length; i++)
                DrawEntryRow(entries[i]);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntryRow(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(RowHeight)))
            {
                string displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.UtilityId : entry.DisplayName;
                string kind = PungentUtilityMinimizer.GetEntryKindLabel(entry);
                PungentMinimizedEntryRestoreAvailability availability = PungentUtilityMinimizer.GetRestoreAvailability(entry);
                string rowTooltip = availability.CanRestore
                    ? $"{kind}: {displayName}"
                    : $"{kind}: {displayName}\nUnavailable: {availability.Reason}";

                GUILayout.Label(
                    new GUIContent(displayName, rowTooltip),
                    EditorStyles.label,
                    GUILayout.MinWidth(120f));

                GUILayout.Label(new GUIContent(availability.CanRestore ? kind : "Unavailable", rowTooltip), EditorStyles.miniLabel, GUILayout.Width(84f));
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!availability.CanRestore))
                {
                    if (GUILayout.Button(new GUIContent("Restore", availability.Tooltip), EditorStyles.miniButtonLeft, GUILayout.Width(64f), GUILayout.Height(22f)))
                    {
                        string id = entry.UtilityId;
                        EditorApplication.delayCall += () => PungentUtilityMinimizer.TryRestore(id);
                        ClosePopup();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button(new GUIContent("x", "Close this minimized entry without restoring the window."), EditorStyles.miniButtonRight, GUILayout.Width(28f), GUILayout.Height(22f)))
                {
                    PungentUtilityMinimizer.CloseMinimizedEntry(entry.UtilityId);
                    RepaintPopup();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawFooter(int count)
        {
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        new GUIContent("Minimize Focused", "Minimize focused editor window to tray."),
                        EditorStyles.miniButtonLeft,
                        GUILayout.Height(24f)))
                {
                    PungentUtilityMinimizer.MinimizeLastEditorWindowFromCommand();
                    ClosePopup();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(
                        new GUIContent("Find in Browser", "Open the Utilities Browser focused on the Minimized Utilities card."),
                        EditorStyles.miniButtonRight,
                        GUILayout.Height(24f)))
                {
                    PungentUtilityControlPanelWindow.OpenUtilityCard("utility-tray");
                    ClosePopup();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space(6f);

            bool bottomStrip = PungentUtilityMinimizer.BottomStripEnabled;
            bool nextBottomStrip = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Show minimized tab panel",
                    "Optional direct-restore tab panel. The overlay tray remains the primary minimizer UI."),
                bottomStrip,
                GUILayout.Height(20f));

            if (nextBottomStrip != bottomStrip)
                PungentUtilityMinimizer.BottomStripEnabled = nextBottomStrip;

            using (new EditorGUI.DisabledScope(!nextBottomStrip))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                PungentUtilityMinimizer.OverlayCollapsed ? "Expand Overlay" : "Collapse Overlay",
                                "Manually collapse or expand the minimized utilities overlay tray."),
                            EditorStyles.miniButton,
                            GUILayout.Height(22f)))
                    {
                        PungentUtilityMinimizer.ToggleOverlayCollapsed();
                        RepaintPopup();
                    }

                    MinimizedOverlayFocusMode mode = PungentUtilityMinimizer.OverlayFocusMode;
                    int nextMode = EditorGUILayout.Popup(
                        (int)mode,
                        FocusModeLabels,
                        GUILayout.Height(22f));

                    if (nextMode != (int)mode &&
                        Enum.IsDefined(typeof(MinimizedOverlayFocusMode), nextMode))
                    {
                        PungentUtilityMinimizer.OverlayFocusMode = (MinimizedOverlayFocusMode)nextMode;
                        RepaintPopup();
                    }
                }

                EditorGUILayout.HelpBox("Drag the overlay header to move the tray. Drag the tab panel handle to move the optional tab panel.", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Reset Overlay", "Reset the minimized overlay to its default position."), EditorStyles.miniButtonLeft, GUILayout.Height(22f)))
                    {
                        PungentUtilityMinimizer.ResetOverlayPlacement();
                        RepaintPopup();
                    }

                    if (GUILayout.Button(new GUIContent("Reset Tabs", "Reset the minimized tab panel to its default position."), EditorStyles.miniButtonRight, GUILayout.Height(22f)))
                    {
                        PungentUtilityMinimizer.ResetStripPlacement();
                        RepaintPopup();
                    }
                }

                _showPlacementFallback = EditorGUILayout.Foldout(
                    _showPlacementFallback,
                    new GUIContent("Placement preset (fallback)", "Use this only if the drag handle is unavailable or you want a preset reset."),
                    true);

                if (_showPlacementFallback)
                {
                    MinimizedStripAnchor anchor = PungentUtilityMinimizer.StripAnchor;
                    int nextAnchor = EditorGUILayout.Popup(
                        new GUIContent("Strip position", "Fallback placement preset for the optional minimized tab strip."),
                        (int)anchor,
                        StripPlacementLabels);

                    if (nextAnchor != (int)anchor &&
                        Enum.IsDefined(typeof(MinimizedStripAnchor), nextAnchor))
                    {
                        PungentUtilityMinimizer.ApplyStripPlacementPreset((MinimizedStripAnchor)nextAnchor);
                    }
                }
            }

            bool allowGeneric = PungentUtilityMinimizer.AllowGenericEditorWindowMinimization;
            bool nextAllowGeneric = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Allow non-PungentFunk editor windows",
                    "Allows the menu/shortcut command to minimize most Unity and third-party EditorWindow panels."),
                allowGeneric,
                GUILayout.Height(20f));

            if (nextAllowGeneric != allowGeneric)
                PungentUtilityMinimizer.AllowGenericEditorWindowMinimization = nextAllowGeneric;

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(count == 0))
                {
                    if (GUILayout.Button(
                            new GUIContent("Restore All", "Restore all minimized utilities."),
                            EditorStyles.miniButtonLeft,
                            GUILayout.Height(24f)))
                    {
                        EditorApplication.delayCall += PungentUtilityMinimizer.RestoreAll;
                        ClosePopup();
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button(
                            new GUIContent("Clear All", "Clear minimized entries without restoring windows."),
                            EditorStyles.miniButtonRight,
                            GUILayout.Height(24f)))
                    {
                        PungentUtilityMinimizer.ClearAll();
                        RepaintPopup();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void RepaintPopup()
        {
            editorWindow?.Repaint();
        }

        private void ClosePopup()
        {
            if (editorWindow != null)
                editorWindow.Close();
        }
    }
#endif
}
