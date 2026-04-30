using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TrickPoseEditorSession
{
    private const string SceneEditModeKey = "TrickPose.SceneEditMode";
    private const string SnapPreviewKey = "TrickPose.SnapPreview";
    private const double SceneRepaintMinIntervalSeconds = 1.0d / 30.0d;

    static TrickPoseEditorSession()
    {
        AssemblyReloadEvents.beforeAssemblyReload += RestoreActivePreviewTarget;
        EditorApplication.quitting += RestoreActivePreviewTarget;
        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    public enum EditablePoint
    {
        Body = 0,
        Head = 1,
        LeftSki = 2,
        RightSki = 3,
        LeftPole = 4,
        RightPole = 5,
        LeftElbow = 6,
        RightElbow = 7,
        LeftKnee = 8,
        RightKnee = 9
    }

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

    public enum MatchPreviewContextMode
    {
        CurrentSceneState = 0,
        SimulateSelectedEntry = 1
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
    public static bool SceneEditMode
    {
        get => SessionState.GetBool(SceneEditModeKey, false);
        set
        {
            bool previous = SceneEditMode;
            SessionState.SetBool(SceneEditModeKey, value);
            if (value && !previous)
                InvalidateSelectedEntryPreview();
        }
    }

    public static bool SnapPreview
    {
        get => SessionState.GetBool(SnapPreviewKey, true);
        set => SessionState.SetBool(SnapPreviewKey, value);
    }
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
        set
        {
            bool wasEnabled = PreviewMatchConditions;
            SessionState.SetBool("TrickPose.PreviewMatchConditions", value);
            if (value && !wasEnabled)
                SelectedMatchPreviewMode = MatchPreviewContextMode.SimulateSelectedEntry;

            InvalidateSelectedEntryPreview();
        }
    }

    public static bool ShowSceneGizmos
    {
        get => SessionState.GetBool("TrickPose.ShowSceneGizmos", true);
        set => SessionState.SetBool("TrickPose.ShowSceneGizmos", value);
    }

    public static bool ShowLegacyAdvancedGates
    {
        get => SessionState.GetBool("TrickPose.ShowLegacyAdvancedGates", false);
        set => SessionState.SetBool("TrickPose.ShowLegacyAdvancedGates", value);
    }

    public static MatchPreviewContextMode SelectedMatchPreviewMode
    {
        get => (MatchPreviewContextMode)SessionState.GetInt("TrickPose.SelectedMatchPreviewMode", (int)MatchPreviewContextMode.CurrentSceneState);
        set
        {
            SessionState.SetInt("TrickPose.SelectedMatchPreviewMode", (int)value);
            InvalidateSelectedEntryPreview();
        }
    }

    public static bool SimulateMatchConditionsDuringPreview
    {
        get => SessionState.GetBool("TrickPose.SimulateMatchConditionsDuringPreview", false);
        set => SessionState.SetBool("TrickPose.SimulateMatchConditionsDuringPreview", value);
    }

    public static EditablePoint ActiveEditablePoint
    {
        get => (EditablePoint)SessionState.GetInt("TrickPose.ActiveEditablePoint", (int)EditablePoint.Body);
        set => SessionState.SetInt("TrickPose.ActiveEditablePoint", (int)value);
    }

    public static TrickPoseEntry PreviewWindowSimulatedEntry { get; private set; }
    public static TrickPoseEditorPreviewContext ActiveSimulatedContext { get; private set; }
    public static TrickPoseEntry ActiveSimulatedMatchedEntry { get; private set; }
    public static TrickPoseCoveragePlanSO ActiveCoveragePlan { get; private set; }
    public static List<TrickPoseCoverageSlot> CoverageSlots { get; } = new List<TrickPoseCoverageSlot>();
    public static int SelectedCoverageSlotIndex { get; private set; } = -1;

    private static PreviewPresentationState _presentationState;
    private static int _selectedEntryPreviewVersion;
    private static int _appliedSceneEditPreviewVersion = -1;
    private static bool _sceneRepaintQueued;
    private static double _lastSceneRepaintTime;
    private static bool _isSceneHandleEditing;
    private static bool _refreshPreviewAfterSceneHandleEdit;

    public static bool IsSceneHandleEditing => _isSceneHandleEditing;

    private static void RestoreActivePreviewTarget()
    {
        RestorePresentation();
        if (PreviewTarget != null)
            PreviewTarget.RestoreTrueDefaultPose(true);

        ClearPreviewWindowSimulatedEntry();
        ClearActiveSimulatedContext();
    }

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

    public static TrickPoseCoverageSlot SelectedCoverageSlot
    {
        get
        {
            if (SelectedCoverageSlotIndex < 0 || SelectedCoverageSlotIndex >= CoverageSlots.Count)
                return null;

            return CoverageSlots[SelectedCoverageSlotIndex];
        }
    }

    public static void SetProfile(TrickPoseProfileSO profile)
    {
        bool changed = ActiveProfile != profile;
        int previousSelectedEntryIndex = SelectedEntryIndex;
        ActiveProfile = profile;
        if (profile == null)
            SelectedEntryIndex = -1;
        else if (SelectedEntryIndex >= profile.entries.Count)
            SelectedEntryIndex = profile.entries.Count - 1;

        if (changed || previousSelectedEntryIndex != SelectedEntryIndex)
            InvalidateSelectedEntryPreview();
    }

    public static void SetSelectedEntry(TrickPoseProfileSO profile, int index)
    {
        SetProfile(profile);
        if (SelectedEntryIndex == index)
            return;

        SelectedEntryIndex = index;
        InvalidateSelectedEntryPreview();
    }

    public static void SetCoverageWorkspace(TrickPoseCoveragePlanSO plan, List<TrickPoseCoverageSlot> slots)
    {
        ActiveCoveragePlan = plan;
        CoverageSlots.Clear();
        if (slots != null)
            CoverageSlots.AddRange(slots);

        if (CoverageSlots.Count == 0)
            SelectedCoverageSlotIndex = -1;
        else
            SelectedCoverageSlotIndex = Mathf.Clamp(SelectedCoverageSlotIndex, 0, CoverageSlots.Count - 1);
    }

    public static void SetSelectedCoverageSlot(int index)
    {
        SelectedCoverageSlotIndex = index >= 0 && index < CoverageSlots.Count ? index : -1;
    }

    public static void RefreshCoverageWorkspace(int nearestEntryCount)
    {
        if (CoverageSlots.Count == 0)
            return;

        TrickPoseCoveragePlanBuilder.RefreshSlotAssignments(ActiveProfile, CoverageSlots, nearestEntryCount);
    }

    public static void SetPreviewTarget(SkiController controller)
    {
        RestorePresentation();
        PreviewTarget?.RestoreTrueDefaultPose(true);
        PreviewTarget = controller;
        if (PreviewTarget != null)
        {
            PreviewTarget.EnsurePoseRigDefaultsCaptured();
            PreviewTarget.EnsureTrueDefaultRigSnapshotCaptured();
            RefreshLimbLineVisual(PreviewTarget);
        }

        InvalidateSelectedEntryPreview();
    }

    public static void RefreshPreview(bool snap)
    {
        if (PreviewTarget == null)
            return;

        if (_isSceneHandleEditing)
        {
            _refreshPreviewAfterSceneHandleEdit = true;
            return;
        }

        InvalidateSelectedEntryPreview();
        TrickPosePreviewWindow.InterruptManualPreview();
        ApplySelectedEntryPreviewNow(snap);
        TrickPosePreviewWindow.RepaintOpenWindow();
    }

    public static void RefreshSelectedEntryMatchPreview(bool snap)
    {
        if (!PreviewMatchConditions || SelectedEntry == null)
            return;

        SelectedMatchPreviewMode = MatchPreviewContextMode.SimulateSelectedEntry;
        RefreshPreview(snap);
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
            snapshot = controller.CaptureTrueDefaultRigSnapshot();

        TrickPoseRigSnapshot adjusted = TrickPoseEditorPreviewUtility.ApplySyntheticPosture(snapshot, context);
        TrickPoseRigSnapshot solved = controller.ResolveRigAssistSnapshot(adjusted, previewSolve: true, includeLimbCorrection: true);
        controller.ApplyRigSnapshot(solved, true, 1f);
        ApplyPresentationRotation(
            controller,
            context,
            useDynamicOverride
                ? dynamicAngles
                : context != null ? TrickPoseEditorPreviewUtility.GetInstantDynamicAngles(context) : Vector3.zero);
        RefreshLimbLineVisual(controller);
        RequestSceneRepaint();
    }

    public static void ApplyPreviewSnapshotWithoutPresentationRotation(SkiController controller, TrickPoseRigSnapshot snapshot, TrickPoseEditorPreviewContext context)
    {
        if (controller == null)
            return;

        if (snapshot == null)
            snapshot = controller.CaptureTrueDefaultRigSnapshot();

        TrickPoseRigSnapshot adjusted = TrickPoseEditorPreviewUtility.ApplySyntheticPosture(snapshot, context);
        TrickPoseRigSnapshot solved = controller.ResolveRigAssistSnapshot(adjusted, previewSolve: true, includeLimbCorrection: true);
        RestorePresentation();
        controller.ApplyRigSnapshot(solved, true, 1f);
        RefreshLimbLineVisual(controller);
        RequestSceneRepaint();
    }

    public static void InvalidateSelectedEntryPreview()
    {
        _selectedEntryPreviewVersion++;
        _appliedSceneEditPreviewVersion = -1;
    }

    public static void EnsureSceneEditPreviewCurrent()
    {
        if (!SceneEditMode || PreviewTarget == null)
            return;

        if (_isSceneHandleEditing)
            return;

        if (_appliedSceneEditPreviewVersion == _selectedEntryPreviewVersion)
            return;

        ApplySelectedEntryPreviewNow(SnapPreview);
    }

    public static void BeginSceneHandleEdit()
    {
        _isSceneHandleEditing = true;
        _refreshPreviewAfterSceneHandleEdit = false;
    }

    public static void EndSceneHandleEdit(bool refreshPreview)
    {
        if (!_isSceneHandleEditing)
            return;

        bool shouldRefresh = refreshPreview || _refreshPreviewAfterSceneHandleEdit;
        _isSceneHandleEditing = false;
        _refreshPreviewAfterSceneHandleEdit = false;

        if (!shouldRefresh)
            return;

        InvalidateSelectedEntryPreview();
        RefreshPreview(SnapPreview);
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
    }

    public static void ClearPreviewWindowSimulatedEntry()
    {
        PreviewWindowSimulatedEntry = null;
    }

    public static void SetActiveSimulatedContext(TrickPoseEditorPreviewContext context, TrickPoseEntry matchedEntry = null)
    {
        ActiveSimulatedContext = context;
        ActiveSimulatedMatchedEntry = matchedEntry;
    }

    public static void ClearActiveSimulatedContext()
    {
        ActiveSimulatedContext = null;
        ActiveSimulatedMatchedEntry = null;
    }

    public static TrickPoseEditorPreviewContext GetSelectedMatchPreviewContext(SkiController controller)
    {
        if (!PreviewMatchConditions || SelectedEntry == null)
            return null;

        bool simulateSelectedEntry = SelectedMatchPreviewMode == MatchPreviewContextMode.SimulateSelectedEntry;
        if (!simulateSelectedEntry)
            RestorePresentation();

        return TrickPoseEditorPreviewUtility.BuildFromEntry(controller, SelectedEntry, TrickPosePreviewContextSource.MatchPreview, simulateSelectedEntry);
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

    public static bool IsPairedPoint(EditablePoint point)
    {
        return point == EditablePoint.LeftSki ||
               point == EditablePoint.RightSki ||
               point == EditablePoint.LeftPole ||
               point == EditablePoint.RightPole ||
               point == EditablePoint.LeftElbow ||
               point == EditablePoint.RightElbow ||
               point == EditablePoint.LeftKnee ||
               point == EditablePoint.RightKnee;
    }

    public static EditablePoint GetMirroredPoint(EditablePoint point)
    {
        return point switch
        {
            EditablePoint.LeftSki => EditablePoint.RightSki,
            EditablePoint.RightSki => EditablePoint.LeftSki,
            EditablePoint.LeftPole => EditablePoint.RightPole,
            EditablePoint.RightPole => EditablePoint.LeftPole,
            EditablePoint.LeftElbow => EditablePoint.RightElbow,
            EditablePoint.RightElbow => EditablePoint.LeftElbow,
            EditablePoint.LeftKnee => EditablePoint.RightKnee,
            EditablePoint.RightKnee => EditablePoint.LeftKnee,
            _ => point
        };
    }

    public static void RefreshLimbLineVisual(SkiController controller)
    {
        if (controller == null)
            return;

        SkierLimbLineVisual limbVisual = controller.GetComponentInChildren<SkierLimbLineVisual>(true);
        if (limbVisual != null)
            limbVisual.RefreshEditorPreview();
    }

    public static void RequestSceneRepaint()
    {
        if (_sceneRepaintQueued)
            return;

        _sceneRepaintQueued = true;
        EditorApplication.delayCall += FlushSceneRepaint;
    }

    public static void RestorePresentation()
    {
        if (_presentationState == null || _presentationState.target == null)
        {
            _presentationState = null;
            return;
        }

        _presentationState.target.transform.rotation = _presentationState.baseRotation;
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

    private static void ApplySelectedEntryPreviewNow(bool snap)
    {
        if (PreviewTarget == null)
            return;

        if (_isSceneHandleEditing)
            return;

        TrickPoseRigSnapshot snapshot = SelectedEntry != null
            ? PreviewTarget.CreateRigSnapshotFromEntry(SelectedEntry)
            : PreviewTarget.CaptureTrueDefaultRigSnapshot();

        TrickPoseEditorPreviewContext context = GetSelectedMatchPreviewContext(PreviewTarget);
        if (context != null)
            SetActiveSimulatedContext(context, SelectedEntry);

        ApplyPreviewSnapshot(PreviewTarget, snapshot, context, false, Vector3.zero);
        _appliedSceneEditPreviewVersion = _selectedEntryPreviewVersion;
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

    private static void FlushSceneRepaint()
    {
        _sceneRepaintQueued = false;
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastSceneRepaintTime < SceneRepaintMinIntervalSeconds)
            return;

        _lastSceneRepaintTime = now;
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
            sceneView.Repaint();
        else
            SceneView.RepaintAll();
    }

    private static void HandleUndoRedo()
    {
        InvalidateSelectedEntryPreview();
        TrickPosePreviewWindow.InterruptManualPreview();

        if (PreviewTarget == null)
        {
            RequestSceneRepaint();
            return;
        }

        if (PreviewMode == TrickPosePreviewMode.Coverage && SelectedCoverageSlot != null)
        {
            TrickPosePreviewWindow.PreviewCoverageSlot(SelectedCoverageSlot);
            return;
        }

        RefreshPreview(SnapPreview);
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
