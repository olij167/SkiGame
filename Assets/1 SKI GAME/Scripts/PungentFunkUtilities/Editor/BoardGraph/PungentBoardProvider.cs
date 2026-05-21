using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public static class PungentBoardEditorStorage
    {
        private static PungentBoardDocumentDatabase _database;
        private static string _loadError = string.Empty;

        public static string StoragePath => PungentBoardDocumentStorage.GetDefaultProjectSettingsPath(Directory.GetParent(Application.dataPath).FullName);
        public static string LoadError => _loadError;

        public static PungentBoardDocumentDatabase Database
        {
            get
            {
                EnsureLoaded();
                return _database;
            }
        }

        public static void EnsureLoaded()
        {
            if (_database != null)
                return;

            Reload();
        }

        public static void Reload()
        {
            if (!PungentBoardDocumentStorage.TryLoad(StoragePath, out _database, out _loadError))
                Debug.LogWarning("PungentFunk Board storage could not be loaded. A safe empty database is being used until the next explicit save. " + _loadError);
        }

        public static PungentBoardDocument FindBoard(string boardId)
        {
            return Database.FindDocument(boardId);
        }

        public static PungentBoardDocument CreateBoard(PungentBoardTemplateKind template)
        {
            PungentBoardDocument document = PungentBoardTemplates.Create(template);
            Database.UpsertDocument(document);
            return document;
        }

        public static bool DeleteBoard(string boardId)
        {
            return Database.DeleteDocument(boardId);
        }

        public static bool Save(out string error)
        {
            return PungentBoardDocumentStorage.Save(StoragePath, Database, out error);
        }
    }

    public sealed class PungentBoardProvider :
        IPungentAuthoringProvider,
        IPungentAuthoringPreviewProvider,
        IPungentAuthoringEditorLauncher,
        IPungentAuthoringCopyProvider,
        IPungentAuthoringValidator
    {
        public const string Id = "board-documents";
        public const string UtilityId = "board-editor";
        public const string PackageId = "com.pungentfunk.utilities.board";
        public const string PackageDisplayName = "PungentFunk Utilities Board / Graph / Visualization";

        private static readonly PungentAuthoringItemKind[] Kinds = { PungentAuthoringItemKind.Board };

        public string ProviderId => Id;
        public string DisplayName => "Board Documents";
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
        public string PackageCapabilityId => PungentAuthoringPackageCapabilities.BoardWhiteboard;
        public string ExtensionId => PungentAuthoringPackageCapabilities.BoardWhiteboard;

        public IEnumerable<PungentAuthoringMetadata> EnumerateItems()
        {
            foreach (PungentBoardDocument document in PungentBoardEditorStorage.Database.documents ?? new List<PungentBoardDocument>())
                if (document != null)
                    yield return ToMetadata(document);
        }

        public bool TryGetMetadata(PungentAuthoringReference reference, out PungentAuthoringMetadata metadata)
        {
            metadata = null;
            PungentBoardDocument document = Find(reference);
            if (document == null)
                return false;

            metadata = ToMetadata(document);
            return true;
        }

        public IEnumerable<PungentAuthoringReference> GetReferences(PungentAuthoringReference reference)
        {
            PungentBoardDocument document = Find(reference);
            if (document == null)
                yield break;

            foreach (PungentAuthoringReference boardReference in document.references ?? new List<PungentAuthoringReference>())
                if (boardReference != null && boardReference.HasItemId)
                    yield return boardReference;

            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                if (node.linkedAuthoringRef != null && node.linkedAuthoringRef.HasItemId)
                    yield return node.linkedAuthoringRef;

                foreach (PungentAuthoringReference nodeReference in node.references ?? new List<PungentAuthoringReference>())
                    if (nodeReference != null && nodeReference.HasItemId)
                        yield return nodeReference;
            }
        }

        public IEnumerable<PungentAuthoringTarget> GetTargets(PungentAuthoringReference reference)
        {
            PungentBoardDocument document = Find(reference);
            if (document == null)
                yield break;

            foreach (PungentAuthoringTarget target in document.targets ?? new List<PungentAuthoringTarget>())
                if (target != null && target.HasTarget)
                    yield return target;

            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                foreach (PungentAuthoringTarget target in node.targets ?? new List<PungentAuthoringTarget>())
                    if (target != null && target.HasTarget)
                        yield return target;
            }
        }

        public bool TryGetPreview(PungentAuthoringReference reference, out PungentAuthoringPreview preview)
        {
            preview = null;
            PungentBoardDocument document = Find(reference);
            if (document == null)
                return false;

            preview = PungentAuthoringPreview.FromMetadata(ToMetadata(document), GetTargets(reference).Count());
            preview.kindLabel = "Board Document";
            preview.subtitle = (document.nodes?.Count ?? 0) + " node(s), " + (document.edges?.Count ?? 0) + " edge(s), " + (document.groups?.Count ?? 0) + " group(s)";
            preview.bodyPreview = string.IsNullOrWhiteSpace(document.summary) ? "Spatial whiteboard / node graph document." : document.summary;
            preview.primaryActionLabels.Add("Open Board");
            preview.primaryActionLabels.Add("Copy Board ID");
            return true;
        }

        public bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = Find(reference) == null ? "Board document could not be found." : string.Empty;
            return string.IsNullOrEmpty(reason);
        }

        public bool Open(PungentAuthoringReference reference)
        {
            PungentBoardDocument document = Find(reference);
            if (document == null)
                return false;

            PungentBoardEditorWindow.OpenAndSelectBoard(document.id);
            return true;
        }

        public bool CanEdit(PungentAuthoringReference reference, out string reason)
        {
            return CanOpen(reference, out reason);
        }

        public bool Edit(PungentAuthoringReference reference)
        {
            return Open(reference);
        }

        public bool CanCreateFromContext(PungentAuthoringTarget context, out string reason)
        {
            reason = "Create a board from the Board Editor, then link nodes to authoring items.";
            return false;
        }

        public bool TryCreateFromContext(PungentAuthoringTarget context, out PungentAuthoringReference createdReference, out string error)
        {
            createdReference = null;
            error = "Create a board from the Board Editor, then link nodes to authoring items.";
            return false;
        }

        public bool TryCopy(PungentAuthoringReference reference, out string copiedValue, out string error)
        {
            copiedValue = reference?.itemId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(copiedValue))
            {
                error = "No board ID to copy.";
                return false;
            }

            EditorGUIUtility.systemCopyBuffer = copiedValue;
            error = string.Empty;
            return true;
        }

        public PungentAuthoringValidationResult ValidateReference(PungentAuthoringReference reference)
        {
            PungentBoardDocument document = Find(reference);
            if (document == null)
            {
                PungentAuthoringValidationResult missing = new PungentAuthoringValidationResult
                {
                    providerId = Id,
                    itemId = reference != null ? reference.itemId ?? string.Empty : string.Empty
                };
                missing.AddIssue(PungentAuthoringValidationIssue.Create(
                    PungentAuthoringValidationSeverity.Error,
                    "Board document could not be found.",
                    Id,
                    missing.itemId,
                    null,
                    "Refresh board storage",
                    missing.itemId,
                    "MISSING_TARGET"));
                return missing;
            }

            return PungentBoardLocalValidation.Validate(document);
        }

        public PungentAuthoringValidationResult ValidateTarget(PungentAuthoringTarget target)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = Id,
                itemId = target != null ? target.rawValue ?? string.Empty : string.Empty
            };

            if (target == null || target.targetKind != PungentAuthoringTargetKind.BoardId)
            {
                result.status = PungentAuthoringValidationStatus.Unsupported;
                return result;
            }

            if (PungentBoardEditorStorage.FindBoard(target.rawValue) == null)
            {
                result.AddIssue(PungentAuthoringValidationIssue.Create(
                    PungentAuthoringValidationSeverity.Error,
                    "Missing board ID '" + target.rawValue + "'.",
                    Id,
                    target.rawValue,
                    target,
                    "Refresh board storage",
                    target.rawValue,
                    "MISSING_TARGET"));
            }
            else
            {
                result.status = PungentAuthoringValidationStatus.Valid;
            }

            return result;
        }

        public static PungentAuthoringMetadata ToMetadata(PungentBoardDocument document)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = document?.id ?? string.Empty,
                title = string.IsNullOrWhiteSpace(document?.title) ? "Untitled Board" : document.title,
                summary = document?.summary ?? string.Empty,
                kind = PungentAuthoringItemKind.Board,
                customKind = "BoardDocument",
                status = document?.status ?? string.Empty,
                priority = document?.priority ?? string.Empty,
                visibility = document?.visibility ?? string.Empty,
                tags = PungentAuthoringMetadata.NormalizeTags(document?.tags),
                createdUtc = document?.createdUtc ?? string.Empty,
                updatedUtc = document?.updatedUtc ?? string.Empty,
                archived = document != null && document.archived,
                developerOnly = document != null && document.developerOnly,
                sourceProviderId = Id,
                packageCapabilityId = PungentAuthoringPackageCapabilities.BoardWhiteboard,
                extensionId = PungentAuthoringPackageCapabilities.BoardWhiteboard
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        private static PungentBoardDocument Find(PungentAuthoringReference reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.itemId))
                return null;

            return PungentBoardEditorStorage.FindBoard(reference.itemId);
        }
    }

    public static class PungentBoardLocalValidation
    {
        public static PungentAuthoringValidationResult Validate(PungentBoardDocument document)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = PungentBoardProvider.Id,
                itemId = document != null ? document.id ?? string.Empty : string.Empty
            };

            if (document == null)
            {
                result.AddIssue(PungentAuthoringValidationIssue.Create(
                    PungentAuthoringValidationSeverity.Error,
                    "Board document is missing.",
                    PungentBoardProvider.Id,
                    string.Empty,
                    null,
                    "Refresh board storage",
                    string.Empty,
                    "MISSING_TARGET"));
                return result;
            }

            ValidateDocument(document, result);
            ValidateNodes(document, result);
            ValidateEdges(document, result);
            ValidateGroups(document, result);
            ValidateReferences(document, result);
            ValidateGraphSchema(document, result);
            result.RefreshStatus();
            return result;
        }

        private static void ValidateDocument(PungentBoardDocument document, PungentAuthoringValidationResult result)
        {
            int nodeCount = document.nodes?.Count ?? 0;
            int edgeCount = document.edges?.Count ?? 0;
            int groupCount = document.groups?.Count ?? 0;
            if (nodeCount > 250)
                Add(result, PungentAuthoringValidationSeverity.Warning, "Board has " + nodeCount + " nodes. Simplified rendering is recommended for very large boards.", "LARGE_BOARD_NODE_COUNT", document.id);
            if (edgeCount > 500)
                Add(result, PungentAuthoringValidationSeverity.Warning, "Board has " + edgeCount + " edges. Consider splitting dense maps into related boards.", "LARGE_BOARD_EDGE_COUNT", document.id);
            if (groupCount > 80)
                Add(result, PungentAuthoringValidationSeverity.Warning, "Board has " + groupCount + " groups. Consider simplifying placemats.", "LARGE_BOARD_GROUP_COUNT", document.id);
        }

        private static void ValidateNodes(PungentBoardDocument document, PungentAuthoringValidationResult result)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                if (string.IsNullOrWhiteSpace(node.id))
                {
                    Add(result, PungentAuthoringValidationSeverity.Error, "Node is missing a stable ID.", "MISSING_NODE_ID", node.title);
                    continue;
                }

                if (!ids.Add(node.id))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Duplicate node ID '" + node.id + "'.", "DUPLICATE_NODE_ID", node.id);
            }
        }

        private static void ValidateEdges(PungentBoardDocument document, PungentAuthoringValidationResult result)
        {
            HashSet<string> nodeIds = new HashSet<string>((document.nodes ?? new List<PungentBoardNode>()).Where(node => node != null).Select(node => node.id), StringComparer.OrdinalIgnoreCase);
            HashSet<string> edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardEdge edge in document.edges ?? new List<PungentBoardEdge>())
            {
                if (edge == null)
                    continue;

                if (!edgeIds.Add(edge.id))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Duplicate edge ID '" + edge.id + "'.", "DUPLICATE_EDGE_ID", edge.id);
                if (string.IsNullOrWhiteSpace(edge.fromNodeId) || !nodeIds.Contains(edge.fromNodeId))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Edge '" + Label(edge) + "' references a missing source node.", "MISSING_EDGE_SOURCE", edge.id);
                if (string.IsNullOrWhiteSpace(edge.toNodeId) || !nodeIds.Contains(edge.toNodeId))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Edge '" + Label(edge) + "' references a missing target node.", "MISSING_EDGE_TARGET", edge.id);
            }
        }

        private static void ValidateGroups(PungentBoardDocument document, PungentAuthoringValidationResult result)
        {
            HashSet<string> nodeIds = new HashSet<string>((document.nodes ?? new List<PungentBoardNode>()).Where(node => node != null).Select(node => node.id), StringComparer.OrdinalIgnoreCase);
            HashSet<string> groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId);
            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
            {
                if (group == null)
                    continue;

                if (string.IsNullOrWhiteSpace(group.id))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Group is missing a stable ID.", "MISSING_GROUP_ID", group.title);
                else if (!groupIds.Add(group.id))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Duplicate group ID '" + group.id + "'.", "DUPLICATE_GROUP_ID", group.id);

                foreach (string nodeId in group.containedNodeIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(nodeId) && !nodeIds.Contains(nodeId))
                        Add(result, PungentAuthoringValidationSeverity.Warning, "Group '" + group.title + "' contains missing node ID '" + nodeId + "'.", "MISSING_GROUP_NODE", group.id);
                }

                ValidateCustomGroup(document, result, group, nodeIds, graphType);
            }
        }

        private static void ValidateCustomGroup(PungentBoardDocument document, PungentAuthoringValidationResult result, PungentBoardGroup group, HashSet<string> nodeIds, PungentBoardGraphTypeDefinition graphType)
        {
            if (group == null)
                return;

            bool customGroup = group.customNodeEnabled || group.collapsedAsNode || !string.IsNullOrWhiteSpace(group.customNodeTemplateId) || !string.IsNullOrWhiteSpace(group.customNodeSourceTemplateId);
            if (!customGroup)
                return;

            if ((group.containedNodeIds?.Count ?? 0) == 0)
                Add(result, PungentAuthoringValidationSeverity.Info, "Collapsed/custom group '" + group.title + "' has no contained nodes. It can act as a visual marker but has no template internals yet.", "CUSTOM_GROUP_EMPTY_CONTENT", group.id);
            if (group.collapsedAsNode && (group.exposedPorts?.Count ?? 0) == 0)
                Add(result, PungentAuthoringValidationSeverity.Info, "Collapsed-as-node group '" + group.title + "' has no exposed ports. It remains visible but cannot create group-port edges.", "CUSTOM_GROUP_NO_PORTS", group.id);

            if (!string.IsNullOrWhiteSpace(group.customNodeSourceTemplateId))
            {
                PungentBoardCustomNodeTemplate template = PungentBoardCustomNodeTemplateStorage.Database.Find(group.customNodeSourceTemplateId);
                if (template == null)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' references a missing template source. The cloned group remains editable.", "MISSING_CUSTOM_NODE_TEMPLATE", group.id);
                else if (!TemplateSourceIsAvailable(template))
                    Add(result, PungentAuthoringValidationSeverity.Info, "Custom group '" + group.title + "' uses a stored template snapshot because the source board/group is not available.", "CUSTOM_GROUP_TEMPLATE_SOURCE_MISSING", group.id);
            }

            HashSet<string> exposedPortKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardPortDefinition port in group.exposedPorts ?? new List<PungentBoardPortDefinition>())
            {
                if (port == null)
                    continue;

                if (string.IsNullOrWhiteSpace(port.key))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' has an exposed port with no key.", "CUSTOM_GROUP_PORT_MISSING_KEY", group.id);
                else if (!exposedPortKeys.Add(port.key))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' has duplicate exposed port key '" + port.key + "'.", "CUSTOM_GROUP_DUPLICATE_PORT", group.id);
            }

            foreach (PungentBoardCustomNodePortMapping mapping in group.exposedPortMappings ?? new List<PungentBoardCustomNodePortMapping>())
            {
                if (mapping == null)
                    continue;

                if (string.IsNullOrWhiteSpace(mapping.exposedPortKey) || !exposedPortKeys.Contains(mapping.exposedPortKey))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' has a port mapping with a missing exposed port.", "CUSTOM_GROUP_MAPPING_MISSING_PORT", group.id);
                if (string.IsNullOrWhiteSpace(mapping.internalNodeId) || !nodeIds.Contains(mapping.internalNodeId))
                {
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' maps exposed port '" + mapping.exposedPortKey + "' to a missing internal node.", "CUSTOM_GROUP_MAPPING_MISSING_NODE", group.id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(mapping.internalPortKey) && string.IsNullOrWhiteSpace(mapping.internalFieldKey))
                {
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' maps exposed port '" + mapping.exposedPortKey + "' without an internal port or field.", "CUSTOM_GROUP_MAPPING_MISSING_TARGET", group.id);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mapping.internalPortKey))
                {
                    PungentBoardNode node = PungentBoardCanvasGUI.FindNode(document, mapping.internalNodeId);
                    PungentBoardNodeTypeDefinition nodeType = PungentBoardCanvasGUI.ResolveNodeType(graphType, node);
                    bool validPort = mapping.direction == PungentBoardPortDirection.Both
                        ? HasPort(nodeType, mapping.internalPortKey, true) || HasPort(nodeType, mapping.internalPortKey, false)
                        : HasPort(nodeType, mapping.internalPortKey, mapping.direction == PungentBoardPortDirection.Input);
                    if (!validPort)
                        Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' maps to missing internal port '" + mapping.internalPortKey + "'.", "CUSTOM_GROUP_MAPPING_MISSING_INTERNAL_PORT", group.id);
                }
            }

            foreach (PungentAuthoringBindingSlot slot in group.exposedBindingSlots ?? new List<PungentAuthoringBindingSlot>())
            {
                if (slot == null)
                    continue;

                if (string.IsNullOrWhiteSpace(slot.fieldKey) && string.IsNullOrWhiteSpace(slot.pathId))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' has a binding slot without a field key or binding path.", "CUSTOM_GROUP_BINDING_SLOT_INCOMPLETE", group.id);
            }

            foreach (PungentBoardCustomNodeReferenceSlot slot in group.referenceSlots ?? new List<PungentBoardCustomNodeReferenceSlot>())
            {
                if (slot == null)
                    continue;

                bool hasTarget = slot.target != null && slot.target.HasTarget;
                bool hasPath = slot.bindingPath != null && slot.bindingPath.HasPath;
                if (slot.required && !hasTarget && !hasPath)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Custom group '" + group.title + "' has required reference slot '" + slot.displayName + "' without a target or binding path.", "CUSTOM_GROUP_REFERENCE_SLOT_MISSING_TARGET", group.id);
            }
        }

        private static void ValidateReferences(PungentBoardDocument document, PungentAuthoringValidationResult result)
        {
            foreach (PungentAuthoringReference reference in document.references ?? new List<PungentAuthoringReference>())
                ValidateReference(result, reference, "Board reference");

            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                ValidateReference(result, node.linkedAuthoringRef, "Node '" + node.title + "' linked item");
                foreach (PungentAuthoringReference reference in node.references ?? new List<PungentAuthoringReference>())
                    ValidateReference(result, reference, "Node '" + node.title + "' reference");
            }
        }

        private static bool TemplateSourceIsAvailable(PungentBoardCustomNodeTemplate template)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.sourceBoardId) || string.IsNullOrWhiteSpace(template.sourceGroupId))
                return true;

            PungentBoardDocument sourceBoard = PungentBoardEditorStorage.FindBoard(template.sourceBoardId);
            if (sourceBoard == null)
                return false;

            return (sourceBoard.groups ?? new List<PungentBoardGroup>())
                .Any(group => group != null && PungentAuthoringId.EqualsId(group.id, template.sourceGroupId));
        }

        private static void ValidateGraphSchema(PungentBoardDocument document, PungentAuthoringValidationResult result)
        {
            string graphTypeId = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(document.graphTypeId);
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.Find(graphTypeId);
            if (graphType == null)
            {
                Add(result, PungentAuthoringValidationSeverity.Warning, "Board graph type '" + graphTypeId + "' is not registered. Content is preserved and shown through the Freeform fallback.", "MISSING_GRAPH_TYPE", document.id);
                return;
            }

            graphType.NormalizeInPlace();
            Dictionary<string, PungentBoardNode> nodesById = (document.nodes ?? new List<PungentBoardNode>())
                .Where(node => node != null && !string.IsNullOrWhiteSpace(node.id))
                .GroupBy(node => node.id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, PungentBoardNodeTypeDefinition> nodeTypesById = new Dictionary<string, PungentBoardNodeTypeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                PungentBoardNodeTypeDefinition nodeType = PungentBoardCanvasGUI.ResolveNodeType(graphType, node);
                if (nodeType == null)
                {
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Node '" + node.title + "' uses unknown node type '" + node.nodeTypeKey + "'.", "MISSING_NODE_TYPE", node.id);
                    continue;
                }

                nodeTypesById[node.id] = nodeType;
                if (!string.IsNullOrWhiteSpace(node.nodeTypeKey) &&
                    graphType.FindNodeType(node.nodeTypeKey) == null &&
                    !string.Equals(node.nodeTypeKey, node.nodeKind.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Node '" + node.title + "' preserves custom/missing node type '" + node.nodeTypeKey + "'.", "MISSING_NODE_TYPE", node.id);
                }

                ValidateRequiredPorts(document, result, node, nodeType);
            }

            ValidateRequiredRoot(document, result, graphType, nodeTypesById);
            ValidateTypedEdges(document, result, graphType, nodesById, nodeTypesById);
            if (!graphType.allowCycles)
                ValidateNoCycles(document, result, nodesById);
            ValidateReachableExecutionPath(document, result, graphType, nodeTypesById);
            if (PungentBoardEventSequenceUtility.IsEventSequence(document))
                ValidateEventSequence(document, result, graphType, nodesById, nodeTypesById);
        }

        private static void ValidateEventSequence(PungentBoardDocument document, PungentAuthoringValidationResult result, PungentBoardGraphTypeDefinition graphType, Dictionary<string, PungentBoardNode> nodesById, Dictionary<string, PungentBoardNodeTypeDefinition> nodeTypesById)
        {
            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                PungentBoardNodeTypeDefinition nodeType;
                if (!nodeTypesById.TryGetValue(node.id, out nodeType))
                    nodeType = PungentBoardCanvasGUI.ResolveNodeType(graphType, node);

                string typeKey = PungentBoardEventSequenceUtility.CleanKey(nodeType != null ? nodeType.typeKey : node.nodeTypeKey);
                switch (typeKey)
                {
                    case "delay":
                        if (!PungentBoardEventSequenceUtility.TryGetPositiveFloat(node, "durationSeconds", out _))
                            Add(result, PungentAuthoringValidationSeverity.Warning, "Delay step '" + NodeLabel(node) + "' should have a duration greater than zero.", PungentBoardEventSequenceUtility.IssueInvalidDelay, node.id);
                        break;
                    case "camera-move":
                        if (string.IsNullOrWhiteSpace(PungentBoardEventSequenceUtility.GetNodeProperty(node, "cameraTarget")))
                            Add(result, PungentAuthoringValidationSeverity.Warning, "Camera Move step '" + NodeLabel(node) + "' is missing a camera target or binding label.", PungentBoardEventSequenceUtility.IssueMissingStepValue, node.id);
                        break;
                    case "trigger":
                        if (string.IsNullOrWhiteSpace(PungentBoardEventSequenceUtility.GetNodeProperty(node, "triggerKey")))
                            Add(result, PungentAuthoringValidationSeverity.Warning, "Trigger step '" + NodeLabel(node) + "' is missing a trigger key.", PungentBoardEventSequenceUtility.IssueMissingStepValue, node.id);
                        break;
                    case "wait":
                        if (string.IsNullOrWhiteSpace(PungentBoardEventSequenceUtility.GetNodeProperty(node, "waitFor")))
                            Add(result, PungentAuthoringValidationSeverity.Warning, "Wait step '" + NodeLabel(node) + "' is missing a wait condition/source.", PungentBoardEventSequenceUtility.IssueMissingStepValue, node.id);
                        break;
                    case "branch":
                        if (string.IsNullOrWhiteSpace(PungentBoardEventSequenceUtility.GetNodeProperty(node, "conditionKey")))
                            Add(result, PungentAuthoringValidationSeverity.Warning, "Branch step '" + NodeLabel(node) + "' is missing a condition key.", PungentBoardEventSequenceUtility.IssueMissingStepValue, node.id);
                        break;
                }
            }

            foreach (IGrouping<string, PungentBoardEdge> group in (document.edges ?? new List<PungentBoardEdge>())
                         .Where(edge => edge != null && !string.IsNullOrWhiteSpace(edge.fromNodeId) && nodesById.ContainsKey(edge.fromNodeId) && PungentBoardEventSequenceUtility.IsEventSequenceEdgeType(edge.edgeTypeKey))
                         .GroupBy(edge => edge.fromNodeId, StringComparer.OrdinalIgnoreCase))
            {
                List<IGrouping<int, PungentBoardEdge>> duplicateOrders = group.GroupBy(edge => edge.executionOrder).Where(orderGroup => orderGroup.Count() > 1).ToList();
                if (duplicateOrders.Count > 0)
                {
                    PungentBoardNode sourceNode;
                    nodesById.TryGetValue(group.Key, out sourceNode);
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Sequence step '" + NodeLabel(sourceNode) + "' has duplicate outgoing execution order values.", PungentBoardEventSequenceUtility.IssueDuplicateOrder, group.Key);
                }
            }

            foreach (PungentBoardEdge edge in document.edges ?? new List<PungentBoardEdge>())
            {
                if (edge == null)
                    continue;

                PungentBoardNode fromNode;
                nodesById.TryGetValue(edge.fromNodeId, out fromNode);
                string fromTypeKey = string.Empty;
                PungentBoardNodeTypeDefinition fromType;
                if (fromNode != null && nodeTypesById.TryGetValue(fromNode.id, out fromType) && fromType != null)
                    fromTypeKey = fromType.typeKey;
                else if (fromNode != null)
                    fromTypeKey = fromNode.nodeTypeKey;

                bool branchSource = string.Equals(PungentBoardEventSequenceUtility.CleanKey(fromTypeKey), "branch", StringComparison.OrdinalIgnoreCase);
                bool branchRule = string.Equals(PungentBoardEventSequenceUtility.CleanKey(edge.edgeTypeKey), "branch", StringComparison.OrdinalIgnoreCase);
                if (!branchSource && !branchRule)
                    continue;

                string pathPort = PungentBoardEventSequenceUtility.CleanKey(edge.fromPortKey);
                bool hasPortPath = string.Equals(pathPort, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(pathPort, "false", StringComparison.OrdinalIgnoreCase);
                bool hasLabel = !string.IsNullOrWhiteSpace(edge.label) ||
                                !string.IsNullOrWhiteSpace(PungentBoardEventSequenceUtility.GetEdgeProperty(edge, "labelOverride")) ||
                                !string.IsNullOrWhiteSpace(PungentBoardEventSequenceUtility.GetEdgeProperty(edge, "conditionKey"));
                if (!hasPortPath && !hasLabel)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Branch edge '" + Label(edge) + "' needs a True/False port, condition, or label.", PungentBoardEventSequenceUtility.IssueBranchEdgeLabel, edge.id);
            }
        }

        private static void ValidateRequiredRoot(PungentBoardDocument document, PungentAuthoringValidationResult result, PungentBoardGraphTypeDefinition graphType, Dictionary<string, PungentBoardNodeTypeDefinition> nodeTypesById)
        {
            if (!graphType.requiresRootNode)
                return;

            bool hasRoot = false;
            foreach (KeyValuePair<string, PungentBoardNodeTypeDefinition> pair in nodeTypesById)
            {
                PungentBoardNodeTypeDefinition type = pair.Value;
                if (type == null)
                    continue;

                if ((!string.IsNullOrWhiteSpace(graphType.rootNodeTypeKey) && string.Equals(type.typeKey, graphType.rootNodeTypeKey, StringComparison.OrdinalIgnoreCase)) || type.canBeRoot)
                {
                    hasRoot = true;
                    break;
                }
            }

            if (!hasRoot)
                Add(result, PungentAuthoringValidationSeverity.Error, "Graph type '" + graphType.displayName + "' requires a root node of type '" + graphType.rootNodeTypeKey + "'.", "MISSING_GRAPH_ROOT", document.id);
        }

        private static void ValidateRequiredPorts(PungentBoardDocument document, PungentAuthoringValidationResult result, PungentBoardNode node, PungentBoardNodeTypeDefinition nodeType)
        {
            foreach (PungentBoardPortDefinition port in nodeType.InputPorts())
            {
                if (port == null || !port.required)
                    continue;

                bool connected = (document.edges ?? new List<PungentBoardEdge>()).Any(edge =>
                    edge != null &&
                    PungentAuthoringId.EqualsId(edge.toNodeId, node.id) &&
                    (string.IsNullOrWhiteSpace(edge.toPortKey) || string.Equals(edge.toPortKey, port.key, StringComparison.OrdinalIgnoreCase)));
                if (!connected)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Node '" + node.title + "' is missing required input port '" + port.displayName + "'.", "MISSING_REQUIRED_PORT", node.id);
            }

            foreach (PungentBoardPortDefinition port in nodeType.OutputPorts())
            {
                if (port == null || !port.required)
                    continue;

                bool connected = (document.edges ?? new List<PungentBoardEdge>()).Any(edge =>
                    edge != null &&
                    PungentAuthoringId.EqualsId(edge.fromNodeId, node.id) &&
                    (string.IsNullOrWhiteSpace(edge.fromPortKey) || string.Equals(edge.fromPortKey, port.key, StringComparison.OrdinalIgnoreCase)));
                if (!connected)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Node '" + node.title + "' is missing required output port '" + port.displayName + "'.", "MISSING_REQUIRED_PORT", node.id);
            }
        }

        private static void ValidateTypedEdges(PungentBoardDocument document, PungentAuthoringValidationResult result, PungentBoardGraphTypeDefinition graphType, Dictionary<string, PungentBoardNode> nodesById, Dictionary<string, PungentBoardNodeTypeDefinition> nodeTypesById)
        {
            foreach (PungentBoardEdge edge in document.edges ?? new List<PungentBoardEdge>())
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.fromNodeId) || string.IsNullOrWhiteSpace(edge.toNodeId))
                    continue;

                PungentBoardNode fromNode;
                PungentBoardNode toNode;
                if (!nodesById.TryGetValue(edge.fromNodeId, out fromNode) || !nodesById.TryGetValue(edge.toNodeId, out toNode))
                    continue;

                PungentBoardNodeTypeDefinition fromType;
                PungentBoardNodeTypeDefinition toType;
                if (!nodeTypesById.TryGetValue(fromNode.id, out fromType))
                    fromType = PungentBoardCanvasGUI.ResolveNodeType(graphType, fromNode);
                if (!nodeTypesById.TryGetValue(toNode.id, out toType))
                    toType = PungentBoardCanvasGUI.ResolveNodeType(graphType, toNode);

                string fromKey = fromType != null ? fromType.typeKey : fromNode.nodeTypeKey;
                string toKey = toType != null ? toType.typeKey : toNode.nodeTypeKey;
                PungentBoardEdgeRuleDefinition explicitRule = string.IsNullOrWhiteSpace(edge.edgeTypeKey) ? null : graphType.FindEdgeRule(edge.edgeTypeKey);
                if (!string.IsNullOrWhiteSpace(edge.edgeTypeKey) && explicitRule == null)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Edge '" + Label(edge) + "' preserves missing edge rule '" + edge.edgeTypeKey + "'.", "MISSING_EDGE_RULE", edge.id);

                PungentBoardEdgeRuleDefinition rule = explicitRule ?? graphType.FindAllowedEdgeRule(fromKey, toKey, edge.edgeTypeKey);
                if (rule == null)
                {
                    Add(result, PungentAuthoringValidationSeverity.Error, "Edge '" + Label(edge) + "' is not allowed from " + fromKey + " to " + toKey + " in graph type '" + graphType.displayName + "'.", "ILLEGAL_NODE_CONNECTION", edge.id);
                    continue;
                }

                if (!rule.AllowsConnection(fromKey, toKey))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Edge '" + Label(edge) + "' violates edge rule '" + rule.displayName + "' direction or endpoint type constraints.", "INVALID_EDGE_DIRECTION", edge.id);
                if (edge.directed != rule.directed)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Edge '" + Label(edge) + "' direction does not match edge rule '" + rule.displayName + "'.", "INVALID_EDGE_DIRECTION", edge.id);
                if (!string.IsNullOrWhiteSpace(edge.fromPortKey) && !HasPort(fromType, edge.fromPortKey, false))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Edge '" + Label(edge) + "' uses unknown source output port '" + edge.fromPortKey + "'.", "INVALID_PORT_CONNECTION", edge.id);
                if (!string.IsNullOrWhiteSpace(edge.toPortKey) && !HasPort(toType, edge.toPortKey, true))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Edge '" + Label(edge) + "' uses unknown target input port '" + edge.toPortKey + "'.", "INVALID_PORT_CONNECTION", edge.id);
                if (!string.IsNullOrWhiteSpace(rule.fromPortKey) && !string.IsNullOrWhiteSpace(edge.fromPortKey) && !string.Equals(rule.fromPortKey, edge.fromPortKey, StringComparison.OrdinalIgnoreCase))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Edge '" + Label(edge) + "' uses source port '" + edge.fromPortKey + "' instead of rule port '" + rule.fromPortKey + "'.", "INVALID_PORT_CONNECTION", edge.id);
                if (!string.IsNullOrWhiteSpace(rule.toPortKey) && !string.IsNullOrWhiteSpace(edge.toPortKey) && !string.Equals(rule.toPortKey, edge.toPortKey, StringComparison.OrdinalIgnoreCase))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Edge '" + Label(edge) + "' uses target port '" + edge.toPortKey + "' instead of rule port '" + rule.toPortKey + "'.", "INVALID_PORT_CONNECTION", edge.id);
            }
        }

        private static bool HasPort(PungentBoardNodeTypeDefinition nodeType, string portKey, bool input)
        {
            if (nodeType == null || string.IsNullOrWhiteSpace(portKey))
                return true;

            IEnumerable<PungentBoardPortDefinition> ports = input ? nodeType.InputPorts() : nodeType.OutputPorts();
            return ports.Any(port => port != null && string.Equals(port.key, portKey, StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateNoCycles(PungentBoardDocument document, PungentAuthoringValidationResult result, Dictionary<string, PungentBoardNode> nodesById)
        {
            Dictionary<string, List<string>> adjacency = BuildAdjacency(document, nodesById);
            HashSet<string> visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string nodeId in adjacency.Keys)
            {
                if (HasCycle(nodeId, adjacency, visiting, visited))
                {
                    Add(result, PungentAuthoringValidationSeverity.Error, "Graph contains a directed cycle, but this graph type disallows cycles.", "GRAPH_CYCLE", nodeId);
                    return;
                }
            }
        }

        private static bool HasCycle(string nodeId, Dictionary<string, List<string>> adjacency, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(nodeId))
                return false;
            if (!visiting.Add(nodeId))
                return true;

            List<string> targets;
            if (adjacency.TryGetValue(nodeId, out targets))
            {
                foreach (string target in targets)
                    if (HasCycle(target, adjacency, visiting, visited))
                        return true;
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
            return false;
        }

        private static void ValidateReachableExecutionPath(PungentBoardDocument document, PungentAuthoringValidationResult result, PungentBoardGraphTypeDefinition graphType, Dictionary<string, PungentBoardNodeTypeDefinition> nodeTypesById)
        {
            if (!graphType.requiresRootNode)
                return;

            HashSet<string> rootIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, PungentBoardNodeTypeDefinition> pair in nodeTypesById)
            {
                PungentBoardNodeTypeDefinition type = pair.Value;
                if (type != null && ((!string.IsNullOrWhiteSpace(graphType.rootNodeTypeKey) && string.Equals(type.typeKey, graphType.rootNodeTypeKey, StringComparison.OrdinalIgnoreCase)) || type.canBeRoot))
                    rootIds.Add(pair.Key);
            }

            if (rootIds.Count == 0)
                return;

            Dictionary<string, PungentBoardNode> nodesById = (document.nodes ?? new List<PungentBoardNode>())
                .Where(node => node != null && !string.IsNullOrWhiteSpace(node.id))
                .GroupBy(node => node.id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> adjacency = BuildAdjacency(document, nodesById);
            HashSet<string> reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Queue<string> queue = new Queue<string>(rootIds);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!reached.Add(current))
                    continue;

                List<string> targets;
                if (!adjacency.TryGetValue(current, out targets))
                    continue;
                foreach (string target in targets)
                    if (!reached.Contains(target))
                        queue.Enqueue(target);
            }

            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id) || reached.Contains(node.id))
                    continue;

                PungentBoardNodeTypeDefinition type;
                if (nodeTypesById.TryGetValue(node.id, out type) && type != null && type.isExecutionNode)
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Execution node '" + node.title + "' is not reachable from the required root.", "DISCONNECTED_EXECUTION_PATH", node.id);
            }
        }

        private static Dictionary<string, List<string>> BuildAdjacency(PungentBoardDocument document, Dictionary<string, PungentBoardNode> nodesById)
        {
            Dictionary<string, List<string>> adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string nodeId in nodesById.Keys)
                adjacency[nodeId] = new List<string>();

            foreach (PungentBoardEdge edge in document.edges ?? new List<PungentBoardEdge>())
            {
                if (edge == null || !edge.directed)
                    continue;
                if (!nodesById.ContainsKey(edge.fromNodeId) || !nodesById.ContainsKey(edge.toNodeId))
                    continue;

                List<string> targets;
                if (!adjacency.TryGetValue(edge.fromNodeId, out targets))
                {
                    targets = new List<string>();
                    adjacency[edge.fromNodeId] = targets;
                }
                targets.Add(edge.toNodeId);
            }

            return adjacency;
        }

        private static void ValidateReference(PungentAuthoringValidationResult result, PungentAuthoringReference reference, string label)
        {
            if (reference == null || !reference.HasItemId)
                return;

            if (reference.itemKind == PungentAuthoringItemKind.Unknown)
            {
                Add(result, PungentAuthoringValidationSeverity.Warning, label + " has an item ID but no item kind.", "INVALID_REFERENCE_KIND", reference.itemId);
                return;
            }

            IReadOnlyList<IPungentAuthoringProvider> providers = GetCandidateProviders(reference);
            if (providers.Count == 0)
            {
                Add(result, PungentAuthoringValidationSeverity.Warning, label + ": " + PungentAuthoringProviderRegistry.MissingProviderMessage(reference), "MISSING_PROVIDER", reference.itemId);
                return;
            }

            if (!PungentAuthoringProviderRegistry.TryGetMetadata(reference, out _))
                Add(result, PungentAuthoringValidationSeverity.Warning, label + " could not resolve item '" + reference.itemId + "'.", "MISSING_TARGET", reference.itemId);
        }

        private static IReadOnlyList<IPungentAuthoringProvider> GetCandidateProviders(PungentAuthoringReference reference)
        {
            if (reference == null)
                return new IPungentAuthoringProvider[0];

            if (!string.IsNullOrWhiteSpace(reference.providerId))
            {
                IPungentAuthoringProvider provider = PungentAuthoringProviderRegistry.FindProvider(reference.providerId);
                return provider == null ? new IPungentAuthoringProvider[0] : new[] { provider };
            }

            return PungentAuthoringProviderRegistry.GetProvidersForKind(reference.itemKind);
        }

        private static void Add(PungentAuthoringValidationResult result, PungentAuthoringValidationSeverity severity, string message, string issueCode, string sourceId)
        {
            result.AddIssue(PungentAuthoringValidationIssue.Create(
                severity,
                message,
                PungentBoardProvider.Id,
                result.itemId,
                null,
                "Review board",
                sourceId,
                issueCode));
        }

        private static string Label(PungentBoardEdge edge)
        {
            if (edge == null)
                return "edge";

            return string.IsNullOrWhiteSpace(edge.label) ? edge.id : edge.label;
        }

        private static string NodeLabel(PungentBoardNode node)
        {
            if (node == null)
                return "missing node";

            return string.IsNullOrWhiteSpace(node.title) ? node.id : node.title;
        }
    }

    internal static class PungentBoardEventSequenceUtility
    {
        public const string IssueDuplicateOrder = "EVENT_SEQUENCE_DUPLICATE_ORDER";
        public const string IssueInvalidDelay = "EVENT_SEQUENCE_INVALID_DELAY";
        public const string IssueMissingStepValue = "EVENT_SEQUENCE_MISSING_STEP_VALUE";
        public const string IssueBranchEdgeLabel = "EVENT_SEQUENCE_BRANCH_EDGE_LABEL";

        public static bool IsEventSequence(PungentBoardDocument document)
        {
            return document != null &&
                   string.Equals(PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(document.graphTypeId), PungentBoardBuiltInGraphTypes.EventSequence, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEventSequenceNodeType(string typeKey)
        {
            switch (CleanKey(typeKey))
            {
                case "event":
                case "delay":
                case "camera-move":
                case "trigger":
                case "wait":
                case "branch":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsEventSequenceEdgeType(string typeKey)
        {
            string clean = CleanKey(typeKey);
            return string.Equals(clean, "sequence", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "branch", StringComparison.OrdinalIgnoreCase);
        }

        public static string CleanKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }

        public static string GetNodeProperty(PungentBoardNode node, string key)
        {
            if (node == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            PungentBoardGraphPropertyValue value = (node.properties ?? new List<PungentBoardGraphPropertyValue>())
                .FirstOrDefault(item => item != null && string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));
            return value != null ? value.value ?? string.Empty : string.Empty;
        }

        public static bool SetNodeProperty(PungentBoardNode node, string key, string value)
        {
            if (node == null || string.IsNullOrWhiteSpace(key))
                return false;

            node.properties = node.properties ?? new List<PungentBoardGraphPropertyValue>();
            PungentBoardGraphPropertyValue property = node.properties.FirstOrDefault(item => item != null && string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                property = new PungentBoardGraphPropertyValue { key = key };
                node.properties.Add(property);
            }

            string next = value ?? string.Empty;
            if (string.Equals(property.value, next, StringComparison.Ordinal))
                return false;

            property.value = next;
            property.NormalizeInPlace();
            return true;
        }

        public static string GetEdgeProperty(PungentBoardEdge edge, string key)
        {
            if (edge == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            PungentBoardGraphPropertyValue value = (edge.properties ?? new List<PungentBoardGraphPropertyValue>())
                .FirstOrDefault(item => item != null && string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));
            return value != null ? value.value ?? string.Empty : string.Empty;
        }

        public static bool SetEdgeProperty(PungentBoardEdge edge, string key, string value)
        {
            if (edge == null || string.IsNullOrWhiteSpace(key))
                return false;

            edge.properties = edge.properties ?? new List<PungentBoardGraphPropertyValue>();
            PungentBoardGraphPropertyValue property = edge.properties.FirstOrDefault(item => item != null && string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                property = new PungentBoardGraphPropertyValue { key = key };
                edge.properties.Add(property);
            }

            string next = value ?? string.Empty;
            if (string.Equals(property.value, next, StringComparison.Ordinal))
                return false;

            property.value = next;
            property.NormalizeInPlace();
            return true;
        }

        public static bool TryGetPositiveFloat(PungentBoardNode node, string key, out float value)
        {
            if (float.TryParse(GetNodeProperty(node, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                return value > 0f;

            value = 0f;
            return false;
        }

        public static int NormalizeAllOutgoingOrders(PungentBoardDocument document)
        {
            if (document == null)
                return 0;

            int changed = 0;
            foreach (string fromNodeId in (document.edges ?? new List<PungentBoardEdge>())
                         .Where(edge => edge != null && !string.IsNullOrWhiteSpace(edge.fromNodeId) && IsEventSequenceEdgeType(edge.edgeTypeKey))
                         .Select(edge => edge.fromNodeId)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                changed += NormalizeOutgoingOrders(document, fromNodeId);
            }

            return changed;
        }

        public static int NormalizeOutgoingOrders(PungentBoardDocument document, string fromNodeId)
        {
            if (document == null || string.IsNullOrWhiteSpace(fromNodeId))
                return 0;

            List<PungentBoardEdge> outgoing = OrderedOutgoingEdges(document, fromNodeId).ToList();
            int changed = 0;
            for (int i = 0; i < outgoing.Count; i++)
            {
                if (outgoing[i].executionOrder == i)
                    continue;

                outgoing[i].executionOrder = i;
                changed++;
            }

            return changed;
        }

        public static IEnumerable<PungentBoardEdge> OrderedOutgoingEdges(PungentBoardDocument document, string fromNodeId)
        {
            return (document?.edges ?? new List<PungentBoardEdge>())
                .Where(edge => edge != null && PungentAuthoringId.EqualsId(edge.fromNodeId, fromNodeId) && IsEventSequenceEdgeType(edge.edgeTypeKey))
                .OrderBy(edge => edge.executionOrder)
                .ThenBy(edge => string.IsNullOrWhiteSpace(edge.label) ? edge.id : edge.label, StringComparer.OrdinalIgnoreCase);
        }

        public static bool ApplyDefaultStepValue(PungentBoardNode node)
        {
            if (node == null)
                return false;

            string typeKey = CleanKey(node.nodeTypeKey);
            string fallback = Slug(node.title);
            if (string.IsNullOrWhiteSpace(fallback))
                fallback = string.IsNullOrWhiteSpace(node.id) ? typeKey : node.id;

            switch (typeKey)
            {
                case "delay":
                    return SetNodeProperty(node, "durationSeconds", "1");
                case "camera-move":
                    return SetNodeProperty(node, "cameraTarget", fallback);
                case "trigger":
                    return SetNodeProperty(node, "triggerKey", fallback);
                case "wait":
                    return SetNodeProperty(node, "waitFor", fallback);
                case "branch":
                    return SetNodeProperty(node, "conditionKey", fallback);
                case "event":
                    return SetNodeProperty(node, "eventKey", fallback);
                default:
                    return false;
            }
        }

        public static bool ApplyDefaultBranchEdgeLabel(PungentBoardEdge edge)
        {
            if (edge == null)
                return false;

            string port = CleanKey(edge.fromPortKey);
            if (string.Equals(port, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(port, "false", StringComparison.OrdinalIgnoreCase))
                return SetEdgeProperty(edge, "labelOverride", char.ToUpperInvariant(port[0]) + port.Substring(1));

            bool changed = SetEdgeProperty(edge, "conditionKey", "condition");
            if (string.IsNullOrWhiteSpace(edge.label))
            {
                edge.label = "Branch";
                changed = true;
            }

            return changed;
        }

        public static string EdgeDisplayLabel(PungentBoardDocument document, PungentBoardEdge edge)
        {
            if (edge == null)
                return "edge";

            string overrideLabel = GetEdgeProperty(edge, "labelOverride");
            if (!string.IsNullOrWhiteSpace(overrideLabel))
                return overrideLabel;
            string condition = GetEdgeProperty(edge, "conditionKey");
            if (!string.IsNullOrWhiteSpace(condition))
                return condition;
            if (!string.IsNullOrWhiteSpace(edge.label))
                return edge.label;
            string path = CleanKey(edge.fromPortKey);
            if (string.Equals(path, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "false", StringComparison.OrdinalIgnoreCase))
                return char.ToUpperInvariant(path[0]) + path.Substring(1);
            return "Order " + edge.executionOrder;
        }

        private static string Slug(string value)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(clean))
                return string.Empty;

            char[] chars = clean.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            string slug = new string(chars);
            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");
            return slug.Trim('-');
        }
    }

    [InitializeOnLoad]
    public static class PungentBoardProviderBootstrap
    {
        static PungentBoardProviderBootstrap()
        {
            RegisterProvider();
        }

        public static void RegisterProvider()
        {
            if (PungentAuthoringProviderRegistry.FindProvider(PungentBoardProvider.Id) is PungentBoardProvider)
                return;

            PungentAuthoringProviderRegistry.Register(new PungentBoardProvider());
        }
    }
#endif
}
