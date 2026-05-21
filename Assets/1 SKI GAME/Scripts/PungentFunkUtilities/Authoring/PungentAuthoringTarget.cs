using System;

namespace PungentFunk.Utilities.Authoring
{
    public enum PungentAuthoringTargetKind
    {
        Unknown = 0,
        AuthoringItem = 1,
        NoteId = 10,
        RichDocumentId = 20,
        BoardId = 30,
        DataSheetId = 40,
        ChecklistId = 45,
        UtilityId = 50,
        FutureUtilityId = 60,
        TokenKey = 70,
        DocumentationLinkId = 80,
        HelpTopicId = 90,
        AuditIssueCode = 100,
        AssetGuid = 110,
        SceneObjectGlobalId = 120,
        ComponentType = 130,
        ComponentInstanceId = 140,
        SerializedPropertyPath = 150,
        ScriptPath = 160,
        ExternalLocalPath = 170,
        ExternalWebUrl = 180,
        ExternalReference = 190,
        Custom = 1000
    }

    [Serializable]
    public sealed class PungentAuthoringTarget
    {
        public PungentAuthoringTargetKind targetKind = PungentAuthoringTargetKind.Unknown;
        public string customKind = string.Empty;
        public string label = string.Empty;
        public string providerId = string.Empty;
        public string rawValue = string.Empty;
        public string sourceContext = string.Empty;
        public string contextId = string.Empty;
        public string propertyPath = string.Empty;

        public bool HasTarget => !string.IsNullOrWhiteSpace(rawValue) ||
                                 !string.IsNullOrWhiteSpace(contextId) ||
                                 !string.IsNullOrWhiteSpace(propertyPath);

        public void NormalizeInPlace()
        {
            customKind = customKind == null ? string.Empty : customKind.Trim();
            label = label == null ? string.Empty : label.Trim();
            providerId = providerId == null ? string.Empty : providerId.Trim();
            rawValue = rawValue == null ? string.Empty : rawValue.Trim();
            sourceContext = sourceContext == null ? string.Empty : sourceContext.Trim();
            contextId = contextId == null ? string.Empty : contextId.Trim();
            propertyPath = propertyPath == null ? string.Empty : propertyPath.Trim();
        }

        public static PungentAuthoringTarget Create(PungentAuthoringTargetKind kind, string rawValue, string label = null, string providerId = null)
        {
            PungentAuthoringTarget target = new PungentAuthoringTarget
            {
                targetKind = kind,
                rawValue = rawValue ?? string.Empty,
                label = label ?? string.Empty,
                providerId = providerId ?? string.Empty
            };
            target.NormalizeInPlace();
            return target;
        }
    }
}
