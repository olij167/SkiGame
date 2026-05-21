namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core.Help;
    using PungentFunk.Utilities.Editor.ProjectAudit;
    using UnityEditor;
    using UnityEngine;

    public static class PungentMinimizedUtilitiesMenu
    {
        public static void Show(Rect activatorRect)
        {
            GenericMenu menu = BuildMenu(activatorRect);
            if (activatorRect.width > 0f && activatorRect.height > 0f)
                menu.DropDown(activatorRect);
            else
                menu.ShowAsContext();
        }

        public static void ShowAsContext()
        {
            BuildMenu().ShowAsContext();
        }

        public static void ShowOverlayTrayDropdownAsContext()
        {
            BuildMenu(null, true).ShowAsContext();
        }

        public static void ShowEntryContext(PungentMinimizedUtilityEntry entry)
        {
            GenericMenu menu = BuildMenu();
            if (entry != null && !string.IsNullOrWhiteSpace(entry.UtilityId))
            {
                PungentMinimizedEntryRestoreAvailability availability = PungentUtilityMinimizer.GetRestoreAvailability(entry);
                menu.AddSeparator(string.Empty);
                if (availability.CanRestore)
                    menu.AddItem(new GUIContent("Selected Entry/Restore"), false, () => PungentUtilityMinimizer.TryRestore(entry.UtilityId));
                else
                    menu.AddDisabledItem(new GUIContent("Selected Entry/Restore Unavailable - " + availability.Reason));
                menu.AddItem(new GUIContent("Selected Entry/Close"), false, () => PungentUtilityMinimizer.CloseMinimizedEntry(entry.UtilityId));
            }

            menu.ShowAsContext();
        }

        public static GenericMenu BuildMenu()
        {
            return BuildMenu(null);
        }

        private static GenericMenu BuildMenu(Rect? popupAnchor)
        {
            return BuildMenu(popupAnchor, false);
        }

        private static GenericMenu BuildMenu(Rect? popupAnchor, bool includeOverlayTrayLinks)
        {
            GenericMenu menu = new GenericMenu();
            int count = PungentUtilityMinimizer.MinimizedCount;
            bool stripEnabled = PungentUtilityMinimizer.BottomStripEnabled;
            bool overlayCollapsed = PungentUtilityMinimizer.OverlayCollapsed;

            menu.AddItem(new GUIContent("Open Minimized Utilities Tray"), false, () =>
            {
                if (popupAnchor.HasValue)
                    PungentUtilityMinimizer.ToggleOverlayTrayFromAccess(popupAnchor.Value);
                else
                    PungentUtilityMinimizer.ToggleOverlayTrayFromAccess(new Rect());
            });
            menu.AddItem(new GUIContent("Find in Utilities Browser"), false, () => PungentUtilityControlPanelWindow.OpenUtilityCard("utility-tray"));
            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent(overlayCollapsed ? "Overlay/Expand Overlay" : "Overlay/Collapse to Header"), false, PungentUtilityMinimizer.ToggleOverlayCollapsed);
            menu.AddItem(new GUIContent("Overlay/Reset Overlay Position"), false, PungentUtilityMinimizer.ResetOverlayPlacement);
            menu.AddItem(new GUIContent("Overlay/Hide Overlay"), false, PungentUtilityMinimizer.HideOverlayTray);
            menu.AddSeparator("Overlay/");
            AddFocusMode(menu, MinimizedOverlayFocusMode.AutoCollapse, "Overlay/Focus Behavior/Auto Collapse");
            AddFocusMode(menu, MinimizedOverlayFocusMode.StayOpen, "Overlay/Focus Behavior/Stay Open");
            AddFocusMode(menu, MinimizedOverlayFocusMode.AutoHide, "Overlay/Focus Behavior/Auto Hide");

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Tab Panel/Show Minimized Tab Panel"), stripEnabled, () => PungentUtilityMinimizer.BottomStripEnabled = !stripEnabled);
            if (stripEnabled)
            {
                menu.AddItem(new GUIContent("Tab Panel/Reset Tab Panel Position"), false, PungentUtilityMinimizer.ResetStripPlacement);
            }
            else
            {
                menu.AddItem(new GUIContent("Tab Panel/Show Tab Panel Now"), false, () =>
                {
                    PungentUtilityMinimizer.BottomStripEnabled = true;
                    PungentUtilityMinimizer.ShowBottomStripIfNeeded();
                });
                menu.AddDisabledItem(new GUIContent("Tab Panel/Reset Tab Panel Position"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Minimize Focused Editor Window to Tray"), false, PungentUtilityMinimizer.MinimizeLastEditorWindowFromStrip);

            if (count > 0)
            {
                menu.AddItem(new GUIContent("Restore All"), false, PungentUtilityMinimizer.RestoreAll);
                menu.AddItem(new GUIContent("Clear Minimized Entries"), false, PungentUtilityMinimizer.ClearAll);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Restore All"));
                menu.AddDisabledItem(new GUIContent("Clear Minimized Entries"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Utilities Browser"), false, PungentUtilityControlPanelWindow.Open);
            menu.AddItem(new GUIContent("Support/Create Support Request"), false, () => PungentSupportRequestBridge.OpenFromBugReportContext(new PungentBugReportContext
            {
                contextLabel = "Minimized utilities support request",
                contextPath = "Minimized Utilities Tray",
                sourceWindow = "Minimized Utilities Tray"
            }));
            menu.AddItem(new GUIContent("Support/Open Requests"), false, PungentSupportRequestBridge.OpenRequests);
            if (includeOverlayTrayLinks)
                AddStickyNotesBrowserItem(menu);
            menu.AddItem(new GUIContent("Open Help Browser"), false, PungentUtilityHelpBrowserWindow.Open);
            return menu;
        }

        private static void AddStickyNotesBrowserItem(GenericMenu menu)
        {
            const string stickyNotesId = "tooltip-notes";
            if (PungentUtilityRegistry.CanOpen(stickyNotesId))
                menu.AddItem(new GUIContent("Open Sticky Notes Browser"), false, () => PungentUtilityRegistry.Open(stickyNotesId));
            else
                menu.AddDisabledItem(new GUIContent("Open Sticky Notes Browser (Unavailable)"));
        }

        private static void AddFocusMode(GenericMenu menu, MinimizedOverlayFocusMode mode, string path)
        {
            menu.AddItem(
                new GUIContent(path),
                PungentUtilityMinimizer.OverlayFocusMode == mode,
                () => PungentUtilityMinimizer.OverlayFocusMode = mode);
        }
    }
#endif
}
