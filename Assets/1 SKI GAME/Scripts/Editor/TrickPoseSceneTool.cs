using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TrickPoseSceneTool
{
    private static readonly EditableHandle[] EditableHandles = new EditableHandle[10];

    private struct EditableHandle
    {
        public TrickPoseEditorSession.EditablePoint point;
        public string label;
        public PosePartTransformData pose;
        public Transform targetTransform;
        public SkierLimbJoint joint;
        public bool isJoint;
    }

    private struct SceneEditBasis
    {
        public TrickPoseEditorPreviewContext previewContext;
        public TrickPoseRigSnapshot rawSnapshot;
        public TrickPoseRigSnapshot defaultSnapshot;
        public Vector3 visibleWorldPosition;
        public Quaternion visibleWorldRotation;
        public Vector3 visibleParentLocalPosition;
        public Quaternion visibleParentLocalRotation;
        public Vector3 visibleBodyLocalPosition;
    }

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

        DrawLimbLengthGuides(controller, TrickPoseEditorSession.SelectedEntry);

        if (!TrickPoseEditorSession.SceneEditMode)
            return;

        TrickPoseEntry entry = TrickPoseEditorSession.SelectedEntry;
        if (entry == null)
            return;

        controller.EnsurePoseRigDefaultsCaptured();
        TrickPoseEditorSession.EnsureSceneEditPreviewCurrent();

        int handleCount = BuildEditableHandles(controller, entry, EditableHandles);
        DrawSelectionMarkers(EditableHandles, handleCount);
        DrawSelectedHandles(controller, entry, EditableHandles, handleCount);
    }

    private static int BuildEditableHandles(SkiController controller, TrickPoseEntry entry, EditableHandle[] handles)
    {
        handles[0] = BuildTransformHandle(TrickPoseEditorSession.EditablePoint.Body, "Body", entry.bodyPose, controller.BodyPoseTransform);
        handles[1] = BuildTransformHandle(TrickPoseEditorSession.EditablePoint.Head, "Head", entry.headPose, controller.HeadPoseTransform);
        handles[2] = BuildTransformHandle(TrickPoseEditorSession.EditablePoint.LeftSki, "Left Ski", entry.leftSkiPose, controller.LeftSkiTransform);
        handles[3] = BuildTransformHandle(TrickPoseEditorSession.EditablePoint.RightSki, "Right Ski", entry.rightSkiPose, controller.RightSkiTransform);
        handles[4] = BuildTransformHandle(TrickPoseEditorSession.EditablePoint.LeftPole, "Left Pole", entry.leftPolePose, controller.LeftPoleContact != null ? controller.LeftPoleContact.PoleRoot : null);
        handles[5] = BuildTransformHandle(TrickPoseEditorSession.EditablePoint.RightPole, "Right Pole", entry.rightPolePose, controller.RightPoleContact != null ? controller.RightPoleContact.PoleRoot : null);
        handles[6] = BuildJointHandle(TrickPoseEditorSession.EditablePoint.LeftElbow, "Left Elbow", entry.leftElbowPose, SkierLimbJoint.LeftElbow);
        handles[7] = BuildJointHandle(TrickPoseEditorSession.EditablePoint.RightElbow, "Right Elbow", entry.rightElbowPose, SkierLimbJoint.RightElbow);
        handles[8] = BuildJointHandle(TrickPoseEditorSession.EditablePoint.LeftKnee, "Left Knee", entry.leftKneePose, SkierLimbJoint.LeftKnee);
        handles[9] = BuildJointHandle(TrickPoseEditorSession.EditablePoint.RightKnee, "Right Knee", entry.rightKneePose, SkierLimbJoint.RightKnee);
        return 10;
    }

    private static EditableHandle BuildTransformHandle(TrickPoseEditorSession.EditablePoint point, string label, PosePartTransformData pose, Transform targetTransform)
    {
        return new EditableHandle
        {
            point = point,
            label = label,
            pose = pose,
            targetTransform = targetTransform,
            joint = default,
            isJoint = false
        };
    }

    private static EditableHandle BuildJointHandle(TrickPoseEditorSession.EditablePoint point, string label, PosePartTransformData pose, SkierLimbJoint joint)
    {
        return new EditableHandle
        {
            point = point,
            label = label,
            pose = pose,
            targetTransform = null,
            joint = joint,
            isJoint = true
        };
    }

    private static void DrawSelectionMarkers(EditableHandle[] handles, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryGetHandlePose(handles[i], out Vector3 position, out Quaternion rotation))
                continue;

            bool active = handles[i].point == TrickPoseEditorSession.ActiveEditablePoint;
            float size = HandleUtility.GetHandleSize(position) * (active ? 0.11f : 0.08f);
            Handles.color = active ? new Color(1f, 0.85f, 0.25f, 0.95f) : new Color(0.9f, 0.95f, 1f, 0.8f);

            if (Handles.Button(position, rotation, size, size * 1.1f, Handles.SphereHandleCap))
            {
                TrickPoseEditorSession.ActiveEditablePoint = handles[i].point;
                TrickPoseEditorSession.RequestSceneRepaint();
            }

            Handles.Label(position + Vector3.up * size * 0.8f, active ? $"{handles[i].label} (active)" : handles[i].label);
        }
    }

    private static void DrawSelectedHandles(SkiController controller, TrickPoseEntry entry, EditableHandle[] handles, int count)
    {
        int activeIndex = FindHandleIndex(handles, count, TrickPoseEditorSession.ActiveEditablePoint);
        if (activeIndex < 0)
            return;

        DrawHandle(controller, entry, handles[activeIndex]);

        if (!TrickPoseEditorSession.LiveMirrorEnabled || !TrickPoseEditorSession.IsPairedPoint(handles[activeIndex].point))
            return;

        TrickPoseEditorSession.EditablePoint mirrored = TrickPoseEditorSession.GetMirroredPoint(handles[activeIndex].point);
        int mirroredIndex = FindHandleIndex(handles, count, mirrored);
        if (mirroredIndex >= 0)
            DrawHandle(controller, entry, handles[mirroredIndex]);
    }

    private static int FindHandleIndex(EditableHandle[] handles, int count, TrickPoseEditorSession.EditablePoint point)
    {
        for (int i = 0; i < count; i++)
        {
            if (handles[i].point == point)
                return i;
        }

        return -1;
    }

    private static void DrawHandle(SkiController controller, TrickPoseEntry entry, EditableHandle handle)
    {
        if (!TryGetHandlePose(handle, out Vector3 position, out Quaternion rotation))
            return;

        Handles.color = new Color(1f, 0.8f, 0.2f, 0.95f);
        Vector3 newPosition = position;
        Quaternion newRotation = rotation;

        if (handle.isJoint)
        {
            EditorGUI.BeginChangeCheck();
            newPosition = Handles.PositionHandle(position, rotation);
            if (!EditorGUI.EndChangeCheck())
                return;
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            if (!IsHeadPositionLocked(handle.point))
                newPosition = Handles.PositionHandle(position, rotation);
            newRotation = Handles.RotationHandle(rotation, position);
            if (!EditorGUI.EndChangeCheck())
                return;
        }

        SceneEditBasis basis = CaptureSceneEditBasis(controller, handle, position, rotation);
        bool changed = false;
        TrickPoseEditorSession.BeginSceneHandleEdit();
        try
        {
            Undo.RecordObject(TrickPoseEditorSession.ActiveProfile, $"Edit {handle.label} Trick Pose");
            string beforeSignature = JsonUtility.ToJson(entry);
            if (handle.isJoint)
                ApplyJointHandleEdit(controller, entry, handle, basis, newPosition);
            else
                ApplyTransformHandleEdit(controller, entry, handle, basis, newPosition, newRotation);

            changed = beforeSignature != JsonUtility.ToJson(entry);
            if (changed)
                EditorUtility.SetDirty(TrickPoseEditorSession.ActiveProfile);
        }
        finally
        {
            TrickPoseEditorSession.EndSceneHandleEdit(changed);
        }
    }

    private static void ApplyTransformHandleEdit(SkiController controller, TrickPoseEntry entry, EditableHandle handle, SceneEditBasis basis, Vector3 newPosition, Quaternion newRotation)
    {
        if (controller == null || entry == null || handle.targetTransform == null)
            return;

        TrickPoseRigSnapshot defaultSnapshot = basis.defaultSnapshot;
        TrickPoseRigSnapshot rawSnapshot = basis.rawSnapshot;
        if (defaultSnapshot == null || rawSnapshot == null)
            return;

        TrickPoseRigSnapshot previousSnapshot = rawSnapshot.Clone();
        TrickPoseRigEditablePoint editedPoint = ToRigEditablePoint(handle.point);
        ApplyDeltaTransformEditToSnapshot(rawSnapshot, handle, basis, newPosition, newRotation);
        ApplySolvedSceneEdit(controller, entry, rawSnapshot, previousSnapshot, defaultSnapshot, editedPoint);
        TryApplyMirrorEdit(entry, handle);
    }

    private static void ApplyJointHandleEdit(SkiController controller, TrickPoseEntry entry, EditableHandle handle, SceneEditBasis basis, Vector3 newPosition)
    {
        if (controller == null || entry == null || handle.pose == null)
            return;

        TrickPoseRigSnapshot defaultSnapshot = basis.defaultSnapshot;
        TrickPoseRigSnapshot rawSnapshot = basis.rawSnapshot;
        if (defaultSnapshot == null || rawSnapshot == null)
            return;

        TrickPoseRigSnapshot previousSnapshot = rawSnapshot.Clone();
        TrickPoseRigEditablePoint editedPoint = ToRigEditablePoint(handle.point);
        ApplyDeltaJointEditToSnapshot(rawSnapshot, handle.point, controller, basis, newPosition);
        ApplySolvedSceneEdit(controller, entry, rawSnapshot, previousSnapshot, defaultSnapshot, editedPoint);
        TryApplyMirrorEdit(entry, handle);
    }

    private static void TryApplyMirrorEdit(TrickPoseEntry entry, EditableHandle sourceHandle)
    {
        if (entry == null ||
            !TrickPoseEditorSession.LiveMirrorEnabled ||
            !TrickPoseEditorSession.IsPairedPoint(sourceHandle.point))
        {
            return;
        }

        if (!ShouldMirrorFromEditedPoint(sourceHandle.point))
            return;

        PosePartTransformData sourcePose = GetPose(entry, sourceHandle.point);
        PosePartTransformData mirroredPose = GetMirroredPose(entry, sourceHandle.point);
        if (sourcePose == null || mirroredPose == null)
            return;

        TrickPoseEditorSession.CopyPairedPose(sourcePose, mirroredPose);
    }

    private static bool ShouldMirrorFromEditedPoint(TrickPoseEditorSession.EditablePoint point)
    {
        bool sourceIsLeft = IsLeftPoint(point);
        bool mirrorLeftToRight = TrickPoseEditorSession.IsLeftToRightFromSource(sourceIsLeft);
        return mirrorLeftToRight == sourceIsLeft;
    }

    private static bool IsLeftPoint(TrickPoseEditorSession.EditablePoint point)
    {
        return point == TrickPoseEditorSession.EditablePoint.LeftSki ||
               point == TrickPoseEditorSession.EditablePoint.LeftPole ||
               point == TrickPoseEditorSession.EditablePoint.LeftElbow ||
               point == TrickPoseEditorSession.EditablePoint.LeftKnee;
    }

    private static void ApplySolvedSceneEdit(
        SkiController controller,
        TrickPoseEntry entry,
        TrickPoseRigSnapshot rawSnapshot,
        TrickPoseRigSnapshot previousSnapshot,
        TrickPoseRigSnapshot defaultSnapshot,
        TrickPoseRigEditablePoint editedPoint)
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        TrickPoseRigAssistSettings settings = profile != null ? profile.rigAssistSettings : null;
        TrickPoseRigSnapshot solvedSnapshot = rawSnapshot;
        string warning = null;
        bool includeAffectedChain = false;
        bool includeHead = editedPoint == TrickPoseRigEditablePoint.Head;
        bool includeBody = editedPoint == TrickPoseRigEditablePoint.Body;
        bool headRotationOnly = false;
        TrickPoseRigAssistUtility.ApplySolvedSceneEditBackToEntry(
            entry,
            solvedSnapshot,
            defaultSnapshot,
            editedPoint,
            includeAffectedChain,
            includeHead,
            includeBody,
            headRotationOnly);

        if (!string.IsNullOrWhiteSpace(warning))
            Debug.LogWarning(warning);
    }

    private static SceneEditBasis CaptureSceneEditBasis(SkiController controller, EditableHandle handle, Vector3 visibleWorldPosition, Quaternion visibleWorldRotation)
    {
        SceneEditBasis basis = new SceneEditBasis
        {
            previewContext = TrickPoseEditorSession.GetScenePreviewContext(controller),
            rawSnapshot = controller != null && TrickPoseEditorSession.SelectedEntry != null ? controller.CreateRigSnapshotFromEntry(TrickPoseEditorSession.SelectedEntry) : null,
            defaultSnapshot = controller != null ? controller.CaptureTrueDefaultRigSnapshot() : null,
            visibleWorldPosition = visibleWorldPosition,
            visibleWorldRotation = visibleWorldRotation,
            visibleParentLocalPosition = GetLocalPosition(handle.targetTransform, visibleWorldPosition),
            visibleParentLocalRotation = GetLocalRotation(handle.targetTransform, visibleWorldRotation),
            visibleBodyLocalPosition = controller != null && controller.BodyPoseTransform != null
                ? controller.BodyPoseTransform.InverseTransformPoint(visibleWorldPosition)
                : Vector3.zero
        };

        return basis;
    }

    private static void ApplyDeltaTransformEditToSnapshot(
        TrickPoseRigSnapshot snapshot,
        EditableHandle handle,
        SceneEditBasis basis,
        Vector3 newPosition,
        Quaternion newRotation)
    {
        Vector3 localPosition = GetLocalPosition(handle.targetTransform, newPosition);
        Quaternion localRotation = GetLocalRotation(handle.targetTransform, newRotation);
        Vector3 localPositionDelta = localPosition - basis.visibleParentLocalPosition;
        Quaternion localRotationDelta = localRotation * Quaternion.Inverse(basis.visibleParentLocalRotation);
        bool headPositionLocked = IsHeadPositionLocked(handle.point);

        switch (handle.point)
        {
            case TrickPoseEditorSession.EditablePoint.Body:
                snapshot.body.localPosition += localPositionDelta;
                snapshot.body.localRotation = localRotationDelta * snapshot.body.localRotation;
                break;
            case TrickPoseEditorSession.EditablePoint.Head:
                if (!headPositionLocked)
                    snapshot.head.localPosition += localPositionDelta;
                snapshot.head.localRotation = localRotationDelta * snapshot.head.localRotation;
                break;
            case TrickPoseEditorSession.EditablePoint.LeftSki:
                snapshot.leftSki.localPosition += localPositionDelta;
                snapshot.leftSki.localRotation = localRotationDelta * snapshot.leftSki.localRotation;
                break;
            case TrickPoseEditorSession.EditablePoint.RightSki:
                snapshot.rightSki.localPosition += localPositionDelta;
                snapshot.rightSki.localRotation = localRotationDelta * snapshot.rightSki.localRotation;
                break;
            case TrickPoseEditorSession.EditablePoint.LeftPole:
                snapshot.leftPole.localPosition += localPositionDelta;
                snapshot.leftPole.localRotation = localRotationDelta * snapshot.leftPole.localRotation;
                break;
            case TrickPoseEditorSession.EditablePoint.RightPole:
                snapshot.rightPole.localPosition += localPositionDelta;
                snapshot.rightPole.localRotation = localRotationDelta * snapshot.rightPole.localRotation;
                break;
        }
    }

    private static Vector3 GetLocalPosition(Transform target, Vector3 worldPosition)
    {
        return target != null && target.parent != null
            ? target.parent.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    private static Quaternion GetLocalRotation(Transform target, Quaternion worldRotation)
    {
        return target != null && target.parent != null
            ? Quaternion.Inverse(target.parent.rotation) * worldRotation
            : worldRotation;
    }

    private static void ApplyDeltaJointEditToSnapshot(TrickPoseRigSnapshot snapshot, TrickPoseEditorSession.EditablePoint point, SkiController controller, SceneEditBasis basis, Vector3 worldPosition)
    {
        if (snapshot == null || controller == null || controller.BodyPoseTransform == null)
            return;

        Vector3 bodyLocal = controller.BodyPoseTransform.InverseTransformPoint(worldPosition);
        Vector3 delta = bodyLocal - basis.visibleBodyLocalPosition;
        switch (point)
        {
            case TrickPoseEditorSession.EditablePoint.LeftElbow:
                snapshot.leftElbow.localPosition += delta;
                break;
            case TrickPoseEditorSession.EditablePoint.RightElbow:
                snapshot.rightElbow.localPosition += delta;
                break;
            case TrickPoseEditorSession.EditablePoint.LeftKnee:
                snapshot.leftKnee.localPosition += delta;
                break;
            case TrickPoseEditorSession.EditablePoint.RightKnee:
                snapshot.rightKnee.localPosition += delta;
                break;
        }
    }

    private static bool IsHeadPositionLocked(TrickPoseEditorSession.EditablePoint point)
    {
        return false;
    }

    private static TrickPoseRigEditablePoint ToRigEditablePoint(TrickPoseEditorSession.EditablePoint point)
    {
        return (TrickPoseRigEditablePoint)(int)point;
    }

    private static PosePartTransformData GetMirroredPose(TrickPoseEntry entry, TrickPoseEditorSession.EditablePoint point)
    {
        if (entry == null)
            return null;

        return point switch
        {
            TrickPoseEditorSession.EditablePoint.LeftSki => entry.rightSkiPose,
            TrickPoseEditorSession.EditablePoint.RightSki => entry.leftSkiPose,
            TrickPoseEditorSession.EditablePoint.LeftPole => entry.rightPolePose,
            TrickPoseEditorSession.EditablePoint.RightPole => entry.leftPolePose,
            TrickPoseEditorSession.EditablePoint.LeftElbow => entry.rightElbowPose,
            TrickPoseEditorSession.EditablePoint.RightElbow => entry.leftElbowPose,
            TrickPoseEditorSession.EditablePoint.LeftKnee => entry.rightKneePose,
            TrickPoseEditorSession.EditablePoint.RightKnee => entry.leftKneePose,
            _ => null
        };
    }

    private static PosePartTransformData GetPose(TrickPoseEntry entry, TrickPoseEditorSession.EditablePoint point)
    {
        if (entry == null)
            return null;

        return point switch
        {
            TrickPoseEditorSession.EditablePoint.LeftSki => entry.leftSkiPose,
            TrickPoseEditorSession.EditablePoint.RightSki => entry.rightSkiPose,
            TrickPoseEditorSession.EditablePoint.LeftPole => entry.leftPolePose,
            TrickPoseEditorSession.EditablePoint.RightPole => entry.rightPolePose,
            TrickPoseEditorSession.EditablePoint.LeftElbow => entry.leftElbowPose,
            TrickPoseEditorSession.EditablePoint.RightElbow => entry.rightElbowPose,
            TrickPoseEditorSession.EditablePoint.LeftKnee => entry.leftKneePose,
            TrickPoseEditorSession.EditablePoint.RightKnee => entry.rightKneePose,
            _ => null
        };
    }

    private static bool TryGetHandlePose(EditableHandle handle, out Vector3 position, out Quaternion rotation)
    {
        if (handle.isJoint)
        {
            SkierLimbLineVisual limbVisual = TrickPoseEditorSession.PreviewTarget != null
                ? TrickPoseEditorSession.PreviewTarget.GetComponentInChildren<SkierLimbLineVisual>(true)
                : null;
            if (limbVisual != null && limbVisual.TryGetJointHandlePose(handle.joint, out position, out rotation))
                return true;
        }
        else if (handle.targetTransform != null)
        {
            position = handle.targetTransform.position;
            rotation = handle.targetTransform.rotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    private static void DrawLimbLengthGuides(SkiController controller, TrickPoseEntry entry)
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        TrickPoseRigAssistSettings settings = profile != null ? profile.rigAssistSettings : null;

        if (controller == null ||
            entry == null ||
            settings == null ||
            !settings.showLimbLengthGuidesInScene ||
            !settings.restSegments.captured)
        {
            return;
        }

        TrickPoseRigSnapshot snapshot = controller.CreateRigSnapshotFromEntry(entry);
        TrickPoseRigAssistReferenceData referenceData = controller.BuildRigAssistReferenceData();

        System.Collections.Generic.List<TrickPoseRigAssistLimbGuide> guides =
            TrickPoseRigAssistUtility.BuildLimbLengthGuides(snapshot, referenceData, settings);

        if (guides.Count == 0)
            return;

        Transform rootTransform = controller.transform;

        Color guideColor = new Color(0.35f, 1f, 0.65f, 0.9f);
        Color deltaColor = new Color(1f, 0.75f, 0.2f, 0.85f);

        for (int i = 0; i < guides.Count; i++)
        {
            TrickPoseRigAssistLimbGuide guide = guides[i];
            if (!guide.valid)
                continue;

            Vector3 root = rootTransform.TransformPoint(guide.root);
            Vector3 currentJoint = rootTransform.TransformPoint(guide.currentJoint);
            Vector3 currentEndpoint = rootTransform.TransformPoint(guide.currentEndpoint);
            Vector3 guideJoint = rootTransform.TransformPoint(guide.guideJoint);
            Vector3 guideEndpoint = rootTransform.TransformPoint(guide.guideEndpoint);

            float handleSize = HandleUtility.GetHandleSize(guideJoint) * 0.055f;

            // Ideal limb guide: root -> corrected joint -> corrected endpoint.
            Handles.color = guideColor;
            Handles.DrawAAPolyLine(3f, root, guideJoint, guideEndpoint);
            Handles.SphereHandleCap(
                0,
                guideJoint,
                Quaternion.identity,
                handleSize,
                EventType.Repaint);

            Handles.CubeHandleCap(
                0,
                guideEndpoint,
                Quaternion.identity,
                handleSize * 1.15f,
                EventType.Repaint);

            // Offset guides: current authored points -> ideal-length guide points.
            Handles.color = deltaColor;
            Handles.DrawDottedLine(currentJoint, guideJoint, 4f);
            Handles.DrawDottedLine(currentEndpoint, guideEndpoint, 4f);
        }
    }

    private static void DrawPreviewGizmos(SkiController controller, TrickPoseEditorPreviewContext context)
    {
        Transform root = controller.transform;
        Vector3 origin = root.position + Vector3.up * 1.35f;
        TrickPoseEntry entry = context.sourceEntry;
        TrickPoseEntry selectedEntry = TrickPoseEditorSession.SelectedEntry;

        Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Vector3 fwd = root.forward * 1.1f;
        Handles.DrawLine(origin, origin + fwd);
        Handles.ConeHandleCap(0, origin + fwd, Quaternion.LookRotation(root.forward), 0.08f, EventType.Repaint);
        Handles.Label(origin + fwd + Vector3.up * 0.05f, "Facing");
        string gatingLabel = $"{(context.poseInputHeld ? "Pose" : "No Pose")} | {(context.airborne ? "Airborne" : "Grounded")}";
        if (context.source == TrickPosePreviewContextSource.InfluencePreview && (!context.poseInputHeld || !context.airborne))
            gatingLabel += " | Derived State Blocked";

        Handles.Label(origin - root.right * 1.15f + root.up * 0.38f, gatingLabel);

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

        DrawSelectedEntryDiagnostic(origin, root, selectedEntry, context);

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
            ? $"Required Family {required} (derived {family})"
            : entry == null ? $"Derived Family {family}" : $"Family {family}";
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
            ? $"Required Shape {required} (derived {shape})"
            : entry == null ? $"Derived Shape {shape}" : $"Shape {shape}";
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
            case SkiController.AerialOrientationModifier.OnSide:
                Handles.DrawLine(origin, origin + root.right * 0.85f);
                Handles.DrawLine(origin, origin - root.right * 0.85f);
                Handles.DrawWireDisc(origin, root.forward, 0.28f);
                break;
            case SkiController.AerialOrientationModifier.ChestDown:
                Handles.DrawLine(origin, origin + root.forward * 0.85f);
                Handles.DrawLine(origin, origin - root.up * 0.45f);
                break;
            case SkiController.AerialOrientationModifier.ChestUp:
                Handles.DrawLine(origin, origin - root.forward * 0.85f);
                Handles.DrawLine(origin, origin + root.up * 0.45f);
                break;
            case SkiController.AerialOrientationModifier.Rising:
                Handles.DrawLine(origin, origin + Vector3.up * 0.85f);
                break;
            case SkiController.AerialOrientationModifier.Diving:
                Handles.DrawLine(origin, origin + Vector3.down * 0.85f);
                break;
        }

        string requiredLabel = FormatOrientation(required);
        string modifierLabel = FormatOrientation(modifier);
        string label = required != SkiController.AerialOrientationModifier.None
            ? $"Required Orientation {requiredLabel} (derived {modifierLabel})"
            : entry == null ? $"Derived Orientation {modifierLabel}" : $"Orientation {modifierLabel}";
        Handles.Label(origin + root.up * 0.88f, label);
    }

    private static string FormatOrientation(SkiController.AerialOrientationModifier orientation)
    {
        string label = SkiController.GetAerialOrientationModifierLabel(orientation);
        return string.IsNullOrWhiteSpace(label) ? "None" : label;
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

    private static void DrawSelectedEntryDiagnostic(Vector3 origin, Transform root, TrickPoseEntry entry, TrickPoseEditorPreviewContext context)
    {
        if (entry == null || context == null)
            return;

        System.Collections.Generic.List<string> failReasons = TrickPoseEditorPreviewUtility.BuildFailReasons(entry, context, 2);
        Vector3 labelPosition = origin + root.right * 1.1f + root.up * 0.95f;
        if (failReasons.Count == 0)
        {
            Handles.color = new Color(0.45f, 1f, 0.45f, 0.9f);
            Handles.Label(labelPosition, $"Selected Entry Matches: {entry.GetSummary()}");
            return;
        }

        Handles.color = new Color(1f, 0.55f, 0.35f, 0.95f);
        Handles.Label(labelPosition, $"Selected Entry Near-Miss: {entry.GetSummary()}");
        for (int i = 0; i < failReasons.Count; i++)
            Handles.Label(labelPosition + Vector3.down * (0.18f * (i + 1)), failReasons[i]);
    }

}
