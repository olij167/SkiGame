using System;

namespace PungentFunk.Utilities.Authoring
{
    public static class PungentAuthoringPackageCapabilities
    {
        public const string Core = "core";
        public const string AuthoringCore = "authoring-core";
        public const string NotesBrowser = "notes-browser";
        public const string RichDocuments = "rich-documents";
        public const string BoardWhiteboard = "board-whiteboard";
        public const string DataSheet = "data-sheet";
        public const string Checklists = "checklists";
        public const string ProjectAudit = "project-audit";
        public const string TokenSystem = "token-system";
        public const string HelpDocumentation = "help-documentation";
        public const string UtilityCore = "utility-core";
        public const string Custom = "custom";

        public static string Normalize(string capabilityId)
        {
            return string.IsNullOrWhiteSpace(capabilityId) ? Custom : capabilityId.Trim();
        }

        public static string GetDisplayName(string capabilityId)
        {
            switch (Normalize(capabilityId))
            {
                case Core: return "Core";
                case AuthoringCore: return "Authoring Core";
                case NotesBrowser: return "Notes Browser";
                case RichDocuments: return "Rich Documents";
                case BoardWhiteboard: return "Board / Whiteboard";
                case DataSheet: return "Data Sheet";
                case Checklists: return "Checklists";
                case ProjectAudit: return "Project Audit";
                case TokenSystem: return "Token System";
                case HelpDocumentation: return "Help / Documentation";
                case UtilityCore: return "Utility Core";
                default: return "Custom Extension";
            }
        }
    }

    [Serializable]
    public sealed class PungentAuthoringPackageCapability
    {
        public string id = PungentAuthoringPackageCapabilities.Custom;
        public string displayName = "Custom Extension";
        public string extensionId = string.Empty;
        public bool installed = true;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringPackageCapabilities.Normalize(id);
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? PungentAuthoringPackageCapabilities.GetDisplayName(id)
                : displayName.Trim();
            extensionId = extensionId == null ? string.Empty : extensionId.Trim();
        }
    }
}
