//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEditorInternal;
//using UnityEngine;
//using SkiGame.Map.UI;

//[CustomEditor(typeof(MapUIStyleSettings))]
//public sealed class MapUIStyleSettingsEditor : Editor
//{
//    private ReorderableList _visibilityRulesList;

//    private SerializedProperty _runLabelAnchorModeProp;
//    private SerializedProperty _visibilityRulesProp;

//    private void OnEnable()
//    {
//        _runLabelAnchorModeProp = serializedObject.FindProperty("runLabelAnchorMode");
//        _visibilityRulesProp = serializedObject.FindProperty("visibilityRules");

//        _visibilityRulesList = new ReorderableList(serializedObject, _visibilityRulesProp, true, true, true, true);
//        _visibilityRulesList.drawHeaderCallback = rect =>
//        {
//            EditorGUI.LabelField(rect, "Visibility Rules");
//        };

//        _visibilityRulesList.elementHeightCallback = index =>
//        {
//            SerializedProperty element = _visibilityRulesProp.GetArrayElementAtIndex(index);
//            return EditorGUI.GetPropertyHeight(element, true) + 6f;
//        };

//        _visibilityRulesList.drawElementCallback = (rect, index, isActive, isFocused) =>
//        {
//            SerializedProperty element = _visibilityRulesProp.GetArrayElementAtIndex(index);
//            SerializedProperty elementNameProp = element.FindPropertyRelative("elementName");
//            SerializedProperty semanticProp = element.FindPropertyRelative("semantic");

//            string title = !string.IsNullOrWhiteSpace(elementNameProp.stringValue)
//                ? elementNameProp.stringValue
//                : semanticProp.enumDisplayNames[semanticProp.enumValueIndex];

//            rect.y += 2f;
//            Rect foldoutRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

//            element.isExpanded = EditorGUI.Foldout(
//                foldoutRect,
//                element.isExpanded,
//                $"{index + 1}. {title}",
//                true
//            );

//            if (!element.isExpanded)
//                return;

//            Rect contentRect = new Rect(
//                rect.x,
//                rect.y + EditorGUIUtility.singleLineHeight + 2f,
//                rect.width,
//                EditorGUI.GetPropertyHeight(element, true) - EditorGUIUtility.singleLineHeight
//            );

//            EditorGUI.indentLevel++;
//            EditorGUI.PropertyField(contentRect, element, GUIContent.none, true);
//            EditorGUI.indentLevel--;
//        };
//    }

//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        DrawPropertiesExcluding(
//            serializedObject,
//            "m_Script",
//            "runLabelAnchorMode",
//            "visibilityRules"
//        );

//        EditorGUILayout.Space();
//        EditorGUILayout.PropertyField(_runLabelAnchorModeProp);
//        EditorGUILayout.Space();

//        _visibilityRulesList.DoLayoutList();

//        serializedObject.ApplyModifiedProperties();
//    }
//}
//#endif