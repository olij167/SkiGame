using UnityEditor;
using UnityEngine;

public static class TrickPoseEditorSession
{
    public enum LiveMirrorMode
    {
        Exact = 0,
        Reflected = 1
    }

    public enum LiveMirrorDirection
    {
        Auto = 0,
        LeftToRight = 1,
        RightToLeft = 2
    }

    private sealed class PreviewPresentationState
    {
        public SkiController target;
        public Quaternion baseRotation;
        public Vector3 pitchAxis;
        public Vector3 rollAxis;
    }

    public static TrickPoseProfileSO ActiveProfile { get; private set; }
    public static SkiController PreviewTarget { get; private set; }
    public static int SelectedEntryIndex { get; private set; } = -1;
    public static bool SceneEditMode { get; set; }
    public static bool SnapPreview { get; set; } = true;
    public static TrickPosePreviewMode PreviewMode
    {
        get => (TrickPosePreviewMode)SessionState.GetInt("TrickPose.PreviewMode", (int)TrickPosePreviewMode.Sequence);
        set => SessionState.SetInt("TrickPose.PreviewMode", (int)value);
    }

    public static TrickPoseInfluencePreviewState InfluencePreviewState { get; } = new TrickPoseInfluencePreviewState();

    public static bool LiveMirrorEnabled
    {
        get => SessionState.GetBool("TrickPose.LiveMirrorEnabled", false);
        set => SessionState.SetBool("TrickPose.LiveMirrorEnabled", value);
    }

    public static LiveMirrorMode MirrorMode
    {
        get => (LiveMirrorMode)SessionState.GetInt("TrickPose.LiveMirrorMode", (int)LiveMirrorMode.Reflected);
        set => SessionState.SetInt("TrickPose.LiveMirrorMode", (int)value);
    }

    public static LiveMirrorDirection MirrorDirection
    {
        get => (LiveMirrorDirection)SessionState.GetInt("TrickPose.LiveMirrorDirection", (int)LiveMirrorDirection.Auto);
        set => SessionState.SetInt("TrickPose.LiveMirrorDirection", (int)value);
    }

    public static bool PreviewMatchConditions
    {
        get => SessionState.GetBool("TrickPose.PreviewMatchConditions", false);
        set => SessionState.SetBool("TrickPose.PreviewMatchConditions", value);
    }

    public static bool ShowSceneGizmos
    {
        get => SessionState.GetBool("TrickPose.ShowSceneGizmos", true);
        set => SessionState.SetBool("TrickPose.ShowSceneGizmos", value);
    }

    public static bool PreviewAsIfMatched
    {
        get => SessionState.GetBool("TrickPose.PreviewAsIfMatched", false);
        set => SessionState.SetBool("TrickPose.PreviewAsIfMatched", value);
    }

    public static bool SimulateMatchConditionsDuringPreview
    {
        get => SessionState.GetBool("TrickPose.SimulateMatchConditionsDuringPreview", false);
        set => SessionState.SetBool("TrickPose.SimulateMatchConditionsDuringPreview", value);
    }

    public static TrickPoseEntry PreviewWindowSimulatedEntry { get; private set; }
    public static TrickPoseEditorPreviewContext ActiveSimulatedContext { get; private set; }
    public static TrickPoseEntry ActiveSimulatedMatchedEntry { get; private set; }

    private static PreviewPresentationState _presentationState;

    public static TrickPoseEntry SelectedEntry
    {
        get
        {
            if (ActiveProfile == null ||
                ActiveProfile.entries == null ||
                SelectedEntryIndex < 0 ||
                SelectedEntryIndex >= ActiveProfile.entries.Count)
                return null;

            return ActiveProfile.entries[SelectedEntryIndex];
        }
    }

    public static void SetProfile(TrickPoseProfileSO profile)
    {
        ActiveProfile = profile;
        if (profile == null)
            SelectedEntryIndex = -1;
        else if (SelectedEntryIndex >= profile.entries.Count)
            SelectedEntryIndex = profile.entries.Count - 1;
    }

