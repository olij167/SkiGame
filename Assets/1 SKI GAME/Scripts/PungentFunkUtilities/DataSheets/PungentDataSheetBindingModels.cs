using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.DataSheets
{
    [Serializable]
    public sealed class PungentDataSheetBindingProfile
    {
        public const int CurrentMigrationVersion = 2;

        public string id = string.Empty;
        public string displayName = "Default Binding";
        public PungentDataSheetBindingTargetSource targetSource = PungentDataSheetBindingTargetSource.CurrentSelection;
        public PungentDataSheetBindingDirection direction = PungentDataSheetBindingDirection.TwoWay;
        public PungentAuthoringBindingApplyMode applyMode = PungentAuthoringBindingApplyMode.ManualApply;
        public string assetFolderPath = string.Empty;
        public bool includeSubFolders = true;
        public string targetTypeName = string.Empty;
        public string rowIdentityColumnId = string.Empty;
        public string objectNameColumnId = string.Empty;
        public List<string> manualTargetGlobalIds = new List<string>();
        public List<PungentDataSheetColumnBinding> columnBindings = new List<PungentDataSheetColumnBinding>();
        public List<PungentDataSheetTargetBinding> targetBindings = new List<PungentDataSheetTargetBinding>();
        public int migrationVersion = CurrentMigrationVersion;

        public static PungentDataSheetBindingProfile Create(string displayName)
        {
            PungentDataSheetBindingProfile profile = new PungentDataSheetBindingProfile
            {
                id = PungentAuthoringId.NewValue(),
                displayName = string.IsNullOrWhiteSpace(displayName) ? "Default Binding" : displayName.Trim()
            };
            profile.NormalizeInPlace();
            return profile;
        }

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = string.IsNullOrWhiteSpace(displayName) ? "Default Binding" : displayName.Trim();
            assetFolderPath = assetFolderPath == null ? string.Empty : assetFolderPath.Trim();
            targetTypeName = targetTypeName == null ? string.Empty : targetTypeName.Trim();
            rowIdentityColumnId = PungentAuthoringId.Normalize(rowIdentityColumnId);
            objectNameColumnId = PungentAuthoringId.Normalize(objectNameColumnId);
            migrationVersion = Math.Max(CurrentMigrationVersion, migrationVersion);

            if (manualTargetGlobalIds == null)
                manualTargetGlobalIds = new List<string>();
            if (columnBindings == null)
                columnBindings = new List<PungentDataSheetColumnBinding>();
            if (targetBindings == null)
                targetBindings = new List<PungentDataSheetTargetBinding>();

            for (int i = 0; i < manualTargetGlobalIds.Count; i++)
                manualTargetGlobalIds[i] = manualTargetGlobalIds[i] == null ? string.Empty : manualTargetGlobalIds[i].Trim();
            manualTargetGlobalIds.RemoveAll(string.IsNullOrWhiteSpace);

            for (int i = 0; i < columnBindings.Count; i++)
                columnBindings[i]?.NormalizeInPlace();
            for (int i = 0; i < targetBindings.Count; i++)
                targetBindings[i]?.NormalizeInPlace();

            columnBindings.RemoveAll(binding => binding == null || string.IsNullOrWhiteSpace(binding.columnId));
            targetBindings.RemoveAll(binding => binding == null || string.IsNullOrWhiteSpace(binding.rowId));
        }

        public PungentDataSheetColumnBinding FindColumnBinding(string columnId)
        {
            if (columnBindings == null || string.IsNullOrWhiteSpace(columnId))
                return null;

            return columnBindings.Find(binding => binding != null && PungentAuthoringId.EqualsId(binding.columnId, columnId));
        }

        public PungentDataSheetColumnBinding GetOrCreateColumnBinding(string columnId, string displayName = null)
        {
            if (columnBindings == null)
                columnBindings = new List<PungentDataSheetColumnBinding>();

            PungentDataSheetColumnBinding binding = FindColumnBinding(columnId);
            if (binding != null)
                return binding;

            binding = new PungentDataSheetColumnBinding
            {
                columnId = PungentAuthoringId.Normalize(columnId),
                displayName = displayName ?? string.Empty
            };
            binding.NormalizeInPlace();
            columnBindings.Add(binding);
            return binding;
        }

        public PungentDataSheetTargetBinding FindTargetBindingByRow(string rowId)
        {
            if (targetBindings == null || string.IsNullOrWhiteSpace(rowId))
                return null;

            return targetBindings.Find(binding => binding != null && PungentAuthoringId.EqualsId(binding.rowId, rowId));
        }

        public PungentDataSheetTargetBinding FindTargetBindingByGlobalId(string globalId)
        {
            if (targetBindings == null || string.IsNullOrWhiteSpace(globalId))
                return null;

            return targetBindings.Find(binding => binding != null && string.Equals(binding.targetGlobalId, globalId, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Serializable]
    public sealed class PungentDataSheetColumnBinding
    {
        public string columnId = string.Empty;
        public string displayName = string.Empty;
        public string propertyPath = string.Empty;
        public string expectedPropertyType = string.Empty;
        public string bindingLinkId = string.Empty;
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string endpointId = string.Empty;
        public string endpointLabel = string.Empty;
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public PungentAuthoringBindingPath bindingPath = new PungentAuthoringBindingPath();
        public PungentAuthoringBindingSlot bindingSlot = new PungentAuthoringBindingSlot();
        public bool pullEnabled = true;
        public bool pushEnabled = true;

        public bool HasPropertyPath => !string.IsNullOrWhiteSpace(propertyPath);

        public void NormalizeInPlace()
        {
            columnId = PungentAuthoringId.Normalize(columnId);
            displayName = displayName == null ? string.Empty : displayName.Trim();
            propertyPath = propertyPath == null ? string.Empty : propertyPath.Trim();
            expectedPropertyType = expectedPropertyType == null ? string.Empty : expectedPropertyType.Trim();
            bindingLinkId = PungentAuthoringId.Normalize(bindingLinkId);
            adapterId = adapterId == null ? string.Empty : adapterId.Trim();
            adapterDisplayName = adapterDisplayName == null ? string.Empty : adapterDisplayName.Trim();
            endpointId = endpointId == null ? string.Empty : endpointId.Trim();
            endpointLabel = endpointLabel == null ? string.Empty : endpointLabel.Trim();
            bindingPath = bindingPath ?? new PungentAuthoringBindingPath();
            bindingPath.NormalizeInPlace();
            bindingSlot = bindingSlot ?? new PungentAuthoringBindingSlot();
            if (bindingSlot.role == PungentAuthoringBindingSlotRole.Unknown)
                bindingSlot.role = PungentAuthoringBindingSlotRole.DataSheetColumn;
            if (string.IsNullOrWhiteSpace(bindingSlot.elementId))
                bindingSlot.elementId = columnId;
            if (string.IsNullOrWhiteSpace(bindingSlot.fieldKey))
                bindingSlot.fieldKey = displayName;
            if (bindingSlot.valueType == PungentAuthoringBindingValueType.Unknown)
                bindingSlot.valueType = valueType;
            if (string.IsNullOrWhiteSpace(bindingSlot.pathId))
                bindingSlot.pathId = bindingPath.id;
            bindingSlot.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentDataSheetTargetBinding
    {
        public string rowId = string.Empty;
        public string targetGlobalId = string.Empty;
        public string assetGuid = string.Empty;
        public string assetPath = string.Empty;
        public string objectName = string.Empty;
        public string targetTypeName = string.Empty;
        public string lastPulledUtc = string.Empty;
        public bool pullEnabled = true;
        public bool pushEnabled = true;
        public PungentAuthoringBindingSlot bindingSlot = new PungentAuthoringBindingSlot();

        public void NormalizeInPlace()
        {
            rowId = PungentAuthoringId.Normalize(rowId);
            targetGlobalId = targetGlobalId == null ? string.Empty : targetGlobalId.Trim();
            assetGuid = assetGuid == null ? string.Empty : assetGuid.Trim();
            assetPath = assetPath == null ? string.Empty : assetPath.Trim();
            objectName = objectName == null ? string.Empty : objectName.Trim();
            targetTypeName = targetTypeName == null ? string.Empty : targetTypeName.Trim();
            lastPulledUtc = lastPulledUtc == null ? string.Empty : lastPulledUtc.Trim();
            bindingSlot = bindingSlot ?? new PungentAuthoringBindingSlot();
            if (bindingSlot.role == PungentAuthoringBindingSlotRole.Unknown)
                bindingSlot.role = PungentAuthoringBindingSlotRole.DataSheetRow;
            if (string.IsNullOrWhiteSpace(bindingSlot.elementId))
                bindingSlot.elementId = rowId;
            if (string.IsNullOrWhiteSpace(bindingSlot.displayName))
                bindingSlot.displayName = objectName;
            bindingSlot.pullEnabled = pullEnabled;
            bindingSlot.pushEnabled = pushEnabled;
            bindingSlot.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentDataSheetApplyPreview
    {
        public string rowId = string.Empty;
        public string columnId = string.Empty;
        public string targetGlobalId = string.Empty;
        public string targetName = string.Empty;
        public string propertyPath = string.Empty;
        public string bindingLinkId = string.Empty;
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public string oldValue = string.Empty;
        public string newValue = string.Empty;
        public PungentDataSheetApplyPreviewStatus status = PungentDataSheetApplyPreviewStatus.Pending;
        public string reason = string.Empty;
        public bool selected = true;

        public bool CanApply => selected && status == PungentDataSheetApplyPreviewStatus.Ready;
    }
}
