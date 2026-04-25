using System;
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

    private enum TopLevelSection
    {
        ProfileSettings,
        PreviewSceneAuthoring,
        FocusedPose,
        FocusedPoseActions,
        AuthoringHelpers,
        AuthoredPoses,
        FullReorderableMaintenance
    }

    private enum EntryToggleFilter
    {
        All,
        Enabled,
        Disabled
    }

    private enum EntryAuthoringFilter
    {
        All,
        Placeholder,
        Authored
    }

    private enum EntryWarningFilter
    {
        All,
        Warning,
        Clean
    }

    private enum CoverageMarkerKind
    {
        None,
        Overlap,
        Ambiguous,
        SuppressedByOther,
        SuppressesOther,
        Placeholder,
        Disabled
    }

    private sealed class CoverageEntrySummary
    {
        public int cleanOwnedCount;
        public int ambiguousCount;
        public int suppressedByCount;
        public int suppressesOtherCount;
        public readonly HashSet<string> ambiguousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressedByNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressesOtherNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> ambiguousSlotLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressedBySlotLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressesOtherSlotLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private ReorderableList _entriesList;
    private SerializedProperty _entriesProperty;
    private SerializedProperty _defaultBlendInSpeedProperty;
    private SerializedProperty _defaultBlendOutSpeedProperty;
    private SerializedProperty _previewLerpSpeedProperty;
    private SerializedProperty _rigAssistSettingsProperty;
    private bool _showContextDebugger;
    private bool _showAdvancedPreviewBaselineTools;
    private bool _showRigAssistTools = true;
    private bool _showLiveMirrorTools = true;
    private bool _showNavigatorFilters = true;
    private string _navigatorSearch = string.Empty;
    private EntryToggleFilter _enabledFilter;
    private EntryAuthoringFilter _authoringFilter;
    private EntryWarningFilter _warningFilter;
    private int _verticalFilterIndex;
    private int _horizontalFilterIndex;
    private int _motionFilterIndex;
    private int _familyFilterIndex;
    private int _shapeFilterIndex;
    private readonly List<int> _filteredEntryIndices = new List<int>();
    private readonly Dictionary<int, CoverageEntrySummary> _coverageSummaries = new Dictionary<int, CoverageEntrySummary>();
    private TrickPoseEditorAnalysis.EntryReport[] _overlapReports = System.Array.Empty<TrickPoseEditorAnalysis.EntryReport>();
    private int _cachedOverlapDirtyCount = -1;
    private int _cachedCoverageSummaryHash = -1;

    private static readonly float Line = EditorGUIUtility.singleLineHeight;
    private const float VSpace = 2f;

    private void OnEnable()
    {
        _entriesProperty = serializedObject.FindProperty("entries");
        _defaultBlendInSpeedProperty = serializedObject.FindProperty("defaultBlendInSpeed");
        _defaultBlendOutSpeedProperty = serializedObject.FindProperty("defaultBlendOutSpeed");
        _previewLerpSpeedProperty = serializedObject.FindProperty("previewLerpSpeed");
        _rigAssistSettingsProperty = serializedObject.FindProperty("rigAssistSettings");

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
        _entriesList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
        {
            serializedObject.ApplyModifiedProperties();
            TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
            TrickPoseEditorSession.SetSelectedEntry(profile, newIndex);
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
            list.index = profile.entries.Count - 1;
            TrickPoseEditorSession.RefreshPreview(true);
        };
        _entriesList.onRemoveCallback = list =>
        {
            DeleteEntryAtIndex(list.index);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        TrickPoseEditorSession.SetProfile((TrickPoseProfileSO)target);
        int selectedIndexAtStart = TrickPoseEditorSession.SelectedEntryIndex;
        string selectedPreviewSignatureBefore = BuildSelectedPreviewSignature(selectedIndexAtStart);
        int dirtyCount = EditorUtility.GetDirtyCount(target);
        if (_cachedOverlapDirtyCount != dirtyCount)
        {
            _overlapReports = TrickPoseEditorAnalysis.AnalyzeProfile((TrickPoseProfileSO)target);
            _cachedOverlapDirtyCount = dirtyCount;
        }

        RebuildFilteredEntryIndices();
        RefreshCoverageSummaryCache();

        DrawTopLevelSection(TopLevelSection.ProfileSettings, "Profile Settings", DrawProfileSettingsSection);
        DrawTopLevelSection(TopLevelSection.PreviewSceneAuthoring, "Preview & Scene Authoring", DrawPreviewSceneAuthoringPanel);
        DrawTopLevelSection(TopLevelSection.FocusedPose, "Focused Pose", DrawFocusedEntryEditor);
        DrawTopLevelSection(TopLevelSection.FocusedPoseActions, "Focused Pose Actions", DrawSelectedEntryToolbar);
        DrawTopLevelSection(TopLevelSection.AuthoringHelpers, "Authoring Helpers", DrawAuthoringHelpers);
        DrawTopLevelSection(TopLevelSection.AuthoredPoses, "Authored Trick Poses / Pose Navigator", DrawAuthoredPoseNavigator);

        string selectedPreviewSignatureAfter = BuildSelectedPreviewSignature(selectedIndexAtStart);
        bool applied = serializedObject.ApplyModifiedProperties();
        if (applied &&
            selectedIndexAtStart == TrickPoseEditorSession.SelectedEntryIndex &&
            selectedPreviewSignatureBefore != selectedPreviewSignatureAfter &&
            IsValidEntryIndex(selectedIndexAtStart))
        {
            if (TrickPoseEditorSession.PreviewMatchConditions)
                TrickPoseEditorSession.RefreshSelectedEntryMatchPreview(true);
            else
                TrickPoseEditorSession.RefreshPreview(true);
        }
    }

    private void DrawProfileSettingsSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(_defaultBlendInSpeedProperty);
            EditorGUILayout.PropertyField(_defaultBlendOutSpeedProperty);
            EditorGUILayout.PropertyField(_previewLerpSpeedProperty);
        }
    }

    private void DrawPreviewSceneAuthoringPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Target / Scene Mode", EditorStyles.miniBoldLabel);
            SkiController previewTarget = (SkiController)EditorGUILayout.ObjectField(TrickPoseEditorHelp.Label("Preview Target", "Profile.PreviewTarget"), TrickPoseEditorSession.PreviewTarget, typeof(SkiController), true);
            if (previewTarget != TrickPoseEditorSession.PreviewTarget)
                TrickPoseEditorSession.SetPreviewTarget(previewTarget);

            TrickPoseEditorSession.SceneEditMode = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Scene Edit Mode", "Profile.SceneEditMode"), TrickPoseEditorSession.SceneEditMode);
            TrickPoseEditorSession.SnapPreview = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Snap Preview", "Profile.SnapPreview"), TrickPoseEditorSession.SnapPreview);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Selected Entry Preview", EditorStyles.miniBoldLabel);
            DrawSelectedEntryPreviewControls();

            SerializedProperty showGuides = _rigAssistSettingsProperty.FindPropertyRelative("showLimbLengthGuidesInScene");
            if (showGuides != null)
                EditorGUILayout.PropertyField(showGuides, new GUIContent("Show Limb Length Guides In Scene"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Live Mirror Utilities", EditorStyles.miniBoldLabel);
            DrawLiveMirrorControls();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Preview Windows / Actions", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = TrickPoseEditorSession.PreviewTarget != null;
                if (GUILayout.Button("Restore True Base Pose"))
                    RestoreTrueBasePose();
                GUI.enabled = true;

                if (GUILayout.Button("Open Preview Window"))
                    TrickPosePreviewWindow.Open();

                if (GUILayout.Button("Open Coverage Window"))
                    TrickPoseCoverageWindow.Open();
            }

            if (TrickPoseEditorHelpState.ShowInlineHelp)
                EditorGUILayout.HelpBox("Restore True Base Pose uses the controller-owned authored skiing pose, not the current previewed rig state.", MessageType.Info);
        }
    }

    private void DrawSelectedEntryPreviewControls()
    {
        EditorGUI.BeginChangeCheck();
        TrickPoseEditorSession.PreviewMatchConditions = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Enable Selected Entry Preview", "Profile.MatchPreviewEnable"), TrickPoseEditorSession.PreviewMatchConditions);
        TrickPoseEditorSession.ShowSceneGizmos = EditorGUILayout.Toggle("Show Scene Gizmos", TrickPoseEditorSession.ShowSceneGizmos);
        if (TrickPoseEditorSession.PreviewMatchConditions)
        {
            TrickPoseEditorSession.MatchPreviewContextMode mode = TrickPoseEditorSession.SelectedMatchPreviewMode;
            mode = (TrickPoseEditorSession.MatchPreviewContextMode)EditorGUILayout.Popup(
                TrickPoseEditorHelp.Label("Preview Context", "Profile.MatchPreviewContext"),
                (int)mode,
                new[] { "Use Current Scene State", "Simulate Selected Entry" });
            TrickPoseEditorSession.SelectedMatchPreviewMode = mode;
        }
        bool changed = EditorGUI.EndChangeCheck();

        DrawSelectedEntryPreviewStatus();
        if (changed)
            TrickPoseEditorSession.RefreshPreview(true);
    }

    private void DrawSelectedEntryPreviewStatus()
    {
        TrickPoseEntry selected = TrickPoseEditorSession.SelectedEntry;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (selected == null || controller == null)
            return;

        TrickPoseEditorPreviewContext previewContext = TrickPoseEditorSession.GetSelectedMatchPreviewContext(controller);
        bool previewEnabled = TrickPoseEditorSession.PreviewMatchConditions && previewContext != null;
        bool simulateSelectedEntry = previewEnabled && TrickPoseEditorSession.SelectedMatchPreviewMode == TrickPoseEditorSession.MatchPreviewContextMode.SimulateSelectedEntry;
        string previewText = previewEnabled
            ? $"{(simulateSelectedEntry ? "Simulated Entry" : "Scene Context")}: {TrickPoseAuthoredStateFormatter.Format(previewContext)}"
            : $"Current: {TrickPoseAuthoredStateFormatter.Format(controller.CurrentPoseFamily, controller.CurrentPoseShape, controller.CurrentPoseVerticalOrientation, controller.CurrentPoseHorizontalOrientation, controller.CurrentPoseMotionState)}";
        EditorGUILayout.LabelField(previewText, EditorStyles.wordWrappedMiniLabel);
    }

    private void RestoreTrueBasePose()
    {
        if (TrickPoseEditorSession.PreviewTarget == null)
            return;

        TrickPoseEditorSession.RestorePresentation();
        TrickPoseEditorSession.ClearPreviewWindowSimulatedEntry();
        TrickPoseEditorSession.ClearActiveSimulatedContext();
        TrickPoseEditorSession.PreviewTarget.RestoreTrueDefaultPose(true);
        TrickPoseEditorSession.RefreshLimbLineVisual(TrickPoseEditorSession.PreviewTarget);
        SceneView.RepaintAll();
    }

    private void DrawAuthoringHelpers()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawRigAssistTools();
            DrawCoverageActions(TrickPoseEditorSession.SelectedEntry);
            DrawAdvancedPreviewBaselineTools();
            DrawInspectorHelp();

            _showContextDebugger = EditorGUILayout.Foldout(_showContextDebugger, "Context Debugger", true);
            if (_showContextDebugger)
                TrickPoseContextDebugger.Draw(TrickPoseEditorSession.PreviewTarget);
        }
    }

    private void DrawAdvancedPreviewBaselineTools()
    {
        _showAdvancedPreviewBaselineTools = EditorGUILayout.Foldout(_showAdvancedPreviewBaselineTools, "Advanced Preview Baseline Tools", true);
        if (!_showAdvancedPreviewBaselineTools)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox("Repair rebuilds the immutable base pose from the authored controller source, such as the prefab source for a scene instance. It does not use the current previewed rig transforms.", MessageType.Warning);
            GUI.enabled = TrickPoseEditorSession.PreviewTarget != null;
            if (GUILayout.Button("Repair Base Pose From Authored Source"))
            {
                Undo.RecordObject(TrickPoseEditorSession.PreviewTarget, "Repair Trick Pose Base Pose");
                TrickPoseEditorSession.PreviewTarget.RebuildTrueDefaultPoseFromAuthoredSource(restorePose: true);
                TrickPoseEditorSession.RefreshLimbLineVisual(TrickPoseEditorSession.PreviewTarget);
                EditorUtility.SetDirty(TrickPoseEditorSession.PreviewTarget);
                SceneView.RepaintAll();
            }
            GUI.enabled = true;
        }
    }

    private void DrawLiveMirrorControls()
    {
        _showLiveMirrorTools = EditorGUILayout.Foldout(_showLiveMirrorTools, TrickPoseEditorHelp.Label("Live Mirror Utilities", "Profile.MirrorUtilities"), true);
        if (!_showLiveMirrorTools)
            return;

        TrickPoseEditorSession.LiveMirrorEnabled = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Mirror Live Updates", "Profile.MirrorUtilities"), TrickPoseEditorSession.LiveMirrorEnabled);
        TrickPoseEditorSession.MirrorMode = (TrickPoseEditorSession.LiveMirrorMode)EditorGUILayout.EnumPopup(TrickPoseEditorHelp.Label("Mirror Mode", "Profile.MirrorUtilities"), TrickPoseEditorSession.MirrorMode);
        TrickPoseEditorSession.MirrorDirection = (TrickPoseEditorSession.LiveMirrorDirection)EditorGUILayout.EnumPopup(TrickPoseEditorHelp.Label("Mirror Direction", "Profile.MirrorUtilities"), TrickPoseEditorSession.MirrorDirection);

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
        {
            EditorGUILayout.HelpBox("Select a pose to access focused actions.", MessageType.Info);
            return;
        }

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
            DeleteEntryAtIndex(index);
        }

        DrawSelectedEntryRigAssistWarnings(entry);
    }

    private float GetElementHeight(int index)
    {
        return (Line * 2f) + 8f;
    }

    private void DrawElement(Rect rect, int index, bool active, bool focused)
    {
        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
        rect.y += 3f;
        rect.height = Line;

        SerializedProperty enabled = entry.FindPropertyRelative("enabled");
        SerializedProperty priority = entry.FindPropertyRelative("priority");
        SerializedProperty placeholder = entry.FindPropertyRelative("isCoveragePlaceholder");
        TrickPoseEditorAnalysis.EntryReport report = GetReport(index);
        CoverageEntrySummary coverage = GetCoverageSummary(index);
        bool isEnabled = enabled.boolValue;
        string tooltip = BuildNavigatorTooltip(index, GetEntryByIndex(index), report, coverage);

        Rect enabledRect = new Rect(rect.x, rect.y, 18f, Line);
        Rect indexRect = new Rect(enabledRect.xMax + 4f, rect.y, 32f, Line);
        Rect priorityRect = new Rect(rect.xMax - 58f, rect.y, 58f, Line);
        Rect markerRect = new Rect(priorityRect.x - 68f, rect.y, 64f, Line);
        Rect iconRect = new Rect(markerRect.x - 18f, rect.y, 16f, 16f);
        Rect labelRect = new Rect(indexRect.xMax + 4f, rect.y, Mathf.Max(80f, markerRect.x - indexRect.xMax - 8f), Line);

        EditorGUI.PropertyField(enabledRect, enabled, GUIContent.none);
        EditorGUI.LabelField(indexRect, $"#{index}");
        EditorGUI.LabelField(labelRect, new GUIContent(GetCompactEntryTitle(index, entry), tooltip));
        EditorGUI.LabelField(priorityRect, $"P {priority.intValue}", EditorStyles.miniLabel);
        EditorGUI.LabelField(markerRect, new GUIContent(BuildCompactMarkers(report, coverage, placeholder.boolValue, isEnabled), tooltip), EditorStyles.miniLabel);
        DrawCompactOverlapIcon(iconRect, report);

        rect.y += Line + 2f;
        Rect summaryRect = new Rect(labelRect.x, rect.y, rect.xMax - labelRect.x, Line);
        EditorGUI.LabelField(summaryRect, new GUIContent(BuildAuthoredStateSummary(entry), tooltip), EditorStyles.miniLabel);
    }

    private void DrawFocusedEntryEditor()
    {
        int index = TrickPoseEditorSession.SelectedEntryIndex;
        if (!IsValidEntryIndex(index))
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                EditorGUILayout.HelpBox("Select a pose from the list to edit it.", MessageType.Info);
            return;
        }

        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(GetEntryHeader(index), EditorStyles.boldLabel);
            DrawFocusedSection(index, EntrySection.General, "General", entry, GetGeneralHeight, DrawGeneral);
            DrawFocusedSection(index, EntrySection.MatchConditions, "Match Conditions", entry, GetMatchConditionsHeight, DrawFocusedMatchConditions);
            DrawFocusedSection(index, EntrySection.PerPartPose, "Per-Part Pose", entry, GetPerPartHeight, DrawPerPartPose);
            DrawFocusedSection(index, EntrySection.Transition, "Transition", entry, GetTransitionHeight, DrawTransition);
        }
    }

    private void DrawTopLevelSection(TopLevelSection section, string label, Action drawContent)
    {
        EditorGUILayout.Space();
        bool expanded = GetTopLevelExpanded(section);
        bool nextExpanded = EditorGUILayout.Foldout(expanded, label, true);
        if (nextExpanded != expanded)
            SetTopLevelExpanded(section, nextExpanded);

        if (!nextExpanded)
            return;

        drawContent?.Invoke();
    }

    private void DrawAuthoredPoseNavigator()
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawNavigatorToolbar(profile);
            DrawNavigatorFilters();
            DrawNavigatorList(profile);

            bool filtered = HasActiveNavigatorFilters();
            string maintenanceLabel = filtered
                ? "Full Reorderable List / Maintenance (clear filters to reorder)"
                : "Full Reorderable List / Maintenance";
            bool showMaintenance = EditorGUILayout.Foldout(GetTopLevelExpanded(TopLevelSection.FullReorderableMaintenance), maintenanceLabel, true);
            if (showMaintenance != GetTopLevelExpanded(TopLevelSection.FullReorderableMaintenance))
                SetTopLevelExpanded(TopLevelSection.FullReorderableMaintenance, showMaintenance);

            if (!showMaintenance)
                return;

            if (filtered)
                EditorGUILayout.HelpBox("Clear navigator filters to safely reorder the authored pose list.", MessageType.Info);

            _entriesList.draggable = !filtered;
            _entriesList.index = Mathf.Clamp(TrickPoseEditorSession.SelectedEntryIndex, -1, _entriesProperty.arraySize - 1);
            _entriesList.DoLayoutList();
        }
    }

    private void DrawNavigatorToolbar(TrickPoseProfileSO profile)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _navigatorSearch = EditorGUILayout.TextField("Search", _navigatorSearch);
            if (GUILayout.Button("Clear", GUILayout.Width(56f)))
                ClearNavigatorFilters();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Showing {_filteredEntryIndices.Count} / {profile.entries.Count} poses", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();

            GUI.enabled = _filteredEntryIndices.Count > 0;
            if (GUILayout.Button("Select Previous Match", GUILayout.Width(136f)))
                SelectRelativeNavigatorMatch(profile, -1);
            if (GUILayout.Button("Select Next Match", GUILayout.Width(120f)))
                SelectRelativeNavigatorMatch(profile, 1);
            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = IsValidEntryIndex(TrickPoseEditorSession.SelectedEntryIndex);
            if (GUILayout.Button("Jump To Selected", GUILayout.Width(120f)))
                JumpToSelected();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawNavigatorFilters()
    {
        _showNavigatorFilters = EditorGUILayout.Foldout(_showNavigatorFilters, "Filters", true);
        if (!_showNavigatorFilters)
            return;

        bool twoColumn = EditorGUIUtility.currentViewWidth >= 420f;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (twoColumn)
            {
                DrawFilterRow(
                    () => _enabledFilter = (EntryToggleFilter)EditorGUILayout.EnumPopup("Enabled", _enabledFilter),
                    () => _authoringFilter = (EntryAuthoringFilter)EditorGUILayout.EnumPopup("Type", _authoringFilter));
                DrawFilterRow(
                    () => _warningFilter = (EntryWarningFilter)EditorGUILayout.EnumPopup("Warnings", _warningFilter),
                    () => _verticalFilterIndex = DrawOptionalEnumPopup("Vertical", _verticalFilterIndex, Enum.GetNames(typeof(TrickPoseVerticalOrientationRequirement))));
                DrawFilterRow(
                    () => _horizontalFilterIndex = DrawOptionalEnumPopup("Horizontal", _horizontalFilterIndex, Enum.GetNames(typeof(TrickPoseHorizontalOrientationRequirement))),
                    () => _motionFilterIndex = DrawOptionalEnumPopup("Motion", _motionFilterIndex, Enum.GetNames(typeof(TrickPoseMotionStateRequirement))));
                DrawFilterRow(
                    () => _familyFilterIndex = DrawOptionalEnumPopup("Family", _familyFilterIndex, Enum.GetNames(typeof(SkiController.AerialPoseFamily))),
                    () => _shapeFilterIndex = DrawOptionalEnumPopup("Shape", _shapeFilterIndex, Enum.GetNames(typeof(SkiController.AerialPoseShape))));
            }
            else
            {
                _enabledFilter = (EntryToggleFilter)EditorGUILayout.EnumPopup("Enabled", _enabledFilter);
                _authoringFilter = (EntryAuthoringFilter)EditorGUILayout.EnumPopup("Type", _authoringFilter);
                _warningFilter = (EntryWarningFilter)EditorGUILayout.EnumPopup("Warnings", _warningFilter);
                _verticalFilterIndex = DrawOptionalEnumPopup("Vertical", _verticalFilterIndex, Enum.GetNames(typeof(TrickPoseVerticalOrientationRequirement)));
                _horizontalFilterIndex = DrawOptionalEnumPopup("Horizontal", _horizontalFilterIndex, Enum.GetNames(typeof(TrickPoseHorizontalOrientationRequirement)));
                _motionFilterIndex = DrawOptionalEnumPopup("Motion", _motionFilterIndex, Enum.GetNames(typeof(TrickPoseMotionStateRequirement)));
                _familyFilterIndex = DrawOptionalEnumPopup("Family", _familyFilterIndex, Enum.GetNames(typeof(SkiController.AerialPoseFamily)));
                _shapeFilterIndex = DrawOptionalEnumPopup("Shape", _shapeFilterIndex, Enum.GetNames(typeof(SkiController.AerialPoseShape)));
            }
        }
    }

    private void DrawNavigatorList(TrickPoseProfileSO profile)
    {
        if (_filteredEntryIndices.Count == 0)
        {
            EditorGUILayout.HelpBox("No authored poses match the current search and filters.", MessageType.Info);
            return;
        }

        for (int i = 0; i < _filteredEntryIndices.Count; i++)
        {
            int entryIndex = _filteredEntryIndices[i];
            TrickPoseEntry entry = profile.entries[entryIndex];
            CoverageEntrySummary coverage = GetCoverageSummary(entryIndex);
            TrickPoseEditorAnalysis.EntryReport report = GetReport(entryIndex);
            string markers = BuildCompactMarkers(report, coverage, entry != null && entry.isCoveragePlaceholder, entry != null && entry.enabled);
            string tooltip = BuildNavigatorTooltip(entryIndex, entry, report, coverage);
            bool selected = entryIndex == TrickPoseEditorSession.SelectedEntryIndex;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(selected ? "Selected" : "Select", GUILayout.Width(62f)))
                        SelectEntry(profile, entryIndex);

                    GUIContent title = new GUIContent($"{GetEntryLabel(entryIndex)}  {GetCompactEntryTitle(entryIndex, _entriesProperty.GetArrayElementAtIndex(entryIndex))}", tooltip);
                    GUILayout.Label(title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (!string.IsNullOrWhiteSpace(markers))
                        GUILayout.Label(new GUIContent(markers, tooltip), EditorStyles.miniBoldLabel, GUILayout.Width(110f));
                }

                EditorGUILayout.LabelField(BuildAuthoredStateSummary(_entriesProperty.GetArrayElementAtIndex(entryIndex)), EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private void RebuildFilteredEntryIndices()
    {
        _filteredEntryIndices.Clear();
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        if (profile == null || profile.entries == null)
            return;

        for (int i = 0; i < profile.entries.Count; i++)
        {
            TrickPoseEntry entry = profile.entries[i];
            if (entry == null || !EntryMatchesNavigatorFilters(i, entry))
                continue;

            _filteredEntryIndices.Add(i);
        }
    }

    private bool EntryMatchesNavigatorFilters(int index, TrickPoseEntry entry)
    {
        if (entry == null)
            return false;

        if (_enabledFilter == EntryToggleFilter.Enabled && !entry.enabled)
            return false;
        if (_enabledFilter == EntryToggleFilter.Disabled && entry.enabled)
            return false;
        if (_authoringFilter == EntryAuthoringFilter.Placeholder && !entry.isCoveragePlaceholder)
            return false;
        if (_authoringFilter == EntryAuthoringFilter.Authored && entry.isCoveragePlaceholder)
            return false;

        CoverageEntrySummary coverage = GetCoverageSummary(index);
        bool hasWarnings = HasWarningState(index, entry, coverage);
        if (_warningFilter == EntryWarningFilter.Warning && !hasWarnings)
            return false;
        if (_warningFilter == EntryWarningFilter.Clean && hasWarnings)
            return false;

        if (!MatchesOptionalFilter(entry.requiredVerticalOrientation, _verticalFilterIndex))
            return false;
        if (!MatchesOptionalFilter(entry.requiredHorizontalOrientation, _horizontalFilterIndex))
            return false;
        if (!MatchesOptionalFilter(entry.requiredMotionState, _motionFilterIndex))
            return false;
        if (!MatchesOptionalFilter(entry.requiredPoseFamily, _familyFilterIndex))
            return false;
        if (!MatchesOptionalFilter(entry.requiredPoseShape, _shapeFilterIndex))
            return false;

        if (string.IsNullOrWhiteSpace(_navigatorSearch))
            return true;

        string needle = _navigatorSearch.Trim();
        return Contains(entry.displayName, needle) ||
               Contains(entry.overridePoseLabel, needle) ||
               Contains(entry.requiredPoseName, needle) ||
               Contains(entry.GetSummary(), needle) ||
               Contains($"#{index}", needle) ||
               Contains(index.ToString(), needle) ||
               Contains(entry.requiredPoseFamily.ToString(), needle) ||
               Contains(entry.requiredPoseShape.ToString(), needle) ||
               Contains(entry.requiredVerticalOrientation.ToString(), needle) ||
               Contains(entry.requiredHorizontalOrientation.ToString(), needle) ||
               Contains(entry.requiredMotionState.ToString(), needle) ||
               Contains(entry.isCoveragePlaceholder ? "placeholder" : "authored", needle) ||
               Contains(entry.enabled ? "enabled" : "disabled", needle) ||
               Contains(BuildOverlapSearchText(GetReport(index), coverage), needle);
    }

    private void RefreshCoverageSummaryCache()
    {
        int hash = BuildCoverageSummaryHash();
        if (_cachedCoverageSummaryHash == hash)
            return;

        _cachedCoverageSummaryHash = hash;
        _coverageSummaries.Clear();

        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        List<TrickPoseCoverageSlot> slots = TrickPoseEditorSession.CoverageSlots;
        if (profile == null || profile.entries == null || slots == null || slots.Count == 0)
            return;

        for (int i = 0; i < profile.entries.Count; i++)
            _coverageSummaries[i] = new CoverageEntrySummary();

        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null)
                continue;

            if (slot.assignedEntryIndex >= 0 && slot.assignedEntryIndex < profile.entries.Count)
            {
                CoverageEntrySummary assignedSummary = GetCoverageSummary(slot.assignedEntryIndex);
                if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Covered)
                    assignedSummary.cleanOwnedCount++;

                if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Suppressed && slot.suppressedEntryIndices != null && slot.suppressedEntryIndices.Count > 0)
                {
                    assignedSummary.suppressesOtherCount++;
                    assignedSummary.suppressesOtherSlotLabels.Add(slot.shortLabel);
                    AppendNames(profile, slot.suppressedEntryIndices, assignedSummary.suppressesOtherNames, slot.assignedEntryIndex);
                }
            }

            if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Ambiguous)
            {
                List<int> ambiguousIndices = slot.ambiguousEntryIndices != null && slot.ambiguousEntryIndices.Count > 0
                    ? slot.ambiguousEntryIndices
                    : slot.candidateEntryIndices;
                for (int j = 0; j < ambiguousIndices.Count; j++)
                {
                    int entryIndex = ambiguousIndices[j];
                    if (!IsProfileEntryIndex(profile, entryIndex))
                        continue;

                    CoverageEntrySummary summary = GetCoverageSummary(entryIndex);
                    summary.ambiguousCount++;
                    summary.ambiguousSlotLabels.Add(slot.shortLabel);
                    AppendNames(profile, ambiguousIndices, summary.ambiguousNames, entryIndex);
                }
            }

            if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Suppressed)
            {
                List<int> suppressedIndices = slot.suppressedEntryIndices != null && slot.suppressedEntryIndices.Count > 0
                    ? slot.suppressedEntryIndices
                    : BuildFallbackSuppressedIndices(slot);

                for (int j = 0; j < suppressedIndices.Count; j++)
                {
                    int entryIndex = suppressedIndices[j];
                    if (!IsProfileEntryIndex(profile, entryIndex))
                        continue;

                    CoverageEntrySummary summary = GetCoverageSummary(entryIndex);
                    summary.suppressedByCount++;
                    summary.suppressedBySlotLabels.Add(slot.shortLabel);
                    string winnerName = GetEntryName(profile, slot.assignedEntryIndex);
                    if (!string.IsNullOrWhiteSpace(winnerName))
                        summary.suppressedByNames.Add(winnerName);
                }
            }
        }
    }

    private CoverageEntrySummary GetCoverageSummary(int index)
    {
        if (!_coverageSummaries.TryGetValue(index, out CoverageEntrySummary summary))
        {
            summary = new CoverageEntrySummary();
            _coverageSummaries[index] = summary;
        }

        return summary;
    }

    private void ClearNavigatorFilters()
    {
        _navigatorSearch = string.Empty;
        _enabledFilter = EntryToggleFilter.All;
        _authoringFilter = EntryAuthoringFilter.All;
        _warningFilter = EntryWarningFilter.All;
        _verticalFilterIndex = 0;
        _horizontalFilterIndex = 0;
        _motionFilterIndex = 0;
        _familyFilterIndex = 0;
        _shapeFilterIndex = 0;
    }

    private void SelectRelativeNavigatorMatch(TrickPoseProfileSO profile, int delta)
    {
        if (profile == null || _filteredEntryIndices.Count == 0)
            return;

        int selectedIndex = TrickPoseEditorSession.SelectedEntryIndex;
        int currentMatchIndex = _filteredEntryIndices.IndexOf(selectedIndex);
        if (currentMatchIndex < 0)
            currentMatchIndex = delta > 0 ? -1 : 0;

        int next = (currentMatchIndex + delta + _filteredEntryIndices.Count) % _filteredEntryIndices.Count;
        SelectEntry(profile, _filteredEntryIndices[next]);
    }

    private void JumpToSelected()
    {
        _entriesList.index = TrickPoseEditorSession.SelectedEntryIndex;
        GUI.FocusControl(null);
    }

    private void SelectEntry(TrickPoseProfileSO profile, int index)
    {
        serializedObject.ApplyModifiedProperties();
        TrickPoseEditorSession.SetSelectedEntry(profile, index);
        _entriesList.index = index;
        TrickPoseEditorSession.RefreshPreview(true);
    }

    private bool HasActiveNavigatorFilters()
    {
        return !string.IsNullOrWhiteSpace(_navigatorSearch) ||
               _enabledFilter != EntryToggleFilter.All ||
               _authoringFilter != EntryAuthoringFilter.All ||
               _warningFilter != EntryWarningFilter.All ||
               _verticalFilterIndex != 0 ||
               _horizontalFilterIndex != 0 ||
               _motionFilterIndex != 0 ||
               _familyFilterIndex != 0 ||
               _shapeFilterIndex != 0;
    }

    private static bool MatchesOptionalFilter<T>(T value, int filterIndex) where T : struct
    {
        if (filterIndex <= 0)
            return true;

        return EqualityComparer<T>.Default.Equals(value, (T)Enum.GetValues(typeof(T)).GetValue(filterIndex - 1));
    }

    private static int DrawOptionalEnumPopup(string label, int selectedIndex, string[] enumNames)
    {
        string[] display = new string[enumNames.Length + 1];
        display[0] = "All";
        for (int i = 0; i < enumNames.Length; i++)
            display[i + 1] = enumNames[i];
        return EditorGUILayout.Popup(label, selectedIndex, display);
    }

    private static void DrawFilterRow(Action left, Action right)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true)))
                left?.Invoke();
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true)))
                right?.Invoke();
        }
    }

    private bool GetTopLevelExpanded(TopLevelSection section)
    {
        bool defaultValue = section == TopLevelSection.FocusedPose || section == TopLevelSection.AuthoredPoses;
        return SessionState.GetBool(GetTopLevelKey(section), defaultValue);
    }

    private void SetTopLevelExpanded(TopLevelSection section, bool expanded)
    {
        SessionState.SetBool(GetTopLevelKey(section), expanded);
    }

    private string GetTopLevelKey(TopLevelSection section)
    {
        return $"TrickPoseProfileSOEditor.{target.GetInstanceID()}.TopLevel.{section}";
    }

    private string GetCoverageActionsKey()
    {
        return $"TrickPoseProfileSOEditor.{target.GetInstanceID()}.CoverageActions";
    }

    private int BuildCoverageSummaryHash()
    {
        unchecked
        {
            int hash = 17;
            List<TrickPoseCoverageSlot> slots = TrickPoseEditorSession.CoverageSlots;
            hash = (hash * 31) + (slots != null ? slots.Count : 0);
            if (slots == null)
                return hash;

            for (int i = 0; i < slots.Count; i++)
            {
                TrickPoseCoverageSlot slot = slots[i];
                if (slot == null)
                    continue;

                hash = (hash * 31) + (int)slot.validationStatus;
                hash = (hash * 31) + slot.assignedEntryIndex;
                hash = (hash * 31) + CountOf(slot.ambiguousEntryIndices);
                hash = (hash * 31) + CountOf(slot.suppressedEntryIndices);
                hash = (hash * 31) + CountOf(slot.candidateEntryIndices);
            }

            return hash;
        }
    }

    private static int CountOf<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }

    private static bool IsProfileEntryIndex(TrickPoseProfileSO profile, int index)
    {
        return profile != null && profile.entries != null && index >= 0 && index < profile.entries.Count;
    }

    private static string GetEntryName(TrickPoseProfileSO profile, int index)
    {
        return IsProfileEntryIndex(profile, index) ? GetEntryName(profile.entries[index]) : string.Empty;
    }

    private static string GetEntryName(TrickPoseEntry entry)
    {
        return entry != null ? entry.GetSummary() : string.Empty;
    }

    private static void AppendNames(TrickPoseProfileSO profile, List<int> indices, HashSet<string> names, int excludedIndex)
    {
        if (profile == null || indices == null || names == null)
            return;

        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            if (index == excludedIndex)
                continue;

            string name = GetEntryName(profile, index);
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
    }

    private static List<int> BuildFallbackSuppressedIndices(TrickPoseCoverageSlot slot)
    {
        List<int> indices = new List<int>();
        if (slot == null || slot.candidateEntryIndices == null)
            return indices;

        for (int i = 0; i < slot.candidateEntryIndices.Count; i++)
        {
            int index = slot.candidateEntryIndices[i];
            if (index != slot.assignedEntryIndex)
                indices.Add(index);
        }

        return indices;
    }

    private static string BuildExampleList(HashSet<string> names)
    {
        if (names == null || names.Count == 0)
            return "none";

        List<string> values = new List<string>(names);
        values.Sort(StringComparer.OrdinalIgnoreCase);
        if (values.Count > 3)
            return $"{values[0]}, {values[1]}, {values[2]}";
        return string.Join(", ", values);
    }

    private void DrawFocusedMatchConditions(ref Rect rect, SerializedProperty entry)
    {
        DrawMatchConditions(ref rect, entry, TrickPoseEditorSession.SelectedEntryIndex);
    }

    private delegate float EntryHeightGetter(SerializedProperty entry);
    private delegate void EntrySectionDrawer(ref Rect rect, SerializedProperty entry);

    private void DrawFocusedSection(
        int index,
        EntrySection section,
        string label,
        SerializedProperty entry,
        EntryHeightGetter getHeight,
        EntrySectionDrawer drawSection)
    {
        bool expanded = GetSectionExpanded(index, section);
        Rect foldoutRect = EditorGUILayout.GetControlRect(false, Line);
        bool nextExpanded = EditorGUI.Foldout(foldoutRect, expanded, label, true);
        if (nextExpanded != expanded)
            SetSectionExpanded(index, section, nextExpanded);

        if (!nextExpanded)
            return;

        float height = getHeight(entry);
        Rect contentRect = EditorGUILayout.GetControlRect(false, height);
        contentRect.x += 12f;
        contentRect.width -= 12f;
        drawSection(ref contentRect, entry);
    }

    private void DrawGeneral(ref Rect rect, SerializedProperty entry)
    {
        DrawProperty(ref rect, entry.FindPropertyRelative("enabled"));
        DrawProperty(ref rect, entry.FindPropertyRelative("displayName"));
        DrawAuthoredStateSummary(ref rect, entry);
        DrawProperty(ref rect, entry.FindPropertyRelative("priority"));
        DrawProperty(ref rect, entry.FindPropertyRelative("overallWeight"));
        DrawProperty(ref rect, entry.FindPropertyRelative("overridePoseLabel"));
    }

    private void DrawAuthoredStateSummary(ref Rect rect, SerializedProperty entry)
    {
        Rect boxRect = new Rect(rect.x, rect.y, rect.width, (Line + VSpace) * 2.4f);
        GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

        Rect row = new Rect(boxRect.x + 6f, boxRect.y + 4f, boxRect.width - 12f, Line);
        EditorGUI.LabelField(row, "Authored Slot Identity", EditorStyles.boldLabel);
        row.y += Line + VSpace;
        EditorGUI.LabelField(row, BuildAuthoredStateSummary(entry), EditorStyles.wordWrappedMiniLabel);

        rect.y += boxRect.height + VSpace;
    }

    private void DrawMatchConditions(ref Rect rect, SerializedProperty entry, int index)
    {
        DrawMiniHeader(ref rect, "Base Pose Entry Conditions");
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredPoseFamily"), TrickPoseEditorHelp.Label("Required Pose Family", "Profile.MatchFamily"));
        DrawPoseShapeField(ref rect, entry.FindPropertyRelative("requiredPoseShape"), TrickPoseEditorHelp.Label("Required Pose Shape", "Profile.MatchShape"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredVerticalOrientation"), new GUIContent(
            TrickPoseOrientationUtility.VerticalAxisLabel,
            "ChestDown: chest/front vector points downward. ChestUp: chest/front vector points upward. Inverted: character up vector points downward."));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredHorizontalOrientation"), new GUIContent(
            TrickPoseOrientationUtility.HorizontalAxisLabel,
            "Roll-only side state. LeftSide/RightSide describe body roll, not heading or travel direction."));
        DrawProperty(ref rect, entry.FindPropertyRelative("requiredMotionState"), new GUIContent("Required Motion State"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requireAirborne"), TrickPoseEditorHelp.Label("Require Airborne", "Profile.MatchAirborne"));
        DrawProperty(ref rect, entry.FindPropertyRelative("requirePoseButtonHeld"), TrickPoseEditorHelp.Label("Require Pose Button Held", "Profile.MatchPoseHeld"));

        bool showLegacyAdvanced = GetLegacyAdvancedExpanded(index);
        Rect foldoutRect = new Rect(rect.x, rect.y, rect.width, Line);
        bool nextExpanded = EditorGUI.Foldout(foldoutRect, showLegacyAdvanced, "Legacy / Advanced Gates", true);
        if (nextExpanded != showLegacyAdvanced)
        {
            SetLegacyAdvancedExpanded(index, nextExpanded);
            TrickPoseEditorSession.ShowLegacyAdvancedGates = nextExpanded;
        }
        rect.y += Line + VSpace;

        if (nextExpanded)
        {
            TrickPoseEditorSession.ShowLegacyAdvancedGates = true;
            DrawOrientationModifierField(ref rect, entry.FindPropertyRelative("requiredOrientationModifier"), new GUIContent("Legacy Orientation Modifier"));
            DrawProperty(ref rect, entry.FindPropertyRelative("requiredPoseName"), new GUIContent("Required Pose Name"));
            DrawRangeField(ref rect, entry.FindPropertyRelative("entryPitchAngleRange"), TrickPoseEditorHelp.Label("Entry Pitch Angle", "Profile.MatchEntryAngle"));
            DrawRangeField(ref rect, entry.FindPropertyRelative("entryYawAngleRange"), TrickPoseEditorHelp.Label("Entry Yaw Angle", "Profile.MatchEntryAngle"));
            DrawRangeField(ref rect, entry.FindPropertyRelative("entryRollAngleRange"), TrickPoseEditorHelp.Label("Entry Roll Angle", "Profile.MatchEntryAngle"));
            SerializedProperty advancedModifierGates = entry.FindPropertyRelative("useAdvancedModifierConditions");
            DrawProperty(ref rect, advancedModifierGates, TrickPoseEditorHelp.Label("Use Advanced Modifier Gates", "Profile.UseAdvancedModifierGates"));
            using (new EditorGUI.DisabledScope(!advancedModifierGates.boolValue))
            {
                DrawProperty(ref rect, entry.FindPropertyRelative("requiredSpinDirection"), TrickPoseEditorHelp.Label("Required Spin Direction", "Profile.MatchSpin"));
                DrawProperty(ref rect, entry.FindPropertyRelative("requiredFlipDirection"), TrickPoseEditorHelp.Label("Required Flip Direction", "Profile.MatchFlip"));
                DrawRangeField(ref rect, entry.FindPropertyRelative("yawAngularVelocityRange"), TrickPoseEditorHelp.Label("Yaw Angular Velocity", "Profile.MatchAngularRange"));
                DrawRangeField(ref rect, entry.FindPropertyRelative("pitchAngularVelocityRange"), TrickPoseEditorHelp.Label("Pitch Angular Velocity", "Profile.MatchAngularRange"));
                DrawRangeField(ref rect, entry.FindPropertyRelative("rollAngularVelocityRange"), TrickPoseEditorHelp.Label("Roll Angular Velocity", "Profile.MatchAngularRange"));
                DrawRangeField(ref rect, entry.FindPropertyRelative("totalAngularSpeedRange"), TrickPoseEditorHelp.Label("Total Angular Speed", "Profile.MatchAngularRange"));
            }

            Rect buttonRect = new Rect(rect.x, rect.y, rect.width, Line);
            if (GUI.Button(buttonRect, "Clear Legacy / Advanced Gates"))
            {
                Undo.RecordObject(target, "Clear Legacy / Advanced Gates");
                ResetLegacyAdvancedGates(GetEntryByIndex(index));
                EditorUtility.SetDirty(target);
            }
            rect.y += Line + VSpace;
        }

        DrawOverlapSummary(ref rect, index);
        if (index == TrickPoseEditorSession.SelectedEntryIndex)
            DrawCoverageFootprintSummary(ref rect);
    }

    private void DrawPerPartPose(ref Rect rect, SerializedProperty entry)
    {
        DrawProperty(ref rect, entry.FindPropertyRelative("bodyPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("headPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("leftSkiPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("rightSkiPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("leftPolePose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("rightPolePose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("leftElbowPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("rightElbowPose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("leftKneePose"), true);
        DrawProperty(ref rect, entry.FindPropertyRelative("rightKneePose"), true);
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

    private void DrawMiniHeader(ref Rect rect, string text)
    {
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, Line);
        EditorGUI.LabelField(headerRect, text, EditorStyles.boldLabel);
        rect.y += Line + VSpace;
    }

    private void DrawPoseShapeField(ref Rect rect, SerializedProperty property, GUIContent label)
    {
        Rect propertyRect = new Rect(rect.x, rect.y, rect.width, Line);
        SkiController.AerialPoseShape current = (SkiController.AerialPoseShape)property.enumValueIndex;
        GUIContent[] labels =
        {
            new GUIContent("None"),
            new GUIContent("Neutral"),
            new GUIContent("Compact"),
            new GUIContent("Forward Lean"),
            new GUIContent("Backward Lean")
        };
        int[] values =
        {
            (int)SkiController.AerialPoseShape.None,
            (int)SkiController.AerialPoseShape.Neutral,
            (int)SkiController.AerialPoseShape.Compact,
            (int)SkiController.AerialPoseShape.Driving,
            (int)SkiController.AerialPoseShape.LaidOut
        };
        int currentIndex = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == (int)current)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = EditorGUI.Popup(propertyRect, label, currentIndex, labels);
        property.enumValueIndex = values[Mathf.Clamp(nextIndex, 0, values.Length - 1)];
        rect.y += Line + VSpace;
    }

    private void DrawOrientationModifierField(ref Rect rect, SerializedProperty property, GUIContent label)
    {
        Rect propertyRect = new Rect(rect.x, rect.y, rect.width, Line);
        SkiController.AerialOrientationModifier current = (SkiController.AerialOrientationModifier)property.enumValueIndex;
        SkiController.AerialOrientationModifier[] values =
        {
            SkiController.AerialOrientationModifier.None,
            SkiController.AerialOrientationModifier.Switch,
            SkiController.AerialOrientationModifier.Inverted,
            SkiController.AerialOrientationModifier.OnSide,
            SkiController.AerialOrientationModifier.ChestDown,
            SkiController.AerialOrientationModifier.ChestUp,
            SkiController.AerialOrientationModifier.Sideways,
            SkiController.AerialOrientationModifier.Rising,
            SkiController.AerialOrientationModifier.Diving
        };
        GUIContent[] labels =
        {
            new GUIContent("None"),
            new GUIContent("Switch"),
            new GUIContent("Inverted"),
            new GUIContent("On Side"),
            new GUIContent("Chest Down"),
            new GUIContent("Chest Up"),
            new GUIContent("Travel Sideways (Legacy)"),
            new GUIContent("Motion Rising"),
            new GUIContent("Motion Diving")
        };

        int currentIndex = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == current)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = EditorGUI.Popup(propertyRect, label, currentIndex, labels);
        property.enumValueIndex = (int)values[Mathf.Clamp(nextIndex, 0, values.Length - 1)];
        rect.y += Line + VSpace;
    }

    private void DrawProperty(ref Rect rect, SerializedProperty property, GUIContent label, bool includeChildren = false)
    {
        float height = EditorGUI.GetPropertyHeight(property, includeChildren);
        Rect propertyRect = new Rect(rect.x, rect.y, rect.width, height);
        EditorGUI.PropertyField(propertyRect, property, label, includeChildren);
        rect.y += height + VSpace;
    }

    private void DrawRangeField(ref Rect rect, SerializedProperty rangeProperty, string label)
    {
        DrawRangeField(ref rect, rangeProperty, new GUIContent(label));
    }

    private void DrawRangeField(ref Rect rect, SerializedProperty rangeProperty, GUIContent label)
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
        return ((Line + VSpace) * 2.4f) + VSpace + SumHeights(
            entry.FindPropertyRelative("enabled"),
            entry.FindPropertyRelative("displayName"),
            entry.FindPropertyRelative("priority"),
            entry.FindPropertyRelative("overallWeight"),
            entry.FindPropertyRelative("overridePoseLabel"));
    }

    private float GetMatchConditionsHeight(SerializedProperty entry)
    {
        float height = (Line + VSpace) * 2f;
        height += SumHeights(
            entry.FindPropertyRelative("requiredPoseFamily"),
            entry.FindPropertyRelative("requiredPoseShape"),
            entry.FindPropertyRelative("requiredVerticalOrientation"),
            entry.FindPropertyRelative("requiredHorizontalOrientation"),
            entry.FindPropertyRelative("requiredMotionState"),
            entry.FindPropertyRelative("requireAirborne"),
            entry.FindPropertyRelative("requirePoseButtonHeld"));

        height += Line + VSpace;

        int index = GetEntryIndex(entry);
        if (GetLegacyAdvancedExpanded(index))
        {
            SerializedProperty advancedModifierGates = entry.FindPropertyRelative("useAdvancedModifierConditions");
            height += EditorGUI.GetPropertyHeight(entry.FindPropertyRelative("requiredOrientationModifier"), false) + VSpace;
            height += EditorGUI.GetPropertyHeight(entry.FindPropertyRelative("requiredPoseName"), false) + VSpace;
            height += (Line + VSpace) * 4f;
            height += EditorGUI.GetPropertyHeight(advancedModifierGates, false) + VSpace;
            height += EditorGUI.GetPropertyHeight(entry.FindPropertyRelative("requiredSpinDirection"), false) + VSpace;
            height += EditorGUI.GetPropertyHeight(entry.FindPropertyRelative("requiredFlipDirection"), false) + VSpace;
            height += (Line + VSpace) * 4f;
            height += Line + VSpace;
        }

        height += GetOverlapSummaryHeight(index);
        if (index == TrickPoseEditorSession.SelectedEntryIndex)
            height += GetCoverageFootprintSummaryHeight();

        return height;
    }

    private float GetPerPartHeight(SerializedProperty entry)
    {
        SerializedProperty[] properties =
        {
            entry.FindPropertyRelative("bodyPose"),
            entry.FindPropertyRelative("headPose"),
            entry.FindPropertyRelative("leftSkiPose"),
            entry.FindPropertyRelative("rightSkiPose"),
            entry.FindPropertyRelative("leftPolePose"),
            entry.FindPropertyRelative("rightPolePose"),
            entry.FindPropertyRelative("leftElbowPose"),
            entry.FindPropertyRelative("rightElbowPose"),
            entry.FindPropertyRelative("leftKneePose"),
            entry.FindPropertyRelative("rightKneePose")
        };

        float total = 0f;
        for (int i = 0; i < properties.Length; i++)
            total += EditorGUI.GetPropertyHeight(properties[i], true) + VSpace;
        return total;
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
        CoverageEntrySummary coverage = GetCoverageSummary(index);
        string tooltip = BuildNavigatorTooltip(index, GetEntryByIndex(index), report, coverage);
        if (report == null || report.Severity == TrickPoseEditorAnalysis.OverlapSeverity.None)
            return new GUIContent(label, tooltip);

        string iconName = report.Severity switch
        {
            TrickPoseEditorAnalysis.OverlapSeverity.Error => "console.erroricon.sml",
            TrickPoseEditorAnalysis.OverlapSeverity.Warning => "console.warnicon.sml",
            _ => "console.infoicon.sml"
        };

        GUIContent content = EditorGUIUtility.IconContent(iconName);
        return new GUIContent(label, content.image, tooltip);
    }

    private string GetCompactEntryTitle(int index, SerializedProperty entry)
    {
        SerializedProperty displayName = entry.FindPropertyRelative("displayName");
        string title = displayName != null && !string.IsNullOrWhiteSpace(displayName.stringValue)
            ? displayName.stringValue
            : GetEntryLabel(index);

        return title;
    }

    private static string BuildCompactMarkers(TrickPoseEditorAnalysis.EntryReport report, CoverageEntrySummary coverage, bool isPlaceholder, bool isEnabled)
    {
        List<string> markers = new List<string>();
        if (!isEnabled)
            markers.Add("Disabled");
        if (isPlaceholder)
            markers.Add("Placeholder");
        if (coverage != null)
        {
            if (coverage.ambiguousCount > 0)
                markers.Add("A");
            if (coverage.suppressedByCount > 0)
                markers.Add("S\u2193");
            if (coverage.suppressesOtherCount > 0)
                markers.Add("S\u2191");
        }
        if (report != null && report.Severity != TrickPoseEditorAnalysis.OverlapSeverity.None)
            markers.Add(report.Severity == TrickPoseEditorAnalysis.OverlapSeverity.Error ? "Overlap!" : "Overlap");
        return string.Join(" ", markers);
    }

    private static void DrawCompactOverlapIcon(Rect rect, TrickPoseEditorAnalysis.EntryReport report)
    {
        if (report == null || report.Severity == TrickPoseEditorAnalysis.OverlapSeverity.None)
            return;

        string iconName = report.Severity switch
        {
            TrickPoseEditorAnalysis.OverlapSeverity.Error => "console.erroricon.sml",
            TrickPoseEditorAnalysis.OverlapSeverity.Warning => "console.warnicon.sml",
            _ => "console.infoicon.sml"
        };

        GUIContent icon = EditorGUIUtility.IconContent(iconName);
        if (icon != null && icon.image != null)
            GUI.Label(rect, new GUIContent(icon.image, report.Summary));
    }

    private static string BuildAuthoredStateSummary(SerializedProperty entry)
    {
        return $"{EnumPropertyName(entry, "requiredPoseFamily")} / {EnumPropertyName(entry, "requiredPoseShape")} | " +
               $"V: {EnumPropertyName(entry, "requiredVerticalOrientation")} | " +
               $"H: {EnumPropertyName(entry, "requiredHorizontalOrientation")} | " +
               $"Motion: {EnumPropertyName(entry, "requiredMotionState")}";
    }

    private bool HasWarningState(int index, TrickPoseEntry entry, CoverageEntrySummary coverage)
    {
        TrickPoseEditorAnalysis.EntryReport report = GetReport(index);
        return (report != null && report.Severity != TrickPoseEditorAnalysis.OverlapSeverity.None) ||
               (coverage != null && (coverage.ambiguousCount > 0 || coverage.suppressedByCount > 0 || coverage.suppressesOtherCount > 0)) ||
               (entry != null && (!entry.enabled || entry.isCoveragePlaceholder));
    }

    private static bool Contains(string haystack, string needle)
    {
        return !string.IsNullOrWhiteSpace(haystack) &&
               haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildOverlapSearchText(TrickPoseEditorAnalysis.EntryReport report, CoverageEntrySummary coverage)
    {
        List<string> labels = new List<string>();
        if (report != null && report.Severity != TrickPoseEditorAnalysis.OverlapSeverity.None)
        {
            labels.Add("overlap");
            labels.Add("warning");
        }
        if (coverage != null)
        {
            if (coverage.ambiguousCount > 0)
                labels.Add("ambiguous");
            if (coverage.suppressedByCount > 0)
                labels.Add("suppressed");
            if (coverage.suppressesOtherCount > 0)
                labels.Add("suppresses");
        }
        if (labels.Count == 0)
            labels.Add("clean");

        return string.Join(" ", labels);
    }

    private string BuildNavigatorTooltip(int index, TrickPoseEntry entry, TrickPoseEditorAnalysis.EntryReport report, CoverageEntrySummary coverage)
    {
        List<string> lines = new List<string>
        {
            $"{GetEntryLabel(index)} {GetEntryName(entry)}",
            entry != null ? BuildAuthoredStateSummary(_entriesProperty.GetArrayElementAtIndex(index)) : string.Empty
        };

        if (report != null && report.Severity != TrickPoseEditorAnalysis.OverlapSeverity.None)
            lines.Add(report.Summary);

        if (coverage != null)
        {
            if (coverage.ambiguousCount > 0)
                lines.Add($"Ambiguous: {coverage.ambiguousCount} ({BuildExampleList(coverage.ambiguousNames)})");
            if (coverage.suppressedByCount > 0)
                lines.Add($"Suppressed By: {coverage.suppressedByCount} ({BuildExampleList(coverage.suppressedByNames)})");
            if (coverage.suppressesOtherCount > 0)
                lines.Add($"Suppresses: {coverage.suppressesOtherCount} ({BuildExampleList(coverage.suppressesOtherNames)})");
        }

        return string.Join("\n", lines.FindAll(line => !string.IsNullOrWhiteSpace(line)));
    }

    private string BuildSelectedPreviewSignature(int selectedIndex)
    {
        if (!IsValidEntryIndex(selectedIndex))
            return string.Empty;

        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(selectedIndex);
        return string.Join("|",
            EnumValue(entry, "requiredPoseFamily"),
            EnumValue(entry, "requiredPoseShape"),
            EnumValue(entry, "requiredVerticalOrientation"),
            EnumValue(entry, "requiredHorizontalOrientation"),
            EnumValue(entry, "requiredMotionState"),
            StringValue(entry, "requiredPoseName"),
            EnumValue(entry, "requireAirborne"),
            EnumValue(entry, "requirePoseButtonHeld"),
            EnumValue(entry, "requiredOrientationModifier"),
            RangeSignature(entry, "entryPitchAngleRange"),
            RangeSignature(entry, "entryYawAngleRange"),
            RangeSignature(entry, "entryRollAngleRange"));
    }

    private bool IsValidEntryIndex(int index)
    {
        return _entriesProperty != null && index >= 0 && index < _entriesProperty.arraySize;
    }

    private static int EnumValue(SerializedProperty entry, string propertyName)
    {
        SerializedProperty property = entry.FindPropertyRelative(propertyName);
        return property != null ? property.enumValueIndex : -1;
    }

    private static string StringValue(SerializedProperty entry, string propertyName)
    {
        SerializedProperty property = entry.FindPropertyRelative(propertyName);
        return property != null ? property.stringValue ?? string.Empty : string.Empty;
    }

    private static string RangeSignature(SerializedProperty entry, string propertyName)
    {
        SerializedProperty property = entry.FindPropertyRelative(propertyName);
        if (property == null)
            return string.Empty;

        SerializedProperty enabled = property.FindPropertyRelative("enabled");
        SerializedProperty range = property.FindPropertyRelative("range");
        return $"{(enabled != null && enabled.boolValue)}:{(range != null ? range.vector2Value.ToString("0.###") : string.Empty)}";
    }

    private static string EnumPropertyName(SerializedProperty entry, string propertyName)
    {
        SerializedProperty property = entry.FindPropertyRelative(propertyName);
        if (property == null || property.enumNames == null || property.enumNames.Length == 0)
            return "(none)";

        int index = Mathf.Clamp(property.enumValueIndex, 0, property.enumNames.Length - 1);
        return property.enumDisplayNames != null && property.enumDisplayNames.Length > index
            ? property.enumDisplayNames[index]
            : property.enumNames[index];
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
            EditorGUI.HelpBox(boxRect, $"{report.Summary}{BuildOverlapGuidance(report)}", type);
        else
            EditorGUI.LabelField(boxRect, $"{report.Summary}{BuildOverlapGuidance(report)}");

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

    private void DrawCoverageFootprintSummary(ref Rect rect)
    {
        TrickPoseEntry selected = TrickPoseEditorSession.SelectedEntry;
        List<TrickPoseCoverageSlot> slots = TrickPoseEditorSession.CoverageSlots;
        if (selected == null || slots == null || slots.Count == 0)
            return;

        int selectedIndex = TrickPoseEditorSession.SelectedEntryIndex;
        List<TrickPoseCoverageSlot> primary = TrickPoseCoveragePlanBuilder.CollectSlotsForEntry(slots, selected, true);
        List<TrickPoseCoverageSlot> all = TrickPoseCoveragePlanBuilder.CollectSlotsForEntry(slots, selected, false);
        int spill = Mathf.Max(0, all.Count - primary.Count);
        CoverageEntrySummary coverage = GetCoverageSummary(selectedIndex);

        Rect boxRect = new Rect(rect.x, rect.y, rect.width, (Line + VSpace) * 6.7f);
        GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

        Rect row = new Rect(boxRect.x + 6f, boxRect.y + 6f, boxRect.width - 12f, Line);
        EditorGUI.LabelField(row, "Coverage Footprint", EditorStyles.boldLabel);
        row.y += Line + VSpace;
        EditorGUI.LabelField(row, $"Clean Owned Cells: {coverage.cleanOwnedCount}");
        row.y += Line + VSpace;
        EditorGUI.LabelField(row, $"Ambiguous Cells: {coverage.ambiguousCount}  ({BuildExampleList(coverage.ambiguousNames)})", EditorStyles.wordWrappedMiniLabel);
        row.y += Line + VSpace;
        EditorGUI.LabelField(row, $"Suppressed Cells: {coverage.suppressedByCount}  ({BuildExampleList(coverage.suppressedByNames)})", EditorStyles.wordWrappedMiniLabel);
        row.y += Line + VSpace;
        EditorGUI.LabelField(row, $"Suppresses Others: {coverage.suppressesOtherCount}  ({BuildExampleList(coverage.suppressesOtherNames)})", EditorStyles.wordWrappedMiniLabel);
        row.y += Line + VSpace;
        EditorGUI.LabelField(row, spill > 0
            ? $"Overlapping / Extra Cells: {spill} - review this entry for spill beyond its intended slot ownership."
            : $"Overlapping / Extra Cells: 0 - ownership looks focused across {primary.Count} primary matches.", EditorStyles.wordWrappedMiniLabel);
        rect.y += boxRect.height + VSpace;
    }

    private float GetCoverageFootprintSummaryHeight()
    {
        return ((Line + VSpace) * 6.7f) + VSpace;
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

    private static string BuildOverlapGuidance(TrickPoseEditorAnalysis.EntryReport report)
    {
        if (report == null)
            return string.Empty;
        if (report.blockedBy.Count > 0)
            return " Review priority/specificity or narrow this entry so it stops being fully blocked.";
        if (report.ambiguousOverlapCount > 0)
            return " Resolve ties by tightening conditions or adjusting priority.";
        if (report.blocksCount > 0)
            return " Check that this broader entry is intentionally shadowing those neighbors.";
        if (report.partials.Count > 0)
            return " Review spill and decide whether the overlap is intentional.";
        return " No immediate overlap action needed.";
    }

    private static string SelectedValueLabel(TrickPoseAngularVelocityRange range, float current)
    {
        if (!range.enabled)
            return current.ToString("0.#");

        Vector2 sorted = range.GetSortedRange();
        return $"{(sorted.x + sorted.y) * 0.5f:0.#} (range {sorted.x:0.#}..{sorted.y:0.#})";
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
            TrickPoseEditorSession.CopyPairedPose(entry.leftElbowPose, entry.rightElbowPose);
            TrickPoseEditorSession.CopyPairedPose(entry.leftKneePose, entry.rightKneePose);
        }
        else
        {
            TrickPoseEditorSession.CopyPairedPose(entry.rightSkiPose, entry.leftSkiPose);
            TrickPoseEditorSession.CopyPairedPose(entry.rightPolePose, entry.leftPolePose);
            TrickPoseEditorSession.CopyPairedPose(entry.rightElbowPose, entry.leftElbowPose);
            TrickPoseEditorSession.CopyPairedPose(entry.rightKneePose, entry.leftKneePose);
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
        copy.isCoveragePlaceholder = false;
        copy.coverageSlotId = string.Empty;
        if (flipped)
            FlipEntry(copy);

        Undo.RecordObject(target, "Duplicate Trick Pose");
        profile.entries.Insert(sourceIndex + 1, copy);
        EditorUtility.SetDirty(target);
        serializedObject.Update();
        TrickPoseEditorSession.SetSelectedEntry(profile, sourceIndex + 1);
        _entriesList.index = sourceIndex + 1;
        TrickPoseEditorSession.RefreshPreview(true);
    }

    private void DeleteEntryAtIndex(int index)
    {
        if (index < 0 || index >= _entriesProperty.arraySize)
            return;

        Undo.RecordObject(target, "Delete Trick Pose");
        _entriesProperty.DeleteArrayElementAtIndex(index);
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();

        int nextIndex = _entriesProperty.arraySize > 0
            ? Mathf.Clamp(index, 0, _entriesProperty.arraySize - 1)
            : -1;
        _entriesList.index = nextIndex;
        TrickPoseEditorSession.SetSelectedEntry((TrickPoseProfileSO)target, nextIndex);
        EditorUtility.SetDirty(target);
        TrickPoseEditorSession.RefreshPreview(true);
    }

    private string GetEntryLabel(int index)
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        if (profile.entries == null || index < 0 || index >= profile.entries.Count || profile.entries[index] == null)
            return $"Entry {index}";

        TrickPoseEntry entry = profile.entries[index];
        string state = entry.enabled ? "On" : "Off";
        string placeholder = entry.isCoveragePlaceholder ? " | Placeholder" : string.Empty;
        return $"{state} | P{entry.priority}{placeholder} | {entry.GetSummary()}";
    }

    private static void ResetEntry(TrickPoseEntry entry)
    {
        entry.enabled = true;
        entry.displayName = "New Trick Pose";
        entry.priority = 0;
        entry.overallWeight = 1f;
        entry.overridePoseLabel = string.Empty;
        entry.isCoveragePlaceholder = false;
        entry.coverageSlotId = string.Empty;
        entry.requiredPoseFamily = SkiController.AerialPoseFamily.None;
        entry.requiredPoseShape = SkiController.AerialPoseShape.None;
        entry.requiredVerticalOrientation = TrickPoseVerticalOrientationRequirement.Any;
        entry.requiredHorizontalOrientation = TrickPoseHorizontalOrientationRequirement.Any;
        entry.requiredMotionState = TrickPoseMotionStateRequirement.Any;
        entry.requiredOrientationModifier = SkiController.AerialOrientationModifier.None;
        entry.requiredPoseName = string.Empty;
        entry.requireAirborne = TrickPoseBoolRequirement.Ignore;
        entry.requirePoseButtonHeld = TrickPoseBoolRequirement.Ignore;
        entry.useAdvancedModifierConditions = false;
        entry.requiredSpinDirection = TrickPoseSpinDirectionRequirement.Any;
        entry.requiredFlipDirection = TrickPoseFlipDirectionRequirement.Any;
        entry.yawAngularVelocityRange = default;
        entry.pitchAngularVelocityRange = default;
        entry.rollAngularVelocityRange = default;
        entry.totalAngularSpeedRange = default;
        entry.entryPitchAngleRange = default;
        entry.entryYawAngleRange = default;
        entry.entryRollAngleRange = default;
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
        entry.leftElbowPose.Reset();
        entry.rightElbowPose.Reset();
        entry.leftKneePose.Reset();
        entry.rightKneePose.Reset();
    }

    private static void FlipEntry(TrickPoseEntry entry)
    {
        SwapAndFlip(ref entry.leftSkiPose, ref entry.rightSkiPose);
        SwapAndFlip(ref entry.leftPolePose, ref entry.rightPolePose);
        SwapAndFlip(ref entry.leftElbowPose, ref entry.rightElbowPose);
        SwapAndFlip(ref entry.leftKneePose, ref entry.rightKneePose);
        FlipSingle(entry.bodyPose);
        FlipSingle(entry.headPose);

        if (entry.requiredPoseFamily == SkiController.AerialPoseFamily.Left)
            entry.requiredPoseFamily = SkiController.AerialPoseFamily.Right;
        else if (entry.requiredPoseFamily == SkiController.AerialPoseFamily.Right)
            entry.requiredPoseFamily = SkiController.AerialPoseFamily.Left;
    }

    private void DrawCoverageActions(TrickPoseEntry entry)
    {
        TrickPoseCoverageSlot slot = TrickPoseEditorSession.SelectedCoverageSlot;
        if (slot == null)
            return;

        EditorGUILayout.Space();
        string foldoutLabel = string.IsNullOrWhiteSpace(slot.shortLabel)
            ? "Coverage Slot Actions"
            : $"Coverage Slot Actions ({slot.shortLabel})";
        bool expanded = SessionState.GetBool(GetCoverageActionsKey(), true);
        bool nextExpanded = EditorGUILayout.Foldout(expanded, foldoutLabel, true);
        if (nextExpanded != expanded)
            SessionState.SetBool(GetCoverageActionsKey(), nextExpanded);
        if (!nextExpanded)
        {
            EditorGUILayout.LabelField(slot.slotId, EditorStyles.wordWrappedMiniLabel);
            return;
        }

        if (TrickPoseEditorHelpState.ShowInlineHelp)
            EditorGUILayout.HelpBox(TrickPoseEditorHelp.GetCoverageActionHelp(), MessageType.None);
        EditorGUILayout.LabelField(slot.slotId, EditorStyles.wordWrappedMiniLabel);

        if (GUILayout.Button(TrickPoseEditorHelp.Button("Profile.CoverageActions", "Assign Entry To Selected Slot")))
        {
            TrickPoseCoverageAssignmentUtility.AssignEntryToSlot((TrickPoseProfileSO)target, slot, TrickPoseEditorSession.SelectedEntryIndex);
            TrickPoseEditorSession.RefreshCoverageWorkspace(3);
            TrickPoseEditorSession.RefreshPreview(true);
        }

        if (GUILayout.Button(TrickPoseEditorHelp.Button("Profile.CoverageActions", "Adopt Selected Slot Conditions")))
        {
            Undo.RecordObject(target, "Adopt Coverage Slot Conditions");
            TrickPoseCoverageAssignmentUtility.ApplySlotConditionsToEntry(slot, entry);
            EditorUtility.SetDirty(target);
            TrickPoseEditorSession.RefreshCoverageWorkspace(3);
            TrickPoseEditorSession.RefreshPreview(true);
        }

        if (GUILayout.Button(TrickPoseEditorHelp.Button("Profile.CoverageActions", "Replace Placeholder Assignment")))
        {
            TrickPoseCoverageAssignmentUtility.ReplacePlaceholderWithEntry((TrickPoseProfileSO)target, slot, TrickPoseEditorSession.SelectedEntryIndex);
            TrickPoseEditorSession.RefreshCoverageWorkspace(3);
            TrickPoseEditorSession.RefreshPreview(true);
        }
    }

    private void DrawInspectorHelp()
    {
        TrickPoseEditorHelpState.InspectorHelpExpanded = EditorGUILayout.Foldout(TrickPoseEditorHelpState.InspectorHelpExpanded, "Inspector Help", true);
        if (!TrickPoseEditorHelpState.InspectorHelpExpanded)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(TrickPoseEditorHelp.GetInspectorHelp(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Direct authoring here is best for final entry details, priorities, overlap cleanup, and pose data. Use slots when you want to reason from intended coverage first.", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private bool GetLegacyAdvancedExpanded(int index)
    {
        return SessionState.GetBool(GetLegacyAdvancedKey(index), false);
    }

    private void SetLegacyAdvancedExpanded(int index, bool expanded)
    {
        SessionState.SetBool(GetLegacyAdvancedKey(index), expanded);
    }

    private string GetLegacyAdvancedKey(int index)
    {
        return $"TrickPoseProfileSOEditor.{target.GetInstanceID()}.LegacyAdvanced.{index}";
    }

    private TrickPoseEntry GetEntryByIndex(int index)
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        if (profile == null || profile.entries == null || index < 0 || index >= profile.entries.Count)
            return null;

        return profile.entries[index];
    }

    private static void ResetLegacyAdvancedGates(TrickPoseEntry entry)
    {
        if (entry == null)
            return;

        entry.requiredOrientationModifier = SkiController.AerialOrientationModifier.None;
        entry.requiredPoseName = string.Empty;
        entry.entryPitchAngleRange = default;
        entry.entryYawAngleRange = default;
        entry.entryRollAngleRange = default;
        entry.useAdvancedModifierConditions = false;
        entry.requiredSpinDirection = TrickPoseSpinDirectionRequirement.Any;
        entry.requiredFlipDirection = TrickPoseFlipDirectionRequirement.Any;
        entry.yawAngularVelocityRange = default;
        entry.pitchAngularVelocityRange = default;
        entry.rollAngularVelocityRange = default;
        entry.totalAngularSpeedRange = default;
    }

    private void DrawRigAssistTools()
    {
        _showRigAssistTools = EditorGUILayout.Foldout(_showRigAssistTools, "Limb Length Guides", true);
        if (!_showRigAssistTools)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawRigAssistSettingsFields();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = TrickPoseEditorSession.PreviewTarget != null;
                if (GUILayout.Button("Capture Rest Segment Lengths"))
                    CaptureRigAssistRestPose();
                if (GUILayout.Button("Validate Selected Pose"))
                    ValidateSelectedPose();
                GUI.enabled = true;
            }

        }
    }

    private void DrawRigAssistSettingsFields()
    {
        EditorGUILayout.LabelField("Limb Length Guides", EditorStyles.boldLabel);
        SerializedProperty tolerance = _rigAssistSettingsProperty.FindPropertyRelative("segmentLengthTolerance");
        SerializedProperty restSegments = _rigAssistSettingsProperty.FindPropertyRelative("restSegments");
        SerializedProperty restCaptured = restSegments.FindPropertyRelative("captured");

        EditorGUILayout.PropertyField(tolerance, new GUIContent("Segment Length Tolerance"));

        EditorGUILayout.Space();
        if (!restCaptured.boolValue)
        {
            EditorGUILayout.LabelField("Rest Segment Lengths", "Not captured");
            EditorGUILayout.HelpBox("Capture rest segment lengths before validating or showing limb length guides.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Rest Segment Lengths", "Captured");
        DrawRestLength(restSegments, "Left Upper Arm", "leftUpperArm");
        DrawRestLength(restSegments, "Left Lower Arm", "leftLowerArm");
        DrawRestLength(restSegments, "Right Upper Arm", "rightUpperArm");
        DrawRestLength(restSegments, "Right Lower Arm", "rightLowerArm");
        DrawRestLength(restSegments, "Left Upper Leg", "leftUpperLeg");
        DrawRestLength(restSegments, "Left Lower Leg", "leftLowerLeg");
        DrawRestLength(restSegments, "Right Upper Leg", "rightUpperLeg");
        DrawRestLength(restSegments, "Right Lower Leg", "rightLowerLeg");
    }

    private static void DrawRestLength(SerializedProperty restSegments, string label, string propertyName)
    {
        SerializedProperty property = restSegments.FindPropertyRelative(propertyName);
        EditorGUILayout.LabelField(label, $"{property.floatValue:0.###} m");
    }

    private void CaptureRigAssistRestPose()
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (profile == null || controller == null)
            return;

        controller.EnsureTrueDefaultRigSnapshotCaptured();
        TrickPoseRigSnapshot defaultSnapshot = controller.CaptureTrueDefaultRigSnapshot();
        TrickPoseRigAssistReferenceData referenceData = controller.BuildRigAssistCaptureReferenceData();

        Undo.RecordObject(profile, "Capture Rig Assist Rest Pose");
        TrickPoseRigAssistUtility.CaptureRestPose(defaultSnapshot, referenceData, profile.rigAssistSettings);
        EditorUtility.SetDirty(profile);
        serializedObject.Update();
    }

    private bool HasCapturedRigAssistData()
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        return profile != null &&
               profile.rigAssistSettings != null &&
               profile.rigAssistSettings.restSegments.captured;
    }

    private void DrawSelectedEntryRigAssistWarnings(TrickPoseEntry entry)
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        if (entry == null || profile == null || controller == null || !HasCapturedRigAssistData())
            return;

        TrickPoseRigSnapshot snapshot = controller.CreateRigSnapshotFromEntry(entry);
        TrickPoseRigAssistReferenceData referenceData = controller.BuildRigAssistReferenceData();
        List<TrickPoseRigAssistValidationMessage> messages = TrickPoseRigAssistUtility.ValidateSnapshot(snapshot, referenceData, profile.rigAssistSettings);
        if (messages.Count == 0)
            return;

        EditorGUILayout.HelpBox($"Rig Assist found {messages.Count} validation warning(s) on this pose.", MessageType.Warning);
        for (int i = 0; i < messages.Count; i++)
            EditorGUILayout.LabelField($"- {FormatRigAssistMessage(messages[i], profile.rigAssistSettings)}", EditorStyles.wordWrappedMiniLabel);
    }

    private void ValidateSelectedPose()
    {
        TrickPoseProfileSO profile = (TrickPoseProfileSO)target;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        TrickPoseEntry entry = TrickPoseEditorSession.SelectedEntry;
        if (entry == null || profile == null || controller == null)
            return;

        if (!HasCapturedRigAssistData())
        {
            Debug.LogWarning("Capture rest segment lengths before validating trick pose limb lengths.");
            return;
        }

        TrickPoseRigSnapshot snapshot = controller.CreateRigSnapshotFromEntry(entry);
        TrickPoseRigAssistReferenceData referenceData = controller.BuildRigAssistReferenceData();
        List<TrickPoseRigAssistValidationMessage> messages = TrickPoseRigAssistUtility.ValidateSnapshot(snapshot, referenceData, profile.rigAssistSettings);
        if (messages.Count == 0)
        {
            Debug.Log("Rig Assist validation passed for selected pose.");
            return;
        }

        List<string> lines = new List<string> { $"Rig Assist validation found {messages.Count} warning(s) on selected pose:" };
        for (int i = 0; i < messages.Count; i++)
            lines.Add(FormatRigAssistMessage(messages[i], profile.rigAssistSettings));
        Debug.LogWarning(string.Join("\n", lines));
    }

    private static string FormatRigAssistMessage(TrickPoseRigAssistValidationMessage message, TrickPoseRigAssistSettings settings)
    {
        return message.ToDisplayString(settings.segmentLengthTolerance);
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
