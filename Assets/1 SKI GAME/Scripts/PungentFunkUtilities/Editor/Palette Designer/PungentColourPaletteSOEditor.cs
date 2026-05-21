using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Colour;

namespace PungentFunk.Utilities.Editor.Colour
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PungentColourPaletteSO))]
    public class PungentColourPaletteSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.14f, 0.08f)))
            {
                UtilityWindowTheme.SectionTitle("Palette Designer", UtilityWindowTheme.Cyan);
                EditorGUILayout.LabelField("Open this palette in the generic PungentFunk Utilities Palette Designer window.", UtilityWindowTheme.MutedMiniLabelStyle);
                if (UtilityWindowTheme.TintedButton("Open In Palette Designer", UtilityWindowTheme.Cyan, GUILayout.Height(26f)))
                {
                    Selection.activeObject = target;
                    PaletteDesignerWindow.Open();
                }
            }
        }
    }
    #endif

}