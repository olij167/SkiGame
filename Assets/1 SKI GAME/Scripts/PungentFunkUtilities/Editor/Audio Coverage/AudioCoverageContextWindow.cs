using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;
using PungentFunk.Utilities.Audio;

namespace PungentFunk.Utilities.Editor.Audio
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Generic, profile-driven audio setup coverage scanner.
    /// Uses configurable component, field, prefab, and asset rules instead of hard-coded gameplay checks.
    /// </summary>
    public sealed class AudioCoverageContextWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtility.AudioSetupCoverage.";
        private const string PrefProfileGuid = PrefPrefix + "ProfileGuid";
        private const string PrefSelectionOnly = PrefPrefix + "SelectionOnly";
        private const string PrefIncludeSceneRefs = PrefPrefix + "IncludeSceneRefs";
        private const string PrefIncludeTerrainProfiles = PrefPrefix + "IncludeTerrainProfiles";
        private const string PrefIncludeMatrices = PrefPrefix + "IncludeMatrices";
        private const string PrefIncludeComponentExpectations = PrefPrefix + "IncludeComponentExpectations";
        private const string PrefIncludeRequiredFields = PrefPrefix + "IncludeRequiredFields";
        private const string PrefConfigHeight = PrefPrefix + "ConfigHeight";

        private Vector2 _mainScroll;
        private Vector2 _configScroll;
        private Vector2 _resultsScroll;
        private Vector2 _scanIssueScroll;
        private AudioCoverageProfileSO _profile;
        private readonly List<AudioCoverageContextResult> _results = new List<AudioCoverageContextResult>();
        private readonly PungentScanSession _scanSession = new PungentScanSession("audio-setup-coverage", "Audio Setup Coverage");
        private string _status = "Ready.";
        private string _resultSourceBanner = string.Empty;
        private bool _selectionOnly;
        private bool _includeSceneReferences = true;
        private bool _includeTerrainProfiles = true;
        private bool _includeInteractionMatrices = true;
        private bool _includeComponentExpectations = true;
        private bool _includeRequiredFields = true;
        private float _configHeight = 184f;

        public static void OpenGeneric()
        {
            Open();
        }

        public static void Open()
        {
            AudioCoverageContextWindow window = GetWindow<AudioCoverageContextWindow>("Audio Setup Coverage");
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        public static bool RunCoordinatorScan(out string status)
        {
            status = "Audio Setup Coverage scan did not run.";
            AudioCoverageContextWindow window = null;
            bool destroyWhenDone = false;
            try
            {
                window = GetCoordinatorInstance(out destroyWhenDone);
                window.RunScan();
                status = window._status;
                return true;
            }
            catch (Exception ex)
            {
                status = "Audio Setup Coverage scan failed: " + ex.Message;
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
            AudioCoverageProfileSO profile = LoadCoordinatorProfile();

            if (profile == null)
            {
                reason = "no AudioCoverageProfileSO is configured";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public static PungentAuditScanJob CreateAuditJob(PungentAuditScanMode mode)
        {
            return AudioCoverageContextScanner.CreateAuditJob(LoadCoordinatorProfile(), LoadCoordinatorOptions(), mode);
        }

        internal static AudioCoverageProfileSO LoadCoordinatorProfile()
        {
            AudioCoverageProfileSO profile = LoadAssetFromGuid<AudioCoverageProfileSO>(UtilityWindowPrefs.GetString(PrefProfileGuid, string.Empty));
            if (profile == null)
                profile = LoadAssetFromGuid<AudioCoverageProfileSO>(SessionState.GetString("GenericUtility.AudioCoverage.ActiveProfileGuid", string.Empty));
            if (profile == null && Selection.activeObject is AudioCoverageProfileSO selectedProfile)
                profile = selectedProfile;
            return profile;
        }

        internal static AudioCoverageContextScanOptions LoadCoordinatorOptions()
        {
            return new AudioCoverageContextScanOptions
            {
                SelectionOnly = UtilityWindowPrefs.GetBool(PrefSelectionOnly, false),
                IncludeSceneReferences = UtilityWindowPrefs.GetBool(PrefIncludeSceneRefs, true),
                IncludeTerrainProfiles = UtilityWindowPrefs.GetBool(PrefIncludeTerrainProfiles, true),
                IncludeInteractionMatrices = UtilityWindowPrefs.GetBool(PrefIncludeMatrices, true),
                IncludeComponentExpectations = UtilityWindowPrefs.GetBool(PrefIncludeComponentExpectations, true),
                IncludeRequiredFields = UtilityWindowPrefs.GetBool(PrefIncludeRequiredFields, true)
            };
        }

        private static AudioCoverageContextWindow GetCoordinatorInstance(out bool destroyWhenDone)
        {
            AudioCoverageContextWindow[] existing = Resources.FindObjectsOfTypeAll<AudioCoverageContextWindow>();
            if (existing != null && existing.Length > 0)
            {
                destroyWhenDone = false;
                return existing[0];
            }
            destroyWhenDone = true;
            return CreateInstance<AudioCoverageContextWindow>();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Audio Setup Coverage");
            LoadPrefs();
            LoadRememberedProfile();
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
                "Audio Setup Coverage",
                "Validate scene references, prefab component expectations, required config fields, terrain profiles, and interaction matrices from a reusable coverage profile.",
                _status);

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawConfigPanel();
            UtilityWindowTheme.VerticalResizeHandle(ref _configHeight, 120f, 360f, SavePrefs);
            DrawResultsPanel();
            EditorGUILayout.EndScrollView();
        }

        private void LoadPrefs()
        {
            _selectionOnly = UtilityWindowPrefs.GetBool(PrefSelectionOnly, _selectionOnly);
            _includeSceneReferences = UtilityWindowPrefs.GetBool(PrefIncludeSceneRefs, _includeSceneReferences);
            _includeTerrainProfiles = UtilityWindowPrefs.GetBool(PrefIncludeTerrainProfiles, _includeTerrainProfiles);
            _includeInteractionMatrices = UtilityWindowPrefs.GetBool(PrefIncludeMatrices, _includeInteractionMatrices);
            _includeComponentExpectations = UtilityWindowPrefs.GetBool(PrefIncludeComponentExpectations, _includeComponentExpectations);
            _includeRequiredFields = UtilityWindowPrefs.GetBool(PrefIncludeRequiredFields, _includeRequiredFields);
            _configHeight = UtilityWindowPrefs.GetFloat(PrefConfigHeight, _configHeight);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefSelectionOnly, _selectionOnly);
            UtilityWindowPrefs.SetBool(PrefIncludeSceneRefs, _includeSceneReferences);
            UtilityWindowPrefs.SetBool(PrefIncludeTerrainProfiles, _includeTerrainProfiles);
            UtilityWindowPrefs.SetBool(PrefIncludeMatrices, _includeInteractionMatrices);
            UtilityWindowPrefs.SetBool(PrefIncludeComponentExpectations, _includeComponentExpectations);
            UtilityWindowPrefs.SetBool(PrefIncludeRequiredFields, _includeRequiredFields);
            UtilityWindowPrefs.SetFloat(PrefConfigHeight, _configHeight);
            StoreAssetGuid(PrefProfileGuid, _profile);
        }

        private void LoadRememberedProfile()
        {
            _profile = LoadAssetFromGuid<AudioCoverageProfileSO>(UtilityWindowPrefs.GetString(PrefProfileGuid, string.Empty));

            if (_profile == null)
            {
                string activeGuid = SessionState.GetString("GenericUtility.AudioCoverage.ActiveProfileGuid", string.Empty);
                _profile = LoadAssetFromGuid<AudioCoverageProfileSO>(activeGuid);
            }

            if (Selection.activeObject is AudioCoverageProfileSO selectedProfile)
                _profile = selectedProfile;
        }

        private void DrawConfigPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Configuration", UtilityWindowTheme.Blue, _profile != null ? _profile.profileName : "No Profile");
                _configScroll = EditorGUILayout.BeginScrollView(_configScroll, GUILayout.Height(_configHeight));

                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _profile = (AudioCoverageProfileSO)EditorGUILayout.ObjectField(
                        new GUIContent("Coverage Profile", "Profile containing setup scan configuration and binding expectations."),
                        _profile,
                        typeof(AudioCoverageProfileSO),
                        false);

                    if (GUILayout.Button("Use Selection", GUILayout.Width(104f)) && Selection.activeObject is AudioCoverageProfileSO selectedProfile)
                    {
                        _profile = selectedProfile;
                        SavePrefs();
                    }

                    using (new EditorGUI.DisabledScope(_profile == null))
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(72f)))
                        {
                            Selection.activeObject = _profile;
                            EditorGUIUtility.PingObject(_profile);
                        }
                    }
                }

                _selectionOnly = EditorGUILayout.ToggleLeft(new GUIContent("Scan Selection Only", "Only validate selected GameObjects and their children for scene-object checks."), _selectionOnly);
                _includeSceneReferences = EditorGUILayout.ToggleLeft(new GUIContent("Scene Collider Reference Coverage", "Check colliders for the configured scene reference component and reference field."), _includeSceneReferences);
                _includeTerrainProfiles = EditorGUILayout.ToggleLeft(new GUIContent("Terrain Profile Coverage", "Check configured terrain audio profile assets if their type exists."), _includeTerrainProfiles);
                _includeInteractionMatrices = EditorGUILayout.ToggleLeft(new GUIContent("Interaction Matrix Coverage", "Check configured audio interaction matrix assets if their type exists."), _includeInteractionMatrices);
                _includeComponentExpectations = EditorGUILayout.ToggleLeft(new GUIContent("Prefab Component Expectations", "Check profile component-pair rules without compile-time type references."), _includeComponentExpectations);
                _includeRequiredFields = EditorGUILayout.ToggleLeft(new GUIContent("Required Config Field Coverage", "Check profile-defined ScriptableObject field requirements."), _includeRequiredFields);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                if (_profile == null)
                    EditorGUILayout.HelpBox("Assign an AudioCoverageProfileSO before scanning.", MessageType.Warning);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    using (new EditorGUI.DisabledScope(_profile == null))
                    {
                        if (GUILayout.Button("Run Scan", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                            QueueRunScan();
                        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                        {
                            _results.Clear();
                            _scanSession.Clear();
                            _status = "Cleared.";
                            SavePrefs();
                        }
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open Catalog Coverage", EditorStyles.toolbarButton, GUILayout.Width(140f)))
                        AudioCoverageWindow.Open();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawResultsPanel()
        {
            int warnings = 0;
            int errors = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].Severity == AudioCoverageResultSeverity.Warning)
                    warnings++;
                else if (_results[i].Severity == AudioCoverageResultSeverity.Error)
                    errors++;
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(errors > 0 ? UtilityWindowTheme.Red : warnings > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Scan Results", errors > 0 ? UtilityWindowTheme.Red : warnings > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Teal, _results.Count.ToString());
                PungentScanGUI.DrawResultHeader(_scanSession.Result, _resultSourceBanner);
                PungentScanGUI.DrawIssueList(_scanSession.Result, ref _scanIssueScroll, 150f, "No shared scan issues to display.");
                EditorGUILayout.Space(4f);

                if (_results.Count == 0)
                    EditorGUILayout.HelpBox("Run a scan to validate the current profile against open scenes, prefabs, and assets.", MessageType.Info);

                _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll, GUILayout.MinHeight(260f));
                for (int i = 0; i < _results.Count; i++)
                    DrawResult(_results[i]);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawResult(AudioCoverageContextResult result)
        {
            if (result == null)
                return;

            Color tint = result.Severity == AudioCoverageResultSeverity.Error
                ? UtilityWindowTheme.Red
                : result.Severity == AudioCoverageResultSeverity.Warning
                    ? UtilityWindowTheme.Amber
                    : UtilityWindowTheme.Green;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.14f, 0.06f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(result.Category, UtilityWindowTheme.SectionHeaderStyle, GUILayout.Width(190f));
                    UtilityWindowTheme.CountPill(result.Severity.ToString(), tint, 78f);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(result.Context == null))
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(52f)))
                        {
                            EditorGUIUtility.PingObject(result.Context);
                            Selection.activeObject = result.Context;
                        }
                    }
                }

                EditorGUILayout.LabelField(result.Message, UtilityWindowTheme.CardLabelStyle);
                if (!string.IsNullOrWhiteSpace(result.Path))
                    EditorGUILayout.LabelField(result.Path, UtilityWindowTheme.PathLabelStyle);
            }
        }

        private void QueueRunScan()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    RunScan();
            };
        }

        private void RunScan()
        {
            _results.Clear();
            PungentScanResult scan = _scanSession.Begin(_selectionOnly ? PungentScanScope.Selection : PungentScanScope.ProjectAssets, _selectionOnly ? "Selection" : "Open Scenes + Project Assets");

            try
            {
                AudioCoverageContextScanOptions options = new AudioCoverageContextScanOptions
                {
                    SelectionOnly = _selectionOnly,
                    IncludeSceneReferences = _includeSceneReferences,
                    IncludeTerrainProfiles = _includeTerrainProfiles,
                    IncludeInteractionMatrices = _includeInteractionMatrices,
                    IncludeComponentExpectations = _includeComponentExpectations,
                    IncludeRequiredFields = _includeRequiredFields
                };

                _results.AddRange(AudioCoverageContextScanner.Run(_profile, options));
                AudioCoverageContextScanner.AddResultsToScan(scan, _results);

                _status = _profile == null ? "No profile assigned." : $"Scan complete: {_results.Count} result(s).";
                _scanSession.Complete(_results.Count, _results.Count, 0, 0, _status);
            }
            catch (Exception exception)
            {
                _status = "Scan failed: " + exception.Message;
                _scanSession.Fail(exception, _status);
            }
        }
        private static void StoreAssetGuid(string key, Object asset)
        {
            string guid = string.Empty;
            if (asset != null)
                guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            UtilityWindowPrefs.SetString(key, guid);
        }

        private static T LoadAssetFromGuid<T>(string guid) where T : Object
        {
            if (string.IsNullOrWhiteSpace(guid))
                return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

    }

    internal sealed class AudioSetupCoverageAuditProvider : IPungentAuditScanProvider
    {
        public string ProviderId => "audio-setup-coverage";
        public string DisplayName => "Audio Setup Coverage";
        public string Description => "Validate reusable audio setup coverage profiles against scenes, prefabs, fields, and assets.";
        public string OpenButtonLabel => "Open Audio Setup Coverage";
        public string RunButtonLabel => "Run Audio Setup Scan";
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
            return AudioCoverageContextWindow.TryGetCoordinatorNotConfiguredReason(out reason);
        }

        public PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
        {
            return AudioCoverageContextWindow.CreateAuditJob(mode);
        }

        public void OpenWindow()
        {
            AudioCoverageContextWindow.Open();
        }
    }
    #endif

}


