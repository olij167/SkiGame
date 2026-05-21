namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR && PUNGENTFUNK_INTERNAL_DEVTOOLS
    using PungentFunk.Utilities.Editor.Developer;
    using PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public sealed partial class UtilityWindowThemeCustomizer
    {
        partial void AdjustPresetBrowserSource(ref IEnumerable<UtilityThemePresetDefinition> presets)
        {
            if (PungentDeveloperMode.Enabled)
                presets = UtilityThemePresetLibrary.Presets;
        }

        partial void DrawDeveloperPresetAuthoringPanel()
        {
            if (!PungentDeveloperMode.CanEditPresetSources)
                return;

            UtilityThemePresetDefinition preset = UtilityThemePresetLibrary.Get(UtilityWindowTheme.ActivePreset);
            bool generated = UtilityThemePresetLibrary.IsGeneratedPreset(preset.Preset);
            string source = UtilityThemePresetLibrary.GetPresetSourceLabel(preset.Preset);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Red, 0.24f, 0.12f, 8, 5)))
            {
                UtilityWindowTheme.SectionTitle("Developer Preset Authoring", UtilityWindowTheme.Red, "source-writing");
                EditorGUILayout.HelpBox("These actions update the preset source used by UtilityThemePresetLibrary. They are intended for package development only and must not be included in release exports.", MessageType.Warning);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Selected Starter Theme", preset.DisplayName, GUILayout.MinWidth(260f));
                    UtilityWindowTheme.CountPill(UtilityWindowTheme.ActiveThemeModified ? "Modified" : "Clean", UtilityWindowTheme.ActiveThemeModified ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 78f);
                    UtilityWindowTheme.CountPill(preset.HiddenFromGallery ? "Archived" : "Visible", preset.HiddenFromGallery ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 78f);
                    UtilityWindowTheme.CountPill(source, generated ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 82f);
                    GUILayout.FlexibleSpace();
                }

                UtilityThemePresetSnapshot draft = UtilityThemePresetSnapshot.CaptureCurrent(preset.Preset, hiddenOverride: preset.HiddenFromGallery);
                string warning = UtilityThemePresetSourceWriter.BuildValidationWarning(draft);
                if (!string.IsNullOrWhiteSpace(warning))
                    EditorGUILayout.HelpBox("Snapshot validation warnings:\n" + warning, MessageType.Warning);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!generated))
                    {
                        if (GUILayout.Button(new GUIContent("Overwrite Selected Preset With Current Values", "Replace this generated preset definition with the exact current working-copy colours and shape values."), GUILayout.Height(24f)))
                            ConfirmAndOverwrite(warning);

                        if (GUILayout.Button(new GUIContent(preset.HiddenFromGallery ? "Unarchive Selected Preset" : "Archive Selected Preset", "Toggle hiddenFromGallery without deleting the enum value."), GUILayout.Width(178f), GUILayout.Height(24f)))
                            ConfirmAndArchive(preset, !preset.HiddenFromGallery);

                        if (GUILayout.Button(new GUIContent("Remove Generated Preset Entry", "Remove the generated source entry. The enum value remains and fallback compatibility can still create a hidden placeholder."), GUILayout.Width(188f), GUILayout.Height(24f)))
                            ConfirmAndRemove(preset);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Add New Preset From Current Values", "Use the first safe archived/fallback enum slot and write the current working copy as a generated preset."), GUILayout.Width(220f), GUILayout.Height(22f)))
                        ConfirmAndAdd(warning);

                    if (GUILayout.Button(new GUIContent("Save Draft Header", "Save a small project-local draft note under ProjectSettings without modifying generated source."), GUILayout.Width(128f), GUILayout.Height(22f)))
                    {
                        UtilityThemePresetDraftStore.SaveLastDraft(draft);
                        ShowNotification(new GUIContent("Saved developer draft header."));
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void ConfirmAndOverwrite(string validationWarning)
        {
            string message = "Overwrite the generated source definition for " + UtilityWindowTheme.GetPresetDisplayName(UtilityWindowTheme.ActivePreset) + "?";
            if (!string.IsNullOrWhiteSpace(validationWarning))
                message += "\n\nValidation warnings:\n" + validationWarning + "\n\nCommit anyway?";

            if (!EditorUtility.DisplayDialog("Overwrite Generated Preset", message, "Overwrite", "Cancel"))
                return;

            if (UtilityThemePresetSourceWriter.OverwriteSelectedWithCurrent(out string status))
                ShowNotification(new GUIContent(status));
            else
                EditorUtility.DisplayDialog("Preset Authoring", status, "OK");
        }

        private void ConfirmAndArchive(UtilityThemePresetDefinition preset, bool archived)
        {
            string action = archived ? "Archive" : "Unarchive";
            if (!EditorUtility.DisplayDialog(action + " Generated Preset", action + " " + preset.DisplayName + "?", action, "Cancel"))
                return;

            if (UtilityThemePresetSourceWriter.SetArchiveState(preset.Preset, archived, out string status))
                ShowNotification(new GUIContent(status));
            else
                EditorUtility.DisplayDialog("Preset Authoring", status, "OK");
        }

        private void ConfirmAndRemove(UtilityThemePresetDefinition preset)
        {
            if (!EditorUtility.DisplayDialog("Remove Generated Preset Entry", "Remove the generated source entry for " + preset.DisplayName + "?\n\nThe enum value will remain and fallback compatibility can create a hidden placeholder.", "Remove Entry", "Cancel"))
                return;

            if (UtilityThemePresetSourceWriter.RemoveGeneratedEntry(preset.Preset, out string status))
                ShowNotification(new GUIContent(status));
            else
                EditorUtility.DisplayDialog("Preset Authoring", status, "OK");
        }

        private void ConfirmAndAdd(string validationWarning)
        {
            string message = "Add the current working-copy values as a new generated preset using the first available hidden/fallback enum slot?";
            if (!string.IsNullOrWhiteSpace(validationWarning))
                message += "\n\nValidation warnings:\n" + validationWarning + "\n\nCommit anyway?";

            if (!EditorUtility.DisplayDialog("Add Generated Preset", message, "Add Preset", "Cancel"))
                return;

            if (UtilityThemePresetSourceWriter.AddFromCurrent(out string status))
                ShowNotification(new GUIContent(status));
            else
                EditorUtility.DisplayDialog("Preset Authoring", status, "OK");
        }
    }
#endif
}
