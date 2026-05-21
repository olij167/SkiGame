namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class PungentNoteContextMenus
    {
        static PungentNoteContextMenus()
        {
            EditorApplication.contextualPropertyMenu -= OnContextualPropertyMenu;
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
        }

        [MenuItem("Assets/PungentFunk Utilities/Add Note", priority = 600)]
        private static void AddAssetNote()
        {
            if (!PungentNoteDisplaySettingsService.Settings.showAssetContextMenus)
                return;
            if (!PungentUtilityRegistry.CanOpen("tooltip-notes", true))
                return;
            if (Selection.activeObject != null)
                AddNoteFor(Selection.activeObject, null, "Note: " + Selection.activeObject.name, PungentNoteKind.ProjectNote);
        }

        [MenuItem("Assets/PungentFunk Utilities/View Notes", priority = 601)]
        private static void ViewAssetNotes()
        {
            if (!PungentNoteDisplaySettingsService.Settings.showAssetContextMenus)
                return;
            if (!PungentUtilityRegistry.CanOpen("tooltip-notes", true))
                return;
            ViewNotesFor(Selection.activeObject);
        }

        [MenuItem("Assets/PungentFunk Utilities/Open Notes in Browser", priority = 602)]
        private static void OpenAssetNotesInBrowser()
        {
            if (!PungentNoteDisplaySettingsService.Settings.showAssetContextMenus)
                return;
            if (!PungentUtilityRegistry.CanOpen("tooltip-notes", true))
                return;
            OpenNotesInBrowserFor(Selection.activeObject);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Add Note", priority = 60)]
        private static void AddGameObjectNote(MenuCommand command)
        {
            GameObject go = command.context as GameObject ?? Selection.activeGameObject;
            if (!PungentNoteDisplaySettingsService.Settings.showGameObjectContextMenus)
                return;
            if (!PungentUtilityRegistry.CanOpen("tooltip-notes", true))
                return;
            if (go != null)
                AddNoteFor(go, null, "Note: " + go.name, PungentNoteKind.ProjectNote);
        }

        [MenuItem("GameObject/PungentFunk Utilities/View Notes", priority = 61)]
        private static void ViewGameObjectNotes(MenuCommand command)
        {
            if (!PungentNoteDisplaySettingsService.Settings.showGameObjectContextMenus)
                return;
            if (!PungentUtilityRegistry.CanOpen("tooltip-notes", true))
                return;
            ViewNotesFor(command.context as GameObject ?? Selection.activeGameObject);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Open Notes in Browser", priority = 62)]
        private static void OpenGameObjectNotesInBrowser(MenuCommand command)
        {
            if (!PungentNoteDisplaySettingsService.Settings.showGameObjectContextMenus)
                return;
            if (!PungentUtilityRegistry.CanOpen("tooltip-notes", true))
                return;
            OpenNotesInBrowserFor(command.context as GameObject ?? Selection.activeGameObject);
        }

        public static PungentNote AddNoteFor(UnityEngine.Object target, string propertyPath, string title, PungentNoteKind kind)
        {
            PungentNote note = PungentNoteStorage.Database.CreateNote(string.IsNullOrWhiteSpace(title) ? "New Note" : title, kind);
            note.status = PungentNoteStatus.ToDo;
            note.priority = PungentNotePriority.NiceToHave;
            PungentNoteTargetLink link = PungentNoteContextResolver.CreateTargetLink(target, propertyPath);
            if (link != null)
                note.targets.Add(link);
            PungentNoteStorage.Save();
            PungentNoteSceneOverlay.InvalidateCache();
            PungentNotesRoadmapWindow.OpenAndSelect(note.id);
            return note;
        }

        public static void ViewNotesFor(UnityEngine.Object target)
        {
            var notes = PungentNoteContextResolver.FindNotesFor(target, true, PungentNoteDisplaySettingsService.Settings.showArchivedInSurfaces);
            if (notes.Count == 1)
                PungentNotesRoadmapWindow.OpenAndSelect(notes[0].id);
            else if (notes.Count > 1)
            {
                PungentNotesRoadmapWindow.Open();
                PungentStickyNoteOverlayController.OpenStack(notes, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, target != null ? target.name : "Context Notes");
            }
            else
                PungentNotesRoadmapWindow.OpenForTarget(target, string.Empty, string.Empty, false);
        }

        public static void ViewNotesForProperty(SerializedProperty property)
        {
            var notes = PungentNoteContextResolver.FindNotesForProperty(property, true, PungentNoteDisplaySettingsService.Settings.showArchivedInSurfaces);
            if (notes.Count == 1)
                PungentNotesRoadmapWindow.OpenAndSelect(notes[0].id);
            else if (notes.Count > 1)
            {
                PungentNotesRoadmapWindow.Open();
                PungentStickyNoteOverlayController.OpenStack(notes, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, property != null ? property.displayName : "Property Notes");
            }
            else if (property != null && property.serializedObject != null)
                PungentNotesRoadmapWindow.OpenForTarget(property.serializedObject.targetObject, property.propertyPath, property.displayName, false);
        }

        public static void OpenNotesInBrowserFor(UnityEngine.Object target)
        {
            PungentNotesRoadmapWindow.OpenForTarget(target, string.Empty, string.Empty, false);
        }

        private static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            if (!PungentNoteDisplaySettingsService.Settings.showPropertyContextMenus || property == null || property.serializedObject == null)
                return;

            UnityEngine.Object targetObject = property.serializedObject.targetObject;
            string propertyPath = property.propertyPath;
            string propertyName = property.displayName;

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("PungentFunk/Add Note"), false, () =>
            {
                AddNoteFor(targetObject, propertyPath, "Note: " + propertyName, PungentNoteKind.TooltipAnnotation);
            });
            menu.AddItem(new GUIContent("PungentFunk/View Notes"), false, () =>
            {
                ViewNotesForProperty(property);
            });
            menu.AddItem(new GUIContent("PungentFunk/Open Notes in Browser"), false, () =>
            {
                PungentNotesRoadmapWindow.OpenForTarget(targetObject, propertyPath, propertyName, false);
            });
            menu.AddItem(new GUIContent("PungentFunk/Copy Property Path"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = propertyPath;
            });
        }
    }
#endif
}
