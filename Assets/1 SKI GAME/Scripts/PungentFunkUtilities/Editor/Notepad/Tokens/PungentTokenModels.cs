using System;
using System.Collections.Generic;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public enum PungentTokenTargetType
    {
        None,
        Asset,
        SceneObject,
        ComponentType,
        ComponentInstance,
        SerializedProperty,
        ScriptPath,
        Note,
        AuditIssue,
        RegisteredUtility,
        FutureUtility,
        ExternalPath
    }

    public enum PungentTokenUsageStatus
    {
        Known,
        Unknown,
        Deprecated,
        MissingRequired,
        InvalidFormat,
        ReplacementMissing,
        DuplicateDefinition,
        BrokenBinding
    }

    [Serializable]
    public sealed class PungentTokenDefinition
    {
        public string id = Guid.NewGuid().ToString("N");
        public string key = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string previewValue = string.Empty;
        public string category = "General";
        public List<string> tags = new List<string>();
        public List<string> examples = new List<string>();
        public bool required;
        public bool deprecated;
        public string replacementKey = string.Empty;
        public bool archived;
        public bool developerOnly;
        public string createdUtc = DateTime.UtcNow.ToString("o");
        public string updatedUtc = DateTime.UtcNow.ToString("o");
        public string allowedContext = string.Empty;
        public string documentationNoteId = string.Empty;
        public string source = string.Empty;
    }

    [Serializable]
    public sealed class PungentTokenBinding
    {
        public string id = Guid.NewGuid().ToString("N");
        public string tokenKey = string.Empty;
        public string label = string.Empty;
        public PungentTokenTargetType targetType = PungentTokenTargetType.None;
        public string assetGuid = string.Empty;
        public string sceneObjectGlobalId = string.Empty;
        public string componentType = string.Empty;
        public string componentInstanceId = string.Empty;
        public string propertyPath = string.Empty;
        public string scriptPath = string.Empty;
        public string noteId = string.Empty;
        public string auditIssueCode = string.Empty;
        public string futureUtilityId = string.Empty;
        public string utilityId = string.Empty;
        public string externalPath = string.Empty;
        public string createdUtc = DateTime.UtcNow.ToString("o");
        public string updatedUtc = DateTime.UtcNow.ToString("o");
        public bool archived;
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public sealed class PungentTokenUsage
    {
        public string tokenKey = string.Empty;
        public string sourceLabel = string.Empty;
        public string sourcePath = string.Empty;
        public string noteId = string.Empty;
        public string assetGuid = string.Empty;
        public PungentTokenUsageStatus status = PungentTokenUsageStatus.Known;
        public string message = string.Empty;
        public int occurrenceCount;
        public List<string> contexts = new List<string>();
    }
#endif
}
