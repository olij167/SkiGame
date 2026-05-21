using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PungentSceneGizmoSource))]
    public sealed class PungentSceneGizmoSourceEditor : Editor
    {
        private SerializedProperty _drawInScene;
        private SerializedProperty _drawLabels;
        private SerializedProperty _drawOnlyWhenComponentEnabled;
        private SerializedProperty _rules;
        private PungentSceneGizmoSource.TemplateKind _template = PungentSceneGizmoSource.TemplateKind.TriggerRadius;

        private void OnEnable()
        {
            _drawInScene = serializedObject.FindProperty("drawInScene");
            _drawLabels = serializedObject.FindProperty("drawLabels");
            _drawOnlyWhenComponentEnabled = serializedObject.FindProperty("drawOnlyWhenComponentEnabled");
            _rules = serializedObject.FindProperty("rules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PungentSceneGizmoSource source = (PungentSceneGizmoSource)target;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Scene Gizmo Source", UtilityWindowTheme.Blue, _rules != null ? _rules.arraySize + " rules" : null);
                EditorGUILayout.PropertyField(_drawInScene);
                EditorGUILayout.PropertyField(_drawLabels);
                EditorGUILayout.PropertyField(_drawOnlyWhenComponentEnabled);
                EditorGUILayout.HelpBox("Start with a template, then bind position/size/label/conditions to transforms, constants, or reflected fields. This avoids writing custom OnDrawGizmos code for common debugging tasks.", MessageType.Info);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Add Template", UtilityWindowTheme.Teal);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _template = (PungentSceneGizmoSource.TemplateKind)EditorGUILayout.EnumPopup(_template);
                    if (UtilityWindowTheme.TintedButton("Add", UtilityWindowTheme.Green, GUILayout.Width(64f)))
                    {
                        Undo.RecordObject(source, "Add Scene Gizmo Template");
                        source.AddTemplate(_template);
                        EditorUtility.SetDirty(source);
                        serializedObject.Update();
                    }
                    if (GUILayout.Button("Browser", GUILayout.Width(72f)))
                        PungentGizmoBrowserWindow.Open();
                }
            }

            DrawRules();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRules()
        {
            if (_rules == null)
                return;

            for (int i = 0; i < _rules.arraySize; i++)
            {
                SerializedProperty rule = _rules.GetArrayElementAtIndex(i);
                SerializedProperty name = rule.FindPropertyRelative("name");
                SerializedProperty enabled = rule.FindPropertyRelative("enabled");

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(enabled.boolValue ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Amber, 0.12f, 0.05f, 7, 4)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
                        rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, string.IsNullOrWhiteSpace(name.stringValue) ? "Gizmo Rule" : name.stringValue, true);
                        if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(72f)))
                        {
                            _rules.InsertArrayElementAtIndex(i);
                            GUIUtility.ExitGUI();
                        }
                        if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(24f)))
                        {
                            _rules.DeleteArrayElementAtIndex(i);
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (!rule.isExpanded)
                        continue;

                    EditorGUILayout.PropertyField(name);
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("shape"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("drawWhen"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("color"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("targetTransform"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("secondaryTransform"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("targetComponent"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("secondaryComponent"));

                    EditorGUILayout.Space(4f);
                    UtilityWindowTheme.SectionTitle("Bindings", UtilityWindowTheme.Cyan);
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("positionMode"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("localOffset"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("worldPosition"));
                    DrawPathField(rule, "positionFieldPath", "Position Field", typeof(Vector3));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("sizeMode"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("size"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("vectorSize"));
                    DrawPathField(rule, "sizeFieldPath", "Size Field", null);
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("direction"));
                    DrawPathField(rule, "directionFieldPath", "Direction Field", typeof(Vector3));
                    DrawPathField(rule, "labelFieldPath", "Label Field", null);
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("label"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("useTargetRotation"));

                    EditorGUILayout.Space(4f);
                    UtilityWindowTheme.SectionTitle("Conditions", UtilityWindowTheme.Amber);
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("condition"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("conditionComponent"));
                    DrawPathField(rule, "conditionFieldPath", "Condition Field", null, "conditionComponent");
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("conditionExpectedValue"));
                }
            }
        }

        private static void DrawPathField(SerializedProperty rule, string propertyName, string label, Type preferredType, string componentPropertyName = "targetComponent")
        {
            SerializedProperty path = rule.FindPropertyRelative(propertyName);
            SerializedProperty componentProperty = rule.FindPropertyRelative(componentPropertyName);
            using (new EditorGUILayout.HorizontalScope())
            {
                path.stringValue = EditorGUILayout.TextField(label, path.stringValue);
                if (GUILayout.Button("Pick", GUILayout.Width(48f)))
                {
                    GenericMenu menu = new GenericMenu();
                    Component component = componentProperty.objectReferenceValue as Component;
                    if (component == null)
                        menu.AddDisabledItem(new GUIContent("Assign a component first"));
                    else
                        PopulateMemberMenu(menu, component.GetType(), preferredType, path);
                    menu.ShowAsContext();
                }
            }
        }

        private static void PopulateMemberMenu(GenericMenu menu, Type type, Type preferredType, SerializedProperty path)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo field in type.GetFields(flags).Where(f => IsSupported(f.FieldType, preferredType)).OrderBy(f => f.Name))
            {
                string name = field.Name;
                menu.AddItem(new GUIContent(name + " : " + field.FieldType.Name), string.Equals(path.stringValue, name), () => path.stringValue = name);
            }
            foreach (PropertyInfo property in type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0 && IsSupported(p.PropertyType, preferredType)).OrderBy(p => p.Name))
            {
                string name = property.Name;
                menu.AddItem(new GUIContent(name + " : " + property.PropertyType.Name), string.Equals(path.stringValue, name), () => path.stringValue = name);
            }
        }

        private static bool IsSupported(Type type, Type preferredType)
        {
            if (preferredType != null)
                return type == preferredType;
            return type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(string) || type.IsEnum || type == typeof(Vector2) || type == typeof(Vector3) || typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
        private static void DrawEditorGizmos(PungentSceneGizmoSource source, GizmoType gizmoType)
        {
            if (source == null || !source.drawInScene || source.rules == null)
                return;

            bool selected = (gizmoType & GizmoType.Selected) != 0 || (gizmoType & GizmoType.Active) != 0;
            foreach (PungentSceneGizmoSource.GizmoRule rule in source.rules)
            {
                if (!source.ShouldDraw(rule, selected))
                    continue;

                Vector3 position = source.ResolvePosition(rule);
                Vector3 direction = source.ResolveDirection(rule);
                float size = source.ResolveSize(rule);

                Color previous = Handles.color;
                Handles.color = rule.color;

                if (rule.shape == PungentSceneGizmoSource.GizmoShape.Disc)
                    Handles.DrawWireDisc(position, direction.sqrMagnitude > 0.001f ? direction : Vector3.up, size);
                else if (rule.shape == PungentSceneGizmoSource.GizmoShape.Arrow)
                    Handles.ArrowHandleCap(0, position, Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward), size, EventType.Repaint);
                else if (rule.shape == PungentSceneGizmoSource.GizmoShape.DistanceBetween && rule.secondaryTransform != null)
                    Handles.Label(position, source.ResolveLabel(rule));

                if (source.drawLabels)
                {
                    string label = source.ResolveLabel(rule);
                    if (!string.IsNullOrWhiteSpace(label))
                        Handles.Label(position + Vector3.up * (HandleUtility.GetHandleSize(position) * 0.08f + 0.1f), label);
                }

                Handles.color = previous;
            }
        }
    }
    #endif

}