using System;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public enum PungentBoardGraphCommand
    {
        Save,
        Undo,
        Redo,
        DeleteSelection,
        FrameSelection,
        FitAll,
        AddNode,
        AddGroup,
        DuplicateSelection,
        ToggleConnectorMode,
        CancelTransientAction,
        ResetZoom,
        ToggleGrid,
        ToggleSnap,
        OpenGridOverlay,
        OpenSnapOverlay,
        OpenShortcutOverlay
    }

    public enum PungentBoardCanvasOverlayKind
    {
        None,
        Grid,
        Snap,
        Shortcuts
    }

    public enum PungentBoardInlineNodeEditField
    {
        None,
        Title,
        Body
    }

    public sealed class PungentBoardInlineNodeEditState
    {
        public string nodeId = string.Empty;
        public PungentBoardInlineNodeEditField field = PungentBoardInlineNodeEditField.None;
        public string draft = string.Empty;
        public bool requestFocus;

        public bool IsEditing(string targetNodeId, PungentBoardInlineNodeEditField targetField)
        {
            return field == targetField && PungentAuthoringId.EqualsId(nodeId, targetNodeId);
        }

        public void Begin(PungentBoardNode node, PungentBoardInlineNodeEditField targetField)
        {
            if (node == null || targetField == PungentBoardInlineNodeEditField.None)
            {
                Clear();
                return;
            }

            nodeId = PungentAuthoringId.Normalize(node.id);
            field = targetField;
            draft = targetField == PungentBoardInlineNodeEditField.Body ? node.body ?? string.Empty : node.title ?? string.Empty;
            requestFocus = true;
        }

        public void Clear()
        {
            nodeId = string.Empty;
            field = PungentBoardInlineNodeEditField.None;
            draft = string.Empty;
            requestFocus = false;
        }
    }

    public sealed class PungentBoardGraphCommandRouter
    {
        private readonly Func<PungentBoardDocument> _currentBoard;

        public PungentBoardGraphCommandRouter(Func<PungentBoardDocument> currentBoard)
        {
            _currentBoard = currentBoard;
        }

        public Func<bool> CanUndo { get; set; }
        public Func<bool> CanRedo { get; set; }
        public Action<string> StatusChanged { get; set; }
        public Action RepaintRequested { get; set; }

        public Action Save { get; set; }
        public Action Undo { get; set; }
        public Action Redo { get; set; }
        public Action DeleteSelection { get; set; }
        public Action FrameSelection { get; set; }
        public Action FitAll { get; set; }
        public Action AddNode { get; set; }
        public Action AddGroup { get; set; }
        public Action DuplicateSelection { get; set; }
        public Action ToggleConnectorMode { get; set; }
        public Action CancelTransientAction { get; set; }
        public Action ResetZoom { get; set; }
        public Action ToggleGrid { get; set; }
        public Action ToggleSnap { get; set; }
        public Action OpenGridOverlay { get; set; }
        public Action OpenSnapOverlay { get; set; }
        public Action OpenShortcutOverlay { get; set; }

        public bool Execute(PungentBoardGraphCommand command, string origin = null)
        {
            if (!CanExecute(command))
            {
                StatusChanged?.Invoke("Command unavailable: " + Label(command) + ".");
                RepaintRequested?.Invoke();
                return false;
            }

            switch (command)
            {
                case PungentBoardGraphCommand.Save:
                    Save?.Invoke();
                    break;
                case PungentBoardGraphCommand.Undo:
                    Undo?.Invoke();
                    break;
                case PungentBoardGraphCommand.Redo:
                    Redo?.Invoke();
                    break;
                case PungentBoardGraphCommand.DeleteSelection:
                    DeleteSelection?.Invoke();
                    break;
                case PungentBoardGraphCommand.FrameSelection:
                    FrameSelection?.Invoke();
                    break;
                case PungentBoardGraphCommand.FitAll:
                    FitAll?.Invoke();
                    break;
                case PungentBoardGraphCommand.AddNode:
                    AddNode?.Invoke();
                    break;
                case PungentBoardGraphCommand.AddGroup:
                    AddGroup?.Invoke();
                    break;
                case PungentBoardGraphCommand.DuplicateSelection:
                    DuplicateSelection?.Invoke();
                    break;
                case PungentBoardGraphCommand.ToggleConnectorMode:
                    ToggleConnectorMode?.Invoke();
                    break;
                case PungentBoardGraphCommand.CancelTransientAction:
                    CancelTransientAction?.Invoke();
                    break;
                case PungentBoardGraphCommand.ResetZoom:
                    ResetZoom?.Invoke();
                    break;
                case PungentBoardGraphCommand.ToggleGrid:
                    ToggleGrid?.Invoke();
                    break;
                case PungentBoardGraphCommand.ToggleSnap:
                    ToggleSnap?.Invoke();
                    break;
                case PungentBoardGraphCommand.OpenGridOverlay:
                    OpenGridOverlay?.Invoke();
                    break;
                case PungentBoardGraphCommand.OpenSnapOverlay:
                    OpenSnapOverlay?.Invoke();
                    break;
                case PungentBoardGraphCommand.OpenShortcutOverlay:
                    OpenShortcutOverlay?.Invoke();
                    break;
            }

            if (!string.IsNullOrWhiteSpace(origin))
                StatusChanged?.Invoke(Label(command) + " via " + origin + ".");
            RepaintRequested?.Invoke();
            return true;
        }

        public bool CanExecute(PungentBoardGraphCommand command)
        {
            if (command == PungentBoardGraphCommand.Undo)
                return CanUndo == null || CanUndo();
            if (command == PungentBoardGraphCommand.Redo)
                return CanRedo == null || CanRedo();
            if (command == PungentBoardGraphCommand.OpenShortcutOverlay || command == PungentBoardGraphCommand.CancelTransientAction)
                return true;
            if (command == PungentBoardGraphCommand.Save)
                return _currentBoard == null || _currentBoard() != null;

            return _currentBoard == null || _currentBoard() != null;
        }

        public static string Label(PungentBoardGraphCommand command)
        {
            switch (command)
            {
                case PungentBoardGraphCommand.Save: return "Save";
                case PungentBoardGraphCommand.Undo: return "Undo";
                case PungentBoardGraphCommand.Redo: return "Redo";
                case PungentBoardGraphCommand.DeleteSelection: return "Delete selection";
                case PungentBoardGraphCommand.FrameSelection: return "Frame selection";
                case PungentBoardGraphCommand.FitAll: return "Fit all";
                case PungentBoardGraphCommand.AddNode: return "Add node";
                case PungentBoardGraphCommand.AddGroup: return "Add group";
                case PungentBoardGraphCommand.DuplicateSelection: return "Duplicate selection";
                case PungentBoardGraphCommand.ToggleConnectorMode: return "Toggle connector mode";
                case PungentBoardGraphCommand.CancelTransientAction: return "Cancel";
                case PungentBoardGraphCommand.ResetZoom: return "Reset zoom";
                case PungentBoardGraphCommand.ToggleGrid: return "Toggle grid";
                case PungentBoardGraphCommand.ToggleSnap: return "Toggle snap";
                case PungentBoardGraphCommand.OpenGridOverlay: return "Grid settings";
                case PungentBoardGraphCommand.OpenSnapOverlay: return "Snap settings";
                case PungentBoardGraphCommand.OpenShortcutOverlay: return "Shortcuts";
                default: return ObjectNames.NicifyVariableName(command.ToString());
            }
        }
    }
#endif
}
