namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public static class PungentNoteDisplaySettingsService
    {
        public static PungentNoteDisplaySettings Settings => PungentNoteStorage.Database.displaySettings;

        public static bool ContextMenusEnabled => Settings.showPropertyContextMenus || Settings.showAssetContextMenus || Settings.showGameObjectContextMenus;

        public static bool ShouldShowSurfaceNote(PungentNote note, bool selectedContext)
        {
            PungentNoteDisplaySettings settings = Settings;
            if (settings.surfaceMode == PungentNoteSurfaceMode.Hidden || note == null)
                return false;
            if (note.archived && !settings.showArchivedInSurfaces)
                return false;
            if (note.developerOnly && !settings.showDeveloperNotes)
                return false;
            if (note.visibility == PungentNoteVisibility.Hidden)
                return false;
            if (settings.showOnlyCriticalInSurfaces && note.priority != PungentNotePriority.Crucial)
                return false;

            switch (settings.surfaceMode)
            {
                case PungentNoteSurfaceMode.BadgesOnly:
                    return true;
                case PungentNoteSurfaceMode.SelectedContextOnly:
                    return selectedContext;
                case PungentNoteSurfaceMode.CriticalOnly:
                    return note.priority == PungentNotePriority.Crucial || note.status == PungentNoteStatus.Blocked;
                case PungentNoteSurfaceMode.AllVisible:
                case PungentNoteSurfaceMode.EditMode:
                    return true;
                default:
                    return false;
            }
        }
    }
#endif
}