    public static void SetSelectedEntry(TrickPoseProfileSO profile, int index)
    {
        SetProfile(profile);
        SelectedEntryIndex = index;
    }

    public static void SetPreviewTarget(SkiController controller)
    {
        RestorePresentation();
        PreviewTarget = controller;
        if (PreviewTarget != null)
            PreviewTarget.EnsurePoseRigDefaultsCaptured();
    }

    public static void RefreshPreview(bool snap)
    {
        if (PreviewTarget == null)
            return;

        TrickPoseRigSnapshot snapshot;
        if (SelectedEntry != null)
            snapshot = PreviewTarget.CreateRigSnapshotFromEntry(SelectedEntry);
        else
            snapshot = PreviewTarget.CaptureDefaultRigSnapshot();

        TrickPoseEditorPreviewContext context = GetSelectedMatchPreviewContext(PreviewTarget);
        ApplyPreviewSnapshot(PreviewTarget, snapshot, context);
        EditorUtility.SetDirty(PreviewTarget.gameObject);
        SceneView.RepaintAll();
    }

    public static void ApplyPreviewSnapshot(SkiController controller, TrickPoseRigSnapshot snapshot, TrickPoseEditorPreviewContext context)
    {
        ApplyPreviewSnapshot(controller, snapshot, context, false, Vector3.zero);
    }

    public static void ApplyPreviewSnapshot(SkiController controller, TrickPoseRigSnapshot snapshot, TrickPoseEditorPreviewContext context, bool useDynamicOverride, Vector3 dynamicAngles)
    {
        if (controller == null)
            return;

        if (snapshot == null)
            snapshot = controller.CaptureDefaultRigSnapshot();

        TrickPoseRigSnapshot adjusted = TrickPoseEditorPreviewUtility.ApplySyntheticPosture(snapshot, context);
        controller.ApplyRigSnapshot(adjusted, true, 1f);
        ApplyPresentationRotation(
            controller,
            context,
            useDynamicOverride
                ? dynamicAngles
                : context != null ? TrickPoseEditorPreviewUtility.GetInstantDynamicAngles(context) : Vector3.zero);
        EditorUtility.SetDirty(controller.gameObject);
        SceneView.RepaintAll();
    }

    public static void ApplyPreviewSnapshotWithoutPresentationRotation(SkiController controller, TrickPoseRigSnapshot snapshot, TrickPoseEditorPreviewContext context)
    {
        if (controller == null)
            return;

        if (snapshot == null)
            snapshot = controller.CaptureDefaultRigSnapshot();

        TrickPoseRigSnapshot adjusted = TrickPoseEditorPreviewUtility.ApplySyntheticPosture(snapshot, context);
        RestorePresentation();
        controller.ApplyRigSnapshot(adjusted, true, 1f);
        EditorUtility.SetDirty(controller.gameObject);
        SceneView.RepaintAll();
    }

    public static void PreparePresentationBasis(SkiController controller)
    {
        if (controller == null)
            return;

        EnsurePresentationState(controller);
    }

    public static void SetPreviewWindowSimulatedEntry(TrickPoseEntry entry)
    {
        PreviewWindowSimulatedEntry = entry;
        SceneView.RepaintAll();
    }

    public static void ClearPreviewWindowSimulatedEntry()
    {
        PreviewWindowSimulatedEntry = null;
        SceneView.RepaintAll();
    }

    public static void SetActiveSimulatedContext(TrickPoseEditorPreviewContext context, TrickPoseEntry matchedEntry = null)
    {
        ActiveSimulatedContext = context;
        ActiveSimulatedMatchedEntry = matchedEntry;
        SceneView.RepaintAll();
    }

    public static void ClearActiveSimulatedContext()
    {
        ActiveSimulatedContext = null;
        ActiveSimulatedMatchedEntry = null;
        SceneView.RepaintAll();
    }

