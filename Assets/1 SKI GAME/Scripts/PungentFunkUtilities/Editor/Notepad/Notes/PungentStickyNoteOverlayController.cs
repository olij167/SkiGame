using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Authoring;
    using PungentFunk.Utilities.Editor.Authoring;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class PungentStickyNoteOverlayController
    {
        private const double DraftAutosaveDelaySeconds = 1.25d;

        private static readonly PungentStickyNoteOverlayState State = new PungentStickyNoteOverlayState();
        private static string _hoverKey = string.Empty;
        private static double _hoverStart;
        private static string _lastCacheStatus = "Idle";
        private static Dictionary<string, PungentNote> _noteById;
        private static int _noteIndexCount = -1;
        private static string _noteIndexLastSavedUtc = string.Empty;

        internal static event Action<PungentStickyNoteOverlayState> OverlayUpdate;
        internal static event Action<PungentStickyNoteOverlayState> BeforeOverlayReset;

        static PungentStickyNoteOverlayController()
        {
            EditorApplication.update -= HandleEditorUpdate;
            EditorApplication.update += HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= CommitBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += CommitBeforeReload;
        }

        public static string LastCacheStatus
        {
            get { return _lastCacheStatus; }
        }

        public static bool HasEditOverlay
        {
            get { return State.IsEditMode; }
        }

        internal static bool HasFloatingOverlay
        {
            get { return UsesFloatingTray(State.mode); }
        }

        internal static PungentStickyNoteOverlayOwner ActiveOwner
        {
            get { return State.owner; }
        }

        public static bool IsPointerOverActiveOverlay()
        {
            if (PungentStickyNoteOverlayTrayWindow.IsMouseOver)
                return true;

            return Event.current != null && State.activeRect.Contains(Event.current.mousePosition);
        }

        public static PungentNote FindNote(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return null;

            EnsureNoteIndex();
            return _noteById != null && _noteById.TryGetValue(noteId, out PungentNote note) ? note : null;
        }

        public static void InvalidateNoteIndex()
        {
            _noteById = null;
            _noteIndexCount = -1;
            _noteIndexLastSavedUtc = string.Empty;
        }

        public static void Apply(PungentStickyNoteOverlayRequest request)
        {
            if (request == null)
                return;

            if (request.mode == PungentStickyNoteOverlayMode.PreviewLocked ||
                request.mode == PungentStickyNoteOverlayMode.EditLocked ||
                request.mode == PungentStickyNoteOverlayMode.StackEdit ||
                request.mode == PungentStickyNoteOverlayMode.AuthoringLocked)
                State.CommitDraftIfDirty("Saved draft before opening overlay.");

            NotifyBeforeOverlayReset();
            State.mode = request.mode;
            State.owner = request.owner;
            State.anchorRect = request.anchorRect;
            State.sourceLabel = request.sourceLabel ?? string.Empty;
            State.noteId = request.noteId ?? string.Empty;
            State.noteIds = request.noteIds == null ? new List<string>() : new List<string>(request.noteIds);
            State.infoKey = request.infoKey ?? string.Empty;
            State.infoTitle = request.infoTitle ?? string.Empty;
            State.infoBody = request.infoBody ?? string.Empty;
            State.infoDetail = request.infoDetail ?? string.Empty;
            State.authoringReference = request.authoringReference;
            State.authoringPreview = request.authoringPreview;
            State.authoringStatus = string.Empty;
            State.activeRect = Rect.zero;
            State.authoringScroll = Vector2.zero;
            State.dataSheetScroll = Vector2.zero;
            State.nodeGraphScroll = Vector2.zero;
            State.nodeGraphViewportSize = Vector2.zero;
            State.nodeGraphZoom = 1f;
            State.nodeGraphSelectedNodeId = string.Empty;
            State.nodeGraphSelectedNodeIds.Clear();
            State.nodeGraphPanning = false;
            State.nodeGraphMarqueeActive = false;
            State.nodeGraphPanStartMouse = Vector2.zero;
            State.nodeGraphPanStartScroll = Vector2.zero;
            State.nodeGraphMarqueeStart = Vector2.zero;
            State.nodeGraphMarqueeEnd = Vector2.zero;
            State.dataSheetSearch = string.Empty;
            State.dataSheetShowHiddenRows = false;
            State.dataSheetShowHiddenColumns = false;
            State.dataSheetHideEmptyRows = false;
            State.dataSheetHideEmptyColumns = false;
            State.ResetChecklistOverlayState();
            State.richDocumentRawMode = false;

            if (State.mode == PungentStickyNoteOverlayMode.PreviewLocked ||
                State.mode == PungentStickyNoteOverlayMode.EditLocked)
                State.LoadDraft(FindNote(State.noteId));
            else if (State.mode == PungentStickyNoteOverlayMode.StackEdit && State.noteIds.Count == 1)
            {
                State.noteId = State.noteIds[0];
                State.mode = PungentStickyNoteOverlayMode.EditLocked;
                State.LoadDraft(FindNote(State.noteId));
            }
            else if (State.mode == PungentStickyNoteOverlayMode.AuthoringLocked)
            {
                State.ClearDraft();
            }
            else if (!State.IsEditMode)
            {
                State.ClearDraft();
            }

            if (UsesFloatingTray(State.mode))
                PungentStickyNoteOverlayTrayWindow.ShowForActiveState(request.anchorRect);
        }

        public static void RequestHoverPreview(
            PungentNote note,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel,
            bool enabled = true,
            bool suppress = false,
            float delaySeconds = 0.35f)
        {
            if (note == null)
                return;

            RequestHoverPreview(note.id, note, anchorRect, owner, sourceLabel, enabled, suppress, delaySeconds);
        }

        public static void RequestHoverPreview(
            string noteId,
            PungentNote note,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel,
            bool enabled,
            bool suppress,
            float delaySeconds)
        {
            if (State.IsEditMode || note == null)
                return;

            string id = string.IsNullOrWhiteSpace(noteId) ? note.id : noteId;
            if (!ShouldShow(anchorRect, "note:" + id + ":" + owner + ":" + sourceLabel, enabled, suppress, delaySeconds))
                return;

            Apply(PungentStickyNoteOverlayRequest.ForNote(
                PungentStickyNoteOverlayMode.HoverPreview,
                id,
                anchorRect,
                owner,
                sourceLabel));
        }

        public static void RequestInfoPreview(
            Rect anchorRect,
            string key,
            string title,
            string body,
            string detail,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel,
            bool enabled = true,
            bool suppress = false,
            float delaySeconds = 0.35f)
        {
            if (State.IsEditMode)
                return;

            string normalizedKey = string.IsNullOrWhiteSpace(key) ? title : key;
            if (!ShouldShow(anchorRect, "info:" + normalizedKey + ":" + owner, enabled, suppress, delaySeconds))
                return;

            Apply(PungentStickyNoteOverlayRequest.ForInfo(
                normalizedKey,
                title,
                body,
                detail,
                anchorRect,
                owner,
                sourceLabel));
        }

        public static void RequestAuthoringPreview(
            PungentAuthoringReference reference,
            PungentAuthoringPreview preview,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel,
            bool enabled = true,
            bool suppress = false,
            float delaySeconds = 0.35f)
        {
            if (State.IsEditMode || reference == null)
                return;

            string key = "authoring:" + (reference.providerId ?? string.Empty) + ":" + reference.itemKind + ":" + (reference.itemId ?? string.Empty) + ":" + owner;
            if (!ShouldShow(anchorRect, key, enabled, suppress, delaySeconds))
                return;

            if (State.mode == PungentStickyNoteOverlayMode.AuthoringPreview &&
                State.owner == owner &&
                SameAuthoringReference(State.authoringReference, reference))
            {
                State.anchorRect = anchorRect;
                return;
            }

            Apply(PungentStickyNoteOverlayRequest.ForAuthoring(
                PungentStickyNoteOverlayMode.AuthoringPreview,
                reference,
                ResolvePreview(reference, preview),
                anchorRect,
                owner,
                sourceLabel));
        }

        public static void OpenAuthoringPreview(
            PungentAuthoringReference reference,
            PungentAuthoringPreview preview,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            if (reference == null)
                return;

            Apply(PungentStickyNoteOverlayRequest.ForAuthoring(
                PungentStickyNoteOverlayMode.AuthoringLocked,
                reference,
                ResolvePreview(reference, preview),
                anchorRect,
                owner,
                sourceLabel));
        }

        public static void OpenEdit(
            string noteId,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return;

            Apply(PungentStickyNoteOverlayRequest.ForNote(
                PungentStickyNoteOverlayMode.EditLocked,
                noteId,
                anchorRect,
                owner,
                sourceLabel));
        }

        public static void OpenSupportRequestReview(
            string noteId,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return;

            OpenEdit(noteId, anchorRect, owner, sourceLabel);
            State.supportRequestSendReview = true;
            State.noteOverlayTab = 0;
            State.status = "Review this support request, then press Send below.";
        }

        public static void OpenPreview(
            string noteId,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return;

            Apply(PungentStickyNoteOverlayRequest.ForNote(
                PungentStickyNoteOverlayMode.PreviewLocked,
                noteId,
                anchorRect,
                owner,
                sourceLabel));
        }

        public static void OpenPreview(
            PungentNote note,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            if (note != null)
                OpenPreview(note.id, anchorRect, owner, sourceLabel);
        }

        public static void OpenEdit(
            PungentNote note,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            if (note != null)
                OpenEdit(note.id, anchorRect, owner, sourceLabel);
        }

        public static void OpenStack(
            IEnumerable<PungentNote> notes,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel,
            bool edit = false)
        {
            List<PungentNote> resolved = notes == null
                ? new List<PungentNote>()
                : notes.Where(note => note != null).ToList();

            if (resolved.Count == 0)
                return;

            if (resolved.Count == 1)
            {
                OpenEdit(resolved[0], anchorRect, owner, sourceLabel);
                return;
            }

            Apply(PungentStickyNoteOverlayRequest.ForStack(
                edit ? PungentStickyNoteOverlayMode.StackEdit : PungentStickyNoteOverlayMode.StackPreview,
                resolved,
                anchorRect,
                owner,
                sourceLabel));
        }

        public static PungentNote CreateForTargetAndEdit(
            UnityEngine.Object target,
            string propertyPath,
            string propertyName,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner)
        {
            PungentNote note = PungentNoteStorage.CreateNoteForTarget(target, propertyPath, propertyName);
            if (note == null)
                return null;

            OpenEdit(note.id, anchorRect, owner, string.IsNullOrWhiteSpace(propertyName) ? "Selection" : propertyName);
            return note;
        }

        public static void DoneEditing(PungentStickyNoteOverlayOwner owner)
        {
            if (State.owner != owner && State.owner != PungentStickyNoteOverlayOwner.PropertyContextMenu)
                return;

            State.CommitDraftIfDirty("Saved note.");
            ResetStateAndNotify();
            _hoverKey = string.Empty;
            _lastCacheStatus = "Closed";
            PungentStickyNoteOverlayTrayWindow.CloseIfOpen();
        }

        public static void Close(PungentStickyNoteOverlayOwner owner)
        {
            if (State.owner != owner && State.owner != PungentStickyNoteOverlayOwner.PropertyContextMenu)
                return;

            State.CommitDraftIfDirty("Saved note.");
            ResetStateAndNotify();
            _hoverKey = string.Empty;
            _lastCacheStatus = "Closed";
            PungentStickyNoteOverlayTrayWindow.CloseIfOpen();
        }

        public static void CloseTransient(PungentStickyNoteOverlayOwner owner)
        {
            if (State.owner != owner && State.owner != PungentStickyNoteOverlayOwner.PropertyContextMenu)
                return;
            if (UsesFloatingTray(State.mode))
                return;

            ResetStateAndNotify();
            _hoverKey = string.Empty;
            _lastCacheStatus = "Closed";
        }

        public static bool CommitActiveDraft(string status)
        {
            return State.CommitDraftIfDirty(status);
        }

        public static void Draw(PungentStickyNoteOverlayOwner owner, Rect bounds)
        {
            if (State.mode == PungentStickyNoteOverlayMode.None)
                return;
            if (State.owner != owner)
                return;
            if (Event.current == null)
                return;
            if (UsesFloatingTray(State.mode))
                return;

            bool layoutEvent = Event.current.type == EventType.Layout;
            if (!layoutEvent && State.IsHoverMode && !PointerStillOwnsHover())
            {
                ResetStateAndNotify();
                return;
            }

            if (!layoutEvent &&
                (State.mode == PungentStickyNoteOverlayMode.PreviewLocked || State.mode == PungentStickyNoteOverlayMode.EditLocked) &&
                FindNote(State.noteId) == null)
            {
                ResetStateAndNotify();
                return;
            }

            if (!layoutEvent && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (State.IsEditMode)
                    DoneEditing(owner);
                else
                    Close(owner);
                Event.current.Use();
                return;
            }

            PungentStickyNoteOverlayGUI.Draw(State, bounds);
        }

        internal static bool ValidateFloatingOverlay()
        {
            if (!UsesFloatingTray(State.mode))
                return false;

            if ((State.mode == PungentStickyNoteOverlayMode.PreviewLocked || State.mode == PungentStickyNoteOverlayMode.EditLocked) &&
                FindNote(State.noteId) == null)
            {
                Close(State.owner);
                return false;
            }

            return true;
        }

        internal static void DrawFloating(Rect bounds)
        {
            if (!ValidateFloatingOverlay())
                return;

            PungentStickyNoteOverlayGUI.DrawFloating(State, bounds);
        }

        internal static string GetFloatingTitle()
        {
            switch (State.mode)
            {
                case PungentStickyNoteOverlayMode.PreviewLocked:
                    PungentNote previewNote = FindNote(State.noteId);
                    return previewNote == null || string.IsNullOrWhiteSpace(previewNote.title) ? "Sticky Note Preview" : previewNote.title;
                case PungentStickyNoteOverlayMode.EditLocked:
                    PungentNote note = FindNote(State.noteId);
                    return note == null || string.IsNullOrWhiteSpace(note.title) ? "Sticky Note" : note.title;
                case PungentStickyNoteOverlayMode.StackEdit:
                case PungentStickyNoteOverlayMode.StackPreview:
                    return (string.IsNullOrWhiteSpace(State.sourceLabel) ? "Sticky Notes" : State.sourceLabel) + " (" + State.noteIds.Count + ")";
                case PungentStickyNoteOverlayMode.AuthoringLocked:
                    if (State.authoringPreview != null && !string.IsNullOrWhiteSpace(State.authoringPreview.title))
                        return State.authoringPreview.title;
                    if (State.authoringReference != null && !string.IsNullOrWhiteSpace(State.authoringReference.label))
                        return State.authoringReference.label;
                    return "Authoring Preview";
                default:
                    return "Sticky Notes";
            }
        }

        internal static string GetActiveCopyValue()
        {
            if ((State.mode == PungentStickyNoteOverlayMode.PreviewLocked || State.mode == PungentStickyNoteOverlayMode.EditLocked) &&
                !string.IsNullOrWhiteSpace(State.noteId))
                return State.noteId;
            if ((State.mode == PungentStickyNoteOverlayMode.StackEdit || State.mode == PungentStickyNoteOverlayMode.StackPreview) && State.noteIds.Count > 0)
                return string.Join(Environment.NewLine, State.noteIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray());
            if (State.mode == PungentStickyNoteOverlayMode.AuthoringLocked && State.authoringReference != null)
                return State.authoringReference.itemId ?? string.Empty;
            return string.Empty;
        }

        internal static Vector2 GetFloatingPreferredSize(bool collapsed)
        {
            if (collapsed)
                return new Vector2(360f, PungentStickyNoteOverlayTrayWindow.HeaderHeight);

            switch (State.mode)
            {
                case PungentStickyNoteOverlayMode.PreviewLocked:
                    return new Vector2(500f, 430f);
                case PungentStickyNoteOverlayMode.EditLocked:
                    return new Vector2(590f, 640f);
                case PungentStickyNoteOverlayMode.StackEdit:
                case PungentStickyNoteOverlayMode.StackPreview:
                    return new Vector2(460f, 440f);
                case PungentStickyNoteOverlayMode.AuthoringLocked:
                    return new Vector2(720f, 560f);
                default:
                    return new Vector2(420f, 300f);
            }
        }

        internal static void OpenFullBrowserForActive()
        {
            if ((State.mode == PungentStickyNoteOverlayMode.PreviewLocked || State.mode == PungentStickyNoteOverlayMode.EditLocked) &&
                !string.IsNullOrWhiteSpace(State.noteId))
            {
                PungentNotesRoadmapWindow.OpenAndSelect(State.noteId);
                return;
            }

            if (State.mode == PungentStickyNoteOverlayMode.AuthoringLocked &&
                State.authoringReference != null &&
                PungentAuthoringProviderRegistry.TryOpen(State.authoringReference))
                return;

            PungentNotesRoadmapWindow.Open();
        }

        internal static void SetLastCacheStatus(string status)
        {
            _lastCacheStatus = string.IsNullOrWhiteSpace(status) ? "Idle" : status;
        }

        private static bool ShouldShow(Rect sourceRect, string key, bool enabled, bool suppress, float delaySeconds)
        {
            if (!enabled || suppress || Event.current == null || Event.current.type == EventType.Layout)
                return false;

            if (!sourceRect.Contains(Event.current.mousePosition))
            {
                if (string.Equals(_hoverKey, key, StringComparison.OrdinalIgnoreCase))
                    _hoverKey = string.Empty;
                return false;
            }

            if (!string.Equals(_hoverKey, key, StringComparison.OrdinalIgnoreCase))
            {
                _hoverKey = key;
                _hoverStart = EditorApplication.timeSinceStartup;
                return false;
            }

            return EditorApplication.timeSinceStartup - _hoverStart >= Mathf.Max(0.05f, delaySeconds);
        }

        private static bool UsesFloatingTray(PungentStickyNoteOverlayMode mode)
        {
            return mode == PungentStickyNoteOverlayMode.PreviewLocked ||
                   mode == PungentStickyNoteOverlayMode.EditLocked ||
                   mode == PungentStickyNoteOverlayMode.StackEdit ||
                   mode == PungentStickyNoteOverlayMode.StackPreview ||
                   mode == PungentStickyNoteOverlayMode.AuthoringLocked;
        }

        private static bool SameAuthoringReference(PungentAuthoringReference a, PungentAuthoringReference b)
        {
            if (a == null || b == null)
                return false;

            return string.Equals(a.providerId, b.providerId, StringComparison.OrdinalIgnoreCase) &&
                   a.itemKind == b.itemKind &&
                   string.Equals(a.itemId, b.itemId, StringComparison.OrdinalIgnoreCase);
        }

        private static PungentAuthoringPreview ResolvePreview(PungentAuthoringReference reference, PungentAuthoringPreview preview)
        {
            if (preview != null)
                return preview;

            PungentAuthoringPreview resolved;
            if (PungentAuthoringProviderRegistry.TryGetPreview(reference, out resolved) && resolved != null)
                return resolved;

            string title = reference == null || string.IsNullOrWhiteSpace(reference.label) ? "Authoring Item" : reference.label;
            string message = reference == null
                ? "Authoring reference is missing."
                : PungentAuthoringProviderRegistry.MissingProviderMessage(reference);
            return PungentAuthoringPreview.Missing(title, message);
        }

        private static bool PointerStillOwnsHover()
        {
            if (Event.current == null)
                return true;

            Vector2 mouse = Event.current.mousePosition;
            if (State.anchorRect.Contains(mouse) || State.activeRect.Contains(mouse))
                return true;

            return Event.current.type != EventType.MouseMove &&
                   Event.current.type != EventType.Repaint &&
                   Event.current.type != EventType.MouseDown;
        }

        private static void HandleEditorUpdate()
        {
            PungentStickyNoteOverlayTrayWindow.TickFocusMode();

            bool hasOverlayWork = State.mode != PungentStickyNoteOverlayMode.None &&
                                  (State.draftDirty ||
                                   State.checklistCommentsDirty ||
                                   State.checklistFilterPending ||
                                   State.dataSheetFilterPending);
            if (hasOverlayWork)
                NotifyOverlayUpdate();

            if (!State.IsEditMode || !State.draftDirty)
                return;

            if (EditorApplication.timeSinceStartup - State.lastDraftEditTime < DraftAutosaveDelaySeconds)
                return;

            State.CommitDraftIfDirty("Autosaved note.");
        }

        private static void CommitBeforeReload()
        {
            NotifyBeforeOverlayReset();
            State.CommitDraftIfDirty("Saved draft before domain reload.");
        }

        private static void EnsureNoteIndex()
        {
            PungentNoteDatabase database = PungentNoteStorage.Database;
            int count = database.notes == null ? 0 : database.notes.Count;
            string lastSavedUtc = database.lastSavedUtc ?? string.Empty;
            if (_noteById != null && _noteIndexCount == count && string.Equals(_noteIndexLastSavedUtc, lastSavedUtc, StringComparison.Ordinal))
                return;

            _noteById = new Dictionary<string, PungentNote>(StringComparer.OrdinalIgnoreCase);
            if (database.notes != null)
            {
                for (int i = 0; i < database.notes.Count; i++)
                {
                    PungentNote note = database.notes[i];
                    if (note == null || string.IsNullOrWhiteSpace(note.id))
                        continue;
                    _noteById[note.id] = note;
                }
            }

            _noteIndexCount = count;
            _noteIndexLastSavedUtc = lastSavedUtc;
        }

        private static void ResetStateAndNotify()
        {
            NotifyBeforeOverlayReset();
            State.Reset();
        }

        private static void NotifyOverlayUpdate()
        {
            Action<PungentStickyNoteOverlayState> handler = OverlayUpdate;
            if (handler == null)
                return;

            try
            {
                handler(State);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void NotifyBeforeOverlayReset()
        {
            Action<PungentStickyNoteOverlayState> handler = BeforeOverlayReset;
            if (handler == null)
                return;

            try
            {
                handler(State);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
#endif
}
