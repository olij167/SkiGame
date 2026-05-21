using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public static class PungentNoteGUI
    {
        private static readonly Regex InlineLinkRegex = new Regex(@"(\{[^{}]+\}|@utility:[A-Za-z0-9_-]+|@future:[A-Za-z0-9_-]+|@note:[A-Za-z0-9_-]+|#[A-Za-z0-9_-]+)", RegexOptions.Compiled);
        private static string _inlineLinkCacheKey = string.Empty;
        private static List<string> _inlineLinkCache = new List<string>();

        public static Color PriorityTint(PungentNotePriority priority)
        {
            switch (priority)
            {
                case PungentNotePriority.Crucial: return UtilityWindowTheme.Red;
                case PungentNotePriority.Important: return UtilityWindowTheme.Amber;
                case PungentNotePriority.FurtherConsideration: return UtilityWindowTheme.Cyan;
                case PungentNotePriority.OutOfScope: return UtilityWindowTheme.Neutral;
                case PungentNotePriority.Low: return UtilityWindowTheme.Teal;
                default: return UtilityWindowTheme.Green;
            }
        }

        public static Color StatusTint(PungentNoteStatus status)
        {
            switch (status)
            {
                case PungentNoteStatus.Complete: return UtilityWindowTheme.Green;
                case PungentNoteStatus.InProgress: return UtilityWindowTheme.Cyan;
                case PungentNoteStatus.Blocked: return UtilityWindowTheme.Red;
                case PungentNoteStatus.NeedsReview: return UtilityWindowTheme.Amber;
                case PungentNoteStatus.OutOfScope: return UtilityWindowTheme.Neutral;
                default: return UtilityWindowTheme.Blue;
            }
        }

        public static void DrawNotePills(PungentNote note)
        {
            if (note == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(note.kind.ToString(), UtilityWindowTheme.Blue, 108f);
                UtilityWindowTheme.CountPill(note.status.ToString(), StatusTint(note.status), 110f);
                UtilityWindowTheme.CountPill(note.priority.ToString(), PriorityTint(note.priority), 130f);
                if (note.archived)
                    UtilityWindowTheme.CountPill("Archived", UtilityWindowTheme.Neutral, 78f);
                if (note.developerOnly)
                    UtilityWindowTheme.CountPill("Developer", UtilityWindowTheme.Purple, 86f);
                GUILayout.FlexibleSpace();
            }
        }

        public static string DrawTagField(string label, List<string> tags)
        {
            string current = tags == null ? string.Empty : string.Join(", ", tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray());
            return EditorGUILayout.TextField(label, current);
        }

        public static List<string> ParseTags(string text)
        {
            return (text ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().TrimStart('#'))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void DrawTags(List<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return;

            int visibleCount = Mathf.Min(tags.Count, 4);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < visibleCount; i++)
                {
                    string tag = tags[i];
                    UtilityWindowTheme.CountPill(
                        "#" + tag,
                        UtilityWindowTheme.Teal,
                        Mathf.Clamp(34f + tag.Length * 7f, 54f, 120f));
                }

                if (tags.Count > visibleCount)
                    UtilityWindowTheme.CountPill("+" + (tags.Count - visibleCount), UtilityWindowTheme.Neutral, 42f);

                GUILayout.FlexibleSpace();
            }
        }

        public static void DrawInlineLinkPills(string body)
        {
            List<string> links = GetInlineLinks(body);
            if (links.Count == 0)
                return;

            EditorGUILayout.LabelField("Detected inline links", UtilityWindowTheme.MutedMiniLabelStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < links.Count; i++)
                    UtilityWindowTheme.CountPill(links[i], UtilityWindowTheme.Cyan, Mathf.Clamp(38f + links[i].Length * 6f, 70f, 160f));
                GUILayout.FlexibleSpace();
            }
        }

        private static List<string> GetInlineLinks(string body)
        {
            string key = ContentFingerprint(body);
            if (string.Equals(_inlineLinkCacheKey, key, StringComparison.Ordinal))
                return _inlineLinkCache;

            _inlineLinkCache = InlineLinkRegex
                .Matches(body ?? string.Empty)
                .Cast<Match>()
                .Select(m => m.Value)
                .Distinct()
                .Take(12)
                .ToList();
            _inlineLinkCacheKey = key;
            return _inlineLinkCache;
        }

        private static string ContentFingerprint(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "0:0";

            unchecked
            {
                int hash = 31;
                int stride = Mathf.Max(1, value.Length / 96);
                for (int i = 0; i < value.Length; i += stride)
                    hash = hash * 31 + value[i];
                hash = hash * 31 + value[value.Length - 1];
                return value.Length + ":" + hash;
            }
        }

        public static string UtilityDisplayName(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return string.Empty;

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            return descriptor == null ? utilityId : descriptor.DisplayName;
        }

        public static string FutureUtilityDisplayName(string futureUtilityId)
        {
            if (string.IsNullOrWhiteSpace(futureUtilityId))
                return string.Empty;

            PungentFutureUtilityRecord record = PungentNoteStorage.Database.futureUtilities.FirstOrDefault(f => f != null && string.Equals(f.id, futureUtilityId, StringComparison.OrdinalIgnoreCase));
            return record == null ? futureUtilityId : record.displayName;
        }
    }

    public static class PungentNoteHoverPreview
    {
        public static string LastCacheStatus => PungentStickyNoteOverlayController.LastCacheStatus;

        public static bool IsPointerOverActiveOverlay()
        {
            return PungentStickyNoteOverlayController.IsPointerOverActiveOverlay();
        }

        public static void DrawNoteCardIfHovered(
            Rect sourceRect,
            PungentNote note,
            string sourceLabel,
            bool enabled,
            bool suppress,
            float delaySeconds,
            Action<PungentNote> openAction = null)
        {
            RequestNoteCardIfHovered(sourceRect, note, sourceLabel, enabled, suppress, delaySeconds);
            DrawPendingHoverOverlay(new Rect(8f, 8f, Mathf.Max(260f, EditorGUIUtility.currentViewWidth - 16f), 520f), openAction);
        }

        public static void RequestNoteCardIfHovered(
            Rect sourceRect,
            PungentNote note,
            string sourceLabel,
            bool enabled,
            bool suppress,
            float delaySeconds)
        {
            PungentStickyNoteOverlayController.RequestHoverPreview(
                note,
                sourceRect,
                PungentStickyNoteOverlayOwner.BrowserWindow,
                sourceLabel,
                enabled,
                suppress,
                delaySeconds);
        }

        public static void DrawInfoCardIfHovered(
            Rect sourceRect,
            string key,
            string title,
            string body,
            string detail,
            bool enabled,
            bool suppress,
            float delaySeconds)
        {
            RequestInfoCardIfHovered(sourceRect, key, title, body, detail, enabled, suppress, delaySeconds);
            DrawPendingHoverOverlay(new Rect(8f, 8f, Mathf.Max(260f, EditorGUIUtility.currentViewWidth - 16f), 520f), null);
        }

        public static void RequestInfoCardIfHovered(
            Rect sourceRect,
            string key,
            string title,
            string body,
            string detail,
            bool enabled,
            bool suppress,
            float delaySeconds)
        {
            PungentStickyNoteOverlayController.RequestInfoPreview(
                sourceRect,
                key,
                title,
                body,
                detail,
                PungentStickyNoteOverlayOwner.BrowserWindow,
                string.Empty,
                enabled,
                suppress,
                delaySeconds);
        }

        public static void DrawPendingHoverOverlay(Rect bounds, Action<PungentNote> openAction = null)
        {
            PungentStickyNoteOverlayController.Draw(PungentStickyNoteOverlayOwner.BrowserWindow, bounds);
        }
    }
#endif
}
