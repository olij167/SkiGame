using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Checklists
{
    public enum PungentChecklistValidationSeverity
    {
        Info = 0,
        Warning = 10,
        Error = 20
    }

    [Serializable]
    public sealed class PungentChecklistValidationIssue
    {
        public PungentChecklistValidationSeverity severity = PungentChecklistValidationSeverity.Info;
        public string code = string.Empty;
        public string message = string.Empty;
        public string checklistId = string.Empty;
        public string sectionId = string.Empty;
        public string itemId = string.Empty;
    }

    [Serializable]
    public sealed class PungentChecklistValidationResult
    {
        public List<PungentChecklistValidationIssue> issues = new List<PungentChecklistValidationIssue>();

        public bool HasErrors => issues != null && issues.Any(issue => issue != null && issue.severity == PungentChecklistValidationSeverity.Error);
        public bool HasWarnings => issues != null && issues.Any(issue => issue != null && issue.severity == PungentChecklistValidationSeverity.Warning);
        public string Summary => HasErrors ? "Checklist has errors." : HasWarnings ? "Checklist has warnings." : "Checklist is valid.";

        public void Add(PungentChecklistValidationSeverity severity, string code, string message, string checklistId = null, string sectionId = null, string itemId = null)
        {
            if (issues == null)
                issues = new List<PungentChecklistValidationIssue>();
            issues.Add(new PungentChecklistValidationIssue
            {
                severity = severity,
                code = code ?? string.Empty,
                message = message ?? string.Empty,
                checklistId = checklistId ?? string.Empty,
                sectionId = sectionId ?? string.Empty,
                itemId = itemId ?? string.Empty
            });
        }

        public string FirstErrorOrWarning()
        {
            if (issues == null || issues.Count == 0)
                return string.Empty;

            PungentChecklistValidationIssue issue = issues.FirstOrDefault(item => item != null && item.severity == PungentChecklistValidationSeverity.Error) ??
                                                   issues.FirstOrDefault(item => item != null && item.severity == PungentChecklistValidationSeverity.Warning) ??
                                                   issues.FirstOrDefault(item => item != null);
            return issue == null ? string.Empty : issue.message;
        }

        public string ToDisplayText(int maxIssues = 12)
        {
            if (issues == null || issues.Count == 0)
                return "Checklist is valid.";

            List<string> lines = new List<string>();
            foreach (PungentChecklistValidationIssue issue in issues.Where(item => item != null).Take(Math.Max(1, maxIssues)))
                lines.Add(issue.severity + " " + issue.code + ": " + issue.message);
            if (issues.Count > lines.Count)
                lines.Add("+" + (issues.Count - lines.Count) + " more issue(s).");
            return string.Join("\n", lines.ToArray());
        }
    }

    public static class PungentChecklistValidation
    {
        public static PungentChecklistValidationResult ValidateDefinition(
            PungentChecklistDefinition checklist,
            IEnumerable<string> knownChecklistIds = null,
            IEnumerable<string> knownLinkedStateKeys = null)
        {
            PungentChecklistValidationResult result = new PungentChecklistValidationResult();
            if (checklist == null)
            {
                result.Add(PungentChecklistValidationSeverity.Error, "MISSING_CHECKLIST", "Checklist definition is missing.");
                return result;
            }

            string checklistId = checklist.checklistId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(checklistId))
                result.Add(PungentChecklistValidationSeverity.Error, "MISSING_CHECKLIST_ID", "Checklist is missing checklistId.");
            if (string.IsNullOrWhiteSpace(checklist.title))
                result.Add(PungentChecklistValidationSeverity.Error, "MISSING_TITLE", "Checklist is missing title.", checklistId);
            if (!PungentChecklistListKinds.BuiltInKinds.Contains(PungentChecklistListKinds.Normalize(checklist.listKind)))
                result.Add(PungentChecklistValidationSeverity.Warning, "CUSTOM_LIST_KIND", "Checklist uses a custom list kind.", checklistId);

            PungentChecklistStateProfileDefinition profile = PungentChecklistProfiles.ResolveProfile(checklist);
            if (profile == null || profile.states == null || profile.states.Count == 0)
                result.Add(PungentChecklistValidationSeverity.Error, "UNKNOWN_STATE_PROFILE", "Checklist state profile could not be resolved.", checklistId);

            HashSet<string> sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> knownChildren = new HashSet<string>(knownChecklistIds ?? new string[0], StringComparer.OrdinalIgnoreCase);
            HashSet<string> knownLinks = new HashSet<string>(knownLinkedStateKeys ?? new string[0], StringComparer.OrdinalIgnoreCase);
            bool hasKnownChildren = knownChecklistIds != null;
            bool hasKnownLinks = knownLinkedStateKeys != null;

            if (checklist.sections == null || checklist.sections.Count == 0)
            {
                result.Add(PungentChecklistValidationSeverity.Error, "MISSING_SECTIONS", "Checklist must contain at least one section.", checklistId);
                return result;
            }

            foreach (PungentChecklistSectionDefinition section in checklist.sections)
            {
                if (section == null)
                {
                    result.Add(PungentChecklistValidationSeverity.Error, "NULL_SECTION", "Checklist contains a missing section.", checklistId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(section.id))
                    result.Add(PungentChecklistValidationSeverity.Error, "MISSING_SECTION_ID", "Section is missing an id.", checklistId);
                else if (!sectionIds.Add(section.id))
                    result.Add(PungentChecklistValidationSeverity.Error, "DUPLICATE_SECTION_ID", "Duplicate section ID '" + section.id + "'.", checklistId, section.id);
                if (string.IsNullOrWhiteSpace(section.title))
                    result.Add(PungentChecklistValidationSeverity.Warning, "MISSING_SECTION_TITLE", "Section '" + section.id + "' has no title.", checklistId, section.id);
                if (section.items == null || section.items.Count == 0)
                    result.Add(PungentChecklistValidationSeverity.Warning, "EMPTY_SECTION", "Section '" + section.id + "' has no items.", checklistId, section.id);

                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                {
                    if (item == null)
                    {
                        result.Add(PungentChecklistValidationSeverity.Error, "NULL_ITEM", "Section '" + section.id + "' contains a missing item.", checklistId, section.id);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.id))
                        result.Add(PungentChecklistValidationSeverity.Error, "MISSING_ITEM_ID", "Item is missing an id.", checklistId, section.id);
                    else if (!itemIds.Add(item.id))
                        result.Add(PungentChecklistValidationSeverity.Error, "DUPLICATE_ITEM_ID", "Duplicate item ID '" + item.id + "'.", checklistId, section.id, item.id);
                    if (string.IsNullOrWhiteSpace(item.label))
                        result.Add(PungentChecklistValidationSeverity.Warning, "MISSING_ITEM_LABEL", "Item '" + item.id + "' has no label.", checklistId, section.id, item.id);
                    if (!string.IsNullOrWhiteSpace(item.childChecklistId))
                    {
                        if (string.Equals(item.childChecklistId, checklist.checklistId, StringComparison.OrdinalIgnoreCase))
                            result.Add(PungentChecklistValidationSeverity.Error, "SELF_CHILD_CHECKLIST", "Item '" + item.id + "' cannot reference its own checklist as a child.", checklistId, section.id, item.id);
                        else if (hasKnownChildren && !knownChildren.Contains(item.childChecklistId))
                            result.Add(PungentChecklistValidationSeverity.Warning, "UNKNOWN_CHILD_CHECKLIST", "Item '" + item.id + "' references missing child checklist '" + item.childChecklistId + "'.", checklistId, section.id, item.id);
                    }
                    if (!string.IsNullOrWhiteSpace(item.linkedStateKey) && hasKnownLinks && !knownLinks.Contains(item.linkedStateKey))
                        result.Add(PungentChecklistValidationSeverity.Warning, "UNKNOWN_LINKED_STATE", "Item '" + item.id + "' references unknown linked state key '" + item.linkedStateKey + "'.", checklistId, section.id, item.id);
                    if (!string.IsNullOrWhiteSpace(item.dueUtc) && !DateTime.TryParse(item.dueUtc, out _))
                        result.Add(PungentChecklistValidationSeverity.Warning, "INVALID_DUE_DATE", "Item '" + item.id + "' has an invalid due date.", checklistId, section.id, item.id);
                }
            }

            return result;
        }
    }
}
