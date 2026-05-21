using System;
using System.Collections.Generic;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public enum PungentNoteKind
    {
        General,
        UtilityNote,
        FutureFeature,
        FutureUtility,
        AuditFollowUp,
        ImplementationTask,
        DesignDecision,
        TooltipAnnotation,
        TokenDocumentation,
        ProjectNote,
        ReleaseNote,
        OutOfScopeIdea,
        SupportRequest
    }

    public enum PungentNoteStatus
    {
        ToDo,
        InProgress,
        Complete,
        Deferred,
        Blocked,
        NeedsReview,
        FurtherConsideration,
        OutOfScope
    }

    public enum PungentNotePriority
    {
        Crucial,
        Important,
        NiceToHave,
        FurtherConsideration,
        OutOfScope,
        Low
    }

    public enum PungentNoteVisibility
    {
        PrivateProject,
        TeamProject,
        PackageDocumentation,
        DeveloperOnly,
        Hidden
    }

    public enum PungentNoteTargetType
    {
        None,
        RegisteredUtility,
        FutureUtility,
        Asset,
        SceneObject,
        ComponentType,
        ComponentInstance,
        SerializedProperty,
        ScriptPath,
        Token,
        AuditIssue,
        DocumentationLink,
        Note,
        ExternalPath
    }

    public enum PungentNoteSurfaceMode
    {
        Hidden,
        BadgesOnly,
        SelectedContextOnly,
        CriticalOnly,
        AllVisible,
        EditMode
    }

    [Serializable]
    public sealed class PungentNoteTargetLink
    {
        public PungentNoteTargetType type = PungentNoteTargetType.None;
        public string label = string.Empty;
        public string utilityId = string.Empty;
        public string futureUtilityId = string.Empty;
        public string assetGuid = string.Empty;
        public string sceneObjectGlobalId = string.Empty;
        public string componentType = string.Empty;
        public string propertyPath = string.Empty;
        public string scriptPath = string.Empty;
        public string tokenKey = string.Empty;
        public string auditIssueCode = string.Empty;
        public string documentationLinkId = string.Empty;
        public string noteId = string.Empty;
        public string externalPathOrUrl = string.Empty;
    }

    [Serializable]
    public sealed class PungentNote
    {
        public string id = Guid.NewGuid().ToString("N");
        public string title = "New Note";
        public string body = string.Empty;
        public PungentNoteKind kind = PungentNoteKind.General;
        public PungentNoteStatus status = PungentNoteStatus.ToDo;
        public PungentNotePriority priority = PungentNotePriority.NiceToHave;
        public PungentNoteVisibility visibility = PungentNoteVisibility.PrivateProject;
        public List<string> tags = new List<string>();
        public List<PungentNoteTargetLink> targets = new List<PungentNoteTargetLink>();
        public List<string> relatedNoteIds = new List<string>();
        public List<string> linkedTokenKeys = new List<string>();
        public List<string> importSourceIds = new List<string>();
        public string linkedUtilityId = string.Empty;
        public string linkedFutureUtilityId = string.Empty;
        public string auditIssueCode = string.Empty;
        public string stableKey = string.Empty;
        public string createdUtc = DateTime.UtcNow.ToString("o");
        public string updatedUtc = DateTime.UtcNow.ToString("o");
        public bool archived;
        public bool developerOnly;
        public bool locked;
        public string generatedBy = string.Empty;
        public string generatedTemplateId = string.Empty;
        public string generatedUtc = string.Empty;
    }

    [Serializable]
    public sealed class PungentFutureUtilityRecord
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "Future Utility";
        public string area = "Project Audit and Authoring";
        public string description = string.Empty;
        public PungentNoteStatus status = PungentNoteStatus.ToDo;
        public PungentNotePriority priority = PungentNotePriority.NiceToHave;
        public List<string> tags = new List<string>();
        public bool implemented;
        public string relatedRegistryId = string.Empty;
        public string stableKey = string.Empty;
    }

    [Serializable]
    public sealed class PungentNoteImportSource
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "Imported Source";
        public string sourcePath = string.Empty;
        public string sourceType = "CSV";
        public string importedUtc = DateTime.UtcNow.ToString("o");
        public string lastRefreshedUtc = string.Empty;
        public int importedCount;
        public int skippedDuplicateCount;
        public int updatedCount;
        public List<string> noteIds = new List<string>();
        public List<string> futureUtilityIds = new List<string>();
        public string lastHash = string.Empty;
        public bool missing;
        public bool archived;
    }

    [Serializable]
    public sealed class PungentNoteDisplaySettings
    {
        public PungentNoteSurfaceMode surfaceMode = PungentNoteSurfaceMode.SelectedContextOnly;
        public bool showInspectorNotes = true;
        public bool showSceneBadges = true;
        public bool showPropertyContextMenus = true;
        public bool showAssetContextMenus = true;
        public bool showGameObjectContextMenus = true;
        public bool editMode;
        public bool showDeveloperNotes;
        public bool showArchivedInSurfaces;
        public bool showOnlyCriticalInSurfaces;
        public bool showInspectorHoverPreviews = true;
        public bool showBrowserHoverPreviews = true;
        public float hoverPreviewDelaySeconds = 0.35f;
    }
#endif
}
