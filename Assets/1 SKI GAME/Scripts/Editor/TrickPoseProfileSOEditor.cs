using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(TrickPoseProfileSO))]
public class TrickPoseProfileSOEditor : Editor
{
    private enum EntrySection
    {
        General,
        MatchConditions,
        PerPartPose,
        Transition
    }

    private ReorderableList _entriesList;
    private SerializedProperty _entriesProperty;
    private SerializedProperty _defaultBlendInSpeedProperty;
    private SerializedProperty _defaultBlendOutSpeedProperty;
    private SerializedProperty _previewLerpSpeedProperty;
    private bool _showContextDebugger = true;
    private TrickPoseEditorAnalysis.EntryReport[] _overlapReports = System.Array.Empty<TrickPoseEditorAnalysis.EntryReport>();

    private static readonly float Line = EditorGUIUtility.singleLineHeight;
    private const float VSpace = 2f;

    private void OnEnable()
    {
        _entriesProperty = serializedObject.FindProperty("entries");
        _defaultBlendInSpeedProperty = serializedObject.FindProperty("defaultBlendInSpeed");
        _defaultBlendOutSpeedProperty = serializedObject.FindProperty("defaultBlendOutSpeed");
        _previewLerpSpeedProperty = serializedObject.FindProperty("previewLerpSpeed");

        _entriesList = new ReorderableList(serializedObject, _entriesProperty, true, true, true, true);
        _entriesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Authored Trick Poses");
        _entriesList.elementHeightCallback = GetElementHeight;
        _entriesList.drawElementCallback = DrawElement;
        _entriesList.onSelectCallback = list =>
        {
            serializedObject.ApplyModifiedProperties();
            TrickPoseEditorSession.SetSelectedEntry((TrickPoseProfileSO)target, list.index);
            TrickPoseEditorSession.RefreshPreview(true);
        };
        _entriesList.onAddCallback = list =>
        {
            serializedObject.ApplyModifiedProperties();
            TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
            Undo.RecordObject(profile, "Add Trick Pose");
            profile.entries.Add(new TrickPoseEntry());
            EditorUtility.SetDirty(profile);
            serializedObject.Update();
            TrickPoseEditorSession.SetSelectedEntry(profile, profile.entries.Count - 1);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        TrickPoseEditorSession.SetProfile((TrickPoseProfileSO)target);
        _overlapReports = TrickPoseEditorAnalysis.AnalyzeProfile((TrickPoseProfileSO)target);

        EditorGUILayout.PropertyField(_defaultBlendInSpeedProperty);
        EditorGUILayout.PropertyField(_defaultBlendOutSpeedProperty);
        EditorGUILayout.PropertyField(_previewLerpSpeedProperty);

        SkiController previewTarget = (SkiController)EditorGUILayout.ObjectField("Preview Target", TrickPoseEditorSession.PreviewTarget, typeof(SkiController), true);
        if (previewTarget != TrickPoseEditorSession.PreviewTarget)
            TrickPoseEditorSession.SetPreviewTarget(previewTarget);

        TrickPoseEditorSession.SceneEditMode = EditorGUILayout.Toggle("Scene Edit Mode", TrickPoseEditorSession.SceneEditMode);
        TrickPoseEditorSession.SnapPreview = EditorGUILayout.Toggle("Snap Preview", TrickPoseEditorSession.SnapPreview);

        DrawLiveMirrorControls();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Preview Window"))
                TrickPosePreviewWindow.Open();

            if (GUILayout.Button("Recapture Defaults") && TrickPoseEditorSession.PreviewTarget != null)
            {
                Undo.RecordObject(TrickPoseEditorSession.PreviewTarget, "Recapture Pose Defaults");
                TrickPoseEditorSession.PreviewTarget.RecapturePoseRigDefaultsFromCurrent();
            }
        }

        EditorGUILayout.Space();
        _entriesList.DoLayoutList();

        DrawSelectedEntryToolbar();

        EditorGUILayout.Space();
        _showContextDebugger = EditorGUILayout.Foldout(_showContextDebugger, "Context Debugger", true);
        if (_showContextDebugger)
            TrickPoseContextDebugger.Draw(TrickPoseEditorSession.PreviewTarget);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLiveMirrorControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Live Mirror Utilities", EditorStyles.boldLabel);
        TrickPoseEditorSession.LiveMirrorEnabled = EditorGUILayout.Toggle("Mirror Live Updates", TrickPoseEditorSession.LiveMirrorEnabled);
        TrickPoseEditorSession.MirrorMode = (TrickPoseEditorSession.LiveMirrorMode)EditorGUILayout.EnumPopup("Mirror Mode", TrickPoseEditorSession.MirrorMode);
        TrickPoseEditorSession.MirrorDirection = (TrickPoseEditorSession.LiveMirrorDirection)EditorGUILayout.EnumPopup("Mirror Direction", TrickPoseEditorSession.MirrorDirection);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Copy Left To Right"))
                CopyLeftRight(leftToRight: true);

            if (GUILayout.Button("Copy Right To Left"))
                CopyLeftRight(leftToRight: false);
        }
    }

    private void DrawSelectedEntryToolbar()
    {
        TrickPoseEntry entry = TrickPoseEditorSession.SelectedEntry;
        if (entry == null)
            return;

        EditorGUILayout.LabelField("Entry Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Apply Pose") && TrickPoseEditorSession.PreviewTarget != null)
            TrickPoseEditorSession.PreviewTarget.PreviewTrickPoseEntry(entry, true);

        if (GUILayout.Button("Snap Preview"))
            TrickPoseEditorSession.RefreshPreview(true);

        if (GUILayout.Button("Lerp Preview") && TrickPoseEditorSession.PreviewTarget != null)
            TrickPosePreviewWindow.LerpToEntry(entry, Mathf.Max(0.01f, ((TrickPoseProfileSO)target).previewLerpSpeed > 0f ? 1f / ((TrickPoseProfileSO)target).previewLerpSpeed : 0.15f));

        if (GUILayout.Button("Capture Current") && TrickPoseEditorSession.PreviewTarget != null)
        {
            Undo.RecordObject(target, "Capture Trick Pose");
            TrickPoseEditorSession.PreviewTarget.CaptureCurrentPoseIntoEntry(entry);
            EditorUtility.SetDirty(target);
        }

        if (GUILayout.Button("Restore Defaults") && TrickPoseEditorSession.PreviewTarget != null)
            TrickPoseEditorSession.PreviewTarget.RestoreDefaultRigSnapshot(true);

        if (GUILayout.Button("Duplicate"))
            DuplicateSelectedEntry(flipped: false);

        if (GUILayout.Button("Duplicate Flipped"))
            DuplicateSelectedEntry(flipped: true);

        if (GUILayout.Button("Flip Current"))
        {
            Undo.RecordObject(target, "Flip Trick Pose");
            FlipEntry(entry);
            EditorUtility.SetDirty(target);
            TrickPoseEditorSession.RefreshPreview(true);
        }

        if (GUILayout.Button("Reset Entry"))
        {
            Undo.RecordObject(target, "Reset Trick Pose Entry");
            ResetEntry(entry);
            EditorUtility.SetDirty(target);
        }

        if (GUILayout.Button("Delete Entry"))
        {
            int index = TrickPoseEditorSession.SelectedEntryIndex;
            if (index >= 0 && index < _entriesProperty.arraySize)
            {
                _entriesProperty.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                TrickPoseEditorSession.SetSelectedEntry((TrickPoseProfileSO)target, Mathf.Clamp(index - 1, -1, _entriesProperty.arraySize - 1));
            }
        }
    }

    private float GetElementHeight(int index)
    {
        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
        float height = VSpace;

        height += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.General))
            height += GetGeneralHeight(entry);

        height += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.MatchConditions))
            height += GetMatchConditionsHeight(entry);

        height += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.PerPartPose))
            height += GetPerPartHeight(entry);

        height += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.Transition))
            height += GetTransitionHeight(entry);

        return height + 4f;
    }

    private void DrawElement(Rect rect, int index, bool active, bool focused)
    {
        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
        rect.y += 2f;
        rect.height = Line;

        DrawSectionFoldout(rect, index, EntrySection.General, GetEntryHeader(index));
        rect.y += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.General))
            DrawGeneral(ref rect, entry);

        DrawSectionFoldout(rect, index, EntrySection.MatchConditions, "Match Conditions");
        rect.y += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.MatchConditions))
            DrawMatchConditions(ref rect, entry, index);

        DrawSectionFoldout(rect, index, EntrySection.PerPartPose, "Per-Part Pose");
        rect.y += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.PerPartPose))
            DrawPerPartPose(ref rect, entry);

        DrawSectionFoldout(rect, index, EntrySection.Transition, "Transition");
        rect.y += Line + VSpace;
        if (GetSectionExpanded(index, EntrySection.Transition))
            DrawTransition(ref rect, entry);
    }

    private void DrawGeneral(ref Rect rect, SerializedProperty entry)
    {
        DrawProperty(ref rect, entry.FindPropertyRelative("enabled"));
        DrawProperty(ref rect, entry.FindPropertyRelative("displayName"));
        DrawProperty(ref rect, entry.FindPropertyRelative("priority"));
        DrawProperty(ref rect, entry.FindPropertyRelative("overallWeight"));
        DrawProperty(ref rect, entry.FindPropertyRelative("overridePoseLabel"));
    }

    private void DrawMatchConditions(ref Rect rect, SerializedProperty entry, int index)
    {
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredPoseFamily"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredPoseShape"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredOrientationModifier"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredPoseName"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requireAirborne"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requirePoseButtonHeld"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredSpinDirection"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredFlipDirection"));
        DrawRangeField(ref rect, entry.FindPropertyRelative("yawAngularVelocityRange"), "Yaw Angular Velocity");
        DrawRangeField(ref rect, entry.FindPropertyRelative("pitchAngularVelocityRange"), "Pitch Angular Velocity");
        DrawRangeField(ref rect, entry.FindPropertyRelative("rollAngularVelocityRange"), "Roll Angular Velocity");
        DrawRangeField(ref rect, entry.FindPropertyRelative("totalAngularSpeedRange"), "Total Angular Speed");

        DrawOverlapSummary(ref rect, index);

        if (index == TrickPoseEditorSession.SelectedEntryIndex)
            DrawMatchPreviewControls(ref rect, index);
    }

    private void DrawPerPartPose(ref Rect rect, SerializedProperty entry)
    {
        DrawProperty(ref rect, entry.FindPropertyRelative("bodyPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("headPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("leftSkiPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("rightSkiPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("leftPolePose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("rightPolePose"), true);
    }

    private void DrawTransition(ref Rect rect, SerializedProperty entry)
    {
        DrawProperty(ref rect, entry.FindPropertyRelative("blendInSpeed"));
        DrawProperty(ref rect, entry.FindPropertyRelative("blendOutSpeed"));
        DrawProperty(ref rect, entry.FindPropertyRelative("snapOnPreview"));
        DrawProperty(ref rect, entry.FindPropertyRelative("allowBlendWithOthers"));
    }

    private void DrawProperty(ref Rect rect, SerializedProperty property, bool includeChildren = false)
    {
        float height = EditorGUI.GetPropertyHeight(property, includeChildren);
        Rect propertyRect = new Rect(rect.x, rect.y, rect.width, height);
        EditorGUI.PropertyField(propertyRect, property, includeChildren);
        rect.y += height + VSpace;
    }

    private void DrawRangeField(ref Rect rect, SerializedProperty rangeProperty, string label)
    {
        SerializedProperty enabledProp = rangeProperty.FindPropertyRelative("enabled");
        SerializedProperty valueProp = rangeProperty.FindPropertyRelative("range");

        Rect lineRect = new Rect(rect.x, rect.y, rect.width, Line);
        Rect toggleRect = new Rect(lineRect.x, lineRect.y, 18f, lineRect.height);
        Rect labelRect = new Rect(toggleRect.xMax + 2f, lineRect.y, 130f, lineRect.height);
        Rect minRect = new Rect(labelRect.xMax + 4f, lineRect.y, (lineRect.width - 160f) * 0.5f, lineRect.height);
        Rect maxRect = new Rect(minRect.xMax + 4f, lineRect.y, minRect.width, lineRect.height);

        enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
        EditorGUI.LabelField(labelRect, label);

        using (new EditorGUI.DisabledScope(!enabledProp.boolValue))
        {
            Vector2 range = valueProp.vector2Value;
            float min = EditorGUI.FloatField(minRect, range.x);
            float max = EditorGUI.FloatField(maxRect, range.y);
            valueProp.vector2Value = new Vector2(min, max);
        }

        rect.y += Line + VSpace;
    }

    private float GetGeneralHeight(SerializedProperty entry)
    {
        return SumHeights(
            entry.FindPropertyRelative("enabled"),
            entry.FindPropertyRelative("displayName"),
            entry.FindPropertyRelative("priority"),
            entry.FindPropertyRelative("overallWeight"),
            entry.FindPropertyRelative("overridePoseLabel"));
    }

    private float GetMatchConditionsHeight(SerializedProperty entry)
    {
        float height = SumHeights(
            entry.FindPropertyRelative("requiredPoseFamily"),
            entry.FindPropertyRelative("requiredPoseShape"),
            entry.FindPropertyRelative("requiredOrientationModifier"),
            entry.FindPropertyRelative("requiredPoseName"),
            entry.FindPropertyRelative("requireAirborne"),
            entry.FindPropertyRelative("requirePoseButtonHeld"),
            entry.FindPropertyRelative("requiredSpinDirection"),
            entry.FindPropertyRelative("requiredFlipDirection"));

        height += ((Line + VSpace) * 4f);

        int index = GetEntryIndex(entry);
        height += GetOverlapSummaryHeight(index);
        if (index == TrickPoseEditorSession.SelectedEntryIndex)
            height += GetMatchPreviewHeight();

        return height;
    }

    private float GetPerPartHeight(SerializedProperty entry)
    {
        return SumHeights(
            entry.FindPropertyRelative("bodyPose"), true,
            entry.FindPropertyRelative("headPose"), true,
            entry.FindPropertyRelative("leftSkiPose"), true,
            entry.FindPropertyRelative("rightSkiPose"), true,
            entry.FindPropertyRelative("leftPolePose"), true,
            entry.FindPropertyRelative("rightPolePose"), true);
    }

    private float GetTransitionHeight(SerializedProperty entry)
    {
        return SumHeights(
            entry.FindPropertyRelative("blendInSpeed"),
            entry.FindPropertyRelative("blendOutSpeed"),
            entry.FindPropertyRelative("snapOnPreview"),
            entry.FindPropertyRelative("allowBlendWithOthers"));
    }

    private float SumHeights(params SerializedProperty[] properties)
    {
        float total = 0f;
        for (int i = 0; i < properties.Length; i++)
            total += EditorGUI.GetPropertyHeight(properties[i], false) + VSpace;
        return total;
    }

    private float SumHeights(SerializedProperty p1, bool c1, SerializedProperty p2, bool c2, SerializedProperty p3, bool c3, SerializedProperty p4, bool c4, SerializedProperty p5, bool c5, SerializedProperty p6, bool c6)
    {
        return EditorGUI.GetPropertyHeight(p1, c1) + VSpace +
               EditorGUI.GetPropertyHeight(p2, c2) + VSpace +
               EditorGUI.GetPropertyHeight(p3, c3) + VSpace +
               EditorGUI.GetPropertyHeight(p4, c4) + VSpace +
               EditorGUI.GetPropertyHeight(p5, c5) + VSpace +
               EditorGUI.GetPropertyHeight(p6, c6) + VSpace;
    }

    private void DrawSectionFoldout(Rect rect, int index, EntrySection section, GUIContent label)
    {
        bool expanded = GetSectionExpanded(index, section);
        bool next = EditorGUI.Foldout(rect, expanded, label, true);
        if (next != expanded)
            SetSectionExpanded(index, section, next);
    }

    private void DrawSectionFoldout(Rect rect, int index, EntrySection section, string label)
    {
        DrawSectionFoldout(rect, index, section, new GUIContent(label));
    }

    private bool GetSectionExpanded(int index, EntrySection section)
    {
        return SessionState.GetBool(GetSectionKey(index, section), section == EntrySection.General);
    }

    private void SetSectionExpanded(int index, EntrySection section, bool expanded)
    {
        SessionState.SetBool(GetSectionKey(index, section), expanded);
    }

    private string GetSectionKey(int index, EntrySection section)
    {
        return $"TrickPoseProfileSOEditor.{target.GetInstanceID()}.{index}.{section}";
    }

    private GUIContent GetEntryHeader(int index)
    {
        string label = GetEntryLabel(index);
        TrickPoseEditorAnalysis.EntryReport report = GetReport(index);
        if (report == null || report.Severity == TrickPoseEditorAnalysis.OverlapSeverity.None)
            return new GUIContent(label);

        string iconName = report.Severity switch
        {
            TrickPoseEditorAnalysis.OverlapSeverity.Error => "console.erroricon.sml",
            TrickPoseEditorAnalysis.OverlapSeverity.Warning => "console.warnicon.sml",
            _ => "console.infoicon.sml"
        };

        GUIContent content = EditorGUIUtility.IconContent(iconName);
        return new GUIContent(label, content.image, report.Summary);
    }

    private TrickPoseEditorAnalysis.EntryReport GetReport(int index)
    {
        if (_overlapReports == null || index < 0 || index >= _overlapReports.Length)
            return null;

        return _overlapReports[index];
    }

    private void DrawOverlapSummary(ref Rect rect, int index)
    {
        TrickPoseEditorAnalysis.EntryReport report = GetReport(index);
        if (report == null)
            return;

        Rect boxRect = new Rect(rect.x, rect.y, rect.width, Line * 1.4f);
        MessageType type = report.Severity switch
        {
            TrickPoseEditorAnalysis.OverlapSeverity.Error => MessageType.Error,
            TrickPoseEditorAnalysis.OverlapSeverity.Warning => MessageType.Warning,
            TrickPoseEditorAnalysis.OverlapSeverity.Info => MessageType.Info,
            _ => MessageType.None
        };

        if (type != MessageType.None)
            EditorGUI.HelpBox(boxRect, report.Summary, type);
        else
            EditorGUI.LabelField(boxRect, report.Summary);

        rect.y += boxRect.height + VSpace;

        if (type != MessageType.None)
        {
            bool detailsExpanded = SessionState.GetBool(GetOverlapDetailKey(index), false);
            Rect foldoutRect = new Rect(rect.x, rect.y, rect.width, Line);
            detailsExpanded = EditorGUI.Foldout(foldoutRect, detailsExpanded, "Overlap Details", true);
            SessionState.SetBool(GetOverlapDetailKey(index), detailsExpanded);
            rect.y += Line + VSpace;

            if (detailsExpanded)
            {
                foreach (string line in BuildOverlapDetailLines(report))
                {
                    Rect labelRect = new Rect(rect.x + 12f, rect.y, rect.width - 12f, Line);
                    EditorGUI.LabelField(labelRect, line);
                    rect.y += Line + VSpace;
                }
            }
        }
    }

    private float GetOverlapSummaryHeight(int index)
    {
        TrickPoseEditorAnalysis.EntryReport report = GetReport(index);
        if (report == null)
            return 0f;

        float height = (Line * 1.4f) + VSpace;
        if (report.Severity != TrickPoseEditorAnalysis.OverlapSeverity.None && SessionState.GetBool(GetOverlapDetailKey(index), false))
            height += (Line + VSpace) * (1 + BuildOverlapDetailLines(report).Count);
        else if (report.Severity != TrickPoseEditorAnalysis.OverlapSeverity.None)
            height += Line + VSpace;

        return height;
    }

    private void DrawMatchPreviewControls(ref Rect rect, int index)
    {
        Rect boxRect = new Rect(rect.x, rect.y, rect.width, (Line + VSpace) * 6.6f);
        GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

        Rect row = new Rect(boxRect.x + 6f, boxRect.y + 6f, boxRect.width - 12f, Line);
        EditorGUI.LabelField(row, "Match Preview", EditorStyles.boldLabel);
        row.y += Line + VSpace;
        EditorGUI.BeginChangeCheck();
        TrickPoseEditorSession.PreviewMatchConditions = EditorGUI.ToggleLeft(row, "Preview Match Conditions", TrickPoseEditorSession.PreviewMatchConditions);
        row.y += Line + VSpace;
        TrickPoseEditorSession.ShowSceneGizmos = EditorGUI.ToggleLeft(row, "Show Scene Gizmos", TrickPoseEditorSession.ShowSceneGizmos);
        row.y += Line + VSpace;
        TrickPoseEditorSession.PreviewAsIfMatched = EditorGUI.ToggleLeft(row, "Preview As If Matched", TrickPoseEditorSession.PreviewAsIfMatched);
        row.y += Line + VSpace;
        bool changed = EditorGUI.EndChangeCheck();

        TrickPoseEntry selected = TrickPoseEditorSession.SelectedEntry;
        if (selected != null && TrickPoseEditorSession.PreviewTarget != null)
        {
            SkiController controller = TrickPoseEditorSession.PreviewTarget;
            TrickPoseEditorPreviewContext previewContext = TrickPoseEditorSession.GetSelectedMatchPreviewContext(controller);
            string previewText = TrickPoseEditorSession.PreviewAsIfMatched && previewContext != null
                ? $"Simulated: {previewContext.poseFamily} / {previewContext.poseShape} / {previewContext.orientationModifier}"
                : $"Current: {controller.CurrentPoseFamily} / {controller.CurrentPoseShape} / {controller.CurrentPoseOrientationModifier}";
            EditorGUI.LabelField(row, previewText);
            row.y += Line + VSpace;

            string angularText = TrickPoseEditorSession.PreviewAsIfMatched && previewContext != null
                ? $"Spin {SelectedSpinLabel(selected, controller)}, Flip {SelectedFlipLabel(selected, controller)}, Yaw {previewContext.yawAngularVelocity:0.#}, Pitch {previewContext.pitchAngularVelocity:0.#}"
                : $"Spin {SpinLabel(controller.CurrentSpinDirectionSign)}, Flip {FlipLabel(controller.CurrentFlipDirectionSign)}, Yaw {controller.CurrentYawAngularVelocity:0.#}, Pitch {controller.CurrentPitchAngularVelocity:0.#}";
            EditorGUI.LabelField(row, angularText);
        }

        if (changed)
            TrickPoseEditorSession.RefreshPreview(true);

        rect.y += boxRect.height + VSpace;
    }

    private float GetMatchPreviewHeight()
    {
        return ((Line + VSpace) * 6.6f) + VSpace;
    }

    private int GetEntryIndex(SerializedProperty entry)
    {
        string path = entry.propertyPath;
        int start = path.IndexOf('[');
        int end = path.IndexOf(']');
        if (start < 0 || end <= start)
            return -1;

        if (int.TryParse(path.Substring(start + 1, end - start - 1), out int index))
            return index;

        return -1;
    }

    private string GetOverlapDetailKey(int index)
    {
        return $"TrickPoseProfileSOEditor.{target.GetInstanceID()}.OverlapDetails.{index}";
    }

    private static List<string> BuildOverlapDetailLines(TrickPoseEditorAnalysis.EntryReport report)
    {
        List<string> lines = new List<string>();
        if (report.blockedBy.Count > 0)
            lines.Add($"Blocked by: {string.Join(", ", report.blockedBy)}");
        if (report.blocks.Count > 0)
            lines.Add($"Blocks: {string.Join(", ", report.blocks)}");
        if (report.ambiguous.Count > 0)
            lines.Add($"Ambiguous overlap: {string.Join(", ", report.ambiguous)}");
        if (report.partials.Count > 0)
            lines.Add($"Partial overlap: {string.Join(", ", report.partials)}");
        if (lines.Count == 0)
            lines.Add("No overlap detected.");
        return lines;
    }

    private static string SelectedValueLabel(TrickPoseAngularVelocityRange range, float current)
    {
        if (!range.enabled)
            return current.ToString("0.#");

        Vector2 sorted = range.GetSortedRange();
        return $"{(sorted.x + sorted.y) * 0.5f:0.#} (range {sorted.x:0.#}..{sorted.y:0.#})";
    }

    private static string SelectedSpinLabel(TrickPoseEntry entry, SkiController controller)
    {
        if (entry.requiredSpinDirection == TrickPoseSpinDirectionRequirement.Any)
            return SpinLabel(controller.CurrentSpinDirectionSign);

        return entry.requiredSpinDirection == TrickPoseSpinDirectionRequirement.Clockwise ? "CW" : "CCW";
    }

    private static string SelectedFlipLabel(TrickPoseEntry entry, SkiController controller)
    {
        if (entry.requiredFlipDirection == TrickPoseFlipDirectionRequirement.Any)
            return FlipLabel(controller.CurrentFlipDirectionSign);

        return entry.requiredFlipDirection == TrickPoseFlipDirectionRequirement.Frontflip ? "Front" : "Back";
    }

    private static string SpinLabel(int sign)
    {
        return sign > 0 ? "CW" : sign < 0 ? "CCW" : "None";
    }

    private static string FlipLabel(int sign)
    {
        return sign > 0 ? "Front" : sign < 0 ? "Back" : "None";
    }

    private void CopyLeftRight(bool leftToRight)
    {
        TrickPoseEntry entry = TrickPoseEditorSession.SelectedEntry;
        if (entry == null)
            return;

        Undo.RecordObject(target, leftToRight ? "Copy Left To Right" : "Copy Right To Left");
        if (leftToRight)
        {
            TrickPoseEditorSession.CopyPairedPose(entry.leftSkiPose, entry.rightSkiPose);
            TrickPoseEditorSession.CopyPairedPose(entry.leftPolePose, entry.rightPolePose);
        }
        else
        {
            TrickPoseEditorSession.CopyPairedPose(entry.rightSkiPose, entry.leftSkiPose);
            TrickPoseEditorSession.CopyPairedPose(entry.rightPolePose, entry.leftPolePose);
        }

        EditorUtility.SetDirty(target);
        TrickPoseEditorSession.RefreshPreview(true);
    }

    private void DuplicateSelectedEntry(bool flipped)
    {
        int sourceIndex = TrickPoseEditorSession.SelectedEntryIndex;
        if (sourceIndex < 0 || sourceIndex >= _entriesProperty.arraySize)
            return;

        serializedObject.ApplyModifiedProperties();
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        TrickPoseEntry source = profile.entries[sourceIndex];
        TrickPoseEntry copy = JsonUtility.FromJson<TrickPoseEntry>(JsonUtility.ToJson(source));
        copy.displayName = flipped ? $"{source.displayName} (Flipped)" : $"{source.displayName} Copy";
        if (flipped)
            FlipEntry(copy);

        Undo.RecordObject(target, "Duplicate Trick Pose");
        profile.entries.Insert(sourceIndex + 1, copy);
        EditorUtility.SetDirty(target);
        serializedObject.Update();
        TrickPoseEditorSession.SetSelectedEntry(profile, sourceIndex + 1);
    }

    private string GetEntryLabel(int index)
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        if (profile.entries == null || index < 0 || index >= profile.entries.Count || profile.entries[index] == null)
            return $"Entry {index}";

        TrickPoseEntry entry = profile.entries[index];
        string state = entry.enabled ? "On" : "Off";
        return $"{state} | P{entry.priority} | {entry.GetSummary()}";
    }

    private static void ResetEntry(TrickPoseEntry entry)
    {
        entry.enabled = true;
        entry.displayName = "New Trick Pose";
        entry.priority = 0;
        entry.overallWeight = 1f;
        entry.overridePoseLabel = string.Empty;
        entry.requiredPoseFamily = SkiController.AerialPoseFamily.None;
        entry.requiredPoseShape = SkiController.AerialPoseShape.None;
        entry.requiredOrientationModifier = SkiController.AerialOrientationModifier.None;
        entry.requiredPoseName = string.Empty;
        entry.requireAirborne = TrickPoseBoolRequirement.Ignore;
        entry.requirePoseButtonHeld = TrickPoseBoolRequirement.Ignore;
        entry.requiredSpinDirection = TrickPoseSpinDirectionRequirement.Any;
        entry.requiredFlipDirection = TrickPoseFlipDirectionRequirement.Any;
        entry.yawAngularVelocityRange = default;
        entry.pitchAngularVelocityRange = default;
        entry.rollAngularVelocityRange = default;
        entry.totalAngularSpeedRange = default;
        entry.blendInSpeed = 8f;
        entry.blendOutSpeed = 8f;
        entry.snapOnPreview = true;
        entry.allowBlendWithOthers = false;
        entry.bodyPose.Reset();
        entry.headPose.Reset();
        entry.leftSkiPose.Reset();
        entry.rightSkiPose.Reset();
        entry.leftPolePose.Reset();
        entry.rightPolePose.Reset();
    }

    private static void FlipEntry(TrickPoseEntry entry)
    {
        SwapAndFlip(ref entry.leftSkiPose, ref entry.rightSkiPose);
        SwapAndFlip(ref entry.leftPolePose, ref entry.rightPolePose);
        FlipSingle(entry.bodyPose);
        FlipSingle(entry.headPose);

        if (entry.requiredPoseFamily == SkiController.AerialPoseFamily.Left)
            entry.requiredPoseFamily = SkiController.AerialPoseFamily.Right;
        else if (entry.requiredPoseFamily == SkiController.AerialPoseFamily.Right)
            entry.requiredPoseFamily = SkiController.AerialPoseFamily.Left;
    }

    private static void SwapAndFlip(ref PosePartTransformData a, ref PosePartTransformData b)
    {
        PosePartTransformData temp = JsonUtility.FromJson<PosePartTransformData>(JsonUtility.ToJson(a));
        a = JsonUtility.FromJson<PosePartTransformData>(JsonUtility.ToJson(b));
        b = temp;
        FlipSingle(a);
        FlipSingle(b);
    }

    private static void FlipSingle(PosePartTransformData part)
    {
        if (part == null)
            return;

        part.localPosition = new Vector3(-part.localPosition.x, part.localPosition.y, part.localPosition.z);
        part.localEulerAngles = new Vector3(part.localEulerAngles.x, -part.localEulerAngles.y, -part.localEulerAngles.z);
    }
}
