namespace PungentFunk.Utilities.Editor.Core.Toolbar
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Core.Help;
    using PungentFunk.Utilities.Editor.ProjectAudit;
    using UnityEditor;
    using UnityEditor.Toolbars;

    public static class PungentFunkMainToolbarElements
    {
        private const string MinimizedUtilitiesId = "PungentFunk/Minimized Utilities";
        private const string UtilitiesBrowserId = "PungentFunk/Utilities Browser";
        private const string HelpBrowserId = "PungentFunk/Help Browser";
        private const string SupportRequestId = "PungentFunk/Support Request";

        static PungentFunkMainToolbarElements()
        {
            PungentUtilityMinimizer.EntriesChanged += RefreshLabel;
        }

        [MainToolbarElement(
            MinimizedUtilitiesId,
            defaultDockPosition = MainToolbarDockPosition.Right,
            defaultDockIndex = 250,
            menuPriority = 2000)]
        public static MainToolbarElement CreateMinimizedUtilities()
        {
            int count = PungentUtilityMinimizer.MinimizedCount;
            string label = count > 0 ? "Min " + count : "Min";
            string tooltip = count > 0
                ? "Open minimized utilities tray. " + count + " window(s) minimized."
                : "Open minimized utilities tray.";

            return new MainToolbarDropdown(
                new MainToolbarContent(label, tooltip),
                PungentMinimizedUtilitiesMenu.Show);
        }

        [MainToolbarElement(
            UtilitiesBrowserId,
            defaultDockPosition = MainToolbarDockPosition.Right,
            defaultDockIndex = 251,
            menuPriority = 2001)]
        public static MainToolbarElement CreateUtilitiesBrowser()
        {
            return new MainToolbarButton(
                new MainToolbarContent("Utils", "Open the PungentFunk Utilities Browser."),
                PungentUtilityControlPanelWindow.Open);
        }

        [MainToolbarElement(
            HelpBrowserId,
            defaultDockPosition = MainToolbarDockPosition.Right,
            defaultDockIndex = 252,
            menuPriority = 2002)]
        public static MainToolbarElement CreateHelpBrowser()
        {
            return new MainToolbarButton(
                new MainToolbarContent("Help", "Open the PungentFunk Help Browser."),
                PungentUtilityHelpBrowserWindow.Open);
        }

        [MainToolbarElement(
            SupportRequestId,
            defaultDockPosition = MainToolbarDockPosition.Right,
            defaultDockIndex = 253,
            menuPriority = 2003)]
        public static MainToolbarElement CreateSupportRequest()
        {
            return new MainToolbarButton(
                new MainToolbarContent("Request", "Create or open a PungentFunk support request in Sticky Notes."),
                () => PungentSupportRequestBridge.OpenFromBugReportContext(new PungentBugReportContext
                {
                    contextLabel = "Main toolbar support request",
                    contextPath = "Unity Main Toolbar",
                    sourceWindow = "Unity Main Toolbar"
                }));
        }

        private static void RefreshLabel()
        {
            MainToolbar.Refresh(MinimizedUtilitiesId);
        }
    }
#endif
}
