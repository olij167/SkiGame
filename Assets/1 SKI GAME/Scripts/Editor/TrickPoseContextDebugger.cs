using UnityEditor;
using UnityEngine;

public static class TrickPoseContextDebugger
{
    public static void Draw(SkiController controller)
    {
        if (controller == null)
        {
            EditorGUILayout.HelpBox("Assign a preview SkiController to inspect live trick context.", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Pose Family", controller.CurrentPoseFamily.ToString());
            EditorGUILayout.TextField("Pose Shape", controller.CurrentPoseShape.ToString());
            EditorGUILayout.TextField("Orientation", FormatOrientation(controller.CurrentPoseOrientationModifier));
            EditorGUILayout.TextField("Pose Name", controller.CurrentPoseName);
            EditorGUILayout.TextField("Tracked Label", controller.CurrentTrackedPoseName);
            EditorGUILayout.Toggle("Pose Button Held", controller.IsPoseButtonHeld);
            EditorGUILayout.Toggle("Airborne", controller.IsAuthoredPoseAirborne);
            EditorGUILayout.FloatField("Yaw Angular Vel", controller.CurrentYawAngularVelocity);
            EditorGUILayout.FloatField("Pitch Angular Vel", controller.CurrentPitchAngularVelocity);
            EditorGUILayout.FloatField("Roll Angular Vel", controller.CurrentRollAngularVelocity);
            EditorGUILayout.FloatField("Total Angular Speed", controller.CurrentTotalAngularSpeed);
            EditorGUILayout.IntField("Spin Direction", controller.CurrentSpinDirectionSign);
            EditorGUILayout.IntField("Flip Direction", controller.CurrentFlipDirectionSign);
            EditorGUILayout.TextField("Active Entry", controller.ActiveTrickPoseEntry != null ? controller.ActiveTrickPoseEntry.GetSummary() : "(none)");
            EditorGUILayout.FloatField("Authored Blend", controller.ActiveTrickPoseBlend);
        }

        TrickPoseEditorPreviewContext simulated = TrickPoseEditorSession.GetScenePreviewContext(controller);
        if (simulated == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Simulated Preview", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Source", simulated.source.ToString());
            EditorGUILayout.Toggle("Pose Input", simulated.poseInputHeld);
            EditorGUILayout.Toggle("Tuck Input", simulated.tuckInput);
            EditorGUILayout.Toggle("Left Input", simulated.leftInput);
            EditorGUILayout.Toggle("Right Input", simulated.rightInput);
            EditorGUILayout.Toggle("Airborne", simulated.airborne);
            EditorGUILayout.Toggle("Grounded", simulated.grounded);
            EditorGUILayout.Toggle("Rising", simulated.rising);
            EditorGUILayout.Toggle("Diving", simulated.diving);
            EditorGUILayout.TextField("Pose Family", simulated.poseFamily.ToString());
            EditorGUILayout.TextField("Pose Shape", simulated.poseShape.ToString());
            EditorGUILayout.TextField("Orientation", FormatOrientation(simulated.orientationModifier));
            EditorGUILayout.TextField("Pose Name", string.IsNullOrWhiteSpace(simulated.poseName) ? "(none)" : simulated.poseName);
            EditorGUILayout.FloatField("Yaw Angular Vel", simulated.yawAngularVelocity);
            EditorGUILayout.FloatField("Pitch Angular Vel", simulated.pitchAngularVelocity);
            EditorGUILayout.FloatField("Roll Angular Vel", simulated.rollAngularVelocity);
            EditorGUILayout.FloatField("Total Angular Speed", simulated.totalAngularSpeed);
            EditorGUILayout.IntField("Spin Direction", simulated.spinDirectionSign);
            EditorGUILayout.IntField("Flip Direction", simulated.flipDirectionSign);
            EditorGUILayout.TextField("Matched Authored Entry", TrickPoseEditorSession.ActiveSimulatedMatchedEntry != null ? TrickPoseEditorSession.ActiveSimulatedMatchedEntry.GetSummary() : "(none)");
        }

        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        if (profile != null)
        {
            TrickPoseCoverageContextEvaluation evaluation = TrickPoseCoverageAnalyzer.EvaluateContext(profile, simulated, 3);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Coverage Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Classification", evaluation.classification.ToString());
            EditorGUILayout.LabelField("Summary", evaluation.Summary, EditorStyles.wordWrappedLabel);

            for (int i = 0; i < evaluation.nearestEntries.Count; i++)
            {
                TrickPoseCoverageEntryDiagnostic diagnostic = evaluation.nearestEntries[i];
                EditorGUILayout.LabelField($"{i + 1}. {diagnostic.Summary}", EditorStyles.boldLabel);
                for (int reasonIndex = 0; reasonIndex < diagnostic.failReasons.Count; reasonIndex++)
                    EditorGUILayout.LabelField($"- {diagnostic.failReasons[reasonIndex]}", EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private static string FormatOrientation(SkiController.AerialOrientationModifier orientation)
    {
        string label = SkiController.GetAerialOrientationModifierLabel(orientation);
        return string.IsNullOrWhiteSpace(label) ? "None" : label;
    }
}
