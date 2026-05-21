using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.Editor.Authoring
{
#if UNITY_EDITOR
    public enum PungentAuthoringActionKind
    {
        Unknown = 0,
        Open = 10,
        Edit = 20,
        Copy = 30,
        CreateFromContext = 40,
        ConvertLegacyNoteToRichDocument = 100,
        CreateBoardFromSelectedNotes = 110,
        CreateDataSheetFromValidationResults = 120,
        CreateChecklistFromRichDocument = 130,
        Custom = 1000
    }

    [Serializable]
    public sealed class PungentAuthoringAction
    {
        public PungentAuthoringActionKind kind = PungentAuthoringActionKind.Unknown;
        public string label = string.Empty;
        public string tooltip = string.Empty;
        public string providerId = string.Empty;
        public string extensionId = string.Empty;
        public PungentAuthoringReference contextReference;
        public bool enabled;
        public string disabledReason = string.Empty;

        public static PungentAuthoringAction MissingExtension(
            PungentAuthoringActionKind kind,
            string label,
            string missingProviderMessage,
            PungentAuthoringReference contextReference = null,
            string extensionId = null)
        {
            return new PungentAuthoringAction
            {
                kind = kind,
                label = label ?? string.Empty,
                tooltip = missingProviderMessage ?? string.Empty,
                extensionId = extensionId ?? string.Empty,
                contextReference = contextReference,
                enabled = false,
                disabledReason = missingProviderMessage ?? string.Empty
            };
        }
    }

    public static class PungentAuthoringConversionActions
    {
        public static IEnumerable<PungentAuthoringAction> GetMissingExtensionPlaceholders(PungentAuthoringReference reference)
        {
            if (reference != null && (reference.itemKind == PungentAuthoringItemKind.LegacyNote || reference.itemKind == PungentAuthoringItemKind.Task))
            {
                yield return PungentAuthoringAction.MissingExtension(
                    PungentAuthoringActionKind.ConvertLegacyNoteToRichDocument,
                    "Convert to Rich Document",
                    PungentAuthoringItemKinds.GetMissingProviderMessage(PungentAuthoringItemKind.RichDocument),
                    reference,
                    PungentAuthoringPackageCapabilities.RichDocuments);

                yield return PungentAuthoringAction.MissingExtension(
                    PungentAuthoringActionKind.CreateBoardFromSelectedNotes,
                    "Create Board",
                    PungentAuthoringItemKinds.GetMissingProviderMessage(PungentAuthoringItemKind.Board),
                    reference,
                    PungentAuthoringPackageCapabilities.BoardWhiteboard);
            }

            if (reference != null && reference.itemKind == PungentAuthoringItemKind.AuditIssue)
            {
                yield return PungentAuthoringAction.MissingExtension(
                    PungentAuthoringActionKind.CreateDataSheetFromValidationResults,
                    "Create Data Sheet",
                    PungentAuthoringItemKinds.GetMissingProviderMessage(PungentAuthoringItemKind.DataSheet),
                    reference,
                    PungentAuthoringPackageCapabilities.DataSheet);
            }
        }
    }
#endif
}
