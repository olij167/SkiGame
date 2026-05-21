using System.Collections.Generic;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal sealed class PungentSpatialPlanCanvasState
    {
        public Vector2 pan = new Vector2(40f, 40f);
        public float zoom = 1f;
        public PungentSpatialItemKind selectedKind = PungentSpatialItemKind.Path;
        public int selectedIndex = -1;
        public int selectedVertex = -1;
        public bool draggingVertex;
        public Vector2 dragOffset;
        public string status = "Plan canvas ready.";
    }

    internal static class PungentSpatialPlanCanvasGUI
    {
        private const float PointHitSize = 10f;
        private const float PointDrawSize = 5f;

        public static void Draw(
            Rect rect,
            PungentSpatialAuthoringAsset asset,
            PungentSpatialPlanCanvasState state,
            PungentSpatialWorkbenchEditMode editMode,
            PungentSpatialWorkbenchTargetKind targetKind)
        {
            if (state == null)
                return;

            GUI.Box(rect, GUIContent.none);
            Event evt = Event.current;
            HandleCanvasNavigation(rect, state, evt);

            Rect content = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 22f);
            GUI.BeginClip(content);
            Rect localRect = new Rect(0f, 0f, content.width, content.height);
            DrawBackground(localRect, asset, state);

            if (asset != null)
            {
                DrawPaths(asset, state, editMode, targetKind, localRect);
                DrawAreas(asset, state, editMode, targetKind, localRect);
                DrawRegions(asset, state, editMode, localRect);
                HandleAddRemove(asset, state, editMode, targetKind, localRect);
            }

            GUI.EndClip();

            Rect status = new Rect(rect.x + 6f, rect.yMax - 20f, rect.width - 12f, 18f);
            EditorGUI.LabelField(status, asset == null ? "Create or assign a Spatial Authoring Asset to use Plan view." : state.status, EditorStyles.miniLabel);
        }

        public static void FrameAll(Rect rect, PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state)
        {
            if (asset == null || state == null || !asset.projection.IsValid)
                return;

            Vector2 size = asset.projection.Size;
            float scaleX = Mathf.Max(0.01f, (rect.width - 80f) / Mathf.Max(0.01f, size.x));
            float scaleY = Mathf.Max(0.01f, (rect.height - 80f) / Mathf.Max(0.01f, size.y));
            state.zoom = Mathf.Clamp(Mathf.Min(scaleX, scaleY), 0.05f, 8f);
            state.pan = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            state.status = "Framed projection bounds.";
        }

        private static void HandleCanvasNavigation(Rect rect, PungentSpatialPlanCanvasState state, Event evt)
        {
            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.ScrollWheel)
            {
                float oldZoom = state.zoom;
                state.zoom = Mathf.Clamp(state.zoom * (evt.delta.y > 0f ? 0.9f : 1.1f), 0.05f, 12f);
                Vector2 mouse = evt.mousePosition - rect.position;
                state.pan = mouse - (mouse - state.pan) * (state.zoom / Mathf.Max(0.001f, oldZoom));
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 2)
            {
                state.pan += evt.delta;
                evt.Use();
            }
        }

        private static void DrawBackground(Rect rect, PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state)
        {
            if (asset != null && asset.backgroundTexture != null)
            {
                Rect image = WorldBoundsToCanvasRect(asset.projection, state, rect);
                GUI.DrawTextureWithTexCoords(image, asset.backgroundTexture, new Rect(asset.backgroundUvMin, asset.backgroundUvMax - asset.backgroundUvMin), true);
                return;
            }

            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = new Color(0.24f, 0.24f, 0.24f, 0.35f);
            float grid = Mathf.Max(10f, 32f * state.zoom);
            for (float x = state.pan.x % grid; x < rect.width; x += grid)
                Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, rect.height));
            for (float y = state.pan.y % grid; y < rect.height; y += grid)
                Handles.DrawLine(new Vector3(0f, y), new Vector3(rect.width, y));
            Handles.color = previous;
            Handles.EndGUI();
        }

        private static void DrawPaths(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state, PungentSpatialWorkbenchEditMode editMode, PungentSpatialWorkbenchTargetKind targetKind, Rect rect)
        {
            if (asset.paths == null)
                return;

            for (int i = 0; i < asset.paths.Count; i++)
            {
                PungentSpatialPath path = asset.paths[i];
                if (path == null || !path.visible || path.worldPoints == null)
                    continue;

                Color color = path.Metadata.color;
                for (int p = 1; p < path.worldPoints.Count; p++)
                    DrawLine(WorldToCanvas(asset.projection, state, path.worldPoints[p - 1]), WorldToCanvas(asset.projection, state, path.worldPoints[p]), color, 2f);
                if (path.closedLoop && path.worldPoints.Count > 2)
                    DrawLine(WorldToCanvas(asset.projection, state, path.worldPoints[path.worldPoints.Count - 1]), WorldToCanvas(asset.projection, state, path.worldPoints[0]), color, 2f);

                for (int p = 0; p < path.worldPoints.Count; p++)
                    DrawPoint(asset, state, PungentSpatialItemKind.Path, i, p, WorldToCanvas(asset.projection, state, path.worldPoints[p]), color, editMode, targetKind, rect);
            }
        }

        private static void DrawAreas(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state, PungentSpatialWorkbenchEditMode editMode, PungentSpatialWorkbenchTargetKind targetKind, Rect rect)
        {
            if (asset.areas == null)
                return;

            for (int i = 0; i < asset.areas.Count; i++)
            {
                PungentSpatialArea area = asset.areas[i];
                if (area == null || !area.visible)
                    continue;

                Vector3[] polygon = area.GetWorldPolygon();
                if (polygon == null || polygon.Length == 0)
                    continue;

                for (int p = 0; p < polygon.Length; p++)
                    DrawLine(WorldToCanvas(asset.projection, state, polygon[p]), WorldToCanvas(asset.projection, state, polygon[(p + 1) % polygon.Length]), area.borderColor, 2f);

                if (area.shape == PungentSpatialAreaShapeMode.Polygon)
                {
                    for (int p = 0; p < area.worldPolygon.Count; p++)
                        DrawPoint(asset, state, PungentSpatialItemKind.Area, i, p, WorldToCanvas(asset.projection, state, area.worldPolygon[p]), area.borderColor, editMode, targetKind, rect);
                }
            }
        }

        private static void DrawRegions(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state, PungentSpatialWorkbenchEditMode editMode, Rect rect)
        {
            if (asset.regionSets == null)
                return;

            for (int setIndex = 0; setIndex < asset.regionSets.Count; setIndex++)
            {
                PungentSpatialRegionSet set = asset.regionSets[setIndex];
                if (set == null || !set.visible || set.vertices == null || set.faces == null)
                    continue;

                for (int f = 0; f < set.faces.Count; f++)
                {
                    PungentSpatialRegionFace face = set.faces[f];
                    if (face == null || !face.visible || face.outerLoop == null || face.outerLoop.Count < 2)
                        continue;

                    for (int i = 0; i < face.outerLoop.Count; i++)
                    {
                        int a = face.outerLoop[i];
                        int b = face.outerLoop[(i + 1) % face.outerLoop.Count];
                        if (a < 0 || b < 0 || a >= set.vertices.Count || b >= set.vertices.Count)
                            continue;
                        DrawLine(NormalizedToCanvas(asset.projection, state, set.vertices[a].normalizedPosition), NormalizedToCanvas(asset.projection, state, set.vertices[b].normalizedPosition), face.color, 1.5f);
                    }
                }
            }
        }

        private static void DrawPoint(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state, PungentSpatialItemKind kind, int itemIndex, int pointIndex, Vector2 canvas, Color color, PungentSpatialWorkbenchEditMode editMode, PungentSpatialWorkbenchTargetKind targetKind, Rect rect)
        {
            Rect hit = new Rect(canvas.x - PointHitSize * 0.5f, canvas.y - PointHitSize * 0.5f, PointHitSize, PointHitSize);
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.MoveArrow);

            Event evt = Event.current;
            bool selected = state.selectedKind == kind && state.selectedIndex == itemIndex && state.selectedVertex == pointIndex;
            Color pointColor = selected ? Color.yellow : color;
            EditorGUI.DrawRect(new Rect(canvas.x - PointDrawSize * 0.5f, canvas.y - PointDrawSize * 0.5f, PointDrawSize, PointDrawSize), pointColor);

            if (evt.type == EventType.MouseDown && evt.button == 0 && hit.Contains(evt.mousePosition - rect.position))
            {
                state.selectedKind = kind;
                state.selectedIndex = itemIndex;
                state.selectedVertex = pointIndex;
                state.draggingVertex = editMode == PungentSpatialWorkbenchEditMode.Move;
                state.status = $"Selected {kind} point {pointIndex + 1}.";
                evt.Use();
            }

            if (state.draggingVertex && selected && evt.type == EventType.MouseDrag && evt.button == 0)
            {
                Vector2 local = evt.mousePosition - rect.position;
                if (TryCanvasToWorld(asset.projection, state, local, out Vector3 world))
                {
                    Undo.RecordObject(asset, "Move Spatial Plan Point");
                    SetWorldPoint(asset, kind, itemIndex, pointIndex, world);
                    EditorUtility.SetDirty(asset);
                    state.status = $"Moved {kind} point {pointIndex + 1}.";
                }
                evt.Use();
            }

            if (state.draggingVertex && selected && evt.type == EventType.MouseUp && evt.button == 0)
            {
                state.draggingVertex = false;
                evt.Use();
            }
        }

        private static void SetWorldPoint(PungentSpatialAuthoringAsset asset, PungentSpatialItemKind kind, int itemIndex, int pointIndex, Vector3 world)
        {
            if (kind == PungentSpatialItemKind.Path && asset.paths != null && itemIndex >= 0 && itemIndex < asset.paths.Count)
            {
                PungentSpatialPath path = asset.paths[itemIndex];
                if (path != null && path.worldPoints != null && pointIndex >= 0 && pointIndex < path.worldPoints.Count)
                    path.worldPoints[pointIndex] = world;
            }
            else if (kind == PungentSpatialItemKind.Area && asset.areas != null && itemIndex >= 0 && itemIndex < asset.areas.Count)
            {
                PungentSpatialArea area = asset.areas[itemIndex];
                if (area != null && area.worldPolygon != null && pointIndex >= 0 && pointIndex < area.worldPolygon.Count)
                    area.worldPolygon[pointIndex] = world;
            }
        }

        private static void HandleAddRemove(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state, PungentSpatialWorkbenchEditMode editMode, PungentSpatialWorkbenchTargetKind targetKind, Rect rect)
        {
            if (asset == null || editMode != PungentSpatialWorkbenchEditMode.AddRemove)
                return;

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace))
            {
                RemoveSelectedPoint(asset, state);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                RemoveSelectedPoint(asset, state);
                evt.Use();
                return;
            }

            if (evt.type != EventType.MouseDown || evt.button != 0)
                return;

            if (!TryCanvasToWorld(asset.projection, state, evt.mousePosition - rect.position, out Vector3 world))
                return;

            Undo.RecordObject(asset, "Add Spatial Plan Point");
            if (targetKind == PungentSpatialWorkbenchTargetKind.Path)
            {
                PungentSpatialPath path = GetOrCreateSelectedPath(asset, state);
                InsertPathPoint(path, world);
                state.selectedKind = PungentSpatialItemKind.Path;
                state.selectedIndex = Mathf.Max(0, state.selectedIndex);
                state.selectedVertex = path.worldPoints.Count - 1;
                state.status = $"Added path point {state.selectedVertex + 1}.";
            }
            else if (targetKind == PungentSpatialWorkbenchTargetKind.Area)
            {
                PungentSpatialArea area = GetOrCreateSelectedArea(asset, state, world);
                area.shape = PungentSpatialAreaShapeMode.Polygon;
                if (area.worldPolygon == null)
                    area.worldPolygon = new List<Vector3>();
                area.worldPolygon.Add(world);
                state.selectedKind = PungentSpatialItemKind.Area;
                state.selectedIndex = Mathf.Max(0, state.selectedIndex);
                state.selectedVertex = area.worldPolygon.Count - 1;
                state.status = $"Added area vertex {state.selectedVertex + 1}.";
            }

            EditorUtility.SetDirty(asset);
            evt.Use();
        }

        private static PungentSpatialPath GetOrCreateSelectedPath(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state)
        {
            if (asset.paths == null)
                asset.paths = new List<PungentSpatialPath>();
            if (state.selectedKind == PungentSpatialItemKind.Path && state.selectedIndex >= 0 && state.selectedIndex < asset.paths.Count && asset.paths[state.selectedIndex] != null)
                return asset.paths[state.selectedIndex];

            PungentSpatialPath path = new PungentSpatialPath();
            path.Normalize($"Path {asset.paths.Count + 1}");
            asset.paths.Add(path);
            state.selectedIndex = asset.paths.Count - 1;
            return path;
        }

        private static PungentSpatialArea GetOrCreateSelectedArea(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state, Vector3 center)
        {
            if (asset.areas == null)
                asset.areas = new List<PungentSpatialArea>();
            if (state.selectedKind == PungentSpatialItemKind.Area && state.selectedIndex >= 0 && state.selectedIndex < asset.areas.Count && asset.areas[state.selectedIndex] != null)
                return asset.areas[state.selectedIndex];

            PungentSpatialArea area = new PungentSpatialArea { center = center };
            area.Normalize($"Area {asset.areas.Count + 1}");
            asset.areas.Add(area);
            state.selectedIndex = asset.areas.Count - 1;
            return area;
        }

        private static void InsertPathPoint(PungentSpatialPath path, Vector3 world)
        {
            if (path.worldPoints == null)
                path.worldPoints = new List<Vector3>();
            path.worldPoints.Add(world);
        }

        private static void RemoveSelectedPoint(PungentSpatialAuthoringAsset asset, PungentSpatialPlanCanvasState state)
        {
            if (asset == null || state.selectedVertex < 0)
                return;

            if (state.selectedKind == PungentSpatialItemKind.Path && asset.paths != null && state.selectedIndex >= 0 && state.selectedIndex < asset.paths.Count)
            {
                PungentSpatialPath path = asset.paths[state.selectedIndex];
                if (path != null && path.worldPoints != null && path.worldPoints.Count > 0 && state.selectedVertex < path.worldPoints.Count)
                {
                    Undo.RecordObject(asset, "Remove Spatial Path Point");
                    path.worldPoints.RemoveAt(state.selectedVertex);
                    state.status = "Removed path point.";
                    state.selectedVertex = -1;
                    EditorUtility.SetDirty(asset);
                }
            }
            else if (state.selectedKind == PungentSpatialItemKind.Area && asset.areas != null && state.selectedIndex >= 0 && state.selectedIndex < asset.areas.Count)
            {
                PungentSpatialArea area = asset.areas[state.selectedIndex];
                if (area != null && area.worldPolygon != null && area.worldPolygon.Count > 0 && state.selectedVertex < area.worldPolygon.Count)
                {
                    Undo.RecordObject(asset, "Remove Spatial Area Point");
                    area.worldPolygon.RemoveAt(state.selectedVertex);
                    state.status = "Removed area vertex.";
                    state.selectedVertex = -1;
                    EditorUtility.SetDirty(asset);
                }
            }
        }

        private static Vector2 WorldToCanvas(PungentSpatialProjection projection, PungentSpatialPlanCanvasState state, Vector3 world)
        {
            if (!projection.TryWorldToNormalized(world, out Vector2 normalized))
                normalized = Vector2.zero;
            return NormalizedToCanvas(projection, state, normalized);
        }

        private static Vector2 NormalizedToCanvas(PungentSpatialProjection projection, PungentSpatialPlanCanvasState state, Vector2 normalized)
        {
            Vector2 size = projection.Size;
            return state.pan + new Vector2((normalized.x - 0.5f) * size.x * state.zoom, (0.5f - normalized.y) * size.y * state.zoom);
        }

        private static bool TryCanvasToWorld(PungentSpatialProjection projection, PungentSpatialPlanCanvasState state, Vector2 canvas, out Vector3 world)
        {
            world = default;
            if (!projection.IsValid)
                return false;

            Vector2 size = projection.Size;
            Vector2 normalized = new Vector2(
                0.5f + (canvas.x - state.pan.x) / Mathf.Max(0.001f, size.x * state.zoom),
                0.5f - (canvas.y - state.pan.y) / Mathf.Max(0.001f, size.y * state.zoom));
            return projection.TryNormalizedToWorld(normalized, projection.origin.y, out world);
        }

        private static Rect WorldBoundsToCanvasRect(PungentSpatialProjection projection, PungentSpatialPlanCanvasState state, Rect rect)
        {
            Vector2 size = projection.Size * state.zoom;
            Vector2 center = state.pan;
            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = color;
            Handles.DrawAAPolyLine(width, a, b);
            Handles.color = previous;
            Handles.EndGUI();
        }
    }
#endif
}
