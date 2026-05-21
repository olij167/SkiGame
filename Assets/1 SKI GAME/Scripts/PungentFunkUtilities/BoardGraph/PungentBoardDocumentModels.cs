using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using UnityEngine;

namespace PungentFunk.Utilities.BoardGraph
{
    public enum PungentBoardNodeKind
    {
        NoteCard = 0,
        DocumentReference = 10,
        UtilityReference = 20,
        TokenReference = 30,
        DocumentationLink = 40,
        AuditFinding = 50,
        Task = 60,
        FreeformCard = 70
    }

    public enum PungentBoardEdgeKind
    {
        Related = 0,
        Relationship = 0,
        DependsOn = 10,
        Blocks = 20,
        LeadsTo = 30,
        References = 40,
        ParentChild = 50,
        Contains = 50,
        Sequence = 60,
        Custom = 1000
    }

    [Serializable]
    public sealed class PungentBoardCanvasState
    {
        public Vector2 pan = Vector2.zero;
        public float zoom = 1f;
        public string selectedNodeId = string.Empty;
        public string selectedEdgeId = string.Empty;
        public string selectedGroupId = string.Empty;
        public bool showGrid = true;
        public bool snapToGrid;
        public float snapSize = 24f;
        public float gridMinorSpacing = 24f;
        public int gridMajorLineFrequency = 5;
        public float gridOpacity = 1f;
        public bool snapNodes = true;
        public bool snapGroups = true;
        public bool snapResize = true;

        public void NormalizeInPlace()
        {
            zoom = Mathf.Clamp(float.IsNaN(zoom) || float.IsInfinity(zoom) ? 1f : zoom, 0.10f, 3f);
            snapSize = Mathf.Clamp(float.IsNaN(snapSize) || float.IsInfinity(snapSize) ? 24f : snapSize, 4f, 240f);
            gridMinorSpacing = Mathf.Clamp(float.IsNaN(gridMinorSpacing) || float.IsInfinity(gridMinorSpacing) ? 24f : gridMinorSpacing, 8f, 240f);
            gridMajorLineFrequency = Mathf.Clamp(gridMajorLineFrequency <= 0 ? 5 : gridMajorLineFrequency, 2, 12);
            gridOpacity = Mathf.Clamp01(float.IsNaN(gridOpacity) || float.IsInfinity(gridOpacity) ? 1f : gridOpacity);
            selectedNodeId = PungentAuthoringId.Normalize(selectedNodeId);
            selectedEdgeId = PungentAuthoringId.Normalize(selectedEdgeId);
            selectedGroupId = PungentAuthoringId.Normalize(selectedGroupId);
        }

        public void ClearSelection()
        {
            selectedNodeId = string.Empty;
            selectedEdgeId = string.Empty;
            selectedGroupId = string.Empty;
        }
    }

    [Serializable]
    public sealed class PungentBoardDocument
    {
        public const int CurrentMigrationVersion = 6;

        public string id = string.Empty;
        public string title = "Untitled Board";
        public string summary = string.Empty;
        public List<string> tags = new List<string>();
        public string status = "Draft";
        public string priority = string.Empty;
        public string visibility = "Project";
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public bool archived;
        public bool developerOnly;
        public bool locked;
        public string generatedBy = string.Empty;
        public string generatedTemplateId = string.Empty;
        public string generatedUtc = string.Empty;
        public int migrationVersion = CurrentMigrationVersion;
        public string templateKey = string.Empty;
        public string templateDisplayName = string.Empty;
        public string integrationProfileId = string.Empty;
        public string graphTypeId = PungentBoardBuiltInGraphTypes.FreeformWhiteboard;
        public string graphTypeDisplayName = "Freeform Whiteboard";
        public int graphSchemaVersion = 1;
        public List<PungentBoardGraphPropertyValue> properties = new List<PungentBoardGraphPropertyValue>();
        public List<PungentBoardNode> nodes = new List<PungentBoardNode>();
        public List<PungentBoardEdge> edges = new List<PungentBoardEdge>();
        public List<PungentBoardGroup> groups = new List<PungentBoardGroup>();
        public List<PungentAuthoringTarget> targets = new List<PungentAuthoringTarget>();
        public List<PungentAuthoringReference> references = new List<PungentAuthoringReference>();
        public PungentBoardCanvasState canvasState = new PungentBoardCanvasState();

