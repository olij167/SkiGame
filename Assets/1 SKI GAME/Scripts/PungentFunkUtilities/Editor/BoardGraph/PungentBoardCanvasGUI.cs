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
    public sealed class PungentBoardCanvasViewState
    {
        public Vector2 pan = Vector2.zero;
        public float zoom = 1f;
        public string selectedNodeId = string.Empty;
        public string selectedEdgeId = string.Empty;
        public string selectedGroupId = string.Empty;
        public readonly List<string> selectedNodeIds = new List<string>();
        public readonly List<string> selectedGroupIds = new List<string>();
        public bool connectorMode;
        public string connectorSourceNodeId = string.Empty;
        public string connectorSourcePortKey = string.Empty;
        public string connectorHoverNodeId = string.Empty;
        public string connectorHoverPortKey = string.Empty;
        public bool connectorHoverValid;
        public bool connectorDragging;
        public PungentBoardNodeKind defaultNodeKind = PungentBoardNodeKind.NoteCard;
        public string defaultNodeTypeKey = string.Empty;
        public string defaultEdgeTypeKey = string.Empty;
        public string connectionWarning = string.Empty;
        public bool showGrid = true;
        public bool snapToGrid;
        public float snapSize = 24f;
        public float gridMinorSpacing = 24f;
        public int gridMajorLineFrequency = 5;
        public float gridOpacity = 1f;
        public bool snapNodes = true;
        public bool snapGroups = true;
        public bool snapResize = true;
        public bool contentChanged;
        public bool viewChanged;
        public bool deleteRequested;
        public bool repaintRequested;
        public readonly PungentBoardInlineNodeEditState inlineEdit = new PungentBoardInlineNodeEditState();
        public Action<PungentBoardGraphCommand, string> commandRequested;
        public Action<PungentBoardGroup> saveGroupTemplateRequested;
        public Action<string, Vector2> instantiateCustomGroupTemplateRequested;
        public string pendingHistoryLabel = string.Empty;
        public string pendingHistorySnapshotJson = string.Empty;
        internal string draggingNodeId = string.Empty;
        internal string draggingGroupId = string.Empty;
        internal string resizingNodeId = string.Empty;
        internal string resizingGroupId = string.Empty;
        internal Vector2 dragStartMouse;
        internal Vector2 dragStartPosition;
        internal Rect dragStartRect;
        internal Vector2 resizeStartSize;
        internal readonly Dictionary<string, Vector2> dragStartNodePositions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, Rect> dragStartGroupRects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
        internal bool panning;
        internal Vector2 panStartMouse;
        internal Vector2 panStartValue;
        internal bool marqueeSelecting;
        internal Vector2 marqueeStartCanvas;
        internal Vector2 marqueeCurrentCanvas;

        public void SelectNode(string nodeId)
        {
            selectedNodeId = PungentAuthoringId.Normalize(nodeId);
            selectedEdgeId = string.Empty;
            selectedGroupId = string.Empty;
            selectedNodeIds.Clear();
            selectedGroupIds.Clear();
            if (!string.IsNullOrWhiteSpace(selectedNodeId))
                selectedNodeIds.Add(selectedNodeId);
        }

        public void SelectEdge(string edgeId)
        {
            selectedNodeId = string.Empty;
            selectedEdgeId = PungentAuthoringId.Normalize(edgeId);
            selectedGroupId = string.Empty;
            selectedNodeIds.Clear();
            selectedGroupIds.Clear();
        }

        public void SelectGroup(string groupId)
        {
            selectedNodeId = string.Empty;
            selectedEdgeId = string.Empty;
            selectedGroupId = PungentAuthoringId.Normalize(groupId);
            selectedNodeIds.Clear();
            selectedGroupIds.Clear();
            if (!string.IsNullOrWhiteSpace(selectedGroupId))
                selectedGroupIds.Add(selectedGroupId);
        }

        public void ClearSelection()
        {
            selectedNodeId = string.Empty;
            selectedEdgeId = string.Empty;
            selectedGroupId = string.Empty;
            selectedNodeIds.Clear();
            selectedGroupIds.Clear();
        }

        public void ToggleNodeSelection(string nodeId)
        {
            string clean = PungentAuthoringId.Normalize(nodeId);
            selectedEdgeId = string.Empty;
            selectedGroupId = string.Empty;
            if (string.IsNullOrWhiteSpace(clean))
                return;

            int index = selectedNodeIds.FindIndex(id => PungentAuthoringId.EqualsId(id, clean));
            if (index >= 0)
                selectedNodeIds.RemoveAt(index);
            else
                selectedNodeIds.Add(clean);
            selectedNodeId = selectedNodeIds.Count > 0 ? selectedNodeIds[selectedNodeIds.Count - 1] : string.Empty;
        }

        public void ToggleGroupSelection(string groupId)
        {
            string clean = PungentAuthoringId.Normalize(groupId);
            selectedEdgeId = string.Empty;
            selectedNodeId = string.Empty;
            if (string.IsNullOrWhiteSpace(clean))
                return;

            int index = selectedGroupIds.FindIndex(id => PungentAuthoringId.EqualsId(id, clean));
            if (index >= 0)
                selectedGroupIds.RemoveAt(index);
            else
                selectedGroupIds.Add(clean);
            selectedGroupId = selectedGroupIds.Count > 0 ? selectedGroupIds[selectedGroupIds.Count - 1] : string.Empty;
        }

        public bool IsNodeSelected(string nodeId)
        {
            return selectedNodeIds.Any(id => PungentAuthoringId.EqualsId(id, nodeId)) ||
                   PungentAuthoringId.EqualsId(selectedNodeId, nodeId);
        }

        public bool IsGroupSelected(string groupId)
        {
            return selectedGroupIds.Any(id => PungentAuthoringId.EqualsId(id, groupId)) ||
                   PungentAuthoringId.EqualsId(selectedGroupId, groupId);
        }

        public int SelectedNodeCount => selectedNodeIds.Count;
        public int SelectedGroupCount => selectedGroupIds.Count;
        public int MultiSelectionCount => selectedNodeIds.Count + selectedGroupIds.Count;

        public void ClearPendingHistory()
        {
            pendingHistoryLabel = string.Empty;
            pendingHistorySnapshotJson = string.Empty;
        }

        public bool HasSelection => !string.IsNullOrWhiteSpace(selectedNodeId) ||
                                    !string.IsNullOrWhiteSpace(selectedEdgeId) ||
                                    !string.IsNullOrWhiteSpace(selectedGroupId) ||
                                    selectedNodeIds.Count > 0 ||
                                    selectedGroupIds.Count > 0;
    }

    public static class PungentBoardCanvasGUI
    {
        private const float MinZoom = 0.10f;
        private const float MaxZoom = 2.4f;
        private const float GridMajor = 120f;
        private const float GridMinor = 24f;
        private const float ResizeHandleSize = 12f;
        private const float CollapseButtonSize = 18f;
        private const float PortSize = 12f;
        private const float SimplifiedZoom = 0.36f;
        private const float BodyTextZoom = 0.52f;
        private const float PortLabelZoom = 0.68f;
        private static GUIStyle _nodeTitleStyle;
        private static GUIStyle _nodeBodyStyle;
        private static GUIStyle _nodeBadgeStyle;
        private static GUIStyle _nodePortLabelStyle;
        private static GUIStyle _edgeLabelStyle;
        private static GUIStyle _groupTitleStyle;

        private struct PungentBoardNodeVisualLayout
        {
            public bool showMeta;
            public bool showBody;
            public bool showPortLabels;
            public bool hasInputLabels;
            public bool hasOutputLabels;
            public bool showMissingStatus;
            public float headerHeight;
            public float inputLabelWidth;
            public float outputLabelWidth;
            public Rect headerRect;
            public Rect dividerRect;
            public Rect titleRect;
            public Rect collapseRect;
            public Rect typeRect;
            public Rect bodyRect;
            public Rect statusRect;
            public Rect controlsRect;
        }

        private sealed class PungentBoardPortHit
        {
            public PungentBoardNode node;
            public PungentBoardGroup group;
            public PungentBoardPortDefinition port;
            public PungentBoardCustomNodePortMapping mapping;
            public bool inputSide;
            public Rect localRect;

            public string PortKey => port != null ? port.key : string.Empty;
            public string EndpointNodeId => node != null ? node.id : mapping != null ? mapping.internalNodeId : string.Empty;
            public string EndpointPortKey => mapping != null && !string.IsNullOrWhiteSpace(mapping.internalPortKey) ? mapping.internalPortKey : PortKey;
            public bool CanStartEdge => !inputSide && !string.IsNullOrWhiteSpace(EndpointNodeId);
            public bool CanFinishEdge => inputSide && !string.IsNullOrWhiteSpace(EndpointNodeId);
            public Vector2 LocalCenter => localRect.center;
        }

        public static void Draw(Rect canvasRect, PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            EnsureStyles();

            EditorGUI.DrawRect(canvasRect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.13f, 0.14f) : new Color(0.82f, 0.84f, 0.86f));
            if (document == null)
            {
                GUI.Label(new Rect(canvasRect.x + 16f, canvasRect.y + 16f, canvasRect.width - 32f, 40f), "Create or select a board to begin.");
                return;
            }

            state.zoom = Mathf.Clamp(float.IsNaN(state.zoom) || float.IsInfinity(state.zoom) ? 1f : state.zoom, MinZoom, MaxZoom);
            state.snapSize = Mathf.Clamp(float.IsNaN(state.snapSize) || float.IsInfinity(state.snapSize) ? GridMinor : state.snapSize, 4f, 240f);
            state.gridMinorSpacing = Mathf.Clamp(float.IsNaN(state.gridMinorSpacing) || float.IsInfinity(state.gridMinorSpacing) ? GridMinor : state.gridMinorSpacing, 8f, 240f);
            state.gridMajorLineFrequency = Mathf.Clamp(state.gridMajorLineFrequency <= 0 ? 5 : state.gridMajorLineFrequency, 2, 12);
            state.gridOpacity = Mathf.Clamp01(float.IsNaN(state.gridOpacity) || float.IsInfinity(state.gridOpacity) ? 1f : state.gridOpacity);
            HandleInput(canvasRect, document, state);

            GUI.BeginGroup(canvasRect);
            Rect localRect = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
            if (state.showGrid)
                DrawGrid(localRect, state);
            DrawGroups(localRect, document, state);
            DrawEdges(localRect, document, state);
            DrawNodes(localRect, document, state);
            DrawConnectorHint(document, state, localRect);
            DrawMarquee(state);
            GUI.EndGroup();
        }

        public static Vector2 ScreenToCanvas(Vector2 screenPosition, Rect canvasRect, PungentBoardCanvasViewState state)
        {
            return (screenPosition - canvasRect.position - state.pan) / Mathf.Max(0.001f, state.zoom);
        }

        public static Vector2 CanvasToLocal(Vector2 canvasPosition, PungentBoardCanvasViewState state)
        {
            return state.pan + canvasPosition * state.zoom;
        }

        public static Rect CanvasToLocal(Rect canvasRect, PungentBoardCanvasViewState state)
        {
            Vector2 position = CanvasToLocal(canvasRect.position, state);
            return new Rect(position.x, position.y, canvasRect.width * state.zoom, canvasRect.height * state.zoom);
        }

        public static PungentBoardNode FindNode(PungentBoardDocument document, string nodeId)
        {
            return (document?.nodes ?? new List<PungentBoardNode>())
                .FirstOrDefault(node => node != null && PungentAuthoringId.EqualsId(node.id, nodeId));
        }

        public static PungentBoardEdge FindEdge(PungentBoardDocument document, string edgeId)
        {
            return (document?.edges ?? new List<PungentBoardEdge>())
                .FirstOrDefault(edge => edge != null && PungentAuthoringId.EqualsId(edge.id, edgeId));
        }

        public static PungentBoardGroup FindGroup(PungentBoardDocument document, string groupId)
        {
            return (document?.groups ?? new List<PungentBoardGroup>())
                .FirstOrDefault(group => group != null && PungentAuthoringId.EqualsId(group.id, groupId));
        }

        public static void FitDocument(Rect canvasRect, PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            if (document == null)
                return;

            Rect bounds;
            if (!TryGetContentBounds(document, out bounds))
            {
                state.zoom = 1f;
                state.pan = canvasRect.size * 0.5f;
                state.viewChanged = true;
                return;
            }

            float widthZoom = (canvasRect.width - 80f) / Mathf.Max(1f, bounds.width);
            float heightZoom = (canvasRect.height - 80f) / Mathf.Max(1f, bounds.height);
            state.zoom = Mathf.Clamp(Mathf.Min(widthZoom, heightZoom), MinZoom, MaxZoom);
            state.pan = canvasRect.size * 0.5f - bounds.center * state.zoom;
            state.viewChanged = true;
        }

        public static void FrameSelection(Rect canvasRect, PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            if (document == null || state == null)
                return;

            Rect bounds;
            if (!TryGetSelectionBounds(document, state, out bounds))
            {
                FitDocument(canvasRect, document, state);
                return;
            }

            float widthZoom = (canvasRect.width - 120f) / Mathf.Max(1f, bounds.width);
            float heightZoom = (canvasRect.height - 120f) / Mathf.Max(1f, bounds.height);
            state.zoom = Mathf.Clamp(Mathf.Min(widthZoom, heightZoom), MinZoom, MaxZoom);
            state.pan = canvasRect.size * 0.5f - bounds.center * state.zoom;
            state.viewChanged = true;
            state.repaintRequested = true;
        }

        public static void AlignSelection(PungentBoardDocument document, PungentBoardCanvasViewState state, bool horizontal)
        {
            if (document == null || state == null || state.selectedNodeIds.Count < 2)
                return;

            List<PungentBoardNode> nodes = SelectedNodes(document, state).ToList();
            if (nodes.Count < 2)
                return;

            float anchor = horizontal ? nodes[0].position.y : nodes[0].position.x;
            foreach (PungentBoardNode node in nodes)
            {
                if (horizontal)
                    node.position.y = anchor;
                else
                    node.position.x = anchor;
            }

            UpdateGroupMemberships(document);
            state.contentChanged = true;
            state.repaintRequested = true;
        }

        public static void DistributeSelection(PungentBoardDocument document, PungentBoardCanvasViewState state, bool horizontal)
        {
            if (document == null || state == null || state.selectedNodeIds.Count < 3)
                return;

            List<PungentBoardNode> nodes = SelectedNodes(document, state)
                .OrderBy(node => horizontal ? node.position.x : node.position.y)
                .ToList();
            if (nodes.Count < 3)
                return;

            float first = horizontal ? nodes[0].position.x : nodes[0].position.y;
            float last = horizontal ? nodes[nodes.Count - 1].position.x : nodes[nodes.Count - 1].position.y;
            float step = (last - first) / Mathf.Max(1, nodes.Count - 1);
            for (int i = 1; i < nodes.Count - 1; i++)
            {
                if (horizontal)
                    nodes[i].position.x = first + step * i;
                else
                    nodes[i].position.y = first + step * i;
            }

            UpdateGroupMemberships(document);
            state.contentChanged = true;
            state.repaintRequested = true;
        }

        public static void UpdateGroupMemberships(PungentBoardDocument document)
        {
            if (document == null)
                return;

            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
            {
                if (group == null)
                    continue;

                group.containedNodeIds = group.containedNodeIds ?? new List<string>();
                group.containedNodeIds.Clear();
                foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
                {
                    if (node != null && group.rect.Overlaps(node.Rect))
                        group.containedNodeIds.Add(node.id);
                }
            }
        }

        public static PungentBoardNode AddNodeAt(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNodeKind kind, Vector2 canvasPoint)
        {
            return AddNodeAt(document, state, kind, state != null ? state.defaultNodeTypeKey : string.Empty, canvasPoint);
        }

        public static PungentBoardNode AddNodeAt(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNodeTypeDefinition nodeType, Vector2 canvasPoint)
        {
            if (nodeType == null)
                return AddNodeAt(document, state, state != null ? state.defaultNodeKind : PungentBoardNodeKind.NoteCard, canvasPoint);

            return AddNodeAt(document, state, nodeType.nodeKind, nodeType.typeKey, canvasPoint);
        }

        public static PungentBoardNode AddNodeAt(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNodeKind kind, string nodeTypeKey, Vector2 canvasPoint)
        {
            if (document == null)
                return null;

            BeginHistory(document, state, "Add Node");
            PungentBoardNode node = PungentBoardNode.Create(kind, canvasPoint - new Vector2(110f, 60f));
            ApplyNodeTypeDefaults(document, node, nodeTypeKey);
            document.nodes.Add(node);
            if (state != null)
            {
                state.SelectNode(node.id);
                state.contentChanged = true;
                state.repaintRequested = true;
            }

            return node;
        }

        public static void ApplyNodeTypeDefaults(PungentBoardDocument document, PungentBoardNode node, string nodeTypeKey)
        {
            if (document == null || node == null)
                return;

            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId);
            PungentBoardNodeTypeDefinition type = graphType != null ? graphType.FindNodeType(nodeTypeKey) : null;
            if (type == null && graphType != null)
                type = graphType.GetDefaultNodeType();
            if (type == null)
                return;

            node.nodeTypeKey = type.typeKey;
            node.nodeKind = type.nodeKind;
            if (string.IsNullOrWhiteSpace(node.title) || string.Equals(node.title, PungentBoardNode.GetDefaultTitle(node.nodeKind), StringComparison.OrdinalIgnoreCase) || string.Equals(node.title, "New Card", StringComparison.OrdinalIgnoreCase))
                node.title = string.IsNullOrWhiteSpace(type.defaultTitle) ? type.displayName : type.defaultTitle;
            if (string.IsNullOrWhiteSpace(node.colorStyleKey) || string.Equals(node.colorStyleKey, PungentBoardNode.GetDefaultStyleKey(node.nodeKind), StringComparison.OrdinalIgnoreCase))
                node.colorStyleKey = type.styleKey;
            if (type.defaultSize.x > 0f && type.defaultSize.y > 0f)
                node.size = type.defaultSize;
            node.properties = node.properties ?? new List<PungentBoardGraphPropertyValue>();
            EnsurePropertyValues(node.properties, type.propertyDefinitions);
            node.NormalizeInPlace();
        }

        public static void EnsurePropertyValues(List<PungentBoardGraphPropertyValue> values, IEnumerable<PungentBoardGraphPropertyDefinition> definitions)
        {
            if (values == null)
                return;

            foreach (PungentBoardGraphPropertyDefinition definition in definitions ?? new PungentBoardGraphPropertyDefinition[0])
                PungentBoardGraphPropertyValue.FindOrCreate(values, definition);
        }

        public static PungentBoardGroup AddGroupAt(PungentBoardDocument document, PungentBoardCanvasViewState state, Vector2 canvasPoint)
        {
            if (document == null)
                return null;

            BeginHistory(document, state, "Add Group");
            PungentBoardGroup group = PungentBoardGroup.Create("Group", new Rect(canvasPoint.x - 180f, canvasPoint.y - 120f, 360f, 240f));
            document.groups.Add(group);
            if (state != null)
            {
                state.SelectGroup(group.id);
                state.contentChanged = true;
                state.repaintRequested = true;
            }

            UpdateGroupMemberships(document);
            return group;
        }

        public static PungentBoardNode DuplicateNode(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNode source, Vector2 offset)
        {
            if (document == null || source == null)
                return null;

            BeginHistory(document, state, "Duplicate Node");
            PungentBoardNode duplicate = JsonUtility.FromJson<PungentBoardNode>(JsonUtility.ToJson(source));
            duplicate.id = PungentAuthoringId.NewValue();
            duplicate.title = string.IsNullOrWhiteSpace(source.title) ? "Copy" : source.title + " Copy";
            duplicate.position = source.position + offset;
            duplicate.NormalizeInPlace();
            document.nodes.Add(duplicate);

            if (state != null)
            {
                state.SelectNode(duplicate.id);
                state.contentChanged = true;
                state.repaintRequested = true;
            }

            UpdateGroupMemberships(document);
            return duplicate;
        }

        public static int DuplicateSelection(PungentBoardDocument document, PungentBoardCanvasViewState state, Vector2 offset)
        {
            if (document == null || state == null)
                return 0;

            List<PungentBoardNode> sources = SelectedNodes(document, state).ToList();
            if (sources.Count == 0)
            {
                PungentBoardNode node = FindNode(document, state.selectedNodeId);
                if (node != null)
                    sources.Add(node);
            }

            if (sources.Count == 0)
                return 0;

            BeginHistory(document, state, "Duplicate Selection");
            Dictionary<string, string> idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> newSelection = new List<string>();
            foreach (PungentBoardNode source in sources)
            {
                PungentBoardNode duplicate = JsonUtility.FromJson<PungentBoardNode>(JsonUtility.ToJson(source));
                string oldId = duplicate.id;
                duplicate.id = PungentAuthoringId.NewValue();
                duplicate.title = string.IsNullOrWhiteSpace(source.title) ? "Copy" : source.title + " Copy";
                duplicate.position = source.position + offset;
                duplicate.integrationSourceAdapterId = string.Empty;
                duplicate.integrationSourceKey = string.Empty;
                duplicate.integrationSourceLabel = string.Empty;
                duplicate.syncState = string.Empty;
                duplicate.NormalizeInPlace();
                document.nodes.Add(duplicate);
                idMap[oldId] = duplicate.id;
                newSelection.Add(duplicate.id);
            }

            foreach (PungentBoardEdge edge in (document.edges ?? new List<PungentBoardEdge>()).ToArray())
            {
                if (edge == null || !idMap.ContainsKey(edge.fromNodeId) || !idMap.ContainsKey(edge.toNodeId))
                    continue;

                PungentBoardEdge duplicateEdge = PungentBoardEdge.Create(idMap[edge.fromNodeId], idMap[edge.toNodeId]);
                duplicateEdge.label = edge.label;
                duplicateEdge.edgeKind = edge.edgeKind;
                duplicateEdge.edgeTypeKey = edge.edgeTypeKey;
                duplicateEdge.styleKey = edge.styleKey;
                duplicateEdge.directed = edge.directed;
                duplicateEdge.fromPortKey = edge.fromPortKey;
                duplicateEdge.toPortKey = edge.toPortKey;
                duplicateEdge.executionOrder = edge.executionOrder;
                document.edges.Add(duplicateEdge);
            }

            state.ClearSelection();
            foreach (string id in newSelection)
                state.selectedNodeIds.Add(id);
            state.selectedNodeId = newSelection.Count > 0 ? newSelection[newSelection.Count - 1] : string.Empty;
            UpdateGroupMemberships(document);
            state.contentChanged = sources.Count > 0;
            state.repaintRequested = sources.Count > 0;
            return sources.Count;
        }

        public static void BeginExternalHistory(PungentBoardDocument document, PungentBoardCanvasViewState state, string label)
        {
            BeginHistory(document, state, label);
        }

        private static void BeginHistory(PungentBoardDocument document, PungentBoardCanvasViewState state, string label)
        {
            if (document == null || state == null || !string.IsNullOrWhiteSpace(state.pendingHistorySnapshotJson))
                return;

            state.pendingHistoryLabel = string.IsNullOrWhiteSpace(label) ? "Board Edit" : label.Trim();
            state.pendingHistorySnapshotJson = JsonUtility.ToJson(document);
        }

        private static void SetPendingHistory(PungentBoardCanvasViewState state, string label, string beforeJson)
        {
            if (state == null || string.IsNullOrWhiteSpace(beforeJson) || !string.IsNullOrWhiteSpace(state.pendingHistorySnapshotJson))
                return;

            state.pendingHistoryLabel = string.IsNullOrWhiteSpace(label) ? "Board Edit" : label.Trim();
            state.pendingHistorySnapshotJson = beforeJson;
        }

        private static void HandleInput(Rect canvasRect, PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            Event evt = Event.current;
            if (evt == null)
                return;

            bool insideCanvas = canvasRect.Contains(evt.mousePosition);
            if (!insideCanvas)
            {
                if (evt.rawType == EventType.MouseUp)
                {
                    CancelConnectorDrag(state);
                    EndMarquee(document, state, false);
                    EndDrag(document, state);
                }
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                Vector2 before = ScreenToCanvas(evt.mousePosition, canvasRect, state);
                float zoomFactor = Mathf.Pow(1.08f, -evt.delta.y);
                float nextZoom = Mathf.Clamp(state.zoom * zoomFactor, MinZoom, MaxZoom);
                if (!Mathf.Approximately(nextZoom, state.zoom))
                {
                    state.zoom = nextZoom;
                    state.pan = evt.mousePosition - canvasRect.position - before * state.zoom;
                    state.viewChanged = true;
                    state.repaintRequested = true;
                }
                evt.Use();
                return;
            }

            if (evt.type == EventType.KeyDown)
            {
                if ((evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) && state.HasSelection)
                {
                    state.deleteRequested = true;
                    evt.Use();
                    return;
                }

                if (evt.keyCode == KeyCode.Escape)
                {
                    CancelConnectorDrag(state);
                    state.connectorMode = false;
                    state.connectorSourceNodeId = string.Empty;
                    EndMarquee(document, state, false);
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }
            }

            if (evt.type == EventType.MouseDown)
            {
                if (evt.button == 2 || (evt.button == 0 && evt.alt))
                {
                    state.panning = true;
                    state.panStartMouse = evt.mousePosition;
                    state.panStartValue = state.pan;
                    evt.Use();
                    return;
                }

                if (evt.button != 0)
                    return;

                Vector2 canvasPoint = ScreenToCanvas(evt.mousePosition, canvasRect, state);
                string beforeJson = JsonUtility.ToJson(document);
                if (IsPointerOverInlineGui(document, evt.mousePosition, canvasRect, state))
                    return;

                if (TryToggleCollapseAt(document, evt.mousePosition, canvasRect, state))
                {
                    SetPendingHistory(state, "Toggle Collapse", beforeJson);
                    state.contentChanged = true;
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                PungentBoardPortHit portHit = FindNodePortAt(document, evt.mousePosition, canvasRect, state);
                if (portHit != null)
                {
                    string endpointNodeId = portHit.EndpointNodeId;
                    string endpointPortKey = portHit.EndpointPortKey;
                    if (!string.IsNullOrWhiteSpace(state.connectorSourceNodeId) && portHit.CanFinishEdge && !PungentAuthoringId.EqualsId(state.connectorSourceNodeId, endpointNodeId))
                    {
                        SetPendingHistory(state, "Create Edge", beforeJson);
                        PungentBoardEdge createdEdge;
                        if (TryCreateTypedEdge(document, state, state.connectorSourceNodeId, endpointNodeId, state.connectorSourcePortKey, endpointPortKey, out createdEdge))
                        {
                            document.edges.Add(createdEdge);
                            state.SelectEdge(createdEdge.id);
                            state.contentChanged = true;
                        }
                        state.connectorMode = false;
                        state.connectorDragging = false;
                        state.connectorSourceNodeId = string.Empty;
                        state.connectorSourcePortKey = string.Empty;
                    }
                    else if (portHit.CanStartEdge)
                    {
                        SetPendingHistory(state, "Create Edge", beforeJson);
                        state.connectorMode = true;
                        state.connectorDragging = true;
                        state.connectorSourceNodeId = endpointNodeId;
                        state.connectorSourcePortKey = endpointPortKey;
                        state.connectionWarning = string.Empty;
                        SelectPortHitOwner(state, portHit);
                    }
                    else
                    {
                        state.connectionWarning = string.IsNullOrWhiteSpace(endpointNodeId)
                            ? "Expose this group port by mapping it to an internal node port first."
                            : "Start connections from an output port.";
                    }

                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                PungentBoardNode resizeNode = FindNodeResizeHandleAt(document, evt.mousePosition, canvasRect, state);
                if (resizeNode != null)
                {
                    SetPendingHistory(state, "Resize Node", beforeJson);
                    state.SelectNode(resizeNode.id);
                    state.resizingNodeId = resizeNode.id;
                    state.dragStartMouse = evt.mousePosition;
                    state.resizeStartSize = resizeNode.size;
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                PungentBoardGroup resizeGroup = FindGroupResizeHandleAt(document, evt.mousePosition, canvasRect, state);
                if (resizeGroup != null)
                {
                    SetPendingHistory(state, "Resize Group", beforeJson);
                    state.SelectGroup(resizeGroup.id);
                    state.resizingGroupId = resizeGroup.id;
                    state.dragStartMouse = evt.mousePosition;
                    state.dragStartRect = resizeGroup.rect;
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                PungentBoardNode node = FindNodeAt(document, canvasPoint);
                if (node != null)
                {
                    if (state.connectorMode)
                    {
                        SetPendingHistory(state, "Create Edge", beforeJson);
                        CompleteConnectorClick(document, state, node);
                        evt.Use();
                        return;
                    }

                    if (evt.shift || evt.control || evt.command)
                        state.ToggleNodeSelection(node.id);
                    else if (!state.IsNodeSelected(node.id))
                        state.SelectNode(node.id);
                    else
                        state.selectedNodeId = node.id;

                    SetPendingHistory(state, "Move Selection", beforeJson);
                    CaptureDragStarts(document, state);
                    state.draggingNodeId = node.id;
                    state.dragStartMouse = evt.mousePosition;
                    state.dragStartPosition = node.position;
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                PungentBoardEdge edge = FindEdgeAt(document, evt.mousePosition, canvasRect, state);
                if (edge != null)
                {
                    state.SelectEdge(edge.id);
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                PungentBoardGroup group = FindGroupAt(document, canvasPoint);
                if (group != null)
                {
                    if (evt.shift || evt.control || evt.command)
                        state.ToggleGroupSelection(group.id);
                    else if (!state.IsGroupSelected(group.id))
                        state.SelectGroup(group.id);
                    else
                        state.selectedGroupId = group.id;

                    SetPendingHistory(state, "Move Selection", beforeJson);
                    CaptureDragStarts(document, state);
                    state.draggingGroupId = group.id;
                    state.dragStartMouse = evt.mousePosition;
                    state.dragStartRect = group.rect;
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                if (evt.clickCount == 2)
                {
                    AddNodeAt(document, state, state.defaultNodeKind, canvasPoint);
                    evt.Use();
                    return;
                }

                state.marqueeSelecting = true;
                state.marqueeStartCanvas = canvasPoint;
                state.marqueeCurrentCanvas = canvasPoint;
                state.repaintRequested = true;
                evt.Use();
                return;
            }

            if (evt.type == EventType.ContextClick)
            {
                ShowContextMenu(canvasRect, document, state, evt.mousePosition);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag)
            {
                if (state.panning)
                {
                    state.pan = state.panStartValue + (evt.mousePosition - state.panStartMouse);
                    state.viewChanged = true;
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                if (state.connectorDragging)
                {
                    UpdateConnectorHover(document, state, evt.mousePosition, canvasRect);
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                if (state.marqueeSelecting)
                {
                    state.marqueeCurrentCanvas = ScreenToCanvas(evt.mousePosition, canvasRect, state);
                    state.repaintRequested = true;
                    evt.Use();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(state.resizingNodeId))
                {
                    PungentBoardNode node = FindNode(document, state.resizingNodeId);
                    if (node != null)
                    {
                        Vector2 delta = (evt.mousePosition - state.dragStartMouse) / Mathf.Max(0.001f, state.zoom);
                        Vector2 nextSize = state.resizeStartSize + delta;
                        if (state.snapToGrid && state.snapResize)
                            nextSize = SnapVector(nextSize, state);
                        node.size = new Vector2(
                            Mathf.Clamp(nextSize.x, 120f, 720f),
                            Mathf.Clamp(nextSize.y, 64f, 520f));
                        state.repaintRequested = true;
                    }
                    evt.Use();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(state.resizingGroupId))
                {
                    PungentBoardGroup group = FindGroup(document, state.resizingGroupId);
                    if (group != null && !group.locked)
                    {
                        Vector2 delta = (evt.mousePosition - state.dragStartMouse) / Mathf.Max(0.001f, state.zoom);
                        Vector2 nextSize = state.dragStartRect.size + delta;
                        if (state.snapToGrid && state.snapResize)
                            nextSize = SnapVector(nextSize, state);
                        group.rect.size = new Vector2(
                            Mathf.Clamp(nextSize.x, 160f, 2000f),
                            Mathf.Clamp(nextSize.y, 100f, 2000f));
                        state.repaintRequested = true;
                    }
                    evt.Use();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(state.draggingNodeId))
                {
                    Vector2 delta = (evt.mousePosition - state.dragStartMouse) / Mathf.Max(0.001f, state.zoom);
                    MoveSelectedNodes(document, state, delta);
                    evt.Use();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(state.draggingGroupId))
                {
                    Vector2 delta = (evt.mousePosition - state.dragStartMouse) / Mathf.Max(0.001f, state.zoom);
                    MoveSelectedGroups(document, state, delta);
                    evt.Use();
                    return;
                }
            }

            if (evt.rawType == EventType.MouseUp)
            {
                if (state.connectorDragging)
                {
                    PungentBoardPortHit targetPort = FindNodePortAt(document, evt.mousePosition, canvasRect, state);
                    PungentBoardNode target = targetPort != null ? targetPort.node : FindNodeAt(document, ScreenToCanvas(evt.mousePosition, canvasRect, state));
                    string targetNodeId = targetPort != null ? targetPort.EndpointNodeId : target != null ? target.id : string.Empty;
                    string targetPortKey = targetPort != null && targetPort.CanFinishEdge ? targetPort.EndpointPortKey : string.Empty;
                    if (!string.IsNullOrWhiteSpace(targetNodeId) && !PungentAuthoringId.EqualsId(targetNodeId, state.connectorSourceNodeId))
                    {
                        PungentBoardEdge edge;
                        if (TryCreateTypedEdge(document, state, state.connectorSourceNodeId, targetNodeId, state.connectorSourcePortKey, targetPortKey, out edge))
                        {
                            document.edges.Add(edge);
                            state.SelectEdge(edge.id);
                            state.contentChanged = true;
                        }
                    }
                    CancelConnectorDrag(state);
                    evt.Use();
                    return;
                }

                if (state.marqueeSelecting)
                {
                    EndMarquee(document, state, true);
                    evt.Use();
                    return;
                }

                EndDrag(document, state);
            }
        }

        private static bool IsPointerOverInlineGui(PungentBoardDocument document, Vector2 mousePosition, Rect canvasRect, PungentBoardCanvasViewState state)
        {
            if (document == null || state == null)
                return false;

            Vector2 localMouse = mousePosition - canvasRect.position;
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId);
            List<PungentBoardNode> nodes = document.nodes ?? new List<PungentBoardNode>();
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                PungentBoardNode node = nodes[i];
                if (node == null || IsNodeHiddenByCollapsedGroup(document, node.id))
                    continue;

                Rect rect = CanvasToLocal(node.Rect, state);
                bool selected = state.IsNodeSelected(node.id);
                bool hovered = rect.Contains(localMouse);
                bool editingTitle = state.inlineEdit.IsEditing(node.id, PungentBoardInlineNodeEditField.Title);
                bool editingBody = state.inlineEdit.IsEditing(node.id, PungentBoardInlineNodeEditField.Body);
                if (!selected && !hovered && !editingTitle && !editingBody)
                    continue;

                PungentBoardNodeTypeDefinition nodeType = ResolveNodeType(graphType, node);
                PungentBoardNodeVisualLayout layout = BuildNodeVisualLayout(rect, node, state, IsMissingLinkedItem(node), InputPortsOrDefault(nodeType), OutputPortsOrDefault(nodeType));
                if (layout.controlsRect.Contains(localMouse))
                    return true;
                if (editingTitle && layout.titleRect.Contains(localMouse))
                    return true;
                if (editingBody && layout.bodyRect.Contains(localMouse))
                    return true;
            }

            List<PungentBoardGroup> groups = document.groups ?? new List<PungentBoardGroup>();
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                PungentBoardGroup group = groups[i];
                if (!IsCollapsedGroupNode(group))
                    continue;

                Rect rect = VisibleGroupLocalRect(group, state);
                if (!rect.Contains(localMouse) && !state.IsGroupSelected(group.id))
                    continue;

                Rect controls = new Rect(rect.xMax - 144f, rect.y + 5f, 136f, 20f);
                if (controls.Contains(localMouse))
                    return true;
            }

            return false;
        }

        private static void CancelConnectorDrag(PungentBoardCanvasViewState state)
        {
            if (state == null)
                return;

            state.connectorDragging = false;
            state.connectorMode = false;
            state.connectorSourceNodeId = string.Empty;
            state.connectorSourcePortKey = string.Empty;
            state.connectorHoverNodeId = string.Empty;
            state.connectorHoverPortKey = string.Empty;
            state.connectorHoverValid = false;
            if (!state.contentChanged)
                state.ClearPendingHistory();
            state.repaintRequested = true;
        }

        private static void CaptureDragStarts(PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            state.dragStartNodePositions.Clear();
            state.dragStartGroupRects.Clear();
            foreach (PungentBoardNode node in SelectedNodes(document, state))
                state.dragStartNodePositions[node.id] = node.position;
            foreach (PungentBoardGroup group in SelectedGroups(document, state))
            {
                state.dragStartGroupRects[group.id] = group.rect;
                if (!IsCollapsedGroupNode(group))
                    continue;

                foreach (string nodeId in group.containedNodeIds ?? new List<string>())
                {
                    if (state.dragStartNodePositions.ContainsKey(nodeId))
                        continue;
                    PungentBoardNode node = FindNode(document, nodeId);
                    if (node != null)
                        state.dragStartNodePositions[node.id] = node.position;
                }
            }
        }

        private static void MoveSelectedNodes(PungentBoardDocument document, PungentBoardCanvasViewState state, Vector2 delta)
        {
            foreach (KeyValuePair<string, Vector2> pair in state.dragStartNodePositions)
            {
                PungentBoardNode node = FindNode(document, pair.Key);
                if (node == null)
                    continue;

                Vector2 nextPosition = pair.Value + delta;
                node.position = state.snapToGrid && state.snapNodes ? SnapVector(nextPosition, state) : nextPosition;
            }

            state.repaintRequested = true;
        }

        private static void MoveSelectedGroups(PungentBoardDocument document, PungentBoardCanvasViewState state, Vector2 delta)
        {
            foreach (KeyValuePair<string, Rect> pair in state.dragStartGroupRects)
            {
                PungentBoardGroup group = FindGroup(document, pair.Key);
                if (group == null || group.locked)
                    continue;

                Rect nextRect = pair.Value;
                Vector2 nextPosition = pair.Value.position + delta;
                nextRect.position = state.snapToGrid && state.snapGroups ? SnapVector(nextPosition, state) : nextPosition;
                group.rect = nextRect;
            }

            foreach (KeyValuePair<string, Vector2> pair in state.dragStartNodePositions)
            {
                PungentBoardNode node = FindNode(document, pair.Key);
                if (node == null)
                    continue;

                Vector2 nextPosition = pair.Value + delta;
                node.position = state.snapToGrid && state.snapNodes ? SnapVector(nextPosition, state) : nextPosition;
            }

            state.repaintRequested = true;
        }

        private static void EndMarquee(PungentBoardDocument document, PungentBoardCanvasViewState state, bool applySelection)
        {
            if (state == null || !state.marqueeSelecting)
                return;

            Rect marquee = GetMarqueeCanvasRect(state);
            state.marqueeSelecting = false;
            if (!applySelection || marquee.width < 4f || marquee.height < 4f)
            {
                state.ClearSelection();
                state.repaintRequested = true;
                return;
            }

            state.ClearSelection();
            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
                if (node != null && !IsNodeHiddenByCollapsedGroup(document, node.id) && marquee.Overlaps(node.Rect))
                    state.selectedNodeIds.Add(node.id);
            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
                if (group != null && marquee.Overlaps(VisibleGroupCanvasRect(group)))
                    state.selectedGroupIds.Add(group.id);

            state.selectedNodeId = state.selectedNodeIds.Count > 0 ? state.selectedNodeIds[state.selectedNodeIds.Count - 1] : string.Empty;
            state.selectedGroupId = state.selectedNodeIds.Count == 0 && state.selectedGroupIds.Count > 0 ? state.selectedGroupIds[state.selectedGroupIds.Count - 1] : string.Empty;
            state.repaintRequested = true;
        }

        private static void EndDrag(PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            bool changed = HasTransformChanged(document, state);
            state.panning = false;
            state.draggingNodeId = string.Empty;
            state.draggingGroupId = string.Empty;
            state.resizingNodeId = string.Empty;
            state.resizingGroupId = string.Empty;
            state.dragStartNodePositions.Clear();
            state.dragStartGroupRects.Clear();
            if (changed)
            {
                UpdateGroupMemberships(document);
                state.contentChanged = true;
                state.repaintRequested = true;
            }
            else
            {
                state.ClearPendingHistory();
            }
        }

        private static bool HasTransformChanged(PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            if (!string.IsNullOrWhiteSpace(state.resizingNodeId))
            {
                PungentBoardNode node = FindNode(document, state.resizingNodeId);
                return node != null && (node.size - state.resizeStartSize).sqrMagnitude > 0.01f;
            }

            if (!string.IsNullOrWhiteSpace(state.resizingGroupId))
            {
                PungentBoardGroup group = FindGroup(document, state.resizingGroupId);
                return group != null && (group.rect.size - state.dragStartRect.size).sqrMagnitude > 0.01f;
            }

            foreach (KeyValuePair<string, Vector2> pair in state.dragStartNodePositions)
            {
                PungentBoardNode node = FindNode(document, pair.Key);
                if (node != null && (node.position - pair.Value).sqrMagnitude > 0.01f)
                    return true;
            }

            foreach (KeyValuePair<string, Rect> pair in state.dragStartGroupRects)
            {
                PungentBoardGroup group = FindGroup(document, pair.Key);
                if (group != null && (group.rect.position - pair.Value.position).sqrMagnitude > 0.01f)
                    return true;
            }

            return false;
        }

        public static bool TryCreateTypedEdge(PungentBoardDocument document, PungentBoardCanvasViewState state, string fromNodeId, string toNodeId, out PungentBoardEdge edge)
        {
            string fromPortKey = state != null ? state.connectorSourcePortKey : string.Empty;
            return TryCreateTypedEdge(document, state, fromNodeId, toNodeId, fromPortKey, string.Empty, out edge);
        }

        public static bool TryCreateTypedEdge(PungentBoardDocument document, PungentBoardCanvasViewState state, string fromNodeId, string toNodeId, string fromPortKey, string toPortKey, out PungentBoardEdge edge)
        {
            edge = null;
            if (document == null || state == null)
                return false;

            PungentBoardEdgeRuleDefinition rule;
            PungentBoardNode from;
            PungentBoardNode to;
            string message;
            if (!TryResolveEdgeRule(document, fromNodeId, toNodeId, fromPortKey, toPortKey, state.defaultEdgeTypeKey, out rule, out from, out to, out message))
            {
                state.connectionWarning = message;
                state.repaintRequested = true;
                return false;
            }

            edge = PungentBoardEdge.Create(from.id, to.id);
            if (rule != null)
                ApplyEdgeRule(edge, rule, fromPortKey, toPortKey);
            else
                edge.edgeTypeKey = state.defaultEdgeTypeKey;

            edge.NormalizeInPlace();
            state.connectionWarning = string.Empty;
            return true;
        }

        public static bool CanCreateTypedEdge(PungentBoardDocument document, PungentBoardCanvasViewState state, string fromNodeId, string toNodeId, string fromPortKey, string toPortKey, out string message)
        {
            PungentBoardEdgeRuleDefinition rule;
            PungentBoardNode from;
            PungentBoardNode to;
            return TryResolveEdgeRule(document, fromNodeId, toNodeId, fromPortKey, toPortKey, state != null ? state.defaultEdgeTypeKey : string.Empty, out rule, out from, out to, out message);
        }

        public static bool ApplyBestEdgeRule(PungentBoardDocument document, PungentBoardEdge edge, string preferredEdgeTypeKey)
        {
            if (document == null || edge == null)
                return false;

            PungentBoardEdgeRuleDefinition rule;
            PungentBoardNode from;
            PungentBoardNode to;
            string message;
            string fromPortKey = edge.fromPortKey;
            string toPortKey = edge.toPortKey;
            if (!TryResolveEdgeRule(document, edge.fromNodeId, edge.toNodeId, fromPortKey, toPortKey, preferredEdgeTypeKey, out rule, out from, out to, out message))
            {
                fromPortKey = string.Empty;
                toPortKey = string.Empty;
                if (!TryResolveEdgeRule(document, edge.fromNodeId, edge.toNodeId, fromPortKey, toPortKey, preferredEdgeTypeKey, out rule, out from, out to, out message))
                    return false;
            }

            ApplyEdgeRule(edge, rule, fromPortKey, toPortKey);
            edge.NormalizeInPlace();
            return true;
        }

        public static bool ClearEdgePorts(PungentBoardEdge edge)
        {
            if (edge == null)
                return false;

            bool changed = !string.IsNullOrWhiteSpace(edge.fromPortKey) || !string.IsNullOrWhiteSpace(edge.toPortKey);
            edge.fromPortKey = string.Empty;
            edge.toPortKey = string.Empty;
            edge.NormalizeInPlace();
            return changed;
        }

        private static bool TryResolveEdgeRule(PungentBoardDocument document, string fromNodeId, string toNodeId, string fromPortKey, string toPortKey, string preferredEdgeTypeKey, out PungentBoardEdgeRuleDefinition rule, out PungentBoardNode from, out PungentBoardNode to, out string message)
        {
            rule = null;
            from = null;
            to = null;
            message = string.Empty;
            if (document == null)
            {
                message = "Cannot create edge: board is missing.";
                return false;
            }

            from = FindNode(document, fromNodeId);
            to = FindNode(document, toNodeId);
            if (from == null || to == null)
            {
                message = "Cannot create edge: source or target node is missing.";
                return false;
            }

            if (PungentAuthoringId.EqualsId(from.id, to.id))
            {
                message = "Cannot create edge: source and target are the same node.";
                return false;
            }

            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId);
            PungentBoardNodeTypeDefinition fromType = ResolveNodeType(graphType, from);
            PungentBoardNodeTypeDefinition toType = ResolveNodeType(graphType, to);
            string fromTypeKey = fromType != null ? fromType.typeKey : from.nodeTypeKey;
            string toTypeKey = toType != null ? toType.typeKey : to.nodeTypeKey;

            if (!string.IsNullOrWhiteSpace(fromPortKey) && !HasPort(fromType, fromPortKey, false))
            {
                message = "Source port '" + fromPortKey + "' is not an output port on " + (fromType != null ? fromType.displayName : "the source node") + ".";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(toPortKey) && !HasPort(toType, toPortKey, true))
            {
                message = "Target port '" + toPortKey + "' is not an input port on " + (toType != null ? toType.displayName : "the target node") + ".";
                return false;
            }

            if (graphType == null || (graphType.edgeRules?.Count ?? 0) == 0)
            {
                return true;
            }

            PungentBoardEdgeRuleDefinition preferredRule = !string.IsNullOrWhiteSpace(preferredEdgeTypeKey)
                ? graphType.FindEdgeRule(preferredEdgeTypeKey)
                : null;
            if (preferredRule != null)
            {
                if (!preferredRule.AllowsConnection(fromTypeKey, toTypeKey))
                {
                    message = "Connection not allowed by selected edge rule '" + preferredRule.displayName + "' from " + fromTypeKey + " to " + toTypeKey + ".";
                    return false;
                }

                if (!HasPort(fromType, preferredRule.fromPortKey, false) || !HasPort(toType, preferredRule.toPortKey, true))
                {
                    message = "Selected edge rule '" + preferredRule.displayName + "' requires ports that are not available on this node pair.";
                    return false;
                }

                rule = preferredRule;
                return true;
            }

            List<PungentBoardEdgeRuleDefinition> candidates = graphType.edgeRules
                .Where(candidate => candidate != null &&
                                    candidate.AllowsConnection(fromTypeKey, toTypeKey) &&
                                    RulePortMatches(candidate.fromPortKey, fromPortKey) &&
                                    RulePortMatches(candidate.toPortKey, toPortKey))
                .ToList();
            if (candidates.Count == 0)
            {
                string portHint = string.IsNullOrWhiteSpace(fromPortKey) && string.IsNullOrWhiteSpace(toPortKey)
                    ? string.Empty
                    : " using ports " + (string.IsNullOrWhiteSpace(fromPortKey) ? "(default)" : fromPortKey) + " -> " + (string.IsNullOrWhiteSpace(toPortKey) ? "(default)" : toPortKey);
                message = "Connection not allowed by graph type '" + graphType.displayName + "' from " + fromTypeKey + " to " + toTypeKey + portHint + ".";
                return false;
            }

            rule = candidates.FirstOrDefault(candidate => string.Equals(candidate.typeKey, preferredEdgeTypeKey, StringComparison.OrdinalIgnoreCase)) ??
                   candidates.FirstOrDefault(candidate => string.Equals(candidate.typeKey, graphType.defaultEdgeTypeKey, StringComparison.OrdinalIgnoreCase)) ??
                   candidates[0];
            return true;
        }

        private static void ApplyEdgeRule(PungentBoardEdge edge, PungentBoardEdgeRuleDefinition rule, string fromPortKey, string toPortKey)
        {
            if (edge == null || rule == null)
                return;

            edge.edgeTypeKey = rule.typeKey;
            edge.edgeKind = rule.edgeKind;
            edge.styleKey = rule.styleKey;
            edge.directed = rule.directed;
            edge.fromPortKey = !string.IsNullOrWhiteSpace(rule.fromPortKey) ? rule.fromPortKey : fromPortKey;
            edge.toPortKey = !string.IsNullOrWhiteSpace(rule.toPortKey) ? rule.toPortKey : toPortKey;
            edge.label = string.IsNullOrWhiteSpace(edge.label) || string.Equals(edge.label, edge.edgeTypeKey, StringComparison.OrdinalIgnoreCase) ? rule.displayName : edge.label;
            edge.properties = edge.properties ?? new List<PungentBoardGraphPropertyValue>();
            EnsurePropertyValues(edge.properties, rule.propertyDefinitions);
        }

        private static bool RulePortMatches(string rulePortKey, string actualPortKey)
        {
            return string.IsNullOrWhiteSpace(rulePortKey) ||
                   string.IsNullOrWhiteSpace(actualPortKey) ||
                   string.Equals(rulePortKey, actualPortKey, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPort(PungentBoardNodeTypeDefinition nodeType, string portKey, bool input)
        {
            if (nodeType == null || string.IsNullOrWhiteSpace(portKey))
                return true;

            IEnumerable<PungentBoardPortDefinition> ports = input ? nodeType.InputPorts() : nodeType.OutputPorts();
            return ports.Any(port => port != null && string.Equals(port.key, portKey, StringComparison.OrdinalIgnoreCase));
        }

        private static void CompleteConnectorClick(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNode node)
        {
            if (node == null)
                return;

            if (string.IsNullOrWhiteSpace(state.connectorSourceNodeId))
            {
                state.connectorSourceNodeId = node.id;
                state.connectorSourcePortKey = string.Empty;
                state.connectionWarning = string.Empty;
                state.SelectNode(node.id);
                state.repaintRequested = true;
                return;
            }

            if (!PungentAuthoringId.EqualsId(state.connectorSourceNodeId, node.id))
            {
                BeginHistory(document, state, "Create Edge");
                PungentBoardEdge edge;
                if (TryCreateTypedEdge(document, state, state.connectorSourceNodeId, node.id, out edge))
                {
                    document.edges.Add(edge);
                    state.SelectEdge(edge.id);
                    state.contentChanged = true;
                }
            }

            state.connectorSourceNodeId = string.Empty;
            state.connectorSourcePortKey = string.Empty;
            state.connectorMode = false;
            state.repaintRequested = true;
        }

        private static void ShowContextMenu(Rect canvasRect, PungentBoardDocument document, PungentBoardCanvasViewState state, Vector2 mousePosition)
        {
            if (document == null || state == null)
                return;

            Vector2 canvasPoint = ScreenToCanvas(mousePosition, canvasRect, state);
            PungentBoardNode hitNode = FindNodeAt(document, canvasPoint);
            PungentBoardEdge hitEdge = FindEdgeAt(document, mousePosition, canvasRect, state);
            PungentBoardGroup hitGroup = FindGroupAt(document, canvasPoint);
            GenericMenu menu = new GenericMenu();

            if (hitNode != null)
            {
                menu.AddItem(new GUIContent("Select Node"), false, () =>
                {
                    state.SelectNode(hitNode.id);
                    state.repaintRequested = true;
                });
                menu.AddItem(new GUIContent("Duplicate Node"), false, () => DuplicateNode(document, state, hitNode, new Vector2(28f, 28f)));
                menu.AddItem(new GUIContent(hitNode.collapsed ? "Expand Node Preview" : "Collapse Node Preview"), false, () =>
                {
                    BeginHistory(document, state, "Toggle Node Preview");
                    hitNode.collapsed = !hitNode.collapsed;
                    state.SelectNode(hitNode.id);
                    state.contentChanged = true;
                    state.repaintRequested = true;
                });
                if (CanOpenLinkedAuthoringItem(hitNode.linkedAuthoringRef))
                    menu.AddItem(new GUIContent("Open Linked Authoring Item"), false, () => PungentAuthoringProviderRegistry.TryOpen(hitNode.linkedAuthoringRef));
                else
                    menu.AddDisabledItem(new GUIContent("Open Linked Authoring Item"));
                menu.AddItem(new GUIContent("Start Edge From Node"), false, () =>
                {
                    state.SelectNode(hitNode.id);
                    state.connectorMode = true;
                    state.connectorSourceNodeId = hitNode.id;
                    state.connectorSourcePortKey = string.Empty;
                    state.connectionWarning = string.Empty;
                    state.repaintRequested = true;
                });

                if (!string.IsNullOrWhiteSpace(state.connectorSourceNodeId) && !PungentAuthoringId.EqualsId(state.connectorSourceNodeId, hitNode.id))
                {
                    menu.AddItem(new GUIContent("Finish Edge To Node"), false, () =>
                    {
                        BeginHistory(document, state, "Create Edge");
                        PungentBoardEdge edge;
                        if (TryCreateTypedEdge(document, state, state.connectorSourceNodeId, hitNode.id, out edge))
                        {
                            document.edges.Add(edge);
                            state.SelectEdge(edge.id);
                            state.contentChanged = true;
                        }
                        state.connectorMode = false;
                        state.connectorSourceNodeId = string.Empty;
                        state.connectorSourcePortKey = string.Empty;
                        state.repaintRequested = true;
                    });
                }

                menu.AddItem(new GUIContent("Delete Node"), false, () =>
                {
                    state.SelectNode(hitNode.id);
                    state.deleteRequested = true;
                    state.repaintRequested = true;
                });
                menu.AddSeparator(string.Empty);
            }
            else if (hitEdge != null)
            {
                menu.AddItem(new GUIContent("Select Edge"), false, () =>
                {
                    state.SelectEdge(hitEdge.id);
                    state.repaintRequested = true;
                });
                menu.AddItem(new GUIContent("Delete Edge"), false, () =>
                {
                    state.SelectEdge(hitEdge.id);
                    state.deleteRequested = true;
                    state.repaintRequested = true;
                });
                menu.AddSeparator(string.Empty);
            }
            else if (hitGroup != null)
            {
                menu.AddItem(new GUIContent("Select Group"), false, () =>
                {
                    state.SelectGroup(hitGroup.id);
                    state.repaintRequested = true;
                });
                menu.AddItem(new GUIContent(hitGroup.collapsed ? "Expand Group" : "Collapse Group"), false, () =>
                {
                    BeginHistory(document, state, "Toggle Group");
                    hitGroup.collapsed = !hitGroup.collapsed;
                    state.SelectGroup(hitGroup.id);
                    state.contentChanged = true;
                    state.repaintRequested = true;
                });
                menu.AddItem(new GUIContent(hitGroup.locked ? "Unlock Group" : "Lock Group"), false, () =>
                {
                    BeginHistory(document, state, "Toggle Group Lock");
                    hitGroup.locked = !hitGroup.locked;
                    state.SelectGroup(hitGroup.id);
                    state.contentChanged = true;
                    state.repaintRequested = true;
                });
                menu.AddItem(new GUIContent(hitGroup.collapsedAsNode ? "Custom Node/Disable Collapse As Node" : "Custom Node/Enable Collapse As Node"), hitGroup.collapsedAsNode, () =>
                {
                    BeginHistory(document, state, "Toggle Group Node Mode");
                    hitGroup.collapsedAsNode = !hitGroup.collapsedAsNode;
                    if (hitGroup.collapsedAsNode)
                    {
                        hitGroup.customNodeEnabled = true;
                        hitGroup.collapsed = true;
                    }
                    state.SelectGroup(hitGroup.id);
                    state.contentChanged = true;
                    state.repaintRequested = true;
                });
                menu.AddItem(new GUIContent("Custom Node/Save Or Update Template"), false, () =>
                {
                    BeginHistory(document, state, "Save Custom Group Template");
                    PungentBoardCustomNodeTemplate template;
                    string message;
                    if (PungentBoardCustomNodeTemplateStorage.SaveGroupAsTemplate(document, hitGroup, out template, out message))
                        state.contentChanged = true;
                    state.SelectGroup(hitGroup.id);
                    state.repaintRequested = true;
                });
                menu.AddItem(new GUIContent("Delete Group"), false, () =>
                {
                    state.SelectGroup(hitGroup.id);
                    state.deleteRequested = true;
                    state.repaintRequested = true;
                });
                menu.AddSeparator(string.Empty);
            }

            List<PungentBoardNodeTypeDefinition> nodeTypes = GetPaletteNodeTypes(document);
            foreach (PungentBoardNodeTypeDefinition nodeType in nodeTypes)
            {
                PungentBoardNodeTypeDefinition capturedType = nodeType;
                bool selectedType = string.Equals(capturedType.typeKey, state.defaultNodeTypeKey, StringComparison.OrdinalIgnoreCase);
                menu.AddItem(new GUIContent("Add Node/" + capturedType.displayName), selectedType, () => AddNodeAt(document, state, capturedType, canvasPoint));
            }

            menu.AddItem(new GUIContent("Add Group"), false, () => AddGroupAt(document, state, canvasPoint));
            menu.AddSeparator(string.Empty);
            if (state.SelectedNodeCount > 0)
                menu.AddItem(new GUIContent("Selection/Duplicate Selected Nodes"), false, () => DuplicateSelection(document, state, new Vector2(32f, 32f)));
            else
                menu.AddDisabledItem(new GUIContent("Selection/Duplicate Selected Nodes"));
            if (state.HasSelection)
            {
                menu.AddItem(new GUIContent("Selection/Delete Selection"), false, () =>
                {
                    state.deleteRequested = true;
                    state.repaintRequested = true;
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Selection/Delete Selection"));
            }

            if (state.SelectedNodeCount >= 2)
            {
                menu.AddItem(new GUIContent("Selection/Align Vertically"), false, () =>
                {
                    BeginHistory(document, state, "Align Nodes Vertically");
                    AlignSelection(document, state, false);
                });
                menu.AddItem(new GUIContent("Selection/Align Horizontally"), false, () =>
                {
                    BeginHistory(document, state, "Align Nodes Horizontally");
                    AlignSelection(document, state, true);
                });
                menu.AddItem(new GUIContent("Selection/Distribute Horizontally"), false, () =>
                {
                    BeginHistory(document, state, "Distribute Nodes Horizontally");
                    DistributeSelection(document, state, true);
                });
                menu.AddItem(new GUIContent("Selection/Distribute Vertically"), false, () =>
                {
                    BeginHistory(document, state, "Distribute Nodes Vertically");
                    DistributeSelection(document, state, false);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Selection/Align Vertically"));
                menu.AddDisabledItem(new GUIContent("Selection/Align Horizontally"));
                menu.AddDisabledItem(new GUIContent("Selection/Distribute Horizontally"));
                menu.AddDisabledItem(new GUIContent("Selection/Distribute Vertically"));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(state.showGrid ? "Hide Grid" : "Show Grid"), false, () =>
            {
                state.showGrid = !state.showGrid;
                state.viewChanged = true;
                state.repaintRequested = true;
            });
            menu.AddItem(new GUIContent(state.snapToGrid ? "Disable Snap" : "Enable Snap"), false, () =>
            {
                state.snapToGrid = !state.snapToGrid;
                state.viewChanged = true;
                state.repaintRequested = true;
            });
            menu.ShowAsContext();
        }

        private static bool CanOpenLinkedAuthoringItem(PungentAuthoringReference reference)
        {
            if (reference == null || !reference.HasItemId)
                return false;

            IEnumerable<IPungentAuthoringProvider> providers;
            if (!string.IsNullOrWhiteSpace(reference.providerId))
                providers = new[] { PungentAuthoringProviderRegistry.FindProvider(reference.providerId) }.Where(provider => provider != null);
            else if (reference.itemKind != PungentAuthoringItemKind.Unknown)
                providers = PungentAuthoringProviderRegistry.GetProvidersForKind(reference.itemKind);
            else
                providers = PungentAuthoringProviderRegistry.GetProviders();

            foreach (IPungentAuthoringProvider provider in providers)
            {
                IPungentAuthoringEditorLauncher launcher = provider as IPungentAuthoringEditorLauncher;
                string reason;
                if (launcher != null && launcher.CanOpen(reference, out reason))
                    return true;
            }

            return false;
        }

        private static void DrawGrid(Rect rect, PungentBoardCanvasViewState state)
        {
            float minorSpacing = Mathf.Clamp(state != null ? state.gridMinorSpacing : GridMinor, 8f, 240f);
            int majorEvery = Mathf.Clamp(state != null ? state.gridMajorLineFrequency : 5, 2, 12);
            float opacity = Mathf.Clamp01(state != null ? state.gridOpacity : 1f);
            Handles.BeginGUI();
            DrawGridLines(rect, state, minorSpacing, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.035f * opacity) : new Color(0f, 0f, 0f, 0.045f * opacity), 1f);
            DrawGridLines(rect, state, minorSpacing * majorEvery, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.09f * opacity) : new Color(0f, 0f, 0f, 0.09f * opacity), 1.25f);
            Handles.EndGUI();
        }

        private static void DrawGridLines(Rect rect, PungentBoardCanvasViewState state, float spacing, Color color, float width)
        {
            float scaled = spacing * state.zoom;
            if (scaled < 6f)
                return;

            Handles.color = color;
            float startX = state.pan.x % scaled;
            float startY = state.pan.y % scaled;
            for (float x = startX; x < rect.width; x += scaled)
                Handles.DrawAAPolyLine(width, new Vector3(x, 0f), new Vector3(x, rect.height));
            for (float y = startY; y < rect.height; y += scaled)
                Handles.DrawAAPolyLine(width, new Vector3(0f, y), new Vector3(rect.width, y));
        }

        private static bool IsCollapsedGroupNode(PungentBoardGroup group)
        {
            return group != null && group.collapsed && group.collapsedAsNode;
        }

        private static Rect VisibleGroupCanvasRect(PungentBoardGroup group)
        {
            if (group == null)
                return Rect.zero;

            if (!group.collapsed)
                return group.rect;

            if (!group.collapsedAsNode)
                return new Rect(group.rect.x, group.rect.y, group.rect.width, Mathf.Min(group.rect.height, 34f));

            float width = Mathf.Clamp(group.rect.width, 180f, 420f);
            float height = Mathf.Clamp(group.rect.height, 96f, 180f);
            return new Rect(group.rect.x, group.rect.y, width, height);
        }

        private static Rect VisibleGroupLocalRect(PungentBoardGroup group, PungentBoardCanvasViewState state)
        {
            return CanvasToLocal(VisibleGroupCanvasRect(group), state);
        }

        private static List<PungentBoardPortDefinition> GroupInputPorts(PungentBoardGroup group)
        {
            return (group?.exposedPorts ?? new List<PungentBoardPortDefinition>())
                .Where(port => port != null && (port.direction == PungentBoardPortDirection.Input || port.direction == PungentBoardPortDirection.Both))
                .ToList();
        }

        private static List<PungentBoardPortDefinition> GroupOutputPorts(PungentBoardGroup group)
        {
            return (group?.exposedPorts ?? new List<PungentBoardPortDefinition>())
                .Where(port => port != null && (port.direction == PungentBoardPortDirection.Output || port.direction == PungentBoardPortDirection.Both))
                .ToList();
        }

        private static PungentBoardCustomNodePortMapping FindPortMapping(PungentBoardGroup group, string exposedPortKey, bool inputSide)
        {
            if (group == null || string.IsNullOrWhiteSpace(exposedPortKey))
                return null;

            foreach (PungentBoardCustomNodePortMapping mapping in group.exposedPortMappings ?? new List<PungentBoardCustomNodePortMapping>())
            {
                if (mapping == null || !string.Equals(mapping.exposedPortKey, exposedPortKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool directionMatches = mapping.direction == PungentBoardPortDirection.Both ||
                                        (inputSide && mapping.direction == PungentBoardPortDirection.Input) ||
                                        (!inputSide && mapping.direction == PungentBoardPortDirection.Output);
                if (directionMatches)
                    return mapping;
            }

            return null;
        }

        private static PungentBoardGroup FindCollapsedGroupForNode(PungentBoardDocument document, string nodeId)
        {
            if (document == null || string.IsNullOrWhiteSpace(nodeId))
                return null;

            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
            {
                if (!IsCollapsedGroupNode(group))
                    continue;

                foreach (string containedNodeId in group.containedNodeIds ?? new List<string>())
                {
                    if (PungentAuthoringId.EqualsId(containedNodeId, nodeId))
                        return group;
                }
            }

            return null;
        }

        private static bool IsNodeHiddenByCollapsedGroup(PungentBoardDocument document, string nodeId)
        {
            return FindCollapsedGroupForNode(document, nodeId) != null;
        }

        private static bool IsInternalEdgeHiddenByCollapsedGroup(PungentBoardDocument document, PungentBoardEdge edge)
        {
            if (document == null || edge == null)
                return false;

            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
            {
                if (!IsCollapsedGroupNode(group))
                    continue;

                List<string> contained = group.containedNodeIds ?? new List<string>();
                bool fromInside = contained.Any(nodeId => PungentAuthoringId.EqualsId(nodeId, edge.fromNodeId));
                bool toInside = contained.Any(nodeId => PungentAuthoringId.EqualsId(nodeId, edge.toNodeId));
                if (fromInside && toInside)
                    return true;
            }

            return false;
        }

        private static bool TryGetCollapsedGroupPortAnchorForEdge(PungentBoardDocument document, PungentBoardEdge edge, bool sourceSide, PungentBoardCanvasViewState state, out Vector2 anchor)
        {
            anchor = Vector2.zero;
            if (edge == null)
                return false;

            string nodeId = sourceSide ? edge.fromNodeId : edge.toNodeId;
            string internalPortKey = sourceSide ? edge.fromPortKey : edge.toPortKey;
            bool inputSide = !sourceSide;
            PungentBoardGroup group = FindCollapsedGroupForNode(document, nodeId);
            if (!IsCollapsedGroupNode(group))
                return false;

            Rect localRect = VisibleGroupLocalRect(group, state);
            List<PungentBoardPortDefinition> ports = inputSide ? GroupInputPorts(group) : GroupOutputPorts(group);
            if (ports.Count == 0)
            {
                anchor = inputSide ? LeftPortRect(localRect).center : RightPortRect(localRect).center;
                return true;
            }

            PungentBoardCustomNodePortMapping mapping = FindMappingForInternalEndpoint(group, nodeId, internalPortKey, inputSide);
            if (mapping != null)
            {
                int index = ports.FindIndex(port => port != null && string.Equals(port.key, mapping.exposedPortKey, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    anchor = PortRectAt(localRect, ports, index, inputSide).center;
                    return true;
                }
            }

            anchor = inputSide ? LeftPortRect(localRect).center : RightPortRect(localRect).center;
            return true;
        }

        private static bool TryGetCollapsedGroupConnectorAnchor(PungentBoardDocument document, string nodeId, string internalPortKey, bool inputSide, PungentBoardCanvasViewState state, out Vector2 anchor)
        {
            anchor = Vector2.zero;
            PungentBoardGroup group = FindCollapsedGroupForNode(document, nodeId);
            if (!IsCollapsedGroupNode(group))
                return false;

            Rect localRect = VisibleGroupLocalRect(group, state);
            List<PungentBoardPortDefinition> ports = inputSide ? GroupInputPorts(group) : GroupOutputPorts(group);
            if (ports.Count == 0)
            {
                anchor = inputSide ? LeftPortRect(localRect).center : RightPortRect(localRect).center;
                return true;
            }

            PungentBoardCustomNodePortMapping mapping = FindMappingForInternalEndpoint(group, nodeId, internalPortKey, inputSide);
            if (mapping != null)
            {
                int index = ports.FindIndex(port => port != null && string.Equals(port.key, mapping.exposedPortKey, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    anchor = PortRectAt(localRect, ports, index, inputSide).center;
                    return true;
                }
            }

            anchor = inputSide ? LeftPortRect(localRect).center : RightPortRect(localRect).center;
            return true;
        }

        private static PungentBoardCustomNodePortMapping FindMappingForInternalEndpoint(PungentBoardGroup group, string internalNodeId, string internalPortKey, bool inputSide)
        {
            if (group == null || string.IsNullOrWhiteSpace(internalNodeId))
                return null;

            foreach (PungentBoardCustomNodePortMapping mapping in group.exposedPortMappings ?? new List<PungentBoardCustomNodePortMapping>())
            {
                if (mapping == null || !PungentAuthoringId.EqualsId(mapping.internalNodeId, internalNodeId))
                    continue;
                if (!string.IsNullOrWhiteSpace(internalPortKey) && !string.Equals(mapping.internalPortKey, internalPortKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool directionMatches = mapping.direction == PungentBoardPortDirection.Both ||
                                        (inputSide && mapping.direction == PungentBoardPortDirection.Input) ||
                                        (!inputSide && mapping.direction == PungentBoardPortDirection.Output);
                if (directionMatches)
                    return mapping;
            }

            return null;
        }

        private static void DrawGroups(Rect localViewRect, PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
            {
                if (group == null)
                    continue;

                Rect visibleRect = VisibleGroupLocalRect(group, state);
                if (!Expanded(localViewRect, 160f).Overlaps(visibleRect))
                    continue;

                bool selected = state.IsGroupSelected(group.id);
                if (IsCollapsedGroupNode(group))
                {
                    DrawCollapsedGroupNode(document, group, visibleRect, selected, state);
                    continue;
                }

                Color fill = StyleColor(group.colorStyleKey, 0.16f);
                if (group.locked)
                    fill = new Color(fill.r * 0.75f, fill.g * 0.75f, fill.b * 0.75f, fill.a * 0.78f);
                EditorGUI.DrawRect(visibleRect, fill);
                Handles.BeginGUI();
                Handles.color = selected ? new Color(1f, 0.72f, 0.28f, 0.95f) : new Color(1f, 1f, 1f, 0.22f);
                Handles.DrawAAPolyLine(selected ? 3f : 1.5f,
                    new Vector3(visibleRect.xMin, visibleRect.yMin),
                    new Vector3(visibleRect.xMax, visibleRect.yMin),
                    new Vector3(visibleRect.xMax, visibleRect.yMax),
                    new Vector3(visibleRect.xMin, visibleRect.yMax),
                    new Vector3(visibleRect.xMin, visibleRect.yMin));
                Handles.EndGUI();
                GUI.Label(new Rect(visibleRect.x + 10f, visibleRect.y + 7f, Mathf.Max(80f, visibleRect.width - 76f), 22f), group.title, _groupTitleStyle);
                if (group.locked)
                    GUI.Label(new Rect(visibleRect.xMax - 68f, visibleRect.y + 7f, 34f, 18f), "Lock", _nodeBadgeStyle);
                GUI.Label(CollapseButtonRect(visibleRect), group.collapsed ? "+" : "-", _nodeBadgeStyle);
                if (selected && !group.locked)
                    EditorGUI.DrawRect(ResizeHandleRect(visibleRect), new Color(1f, 0.72f, 0.28f, 0.92f));
            }
        }

        private static void DrawCollapsedGroupNode(PungentBoardDocument document, PungentBoardGroup group, Rect rect, bool selected, PungentBoardCanvasViewState state)
        {
            Color fill = StyleColor(group.colorStyleKey, selected ? 0.42f : 0.28f);
            if (group.locked)
                fill = new Color(fill.r * 0.76f, fill.g * 0.76f, fill.b * 0.76f, fill.a * 0.84f);

            EditorGUI.DrawRect(rect, fill);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(46f, rect.height));
            EditorGUI.DrawRect(headerRect, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.14f) : new Color(1f, 1f, 1f, 0.16f));

            Handles.BeginGUI();
            Handles.color = selected ? new Color(1f, 0.72f, 0.28f, 0.95f) : new Color(1f, 1f, 1f, 0.24f);
            Handles.DrawAAPolyLine(selected ? 3f : 1.5f,
                new Vector3(rect.xMin, rect.yMin),
                new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin));
            Handles.EndGUI();

            Event evt = Event.current;
            bool hovered = evt != null && rect.Contains(evt.mousePosition);
            bool showInlineControls = selected || hovered;
            Rect titleRect = new Rect(rect.x + 10f, rect.y + 7f, Mathf.Max(36f, rect.width - (showInlineControls ? 154f : 44f)), 20f);
            DrawClippedLabel(titleRect, string.IsNullOrWhiteSpace(group.title) ? "Custom Group" : group.title, _nodeTitleStyle);
            if (!showInlineControls)
                GUI.Label(CollapseButtonRect(rect), "+", _nodeBadgeStyle);

            float badgeX = rect.x + 10f;
            GUI.Label(new Rect(badgeX, rect.y + 28f, 44f, 16f), "Group", _nodeBadgeStyle);
            badgeX += 48f;
            if (group.customNodeEnabled || !string.IsNullOrWhiteSpace(group.customNodeSourceTemplateId))
            {
                GUI.Label(new Rect(badgeX, rect.y + 28f, 48f, 16f), "Custom", _nodeBadgeStyle);
                badgeX += 52f;
            }
            if (group.locked)
                GUI.Label(new Rect(badgeX, rect.y + 28f, 36f, 16f), "Lock", _nodeBadgeStyle);

            if (state != null && state.zoom >= SimplifiedZoom && rect.height >= 74f)
            {
                int contained = group.containedNodeIds?.Count ?? 0;
                int inputs = GroupInputPorts(group).Count;
                int outputs = GroupOutputPorts(group).Count;
                string summary = contained + " internal node" + (contained == 1 ? string.Empty : "s") + " | " + inputs + " in / " + outputs + " out";
                DrawClippedLabel(new Rect(rect.x + 12f, rect.y + 54f, Mathf.Max(20f, rect.width - 24f), 18f), summary, _nodeBadgeStyle);
            }

            DrawGroupPortList(document, group, GroupInputPorts(group), true, rect, state, selected);
            DrawGroupPortList(document, group, GroupOutputPorts(group), false, rect, state, selected);
            if (showInlineControls)
                DrawCollapsedGroupInlineControls(document, group, rect, state);
            if (selected && !group.locked)
                EditorGUI.DrawRect(ResizeHandleRect(rect), new Color(1f, 0.72f, 0.28f, 0.92f));
        }

        private static void DrawCollapsedGroupInlineControls(PungentBoardDocument document, PungentBoardGroup group, Rect rect, PungentBoardCanvasViewState state)
        {
            if (document == null || group == null || state == null || rect.width < 126f)
                return;

            Rect row = new Rect(rect.xMax - 144f, rect.y + 5f, 136f, 20f);
            if (SmallNodeButton(ref row, "+", "Expand this collapsed group node."))
            {
                BeginHistory(document, state, "Expand Group");
                group.collapsed = false;
                state.SelectGroup(group.id);
                state.contentChanged = true;
                state.repaintRequested = true;
            }

            if (SmallNodeButton(ref row, "Tpl", "Save or update this group-backed custom node template."))
            {
                state.SelectGroup(group.id);
                state.saveGroupTemplateRequested?.Invoke(group);
            }

            if (SmallNodeButton(ref row, "Ports", "Select this group and edit exposed ports in the inspector."))
            {
                state.SelectGroup(group.id);
                state.repaintRequested = true;
            }

            string templateId = !string.IsNullOrWhiteSpace(group.customNodeSourceTemplateId) ? group.customNodeSourceTemplateId : group.customNodeTemplateId;
            if (!string.IsNullOrWhiteSpace(templateId) && SmallNodeButton(ref row, "D", "Duplicate this custom group node instance from its template."))
            {
                state.instantiateCustomGroupTemplateRequested?.Invoke(templateId, group.rect.center + new Vector2(36f, 36f));
            }
        }

        private static void DrawGroupPortList(PungentBoardDocument document, PungentBoardGroup group, List<PungentBoardPortDefinition> ports, bool left, Rect rect, PungentBoardCanvasViewState state, bool active)
        {
            int count = ports != null ? ports.Count : 0;
            if (count == 0)
                return;

            bool showLabels = state != null && state.zoom >= PortLabelZoom && rect.width >= 168f && rect.height >= 100f && HasVisiblePortLabels(ports);
            float labelWidth = showLabels ? Mathf.Clamp(rect.width * 0.26f, 48f, 92f) : 0f;
            for (int i = 0; i < count; i++)
            {
                PungentBoardPortDefinition port = ports[i];
                Rect portRect = PortRectAt(rect, ports, i, left);
                bool mapped = FindPortMapping(group, port != null ? port.key : string.Empty, left) != null;
                Color fallback = active ? new Color(0.36f, 1f, 0.72f, 0.95f) : new Color(0.72f, 0.9f, 1f, 0.58f);
                Color socketColor = mapped
                    ? GroupConnectionPortColor(document, state, group, port, left, PortSemanticColor(port, left, fallback))
                    : new Color(0.95f, 0.45f, 0.22f, active ? 0.92f : 0.70f);
                DrawPortSocket(portRect, socketColor, port, left);
                if (showLabels && ShouldShowGroupPortLabel(port, ports))
                {
                    string label = TruncatePortLabel(PortLabel(port), labelWidth - 8f);
                    float y = Mathf.Clamp(portRect.center.y - 8f, rect.y + 48f, rect.yMax - 18f);
                    Rect labelRect = left
                        ? new Rect(rect.x + 8f, y, labelWidth - 8f, 16f)
                        : new Rect(rect.xMax - labelWidth, y, labelWidth - 8f, 16f);
                    DrawClippedLabel(labelRect, label, _nodePortLabelStyle);
                }
            }
        }

        private static bool ShouldShowGroupPortLabel(PungentBoardPortDefinition port, List<PungentBoardPortDefinition> ports)
        {
            return port != null && IsMeaningfulPortLabel(port, ports);
        }

        private static Color GroupConnectionPortColor(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardGroup group, PungentBoardPortDefinition port, bool inputSide, Color fallback)
        {
            if (document == null || state == null || group == null || !state.connectorMode || string.IsNullOrWhiteSpace(state.connectorSourceNodeId))
                return fallback;

            PungentBoardCustomNodePortMapping mapping = FindPortMapping(group, port != null ? port.key : string.Empty, inputSide);
            if (mapping == null || string.IsNullOrWhiteSpace(mapping.internalNodeId))
                return new Color(0.95f, 0.45f, 0.22f, 0.78f);

            if (PungentAuthoringId.EqualsId(state.connectorSourceNodeId, mapping.internalNodeId))
                return inputSide ? new Color(0.55f, 0.55f, 0.55f, 0.70f) : new Color(0.36f, 1f, 0.72f, 0.96f);

            if (!inputSide)
                return new Color(0.55f, 0.55f, 0.55f, 0.58f);

            string message;
            bool valid = CanCreateTypedEdge(document, state, state.connectorSourceNodeId, mapping.internalNodeId, state.connectorSourcePortKey, mapping.internalPortKey, out message);
            return valid ? new Color(0.32f, 0.92f, 0.52f, 0.96f) : new Color(0.95f, 0.36f, 0.26f, 0.84f);
        }

        private static void DrawEdges(Rect localViewRect, PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            Handles.BeginGUI();
            foreach (PungentBoardEdge edge in document.edges ?? new List<PungentBoardEdge>())
            {
                if (edge == null)
                    continue;
                if (IsInternalEdgeHiddenByCollapsedGroup(document, edge))
                    continue;

                PungentBoardNode from = FindNode(document, edge.fromNodeId);
                PungentBoardNode to = FindNode(document, edge.toNodeId);
                if (from == null || to == null)
                    continue;

                Vector2 a;
                Vector2 b;
                GetEdgeLocalEndpoints(document, edge, state, out a, out b);
                if (!Expanded(localViewRect, 80f).Overlaps(RectFromPoints(a, b)))
                    continue;

                bool selected = PungentAuthoringId.EqualsId(edge.id, state.selectedEdgeId);
                Handles.color = selected ? new Color(1f, 0.7f, 0.25f, 1f) : new Color(0.62f, 0.78f, 0.92f, 0.72f);
                DrawEdgeLine(edge, a, b, selected);
                if (edge.directed)
                    DrawArrowHead(a, b, selected);
            }
            Handles.EndGUI();

            foreach (PungentBoardEdge edge in document.edges ?? new List<PungentBoardEdge>())
            {
                string label = EdgeCanvasLabel(document, edge);
                if (edge == null || string.IsNullOrWhiteSpace(label))
                    continue;
                if (IsInternalEdgeHiddenByCollapsedGroup(document, edge))
                    continue;

                PungentBoardNode from = FindNode(document, edge.fromNodeId);
                PungentBoardNode to = FindNode(document, edge.toNodeId);
                if (from == null || to == null)
                    continue;

                Vector2 a;
                Vector2 b;
                GetEdgeLocalEndpoints(document, edge, state, out a, out b);
                Vector2 midpoint = (a + b) * 0.5f;
                if (!Expanded(localViewRect, 40f).Contains(midpoint))
                    continue;
                GUI.Label(new Rect(midpoint.x - 70f, midpoint.y - 12f, 140f, 20f), label, _edgeLabelStyle);
            }
        }

        private static string EdgeCanvasLabel(PungentBoardDocument document, PungentBoardEdge edge)
        {
            if (edge == null)
                return string.Empty;

            if (PungentBoardEventSequenceUtility.IsEventSequence(document) && PungentBoardEventSequenceUtility.IsEventSequenceEdgeType(edge.edgeTypeKey))
                return PungentBoardEventSequenceUtility.EdgeDisplayLabel(document, edge);

            return edge.label ?? string.Empty;
        }

        private static void GetEdgeLocalEndpoints(PungentBoardDocument document, PungentBoardEdge edge, PungentBoardCanvasViewState state, out Vector2 fromPoint, out Vector2 toPoint)
        {
            PungentBoardNode from = FindNode(document, edge != null ? edge.fromNodeId : string.Empty);
            PungentBoardNode to = FindNode(document, edge != null ? edge.toNodeId : string.Empty);
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document != null ? document.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            PungentBoardNodeTypeDefinition fromType = ResolveNodeType(graphType, from);
            PungentBoardNodeTypeDefinition toType = ResolveNodeType(graphType, to);
            if (!TryGetCollapsedGroupPortAnchorForEdge(document, edge, true, state, out fromPoint))
                fromPoint = GetPortAnchorLocal(from, fromType, edge != null ? edge.fromPortKey : string.Empty, false, state);
            if (!TryGetCollapsedGroupPortAnchorForEdge(document, edge, false, state, out toPoint))
                toPoint = GetPortAnchorLocal(to, toType, edge != null ? edge.toPortKey : string.Empty, true, state);
        }

        private static void DrawEdgeLine(PungentBoardEdge edge, Vector2 a, Vector2 b, bool selected)
        {
            float width = selected ? 4f : 2.25f;
            string style = (edge?.styleKey ?? string.Empty).Trim().ToLowerInvariant();
            if (style == "elbow")
            {
                Vector2 mid = new Vector2((a.x + b.x) * 0.5f, a.y);
                Vector2 mid2 = new Vector2((a.x + b.x) * 0.5f, b.y);
                Handles.DrawAAPolyLine(width, new Vector3(a.x, a.y), new Vector3(mid.x, mid.y), new Vector3(mid2.x, mid2.y), new Vector3(b.x, b.y));
                return;
            }

            if (style == "curve")
            {
                Vector2 tangent = new Vector2(Mathf.Clamp(Mathf.Abs(b.x - a.x) * 0.45f, 50f, 240f), 0f);
                Handles.DrawBezier(a, b, a + tangent, b - tangent, Handles.color, null, width);
                return;
            }

            Handles.DrawAAPolyLine(width, new Vector3(a.x, a.y), new Vector3(b.x, b.y));
        }

        private static void DrawArrowHead(Vector2 from, Vector2 to, bool selected)
        {
            Vector2 dir = (to - from).normalized;
            if (dir.sqrMagnitude < 0.001f)
                return;

            Vector2 right = new Vector2(-dir.y, dir.x);
            Vector2 tip = to - dir * 18f;
            Vector2 p1 = tip - dir * 12f + right * 6f;
            Vector2 p2 = tip - dir * 12f - right * 6f;
            Handles.DrawAAPolyLine(selected ? 4f : 2.25f, new Vector3(p1.x, p1.y), new Vector3(tip.x, tip.y), new Vector3(p2.x, p2.y));
        }

        private static void DrawNodes(Rect localViewRect, PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;
                if (IsNodeHiddenByCollapsedGroup(document, node.id))
                    continue;

                Rect rect = CanvasToLocal(node.Rect, state);
                if (!Expanded(localViewRect, 80f).Overlaps(rect))
                    continue;

                PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document.graphTypeId);
                PungentBoardNodeTypeDefinition nodeType = ResolveNodeType(graphType, node);
                bool selected = state.IsNodeSelected(node.id);
                bool connectorSource = PungentAuthoringId.EqualsId(node.id, state.connectorSourceNodeId);
                bool missing = IsMissingLinkedItem(node);
                Color fill = StyleColor(node.colorStyleKey, selected ? 0.44f : 0.30f);
                if (missing)
                    fill = new Color(0.62f, 0.34f, 0.24f, selected ? 0.62f : 0.46f);

                EditorGUI.DrawRect(rect, fill);
                Handles.BeginGUI();
                Handles.color = selected ? new Color(1f, 0.77f, 0.32f, 1f) : connectorSource ? new Color(0.36f, 1f, 0.72f, 1f) : new Color(1f, 1f, 1f, 0.22f);
                Handles.DrawAAPolyLine(selected || connectorSource ? 3f : 1.5f,
                    new Vector3(rect.xMin, rect.yMin),
                    new Vector3(rect.xMax, rect.yMin),
                    new Vector3(rect.xMax, rect.yMax),
                    new Vector3(rect.xMin, rect.yMax),
                    new Vector3(rect.xMin, rect.yMin));
                Handles.EndGUI();

                List<PungentBoardPortDefinition> inputs = InputPortsOrDefault(nodeType);
                List<PungentBoardPortDefinition> outputs = OutputPortsOrDefault(nodeType);
                PungentBoardNodeVisualLayout layout = BuildNodeVisualLayout(rect, node, state, missing, inputs, outputs);
                Event evt = Event.current;
                bool hovered = evt != null && rect.Contains(evt.mousePosition);
                bool inlineEditingTitle = state.inlineEdit.IsEditing(node.id, PungentBoardInlineNodeEditField.Title);
                bool inlineEditingBody = state.inlineEdit.IsEditing(node.id, PungentBoardInlineNodeEditField.Body);
                bool showInlineControls = selected || hovered || inlineEditingTitle || inlineEditingBody;
                if (showInlineControls)
                    layout.titleRect.width = Mathf.Max(18f, layout.titleRect.width - Mathf.Min(138f, rect.width * 0.46f));

                EditorGUI.DrawRect(layout.headerRect, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.12f) : new Color(1f, 1f, 1f, 0.14f));
                if (rect.height > layout.headerHeight + 2f)
                    EditorGUI.DrawRect(layout.dividerRect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.14f) : new Color(0f, 0f, 0f, 0.12f));

                if (inlineEditingTitle)
                    DrawInlineTextField(document, state, node, layout.titleRect, PungentBoardInlineNodeEditField.Title, _nodeTitleStyle);
                else
                    DrawClippedLabel(layout.titleRect, string.IsNullOrWhiteSpace(node.title) ? "Untitled Node" : node.title, _nodeTitleStyle);
                if (!showInlineControls && rect.width >= 46f && rect.height >= 30f)
                    GUI.Label(layout.collapseRect, node.collapsed ? "+" : "-", _nodeBadgeStyle);
                if (layout.showMeta)
                    DrawNodeStatusBadges(layout.typeRect, node, nodeType, missing);

                if (layout.showMissingStatus)
                    DrawClippedLabel(layout.statusRect, "Missing linked provider/item", _nodeBadgeStyle);
                else if (missing && !layout.showMeta && rect.height >= 48f)
                    DrawClippedLabel(new Rect(rect.x + 10f, rect.y + 31f, Mathf.Max(16f, rect.width - 20f), 18f), "Missing", _nodeBadgeStyle);
                else if (inlineEditingBody)
                    DrawInlineTextField(document, state, node, layout.bodyRect, PungentBoardInlineNodeEditField.Body, _nodeBodyStyle);
                else if (layout.showBody)
                {
                    string source = string.IsNullOrWhiteSpace(node.body) ? node.summary : node.body;
                    DrawClippedLabel(layout.bodyRect, PreviewMultilineText(source, layout.bodyRect, state.zoom), _nodeBodyStyle);
                }

                DrawNodePorts(document, node, inputs, outputs, layout, rect, state, connectorSource || selected);
                if (showInlineControls)
                    DrawNodeInlineControls(document, state, node, rect, layout, inlineEditingTitle || inlineEditingBody);
                if (selected)
                    EditorGUI.DrawRect(ResizeHandleRect(rect), new Color(1f, 0.77f, 0.32f, 0.95f));
            }
        }

        private static PungentBoardNodeVisualLayout BuildNodeVisualLayout(Rect rect, PungentBoardNode node, PungentBoardCanvasViewState state, bool missing, List<PungentBoardPortDefinition> inputs, List<PungentBoardPortDefinition> outputs)
        {
            PungentBoardNodeVisualLayout layout = new PungentBoardNodeVisualLayout();
            float zoom = state != null ? state.zoom : 1f;
            layout.showMeta = zoom >= SimplifiedZoom && rect.width >= 86f && rect.height >= 50f;
            layout.headerHeight = GetNodeHeaderHeight(rect);
            layout.headerRect = new Rect(rect.x, rect.y, rect.width, Mathf.Min(layout.headerHeight, rect.height));
            layout.dividerRect = new Rect(rect.x + 8f, layout.headerRect.yMax - 1f, Mathf.Max(1f, rect.width - 16f), 1f);
            layout.titleRect = new Rect(rect.x + 10f, rect.y + 7f, Mathf.Max(16f, rect.width - 42f), 22f);
            layout.collapseRect = CollapseButtonRect(rect);
            layout.controlsRect = new Rect(rect.xMax - 146f, rect.y + 5f, 138f, 20f);
            layout.typeRect = new Rect(rect.x + 10f, rect.y + 31f, Mathf.Max(16f, rect.width - 20f), 18f);

            layout.showPortLabels = zoom >= PortLabelZoom && rect.width >= 168f && rect.height >= 102f;
            layout.hasInputLabels = layout.showPortLabels && HasVisiblePortLabels(inputs);
            layout.hasOutputLabels = layout.showPortLabels && HasVisiblePortLabels(outputs);
            layout.inputLabelWidth = layout.hasInputLabels ? Mathf.Clamp(rect.width * 0.24f, 44f, 82f) : 0f;
            layout.outputLabelWidth = layout.hasOutputLabels ? Mathf.Clamp(rect.width * 0.24f, 44f, 82f) : 0f;

            float bodyTop = rect.y + layout.headerHeight + 8f;
            float bodyX = rect.x + 12f + layout.inputLabelWidth;
            float bodyRightPadding = 12f + layout.outputLabelWidth;
            layout.bodyRect = new Rect(bodyX, bodyTop, rect.width - 12f - layout.inputLabelWidth - bodyRightPadding, rect.yMax - bodyTop - 10f);
            layout.bodyRect.width = Mathf.Max(0f, layout.bodyRect.width);
            layout.bodyRect.height = Mathf.Max(0f, layout.bodyRect.height);
            layout.showBody = node != null && !node.collapsed && !missing && zoom >= BodyTextZoom && layout.bodyRect.width >= 64f && layout.bodyRect.height >= 22f;
            layout.statusRect = layout.bodyRect.height >= 18f
                ? new Rect(layout.bodyRect.x, layout.bodyRect.y, layout.bodyRect.width, Mathf.Min(18f, layout.bodyRect.height))
                : Rect.zero;
            layout.showMissingStatus = missing && layout.showMeta && layout.statusRect.width >= 48f && layout.statusRect.height >= 14f;
            return layout;
        }

        private static void DrawNodePorts(PungentBoardDocument document, PungentBoardNode node, List<PungentBoardPortDefinition> inputs, List<PungentBoardPortDefinition> outputs, PungentBoardNodeVisualLayout layout, Rect rect, PungentBoardCanvasViewState state, bool active)
        {
            Color color = active ? new Color(0.36f, 1f, 0.72f, 0.95f) : new Color(0.72f, 0.9f, 1f, 0.55f);
            DrawPortList(document, node, rect, inputs, true, color, layout, state);
            DrawPortList(document, node, rect, outputs, false, color, layout, state);
        }

        private static void DrawNodeStatusBadges(Rect rect, PungentBoardNode node, PungentBoardNodeTypeDefinition nodeType, bool missing)
        {
            string label = NodeTypeLabel(node, nodeType);
            if (node != null && node.linkedAuthoringRef != null && node.linkedAuthoringRef.HasItemId)
                label += " | Link";
            if (HasProjectBinding(node))
                label += " | Bind";
            if (node != null && !string.IsNullOrWhiteSpace(node.syncState) && !string.Equals(node.syncState, "Synced", StringComparison.OrdinalIgnoreCase))
                label += " | " + node.syncState;
            if (missing)
                label += " | Missing";
            DrawClippedLabel(rect, label, _nodeBadgeStyle);
        }

        private static bool HasProjectBinding(PungentBoardNode node)
        {
            if (node == null)
                return false;

            return !string.IsNullOrWhiteSpace(node.projectBindingLinkId) ||
                   !string.IsNullOrWhiteSpace(node.projectBindingEndpointId) ||
                   (node.projectBindingPath != null && (node.projectBindingPath.segments?.Count ?? 0) > 0);
        }

        private static void DrawInlineTextField(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNode node, Rect rect, PungentBoardInlineNodeEditField field, GUIStyle style)
        {
            if (document == null || state == null || node == null || rect.width < 8f || rect.height < 8f)
                return;

            string controlName = "PungentBoardInline_" + field + "_" + node.id;
            GUI.SetNextControlName(controlName);
            if (field == PungentBoardInlineNodeEditField.Body)
                state.inlineEdit.draft = GUI.TextArea(rect, state.inlineEdit.draft ?? string.Empty, style);
            else
                state.inlineEdit.draft = GUI.TextField(rect, state.inlineEdit.draft ?? string.Empty, style);

            if (state.inlineEdit.requestFocus)
            {
                GUI.FocusControl(controlName);
                state.inlineEdit.requestFocus = false;
            }

            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
                return;

            if (evt.keyCode == KeyCode.Escape)
            {
                state.inlineEdit.Clear();
                state.repaintRequested = true;
                evt.Use();
            }
            else if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) &&
                     (field == PungentBoardInlineNodeEditField.Title || evt.control || evt.command))
            {
                CommitInlineNodeEdit(document, state, node);
                evt.Use();
            }
        }

        private static void DrawNodeInlineControls(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNode node, Rect rect, PungentBoardNodeVisualLayout layout, bool editing)
        {
            if (document == null || state == null || node == null || rect.width < 96f || rect.height < 30f)
                return;

            Rect row = layout.controlsRect;
            if (row.x < rect.x + 48f)
                row.x = rect.x + 48f;
            row.width = Mathf.Min(row.width, rect.xMax - row.x - 6f);
            if (row.width < 42f)
                return;

            if (editing)
            {
                if (SmallNodeButton(ref row, "OK", "Commit inline edit. Body text also commits with Ctrl/Cmd+Enter."))
                    CommitInlineNodeEdit(document, state, node);
                if (SmallNodeButton(ref row, "X", "Cancel inline edit."))
                {
                    state.inlineEdit.Clear();
                    state.repaintRequested = true;
                }
                return;
            }

            if (SmallNodeButton(ref row, "T", "Rename this node on the canvas."))
                state.inlineEdit.Begin(node, PungentBoardInlineNodeEditField.Title);

            if (SmallNodeButton(ref row, "B", "Edit body preview text on the canvas."))
            {
                if (node.collapsed)
                {
                    BeginHistory(document, state, "Expand Node Preview");
                    node.collapsed = false;
                    state.contentChanged = true;
                }
                state.inlineEdit.Begin(node, PungentBoardInlineNodeEditField.Body);
            }

            if (SmallNodeButton(ref row, node.collapsed ? "+" : "-", node.collapsed ? "Expand body preview." : "Collapse body preview."))
            {
                BeginHistory(document, state, "Toggle Node Preview");
                node.collapsed = !node.collapsed;
                state.SelectNode(node.id);
                state.contentChanged = true;
                state.repaintRequested = true;
            }

            if (SmallNodeButton(ref row, "D", "Duplicate selected node(s)."))
            {
                state.SelectNode(node.id);
                state.commandRequested?.Invoke(PungentBoardGraphCommand.DuplicateSelection, "inline node");
            }

            if (SmallNodeButton(ref row, ">", "Start an edge from this node."))
            {
                state.SelectNode(node.id);
                state.connectorMode = true;
                state.connectorDragging = false;
                state.connectorSourceNodeId = node.id;
                state.connectorSourcePortKey = string.Empty;
                state.connectionWarning = string.Empty;
                state.repaintRequested = true;
            }

            if (SmallNodeButton(ref row, "S", "Choose a node style/color."))
                ShowNodeStyleMenu(document, state, node);

            if (CanOpenLinkedAuthoringItem(node.linkedAuthoringRef) && SmallNodeButton(ref row, "O", "Open linked authoring item."))
                PungentAuthoringProviderRegistry.TryOpen(node.linkedAuthoringRef);

            if (SmallNodeButton(ref row, "X", "Delete this node after confirmation."))
            {
                state.SelectNode(node.id);
                state.commandRequested?.Invoke(PungentBoardGraphCommand.DeleteSelection, "inline node");
            }
        }

        private static bool SmallNodeButton(ref Rect row, string label, string tooltip)
        {
            float width = label != null && label.Length > 1 ? 30f : 20f;
            if (row.width < width)
                return false;

            Rect buttonRect = new Rect(row.x, row.y, width, row.height);
            row.x += width + 3f;
            row.width -= width + 3f;
            return GUI.Button(buttonRect, new GUIContent(label, tooltip), EditorStyles.miniButton);
        }

        private static void CommitInlineNodeEdit(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNode node)
        {
            if (document == null || state == null || node == null || !PungentAuthoringId.EqualsId(state.inlineEdit.nodeId, node.id))
                return;

            string next = state.inlineEdit.draft ?? string.Empty;
            if (state.inlineEdit.field == PungentBoardInlineNodeEditField.Title)
            {
                next = string.IsNullOrWhiteSpace(next) ? "Untitled Node" : next.Trim();
                if (!string.Equals(node.title ?? string.Empty, next, StringComparison.Ordinal))
                {
                    BeginHistory(document, state, "Rename Node");
                    node.title = next;
                    state.contentChanged = true;
                }
            }
            else if (state.inlineEdit.field == PungentBoardInlineNodeEditField.Body)
            {
                if (!string.Equals(node.body ?? string.Empty, next, StringComparison.Ordinal))
                {
                    BeginHistory(document, state, "Edit Node Body");
                    node.body = next;
                    state.contentChanged = true;
                }
            }

            node.NormalizeInPlace();
            state.inlineEdit.Clear();
            state.SelectNode(node.id);
            state.repaintRequested = true;
        }

        private static void ShowNodeStyleMenu(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNode node)
        {
            if (document == null || state == null || node == null)
                return;

            GenericMenu menu = new GenericMenu();
            string[] styles = { "note", "freeform", "task", "document", "utility", "token", "event", "condition", "action", "branch", "skill", "warning", "success", "group" };
            foreach (string style in styles)
            {
                string captured = style;
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(captured)), string.Equals(node.colorStyleKey, captured, StringComparison.OrdinalIgnoreCase), () =>
                {
                    BeginHistory(document, state, "Set Node Style");
                    node.colorStyleKey = captured;
                    node.NormalizeInPlace();
                    state.SelectNode(node.id);
                    state.contentChanged = true;
                    state.repaintRequested = true;
                });
            }
            menu.ShowAsContext();
        }

        private static void DrawPortList(PungentBoardDocument document, PungentBoardNode node, Rect rect, List<PungentBoardPortDefinition> ports, bool left, Color color, PungentBoardNodeVisualLayout layout, PungentBoardCanvasViewState state)
        {
            int count = Mathf.Max(1, ports != null ? ports.Count : 0);
            for (int i = 0; i < count; i++)
            {
                PungentBoardPortDefinition port = ports != null && i < ports.Count ? ports[i] : null;
                Rect portRect = PortRectAt(rect, ports, i, left);
                Color socketColor = ConnectionPortColor(document, state, node, port, left, PortSemanticColor(port, left, color));
                DrawPortSocket(portRect, socketColor, port, left);
                if (ShouldShowPortLabel(port, ports, left, layout))
                {
                    float labelWidth = left ? layout.inputLabelWidth : layout.outputLabelWidth;
                    if (labelWidth <= 12f)
                        continue;

                    string label = TruncatePortLabel(PortLabel(port), labelWidth - 8f);
                    float y = Mathf.Clamp(portRect.center.y - 8f, rect.y + layout.headerHeight + 2f, rect.yMax - 18f);
                    Rect labelRect = left
                        ? new Rect(rect.x + 8f, y, labelWidth - 8f, 16f)
                        : new Rect(rect.xMax - labelWidth, y, labelWidth - 8f, 16f);
                    DrawClippedLabel(labelRect, label, _nodePortLabelStyle);
                }
            }
        }

        private static Color ConnectionPortColor(PungentBoardDocument document, PungentBoardCanvasViewState state, PungentBoardNode node, PungentBoardPortDefinition port, bool inputSide, Color fallback)
        {
            if (document == null || state == null || node == null || !state.connectorMode || string.IsNullOrWhiteSpace(state.connectorSourceNodeId))
                return fallback;

            if (PungentAuthoringId.EqualsId(state.connectorSourceNodeId, node.id))
                return inputSide ? new Color(0.55f, 0.55f, 0.55f, 0.70f) : new Color(0.36f, 1f, 0.72f, 0.96f);

            if (!inputSide)
                return new Color(0.55f, 0.55f, 0.55f, 0.58f);

            string message;
            bool valid = CanCreateTypedEdge(document, state, state.connectorSourceNodeId, node.id, state.connectorSourcePortKey, port != null ? port.key : string.Empty, out message);
            return valid ? new Color(0.32f, 0.92f, 0.52f, 0.96f) : new Color(0.95f, 0.36f, 0.26f, 0.84f);
        }

        private static float GetNodeHeaderHeight(Rect rect)
        {
            bool showMeta = rect.width >= 86f && rect.height >= 50f;
            float preferred = showMeta ? 54f : 32f;
            return Mathf.Min(preferred, Mathf.Max(24f, rect.height - 18f));
        }

        private static bool HasVisiblePortLabels(List<PungentBoardPortDefinition> ports)
        {
            if (ports == null || ports.Count == 0)
                return false;

            for (int i = 0; i < ports.Count; i++)
            {
                if (IsMeaningfulPortLabel(ports[i], ports))
                    return true;
            }

            return false;
        }

        private static bool ShouldShowPortLabel(PungentBoardPortDefinition port, List<PungentBoardPortDefinition> ports, bool left, PungentBoardNodeVisualLayout layout)
        {
            if (!layout.showPortLabels || port == null)
                return false;

            if (left && !layout.hasInputLabels)
                return false;

            if (!left && !layout.hasOutputLabels)
                return false;

            return IsMeaningfulPortLabel(port, ports);
        }

        private static bool IsMeaningfulPortLabel(PungentBoardPortDefinition port, List<PungentBoardPortDefinition> ports)
        {
            if (port == null)
                return false;

            string label = PortLabel(port);
            if (string.IsNullOrWhiteSpace(label) || IsGenericPortLabel(label))
                return false;

            if (port.required)
                return true;

            string key = PortSemanticKey(port);
            if (key.Contains("true") || key.Contains("false") || key.Contains("success") || key.Contains("failure") ||
                key.Contains("child") || key.Contains("parent") || key.Contains("option") || key.Contains("choice") ||
                key.Contains("condition") || key.Contains("decision") || key.Contains("require") || key.Contains("unlock") ||
                key.Contains("provide") || key.Contains("used") || key.Contains("transition") || key.Contains("complete") ||
                key.Contains("done") || key.Contains("next") || key.Contains("branch") || key.Contains("from") || key.Contains("to"))
                return true;

            int meaningfulCount = 0;
            foreach (PungentBoardPortDefinition item in ports ?? new List<PungentBoardPortDefinition>())
            {
                if (item != null && !IsGenericPortLabel(PortLabel(item)))
                    meaningfulCount++;
            }

            return meaningfulCount > 1;
        }

        private static bool IsGenericPortLabel(string label)
        {
            string clean = (label ?? string.Empty).Trim().ToLowerInvariant();
            return clean == "in" || clean == "input" || clean == "out" || clean == "output";
        }

        private static string PortLabel(PungentBoardPortDefinition port)
        {
            string raw = port == null ? string.Empty : string.IsNullOrWhiteSpace(port.displayName) ? port.key : port.displayName;
            raw = (raw ?? string.Empty).Trim().Replace("-", " ").Replace("_", " ");
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : ObjectNames.NicifyVariableName(raw);
        }

        private static string PortSemanticKey(PungentBoardPortDefinition port)
        {
            return ((port != null ? port.key : string.Empty) + " " + (port != null ? port.displayName : string.Empty)).Trim().ToLowerInvariant();
        }

        private static string TruncatePortLabel(string label, float width)
        {
            string clean = label ?? string.Empty;
            int maxChars = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(18f, width) / 6.2f), 3, 18);
            return clean.Length <= maxChars ? clean : clean.Substring(0, Mathf.Max(1, maxChars - 3)) + "...";
        }

        private static void DrawPortSocket(Rect portRect, Color color, PungentBoardPortDefinition port, bool inputSide)
        {
            Color outline = port != null && port.required
                ? new Color(1f, 0.86f, 0.36f, 0.98f)
                : new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.70f : 0.35f);
            EditorGUI.DrawRect(portRect, outline);

            Rect inner = new Rect(portRect.x + 2f, portRect.y + 2f, Mathf.Max(1f, portRect.width - 4f), Mathf.Max(1f, portRect.height - 4f));
            EditorGUI.DrawRect(inner, color);

            Rect directionStripe = inputSide
                ? new Rect(portRect.x + 1f, portRect.y + 2f, 2f, Mathf.Max(1f, portRect.height - 4f))
                : new Rect(portRect.xMax - 3f, portRect.y + 2f, 2f, Mathf.Max(1f, portRect.height - 4f));
            EditorGUI.DrawRect(directionStripe, new Color(1f, 1f, 1f, port != null && port.required ? 0.72f : 0.38f));
        }

        private static Color PortSemanticColor(PungentBoardPortDefinition port, bool inputSide, Color fallback)
        {
            if (port == null)
                return inputSide ? new Color(0.58f, 0.70f, 0.78f, fallback.a) : new Color(0.66f, 0.78f, 0.86f, fallback.a);

            string key = PortSemanticKey(port);
            float alpha = Mathf.Clamp(fallback.a, 0.48f, 0.98f);
            if (key.Contains("false") || key.Contains("failure") || key.Contains("fail") || key.Contains("error"))
                return new Color(0.92f, 0.34f, 0.30f, alpha);
            if (key.Contains("true") || key.Contains("success") || key.Contains("complete") || key.Contains("done"))
                return new Color(0.34f, 0.82f, 0.42f, alpha);
            if (key.Contains("condition") || key.Contains("decision") || key.Contains("branch") || key.Contains("option") || key.Contains("choice"))
                return new Color(0.86f, 0.68f, 0.28f, alpha);
            if (key.Contains("require") || key.Contains("depend") || key.Contains("used"))
                return new Color(0.72f, 0.52f, 0.86f, alpha);
            if (key.Contains("unlock") || key.Contains("provide"))
                return new Color(0.42f, 0.78f, 0.62f, alpha);
            if (key.Contains("child") || key.Contains("parent") || key.Contains("next") || key.Contains("transition") || key.Contains("flow") || key.Contains("sequence") || key.Contains("from") || key.Contains("to"))
                return new Color(0.34f, 0.72f, 0.96f, alpha);

            return fallback;
        }

        private static List<PungentBoardPortDefinition> InputPortsOrDefault(PungentBoardNodeTypeDefinition nodeType)
        {
            List<PungentBoardPortDefinition> ports = nodeType != null ? nodeType.InputPorts().ToList() : new List<PungentBoardPortDefinition>();
            if (ports.Count == 0)
                ports.Add(new PungentBoardPortDefinition { key = "in", displayName = "In", direction = PungentBoardPortDirection.Input });
            return ports;
        }

        private static List<PungentBoardPortDefinition> OutputPortsOrDefault(PungentBoardNodeTypeDefinition nodeType)
        {
            List<PungentBoardPortDefinition> ports = nodeType != null ? nodeType.OutputPorts().ToList() : new List<PungentBoardPortDefinition>();
            if (ports.Count == 0)
                ports.Add(new PungentBoardPortDefinition { key = "out", displayName = "Out", direction = PungentBoardPortDirection.Output });
            return ports;
        }

        private static Rect PortRectAt(Rect nodeRect, List<PungentBoardPortDefinition> ports, int index, bool left)
        {
            int count = Mathf.Max(1, ports != null ? ports.Count : 0);
            float headerHeight = GetNodeHeaderHeight(nodeRect);
            float top = nodeRect.y + headerHeight + 14f;
            float bottom = nodeRect.yMax - 18f;
            float y;
            if (bottom <= top + 2f)
            {
                float min = nodeRect.y + Mathf.Min(26f, Mathf.Max(12f, nodeRect.height * 0.45f));
                float max = nodeRect.yMax - 14f;
                y = Mathf.Clamp(nodeRect.y + nodeRect.height * 0.64f, min, Mathf.Max(min, max));
            }
            else
            {
                y = count == 1
                    ? Mathf.Lerp(top, bottom, 0.5f)
                    : Mathf.Lerp(top, bottom, (index + 1f) / (count + 1f));
            }

            return new Rect(left ? nodeRect.xMin - PortSize * 0.5f : nodeRect.xMax - PortSize * 0.5f, y - PortSize * 0.5f, PortSize, PortSize);
        }

        private static Vector2 GetPortAnchorLocal(PungentBoardNode node, PungentBoardNodeTypeDefinition nodeType, string portKey, bool inputSide, PungentBoardCanvasViewState state)
        {
            if (node == null)
                return Vector2.zero;

            Rect rect = CanvasToLocal(node.Rect, state);
            List<PungentBoardPortDefinition> ports = inputSide ? InputPortsOrDefault(nodeType) : OutputPortsOrDefault(nodeType);
            int index = string.IsNullOrWhiteSpace(portKey)
                ? -1
                : ports.FindIndex(port => port != null && string.Equals(port.key, portKey, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                return PortRectAt(rect, ports, index, inputSide).center;

            return inputSide ? new Vector2(rect.xMin, rect.center.y) : new Vector2(rect.xMax, rect.center.y);
        }

        private static void DrawConnectorHint(PungentBoardDocument document, PungentBoardCanvasViewState state, Rect localRect)
        {
            if (!state.connectorMode)
                return;

            string message = !string.IsNullOrWhiteSpace(state.connectionWarning)
                ? state.connectionWarning
                : string.IsNullOrWhiteSpace(state.connectorSourceNodeId)
                ? "Connector mode: click an output port or source node."
                : "Connector mode: release on a valid input port or target node.";
            EditorGUI.DrawRect(new Rect(localRect.x + 12f, localRect.y + 12f, 360f, 26f), string.IsNullOrWhiteSpace(state.connectionWarning) ? new Color(0.05f, 0.08f, 0.1f, 0.78f) : new Color(0.42f, 0.18f, 0.12f, 0.84f));
            GUI.Label(new Rect(localRect.x + 20f, localRect.y + 17f, 340f, 18f), message, EditorStyles.whiteMiniLabel);

            if (string.IsNullOrWhiteSpace(state.connectorSourceNodeId))
                return;

            PungentBoardNode source = FindNode(document, state.connectorSourceNodeId);
            if (source == null || Event.current == null)
                return;

            Handles.BeginGUI();
            bool lineValid = string.IsNullOrWhiteSpace(state.connectorHoverNodeId) || state.connectorHoverValid;
            Handles.color = lineValid ? new Color(0.36f, 1f, 0.72f, 0.75f) : new Color(0.95f, 0.46f, 0.32f, 0.70f);
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document != null ? document.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            Vector2 start;
            if (!TryGetCollapsedGroupConnectorAnchor(document, source.id, state.connectorSourcePortKey, false, state, out start))
                start = GetPortAnchorLocal(source, ResolveNodeType(graphType, source), state.connectorSourcePortKey, false, state);
            Vector2 mouse = Event.current.mousePosition;
            Handles.DrawAAPolyLine(2f, new Vector3(start.x, start.y), new Vector3(mouse.x, mouse.y));
            Handles.EndGUI();
        }

        private static bool TryToggleCollapseAt(PungentBoardDocument document, Vector2 mousePosition, Rect canvasRect, PungentBoardCanvasViewState state)
        {
            Vector2 localMouse = mousePosition - canvasRect.position;
            List<PungentBoardNode> nodes = document?.nodes ?? new List<PungentBoardNode>();
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                PungentBoardNode node = nodes[i];
                if (node == null || IsNodeHiddenByCollapsedGroup(document, node.id))
                    continue;

                Rect localRect = CanvasToLocal(node.Rect, state);
                if (!CollapseButtonRect(localRect).Contains(localMouse))
                    continue;

                node.collapsed = !node.collapsed;
                state.SelectNode(node.id);
                return true;
            }

            List<PungentBoardGroup> groups = document?.groups ?? new List<PungentBoardGroup>();
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                PungentBoardGroup group = groups[i];
                if (group == null)
                    continue;

                Rect localRect = VisibleGroupLocalRect(group, state);
                if (!CollapseButtonRect(localRect).Contains(localMouse))
                    continue;

                group.collapsed = !group.collapsed;
                state.SelectGroup(group.id);
                return true;
            }

            return false;
        }

        private static PungentBoardNode FindNodeResizeHandleAt(PungentBoardDocument document, Vector2 mousePosition, Rect canvasRect, PungentBoardCanvasViewState state)
        {
            Vector2 localMouse = mousePosition - canvasRect.position;
            PungentBoardNode selected = FindNode(document, state.selectedNodeId);
            if (selected == null)
                return null;

            return ResizeHandleRect(CanvasToLocal(selected.Rect, state)).Contains(localMouse) ? selected : null;
        }

        private static void UpdateConnectorHover(PungentBoardDocument document, PungentBoardCanvasViewState state, Vector2 mousePosition, Rect canvasRect)
        {
            if (state == null)
                return;

            state.connectorHoverNodeId = string.Empty;
            state.connectorHoverPortKey = string.Empty;
            state.connectorHoverValid = false;
            PungentBoardPortHit hit = FindNodePortAt(document, mousePosition, canvasRect, state);
            PungentBoardNode target = hit != null ? hit.node : FindNodeAt(document, ScreenToCanvas(mousePosition, canvasRect, state));
            string targetNodeId = hit != null ? hit.EndpointNodeId : target != null ? target.id : string.Empty;
            if (string.IsNullOrWhiteSpace(targetNodeId) || string.IsNullOrWhiteSpace(state.connectorSourceNodeId) || PungentAuthoringId.EqualsId(targetNodeId, state.connectorSourceNodeId))
                return;

            string targetPortKey = hit != null && hit.CanFinishEdge ? hit.EndpointPortKey : string.Empty;
            string message;
            bool valid = CanCreateTypedEdge(document, state, state.connectorSourceNodeId, targetNodeId, state.connectorSourcePortKey, targetPortKey, out message);
            state.connectorHoverNodeId = targetNodeId;
            state.connectorHoverPortKey = targetPortKey;
            state.connectorHoverValid = valid;
            string label = target != null ? string.IsNullOrWhiteSpace(target.title) ? target.id : target.title : hit != null && hit.group != null ? hit.group.title : targetNodeId;
            state.connectionWarning = valid ? "Release to create edge to " + label + "." : message;
        }

        private static PungentBoardPortHit FindNodePortAt(PungentBoardDocument document, Vector2 mousePosition, Rect canvasRect, PungentBoardCanvasViewState state)
        {
            Vector2 localMouse = mousePosition - canvasRect.position;
            PungentBoardPortHit groupHit = FindGroupPortAt(document, localMouse, state);
            if (groupHit != null)
                return groupHit;

            List<PungentBoardNode> nodes = document?.nodes ?? new List<PungentBoardNode>();
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document != null ? document.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                PungentBoardNode node = nodes[i];
                if (node == null || IsNodeHiddenByCollapsedGroup(document, node.id))
                    continue;

                Rect localRect = CanvasToLocal(node.Rect, state);
                PungentBoardNodeTypeDefinition nodeType = ResolveNodeType(graphType, node);
                PungentBoardPortHit hit = FindPortInList(node, InputPortsOrDefault(nodeType), true, localRect, localMouse);
                if (hit != null)
                    return hit;

                hit = FindPortInList(node, OutputPortsOrDefault(nodeType), false, localRect, localMouse);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        private static PungentBoardPortHit FindGroupPortAt(PungentBoardDocument document, Vector2 localMouse, PungentBoardCanvasViewState state)
        {
            List<PungentBoardGroup> groups = document?.groups ?? new List<PungentBoardGroup>();
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                PungentBoardGroup group = groups[i];
                if (!IsCollapsedGroupNode(group))
                    continue;

                Rect localRect = CanvasToLocal(VisibleGroupCanvasRect(group), state);
                PungentBoardPortHit hit = FindGroupPortInList(group, GroupInputPorts(group), true, localRect, localMouse);
                if (hit != null)
                    return hit;
                hit = FindGroupPortInList(group, GroupOutputPorts(group), false, localRect, localMouse);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        private static PungentBoardPortHit FindGroupPortInList(PungentBoardGroup group, List<PungentBoardPortDefinition> ports, bool inputSide, Rect localRect, Vector2 localMouse)
        {
            int count = ports != null ? ports.Count : 0;
            if (count == 0)
                return null;

            for (int i = 0; i < count; i++)
            {
                PungentBoardPortDefinition port = ports != null && i < ports.Count ? ports[i] : null;
                Rect portRect = PortRectAt(localRect, ports, i, inputSide);
                if (!portRect.Contains(localMouse))
                    continue;

                return new PungentBoardPortHit
                {
                    group = group,
                    port = port,
                    mapping = FindPortMapping(group, port != null ? port.key : string.Empty, inputSide),
                    inputSide = inputSide,
                    localRect = portRect
                };
            }

            return null;
        }

        private static void SelectPortHitOwner(PungentBoardCanvasViewState state, PungentBoardPortHit hit)
        {
            if (state == null || hit == null)
                return;
            if (hit.group != null)
                state.SelectGroup(hit.group.id);
            else if (hit.node != null)
                state.SelectNode(hit.node.id);
        }

        private static PungentBoardPortHit FindPortInList(PungentBoardNode node, List<PungentBoardPortDefinition> ports, bool inputSide, Rect localRect, Vector2 localMouse)
        {
            int count = Mathf.Max(1, ports != null ? ports.Count : 0);
            for (int i = 0; i < count; i++)
            {
                PungentBoardPortDefinition port = ports != null && i < ports.Count ? ports[i] : null;
                Rect portRect = PortRectAt(localRect, ports, i, inputSide);
                if (!portRect.Contains(localMouse))
                    continue;

                return new PungentBoardPortHit
                {
                    node = node,
                    port = port,
                    inputSide = inputSide,
                    localRect = portRect
                };
            }

            return null;
        }

        private static PungentBoardGroup FindGroupResizeHandleAt(PungentBoardDocument document, Vector2 mousePosition, Rect canvasRect, PungentBoardCanvasViewState state)
        {
            Vector2 localMouse = mousePosition - canvasRect.position;
            PungentBoardGroup selected = FindGroup(document, state.selectedGroupId);
            if (selected == null || selected.locked)
                return null;

            Rect localRect = VisibleGroupLocalRect(selected, state);
            return ResizeHandleRect(localRect).Contains(localMouse) ? selected : null;
        }

        private static Rect CollapseButtonRect(Rect localRect)
        {
            return new Rect(localRect.xMax - CollapseButtonSize - 8f, localRect.y + 6f, CollapseButtonSize, CollapseButtonSize);
        }

        private static Rect ResizeHandleRect(Rect localRect)
        {
            return new Rect(localRect.xMax - ResizeHandleSize - 3f, localRect.yMax - ResizeHandleSize - 3f, ResizeHandleSize, ResizeHandleSize);
        }

        private static Rect LeftPortRect(Rect localRect)
        {
            return new Rect(localRect.xMin - PortSize * 0.5f, localRect.center.y - PortSize * 0.5f, PortSize, PortSize);
        }

        private static Rect RightPortRect(Rect localRect)
        {
            return new Rect(localRect.xMax - PortSize * 0.5f, localRect.center.y - PortSize * 0.5f, PortSize, PortSize);
        }

        private static Vector2 SnapVector(Vector2 value, PungentBoardCanvasViewState state)
        {
            float size = Mathf.Clamp(state != null ? state.snapSize : GridMinor, 4f, 240f);
            return new Vector2(Mathf.Round(value.x / size) * size, Mathf.Round(value.y / size) * size);
        }

        private static PungentBoardNode FindNodeAt(PungentBoardDocument document, Vector2 canvasPoint)
        {
            List<PungentBoardNode> nodes = document?.nodes ?? new List<PungentBoardNode>();
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                PungentBoardNode node = nodes[i];
                if (node != null && !IsNodeHiddenByCollapsedGroup(document, node.id) && node.Rect.Contains(canvasPoint))
                    return node;
            }

            return null;
        }

        private static PungentBoardGroup FindGroupAt(PungentBoardDocument document, Vector2 canvasPoint)
        {
            List<PungentBoardGroup> groups = document?.groups ?? new List<PungentBoardGroup>();
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                PungentBoardGroup group = groups[i];
                if (group != null && VisibleGroupCanvasRect(group).Contains(canvasPoint))
                    return group;
            }

            return null;
        }

        private static PungentBoardEdge FindEdgeAt(PungentBoardDocument document, Vector2 mousePosition, Rect canvasRect, PungentBoardCanvasViewState state)
        {
            foreach (PungentBoardEdge edge in document?.edges ?? new List<PungentBoardEdge>())
            {
                if (edge == null)
                    continue;

                PungentBoardNode from = FindNode(document, edge.fromNodeId);
                PungentBoardNode to = FindNode(document, edge.toNodeId);
                if (from == null || to == null)
                    continue;
                if (IsInternalEdgeHiddenByCollapsedGroup(document, edge))
                    continue;

                Vector2 localA;
                Vector2 localB;
                GetEdgeLocalEndpoints(document, edge, state, out localA, out localB);
                Vector2 a = canvasRect.position + localA;
                Vector2 b = canvasRect.position + localB;
                if (DistanceToSegment(mousePosition, a, b) <= 8f)
                    return edge;
            }

            return null;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float length = segment.sqrMagnitude;
            if (length < 0.0001f)
                return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / length);
            return Vector2.Distance(point, a + segment * t);
        }

        private static bool TryGetContentBounds(PungentBoardDocument document, out Rect bounds)
        {
            bool hasBounds = false;
            bounds = new Rect(0f, 0f, 1f, 1f);

            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
                if (node != null)
                    Encapsulate(ref bounds, ref hasBounds, node.Rect);

            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
                if (group != null)
                    Encapsulate(ref bounds, ref hasBounds, VisibleGroupCanvasRect(group));

            return hasBounds;
        }

        private static bool TryGetSelectionBounds(PungentBoardDocument document, PungentBoardCanvasViewState state, out Rect bounds)
        {
            bool hasBounds = false;
            bounds = new Rect(0f, 0f, 1f, 1f);

            foreach (PungentBoardNode node in SelectedNodes(document, state))
                Encapsulate(ref bounds, ref hasBounds, node.Rect);

            foreach (PungentBoardGroup group in SelectedGroups(document, state))
                Encapsulate(ref bounds, ref hasBounds, VisibleGroupCanvasRect(group));

            PungentBoardEdge edge = FindEdge(document, state.selectedEdgeId);
            if (edge != null)
            {
                PungentBoardNode from = FindNode(document, edge.fromNodeId);
                PungentBoardNode to = FindNode(document, edge.toNodeId);
                if (from != null)
                    Encapsulate(ref bounds, ref hasBounds, from.Rect);
                if (to != null)
                    Encapsulate(ref bounds, ref hasBounds, to.Rect);
            }

            return hasBounds;
        }

        private static void Encapsulate(ref Rect bounds, ref bool hasBounds, Rect rect)
        {
            if (!hasBounds)
            {
                bounds = rect;
                hasBounds = true;
                return;
            }

            float xMin = Mathf.Min(bounds.xMin, rect.xMin);
            float yMin = Mathf.Min(bounds.yMin, rect.yMin);
            float xMax = Mathf.Max(bounds.xMax, rect.xMax);
            float yMax = Mathf.Max(bounds.yMax, rect.yMax);
            bounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static IEnumerable<PungentBoardNode> SelectedNodes(PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            if (document == null || state == null)
                yield break;

            HashSet<string> ids = new HashSet<string>(state.selectedNodeIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(state.selectedNodeId))
                ids.Add(state.selectedNodeId);
            foreach (PungentBoardNode node in document.nodes ?? new List<PungentBoardNode>())
                if (node != null && ids.Contains(node.id))
                    yield return node;
        }

        private static IEnumerable<PungentBoardGroup> SelectedGroups(PungentBoardDocument document, PungentBoardCanvasViewState state)
        {
            if (document == null || state == null)
                yield break;

            HashSet<string> ids = new HashSet<string>(state.selectedGroupIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(state.selectedGroupId))
                ids.Add(state.selectedGroupId);
            foreach (PungentBoardGroup group in document.groups ?? new List<PungentBoardGroup>())
                if (group != null && ids.Contains(group.id))
                    yield return group;
        }

        private static Rect Expanded(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private static Rect RectFromPoints(Vector2 a, Vector2 b)
        {
            Rect rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            if (rect.width < 1f)
                rect.width = 1f;
            if (rect.height < 1f)
                rect.height = 1f;
            return rect;
        }

        private static Rect GetMarqueeCanvasRect(PungentBoardCanvasViewState state)
        {
            return Rect.MinMaxRect(
                Mathf.Min(state.marqueeStartCanvas.x, state.marqueeCurrentCanvas.x),
                Mathf.Min(state.marqueeStartCanvas.y, state.marqueeCurrentCanvas.y),
                Mathf.Max(state.marqueeStartCanvas.x, state.marqueeCurrentCanvas.x),
                Mathf.Max(state.marqueeStartCanvas.y, state.marqueeCurrentCanvas.y));
        }

        private static void DrawMarquee(PungentBoardCanvasViewState state)
        {
            if (state == null || !state.marqueeSelecting)
                return;

            Rect canvasRect = GetMarqueeCanvasRect(state);
            Rect localRect = CanvasToLocal(canvasRect, state);
            EditorGUI.DrawRect(localRect, new Color(0.38f, 0.72f, 1f, 0.12f));
            Handles.BeginGUI();
            Handles.color = new Color(0.38f, 0.72f, 1f, 0.72f);
            Handles.DrawAAPolyLine(1.5f,
                new Vector3(localRect.xMin, localRect.yMin),
                new Vector3(localRect.xMax, localRect.yMin),
                new Vector3(localRect.xMax, localRect.yMax),
                new Vector3(localRect.xMin, localRect.yMax),
                new Vector3(localRect.xMin, localRect.yMin));
            Handles.EndGUI();
        }

        private static bool IsMissingLinkedItem(PungentBoardNode node)
        {
            if (node == null || node.linkedAuthoringRef == null || !node.linkedAuthoringRef.HasItemId)
                return false;

            PungentAuthoringPreview preview;
            return !PungentAuthoringProviderRegistry.TryGetPreview(node.linkedAuthoringRef, out preview) || (preview != null && preview.missing);
        }

        public static List<PungentBoardNodeTypeDefinition> GetPaletteNodeTypes(PungentBoardDocument document)
        {
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(document != null ? document.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            if (graphType != null)
            {
                graphType.NormalizeInPlace();
                if (graphType.nodeTypes != null && graphType.nodeTypes.Count > 0)
                    return graphType.nodeTypes.Where(type => type != null).ToList();
            }

            return Enum.GetValues(typeof(PungentBoardNodeKind))
                .Cast<PungentBoardNodeKind>()
                .Select(kind => new PungentBoardNodeTypeDefinition
                {
                    typeKey = kind.ToString(),
                    displayName = NodeKindLabel(kind),
                    defaultTitle = PungentBoardNode.GetDefaultTitle(kind),
                    nodeKind = kind,
                    styleKey = PungentBoardNode.GetDefaultStyleKey(kind)
                })
                .ToList();
        }

        public static PungentBoardNodeTypeDefinition ResolveNodeType(PungentBoardGraphTypeDefinition graphType, PungentBoardNode node)
        {
            if (node == null)
                return null;

            if (graphType != null)
            {
                if (!string.IsNullOrWhiteSpace(node.nodeTypeKey))
                {
                    PungentBoardNodeTypeDefinition byKey = graphType.FindNodeType(node.nodeTypeKey);
                    if (byKey != null)
                        return byKey;
                }

                PungentBoardNodeTypeDefinition byLegacyKey = graphType.FindNodeType(node.nodeKind.ToString());
                if (byLegacyKey != null)
                    return byLegacyKey;

                PungentBoardNodeTypeDefinition byKind = (graphType.nodeTypes ?? new List<PungentBoardNodeTypeDefinition>())
                    .FirstOrDefault(type => type != null && type.nodeKind == node.nodeKind);
                if (byKind != null)
                    return byKind;

                PungentBoardNodeTypeDefinition defaultType = graphType.GetDefaultNodeType();
                if (defaultType != null)
                    return defaultType;
            }

            PungentBoardNodeTypeDefinition fallback = new PungentBoardNodeTypeDefinition
            {
                typeKey = string.IsNullOrWhiteSpace(node.nodeTypeKey) ? node.nodeKind.ToString() : node.nodeTypeKey,
                displayName = string.IsNullOrWhiteSpace(node.nodeTypeKey) ? NodeKindLabel(node.nodeKind) : node.nodeTypeKey,
                defaultTitle = PungentBoardNode.GetDefaultTitle(node.nodeKind),
                nodeKind = node.nodeKind,
                styleKey = PungentBoardNode.GetDefaultStyleKey(node.nodeKind)
            };
            fallback.NormalizeInPlace();
            return fallback;
        }

        private static string NodeTypeLabel(PungentBoardNode node, PungentBoardNodeTypeDefinition nodeType)
        {
            if (nodeType != null && !string.IsNullOrWhiteSpace(nodeType.displayName))
                return nodeType.displayName;
            return node == null ? "Node" : NodeKindLabel(node.nodeKind);
        }

        private static string NodeKindLabel(PungentBoardNodeKind kind)
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

        private static int BodyPreviewCharacterLimit(Rect bodyRect, float zoom)
        {
            int approximateLines = Mathf.Clamp(Mathf.FloorToInt(bodyRect.height / 13f), 1, 8);
            int approximateCharsPerLine = Mathf.Clamp(Mathf.FloorToInt(bodyRect.width / 6.2f), 8, 80);
            int areaLimit = approximateLines * approximateCharsPerLine;
            int zoomLimit = zoom < 0.66f ? 70 : zoom < 0.95f ? 125 : 220;
            return Mathf.Clamp(Mathf.Min(areaLimit, zoomLimit), 32, 260);
        }

        private static string PreviewMultilineText(string value, Rect bodyRect, float zoom)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace("\r\n", "\n").Replace('\r', '\n');
            if (string.IsNullOrWhiteSpace(clean))
                return string.Empty;

            int maxLines = Mathf.Clamp(Mathf.FloorToInt(bodyRect.height / 13f), 1, zoom < 0.95f ? 3 : 7);
            int maxCharsPerLine = Mathf.Clamp(Mathf.FloorToInt(bodyRect.width / 6.1f), 8, 92);
            int maxCharacters = BodyPreviewCharacterLimit(bodyRect, zoom);
            string[] sourceLines = clean.Split('\n');
            List<string> lines = new List<string>();
            int usedCharacters = 0;
            bool truncated = false;

            for (int i = 0; i < sourceLines.Length && lines.Count < maxLines; i++)
            {
                string line = sourceLines[i] ?? string.Empty;
                if (line.Length > maxCharsPerLine)
                {
                    line = line.Substring(0, Mathf.Max(1, maxCharsPerLine - 3)) + "...";
                    truncated = true;
                }

                if (usedCharacters + line.Length > maxCharacters)
                {
                    int remaining = Mathf.Max(4, maxCharacters - usedCharacters);
                    line = line.Length <= remaining ? line : line.Substring(0, Mathf.Max(1, remaining - 3)) + "...";
                    truncated = true;
                    lines.Add(line);
                    break;
                }

                usedCharacters += line.Length;
                lines.Add(line);
            }

            if (sourceLines.Length > lines.Count)
                truncated = true;
            if (truncated && lines.Count > 0 && !lines[lines.Count - 1].EndsWith("...", StringComparison.Ordinal))
                lines[lines.Count - 1] = lines[lines.Count - 1] + "...";

            return string.Join("\n", lines);
        }

        private static void DrawClippedLabel(Rect rect, string text, GUIStyle style)
        {
            if (rect.width < 2f || rect.height < 2f)
                return;

            GUI.BeginGroup(rect);
            GUI.Label(new Rect(0f, 0f, rect.width, rect.height), text ?? string.Empty, style);
            GUI.EndGroup();
        }

        private static string PreviewText(string value)
        {
            return PreviewText(value, 180);
        }

        private static string PreviewText(string value, int maxCharacters)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace("\r", " ").Replace("\n", " ");
            maxCharacters = Mathf.Clamp(maxCharacters, 8, 400);
            return clean.Length <= maxCharacters ? clean : clean.Substring(0, Mathf.Max(1, maxCharacters - 3)) + "...";
        }

        private static Color StyleColor(string key, float alpha)
        {
            string clean = (key ?? string.Empty).Trim().ToLowerInvariant();
            switch (clean)
            {
                case "document": return new Color(0.28f, 0.47f, 0.78f, alpha);
                case "utility": return new Color(0.22f, 0.62f, 0.55f, alpha);
                case "token": return new Color(0.58f, 0.45f, 0.78f, alpha);
                case "documentation": return new Color(0.64f, 0.53f, 0.28f, alpha);
                case "audit": return new Color(0.78f, 0.38f, 0.31f, alpha);
                case "task": return new Color(0.34f, 0.58f, 0.32f, alpha);
                case "freeform": return new Color(0.45f, 0.45f, 0.48f, alpha);
                case "group": return new Color(0.30f, 0.45f, 0.56f, alpha);
                case "root": return new Color(0.72f, 0.40f, 0.28f, alpha);
                case "selector": return new Color(0.35f, 0.55f, 0.82f, alpha);
                case "sequence": return new Color(0.30f, 0.62f, 0.48f, alpha);
                case "condition": return new Color(0.72f, 0.58f, 0.28f, alpha);
                case "action": return new Color(0.45f, 0.58f, 0.34f, alpha);
                case "decorator": return new Color(0.56f, 0.42f, 0.72f, alpha);
                case "state": return new Color(0.25f, 0.52f, 0.66f, alpha);
                case "entry": return new Color(0.34f, 0.66f, 0.42f, alpha);
                case "exit": return new Color(0.70f, 0.34f, 0.34f, alpha);
                case "event": return new Color(0.32f, 0.48f, 0.78f, alpha);
                case "camera": return new Color(0.48f, 0.42f, 0.72f, alpha);
                case "trigger": return new Color(0.62f, 0.42f, 0.28f, alpha);
                case "wait": return new Color(0.38f, 0.52f, 0.54f, alpha);
                case "branch": return new Color(0.62f, 0.50f, 0.30f, alpha);
                case "skill": return new Color(0.28f, 0.58f, 0.62f, alpha);
                case "requirement": return new Color(0.56f, 0.56f, 0.40f, alpha);
                case "unlock": return new Color(0.38f, 0.64f, 0.38f, alpha);
                case "modifier": return new Color(0.54f, 0.44f, 0.68f, alpha);
                case "gate": return new Color(0.64f, 0.44f, 0.32f, alpha);
                case "system": return new Color(0.34f, 0.48f, 0.60f, alpha);
                case "provider": return new Color(0.30f, 0.60f, 0.58f, alpha);
                case "package": return new Color(0.48f, 0.48f, 0.64f, alpha);
                case "external": return new Color(0.48f, 0.44f, 0.36f, alpha);
                case "dialogue": return new Color(0.35f, 0.50f, 0.76f, alpha);
                case "choice": return new Color(0.58f, 0.46f, 0.72f, alpha);
                case "outcome": return new Color(0.42f, 0.58f, 0.44f, alpha);
                default: return new Color(0.32f, 0.48f, 0.68f, alpha);
            }
        }

        private static void EnsureStyles()
        {
            if (_nodeTitleStyle != null)
                return;

            _nodeTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.08f, 0.09f, 0.1f) }
            };
            _nodeBodyStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.94f, 0.96f) : new Color(0.08f, 0.09f, 0.1f) }
            };
            _nodeBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.76f, 0.86f, 0.92f) : new Color(0.18f, 0.22f, 0.25f) }
            };
            _nodePortLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.88f, 0.94f, 1f) : new Color(0.09f, 0.13f, 0.16f) }
            };
            _edgeLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.82f, 0.9f, 1f) : new Color(0.12f, 0.18f, 0.24f) }
            };
            _groupTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.88f, 0.94f, 1f) : new Color(0.12f, 0.16f, 0.2f) }
            };
        }
    }
#endif
}
