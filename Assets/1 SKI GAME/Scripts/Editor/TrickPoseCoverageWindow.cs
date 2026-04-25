using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class TrickPoseCoverageWindow : EditorWindow
{
    private enum EntrySidebarFilter
    {
        All,
        Ambiguous,
        Suppressed,
        SuppressesOthers
    }

    private sealed class EntryCoverageStatusSummary
    {
        public int ambiguousCount;
        public int suppressedByCount;
        public int suppressesOtherCount;
        public readonly HashSet<string> ambiguousSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressedBySlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressesOtherSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> ambiguousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressedByNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> suppressesOtherNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private const int SamplesPerStep = 128;
    private const float SidebarWidth = 330f;
    private const float InspectorWidth = 330f;

    [SerializeField] private TrickPoseCoverageSettings _settings = new TrickPoseCoverageSettings();
    [SerializeField] private TrickPoseCoveragePlanSO _coveragePlan;
    [SerializeField] private TrickPoseCoverageAtlasState _atlasState = new TrickPoseCoverageAtlasState();
    [SerializeField] private List<TrickPoseCoverageSlot> _slots = new List<TrickPoseCoverageSlot>();
    [SerializeField] private int _selectedSlotIndex = -1;
    [SerializeField] private int _selectedClusterIndex = -1;
    [SerializeField] private int _selectedSampleIndex = -1;
    [SerializeField] private bool _showAdvancedWorkspace;
    [SerializeField] private bool _showSettings;
    [SerializeField] private bool _showDiagnosticsSettings;
    [SerializeField] private bool _showLegend = true;
    [SerializeField] private bool _showHeaderPanel = true;
    [SerializeField] private bool _showFocusPanel = true;
    [SerializeField] private bool _showInspectorActions = true;
    [SerializeField] private Vector2 _entrySidebarScroll;
    [SerializeField] private Vector2 _atlasScroll;
    [SerializeField] private Vector2 _inspectorScroll;
    [SerializeField] private EntrySidebarFilter _entrySidebarFilter;

    private TrickPoseCoverageReport _report;
    private TrickPoseCoverageAnalysisJob _job;
    private string _statusMessage;
    private double _lastProgressRepaintTime;
    private readonly Dictionary<string, TrickPoseCoverageContextEvaluation> _evaluationCache = new Dictionary<string, TrickPoseCoverageContextEvaluation>();
    private readonly Dictionary<int, EntryCoverageStatusSummary> _entryCoverageSummaryCache = new Dictionary<int, EntryCoverageStatusSummary>();
    private int _lastPreviewedSlotIndex = -2;
    private int _entryCoverageSummaryHash = -1;
    private bool _editorUpdateRegistered;

    [MenuItem("Window/SkiGame/Trick Pose Coverage")]
    public static void Open()
    {
        GetWindow<TrickPoseCoverageWindow>("Trick Pose Coverage");
    }

    private void OnEnable()
    {
        TrickPoseEditorSession.SetCoverageWorkspace(_coveragePlan, _slots);
        _lastPreviewedSlotIndex = -2;
        SetEditorUpdateRegistration(_job != null);
    }

    private void OnDisable()
    {
        SetEditorUpdateRegistration(false);
        CancelAnalysis();
    }

    private void OnGUI()
    {
        TrickPoseProfileSO profile = TrickPoseEditorSession.ActiveProfile;
        SkiController controller = TrickPoseEditorSession.PreviewTarget;
        RefreshEntryCoverageSummaryCache(profile);

        DrawHeader(profile, controller);
        DrawWorkspaceToolbar(profile, controller);
        DrawAdvancedFoldouts(profile, controller);
        DrawWorkspace(profile);
    }

    private void DrawHeader(TrickPoseProfileSO profile, SkiController controller)
    {
        _showHeaderPanel = EditorGUILayout.Foldout(_showHeaderPanel, "Header", true);
        if (!_showHeaderPanel)
        {
            DrawCompactTopSummary(profile, compactHeaderOnly: true);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.ObjectField(TrickPoseEditorHelp.Label("Profile", "Coverage.ProfileField"), profile, typeof(TrickPoseProfileSO), false);
                    _coveragePlan = (TrickPoseCoveragePlanSO)EditorGUILayout.ObjectField(TrickPoseEditorHelp.Label("Coverage Plan", "Coverage.PlanField"), _coveragePlan, typeof(TrickPoseCoveragePlanSO), false);
                    EditorGUILayout.ObjectField(TrickPoseEditorHelp.Label("Preview Target", "Coverage.PreviewTargetField"), controller, typeof(SkiController), true);
                }

                GUILayout.FlexibleSpace();
                DrawHelpToolbar();
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);

            DrawOnboardingBanner(profile, controller);
        }
    }

    private void DrawWorkspaceToolbar(TrickPoseProfileSO profile, SkiController controller)
    {
        _showFocusPanel = EditorGUILayout.Foldout(_showFocusPanel, "Atlas Toolbar", true);
        if (!_showFocusPanel)
        {
            DrawCompactTopSummary(profile, compactHeaderOnly: false);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Pose Coverage Atlas", EditorStyles.boldLabel);
            DrawSummaryCounts();

            if (_coveragePlan == null)
            {
                EditorGUILayout.HelpBox("Create or assign a coverage plan to build the pose coverage atlas.", MessageType.Info);
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Coverage.BuildSlots", "Create Coverage Plan Asset")))
                {
                    TrickPoseCoveragePlanSO asset = CreateInstance<TrickPoseCoveragePlanSO>();
                    string path = EditorUtility.SaveFilePanelInProject("Create Coverage Plan", "TrickPoseCoveragePlan", "asset", "Choose where to save the new coverage plan.");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        AssetDatabase.CreateAsset(asset, path);
                        AssetDatabase.SaveAssets();
                        _coveragePlan = asset;
                    }
                    else
                    {
                        DestroyImmediate(asset);
                    }
                }

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Slot Atlas"))
                    BuildSlots(controller, profile);

                GUI.enabled = profile != null && _coveragePlan != null;
                if (GUILayout.Button(new GUIContent("Refresh / Repair Ownership", "Re-evaluates slot ownership and repairs missing slot preview contexts if needed.")))
                    RefreshSlots(profile);
                GUI.enabled = true;
            }

            DrawFocusToolbar();

            if (TrickPoseEditorHelpState.ShowInlineHelp)
                EditorGUILayout.HelpBox("Use the atlas as the authoring workspace: select a matrix slot, compare it against authored entries, then preview, create, clone, or apply conditions from the inspector.", MessageType.None);
        }
    }

    private void DrawCompactTopSummary(TrickPoseProfileSO profile, bool compactHeaderOnly)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            string profileName = profile != null ? profile.name : "No Profile";
            string planName = _coveragePlan != null ? _coveragePlan.name : "No Coverage Plan";
            GUILayout.Label($"{profileName} | {planName} | Covered {CountSlots(TrickPoseCoverageSlotValidationStatus.Covered)} | Ambiguous {CountSlots(TrickPoseCoverageSlotValidationStatus.Ambiguous)} | Suppressed {CountSlots(TrickPoseCoverageSlotValidationStatus.Suppressed)} | Gaps {CountSlots(TrickPoseCoverageSlotValidationStatus.Gap)}", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (!compactHeaderOnly)
            {
                if (GUILayout.Button("Build Slot Atlas", GUILayout.Width(100f)))
                    BuildSlots(TrickPoseEditorSession.PreviewTarget, profile);

                GUI.enabled = profile != null && _coveragePlan != null;
                if (GUILayout.Button(new GUIContent("Refresh / Repair Ownership", "Re-evaluates slot ownership and repairs missing slot preview contexts if needed."), GUILayout.Width(160f)))
                    RefreshSlots(profile);
                GUI.enabled = true;
            }
        }
    }

    private void DrawFocusToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Focus / Search", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _atlasState.verticalFilter = (TrickPoseVerticalOrientationRequirement)EditorGUILayout.EnumPopup("Vertical", _atlasState.verticalFilter, GUILayout.Width(240f));
                _atlasState.horizontalFilter = (TrickPoseHorizontalOrientationRequirement)EditorGUILayout.EnumPopup("Horizontal", _atlasState.horizontalFilter, GUILayout.Width(240f));
                _atlasState.motionFilter = (TrickPoseMotionStateRequirement)EditorGUILayout.EnumPopup("Motion", _atlasState.motionFilter, GUILayout.Width(220f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _atlasState.searchText = EditorGUILayout.TextField("Search", _atlasState.searchText);
                if (GUILayout.Button("Clear Focus", GUILayout.Width(100f)))
                    ClearFocus();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _atlasState.hideCleanMatrices = EditorGUILayout.ToggleLeft("Hide Clean Matrices", _atlasState.hideCleanMatrices, GUILayout.Width(170f));
                _atlasState.dimNonMatchingFocus = EditorGUILayout.ToggleLeft("Dim Non-Matching Focus", _atlasState.dimNonMatchingFocus, GUILayout.Width(190f));
                GUILayout.FlexibleSpace();
            }
        }
    }

    private void DrawAdvancedFoldouts(TrickPoseProfileSO profile, SkiController controller)
    {
        _showAdvancedWorkspace = EditorGUILayout.Foldout(_showAdvancedWorkspace, "Advanced", true);
        if (!_showAdvancedWorkspace)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings", true);
            if (_showSettings)
                _coveragePlan = (TrickPoseCoveragePlanSO)EditorGUILayout.ObjectField("Coverage Plan", _coveragePlan, typeof(TrickPoseCoveragePlanSO), false);

            _showDiagnosticsSettings = EditorGUILayout.Foldout(_showDiagnosticsSettings, "Sampled Diagnostics", true);
            if (_showDiagnosticsSettings)
            {
                DrawDiagnosticsSamplingControls(profile);
                DrawDiagnosticsSummary();
                DrawGapClusters(profile);
                DrawSelectedDiagnosticDetail(profile);
            }
        }
    }

    private void DrawWorkspace(TrickPoseProfileSO profile)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSidebar(profile);
            DrawAtlasPane();
            DrawInspectorPane(profile);
        }
    }

    private void DrawSidebar(TrickPoseProfileSO profile)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField("Authored Entries", EditorStyles.boldLabel);
            _entrySidebarFilter = (EntrySidebarFilter)EditorGUILayout.EnumPopup("Coverage Filter", _entrySidebarFilter);
            _entrySidebarScroll = EditorGUILayout.BeginScrollView(_entrySidebarScroll, GUILayout.ExpandHeight(true));
            DrawEntryList(profile);
            EditorGUILayout.Space(10f);
            DrawAmbiguityGroups(profile);
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawEntryList(TrickPoseProfileSO profile)
    {
        if (profile == null || profile.entries == null || profile.entries.Count == 0)
        {
            EditorGUILayout.LabelField("No authored pose entries.");
            return;
        }

        bool drewAny = false;
        for (int i = 0; i < profile.entries.Count; i++)
        {
            TrickPoseEntry entry = profile.entries[i];
            EntryCoverageStatusSummary status = GetEntryCoverageStatusSummary(i);
            if (entry == null || !EntryMatchesFocus(entry, status) || !EntryMatchesSidebarFilter(status))
                continue;

            drewAny = true;

            GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
            bool isSelected = TrickPoseEditorSession.SelectedEntryIndex == i;
            Color previousBackground = GUI.backgroundColor;
            if (isSelected)
                GUI.backgroundColor = new Color(0.44f, 0.60f, 0.84f);
            else if (!entry.enabled)
                GUI.backgroundColor = new Color(0.30f, 0.30f, 0.30f);
            else if (entry.isCoveragePlaceholder)
                GUI.backgroundColor = new Color(0.46f, 0.63f, 0.83f);

            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(isSelected ? "Selected" : "Select", GUILayout.Width(68f)))
                    {
                        TrickPoseEditorSession.SetSelectedEntry(profile, i);
                        Repaint();
                    }

                    GUILayout.Label(BuildEntryHeader(entry), EditorStyles.boldLabel);
                }

                EditorGUILayout.LabelField($"Family / Shape: {DescribeEntryFamilyShape(entry)}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"Vertical / Horizontal / Motion: {DescribeEntryOrientation(entry)}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"Airborne / PoseHeld: {DescribeBoolRequirement(entry.requireAirborne)} / {DescribeBoolRequirement(entry.requirePoseButtonHeld)}", EditorStyles.wordWrappedMiniLabel);

                string broad = BuildBroadConditionLabel(entry);
                if (!string.IsNullOrWhiteSpace(broad))
                    EditorGUILayout.LabelField($"Broad: {broad}", EditorStyles.wordWrappedMiniLabel);

                if (status.ambiguousCount > 0 || status.suppressedByCount > 0 || status.suppressesOtherCount > 0)
                {
                    string warningLine = $"Coverage: {status.ambiguousCount} ambiguous, {status.suppressedByCount} suppressed, suppresses {status.suppressesOtherCount}";
                    EditorGUILayout.LabelField(new GUIContent(warningLine, BuildEntryCoverageTooltip(status)), EditorStyles.wordWrappedMiniLabel);
                }
            }

            GUI.backgroundColor = previousBackground;
        }

        if (!drewAny)
            EditorGUILayout.LabelField("No authored entries match the current focus.");
    }

    private void DrawAmbiguityGroups(TrickPoseProfileSO profile)
    {
        List<CoverageAmbiguityGroup> groups = BuildAmbiguityGroups(profile);

        EditorGUILayout.LabelField("Ambiguity Groups", EditorStyles.boldLabel);
        if (groups.Count == 0)
        {
            EditorGUILayout.LabelField("No ambiguous or suppressed slot groups.");
            return;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            CoverageAmbiguityGroup group = groups[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Slot", GUILayout.Width(78f)))
                    {
                        _selectedSlotIndex = group.representativeSlotIndex;
                        SyncSelectedSlot();
                    }

                    GUI.enabled = profile != null && group.involvedIndices.Count > 0;
                    if (GUILayout.Button("Select Pose", GUILayout.Width(78f)))
                    {
                        TrickPoseEditorSession.SetSelectedEntry(profile, group.involvedIndices[0]);
                        Repaint();
                    }
                    GUI.enabled = true;

                    GUILayout.Label($"{group.statusLabel} x{group.slotIndices.Count}", EditorStyles.boldLabel);
                }

                EditorGUILayout.LabelField($"Representative: {group.representativeLabel}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"Involved: {group.candidateSummary}", EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrWhiteSpace(group.broadSegments))
                    EditorGUILayout.LabelField($"Collision-prone: {group.broadSegments}", EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private void DrawAtlasPane()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
        {
            _atlasState.selectedEntryIndex = TrickPoseEditorSession.SelectedEntryIndex;
            _atlasState.highlightSelectedEntry = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Atlas", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                _showLegend = EditorGUILayout.Foldout(_showLegend, "Legend", true);
            }

            if (_showLegend)
                DrawMatrixLegend();

            _atlasScroll = EditorGUILayout.BeginScrollView(_atlasScroll, GUILayout.ExpandHeight(true));
            _selectedSlotIndex = TrickPoseCoverageMatrixView.DrawAtlas(_slots, _coveragePlan, _atlasState, _selectedSlotIndex);
            EditorGUILayout.EndScrollView();
            SyncSelectedSlot();
        }
    }

    private void DrawInspectorPane(TrickPoseProfileSO profile)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(InspectorWidth), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll, GUILayout.ExpandHeight(true));
            DrawSelectedSlotDetail(profile);
            EditorGUILayout.Space(10f);
            DrawSelectedEntryDetail(profile);
            EditorGUILayout.Space(10f);
            DrawInspectorActions(profile);
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSelectedSlotDetail(TrickPoseProfileSO profile = null)
    {
        TrickPoseCoverageSlot slot = GetSelectedSlot();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Selected Slot", EditorStyles.boldLabel);
            if (slot == null)
            {
                EditorGUILayout.LabelField("Select a matrix cell to inspect it.");
                return;
            }

            EditorGUILayout.LabelField(slot.shortLabel, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", slot.validationStatus.ToString());
            EditorGUILayout.LabelField("Owner", slot.assignedEntry != null ? slot.assignedEntry.GetSummary() : "(none)");
            EditorGUILayout.LabelField("Vertical / Horizontal / Motion", $"{slot.verticalOrientation} / {slot.horizontalOrientation} / {slot.motionState}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Family / Shape", $"{slot.poseFamily} / {slot.poseShape}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Airborne / PoseHeld", $"{slot.airborne} / {slot.poseHeld}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Summary", slot.summary, EditorStyles.wordWrappedLabel);

            if (slot.candidateEntryLabels != null && slot.candidateEntryLabels.Count > 0)
                EditorGUILayout.LabelField("Candidates", string.Join(", ", slot.candidateEntryLabels), EditorStyles.wordWrappedMiniLabel);
            if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Ambiguous)
            {
                List<int> involved = slot.ambiguousEntryIndices != null && slot.ambiguousEntryIndices.Count > 0
                    ? slot.ambiguousEntryIndices
                    : slot.candidateEntryIndices;
                EditorGUILayout.LabelField("Ambiguous Between", BuildEntryListLabel(profile, involved), EditorStyles.wordWrappedMiniLabel);
            }
            else if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Suppressed)
            {
                EditorGUILayout.LabelField("Winning Pose", slot.assignedEntry != null ? slot.assignedEntry.GetSummary() : "(none)", EditorStyles.wordWrappedMiniLabel);
                List<int> suppressed = slot.suppressedEntryIndices != null && slot.suppressedEntryIndices.Count > 0
                    ? slot.suppressedEntryIndices
                    : BuildFallbackSuppressedIndices(slot);
                EditorGUILayout.LabelField("Suppressed Poses", BuildEntryListLabel(profile, suppressed), EditorStyles.wordWrappedMiniLabel);
            }
            if (!string.IsNullOrWhiteSpace(slot.exclusionReason))
                EditorGUILayout.LabelField("Exclusion", slot.exclusionReason, EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void DrawSelectedEntryDetail(TrickPoseProfileSO profile)
    {
        TrickPoseEntry entry = TrickPoseEditorSession.SelectedEntry;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Selected Entry", EditorStyles.boldLabel);
            if (profile == null || entry == null)
            {
                EditorGUILayout.LabelField("Select an authored entry from the sidebar.");
                return;
            }

            EditorGUILayout.LabelField(BuildEntryHeader(entry), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Family / Shape", DescribeEntryFamilyShape(entry), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Vertical / Horizontal / Motion", DescribeEntryOrientation(entry), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Airborne / PoseHeld", $"{DescribeBoolRequirement(entry.requireAirborne)} / {DescribeBoolRequirement(entry.requirePoseButtonHeld)}", EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrWhiteSpace(entry.requiredPoseName) || !string.IsNullOrWhiteSpace(entry.overridePoseLabel))
                EditorGUILayout.LabelField("Pose Labels", $"{entry.requiredPoseName} {entry.overridePoseLabel}".Trim(), EditorStyles.wordWrappedMiniLabel);

            string broad = BuildBroadConditionLabel(entry);
            if (!string.IsNullOrWhiteSpace(broad))
                EditorGUILayout.LabelField("Broad Conditions", broad, EditorStyles.wordWrappedMiniLabel);

            TrickPoseCoverageSlot slot = GetSelectedSlot();
            if (slot?.representativeContext != null)
            {
                List<TrickPoseEntryConditionStatus> statuses = TrickPoseEditorPreviewUtility.BuildConditionStatuses(
                    entry,
                    slot.representativeContext,
                    TrickPoseEditorSession.ShowLegacyAdvancedGates);
                EditorGUILayout.LabelField("Against Selected Slot", EditorStyles.miniBoldLabel);
                for (int i = 0; i < statuses.Count; i++)
                {
                    TrickPoseEntryConditionStatus status = statuses[i];
                    if (string.IsNullOrWhiteSpace(status.status))
                        continue;

                    EditorGUILayout.LabelField($"{status.label}: {status.status}", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }

    private void DrawInspectorActions(TrickPoseProfileSO profile)
    {
        TrickPoseCoverageSlot slot = GetSelectedSlot();
        TrickPoseEntry selectedEntry = TrickPoseEditorSession.SelectedEntry;
        int selectedEntryIndex = TrickPoseEditorSession.SelectedEntryIndex;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string foldoutLabel = slot != null ? $"Actions ({slot.shortLabel})" : "Actions";
            _showInspectorActions = EditorGUILayout.Foldout(_showInspectorActions, foldoutLabel, true);
            if (!_showInspectorActions)
                return;

            if (slot == null)
            {
                EditorGUILayout.LabelField("Select a slot to preview, create, clone, or apply.");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Preview.Mode.Coverage", "Preview Slot")))
                    TrickPosePreviewWindow.PreviewCoverageSlot(slot);

                GUI.enabled = profile != null && slot.assignedEntryIndex >= 0;
                if (GUILayout.Button("Open Owner"))
                {
                    TrickPoseEditorSession.SetSelectedEntry(profile, slot.assignedEntryIndex);
                    TrickPoseEditorSession.RefreshPreview(true);
                }
                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = profile != null;
                if (GUILayout.Button("Create Entry From Slot"))
                {
                    int index = TrickPoseCoverageAssignmentUtility.CreateEntryFromSlot(profile, slot, placeholder: false);
                    if (index >= 0)
                    {
                        TrickPoseEditorSession.SetSelectedEntry(profile, index);
                        RefreshSlots(profile);
                    }
                }

                GUI.enabled = profile != null && selectedEntryIndex >= 0;
                if (GUILayout.Button("Clone Selected Entry For Slot"))
                {
                    int index = TrickPoseCoverageAssignmentUtility.DuplicateEntryForSlot(profile, slot, selectedEntryIndex);
                    if (index >= 0)
                    {
                        TrickPoseEditorSession.SetSelectedEntry(profile, index);
                        RefreshSlots(profile);
                    }
                }
                GUI.enabled = true;
            }

            EditorGUILayout.HelpBox("Applying a matrix slot copies its base match conditions into the selected entry and clears advanced modifier gates plus entry angle ranges.", MessageType.Warning);

            GUI.enabled = profile != null && selectedEntry != null && selectedEntryIndex >= 0;
            if (GUILayout.Button("Apply Selected Matrix Slot To Selected Entry"))
            {
                TrickPoseCoverageAssignmentUtility.AssignEntryToSlot(profile, slot, selectedEntryIndex);
                RefreshSlots(profile);
            }
            GUI.enabled = true;
        }
    }

    private void DrawDiagnosticsSamplingControls(TrickPoseProfileSO profile)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int estimate = TrickPoseCoverageAnalyzer.EstimateSampleCount(_settings);
            EditorGUILayout.LabelField(TrickPoseEditorHelp.Label("Estimated Samples", "Coverage.AnalyzeSamples"), new GUIContent(estimate.ToString()));
            _settings.density = (TrickPoseCoverageSettings.SampleDensity)EditorGUILayout.EnumPopup(TrickPoseEditorHelp.Label("Sample Density", "Coverage.AnalyzeSamples"), _settings.density);
            _settings.airborneOnly = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Airborne Only", "Coverage.AnalyzeSamples"), _settings.airborneOnly);
            using (new EditorGUI.DisabledScope(_settings.airborneOnly))
                _settings.includeGroundedStates = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Include Grounded States", "Coverage.AnalyzeSamples"), _settings.includeGroundedStates);
            _settings.includeNoPoseInputStates = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Include No Pose Input States", "Coverage.AnalyzeSamples"), _settings.includeNoPoseInputStates);
            _settings.collapseInactiveStateVariants = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Collapse Inactive Variants", "Coverage.AnalyzeSamples"), _settings.collapseInactiveStateVariants);
            _settings.skipZeroAngularDuplicateStates = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Skip Zero-Angle Duplicates", "Coverage.AnalyzeSamples"), _settings.skipZeroAngularDuplicateStates);
            _settings.nearestEntryCount = Mathf.Clamp(EditorGUILayout.IntField(TrickPoseEditorHelp.Label("Nearest Entries", "Coverage.AnalyzeSamples"), _settings.nearestEntryCount), 1, 8);
            _settings.allowUnsafeSampleCount = EditorGUILayout.Toggle(TrickPoseEditorHelp.Label("Allow Unsafe Override", "Coverage.AnalyzeSamples"), _settings.allowUnsafeSampleCount);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _job == null && profile != null;
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Coverage.AnalyzeSamples", "Analyze Samples")))
                    BeginAnalysis(profile, TrickPoseEditorSession.PreviewTarget);
                GUI.enabled = _job != null;
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Coverage.CancelSamples", "Cancel")))
                    CancelAnalysis();
                GUI.enabled = _job == null && _report != null;
                if (GUILayout.Button(TrickPoseEditorHelp.Button("Coverage.ClearSamples", "Clear")))
                    ClearResults();
                GUI.enabled = true;
            }
        }
    }

    private void DrawDiagnosticsSummary()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (_report == null)
            {
                EditorGUILayout.LabelField("No cached sampled diagnostics.");
                return;
            }

            DrawCountLine("Estimated Samples", _report.estimatedSampleCount, 1f);
            DrawCountLine("Processed Samples", _report.totalSamples, Percentage(_report.totalSamples, _report.estimatedSampleCount));
            DrawCountLine("Covered", _report.coveredCount, Percentage(_report.coveredCount, _report.totalSamples));
            DrawCountLine("Ambiguous", _report.ambiguousCount, Percentage(_report.ambiguousCount, _report.totalSamples));
            DrawCountLine("Suppressed", _report.suppressedCount, Percentage(_report.suppressedCount, _report.totalSamples));
            DrawCountLine("Gaps", _report.gapCount, Percentage(_report.gapCount, _report.totalSamples));
        }
    }

    private void DrawGapClusters(TrickPoseProfileSO profile)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Sampled Gap Clusters", EditorStyles.boldLabel);
            if (_report == null || _report.gapClusters.Count == 0)
            {
                EditorGUILayout.LabelField("No cached gap clusters.");
                return;
            }

            for (int i = 0; i < _report.gapClusters.Count; i++)
            {
                TrickPoseCoverageGapCluster cluster = _report.gapClusters[i];
                bool selected = i == _selectedClusterIndex;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(selected ? "Selected" : "Inspect", GUILayout.Width(72f)))
                    {
                        _selectedClusterIndex = i;
                        _selectedSampleIndex = 0;
                    }

                    if (GUILayout.Button("Preview", GUILayout.Width(72f)))
                        TrickPosePreviewWindow.PushInfluenceState(cluster.representative.context);

                    GUILayout.Label($"{cluster.Count} samples  |  {cluster.traitSummary}", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }

    private void DrawSelectedDiagnosticDetail(TrickPoseProfileSO profile)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            TrickPoseCoverageGapCluster cluster = GetSelectedCluster();
            if (cluster == null)
            {
                EditorGUILayout.LabelField("Select a sampled cluster to inspect representative detail.");
                return;
            }

            TrickPoseEditorPreviewContext context = GetSelectedContext(cluster);
            TrickPoseCoverageContextEvaluation evaluation = GetOrBuildEvaluation(profile, context);
            EditorGUILayout.LabelField("Sample", TrickPoseCoverageAnalyzer.DescribeContext(context), EditorStyles.wordWrappedLabel);
            if (evaluation != null)
            {
                EditorGUILayout.LabelField("Classification", evaluation.classification.ToString());
                EditorGUILayout.LabelField("Summary", evaluation.Summary, EditorStyles.wordWrappedLabel);
            }
        }
    }

    private void DrawSummaryCounts()
    {
        int total = _slots.Count;
        int excluded = CountSlots(TrickPoseCoverageSlotValidationStatus.Excluded);
        int covered = CountSlots(TrickPoseCoverageSlotValidationStatus.Covered);
        int ambiguous = CountSlots(TrickPoseCoverageSlotValidationStatus.Ambiguous);
        int suppressed = CountSlots(TrickPoseCoverageSlotValidationStatus.Suppressed);
        int gaps = CountSlots(TrickPoseCoverageSlotValidationStatus.Gap);
        int placeholders = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].assignmentState == TrickPoseCoverageAssignmentState.AssignedToPlaceholder)
                placeholders++;
        }

        EditorGUILayout.LabelField("Intended Slots", (total - excluded).ToString());
        EditorGUILayout.LabelField("Covered Cleanly", covered.ToString());
        EditorGUILayout.LabelField("Ambiguous", ambiguous.ToString());
        EditorGUILayout.LabelField("Suppressed", suppressed.ToString());
        EditorGUILayout.LabelField("Gaps", gaps.ToString());
        EditorGUILayout.LabelField("Placeholders Remaining", placeholders.ToString());
    }

    private void BuildSlots(SkiController controller, TrickPoseProfileSO profile)
    {
        if (_coveragePlan == null)
        {
            _statusMessage = "Assign a coverage plan before building slots.";
            return;
        }

        string selectedSlotId = GetSelectedSlot() != null ? GetSelectedSlot().slotId : null;
        TrickPoseCoverageBuildResult result = TrickPoseCoveragePlanBuilder.BuildSlots(_coveragePlan, controller);
        _slots = result.slots;
        RestoreSelectedSlotById(selectedSlotId);
        RefreshSlots(profile, atlasRebuilt: true);
        _statusMessage = $"Rebuilt atlas and refreshed ownership for {_slots.Count} slots ({result.excludedCount} excluded).";
    }

    private void RefreshSlots(TrickPoseProfileSO profile)
    {
        RefreshSlots(profile, atlasRebuilt: false);
    }

    private void RefreshSlots(TrickPoseProfileSO profile, bool atlasRebuilt)
    {
        if (_coveragePlan == null)
        {
            _statusMessage = "Assign a coverage plan before refreshing ownership.";
            return;
        }

        if (_slots == null || _slots.Count == 0)
        {
            BuildSlots(TrickPoseEditorSession.PreviewTarget, profile);
            return;
        }

        string selectedSlotId = GetSelectedSlot() != null ? GetSelectedSlot().slotId : null;
        bool rebuiltContexts = TrickPoseCoveragePlanBuilder.RefreshSlotRepresentativeContexts(_coveragePlan, TrickPoseEditorSession.PreviewTarget, _slots);
        TrickPoseCoveragePlanBuilder.RefreshSlotAssignments(profile, _slots, _settings.nearestEntryCount);
        TrickPoseEditorSession.SetCoverageWorkspace(_coveragePlan, _slots);
        _lastPreviewedSlotIndex = -2;
        RestoreSelectedSlotById(selectedSlotId);
        SyncSelectedSlot();
        _statusMessage = atlasRebuilt || rebuiltContexts
            ? $"Rebuilt atlas and refreshed ownership for {_slots.Count} slots."
            : $"Refreshed ownership for {_slots.Count} slots.";
        Repaint();
    }

    private void RestoreSelectedSlotById(string slotId)
    {
        if (_slots == null || _slots.Count == 0)
        {
            _selectedSlotIndex = -1;
            return;
        }

        if (!string.IsNullOrWhiteSpace(slotId))
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null && string.Equals(_slots[i].slotId, slotId, StringComparison.Ordinal))
                {
                    _selectedSlotIndex = i;
                    return;
                }
            }
        }

        _selectedSlotIndex = Mathf.Clamp(_selectedSlotIndex, 0, _slots.Count - 1);
    }

    private void SyncSelectedSlot()
    {
        TrickPoseEditorSession.SetCoverageWorkspace(_coveragePlan, _slots);
        TrickPoseEditorSession.SetSelectedCoverageSlot(_selectedSlotIndex);
        if (TrickPoseEditorSession.PreviewMode == TrickPosePreviewMode.Coverage &&
            _lastPreviewedSlotIndex != _selectedSlotIndex)
        {
            TrickPosePreviewWindow.PreviewCoverageSlot(GetSelectedSlot());
            _lastPreviewedSlotIndex = _selectedSlotIndex;
        }
    }

    private TrickPoseCoverageSlot GetSelectedSlot()
    {
        if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _slots.Count)
            return null;
        return _slots[_selectedSlotIndex];
    }

    private int CountSlots(TrickPoseCoverageSlotValidationStatus status)
    {
        int count = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].validationStatus == status)
                count++;
        }
        return count;
    }

    private void OnEditorUpdate()
    {
        if (_job == null)
        {
            SetEditorUpdateRegistration(false);
            return;
        }

        bool complete = TrickPoseCoverageAnalyzer.ProcessJob(_job, SamplesPerStep);
        double now = EditorApplication.timeSinceStartup;
        if (complete)
        {
            _report = _job.report;
            _job = null;
            _selectedClusterIndex = _report.gapClusters.Count > 0 ? 0 : -1;
            _selectedSampleIndex = _selectedClusterIndex >= 0 ? 0 : -1;
            _evaluationCache.Clear();
            _statusMessage = $"Sampled analysis complete. Cached {_report.totalSamples} states.";
            SetEditorUpdateRegistration(false);
            Repaint();
            return;
        }

        if (now - _lastProgressRepaintTime > 0.15d)
        {
            _statusMessage = $"Analyzing sampled coverage... {_job.nextIndex}/{_job.contexts.Count}";
            _lastProgressRepaintTime = now;
            Repaint();
        }
    }

    private void BeginAnalysis(TrickPoseProfileSO profile, SkiController controller)
    {
        CancelAnalysis();
        if (profile == null)
        {
            _statusMessage = "Assign a profile before running sampled diagnostics.";
            return;
        }

        _job = TrickPoseCoverageAnalyzer.CreateJob(profile, controller, _settings);
        _lastProgressRepaintTime = 0d;
        _evaluationCache.Clear();
        _statusMessage = $"Sampled analysis started for {_job.contexts.Count} states.";
        SetEditorUpdateRegistration(true);
    }

    private void CancelAnalysis()
    {
        if (_job == null)
            return;

        TrickPoseCoverageAnalyzer.CancelJob(_job);
        _job = null;
        SetEditorUpdateRegistration(false);
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

    private void ClearResults()
    {
        _report = null;
        _selectedClusterIndex = -1;
        _selectedSampleIndex = -1;
        _evaluationCache.Clear();
    }

    private void ClearFocus()
    {
        _atlasState.verticalFilter = TrickPoseVerticalOrientationRequirement.Any;
        _atlasState.horizontalFilter = TrickPoseHorizontalOrientationRequirement.Any;
        _atlasState.motionFilter = TrickPoseMotionStateRequirement.Any;
        _atlasState.searchText = string.Empty;
    }

    private TrickPoseCoverageGapCluster GetSelectedCluster()
    {
        if (_report == null || _selectedClusterIndex < 0 || _selectedClusterIndex >= _report.gapClusters.Count)
            return null;
        return _report.gapClusters[_selectedClusterIndex];
    }

    private TrickPoseEditorPreviewContext GetSelectedContext(TrickPoseCoverageGapCluster cluster)
    {
        if (cluster == null || cluster.samples.Count == 0)
            return null;
        _selectedSampleIndex = Mathf.Clamp(_selectedSampleIndex, 0, cluster.samples.Count - 1);
        return cluster.samples[_selectedSampleIndex];
    }

    private TrickPoseCoverageContextEvaluation GetOrBuildEvaluation(TrickPoseProfileSO profile, TrickPoseEditorPreviewContext context)
    {
        if (profile == null || context == null)
            return null;

        string key = string.Join("|",
            context.airborne ? "air" : "ground",
            context.poseInputHeld ? "pose" : "nopose",
            context.yawAngularVelocity.ToString("0.###"),
            context.pitchAngularVelocity.ToString("0.###"),
            context.rollAngularVelocity.ToString("0.###"));
        if (_evaluationCache.TryGetValue(key, out TrickPoseCoverageContextEvaluation cached))
            return cached;

        TrickPoseCoverageContextEvaluation evaluation = TrickPoseCoverageAnalyzer.EvaluateContext(profile, context, _settings.nearestEntryCount);
        _evaluationCache[key] = evaluation;
        return evaluation;
    }

    private List<CoverageAmbiguityGroup> BuildAmbiguityGroups(TrickPoseProfileSO profile)
    {
        Dictionary<string, CoverageAmbiguityGroup> groups = new Dictionary<string, CoverageAmbiguityGroup>(StringComparer.Ordinal);
        for (int i = 0; i < _slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = _slots[i];
            if (slot == null ||
                (slot.validationStatus != TrickPoseCoverageSlotValidationStatus.Ambiguous &&
                 slot.validationStatus != TrickPoseCoverageSlotValidationStatus.Suppressed))
            {
                continue;
            }

            if (!TrickPoseCoverageMatrixView.MatchesFocus(slot, _atlasState))
                continue;

            List<int> keys = slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Ambiguous
                ? (slot.ambiguousEntryIndices != null && slot.ambiguousEntryIndices.Count > 0 ? slot.ambiguousEntryIndices : slot.candidateEntryIndices)
                : (slot.suppressedEntryIndices != null && slot.suppressedEntryIndices.Count > 0 ? slot.suppressedEntryIndices : BuildFallbackSuppressedIndices(slot));
            string groupKey = $"{slot.validationStatus}:{BuildKey(keys)}";
            if (!groups.TryGetValue(groupKey, out CoverageAmbiguityGroup group))
            {
                group = new CoverageAmbiguityGroup
                {
                    representativeSlotIndex = i,
                    representativeLabel = slot.shortLabel,
                    statusLabel = slot.validationStatus.ToString()
                };
                group.involvedIndices.AddRange(keys);
                groups[groupKey] = group;
            }

            group.slotIndices.Add(i);
            group.candidateSummary = BuildEntryListLabel(profile, keys);
            group.broadSegments = BuildCandidateBroadSummary(profile, slot);
        }

        List<CoverageAmbiguityGroup> result = new List<CoverageAmbiguityGroup>(groups.Values);
        result.Sort((a, b) => b.slotIndices.Count.CompareTo(a.slotIndices.Count));
        return result;
    }

    private bool EntryMatchesFocus(TrickPoseEntry entry, EntryCoverageStatusSummary status)
    {
        if (entry == null)
            return false;

        if (_atlasState.verticalFilter != TrickPoseVerticalOrientationRequirement.Any &&
            entry.requiredVerticalOrientation != TrickPoseVerticalOrientationRequirement.Any &&
            entry.requiredVerticalOrientation != _atlasState.verticalFilter)
        {
            return false;
        }

        if (_atlasState.horizontalFilter != TrickPoseHorizontalOrientationRequirement.Any &&
            entry.requiredHorizontalOrientation != TrickPoseHorizontalOrientationRequirement.Any &&
            entry.requiredHorizontalOrientation != _atlasState.horizontalFilter)
        {
            return false;
        }

        if (_atlasState.motionFilter != TrickPoseMotionStateRequirement.Any &&
            entry.requiredMotionState != TrickPoseMotionStateRequirement.Any &&
            entry.requiredMotionState != _atlasState.motionFilter)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_atlasState.searchText))
            return true;

        string needle = _atlasState.searchText.Trim();
        return Contains(entry.GetSummary(), needle) ||
               Contains(entry.displayName, needle) ||
               Contains(entry.overridePoseLabel, needle) ||
               Contains(entry.requiredPoseName, needle) ||
               Contains(DescribeEntryFamilyShape(entry), needle) ||
               Contains(DescribeEntryOrientation(entry), needle) ||
               Contains(BuildBroadConditionLabel(entry), needle) ||
               Contains(entry.enabled ? "enabled" : "disabled", needle) ||
               Contains(entry.isCoveragePlaceholder ? "placeholder" : "authored", needle) ||
               Contains(BuildEntryCoverageSearchText(status), needle);
    }

    private static string BuildEntryHeader(TrickPoseEntry entry)
    {
        if (entry == null)
            return "(null)";

        string label = entry.GetSummary();
        if (entry.isCoveragePlaceholder)
            label += " [Placeholder]";
        if (!entry.enabled)
            label += " [Disabled]";
        return label;
    }

    private static string DescribeEntryFamilyShape(TrickPoseEntry entry)
    {
        return $"{DescribeEnum(entry.requiredPoseFamily, SkiController.AerialPoseFamily.None)} / {DescribeEnum(entry.requiredPoseShape, SkiController.AerialPoseShape.None)}";
    }

    private static string DescribeEntryOrientation(TrickPoseEntry entry)
    {
        return $"{DescribeEnum(entry.requiredVerticalOrientation, TrickPoseVerticalOrientationRequirement.Any)} / {DescribeEnum(entry.requiredHorizontalOrientation, TrickPoseHorizontalOrientationRequirement.Any)} / {DescribeEnum(entry.requiredMotionState, TrickPoseMotionStateRequirement.Any)}";
    }

    private static string DescribeBoolRequirement(TrickPoseBoolRequirement requirement)
    {
        return requirement switch
        {
            TrickPoseBoolRequirement.True => "True",
            TrickPoseBoolRequirement.False => "False",
            _ => "Ignore"
        };
    }

    private static string DescribeEnum<T>(T value, T broadValue)
    {
        return EqualityComparer<T>.Default.Equals(value, broadValue) ? "Any" : value.ToString();
    }

    private static string BuildBroadConditionLabel(TrickPoseEntry entry)
    {
        if (entry == null)
            return string.Empty;

        List<string> labels = new List<string>();
        if (entry.requiredPoseFamily == SkiController.AerialPoseFamily.None)
            labels.Add("Family=None");
        if (entry.requiredPoseShape == SkiController.AerialPoseShape.None)
            labels.Add("Shape=None");
        if (entry.requiredVerticalOrientation == TrickPoseVerticalOrientationRequirement.Any)
            labels.Add("Vertical=Any");
        if (entry.requiredHorizontalOrientation == TrickPoseHorizontalOrientationRequirement.Any)
            labels.Add("Horizontal=Any");
        if (entry.requiredMotionState == TrickPoseMotionStateRequirement.Any)
            labels.Add("Motion=Any");
        if (entry.requireAirborne == TrickPoseBoolRequirement.Ignore)
            labels.Add("Airborne=Ignore");
        if (entry.requirePoseButtonHeld == TrickPoseBoolRequirement.Ignore)
            labels.Add("PoseHeld=Ignore");
        if (!entry.useAdvancedModifierConditions)
            labels.Add("AdvancedGates=Off");
        if (!entry.entryPitchAngleRange.enabled && !entry.entryYawAngleRange.enabled && !entry.entryRollAngleRange.enabled)
            labels.Add("EntryAngles=Ignore");

        return string.Join(", ", labels);
    }

    private static string BuildCandidateSummary(TrickPoseProfileSO profile, TrickPoseCoverageSlot slot)
    {
        List<string> labels = new List<string>();
        List<int> indices = slot.candidateEntryIndices;
        for (int i = 0; i < indices.Count; i++)
        {
            TrickPoseEntry entry = ResolveEntry(profile, indices[i]);
            if (entry != null)
                labels.Add(entry.GetSummary());
        }

        return labels.Count > 0 ? string.Join(", ", labels) : "(no candidates)";
    }

    private static string BuildCandidateBroadSummary(TrickPoseProfileSO profile, TrickPoseCoverageSlot slot)
    {
        List<string> broad = new List<string>();
        List<int> indices = slot.candidateEntryIndices;
        for (int i = 0; i < indices.Count; i++)
        {
            TrickPoseEntry entry = ResolveEntry(profile, indices[i]);
            string broadLabel = BuildBroadConditionLabel(entry);
            if (!string.IsNullOrWhiteSpace(broadLabel))
                broad.Add($"{entry.GetSummary()} -> {broadLabel}");
        }

        return string.Join(" | ", broad);
    }

    private void RefreshEntryCoverageSummaryCache(TrickPoseProfileSO profile)
    {
        int hash = BuildEntryCoverageSummaryHash();
        if (_entryCoverageSummaryHash == hash)
            return;

        _entryCoverageSummaryHash = hash;
        _entryCoverageSummaryCache.Clear();
        if (profile == null || profile.entries == null)
            return;

        for (int i = 0; i < profile.entries.Count; i++)
            _entryCoverageSummaryCache[i] = new EntryCoverageStatusSummary();

        for (int i = 0; i < _slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = _slots[i];
            if (slot == null)
                continue;

            if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Ambiguous)
            {
                List<int> ambiguous = slot.ambiguousEntryIndices != null && slot.ambiguousEntryIndices.Count > 0
                    ? slot.ambiguousEntryIndices
                    : slot.candidateEntryIndices;
                for (int j = 0; j < ambiguous.Count; j++)
                {
                    int entryIndex = ambiguous[j];
                    if (!IsValidEntryIndex(profile, entryIndex))
                        continue;

                    EntryCoverageStatusSummary summary = GetEntryCoverageStatusSummary(entryIndex);
                    summary.ambiguousCount++;
                    summary.ambiguousSlots.Add(slot.shortLabel);
                    AppendEntryNames(profile, ambiguous, summary.ambiguousNames, entryIndex);
                }
            }

            if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Suppressed)
            {
                List<int> suppressed = slot.suppressedEntryIndices != null && slot.suppressedEntryIndices.Count > 0
                    ? slot.suppressedEntryIndices
                    : BuildFallbackSuppressedIndices(slot);

                if (IsValidEntryIndex(profile, slot.assignedEntryIndex))
                {
                    EntryCoverageStatusSummary winner = GetEntryCoverageStatusSummary(slot.assignedEntryIndex);
                    winner.suppressesOtherCount++;
                    winner.suppressesOtherSlots.Add(slot.shortLabel);
                    AppendEntryNames(profile, suppressed, winner.suppressesOtherNames, slot.assignedEntryIndex);
                }

                for (int j = 0; j < suppressed.Count; j++)
                {
                    int entryIndex = suppressed[j];
                    if (!IsValidEntryIndex(profile, entryIndex))
                        continue;

                    EntryCoverageStatusSummary summary = GetEntryCoverageStatusSummary(entryIndex);
                    summary.suppressedByCount++;
                    summary.suppressedBySlots.Add(slot.shortLabel);
                    TrickPoseEntry winnerEntry = ResolveEntry(profile, slot.assignedEntryIndex);
                    if (winnerEntry != null)
                        summary.suppressedByNames.Add(winnerEntry.GetSummary());
                }
            }
        }
    }

    private int BuildEntryCoverageSummaryHash()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + _slots.Count;
            for (int i = 0; i < _slots.Count; i++)
            {
                TrickPoseCoverageSlot slot = _slots[i];
                if (slot == null)
                    continue;

                hash = (hash * 31) + (int)slot.validationStatus;
                hash = (hash * 31) + slot.assignedEntryIndex;
                hash = (hash * 31) + (slot.candidateEntryIndices != null ? slot.candidateEntryIndices.Count : 0);
                hash = (hash * 31) + (slot.ambiguousEntryIndices != null ? slot.ambiguousEntryIndices.Count : 0);
                hash = (hash * 31) + (slot.suppressedEntryIndices != null ? slot.suppressedEntryIndices.Count : 0);
            }

            return hash;
        }
    }

    private EntryCoverageStatusSummary GetEntryCoverageStatusSummary(int index)
    {
        if (!_entryCoverageSummaryCache.TryGetValue(index, out EntryCoverageStatusSummary summary))
        {
            summary = new EntryCoverageStatusSummary();
            _entryCoverageSummaryCache[index] = summary;
        }

        return summary;
    }

    private bool EntryMatchesSidebarFilter(EntryCoverageStatusSummary status)
    {
        return _entrySidebarFilter switch
        {
            EntrySidebarFilter.Ambiguous => status.ambiguousCount > 0,
            EntrySidebarFilter.Suppressed => status.suppressedByCount > 0,
            EntrySidebarFilter.SuppressesOthers => status.suppressesOtherCount > 0,
            _ => true
        };
    }

    private static string BuildEntryCoverageSearchText(EntryCoverageStatusSummary status)
    {
        if (status == null)
            return string.Empty;

        List<string> values = new List<string>();
        if (status.ambiguousCount > 0)
            values.Add("ambiguous");
        if (status.suppressedByCount > 0)
            values.Add("suppressed");
        if (status.suppressesOtherCount > 0)
            values.Add("suppresses");
        return string.Join(" ", values);
    }

    private static string BuildEntryCoverageTooltip(EntryCoverageStatusSummary status)
    {
        if (status == null)
            return string.Empty;

        List<string> lines = new List<string>();
        if (status.ambiguousCount > 0)
            lines.Add($"Ambiguous: {status.ambiguousCount} ({BuildExampleList(status.ambiguousSlots)} | {BuildExampleList(status.ambiguousNames)})");
        if (status.suppressedByCount > 0)
            lines.Add($"Suppressed: {status.suppressedByCount} ({BuildExampleList(status.suppressedBySlots)} | {BuildExampleList(status.suppressedByNames)})");
        if (status.suppressesOtherCount > 0)
            lines.Add($"Suppresses: {status.suppressesOtherCount} ({BuildExampleList(status.suppressesOtherSlots)} | {BuildExampleList(status.suppressesOtherNames)})");
        return string.Join("\n", lines);
    }

    private static string BuildEntryListLabel(TrickPoseProfileSO profile, List<int> indices)
    {
        if (profile == null || indices == null || indices.Count == 0)
            return "(none)";

        List<string> names = new List<string>();
        for (int i = 0; i < indices.Count; i++)
        {
            TrickPoseEntry entry = ResolveEntry(profile, indices[i]);
            if (entry != null)
                names.Add(entry.GetSummary());
        }

        return names.Count > 0 ? string.Join(", ", names) : "(none)";
    }

    private static void AppendEntryNames(TrickPoseProfileSO profile, List<int> indices, HashSet<string> names, int exclude)
    {
        if (profile == null || indices == null || names == null)
            return;

        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            if (index == exclude)
                continue;

            TrickPoseEntry entry = ResolveEntry(profile, index);
            if (entry != null)
                names.Add(entry.GetSummary());
        }
    }

    private static bool IsValidEntryIndex(TrickPoseProfileSO profile, int index)
    {
        return profile != null && profile.entries != null && index >= 0 && index < profile.entries.Count;
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

    private static string BuildExampleList(HashSet<string> values)
    {
        if (values == null || values.Count == 0)
            return "none";

        List<string> ordered = new List<string>(values);
        ordered.Sort(StringComparer.OrdinalIgnoreCase);
        if (ordered.Count > 3)
            return $"{ordered[0]}, {ordered[1]}, {ordered[2]}";
        return string.Join(", ", ordered);
    }

    private static TrickPoseEntry ResolveEntry(TrickPoseProfileSO profile, int index)
    {
        if (profile == null || profile.entries == null || index < 0 || index >= profile.entries.Count)
            return null;
        return profile.entries[index];
    }

    private static string BuildKey(List<int> values)
    {
        if (values == null || values.Count == 0)
            return "none";

        List<int> ordered = new List<int>(values);
        ordered.Sort();
        return string.Join(",", ordered);
    }

    private static void DrawCountLine(string label, int count, float percentage)
    {
        EditorGUILayout.LabelField(label, $"{count} ({percentage * 100f:0.#}%)");
    }

    private static float Percentage(int count, int total)
    {
        return total > 0 ? (float)count / total : 0f;
    }

    private static bool Contains(string haystack, string needle)
    {
        return !string.IsNullOrWhiteSpace(haystack) &&
               haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void DrawHelpToolbar()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(110f)))
        {
            TrickPoseEditorHelpState.ShowInlineHelp = GUILayout.Toggle(TrickPoseEditorHelpState.ShowInlineHelp, TrickPoseEditorHelp.Button("Coverage.ShowHelp", "Show Help"), "Button");
            TrickPoseEditorHelpState.ShowAdvancedHelp = GUILayout.Toggle(TrickPoseEditorHelpState.ShowAdvancedHelp, TrickPoseEditorHelp.Button("Coverage.ShowAdvancedHelp", "Advanced Help"), "Button");
            if (GUILayout.Button(TrickPoseEditorHelp.Button("Coverage.ResetHelp", "Reset Help")))
                TrickPoseEditorHelpState.ResetAll();
        }
    }

    private void DrawOnboardingBanner(TrickPoseProfileSO profile, SkiController controller)
    {
        bool shouldShow = TrickPoseEditorHelpState.ShowFirstTimeBanner &&
            !TrickPoseEditorHelpState.CoverageGettingStartedDismissed &&
            (profile == null || controller == null || _coveragePlan == null || _slots.Count == 0);
        if (!shouldShow)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Getting Started", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(TrickPoseEditorHelp.GetWorkflowGuide(), EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Dismiss", GUILayout.Width(80f)))
                TrickPoseEditorHelpState.CoverageGettingStartedDismissed = true;
        }
    }

    private void DrawMatrixLegend()
    {
        if (!TrickPoseEditorHelpState.ShowInlineHelp)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Covered / C", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Placeholder / P", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Gap / G", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Ambiguous / A", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Suppressed / S", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Excluded / X", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private sealed class CoverageAmbiguityGroup
    {
        public int representativeSlotIndex = -1;
        public string representativeLabel;
        public string statusLabel;
        public string candidateSummary;
        public string broadSegments;
        public readonly List<int> slotIndices = new List<int>();
        public readonly List<int> involvedIndices = new List<int>();
    }
}