        public void NormalizeInPlace()
        {
            string now = DateTime.UtcNow.ToString("o");
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : PungentAuthoringId.NewValue();
            title = string.IsNullOrWhiteSpace(title) ? "Untitled Board" : title.Trim();
            summary = summary == null ? string.Empty : summary.Trim();
            status = status == null ? string.Empty : status.Trim();
            priority = priority == null ? string.Empty : priority.Trim();
            visibility = string.IsNullOrWhiteSpace(visibility) ? "Project" : visibility.Trim();
            generatedBy = generatedBy == null ? string.Empty : generatedBy.Trim();
            generatedTemplateId = PungentAuthoringId.Normalize(generatedTemplateId);
            generatedUtc = string.IsNullOrWhiteSpace(generatedUtc) ? string.Empty : generatedUtc.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? now : createdUtc.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? createdUtc : updatedUtc.Trim();
            migrationVersion = Mathf.Max(CurrentMigrationVersion, migrationVersion);
            templateKey = templateKey == null ? string.Empty : templateKey.Trim();
            templateDisplayName = templateDisplayName == null ? string.Empty : templateDisplayName.Trim();
            integrationProfileId = integrationProfileId == null ? string.Empty : integrationProfileId.Trim();
            graphTypeId = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(graphTypeId);
            graphTypeDisplayName = string.IsNullOrWhiteSpace(graphTypeDisplayName) ? "Freeform Whiteboard" : graphTypeDisplayName.Trim();
            graphSchemaVersion = Mathf.Max(1, graphSchemaVersion);
            tags = PungentAuthoringMetadata.NormalizeTags(tags);
            properties = properties ?? new List<PungentBoardGraphPropertyValue>();
            nodes = nodes ?? new List<PungentBoardNode>();
            edges = edges ?? new List<PungentBoardEdge>();
            groups = groups ?? new List<PungentBoardGroup>();
            targets = targets ?? new List<PungentAuthoringTarget>();
            references = references ?? new List<PungentAuthoringReference>();
            canvasState = canvasState ?? new PungentBoardCanvasState();

            for (int i = properties.Count - 1; i >= 0; i--)
            {
                if (properties[i] == null)
                {
                    properties.RemoveAt(i);
                    continue;
                }

                properties[i].NormalizeInPlace();
                if (string.IsNullOrWhiteSpace(properties[i].key))
                    properties.RemoveAt(i);
            }

            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null)
                    nodes[i].NormalizeInPlace();

            for (int i = 0; i < edges.Count; i++)
                if (edges[i] != null)
                    edges[i].NormalizeInPlace();

            for (int i = 0; i < groups.Count; i++)
                if (groups[i] != null)
                    groups[i].NormalizeInPlace();

            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null)
                    targets[i].NormalizeInPlace();

            for (int i = 0; i < references.Count; i++)
                if (references[i] != null)
                    references[i].NormalizeInPlace();

