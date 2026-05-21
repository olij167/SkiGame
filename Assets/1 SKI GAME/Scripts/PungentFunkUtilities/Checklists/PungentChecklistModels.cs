using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PungentFunk.Utilities.Authoring;
using UnityEngine;

namespace PungentFunk.Utilities.Checklists
{
    public static class PungentChecklistConstants
    {
        public const int LegacySchemaVersion = 1;
        public const int SchemaVersionTwo = 2;
        public const int SchemaVersionThree = 3;
        public const int CurrentSchemaVersion = 4;

        public const string ProviderId = "checklists";
        public const string UtilityId = "qa-checklist-utility";
        public const string DataSheetChecklistId = "data-sheet-current-qa";
        public const string BoardGraphChecklistId = "board-graph-feature-test";
        public const string ParentStateComputedFromChild = "computedFromChild";
        public const string ChildPassRuleAllRequiredPass = "allRequiredPass";
        public const string LegacyLocalSourceProviderId = "legacy-local-prefs";
    }

    public enum PungentChecklistItemState
    {
        Untested = 0,
        Pass = 10,
        Partial = 20,
        Fail = 30
    }

    [Serializable]
    public sealed class PungentChecklistDatabase
    {
        public int schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
        public string lastSavedUtc = string.Empty;
        public List<PungentChecklistDefinition> checklists = new List<PungentChecklistDefinition>();
        public List<PungentLinkedStateGroupDefinition> linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();

        public void NormalizeInPlace()
        {
            schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
            if (checklists == null)
                checklists = new List<PungentChecklistDefinition>();
            if (linkedStateGroups == null)
                linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();

            for (int i = 0; i < checklists.Count; i++)
                checklists[i]?.NormalizeInPlace();
            for (int i = 0; i < linkedStateGroups.Count; i++)
                linkedStateGroups[i]?.NormalizeInPlace();

            checklists.RemoveAll(checklist => checklist == null || string.IsNullOrWhiteSpace(checklist.checklistId));
            linkedStateGroups.RemoveAll(group => group == null || string.IsNullOrWhiteSpace(group.linkedStateKey));

            DeduplicateChecklists();
            DeduplicateLinkedStateGroups();
            HydrateLinkedStateGroupsFromDefinitions();
        }

        public PungentChecklistDefinition FindChecklist(string checklistId)
        {
            string clean = PungentAuthoringId.Normalize(checklistId);
            return checklists.FirstOrDefault(item => item != null && PungentAuthoringId.EqualsId(item.checklistId, clean));
        }

