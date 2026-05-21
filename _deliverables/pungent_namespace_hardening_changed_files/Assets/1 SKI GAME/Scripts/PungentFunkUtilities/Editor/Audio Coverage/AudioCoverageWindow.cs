using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Audio;

namespace PungentFunk.Utilities.Editor.Audio
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Themed, profile-driven audio catalog coverage window.
    /// This window uses AudioCoverageProfileSO schema fields, cue bindings, and reflection-based diagnostics
    /// so it can audit project-specific audio coverage without compile-time dependencies on gameplay systems.
    /// </summary>
    public sealed class AudioCoverageWindow : EditorWindow
    {
        private const string SelectedCueSessionKey = "GenericUtility.AudioCoverage.SelectedCueId";
        private const string ActiveProfileGuidSessionKey = "GenericUtility.AudioCoverage.ActiveProfileGuid";

        private const string PrefPrefix = "GenericUtility.AudioCatalogCoverage.";
        private const string PrefCatalogGuid = PrefPrefix + "CatalogGuid";
        private const string PrefProfileGuid = PrefPrefix + "ProfileGuid";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefTab = PrefPrefix + "Tab";
        private const string PrefShowValid = PrefPrefix + "ShowValid";
        private const string PrefShowWarning = PrefPrefix + "ShowWarning";
        private const string PrefShowInvalid = PrefPrefix + "ShowInvalid";
        private const string PrefShowMissing = PrefPrefix + "ShowMissing";
        private const string PrefConfigHeight = PrefPrefix + "ConfigHeight";
        private const string PrefLeftWidth = PrefPrefix + "LeftWidth";
        private const string PrefBindingCue = PrefPrefix + "BindingCue";

        private enum CueStatus
        {
            Valid,
            Warning,
            Invalid,
            Missing
        }

        private enum WindowTab
        {
            CatalogAudit,
            CueBindings,
            RuntimeSources,
            Settings
        }

        private sealed class CueAuditRow
        {
            public string CueName;
            public CueStatus Status;
            public bool HasCoverage;
            public bool IsIgnored;
            public int BindingCount;
            public int ScriptReferenceCount;
            public readonly List<int> EntryIndices = new List<int>();
            public readonly List<string> Issues = new List<string>();
        }

        private sealed class CatalogEntryInfo
        {
            public string CueName;
            public int Index;
            public bool HasClipProperty;
            public int ClipSlots;
            public int NonNullClipSlots;
            public bool HasVolume;
            public float Volume;
            public bool HasPitchRange;
            public Vector2 PitchRange;
            public bool HasSpatialBlend;
            public float SpatialBlend;
            public bool HasMinDistance;
            public float MinDistance;
            public bool HasMaxDistance;
            public float MaxDistance;
            public bool HasMixerGroup;
            public bool MixerGroupAssigned;
        }

        private sealed class ScriptReferenceHit
        {
            public string Path;
            public string ScriptName;
        }

        private Vector2 _mainScroll;
        private Vector2 _configScroll;
        private Vector2 _auditScroll;
        private Vector2 _bindingListScroll;
        private Vector2 _bindingDetailScroll;
        private Vector2 _runtimeScroll;
        private Vector2 _settingsScroll;

        private ScriptableObject _catalog;
        private AudioCoverageProfileSO _profile;

        private readonly List<CueAuditRow> _rows = new List<CueAuditRow>();
        private readonly Dictionary<string, List<CatalogEntryInfo>> _catalogEntriesByCue = new Dictionary<string, List<CatalogEntryInfo>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ScriptReferenceHit>> _scriptHitsByCue = new Dictionary<string, List<ScriptReferenceHit>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _runtimeCountsByTypeName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _missingRuntimeTypes = new HashSet<string>(StringComparer.Ordinal);

        private string _search = string.Empty;
        private string _status = "Ready.";
        private string _bindingCueName = string.Empty;
        private bool _showValid = true;
        private bool _showWarning = true;
        private bool _showInvalid = true;
        private bool _showMissing = true;
        private WindowTab _tab = WindowTab.CatalogAudit;
        private float _configHeight = 176f;
        private float _leftWidth = 330f;

        [MenuItem("Tools/Utilities/Audio/Audio Catalog Coverage", priority = 1200)]
        public static void Open()
        {
            AudioCoverageWindow window = GetWindow<AudioCoverageWindow>("Audio Coverage");
            window.minSize = new Vector2(680f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Audio Coverage");
            LoadPrefs();
            LoadRememberedAssets();
            RebuildAll(refreshScriptReferences: true);
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Audio Catalog Coverage",
                "Audit cue catalog entries and define project-specific component/event bindings without hard-coded gameplay dependencies.",
                _status);

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawConfigPanel();
            UtilityWindowTheme.VerticalResizeHandle(ref _configHeight, 116f, 370f, SavePrefs);
            DrawTabToolbar();

            switch (_tab)
            {
                case WindowTab.CatalogAudit:
                    DrawCatalogAuditTab();
                    break;
                case WindowTab.CueBindings:
                    DrawCueBindingsTab();
                    break;
                case WindowTab.RuntimeSources:
                    DrawRuntimeSourcesTab();
                    break;
                case WindowTab.Settings:
                    DrawSettingsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void LoadPrefs()
        {
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _tab = (WindowTab)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefTab, (int)WindowTab.CatalogAudit), 0, Enum.GetValues(typeof(WindowTab)).Length - 1);
            _showValid = UtilityWindowPrefs.GetBool(PrefShowValid, _showValid);
            _showWarning = UtilityWindowPrefs.GetBool(PrefShowWarning, _showWarning);
            _showInvalid = UtilityWindowPrefs.GetBool(PrefShowInvalid, _showInvalid);
            _showMissing = UtilityWindowPrefs.GetBool(PrefShowMissing, _showMissing);
            _configHeight = UtilityWindowPrefs.GetFloat(PrefConfigHeight, _configHeight);
            _leftWidth = UtilityWindowPrefs.GetFloat(PrefLeftWidth, _leftWidth);
            _bindingCueName = UtilityWindowPrefs.GetString(PrefBindingCue, _bindingCueName);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetInt(PrefTab, (int)_tab);
            UtilityWindowPrefs.SetBool(PrefShowValid, _showValid);
            UtilityWindowPrefs.SetBool(PrefShowWarning, _showWarning);
            UtilityWindowPrefs.SetBool(PrefShowInvalid, _showInvalid);
            UtilityWindowPrefs.SetBool(PrefShowMissing, _showMissing);
            UtilityWindowPrefs.SetFloat(PrefConfigHeight, _configHeight);
            UtilityWindowPrefs.SetFloat(PrefLeftWidth, _leftWidth);
            UtilityWindowPrefs.SetString(PrefBindingCue, _bindingCueName);

            StoreAssetGuid(PrefCatalogGuid, _catalog);
            StoreAssetGuid(PrefProfileGuid, _profile);
            if (_profile != null)
                SessionState.SetString(ActiveProfileGuidSessionKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_profile)));
            if (!string.IsNullOrWhiteSpace(_bindingCueName))
                SessionState.SetString(SelectedCueSessionKey, _bindingCueName);
        }

        private void LoadRememberedAssets()
        {
            _profile = LoadAssetFromGuid<AudioCoverageProfileSO>(UtilityWindowPrefs.GetString(PrefProfileGuid, string.Empty));
            _catalog = LoadAssetFromGuid<ScriptableObject>(UtilityWindowPrefs.GetString(PrefCatalogGuid, string.Empty));

            if (_profile == null)
            {
                string activeGuid = SessionState.GetString(ActiveProfileGuidSessionKey, string.Empty);
                _profile = LoadAssetFromGuid<AudioCoverageProfileSO>(activeGuid);
            }

            if (_profile == null)
                _profile = FindFirstAsset<AudioCoverageProfileSO>();

            if (_catalog == null && _profile != null)
                _catalog = FindFirstCatalogAsset();

            if (Selection.activeObject is AudioCoverageProfileSO selectedProfile)
                _profile = selectedProfile;
            else if (Selection.activeObject is ScriptableObject selectedAsset && IsCatalogCompatible(selectedAsset))
                _catalog = selectedAsset;
        }

        private void DrawConfigPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Configuration", UtilityWindowTheme.Blue, _rows.Count.ToString());
                _configScroll = EditorGUILayout.BeginScrollView(_configScroll, GUILayout.Height(_configHeight));

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _profile = (AudioCoverageProfileSO)EditorGUILayout.ObjectField(
                        new GUIContent("Coverage Profile", "Data-driven cue/component/event binding rules used by this window."),
                        _profile,
                        typeof(AudioCoverageProfileSO),
                        false);
                    if (EditorGUI.EndChangeCheck())
                        RebuildAll(refreshScriptReferences: true);

                    if (GUILayout.Button("Create", GUILayout.Width(72f)))
                        CreateProfileAsset();

                    using (new EditorGUI.DisabledScope(_profile == null))
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(72f)))
                        {
                            Selection.activeObject = _profile;
                            EditorGUIUtility.PingObject(_profile);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _catalog = (ScriptableObject)EditorGUILayout.ObjectField(
                        new GUIContent("Catalog Asset", "Optional ScriptableObject catalog to validate using the schema fields defined in the active profile."),
                        _catalog,
                        typeof(ScriptableObject),
                        false);
                    if (EditorGUI.EndChangeCheck())
                        RebuildAll(refreshScriptReferences: false);

                    if (GUILayout.Button("Find", GUILayout.Width(64f)))
                    {
                        _catalog = FindFirstCatalogAsset();
                        RebuildAll(refreshScriptReferences: false);
                    }

                    using (new EditorGUI.DisabledScope(_catalog == null))
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(72f)))
                        {
                            Selection.activeObject = _catalog;
                            EditorGUIUtility.PingObject(_catalog);
                        }
                    }
                }

                if (_profile != null && _catalog != null && !IsCatalogCompatible(_catalog))
                    EditorGUILayout.HelpBox($"Catalog asset type does not match profile catalog type '{_profile.catalogTypeName}'. Validation will still attempt to use the configured serialized field names.", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.MinWidth(130f));
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                        RebuildAll(refreshScriptReferences: true);
                    if (GUILayout.Button("Open Setup Coverage", EditorStyles.toolbarButton, GUILayout.Width(132f)))
                        AudioCoverageContextWindow.Open();
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _showValid = GUILayout.Toggle(_showValid, "Valid", EditorStyles.miniButtonLeft, GUILayout.Width(58f));
                    _showWarning = GUILayout.Toggle(_showWarning, "Warning", EditorStyles.miniButtonMid, GUILayout.Width(74f));
                    _showInvalid = GUILayout.Toggle(_showInvalid, "Invalid", EditorStyles.miniButtonMid, GUILayout.Width(66f));
                    _showMissing = GUILayout.Toggle(_showMissing, "Missing", EditorStyles.miniButtonRight, GUILayout.Width(70f));
                    GUILayout.FlexibleSpace();
                }

                if (_profile == null)
                    EditorGUILayout.HelpBox("Assign or create an AudioCoverageProfileSO to define catalog schema, cue/component/event links, and setup scan rules.", MessageType.Warning);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTabToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawToolbarTab(WindowTab.CatalogAudit, "Catalog Audit");
                DrawToolbarTab(WindowTab.CueBindings, "Cue Bindings");
                DrawToolbarTab(WindowTab.RuntimeSources, "Runtime Sources");
                DrawToolbarTab(WindowTab.Settings, "Settings");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawToolbarTab(WindowTab tab, string label)
        {
            bool selected = _tab == tab;
            bool next = GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(Mathf.Max(84f, label.Length * 8f)));
            if (next && !selected)
            {
                _tab = tab;
                SavePrefs();
            }
        }

        private void DrawCatalogAuditTab()
        {
            DrawSummaryPanel();
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Cue Coverage", UtilityWindowTheme.Purple, CountFilteredRows().ToString());

                if (_catalog == null)
                    EditorGUILayout.HelpBox("Assign a catalog asset to validate serialized cue entries. Binding/script coverage can still be edited without a catalog.", MessageType.Info);

                _auditScroll = EditorGUILayout.BeginScrollView(_auditScroll, GUILayout.MinHeight(260f));
                for (int i = 0; i < _rows.Count; i++)
                {
                    CueAuditRow row = _rows[i];
                    if (!ShouldShowRow(row))
                        continue;

                    DrawCueRow(row);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSummaryPanel()
        {
            int valid = 0;
            int warning = 0;
            int invalid = 0;
            int missing = 0;
            int covered = 0;
            int ignored = 0;

            for (int i = 0; i < _rows.Count; i++)
            {
                CueAuditRow row = _rows[i];
                switch (row.Status)
                {
                    case CueStatus.Valid: valid++; break;
                    case CueStatus.Warning: warning++; break;
                    case CueStatus.Invalid: invalid++; break;
                    case CueStatus.Missing: missing++; break;
                }

                if (row.HasCoverage)
                    covered++;
                if (row.IsIgnored)
                    ignored++;
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Summary", UtilityWindowTheme.Teal, _rows.Count.ToString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"Valid {valid}", UtilityWindowTheme.Green, 82f);
                    UtilityWindowTheme.CountPill($"Warnings {warning}", UtilityWindowTheme.Amber, 98f);
                    UtilityWindowTheme.CountPill($"Invalid {invalid}", UtilityWindowTheme.Red, 88f);
                    UtilityWindowTheme.CountPill($"Missing {missing}", UtilityWindowTheme.Neutral, 94f);
                    UtilityWindowTheme.CountPill($"Covered {covered}", UtilityWindowTheme.Blue, 94f);
                    if (ignored > 0)
                        UtilityWindowTheme.CountPill($"Ignored {ignored}", UtilityWindowTheme.Neutral, 92f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCueRow(CueAuditRow row)
        {
            Color statusColor = GetStatusColor(row.Status);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(statusColor, 0.16f, 0.08f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(row.CueName, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(row.Status.ToString(), statusColor, 86f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(row.HasCoverage ? "Covered" : "Uncovered", row.HasCoverage ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 92f);
                    UtilityWindowTheme.CountPill($"Entries {row.EntryIndices.Count}", UtilityWindowTheme.Blue, 82f);
                    UtilityWindowTheme.CountPill($"Bindings {row.BindingCount}", UtilityWindowTheme.Teal, 92f);
                    UtilityWindowTheme.CountPill($"Script refs {row.ScriptReferenceCount}", UtilityWindowTheme.Purple, 106f);
                    if (row.IsIgnored)
                        UtilityWindowTheme.CountPill("Ignored", UtilityWindowTheme.Neutral, 74f);
                    GUILayout.FlexibleSpace();
                }

                if (row.Issues.Count > 0)
                {
                    EditorGUILayout.Space(2f);
                    for (int i = 0; i < row.Issues.Count; i++)
                        EditorGUILayout.LabelField("� " + row.Issues[i], UtilityWindowTheme.MutedMiniLabelStyle);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Bindings", UtilityWindowTheme.Teal, GUILayout.Width(88f)))
                    {
                        _bindingCueName = row.CueName;
                        _tab = WindowTab.CueBindings;
                        SavePrefs();
                    }

                    if (_scriptHitsByCue.TryGetValue(row.CueName, out List<ScriptReferenceHit> hits) && hits.Count > 0)
                    {
                        if (UtilityWindowTheme.TintedButton("Ping Script", UtilityWindowTheme.Purple, GUILayout.Width(92f)))
                        {
                            Object script = AssetDatabase.LoadAssetAtPath<MonoScript>(hits[0].Path);
                            if (script != null)
                                EditorGUIUtility.PingObject(script);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCueBindingsTab()
        {
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Create or assign an AudioCoverageProfileSO before editing cue bindings.", MessageType.Warning);
                return;
            }

            string[] cueNames = GetCueNames();
            if (string.IsNullOrWhiteSpace(_bindingCueName) || Array.IndexOf(cueNames, _bindingCueName) < 0)
                _bindingCueName = cueNames.Length > 0 ? cueNames[0] : string.Empty;

            float available = Mathf.Max(360f, position.width - 36f);
            _leftWidth = Mathf.Clamp(_leftWidth, 260f, available - 320f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBindingCueList(_leftWidth, cueNames);
                UtilityWindowTheme.HorizontalResizeHandle(ref _leftWidth, 260f, available - 320f, SavePrefs);
                DrawBindingEditor(Mathf.Max(300f, available - _leftWidth - 12f), cueNames);
            }
        }

        private void DrawBindingCueList(float width, string[] cueNames)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), GUILayout.Width(width)))
            {
                UtilityWindowTheme.SectionTitle("Cue Binding Index", UtilityWindowTheme.Teal, cueNames.Length.ToString());
                _bindingListScroll = EditorGUILayout.BeginScrollView(_bindingListScroll, GUILayout.MinHeight(300f));

                for (int i = 0; i < cueNames.Length; i++)
                {
                    string cueName = cueNames[i];
                    if (!MatchesSearch(cueName))
                        continue;

                    int count = _profile.CountBindingsForCue(cueName, includeIgnored: false);
                    bool ignored = _profile.IsCueIgnored(cueName);
                    bool selected = string.Equals(_bindingCueName, cueName, StringComparison.Ordinal);

                    using (new EditorGUILayout.HorizontalScope(selected ? UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.24f, 0.12f, 4, 2) : GUIStyle.none))
                    {
                        if (GUILayout.Button(cueName, EditorStyles.label, GUILayout.MinWidth(120f)))
                            _bindingCueName = cueName;

                        GUILayout.FlexibleSpace();
                        string pill = ignored ? "Ignored" : count.ToString();
                        UtilityWindowTheme.CountPill(pill, count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 62f);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBindingEditor(float width, string[] cueNames)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue), GUILayout.Width(width)))
            {
                UtilityWindowTheme.SectionTitle("Selected Cue Bindings", UtilityWindowTheme.Blue, _bindingCueName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    int currentCueIndex = Mathf.Max(0, Array.IndexOf(cueNames, _bindingCueName));
                    int newCueIndex = cueNames.Length > 0 ? EditorGUILayout.Popup(new GUIContent("Cue", "Cue to inspect/edit."), currentCueIndex, cueNames) : -1;
                    if (newCueIndex >= 0 && newCueIndex < cueNames.Length)
                        _bindingCueName = cueNames[newCueIndex];

                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_bindingCueName)))
                    {
                        if (UtilityWindowTheme.TintedButton("Add Binding", UtilityWindowTheme.Green, GUILayout.Width(112f)))
                            AddBindingForCue(_bindingCueName);

                        if (UtilityWindowTheme.TintedButton("Mark Ignored", UtilityWindowTheme.Neutral, GUILayout.Width(112f)))
                            AddIgnoredBindingForCue(_bindingCueName);
                    }
                }

                _bindingDetailScroll = EditorGUILayout.BeginScrollView(_bindingDetailScroll, GUILayout.MinHeight(300f));

                bool found = false;
                for (int i = 0; i < _profile.cueBindings.Count; i++)
                {
                    AudioCueBindingRule binding = _profile.cueBindings[i];
                    if (binding == null || !string.Equals(binding.cueName, _bindingCueName, StringComparison.Ordinal))
                        continue;

                    found = true;
                    DrawBindingCard(binding, i, cueNames);
                }

                if (!found)
                    EditorGUILayout.HelpBox("No binding rules exist for this cue yet. Add a binding to define which component/event/source is expected to play it.", MessageType.Info);

                DrawScriptReferencePreview(_bindingCueName);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawScriptReferencePreview(string cueName)
        {
            if (string.IsNullOrWhiteSpace(cueName))
                return;

            if (!_scriptHitsByCue.TryGetValue(cueName, out List<ScriptReferenceHit> hits) || hits.Count == 0)
                return;

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.14f, 0.06f, 6, 3)))
            {
                UtilityWindowTheme.SectionTitle("Script References", UtilityWindowTheme.Purple, hits.Count.ToString());
                for (int i = 0; i < Mathf.Min(hits.Count, 8); i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(hits[i].ScriptName, UtilityWindowTheme.PathLabelStyle);
                        if (GUILayout.Button("Ping", GUILayout.Width(50f)))
                        {
                            Object script = AssetDatabase.LoadAssetAtPath<MonoScript>(hits[i].Path);
                            if (script != null)
                                EditorGUIUtility.PingObject(script);
                        }
                    }
                }
            }
        }

        private void DrawBindingCard(AudioCueBindingRule binding, int index, string[] cueNames)
        {
            if (binding == null)
                return;

            Color tint = binding.ignoreInCoverage ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Teal;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.16f, 0.08f, 7, 4)))
            {
                EditorGUI.BeginChangeCheck();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(binding.displayName) ? $"Binding {index + 1}" : binding.displayName, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (UtilityWindowTheme.TintedButton("Duplicate", UtilityWindowTheme.Blue, GUILayout.Width(86f)))
                    {
                        DuplicateBinding(binding);
                        return;
                    }
                    if (UtilityWindowTheme.TintedButton("Remove", UtilityWindowTheme.Red, GUILayout.Width(72f)))
                    {
                        RemoveBindingAt(index);
                        return;
                    }
                }

                int cueIndex = Mathf.Max(0, Array.IndexOf(cueNames, binding.cueName));
                int nextCueIndex = cueNames.Length > 0 ? EditorGUILayout.Popup(new GUIContent("Cue", "Cue covered by this binding."), cueIndex, cueNames) : -1;
                if (nextCueIndex >= 0 && nextCueIndex < cueNames.Length)
                    binding.cueName = cueNames[nextCueIndex];

                binding.displayName = EditorGUILayout.TextField(new GUIContent("Display Name", "Human-readable binding source label."), binding.displayName);
                binding.sourceKind = (AudioBindingSourceKind)EditorGUILayout.EnumPopup(new GUIContent("Source Kind", "How the cue is linked to gameplay/UI/runtime state."), binding.sourceKind);
                binding.sourceComponentTypeName = EditorGUILayout.TextField(new GUIContent("Source Component", "Component type that owns the source event/state. Use a short class name or full type name."), binding.sourceComponentTypeName);
                binding.sourceMemberName = EditorGUILayout.TextField(new GUIContent("Event / Member", "UnityEvent, C# event, method, state, field, property, or authoring note."), binding.sourceMemberName);
                binding.adapterComponentTypeName = EditorGUILayout.TextField(new GUIContent("Adapter Component", "Optional adapter/binder type expected to bridge this source to audio playback."), binding.adapterComponentTypeName);
                binding.requiredCompanionComponentTypeName = EditorGUILayout.TextField(new GUIContent("Required Companion", "Optional companion component expected on the same prefab/object."), binding.requiredCompanionComponentTypeName);
                binding.triggerKind = (AudioBindingTriggerKind)EditorGUILayout.EnumPopup(new GUIContent("Trigger Kind", "Runtime trigger type."), binding.triggerKind);
                binding.playbackMode = (AudioPlaybackMode)EditorGUILayout.EnumPopup(new GUIContent("Playback Mode", "How this cue should be played."), binding.playbackMode);
                binding.positionSource = (AudioPositionSourceKind)EditorGUILayout.EnumPopup(new GUIContent("Position Source", "How positional/attached playback resolves position or target."), binding.positionSource);
                binding.transformFieldOrPropertyName = EditorGUILayout.TextField(new GUIContent("Transform Field", "Optional transform field/property/child name used by the position source."), binding.transformFieldOrPropertyName);
                binding.validationScope = (AudioBindingValidationScope)EditorGUILayout.EnumPopup(new GUIContent("Validation Scope", "Where setup tools should look for this source."), binding.validationScope);

                using (new EditorGUILayout.HorizontalScope())
                {
                    binding.required = EditorGUILayout.ToggleLeft(new GUIContent("Required", "Report missing/invalid required bindings."), binding.required, GUILayout.Width(92f));
                    binding.allowMultipleSources = EditorGUILayout.ToggleLeft(new GUIContent("Allow Multiple Sources", "Permit more than one runtime source to cover this cue."), binding.allowMultipleSources, GUILayout.Width(170f));
                    binding.ignoreInCoverage = EditorGUILayout.ToggleLeft(new GUIContent("Ignore In Coverage", "Do not warn if this cue is otherwise uncovered."), binding.ignoreInCoverage, GUILayout.Width(150f));
                    GUILayout.FlexibleSpace();
                }

                binding.notes = EditorGUILayout.TextArea(binding.notes, GUILayout.MinHeight(34f));

                DrawBindingDiagnostics(binding);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_profile, "Edit Audio Cue Binding");
                    EditorUtility.SetDirty(_profile);
                    RebuildAll(refreshScriptReferences: false);
                }
            }
        }

        private void DrawBindingDiagnostics(AudioCueBindingRule binding)
        {
            if (binding == null)
                return;

            Type sourceType = FindType(binding.sourceComponentTypeName);
            Type adapterType = FindType(binding.adapterComponentTypeName);
            Type companionType = FindType(binding.requiredCompanionComponentTypeName);

            if (!string.IsNullOrWhiteSpace(binding.sourceComponentTypeName) && sourceType == null)
                EditorGUILayout.HelpBox($"Source component type '{binding.sourceComponentTypeName}' was not found. This is allowed for cross-project profiles, but setup validation cannot locate instances until the type exists.", MessageType.Warning);

            if (!string.IsNullOrWhiteSpace(binding.adapterComponentTypeName) && adapterType == null)
                EditorGUILayout.HelpBox($"Adapter component type '{binding.adapterComponentTypeName}' was not found.", MessageType.Warning);

            if (!string.IsNullOrWhiteSpace(binding.requiredCompanionComponentTypeName) && companionType == null)
                EditorGUILayout.HelpBox($"Required companion component type '{binding.requiredCompanionComponentTypeName}' was not found.", MessageType.Warning);

            if (binding.sourceKind == AudioBindingSourceKind.CSharpEventViaAdapter && string.IsNullOrWhiteSpace(binding.adapterComponentTypeName))
                EditorGUILayout.HelpBox("C# events cannot be generically wired by the editor window. Add an adapter component type here or treat this as manual/script coverage.", MessageType.Info);
        }

        private void DrawRuntimeSourcesTab()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Runtime Source Diagnostics", UtilityWindowTheme.Teal, _runtimeCountsByTypeName.Count.ToString());

                if (_profile == null)
                {
                    EditorGUILayout.HelpBox("Assign a profile to configure runtime source type names.", MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Refresh Scene Counts", UtilityWindowTheme.Green, GUILayout.Width(150f)))
                        RefreshRuntimeSourceCounts();
                    if (UtilityWindowTheme.TintedButton("Add Type", UtilityWindowTheme.Blue, GUILayout.Width(88f)))
                        AddRuntimeTypeName();
                    GUILayout.FlexibleSpace();
                }

                _runtimeScroll = EditorGUILayout.BeginScrollView(_runtimeScroll, GUILayout.MinHeight(260f));

                for (int i = 0; i < _profile.runtimeHookTypeNames.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 2)))
                    {
                        EditorGUI.BeginChangeCheck();
                        _profile.runtimeHookTypeNames[i] = EditorGUILayout.TextField(_profile.runtimeHookTypeNames[i]);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(_profile, "Edit Runtime Source Type");
                            EditorUtility.SetDirty(_profile);
                        }

                        string typeName = _profile.runtimeHookTypeNames[i];
                        bool missing = _missingRuntimeTypes.Contains(typeName);
                        int count = _runtimeCountsByTypeName.TryGetValue(typeName, out int resolvedCount) ? resolvedCount : 0;
                        UtilityWindowTheme.CountPill(missing ? "Missing Type" : count.ToString(), missing ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 94f);

                        if (GUILayout.Button("X", GUILayout.Width(24f)))
                        {
                            RemoveRuntimeTypeNameAt(i);
                            return;
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSettingsTab()
        {
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Assign a profile to edit coverage settings.", MessageType.Warning);
                return;
            }

            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll, GUILayout.MinHeight(320f));

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Profile Schema", UtilityWindowTheme.Blue);
                EditorGUI.BeginChangeCheck();
                _profile.profileName = EditorGUILayout.TextField("Profile Name", _profile.profileName);
                _profile.catalogTypeName = EditorGUILayout.TextField("Catalog Type", _profile.catalogTypeName);
                _profile.cueEnumTypeName = EditorGUILayout.TextField("Cue Enum/Key Type", _profile.cueEnumTypeName);
                _profile.cueListPropertyName = EditorGUILayout.TextField("Cue List Property", _profile.cueListPropertyName);
                _profile.cueIdPropertyName = EditorGUILayout.TextField("Cue ID Property", _profile.cueIdPropertyName);
                _profile.clipsPropertyName = EditorGUILayout.TextField("Clips Property", _profile.clipsPropertyName);
                _profile.volumePropertyName = EditorGUILayout.TextField("Volume Property", _profile.volumePropertyName);
                _profile.pitchRangePropertyName = EditorGUILayout.TextField("Pitch Range Property", _profile.pitchRangePropertyName);
                _profile.spatialBlendPropertyName = EditorGUILayout.TextField("Spatial Blend Property", _profile.spatialBlendPropertyName);
                _profile.minDistancePropertyName = EditorGUILayout.TextField("Min Distance Property", _profile.minDistancePropertyName);
                _profile.maxDistancePropertyName = EditorGUILayout.TextField("Max Distance Property", _profile.maxDistancePropertyName);
                _profile.mixerGroupPropertyName = EditorGUILayout.TextField("Mixer Group Property", _profile.mixerGroupPropertyName);
                _profile.noneCueName = EditorGUILayout.TextField("None Cue Name", _profile.noneCueName);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_profile, "Edit Audio Coverage Profile Schema");
                    EditorUtility.SetDirty(_profile);
                    RebuildAll(refreshScriptReferences: true);
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Coverage Rules", UtilityWindowTheme.Teal);
                EditorGUI.BeginChangeCheck();
                _profile.treatBindingRulesAsCoverage = EditorGUILayout.ToggleLeft("Treat binding rules as coverage", _profile.treatBindingRulesAsCoverage);
                _profile.treatScriptReferencesAsCoverage = EditorGUILayout.ToggleLeft("Treat script references as coverage", _profile.treatScriptReferencesAsCoverage);
                _profile.treatManualBindingsAsCoverage = EditorGUILayout.ToggleLeft("Treat manual-only bindings as coverage", _profile.treatManualBindingsAsCoverage);
                _profile.warnWhenCueHasNoCoverage = EditorGUILayout.ToggleLeft("Warn when cue has no coverage", _profile.warnWhenCueHasNoCoverage);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_profile, "Edit Audio Coverage Settings");
                    EditorUtility.SetDirty(_profile);
                    RebuildAll(refreshScriptReferences: true);
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Script Search Tokens", UtilityWindowTheme.Purple, _profile.scriptSearchTokens.Count.ToString());
                for (int i = 0; i < _profile.scriptSearchTokens.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        _profile.scriptSearchTokens[i] = EditorGUILayout.TextField(_profile.scriptSearchTokens[i]);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(_profile, "Edit Script Search Token");
                            EditorUtility.SetDirty(_profile);
                        }
                        if (GUILayout.Button("X", GUILayout.Width(24f)))
                        {
                            Undo.RecordObject(_profile, "Remove Script Search Token");
                            _profile.scriptSearchTokens.RemoveAt(i);
                            EditorUtility.SetDirty(_profile);
                            RebuildAll(refreshScriptReferences: true);
                            return;
                        }
                    }
                }

                if (UtilityWindowTheme.TintedButton("Add Token", UtilityWindowTheme.Green, GUILayout.Width(100f)))
                {
                    Undo.RecordObject(_profile, "Add Script Search Token");
                    _profile.scriptSearchTokens.Add("PlayCue");
                    EditorUtility.SetDirty(_profile);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void RebuildAll(bool refreshScriptReferences)
        {
            RefreshCatalogEntryCache();
            if (refreshScriptReferences)
                RefreshScriptReferences();
            RefreshRuntimeSourceCounts();
            BuildAuditRows();
            _status = _rows.Count > 0 ? $"{_rows.Count} cue(s) indexed." : "Ready.";
            Repaint();
        }

        private void RefreshCatalogEntryCache()
        {
            _catalogEntriesByCue.Clear();
            if (_catalog == null || _profile == null || string.IsNullOrWhiteSpace(_profile.cueListPropertyName))
                return;

            SerializedObject serializedCatalog = new SerializedObject(_catalog);
            SerializedProperty list = serializedCatalog.FindProperty(_profile.cueListPropertyName);
            if (list == null || !list.isArray)
                return;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                SerializedProperty cueProperty = element.FindPropertyRelative(_profile.cueIdPropertyName);
                string cueName = ReadCueName(cueProperty);
                if (string.IsNullOrWhiteSpace(cueName) || IsNoneCue(cueName))
                    continue;

                CatalogEntryInfo entry = BuildCatalogEntryInfo(cueName, i, element);
                if (!_catalogEntriesByCue.TryGetValue(cueName, out List<CatalogEntryInfo> entries))
                {
                    entries = new List<CatalogEntryInfo>();
                    _catalogEntriesByCue.Add(cueName, entries);
                }
                entries.Add(entry);
            }
        }

        private CatalogEntryInfo BuildCatalogEntryInfo(string cueName, int index, SerializedProperty element)
        {
            CatalogEntryInfo entry = new CatalogEntryInfo
            {
                CueName = cueName,
                Index = index
            };

            SerializedProperty clips = FindRelative(element, _profile.clipsPropertyName);
            entry.HasClipProperty = clips != null;
            if (clips != null && clips.isArray)
            {
                entry.ClipSlots = clips.arraySize;
                for (int i = 0; i < clips.arraySize; i++)
                {
                    SerializedProperty slot = clips.GetArrayElementAtIndex(i);
                    if (slot != null && slot.propertyType == SerializedPropertyType.ObjectReference && slot.objectReferenceValue != null)
                        entry.NonNullClipSlots++;
                }
            }

            entry.HasVolume = TryReadFloat(element, _profile.volumePropertyName, out entry.Volume);
            entry.HasPitchRange = TryReadVector2(element, _profile.pitchRangePropertyName, out entry.PitchRange);
            entry.HasSpatialBlend = TryReadFloat(element, _profile.spatialBlendPropertyName, out entry.SpatialBlend);
            entry.HasMinDistance = TryReadFloat(element, _profile.minDistancePropertyName, out entry.MinDistance);
            entry.HasMaxDistance = TryReadFloat(element, _profile.maxDistancePropertyName, out entry.MaxDistance);

            SerializedProperty mixer = FindRelative(element, _profile.mixerGroupPropertyName);
            entry.HasMixerGroup = mixer != null && mixer.propertyType == SerializedPropertyType.ObjectReference;
            entry.MixerGroupAssigned = !entry.HasMixerGroup || mixer.objectReferenceValue != null;
            return entry;
        }

        private void BuildAuditRows()
        {
            _rows.Clear();
            string[] cueNames = GetCueNames();
            for (int i = 0; i < cueNames.Length; i++)
            {
                string cueName = cueNames[i];
                if (string.IsNullOrWhiteSpace(cueName) || IsNoneCue(cueName))
                    continue;

                CueAuditRow row = new CueAuditRow { CueName = cueName };
                if (_catalogEntriesByCue.TryGetValue(cueName, out List<CatalogEntryInfo> entries))
                {
                    for (int e = 0; e < entries.Count; e++)
                        row.EntryIndices.Add(entries[e].Index);
                }

                if (_profile != null)
                {
                    row.BindingCount = _profile.CountBindingsForCue(cueName, includeIgnored: false);
                    row.IsIgnored = _profile.IsCueIgnored(cueName);
                }

                row.ScriptReferenceCount = _scriptHitsByCue.TryGetValue(cueName, out List<ScriptReferenceHit> hits) ? hits.Count : 0;
                row.HasCoverage = row.IsIgnored || row.BindingCount > 0 || (_profile != null && _profile.treatScriptReferencesAsCoverage && row.ScriptReferenceCount > 0);
                ValidateRow(row);
                _rows.Add(row);
            }
        }

        private void ValidateRow(CueAuditRow row)
        {
            bool hasWarning = false;
            bool hasInvalid = false;

            if (_catalog != null)
            {
                if (!_catalogEntriesByCue.TryGetValue(row.CueName, out List<CatalogEntryInfo> entries) || entries.Count == 0)
                {
                    row.Status = CueStatus.Missing;
                    row.Issues.Add("No catalog entry exists for this cue.");
                    if (row.HasCoverage)
                        row.Issues.Add("This cue has binding/script coverage and should probably exist in the catalog.");
                    return;
                }

                if (entries.Count > 1)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Duplicate catalog entries found: {entries.Count}.");
                }

                for (int i = 0; i < entries.Count; i++)
                    ValidateCatalogEntry(entries[i], row, ref hasWarning, ref hasInvalid);
            }
            else
            {
                hasWarning = true;
                row.Issues.Add("No catalog asset assigned; catalog entry validation skipped.");
            }

            if (_profile == null)
            {
                hasWarning = true;
                row.Issues.Add("No AudioCoverageProfileSO assigned, so component/event binding coverage is unknown.");
            }
            else if (_profile.warnWhenCueHasNoCoverage && !row.HasCoverage && !row.IsIgnored)
            {
                hasWarning = true;
                row.Issues.Add("No profile binding or script reference coverage found for this cue.");
            }

            if (hasInvalid)
                row.Status = CueStatus.Invalid;
            else if (hasWarning)
                row.Status = CueStatus.Warning;
            else
                row.Status = CueStatus.Valid;
        }

        private void ValidateCatalogEntry(CatalogEntryInfo entry, CueAuditRow row, ref bool hasWarning, ref bool hasInvalid)
        {
            if (entry == null)
                return;

            if (entry.HasClipProperty)
            {
                if (entry.ClipSlots == 0)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Entry {entry.Index}: no clips assigned.");
                }
                else if (entry.NonNullClipSlots == 0)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Entry {entry.Index}: all clip slots are null.");
                }
                else if (entry.NonNullClipSlots < entry.ClipSlots)
                {
                    hasWarning = true;
                    row.Issues.Add($"Entry {entry.Index}: some clip slots are null.");
                }
            }

            if (entry.HasVolume && entry.Volume <= 0f)
            {
                hasWarning = true;
                row.Issues.Add($"Entry {entry.Index}: volume is zero or below.");
            }

            if (entry.HasPitchRange)
            {
                if (entry.PitchRange.x > entry.PitchRange.y)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Entry {entry.Index}: pitch range is reversed.");
                }
                else if (Mathf.Abs(entry.PitchRange.y - entry.PitchRange.x) > 0.5f)
                {
                    hasWarning = true;
                    row.Issues.Add($"Entry {entry.Index}: pitch range is unusually wide.");
                }
            }

            if (entry.HasMinDistance && entry.HasMaxDistance && entry.MaxDistance < entry.MinDistance)
            {
                hasInvalid = true;
                row.Issues.Add($"Entry {entry.Index}: max distance is less than min distance.");
            }

            if (entry.HasSpatialBlend && (entry.SpatialBlend < 0f || entry.SpatialBlend > 1f))
            {
                hasWarning = true;
                row.Issues.Add($"Entry {entry.Index}: spatial blend is outside 0-1.");
            }

            if (entry.HasMixerGroup && !entry.MixerGroupAssigned)
            {
                hasWarning = true;
                row.Issues.Add($"Entry {entry.Index}: no mixer group assigned.");
            }
        }

        private void RefreshScriptReferences()
        {
            _scriptHitsByCue.Clear();
            if (_profile == null || !_profile.treatScriptReferencesAsCoverage)
                return;

            string[] cueNames = GetCueNames();
            if (cueNames.Length == 0)
                return;

            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                    continue;

                string text = script.text;
                if (string.IsNullOrEmpty(text))
                    continue;

                if (!MatchesAnyScriptToken(text))
                    continue;

                for (int c = 0; c < cueNames.Length; c++)
                {
                    string cueName = cueNames[c];
                    if (IsNoneCue(cueName))
                        continue;

                    if (!text.Contains(cueName))
                        continue;

                    if (!_scriptHitsByCue.TryGetValue(cueName, out List<ScriptReferenceHit> hits))
                    {
                        hits = new List<ScriptReferenceHit>();
                        _scriptHitsByCue.Add(cueName, hits);
                    }

                    hits.Add(new ScriptReferenceHit
                    {
                        Path = path,
                        ScriptName = script.name
                    });
                }
            }
        }

        private bool MatchesAnyScriptToken(string text)
        {
            if (_profile == null || _profile.scriptSearchTokens == null || _profile.scriptSearchTokens.Count == 0)
                return true;

            for (int i = 0; i < _profile.scriptSearchTokens.Count; i++)
            {
                string token = _profile.scriptSearchTokens[i];
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (text.Contains(token))
                    return true;
            }

            return false;
        }

        private void RefreshRuntimeSourceCounts()
        {
            _runtimeCountsByTypeName.Clear();
            _missingRuntimeTypes.Clear();

            if (_profile == null || _profile.runtimeHookTypeNames == null)
                return;

            for (int i = 0; i < _profile.runtimeHookTypeNames.Count; i++)
            {
                string typeName = _profile.runtimeHookTypeNames[i];
                if (string.IsNullOrWhiteSpace(typeName))
                    continue;

                Type type = FindType(typeName);
                if (type == null || !typeof(Object).IsAssignableFrom(type))
                {
                    _missingRuntimeTypes.Add(typeName);
                    _runtimeCountsByTypeName[typeName] = 0;
                    continue;
                }

                _runtimeCountsByTypeName[typeName] = CountObjectsByType(type);
            }
        }

        private static int CountObjectsByType(Type type)
        {
            if (type == null)
                return 0;

    #if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType(type, FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
    #else
            return Object.FindObjectsOfType(type).Length;
    #endif
        }

        private string[] GetCueNames()
        {
            SortedSet<string> cueNames = new SortedSet<string>(StringComparer.Ordinal);

            Type cueType = _profile != null ? FindType(_profile.cueEnumTypeName) : null;
            if (cueType != null && cueType.IsEnum)
            {
                string[] names = Enum.GetNames(cueType);
                for (int i = 0; i < names.Length; i++)
                {
                    if (!IsNoneCue(names[i]))
                        cueNames.Add(names[i]);
                }
            }

            foreach (string key in _catalogEntriesByCue.Keys)
            {
                if (!IsNoneCue(key))
                    cueNames.Add(key);
            }

            if (_profile != null && _profile.cueBindings != null)
            {
                for (int i = 0; i < _profile.cueBindings.Count; i++)
                {
                    AudioCueBindingRule binding = _profile.cueBindings[i];
                    if (binding != null && !string.IsNullOrWhiteSpace(binding.cueName) && !IsNoneCue(binding.cueName))
                        cueNames.Add(binding.cueName);
                }
            }

            string sessionCue = SessionState.GetString(SelectedCueSessionKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(sessionCue) && !IsNoneCue(sessionCue))
                cueNames.Add(sessionCue);

            string[] result = new string[cueNames.Count];
            cueNames.CopyTo(result);
            return result;
        }

        private string ReadCueName(SerializedProperty cueProperty)
        {
            if (cueProperty == null)
                return string.Empty;

            switch (cueProperty.propertyType)
            {
                case SerializedPropertyType.Enum:
                    if (cueProperty.enumDisplayNames != null && cueProperty.enumValueIndex >= 0 && cueProperty.enumValueIndex < cueProperty.enumDisplayNames.Length)
                        return cueProperty.enumDisplayNames[cueProperty.enumValueIndex];
                    return cueProperty.enumValueIndex.ToString();
                case SerializedPropertyType.String:
                    return cueProperty.stringValue;
                case SerializedPropertyType.ObjectReference:
                    return cueProperty.objectReferenceValue != null ? cueProperty.objectReferenceValue.name : string.Empty;
                case SerializedPropertyType.Integer:
                    return cueProperty.intValue.ToString();
                default:
                    return cueProperty.displayName;
            }
        }

        private bool IsNoneCue(string cueName)
        {
            if (string.IsNullOrWhiteSpace(cueName))
                return true;
            return _profile != null && string.Equals(cueName, _profile.noneCueName, StringComparison.Ordinal);
        }

        private bool ShouldShowRow(CueAuditRow row)
        {
            if (row == null)
                return false;

            if (!MatchesSearch(row.CueName))
                return false;

            return row.Status switch
            {
                CueStatus.Valid => _showValid,
                CueStatus.Warning => _showWarning,
                CueStatus.Invalid => _showInvalid,
                CueStatus.Missing => _showMissing,
                _ => true
            };
        }

        private bool MatchesSearch(string value)
        {
            return string.IsNullOrWhiteSpace(_search) || (!string.IsNullOrEmpty(value) && value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private int CountFilteredRows()
        {
            int count = 0;
            for (int i = 0; i < _rows.Count; i++)
                if (ShouldShowRow(_rows[i]))
                    count++;
            return count;
        }

        private Color GetStatusColor(CueStatus status)
        {
            return status switch
            {
                CueStatus.Valid => UtilityWindowTheme.Green,
                CueStatus.Warning => UtilityWindowTheme.Amber,
                CueStatus.Invalid => UtilityWindowTheme.Red,
                CueStatus.Missing => UtilityWindowTheme.Neutral,
                _ => UtilityWindowTheme.Neutral
            };
        }

        private void AddBindingForCue(string cueName)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(cueName))
                return;

            Undo.RecordObject(_profile, "Add Audio Cue Binding");
            _profile.cueBindings.Add(new AudioCueBindingRule
            {
                cueName = cueName,
                displayName = cueName,
                sourceKind = AudioBindingSourceKind.CSharpEventViaAdapter,
                triggerKind = AudioBindingTriggerKind.CSharpEvent,
                playbackMode = AudioPlaybackMode.PlayAttached,
                positionSource = AudioPositionSourceKind.SourceTransform,
                validationScope = AudioBindingValidationScope.SceneAndPrefabs,
                required = true,
                allowMultipleSources = true
            });
            EditorUtility.SetDirty(_profile);
            RebuildAll(refreshScriptReferences: false);
        }

        private void AddIgnoredBindingForCue(string cueName)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(cueName))
                return;

            Undo.RecordObject(_profile, "Ignore Audio Cue Coverage");
            _profile.cueBindings.Add(new AudioCueBindingRule
            {
                cueName = cueName,
                displayName = "Ignored / Manual",
                sourceKind = AudioBindingSourceKind.ManualCoverageOnly,
                triggerKind = AudioBindingTriggerKind.Manual,
                playbackMode = AudioPlaybackMode.PlayUi,
                positionSource = AudioPositionSourceKind.None,
                validationScope = AudioBindingValidationScope.None,
                required = false,
                allowMultipleSources = true,
                ignoreInCoverage = true,
                notes = "Intentionally ignored by coverage validation."
            });
            EditorUtility.SetDirty(_profile);
            RebuildAll(refreshScriptReferences: false);
        }

        private void DuplicateBinding(AudioCueBindingRule source)
        {
            if (_profile == null || source == null)
                return;

            Undo.RecordObject(_profile, "Duplicate Audio Cue Binding");
            _profile.cueBindings.Add(new AudioCueBindingRule
            {
                cueName = source.cueName,
                displayName = source.displayName + " Copy",
                notes = source.notes,
                sourceKind = source.sourceKind,
                sourceComponentTypeName = source.sourceComponentTypeName,
                sourceMemberName = source.sourceMemberName,
                adapterComponentTypeName = source.adapterComponentTypeName,
                requiredCompanionComponentTypeName = source.requiredCompanionComponentTypeName,
                triggerKind = source.triggerKind,
                playbackMode = source.playbackMode,
                positionSource = source.positionSource,
                transformFieldOrPropertyName = source.transformFieldOrPropertyName,
                validationScope = source.validationScope,
                required = source.required,
                allowMultipleSources = source.allowMultipleSources,
                ignoreInCoverage = source.ignoreInCoverage,
                conditions = source.conditions != null ? new List<AudioBindingCondition>(source.conditions) : new List<AudioBindingCondition>()
            });
            EditorUtility.SetDirty(_profile);
            RebuildAll(refreshScriptReferences: false);
        }

        private void RemoveBindingAt(int index)
        {
            if (_profile == null || index < 0 || index >= _profile.cueBindings.Count)
                return;

            Undo.RecordObject(_profile, "Remove Audio Cue Binding");
            _profile.cueBindings.RemoveAt(index);
            EditorUtility.SetDirty(_profile);
            RebuildAll(refreshScriptReferences: false);
        }

        private void AddRuntimeTypeName()
        {
            if (_profile == null)
                return;

            Undo.RecordObject(_profile, "Add Runtime Source Type");
            _profile.runtimeHookTypeNames.Add("AudioSource");
            EditorUtility.SetDirty(_profile);
            RefreshRuntimeSourceCounts();
        }

        private void RemoveRuntimeTypeNameAt(int index)
        {
            if (_profile == null || index < 0 || index >= _profile.runtimeHookTypeNames.Count)
                return;

            Undo.RecordObject(_profile, "Remove Runtime Source Type");
            _profile.runtimeHookTypeNames.RemoveAt(index);
            EditorUtility.SetDirty(_profile);
            RefreshRuntimeSourceCounts();
        }

        private void CreateProfileAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Audio Coverage Profile",
                "AudioCoverageProfile",
                "asset",
                "Choose where to save the new audio coverage profile.");

            if (string.IsNullOrWhiteSpace(path))
                return;

            AudioCoverageProfileSO profile = CreateInstance<AudioCoverageProfileSO>();
            profile.ResetToGenericDefaults(clearExistingRules: true);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            _profile = profile;
            Selection.activeObject = profile;
            RebuildAll(refreshScriptReferences: true);
        }

        private ScriptableObject FindFirstCatalogAsset()
        {
            if (_profile == null || string.IsNullOrWhiteSpace(_profile.catalogTypeName))
                return null;

            Type catalogType = FindType(_profile.catalogTypeName);
            if (catalogType == null || !typeof(ScriptableObject).IsAssignableFrom(catalogType))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:" + catalogType.Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Object asset = AssetDatabase.LoadAssetAtPath(path, catalogType);
                if (asset is ScriptableObject scriptable)
                    return scriptable;
            }

            return null;
        }

        private bool IsCatalogCompatible(ScriptableObject asset)
        {
            if (asset == null || _profile == null || string.IsNullOrWhiteSpace(_profile.catalogTypeName))
                return false;

            Type expectedType = FindType(_profile.catalogTypeName);
            return expectedType == null || expectedType.IsInstanceOfType(asset);
        }

        private static T LoadAssetFromGuid<T>(string guid) where T : Object
        {
            if (string.IsNullOrWhiteSpace(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T FindFirstAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids == null || guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void StoreAssetGuid(string key, Object asset)
        {
            string guid = string.Empty;
            if (asset != null)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrWhiteSpace(path))
                    guid = AssetDatabase.AssetPathToGUID(path);
            }
            UtilityWindowPrefs.SetString(key, guid);
        }

        private static SerializedProperty FindRelative(SerializedProperty parent, string relativeName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(relativeName))
                return null;
            return parent.FindPropertyRelative(relativeName);
        }

        private static bool TryReadFloat(SerializedProperty parent, string relativeName, out float value)
        {
            value = 0f;
            SerializedProperty property = FindRelative(parent, relativeName);
            if (property == null)
                return false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    value = property.floatValue;
                    return true;
                case SerializedPropertyType.Integer:
                    value = property.intValue;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadVector2(SerializedProperty parent, string relativeName, out Vector2 value)
        {
            value = Vector2.zero;
            SerializedProperty property = FindRelative(parent, relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.Vector2)
                return false;

            value = property.vector2Value;
            return true;
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            Type direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(typeName);
                if (found != null)
                    return found;
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                    continue;

                for (int t = 0; t < types.Length; t++)
                {
                    Type candidate = types[t];
                    if (candidate == null)
                        continue;

                    if (candidate.Name == typeName || candidate.FullName == typeName)
                        return candidate;
                }
            }

            return null;
        }
    }
    #endif

}