            canvasState.NormalizeInPlace();
        }

        public PungentAuthoringReference ToReference(string providerId)
        {
            return PungentAuthoringReference.Create(PungentAuthoringItemKind.Board, id, providerId, title);
        }

        public static PungentBoardDocument Create(string title)
        {
            string now = DateTime.UtcNow.ToString("o");
            PungentBoardDocument document = new PungentBoardDocument
            {
                id = PungentAuthoringId.NewValue(),
                title = string.IsNullOrWhiteSpace(title) ? "Untitled Board" : title.Trim(),
                createdUtc = now,
                updatedUtc = now,
                migrationVersion = CurrentMigrationVersion
            };
            document.NormalizeInPlace();
            return document;
        }
    }

    [Serializable]
    public sealed class PungentBoardNode
    {
        public string id = string.Empty;
        public string title = "New Card";
        public string body = string.Empty;
        public string summary = string.Empty;
        public PungentBoardNodeKind nodeKind = PungentBoardNodeKind.NoteCard;
        public string nodeTypeKey = string.Empty;
        public Vector2 position = Vector2.zero;
        public Vector2 size = new Vector2(220f, 120f);
        public string colorStyleKey = "note";
        public bool collapsed;
        public string integrationSourceAdapterId = string.Empty;
        public string integrationSourceKey = string.Empty;
        public string integrationSourceLabel = string.Empty;
        public string syncState = string.Empty;
        public string projectBindingLinkId = string.Empty;
        public string projectBindingAdapterId = string.Empty;
        public string projectBindingEndpointId = string.Empty;
        public PungentAuthoringBindingValueType projectBindingValueType = PungentAuthoringBindingValueType.Unknown;
        public PungentAuthoringBindingPath projectBindingPath = new PungentAuthoringBindingPath();
        public PungentAuthoringBindingSlot projectBindingSlot = new PungentAuthoringBindingSlot();
        public PungentAuthoringBindingApplyMode applyMode = PungentAuthoringBindingApplyMode.ManualApply;
        public string runtimeBindingAdapterId = string.Empty;
        public bool runtimeBindingEnabled;
        public PungentAuthoringReference linkedAuthoringRef = new PungentAuthoringReference();
        public List<PungentBoardGraphPropertyValue> properties = new List<PungentBoardGraphPropertyValue>();
        public List<PungentAuthoringTarget> targets = new List<PungentAuthoringTarget>();
        public List<PungentAuthoringReference> references = new List<PungentAuthoringReference>();

        public Rect Rect => new Rect(position, size);

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : PungentAuthoringId.NewValue();
            title = string.IsNullOrWhiteSpace(title) ? "New Card" : title.Trim();
            body = body == null ? string.Empty : body;
            summary = summary == null ? string.Empty : summary.Trim();
            nodeTypeKey = nodeTypeKey == null ? string.Empty : nodeTypeKey.Trim();
            size.x = Mathf.Clamp(float.IsNaN(size.x) || float.IsInfinity(size.x) ? 220f : size.x, 120f, 720f);
            size.y = Mathf.Clamp(float.IsNaN(size.y) || float.IsInfinity(size.y) ? 120f : size.y, 64f, 520f);
            colorStyleKey = string.IsNullOrWhiteSpace(colorStyleKey) ? "note" : colorStyleKey.Trim();
            integrationSourceAdapterId = integrationSourceAdapterId == null ? string.Empty : integrationSourceAdapterId.Trim();
            integrationSourceKey = integrationSourceKey == null ? string.Empty : integrationSourceKey.Trim();
            integrationSourceLabel = integrationSourceLabel == null ? string.Empty : integrationSourceLabel.Trim();
            syncState = syncState == null ? string.Empty : syncState.Trim();
            projectBindingLinkId = PungentAuthoringId.Normalize(projectBindingLinkId);
            projectBindingAdapterId = projectBindingAdapterId == null ? string.Empty : projectBindingAdapterId.Trim();
            projectBindingEndpointId = projectBindingEndpointId == null ? string.Empty : projectBindingEndpointId.Trim();
            projectBindingPath = projectBindingPath ?? new PungentAuthoringBindingPath();
            projectBindingPath.NormalizeInPlace();
            projectBindingSlot = projectBindingSlot ?? new PungentAuthoringBindingSlot();
            if (projectBindingSlot.role == PungentAuthoringBindingSlotRole.Unknown)
                projectBindingSlot.role = PungentAuthoringBindingSlotRole.BoardNode;
            if (string.IsNullOrWhiteSpace(projectBindingSlot.elementId))
                projectBindingSlot.elementId = id;
            if (string.IsNullOrWhiteSpace(projectBindingSlot.fieldKey))
                projectBindingSlot.fieldKey = "node.body";
            if (projectBindingSlot.valueType == PungentAuthoringBindingValueType.Unknown)
                projectBindingSlot.valueType = projectBindingValueType;
            if (string.IsNullOrWhiteSpace(projectBindingSlot.pathId))
                projectBindingSlot.pathId = projectBindingPath.id;
            projectBindingSlot.NormalizeInPlace();
            runtimeBindingAdapterId = runtimeBindingAdapterId == null ? string.Empty : runtimeBindingAdapterId.Trim();
            linkedAuthoringRef = linkedAuthoringRef ?? new PungentAuthoringReference();
            linkedAuthoringRef.NormalizeInPlace();
            properties = properties ?? new List<PungentBoardGraphPropertyValue>();
            targets = targets ?? new List<PungentAuthoringTarget>();
            references = references ?? new List<PungentAuthoringReference>();

            for (int i = properties.Count - 1; i >= 0; i--)
            {
                if (properties[i] == null)
                {
                    properties.RemoveAt(i);
                    continue;
                }

                properties[i].NormalizeInPlace();
                if (string.IsNullOrWhiteSpace(properties[i].key))
                    properties.RemoveAt(i);
            }

            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null)
                    targets[i].NormalizeInPlace();

            for (int i = 0; i < references.Count; i++)
                if (references[i] != null)
                    references[i].NormalizeInPlace();
        }

        public static PungentBoardNode Create(PungentBoardNodeKind kind, Vector2 position)
        {
            PungentBoardNode node = new PungentBoardNode
            {
                id = PungentAuthoringId.NewValue(),
                title = GetDefaultTitle(kind),
                nodeKind = kind,
                nodeTypeKey = kind.ToString(),
                position = position,
                colorStyleKey = GetDefaultStyleKey(kind)
            };
            node.NormalizeInPlace();
            return node;
        }

        public static string GetDefaultTitle(PungentBoardNodeKind kind)
        {
            switch (kind)
            {
                case PungentBoardNodeKind.DocumentReference: return "Document Reference";
                case PungentBoardNodeKind.UtilityReference: return "Utility Reference";
                case PungentBoardNodeKind.TokenReference: return "Token Reference";
                case PungentBoardNodeKind.DocumentationLink: return "Documentation Link";
                case PungentBoardNodeKind.AuditFinding: return "Audit Finding";
                case PungentBoardNodeKind.Task: return "Task";
                case PungentBoardNodeKind.FreeformCard: return "Freeform Card";
                default: return "Note Card";
            }
        }

        public static string GetDefaultStyleKey(PungentBoardNodeKind kind)
        {
            switch (kind)
            {
                case PungentBoardNodeKind.DocumentReference: return "document";
                case PungentBoardNodeKind.UtilityReference: return "utility";
                case PungentBoardNodeKind.TokenReference: return "token";
                case PungentBoardNodeKind.DocumentationLink: return "documentation";
                case PungentBoardNodeKind.AuditFinding: return "audit";
                case PungentBoardNodeKind.Task: return "task";
                case PungentBoardNodeKind.FreeformCard: return "freeform";
                default: return "note";
            }
        }
    }

    [Serializable]
    public sealed class PungentBoardEdge
    {
        public string id = string.Empty;
        public string fromNodeId = string.Empty;
        public string toNodeId = string.Empty;
        public string label = string.Empty;
        public PungentBoardEdgeKind edgeKind = PungentBoardEdgeKind.Related;
        public string edgeTypeKey = string.Empty;
        public string styleKey = "default";
        public bool directed = true;
        public string fromPortKey = string.Empty;
        public string toPortKey = string.Empty;
        public int executionOrder;
        public string integrationSourceAdapterId = string.Empty;
        public string integrationSourceKey = string.Empty;
        public string integrationSourceLabel = string.Empty;
        public string syncState = string.Empty;
        public List<PungentBoardGraphPropertyValue> properties = new List<PungentBoardGraphPropertyValue>();

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : PungentAuthoringId.NewValue();
            fromNodeId = PungentAuthoringId.Normalize(fromNodeId);
            toNodeId = PungentAuthoringId.Normalize(toNodeId);
            label = label == null ? string.Empty : label.Trim();
            edgeTypeKey = edgeTypeKey == null ? string.Empty : edgeTypeKey.Trim();
            styleKey = string.IsNullOrWhiteSpace(styleKey) ? "default" : styleKey.Trim();
            fromPortKey = fromPortKey == null ? string.Empty : fromPortKey.Trim();
            toPortKey = toPortKey == null ? string.Empty : toPortKey.Trim();
            integrationSourceAdapterId = integrationSourceAdapterId == null ? string.Empty : integrationSourceAdapterId.Trim();
            integrationSourceKey = integrationSourceKey == null ? string.Empty : integrationSourceKey.Trim();
            integrationSourceLabel = integrationSourceLabel == null ? string.Empty : integrationSourceLabel.Trim();
            syncState = syncState == null ? string.Empty : syncState.Trim();
            properties = properties ?? new List<PungentBoardGraphPropertyValue>();
            for (int i = properties.Count - 1; i >= 0; i--)
            {
                if (properties[i] == null)
                {
                    properties.RemoveAt(i);
                    continue;
                }

                properties[i].NormalizeInPlace();
                if (string.IsNullOrWhiteSpace(properties[i].key))
                    properties.RemoveAt(i);
            }
        }

        public static PungentBoardEdge Create(string fromNodeId, string toNodeId)
        {
            PungentBoardEdge edge = new PungentBoardEdge
            {
                id = PungentAuthoringId.NewValue(),
                fromNodeId = fromNodeId,
                toNodeId = toNodeId
            };
            edge.NormalizeInPlace();
            return edge;
        }
    }

    [Serializable]
    public sealed class PungentBoardGroup
    {
        public string id = string.Empty;
        public string title = "Group";
        public Rect rect = new Rect(-40f, -40f, 360f, 240f);
        public string colorStyleKey = "group";
        public bool collapsed;
        public bool locked;
        public string integrationSourceAdapterId = string.Empty;
        public string integrationSourceKey = string.Empty;
        public string integrationSourceLabel = string.Empty;
        public string syncState = string.Empty;
        public List<string> containedNodeIds = new List<string>();
        public bool customNodeEnabled;
        public string customNodeTemplateId = string.Empty;
        public string customNodeSourceTemplateId = string.Empty;
        public bool collapsedAsNode;
        public List<PungentBoardPortDefinition> exposedPorts = new List<PungentBoardPortDefinition>();
        public List<PungentAuthoringBindingSlot> exposedBindingSlots = new List<PungentAuthoringBindingSlot>();
        public List<PungentBoardCustomNodePortMapping> exposedPortMappings = new List<PungentBoardCustomNodePortMapping>();
        public List<PungentBoardCustomNodeReferenceSlot> referenceSlots = new List<PungentBoardCustomNodeReferenceSlot>();

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : PungentAuthoringId.NewValue();
            title = string.IsNullOrWhiteSpace(title) ? "Group" : title.Trim();
            rect.width = Mathf.Clamp(float.IsNaN(rect.width) || float.IsInfinity(rect.width) ? 360f : rect.width, 160f, 2000f);
            rect.height = Mathf.Clamp(float.IsNaN(rect.height) || float.IsInfinity(rect.height) ? 240f : rect.height, 100f, 2000f);
            colorStyleKey = string.IsNullOrWhiteSpace(colorStyleKey) ? "group" : colorStyleKey.Trim();
            integrationSourceAdapterId = integrationSourceAdapterId == null ? string.Empty : integrationSourceAdapterId.Trim();
            integrationSourceKey = integrationSourceKey == null ? string.Empty : integrationSourceKey.Trim();
            integrationSourceLabel = integrationSourceLabel == null ? string.Empty : integrationSourceLabel.Trim();
            syncState = syncState == null ? string.Empty : syncState.Trim();
            customNodeTemplateId = PungentAuthoringId.Normalize(customNodeTemplateId);
            customNodeSourceTemplateId = PungentAuthoringId.Normalize(customNodeSourceTemplateId);
            containedNodeIds = containedNodeIds ?? new List<string>();
            for (int i = containedNodeIds.Count - 1; i >= 0; i--)
            {
                containedNodeIds[i] = PungentAuthoringId.Normalize(containedNodeIds[i]);
                if (string.IsNullOrWhiteSpace(containedNodeIds[i]))
                    containedNodeIds.RemoveAt(i);
            }

            exposedPorts = exposedPorts ?? new List<PungentBoardPortDefinition>();
            for (int i = exposedPorts.Count - 1; i >= 0; i--)
            {
                if (exposedPorts[i] == null)
                {
                    exposedPorts.RemoveAt(i);
                    continue;
                }

                exposedPorts[i].NormalizeInPlace();
            }

            exposedBindingSlots = exposedBindingSlots ?? new List<PungentAuthoringBindingSlot>();
            for (int i = exposedBindingSlots.Count - 1; i >= 0; i--)
            {
                if (exposedBindingSlots[i] == null)
                {
                    exposedBindingSlots.RemoveAt(i);
                    continue;
                }

                if (exposedBindingSlots[i].role == PungentAuthoringBindingSlotRole.Unknown)
                    exposedBindingSlots[i].role = PungentAuthoringBindingSlotRole.BoardGroup;
                exposedBindingSlots[i].NormalizeInPlace();
            }

            exposedPortMappings = exposedPortMappings ?? new List<PungentBoardCustomNodePortMapping>();
            for (int i = exposedPortMappings.Count - 1; i >= 0; i--)
            {
                if (exposedPortMappings[i] == null)
                {
                    exposedPortMappings.RemoveAt(i);
                    continue;
                }

                exposedPortMappings[i].NormalizeInPlace();
            }

            referenceSlots = referenceSlots ?? new List<PungentBoardCustomNodeReferenceSlot>();
            for (int i = referenceSlots.Count - 1; i >= 0; i--)
            {
                if (referenceSlots[i] == null)
                {
                    referenceSlots.RemoveAt(i);
                    continue;
                }

                referenceSlots[i].NormalizeInPlace();
            }
        }

        public static PungentBoardGroup Create(string title, Rect rect)
        {
            PungentBoardGroup group = new PungentBoardGroup
            {
                id = PungentAuthoringId.NewValue(),
                title = string.IsNullOrWhiteSpace(title) ? "Group" : title.Trim(),
                rect = rect
            };
            group.NormalizeInPlace();
            return group;
        }
    }

    [Serializable]
    public sealed class PungentBoardCustomNodePortMapping
    {
        public string exposedPortKey = string.Empty;
        public string internalNodeId = string.Empty;
        public string internalPortKey = string.Empty;
        public string internalFieldKey = string.Empty;
        public PungentBoardPortDirection direction = PungentBoardPortDirection.Both;
        public PungentAuthoringBindingValueType valueType = PungentAuthoringBindingValueType.Unknown;
        public string notes = string.Empty;

        public void NormalizeInPlace()
        {
            exposedPortKey = exposedPortKey == null ? string.Empty : exposedPortKey.Trim();
            internalNodeId = PungentAuthoringId.Normalize(internalNodeId);
            internalPortKey = internalPortKey == null ? string.Empty : internalPortKey.Trim();
            internalFieldKey = internalFieldKey == null ? string.Empty : internalFieldKey.Trim();
            notes = notes == null ? string.Empty : notes.Trim();
        }
    }

    [Serializable]
    public sealed class PungentBoardCustomNodeReferenceSlot
    {
        public string id = string.Empty;
        public string displayName = "Reference";
        public PungentAuthoringTarget target = new PungentAuthoringTarget();
        public PungentAuthoringBindingPath bindingPath = new PungentAuthoringBindingPath();
        public PungentAuthoringBindingSlot bindingSlot = new PungentAuthoringBindingSlot();
        public bool required;
        public string notes = string.Empty;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Reference" : displayName.Trim();
            target = target ?? new PungentAuthoringTarget();
            target.NormalizeInPlace();
            bindingPath = bindingPath ?? new PungentAuthoringBindingPath();
            bindingPath.NormalizeInPlace();
            bindingSlot = bindingSlot ?? new PungentAuthoringBindingSlot();
            if (bindingSlot.role == PungentAuthoringBindingSlotRole.Unknown)
                bindingSlot.role = PungentAuthoringBindingSlotRole.BoardParameter;
            if (string.IsNullOrWhiteSpace(bindingSlot.pathId))
                bindingSlot.pathId = bindingPath.id;
            bindingSlot.NormalizeInPlace();
            notes = notes == null ? string.Empty : notes.Trim();
        }
    }

    [Serializable]
    public sealed class PungentBoardCustomNodeTemplate
    {
        public const int CurrentMigrationVersion = 2;

        public string id = string.Empty;
        public string displayName = "Custom Node";
        public string description = string.Empty;
        public string graphTypeId = PungentBoardBuiltInGraphTypes.FreeformWhiteboard;
        public string nodeTypeKey = "custom-group-node";
        public string sourceBoardId = string.Empty;
        public string sourceGroupId = string.Empty;
        public string sourceGroupTitle = string.Empty;
        public string styleKey = "group";
        public bool collapsedAsNode = true;
        public Rect sourceBounds = new Rect(-40f, -40f, 360f, 240f);
        public List<string> containedNodeIds = new List<string>();
        public List<PungentBoardNode> nodes = new List<PungentBoardNode>();
        public List<PungentBoardEdge> edges = new List<PungentBoardEdge>();
        public List<PungentBoardPortDefinition> ports = new List<PungentBoardPortDefinition>();
        public List<PungentAuthoringBindingSlot> exposedBindingSlots = new List<PungentAuthoringBindingSlot>();
        public List<PungentBoardCustomNodePortMapping> portMappings = new List<PungentBoardCustomNodePortMapping>();
        public List<PungentBoardCustomNodeReferenceSlot> referenceSlots = new List<PungentBoardCustomNodeReferenceSlot>();
        public int migrationVersion = CurrentMigrationVersion;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Custom Node" : displayName.Trim();
            description = description == null ? string.Empty : description.Trim();
            graphTypeId = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(graphTypeId);
            nodeTypeKey = string.IsNullOrWhiteSpace(nodeTypeKey) ? "custom-group-node" : nodeTypeKey.Trim();
            sourceBoardId = PungentAuthoringId.Normalize(sourceBoardId);
            sourceGroupId = PungentAuthoringId.Normalize(sourceGroupId);
            sourceGroupTitle = sourceGroupTitle == null ? string.Empty : sourceGroupTitle.Trim();
            styleKey = string.IsNullOrWhiteSpace(styleKey) ? "group" : styleKey.Trim();
            sourceBounds.width = Mathf.Clamp(float.IsNaN(sourceBounds.width) || float.IsInfinity(sourceBounds.width) ? 360f : sourceBounds.width, 160f, 2000f);
            sourceBounds.height = Mathf.Clamp(float.IsNaN(sourceBounds.height) || float.IsInfinity(sourceBounds.height) ? 240f : sourceBounds.height, 100f, 2000f);
            migrationVersion = Mathf.Max(CurrentMigrationVersion, migrationVersion);

            containedNodeIds = containedNodeIds ?? new List<string>();
            for (int i = containedNodeIds.Count - 1; i >= 0; i--)
            {
                containedNodeIds[i] = PungentAuthoringId.Normalize(containedNodeIds[i]);
                if (string.IsNullOrWhiteSpace(containedNodeIds[i]))
                    containedNodeIds.RemoveAt(i);
            }

            nodes = nodes ?? new List<PungentBoardNode>();
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i] == null)
                {
                    nodes.RemoveAt(i);
                    continue;
                }

                nodes[i].NormalizeInPlace();
            }

            edges = edges ?? new List<PungentBoardEdge>();
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                if (edges[i] == null)
                {
                    edges.RemoveAt(i);
                    continue;
                }

                edges[i].NormalizeInPlace();
            }

            ports = ports ?? new List<PungentBoardPortDefinition>();
            for (int i = ports.Count - 1; i >= 0; i--)
            {
                if (ports[i] == null)
                {
                    ports.RemoveAt(i);
                    continue;
                }

                ports[i].NormalizeInPlace();
            }

            exposedBindingSlots = exposedBindingSlots ?? new List<PungentAuthoringBindingSlot>();
            for (int i = exposedBindingSlots.Count - 1; i >= 0; i--)
            {
                if (exposedBindingSlots[i] == null)
                {
                    exposedBindingSlots.RemoveAt(i);
                    continue;
                }

                if (exposedBindingSlots[i].role == PungentAuthoringBindingSlotRole.Unknown)
                    exposedBindingSlots[i].role = PungentAuthoringBindingSlotRole.BoardGroup;
                exposedBindingSlots[i].NormalizeInPlace();
            }

            portMappings = portMappings ?? new List<PungentBoardCustomNodePortMapping>();
            for (int i = portMappings.Count - 1; i >= 0; i--)
            {
                if (portMappings[i] == null)
                {
                    portMappings.RemoveAt(i);
                    continue;
                }

                portMappings[i].NormalizeInPlace();
            }

            referenceSlots = referenceSlots ?? new List<PungentBoardCustomNodeReferenceSlot>();
            for (int i = referenceSlots.Count - 1; i >= 0; i--)
            {
                if (referenceSlots[i] == null)
                {
                    referenceSlots.RemoveAt(i);
                    continue;
                }

                referenceSlots[i].NormalizeInPlace();
            }
        }

        public static PungentBoardCustomNodeTemplate FromGroup(PungentBoardDocument document, PungentBoardGroup group)
        {
            PungentBoardCustomNodeTemplate template = new PungentBoardCustomNodeTemplate
            {
                id = group == null || string.IsNullOrWhiteSpace(group.customNodeTemplateId) ? PungentAuthoringId.NewValue() : group.customNodeTemplateId,
                displayName = group == null ? "Custom Node" : group.title,
                graphTypeId = document == null ? PungentBoardBuiltInGraphTypes.FreeformWhiteboard : document.graphTypeId,
                sourceBoardId = document == null ? string.Empty : document.id,
                sourceGroupId = group == null ? string.Empty : group.id,
                sourceGroupTitle = group == null ? string.Empty : group.title,
                styleKey = group == null ? "group" : group.colorStyleKey,
                collapsedAsNode = group == null || group.collapsedAsNode || group.collapsed,
                sourceBounds = group == null ? new Rect(-40f, -40f, 360f, 240f) : group.rect
            };

            if (group != null)
            {
                template.ports.AddRange(CloneList(group.exposedPorts));
                template.exposedBindingSlots.AddRange(CloneList(group.exposedBindingSlots));
                template.portMappings.AddRange(CloneList(group.exposedPortMappings));
                template.referenceSlots.AddRange(CloneList(group.referenceSlots));

                HashSet<string> contained = new HashSet<string>(group.containedNodeIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                if (document != null && contained.Count == 0)
                {
                    foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
                        if (node != null && group.rect.Overlaps(node.Rect))
                            contained.Add(node.id);
                }

                template.containedNodeIds.AddRange(contained);
                if (document != null)
                {
                    foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
                    {
                        if (node == null || !contained.Contains(node.id))
                            continue;

                        template.nodes.Add(JsonUtility.FromJson<PungentBoardNode>(JsonUtility.ToJson(node)));
                    }

                    foreach (PungentBoardEdge edge in document.edges ?? new List<PungentBoardEdge>())
                    {
                        if (edge == null || !contained.Contains(edge.fromNodeId) || !contained.Contains(edge.toNodeId))
                            continue;

                        template.edges.Add(JsonUtility.FromJson<PungentBoardEdge>(JsonUtility.ToJson(edge)));
                    }
                }
            }

            template.NormalizeInPlace();
            return template;
        }

        private static List<T> CloneList<T>(IEnumerable<T> values)
        {
            List<T> clone = new List<T>();
            foreach (T value in values ?? new T[0])
            {
                if (value == null)
                    continue;

                clone.Add(JsonUtility.FromJson<T>(JsonUtility.ToJson(value)));
            }

            return clone;
        }
    }

    [Serializable]
    public sealed class PungentBoardCustomNodeTemplateDatabase
    {
        public const int CurrentMigrationVersion = 1;

        public int migrationVersion = CurrentMigrationVersion;
        public string lastSavedUtc = string.Empty;
        public List<PungentBoardCustomNodeTemplate> templates = new List<PungentBoardCustomNodeTemplate>();

        public void NormalizeInPlace()
        {
            migrationVersion = Mathf.Max(CurrentMigrationVersion, migrationVersion);
            lastSavedUtc = lastSavedUtc == null ? string.Empty : lastSavedUtc.Trim();
            templates = templates ?? new List<PungentBoardCustomNodeTemplate>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = templates.Count - 1; i >= 0; i--)
            {
                if (templates[i] == null)
                {
                    templates.RemoveAt(i);
                    continue;
                }

                templates[i].NormalizeInPlace();
                if (!seen.Add(templates[i].id))
                    templates.RemoveAt(i);
            }
        }

        public PungentBoardCustomNodeTemplate Find(string templateId)
        {
            NormalizeInPlace();
            string clean = PungentAuthoringId.Normalize(templateId);
            return string.IsNullOrWhiteSpace(clean)
                ? null
                : templates.Find(template => template != null && PungentAuthoringId.EqualsId(template.id, clean));
        }

        public void AddOrUpdate(PungentBoardCustomNodeTemplate template)
        {
            if (template == null)
                return;

            NormalizeInPlace();
            template.NormalizeInPlace();
            int index = templates.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, template.id));
            if (index >= 0)
                templates[index] = template;
            else
                templates.Add(template);
            lastSavedUtc = DateTime.UtcNow.ToString("o");
        }
    }

    [CreateAssetMenu(fileName = "BoardCustomNodeTemplates", menuName = "PungentFunk Utilities/BoardGraph/Custom Node Templates")]
    public sealed class PungentBoardCustomNodeTemplateAsset : ScriptableObject
    {
        public PungentBoardCustomNodeTemplateDatabase database = new PungentBoardCustomNodeTemplateDatabase();

        private void OnValidate()
        {
            database = database ?? new PungentBoardCustomNodeTemplateDatabase();
            database.NormalizeInPlace();
        }
    }
}
