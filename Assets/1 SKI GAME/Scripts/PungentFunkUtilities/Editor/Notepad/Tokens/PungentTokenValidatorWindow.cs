using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class PungentTokenValidatorWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.TokenValidator.";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefSelected = PrefPrefix + "SelectedToken";
        private const string PrefShowArchived = PrefPrefix + "ShowArchived";
        private const string PrefShowDeprecated = PrefPrefix + "ShowDeprecated";
        private const string PrefScanFilter = PrefPrefix + "ScanFilter";
        private const string PrefText = PrefPrefix + "Text";
        private const string PrefLeftWidth = PrefPrefix + "LeftWidth";
        private const string PrefRightWidth = PrefPrefix + "RightWidth";
        private const string PrefScanIssuesFoldout = PrefPrefix + "ScanIssuesFoldout";
        private const string PrefShowScanErrors = PrefPrefix + "ShowScanErrors";
        private const string PrefShowScanWarnings = PrefPrefix + "ShowScanWarnings";
        private const string PrefShowScanInfo = PrefPrefix + "ShowScanInfo";
        private const string PrefScanSettingsFoldout = PrefPrefix + "ScanSettingsFoldout";

        private const float MinLeftWidth = 220f;
        private const float MinCenterWidth = 460f;
        private const float MinRightWidth = 320f;
        private const float SplitterWidth = 9f;
        private const float BodyPaddingReserve = 34f;
        private const int MaxStoredProblemUsages = 250;
        private const int MaxStoredUsageRows = 360;
        private const int MaxInlineUsageRows = 20;
        private const int MaxStoredCandidates = 400;
        private const int MaxDisplayedCandidates = 120;

        private readonly PungentScanSession _scanSession = new PungentScanSession("token-validator", "Token Validator");
        private Vector2 _leftScroll;
        private Vector2 _centerScroll;
        private Vector2 _rightScroll;
        private Vector2 _scanIssueScroll;
        private float _leftWidth = 270f;
        private float _rightWidth = 360f;
        private string _search = string.Empty;
        private string _selectedTokenKey = string.Empty;
        private string _text = "Hello {playerName}, inspect {objectName}.";
        private string _scanFilter = "t:TextAsset";
        private bool _showArchived;
        private bool _showDeprecated = true;
        private bool _scanIssuesExpanded;
        private PungentTokenQuickFilter _quickFilter = PungentTokenQuickFilter.All;
        private readonly List<PungentTokenUsage> _lastUsages = new List<PungentTokenUsage>();
        private readonly List<PungentTokenUsage> _inlineUsages = new List<PungentTokenUsage>();
        private readonly List<PungentTokenCandidate> _lastCandidates = new List<PungentTokenCandidate>();
        private string _resultSourceBanner = string.Empty;

        private string _inlineValidationText = null;
        private string _lastUsageSummary = string.Empty;
        private string _candidateSummary = string.Empty;
        private string _status = "Ready.";
        private int _lastUsageRowTotal;
        private int _lastKnownUsageRows;
        private int _lastProblemUsageRows;
        private int _lastHiddenKnownRows;
        private int _lastHiddenProblemRows;
        private int _lastLikelyCandidateCount;
        private int _lastPossibleCandidateCount;
        private int _lastUnlikelyCandidateCount;
        private int _lastNotTokenCandidateCount;
        private int _lastIgnoredCandidateCount;
        private int _lastHiddenCandidateRows;

        private bool _projectScanQueued;
        private bool _projectScanRunning;

        private bool _showScanErrors = true;
        private bool _showScanWarnings = true;
        private bool _showScanInfo = true;
        private bool _scanSettingsExpanded;
        public static void Open()
        {
            PungentTokenValidatorWindow window = GetWindow<PungentTokenValidatorWindow>("Token Validator");
            window.minSize = new Vector2(940f, 520f);
            window.Show();
        }

        public static void OpenAndSelect(string tokenKey)
        {
            PungentTokenValidatorWindow window = GetWindow<PungentTokenValidatorWindow>("Token Validator");
            window.minSize = new Vector2(940f, 520f);
            string clean = PungentTokenDatabase.NormalizeKey(tokenKey);
            window._selectedTokenKey = clean;
            window._search = clean;
            if (PungentTokenDatabase.instance.FindToken(clean) == null && !string.IsNullOrWhiteSpace(clean))
            {
                EditorGUIUtility.systemCopyBuffer = clean;
                window._status = "Token key copied because the token could not be selected: {" + clean + "}.";
            }
            else
            {
                window._status = string.IsNullOrWhiteSpace(clean) ? "Token Validator opened." : "Selected token {" + clean + "}.";
            }
            window.SavePrefs();
            window.Show();
        }

        public static PungentAuditScanJob CreateAuditJob(PungentAuditScanMode mode)
        {
            return CooperativeTokenProjectScan.CreateJob(mode);
        }

        internal static bool CanRunBackgroundAudit()
        {
            PungentTokenStorage.EnsureLoaded();
            if (PungentTokenStorage.Database == null)
                return false;

            PungentTokenScanSettings settings = PungentTokenStorage.Database.EnsureScanSettings();
            return settings.projectScanMode == PungentTokenProjectScanMode.StrictOptIn || settings.scanOnlyOptInMarkedFiles;
        }

        private static string LoadCoordinatorScanFilter()
        {
            return UtilityWindowPrefs.GetString(PrefScanFilter, "t:TextAsset");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Token Validator");
            PungentTokenStorage.EnsureLoaded();
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _selectedTokenKey = UtilityWindowPrefs.GetString(PrefSelected, string.Empty);
            _showArchived = UtilityWindowPrefs.GetBool(PrefShowArchived, false);
            _showDeprecated = UtilityWindowPrefs.GetBool(PrefShowDeprecated, true);
            _scanFilter = UtilityWindowPrefs.GetString(PrefScanFilter, "t:TextAsset");
            _text = UtilityWindowPrefs.GetString(PrefText, _text);
            _leftWidth = UtilityWindowPrefs.GetFloat(PrefLeftWidth, 270f);
            _rightWidth = UtilityWindowPrefs.GetFloat(PrefRightWidth, 360f);
            _scanIssuesExpanded = UtilityWindowPrefs.GetBool(PrefScanIssuesFoldout, false);
            _showScanErrors = UtilityWindowPrefs.GetBool(PrefShowScanErrors, true);
            _showScanWarnings = UtilityWindowPrefs.GetBool(PrefShowScanWarnings, true);
            _showScanInfo = UtilityWindowPrefs.GetBool(PrefShowScanInfo, true);
            _scanSettingsExpanded = UtilityWindowPrefs.GetBool(PrefScanSettingsFoldout, false);
            if (PungentScanCache.TryHydrateSession(_scanSession, out _resultSourceBanner) && _scanSession.Result != null)
                _status = _scanSession.Result.StatusMessage;
            if (string.IsNullOrWhiteSpace(_selectedTokenKey) && PungentTokenStorage.Database.tokens.Count > 0)
                _selectedTokenKey = PungentTokenStorage.Database.tokens[0].key;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RunQueuedProjectScan;
            SavePrefs();
        }

        private void OnGUI()
        {
            PungentTokenStorage.EnsureLoaded();
            UtilityWindowTheme.Header("Token Validator", "Define reusable brace tokens, link tokens to editor targets, and validate text, assets, notes, and token bindings.", _status);
            DrawToolbar();

            ClampPanelWidths();
            float centerWidth = GetCenterWidth();

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawTokenList(GUILayout.Width(_leftWidth), GUILayout.ExpandHeight(true));

                UtilityWindowTheme.HorizontalResizeHandle(
                    ref _leftWidth,
                    MinLeftWidth,
                    MaxLeftWidth(),
                    SavePrefs);

                DrawValidationWorkspace(
                    GUILayout.Width(centerWidth),
                    GUILayout.ExpandHeight(true));

                UtilityWindowTheme.HorizontalResizeHandle(
                    ref _rightWidth,
                    MinRightWidth,
                    MaxRightWidth(),
                    SavePrefs,
                    "Drag to resize token editor",
                    true);

                DrawTokenEditor(GUILayout.Width(_rightWidth), GUILayout.ExpandHeight(true));
            }
        }

        private void ClampPanelWidths()
        {
            float available = AvailablePanelWidth();

            float maxLeft = Mathf.Max(MinLeftWidth, available - MinCenterWidth - MinRightWidth - SplitterWidth * 2f);
            _leftWidth = Mathf.Clamp(_leftWidth, MinLeftWidth, maxLeft);

            float maxRight = Mathf.Max(MinRightWidth, available - _leftWidth - MinCenterWidth - SplitterWidth * 2f);
            _rightWidth = Mathf.Clamp(_rightWidth, MinRightWidth, maxRight);
        }

        private float AvailablePanelWidth()
        {
            return Mathf.Max(
                MinLeftWidth + MinCenterWidth + MinRightWidth + SplitterWidth * 2f,
                position.width - BodyPaddingReserve);
        }

        private float GetCenterWidth()
        {
            float width = AvailablePanelWidth() - _leftWidth - _rightWidth - SplitterWidth * 2f;
            return Mathf.Max(MinCenterWidth, width);
        }

        private float MaxLeftWidth()
        {
            return Mathf.Max(
                MinLeftWidth,
                AvailablePanelWidth() - _rightWidth - MinCenterWidth - SplitterWidth * 2f);
        }

        private float MaxRightWidth()
        {
            return Mathf.Max(
                MinRightWidth,
                AvailablePanelWidth() - _leftWidth - MinCenterWidth - SplitterWidth * 2f);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search", GUILayout.Width(48f));
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (UtilityWindowTheme.TintedButton("New Token", UtilityWindowTheme.Green, GUILayout.Width(88f)))
                        SelectToken(PungentTokenStorage.Database.AddToken("newToken"));
                    if (GUILayout.Button("Validate Text", GUILayout.Width(96f)))
                        ValidateText();
                    if (GUILayout.Button("Validate Selected", GUILayout.Width(118f)))
                        ValidateObjects(Selection.objects);
                    using (new EditorGUI.DisabledScope(_projectScanQueued || _projectScanRunning))
                    {
                        string scanLabel = _projectScanQueued || _projectScanRunning ? "Scanning..." : "Scan Project";
                        if (GUILayout.Button(scanLabel, GUILayout.Width(92f)))
                            QueueProjectScan();
                    }
                    if (GUILayout.Button("Validate Notes", GUILayout.Width(108f)))
                        ValidateNotes();
                    _showArchived = GUILayout.Toggle(_showArchived, "Archived", EditorStyles.toolbarButton, GUILayout.Width(78f));
                    _showDeprecated = GUILayout.Toggle(_showDeprecated, "Deprecated", EditorStyles.toolbarButton, GUILayout.Width(92f));
                    if (GUILayout.Button("Open Notes & Roadmap", GUILayout.Width(154f)))
                        PungentNotesRoadmapWindow.Open();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _quickFilter = (PungentTokenQuickFilter)EditorGUILayout.EnumPopup("Filter", _quickFilter, GUILayout.Width(220f));
                    EditorGUILayout.LabelField("Project Scan", GUILayout.Width(76f));
                    _scanFilter = EditorGUILayout.TextField(_scanFilter);
                    if (GUILayout.Button(_scanSettingsExpanded ? "Settings -" : "Settings +", EditorStyles.miniButton, GUILayout.Width(82f)))
                    {
                        _scanSettingsExpanded = !_scanSettingsExpanded;
                        SavePrefs();
                    }
                    GUILayout.FlexibleSpace();
                }

                if (_scanSettingsExpanded)
                    DrawScanSettingsPanel();
            }
        }

        private void DrawScanSettingsPanel()
        {
            PungentTokenScanSettings settings = ScanSettings;
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    settings.projectScanMode = (PungentTokenProjectScanMode)EditorGUILayout.EnumPopup("Scan Mode", settings.projectScanMode, GUILayout.Width(260f));
                    settings.reportLikelyUnknownsAsWarnings = GUILayout.Toggle(settings.reportLikelyUnknownsAsWarnings, "Likely as warnings", EditorStyles.toolbarButton, GUILayout.Width(132f));
                    settings.ignoreOptOutMarkedFiles = GUILayout.Toggle(settings.ignoreOptOutMarkedFiles, "Respect opt-out", EditorStyles.toolbarButton, GUILayout.Width(112f));
                    settings.scanOnlyOptInMarkedFiles = GUILayout.Toggle(settings.scanOnlyOptInMarkedFiles, "Opt-in only", EditorStyles.toolbarButton, GUILayout.Width(92f));
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    settings.showLikelyCandidates = GUILayout.Toggle(settings.showLikelyCandidates, "Show Likely", EditorStyles.toolbarButton, GUILayout.Width(92f));
                    settings.showPossibleCandidates = GUILayout.Toggle(settings.showPossibleCandidates, "Show Possible", EditorStyles.toolbarButton, GUILayout.Width(104f));
                    settings.showUnlikelyCandidates = GUILayout.Toggle(settings.showUnlikelyCandidates, "Show Unlikely", EditorStyles.toolbarButton, GUILayout.Width(108f));
                    settings.likelyThreshold = EditorGUILayout.IntSlider("Likely", settings.likelyThreshold, 1, 100, GUILayout.Width(190f));
                    settings.possibleThreshold = EditorGUILayout.IntSlider("Possible", settings.possibleThreshold, 1, settings.likelyThreshold, GUILayout.Width(205f));
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Include", GUILayout.Width(48f));
                    string include = EditorGUILayout.TextField(JoinHints(settings.includePathHints));
                    EditorGUILayout.LabelField("Exclude", GUILayout.Width(48f));
                    string exclude = EditorGUILayout.TextField(JoinHints(settings.excludePathHints));

                    if (GUI.changed)
                    {
                        settings.includePathHints = ParseHintList(include);
                        settings.excludePathHints = ParseHintList(exclude);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        "Ignored: " + settings.ignoredTokenKeys.Count + " key(s), " +
                        settings.ignoredSourcePaths.Count + " file(s), " +
                        settings.ignoredCandidateHashes.Count + " candidate(s)",
                        UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Clear Ignored Keys", EditorStyles.miniButton, GUILayout.Width(118f)))
                    {
                        settings.ignoredTokenKeys.Clear();
                        SaveScanSettings();
                        RefreshCandidateIgnoreState();
                    }

                    if (GUILayout.Button("Clear Ignored Files", EditorStyles.miniButton, GUILayout.Width(118f)))
                    {
                        settings.ignoredSourcePaths.Clear();
                        SaveScanSettings();
                        RefreshCandidateIgnoreState();
                    }

                    if (GUILayout.Button("Clear Ignored Items", EditorStyles.miniButton, GUILayout.Width(124f)))
                    {
                        settings.ignoredCandidateHashes.Clear();
                        SaveScanSettings();
                        RefreshCandidateIgnoreState();
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
                SaveScanSettings();
        }

        private void DrawTokenList(params GUILayoutOption[] options)
        {
            List<PungentTokenDefinition> tokens = PungentTokenFilters.Query(_search, _showArchived, _showDeprecated, _quickFilter);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), options))
            {
                UtilityWindowTheme.SectionTitle("Tokens", UtilityWindowTheme.Teal, tokens.Count + " shown");
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, false, true);
                if (tokens.Count == 0)
                    EditorGUILayout.HelpBox("No tokens match the current filter.", MessageType.Info);
                foreach (PungentTokenDefinition token in tokens)
                    DrawTokenRow(token);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTokenRow(PungentTokenDefinition token)
        {
            bool selected = string.Equals(PungentTokenParser.NormalizeKey(token.key), PungentTokenParser.NormalizeKey(_selectedTokenKey), StringComparison.OrdinalIgnoreCase);
            Color tint = token.deprecated ? UtilityWindowTheme.Amber : token.archived ? UtilityWindowTheme.Neutral : selected ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Blue;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.20f : 0.10f, selected ? 0.10f : 0.04f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string tokenLabel = "{" + token.key + "}";
                    if (GUILayout.Button(new GUIContent(tokenLabel, tokenLabel), UtilityWindowTheme.CardLabelStyle, GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true)))
                        SelectToken(token);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(PungentTokenStorage.Database.GetBindingsForToken(token.key).Count + " links", UtilityWindowTheme.Teal, 68f);
                }
                EditorGUILayout.LabelField((string.IsNullOrWhiteSpace(token.category) ? "General" : token.category) + (token.deprecated ? " | Deprecated" : string.Empty), UtilityWindowTheme.PathLabelStyle);
            }
        }

        private void DrawValidationWorkspace(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, margin: 2), options))
            {
                UtilityWindowTheme.SectionTitle("Validate & Preview", UtilityWindowTheme.Neutral);

                _centerScroll = EditorGUILayout.BeginScrollView(
                    _centerScroll,
                    false,
                    true,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Load Selected TextAsset", GUILayout.Width(150f)))
                        LoadSelectedText();

                    if (GUILayout.Button("Validate Text", GUILayout.Width(96f)))
                        ValidateText();

                    if (GUILayout.Button("Copy Preview", GUILayout.Width(96f)))
                        EditorGUIUtility.systemCopyBuffer = PungentTokenParser.ResolvePreview(_text);

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.12f, 0.05f, 6, 2)))
                {
                    UtilityWindowTheme.SectionTitle("Input Text", UtilityWindowTheme.Blue);
                    EditorGUI.BeginChangeCheck();
                    _text = EditorGUILayout.TextArea(
                        _text,
                        GUILayout.MinHeight(110f),
                        GUILayout.ExpandWidth(true));

                    if (EditorGUI.EndChangeCheck())
                        _inlineValidationText = null;
                }

                DrawInlineResults(_text);
                DrawPreviewPanel();

                EditorGUILayout.Space(6f);

                PungentScanGUI.DrawResultHeader(_scanSession.Result, _resultSourceBanner);
                DrawCandidateScanSummary();
                DrawScanIssuesPanel();
                DrawUnknownCandidatesPanel();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPreviewPanel()
        {
            string preview = PungentTokenParser.ResolvePreview(_text);

            using (new EditorGUILayout.VerticalScope(
                UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.16f, 0.08f, 8, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Preview Output", UtilityWindowTheme.Purple);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(52f)))
                        EditorGUIUtility.systemCopyBuffer = preview;
                }

                EditorGUILayout.SelectableLabel(
                    preview,
                    WrappedPreviewStyle(),
                    GUILayout.MinHeight(58f),
                    GUILayout.ExpandWidth(true));
            }
        }

        private static GUIStyle WrappedPreviewStyle()
        {
            GUIStyle style = new GUIStyle(UtilityWindowTheme.BodyStyle)
            {
                wordWrap = true,
                clipping = TextClipping.Overflow
            };
            return style;
        }

        private void DrawInlineResults(string text)
        {
            if (!string.Equals(_inlineValidationText, text, StringComparison.Ordinal))
            {
                _inlineUsages.Clear();
                _inlineUsages.AddRange(PungentTokenValidatorService.ValidateText(text, "Pasted Text", null, null, null, false));
                _inlineValidationText = text;
            }

            bool hasProblems = _inlineUsages.Any(u => u.status != PungentTokenUsageStatus.Known);

            using (new EditorGUILayout.VerticalScope(
                UtilityWindowTheme.PanelStyle(
                    hasProblems ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green,
                    0.12f,
                    0.05f,
                    6,
                    2)))
            {
                UtilityWindowTheme.SectionTitle(
                    "Detected Tokens",
                    hasProblems ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green,
                    _inlineUsages.Sum(u => u.occurrenceCount) + " found");

                if (_inlineUsages.Count == 0)
                {
                    EditorGUILayout.HelpBox("No brace tokens found. Use {tokenName} syntax to reference token values.", MessageType.Info);
                    return;
                }

                foreach (PungentTokenUsage usage in _inlineUsages.Take(MaxInlineUsageRows))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill(
                            usage.status.ToString(),
                            usage.status == PungentTokenUsageStatus.Known ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber,
                            104f);

                        EditorGUILayout.LabelField(
                            new GUIContent("{" + usage.tokenKey + "} x" + usage.occurrenceCount, usage.sourceLabel),
                            UtilityWindowTheme.PathLabelStyle,
                            GUILayout.ExpandWidth(true));
                    }
                }

                if (_inlineUsages.Count > MaxInlineUsageRows)
                    EditorGUILayout.LabelField("+" + (_inlineUsages.Count - MaxInlineUsageRows) + " more token rows.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawCandidateScanSummary()
        {
            if (_scanSession.Result == null || string.IsNullOrWhiteSpace(_candidateSummary))
                return;

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.10f, 0.04f, 5, 2)))
            {
                UtilityWindowTheme.CountPill("Mode: " + ObjectNames.NicifyVariableName(ScanSettings.projectScanMode.ToString()), UtilityWindowTheme.Teal, 170f);
                UtilityWindowTheme.CountPill("Likely: " + _lastLikelyCandidateCount, UtilityWindowTheme.Amber, 82f);
                UtilityWindowTheme.CountPill("Possible: " + _lastPossibleCandidateCount, UtilityWindowTheme.Blue, 92f);
                UtilityWindowTheme.CountPill("Unlikely: " + _lastUnlikelyCandidateCount, UtilityWindowTheme.Neutral, 92f);
                UtilityWindowTheme.CountPill("Ignored: " + _lastIgnoredCandidateCount, UtilityWindowTheme.Neutral, 86f);
                GUILayout.Label(_candidateSummary, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawUnknownCandidatesPanel()
        {
            if (_scanSession.Result == null && _lastCandidates.Count == 0)
                return;

            PungentTokenScanSettings settings = ScanSettings;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.10f, 0.04f, 6, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle(
                        "Unknown Candidates",
                        UtilityWindowTheme.Teal,
                        "Likely " + _lastLikelyCandidateCount + " | Possible " + _lastPossibleCandidateCount + " | Unlikely " + _lastUnlikelyCandidateCount);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawCandidateVisibilityChip("Show Likely", _lastLikelyCandidateCount, UtilityWindowTheme.Amber, ref settings.showLikelyCandidates);
                    DrawCandidateVisibilityChip("Show Possible", _lastPossibleCandidateCount, UtilityWindowTheme.Blue, ref settings.showPossibleCandidates);
                    DrawCandidateVisibilityChip("Show Unlikely", _lastUnlikelyCandidateCount, UtilityWindowTheme.Neutral, ref settings.showUnlikelyCandidates);
                    GUILayout.FlexibleSpace();
                    if (_lastHiddenCandidateRows > 0)
                        EditorGUILayout.LabelField(_lastHiddenCandidateRows + " candidate(s) hidden by row cap.", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(190f));
                }

                List<PungentTokenCandidate> visible = _lastCandidates
                    .Where(c => c != null && !c.ignored && ShouldShowCandidate(c, settings))
                    .Take(MaxDisplayedCandidates)
                    .ToList();

                if (visible.Count == 0)
                {
                    string empty = _lastCandidates.Count == 0
                        ? "No unknown token candidates to review."
                        : "No unknown token candidates match the active visibility settings.";
                    EditorGUILayout.HelpBox(empty, MessageType.Info);
                    return;
                }

                DrawCandidateGroup("Likely", PungentTokenIntentLevel.Likely, visible);
                DrawCandidateGroup("Possible", PungentTokenIntentLevel.Possible, visible);
                DrawCandidateGroup("Unlikely", PungentTokenIntentLevel.Unlikely, visible);

                int visibleTotal = _lastCandidates.Count(c => c != null && !c.ignored && ShouldShowCandidate(c, settings));
                if (visibleTotal > MaxDisplayedCandidates)
                    EditorGUILayout.LabelField("+" + (visibleTotal - MaxDisplayedCandidates) + " more candidate row(s) omitted from display.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawCandidateVisibilityChip(string label, int count, Color tint, ref bool enabled)
        {
            Color previous = GUI.color;
            GUI.color = enabled ? previous : new Color(previous.r, previous.g, previous.b, 0.55f);
            if (GUILayout.Button(label + ": " + count, EditorStyles.miniButton, GUILayout.Width(112f)))
            {
                enabled = !enabled;
                SaveScanSettings();
                Repaint();
            }
            GUI.color = previous;
        }

        private void DrawCandidateGroup(string label, PungentTokenIntentLevel level, List<PungentTokenCandidate> visible)
        {
            List<PungentTokenCandidate> group = visible.Where(c => c.intentLevel == level).ToList();
            if (group.Count == 0)
                return;

            UtilityWindowTheme.SectionTitle(label + " Candidates", CandidateTint(level), group.Count + " shown");
            foreach (PungentTokenCandidate candidate in group)
                DrawCandidateRow(candidate);
        }

        private void DrawCandidateRow(PungentTokenCandidate candidate)
        {
            if (candidate == null)
                return;

            Color tint = CandidateTint(candidate.intentLevel);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 4, 1), GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(candidate.intentLevel.ToString(), tint, 82f);
                    UtilityWindowTheme.CountPill(candidate.confidence + "%", UtilityWindowTheme.Neutral, 52f);

                    string tokenLabel = "{" + candidate.tokenKey + "} x" + Mathf.Max(1, candidate.occurrenceCount);
                    EditorGUILayout.LabelField(new GUIContent(tokenLabel, tokenLabel), UtilityWindowTheme.CardLabelStyle, GUILayout.MaxWidth(180f));

                    EditorGUILayout.LabelField(new GUIContent(candidate.sourceLabel, candidate.sourcePath), UtilityWindowTheme.PathLabelStyle, GUILayout.ExpandWidth(true));
                }

                if (!string.IsNullOrWhiteSpace(candidate.reason))
                    EditorGUILayout.LabelField(candidate.reason, UtilityWindowTheme.MutedMiniLabelStyle);

                if (!string.IsNullOrWhiteSpace(candidate.sourcePath))
                    EditorGUILayout.LabelField(new GUIContent(candidate.sourcePath, candidate.sourcePath), UtilityWindowTheme.PathLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Token", EditorStyles.miniButton, GUILayout.Width(88f)))
                        CreateTokenFromCandidate(candidate);
                    if (GUILayout.Button("Ignore One", EditorStyles.miniButton, GUILayout.Width(78f)))
                        IgnoreCandidateHash(candidate);
                    if (GUILayout.Button("Ignore Key", EditorStyles.miniButton, GUILayout.Width(78f)))
                        IgnoreCandidateKey(candidate);
                    if (GUILayout.Button("Ignore File", EditorStyles.miniButton, GUILayout.Width(78f)))
                        IgnoreCandidateFile(candidate);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy Path", EditorStyles.miniButton, GUILayout.Width(72f)))
                        EditorGUIUtility.systemCopyBuffer = candidate.sourcePath ?? string.Empty;
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(48f)))
                        PingCandidateSource(candidate);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private bool ShouldShowCandidate(PungentTokenCandidate candidate, PungentTokenScanSettings settings)
        {
            if (candidate == null || candidate.intentLevel == PungentTokenIntentLevel.NotToken)
                return false;

            switch (candidate.intentLevel)
            {
                case PungentTokenIntentLevel.Likely:
                case PungentTokenIntentLevel.Confirmed:
                    return settings.showLikelyCandidates;
                case PungentTokenIntentLevel.Possible:
                    return settings.showPossibleCandidates;
                case PungentTokenIntentLevel.Unlikely:
                    return settings.showUnlikelyCandidates;
                default:
                    return false;
            }
        }

        private Color CandidateTint(PungentTokenIntentLevel level)
        {
            switch (level)
            {
                case PungentTokenIntentLevel.Likely:
                case PungentTokenIntentLevel.Confirmed:
                    return UtilityWindowTheme.Amber;
                case PungentTokenIntentLevel.Possible:
                    return UtilityWindowTheme.Blue;
                case PungentTokenIntentLevel.Unlikely:
                    return UtilityWindowTheme.Neutral;
                default:
                    return UtilityWindowTheme.Teal;
            }
        }

        private void DrawScanIssuesPanel()
        {
            PungentScanResult result = _scanSession.Result;
            if (result == null || result.Issues == null)
                return;

            IReadOnlyList<PungentScanIssue> allIssues = result.Issues;
            int errorCount = allIssues.Count(i => i != null && i.Severity == PungentScanSeverity.Error);
            int warningCount = allIssues.Count(i => i != null && i.Severity == PungentScanSeverity.Warning);
            int infoCount = allIssues.Count(i => i != null && i.Severity == PungentScanSeverity.Info);

            bool hasAnyIssues = allIssues.Count > 0;

            using (new EditorGUILayout.VerticalScope(
                UtilityWindowTheme.PanelStyle(hasAnyIssues ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 0.10f, 0.04f, 6, 2),
                GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle(
                        "Scan Issues",
                        hasAnyIssues ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green,
                        allIssues.Count + " total");

                    GUILayout.FlexibleSpace();

                    DrawScanIssueFilterChip("Errors", errorCount, UtilityWindowTheme.Red, ref _showScanErrors);
                    DrawScanIssueFilterChip("Warnings", warningCount, UtilityWindowTheme.Amber, ref _showScanWarnings);
                    DrawScanIssueFilterChip("Info", infoCount, UtilityWindowTheme.Blue, ref _showScanInfo);
                }

                if (!hasAnyIssues)
                {
                    EditorGUILayout.HelpBox("No scan issues to display.", MessageType.Info);
                    return;
                }

                List<PungentScanIssue> visibleIssues = allIssues
                    .Where(ShouldShowScanIssue)
                    .ToList();

                if (visibleIssues.Count == 0)
                {
                    EditorGUILayout.HelpBox("All scan issues are hidden by the active severity filters.", MessageType.Info);
                    return;
                }

                EditorGUILayout.Space(3f);

                // No nested fixed-height scroll view here.
                // The centre panel already scrolls, so rows should naturally stretch downward.
                foreach (PungentScanIssue issue in visibleIssues)
                    DrawTokenScanIssueRow(issue);

                if (visibleIssues.Count < allIssues.Count)
                {
                    EditorGUILayout.LabelField(
                        (allIssues.Count - visibleIssues.Count) + " issue(s) hidden by filters.",
                        UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private bool ShouldShowScanIssue(PungentScanIssue issue)
        {
            if (issue == null)
                return false;

            switch (issue.Severity)
            {
                case PungentScanSeverity.Error:
                    return _showScanErrors;

                case PungentScanSeverity.Warning:
                    return _showScanWarnings;

                case PungentScanSeverity.Info:
                    return _showScanInfo;

                default:
                    return true;
            }
        }

        private void DrawScanIssueFilterChip(
            string label,
            int count,
            Color tint,
            ref bool enabled)
        {
            Color previous = GUI.color;
            GUI.color = enabled ? previous : new Color(previous.r, previous.g, previous.b, 0.55f);

            string text = label + ": " + count;
            if (GUILayout.Button(text, EditorStyles.miniButton, GUILayout.Width(92f)))
            {
                enabled = !enabled;
                SavePrefs();
                Repaint();
            }

            GUI.color = previous;
        }

        private void DrawTokenScanIssueRow(PungentScanIssue issue)
        {
            if (issue == null)
                return;

            Color tint = SeverityTint(issue.Severity);

            using (new EditorGUILayout.VerticalScope(
                UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 4, 1),
                GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(issue.Severity.ToString(), tint, 92f);

                    EditorGUILayout.LabelField(
                        string.IsNullOrWhiteSpace(issue.Title) ? "Token scan issue" : issue.Title,
                        UtilityWindowTheme.CardLabelStyle,
                        GUILayout.ExpandWidth(true));
                }

                if (!string.IsNullOrWhiteSpace(issue.Message))
                {
                    EditorGUILayout.LabelField(
                        issue.Message,
                        UtilityWindowTheme.PathLabelStyle,
                        GUILayout.ExpandWidth(true));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!string.IsNullOrWhiteSpace(issue.Code))
                        UtilityWindowTheme.CountPill(issue.Code, UtilityWindowTheme.Neutral, Mathf.Clamp(64f + issue.Code.Length * 6f, 92f, 180f));

                    if (!string.IsNullOrWhiteSpace(issue.Path))
                    {
                        EditorGUILayout.LabelField(
                            issue.Path,
                            UtilityWindowTheme.MutedMiniLabelStyle,
                            GUILayout.ExpandWidth(true));

                        if (GUILayout.Button("Copy Path", EditorStyles.miniButton, GUILayout.Width(72f)))
                            EditorGUIUtility.systemCopyBuffer = issue.Path;
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private Color SeverityTint(PungentScanSeverity severity)
        {
            switch (severity)
            {
                case PungentScanSeverity.Error:
                    return UtilityWindowTheme.Red;

                case PungentScanSeverity.Warning:
                    return UtilityWindowTheme.Amber;

                case PungentScanSeverity.Info:
                    return UtilityWindowTheme.Blue;

                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private void DrawTokenEditor(params GUILayoutOption[] options)
        {
            PungentTokenDefinition token = SelectedToken;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple), options))
            {
                UtilityWindowTheme.SectionTitle("Token Editor", UtilityWindowTheme.Purple, token != null ? "{" + token.key + "}" : "No token");
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, false, true);
                if (token == null)
                {
                    EditorGUILayout.HelpBox("Select or create a token.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                EditorGUI.BeginChangeCheck();
                string nextKey = EditorGUILayout.TextField("Key", token.key);
                if (!string.Equals(nextKey, token.key, StringComparison.Ordinal))
                    TryRenameToken(token, nextKey);
                token.displayName = EditorGUILayout.TextField("Display Name", token.displayName);
                token.category = EditorGUILayout.TextField("Category", token.category);
                token.description = EditorGUILayout.TextField("Description", token.description);
                token.previewValue = EditorGUILayout.TextField("Preview Value", token.previewValue);
                token.tags = PungentTokenGUI.ParseTags(PungentTokenGUI.DrawTagsField(token.tags));
                token.examples = PungentTokenGUI.ParseExamples(PungentTokenGUI.DrawExamplesField(token.examples));
                token.required = EditorGUILayout.Toggle("Required", token.required);
                token.deprecated = EditorGUILayout.Toggle("Deprecated", token.deprecated);
                token.replacementKey = DrawTokenKeyPopup("Replacement", token.replacementKey, true);
                token.archived = EditorGUILayout.Toggle("Archived", token.archived);
                token.developerOnly = EditorGUILayout.Toggle("Developer Only", token.developerOnly);
                token.documentationNoteId = EditorGUILayout.TextField("Documentation Note", token.documentationNoteId);

                if (EditorGUI.EndChangeCheck())
                {
                    token.key = PungentTokenParser.NormalizeKey(token.key);
                    token.updatedUtc = DateTime.UtcNow.ToString("o");
                    PungentTokenStorage.Save();
                    SavePrefs();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy Token", GUILayout.Width(88f)))
                        EditorGUIUtility.systemCopyBuffer = "{" + token.key + "}";
                    if (GUILayout.Button("Duplicate", GUILayout.Width(78f)))
                        SelectToken(PungentTokenStorage.Database.DuplicateToken(token));
                    if (GUILayout.Button(token.archived ? "Unarchive" : "Archive", GUILayout.Width(78f)))
                        PungentTokenStorage.Database.ArchiveToken(token, !token.archived);
                }

                DrawBindings(token);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBindings(PungentTokenDefinition token)
        {
            List<PungentTokenBinding> bindings = PungentTokenStorage.Database.GetBindingsForToken(token.key);
            UtilityWindowTheme.SectionTitle("Bindings", UtilityWindowTheme.Teal, bindings.Count + " links");
            foreach (PungentTokenBinding binding in bindings)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 4, 1)))
                {
                    EditorGUILayout.LabelField(new GUIContent(binding.label, binding.label), UtilityWindowTheme.CardLabelStyle);
                    string bindingPath = binding.targetType + " " + binding.propertyPath;
                    EditorGUILayout.LabelField(new GUIContent(bindingPath, bindingPath), UtilityWindowTheme.PathLabelStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (!string.IsNullOrWhiteSpace(binding.noteId) && GUILayout.Button("Open Note", EditorStyles.miniButton))
                            PungentNotesRoadmapWindow.OpenAndSelect(binding.noteId);
                        if (GUILayout.Button("Remove", EditorStyles.miniButton))
                        {
                            PungentTokenStorage.Database.RemoveBinding(binding);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }

        private void ValidateText()
        {
            PungentScanResult scan = _scanSession.Begin(PungentScanScope.Manual, "Pasted Text");
            ResetLastUsageResults();
            ResetCandidateResults();
            List<PungentTokenUsage> usages = PungentTokenValidatorService.ValidateText(_text, "Pasted Text");
            RecordLastUsages(usages);
            PungentTokenValidatorService.AddUsagesToScan(scan, usages);
            PungentTokenValidatorService.ValidateDefinitions(scan);
            PungentTokenValidatorService.ValidateBindings(scan);
            _lastUsageSummary = BuildLastUsageSummary(usages.Sum(u => Mathf.Max(1, u.occurrenceCount)));
            _scanSession.Complete(1, usages.Count, 0, 0, "Validated pasted text.");
            AutoOpenScanIssuesForProblems(scan);
            _status = "Validated pasted text.";
        }

        private void LoadSelectedText()
        {
            TextAsset asset = Selection.activeObject as TextAsset;
            if (asset == null)
            {
                _status = "Select a TextAsset first.";
                return;
            }
            _text = asset.text;
            _inlineValidationText = null;
            _status = "Loaded " + asset.name + ".";
        }

        private void ValidateObjects(Object[] objects)
        {
            PungentScanResult scan = _scanSession.Begin(PungentScanScope.Selection, "Selected TextAssets");
            ResetLastUsageResults();
            ResetCandidateResults();
            int scanned = 0;
            int totalTokenUsages = 0;
            foreach (Object obj in objects ?? new Object[0])
            {
                TextAsset text = obj as TextAsset;
                if (text == null)
                    continue;
                scanned++;
                string path = AssetDatabase.GetAssetPath(text);
                string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                List<PungentTokenUsage> usages = PungentTokenValidatorService.ValidateText(text.text, path, path, null, guid);
                totalTokenUsages += usages.Sum(u => Mathf.Max(1, u.occurrenceCount));
                RecordLastUsages(usages);
                PungentTokenValidatorService.AddUsagesToScan(scan, usages, text);
            }
            _lastUsageSummary = BuildLastUsageSummary(totalTokenUsages);
            _scanSession.Complete(scanned, totalTokenUsages, 0, 0, "Validated " + scanned + " selected TextAsset(s).");
            AutoOpenScanIssuesForProblems(scan);
            _status = "Validated selection.";
        }

        private void QueueProjectScan()
        {
            if (_projectScanQueued || _projectScanRunning)
                return;

            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);

            _projectScanQueued = true;
            _status = "Project token scan queued.";

            EditorApplication.delayCall -= RunQueuedProjectScan;
            EditorApplication.delayCall += RunQueuedProjectScan;

            Repaint();
        }

        private void RunQueuedProjectScan()
        {
            EditorApplication.delayCall -= RunQueuedProjectScan;

            if (this == null)
                return;

            _projectScanQueued = false;
            _projectScanRunning = true;
            _status = "Scanning project TextAssets...";
            Repaint();

            try
            {
                ScanProjectNow();
            }
            finally
            {
                _projectScanRunning = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void ScanProjectNow()
        {
            PungentScanResult scan = _scanSession.Begin(PungentScanScope.ProjectAssets, _scanFilter);
            ResetLastUsageResults();
            ResetCandidateResults();
            PungentTokenScanSettings settings = ScanSettings;

            int totalTokenUsages = 0;

            try
            {
                string[] guids = GetProjectScanGuids(settings);

                int limit = Mathf.Min(guids.Length, 500);
                int scanned = 0;
                bool cancelled = false;

                for (int i = 0; i < limit; i++)
                {
                    float progress = limit <= 0 ? 1f : (float)i / limit;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Token Validator",
                            "Scanning TextAssets " + (i + 1) + " / " + limit,
                            progress))
                    {
                        cancelled = true;
                        scan.AddIssue(
                            PungentScanSeverity.Info,
                            "Project scan cancelled",
                            "Token project scan was cancelled by the user.",
                            null,
                            null,
                            "TOKEN_SCAN_CANCELLED");
                        break;
                    }

                    string guid = guids[i];
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset == null)
                        continue;

                    scanned++;
                    PungentTokenIntentFileContext intentContext = PungentTokenIntentClassifier.CreateContext(asset.text, path);

                    List<PungentTokenUsage> usages = PungentTokenValidatorService.ValidateText(
                        asset.text,
                        path,
                        path,
                        null,
                        guid,
                        false,
                        false);

                    totalTokenUsages += usages.Sum(u => Mathf.Max(1, u.occurrenceCount));
                    RecordLastUsages(usages.Where(u => u.status != PungentTokenUsageStatus.Unknown));

                    List<PungentTokenCandidate> candidates = ClassifyProjectUnknowns(usages, intentContext, settings);

                    PungentTokenValidatorService.AddProjectUsagesToScan(
                        scan,
                        usages,
                        candidates,
                        settings,
                        asset,
                        maxIssues: 200);
                }

                PungentTokenValidatorService.ValidateDefinitions(scan);
                PungentTokenValidatorService.ValidateBindings(scan);

                int skipped = Mathf.Max(0, guids.Length - scanned);
                if (guids.Length > 500)
                {
                    scan.AddIssue(
                        PungentScanSeverity.Info,
                        "Scan capped",
                        "Only the first 500 matching TextAssets were validated.",
                        null,
                        null,
                        "TOKEN_SCAN_CAPPED");
                }

                _lastUsageSummary = BuildLastUsageSummary(totalTokenUsages);
                _candidateSummary = BuildCandidateSummary();

                string summary = cancelled
                    ? "Project token scan cancelled after " + scanned + " asset(s)."
                    : "Scanned " + scanned + " TextAsset(s).";

                _scanSession.Complete(scanned, totalTokenUsages, skipped, 0, summary);
                AutoOpenScanIssuesForProblems(scan);
                _status = summary;
            }
            catch (Exception exception)
            {
                _status = "Project scan failed: " + exception.Message;
                _scanSession.Fail(exception, _status);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ValidateNotes()
        {
            PungentScanResult scan = _scanSession.Begin(PungentScanScope.Custom, "Notes & Roadmap");
            ResetLastUsageResults();
            ResetCandidateResults();
            List<PungentTokenUsage> usages = PungentTokenValidatorService.ValidateNotes(scan);
            RecordLastUsages(usages);
            int totalTokenUsages = usages.Sum(u => Mathf.Max(1, u.occurrenceCount));
            _lastUsageSummary = BuildLastUsageSummary(totalTokenUsages);
            _scanSession.Complete(PungentNoteStorage.Database.notes.Count, totalTokenUsages, 0, 0, "Validated note bodies and linked token keys.");
            AutoOpenScanIssuesForProblems(scan);
            _status = "Validated notes.";
        }

        private string[] GetProjectScanGuids(PungentTokenScanSettings settings)
        {
            if (settings != null && settings.projectScanMode == PungentTokenProjectScanMode.SelectionOnly)
            {
                return (Selection.objects ?? new Object[0])
                    .OfType<TextAsset>()
                    .Select(AssetDatabase.GetAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(AssetDatabase.AssetPathToGUID)
                    .Where(guid => !string.IsNullOrWhiteSpace(guid))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            string filter = string.IsNullOrWhiteSpace(_scanFilter) ? "t:TextAsset" : _scanFilter;
            return AssetDatabase.FindAssets(filter);
        }

        private List<PungentTokenCandidate> ClassifyProjectUnknowns(
            IEnumerable<PungentTokenUsage> usages,
            PungentTokenIntentFileContext context,
            PungentTokenScanSettings settings)
        {
            List<PungentTokenCandidate> candidates = new List<PungentTokenCandidate>();
            if (usages == null)
                return candidates;

            foreach (PungentTokenUsage usage in usages)
            {
                if (usage == null || usage.status != PungentTokenUsageStatus.Unknown)
                    continue;

                PungentTokenCandidate candidate = PungentTokenIntentClassifier.Classify(usage, context, settings);
                bool ignoredByMarker = context != null && context.hasPungentOptOut && settings.ignoreOptOutMarkedFiles;
                if (ignoredByMarker)
                {
                    candidate.ignored = true;
                    candidate.ignoredByFile = true;
                }
                bool ignored = ignoredByMarker || PungentTokenIntentClassifier.IsIgnored(candidate, settings);
                RecordCandidate(candidate, ignored);

                if (!ignored && candidate.intentLevel != PungentTokenIntentLevel.NotToken)
                    candidates.Add(candidate);
            }

            return candidates;
        }

        private void ResetLastUsageResults()
        {
            _lastUsages.Clear();
            _lastUsageSummary = string.Empty;
            _lastUsageRowTotal = 0;
            _lastKnownUsageRows = 0;
            _lastProblemUsageRows = 0;
            _lastHiddenKnownRows = 0;
            _lastHiddenProblemRows = 0;
        }

        private void ResetCandidateResults()
        {
            _lastCandidates.Clear();
            _candidateSummary = string.Empty;
            _lastLikelyCandidateCount = 0;
            _lastPossibleCandidateCount = 0;
            _lastUnlikelyCandidateCount = 0;
            _lastNotTokenCandidateCount = 0;
            _lastIgnoredCandidateCount = 0;
            _lastHiddenCandidateRows = 0;
        }

        private void RecordCandidate(PungentTokenCandidate candidate, bool ignored)
        {
            if (candidate == null)
                return;

            if (ignored)
            {
                _lastIgnoredCandidateCount++;
                return;
            }

            switch (candidate.intentLevel)
            {
                case PungentTokenIntentLevel.Likely:
                case PungentTokenIntentLevel.Confirmed:
                    _lastLikelyCandidateCount++;
                    break;
                case PungentTokenIntentLevel.Possible:
                    _lastPossibleCandidateCount++;
                    break;
                case PungentTokenIntentLevel.Unlikely:
                    _lastUnlikelyCandidateCount++;
                    break;
                default:
                    _lastNotTokenCandidateCount++;
                    return;
            }

            if (_lastCandidates.Count < MaxStoredCandidates)
                _lastCandidates.Add(candidate);
            else
                _lastHiddenCandidateRows++;
        }

        private string BuildCandidateSummary()
        {
            int hiddenByVisibility = ScanSettings.showUnlikelyCandidates ? 0 : _lastUnlikelyCandidateCount;
            return
                "Unknown candidates: " +
                _lastLikelyCandidateCount + " likely, " +
                _lastPossibleCandidateCount + " possible, " +
                _lastUnlikelyCandidateCount + " unlikely, " +
                hiddenByVisibility + " hidden unlikely, " +
                _lastIgnoredCandidateCount + " ignored.";
        }

        private void RecordLastUsages(IEnumerable<PungentTokenUsage> usages)
        {
            if (usages == null)
                return;

            foreach (PungentTokenUsage usage in usages)
                RecordLastUsage(usage);
        }

        private void RecordLastUsage(PungentTokenUsage usage)
        {
            if (usage == null)
                return;

            _lastUsageRowTotal++;

            bool known = usage.status == PungentTokenUsageStatus.Known;
            if (known)
                _lastKnownUsageRows++;
            else
                _lastProblemUsageRows++;

            if (!known && CountStoredProblemUsages() >= MaxStoredProblemUsages)
            {
                _lastHiddenProblemRows++;
                return;
            }

            if (_lastUsages.Count < MaxStoredUsageRows)
            {
                _lastUsages.Add(usage);
                return;
            }

            if (!known && RemoveFirstStoredKnownUsage())
            {
                _lastUsages.Add(usage);
                _lastHiddenKnownRows++;
                return;
            }

            if (known)
                _lastHiddenKnownRows++;
            else
                _lastHiddenProblemRows++;
        }

        private int CountStoredProblemUsages()
        {
            return _lastUsages.Count(u => u != null && u.status != PungentTokenUsageStatus.Known);
        }

        private bool RemoveFirstStoredKnownUsage()
        {
            int index = _lastUsages.FindIndex(u => u != null && u.status == PungentTokenUsageStatus.Known);
            if (index < 0)
                return false;

            _lastUsages.RemoveAt(index);
            return true;
        }

        private string BuildLastUsageSummary(int totalTokenUsages)
        {
            string summary =
                totalTokenUsages + " token use(s), " +
                _lastUsageRowTotal + " result row(s), " +
                _lastProblemUsageRows + " warning/error row(s), " +
                _lastKnownUsageRows + " known row(s)";

            int hidden = _lastHiddenKnownRows + _lastHiddenProblemRows;
            if (hidden > 0)
                summary += ", " + hidden + " hidden by row cap";

            return summary;
        }

        private void AutoOpenScanIssuesForProblems(PungentScanResult scan)
        {
            if (scan == null)
                return;

            if (scan.Summary.WarningCount <= 0 && scan.Summary.ErrorCount <= 0)
                return;

            _scanIssuesExpanded = true;
            SavePrefs();
        }

        private void TryRenameToken(PungentTokenDefinition token, string nextKey)
        {
            string clean = PungentTokenParser.NormalizeKey(nextKey);
            if (string.Equals(clean, token.key, StringComparison.OrdinalIgnoreCase))
                return;
            if (!PungentTokenParser.IsValidKey(clean) || PungentTokenStorage.Database.FindToken(clean) != null)
                return;
            bool used = PungentTokenStorage.Database.GetBindingsForToken(token.key).Count > 0 || PungentNoteStorage.Database.notes.Any(n => n != null && ((n.linkedTokenKeys != null && n.linkedTokenKeys.Contains(token.key)) || (n.body ?? string.Empty).Contains("{" + token.key + "}")));
            if (used && !EditorUtility.DisplayDialog("Rename Token", "This token is used in bindings or notes. Update bindings to the new key? Note bodies will not be rewritten.", "Rename", "Cancel"))
                return;
            string old = token.key;
            token.key = clean;
            if (used)
            {
                foreach (PungentTokenBinding binding in PungentTokenStorage.Database.bindings.Where(b => b != null && string.Equals(b.tokenKey, old, StringComparison.OrdinalIgnoreCase)))
                    binding.tokenKey = clean;
            }
            _selectedTokenKey = clean;
        }

        private string DrawTokenKeyPopup(string label, string current, bool includeNone)
        {
            List<string> keys = PungentTokenStorage.Database.tokens.Where(t => t != null && !t.archived).Select(t => t.key).OrderBy(k => k).ToList();
            if (includeNone)
                keys.Insert(0, string.Empty);
            int index = Mathf.Max(0, keys.FindIndex(k => string.Equals(k, current, StringComparison.OrdinalIgnoreCase)));
            int next = EditorGUILayout.Popup(label, index, keys.Select(k => string.IsNullOrEmpty(k) ? "None" : "{" + k + "}").ToArray());
            return next >= 0 && next < keys.Count ? keys[next] : string.Empty;
        }

        private PungentTokenDefinition SelectedToken => PungentTokenStorage.Database.FindToken(_selectedTokenKey);

        private PungentTokenScanSettings ScanSettings
        {
            get { return PungentTokenStorage.Database.EnsureScanSettings(); }
        }

        private void SelectToken(PungentTokenDefinition token)
        {
            _selectedTokenKey = token != null ? token.key : string.Empty;
            SavePrefs();
        }

        private void CreateTokenFromCandidate(PungentTokenCandidate candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.tokenKey))
                return;

            PungentTokenDefinition existing = PungentTokenStorage.Database.FindToken(candidate.tokenKey);
            PungentTokenDefinition token = existing ?? PungentTokenStorage.Database.AddToken(candidate.tokenKey);
            token.category = string.IsNullOrWhiteSpace(token.category) || token.category == "General" ? "Candidate" : token.category;
            token.source = "candidate";
            token.updatedUtc = DateTime.UtcNow.ToString("o");
            PungentTokenStorage.Save();
            SelectToken(token);
            RemoveCandidatesForKey(candidate.tokenKey);
            _status = "Created token {" + token.key + "} from candidate.";
        }

        private void IgnoreCandidateKey(PungentTokenCandidate candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.tokenKey))
                return;

            PungentTokenScanSettings settings = ScanSettings;
            AddUnique(settings.ignoredTokenKeys, PungentTokenParser.NormalizeKey(candidate.tokenKey));
            SaveScanSettings();
            RefreshCandidateIgnoreState();
            _status = "Ignored candidate key {" + candidate.tokenKey + "}.";
        }

        private void IgnoreCandidateHash(PungentTokenCandidate candidate)
        {
            if (candidate == null)
                return;

            PungentTokenScanSettings settings = ScanSettings;
            AddUnique(settings.ignoredCandidateHashes, candidate.Hash);
            SaveScanSettings();
            RefreshCandidateIgnoreState();
            _status = "Ignored candidate {" + candidate.tokenKey + "} in " + candidate.sourcePath + ".";
        }

        private void IgnoreCandidateFile(PungentTokenCandidate candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.sourcePath))
                return;

            PungentTokenScanSettings settings = ScanSettings;
            AddUnique(settings.ignoredSourcePaths, candidate.sourcePath.Replace('\\', '/'));
            SaveScanSettings();
            RefreshCandidateIgnoreState();
            _status = "Ignored token candidates from " + candidate.sourcePath + ".";
        }

        private void PingCandidateSource(PungentTokenCandidate candidate)
        {
            if (candidate == null)
                return;

            string path = !string.IsNullOrWhiteSpace(candidate.sourcePath)
                ? candidate.sourcePath
                : string.IsNullOrWhiteSpace(candidate.assetGuid) ? string.Empty : AssetDatabase.GUIDToAssetPath(candidate.assetGuid);

            if (string.IsNullOrWhiteSpace(path))
                return;

            Object source = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (source != null)
                EditorGUIUtility.PingObject(source);
        }

        private void RemoveCandidatesForKey(string key)
        {
            string clean = PungentTokenParser.NormalizeKey(key);
            _lastCandidates.RemoveAll(c => c != null && string.Equals(PungentTokenParser.NormalizeKey(c.tokenKey), clean, StringComparison.OrdinalIgnoreCase));
            RecountStoredCandidates();
        }

        private void RefreshCandidateIgnoreState()
        {
            PungentTokenScanSettings settings = ScanSettings;
            for (int i = _lastCandidates.Count - 1; i >= 0; i--)
            {
                PungentTokenCandidate candidate = _lastCandidates[i];
                if (PungentTokenIntentClassifier.IsIgnored(candidate, settings))
                    _lastCandidates.RemoveAt(i);
            }
            RecountStoredCandidates();
            _candidateSummary = BuildCandidateSummary();
        }

        private void RecountStoredCandidates()
        {
            _lastLikelyCandidateCount = _lastCandidates.Count(c => c != null && (c.intentLevel == PungentTokenIntentLevel.Likely || c.intentLevel == PungentTokenIntentLevel.Confirmed));
            _lastPossibleCandidateCount = _lastCandidates.Count(c => c != null && c.intentLevel == PungentTokenIntentLevel.Possible);
            _lastUnlikelyCandidateCount = _lastCandidates.Count(c => c != null && c.intentLevel == PungentTokenIntentLevel.Unlikely);
            _candidateSummary = BuildCandidateSummary();
            Repaint();
        }

        private void SaveScanSettings()
        {
            ScanSettings.EnsureDefaults();
            PungentTokenStorage.Save();
        }

        private sealed class CooperativeTokenProjectScan
        {
            private enum Stage
            {
                Begin,
                GatherAssets,
                ProcessAssets,
                ValidateDefinitions,
                ValidateBindings,
                Publish,
                Done
            }

            private readonly PungentAuditScanMode _mode;
            private Stage _stage = Stage.Begin;
            private PungentScanSession _session;
            private PungentScanResult _result;
            private PungentTokenScanSettings _settings;
            private string _scanFilter;
            private string[] _guids = Array.Empty<string>();
            private int _limit;
            private int _index;
            private int _scanned;
            private int _skipped;
            private int _totalTokenUsages;
            private int _likelyCandidates;
            private int _possibleCandidates;

            private CooperativeTokenProjectScan(PungentAuditScanMode mode)
            {
                _mode = mode;
            }

            public static PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
            {
                CooperativeTokenProjectScan scan = new CooperativeTokenProjectScan(mode);
                PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative("token-validator", "Token Validator", scan.Step);
                job.canPause = true;
                job.canCancel = true;
                job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                                   PungentAuditScanJobCapabilities.UsesAssetDatabase |
                                   PungentAuditScanJobCapabilities.ScanOnly;
                if (mode == PungentAuditScanMode.BackgroundIdle && CanRunBackgroundAudit())
                    job.capabilities |= PungentAuditScanJobCapabilities.BackgroundSafe;
                job.Report(0f, 0, 1, "Queued Token Validator project scan.", false, "Queued");
                return job;
            }

            public PungentAuditScanStepResult Step(PungentAuditScanContext context)
            {
                if (context.IsCancellationRequested())
                    return Cancel("Token Validator scan cancelled before the next TextAsset batch.");
                if (context.IsPauseRequested())
                    return PungentAuditScanStepResult.Continue("Token Validator scan paused.");

                try
                {
                    switch (_stage)
                    {
                        case Stage.Begin:
                            PungentTokenStorage.EnsureLoaded();
                            _settings = PungentTokenStorage.Database.EnsureScanSettings();
                            _settings.EnsureDefaults();
                            _scanFilter = LoadCoordinatorScanFilter();
                            _session = new PungentScanSession("token-validator", "Token Validator");
                            _result = _session.Begin(PungentScanScope.ProjectAssets, string.IsNullOrWhiteSpace(_scanFilter) ? "t:TextAsset" : _scanFilter);
                            if (_mode == PungentAuditScanMode.BackgroundIdle && !CanRunBackgroundAudit())
                            {
                                _result.AddIssue(PungentScanSeverity.Warning, "Background token scan needs opt-in settings", "Enable Strict Opt-In or Opt-in only in Token Validator before background project token scans can run.", null, null, "TOKEN_BACKGROUND_NEEDS_OPT_IN");
                                PungentScanResult notConfigured = _session.Complete(0, 0, 0, 0, "Background token scan skipped until scan settings are conservative.");
                                _stage = Stage.Done;
                                return PungentAuditScanStepResult.NotConfigured("Background token scan requires conservative opt-in settings.", notConfigured);
                            }
                            _stage = Stage.GatherAssets;
                            context.Report(0.05f, 0, 1, "Loading token scan settings.", false, "Settings");
                            return PungentAuditScanStepResult.Continue("Loading token scan settings.");

                        case Stage.GatherAssets:
                            _guids = GetProjectScanGuids(_settings, _scanFilter);
                            _limit = Mathf.Min(_guids.Length, _mode == PungentAuditScanMode.BackgroundIdle ? 150 : 500);
                            if (_guids.Length > _limit)
                                _result.AddIssue(PungentScanSeverity.Info, "Scan capped", "Token Validator will scan the first " + _limit + " of " + _guids.Length + " matching TextAsset(s) in this cooperative pass.", null, null, "TOKEN_SCAN_CAPPED");
                            _stage = Stage.ProcessAssets;
                            context.Report(0.12f, 0, Math.Max(1, _limit), "Gathered " + _guids.Length + " TextAsset candidate(s).", false, "Gather assets");
                            return PungentAuditScanStepResult.Continue("Gathered TextAsset candidates.");

                        case Stage.ProcessAssets:
                            int batch = _mode == PungentAuditScanMode.BackgroundIdle ? 5 : 20;
                            int stop = Math.Min(_limit, _index + batch);
                            while (_index < stop)
                                ProcessGuid(_guids[_index++]);
                            float progress = _limit <= 0 ? 0.80f : 0.12f + (0.68f * _index / Mathf.Max(1, _limit));
                            context.Report(progress, _index, Math.Max(1, _limit), _index + " / " + _limit + " TextAsset(s) checked.", false, "Scan TextAssets");
                            if (_index < _limit)
                                return PungentAuditScanStepResult.Continue(_index + " / " + _limit + " TextAsset(s) checked.");
                            _stage = Stage.ValidateDefinitions;
                            return PungentAuditScanStepResult.Continue("Validating token definitions.");

                        case Stage.ValidateDefinitions:
                            PungentTokenValidatorService.ValidateDefinitions(_result);
                            _stage = Stage.ValidateBindings;
                            context.Report(0.86f, _index, Math.Max(1, _limit), "Token definitions validated.", false, "Definitions");
                            return PungentAuditScanStepResult.Continue("Token definitions validated.");

                        case Stage.ValidateBindings:
                            PungentTokenValidatorService.ValidateBindings(_result);
                            _stage = Stage.Publish;
                            context.Report(0.92f, _index, Math.Max(1, _limit), "Token bindings validated.", false, "Bindings");
                            return PungentAuditScanStepResult.Continue("Token bindings validated.");

                        case Stage.Publish:
                            string status = "Scanned " + _scanned + " TextAsset(s), " + _totalTokenUsages + " token use(s), " + _likelyCandidates + " likely candidate(s), " + _possibleCandidates + " possible candidate(s).";
                            PungentScanResult completed = _session.Complete(_scanned, _totalTokenUsages, _skipped + Math.Max(0, _guids.Length - _limit), 0, status);
                            _stage = Stage.Done;
                            context.Report(1f, _limit, Math.Max(1, _limit), status, false, "Complete");
                            return PungentAuditScanStepResult.Complete(status, completed);

                        default:
                            return PungentAuditScanStepResult.Complete("Token Validator scan already completed.", _result);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    PungentScanResult failed = _session != null ? _session.Fail(ex, "Token Validator scan failed: " + ex.Message) : null;
                    _stage = Stage.Done;
                    return PungentAuditScanStepResult.Failed("Token Validator scan failed: " + ex.Message, failed);
                }
            }

            private void ProcessGuid(string guid)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || ShouldSkipPath(path, _settings))
                {
                    _skipped++;
                    return;
                }

                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                {
                    _skipped++;
                    return;
                }

                PungentTokenIntentFileContext intentContext = PungentTokenIntentClassifier.CreateContext(asset.text, path);
                if ((_settings.projectScanMode == PungentTokenProjectScanMode.StrictOptIn || _settings.scanOnlyOptInMarkedFiles) && !intentContext.hasPungentOptIn)
                {
                    _skipped++;
                    return;
                }

                _scanned++;
                List<PungentTokenUsage> usages = PungentTokenValidatorService.ValidateText(asset.text, path, path, null, guid, false, false);
                _totalTokenUsages += usages.Sum(u => Mathf.Max(1, u.occurrenceCount));
                List<PungentTokenCandidate> candidates = ClassifyProjectUnknowns(usages, intentContext, _settings);
                _likelyCandidates += candidates.Count(c => c != null && c.intentLevel == PungentTokenIntentLevel.Likely && !c.ignored);
                _possibleCandidates += candidates.Count(c => c != null && c.intentLevel == PungentTokenIntentLevel.Possible && !c.ignored);
                PungentTokenValidatorService.AddProjectUsagesToScan(_result, usages, candidates, _settings, asset, maxIssues: 200);
            }

            private static string[] GetProjectScanGuids(PungentTokenScanSettings settings, string scanFilter)
            {
                if (settings != null && settings.projectScanMode == PungentTokenProjectScanMode.SelectionOnly)
                {
                    return (Selection.objects ?? Array.Empty<UnityEngine.Object>())
                        .OfType<TextAsset>()
                        .Select(AssetDatabase.GetAssetPath)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(AssetDatabase.AssetPathToGUID)
                        .Where(guid => !string.IsNullOrWhiteSpace(guid))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }

                string filter = string.IsNullOrWhiteSpace(scanFilter) ? "t:TextAsset" : scanFilter;
                return AssetDatabase.FindAssets(filter);
            }

            private static bool ShouldSkipPath(string path, PungentTokenScanSettings settings)
            {
                if (settings == null)
                    return false;
                settings.EnsureDefaults();
                string normalized = (path ?? string.Empty).Replace('\\', '/');
                string lower = normalized.ToLowerInvariant();
                if (settings.excludePathHints != null && settings.excludePathHints.Any(hint => !string.IsNullOrWhiteSpace(hint) && lower.Contains(hint.Replace('\\', '/').ToLowerInvariant())))
                    return true;
                if (settings.projectScanMode == PungentTokenProjectScanMode.LikelyAuthoredText &&
                    settings.includePathHints != null &&
                    settings.includePathHints.Count > 0 &&
                    !settings.includePathHints.Any(hint => !string.IsNullOrWhiteSpace(hint) && lower.Contains(hint.Replace('\\', '/').ToLowerInvariant())))
                    return true;
                return false;
            }

            private static List<PungentTokenCandidate> ClassifyProjectUnknowns(IEnumerable<PungentTokenUsage> usages, PungentTokenIntentFileContext context, PungentTokenScanSettings settings)
            {
                List<PungentTokenCandidate> candidates = new List<PungentTokenCandidate>();
                if (usages == null)
                    return candidates;

                foreach (PungentTokenUsage usage in usages)
                {
                    if (usage == null || usage.status != PungentTokenUsageStatus.Unknown)
                        continue;
                    PungentTokenCandidate candidate = PungentTokenIntentClassifier.Classify(usage, context, settings);
                    PungentTokenIntentClassifier.IsIgnored(candidate, settings);
                    if (!candidate.ignored && candidate.intentLevel != PungentTokenIntentLevel.NotToken)
                        candidates.Add(candidate);
                }
                return candidates;
            }

            private PungentAuditScanStepResult Cancel(string status)
            {
                if (_session != null)
                    _session.Cancel(status, false);
                _stage = Stage.Done;
                return PungentAuditScanStepResult.Cancelled(status);
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
                return;

            if (!values.Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)))
                values.Add(value);
        }

        private static string JoinHints(List<string> hints)
        {
            return hints == null ? string.Empty : string.Join(", ", hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray());
        }

        private static List<string> ParseHintList(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search);
            UtilityWindowPrefs.SetString(PrefSelected, _selectedTokenKey);
            UtilityWindowPrefs.SetBool(PrefShowArchived, _showArchived);
            UtilityWindowPrefs.SetBool(PrefShowDeprecated, _showDeprecated);
            UtilityWindowPrefs.SetString(PrefScanFilter, _scanFilter);
            UtilityWindowPrefs.SetString(PrefText, _text);
            UtilityWindowPrefs.SetFloat(PrefLeftWidth, _leftWidth);
            UtilityWindowPrefs.SetFloat(PrefRightWidth, _rightWidth);
            UtilityWindowPrefs.SetBool(PrefScanIssuesFoldout, _scanIssuesExpanded);
            UtilityWindowPrefs.SetBool(PrefShowScanErrors, _showScanErrors);
            UtilityWindowPrefs.SetBool(PrefShowScanWarnings, _showScanWarnings);
            UtilityWindowPrefs.SetBool(PrefShowScanInfo, _showScanInfo);
            UtilityWindowPrefs.SetBool(PrefScanSettingsFoldout, _scanSettingsExpanded);
        }
    }

    internal sealed class TokenValidatorAuditProvider : IPungentAuditScanProvider
    {
        public string ProviderId => "token-validator";
        public string DisplayName => "Token Validator";
        public string Description => "Validates project token definitions, bindings, and likely authored text references without mutating notes or token assets.";
        public string OpenButtonLabel => "Open Token Validator";
        public string RunButtonLabel => "Run Token Scan";
        public bool CanRunImmediate => true;
        public bool CanRunBackground => PungentTokenValidatorWindow.CanRunBackgroundAudit();
        public bool CanPause => true;
        public bool CanCancel => true;
        public bool UsesSceneOpening => false;
        public bool UsesAssetDatabase => true;
        public bool UsesModalProgress => false;
        public bool CanRunFromCoordinator => true;
        public bool IsCooperative => true;
        public bool IsMonolithic => false;
        public bool IsScanOnly => true;

        public bool TryGetNotConfiguredReason(out string reason)
        {
            reason = null;
            PungentTokenStorage.EnsureLoaded();
            if (PungentTokenStorage.Database == null)
            {
                reason = "Token database is unavailable.";
                return true;
            }
            return false;
        }

        public PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
        {
            return PungentTokenValidatorWindow.CreateAuditJob(mode);
        }

        public void OpenWindow()
        {
            PungentTokenValidatorWindow.Open();
        }
    }
#endif
}
