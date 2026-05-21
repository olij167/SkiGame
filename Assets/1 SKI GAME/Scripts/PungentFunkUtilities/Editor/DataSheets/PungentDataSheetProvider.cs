using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using UnityEditor;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public sealed class PungentDataSheetProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider,
        IPungentAuthoringValidator
    {
        public const string Id = "data-sheets";

        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.DataSheet };

        public string ProviderId => Id;
        public string DisplayName => "Data Sheets";
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
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.DataSheet;
        public string ExtensionId => PungentAuthoringPackageCapabilities.DataSheet;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentDataSheet sheet in PungentDataSheetEditorStorage.Database.sheets ?? new List<PungentDataSheet>())
                if (sheet != null)
                    yield return ToMetadata(sheet);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentDataSheet sheet = FindSheet(reference);
            if (sheet == null)
                return false;

            metadata = ToMetadata(sheet);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentDataSheet sheet = FindSheet(reference);
            if (sheet == null)
                yield break;

            foreach (PungentAuthoringReference linked in sheet.references ?? new List<PungentAuthoringReference>())
                if (linked != null && linked.HasItemId)
                    yield return linked;

            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
                if (row != null && row.linkedAuthoringRef != null && row.linkedAuthoringRef.HasItemId)
                    yield return row.linkedAuthoringRef;

            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
                if (column != null && column.reference != null && column.reference.HasItemId)
                    yield return column.reference;

            foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
                if (cell != null && cell.linkedAuthoringRef != null && cell.linkedAuthoringRef.HasItemId)
                    yield return cell.linkedAuthoringRef;

            // RDE/STICKY-NOTES MIGRATION NOTE: Keep LegacyNote references for compatibility; UI handoffs should call them sheet sticky notes, not rebuild the old Notes browser dependency.
            foreach (PungentNote note in FindSheetNotes(sheet.id))
                yield return PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, note.id, PungentAuthoringLegacyNoteProvider.Id, note.title);
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentDataSheet sheet = FindSheet(reference);
            if (sheet == null)
                yield break;

            yield return PungentAuthoringTarget.Create(PungentAuthoringTargetKind.DataSheetId, sheet.id, sheet.title, Id);
            foreach (PungentAuthoringTarget target in sheet.targets ?? new List<PungentAuthoringTarget>())
                if (target != null && target.HasTarget)
                    yield return target;

            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
                if (column != null && column.target != null && column.target.HasTarget)
                    yield return column.target;

            foreach (PungentDataSheetBindingProfile profile in sheet.bindingProfiles ?? new List<PungentDataSheetBindingProfile>())
            {
                if (profile == null)
                    continue;

                foreach (PungentDataSheetTargetBinding targetBinding in profile.targetBindings ?? new List<PungentDataSheetTargetBinding>())
                {
                    if (targetBinding == null || string.IsNullOrWhiteSpace(targetBinding.targetGlobalId))
                        continue;

                    PungentAuthoringTarget bindingTarget = PungentAuthoringTarget.Create(
                        PungentAuthoringTargetKind.SceneObjectGlobalId,
                        targetBinding.targetGlobalId,
                        string.IsNullOrWhiteSpace(targetBinding.objectName) ? "Bound Data Sheet Target" : targetBinding.objectName,
                        Id);
                    bindingTarget.sourceContext = "Data Sheet Binding Profile: " + profile.displayName;
                    bindingTarget.contextId = targetBinding.assetGuid;
                    yield return bindingTarget;
                }
            }
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentDataSheet sheet = FindSheet(reference);
            if (sheet == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(sheet), GetTargets(reference).Count());
            preview.bodyPreview = PreviewText(sheet);
            preview.primaryActionLabels.Add("Open Data Sheet");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = string.Empty;
            if (FindSheet(reference) == null)
            {
                reason = "Data sheet could not be found.";
                return false;
            }

            return true;
        }

        public bool Open(PungentAuthoringReference reference)
        {
            return OpenSheet(reference);
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            return CanOpen(reference, out reason);
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            return OpenSheet(reference);
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Data sheet creation from arbitrary authoring context is deferred to templates and CSV import.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Data sheet creation from arbitrary authoring context is deferred to templates and CSV import.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = reference?.itemId ?? string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(copiedValue))
            {
                error = "No Data Sheet ID to copy.";
                return false;
            }

            EditorGUIUtility.systemCopyBuffer = copiedValue;
            return true;
        }

        public PungentAuthoringValidationResult ValidateReference(PungentAuthoringReference reference)
        {
            PungentDataSheet sheet = FindSheet(reference);
            return sheet == null
                ? Missing(reference)
                : PungentDataSheetValidation.ValidateSheet(sheet);
        }

        public PungentAuthoringValidationResult ValidateTarget(PungentAuthoringTarget target)
        {
            if (target == null || target.targetKind != PungentAuthoringTargetKind.DataSheetId)
                return PungentAuthoringValidationResult.CreateValid(Id, target != null ? target.rawValue : string.Empty);

            PungentAuthoringReference reference = PungentAuthoringReference.Create(PungentAuthoringItemKind.DataSheet, target.rawValue, Id, target.label);
            return ValidateReference(reference);
        }

        public static PungentAuthoringMetadata ToMetadata(PungentDataSheet sheet)
        {
            return sheet == null
                ? null
                : sheet.ToMetadata(Id, PungentAuthoringPackageCapabilities.DataSheet);
        }

        private static PungentDataSheet FindSheet(PungentAuthoringReference reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.itemId))
                return null;

            return PungentDataSheetEditorStorage.Database.FindSheet(reference.itemId);
        }

        private static bool OpenSheet(PungentAuthoringReference reference)
        {
            PungentDataSheet sheet = FindSheet(reference);
            if (sheet == null)
                return false;

            PungentDataSheetEditorWindow.OpenAndSelect(sheet.id);
            return true;
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
                "Data sheet could not be found.",
                Id,
                result.itemId,
                null,
                "Open Data Sheet Editor",
                result.itemId,
                "MISSING_TARGET"));
            return result;
        }

        private static string PreviewText(PungentDataSheet sheet)
        {
            if (sheet == null)
                return string.Empty;

            string summary = sheet.summary ?? string.Empty;
            string shape = (sheet.rows != null ? sheet.rows.Count : 0) + " row(s), " + (sheet.columns != null ? sheet.columns.Count : 0) + " column(s)";
            return string.IsNullOrWhiteSpace(summary) ? shape : summary.Trim() + " | " + shape;
        }

        private static IEnumerable<PungentNote> FindSheetNotes(string sheetId)
        {
            if (string.IsNullOrWhiteSpace(sheetId))
                yield break;

            string stableKey = "data-sheet:" + sheetId;
            foreach (PungentNote note in PungentNoteDatabase.instance.notes ?? new List<PungentNote>())
            {
                if (note == null || note.archived)
                    continue;

                if (string.Equals(note.stableKey, stableKey, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(note.body) && note.body.IndexOf("Data Sheet ID: " + sheetId, StringComparison.OrdinalIgnoreCase) >= 0))
                    yield return note;
            }
        }
    }

    [InitializeOnLoad]
    public static class PungentDataSheetProviderRegistration
    {
        static PungentDataSheetProviderRegistration()
        {
            RegisterProvider();
        }

        public static void RegisterProvider()
        {
            // PungentAuthoringProviderRegistry replaces an existing provider with the same ID, so repeated domain-load calls are safe.
            PungentAuthoringProviderRegistry.Register(new PungentDataSheetProvider());
            PungentAuthoringProviderRegistry.Register(new PungentDataSheetUnityObjectReferenceProvider());
        }
    }
#endif
}