    public static TrickPoseEditorPreviewContext GetSelectedMatchPreviewContext(SkiController controller)
    {
        if (!PreviewMatchConditions || SelectedEntry == null)
            return null;

        return TrickPoseEditorPreviewUtility.BuildFromEntry(controller, SelectedEntry, TrickPosePreviewContextSource.MatchPreview, PreviewAsIfMatched);
    }

    public static TrickPoseEditorPreviewContext GetScenePreviewContext(SkiController controller)
    {
        if (PreviewMode == TrickPosePreviewMode.Influence && ActiveSimulatedContext != null)
            return ActiveSimulatedContext;

        TrickPoseEditorPreviewContext selectedMatch = GetSelectedMatchPreviewContext(controller);
        if (selectedMatch != null)
            return selectedMatch;

        return ActiveSimulatedContext;
    }

    public static bool IsLeftToRightFromSource(bool sourceIsLeft)
    {
        return MirrorDirection switch
        {
            LiveMirrorDirection.LeftToRight => true,
            LiveMirrorDirection.RightToLeft => false,
            _ => sourceIsLeft
        };
    }

    public static void ApplyMirroredTransform(Transform source, Transform destination)
    {
        if (source == null || destination == null)
            return;

        switch (MirrorMode)
        {
            case LiveMirrorMode.Exact:
                destination.localPosition = source.localPosition;
                destination.localRotation = source.localRotation;
                break;

            case LiveMirrorMode.Reflected:
            default:
                destination.localPosition = ReflectPosition(source.localPosition);
                destination.localEulerAngles = ReflectEuler(source.localEulerAngles);
                break;
        }
    }

    public static void CopyPairedPose(PosePartTransformData source, PosePartTransformData destination)
    {
        if (source == null || destination == null)
            return;

        destination.enabled = source.enabled;
        destination.weight = source.weight;

        if (MirrorMode == LiveMirrorMode.Exact)
        {
            destination.localPosition = source.localPosition;
            destination.localEulerAngles = source.localEulerAngles;
            return;
        }

        destination.localPosition = ReflectPosition(source.localPosition);
        destination.localEulerAngles = ReflectEuler(source.localEulerAngles);
    }

    public static void RestorePresentation()
    {
        if (_presentationState == null || _presentationState.target == null)
        {
            _presentationState = null;
            return;
        }

        _presentationState.target.transform.rotation = _presentationState.baseRotation;
        EditorUtility.SetDirty(_presentationState.target.gameObject);
        _presentationState = null;
    }

    private static void ApplyPresentationRotation(SkiController controller, TrickPoseEditorPreviewContext context, Vector3 dynamicAngles)
    {
        if (controller == null)
            return;

        if (context == null)
        {
            RestorePresentation();
            return;
        }

        EnsurePresentationState(controller);
        if (_presentationState == null)
            return;

        controller.transform.rotation = TrickPoseEditorPreviewUtility.ComposePreviewRotation(
            _presentationState.baseRotation,
            _presentationState.pitchAxis,
            _presentationState.rollAxis,
            context,
            dynamicAngles);
    }

    private static void EnsurePresentationState(SkiController controller)
    {
        if (_presentationState != null && _presentationState.target == controller)
            return;

        RestorePresentation();

        _presentationState = new PreviewPresentationState
        {
            target = controller,
            baseRotation = controller.transform.rotation,
            pitchAxis = controller.transform.right.sqrMagnitude > 0.0001f ? controller.transform.right.normalized : Vector3.right,
            rollAxis = controller.transform.forward.sqrMagnitude > 0.0001f ? controller.transform.forward.normalized : Vector3.forward
        };
    }

    private static Vector3 ReflectPosition(Vector3 position)
    {
        return new Vector3(-position.x, position.y, position.z);
    }

    private static Vector3 ReflectEuler(Vector3 euler)
    {
        return new Vector3(euler.x, -euler.y, -euler.z);
    }
}
