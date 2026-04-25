using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TrickPosePreviewWindow : EditorWindow
{
    [Serializable]
    private class SequenceStep
    {
        public int entryIndex;
        public float duration = 0.45f;
        public float transitionDuration = 0.15f;
        public bool expanded = true;
    }

    private struct SequenceEvaluation
    {
        public TrickPoseRigSnapshot snapshot;
        public int stepIndex;
        public string label;
        public TrickPoseEntry entry;
        public TrickPoseEditorPreviewContext context;
    }

    private sealed class ManualLerpState
    {
        public TrickPoseRigSnapshot fromSnapshot;
        public TrickPoseRigSnapshot toSnapshot;
        public float duration;
        public double startTime;
        public TrickPoseEditorPreviewContext context;
    }

    [SerializeField] private List<SequenceStep> steps = new List<SequenceStep>();
    [SerializeField] private bool _isLooping = true;
    [SerializeField] private float _sequenceTime;
    [SerializeField] private float _previewLerpDuration = 0.2f;
    [SerializeField] private float _playbackSpeed = 1f;
    [SerializeField] private bool _simulateMatchConditionsDuringPreview;
    [SerializeField] private Vector2 _scrollPosition;
    [SerializeField] private float _influencePlaybackSpeed = 1f;
    [SerializeField] private bool _influenceUseYaw = true;
    [SerializeField] private bool _influenceUsePitch = true;
    [SerializeField] private bool _influenceUseRoll = true;
    [SerializeField] private Vector3 _influenceManualRotation;
    private string _autoInfluenceReadout = "Auto Influence is off.";

    private bool _isPlaying;
    private bool _influenceIsPlaying;
    private double _lastEditorTime;
    private bool _isScrubbing;
    private ManualLerpState _manualLerp;
    private Vector3 _influenceAccumulatedAngles;
    private bool _editorUpdateRegistered;
    private static TrickPosePreviewWindow _instance;

    [MenuItem("Window/Ski Game/Trick Pose Preview")]
    public static void Open()
    {
        _instance = GetWindow<TrickPosePreviewWindow>("Trick Pose Preview");
    }

    public static void RepaintOpenWindow()
    {
        if (_instance != null)
            _instance.Repaint();
    }

    public static void InterruptManualPreview()
    {
        if (_instance != null)
        {
            _instance._manualLerp = null;
            _instance.UpdateEditorUpdateRegistration();
        }
    }

    public static void LerpToEntry(TrickPoseEntry entry, float duration)
    {
        if (_instance == null)
            Open();

        _instance?.StartManualLerpToEntry(entry, duration);
    }

    public static void PushInfluenceState(TrickPoseEditorPreviewContext context)
    {
        if (_instance == null)
            Open();

        _instance?.ApplyInfluenceStateFromContext(context);
    }

    public static void PreviewCoverageSlot(TrickPoseCoverageSlot slot)
    {
        if (_instance == null)
            Open();

        _instance?.ApplyCoverageSlot(slot);
    }

    private void OnEnable()
    {
        _instance = this;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        UpdateEditorUpdateRegistration();
    }

    private void OnDisable()
    {
        if (_instance == this)
            _instance = null;

        StopInfluencePlayback(restoreRotation: true, reapplyStaticPreview: false);
        TrickPoseEditorSession.RestorePresentation();
        TrickPoseEditorSession.ClearPreviewWindowSimulatedEntry();
        TrickPoseEditorSession.ClearActiveSimulatedContext();
        TrickPoseEditorSession.PreviewTarget?.RestoreTrueDefaultPose(true);
        SetEditorUpdateRegistration(false);
    }

    private void OnGUI()
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;

        DrawModeToolbar();
        DrawModeHelp();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawSectionHeader("Preview Setup");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField(TrickPoseEditorHelp.Label("Profile", "Coverage.ProfileField"), profile, typeof(TrickPoseProfileSO), false);
            EditorGUILayout.ObjectField(TrickPoseEditorHelp.Label("Preview Target", "Profile.PreviewTarget"), controller, typeof(SkiController), true);
        }

        if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Sequence)
            DrawSequencePreview(profile, controller);
        else if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Influence)
            DrawInfluencePreview(profile, controller);
        else
            DrawCoveragePreview(profile, controller);

        EditorGUILayout.EndScrollView();
    }

    private void DrawModeToolbar()
    {
        EditorGUI.BeginChangeCheck();
        TrickPosePreviewMode mode = (TrickPosePreviewMode)GUILayout.Toolbar(
            (int)TrickPoseEditorSession.PreviewMode,
            new[]
            {
                TrickPoseEditorHelp.Button("Preview.Mode.Sequence", "Sequence Preview"),
                TrickPoseEditorHelp.Button("Preview.Mode.Influence", "Influence Preview"),
                TrickPoseEditorHelp.Button("Preview.Mode.Coverage", "Coverage Preview")
            });
        if (EditorGUI.EndChangeCheck())
        {
            if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Influence)
                StopInfluencePlayback(restoreRotation: false, reapplyStaticPreview: false);

            TrickPoseEditorSession.PreviewMode = mode;
            Pause();
            _manualLerp = null;
            if (mode == TrickPosePreviewMode.Influence)
                EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
            else if (mode == TrickPosePreviewMode.Sequence)
                EvaluateAndApplyCurrentTime();
            else
                ApplyCoverageSlot(TrickPoseEditorSession.SelectedCoverageSlot);
        }
    }

    private void DrawModeHelp()
    {
        if (!TrickPoseEditorHelpState.ShowInlineHelp)
            return;

        EditorGUILayout.HelpBox(TrickPoseEditorHelp.GetPreviewModeDescription(TrickPoseEditorSession.PreviewMode), MessageType.None);

        bool shouldShowBanner = TrickPoseEditorHelpState.ShowFirstTimeBanner &&
            !TrickPoseEditorHelpState.PreviewGettingStartedDismissed &&
            (TrickPoseEditorSession.PreviewTarget == null || TrickPoseEditorSession.ActiveProfile == null);
        if (!shouldShowBanner)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Preview Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Assign a profile, assign a preview target, then choose the preview mode that matches the task: sequence for playback, influence for rule behavior, coverage for slot-first authoring.", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Dismiss", GUILayout.Width(80f)))
                TrickPoseEditorHelpState.PreviewGettingStartedDismissed = true;
        }
    }

    private void DrawCoveragePreview(TrickPoseProfileSO profile, SkiController controller)
    {
        TrickPoseCoverageSlot slot = TrickPoseEditorSession.SelectedCoverageSlot;
        DrawSectionHeader("Coverage Slot");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (TrickPoseEditorHelpState.ShowInlineHelp)
                EditorGUILayout.HelpBox("Coverage Preview shows the selected authored slot. Use it to preview the slot state, assign the selected entry, or copy slot conditions before tuning the pose in Influence Preview.", MessageType.Info);

            if (slot == null)
            {
                EditorGUILayout.LabelField("Select a slot from the coverage window to preview it here.");
                return;
            }

            EditorGUILayout.LabelField("Pose Slot State", TrickPoseAuthoredStateFormatter.Format(slot), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Status", slot.validationStatus.ToString());
            EditorGUILayout.LabelField("Assigned Entry", slot.assignedEntry != null ? slot.assignedEntry.GetSummary() : "(none)");
            EditorGUILayout.LabelField("Summary", slot.summary, EditorStyles.wordWrappedLabel);
            if (slot.candidateEntryLabels != null && slot.candidateEntryLabels.Count > 0)
                EditorGUILayout.LabelField("Candidate Matches", string.Join(", ", slot.candidateEntryLabels), EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Preview.Mode.Coverage", "Preview Slot")))
                    ApplyCoverageSlot(slot);

                GUI.enabled = profile != null && TrickPoseEditorSession.SelectedEntryIndex >= 0;
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Preview.AssignSelectedEntry", "Assign Selected Entry")))
                {
                    TrickPoseCoverageAssignmentUtility.AssignEntryToSlot(profile, slot, TrickPoseEditorSession.SelectedEntryIndex);
                    slot.representativeContext = TrickPoseCoveragePlanBuilder.BuildRepresentativeContext(controller, slot);
                    TrickPoseEditorSession.RefreshCoverageWorkspace(3);
                }
                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = TrickPoseEditorSession.SelectedEntry != null;
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Preview.CopySlotConditions", "Copy Slot Conditions Into Selected Entry")))
                {
                    Undo.RecordObject(profile, "Apply Slot Conditions");
                    TrickPoseCoverageAssignmentUtility.ApplySlotConditionsToEntry(slot, TrickPoseEditorSession.SelectedEntry);
                    EditorUtility.SetDirty(profile);
                    TrickPoseEditorSession.RefreshCoverageWorkspace(3);
                }
                GUI.enabled = true;
            }

            if (slot.representativeContext != null)
                DrawPreviewDiagnostics(profile, slot.representativeContext);
        }
    }

    private void DrawSequencePreview(TrickPoseProfileSO profile, SkiController controller)
    {
        if (TrickPoseEditorHelpState.ShowInlineHelp)
            EditorGUILayout.HelpBox("Sequence Preview is for playback intent, timing, and transition feel. It can simulate rule contexts, but it is not the same thing as exhaustively validating rule ownership.", MessageType.None);

        float totalDuration = GetTotalDuration();
        int activeStepIndex = GetActiveStepIndex(totalDuration);
        SequenceEvaluation evaluation = EvaluateSequenceAtTime(profile, controller, _sequenceTime, totalDuration);

        DrawSectionHeader("Playback Settings");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _isLooping = EditorGUILayout.Toggle("Loop Sequence", _isLooping);
            _previewLerpDuration = Mathf.Max(0.01f, EditorGUILayout.FloatField("Preview Lerp Duration", _previewLerpDuration));
            _playbackSpeed = DrawPlaybackSpeed("Playback Speed", _playbackSpeed);
            _simulateMatchConditionsDuringPreview = EditorGUILayout.Toggle("Simulate Match Conditions During Preview", _simulateMatchConditionsDuringPreview);
            TrickPoseEditorSession.SimulateMatchConditionsDuringPreview = _simulateMatchConditionsDuringPreview;
        }

        DrawSectionHeader("Playback Timeline");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Current Time", $"{_sequenceTime:0.00}s");
            EditorGUILayout.LabelField("Total Duration", $"{totalDuration:0.00}s");
            EditorGUILayout.LabelField("Active Step", string.IsNullOrWhiteSpace(evaluation.label) ? "(none)" : evaluation.label);
            DrawPlaybackBar(totalDuration);
        }

        DrawSectionHeader("Transport");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                    Play();
                if (GUILayout.Button("Pause"))
                    Pause();
                if (GUILayout.Button("Stop"))
                    StopPlayback(previewStartFrame: true);
                if (GUILayout.Button("Clear Preview"))
                    ClearPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Jump To Start"))
                    JumpToStep(0);
                if (GUILayout.Button("Prev Step"))
                    JumpToStep(activeStepIndex - 1);
                if (GUILayout.Button("Next Step"))
                    JumpToStep(activeStepIndex + 1);
                if (GUILayout.Button("Jump To End"))
                    JumpToSequenceEnd(totalDuration);
            }
        }

        DrawSectionHeader("Sequence Steps");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button("Add Step"))
                steps.Add(new SequenceStep());

            for (int i = 0; i < steps.Count; i++)
                DrawStepEditor(profile, i, steps[i], i == activeStepIndex);
        }
    }

    private void DrawInfluencePreview(TrickPoseProfileSO profile, SkiController controller)
    {
        if (TrickPoseEditorHelpState.ShowInlineHelp)
            EditorGUILayout.HelpBox("Influence Preview simulates derived pose matching from inputs and angular motion. It helps explain what the rules respond to, but it does not prove full coverage quality or intended slot ownership by itself.", MessageType.None);

        TrickPoseInfluencePreviewState state = TrickPoseEditorSession.InfluencePreviewState;
        if (state.manualRotationEuler == Vector3.zero && _influenceManualRotation != Vector3.zero)
            state.manualRotationEuler = _influenceManualRotation;

        DrawSectionHeader("Influence Controls");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            bool autoInfluence = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Auto Influence", "Preview.AutoInfluence"), state.autoInfluence);
            state.poseInputHeld = EditorGUILayout.Toggle("Pose Input", state.poseInputHeld);
            state.tuckInput = EditorGUILayout.Toggle("Tuck Input", state.tuckInput);
            state.leftInput = EditorGUILayout.Toggle("Left Input", state.leftInput);
            state.rightInput = EditorGUILayout.Toggle("Right Input", state.rightInput);
            state.leanInput = EditorGUILayout.Slider(TrickPoseEditorHelp.Label("Lean Input", "Preview.LeanInput"), state.leanInput, -1f, 1f);
            bool airborne = EditorGUILayout.Toggle("Airborne", state.airborne);
            if (airborne != state.airborne)
                state.airborne = airborne;
            EditorGUILayout.Toggle("Grounded", !state.airborne);
            bool rising = EditorGUILayout.Toggle("Rising", state.rising);
            bool diving = EditorGUILayout.Toggle("Diving", state.diving);
            state.rising = rising && !diving;
            state.diving = diving && !rising;
            Vector3 previousManualRotation = state.manualRotationEuler;
            _influenceManualRotation = EditorGUILayout.Vector3Field(TrickPoseEditorHelp.Label("Manual Rotation", "Preview.ManualRotation"), state.manualRotationEuler);
            if (_influenceManualRotation != previousManualRotation)
                state.manualRotationActive = true;
            state.autoInfluence = autoInfluence;
            state.yawAngularVelocity = EditorGUILayout.FloatField("Yaw Angular Velocity", state.yawAngularVelocity);
            state.pitchAngularVelocity = EditorGUILayout.FloatField("Pitch Angular Velocity", state.pitchAngularVelocity);
            state.rollAngularVelocity = EditorGUILayout.FloatField("Roll Angular Velocity", state.rollAngularVelocity);

            if (EditorGUI.EndChangeCheck())
            {
                state.manualRotationEuler = _influenceManualRotation;
                if (state.autoInfluence)
                    ApplyAutoInfluence(profile, controller, state);

                if (_influenceIsPlaying)
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: true);
                else
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Rotation"))
                {
                    state.manualRotationEuler = _influenceManualRotation;
                    state.manualRotationActive = true;
                    if (state.autoInfluence)
                        ApplyAutoInfluence(profile, controller, state);
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
                }
                if (GUILayout.Button("Reset Manual Rotation"))
                {
                    _influenceManualRotation = Vector3.zero;
                    state.manualRotationEuler = Vector3.zero;
                    state.manualRotationActive = false;
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
                }
            }

            EditorGUILayout.HelpBox(_autoInfluenceReadout, MessageType.None);
        }

        TrickPoseEditorPreviewContext context = BuildEffectiveInfluenceContext(controller);
        List<TrickPoseEntry> matches = TrickPoseEditorPreviewUtility.EvaluateMatchingEntries(profile, context);
        TrickPoseEntry bestMatchedEntry = TrickPoseEditorPreviewUtility.SelectBestMatchingEntry(matches);

        DrawSectionHeader("Influence Playback");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _influencePlaybackSpeed = DrawPlaybackSpeed("Playback Speed", _influencePlaybackSpeed);
            using (new EditorGUILayout.HorizontalScope())
            {
                _influenceUseYaw = GUILayout.Toggle(_influenceUseYaw, "Yaw", "Button");
                _influenceUsePitch = GUILayout.Toggle(_influenceUsePitch, "Pitch", "Button");
                _influenceUseRoll = GUILayout.Toggle(_influenceUseRoll, "Roll", "Button");
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (_influenceIsPlaying)
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: true);
                else
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
            }

            EditorGUILayout.LabelField("Accumulated Rotation", $"Pitch {_influenceAccumulatedAngles.x:0.#}  |  Yaw {_influenceAccumulatedAngles.y:0.#}  |  Roll {_influenceAccumulatedAngles.z:0.#}");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                    StartInfluencePlayback();
                if (GUILayout.Button("Pause"))
                    PauseInfluencePlayback();
                if (GUILayout.Button("Stop"))
                    StopInfluencePlayback(restoreRotation: false, reapplyStaticPreview: false);
                if (GUILayout.Button("Reset Rotation"))
                    ResetInfluenceRotation();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
        if (GUILayout.Button("Apply Influence Preview"))
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: _influenceIsPlaying);
                if (GUILayout.Button("Clear Preview"))
                    ClearPreview();
            }
        }

        DrawSectionHeader("Derived Authored State");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool blocked = context == null || !context.airborne || !context.poseInputHeld;
            DrawDerivedInfluenceHint(context);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "Pose Slot State",
                    context != null ? FormatDerivedValueBlockedAware(blocked, TrickPoseAuthoredStateFormatter.Format(context)) : "(none)");

                EditorGUILayout.TextField(
                    "Pose Family",
                    context != null ? FormatDerivedValueBlockedAware(blocked, context.poseFamily.ToString()) : "(none)");

                EditorGUILayout.TextField(
                    "Pose Shape",
                    context != null ? FormatDerivedValueBlockedAware(blocked, FormatPoseShape(context.poseShape)) : "(none)");

                EditorGUILayout.TextField(
                    "Vertical Orientation",
                    context != null ? FormatDerivedValueBlockedAware(blocked, context.verticalOrientation.ToString()) : "(none)");

                EditorGUILayout.TextField(
                    "Horizontal Orientation",
                    context != null ? FormatDerivedValueBlockedAware(blocked, context.horizontalOrientation.ToString()) : "(none)");

                EditorGUILayout.TextField(
                    "Motion State",
                    context != null ? FormatDerivedValueBlockedAware(blocked, context.motionState.ToString()) : "(none)");

                EditorGUILayout.FloatField("Lean Input", context != null ? context.leanInput : 0f);
                EditorGUILayout.Vector3Field("Entry Rotation", context != null ? context.entryEulerAngles : Vector3.zero);
                EditorGUILayout.TextField("Spin Direction", context != null ? SpinLabel(context.spinDirectionSign) : "None");
                EditorGUILayout.TextField("Flip Direction", context != null ? FlipLabel(context.flipDirectionSign) : "None");
                EditorGUILayout.FloatField("Total Angular Speed", context != null ? context.totalAngularSpeed : 0f);
                EditorGUILayout.TextField("Derived Pose Name", context != null && !string.IsNullOrWhiteSpace(context.poseName) ? context.poseName : "(none)");
            }
        }

        DrawSectionHeader("Matched Entries");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Best Match", bestMatchedEntry != null ? bestMatchedEntry.GetSummary() : "(none)");
            if (matches.Count > 1)
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    if (matches[i] == bestMatchedEntry)
                        continue;
                    EditorGUILayout.LabelField($"Also Matches {i}", matches[i].GetSummary());
                }
            }
        }

        DrawPreviewDiagnostics(profile, context);

        TrickPoseEntry selected = TrickPoseEditorSession.SelectedEntry;
        if (selected != null && context != null)
        {
            List<TrickPoseEntryConditionStatus> statuses = TrickPoseEditorPreviewUtility.BuildConditionStatuses(
                selected,
                context,
                TrickPoseEditorSession.ShowLegacyAdvancedGates);
            DrawSectionHeader("Selected Entry Conditions");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Selected Entry", selected.GetSummary());
                for (int i = 0; i < statuses.Count; i++)
                    EditorGUILayout.LabelField(statuses[i].label, statuses[i].status);

                List<string> failReasons = TrickPoseEditorPreviewUtility.BuildFailReasons(selected, context, 6);
                if (failReasons.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Near-Miss Summary", EditorStyles.boldLabel);
                    for (int i = 0; i < failReasons.Count; i++)
                        EditorGUILayout.LabelField($"- {failReasons[i]}", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }

    private void OnEditorUpdate()
    {
        if (!NeedsEditorTick())
        {
            SetEditorUpdateRegistration(false);
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(now - _lastEditorTime);
        _lastEditorTime = now;

        if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Sequence && _isPlaying)
            AdvancePlayback(deltaTime);

        if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Influence && _influenceIsPlaying)
            AdvanceInfluencePlayback(deltaTime);

        if (_manualLerp != null)
            UpdateManualLerp(now);
    }

    private void DrawSectionHeader(string title)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void DrawStepEditor(TrickPoseProfileSO profile, int index, SequenceStep step, bool active)
    {
        TrickPoseEntry entry = ResolveEntry(profile, step.entryIndex);
        string entryName = entry != null ? entry.GetSummary() : "(none)";
        string header = $"{index + 1}. {entryName}  |  Dur {step.duration:0.##}  |  Xfade {step.transitionDuration:0.##}";

        Color previous = GUI.backgroundColor;
        if (active)
            GUI.backgroundColor = new Color(0.9f, 0.95f, 0.6f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUI.backgroundColor = previous;
            step.expanded = EditorGUILayout.Foldout(step.expanded, header, true);
            if (!step.expanded)
                return;

            step.entryIndex = DrawEntryPopup("Entry", profile, step.entryIndex);
            step.duration = Mathf.Max(0.05f, EditorGUILayout.FloatField("Duration", step.duration));
            step.transitionDuration = Mathf.Clamp(EditorGUILayout.FloatField("Transition", step.transitionDuration), 0f, step.duration);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Move Up"))
                {
                    MoveStep(index, -1);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Move Down"))
                {
                    MoveStep(index, 1);
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Snap"))
                    SnapToStep(index);
                if (GUILayout.Button("Lerp"))
                    LerpToStep(index);
                if (GUILayout.Button("Remove"))
                {
                    steps.RemoveAt(index);
                    EvaluateAndApplyCurrentTime();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private void MoveStep(int index, int direction)
    {
        int nextIndex = Mathf.Clamp(index + direction, 0, steps.Count - 1);
        if (nextIndex == index)
            return;

        SequenceStep moved = steps[index];
        steps.RemoveAt(index);
        steps.Insert(nextIndex, moved);
        EvaluateAndApplyCurrentTime();
    }

    private void AdvancePlayback(float deltaTime)
    {
        float totalDuration = GetTotalDuration();
        if (totalDuration <= 0f)
            return;

        _sequenceTime += deltaTime * _playbackSpeed;
        if (_isLooping)
        {
            while (_sequenceTime >= totalDuration)
                _sequenceTime -= totalDuration;
        }
        else if (_sequenceTime >= totalDuration)
        {
            _sequenceTime = totalDuration;
            _isPlaying = false;
        }

        EvaluateAndApplyCurrentTime();
    }

    private void AdvanceInfluencePlayback(float deltaTime)
    {
        TrickPoseInfluencePreviewState state = BuildEffectiveInfluenceState();
        _influenceAccumulatedAngles += new Vector3(
            state.pitchAngularVelocity * deltaTime * _influencePlaybackSpeed,
            state.yawAngularVelocity * deltaTime * _influencePlaybackSpeed,
            state.rollAngularVelocity * deltaTime * _influencePlaybackSpeed);

        EvaluateAndApplyInfluencePreview(useAccumulatedAngles: true);
    }

    private void UpdateManualLerp(double now)
    {
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null || _manualLerp == null)
        {
            _manualLerp = null;
            return;
        }

        float t = _manualLerp.duration <= 0.0001f
            ? 1f
            : Mathf.Clamp01((float)((now - _manualLerp.startTime) / _manualLerp.duration));

        TrickPoseRigSnapshot snapshot = TrickPoseRigSnapshot.Lerp(_manualLerp.fromSnapshot, _manualLerp.toSnapshot, t);
        TrickPoseEditorSession.ApplyPreviewSnapshot(controller, snapshot, _manualLerp.context);
        Repaint();

        if (t >= 1f)
            _manualLerp = null;
    }

    private void DrawPlaybackBar(float totalDuration)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            Rect sliderRect = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
            EditorGUI.BeginChangeCheck();
            float newTime = GUI.HorizontalSlider(sliderRect, _sequenceTime, 0f, Mathf.Max(0.001f, totalDuration));
            if (EditorGUI.EndChangeCheck())
            {
                _isScrubbing = true;
                Pause();
                _manualLerp = null;
                _sequenceTime = Mathf.Clamp(newTime, 0f, totalDuration);
                EvaluateAndApplyCurrentTime();
            }

            Rect labelsRect = GUILayoutUtility.GetRect(10f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            EditorGUI.LabelField(new Rect(labelsRect.x, labelsRect.y, 120f, labelsRect.height), $"{_sequenceTime:0.00}s");
            EditorGUI.LabelField(new Rect(labelsRect.xMax - 120f, labelsRect.y, 120f, labelsRect.height), $"{totalDuration:0.00}s", EditorStyles.miniLabel);

            if (_isScrubbing && Event.current != null && Event.current.rawType == EventType.MouseUp)
                _isScrubbing = false;
        }
    }

    private void EvaluateAndApplyCurrentTime()
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (profile == null || controller == null || steps.Count == 0)
        {
            TrickPoseEditorSession.RestorePresentation();
            TrickPoseEditorSession.ClearPreviewWindowSimulatedEntry();
            TrickPoseEditorSession.ClearActiveSimulatedContext();
            controller?.RestoreTrueDefaultPose(true);
            return;
        }

        float totalDuration = GetTotalDuration();
        SequenceEvaluation evaluation = EvaluateSequenceAtTime(profile, controller, _sequenceTime, totalDuration);
        ApplySequenceSimulation(evaluation);
    }

    private void EvaluateAndApplyInfluencePreview(bool useAccumulatedAngles)
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        TrickPoseEditorPreviewContext context = BuildEffectiveInfluenceContext(controller);
        TrickPoseEntry matched = TrickPoseEditorPreviewUtility.EvaluateBestMatchingEntry(profile, context);
        TrickPoseRigSnapshot snapshot = matched != null
            ? controller.CreateRigSnapshotFromEntry(matched)
            : controller.CaptureTrueDefaultRigSnapshot();

        TrickPoseEditorSession.SetPreviewWindowSimulatedEntry(matched);
        TrickPoseEditorSession.SetActiveSimulatedContext(context, matched);
        TrickPoseEditorSession.ApplyPreviewSnapshot(
            controller,
            snapshot,
            context,
            useAccumulatedAngles,
            useAccumulatedAngles ? _influenceAccumulatedAngles : Vector3.zero);
        Repaint();
    }

    private TrickPoseEditorPreviewContext BuildEffectiveInfluenceContext(SkiController controller)
    {
        return TrickPoseEditorPreviewUtility.BuildFromInfluences(controller, BuildEffectiveInfluenceState());
    }

    private TrickPoseInfluencePreviewState BuildEffectiveInfluenceState()
    {
        TrickPoseInfluencePreviewState source = TrickPoseEditorSession.InfluencePreviewState;
        return new TrickPoseInfluencePreviewState
        {
            autoInfluence = source.autoInfluence,
            poseInputHeld = source.poseInputHeld,
            tuckInput = source.tuckInput,
            leftInput = source.leftInput,
            rightInput = source.rightInput,
            airborne = source.airborne,
            rising = source.rising,
            diving = source.diving,
            leanInput = source.leanInput,
            manualRotationActive = source.manualRotationActive,
            manualRotationEuler = source.manualRotationEuler,
            yawAngularVelocity = _influenceUseYaw ? source.yawAngularVelocity : 0f,
            pitchAngularVelocity = _influenceUsePitch ? source.pitchAngularVelocity : 0f,
            rollAngularVelocity = _influenceUseRoll ? source.rollAngularVelocity : 0f
        };
    }

    private void ApplyAutoInfluence(TrickPoseProfileSO profile, SkiController controller, TrickPoseInfluencePreviewState state)
    {
        TrickPoseEditorPreviewContext context = TrickPoseEditorPreviewUtility.BuildFromInfluences(controller, state);
        TrickPoseCoverageContextEvaluation evaluation = TrickPoseCoverageAnalyzer.EvaluateContext(profile, context, 6);
        TrickPoseEntry closest = evaluation.bestEntry;
        if (closest == null && evaluation.nearestEntries.Count > 0)
            closest = evaluation.nearestEntries[0].entry;

        if (closest == null)
        {
            _autoInfluenceReadout = "Auto Influence: no entries available to approximate.";
            return;
        }

        ApplyEntryConditionHintsToInfluenceState(closest, state);
        List<string> reasons = TrickPoseEditorPreviewUtility.BuildFailReasons(closest, context, 3);
        string why = reasons.Count > 0 ? string.Join(" | ", reasons) : "current state already matches";
        _autoInfluenceReadout = $"Auto Influence closest entry: {closest.GetSummary()} ({why}). Angular velocity values were left unchanged.";
    }

    private void ApplyEntryConditionHintsToInfluenceState(TrickPoseEntry entry, TrickPoseInfluencePreviewState state)
    {
        if (entry == null || state == null)
            return;

        state.airborne = entry.requireAirborne != TrickPoseBoolRequirement.False;
        state.poseInputHeld = entry.requirePoseButtonHeld != TrickPoseBoolRequirement.False;
        state.leftInput = entry.requiredPoseFamily == SkiController.AerialPoseFamily.Left || entry.requiredPoseFamily == SkiController.AerialPoseFamily.Spread;
        state.rightInput = entry.requiredPoseFamily == SkiController.AerialPoseFamily.Right || entry.requiredPoseFamily == SkiController.AerialPoseFamily.Spread;
        state.tuckInput = entry.requiredPoseShape == SkiController.AerialPoseShape.Compact;
        state.leanInput = entry.requiredPoseShape switch
        {
            SkiController.AerialPoseShape.Driving => 0.65f,
            SkiController.AerialPoseShape.LaidOut => -0.65f,
            _ => state.tuckInput ? 0f : state.leanInput
        };

        if (entry.requiredPoseShape == SkiController.AerialPoseShape.Neutral)
            state.leanInput = 0f;

        state.rising = entry.requiredMotionState == TrickPoseMotionStateRequirement.Rising;
        state.diving = entry.requiredMotionState == TrickPoseMotionStateRequirement.Diving;
        if (TrickPoseOrientationUtility.TryBuildPresentationEuler(entry.requiredVerticalOrientation, entry.requiredHorizontalOrientation, out Vector3 orientationEuler))
        {
            state.manualRotationActive = true;
            state.manualRotationEuler = orientationEuler;
            _influenceManualRotation = orientationEuler;
        }
    }

    private SequenceEvaluation EvaluateSequenceAtTime(TrickPoseProfileSO profile, SkiController controller, float time, float totalDuration)
    {
        SequenceEvaluation result = new SequenceEvaluation
        {
            snapshot = controller != null ? controller.CaptureTrueDefaultRigSnapshot() : null,
            stepIndex = -1,
            label = string.Empty,
            entry = null,
            context = null
        };

        if (profile == null || controller == null || steps.Count == 0 || totalDuration <= 0f)
            return result;

        float clampedTime = Mathf.Clamp(time, 0f, totalDuration);
        float cursor = 0f;

        for (int i = 0; i < steps.Count; i++)
        {
            SequenceStep step = steps[i];
            float stepStart = cursor;
            float stepEnd = cursor + step.duration;
            bool isLast = i == steps.Count - 1;

            if (clampedTime <= stepEnd || isLast)
            {
                TrickPoseEntry currentEntry = ResolveEntry(profile, step.entryIndex);
                TrickPoseRigSnapshot currentSnapshot = controller.CreateRigSnapshotFromEntry(currentEntry);
                TrickPoseEditorPreviewContext currentContext = _simulateMatchConditionsDuringPreview
                    ? TrickPoseEditorPreviewUtility.BuildFromEntry(controller, currentEntry, TrickPosePreviewContextSource.SequencePreview, true)
                    : null;

                result.snapshot = currentSnapshot;
                result.stepIndex = i;
                result.label = currentEntry != null ? currentEntry.GetSummary() : $"Step {i + 1}";
                result.entry = currentEntry;
                result.context = currentContext;

                if (!isLast && step.transitionDuration > 0f)
                {
                    float transitionStart = Mathf.Max(stepStart, stepEnd - step.transitionDuration);
                    if (clampedTime > transitionStart)
                    {
                        float t = Mathf.InverseLerp(transitionStart, stepEnd, clampedTime);
                        TrickPoseEntry nextEntry = ResolveEntry(profile, steps[i + 1].entryIndex);
                        TrickPoseRigSnapshot nextSnapshot = controller.CreateRigSnapshotFromEntry(nextEntry);
                        result.snapshot = TrickPoseRigSnapshot.Lerp(currentSnapshot, nextSnapshot, t);
                    }
                }

                return result;
            }

            cursor = stepEnd;
        }

        return result;
    }

    private void ApplySequenceSimulation(SequenceEvaluation evaluation)
    {
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        if (_simulateMatchConditionsDuringPreview && evaluation.entry != null)
        {
            TrickPoseEditorSession.SetPreviewWindowSimulatedEntry(evaluation.entry);
            TrickPoseEditorSession.SetActiveSimulatedContext(evaluation.context, evaluation.entry);
        }
        else
        {
            TrickPoseEditorSession.ClearPreviewWindowSimulatedEntry();
            TrickPoseEditorSession.ClearActiveSimulatedContext();
        }

        TrickPoseEditorSession.ApplyPreviewSnapshot(controller, evaluation.snapshot, evaluation.context);
        Repaint();
    }

    private void Play()
    {
        _isPlaying = true;
        _manualLerp = null;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        UpdateEditorUpdateRegistration();
    }

    private void Pause()
    {
        _isPlaying = false;
        UpdateEditorUpdateRegistration();
    }

    private void StartInfluencePlayback()
    {
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        TrickPoseEditorSession.PreparePresentationBasis(controller);
        _influenceIsPlaying = true;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        UpdateEditorUpdateRegistration();
        EvaluateAndApplyInfluencePreview(useAccumulatedAngles: true);
    }

    private void PauseInfluencePlayback()
    {
        _influenceIsPlaying = false;
        UpdateEditorUpdateRegistration();
    }

    private void StopInfluencePlayback(bool restoreRotation, bool reapplyStaticPreview)
    {
        _influenceIsPlaying = false;
        UpdateEditorUpdateRegistration();
        if (restoreRotation)
            TrickPoseEditorSession.RestorePresentation();

        if (!reapplyStaticPreview)
            return;

        _influenceAccumulatedAngles = Vector3.zero;
        EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
    }

    private void ResetInfluenceRotation()
    {
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        _influenceAccumulatedAngles = Vector3.zero;
        TrickPoseEditorPreviewContext context = BuildEffectiveInfluenceContext(controller);
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        TrickPoseEntry matched = TrickPoseEditorPreviewUtility.EvaluateBestMatchingEntry(profile, context);
        TrickPoseRigSnapshot snapshot = matched != null
            ? controller.CreateRigSnapshotFromEntry(matched)
            : controller.CaptureTrueDefaultRigSnapshot();

        TrickPoseEditorSession.SetPreviewWindowSimulatedEntry(matched);
        TrickPoseEditorSession.SetActiveSimulatedContext(context, matched);
        TrickPoseEditorSession.ApplyPreviewSnapshot(controller, snapshot, context, true, Vector3.zero);
        Repaint();
    }

    private void StopPlayback(bool previewStartFrame)
    {
        _isPlaying = false;
        _manualLerp = null;
        _sequenceTime = 0f;
        UpdateEditorUpdateRegistration();
        if (previewStartFrame)
            EvaluateAndApplyCurrentTime();
    }

    private void ClearPreview()
    {
        Pause();
        PauseInfluencePlayback();
        _manualLerp = null;
        _influenceAccumulatedAngles = Vector3.zero;
        UpdateEditorUpdateRegistration();
        TrickPoseEditorSession.RestorePresentation();
        TrickPoseEditorSession.ClearPreviewWindowSimulatedEntry();
        TrickPoseEditorSession.ClearActiveSimulatedContext();
        TrickPoseEditorSession.PreviewTarget?.RestoreTrueDefaultPose(true);
        SceneView.RepaintAll();
        Repaint();
    }

    private void JumpToStep(int requestedIndex)
    {
        if (steps.Count == 0)
            return;

        Pause();
        _manualLerp = null;
        int index = Mathf.Clamp(requestedIndex, 0, steps.Count - 1);
        _sequenceTime = GetStepStartTime(index);
        EvaluateAndApplyCurrentTime();
    }

    private void JumpToSequenceEnd(float totalDuration)
    {
        Pause();
        _manualLerp = null;
        _sequenceTime = Mathf.Max(0f, totalDuration);
        EvaluateAndApplyCurrentTime();
    }

    private void SnapToStep(int index)
    {
        if (steps.Count == 0)
            return;

        Pause();
        _manualLerp = null;
        _sequenceTime = GetStepStartTime(index);
        EvaluateAndApplyCurrentTime();
    }

    private void LerpToStep(int index)
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (profile == null || controller == null || index < 0 || index >= steps.Count)
            return;

        _sequenceTime = GetStepStartTime(index);
        TrickPoseEntry entry = ResolveEntry(profile, steps[index].entryIndex);
        TrickPoseEditorPreviewContext context = _simulateMatchConditionsDuringPreview
            ? TrickPoseEditorPreviewUtility.BuildFromEntry(controller, entry, TrickPosePreviewContextSource.SequencePreview, true)
            : null;

        StartManualLerp(controller, entry, context, _previewLerpDuration);
    }

    private void StartManualLerpToEntry(TrickPoseEntry entry, float duration)
    {
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        TrickPoseEditorPreviewContext context = TrickPoseEditorSession.GetSelectedMatchPreviewContext(controller);
        StartManualLerp(controller, entry, context, duration);
    }

    private void StartManualLerp(SkiController controller, TrickPoseEntry entry, TrickPoseEditorPreviewContext context, float duration)
    {
        Pause();
        controller.EnsurePoseRigDefaultsCaptured();
        _manualLerp = new ManualLerpState
        {
            fromSnapshot = controller.CaptureCurrentRigSnapshot(),
            toSnapshot = entry != null ? controller.CreateRigSnapshotFromEntry(entry) : controller.CaptureTrueDefaultRigSnapshot(),
            duration = Mathf.Max(0.01f, duration),
            startTime = EditorApplication.timeSinceStartup,
            context = context
        };
        UpdateEditorUpdateRegistration();
    }

    private void ApplyInfluenceStateFromContext(TrickPoseEditorPreviewContext context)
    {
        if (context == null)
            return;

        TrickPoseEditorSession.PreviewMode = TrickPosePreviewMode.Influence;
        Pause();
        PauseInfluencePlayback();
        _manualLerp = null;
        _influenceAccumulatedAngles = Vector3.zero;

        TrickPoseInfluencePreviewState state = TrickPoseEditorSession.InfluencePreviewState;
        state.poseInputHeld = context.poseInputHeld;
        state.tuckInput = context.tuckInput;
        state.leftInput = context.leftInput;
        state.rightInput = context.rightInput;
        state.airborne = context.airborne;
        state.leanInput = context.leanInput;
        state.rising = context.rising;
        state.diving = context.diving;
        state.manualRotationEuler = context.hasPresentationRotation
            ? context.presentationRotationEuler
            : context.entryEulerAngles;
        state.manualRotationActive = context.hasPresentationRotation;
        _influenceManualRotation = state.manualRotationEuler;
        state.yawAngularVelocity = context.yawAngularVelocity;
        state.pitchAngularVelocity = context.pitchAngularVelocity;
        state.rollAngularVelocity = context.rollAngularVelocity;

        EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
        Repaint();
    }

    private void ApplyCoverageSlot(TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return;

        TrickPoseEditorSession.PreviewMode = TrickPosePreviewMode.Coverage;
        Pause();
        PauseInfluencePlayback();
        _manualLerp = null;
        _influenceAccumulatedAngles = Vector3.zero;

        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        TrickPoseEditorPreviewContext context = slot.representativeContext ?? TrickPoseCoveragePlanBuilder.BuildRepresentativeContext(controller, slot);
        slot.representativeContext = context;
        TrickPoseRigSnapshot snapshot = slot.assignedEntry != null
            ? controller.CreateRigSnapshotFromEntry(slot.assignedEntry)
            : controller.CaptureTrueDefaultRigSnapshot();

        TrickPoseEditorSession.SetActiveSimulatedContext(context, slot.assignedEntry);
        TrickPoseEditorSession.SetPreviewWindowSimulatedEntry(slot.assignedEntry);
        TrickPoseEditorSession.ApplyPreviewSnapshot(controller, snapshot, context);
        Repaint();
    }

    private bool NeedsEditorTick()
    {
        return _isPlaying || _influenceIsPlaying || _manualLerp != null;
    }

    private void UpdateEditorUpdateRegistration()
    {
        if (NeedsEditorTick())
            _lastEditorTime = EditorApplication.timeSinceStartup;

        SetEditorUpdateRegistration(NeedsEditorTick());
    }

    private void SetEditorUpdateRegistration(bool enabled)
    {
        if (_editorUpdateRegistered == enabled)
            return;

        if (enabled)
            EditorApplication.update += OnEditorUpdate;
        else
            EditorApplication.update -= OnEditorUpdate;

        _editorUpdateRegistered = enabled;
    }

    private int GetActiveStepIndex(float totalDuration)
    {
        if (steps.Count == 0)
            return -1;

        return EvaluateSequenceAtTime(TrickPoseEditorSession.ActiveProfile, TrickPoseEditorSession.PreviewTarget, _sequenceTime, totalDuration).stepIndex;
    }

    private float GetStepStartTime(int index)
    {
        float time = 0f;
        index = Mathf.Clamp(index, 0, Mathf.Max(0, steps.Count - 1));
        for (int i = 0; i < index; i++)
            time += steps[i].duration;
        return time;
    }

    private float GetTotalDuration()
    {
        float total = 0f;
        for (int i = 0; i < steps.Count; i++)
            total += Mathf.Max(0.05f, steps[i].duration);
        return total;
    }

    private static int DrawEntryPopup(string label, TrickPoseProfileSO profile, int currentIndex)
    {
        if (profile == null || profile.entries == null || profile.entries.Count == 0)
            return EditorGUILayout.IntField(label, currentIndex);

        string[] names = new string[profile.entries.Count];
        for (int i = 0; i < profile.entries.Count; i++)
            names[i] = profile.entries[i] != null ? profile.entries[i].GetSummary() : $"Entry {i}";

        currentIndex = Mathf.Clamp(currentIndex, 0, profile.entries.Count - 1);
        return EditorGUILayout.Popup(label, currentIndex, names);
    }

    private static TrickPoseEntry ResolveEntry(TrickPoseProfileSO profile, int index)
    {
        if (profile == null || profile.entries == null || profile.entries.Count == 0)
            return null;

        index = Mathf.Clamp(index, 0, profile.entries.Count - 1);
        return profile.entries[index];
    }

    private static float DrawPlaybackSpeed(string label, float current)
    {
        string[] options = { "0.25x", "0.5x", "1x", "2x" };
        float[] values = { 0.25f, 0.5f, 1f, 2f };
        int currentIndex = 2;
        for (int i = 0; i < values.Length; i++)
        {
            if (Mathf.Approximately(values[i], current))
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = EditorGUILayout.Popup(label, currentIndex, options);
        return values[nextIndex];
    }

    private static string SpinLabel(int sign)
    {
        return sign > 0 ? "CW" : sign < 0 ? "CCW" : "None";
    }

    private static string FlipLabel(int sign)
    {
        return sign > 0 ? "Front" : sign < 0 ? "Back" : "None";
    }

    private static string FormatPoseShape(SkiController.AerialPoseShape shape)
    {
        return shape switch
        {
            SkiController.AerialPoseShape.Compact => "Compact",
            SkiController.AerialPoseShape.Driving => "Forward Lean",
            SkiController.AerialPoseShape.LaidOut => "Backward Lean",
            _ => shape.ToString()
        };
    }

    private static string FormatOrientation(SkiController.AerialOrientationModifier orientation)
    {
        string label = SkiController.GetAerialOrientationModifierLabel(orientation);
        return string.IsNullOrWhiteSpace(label) ? "None" : label;
    }

    private void DrawDerivedInfluenceHint(TrickPoseEditorPreviewContext context)
    {
        if (context == null)
            return;

        bool canDeriveAerialState = context.airborne && context.poseInputHeld;
        if (canDeriveAerialState)
            return;

        string reason;
        if (!context.airborne && !context.poseInputHeld)
            reason = "Family / Shape / Orientation derivation is blocked because Airborne and Pose Input are both off.";
        else if (!context.airborne)
            reason = "Family / Shape / Orientation derivation is blocked because Airborne is off.";
        else
            reason = "Family / Shape / Orientation derivation is blocked because Pose Input is off.";

        EditorGUILayout.HelpBox(reason, MessageType.Info);
    }

    private string FormatDerivedValueBlockedAware(bool blocked, string value)
    {
        return blocked ? $"{value} (blocked)" : value;
    }

    private void DrawPreviewDiagnostics(TrickPoseProfileSO profile, TrickPoseEditorPreviewContext context)
    {
        if (profile == null || context == null)
            return;

        TrickPoseCoverageContextEvaluation evaluation = TrickPoseCoverageAnalyzer.EvaluateContext(profile, context, 3);

        DrawSectionHeader("Preview Diagnostics");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("State", TrickPoseCoverageAnalyzer.DescribeContext(context), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Classification", evaluation.classification.ToString());
            EditorGUILayout.LabelField("Summary", evaluation.Summary, EditorStyles.wordWrappedLabel);

            if (evaluation.ambiguousEntries.Count > 1)
                EditorGUILayout.LabelField("Ambiguous With", string.Join(", ", evaluation.ambiguousEntries.ConvertAll(entry => entry.GetSummary())));

            if (evaluation.suppressedEntries.Count > 0)
                EditorGUILayout.LabelField("Suppressed", string.Join(", ", evaluation.suppressedEntries.ConvertAll(entry => entry.GetSummary())), EditorStyles.wordWrappedLabel);

            if (evaluation.classification == TrickPoseCoverageClassification.Gap)
            {
                TrickPoseCoverageEntryDiagnostic nearest = evaluation.nearestEntries.Count > 0 ? evaluation.nearestEntries[0] : null;
                EditorGUILayout.HelpBox(TrickPoseCoverageAnalyzer.BuildGapSuggestion(context, nearest), MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Nearest Entries", EditorStyles.boldLabel);
            if (evaluation.nearestEntries.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                for (int i = 0; i < evaluation.nearestEntries.Count; i++)
                {
                    TrickPoseCoverageEntryDiagnostic diagnostic = evaluation.nearestEntries[i];
                    string failSummary = diagnostic.failReasons.Count > 0
                        ? string.Join(" | ", diagnostic.failReasons)
                        : "Matches current state";
                    EditorGUILayout.LabelField(
                        $"{i + 1}. {diagnostic.Summary} (P{diagnostic.priority}, S{diagnostic.specificity}, D {diagnostic.heuristicDistance:0.##})",
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(failSummary, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }
}
