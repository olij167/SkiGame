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

    private bool _isPlaying;
    private bool _influenceIsPlaying;
    private double _lastEditorTime;
    private bool _isScrubbing;
    private ManualLerpState _manualLerp;
    private Vector3 _influenceAccumulatedAngles;
    private static TrickPosePreviewWindow _instance;

    [MenuItem("Window/Ski Game/Trick Pose Preview")]
    public static void Open()
    {
        _instance = GetWindow<TrickPosePreviewWindow>("Trick Pose Preview");
    }

    public static void LerpToEntry(TrickPoseEntry entry, float duration)
    {
        if (_instance == null)
            Open();

        _instance?.StartManualLerpToEntry(entry, duration);
    }

    private void OnEnable()
    {
        _instance = this;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        if (_instance == this)
            _instance = null;

        StopInfluencePlayback(restoreRotation: true, reapplyStaticPreview: false);
        TrickPoseEditorSession.RestorePresentation();
        TrickPoseEditorSession.ClearPreviewWindowSimulatedEntry();
        TrickPoseEditorSession.ClearActiveSimulatedContext();
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnGUI()
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;

        DrawModeToolbar();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawSectionHeader("Preview Setup");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField("Profile", profile, typeof(TrickPoseProfileSO), false);
            EditorGUILayout.ObjectField("Preview Target", controller, typeof(SkiController), true);
        }

        if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Sequence)
            DrawSequencePreview(profile, controller);
        else
            DrawInfluencePreview(profile, controller);

        EditorGUILayout.EndScrollView();
    }

    private void DrawModeToolbar()
    {
        EditorGUI.BeginChangeCheck();
        TrickPosePreviewMode mode = (TrickPosePreviewMode)GUILayout.Toolbar(
            (int)TrickPoseEditorSession.PreviewMode,
            new[] { "Sequence Preview", "Influence Preview" });
        if (EditorGUI.EndChangeCheck())
        {
            if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Influence)
                StopInfluencePlayback(restoreRotation: false, reapplyStaticPreview: false);

            TrickPoseEditorSession.PreviewMode = mode;
            Pause();
            _manualLerp = null;
            if (mode == TrickPosePreviewMode.Influence)
                EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
            else
                EvaluateAndApplyCurrentTime();
        }
    }

    private void DrawSequencePreview(TrickPoseProfileSO profile, SkiController controller)
    {
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
        TrickPoseInfluencePreviewState state = TrickPoseEditorSession.InfluencePreviewState;
        DrawSectionHeader("Influence Controls");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            state.poseInputHeld = EditorGUILayout.Toggle("Pose Input", state.poseInputHeld);
            state.tuckInput = EditorGUILayout.Toggle("Tuck Input", state.tuckInput);
            state.leftInput = EditorGUILayout.Toggle("Left Input", state.leftInput);
            state.rightInput = EditorGUILayout.Toggle("Right Input", state.rightInput);
            bool airborne = EditorGUILayout.Toggle("Airborne", state.airborne);
            if (airborne != state.airborne)
                state.airborne = airborne;
            EditorGUILayout.Toggle("Grounded", !state.airborne);
            bool rising = EditorGUILayout.Toggle("Rising", state.rising);
            bool diving = EditorGUILayout.Toggle("Diving", state.diving);
            state.rising = rising && !diving;
            state.diving = diving && !rising;
            state.yawAngularVelocity = EditorGUILayout.FloatField("Yaw Angular Velocity", state.yawAngularVelocity);
            state.pitchAngularVelocity = EditorGUILayout.FloatField("Pitch Angular Velocity", state.pitchAngularVelocity);
            state.rollAngularVelocity = EditorGUILayout.FloatField("Roll Angular Velocity", state.rollAngularVelocity);

            if (EditorGUI.EndChangeCheck())
            {
                if (_influenceIsPlaying)
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: true);
                else
                    EvaluateAndApplyInfluencePreview(useAccumulatedAngles: false);
            }
        }

        TrickPoseEditorPreviewContext context = BuildEffectiveInfluenceContext(controller);
        List<TrickPoseEntry> matches = TrickPoseEditorPreviewUtility.EvaluateMatchingEntries(profile, context);
        TrickPoseEntry bestMatchedEntry = matches.Count > 0 ? TrickPoseEditorPreviewUtility.EvaluateBestMatchingEntry(profile, context) : null;

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

        DrawSectionHeader("Derived Preview Context");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Pose Family", context != null ? context.poseFamily.ToString() : "(none)");
                EditorGUILayout.TextField("Pose Shape", context != null ? context.poseShape.ToString() : "(none)");
                EditorGUILayout.TextField("Orientation Modifier", context != null ? context.orientationModifier.ToString() : "(none)");
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

        TrickPoseEntry selected = TrickPoseEditorSession.SelectedEntry;
        if (selected != null && context != null)
        {
            List<TrickPoseEntryConditionStatus> statuses = TrickPoseEditorPreviewUtility.BuildConditionStatuses(selected, context);
            DrawSectionHeader("Selected Entry Conditions");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Selected Entry", selected.GetSummary());
                for (int i = 0; i < statuses.Count; i++)
                    EditorGUILayout.LabelField(statuses[i].label, statuses[i].status);
            }
        }
    }

    private void OnEditorUpdate()
    {
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
        SceneView.RepaintAll();
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
        List<TrickPoseEntry> matches = TrickPoseEditorPreviewUtility.EvaluateMatchingEntries(profile, context);
        TrickPoseEntry matched = matches.Count > 0 ? TrickPoseEditorPreviewUtility.EvaluateBestMatchingEntry(profile, context) : null;
        TrickPoseRigSnapshot snapshot = matched != null
            ? controller.CreateRigSnapshotFromEntry(matched)
            : controller.CaptureDefaultRigSnapshot();

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
            poseInputHeld = source.poseInputHeld,
            tuckInput = source.tuckInput,
            leftInput = source.leftInput,
            rightInput = source.rightInput,
            airborne = source.airborne,
            rising = source.rising,
            diving = source.diving,
            yawAngularVelocity = _influenceUseYaw ? source.yawAngularVelocity : 0f,
            pitchAngularVelocity = _influenceUsePitch ? source.pitchAngularVelocity : 0f,
            rollAngularVelocity = _influenceUseRoll ? source.rollAngularVelocity : 0f
        };
    }

    private SequenceEvaluation EvaluateSequenceAtTime(TrickPoseProfileSO profile, SkiController controller, float time, float totalDuration)
    {
        SequenceEvaluation result = new SequenceEvaluation
        {
            snapshot = controller != null ? controller.CaptureDefaultRigSnapshot() : null,
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
    }

    private void Pause()
    {
        _isPlaying = false;
    }

    private void StartInfluencePlayback()
    {
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (controller == null)
            return;

        TrickPoseEditorSession.PreparePresentationBasis(controller);
        _influenceIsPlaying = true;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EvaluateAndApplyInfluencePreview(useAccumulatedAngles: true);
    }

    private void PauseInfluencePlayback()
    {
        _influenceIsPlaying = false;
    }

    private void StopInfluencePlayback(bool restoreRotation, bool reapplyStaticPreview)
    {
        _influenceIsPlaying = false;
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
        List<TrickPoseEntry> matches = TrickPoseEditorPreviewUtility.EvaluateMatchingEntries(profile, context);
        TrickPoseEntry matched = matches.Count > 0 ? TrickPoseEditorPreviewUtility.EvaluateBestMatchingEntry(profile, context) : null;
        TrickPoseRigSnapshot snapshot = matched != null
            ? controller.CreateRigSnapshotFromEntry(matched)
            : controller.CaptureDefaultRigSnapshot();

        TrickPoseEditorSession.SetPreviewWindowSimulatedEntry(matched);
        TrickPoseEditorSession.SetActiveSimulatedContext(context, matched);
        TrickPoseEditorSession.ApplyPreviewSnapshotWithoutPresentationRotation(controller, snapshot, context);
        Repaint();
    }

    private void StopPlayback(bool previewStartFrame)
    {
        _isPlaying = false;
        _manualLerp = null;
        _sequenceTime = 0f;
        if (previewStartFrame)
            EvaluateAndApplyCurrentTime();
    }

    private void ClearPreview()
    {
        Pause();
        PauseInfluencePlayback();
        _manualLerp = null;
        _influenceAccumulatedAngles = Vector3.zero;
        TrickPoseEditorSession.RestorePresentation();
        TrickPoseEditorSession.ClearPreviewWindowSimulatedEntry();
        TrickPoseEditorSession.ClearActiveSimulatedContext();
        TrickPoseEditorSession.PreviewTarget?.RestoreDefaultRigSnapshot(true);
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
            toSnapshot = entry != null ? controller.CreateRigSnapshotFromEntry(entry) : controller.CaptureDefaultRigSnapshot(),
            duration = Mathf.Max(0.01f, duration),
            startTime = EditorApplication.timeSinceStartup,
            context = context
        };
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
}
