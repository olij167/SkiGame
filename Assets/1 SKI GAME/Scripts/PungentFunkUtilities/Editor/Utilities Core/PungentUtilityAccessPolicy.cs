using PungentFunk.Utilities.Editor.Developer;
using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;

    /// <summary>
    /// Central status gate for user-facing utility launch surfaces.
    /// </summary>
    public static class PungentUtilityAccessPolicy
    {
        private const string PrefExperimentalUtilitiesEnabled = "PungentFunkUtilities.UtilityAccess.ExperimentalUtilitiesEnabled";

        public const string ExperimentalDisabledMessage =
            "Enable experimental utilities in the filters panel to access. These utilities are still undergoing testing and may have minor unresolved issues.";

        public static event Action Changed;

        public static bool ExperimentalUtilitiesEnabled
        {
            get => UtilityWindowPrefs.GetBool(PrefExperimentalUtilitiesEnabled, false);
            set
            {
                if (value == ExperimentalUtilitiesEnabled)
                    return;

                UtilityWindowPrefs.SetBool(PrefExperimentalUtilitiesEnabled, value);
                Changed?.Invoke();
            }
        }

        public static bool DeveloperModeBypassesStatus => PungentDeveloperMode.Available && PungentDeveloperMode.Enabled;

        public static bool CanAccess(PungentUtilityDescriptor descriptor)
        {
            return CanAccess(descriptor, out _);
        }

        public static bool CanAccess(PungentUtilityDescriptor descriptor, out string reason)
        {
            reason = string.Empty;
            if (descriptor == null)
            {
                reason = "No utility was selected.";
                return false;
            }

            if (DeveloperModeBypassesStatus)
                return true;

            string status = PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus);
            if (string.Equals(status, PungentUtilityPackageStatus.Stable, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(status, PungentUtilityPackageStatus.Experimental, StringComparison.OrdinalIgnoreCase))
            {
                if (ExperimentalUtilitiesEnabled)
                    return true;

                reason = ExperimentalDisabledMessage;
                return false;
            }

            reason = "Coming soon: " + status;
            return false;
        }

        public static bool IsExperimentalBlocked(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || DeveloperModeBypassesStatus || ExperimentalUtilitiesEnabled)
                return false;

            return string.Equals(PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus), PungentUtilityPackageStatus.Experimental, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsComingSoon(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null || DeveloperModeBypassesStatus)
                return false;

            string status = PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus);
            return !string.Equals(status, PungentUtilityPackageStatus.Stable, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(status, PungentUtilityPackageStatus.Experimental, StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildBlockedActionLabel(PungentUtilityDescriptor descriptor)
        {
            if (descriptor == null)
                return "Unavailable";

            if (IsExperimentalBlocked(descriptor))
                return "Enable Experimental";

            if (IsComingSoon(descriptor))
                return "Coming soon: " + PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus);

            return "Unavailable";
        }
    }
#endif
}
