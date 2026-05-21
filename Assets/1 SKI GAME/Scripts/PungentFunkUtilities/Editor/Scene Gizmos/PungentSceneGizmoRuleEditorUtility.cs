using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    internal static class PungentSceneGizmoRuleEditorUtility
    {
        public static void DrawStatusChip(PungentSceneGizmoSource.RuleStatus status, float width = 62f)
        {
            if (status == null)
                return;

            UtilityWindowTheme.CountPill(StatusLabel(status), StatusTint(status), width);
        }

        public static string StatusLabel(PungentSceneGizmoSource.RuleStatus status)
        {
            if (status == null)
                return "Unknown";

            if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Error)
                return "Invalid";
            if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Warning)
                return "Warn";
            if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Info)
                return "Info";
            return "Ready";
        }

        public static Color StatusTint(PungentSceneGizmoSource.RuleStatus status)
        {
            if (status == null)
                return UtilityWindowTheme.Neutral;

            if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Error)
                return UtilityWindowTheme.Red;
            if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Warning)
                return UtilityWindowTheme.Amber;
            if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Info)
                return UtilityWindowTheme.Cyan;
            return UtilityWindowTheme.Green;
        }

        public static void DrawRuleMessages(PungentSceneGizmoSource.RuleStatus status)
        {
            if (status == null || status.messages == null)
                return;

            MessageType messageType = MessageType.None;
            if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Error)
                messageType = MessageType.Error;
            else if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Warning)
                messageType = MessageType.Warning;
            else if (status.severity == PungentSceneGizmoSource.RuleStatusSeverity.Info)
                messageType = MessageType.Info;

            for (int m = 0; m < status.messages.Count; m++)
            {
                if (string.Equals(status.messages[m], "Ready.", System.StringComparison.Ordinal))
                    continue;
                EditorGUILayout.HelpBox(status.messages[m], messageType);
            }
        }

        public static void DrawCompactRuleProperties(SerializedProperty rule, PungentSceneGizmoSource.RuleStatus status, bool showAdvanced)
        {
            if (rule == null)
                return;

            SerializedProperty shapeProperty = rule.FindPropertyRelative("shape");
            SerializedProperty positionModeProperty = rule.FindPropertyRelative("positionMode");
            SerializedProperty sizeModeProperty = rule.FindPropertyRelative("sizeMode");
            SerializedProperty conditionProperty = rule.FindPropertyRelative("condition");
            SerializedProperty useStateColorMap = rule.FindPropertyRelative("useStateColorMap");

            PungentSceneGizmoSource.GizmoShape shape = shapeProperty != null
                ? (PungentSceneGizmoSource.GizmoShape)shapeProperty.enumValueIndex
                : PungentSceneGizmoSource.GizmoShape.WireSphere;
            PungentSceneGizmoSource.PositionMode positionMode = positionModeProperty != null
                ? (PungentSceneGizmoSource.PositionMode)positionModeProperty.enumValueIndex
                : PungentSceneGizmoSource.PositionMode.Transform;
            PungentSceneGizmoSource.SizeMode sizeMode = sizeModeProperty != null
                ? (PungentSceneGizmoSource.SizeMode)sizeModeProperty.enumValueIndex
                : PungentSceneGizmoSource.SizeMode.Constant;
            PungentSceneGizmoSource.ConditionMode condition = conditionProperty != null
                ? (PungentSceneGizmoSource.ConditionMode)conditionProperty.enumValueIndex
                : PungentSceneGizmoSource.ConditionMode.Always;

            DrawRuleMessages(status);

            UtilityWindowTheme.SectionTitle("Block", UtilityWindowTheme.Cyan);
            DrawProperty(rule, "name");
            DrawProperty(rule, "enabled");
            DrawProperty(rule, "shape");
            DrawProperty(rule, "drawWhen");
            DrawProperty(rule, "color");

            UtilityWindowTheme.SectionTitle("Position", UtilityWindowTheme.Teal);
            DrawProperty(rule, "positionMode");
            if (positionMode == PungentSceneGizmoSource.PositionMode.LocalOffset)
                DrawProperty(rule, "localOffset");
            if (positionMode == PungentSceneGizmoSource.PositionMode.WorldPosition)
                DrawProperty(rule, "worldPosition");
            if (positionMode == PungentSceneGizmoSource.PositionMode.FieldVector3)
                DrawProperty(rule, "positionFieldPath", new GUIContent("Position Field"));

            UtilityWindowTheme.SectionTitle("Size", UtilityWindowTheme.Teal);
            DrawProperty(rule, "sizeMode");
            if (sizeMode == PungentSceneGizmoSource.SizeMode.Constant)
                DrawFloatSlider(rule, "size", "Size", 0.01f, 25f);
            if (ShapeUsesVectorSize(shape))
                DrawProperty(rule, "vectorSize");
            if (sizeMode == PungentSceneGizmoSource.SizeMode.FieldFloat || sizeMode == PungentSceneGizmoSource.SizeMode.FieldVector3Magnitude)
                DrawProperty(rule, "sizeFieldPath", new GUIContent("Size Field"));

            if (ShapeUsesDirection(shape) || HasString(rule, "directionFieldPath"))
            {
                UtilityWindowTheme.SectionTitle("Direction", UtilityWindowTheme.Purple);
                DrawProperty(rule, "direction");
                DrawProperty(rule, "directionFieldPath", new GUIContent("Direction Field"));
                DrawProperty(rule, "useTargetRotation");
            }

            if (ShapeCanShowLabel(shape) || HasString(rule, "label") || HasString(rule, "labelFieldPath"))
            {
                UtilityWindowTheme.SectionTitle("Label & State", UtilityWindowTheme.Green);
                DrawProperty(rule, "label");
                DrawProperty(rule, "labelFieldPath", new GUIContent("Label Field"));
                DrawProperty(rule, "useStateColorMap");
                if (useStateColorMap != null && useStateColorMap.boolValue)
                    DrawProperty(rule, "stateColorMap", includeChildren: true);
            }

            if (condition != PungentSceneGizmoSource.ConditionMode.Always || showAdvanced)
            {
                UtilityWindowTheme.SectionTitle("Condition", UtilityWindowTheme.Amber);
                DrawProperty(rule, "condition");
                if (condition != PungentSceneGizmoSource.ConditionMode.Always)
                {
                    DrawProperty(rule, "conditionComponent");
                    DrawProperty(rule, "conditionFieldPath");
                    DrawProperty(rule, "conditionExpectedValue");
                }
            }

            if (showAdvanced)
            {
                UtilityWindowTheme.SectionTitle("Targets", UtilityWindowTheme.Cyan);
                DrawProperty(rule, "targetTransform");
                DrawProperty(rule, "targetComponent");
                DrawProperty(rule, "secondaryTransform");
                DrawProperty(rule, "secondaryComponent");

                UtilityWindowTheme.SectionTitle("Performance", UtilityWindowTheme.Amber);
                DrawProperty(rule, "maxDrawDistance");
                DrawProperty(rule, "drawLabelsWhenSelectedOnly");

                UtilityWindowTheme.SectionTitle("Preset Metadata", UtilityWindowTheme.Neutral);
                DrawProperty(rule, "presetId");
                DrawProperty(rule, "presetCategory");
            }
        }

        public static bool ShapeUsesVectorSize(PungentSceneGizmoSource.GizmoShape shape)
        {
            return shape == PungentSceneGizmoSource.GizmoShape.Cube ||
                   shape == PungentSceneGizmoSource.GizmoShape.WireCube ||
                   shape == PungentSceneGizmoSource.GizmoShape.Bounds;
        }

        public static bool ShapeUsesDirection(PungentSceneGizmoSource.GizmoShape shape)
        {
            return shape == PungentSceneGizmoSource.GizmoShape.Line ||
                   shape == PungentSceneGizmoSource.GizmoShape.Ray ||
                   shape == PungentSceneGizmoSource.GizmoShape.Arrow ||
                   shape == PungentSceneGizmoSource.GizmoShape.Disc;
        }

        public static bool ShapeCanShowLabel(PungentSceneGizmoSource.GizmoShape shape)
        {
            return shape == PungentSceneGizmoSource.GizmoShape.Label ||
                   shape == PungentSceneGizmoSource.GizmoShape.StateLabel ||
                   shape == PungentSceneGizmoSource.GizmoShape.DistanceBetween ||
                   shape == PungentSceneGizmoSource.GizmoShape.Sphere ||
                   shape == PungentSceneGizmoSource.GizmoShape.WireSphere ||
                   shape == PungentSceneGizmoSource.GizmoShape.Arrow ||
                   shape == PungentSceneGizmoSource.GizmoShape.Ray ||
                   shape == PungentSceneGizmoSource.GizmoShape.Disc ||
                   shape == PungentSceneGizmoSource.GizmoShape.ColliderBounds;
        }

        private static void DrawProperty(SerializedProperty parent, string relativeName, GUIContent label = null, bool includeChildren = false)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativeName);
            if (property == null)
                return;

            if (label != null)
                EditorGUILayout.PropertyField(property, label, includeChildren);
            else
                EditorGUILayout.PropertyField(property, includeChildren);
        }

        private static void DrawFloatSlider(SerializedProperty parent, string relativeName, string label, float min, float max)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativeName);
            if (property == null)
                return;

            property.floatValue = EditorGUILayout.Slider(label, property.floatValue, min, max);
        }

        private static bool HasString(SerializedProperty parent, string relativeName)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativeName);
            return property != null && !string.IsNullOrWhiteSpace(property.stringValue);
        }
    }
#endif
}
