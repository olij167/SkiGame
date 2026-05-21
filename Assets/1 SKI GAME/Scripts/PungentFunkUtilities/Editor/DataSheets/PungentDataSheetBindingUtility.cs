using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public static class PungentDataSheetBindingUtility
    {
        // SHARED BINDING NOTE: Data Sheets currently consume Editor/Authoring/PungentAuthoringBindingServices.cs
        // for endpoint picking, preview, and apply behavior. The profile schema remains sheet-owned so existing
        // PungentDataSheetBindingProfile data stays compatible and non-destructive.
        private sealed class BindingTargetInfo
        {
            public Object unityObject;
            public string globalId = string.Empty;
            public string assetGuid = string.Empty;
            public string assetPath = string.Empty;
            public string displayName = string.Empty;
            public string typeName = string.Empty;
        }

        public static PungentAuthoringBindingDraft BuildBindingDraftFromTargets(IEnumerable<Object> targets, string displayName = null)
        {
            return PungentAuthoringBindingDraftBuilder.BuildForTargets(
                targets ?? new Object[0],
                string.IsNullOrWhiteSpace(displayName) ? "Data Sheet Binding Draft" : displayName.Trim(),
                "DataSheetBindingDraft");
        }

        public static PungentAuthoringBindingDraft BuildBindingDraftFromSelection(string displayName = null)
        {
            return BuildBindingDraftFromTargets(Selection.objects, displayName);
        }

        public static PungentAuthoringBindingDraft BuildBindingDraftFromSelection(PungentDataSheetBindingProfile profile, out string message, string displayName = null)
        {
            List<BindingTargetInfo> targets = CollectTargetsFromObjects(Selection.objects, profile);
            PungentAuthoringBindingDraft draft = BuildBindingDraftFromTargets(targets.Select(target => target.unityObject), displayName);
            message = "Generated draft from " + targets.Count.ToString(CultureInfo.InvariantCulture) + " selected target(s).";
            return draft;
        }

        public static PungentAuthoringBindingDraft BuildBindingDraftFromCachedTargets(PungentDataSheetBindingProfile profile, out string message, string displayName = null)
        {
            message = string.Empty;
            if (profile == null)
            {
                message = "Binding profile is missing.";
                return BuildBindingDraftFromTargets(Array.Empty<Object>(), displayName);
            }

            List<Object> targets = new List<Object>();
            foreach (string globalId in profile.manualTargetGlobalIds ?? new List<string>())
            {
                Object target = ResolveGlobalObject(globalId);
                if (target != null)
                    targets.Add(target);
            }

            foreach (PungentDataSheetTargetBinding targetBinding in profile.targetBindings ?? new List<PungentDataSheetTargetBinding>())
            {
                if (targetBinding == null || string.IsNullOrWhiteSpace(targetBinding.targetGlobalId))
                    continue;
                if ((profile.manualTargetGlobalIds ?? new List<string>()).Exists(id => string.Equals(id, targetBinding.targetGlobalId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Object target = ResolveGlobalObject(targetBinding.targetGlobalId);
                if (target != null)
                    targets.Add(target);
            }

            PungentAuthoringBindingDraft draft = BuildBindingDraftFromTargets(targets, displayName);
            message = "Generated draft from " + targets.Count.ToString(CultureInfo.InvariantCulture) + " cached target(s).";
            return draft;
        }

        public static PungentAuthoringBindingDraft BuildBindingDraftFromAssetFolder(PungentDataSheetBindingProfile profile, out string message, string displayName = null)
        {
            List<Object> candidates = CollectAssetFolderObjects(profile, out message);
            if (candidates.Count == 0)
                return BuildBindingDraftFromTargets(Array.Empty<Object>(), displayName);

            List<BindingTargetInfo> targets = CollectTargetsFromObjects(candidates.ToArray(), profile);
            PungentAuthoringBindingDraft draft = BuildBindingDraftFromTargets(targets.Select(target => target.unityObject), displayName);
            message = "Generated draft from " + targets.Count.ToString(CultureInfo.InvariantCulture) + " folder target(s). Folder discovery only ran from this explicit action.";
            return draft;
        }

        public static void ApplyExistingBindingGatesToDraft(PungentDataSheetBindingProfile profile, PungentAuthoringBindingDraft draft)
        {
            if (profile == null || draft == null)
                return;

            Dictionary<string, PungentDataSheetColumnBinding> columnBindingsByPath = new Dictionary<string, PungentDataSheetColumnBinding>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentDataSheetColumnBinding binding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
            {
                if (binding == null)
                    continue;

                string key = BindingPathKey(binding.bindingPath, binding.propertyPath, binding.valueType, binding.adapterId, binding.endpointLabel);
                if (!string.IsNullOrWhiteSpace(key) && !columnBindingsByPath.ContainsKey(key))
                    columnBindingsByPath.Add(key, binding);
            }

            foreach (PungentAuthoringBindingDraftColumn column in draft.columns ?? new List<PungentAuthoringBindingDraftColumn>())
            {
                if (column == null)
                    continue;

                string key = BindingPathKey(column.path);
                if (!string.IsNullOrWhiteSpace(key) && columnBindingsByPath.TryGetValue(key, out PungentDataSheetColumnBinding binding))
                {
                    column.pullEnabled = binding.pullEnabled;
                    column.pushEnabled = binding.pushEnabled;
                }
            }

            foreach (PungentAuthoringBindingDraftRow row in draft.rows ?? new List<PungentAuthoringBindingDraftRow>())
            {
                if (row == null)
                    continue;

                BindingTargetInfo target = BindingTargetInfoFromDraftRow(row);
                PungentDataSheetTargetBinding binding = profile.FindTargetBindingByGlobalId(target.globalId);
                if (binding == null)
                    continue;

                row.pullEnabled = binding.pullEnabled;
                row.pushEnabled = binding.pushEnabled;
            }
        }

        public sealed class BindingDraftApplyOptions
        {
            public PungentDataSheetBindingTargetSource targetSource = PungentDataSheetBindingTargetSource.ManualObjects;
            public bool addTargetsToManualCache = true;
        }

        public sealed class BindingDraftApplySummary
        {
            public int rowsCreated;
            public int rowsUpdated;
            public int columnsCreated;
            public int columnsUpdated;
            public int rowBindingsUpdated;
            public int columnBindingsUpdated;

            public override string ToString()
            {
                return "Committed binding draft: " +
                       rowsCreated.ToString(CultureInfo.InvariantCulture) + " row(s) created, " +
                       rowsUpdated.ToString(CultureInfo.InvariantCulture) + " row(s) reused, " +
                       columnsCreated.ToString(CultureInfo.InvariantCulture) + " column(s) created, " +
                       columnsUpdated.ToString(CultureInfo.InvariantCulture) + " column(s) reused.";
            }
        }

        public static bool ApplyBindingDraftToSheet(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            PungentAuthoringBindingDraft draft,
            BindingDraftApplyOptions options,
            out BindingDraftApplySummary summary)
        {
            summary = new BindingDraftApplySummary();
            if (sheet == null || profile == null || draft == null)
                return false;

            sheet.NormalizeInPlace();
            profile.NormalizeInPlace();
            draft.NormalizeInPlace();
            options = options ?? new BindingDraftApplyOptions();

            Dictionary<string, PungentDataSheetColumn> columnsByPath = BuildColumnsByBindingPath(sheet, profile);
            foreach (PungentAuthoringBindingDraftColumn draftColumn in draft.columns ?? new List<PungentAuthoringBindingDraftColumn>())
            {
                if (draftColumn == null || !draftColumn.include || draftColumn.path == null || string.IsNullOrWhiteSpace(draftColumn.path.propertyPath))
                    continue;

                string pathKey = BindingPathKey(draftColumn.path);
                if (!columnsByPath.TryGetValue(pathKey, out PungentDataSheetColumn column) || column == null)
                {
                    column = sheet.AddColumn(string.IsNullOrWhiteSpace(draftColumn.displayName) ? "Bound Field" : draftColumn.displayName, DataTypeFor(draftColumn.path));
                    columnsByPath[pathKey] = column;
                    summary.columnsCreated++;
                }
                else
                {
                    column.dataType = DataTypeFor(draftColumn.path);
                    column.NormalizeInPlace();
                    summary.columnsUpdated++;
                }

                PungentDataSheetColumnBinding columnBinding = profile.GetOrCreateColumnBinding(column.id, column.displayName);
                ApplyDraftColumnToBinding(sheet, profile, column, columnBinding, draftColumn);
                summary.columnBindingsUpdated++;
            }

            foreach (PungentAuthoringBindingDraftRow draftRow in draft.rows ?? new List<PungentAuthoringBindingDraftRow>())
            {
                if (draftRow == null || !draftRow.include)
                    continue;

                BindingTargetInfo target = BindingTargetInfoFromDraftRow(draftRow);
                if (string.IsNullOrWhiteSpace(target.globalId))
                    continue;

                PungentDataSheetTargetBinding targetBinding = profile.FindTargetBindingByGlobalId(target.globalId);
                PungentDataSheetRow row = targetBinding == null ? null : sheet.FindRow(targetBinding.rowId);
                bool created = false;
                if (row == null)
                {
                    int rowCountBefore = sheet.rows == null ? 0 : sheet.rows.Count;
                    row = GetOrCreateTargetRow(sheet, profile, target);
                    created = (sheet.rows == null ? 0 : sheet.rows.Count) > rowCountBefore;
                }

                if (targetBinding == null)
                {
                    targetBinding = new PungentDataSheetTargetBinding();
                    profile.targetBindings.Add(targetBinding);
                }

                ApplyDraftRowToBinding(sheet, profile, row, targetBinding, draftRow, target);
                if (options.addTargetsToManualCache)
                    AddTargetIdToManualCache(profile, target.globalId);
                if (!string.IsNullOrWhiteSpace(profile.rowIdentityColumnId))
                    SetDraftMetadataCell(sheet.GetOrCreateCell(row.id, profile.rowIdentityColumnId), target.globalId);
                if (!string.IsNullOrWhiteSpace(profile.objectNameColumnId))
                    SetDraftMetadataCell(sheet.GetOrCreateCell(row.id, profile.objectNameColumnId), target.displayName);

                if (created)
                    summary.rowsCreated++;
                else
                    summary.rowsUpdated++;
                summary.rowBindingsUpdated++;
            }

            profile.targetSource = options.targetSource;
            profile.NormalizeInPlace();
            sheet.Touch();
            return summary.rowBindingsUpdated > 0 || summary.columnBindingsUpdated > 0;
        }

        public static PungentDataSheetDataType DataTypeFor(PungentAuthoringBindingValueType valueType)
        {
            switch (valueType)
            {
                case PungentAuthoringBindingValueType.Number:
                    return PungentDataSheetDataType.Number;
                case PungentAuthoringBindingValueType.Boolean:
                    return PungentDataSheetDataType.Boolean;
                case PungentAuthoringBindingValueType.Enum:
                    return PungentDataSheetDataType.EnumText;
                case PungentAuthoringBindingValueType.ObjectReference:
                case PungentAuthoringBindingValueType.UnityObject:
                case PungentAuthoringBindingValueType.AssetReference:
                case PungentAuthoringBindingValueType.SceneObjectReference:
                case PungentAuthoringBindingValueType.ComponentReference:
                case PungentAuthoringBindingValueType.Sprite:
                case PungentAuthoringBindingValueType.AudioClip:
                    return PungentDataSheetDataType.AuthoringReference;
                default:
                    return PungentDataSheetDataType.Text;
            }
        }

        public static PungentDataSheetDataType DataTypeFor(PungentAuthoringBindingPath path)
        {
            return DataTypeFor(path == null ? PungentAuthoringBindingValueType.Unknown : path.valueType);
        }

        public static int AddSelectionToManualTargets(PungentDataSheetBindingProfile profile, out string message)
        {
            message = string.Empty;
            if (profile == null)
            {
                message = "Binding profile is missing.";
                return 0;
            }

            List<BindingTargetInfo> targets = CollectTargetsFromObjects(Selection.objects, profile);
            int added = AddTargetsToManualList(profile, targets);
            message = added == 0
                ? "No new supported selected objects were added."
                : "Added " + added.ToString(CultureInfo.InvariantCulture) + " selected target(s).";
            return added;
        }

        public static int RefreshFolderTargets(PungentDataSheetBindingProfile profile, out string message)
        {
            message = string.Empty;
            if (profile == null)
            {
                message = "Binding profile is missing.";
                return 0;
            }

            if (string.IsNullOrWhiteSpace(profile.assetFolderPath) || !profile.assetFolderPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                message = "Choose an Assets folder before refreshing folder targets.";
                return 0;
            }

            if (profile.manualTargetGlobalIds == null)
                profile.manualTargetGlobalIds = new List<string>();

            List<Object> candidates = CollectAssetFolderObjects(profile, out string folderMessage);
            if (candidates.Count == 0)
            {
                message = string.IsNullOrWhiteSpace(folderMessage) ? "No supported folder targets were found." : folderMessage;
                return 0;
            }

            List<BindingTargetInfo> targets = CollectTargetsFromObjects(candidates.ToArray(), profile);
            profile.manualTargetGlobalIds.Clear();
            int added = AddTargetsToManualList(profile, targets);
            profile.targetSource = PungentDataSheetBindingTargetSource.AssetFolder;
            profile.NormalizeInPlace();
            message = "Cached " + added.ToString(CultureInfo.InvariantCulture) + " folder target(s). Folder refresh only runs from this explicit action.";
            return added;
        }

        public static int PullFromTargets(PungentDataSheet sheet, PungentDataSheetBindingProfile profile, out string message)
        {
            message = string.Empty;
            if (sheet == null || profile == null)
            {
                message = "Sheet or binding profile is missing.";
                return 0;
            }

            sheet.NormalizeInPlace();
            profile.NormalizeInPlace();
            if (profile.direction == PungentDataSheetBindingDirection.PushOnly)
            {
                message = "This binding profile is Push Only, so pull is disabled.";
                return 0;
            }

            List<BindingTargetInfo> targets = CollectTargets(profile);
            if (targets.Count == 0)
            {
                message = "No supported targets were found for this binding source.";
                return 0;
            }

            string now = DateTime.UtcNow.ToString("o");
            int cellsPulled = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                BindingTargetInfo target = targets[i];
                PungentDataSheetRow row = GetOrCreateTargetRow(sheet, profile, target);
                PungentDataSheetTargetBinding targetBinding = profile.FindTargetBindingByGlobalId(target.globalId);
                if (targetBinding == null)
                {
                    targetBinding = new PungentDataSheetTargetBinding();
                    profile.targetBindings.Add(targetBinding);
                }

                targetBinding.rowId = row.id;
                targetBinding.targetGlobalId = target.globalId;
                targetBinding.assetGuid = target.assetGuid;
                targetBinding.assetPath = target.assetPath;
                targetBinding.objectName = target.displayName;
                targetBinding.targetTypeName = target.typeName;
                targetBinding.lastPulledUtc = now;
                targetBinding.NormalizeInPlace();

                if (!targetBinding.pullEnabled)
                    continue;

                if (!string.IsNullOrWhiteSpace(profile.rowIdentityColumnId))
                    SetPulledCell(sheet.GetOrCreateCell(row.id, profile.rowIdentityColumnId), target.globalId, now, string.Empty);
                if (!string.IsNullOrWhiteSpace(profile.objectNameColumnId))
                    SetPulledCell(sheet.GetOrCreateCell(row.id, profile.objectNameColumnId), target.displayName, now, string.Empty);

                foreach (PungentDataSheetColumnBinding columnBinding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
                {
                    if (columnBinding == null || !columnBinding.pullEnabled || string.IsNullOrWhiteSpace(columnBinding.propertyPath))
                        continue;

                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, columnBinding.columnId);
                    PungentAuthoringBindingLink link = CreateColumnBindingLink(sheet, profile, row.id, target.globalId, target.displayName, columnBinding);
                    PungentAuthoringBindingLinkPreview bridgePreview = PungentAuthoringBindingBridgeService.PreviewLink(link);
                    PungentAuthoringBindingPreview preview = bridgePreview.endpointPreview;
                    if (preview == null || !bridgePreview.canRead)
                    {
                        MarkCellFailed(cell, string.IsNullOrWhiteSpace(bridgePreview.disabledReason) ? "Endpoint could not be read." : bridgePreview.disabledReason);
                        continue;
                    }

                    columnBinding.expectedPropertyType = preview.valueKind.ToString();
                    if (columnBinding.valueType == PungentAuthoringBindingValueType.Unknown)
                        columnBinding.valueType = PungentAuthoringBindingBridgeService.ToRuntimeValueType(preview.valueKind);
                    SetPulledCell(cell, preview.currentValue, now, string.IsNullOrWhiteSpace(preview.warning) ? string.Empty : preview.warning);
                    cellsPulled++;
                }
            }

            sheet.Touch();
            profile.NormalizeInPlace();
            message = "Pulled " + cellsPulled.ToString(CultureInfo.InvariantCulture) + " bound cell value(s) from " + targets.Count.ToString(CultureInfo.InvariantCulture) + " target(s).";
            return cellsPulled;
        }

        public static List<PungentDataSheetApplyPreview> BuildApplyPreview(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            PungentDataSheetApplyScope scope,
            PungentDataSheetConflictPolicy conflictPolicy,
            IEnumerable<string> selectedRowIds,
            IEnumerable<PungentDataSheetCellAddress> selectedCells)
        {
            List<PungentDataSheetApplyPreview> previews = new List<PungentDataSheetApplyPreview>();
            if (sheet == null || profile == null)
                return previews;
            if (profile.direction == PungentDataSheetBindingDirection.PullOnly)
                return previews;

            HashSet<string> selectedRows = NormalizeSet(selectedRowIds);
            HashSet<string> selectedCellKeys = NormalizeCellSet(selectedCells);
            HashSet<string> emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
            {
                if (row == null)
                    continue;

                PungentDataSheetTargetBinding targetBinding = profile.FindTargetBindingByRow(row.id);
                foreach (PungentDataSheetColumnBinding columnBinding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
                {
                    if (columnBinding == null || string.IsNullOrWhiteSpace(columnBinding.propertyPath))
                        continue;
                    if (targetBinding != null && !targetBinding.pushEnabled)
                    {
                        if (ShouldIncludeCell(sheet, row.id, columnBinding.columnId, scope, selectedRows, selectedCellKeys))
                            previews.Add(CreateSkippedPreview(row.id, columnBinding.columnId, targetBinding.objectName, columnBinding.propertyPath, "Apply is disabled for this row."));
                        continue;
                    }
                    if (!columnBinding.pushEnabled)
                        continue;

                    if (!ShouldIncludeCell(sheet, row.id, columnBinding.columnId, scope, selectedRows, selectedCellKeys))
                        continue;

                    string emittedKey = CellKey(row.id, columnBinding.columnId);
                    if (!emitted.Add(emittedKey))
                        continue;

                    PungentDataSheetApplyPreview preview = BuildCellPreview(sheet, profile, row, columnBinding, targetBinding, conflictPolicy);
                    previews.Add(preview);
                }
            }

            if (previews.Count == 0 && scope == PungentDataSheetApplyScope.SelectedCells)
            {
                foreach (PungentDataSheetCellAddress address in selectedCells ?? new List<PungentDataSheetCellAddress>())
                {
                    PungentDataSheetRow row = sheet.FindRow(address.rowId);
                    PungentDataSheetColumnBinding columnBinding = profile.FindColumnBinding(address.columnId);
                    if (row == null)
                    {
                        previews.Add(CreateSkippedPreview(address.rowId, address.columnId, string.Empty, string.Empty, "Selected row is virtual or missing. Edit/pull it before applying."));
                        continue;
                    }

                    if (columnBinding == null)
                    {
                        previews.Add(CreateSkippedPreview(row.id, address.columnId, string.Empty, string.Empty, "Selected column is not mapped in the active binding profile."));
                        continue;
                    }

                    if (!columnBinding.pushEnabled)
                    {
                        previews.Add(CreateSkippedPreview(row.id, columnBinding.columnId, string.Empty, columnBinding.propertyPath, "Apply is disabled for this column."));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(columnBinding.propertyPath))
                    {
                        previews.Add(CreateSkippedPreview(row.id, columnBinding.columnId, string.Empty, string.Empty, "Column binding has no SerializedProperty path."));
                        continue;
                    }

                    PungentDataSheetTargetBinding targetBinding = profile.FindTargetBindingByRow(row.id);
                    if (targetBinding != null && !targetBinding.pushEnabled)
                    {
                        previews.Add(CreateSkippedPreview(row.id, columnBinding.columnId, targetBinding.objectName, columnBinding.propertyPath, "Apply is disabled for this row."));
                        continue;
                    }

                    previews.Add(BuildCellPreview(sheet, profile, row, columnBinding, targetBinding, conflictPolicy));
                }
            }

            return previews;
        }

        public static int ApplyPreview(PungentDataSheet sheet, PungentDataSheetBindingProfile profile, List<PungentDataSheetApplyPreview> previews, out string message)
        {
            message = string.Empty;
            if (sheet == null || profile == null || previews == null || previews.Count == 0)
            {
                message = "No apply preview entries are ready.";
                return 0;
            }

            int applied = 0;
            int skipped = 0;
            int failed = 0;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Data Sheet Values");

            for (int i = 0; i < previews.Count; i++)
            {
                PungentDataSheetApplyPreview preview = previews[i];
                if (preview == null || !preview.selected || !preview.CanApply)
                {
                    skipped++;
                    continue;
                }

                PungentDataSheetColumnBinding columnBinding = profile.FindColumnBinding(preview.columnId);
                PungentAuthoringBindingLink link = CreateColumnBindingLink(sheet, profile, preview.rowId, preview.targetGlobalId, preview.targetName, columnBinding);
                PungentAuthoringBindingApplyResult result = PungentAuthoringBindingBridgeService.ApplyLink(link, preview.newValue, "Apply Data Sheet Value");
                if (result == null || !result.applied)
                {
                    MarkPreviewFailed(sheet, preview, result == null || string.IsNullOrWhiteSpace(result.message)
                        ? "Endpoint could not be applied."
                        : result.message);
                    failed++;
                    continue;
                }

                preview.status = PungentDataSheetApplyPreviewStatus.Applied;
                preview.reason = string.IsNullOrWhiteSpace(result.message) ? "Applied." : result.message;
                PungentDataSheetCell cell = sheet.FindCell(preview.rowId, preview.columnId);
                if (cell != null)
                {
                    cell.lastAppliedUtc = DateTime.UtcNow.ToString("o");
                    cell.propagationStatus = PungentDataSheetPropagationStatus.Applied;
                    cell.propagationMessage = "Applied to " + preview.targetName + ".";
                }

                applied++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            sheet.Touch();
            message = "Applied " + applied.ToString(CultureInfo.InvariantCulture) + ", skipped " + skipped.ToString(CultureInfo.InvariantCulture) + ", failed " + failed.ToString(CultureInfo.InvariantCulture) + ".";
            return applied;
        }

        public static PungentDataSheetApplyPreview BuildSingleCellApplyPreview(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            string rowId,
            string columnId,
            PungentDataSheetConflictPolicy conflictPolicy)
        {
            if (sheet == null || profile == null)
                return CreateSkippedPreview(rowId, columnId, string.Empty, string.Empty, "Sheet or binding profile is missing.");
            if (profile.direction == PungentDataSheetBindingDirection.PullOnly)
                return CreateSkippedPreview(rowId, columnId, string.Empty, string.Empty, "Flow is Pull Only.");

            PungentDataSheetRow row = sheet.FindRow(rowId);
            if (row == null)
                return CreateSkippedPreview(rowId, columnId, string.Empty, string.Empty, "Row is missing.");

            PungentDataSheetColumnBinding columnBinding = profile.FindColumnBinding(columnId);
            if (columnBinding == null || string.IsNullOrWhiteSpace(columnBinding.propertyPath))
                return CreateSkippedPreview(row.id, columnId, string.Empty, string.Empty, "Column is not mapped to a field.");
            if (!columnBinding.pushEnabled)
                return CreateSkippedPreview(row.id, columnBinding.columnId, string.Empty, columnBinding.propertyPath, "Apply is disabled for this column.");

            return BuildCellPreview(sheet, profile, row, columnBinding, profile.FindTargetBindingByRow(row.id), conflictPolicy);
        }

        public static bool TryApplySingleCell(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            string rowId,
            string columnId,
            PungentDataSheetConflictPolicy conflictPolicy,
            out PungentDataSheetApplyPreview preview,
            out string message)
        {
            preview = BuildSingleCellApplyPreview(sheet, profile, rowId, columnId, conflictPolicy);
            if (preview == null || !preview.CanApply)
            {
                message = preview == null || string.IsNullOrWhiteSpace(preview.reason)
                    ? "Cell is not ready to apply."
                    : preview.reason;
                return false;
            }

            List<PungentDataSheetApplyPreview> previews = new List<PungentDataSheetApplyPreview> { preview };
            int applied = ApplyPreview(sheet, profile, previews, out message);
            return applied > 0;
        }

        public static PungentAuthoringBindingPlan BuildRuntimePlan(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            PungentAuthoringBindingPlan plan = new PungentAuthoringBindingPlan
            {
                displayName = sheet == null ? "Data Sheet Binding Plan" : "Data Sheet Binding Plan: " + sheet.title,
                sourceInterfaceId = "data-sheets",
                sourceItemId = sheet == null ? string.Empty : sheet.id,
                sourceLabel = sheet == null ? string.Empty : sheet.title
            };

            if (sheet == null || profile == null)
            {
                plan.NormalizeInPlace();
                return plan;
            }

            foreach (PungentDataSheetTargetBinding targetBinding in profile.targetBindings ?? new List<PungentDataSheetTargetBinding>())
            {
                if (targetBinding == null || string.IsNullOrWhiteSpace(targetBinding.rowId) || string.IsNullOrWhiteSpace(targetBinding.targetGlobalId))
                    continue;
                if (!targetBinding.pullEnabled && !targetBinding.pushEnabled)
                    continue;

                foreach (PungentDataSheetColumnBinding columnBinding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
                {
                    if (columnBinding == null || string.IsNullOrWhiteSpace(columnBinding.propertyPath))
                        continue;
                    if (!columnBinding.pullEnabled && !columnBinding.pushEnabled)
                        continue;

                    PungentAuthoringBindingLink link = CreateColumnBindingLink(sheet, profile, targetBinding.rowId, targetBinding.targetGlobalId, targetBinding.objectName, columnBinding);
                    bool rowCanRead = targetBinding.pullEnabled && link.CanRead;
                    bool rowCanApply = targetBinding.pushEnabled && link.CanApply;
                    if (rowCanRead && rowCanApply)
                        link.direction = PungentAuthoringBindingLinkDirection.TwoWay;
                    else if (rowCanRead)
                        link.direction = PungentAuthoringBindingLinkDirection.ReadOnly;
                    else if (rowCanApply)
                        link.direction = PungentAuthoringBindingLinkDirection.WriteOnly;
                    else
                        continue;
                    link.bindingSlot.pullEnabled = rowCanRead;
                    link.bindingSlot.pushEnabled = rowCanApply;
                    link.runtimeEnabled = false;
                    link.notes = "Exported from Data Sheets. Runtime execution requires a project adapter with a matching runtimeAdapterId.";
                    link.NormalizeInPlace();
                    plan.links.Add(link);
                }
            }

            plan.NormalizeInPlace();
            return plan;
        }

        public static string DisplayTarget(Object target)
        {
            if (target == null)
                return "Missing";

            string path = AssetDatabase.GetAssetPath(target);
            return string.IsNullOrWhiteSpace(path)
                ? target.name + " (" + target.GetType().Name + ")"
                : target.name + " (" + target.GetType().Name + ", " + path + ")";
        }

        public static string BuildReadinessSummary(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            if (sheet == null || profile == null)
                return "Binding readiness: select a sheet and binding profile.";

            int mapped = 0;
            int pullEnabled = 0;
            int applyEnabled = 0;
            foreach (PungentDataSheetColumnBinding binding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.propertyPath))
                    continue;
                mapped++;
                if (binding.pullEnabled)
                    pullEnabled++;
                if (binding.pushEnabled)
                    applyEnabled++;
            }

            int manualTargets = profile.manualTargetGlobalIds == null ? 0 : profile.manualTargetGlobalIds.Count;
            int rowTargets = profile.targetBindings == null ? 0 : profile.targetBindings.Count;
            int selectedTargets = Selection.objects == null ? 0 : Selection.objects.Length;
            HashSet<string> applyColumnIds = new HashSet<string>(
                (profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
                    .Where(binding => binding != null && binding.pushEnabled && !string.IsNullOrWhiteSpace(binding.propertyPath))
                    .Select(binding => PungentAuthoringId.Normalize(binding.columnId)),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> targetRowIds = new HashSet<string>(
                (profile.targetBindings ?? new List<PungentDataSheetTargetBinding>())
                    .Where(binding => binding != null && binding.pushEnabled && !string.IsNullOrWhiteSpace(binding.rowId) && !string.IsNullOrWhiteSpace(binding.targetGlobalId))
                    .Select(binding => PungentAuthoringId.Normalize(binding.rowId)),
                StringComparer.OrdinalIgnoreCase);
            int changedMappedCells = 0;
            int applyReadyCandidates = 0;
            foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
            {
                if (cell == null || cell.propagationStatus != PungentDataSheetPropagationStatus.Changed)
                    continue;
                if (!applyColumnIds.Contains(PungentAuthoringId.Normalize(cell.columnId)))
                    continue;

                changedMappedCells++;
                if (targetRowIds.Contains(PungentAuthoringId.Normalize(cell.rowId)))
                    applyReadyCandidates++;
            }

            string targetSource = profile.targetSource == PungentDataSheetBindingTargetSource.CurrentSelection
                ? selectedTargets + " currently selected object(s)" + (selectedTargets == 0 && manualTargets > 0 ? ", fallback " + manualTargets + " cached target id(s)" : string.Empty)
                : manualTargets + " cached target id(s)";

            List<string> hints = new List<string>();
            if (mapped == 0)
                hints.Add("map at least one column with Choose Field");
            if (profile.targetSource == PungentDataSheetBindingTargetSource.CurrentSelection && selectedTargets == 0 && manualTargets == 0)
                hints.Add("select target objects before pulling");
            if (profile.targetSource != PungentDataSheetBindingTargetSource.CurrentSelection && manualTargets == 0)
                hints.Add("cache targets with Cache Selection or Refresh Folder Targets");
            if (rowTargets == 0)
                hints.Add("pull once to create target-linked rows");
            if (applyEnabled == 0 && profile.direction != PungentDataSheetBindingDirection.PullOnly)
                hints.Add("enable Apply on at least one mapped column");

            string summary = "Binding readiness: target source " + targetSource +
                             "; " + mapped + " mapped column(s), " + pullEnabled + " pull-enabled, " + applyEnabled + " apply-enabled" +
                             "; " + rowTargets + " row binding(s), " + changedMappedCells + " changed mapped cell(s), " + applyReadyCandidates + " linked changed candidate(s).";
            if (hints.Count > 0)
                summary += " Next: " + string.Join("; ", hints.ToArray()) + ".";
            return summary;
        }

        public static string DescribeApplyPreviewBlockers(PungentDataSheet sheet, PungentDataSheetBindingProfile profile, PungentDataSheetApplyScope scope)
        {
            if (sheet == null || profile == null)
                return "No apply preview entries: sheet or binding profile is missing.";
            if (profile.direction == PungentDataSheetBindingDirection.PullOnly)
                return "No apply preview entries: this profile is Pull Only.";

            int mapped = 0;
            int applyEnabled = 0;
            foreach (PungentDataSheetColumnBinding binding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
            {
                if (binding == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(binding.propertyPath))
                    mapped++;
                if (binding.pushEnabled && !string.IsNullOrWhiteSpace(binding.propertyPath))
                    applyEnabled++;
            }

            if (mapped == 0)
                return "No apply preview entries: no columns are mapped to shared binding endpoints.";
            if (applyEnabled == 0)
                return "No apply preview entries: mapped columns exist, but Apply is disabled or property paths are empty.";
            if (profile.targetBindings == null || profile.targetBindings.Count == 0)
                return "No apply preview entries: no rows are linked to binding targets. Pull Rows first.";
            if (scope == PungentDataSheetApplyScope.SelectedCells)
                return "No apply preview entries: selected cells are not in apply-enabled mapped columns or their rows are not linked to targets.";
            if (scope == PungentDataSheetApplyScope.SelectedRows)
                return "No apply preview entries: selected rows are not linked to targets.";
            if (scope == PungentDataSheetApplyScope.ChangedCells)
                return "No apply preview entries: no mapped cells are marked changed.";
            return "No apply preview entries: all candidates were filtered out before preview.";
        }

        public static Object ResolveGlobalObject(string globalId)
        {
            if (string.IsNullOrWhiteSpace(globalId))
                return null;

            return GlobalObjectId.TryParse(globalId, out GlobalObjectId parsed)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed)
                : null;
        }

        private static List<Object> CollectAssetFolderObjects(PungentDataSheetBindingProfile profile, out string message)
        {
            message = string.Empty;
            List<Object> candidates = new List<Object>();
            if (profile == null)
            {
                message = "Binding profile is missing.";
                return candidates;
            }

            if (string.IsNullOrWhiteSpace(profile.assetFolderPath) || !profile.assetFolderPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                message = "Choose an Assets folder before refreshing folder targets.";
                return candidates;
            }

            string[] folders = { profile.assetFolderPath };
            string[] guids = AssetDatabase.FindAssets(string.Empty, folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                if (!profile.includeSubFolders && !string.Equals(System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/"), profile.assetFolderPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                    candidates.Add(asset);
            }

            message = "Found " + candidates.Count.ToString(CultureInfo.InvariantCulture) + " asset candidate(s).";
            return candidates;
        }

        private static Dictionary<string, PungentDataSheetColumn> BuildColumnsByBindingPath(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            Dictionary<string, PungentDataSheetColumn> columns = new Dictionary<string, PungentDataSheetColumn>(StringComparer.OrdinalIgnoreCase);
            if (sheet == null || profile == null)
                return columns;

            foreach (PungentDataSheetColumnBinding binding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
            {
                if (binding == null)
                    continue;

                string key = BindingPathKey(binding.bindingPath, binding.propertyPath, binding.valueType, binding.adapterId, binding.endpointLabel);
                if (string.IsNullOrWhiteSpace(key) || columns.ContainsKey(key))
                    continue;

                PungentDataSheetColumn column = sheet.FindColumn(binding.columnId);
                if (column != null)
                    columns.Add(key, column);
            }

            return columns;
        }

        private static string BindingPathKey(PungentAuthoringBindingPath path)
        {
            if (path == null)
                return string.Empty;

            return BindingPathKey(path, path.propertyPath, path.valueType, path.adapterId, path.displayName);
        }

        private static string BindingPathKey(PungentAuthoringBindingPath path, string propertyPath, PungentAuthoringBindingValueType valueType, string adapterId, string fallbackGroup)
        {
            string group = string.Empty;
            foreach (PungentAuthoringBindingPathSegment segment in path?.segments ?? new List<PungentAuthoringBindingPathSegment>())
            {
                if (segment == null || segment.kind != PungentAuthoringBindingPathSegmentKind.Component)
                    continue;

                group = segment.displayName ?? string.Empty;
                int slash = group.LastIndexOf(" / ", StringComparison.Ordinal);
                if (slash >= 0)
                    group = group.Substring(slash + 3);
                break;
            }

            if (string.IsNullOrWhiteSpace(group))
                group = fallbackGroup ?? string.Empty;

            return string.Join("|", new[]
            {
                adapterId ?? string.Empty,
                group,
                propertyPath ?? string.Empty,
                valueType.ToString()
            });
        }

        private static void ApplyDraftColumnToBinding(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            PungentDataSheetColumn column,
            PungentDataSheetColumnBinding binding,
            PungentAuthoringBindingDraftColumn draftColumn)
        {
            PungentAuthoringBindingPath path = CloneBindingPath(draftColumn.path);
            PungentAuthoringBindingValueType valueType = draftColumn.valueType == PungentAuthoringBindingValueType.Unknown
                ? path.valueType
                : draftColumn.valueType;
            if (valueType == PungentAuthoringBindingValueType.Unknown)
                valueType = path.valueType;

            column.dataType = DataTypeFor(valueType);
            column.NormalizeInPlace();

            binding.displayName = string.IsNullOrWhiteSpace(draftColumn.displayName) ? column.displayName : draftColumn.displayName;
            binding.propertyPath = path.propertyPath;
            binding.expectedPropertyType = valueType.ToString();
            binding.adapterId = path.adapterId;
            binding.adapterDisplayName = path.adapterDisplayName;
            binding.endpointId = path.endpointId;
            binding.endpointLabel = string.IsNullOrWhiteSpace(path.displayName) ? binding.displayName : path.displayName;
            binding.valueType = valueType;
            binding.bindingPath = path;
            binding.pullEnabled = draftColumn.pullEnabled;
            binding.pushEnabled = draftColumn.pushEnabled;
            binding.bindingSlot = PungentAuthoringBindingDiscoveryService.CreateSlot(
                PungentAuthoringBindingSlotRole.DataSheetColumn,
                "data-sheets",
                sheet.id,
                column.id,
                column.displayName,
                binding.endpointLabel,
                path);
            binding.bindingSlot.pullEnabled = binding.pullEnabled;
            binding.bindingSlot.pushEnabled = binding.pushEnabled;
            binding.bindingLinkId = PungentAuthoringBindingPreviewApplyFacade.CreateLink(path, binding.bindingSlot, DirectionFor(profile, binding)).id;
            binding.NormalizeInPlace();
        }

        private static void ApplyDraftRowToBinding(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            PungentDataSheetRow row,
            PungentDataSheetTargetBinding binding,
            PungentAuthoringBindingDraftRow draftRow,
            BindingTargetInfo target)
        {
            row.displayName = string.IsNullOrWhiteSpace(draftRow.displayName) ? target.displayName : draftRow.displayName;
            row.NormalizeInPlace(row.order);

            PungentAuthoringBindingPath rootPath = target.unityObject == null
                ? null
                : PungentAuthoringBindingDiscoveryService.CreateRootObjectPath(target.unityObject, draftRow.target, "DataSheetBindingDraft");

            binding.rowId = row.id;
            binding.targetGlobalId = target.globalId;
            binding.assetGuid = target.assetGuid;
            binding.assetPath = target.assetPath;
            binding.objectName = string.IsNullOrWhiteSpace(target.displayName) ? row.displayName : target.displayName;
            binding.targetTypeName = target.typeName;
            binding.pullEnabled = draftRow.pullEnabled;
            binding.pushEnabled = draftRow.pushEnabled;
            binding.bindingSlot = PungentAuthoringBindingDiscoveryService.CreateSlot(
                PungentAuthoringBindingSlotRole.DataSheetRow,
                "data-sheets",
                sheet.id,
                row.id,
                target.globalId,
                binding.objectName,
                rootPath);
            binding.bindingSlot.pullEnabled = binding.pullEnabled;
            binding.bindingSlot.pushEnabled = binding.pushEnabled;
            binding.NormalizeInPlace();
        }

        private static BindingTargetInfo BindingTargetInfoFromDraftRow(PungentAuthoringBindingDraftRow draftRow)
        {
            BindingTargetInfo info = new BindingTargetInfo
            {
                displayName = string.IsNullOrWhiteSpace(draftRow?.displayName) ? "Binding Target" : draftRow.displayName
            };

            Object targetObject = ResolveAuthoringTargetObject(draftRow?.target);
            if (targetObject != null)
            {
                List<BindingTargetInfo> targets = new List<BindingTargetInfo>();
                AddTargetInfo(targetObject, targets, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (targets.Count > 0)
                    return targets[0];
            }

            PungentAuthoringTarget target = draftRow?.target;
            info.globalId = FirstNonEmpty(target?.contextId, target?.rawValue);
            info.displayName = FirstNonEmpty(target?.label, info.displayName);
            info.typeName = target?.customKind ?? string.Empty;
            return info;
        }

        private static Object ResolveAuthoringTargetObject(PungentAuthoringTarget target)
        {
            if (target == null)
                return null;

            if (!string.IsNullOrWhiteSpace(target.contextId))
            {
                Object resolved = ResolveGlobalObject(target.contextId);
                if (resolved != null)
                    return resolved;
            }

            if (target.targetKind == PungentAuthoringTargetKind.AssetGuid)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(target.rawValue ?? string.Empty);
                return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
            }

            if (target.targetKind == PungentAuthoringTargetKind.SceneObjectGlobalId ||
                target.targetKind == PungentAuthoringTargetKind.ComponentInstanceId)
            {
                return ResolveGlobalObject(target.rawValue);
            }

            return ResolveGlobalObject(target.rawValue);
        }

        private static void AddTargetIdToManualCache(PungentDataSheetBindingProfile profile, string globalId)
        {
            if (profile == null || string.IsNullOrWhiteSpace(globalId))
                return;

            if (profile.manualTargetGlobalIds == null)
                profile.manualTargetGlobalIds = new List<string>();
            if (!profile.manualTargetGlobalIds.Exists(id => string.Equals(id, globalId, StringComparison.OrdinalIgnoreCase)))
                profile.manualTargetGlobalIds.Add(globalId);
        }

        private static void SetDraftMetadataCell(PungentDataSheetCell cell, string value)
        {
            if (cell == null)
                return;

            cell.rawValue = value ?? string.Empty;
            cell.displayValue = cell.rawValue;
            cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
        }

        private static PungentAuthoringBindingPath CloneBindingPath(PungentAuthoringBindingPath source)
        {
            PungentAuthoringBindingPath clone = new PungentAuthoringBindingPath();
            if (source == null)
                return clone;

            clone.id = source.id;
            clone.displayName = source.displayName;
            clone.rootTarget = CloneTarget(source.rootTarget);
            clone.rootLabel = source.rootLabel;
            clone.resolvedTypeName = source.resolvedTypeName;
            clone.valueType = source.valueType;
            clone.adapterId = source.adapterId;
            clone.adapterDisplayName = source.adapterDisplayName;
            clone.endpointId = source.endpointId;
            clone.propertyPath = source.propertyPath;
            clone.runtimePayload = source.runtimePayload;
            clone.notes = source.notes;
            clone.segments = new List<PungentAuthoringBindingPathSegment>();
            foreach (PungentAuthoringBindingPathSegment segment in source.segments ?? new List<PungentAuthoringBindingPathSegment>())
            {
                if (segment == null)
                    continue;

                clone.segments.Add(new PungentAuthoringBindingPathSegment
                {
                    kind = segment.kind,
                    key = segment.key,
                    displayName = segment.displayName,
                    typeName = segment.typeName,
                    valueType = segment.valueType,
                    propertyPath = segment.propertyPath,
                    target = CloneTarget(segment.target),
                    notes = segment.notes,
                    order = segment.order
                });
            }

            clone.NormalizeInPlace();
            return clone;
        }

        private static PungentAuthoringTarget CloneTarget(PungentAuthoringTarget source)
        {
            if (source == null)
                return new PungentAuthoringTarget();

            PungentAuthoringTarget clone = new PungentAuthoringTarget
            {
                targetKind = source.targetKind,
                customKind = source.customKind,
                label = source.label,
                providerId = source.providerId,
                rawValue = source.rawValue,
                sourceContext = source.sourceContext,
                contextId = source.contextId,
                propertyPath = source.propertyPath
            };
            clone.NormalizeInPlace();
            return clone;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            return string.Empty;
        }

        private static PungentAuthoringBindingLink CreateColumnBindingLink(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            string rowId,
            string targetGlobalId,
            string targetName,
            PungentDataSheetColumnBinding columnBinding)
        {
            string columnId = columnBinding == null ? string.Empty : columnBinding.columnId;
            string propertyPath = columnBinding == null ? string.Empty : columnBinding.propertyPath;
            string label = columnBinding == null || string.IsNullOrWhiteSpace(columnBinding.endpointLabel)
                ? targetName
                : columnBinding.endpointLabel;
            PungentAuthoringTarget target = CreateSerializedPropertyBindingTarget(targetGlobalId, propertyPath, label);
            string adapterId = columnBinding == null || string.IsNullOrWhiteSpace(columnBinding.adapterId)
                ? PungentSerializedPropertyAuthoringBindingAdapter.AdapterId
                : columnBinding.adapterId;
            string adapterDisplayName = columnBinding == null || string.IsNullOrWhiteSpace(columnBinding.adapterDisplayName)
                ? "Serialized Property"
                : columnBinding.adapterDisplayName;
            string sourceElementId = PungentAuthoringId.Normalize(rowId) + "/" + PungentAuthoringId.Normalize(columnId);

            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromTarget(
                target,
                "data-sheets",
                sheet == null ? string.Empty : sheet.id,
                sourceElementId,
                columnId,
                DirectionFor(profile, columnBinding),
                adapterId,
                adapterDisplayName,
                columnBinding == null ? string.Empty : columnBinding.endpointId,
                label,
                ValueTypeFor(columnBinding));

            link.id = StableBindingLinkId("data-sheet", sheet == null ? string.Empty : sheet.id, profile == null ? string.Empty : profile.id, rowId, columnId, targetGlobalId, propertyPath);
            link.displayName = string.IsNullOrWhiteSpace(columnBinding?.displayName)
                ? "Data Sheet Binding"
                : columnBinding.displayName;
            if (columnBinding != null && columnBinding.bindingPath != null && columnBinding.bindingPath.HasPath)
            {
                link.bindingPath.endpointId = string.IsNullOrWhiteSpace(link.bindingPath.endpointId) ? columnBinding.bindingPath.endpointId : link.bindingPath.endpointId;
                link.bindingPath.adapterId = string.IsNullOrWhiteSpace(link.bindingPath.adapterId) ? columnBinding.bindingPath.adapterId : link.bindingPath.adapterId;
                link.bindingPath.adapterDisplayName = string.IsNullOrWhiteSpace(link.bindingPath.adapterDisplayName) ? columnBinding.bindingPath.adapterDisplayName : link.bindingPath.adapterDisplayName;
                link.bindingPath.valueType = link.bindingPath.valueType == PungentAuthoringBindingValueType.Unknown ? columnBinding.bindingPath.valueType : link.bindingPath.valueType;
            }
            link.bindingSlot.role = PungentAuthoringBindingSlotRole.DataSheetCell;
            link.bindingSlot.interfaceId = "data-sheets";
            link.bindingSlot.itemId = sheet == null ? string.Empty : sheet.id;
            link.bindingSlot.elementId = sourceElementId;
            link.bindingSlot.fieldKey = columnId;
            link.bindingSlot.displayName = string.IsNullOrWhiteSpace(columnBinding?.displayName) ? label : columnBinding.displayName;
            link.bindingSlot.pullEnabled = columnBinding == null || columnBinding.pullEnabled;
            link.bindingSlot.pushEnabled = columnBinding == null || columnBinding.pushEnabled;
            link.bindingSlot.valueType = link.valueType;
            link.NormalizeInPlace();
            return link;
        }

        private static PungentAuthoringTarget CreateSerializedPropertyBindingTarget(BindingTargetInfo target, string propertyPath, string label)
        {
            return CreateSerializedPropertyBindingTarget(
                target == null ? string.Empty : target.globalId,
                propertyPath,
                string.IsNullOrWhiteSpace(label) && target != null ? target.displayName : label);
        }

        private static PungentAuthoringTarget CreateSerializedPropertyBindingTarget(PungentDataSheetTargetBinding targetBinding, PungentDataSheetColumnBinding columnBinding)
        {
            string targetLabel = targetBinding == null ? string.Empty : targetBinding.objectName;
            string propertyPath = columnBinding == null ? string.Empty : columnBinding.propertyPath;
            return CreateSerializedPropertyBindingTarget(
                targetBinding == null ? string.Empty : targetBinding.targetGlobalId,
                propertyPath,
                targetLabel);
        }

        private static PungentAuthoringTarget CreateSerializedPropertyBindingTarget(string targetGlobalId, string propertyPath, string label)
        {
            PungentAuthoringTarget target = PungentAuthoringTarget.Create(
                PungentAuthoringTargetKind.SerializedPropertyPath,
                propertyPath,
                label,
                PungentSerializedPropertyAuthoringBindingAdapter.AdapterId);
            target.contextId = targetGlobalId ?? string.Empty;
            target.propertyPath = propertyPath ?? string.Empty;
            target.sourceContext = "DataSheetBindingProfile";
            target.customKind = PungentSerializedPropertyAuthoringBindingAdapter.AdapterId;
            target.NormalizeInPlace();
            return target;
        }

        private static PungentAuthoringBindingLinkDirection DirectionFor(PungentDataSheetBindingProfile profile, PungentDataSheetColumnBinding columnBinding)
        {
            if (profile != null && profile.direction == PungentDataSheetBindingDirection.PullOnly)
                return PungentAuthoringBindingLinkDirection.ReadOnly;
            if (profile != null && profile.direction == PungentDataSheetBindingDirection.PushOnly)
                return PungentAuthoringBindingLinkDirection.WriteOnly;

            bool pull = columnBinding == null || columnBinding.pullEnabled;
            bool push = columnBinding == null || columnBinding.pushEnabled;
            if (pull && push)
                return PungentAuthoringBindingLinkDirection.TwoWay;
            if (push)
                return PungentAuthoringBindingLinkDirection.WriteOnly;
            return PungentAuthoringBindingLinkDirection.ReadOnly;
        }

        private static PungentAuthoringBindingValueType ValueTypeFor(PungentDataSheetColumnBinding columnBinding)
        {
            if (columnBinding == null)
                return PungentAuthoringBindingValueType.Unknown;
            if (columnBinding.valueType != PungentAuthoringBindingValueType.Unknown)
                return columnBinding.valueType;
            if (Enum.TryParse(columnBinding.expectedPropertyType, true, out PungentAuthoringBindingValueType runtimeType))
                return runtimeType;
            if (Enum.TryParse(columnBinding.expectedPropertyType, true, out PungentAuthoringBindingValueKind editorType))
                return PungentAuthoringBindingBridgeService.ToRuntimeValueType(editorType);
            return PungentAuthoringBindingValueType.Unknown;
        }

        private static string StableBindingLinkId(params string[] parts)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int p = 0; p < parts.Length; p++)
                {
                    string value = parts[p] ?? string.Empty;
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619;
                    }

                    hash ^= 31;
                    hash *= 16777619;
                }

                return "binding-" + hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        private static PungentDataSheetApplyPreview BuildCellPreview(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            PungentDataSheetRow row,
            PungentDataSheetColumnBinding columnBinding,
            PungentDataSheetTargetBinding targetBinding,
            PungentDataSheetConflictPolicy conflictPolicy)
        {
            PungentDataSheetCell cell = sheet.FindCell(row.id, columnBinding.columnId);
            string newValue = cell != null ? cell.rawValue ?? string.Empty : string.Empty;
            PungentDataSheetApplyPreview preview = new PungentDataSheetApplyPreview
            {
                rowId = row.id,
                columnId = columnBinding.columnId,
                targetGlobalId = targetBinding != null ? targetBinding.targetGlobalId : string.Empty,
                targetName = targetBinding != null ? targetBinding.objectName : string.Empty,
                propertyPath = columnBinding.propertyPath,
                bindingLinkId = columnBinding.bindingLinkId,
                adapterId = columnBinding.adapterId,
                adapterDisplayName = columnBinding.adapterDisplayName,
                valueType = columnBinding.valueType,
                newValue = newValue,
                status = PungentDataSheetApplyPreviewStatus.Skipped,
                reason = string.Empty
            };

            if (targetBinding == null || string.IsNullOrWhiteSpace(targetBinding.targetGlobalId))
            {
                preview.reason = "Row is not linked to a binding target. Pull targets first.";
                return preview;
            }

            if (!targetBinding.pushEnabled)
            {
                preview.reason = "Apply is disabled for this row.";
                return preview;
            }

            PungentAuthoringBindingLink bindingLink = CreateColumnBindingLink(sheet, profile, row.id, targetBinding.targetGlobalId, targetBinding.objectName, columnBinding);
            PungentAuthoringBindingLinkPreview sharedPreview = PungentAuthoringBindingBridgeService.PreviewLink(bindingLink);
            if (sharedPreview.endpointPreview == null || !sharedPreview.canRead)
            {
                preview.reason = string.IsNullOrWhiteSpace(sharedPreview.disabledReason)
                    ? "Endpoint could not be read."
                    : sharedPreview.disabledReason;
                return preview;
            }

            preview.targetName = string.IsNullOrWhiteSpace(targetBinding.objectName)
                ? sharedPreview.endpointPreview.targetLabel
                : targetBinding.objectName;
            preview.oldValue = sharedPreview.endpointPreview.currentValue;
            preview.bindingLinkId = bindingLink.id;
            preview.adapterId = bindingLink.adapterId;
            preview.adapterDisplayName = bindingLink.adapterDisplayName;
            preview.valueType = bindingLink.valueType;
            string unsupportedReason;
            if (!PungentAuthoringBindingBridgeService.CanApply(bindingLink, out unsupportedReason))
            {
                preview.reason = unsupportedReason;
                return preview;
            }

            if (conflictPolicy == PungentDataSheetConflictPolicy.FillBlanksOnly && !string.IsNullOrWhiteSpace(preview.oldValue))
            {
                preview.reason = "Skipped because the project value is already filled.";
                return preview;
            }

            if (conflictPolicy == PungentDataSheetConflictPolicy.SkipIfProjectChangedSincePull)
            {
                if (cell == null || string.IsNullOrEmpty(cell.lastPulledUtc))
                {
                    preview.reason = "Skipped because this cell has not been pulled yet.";
                    return preview;
                }

                if (!string.Equals(preview.oldValue, cell.lastPulledValue ?? string.Empty, StringComparison.Ordinal))
                {
                    preview.reason = "Skipped because the project value changed since the last pull.";
                    return preview;
                }
            }

            if (string.Equals(preview.oldValue, preview.newValue, StringComparison.Ordinal))
            {
                preview.reason = "Project value already matches.";
                return preview;
            }

            preview.status = PungentDataSheetApplyPreviewStatus.Ready;
            preview.reason = "Ready.";
            return preview;
        }

        private static PungentDataSheetApplyPreview CreateSkippedPreview(string rowId, string columnId, string targetName, string propertyPath, string reason)
        {
            return new PungentDataSheetApplyPreview
            {
                rowId = rowId ?? string.Empty,
                columnId = columnId ?? string.Empty,
                targetName = targetName ?? string.Empty,
                propertyPath = propertyPath ?? string.Empty,
                status = PungentDataSheetApplyPreviewStatus.Skipped,
                reason = reason ?? string.Empty
            };
        }

        private static bool ShouldIncludeCell(
            PungentDataSheet sheet,
            string rowId,
            string columnId,
            PungentDataSheetApplyScope scope,
            HashSet<string> selectedRows,
            HashSet<string> selectedCellKeys)
        {
            switch (scope)
            {
                case PungentDataSheetApplyScope.SelectedCells:
                    return selectedCellKeys.Contains(CellKey(rowId, columnId));
                case PungentDataSheetApplyScope.SelectedRows:
                    return selectedRows.Contains(PungentAuthoringId.Normalize(rowId));
                case PungentDataSheetApplyScope.ChangedCells:
                    PungentDataSheetCell cell = sheet.FindCell(rowId, columnId);
                    return cell != null && cell.propagationStatus == PungentDataSheetPropagationStatus.Changed;
                case PungentDataSheetApplyScope.FullSheet:
                    return true;
                default:
                    return false;
            }
        }

        private static List<BindingTargetInfo> CollectTargets(PungentDataSheetBindingProfile profile)
        {
            if (profile.targetSource == PungentDataSheetBindingTargetSource.CurrentSelection)
            {
                List<BindingTargetInfo> selectedTargets = CollectTargetsFromObjects(Selection.objects, profile);
                if (selectedTargets.Count > 0)
                    return selectedTargets;
            }

            List<Object> objects = new List<Object>();
            foreach (string globalId in profile.manualTargetGlobalIds ?? new List<string>())
            {
                Object target = ResolveGlobalObject(globalId);
                if (target != null)
                    objects.Add(target);
            }

            foreach (PungentDataSheetTargetBinding targetBinding in profile.targetBindings ?? new List<PungentDataSheetTargetBinding>())
            {
                if (targetBinding == null || string.IsNullOrWhiteSpace(targetBinding.targetGlobalId))
                    continue;
                if ((profile.manualTargetGlobalIds ?? new List<string>()).Exists(id => string.Equals(id, targetBinding.targetGlobalId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Object target = ResolveGlobalObject(targetBinding.targetGlobalId);
                if (target != null)
                    objects.Add(target);
            }

            return CollectTargetsFromObjects(objects.ToArray(), profile);
        }

        private static List<BindingTargetInfo> CollectTargetsFromObjects(Object[] objects, PungentDataSheetBindingProfile profile)
        {
            List<BindingTargetInfo> targets = new List<BindingTargetInfo>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (objects == null)
                return targets;

            bool hasTypeFilter = !string.IsNullOrWhiteSpace(profile?.targetTypeName);
            for (int i = 0; i < objects.Length; i++)
            {
                Object candidate = objects[i];
                if (candidate == null)
                    continue;

                GameObject gameObject = candidate as GameObject;
                if (gameObject != null)
                {
                    if (!hasTypeFilter || MatchesTypeFilter(gameObject, profile))
                        AddTargetInfo(gameObject, targets, ids);

                    if (hasTypeFilter)
                    {
                        Component[] components = gameObject.GetComponents<Component>();
                        for (int c = 0; c < components.Length; c++)
                            if (components[c] != null && MatchesTypeFilter(components[c], profile))
                                AddTargetInfo(components[c], targets, ids);
                    }

                    continue;
                }

                if (IsSupportedTarget(candidate) && MatchesTypeFilter(candidate, profile))
                    AddTargetInfo(candidate, targets, ids);
            }

            return targets;
        }

        private static int AddTargetsToManualList(PungentDataSheetBindingProfile profile, List<BindingTargetInfo> targets)
        {
            if (profile.manualTargetGlobalIds == null)
                profile.manualTargetGlobalIds = new List<string>();

            int added = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                string id = targets[i].globalId;
                if (string.IsNullOrWhiteSpace(id) || profile.manualTargetGlobalIds.Exists(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                profile.manualTargetGlobalIds.Add(id);
                added++;
            }

            profile.NormalizeInPlace();
            return added;
        }

        private static void AddTargetInfo(Object target, List<BindingTargetInfo> targets, HashSet<string> ids)
        {
            if (target == null)
                return;

            string globalId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            if (string.IsNullOrWhiteSpace(globalId) || !ids.Add(globalId))
                return;

            string assetPath = AssetDatabase.GetAssetPath(target);
            targets.Add(new BindingTargetInfo
            {
                unityObject = target,
                globalId = globalId,
                assetPath = assetPath ?? string.Empty,
                assetGuid = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath),
                displayName = target.name ?? target.GetType().Name,
                typeName = target.GetType().FullName ?? target.GetType().Name
            });
        }

        private static bool IsSupportedTarget(Object target)
        {
            return target is ScriptableObject || target is GameObject || target is Component;
        }

        private static bool MatchesTypeFilter(Object target, PungentDataSheetBindingProfile profile)
        {
            if (target == null || !IsSupportedTarget(target))
                return false;

            string filter = profile == null ? string.Empty : profile.targetTypeName;
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            Type targetType = target.GetType();
            filter = filter.Trim();
            if (string.Equals(targetType.Name, filter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetType.FullName, filter, StringComparison.OrdinalIgnoreCase))
                return true;

            Type filterType = FindType(filter);
            return filterType != null && filterType.IsAssignableFrom(targetType);
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            Type direct = Type.GetType(typeName, false);
            if (direct != null)
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName, false);
                if (type != null)
                    return type;
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type != null && string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase))
                        return type;
                }
            }

            return null;
        }

        private static PungentDataSheetRow GetOrCreateTargetRow(PungentDataSheet sheet, PungentDataSheetBindingProfile profile, BindingTargetInfo target)
        {
            PungentDataSheetTargetBinding targetBinding = profile.FindTargetBindingByGlobalId(target.globalId);
            PungentDataSheetRow row = targetBinding != null ? sheet.FindRow(targetBinding.rowId) : null;
            if (row != null)
                return row;

            if (!string.IsNullOrWhiteSpace(profile.rowIdentityColumnId))
            {
                foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
                    if (cell != null && PungentAuthoringId.EqualsId(cell.columnId, profile.rowIdentityColumnId) && string.Equals(cell.rawValue, target.globalId, StringComparison.OrdinalIgnoreCase))
                        return sheet.FindRow(cell.rowId) ?? sheet.AddRow(target.displayName);
            }

            return sheet.AddRow(string.IsNullOrWhiteSpace(target.displayName) ? "Bound Target" : target.displayName);
        }

        private static void SetPulledCell(PungentDataSheetCell cell, string value, string timestampUtc, string message)
        {
            if (cell == null)
                return;

            cell.rawValue = value ?? string.Empty;
            cell.displayValue = cell.rawValue;
            cell.lastPulledValue = cell.rawValue;
            cell.lastPulledUtc = timestampUtc ?? string.Empty;
            cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
            cell.propagationStatus = PungentDataSheetPropagationStatus.Clean;
            cell.propagationMessage = message ?? string.Empty;
        }

        private static void MarkCellFailed(PungentDataSheetCell cell, string message)
        {
            if (cell == null)
                return;

            cell.validationStatus = PungentDataSheetCellValidationStatus.Warning;
            cell.propagationStatus = PungentDataSheetPropagationStatus.Failed;
            cell.propagationMessage = message ?? string.Empty;
        }

        private static void MarkPreviewFailed(PungentDataSheet sheet, PungentDataSheetApplyPreview preview, string reason)
        {
            preview.status = PungentDataSheetApplyPreviewStatus.Failed;
            preview.reason = reason ?? string.Empty;
            PungentDataSheetCell cell = sheet.FindCell(preview.rowId, preview.columnId);
            if (cell != null)
            {
                cell.propagationStatus = PungentDataSheetPropagationStatus.Failed;
                cell.propagationMessage = preview.reason;
            }
        }

        private static HashSet<string> NormalizeSet(IEnumerable<string> values)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values ?? new List<string>())
            {
                string normalized = PungentAuthoringId.Normalize(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                    set.Add(normalized);
            }

            return set;
        }

        private static HashSet<string> NormalizeCellSet(IEnumerable<PungentDataSheetCellAddress> values)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentDataSheetCellAddress address in values ?? new List<PungentDataSheetCellAddress>())
                set.Add(CellKey(address.rowId, address.columnId));
            return set;
        }

        private static string CellKey(string rowId, string columnId)
        {
            return PungentAuthoringId.Normalize(rowId) + "\u001F" + PungentAuthoringId.Normalize(columnId);
        }
    }
#endif
}
