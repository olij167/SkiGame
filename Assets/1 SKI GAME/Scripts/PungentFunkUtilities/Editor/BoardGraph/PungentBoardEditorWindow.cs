using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public sealed class PungentBoardEditorWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.BoardEditor.";
        private const string PrefOpenBoardId = PrefPrefix + "OpenBoardId";
        private const string PrefPanX = PrefPrefix + "PanX";
        private const string PrefPanY = PrefPrefix + "PanY";
        private const string PrefZoom = PrefPrefix + "Zoom";
        private const string PrefSelectedNode = PrefPrefix + "SelectedNode";
        private const string PrefSelectedEdge = PrefPrefix + "SelectedEdge";
        private const string PrefSelectedGroup = PrefPrefix + "SelectedGroup";
        private const string PrefTemplate = PrefPrefix + "Template";
        private const string PrefNodeKind = PrefPrefix + "NodeKind";
        private const string PrefNodeTypeKey = PrefPrefix + "NodeTypeKey";
        private const string PrefEdgeTypeKey = PrefPrefix + "EdgeTypeKey";
        private const string PrefNodePaletteSearch = PrefPrefix + "NodePaletteSearch";
        private const string PrefBoardSearch = PrefPrefix + "BoardSearch";
        private const string PrefShowGrid = PrefPrefix + "ShowGrid";
        private const string PrefSnapToGrid = PrefPrefix + "SnapToGrid";
        private const string PrefSnapSize = PrefPrefix + "SnapSize";
        private const string PrefGridMinorSpacing = PrefPrefix + "GridMinorSpacing";
        private const string PrefGridMajorLineFrequency = PrefPrefix + "GridMajorLineFrequency";
        private const string PrefGridOpacity = PrefPrefix + "GridOpacity";
        private const string PrefSnapNodes = PrefPrefix + "SnapNodes";
        private const string PrefSnapGroups = PrefPrefix + "SnapGroups";
        private const string PrefSnapResize = PrefPrefix + "SnapResize";
        private const string PrefLeftWidth = PrefPrefix + "LeftWidth";
        private const string PrefRightWidth = PrefPrefix + "RightWidth";
        private const string PrefContentSearch = PrefPrefix + "ContentSearch";
        private const string PrefIntegrationProfile = PrefPrefix + "IntegrationProfile";
        private const double DebouncedSaveSeconds = 1.25d;
        private const float ToolbarHeight = 22f;
        private const float StatusBarHeight = 22f;
        private const float MinLeftWidth = 210f;
        private const float MaxLeftWidth = 360f;
        private const float MinRightWidth = 280f;
        private const float MaxRightWidth = 460f;
        private const float MinCanvasWidth = 320f;

        private readonly PungentBoardCanvasViewState _canvasState = new PungentBoardCanvasViewState();
        private readonly PungentBoardEditorHistory _history = new PungentBoardEditorHistory();
        private Vector2 _boardListScroll;
        private Vector2 _outlineScroll;
        private Vector2 _inspectorScroll;
        private Vector2 _projectionScroll;
        private Vector2 _eventSequenceScroll;
        private Vector2 _stackedBodyScroll;
        private readonly Dictionary<string, PungentAuthoringGuidedBindingState> _profileBindingPickerStates = new Dictionary<string, PungentAuthoringGuidedBindingState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _profileBindingPickerFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private string _selectedBoardId = string.Empty;
        private string _status = "Ready.";
        private string _boardSearch = string.Empty;
        private string _contentSearch = string.Empty;
        private string _selectedProfileId = string.Empty;
        private PungentBoardTemplateKind _selectedTemplate = PungentBoardTemplateKind.BlankBoard;
        private PungentBoardNodeKind _selectedNodeKind = PungentBoardNodeKind.NoteCard;
        private string _selectedNodeTypeKey = string.Empty;
        private string _selectedEdgeTypeKey = string.Empty;
        private string _nodePaletteSearch = string.Empty;
        private PungentAuthoringValidationResult _validationResult;
        private PungentBoardProjectionPreview _projectionPreview;
        private float _leftWidth = 250f;
        private float _rightWidth = 340f;
        private bool _contentDirty;
        private bool _integrationFoldout = true;
        private bool _deleteConfirmationScheduled;
        private bool _renamingBoardTitle;
        private bool _showOptionalProviderFallbackPreview;
        private double _nextSaveTime;
        private Rect _lastCanvasRect;
        private string _drawError = string.Empty;
        private double _nextDrawErrorLogTime;
        private string _renameBoardTitle = string.Empty;
        private PungentBoardGraphCommandRouter _commandRouter;
        private PungentBoardCanvasOverlayKind _activeCanvasOverlay = PungentBoardCanvasOverlayKind.None;
        private Rect _canvasOverlayAnchorRect = Rect.zero;
        private Rect _canvasOverlayRect = Rect.zero;

        private PungentBoardDocument CurrentBoard => PungentBoardEditorStorage.FindBoard(_selectedBoardId);

        public static void Open()
        {
            PungentBoardEditorWindow window = GetWindow<PungentBoardEditorWindow>("Board Editor");
            window.minSize = new Vector2(680f, 440f);
            window.Show();
        }

        public static void OpenAndSelectBoard(string boardId)
        {
            PungentBoardEditorWindow window = GetWindow<PungentBoardEditorWindow>("Board Editor");
            window.minSize = new Vector2(680f, 440f);
            window.Show();
            window.Focus();
            window.SelectBoard(boardId);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Board Editor");
            minSize = new Vector2(680f, 440f);
            PungentBoardProviderBootstrap.RegisterProvider();
            PungentBoardGraphTypeRegistry.EnsureBuiltInsRegistered();
            PungentBoardGraphTypeStorage.EnsureLoaded();
            PungentBoardProjectionRegistry.EnsureBuiltInsRegistered();
            PungentBoardEditorStorage.EnsureLoaded();
            PungentBoardIntegrationProfileStorage.EnsureLoaded();
            PungentBoardCustomNodeTemplateStorage.EnsureLoaded();
            _selectedBoardId = UtilityWindowPrefs.GetString(PrefOpenBoardId, string.Empty);
            int storedTemplate = UtilityWindowPrefs.GetInt(PrefTemplate, (int)PungentBoardTemplateKind.BlankBoard);
            _selectedTemplate = Enum.IsDefined(typeof(PungentBoardTemplateKind), storedTemplate)
                ? (PungentBoardTemplateKind)storedTemplate
                : PungentBoardTemplateKind.BlankBoard;
            int storedNodeKind = UtilityWindowPrefs.GetInt(PrefNodeKind, (int)PungentBoardNodeKind.NoteCard);
            _selectedNodeKind = Enum.IsDefined(typeof(PungentBoardNodeKind), storedNodeKind)
                ? (PungentBoardNodeKind)storedNodeKind
                : PungentBoardNodeKind.NoteCard;
            _selectedNodeTypeKey = UtilityWindowPrefs.GetString(PrefNodeTypeKey, string.Empty);
            _selectedEdgeTypeKey = UtilityWindowPrefs.GetString(PrefEdgeTypeKey, string.Empty);
            _nodePaletteSearch = UtilityWindowPrefs.GetString(PrefNodePaletteSearch, string.Empty);
            _boardSearch = UtilityWindowPrefs.GetString(PrefBoardSearch, string.Empty);
            _contentSearch = UtilityWindowPrefs.GetString(PrefContentSearch, string.Empty);
            _selectedProfileId = UtilityWindowPrefs.GetString(PrefIntegrationProfile, string.Empty);
            if (PungentBoardIntegrationProfileStorage.Find(_selectedProfileId) == null)
                _selectedProfileId = PungentBoardIntegrationProfileStorage.Database.profiles.FirstOrDefault()?.id ?? string.Empty;
            _leftWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefLeftWidth, _leftWidth), MinLeftWidth, MaxLeftWidth);
            _rightWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefRightWidth, _rightWidth), MinRightWidth, MaxRightWidth);
            _canvasState.showGrid = UtilityWindowPrefs.GetBool(PrefShowGrid, _canvasState.showGrid);
            _canvasState.snapToGrid = UtilityWindowPrefs.GetBool(PrefSnapToGrid, _canvasState.snapToGrid);
            _canvasState.snapSize = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefSnapSize, _canvasState.snapSize), 4f, 240f);
            _canvasState.gridMinorSpacing = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefGridMinorSpacing, _canvasState.gridMinorSpacing), 8f, 240f);
            _canvasState.gridMajorLineFrequency = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefGridMajorLineFrequency, _canvasState.gridMajorLineFrequency), 2, 12);
            _canvasState.gridOpacity = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefGridOpacity, _canvasState.gridOpacity));
            _canvasState.snapNodes = UtilityWindowPrefs.GetBool(PrefSnapNodes, _canvasState.snapNodes);
            _canvasState.snapGroups = UtilityWindowPrefs.GetBool(PrefSnapGroups, _canvasState.snapGroups);
            _canvasState.snapResize = UtilityWindowPrefs.GetBool(PrefSnapResize, _canvasState.snapResize);
            ConfigureCanvasCallbacks();
            EnsureCommandRouter();
            if (CurrentBoard == null)
                _selectedBoardId = FirstBoardId();
            LoadViewStateFromPrefsOrBoard();
            EnsureSelectedNodeTypeForCurrentBoard();
            EnsureSelectedEdgeTypeForCurrentBoard();
            _history.Reset(CurrentBoard);
            if (!string.IsNullOrWhiteSpace(PungentBoardEditorStorage.LoadError))
                _status = "Storage load warning: " + PungentBoardEditorStorage.LoadError;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.delayCall -= RunScheduledDeleteConfirmation;
            PersistViewState();
            PersistPanelState();
            if (_contentDirty)
                SaveNow("Saved pending board changes on close.");
        }

        private void OnGUI()
        {
            EnsureCommandRouter();
            ConfigureCanvasCallbacks();
            HandleGlobalShortcuts();
            HandleActiveCanvasOverlayInput();

            float toolbarHeight = ToolbarHeight;
            Rect toolbarRect = new Rect(0f, 0f, position.width, toolbarHeight);
            Rect statusRect = new Rect(0f, Mathf.Max(toolbarHeight, position.height - StatusBarHeight), position.width, StatusBarHeight);
            Rect bodyRect = new Rect(0f, toolbarRect.yMax, position.width, Mathf.Max(1f, statusRect.yMin - toolbarRect.yMax));

            GUILayout.BeginArea(toolbarRect);
            DrawToolbar(toolbarHeight > ToolbarHeight);
            GUILayout.EndArea();

            DrawBodySafe(bodyRect);

            GUILayout.BeginArea(statusRect);
            DrawStatusBar();
            GUILayout.EndArea();

            DrawActiveCanvasOverlay();

            if (_canvasState.contentChanged)
            {
                _history.RecordSnapshot(_selectedBoardId, _canvasState.pendingHistoryLabel, _canvasState.pendingHistorySnapshotJson, CurrentBoard);
                _canvasState.ClearPendingHistory();
                MarkContentDirty("Board canvas changed.");
                _canvasState.contentChanged = false;
                _validationResult = null;
                _projectionPreview = null;
            }
            if (_canvasState.viewChanged)
            {
                PersistViewState();
                _canvasState.viewChanged = false;
            }
            if (_canvasState.deleteRequested)
            {
                ScheduleDeleteSelectedWithConfirmation();
                _canvasState.deleteRequested = false;
            }
            if (_canvasState.repaintRequested)
            {
                Repaint();
                _canvasState.repaintRequested = false;
            }
        }

        private void ConfigureCanvasCallbacks()
        {
            _canvasState.commandRequested = (command, origin) => EnsureCommandRouter().Execute(command, origin);
            _canvasState.saveGroupTemplateRequested = SaveGroupAsTemplateFromCanvas;
            _canvasState.instantiateCustomGroupTemplateRequested = AddCustomGroupNodeAtCanvasPoint;
        }

        private PungentBoardGraphCommandRouter EnsureCommandRouter()
        {
            if (_commandRouter == null)
                _commandRouter = new PungentBoardGraphCommandRouter(() => CurrentBoard);

            _commandRouter.CanUndo = () => _history.CanUndo;
            _commandRouter.CanRedo = () => _history.CanRedo;
            _commandRouter.StatusChanged = message =>
            {
                if (!string.IsNullOrWhiteSpace(message))
                    _status = message;
            };
            _commandRouter.RepaintRequested = Repaint;
            _commandRouter.Save = () => SaveNow("Saved board.");
            _commandRouter.Undo = UndoBoard;
            _commandRouter.Redo = RedoBoard;
            _commandRouter.DeleteSelection = ScheduleDeleteSelectedWithConfirmation;
            _commandRouter.FrameSelection = FrameSelection;
            _commandRouter.FitAll = FitCurrentBoard;
            _commandRouter.AddNode = AddNodeAtCanvasCenter;
            _commandRouter.AddGroup = AddGroupAtCanvasCenter;
            _commandRouter.DuplicateSelection = DuplicateSelectedNode;
            _commandRouter.ToggleConnectorMode = () => SetConnectorMode(!_canvasState.connectorMode);
            _commandRouter.CancelTransientAction = CancelTransientCanvasAction;
            _commandRouter.ResetZoom = ResetCanvasZoom;
            _commandRouter.ToggleGrid = ToggleGrid;
            _commandRouter.ToggleSnap = ToggleSnap;
            _commandRouter.OpenGridOverlay = () => OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Grid, _canvasOverlayAnchorRect);
            _commandRouter.OpenSnapOverlay = () => OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Snap, _canvasOverlayAnchorRect);
            _commandRouter.OpenShortcutOverlay = () => OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Shortcuts, _canvasOverlayAnchorRect);
            return _commandRouter;
        }

        private void HandleGlobalShortcuts()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
                return;

            bool actionModifier = evt.control || evt.command;
            if (actionModifier && evt.keyCode == KeyCode.S)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.Save, "shortcut");
                evt.Use();
                return;
            }

            if (actionModifier && evt.keyCode == KeyCode.Z && !evt.shift)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.Undo, "shortcut");
                evt.Use();
                return;
            }

            if ((actionModifier && evt.keyCode == KeyCode.Y) || (actionModifier && evt.shift && evt.keyCode == KeyCode.Z))
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.Redo, "shortcut");
                evt.Use();
                return;
            }

            if (EditorGUIUtility.editingTextField)
                return;

            if (evt.character == '?')
            {
                OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Shortcuts, Rect.zero);
                evt.Use();
                return;
            }

            if (CurrentBoard == null)
                return;

            if ((evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) && _canvasState.HasSelection)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.DeleteSelection, "shortcut");
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.F && evt.shift)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.FitAll, "shortcut");
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.F)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.FrameSelection, "shortcut");
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.N)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.AddNode, "shortcut");
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.G)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.AddGroup, "shortcut");
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.D && actionModifier)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.DuplicateSelection, "shortcut");
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.C && !actionModifier)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.ToggleConnectorMode, "shortcut");
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.CancelTransientAction, "shortcut");
                evt.Use();
            }
        }

        private void DrawBodySafe(Rect body)
        {
            if (body.width <= 1f || body.height <= 1f)
                return;

            try
            {
                DrawBody(body);
                _drawError = string.Empty;
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _drawError = ex.GetType().Name + ": " + ex.Message;
                if (EditorApplication.timeSinceStartup >= _nextDrawErrorLogTime)
                {
                    Debug.LogException(ex);
                    _nextDrawErrorLogTime = EditorApplication.timeSinceStartup + 5d;
                }

                EditorGUI.DrawRect(body, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.1f, 0.1f) : new Color(0.94f, 0.86f, 0.84f));
                GUILayout.BeginArea(new Rect(body.x + 12f, body.y + 12f, Mathf.Max(120f, body.width - 24f), Mathf.Max(60f, body.height - 24f)));
                EditorGUILayout.HelpBox("Board Editor body failed to draw.\n\n" + _drawError, MessageType.Error);
                GUILayout.EndArea();
            }
        }

        private void DrawToolbar(bool compact)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawToolbarBoardControls(position.width < 820f);
                GUILayout.FlexibleSpace();
                DrawToolbarStateControls();
            }
        }

        private void DrawToolbarBoardControls(bool compact = false)
        {
            if (GUILayout.Button(new GUIContent("File", "New, open, save, duplicate, delete, validation, and sticky-note handoffs."), EditorStyles.toolbarDropDown, GUILayout.Width(48f)))
                ShowBoardFileMenu();

            _selectedTemplate = (PungentBoardTemplateKind)EditorGUILayout.EnumPopup(_selectedTemplate, GUILayout.Width(compact ? 150f : 188f));
            if (GUILayout.Button(new GUIContent("New Board", "Create a board from the selected template."), EditorStyles.toolbarButton, GUILayout.Width(82f)))
                CreateBoard();

            using (new EditorGUI.DisabledScope(CurrentBoard == null))
            {
                if (GUILayout.Button(new GUIContent("Save", "Save board storage now."), EditorStyles.toolbarButton, GUILayout.Width(50f)))
                    EnsureCommandRouter().Execute(PungentBoardGraphCommand.Save, "toolbar");
            }

            if (compact)
            {
                if (GUILayout.Button(new GUIContent("More", "Board actions, validation, help, and browser access."), EditorStyles.toolbarDropDown, GUILayout.Width(54f)))
                    ShowBoardMoreMenu();
                return;
            }

            using (new EditorGUI.DisabledScope(CurrentBoard == null))
            {
                using (new EditorGUI.DisabledScope(!_history.CanUndo))
                {
                    if (GUILayout.Button(new GUIContent("Undo", string.IsNullOrWhiteSpace(_history.NextUndoLabel) ? "Undo board edit." : "Undo " + _history.NextUndoLabel), EditorStyles.toolbarButton, GUILayout.Width(48f)))
                        EnsureCommandRouter().Execute(PungentBoardGraphCommand.Undo, "toolbar");
                }
                using (new EditorGUI.DisabledScope(!_history.CanRedo))
                {
                    if (GUILayout.Button(new GUIContent("Redo", string.IsNullOrWhiteSpace(_history.NextRedoLabel) ? "Redo board edit." : "Redo " + _history.NextRedoLabel), EditorStyles.toolbarButton, GUILayout.Width(48f)))
                        EnsureCommandRouter().Execute(PungentBoardGraphCommand.Redo, "toolbar");
                }
                if (GUILayout.Button(new GUIContent("Validate", "Run local board validation without project-wide scans."), EditorStyles.toolbarButton, GUILayout.Width(68f)))
                    ValidateCurrentBoard();
                if (GUILayout.Button(new GUIContent("Help", "Open the BoardGraph help topic."), EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    OpenBoardHelp();
            }

            if (GUILayout.Button(new GUIContent("Open Browser", "Open the Utilities Browser. The board authoring provider is registered for authoring browser consumers."), EditorStyles.toolbarButton, GUILayout.Width(94f)))
            {
                PungentBoardMenuItems.RegisterUtilityDescriptor();
                PungentUtilityControlPanelWindow.OpenCategory(PungentUtilityCategories.DocumentationPlanning);
                _status = "Opened Utilities Browser. Search for board, whiteboard, or graph to jump straight to this utility.";
            }
        }

        private void ShowBoardMoreMenu()
        {
            GenericMenu menu = new GenericMenu();
            AddMenuItem(menu, "Undo", _history.CanUndo, UndoBoard);
            AddMenuItem(menu, "Redo", _history.CanRedo, RedoBoard);
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Validate", CurrentBoard != null, ValidateCurrentBoard);
            AddMenuItem(menu, "Help/BoardGraph Help", true, OpenBoardHelp);
            menu.AddItem(new GUIContent(_showOptionalProviderFallbackPreview ? "Help/Hide Missing Provider Preview" : "Help/Show Missing Provider Preview"), _showOptionalProviderFallbackPreview, ToggleOptionalProviderFallbackPreview);
            AddMenuItem(menu, "Open Sticky Notes", true, OpenStickyNotesFromToolbar);
            AddMenuItem(menu, "Open Utilities Browser", true, OpenUtilitiesBrowserFromToolbar);
            menu.ShowAsContext();
        }

        private void ShowBoardFileMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentBoardTemplateKind template in Enum.GetValues(typeof(PungentBoardTemplateKind)))
            {
                PungentBoardTemplateKind captured = template;
                menu.AddItem(new GUIContent("New/" + PungentBoardTemplates.GetDisplayName(captured)), false, () => CreateBoardFromTemplate(captured));
            }

            List<PungentBoardDocument> boards = (PungentBoardEditorStorage.Database.documents ?? new List<PungentBoardDocument>())
                .Where(board => board != null && !board.archived)
                .OrderBy(board => string.IsNullOrWhiteSpace(board.title) ? "Untitled Board" : board.title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            menu.AddSeparator(string.Empty);
            if (boards.Count == 0)
                menu.AddDisabledItem(new GUIContent("Open/No boards yet"));
            else
            {
                foreach (PungentBoardDocument board in boards)
                {
                    string id = board.id;
                    bool selected = PungentAuthoringId.EqualsId(id, _selectedBoardId);
                    menu.AddItem(new GUIContent("Open/" + (string.IsNullOrWhiteSpace(board.title) ? "Untitled Board" : board.title)), selected, () => SelectBoard(id));
                }
            }

            PungentBoardDocument current = CurrentBoard;
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Save", current != null, () => SaveNow("Saved board."));
            AddMenuItem(menu, "Duplicate Board", current != null, DuplicateCurrentBoard);
            AddMenuItem(menu, "Rename Board", current != null, BeginBoardTitleRename);
            AddMenuItem(menu, "Copy Board ID", current != null, () => EditorGUIUtility.systemCopyBuffer = current.id);
            AddMenuItem(menu, "Delete Board...", current != null, DeleteCurrentBoardWithConfirmation);

            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Validate", current != null, ValidateCurrentBoard);
            AddMenuItem(menu, "Help/BoardGraph Help", true, OpenBoardHelp);
            menu.AddItem(new GUIContent(_showOptionalProviderFallbackPreview ? "Help/Hide Missing Provider Preview" : "Help/Show Missing Provider Preview"), _showOptionalProviderFallbackPreview, ToggleOptionalProviderFallbackPreview);
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Open Sticky Notes", true, OpenStickyNotesFromToolbar);
            AddMenuItem(menu, "Open Utilities Browser", true, OpenUtilitiesBrowserFromToolbar);
            // RDE/STICKY-NOTES MIGRATION NOTE: BoardGraph has its own File menu now; future work should use Sticky Notes/Rich Documents handoffs instead of recreating Notes & Roadmap browser features here.
            menu.ShowAsContext();
        }

        private void OpenBoardHelp()
        {
            PungentBoardMenuItems.RegisterUtilityDescriptor();
            PungentUtilityHelpRegistry.Open(PungentBoardProvider.UtilityId, "overview", "board-whiteboard-node-graph");
            _status = "Opened BoardGraph help. If the topic is not authored yet, use the Utilities Browser fallback from the same window.";
        }

        private void ToggleOptionalProviderFallbackPreview()
        {
            _showOptionalProviderFallbackPreview = !_showOptionalProviderFallbackPreview;
            _status = _showOptionalProviderFallbackPreview
                ? "Showing simulated missing-provider guidance. No packages or board data were changed."
                : "Hidden missing-provider guidance.";
            Repaint();
        }

        private void OpenUtilitiesBrowserFromToolbar()
        {
            PungentBoardMenuItems.RegisterUtilityDescriptor();
            PungentUtilityControlPanelWindow.OpenCategory(PungentUtilityCategories.DocumentationPlanning);
            _status = "Opened Utilities Browser. Search for board, whiteboard, or graph to jump straight to this utility.";
        }

        private void OpenStickyNotesFromToolbar()
        {
            PungentNotesRoadmapWindow.Open();
            _status = "Opened Sticky Notes.";
        }

        private static void AddMenuItem(GenericMenu menu, string path, bool enabled, Action action)
        {
            if (enabled)
                menu.AddItem(new GUIContent(path), false, () => action?.Invoke());
            else
                menu.AddDisabledItem(new GUIContent(path));
        }

        private void CreateBoardFromTemplate(PungentBoardTemplateKind template)
        {
            _selectedTemplate = template;
            UtilityWindowPrefs.SetInt(PrefTemplate, (int)_selectedTemplate);
            CreateBoard();
        }

        private void DrawNodeTypePopup(float width = 154f)
        {
            PungentBoardDocument board = CurrentBoard;
            List<PungentBoardNodeTypeDefinition> nodeTypes = PungentBoardCanvasGUI.GetPaletteNodeTypes(board);
            if (nodeTypes.Count == 0)
            {
                EditorGUI.BeginChangeCheck();
                _selectedNodeKind = (PungentBoardNodeKind)EditorGUILayout.EnumPopup(_selectedNodeKind, EditorStyles.toolbarPopup, GUILayout.Width(Mathf.Min(width, 132f)));
                if (EditorGUI.EndChangeCheck())
                    UtilityWindowPrefs.SetInt(PrefNodeKind, (int)_selectedNodeKind);
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedNodeTypeKey) || nodeTypes.All(type => !string.Equals(type.typeKey, _selectedNodeTypeKey, StringComparison.OrdinalIgnoreCase)))
                _selectedNodeTypeKey = nodeTypes[0].typeKey;

            int currentIndex = Mathf.Max(0, nodeTypes.FindIndex(type => string.Equals(type.typeKey, _selectedNodeTypeKey, StringComparison.OrdinalIgnoreCase)));
            string[] labels = nodeTypes.Select(type => type.displayName).ToArray();
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(currentIndex, labels, EditorStyles.toolbarPopup, GUILayout.Width(width));
            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < nodeTypes.Count)
            {
                PungentBoardNodeTypeDefinition selected = nodeTypes[nextIndex];
                _selectedNodeTypeKey = selected.typeKey;
                _selectedNodeKind = selected.nodeKind;
                UtilityWindowPrefs.SetString(PrefNodeTypeKey, _selectedNodeTypeKey);
                UtilityWindowPrefs.SetInt(PrefNodeKind, (int)_selectedNodeKind);
            }
        }

        private void DrawEdgeRulePopup(float width = 138f)
        {
            PungentBoardDocument board = CurrentBoard;
            PungentBoardGraphTypeDefinition graphType = board != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId) : null;
            List<PungentBoardEdgeRuleDefinition> rules = graphType != null
                ? (graphType.edgeRules ?? new List<PungentBoardEdgeRuleDefinition>()).Where(rule => rule != null).ToList()
                : new List<PungentBoardEdgeRuleDefinition>();
            if (rules.Count == 0)
            {
                _selectedEdgeTypeKey = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedEdgeTypeKey) || rules.All(rule => !string.Equals(rule.typeKey, _selectedEdgeTypeKey, StringComparison.OrdinalIgnoreCase)))
            {
                PungentBoardEdgeRuleDefinition defaultRule = graphType != null ? graphType.FindEdgeRule(graphType.defaultEdgeTypeKey) : null;
                _selectedEdgeTypeKey = (defaultRule ?? rules[0]).typeKey;
                UtilityWindowPrefs.SetString(PrefEdgeTypeKey, _selectedEdgeTypeKey);
            }

            int currentIndex = Mathf.Max(0, rules.FindIndex(rule => string.Equals(rule.typeKey, _selectedEdgeTypeKey, StringComparison.OrdinalIgnoreCase)));
            string[] labels = rules.Select(rule => "Edge: " + rule.displayName).ToArray();
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(currentIndex, labels, EditorStyles.toolbarPopup, GUILayout.Width(width));
            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < rules.Count)
            {
                _selectedEdgeTypeKey = rules[nextIndex].typeKey;
                _canvasState.defaultEdgeTypeKey = _selectedEdgeTypeKey;
                UtilityWindowPrefs.SetString(PrefEdgeTypeKey, _selectedEdgeTypeKey);
            }
        }

        private void DrawToolbarStateControls()
        {
            GUILayout.Label(_contentDirty ? "Unsaved" : "Saved", EditorStyles.miniLabel, GUILayout.Width(58f));
        }

        private void DrawBody(Rect body)
        {
            if (body.width < 760f)
            {
                GUILayout.BeginArea(body);
                try
                {
                    _stackedBodyScroll = EditorGUILayout.BeginScrollView(_stackedBodyScroll);
                    DrawBoardList(GUILayout.Height(Mathf.Clamp(body.height * 0.30f, 150f, 230f)));
                    DrawCanvasPanel(GUILayout.Height(Mathf.Max(280f, body.height * 0.62f)));
                    DrawInspectorPanel(GUILayout.MinHeight(Mathf.Max(320f, body.height * 0.72f)));
                    EditorGUILayout.EndScrollView();
                }
                finally
                {
                    GUILayout.EndArea();
                }
                return;
            }

            GUILayout.BeginArea(body);
            try
            {
                ClampPanelWidths(body.width);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawBoardList(GUILayout.Width(_leftWidth), GUILayout.ExpandHeight(true));
                    UtilityWindowTheme.HorizontalResizeHandle(ref _leftWidth, MinLeftWidth, MaxLeftWidth, PersistPanelState, "Drag to resize the board list.");
                    DrawCanvasPanel(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    UtilityWindowTheme.HorizontalResizeHandle(ref _rightWidth, MinRightWidth, MaxRightWidth, PersistPanelState, "Drag to resize the inspector.", true);
                    DrawInspectorPanel(GUILayout.Width(_rightWidth), GUILayout.ExpandHeight(true));
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawBoardList(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.12f, 0.05f), options))
            {
                UtilityWindowTheme.SectionTitle("Boards", UtilityWindowTheme.Teal, (PungentBoardEditorStorage.Database.documents?.Count ?? 0) + " saved");
                if (GUILayout.Button(new GUIContent("Reload Storage", "Reload board JSON storage from disk."), GUILayout.Height(22f)))
                {
                    PungentBoardEditorStorage.Reload();
                    if (CurrentBoard == null)
                        SelectBoard(FirstBoardId());
                    _status = "Reloaded board storage.";
                }

                EditorGUI.BeginChangeCheck();
                _boardSearch = EditorGUILayout.TextField(new GUIContent("Search", "Filter the saved board list by title, summary, status, or tag."), _boardSearch);
                if (EditorGUI.EndChangeCheck())
                    UtilityWindowPrefs.SetString(PrefBoardSearch, _boardSearch ?? string.Empty);

                EditorGUI.BeginChangeCheck();
                _contentSearch = EditorGUILayout.TextField(new GUIContent("Find", "Find nodes, edges, groups, tags, linked IDs, or board text in the current board."), _contentSearch);
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowPrefs.SetString(PrefContentSearch, _contentSearch ?? string.Empty);
                    Repaint();
                }
                using (new EditorGUI.DisabledScope(CurrentBoard == null || string.IsNullOrWhiteSpace(_contentSearch)))
                {
                    if (GUILayout.Button(new GUIContent("Frame First Match", "Focus the first current-board search match."), GUILayout.Height(21f)))
                        FrameFirstSearchMatch();
                }

                DrawNodePalette();

                _boardListScroll = EditorGUILayout.BeginScrollView(_boardListScroll);
                foreach (PungentBoardDocument board in PungentBoardEditorStorage.Database.documents ?? new List<PungentBoardDocument>())
                {
                    if (board == null)
                        continue;
                    if (!MatchesBoardFilter(board))
                        continue;

                    DrawBoardListItem(board);
                }
                EditorGUILayout.EndScrollView();

                DrawCurrentBoardOutline();
            }
        }

        private void DrawNodePalette()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.Find(board.graphTypeId);
            PungentBoardGraphTypeDefinition fallbackGraphType = PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId);
            EditorGUILayout.Space(6f);
            UtilityWindowTheme.SectionTitle("Node Palette", UtilityWindowTheme.Teal, fallbackGraphType != null ? fallbackGraphType.displayName : "Freeform");
            if (graphType == null)
                EditorGUILayout.HelpBox("Graph type '" + board.graphTypeId + "' is not registered. Nodes stay editable through the Freeform fallback.", MessageType.Warning);
            else if (!string.IsNullOrWhiteSpace(graphType.description))
                EditorGUILayout.LabelField(PreviewText(graphType.description, 120), EditorStyles.wordWrappedMiniLabel);

            EditorGUI.BeginChangeCheck();
            _nodePaletteSearch = EditorGUILayout.TextField(new GUIContent("Filter", "Filter node presets for this graph type."), _nodePaletteSearch);
            if (EditorGUI.EndChangeCheck())
                UtilityWindowPrefs.SetString(PrefNodePaletteSearch, _nodePaletteSearch ?? string.Empty);

            EditorGUILayout.LabelField("Board-Type Nodes", EditorStyles.boldLabel);
            List<PungentBoardNodeTypeDefinition> nodeTypes = PungentBoardCanvasGUI.GetPaletteNodeTypes(board)
                .Where(MatchesPaletteSearch)
                .ToList();
            if (nodeTypes.Count == 0)
            {
                EditorGUILayout.LabelField("No board-type nodes match this filter.", EditorStyles.miniLabel);
            }
            else
            {
                int rows = Mathf.Min(8, nodeTypes.Count);
                for (int i = 0; i < rows; i++)
                {
                    PungentBoardNodeTypeDefinition nodeType = nodeTypes[i];
                    bool selected = string.Equals(nodeType.typeKey, _selectedNodeTypeKey, StringComparison.OrdinalIgnoreCase);
                    using (new EditorGUILayout.HorizontalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
                    {
                        if (GUILayout.Toggle(selected, new GUIContent(nodeType.displayName, nodeType.description), EditorStyles.miniButtonLeft, GUILayout.Height(22f)))
                            SelectNodePaletteType(nodeType);

                        if (GUILayout.Button(new GUIContent("+", "Add this node type at the canvas centre."), EditorStyles.miniButtonRight, GUILayout.Width(28f), GUILayout.Height(22f)))
                        {
                            SelectNodePaletteType(nodeType);
                            AddNodeAtCanvasCenter();
                        }
                    }
                }

                if (nodeTypes.Count > rows)
                    EditorGUILayout.LabelField("+" + (nodeTypes.Count - rows) + " more matching preset(s). Refine the filter or use the toolbar popup.", EditorStyles.miniLabel);
            }

            DrawParameterReferencePalette();
            DrawCustomGroupNodePalette(board);
        }

        private bool MatchesPaletteSearch(PungentBoardNodeTypeDefinition nodeType)
        {
            if (nodeType == null)
                return false;

            string query = (_nodePaletteSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return Contains(nodeType.displayName, query) ||
                   Contains(nodeType.typeKey, query) ||
                   Contains(nodeType.description, query) ||
                   Contains(nodeType.nodeKind.ToString(), query) ||
                   Contains(nodeType.styleKey, query);
        }

        private bool MatchesPaletteSearch(string displayName, string key, string description = null)
        {
            string query = (_nodePaletteSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return Contains(displayName, query) ||
                   Contains(key, query) ||
                   Contains(description, query);
        }

        private void DrawParameterReferencePalette()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Parameter / Reference Nodes", EditorStyles.boldLabel);
            DrawParameterReferencePaletteRow("Text Parameter", "text-parameter", PungentBoardNodeKind.FreeformCard, "parameter", "Reusable text value or label.");
            DrawParameterReferencePaletteRow("Number Parameter", "number-parameter", PungentBoardNodeKind.FreeformCard, "parameter", "Reusable numeric value.");
            DrawParameterReferencePaletteRow("Object Reference", "object-reference", PungentBoardNodeKind.UtilityReference, "reference", "Object or asset reference placeholder.");
            DrawParameterReferencePaletteRow("Component Reference", "component-reference", PungentBoardNodeKind.UtilityReference, "reference", "Component reference placeholder.");
            DrawParameterReferencePaletteRow("Field Reference", "field-reference", PungentBoardNodeKind.UtilityReference, "reference", "Serialized field/property reference placeholder.");
        }

        private void DrawParameterReferencePaletteRow(string label, string typeKey, PungentBoardNodeKind kind, string styleKey, string tooltip)
        {
            if (!MatchesPaletteSearch(label, typeKey, tooltip))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(label, tooltip), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("+", "Add this parameter/reference node at the canvas centre."), EditorStyles.miniButton, GUILayout.Width(28f), GUILayout.Height(20f)))
                    AddParameterReferenceNodeAtCanvasCenter(label, typeKey, kind, styleKey);
            }
        }

        private void DrawCustomGroupNodePalette(PungentBoardDocument board)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Custom Group Nodes", EditorStyles.boldLabel);
            PungentBoardCustomNodeTemplateDatabase database = PungentBoardCustomNodeTemplateStorage.Database;
            List<PungentBoardCustomNodeTemplate> templates = (database.templates ?? new List<PungentBoardCustomNodeTemplate>())
                .Where(template => IsCustomTemplateCompatible(board, template))
                .Where(template => MatchesPaletteSearch(template.displayName, template.id, template.description))
                .OrderBy(template => template.displayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (templates.Count == 0)
            {
                EditorGUILayout.LabelField("No saved custom group nodes match this board/filter.", EditorStyles.miniLabel);
                return;
            }

            int rows = Mathf.Min(8, templates.Count);
            for (int i = 0; i < rows; i++)
            {
                PungentBoardCustomNodeTemplate template = templates[i];
                if (template == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string tooltip = string.IsNullOrWhiteSpace(template.description)
                        ? "Instantiate this group-backed custom node."
                        : template.description;
                    GUILayout.Label(new GUIContent(template.displayName, tooltip), EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label((template.nodes?.Count ?? 0) + "n", EditorStyles.miniLabel, GUILayout.Width(28f));
                    if (GUILayout.Button(new GUIContent("+", "Clone this custom group node into the current board."), EditorStyles.miniButton, GUILayout.Width(28f), GUILayout.Height(20f)))
                        AddCustomGroupNodeAtCanvasCenter(template);
                }
            }

            if (templates.Count > rows)
                EditorGUILayout.LabelField("+" + (templates.Count - rows) + " more custom group node template(s). Refine the filter.", EditorStyles.miniLabel);
        }

        private static bool IsCustomTemplateCompatible(PungentBoardDocument board, PungentBoardCustomNodeTemplate template)
        {
            if (template == null)
                return false;

            string boardGraphType = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(board != null ? board.graphTypeId : PungentBoardBuiltInGraphTypes.FreeformWhiteboard);
            string templateGraphType = PungentBoardBuiltInGraphTypes.NormalizeGraphTypeId(template.graphTypeId);
            return string.Equals(templateGraphType, boardGraphType, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(templateGraphType, PungentBoardBuiltInGraphTypes.FreeformWhiteboard, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(boardGraphType, PungentBoardBuiltInGraphTypes.FreeformWhiteboard, StringComparison.OrdinalIgnoreCase);
        }

        private void SelectNodePaletteType(PungentBoardNodeTypeDefinition nodeType)
        {
            if (nodeType == null)
                return;

            _selectedNodeTypeKey = nodeType.typeKey;
            _selectedNodeKind = nodeType.nodeKind;
            _canvasState.defaultNodeTypeKey = _selectedNodeTypeKey;
            _canvasState.defaultNodeKind = _selectedNodeKind;
            UtilityWindowPrefs.SetString(PrefNodeTypeKey, _selectedNodeTypeKey);
            UtilityWindowPrefs.SetInt(PrefNodeKind, (int)_selectedNodeKind);
            Repaint();
        }

        private void DrawCurrentBoardOutline()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            EditorGUILayout.Space(6f);
            UtilityWindowTheme.SectionTitle("Outline", UtilityWindowTheme.Teal, SelectionPill());
            _outlineScroll = EditorGUILayout.BeginScrollView(_outlineScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
            DrawOutlineNodes(board);
            DrawOutlineEdges(board);
            DrawOutlineGroups(board);
            EditorGUILayout.EndScrollView();
        }

        private void DrawOutlineNodes(PungentBoardDocument board)
        {
            EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);
            foreach (PungentBoardNode node in board.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;
                if (!MatchesContentSearch(node))
                    continue;

                bool selected = _canvasState.IsNodeSelected(node.id);
                string label = (node.collapsed ? "+ " : string.Empty) + (string.IsNullOrWhiteSpace(node.title) ? node.id : node.title);
                if (GUILayout.Toggle(selected, new GUIContent(label, node.nodeKind.ToString()), EditorStyles.miniButton))
                {
                    if (!selected)
                    {
                        _canvasState.SelectNode(node.id);
                        PersistViewState();
                        Repaint();
                    }
                }
            }
        }

        private void DrawOutlineEdges(PungentBoardDocument board)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Edges", EditorStyles.boldLabel);
            foreach (PungentBoardEdge edge in board.edges ?? new List<PungentBoardEdge>())
            {
                if (edge == null)
                    continue;
                if (!MatchesContentSearch(edge))
                    continue;

                bool selected = PungentAuthoringId.EqualsId(edge.id, _canvasState.selectedEdgeId);
                string label = string.IsNullOrWhiteSpace(edge.label) ? BuildEdgeEndpointLabel(board, edge) : edge.label;
                if (GUILayout.Toggle(selected, new GUIContent(label, edge.edgeKind.ToString()), EditorStyles.miniButton))
                {
                    if (!selected)
                    {
                        _canvasState.SelectEdge(edge.id);
                        PersistViewState();
                        Repaint();
                    }
                }
            }
        }

        private void DrawOutlineGroups(PungentBoardDocument board)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Groups", EditorStyles.boldLabel);
            foreach (PungentBoardGroup group in board.groups ?? new List<PungentBoardGroup>())
            {
                if (group == null)
                    continue;
                if (!MatchesContentSearch(group))
                    continue;

                bool selected = _canvasState.IsGroupSelected(group.id);
                string label = (group.collapsed ? "+ " : string.Empty) + (string.IsNullOrWhiteSpace(group.title) ? group.id : group.title);
                if (GUILayout.Toggle(selected, new GUIContent(label, (group.containedNodeIds?.Count ?? 0) + " contained node(s)"), EditorStyles.miniButton))
                {
                    if (!selected)
                    {
                        _canvasState.SelectGroup(group.id);
                        PersistViewState();
                        Repaint();
                    }
                }
            }
        }

        private void DrawBoardListItem(PungentBoardDocument board)
        {
            bool selected = PungentAuthoringId.EqualsId(board.id, _selectedBoardId);
            GUIStyle style = selected ? EditorStyles.helpBox : GUI.skin.box;
            using (new EditorGUILayout.VerticalScope(style))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(board.title, "Open this board."), EditorStyles.boldLabel))
                        SelectBoard(board.id);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label((board.nodes?.Count ?? 0).ToString(), EditorStyles.miniLabel, GUILayout.Width(28f));
                }

                string summary = string.IsNullOrWhiteSpace(board.summary) ? board.status : board.summary;
                if (!string.IsNullOrWhiteSpace(summary))
                    EditorGUILayout.LabelField(PreviewText(summary, 90), EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawCanvasPanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f), options))
            {
                PungentBoardDocument board = CurrentBoard;
                PungentBoardGraphTypeDefinition graphType = board != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId) : null;
                string pill = board == null
                    ? "No board"
                    : (graphType != null ? graphType.displayName + " | " : string.Empty) + Mathf.RoundToInt(_canvasState.zoom * 100f) + "%" + (_canvasState.snapToGrid ? " | snap " + Mathf.RoundToInt(_canvasState.snapSize) : _canvasState.showGrid ? " | grid" : " | grid off");
                DrawCanvasHeader(board, pill);
                DrawCanvasLocalControls(board);
                if (board == null)
                    DrawNoBoardCanvasActions();
                Rect canvasRect = GUILayoutUtility.GetRect(260f, 180f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                _lastCanvasRect = canvasRect;
                _canvasState.defaultNodeKind = _selectedNodeKind;
                _canvasState.defaultNodeTypeKey = _selectedNodeTypeKey;
                _canvasState.defaultEdgeTypeKey = string.IsNullOrWhiteSpace(_selectedEdgeTypeKey) && graphType != null ? graphType.defaultEdgeTypeKey : _selectedEdgeTypeKey;
                PungentBoardCanvasGUI.Draw(canvasRect, board, _canvasState);
            }
        }

        private void DrawNoBoardCanvasActions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("No board is selected. Create a new board or use File > Open to choose an existing board.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("New Board", "Create a board from the selected template."), GUILayout.Width(96f)))
                        CreateBoard();
                    if (GUILayout.Button(new GUIContent("Open Board", "Open the board file menu."), GUILayout.Width(96f)))
                        ShowBoardFileMenu();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCanvasHeader(PungentBoardDocument board, string pill)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (board == null)
                {
                    GUILayout.Label("Canvas", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(pill, EditorStyles.miniLabel);
                    return;
                }

                if (_renamingBoardTitle)
                {
                    GUI.SetNextControlName("PungentBoardHeaderRename");
                    EditorGUI.BeginChangeCheck();
                    _renameBoardTitle = EditorGUILayout.TextField(_renameBoardTitle, GUILayout.MinWidth(120f));
                    if (EditorGUI.EndChangeCheck())
                        Repaint();

                    if (GUILayout.Button(new GUIContent("OK", "Commit board title rename."), EditorStyles.toolbarButton, GUILayout.Width(34f)))
                        CommitBoardTitleRename();
                    if (GUILayout.Button(new GUIContent("Cancel", "Cancel board title rename."), EditorStyles.toolbarButton, GUILayout.Width(54f)))
                        CancelBoardTitleRename();
                    HandleBoardTitleRenameKeys();
                    EditorGUI.FocusTextInControl("PungentBoardHeaderRename");
                }
                else
                {
                    string title = string.IsNullOrWhiteSpace(board.title) ? "Untitled Board" : board.title;
                    if (GUILayout.Button(new GUIContent(title, "Click to rename this board."), EditorStyles.boldLabel, GUILayout.MinWidth(120f)))
                        BeginBoardTitleRename();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(pill, EditorStyles.miniLabel);
            }
        }

        private void DrawCanvasLocalControls(PungentBoardDocument board)
        {
            bool compact = EstimatedCanvasPanelWidth() < 560f;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(board == null))
                {
                    DrawNodeTypePopup(compact ? 118f : 154f);
                    DrawEdgeRulePopup(compact ? 112f : 138f);
                    if (compact)
                    {
                        if (GUILayout.Button(new GUIContent("Add", "Create nodes and groups."), EditorStyles.toolbarDropDown, GUILayout.Width(44f)))
                            ShowCanvasAddMenu();
                        if (GUILayout.Button(new GUIContent("View", "Frame, fit, zoom, grid, and snap controls."), EditorStyles.toolbarDropDown, GUILayout.Width(48f)))
                            ShowCanvasViewMenu();
                        if (GUILayout.Button(new GUIContent("Edit", "Selection, connection, alignment, and distribution controls."), EditorStyles.toolbarDropDown, GUILayout.Width(44f)))
                            ShowCanvasEditMenu();
                    }
                    else
                    {
                        if (GUILayout.Button(new GUIContent("Add Node", "Add a node/card at the current canvas centre. Shortcut: N."), EditorStyles.toolbarButton, GUILayout.Width(74f)))
                            EnsureCommandRouter().Execute(PungentBoardGraphCommand.AddNode, "canvas toolbar");
                        if (GUILayout.Button(new GUIContent("Add Group", "Add a placemat group at the current canvas centre. Shortcut: G."), EditorStyles.toolbarButton, GUILayout.Width(78f)))
                            EnsureCommandRouter().Execute(PungentBoardGraphCommand.AddGroup, "canvas toolbar");
                        if (GUILayout.Button(new GUIContent("Frame", "Frame selected content, or fit all when nothing is selected. Shortcut: F."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                            EnsureCommandRouter().Execute(PungentBoardGraphCommand.FrameSelection, "canvas toolbar");
                        if (GUILayout.Button(new GUIContent("Fit", "Fit all board content in the canvas. Shortcut: Shift+F."), EditorStyles.toolbarButton, GUILayout.Width(40f)))
                            EnsureCommandRouter().Execute(PungentBoardGraphCommand.FitAll, "canvas toolbar");
                        if (GUILayout.Button(new GUIContent("100%", "Reset canvas zoom to 100% without moving the board."), EditorStyles.toolbarButton, GUILayout.Width(44f)))
                            EnsureCommandRouter().Execute(PungentBoardGraphCommand.ResetZoom, "canvas toolbar");
                        using (new EditorGUI.DisabledScope(!_canvasState.HasSelection))
                        {
                            if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate the selected content. Shortcut: Ctrl/Cmd+D."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                                EnsureCommandRouter().Execute(PungentBoardGraphCommand.DuplicateSelection, "canvas toolbar");
                        }

                        bool nextConnectorMode = GUILayout.Toggle(_canvasState.connectorMode, new GUIContent("Connect", "Click or drag from a node port to create an edge. Shortcut: C."), EditorStyles.toolbarButton, GUILayout.Width(68f));
                        if (nextConnectorMode != _canvasState.connectorMode)
                            EnsureCommandRouter().Execute(PungentBoardGraphCommand.ToggleConnectorMode, "canvas toolbar");

                        DrawGridSnapToolbarButtons();
                        if (GUILayout.Button(new GUIContent("?", "Show BoardGraph shortcuts, mouse controls, and graph-mode hints."), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                            OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Shortcuts, LastWindowRect());
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private float EstimatedCanvasPanelWidth()
        {
            if (position.width < 760f)
                return position.width;

            return Mathf.Max(MinCanvasWidth, position.width - _leftWidth - _rightWidth - 32f);
        }

        private void ShowCanvasAddMenu()
        {
            GenericMenu menu = new GenericMenu();
            AddMenuItem(menu, "Add Node", CurrentBoard != null, AddNodeAtCanvasCenter);
            AddMenuItem(menu, "Add Group", CurrentBoard != null, AddGroupAtCanvasCenter);
            PungentBoardDocument board = CurrentBoard;
            List<PungentBoardCustomNodeTemplate> templates = (PungentBoardCustomNodeTemplateStorage.Database.templates ?? new List<PungentBoardCustomNodeTemplate>())
                .Where(template => IsCustomTemplateCompatible(board, template))
                .OrderBy(template => template.displayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (templates.Count > 0)
            {
                menu.AddSeparator("Custom Group Nodes/");
                foreach (PungentBoardCustomNodeTemplate template in templates)
                {
                    PungentBoardCustomNodeTemplate captured = template;
                    AddMenuItem(menu, "Custom Group Nodes/" + (string.IsNullOrWhiteSpace(captured.displayName) ? captured.id : captured.displayName), board != null, () => AddCustomGroupNodeAtCanvasCenter(captured));
                }
            }
            menu.ShowAsContext();
        }

        private void ShowCanvasViewMenu()
        {
            GenericMenu menu = new GenericMenu();
            AddMenuItem(menu, "Frame Selected", CurrentBoard != null, FrameSelection);
            AddMenuItem(menu, "Fit All", CurrentBoard != null, FitCurrentBoard);
            AddMenuItem(menu, "Zoom/100%", CurrentBoard != null, ResetCanvasZoom);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(_canvasState.showGrid ? "Hide Grid" : "Show Grid"), false, ToggleGrid);
            menu.AddItem(new GUIContent("Grid Settings..."), false, () => OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Grid, Rect.zero));
            menu.AddItem(new GUIContent(_canvasState.snapToGrid ? "Disable Snap" : "Enable Snap"), false, ToggleSnap);
            menu.AddItem(new GUIContent("Snap Settings..."), false, () => OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Snap, Rect.zero));
            menu.AddSeparator("Snap Size/");
            menu.AddItem(new GUIContent("Snap Size/12"), Mathf.Approximately(_canvasState.snapSize, 12f), () => SetSnapSize(12f));
            menu.AddItem(new GUIContent("Snap Size/24"), Mathf.Approximately(_canvasState.snapSize, 24f), () => SetSnapSize(24f));
            menu.AddItem(new GUIContent("Snap Size/48"), Mathf.Approximately(_canvasState.snapSize, 48f), () => SetSnapSize(48f));
            menu.AddItem(new GUIContent("Snap Size/96"), Mathf.Approximately(_canvasState.snapSize, 96f), () => SetSnapSize(96f));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Shortcuts And Mouse Controls"), false, () => OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Shortcuts, Rect.zero));
            menu.ShowAsContext();
        }

        private void ShowCanvasEditMenu()
        {
            GenericMenu menu = new GenericMenu();
            AddMenuItem(menu, "Duplicate Selection", CurrentBoard != null && _canvasState.HasSelection, DuplicateSelectedNode);
            menu.AddItem(new GUIContent(_canvasState.connectorMode ? "Cancel Connector Mode" : "Start Connector Mode"), false, () => SetConnectorMode(!_canvasState.connectorMode));
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Align/Align X", CurrentBoard != null && _canvasState.SelectedNodeCount >= 2, () => AlignSelection(false));
            AddMenuItem(menu, "Align/Align Y", CurrentBoard != null && _canvasState.SelectedNodeCount >= 2, () => AlignSelection(true));
            AddMenuItem(menu, "Distribute/Distribute X", CurrentBoard != null && _canvasState.SelectedNodeCount >= 3, () => DistributeSelection(true));
            AddMenuItem(menu, "Distribute/Distribute Y", CurrentBoard != null && _canvasState.SelectedNodeCount >= 3, () => DistributeSelection(false));
            menu.ShowAsContext();
        }

        private void ResetCanvasZoom()
        {
            _canvasState.zoom = 1f;
            PersistViewState();
            Repaint();
        }

        private void ToggleGrid()
        {
            _canvasState.showGrid = !_canvasState.showGrid;
            PersistViewState();
            Repaint();
        }

        private void ToggleSnap()
        {
            _canvasState.snapToGrid = !_canvasState.snapToGrid;
            PersistViewState();
            Repaint();
        }

        private void SetSnapSize(float size)
        {
            _canvasState.snapSize = Mathf.Clamp(size, 4f, 240f);
            _canvasState.snapToGrid = true;
            PersistViewState();
            Repaint();
        }

        private void DrawGridSnapToolbarButtons()
        {
            bool nextGrid = GUILayout.Toggle(_canvasState.showGrid, new GUIContent("Grid", "Show or hide the board grid. Use the adjacent menu for spacing and opacity."), EditorStyles.toolbarButton, GUILayout.Width(44f));
            if (nextGrid != _canvasState.showGrid)
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.ToggleGrid, "canvas toolbar");
            Rect gridRect = LastWindowRect();
            if (GUILayout.Button(new GUIContent("v", "Grid settings."), EditorStyles.toolbarDropDown, GUILayout.Width(22f)))
                OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Grid, gridRect);

            bool nextSnap = GUILayout.Toggle(_canvasState.snapToGrid, new GUIContent("Snap", "Snap moved nodes, groups, and resize handles. Use the adjacent menu for detail."), EditorStyles.toolbarButton, GUILayout.Width(46f));
            if (nextSnap != _canvasState.snapToGrid)
                EnsureCommandRouter().Execute(PungentBoardGraphCommand.ToggleSnap, "canvas toolbar");
            Rect snapRect = LastWindowRect();
            if (GUILayout.Button(new GUIContent("v", "Snap settings."), EditorStyles.toolbarDropDown, GUILayout.Width(22f)))
                OpenCanvasOverlay(PungentBoardCanvasOverlayKind.Snap, snapRect);
        }

        private Rect LastWindowRect()
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(rect.position);
            return new Rect(screenPoint.x - position.x, screenPoint.y - position.y, rect.width, rect.height);
        }

        private void SetConnectorMode(bool enabled)
        {
            _canvasState.connectorMode = enabled;
            _canvasState.connectorSourceNodeId = string.Empty;
            _canvasState.connectorSourcePortKey = string.Empty;
            _canvasState.connectionWarning = string.Empty;
            Repaint();
        }

        private void CancelTransientCanvasAction()
        {
            CloseCanvasOverlay();
            _canvasState.inlineEdit.Clear();
            _canvasState.connectorMode = false;
            _canvasState.connectorDragging = false;
            _canvasState.connectorSourceNodeId = string.Empty;
            _canvasState.connectorSourcePortKey = string.Empty;
            _canvasState.connectionWarning = string.Empty;
            _canvasState.marqueeSelecting = false;
            _status = "Cancelled transient board action.";
            GUI.FocusControl(null);
            Repaint();
        }

        private void OpenCanvasOverlay(PungentBoardCanvasOverlayKind kind, Rect anchorRect)
        {
            _activeCanvasOverlay = kind;
            _canvasOverlayAnchorRect = anchorRect;
            _canvasOverlayRect = Rect.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void CloseCanvasOverlay()
        {
            _activeCanvasOverlay = PungentBoardCanvasOverlayKind.None;
            _canvasOverlayAnchorRect = Rect.zero;
            _canvasOverlayRect = Rect.zero;
        }

        private void HandleActiveCanvasOverlayInput()
        {
            if (_activeCanvasOverlay == PungentBoardCanvasOverlayKind.None)
                return;

            CalculateCanvasOverlayRect();
            Event evt = Event.current;
            if (evt == null)
                return;

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                CloseCanvasOverlay();
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown &&
                !_canvasOverlayRect.Contains(evt.mousePosition) &&
                !_canvasOverlayAnchorRect.Contains(evt.mousePosition))
            {
                CloseCanvasOverlay();
                evt.Use();
            }
        }

        private Rect CalculateCanvasOverlayRect()
        {
            switch (_activeCanvasOverlay)
            {
                case PungentBoardCanvasOverlayKind.Grid:
                    return CalculateCanvasOverlayRect(190f, 360f, 74f);
                case PungentBoardCanvasOverlayKind.Snap:
                    return CalculateCanvasOverlayRect(206f, 360f, 74f);
                case PungentBoardCanvasOverlayKind.Shortcuts:
                    return CalculateCanvasOverlayRect(Mathf.Min(390f, Mathf.Max(260f, position.height - 110f)), 560f, 74f);
                default:
                    _canvasOverlayRect = Rect.zero;
                    return _canvasOverlayRect;
            }
        }

        private Rect CalculateCanvasOverlayRect(float height, float maxWidth, float defaultTop)
        {
            float width = Mathf.Clamp(position.width - 48f, 280f, maxWidth);
            float x = _canvasOverlayAnchorRect.width > 0f
                ? _canvasOverlayAnchorRect.center.x - width * 0.5f
                : (position.width - width) * 0.5f;
            x = Mathf.Clamp(x, 12f, Mathf.Max(12f, position.width - width - 12f));

            float y = _canvasOverlayAnchorRect.height > 0f
                ? Mathf.Max(defaultTop, _canvasOverlayAnchorRect.yMax + 4f)
                : defaultTop;
            y = Mathf.Min(y, Mathf.Max(defaultTop, position.height - height - StatusBarHeight - 8f));

            _canvasOverlayRect = new Rect(x, y, width, height);
            return _canvasOverlayRect;
        }

        private void DrawActiveCanvasOverlay()
        {
            if (_activeCanvasOverlay == PungentBoardCanvasOverlayKind.None)
                return;

            Rect trayRect = CalculateCanvasOverlayRect();
            DrawCanvasOverlayChrome(trayRect);
            GUILayout.BeginArea(trayRect, EditorStyles.helpBox);
            try
            {
                if (_activeCanvasOverlay == PungentBoardCanvasOverlayKind.Grid)
                    DrawGridSettingsOverlay();
                else if (_activeCanvasOverlay == PungentBoardCanvasOverlayKind.Snap)
                    DrawSnapSettingsOverlay();
                else if (_activeCanvasOverlay == PungentBoardCanvasOverlayKind.Shortcuts)
                    DrawShortcutHelpOverlay();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawCanvasOverlayChrome(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.26f));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 0.98f) : new Color(0.90f, 0.91f, 0.92f, 0.98f));
        }

        private void DrawGridSettingsOverlay()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Close", "Close grid settings."), EditorStyles.miniButton, GUILayout.Width(52f)))
                    CloseCanvasOverlay();
            }

            EditorGUI.BeginChangeCheck();
            _canvasState.showGrid = EditorGUILayout.Toggle(new GUIContent("Show Grid", "Draw the board grid behind graph content."), _canvasState.showGrid);
            _canvasState.gridMinorSpacing = EditorGUILayout.Slider(new GUIContent("Minor Spacing", "Distance between minor grid lines in canvas units."), _canvasState.gridMinorSpacing, 8f, 120f);
            _canvasState.gridMajorLineFrequency = EditorGUILayout.IntSlider(new GUIContent("Major Every", "Draw a stronger grid line every N minor lines."), _canvasState.gridMajorLineFrequency, 2, 12);
            _canvasState.gridOpacity = EditorGUILayout.Slider(new GUIContent("Opacity", "Grid visibility. Lower values keep dense graphs calmer."), _canvasState.gridOpacity, 0.10f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _canvasState.viewChanged = true;
                PersistViewState();
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Reset", "Restore default grid spacing and opacity."), GUILayout.Width(72f)))
                {
                    _canvasState.showGrid = true;
                    _canvasState.gridMinorSpacing = 24f;
                    _canvasState.gridMajorLineFrequency = 5;
                    _canvasState.gridOpacity = 1f;
                    PersistViewState();
                    Repaint();
                }
            }
        }

        private void DrawSnapSettingsOverlay()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Snap", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Close", "Close snap settings."), EditorStyles.miniButton, GUILayout.Width(52f)))
                    CloseCanvasOverlay();
            }

            EditorGUI.BeginChangeCheck();
            _canvasState.snapToGrid = EditorGUILayout.Toggle(new GUIContent("Enable Snap", "Snap moved or resized graph objects."), _canvasState.snapToGrid);
            using (new EditorGUI.DisabledScope(!_canvasState.snapToGrid))
            {
                _canvasState.snapSize = EditorGUILayout.Slider(new GUIContent("Distance", "Snap interval in canvas units."), _canvasState.snapSize, 4f, 120f);
                _canvasState.snapNodes = EditorGUILayout.Toggle(new GUIContent("Snap Nodes", "Snap node movement."), _canvasState.snapNodes);
                _canvasState.snapGroups = EditorGUILayout.Toggle(new GUIContent("Snap Groups", "Snap group movement."), _canvasState.snapGroups);
                _canvasState.snapResize = EditorGUILayout.Toggle(new GUIContent("Snap Resize", "Snap node and group resize handles."), _canvasState.snapResize);
            }
            if (EditorGUI.EndChangeCheck())
            {
                _canvasState.viewChanged = true;
                PersistViewState();
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Reset", "Restore default snap behavior."), GUILayout.Width(72f)))
                {
                    _canvasState.snapToGrid = false;
                    _canvasState.snapSize = 24f;
                    _canvasState.snapNodes = true;
                    _canvasState.snapGroups = true;
                    _canvasState.snapResize = true;
                    PersistViewState();
                    Repaint();
                }
            }
        }

        private void DrawShortcutHelpOverlay()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Shortcuts", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Close", "Close shortcut help."), EditorStyles.miniButton, GUILayout.Width(52f)))
                    CloseCanvasOverlay();
            }

            DrawShortcutRow("Ctrl/Cmd+S", "Save board");
            DrawShortcutRow("Ctrl/Cmd+Z", "Undo");
            DrawShortcutRow("Ctrl/Cmd+Y / Ctrl/Cmd+Shift+Z", "Redo");
            DrawShortcutRow("Delete / Backspace", "Delete selection");
            DrawShortcutRow("F / Shift+F", "Frame selection / fit all");
            DrawShortcutRow("N / G", "Add node / add group");
            DrawShortcutRow("Ctrl/Cmd+D", "Duplicate selection");
            DrawShortcutRow("C", "Toggle connector mode");
            DrawShortcutRow("Esc", "Cancel connector, marquee, overlay, or inline edit");
            DrawShortcutRow("?", "Show this help");
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox("Mouse: drag nodes/groups to move, drag bottom-right handles to resize, middle mouse or Alt+drag to pan, scroll to zoom, double-click canvas to add a node, drag from output ports to input ports to connect.", MessageType.Info);
            PungentBoardDocument board = CurrentBoard;
            PungentBoardGraphTypeDefinition graphType = board != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId) : null;
            if (graphType != null && !string.IsNullOrWhiteSpace(graphType.displayName))
                EditorGUILayout.HelpBox("Current graph mode: " + graphType.displayName + ". The palette, valid ports, edge rules, and inspector fields adapt to this mode.", MessageType.None);
        }

        private static void DrawShortcutRow(string shortcut, string action)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(shortcut, EditorStyles.miniBoldLabel, GUILayout.Width(168f));
                GUILayout.Label(action, EditorStyles.miniLabel);
            }
        }

        private void DrawInspectorPanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.10f, 0.04f), options))
            {
                UtilityWindowTheme.SectionTitle("Inspector", UtilityWindowTheme.Blue, SelectionPill());
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                bool deleteRequested;
                PungentBoardDocument board = CurrentBoard;
                PungentBoardEditorMutationContext mutation = _history.Capture(board, "Inspector Edit");
                bool changed = PungentBoardNodeInspectorGUI.DrawInspector(CurrentBoard, _canvasState, out deleteRequested);
                if (changed)
                {
                    _history.Commit(mutation, board);
                    MarkContentDirty("Board inspector changed.");
                    _validationResult = null;
                    _projectionPreview = null;
                }
                if (deleteRequested)
                    ScheduleDeleteSelectedWithConfirmation();
                DrawEventSequencePanel();
                DrawOptionalProviderFallbackPreview();
                DrawValidationResult();
                DrawIntegrationPanel();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawOptionalProviderFallbackPreview()
        {
            if (!_showOptionalProviderFallbackPreview)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Missing Provider Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Developer-safe simulation: a linked item with provider 'missing-boardgraph-provider' would remain visible, keep its node data, and disable unsupported Open/Preview actions. This does not uninstall packages, change project configuration, or mutate this board.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Provider", "missing-boardgraph-provider");
                EditorGUILayout.TextField("Item ID", "demo-missing-authoring-item");
            }
        }

        private sealed class EventSequenceRow
        {
            public PungentBoardNode node;
            public PungentBoardEdge incomingEdge;
            public int depth;
            public bool reachable;
        }

        private void DrawEventSequencePanel()
        {
            PungentBoardDocument board = CurrentBoard;
            if (!PungentBoardEventSequenceUtility.IsEventSequence(board))
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Event Sequence", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Author step order, branch paths, and sequencing fields here. This is an editor preview of authored flow only; no scene code is invoked.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(board == null || (board.edges?.Count ?? 0) == 0))
                {
                    if (GUILayout.Button(new GUIContent("Normalize Orders", "Renumber outgoing Event Sequence edge order values from each source node."), EditorStyles.toolbarButton, GUILayout.Width(112f)))
                    {
                        _history.RecordBefore(board, "Normalize Event Sequence Orders");
                        int changed = PungentBoardEventSequenceUtility.NormalizeAllOutgoingOrders(board);
                        if (changed > 0)
                            MarkEventSequenceChanged("Normalized " + changed + " sequence order value(s).");
                    }
                }

                using (new EditorGUI.DisabledScope(board == null))
                {
                    if (GUILayout.Button(new GUIContent("Validate", "Run local board validation."), EditorStyles.toolbarButton, GUILayout.Width(64f)))
                        ValidateCurrentBoard();
                }

                GUILayout.FlexibleSpace();
            }

            if (board == null || (board.nodes?.Count ?? 0) == 0)
            {
                EditorGUILayout.HelpBox("Add Event, Delay, Camera Move, Trigger, Wait, or Branch nodes to build a sequence.", MessageType.Info);
                return;
            }

            List<EventSequenceRow> rows = BuildEventSequenceRows(board);
            _eventSequenceScroll = EditorGUILayout.BeginScrollView(_eventSequenceScroll, GUILayout.MinHeight(110f), GUILayout.MaxHeight(260f));
            foreach (EventSequenceRow row in rows)
                DrawEventSequenceRow(board, row);
            EditorGUILayout.EndScrollView();

            DrawEventSequenceConnectionList(board);
        }

        private List<EventSequenceRow> BuildEventSequenceRows(PungentBoardDocument board)
        {
            List<EventSequenceRow> rows = new List<EventSequenceRow>();
            if (board == null)
                return rows;

            Dictionary<string, PungentBoardNode> nodesById = (board.nodes ?? new List<PungentBoardNode>())
                .Where(node => node != null && !string.IsNullOrWhiteSpace(node.id))
                .GroupBy(node => node.id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> incomingNodeIds = new HashSet<string>((board.edges ?? new List<PungentBoardEdge>())
                .Where(edge => edge != null && PungentBoardEventSequenceUtility.IsEventSequenceEdgeType(edge.edgeTypeKey) && !string.IsNullOrWhiteSpace(edge.toNodeId))
                .Select(edge => edge.toNodeId), StringComparer.OrdinalIgnoreCase);
            List<PungentBoardNode> roots = nodesById.Values
                .Where(node => string.Equals(PungentBoardEventSequenceUtility.CleanKey(node.nodeTypeKey), "event", StringComparison.OrdinalIgnoreCase) && !incomingNodeIds.Contains(node.id))
                .OrderBy(node => node.position.x)
                .ThenBy(node => node.position.y)
                .ThenBy(node => node.title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roots.Count == 0)
                roots = nodesById.Values
                    .Where(node => string.Equals(PungentBoardEventSequenceUtility.CleanKey(node.nodeTypeKey), "event", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(node => node.position.x)
                    .ThenBy(node => node.position.y)
                    .ThenBy(node => node.title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            if (roots.Count == 0)
                roots = nodesById.Values.OrderBy(node => node.position.x).ThenBy(node => node.position.y).ThenBy(node => node.title, StringComparer.OrdinalIgnoreCase).ToList();

            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardNode root in roots)
                AddEventSequenceRowsFrom(board, nodesById, root, null, 0, rows, visited);

            foreach (PungentBoardNode node in nodesById.Values.OrderBy(node => node.position.x).ThenBy(node => node.position.y).ThenBy(node => node.title, StringComparer.OrdinalIgnoreCase))
            {
                if (visited.Contains(node.id))
                    continue;

                rows.Add(new EventSequenceRow
                {
                    node = node,
                    incomingEdge = FindFirstIncomingEventSequenceEdge(board, node.id),
                    depth = 0,
                    reachable = false
                });
            }

            return rows;
        }

        private static void AddEventSequenceRowsFrom(PungentBoardDocument board, Dictionary<string, PungentBoardNode> nodesById, PungentBoardNode node, PungentBoardEdge incomingEdge, int depth, List<EventSequenceRow> rows, HashSet<string> visited)
        {
            if (node == null || !visited.Add(node.id))
                return;

            rows.Add(new EventSequenceRow
            {
                node = node,
                incomingEdge = incomingEdge,
                depth = Mathf.Clamp(depth, 0, 8),
                reachable = true
            });

            foreach (PungentBoardEdge edge in PungentBoardEventSequenceUtility.OrderedOutgoingEdges(board, node.id))
            {
                PungentBoardNode toNode;
                if (edge != null && nodesById.TryGetValue(edge.toNodeId, out toNode))
                    AddEventSequenceRowsFrom(board, nodesById, toNode, edge, depth + 1, rows, visited);
            }
        }

        private static PungentBoardEdge FindFirstIncomingEventSequenceEdge(PungentBoardDocument board, string nodeId)
        {
            return (board?.edges ?? new List<PungentBoardEdge>())
                .Where(edge => edge != null && PungentAuthoringId.EqualsId(edge.toNodeId, nodeId) && PungentBoardEventSequenceUtility.IsEventSequenceEdgeType(edge.edgeTypeKey))
                .OrderBy(edge => edge.executionOrder)
                .FirstOrDefault();
        }

        private void DrawEventSequenceRow(PungentBoardDocument board, EventSequenceRow row)
        {
            if (row == null || row.node == null)
                return;

            using (new EditorGUILayout.HorizontalScope(row.reachable ? GUIStyle.none : EditorStyles.helpBox))
            {
                GUILayout.Space(row.depth * 12f);
                string type = string.IsNullOrWhiteSpace(row.node.nodeTypeKey) ? row.node.nodeKind.ToString() : row.node.nodeTypeKey;
                string title = string.IsNullOrWhiteSpace(row.node.title) ? row.node.id : row.node.title;
                if (GUILayout.Button(new GUIContent(title, type), EditorStyles.miniButtonLeft, GUILayout.MinWidth(80f)))
                {
                    _canvasState.SelectNode(row.node.id);
                    PersistViewState();
                    Repaint();
                }

                GUILayout.Label(type, EditorStyles.miniLabel, GUILayout.Width(82f));

                if (row.incomingEdge != null)
                {
                    int current = row.incomingEdge.executionOrder;
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.IntField(current, GUILayout.Width(42f));
                    if (EditorGUI.EndChangeCheck() && next != current)
                    {
                        _history.RecordBefore(board, "Edit Event Sequence Order");
                        row.incomingEdge.executionOrder = next;
                        MarkEventSequenceChanged("Updated sequence edge order.");
                    }

                    if (GUILayout.Button(new GUIContent("Edge", PungentBoardEventSequenceUtility.EdgeDisplayLabel(board, row.incomingEdge)), EditorStyles.miniButtonRight, GUILayout.Width(42f)))
                    {
                        _canvasState.SelectEdge(row.incomingEdge.id);
                        PersistViewState();
                        Repaint();
                    }
                }
                else
                {
                    GUILayout.Label("root", EditorStyles.miniLabel, GUILayout.Width(88f));
                }

                if (GUILayout.Button(new GUIContent("Frame", "Frame this step on the canvas."), EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    _canvasState.SelectNode(row.node.id);
                    FrameSelection();
                }
            }
        }

        private void DrawEventSequenceConnectionList(PungentBoardDocument board)
        {
            List<PungentBoardEdge> edges = (board.edges ?? new List<PungentBoardEdge>())
                .Where(edge => edge != null && PungentBoardEventSequenceUtility.IsEventSequenceEdgeType(edge.edgeTypeKey))
                .OrderBy(edge => edge.fromNodeId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.executionOrder)
                .ThenBy(edge => edge.id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (edges.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Sequence Connections", EditorStyles.boldLabel);
            foreach (PungentBoardEdge edge in edges)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = PungentBoardEventSequenceUtility.EdgeDisplayLabel(board, edge);
                    if (GUILayout.Button(new GUIContent(label, BuildEdgeEndpointLabel(board, edge)), EditorStyles.miniButton, GUILayout.MinWidth(90f)))
                    {
                        _canvasState.SelectEdge(edge.id);
                        PersistViewState();
                        Repaint();
                    }

                    int current = edge.executionOrder;
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.IntField(current, GUILayout.Width(42f));
                    if (EditorGUI.EndChangeCheck() && next != current)
                    {
                        _history.RecordBefore(board, "Edit Event Sequence Order");
                        edge.executionOrder = next;
                        MarkEventSequenceChanged("Updated sequence edge order.");
                    }
                }
            }
        }

        private void MarkEventSequenceChanged(string status)
        {
            _validationResult = null;
            _projectionPreview = null;
            MarkContentDirty(status);
            Repaint();
        }

        private void DrawValidationResult()
        {
            if (_validationResult == null)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            MessageType type = _validationResult.status == PungentAuthoringValidationStatus.Valid ? MessageType.Info :
                _validationResult.status == PungentAuthoringValidationStatus.ValidWithWarnings ? MessageType.Warning : MessageType.Error;
            EditorGUILayout.HelpBox(_validationResult.status + " (" + (_validationResult.issues?.Count ?? 0) + " issue(s))", type);
            List<PungentAuthoringValidationIssue> issues = _validationResult.issues ?? new List<PungentAuthoringValidationIssue>();
            List<PungentAuthoringValidationIssue> schemaIssues = issues.Where(IsSchemaIssue).ToList();
            List<PungentAuthoringValidationIssue> localIssues = issues.Where(issue => !IsSchemaIssue(issue)).ToList();
            if (schemaIssues.Count > 0)
            {
                EditorGUILayout.LabelField("Schema", EditorStyles.boldLabel);
                foreach (PungentAuthoringValidationIssue issue in schemaIssues)
                {
                    if (issue != null)
                        DrawValidationIssue(issue);
                }
            }

            if (localIssues.Count > 0)
            {
                EditorGUILayout.LabelField("Local Data", EditorStyles.boldLabel);
            }

            foreach (PungentAuthoringValidationIssue issue in localIssues)
            {
                if (issue != null)
                    DrawValidationIssue(issue);
            }
        }

        private static bool IsSchemaIssue(PungentAuthoringValidationIssue issue)
        {
            if (issue == null || string.IsNullOrWhiteSpace(issue.issueCode))
                return false;

            string code = issue.issueCode;
            return string.Equals(code, "MISSING_GRAPH_TYPE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "MISSING_NODE_TYPE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "MISSING_GRAPH_ROOT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "MISSING_REQUIRED_PORT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "MISSING_EDGE_RULE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "ILLEGAL_NODE_CONNECTION", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "INVALID_EDGE_DIRECTION", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "INVALID_PORT_CONNECTION", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "GRAPH_CYCLE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, "DISCONNECTED_EXECUTION_PATH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, PungentBoardEventSequenceUtility.IssueDuplicateOrder, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, PungentBoardEventSequenceUtility.IssueInvalidDelay, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, PungentBoardEventSequenceUtility.IssueMissingStepValue, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, PungentBoardEventSequenceUtility.IssueBranchEdgeLabel, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawValidationIssue(PungentAuthoringValidationIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(issue.severity + ": " + issue.message, EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(issue.sourcePathKeyOrId)))
                    {
                        if (GUILayout.Button(new GUIContent("Focus", "Select and frame the affected board element."), GUILayout.Width(62f)))
                            FocusValidationSource(issue.sourcePathKeyOrId);
                    }

                    if (CanQuickFix(issue) && GUILayout.Button(new GUIContent("Quick Fix", "Apply a safe local board fix."), GUILayout.Width(78f)))
                        ApplyValidationQuickFix(issue);

                    GUILayout.FlexibleSpace();
                    if (!string.IsNullOrWhiteSpace(issue.issueCode))
                        GUILayout.Label(issue.issueCode, EditorStyles.miniLabel, GUILayout.Width(132f));
                }
            }
        }

        private void DrawIntegrationPanel()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            EditorGUILayout.Space(8f);
            _integrationFoldout = EditorGUILayout.Foldout(_integrationFoldout, "Import / Refresh", true);
            if (!_integrationFoldout)
                return;

            PungentBoardIntegrationProfileDatabase database = PungentBoardIntegrationProfileStorage.Database;
            List<PungentBoardIntegrationProfile> profiles = database.profiles ?? new List<PungentBoardIntegrationProfile>();
            if (profiles.Count == 0)
                profiles.Add(PungentBoardIntegrationProfile.CreateDefault());

            string[] labels = profiles.Select(profile => profile.displayName).ToArray();
            int currentIndex = Mathf.Max(0, profiles.FindIndex(profile => string.Equals(profile.id, _selectedProfileId, StringComparison.OrdinalIgnoreCase)));
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(new GUIContent("Profile", "Choose a configurable board integration profile."), currentIndex, labels);
            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < profiles.Count)
            {
                _selectedProfileId = profiles[nextIndex].id;
                UtilityWindowPrefs.SetString(PrefIntegrationProfile, _selectedProfileId);
                board.integrationProfileId = _selectedProfileId;
                _projectionPreview = null;
                MarkContentDirty("Selected board integration profile.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("New Component Profile", "Create a profile for selected GameObjects, Components, and explicit asset roots."), GUILayout.Height(20f)))
                {
                    PungentBoardIntegrationProfile profile = PungentBoardIntegrationProfile.CreateComponentSelectionDefault();
                    profile.id = Guid.NewGuid().ToString("N");
                    database.profiles.Add(profile);
                    _selectedProfileId = profile.id;
                    UtilityWindowPrefs.SetString(PrefIntegrationProfile, _selectedProfileId);
                    board.integrationProfileId = _selectedProfileId;
                    _projectionPreview = null;
                    _status = "Created component selection integration profile. Save Profile to persist.";
                }

                if (GUILayout.Button(new GUIContent("Duplicate Profile", "Duplicate the selected profile for a custom mapping variant."), GUILayout.Height(20f)))
                {
                    PungentBoardIntegrationProfile source = PungentBoardIntegrationProfileStorage.Find(_selectedProfileId);
                    if (source != null)
                    {
                        PungentBoardIntegrationProfile copy = JsonUtility.FromJson<PungentBoardIntegrationProfile>(JsonUtility.ToJson(source));
                        copy.id = Guid.NewGuid().ToString("N");
                        copy.displayName = source.displayName + " Copy";
                        copy.NormalizeInPlace();
                        database.profiles.Add(copy);
                        _selectedProfileId = copy.id;
                        UtilityWindowPrefs.SetString(PrefIntegrationProfile, _selectedProfileId);
                        board.integrationProfileId = _selectedProfileId;
                        _projectionPreview = null;
                        _status = "Duplicated integration profile. Save Profile to persist.";
                    }
                }

                if (GUILayout.Button(new GUIContent("Reload Profiles", "Reload saved integration profiles from ProjectSettings."), GUILayout.Height(20f)))
                {
                    PungentBoardIntegrationProfileStorage.Reload();
                    PungentBoardIntegrationProfile reloaded = PungentBoardIntegrationProfileStorage.Find(_selectedProfileId);
                    if (reloaded == null)
                    {
                        _selectedProfileId = PungentBoardIntegrationProfileStorage.Database.profiles.FirstOrDefault()?.id ?? string.Empty;
                        UtilityWindowPrefs.SetString(PrefIntegrationProfile, _selectedProfileId);
                        board.integrationProfileId = _selectedProfileId;
                    }
                    _projectionPreview = null;
                    _status = "Reloaded board integration profiles from disk.";
                }
            }

            PungentBoardIntegrationProfile selectedProfile = PungentBoardIntegrationProfileStorage.Find(_selectedProfileId);
            if (selectedProfile != null)
            {
                EditorGUILayout.LabelField(selectedProfile.description, EditorStyles.wordWrappedMiniLabel);
                DrawProfileSummaryChips(selectedProfile);
                DrawProfileSettings(selectedProfile);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Preview Import", "Enumerate configured providers once and preview non-destructive board changes."), GUILayout.Height(22f)))
                    PreviewImport();

                using (new EditorGUI.DisabledScope(_projectionPreview == null || _projectionPreview.SelectedCount == 0))
                {
                    if (GUILayout.Button(new GUIContent("Apply Selected", "Apply selected preview changes to this board."), GUILayout.Height(22f)))
                        ApplyProjectionPreview();
                }
            }

            if (!string.IsNullOrWhiteSpace(PungentBoardIntegrationProfileStorage.LoadError))
                EditorGUILayout.HelpBox("Profile load warning: " + PungentBoardIntegrationProfileStorage.LoadError, MessageType.Warning);

            DrawProjectionPreview();
        }

        private void DrawProjectionPreview()
        {
            if (_projectionPreview == null)
                return;

            foreach (string message in _projectionPreview.messages)
                if (!string.IsNullOrWhiteSpace(message))
                    EditorGUILayout.HelpBox(message, MessageType.Info);

            DrawProjectionPreviewSummary();

            _projectionScroll = EditorGUILayout.BeginScrollView(_projectionScroll, GUILayout.MinHeight(90f), GUILayout.MaxHeight(220f));
            foreach (PungentBoardProjectionChange change in _projectionPreview.changes)
            {
                if (change == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    change.selected = EditorGUILayout.Toggle(change.selected, GUILayout.Width(18f));
                    GUILayout.Label(change.changeKind.ToString(), EditorStyles.miniLabel, GUILayout.Width(82f));
                    string title = string.IsNullOrWhiteSpace(change.title) ? string.IsNullOrWhiteSpace(change.groupTitle) ? change.sourceKey : change.groupTitle : change.title;
                    GUILayout.Label(title, EditorStyles.wordWrappedMiniLabel);
                    if (!string.IsNullOrWhiteSpace(change.skipReason))
                        GUILayout.Label(change.skipReason, EditorStyles.miniLabel, GUILayout.Width(180f));
                    else if (!string.IsNullOrWhiteSpace(change.sourceLabel))
                        GUILayout.Label(change.sourceLabel, EditorStyles.miniLabel, GUILayout.Width(92f));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawProjectionPreviewSummary()
        {
            List<PungentBoardProjectionChange> changes = _projectionPreview.changes ?? new List<PungentBoardProjectionChange>();
            int adds = changes.Count(IsAddChange);
            int updates = changes.Count(IsUpdateChange);
            int stale = changes.Count(change => change != null && change.changeKind == PungentBoardProjectionChangeKind.MarkStale);
            int skips = changes.Count(change => change != null && change.changeKind == PungentBoardProjectionChangeKind.Skip);
            int selected = _projectionPreview.SelectedCount;
            EditorGUILayout.LabelField("Preview Summary", adds + " add(s), " + updates + " update(s), " + stale + " stale marker(s), " + skips + " skipped, " + selected + " selected", EditorStyles.miniLabel);

            if (adds >= 50)
                EditorGUILayout.HelpBox("Large import preview: review and use Select Adds/Updates/None before applying. Broad providers and large selections are capped and never apply automatically.", MessageType.Warning);

            Dictionary<string, int> grouped = changes
                .Where(change => change != null)
                .GroupBy(change => string.IsNullOrWhiteSpace(change.sourceLabel) ? string.IsNullOrWhiteSpace(change.adapterId) ? "Unknown Source" : change.adapterId : change.sourceLabel)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            if (grouped.Count > 0)
                EditorGUILayout.LabelField(string.Join("  |  ", grouped.Select(pair => pair.Key + ": " + pair.Value).ToArray()), EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("Select None", "Clear every preview selection."), EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    SetProjectionSelection(_ => false);
                if (GUILayout.Button(new GUIContent("Select Adds", "Select add-node, add-edge, and add-group changes only."), EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    SetProjectionSelection(IsAddChange);
                if (GUILayout.Button(new GUIContent("Select Updates", "Select update-node, update-edge, update-group, and stale marker changes only."), EditorStyles.toolbarButton, GUILayout.Width(94f)))
                    SetProjectionSelection(change => IsUpdateChange(change) || (change != null && change.changeKind == PungentBoardProjectionChangeKind.MarkStale));
                GUILayout.FlexibleSpace();
            }
        }

        private void SetProjectionSelection(Func<PungentBoardProjectionChange, bool> predicate)
        {
            if (_projectionPreview == null || _projectionPreview.changes == null)
                return;

            foreach (PungentBoardProjectionChange change in _projectionPreview.changes)
            {
                if (change == null || change.changeKind == PungentBoardProjectionChangeKind.Skip)
                    continue;
                change.selected = predicate != null && predicate(change);
            }
        }

        private static bool IsAddChange(PungentBoardProjectionChange change)
        {
            return change != null &&
                   (change.changeKind == PungentBoardProjectionChangeKind.AddNode ||
                    change.changeKind == PungentBoardProjectionChangeKind.AddEdge ||
                    change.changeKind == PungentBoardProjectionChangeKind.AddGroup);
        }

        private static bool IsUpdateChange(PungentBoardProjectionChange change)
        {
            return change != null &&
                   (change.changeKind == PungentBoardProjectionChangeKind.UpdateNode ||
                    change.changeKind == PungentBoardProjectionChangeKind.UpdateEdge ||
                    change.changeKind == PungentBoardProjectionChangeKind.UpdateGroup);
        }

        private void DrawProfileSummaryChips(PungentBoardIntegrationProfile profile)
        {
            if (profile == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Sources: " + profile.sourceScopes, EditorStyles.miniLabel);
                GUILayout.Label("Cap: " + profile.maxItemsPerRefresh, EditorStyles.miniLabel, GUILayout.Width(64f));
                GUILayout.Label("Fields: " + (profile.fieldMappings?.Count ?? 0), EditorStyles.miniLabel, GUILayout.Width(64f));
                GUILayout.Label("Relations: " + (profile.relationMappings?.Count ?? 0), EditorStyles.miniLabel, GUILayout.Width(82f));
                GUILayout.Label("Groups: " + (profile.groupMappings?.Count ?? 0), EditorStyles.miniLabel, GUILayout.Width(70f));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawProfileSettings(PungentBoardIntegrationProfile profile)
        {
            EditorGUI.BeginChangeCheck();
            profile.displayName = EditorGUILayout.TextField(new GUIContent("Name", "Profile display name."), profile.displayName);
            profile.sourceLabel = EditorGUILayout.TextField(new GUIContent("Source Label", "Label written onto projected nodes."), profile.sourceLabel);
            profile.sourceScopes = (PungentBoardProjectionSourceScope)EditorGUILayout.EnumFlagsField(new GUIContent("Sources", "Explicit source scopes used only when Preview Import is clicked."), profile.sourceScopes);
            profile.maxItemsPerRefresh = EditorGUILayout.IntField(new GUIContent("Cap", "Maximum provider items to preview."), profile.maxItemsPerRefresh);
            if (profile.maxItemsPerRefresh > 80)
                EditorGUILayout.HelpBox("High caps are useful for deliberate broad imports, but previews can become noisy. Use Select None/Adds/Updates before applying large results.", MessageType.Warning);
            profile.updateExistingNodes = EditorGUILayout.Toggle(new GUIContent("Update Existing", "Preview updates for nodes that already came from the same provider/source key."), profile.updateExistingNodes);
            profile.createMissingNodes = EditorGUILayout.Toggle(new GUIContent("Create Missing", "Preview new nodes for provider items that do not already exist on the board."), profile.createMissingNodes);
            profile.updateExistingEdges = EditorGUILayout.Toggle(new GUIContent("Update Edges", "Preview updates for edges that already came from the same source key."), profile.updateExistingEdges);
            profile.createMissingEdges = EditorGUILayout.Toggle(new GUIContent("Create Edges", "Preview new relationship edges from mapped references."), profile.createMissingEdges);
            profile.updateExistingGroups = EditorGUILayout.Toggle(new GUIContent("Update Groups", "Preview updates for groups that already came from the same source key."), profile.updateExistingGroups);
            profile.createMissingGroups = EditorGUILayout.Toggle(new GUIContent("Create Groups", "Preview new groups from mapping rules."), profile.createMissingGroups);
            profile.createProviderGroups = EditorGUILayout.Toggle(new GUIContent("Provider Groups", "Create provider placemats for Authoring Provider imports."), profile.createProviderGroups);
            profile.markStaleMissingSources = EditorGUILayout.Toggle(new GUIContent("Mark Stale", "Preview stale/missing markers for prior nodes from this adapter that are absent from the explicit refresh."), profile.markStaleMissingSources);
            profile.propagationDirection = (PungentBoardPropagationDirection)EditorGUILayout.EnumPopup(new GUIContent("Direction", "One-way import is the safe default. Two-way fields are reserved for explicit future adapters."), profile.propagationDirection);
            string providerIds = string.Join(", ", (profile.includedProviderIds ?? new List<string>()).ToArray());
            providerIds = EditorGUILayout.TextField(new GUIContent("Provider IDs", "Optional comma-separated provider allowlist. Empty means all matching kinds."), providerIds);
            string kindList = string.Join(", ", (profile.includedKinds ?? new List<PungentAuthoringItemKind>()).Select(kind => kind.ToString()).ToArray());
            kindList = EditorGUILayout.TextField(new GUIContent("Kinds", "Comma-separated authoring item kind names. Empty means all kinds."), kindList);
            string includeTypes = string.Join(", ", (profile.includedSourceTypeNames ?? new List<string>()).ToArray());
            includeTypes = EditorGUILayout.TextField(new GUIContent("Include Types", "Optional comma-separated Unity object/component type filters. Empty means all selected source types."), includeTypes);
            string excludeTypes = string.Join(", ", (profile.excludedSourceTypeNames ?? new List<string>()).ToArray());
            excludeTypes = EditorGUILayout.TextField(new GUIContent("Exclude Types", "Optional comma-separated Unity object/component type filters to skip."), excludeTypes);
            string explicitRoots = string.Join(", ", (profile.explicitRootPaths ?? new List<string>()).ToArray());
            explicitRoots = EditorGUILayout.TextField(new GUIContent("Explicit Roots", "Optional comma-separated Assets paths. Folder contents are enumerated only when Preview Import is clicked."), explicitRoots);
            if (EditorGUI.EndChangeCheck())
            {
                profile.includedProviderIds = SplitCsv(providerIds);
                profile.includedKinds = ParseKinds(kindList);
                profile.includedSourceTypeNames = SplitCsv(includeTypes);
                profile.excludedSourceTypeNames = SplitCsv(excludeTypes);
                profile.explicitRootPaths = SplitCsv(explicitRoots);
                profile.NormalizeInPlace();
                _projectionPreview = null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Selection", EditorStyles.miniLabel, GUILayout.Width(62f));
                GUILayout.Label((Selection.objects?.Length ?? 0) + " object(s)", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Add Selected Roots", "Append selected asset/folder paths to Explicit Roots. No folder scan runs until Preview Import."), GUILayout.Width(126f)))
                {
                    AddSelectedAssetRoots(profile);
                    _projectionPreview = null;
                }
            }
            DrawExplicitRootChips(profile);

            DrawFieldMappingRules(profile);
            DrawRelationMappingRules(profile);
            DrawGroupMappingRules(profile);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Save Profile", "Save integration profile settings."), GUILayout.Width(94f)))
                {
                    string error;
                    _status = PungentBoardIntegrationProfileStorage.Save(out error) ? "Saved board integration profiles." : "Profile save failed: " + error;
                }

                if (GUILayout.Button(new GUIContent("Reset Default", "Restore the default import profile in memory. Save Profile to persist."), GUILayout.Width(96f)))
                {
                    PungentBoardIntegrationProfile defaults = PungentBoardIntegrationProfile.CreateDefault();
                    profile.sourceLabel = defaults.sourceLabel;
                    profile.includedKinds = defaults.includedKinds;
                    profile.includedProviderIds = defaults.includedProviderIds;
                    profile.includedSourceTypeNames = defaults.includedSourceTypeNames;
                    profile.excludedSourceTypeNames = defaults.excludedSourceTypeNames;
                    profile.explicitRootPaths = defaults.explicitRootPaths;
                    profile.fieldMappings = defaults.fieldMappings;
                    profile.relationMappings = defaults.relationMappings;
                    profile.groupMappings = defaults.groupMappings;
                    profile.sourceScopes = defaults.sourceScopes;
                    profile.updateExistingNodes = defaults.updateExistingNodes;
                    profile.createMissingNodes = defaults.createMissingNodes;
                    profile.createProviderGroups = defaults.createProviderGroups;
                    profile.updateExistingEdges = defaults.updateExistingEdges;
                    profile.createMissingEdges = defaults.createMissingEdges;
                    profile.updateExistingGroups = defaults.updateExistingGroups;
                    profile.createMissingGroups = defaults.createMissingGroups;
                    profile.markStaleMissingSources = defaults.markStaleMissingSources;
                    profile.maxItemsPerRefresh = defaults.maxItemsPerRefresh;
                    profile.nodeSpacingX = defaults.nodeSpacingX;
                    profile.nodeSpacingY = defaults.nodeSpacingY;
                    _projectionPreview = null;
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawExplicitRootChips(PungentBoardIntegrationProfile profile)
        {
            if (profile == null || profile.explicitRootPaths == null || profile.explicitRootPaths.Count == 0)
                return;

            EditorGUILayout.LabelField("Explicit Roots", EditorStyles.miniLabel);
            for (int i = 0; i < profile.explicitRootPaths.Count; i++)
            {
                string root = profile.explicitRootPaths[i];
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(root, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(root);
                    using (new EditorGUI.DisabledScope(asset == null))
                    {
                        if (GUILayout.Button(new GUIContent("Ping", "Ping this explicit root in the Project window."), GUILayout.Width(44f)))
                            EditorGUIUtility.PingObject(asset);
                    }
                    if (GUILayout.Button(new GUIContent("Remove", "Remove this explicit root from the profile. No assets are changed."), GUILayout.Width(62f)))
                    {
                        profile.explicitRootPaths.RemoveAt(i);
                        profile.NormalizeInPlace();
                        _projectionPreview = null;
                        i--;
                    }
                }
            }
        }

        private void DrawFieldMappingRules(PungentBoardIntegrationProfile profile)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(new GUIContent("Field Mappings", "Map source values or SerializedProperty paths to board node fields/properties."), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Examples: component.type -> Tag, gameObject.name -> Title, property:m_Name -> Summary, or a picked SerializedProperty path -> Node Property. Mapping edits do not scan; Preview Import is the explicit refresh point.", MessageType.None);
            profile.fieldMappings = profile.fieldMappings ?? new List<PungentBoardFieldMappingRule>();
            for (int i = 0; i < profile.fieldMappings.Count; i++)
            {
                PungentBoardFieldMappingRule rule = profile.fieldMappings[i];
                if (rule == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        rule.enabled = EditorGUILayout.Toggle(rule.enabled, GUILayout.Width(18f));
                        rule.target = (PungentBoardFieldMappingTarget)EditorGUILayout.EnumPopup(rule.target, GUILayout.Width(112f));
                        rule.sourcePath = EditorGUILayout.TextField(new GUIContent("Source", "Examples: component.type, gameObject.name, property path such as m_Script, or property:path."), rule.sourcePath);
                        if (GUILayout.Button("X", GUILayout.Width(24f)))
                        {
                            profile.fieldMappings.RemoveAt(i);
                            i--;
                            _projectionPreview = null;
                            continue;
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        rule.targetKey = EditorGUILayout.TextField(new GUIContent("Key", "Used for NodeProperty or as a hint for tags."), rule.targetKey);
                        rule.fallbackValue = EditorGUILayout.TextField(new GUIContent("Fallback", "Used when the source path is empty or missing."), rule.fallbackValue);
                    }

                    DrawFieldMappingEndpointPicker(profile, rule, i);
                }
            }

            if (GUILayout.Button(new GUIContent("Add Field Mapping", "Add a mapping row for source values to node fields or properties."), GUILayout.Height(20f)))
            {
                profile.fieldMappings.Add(new PungentBoardFieldMappingRule { sourcePath = "component.type", target = PungentBoardFieldMappingTarget.NodeProperty, targetKey = "componentType" });
                _projectionPreview = null;
            }
        }

        private void DrawFieldMappingEndpointPicker(PungentBoardIntegrationProfile profile, PungentBoardFieldMappingRule rule, int index)
        {
            if (profile == null || rule == null)
                return;

            string key = profile.id + ":field:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!DrawProfileBindingPickerFoldout(key, "Endpoint Picker"))
                return;

            EditorGUILayout.HelpBox("Pick an explicit sample object/component endpoint to fill the mapping source path. The profile stores only the property path, not this sample object.", MessageType.None);
            PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
            {
                contextLabel = "Mapping Endpoint",
                objectLabel = "Sample",
                componentLabel = "Component",
                endpointLabel = "Field",
                bindButtonLabel = "Use Property Path",
                helpText = "Pick a sample endpoint to fill this mapping path. The board profile stores the path, not this sample object.",
                showBindButton = false
            };
            PungentAuthoringGuidedBindingResult result = PungentAuthoringGuidedBindingView.Draw(GetProfileBindingPickerState(key), options);
            PungentAuthoringBindingEndpoint endpoint = result.selectedEndpoint;
            bool canUse = endpoint != null && endpoint.target != null && !string.IsNullOrWhiteSpace(endpoint.target.propertyPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canUse))
                {
                    if (canUse && (result.bindClicked || GUILayout.Button(new GUIContent("Use Property Path", canUse ? "Fill Source from the selected endpoint property path." : "Select a serialized property endpoint first."), GUILayout.Width(126f))))
                    {
                        rule.sourcePath = endpoint.target.propertyPath;
                        if (rule.target == PungentBoardFieldMappingTarget.NodeProperty && string.IsNullOrWhiteSpace(rule.targetKey))
                            rule.targetKey = SuggestPropertyKey(endpoint.target.propertyPath);
                        rule.NormalizeInPlace();
                        _projectionPreview = null;
                    }
                }

                if (endpoint != null)
                    GUILayout.Label(endpoint.valueKind.ToString(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRelationMappingRules(PungentBoardIntegrationProfile profile)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(new GUIContent("Relation Mappings", "Map SerializedObject object-reference fields into board edges."), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Relation rows turn serialized object-reference fields into edges only during Preview Import. Use the object-reference picker to fill a friendly path filter instead of typing long property paths by hand.", MessageType.None);
            profile.relationMappings = profile.relationMappings ?? new List<PungentBoardRelationMappingRule>();
            for (int i = 0; i < profile.relationMappings.Count; i++)
            {
                PungentBoardRelationMappingRule rule = profile.relationMappings[i];
                if (rule == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        rule.enabled = EditorGUILayout.Toggle(rule.enabled, GUILayout.Width(18f));
                        rule.includeObjectReferenceFields = EditorGUILayout.ToggleLeft(new GUIContent("Object refs", "Use SerializedProperty object-reference fields."), rule.includeObjectReferenceFields, GUILayout.Width(92f));
                        rule.onlyWhenTargetIncluded = EditorGUILayout.ToggleLeft(new GUIContent("Included only", "Only make edges when the target is also included in the preview or already on the board."), rule.onlyWhenTargetIncluded, GUILayout.Width(104f));
                        if (GUILayout.Button("X", GUILayout.Width(24f)))
                        {
                            profile.relationMappings.RemoveAt(i);
                            i--;
                            _projectionPreview = null;
                            continue;
                        }
                    }

                    rule.sourcePropertyPathContains = EditorGUILayout.TextField(new GUIContent("Path Contains", "Optional SerializedProperty path/display-name filter."), rule.sourcePropertyPathContains);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        rule.edgeTypeKey = EditorGUILayout.TextField(new GUIContent("Edge Type", "Optional board edge rule key. Empty uses the graph default."), rule.edgeTypeKey);
                        rule.labelPrefix = EditorGUILayout.TextField(new GUIContent("Label Prefix", "Optional edge label prefix."), rule.labelPrefix);
                    }

                    DrawRelationMappingEndpointPicker(profile, rule, i);
                }
            }

            if (GUILayout.Button(new GUIContent("Add Relation Mapping", "Add a relation mapping row for object references."), GUILayout.Height(20f)))
            {
                profile.relationMappings.Add(new PungentBoardRelationMappingRule());
                _projectionPreview = null;
            }
        }

        private void DrawRelationMappingEndpointPicker(PungentBoardIntegrationProfile profile, PungentBoardRelationMappingRule rule, int index)
        {
            if (profile == null || rule == null)
                return;

            string key = profile.id + ":relation:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!DrawProfileBindingPickerFoldout(key, "Object Reference Picker"))
                return;

            EditorGUILayout.HelpBox("Pick an object-reference endpoint from a sample component to fill the relation path filter. Import still runs only from Preview Import.", MessageType.None);
            PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
            {
                contextLabel = "Relation Endpoint",
                objectLabel = "Sample",
                componentLabel = "Component",
                endpointLabel = "Object Reference Field",
                bindButtonLabel = "Use Object Ref Path",
                helpText = "Pick an object-reference endpoint from a sample component. Import still runs only from Preview Import.",
                showBindButton = false,
                allowedValueKinds = new List<PungentAuthoringBindingValueKind>
                {
                    PungentAuthoringBindingValueKind.ObjectReference,
                    PungentAuthoringBindingValueKind.UnityObject,
                    PungentAuthoringBindingValueKind.AssetReference,
                    PungentAuthoringBindingValueKind.SceneObjectReference,
                    PungentAuthoringBindingValueKind.ComponentReference,
                    PungentAuthoringBindingValueKind.Sprite,
                    PungentAuthoringBindingValueKind.AudioClip
                }
            };
            PungentAuthoringGuidedBindingResult result = PungentAuthoringGuidedBindingView.Draw(GetProfileBindingPickerState(key), options);
            PungentAuthoringBindingEndpoint endpoint = result.selectedEndpoint;
            bool canUse = endpoint != null &&
                          PungentAuthoringBindingValueCodec.IsUnityObjectFacing(PungentAuthoringBindingDiscoveryService.ToRuntimeValueType(endpoint)) &&
                          endpoint.target != null &&
                          !string.IsNullOrWhiteSpace(endpoint.target.propertyPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canUse))
                {
                    if (canUse && (result.bindClicked || GUILayout.Button(new GUIContent("Use Object Ref Path", canUse ? "Fill Path Contains from this object-reference property path." : "Select an object-reference endpoint first."), GUILayout.Width(136f))))
                    {
                        rule.includeObjectReferenceFields = true;
                        rule.sourcePropertyPathContains = endpoint.target.propertyPath;
                        rule.NormalizeInPlace();
                        _projectionPreview = null;
                    }
                }

                if (endpoint != null && !PungentAuthoringBindingValueCodec.IsUnityObjectFacing(PungentAuthoringBindingDiscoveryService.ToRuntimeValueType(endpoint)))
                    GUILayout.Label("Select an ObjectReference endpoint for relation edges.", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private bool DrawProfileBindingPickerFoldout(string key, string label)
        {
            bool open;
            _profileBindingPickerFoldouts.TryGetValue(key, out open);
            open = EditorGUILayout.Foldout(open, label, true);
            _profileBindingPickerFoldouts[key] = open;
            return open;
        }

        private PungentAuthoringGuidedBindingState GetProfileBindingPickerState(string key)
        {
            // RDE/GUIDED-BINDING MIGRATION NOTE: BoardGraph profiles keep their own mapping data; only the endpoint selection UI is shared.
            PungentAuthoringGuidedBindingState state;
            if (!_profileBindingPickerStates.TryGetValue(key, out state) || state == null)
            {
                state = new PungentAuthoringGuidedBindingState
                {
                    targetObject = Selection.activeObject
                };
                _profileBindingPickerStates[key] = state;
            }

            return state;
        }

        private static string SuggestPropertyKey(string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
                return "value";

            string[] parts = propertyPath.Split('.');
            string key = parts.Length == 0 ? propertyPath : parts[parts.Length - 1];
            key = key.Replace("m_", string.Empty).Replace(" ", string.Empty).Trim();
            return string.IsNullOrWhiteSpace(key) ? "value" : char.ToLowerInvariant(key[0]) + key.Substring(1);
        }

        private void DrawGroupMappingRules(PungentBoardIntegrationProfile profile)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(new GUIContent("Group Mappings", "Create placemat groups from imported source metadata."), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Groups are optional placemats for imported nodes. Common starts: group by GameObject for component maps, provider for authoring imports, or folder for explicit asset roots.", MessageType.None);
            profile.groupMappings = profile.groupMappings ?? new List<PungentBoardGroupMappingRule>();
            for (int i = 0; i < profile.groupMappings.Count; i++)
            {
                PungentBoardGroupMappingRule rule = profile.groupMappings[i];
                if (rule == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    rule.enabled = EditorGUILayout.Toggle(rule.enabled, GUILayout.Width(18f));
                    rule.groupBy = (PungentBoardGroupBy)EditorGUILayout.EnumPopup(rule.groupBy, GUILayout.Width(112f));
                    rule.titlePrefix = EditorGUILayout.TextField(rule.titlePrefix);
                    rule.styleKey = EditorGUILayout.TextField(rule.styleKey, GUILayout.Width(72f));
                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        profile.groupMappings.RemoveAt(i);
                        i--;
                        _projectionPreview = null;
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("Add Group Mapping", "Add a group mapping row."), GUILayout.Height(20f)))
            {
                profile.groupMappings.Add(new PungentBoardGroupMappingRule { enabled = true, groupBy = PungentBoardGroupBy.GameObject, titlePrefix = "GameObject", styleKey = "group" });
                _projectionPreview = null;
            }
        }

        private void AddSelectedAssetRoots(PungentBoardIntegrationProfile profile)
        {
            profile.explicitRootPaths = profile.explicitRootPaths ?? new List<string>();
            foreach (UnityEngine.Object selected in Selection.objects ?? new UnityEngine.Object[0])
            {
                if (selected == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrWhiteSpace(path) && !profile.explicitRootPaths.Any(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase)))
                    profile.explicitRootPaths.Add(path);
            }

            profile.NormalizeInPlace();
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(string.IsNullOrWhiteSpace(_drawError) ? StatusMessageWithHints() : "Draw error: " + _drawError, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(ValidationFooter(), EditorStyles.miniLabel, GUILayout.Width(210f));
                GUILayout.Label(PungentBoardEditorStorage.StoragePath, EditorStyles.miniLabel);
            }
        }

        private string StatusMessageWithHints()
        {
            if (_activeCanvasOverlay != PungentBoardCanvasOverlayKind.None)
                return PungentBoardGraphCommandRouter.Label(_activeCanvasOverlay == PungentBoardCanvasOverlayKind.Grid
                    ? PungentBoardGraphCommand.OpenGridOverlay
                    : _activeCanvasOverlay == PungentBoardCanvasOverlayKind.Snap
                        ? PungentBoardGraphCommand.OpenSnapOverlay
                        : PungentBoardGraphCommand.OpenShortcutOverlay) + " open. Esc closes overlays.";
            if (_canvasState.inlineEdit.field != PungentBoardInlineNodeEditField.None)
                return "Inline edit active. Press OK or Enter to commit title, Ctrl/Cmd+Enter for body, Esc to cancel.";
            if (_canvasState.connectorMode)
                return "Connector mode active. Drag from output ports to input ports, or press Esc to cancel.";
            if (_canvasState.marqueeSelecting)
                return "Marquee selecting. Release to select content.";
            if (_canvasState.snapToGrid)
                return _status + " Snap " + Mathf.RoundToInt(_canvasState.snapSize) + " active.";
            return _status;
        }

        private void CreateBoard()
        {
            PungentBoardDocument board = PungentBoardEditorStorage.CreateBoard(_selectedTemplate);
            board.integrationProfileId = _selectedProfileId;
            SelectBoard(board.id);
            EnsureSelectedNodeTypeForCurrentBoard();
            EnsureSelectedEdgeTypeForCurrentBoard();
            _history.Reset(board);
            MarkContentDirty("Created " + PungentBoardTemplates.GetDisplayName(_selectedTemplate) + ".");
            UtilityWindowPrefs.SetInt(PrefTemplate, (int)_selectedTemplate);
        }

        private void DuplicateCurrentBoard()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            PersistViewState();
            PungentBoardDocument copy = JsonUtility.FromJson<PungentBoardDocument>(JsonUtility.ToJson(board));
            copy.id = PungentAuthoringId.NewValue();
            copy.title = (string.IsNullOrWhiteSpace(board.title) ? "Untitled Board" : board.title.Trim()) + " Copy";
            string now = DateTime.UtcNow.ToString("o");
            copy.createdUtc = now;
            copy.updatedUtc = now;
            copy.NormalizeInPlace();
            PungentBoardEditorStorage.Database.UpsertDocument(copy);
            SelectBoard(copy.id);
            _history.Reset(copy);
            MarkContentDirty("Duplicated board.");
            SaveNow("Saved duplicated board.");
        }

        private void FocusBoardTitleInInspector()
        {
            BeginBoardTitleRename();
        }

        private void BeginBoardTitleRename()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            _renameBoardTitle = string.IsNullOrWhiteSpace(board.title) ? "Untitled Board" : board.title;
            _renamingBoardTitle = true;
            _status = "Renaming board title. Press Enter to commit or Escape to cancel.";
            Repaint();
        }

        private void CommitBoardTitleRename()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
            {
                CancelBoardTitleRename();
                return;
            }

            string nextTitle = string.IsNullOrWhiteSpace(_renameBoardTitle) ? "Untitled Board" : _renameBoardTitle.Trim();
            if (!string.Equals(board.title, nextTitle, StringComparison.Ordinal))
            {
                _history.RecordBefore(board, "Rename Board");
                board.title = nextTitle;
                board.updatedUtc = DateTime.UtcNow.ToString("o");
                MarkContentDirty("Renamed board.");
            }

            _renamingBoardTitle = false;
            GUI.FocusControl(null);
            Repaint();
        }

        private void CancelBoardTitleRename()
        {
            _renamingBoardTitle = false;
            _renameBoardTitle = string.Empty;
            GUI.FocusControl(null);
            _status = "Cancelled board rename.";
            Repaint();
        }

        private void HandleBoardTitleRenameKeys()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
                return;

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitBoardTitleRename();
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                CancelBoardTitleRename();
                evt.Use();
            }
        }

        private void DeleteCurrentBoardWithConfirmation()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            if (!EditorUtility.DisplayDialog("Delete Board", "Delete board '" + board.title + "' from board storage?", "Delete", "Cancel"))
                return;

            PungentBoardEditorStorage.DeleteBoard(board.id);
            _selectedBoardId = string.Empty;
            UtilityWindowPrefs.SetString(PrefOpenBoardId, string.Empty);
            _canvasState.ClearSelection();
            _history.Reset(null);
            _renamingBoardTitle = false;
            LoadViewStateFromPrefsOrBoard();
            MarkContentDirty("Deleted board.");
            SaveNow("Deleted board.");
        }

        private void AddNodeAtCanvasCenter()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            Vector2 center = CanvasCenterPoint();
            PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId);
            PungentBoardNodeTypeDefinition nodeType = graphType != null ? graphType.FindNodeType(_selectedNodeTypeKey) ?? graphType.GetDefaultNodeType() : null;
            if (nodeType != null)
                PungentBoardCanvasGUI.AddNodeAt(board, _canvasState, nodeType, center);
            else
                PungentBoardCanvasGUI.AddNodeAt(board, _canvasState, _selectedNodeKind, center);
        }

        private void AddParameterReferenceNodeAtCanvasCenter(string label, string typeKey, PungentBoardNodeKind kind, string styleKey)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            PungentBoardNode node = PungentBoardCanvasGUI.AddNodeAt(board, _canvasState, kind, CanvasCenterPoint());
            if (node == null)
                return;

            node.title = label;
            node.nodeTypeKey = typeKey;
            node.colorStyleKey = styleKey;
            node.summary = "Parameter/reference node";
            node.body = "Use Project Binding or Authoring Link to connect this placeholder to project data.";
            node.NormalizeInPlace();
        }

        private void AddCustomGroupNodeAtCanvasCenter(PungentBoardCustomNodeTemplate template)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || template == null)
                return;

            AddCustomGroupNodeAtCanvasPoint(template, CanvasCenterPoint());
        }

        private void AddCustomGroupNodeAtCanvasPoint(string templateId, Vector2 canvasPoint)
        {
            PungentBoardCustomNodeTemplate template = PungentBoardCustomNodeTemplateStorage.Database.Find(templateId);
            if (template == null)
            {
                _status = "Custom node template is unavailable.";
                Repaint();
                return;
            }

            AddCustomGroupNodeAtCanvasPoint(template, canvasPoint);
        }

        private void AddCustomGroupNodeAtCanvasPoint(PungentBoardCustomNodeTemplate template, Vector2 canvasPoint)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || template == null)
                return;

            PungentBoardGroup group;
            string message;
            if (PungentBoardCustomNodeTemplateStorage.InstantiateTemplate(board, _canvasState, template, canvasPoint, out group, out message))
            {
                _status = message;
                _validationResult = null;
                _projectionPreview = null;
                Repaint();
            }
            else
            {
                _status = "Custom node add failed: " + message;
            }
        }

        private void SaveGroupAsTemplateFromCanvas(PungentBoardGroup group)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || group == null)
                return;

            _history.RecordBefore(board, "Save Custom Group Template");
            PungentBoardCustomNodeTemplate template;
            string message;
            if (PungentBoardCustomNodeTemplateStorage.SaveGroupAsTemplate(board, group, out template, out message))
            {
                group.customNodeEnabled = true;
                group.customNodeSourceTemplateId = template.id;
                group.collapsedAsNode = true;
                _canvasState.SelectGroup(group.id);
                MarkContentDirty("Saved custom group template.");
                _status = message;
            }
            else
            {
                _status = "Custom group template save failed: " + message;
            }

            Repaint();
        }

        private Vector2 CanvasCenterPoint()
        {
            return _lastCanvasRect.width > 1f && _lastCanvasRect.height > 1f
                ? PungentBoardCanvasGUI.ScreenToCanvas(_lastCanvasRect.center, _lastCanvasRect, _canvasState)
                : Vector2.zero;
        }

        private void AddGroupAtCanvasCenter()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            Vector2 center = CanvasCenterPoint();
            PungentBoardCanvasGUI.AddGroupAt(board, _canvasState, center);
        }

        private void DuplicateSelectedNode()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || !_canvasState.HasSelection)
                return;

            PungentBoardCanvasGUI.DuplicateSelection(board, _canvasState, new Vector2(32f, 32f));
        }

        private void ScheduleDeleteSelectedWithConfirmation()
        {
            if (_deleteConfirmationScheduled)
                return;

            _deleteConfirmationScheduled = true;
            EditorApplication.delayCall -= RunScheduledDeleteConfirmation;
            EditorApplication.delayCall += RunScheduledDeleteConfirmation;
        }

        private void RunScheduledDeleteConfirmation()
        {
            _deleteConfirmationScheduled = false;
            if (this == null)
                return;

            DeleteSelectedWithConfirmation();
            Repaint();
        }

        private void DeleteSelectedWithConfirmation()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            if (_canvasState.MultiSelectionCount > 1)
            {
                if (EditorUtility.DisplayDialog("Delete Selection", "Delete selected nodes/groups? Connected edges for deleted nodes are removed. Group deletion keeps nodes on the board.", "Delete", "Cancel"))
                {
                    _history.RecordBefore(board, "Delete Selection");
                    HashSet<string> nodeIds = new HashSet<string>(_canvasState.selectedNodeIds, StringComparer.OrdinalIgnoreCase);
                    HashSet<string> groupIds = new HashSet<string>(_canvasState.selectedGroupIds, StringComparer.OrdinalIgnoreCase);
                    board.nodes.RemoveAll(node => node != null && nodeIds.Contains(node.id));
                    board.edges.RemoveAll(edge => edge != null && (nodeIds.Contains(edge.fromNodeId) || nodeIds.Contains(edge.toNodeId)));
                    board.groups.RemoveAll(group => group != null && groupIds.Contains(group.id));
                    _canvasState.ClearSelection();
                    MarkContentDirty("Deleted selection.");
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(_canvasState.selectedNodeId))
            {
                PungentBoardNode node = PungentBoardCanvasGUI.FindNode(board, _canvasState.selectedNodeId);
                if (node != null && EditorUtility.DisplayDialog("Delete Node", "Delete node '" + node.title + "' and its connected edges?", "Delete", "Cancel"))
                {
                    _history.RecordBefore(board, "Delete Node");
                    board.nodes.Remove(node);
                    board.edges.RemoveAll(edge => edge != null && (PungentAuthoringId.EqualsId(edge.fromNodeId, node.id) || PungentAuthoringId.EqualsId(edge.toNodeId, node.id)));
                    _canvasState.ClearSelection();
                    MarkContentDirty("Deleted node.");
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(_canvasState.selectedEdgeId))
            {
                PungentBoardEdge edge = PungentBoardCanvasGUI.FindEdge(board, _canvasState.selectedEdgeId);
                if (edge != null && EditorUtility.DisplayDialog("Delete Edge", "Delete selected edge?", "Delete", "Cancel"))
                {
                    _history.RecordBefore(board, "Delete Edge");
                    board.edges.Remove(edge);
                    _canvasState.ClearSelection();
                    MarkContentDirty("Deleted edge.");
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(_canvasState.selectedGroupId))
            {
                PungentBoardGroup group = PungentBoardCanvasGUI.FindGroup(board, _canvasState.selectedGroupId);
                if (group != null && EditorUtility.DisplayDialog("Delete Group", "Delete group '" + group.title + "'? Nodes remain on the board.", "Delete", "Cancel"))
                {
                    _history.RecordBefore(board, "Delete Group");
                    board.groups.Remove(group);
                    _canvasState.ClearSelection();
                    MarkContentDirty("Deleted group.");
                }
                return;
            }

            if (EditorUtility.DisplayDialog("Delete Board", "Delete board '" + board.title + "' from board storage?", "Delete", "Cancel"))
            {
                PungentBoardEditorStorage.DeleteBoard(board.id);
                _selectedBoardId = string.Empty;
                UtilityWindowPrefs.SetString(PrefOpenBoardId, string.Empty);
                _canvasState.ClearSelection();
                _history.Reset(null);
                _renamingBoardTitle = false;
                LoadViewStateFromPrefsOrBoard();
                MarkContentDirty("Deleted board.");
                SaveNow("Deleted board.");
            }
        }

        private void ValidateCurrentBoard()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            _validationResult = PungentBoardLocalValidation.Validate(board);
            _status = "Board validation: " + _validationResult.status + " (" + (_validationResult.issues?.Count ?? 0) + " issue(s)).";
            Repaint();
        }

        private void SelectBoard(string boardId)
        {
            if (PungentAuthoringId.EqualsId(_selectedBoardId, boardId))
                return;

            PersistViewState();
            _selectedBoardId = PungentAuthoringId.Normalize(boardId);
            UtilityWindowPrefs.SetString(PrefOpenBoardId, _selectedBoardId);
            LoadViewStateFromPrefsOrBoard();
            EnsureSelectedNodeTypeForCurrentBoard();
            EnsureSelectedEdgeTypeForCurrentBoard();
            _history.Reset(CurrentBoard);
            _validationResult = null;
            _projectionPreview = null;
            _status = string.IsNullOrWhiteSpace(_selectedBoardId) ? "No board selected." : "Opened board.";
            Repaint();
        }

        private void LoadViewStateFromPrefsOrBoard()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board != null && board.canvasState != null)
            {
                _canvasState.pan = board.canvasState.pan;
                _canvasState.zoom = board.canvasState.zoom;
                _canvasState.selectedNodeId = board.canvasState.selectedNodeId;
                _canvasState.selectedEdgeId = board.canvasState.selectedEdgeId;
                _canvasState.selectedGroupId = board.canvasState.selectedGroupId;
                _canvasState.showGrid = board.canvasState.showGrid;
                _canvasState.snapToGrid = board.canvasState.snapToGrid;
                _canvasState.snapSize = board.canvasState.snapSize;
                _canvasState.gridMinorSpacing = board.canvasState.gridMinorSpacing;
                _canvasState.gridMajorLineFrequency = board.canvasState.gridMajorLineFrequency;
                _canvasState.gridOpacity = board.canvasState.gridOpacity;
                _canvasState.snapNodes = board.canvasState.snapNodes;
                _canvasState.snapGroups = board.canvasState.snapGroups;
                _canvasState.snapResize = board.canvasState.snapResize;
            }

            if (PungentAuthoringId.EqualsId(UtilityWindowPrefs.GetString(PrefOpenBoardId, string.Empty), _selectedBoardId))
            {
                _canvasState.pan = new Vector2(UtilityWindowPrefs.GetFloat(PrefPanX, _canvasState.pan.x), UtilityWindowPrefs.GetFloat(PrefPanY, _canvasState.pan.y));
                _canvasState.zoom = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefZoom, _canvasState.zoom), 0.10f, 2.4f);
                _canvasState.selectedNodeId = UtilityWindowPrefs.GetString(PrefSelectedNode, _canvasState.selectedNodeId);
                _canvasState.selectedEdgeId = UtilityWindowPrefs.GetString(PrefSelectedEdge, _canvasState.selectedEdgeId);
                _canvasState.selectedGroupId = UtilityWindowPrefs.GetString(PrefSelectedGroup, _canvasState.selectedGroupId);
                _canvasState.showGrid = UtilityWindowPrefs.GetBool(PrefShowGrid, _canvasState.showGrid);
                _canvasState.snapToGrid = UtilityWindowPrefs.GetBool(PrefSnapToGrid, _canvasState.snapToGrid);
                _canvasState.snapSize = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefSnapSize, _canvasState.snapSize), 4f, 240f);
                _canvasState.gridMinorSpacing = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefGridMinorSpacing, _canvasState.gridMinorSpacing), 8f, 240f);
                _canvasState.gridMajorLineFrequency = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefGridMajorLineFrequency, _canvasState.gridMajorLineFrequency), 2, 12);
                _canvasState.gridOpacity = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefGridOpacity, _canvasState.gridOpacity));
                _canvasState.snapNodes = UtilityWindowPrefs.GetBool(PrefSnapNodes, _canvasState.snapNodes);
                _canvasState.snapGroups = UtilityWindowPrefs.GetBool(PrefSnapGroups, _canvasState.snapGroups);
                _canvasState.snapResize = UtilityWindowPrefs.GetBool(PrefSnapResize, _canvasState.snapResize);
            }

            _canvasState.connectorMode = false;
            _canvasState.connectorSourceNodeId = string.Empty;
            _canvasState.connectorSourcePortKey = string.Empty;
            _canvasState.connectorHoverNodeId = string.Empty;
            _canvasState.connectorHoverPortKey = string.Empty;
            _canvasState.connectorHoverValid = false;
            _canvasState.connectionWarning = string.Empty;
            _canvasState.selectedNodeIds.Clear();
            _canvasState.selectedGroupIds.Clear();
            if (!string.IsNullOrWhiteSpace(_canvasState.selectedNodeId))
                _canvasState.selectedNodeIds.Add(_canvasState.selectedNodeId);
            if (!string.IsNullOrWhiteSpace(_canvasState.selectedGroupId))
                _canvasState.selectedGroupIds.Add(_canvasState.selectedGroupId);
        }

        private void EnsureSelectedNodeTypeForCurrentBoard()
        {
            PungentBoardDocument board = CurrentBoard;
            List<PungentBoardNodeTypeDefinition> nodeTypes = PungentBoardCanvasGUI.GetPaletteNodeTypes(board);
            if (nodeTypes.Count == 0)
                return;

            PungentBoardNodeTypeDefinition selected = nodeTypes.FirstOrDefault(type => type != null && string.Equals(type.typeKey, _selectedNodeTypeKey, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                PungentBoardGraphTypeDefinition graphType = board != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId) : null;
                selected = graphType != null ? graphType.GetDefaultNodeType() : null;
                if (selected == null)
                    selected = nodeTypes[0];
            }

            _selectedNodeTypeKey = selected.typeKey;
            _selectedNodeKind = selected.nodeKind;
            _canvasState.defaultNodeTypeKey = _selectedNodeTypeKey;
            _canvasState.defaultNodeKind = _selectedNodeKind;
            UtilityWindowPrefs.SetString(PrefNodeTypeKey, _selectedNodeTypeKey);
            UtilityWindowPrefs.SetInt(PrefNodeKind, (int)_selectedNodeKind);
        }

        private void EnsureSelectedEdgeTypeForCurrentBoard()
        {
            PungentBoardDocument board = CurrentBoard;
            PungentBoardGraphTypeDefinition graphType = board != null ? PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId) : null;
            List<PungentBoardEdgeRuleDefinition> rules = graphType != null
                ? (graphType.edgeRules ?? new List<PungentBoardEdgeRuleDefinition>()).Where(rule => rule != null).ToList()
                : new List<PungentBoardEdgeRuleDefinition>();
            if (rules.Count == 0)
            {
                _selectedEdgeTypeKey = string.Empty;
                _canvasState.defaultEdgeTypeKey = string.Empty;
                UtilityWindowPrefs.SetString(PrefEdgeTypeKey, string.Empty);
                return;
            }

            PungentBoardEdgeRuleDefinition selected = rules.FirstOrDefault(rule => string.Equals(rule.typeKey, _selectedEdgeTypeKey, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
                selected = graphType != null ? graphType.FindEdgeRule(graphType.defaultEdgeTypeKey) : null;
            if (selected == null)
                selected = rules[0];

            _selectedEdgeTypeKey = selected.typeKey;
            _canvasState.defaultEdgeTypeKey = _selectedEdgeTypeKey;
            UtilityWindowPrefs.SetString(PrefEdgeTypeKey, _selectedEdgeTypeKey);
        }

        private void PersistViewState()
        {
            UtilityWindowPrefs.SetString(PrefOpenBoardId, _selectedBoardId);
            UtilityWindowPrefs.SetFloat(PrefPanX, _canvasState.pan.x);
            UtilityWindowPrefs.SetFloat(PrefPanY, _canvasState.pan.y);
            UtilityWindowPrefs.SetFloat(PrefZoom, _canvasState.zoom);
            UtilityWindowPrefs.SetString(PrefSelectedNode, _canvasState.selectedNodeId);
            UtilityWindowPrefs.SetString(PrefSelectedEdge, _canvasState.selectedEdgeId);
            UtilityWindowPrefs.SetString(PrefSelectedGroup, _canvasState.selectedGroupId);
            UtilityWindowPrefs.SetBool(PrefShowGrid, _canvasState.showGrid);
            UtilityWindowPrefs.SetBool(PrefSnapToGrid, _canvasState.snapToGrid);
            UtilityWindowPrefs.SetFloat(PrefSnapSize, Mathf.Clamp(_canvasState.snapSize, 4f, 240f));
            UtilityWindowPrefs.SetFloat(PrefGridMinorSpacing, Mathf.Clamp(_canvasState.gridMinorSpacing, 8f, 240f));
            UtilityWindowPrefs.SetInt(PrefGridMajorLineFrequency, Mathf.Clamp(_canvasState.gridMajorLineFrequency, 2, 12));
            UtilityWindowPrefs.SetFloat(PrefGridOpacity, Mathf.Clamp01(_canvasState.gridOpacity));
            UtilityWindowPrefs.SetBool(PrefSnapNodes, _canvasState.snapNodes);
            UtilityWindowPrefs.SetBool(PrefSnapGroups, _canvasState.snapGroups);
            UtilityWindowPrefs.SetBool(PrefSnapResize, _canvasState.snapResize);
            UtilityWindowPrefs.SetString(PrefNodeTypeKey, _selectedNodeTypeKey ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefEdgeTypeKey, _selectedEdgeTypeKey ?? string.Empty);

            PungentBoardDocument board = CurrentBoard;
            if (board != null)
            {
                board.canvasState = board.canvasState ?? new PungentBoardCanvasState();
                board.canvasState.pan = _canvasState.pan;
                board.canvasState.zoom = _canvasState.zoom;
                board.canvasState.selectedNodeId = _canvasState.selectedNodeId;
                board.canvasState.selectedEdgeId = _canvasState.selectedEdgeId;
                board.canvasState.selectedGroupId = _canvasState.selectedGroupId;
                board.canvasState.showGrid = _canvasState.showGrid;
                board.canvasState.snapToGrid = _canvasState.snapToGrid;
                board.canvasState.snapSize = _canvasState.snapSize;
                board.canvasState.gridMinorSpacing = _canvasState.gridMinorSpacing;
                board.canvasState.gridMajorLineFrequency = _canvasState.gridMajorLineFrequency;
                board.canvasState.gridOpacity = _canvasState.gridOpacity;
                board.canvasState.snapNodes = _canvasState.snapNodes;
                board.canvasState.snapGroups = _canvasState.snapGroups;
                board.canvasState.snapResize = _canvasState.snapResize;
                board.canvasState.NormalizeInPlace();
            }
        }

        private void PersistPanelState()
        {
            UtilityWindowPrefs.SetFloat(PrefLeftWidth, _leftWidth);
            UtilityWindowPrefs.SetFloat(PrefRightWidth, _rightWidth);
            UtilityWindowPrefs.SetString(PrefContentSearch, _contentSearch ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefNodePaletteSearch, _nodePaletteSearch ?? string.Empty);
            UtilityWindowPrefs.SetString(PrefIntegrationProfile, _selectedProfileId ?? string.Empty);
        }

        private void ClampPanelWidths(float availableWidth)
        {
            float maxLeft = Mathf.Min(MaxLeftWidth, Mathf.Max(MinLeftWidth, availableWidth - MinCanvasWidth - MinRightWidth - 18f));
            _leftWidth = Mathf.Clamp(_leftWidth, MinLeftWidth, maxLeft);
            float maxRight = Mathf.Min(MaxRightWidth, Mathf.Max(MinRightWidth, availableWidth - MinCanvasWidth - _leftWidth - 18f));
            _rightWidth = Mathf.Clamp(_rightWidth, MinRightWidth, maxRight);
        }

        private void MarkContentDirty(string status)
        {
            _contentDirty = true;
            _status = status + " Debounced save queued.";
            _nextSaveTime = EditorApplication.timeSinceStartup + DebouncedSaveSeconds;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            PersistViewState();
        }

        private void OnEditorUpdate()
        {
            if (!_contentDirty)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            if (EditorApplication.timeSinceStartup >= _nextSaveTime)
                SaveNow("Autosaved board.");
        }

        private void SaveNow(string successStatus)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board != null)
            {
                PersistViewState();
                PungentBoardCanvasGUI.UpdateGroupMemberships(board);
                board.NormalizeInPlace();
                PungentBoardEditorStorage.Database.Touch(board);
            }

            string error;
            if (PungentBoardEditorStorage.Save(out error))
            {
                _contentDirty = false;
                EditorApplication.update -= OnEditorUpdate;
                _status = successStatus;
            }
            else
            {
                _status = "Board save failed: " + error;
            }

            Repaint();
        }

        private void UndoBoard()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || !_history.Undo(board))
                return;

            PungentBoardCanvasGUI.UpdateGroupMemberships(board);
            _validationResult = null;
            _projectionPreview = null;
            MarkContentDirty("Undo.");
            Repaint();
        }

        private void RedoBoard()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || !_history.Redo(board))
                return;

            PungentBoardCanvasGUI.UpdateGroupMemberships(board);
            _validationResult = null;
            _projectionPreview = null;
            MarkContentDirty("Redo.");
            Repaint();
        }

        private void FrameSelection()
        {
            if (CurrentBoard == null)
                return;

            PungentBoardCanvasGUI.FrameSelection(CurrentCanvasRectOrFallback(), CurrentBoard, _canvasState);
            PersistViewState();
            Repaint();
        }

        private void FitCurrentBoard()
        {
            if (CurrentBoard == null)
                return;

            PungentBoardCanvasGUI.FitDocument(CurrentCanvasRectOrFallback(), CurrentBoard, _canvasState);
            PersistViewState();
            Repaint();
        }

        private Rect CurrentCanvasRectOrFallback()
        {
            if (_lastCanvasRect.width > 16f && _lastCanvasRect.height > 16f)
                return _lastCanvasRect;

            float toolbarHeight = ToolbarHeight;
            float bodyHeight = Mathf.Max(240f, position.height - toolbarHeight - StatusBarHeight);
            float canvasWidth = position.width < 760f
                ? Mathf.Max(MinCanvasWidth, position.width)
                : Mathf.Max(MinCanvasWidth, position.width - _leftWidth - _rightWidth - 24f);
            float canvasHeight = position.width < 760f
                ? Mathf.Max(220f, bodyHeight * 0.46f)
                : Mathf.Max(260f, bodyHeight - 24f);
            return new Rect(0f, 0f, canvasWidth, canvasHeight);
        }

        private void AlignSelection(bool horizontal)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            _history.RecordBefore(board, horizontal ? "Align Nodes Horizontally" : "Align Nodes Vertically");
            PungentBoardCanvasGUI.AlignSelection(board, _canvasState, horizontal);
            if (_canvasState.contentChanged)
            {
                _canvasState.ClearPendingHistory();
                _canvasState.contentChanged = false;
                MarkContentDirty(horizontal ? "Aligned selected nodes horizontally." : "Aligned selected nodes vertically.");
            }
        }

        private void DistributeSelection(bool horizontal)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null)
                return;

            _history.RecordBefore(board, horizontal ? "Distribute Nodes Horizontally" : "Distribute Nodes Vertically");
            PungentBoardCanvasGUI.DistributeSelection(board, _canvasState, horizontal);
            if (_canvasState.contentChanged)
            {
                _canvasState.contentChanged = false;
                _canvasState.ClearPendingHistory();
                MarkContentDirty(horizontal ? "Distributed selected nodes horizontally." : "Distributed selected nodes vertically.");
            }
        }

        private void PreviewImport()
        {
            PungentBoardDocument board = CurrentBoard;
            PungentBoardIntegrationProfile profile = PungentBoardIntegrationProfileStorage.Find(_selectedProfileId);
            if (board == null || profile == null)
                return;

            board.integrationProfileId = profile.id;
            Vector2 origin = _lastCanvasRect.width > 1f && _lastCanvasRect.height > 1f
                ? PungentBoardCanvasGUI.ScreenToCanvas(_lastCanvasRect.center, _lastCanvasRect, _canvasState)
                : Vector2.zero;
            PungentBoardProjectionRequest request = new PungentBoardProjectionRequest
            {
                document = board,
                profile = profile,
                origin = origin,
                selectionObjects = Selection.objects ?? new UnityEngine.Object[0],
                explicitRootPaths = profile.explicitRootPaths != null ? profile.explicitRootPaths.ToList() : new List<string>()
            };

            _projectionPreview = new PungentBoardProjectionPreview
            {
                adapterId = "aggregate",
                displayName = "Board Import Preview",
                generatedUtc = DateTime.UtcNow.ToString("o")
            };

            foreach (IPungentBoardProjectionAdapter adapter in PungentBoardProjectionRegistry.RegisteredAdapters)
            {
                string reason = string.Empty;
                if (adapter == null || !adapter.CanPreview(request, out reason))
                {
                    if (!string.IsNullOrWhiteSpace(reason))
                        _projectionPreview.messages.Add(reason);
                    continue;
                }

                PungentBoardProjectionPreview adapterPreview = adapter.BuildPreview(request);
                if (adapterPreview == null)
                    continue;
                _projectionPreview.messages.AddRange(adapterPreview.messages);
                _projectionPreview.changes.AddRange(adapterPreview.changes);
            }

            ApplySafeProjectionPreviewDefaults(profile);
            _status = "Previewed board import: " + (_projectionPreview.changes?.Count ?? 0) + " change(s).";
            Repaint();
        }

        private void ApplySafeProjectionPreviewDefaults(PungentBoardIntegrationProfile profile)
        {
            if (_projectionPreview == null || _projectionPreview.changes == null)
                return;

            int addNodeCount = _projectionPreview.changes.Count(change => change != null && change.changeKind == PungentBoardProjectionChangeKind.AddNode);
            bool broadAuthoring = profile != null &&
                                  profile.HasScope(PungentBoardProjectionSourceScope.AuthoringProviders) &&
                                  (profile.includedProviderIds == null || profile.includedProviderIds.Count == 0);
            bool broadSelection = profile != null &&
                                  (profile.HasScope(PungentBoardProjectionSourceScope.CurrentSelection) ||
                                   profile.HasScope(PungentBoardProjectionSourceScope.SelectedGameObjectsAndComponents)) &&
                                  (Selection.objects?.Length ?? 0) >= 20;

            if (addNodeCount >= 50 || broadAuthoring || broadSelection)
            {
                foreach (PungentBoardProjectionChange change in _projectionPreview.changes)
                {
                    if (change != null && change.changeKind == PungentBoardProjectionChangeKind.AddNode)
                        change.selected = false;
                }

                _projectionPreview.messages.Add("Safe default: add-node changes were deselected because this preview looks broad. Use Select Adds if you deliberately want to apply them.");
            }
        }

        private void ApplyProjectionPreview()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || _projectionPreview == null)
                return;

            _history.RecordBefore(board, "Apply Import Preview");
            int applied = PungentBoardProjectionApplier.Apply(board, _canvasState, _projectionPreview);
            _projectionPreview = null;
            if (applied > 0)
            {
                _canvasState.contentChanged = false;
                _canvasState.ClearPendingHistory();
                MarkContentDirty("Applied " + applied + " import change(s).");
                _validationResult = null;
            }
            else
            {
                _status = "No selected import changes to apply.";
            }
        }

        private void FocusValidationSource(string sourceId)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || string.IsNullOrWhiteSpace(sourceId))
                return;

            if (PungentBoardCanvasGUI.FindNode(board, sourceId) != null)
                _canvasState.SelectNode(sourceId);
            else if (PungentBoardCanvasGUI.FindEdge(board, sourceId) != null)
                _canvasState.SelectEdge(sourceId);
            else if (PungentBoardCanvasGUI.FindGroup(board, sourceId) != null)
                _canvasState.SelectGroup(sourceId);
            else
            {
                PungentBoardNode linked = (board.nodes ?? new List<PungentBoardNode>()).FirstOrDefault(node =>
                    node != null && node.linkedAuthoringRef != null && PungentAuthoringId.EqualsId(node.linkedAuthoringRef.itemId, sourceId));
                if (linked != null)
                    _canvasState.SelectNode(linked.id);
            }

            FrameSelection();
        }

        private static bool CanQuickFix(PungentAuthoringValidationIssue issue)
        {
            if (issue == null || string.IsNullOrWhiteSpace(issue.issueCode))
                return false;

            return string.Equals(issue.issueCode, "MISSING_EDGE_SOURCE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "MISSING_EDGE_TARGET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "MISSING_GROUP_NODE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "MISSING_NODE_ID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "MISSING_GROUP_ID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "DUPLICATE_NODE_ID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "DUPLICATE_GROUP_ID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "MISSING_NODE_TYPE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "MISSING_EDGE_RULE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "INVALID_PORT_CONNECTION", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, PungentBoardEventSequenceUtility.IssueDuplicateOrder, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, PungentBoardEventSequenceUtility.IssueInvalidDelay, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, PungentBoardEventSequenceUtility.IssueMissingStepValue, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, PungentBoardEventSequenceUtility.IssueBranchEdgeLabel, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_PORT_MISSING_KEY", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_DUPLICATE_PORT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_MAPPING_MISSING_PORT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_MAPPING_MISSING_NODE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_MAPPING_MISSING_TARGET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_MAPPING_MISSING_INTERNAL_PORT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_BINDING_SLOT_INCOMPLETE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(issue.issueCode, "CUSTOM_GROUP_REFERENCE_SLOT_MISSING_TARGET", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyValidationQuickFix(PungentAuthoringValidationIssue issue)
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || issue == null)
                return;

            _history.RecordBefore(board, "Validation Quick Fix");
            bool changed = false;
            string code = issue.issueCode ?? string.Empty;
            if (string.Equals(code, "MISSING_EDGE_SOURCE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, "MISSING_EDGE_TARGET", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardEdge edge = PungentBoardCanvasGUI.FindEdge(board, issue.sourcePathKeyOrId);
                if (edge != null)
                    changed = board.edges.Remove(edge);
            }
            else if (string.Equals(code, "MISSING_GROUP_NODE", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardCanvasGUI.UpdateGroupMemberships(board);
                changed = true;
            }
            else if (string.Equals(code, "MISSING_NODE_ID", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "DUPLICATE_NODE_ID", StringComparison.OrdinalIgnoreCase))
            {
                changed = RegenerateDuplicateOrMissingNodeId(board, issue.sourcePathKeyOrId);
            }
            else if (string.Equals(code, "MISSING_GROUP_ID", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "DUPLICATE_GROUP_ID", StringComparison.OrdinalIgnoreCase))
            {
                changed = RegenerateDuplicateOrMissingGroupId(board, issue.sourcePathKeyOrId);
            }
            else if (string.Equals(code, "MISSING_NODE_TYPE", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardNode node = PungentBoardCanvasGUI.FindNode(board, issue.sourcePathKeyOrId);
                if (node != null)
                {
                    PungentBoardGraphTypeDefinition graphType = PungentBoardGraphTypeRegistry.FindOrFreeform(board.graphTypeId);
                    PungentBoardNodeTypeDefinition defaultType = graphType != null ? graphType.GetDefaultNodeType() : null;
                    if (defaultType != null)
                    {
                        PungentBoardCanvasGUI.ApplyNodeTypeDefaults(board, node, defaultType.typeKey);
                        changed = true;
                    }
                }
            }
            else if (string.Equals(code, "MISSING_EDGE_RULE", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardEdge edge = PungentBoardCanvasGUI.FindEdge(board, issue.sourcePathKeyOrId);
                changed = PungentBoardCanvasGUI.ApplyBestEdgeRule(board, edge, _selectedEdgeTypeKey);
            }
            else if (string.Equals(code, "INVALID_PORT_CONNECTION", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardEdge edge = PungentBoardCanvasGUI.FindEdge(board, issue.sourcePathKeyOrId);
                string preferredRule = edge != null && !string.IsNullOrWhiteSpace(edge.edgeTypeKey) ? edge.edgeTypeKey : _selectedEdgeTypeKey;
                changed = PungentBoardCanvasGUI.ClearEdgePorts(edge);
                if (edge != null)
                    changed = PungentBoardCanvasGUI.ApplyBestEdgeRule(board, edge, preferredRule) || changed;
            }
            else if (string.Equals(code, PungentBoardEventSequenceUtility.IssueDuplicateOrder, StringComparison.OrdinalIgnoreCase))
            {
                changed = PungentBoardEventSequenceUtility.NormalizeOutgoingOrders(board, issue.sourcePathKeyOrId) > 0;
            }
            else if (string.Equals(code, PungentBoardEventSequenceUtility.IssueInvalidDelay, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, PungentBoardEventSequenceUtility.IssueMissingStepValue, StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardNode node = PungentBoardCanvasGUI.FindNode(board, issue.sourcePathKeyOrId);
                changed = PungentBoardEventSequenceUtility.ApplyDefaultStepValue(node);
            }
            else if (string.Equals(code, PungentBoardEventSequenceUtility.IssueBranchEdgeLabel, StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardEdge edge = PungentBoardCanvasGUI.FindEdge(board, issue.sourcePathKeyOrId);
                changed = PungentBoardEventSequenceUtility.ApplyDefaultBranchEdgeLabel(edge);
            }
            else if (string.Equals(code, "CUSTOM_GROUP_PORT_MISSING_KEY", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "CUSTOM_GROUP_DUPLICATE_PORT", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardGroup group = PungentBoardCanvasGUI.FindGroup(board, issue.sourcePathKeyOrId);
                changed = NormalizeCustomGroupPortKeys(group);
            }
            else if (string.Equals(code, "CUSTOM_GROUP_MAPPING_MISSING_PORT", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "CUSTOM_GROUP_MAPPING_MISSING_NODE", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "CUSTOM_GROUP_MAPPING_MISSING_TARGET", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "CUSTOM_GROUP_MAPPING_MISSING_INTERNAL_PORT", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardGroup group = PungentBoardCanvasGUI.FindGroup(board, issue.sourcePathKeyOrId);
                changed = RemoveInvalidCustomGroupMappings(board, group);
            }
            else if (string.Equals(code, "CUSTOM_GROUP_BINDING_SLOT_INCOMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardGroup group = PungentBoardCanvasGUI.FindGroup(board, issue.sourcePathKeyOrId);
                changed = FillCustomGroupBindingSlotDefaults(group);
            }
            else if (string.Equals(code, "CUSTOM_GROUP_REFERENCE_SLOT_MISSING_TARGET", StringComparison.OrdinalIgnoreCase))
            {
                PungentBoardGroup group = PungentBoardCanvasGUI.FindGroup(board, issue.sourcePathKeyOrId);
                changed = MarkMissingReferenceSlotsOptional(group);
            }

            if (!changed)
                return;

            MarkContentDirty("Applied board validation quick fix.");
            ValidateCurrentBoard();
        }

        private static bool RegenerateDuplicateOrMissingNodeId(PungentBoardDocument board, string sourceId)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardNode node in board.nodes ?? new List<PungentBoardNode>())
            {
                if (node == null)
                    continue;

                bool missing = string.IsNullOrWhiteSpace(node.id);
                bool duplicate = !missing && !seen.Add(node.id);
                bool matches = string.IsNullOrWhiteSpace(sourceId) || PungentAuthoringId.EqualsId(node.id, sourceId) || string.Equals(node.title, sourceId, StringComparison.OrdinalIgnoreCase);
                if ((missing || duplicate) && matches)
                {
                    node.id = PungentAuthoringId.NewValue();
                    node.NormalizeInPlace();
                    return true;
                }
            }

            return false;
        }

        private static bool RegenerateDuplicateOrMissingGroupId(PungentBoardDocument board, string sourceId)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentBoardGroup group in board.groups ?? new List<PungentBoardGroup>())
            {
                if (group == null)
                    continue;

                bool missing = string.IsNullOrWhiteSpace(group.id);
                bool duplicate = !missing && !seen.Add(group.id);
                bool matches = string.IsNullOrWhiteSpace(sourceId) || PungentAuthoringId.EqualsId(group.id, sourceId) || string.Equals(group.title, sourceId, StringComparison.OrdinalIgnoreCase);
                if ((missing || duplicate) && matches)
                {
                    group.id = PungentAuthoringId.NewValue();
                    group.NormalizeInPlace();
                    return true;
                }
            }

            return false;
        }

        private static bool NormalizeCustomGroupPortKeys(PungentBoardGroup group)
        {
            if (group == null)
                return false;

            bool changed = false;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int counter = 1;
            foreach (PungentBoardPortDefinition port in group.exposedPorts ?? new List<PungentBoardPortDefinition>())
            {
                if (port == null)
                    continue;

                string key = string.IsNullOrWhiteSpace(port.key) ? "port" + counter.ToString(System.Globalization.CultureInfo.InvariantCulture) : port.key.Trim();
                while (seen.Contains(key))
                {
                    counter++;
                    key = "port" + counter.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                if (!string.Equals(port.key, key, StringComparison.Ordinal))
                {
                    port.key = key;
                    changed = true;
                }

                seen.Add(key);
                counter++;
                port.NormalizeInPlace();
            }

            group.NormalizeInPlace();
            return changed;
        }

        private static bool RemoveInvalidCustomGroupMappings(PungentBoardDocument board, PungentBoardGroup group)
        {
            if (board == null || group == null || group.exposedPortMappings == null)
                return false;

            HashSet<string> portKeys = new HashSet<string>((group.exposedPorts ?? new List<PungentBoardPortDefinition>()).Where(port => port != null).Select(port => port.key), StringComparer.OrdinalIgnoreCase);
            HashSet<string> nodeIds = new HashSet<string>((board.nodes ?? new List<PungentBoardNode>()).Where(node => node != null).Select(node => node.id), StringComparer.OrdinalIgnoreCase);
            int before = group.exposedPortMappings.Count;
            group.exposedPortMappings.RemoveAll(mapping =>
                mapping == null ||
                string.IsNullOrWhiteSpace(mapping.exposedPortKey) ||
                !portKeys.Contains(mapping.exposedPortKey) ||
                string.IsNullOrWhiteSpace(mapping.internalNodeId) ||
                !nodeIds.Contains(mapping.internalNodeId) ||
                (string.IsNullOrWhiteSpace(mapping.internalPortKey) && string.IsNullOrWhiteSpace(mapping.internalFieldKey)));
            if (group.exposedPortMappings.Count == before)
                return false;

            group.NormalizeInPlace();
            return true;
        }

        private static bool FillCustomGroupBindingSlotDefaults(PungentBoardGroup group)
        {
            if (group == null)
                return false;

            bool changed = false;
            foreach (PungentAuthoringBindingSlot slot in group.exposedBindingSlots ?? new List<PungentAuthoringBindingSlot>())
            {
                if (slot == null)
                    continue;
                if (string.IsNullOrWhiteSpace(slot.fieldKey) && string.IsNullOrWhiteSpace(slot.pathId))
                {
                    slot.fieldKey = "group.parameter";
                    slot.elementId = group.id;
                    slot.NormalizeInPlace();
                    changed = true;
                }
            }

            group.NormalizeInPlace();
            return changed;
        }

        private static bool MarkMissingReferenceSlotsOptional(PungentBoardGroup group)
        {
            if (group == null)
                return false;

            bool changed = false;
            foreach (PungentBoardCustomNodeReferenceSlot slot in group.referenceSlots ?? new List<PungentBoardCustomNodeReferenceSlot>())
            {
                if (slot == null || !slot.required)
                    continue;

                bool hasTarget = slot.target != null && slot.target.HasTarget;
                bool hasPath = slot.bindingPath != null && slot.bindingPath.HasPath;
                if (!hasTarget && !hasPath)
                {
                    slot.required = false;
                    slot.NormalizeInPlace();
                    changed = true;
                }
            }

            group.NormalizeInPlace();
            return changed;
        }

        private string FirstBoardId()
        {
            PungentBoardDocument first = (PungentBoardEditorStorage.Database.documents ?? new List<PungentBoardDocument>()).FirstOrDefault(board => board != null);
            return first != null ? first.id : string.Empty;
        }

        private bool MatchesBoardFilter(PungentBoardDocument board)
        {
            if (board == null)
                return false;

            string query = (_boardSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return Contains(board.title, query) ||
                   Contains(board.summary, query) ||
                   Contains(board.status, query) ||
                   Contains(board.priority, query) ||
                   Contains(board.visibility, query) ||
                   (board.tags != null && board.tags.Any(tag => Contains(tag, query)));
        }

        private bool MatchesContentSearch(PungentBoardNode node)
        {
            string query = (_contentSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query) || node == null)
                return true;

            return Contains(node.title, query) ||
                   Contains(node.summary, query) ||
                   Contains(node.body, query) ||
                   Contains(node.nodeKind.ToString(), query) ||
                   Contains(node.nodeTypeKey, query) ||
                   Contains(node.id, query) ||
                   Contains(node.colorStyleKey, query) ||
                   (node.linkedAuthoringRef != null && (Contains(node.linkedAuthoringRef.itemId, query) || Contains(node.linkedAuthoringRef.providerId, query) || Contains(node.linkedAuthoringRef.label, query))) ||
                   Contains(node.integrationSourceKey, query) ||
                   Contains(node.integrationSourceLabel, query);
        }

        private bool MatchesContentSearch(PungentBoardEdge edge)
        {
            string query = (_contentSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query) || edge == null)
                return true;

            return Contains(edge.label, query) ||
                   Contains(edge.edgeKind.ToString(), query) ||
                   Contains(edge.edgeTypeKey, query) ||
                   Contains(edge.id, query) ||
                   Contains(edge.fromNodeId, query) ||
                   Contains(edge.toNodeId, query) ||
                   Contains(edge.styleKey, query);
        }

        private bool MatchesContentSearch(PungentBoardGroup group)
        {
            string query = (_contentSearch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query) || group == null)
                return true;

            return Contains(group.title, query) ||
                   Contains(group.id, query) ||
                   Contains(group.colorStyleKey, query) ||
                   (group.containedNodeIds != null && group.containedNodeIds.Any(id => Contains(id, query)));
        }

        private void FrameFirstSearchMatch()
        {
            PungentBoardDocument board = CurrentBoard;
            if (board == null || string.IsNullOrWhiteSpace(_contentSearch))
                return;

            PungentBoardNode node = (board.nodes ?? new List<PungentBoardNode>()).FirstOrDefault(item => MatchesContentSearch(item));
            if (node != null)
            {
                _canvasState.SelectNode(node.id);
                FrameSelection();
                return;
            }

            PungentBoardEdge edge = (board.edges ?? new List<PungentBoardEdge>()).FirstOrDefault(item => MatchesContentSearch(item));
            if (edge != null)
            {
                _canvasState.SelectEdge(edge.id);
                FrameSelection();
                return;
            }

            PungentBoardGroup group = (board.groups ?? new List<PungentBoardGroup>()).FirstOrDefault(item => MatchesContentSearch(item));
            if (group != null)
            {
                _canvasState.SelectGroup(group.id);
                FrameSelection();
            }
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<string> SplitCsv(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<PungentAuthoringItemKind> ParseKinds(string value)
        {
            List<PungentAuthoringItemKind> result = new List<PungentAuthoringItemKind>();
            foreach (string item in SplitCsv(value))
            {
                PungentAuthoringItemKind kind;
                if (Enum.TryParse(item, true, out kind) && !result.Contains(kind))
                    result.Add(kind);
            }

            return result;
        }

        private string SelectionPill()
        {
            if (_canvasState.MultiSelectionCount > 1)
                return _canvasState.MultiSelectionCount + " selected";
            if (!string.IsNullOrWhiteSpace(_canvasState.selectedNodeId))
                return "Node";
            if (!string.IsNullOrWhiteSpace(_canvasState.selectedEdgeId))
                return "Edge";
            if (!string.IsNullOrWhiteSpace(_canvasState.selectedGroupId))
                return "Group";
            return "Board";
        }

        private string ValidationFooter()
        {
            if (_validationResult == null)
                return "Validation: not run";

            int issueCount = _validationResult.issues?.Count ?? 0;
            int missingProviderCount = _validationResult.issues == null
                ? 0
                : _validationResult.issues.Count(issue => issue != null && string.Equals(issue.issueCode, "MISSING_PROVIDER", StringComparison.OrdinalIgnoreCase));
            return "Validation: " + _validationResult.status + " | Issues: " + issueCount + " | Missing providers: " + missingProviderCount;
        }

        private static string BuildEdgeEndpointLabel(PungentBoardDocument board, PungentBoardEdge edge)
        {
            if (board == null || edge == null)
                return "Edge";

            PungentBoardNode from = PungentBoardCanvasGUI.FindNode(board, edge.fromNodeId);
            PungentBoardNode to = PungentBoardCanvasGUI.FindNode(board, edge.toNodeId);
            string fromLabel = from == null ? "Missing" : string.IsNullOrWhiteSpace(from.title) ? from.id : from.title;
            string toLabel = to == null ? "Missing" : string.IsNullOrWhiteSpace(to.title) ? to.id : to.title;
            return fromLabel + (edge.directed ? " -> " : " - ") + toLabel;
        }

        private static string PreviewText(string value, int maxLength)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace("\r", " ").Replace("\n", " ");
            return clean.Length <= maxLength ? clean : clean.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
        }
    }
#endif
}
