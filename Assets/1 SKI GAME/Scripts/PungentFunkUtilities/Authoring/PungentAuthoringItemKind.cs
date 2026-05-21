namespace PungentFunk.Utilities.Authoring
{
    public enum PungentAuthoringItemKind
    {
        Unknown = 0,
        Custom = 1,
        LegacyNote = 10,
        RichDocument = 20,
        Board = 30,
        DataSheet = 40,
        Checklist = 45,
        Task = 50,
        DocumentationLink = 60,
        HelpTopic = 70,
        TokenDefinition = 80,
        AuditIssue = 90,
        Utility = 100,
        FutureUtility = 110,
        ExternalReference = 120
    }

    public static class PungentAuthoringItemKinds
    {
        public static string GetDisplayName(PungentAuthoringItemKind kind, string customKind = null)
        {
            switch (kind)
            {
                case PungentAuthoringItemKind.Custom: return string.IsNullOrWhiteSpace(customKind) ? "Custom Item" : customKind.Trim();
                case PungentAuthoringItemKind.LegacyNote: return "Legacy Note";
                case PungentAuthoringItemKind.RichDocument: return "Rich Document";
                case PungentAuthoringItemKind.Board: return "Board";
                case PungentAuthoringItemKind.DataSheet: return "Data Sheet";
                case PungentAuthoringItemKind.Checklist: return "Checklist";
                case PungentAuthoringItemKind.Task: return "Task";
                case PungentAuthoringItemKind.DocumentationLink: return "Documentation Link";
                case PungentAuthoringItemKind.HelpTopic: return "Help Topic";
                case PungentAuthoringItemKind.TokenDefinition: return "Token Definition";
                case PungentAuthoringItemKind.AuditIssue: return "Audit Issue";
                case PungentAuthoringItemKind.Utility: return "Utility";
                case PungentAuthoringItemKind.FutureUtility: return "Future Utility";
                case PungentAuthoringItemKind.ExternalReference: return "External Reference";
                default: return "Unknown Item";
            }
        }

        public static string GetMissingProviderMessage(PungentAuthoringItemKind kind, string customKind = null)
        {
            string label = GetDisplayName(kind, customKind);
            return label + " provider is not installed.";
        }

        public static bool IsFutureExtensionKind(PungentAuthoringItemKind kind)
        {
            return kind == PungentAuthoringItemKind.RichDocument ||
                   kind == PungentAuthoringItemKind.Board ||
                   kind == PungentAuthoringItemKind.DataSheet ||
                   kind == PungentAuthoringItemKind.Checklist ||
                   kind == PungentAuthoringItemKind.Custom;
        }
    }
}
