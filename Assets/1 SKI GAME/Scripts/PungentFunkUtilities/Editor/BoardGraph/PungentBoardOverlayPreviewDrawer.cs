using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    internal sealed class PungentBoardOverlayPreviewDrawer : IPungentAuthoringOverlayPreviewDrawer
    {
        private const float Padding = 80f;
        private static GUIStyle _nodeTitleStyle;
        private static GUIStyle _nodeBodyStyle;
        private static GUIStyle _groupTitleStyle;
        private static readonly BoardPreviewCache PreviewCache = new BoardPreviewCache();

        private sealed class BoardPreviewCache
        {
            public string key = string.Empty;
            public Rect worldBounds = new Rect(-120f, -80f, 420f, 280f);
            public readonly Dictionary<string, PungentBoardNode> nodeById = new Dictionary<string, PungentBoardNode>(System.StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> validNodeIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            public string selectionCleanKey = string.Empty;
            public int selectionCount;
            public int lastVisibleNodes;
        }

        static PungentBoardOverlayPreviewDrawer()
        {
            PungentAuthoringOverlayPreviewRegistry.Register(new PungentBoardOverlayPreviewDrawer());
        }

        public bool CanDraw(PungentAuthoringReference reference)
        {
            return reference != null &&
                   (reference.itemKind == PungentAuthoringItemKind.Board ||
                    string.Equals(reference.providerId, PungentBoardProvider.Id, System.StringComparison.OrdinalIgnoreCase));
        }

        public void Draw(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state)
        {
            long drawSample = PungentAuthoringOverlayPerformance.BeginSample();
            PungentBoardDocument board = PungentBoardEditorStorage.FindBoard(reference.itemId);
            if (board == null)
            {
                EditorGUILayout.LabelField("Node graph missing", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The linked board or node graph could not be found.", UtilityWindowTheme.MutedMiniLabelStyle);
                PungentAuthoringOverlayPerformance.EndSample("Board Overlay Draw", drawSample);
                return;
            }

            EnsureStyles();
            BoardPreviewCache cache = GetBoardCache(board);
            Rect worldBounds = cache.worldBounds;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                    FitToBounds(worldBounds, state);
                if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    FrameSelection(board, worldBounds, state);
                GUILayout.Space(4f);
                state.nodeGraphZoom = GUILayout.HorizontalSlider(Mathf.Clamp(state.nodeGraphZoom, 0.2f, 2.5f), 0.2f, 2.5f, GUILayout.Width(116f));
                EditorGUILayout.LabelField(Mathf.RoundToInt(state.nodeGraphZoom * 100f) + "%", UtilityWindowTheme.PathLabelStyle, GUILayout.Width(44f));
                GUILayout.FlexibleSpace();
                int selectedCount = CleanSelection(board, state, cache);
                EditorGUILayout.LabelField(selectedCount > 0 ? selectedCount + " selected" : (board.nodes == null ? 0 : board.nodes.Count) + " nodes", UtilityWindowTheme.PathLabelStyle, GUILayout.Width(92f));
            }

            Rect canvasRect = GUILayoutUtility.GetRect(160f, 10000f, 180f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            state.nodeGraphViewportSize = canvasRect.size;
            DrawCanvas(canvasRect, board, worldBounds, cache, state);
            PungentAuthoringOverlayPerformance.EndSample("Board Overlay Draw", drawSample, cache.lastVisibleNodes);
        }

        private static void DrawCanvas(Rect rect, PungentBoardDocument board, Rect worldBounds, BoardPreviewCache cache, PungentStickyNoteOverlayState state)
        {
            if (rect.width <= 8f || rect.height <= 8f)
                return;

            float zoom = Mathf.Clamp(state.nodeGraphZoom <= 0f ? 1f : state.nodeGraphZoom, 0.2f, 2.5f);
            state.nodeGraphZoom = zoom;
            HandleViewportInput(rect, worldBounds, state);
            zoom = Mathf.Clamp(state.nodeGraphZoom <= 0f ? 1f : state.nodeGraphZoom, 0.2f, 2.5f);
            Vector2 contentSize = ContentSize(rect.size, worldBounds, zoom);
            state.nodeGraphScroll = ClampScroll(state.nodeGraphScroll, contentSize, rect.size);
            Rect viewRect = new Rect(0f, 0f, contentSize.x, contentSize.y);

            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.085f, 0.09f, 0.098f, 1f) : new Color(0.86f, 0.87f, 0.89f, 1f));
            state.nodeGraphScroll = GUI.BeginScrollView(rect, state.nodeGraphScroll, viewRect, true, true);
            try
            {
                HandleContentInput(board, worldBounds, zoom, state);
                Rect visibleRect = new Rect(state.nodeGraphScroll.x, state.nodeGraphScroll.y, rect.width, rect.height);
                DrawGrid(viewRect, zoom, visibleRect);
                DrawGroups(board, worldBounds, zoom, visibleRect);
                DrawEdges(board, worldBounds, zoom, visibleRect, cache);
                cache.lastVisibleNodes = DrawNodes(board, worldBounds, zoom, visibleRect, state);
                DrawMarquee(state);
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private static void DrawGrid(Rect viewRect, float zoom, Rect visibleRect)
        {
            Color line = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.045f) : new Color(0f, 0f, 0f, 0.055f);
            float spacing = Mathf.Max(24f, 48f * zoom);
            float startX = Mathf.Floor(Mathf.Max(0f, visibleRect.xMin) / spacing) * spacing;
            float endX = Mathf.Min(viewRect.width, visibleRect.xMax + spacing);
            float startY = Mathf.Floor(Mathf.Max(0f, visibleRect.yMin) / spacing) * spacing;
            float endY = Mathf.Min(viewRect.height, visibleRect.yMax + spacing);
            Handles.BeginGUI();
            Handles.color = line;
            for (float x = startX; x < endX; x += spacing)
                Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, viewRect.height));
            for (float y = startY; y < endY; y += spacing)
                Handles.DrawLine(new Vector3(0f, y), new Vector3(viewRect.width, y));
            Handles.EndGUI();
        }

        private static void DrawGroups(PungentBoardDocument board, Rect bounds, float zoom, Rect visibleRect)
        {
            if (board.groups == null)
                return;

            for (int i = 0; i < board.groups.Count; i++)
            {
                PungentBoardGroup group = board.groups[i];
                if (group == null)
                    continue;

                Rect rect = WorldToView(group.rect, bounds, zoom);
                if (!rect.Overlaps(visibleRect, true))
                    continue;
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.34f, 0.35f, 0.38f, 0.18f) : new Color(0.55f, 0.57f, 0.62f, 0.16f));
                DrawBorder(rect, EditorGUIUtility.isProSkin ? new Color(0.78f, 0.80f, 0.85f, 0.24f) : new Color(0.12f, 0.14f, 0.18f, 0.18f));
                GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, Mathf.Max(30f, rect.width - 16f), 18f), string.IsNullOrWhiteSpace(group.title) ? "Group" : group.title, _groupTitleStyle);
            }
        }

        private static void DrawEdges(PungentBoardDocument board, Rect bounds, float zoom, Rect visibleRect, BoardPreviewCache cache)
        {
            if (board.edges == null || cache == null)
                return;

            Handles.BeginGUI();
            Handles.color = EditorGUIUtility.isProSkin ? new Color(0.58f, 0.64f, 0.72f, 0.66f) : new Color(0.25f, 0.30f, 0.38f, 0.58f);
            for (int i = 0; i < board.edges.Count; i++)
            {
                PungentBoardEdge edge = board.edges[i];
                if (edge == null || !cache.nodeById.TryGetValue(edge.fromNodeId, out PungentBoardNode from) || !cache.nodeById.TryGetValue(edge.toNodeId, out PungentBoardNode to))
                    continue;

                Vector2 a = WorldToView(from.Rect.center, bounds, zoom);
                Vector2 b = WorldToView(to.Rect.center, bounds, zoom);
                Rect edgeBounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 36f, Mathf.Min(a.y, b.y) - 36f, Mathf.Max(a.x, b.x) + 36f, Mathf.Max(a.y, b.y) + 36f);
                if (!edgeBounds.Overlaps(visibleRect, true))
                    continue;
                Vector2 mid = (a + b) * 0.5f;
                Vector2 normal = (b - a).normalized;
                Vector2 control = mid + new Vector2(-normal.y, normal.x) * 32f;
                Handles.DrawBezier(a, b, control, control, Handles.color, null, Mathf.Max(1.6f, zoom * 2f));
            }

            Handles.EndGUI();
        }

        private static int DrawNodes(PungentBoardDocument board, Rect bounds, float zoom, Rect visibleRect, PungentStickyNoteOverlayState state)
        {
            if (board.nodes == null)
                return 0;

            int drawn = 0;
            for (int i = 0; i < board.nodes.Count; i++)
            {
                PungentBoardNode node = board.nodes[i];
                if (node == null)
                    continue;

                Rect rect = WorldToView(node.Rect, bounds, zoom);
                if (!rect.Overlaps(visibleRect, true))
                    continue;
                drawn++;
                bool selected = IsSelected(state, node.id);

                Color fill = NodeFill(node);
                EditorGUI.DrawRect(rect, fill);
                DrawBorder(rect, selected ? UtilityWindowTheme.Cyan : (EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.52f) : new Color(0f, 0f, 0f, 0.18f)));
                Rect titleRect = new Rect(rect.x + 8f, rect.y + 6f, Mathf.Max(20f, rect.width - 16f), 20f);
                GUI.Label(titleRect, string.IsNullOrWhiteSpace(node.title) ? "Node" : node.title, _nodeTitleStyle);
                if (!node.collapsed)
                {
                    string body = !string.IsNullOrWhiteSpace(node.summary) ? node.summary : node.body;
                    if (!string.IsNullOrWhiteSpace(body))
                        GUI.Label(new Rect(rect.x + 8f, rect.y + 28f, Mathf.Max(20f, rect.width - 16f), Mathf.Max(18f, rect.height - 36f)), body, _nodeBodyStyle);
                }
            }
            return drawn;
        }

        private static Rect CalculateWorldBounds(PungentBoardDocument board)
        {
            bool hasAny = false;
            Rect bounds = new Rect(-120f, -80f, 420f, 280f);
            if (board.nodes != null)
            {
                for (int i = 0; i < board.nodes.Count; i++)
                {
                    PungentBoardNode node = board.nodes[i];
                    if (node == null)
                        continue;
                    bounds = hasAny ? Union(bounds, node.Rect) : node.Rect;
                    hasAny = true;
                }
            }

            if (board.groups != null)
            {
                for (int i = 0; i < board.groups.Count; i++)
                {
                    PungentBoardGroup group = board.groups[i];
                    if (group == null)
                        continue;
                    bounds = hasAny ? Union(bounds, group.rect) : group.rect;
                    hasAny = true;
                }
            }

            if (!hasAny)
                return bounds;

            bounds.xMin -= 40f;
            bounds.yMin -= 40f;
            bounds.xMax += 40f;
            bounds.yMax += 40f;
            return bounds;
        }

        private static Rect Union(Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.xMin, b.xMin);
            float yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax);
            float yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Rect WorldToView(Rect rect, Rect bounds, float zoom)
        {
            Vector2 min = WorldToView(rect.min, bounds, zoom);
            return new Rect(min.x, min.y, rect.width * zoom, rect.height * zoom);
        }

        private static Vector2 WorldToView(Vector2 point, Rect bounds, float zoom)
        {
            return new Vector2((point.x - bounds.xMin) * zoom + Padding, (point.y - bounds.yMin) * zoom + Padding);
        }

        private static Vector2 ViewToWorld(Vector2 point, Rect bounds, float zoom)
        {
            float safeZoom = Mathf.Max(0.001f, zoom);
            return new Vector2((point.x - Padding) / safeZoom + bounds.xMin, (point.y - Padding) / safeZoom + bounds.yMin);
        }

        private static void FitToBounds(Rect bounds, PungentStickyNoteOverlayState state)
        {
            Vector2 viewport = state.nodeGraphViewportSize;
            if (viewport.x <= 1f || viewport.y <= 1f)
                viewport = new Vector2(560f, 360f);

            float x = (viewport.x - 60f) / Mathf.Max(160f, bounds.width);
            float y = (viewport.y - 60f) / Mathf.Max(120f, bounds.height);
            state.nodeGraphZoom = Mathf.Clamp(Mathf.Min(x, y), 0.2f, 2.5f);
            state.nodeGraphScroll = Vector2.zero;
        }

        private static void FrameSelection(PungentBoardDocument board, Rect bounds, PungentStickyNoteOverlayState state)
        {
            Rect targetBounds;
            if (!TryGetSelectedBounds(board, state, out targetBounds))
            {
                FitToBounds(bounds, state);
                return;
            }

            Vector2 viewport = state.nodeGraphViewportSize;
            if (viewport.x <= 1f || viewport.y <= 1f)
                viewport = new Vector2(560f, 360f);
            float targetZoom = Mathf.Clamp(Mathf.Min(
                (viewport.x - 96f) / Mathf.Max(80f, targetBounds.width),
                (viewport.y - 96f) / Mathf.Max(64f, targetBounds.height)), 0.2f, 2.5f);
            Vector2 targetCenter = WorldToView(targetBounds.center, bounds, targetZoom);
            state.nodeGraphZoom = targetZoom;
            state.nodeGraphScroll = ClampScroll(targetCenter - viewport * 0.5f, ContentSize(viewport, bounds, targetZoom), viewport);
        }

        private static void HandleViewportInput(Rect rect, Rect worldBounds, PungentStickyNoteOverlayState state)
        {
            Event evt = Event.current;
            if (evt == null || evt.type == EventType.Layout)
                return;

            bool inside = rect.Contains(evt.mousePosition);
            if (evt.type == EventType.ScrollWheel && inside)
            {
                float oldZoom = Mathf.Clamp(state.nodeGraphZoom <= 0f ? 1f : state.nodeGraphZoom, 0.2f, 2.5f);
                Vector2 viewportMouse = evt.mousePosition - rect.position;
                Vector2 world = ViewToWorld(state.nodeGraphScroll + viewportMouse, worldBounds, oldZoom);
                float nextZoom = Mathf.Clamp(oldZoom * (1f - evt.delta.y * 0.06f), 0.2f, 2.5f);
                Vector2 after = WorldToView(world, worldBounds, nextZoom);
                state.nodeGraphZoom = nextZoom;
                state.nodeGraphScroll = ClampScroll(after - viewportMouse, ContentSize(rect.size, worldBounds, nextZoom), rect.size);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 2 && inside)
            {
                state.nodeGraphPanning = true;
                state.nodeGraphPanStartMouse = evt.mousePosition;
                state.nodeGraphPanStartScroll = state.nodeGraphScroll;
                evt.Use();
                return;
            }

            if (state.nodeGraphPanning && evt.type == EventType.MouseDrag && evt.button == 2)
            {
                state.nodeGraphScroll = ClampScroll(
                    state.nodeGraphPanStartScroll - (evt.mousePosition - state.nodeGraphPanStartMouse),
                    ContentSize(rect.size, worldBounds, Mathf.Clamp(state.nodeGraphZoom, 0.2f, 2.5f)),
                    rect.size);
                evt.Use();
                return;
            }

            if (state.nodeGraphPanning && (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp))
            {
                state.nodeGraphPanning = false;
                evt.Use();
            }
        }

        private static void HandleContentInput(PungentBoardDocument board, Rect bounds, float zoom, PungentStickyNoteOverlayState state)
        {
            Event evt = Event.current;
            if (evt == null || board.nodes == null || evt.type == EventType.Layout)
                return;

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                PungentBoardNode hit = FindNodeAt(board, bounds, zoom, evt.mousePosition);
                bool additive = evt.shift || evt.control || evt.command;
                if (hit != null)
                {
                    SelectNode(state, hit.id, additive);
                    evt.Use();
                    return;
                }

                state.nodeGraphMarqueeActive = true;
                state.nodeGraphMarqueeStart = evt.mousePosition;
                state.nodeGraphMarqueeEnd = evt.mousePosition;
                if (!additive)
                    ClearSelection(state);
                evt.Use();
                return;
            }

            if (state.nodeGraphMarqueeActive && evt.type == EventType.MouseDrag && evt.button == 0)
            {
                state.nodeGraphMarqueeEnd = evt.mousePosition;
                evt.Use();
                return;
            }

            if (state.nodeGraphMarqueeActive && evt.type == EventType.MouseUp && evt.button == 0)
            {
                Rect marquee = NormalizedRect(state.nodeGraphMarqueeStart, state.nodeGraphMarqueeEnd);
                bool additive = evt.shift || evt.control || evt.command;
                if (!additive)
                    ClearSelection(state);
                for (int i = 0; i < board.nodes.Count; i++)
                {
                    PungentBoardNode node = board.nodes[i];
                    if (node != null && marquee.Overlaps(WorldToView(node.Rect, bounds, zoom), true))
                        AddSelection(state, node.id);
                }

                state.nodeGraphMarqueeActive = false;
                evt.Use();
            }
        }

        private static PungentBoardNode FindNodeAt(PungentBoardDocument board, Rect bounds, float zoom, Vector2 viewPosition)
        {
            if (board.nodes == null)
                return null;

            for (int i = board.nodes.Count - 1; i >= 0; i--)
            {
                PungentBoardNode node = board.nodes[i];
                if (node != null && WorldToView(node.Rect, bounds, zoom).Contains(viewPosition))
                    return node;
            }

            return null;
        }

        private static void DrawMarquee(PungentStickyNoteOverlayState state)
        {
            if (!state.nodeGraphMarqueeActive)
                return;

            Rect rect = NormalizedRect(state.nodeGraphMarqueeStart, state.nodeGraphMarqueeEnd);
            EditorGUI.DrawRect(rect, new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.12f));
            DrawBorder(rect, UtilityWindowTheme.Cyan);
        }

        private static bool TryGetSelectedBounds(PungentBoardDocument board, PungentStickyNoteOverlayState state, out Rect selectedBounds)
        {
            selectedBounds = Rect.zero;
            if (board.nodes == null)
                return false;

            bool hasAny = false;
            for (int i = 0; i < board.nodes.Count; i++)
            {
                PungentBoardNode node = board.nodes[i];
                if (node == null || !IsSelected(state, node.id))
                    continue;

                selectedBounds = hasAny ? Union(selectedBounds, node.Rect) : node.Rect;
                hasAny = true;
            }

            if (!hasAny)
                return false;

            selectedBounds.xMin -= 24f;
            selectedBounds.yMin -= 24f;
            selectedBounds.xMax += 24f;
            selectedBounds.yMax += 24f;
            return true;
        }

        private static int CleanSelection(PungentBoardDocument board, PungentStickyNoteOverlayState state, BoardPreviewCache cache)
        {
            if (state.nodeGraphSelectedNodeIds == null)
                state.nodeGraphSelectedNodeIds = new List<string>();
            if (board.nodes == null || cache == null)
            {
                ClearSelection(state);
                return 0;
            }

            string cleanKey = cache.key + "|" + SelectionKey(state);
            if (string.Equals(cache.selectionCleanKey, cleanKey, System.StringComparison.Ordinal))
                return cache.selectionCount;

            state.nodeGraphSelectedNodeIds.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !cache.validNodeIds.Contains(id));
            if (!string.IsNullOrWhiteSpace(state.nodeGraphSelectedNodeId) && cache.validNodeIds.Contains(state.nodeGraphSelectedNodeId))
                AddSelection(state, state.nodeGraphSelectedNodeId);
            state.nodeGraphSelectedNodeId = state.nodeGraphSelectedNodeIds.Count == 0 ? string.Empty : state.nodeGraphSelectedNodeIds[state.nodeGraphSelectedNodeIds.Count - 1];
            cache.selectionCleanKey = cache.key + "|" + SelectionKey(state);
            cache.selectionCount = state.nodeGraphSelectedNodeIds.Count;
            return cache.selectionCount;
        }

        private static BoardPreviewCache GetBoardCache(PungentBoardDocument board)
        {
            string key = BuildBoardCacheKey(board);
            if (string.Equals(PreviewCache.key, key, System.StringComparison.Ordinal))
                return PreviewCache;

            PreviewCache.key = key;
            PreviewCache.selectionCleanKey = string.Empty;
            PreviewCache.selectionCount = 0;
            PreviewCache.worldBounds = CalculateWorldBounds(board);
            PreviewCache.nodeById.Clear();
            PreviewCache.validNodeIds.Clear();
            if (board != null && board.nodes != null)
            {
                for (int i = 0; i < board.nodes.Count; i++)
                {
                    PungentBoardNode node = board.nodes[i];
                    if (node == null || string.IsNullOrWhiteSpace(node.id))
                        continue;
                    if (!PreviewCache.nodeById.ContainsKey(node.id))
                        PreviewCache.nodeById.Add(node.id, node);
                    PreviewCache.validNodeIds.Add(node.id);
                }
            }
            return PreviewCache;
        }

        private static string BuildBoardCacheKey(PungentBoardDocument board)
        {
            if (board == null)
                return "missing";

            return string.Join("|",
                board.id ?? string.Empty,
                board.updatedUtc ?? string.Empty,
                board.nodes == null ? "n0" : "n" + board.nodes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                board.edges == null ? "e0" : "e" + board.edges.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                board.groups == null ? "g0" : "g" + board.groups.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static bool IsSelected(PungentStickyNoteOverlayState state, string nodeId)
        {
            if (state == null || string.IsNullOrWhiteSpace(nodeId))
                return false;
            if (string.Equals(state.nodeGraphSelectedNodeId, nodeId, System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (state.nodeGraphSelectedNodeIds == null)
                return false;
            for (int i = 0; i < state.nodeGraphSelectedNodeIds.Count; i++)
                if (string.Equals(state.nodeGraphSelectedNodeIds[i], nodeId, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static void SelectNode(PungentStickyNoteOverlayState state, string nodeId, bool additive)
        {
            if (!additive)
                ClearSelection(state);
            if (additive && IsSelected(state, nodeId))
            {
                state.nodeGraphSelectedNodeIds.RemoveAll(id => string.Equals(id, nodeId, System.StringComparison.OrdinalIgnoreCase));
                state.nodeGraphSelectedNodeId = state.nodeGraphSelectedNodeIds.Count == 0 ? string.Empty : state.nodeGraphSelectedNodeIds[state.nodeGraphSelectedNodeIds.Count - 1];
                return;
            }

            AddSelection(state, nodeId);
        }

        private static void AddSelection(PungentStickyNoteOverlayState state, string nodeId)
        {
            if (state == null || string.IsNullOrWhiteSpace(nodeId))
                return;
            if (state.nodeGraphSelectedNodeIds == null)
                state.nodeGraphSelectedNodeIds = new List<string>();
            bool exists = false;
            for (int i = 0; i < state.nodeGraphSelectedNodeIds.Count; i++)
            {
                if (string.Equals(state.nodeGraphSelectedNodeIds[i], nodeId, System.StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
                state.nodeGraphSelectedNodeIds.Add(nodeId);
            state.nodeGraphSelectedNodeId = nodeId;
        }

        private static string SelectionKey(PungentStickyNoteOverlayState state)
        {
            if (state == null || state.nodeGraphSelectedNodeIds == null || state.nodeGraphSelectedNodeIds.Count == 0)
                return string.Empty;
            return string.Join(",", state.nodeGraphSelectedNodeIds.ToArray());
        }

        private static void ClearSelection(PungentStickyNoteOverlayState state)
        {
            if (state.nodeGraphSelectedNodeIds != null)
                state.nodeGraphSelectedNodeIds.Clear();
            state.nodeGraphSelectedNodeId = string.Empty;
        }

        private static Rect NormalizedRect(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Vector2 ContentSize(Vector2 viewport, Rect worldBounds, float zoom)
        {
            return new Vector2(
                Mathf.Max(viewport.x - 18f, worldBounds.width * zoom + Padding * 2f),
                Mathf.Max(viewport.y - 18f, worldBounds.height * zoom + Padding * 2f));
        }

        private static Vector2 ClampScroll(Vector2 scroll, Vector2 contentSize, Vector2 viewport)
        {
            return new Vector2(
                Mathf.Clamp(scroll.x, 0f, Mathf.Max(0f, contentSize.x - viewport.x)),
                Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, contentSize.y - viewport.y)));
        }

        private static Color NodeFill(PungentBoardNode node)
        {
            string key = node == null ? string.Empty : (node.colorStyleKey ?? string.Empty).ToLowerInvariant();
            Color baseColor = UtilityWindowTheme.Neutral;
            if (key.Contains("task"))
                baseColor = UtilityWindowTheme.Amber;
            else if (key.Contains("document"))
                baseColor = UtilityWindowTheme.Blue;
            else if (key.Contains("token"))
                baseColor = UtilityWindowTheme.Purple;
            else if (key.Contains("audit"))
                baseColor = UtilityWindowTheme.Red;
            else if (key.Contains("utility"))
                baseColor = UtilityWindowTheme.Teal;

            return EditorGUIUtility.isProSkin
                ? new Color(baseColor.r * 0.42f, baseColor.g * 0.42f, baseColor.b * 0.42f, 0.95f)
                : new Color(Mathf.Lerp(1f, baseColor.r, 0.20f), Mathf.Lerp(1f, baseColor.g, 0.20f), Mathf.Lerp(1f, baseColor.b, 0.20f), 0.98f);
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static void EnsureStyles()
        {
            if (_nodeTitleStyle != null)
                return;

            _nodeTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft,
                fontSize = 11
            };
            _nodeBodyStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                clipping = TextClipping.Clip,
                wordWrap = true
            };
            _groupTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft
            };
        }
    }
#endif
}
