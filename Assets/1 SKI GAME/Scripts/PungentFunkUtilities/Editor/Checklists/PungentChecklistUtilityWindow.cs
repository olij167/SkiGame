using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.Checklists
{
#if UNITY_EDITOR
    public sealed class PungentChecklistUtilityWindow : EditorWindow
    {
        public const string UtilityId = PungentChecklistConstants.UtilityId;
        public const string DataSheetChecklistId = PungentChecklistConstants.DataSheetChecklistId;
        public const string BoardGraphChecklistId = PungentChecklistConstants.BoardGraphChecklistId;

        private const string PrefPrefix = "PungentFunkUtilities.ChecklistUtility.";
        private const string PrefSelectedChecklist = PrefPrefix + "SelectedChecklist";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefFilter = PrefPrefix + "Filter";
        private const string PrefMode = PrefPrefix + "Mode";
        private const string PrefStateFilter = PrefPrefix + "StateFilter";
        private const string PrefSectionFilter = PrefPrefix + "SectionFilter";
        private const string PrefOwnerFilter = PrefPrefix + "OwnerFilter";
        private const string PrefPriorityFilter = PrefPrefix + "PriorityFilter";
        private const string PrefDueFilter = PrefPrefix + "DueFilter";
        private const string PrefShowArchived = PrefPrefix + "ShowArchived";
        private const string PrefImportedJson = PrefPrefix + "ImportedJson";
        private const string PrefLinkedStateGroups = PrefPrefix + "LinkedStateGroupsJson";
        private const double TextPersistenceDebounceSeconds = 0.35d;

        private static readonly string[] FilterLabels = { "All", "Open", "Pass", "Partial", "Fail" };
        private static readonly string[] ModeLabels = { "Run/List", "Edit" };
        private static GUIStyle _progressLabelStyle;

        private readonly List<PungentChecklistDefinition> _checklists = new List<PungentChecklistDefinition>();
        private readonly List<string> _legacyLocalJsonPresets = new List<string>();
        private readonly List<PungentLinkedStateGroupDefinition> _linkedStateGroups = new List<PungentLinkedStateGroupDefinition>();
        private readonly Dictionary<string, string> _notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pendingCommentSaves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _editListTextCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _projectChecklistIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _providerChecklistIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly RunViewCache _runViewCache = new RunViewCache();
        private Vector2 _scroll;
        private PungentChecklistDefinition _selectedChecklistCache;
        private string _selectedChecklistCacheId = string.Empty;
        private string _selectedChecklistId = DataSheetChecklistId;
        private string _search = string.Empty;
        private string _stateFilter = string.Empty;
        private string _sectionFilter = string.Empty;
        private string _ownerFilter = string.Empty;
        private string _priorityFilter = string.Empty;
        private string _dueFilter = string.Empty;
        private string _status = "Ready.";
        private FilterMode _filterMode = FilterMode.All;
        private WorkbenchMode _mode = WorkbenchMode.RunList;
        private bool _showArchived;
        private bool _runViewDirty = true;
        private bool _prefsSaveQueued;
        private bool _commentSaveQueued;
        private double _nextPrefsSaveAt;
        private double _nextCommentSaveAt;
        private readonly PungentChecklistDefinitionEditSession _editSession = new PungentChecklistDefinitionEditSession();
        private PungentChecklistValidationResult _lastValidation;

        public static void Open()
        {
            OpenChecklist(string.Empty);
        }

        public static void OpenDataSheetCurrentQa()
        {
            OpenChecklist(DataSheetChecklistId);
        }

        public static void OpenBoardGraphFeatureTest()
        {
            OpenChecklist(BoardGraphChecklistId);
        }

        public static void OpenChecklist(string checklistId)
        {
            OpenChecklist(checklistId, WorkbenchMode.RunList);
        }

        public static void OpenChecklistEdit(string checklistId)
        {
            OpenChecklist(checklistId, WorkbenchMode.Edit);
        }

        private static void OpenChecklist(string checklistId, WorkbenchMode mode)
        {
            PungentChecklistUtilityWindow window = GetWindow<PungentChecklistUtilityWindow>("Checklist Utility");
            window.minSize = new Vector2(720f, 460f);
            window.Show();
            window.Focus();
            window._mode = mode;
            window.EnsureDefinitionsLoaded(true);
            if (!string.IsNullOrWhiteSpace(checklistId))
                window.SelectChecklist(checklistId);
            window.SavePrefs();
            window.Repaint();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Checklist Utility");
            minSize = new Vector2(720f, 460f);
            _selectedChecklistId = UtilityWindowPrefs.GetString(PrefSelectedChecklist, DataSheetChecklistId);
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _filterMode = (FilterMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefFilter, 0), 0, FilterLabels.Length - 1);
            _mode = (WorkbenchMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefMode, 0), 0, ModeLabels.Length - 1);
            _stateFilter = UtilityWindowPrefs.GetString(PrefStateFilter, string.Empty);
            _sectionFilter = UtilityWindowPrefs.GetString(PrefSectionFilter, string.Empty);
            _ownerFilter = UtilityWindowPrefs.GetString(PrefOwnerFilter, string.Empty);
            _priorityFilter = UtilityWindowPrefs.GetString(PrefPriorityFilter, string.Empty);
            _dueFilter = UtilityWindowPrefs.GetString(PrefDueFilter, string.Empty);
            _showArchived = UtilityWindowPrefs.GetBool(PrefShowArchived, false);
            PungentChecklistDefinitionRegistry.Changed -= HandleRegistryChanged;
            PungentChecklistDefinitionRegistry.Changed += HandleRegistryChanged;
            EditorApplication.update -= HandleEditorUpdate;
            EditorApplication.update += HandleEditorUpdate;
            EnsureDefinitionsLoaded(true);
            LoadVisibleNotes();
        }

        private void OnDisable()
        {
            PungentChecklistDefinitionRegistry.Changed -= HandleRegistryChanged;
            EditorApplication.update -= HandleEditorUpdate;
            FlushPendingTextPersistence(true);
            SavePrefs();
        }

        private void OnLostFocus()
        {
            FlushPendingTextPersistence(true);
        }

        private void OnGUI()
        {
            EnsureDefinitionsLoaded();
            PungentChecklistDefinition checklist = SelectedChecklist;

            DrawFileToolbar(checklist);
            DrawHeaderCard(checklist);
            DrawContextActionToolbar(checklist);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (checklist == null)
            {
                DrawEmptyState();
            }
            else
            {
                if (_mode == WorkbenchMode.Edit)
                    DrawEditMode(checklist);
                else
                {
                    DrawChecklist(checklist);
                    DrawGuidance(checklist);
                }
            }
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private PungentChecklistDefinition SelectedChecklist
        {
            get
            {
                if (_selectedChecklistCache != null &&
                    string.Equals(_selectedChecklistCacheId, _selectedChecklistId, StringComparison.OrdinalIgnoreCase) &&
                    _checklists.Contains(_selectedChecklistCache))
                    return _selectedChecklistCache;

                PungentChecklistDefinition selected = _checklists.FirstOrDefault(item => item != null && string.Equals(item.checklistId, _selectedChecklistId, StringComparison.OrdinalIgnoreCase));
                _selectedChecklistCache = selected ?? _checklists.FirstOrDefault();
                _selectedChecklistCacheId = _selectedChecklistCache == null ? string.Empty : _selectedChecklistCache.checklistId;
                return _selectedChecklistCache;
            }
        }

        private void DrawFileToolbar(PungentChecklistDefinition checklist)
        {
            bool compact = position.width < 860f;
            bool projectOwned = IsProjectChecklist(checklist);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (_mode == WorkbenchMode.Edit)
                {
                    using (new EditorGUI.DisabledScope(checklist == null || !projectOwned))
                    {
                        if (GUILayout.Button(new GUIContent("Save", "Save the edited project checklist definition."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                            SaveEditDraft();
                        if (GUILayout.Button(new GUIContent("Revert", "Revert the edit draft to the stored checklist definition."), EditorStyles.toolbarButton, GUILayout.Width(58f)))
                            RevertEditDraft();
                    }

                    GUILayout.Space(4f);
                }

                GUILayout.Label("Current:", EditorStyles.miniLabel, GUILayout.Width(52f));
                DrawChecklistDropdown(checklist);

                if (!compact)
                    GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("New", "Create checklist definitions or authoring templates."), EditorStyles.toolbarDropDown, GUILayout.Width(54f)))
                    ShowCreateMenu();
                if (GUILayout.Button(new GUIContent("Import", "Import checklist JSON or copy JSON templates."), EditorStyles.toolbarDropDown, GUILayout.Width(64f)))
                    ShowImportMenu();

                using (new EditorGUI.DisabledScope(checklist == null))
                {
                    if (GUILayout.Button(new GUIContent("Export", "Export checklist definitions, bundles, results, or authoring surfaces."), EditorStyles.toolbarDropDown, GUILayout.Width(64f)))
                        ShowExportMenu(checklist);
                    if (GUILayout.Button(new GUIContent("Overlay", "Open this checklist in the sticky notes preview overlay."), EditorStyles.toolbarButton, GUILayout.Width(66f)))
                        OpenChecklistPreviewOverlay(checklist);
                }

                PungentUtilityHelpButton.Draw(UtilityId, "overview", "overview", "Open help for the Checklist Utility.");
            }
        }

        private void DrawContextActionToolbar(PungentChecklistDefinition checklist)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawModeToggle();
                GUILayout.Space(6f);

                using (new EditorGUI.DisabledScope(checklist == null))
                {
                    if (GUILayout.Button(new GUIContent("Open Related", "Open the utility or source editor related to this checklist."), EditorStyles.toolbarButton, GUILayout.Width(98f)))
                        OpenChecklistTarget(checklist);
                    if (GUILayout.Button(new GUIContent("Copy Results", "Copy checklist results for follow-up."), EditorStyles.toolbarButton, GUILayout.Width(92f)))
                        CopyResults(checklist);
                    if (GUILayout.Button(new GUIContent("Actions", "Checklist run/edit actions, bulk updates, reset, validation, and project management."), EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                        ShowContextActionsMenu(checklist);
                }

                GUILayout.FlexibleSpace();
                if (checklist != null)
                    EditorGUILayout.LabelField(_mode == WorkbenchMode.Edit ? "Editing definition" : "Running checklist", EditorStyles.miniLabel, GUILayout.Width(118f));
            }
        }

        private void DrawModeToggle()
        {
            bool edit = _mode == WorkbenchMode.Edit;
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = edit ? UtilityWindowTheme.Amber : UtilityWindowTheme.Blue;
            bool nextEdit = GUILayout.Toggle(edit, edit ? "Edit Mode" : "Run Mode", EditorStyles.toolbarButton, GUILayout.Width(120f));
            GUI.backgroundColor = previous;

            if (nextEdit == edit)
                return;

            FlushPendingTextPersistence(true);
            _mode = nextEdit ? WorkbenchMode.Edit : WorkbenchMode.RunList;
            SavePrefs();
            _editSession.Clear();
            _editListTextCache.Clear();
            MarkRunViewDirty();
        }

        private void DrawToolbar()
        {
            bool compact = position.width < 860f;
            DrawSelectionToolbar(compact);
            if (_mode == WorkbenchMode.Edit)
                DrawEditToolbar(compact);
            else
            {
                DrawActionToolbar(compact);
                DrawFilterToolbar(compact);
            }
        }

        private void DrawSelectionToolbar(bool compact)
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _mode = (WorkbenchMode)GUILayout.Toolbar((int)_mode, ModeLabels, EditorStyles.toolbarButton, GUILayout.Width(compact ? 150f : 170f));
                if (EditorGUI.EndChangeCheck())
                {
                    FlushPendingTextPersistence(true);
                    SavePrefs();
                    _editSession.Clear();
                    _editListTextCache.Clear();
                    MarkRunViewDirty();
                }

                DrawChecklistDropdown(checklist);
                if (!compact)
                    GUILayout.Space(4f);

                using (new EditorGUI.DisabledScope(checklist == null))
                {
                    if (GUILayout.Button(new GUIContent("Open Related", "Open the utility or source editor related to this checklist."), EditorStyles.toolbarButton, GUILayout.Width(98f)))
                        OpenChecklistTarget(checklist);
                }

                if (!compact)
                    GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Create", "Create or copy checklist authoring templates."), EditorStyles.toolbarDropDown, GUILayout.Width(64f)))
                    ShowCreateMenu();

                PungentUtilityHelpButton.Draw(UtilityId, "overview", "overview", "Open help for the Checklist Utility.");

                if (compact)
                    GUILayout.FlexibleSpace();
            }
        }

        private void DrawActionToolbar(bool compact)
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(checklist == null))
                {
                    if (GUILayout.Button(new GUIContent("Copy Results", "Copy pass/partial/fail results for follow-up."), EditorStyles.toolbarButton, GUILayout.Width(92f)))
                        CopyResults(checklist);

                    if (GUILayout.Button(new GUIContent("Export Results", "Export this checklist's result state and notes as JSON."), EditorStyles.toolbarButton, GUILayout.Width(102f)))
                        ExportResultsJson(checklist);

                    if (GUILayout.Button(new GUIContent("Reset", "Reset result states and notes for the selected checklist and child checklists it references."), EditorStyles.toolbarButton, GUILayout.Width(54f)))
                        ResetChecklist(checklist);

                    if (GUILayout.Button(new GUIContent("Actions", "Guidance, exports, archive, and checklist management actions."), EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                        ShowRunActionsMenu(checklist);
                }

                if (!compact)
                    GUILayout.FlexibleSpace();
            }
        }

        private void DrawEditToolbar(bool compact)
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            bool projectOwned = IsProjectChecklist(checklist);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(checklist == null || !projectOwned))
                {
                    if (GUILayout.Button(new GUIContent("Save", "Save the edited project checklist definition."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                        SaveEditDraft();
                    if (GUILayout.Button(new GUIContent("Revert", "Revert the edit draft to the stored checklist definition."), EditorStyles.toolbarButton, GUILayout.Width(58f)))
                        RevertEditDraft();
                }

                using (new EditorGUI.DisabledScope(checklist == null))
                {
                    if (GUILayout.Button(new GUIContent("Validate", "Validate this checklist definition."), EditorStyles.toolbarButton, GUILayout.Width(70f)))
                        ValidateSelectedChecklist();
                    if (GUILayout.Button(new GUIContent("Actions", "Export, clone, promote, archive, and delete checklist actions."), EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                        ShowEditActionsMenu(checklist);
                }

                GUILayout.FlexibleSpace();
                if (!compact && checklist != null)
                    EditorGUILayout.LabelField(projectOwned ? (_editSession.IsDirty ? "Unsaved project edits" : "Project checklist") : "Read-only checklist", EditorStyles.miniLabel, GUILayout.Width(150f));
            }
        }

        private void ShowRunActionsMenu(PungentChecklistDefinition checklist)
        {
            ShowContextActionsMenu(checklist);
        }

        private void ShowEditActionsMenu(PungentChecklistDefinition checklist)
        {
            ShowContextActionsMenu(checklist);
        }

        private void ShowContextActionsMenu(PungentChecklistDefinition checklist)
        {
            GenericMenu menu = new GenericMenu();
            if (checklist == null)
            {
                menu.AddDisabledItem(new GUIContent("No checklist selected"));
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("Copy/Copy Guidance"), false, () => CopyGuidance(checklist));
            menu.AddItem(new GUIContent("Validate Checklist"), false, ValidateSelectedChecklist);
            menu.AddSeparator("Run/");
            menu.AddItem(new GUIContent("Run/Reset States And Comments"), false, () => ResetChecklist(checklist));
            menu.AddItem(new GUIContent("Run/Clear Visible Comments"), false, BulkClearVisibleComments);
            menu.AddItem(new GUIContent("Run/Clear Visible State"), false, BulkClearVisibleState);

            foreach (PungentChecklistStateOptionDefinition state in RebuildRunViewCacheIfNeeded(checklist).StateOptions)
            {
                string stateId = state.stateId;
                menu.AddItem(new GUIContent("Run/Set Visible To " + state.label), false, () => BulkSetVisibleState(stateId));
            }

            menu.AddSeparator("Items/");
            if (IsProjectChecklist(checklist))
            {
                menu.AddItem(new GUIContent("Items/Archive Completed Items"), false, () => ArchiveCompletedItems(checklist, false));
                menu.AddItem(new GUIContent("Items/Archive Completed Visible Items"), false, () => ArchiveCompletedItems(checklist, true));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Items/Archive Completed Items"));
                menu.AddDisabledItem(new GUIContent("Items/Archive Completed Visible Items"));
            }

            menu.AddSeparator("Manage/");
            AddChecklistManagementMenuItems(menu, checklist);
            menu.ShowAsContext();
        }

        private void AddChecklistManagementMenuItems(GenericMenu menu, PungentChecklistDefinition checklist)
        {
            if (menu == null || checklist == null)
                return;

            if (IsProviderChecklist(checklist))
                menu.AddItem(new GUIContent("Manage/Clone to Project"), false, () => CloneSelectedChecklistToProject(checklist));
            else
                menu.AddDisabledItem(new GUIContent("Manage/Clone to Project"));

            if (IsLegacyLocalChecklist(checklist))
                menu.AddItem(new GUIContent("Manage/Promote Local"), false, () => PromoteLegacyLocalChecklist(checklist));
            else
                menu.AddDisabledItem(new GUIContent("Manage/Promote Local"));

            if (IsProjectChecklist(checklist))
            {
                menu.AddItem(new GUIContent(checklist.archived ? "Manage/Unarchive Checklist" : "Manage/Archive Checklist"), false, () => SetSelectedChecklistArchived(checklist, !checklist.archived));
                menu.AddItem(new GUIContent("Manage/Delete Project Checklist"), false, () => DeleteSelectedProjectChecklist(checklist));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Manage/Archive Checklist"));
                menu.AddDisabledItem(new GUIContent("Manage/Delete Project Checklist"));
            }
        }

        private void DrawFilterToolbar(bool compact)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Filter", EditorStyles.miniLabel, GUILayout.Width(38f));
                EditorGUI.BeginChangeCheck();
                _filterMode = (FilterMode)GUILayout.Toolbar((int)_filterMode, FilterLabels, EditorStyles.toolbarButton, GUILayout.Width(compact ? 230f : 250f));
                if (EditorGUI.EndChangeCheck())
                {
                    SavePrefs();
                    MarkRunViewDirty();
                }

                if (!compact)
                    GUILayout.FlexibleSpace();

                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(44f));
                EditorGUI.BeginChangeCheck();
                _search = GUILayout.TextField(_search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(110f), GUILayout.MaxWidth(compact ? 180f : 240f));
                if (EditorGUI.EndChangeCheck())
                {
                    QueuePrefsSave();
                    MarkRunViewDirty();
                }

                if (GUILayout.Button(new GUIContent("More", "State, section, owner, priority, due date, archived, and bulk filters."), EditorStyles.toolbarDropDown, GUILayout.Width(58f)))
                    ShowFilterMenu();
            }
        }

        private void ShowFilterMenu()
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show Archived"), _showArchived, () =>
            {
                _showArchived = !_showArchived;
                SavePrefs();
                MarkRunViewDirty();
                Repaint();
            });
            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("View/All"), _filterMode == FilterMode.All && string.IsNullOrWhiteSpace(_stateFilter), () => SetFilterMode(FilterMode.All));
            menu.AddItem(new GUIContent("View/Open"), _filterMode == FilterMode.Open, () => SetFilterMode(FilterMode.Open));
            menu.AddItem(new GUIContent("View/Complete"), _filterMode == FilterMode.Pass, () => SetFilterMode(FilterMode.Pass));
            menu.AddItem(new GUIContent("View/Partial Or In Progress"), _filterMode == FilterMode.Partial, () => SetFilterMode(FilterMode.Partial));
            menu.AddItem(new GUIContent("View/Needs Attention"), _filterMode == FilterMode.Fail, () => SetFilterMode(FilterMode.Fail));
            menu.AddSeparator("State/");
            menu.AddItem(new GUIContent("State/All"), string.IsNullOrWhiteSpace(_stateFilter), () => SetStateFilter(string.Empty));
            if (checklist != null)
            {
                foreach (PungentChecklistStateOptionDefinition state in PungentChecklistProfiles.ResolveStateOptions(checklist))
                {
                    string stateId = state.stateId;
                    menu.AddItem(new GUIContent("State/" + state.label), string.Equals(_stateFilter, stateId, StringComparison.OrdinalIgnoreCase), () => SetStateFilter(stateId));
                }
            }

            menu.AddSeparator("Section/");
            menu.AddItem(new GUIContent("Section/All"), string.IsNullOrWhiteSpace(_sectionFilter), () => SetSectionFilter(string.Empty));
            foreach (PungentChecklistSectionDefinition section in checklist == null ? new List<PungentChecklistSectionDefinition>() : checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null)
                    continue;
                string id = section.id;
                menu.AddItem(new GUIContent("Section/" + section.title), string.Equals(_sectionFilter, id, StringComparison.OrdinalIgnoreCase), () => SetSectionFilter(id));
            }

            foreach (string owner in DistinctItemMetadata(checklist, item => item.owner))
                menu.AddItem(new GUIContent("Owner/" + owner), string.Equals(_ownerFilter, owner, StringComparison.OrdinalIgnoreCase), () => SetOwnerFilter(owner));
            foreach (string priority in DistinctItemMetadata(checklist, item => item.priority))
                menu.AddItem(new GUIContent("Priority/" + priority), string.Equals(_priorityFilter, priority, StringComparison.OrdinalIgnoreCase), () => SetPriorityFilter(priority));

            menu.AddSeparator("Clear/");
            menu.AddItem(new GUIContent("Clear/State And View Filters"), false, ClearStateAndModeFilters);
            menu.AddItem(new GUIContent("Clear/Section Filter"), false, () => SetSectionFilter(string.Empty));
            menu.AddItem(new GUIContent("Clear/Owner Filter"), false, () => SetOwnerFilter(string.Empty));
            menu.AddItem(new GUIContent("Clear/Priority Filter"), false, () => SetPriorityFilter(string.Empty));
            menu.AddItem(new GUIContent("Clear/Due Filter"), false, () => SetDueFilter(string.Empty));
            menu.ShowAsContext();
        }

        private void SetStateFilter(string stateId)
        {
            _stateFilter = stateId ?? string.Empty;
            _filterMode = FilterMode.All;
            SavePrefs();
            MarkRunViewDirty();
            Repaint();
        }

        private void SetFilterMode(FilterMode mode)
        {
            _filterMode = mode;
            if (mode != FilterMode.All)
                _stateFilter = string.Empty;
            SavePrefs();
            MarkRunViewDirty();
            Repaint();
        }

        private void ClearStateAndModeFilters()
        {
            _stateFilter = string.Empty;
            _filterMode = FilterMode.All;
            SavePrefs();
            MarkRunViewDirty();
            Repaint();
        }

        private void SetSectionFilter(string value)
        {
            _sectionFilter = value ?? string.Empty;
            SavePrefs();
            MarkRunViewDirty();
            Repaint();
        }

        private void SetOwnerFilter(string value)
        {
            _ownerFilter = value ?? string.Empty;
            SavePrefs();
            MarkRunViewDirty();
            Repaint();
        }

        private void SetPriorityFilter(string value)
        {
            _priorityFilter = value ?? string.Empty;
            SavePrefs();
            MarkRunViewDirty();
            Repaint();
        }

        private void SetDueFilter(string value)
        {
            _dueFilter = value ?? string.Empty;
            SavePrefs();
            MarkRunViewDirty();
            Repaint();
        }

        private static IEnumerable<string> DistinctItemMetadata(PungentChecklistDefinition checklist, Func<PungentChecklistItemDefinition, string> selector)
        {
            if (checklist == null || selector == null)
                yield break;

            foreach (string value in PungentChecklistSerialization.EnumerateItems(checklist)
                         .Select(selector)
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => value.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                yield return value;
        }

        private void BulkSetVisibleState(string stateId)
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            if (checklist == null)
                return;

            List<PungentChecklistItemDefinition> visible = VisibleRunItems(checklist).Where(item => !IsComputedFromChild(item)).ToList();
            PungentChecklistStateOptionDefinition state = PungentChecklistProfiles.ResolveState(checklist, stateId);
            if (visible.Count == 0)
            {
                _status = "No visible editable checklist items.";
                return;
            }

            if (!EditorUtility.DisplayDialog("Bulk Checklist State", "Set " + visible.Count + " visible item(s) to '" + state.label + "'?", "Set State", "Cancel"))
                return;

            foreach (PungentChecklistItemDefinition item in visible)
                SetStateId(checklist, item, state.stateId);
            _status = "Updated " + visible.Count + " visible item(s).";
        }

        private void BulkClearVisibleComments()
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            if (checklist == null)
                return;

            List<PungentChecklistItemDefinition> visible = VisibleRunItems(checklist).ToList();
            if (visible.Count == 0)
            {
                _status = "No visible checklist items.";
                return;
            }

            if (!EditorUtility.DisplayDialog("Clear Checklist Comments", "Clear comments for " + visible.Count + " visible item(s)?", "Clear Comments", "Cancel"))
                return;

            foreach (PungentChecklistItemDefinition item in visible)
                SetNote(checklist, item, string.Empty);
            _status = "Cleared visible comments.";
        }

        private void BulkClearVisibleState()
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            if (checklist == null)
                return;

            List<PungentChecklistItemDefinition> visible = VisibleRunItems(checklist).Where(item => !IsComputedFromChild(item)).ToList();
            if (visible.Count == 0)
            {
                _status = "No visible editable checklist items.";
                return;
            }

            if (!EditorUtility.DisplayDialog("Clear Checklist State", "Clear state for " + visible.Count + " visible item(s)?", "Clear State", "Cancel"))
                return;

            foreach (PungentChecklistItemDefinition item in visible)
                SetStateId(checklist, item, PungentChecklistProfiles.DefaultStateId(checklist));
            _status = "Cleared visible item states.";
        }

        private IEnumerable<PungentChecklistItemDefinition> VisibleRunItems(PungentChecklistDefinition checklist)
        {
            if (checklist != null && string.Equals(checklist.checklistId, _selectedChecklistId, StringComparison.OrdinalIgnoreCase))
            {
                RunViewCache cache = RebuildRunViewCacheIfNeeded(checklist);
                for (int i = 0; i < cache.sections.Count; i++)
                    for (int j = 0; j < cache.sections[i].items.Count; j++)
                        yield return cache.sections[i].items[j];
                yield break;
            }

            foreach (PungentChecklistSectionDefinition section in checklist == null ? new List<PungentChecklistSectionDefinition>() : checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null || (section.archived && !_showArchived))
                    continue;

                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                    if (ShouldShow(checklist, item))
                        yield return item;
            }
        }

        private void DrawChecklistDropdown(PungentChecklistDefinition selected)
        {
            string label = selected == null ? "No Checklist" : selected.archived ? selected.title + " (Archived)" : selected.title;
            if (GUILayout.Button(new GUIContent(label, selected == null ? string.Empty : selected.description), EditorStyles.toolbarDropDown, GUILayout.MinWidth(180f), GUILayout.MaxWidth(340f)))
            {
                GenericMenu menu = new GenericMenu();
                bool hasArchived = false;
                foreach (PungentChecklistDefinition checklist in _checklists.Where(item => item != null && !item.archived))
                {
                    if (string.IsNullOrWhiteSpace(checklist.checklistId))
                        continue;

                    string id = checklist.checklistId;
                    string sourcePrefix = IsLegacyLocalChecklist(checklist) ? "Legacy Local/" : _projectChecklistIds.Contains(id) ? "Project/" : "Package/";
                    menu.AddItem(new GUIContent(sourcePrefix + checklist.title), string.Equals(id, _selectedChecklistId, StringComparison.OrdinalIgnoreCase), () => SelectChecklist(id));
                }

                menu.AddSeparator(string.Empty);
                foreach (PungentChecklistDefinition checklist in _checklists.Where(item => item != null && item.archived))
                {
                    if (string.IsNullOrWhiteSpace(checklist.checklistId))
                        continue;

                    hasArchived = true;
                    string id = checklist.checklistId;
                    string sourcePrefix = IsLegacyLocalChecklist(checklist) ? "Archived/Legacy Local/" : _projectChecklistIds.Contains(id) ? "Archived/Project/" : "Archived/Package/";
                    menu.AddItem(new GUIContent(sourcePrefix + checklist.title), string.Equals(id, _selectedChecklistId, StringComparison.OrdinalIgnoreCase), () => SelectChecklist(id));
                }

                if (!hasArchived)
                    menu.AddDisabledItem(new GUIContent("Archived/No archived checklists"));
                menu.ShowAsContext();
            }
        }

        private void DrawHeaderCard(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            RunViewCache cache = RebuildRunViewCacheIfNeeded(checklist);
            PungentChecklistProgressSummary summary = cache.summary;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(checklist.title, EditorStyles.boldLabel);
                if (!string.IsNullOrWhiteSpace(checklist.description))
                    EditorGUILayout.LabelField(checklist.description, EditorStyles.wordWrappedMiniLabel);
                else if (!string.IsNullOrWhiteSpace(checklist.defaultGuidance))
                    EditorGUILayout.LabelField(checklist.defaultGuidance, EditorStyles.wordWrappedMiniLabel);

                List<string> metadata = new List<string>();
                if (IsLegacyLocalChecklist(checklist))
                    metadata.Add("Legacy local definition");
                else if (IsProjectChecklist(checklist))
                    metadata.Add("Project definition");
                else
                    metadata.Add("Package definition");
                if (!string.IsNullOrWhiteSpace(checklist.targetUtilityId))
                    metadata.Add("Target: " + checklist.targetUtilityId);
                if (!string.IsNullOrWhiteSpace(checklist.sourceLabel))
                    metadata.Add("Source: " + checklist.sourceLabel);
                metadata.Add("Kind: " + PungentChecklistListKinds.GetDisplayName(checklist.listKind));
                if (cache.snapshot != null && !string.IsNullOrWhiteSpace(cache.snapshot.ProfileLabel))
                    metadata.Add("Profile: " + cache.snapshot.ProfileLabel);
                if (checklist.archived)
                    metadata.Add("Archived");
                EditorGUILayout.LabelField(string.Join(" | ", metadata.ToArray()), EditorStyles.wordWrappedMiniLabel);

                Rect progressRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
                if (cache.snapshot != null && cache.snapshot.IsQualityGate)
                {
                    int pass = summary.Count(PungentChecklistProfiles.StatePass);
                    int partial = summary.Count(PungentChecklistProfiles.StatePartial);
                    int fail = summary.Count(PungentChecklistProfiles.StateFail);
                    int untested = summary.Count(PungentChecklistProfiles.StateUntested);
                    DrawSegmentedProgressBar(progressRect, pass, partial, fail, untested, summary.total);
                }
                else
                {
                    DrawProfileProgressBar(progressRect, summary, cache.snapshot);
                }

                DrawStatusFilterChips(cache);
                DrawHeaderSearchRow();
            }
        }

        private void DrawStatusFilterChips(RunViewCache cache)
        {
            if (cache == null || cache.summary == null)
                return;

            List<FilterToggleSpec> chips = new List<FilterToggleSpec>();
            bool allActive = string.IsNullOrWhiteSpace(_stateFilter) && _filterMode == FilterMode.All;
            bool openActive = string.IsNullOrWhiteSpace(_stateFilter) && _filterMode == FilterMode.Open;
            AddFilterToggleChip(chips, "All: " + cache.summary.total, "Show all checklist items that match archived visibility.", UtilityWindowTheme.Blue, allActive, allActive, ClearStateAndModeFilters);
            AddFilterToggleChip(chips, "Open: " + Mathf.Max(0, cache.summary.total - cache.summary.complete), "Show checklist items that are not complete.", UtilityWindowTheme.Blue, openActive, allActive || openActive, () => SetFilterMode(FilterMode.Open));

            if (cache.snapshot != null && cache.snapshot.IsQualityGate)
            {
                AddStateFilterToggleChip(chips, cache, "Pass", PungentChecklistProfiles.StatePass, UtilityWindowTheme.Green, allActive, _filterMode == FilterMode.Pass);
                AddStateFilterToggleChip(chips, cache, "Partial", PungentChecklistProfiles.StatePartial, UtilityWindowTheme.Amber, allActive, _filterMode == FilterMode.Partial);
                AddStateFilterToggleChip(chips, cache, "Fail", PungentChecklistProfiles.StateFail, UtilityWindowTheme.Red, allActive, _filterMode == FilterMode.Fail);
            }
            else
            {
                foreach (PungentChecklistStateOptionDefinition state in cache.StateOptions)
                {
                    if (state == null || string.IsNullOrWhiteSpace(state.stateId))
                        continue;

                    string stateId = state.stateId;
                    bool active = string.Equals(_stateFilter, stateId, StringComparison.OrdinalIgnoreCase);
                    AddFilterToggleChip(
                        chips,
                        state.shortLabel + ": " + cache.summary.Count(state.stateId),
                        "Filter to " + state.label + " checklist items.",
                        StateColor(state),
                        active,
                        allActive || active,
                        () => SetStateFilter(stateId));
                }
            }

            DrawWrappedFilterToggleChips(chips, Mathf.Max(220f, position.width - 36f));
        }

        private void AddStateFilterToggleChip(List<FilterToggleSpec> chips, RunViewCache cache, string label, string stateId, Color tint, bool allActive, bool compatibleFilterActive)
        {
            bool active = string.Equals(_stateFilter, stateId, StringComparison.OrdinalIgnoreCase) ||
                          (string.IsNullOrWhiteSpace(_stateFilter) && compatibleFilterActive);
            AddFilterToggleChip(
                chips,
                label + ": " + cache.summary.Count(stateId),
                "Filter to " + label.ToLowerInvariant() + " checklist items.",
                tint,
                active,
                allActive || active,
                () => SetStateFilter(stateId));
        }

        private static void AddFilterToggleChip(List<FilterToggleSpec> chips, string label, string tooltip, Color tint, bool isChecked, bool fullColor, Action action)
        {
            if (chips == null)
                return;

            string text = label ?? string.Empty;
            chips.Add(new FilterToggleSpec(
                new GUIContent(text, tooltip ?? string.Empty),
                tint,
                isChecked,
                fullColor,
                action,
                Mathf.Clamp(text.Length * 6f + 38f, 82f, 164f)));
        }

        private void DrawWrappedFilterToggleChips(IReadOnlyList<FilterToggleSpec> chips, float rowWidth)
        {
            if (chips == null || chips.Count == 0)
                return;

            float used = 0f;
            bool rowOpen = false;
            float available = Mathf.Max(180f, rowWidth);

            try
            {
                for (int i = 0; i < chips.Count; i++)
                {
                    FilterToggleSpec chip = chips[i];
                    if (chip == null)
                        continue;

                    float width = chip.width;
                    if (!rowOpen)
                    {
                        EditorGUILayout.BeginHorizontal();
                        rowOpen = true;
                        used = 0f;
                    }

                    if (used > 0f && used + width > available)
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        used = 0f;
                    }

                    if (FilterToggleChip(chip.content, chip.isChecked, chip.fullColor, chip.tint, width))
                        chip.action?.Invoke();
                    used += width + 4f;
                }
            }
            finally
            {
                if (rowOpen)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private bool FilterToggleChip(GUIContent content, bool isChecked, bool fullColor, Color tint, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 20f, GUILayout.Width(width), GUILayout.Height(20f));
            Event current = Event.current;
            bool hover = rect.Contains(current.mousePosition);
            bool enabled = GUI.enabled;
            Color chipTint = fullColor ? FilterFullTint(tint) : FilterDimTint(tint);
            Color borderTint = fullColor ? chipTint : Color.Lerp(FilterDimTint(tint), FilterFullTint(tint), hover ? 0.58f : 0.36f);

            if (current.type == EventType.Repaint)
            {
                Color border = enabled
                    ? new Color(borderTint.r, borderTint.g, borderTint.b, fullColor ? 0.92f : hover ? 0.84f : 0.62f)
                    : new Color(0.45f, 0.45f, 0.45f, 0.18f);

                using (UtilityWindowTheme.Background(chipTint))
                    UtilityWindowTheme.CountPillStyle.Draw(rect, GUIContent.none, hover, false, isChecked, false);
                DrawFilterBoxBorder(rect, border);

                Rect checkRect = new Rect(rect.x + 6f, rect.y + 5f, 10f, 10f);
                Color checkFill = isChecked
                    ? new Color(chipTint.r, chipTint.g, chipTint.b, 0.84f)
                    : (EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, hover ? 0.18f : 0.13f) : new Color(0f, 0f, 0f, hover ? 0.14f : 0.09f));
                Color checkBorder = isChecked
                    ? new Color(chipTint.r, chipTint.g, chipTint.b, 0.96f)
                    : new Color(border.r, border.g, border.b, Mathf.Max(border.a, 0.78f));
                DrawFilterBox(checkRect, checkFill, checkBorder);
                if (isChecked)
                {
                    Color mark = ReadableOn(chipTint);
                    mark.a = 0.94f;
                    EditorGUI.DrawRect(new Rect(checkRect.x + 3f, checkRect.y + 3f, 4f, 4f), mark);
                }

                Color textColor = enabled
                    ? ReadableOn(chipTint)
                    : (EditorGUIUtility.isProSkin ? new Color(0.54f, 0.54f, 0.56f) : new Color(0.48f, 0.48f, 0.50f));
                Rect labelRect = new Rect(rect.x + 21f, rect.y, rect.width - 25f, rect.height);
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 2, 0, 1)
                };
                labelStyle.normal.textColor = textColor;
                labelStyle.hover.textColor = textColor;
                labelStyle.active.textColor = textColor;
                labelStyle.focused.textColor = textColor;
                labelStyle.onNormal.textColor = textColor;
                labelStyle.onHover.textColor = textColor;
                labelStyle.onActive.textColor = textColor;
                labelStyle.onFocused.textColor = textColor;
                GUI.Label(labelRect, content ?? GUIContent.none, labelStyle);
            }

            if (enabled)
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
                {
                    GUI.FocusControl(null);
                    current.Use();
                    Repaint();
                    return true;
                }
            }

            return false;
        }

        private void DrawHeaderSearchRow()
        {
            bool compact = position.width < 720f;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(44f));
                EditorGUI.BeginChangeCheck();
                _search = GUILayout.TextField(_search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(120f), GUILayout.MaxWidth(compact ? 220f : 340f));
                if (EditorGUI.EndChangeCheck())
                {
                    QueuePrefsSave();
                    MarkRunViewDirty();
                }

                if (GUILayout.Button(new GUIContent("More", "State, section, owner, priority, due date, archived, and secondary filters."), EditorStyles.miniButton, GUILayout.Width(58f)))
                    ShowFilterMenu();

                GUILayout.FlexibleSpace();
                if (!string.IsNullOrWhiteSpace(_stateFilter) || _filterMode != FilterMode.All || !string.IsNullOrWhiteSpace(_sectionFilter) || !string.IsNullOrWhiteSpace(_ownerFilter) || !string.IsNullOrWhiteSpace(_priorityFilter) || !string.IsNullOrWhiteSpace(_dueFilter))
                    GUILayout.Label("Filtered", EditorStyles.miniLabel, GUILayout.Width(52f));
            }
        }

        private static void DrawProfileProgressBar(Rect rect, PungentChecklistProgressSummary summary, PungentChecklistUtilityStateService.StateSnapshot snapshot)
        {
            string label = summary.complete + "/" + summary.total + " complete | " + summary.problem + " needs attention | " + summary.unstarted + " open";
            Rect trackRect = new Rect(rect.x, rect.y + 2f, rect.width, Mathf.Max(1f, rect.height - 4f));
            Color track = EditorGUIUtility.isProSkin ? new Color(0.11f, 0.12f, 0.14f, 1f) : new Color(0.78f, 0.80f, 0.84f, 1f);
            Color outline = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.20f);

            EditorGUI.DrawRect(trackRect, track);
            float totalWidth = trackRect.width;
            foreach (PungentChecklistStateOptionDefinition state in snapshot == null ? Array.Empty<PungentChecklistStateOptionDefinition>() : snapshot.StateOptions)
            {
                if (summary.Count(state.stateId) <= 0 || !snapshot.CountsAsStarted(state.stateId))
                    continue;
                DrawProgressSegment(ref trackRect, summary.Count(state.stateId), summary.total, totalWidth, StateColor(state));
            }

            DrawProgressOutline(rect, outline);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), label, ProgressLabelStyle(true));
            GUI.Label(rect, label, ProgressLabelStyle(false));
        }

        private static void DrawSegmentedProgressBar(Rect rect, int pass, int partial, int fail, int untested, int total)
        {
            string label = pass + "/" + total + " passed | " + partial + " partial | " + fail + " failed | " + untested + " untested";
            Rect trackRect = new Rect(rect.x, rect.y + 2f, rect.width, Mathf.Max(1f, rect.height - 4f));
            Color track = EditorGUIUtility.isProSkin ? new Color(0.11f, 0.12f, 0.14f, 1f) : new Color(0.78f, 0.80f, 0.84f, 1f);
            Color outline = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.20f);

            EditorGUI.DrawRect(trackRect, track);
            float totalWidth = trackRect.width;
            DrawProgressSegment(ref trackRect, pass, total, totalWidth, UtilityWindowTheme.Green);
            DrawProgressSegment(ref trackRect, partial, total, totalWidth, UtilityWindowTheme.Amber);
            DrawProgressSegment(ref trackRect, fail, total, totalWidth, UtilityWindowTheme.Red);

            DrawProgressOutline(rect, outline);

            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), label, ProgressLabelStyle(true));
            GUI.Label(rect, label, ProgressLabelStyle(false));
        }

        private static void DrawProgressOutline(Rect rect, Color outline)
        {
            Rect top = new Rect(rect.x, rect.y + 2f, rect.width, 1f);
            Rect bottom = new Rect(rect.x, rect.yMax - 3f, rect.width, 1f);
            Rect left = new Rect(rect.x, rect.y + 2f, 1f, Mathf.Max(1f, rect.height - 4f));
            Rect right = new Rect(rect.xMax - 1f, rect.y + 2f, 1f, Mathf.Max(1f, rect.height - 4f));
            EditorGUI.DrawRect(top, outline);
            EditorGUI.DrawRect(bottom, outline);
            EditorGUI.DrawRect(left, outline);
            EditorGUI.DrawRect(right, outline);
        }

        private static void DrawProgressSegment(ref Rect remainingRect, int count, int total, float totalWidth, Color color)
        {
            if (count <= 0 || total <= 0 || remainingRect.width <= 0f)
                return;

            float width = Mathf.Min(remainingRect.width, Mathf.Round(totalWidth * (count / (float)total)));
            if (width <= 0f)
                return;

            Rect segmentRect = new Rect(remainingRect.x, remainingRect.y, width, remainingRect.height);
            color.a = 0.92f;
            EditorGUI.DrawRect(segmentRect, color);
            remainingRect.x += width;
            remainingRect.width -= width;
        }

        private static GUIStyle ProgressLabelStyle(bool shadow)
        {
            if (_progressLabelStyle == null)
            {
                _progressLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fontStyle = FontStyle.Bold
                };
            }

            _progressLabelStyle.normal.textColor = shadow
                ? (EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.65f) : new Color(1f, 1f, 1f, 0.65f))
                : (EditorGUIUtility.isProSkin ? new Color(0.92f, 0.94f, 0.98f, 1f) : new Color(0.08f, 0.09f, 0.10f, 1f));
            return _progressLabelStyle;
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.HelpBox("No checklist definitions are available yet. Import JSON, copy a definition template, or create a checklist authoring sheet/document.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy JSON Template", GUILayout.Height(26f)))
                    CopyDefinitionTemplate();
                if (GUILayout.Button("Create Data Sheet Template", GUILayout.Height(26f)))
                    CreateChecklistDataSheet();
                if (GUILayout.Button("Create Rich Document Template", GUILayout.Height(26f)))
                    CreateChecklistRichDocument();
            }
        }

        private void DrawChecklist(PungentChecklistDefinition checklist)
        {
            RunViewCache cache = RebuildRunViewCacheIfNeeded(checklist);
            for (int i = 0; i < cache.sections.Count; i++)
            {
                RunSectionView sectionView = cache.sections[i];
                EditorGUILayout.Space(4f);
                UtilityWindowTheme.SectionTitle(sectionView.section.title, UtilityWindowTheme.Teal, sectionView.pill);
                for (int j = 0; j < sectionView.items.Count; j++)
                    DrawItem(checklist, sectionView.items[j], cache);
            }
        }

        private void DrawEditMode(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            if (IsProviderChecklist(checklist))
            {
                EditorGUILayout.HelpBox("This checklist is package-provided. Clone it to the project before editing.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Clone to Project", GUILayout.Width(120f)))
                        CloneSelectedChecklistToProject(checklist);
                    if (GUILayout.Button("Export JSON", GUILayout.Width(96f)))
                        ExportDefinitionJson(checklist);
                    if (GUILayout.Button("Open Source Provider", GUILayout.Width(140f)))
                        OpenChecklistTarget(checklist);
                    GUILayout.FlexibleSpace();
                }
                DrawReadOnlyChecklistPreview(checklist);
                return;
            }

            if (IsLegacyLocalChecklist(checklist))
            {
                EditorGUILayout.HelpBox("This is a legacy local checklist. Promote it to project storage before editing.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Promote To Project", GUILayout.Width(130f)))
                        PromoteLegacyLocalChecklist(checklist);
                    if (GUILayout.Button("Export JSON", GUILayout.Width(96f)))
                        ExportDefinitionJson(checklist);
                    GUILayout.FlexibleSpace();
                }
                DrawReadOnlyChecklistPreview(checklist);
                return;
            }

            EnsureEditSession(checklist);
            PungentChecklistDefinition draft = _editSession.Draft;
            if (draft == null)
                return;

            if (_lastValidation != null && (_lastValidation.HasErrors || _lastValidation.HasWarnings))
                EditorGUILayout.HelpBox(_lastValidation.ToDisplayText(), _lastValidation.HasErrors ? MessageType.Error : MessageType.Warning);

            DrawEditDefinitionFields(draft);
            EditorGUILayout.Space(8f);
            if (draft.sections == null || draft.sections.Count == 0)
            {
                EditorGUILayout.HelpBox("This checklist has no sections yet. Add a section to start authoring.", MessageType.Info);
                DrawAddSectionRow(0);
                return;
            }

            for (int i = 0; i < draft.sections.Count; i++)
            {
                DrawEditSection(draft.sections[i], i);
                DrawAddSectionRow(i + 1);
            }
        }

        private void DrawReadOnlyChecklistPreview(PungentChecklistDefinition checklist)
        {
            foreach (PungentChecklistSectionDefinition section in checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null)
                    continue;

                EditorGUILayout.Space(4f);
                UtilityWindowTheme.SectionTitle(section.title, UtilityWindowTheme.Neutral, (section.items == null ? 0 : section.items.Count) + " items");
                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                    EditorGUILayout.LabelField(item.id + " | " + item.label, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawEditDefinitionFields(PungentChecklistDefinition draft)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Checklist Definition", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                draft.title = EditorGUILayout.TextField("Title", draft.title);
                draft.description = EditorGUILayout.TextField("Description", draft.description);
                draft.defaultGuidance = EditorGUILayout.TextField("Default Guidance", draft.defaultGuidance);
                draft.targetUtilityId = EditorGUILayout.TextField("Target Utility", draft.targetUtilityId);
                draft.tags = DrawTagListField("draft-tags:" + draft.checklistId, "Tags", draft.tags);
                draft.archived = EditorGUILayout.Toggle("Archived", draft.archived);

                IReadOnlyList<PungentChecklistStateProfileDefinition> profiles = PungentChecklistProfiles.AllBuiltInProfiles;
                int current = Mathf.Max(0, profiles.ToList().FindIndex(profile => string.Equals(profile.profileId, draft.stateProfileId, StringComparison.OrdinalIgnoreCase)));
                string[] labels = profiles.Select(profile => profile.label).ToArray();
                int next = EditorGUILayout.Popup("State Profile", current, labels);
                if (next >= 0 && next < profiles.Count)
                {
                    draft.stateProfileId = profiles[next].profileId;
                    draft.listKind = profiles[next].listKind;
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Checklist ID", draft.checklistId);
                    EditorGUILayout.TextField("Source", string.IsNullOrWhiteSpace(draft.sourceLabel) ? draft.sourceProviderId : draft.sourceLabel);
                }

                if (EditorGUI.EndChangeCheck())
                    _editSession.Touch();
            }
        }

        private void DrawEditSection(PungentChecklistSectionDefinition section, int index)
        {
            if (section == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Section " + (index + 1), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(index <= 0))
                    {
                        if (GUILayout.Button(new GUIContent("Up", "Move this section up."), EditorStyles.miniButton, GUILayout.Width(38f)))
                            _editSession.MoveSection(section, -1);
                    }
                    using (new EditorGUI.DisabledScope(_editSession.Draft == null || _editSession.Draft.sections == null || index >= _editSession.Draft.sections.Count - 1))
                    {
                        if (GUILayout.Button(new GUIContent("Down", "Move this section down."), EditorStyles.miniButton, GUILayout.Width(48f)))
                            _editSession.MoveSection(section, 1);
                    }
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(74f)))
                        _editSession.DuplicateSection(section);
                    if (GUILayout.Button(section.archived ? "Unarchive" : "Archive", EditorStyles.miniButton, GUILayout.Width(82f)))
                    {
                        section.archived = !section.archived;
                        _editSession.Touch();
                    }
                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(64f)) &&
                        EditorUtility.DisplayDialog("Remove Section", "Remove section '" + section.title + "' from this project checklist?", "Remove", "Cancel"))
                    {
                        _editSession.RemoveSection(section);
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUI.BeginChangeCheck();
                section.id = EditorGUILayout.TextField("ID", section.id);
                section.title = EditorGUILayout.TextField("Title", section.title);
                section.guidancePrompt = EditorGUILayout.TextField("Guidance", section.guidancePrompt);
                section.owner = EditorGUILayout.TextField("Owner", section.owner);
                section.priority = EditorGUILayout.TextField("Priority", section.priority);
                section.dueUtc = EditorGUILayout.TextField("Due UTC", section.dueUtc);
                if (EditorGUI.EndChangeCheck())
                    _editSession.Touch();

                List<PungentChecklistItemDefinition> items = (section.items ?? new List<PungentChecklistItemDefinition>()).ToList();
                for (int i = 0; i < items.Count; i++)
                    DrawEditItem(section, items[i], i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(16f);
                    if (GUILayout.Button(new GUIContent("+ Item", "Add a new checklist item to this section."), EditorStyles.miniButton, GUILayout.Width(74f)))
                        _editSession.AddItem(section);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawAddSectionRow(int insertIndex)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                if (GUILayout.Button(new GUIContent("+ Section", "Add a new section below this section."), EditorStyles.miniButton, GUILayout.Width(92f)))
                    _editSession.AddSection(insertIndex);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawEditItem(PungentChecklistSectionDefinition section, PungentChecklistItemDefinition item, int index)
        {
            if (item == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(item.archived ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(item.label) ? "Checklist Item" : item.label, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(index <= 0))
                    {
                        if (GUILayout.Button(new GUIContent("Up", "Move this item up."), EditorStyles.miniButton, GUILayout.Width(38f)))
                            _editSession.MoveItem(section, item, -1);
                    }
                    using (new EditorGUI.DisabledScope(section == null || section.items == null || index >= section.items.Count - 1))
                    {
                        if (GUILayout.Button(new GUIContent("Down", "Move this item down."), EditorStyles.miniButton, GUILayout.Width(48f)))
                            _editSession.MoveItem(section, item, 1);
                    }
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(74f)))
                        _editSession.DuplicateItem(section, item);
                    if (GUILayout.Button(item.archived ? "Unarchive" : "Archive", EditorStyles.miniButton, GUILayout.Width(82f)))
                    {
                        item.archived = !item.archived;
                        _editSession.Touch();
                    }
                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(64f)) &&
                        EditorUtility.DisplayDialog("Remove Item", "Remove item '" + item.label + "' from this project checklist?", "Remove", "Cancel"))
                    {
                        _editSession.RemoveItem(section, item);
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUI.BeginChangeCheck();
                item.id = EditorGUILayout.TextField("ID", item.id);
                item.label = EditorGUILayout.TextField("Label", item.label);
                item.detail = EditorGUILayout.TextField("Detail", item.detail);
                item.isOptional = EditorGUILayout.Toggle("Optional", item.isOptional);
                item.owner = EditorGUILayout.TextField("Owner", item.owner);
                item.priority = EditorGUILayout.TextField("Priority", item.priority);
                item.dueUtc = EditorGUILayout.TextField("Due UTC", item.dueUtc);
                item.linkedStateKey = EditorGUILayout.TextField("Linked State Key", item.linkedStateKey);
                item.ownerPackageId = EditorGUILayout.TextField("Owner Package", item.ownerPackageId);
                item.canonicalOwnerPackageId = EditorGUILayout.TextField("Canonical Package", item.canonicalOwnerPackageId);
                item.childChecklistId = EditorGUILayout.TextField("Child Checklist ID", item.childChecklistId);
                item.childPassRule = EditorGUILayout.TextField("Child Pass Rule", item.childPassRule);
                item.appearsInPackageIds = DrawTagListField("item-appears:" + item.GetHashCode(), "Appears In Packages", item.appearsInPackageIds);
                if (EditorGUI.EndChangeCheck())
                    _editSession.Touch();
            }
        }

        private void DrawItem(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, RunViewCache cache = null)
        {
            string stateId = cache == null ? GetStateId(checklist, item) : cache.GetStateId(item);
            PungentChecklistStateOptionDefinition state = cache == null ? PungentChecklistProfiles.ResolveState(checklist, stateId) : cache.GetState(item);
            Color tint = StateColor(state);
            bool computedFromChild = IsComputedFromChild(item);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(item.id, EditorStyles.boldLabel, GUILayout.Width(80f));
                    GUILayout.Label(item.label, EditorStyles.wordWrappedMiniLabel);
                    GUILayout.FlexibleSpace();
                    DrawStatePill(state, computedFromChild);
                }

                if (!string.IsNullOrWhiteSpace(item.detail))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(item.detail, EditorStyles.wordWrappedMiniLabel);
                    EditorGUI.indentLevel--;
                }

                DrawItemMetadata(item, cache);

                if (computedFromChild)
                    DrawChildChecklistControls(item, state);
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        IReadOnlyList<PungentChecklistStateOptionDefinition> options = cache == null ? PungentChecklistProfiles.ResolveStateOptions(checklist) : cache.StateOptions;
                        foreach (PungentChecklistStateOptionDefinition option in options)
                            DrawStateButton(checklist, item, option, state);
                        GUILayout.FlexibleSpace();
                    }
                }

                string note = cache == null ? GetNote(checklist, item.id) : cache.GetNote(item);
                string commentControlName = "ChecklistComment_" + SafeControlName(checklist == null ? string.Empty : checklist.checklistId) + "_" + SafeControlName(item.id) + "_" + item.GetHashCode().ToString("X");
                GUI.SetNextControlName(commentControlName);
                EditorGUI.BeginChangeCheck();
                string nextNote = EditorGUILayout.TextField(new GUIContent("Comment", "Short follow-up detail for this result appearance."), note);
                if (EditorGUI.EndChangeCheck())
                    SetNote(checklist, item, nextNote);

                if (state.requiresComment && string.IsNullOrWhiteSpace(nextNote))
                    EditorGUILayout.HelpBox(state.label + " requires a short comment.", MessageType.Warning);
            }
        }

        private void DrawStatePill(PungentChecklistStateOptionDefinition state, bool computedFromChild)
        {
            string suffix = computedFromChild ? " (child)" : string.Empty;
            Color previous = GUI.color;
            GUI.color = StateColor(state);
            GUILayout.Label((state == null ? "Open" : state.shortLabel) + suffix, EditorStyles.miniLabel, GUILayout.Width(computedFromChild ? 104f : 78f));
            GUI.color = previous;
        }

        private void DrawStateButton(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, PungentChecklistStateOptionDefinition buttonState, PungentChecklistStateOptionDefinition currentState)
        {
            if (buttonState == null)
                return;

            bool selected = currentState != null && string.Equals(currentState.stateId, buttonState.stateId, StringComparison.OrdinalIgnoreCase);
            Color previous = GUI.backgroundColor;
            if (selected)
                GUI.backgroundColor = StateColor(buttonState);
            string label = PungentChecklistProfiles.IsQualityGate(checklist) && buttonState.stateId == PungentChecklistProfiles.StateUntested ? "Clear" : buttonState.shortLabel;
            float width = Mathf.Clamp(46f + label.Length * 5f, 54f, 116f);
            if (GUILayout.Button(label, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Width(width)))
            {
                string next = selected && buttonState.stateId != PungentChecklistProfiles.DefaultStateId(checklist)
                    ? PungentChecklistProfiles.DefaultStateId(checklist)
                    : buttonState.stateId;
                SetStateId(checklist, item, next);
            }
            GUI.backgroundColor = previous;
        }

        private void DrawItemMetadata(PungentChecklistItemDefinition item, RunViewCache cache = null)
        {
            if (cache != null && cache.metadataByItem.TryGetValue(item, out string cachedMetadata))
            {
                if (string.IsNullOrWhiteSpace(cachedMetadata))
                    return;

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(cachedMetadata, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
                return;
            }

            List<string> metadata = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.linkedStateKey))
                metadata.Add("Linked state: " + LinkedStateLabel(item.linkedStateKey) + " (" + CountLinkedAppearances(item.linkedStateKey) + " appearances)");
            if (!string.IsNullOrWhiteSpace(item.ownerPackageId))
                metadata.Add("Owner: " + item.ownerPackageId);
            if (!string.IsNullOrWhiteSpace(item.canonicalOwnerPackageId) && !string.Equals(item.canonicalOwnerPackageId, item.ownerPackageId, StringComparison.OrdinalIgnoreCase))
                metadata.Add("Canonical owner: " + item.canonicalOwnerPackageId);
            if (item.appearsInPackageIds != null && item.appearsInPackageIds.Count > 0)
                metadata.Add("Appears in: " + string.Join(", ", item.appearsInPackageIds.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()));
            if (item.isOptional)
                metadata.Add("Optional child item");
            if (!string.IsNullOrWhiteSpace(item.priority))
                metadata.Add("Priority: " + item.priority);
            if (!string.IsNullOrWhiteSpace(item.owner))
                metadata.Add("Owner: " + item.owner);
            if (!string.IsNullOrWhiteSpace(item.dueUtc))
                metadata.Add("Due: " + item.dueUtc);
            if (item.archived)
                metadata.Add("Archived");

            if (metadata.Count == 0)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(string.Join(" | ", metadata.ToArray()), EditorStyles.wordWrappedMiniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawChildChecklistControls(PungentChecklistItemDefinition item, PungentChecklistStateOptionDefinition state)
        {
            string childId = item.childChecklistId ?? string.Empty;
            PungentChecklistDefinition child = FindChecklist(childId);
            string message = child == null
                ? "Child checklist '" + childId + "' is missing. Import the child checklist before this parent can pass."
                : "State is computed from child checklist '" + child.title + "'. The parent passes only when the child checklist satisfies " + EffectiveChildPassRule(item) + ".";
            EditorGUILayout.HelpBox(message, child == null ? MessageType.Warning : MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(child == null))
                {
                    if (GUILayout.Button(new GUIContent("Open Child", "Switch to the child checklist that controls this parent item."), EditorStyles.miniButton, GUILayout.Width(88f)))
                        SelectChecklist(childId);
                }

                if (GUILayout.Button(new GUIContent("Copy Child ID", "Copy the child checklist ID."), EditorStyles.miniButton, GUILayout.Width(96f)))
                {
                    EditorGUIUtility.systemCopyBuffer = childId;
                    _status = "Copied child checklist ID.";
                }

                GUILayout.Label("Computed state: " + (state == null ? "Open" : state.label), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawGuidance(PungentChecklistDefinition checklist)
        {
            string guidance = RebuildRunViewCacheIfNeeded(checklist).guidance;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Follow-Up Guidance", EditorStyles.boldLabel);
            if (string.IsNullOrWhiteSpace(guidance))
            {
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(checklist.defaultGuidance) ? "No partial or failed items yet. Keep running the checklist or copy results when finished." : checklist.defaultGuidance, MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(guidance, MessageType.Warning);
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                string storage = string.IsNullOrWhiteSpace(PungentChecklistStorage.LastError)
                    ? "Definitions: package + project storage + legacy local"
                    : PungentChecklistStorage.LastError;
                EditorGUILayout.LabelField(storage, EditorStyles.miniLabel, GUILayout.Width(330f));
            }
        }

        private void ShowCreateMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Project Checklist/Quality Gate"), false, () => CreateProjectChecklist("New Quality Gate Checklist", PungentChecklistListKinds.QualityGate, PungentChecklistProfileIds.QualityGate));
            menu.AddItem(new GUIContent("New Project Checklist/To-Do List"), false, () => CreateProjectChecklist("New To-Do List", PungentChecklistListKinds.ToDo, PungentChecklistProfileIds.ToDo));
            menu.AddItem(new GUIContent("New Project Checklist/Review Checklist"), false, () => CreateProjectChecklist("New Review Checklist", PungentChecklistListKinds.Review, PungentChecklistProfileIds.Review));
            menu.AddItem(new GUIContent("New Project Checklist/Release Readiness"), false, () => CreateProjectChecklist("New Release Readiness Checklist", PungentChecklistListKinds.ReleaseReadiness, PungentChecklistProfileIds.ReleaseMigration));
            menu.AddItem(new GUIContent("New Project Checklist/Bug Triage List"), false, () => CreateProjectChecklist("New Bug Triage List", PungentChecklistListKinds.BugTriage, PungentChecklistProfileIds.BugTriage));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Create Data Sheet Checklist Template"), false, CreateChecklistDataSheet);
            PungentChecklistDefinition selected = SelectedChecklist;
            if (selected != null)
                menu.AddItem(new GUIContent("Create Data Sheet From Selected Checklist"), false, () => CreateChecklistDataSheet(selected));
            else
                menu.AddDisabledItem(new GUIContent("Create Data Sheet From Selected Checklist"));
            menu.AddItem(new GUIContent("Create Rich Document Checklist Template"), false, CreateChecklistRichDocument);
            menu.ShowAsContext();
        }

        private void ShowImportMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Import JSON"), false, ImportChecklistJson);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy JSON Definition Template"), false, CopyDefinitionTemplate);
            menu.AddItem(new GUIContent("Copy JSON Bundle Template"), false, CopyBundleTemplate);
            menu.ShowAsContext();
        }

        private void ShowExportMenu(PungentChecklistDefinition checklist)
        {
            GenericMenu menu = new GenericMenu();
            if (checklist == null)
            {
                menu.AddDisabledItem(new GUIContent("No checklist selected"));
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("Definition JSON"), false, () => ExportDefinitionJson(checklist));
            menu.AddItem(new GUIContent("Bundle JSON"), false, () => ExportBundleJson(checklist));
            menu.AddItem(new GUIContent("Results JSON"), false, () => ExportResultsJson(checklist));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy Results To Clipboard"), false, () => CopyResults(checklist));
            menu.AddItem(new GUIContent("Copy Guidance To Clipboard"), false, () => CopyGuidance(checklist));
            menu.AddSeparator("Authoring Surfaces/");
            menu.AddItem(new GUIContent("Authoring Surfaces/Data Sheet From Selected Checklist"), false, () => CreateChecklistDataSheet(checklist));
            menu.AddItem(new GUIContent("Authoring Surfaces/Blank Data Sheet Checklist Template"), false, CreateChecklistDataSheet);
            menu.AddItem(new GUIContent("Authoring Surfaces/Rich Document Checklist Template"), false, CreateChecklistRichDocument);
            menu.ShowAsContext();
        }

        private void CreateProjectChecklist(string title, string listKind, string profileId)
        {
            PungentChecklistDefinition checklist = PungentChecklistDefinitionEditSession.CreateBlank(title, listKind, profileId);
            checklist.checklistId = PungentChecklistDefinitionRegistry.UniqueProjectChecklistId(checklist.checklistId);
            checklist.NormalizeInPlace();
            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(checklist, _linkedStateGroups, out string error))
            {
                EditorUtility.DisplayDialog("Create Checklist", error, "OK");
                return;
            }

            EnsureDefinitionsLoaded(true);
            SelectChecklist(checklist.checklistId);
            _mode = WorkbenchMode.Edit;
            _editSession.Load(checklist);
            SavePrefs();
            _status = "Created project checklist.";
        }

        private void EnsureEditSession(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
            {
                _editSession.Clear();
                return;
            }

            if (!_editSession.HasDraft || !string.Equals(_editSession.SourceId, checklist.checklistId, StringComparison.OrdinalIgnoreCase))
            {
                _lastValidation = null;
                _editListTextCache.Clear();
                _editSession.Load(checklist);
            }
        }

        private void SaveEditDraft()
        {
            PungentChecklistDefinition draft = _editSession.Draft;
            if (draft == null)
                return;

            _editSession.NormalizeDraft();
            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(draft, _linkedStateGroups, out string error))
            {
                EditorUtility.DisplayDialog("Save Checklist", error, "OK");
                return;
            }

            _editSession.MarkClean();
            EnsureDefinitionsLoaded(true);
            SelectChecklist(draft.checklistId);
            _status = "Saved project checklist.";
        }

        private void RevertEditDraft()
        {
            PungentChecklistDefinition checklist = SelectedChecklist;
            if (checklist == null)
                return;

            if (_editSession.IsDirty && !EditorUtility.DisplayDialog("Revert Checklist Edits", "Discard unsaved checklist edits?", "Revert", "Cancel"))
                return;

            _editSession.Load(checklist);
            _editListTextCache.Clear();
            _lastValidation = null;
            _status = "Reverted checklist edits.";
        }

        private void AddEditSection()
        {
            EnsureEditSession(SelectedChecklist);
            _editSession.AddSection();
        }

        private void ValidateSelectedChecklist()
        {
            PungentChecklistDefinition checklist = _mode == WorkbenchMode.Edit && _editSession.HasDraft ? PungentChecklistSerialization.Clone(_editSession.Draft) : SelectedChecklist;
            if (checklist == null)
                return;

            checklist.NormalizeInPlace();
            _lastValidation = PungentChecklistValidation.ValidateDefinition(
                checklist,
                _checklists.Where(item => item != null).Select(item => item.checklistId),
                _linkedStateGroups.Where(item => item != null).Select(item => item.linkedStateKey));
            _status = _lastValidation.Summary;
            if (_lastValidation.HasErrors || _lastValidation.HasWarnings)
                EditorUtility.DisplayDialog("Validate Checklist", _lastValidation.ToDisplayText(20), "OK");
        }

        private void CloneSelectedChecklistToProject(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            if (!PungentChecklistDefinitionRegistry.CloneToProject(checklist, out PungentChecklistDefinition clone, out string error))
            {
                EditorUtility.DisplayDialog("Clone Checklist", error, "OK");
                return;
            }

            EnsureDefinitionsLoaded(true);
            SelectChecklist(clone.checklistId);
            _mode = WorkbenchMode.Edit;
            _editSession.Load(clone);
            SavePrefs();
            _status = "Cloned checklist to project storage.";
        }

        private static List<string> ParseTags(string value)
        {
            return PungentAuthoringMetadata.NormalizeTags((value ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private List<string> DrawTagListField(string cacheKey, string label, List<string> values)
        {
            if (!_editListTextCache.TryGetValue(cacheKey, out string text))
            {
                text = values == null ? string.Empty : string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
                _editListTextCache[cacheKey] = text;
            }

            string next = EditorGUILayout.TextField(label, text);
            if (string.Equals(next, text, StringComparison.Ordinal))
                return values ?? new List<string>();

            _editListTextCache[cacheKey] = next;
            return ParseTags(next);
        }

        private void OpenChecklistTarget(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            if (!string.IsNullOrWhiteSpace(checklist.targetUtilityId))
            {
                if (PungentUtilityRegistry.Open(checklist.targetUtilityId))
                {
                    _status = "Opened target utility.";
                    return;
                }

                _status = "Target utility '" + checklist.targetUtilityId + "' is not available.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(checklist.sourceProviderId) && !string.IsNullOrWhiteSpace(checklist.sourceItemId))
            {
                PungentAuthoringReference source = PungentAuthoringReference.Create(PungentAuthoringItemKind.Custom, checklist.sourceItemId, checklist.sourceProviderId, checklist.sourceLabel);
                if (PungentAuthoringProviderRegistry.TryOpen(source))
                {
                    _status = "Opened checklist source.";
                    return;
                }

                _status = "Checklist source provider '" + checklist.sourceProviderId + "' is not available.";
                return;
            }

            _status = "This checklist has no linked target editor.";
        }

        private void OpenChecklistPreviewOverlay(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            PungentAuthoringReference reference = checklist.ToReference();
            PungentAuthoringPreview preview;
            if (!PungentAuthoringProviderRegistry.TryGetPreview(reference, out preview))
                preview = PungentAuthoringPreview.Missing(string.IsNullOrWhiteSpace(checklist.title) ? "Checklist" : checklist.title, "Checklist preview data is not available.");

            PungentStickyNoteOverlayController.OpenAuthoringPreview(
                reference,
                preview,
                GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect()),
                PungentStickyNoteOverlayOwner.UtilitySurface,
                "Checklist Utility");
            _status = "Opened checklist preview overlay.";
        }

        private void ImportChecklistJson()
        {
            string path = EditorUtility.OpenFilePanel("Import Checklist JSON", string.Empty, "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Checklist JSON", "Could not read the selected file.\n\n" + ex.Message, "OK");
                return;
            }

            if (!PungentChecklistSerialization.TryParseChecklistImport(json, out List<PungentChecklistDefinition> importedChecklists, out List<PungentLinkedStateGroupDefinition> importedLinkedGroups, out string error))
            {
                EditorUtility.DisplayDialog("Import Checklist JSON", error, "OK");
                return;
            }

            foreach (PungentChecklistDefinition checklist in importedChecklists)
            {
                if (PungentChecklistDefinitionRegistry.IsProviderDefinitionId(checklist.checklistId))
                {
                    EditorUtility.DisplayDialog("Import Checklist JSON", "Imported checklist ID '" + checklist.checklistId + "' is reserved by an installed package-provided checklist.", "OK");
                    return;
                }
            }

            List<string> replacing = importedChecklists
                .Where(checklist => PungentChecklistDefinitionRegistry.IsProjectDefinitionId(checklist.checklistId))
                .Select(checklist => checklist.title)
                .ToList();
            if (replacing.Count > 0 && !EditorUtility.DisplayDialog("Replace Project Checklist", "Replace project checklist(s):\n\n" + string.Join("\n", replacing.ToArray()), "Replace", "Cancel"))
                return;

            for (int i = 0; i < importedChecklists.Count; i++)
            {
                PungentChecklistDefinition checklist = importedChecklists[i];
                checklist.schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
                if (string.IsNullOrWhiteSpace(checklist.sourceProviderId))
                    checklist.sourceProviderId = PungentChecklistConstants.ProviderId;
                if (string.IsNullOrWhiteSpace(checklist.sourcePath))
                    checklist.sourcePath = path;
                if (string.IsNullOrWhiteSpace(checklist.sourceLabel))
                    checklist.sourceLabel = Path.GetFileName(path);
                checklist.NormalizeInPlace();

                if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(checklist, importedLinkedGroups, out error))
                {
                    EditorUtility.DisplayDialog("Import Checklist JSON", error, "OK");
                    return;
                }
            }

            EnsureDefinitionsLoaded(true);
            SelectChecklist(importedChecklists[0].checklistId);
            _status = importedChecklists.Count == 1
                ? "Imported project checklist '" + importedChecklists[0].title + "'."
                : "Imported " + importedChecklists.Count + " project checklists.";
        }

        private void ExportDefinitionJson(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            string safeName = string.IsNullOrWhiteSpace(checklist.checklistId) ? "checklist-definition" : checklist.checklistId;
            string path = EditorUtility.SaveFilePanel("Export Checklist Definition JSON", string.Empty, safeName + ".json", "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                File.WriteAllText(path, PungentChecklistSerialization.ToJson(checklist, true), new UTF8Encoding(false));
                _status = "Exported checklist definition.";
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Checklist Definition JSON", "Could not write definition.\n\n" + ex.Message, "OK");
            }
        }

        private void ExportBundleJson(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            string safeName = string.IsNullOrWhiteSpace(checklist.checklistId) ? "checklist-bundle" : checklist.checklistId + "-bundle";
            string path = EditorUtility.SaveFilePanel("Export Checklist Bundle JSON", string.Empty, safeName + ".json", "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            List<PungentChecklistDefinition> bundleChecklists = new List<PungentChecklistDefinition>();
            CollectChecklistAndChildren(checklist, bundleChecklists, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            HashSet<string> linkedKeys = new HashSet<string>(PungentChecklistSerialization.EnumerateItems(checklist).Where(item => !string.IsNullOrWhiteSpace(item.linkedStateKey)).Select(item => item.linkedStateKey), StringComparer.OrdinalIgnoreCase);
            PungentChecklistImportBundle bundle = new PungentChecklistImportBundle
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                bundleId = checklist.checklistId + "-bundle",
                title = checklist.title + " Bundle",
                sourceProviderId = PungentChecklistConstants.ProviderId,
                checklists = bundleChecklists.Select(PungentChecklistSerialization.Clone).ToList(),
                linkedStateGroups = _linkedStateGroups.Where(group => group != null && linkedKeys.Contains(group.linkedStateKey)).Select(PungentChecklistSerialization.Clone).ToList()
            };
            bundle.NormalizeInPlace();

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(bundle, true), new UTF8Encoding(false));
                _status = "Exported checklist bundle.";
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Checklist Bundle JSON", "Could not write bundle.\n\n" + ex.Message, "OK");
            }
        }

        private void ExportResultsJson(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            FlushPendingTextPersistence(true);
            string safeName = string.IsNullOrWhiteSpace(checklist.checklistId) ? "checklist-results" : checklist.checklistId + "-results";
            string path = EditorUtility.SaveFilePanel("Export Checklist Results JSON", string.Empty, safeName + ".json", "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            PungentChecklistResultsExport export = new PungentChecklistResultsExport
            {
                schemaVersion = PungentChecklistConstants.CurrentSchemaVersion,
                checklistId = checklist.checklistId,
                title = checklist.title,
                listKind = checklist.listKind,
                stateProfileId = checklist.stateProfileId,
                exportedUtc = DateTime.UtcNow.ToString("o"),
                followUpGuidance = BuildGuidanceText(checklist),
                items = new List<PungentChecklistResultItemExport>()
            };

            PungentChecklistUtilityStateService.StateSnapshot snapshot = new PungentChecklistUtilityStateService.StateSnapshot(checklist, FindChecklist);
            foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
            {
                string stateId = snapshot.GetStateId(item);
                export.items.Add(new PungentChecklistResultItemExport
                {
                    id = item.id,
                    label = item.label,
                    state = snapshot.ResolveState(stateId).label,
                    stateId = stateId,
                    note = GetNote(checklist, item.id),
                    linkedStateKey = item.linkedStateKey,
                    isLinkedAppearance = !string.IsNullOrWhiteSpace(item.linkedStateKey),
                    ownerPackageId = item.ownerPackageId,
                    canonicalOwnerPackageId = item.canonicalOwnerPackageId,
                    childChecklistId = item.childChecklistId,
                    parentStateMode = item.parentStateMode,
                    childPassRule = item.childPassRule
                });
            }

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(export, true), new UTF8Encoding(false));
                _status = "Exported checklist results.";
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Checklist Results JSON", "Could not write results.\n\n" + ex.Message, "OK");
            }
        }

        private void CopyResults(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            FlushPendingTextPersistence(true);
            EditorGUIUtility.systemCopyBuffer = BuildResultsText(checklist);
            _status = "Copied checklist results.";
        }

        private void CopyGuidance(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            FlushPendingTextPersistence(true);
            EditorGUIUtility.systemCopyBuffer = BuildGuidanceText(checklist);
            _status = "Copied checklist guidance.";
        }

        private void CopyDefinitionTemplate()
        {
            EditorGUIUtility.systemCopyBuffer = PungentChecklistSerialization.ToJson(PungentChecklistSerialization.CreateDefinitionTemplate(), true);
            _status = "Copied checklist definition template.";
        }

        private void CopyBundleTemplate()
        {
            EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(PungentChecklistSerialization.CreateBundleTemplate(), true);
            _status = "Copied checklist bundle template.";
        }

        private void CreateChecklistDataSheet()
        {
            if (InvokeStatic("PungentFunk.Utilities.Editor.DataSheets.PungentDataSheetEditorWindow", "CreateChecklistDefinitionSheet"))
                _status = "Created checklist definition sheet.";
            else
                _status = "Data Sheet editor is not available.";
        }

        private void CreateChecklistDataSheet(PungentChecklistDefinition checklist)
        {
            if (checklist != null && InvokeStatic("PungentFunk.Utilities.Editor.DataSheets.PungentDataSheetEditorWindow", "CreateChecklistDefinitionSheetFromChecklist", checklist.checklistId))
                _status = "Created checklist definition sheet from selected checklist.";
            else
                _status = "Data Sheet editor is not available.";
        }

        private void CreateChecklistRichDocument()
        {
            if (InvokeStatic("PungentFunk.Utilities.Editor.RichDocuments.PungentRichDocumentEditorWindow", "CreateChecklistDefinitionDocument"))
                _status = "Created checklist definition document.";
            else
                _status = "Rich Document editor is not available.";
        }

        private void PromoteLegacyLocalChecklist(PungentChecklistDefinition checklist)
        {
            if (checklist == null || !IsLegacyLocalChecklist(checklist))
                return;

            if (!EditorUtility.DisplayDialog("Promote Local Checklist", "Save '" + checklist.title + "' into project checklist storage? The local editor-pref copy will be removed after a successful save.", "Promote", "Cancel"))
                return;

            PungentChecklistDefinition copy = PungentChecklistSerialization.Clone(checklist);
            copy.sourceProviderId = PungentChecklistConstants.ProviderId;
            copy.sourceLabel = "Promoted legacy local checklist";
            copy.sourcePath = string.Empty;
            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(copy, _linkedStateGroups, out string error))
            {
                EditorUtility.DisplayDialog("Promote Local Checklist", error, "OK");
                return;
            }

            RemoveLegacyLocalChecklist(checklist.checklistId);
            SaveLegacyLocalDefinitions();
            EnsureDefinitionsLoaded(true);
            SelectChecklist(copy.checklistId);
            _status = "Promoted local checklist to project storage.";
        }

        private void ResetChecklist(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
                return;

            FlushPendingTextPersistence(true);
            if (!EditorUtility.DisplayDialog("Reset Checklist", "Reset states and notes for '" + checklist.title + "' and any child checklists it references?", "Reset", "Cancel"))
                return;

            List<PungentChecklistDefinition> resetTargets = new List<PungentChecklistDefinition>();
            CollectChecklistAndChildren(checklist, resetTargets, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            PungentChecklistUtilityStateService.Reset(resetTargets);
            foreach (PungentChecklistDefinition target in resetTargets)
                foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(target))
                    _notes[NoteCacheKey(target, item.id)] = string.Empty;
            MarkRunViewDirty();
            _status = resetTargets.Count == 1 ? "Checklist reset." : "Checklist and child checklists reset.";
        }

        private void SetSelectedChecklistArchived(PungentChecklistDefinition checklist, bool archived)
        {
            if (checklist == null || !IsProjectChecklist(checklist))
                return;

            if (!PungentChecklistDefinitionRegistry.ArchiveProjectDefinition(checklist.checklistId, archived, out string error))
            {
                EditorUtility.DisplayDialog(archived ? "Archive Checklist" : "Unarchive Checklist", error, "OK");
                return;
            }

            EnsureDefinitionsLoaded(true);
            SelectChecklist(checklist.checklistId);
            _status = archived ? "Archived checklist." : "Unarchived checklist.";
        }

        private void DeleteSelectedProjectChecklist(PungentChecklistDefinition checklist)
        {
            if (checklist == null || !IsProjectChecklist(checklist))
                return;

            if (checklist.locked)
            {
                EditorUtility.DisplayDialog("Delete Checklist", "This checklist is locked. Unlock it before deleting it.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete Project Checklist", "Delete project checklist '" + checklist.title + "'? This removes the definition from project checklist storage. Result notes and states are left untouched.", "Delete", "Cancel"))
                return;

            if (!PungentChecklistDefinitionRegistry.DeleteProjectDefinition(checklist.checklistId, out string error))
            {
                EditorUtility.DisplayDialog("Delete Checklist", error, "OK");
                return;
            }

            EnsureDefinitionsLoaded(true);
            if (_checklists.Count > 0)
                SelectChecklist(_checklists[0].checklistId);
            _status = "Deleted project checklist definition.";
        }

        private void ArchiveCompletedItems(PungentChecklistDefinition checklist, bool visibleOnly)
        {
            if (checklist == null || !IsProjectChecklist(checklist))
                return;

            HashSet<string> visibleIds = visibleOnly
                ? new HashSet<string>(VisibleRunItems(checklist).Select(item => item.id), StringComparer.OrdinalIgnoreCase)
                : null;

            PungentChecklistDefinition clone = PungentChecklistSerialization.Clone(checklist);
            if (clone == null)
                return;

            int count = 0;
            foreach (PungentChecklistSectionDefinition section in clone.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null || section.archived)
                    continue;

                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                {
                    if (item == null || item.archived)
                        continue;
                    if (visibleOnly && (visibleIds == null || !visibleIds.Contains(item.id)))
                        continue;

                    PungentChecklistItemDefinition sourceItem = FindChecklistItem(checklist, item.id);
                    string stateId = GetStateId(checklist, sourceItem ?? item);
                    if (!PungentChecklistProfiles.ResolveState(checklist, stateId).countsAsComplete)
                        continue;

                    item.archived = true;
                    item.updatedUtc = DateTime.UtcNow.ToString("o");
                    count++;
                }
            }

            if (count == 0)
            {
                _status = visibleOnly ? "No visible completed items to archive." : "No completed items to archive.";
                return;
            }

            string scope = visibleOnly ? "visible completed item(s)" : "completed item(s)";
            if (!EditorUtility.DisplayDialog("Archive Completed Items", "Archive " + count + " " + scope + " in '" + checklist.title + "'?", "Archive", "Cancel"))
                return;

            clone.Touch();
            if (!PungentChecklistDefinitionRegistry.UpsertProjectDefinition(clone, _linkedStateGroups, out string error))
            {
                EditorUtility.DisplayDialog("Archive Completed Items", error, "OK");
                return;
            }

            EnsureDefinitionsLoaded(true);
            SelectChecklist(checklist.checklistId);
            _status = "Archived " + count + " completed item(s).";
        }

        private RunViewCache RebuildRunViewCacheIfNeeded(PungentChecklistDefinition checklist)
        {
            if (checklist == null)
            {
                _runViewCache.Clear();
                _runViewDirty = false;
                return _runViewCache;
            }

            if (!_runViewDirty && _runViewCache.Matches(checklist, _showArchived, _filterMode, _search, _stateFilter, _sectionFilter, _ownerFilter, _priorityFilter, _dueFilter))
                return _runViewCache;

            _runViewCache.Clear();
            _runViewCache.checklistId = checklist.checklistId ?? string.Empty;
            _runViewCache.showArchived = _showArchived;
            _runViewCache.filterMode = _filterMode;
            _runViewCache.search = _search ?? string.Empty;
            _runViewCache.stateFilter = _stateFilter ?? string.Empty;
            _runViewCache.sectionFilter = _sectionFilter ?? string.Empty;
            _runViewCache.ownerFilter = _ownerFilter ?? string.Empty;
            _runViewCache.priorityFilter = _priorityFilter ?? string.Empty;
            _runViewCache.dueFilter = _dueFilter ?? string.Empty;
            _runViewCache.snapshot = new PungentChecklistUtilityStateService.StateSnapshot(checklist, FindChecklist);
            _runViewCache.summary = PungentChecklistUtilityStateService.Count(_runViewCache.snapshot, _showArchived);
            _runViewCache.guidance = BuildGuidanceText(checklist, _runViewCache.snapshot);

            foreach (PungentChecklistSectionDefinition section in checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null || (section.archived && !_showArchived))
                    continue;

                RunSectionView sectionView = new RunSectionView
                {
                    section = section,
                    pill = SectionPill(checklist, section, _runViewCache.snapshot)
                };

                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                {
                    if (item == null)
                        continue;

                    string stateId = _runViewCache.snapshot.GetStateId(item);
                    PungentChecklistStateOptionDefinition state = _runViewCache.snapshot.ResolveState(stateId);
                    string note = GetNote(checklist, item.id);
                    _runViewCache.stateIdsByItem[item] = stateId;
                    _runViewCache.statesByItem[item] = state;
                    _runViewCache.notesByItem[item] = note;
                    _runViewCache.metadataByItem[item] = BuildItemMetadataString(item);

                    if (ShouldShow(checklist, item, _runViewCache.snapshot, stateId, note))
                        sectionView.items.Add(item);
                }

                if (sectionView.items.Count > 0)
                    _runViewCache.sections.Add(sectionView);
            }

            _runViewCache.valid = true;
            _runViewDirty = false;
            return _runViewCache;
        }

        private void MarkRunViewDirty()
        {
            _runViewDirty = true;
            _runViewCache.valid = false;
        }

        private string BuildGuidanceText(PungentChecklistDefinition checklist, PungentChecklistUtilityStateService.StateSnapshot snapshot)
        {
            if (checklist == null || snapshot == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            foreach (PungentChecklistSectionDefinition section in checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null)
                    continue;

                bool needsFollowUp = false;
                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                {
                    if (item == null)
                        continue;

                    string stateId = snapshot.GetStateId(item);
                    PungentChecklistStateOptionDefinition state = snapshot.ResolveState(stateId);
                    if (state.requiresComment || snapshot.CountsAsProblem(stateId))
                    {
                        needsFollowUp = true;
                        break;
                    }
                }

                if (needsFollowUp && !string.IsNullOrWhiteSpace(section.guidancePrompt))
                    builder.AppendLine(section.guidancePrompt);
            }

            return builder.ToString().Trim();
        }

        private string BuildItemMetadataString(PungentChecklistItemDefinition item)
        {
            if (item == null)
                return string.Empty;

            List<string> metadata = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.linkedStateKey))
                metadata.Add("Linked state: " + LinkedStateLabel(item.linkedStateKey) + " (" + CountLinkedAppearances(item.linkedStateKey) + " appearances)");
            if (!string.IsNullOrWhiteSpace(item.ownerPackageId))
                metadata.Add("Owner: " + item.ownerPackageId);
            if (!string.IsNullOrWhiteSpace(item.canonicalOwnerPackageId) && !string.Equals(item.canonicalOwnerPackageId, item.ownerPackageId, StringComparison.OrdinalIgnoreCase))
                metadata.Add("Canonical owner: " + item.canonicalOwnerPackageId);
            if (item.appearsInPackageIds != null && item.appearsInPackageIds.Count > 0)
                metadata.Add("Appears in: " + string.Join(", ", item.appearsInPackageIds.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()));
            if (item.isOptional)
                metadata.Add("Optional child item");
            if (!string.IsNullOrWhiteSpace(item.priority))
                metadata.Add("Priority: " + item.priority);
            if (!string.IsNullOrWhiteSpace(item.owner))
                metadata.Add("Owner: " + item.owner);
            if (!string.IsNullOrWhiteSpace(item.dueUtc))
                metadata.Add("Due: " + item.dueUtc);
            if (item.archived)
                metadata.Add("Archived");

            return metadata.Count == 0 ? string.Empty : string.Join(" | ", metadata.ToArray());
        }

        private void EnsureDefinitionsLoaded(bool force = false)
        {
            if (!force && _checklists.Count > 0)
                return;

            _checklists.Clear();
            _selectedChecklistCache = null;
            _selectedChecklistCacheId = string.Empty;
            _checklists.AddRange(PungentChecklistDefinitionRegistry.AllDefinitions);
            RebuildOwnershipCache();

            LoadLegacyLocalDefinitions();
            foreach (string json in _legacyLocalJsonPresets)
            {
                if (PungentChecklistSerialization.TryParseChecklist(json, out PungentChecklistDefinition checklist, out _) &&
                    !_checklists.Any(item => item != null && string.Equals(item.checklistId, checklist.checklistId, StringComparison.OrdinalIgnoreCase)))
                {
                    checklist.schemaVersion = PungentChecklistConstants.CurrentSchemaVersion;
                    checklist.sourceProviderId = PungentChecklistConstants.LegacyLocalSourceProviderId;
                    checklist.sourceLabel = "Legacy editor-pref import";
                    checklist.NormalizeInPlace();
                    _checklists.Add(checklist);
                }
            }

            _linkedStateGroups.Clear();
            _linkedStateGroups.AddRange(PungentChecklistDefinitionRegistry.AllLinkedStateGroups);
            LoadLegacyLinkedStateGroups();
            HydrateLinkedStateGroupsFromDefinitions();

            if (!_checklists.Any(item => item != null && string.Equals(item.checklistId, _selectedChecklistId, StringComparison.OrdinalIgnoreCase)) && _checklists.Count > 0)
                _selectedChecklistId = _checklists[0].checklistId;

            LoadVisibleNotes();
            MarkRunViewDirty();
        }

        private void RebuildOwnershipCache()
        {
            _projectChecklistIds.Clear();
            _providerChecklistIds.Clear();

            foreach (PungentChecklistDefinition checklist in PungentChecklistDefinitionRegistry.ProjectDefinitions)
                if (checklist != null && !string.IsNullOrWhiteSpace(checklist.checklistId))
                    _projectChecklistIds.Add(checklist.checklistId);

            foreach (PungentChecklistDefinition checklist in PungentChecklistDefinitionRegistry.ProviderDefinitions)
                if (checklist != null && !string.IsNullOrWhiteSpace(checklist.checklistId))
                    _providerChecklistIds.Add(checklist.checklistId);
        }

        private void LoadLegacyLocalDefinitions()
        {
            _legacyLocalJsonPresets.Clear();
            string encoded = UtilityWindowPrefs.GetString(PrefImportedJson, string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
                return;

            string[] entries = encoded.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                try
                {
                    string json = Encoding.UTF8.GetString(Convert.FromBase64String(entries[i]));
                    if (!string.IsNullOrWhiteSpace(json))
                        _legacyLocalJsonPresets.Add(json);
                }
                catch
                {
                    // Ignore malformed persisted entries. Import validation handles user-facing errors.
                }
            }
        }

        private void SaveLegacyLocalDefinitions()
        {
            List<string> encoded = new List<string>();
            for (int i = 0; i < _legacyLocalJsonPresets.Count; i++)
            {
                string json = _legacyLocalJsonPresets[i];
                if (!string.IsNullOrWhiteSpace(json))
                    encoded.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
            }

            UtilityWindowPrefs.SetString(PrefImportedJson, string.Join("|", encoded.ToArray()));
        }

        private void RemoveLegacyLocalChecklist(string checklistId)
        {
            _legacyLocalJsonPresets.RemoveAll(json =>
            {
                if (!PungentChecklistSerialization.TryParseChecklist(json, out PungentChecklistDefinition existing, out _))
                    return false;
                return string.Equals(existing.checklistId, checklistId, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void LoadLegacyLinkedStateGroups()
        {
            string json = UtilityWindowPrefs.GetString(PrefLinkedStateGroups, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                PungentLinkedStateGroupList list = JsonUtility.FromJson<PungentLinkedStateGroupList>(json);
                MergeLinkedStateGroups(list == null ? null : list.groups);
            }
            catch
            {
                // Ignore malformed persisted linked group metadata. Item-level linkedStateKey still works.
            }
        }

        private void MergeLinkedStateGroups(IEnumerable<PungentLinkedStateGroupDefinition> groups)
        {
            if (groups == null)
                return;

            foreach (PungentLinkedStateGroupDefinition group in groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.linkedStateKey))
                    continue;

                int existing = _linkedStateGroups.FindIndex(item => item != null && string.Equals(item.linkedStateKey, group.linkedStateKey, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                    _linkedStateGroups[existing] = group;
                else
                    _linkedStateGroups.Add(group);
            }
        }

        private void HydrateLinkedStateGroupsFromDefinitions()
        {
            foreach (PungentChecklistDefinition checklist in _checklists)
            {
                foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.linkedStateKey))
                        continue;

                    if (_linkedStateGroups.Any(group => group != null && string.Equals(group.linkedStateKey, item.linkedStateKey, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _linkedStateGroups.Add(new PungentLinkedStateGroupDefinition
                    {
                        linkedStateKey = item.linkedStateKey,
                        label = item.label,
                        ownerPackageId = string.IsNullOrWhiteSpace(item.canonicalOwnerPackageId) ? item.ownerPackageId : item.canonicalOwnerPackageId,
                        sourceProviderId = checklist.sourceProviderId,
                        sourceItemId = checklist.sourceItemId
                    });
                }
            }
        }

        private void LoadVisibleNotes()
        {
            _notes.Clear();
            foreach (PungentChecklistDefinition checklist in _checklists)
            {
                foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
                    _notes[NoteCacheKey(checklist, item.id)] = PungentChecklistUtilityStateService.GetComment(checklist, item.id);
            }
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSelectedChecklist, _selectedChecklistId ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefSearch, _search ?? string.Empty);
            UtilityWindowPrefs.SetInt(PrefFilter, (int)_filterMode);
            UtilityWindowPrefs.SetInt(PrefMode, (int)_mode);
            UtilityWindowPrefs.SetString(PrefStateFilter, _stateFilter ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefSectionFilter, _sectionFilter ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefOwnerFilter, _ownerFilter ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefPriorityFilter, _priorityFilter ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefDueFilter, _dueFilter ?? string.Empty);
            UtilityWindowPrefs.SetBool(PrefShowArchived, _showArchived);
        }

        private void QueuePrefsSave()
        {
            _prefsSaveQueued = true;
            _nextPrefsSaveAt = EditorApplication.timeSinceStartup + TextPersistenceDebounceSeconds;
        }

        private void QueueCommentSave(PungentChecklistDefinition checklist, string itemId, string comment)
        {
            _pendingCommentSaves[NoteKey(checklist, itemId)] = comment ?? string.Empty;
            _commentSaveQueued = true;
            _nextCommentSaveAt = EditorApplication.timeSinceStartup + TextPersistenceDebounceSeconds;
        }

        private void HandleEditorUpdate()
        {
            FlushPendingTextPersistence(false);
        }

        private void FlushPendingTextPersistence(bool force)
        {
            double now = EditorApplication.timeSinceStartup;
            if (_prefsSaveQueued && (force || now >= _nextPrefsSaveAt))
            {
                SavePrefs();
                _prefsSaveQueued = false;
            }

            if (_commentSaveQueued && (force || now >= _nextCommentSaveAt))
            {
                foreach (KeyValuePair<string, string> pair in _pendingCommentSaves)
                    UtilityWindowPrefs.SetString(pair.Key, pair.Value);
                _pendingCommentSaves.Clear();
                _commentSaveQueued = false;
            }
        }

        private void SelectChecklist(string checklistId)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
                return;

            FlushPendingTextPersistence(true);
            PungentChecklistDefinition target = FindChecklist(checklistId);
            if (target == null)
            {
                _status = "Checklist '" + checklistId + "' is not available.";
                return;
            }

            _selectedChecklistId = checklistId;
            _selectedChecklistCache = target;
            _selectedChecklistCacheId = target.checklistId;
            _editSession.Clear();
            _editListTextCache.Clear();
            SavePrefs();
            LoadVisibleNotes();
            MarkRunViewDirty();
            Repaint();
        }

        private PungentChecklistItemState GetState(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item)
        {
            return PungentChecklistUtilityStateService.GetCompatibilityState(checklist, item, FindChecklist);
        }

        private PungentChecklistItemState GetState(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, HashSet<string> childStack)
        {
            return PungentChecklistProfiles.ToCompatibilityState(PungentChecklistUtilityStateService.GetStateId(checklist, item, FindChecklist, childStack));
        }

        private string GetStateId(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item)
        {
            return PungentChecklistUtilityStateService.GetStateId(checklist, item, FindChecklist);
        }

        private PungentChecklistItemState GetChildChecklistState(PungentChecklistItemDefinition item, HashSet<string> childStack)
        {
            string childId = item == null ? string.Empty : item.childChecklistId;
            if (string.IsNullOrWhiteSpace(childId))
                return PungentChecklistItemState.Untested;

            if (childStack == null)
                childStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!childStack.Add(childId))
                return PungentChecklistItemState.Fail;

            try
            {
                PungentChecklistDefinition child = FindChecklist(childId);
                if (child == null)
                    return PungentChecklistItemState.Fail;

                return AggregateChecklistState(child, childStack, EffectiveChildPassRule(item));
            }
            finally
            {
                childStack.Remove(childId);
            }
        }

        private PungentChecklistItemState AggregateChecklistState(PungentChecklistDefinition checklist, HashSet<string> childStack, string childPassRule)
        {
            List<PungentChecklistItemDefinition> requiredItems = PungentChecklistSerialization.EnumerateItems(checklist).Where(item => item != null && !item.isOptional).ToList();
            if (requiredItems.Count == 0)
                return PungentChecklistItemState.Untested;

            int pass = 0;
            int partial = 0;
            int fail = 0;
            int untested = 0;
            foreach (PungentChecklistItemDefinition childItem in requiredItems)
            {
                PungentChecklistItemState state = GetState(checklist, childItem, childStack);
                switch (state)
                {
                    case PungentChecklistItemState.Pass:
                        pass++;
                        break;
                    case PungentChecklistItemState.Partial:
                        partial++;
                        break;
                    case PungentChecklistItemState.Fail:
                        fail++;
                        break;
                    default:
                        untested++;
                        break;
                }
            }

            if (!string.Equals(childPassRule, PungentChecklistConstants.ChildPassRuleAllRequiredPass, StringComparison.OrdinalIgnoreCase))
                childPassRule = PungentChecklistConstants.ChildPassRuleAllRequiredPass;

            if (fail > 0)
                return PungentChecklistItemState.Fail;
            if (pass == requiredItems.Count)
                return PungentChecklistItemState.Pass;
            if (pass > 0 || partial > 0)
                return PungentChecklistItemState.Partial;
            if (untested > 0)
                return PungentChecklistItemState.Untested;
            return PungentChecklistItemState.Untested;
        }

        private void SetState(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, PungentChecklistItemState state)
        {
            SetStateId(checklist, item, PungentChecklistProfiles.ToStateId(state));
        }

        private void SetStateId(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, string stateId)
        {
            PungentChecklistUtilityStateService.SetStateId(checklist, item, stateId);
            MarkRunViewDirty();
            Repaint();
        }

        private string GetNote(PungentChecklistDefinition checklist, string itemId)
        {
            string key = NoteCacheKey(checklist, itemId);
            if (_notes.TryGetValue(key, out string note))
                return note ?? string.Empty;
            note = PungentChecklistUtilityStateService.GetComment(checklist, itemId);
            _notes[key] = note ?? string.Empty;
            return note ?? string.Empty;
        }

        private void SetNote(PungentChecklistDefinition checklist, string itemId, string note)
        {
            SetNote(checklist, FindChecklistItem(checklist, itemId), note);
        }

        private void SetNote(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, string note)
        {
            if (checklist == null || item == null)
                return;

            string itemId = item.id;
            string key = NoteCacheKey(checklist, itemId);
            string value = note ?? string.Empty;
            _notes[key] = value;
            if (_runViewCache.valid)
                _runViewCache.notesByItem[item] = value;
            QueueCommentSave(checklist, itemId, value);
            if (!string.IsNullOrWhiteSpace(_search))
                MarkRunViewDirty();
            Repaint();
        }

        private string BuildResultsText(PungentChecklistDefinition checklist)
        {
            FlushPendingTextPersistence(true);
            return PungentChecklistUtilityStateService.BuildResultsText(checklist, FindChecklist);
        }

        private string BuildGuidanceText(PungentChecklistDefinition checklist)
        {
            FlushPendingTextPersistence(true);
            return PungentChecklistUtilityStateService.BuildGuidanceText(checklist, FindChecklist);
        }

        private string JoinIds(PungentChecklistDefinition checklist, PungentChecklistItemState state, bool includeNotes = false)
        {
            return PungentChecklistUtilityStateService.JoinIds(checklist, PungentChecklistProfiles.ToStateId(state), FindChecklist, includeNotes);
        }

        private string SectionPill(PungentChecklistDefinition checklist, PungentChecklistSectionDefinition section)
        {
            return SectionPill(checklist, section, new PungentChecklistUtilityStateService.StateSnapshot(checklist, FindChecklist));
        }

        private string SectionPill(PungentChecklistDefinition checklist, PungentChecklistSectionDefinition section, PungentChecklistUtilityStateService.StateSnapshot snapshot)
        {
            int complete = 0;
            int total = 0;
            foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
            {
                if (item == null || (!_showArchived && item.archived))
                    continue;

                total++;
                string stateId = snapshot == null ? GetStateId(checklist, item) : snapshot.GetStateId(item);
                PungentChecklistStateOptionDefinition state = snapshot == null ? PungentChecklistProfiles.ResolveState(checklist, stateId) : snapshot.ResolveState(stateId);
                if (state.countsAsComplete)
                    complete++;
            }

            int open = Mathf.Max(0, total - complete);
            return complete + "/" + total + " complete" + (open > 0 ? " | " + open + " open" : string.Empty);
        }

        private bool ShouldShow(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item)
        {
            return ShouldShow(checklist, item, null, null, null);
        }

        private bool ShouldShow(
            PungentChecklistDefinition checklist,
            PungentChecklistItemDefinition item,
            PungentChecklistUtilityStateService.StateSnapshot snapshot,
            string stateId,
            string note)
        {
            if (checklist == null || item == null)
                return false;
            if (item.archived && !_showArchived)
                return false;

            if (string.IsNullOrWhiteSpace(stateId))
                stateId = snapshot == null ? GetStateId(checklist, item) : snapshot.GetStateId(item);
            PungentChecklistStateOptionDefinition state = snapshot == null ? PungentChecklistProfiles.ResolveState(checklist, stateId) : snapshot.ResolveState(stateId);
            switch (_filterMode)
            {
                case FilterMode.Open:
                    if (state.countsAsComplete)
                        return false;
                    break;
                case FilterMode.Pass:
                    if (!state.countsAsComplete)
                        return false;
                    break;
                case FilterMode.Partial:
                    if (!string.Equals(state.semanticRole, PungentChecklistStateRoles.Partial, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(state.semanticRole, PungentChecklistStateRoles.Active, StringComparison.OrdinalIgnoreCase))
                        return false;
                    break;
                case FilterMode.Fail:
                    if (!PungentChecklistProfiles.CountsAsProblem(checklist, stateId))
                        return false;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_stateFilter) && !string.Equals(stateId, _stateFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrWhiteSpace(_sectionFilter) && !SectionContainsItem(checklist, _sectionFilter, item))
                return false;
            if (!string.IsNullOrWhiteSpace(_ownerFilter) && !ContainsIgnoreCase(item.owner, _ownerFilter) && !ContainsIgnoreCase(item.ownerPackageId, _ownerFilter))
                return false;
            if (!string.IsNullOrWhiteSpace(_priorityFilter) && !ContainsIgnoreCase(item.priority, _priorityFilter))
                return false;
            if (!string.IsNullOrWhiteSpace(_dueFilter) && !ContainsIgnoreCase(item.dueUtc, _dueFilter))
                return false;

            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string search = _search.Trim();
            return item.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.detail ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.linkedStateKey ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.ownerPackageId ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.owner ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.priority ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.dueUtc ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (item.childChecklistId ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (note ?? GetNote(checklist, item.id)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsIgnoreCase(string source, string search)
        {
            return (source ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool SectionContainsItem(PungentChecklistDefinition checklist, string sectionId, PungentChecklistItemDefinition item)
        {
            if (checklist == null || item == null || string.IsNullOrWhiteSpace(sectionId))
                return true;

            return (checklist.sections ?? new List<PungentChecklistSectionDefinition>()).Any(section =>
                section != null &&
                string.Equals(section.id, sectionId, StringComparison.OrdinalIgnoreCase) &&
                (section.items ?? new List<PungentChecklistItemDefinition>()).Contains(item));
        }

        private void CountStatuses(PungentChecklistDefinition checklist, out int pass, out int partial, out int fail, out int untested, out int total)
        {
            pass = 0;
            partial = 0;
            fail = 0;
            untested = 0;
            total = 0;

            foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
            {
                total++;
                switch (GetState(checklist, item))
                {
                    case PungentChecklistItemState.Pass:
                        pass++;
                        break;
                    case PungentChecklistItemState.Partial:
                        partial++;
                        break;
                    case PungentChecklistItemState.Fail:
                        fail++;
                        break;
                    default:
                        untested++;
                        break;
                }
            }
        }

        private PungentChecklistDefinition FindChecklist(string checklistId)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
                return null;

            return _checklists.FirstOrDefault(item => item != null && string.Equals(item.checklistId, checklistId, StringComparison.OrdinalIgnoreCase));
        }

        private static PungentChecklistItemDefinition FindChecklistItem(PungentChecklistDefinition checklist, string itemId)
        {
            if (checklist == null || string.IsNullOrWhiteSpace(itemId))
                return null;

            return PungentChecklistSerialization.EnumerateItems(checklist)
                .FirstOrDefault(item => item != null && string.Equals(item.id, itemId, StringComparison.OrdinalIgnoreCase));
        }

        private void CollectChecklistAndChildren(PungentChecklistDefinition checklist, List<PungentChecklistDefinition> results, HashSet<string> visited)
        {
            if (checklist == null || results == null || visited == null || !visited.Add(checklist.checklistId))
                return;

            results.Add(checklist);
            foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
            {
                if (!IsComputedFromChild(item) || string.IsNullOrWhiteSpace(item.childChecklistId))
                    continue;

                CollectChecklistAndChildren(FindChecklist(item.childChecklistId), results, visited);
            }
        }

        private string LinkedStateLabel(string linkedStateKey)
        {
            PungentLinkedStateGroupDefinition group = _linkedStateGroups.FirstOrDefault(item => item != null && string.Equals(item.linkedStateKey, linkedStateKey, StringComparison.OrdinalIgnoreCase));
            return group == null || string.IsNullOrWhiteSpace(group.label) ? linkedStateKey : group.label;
        }

        private int CountLinkedAppearances(string linkedStateKey)
        {
            int count = 0;
            foreach (PungentChecklistDefinition checklist in _checklists)
            {
                count += PungentChecklistSerialization.EnumerateItems(checklist)
                    .Count(item => item != null && string.Equals(item.linkedStateKey, linkedStateKey, StringComparison.OrdinalIgnoreCase));
            }
            return count;
        }

        private void HandleRegistryChanged()
        {
            EnsureDefinitionsLoaded(true);
            Repaint();
        }

        private static bool IsComputedFromChild(PungentChecklistItemDefinition item)
        {
            return item != null &&
                   !string.IsNullOrWhiteSpace(item.childChecklistId) &&
                   string.Equals(item.parentStateMode, PungentChecklistConstants.ParentStateComputedFromChild, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLegacyLocalChecklist(PungentChecklistDefinition checklist)
        {
            return checklist != null && string.Equals(checklist.sourceProviderId, PungentChecklistConstants.LegacyLocalSourceProviderId, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsProjectChecklist(PungentChecklistDefinition checklist)
        {
            return checklist != null && _projectChecklistIds.Contains(checklist.checklistId ?? string.Empty);
        }

        private bool IsProviderChecklist(PungentChecklistDefinition checklist)
        {
            return checklist != null && _providerChecklistIds.Contains(checklist.checklistId ?? string.Empty);
        }

        private static string EffectiveChildPassRule(PungentChecklistItemDefinition item)
        {
            return item == null || string.IsNullOrWhiteSpace(item.childPassRule)
                ? PungentChecklistConstants.ChildPassRuleAllRequiredPass
                : item.childPassRule;
        }

        private static string StateKey(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item)
        {
            return PungentChecklistUtilityStateService.StateKey(checklist, item);
        }

        private static string NoteKey(PungentChecklistDefinition checklist, string itemId)
        {
            return PungentChecklistUtilityStateService.NoteKey(checklist, itemId);
        }

        private static string NoteCacheKey(PungentChecklistDefinition checklist, string itemId)
        {
            return PungentChecklistUtilityStateService.NoteCacheKey(checklist, itemId);
        }

        private static string SafeKey(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string SafeControlName(string value)
        {
            string clean = SafeKey(value);
            if (string.IsNullOrWhiteSpace(clean))
                return "unnamed";

            StringBuilder builder = new StringBuilder(clean.Length);
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return builder.ToString();
        }

        private static Color FilterFullTint(Color tint)
        {
            Color color = tint;
            color.a = 1f;
            float luminance = Luminance(color);
            if (EditorGUIUtility.isProSkin && luminance < 0.24f)
                color = Color.Lerp(color, Color.white, 0.18f);
            else if (!EditorGUIUtility.isProSkin && luminance > 0.78f)
                color = Color.Lerp(color, Color.black, 0.14f);
            color.a = 1f;
            return color;
        }

        private static Color FilterDimTint(Color tint)
        {
            Color baseColor = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.13f, 0.15f, 1f)
                : new Color(0.82f, 0.84f, 0.88f, 1f);
            Color color = Color.Lerp(baseColor, FilterFullTint(tint), EditorGUIUtility.isProSkin ? 0.34f : 0.42f);
            color.a = 1f;
            return color;
        }

        private static Color ReadableOn(Color color)
        {
            return Luminance(color) > 0.56f
                ? new Color(0.08f, 0.09f, 0.11f, 1f)
                : new Color(0.96f, 0.97f, 0.99f, 1f);
        }

        private static float Luminance(Color color)
        {
            return (0.2126f * color.r) + (0.7152f * color.g) + (0.0722f * color.b);
        }

        private static void DrawFilterBox(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, fill);
            DrawFilterBoxBorder(rect, border);
        }

        private static void DrawFilterBoxBorder(Rect rect, Color border)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }

        private static Color StateColor(PungentChecklistItemState state)
        {
            switch (state)
            {
                case PungentChecklistItemState.Pass: return new Color(0.36f, 0.78f, 0.42f, 1f);
                case PungentChecklistItemState.Partial: return new Color(0.95f, 0.72f, 0.28f, 1f);
                case PungentChecklistItemState.Fail: return new Color(0.86f, 0.36f, 0.30f, 1f);
                default: return Color.white;
            }
        }

        private static Color StateColor(PungentChecklistStateOptionDefinition state)
        {
            if (state == null)
                return Color.white;

            switch (PungentChecklistThemeTokens.Normalize(state.themeToken))
            {
                case PungentChecklistThemeTokens.Green: return UtilityWindowTheme.Green;
                case PungentChecklistThemeTokens.Amber: return UtilityWindowTheme.Amber;
                case PungentChecklistThemeTokens.Red: return UtilityWindowTheme.Red;
                case PungentChecklistThemeTokens.Blue: return UtilityWindowTheme.Blue;
                case PungentChecklistThemeTokens.Cyan: return UtilityWindowTheme.Cyan;
                case PungentChecklistThemeTokens.Purple: return UtilityWindowTheme.Purple;
                default:
                    if (state.countsAsComplete)
                        return UtilityWindowTheme.Green;
                    if (state.countsAsFailure)
                        return UtilityWindowTheme.Red;
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static bool InvokeStatic(string typeName, string methodName)
        {
            Type type = FindType(typeName);
            MethodInfo method = type == null ? null : type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                return false;

            method.Invoke(null, null);
            return true;
        }

        private static bool InvokeStatic(string typeName, string methodName, params object[] args)
        {
            Type type = FindType(typeName);
            MethodInfo method = type == null ? null : type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                return false;

            method.Invoke(null, args);
            return true;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private sealed class RunSectionView
        {
            public PungentChecklistSectionDefinition section;
            public string pill = string.Empty;
            public readonly List<PungentChecklistItemDefinition> items = new List<PungentChecklistItemDefinition>();
        }

        private sealed class FilterToggleSpec
        {
            public GUIContent content;
            public Color tint;
            public bool isChecked;
            public bool fullColor;
            public Action action;
            public float width;

            public FilterToggleSpec(GUIContent content, Color tint, bool isChecked, bool fullColor, Action action, float width)
            {
                this.content = content ?? GUIContent.none;
                this.tint = tint;
                this.isChecked = isChecked;
                this.fullColor = fullColor;
                this.action = action;
                this.width = width;
            }
        }

        private sealed class RunViewCache
        {
            public bool valid;
            public string checklistId = string.Empty;
            public bool showArchived;
            public FilterMode filterMode;
            public string search = string.Empty;
            public string stateFilter = string.Empty;
            public string sectionFilter = string.Empty;
            public string ownerFilter = string.Empty;
            public string priorityFilter = string.Empty;
            public string dueFilter = string.Empty;
            public string guidance = string.Empty;
            public PungentChecklistProgressSummary summary = new PungentChecklistProgressSummary();
            public PungentChecklistUtilityStateService.StateSnapshot snapshot;
            public readonly List<RunSectionView> sections = new List<RunSectionView>();
            public readonly Dictionary<PungentChecklistItemDefinition, string> stateIdsByItem = new Dictionary<PungentChecklistItemDefinition, string>();
            public readonly Dictionary<PungentChecklistItemDefinition, PungentChecklistStateOptionDefinition> statesByItem = new Dictionary<PungentChecklistItemDefinition, PungentChecklistStateOptionDefinition>();
            public readonly Dictionary<PungentChecklistItemDefinition, string> notesByItem = new Dictionary<PungentChecklistItemDefinition, string>();
            public readonly Dictionary<PungentChecklistItemDefinition, string> metadataByItem = new Dictionary<PungentChecklistItemDefinition, string>();

            public IReadOnlyList<PungentChecklistStateOptionDefinition> StateOptions => snapshot == null
                ? Array.Empty<PungentChecklistStateOptionDefinition>()
                : snapshot.StateOptions;

            public bool Matches(PungentChecklistDefinition checklist, bool currentShowArchived, FilterMode currentFilterMode, string currentSearch, string currentStateFilter, string currentSectionFilter, string currentOwnerFilter, string currentPriorityFilter, string currentDueFilter)
            {
                return valid &&
                       checklist != null &&
                       string.Equals(checklistId, checklist.checklistId, StringComparison.OrdinalIgnoreCase) &&
                       showArchived == currentShowArchived &&
                       filterMode == currentFilterMode &&
                       string.Equals(search, currentSearch ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(stateFilter, currentStateFilter ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(sectionFilter, currentSectionFilter ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(ownerFilter, currentOwnerFilter ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(priorityFilter, currentPriorityFilter ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(dueFilter, currentDueFilter ?? string.Empty, StringComparison.Ordinal);
            }

            public string GetStateId(PungentChecklistItemDefinition item)
            {
                return item != null && stateIdsByItem.TryGetValue(item, out string stateId)
                    ? stateId
                    : snapshot == null ? string.Empty : snapshot.GetStateId(item);
            }

            public PungentChecklistStateOptionDefinition GetState(PungentChecklistItemDefinition item)
            {
                if (item != null && statesByItem.TryGetValue(item, out PungentChecklistStateOptionDefinition state))
                    return state;

                string stateId = GetStateId(item);
                return snapshot == null ? null : snapshot.ResolveState(stateId);
            }

            public string GetNote(PungentChecklistItemDefinition item)
            {
                return item != null && notesByItem.TryGetValue(item, out string note) ? note : string.Empty;
            }

            public void Clear()
            {
                valid = false;
                checklistId = string.Empty;
                showArchived = false;
                filterMode = FilterMode.All;
                search = string.Empty;
                stateFilter = string.Empty;
                sectionFilter = string.Empty;
                ownerFilter = string.Empty;
                priorityFilter = string.Empty;
                dueFilter = string.Empty;
                guidance = string.Empty;
                summary = new PungentChecklistProgressSummary();
                snapshot = null;
                sections.Clear();
                stateIdsByItem.Clear();
                statesByItem.Clear();
                notesByItem.Clear();
                metadataByItem.Clear();
            }
        }

        private enum FilterMode
        {
            All = 0,
            Open = 1,
            Pass = 2,
            Partial = 3,
            Fail = 4
        }

        private enum WorkbenchMode
        {
            RunList = 0,
            Edit = 1
        }
    }
#endif
}
