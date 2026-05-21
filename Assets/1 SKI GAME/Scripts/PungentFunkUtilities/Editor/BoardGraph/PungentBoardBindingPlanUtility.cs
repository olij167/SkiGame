using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public static class PungentBoardBindingPlanUtility
    {
        private const string BoardGraphNodeBindingSourceContext = "BoardGraphNodeBinding";

        public static PungentAuthoringBindingPlan BuildRuntimePlan(PungentBoardDocument document)
        {
            PungentAuthoringBindingPlan plan = new PungentAuthoringBindingPlan
            {
                displayName = document == null ? "BoardGraph Binding Plan" : "BoardGraph Binding Plan: " + document.title,
                sourceInterfaceId = "board-graph",
                sourceItemId = document == null ? string.Empty : document.id,
                sourceLabel = document == null ? string.Empty : document.title
            };

            foreach (PungentBoardNode node in document?.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                PungentAuthoringTarget target = FindProjectBindingTarget(node);
                if (target == null || !target.HasTarget)
                    continue;

                PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromTarget(
                    target,
                    "board-graph",
                    document == null ? string.Empty : document.id,
                    node.id,
                    "node.body",
                    PungentAuthoringBindingLinkDirection.TwoWay,
                    node.projectBindingAdapterId,
                    string.Empty,
                    node.projectBindingEndpointId,
                    target.label,
                    node.projectBindingValueType);

                if (!string.IsNullOrWhiteSpace(node.projectBindingLinkId))
                    link.id = node.projectBindingLinkId;
                if (node.projectBindingPath != null && node.projectBindingPath.HasPath)
                {
                    link.bindingPath = node.projectBindingPath;
                    link.bindingPath.rootTarget = target;
                }
                if (node.projectBindingSlot != null)
                {
                    link.bindingSlot = node.projectBindingSlot;
                    link.bindingSlot.role = PungentAuthoringBindingSlotRole.BoardNode;
                    link.bindingSlot.elementId = node.id;
                    link.bindingSlot.fieldKey = "node.body";
                }
                link.runtimeAdapterId = node.runtimeBindingAdapterId;
                link.runtimeEnabled = node.runtimeBindingEnabled;
                link.notes = "Exported from BoardGraph. Runtime execution requires a project adapter with a matching runtimeAdapterId.";
                link.NormalizeInPlace();
                plan.links.Add(link);
            }

            foreach (PungentBoardGroup group in document?.groups ?? new List<PungentBoardGroup>())
            {
                if (group == null)
                    continue;

                AddGroupBindingSlots(plan, document, group);
                AddGroupPortSlots(plan, document, group);
                AddGroupReferenceSlots(plan, document, group);
            }

            plan.NormalizeInPlace();
            return plan;
        }

        private static void AddGroupBindingSlots(PungentAuthoringBindingPlan plan, PungentBoardDocument document, PungentBoardGroup group)
        {
            foreach (PungentAuthoringBindingSlot slot in group.exposedBindingSlots ?? new List<PungentAuthoringBindingSlot>())
            {
                if (slot == null)
                    continue;

                PungentAuthoringBindingSlot linkSlot = CloneSlot(slot);
                linkSlot.role = PungentAuthoringBindingSlotRole.BoardGroup;
                linkSlot.interfaceId = "board-graph";
                linkSlot.itemId = document != null ? document.id : string.Empty;
                linkSlot.elementId = group.id;
                if (string.IsNullOrWhiteSpace(linkSlot.fieldKey))
                    linkSlot.fieldKey = "group.parameter";
                linkSlot.NormalizeInPlace();

                PungentAuthoringBindingLink link = CreatePlanOnlyLink(
                    document,
                    group.id,
                    linkSlot.fieldKey,
                    string.IsNullOrWhiteSpace(linkSlot.displayName) ? group.title + " Binding" : linkSlot.displayName,
                    linkSlot.valueType,
                    linkSlot,
                    new PungentAuthoringTarget(),
                    null,
                    "BoardGroup");
                plan.links.Add(link);
            }
        }

        private static void AddGroupPortSlots(PungentAuthoringBindingPlan plan, PungentBoardDocument document, PungentBoardGroup group)
        {
            foreach (PungentBoardPortDefinition port in group.exposedPorts ?? new List<PungentBoardPortDefinition>())
            {
                if (port == null)
                    continue;

                PungentBoardCustomNodePortMapping mapping = FindPortMapping(group, port.key);
                PungentAuthoringBindingSlot slot = new PungentAuthoringBindingSlot
                {
                    role = PungentAuthoringBindingSlotRole.BoardPort,
                    interfaceId = "board-graph",
                    itemId = document != null ? document.id : string.Empty,
                    elementId = group.id,
                    fieldKey = "port." + port.key,
                    displayName = (string.IsNullOrWhiteSpace(group.title) ? "Group" : group.title) + " / " + (string.IsNullOrWhiteSpace(port.displayName) ? port.key : port.displayName),
                    valueType = mapping != null ? mapping.valueType : PungentAuthoringBindingValueType.Unknown,
                    runtimePayload = mapping == null ? string.Empty : "internalNode=" + mapping.internalNodeId + ";internalPort=" + mapping.internalPortKey + ";internalField=" + mapping.internalFieldKey
                };
                slot.NormalizeInPlace();

                PungentAuthoringBindingLink link = CreatePlanOnlyLink(
                    document,
                    group.id,
                    slot.fieldKey,
                    slot.displayName,
                    slot.valueType,
                    slot,
                    new PungentAuthoringTarget(),
                    null,
                    "BoardPort");
                plan.links.Add(link);
            }
        }

        private static void AddGroupReferenceSlots(PungentAuthoringBindingPlan plan, PungentBoardDocument document, PungentBoardGroup group)
        {
            foreach (PungentBoardCustomNodeReferenceSlot referenceSlot in group.referenceSlots ?? new List<PungentBoardCustomNodeReferenceSlot>())
            {
                if (referenceSlot == null)
                    continue;

                PungentAuthoringBindingSlot slot = CloneSlot(referenceSlot.bindingSlot);
                slot.role = PungentAuthoringBindingSlotRole.BoardParameter;
                slot.interfaceId = "board-graph";
                slot.itemId = document != null ? document.id : string.Empty;
                slot.elementId = group.id;
                if (string.IsNullOrWhiteSpace(slot.fieldKey))
                    slot.fieldKey = "reference." + referenceSlot.id;
                if (string.IsNullOrWhiteSpace(slot.displayName))
                    slot.displayName = referenceSlot.displayName;
                slot.NormalizeInPlace();

                PungentAuthoringBindingLink link = CreatePlanOnlyLink(
                    document,
                    group.id,
                    slot.fieldKey,
                    string.IsNullOrWhiteSpace(referenceSlot.displayName) ? group.title + " Reference" : referenceSlot.displayName,
                    slot.valueType,
                    slot,
                    CloneTarget(referenceSlot.target),
                    referenceSlot.bindingPath,
                    "BoardParameter");
                link.runtimePayload = referenceSlot.notes ?? string.Empty;
                link.NormalizeInPlace();
                plan.links.Add(link);
            }
        }

        private static PungentAuthoringBindingLink CreatePlanOnlyLink(
            PungentBoardDocument document,
            string sourceElementId,
            string sourceFieldKey,
            string displayName,
            PungentAuthoringBindingValueType valueType,
            PungentAuthoringBindingSlot slot,
            PungentAuthoringTarget target,
            PungentAuthoringBindingPath path,
            string roleLabel)
        {
            PungentAuthoringBindingLink link = new PungentAuthoringBindingLink
            {
                displayName = string.IsNullOrWhiteSpace(displayName) ? "BoardGraph Binding" : displayName,
                sourceInterfaceId = "board-graph",
                sourceItemId = document != null ? document.id : string.Empty,
                sourceElementId = sourceElementId ?? string.Empty,
                sourceFieldKey = sourceFieldKey ?? string.Empty,
                direction = PungentAuthoringBindingLinkDirection.TwoWay,
                valueType = valueType,
                target = target ?? new PungentAuthoringTarget(),
                bindingPath = path ?? new PungentAuthoringBindingPath(),
                bindingSlot = slot ?? new PungentAuthoringBindingSlot(),
                notes = "Exported from BoardGraph " + roleLabel + ". Runtime behavior remains adapter-plan based; no generic reflection executor is implied."
            };
            link.NormalizeInPlace();
            return link;
        }

        private static PungentBoardCustomNodePortMapping FindPortMapping(PungentBoardGroup group, string portKey)
        {
            foreach (PungentBoardCustomNodePortMapping mapping in group?.exposedPortMappings ?? new List<PungentBoardCustomNodePortMapping>())
            {
                if (mapping != null && string.Equals(mapping.exposedPortKey, portKey, StringComparison.OrdinalIgnoreCase))
                    return mapping;
            }

            return null;
        }

        private static PungentAuthoringBindingSlot CloneSlot(PungentAuthoringBindingSlot source)
        {
            if (source == null)
                return new PungentAuthoringBindingSlot();

            PungentAuthoringBindingSlot slot = new PungentAuthoringBindingSlot
            {
                id = source.id,
                role = source.role,
                interfaceId = source.interfaceId,
                itemId = source.itemId,
                elementId = source.elementId,
                fieldKey = source.fieldKey,
                displayName = source.displayName,
                valueType = source.valueType,
                pathId = source.pathId,
                bindingLinkId = source.bindingLinkId,
                enabled = source.enabled,
                pullEnabled = source.pullEnabled,
                pushEnabled = source.pushEnabled,
                runtimePayload = source.runtimePayload,
                notes = source.notes
            };
            slot.NormalizeInPlace();
            return slot;
        }

        private static PungentAuthoringTarget CloneTarget(PungentAuthoringTarget source)
        {
            if (source == null)
                return new PungentAuthoringTarget();

            PungentAuthoringTarget target = new PungentAuthoringTarget
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
            target.NormalizeInPlace();
            return target;
        }

        private static PungentAuthoringTarget FindProjectBindingTarget(PungentBoardNode node)
        {
            if (node == null || node.targets == null)
                return null;

            return node.targets.Find(target =>
                target != null &&
                string.Equals(target.sourceContext, BoardGraphNodeBindingSourceContext, StringComparison.OrdinalIgnoreCase));
        }
    }
#endif
}
