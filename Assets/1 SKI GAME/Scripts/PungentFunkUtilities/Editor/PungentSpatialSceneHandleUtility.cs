using System.Collections.Generic;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    internal static class PungentSpatialSceneHandleUtility
    {
        private const int PointControlSalt = 0x51A7;
        private const int EdgeControlSalt = 0x45D6;
        private const int PassiveControlSalt = 0x71C3;
        private static readonly HashSet<int> s_SpatialControls = new HashSet<int>();
        private static readonly HashSet<int> s_PointControls = new HashSet<int>();
        private static readonly HashSet<int> s_EdgeControls = new HashSet<int>();

        public static void BeginSceneGUI()
        {
            if (Event.current != null && Event.current.type == EventType.Layout)
            {
                s_SpatialControls.Clear();
                s_PointControls.Clear();
                s_EdgeControls.Clear();
            }
        }

        public static void ProtectSelectionIfNeeded(bool shouldCapture)
        {
            Event e = Event.current;
            if (!shouldCapture || e == null || e.type != EventType.Layout)
                return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(PassiveControlSalt, FocusType.Passive));
        }

        public static int GetPointControlId(Object owner, int pointIndex)
        {
            int ownerId = owner != null ? owner.GetHashCode() : 0;
            return GUIUtility.GetControlID(ownerId ^ (pointIndex * 397) ^ PointControlSalt, FocusType.Passive);
        }

        public static int GetEdgeControlId(Object owner, int edgeIndex)
        {
            int ownerId = owner != null ? owner.GetHashCode() : 0;
            return GUIUtility.GetControlID(ownerId ^ (edgeIndex * 521) ^ EdgeControlSalt, FocusType.Passive);
        }

        public static bool RegisterPointControl(int controlId, Vector3 worldPoint, float pickRadius)
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.Layout)
            {
                s_SpatialControls.Add(controlId);
                s_PointControls.Add(controlId);
                HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(worldPoint, Mathf.Max(0.01f, pickRadius)));
            }

            return HandleUtility.nearestControl == controlId;
        }

        public static bool RegisterEdgeControl(int controlId, Vector3 start, Vector3 end)
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.Layout)
            {
                s_SpatialControls.Add(controlId);
                s_EdgeControls.Add(controlId);
                HandleUtility.AddControl(controlId, HandleUtility.DistanceToLine(start, end));
            }

            return HandleUtility.nearestControl == controlId;
        }

        public static bool IsNearestSpatialControl()
        {
            return s_SpatialControls.Contains(HandleUtility.nearestControl);
        }

        public static bool IsNearestPointControl()
        {
            return s_PointControls.Contains(HandleUtility.nearestControl);
        }

        public static bool IsNearestEdgeControl()
        {
            return s_EdgeControls.Contains(HandleUtility.nearestControl);
        }

        public static bool IsSceneNavigationEvent(Event e)
        {
            return e != null && (e.alt || e.button == 1 || e.button == 2);
        }

        public static bool TryBeginPointDrag(int controlId, Event e)
        {
            if (e == null || e.type != EventType.MouseDown || e.button != 0 || IsSceneNavigationEvent(e))
                return false;

            if (HandleUtility.nearestControl != controlId)
                return false;

            GUIUtility.hotControl = controlId;
            GUIUtility.keyboardControl = 0;
            e.Use();
            return true;
        }

        public static bool IsDraggingPoint(int controlId, Event e)
        {
            return e != null && e.type == EventType.MouseDrag && GUIUtility.hotControl == controlId;
        }

        public static bool TryEndPointDrag(int controlId, Event e)
        {
            if (e == null || e.rawType != EventType.MouseUp || GUIUtility.hotControl != controlId)
                return false;

            GUIUtility.hotControl = 0;
            e.Use();
            return true;
        }

        public static void DrawPointCap(int controlId, Vector3 worldPoint, float visualSize, Color color)
        {
            Color previous = Handles.color;
            Handles.color = color;
            Handles.SphereHandleCap(controlId, worldPoint, Quaternion.identity, Mathf.Max(0.01f, visualSize), EventType.Repaint);
            Handles.color = previous;
        }
    }
#endif
}