        public void AddOrUpdate(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            checklist.NormalizeInPlace();
            if (string.IsNullOrWhiteSpace(checklist.checklistId))
                return;

            int index = checklists.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.checklistId, checklist.checklistId));
            PungentChecklistDefinition copy = PungentChecklistSerialization.Clone(checklist);
            if (index >= 0)
                checklists[index] = copy;
            else
                checklists.Add(copy);
            NormalizeInPlace();
        }

        public bool DeleteChecklist(string checklistId)
        {
            int removed = checklists.RemoveAll(item => item != null && PungentAuthoringId.EqualsId(item.checklistId, checklistId));
            if (removed > 0)
                NormalizeInPlace();
            return removed > 0;
        }

        private void DeduplicateChecklists()
        {
            for (int i = checklists.Count - 1; i >= 0; i--)
            {
                string id = checklists[i].checklistId;
                int first = checklists.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.checklistId, id));
                if (first >= 0 && first != i)
                    checklists.RemoveAt(i);
            }
        }

        private void DeduplicateLinkedStateGroups()
        {
            for (int i = linkedStateGroups.Count - 1; i >= 0; i--)
            {
                string key = linkedStateGroups[i].linkedStateKey;
                int first = linkedStateGroups.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.linkedStateKey, key));
                if (first >= 0 && first != i)
                    linkedStateGroups.RemoveAt(i);
            }
        }

        private void HydrateLinkedStateGroupsFromDefinitions()
        {
            foreach (PungentChecklistDefinition checklist in checklists)
            {
                foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.linkedStateKey))
                        continue;

                    if (linkedStateGroups.Any(group => group != null && PungentAuthoringId.EqualsId(group.linkedStateKey, item.linkedStateKey)))
                        continue;

                    linkedStateGroups.Add(new PungentLinkedStateGroupDefinition
                    {
                        linkedStateKey = item.linkedStateKey,
                        label = item.label,
                        ownerPackageId = string.IsNullOrWhiteSpace(item.canonicalOwnerPackageId) ? item.ownerPackageId : item.canonicalOwnerPackageId,
                        sourceProviderId = checklist.sourceProviderId,
                        sourceItemId = checklist.sourceItemId
                    });
                }
            }
        }
    }

    [Serializable]
    public sealed class PungentChecklistImportBundle
    {
        public int schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
        public string bundleId = string.Empty;
        public string title = string.Empty;
        public string sourceProviderId = string.Empty;
        public string sourceItemId = string.Empty;
        public List<PungentChecklistDefinition> checklists = new List<PungentChecklistDefinition>();
        public List<PungentLinkedStateGroupDefinition> linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();

        public void NormalizeInPlace()
        {
            schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
            bundleId = PungentAuthoringId.Normalize(bundleId);
            title = title == null ? string.Empty : title.Trim();
            sourceProviderId = sourceProviderId == null ? string.Empty : sourceProviderId.Trim();
            sourceItemId = PungentAuthoringId.Normalize(sourceItemId);
            if (checklists == null)
                checklists = new List<PungentChecklistDefinition>();
            if (linkedStateGroups == null)
                linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();
            for (int i = 0; i < checklists.Count; i++)
                checklists[i]?.NormalizeInPlace();
            for (int i = 0; i < linkedStateGroups.Count; i++)
                linkedStateGroups[i]?.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentChecklistDefinition
    {
        public int schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
        public string checklistId = string.Empty;
        public string title = "Untitled Checklist";
        public string description = string.Empty;
        public string defaultGuidance = string.Empty;
        public string targetUtilityId = string.Empty;
        public string sourceProviderId = string.Empty;
        public string sourceItemId = string.Empty;
        public string sourcePath = string.Empty;
        public string sourceLabel = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public string listKind = PungentChecklistListKinds.QualityGate;
        public string stateProfileId = PungentChecklistProfileIds.QualityGate;
        public bool archived;
        public bool locked;
        public string generatedBy = string.Empty;
        public string generatedTemplateId = string.Empty;
        public string generatedUtc = string.Empty;
        public int sortOrder;
        public List<string> tags = new List<string>();
        public List<PungentChecklistStateOptionDefinition> stateOptions = new List<PungentChecklistStateOptionDefinition>();
        public List<PungentChecklistSectionDefinition> sections = new List<PungentChecklistSectionDefinition>();

        public void NormalizeInPlace()
        {
            schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
            checklistId = PungentAuthoringId.Normalize(checklistId);
            title = string.IsNullOrWhiteSpace(title) ? "Untitled Checklist" : title.Trim();
            description = description == null ? string.Empty : description.Trim();
            defaultGuidance = defaultGuidance == null ? string.Empty : defaultGuidance.Trim();
            targetUtilityId = targetUtilityId == null ? string.Empty : targetUtilityId.Trim();
            sourceProviderId = sourceProviderId == null ? string.Empty : sourceProviderId.Trim();
            sourceItemId = PungentAuthoringId.Normalize(sourceItemId);
            sourcePath = sourcePath == null ? string.Empty : sourcePath.Trim();
            sourceLabel = sourceLabel == null ? string.Empty : sourceLabel.Trim();
            generatedBy = generatedBy == null ? string.Empty : generatedBy.Trim();
            generatedTemplateId = PungentAuthoringId.Normalize(generatedTemplateId);
            generatedUtc = NormalizeDateString(generatedUtc);
            listKind = PungentChecklistListKinds.Normalize(listKind);
            stateProfileId = string.IsNullOrWhiteSpace(stateProfileId)
                ? PungentChecklistProfiles.DefaultProfileIdForListKind(listKind)
                : PungentChecklistProfiles.NormalizeProfileId(stateProfileId);
            tags = PungentAuthoringMetadata.NormalizeTags(tags);
            if (stateOptions == null)
                stateOptions = new List<PungentChecklistStateOptionDefinition>();
            for (int i = 0; i < stateOptions.Count; i++)
                stateOptions[i]?.NormalizeInPlace();
            stateOptions.RemoveAll(option => option == null || string.IsNullOrWhiteSpace(option.stateId));
            createdUtc = NormalizeDateString(createdUtc);
            updatedUtc = NormalizeDateString(updatedUtc);
            if (string.IsNullOrWhiteSpace(createdUtc))
                createdUtc = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrWhiteSpace(updatedUtc))
                updatedUtc = createdUtc;

            if (sections == null)
                sections = new List<PungentChecklistSectionDefinition>();
            for (int i = 0; i < sections.Count; i++)
                sections[i]?.NormalizeInPlace();
            sections.RemoveAll(section => section == null || string.IsNullOrWhiteSpace(section.id));
        }

        public PungentAuthoringReference ToReference()
        {
            return PungentAuthoringReference.Create(PungentAuthoringItemKind.Checklist, checklistId, PungentChecklistConstants.ProviderId, title);
        }

        public void Touch()
        {
            updatedUtc = DateTime.UtcNow.ToString("o");
        }

        private static string NormalizeDateString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [Serializable]
    public sealed class PungentChecklistSectionDefinition
    {
        public string id = string.Empty;
        public string title = "Section";
        public string guidancePrompt = string.Empty;
        public bool archived;
        public int sortOrder;
        public string priority = string.Empty;
        public string owner = string.Empty;
        public string dueUtc = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public List<PungentChecklistItemDefinition> items = new List<PungentChecklistItemDefinition>();

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            title = string.IsNullOrWhiteSpace(title) ? "Section" : title.Trim();
            guidancePrompt = guidancePrompt == null ? string.Empty : guidancePrompt.Trim();
            priority = priority == null ? string.Empty : priority.Trim();
            owner = owner == null ? string.Empty : owner.Trim();
            dueUtc = dueUtc == null ? string.Empty : dueUtc.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? DateTime.UtcNow.ToString("o") : createdUtc.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? createdUtc : updatedUtc.Trim();
            if (items == null)
                items = new List<PungentChecklistItemDefinition>();
            for (int i = 0; i < items.Count; i++)
                items[i]?.NormalizeInPlace();
            items.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.id));
        }
    }

    [Serializable]
    public sealed class PungentChecklistItemDefinition
    {
        public string id = string.Empty;
        public string label = string.Empty;
        public string detail = string.Empty;
        public string linkedStateKey = string.Empty;
        public string ownerPackageId = string.Empty;
        public string canonicalOwnerPackageId = string.Empty;
        public List<string> appearsInPackageIds = new List<string>();
        public string childChecklistId = string.Empty;
        public string childPassRule = PungentChecklistConstants.ChildPassRuleAllRequiredPass;
        public string parentStateMode = string.Empty;
        public bool isOptional;
        public bool archived;
        public int sortOrder;
        public string priority = string.Empty;
        public string owner = string.Empty;
        public string dueUtc = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            label = label == null ? string.Empty : label.Trim();
            detail = detail == null ? string.Empty : detail.Trim();
            linkedStateKey = linkedStateKey == null ? string.Empty : linkedStateKey.Trim();
            ownerPackageId = ownerPackageId == null ? string.Empty : ownerPackageId.Trim();
            canonicalOwnerPackageId = canonicalOwnerPackageId == null ? string.Empty : canonicalOwnerPackageId.Trim();
            appearsInPackageIds = PungentAuthoringMetadata.NormalizeTags(appearsInPackageIds);
            childChecklistId = PungentAuthoringId.Normalize(childChecklistId);
            childPassRule = string.IsNullOrWhiteSpace(childPassRule) ? PungentChecklistConstants.ChildPassRuleAllRequiredPass : childPassRule.Trim();
            parentStateMode = parentStateMode == null ? string.Empty : parentStateMode.Trim();
            priority = priority == null ? string.Empty : priority.Trim();
            owner = owner == null ? string.Empty : owner.Trim();
            dueUtc = dueUtc == null ? string.Empty : dueUtc.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? DateTime.UtcNow.ToString("o") : createdUtc.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? createdUtc : updatedUtc.Trim();
        }
    }

    [Serializable]
    public sealed class PungentLinkedStateGroupDefinition
    {
        public string linkedStateKey = string.Empty;
        public string label = string.Empty;
        public string ownerPackageId = string.Empty;
        public string description = string.Empty;
        public string sourceProviderId = string.Empty;
        public string sourceItemId = string.Empty;

        public void NormalizeInPlace()
        {
            linkedStateKey = linkedStateKey == null ? string.Empty : linkedStateKey.Trim();
            label = label == null ? string.Empty : label.Trim();
            ownerPackageId = ownerPackageId == null ? string.Empty : ownerPackageId.Trim();
            description = description == null ? string.Empty : description.Trim();
            sourceProviderId = sourceProviderId == null ? string.Empty : sourceProviderId.Trim();
            sourceItemId = PungentAuthoringId.Normalize(sourceItemId);
        }
    }

    [Serializable]
    public sealed class PungentLinkedStateGroupList
    {
        public List<PungentLinkedStateGroupDefinition> groups = new List<PungentLinkedStateGroupDefinition>();
    }

    [Serializable]
    public sealed class PungentChecklistResultsExport
    {
        public int schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
        public string checklistId = string.Empty;
        public string title = string.Empty;
        public string listKind = string.Empty;
        public string stateProfileId = string.Empty;
        public string exportedUtc = string.Empty;
        public string followUpGuidance = string.Empty;
        public List<PungentChecklistResultItemExport> items = new List<PungentChecklistResultItemExport>();
    }

    [Serializable]
    public sealed class PungentChecklistResultItemExport
    {
        public string id = string.Empty;
        public string label = string.Empty;
        public string state = string.Empty;
        public string stateId = string.Empty;
        public string note = string.Empty;
        public string linkedStateKey = string.Empty;
        public bool isLinkedAppearance;
        public string ownerPackageId = string.Empty;
        public string canonicalOwnerPackageId = string.Empty;
        public string childChecklistId = string.Empty;
        public string parentStateMode = string.Empty;
        public string childPassRule = string.Empty;
    }

    public static class PungentChecklistSerialization
    {
        public static bool TryParseChecklistImport(
            string json,
            out List<PungentChecklistDefinition> checklists,
            out List<PungentLinkedStateGroupDefinition> linkedStateGroups,
            out string error)
        {
            checklists = new List<PungentChecklistDefinition>();
            linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Checklist JSON is empty.";
                return false;
            }

            if (LooksLikeBundle(json))
                return TryParseBundle(json, out checklists, out linkedStateGroups, out error);

            if (!TryParseChecklist(json, out PungentChecklistDefinition checklist, out error))
                return false;

            checklists.Add(checklist);
            return true;
        }

        public static bool TryParseChecklist(string json, out PungentChecklistDefinition checklist, out string error)
        {
            checklist = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Checklist JSON is empty.";
                return false;
            }

            try
            {
                checklist = JsonUtility.FromJson<PungentChecklistDefinition>(json);
            }
            catch (Exception ex)
            {
                error = "Checklist JSON could not be parsed.\n\n" + ex.Message;
                return false;
            }

            return ValidateChecklist(checklist, out error);
        }

        public static bool ValidateChecklist(PungentChecklistDefinition checklist, out string error)
        {
            error = string.Empty;
            if (checklist == null)
            {
                error = "Checklist JSON did not contain a checklist object.";
                return false;
            }

            if (checklist.schemaVersion != PungentChecklistConstants.LegacySchemaVersion &&
                checklist.schemaVersion != PungentChecklistConstants.SchemaVersionTwo &&
                checklist.schemaVersion != PungentChecklistConstants.SchemaVersionThree &&
                checklist.schemaVersion != PungentChecklistConstants.CurrentSchemaVersion)
            {
                error = "Unsupported checklist schema version '" + checklist.schemaVersion + "'. Expected version 1, 2, 3, or 4.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(checklist.checklistId))
            {
                error = "Checklist is missing checklistId.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(checklist.title))
            {
                error = "Checklist is missing title.";
                return false;
            }

            if (checklist.sections == null || checklist.sections.Count == 0)
            {
                error = "Checklist must contain at least one section.";
                return false;
            }

            HashSet<string> itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int s = 0; s < checklist.sections.Count; s++)
            {
                PungentChecklistSectionDefinition section = checklist.sections[s];
                if (section == null || string.IsNullOrWhiteSpace(section.id) || string.IsNullOrWhiteSpace(section.title))
                {
                    error = "Checklist section " + (s + 1) + " is missing id or title.";
                    return false;
                }

                if (section.items == null || section.items.Count == 0)
                {
                    error = "Checklist section '" + section.title + "' has no items.";
                    return false;
                }

                for (int i = 0; i < section.items.Count; i++)
                {
                    PungentChecklistItemDefinition item = section.items[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.label))
                    {
                        error = "Checklist section '" + section.title + "' contains an item missing id or label.";
                        return false;
                    }

                    if (!itemIds.Add(item.id))
                    {
                        error = "Checklist contains duplicate item ID '" + item.id + "'. Use unique appearance IDs and the same linkedStateKey for linked duplicate checklist elements.";
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(item.childChecklistId) &&
                        string.Equals(item.childChecklistId, checklist.checklistId, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Checklist item '" + item.id + "' cannot use its own checklist as childChecklistId.";
                        return false;
                    }
                }
            }

            PungentChecklistValidationResult detailed = PungentChecklistValidation.ValidateDefinition(checklist);
            if (detailed.HasErrors)
            {
                error = detailed.FirstErrorOrWarning();
                return false;
            }

            return true;
        }

        public static IEnumerable<PungentChecklistItemDefinition> EnumerateItems(PungentChecklistDefinition checklist)
        {
            if (checklist == null || checklist.sections == null)
                yield break;

            foreach (PungentChecklistSectionDefinition section in checklist.sections)
            {
                if (section == null || section.items == null)
                    continue;

                foreach (PungentChecklistItemDefinition item in section.items)
                    if (item != null)
                        yield return item;
            }
        }

        public static PungentChecklistDefinition Clone(PungentChecklistDefinition source)
        {
            if (source == null)
                return null;

            PungentChecklistDefinition clone = JsonUtility.FromJson<PungentChecklistDefinition>(JsonUtility.ToJson(source, false));
            clone?.NormalizeInPlace();
            return clone;
        }

        public static PungentLinkedStateGroupDefinition Clone(PungentLinkedStateGroupDefinition source)
        {
            if (source == null)
                return null;

            PungentLinkedStateGroupDefinition clone = JsonUtility.FromJson<PungentLinkedStateGroupDefinition>(JsonUtility.ToJson(source, false));
            clone?.NormalizeInPlace();
            return clone;
        }

        public static string ToJson(PungentChecklistDefinition checklist, bool prettyPrint)
        {
            PungentChecklistDefinition copy = Clone(checklist);
            if (copy != null)
            {
                copy.schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
                copy.NormalizeInPlace();
            }

            return JsonUtility.ToJson(copy, prettyPrint);
        }

        public static PungentChecklistDefinition CreateDefinitionTemplate()
        {
            PungentChecklistDefinition checklist = new PungentChecklistDefinition
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                checklistId = "example-checklist",
                title = "Example Checklist",
                description = "Replace this with a short description of when this checklist should be used.",
                defaultGuidance = "Use partial or failed items as follow-up work.",
                listKind = PungentChecklistListKinds.QualityGate,
                stateProfileId = PungentChecklistProfileIds.QualityGate,
                targetUtilityId = PungentChecklistConstants.UtilityId,
                sourceProviderId = PungentChecklistConstants.ProviderId,
                sourceLabel = "JSON Template",
                tags = new List<string> { "example", "checklist" },
                sections = new List<PungentChecklistSectionDefinition>
                {
                    new PungentChecklistSectionDefinition
                    {
                        id = "setup",
                        title = "Setup",
                        guidancePrompt = "Resolve setup failures before continuing.",
                        items = new List<PungentChecklistItemDefinition>
                        {
                            new PungentChecklistItemDefinition
                            {
                                id = "setup-01",
                                label = "Open the target workflow.",
                                detail = "Name the exact window, inspector, scene, or asset state to verify."
                            },
                            new PungentChecklistItemDefinition
                            {
                                id = "setup-02",
                                label = "Confirm the expected data is visible.",
                                detail = "List the fields, rows, references, or assets that should be present."
                            }
                        }
                    },
                    new PungentChecklistSectionDefinition
                    {
                        id = "validation",
                        title = "Validation",
                        guidancePrompt = "Capture failed validation as follow-up notes.",
                        items = new List<PungentChecklistItemDefinition>
                        {
                            new PungentChecklistItemDefinition
                            {
                                id = "validation-01",
                                label = "Run the main validation path.",
                                detail = "Mark partial if the workflow succeeds but needs polish."
                            }
                        }
                    }
                }
            };
            checklist.NormalizeInPlace();
            return checklist;
        }

        public static PungentChecklistImportBundle CreateBundleTemplate()
        {
            PungentChecklistImportBundle bundle = new PungentChecklistImportBundle
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                bundleId = "example-checklist-bundle",
                title = "Example Checklist Bundle",
                sourceProviderId = PungentChecklistConstants.ProviderId,
                checklists = new List<PungentChecklistDefinition> { CreateDefinitionTemplate() },
                linkedStateGroups = new List<PungentLinkedStateGroupDefinition>
                {
                    new PungentLinkedStateGroupDefinition
                    {
                        linkedStateKey = "shared-example-state",
                        label = "Shared Example State",
                        description = "Use linked state when the same requirement appears in more than one checklist."
                    }
                }
            };
            bundle.NormalizeInPlace();
            return bundle;
        }

        public static string Slug(string value, string fallback)
        {
            string text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            StringBuilder builder = new StringBuilder();
            bool lastWasDash = false;
            foreach (char c in text.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    builder.Append(c);
                    lastWasDash = false;
                }
                else if (!lastWasDash)
                {
                    builder.Append('-');
                    lastWasDash = true;
                }
            }

            string result = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        public static string UniqueId(string preferred, ISet<string> used, string fallback)
        {
            string baseId = Slug(preferred, fallback);
            string candidate = baseId;
            int suffix = 2;
            while (used.Contains(candidate))
            {
                candidate = baseId + "-" + suffix.ToString();
                suffix++;
            }

            used.Add(candidate);
            return candidate;
        }

        private static bool TryParseBundle(
            string json,
            out List<PungentChecklistDefinition> checklists,
            out List<PungentLinkedStateGroupDefinition> linkedStateGroups,
            out string error)
        {
            checklists = new List<PungentChecklistDefinition>();
            linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();
            error = string.Empty;

            PungentChecklistImportBundle bundle;
            try
            {
                bundle = JsonUtility.FromJson<PungentChecklistImportBundle>(json);
            }
            catch (Exception ex)
            {
                error = "Checklist bundle JSON could not be parsed.\n\n" + ex.Message;
                return false;
            }

            if (bundle == null)
            {
                error = "Checklist bundle JSON did not contain a bundle object.";
                return false;
            }

            if (bundle.schemaVersion != PungentChecklistConstants.SchemaVersionTwo &&
                bundle.schemaVersion != PungentChecklistConstants.SchemaVersionThree &&
                bundle.schemaVersion != PungentChecklistConstants.CurrentSchemaVersion)
            {
                error = "Unsupported checklist bundle schema version '" + bundle.schemaVersion + "'. Expected version 2, 3, or 4.";
                return false;
            }

            if (bundle.checklists == null || bundle.checklists.Count == 0)
            {
                error = "Checklist bundle must contain at least one checklist.";
                return false;
            }

            HashSet<string> checklistIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < bundle.checklists.Count; i++)
            {
                PungentChecklistDefinition checklist = bundle.checklists[i];
                if (!ValidateChecklist(checklist, out error))
                    return false;
                if (!checklistIds.Add(checklist.checklistId))
                {
                    error = "Checklist bundle contains duplicate checklist ID '" + checklist.checklistId + "'.";
                    return false;
                }

                checklists.Add(checklist);
            }

            if (bundle.linkedStateGroups != null)
                linkedStateGroups.AddRange(bundle.linkedStateGroups.Where(group => group != null && !string.IsNullOrWhiteSpace(group.linkedStateKey)));

            return true;
        }

        private static bool LooksLikeBundle(string json)
        {
            return json.IndexOf("\"checklists\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   json.IndexOf("\"bundleId\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public static class PungentChecklistStorage
    {
        public const string RelativeStoragePath = "ProjectSettings/PungentFunkUtilities/Checklists.json";

        private static PungentChecklistDatabase _database;
        private static string _storagePath = string.Empty;
        private static string _lastError = string.Empty;

        public static PungentChecklistDatabase Database
        {
            get
            {
                EnsureLoaded();
                return _database;
            }
        }

        public static string StoragePath
        {
            get
            {
                EnsureLoaded();
                return _storagePath;
            }
        }

        public static string LastError => _lastError;

        public static void EnsureLoaded(string projectRootPath = null)
        {
            if (_database != null)
                return;

            Reload(projectRootPath);
        }

        public static void Reload(string projectRootPath = null)
        {
            _storagePath = ResolveStoragePath(projectRootPath);
            _database = LoadFromPath(_storagePath, out _lastError);
            _database.NormalizeInPlace();
        }

        public static bool Save(out string error, string projectRootPath = null)
        {
            EnsureLoaded(projectRootPath);

            if (!string.IsNullOrWhiteSpace(projectRootPath))
                _storagePath = ResolveStoragePath(projectRootPath);

            try
            {
                _database.NormalizeInPlace();
                _database.lastSavedUtc = DateTime.UtcNow.ToString("o");

                string directory = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_storagePath, JsonUtility.ToJson(_database, true), new UTF8Encoding(false));
                error = string.Empty;
                _lastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = "Unable to save checklists: " + ex.Message;
                _lastError = error;
                return false;
            }
        }

        public static bool UpsertChecklist(PungentChecklistDefinition checklist, out string error, bool saveImmediately = true)
        {
            error = string.Empty;
            if (checklist == null)
            {
                error = "Checklist definition is missing.";
                return false;
            }

            if (!PungentChecklistSerialization.ValidateChecklist(checklist, out error))
                return false;

            Database.AddOrUpdate(checklist);
            if (saveImmediately)
                return Save(out error);

            return true;
        }

        public static bool TryDeleteChecklist(string checklistId, out string error, bool saveImmediately = true)
        {
            error = string.Empty;
            bool removed = Database.DeleteChecklist(checklistId);
            if (!removed)
            {
                error = "Checklist could not be found.";
                return false;
            }

            if (saveImmediately)
                return Save(out error);

            return true;
        }

        private static PungentChecklistDatabase LoadFromPath(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new PungentChecklistDatabase();

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return new PungentChecklistDatabase();

                PungentChecklistDatabase database = JsonUtility.FromJson<PungentChecklistDatabase>(json);
                return database ?? new PungentChecklistDatabase();
            }
            catch (Exception ex)
            {
                error = "Unable to load checklists: " + ex.Message;
                return new PungentChecklistDatabase();
            }
        }

        private static string ResolveStoragePath(string projectRootPath)
        {
            string root = projectRootPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                string dataPath = Application.dataPath;
                DirectoryInfo parent = string.IsNullOrWhiteSpace(dataPath) ? null : Directory.GetParent(dataPath);
                root = parent != null ? parent.FullName : Directory.GetCurrentDirectory();
            }

            return Path.Combine(root, RelativeStoragePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
