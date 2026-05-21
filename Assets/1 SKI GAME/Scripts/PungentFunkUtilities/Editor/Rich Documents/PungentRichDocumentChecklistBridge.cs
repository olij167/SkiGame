using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentChecklistBridge
    {
        public static bool TryBuildChecklistDefinition(
            PungentRichDocument document,
            out PungentChecklistDefinition checklist,
            out List<PungentLinkedStateGroupDefinition> linkedStateGroups,
            out string error)
        {
            checklist = null;
            linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();
            error = string.Empty;
            if (document == null)
            {
                error = "Rich Document is missing.";
                return false;
            }

            string body = document.bodyText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(body) && document.blocks != null && document.blocks.Count > 0)
                body = BuildBodyFromBlocks(document.blocks);

            if (string.IsNullOrWhiteSpace(body))
            {
                error = "Rich Document has no body text to convert.";
                return false;
            }

            Dictionary<string, string> metadata = ReadMetadata(body);
            string checklistId = metadata.TryGetValue("checklist id", out string explicitId) ? explicitId : string.Empty;
            if (string.IsNullOrWhiteSpace(checklistId))
                checklistId = PungentChecklistSerialization.Slug(document.title, document.id);

            checklist = new PungentChecklistDefinition
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                checklistId = checklistId,
                title = string.IsNullOrWhiteSpace(document.title) ? "Checklist Definition" : document.title,
                description = string.IsNullOrWhiteSpace(document.summary) ? "Checklist generated from Rich Document '" + document.title + "'." : document.summary,
                targetUtilityId = metadata.TryGetValue("target utility id", out string targetUtilityId) ? targetUtilityId : string.Empty,
                listKind = metadata.TryGetValue("list kind", out string listKind) ? listKind : PungentChecklistListKinds.QualityGate,
                stateProfileId = metadata.TryGetValue("state profile", out string stateProfile) ? stateProfile : PungentChecklistProfileIds.QualityGate,
                sourceProviderId = PungentRichDocumentProvider.Id,
                sourceItemId = document.id,
                sourceLabel = document.title,
                tags = metadata.TryGetValue("tags", out string tags) ? SplitTags(tags) : PungentAuthoringMetadata.NormalizeTags(document.tags),
                sections = new List<PungentChecklistSectionDefinition>()
            };

            PungentChecklistSectionDefinition currentSection = null;
            HashSet<string> usedSectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine == null ? string.Empty : rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int headingLevel = HeadingLevel(line);
                if (headingLevel > 0)
                {
                    string title = line.Substring(headingLevel).Trim();
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    if (headingLevel == 1 && checklist.title == document.title)
                        checklist.title = title;
                    if (headingLevel >= 2)
                        currentSection = AddSection(checklist, title, usedSectionIds);
                    continue;
                }

                if (!TryParseChecklistLine(line, out string label, out Dictionary<string, string> itemMetadata))
                    continue;

                if (currentSection == null)
                    currentSection = AddSection(checklist, "Checklist", usedSectionIds);

                string explicitItemId = itemMetadata.TryGetValue("id", out string itemId) ? itemId : currentSection.id + "-" + label;
                PungentChecklistItemDefinition item = new PungentChecklistItemDefinition
                {
                    id = PungentChecklistSerialization.UniqueId(explicitItemId, usedItemIds, "item"),
                    label = label,
                    detail = itemMetadata.TryGetValue("detail", out string detail) ? detail : string.Empty,
                    owner = itemMetadata.TryGetValue("owner", out string owner) ? owner : string.Empty,
                    priority = itemMetadata.TryGetValue("priority", out string priority) ? priority : string.Empty,
                    dueUtc = itemMetadata.TryGetValue("due", out string due) ? due : string.Empty,
                    isOptional = itemMetadata.TryGetValue("optional", out string optional) && ParseBool(optional),
                    archived = itemMetadata.TryGetValue("archived", out string archived) && ParseBool(archived),
                    linkedStateKey = itemMetadata.TryGetValue("linked", out string linked) ? linked : string.Empty,
                    ownerPackageId = itemMetadata.TryGetValue("package", out string package) ? package : string.Empty,
                    childChecklistId = itemMetadata.TryGetValue("child", out string child) ? child : string.Empty,
                    childPassRule = itemMetadata.TryGetValue("child-pass-rule", out string childRule) ? childRule : PungentChecklistConstants.ChildPassRuleAllRequiredPass
                };
                if (!string.IsNullOrWhiteSpace(item.childChecklistId))
                    item.parentStateMode = PungentChecklistConstants.ParentStateComputedFromChild;
                currentSection.items.Add(item);
            }

            checklist.NormalizeInPlace();
            if (!PungentChecklistSerialization.ValidateChecklist(checklist, out error))
                return false;

            return true;
        }

        public static bool CreateOrUpdateChecklistDefinition(PungentRichDocument document, out string checklistId, out string error)
        {
            checklistId = string.Empty;
            if (!TryBuildChecklistDefinition(document, out PungentChecklistDefinition checklist, out List<PungentLinkedStateGroupDefinition> linkedStateGroups, out error))
                return false;

            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(checklist, linkedStateGroups, out error))
                return false;

            checklistId = checklist.checklistId;
            EnsureChecklistReference(document, checklist);
            return true;
        }

        private static PungentChecklistSectionDefinition AddSection(PungentChecklistDefinition checklist, string title, HashSet<string> usedSectionIds)
        {
            PungentChecklistSectionDefinition section = new PungentChecklistSectionDefinition
            {
                id = PungentChecklistSerialization.UniqueId(title, usedSectionIds, "section"),
                title = title,
                guidancePrompt = "Review partial or failed items in " + title + ".",
                items = new List<PungentChecklistItemDefinition>()
            };
            checklist.sections.Add(section);
            return section;
        }

        private static Dictionary<string, string> ReadMetadata(string body)
        {
            Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine == null ? string.Empty : rawLine.Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (!metadata.ContainsKey(key) && IsKnownMetadataKey(key))
                    metadata.Add(key, value);
            }

            return metadata;
        }

        private static bool IsKnownMetadataKey(string key)
        {
            return string.Equals(key, "Checklist ID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "Target Utility ID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "List Kind", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "State Profile", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "Tags", StringComparison.OrdinalIgnoreCase);
        }

        private static int HeadingLevel(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] != '#')
                return 0;

            int count = 0;
            while (count < line.Length && line[count] == '#')
                count++;
            return count < line.Length && char.IsWhiteSpace(line[count]) ? count : 0;
        }

        private static bool TryParseChecklistLine(string line, out string label, out Dictionary<string, string> metadata)
        {
            label = string.Empty;
            metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(line) || line.Length < 6)
                return false;

            string trimmed = line.Trim();
            if (!(trimmed.StartsWith("- [ ] ", StringComparison.Ordinal) ||
                  trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) ||
                  trimmed.StartsWith("* [ ] ", StringComparison.Ordinal) ||
                  trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase)))
                return false;

            string content = trimmed.Substring(6).Trim();
            string[] parts = content.Split('|');
            label = parts.Length == 0 ? content : parts[0].Trim();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i] == null ? string.Empty : parts[i].Trim();
                int colon = part.IndexOf(':');
                if (colon <= 0)
                    continue;

                string key = part.Substring(0, colon).Trim().ToLowerInvariant();
                string value = part.Substring(colon + 1).Trim();
                if (!metadata.ContainsKey(key))
                    metadata.Add(key, value);
            }
            return !string.IsNullOrWhiteSpace(label);
        }

        private static bool ParseBool(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "x", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildBodyFromBlocks(IEnumerable<PungentRichDocumentBlock> blocks)
        {
            List<string> lines = new List<string>();
            foreach (PungentRichDocumentBlock block in blocks ?? Enumerable.Empty<PungentRichDocumentBlock>())
            {
                if (block == null)
                    continue;

                if (block.type == PungentRichDocumentBlockType.Heading)
                    lines.Add(new string('#', Math.Max(1, block.headingLevel)) + " " + block.text);
                else if (block.type == PungentRichDocumentBlockType.Checklist)
                {
                    foreach (PungentRichDocumentBlockItem item in block.items ?? new List<PungentRichDocumentBlockItem>())
                        if (item != null && !string.IsNullOrWhiteSpace(item.text))
                            lines.Add("- [ ] " + item.text.Trim());
                }
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static void EnsureChecklistReference(PungentRichDocument document, PungentChecklistDefinition checklist)
        {
            if (document == null || checklist == null)
                return;

            if (document.references == null)
                document.references = new List<PungentAuthoringReference>();
            if (document.references.Any(reference => reference != null &&
                                                    reference.itemKind == PungentAuthoringItemKind.Checklist &&
                                                    PungentAuthoringId.EqualsId(reference.itemId, checklist.checklistId)))
                return;

            document.references.Add(checklist.ToReference());
            document.Touch();
        }

        private static List<string> SplitTags(string value)
        {
            return PungentAuthoringMetadata.NormalizeTags((value ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim()));
        }
    }
#endif
}
