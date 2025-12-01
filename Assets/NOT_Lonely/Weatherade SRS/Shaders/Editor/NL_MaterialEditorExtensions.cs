using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MaterialEditorExtension
{
    public static void MinMaxSliderWithFloats(this MaterialEditor editor, MaterialProperty remapProp, float minLimit, float maxLimit, GUIContent label)
    {
        MaterialEditor.BeginProperty(remapProp);

        Vector2 remap = remapProp.vectorValue;

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PrefixLabel(label);

        //remap.x = EditorGUILayout.FloatField(remap.x, GUILayout.MaxWidth(50));
        EditorGUILayout.MinMaxSlider(GUIContent.none, ref remap.x, ref remap.y, minLimit, maxLimit);
        //remap.y = EditorGUILayout.FloatField(remap.y, GUILayout.MaxWidth(50));

        if (EditorGUI.EndChangeCheck())
            remapProp.vectorValue = remap;

        EditorGUILayout.EndHorizontal();
        MaterialEditor.EndProperty();
    }
}
