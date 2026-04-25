using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrickPoseCoveragePlanSO))]
public sealed class TrickPoseCoveragePlanSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox("This plan defines enum-owned pose slots. The normal workflow is family x shape with vertical orientation, horizontal orientation, and motion as page/filter dimensions.", MessageType.Info);

        DrawSection("Coverage Participation", "Plan.CoverageParticipation", () =>
        {
            DrawProperty("includeAirborne", "Include Airborne");
            DrawProperty("includeGrounded", "Include Grounded");
            DrawProperty("includePoseHeld", "Include Pose Held");
            DrawProperty("includePoseReleased", "Include Pose Released");
            DrawProperty("usePoseFamily", "Use Pose Family");
            DrawProperty("usePoseShape", "Use Pose Shape");
            DrawProperty("useVerticalOrientation", "Use Vertical Orientation");
            DrawProperty("useHorizontalOrientation", "Use Horizontal Orientation");
            DrawProperty("useMotionState", "Use Motion State");
        });

        DrawSection("Allowed Values", "Plan.AllowedValues", () =>
        {
            DrawProperty("poseFamilies", "Pose Families");
            DrawProperty("poseShapes", "Pose Shapes");
            DrawProperty("verticalOrientations", "Vertical Orientations");
            DrawProperty("horizontalOrientations", "Horizontal Orientations");
            DrawProperty("motionStates", "Motion States");
        });

        DrawSection("Advanced / Legacy Domains", "Plan.AngularBuckets", () =>
        {
            EditorGUILayout.LabelField("These legacy/debug domains are intentionally outside the normal coverage matrix.", EditorStyles.wordWrappedMiniLabel);
            DrawProperty("useOrientation", "Use Legacy Orientation");
            DrawProperty("orientationModifiers", "Legacy Orientation Modifiers");
            DrawProperty("useSpinDirection", "Use Spin Direction");
            DrawProperty("spinDirections", "Spin Directions");
            DrawProperty("useFlipDirection", "Use Flip Direction");
            DrawProperty("flipDirections", "Flip Directions");
            DrawProperty("useYawBuckets", "Use Yaw Buckets");
            DrawProperty("yawBuckets", "Yaw Buckets");
            DrawProperty("usePitchBuckets", "Use Pitch Buckets");
            DrawProperty("pitchBuckets", "Pitch Buckets");
            DrawProperty("useRollBuckets", "Use Roll Buckets");
            DrawProperty("rollBuckets", "Roll Buckets");
            DrawProperty("useTotalSpeedBuckets", "Use Total Speed Buckets");
            DrawProperty("totalSpeedBuckets", "Total Speed Buckets");
        });

        DrawSection("Exclusions", "Plan.Exclusions", () =>
        {
            EditorGUILayout.LabelField("Use exclusions for illegal or intentionally unsupported combinations so they do not show up as gaps.", EditorStyles.wordWrappedMiniLabel);
            DrawProperty("exclusions", "Exclusions");
        });

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string title, string helpKey, System.Action drawBody)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (TrickPoseEditorHelpState.ShowInlineHelp)
            EditorGUILayout.HelpBox(TrickPoseEditorHelp.GetTooltip(helpKey), MessageType.None);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            drawBody();
    }

    private void DrawProperty(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label, TrickPoseEditorHelp.GetTooltip($"Plan.{label.Replace(" ", string.Empty)}")));
    }
}
