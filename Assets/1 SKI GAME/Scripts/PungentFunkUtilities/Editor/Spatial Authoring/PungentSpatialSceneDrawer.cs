using System.Collections.Generic;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal static class PungentSpatialSceneDrawer
    {
        private const int MaxDrawnLabels = 48;
        private static double _nextAllowedSceneRepaintTime;

        public static void DrawAsset(PungentSpatialAuthoringAsset asset, bool selectedOnly, bool drawLabels)
        {
            if (asset == null)
                return;

            int labels = 0;
            if (asset.paths != null)
            {
                for (int i = 0; i < asset.paths.Count; i++)
                    DrawPath(asset.paths[i], drawLabels && labels++ < MaxDrawnLabels);
            }

            if (asset.areas != null)
            {
                for (int i = 0; i < asset.areas.Count; i++)
                    DrawArea(asset.areas[i], drawLabels && labels++ < MaxDrawnLabels);
            }
        }

        private static void DrawPath(PungentSpatialPath path, bool drawLabel)
        {
            if (path == null || !path.visible || path.worldPoints == null || path.worldPoints.Count < 2)
                return;

            Handles.color = path.Metadata.color;
            for (int i = 1; i < path.worldPoints.Count; i++)
                Handles.DrawLine(path.worldPoints[i - 1], path.worldPoints[i]);
            if (path.closedLoop && path.worldPoints.Count > 2)
                Handles.DrawLine(path.worldPoints[path.worldPoints.Count - 1], path.worldPoints[0]);

            if (drawLabel)
                Handles.Label(path.worldPoints[0], path.DisplayName);
        }

        private static void DrawArea(PungentSpatialArea area, bool drawLabel)
        {
            if (area == null || !area.visible)
                return;

            Vector3[] polygon = area.GetWorldPolygon();
            if (polygon == null || polygon.Length < 2)
                return;

            Handles.color = area.borderColor;
            for (int i = 0; i < polygon.Length; i++)
                Handles.DrawLine(polygon[i], polygon[(i + 1) % polygon.Length]);

            Color fill = area.fillColor;
            fill.a = Mathf.Clamp01(fill.a);
            Handles.color = fill;
            if (polygon.Length >= 3)
                Handles.DrawAAConvexPolygon(polygon);

            if (drawLabel)
                Handles.Label(polygon[0], area.DisplayName);
        }

        public static void RequestThrottledRepaint()
        {
            PungentEditorPerformanceUtility.RequestLastActiveSceneViewRepaintThrottled(ref _nextAllowedSceneRepaintTime, 0.10d);
        }
    }
#endif
}
