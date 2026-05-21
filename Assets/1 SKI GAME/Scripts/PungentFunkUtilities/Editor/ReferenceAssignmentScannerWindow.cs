using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class ReferenceAssignmentScannerWindow : EditorWindow
    {
        private enum ScanMode
        {
            Colliders,
            Renderers,
            GameObjects,
            ExistingComponents
        }

        private sealed class Candidate
        {
            public Object asset;
            public string normalizedName;
            public string displayName;
        }

        private sealed class SuggestionRow
        {
            public Object contextObject;
            public GameObject gameObject;
            public Component existingComponent;
            public Object currentValue;
            public Object suggestion;
            public string reason;
        }

        private const string PrefPrefix = "PungentFunk.Utilities.ReferenceAssignmentScanner.";
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<Object> _manualCandidates = new List<Object>();
        private readonly List<SuggestionRow> _rows = new List<SuggestionRow>();
        private readonly PungentScanSession _scanSession = new PungentScanSession("reference-assignment-scanner", "Reference Assignment Scanner");

        private Vector2 _mainScroll;
        private Vector2 _candidateScroll;
        private Vector2 _resultScroll;
        private string _resultSourceBanner = string.Empty;
        private string _componentTypeName = "AudioMaterialTag";
        private string _referenceFieldName = "surfaceMaterial";
        private string _referenceAssetTypeName = "AudioSurfaceMaterialSO";
        private string _status = "Ready.";
        private string _candidateFilter = string.Empty;
        private string _rowFilter = string.Empty;
        private bool _selectionOnly;
        private bool _includeInactive = true;
        private bool _includeChildren = true;
        private bool _autoFindCandidateAssets = true;
        private bool _includeManualCandidates = true;
        private bool _includeRowsWithExistingComponents = true;
        private bool _onlyRowsWithSuggestion = true;
        private bool _addMissingComponent = true;
        private bool _overwriteExistingReferences;
        private bool _configExpanded = true;
        private bool _candidateExpanded = true;
        private bool _resultsExpanded = true;
        private float _configHeight = 205f;
        private float _candidateHeight = 150f;
        private float _leftResultsWidth = 430f;
        private ScanMode _scanMode = ScanMode.Colliders;

        public static void Open()
        {
            GetWindow<ReferenceAssignmentScannerWindow>("Reference Assignment Scanner");
        }

        public static void OpenAudioMaterialPreset()
        {
            ReferenceAssignmentScannerWindow window = GetWindow<ReferenceAssignmentScannerWindow>("Reference Assignment Scanner");
            window.ApplyAudioMaterialPreset();
            window.Show();
        }

        public static bool RunCoordinatorScan(out string status)
        {
            status = "Reference Assignment scan did not run.";
            ReferenceAssignmentScannerWindow window = null;
            bool destroyWhenDone = false;
            try
            {
                window = GetCoordinatorInstance(out destroyWhenDone);
                window.Scan();
                status = window._status;
                return true;
            }
            catch (Exception ex)
            {
                status = "Reference Assignment scan failed: " + ex.Message;
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                if (destroyWhenDone && window != null)
                    DestroyImmediate(window);
            }
        }

        public static bool TryGetCoordinatorNotConfiguredReason(out string reason)
        {
            string componentTypeName = UtilityWindowPrefs.GetString(PrefPrefix + "ComponentTypeName", "AudioMaterialTag");
            string referenceFieldName = UtilityWindowPrefs.GetString(PrefPrefix + "ReferenceFieldName", "surfaceMaterial");
            string referenceAssetTypeName = UtilityWindowPrefs.GetString(PrefPrefix + "ReferenceAssetTypeName", "AudioSurfaceMaterialSO");

            if (string.IsNullOrWhiteSpace(componentTypeName))
            {
                reason = "component type is not configured";
                return true;
            }

            Type componentType = FindType(componentTypeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                reason = "component type '" + componentTypeName + "' could not be resolved";
                return true;
            }

            if (string.IsNullOrWhiteSpace(referenceFieldName))
            {
                reason = "reference field is not configured";
                return true;
            }

            if (string.IsNullOrWhiteSpace(referenceAssetTypeName))
            {
                reason = "reference asset type is not configured";
                return true;
            }

            Type referenceAssetType = FindType(referenceAssetTypeName);
            if (referenceAssetType == null || !typeof(Object).IsAssignableFrom(referenceAssetType))
            {
                reason = "reference asset type '" + referenceAssetTypeName + "' could not be resolved";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public static PungentAuditScanJob CreateAuditJob(PungentAuditScanMode mode)
        {
            CooperativeReferenceAssignmentAudit audit = new CooperativeReferenceAssignmentAudit();
            PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative("reference-assignment-scanner", "Reference Assignment Scanner", audit.Step);
            job.canPause = true;
            job.canCancel = true;
            job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                               PungentAuditScanJobCapabilities.BackgroundSafe |
                               PungentAuditScanJobCapabilities.UsesAssetDatabase |
                               PungentAuditScanJobCapabilities.ScanOnly;
            job.Report(0f, 0, 1, "Queued reference assignment scan.", false, "Queued");
            return job;
        }

        private static ReferenceAssignmentScannerWindow GetCoordinatorInstance(out bool destroyWhenDone)
        {
            ReferenceAssignmentScannerWindow[] existing = Resources.FindObjectsOfTypeAll<ReferenceAssignmentScannerWindow>();
            if (existing != null && existing.Length > 0)
            {
                destroyWhenDone = false;
                return existing[0];
            }
            destroyWhenDone = true;
            return CreateInstance<ReferenceAssignmentScannerWindow>();
        }

        private void OnEnable()
        {
            minSize = new Vector2(620f, 430f);
            LoadPrefs();
            if (PungentScanCache.TryHydrateSession(_scanSession, out _resultSourceBanner) && _scanSession.Result != null)
                _status = _scanSession.Result.StatusMessage;
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Reference Assignment Scanner",
                "Scan scene objects, suggest reference assets from naming context, then add/update a selected component field.",
                _status);

            DrawToolbar();

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawConfigurationSection();
            DrawCandidateSection();
            DrawResultsSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Audio Preset", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                    ApplyAudioMaterialPreset();

                if (GUILayout.Button("Scan Scene", EditorStyles.toolbarButton, GUILayout.Width(86f)))
                {
                    _selectionOnly = false;
                    QueueScan();
                }

                if (GUILayout.Button("Scan Selection", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                {
                    _selectionOnly = true;
                    QueueScan();
                }

                if (GUILayout.Button("Apply All", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                    ApplyAllVisibleRows();

                GUILayout.FlexibleSpace();
                GUILayout.Label("Rows", EditorStyles.miniLabel, GUILayout.Width(34f));
                _rowFilter = EditorGUILayout.TextField(_rowFilter, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.Width(200f));
            }
        }

        private void DrawConfigurationSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                DrawFoldoutHeader(ref _configExpanded, "Scanner Configuration", UtilityWindowTheme.Blue, _scanMode.ToString());
                if (!_configExpanded)
                    return;

                using (new EditorGUILayout.VerticalScope(GUILayout.Height(_configHeight)))
                {
                    _scanMode = (ScanMode)EditorGUILayout.EnumPopup("Scan Mode", _scanMode);
                    _componentTypeName = EditorGUILayout.TextField(new GUIContent("Component Type", "The component type to add/update. Use the class name, e.g. AudioMaterialTag."), _componentTypeName);
                    _referenceFieldName = EditorGUILayout.TextField(new GUIContent("Reference Field", "Serialized ObjectReference field to assign on the component."), _referenceFieldName);
                    _referenceAssetTypeName = EditorGUILayout.TextField(new GUIContent("Reference Asset Type", "Candidate asset type to search for. Use the class name, e.g. AudioSurfaceMaterialSO."), _referenceAssetTypeName);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _selectionOnly = EditorGUILayout.Toggle("Selection Only", _selectionOnly);
                        _includeChildren = EditorGUILayout.Toggle("Include Children", _includeChildren);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _includeInactive = EditorGUILayout.Toggle("Include Inactive", _includeInactive);
                        _includeRowsWithExistingComponents = EditorGUILayout.Toggle("Rows With Existing Components", _includeRowsWithExistingComponents);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _onlyRowsWithSuggestion = EditorGUILayout.Toggle("Only With Suggestion", _onlyRowsWithSuggestion);
                        _addMissingComponent = EditorGUILayout.Toggle("Add Missing Component", _addMissingComponent);
                    }

                    _overwriteExistingReferences = EditorGUILayout.Toggle(new GUIContent("Overwrite Existing References", "When disabled, rows with existing assigned references are not overwritten by Apply All."), _overwriteExistingReferences);

                    EditorGUILayout.HelpBox("Suggestions are name-based. The scanner compares candidate asset names against object, tag, layer, material, and component context names.", MessageType.Info);
                }

                UtilityWindowTheme.VerticalResizeHandle(ref _configHeight, 120f, Mathf.Max(120f, position.height - 180f), SavePrefs);
            }
        }

        private void DrawCandidateSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                DrawFoldoutHeader(ref _candidateExpanded, "Candidate Reference Assets", UtilityWindowTheme.Teal, _candidates.Count.ToString());
                if (!_candidateExpanded)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    _autoFindCandidateAssets = EditorGUILayout.Toggle("Auto Find Assets", _autoFindCandidateAssets);
                    _includeManualCandidates = EditorGUILayout.Toggle("Include Manual", _includeManualCandidates);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rebuild Candidates"))
                        RebuildCandidates();

                    if (GUILayout.Button("Add Selected Assets"))
                        AddSelectedCandidates();

                    if (GUILayout.Button("Clear Manual"))
                        _manualCandidates.Clear();

                    GUILayout.Label("Filter", GUILayout.Width(38f));
                    _candidateFilter = EditorGUILayout.TextField(_candidateFilter, UtilityWindowTheme.ToolbarSearchStyle);
                }

                _candidateScroll = EditorGUILayout.BeginScrollView(_candidateScroll, GUILayout.Height(_candidateHeight));
                for (int i = _manualCandidates.Count - 1; i >= 0; i--)
                {
                    Object candidate = _manualCandidates[i];
                    if (!PassesFilter(candidate == null ? string.Empty : candidate.name, _candidateFilter))
                        continue;

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        _manualCandidates[i] = EditorGUILayout.ObjectField(candidate, typeof(Object), false);
                        if (GUILayout.Button("?", GUILayout.Width(26f)))
                            _manualCandidates.RemoveAt(i);
                    }
                }

                if (_manualCandidates.Count == 0)
                    EditorGUILayout.LabelField("No manual candidates. Use Auto Find Assets or add selected assets.", UtilityWindowTheme.MutedMiniLabelStyle);

                EditorGUILayout.EndScrollView();
                UtilityWindowTheme.VerticalResizeHandle(ref _candidateHeight, 70f, Mathf.Max(70f, position.height - 200f), SavePrefs);
            }
        }

        private void DrawResultsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                DrawFoldoutHeader(ref _resultsExpanded, "Scan Results", UtilityWindowTheme.Purple, _rows.Count.ToString());
                if (!_resultsExpanded)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(_leftResultsWidth)))
                    {
                        EditorGUILayout.LabelField("Targets", UtilityWindowTheme.SectionHeaderStyle);
                        DrawResultRows();
                    }

                    UtilityWindowTheme.HorizontalResizeHandle(ref _leftResultsWidth, 280f, Mathf.Max(280f, position.width - 220f), SavePrefs);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField("Summary", UtilityWindowTheme.SectionHeaderStyle);
                        PungentScanGUI.DrawResultHeader(_scanSession.Result, _resultSourceBanner);
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.LabelField($"Rows: {_rows.Count}", UtilityWindowTheme.CardLabelStyle);
                        EditorGUILayout.LabelField($"Candidates: {_candidates.Count}", UtilityWindowTheme.CardLabelStyle);
                        EditorGUILayout.LabelField($"Component: {_componentTypeName}", UtilityWindowTheme.PathLabelStyle);
                        EditorGUILayout.LabelField($"Field: {_referenceFieldName}", UtilityWindowTheme.PathLabelStyle);
                        EditorGUILayout.Space(8f);
                        EditorGUILayout.HelpBox("Use Apply on individual rows first. Apply All respects Overwrite Existing References.", MessageType.None);
                    }
                }
            }
        }

        private void DrawResultRows()
        {
            _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll, GUILayout.MinHeight(220f));
            for (int i = 0; i < _rows.Count; i++)
            {
                SuggestionRow row = _rows[i];
                if (row == null || !RowPassesFilter(row))
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(row.contextObject, typeof(Object), true);
                        if (GUILayout.Button("Ping", GUILayout.Width(48f)) && row.contextObject != null)
                            EditorGUIUtility.PingObject(row.contextObject);
                        using (new EditorGUI.DisabledScope(!CanApply(row)))
                        {
                            if (GUILayout.Button("Apply", GUILayout.Width(62f)))
                                ApplySuggestion(row);
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Current", GUILayout.Width(54f));
                        EditorGUILayout.ObjectField(row.currentValue, typeof(Object), false);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Suggest", GUILayout.Width(54f));
                        row.suggestion = EditorGUILayout.ObjectField(row.suggestion, typeof(Object), false);
                    }

                    EditorGUILayout.LabelField(row.reason ?? string.Empty, UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void QueueScan()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    Scan();
            };
        }

        private void Scan()
        {
            PungentScanScope scope = _selectionOnly ? PungentScanScope.Selection : PungentScanScope.OpenScenes;
            string scopeLabel = _selectionOnly ? "Selection" : _scanMode.ToString();
            PungentScanResult scanResult = _scanSession.Begin(scope, scopeLabel);

            _rows.Clear();

            try
            {
                RebuildCandidates();

                Type componentType = FindType(_componentTypeName);
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                {
                    _status = "Invalid component type.";
                    scanResult.AddIssue(PungentScanSeverity.Error, "Invalid component type", $"Could not resolve a Component type named '{_componentTypeName}'.", null, null, "REFSCAN_COMPONENT_TYPE");
                    _scanSession.Complete(0, 0, 0, 0, _status);
                    return;
                }

                if (_candidates.Count == 0)
                    scanResult.AddIssue(PungentScanSeverity.Warning, "No candidates found", $"No assets matched candidate type '{_referenceAssetTypeName}'. Suggestions will be empty unless rows already have references.", null, null, "REFSCAN_NO_CANDIDATES");

                List<Object> contexts = GatherContexts();
                int skippedExisting = 0;
                int skippedNoSuggestion = 0;
                int skippedInvalidContext = 0;

                foreach (Object context in contexts)
                {
                    GameObject gameObject = GetContextGameObject(context);
                    if (gameObject == null)
                    {
                        skippedInvalidContext++;
                        continue;
                    }

                    Component existingComponent = gameObject.GetComponent(componentType);
                    if (existingComponent != null && !_includeRowsWithExistingComponents)
                    {
                        skippedExisting++;
                        continue;
                    }

                    Object currentValue = existingComponent != null ? GetObjectReferenceValue(existingComponent, _referenceFieldName) : null;
                    string reason;
                    Object suggestion;

                    if (currentValue != null)
                    {
                        suggestion = currentValue;
                        reason = "Existing assigned reference.";
                    }
                    else
                    {
                        suggestion = SuggestReference(context, gameObject, out reason);
                    }

                    if (_onlyRowsWithSuggestion && suggestion == null)
                    {
                        skippedNoSuggestion++;
                        continue;
                    }

                    SuggestionRow row = new SuggestionRow
                    {
                        contextObject = context,
                        gameObject = gameObject,
                        existingComponent = existingComponent,
                        currentValue = currentValue,
                        suggestion = suggestion,
                        reason = string.IsNullOrEmpty(reason) ? "No confident suggestion." : reason
                    };
                    _rows.Add(row);

                    if (row.currentValue == null && row.suggestion != null)
                    {
                        string path = AssetDatabase.GetAssetPath(row.suggestion);
                        scanResult.AddIssue(
                            PungentScanSeverity.Warning,
                            "Assignment suggestion found",
                            row.gameObject.name + " can use candidate '" + row.suggestion.name + "' for field '" + _referenceFieldName + "'. " + row.reason,
                            row.contextObject,
                            path,
                            "REFSCAN_ASSIGNMENT_SUGGESTION");
                    }
                }

                int skippedTotal = skippedExisting + skippedNoSuggestion + skippedInvalidContext;
                if (skippedExisting > 0)
                    scanResult.AddIssue(PungentScanSeverity.Info, "Skipped existing components", $"Skipped {skippedExisting} object(s) because Rows With Existing Components is disabled.", null, null, "REFSCAN_SKIPPED_EXISTING");
                if (skippedNoSuggestion > 0)
                    scanResult.AddIssue(PungentScanSeverity.Info, "Skipped rows without suggestions", $"Skipped {skippedNoSuggestion} object(s) because Only With Suggestion is enabled.", null, null, "REFSCAN_SKIPPED_NO_SUGGESTION");
                if (skippedInvalidContext > 0)
                    scanResult.AddIssue(PungentScanSeverity.Warning, "Skipped invalid contexts", $"Skipped {skippedInvalidContext} context object(s) that did not resolve to a scene GameObject.", null, null, "REFSCAN_INVALID_CONTEXT");

                _status = $"Scanned {contexts.Count} context object(s). Rows={_rows.Count}, skipped={skippedTotal}.";
                _scanSession.Complete(contexts.Count, _rows.Count, skippedTotal, 0, _status);
            }
            catch (Exception ex)
            {
                _status = "Reference assignment scan failed: " + ex.Message;
                _scanSession.Fail(ex, _status);
                Debug.LogException(ex);
            }
        }

        private sealed class CooperativeReferenceAssignmentAudit
        {
            private enum Stage
            {
                Begin,
                ResolveTypes,
                RebuildCandidates,
                GatherContexts,
                ProcessContexts,
                Publish,
                Done
            }

            private readonly List<Candidate> _candidates = new List<Candidate>();
            private readonly List<Object> _contexts = new List<Object>();
            private Stage _stage = Stage.Begin;
            private PungentScanSession _session;
            private PungentScanResult _result;
            private Type _componentType;
            private Type _candidateType;
            private int _contextIndex;
            private int _rows;
            private int _skippedExisting;
            private int _skippedNoSuggestion;
            private int _skippedInvalidContext;
            private ScanMode _scanMode;
            private string _componentTypeName;
            private string _referenceFieldName;
            private string _referenceAssetTypeName;
            private bool _selectionOnly;
            private bool _includeInactive;
            private bool _includeChildren;
            private bool _autoFindCandidateAssets;
            private bool _includeManualCandidates;
            private bool _includeRowsWithExistingComponents;
            private bool _onlyRowsWithSuggestion;

            public PungentAuditScanStepResult Step(PungentAuditScanContext context)
            {
                if (context.IsCancellationRequested())
                    return Cancel("Reference Assignment scan cancelled before the next context batch.");
                if (context.IsPauseRequested())
                    return PungentAuditScanStepResult.Continue("Reference Assignment scan paused.");

                switch (_stage)
                {
                    case Stage.Begin:
                        LoadConfig();
                        _session = new PungentScanSession("reference-assignment-scanner", "Reference Assignment Scanner");
                        _result = _session.Begin(_selectionOnly ? PungentScanScope.Selection : PungentScanScope.OpenScenes, _selectionOnly ? "Selection" : _scanMode.ToString());
                        _stage = Stage.ResolveTypes;
                        context.Report(0.05f, 0, 1, "Resolving component and reference types.", false, "Resolve types");
                        return PungentAuditScanStepResult.Continue("Resolving component and reference types.");

                    case Stage.ResolveTypes:
                        _componentType = FindType(_componentTypeName);
                        _candidateType = FindType(_referenceAssetTypeName);
                        if (_componentType == null || !typeof(Component).IsAssignableFrom(_componentType))
                        {
                            _result.AddIssue(PungentScanSeverity.Error, "Invalid component type", "Could not resolve a Component type named '" + _componentTypeName + "'.", null, null, "REFSCAN_COMPONENT_TYPE");
                            PungentScanResult notConfigured = _session.Complete(0, 0, 0, 0, "Invalid component type.");
                            _stage = Stage.Done;
                            return PungentAuditScanStepResult.NotConfigured("Invalid component type.", notConfigured);
                        }
                        if (_candidateType == null || !typeof(Object).IsAssignableFrom(_candidateType))
                        {
                            _result.AddIssue(PungentScanSeverity.Error, "Invalid candidate type", "Could not resolve an Object type named '" + _referenceAssetTypeName + "'.", null, null, "REFSCAN_REFERENCE_ASSET_TYPE");
                            PungentScanResult notConfigured = _session.Complete(0, 0, 0, 0, "Invalid reference asset type.");
                            _stage = Stage.Done;
                            return PungentAuditScanStepResult.NotConfigured("Invalid reference asset type.", notConfigured);
                        }
                        _stage = Stage.RebuildCandidates;
                        context.Report(0.15f, 0, 1, "Rebuilding candidate assets.", true, "Candidates");
                        return PungentAuditScanStepResult.Continue("Rebuilding candidate assets.");

                    case Stage.RebuildCandidates:
                        RebuildCandidates();
                        if (_candidates.Count == 0)
                            _result.AddIssue(PungentScanSeverity.Warning, "No candidates found", "No assets matched candidate type '" + _referenceAssetTypeName + "'. Suggestions will be empty unless rows already have references.", null, null, "REFSCAN_NO_CANDIDATES");
                        _stage = Stage.GatherContexts;
                        context.Report(0.28f, _candidates.Count, Math.Max(1, _candidates.Count), "Gathered " + _candidates.Count + " candidate asset(s).", false, "Candidates");
                        return PungentAuditScanStepResult.Continue("Candidate assets gathered.");

                    case Stage.GatherContexts:
                        GatherContexts(_contexts);
                        _stage = Stage.ProcessContexts;
                        context.Report(0.36f, 0, Math.Max(1, _contexts.Count), "Gathered " + _contexts.Count + " context object(s).", false, "Contexts");
                        return PungentAuditScanStepResult.Continue("Context objects gathered.");

                    case Stage.ProcessContexts:
                        int batch = context.Mode == PungentAuditScanMode.BackgroundIdle ? 15 : 50;
                        int limit = Math.Min(_contexts.Count, _contextIndex + batch);
                        while (_contextIndex < limit)
                            ProcessContext(_contexts[_contextIndex++]);
                        float progress = _contexts.Count == 0 ? 0.90f : 0.36f + (0.54f * _contextIndex / Mathf.Max(1, _contexts.Count));
                        context.Report(progress, _contextIndex, _contexts.Count, _contextIndex + " / " + _contexts.Count + " contexts checked.", false, "Contexts");
                        if (_contextIndex < _contexts.Count)
                            return PungentAuditScanStepResult.Continue(_contextIndex + " / " + _contexts.Count + " contexts checked.");
                        _stage = Stage.Publish;
                        return PungentAuditScanStepResult.Continue("Publishing Reference Assignment scan.");

                    case Stage.Publish:
                        int skippedTotal = _skippedExisting + _skippedNoSuggestion + _skippedInvalidContext;
                        if (_skippedExisting > 0)
                            _result.AddIssue(PungentScanSeverity.Info, "Skipped existing components", "Skipped " + _skippedExisting + " object(s) because Rows With Existing Components is disabled.", null, null, "REFSCAN_SKIPPED_EXISTING");
                        if (_skippedNoSuggestion > 0)
                            _result.AddIssue(PungentScanSeverity.Info, "Skipped rows without suggestions", "Skipped " + _skippedNoSuggestion + " object(s) because Only With Suggestion is enabled.", null, null, "REFSCAN_SKIPPED_NO_SUGGESTION");
                        if (_skippedInvalidContext > 0)
                            _result.AddIssue(PungentScanSeverity.Warning, "Skipped invalid contexts", "Skipped " + _skippedInvalidContext + " context object(s) that did not resolve to a scene GameObject.", null, null, "REFSCAN_INVALID_CONTEXT");
                        string status = "Scanned " + _contexts.Count + " context object(s). Rows=" + _rows + ", skipped=" + skippedTotal + ".";
                        PungentScanResult completed = _session.Complete(_contexts.Count, _rows, skippedTotal, 0, status);
                        context.Report(1f, _contexts.Count, Math.Max(1, _contexts.Count), status, false, "Complete");
                        _stage = Stage.Done;
                        return PungentAuditScanStepResult.Complete(status, completed);

                    default:
                        return PungentAuditScanStepResult.Complete("Reference Assignment scan already completed.", _result);
                }
            }

            private void LoadConfig()
            {
                _scanMode = (ScanMode)UtilityWindowPrefs.GetInt(PrefPrefix + "ScanMode", 0);
                _componentTypeName = UtilityWindowPrefs.GetString(PrefPrefix + "ComponentTypeName", "AudioMaterialTag");
                _referenceFieldName = UtilityWindowPrefs.GetString(PrefPrefix + "ReferenceFieldName", "surfaceMaterial");
                _referenceAssetTypeName = UtilityWindowPrefs.GetString(PrefPrefix + "ReferenceAssetTypeName", "AudioSurfaceMaterialSO");
                _selectionOnly = UtilityWindowPrefs.GetBool(PrefPrefix + "SelectionOnly", false);
                _includeInactive = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeInactive", true);
                _includeChildren = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeChildren", true);
                _autoFindCandidateAssets = UtilityWindowPrefs.GetBool(PrefPrefix + "AutoFindCandidateAssets", true);
                _includeManualCandidates = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeManualCandidates", true);
                _includeRowsWithExistingComponents = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeRowsWithExistingComponents", true);
                _onlyRowsWithSuggestion = UtilityWindowPrefs.GetBool(PrefPrefix + "OnlyRowsWithSuggestion", true);
            }

            private void RebuildCandidates()
            {
                _candidates.Clear();
                if (_autoFindCandidateAssets && _candidateType != null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:" + _candidateType.Name);
                    for (int i = 0; i < guids.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                        Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                        if (asset != null && _candidateType.IsInstanceOfType(asset))
                            AddCandidate(asset);
                    }
                }

                if (!_includeManualCandidates)
                    return;

                ReferenceAssignmentScannerWindow[] windows = Resources.FindObjectsOfTypeAll<ReferenceAssignmentScannerWindow>();
                if (windows == null || windows.Length == 0)
                    return;
                for (int i = 0; i < windows[0]._manualCandidates.Count; i++)
                {
                    Object candidate = windows[0]._manualCandidates[i];
                    if (candidate != null && (_candidateType == null || _candidateType.IsInstanceOfType(candidate)))
                        AddCandidate(candidate);
                }
            }

            private void AddCandidate(Object asset)
            {
                if (asset == null)
                    return;
                for (int i = 0; i < _candidates.Count; i++)
                    if (_candidates[i].asset == asset)
                        return;
                _candidates.Add(new Candidate
                {
                    asset = asset,
                    displayName = ObjectNames.NicifyVariableName(asset.name),
                    normalizedName = Normalize(asset.name)
                });
            }

            private void GatherContexts(List<Object> results)
            {
                results.Clear();
                if (_selectionOnly)
                {
                    foreach (GameObject root in Selection.gameObjects)
                    {
                        if (root == null)
                            continue;
                        if (_includeChildren)
                            AddContextsFromRoot(root, results);
                        else
                            AddContextFromGameObject(root, results);
                    }
                    return;
                }

                switch (_scanMode)
                {
                    case ScanMode.Colliders:
#if UNITY_2023_1_OR_NEWER
                        results.AddRange(Object.FindObjectsByType<Collider>(_includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude));
#else
                        results.AddRange(Object.FindObjectsOfType<Collider>(_includeInactive));
#endif
                        break;
                    case ScanMode.Renderers:
#if UNITY_2023_1_OR_NEWER
                        results.AddRange(Object.FindObjectsByType<Renderer>(_includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude));
#else
                        results.AddRange(Object.FindObjectsOfType<Renderer>(_includeInactive));
#endif
                        break;
                    case ScanMode.ExistingComponents:
                        AddExistingComponents(results);
                        break;
                    default:
                        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                            if (IsSceneObject(go) && (_includeInactive || go.activeInHierarchy))
                                results.Add(go);
                        break;
                }
            }

            private void AddContextsFromRoot(GameObject root, List<Object> results)
            {
                if (root == null)
                    return;
                switch (_scanMode)
                {
                    case ScanMode.Colliders:
                        results.AddRange(root.GetComponentsInChildren<Collider>(_includeInactive));
                        break;
                    case ScanMode.Renderers:
                        results.AddRange(root.GetComponentsInChildren<Renderer>(_includeInactive));
                        break;
                    case ScanMode.ExistingComponents:
                        foreach (Component component in root.GetComponentsInChildren(_componentType, _includeInactive))
                            results.Add(component);
                        break;
                    default:
                        foreach (Transform child in root.GetComponentsInChildren<Transform>(_includeInactive))
                            results.Add(child.gameObject);
                        break;
                }
            }

            private void AddContextFromGameObject(GameObject go, List<Object> results)
            {
                if (go == null)
                    return;
                switch (_scanMode)
                {
                    case ScanMode.Colliders:
                        Collider collider = go.GetComponent<Collider>();
                        if (collider != null)
                            results.Add(collider);
                        break;
                    case ScanMode.Renderers:
                        Renderer renderer = go.GetComponent<Renderer>();
                        if (renderer != null)
                            results.Add(renderer);
                        break;
                    case ScanMode.ExistingComponents:
                        Component component = go.GetComponent(_componentType);
                        if (component != null)
                            results.Add(component);
                        break;
                    default:
                        results.Add(go);
                        break;
                }
            }

            private void AddExistingComponents(List<Object> results)
            {
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (!IsSceneObject(go) || (!_includeInactive && !go.activeInHierarchy))
                        continue;
                    Component component = go.GetComponent(_componentType);
                    if (component != null)
                        results.Add(component);
                }
            }

            private void ProcessContext(Object context)
            {
                GameObject gameObject = GetContextGameObject(context);
                if (gameObject == null)
                {
                    _skippedInvalidContext++;
                    return;
                }

                Component existingComponent = gameObject.GetComponent(_componentType);
                if (existingComponent != null && !_includeRowsWithExistingComponents)
                {
                    _skippedExisting++;
                    return;
                }

                Object currentValue = existingComponent != null ? GetObjectReferenceValue(existingComponent, _referenceFieldName) : null;
                string reason = string.Empty;
                Object suggestion = currentValue != null ? currentValue : SuggestReference(context, gameObject, out reason);
                if (currentValue != null)
                    reason = "Existing assigned reference.";

                if (_onlyRowsWithSuggestion && suggestion == null)
                {
                    _skippedNoSuggestion++;
                    return;
                }

                _rows++;
                if (currentValue == null && suggestion != null)
                {
                    string path = AssetDatabase.GetAssetPath(suggestion);
                    _result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Assignment suggestion found",
                        gameObject.name + " can use candidate '" + suggestion.name + "' for field '" + _referenceFieldName + "'. " + (string.IsNullOrEmpty(reason) ? "No confident suggestion." : reason),
                        context,
                        path,
                        "REFSCAN_ASSIGNMENT_SUGGESTION");
                }
            }

            private Object SuggestReference(Object context, GameObject gameObject, out string reason)
            {
                reason = null;
                if (_candidates.Count == 0)
                {
                    reason = "No candidate assets found.";
                    return null;
                }

                string haystack = BuildHaystack(context, gameObject);
                Candidate best = null;
                int bestScore = 0;
                for (int i = 0; i < _candidates.Count; i++)
                {
                    Candidate candidate = _candidates[i];
                    if (candidate == null || candidate.asset == null)
                        continue;
                    int score = GetCandidateScore(candidate, haystack);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }

                if (best != null && bestScore > 0)
                {
                    reason = "Matched candidate '" + best.asset.name + "' by name context. Score=" + bestScore + ".";
                    return best.asset;
                }

                reason = "No candidate name matched this object's context.";
                return null;
            }

            private PungentAuditScanStepResult Cancel(string status)
            {
                if (_session != null)
                    _session.Cancel(status, false);
                _stage = Stage.Done;
                return PungentAuditScanStepResult.Cancelled(status);
            }
        }

        private List<Object> GatherContexts()
        {
            var results = new List<Object>();
            if (_selectionOnly)
            {
                foreach (GameObject root in Selection.gameObjects)
                {
                    if (root == null)
                        continue;

                    if (_includeChildren)
                        AddContextsFromRoot(root, results);
                    else
                        AddContextFromGameObject(root, results);
                }
            }
            else
            {
                switch (_scanMode)
                {
                    case ScanMode.Colliders:
                        results.AddRange(Object.FindObjectsOfType<Collider>(_includeInactive));
                        break;
                    case ScanMode.Renderers:
                        results.AddRange(Object.FindObjectsOfType<Renderer>(_includeInactive));
                        break;
                    case ScanMode.ExistingComponents:
                        AddExistingComponents(results);
                        break;
                    default:
                        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                        {
                            if (IsSceneObject(go) && (_includeInactive || go.activeInHierarchy))
                                results.Add(go);
                        }
                        break;
                }
            }

            return results;
        }

        private void AddContextsFromRoot(GameObject root, List<Object> results)
        {
            if (root == null)
                return;

            switch (_scanMode)
            {
                case ScanMode.Colliders:
                    results.AddRange(root.GetComponentsInChildren<Collider>(_includeInactive));
                    break;
                case ScanMode.Renderers:
                    results.AddRange(root.GetComponentsInChildren<Renderer>(_includeInactive));
                    break;
                case ScanMode.ExistingComponents:
                    Type type = FindType(_componentTypeName);
                    if (type != null && typeof(Component).IsAssignableFrom(type))
                    {
                        foreach (Component component in root.GetComponentsInChildren(type, _includeInactive))
                            results.Add(component);
                    }
                    break;
                default:
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(_includeInactive))
                        results.Add(child.gameObject);
                    break;
            }
        }

        private void AddContextFromGameObject(GameObject go, List<Object> results)
        {
            if (go == null)
                return;

            switch (_scanMode)
            {
                case ScanMode.Colliders:
                    Collider collider = go.GetComponent<Collider>();
                    if (collider != null)
                        results.Add(collider);
                    break;
                case ScanMode.Renderers:
                    Renderer renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                        results.Add(renderer);
                    break;
                case ScanMode.ExistingComponents:
                    Type type = FindType(_componentTypeName);
                    if (type != null)
                    {
                        Component component = go.GetComponent(type);
                        if (component != null)
                            results.Add(component);
                    }
                    break;
                default:
                    results.Add(go);
                    break;
            }
        }

        private void AddExistingComponents(List<Object> results)
        {
            Type type = FindType(_componentTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                return;

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsSceneObject(go) || (!_includeInactive && !go.activeInHierarchy))
                    continue;

                Component component = go.GetComponent(type);
                if (component != null)
                    results.Add(component);
            }
        }

        private void RebuildCandidates()
        {
            _candidates.Clear();
            Type candidateType = FindType(_referenceAssetTypeName);

            if (_autoFindCandidateAssets && candidateType != null)
            {
                string[] guids = AssetDatabase.FindAssets("t:" + candidateType.Name);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (asset != null && candidateType.IsInstanceOfType(asset))
                        AddCandidate(asset);
                }
            }

            if (_includeManualCandidates)
            {
                foreach (Object candidate in _manualCandidates)
                {
                    if (candidate == null)
                        continue;

                    if (candidateType == null || candidateType.IsInstanceOfType(candidate))
                        AddCandidate(candidate);
                }
            }
        }

        private void AddCandidate(Object asset)
        {
            if (asset == null)
                return;

            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].asset == asset)
                    return;
            }

            _candidates.Add(new Candidate
            {
                asset = asset,
                displayName = ObjectNames.NicifyVariableName(asset.name),
                normalizedName = Normalize(asset.name)
            });
        }

        private Object SuggestReference(Object context, GameObject gameObject, out string reason)
        {
            reason = null;
            if (_candidates.Count == 0)
            {
                reason = "No candidate assets found.";
                return null;
            }

            string haystack = BuildHaystack(context, gameObject);
            Candidate best = null;
            int bestScore = 0;

            foreach (Candidate candidate in _candidates)
            {
                if (candidate == null || candidate.asset == null)
                    continue;

                int score = GetCandidateScore(candidate, haystack);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best != null && bestScore > 0)
            {
                reason = $"Matched candidate '{best.asset.name}' by name context. Score={bestScore}.";
                return best.asset;
            }

            reason = "No candidate name matched this object's context.";
            return null;
        }

        private static int GetCandidateScore(Candidate candidate, string haystack)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.normalizedName) || string.IsNullOrEmpty(haystack))
                return 0;

            int score = 0;
            if (haystack.Contains(candidate.normalizedName))
                score += 100 + candidate.normalizedName.Length;

            string[] words = SplitWords(candidate.displayName);
            for (int i = 0; i < words.Length; i++)
            {
                string word = Normalize(words[i]);
                if (word.Length >= 3 && haystack.Contains(word))
                    score += 10 + word.Length;
            }

            return score;
        }

        private static string BuildHaystack(Object context, GameObject go)
        {
            var parts = new List<string>();
            if (context != null)
                parts.Add(context.name);
            if (go != null)
            {
                parts.Add(go.name);
                parts.Add(go.tag);
                parts.Add(LayerMask.LayerToName(go.layer));

                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null)
                            parts.Add(material.name);
                    }
                }

                Collider collider = go.GetComponent<Collider>();
                if (collider != null && collider.sharedMaterial != null)
                    parts.Add(collider.sharedMaterial.name);
            }

            return Normalize(string.Join(" ", parts.ToArray()));
        }

        private static Object GetObjectReferenceValue(Component component, string propertyName)
        {
            if (component == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            SerializedObject so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return null;

            return property.objectReferenceValue;
        }

        private bool CanApply(SuggestionRow row)
        {
            if (row == null || row.gameObject == null || row.suggestion == null)
                return false;

            if (row.currentValue != null && !_overwriteExistingReferences)
                return false;

            return true;
        }

        private void ApplySuggestion(SuggestionRow row)
        {
            if (!CanApply(row))
                return;

            Type componentType = FindType(_componentTypeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                _status = "Cannot apply: invalid component type.";
                return;
            }

            Component component = row.existingComponent;
            if (component == null)
            {
                if (!_addMissingComponent)
                {
                    _status = "Cannot apply: missing component and Add Missing Component is disabled.";
                    return;
                }

                Undo.RegisterCompleteObjectUndo(row.gameObject, "Add Reference Assignment Component");
                component = Undo.AddComponent(row.gameObject, componentType);
            }
            else
            {
                Undo.RecordObject(component, "Assign Reference");
            }

            SerializedObject so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(_referenceFieldName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                _status = $"Cannot apply: '{_referenceFieldName}' is not an object-reference field on {componentType.Name}.";
                return;
            }

            property.objectReferenceValue = row.suggestion;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);

            row.existingComponent = component;
            row.currentValue = row.suggestion;
            row.reason = "Applied.";
            _status = $"Applied reference to {row.gameObject.name}.";
        }

        private void ApplyAllVisibleRows()
        {
            int applied = 0;
            foreach (SuggestionRow row in _rows)
            {
                if (row == null || !RowPassesFilter(row) || !CanApply(row))
                    continue;

                ApplySuggestion(row);
                applied++;
            }

            _status = $"Applied {applied} row(s).";
        }

        private bool RowPassesFilter(SuggestionRow row)
        {
            if (string.IsNullOrWhiteSpace(_rowFilter))
                return true;

            string haystack = $"{row.contextObject?.name} {row.gameObject?.name} {row.currentValue?.name} {row.suggestion?.name} {row.reason}";
            return haystack.IndexOf(_rowFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddSelectedCandidates()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj == null || _manualCandidates.Contains(obj))
                    continue;
                _manualCandidates.Add(obj);
            }

            RebuildCandidates();
            _status = "Added selected candidate assets.";
        }

        private void ApplyAudioMaterialPreset()
        {
            _scanMode = ScanMode.Colliders;
            _componentTypeName = "AudioMaterialTag";
            _referenceFieldName = "surfaceMaterial";
            _referenceAssetTypeName = "AudioSurfaceMaterialSO";
            _selectionOnly = false;
            _includeInactive = true;
            _includeChildren = true;
            _autoFindCandidateAssets = true;
            _includeRowsWithExistingComponents = false;
            _onlyRowsWithSuggestion = true;
            _addMissingComponent = true;
            _overwriteExistingReferences = false;
            _status = "Loaded Audio Material assignment preset.";
            SavePrefs();
        }

        private static GameObject GetContextGameObject(Object context)
        {
            if (context is GameObject go)
                return go;
            if (context is Component component)
                return component.gameObject;
            return null;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go);
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            return PungentEditorPerformanceUtility.ResolveTypeCached(typeName);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] buffer = new char[value.Length];
            int length = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(c))
                    buffer[length++] = c;
            }
            return new string(buffer, 0, length);
        }

        private static string[] SplitWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value.Split(new[] { ' ', '_', '-', '.', '/', '\\', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool PassesFilter(string value, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) || (!string.IsNullOrEmpty(value) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void DrawFoldoutHeader(ref bool expanded, string title, Color tint, string pill)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, title, true, UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(pill, tint);
            }
        }

        private void LoadPrefs()
        {
            _scanMode = (ScanMode)UtilityWindowPrefs.GetInt(PrefPrefix + "ScanMode", 0);
            _componentTypeName = UtilityWindowPrefs.GetString(PrefPrefix + "ComponentTypeName", _componentTypeName);
            _referenceFieldName = UtilityWindowPrefs.GetString(PrefPrefix + "ReferenceFieldName", _referenceFieldName);
            _referenceAssetTypeName = UtilityWindowPrefs.GetString(PrefPrefix + "ReferenceAssetTypeName", _referenceAssetTypeName);
            _selectionOnly = UtilityWindowPrefs.GetBool(PrefPrefix + "SelectionOnly", false);
            _includeInactive = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeInactive", true);
            _includeChildren = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeChildren", true);
            _autoFindCandidateAssets = UtilityWindowPrefs.GetBool(PrefPrefix + "AutoFindCandidateAssets", true);
            _includeManualCandidates = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeManualCandidates", true);
            _includeRowsWithExistingComponents = UtilityWindowPrefs.GetBool(PrefPrefix + "IncludeRowsWithExistingComponents", true);
            _onlyRowsWithSuggestion = UtilityWindowPrefs.GetBool(PrefPrefix + "OnlyRowsWithSuggestion", true);
            _addMissingComponent = UtilityWindowPrefs.GetBool(PrefPrefix + "AddMissingComponent", true);
            _overwriteExistingReferences = UtilityWindowPrefs.GetBool(PrefPrefix + "OverwriteExistingReferences", false);
            _configExpanded = UtilityWindowPrefs.GetBool(PrefPrefix + "ConfigExpanded", true);
            _candidateExpanded = UtilityWindowPrefs.GetBool(PrefPrefix + "CandidateExpanded", true);
            _resultsExpanded = UtilityWindowPrefs.GetBool(PrefPrefix + "ResultsExpanded", true);
            _configHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "ConfigHeight", 205f);
            _candidateHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "CandidateHeight", 150f);
            _leftResultsWidth = UtilityWindowPrefs.GetFloat(PrefPrefix + "LeftResultsWidth", 430f);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetInt(PrefPrefix + "ScanMode", (int)_scanMode);
            UtilityWindowPrefs.SetString(PrefPrefix + "ComponentTypeName", _componentTypeName);
            UtilityWindowPrefs.SetString(PrefPrefix + "ReferenceFieldName", _referenceFieldName);
            UtilityWindowPrefs.SetString(PrefPrefix + "ReferenceAssetTypeName", _referenceAssetTypeName);
            UtilityWindowPrefs.SetBool(PrefPrefix + "SelectionOnly", _selectionOnly);
            UtilityWindowPrefs.SetBool(PrefPrefix + "IncludeInactive", _includeInactive);
            UtilityWindowPrefs.SetBool(PrefPrefix + "IncludeChildren", _includeChildren);
            UtilityWindowPrefs.SetBool(PrefPrefix + "AutoFindCandidateAssets", _autoFindCandidateAssets);
            UtilityWindowPrefs.SetBool(PrefPrefix + "IncludeManualCandidates", _includeManualCandidates);
            UtilityWindowPrefs.SetBool(PrefPrefix + "IncludeRowsWithExistingComponents", _includeRowsWithExistingComponents);
            UtilityWindowPrefs.SetBool(PrefPrefix + "OnlyRowsWithSuggestion", _onlyRowsWithSuggestion);
            UtilityWindowPrefs.SetBool(PrefPrefix + "AddMissingComponent", _addMissingComponent);
            UtilityWindowPrefs.SetBool(PrefPrefix + "OverwriteExistingReferences", _overwriteExistingReferences);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ConfigExpanded", _configExpanded);
            UtilityWindowPrefs.SetBool(PrefPrefix + "CandidateExpanded", _candidateExpanded);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ResultsExpanded", _resultsExpanded);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "ConfigHeight", _configHeight);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "CandidateHeight", _candidateHeight);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "LeftResultsWidth", _leftResultsWidth);
        }
    }

    internal sealed class ReferenceAssignmentAuditProvider : IPungentAuditScanProvider
    {
        public string ProviderId => "reference-assignment-scanner";
        public string DisplayName => "Reference Assignment Scanner";
        public string Description => "Checks missing assignable references in selected, open-scene, or project scopes.";
        public string OpenButtonLabel => "Open Reference Scanner";
        public string RunButtonLabel => "Run Reference Scan";
        public bool CanRunImmediate => true;
        public bool CanRunBackground => true;
        public bool CanRunFromCoordinator => true;
        public bool CanPause => true;
        public bool CanCancel => true;
        public bool UsesSceneOpening => false;
        public bool UsesAssetDatabase => true;
        public bool UsesModalProgress => false;
        public bool IsCooperative => true;
        public bool IsMonolithic => false;
        public bool IsScanOnly => true;

        public bool TryGetNotConfiguredReason(out string reason)
        {
            return ReferenceAssignmentScannerWindow.TryGetCoordinatorNotConfiguredReason(out reason);
        }

        public PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
        {
            return ReferenceAssignmentScannerWindow.CreateAuditJob(mode);
        }

        public void OpenWindow()
        {
            ReferenceAssignmentScannerWindow.Open();
        }
    }
#endif

}
