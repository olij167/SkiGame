namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public static class PungentNoteSurfaceGUI
    {
        public static void DrawUtilityNotesSummary(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return;

            var notes = PungentNoteContextResolver.FindNotesForUtility(utilityId, PungentNoteDisplaySettingsService.Settings.showArchivedInSurfaces);
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.12f, 0.06f, 5, 2)))
            {
                EditorGUILayout.LabelField("Sticky Notes", EditorStyles.boldLabel);
                UtilityWindowTheme.CountPill(notes.Count.ToString(), UtilityWindowTheme.Teal, 36f);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Sticky", EditorStyles.miniButton, GUILayout.Width(82f)))
                {
                    if (notes.Count == 1)
                        PungentStickyNoteOverlayController.OpenEdit(notes[0], Rect.zero, PungentStickyNoteOverlayOwner.UtilitySurface, "Utility Notes");
                    else if (notes.Count > 1)
                        PungentStickyNoteOverlayController.OpenStack(notes, Rect.zero, PungentStickyNoteOverlayOwner.UtilitySurface, "Utility Notes");
                    else
                        PungentNotesRoadmapWindow.Open();
                }
                if (GUILayout.Button("Add Sticky", EditorStyles.miniButton, GUILayout.Width(76f)))
                    DrawAddUtilityNoteButton(utilityId);
            }

            PungentStickyNoteOverlayController.Draw(
                PungentStickyNoteOverlayOwner.UtilitySurface,
                new Rect(8f, 8f, Mathf.Max(260f, EditorGUIUtility.currentViewWidth - 16f), 520f));
        }

        public static void DrawAddUtilityNoteButton(string utilityId)
        {
            PungentNote note = PungentNoteStorage.Database.CreateNote("Utility Note", PungentNoteKind.UtilityNote);
            note.linkedUtilityId = utilityId;
            note.targets.Add(new PungentNoteTargetLink { type = PungentNoteTargetType.RegisteredUtility, utilityId = utilityId, label = utilityId });
            PungentNoteStorage.Save();
            PungentStickyNoteOverlayController.OpenEdit(note.id, Rect.zero, PungentStickyNoteOverlayOwner.UtilitySurface, "Utility Notes");
        }
    }
#endif
}
