using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public static class PungentSceneHelpOverlay
    {
        public static void DrawPanel(SceneView sceneView, string title, string body, Color tint, int width = 320)
        {
            if (sceneView == null || Event.current == null)
                return;

            Handles.BeginGUI();
            Rect rect = new Rect(12f, 12f, width, 88f);
            GUILayout.BeginArea(rect, UtilityWindowTheme.PanelStyle(tint, 0.28f, 0.14f, 8, 0));
            GUILayout.Label(title, UtilityWindowTheme.SectionHeaderStyle);
            GUILayout.Label(body, UtilityWindowTheme.MutedMiniLabelStyle);
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
    #endif

}