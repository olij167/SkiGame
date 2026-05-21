using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public static class PungentBoardNodeInspectorGUI
    {
        private const string BoardGraphNodeBindingSourceContext = "BoardGraphNodeBinding";
        private static readonly Dictionary<string, PungentAuthoringGuidedBindingState> ProjectBindingPickerStates = new Dictionary<string, PungentAuthoringGuidedBindingState>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ProjectBindingMessages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PendingBodyAutoApplyNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> AdvancedSchemaFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> InspectorSectionFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> GroupTemplateMessages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool DrawInspector(PungentBoardDocument document, PungentBoardCanvasViewState state, out bool deleteRequested)
        {
            deleteRequested = false;
            if (document == null)
            {
                EditorGUILayout.HelpBox("Create or select a board to inspect spatial content.", MessageType.Info);
                return false;
            }

            if (state != null && state.MultiSelectionCount > 1)
                return DrawMultiSelection(document, state, out deleteRequested);

            PungentBoardNode node = PungentBoardCanvasGUI.FindNode(document, state.selectedNodeId);
            if (node != null)
                return DrawNode(document, node, state, out deleteRequested);

            PungentBoardEdge edge = PungentBoardCanvasGUI.FindEdge(document, state.selectedEdgeId);
            if (edge != null)
                return DrawEdge(document, edge, out deleteRequested);

            PungentBoardGroup group = PungentBoardCanvasGUI.FindGroup(document, state.selectedGroupId);
            if (group != null)
                return DrawGroup(document, group, out deleteRequested);

            return DrawBoard(document, out deleteRequested);
        }

        private static bool DrawBoard(PungentBoardDocument document, out bool deleteRequested)
        {
            deleteRequested = false;
            bool changed = false;

            DrawInspectorActionHeader("Board", document.id, "Copy the board authoring ID.");

            if (DrawInspectorSection("board:" + document.id + ":basics", "Basics", true))
            {
                EditorGUI.BeginChangeCheck();
                document.title = EditorGUILayout.TextField("Title", document.title);
                document.summary = EditorGUILayout.TextArea(document.summary, GUILayout.MinHeight(58f));
                document.status = EditorGUILayout.TextField("Status", document.status);
                document.priority = EditorGUILayout.TextField("Priority", document.priority);
                document.visibility = EditorGUILayout.TextField("Visibility", document.visibility);
                document.developerOnly = EditorGUILayout.Toggle("Developer Only", document.developerOnly);
                document.archived = EditorGUILayout.Toggle("Archived", document.archived);
                if (EditorGUI.EndChangeCheck())
                    changed = true;

                DrawTagsField(ref document.tags, ref changed);
            }

            if (DrawInspectorSection("board:" + document.id + ":schema", "Graph Schema", true))
            {
                DrawGraphTypeSelector(document, ref changed);
                EditorGUILayout.LabelField("Schema Version", document.graphSchemaVersion.ToString());
                EditorGUILayout.LabelField("Board Properties", (document.properties?.Count ?? 0).ToString());
            }

            if (DrawInspectorSection("board:" + document.id + ":advanced", "Advanced", false))
            {
                EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Nodes", (document.nodes?.Count ?? 0).ToString());
                EditorGUILayout.LabelField("Edges", (document.edges?.Count ?? 0).ToString());
                EditorGUILayout.LabelField("Groups", (document.groups?.Count ?? 0).ToString());
                EditorGUILayout.LabelField("Template", string.IsNullOrWhiteSpace(document.templateDisplayName) ? "(None)" : document.templateDisplayName);
                EditorGUILayout.LabelField("Integration Profile", string.IsNullOrWhiteSpace(document.integrationProfileId) ? "(None)" : document.integrationProfileId);
                EditorGUILayout.LabelField("Board References", (document.references?.Count ?? 0).ToString());
                EditorGUILayout.LabelField("Board Targets", (document.targets?.Count ?? 0).ToString());
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Canvas Defaults", EditorStyles.boldLabel);
                document.canvasState = document.canvasState ?? new PungentBoardCanvasState();
                EditorGUI.BeginChangeCheck();
                document.canvasState.showGrid = EditorGUILayout.Toggle("Show Grid", document.canvasState.showGrid);
                document.canvasState.snapToGrid = EditorGUILayout.Toggle("Snap To Grid", document.canvasState.snapToGrid);
                document.canvasState.snapSize = EditorGUILayout.FloatField("Snap Size", document.canvasState.snapSize);
                document.canvasState.gridMinorSpacing = EditorGUILayout.FloatField("Grid Minor Spacing", document.canvasState.gridMinorSpacing);
                document.canvasState.gridMajorLineFrequency = EditorGUILayout.IntField("Grid Major Frequency", document.canvasState.gridMajorLineFrequency);
                document.canvasState.gridOpacity = EditorGUILayout.Slider("Grid Opacity", document.canvasState.gridOpacity, 0f, 1f);
                document.canvasState.snapNodes = EditorGUILayout.Toggle("Snap Nodes", document.canvasState.snapNodes);
                document.canvasState.snapGroups = EditorGUILayout.Toggle("Snap Groups", document.canvasState.snapGroups);
                document.canvasState.snapResize = EditorGUILayout.Toggle("Snap Resize", document.canvasState.snapResize);
                if (EditorGUI.EndChangeCheck())
                {
                    document.canvasState.NormalizeInPlace();
                    changed = true;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Delete Board", "Delete this board document after confirmation."), GUILayout.Width(104f)))
                    deleteRequested = true;
                GUILayout.FlexibleSpace();
            }

            return changed;
        }

        private static bool DrawNode(PungentBoardDocument document, PungentBoardNode node, PungentBoardCanvasViewState state, out bool deleteRequested)
        {
            deleteRequested = false;
            bool changed = false;

            DrawInspectorActionHeader("Node", node.id, "Copy the node ID.");

            PungentBoardGraphTypeDefinition graphType = document != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId) : null;
            PungentBoardNodeTypeDefinition selectedNodeType = graphType != null ? PungentBoardCanvasGUI.ResolveNodeType(graphType, node) : null;
            string previousBody = node.body ?? string.Empty;
            string bodyControlName = "BoardNodeBody_" + node.id;
            if (DrawInspectorSection("node:" + node.id + ":basics", "Inline-Editable Content", true))
            {
                EditorGUI.BeginChangeCheck();
                node.title = EditorGUILayout.TextField("Title", node.title);
                selectedNodeType = DrawNodeTypeSelector(document, node);
                node.nodeKind = (PungentBoardNodeKind)EditorGUILayout.EnumPopup("Kind", node.nodeKind);
                node.nodeTypeKey = EditorGUILayout.TextField("Type Key", node.nodeTypeKey);
                node.summary = EditorGUILayout.TextField("Summary", node.summary);
                EditorGUILayout.LabelField("Body");
                GUI.SetNextControlName(bodyControlName);
                node.body = EditorGUILayout.TextArea(node.body, GUILayout.MinHeight(84f));
                node.position = EditorGUILayout.Vector2Field("Position", node.position);
                node.size = EditorGUILayout.Vector2Field("Size", node.size);
                node.colorStyleKey = EditorGUILayout.TextField("Style Key", node.colorStyleKey);
                node.collapsed = EditorGUILayout.Toggle(new GUIContent("Collapsed", "Collapsed nodes hide their body preview on the canvas but keep title, type, status, and ports visible."), node.collapsed);
                if (EditorGUI.EndChangeCheck())
                    changed = true;

                if (GUILayout.Button(new GUIContent(node.collapsed ? "Expand Body Preview" : "Collapse Body Preview", "Toggle the node body preview shown on the canvas."), GUILayout.Height(20f)))
                {
                    node.collapsed = !node.collapsed;
                    changed = true;
                }
            }

            if (!string.Equals(previousBody, node.body ?? string.Empty, StringComparison.Ordinal))
                PendingBodyAutoApplyNodeIds.Add(node.id);
            FlushPendingBodyAutoApply(node, bodyControlName);

            node.properties = node.properties ?? new List<PungentBoardGraphPropertyValue>();
            if (DrawInspectorSection("node:" + node.id + ":schema", "Type / Schema", true))
            {
                if (PungentBoardEventSequenceUtility.IsEventSequence(document) && selectedNodeType != null && PungentBoardEventSequenceUtility.IsEventSequenceNodeType(selectedNodeType.typeKey))
                    DrawEventSequenceNodeFields(node, selectedNodeType, ref changed);
                else
                    EditorGUILayout.HelpBox("Contextual schema controls for this node type will appear here as they are added. Raw key/value schema remains in Advanced Raw Data.", MessageType.Info);
            }

            if (DrawInspectorSection("node:" + node.id + ":authoring", "Authoring Link", true))
                DrawLinkedAuthoringReference(document, node, ref changed);

            if (DrawInspectorSection("node:" + node.id + ":binding", "Project Binding", true))
                DrawProjectBinding(node, ref changed);

            if (DrawInspectorSection("node:" + node.id + ":local", "Local Links", false))
            {
                EditorGUILayout.LabelField("References", (node.references?.Count ?? 0).ToString());
                EditorGUILayout.LabelField("Targets", (node.targets?.Count ?? 0).ToString());
            }

            if (DrawInspectorSection("node:" + node.id + ":advanced", "Advanced Raw Data", false))
            {
                DrawSchemaProperties("Raw Schema Fields", node.properties, selectedNodeType != null ? selectedNodeType.propertyDefinitions : null, ref changed);
                DrawProjectionSource(node);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Start Edge", "Use this node as the source for the next explicit edge."), GUILayout.Width(86f)))
                {
                    state.connectorMode = true;
                    state.connectorSourceNodeId = node.id;
                    state.connectorSourcePortKey = string.Empty;
                    state.repaintRequested = true;
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(state.connectorSourceNodeId) || PungentAuthoringId.EqualsId(state.connectorSourceNodeId, node.id)))
                {
                    if (GUILayout.Button(new GUIContent("Connect Here", "Create an edge from the current source node to this node."), GUILayout.Width(96f)))
                    {
                        PungentBoardEdge edge;
                        if (PungentBoardCanvasGUI.TryCreateTypedEdge(document, state, state.connectorSourceNodeId, node.id, out edge))
                        {
                            document.edges.Add(edge);
                            state.SelectEdge(edge.id);
                            state.contentChanged = true;
                            changed = true;
                        }
                        state.connectorMode = false;
                        state.connectorSourceNodeId = string.Empty;
                        state.connectorSourcePortKey = string.Empty;
                        state.repaintRequested = true;
                    }
                }
                if (!string.IsNullOrWhiteSpace(state.connectorSourceNodeId) && GUILayout.Button(new GUIContent("Cancel", "Cancel edge creation."), GUILayout.Width(62f)))
                {
                    state.connectorMode = false;
                    state.connectorSourceNodeId = string.Empty;
                    state.connectorSourcePortKey = string.Empty;
                    state.repaintRequested = true;
                }
                if (GUILayout.Button(new GUIContent("Delete Node", "Delete this node after confirmation. Connected edges are removed."), GUILayout.Width(104f)))
                    deleteRequested = true;
                GUILayout.FlexibleSpace();
            }

            node.NormalizeInPlace();
            return changed;
        }

        private static bool DrawEdge(PungentBoardDocument document, PungentBoardEdge edge, out bool deleteRequested)
        {
            deleteRequested = false;
            bool changed = false;

            DrawInspectorActionHeader("Edge", edge.id, "Copy the edge ID.");

            EditorGUI.BeginChangeCheck();
            edge.label = EditorGUILayout.TextField("Label", edge.label);
            PungentBoardEdgeRuleDefinition selectedRule = DrawEdgeTypeSelector(document, edge);
            edge.edgeKind = (PungentBoardEdgeKind)EditorGUILayout.EnumPopup("Kind", edge.edgeKind);
            edge.edgeTypeKey = EditorGUILayout.TextField("Type Key", edge.edgeTypeKey);
            edge.directed = EditorGUILayout.Toggle("Directed", edge.directed);
            edge.styleKey = EditorGUILayout.TextField("Style Key", edge.styleKey);
            edge.fromPortKey = EditorGUILayout.TextField("From Port", edge.fromPortKey);
            edge.toPortKey = EditorGUILayout.TextField("To Port", edge.toPortKey);
            bool eventSequenceEdge = PungentBoardEventSequenceUtility.IsEventSequence(document) && selectedRule != null && PungentBoardEventSequenceUtility.IsEventSequenceEdgeType(selectedRule.typeKey);
            if (!eventSequenceEdge)
                edge.executionOrder = EditorGUILayout.IntField("Execution Order", edge.executionOrder);
            DrawNodeEndpointPopup(document, "From", ref edge.fromNodeId);
            DrawNodeEndpointPopup(document, "To", ref edge.toNodeId);
            if (EditorGUI.EndChangeCheck())
                changed = true;

            edge.properties = edge.properties ?? new List<PungentBoardGraphPropertyValue>();
            if (eventSequenceEdge)
                DrawEventSequenceEdgeFields(document, edge, selectedRule, ref changed);
            else
                DrawSchemaProperties("Edge Fields", edge.properties, selectedRule != null ? selectedRule.propertyDefinitions : null, ref changed);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Delete Edge", "Delete this selected edge after confirmation."), GUILayout.Width(104f)))
                    deleteRequested = true;
                GUILayout.FlexibleSpace();
            }

            edge.NormalizeInPlace();
            return changed;
        }

        private static bool DrawGroup(PungentBoardDocument document, PungentBoardGroup group, out bool deleteRequested)
        {
            deleteRequested = false;
            bool changed = false;

            DrawInspectorActionHeader("Group", group.id, "Copy the group ID.");

            if (DrawInspectorSection("group:" + group.id + ":basics", "Basics", true))
            {
                EditorGUI.BeginChangeCheck();
                group.title = EditorGUILayout.TextField("Title", group.title);
                Vector2 position = EditorGUILayout.Vector2Field("Position", group.rect.position);
                Vector2 size = EditorGUILayout.Vector2Field("Size", group.rect.size);
                group.rect = new Rect(position, size);
                group.colorStyleKey = EditorGUILayout.TextField("Style Key", group.colorStyleKey);
                group.collapsed = EditorGUILayout.Toggle(new GUIContent("Collapsed", "Collapse/expand the group. When Collapse As Node is enabled, collapsed groups draw as a reusable node-like card."), group.collapsed);
                group.locked = EditorGUILayout.Toggle("Locked", group.locked);
                if (EditorGUI.EndChangeCheck())
                    changed = true;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(group.collapsed ? "Expand Group" : "Collapse Group", "Toggle the group collapse state."), GUILayout.Height(20f)))
                    {
                        group.collapsed = !group.collapsed;
                        changed = true;
                    }
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField("Contained Nodes", (group.containedNodeIds?.Count ?? 0).ToString());
            }

            if (DrawInspectorSection("group:" + group.id + ":custom", "Custom Node", true))
                changed = DrawGroupCustomNodeSection(document, group) || changed;

            if (DrawInspectorSection("group:" + group.id + ":ports", "Ports", true))
            {
                changed = DrawGroupPortDefinitions(group) || changed;
                changed = DrawGroupPortMappings(document, group) || changed;
            }

            if (DrawInspectorSection("group:" + group.id + ":slots", "Bindings / References", false))
            {
                changed = DrawGroupBindingSlots(group) || changed;
                changed = DrawGroupReferenceSlots(group) || changed;
            }

            if (DrawInspectorSection("group:" + group.id + ":advanced", "Advanced", false))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Template ID", group.customNodeTemplateId ?? string.Empty);
                    EditorGUILayout.TextField("Source Template", group.customNodeSourceTemplateId ?? string.Empty);
                    EditorGUILayout.TextField("Source Adapter", group.integrationSourceAdapterId ?? string.Empty);
                    EditorGUILayout.TextField("Source Key", group.integrationSourceKey ?? string.Empty);
                }
                DrawStringList("Contained Node IDs", group.containedNodeIds, ref changed);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Delete Group", "Delete this group after confirmation. Nodes remain."), GUILayout.Width(104f)))
                    deleteRequested = true;
                GUILayout.FlexibleSpace();
            }

            group.NormalizeInPlace();
            return changed;
        }

        private static bool DrawGroupCustomNodeSection(PungentBoardDocument document, PungentBoardGroup group)
        {
            bool changed = false;
            EditorGUILayout.HelpBox("Custom group nodes are editable group snapshots. Saving a template adds it to the Custom Group Nodes palette; instancing clones the internal nodes and edges with fresh IDs.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            group.customNodeEnabled = EditorGUILayout.Toggle(new GUIContent("Enable Custom Node", "Mark this group as a group-backed custom node/template source."), group.customNodeEnabled);
            group.collapsedAsNode = EditorGUILayout.Toggle(new GUIContent("Collapse As Node", "When collapsed, draw this group as a node-like card with exposed ports."), group.collapsedAsNode);
            if (group.collapsedAsNode && !group.collapsed)
                EditorGUILayout.HelpBox("Collapse the group to preview it as a node-like custom group card.", MessageType.Info);
            if (EditorGUI.EndChangeCheck())
                changed = true;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Template ID", string.IsNullOrWhiteSpace(group.customNodeTemplateId) ? "(New template on save)" : group.customNodeTemplateId);
                EditorGUILayout.TextField("Source Template", string.IsNullOrWhiteSpace(group.customNodeSourceTemplateId) ? "(This group is a source or unsaved clone)" : group.customNodeSourceTemplateId);
            }

            PungentBoardCustomNodeTemplate sourceTemplate = PungentBoardCustomNodeTemplateStorage.Database.Find(group.customNodeSourceTemplateId);
            if (sourceTemplate != null && !TemplateSourceIsAvailable(sourceTemplate))
                EditorGUILayout.HelpBox("The source board/group for this template is not available. Instantiation still uses the stored snapshot, so cloned content remains safe and editable.", MessageType.Info);

            int contained = group.containedNodeIds?.Count ?? 0;
            if (contained == 0)
                EditorGUILayout.HelpBox("No contained nodes are recorded yet. Move nodes inside the group, then save the board or template so the snapshot has content.", MessageType.Info);
            if ((group.exposedPorts?.Count ?? 0) == 0)
                EditorGUILayout.HelpBox("No exposed ports are configured. The collapsed group can still act as a visual macro, but it cannot connect through group sockets yet.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Save / Update Template", "Snapshot this group into the ProjectSettings custom node template library."), GUILayout.Height(22f)))
                {
                    PungentBoardCustomNodeTemplate template;
                    string message;
                    if (PungentBoardCustomNodeTemplateStorage.SaveGroupAsTemplate(document, group, out template, out message))
                    {
                        GroupTemplateMessages[group.id] = message;
                        changed = true;
                    }
                    else
                    {
                        GroupTemplateMessages[group.id] = string.IsNullOrWhiteSpace(message) ? "Could not save custom node template." : message;
                    }
                }

                if (GUILayout.Button(new GUIContent("Reload Templates", "Reload the custom node template library from ProjectSettings."), GUILayout.Height(22f), GUILayout.Width(112f)))
                {
                    PungentBoardCustomNodeTemplateStorage.Reload();
                }
            }

            string templateMessage;
            if (GroupTemplateMessages.TryGetValue(group.id, out templateMessage) && !string.IsNullOrWhiteSpace(templateMessage))
                EditorGUILayout.HelpBox(templateMessage, templateMessage.IndexOf("could not", StringComparison.OrdinalIgnoreCase) >= 0 ? MessageType.Warning : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(PungentBoardCustomNodeTemplateStorage.LoadError))
                EditorGUILayout.HelpBox("Template load warning: " + PungentBoardCustomNodeTemplateStorage.LoadError, MessageType.Warning);

            return changed;
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

        private static bool DrawGroupPortDefinitions(PungentBoardGroup group)
        {
            bool changed = false;
            group.exposedPorts = group.exposedPorts ?? new List<PungentBoardPortDefinition>();
            EditorGUILayout.LabelField("Exposed Ports", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Exposed ports are the public sockets shown when the group is collapsed as a node. Map each one to an internal node port below before using it for connections.", MessageType.None);

            for (int i = 0; i < group.exposedPorts.Count; i++)
            {
                PungentBoardPortDefinition port = group.exposedPorts[i];
                if (port == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Port " + (i + 1), EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Remove", "Remove this exposed group port."), GUILayout.Width(68f)))
                        {
                            group.exposedPorts.RemoveAt(i);
                            group.exposedPortMappings?.RemoveAll(mapping => mapping != null && string.Equals(mapping.exposedPortKey, port.key, StringComparison.OrdinalIgnoreCase));
                            changed = true;
                            i--;
                            continue;
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    port.key = EditorGUILayout.TextField(new GUIContent("Key", "Stable exposed port key used by mappings and validation."), port.key);
                    port.displayName = EditorGUILayout.TextField(new GUIContent("Label", "Human-readable socket label. Generic In/Out labels are hidden on the canvas."), port.displayName);
                    port.direction = (PungentBoardPortDirection)EditorGUILayout.EnumPopup("Direction", port.direction);
                    port.required = EditorGUILayout.Toggle("Required", port.required);
                    port.allowMultipleConnections = EditorGUILayout.Toggle("Multiple", port.allowMultipleConnections);
                    if (EditorGUI.EndChangeCheck())
                    {
                        port.NormalizeInPlace();
                        changed = true;
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("Add Exposed Port", "Add a public socket for this group-backed custom node."), GUILayout.Height(20f)))
            {
                group.exposedPorts.Add(new PungentBoardPortDefinition
                {
                    key = "port" + (group.exposedPorts.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    displayName = "Port",
                    direction = group.exposedPorts.Count == 0 ? PungentBoardPortDirection.Input : PungentBoardPortDirection.Output
                });
                changed = true;
            }

            return changed;
        }

        private static bool DrawGroupPortMappings(PungentBoardDocument document, PungentBoardGroup group)
        {
            bool changed = false;
            group.exposedPortMappings = group.exposedPortMappings ?? new List<PungentBoardCustomNodePortMapping>();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Port Mappings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Mappings connect public group sockets to internal node ports or fields. External edges still store node endpoints; the group port is a visual proxy.", MessageType.None);

            for (int i = 0; i < group.exposedPortMappings.Count; i++)
            {
                PungentBoardCustomNodePortMapping mapping = group.exposedPortMappings[i];
                if (mapping == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Mapping " + (i + 1), EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Remove", "Remove this public-to-internal port mapping."), GUILayout.Width(68f)))
                        {
                            group.exposedPortMappings.RemoveAt(i);
                            changed = true;
                            i--;
                            continue;
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    mapping.exposedPortKey = DrawExposedPortPopup(group, "Exposed", mapping.exposedPortKey);
                    mapping.direction = (PungentBoardPortDirection)EditorGUILayout.EnumPopup("Direction", mapping.direction);
                    mapping.internalNodeId = DrawInternalNodePopup(document, group, "Internal Node", mapping.internalNodeId);
                    mapping.internalPortKey = DrawInternalPortPopup(document, mapping.internalNodeId, mapping.direction, mapping.internalPortKey);
                    mapping.internalFieldKey = EditorGUILayout.TextField(new GUIContent("Field Key", "Optional field/property key for parameter-style ports."), mapping.internalFieldKey);
                    mapping.valueType = (PungentAuthoringBindingValueType)EditorGUILayout.EnumPopup("Value Type", mapping.valueType);
                    mapping.notes = EditorGUILayout.TextField("Notes", mapping.notes);
                    if (EditorGUI.EndChangeCheck())
                    {
                        mapping.NormalizeInPlace();
                        changed = true;
                    }

                    if (string.IsNullOrWhiteSpace(mapping.exposedPortKey) || string.IsNullOrWhiteSpace(mapping.internalNodeId))
                        EditorGUILayout.HelpBox("Choose an exposed port and an internal node before this mapping can create group-port edges.", MessageType.Warning);
                }
            }

            if (GUILayout.Button(new GUIContent("Add Port Mapping", "Map an exposed group socket to an internal node port."), GUILayout.Height(20f)))
            {
                string firstPort = group.exposedPorts != null && group.exposedPorts.Count > 0 ? group.exposedPorts[0].key : string.Empty;
                group.exposedPortMappings.Add(new PungentBoardCustomNodePortMapping
                {
                    exposedPortKey = firstPort,
                    direction = PungentBoardPortDirection.Both
                });
                changed = true;
            }

            return changed;
        }

        private static bool DrawGroupBindingSlots(PungentBoardGroup group)
        {
            bool changed = false;
            group.exposedBindingSlots = group.exposedBindingSlots ?? new List<PungentAuthoringBindingSlot>();
            EditorGUILayout.LabelField("Binding Slots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Slots describe runtime/export-facing binding points. Endpoint discovery and read/apply stay in the shared Authoring binding services; this group owns only the workflow metadata.", MessageType.None);

            for (int i = 0; i < group.exposedBindingSlots.Count; i++)
            {
                PungentAuthoringBindingSlot slot = group.exposedBindingSlots[i];
                if (slot == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        slot.enabled = EditorGUILayout.Toggle(slot.enabled, GUILayout.Width(18f));
                        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(slot.displayName) ? "Binding Slot" : slot.displayName, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(68f)))
                        {
                            group.exposedBindingSlots.RemoveAt(i);
                            changed = true;
                            i--;
                            continue;
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    slot.displayName = EditorGUILayout.TextField("Name", slot.displayName);
                    slot.role = (PungentAuthoringBindingSlotRole)EditorGUILayout.EnumPopup("Role", slot.role == PungentAuthoringBindingSlotRole.Unknown ? PungentAuthoringBindingSlotRole.BoardGroup : slot.role);
                    slot.fieldKey = EditorGUILayout.TextField(new GUIContent("Field Key", "Example: group.parameter.speed or exposedPorts.true"), slot.fieldKey);
                    slot.valueType = (PungentAuthoringBindingValueType)EditorGUILayout.EnumPopup("Value Type", slot.valueType);
                    slot.pullEnabled = EditorGUILayout.Toggle("Pull", slot.pullEnabled);
                    slot.pushEnabled = EditorGUILayout.Toggle("Push", slot.pushEnabled);
                    slot.runtimePayload = EditorGUILayout.TextField("Runtime Payload", slot.runtimePayload);
                    if (EditorGUI.EndChangeCheck())
                    {
                        slot.elementId = group.id;
                        slot.NormalizeInPlace();
                        changed = true;
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("Add Binding Slot", "Expose a binding slot for plan/export adapters."), GUILayout.Height(20f)))
            {
                group.exposedBindingSlots.Add(new PungentAuthoringBindingSlot
                {
                    role = PungentAuthoringBindingSlotRole.BoardGroup,
                    interfaceId = "board-graph",
                    itemId = group.id,
                    elementId = group.id,
                    fieldKey = "group.parameter",
                    displayName = "Group Parameter",
                    enabled = true,
                    pullEnabled = true,
                    pushEnabled = true
                });
                changed = true;
            }

            return changed;
        }

        private static bool DrawGroupReferenceSlots(PungentBoardGroup group)
        {
            bool changed = false;
            group.referenceSlots = group.referenceSlots ?? new List<PungentBoardCustomNodeReferenceSlot>();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Reference Slots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Reference slots mark object, component, field, or parameter references that project adapters can bind later. Missing targets stay visible and non-destructive.", MessageType.None);

            for (int i = 0; i < group.referenceSlots.Count; i++)
            {
                PungentBoardCustomNodeReferenceSlot slot = group.referenceSlots[i];
                if (slot == null)
                    continue;

                slot.target = slot.target ?? new PungentAuthoringTarget();
                slot.bindingPath = slot.bindingPath ?? new PungentAuthoringBindingPath();
                slot.bindingSlot = slot.bindingSlot ?? new PungentAuthoringBindingSlot();

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(slot.displayName) ? "Reference Slot" : slot.displayName, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(68f)))
                        {
                            group.referenceSlots.RemoveAt(i);
                            changed = true;
                            i--;
                            continue;
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    slot.displayName = EditorGUILayout.TextField("Name", slot.displayName);
                    slot.required = EditorGUILayout.Toggle("Required", slot.required);
                    slot.target.targetKind = (PungentAuthoringTargetKind)EditorGUILayout.EnumPopup("Target Kind", slot.target.targetKind);
                    slot.target.label = EditorGUILayout.TextField("Target Label", slot.target.label);
                    slot.target.rawValue = EditorGUILayout.TextField("Target Value", slot.target.rawValue);
                    slot.target.contextId = EditorGUILayout.TextField("Context ID", slot.target.contextId);
                    slot.target.propertyPath = EditorGUILayout.TextField("Property Path", slot.target.propertyPath);
                    slot.bindingSlot.role = (PungentAuthoringBindingSlotRole)EditorGUILayout.EnumPopup("Slot Role", slot.bindingSlot.role == PungentAuthoringBindingSlotRole.Unknown ? PungentAuthoringBindingSlotRole.BoardParameter : slot.bindingSlot.role);
                    slot.bindingSlot.fieldKey = EditorGUILayout.TextField("Slot Field", slot.bindingSlot.fieldKey);
                    slot.notes = EditorGUILayout.TextField("Notes", slot.notes);
                    if (EditorGUI.EndChangeCheck())
                    {
                        slot.bindingSlot.elementId = group.id;
                        slot.NormalizeInPlace();
                        changed = true;
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("Add Reference Slot", "Expose an object/component/field/parameter reference slot."), GUILayout.Height(20f)))
            {
                group.referenceSlots.Add(new PungentBoardCustomNodeReferenceSlot
                {
                    displayName = "Reference",
                    required = false,
                    bindingSlot = new PungentAuthoringBindingSlot
                    {
                        role = PungentAuthoringBindingSlotRole.BoardParameter,
                        interfaceId = "board-graph",
                        itemId = group.id,
                        elementId = group.id,
                        fieldKey = "group.reference",
                        displayName = "Reference"
                    }
                });
                changed = true;
            }

            return changed;
        }

        private static string DrawExposedPortPopup(PungentBoardGroup group, string label, string currentKey)
        {
            List<PungentBoardPortDefinition> ports = (group?.exposedPorts ?? new List<PungentBoardPortDefinition>()).Where(port => port != null).ToList();
            if (ports.Count == 0)
                return EditorGUILayout.TextField(label, currentKey);

            int index = Mathf.Max(0, ports.FindIndex(port => string.Equals(port.key, currentKey, StringComparison.OrdinalIgnoreCase)));
            string[] labels = ports.Select(port => PortLabel(port)).ToArray();
            int next = EditorGUILayout.Popup(label, index, labels);
            return next >= 0 && next < ports.Count ? ports[next].key : currentKey;
        }

        private static string DrawInternalNodePopup(PungentBoardDocument document, PungentBoardGroup group, string label, string currentNodeId)
        {
            HashSet<string> contained = new HashSet<string>(group?.containedNodeIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            List<PungentBoardNode> nodes = (document?.nodes ?? new List<PungentBoardNode>())
                .Where(node => node != null && (contained.Count == 0 || contained.Contains(node.id)))
                .ToList();
            if (nodes.Count == 0)
                return EditorGUILayout.TextField(label, currentNodeId);

            int index = Mathf.Max(0, nodes.FindIndex(node => PungentAuthoringId.EqualsId(node.id, currentNodeId)));
            string[] labels = nodes.Select(node => string.IsNullOrWhiteSpace(node.title) ? node.id : node.title).ToArray();
            int next = EditorGUILayout.Popup(label, index, labels);
            return next >= 0 && next < nodes.Count ? nodes[next].id : currentNodeId;
        }

        private static string DrawInternalPortPopup(PungentBoardDocument document, string nodeId, PungentBoardPortDirection direction, string currentPortKey)
        {
            PungentBoardNode node = PungentBoardCanvasGUI.FindNode(document, nodeId);
            PungentBoardGraphTypeDefinition graphType = document != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId) : null;
            PungentBoardNodeTypeDefinition nodeType = PungentBoardCanvasGUI.ResolveNodeType(graphType, node);
            List<PungentBoardPortDefinition> ports = new List<PungentBoardPortDefinition>();
            if (nodeType != null)
            {
                if (direction == PungentBoardPortDirection.Input || direction == PungentBoardPortDirection.Both)
                    ports.AddRange(nodeType.InputPorts());
                if (direction == PungentBoardPortDirection.Output || direction == PungentBoardPortDirection.Both)
                    ports.AddRange(nodeType.OutputPorts());
            }

            ports = ports.Where(port => port != null).GroupBy(port => port.key, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
            if (ports.Count == 0)
                return EditorGUILayout.TextField(new GUIContent("Internal Port", "Internal node port key. Raw text is preserved for custom/missing schemas."), currentPortKey);

            int index = ports.FindIndex(port => string.Equals(port.key, currentPortKey, StringComparison.OrdinalIgnoreCase));
            index = Mathf.Max(0, index);
            string[] labels = ports.Select(PortLabel).ToArray();
            int next = EditorGUILayout.Popup("Internal Port", index, labels);
            return next >= 0 && next < ports.Count ? ports[next].key : currentPortKey;
        }

        private static void DrawStringList(string title, List<string> values, ref bool changed)
        {
            values = values ?? new List<string>();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int i = 0; i < values.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.TextField(values[i] ?? string.Empty);
                    if (EditorGUI.EndChangeCheck())
                    {
                        values[i] = next;
                        changed = true;
                    }

                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        values.RemoveAt(i);
                        changed = true;
                        i--;
                    }
                }
            }
        }

        private static bool DrawMultiSelection(PungentBoardDocument document, PungentBoardCanvasViewState state, out bool deleteRequested)
        {
            deleteRequested = false;
            bool changed = false;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Multi-selection", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Copy IDs", "Copy selected node and group IDs."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    List<string> ids = new List<string>();
                    ids.AddRange(state.selectedNodeIds);
                    ids.AddRange(state.selectedGroupIds);
                    EditorGUIUtility.systemCopyBuffer = string.Join("\n", ids.ToArray());
                }
            }
            EditorGUILayout.LabelField("Nodes", state.SelectedNodeCount.ToString());
            EditorGUILayout.LabelField("Groups", state.SelectedGroupCount.ToString());
            EditorGUILayout.HelpBox("Use the canvas to move the selection together. Alignment, distribution, duplication, and deletion are available from the toolbar/context actions.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Delete Selection", "Delete selected nodes/groups after confirmation."), GUILayout.Width(116f)))
                    deleteRequested = true;

                GUILayout.FlexibleSpace();
            }

            return changed;
        }

        private static void DrawInspectorActionHeader(string label, string id, string copyTooltip)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(label, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(id)))
                {
                    if (GUILayout.Button(new GUIContent("Copy ID", copyTooltip), EditorStyles.toolbarButton, GUILayout.Width(66f)))
                        EditorGUIUtility.systemCopyBuffer = id ?? string.Empty;
                }
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("ID", id ?? string.Empty);
        }

        private static bool DrawInspectorSection(string key, string label, bool defaultOpen)
        {
            bool open;
            if (!InspectorSectionFoldouts.TryGetValue(key, out open))
                open = defaultOpen;

            open = EditorGUILayout.Foldout(open, label, true);
            InspectorSectionFoldouts[key] = open;
            return open;
        }

        private static void DrawGraphTypeSelector(PungentBoardDocument document, ref bool changed)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Graph Type", EditorStyles.boldLabel);
            List<PungentBoardGraphTypeDefinition> graphTypes = PungentBoardGraphTypeRegistry.GraphTypes.ToList();
            string currentId = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(document.graphTypeId);
            int currentIndex = graphTypes.FindIndex(type => type != null && string.Equals(type.id, currentId, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                EditorGUILayout.HelpBox("Graph type '" + currentId + "' is not registered. The board remains visible and editable through the Freeform fallback.", MessageType.Warning);
                EditorGUI.BeginChangeCheck();
                document.graphTypeId = EditorGUILayout.TextField("Missing Type ID", document.graphTypeId);
                if (EditorGUI.EndChangeCheck())
                    changed = true;
                return;
            }

            string[] labels = graphTypes.Select(type => type.displayName).ToArray();
            int nextIndex = EditorGUILayout.Popup(new GUIContent("Type", "Choose the schema/preset that drives node palette, ports, edge rules, and local validation."), currentIndex, labels);
            if (nextIndex != currentIndex && nextIndex >= 0 && nextIndex < graphTypes.Count)
            {
                PungentBoardGraphTypeDefinition selected = graphTypes[nextIndex];
                document.graphTypeId = selected.id;
                document.graphTypeDisplayName = selected.displayName;
                document.graphSchemaVersion = selected.schemaVersion;
                changed = true;
            }

            PungentBoardGraphTypeDefinition graphType = graphTypes[Mathf.Clamp(nextIndex, 0, graphTypes.Count - 1)];
            EditorGUILayout.LabelField("ID", graphType.id);
            EditorGUILayout.LabelField("Version", graphType.schemaVersion.ToString());
            EditorGUILayout.LabelField("Execution", graphType.executionMetadata != null ? graphType.executionMetadata.executionMode.ToString() : "None");
            EditorGUILayout.LabelField("Root Required", graphType.requiresRootNode ? (string.IsNullOrWhiteSpace(graphType.rootNodeTypeKey) ? "Yes" : graphType.rootNodeTypeKey) : "No");
            EditorGUILayout.LabelField("Cycles", graphType.allowCycles ? "Allowed" : "Disallowed");
            if (!string.IsNullOrWhiteSpace(graphType.description))
                EditorGUILayout.LabelField(graphType.description, EditorStyles.wordWrappedMiniLabel);
        }

        private static PungentBoardNodeTypeDefinition DrawNodeTypeSelector(PungentBoardDocument document, PungentBoardNode node)
        {
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.Find(document.graphTypeId);
            if (graphType == null)
            {
                EditorGUILayout.HelpBox("The selected graph type is missing. Node type is preserved as text.", MessageType.Warning);
                return null;
            }

            List<PungentBoardNodeTypeDefinition> nodeTypes = (graphType.nodeTypes ?? new List<PungentBoardNodeTypeDefinition>()).Where(type => type != null).ToList();
            if (nodeTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("This graph type has no node palette entries.", MessageType.Info);
                return null;
            }

            PungentBoardNodeTypeDefinition currentType = PungentBoardCanvasGUI.ResolveNodeType(graphType, node);
            int currentIndex = Mathf.Max(0, nodeTypes.FindIndex(type => currentType != null && string.Equals(type.typeKey, currentType.typeKey, StringComparison.OrdinalIgnoreCase)));
            string[] labels = nodeTypes.Select(type => type.displayName).ToArray();
            int nextIndex = EditorGUILayout.Popup(new GUIContent("Type", "Node schema entry for palette, ports, validation, and inspector hints."), currentIndex, labels);
            if (nextIndex != currentIndex && nextIndex >= 0 && nextIndex < nodeTypes.Count)
                PungentBoardCanvasGUI.ApplyNodeTypeDefaults(document, node, nodeTypes[nextIndex].typeKey);

            PungentBoardNodeTypeDefinition selectedType = nodeTypes[Mathf.Clamp(nextIndex, 0, nodeTypes.Count - 1)];
            if (!string.IsNullOrWhiteSpace(selectedType.description))
                EditorGUILayout.LabelField(selectedType.description, EditorStyles.wordWrappedMiniLabel);
            DrawNodePortsSummary(selectedType);
            return selectedType;
        }

        private static void DrawNodePortsSummary(PungentBoardNodeTypeDefinition nodeType)
        {
            if (nodeType == null || nodeType.ports == null || nodeType.ports.Count == 0)
                return;

            string inputs = string.Join(", ", nodeType.InputPorts().Select(port => PortLabel(port)).ToArray());
            string outputs = string.Join(", ", nodeType.OutputPorts().Select(port => PortLabel(port)).ToArray());
            if (!string.IsNullOrWhiteSpace(inputs))
                EditorGUILayout.LabelField("Inputs", inputs);
            if (!string.IsNullOrWhiteSpace(outputs))
                EditorGUILayout.LabelField("Outputs", outputs);
        }

        private static string PortLabel(PungentBoardPortDefinition port)
        {
            if (port == null)
                return string.Empty;
            return port.displayName + (port.required ? " *" : string.Empty);
        }

        private static PungentBoardEdgeRuleDefinition DrawEdgeTypeSelector(PungentBoardDocument document, PungentBoardEdge edge)
        {
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.Find(document.graphTypeId);
            if (graphType == null)
            {
                EditorGUILayout.HelpBox("The selected graph type is missing. Edge type is preserved as text.", MessageType.Warning);
                return null;
            }

            List<PungentBoardEdgeRuleDefinition> rules = (graphType.edgeRules ?? new List<PungentBoardEdgeRuleDefinition>()).Where(rule => rule != null).ToList();
            if (rules.Count == 0)
                return null;

            int currentIndex = Mathf.Max(0, rules.FindIndex(rule => string.Equals(rule.typeKey, edge.edgeTypeKey, StringComparison.OrdinalIgnoreCase)));
            string[] labels = rules.Select(rule => rule.displayName).ToArray();
            int nextIndex = EditorGUILayout.Popup(new GUIContent("Type", "Edge rule for allowed endpoints, ports, style, direction, and validation."), currentIndex, labels);
            if (nextIndex != currentIndex && nextIndex >= 0 && nextIndex < rules.Count)
                ApplyEdgeRule(edge, rules[nextIndex]);

            PungentBoardEdgeRuleDefinition selectedRule = rules[Mathf.Clamp(nextIndex, 0, rules.Count - 1)];
            if (!string.IsNullOrWhiteSpace(selectedRule.description))
                EditorGUILayout.LabelField(selectedRule.description, EditorStyles.wordWrappedMiniLabel);
            DrawEdgeRuleSummary(selectedRule);
            return selectedRule;
        }

        private static void DrawEdgeRuleSummary(PungentBoardEdgeRuleDefinition rule)
        {
            if (rule == null)
                return;

            string fromTypes = rule.fromNodeTypeKeys == null || rule.fromNodeTypeKeys.Count == 0 ? "Any" : string.Join(", ", rule.fromNodeTypeKeys.ToArray());
            string toTypes = rule.toNodeTypeKeys == null || rule.toNodeTypeKeys.Count == 0 ? "Any" : string.Join(", ", rule.toNodeTypeKeys.ToArray());
            EditorGUILayout.LabelField("Allows", fromTypes + " -> " + toTypes);
            if (!string.IsNullOrWhiteSpace(rule.fromPortKey) || !string.IsNullOrWhiteSpace(rule.toPortKey))
                EditorGUILayout.LabelField("Ports", (string.IsNullOrWhiteSpace(rule.fromPortKey) ? "(default)" : rule.fromPortKey) + " -> " + (string.IsNullOrWhiteSpace(rule.toPortKey) ? "(default)" : rule.toPortKey));
            EditorGUILayout.LabelField("Directed", rule.directed ? "Yes" : "No");
        }

        private static void DrawEventSequenceNodeFields(PungentBoardNode node, PungentBoardNodeTypeDefinition nodeType, ref bool changed)
        {
            if (node == null || nodeType == null)
                return;

            PungentBoardCanvasGUI.EnsurePropertyValues(node.properties, nodeType.propertyDefinitions);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Event Sequence Step", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Step Type", nodeType.displayName);

            string typeKey = PungentBoardEventSequenceUtility.CleanKey(nodeType.typeKey);
            switch (typeKey)
            {
                case "event":
                    changed = DrawEventSequenceTextField(node, "eventKey", "Event Key", "Logical entry/event key for this sequence step.") || changed;
                    changed = DrawEventSequenceTextArea(node, "payloadJson", "Payload JSON", 54f) || changed;
                    break;
                case "delay":
                    changed = DrawEventSequenceFloatField(node, "durationSeconds", "Duration Seconds", 1f, 0.01f) || changed;
                    if (!PungentBoardEventSequenceUtility.TryGetPositiveFloat(node, "durationSeconds", out _))
                        EditorGUILayout.HelpBox("Delay steps should have a duration greater than zero.", MessageType.Warning);
                    break;
                case "camera-move":
                    changed = DrawEventSequenceTextField(node, "cameraTarget", "Camera Target", "Camera rig, virtual camera, shot marker, or binding key.") || changed;
                    changed = DrawBindingTargetFillButton(node, "cameraTarget", "Use Binding As Target") || changed;
                    changed = DrawEventSequenceFloatField(node, "durationSeconds", "Duration Seconds", 1f, 0f) || changed;
                    changed = DrawEventSequenceEnumField(node, "easing", "Easing", new[] { "Linear", "EaseIn", "EaseOut", "EaseInOut" }) || changed;
                    break;
                case "trigger":
                    changed = DrawEventSequenceTextField(node, "triggerKey", "Trigger Key", "Adapter-facing trigger/event name.") || changed;
                    changed = DrawBindingTargetFillButton(node, "triggerKey", "Use Binding As Trigger") || changed;
                    changed = DrawEventSequenceTextArea(node, "payloadJson", "Payload JSON", 54f) || changed;
                    break;
                case "wait":
                    changed = DrawEventSequenceTextField(node, "waitFor", "Wait For", "Condition, flag, signal, binding key, or adapter event to wait for.") || changed;
                    changed = DrawBindingTargetFillButton(node, "waitFor", "Use Binding As Wait Source") || changed;
                    changed = DrawEventSequenceFloatField(node, "timeoutSeconds", "Timeout Seconds", 0f, 0f) || changed;
                    break;
                case "branch":
                    changed = DrawEventSequenceTextField(node, "conditionKey", "Condition Key", "Condition evaluated by a later adapter/simulation pass.") || changed;
                    changed = DrawBindingTargetFillButton(node, "conditionKey", "Use Binding As Condition") || changed;
                    changed = DrawEventSequenceTextField(node, "falseFallback", "False Fallback", "Optional note/key for the false branch fallback.") || changed;
                    EditorGUILayout.HelpBox("Use True and False output ports for the branch paths. Edge labels or condition fields clarify the authored decision.", MessageType.Info);
                    break;
                default:
                    DrawSchemaProperties("Schema Fields", node.properties, nodeType.propertyDefinitions, ref changed);
                    return;
            }

            DrawAdvancedSchemaFoldout("node-" + node.id, "Advanced Schema", node.properties, nodeType.propertyDefinitions, ref changed);
        }

        private static void DrawEventSequenceEdgeFields(PungentBoardDocument document, PungentBoardEdge edge, PungentBoardEdgeRuleDefinition rule, ref bool changed)
        {
            if (edge == null || rule == null)
                return;

            PungentBoardCanvasGUI.EnsurePropertyValues(edge.properties, rule.propertyDefinitions);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Event Sequence Edge", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            edge.executionOrder = EditorGUILayout.IntField(new GUIContent("Order", "Outgoing execution order from the source node."), edge.executionOrder);
            if (EditorGUI.EndChangeCheck())
                changed = true;

            string typeKey = PungentBoardEventSequenceUtility.CleanKey(rule.typeKey);
            if (string.Equals(typeKey, "sequence", StringComparison.OrdinalIgnoreCase))
            {
                changed = DrawEventSequenceEdgeFloatField(edge, "delaySeconds", "Delay Seconds", 0f, 0f) || changed;
            }
            else if (string.Equals(typeKey, "branch", StringComparison.OrdinalIgnoreCase))
            {
                changed = DrawEventSequenceEdgeTextField(edge, "conditionKey", "Condition Key") || changed;
                changed = DrawEventSequenceEdgeTextField(edge, "labelOverride", "Label Override") || changed;
                DrawBranchEdgeGuidance(document, edge, ref changed);
            }

            DrawAdvancedSchemaFoldout("edge-" + edge.id, "Advanced Edge Schema", edge.properties, rule.propertyDefinitions, ref changed);
        }

        private static void DrawBranchEdgeGuidance(PungentBoardDocument document, PungentBoardEdge edge, ref bool changed)
        {
            PungentBoardNode fromNode = PungentBoardCanvasGUI.FindNode(document, edge.fromNodeId);
            bool fromBranch = fromNode != null && string.Equals(PungentBoardEventSequenceUtility.CleanKey(fromNode.nodeTypeKey), "branch", StringComparison.OrdinalIgnoreCase);
            if (!fromBranch)
                return;

            string port = PungentBoardEventSequenceUtility.CleanKey(edge.fromPortKey);
            bool hasPathPort = string.Equals(port, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(port, "false", StringComparison.OrdinalIgnoreCase);
            if (hasPathPort)
            {
                EditorGUILayout.LabelField("Branch Path", char.ToUpperInvariant(port[0]) + port.Substring(1));
                if (GUILayout.Button(new GUIContent("Use Port As Label", "Copy the True/False port meaning into the edge label override."), GUILayout.Height(20f)))
                {
                    PungentBoardEventSequenceUtility.SetEdgeProperty(edge, "labelOverride", char.ToUpperInvariant(port[0]) + port.Substring(1));
                    changed = true;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("This branch edge is not using the True or False output port. Add a condition or label so the path is readable.", MessageType.Warning);
            }
        }

        private static void DrawAdvancedSchemaFoldout(string key, string title, List<PungentBoardGraphPropertyValue> values, List<PungentBoardGraphPropertyDefinition> definitions, ref bool changed)
        {
            if ((definitions == null || definitions.Count == 0) && (values == null || values.Count == 0))
                return;

            bool open = false;
            AdvancedSchemaFoldouts.TryGetValue(key, out open);
            open = EditorGUILayout.Foldout(open, title, true);
            AdvancedSchemaFoldouts[key] = open;
            if (open)
                DrawSchemaProperties(title, values, definitions, ref changed);
        }

        private static bool DrawEventSequenceTextField(PungentBoardNode node, string key, string label, string tooltip)
        {
            string current = PungentBoardEventSequenceUtility.GetNodeProperty(node, key);
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.TextField(new GUIContent(label, tooltip), current);
            if (!EditorGUI.EndChangeCheck())
                return false;

            PungentBoardEventSequenceUtility.SetNodeProperty(node, key, next);
            return true;
        }

        private static bool DrawEventSequenceTextArea(PungentBoardNode node, string key, string label, float minHeight)
        {
            string current = PungentBoardEventSequenceUtility.GetNodeProperty(node, key);
            EditorGUILayout.LabelField(label);
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.TextArea(current, GUILayout.MinHeight(minHeight));
            if (!EditorGUI.EndChangeCheck())
                return false;

            PungentBoardEventSequenceUtility.SetNodeProperty(node, key, next);
            return true;
        }

        private static bool DrawEventSequenceFloatField(PungentBoardNode node, string key, string label, float defaultValue, float minValue)
        {
            float current;
            if (!float.TryParse(PungentBoardEventSequenceUtility.GetNodeProperty(node, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out current))
                current = defaultValue;

            EditorGUI.BeginChangeCheck();
            float next = Mathf.Max(minValue, EditorGUILayout.FloatField(label, current));
            if (!EditorGUI.EndChangeCheck())
                return false;

            PungentBoardEventSequenceUtility.SetNodeProperty(node, key, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        private static bool DrawEventSequenceEnumField(PungentBoardNode node, string key, string label, string[] options)
        {
            string current = PungentBoardEventSequenceUtility.GetNodeProperty(node, key);
            int currentIndex = Mathf.Max(0, Array.FindIndex(options, option => string.Equals(option, current, StringComparison.OrdinalIgnoreCase)));
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(label, currentIndex, options);
            if (!EditorGUI.EndChangeCheck() || nextIndex < 0 || nextIndex >= options.Length)
                return false;

            PungentBoardEventSequenceUtility.SetNodeProperty(node, key, options[nextIndex]);
            return true;
        }

        private static bool DrawEventSequenceEdgeTextField(PungentBoardEdge edge, string key, string label)
        {
            string current = PungentBoardEventSequenceUtility.GetEdgeProperty(edge, key);
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.TextField(label, current);
            if (!EditorGUI.EndChangeCheck())
                return false;

            PungentBoardEventSequenceUtility.SetEdgeProperty(edge, key, next);
            return true;
        }

        private static bool DrawEventSequenceEdgeFloatField(PungentBoardEdge edge, string key, string label, float defaultValue, float minValue)
        {
            float current;
            if (!float.TryParse(PungentBoardEventSequenceUtility.GetEdgeProperty(edge, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out current))
                current = defaultValue;

            EditorGUI.BeginChangeCheck();
            float next = Mathf.Max(minValue, EditorGUILayout.FloatField(label, current));
            if (!EditorGUI.EndChangeCheck())
                return false;

            PungentBoardEventSequenceUtility.SetEdgeProperty(edge, key, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        private static bool DrawBindingTargetFillButton(PungentBoardNode node, string propertyKey, string label)
        {
            PungentAuthoringTarget target = FindProjectBindingTarget(node);
            bool canUse = target != null && target.HasTarget;
            using (new EditorGUI.DisabledScope(!canUse))
            {
                if (!GUILayout.Button(new GUIContent(label, canUse ? "Copy the current project binding label/path into this sequence field." : "Bind a project endpoint below before using it here."), GUILayout.Height(20f)))
                    return false;
            }

            PungentBoardEventSequenceUtility.SetNodeProperty(node, propertyKey, BindingTargetDisplay(target));
            return true;
        }

        private static string BindingTargetDisplay(PungentAuthoringTarget target)
        {
            if (target == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(target.label))
                return target.label;
            if (!string.IsNullOrWhiteSpace(target.propertyPath))
                return target.propertyPath;
            return target.rawValue ?? string.Empty;
        }

        private static void DrawSchemaProperties(string title, List<PungentBoardGraphPropertyValue> values, List<PungentBoardGraphPropertyDefinition> definitions, ref bool changed)
        {
            values = values ?? new List<PungentBoardGraphPropertyValue>();
            List<PungentBoardGraphPropertyDefinition> schema = (definitions ?? new List<PungentBoardGraphPropertyDefinition>()).Where(definition => definition != null).ToList();
            if (schema.Count == 0 && values.Count == 0)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (schema.Count == 0)
            {
                EditorGUILayout.HelpBox("This item has custom stored fields, but its graph schema/type is missing or no longer defines them. Values are preserved.", MessageType.Info);
            }

            foreach (PungentBoardGraphPropertyDefinition definition in schema)
            {
                PungentBoardGraphPropertyValue value = PungentBoardGraphPropertyValue.FindOrCreate(values, definition);
                if (value == null)
                    continue;

                EditorGUI.BeginChangeCheck();
                DrawPropertyField(definition, value);
                if (EditorGUI.EndChangeCheck())
                {
                    value.NormalizeInPlace();
                    changed = true;
                }
            }

            foreach (PungentBoardGraphPropertyValue customValue in values.Where(value => value != null && schema.All(definition => !string.Equals(definition.key, value.key, StringComparison.OrdinalIgnoreCase))).ToList())
            {
                EditorGUI.BeginChangeCheck();
                customValue.key = EditorGUILayout.TextField("Custom Key", customValue.key);
                customValue.value = EditorGUILayout.TextField("Custom Value", customValue.value);
                if (EditorGUI.EndChangeCheck())
                {
                    customValue.NormalizeInPlace();
                    changed = true;
                }
            }
        }

        private static void DrawPropertyField(PungentBoardGraphPropertyDefinition definition, PungentBoardGraphPropertyValue value)
        {
            GUIContent label = new GUIContent(definition.displayName + (definition.required ? " *" : string.Empty), definition.description);
            switch (definition.valueKind)
            {
                case PungentBoardGraphPropertyValueKind.LongText:
                case PungentBoardGraphPropertyValueKind.Json:
                    EditorGUILayout.LabelField(label);
                    value.value = EditorGUILayout.TextArea(value.value, GUILayout.MinHeight(definition.valueKind == PungentBoardGraphPropertyValueKind.Json ? 54f : 42f));
                    break;
                case PungentBoardGraphPropertyValueKind.Number:
                    float number;
                    if (!float.TryParse(value.value, out number))
                        number = 0f;
                    value.value = EditorGUILayout.FloatField(label, number).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case PungentBoardGraphPropertyValueKind.Boolean:
                    bool boolValue = string.Equals(value.value, "true", StringComparison.OrdinalIgnoreCase);
                    value.value = EditorGUILayout.Toggle(label, boolValue) ? "true" : "false";
                    break;
                case PungentBoardGraphPropertyValueKind.Enum:
                    List<string> options = definition.options ?? new List<string>();
                    if (options.Count == 0)
                    {
                        value.value = EditorGUILayout.TextField(label, value.value);
                        break;
                    }

                    int currentIndex = Mathf.Max(0, options.FindIndex(option => string.Equals(option, value.value, StringComparison.OrdinalIgnoreCase)));
                    int nextIndex = EditorGUILayout.Popup(label, currentIndex, options.ToArray());
                    if (nextIndex >= 0 && nextIndex < options.Count)
                        value.value = options[nextIndex];
                    break;
                default:
                    value.value = EditorGUILayout.TextField(label, value.value);
                    break;
            }
        }

        private static void ApplyEdgeRule(PungentBoardEdge edge, PungentBoardEdgeRuleDefinition rule)
        {
            if (edge == null || rule == null)
                return;

            edge.edgeTypeKey = rule.typeKey;
            edge.edgeKind = rule.edgeKind;
            edge.styleKey = rule.styleKey;
            edge.directed = rule.directed;
            edge.fromPortKey = rule.fromPortKey;
            edge.toPortKey = rule.toPortKey;
            if (string.IsNullOrWhiteSpace(edge.label))
                edge.label = rule.displayName;
            edge.properties = edge.properties ?? new List<PungentBoardGraphPropertyValue>();
            PungentBoardCanvasGUI.EnsurePropertyValues(edge.properties, rule.propertyDefinitions);
        }

        private static void DrawProjectionSource(PungentBoardNode node)
        {
            bool hasSource = !string.IsNullOrWhiteSpace(node.integrationSourceKey) ||
                             !string.IsNullOrWhiteSpace(node.integrationSourceAdapterId) ||
                             !string.IsNullOrWhiteSpace(node.syncState);
            if (!hasSource)
                return;

            EditorGUILayout.LabelField("Projection Source", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Adapter", node.integrationSourceAdapterId);
                EditorGUILayout.TextField("Source Key", node.integrationSourceKey);
                EditorGUILayout.TextField("Source Label", node.integrationSourceLabel);
                EditorGUILayout.TextField("Sync State", node.syncState);
            }
        }

        private static void DrawProjectBinding(PungentBoardNode node, ref bool changed)
        {
            if (node == null)
                return;

            node.targets = node.targets ?? new List<PungentAuthoringTarget>();
            EditorGUILayout.LabelField("Bound Field", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            node.applyMode = DrawApplyModePopup("Apply Mode", node.applyMode);
            if (EditorGUI.EndChangeCheck())
                changed = true;

            PungentAuthoringTarget currentTarget = FindProjectBindingTarget(node);
            if (currentTarget != null && currentTarget.HasTarget)
            {
                PungentAuthoringBindingLink currentLink = CreateNodeBindingLink(node, currentTarget);
                PungentAuthoringBindingLinkPreview bridgePreview = PungentAuthoringBindingBridgeService.PreviewLink(currentLink);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Target", string.IsNullOrWhiteSpace(currentTarget.label) ? currentTarget.rawValue : currentTarget.label);
                    EditorGUILayout.TextField("Property Path", currentTarget.propertyPath);
                }

                PungentAuthoringBindingPreview preview = bridgePreview.endpointPreview;
                if (preview != null && !string.IsNullOrWhiteSpace(preview.disabledReason))
                    EditorGUILayout.HelpBox(preview.disabledReason, preview.canRead ? MessageType.Info : MessageType.Warning);
                if (preview != null && !string.IsNullOrWhiteSpace(preview.warning))
                    EditorGUILayout.LabelField(preview.warning, EditorStyles.miniLabel);
                if (preview != null && preview.canRead)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField(new GUIContent("Current Value", "Read from the bound project target through the shared Authoring binding service."), preview.currentValue ?? string.Empty);
                }

                string applyReason;
                bool canApply = PungentAuthoringBindingBridgeService.CanApply(currentLink, out applyReason);
                EditorGUILayout.LabelField("Status", BindingStatus(bridgePreview, canApply, applyReason), EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Copy Path", "Copy the binding target context and property path."), GUILayout.Width(82f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BindingTargetCopyText(currentTarget);
                        SetProjectBindingMessage(node, "Copied binding target.");
                    }

                    using (new EditorGUI.DisabledScope(preview == null || !preview.canRead))
                    {
                        if (GUILayout.Button(new GUIContent("Read Field", "Copy the current field value into this node body."), GUILayout.Width(84f)))
                        {
                            node.body = preview.currentValue ?? string.Empty;
                            changed = true;
                            SetProjectBindingMessage(node, "Read field value into node body.");
                        }
                    }

                    using (new EditorGUI.DisabledScope(!canApply))
                    {
                        if (GUILayout.Button(new GUIContent("Apply Body", string.IsNullOrWhiteSpace(applyReason) ? "Apply this node body to the bound project field." : applyReason), GUILayout.Width(92f)))
                        {
                            PungentAuthoringBindingApplyResult applyResult = PungentAuthoringBindingBridgeService.ApplyLink(currentLink, NodeBindingValue(node), "Apply Board Node Binding");
                            SetProjectBindingMessage(node, applyResult == null ? "Binding apply did not return a result." : applyResult.message);
                        }
                    }

                    if (GUILayout.Button(new GUIContent("Clear", "Remove the project binding target from this node. The node remains visible."), GUILayout.Width(58f)))
                    {
                        ClearProjectBindingTarget(node);
                        changed = true;
                        SetProjectBindingMessage(node, "Cleared project binding.");
                    }

                    GUILayout.FlexibleSpace();
                }

                if (!canApply && !string.IsNullOrWhiteSpace(applyReason))
                    EditorGUILayout.HelpBox(applyReason, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Choose a target object, component, and field to bind this node body.", MessageType.Info);
            }

            string message = GetProjectBindingMessage(node);
            if (!string.IsNullOrWhiteSpace(message))
                EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);

            PungentAuthoringGuidedBindingState pickerState = GetProjectBindingPickerState(node);
            PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
            {
                contextLabel = "Choose Field",
                objectLabel = "Target Object",
                componentLabel = "Component",
                endpointLabel = "Field",
                bindButtonLabel = "Bind Field",
                helpText = string.Empty,
                showHelp = false,
                showBindButton = false
            };
            PungentAuthoringGuidedBindingResult result = PungentAuthoringGuidedBindingView.Draw(pickerState, options);
            PungentAuthoringBindingEndpoint endpoint = result.selectedEndpoint;
            if (endpoint != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!endpoint.CanBind))
                    {
                        if (endpoint.CanBind && (result.bindClicked || GUILayout.Button(new GUIContent("Bind Field", endpoint.CanBind ? "Store this field as the node binding." : endpoint.disabledReason), GUILayout.Width(92f))))
                        {
                            SetProjectBindingTarget(node, endpoint);
                            changed = true;
                            SetProjectBindingMessage(node, "Bound field to node.");
                        }
                    }

                    if (PungentAuthoringBindingValueCodec.IsUnityObjectFacing(PungentAuthoringBindingDiscoveryService.ToRuntimeValueType(endpoint)))
                        GUILayout.Label("Object refs are read-only here; use relation mappings for edges.", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void FlushPendingBodyAutoApply(PungentBoardNode node, string bodyControlName)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
                return;
            if (!PendingBodyAutoApplyNodeIds.Contains(node.id))
                return;
            if (string.Equals(GUI.GetNameOfFocusedControl(), bodyControlName, StringComparison.Ordinal))
                return;

            PendingBodyAutoApplyNodeIds.Remove(node.id);
            if (node.applyMode == PungentAuthoringBindingApplyMode.ManualApply)
                return;
            if (node.applyMode == PungentAuthoringBindingApplyMode.AutoApplyEditMode && EditorApplication.isPlaying)
            {
                SetProjectBindingMessage(node, "Auto apply waits for Edit Mode.");
                return;
            }

            PungentAuthoringTarget target = FindProjectBindingTarget(node);
            if (target == null || !target.HasTarget)
            {
                SetProjectBindingMessage(node, "Auto apply skipped: no bound field.");
                return;
            }

            PungentAuthoringBindingLink link = CreateNodeBindingLink(node, target);
            PungentAuthoringBindingLinkPreview preview = PungentAuthoringBindingBridgeService.PreviewLink(link);
            if (preview != null && preview.canRead && preview.endpointPreview != null &&
                string.Equals(preview.endpointPreview.currentValue ?? string.Empty, NodeBindingValue(node), StringComparison.Ordinal))
            {
                SetProjectBindingMessage(node, "Bound field already matches body.");
                return;
            }

            if (!PungentAuthoringBindingBridgeService.CanApply(link, out string reason))
            {
                SetProjectBindingMessage(node, string.IsNullOrWhiteSpace(reason) ? "Auto apply skipped." : reason);
                return;
            }

            PungentAuthoringBindingApplyResult result = PungentAuthoringBindingBridgeService.ApplyLink(link, NodeBindingValue(node), "Apply Board Node Binding");
            SetProjectBindingMessage(node, result != null && result.applied
                ? "Auto applied body to bound field."
                : result == null || string.IsNullOrWhiteSpace(result.message) ? "Auto apply skipped." : result.message);
        }

        private static string BindingStatus(PungentAuthoringBindingLinkPreview preview, bool canApply, string applyReason)
        {
            if (preview == null)
                return "No preview.";
            if (!string.IsNullOrWhiteSpace(preview.disabledReason))
                return "Blocked: " + preview.disabledReason;
            if (!canApply && !string.IsNullOrWhiteSpace(applyReason))
                return "Blocked: " + applyReason;
            if (preview.canRead && canApply)
                return "Ready: reads and writes";
            if (preview.canRead)
                return "Ready: reads only";
            if (canApply)
                return "Ready: writes only";
            return "Blocked: field cannot read or write.";
        }

        private static PungentAuthoringBindingApplyMode DrawApplyModePopup(string label, PungentAuthoringBindingApplyMode value)
        {
            PungentAuthoringBindingApplyMode[] values =
            {
                PungentAuthoringBindingApplyMode.ManualApply,
                PungentAuthoringBindingApplyMode.AutoApplyEditMode,
                PungentAuthoringBindingApplyMode.AutoApplyAlways
            };
            string[] labels = { "Manual Apply", "Auto in Edit Mode", "Auto in Edit + Play" };
            int index = Mathf.Clamp(Array.IndexOf(values, value), 0, values.Length - 1);
            return values[Mathf.Clamp(EditorGUILayout.Popup(label, index, labels), 0, values.Length - 1)];
        }

        private static PungentAuthoringTarget FindProjectBindingTarget(PungentBoardNode node)
        {
            if (node == null || node.targets == null)
                return null;

            return node.targets.FirstOrDefault(target =>
                target != null &&
                string.Equals(target.sourceContext, BoardGraphNodeBindingSourceContext, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetProjectBindingTarget(PungentBoardNode node, PungentAuthoringBindingEndpoint endpoint)
        {
            if (node == null || endpoint == null || endpoint.target == null)
                return;

            node.targets = node.targets ?? new List<PungentAuthoringTarget>();
            ClearProjectBindingTarget(node);
            PungentAuthoringTarget target = CloneTarget(endpoint.target);
            target.sourceContext = BoardGraphNodeBindingSourceContext;
            if (string.IsNullOrWhiteSpace(target.label))
                target.label = endpoint.label;
            if (string.IsNullOrWhiteSpace(target.customKind))
                target.customKind = endpoint.adapterId;
            target.NormalizeInPlace();
            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromEndpoint(
                endpoint,
                "board-graph",
                string.Empty,
                node.id,
                "node.body",
                PungentAuthoringBindingLinkDirection.TwoWay);
            node.projectBindingLinkId = link.id;
            node.projectBindingAdapterId = endpoint.adapterId;
            node.projectBindingEndpointId = endpoint.id;
            node.projectBindingValueType = link.valueType;
            node.projectBindingPath = link.bindingPath;
            node.projectBindingSlot = link.bindingSlot;
            node.projectBindingSlot.role = PungentAuthoringBindingSlotRole.BoardNode;
            node.projectBindingSlot.elementId = node.id;
            node.projectBindingSlot.fieldKey = "node.body";
            node.projectBindingSlot.NormalizeInPlace();
            node.targets.Add(target);
        }

        private static void ClearProjectBindingTarget(PungentBoardNode node)
        {
            if (node == null)
                return;

            if (node.targets != null)
            {
                node.targets.RemoveAll(target =>
                    target != null &&
                    string.Equals(target.sourceContext, BoardGraphNodeBindingSourceContext, StringComparison.OrdinalIgnoreCase));
            }
            node.projectBindingLinkId = string.Empty;
            node.projectBindingAdapterId = string.Empty;
            node.projectBindingEndpointId = string.Empty;
            node.projectBindingValueType = PungentAuthoringBindingValueType.Unknown;
            node.projectBindingPath = new PungentAuthoringBindingPath();
            node.projectBindingSlot = new PungentAuthoringBindingSlot
            {
                role = PungentAuthoringBindingSlotRole.BoardNode,
                elementId = node.id,
                fieldKey = "node.body"
            };
            node.projectBindingSlot.NormalizeInPlace();
        }

        private static PungentAuthoringBindingLink CreateNodeBindingLink(PungentBoardNode node, PungentAuthoringTarget target)
        {
            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromTarget(
                target,
                "board-graph",
                string.Empty,
                node == null ? string.Empty : node.id,
                "node.body",
                PungentAuthoringBindingLinkDirection.TwoWay,
                node == null ? string.Empty : node.projectBindingAdapterId,
                string.Empty,
                node == null ? string.Empty : node.projectBindingEndpointId,
                target == null ? string.Empty : target.label,
                node == null ? PungentAuthoringBindingValueType.Unknown : node.projectBindingValueType);

            if (node != null && !string.IsNullOrWhiteSpace(node.projectBindingLinkId))
                link.id = node.projectBindingLinkId;
            if (node != null)
            {
                if (node.projectBindingPath != null && node.projectBindingPath.HasPath)
                {
                    link.bindingPath = node.projectBindingPath;
                    link.bindingPath.rootTarget = CloneTarget(target);
                    link.bindingPath.valueType = node.projectBindingValueType == PungentAuthoringBindingValueType.Unknown ? link.bindingPath.valueType : node.projectBindingValueType;
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
                link.notes = "Exported from a BoardGraph node project binding. Runtime execution requires a project adapter.";
            }

            link.NormalizeInPlace();
            return link;
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

        private static PungentAuthoringGuidedBindingState GetProjectBindingPickerState(PungentBoardNode node)
        {
            // RDE/GUIDED-BINDING MIGRATION NOTE: node project bindings use Authoring targets, not a BoardGraph-specific picker.
            string key = node == null || string.IsNullOrWhiteSpace(node.id) ? "default" : node.id;
            PungentAuthoringGuidedBindingState state;
            if (!ProjectBindingPickerStates.TryGetValue(key, out state) || state == null)
            {
                state = new PungentAuthoringGuidedBindingState
                {
                    targetObject = Selection.activeObject
                };
                ProjectBindingPickerStates[key] = state;
            }

            return state;
        }

        private static string BindingTargetCopyText(PungentAuthoringTarget target)
        {
            if (target == null)
                return string.Empty;

            return target.targetKind + "|" + target.contextId + "|" + target.rawValue + "|" + target.propertyPath;
        }

        private static string NodeBindingValue(PungentBoardNode node)
        {
            if (node == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(node.body))
                return node.body;
            if (!string.IsNullOrWhiteSpace(node.summary))
                return node.summary;
            return node.title ?? string.Empty;
        }

        private static void SetProjectBindingMessage(PungentBoardNode node, string message)
        {
            string key = node == null || string.IsNullOrWhiteSpace(node.id) ? "default" : node.id;
            ProjectBindingMessages[key] = message ?? string.Empty;
        }

        private static string GetProjectBindingMessage(PungentBoardNode node)
        {
            string key = node == null || string.IsNullOrWhiteSpace(node.id) ? "default" : node.id;
            string message;
            return ProjectBindingMessages.TryGetValue(key, out message) ? message : string.Empty;
        }

        private static void DrawLinkedAuthoringReference(PungentBoardDocument document, PungentBoardNode node, ref bool changed)
        {
            node.linkedAuthoringRef = node.linkedAuthoringRef ?? new PungentAuthoringReference();
            PungentAuthoringReference reference = node.linkedAuthoringRef;

            EditorGUILayout.LabelField("Linked Authoring Item", EditorStyles.boldLabel);
            DrawLinkedActionRow(reference);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Set Ref From Node", "Set linked item kind/provider based on this node's current kind."), GUILayout.Width(124f)))
                {
                    ApplyKindDefaults(node);
                    changed = true;
                }

                using (new EditorGUI.DisabledScope(!CanMatchNodeToReference(reference)))
                {
                    if (GUILayout.Button(new GUIContent("Match Node To Ref", "Change this node's kind/type to suit the linked authoring item kind."), GUILayout.Width(126f)))
                        changed = ApplyNodeKindFromReference(document, node) || changed;
                }

                using (new EditorGUI.DisabledScope(!CanSyncFromLinkedItem(reference)))
                {
                    if (GUILayout.Button(new GUIContent("Sync From Linked", "Fill the node title, summary, and body preview from the linked provider preview."), GUILayout.Width(118f)))
                        changed = SyncNodeFromLinkedItem(node) || changed;
                }

                if (GUILayout.Button(new GUIContent("Clear", "Remove the linked authoring reference from this node."), GUILayout.Width(58f)))
                {
                    node.linkedAuthoringRef = new PungentAuthoringReference();
                    changed = true;
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUI.BeginChangeCheck();
            reference.itemKind = (PungentAuthoringItemKind)EditorGUILayout.EnumPopup("Item Kind", reference.itemKind);
            reference.customKind = EditorGUILayout.TextField("Custom Kind", reference.customKind);
            reference.providerId = EditorGUILayout.TextField("Provider ID", reference.providerId);
            reference.itemId = EditorGUILayout.TextField("Item ID", reference.itemId);
            reference.label = EditorGUILayout.TextField("Label", reference.label);
            reference.sourceContext = EditorGUILayout.TextField("Source Context", reference.sourceContext);
            if (EditorGUI.EndChangeCheck())
            {
                reference.NormalizeInPlace();
                changed = true;
            }

            DrawLinkKindMismatchHint(node, reference);
            EditorGUILayout.HelpBox("Use the picker or paste an ID, then choose a kind/provider. Provider previews are resolved only for this explicit reference; no provider-wide scan runs during repaint.", MessageType.None);
            PungentBoardAuthoringPickerGUI.Draw(reference, ref changed);
            DrawLinkedPreview(reference);
        }

        private static bool CanSyncFromLinkedItem(PungentAuthoringReference reference)
        {
            PungentAuthoringPreview preview;
            return reference != null &&
                   reference.HasItemId &&
                   PungentAuthoringProviderRegistry.TryGetPreview(reference, out preview) &&
                   preview != null &&
                   !preview.missing;
        }

        private static bool SyncNodeFromLinkedItem(PungentBoardNode node)
        {
            if (node == null || node.linkedAuthoringRef == null || !node.linkedAuthoringRef.HasItemId)
                return false;

            PungentAuthoringPreview preview;
            if (!PungentAuthoringProviderRegistry.TryGetPreview(node.linkedAuthoringRef, out preview) || preview == null || preview.missing)
                return false;

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(preview.title) && !string.Equals(node.title, preview.title, StringComparison.Ordinal))
            {
                node.title = preview.title.Trim();
                changed = true;
            }

            string summary = !string.IsNullOrWhiteSpace(preview.subtitle) ? preview.subtitle : preview.statusLabel;
            if (!string.IsNullOrWhiteSpace(summary) && !string.Equals(node.summary, summary, StringComparison.Ordinal))
            {
                node.summary = summary.Trim();
                changed = true;
            }

            string body = !string.IsNullOrWhiteSpace(preview.bodyPreview) ? preview.bodyPreview : preview.warningLabel;
            if (!string.IsNullOrWhiteSpace(body) && !string.Equals(node.body, body, StringComparison.Ordinal))
            {
                node.body = body.Trim();
                changed = true;
            }

            return changed;
        }

        private static void DrawLinkedActionRow(PungentAuthoringReference reference)
        {
            string openReason;
            bool canOpen = CanOpen(reference, out openReason);
            bool hasItemId = reference != null && reference.HasItemId;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!canOpen))
                {
                    if (GUILayout.Button(new GUIContent("Open Linked", string.IsNullOrWhiteSpace(openReason) ? "Open linked item through its authoring provider." : openReason), EditorStyles.toolbarButton, GUILayout.Width(94f)))
                        PungentAuthoringProviderRegistry.TryOpen(reference);
                }

                using (new EditorGUI.DisabledScope(!hasItemId))
                {
                    if (GUILayout.Button(new GUIContent("Copy ID", hasItemId ? "Copy the linked authoring item ID." : "No linked item ID to copy."), EditorStyles.toolbarButton, GUILayout.Width(66f)))
                        EditorGUIUtility.systemCopyBuffer = reference.itemId ?? string.Empty;
                }

                using (new EditorGUI.DisabledScope(!hasItemId || reference.itemKind != PungentAuthoringItemKind.Checklist))
                {
                    if (GUILayout.Button(new GUIContent("Overlay", "Open the linked checklist in the shared overlay tray."), EditorStyles.toolbarButton, GUILayout.Width(66f)))
                        PungentBoardChecklistBridge.TryOpenChecklistOverlay(reference, Rect.zero);
                }

                GUILayout.FlexibleSpace();
                if (hasItemId && PungentBoardChecklistBridge.TryGetChecklistProgress(reference, out string progress))
                    GUILayout.Label(progress, EditorStyles.miniLabel, GUILayout.Width(160f));
                else
                    GUILayout.Label(hasItemId ? "linked" : "no item id", EditorStyles.miniLabel);
            }
        }

        private static void DrawLinkedPreview(PungentAuthoringReference reference)
        {
            if (reference == null || !reference.HasItemId)
            {
                EditorGUILayout.HelpBox("Paste an existing authoring item ID, then choose its kind/provider. No project scan runs here.", MessageType.Info);
                return;
            }

            PungentAuthoringPreview preview;
            bool hasPreview = PungentAuthoringProviderRegistry.TryGetPreview(reference, out preview);
            if (preview != null)
            {
                MessageType type = preview.missing ? MessageType.Warning : preview.warning ? MessageType.Info : MessageType.None;
                if (type == MessageType.None)
                {
                    EditorGUILayout.LabelField(preview.title, EditorStyles.boldLabel);
                    if (!string.IsNullOrWhiteSpace(preview.subtitle))
                        EditorGUILayout.LabelField(preview.subtitle, EditorStyles.miniLabel);
                    if (!string.IsNullOrWhiteSpace(preview.bodyPreview))
                        EditorGUILayout.LabelField(preview.bodyPreview, EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(preview.warningLabel) ? preview.bodyPreview : preview.warningLabel, type);
                }
            }

            if (!hasPreview && preview == null)
                EditorGUILayout.HelpBox(PungentAuthoringProviderRegistry.MissingProviderMessage(reference), MessageType.Warning);

            string openReason;
            bool canOpen = CanOpen(reference, out openReason);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canOpen))
                {
                    if (GUILayout.Button(new GUIContent("Open Linked", string.IsNullOrWhiteSpace(openReason) ? "Open linked item through its authoring provider." : openReason), GUILayout.Width(96f)))
                        PungentAuthoringProviderRegistry.TryOpen(reference);
                }

                if (GUILayout.Button(new GUIContent("Copy ID", "Copy the linked authoring item ID."), GUILayout.Width(76f)))
                    EditorGUIUtility.systemCopyBuffer = reference.itemId ?? string.Empty;

                GUILayout.FlexibleSpace();
            }

            if (!canOpen && !string.IsNullOrWhiteSpace(openReason))
                EditorGUILayout.HelpBox(openReason, MessageType.Info);
        }

        private static bool CanOpen(PungentAuthoringReference reference, out string reason)
        {
            reason = string.Empty;
            if (reference == null || !reference.HasItemId)
            {
                reason = "No linked item ID.";
                return false;
            }

            IReadOnlyList<IPungentAuthoringProvider> providers = GetCandidateProviders(reference);
            if (providers.Count == 0)
            {
                reason = PungentAuthoringProviderRegistry.MissingProviderMessage(reference);
                return false;
            }

            foreach (IPungentAuthoringProvider provider in providers)
            {
                IPungentAuthoringEditorLauncher launcher = provider as IPungentAuthoringEditorLauncher;
                if (launcher != null && launcher.CanOpen(reference, out reason))
                    return true;
            }

            if (string.IsNullOrWhiteSpace(reason))
                reason = "The installed provider does not expose an open action for this item.";
            return false;
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

        private static bool CanMatchNodeToReference(PungentAuthoringReference reference)
        {
            return reference != null &&
                   (reference.itemKind != PungentAuthoringItemKind.Unknown || !string.IsNullOrWhiteSpace(reference.providerId));
        }

        private static void DrawLinkKindMismatchHint(PungentBoardNode node, PungentAuthoringReference reference)
        {
            if (node == null || reference == null || !CanMatchNodeToReference(reference))
                return;

            PungentBoardNodeKind expectedKind = NodeKindForReference(reference);
            if (expectedKind == node.nodeKind)
                return;

            EditorGUILayout.HelpBox("Linked item kind suggests a " + expectedKind + " node. Use Match Node To Ref to align the node without changing the linked item ID.", MessageType.Info);
        }

        private static bool ApplyNodeKindFromReference(PungentBoardDocument document, PungentBoardNode node)
        {
            if (node == null || node.linkedAuthoringRef == null)
                return false;

            PungentBoardNodeKind nextKind = NodeKindForReference(node.linkedAuthoringRef);
            string nextTypeKey = NodeTypeKeyForKind(nextKind);
            bool changed = node.nodeKind != nextKind;
            node.nodeKind = nextKind;

            PungentBoardGraphTypeDefinition graphType = document != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId) : null;
            bool graphSupportsType = graphType != null && graphType.FindNodeType(nextTypeKey) != null;
            if (graphSupportsType && !string.Equals(node.nodeTypeKey, nextTypeKey, StringComparison.OrdinalIgnoreCase))
            {
                node.nodeTypeKey = nextTypeKey;
                node.colorStyleKey = PungentBoardNode.GetDefaultStyleKey(nextKind);
                changed = true;
            }
            else if (string.IsNullOrWhiteSpace(node.colorStyleKey))
            {
                node.colorStyleKey = PungentBoardNode.GetDefaultStyleKey(nextKind);
                changed = true;
            }

            node.NormalizeInPlace();
            return changed;
        }

        private static PungentBoardNodeKind NodeKindForReference(PungentAuthoringReference reference)
        {
            if (reference == null)
                return PungentBoardNodeKind.NoteCard;

            switch (reference.itemKind)
            {
                case PungentAuthoringItemKind.RichDocument:
                case PungentAuthoringItemKind.DataSheet:
                    return PungentBoardNodeKind.DocumentReference;
                case PungentAuthoringItemKind.Utility:
                case PungentAuthoringItemKind.FutureUtility:
                    return PungentBoardNodeKind.UtilityReference;
                case PungentAuthoringItemKind.TokenDefinition:
                    return PungentBoardNodeKind.TokenReference;
                case PungentAuthoringItemKind.DocumentationLink:
                case PungentAuthoringItemKind.HelpTopic:
                    return PungentBoardNodeKind.DocumentationLink;
                case PungentAuthoringItemKind.AuditIssue:
                    return PungentBoardNodeKind.AuditFinding;
                case PungentAuthoringItemKind.Task:
                case PungentAuthoringItemKind.Checklist:
                    return PungentBoardNodeKind.Task;
                case PungentAuthoringItemKind.Board:
                    return PungentBoardNodeKind.FreeformCard;
                default:
                    return ProviderSuggestsUtility(reference.providerId)
                        ? PungentBoardNodeKind.UtilityReference
                        : PungentBoardNodeKind.NoteCard;
            }
        }

        private static bool ProviderSuggestsUtility(string providerId)
        {
            return !string.IsNullOrWhiteSpace(providerId) &&
                   providerId.Trim().IndexOf("utility", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NodeTypeKeyForKind(PungentBoardNodeKind kind)
        {
            switch (kind)
            {
                case PungentBoardNodeKind.DocumentReference:
                    return "document-reference";
                case PungentBoardNodeKind.UtilityReference:
                    return "utility-reference";
                case PungentBoardNodeKind.TokenReference:
                    return "token-reference";
                case PungentBoardNodeKind.DocumentationLink:
                    return "documentation-link";
                case PungentBoardNodeKind.AuditFinding:
                    return "audit-finding";
                case PungentBoardNodeKind.Task:
                    return "task";
                case PungentBoardNodeKind.FreeformCard:
                    return "freeform-card";
                default:
                    return "note-card";
            }
        }

        private static void ApplyKindDefaults(PungentBoardNode node)
        {
            node.linkedAuthoringRef = node.linkedAuthoringRef ?? new PungentAuthoringReference();
            switch (node.nodeKind)
            {
                case PungentBoardNodeKind.DocumentReference:
                    node.linkedAuthoringRef.itemKind = PungentAuthoringItemKind.RichDocument;
                    node.linkedAuthoringRef.providerId = string.Empty;
                    break;
                case PungentBoardNodeKind.UtilityReference:
                    node.linkedAuthoringRef.itemKind = PungentAuthoringItemKind.Utility;
                    node.linkedAuthoringRef.providerId = "utility-registry";
                    break;
                case PungentBoardNodeKind.TokenReference:
                    node.linkedAuthoringRef.itemKind = PungentAuthoringItemKind.TokenDefinition;
                    node.linkedAuthoringRef.providerId = "token-definitions";
                    break;
                case PungentBoardNodeKind.DocumentationLink:
                    node.linkedAuthoringRef.itemKind = PungentAuthoringItemKind.DocumentationLink;
                    node.linkedAuthoringRef.providerId = "documentation-links";
                    break;
                case PungentBoardNodeKind.AuditFinding:
                    node.linkedAuthoringRef.itemKind = PungentAuthoringItemKind.AuditIssue;
                    node.linkedAuthoringRef.providerId = "cached-audit-issues";
                    break;
                case PungentBoardNodeKind.Task:
                    node.linkedAuthoringRef.itemKind = PungentAuthoringItemKind.Task;
                    node.linkedAuthoringRef.providerId = "legacy-notes";
                    break;
                default:
                    // RDE/STICKY-NOTES MIGRATION NOTE: Board note-card references remain LegacyNote-compatible, but richer notebook writing belongs in Rich Documents.
                    node.linkedAuthoringRef.itemKind = PungentAuthoringItemKind.LegacyNote;
                    node.linkedAuthoringRef.providerId = "legacy-notes";
                    break;
            }

            node.linkedAuthoringRef.NormalizeInPlace();
        }

        private static void DrawNodeEndpointPopup(PungentBoardDocument document, string label, ref string nodeId)
        {
            List<PungentBoardNode> nodes = (document.nodes ?? new List<PungentBoardNode>()).Where(node => node != null).ToList();
            string currentNodeId = nodeId;
            int nodeIndex = nodes.FindIndex(node => PungentAuthoringId.EqualsId(node.id, currentNodeId));
            string[] labels = new string[nodes.Count + 1];
            labels[0] = string.IsNullOrWhiteSpace(currentNodeId) ? "(None)" : "(Missing: " + currentNodeId + ")";
            for (int i = 0; i < nodes.Count; i++)
                labels[i + 1] = string.IsNullOrWhiteSpace(nodes[i].title) ? nodes[i].id : nodes[i].title;

            int index = nodeIndex >= 0 ? nodeIndex + 1 : 0;
            int next = EditorGUILayout.Popup(label, index, labels.Length == 1 ? new[] { labels[0] } : labels);
            if (next > 0 && next - 1 < nodes.Count)
                nodeId = nodes[next - 1].id;
            else if (next == 0 && nodeIndex >= 0)
                nodeId = string.Empty;
        }

        private static void DrawTagsField(ref List<string> tags, ref bool changed)
        {
            tags = tags ?? new List<string>();
            string joined = string.Join(", ", tags.ToArray());
            EditorGUI.BeginChangeCheck();
            joined = EditorGUILayout.TextField("Tags", joined);
            if (EditorGUI.EndChangeCheck())
            {
                tags.Clear();
                tags.AddRange(PungentAuthoringMetadata.NormalizeTags((joined ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)));
                changed = true;
            }
        }
    }
#endif
}
