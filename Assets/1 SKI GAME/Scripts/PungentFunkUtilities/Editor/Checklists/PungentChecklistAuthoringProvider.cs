using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.Checklists
{
#if UNITY_EDITOR
    public sealed class PungentChecklistAuthoringProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider,
        IPungentAuthoringValidator
    {
        public const string Id = PungentChecklistConstants.ProviderId;

        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.Checklist };

        public string ProviderId => Id;
        public string DisplayName => "Checklists";
        public IReadOnlyList<PungentAuthoringItemKind> SupportedKinds => Kinds;
        public PungentAuthoringProviderCapabilities Capabilities =>
            PungentAuthoringProviderCapabilities.EnumerateItems |
            PungentAuthoringProviderCapabilities.Metadata |
            PungentAuthoringProviderCapabilities.References |
            PungentAuthoringProviderCapabilities.Targets |
            PungentAuthoringProviderCapabilities.Preview |
            PungentAuthoringProviderCapabilities.Open |
            PungentAuthoringProviderCapabilities.Edit |
            PungentAuthoringProviderCapabilities.Copy |
            PungentAuthoringProviderCapabilities.Validate;
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.Checklists;
        public string ExtensionId => PungentAuthoringPackageCapabilities.Checklists;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentChecklistDefinition checklist in PungentChecklistDefinitionRegistry.AllDefinitions)
                if (checklist != null)
                    yield return ToMetadata(checklist);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
                return false;

            metadata = ToMetadata(checklist);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
                yield break;

            if (!string.IsNullOrWhiteSpace(checklist.sourceProviderId) && !string.IsNullOrWhiteSpace(checklist.sourceItemId))
            {
                PungentAuthoringReference source = PungentAuthoringReference.Create(PungentAuthoringItemKind.Custom, checklist.sourceItemId, checklist.sourceProviderId, checklist.sourceLabel);
                source.customKind = string.IsNullOrWhiteSpace(checklist.sourceLabel) ? "Checklist Source" : checklist.sourceLabel;
                yield return source;
            }
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
                yield break;

            yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.ChecklistId, checklist.checklistId, checklist.title, Id);
            if (!string.IsNullOrWhiteSpace(checklist.targetUtilityId))
                yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.UtilityId, checklist.targetUtilityId, "Target Utility", "utilities");
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(checklist), GetTargets(reference).Count());
            preview.bodyPreview = PreviewText(checklist);
            preview.primaryActionLabels.Add("Open Checklist");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = string.Empty;
            if (Find(reference) == null)
            {
                reason = "Checklist could not be found.";
                return false;
            }

            return true;
        }

        public bool Open(PungentAuthoringReference reference)
        {
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
                return false;

            PungentChecklistUtilityWindow.OpenChecklist(checklist.checklistId);
            return true;
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            return CanOpen(reference, out reason);
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
                return false;

            PungentChecklistUtilityWindow.OpenChecklistEdit(checklist.checklistId);
            return true;
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Checklist creation is available from JSON import, Data Sheet templates, and Rich Document templates.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Checklist creation is available from JSON import, Data Sheet templates, and Rich Document templates.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = string.Empty;
            error = string.Empty;
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
            {
                error = "Checklist could not be found.";
                return false;
            }

            copiedValue = PungentChecklistSerialization.ToJson(checklist, true);
            EditorGUIUtility.systemCopyBuffer = copiedValue;
            return true;
        }

        public PungentAuthoringValidationResult ValidateReference(PungentAuthoringReference reference)
        {
            PungentChecklistDefinition checklist = Find(reference);
            if (checklist == null)
                return Missing(reference);

            return PungentChecklistSerialization.ValidateChecklist(checklist, out string error)
                ? PungentAuthoringValidationResult.CreateValid(Id, checklist.checklistId)
                : Invalid(checklist.checklistId, error);
        }

        public PungentAuthoringValidationResult ValidateTarget(PungentAuthoringTarget target)
        {
            if (target == null || target.targetKind != PungentAuthoringTargetKind.ChecklistId)
                return PungentAuthoringValidationResult.CreateValid(Id, target != null ? target.rawValue : string.Empty);

            return ValidateReference(PungentAuthoringReference.Create(PungentAuthoringItemKind.Checklist, target.rawValue, Id, target.label));
        }

        public static PungentAuthoringMetadata ToMetadata(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return null;

            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = checklist.checklistId,
                title = checklist.title,
                summary = ChecklistSummary(checklist),
                kind = PungentAuthoringItemKind.Checklist,
                customKind = PungentChecklistListKinds.GetDisplayName(checklist.listKind) + " Checklist",
                status = checklist.archived ? "Archived" : SourceState(checklist),
                priority = "Normal",
                visibility = PungentChecklistDefinitionRegistry.IsProviderDefinitionId(checklist.checklistId) ? "Package" : "Project",
                tags = PungentAuthoringMetadata.NormalizeTags(checklist.tags),
                createdUtc = checklist.createdUtc,
                updatedUtc = checklist.updatedUtc,
                archived = checklist.archived,
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.Checklists,
                extensionId = PungentAuthoringPackageCapabilities.Checklists
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        private static PungentChecklistDefinition Find(PungentAuthoringReference reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.itemId))
                return null;

            return PungentChecklistDefinitionRegistry.Find(reference.itemId);
        }

        private static string PreviewText(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return string.Empty;

            return ChecklistCounts(checklist) +
                   "\nKind: " + PungentChecklistListKinds.GetDisplayName(checklist.listKind) +
                   "\nProgress: " + ProgressText(checklist) +
                   (string.IsNullOrWhiteSpace(checklist.targetUtilityId) ? string.Empty : "\nTarget: " + checklist.targetUtilityId) +
                   (string.IsNullOrWhiteSpace(checklist.sourceLabel) ? string.Empty : "\nSource: " + checklist.sourceLabel);
        }

        private static string ChecklistSummary(PungentChecklistDefinition checklist)
        {
            string description = string.IsNullOrWhiteSpace(checklist.description) ? ChecklistCounts(checklist) : checklist.description;
            return description + " | " + PungentChecklistListKinds.GetDisplayName(checklist.listKind) + " | " + ProgressText(checklist);
        }

        private static string ProgressText(PungentChecklistDefinition checklist)
        {
            PungentChecklistProgressSummary summary = PungentChecklistUtilityStateService.Count(checklist, PungentChecklistDefinitionRegistry.Find, false);
            return summary.complete + "/" + summary.total + " complete" + (summary.problem > 0 ? " | " + summary.problem + " attention" : string.Empty);
        }

        private static string SourceState(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return "Missing";
            if (PungentChecklistDefinitionRegistry.IsProviderDefinitionId(checklist.checklistId))
                return "Package";
            if (PungentChecklistDefinitionRegistry.IsProjectDefinitionId(checklist.checklistId))
                return "Project";
            if (string.Equals(checklist.sourceProviderId, PungentChecklistConstants.LegacyLocalSourceProviderId, System.StringComparison.OrdinalIgnoreCase))
                return "Legacy Local";
            return "Ready";
        }

        private static string ChecklistCounts(PungentChecklistDefinition checklist)
        {
            int sectionCount = checklist == null || checklist.sections == null ? 0 : checklist.sections.Count;
            int itemCount = PungentChecklistSerialization.EnumerateItems(checklist).Count();
            return sectionCount + " section" + (sectionCount == 1 ? string.Empty : "s") + " | " + itemCount + " item" + (itemCount == 1 ? string.Empty : "s");
        }

        private static PungentAuthoringValidationResult Missing(PungentAuthoringReference reference)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = Id,
                itemId = reference != null ? reference.itemId ?? string.Empty : string.Empty
            };
            result.AddIssue(PungentAuthoringValidationIssue.Create(
                PungentAuthoringValidationSeverity.Warning,
                "Checklist could not be found.",
                Id,
                result.itemId,
                null,
                "Import or create the checklist definition.",
                result.itemId,
                "MISSING_TARGET"));
            return result;
        }

        private static PungentAuthoringValidationResult Invalid(string checklistId, string error)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = Id,
                itemId = checklistId ?? string.Empty
            };
            result.AddIssue(PungentAuthoringValidationIssue.Create(
                PungentAuthoringValidationSeverity.Error,
                string.IsNullOrWhiteSpace(error) ? "Checklist definition is invalid." : error,
                Id,
                checklistId,
                null,
                "Fix the checklist definition and import it again.",
                checklistId,
                "INVALID_CHECKLIST"));
            return result;
        }
    }

    [InitializeOnLoad]
    public static class PungentChecklistAuthoringProviderRegistration
    {
        static PungentChecklistAuthoringProviderRegistration()
        {
            RegisterProvider();
        }

        public static void RegisterProvider()
        {
            PungentAuthoringProviderRegistry.Register(new PungentChecklistAuthoringProvider());
        }
    }
#endif
}
