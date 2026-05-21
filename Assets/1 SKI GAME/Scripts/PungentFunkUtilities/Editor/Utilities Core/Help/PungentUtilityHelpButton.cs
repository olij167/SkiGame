namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Developer;
    using PungentFunk.Utilities.Editor.ProjectAudit;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    [Serializable]
    public sealed class PungentUtilityHelpContext
    {
        public string utilityId = string.Empty;
        public string sectionId = string.Empty;
        public string topicId = string.Empty;
        public string label = string.Empty;
        public string location = string.Empty;
        public string sourcePath = string.Empty;
        public string selectedContextId = string.Empty;
        public string selectedContextLabel = string.Empty;
        public int sourceLine;
        public bool generated;

        public string StableTopicId => PungentUtilityHelpIds.TopicKey(utilityId, sectionId, topicId);

        public void Normalize()
        {
            utilityId = PungentUtilityHelpIds.Normalize(utilityId);
            sectionId = PungentUtilityHelpIds.Normalize(sectionId, PungentUtilityHelpIds.DefaultSection);
            topicId = PungentUtilityHelpIds.Normalize(topicId, PungentUtilityHelpIds.DefaultTopic);
            label = label == null ? string.Empty : label.Trim();
            location = location == null ? string.Empty : location.Trim();
            sourcePath = sourcePath == null ? string.Empty : sourcePath.Trim();
            selectedContextId = selectedContextId == null ? string.Empty : selectedContextId.Trim();
            selectedContextLabel = selectedContextLabel == null ? string.Empty : selectedContextLabel.Trim();
        }
    }

    public static class PungentUtilityHelpButton
    {
        private const float Size = 22f;
        private static readonly Dictionary<string, PungentUtilityHelpContext> RuntimeContexts = new Dictionary<string, PungentUtilityHelpContext>(StringComparer.OrdinalIgnoreCase);

        public static bool Draw(string utilityId, string sectionId = null, string topicId = null, string tooltip = null, string contextLabel = null, string selectedContextId = null, string selectedContextLabel = null)
        {
            return DrawIcon(utilityId, sectionId, topicId, tooltip, contextLabel, selectedContextId, selectedContextLabel);
        }

        public static bool DrawIcon(string utilityId, string sectionId = null, string topicId = null, string tooltip = null, string contextLabel = null, string selectedContextId = null, string selectedContextLabel = null)
        {
            PungentUtilityHelpContext context = RegisterRuntimeContext(utilityId, sectionId, topicId, contextLabel, selectedContextId, selectedContextLabel);
            GUIContent content = new GUIContent("?", BuildTooltip(context, tooltip));
            Rect rect = GUILayoutUtility.GetRect(content, EditorStyles.miniButton, GUILayout.Width(Size), GUILayout.Height(Size));
            Event current = Event.current;
            if (current != null && current.type == EventType.ContextClick && rect.Contains(current.mousePosition))
            {
                ShowContextMenu(context);
                current.Use();
                return false;
            }

            bool openFullDirectly = current != null && (current.alt || current.control || current.command);
            if (!GUI.Button(rect, content, EditorStyles.miniButton))
                return false;

            if (openFullDirectly)
                Open(context.utilityId, context.sectionId, context.topicId);
            else
                PungentUtilityQuickHelpTray.Show(rect, context);
            return true;
        }

        public static void Open(string utilityId, string sectionId = null, string topicId = null)
        {
            PungentUtilityHelpRegistry.Open(utilityId, sectionId, topicId);
        }

        public static List<PungentUtilityHelpContext> GetRuntimeContexts()
        {
            return new List<PungentUtilityHelpContext>(RuntimeContexts.Values);
        }

        private static PungentUtilityHelpContext RegisterRuntimeContext(string utilityId, string sectionId, string topicId, string label, string selectedContextId, string selectedContextLabel)
        {
            PungentUtilityHelpContext context = new PungentUtilityHelpContext
            {
                utilityId = utilityId,
                sectionId = sectionId,
                topicId = topicId,
                label = label,
                location = "Runtime draw path",
                selectedContextId = selectedContextId,
                selectedContextLabel = selectedContextLabel
            };
            context.Normalize();
            RuntimeContexts[context.StableTopicId] = context;
            return context;
        }

        private static string BuildTooltip(PungentUtilityHelpContext context, string tooltip)
        {
            if (!string.IsNullOrWhiteSpace(tooltip))
                return tooltip;

            PungentUtilityDescriptor utility = PungentUtilityRegistry.Find(context.utilityId);
            string utilityName = utility == null ? context.utilityId : utility.DisplayName;
            string target = string.IsNullOrWhiteSpace(context.topicId) ? context.sectionId : context.topicId;
            string missing = PungentUtilityHelpRegistry.Find(context.utilityId, context.sectionId, context.topicId) == null
                ? "\nTopic has not been written yet; Developer Mode can create a stub."
                : string.Empty;
            string contextSuffix = string.IsNullOrWhiteSpace(context.selectedContextLabel)
                ? string.Empty
                : "\nContext: " + context.selectedContextLabel;
            return string.IsNullOrWhiteSpace(target)
                ? "Open help for " + utilityName + "."
                : "Open help for " + utilityName + " / " + ObjectNames.NicifyVariableName(target) + "." + contextSuffix + missing;
        }

        private static void ShowContextMenu(PungentUtilityHelpContext context)
        {
            GenericMenu menu = new GenericMenu();
            Vector2 mousePosition = Event.current == null ? Vector2.zero : Event.current.mousePosition;
            menu.AddItem(new GUIContent("Open Quick Help"), false, () => PungentUtilityQuickHelpTray.Show(new Rect(mousePosition, Vector2.zero), context));
            menu.AddItem(new GUIContent("Open Full Help Browser"), false, () => Open(context.utilityId, context.sectionId, context.topicId));
            menu.AddItem(new GUIContent("Copy Help Link"), false, () => EditorGUIUtility.systemCopyBuffer = "pungent-help://" + context.StableTopicId);
            menu.AddItem(new GUIContent("Copy Topic ID"), false, () => EditorGUIUtility.systemCopyBuffer = context.StableTopicId);
            if (!string.IsNullOrWhiteSpace(context.selectedContextId))
                menu.AddItem(new GUIContent("Copy Selected Context ID"), false, () => EditorGUIUtility.systemCopyBuffer = context.selectedContextId);

            menu.ShowAsContext();
        }
    }

    public static class PungentUtilityQuickHelpTray
    {
        public static void Show(Rect activatorRect, PungentUtilityHelpContext context)
        {
            if (context == null)
                return;

            PungentUtilityHelpContext copy = new PungentUtilityHelpContext
            {
                utilityId = context.utilityId,
                sectionId = context.sectionId,
                topicId = context.topicId,
                label = context.label,
                location = context.location,
                sourcePath = context.sourcePath,
                selectedContextId = context.selectedContextId,
                selectedContextLabel = context.selectedContextLabel,
                sourceLine = context.sourceLine,
                generated = context.generated
            };
            copy.Normalize();
            PopupWindow.Show(activatorRect, new PungentUtilityQuickHelpPopupContent(copy));
        }
    }

    internal sealed class PungentUtilityQuickHelpPopupContent : PopupWindowContent
    {
        private const float Width = 430f;
        private const float Height = 540f;
        private const int MaxQuickUseCharacters = 720;

        private readonly PungentUtilityHelpContext _initialContext;
        private readonly List<PungentUtilityHelpTopic> _topics = new List<PungentUtilityHelpTopic>();
        private Vector2 _scroll;
        private PungentUtilityHelpTopic _selectedTopic;
        private bool _showTopicList;

        public PungentUtilityQuickHelpPopupContent(PungentUtilityHelpContext context)
        {
            _initialContext = context;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(Width, Height);
        }

        public override void OnOpen()
        {
            BuildTopicList();
        }

        public override void OnGUI(Rect rect)
        {
            DrawHeader();
            DrawNavigation();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUILayout.ExpandHeight(true));
            try
            {
                if (_showTopicList)
                    DrawTopicList();
                else
                    DrawTopicBody();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            DrawFooter();
        }

        private void BuildTopicList()
        {
            _topics.Clear();
            List<PungentUtilityHelpTopic> utilityTopics = PungentUtilityHelpRegistry.TopicsForUtility(_initialContext.utilityId, false, false, includeGenerated: false);
            PungentUtilityHelpTopic requested = PungentUtilityHelpRegistry.Find(_initialContext.utilityId, _initialContext.sectionId, _initialContext.topicId);

            foreach (PungentUtilityHelpTopic topic in utilityTopics.OrderBy(TopicSortKey).ThenBy(t => t.title, StringComparer.OrdinalIgnoreCase))
                AddTopic(topic);

            if (requested != null)
            {
                AddTopic(requested);
                foreach (string relatedId in requested.relatedTopicIds ?? new List<string>())
                {
                    PungentUtilityHelpTopic related = PungentUtilityHelpRegistry.FindByStableId(NormalizeRelatedTopicId(requested.utilityId, relatedId));
                    AddTopic(related);
                }
            }

            _selectedTopic = requested;
            _showTopicList = requested == null;
        }

        private void AddTopic(PungentUtilityHelpTopic topic)
        {
            if (topic == null || _topics.Any(t => string.Equals(t.StableId, topic.StableId, StringComparison.OrdinalIgnoreCase)))
                return;

            _topics.Add(topic);
        }

        private int TopicSortKey(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return 99;
            if (string.Equals(topic.sectionId, PungentUtilityHelpIds.DefaultSection, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(topic.topicId, PungentUtilityHelpIds.DefaultTopic, StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(topic.sectionId, _initialContext.sectionId, StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(topic.sectionId, "controls-tooltips", StringComparison.OrdinalIgnoreCase))
                return 3;
            return 2;
        }

        private void DrawHeader()
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(_initialContext.utilityId);
            string utilityName = descriptor == null ? _initialContext.utilityId : descriptor.DisplayName;
            string title = _selectedTopic == null ? ObjectNames.NicifyVariableName(_initialContext.topicId) : _selectedTopic.title;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                GUILayout.Label(new GUIContent("? " + utilityName + " / " + ObjectNames.NicifyVariableName(_initialContext.sectionId), _initialContext.StableTopicId), EditorStyles.miniBoldLabel, GUILayout.MinWidth(160f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Open full help", "Open the full Help Browser to this topic."), EditorStyles.toolbarButton, GUILayout.Width(104f)))
                    OpenFullAndClose();
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.08f, 0.03f, 4, 2)))
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(title) ? "Quick Help" : title, UtilityWindowTheme.SectionHeaderStyle);
                if (!string.IsNullOrWhiteSpace(_initialContext.selectedContextLabel))
                    EditorGUILayout.LabelField("Context: " + _initialContext.selectedContextLabel, UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField(_selectedTopic == null ? _initialContext.StableTopicId : _selectedTopic.StableId, UtilityWindowTheme.PathLabelStyle);
            }
        }

        private void DrawNavigation()
        {
            int selectedIndex = SelectedIndex;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Topics", "Show local help topics for this utility."), EditorStyles.linkLabel, GUILayout.Width(58f)))
                    _showTopicList = true;

                using (new EditorGUI.DisabledScope(_topics.Count <= 1 || selectedIndex <= 0))
                {
                    if (GUILayout.Button(new GUIContent("< Previous", "Show the previous local topic."), EditorStyles.linkLabel, GUILayout.Width(78f)))
                        SelectTopicAt(selectedIndex - 1);
                }

                using (new EditorGUI.DisabledScope(_topics.Count <= 1 || selectedIndex < 0 || selectedIndex >= _topics.Count - 1))
                {
                    if (GUILayout.Button(new GUIContent("Next >", "Show the next local topic."), EditorStyles.linkLabel, GUILayout.Width(58f)))
                        SelectTopicAt(selectedIndex + 1);
                }

                GUILayout.FlexibleSpace();
                UtilityWindowTheme.InfoPill(new GUIContent(_topics.Count + " topics", "Local topic list for this utility and current section."), UtilityWindowTheme.Neutral, 74f);
            }
        }

        private void DrawTopicList()
        {
            EditorGUILayout.LabelField("Topic Glossary", UtilityWindowTheme.SectionHeaderStyle);
            if (_topics.Count == 0)
            {
                EditorGUILayout.HelpBox("No help topics are registered for this utility yet. Open the full Help Browser for the available documentation directory.", MessageType.Info);
                return;
            }

            foreach (PungentUtilityHelpTopic topic in _topics)
            {
                bool selected = _selectedTopic != null && string.Equals(topic.StableId, _selectedTopic.StableId, StringComparison.OrdinalIgnoreCase);
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(selected ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, selected ? 0.18f : 0.08f, 0.04f, 4, 2)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(string.IsNullOrWhiteSpace(topic.title) ? topic.topicId : topic.title, topic.StableId), selected ? EditorStyles.miniButton : EditorStyles.label))
                        {
                            SelectTopic(topic);
                        }
                        PungentUtilityHelpSourceBadge.DrawTopicBadges(topic);
                    }

                    if (!string.IsNullOrWhiteSpace(topic.summary))
                        EditorGUILayout.LabelField(topic.summary, UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawTopicBody()
        {
            if (_selectedTopic == null)
            {
                DrawMissingTopic();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                PungentUtilityHelpSourceBadge.DrawTopicBadges(_selectedTopic);
                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrWhiteSpace(_selectedTopic.summary))
                EditorGUILayout.LabelField(_selectedTopic.summary, UtilityWindowTheme.BodyStyle);

            string quickUse = TrimText(_selectedTopic.quickUseMarkdown, MaxQuickUseCharacters);
            if (!string.IsNullOrWhiteSpace(quickUse))
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Quick Use", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.LabelField(quickUse, UtilityWindowTheme.BodyStyle);
            }

            DrawFeaturePreview(_selectedTopic);
            DrawScriptingPreview(_selectedTopic);
            DrawCompactCounts(_selectedTopic);
        }

        private void DrawMissingTopic()
        {
            EditorGUILayout.HelpBox("This help topic has not been written yet. Open the full Help Browser for the missing-topic page.", MessageType.Info);
            EditorGUILayout.LabelField(_initialContext.StableTopicId, UtilityWindowTheme.PathLabelStyle);
        }

        private void DrawMissingTopicDeveloperAction()
        {
            if (!PungentDeveloperMode.Available || !PungentDeveloperMode.Enabled)
                return;

            if (GUILayout.Button(new GUIContent("Create Topic Stub", "Create a manual help topic stub for this destination."), EditorStyles.miniButton, GUILayout.Width(132f)))
            {
                PungentUtilityHelpTopic topic = PungentUtilityHelpRegistry.CreateTopicStub(_initialContext.utilityId, _initialContext.sectionId, _initialContext.topicId);
                BuildTopicList();
                SelectTopic(topic);
            }
        }

        private static void DrawFeaturePreview(PungentUtilityHelpTopic topic)
        {
            List<PungentUtilityHelpFeatureEntry> entries = topic.featureEntries == null
                ? new List<PungentUtilityHelpFeatureEntry>()
                : topic.featureEntries.Where(IsVisibleEntry).Take(5).ToList();
            if (entries.Count == 0)
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Features", UtilityWindowTheme.SectionHeaderStyle);
            foreach (PungentUtilityHelpFeatureEntry entry in entries)
                EditorGUILayout.LabelField("- " + (string.IsNullOrWhiteSpace(entry.label) ? entry.id : entry.label) + ": " + TrimText(entry.description, 160), UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static void DrawScriptingPreview(PungentUtilityHelpTopic topic)
        {
            if (!IsScriptingOriented(topic) || topic.scriptingEntries == null)
                return;

            List<PungentUtilityHelpScriptingEntry> entries = topic.scriptingEntries.Where(IsVisibleEntry).Take(4).ToList();
            if (entries.Count == 0)
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Scripting", UtilityWindowTheme.SectionHeaderStyle);
            foreach (PungentUtilityHelpScriptingEntry entry in entries)
                EditorGUILayout.LabelField("- " + (string.IsNullOrWhiteSpace(entry.signature) ? entry.memberName : entry.signature), UtilityWindowTheme.PathLabelStyle);
        }

        private static void DrawCompactCounts(PungentUtilityHelpTopic topic)
        {
            int relatedCount = topic.relatedTopicIds == null ? 0 : topic.relatedTopicIds.Count;
            int docCount = PungentUtilityHelpDocumentationLinksProvider.GetRelatedLinks(topic, includeUtilityAssigned: true, includeGlobal: true).Count;
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(relatedCount + " related", relatedCount > 0 ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral, 82f);
                UtilityWindowTheme.CountPill(docCount + " docs", docCount > 0 ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 70f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                if (GUILayout.Button(new GUIContent("Open Full Help Browser", "Open the full Help Browser to this topic."), EditorStyles.toolbarButton, GUILayout.MinWidth(148f)))
                    OpenFullAndClose();

                if (GUILayout.Button(new GUIContent("Copy link", "Copy this help topic link."), EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    EditorGUIUtility.systemCopyBuffer = "pungent-help://" + CurrentStableId;

                if (GUILayout.Button(new GUIContent("Request", "Create a support request draft for this help context."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    PungentBugReportOverlay.Show(new Rect(Event.current == null ? Vector2.zero : Event.current.mousePosition, Vector2.zero), CurrentReportContext());
                    ClosePopup();
                    GUIUtility.ExitGUI();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private int SelectedIndex => _selectedTopic == null ? -1 : _topics.FindIndex(t => string.Equals(t.StableId, _selectedTopic.StableId, StringComparison.OrdinalIgnoreCase));

        private string CurrentStableId => _selectedTopic == null ? _initialContext.StableTopicId : _selectedTopic.StableId;

        private PungentBugReportContext CurrentReportContext()
        {
            if (_selectedTopic == null)
                return PungentBugReportContext.FromHelpContext(_initialContext, "Quick Help Tray");

            PungentBugReportContext context = new PungentBugReportContext
            {
                utilityId = _selectedTopic.utilityId,
                sectionId = _selectedTopic.sectionId,
                topicId = _selectedTopic.topicId,
                contextLabel = string.IsNullOrWhiteSpace(_initialContext.label) ? _selectedTopic.title : _initialContext.label,
                contextPath = _selectedTopic.StableId,
                sourceWindow = "Quick Help Tray",
                selectedContextId = _initialContext.selectedContextId,
                selectedContextLabel = _initialContext.selectedContextLabel
            };
            context.Normalize();
            return context;
        }

        private void SelectTopicAt(int index)
        {
            if (index < 0 || index >= _topics.Count)
                return;

            SelectTopic(_topics[index]);
        }

        private void SelectTopic(PungentUtilityHelpTopic topic)
        {
            _selectedTopic = topic;
            _showTopicList = false;
            _scroll = Vector2.zero;
        }

        private void OpenFullAndClose()
        {
            if (_selectedTopic != null)
                PungentUtilityHelpRegistry.Open(_selectedTopic.utilityId, _selectedTopic.sectionId, _selectedTopic.topicId);
            else
                PungentUtilityHelpRegistry.Open(_initialContext.utilityId, _initialContext.sectionId, _initialContext.topicId);
            ClosePopup();
            GUIUtility.ExitGUI();
        }

        private void ClosePopup()
        {
            if (editorWindow != null)
                editorWindow.Close();
        }

        private static bool IsVisibleEntry(PungentUtilityHelpFeatureEntry entry)
        {
            if (entry == null)
                return false;
            return !entry.hidden && !entry.developerOnly && !entry.generated;
        }

        private static bool IsVisibleEntry(PungentUtilityHelpScriptingEntry entry)
        {
            if (entry == null)
                return false;
            return !entry.hidden && !entry.developerOnly && !entry.generated;
        }

        private static bool IsScriptingOriented(PungentUtilityHelpTopic topic)
        {
            if (topic == null)
                return false;
            string id = (topic.sectionId + " " + topic.topicId + " " + topic.title).ToLowerInvariant();
            return id.Contains("scripting") || id.Contains("api") || id.Contains("reference");
        }

        private static string TrimText(string value, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string trimmed = value.Trim();
            if (trimmed.Length <= maxCharacters)
                return trimmed;
            return trimmed.Substring(0, Math.Max(0, maxCharacters - 3)).TrimEnd() + "...";
        }

        private static string NormalizeRelatedTopicId(string currentUtilityId, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            string trimmed = id.Trim();
            string[] parts = trimmed.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                return PungentUtilityHelpIds.TopicKey(parts[0], parts[1], parts[2]);
            if (parts.Length == 2)
                return PungentUtilityHelpIds.TopicKey(parts[0], parts[1], parts[1]);
            return PungentUtilityHelpIds.TopicKey(currentUtilityId, trimmed, trimmed);
        }
    }

    public static class PungentBugReportOverlay
    {
        public static void Show(Rect activatorRect, PungentBugReportContext context)
        {
            context = context ?? new PungentBugReportContext();
            context.Normalize();
            PungentSupportRequestBridge.OpenFromBugReportContext(context);
        }
    }

    internal sealed class PungentBugReportPopupContent : PopupWindowContent
    {
        private const float Width = 520f;
        private const float Height = 620f;

        private readonly PungentBugReportContext _context;
        private Vector2 _scroll;
        private string _title = string.Empty;
        private string _details = string.Empty;
        private string _contactEmail = string.Empty;
        private string _contactDiscord = string.Empty;
        private PungentBugReportCategory _category = PungentBugReportCategory.Bug;
        private PungentBugReportSeverity _severity = PungentBugReportSeverity.Normal;
        private bool _includeDiagnostics;
        private bool _includeConsoleSummary;
        private bool _showMetadata = true;
        private bool _showPayload;
        private bool _showBackendSettings;
        private bool _submitting;
        private string _status = "Ready.";

        public PungentBugReportPopupContent(PungentBugReportContext context)
        {
            _context = context ?? new PungentBugReportContext();
            _context.Normalize();
            _title = DefaultTitle(_context);
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(Width, Height);
        }

        public override void OnGUI(Rect rect)
        {
            DrawHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUILayout.ExpandHeight(true));
            try
            {
                DrawBody();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            DrawFooter();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                GUILayout.Label(new GUIContent("Report Issue", "Send a PungentFunk utility bug report through the configured Wix relay."), EditorStyles.miniBoldLabel, GUILayout.Width(120f));
                GUILayout.FlexibleSpace();
                if (PungentDeveloperMode.Available && PungentDeveloperMode.Enabled &&
                    GUILayout.Button(new GUIContent("Backend Topic", "Open the backend contract help topic."), EditorStyles.toolbarButton, GUILayout.Width(104f)))
                {
                    PungentUtilityHelpRegistry.Open("help-browser", "bug-report-backend-contract", "bug-report-backend-contract");
                }
            }

            EditorGUILayout.HelpBox(PungentBugReportSettings.ScaffoldNotice, MessageType.Info);
        }

        private void DrawBody()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.10f, 0.05f, 5, 3)))
            {
                EditorGUILayout.LabelField("Report Details", UtilityWindowTheme.SectionHeaderStyle);
                _title = EditorGUILayout.TextField(new GUIContent("Title", "Short summary for the PungentFunk bug report relay."), _title);
                _category = (PungentBugReportCategory)EditorGUILayout.EnumPopup(new GUIContent("Category", "Bug report category."), _category);
                _severity = (PungentBugReportSeverity)EditorGUILayout.EnumPopup(new GUIContent("Severity", "Impact level."), _severity);
                EditorGUILayout.LabelField(new GUIContent("Details", "What happened, expected behaviour, steps to reproduce, or proposed correction."));
                _details = EditorGUILayout.TextArea(_details, GUILayout.MinHeight(96f));
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.08f, 0.04f, 5, 3)))
            {
                EditorGUILayout.LabelField("Optional Contact", UtilityWindowTheme.SectionHeaderStyle);
                _contactEmail = EditorGUILayout.TextField(new GUIContent("Email", "Optional contact email. Leave empty for no email contact."), _contactEmail);
                _contactDiscord = EditorGUILayout.TextField(new GUIContent("Discord", "Optional Discord handle. Leave empty for no Discord contact."), _contactDiscord);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.08f, 0.04f, 5, 3)))
            {
                EditorGUILayout.LabelField("Consent", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.LabelField("Reports include title, details, category, severity, utility/topic context, Unity version, platform, timestamp, and an anonymous install id. Contact details and diagnostics are optional.", UtilityWindowTheme.MutedMiniLabelStyle);
                _includeDiagnostics = EditorGUILayout.ToggleLeft(new GUIContent("Include safe diagnostics", DeveloperContextVisible ? "Includes Unity version, platform, utility/topic IDs, and source context when available." : "Includes Unity version, platform, and utility/topic IDs. Source paths remain Developer Mode-only."), _includeDiagnostics);
                _includeConsoleSummary = EditorGUILayout.ToggleLeft(new GUIContent("Include console summary", "Currently scaffolded; future implementation should include an opt-in sanitized recent exception summary."), _includeConsoleSummary);
            }

            DrawMetadataPreview();
            DrawBackendSettings();
            DrawPayloadPreview();
        }

        private void DrawMetadataPreview()
        {
            _showMetadata = EditorGUILayout.Foldout(_showMetadata, new GUIContent("Auto Metadata Preview", "Context captured from Help Browser, Quick Help, or the help button."), true);
            if (!_showMetadata)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 5, 3)))
            {
                DrawReadonly("Utility", _context.utilityId);
                DrawReadonly("Section", _context.sectionId);
                DrawReadonly("Topic", _context.topicId);
                DrawReadonly("Context", _context.contextLabel);
                DrawReadonly("Path", string.IsNullOrWhiteSpace(_context.contextPath) ? _context.StableTopicId : _context.contextPath);
                DrawReadonly("Window", _context.sourceWindow);
                if (!string.IsNullOrWhiteSpace(_context.helpTab))
                    DrawReadonly("Help Tab", _context.helpTab);
                if (DeveloperContextVisible && !string.IsNullOrWhiteSpace(_context.generatedEntryId))
                    DrawReadonly("Generated Entry", _context.generatedEntryId);
                if (DeveloperContextVisible && !string.IsNullOrWhiteSpace(_context.sourcePath))
                    DrawReadonly("Source", _context.sourcePath + (_context.sourceLine > 0 ? ":" + _context.sourceLine : string.Empty));
            }
        }

        private void DrawBackendSettings()
        {
            if (!DeveloperContextVisible)
                return;

            _showBackendSettings = EditorGUILayout.Foldout(_showBackendSettings, new GUIContent("Relay Settings", "Local editor settings for the Wix relay endpoint. No Discord secrets are stored here."), true);
            if (!_showBackendSettings)
                return;

            PungentBugReportStorage storage = PungentBugReportStorage.instance;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.08f, 0.04f, 5, 3)))
            {
                storage.EnsureDefaults();
                EditorGUILayout.LabelField("Default relay endpoint: " + PungentBugReportSettings.SuggestedBackendEndpoint, UtilityWindowTheme.PathLabelStyle);
                EditorGUI.BeginChangeCheck();
                storage.lastEndpointUrl = EditorGUILayout.TextField(new GUIContent("Endpoint", "Configurable Wix relay endpoint. Do not place Discord webhook URLs or secrets in this field."), storage.lastEndpointUrl);
                storage.backendEnabled = EditorGUILayout.ToggleLeft(new GUIContent("Enable relay submit", "Allow Send Report to POST to the configured Wix relay."), storage.backendEnabled);
                storage.scaffoldAcknowledged = EditorGUILayout.ToggleLeft(new GUIContent("Wix setup acknowledged", "Records that this local developer has configured or reviewed the Wix relay setup."), storage.scaffoldAcknowledged);
                if (EditorGUI.EndChangeCheck())
                    storage.Persist();

                if (!string.IsNullOrWhiteSpace(storage.lastSuccessfulReportId))
                    DrawReadonly("Last Report ID", storage.lastSuccessfulReportId);
                if (!string.IsNullOrWhiteSpace(storage.lastSubmitError))
                    DrawReadonly("Last Error", storage.lastSubmitError);
            }
        }

        private void DrawPayloadPreview()
        {
            if (!DeveloperContextVisible)
                return;

            _showPayload = EditorGUILayout.Foldout(_showPayload, new GUIContent("Payload Preview", "JSON payload that can be copied, saved, or queued locally."), true);
            if (!_showPayload)
                return;

            EditorGUILayout.TextArea(PungentBugReportService.ToJson(CurrentPayload()), GUILayout.MinHeight(150f));
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(26f)))
            {
                if (GUILayout.Button(new GUIContent("Copy JSON", "Copy the current JSON payload to the clipboard."), EditorStyles.toolbarButton, GUILayout.Width(78f)))
                {
                    PungentBugReportService.CopyPayloadToClipboard(CurrentPayload());
                    _status = "Copied bug report payload.";
                }

                if (GUILayout.Button(new GUIContent("Save Draft", "Save this report as a local editor draft."), EditorStyles.toolbarButton, GUILayout.Width(82f)))
                {
                    PungentBugReportService.SaveDraft(CurrentPayload());
                    _status = "Saved local bug report draft.";
                }

                if (GUILayout.Button(new GUIContent("Queue Locally", "Queue this report locally without sending a network request."), EditorStyles.toolbarButton, GUILayout.Width(98f)))
                {
                    PungentBugReportService.QueueLocally(CurrentPayload());
                    _status = "Queued locally; no network request was sent.";
                }

                string reason;
                bool canSubmit = PungentBugReportService.CanAttemptSubmit(out reason);
                string validation = ValidateCurrentReport();
                bool canSend = canSubmit && string.IsNullOrEmpty(validation) && !_submitting;
                using (new EditorGUI.DisabledScope(!canSend))
                {
                    string tooltip = !string.IsNullOrEmpty(validation) ? validation : canSubmit ? "Send this report to the configured Wix relay." : reason;
                    if (GUILayout.Button(new GUIContent(_submitting ? "Sending..." : "Send Report", tooltip), EditorStyles.toolbarButton, GUILayout.Width(92f)))
                        SendCurrentReport();
                }

                if (DeveloperContextVisible && GUILayout.Button(new GUIContent("Copy Wix Setup", "Copy Wix relay implementation notes."), EditorStyles.toolbarButton, GUILayout.Width(108f)))
                {
                    EditorGUIUtility.systemCopyBuffer = PungentBugReportService.BackendTodoText();
                    _status = "Copied Wix relay setup notes.";
                }
            }

            EditorGUILayout.LabelField(_status, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void SendCurrentReport()
        {
            string validation = ValidateCurrentReport();
            if (!string.IsNullOrEmpty(validation))
            {
                _status = validation;
                return;
            }

            _submitting = true;
            if (!PungentBugReportService.TrySubmit(CurrentPayload(), (success, message, reportId) =>
            {
                _submitting = false;
                _status = message;
                editorWindow?.Repaint();
            }, out _status))
            {
                _submitting = false;
            }
        }

        private string ValidateCurrentReport()
        {
            if (string.IsNullOrWhiteSpace(_title) || _title.Trim().Length < 3)
                return "Add a short title before sending.";
            if (string.IsNullOrWhiteSpace(_details) || _details.Trim().Length < 10)
                return "Add report details before sending.";
            return string.Empty;
        }

        private PungentBugReportPayload CurrentPayload()
        {
            return PungentBugReportService.BuildPayload(
                _context,
                _title,
                _details,
                _category,
                _severity,
                _contactEmail,
                _contactDiscord,
                _includeDiagnostics,
                _includeConsoleSummary);
        }

        private static void DrawReadonly(string label, string value)
        {
            EditorGUILayout.LabelField(label, string.IsNullOrWhiteSpace(value) ? "(none)" : value);
        }

        private static string DefaultTitle(PungentBugReportContext context)
        {
            string topic = context == null ? string.Empty : context.topicId;
            string label = context == null ? string.Empty : context.contextLabel;
            if (!string.IsNullOrWhiteSpace(label))
                return "Issue: " + label;
            if (!string.IsNullOrWhiteSpace(topic))
                return "Issue: " + ObjectNames.NicifyVariableName(topic);
            return "PungentFunk utility issue";
        }

        private static bool DeveloperContextVisible => PungentDeveloperMode.Available && PungentDeveloperMode.Enabled;
    }
#endif
}
