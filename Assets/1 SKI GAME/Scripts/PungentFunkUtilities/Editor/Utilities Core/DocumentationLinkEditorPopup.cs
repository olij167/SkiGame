namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Developer;
    using PungentFunk.Utilities.Editor.Core.Help;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public sealed class DocumentationLinkEditorPopup : EditorWindow
    {
        private const string HelpUtilityId = "documentation-links";
        private const string PrefPrefix = "PungentFunkUtilities.DocumentationLinks.";
        private const string PrefListWidth = PrefPrefix + "ListWidth";
        private const string PrefOnlyThisUtility = PrefPrefix + "OnlyThisUtility";
        private const string PrefAssignmentFoldout = PrefPrefix + "AssignmentFoldout";
        private const string PrefReplaceFoldout = PrefPrefix + "ReplaceFoldout";
        private const string PrefSuppressReplaceWarning = PrefPrefix + "SuppressReplaceWarning";

        private string _search = string.Empty;
        private string _utilitySearch = string.Empty;
        private string _selectedLinkId = string.Empty;
        private string _selectedUtilityId = string.Empty;
        private UnityEngine.Object _updateAsset;
        private UnityEngine.Object _setupAsset;
        private UnityEngine.Object _replaceAsset;
        private string _updateExternalPath = string.Empty;
        private string _updateVersionLabel = string.Empty;
        private string _updateNotes = string.Empty;
        private string _setupExternalPath = string.Empty;
        private string _setupVersionLabel = string.Empty;
        private string _replaceExternalPath = string.Empty;
        private string _replaceVersionLabel = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private bool _backlogFoldout = true;
        private bool _onlyThisUtility;
        private bool _assignmentFoldout;
        private bool _replaceFoldout;
        private bool _suppressReplaceWarning;
        private float _listWidth = 280f;

        [MenuItem(PungentUtilityMenuPaths.Root + "/Core/Documentation Links")]
        public static void Open()
        {
            if (!PungentUtilityRegistry.CanOpen(HelpUtilityId, true))
                return;

            DocumentationLinkEditorPopup window = GetWindow<DocumentationLinkEditorPopup>("Documentation Links");
            window.minSize = new Vector2(620f, 480f);
            window.Show();
        }

        public static void OpenForUtility(string utilityId)
        {
            if (!PungentUtilityRegistry.CanOpen(HelpUtilityId, true))
                return;

            DocumentationLinkEditorPopup window = GetWindow<DocumentationLinkEditorPopup>("Documentation Links");
            window.minSize = new Vector2(620f, 480f);
            window._selectedUtilityId = utilityId ?? string.Empty;
            window._onlyThisUtility = false;
            PungentUtilityDocumentationLinks.DocumentationLink first = PungentUtilityDocumentationLinks.instance.GetLinksForUtility(utilityId).FirstOrDefault();
            window.SelectLink(first);
            window.Show();
        }

        public static void OpenForLink(string linkId, string utilityId = null)
        {
            if (!PungentUtilityRegistry.CanOpen(HelpUtilityId, true))
                return;

            DocumentationLinkEditorPopup window = GetWindow<DocumentationLinkEditorPopup>("Documentation Links");
            window.minSize = new Vector2(620f, 480f);
            window._selectedUtilityId = utilityId ?? string.Empty;
            window._onlyThisUtility = false;
            window._selectedLinkId = linkId ?? string.Empty;
            window.Show();
        }

        public static void ManageForUtility(string utilityId) => OpenForUtility(utilityId);

        public static void CreateForUtility(string utilityId)
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            PungentUtilityDocumentationLinks.DocumentationLink link = new PungentUtilityDocumentationLinks.DocumentationLink
            {
                displayName = descriptor != null ? descriptor.DisplayName + " Documentation" : "Utility Documentation",
                category = descriptor != null ? PungentUtilityCategories.GetDisplayName(PungentUtilityRegistry.GetAreaCategory(descriptor)) : string.Empty,
                versionLabel = "Current",
                utilityIds = string.IsNullOrWhiteSpace(utilityId) ? Array.Empty<string>() : new[] { utilityId }
            };

            PungentUtilityDocumentationLinks.instance.AddLink(link);
            DocumentationLinkEditorPopup window = GetWindow<DocumentationLinkEditorPopup>("Documentation Links");
            window.minSize = new Vector2(620f, 480f);
            window._selectedUtilityId = utilityId ?? string.Empty;
            window._onlyThisUtility = false;
            window.SelectLink(link);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Documentation Links");
            _listWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefListWidth, 280f), 220f, 420f);
            _onlyThisUtility = UtilityWindowPrefs.GetBool(PrefOnlyThisUtility, false);
            _assignmentFoldout = UtilityWindowPrefs.GetBool(PrefAssignmentFoldout, false);
            _replaceFoldout = UtilityWindowPrefs.GetBool(PrefReplaceFoldout, false);
            _suppressReplaceWarning = UtilityWindowPrefs.GetBool(PrefSuppressReplaceWarning, false);
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions { UtilityId = "documentation-links", Title = "Documentation Links", Description = "Assign documentation to utilities and keep previous versions in a small project-local backlog.", Status = BuildStatusText(), ShowHelp = true, ShowMinimizeTray = true, ShowMinimizeButton = true, Tint = UtilityWindowTheme.HeaderTint });

            DrawHeaderControls();
            DrawContextBar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLinksList();
                UtilityWindowTheme.HorizontalResizeHandle(ref _listWidth, 220f, Mathf.Min(460f, Mathf.Max(260f, position.width - 320f)), SavePrefs, "Drag to resize docs list");
                DrawSelectedLinkDetails();
            }
        }

        private void DrawHeaderControls()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search docs", GUILayout.Width(76f));
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(52f)))
                        _search = string.Empty;
                    if (UtilityWindowTheme.TintedButton("Add New Doc", UtilityWindowTheme.Cyan, GUILayout.Width(108f), GUILayout.Height(22f)))
                        AddNewLink();
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw(HelpUtilityId, "linked-docs-list", "linked-docs-list", "Open help for searching and adding documentation links.", "Documentation Links search toolbar");
                    PungentUtilityHelpButton.Draw(HelpUtilityId, "overview", "overview", "Open help for Documentation Links.", "Documentation Links header");
                }

                if (!string.IsNullOrWhiteSpace(_selectedUtilityId))
                {
                    PungentUtilityDescriptor utility = PungentUtilityRegistry.Find(_selectedUtilityId);
                    EditorGUILayout.LabelField("Utility context: " + (utility != null ? utility.DisplayName : _selectedUtilityId), UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawContextBar()
        {
            if (string.IsNullOrWhiteSpace(_selectedUtilityId))
                return;

            PungentUtilityDescriptor utility = PungentUtilityRegistry.Find(_selectedUtilityId);
            string utilityName = utility != null ? utility.DisplayName : _selectedUtilityId;

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.06f, 6, 3)))
            {
                EditorGUILayout.LabelField("Context: " + utilityName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                bool showAll = !_onlyThisUtility;
                if (GUILayout.Toggle(showAll, new GUIContent("Show All Docs", "Show global, utility, and unassigned documentation while keeping this utility as context."), EditorStyles.miniButtonLeft, GUILayout.Width(104f)) != showAll)
                {
                    _onlyThisUtility = false;
                    SavePrefs();
                }

                bool onlyThis = _onlyThisUtility;
                if (GUILayout.Toggle(onlyThis, new GUIContent("Only This Utility", "Show only documentation assigned to the current utility context."), EditorStyles.miniButtonMid, GUILayout.Width(114f)) != onlyThis)
                {
                    _onlyThisUtility = true;
                    SavePrefs();
                }

                if (GUILayout.Button(new GUIContent("Clear Context", "Clear utility context and show all documentation."), EditorStyles.miniButtonRight, GUILayout.Width(96f)))
                {
                    _selectedUtilityId = string.Empty;
                    _onlyThisUtility = false;
                    SavePrefs();
                }
            }
        }

        private void DrawLinksList()
        {
            List<PungentUtilityDocumentationLinks.DocumentationLink> links = FilteredLinks().ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral), GUILayout.Width(_listWidth), GUILayout.ExpandHeight(true)))
            {
                DrawDocumentationSectionTitle("Linked Docs", UtilityWindowTheme.Neutral, "linked-docs-list", "Open help for the linked documentation list, search, and add controls.", links.Count.ToString());
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, false, false, GUILayout.ExpandHeight(true));

                if (links.Count == 0)
                {
                    bool anyLinks = PungentUtilityDocumentationLinks.instance.GetAll().Any();
                    string message = !anyLinks
                        ? "No documentation links exist yet. Add New Doc to connect a guide, PDF, markdown file, Unity asset, local path, or web reference."
                        : _onlyThisUtility && !string.IsNullOrWhiteSpace(_selectedUtilityId)
                            ? "No documentation links are assigned to this utility yet. Create Link For Utility to connect a guide, PDF, markdown file, or web reference."
                            : "No documentation links match the current search or utility filter.";
                    EditorGUILayout.HelpBox(message, MessageType.Info);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (!string.IsNullOrWhiteSpace(_selectedUtilityId) &&
                            GUILayout.Button(new GUIContent("Create Link For Utility", "Create a documentation link already assigned to this utility."), EditorStyles.miniButton, GUILayout.Width(150f)))
                        {
                            CreateForUtility(_selectedUtilityId);
                        }

                        if (GUILayout.Button(new GUIContent("Add New Doc", "Create an unassigned documentation link."), EditorStyles.miniButton, GUILayout.Width(96f)))
                            AddNewLink();
                        GUILayout.FlexibleSpace();
                    }
                }

                foreach (PungentUtilityDocumentationLinks.DocumentationLink link in links)
                {
                    bool selected = string.Equals(link.id, _selectedLinkId, StringComparison.OrdinalIgnoreCase);
                    PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
                    Color tint = selected ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral;
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, selected ? 0.22f : 0.10f, 0.05f, 5, 2)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(new GUIContent(DisplayName(link), TargetSummary(link) + "\n\n" + status.message), EditorStyles.miniButtonLeft, GUILayout.Height(24f)))
                                SelectLink(link);
                            using (new EditorGUI.DisabledScope(!status.canOpen))
                            {
                                if (GUILayout.Button(new GUIContent("Open", status.canOpen ? "Open the current documentation target." : status.message), EditorStyles.miniButtonRight, GUILayout.Width(48f), GUILayout.Height(24f)))
                                    OpenOrWarn(link);
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            UtilityWindowTheme.CountPill(string.IsNullOrWhiteSpace(link.versionLabel) ? "Current" : link.versionLabel, UtilityWindowTheme.Teal, 86f);
                            UtilityWindowTheme.CountPill(status.kindLabel, TargetTint(status.kind), 94f);
                            UtilityWindowTheme.CountPill((link.utilityIds == null || link.utilityIds.Length == 0 ? "Global" : link.utilityIds.Length + " tools"), UtilityWindowTheme.Blue, 76f);
                            if (link.backlog != null && link.backlog.Count > 0)
                                UtilityWindowTheme.CountPill(link.backlog.Count + " old", UtilityWindowTheme.Amber, 62f);
                            GUILayout.FlexibleSpace();
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedLinkDetails()
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(_selectedLinkId);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawDocumentationSectionTitle("Details", UtilityWindowTheme.Teal, "selected-document-details", "Open help for selected documentation details.", link == null ? "Select a doc" : DisplayName(link));

                _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll, false, false, GUILayout.ExpandHeight(true));
                try
                {
                    if (link == null)
                    {
                        EditorGUILayout.HelpBox("Select a documentation link, or add a new one.", MessageType.Info);
                        return;
                    }

                    EditorGUI.BeginChangeCheck();
                    link.displayName = EditorGUILayout.TextField("Display Name", link.displayName);
                    link.description = EditorGUILayout.TextField("Description", link.description);
                    link.category = EditorGUILayout.TextField("Category", link.category);
                    link.versionLabel = EditorGUILayout.TextField("Version Label", string.IsNullOrWhiteSpace(link.versionLabel) ? "Current" : link.versionLabel);

                    if (EditorGUI.EndChangeCheck())
                        PungentUtilityDocumentationLinks.instance.UpdateLink(link);

                    DrawCurrentTarget(link);
                    DrawUtilityAssignments(link);
                    DrawVersionUpdate(link);
                    DrawReplaceCurrentTarget(link);
                    DrawBacklog(link);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
                        if (UtilityWindowTheme.TintedButton("Save", UtilityWindowTheme.Green, GUILayout.Width(72f), GUILayout.Height(24f)))
                            PungentUtilityDocumentationLinks.instance.UpdateLink(link);
                        using (new EditorGUI.DisabledScope(!status.canOpen))
                        {
                            if (GUILayout.Button(new GUIContent("Open Target", status.canOpen ? "Open the current documentation target." : status.message), GUILayout.Width(104f), GUILayout.Height(24f)))
                                OpenOrWarn(link);
                        }
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Remove", "Remove this documentation record."), GUILayout.Width(82f), GUILayout.Height(24f)) &&
                            EditorUtility.DisplayDialog("Remove Documentation Link", "Remove this documentation link?", "Remove", "Cancel"))
                        {
                            PungentUtilityDocumentationLinks.instance.RemoveLink(link.id);
                            _selectedLinkId = string.Empty;
                        }
                    }
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawCurrentTarget(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(link);
            bool hasCurrent = status.hasTarget;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(hasCurrent ? TargetTint(status.kind) : UtilityWindowTheme.Cyan, 0.12f, 0.06f)))
            {
                DrawDocumentationSectionTitle("Current Target", hasCurrent ? TargetTint(status.kind) : UtilityWindowTheme.Cyan, "current-target", "Open help for current documentation targets.", hasCurrent ? status.kindLabel : "Not set");

                if (hasCurrent)
                {
                    DrawTargetStatus(status);
                    EditorGUILayout.LabelField(TargetSummary(link), UtilityWindowTheme.PathLabelStyle);
                    DrawTargetActions(link.assetGuid, link.externalPath, "current documentation target");
                    DrawDocumentationNoteActions(link);
                    EditorGUILayout.LabelField("Use Update / Add New Version to preserve this target in Backlog.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                EditorGUILayout.HelpBox("This documentation record does not have a current target yet. Set one directly; no backlog entry will be created.", MessageType.Info);
                _setupVersionLabel = EditorGUILayout.TextField("Version Label", string.IsNullOrWhiteSpace(_setupVersionLabel) ? "Current" : _setupVersionLabel);
                _setupAsset = EditorGUILayout.ObjectField("Current Asset", _setupAsset, typeof(UnityEngine.Object), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _setupExternalPath = EditorGUILayout.TextField("External Path / Web URL", _setupExternalPath);
                    if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                    {
                        string path = EditorUtility.OpenFilePanel("Choose Documentation", string.Empty, string.Empty);
                        if (!string.IsNullOrWhiteSpace(path))
                            _setupExternalPath = path;
                    }
                }
                DrawExternalPathUrlHint();
                DrawCandidateTargetStatus(GuidFromAsset(_setupAsset), _setupExternalPath);

                using (new EditorGUI.DisabledScope(_setupAsset == null && string.IsNullOrWhiteSpace(_setupExternalPath)))
                {
                    if (UtilityWindowTheme.TintedButton("Set Current", UtilityWindowTheme.Green, GUILayout.Width(104f), GUILayout.Height(24f)))
                    {
                        PungentUtilityDocumentationLinks.instance.UpdateCurrentVersion(link.id, GuidFromAsset(_setupAsset), _setupExternalPath, _setupVersionLabel, string.Empty);
                        _setupAsset = null;
                        _setupExternalPath = string.Empty;
                        _setupVersionLabel = string.Empty;
                    }
                }
            }
        }

        private void DrawUtilityAssignments(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (!IsDeveloperModeEnabled())
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.10f, 0.05f)))
                {
                    DrawDocumentationSectionTitle("Utility Assignment", UtilityWindowTheme.Cyan, "utility-assignments", "Open help for global and utility-specific documentation link assignments.", AssignmentPill(link));
                    DrawAssignedUtilitySummary(link);
                }
                return;
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.06f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _assignmentFoldout = EditorGUILayout.Foldout(_assignmentFoldout, new GUIContent("Utility Assignment", "Developer-only utility assignment controls."), true);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(AssignmentPill(link), UtilityWindowTheme.Cyan, 86f);
                    PungentUtilityHelpButton.Draw(HelpUtilityId, "utility-assignments", "utility-assignments", "Open help for global and utility-specific documentation link assignments.", "Documentation Links utility assignment section");
                }
                if (!_assignmentFoldout)
                {
                    DrawAssignedUtilitySummary(link);
                    SavePrefs();
                    return;
                }

                _utilitySearch = EditorGUILayout.TextField("Filter Utilities", _utilitySearch, UtilityWindowTheme.ToolbarSearchStyle);

                foreach (PungentUtilityDescriptor utility in PungentUtilityRegistry.BrowserUtilities.Where(u => u.Matches(_utilitySearch)).OrderBy(u => u.DisplayName).Take(80))
                {
                    bool assigned = link.utilityIds != null && link.utilityIds.Any(id => string.Equals(id, utility.Id, StringComparison.OrdinalIgnoreCase));
                    bool nextAssigned = EditorGUILayout.ToggleLeft(new GUIContent(utility.DisplayName, utility.Id), assigned);
                    if (nextAssigned != assigned)
                    {
                        if (nextAssigned)
                            PungentUtilityDocumentationLinks.instance.AssignLinkToUtility(link.id, utility.Id);
                        else
                            PungentUtilityDocumentationLinks.instance.UnassignLinkFromUtility(link.id, utility.Id);
                    }
                }
            }

            SavePrefs();
        }

        private void DrawVersionUpdate(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (!HasCurrentTarget(link))
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.12f, 0.06f)))
            {
                DrawDocumentationSectionTitle("Update / Add New Version", UtilityWindowTheme.Amber, "current-version", "Open help for updating the current documentation version while preserving the previous target.");
                EditorGUILayout.HelpBox("Updating moves the previous current target into the backlog.", MessageType.Info);
                _updateVersionLabel = EditorGUILayout.TextField("New Version Label", _updateVersionLabel);
                _updateAsset = EditorGUILayout.ObjectField("New Asset", _updateAsset, typeof(UnityEngine.Object), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _updateExternalPath = EditorGUILayout.TextField("New External Path / Web URL", _updateExternalPath);
                    if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                    {
                        string path = EditorUtility.OpenFilePanel("Choose Documentation", string.Empty, string.Empty);
                        if (!string.IsNullOrWhiteSpace(path))
                            _updateExternalPath = path;
                    }
                }
                DrawExternalPathUrlHint();
                DrawCandidateTargetStatus(GuidFromAsset(_updateAsset), _updateExternalPath);
                _updateNotes = EditorGUILayout.TextField("Backlog Notes", _updateNotes);

                using (new EditorGUI.DisabledScope(_updateAsset == null && string.IsNullOrWhiteSpace(_updateExternalPath)))
                {
                    if (GUILayout.Button(new GUIContent("Update / Add New Version", "Make the new target current and archive the previous target in Backlog."), GUILayout.Height(24f)))
                    {
                        PungentUtilityDocumentationLinks.instance.UpdateCurrentVersion(link.id, GuidFromAsset(_updateAsset), _updateExternalPath, _updateVersionLabel, _updateNotes);
                        _updateAsset = null;
                        _updateExternalPath = string.Empty;
                        _updateVersionLabel = string.Empty;
                        _updateNotes = string.Empty;
                    }
                }
            }
        }

        private void DrawReplaceCurrentTarget(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (!HasCurrentTarget(link))
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Red, 0.14f, 0.07f)))
            {
                bool nextFoldout;
                using (new EditorGUILayout.HorizontalScope())
                {
                    nextFoldout = EditorGUILayout.Foldout(_replaceFoldout, new GUIContent("Replace Current Target", "Warning: replacing does not archive the current target in Backlog."), true);
                    GUILayout.FlexibleSpace();
                    PungentUtilityHelpButton.Draw(HelpUtilityId, "replace-current-target", "replace-current-target", "Open help for replacing the current documentation target.", "Documentation Links replace current target section");
                }
                if (nextFoldout != _replaceFoldout)
                {
                    _replaceFoldout = nextFoldout;
                    SavePrefs();
                }

                if (!_replaceFoldout)
                {
                    EditorGUILayout.LabelField("Collapsed by default. Prefer Update / Add New Version when the current target should be preserved.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                EditorGUILayout.HelpBox("Replace changes the current target without adding the previous version to Backlog.", MessageType.Warning);
                _replaceVersionLabel = EditorGUILayout.TextField("Replacement Version Label", _replaceVersionLabel);
                _replaceAsset = EditorGUILayout.ObjectField("Replacement Asset", _replaceAsset, typeof(UnityEngine.Object), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _replaceExternalPath = EditorGUILayout.TextField("Replacement External Path / Web URL", _replaceExternalPath);
                    if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                    {
                        string path = EditorUtility.OpenFilePanel("Choose Documentation", string.Empty, string.Empty);
                        if (!string.IsNullOrWhiteSpace(path))
                            _replaceExternalPath = path;
                    }
                }
                DrawExternalPathUrlHint();
                DrawCandidateTargetStatus(GuidFromAsset(_replaceAsset), _replaceExternalPath);

                bool nextSuppressReplaceWarning = EditorGUILayout.ToggleLeft(new GUIContent("Don't show replace warning again", "Persistently suppress the replace confirmation dialog."), _suppressReplaceWarning);
                if (nextSuppressReplaceWarning != _suppressReplaceWarning)
                {
                    _suppressReplaceWarning = nextSuppressReplaceWarning;
                    SavePrefs();
                }

                using (new EditorGUI.DisabledScope(_replaceAsset == null && string.IsNullOrWhiteSpace(_replaceExternalPath)))
                {
                    if (UtilityWindowTheme.TintedButton("Replace Current Target", UtilityWindowTheme.Red, GUILayout.Width(166f), GUILayout.Height(24f)))
                        TryReplaceCurrentTarget(link);
                }
            }
        }

        private void DrawBacklog(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _backlogFoldout = EditorGUILayout.Foldout(_backlogFoldout, new GUIContent("Backlog", "Previous documentation versions."), true);
                    GUILayout.FlexibleSpace();
                    int count = link.backlog == null ? 0 : link.backlog.Count;
                    UtilityWindowTheme.CountPill(count + " old", UtilityWindowTheme.Amber, 62f);
                    PungentUtilityHelpButton.Draw(HelpUtilityId, "backlog", "backlog", "Open help for documentation backlog versions.", "Documentation Links backlog section");
                }

                if (!_backlogFoldout || link.backlog == null)
                    return;

                if (link.backlog.Count == 0)
                {
                    EditorGUILayout.LabelField("No previous versions yet. Use Update / Add New Version to archive the current target before replacing it.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                for (int i = 0; i < link.backlog.Count; i++)
                {
                    PungentUtilityDocumentationLinks.DocumentationVersion version = link.backlog[i];
                    PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(version);
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 5, 2)))
                    {
                        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(version.versionLabel) ? "Archived" : version.versionLabel, EditorStyles.boldLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            UtilityWindowTheme.CountPill(status.kindLabel, TargetTint(status.kind), 94f);
                            EditorGUILayout.LabelField(BuildArchivedDate(version.archivedUtcTicks) + " - " + TargetSummary(version.assetGuid, version.externalPath), UtilityWindowTheme.MutedMiniLabelStyle);
                        }
                        DrawTargetStatus(status);
                        if (!string.IsNullOrWhiteSpace(version.notes))
                            EditorGUILayout.LabelField(version.notes, UtilityWindowTheme.MutedMiniLabelStyle);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(!status.canOpen))
                            {
                                if (GUILayout.Button(new GUIContent("Open", status.canOpen ? "Open this archived documentation target." : status.message), EditorStyles.miniButtonLeft, GUILayout.Width(54f)))
                                    PungentUtilityDocumentationLinks.instance.OpenBacklogVersion(link, i);
                            }
                            using (new EditorGUI.DisabledScope(!status.canCopy))
                            {
                                if (GUILayout.Button(new GUIContent("Copy", status.canCopy ? "Copy this archived documentation target." : status.message), EditorStyles.miniButtonMid, GUILayout.Width(54f)))
                                    CopyOrWarn(version.assetGuid, version.externalPath);
                            }
                            if (GUILayout.Button("Restore as Current", EditorStyles.miniButtonMid, GUILayout.Width(124f)))
                                PungentUtilityDocumentationLinks.instance.RestoreBacklogVersionAsCurrent(link.id, i);
                            if (GUILayout.Button("Remove", EditorStyles.miniButtonRight, GUILayout.Width(68f)))
                                PungentUtilityDocumentationLinks.instance.RemoveBacklogVersion(link.id, i);
                            GUILayout.FlexibleSpace();
                        }
                    }
                }
            }
        }

        private IEnumerable<PungentUtilityDocumentationLinks.DocumentationLink> FilteredLinks()
        {
            IEnumerable<PungentUtilityDocumentationLinks.DocumentationLink> links = PungentUtilityDocumentationLinks.instance.GetAll();
            if (_onlyThisUtility && !string.IsNullOrWhiteSpace(_selectedUtilityId))
                links = links.Where(link => link.utilityIds != null && link.utilityIds.Any(id => string.Equals(id, _selectedUtilityId, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(_search))
            {
                string q = _search.Trim();
                links = links.Where(link =>
                    Contains(link.displayName, q) ||
                    Contains(link.description, q) ||
                    Contains(link.category, q) ||
                    Contains(link.versionLabel, q) ||
                    (link.utilityIds != null && link.utilityIds.Any(id => Contains(id, q) || Contains(PungentUtilityRegistry.Find(id)?.DisplayName, q))));
            }

            return links.OrderBy(link => link.order).ThenBy(DisplayName);
        }

        private void AddNewLink()
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = new PungentUtilityDocumentationLinks.DocumentationLink
            {
                displayName = "Documentation",
                category = string.Empty,
                versionLabel = "Current",
                utilityIds = Array.Empty<string>()
            };
            PungentUtilityDocumentationLinks.instance.AddLink(link);
            SelectLink(link);
        }

        private void SelectLink(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            _selectedLinkId = link == null ? string.Empty : link.id;
        }

        private void DrawAssignedUtilitySummary(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            string[] utilityIds = link.utilityIds ?? Array.Empty<string>();
            string summary = utilityIds.Length == 0
                ? "Global documentation"
                : "Assigned to " + string.Join(", ", utilityIds.Select(id => PungentUtilityRegistry.Find(id)?.DisplayName ?? id).Take(4).ToArray());
            EditorGUILayout.LabelField(summary, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static string AssignmentPill(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            return link == null || link.utilityIds == null || link.utilityIds.Length == 0 ? "Global" : link.utilityIds.Length + " assigned";
        }

        private void TryReplaceCurrentTarget(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (!_suppressReplaceWarning)
            {
                int result = EditorUtility.DisplayDialogComplex(
                    "Replace Current Target",
                    "This replaces the current documentation target without adding the previous version to Backlog. Would you rather update instead?",
                    "Update Instead",
                    "Replace Anyway",
                    "Cancel");

                if (result == 0)
                {
                    _updateAsset = _replaceAsset;
                    _updateExternalPath = _replaceExternalPath;
                    _updateVersionLabel = _replaceVersionLabel;
                    _replaceAsset = null;
                    _replaceExternalPath = string.Empty;
                    _replaceVersionLabel = string.Empty;
                    return;
                }

                if (result != 1)
                    return;
            }

            PungentUtilityDocumentationLinks.instance.ReplaceCurrentTarget(link.id, GuidFromAsset(_replaceAsset), _replaceExternalPath, _replaceVersionLabel);
            _replaceAsset = null;
            _replaceExternalPath = string.Empty;
            _replaceVersionLabel = string.Empty;
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetFloat(PrefListWidth, _listWidth);
            UtilityWindowPrefs.SetBool(PrefOnlyThisUtility, _onlyThisUtility);
            UtilityWindowPrefs.SetBool(PrefAssignmentFoldout, _assignmentFoldout);
            UtilityWindowPrefs.SetBool(PrefReplaceFoldout, _replaceFoldout);
            UtilityWindowPrefs.SetBool(PrefSuppressReplaceWarning, _suppressReplaceWarning);
        }

        private static string BuildStatusText()
        {
            int total = PungentUtilityDocumentationLinks.instance.GetAll().Count();
            int assigned = PungentUtilityDocumentationLinks.instance.GetAll().Count(link => link.utilityIds != null && link.utilityIds.Length > 0);
            return total + " docs - " + assigned + " assigned";
        }

        private static string DisplayName(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            return PungentUtilityDocumentationLinks.GetDisplayName(link);
        }

        private static string TargetSummary(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            return link == null ? string.Empty : TargetSummary(link.assetGuid, link.externalPath);
        }

        private static bool HasCurrentTarget(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            return PungentUtilityDocumentationLinks.GetTargetStatus(link).hasTarget;
        }

        private static string TargetSummary(string assetGuid, string externalPath)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(assetGuid, externalPath);
            return string.IsNullOrWhiteSpace(status.targetValue) ? "No current target" : status.targetValue;
        }

        private static string BuildArchivedDate(long ticks)
        {
            if (ticks <= 0)
                return "Archived";

            return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("g");
        }

        private static UnityEngine.Object AssetFromGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }

        private static string GuidFromAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DrawCandidateTargetStatus(string assetGuid, string externalPath)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(assetGuid, externalPath);
            if (!status.hasTarget)
                return;

            DrawTargetStatus(status);
        }

        private static void DrawExternalPathUrlHint()
        {
            EditorGUILayout.LabelField("Type a local file path, or a web address like google.com. Bare domains are saved as https://...", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static void DrawTargetStatus(PungentUtilityDocumentationLinks.DocumentationTargetStatus status)
        {
            if (status == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(status.kindLabel, TargetTint(status.kind), 110f);
                EditorGUILayout.LabelField(status.message, UtilityWindowTheme.MutedMiniLabelStyle);
            }

            if (status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl &&
                !string.IsNullOrWhiteSpace(status.targetValue))
                EditorGUILayout.LabelField((status.normalizedFromInput ? "Normalized URL: " : "URL: ") + status.targetValue, UtilityWindowTheme.PathLabelStyle);

            if (status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.UnsupportedUrlScheme)
                EditorGUILayout.HelpBox(status.message, MessageType.Warning);
            else if (status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing ||
                     status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.InvalidTarget ||
                     status.kind == PungentUtilityDocumentationLinks.DocumentationTargetKind.Empty)
                EditorGUILayout.HelpBox(status.message, MessageType.Warning);
        }

        private static void DrawTargetActions(string assetGuid, string externalPath, string label)
        {
            PungentUtilityDocumentationLinks.DocumentationTargetStatus status = PungentUtilityDocumentationLinks.GetTargetStatus(assetGuid, externalPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!status.canOpen))
                {
                    if (GUILayout.Button(new GUIContent("Open Target", status.canOpen ? "Open the " + label + "." : status.message), GUILayout.Width(96f), GUILayout.Height(24f)))
                        OpenTargetOrWarn(assetGuid, externalPath);
                }

                using (new EditorGUI.DisabledScope(!status.canCopy))
                {
                    if (GUILayout.Button(new GUIContent("Copy Target", status.canCopy ? "Copy the " + label + "." : status.message), GUILayout.Width(96f), GUILayout.Height(24f)))
                        CopyOrWarn(assetGuid, externalPath);
                }

                using (new EditorGUI.DisabledScope(!status.canPingAsset))
                {
                    if (GUILayout.Button(new GUIContent("Ping Asset", status.canPingAsset ? "Ping the Unity asset target." : "Ping Asset is only available for valid Unity asset targets."), GUILayout.Width(86f), GUILayout.Height(24f)))
                        PingOrWarn(assetGuid);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawDocumentationNoteActions(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.id))
                return;

            bool hasCreate = PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink != null;
            bool hasAttach = PungentUtilityHelpNotesBridge.AttachDocumentationLinkToSelectedNote != null;
            bool hasOpen = PungentUtilityHelpNotesBridge.OpenNotesForDocumentationLink != null;
            int noteCount = PungentUtilityHelpNotesBridge.CountNotesForDocumentationLink != null
                ? PungentUtilityHelpNotesBridge.CountNotesForDocumentationLink(link.id)
                : 0;

            if (!hasCreate && !hasAttach && !hasOpen)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Documentation Notes", UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();
                PungentUtilityHelpButton.Draw(HelpUtilityId, "notes-integration", "notes-integration", "Open help for Documentation Links note actions.", "Documentation Links note actions section", link.id, DisplayName(link));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasCreate))
                {
                    if (GUILayout.Button(new GUIContent("Create Note", hasCreate ? "Create a Notes & Roadmap note for this documentation link." : "Notes integration is unavailable."), EditorStyles.miniButton, GUILayout.Width(90f)))
                        PungentUtilityHelpNotesBridge.CreateNoteFromDocumentationLink(link);
                }

                using (new EditorGUI.DisabledScope(!hasAttach))
                {
                    if (GUILayout.Button(new GUIContent("Attach to Note", hasAttach ? "Attach this documentation link to the selected/current Notes & Roadmap note." : "Notes integration is unavailable."), EditorStyles.miniButton, GUILayout.Width(104f)))
                        PungentUtilityHelpNotesBridge.AttachDocumentationLinkToSelectedNote(link.id);
                }

                using (new EditorGUI.DisabledScope(!hasOpen))
                {
                    string label = noteCount > 0 ? "Notes (" + noteCount + ")" : "Open Notes";
                    if (GUILayout.Button(new GUIContent(label, hasOpen ? "Open notes referencing this documentation link." : "Notes integration is unavailable."), EditorStyles.miniButton, GUILayout.Width(92f)))
                        PungentUtilityHelpNotesBridge.OpenNotesForDocumentationLink(link.id);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawDocumentationSectionTitle(string title, Color tint, string topicId, string tooltip, string pill = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrWhiteSpace(pill))
                    UtilityWindowTheme.CountPill(pill, tint);
                PungentUtilityHelpButton.Draw(HelpUtilityId, topicId, topicId, tooltip, "Documentation Links " + title + " section");
            }
        }

        private static Color TargetTint(PungentUtilityDocumentationLinks.DocumentationTargetKind kind)
        {
            switch (kind)
            {
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.UnityAsset:
                    return UtilityWindowTheme.Green;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.LocalFile:
                    return UtilityWindowTheme.Teal;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.WebUrl:
                    return UtilityWindowTheme.Cyan;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.UnsupportedUrlScheme:
                    return UtilityWindowTheme.Red;
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.Missing:
                case PungentUtilityDocumentationLinks.DocumentationTargetKind.InvalidTarget:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static void OpenOrWarn(PungentUtilityDocumentationLinks.DocumentationLink link)
        {
            if (!PungentUtilityDocumentationLinks.instance.Open(link, out string error))
                EditorUtility.DisplayDialog("Documentation Link", error, "OK");
        }

        private static void OpenTargetOrWarn(string assetGuid, string externalPath)
        {
            if (!PungentUtilityDocumentationLinks.TryOpenTarget(assetGuid, externalPath, out string error))
                EditorUtility.DisplayDialog("Documentation Link", error, "OK");
        }

        private static void CopyOrWarn(string assetGuid, string externalPath)
        {
            if (!PungentUtilityDocumentationLinks.TryCopyTarget(assetGuid, externalPath, out string error))
                EditorUtility.DisplayDialog("Documentation Link", error, "OK");
        }

        private static void PingOrWarn(string assetGuid)
        {
            if (!PungentUtilityDocumentationLinks.TryPingAsset(assetGuid, out string error))
                EditorUtility.DisplayDialog("Documentation Link", error, "OK");
        }

        private static bool IsDeveloperModeEnabled()
        {
            return false;
        }
    }
#endif
}
