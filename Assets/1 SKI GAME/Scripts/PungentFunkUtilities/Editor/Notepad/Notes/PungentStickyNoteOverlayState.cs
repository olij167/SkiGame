using System;
using System.Collections.Generic;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Authoring;
    using PungentFunk.Utilities.Editor.Authoring;
    using UnityEditor;
    using UnityEngine;

    public enum PungentStickyNoteOverlayMode
    {
        None,
        HoverPreview,
        PreviewLocked,
        EditLocked,
        StackPreview,
        StackEdit,
        InfoPreview,
        AuthoringPreview,
        AuthoringLocked
    }

    public enum PungentStickyNoteOverlayOwner
    {
        BrowserWindow,
        InspectorHeader,
        PropertyContextMenu,
        SceneView,
        UtilitySurface,
        HelpSurface,
        AuthoringBrowser
    }

    public sealed class PungentStickyNoteOverlayState
    {
        public PungentStickyNoteOverlayMode mode = PungentStickyNoteOverlayMode.None;
        public PungentStickyNoteOverlayOwner owner = PungentStickyNoteOverlayOwner.BrowserWindow;
        public Rect anchorRect;
        public Rect activeRect;
        public string sourceLabel = string.Empty;
        public string noteId = string.Empty;
        public List<string> noteIds = new List<string>();
        public string infoKey = string.Empty;
        public string infoTitle = string.Empty;
        public string infoBody = string.Empty;
        public string infoDetail = string.Empty;
        public PungentAuthoringReference authoringReference;
        public PungentAuthoringPreview authoringPreview;
        public string authoringStatus = string.Empty;
        public Vector2 scroll;
        public Vector2 authoringScroll;
        public Vector2 dataSheetScroll;
        public Vector2 nodeGraphScroll;
        public Vector2 nodeGraphViewportSize;
        public float nodeGraphZoom = 1f;
        public string nodeGraphSelectedNodeId = string.Empty;
        public List<string> nodeGraphSelectedNodeIds = new List<string>();
        public bool nodeGraphPanning;
        public bool nodeGraphMarqueeActive;
        public Vector2 nodeGraphPanStartMouse;
        public Vector2 nodeGraphPanStartScroll;
        public Vector2 nodeGraphMarqueeStart;
        public Vector2 nodeGraphMarqueeEnd;
        public string dataSheetSearch = string.Empty;
        public bool dataSheetShowHiddenRows;
        public bool dataSheetShowHiddenColumns;
        public bool dataSheetHideEmptyRows;
        public bool dataSheetHideEmptyColumns;
        public string dataSheetAppliedSearch = string.Empty;
        public double dataSheetLastSearchEditTime;
        public bool dataSheetFilterPending;
        public string checklistSearch = string.Empty;
        public string checklistAppliedSearch = string.Empty;
        public double checklistLastSearchEditTime;
        public bool checklistFilterPending;
        public bool checklistShowArchived;
        public Vector2 checklistRunScroll;
        public Vector2 checklistEditScroll;
        public string checklistFocusedCommentItemId = string.Empty;
        public double checklistLastCommentEditTime;
        public Dictionary<string, string> checklistCommentDrafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> checklistDirtyCommentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool checklistCommentsDirty;
        public int checklistOverlayDirtyVersion;
        public bool richDocumentRawMode;
        public Vector2 stackScroll;
        public bool previewMode;
        public int noteOverlayTab;
        public bool showLinks = true;
        public bool showTargets;
        public bool showAdvanced;
        public bool supportRequestSendReview;

        public string draftNoteId = string.Empty;
        public string draftTitle = string.Empty;
        public string draftBody = string.Empty;
        public bool draftDirty;
        public double lastDraftEditTime;
        public string draftSaveState = "Saved";
        public string status = "Ready.";

        public bool IsEditMode
        {
            get { return mode == PungentStickyNoteOverlayMode.PreviewLocked || mode == PungentStickyNoteOverlayMode.EditLocked || mode == PungentStickyNoteOverlayMode.StackEdit || mode == PungentStickyNoteOverlayMode.AuthoringLocked; }
        }

        public bool IsHoverMode
        {
            get { return mode == PungentStickyNoteOverlayMode.HoverPreview || mode == PungentStickyNoteOverlayMode.InfoPreview || mode == PungentStickyNoteOverlayMode.StackPreview || mode == PungentStickyNoteOverlayMode.AuthoringPreview; }
        }

        public bool IsAuthoringMode
        {
            get { return mode == PungentStickyNoteOverlayMode.AuthoringPreview || mode == PungentStickyNoteOverlayMode.AuthoringLocked; }
        }

        public void Reset()
        {
            mode = PungentStickyNoteOverlayMode.None;
            anchorRect = Rect.zero;
            activeRect = Rect.zero;
            sourceLabel = string.Empty;
            noteId = string.Empty;
            noteIds.Clear();
            infoKey = string.Empty;
            infoTitle = string.Empty;
            infoBody = string.Empty;
            infoDetail = string.Empty;
            authoringReference = null;
            authoringPreview = null;
            authoringStatus = string.Empty;
            scroll = Vector2.zero;
            authoringScroll = Vector2.zero;
            dataSheetScroll = Vector2.zero;
            nodeGraphScroll = Vector2.zero;
            nodeGraphViewportSize = Vector2.zero;
            nodeGraphZoom = 1f;
            nodeGraphSelectedNodeId = string.Empty;
            nodeGraphSelectedNodeIds.Clear();
            nodeGraphPanning = false;
            nodeGraphMarqueeActive = false;
            nodeGraphPanStartMouse = Vector2.zero;
            nodeGraphPanStartScroll = Vector2.zero;
            nodeGraphMarqueeStart = Vector2.zero;
            nodeGraphMarqueeEnd = Vector2.zero;
            dataSheetSearch = string.Empty;
            dataSheetShowHiddenRows = false;
            dataSheetShowHiddenColumns = false;
            dataSheetHideEmptyRows = false;
            dataSheetHideEmptyColumns = false;
            dataSheetAppliedSearch = string.Empty;
            dataSheetLastSearchEditTime = EditorApplication.timeSinceStartup;
            dataSheetFilterPending = false;
            ResetChecklistOverlayState();
            richDocumentRawMode = false;
            stackScroll = Vector2.zero;
            previewMode = false;
            noteOverlayTab = 0;
            showLinks = true;
            showTargets = false;
            showAdvanced = false;
            supportRequestSendReview = false;
            ClearDraft();
        }

        public void ResetChecklistOverlayState()
        {
            checklistSearch = string.Empty;
            checklistAppliedSearch = string.Empty;
            checklistLastSearchEditTime = EditorApplication.timeSinceStartup;
            checklistFilterPending = false;
            checklistShowArchived = false;
            checklistRunScroll = Vector2.zero;
            checklistEditScroll = Vector2.zero;
            checklistFocusedCommentItemId = string.Empty;
            checklistLastCommentEditTime = EditorApplication.timeSinceStartup;
            if (checklistCommentDrafts == null)
                checklistCommentDrafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            else
                checklistCommentDrafts.Clear();
            if (checklistDirtyCommentKeys == null)
                checklistDirtyCommentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            else
                checklistDirtyCommentKeys.Clear();
            checklistCommentsDirty = false;
            checklistOverlayDirtyVersion++;
        }

        public void ClearDraft()
        {
            draftNoteId = string.Empty;
            draftTitle = string.Empty;
            draftBody = string.Empty;
            draftDirty = false;
            lastDraftEditTime = EditorApplication.timeSinceStartup;
            draftSaveState = "Saved";
        }

        public void LoadDraft(PungentNote note)
        {
            if (note == null)
            {
                ClearDraft();
                return;
            }

            draftNoteId = note.id ?? string.Empty;
            draftTitle = note.title ?? string.Empty;
            draftBody = note.body ?? string.Empty;
            draftDirty = false;
            lastDraftEditTime = EditorApplication.timeSinceStartup;
            draftSaveState = "Saved";
        }

        public void MarkDraftDirty(string saveState)
        {
            draftDirty = true;
            draftSaveState = string.IsNullOrWhiteSpace(saveState) ? "Unsaved changes" : saveState;
            lastDraftEditTime = EditorApplication.timeSinceStartup;
            status = draftSaveState + ".";
        }

        public bool CommitDraftIfDirty(string saveStatus)
        {
            if (!draftDirty || string.IsNullOrWhiteSpace(draftNoteId))
                return false;

            PungentNote note = PungentStickyNoteOverlayController.FindNote(draftNoteId);
            if (note == null)
            {
                draftDirty = false;
                draftSaveState = "Draft target missing";
                status = "Draft target is missing.";
                return true;
            }

            bool changed = !string.Equals(note.title ?? string.Empty, draftTitle ?? string.Empty, StringComparison.Ordinal) ||
                           !string.Equals(note.body ?? string.Empty, draftBody ?? string.Empty, StringComparison.Ordinal);
            if (changed)
            {
                note.title = string.IsNullOrWhiteSpace(draftTitle) ? "Untitled Note" : draftTitle;
                note.body = draftBody ?? string.Empty;
                PungentNoteStorage.Database.Touch(note);
                PungentNoteStorage.Save();
            }

            draftDirty = false;
            draftSaveState = !string.IsNullOrWhiteSpace(saveStatus) &&
                             saveStatus.IndexOf("auto", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Autosaved"
                : "Saved";
            status = string.IsNullOrWhiteSpace(saveStatus) ? "Saved note." : saveStatus;
            return true;
        }
    }
#endif
}
