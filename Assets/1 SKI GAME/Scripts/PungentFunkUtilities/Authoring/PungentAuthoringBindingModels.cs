using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Authoring
{
    public enum PungentAuthoringBindingLinkDirection
    {
        ReadOnly = 0,
        WriteOnly = 10,
        TwoWay = 20
    }

    public enum PungentAuthoringBindingValueType
    {
        Unknown = 0,
        Text = 10,
        Number = 20,
        Boolean = 30,
        Enum = 40,
        ObjectReference = 50,
        Json = 60,
        UnityObject = 70,
        AssetReference = 80,
        SceneObjectReference = 90,
        ComponentReference = 100,
        Sprite = 110,
        AudioClip = 120,
        Vector2 = 130,
        Vector3 = 140,
        Color = 150
    }

    public enum PungentAuthoringBindingApplyMode
    {
        ManualApply = 0,
        AutoApplyEditMode = 10,
        AutoApplyAlways = 20
    }

    public enum PungentAuthoringBindingPathSegmentKind
    {
        Unknown = 0,
        RootTarget = 10,
        Asset = 20,
        SceneObject = 30,
        Component = 40,
        Field = 50,
        Property = 60,
        ArrayElement = 70,
        DictionaryEntry = 80,
        MethodResult = 90,
        Token = 100,
        Custom = 1000
    }

    public enum PungentAuthoringBindingSlotRole
    {
        Unknown = 0,
        DataSheetRow = 10,
        DataSheetColumn = 20,
        DataSheetCell = 30,
        RichDocumentToken = 100,
        RichDocumentSemanticSpan = 110,
        RichDocumentInsertionField = 120,
        BoardNode = 200,
        BoardGroup = 210,
        BoardPort = 220,
        BoardParameter = 230,
        Custom = 1000
    }

    [Serializable]
    public sealed class PungentAuthoringBindingPathSegment
    {
        public PungentAuthoringBindingPathSegmentKind kind = PungentAuthoringBindingPathSegmentKind.Unknown;
        public string key = string.Empty;
        public string displayName = string.Empty;
        public string typeName = string.Empty;
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public string propertyPath = string.Empty;
        public PungentAuthoringTarget target;
        public string notes = string.Empty;
        public int order;

        public void NormalizeInPlace(int fallbackOrder = 0)
        {
            key = key == null ? string.Empty : key.Trim();
            displayName = displayName == null ? string.Empty : displayName.Trim();
            typeName = typeName == null ? string.Empty : typeName.Trim();
            propertyPath = propertyPath == null ? string.Empty : propertyPath.Trim();
            notes = notes == null ? string.Empty : notes.Trim();
            order = Math.Max(0, order < 0 ? fallbackOrder : order);
            target?.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentAuthoringBindingPath
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public PungentAuthoringTarget rootTarget = new PungentAuthoringTarget();
        public string rootLabel = string.Empty;
        public string resolvedTypeName = string.Empty;
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string endpointId = string.Empty;
        public string propertyPath = string.Empty;
        public List<PungentAuthoringBindingPathSegment> segments = new List<PungentAuthoringBindingPathSegment>();
        public string runtimePayload = string.Empty;
        public string notes = string.Empty;

        public bool HasTarget => rootTarget != null && rootTarget.HasTarget;
        public bool HasPath => HasTarget || (segments != null && segments.Exists(segment => segment != null && !string.IsNullOrWhiteSpace(segment.key)));

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = displayName == null ? string.Empty : displayName.Trim();
            rootLabel = rootLabel == null ? string.Empty : rootLabel.Trim();
            resolvedTypeName = resolvedTypeName == null ? string.Empty : resolvedTypeName.Trim();
            adapterId = adapterId == null ? string.Empty : adapterId.Trim();
            adapterDisplayName = adapterDisplayName == null ? string.Empty : adapterDisplayName.Trim();
            endpointId = endpointId == null ? string.Empty : endpointId.Trim();
            propertyPath = propertyPath == null ? string.Empty : propertyPath.Trim();
            runtimePayload = runtimePayload == null ? string.Empty : runtimePayload.Trim();
            notes = notes == null ? string.Empty : notes.Trim();
            rootTarget = rootTarget ?? new PungentAuthoringTarget();
            rootTarget.NormalizeInPlace();
            segments = segments ?? new List<PungentAuthoringBindingPathSegment>();

            for (int i = segments.Count - 1; i >= 0; i--)
            {
                if (segments[i] == null)
                {
                    segments.RemoveAt(i);
                    continue;
                }

                segments[i].NormalizeInPlace(i);
            }
        }
    }

    [Serializable]
    public sealed class PungentAuthoringBindingSlot
    {
        public string id = string.Empty;
        public PungentAuthoringBindingSlotRole role = PungentAuthoringBindingSlotRole.Unknown;
        public string interfaceId = string.Empty;
        public string itemId = string.Empty;
        public string elementId = string.Empty;
        public string fieldKey = string.Empty;
        public string displayName = string.Empty;
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public string pathId = string.Empty;
        public string bindingLinkId = string.Empty;
        public bool enabled = true;
        public bool pullEnabled = true;
        public bool pushEnabled = true;
        public string runtimePayload = string.Empty;
        public string notes = string.Empty;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            interfaceId = interfaceId == null ? string.Empty : interfaceId.Trim();
            itemId = PungentAuthoringId.Normalize(itemId);
            elementId = PungentAuthoringId.Normalize(elementId);
            fieldKey = fieldKey == null ? string.Empty : fieldKey.Trim();
            displayName = displayName == null ? string.Empty : displayName.Trim();
            pathId = PungentAuthoringId.Normalize(pathId);
            bindingLinkId = PungentAuthoringId.Normalize(bindingLinkId);
            runtimePayload = runtimePayload == null ? string.Empty : runtimePayload.Trim();
            notes = notes == null ? string.Empty : notes.Trim();
        }
    }

    [Serializable]
    public sealed class PungentAuthoringBindingDiscoverySnapshot
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string createdUtc = string.Empty;
        public PungentAuthoringTarget rootTarget = new PungentAuthoringTarget();
        public string rootLabel = string.Empty;
        public string sourceContext = string.Empty;
        public List<PungentAuthoringBindingPath> paths = new List<PungentAuthoringBindingPath>();
        public List<PungentAuthoringBindingSlot> slots = new List<PungentAuthoringBindingSlot>();

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = string.IsNullOrWhiteSpace(displayName) ? "Binding Discovery Snapshot" : displayName.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? DateTime.UtcNow.ToString("o") : createdUtc.Trim();
            rootTarget = rootTarget ?? new PungentAuthoringTarget();
            rootTarget.NormalizeInPlace();
            rootLabel = rootLabel == null ? string.Empty : rootLabel.Trim();
            sourceContext = sourceContext == null ? string.Empty : sourceContext.Trim();
            paths = paths ?? new List<PungentAuthoringBindingPath>();
            slots = slots ?? new List<PungentAuthoringBindingSlot>();

            for (int i = paths.Count - 1; i >= 0; i--)
            {
                if (paths[i] == null)
                {
                    paths.RemoveAt(i);
                    continue;
                }

                paths[i].NormalizeInPlace();
            }

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i] == null)
                {
                    slots.RemoveAt(i);
                    continue;
                }

                slots[i].NormalizeInPlace();
            }
        }
    }

    [Serializable]
    public sealed class PungentAuthoringBindingLink
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string sourceInterfaceId = string.Empty;
        public string sourceItemId = string.Empty;
        public string sourceElementId = string.Empty;
        public string sourceFieldKey = string.Empty;
        public PungentAuthoringBindingLinkDirection direction = PungentAuthoringBindingLinkDirection.TwoWay;
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string endpointId = string.Empty;
        public string endpointLabel = string.Empty;
        public PungentAuthoringTarget target = new PungentAuthoringTarget();
        public PungentAuthoringBindingPath bindingPath = new PungentAuthoringBindingPath();
        public PungentAuthoringBindingSlot bindingSlot = new PungentAuthoringBindingSlot();
        public string runtimeAdapterId = string.Empty;
        public string runtimePayload = string.Empty;
        public bool runtimeEnabled;
        public string notes = string.Empty;

        public bool HasTarget => target != null && target.HasTarget;
        public bool CanRead => direction == PungentAuthoringBindingLinkDirection.ReadOnly ||
                               direction == PungentAuthoringBindingLinkDirection.TwoWay;
        public bool CanApply => direction == PungentAuthoringBindingLinkDirection.WriteOnly ||
                                direction == PungentAuthoringBindingLinkDirection.TwoWay;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = string.IsNullOrWhiteSpace(displayName) ? "Binding Link" : displayName.Trim();
            sourceInterfaceId = sourceInterfaceId == null ? string.Empty : sourceInterfaceId.Trim();
            sourceItemId = PungentAuthoringId.Normalize(sourceItemId);
            sourceElementId = PungentAuthoringId.Normalize(sourceElementId);
            sourceFieldKey = sourceFieldKey == null ? string.Empty : sourceFieldKey.Trim();
            adapterId = adapterId == null ? string.Empty : adapterId.Trim();
            adapterDisplayName = adapterDisplayName == null ? string.Empty : adapterDisplayName.Trim();
            endpointId = endpointId == null ? string.Empty : endpointId.Trim();
            endpointLabel = endpointLabel == null ? string.Empty : endpointLabel.Trim();
            runtimeAdapterId = runtimeAdapterId == null ? string.Empty : runtimeAdapterId.Trim();
            runtimePayload = runtimePayload == null ? string.Empty : runtimePayload.Trim();
            notes = notes == null ? string.Empty : notes.Trim();
            target = target ?? new PungentAuthoringTarget();
            target.NormalizeInPlace();
            bindingPath = bindingPath ?? new PungentAuthoringBindingPath();
            if (bindingPath.rootTarget == null || !bindingPath.rootTarget.HasTarget)
                bindingPath.rootTarget = target;
            if (string.IsNullOrWhiteSpace(bindingPath.adapterId))
                bindingPath.adapterId = adapterId;
            if (string.IsNullOrWhiteSpace(bindingPath.adapterDisplayName))
                bindingPath.adapterDisplayName = adapterDisplayName;
            if (string.IsNullOrWhiteSpace(bindingPath.endpointId))
                bindingPath.endpointId = endpointId;
            if (string.IsNullOrWhiteSpace(bindingPath.displayName))
                bindingPath.displayName = endpointLabel;
            if (string.IsNullOrWhiteSpace(bindingPath.propertyPath) && target != null)
                bindingPath.propertyPath = target.propertyPath;
            if (bindingPath.valueType == PungentAuthoringBindingValueType.Unknown)
                bindingPath.valueType = valueType;
            bindingPath.NormalizeInPlace();
            bindingSlot = bindingSlot ?? new PungentAuthoringBindingSlot();
            if (string.IsNullOrWhiteSpace(bindingSlot.interfaceId))
                bindingSlot.interfaceId = sourceInterfaceId;
            if (string.IsNullOrWhiteSpace(bindingSlot.itemId))
                bindingSlot.itemId = sourceItemId;
            if (string.IsNullOrWhiteSpace(bindingSlot.elementId))
                bindingSlot.elementId = sourceElementId;
            if (string.IsNullOrWhiteSpace(bindingSlot.fieldKey))
                bindingSlot.fieldKey = sourceFieldKey;
            if (string.IsNullOrWhiteSpace(bindingSlot.displayName))
                bindingSlot.displayName = displayName;
            if (bindingSlot.valueType == PungentAuthoringBindingValueType.Unknown)
                bindingSlot.valueType = valueType;
            if (string.IsNullOrWhiteSpace(bindingSlot.pathId))
                bindingSlot.pathId = bindingPath.id;
            if (string.IsNullOrWhiteSpace(bindingSlot.bindingLinkId))
                bindingSlot.bindingLinkId = id;
            bindingSlot.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentAuthoringBindingPlan
    {
        public const int CurrentMigrationVersion = 2;

        public string id = string.Empty;
        public string displayName = "Authoring Binding Plan";
        public string sourceInterfaceId = string.Empty;
        public string sourceItemId = string.Empty;
        public string sourceLabel = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public int migrationVersion = CurrentMigrationVersion;
        public List<PungentAuthoringBindingLink> links = new List<PungentAuthoringBindingLink>();

        public void NormalizeInPlace()
        {
            string now = DateTime.UtcNow.ToString("o");
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = string.IsNullOrWhiteSpace(displayName) ? "Authoring Binding Plan" : displayName.Trim();
            sourceInterfaceId = sourceInterfaceId == null ? string.Empty : sourceInterfaceId.Trim();
            sourceItemId = PungentAuthoringId.Normalize(sourceItemId);
            sourceLabel = sourceLabel == null ? string.Empty : sourceLabel.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? now : createdUtc.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? createdUtc : updatedUtc.Trim();
            migrationVersion = Math.Max(CurrentMigrationVersion, migrationVersion);
            links = links ?? new List<PungentAuthoringBindingLink>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = links.Count - 1; i >= 0; i--)
            {
                if (links[i] == null)
                {
                    links.RemoveAt(i);
                    continue;
                }

                links[i].NormalizeInPlace();
                if (!seen.Add(links[i].id))
                {
                    links[i].id = PungentAuthoringId.NewValue();
                    links[i].NormalizeInPlace();
                    seen.Add(links[i].id);
                }
            }
        }

        public void Touch()
        {
            updatedUtc = DateTime.UtcNow.ToString("o");
        }
    }

    [CreateAssetMenu(fileName = "AuthoringBindingPlan", menuName = "PungentFunk Utilities/Authoring/Binding Plan")]
    public sealed class PungentAuthoringBindingPlanAsset : ScriptableObject
    {
        public PungentAuthoringBindingPlan plan = new PungentAuthoringBindingPlan();

        private void OnValidate()
        {
            plan = plan ?? new PungentAuthoringBindingPlan();
            plan.NormalizeInPlace();
        }
    }

    public interface IPungentAuthoringRuntimeBindingAdapter
    {
        string AdapterId { get; }
        string DisplayName { get; }
        bool CanRead(PungentAuthoringBindingLink link, out string disabledReason);
        bool TryRead(PungentAuthoringBindingLink link, out string value, out string message);
        bool CanApply(PungentAuthoringBindingLink link, out string disabledReason);
        bool TryApply(PungentAuthoringBindingLink link, string value, out string message);
    }
}
