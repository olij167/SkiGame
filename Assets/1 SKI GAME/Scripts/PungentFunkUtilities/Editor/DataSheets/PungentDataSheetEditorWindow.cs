using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public sealed class PungentDataSheetEditorWindow : EditorWindow
    {
        private enum InspectorPane
        {
            Selection = 0,
            Bindings = 1,
            ApplyPreview = 2
        }

        private enum DataSheetOverlayKind
        {
            None = 0,
            CsvImport = 10,
            CsvExport = 20,
            HiddenItems = 30,
            BindingDraft = 40
        }

        private enum BindingDraftCommitMode
        {
            MergeIntoCurrentSheet = 0,
            CreateNewSheet = 10
        }

        private const string PrefPrefix = "PungentFunkUtilities.DataSheets.";
        private const string PrefSelectedSheet = PrefPrefix + "SelectedSheet";
        private const string PrefRightWidth = PrefPrefix + "RightWidth";
        private const string PrefGridFilter = PrefPrefix + "GridFilter";
        private const string PrefSelectedRow = PrefPrefix + "SelectedRow";
        private const string PrefSelectedColumn = PrefPrefix + "SelectedColumn";
        private const string PrefSelectedCellRow = PrefPrefix + "SelectedCellRow";
        private const string PrefSelectedCellColumn = PrefPrefix + "SelectedCellColumn";
        private const string PrefGridScrollX = PrefPrefix + "GridScrollX";
        private const string PrefGridScrollY = PrefPrefix + "GridScrollY";
        private const string PrefShowInspector = PrefPrefix + "ShowInspector";
        private const string PrefSerializedPropertyPath = PrefPrefix + "SerializedPropertyPath";
        private const string PrefClosedSheetIds = PrefPrefix + "ClosedSheetIds";
        private const string PrefDateDisplayFormat = PrefPrefix + "DateDisplayFormat";
        private const string SheetNoteStableKeyPrefix = "data-sheet:";
        private const float MinCenterWidth = 360f;
        private const float MinRightWidth = 270f;
        private const float SheetTabStripHeight = 42f;
        private const float SheetTabBodyHeight = 24f;
        private const float SheetTabScrollbarHeight = 12f;
        private const float SheetTabMinWidth = 58f;
        private const float SheetTabNaturalMinWidth = 86f;
        private const float SheetTabMaxWidth = 190f;
        private const float SheetTabSpacing = 2f;
        private const int MaxAuthoringPickerItems = 600;
        private const int MaxVisibleAuthoringPickerItems = 80;
        private static readonly Color SheetTabStripTint = new Color(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color SheetTabLaneTint = new Color(0.11f, 0.12f, 0.13f, 1f);
        private static readonly Color SheetTabHoverTint = new Color(0.22f, 0.24f, 0.26f, 1f);
        private static readonly Color SheetTabSelectedTint = new Color(0.26f, 0.31f, 0.34f, 1f);

        private sealed class AuthoringPickerCandidate
        {
            public PungentAuthoringMetadata metadata;
            public string providerId;
            public string providerName;
            public string searchText;
        }

        private readonly PungentDataSheetGridState _gridState = new PungentDataSheetGridState();
        private readonly List<AuthoringPickerCandidate> _authoringPickerCandidates = new List<AuthoringPickerCandidate>();
        private readonly HashSet<string> _closedSheetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly PungentAuthoringGuidedBindingState _bindingEndpointPickerState = new PungentAuthoringGuidedBindingState();
        private readonly PungentAuthoringGuidedBindingState _bindingDraftEndpointPickerState = new PungentAuthoringGuidedBindingState();
        private Vector2 _sheetTabsScroll;
        private Vector2 _inspectorScroll;
        private Vector2 _authoringPickerScroll;
        private Vector2 _hiddenItemsScroll;
        private Vector2 _bindingDraftRowsScroll;
        private Vector2 _bindingDraftColumnsScroll;
        private string _selectedSheetId = string.Empty;
        private string _renamingSheetId = string.Empty;
        private string _renamingSheetTitle = string.Empty;
        private string _serializedPropertyPath = string.Empty;
        private string _authoringPickerSearch = string.Empty;
        private string _authoringPickerStatus = "Picker not refreshed.";
        private string _pendingSheetTabDragId = string.Empty;
        private string _draggingSheetTabId = string.Empty;
        private string _bindingEndpointPickerColumnId = string.Empty;
        private string _bindingEndpointPickerStatus = "Choose a column field with a sample target.";
        private string _bindingDraftStatus = "Generate a draft from explicit targets.";
        private string _bindingDraftSearch = string.Empty;
        private string _bindingDraftRemapColumnId = string.Empty;
        private string _advancedCellRefKey = string.Empty;
        private string _advancedCellRefProvider = string.Empty;
        private string _advancedCellRefCustomKind = string.Empty;
        private string _advancedCellRefItemId = string.Empty;
        private string _advancedCellRefLabel = string.Empty;
        private string _status = "Ready.";
        private string _hoveredSheetTabLabel = string.Empty;
        private int _pendingSheetTabDragIndex = -1;
        private int _draggingSheetTabToIndex = -1;
        private float _rightWidth = 330f;
        private bool _showInspector;
        private bool _authoringPickerLoaded;
        private DataSheetOverlayKind _activeOverlay = DataSheetOverlayKind.None;
        private bool _csvImportIntoCurrent;
        private bool _csvImportFirstRowIsHeaders = true;
        private bool _csvImportSaveBefore = true;
        private bool _csvExportVisibleColumnsOnly = true;
        private bool _csvExportIncludeHiddenRows;
        private bool _csvExportDisplayValues;
        private bool _showCellAuthoringPicker;
        private bool _showAdvancedCellRawRef;
        private Vector2 _sheetTabDragStartMouse;
        private Rect _hoveredSheetTabRect;
        private Rect _overlayAnchorRect;
        private Rect _activeOverlayRect;
        private PungentAuthoringBindingDraft _bindingDraft;
        private PungentDataSheetBindingTargetSource _bindingDraftSource = PungentDataSheetBindingTargetSource.ManualObjects;
        private BindingDraftCommitMode _bindingDraftCommitMode = BindingDraftCommitMode.MergeIntoCurrentSheet;
        private string _csvImportPath = string.Empty;
        private string _csvImportPreviewSummary = string.Empty;
        private bool _csvImportPreviewValid;
        private PungentDataSheetCsvUtility.ColumnMappingMode _csvImportColumnMode = PungentDataSheetCsvUtility.ColumnMappingMode.MatchHeaders;
        private InspectorPane _inspectorPane = InspectorPane.Selection;
        private PungentDataSheetApplyScope _applyScope = PungentDataSheetApplyScope.ChangedCells;
        private PungentDataSheetConflictPolicy _conflictPolicy = PungentDataSheetConflictPolicy.Overwrite;
        private PungentAuthoringItemKind _advancedCellRefKind = PungentAuthoringItemKind.LegacyNote;
        private readonly List<PungentDataSheetApplyPreview> _applyPreview = new List<PungentDataSheetApplyPreview>();
        private PungentAuthoringValidationResult _lastValidation;

        public static void Open()
        {
            PungentDataSheetEditorWindow window = GetWindow<PungentDataSheetEditorWindow>("Data Sheets");
            window.minSize = new Vector2(900f, 480f);
            window.Show();
        }

        public static void OpenAndSelect(string sheetId)
        {
            PungentDataSheetEditorWindow window = GetWindow<PungentDataSheetEditorWindow>("Data Sheets");
            window.minSize = new Vector2(900f, 480f);
            window.Show();
            window.Focus();
            window.SelectSheet(sheetId);
        }

        public static void CreateChecklistDefinitionSheet()
        {
            PungentDataSheetEditorWindow window = GetWindow<PungentDataSheetEditorWindow>("Data Sheets");
            window.minSize = new Vector2(900f, 480f);
            window.Show();
            window.Focus();
            window.CreateSheetFromTemplate(PungentDataSheetTemplates.GetChecklistDefinitionTemplate());
        }

        public static void CreateChecklistDefinitionSheetFromChecklist(string checklistId)
        {
            PungentDataSheetEditorWindow window = GetWindow<PungentDataSheetEditorWindow>("Data Sheets");
            window.minSize = new Vector2(900f, 480f);
            window.Show();
            window.Focus();
            if (PungentDataSheetChecklistBridge.TryCreateSheetFromChecklist(checklistId, out PungentDataSheet sheet, out string error))
            {
                window.SelectSheet(sheet.id);
                window.MarkDirty("Created checklist sheet for '" + checklistId + "'.", true);
                return;
            }

            window._status = "Checklist sheet not created: " + error;
            EditorUtility.DisplayDialog("Create Checklist Sheet", error, "OK");
        }

        private PungentDataSheet SelectedSheet => IsSheetClosed(_selectedSheetId) ? null : PungentDataSheetEditorStorage.Database.FindSheet(_selectedSheetId);

        private void OnEnable()
        {
            titleContent = new GUIContent("Data Sheets");
            minSize = new Vector2(900f, 480f);
            wantsMouseMove = true;
            _selectedSheetId = UtilityWindowPrefs.GetString(PrefSelectedSheet, string.Empty);
            _rightWidth = UtilityWindowPrefs.GetFloat(PrefRightWidth, 330f);
            _gridState.filterText = UtilityWindowPrefs.GetString(PrefGridFilter, string.Empty);
            _gridState.selectedRowId = UtilityWindowPrefs.GetString(PrefSelectedRow, string.Empty);
            _gridState.selectedColumnId = UtilityWindowPrefs.GetString(PrefSelectedColumn, string.Empty);
            _gridState.selectedCellRowId = UtilityWindowPrefs.GetString(PrefSelectedCellRow, string.Empty);
            _gridState.selectedCellColumnId = UtilityWindowPrefs.GetString(PrefSelectedCellColumn, string.Empty);
            _gridState.scroll = new Vector2(
                UtilityWindowPrefs.GetFloat(PrefGridScrollX, 0f),
                UtilityWindowPrefs.GetFloat(PrefGridScrollY, 0f));
            _showInspector = UtilityWindowPrefs.GetBool(PrefShowInspector, false);
            _serializedPropertyPath = UtilityWindowPrefs.GetString(PrefSerializedPropertyPath, string.Empty);
            _gridState.dateDisplayFormat = UtilityWindowPrefs.GetString(PrefDateDisplayFormat, "dd/MM/yyyy");
            DecodeClosedSheets(UtilityWindowPrefs.GetString(PrefClosedSheetIds, string.Empty));
            PungentDataSheetProviderRegistration.RegisterProvider();
            PungentDataSheetEditorStorage.EnsureLoaded();
            AssemblyReloadEvents.beforeAssemblyReload -= SaveBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += SaveBeforeReload;
        }

        private void OnDisable()
        {
            SaveBeforeReload();
            SavePrefs();
            AssemblyReloadEvents.beforeAssemblyReload -= SaveBeforeReload;
        }

        private void OnGUI()
        {
            PungentDataSheetEditorStorage.EnsureLoaded();
            EnsureSelection();
            ClampPanelWidths();

            PungentDataSheet sheet = SelectedSheet;
            DrawWorkbookToolbar(sheet);
            DrawFormulaBar(sheet);
            HandleActiveOverlayInput();

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawGridPanel(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (_showInspector)
                {
                    UtilityWindowTheme.HorizontalResizeHandle(ref _rightWidth, MinRightWidth, MaxRightWidth(), SavePrefs, "Drag to resize inspector", true);
                    DrawInspector(GUILayout.Width(_rightWidth), GUILayout.ExpandHeight(true));
                }
            }

            DrawSheetTabs();
            DrawOverlayTrays(sheet);
        }

        private void DrawWorkbookToolbar(PungentDataSheet sheet)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("File", "New, duplicate, delete, import, and export sheets."), EditorStyles.toolbarDropDown, GUILayout.Width(48f)))
                    ShowSheetMenu();

                DrawSheetDropdown(sheet);

                GUI.enabled = sheet != null;
                if (GUILayout.Button(new GUIContent("Save", "Validate and save Data Sheet storage."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                    SaveCurrentSheet();
                if (GUILayout.Button(new GUIContent("+ Row", "Add a row."), EditorStyles.toolbarButton, GUILayout.Width(56f)))
                    AddRow();
                if (GUILayout.Button(new GUIContent("+ Column", "Add a column."), EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    AddColumn();
                if (GUILayout.Button(new GUIContent("Validate", "Run sheet-local validation."), EditorStyles.toolbarButton, GUILayout.Width(68f)))
                    ValidateCurrentSheet(true);
                GUI.enabled = true;

                if (GUILayout.Button(new GUIContent("Hidden", "Recover hidden rows and columns."), EditorStyles.toolbarButton, GUILayout.Width(58f)))
                    OpenDataSheetOverlay(DataSheetOverlayKind.HiddenItems, GUILayoutUtility.GetLastRect());

                if (GUILayout.Button(new GUIContent("Checklist", "Open the shared Checklist Utility with the Data Sheet checklist selected."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    PungentChecklistUtilityWindow.OpenDataSheetCurrentQa();

                GUILayout.Space(8f);
                if (GUILayout.Button(new GUIContent("Import CSV", "Import a CSV file into a new sheet or append it into the current sheet."), EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    OpenDataSheetOverlay(DataSheetOverlayKind.CsvImport, GUILayoutUtility.GetLastRect());

                GUI.enabled = sheet != null;
                if (GUILayout.Button(new GUIContent("Export CSV", "Export the current sheet to CSV."), EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    OpenDataSheetOverlay(DataSheetOverlayKind.CsvExport, GUILayoutUtility.GetLastRect());
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
                string storageState = !string.IsNullOrWhiteSpace(PungentDataSheetEditorStorage.LastError)
                    ? PungentDataSheetEditorStorage.LastError
                    : PungentDataSheetEditorStorage.Dirty ? "Unsaved" : "Saved";
                GUILayout.Label(storageState, EditorStyles.miniLabel, GUILayout.Width(Mathf.Clamp(storageState.Length * 7f + 20f, 70f, 240f)));

                if (GUILayout.Button(new GUIContent("Sticky", "Open Sticky Notes for quick contextual notes."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                    OpenStickyNotes();

                GUI.enabled = sheet != null;
                if (GUILayout.Button(new GUIContent("Sheet Sticky", "Open or create a linked sticky note for this sheet."), EditorStyles.toolbarButton, GUILayout.Width(86f)))
                    OpenOrCreateSheetNote(sheet);
                GUI.enabled = true;

                bool nextInspector = GUILayout.Toggle(_showInspector, new GUIContent("Inspector", "Show row, column, cell, and project utility tools."), EditorStyles.toolbarButton, GUILayout.Width(72f));
                if (nextInspector != _showInspector)
                {
                    _showInspector = nextInspector;
                    if (_showInspector)
                        _inspectorPane = InspectorPane.Selection;
                    SavePrefs();
                }

                GUI.enabled = sheet != null;
                if (GUILayout.Button(new GUIContent("Bindings", "Configure project-object pull and preview/apply bindings."), EditorStyles.toolbarButton, GUILayout.Width(68f)))
                {
                    _showInspector = true;
                    _inspectorPane = InspectorPane.Bindings;
                    SavePrefs();
                }
                GUI.enabled = true;
            }
        }

        private void DrawOverlayTrays(PungentDataSheet sheet)
        {
            if (_activeOverlay == DataSheetOverlayKind.None)
                return;

            DrawCsvSettingsTray(sheet);
            DrawHiddenItemsTray(sheet);
            DrawBindingDraftTray(sheet);
        }

        private void OpenDataSheetOverlay(DataSheetOverlayKind kind, Rect anchorRect)
        {
            FlushGridEdit();
            _activeOverlay = kind;
            _overlayAnchorRect = anchorRect;
            _activeOverlayRect = Rect.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void CloseDataSheetOverlay()
        {
            _activeOverlay = DataSheetOverlayKind.None;
            _overlayAnchorRect = Rect.zero;
            _activeOverlayRect = Rect.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void HandleActiveOverlayInput()
        {
            if (_activeOverlay == DataSheetOverlayKind.None)
                return;

            CalculateActiveOverlayRect();
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                CloseDataSheetOverlay();
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown &&
                !_activeOverlayRect.Contains(evt.mousePosition) &&
                !_overlayAnchorRect.Contains(evt.mousePosition))
            {
                CloseDataSheetOverlay();
                evt.Use();
            }
        }

        private Rect CalculateActiveOverlayRect()
        {
            switch (_activeOverlay)
            {
                case DataSheetOverlayKind.CsvImport:
                    return CalculateOverlayRect(252f, 660f, 52f);
                case DataSheetOverlayKind.CsvExport:
                    return CalculateOverlayRect(138f, 660f, 52f);
                case DataSheetOverlayKind.HiddenItems:
                    return CalculateOverlayRect(Mathf.Min(330f, Mathf.Max(190f, position.height - 150f)), 620f, 52f);
                case DataSheetOverlayKind.BindingDraft:
                    return CalculateOverlayRect(Mathf.Min(560f, Mathf.Max(320f, position.height - 110f)), 1040f, 52f);
                default:
                    _activeOverlayRect = Rect.zero;
                    return _activeOverlayRect;
            }
        }

        private Rect CalculateOverlayRect(float height, float maxWidth, float defaultTop)
        {
            float width = Mathf.Clamp(position.width - 48f, 380f, maxWidth);
            float x = _overlayAnchorRect.width > 0f
                ? _overlayAnchorRect.center.x - width * 0.5f
                : (position.width - width) * 0.5f;
            x = Mathf.Clamp(x, 12f, Mathf.Max(12f, position.width - width - 12f));

            float y = _overlayAnchorRect.height > 0f
                ? Mathf.Max(defaultTop, _overlayAnchorRect.yMax + 4f)
                : defaultTop;
            y = Mathf.Min(y, Mathf.Max(defaultTop, position.height - height - 44f));

            _activeOverlayRect = new Rect(x, y, width, height);
            return _activeOverlayRect;
        }

        private static void DrawOverlayChrome(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.26f));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 0.98f));
        }

        private void DrawCsvSettingsTray(PungentDataSheet sheet)
        {
            if (_activeOverlay != DataSheetOverlayKind.CsvImport && _activeOverlay != DataSheetOverlayKind.CsvExport)
                return;

            Rect trayRect = CalculateOverlayRect(_activeOverlay == DataSheetOverlayKind.CsvImport ? 252f : 138f, 660f, 52f);
            DrawOverlayChrome(trayRect);
            GUILayout.BeginArea(trayRect, EditorStyles.helpBox);
            if (_activeOverlay == DataSheetOverlayKind.CsvImport)
                DrawImportSettingsTray(sheet);
            if (_activeOverlay == DataSheetOverlayKind.CsvExport)
                DrawExportSettingsTray(sheet);
            GUILayout.EndArea();
        }

        private void DrawImportSettingsTray(PungentDataSheet sheet)
        {
            EditorGUILayout.LabelField("CSV Import Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _csvImportIntoCurrent = EditorGUILayout.ToggleLeft(new GUIContent("Append into current sheet", "When disabled, import creates a new sheet."), _csvImportIntoCurrent && sheet != null);
            _csvImportFirstRowIsHeaders = EditorGUILayout.ToggleLeft("First row contains headers", _csvImportFirstRowIsHeaders);
            using (new EditorGUI.DisabledScope(!_csvImportIntoCurrent || sheet == null))
                _csvImportColumnMode = (PungentDataSheetCsvUtility.ColumnMappingMode)EditorGUILayout.EnumPopup(new GUIContent("Column Mapping", "Match by header, reuse current columns by position, or always create new columns."), _csvImportColumnMode);
            using (new EditorGUI.DisabledScope(!PungentDataSheetEditorStorage.Dirty))
                _csvImportSaveBefore = EditorGUILayout.ToggleLeft("Save pending Data Sheet storage before import", _csvImportSaveBefore);
            if (EditorGUI.EndChangeCheck())
                ClearCsvImportPreview();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _csvImportPath = EditorGUILayout.TextField("CSV File", _csvImportPath ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                    ClearCsvImportPreview();
                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    string path = EditorUtility.OpenFilePanel("Import CSV as Data Sheet", string.Empty, "csv");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        _csvImportPath = path;
                        ClearCsvImportPreview();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_csvImportPreviewSummary))
                EditorGUILayout.HelpBox(_csvImportPreviewSummary, _csvImportPreviewValid ? MessageType.Info : MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_csvImportPath) || (_csvImportIntoCurrent && sheet == null)))
                {
                    if (GUILayout.Button("Preview Mapping", GUILayout.Width(122f)))
                        PreviewCsvImport(sheet);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(82f)))
                    CloseDataSheetOverlay();
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_csvImportPath) || (_csvImportIntoCurrent && sheet == null)))
                {
                    if (GUILayout.Button(_csvImportIntoCurrent ? "Append CSV" : "Import New Sheet", GUILayout.Width(122f)))
                        ExecuteCsvImport(sheet);
                }
            }
        }

        private void DrawExportSettingsTray(PungentDataSheet sheet)
        {
            EditorGUILayout.LabelField("CSV Export Settings", EditorStyles.boldLabel);
            _csvExportVisibleColumnsOnly = EditorGUILayout.ToggleLeft("Visible columns only", _csvExportVisibleColumnsOnly);
            _csvExportIncludeHiddenRows = EditorGUILayout.ToggleLeft("Include hidden rows", _csvExportIncludeHiddenRows);
            _csvExportDisplayValues = EditorGUILayout.ToggleLeft("Use display values instead of raw values", _csvExportDisplayValues);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(82f)))
                    CloseDataSheetOverlay();
                using (new EditorGUI.DisabledScope(sheet == null))
                {
                    if (GUILayout.Button("Export CSV", GUILayout.Width(96f)))
                        ExecuteCsvExport(sheet);
                }
            }
        }

        private void ShowHiddenItemsTray()
        {
            OpenDataSheetOverlay(DataSheetOverlayKind.HiddenItems, Rect.zero);
        }

        private void DrawHiddenItemsTray(PungentDataSheet sheet)
        {
            if (_activeOverlay != DataSheetOverlayKind.HiddenItems)
                return;

            Rect trayRect = CalculateOverlayRect(Mathf.Min(330f, Mathf.Max(190f, position.height - 150f)), 620f, 52f);
            DrawOverlayChrome(trayRect);
            GUILayout.BeginArea(trayRect, EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Hidden Rows / Columns", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", EditorStyles.miniButton, GUILayout.Width(62f)))
                    CloseDataSheetOverlay();
            }

            if (sheet == null)
            {
                EditorGUILayout.HelpBox("Select a sheet to recover hidden rows or columns.", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            List<PungentDataSheetRow> hiddenRows = (sheet.rows ?? new List<PungentDataSheetRow>())
                .Where(row => row != null && row.hidden)
                .OrderBy(row => row.order)
                .ToList();
            List<PungentDataSheetColumn> hiddenColumns = (sheet.columns ?? new List<PungentDataSheetColumn>())
                .Where(column => column != null && column.hidden)
                .ToList();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(hiddenRows.Count == 0))
                {
                    if (GUILayout.Button("Unhide All Rows", EditorStyles.miniButton))
                        UnhideRows(sheet, hiddenRows);
                }

                using (new EditorGUI.DisabledScope(hiddenColumns.Count == 0))
                {
                    if (GUILayout.Button("Unhide All Columns", EditorStyles.miniButton))
                        UnhideColumns(sheet, hiddenColumns);
                }
            }

            if (hiddenRows.Count == 0 && hiddenColumns.Count == 0)
            {
                EditorGUILayout.HelpBox("No hidden rows or columns in the selected sheet.", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            _hiddenItemsScroll = EditorGUILayout.BeginScrollView(_hiddenItemsScroll, GUILayout.ExpandHeight(true));
            DrawHiddenRowsList(sheet, hiddenRows);
            DrawHiddenColumnsList(sheet, hiddenColumns);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawBindingDraftTray(PungentDataSheet sheet)
        {
            if (_activeOverlay != DataSheetOverlayKind.BindingDraft)
                return;

            Rect trayRect = CalculateOverlayRect(Mathf.Min(560f, Mathf.Max(320f, position.height - 110f)), 1040f, 52f);
            DrawOverlayChrome(trayRect);
            GUILayout.BeginArea(trayRect, EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Binding Draft", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", EditorStyles.miniButton, GUILayout.Width(62f)))
                    CloseDataSheetOverlay();
            }

            if (_bindingDraft == null)
            {
                EditorGUILayout.HelpBox("Generate a draft from Selection, cached targets, or an explicit asset folder refresh. Drafts do not change the sheet until Confirm.", MessageType.Info);
                if (!string.IsNullOrWhiteSpace(_bindingDraftStatus))
                    EditorGUILayout.LabelField(_bindingDraftStatus, UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.EndArea();
                return;
            }

            _bindingDraft.NormalizeInPlace();
            DrawBindingDraftHeader(sheet);

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                DrawBindingDraftRowsPane();
                DrawBindingDraftColumnsPane();
            }

            if (!string.IsNullOrWhiteSpace(_bindingDraftRemapColumnId))
                DrawBindingDraftColumnRemapPicker();

            GUILayout.EndArea();
        }

        private void DrawBindingDraftHeader(PungentDataSheet sheet)
        {
            int includedRows = (_bindingDraft.rows ?? new List<PungentAuthoringBindingDraftRow>()).Count(row => row != null && row.include);
            int includedColumns = (_bindingDraft.columns ?? new List<PungentAuthoringBindingDraftColumn>()).Count(column => column != null && column.include);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    includedRows + "/" + (_bindingDraft.rows?.Count ?? 0) + " row(s), " +
                    includedColumns + "/" + (_bindingDraft.columns?.Count ?? 0) + " column(s)",
                    UtilityWindowTheme.MutedMiniLabelStyle,
                    GUILayout.MinWidth(180f));
                GUILayout.Label("Filter", GUILayout.Width(38f));
                _bindingDraftSearch = EditorGUILayout.TextField(_bindingDraftSearch ?? string.Empty);
                _bindingDraftCommitMode = (BindingDraftCommitMode)EditorGUILayout.EnumPopup(_bindingDraftCommitMode, GUILayout.Width(178f));
                using (new EditorGUI.DisabledScope(includedRows == 0 || includedColumns == 0 || (sheet == null && _bindingDraftCommitMode == BindingDraftCommitMode.MergeIntoCurrentSheet)))
                {
                    if (GUILayout.Button("Confirm", EditorStyles.miniButton, GUILayout.Width(82f)))
                        CommitBindingDraft(sheet);
                }
            }

            if (!string.IsNullOrWhiteSpace(_bindingDraftStatus))
                EditorGUILayout.LabelField(_bindingDraftStatus, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawBindingDraftRowsPane()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(Mathf.Max(260f, _activeOverlayRect.width * 0.34f)), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                        SetBindingDraftRowsIncluded(true);
                    if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(48f)))
                        SetBindingDraftRowsIncluded(false);
                }

                _bindingDraftRowsScroll = EditorGUILayout.BeginScrollView(_bindingDraftRowsScroll, GUILayout.ExpandHeight(true));
                foreach (PungentAuthoringBindingDraftRow row in _bindingDraft.rows ?? new List<PungentAuthoringBindingDraftRow>())
                {
                    if (row == null || !BindingDraftMatchesFilter(row.displayName, row.target?.rawValue, row.target?.contextId))
                        continue;

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        row.include = EditorGUILayout.Toggle(row.include, GUILayout.Width(18f));
                        using (new EditorGUI.DisabledScope(!row.include))
                        {
                            row.pullEnabled = GUILayout.Toggle(row.pullEnabled, new GUIContent("Pull", "Create/update this row and read enabled column values during Pull Rows."), EditorStyles.miniButtonLeft, GUILayout.Width(46f));
                            row.pushEnabled = GUILayout.Toggle(row.pushEnabled, new GUIContent("Apply", "Allow this row to contribute values during Apply Preview."), EditorStyles.miniButtonRight, GUILayout.Width(52f));
                        }

                        GUILayout.Label(new GUIContent(string.IsNullOrWhiteSpace(row.displayName) ? "Target" : row.displayName, DraftTargetTooltip(row)), UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBindingDraftColumnsPane()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Columns", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                        SetBindingDraftColumnsIncluded(true);
                    if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(48f)))
                        SetBindingDraftColumnsIncluded(false);
                }

                _bindingDraftColumnsScroll = EditorGUILayout.BeginScrollView(_bindingDraftColumnsScroll, GUILayout.ExpandHeight(true));
                foreach (PungentAuthoringBindingDraftColumn column in _bindingDraft.columns ?? new List<PungentAuthoringBindingDraftColumn>())
                {
                    if (column == null || !BindingDraftMatchesFilter(column.displayName, column.path?.propertyPath, column.valueType.ToString()))
                        continue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            column.include = EditorGUILayout.Toggle(column.include, GUILayout.Width(18f));
                            GUILayout.Label(new GUIContent(string.IsNullOrWhiteSpace(column.displayName) ? "Field" : column.displayName, DraftColumnTooltip(column)), EditorStyles.boldLabel, GUILayout.MinWidth(150f));
                            GUILayout.Label(column.valueType.ToString(), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(96f));
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Remap", EditorStyles.miniButton, GUILayout.Width(62f)))
                            {
                                _bindingDraftRemapColumnId = column.id;
                                if (_bindingDraftEndpointPickerState.targetObject == null)
                                    _bindingDraftEndpointPickerState.targetObject = Selection.activeObject;
                                _bindingDraftEndpointPickerState.selectedGroupLabel = string.Empty;
                                _bindingDraftEndpointPickerState.selectedEndpointId = string.Empty;
                            }
                        }

                        using (new EditorGUI.DisabledScope(!column.include))
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            column.pullEnabled = GUILayout.Toggle(column.pullEnabled, new GUIContent("Pull", "Read this field into the sheet during Pull Rows."), EditorStyles.miniButtonLeft, GUILayout.Width(54f));
                            column.pushEnabled = GUILayout.Toggle(column.pushEnabled, new GUIContent("Apply", "Include this column in Apply Preview."), EditorStyles.miniButtonRight, GUILayout.Width(58f));
                            GUILayout.Label(column.path == null ? string.Empty : column.path.propertyPath, UtilityWindowTheme.MutedMiniLabelStyle);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBindingDraftColumnRemapPicker()
        {
            PungentAuthoringBindingDraftColumn column = (_bindingDraft.columns ?? new List<PungentAuthoringBindingDraftColumn>())
                .FirstOrDefault(item => item != null && PungentAuthoringId.EqualsId(item.id, _bindingDraftRemapColumnId));
            if (column == null)
            {
                _bindingDraftRemapColumnId = string.Empty;
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Remap: " + (string.IsNullOrWhiteSpace(column.displayName) ? column.id : column.displayName), EditorStyles.boldLabel);
                    if (GUILayout.Button("Close", EditorStyles.miniButton, GUILayout.Width(62f)))
                        _bindingDraftRemapColumnId = string.Empty;
                }

                PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
                {
                    contextLabel = string.Empty,
                    objectLabel = "Sample Target",
                    componentLabel = "Component",
                    endpointLabel = "Field",
                    bindButtonLabel = "Use In Draft",
                    helpText = string.Empty,
                    showHeader = false,
                    showHelp = false
                };
                PungentAuthoringGuidedBindingResult result = PungentAuthoringGuidedBindingView.Draw(_bindingDraftEndpointPickerState, options);
                if (result.bindClicked && result.selectedEndpoint != null && result.selectedEndpoint.CanBind)
                {
                    PungentAuthoringBindingPath path = PungentAuthoringBindingDiscoveryService.CreatePath(result.selectedEndpoint, "DataSheetBindingDraft");
                    if (path != null)
                    {
                        column.id = path.id;
                        column.displayName = string.IsNullOrWhiteSpace(path.displayName) ? result.selectedEndpoint.label : path.displayName;
                        column.path = path;
                        column.valueType = path.valueType == PungentAuthoringBindingValueType.Unknown
                            ? PungentAuthoringBindingDiscoveryService.ToRuntimeValueType(result.selectedEndpoint)
                            : path.valueType;
                        _bindingDraft.NormalizeInPlace();
                        _bindingDraftStatus = "Remapped draft column to " + column.displayName + ".";
                        _bindingDraftRemapColumnId = string.Empty;
                    }
                }
            }
        }

        private bool BindingDraftMatchesFilter(string a, string b, string c)
        {
            string filter = (_bindingDraftSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return ContainsFilter(a, filter) || ContainsFilter(b, filter) || ContainsFilter(c, filter);
        }

        private static bool ContainsFilter(string value, string filter)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DraftTargetTooltip(PungentAuthoringBindingDraftRow row)
        {
            if (row == null || row.target == null)
                return string.Empty;
            return row.target.targetKind + "\n" + row.target.rawValue + "\n" + row.target.contextId;
        }

        private static string DraftColumnTooltip(PungentAuthoringBindingDraftColumn column)
        {
            if (column == null || column.path == null)
                return string.Empty;
            return column.path.adapterDisplayName + "\n" + column.path.propertyPath + "\n" + column.path.id;
        }

        private void SetBindingDraftRowsIncluded(bool included)
        {
            foreach (PungentAuthoringBindingDraftRow row in _bindingDraft?.rows ?? new List<PungentAuthoringBindingDraftRow>())
                if (row != null)
                    row.include = included;
        }

        private void SetBindingDraftColumnsIncluded(bool included)
        {
            foreach (PungentAuthoringBindingDraftColumn column in _bindingDraft?.columns ?? new List<PungentAuthoringBindingDraftColumn>())
                if (column != null)
                    column.include = included;
        }

        private void DrawHiddenRowsList(PungentDataSheet sheet, List<PungentDataSheetRow> rows)
        {
            EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
            if (rows == null || rows.Count == 0)
            {
                EditorGUILayout.LabelField("No hidden rows.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            foreach (PungentDataSheetRow row in rows)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(row.displayName) ? row.id : row.displayName, GUILayout.MinWidth(120f));
                    EditorGUILayout.LabelField("Order " + row.order.ToString(), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(64f));
                    if (GUILayout.Button("Unhide", EditorStyles.miniButton, GUILayout.Width(64f)))
                        UnhideRows(sheet, new List<PungentDataSheetRow> { row });
                }
            }
        }

        private void DrawHiddenColumnsList(PungentDataSheet sheet, List<PungentDataSheetColumn> columns)
        {
            EditorGUILayout.LabelField("Columns", EditorStyles.boldLabel);
            if (columns == null || columns.Count == 0)
            {
                EditorGUILayout.LabelField("No hidden columns.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            foreach (PungentDataSheetColumn column in columns)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(column.displayName) ? column.id : column.displayName, GUILayout.MinWidth(120f));
                    EditorGUILayout.LabelField(column.dataType.ToString(), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(90f));
                    if (GUILayout.Button("Unhide", EditorStyles.miniButton, GUILayout.Width(64f)))
                        UnhideColumns(sheet, new List<PungentDataSheetColumn> { column });
                }
            }
        }

        private void UnhideRows(PungentDataSheet sheet, List<PungentDataSheetRow> rows)
        {
            if (sheet == null || rows == null || rows.Count == 0)
                return;

            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null)
                    rows[i].hidden = false;
            sheet.Touch();
            _gridState.InvalidateCache();
            MarkDirty("Unhid " + rows.Count + " row(s).", true);
        }

        private void UnhideColumns(PungentDataSheet sheet, List<PungentDataSheetColumn> columns)
        {
            if (sheet == null || columns == null || columns.Count == 0)
                return;

            for (int i = 0; i < columns.Count; i++)
                if (columns[i] != null)
                    columns[i].hidden = false;
            sheet.Touch();
            _gridState.InvalidateCache();
            MarkDirty("Unhid " + columns.Count + " column(s).", true);
        }

        private void DrawSheetDropdown(PungentDataSheet selected)
        {
            string label = selected == null ? "No Sheet" : string.IsNullOrWhiteSpace(selected.title) ? "Untitled Sheet" : selected.title;
            if (!PungentDataSheetEditorStorage.Database.sheets.Any(sheet => sheet != null))
            {
                if (GUILayout.Button(new GUIContent("Create Sheet", "Create a blank sheet."), EditorStyles.toolbarButton, GUILayout.Width(104f)))
                    CreateSheetFromTemplate(PungentDataSheetTemplates.GetTemplates()[0]);
                return;
            }

            if (GUILayout.Button(new GUIContent(label, selected != null ? selected.id : string.Empty), EditorStyles.toolbarDropDown, GUILayout.MinWidth(160f), GUILayout.MaxWidth(280f)))
            {
                GenericMenu menu = new GenericMenu();
                foreach (PungentDataSheet sheet in PungentDataSheetEditorStorage.Database.sheets.Where(item => item != null))
                {
                    PungentDataSheet captured = sheet;
                    menu.AddItem(new GUIContent(string.IsNullOrWhiteSpace(captured.title) ? "Untitled Sheet" : captured.title), PungentAuthoringId.EqualsId(captured.id, _selectedSheetId), () => SelectSheet(captured.id));
                }

                menu.DropDown(GUILayoutUtility.GetLastRect());
            }
        }

        private void ShowSheetMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentDataSheetTemplateDefinition template in PungentDataSheetTemplates.GetTemplates())
            {
                PungentDataSheetTemplateDefinition captured = template;
                if (captured.enabled)
                    menu.AddItem(new GUIContent("New/" + captured.title), false, () => CreateSheetFromTemplate(captured));
                else
                    menu.AddDisabledItem(new GUIContent("New/" + captured.title + " (Unavailable)"));
            }

            PungentDataSheet sheet = SelectedSheet;
            menu.AddSeparator(string.Empty);
            foreach (PungentDataSheet existing in PungentDataSheetEditorStorage.Database.sheets.Where(item => item != null))
            {
                PungentDataSheet captured = existing;
                string title = string.IsNullOrWhiteSpace(captured.title) ? "Untitled Sheet" : captured.title;
                menu.AddItem(new GUIContent("Open/" + title), PungentAuthoringId.EqualsId(captured.id, _selectedSheetId), () => SelectSheet(captured.id));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Import CSV..."), false, ImportCsv);
            if (sheet != null)
                menu.AddItem(new GUIContent("Export CSV..."), false, ExportCsv);
            else
                menu.AddDisabledItem(new GUIContent("Export CSV..."));

            menu.AddSeparator(string.Empty);
            AddDateFormatMenuItem(menu, "dd/MM/yyyy");
            AddDateFormatMenuItem(menu, "yyyy-MM-dd");
            AddDateFormatMenuItem(menu, "MM/dd/yyyy");

            menu.AddSeparator(string.Empty);
            if (sheet != null)
            {
                menu.AddItem(new GUIContent("View/Hidden Rows & Columns"), _activeOverlay == DataSheetOverlayKind.HiddenItems, ShowHiddenItemsTray);
                menu.AddSeparator("View/");
                menu.AddItem(new GUIContent("Checklist/Create Or Update Checklist Definition"), false, () => CreateOrUpdateChecklistDefinition(sheet));
                menu.AddItem(new GUIContent("Checklist/Validate Checklist Definition"), false, () => ValidateChecklistDefinition(sheet));
                menu.AddItem(new GUIContent("Checklist/Open Linked Checklist"), false, () => OpenLinkedChecklist(sheet));
                menu.AddItem(new GUIContent("Checklist/Sync Linked Checklist To Sheet"), false, () => SyncLinkedChecklistToSheet(sheet));
                menu.AddItem(new GUIContent("Checklist/Create Sheet From Linked Checklist"), false, () => CreateSheetFromLinkedChecklist(sheet));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Rename Sheet..."), false, () => BeginRenameSheet(sheet));
                menu.AddItem(new GUIContent("Close Current Sheet"), false, CloseCurrentSheet);
                menu.AddItem(new GUIContent("Close All Sheets"), false, CloseAllSheets);
                menu.AddItem(new GUIContent("Duplicate Sheet"), false, DuplicateCurrentSheet);
                menu.AddItem(new GUIContent("Copy Sheet ID"), false, () => EditorGUIUtility.systemCopyBuffer = sheet.id);
                menu.AddItem(new GUIContent("Open/Create Sheet Sticky Note"), false, () => OpenOrCreateSheetNote(sheet));
                menu.AddItem(new GUIContent("Delete Sheet..."), false, DeleteCurrentSheet);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Checklist/Create Or Update Checklist Definition"));
                menu.AddDisabledItem(new GUIContent("Checklist/Validate Checklist Definition"));
                menu.AddDisabledItem(new GUIContent("Checklist/Open Linked Checklist"));
                menu.AddDisabledItem(new GUIContent("Checklist/Sync Linked Checklist To Sheet"));
                menu.AddDisabledItem(new GUIContent("Checklist/Create Sheet From Linked Checklist"));
                menu.AddSeparator(string.Empty);
                menu.AddDisabledItem(new GUIContent("Rename Sheet..."));
                menu.AddDisabledItem(new GUIContent("Close Current Sheet"));
                if (PungentDataSheetEditorStorage.Database.sheets.Any(item => item != null && !IsSheetClosed(item.id)))
                    menu.AddItem(new GUIContent("Close All Sheets"), false, CloseAllSheets);
                else
                    menu.AddDisabledItem(new GUIContent("Close All Sheets"));
                menu.AddDisabledItem(new GUIContent("Duplicate Sheet"));
                menu.AddDisabledItem(new GUIContent("Copy Sheet ID"));
                menu.AddDisabledItem(new GUIContent("Open/Create Sheet Sticky Note"));
                menu.AddDisabledItem(new GUIContent("Delete Sheet..."));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Sticky Notes"), false, OpenStickyNotes);
            menu.DropDown(new Rect(4f, 18f, 0f, 0f));
        }

        private void AddDateFormatMenuItem(GenericMenu menu, string format)
        {
            menu.AddItem(new GUIContent("Date Format/" + format), string.Equals(_gridState.dateDisplayFormat, format, StringComparison.Ordinal), () =>
            {
                _gridState.dateDisplayFormat = format;
                SavePrefs();
                Repaint();
            });
        }

        private void DrawFormulaBar(PungentDataSheet sheet)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(new GUIContent(SelectedCellAddress(sheet), "Selected cell address."), EditorStyles.toolbarButton, GUILayout.Width(56f));

                bool hasCell = sheet != null && _gridState.HasCellSelection;
                EditorGUI.BeginDisabledGroup(!hasCell);
                string value = hasCell ? PungentDataSheetGridGUI.GetSelectedCellRawValue(sheet, _gridState) : string.Empty;
                EditorGUI.BeginChangeCheck();
                string next = EditorGUILayout.TextField(value, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && hasCell)
                {
                    if (PungentDataSheetGridGUI.CommitSelectedCellValue(sheet, _gridState, next, out PungentDataSheetCell committedCell))
                        OnSheetCellEdited(sheet, committedCell, "Cell edited.");
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawSheetTabs()
        {
            List<PungentDataSheet> visibleSheets = PungentDataSheetEditorStorage.Database.sheets
                .Where(item => item != null && !IsSheetClosed(item.id))
                .ToList();

            _hoveredSheetTabLabel = string.Empty;
            _hoveredSheetTabRect = Rect.zero;

            Rect stripRect = GUILayoutUtility.GetRect(1f, SheetTabStripHeight, GUILayout.ExpandWidth(true), GUILayout.Height(SheetTabStripHeight));
            EditorGUI.DrawRect(stripRect, SheetTabStripTint);
            if (Event.current.type == EventType.MouseMove && stripRect.Contains(Event.current.mousePosition))
                Repaint();

            Rect addRect = new Rect(stripRect.x + 4f, stripRect.y + 5f, 28f, SheetTabBodyHeight);
            if (GUI.Button(addRect, new GUIContent("+", "Create a new blank sheet."), EditorStyles.toolbarButton))
                CreateSheetFromTemplate(PungentDataSheetTemplates.GetTemplates()[0]);

            float statusWidth = stripRect.width > 560f ? Mathf.Clamp(stripRect.width * 0.24f, 160f, 320f) : 0f;
            Rect statusRect = statusWidth > 0f
                ? new Rect(stripRect.xMax - statusWidth - 6f, stripRect.y + 6f, statusWidth, SheetTabBodyHeight)
                : Rect.zero;
            Rect laneRect = new Rect(addRect.xMax + 4f, stripRect.y + 4f, Mathf.Max(40f, (statusWidth > 0f ? statusRect.x : stripRect.xMax - 4f) - addRect.xMax - 8f), SheetTabBodyHeight);
            Rect scrollbarRect = new Rect(laneRect.x, laneRect.yMax + 2f, laneRect.width, SheetTabScrollbarHeight);

            DrawSheetTabLane(laneRect, scrollbarRect, visibleSheets);

            if (statusWidth > 0f)
                GUI.Label(statusRect, _status, EditorStyles.miniLabel);

            DrawHoveredSheetTabPopover(stripRect);
        }

        private void DrawSheetTabLane(Rect laneRect, Rect scrollbarRect, List<PungentDataSheet> visibleSheets)
        {
            EditorGUI.DrawRect(laneRect, SheetTabLaneTint);
            if (visibleSheets == null || visibleSheets.Count == 0)
            {
                GUI.Label(laneRect, new GUIContent("Open sheets from File/Open or create a new sheet.", "Closed sheets remain available from the File/Open menu."), EditorStyles.centeredGreyMiniLabel);
                _sheetTabsScroll.x = 0f;
                return;
            }

            List<float> widths = CalculateSheetTabWidths(visibleSheets, laneRect.width);
            float contentWidth = Mathf.Max(laneRect.width, widths.Sum() + Mathf.Max(0, widths.Count - 1) * SheetTabSpacing);
            float maxScroll = Mathf.Max(0f, contentWidth - laneRect.width);
            _sheetTabsScroll.x = Mathf.Clamp(_sheetTabsScroll.x, 0f, maxScroll);
            Event evt = Event.current;

            if (!string.IsNullOrWhiteSpace(_draggingSheetTabId) && evt.type == EventType.MouseDrag && laneRect.Contains(evt.mousePosition))
            {
                _draggingSheetTabToIndex = SheetTabIndexFromMouse(widths, evt.mousePosition.x - laneRect.x + _sheetTabsScroll.x);
                evt.Use();
            }

            GUI.BeginGroup(laneRect);
            float x = -_sheetTabsScroll.x;
            for (int i = 0; i < visibleSheets.Count; i++)
            {
                PungentDataSheet sheet = visibleSheets[i];
                float width = widths[i];
                Rect tabRect = new Rect(x, 1f, width, SheetTabBodyHeight - 1f);
                string label = string.IsNullOrWhiteSpace(sheet.title) ? "Untitled" : sheet.title;
                bool selected = PungentAuthoringId.EqualsId(sheet.id, _selectedSheetId);

                if (tabRect.xMax >= 0f && tabRect.x <= laneRect.width)
                    DrawSheetTab(sheet, label, selected, i, visibleSheets.Count, tabRect, laneRect, width < SheetTabNaturalMinWidth + 1f);

                x += width + SheetTabSpacing;
            }

            DrawSheetTabInsertionLine(widths);
            GUI.EndGroup();

            HandleSheetTabDragCompletion();

            if (contentWidth > laneRect.width + 0.5f)
                _sheetTabsScroll.x = GUI.HorizontalScrollbar(scrollbarRect, _sheetTabsScroll.x, laneRect.width, 0f, contentWidth);
            else
                _sheetTabsScroll.x = 0f;
        }

        private List<float> CalculateSheetTabWidths(List<PungentDataSheet> visibleSheets, float availableWidth)
        {
            List<float> widths = new List<float>();
            if (visibleSheets == null || visibleSheets.Count == 0)
                return widths;

            float naturalTotal = 0f;
            for (int i = 0; i < visibleSheets.Count; i++)
            {
                string label = string.IsNullOrWhiteSpace(visibleSheets[i].title) ? "Untitled" : visibleSheets[i].title;
                float width = Mathf.Clamp(EditorStyles.toolbarButton.CalcSize(new GUIContent(label)).x + 36f, SheetTabNaturalMinWidth, SheetTabMaxWidth);
                widths.Add(width);
                naturalTotal += width;
            }

            naturalTotal += Mathf.Max(0, visibleSheets.Count - 1) * SheetTabSpacing;
            if (naturalTotal <= availableWidth)
                return widths;

            float shared = Mathf.Clamp((availableWidth - Mathf.Max(0, visibleSheets.Count - 1) * SheetTabSpacing) / Mathf.Max(1, visibleSheets.Count), SheetTabMinWidth, SheetTabMaxWidth);
            for (int i = 0; i < widths.Count; i++)
                widths[i] = Mathf.Clamp(Mathf.Min(widths[i], shared), SheetTabMinWidth, SheetTabMaxWidth);
            return widths;
        }

        private int SheetTabIndexFromMouse(List<float> widths, float mouseX)
        {
            if (widths == null || widths.Count == 0)
                return 0;

            float x = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                float center = x + widths[i] * 0.5f;
                if (mouseX < center)
                    return i;
                x += widths[i] + SheetTabSpacing;
            }

            return widths.Count;
        }

        private void DrawSheetTab(PungentDataSheet sheet, string label, bool selected, int visibleIndex, int visibleCount, Rect tabRect, Rect laneRect, bool compact)
        {
            Rect closeRect = new Rect(tabRect.xMax - 18f, tabRect.y + 4f, 14f, tabRect.height - 8f);
            Event evt = Event.current;
            bool hovered = tabRect.Contains(evt.mousePosition);

            if (evt.type == EventType.MouseDrag && PungentAuthoringId.EqualsId(_pendingSheetTabDragId, sheet.id) &&
                Vector2.Distance(evt.mousePosition, _sheetTabDragStartMouse) > 4f)
            {
                _draggingSheetTabId = sheet.id;
                _draggingSheetTabToIndex = visibleIndex;
                _pendingSheetTabDragId = string.Empty;
                evt.Use();
            }

            if (!string.IsNullOrWhiteSpace(_draggingSheetTabId) && evt.type == EventType.MouseDrag && tabRect.Contains(evt.mousePosition))
            {
                _draggingSheetTabToIndex = evt.mousePosition.x < tabRect.center.x ? visibleIndex : visibleIndex + 1;
                evt.Use();
            }

            if (hovered && (compact || EditorStyles.toolbarButton.CalcSize(new GUIContent(label)).x + 36f > tabRect.width))
            {
                _hoveredSheetTabLabel = label;
                _hoveredSheetTabRect = new Rect(laneRect.x + tabRect.x, laneRect.y + tabRect.y, tabRect.width, tabRect.height);
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && closeRect.Contains(evt.mousePosition))
            {
                CloseSheet(sheet.id);
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && tabRect.Contains(evt.mousePosition))
            {
                if (evt.clickCount >= 2)
                {
                    BeginRenameSheet(sheet);
                    evt.Use();
                    return;
                }

                SelectSheet(sheet.id);
                _pendingSheetTabDragId = sheet.id;
                _pendingSheetTabDragIndex = visibleIndex;
                _sheetTabDragStartMouse = evt.mousePosition;
                evt.Use();
            }
            else if (((evt.type == EventType.MouseDown && evt.button == 1) || evt.type == EventType.ContextClick) && tabRect.Contains(evt.mousePosition))
            {
                SelectSheet(sheet.id);
                ShowSheetTabMenu(sheet);
                evt.Use();
            }

            bool dragging = PungentAuthoringId.EqualsId(_draggingSheetTabId, sheet.id);
            if (hovered && !selected && !dragging)
                EditorGUI.DrawRect(tabRect, SheetTabHoverTint);
            if (selected || dragging)
                EditorGUI.DrawRect(tabRect, SheetTabSelectedTint);

            if (PungentAuthoringId.EqualsId(_renamingSheetId, sheet.id))
            {
                GUI.SetNextControlName("DataSheetTabRename");
                EditorGUI.BeginChangeCheck();
                _renamingSheetTitle = GUI.TextField(new Rect(tabRect.x + 6f, tabRect.y + 3f, Mathf.Max(20f, tabRect.width - 26f), tabRect.height - 6f), _renamingSheetTitle ?? string.Empty, EditorStyles.toolbarTextField);
                if (EditorGUI.EndChangeCheck())
                {
                    sheet.title = string.IsNullOrWhiteSpace(_renamingSheetTitle) ? "Untitled Sheet" : _renamingSheetTitle.Trim();
                    sheet.Touch();
                    MarkDirty("Renamed sheet.", true);
                }

                if (evt.type == EventType.KeyDown &&
                    (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Escape))
                {
                    _renamingSheetId = string.Empty;
                    evt.Use();
                }
                return;
            }

            GUI.Toggle(tabRect, selected || dragging, new GUIContent(CompactTabLabel(label, tabRect.width), label + "\n" + sheet.id), EditorStyles.toolbarButton);
            GUI.Label(closeRect, new GUIContent("x", "Close this sheet tab without deleting it."), EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSheetTabInsertionLine(List<float> widths)
        {
            if (string.IsNullOrWhiteSpace(_draggingSheetTabId) || widths == null || _draggingSheetTabToIndex < 0 || _draggingSheetTabToIndex > widths.Count)
                return;

            float x = 0f;
            for (int i = 0; i < Mathf.Min(_draggingSheetTabToIndex, widths.Count); i++)
                x += widths[i] + SheetTabSpacing;
            if (_draggingSheetTabToIndex >= widths.Count && widths.Count > 0)
                x -= SheetTabSpacing;

            x -= _sheetTabsScroll.x;
            Handles.color = Color.white;
            Handles.DrawAAPolyLine(3f, new Vector3(x, 0f), new Vector3(x, SheetTabBodyHeight));
        }

        private void DrawHoveredSheetTabPopover(Rect stripRect)
        {
            if (string.IsNullOrWhiteSpace(_hoveredSheetTabLabel) || Event.current.type != EventType.Repaint)
                return;

            GUIContent content = new GUIContent(_hoveredSheetTabLabel);
            Vector2 size = EditorStyles.helpBox.CalcSize(content);
            size.x = Mathf.Clamp(size.x + 18f, 120f, 320f);
            size.y = 24f;
            Rect popupRect = new Rect(
                Mathf.Clamp(_hoveredSheetTabRect.center.x - size.x * 0.5f, stripRect.x + 4f, stripRect.xMax - size.x - 4f),
                Mathf.Max(0f, stripRect.y - size.y + 5f),
                size.x,
                size.y);
            EditorGUI.DrawRect(popupRect, SheetTabSelectedTint);
            GUI.Box(popupRect, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(popupRect.x + 8f, popupRect.y + 4f, popupRect.width - 16f, popupRect.height - 8f), content, EditorStyles.miniLabel);
        }

        private static string CompactTabLabel(string label, float width)
        {
            label = string.IsNullOrWhiteSpace(label) ? "Untitled" : label;
            if (width >= 96f || label.Length <= 6)
                return label;
            if (width <= 64f)
                return label.Substring(0, Mathf.Min(2, label.Length));
            return label.Substring(0, Mathf.Min(5, label.Length)) + "...";
        }

        private void HandleSheetTabDragCompletion()
        {
            Event evt = Event.current;
            if (evt == null)
                return;

            if ((evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp) && !string.IsNullOrWhiteSpace(_draggingSheetTabId))
            {
                MoveSheetToVisibleIndex(_draggingSheetTabId, _draggingSheetTabToIndex);
                ClearSheetTabDrag();
                evt.Use();
            }
            else if ((evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp) && !string.IsNullOrWhiteSpace(_pendingSheetTabDragId))
            {
                ClearSheetTabDrag();
            }
        }

        private void ClearSheetTabDrag()
        {
            _pendingSheetTabDragId = string.Empty;
            _draggingSheetTabId = string.Empty;
            _pendingSheetTabDragIndex = -1;
            _draggingSheetTabToIndex = -1;
            _sheetTabDragStartMouse = Vector2.zero;
        }

        private void MoveSheetToVisibleIndex(string sheetId, int targetVisibleIndex)
        {
            List<PungentDataSheet> sheets = PungentDataSheetEditorStorage.Database.sheets;
            if (sheets == null)
                return;

            int from = sheets.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, sheetId));
            if (from < 0)
                return;

            PungentDataSheet moving = sheets[from];
            sheets.RemoveAt(from);
            List<PungentDataSheet> openAfterRemove = sheets.Where(item => item != null && !IsSheetClosed(item.id)).ToList();
            targetVisibleIndex = Mathf.Clamp(targetVisibleIndex, 0, openAfterRemove.Count);
            int insertIndex = targetVisibleIndex >= openAfterRemove.Count
                ? sheets.Count
                : Mathf.Max(0, sheets.IndexOf(openAfterRemove[targetVisibleIndex]));
            sheets.Insert(insertIndex, moving);
            MarkDirty("Reordered sheet tabs.", true);
            SavePrefs();
            Repaint();
        }

        private void BeginRenameSheet(PungentDataSheet sheet)
        {
            if (sheet == null)
                return;

            SelectSheet(sheet.id);
            _renamingSheetId = sheet.id;
            _renamingSheetTitle = sheet.title ?? string.Empty;
            EditorGUI.FocusTextInControl("DataSheetTabRename");
            Repaint();
        }

        private void ShowSheetTabMenu(PungentDataSheet sheet)
        {
            GenericMenu menu = new GenericMenu();
            if (sheet == null)
                return;

            menu.AddItem(new GUIContent("Rename"), false, () => BeginRenameSheet(sheet));
            menu.AddItem(new GUIContent("Close Tab"), false, () => CloseSheet(sheet.id));
            menu.AddItem(new GUIContent("Duplicate"), false, () => { SelectSheet(sheet.id); DuplicateCurrentSheet(); });
            menu.AddItem(new GUIContent("Copy Sheet ID"), false, () => EditorGUIUtility.systemCopyBuffer = sheet.id);
            menu.AddItem(new GUIContent("Open/Create Sheet Sticky Note"), false, () => OpenOrCreateSheetNote(sheet));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, () => { SelectSheet(sheet.id); DeleteCurrentSheet(); });
            menu.ShowAsContext();
        }

        private void DrawGridPanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(options))
            {
                PungentDataSheet sheet = SelectedSheet;
                if (sheet == null)
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(360f)))
                        {
                            string help = PungentDataSheetEditorStorage.Database.sheets.Any(item => item != null)
                                ? "Open a sheet from the File/Open menu or sheet dropdown, or create/import another sheet."
                                : "Create or import a sheet to begin editing.";
                            EditorGUILayout.HelpBox(help, MessageType.Info);
                            if (GUILayout.Button("Create Blank Sheet", GUILayout.Height(26f)))
                                CreateSheetFromTemplate(PungentDataSheetTemplates.GetTemplates()[0]);
                            if (GUILayout.Button("Import CSV", GUILayout.Height(26f)))
                                ImportCsv();
                        }
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.FlexibleSpace();
                    return;
                }

                _gridState.inputBlocked = _activeOverlay != DataSheetOverlayKind.None;
                PungentDataSheetGridGUI.Draw(sheet, _gridState, cell => OnSheetCellEdited(sheet, cell, cell == null ? "Sheet edited." : "Cell edited."));
                _gridState.inputBlocked = false;
                DrawFooter(sheet);
            }
        }

        private void OnSheetCellEdited(PungentDataSheet sheet, PungentDataSheetCell cell, string dirtyMessage)
        {
            TryAutoApplyEditedCell(sheet, cell);
            MarkDirty(dirtyMessage, true);
        }

        private void DrawFooter(PungentDataSheet sheet)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                string counts = (sheet.rows != null ? sheet.rows.Count : 0) + " rows | " +
                                (sheet.columns != null ? sheet.columns.Count : 0) + " columns | " +
                                (sheet.cells != null ? sheet.cells.Count : 0) + " stored cells";
                EditorGUILayout.LabelField(counts, UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();
                string validation = _lastValidation == null ? "Validation not run" : _lastValidation.status + " / " + _lastValidation.issues.Count + " issue(s)";
                EditorGUILayout.LabelField(validation, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(180f));
            }
        }

        private void DrawInspector(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, options))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    {
                        _showInspector = false;
                        SavePrefs();
                        GUIUtility.ExitGUI();
                    }
                }
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll, GUILayout.ExpandHeight(true));

                PungentDataSheet sheet = SelectedSheet;
                if (sheet == null)
                {
                    EditorGUILayout.HelpBox("No sheet selected.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                DrawValidationSummary();
                DrawHiddenRecoveryShortcut(sheet);
                DrawInspectorPaneTabs();
                if (_inspectorPane == InspectorPane.Bindings)
                {
                    DrawBindingInspector(sheet);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                if (_inspectorPane == InspectorPane.ApplyPreview)
                {
                    DrawApplyPreviewInspector(sheet);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                PungentDataSheetCell selectedCell = _gridState.HasCellSelection ? sheet.FindCell(_gridState.selectedCellRowId, _gridState.selectedCellColumnId) : null;
                PungentDataSheetRow selectedRow = sheet.FindRow(_gridState.selectedRowId);
                PungentDataSheetColumn selectedColumn = sheet.FindColumn(_gridState.selectedColumnId);

                if (_gridState.HasCellSelection)
                    DrawCellInspector(sheet, selectedCell);
                else if (selectedRow != null)
                    DrawRowInspector(sheet, selectedRow);
                else if (selectedColumn != null)
                    DrawColumnInspector(sheet, selectedColumn);
                else
                    DrawSheetInspector(sheet);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawInspectorPaneTabs()
        {
            string[] labels = { "Selection", "Binding", "Apply" };
            EditorGUI.BeginChangeCheck();
            _inspectorPane = (InspectorPane)GUILayout.Toolbar((int)_inspectorPane, labels, EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
                SavePrefs();
            EditorGUILayout.Space(4f);
        }

        private void DrawHiddenRecoveryShortcut(PungentDataSheet sheet)
        {
            int hiddenRows = sheet?.rows == null ? 0 : sheet.rows.Count(row => row != null && row.hidden);
            int hiddenColumns = sheet?.columns == null ? 0 : sheet.columns.Count(column => column != null && column.hidden);
            if (hiddenRows == 0 && hiddenColumns == 0)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(hiddenRows + " hidden row(s), " + hiddenColumns + " hidden column(s)", UtilityWindowTheme.MutedMiniLabelStyle);
                if (GUILayout.Button("Recover", EditorStyles.miniButton, GUILayout.Width(68f)))
                    ShowHiddenItemsTray();
            }
        }

        private void DrawBindingInspector(PungentDataSheet sheet)
        {
            EditorGUILayout.LabelField("Binding", EditorStyles.boldLabel);
            if (sheet.bindingProfiles == null || sheet.bindingProfiles.Count == 0)
            {
                EditorGUILayout.HelpBox("Create a binding profile to pull values from selected project objects into rows, then preview/apply sheet values back through SerializedProperty paths.", MessageType.Info);
                if (GUILayout.Button("Create Binding Profile"))
                {
                    PungentDataSheetBindingProfile created = PungentDataSheetBindingProfile.Create("Default Binding");
                    sheet.bindingProfiles.Add(created);
                    sheet.activeBindingProfileId = created.id;
                    sheet.Touch();
                    MarkDirty("Created binding profile.", true);
                }

                return;
            }

            PungentDataSheetBindingProfile profile = DrawBindingProfileSelector(sheet);
            if (profile == null)
                return;

            EditorGUI.BeginChangeCheck();
            profile.displayName = EditorGUILayout.TextField("Name", profile.displayName);
            profile.targetSource = DrawTargetSourcePopup("Row Source", profile.targetSource);
            profile.targetTypeName = EditorGUILayout.TextField(new GUIContent("Target Type", "Optional type name or full name, such as MyStatsAsset or UnityEngine.MonoBehaviour."), profile.targetTypeName);
            profile.rowIdentityColumnId = DrawColumnIdPopup("Identity Column", sheet, profile.rowIdentityColumnId, true);
            profile.objectNameColumnId = DrawColumnIdPopup("Name Column", sheet, profile.objectNameColumnId, true);
            if (EditorGUI.EndChangeCheck())
            {
                profile.NormalizeInPlace();
                sheet.Touch();
                MarkDirty("Binding profile edited.", true);
            }

            EditorGUILayout.LabelField("Flow", "Generate Draft -> Confirm -> Pull Rows -> Edit -> Preview Changes -> Apply Selected", UtilityWindowTheme.MutedMiniLabelStyle);
            DrawBindingTargetControls(sheet, profile);
            DrawColumnBindingControls(sheet, profile);
            DrawBindingApplyControls(sheet, profile);
        }

        private PungentDataSheetBindingProfile DrawBindingProfileSelector(PungentDataSheet sheet)
        {
            List<PungentDataSheetBindingProfile> profiles = sheet.bindingProfiles.Where(profile => profile != null).ToList();
            int selectedIndex = Mathf.Max(0, profiles.FindIndex(profile => PungentAuthoringId.EqualsId(profile.id, sheet.activeBindingProfileId)));
            string[] labels = profiles.Select(profile => string.IsNullOrWhiteSpace(profile.displayName) ? profile.id : profile.displayName).ToArray();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                selectedIndex = EditorGUILayout.Popup(selectedIndex, labels);
                if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < profiles.Count)
                {
                    sheet.activeBindingProfileId = profiles[selectedIndex].id;
                    sheet.Touch();
                    MarkDirty("Selected binding profile.", true);
                }

                if (GUILayout.Button("+", GUILayout.Width(26f)))
                {
                    PungentDataSheetBindingProfile created = PungentDataSheetBindingProfile.Create("Binding " + (profiles.Count + 1));
                    sheet.bindingProfiles.Add(created);
                    sheet.activeBindingProfileId = created.id;
                    sheet.Touch();
                    MarkDirty("Created binding profile.", true);
                    return created;
                }

                EditorGUI.BeginDisabledGroup(profiles.Count <= 1);
                if (GUILayout.Button("-", GUILayout.Width(26f)) &&
                    EditorUtility.DisplayDialog("Delete Binding Profile", "Delete this binding profile? Sheet rows and cells are left untouched.", "Delete", "Cancel"))
                {
                    PungentDataSheetBindingProfile current = profiles[Mathf.Clamp(selectedIndex, 0, profiles.Count - 1)];
                    sheet.bindingProfiles.RemoveAll(profile => profile != null && PungentAuthoringId.EqualsId(profile.id, current.id));
                    sheet.activeBindingProfileId = sheet.bindingProfiles.FirstOrDefault(profile => profile != null)?.id ?? string.Empty;
                    sheet.Touch();
                    MarkDirty("Deleted binding profile.", true);
                    GUIUtility.ExitGUI();
                }
                EditorGUI.EndDisabledGroup();
            }

            return sheet.FindBindingProfile(sheet.activeBindingProfileId) ?? profiles[Mathf.Clamp(selectedIndex, 0, profiles.Count - 1)];
        }

        private void DrawBindingTargetControls(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
                if (profile.targetSource == PungentDataSheetBindingTargetSource.AssetFolder)
                {
                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        profile.assetFolderPath = EditorGUILayout.TextField("Folder", profile.assetFolderPath);
                        if (GUILayout.Button("Choose", GUILayout.Width(64f)))
                        {
                            string selected = EditorUtility.OpenFolderPanel("Choose Asset Folder", Application.dataPath, string.Empty);
                            if (!string.IsNullOrWhiteSpace(selected))
                            {
                                string assetPath = ToAssetsFolderPath(selected);
                                if (string.IsNullOrWhiteSpace(assetPath))
                                    _status = "Choose a folder inside this project's Assets folder.";
                                else
                                    profile.assetFolderPath = assetPath;
                            }
                        }
                    }
                    profile.includeSubFolders = EditorGUILayout.Toggle("Include Subfolders", profile.includeSubFolders);
                    if (EditorGUI.EndChangeCheck())
                    {
                        profile.NormalizeInPlace();
                        sheet.Touch();
                        MarkDirty("Binding folder edited.", true);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Generate From Selection", "Generate a non-mutating binding draft from the current explicit Unity selection.")))
                        GenerateBindingDraftFromSelection(profile);

                    using (new EditorGUI.DisabledScope((profile.manualTargetGlobalIds == null || profile.manualTargetGlobalIds.Count == 0) && (profile.targetBindings == null || profile.targetBindings.Count == 0)))
                    {
                        if (GUILayout.Button(new GUIContent("Generate From Cached", "Generate a non-mutating binding draft from cached target IDs and existing linked rows.")))
                            GenerateBindingDraftFromCachedTargets(profile);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Add Selection", "Add selected project objects/components to this profile's cached row targets.")))
                    {
                        int added = PungentDataSheetBindingUtility.AddSelectionToManualTargets(profile, out string message);
                        _status = message;
                        if (added > 0)
                        {
                            sheet.Touch();
                            MarkDirty(message, true);
                        }
                    }

                    EditorGUI.BeginDisabledGroup(profile.targetSource != PungentDataSheetBindingTargetSource.AssetFolder);
                    if (GUILayout.Button(new GUIContent("Refresh Folder", "Explicitly refresh row targets from the configured Assets folder and cache them immediately.")))
                    {
                        int cached = PungentDataSheetBindingUtility.RefreshFolderTargets(profile, out string message);
                        _status = message;
                        if (cached >= 0)
                        {
                            sheet.Touch();
                            MarkDirty(message, true);
                        }
                    }
                    if (GUILayout.Button(new GUIContent("Refresh Folder Draft", "Explicitly generate a non-mutating draft from the configured Assets folder.")))
                        GenerateBindingDraftFromAssetFolder(profile);
                    EditorGUI.EndDisabledGroup();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Pull Rows", "Create/update linked rows and pull readable enabled column values.")))
                    {
                        int pulled = PungentDataSheetBindingUtility.PullFromTargets(sheet, profile, out string message);
                        _status = message;
                        if (pulled > 0)
                        {
                            _applyScope = PungentDataSheetApplyScope.ChangedCells;
                            _gridState.InvalidateCache();
                            MarkDirty(message, true);
                        }
                    }

                    if (GUILayout.Button(new GUIContent("Clear Cache", "Clear cached target IDs. Existing sheet rows and row bindings remain visible.")) &&
                        EditorUtility.DisplayDialog("Clear Cached Targets", "Clear cached manual/folder target IDs for this profile? Existing sheet rows and target row bindings remain visible.", "Clear", "Cancel"))
                    {
                        if (profile.manualTargetGlobalIds == null)
                            profile.manualTargetGlobalIds = new List<string>();
                        profile.manualTargetGlobalIds.Clear();
                        sheet.Touch();
                        MarkDirty("Cleared cached binding targets.", true);
                    }
                }

                int manualCount = profile.manualTargetGlobalIds != null ? profile.manualTargetGlobalIds.Count : 0;
                int rowBindingCount = profile.targetBindings != null ? profile.targetBindings.Count : 0;
                EditorGUILayout.LabelField("Linked Rows", manualCount + " cached target(s) / " + rowBindingCount + " linked row(s)", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.HelpBox(PungentDataSheetBindingUtility.BuildReadinessSummary(sheet, profile), MessageType.None);
                DrawCachedTargetPreview(sheet, profile);
            }
        }

        private void DrawCachedTargetPreview(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            int shown = 0;
            foreach (string globalId in profile.manualTargetGlobalIds ?? new List<string>())
            {
                UnityEngine.Object target = PungentDataSheetBindingUtility.ResolveGlobalObject(globalId);
                EditorGUILayout.LabelField(target != null ? PungentDataSheetBindingUtility.DisplayTarget(target) : "Missing target: " + globalId, UtilityWindowTheme.MutedMiniLabelStyle);
                shown++;
                if (shown >= 6)
                    break;
            }

            int total = profile.manualTargetGlobalIds != null ? profile.manualTargetGlobalIds.Count : 0;
            if (total > shown)
                EditorGUILayout.LabelField("+" + (total - shown) + " more cached target(s)", UtilityWindowTheme.MutedMiniLabelStyle);

            int linkedShown = 0;
            foreach (PungentDataSheetTargetBinding targetBinding in profile.targetBindings ?? new List<PungentDataSheetTargetBinding>())
            {
                if (targetBinding == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    string rowLabel = PreviewRowLabel(sheet, targetBinding.rowId);
                    string targetLabel = string.IsNullOrWhiteSpace(targetBinding.objectName) ? targetBinding.targetGlobalId : targetBinding.objectName;
                    GUILayout.Label(new GUIContent(rowLabel + " -> " + targetLabel, targetBinding.targetGlobalId), UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUI.BeginChangeCheck();
                    targetBinding.pullEnabled = GUILayout.Toggle(targetBinding.pullEnabled, new GUIContent("Pull", "Include this row when pulling values."), EditorStyles.miniButtonLeft, GUILayout.Width(48f));
                    targetBinding.pushEnabled = GUILayout.Toggle(targetBinding.pushEnabled, new GUIContent("Apply", "Include this row when applying sheet values."), EditorStyles.miniButtonRight, GUILayout.Width(54f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        targetBinding.NormalizeInPlace();
                        profile.NormalizeInPlace();
                        sheet.Touch();
                        MarkDirty("Row binding edited.", true);
                    }
                }

                linkedShown++;
                if (linkedShown >= 8)
                    break;
            }

            int linkedTotal = profile.targetBindings != null ? profile.targetBindings.Count : 0;
            if (linkedTotal > linkedShown)
                EditorGUILayout.LabelField("+" + (linkedTotal - linkedShown) + " more linked row(s)", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void GenerateBindingDraftFromSelection(PungentDataSheetBindingProfile profile)
        {
            _bindingDraft = PungentDataSheetBindingUtility.BuildBindingDraftFromSelection(profile, out string message, "Selection Binding Draft");
            PungentDataSheetBindingUtility.ApplyExistingBindingGatesToDraft(profile, _bindingDraft);
            _bindingDraftSource = PungentDataSheetBindingTargetSource.ManualObjects;
            _bindingDraftCommitMode = BindingDraftCommitMode.MergeIntoCurrentSheet;
            _bindingDraftRemapColumnId = string.Empty;
            _bindingDraftStatus = message + " " + DraftStatus("Selection", _bindingDraft);
            OpenDataSheetOverlay(DataSheetOverlayKind.BindingDraft, GUILayoutUtility.GetLastRect());
        }

        private void GenerateBindingDraftFromCachedTargets(PungentDataSheetBindingProfile profile)
        {
            _bindingDraft = PungentDataSheetBindingUtility.BuildBindingDraftFromCachedTargets(profile, out string message, "Cached Binding Draft");
            PungentDataSheetBindingUtility.ApplyExistingBindingGatesToDraft(profile, _bindingDraft);
            _bindingDraftSource = PungentDataSheetBindingTargetSource.ManualObjects;
            _bindingDraftCommitMode = BindingDraftCommitMode.MergeIntoCurrentSheet;
            _bindingDraftRemapColumnId = string.Empty;
            _bindingDraftStatus = message + " " + DraftStatus("Cached", _bindingDraft);
            OpenDataSheetOverlay(DataSheetOverlayKind.BindingDraft, GUILayoutUtility.GetLastRect());
        }

        private void GenerateBindingDraftFromAssetFolder(PungentDataSheetBindingProfile profile)
        {
            _bindingDraft = PungentDataSheetBindingUtility.BuildBindingDraftFromAssetFolder(profile, out string message, "Folder Binding Draft");
            PungentDataSheetBindingUtility.ApplyExistingBindingGatesToDraft(profile, _bindingDraft);
            _bindingDraftSource = PungentDataSheetBindingTargetSource.AssetFolder;
            _bindingDraftCommitMode = BindingDraftCommitMode.MergeIntoCurrentSheet;
            _bindingDraftRemapColumnId = string.Empty;
            _bindingDraftStatus = message + " " + DraftStatus("Folder", _bindingDraft);
            OpenDataSheetOverlay(DataSheetOverlayKind.BindingDraft, GUILayoutUtility.GetLastRect());
        }

        private static string DraftStatus(string source, PungentAuthoringBindingDraft draft)
        {
            int rows = draft?.rows == null ? 0 : draft.rows.Count;
            int columns = draft?.columns == null ? 0 : draft.columns.Count;
            if (rows == 0)
                return source + " draft has no supported targets.";
            if (columns == 0)
                return source + " draft has " + rows.ToString() + " target(s), but no bindable fields.";
            return source + " draft ready: " + rows.ToString() + " target row(s), " + columns.ToString() + " field column(s).";
        }

        private void CommitBindingDraft(PungentDataSheet currentSheet)
        {
            if (_bindingDraft == null)
                return;

            PungentDataSheet sheet = currentSheet;
            PungentDataSheetBindingProfile profile = sheet == null ? null : ActiveBindingProfile(sheet);
            if (_bindingDraftCommitMode == BindingDraftCommitMode.CreateNewSheet)
            {
                sheet = PungentDataSheetEditorStorage.CreateSheet(string.IsNullOrWhiteSpace(_bindingDraft.displayName) ? "Binding Draft Sheet" : _bindingDraft.displayName);
                PungentDataSheetBindingProfile created = PungentDataSheetBindingProfile.Create("Default Binding");
                if (profile != null)
                {
                    created.direction = profile.direction;
                    created.applyMode = profile.applyMode;
                    created.assetFolderPath = profile.assetFolderPath;
                    created.includeSubFolders = profile.includeSubFolders;
                    created.targetTypeName = profile.targetTypeName;
                }

                sheet.bindingProfiles.Add(created);
                sheet.activeBindingProfileId = created.id;
                profile = created;
            }
            else if (sheet == null)
            {
                _bindingDraftStatus = "Select a sheet or choose Create New Sheet before confirming.";
                return;
            }

            if (profile == null)
                profile = sheet.GetOrCreateActiveBindingProfile();

            bool changed = PungentDataSheetBindingUtility.ApplyBindingDraftToSheet(
                sheet,
                profile,
                _bindingDraft,
                new PungentDataSheetBindingUtility.BindingDraftApplyOptions
                {
                    targetSource = _bindingDraftSource,
                    addTargetsToManualCache = true
                },
                out PungentDataSheetBindingUtility.BindingDraftApplySummary summary);

            if (!changed)
            {
                _bindingDraftStatus = "Draft did not contain any included rows or columns to commit.";
                return;
            }

            SelectSheet(sheet.id);
            _gridState.InvalidateCache();
            _applyPreview.Clear();
            _bindingDraftStatus = summary.ToString();
            _status = _bindingDraftStatus + " Pull Rows to read current values.";
            MarkDirty(_status, true);
            CloseDataSheetOverlay();
        }

        private void DrawColumnBindingControls(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Columns", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Column -> Bound Field", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Use Selection As Sample"))
                    {
                        _bindingEndpointPickerState.targetObject = Selection.activeObject;
                        _bindingEndpointPickerState.selectedGroupLabel = string.Empty;
                        _bindingEndpointPickerState.selectedEndpointId = string.Empty;
                        _bindingEndpointPickerStatus = Selection.activeObject == null
                            ? "Select an object, asset, or component first, or assign one below."
                            : "Picker loaded from current selection.";
                    }

                    if (GUILayout.Button("Clear Sample"))
                    {
                        _bindingEndpointPickerColumnId = string.Empty;
                        _bindingEndpointPickerState.targetObject = null;
                        _bindingEndpointPickerState.selectedGroupLabel = string.Empty;
                        _bindingEndpointPickerState.selectedEndpointId = string.Empty;
                        _bindingEndpointPickerStatus = "Picker cleared.";
                    }
                }

                foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
                {
                    if (column == null)
                        continue;

                    PungentDataSheetColumnBinding binding = profile.FindColumnBinding(column.id);
                    string path = binding != null ? binding.propertyPath : string.Empty;
                    bool pull = binding == null || binding.pullEnabled;
                    bool push = binding == null || binding.pushEnabled;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(new GUIContent(column.displayName, column.id), EditorStyles.boldLabel, GUILayout.Width(120f));
                            GUILayout.Label(new GUIContent(BindingSummary(binding), "Field label, value kind, and property path."), UtilityWindowTheme.MutedMiniLabelStyle);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Choose Field", EditorStyles.miniButton, GUILayout.Width(86f)))
                            {
                                _bindingEndpointPickerColumnId = column.id;
                                if (_bindingEndpointPickerState.targetObject == null)
                                    _bindingEndpointPickerState.targetObject = Selection.activeObject;
                                _bindingEndpointPickerState.selectedGroupLabel = string.Empty;
                                _bindingEndpointPickerState.selectedEndpointId = string.Empty;
                                _bindingEndpointPickerStatus = "Choosing field for " + column.displayName + ".";
                            }
                        }

                        EditorGUI.BeginChangeCheck();
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Field Path", GUILayout.Width(70f));
                            path = EditorGUILayout.TextField(path);
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            pull = GUILayout.Toggle(pull, new GUIContent("Pull", "When enabled, Pull Rows reads this field into the sheet."), EditorStyles.miniButtonLeft);
                            push = GUILayout.Toggle(push, new GUIContent("Apply", "When enabled, previews and apply modes can write changed cells in this column."), EditorStyles.miniButtonRight);
                        }

                        if (EditorGUI.EndChangeCheck())
                        {
                            PungentDataSheetColumnBinding next = profile.GetOrCreateColumnBinding(column.id, column.displayName);
                            next.propertyPath = path;
                            next.pullEnabled = pull;
                            next.pushEnabled = push;
                            next.NormalizeInPlace();
                            sheet.Touch();
                            MarkDirty("Column binding edited.", true);
                        }

                        if (binding != null && !string.IsNullOrWhiteSpace(binding.expectedPropertyType))
                            EditorGUILayout.LabelField("Expected", binding.expectedPropertyType, UtilityWindowTheme.MutedMiniLabelStyle);

                        if (PungentAuthoringId.EqualsId(_bindingEndpointPickerColumnId, column.id))
                            DrawColumnEndpointPicker(sheet, profile, column);
                    }
                }
            }
        }

        private void DrawColumnEndpointPicker(PungentDataSheet sheet, PungentDataSheetBindingProfile profile, PungentDataSheetColumn column)
        {
            // RDE/GUIDED-BINDING MIGRATION NOTE: keep Data Sheet profile serialization local, but use the shared endpoint picker here.
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Choose Field", EditorStyles.boldLabel);

                PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
                {
                    contextLabel = string.Empty,
                    objectLabel = "Sample Target",
                    componentLabel = "Component",
                    endpointLabel = "Field",
                    bindButtonLabel = "Use Field",
                    helpText = string.Empty,
                    showHeader = false,
                    showHelp = false
                };
                PungentAuthoringGuidedBindingResult result = PungentAuthoringGuidedBindingView.Draw(_bindingEndpointPickerState, options);
                PungentAuthoringBindingEndpoint endpoint = result.selectedEndpoint;
                if (endpoint != null)
                {
                    if (result.bindClicked && endpoint.CanBind)
                    {
                        ApplyEndpointToColumnBinding(sheet, profile, column, endpoint);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Use Selection", EditorStyles.miniButton))
                    {
                        _bindingEndpointPickerState.targetObject = Selection.activeObject;
                        _bindingEndpointPickerState.selectedGroupLabel = string.Empty;
                        _bindingEndpointPickerState.selectedEndpointId = string.Empty;
                        _bindingEndpointPickerStatus = Selection.activeObject == null
                            ? "Nothing is selected."
                            : "Picker loaded from current selection.";
                    }

                    if (GUILayout.Button("Close", EditorStyles.miniButton))
                        _bindingEndpointPickerColumnId = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(_bindingEndpointPickerStatus))
                    EditorGUILayout.LabelField(_bindingEndpointPickerStatus, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void ApplyEndpointToColumnBinding(
            PungentDataSheet sheet,
            PungentDataSheetBindingProfile profile,
            PungentDataSheetColumn column,
            PungentAuthoringBindingEndpoint endpoint)
        {
            if (sheet == null || profile == null || column == null || endpoint == null || endpoint.target == null)
                return;

            PungentDataSheetColumnBinding binding = profile.GetOrCreateColumnBinding(column.id, column.displayName);
            PungentAuthoringBindingValueType runtimeValueType = PungentAuthoringBindingDiscoveryService.ToRuntimeValueType(endpoint);
            PungentAuthoringBindingLink link = PungentAuthoringBindingBridgeService.CreateLinkFromEndpoint(
                endpoint,
                "data-sheets",
                sheet.id,
                column.id,
                column.displayName,
                profile.direction == PungentDataSheetBindingDirection.PullOnly
                    ? PungentAuthoringBindingLinkDirection.ReadOnly
                    : profile.direction == PungentDataSheetBindingDirection.PushOnly
                        ? PungentAuthoringBindingLinkDirection.WriteOnly
                        : PungentAuthoringBindingLinkDirection.TwoWay);
            binding.propertyPath = endpoint.target.propertyPath;
            binding.expectedPropertyType = runtimeValueType == PungentAuthoringBindingValueType.Unknown ? endpoint.valueKind.ToString() : runtimeValueType.ToString();
            binding.displayName = string.IsNullOrWhiteSpace(endpoint.label) ? column.displayName : endpoint.label;
            binding.bindingLinkId = link.id;
            binding.adapterId = endpoint.adapterId;
            binding.adapterDisplayName = endpoint.adapterDisplayName;
            binding.endpointId = endpoint.id;
            binding.endpointLabel = endpoint.label;
            binding.valueType = runtimeValueType;
            binding.bindingPath = link.bindingPath;
            binding.bindingSlot = PungentAuthoringBindingDiscoveryService.CreateSlot(
                PungentAuthoringBindingSlotRole.DataSheetColumn,
                "data-sheets",
                sheet.id,
                column.id,
                column.displayName,
                string.IsNullOrWhiteSpace(endpoint.label) ? column.displayName : endpoint.label,
                link.bindingPath);
            binding.bindingSlot.pullEnabled = binding.pullEnabled;
            binding.bindingSlot.pushEnabled = binding.pushEnabled;
            binding.NormalizeInPlace();
            column.dataType = PungentDataSheetBindingUtility.DataTypeFor(runtimeValueType);
            column.NormalizeInPlace();

            if (endpoint.target != null &&
                !string.IsNullOrWhiteSpace(endpoint.target.contextId))
            {
                if (profile.manualTargetGlobalIds == null)
                    profile.manualTargetGlobalIds = new List<string>();
                if (!profile.manualTargetGlobalIds.Exists(id => string.Equals(id, endpoint.target.contextId, StringComparison.OrdinalIgnoreCase)))
                    profile.manualTargetGlobalIds.Add(endpoint.target.contextId);
            }

            if (string.IsNullOrWhiteSpace(profile.targetTypeName) &&
                endpoint.target != null &&
                PungentAuthoringBindingApplicationService.TryResolveUnityTarget(endpoint.target, out UnityEngine.Object endpointTarget, out _) &&
                endpointTarget != null)
            {
                profile.targetTypeName = endpointTarget.GetType().FullName ?? endpointTarget.GetType().Name;
            }

            profile.NormalizeInPlace();
            sheet.Touch();
            _bindingEndpointPickerStatus = "Mapped " + column.displayName + " to " + endpoint.label + ".";
            MarkDirty("Column field selected.", true);
        }

        private static string BindingSummary(PungentDataSheetColumnBinding binding)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.propertyPath))
                return "Unmapped";

            string kind = string.IsNullOrWhiteSpace(binding.expectedPropertyType) ? "Unknown" : binding.expectedPropertyType;
            string endpoint = string.IsNullOrWhiteSpace(binding.endpointLabel) ? binding.propertyPath : binding.endpointLabel;
            return endpoint + " (" + kind + ")";
        }

        private void DrawBindingApplyControls(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Sync", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                profile.direction = DrawBindingDirectionPopup("Flow", profile.direction);
                profile.applyMode = DrawApplyModePopup("Apply Mode", profile.applyMode);
                if (EditorGUI.EndChangeCheck())
                {
                    profile.NormalizeInPlace();
                    sheet.Touch();
                    MarkDirty("Binding sync settings edited.", true);
                }
                _applyScope = DrawApplyScopePopup("Apply Range", _applyScope);
                _conflictPolicy = DrawConflictPolicyPopup("When Project Changed", _conflictPolicy);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview Changes"))
                        BuildBindingApplyPreview(sheet, profile);

                    EditorGUI.BeginDisabledGroup(_applyPreview.Count == 0);
                    if (GUILayout.Button("Open Preview"))
                        _inspectorPane = InspectorPane.ApplyPreview;
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        private void DrawApplyPreviewInspector(PungentDataSheet sheet)
        {
            EditorGUILayout.LabelField("Apply Preview", EditorStyles.boldLabel);
            PungentDataSheetBindingProfile profile = ActiveBindingProfile(sheet);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Create a binding profile before building an apply preview.", MessageType.Info);
                return;
            }

            _applyScope = DrawApplyScopePopup("Apply Range", _applyScope);
            _conflictPolicy = DrawConflictPolicyPopup("When Project Changed", _conflictPolicy);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Preview"))
                    BuildBindingApplyPreview(sheet, profile);

                int ready = _applyPreview.Count(item => item != null && item.CanApply);
                EditorGUI.BeginDisabledGroup(ready == 0);
                if (GUILayout.Button("Apply Selected") &&
                    EditorUtility.DisplayDialog("Apply Data Sheet Values", "Apply " + ready + " ready value(s) to project objects using Undo?", "Apply", "Cancel"))
                    ApplyBindingPreview(sheet, profile);
                EditorGUI.EndDisabledGroup();
            }

            if (_applyPreview.Count == 0)
            {
                EditorGUILayout.HelpBox(PungentDataSheetBindingUtility.DescribeApplyPreviewBlockers(sheet, profile, _applyScope), MessageType.Info);
                return;
            }

            int readyCount = _applyPreview.Count(item => item != null && item.status == PungentDataSheetApplyPreviewStatus.Ready);
            int selectedReadyCount = _applyPreview.Count(item => item != null && item.CanApply);
            int skippedCount = _applyPreview.Count(item => item != null && item.status == PungentDataSheetApplyPreviewStatus.Skipped);
            int failedCount = _applyPreview.Count(item => item != null && item.status == PungentDataSheetApplyPreviewStatus.Failed);
            EditorGUILayout.LabelField("Summary", selectedReadyCount + " selected / " + readyCount + " ready / " + skippedCount + " skipped / " + failedCount + " failed", UtilityWindowTheme.MutedMiniLabelStyle);
            DrawApplyPreviewReasonSummary(readyCount);

            int max = Mathf.Min(_applyPreview.Count, 120);
            for (int i = 0; i < max; i++)
            {
                PungentDataSheetApplyPreview preview = _applyPreview[i];
                if (preview == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginDisabledGroup(preview.status != PungentDataSheetApplyPreviewStatus.Ready);
                        preview.selected = EditorGUILayout.Toggle(preview.selected, GUILayout.Width(18f));
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.LabelField(preview.status + ": " + (string.IsNullOrWhiteSpace(preview.targetName) ? PreviewRowLabel(sheet, preview.rowId) : preview.targetName), EditorStyles.boldLabel);
                    }
                    EditorGUILayout.LabelField("Cell", PreviewRowLabel(sheet, preview.rowId) + " / " + PreviewColumnLabel(sheet, preview.columnId), UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Property", preview.propertyPath, UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Old", preview.oldValue, UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("New", preview.newValue, UtilityWindowTheme.MutedMiniLabelStyle);
                    if (!string.IsNullOrWhiteSpace(preview.reason))
                        EditorGUILayout.LabelField("Reason", preview.reason, UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }

            if (_applyPreview.Count > max)
                EditorGUILayout.HelpBox("Showing first " + max + " preview entries. The full preview will still apply ready entries.", MessageType.Info);
        }

        private void DrawApplyPreviewReasonSummary(int readyCount)
        {
            Dictionary<string, int> groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _applyPreview.Count; i++)
            {
                PungentDataSheetApplyPreview preview = _applyPreview[i];
                if (preview == null || preview.status == PungentDataSheetApplyPreviewStatus.Ready)
                    continue;

                string group = ApplyPreviewReasonGroup(preview.reason);
                if (!groups.ContainsKey(group))
                    groups[group] = 0;
                groups[group]++;
            }

            if (groups.Count == 0)
                return;

            EditorGUILayout.LabelField("Skip Reasons", string.Join("; ", groups.Select(pair => pair.Key + " x" + pair.Value).ToArray()), UtilityWindowTheme.MutedMiniLabelStyle);
            if (readyCount == 0 && groups.Count == 1 && groups.ContainsKey("Already matches project value"))
                EditorGUILayout.HelpBox("Pull succeeded and the sheet currently matches the project. Edit a bound cell value, then rebuild the preview to produce ready entries.", MessageType.Info);
        }

        private static string ApplyPreviewReasonGroup(string reason)
        {
            string text = reason ?? string.Empty;
            if (text.IndexOf("already matches", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Already matches project value";
            if (text.IndexOf("not linked", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Row not linked";
            if (text.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Apply disabled";
            if (text.IndexOf("unsupported", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Property unsupported";
            if (text.IndexOf("changed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Conflict policy";
            if (text.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("unresolved", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Unresolved object or property";
            if (text.IndexOf("not been pulled", StringComparison.OrdinalIgnoreCase) >= 0)
                return "No changed cell value";
            return string.IsNullOrWhiteSpace(text) ? "Skipped" : text;
        }

        private static string PreviewRowLabel(PungentDataSheet sheet, string rowId)
        {
            PungentDataSheetRow row = sheet != null ? sheet.FindRow(rowId) : null;
            if (row == null)
                return string.IsNullOrWhiteSpace(rowId) ? "(no row)" : rowId;
            return string.IsNullOrWhiteSpace(row.displayName) ? row.id : row.displayName;
        }

        private static string PreviewColumnLabel(PungentDataSheet sheet, string columnId)
        {
            PungentDataSheetColumn column = sheet != null ? sheet.FindColumn(columnId) : null;
            if (column == null)
                return string.IsNullOrWhiteSpace(columnId) ? "(no column)" : columnId;
            return string.IsNullOrWhiteSpace(column.displayName) ? column.id : column.displayName;
        }

        private void BuildBindingApplyPreview(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            _applyPreview.Clear();
            _applyPreview.AddRange(PungentDataSheetBindingUtility.BuildApplyPreview(
                sheet,
                profile,
                _applyScope,
                _conflictPolicy,
                PungentDataSheetGridGUI.GetSelectedRowIds(sheet, _gridState),
                PungentDataSheetGridGUI.GetSelectedCellAddresses(sheet, _gridState)));
            _inspectorPane = InspectorPane.ApplyPreview;
            _showInspector = true;
            int ready = _applyPreview.Count(item => item != null && item.CanApply);
            _status = _applyPreview.Count == 0
                ? PungentDataSheetBindingUtility.DescribeApplyPreviewBlockers(sheet, profile, _applyScope)
                : "Built apply preview: " + ready + " ready / " + _applyPreview.Count + " total.";
        }

        private void ApplyBindingPreview(PungentDataSheet sheet, PungentDataSheetBindingProfile profile)
        {
            int applied = PungentDataSheetBindingUtility.ApplyPreview(sheet, profile, _applyPreview, out string message);
            _status = message;
            if (applied > 0)
            {
                _gridState.InvalidateCache();
                MarkDirty(message, true);
            }
        }

        private void TryAutoApplyEditedCell(PungentDataSheet sheet, PungentDataSheetCell cell)
        {
            if (sheet == null || cell == null)
                return;

            PungentDataSheetBindingProfile profile = ActiveBindingProfile(sheet);
            if (profile == null || profile.applyMode == PungentAuthoringBindingApplyMode.ManualApply)
                return;

            if (profile.applyMode == PungentAuthoringBindingApplyMode.AutoApplyEditMode && EditorApplication.isPlaying)
            {
                SetAutoApplyBlocked(cell, "Auto apply waits for Edit Mode.");
                return;
            }

            if (profile.direction == PungentDataSheetBindingDirection.PullOnly)
            {
                SetAutoApplyBlocked(cell, "Flow is Pull Only.");
                return;
            }

            PungentDataSheetTargetBinding targetBinding = profile.FindTargetBindingByRow(cell.rowId);
            if (targetBinding == null || string.IsNullOrWhiteSpace(targetBinding.targetGlobalId))
            {
                SetAutoApplyBlocked(cell, "Row is not linked.");
                return;
            }

            if (!targetBinding.pushEnabled)
            {
                SetAutoApplyBlocked(cell, "Apply is off for this row.");
                return;
            }

            PungentDataSheetColumnBinding columnBinding = profile.FindColumnBinding(cell.columnId);
            if (columnBinding == null || string.IsNullOrWhiteSpace(columnBinding.propertyPath))
            {
                SetAutoApplyBlocked(cell, "Column is not mapped.");
                return;
            }

            if (!columnBinding.pushEnabled)
            {
                SetAutoApplyBlocked(cell, "Apply is off for this column.");
                return;
            }

            if (PungentDataSheetBindingUtility.TryApplySingleCell(
                    sheet,
                    profile,
                    cell.rowId,
                    cell.columnId,
                    _conflictPolicy,
                    out PungentDataSheetApplyPreview preview,
                    out string message))
            {
                _status = "Auto applied " + PreviewColumnLabel(sheet, cell.columnId) + " for " + PreviewRowLabel(sheet, cell.rowId) + ".";
                return;
            }

            if (preview != null &&
                preview.status == PungentDataSheetApplyPreviewStatus.Skipped &&
                !string.IsNullOrWhiteSpace(preview.reason) &&
                preview.reason.IndexOf("already matches", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cell.propagationStatus = PungentDataSheetPropagationStatus.Clean;
                cell.propagationMessage = "Project already matches.";
                _status = "Project already matches " + PreviewColumnLabel(sheet, cell.columnId) + " for " + PreviewRowLabel(sheet, cell.rowId) + ".";
                return;
            }

            SetAutoApplyBlocked(cell, preview == null || string.IsNullOrWhiteSpace(preview.reason) ? message : preview.reason);
        }

        private static void SetAutoApplyBlocked(PungentDataSheetCell cell, string reason)
        {
            if (cell == null)
                return;

            if (cell.propagationStatus == PungentDataSheetPropagationStatus.Clean)
                cell.propagationStatus = PungentDataSheetPropagationStatus.Changed;
            cell.propagationMessage = string.IsNullOrWhiteSpace(reason) ? "Auto apply blocked." : reason;
        }

        private PungentDataSheetBindingProfile ActiveBindingProfile(PungentDataSheet sheet)
        {
            if (sheet == null || sheet.bindingProfiles == null || sheet.bindingProfiles.Count == 0)
                return null;

            return sheet.FindBindingProfile(sheet.activeBindingProfileId) ?? sheet.bindingProfiles.FirstOrDefault(profile => profile != null);
        }

        private static PungentDataSheetBindingTargetSource DrawTargetSourcePopup(string label, PungentDataSheetBindingTargetSource value)
        {
            PungentDataSheetBindingTargetSource[] values =
            {
                PungentDataSheetBindingTargetSource.CurrentSelection,
                PungentDataSheetBindingTargetSource.ManualObjects,
                PungentDataSheetBindingTargetSource.AssetFolder
            };
            string[] labels = { "Current Selection", "Cached Objects", "Asset Folder" };
            int index = Mathf.Clamp(Array.IndexOf(values, value), 0, values.Length - 1);
            return values[Mathf.Clamp(EditorGUILayout.Popup(label, index, labels), 0, values.Length - 1)];
        }

        private static PungentDataSheetBindingDirection DrawBindingDirectionPopup(string label, PungentDataSheetBindingDirection value)
        {
            PungentDataSheetBindingDirection[] values =
            {
                PungentDataSheetBindingDirection.TwoWay,
                PungentDataSheetBindingDirection.PullOnly,
                PungentDataSheetBindingDirection.PushOnly
            };
            string[] labels = { "Pull and Apply", "Pull Only", "Apply Only" };
            int index = Mathf.Clamp(Array.IndexOf(values, value), 0, values.Length - 1);
            return values[Mathf.Clamp(EditorGUILayout.Popup(label, index, labels), 0, values.Length - 1)];
        }

        private static PungentAuthoringBindingApplyMode DrawApplyModePopup(string label, PungentAuthoringBindingApplyMode value)
        {
            PungentAuthoringBindingApplyMode[] values =
            {
                PungentAuthoringBindingApplyMode.ManualApply,
                PungentAuthoringBindingApplyMode.AutoApplyEditMode,
                PungentAuthoringBindingApplyMode.AutoApplyAlways
            };
            string[] labels = { "Manual Apply", "Auto in Edit Mode", "Auto in Edit + Play" };
            int index = Mathf.Clamp(Array.IndexOf(values, value), 0, values.Length - 1);
            return values[Mathf.Clamp(EditorGUILayout.Popup(label, index, labels), 0, values.Length - 1)];
        }

        private static PungentDataSheetApplyScope DrawApplyScopePopup(string label, PungentDataSheetApplyScope value)
        {
            PungentDataSheetApplyScope[] values =
            {
                PungentDataSheetApplyScope.SelectedCells,
                PungentDataSheetApplyScope.SelectedRows,
                PungentDataSheetApplyScope.ChangedCells,
                PungentDataSheetApplyScope.FullSheet
            };
            string[] labels = { "Selected Cells", "Selected Rows", "Changed Cells", "Full Sheet" };
            int index = Mathf.Clamp(Array.IndexOf(values, value), 0, values.Length - 1);
            return values[Mathf.Clamp(EditorGUILayout.Popup(label, index, labels), 0, values.Length - 1)];
        }

        private static PungentDataSheetConflictPolicy DrawConflictPolicyPopup(string label, PungentDataSheetConflictPolicy value)
        {
            PungentDataSheetConflictPolicy[] values =
            {
                PungentDataSheetConflictPolicy.Overwrite,
                PungentDataSheetConflictPolicy.FillBlanksOnly,
                PungentDataSheetConflictPolicy.SkipIfProjectChangedSincePull
            };
            string[] labels = { "Overwrite", "Fill Blanks Only", "Skip If Changed Since Pull" };
            int index = Mathf.Clamp(Array.IndexOf(values, value), 0, values.Length - 1);
            return values[Mathf.Clamp(EditorGUILayout.Popup(label, index, labels), 0, values.Length - 1)];
        }

        private string DrawColumnIdPopup(string label, PungentDataSheet sheet, string currentColumnId, bool includeNone)
        {
            List<string> ids = new List<string>();
            List<string> labels = new List<string>();
            if (includeNone)
            {
                ids.Add(string.Empty);
                labels.Add("None");
            }

            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
            {
                if (column == null)
                    continue;

                ids.Add(column.id);
                labels.Add(string.IsNullOrWhiteSpace(column.displayName) ? column.id : column.displayName);
            }

            int index = Mathf.Max(0, ids.FindIndex(id => PungentAuthoringId.EqualsId(id, currentColumnId)));
            int nextIndex = EditorGUILayout.Popup(label, index, labels.ToArray());
            return nextIndex >= 0 && nextIndex < ids.Count ? ids[nextIndex] : string.Empty;
        }

        private static string ToAssetsFolderPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                return string.Empty;

            string dataPath = Application.dataPath.Replace("\\", "/");
            string clean = absolutePath.Replace("\\", "/");
            if (string.Equals(clean, dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets";
            if (clean.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
                return "Assets" + clean.Substring(dataPath.Length);
            return string.Empty;
        }

        private void DrawSheetInspector(PungentDataSheet sheet)
        {
            EditorGUILayout.LabelField("Sheet", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            sheet.title = EditorGUILayout.TextField("Title", sheet.title);
            sheet.summary = EditorGUILayout.TextField("Summary", sheet.summary);
            sheet.status = EditorGUILayout.TextField("Status", sheet.status);
            sheet.priority = EditorGUILayout.TextField("Priority", sheet.priority);
            sheet.visibility = EditorGUILayout.TextField("Visibility", sheet.visibility);
            sheet.archived = EditorGUILayout.Toggle("Archived", sheet.archived);
            sheet.developerOnly = EditorGUILayout.Toggle("Developer Only", sheet.developerOnly);
            string tags = EditorGUILayout.TextField("Tags", JoinTags(sheet.tags));
            if (EditorGUI.EndChangeCheck())
            {
                sheet.tags = SplitTags(tags);
                sheet.Touch();
                MarkDirty("Sheet metadata edited.", true);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.SelectableLabel("ID: " + sheet.id, EditorStyles.miniLabel, GUILayout.Height(18f));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Sheet ID"))
                    EditorGUIUtility.systemCopyBuffer = sheet.id;
                if (GUILayout.Button("Open Sheet Sticky Note"))
                    OpenOrCreateSheetNote(sheet);
            }
        }

        private void DrawRowInspector(PungentDataSheet sheet, PungentDataSheetRow row)
        {
            EditorGUILayout.LabelField("Row", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            row.displayName = EditorGUILayout.TextField("Display Name", row.displayName);
            row.order = EditorGUILayout.IntField("Order", row.order);
            row.hidden = EditorGUILayout.Toggle("Hidden", row.hidden);
            row.locked = EditorGUILayout.Toggle("Locked", row.locked);
            string tags = EditorGUILayout.TextField("Tags", JoinTags(row.tags));
            if (EditorGUI.EndChangeCheck())
            {
                row.tags = SplitTags(tags);
                sheet.Touch();
                MarkDirty("Row edited.", true);
            }

            EditorGUILayout.SelectableLabel("ID: " + row.id, EditorStyles.miniLabel, GUILayout.Height(18f));
            bool rowReorderBlocked = !string.IsNullOrWhiteSpace(_gridState.sortColumnId) || !string.IsNullOrWhiteSpace(_gridState.filterText);
            using (new EditorGUI.DisabledScope(rowReorderBlocked))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Move Up"))
                        MoveRow(row, -1);
                    if (GUILayout.Button("Move Down"))
                        MoveRow(row, 1);
                }
            }
            if (rowReorderBlocked)
                EditorGUILayout.HelpBox("Row reordering is disabled while sorted or filtered, matching row-header drag behaviour.", MessageType.Info);

            if (DrawAuthoringReferenceEditor("Linked Authoring Ref", row.linkedAuthoringRef, value => row.linkedAuthoringRef = value))
            {
                sheet.Touch();
                _gridState.InvalidateCache();
                MarkDirty("Row link edited.", true);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Delete Row") &&
                EditorUtility.DisplayDialog("Delete Row", "Delete row '" + (string.IsNullOrWhiteSpace(row.displayName) ? row.id : row.displayName) + "' and its cells?", "Delete", "Cancel"))
            {
                sheet.DeleteRow(row.id);
                _gridState.selectedRowId = string.Empty;
                MarkDirty("Deleted row.", true);
                GUIUtility.ExitGUI();
            }
        }

        private void DrawColumnInspector(PungentDataSheet sheet, PungentDataSheetColumn column)
        {
            EditorGUILayout.LabelField("Column", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            column.displayName = EditorGUILayout.TextField("Display Name", column.displayName);
            column.dataType = (PungentDataSheetDataType)EditorGUILayout.EnumPopup("Data Type", column.dataType);
            column.width = EditorGUILayout.FloatField("Width", column.width);
            column.hidden = EditorGUILayout.Toggle("Hidden", column.hidden);
            column.locked = EditorGUILayout.Toggle("Locked", column.locked);
            string enumOptions = column.dataType == PungentDataSheetDataType.EnumText
                ? EditorGUILayout.TextField("Enum Options", JoinTags(column.enumOptions))
                : JoinTags(column.enumOptions);
            if (column.dataType == PungentDataSheetDataType.EnumText)
                EditorGUILayout.HelpBox("Enter comma-separated options here, then double-click cells in this column to edit with a popup.", MessageType.Info);
            if (EditorGUI.EndChangeCheck())
            {
                if (column.dataType == PungentDataSheetDataType.EnumText)
                    column.enumOptions = SplitTags(enumOptions);
                column.NormalizeInPlace();
                sheet.Touch();
                MarkDirty("Column edited.", true);
            }

            EditorGUILayout.SelectableLabel("ID: " + column.id, EditorStyles.miniLabel, GUILayout.Height(18f));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Move Left"))
                    MoveColumn(column, -1);
                if (GUILayout.Button("Move Right"))
                    MoveColumn(column, 1);
            }

            if (DrawAuthoringReferenceEditor("Column Reference", column.reference, value => column.reference = value))
            {
                sheet.Touch();
                _gridState.InvalidateCache();
                MarkDirty("Column link edited.", true);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Delete Column") &&
                EditorUtility.DisplayDialog("Delete Column", "Delete column '" + column.displayName + "' and its cells?", "Delete", "Cancel"))
            {
                sheet.DeleteColumn(column.id);
                _gridState.selectedColumnId = string.Empty;
                MarkDirty("Deleted column.", true);
                GUIUtility.ExitGUI();
            }
        }

        private void DrawCellInspector(PungentDataSheet sheet, PungentDataSheetCell cell)
        {
            PungentDataSheetRow row = sheet.FindRow(_gridState.selectedCellRowId);
            PungentDataSheetColumn column = sheet.FindColumn(_gridState.selectedCellColumnId);
            EditorGUILayout.LabelField("Cell", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Row", row != null ? (string.IsNullOrWhiteSpace(row.displayName) ? row.id : row.displayName) : _gridState.selectedCellRowId);
            EditorGUILayout.LabelField("Column", column != null ? column.displayName : _gridState.selectedCellColumnId);

            string rawValue = cell != null ? cell.rawValue ?? string.Empty : string.Empty;
            string displayValue = cell != null ? cell.displayValue ?? string.Empty : rawValue;
            EditorGUI.BeginChangeCheck();
            rawValue = EditorGUILayout.TextField("Raw Value", rawValue);
            displayValue = EditorGUILayout.TextField("Display Value", displayValue);
            if (EditorGUI.EndChangeCheck())
            {
                if (PungentDataSheetGridGUI.MaterializeSelectedCell(sheet, _gridState, out cell))
                {
                    SetCellValue(cell, rawValue);
                    cell.displayValue = displayValue ?? rawValue;
                    cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
                    sheet.Touch();
                    _gridState.InvalidateCache();
                    OnSheetCellEdited(sheet, cell, "Cell edited.");
                }
            }

            EditorGUILayout.LabelField("Validation", cell != null ? cell.validationStatus.ToString() : "NotRun");
            DrawCompactCellAuthoringReferenceEditor(sheet, cell);
            if (column != null && column.dataType == PungentDataSheetDataType.EnumText)
                DrawEnumCellValueTools(sheet, column, cell);

            DrawSelectedObjectValueTools(sheet, cell);
        }

        private void DrawCompactCellAuthoringReferenceEditor(PungentDataSheet sheet, PungentDataSheetCell cell)
        {
            PungentAuthoringReference linked = cell != null && cell.linkedAuthoringRef != null && cell.linkedAuthoringRef.HasItemId
                ? cell.linkedAuthoringRef
                : null;
            PungentAuthoringReference rawReference = null;
            bool parsedRaw = cell != null && PungentDataSheetAuthoringReferenceCodec.TryParseStructured(cell.rawValue, out rawReference);
            PungentAuthoringReference displayReference = linked ?? (parsedRaw ? rawReference : null);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Authoring Link", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Pick", "Pick a linkable item from cached authoring providers."), EditorStyles.miniButton, GUILayout.Width(44f)))
                        _showCellAuthoringPicker = !_showCellAuthoringPicker;
                }

                if (displayReference == null || !displayReference.HasItemId)
                {
                    EditorGUILayout.LabelField("Status", "No linked authoring item.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                else
                {
                    DrawCompactAuthoringReferenceLabels(displayReference, linked != null ? "Stored link" : "Structured raw value");
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(displayReference == null || !displayReference.HasItemId || !string.IsNullOrEmpty(ProviderState(displayReference))))
                    {
                        if (GUILayout.Button(new GUIContent("Open", "Open the linked authoring item when its provider supports opening."), EditorStyles.miniButton))
                        {
                            bool opened = PungentAuthoringProviderRegistry.TryOpen(displayReference);
                            _status = opened ? "Opened linked item." : "Linked item could not be opened.";
                        }
                    }

                    using (new EditorGUI.DisabledScope(displayReference == null || !displayReference.HasItemId))
                    {
                        if (GUILayout.Button(new GUIContent("Copy ID", "Copy the linked item ID."), EditorStyles.miniButton))
                            EditorGUIUtility.systemCopyBuffer = displayReference.itemId;
                    }

                    using (new EditorGUI.DisabledScope(linked == null))
                    {
                        if (GUILayout.Button(new GUIContent("Write Raw", "Write the stored link as provider|kind|id|label in Raw Value."), EditorStyles.miniButton))
                        {
                            if (PungentDataSheetGridGUI.MaterializeSelectedCell(sheet, _gridState, out PungentDataSheetCell target))
                            {
                                SetCellValue(target, PungentDataSheetAuthoringReferenceCodec.Format(target.linkedAuthoringRef));
                                sheet.Touch();
                                _gridState.InvalidateCache();
                                MarkDirty("Cell link written to raw value.", true);
                            }
                        }
                    }

                    using (new EditorGUI.DisabledScope(!parsedRaw))
                    {
                        if (GUILayout.Button(new GUIContent("Use Raw", "Store the structured raw reference as the cell link."), EditorStyles.miniButton))
                            AssignCellAuthoringReference(sheet, rawReference, "Cell raw reference linked.");
                    }

                    using (new EditorGUI.DisabledScope(linked == null))
                    {
                        if (GUILayout.Button(new GUIContent("Clear", "Clear the stored link without deleting the raw cell value."), EditorStyles.miniButton))
                            AssignCellAuthoringReference(sheet, null, "Cell link cleared.");
                    }
                }

                if (_showCellAuthoringPicker)
                {
                    PungentAuthoringReference pickerReference = CloneAuthoringReference(linked) ?? new PungentAuthoringReference { itemKind = PungentAuthoringItemKind.LegacyNote };
                    bool changed = false;
                    DrawAuthoringReferencePicker(pickerReference, ref changed);
                    if (changed)
                        AssignCellAuthoringReference(sheet, pickerReference, "Cell link picked.");
                }

                _showAdvancedCellRawRef = EditorGUILayout.Foldout(_showAdvancedCellRawRef, "Advanced Raw Ref", true);
                if (_showAdvancedCellRawRef)
                    DrawAdvancedCellReferenceFields(sheet, displayReference);
            }
        }

        private void DrawCompactAuthoringReferenceLabels(PungentAuthoringReference reference, string sourceLabel)
        {
            string providerState = ProviderState(reference);
            EditorGUILayout.LabelField("Source", sourceLabel, UtilityWindowTheme.MutedMiniLabelStyle);
            EditorGUILayout.LabelField("Provider", string.IsNullOrWhiteSpace(reference.providerId) ? "(kind lookup)" : reference.providerId, UtilityWindowTheme.MutedMiniLabelStyle);
            EditorGUILayout.LabelField("Kind", reference.KindLabel, UtilityWindowTheme.MutedMiniLabelStyle);
            EditorGUILayout.LabelField("ID", reference.itemId, UtilityWindowTheme.MutedMiniLabelStyle);

            if (PungentAuthoringProviderRegistry.TryGetPreview(reference, out PungentAuthoringPreview preview) && preview != null)
            {
                EditorGUILayout.LabelField("Title", preview.title, UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(preview.bodyPreview))
                    EditorGUILayout.LabelField("Preview", PreviewAuthoringPickerText(preview.bodyPreview, 120), EditorStyles.wordWrappedMiniLabel);
            }
            else if (!string.IsNullOrWhiteSpace(reference.label))
            {
                EditorGUILayout.LabelField("Label", reference.label, UtilityWindowTheme.MutedMiniLabelStyle);
            }

            if (!string.IsNullOrWhiteSpace(providerState))
                EditorGUILayout.HelpBox(providerState, MessageType.Warning);
        }

        private void DrawAdvancedCellReferenceFields(PungentDataSheet sheet, PungentAuthoringReference source)
        {
            string key = (_gridState.selectedCellRowId ?? string.Empty) + "|" + (_gridState.selectedCellColumnId ?? string.Empty) + "|" +
                         (source != null ? source.providerId + "|" + source.KindLabel + "|" + source.itemId + "|" + source.label : "empty");
            if (!string.Equals(_advancedCellRefKey, key, StringComparison.Ordinal))
            {
                _advancedCellRefKey = key;
                _advancedCellRefKind = source != null ? source.itemKind : PungentAuthoringItemKind.LegacyNote;
                _advancedCellRefProvider = source != null ? source.providerId ?? string.Empty : string.Empty;
                _advancedCellRefCustomKind = source != null ? source.customKind ?? string.Empty : string.Empty;
                _advancedCellRefItemId = source != null ? source.itemId ?? string.Empty : string.Empty;
                _advancedCellRefLabel = source != null ? source.label ?? string.Empty : string.Empty;
            }

            _advancedCellRefKind = (PungentAuthoringItemKind)EditorGUILayout.EnumPopup("Kind", _advancedCellRefKind);
            _advancedCellRefProvider = EditorGUILayout.TextField("Provider", _advancedCellRefProvider ?? string.Empty);
            _advancedCellRefCustomKind = EditorGUILayout.TextField("Custom Kind", _advancedCellRefCustomKind ?? string.Empty);
            _advancedCellRefItemId = EditorGUILayout.TextField("Item ID", _advancedCellRefItemId ?? string.Empty);
            _advancedCellRefLabel = EditorGUILayout.TextField("Label", _advancedCellRefLabel ?? string.Empty);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_advancedCellRefItemId)))
            {
                if (GUILayout.Button("Apply Advanced Ref"))
                {
                    AssignCellAuthoringReference(sheet, new PungentAuthoringReference
                    {
                        providerId = _advancedCellRefProvider,
                        itemKind = _advancedCellRefKind,
                        customKind = _advancedCellRefCustomKind,
                        itemId = _advancedCellRefItemId,
                        label = _advancedCellRefLabel
                    }, "Cell advanced link edited.");
                }
            }
        }

        private void AssignCellAuthoringReference(PungentDataSheet sheet, PungentAuthoringReference reference, string status)
        {
            if (sheet == null || !PungentDataSheetGridGUI.MaterializeSelectedCell(sheet, _gridState, out PungentDataSheetCell target))
                return;

            PungentAuthoringReference next = CloneAuthoringReference(reference);
            if (next != null)
                next.NormalizeInPlace();
            target.linkedAuthoringRef = next;
            target.displayValue = next == null ? target.rawValue ?? string.Empty : string.IsNullOrWhiteSpace(next.label) ? next.itemId : next.label;
            target.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
            target.propagationMessage = string.Empty;
            sheet.Touch();
            _gridState.InvalidateCache();
            MarkDirty(status, true);
        }

        private static PungentAuthoringReference CloneAuthoringReference(PungentAuthoringReference source)
        {
            if (source == null)
                return null;

            return new PungentAuthoringReference
            {
                providerId = source.providerId,
                itemKind = source.itemKind,
                customKind = source.customKind,
                itemId = source.itemId,
                label = source.label,
                sourceContext = source.sourceContext
            };
        }

        private void DrawAuthoringReferenceCellValueTools(PungentDataSheet sheet, PungentDataSheetCell cell)
        {
            PungentAuthoringReference parsed = null;
            bool parsedRaw = cell != null && PungentDataSheetAuthoringReferenceCodec.TryParseStructured(cell.rawValue, out parsed);
            bool hasLinked = cell != null && cell.linkedAuthoringRef != null && cell.linkedAuthoringRef.HasItemId;
            if (!parsedRaw && !hasLinked)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Link Encoding", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Raw link format: provider|kind|id|label. Parsed raw links are visible in the grid, but Use Raw As Link stores the structured cell reference.", MessageType.Info);
                if (parsedRaw)
                {
                    EditorGUILayout.LabelField("Parsed", parsed.KindLabel + " / " + parsed.itemId);
                    DrawAuthoringReferencePreview(parsed);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = parsedRaw;
                    if (GUILayout.Button("Use Raw As Link"))
                    {
                        if (PungentDataSheetGridGUI.MaterializeSelectedCell(sheet, _gridState, out PungentDataSheetCell target))
                        {
                            ApplyRawReferenceToCell(target, parsed);
                            sheet.Touch();
                            _gridState.InvalidateCache();
                        }
                        MarkDirty("Cell raw reference linked.", true);
                    }
                    GUI.enabled = hasLinked;
                    if (GUILayout.Button("Write Link To Raw"))
                    {
                        SetCellValue(cell, PungentDataSheetAuthoringReferenceCodec.Format(cell.linkedAuthoringRef));
                        sheet.Touch();
                        _gridState.InvalidateCache();
                        MarkDirty("Cell link written to raw value.", true);
                    }
                    GUI.enabled = true;
                }
            }
        }

        private static void ApplyRawReferenceToCell(PungentDataSheetCell cell, PungentAuthoringReference reference)
        {
            if (cell == null || reference == null)
                return;

            reference.NormalizeInPlace();
            cell.linkedAuthoringRef = reference;
            cell.displayValue = string.IsNullOrWhiteSpace(reference.label) ? reference.itemId : reference.label;
            cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
            cell.propagationMessage = string.Empty;
        }

        private void DrawEnumCellValueTools(PungentDataSheet sheet, PungentDataSheetColumn column, PungentDataSheetCell cell)
        {
            List<string> options = CollectEnumOptions(sheet, column);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Enum Values", EditorStyles.boldLabel);
                if (options.Count == 0)
                {
                    EditorGUILayout.HelpBox("No enum values are known yet. Add comma-separated options on the column, or enter values in this column and use Harvest Existing Values.", MessageType.Info);
                }
                else
                {
                    string current = cell != null ? cell.rawValue ?? string.Empty : string.Empty;
                    int selected = Mathf.Max(0, options.FindIndex(option => string.Equals(option, current, StringComparison.OrdinalIgnoreCase)));
                    if (selected < 0)
                        selected = 0;
                    EditorGUI.BeginChangeCheck();
                    selected = EditorGUILayout.Popup("Apply Value", selected, options.ToArray());
                    if (EditorGUI.EndChangeCheck() && selected >= 0 && selected < options.Count)
                        ApplyEnumValueToSelectedCell(sheet, options[selected]);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Harvest Existing Values"))
                    {
                        column.enumOptions = CollectEnumOptions(sheet, column);
                        column.NormalizeInPlace();
                        sheet.Touch();
                        _gridState.InvalidateCache();
                        MarkDirty("Enum options harvested.", true);
                    }

                    using (new EditorGUI.DisabledScope(cell == null || string.IsNullOrWhiteSpace(cell.rawValue)))
                    {
                        if (GUILayout.Button("Add Current To Options"))
                        {
                            column.enumOptions = PungentAuthoringMetadata.NormalizeTags(column.enumOptions);
                            if (!column.enumOptions.Exists(option => string.Equals(option, cell.rawValue, StringComparison.OrdinalIgnoreCase)))
                                column.enumOptions.Add(cell.rawValue);
                            column.NormalizeInPlace();
                            sheet.Touch();
                            _gridState.InvalidateCache();
                            MarkDirty("Enum option added.", true);
                        }
                    }
                }
            }
        }

        private void ApplyEnumValueToSelectedCell(PungentDataSheet sheet, string value)
        {
            if (!PungentDataSheetGridGUI.MaterializeSelectedCell(sheet, _gridState, out PungentDataSheetCell cell))
                return;

            SetCellValue(cell, value);
            sheet.Touch();
            _gridState.InvalidateCache();
            OnSheetCellEdited(sheet, cell, "Enum cell edited.");
        }

        private static List<string> CollectEnumOptions(PungentDataSheet sheet, PungentDataSheetColumn column)
        {
            List<string> options = PungentAuthoringMetadata.NormalizeTags(column != null ? column.enumOptions : null);
            if (sheet == null || column == null)
                return options;

            foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
            {
                if (cell == null || !PungentAuthoringId.EqualsId(cell.columnId, column.id))
                    continue;
                string value = cell.rawValue == null ? string.Empty : cell.rawValue.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !options.Exists(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)))
                    options.Add(value);
            }

            return options;
        }

        private void DrawSelectedObjectValueTools(PungentDataSheet sheet, PungentDataSheetCell cell)
        {
            if (sheet == null || !_gridState.HasCellSelection)
                return;

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Project Value", EditorStyles.boldLabel);
                UnityEngine.Object target = Selection.activeObject;
                EditorGUILayout.ObjectField("Selected Object", target, typeof(UnityEngine.Object), true);

                EditorGUI.BeginChangeCheck();
                _serializedPropertyPath = EditorGUILayout.TextField("Property Path", _serializedPropertyPath ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(target == null);
                    if (GUILayout.Button("Link Selection"))
                        LinkSelectedObjectToCell(sheet);
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(target == null || string.IsNullOrWhiteSpace(_serializedPropertyPath));
                    if (GUILayout.Button("Read -> Cell"))
                        ReadSelectedPropertyIntoCell(sheet);

                    string raw = cell != null ? cell.rawValue ?? string.Empty : string.Empty;
                    EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(raw));
                    if (GUILayout.Button("Cell -> Property"))
                        ApplyCellToSelectedProperty(sheet, raw);
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        private void LinkSelectedObjectToCell(PungentDataSheet sheet)
        {
            UnityEngine.Object target = Selection.activeObject;
            if (sheet == null || target == null || !_gridState.HasCellSelection)
                return;

            string id = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            if (string.IsNullOrWhiteSpace(id))
                id = target.GetInstanceID().ToString();

            if (!PungentDataSheetGridGUI.MaterializeSelectedCell(sheet, _gridState, out PungentDataSheetCell cell))
                return;

            cell.linkedAuthoringRef = PungentAuthoringReference.Create(PungentAuthoringItemKind.ExternalReference, id, PungentDataSheetUnityObjectReferenceProvider.Id, target.name);
            cell.linkedAuthoringRef.sourceContext = "Unity Selection";
            sheet.Touch();
            _gridState.InvalidateCache();
            MarkDirty("Linked selected object to cell.", true);
        }

        private void ReadSelectedPropertyIntoCell(PungentDataSheet sheet)
        {
            UnityEngine.Object target = Selection.activeObject;
            if (sheet == null || target == null || !_gridState.HasCellSelection)
                return;

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(_serializedPropertyPath);
            if (property == null)
            {
                _status = "Property path was not found.";
                return;
            }

            string value = ReadSerializedProperty(property);
            if (!PungentDataSheetGridGUI.MaterializeSelectedCell(sheet, _gridState, out PungentDataSheetCell cell))
                return;

            SetCellValue(cell, value);
            sheet.Touch();
            _gridState.InvalidateCache();
            MarkDirty("Read selected property into cell.", true);
        }

        private void ApplyCellToSelectedProperty(PungentDataSheet sheet, string rawValue)
        {
            UnityEngine.Object target = Selection.activeObject;
            if (sheet == null || target == null || !_gridState.HasCellSelection)
                return;

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(_serializedPropertyPath);
            if (property == null)
            {
                _status = "Property path was not found.";
                return;
            }

            if (!EditorUtility.DisplayDialog("Apply Cell To Property", "Apply the selected cell value to '" + target.name + "' at property path '" + _serializedPropertyPath + "'?", "Apply", "Cancel"))
                return;

            Undo.RecordObject(target, "Apply Data Sheet Cell");
            if (!TryWriteSerializedProperty(property, rawValue, out string error))
            {
                _status = error;
                return;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            _status = "Applied cell value to selected object.";
        }

        private static string ReadSerializedProperty(SerializedProperty property)
        {
            if (property == null)
                return string.Empty;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? property.objectReferenceValue.name : string.Empty;
                default:
                    return property.propertyType.ToString();
            }
        }

        private static bool TryWriteSerializedProperty(SerializedProperty property, string value, out string error)
        {
            error = string.Empty;
            if (property == null)
            {
                error = "Property path was not found.";
                return false;
            }

            value = value ?? string.Empty;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (int.TryParse(value, out int intValue))
                    {
                        property.intValue = intValue;
                        return true;
                    }
                    error = "Cell value is not a valid integer.";
                    return false;
                case SerializedPropertyType.Boolean:
                    if (bool.TryParse(value, out bool boolValue))
                    {
                        property.boolValue = boolValue;
                        return true;
                    }
                    if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        property.boolValue = true;
                        return true;
                    }
                    if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
                    {
                        property.boolValue = false;
                        return true;
                    }
                    error = "Cell value is not a valid boolean.";
                    return false;
                case SerializedPropertyType.Float:
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatValue))
                    {
                        property.floatValue = floatValue;
                        return true;
                    }
                    error = "Cell value is not a valid number.";
                    return false;
                case SerializedPropertyType.String:
                    property.stringValue = value;
                    return true;
                case SerializedPropertyType.Enum:
                    int index = Array.FindIndex(property.enumDisplayNames, item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
                    if (index < 0 && int.TryParse(value, out int parsedIndex))
                        index = parsedIndex;
                    if (index >= 0 && index < property.enumDisplayNames.Length)
                    {
                        property.enumValueIndex = index;
                        return true;
                    }
                    error = "Cell value does not match this enum.";
                    return false;
                default:
                    error = "This property type is not supported by the MVP apply tool.";
                    return false;
            }
        }

        private bool DrawAuthoringReferenceEditor(string title, PungentAuthoringReference reference, Action<PungentAuthoringReference> assign)
        {
            bool changed = false;
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (reference != null && GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48f)))
                    {
                        assign(null);
                        return true;
                    }
                }

                if (reference == null)
                {
                    if (GUILayout.Button("Add Link"))
                    {
                        assign(new PungentAuthoringReference { itemKind = PungentAuthoringItemKind.LegacyNote });
                        return true;
                    }

                    return false;
                }

                EditorGUI.BeginChangeCheck();
                reference.itemKind = (PungentAuthoringItemKind)EditorGUILayout.EnumPopup("Kind", reference.itemKind);
                reference.providerId = EditorGUILayout.TextField("Provider", reference.providerId);
                reference.customKind = EditorGUILayout.TextField("Custom Kind", reference.customKind);
                reference.itemId = EditorGUILayout.TextField("Item ID", reference.itemId);
                reference.label = EditorGUILayout.TextField("Label", reference.label);
                if (EditorGUI.EndChangeCheck())
                {
                    reference.NormalizeInPlace();
                    changed = true;
                }

                DrawAuthoringReferencePicker(reference, ref changed);
                DrawAuthoringReferencePreview(reference);
            }

            if (changed)
                assign(reference);
            return changed;
        }

        private void DrawAuthoringReferencePicker(PungentAuthoringReference reference, ref bool changed)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Authoring Picker", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Refresh", "Enumerate registered authoring providers once. This does not run during repaint."), GUILayout.Width(76f)))
                        RefreshAuthoringPicker();

                    using (new EditorGUI.DisabledScope(!_authoringPickerLoaded))
                    {
                        if (GUILayout.Button(new GUIContent("Clear", "Clear cached picker results."), GUILayout.Width(56f)))
                        {
                            _authoringPickerCandidates.Clear();
                            _authoringPickerLoaded = false;
                            _authoringPickerStatus = "Picker cache cleared.";
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                _authoringPickerSearch = EditorGUILayout.TextField(new GUIContent("Filter", "Filter cached authoring items by title, ID, kind, provider, status, or tags."), _authoringPickerSearch);
                EditorGUILayout.LabelField(_authoringPickerStatus, UtilityWindowTheme.MutedMiniLabelStyle);

                if (!_authoringPickerLoaded)
                {
                    EditorGUILayout.HelpBox("Press Refresh to list linkable notes, utilities, tokens, help topics, audit issues, and data sheets from installed providers. The list is cached until refreshed.", MessageType.Info);
                    return;
                }

                int shown = 0;
                int matches = 0;
                _authoringPickerScroll = EditorGUILayout.BeginScrollView(_authoringPickerScroll, GUILayout.MinHeight(92f), GUILayout.MaxHeight(220f));
                for (int i = 0; i < _authoringPickerCandidates.Count; i++)
                {
                    AuthoringPickerCandidate candidate = _authoringPickerCandidates[i];
                    if (!MatchesAuthoringPickerSearch(candidate))
                        continue;

                    matches++;
                    if (shown >= MaxVisibleAuthoringPickerItems)
                        continue;

                    DrawAuthoringPickerCandidate(candidate, reference, ref changed);
                    shown++;
                }
                EditorGUILayout.EndScrollView();

                if (matches > shown)
                    EditorGUILayout.LabelField((matches - shown) + " more cached item(s). Refine the filter to narrow the list.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void RefreshAuthoringPicker()
        {
            _authoringPickerCandidates.Clear();
            int providerCount = 0;
            int errorCount = 0;

            foreach (IPungentAuthoringProvider provider in PungentAuthoringProviderRegistry.GetProviders())
            {
                if (provider == null || (provider.Capabilities & PungentAuthoringProviderCapabilities.EnumerateItems) == 0)
                    continue;

                providerCount++;
                try
                {
                    foreach (PungentAuthoringMetadata metadata in provider.EnumerateItems() ?? Enumerable.Empty<PungentAuthoringMetadata>())
                    {
                        if (metadata == null)
                            continue;

                        metadata.NormalizeInPlace();
                        if (string.IsNullOrWhiteSpace(metadata.id))
                            continue;

                        if (string.IsNullOrWhiteSpace(metadata.sourceProviderId))
                            metadata.sourceProviderId = provider.ProviderId;

                        _authoringPickerCandidates.Add(new AuthoringPickerCandidate
                        {
                            metadata = metadata,
                            providerId = provider.ProviderId,
                            providerName = provider.DisplayName,
                            searchText = BuildAuthoringPickerSearchText(metadata, provider)
                        });

                        if (_authoringPickerCandidates.Count >= MaxAuthoringPickerItems)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Debug.LogWarning("PungentFunk Data Sheet authoring picker could not enumerate provider '" + provider.ProviderId + "': " + ex.Message);
                }

                if (_authoringPickerCandidates.Count >= MaxAuthoringPickerItems)
                    break;
            }

            _authoringPickerCandidates.Sort((a, b) =>
            {
                int kind = string.Compare(a.metadata.KindLabel, b.metadata.KindLabel, StringComparison.OrdinalIgnoreCase);
                return kind != 0 ? kind : string.Compare(a.metadata.title, b.metadata.title, StringComparison.OrdinalIgnoreCase);
            });

            _authoringPickerLoaded = true;
            _authoringPickerStatus = "Cached " + _authoringPickerCandidates.Count + " item(s) from " + providerCount + " provider(s)" +
                                     (_authoringPickerCandidates.Count >= MaxAuthoringPickerItems ? " (limit reached)" : string.Empty) +
                                     (errorCount > 0 ? " with " + errorCount + " provider warning(s)." : ".");
        }

        private void DrawAuthoringPickerCandidate(AuthoringPickerCandidate candidate, PungentAuthoringReference reference, ref bool changed)
        {
            if (candidate == null || candidate.metadata == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(candidate.metadata.title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Link", "Link the selected row/cell/column to this authoring item."), GUILayout.Width(54f)))
                    {
                        ApplyAuthoringPickerCandidate(candidate, reference);
                        changed = true;
                    }
                }

                string subtitle = candidate.metadata.KindLabel + " / " + candidate.providerName;
                if (!string.IsNullOrWhiteSpace(candidate.metadata.status))
                    subtitle += " / " + candidate.metadata.status;
                EditorGUILayout.LabelField(subtitle, UtilityWindowTheme.MutedMiniLabelStyle);

                if (!string.IsNullOrWhiteSpace(candidate.metadata.summary))
                    EditorGUILayout.LabelField(PreviewAuthoringPickerText(candidate.metadata.summary, 140), EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void ApplyAuthoringPickerCandidate(AuthoringPickerCandidate candidate, PungentAuthoringReference reference)
        {
            if (candidate == null || candidate.metadata == null || reference == null)
                return;

            reference.itemKind = candidate.metadata.kind;
            reference.customKind = candidate.metadata.customKind;
            reference.itemId = candidate.metadata.id;
            reference.providerId = string.IsNullOrWhiteSpace(candidate.metadata.sourceProviderId) ? candidate.providerId : candidate.metadata.sourceProviderId;
            reference.label = candidate.metadata.title;
            reference.sourceContext = candidate.metadata.packageCapabilityId;
            reference.NormalizeInPlace();
        }

        private bool MatchesAuthoringPickerSearch(AuthoringPickerCandidate candidate)
        {
            if (candidate == null)
                return false;

            string query = (_authoringPickerSearch ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(query) ||
                   (!string.IsNullOrWhiteSpace(candidate.searchText) &&
                    candidate.searchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildAuthoringPickerSearchText(PungentAuthoringMetadata metadata, IPungentAuthoringProvider provider)
        {
            IEnumerable<string> parts = new[]
            {
                metadata.id,
                metadata.title,
                metadata.summary,
                metadata.KindLabel,
                metadata.status,
                metadata.priority,
                metadata.visibility,
                metadata.sourceProviderId,
                provider != null ? provider.ProviderId : string.Empty,
                provider != null ? provider.DisplayName : string.Empty
            }.Concat(metadata.tags ?? new List<string>());

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray());
        }

        private static string PreviewAuthoringPickerText(string value, int maxLength)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace("\r", " ").Replace("\n", " ");
            return clean.Length <= maxLength ? clean : clean.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
        }

        private void DrawAuthoringReferencePreview(PungentAuthoringReference reference)
        {
            if (reference == null || !reference.HasItemId)
            {
                EditorGUILayout.HelpBox("Paste an authoring item ID to link this row or cell.", MessageType.Info);
                return;
            }

            string providerState = ProviderState(reference);
            if (!string.IsNullOrEmpty(providerState))
                EditorGUILayout.HelpBox(providerState, MessageType.Warning);

            PungentAuthoringProviderRegistry.TryGetPreview(reference, out PungentAuthoringPreview preview);
            if (preview != null)
            {
                EditorGUILayout.LabelField("Linked Item Kind", reference.KindLabel);
                EditorGUILayout.LabelField("Title", preview.title);
                if (!string.IsNullOrWhiteSpace(preview.bodyPreview))
                    EditorGUILayout.HelpBox(preview.bodyPreview, preview.missing ? MessageType.Warning : MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = string.IsNullOrEmpty(providerState);
                if (GUILayout.Button("Open Linked Item"))
                {
                    bool opened = PungentAuthoringProviderRegistry.TryOpen(reference);
                    _status = opened ? "Opened linked item." : "Linked item could not be opened.";
                }
                GUI.enabled = true;

                if (GUILayout.Button("Copy ID"))
                    EditorGUIUtility.systemCopyBuffer = reference.itemId;
            }
        }

        private string ProviderState(PungentAuthoringReference reference)
        {
            if (reference == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(reference.providerId))
                return PungentAuthoringProviderRegistry.FindProvider(reference.providerId) == null
                    ? PungentAuthoringProviderRegistry.MissingProviderMessage(reference)
                    : string.Empty;

            return PungentAuthoringProviderRegistry.GetProvidersForKind(reference.itemKind).Count == 0
                ? PungentAuthoringProviderRegistry.MissingProviderMessage(reference)
                : string.Empty;
        }

        private void DrawValidationSummary()
        {
            if (_lastValidation == null)
                return;

            MessageType messageType = _lastValidation.status == PungentAuthoringValidationStatus.Valid ? MessageType.Info :
                _lastValidation.status == PungentAuthoringValidationStatus.ValidWithWarnings ? MessageType.Warning :
                MessageType.Error;
            EditorGUILayout.HelpBox("Validation: " + _lastValidation.status + " (" + _lastValidation.issues.Count + " issue(s))", messageType);
            for (int i = 0; i < Mathf.Min(8, _lastValidation.issues.Count); i++)
            {
                PungentAuthoringValidationIssue issue = _lastValidation.issues[i];
                if (issue != null)
                    EditorGUILayout.LabelField(issue.issueCode, issue.message, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void CreateSheetFromTemplate(PungentDataSheetTemplateDefinition template)
        {
            FlushGridEdit();
            PungentDataSheet sheet = PungentDataSheetEditorStorage.CreateSheet(template != null ? template.title : "Blank Sheet");
            PungentDataSheetTemplates.ApplyTemplate(sheet, template ?? PungentDataSheetTemplates.GetTemplates()[0]);
            SelectSheet(sheet.id);
            MarkDirty("Created " + sheet.title + ".", true);
        }

        private void ValidateChecklistDefinition(PungentDataSheet sheet)
        {
            if (PungentDataSheetChecklistBridge.TryBuildChecklistDefinition(sheet, out PungentChecklistDefinition checklist, out _, out string error))
            {
                int sectionCount = checklist.sections == null ? 0 : checklist.sections.Count;
                int itemCount = PungentChecklistSerialization.EnumerateItems(checklist).Count();
                _status = "Checklist definition valid: " + checklist.checklistId + " (" + sectionCount + " sections, " + itemCount + " items).";
                return;
            }

            _status = "Checklist definition invalid: " + error;
            EditorUtility.DisplayDialog("Validate Checklist Definition", error, "OK");
        }

        private void CreateOrUpdateChecklistDefinition(PungentDataSheet sheet)
        {
            if (PungentDataSheetChecklistBridge.CreateOrUpdateChecklistDefinition(sheet, out string checklistId, out string error))
            {
                MarkDirty("Created/updated checklist definition '" + checklistId + "'.", true);
                PungentChecklistUtilityWindow.OpenChecklist(checklistId);
                return;
            }

            _status = "Checklist definition not saved: " + error;
            EditorUtility.DisplayDialog("Create Or Update Checklist Definition", error, "OK");
        }

        private void OpenLinkedChecklist(PungentDataSheet sheet)
        {
            PungentAuthoringReference reference = sheet?.references == null
                ? null
                : sheet.references.FirstOrDefault(item => item != null && item.itemKind == PungentAuthoringItemKind.Checklist && !string.IsNullOrWhiteSpace(item.itemId));
            if (reference == null)
            {
                _status = "No linked checklist reference on this sheet.";
                return;
            }

            PungentChecklistUtilityWindow.OpenChecklist(reference.itemId);
        }

        private void SyncLinkedChecklistToSheet(PungentDataSheet sheet)
        {
            if (PungentDataSheetChecklistBridge.TrySyncLinkedChecklistToSheet(sheet, out string checklistId, out string error))
            {
                _gridState.InvalidateCache();
                MarkDirty("Synced checklist '" + checklistId + "' into the current sheet.", true);
                return;
            }

            _status = "Checklist sync failed: " + error;
            EditorUtility.DisplayDialog("Sync Linked Checklist To Sheet", error, "OK");
        }

        private void CreateSheetFromLinkedChecklist(PungentDataSheet sheet)
        {
            if (!PungentDataSheetChecklistBridge.TryFindLinkedChecklist(sheet, out PungentChecklistDefinition checklist, out string error))
            {
                _status = "Checklist sheet not created: " + error;
                EditorUtility.DisplayDialog("Create Sheet From Linked Checklist", error, "OK");
                return;
            }

            if (PungentDataSheetChecklistBridge.TryCreateSheetFromChecklist(checklist.checklistId, out PungentDataSheet created, out error))
            {
                SelectSheet(created.id);
                _gridState.InvalidateCache();
                MarkDirty("Created checklist sheet for '" + checklist.checklistId + "'.", true);
                return;
            }

            _status = "Checklist sheet not created: " + error;
            EditorUtility.DisplayDialog("Create Sheet From Linked Checklist", error, "OK");
        }

        private void DuplicateCurrentSheet()
        {
            FlushGridEdit();
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null)
                return;

            PungentDataSheet copy = PungentDataSheetEditorStorage.Database.DuplicateSheet(sheet);
            if (copy == null)
                return;

            SelectSheet(copy.id);
            MarkDirty("Duplicated sheet.", true);
        }

        private void DeleteCurrentSheet()
        {
            FlushGridEdit();
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null)
                return;

            if (!EditorUtility.DisplayDialog("Delete Data Sheet", "Delete '" + sheet.title + "'? This only removes the Data Sheet storage record.", "Delete", "Cancel"))
                return;

            PungentDataSheetEditorStorage.DeleteSheet(sheet.id);
            _closedSheetIds.Remove(PungentAuthoringId.Normalize(sheet.id));
            _selectedSheetId = string.Empty;
            EnsureSelection();
            _status = "Deleted sheet.";
        }

        private void OpenStickyNotes()
        {
            PungentDataSheetProviderRegistration.RegisterProvider();
            PungentNotesRoadmapWindow.Open();
            _status = "Opened Sticky Notes.";
        }

        private void OpenOrCreateSheetNote(PungentDataSheet sheet)
        {
            if (sheet == null)
                return;

            // RDE/STICKY-NOTES MIGRATION NOTE: Data Sheets keeps legacy note references for compatibility, but future work should treat this as a lightweight sticky-note bridge, not a dependency on the old Notes browser.
            PungentNoteStorage.EnsureLoaded();
            PungentNote note = FindSheetNote(sheet.id);
            if (note == null)
            {
                note = PungentNoteStorage.Database.CreateNote("Data Sheet Sticky: " + (string.IsNullOrWhiteSpace(sheet.title) ? sheet.id : sheet.title), PungentNoteKind.ProjectNote);
                note.linkedUtilityId = "data-sheet-editor";
                note.stableKey = SheetNoteStableKeyPrefix + sheet.id;
                note.visibility = PungentNoteVisibility.PrivateProject;
                note.tags = PungentAuthoringMetadata.NormalizeTags(new[] { "data-sheet", "spreadsheet", "csv" });
                note.body = "Data Sheet ID: " + sheet.id + "\n\n" + (sheet.summary ?? string.Empty);
                PungentNoteStorage.Database.Touch(note);
                PungentNoteStorage.Save();
            }

            PungentNotesRoadmapWindow.OpenAndSelect(note.id);
            _status = "Opened sheet sticky note.";
        }

        private static PungentNote FindSheetNote(string sheetId)
        {
            if (string.IsNullOrWhiteSpace(sheetId))
                return null;

            string stableKey = SheetNoteStableKeyPrefix + sheetId;
            return PungentNoteStorage.Database.notes.FirstOrDefault(note =>
                note != null &&
                !note.archived &&
                (string.Equals(note.stableKey, stableKey, StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrWhiteSpace(note.body) && note.body.IndexOf("Data Sheet ID: " + sheetId, StringComparison.OrdinalIgnoreCase) >= 0)));
        }

        private void AddRow()
        {
            FlushGridEdit();
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null)
                return;

            PungentDataSheetRow row = sheet.AddRow("Row " + (sheet.rows.Count + 1));
            _gridState.selectedRowId = row.id;
            _gridState.selectedCellRowId = string.Empty;
            MarkDirty("Added row.", true);
        }

        private void AddColumn()
        {
            FlushGridEdit();
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null)
                return;

            PungentDataSheetColumn column = sheet.AddColumn("Column " + (sheet.columns.Count + 1));
            _gridState.selectedColumnId = column.id;
            _gridState.selectedCellColumnId = string.Empty;
            MarkDirty("Added column.", true);
        }

        private void MoveRow(PungentDataSheetRow row, int delta)
        {
            FlushGridEdit();
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null || row == null || sheet.rows == null)
                return;

            int index = sheet.rows.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, row.id));
            int nextIndex = Mathf.Clamp(index + delta, 0, sheet.rows.Count - 1);
            if (index < 0 || index == nextIndex)
                return;

            sheet.rows.RemoveAt(index);
            sheet.rows.Insert(nextIndex, row);
            sheet.ReindexRows();
            sheet.Touch();
            _gridState.selectedRowId = row.id;
            _gridState.InvalidateCache();
            MarkDirty("Moved row.", true);
        }

        private void MoveColumn(PungentDataSheetColumn column, int delta)
        {
            FlushGridEdit();
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null || column == null || sheet.columns == null)
                return;

            int index = sheet.columns.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, column.id));
            int nextIndex = Mathf.Clamp(index + delta, 0, sheet.columns.Count - 1);
            if (index < 0 || index == nextIndex)
                return;

            sheet.columns.RemoveAt(index);
            sheet.columns.Insert(nextIndex, column);
            sheet.Touch();
            _gridState.selectedColumnId = column.id;
            _gridState.InvalidateCache();
            MarkDirty("Moved column.", true);
        }

        private void ImportCsv()
        {
            OpenDataSheetOverlay(DataSheetOverlayKind.CsvImport, Rect.zero);
        }

        private void ExportCsv()
        {
            if (SelectedSheet == null)
                return;

            OpenDataSheetOverlay(DataSheetOverlayKind.CsvExport, Rect.zero);
        }

        private void ClearCsvImportPreview()
        {
            _csvImportPreviewSummary = string.Empty;
            _csvImportPreviewValid = false;
        }

        private void PreviewCsvImport(PungentDataSheet currentSheet)
        {
            PungentDataSheet target = _csvImportIntoCurrent ? currentSheet : null;
            if (PungentDataSheetCsvUtility.TryPreviewImport(
                    target,
                    _csvImportPath,
                    _csvImportFirstRowIsHeaders,
                    _csvImportColumnMode,
                    out PungentDataSheetCsvUtility.ImportPreview preview))
            {
                _csvImportPreviewValid = true;
                _csvImportPreviewSummary = preview.Summary;
                return;
            }

            _csvImportPreviewValid = false;
            _csvImportPreviewSummary = preview != null && !string.IsNullOrWhiteSpace(preview.error)
                ? preview.error
                : "CSV preview failed.";
        }

        private void ExecuteCsvImport(PungentDataSheet currentSheet)
        {
            if (PungentDataSheetEditorStorage.Dirty && _csvImportSaveBefore)
                PungentDataSheetEditorStorage.SaveNow();

            if (_csvImportIntoCurrent && currentSheet != null)
            {
                if (PungentDataSheetCsvUtility.ImportIntoExisting(currentSheet, _csvImportPath, _csvImportFirstRowIsHeaders, _csvImportColumnMode, out string summary))
                {
                    _gridState.InvalidateCache();
                    MarkDirty(summary, true);
                    _status = summary;
                    CloseDataSheetOverlay();
                    ClearCsvImportPreview();
                }
                else
                {
                    _status = summary;
                }

                return;
            }

            PungentDataSheet sheet = PungentDataSheetCsvUtility.ImportFromCsv(_csvImportPath, _csvImportFirstRowIsHeaders);
            if (sheet == null)
            {
                _status = "CSV import failed.";
                return;
            }

            PungentDataSheetEditorStorage.UpsertSheet(sheet, true);
            SelectSheet(sheet.id);
            CloseDataSheetOverlay();
            ClearCsvImportPreview();
            _status = "Imported CSV into new sheet.";
        }

        private void ExecuteCsvExport(PungentDataSheet sheet)
        {
            if (sheet == null)
                return;

            string defaultName = string.IsNullOrWhiteSpace(sheet.title) ? "DataSheet.csv" : sheet.title + ".csv";
            string path = EditorUtility.SaveFilePanel("Export Data Sheet CSV", string.Empty, defaultName, "csv");
            if (string.IsNullOrWhiteSpace(path))
                return;

            PungentDataSheetCsvUtility.ExportToCsv(sheet, path, new PungentDataSheetCsvUtility.ExportOptions
            {
                visibleColumnsOnly = _csvExportVisibleColumnsOnly,
                includeHiddenRows = _csvExportIncludeHiddenRows,
                useDisplayValues = _csvExportDisplayValues,
                dateDisplayFormat = _gridState.dateDisplayFormat
            });
            CloseDataSheetOverlay();
            _status = "Exported CSV.";
        }

        private void ValidateCurrentSheet(bool markDirty)
        {
            FlushGridEdit();
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null)
                return;

            _lastValidation = PungentDataSheetValidation.ValidateSheet(sheet);
            _status = "Validation " + _lastValidation.status + ".";
            if (markDirty)
                PungentDataSheetEditorStorage.MarkDirty(true);
        }

        private void SaveCurrentSheet()
        {
            ValidateCurrentSheet(false);
            PungentDataSheetEditorStorage.MarkDirty(false);
            PungentDataSheetEditorStorage.SaveNow();
            _status = "Saved Data Sheets.";
        }

        private void SelectSheet(string sheetId)
        {
            if (!PungentAuthoringId.EqualsId(_selectedSheetId, sheetId))
                FlushGridEdit();

            _selectedSheetId = sheetId ?? string.Empty;
            _closedSheetIds.Remove(PungentAuthoringId.Normalize(_selectedSheetId));
            _renamingSheetId = string.Empty;
            _renamingSheetTitle = string.Empty;
            _gridState.selectedRowId = string.Empty;
            _gridState.selectedColumnId = string.Empty;
            _gridState.selectedCellRowId = string.Empty;
            _gridState.selectedCellColumnId = string.Empty;
            _lastValidation = null;
            SavePrefs();
            Repaint();
        }

        private void CloseCurrentSheet()
        {
            if (string.IsNullOrWhiteSpace(_selectedSheetId))
                return;

            CloseSheet(_selectedSheetId);
        }

        private void CloseAllSheets()
        {
            FlushGridEdit();
            foreach (PungentDataSheet sheet in PungentDataSheetEditorStorage.Database.sheets ?? new List<PungentDataSheet>())
                if (sheet != null && !string.IsNullOrWhiteSpace(sheet.id))
                    _closedSheetIds.Add(PungentAuthoringId.Normalize(sheet.id));

            _selectedSheetId = string.Empty;
            _renamingSheetId = string.Empty;
            _renamingSheetTitle = string.Empty;
            _lastValidation = null;
            ClearSheetTabDrag();
            SavePrefs();
            Repaint();
        }

        private void CloseSheet(string sheetId)
        {
            sheetId = PungentAuthoringId.Normalize(sheetId);
            if (string.IsNullOrWhiteSpace(sheetId))
                return;

            FlushGridEdit();
            _closedSheetIds.Add(sheetId);
            if (PungentAuthoringId.EqualsId(_selectedSheetId, sheetId))
            {
                PungentDataSheet next = PungentDataSheetEditorStorage.Database.sheets.FirstOrDefault(sheet => sheet != null && !IsSheetClosed(sheet.id));
                _selectedSheetId = next != null ? next.id : string.Empty;
                _gridState.selectedRowId = string.Empty;
                _gridState.selectedColumnId = string.Empty;
                _gridState.selectedCellRowId = string.Empty;
                _gridState.selectedCellColumnId = string.Empty;
                _lastValidation = null;
            }

            if (PungentAuthoringId.EqualsId(_renamingSheetId, sheetId))
            {
                _renamingSheetId = string.Empty;
                _renamingSheetTitle = string.Empty;
            }

            ClearSheetTabDrag();
            SavePrefs();
            Repaint();
        }

        private void EnsureSelection()
        {
            if (SelectedSheet != null)
                return;

            PungentDataSheet first = PungentDataSheetEditorStorage.Database.sheets.FirstOrDefault(sheet => sheet != null && !IsSheetClosed(sheet.id));
            _selectedSheetId = first != null ? first.id : string.Empty;
        }

        private bool IsSheetClosed(string sheetId)
        {
            return !string.IsNullOrWhiteSpace(sheetId) && _closedSheetIds.Contains(PungentAuthoringId.Normalize(sheetId));
        }

        private void MarkDirty(string status, bool scheduleSave)
        {
            _status = status;
            PungentDataSheetEditorStorage.MarkDirty(scheduleSave);
        }

        private void FlushGridEdit()
        {
            PungentDataSheet sheet = SelectedSheet;
            if (sheet == null)
                return;

            PungentDataSheetGridGUI.CommitActiveEdit(sheet, _gridState, cell => MarkDirty(cell == null ? "Sheet edited." : "Cell edited.", true));
        }

        private void SaveBeforeReload()
        {
            FlushGridEdit();
            if (PungentDataSheetEditorStorage.Dirty)
                PungentDataSheetEditorStorage.SaveNow();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSelectedSheet, _selectedSheetId ?? string.Empty);
            UtilityWindowPrefs.SetFloat(PrefRightWidth, _rightWidth);
            UtilityWindowPrefs.SetString(PrefGridFilter, _gridState.filterText ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefSelectedRow, _gridState.selectedRowId ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefSelectedColumn, _gridState.selectedColumnId ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefSelectedCellRow, _gridState.selectedCellRowId ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefSelectedCellColumn, _gridState.selectedCellColumnId ?? string.Empty);
            UtilityWindowPrefs.SetFloat(PrefGridScrollX, _gridState.scroll.x);
            UtilityWindowPrefs.SetFloat(PrefGridScrollY, _gridState.scroll.y);
            UtilityWindowPrefs.SetBool(PrefShowInspector, _showInspector);
            UtilityWindowPrefs.SetString(PrefSerializedPropertyPath, _serializedPropertyPath ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefClosedSheetIds, EncodeClosedSheets());
            UtilityWindowPrefs.SetString(PrefDateDisplayFormat, string.IsNullOrWhiteSpace(_gridState.dateDisplayFormat) ? "dd/MM/yyyy" : _gridState.dateDisplayFormat);
        }

        private void DecodeClosedSheets(string encoded)
        {
            _closedSheetIds.Clear();
            foreach (string id in (encoded ?? string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalized = PungentAuthoringId.Normalize(id);
                if (!string.IsNullOrWhiteSpace(normalized))
                    _closedSheetIds.Add(normalized);
            }
        }

        private string EncodeClosedSheets()
        {
            return string.Join("|", _closedSheetIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray());
        }

        private void ClampPanelWidths()
        {
            float available = Mathf.Max(MinCenterWidth + MinRightWidth + 24f, position.width - 24f);
            _rightWidth = Mathf.Clamp(_rightWidth, MinRightWidth, Mathf.Max(MinRightWidth, available - MinCenterWidth - 18f));
        }

        private float MaxRightWidth()
        {
            return Mathf.Max(MinRightWidth, position.width - MinCenterWidth - 42f);
        }

        private static void SetCellValue(PungentDataSheetCell cell, string value)
        {
            if (cell == null)
                return;

            cell.rawValue = value ?? string.Empty;
            cell.displayValue = cell.rawValue;
            cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
            cell.propagationStatus = string.Equals(cell.rawValue, cell.lastPulledValue, StringComparison.Ordinal)
                ? PungentDataSheetPropagationStatus.Clean
                : PungentDataSheetPropagationStatus.Changed;
            cell.propagationMessage = string.Empty;
        }

        private string SelectedCellAddress(PungentDataSheet sheet)
        {
            return PungentDataSheetGridGUI.AddressFor(sheet, _gridState);
        }

        private static string ColumnLabel(int index)
        {
            if (index < 0)
                return string.Empty;

            string label = string.Empty;
            index++;
            while (index > 0)
            {
                int remainder = (index - 1) % 26;
                label = (char)('A' + remainder) + label;
                index = (index - 1) / 26;
            }

            return label;
        }

        private static string JoinTags(List<string> tags)
        {
            return tags == null ? string.Empty : string.Join(", ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray());
        }

        private static List<string> SplitTags(string tags)
        {
            return PungentAuthoringMetadata.NormalizeTags((tags ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
#endif
}
