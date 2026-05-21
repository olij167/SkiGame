namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Shared IMGUI compound control for the lightweight Minimized Utilities overlay.
    /// Kept IMGUI-first by design: this is a small Core editor status control, not a persistent flagship UI surface.
    /// </summary>
    public static class PungentMinimizedUtilitiesButton
    {
        private static readonly GUILayoutOption[] HeaderOptions =
        {
            GUILayout.Width(104f),
            GUILayout.Height(22f)
        };

        public static bool DrawHeaderPill()
        {
            return DrawButton(HeaderOptions);
        }

        public static bool DrawToolbarButton(params GUILayoutOption[] options)
        {
            return DrawButton(options);
        }

        private static bool DrawButton(params GUILayoutOption[] options)
        {
            int count = PungentUtilityMinimizer.MinimizedCount;
            string label = count == 1 ? "Minimized 1" : "Minimized " + count;
            string tooltip = count == 0
                ? "Open minimized utilities tray. No utilities are currently minimized."
                : "Open minimized utilities tray to restore or remove minimized utility windows.";

            GUIContent content = new GUIContent(label, tooltip);
            GUIStyle style = EditorStyles.miniButton;

            Rect rect = GUILayoutUtility.GetRect(content, style, options ?? System.Array.Empty<GUILayoutOption>());
            bool clicked = GUI.Button(rect, content, style);

            if (clicked)
            {
                Vector2 screenPosition = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
                PungentUtilityMinimizer.ToggleOverlayTrayFromAccess(new Rect(screenPosition, rect.size));
                GUIUtility.ExitGUI();
            }

            return clicked;
        }
    }
#endif
}
