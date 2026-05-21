using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal static class PungentSpatialSceneHandleTool
    {
        public static void DrawPathPointHandles(PungentSpatialAuthoringAsset asset, int pathIndex, PungentSpatialWorkbenchEditMode editMode)
        {
            if (asset == null || asset.paths == null || pathIndex < 0 || pathIndex >= asset.paths.Count)
                return;

            PungentSpatialPath path = asset.paths[pathIndex];
            if (path == null || path.locked || path.worldPoints == null)
                return;

            for (int i = 0; i < path.worldPoints.Count; i++)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(path.worldPoints[i], Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(asset, "Move Spatial Path Point");
                    path.worldPoints[i] = moved;
                    EditorUtility.SetDirty(asset);
                }
            }
        }

        public static void DrawAreaPointHandles(PungentSpatialAuthoringAsset asset, int areaIndex, PungentSpatialWorkbenchEditMode editMode)
        {
            if (asset == null || asset.areas == null || areaIndex < 0 || areaIndex >= asset.areas.Count)
                return;

            PungentSpatialArea area = asset.areas[areaIndex];
            if (area == null || area.locked || area.shape != PungentSpatialAreaShapeMode.Polygon || area.worldPolygon == null)
                return;

            for (int i = 0; i < area.worldPolygon.Count; i++)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(area.worldPolygon[i], Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(asset, "Move Spatial Area Point");
                    area.worldPolygon[i] = moved;
                    EditorUtility.SetDirty(asset);
                }
            }
        }
    }
#endif
}
