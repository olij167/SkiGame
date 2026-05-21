using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public static class PungentPackageIntegrationGUI
    {
        public static bool DrawIntegrationCTA(string label, string description, string[] packageIds, string sourceUtilityId, Color tint)
        {
            string[] ids = CleanIds(packageIds);
            if (ids.Length == 0)
                return false;

            PungentUtilityPackageRecord[] records = ids
                .Select(id => PungentUtilityPackageCatalog.FindRecord(id) ?? PungentUtilityPackageCatalog.CreateEditableRecord(id))
                .Where(record => record != null)
                .ToArray();

            if (records.Length == 0)
            {
                EditorGUILayout.HelpBox("Package metadata is not configured for this optional integration.", MessageType.Info);
                return false;
            }

            string display = records.Length == 1 ? records[0].displayName : records.Length + " package options";
            string action = BuildActionLabel(records);
            string heading = string.IsNullOrWhiteSpace(label) ? "Requires " + display : label.Trim();
            string body = string.IsNullOrWhiteSpace(description)
                ? "This workflow is available through an optional PungentFunk package."
                : description.Trim();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 5, 2)))
            {
                EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(body, UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (PungentUtilityPackageSimulation.Active)
                        UtilityWindowTheme.CountPill("Simulation", UtilityWindowTheme.Amber, 86f);

                    GUILayout.FlexibleSpace();
                    if (UtilityWindowTheme.StyledButton(new GUIContent(action, "Open Other Packages focused on this integration."), UtilityWindowTheme.PungentButtonRole.Warning, GUILayout.Width(Mathf.Clamp(action.Length * 7f + 24f, 96f, 170f)), GUILayout.Height(24f)))
                    {
                        PungentUtilityControlPanelWindow.OpenOtherPackagesFocused(ids);
                        return true;
                    }
                }
            }

            return false;
        }

        private static string BuildActionLabel(PungentUtilityPackageRecord[] records)
        {
            bool anyOwnedNotInstalled = records.Any(record => ResolveState(record) == PungentUtilityPackageSimulationState.OwnedNotInstalled);
            if (anyOwnedNotInstalled)
                return "Install Package";

            bool anyMissing = records.Any(record => ResolveState(record) == PungentUtilityPackageSimulationState.MissingRequiredPackage);
            if (anyMissing)
                return "View Requirements";

            bool anyUnavailable = records.Any(record => ResolveState(record) == PungentUtilityPackageSimulationState.ComingSoonUnavailable || ResolveState(record) == PungentUtilityPackageSimulationState.Disabled);
            if (anyUnavailable)
                return "Learn More";

            return "View Package";
        }

        private static PungentUtilityPackageSimulationState ResolveState(PungentUtilityPackageRecord record)
        {
            PungentUtilityPackageSimulationState simulated = PungentUtilityPackageSimulation.GetState(record.packageId);
            if (simulated != PungentUtilityPackageSimulationState.Actual)
                return simulated;

            switch (PungentUtilityPackageCatalog.ResolveAvailability(record))
            {
                case PungentUtilityPackageAvailability.OwnedNotInstalled:
                    return PungentUtilityPackageSimulationState.OwnedNotInstalled;
                case PungentUtilityPackageAvailability.Unowned:
                    return PungentUtilityPackageSimulationState.AvailableNotOwned;
                default:
                    return PungentUtilityPackageSimulationState.Installed;
            }
        }

        private static string[] CleanIds(string[] packageIds)
        {
            if (packageIds == null)
                return new string[0];

            return packageIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(PungentUtilityPackageCatalog.NormalizePackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
#endif
}
