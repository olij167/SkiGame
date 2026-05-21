using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class PungentNoteInspectorIntegration
    {
        static PungentNoteInspectorIntegration()
        {
            Editor.finishedDefaultHeaderGUI -= OnFinishedDefaultHeaderGUI;
            Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
        }

        private static void OnFinishedDefaultHeaderGUI(Editor editor)
        {
            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            if (!settings.showInspectorNotes || settings.surfaceMode == PungentNoteSurfaceMode.Hidden || editor == null || editor.targets == null || editor.targets.Length == 0)
                return;

            List<PungentNote> notes = new List<PungentNote>();
            foreach (UnityEngine.Object target in editor.targets)
            {
                if (target == null || (target.hideFlags & HideFlags.HideAndDontSave) != 0)
                    continue;
                notes.AddRange(PungentNoteContextResolver.FindNotesFor(target, true, settings.showArchivedInSurfaces));
            }

            notes = notes
                .Where(n => PungentNoteDisplaySettingsService.ShouldShowSurfaceNote(n, true))
                .GroupBy(n => n.id)
                .Select(g => g.First())
                .OrderBy(n => n.priority)
                .ThenByDescending(n => n.updatedUtc)
                .ToList();

            if (notes.Count == 0 && !settings.editMode && settings.surfaceMode != PungentNoteSurfaceMode.EditMode)
                return;

            Color tint = notes.Count > 0 ? PungentNoteGUI.PriorityTint(notes[0].priority) : UtilityWindowTheme.Neutral;
            Rect headerRect = Rect.zero;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.14f, 0.08f, 6, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Pungent Notes", EditorStyles.boldLabel);
                    UtilityWindowTheme.CountPill(notes.Count.ToString(), tint, 36f);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(48f)))
                    {
                        if (notes.Count == 1)
                            PungentStickyNoteOverlayController.OpenEdit(notes[0], headerRect, PungentStickyNoteOverlayOwner.InspectorHeader, "Inspector Notes");
                        else if (notes.Count > 1)
                            PungentStickyNoteOverlayController.OpenStack(notes, headerRect, PungentStickyNoteOverlayOwner.InspectorHeader, editor.target != null ? editor.target.name : "Inspector Notes");
                        else
                            PungentNotesRoadmapWindow.OpenForTarget(editor.target, string.Empty, string.Empty, false);
                    }
                    if (settings.editMode || settings.surfaceMode == PungentNoteSurfaceMode.EditMode)
                    {
                        if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(42f)))
                            PungentStickyNoteOverlayController.CreateForTargetAndEdit(editor.target, null, editor.target != null ? editor.target.name : "Selection", headerRect, PungentStickyNoteOverlayOwner.InspectorHeader);
                    }
                }
                if (Event.current != null && Event.current.type == EventType.Repaint)
                    headerRect = GUILayoutUtility.GetLastRect();

                foreach (PungentNote note in notes.Take(4))
                {
                    Rect rowRect = EditorGUILayout.BeginHorizontal();
                    try
                    {
                        UtilityWindowTheme.CountPill(note.priority.ToString(), PungentNoteGUI.PriorityTint(note.priority), 88f);
                        if (GUILayout.Button(new GUIContent(note.title, "Open this note in the shared sticky-note overlay."), UtilityWindowTheme.MutedMiniLabelStyle))
                            PungentStickyNoteOverlayController.OpenEdit(note.id, rowRect, PungentStickyNoteOverlayOwner.InspectorHeader, "Inspector Notes");
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }

                    PungentStickyNoteOverlayController.RequestHoverPreview(
                        note,
                        rowRect,
                        PungentStickyNoteOverlayOwner.InspectorHeader,
                        "Inspector Notes",
                        settings.showInspectorHoverPreviews,
                        false,
                        settings.hoverPreviewDelaySeconds <= 0f ? 0.35f : settings.hoverPreviewDelaySeconds);
                }

                if (notes.Count > 4)
                    EditorGUILayout.LabelField((notes.Count - 4) + " more notes...", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            PungentStickyNoteOverlayController.Draw(
                PungentStickyNoteOverlayOwner.InspectorHeader,
                new Rect(8f, 8f, Mathf.Max(260f, EditorGUIUtility.currentViewWidth - 16f), 520f));
        }
    }
#endif
}
