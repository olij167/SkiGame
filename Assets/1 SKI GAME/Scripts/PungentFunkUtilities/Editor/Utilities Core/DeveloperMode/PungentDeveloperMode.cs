namespace PungentFunk.Utilities.Editor.Developer
{
#if UNITY_EDITOR
    using UnityEditor;

    public static class PungentDeveloperMode
    {
        private const string PrefEnabled = "PungentFunkUtilities.DeveloperMode.Enabled";
        private const string PrefWarningAcknowledged = "PungentFunkUtilities.DeveloperMode.WarningAcknowledged";

        public const string WarningMessage =
            "These features are intended for development and package customisation. " +
            "Enabling Developer Mode allows you to edit metadata, category appearance, documentation links, and supported presets across the PungentFunk utilities. " +
            "It should not affect gameplay data or scene content, but it can change project-local utility settings and package-facing authoring data. " +
            "If you need to restore the package to its default state, re-import the package or remove the relevant PungentFunkUtilities files from ProjectSettings.";

        public static bool Available => true;

        public static bool InternalSourceAuthoringAvailable
        {
            get
            {
#if PUNGENTFUNK_INTERNAL_DEVTOOLS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefEnabled, false);
            set
            {
                if (value && !EditorPrefs.GetBool(PrefWarningAcknowledged, false))
                {
                    int result = EditorUtility.DisplayDialogComplex(
                        "Enable PungentFunk Developer Mode?",
                        WarningMessage,
                        "Enable",
                        "Cancel",
                        "Enable and don't show again");

                    if (result == 1)
                    {
                        EditorPrefs.SetBool(PrefEnabled, false);
                        return;
                    }

                    if (result == 2)
                        EditorPrefs.SetBool(PrefWarningAcknowledged, true);
                }

                EditorPrefs.SetBool(PrefEnabled, value);
            }
        }

        public static bool WarningAcknowledged
        {
            get => EditorPrefs.GetBool(PrefWarningAcknowledged, false);
            set => EditorPrefs.SetBool(PrefWarningAcknowledged, value);
        }

        public static bool CanEditProjectMetadata => Available && Enabled && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        public static bool CanEditPresetSources => InternalSourceAuthoringAvailable && Enabled && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        public static string DeveloperModeTooltip =>
            "Enables package-development tools for editing utility metadata, documentation links, category appearance, and supported presets. " +
            "Changes are developer-facing and may require package re-import to fully restore defaults.";

        public static string ReleaseBlockerMessage =>
            "Developer Mode is intentionally available. Verify that project-local overrides are safe to ship, and keep source-writing preset tools behind PUNGENTFUNK_INTERNAL_DEVTOOLS unless public preset source editing is intended.";
    }
#endif
}