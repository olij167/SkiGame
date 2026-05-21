using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Authoring;
    using PungentFunk.Utilities.Editor.Authoring;
    using UnityEngine;

    public sealed class PungentStickyNoteOverlayRequest
    {
        public PungentStickyNoteOverlayMode mode;
        public PungentStickyNoteOverlayOwner owner;
        public Rect anchorRect;
        public string sourceLabel = string.Empty;
        public string noteId = string.Empty;
        public List<string> noteIds = new List<string>();
        public string infoKey = string.Empty;
        public string infoTitle = string.Empty;
        public string infoBody = string.Empty;
        public string infoDetail = string.Empty;
        public PungentAuthoringReference authoringReference;
        public PungentAuthoringPreview authoringPreview;

        public static PungentStickyNoteOverlayRequest ForNote(
            PungentStickyNoteOverlayMode mode,
            string noteId,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            return new PungentStickyNoteOverlayRequest
            {
                mode = mode,
                noteId = noteId ?? string.Empty,
                anchorRect = anchorRect,
                owner = owner,
                sourceLabel = sourceLabel ?? string.Empty
            };
        }

        public static PungentStickyNoteOverlayRequest ForStack(
            PungentStickyNoteOverlayMode mode,
            IEnumerable<PungentNote> notes,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            return new PungentStickyNoteOverlayRequest
            {
                mode = mode,
                noteIds = notes == null
                    ? new List<string>()
                    : notes.Where(note => note != null && !string.IsNullOrWhiteSpace(note.id))
                        .Select(note => note.id)
                        .Distinct(System.StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                anchorRect = anchorRect,
                owner = owner,
                sourceLabel = sourceLabel ?? string.Empty
            };
        }

        public static PungentStickyNoteOverlayRequest ForInfo(
            string key,
            string title,
            string body,
            string detail,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            return new PungentStickyNoteOverlayRequest
            {
                mode = PungentStickyNoteOverlayMode.InfoPreview,
                infoKey = key ?? string.Empty,
                infoTitle = title ?? string.Empty,
                infoBody = body ?? string.Empty,
                infoDetail = detail ?? string.Empty,
                anchorRect = anchorRect,
                owner = owner,
                sourceLabel = sourceLabel ?? string.Empty
            };
        }

        public static PungentStickyNoteOverlayRequest ForAuthoring(
            PungentStickyNoteOverlayMode mode,
            PungentAuthoringReference reference,
            PungentAuthoringPreview preview,
            Rect anchorRect,
            PungentStickyNoteOverlayOwner owner,
            string sourceLabel)
        {
            return new PungentStickyNoteOverlayRequest
            {
                mode = mode,
                authoringReference = reference,
                authoringPreview = preview,
                anchorRect = anchorRect,
                owner = owner,
                sourceLabel = sourceLabel ?? string.Empty
            };
        }
    }
#endif
}
