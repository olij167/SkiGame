using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TrickPoseSceneTool
{
    static TrickPoseSceneTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        TrickPoseEditorPreviewContext previewContext = TrickPoseEditorSession.GetScenePreviewContext(controller);
        if (TrickPoseEditorSession.ShowSceneGizmos && previewContext != null)
            DrawPreviewGizmos(controller, previewContext);

        if (!TrickPoseEditorSession.SceneEditMode)
            return;

        TrickPoseEntry entry = TrickPoseEditorSession.SelectedEntry;
        if (entry == null)
            return;

        controller.EnsurePoseRigDefaultsCaptured();
        controller.PreviewTrickPoseEntry(entry, true);

        DrawHandle(controller, entry.bodyPose, controller.BodyPoseTransform, "Body");
        DrawHandle(controller, entry.headPose, controller.HeadPoseTransform, "Head");
        DrawHandle(controller, entry.leftSkiPose, controller.LeftSkiTransform, "Left Ski");
        DrawHandle(controller, entry.rightSkiPose, controller.RightSkiTransform, "Right Ski");
        DrawHandle(controller, entry.leftPolePose, controller.LeftPoleContact != null ? controller.LeftPoleContact.PoleRoot : null, "Left Pole");
        DrawHandle(controller, entry.rightPolePose, controller.RightPoleContact != null ? controller.RightPoleContact.PoleRoot : null, "Right Pole");
    }

    private static void DrawHandle(SkiController controller, PosePartTransformData pose, Transform target, string label)
    {
        if (pose == null || target == null)
            return;

        Handles.Label(target.position, label);

        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(target.position, target.rotation);
        Quaternion newRotation = Handles.RotationHandle(target.rotation, target.position);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(TrickPoseEditorSession.ActiveProfile, $"Edit {label} Trick Pose");
        target.position = newPosition;
        target.rotation = newRotation;

        ApplyLiveMirrorIfNeeded(controller, target);
        controller.CaptureCurrentPoseIntoEntry(TrickPoseEditorSession.SelectedEntry);
        EditorUtility.SetDirty(TrickPoseEditorSession.ActiveProfile);
        TrickPoseEditorSession.RefreshPreview(true);
    }

    private static void ApplyLiveMirrorIfNeeded(SkiController controller, Transform source)
    {
        if (!TrickPoseEditorSession.LiveMirrorEnabled || controller == null || source == null)
            return;

        bool isLeftSki = source == controller.LeftSkiTransform;
        bool isRightSki = source == controller.RightSkiTransform;
        bool isLeftPole = controller.LeftPoleContact != null && source == controller.LeftPoleContact.PoleRoot;
        bool isRightPole = controller.RightPoleContact != null && source == controller.RightPoleContact.PoleRoot;
        if (!isLeftSki && !isRightSki && !isLeftPole && !isRightPole)
            return;

        bool sourceIsLeft = isLeftSki || isLeftPole;
        bool sourceToRight = TrickPoseEditorSession.IsLeftToRightFromSource(sourceIsLeft);

        Transform left = isLeftSki || isRightSki
            ? controller.LeftSkiTransform
            : controller.LeftPoleContact != null ? controller.LeftPoleContact.PoleRoot : null;
        Transform right = isLeftSki || isRightSki
            ? controller.RightSkiTransform
            : controller.RightPoleContact != null ? controller.RightPoleContact.PoleRoot : null;

        if (left == null || right == null)
            return;

        Transform actualSource = sourceToRight ? left : right;
        Transform actualDestination = sourceToRight ? right : left;
        if (source != actualSource)
            return;

        TrickPoseEditorSession.ApplyMirroredTransform(actualSource, actualDestination);
    }

    private static void DrawPreviewGizmos(SkiController controller, TrickPoseEditorPreviewContext context)
    {
        Transform root = controller.transform;
        Vector3 origin = root.position + Vector3.up * 1.35f;
        TrickPoseEntry entry = context.sourceEntry;

        Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Vector3 fwd = root.forward * 1.1f;
        Handles.DrawLine(origin, origin + fwd);
        Handles.ConeHandleCap(0, origin + fwd, Quaternion.LookRotation(root.forward), 0.08f, EventType.Repaint);
        Handles.Label(origin + fwd + Vector3.up * 0.05f, "Facing");
        Handles.Label(origin - root.right * 0.95f + root.up * 0.38f, $"{(context.poseInputHeld ? "Pose" : "No Pose")} | {(context.airborne ? "Airborne" : "Grounded")}");

        DrawFamilyGizmo(origin, root, context.poseFamily, entry);
        DrawShapeGizmo(origin, root, context.poseShape, entry);
        DrawOrientationGizmo(origin, root, context.orientationModifier, entry);
        DrawSpinGizmo(origin, root, context.spinDirectionSign, entry);
        DrawFlipGizmo(origin, root, context.flipDirectionSign, entry);
        DrawAngularRangeGizmo(origin, root.up, 0.55f, "Yaw", entry != null ? entry.yawAngularVelocityRange : default, context.yawAngularVelocity, new Color(0.3f, 1f, 0.9f, 0.8f));
        DrawAngularRangeGizmo(origin, root.right, 0.72f, "Pitch", entry != null ? entry.pitchAngularVelocityRange : default, context.pitchAngularVelocity, new Color(1f, 0.7f, 0.3f, 0.8f));
        DrawAngularRangeGizmo(origin, root.forward, 0.89f, "Roll", entry != null ? entry.rollAngularVelocityRange : default, context.rollAngularVelocity, new Color(1f, 0.4f, 0.7f, 0.8f));

        if (entry != null && entry.totalAngularSpeedRange.enabled)
        {
            Vector2 range = entry.totalAngularSpeedRange.GetSortedRange();
            Handles.Label(origin + root.up * 1.15f, $"Total {range.x:0.#}..{range.y:0.#} (now {context.totalAngularSpeed:0.#})");
        }
        else
        {
            Handles.Label(origin + root.up * 1.15f, $"Total {context.totalAngularSpeed:0.#}");
        }

        if (!string.IsNullOrWhiteSpace(context.poseName))
            Handles.Label(origin + root.up * 1.33f, $"Pose {context.poseName}");

        string sourceLabel = context.source switch
        {
            TrickPosePreviewContextSource.MatchPreview => "Match Preview",
            TrickPosePreviewContextSource.SequencePreview => "Sequence Preview",
            TrickPosePreviewContextSource.InfluencePreview => "Influence Preview",
            _ => "Preview"
        };
        Handles.Label(origin + root.right * 1.05f + root.up * 0.35f, sourceLabel);
    }

    private static void DrawFamilyGizmo(Vector3 origin, Transform root, SkiController.AerialPoseFamily family, TrickPoseEntry entry)
    {
        SkiController.AerialPoseFamily required = entry != null ? entry.requiredPoseFamily : SkiController.AerialPoseFamily.None;
        if (family == SkiController.AerialPoseFamily.None && required == SkiController.AerialPoseFamily.None)
            return;

        Handles.color = new Color(0.45f, 0.85f, 0.45f, 0.85f);
        string label = required != SkiController.AerialPoseFamily.None
            ? $"Family {required} (now {family})"
            : $"Family {family}";
        Handles.DrawLine(origin, origin + root.right * 0.45f);
        Handles.DrawLine(origin, origin - root.right * 0.45f);
        Handles.Label(origin + root.right * 0.5f + root.up * 0.12f, label);
    }

    private static void DrawShapeGizmo(Vector3 origin, Transform root, SkiController.AerialPoseShape shape, TrickPoseEntry entry)
    {
        SkiController.AerialPoseShape required = entry != null ? entry.requiredPoseShape : SkiController.AerialPoseShape.None;
        if (shape == SkiController.AerialPoseShape.None && required == SkiController.AerialPoseShape.None)
            return;

        Handles.color = new Color(0.7f, 1f, 0.35f, 0.85f);
        string label = required != SkiController.AerialPoseShape.None
            ? $"Shape {required} (now {shape})"
            : $"Shape {shape}";
        Handles.DrawWireDisc(origin + root.up * 0.18f, root.forward, 0.18f);
        Handles.Label(origin - root.right * 0.85f + root.up * 0.25f, label);
    }

    private static void DrawOrientationGizmo(Vector3 origin, Transform root, SkiController.AerialOrientationModifier modifier, TrickPoseEntry entry)
    {
        SkiController.AerialOrientationModifier required = entry != null ? entry.requiredOrientationModifier : SkiController.AerialOrientationModifier.None;
        if (modifier == SkiController.AerialOrientationModifier.None && required == SkiController.AerialOrientationModifier.None)
            return;

        Handles.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        SkiController.AerialOrientationModifier shown = required != SkiController.AerialOrientationModifier.None ? required : modifier;
        switch (shown)
        {
            case SkiController.AerialOrientationModifier.Switch:
                Handles.DrawLine(origin, origin - root.forward * 0.9f);
                break;
            case SkiController.AerialOrientationModifier.Sideways:
                Handles.DrawLine(origin, origin + root.right * 0.85f);
                Handles.DrawLine(origin, origin - root.right * 0.85f);
                break;
            case SkiController.AerialOrientationModifier.Inverted:
                Handles.DrawLine(origin, origin - root.up * 0.85f);
                break;
            case SkiController.AerialOrientationModifier.Rising:
                Handles.DrawLine(origin, origin + Vector3.up * 0.85f);
                break;
            case SkiController.AerialOrientationModifier.Diving:
                Handles.DrawLine(origin, origin + Vector3.down * 0.85f);
                break;
        }

        string label = required != SkiController.AerialOrientationModifier.None
            ? $"Orientation {required} (now {modifier})"
            : $"Orientation {modifier}";
        Handles.Label(origin + root.up * 0.88f, label);
    }

    private static void DrawSpinGizmo(Vector3 origin, Transform root, int current, TrickPoseEntry entry)
    {
        TrickPoseSpinDirectionRequirement required = entry != null ? entry.requiredSpinDirection : TrickPoseSpinDirectionRequirement.Any;
        if (required == TrickPoseSpinDirectionRequirement.Any && current == 0)
            return;

        Handles.color = new Color(0.9f, 0.5f, 0.1f, 0.85f);
        Handles.DrawWireArc(origin, root.up, root.forward, 300f, 0.42f);
        string label = required != TrickPoseSpinDirectionRequirement.Any
            ? $"Spin {(required == TrickPoseSpinDirectionRequirement.Clockwise ? "CW" : "CCW")} (now {SpinLabel(current)})"
            : $"Spin {SpinLabel(current)}";
        Handles.Label(origin + root.right * 0.5f, label);
    }

    private static void DrawFlipGizmo(Vector3 origin, Transform root, int current, TrickPoseEntry entry)
    {
        TrickPoseFlipDirectionRequirement required = entry != null ? entry.requiredFlipDirection : TrickPoseFlipDirectionRequirement.Any;
        if (required == TrickPoseFlipDirectionRequirement.Any && current == 0)
            return;

        Handles.color = new Color(1f, 0.8f, 0.25f, 0.85f);
        Handles.DrawWireArc(origin, root.right, root.forward, 300f, 0.58f);
        string label = required != TrickPoseFlipDirectionRequirement.Any
            ? $"Flip {(required == TrickPoseFlipDirectionRequirement.Frontflip ? "Front" : "Back")} (now {FlipLabel(current)})"
            : $"Flip {FlipLabel(current)}";
        Handles.Label(origin - root.forward * 0.55f, label);
    }

    private static void DrawAngularRangeGizmo(Vector3 origin, Vector3 axis, float radius, string label, TrickPoseAngularVelocityRange range, float value, Color color)
    {
        if (!range.enabled)
        {
            Handles.color = new Color(color.r, color.g, color.b, 0.35f);
            Handles.DrawWireDisc(origin, axis, radius);
            Handles.Label(origin + GetPerpendicular(axis) * radius, $"{label} {value:0.#}");
            return;
        }

        Handles.color = color;
        Handles.DrawWireDisc(origin, axis, radius);
        Vector2 sorted = range.GetSortedRange();
        Handles.Label(origin + GetPerpendicular(axis) * radius, $"{label} {sorted.x:0.#}..{sorted.y:0.#} (now {value:0.#})");
    }

    private static Vector3 GetPerpendicular(Vector3 axis)
    {
        Vector3 candidate = Vector3.Cross(axis.normalized, Vector3.up);
        if (candidate.sqrMagnitude < 0.0001f)
            candidate = Vector3.Cross(axis.normalized, Vector3.forward);
        return candidate.normalized;
    }

    private static string SpinLabel(int sign)
    {
        return sign > 0 ? "CW" : sign < 0 ? "CCW" : "None";
    }

    private static string FlipLabel(int sign)
    {
        return sign > 0 ? "Front" : sign < 0 ? "Back" : "None";
    }
}
