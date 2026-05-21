using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Authoring;
    using PungentFunk.Utilities.Editor.Authoring;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public static class PungentStickyNoteOverlayGUI
    {
        private sealed class CachedPreview
        {
            public string fingerprint;
            public string bodyPreview;
            public string tags;
            public string linkLabel;
            public int targetCount;
            public int tokenCount;
        }

        private static readonly Dictionary<string, CachedPreview> PreviewCache = new Dictionary<string, CachedPreview>(StringComparer.OrdinalIgnoreCase);
        private const int MaxPreviewCacheEntries = 96;
        private const float OverlayHeaderHeight = 30f;
        private const float OverlayFooterHeight = 32f;
        private static GUIStyle _overlayTitleStyle;
        private static GUIStyle _overlayDetailStyle;

        public static void Draw(PungentStickyNoteOverlayState state, Rect bounds)
        {
            if (state == null || state.mode == PungentStickyNoteOverlayMode.None)
                return;

            Rect safeBounds = bounds.width > 24f && bounds.height > 24f
                ? bounds
                : new Rect(8f, 8f, Mathf.Max(260f, EditorGUIUtility.currentViewWidth - 16f), 520f);

            Rect rect = CardRectFor(state, safeBounds);
            state.activeRect = rect;
            DrawAtRect(rect, state);
        }

        internal static void DrawFloating(PungentStickyNoteOverlayState state, Rect rect)
        {
            if (state == null || state.mode == PungentStickyNoteOverlayMode.None)
                return;

            Rect safeRect = rect.width > 24f && rect.height > 24f
                ? rect
                : new Rect(0f, 0f, 360f, 240f);
            state.activeRect = safeRect;
            DrawAtRect(safeRect, state);
        }

        private static void DrawAtRect(Rect rect, PungentStickyNoteOverlayState state)
        {
            switch (state.mode)
            {
                case PungentStickyNoteOverlayMode.InfoPreview:
                    DrawInfoOverlay(rect, state);
                    break;
                case PungentStickyNoteOverlayMode.AuthoringPreview:
                case PungentStickyNoteOverlayMode.AuthoringLocked:
                    DrawAuthoringOverlay(rect, state);
                    break;
                case PungentStickyNoteOverlayMode.StackPreview:
                case PungentStickyNoteOverlayMode.StackEdit:
                    DrawStackOverlay(rect, state);
                    break;
                case PungentStickyNoteOverlayMode.PreviewLocked:
                case PungentStickyNoteOverlayMode.EditLocked:
                    DrawEditOverlay(rect, state);
                    break;
                case PungentStickyNoteOverlayMode.HoverPreview:
                    DrawNotePreviewOverlay(rect, state);
                    break;
            }
        }

        private static Rect CardRectFor(PungentStickyNoteOverlayState state, Rect bounds)
        {
            bool editing = state.mode == PungentStickyNoteOverlayMode.PreviewLocked || state.mode == PungentStickyNoteOverlayMode.EditLocked || state.mode == PungentStickyNoteOverlayMode.StackEdit;
            bool stack = state.mode == PungentStickyNoteOverlayMode.StackPreview || state.mode == PungentStickyNoteOverlayMode.StackEdit;
            bool info = state.mode == PungentStickyNoteOverlayMode.InfoPreview;
            bool authoring = state.mode == PungentStickyNoteOverlayMode.AuthoringPreview || state.mode == PungentStickyNoteOverlayMode.AuthoringLocked;
            float preferredWidth = editing ? 560f : stack ? 420f : authoring ? 440f : info ? 330f : 380f;
            float preferredHeight = editing ? 520f : stack ? 360f : authoring ? (state.mode == PungentStickyNoteOverlayMode.AuthoringLocked ? 268f : 212f) : info ? 118f : 206f;
            float width = Mathf.Min(preferredWidth, Mathf.Max(240f, bounds.width - 16f));
            float height = Mathf.Min(preferredHeight, Mathf.Max(110f, bounds.height - 16f));

            Rect source = state.anchorRect;
            float x = source.width > 1f ? source.xMax + 10f : bounds.xMax - width - 10f;
            if (x + width > bounds.xMax - 8f)
                x = source.width > 1f ? source.x - width - 10f : bounds.xMax - width - 10f;
            x = Mathf.Clamp(x, bounds.xMin + 8f, Mathf.Max(bounds.xMin + 8f, bounds.xMax - width - 8f));

            float y = source.height > 1f ? source.y : bounds.yMin + 58f;
            y = Mathf.Clamp(y, bounds.yMin + 8f, Mathf.Max(bounds.yMin + 8f, bounds.yMax - height - 8f));
            return new Rect(x, y, width, height);
        }

        private static void DrawNotePreviewOverlay(Rect rect, PungentStickyNoteOverlayState state)
        {
            PungentNote note = PungentStickyNoteOverlayController.FindNote(state.noteId);
            if (note == null)
                return;

            CachedPreview preview = GetPreview(note);
            Rect bodyRect;
            Rect footerRect;
            DrawOverlayChrome(
                rect,
                PungentNoteGUI.PriorityTint(note.priority),
                string.IsNullOrWhiteSpace(note.title) ? "Untitled Note" : note.title,
                note.kind + " | " + note.status + " | " + note.priority,
                true,
                out bodyRect,
                out footerRect);

            GUILayout.BeginArea(bodyRect);
            try
            {
                EditorGUILayout.LabelField("Updated " + ShortDate(note.updatedUtc), UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(state.sourceLabel))
                    EditorGUILayout.LabelField(state.sourceLabel, UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(preview.bodyPreview))
                    EditorGUILayout.LabelField(preview.bodyPreview, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinHeight(44f));
                if (!string.IsNullOrWhiteSpace(preview.tags))
                    EditorGUILayout.LabelField("Tags: " + preview.tags, UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(preview.linkLabel))
                    EditorGUILayout.LabelField("Link: " + preview.linkLabel, UtilityWindowTheme.PathLabelStyle);
            }
            finally
            {
                GUILayout.EndArea();
            }

            GUILayout.BeginArea(footerRect);
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(preview.targetCount + " targets | " + preview.tokenCount + " tokens", UtilityWindowTheme.PathLabelStyle);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(54f)))
                        PungentStickyNoteOverlayController.OpenEdit(note.id, state.anchorRect, state.owner, state.sourceLabel);
                    if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(60f)))
                        EditorGUIUtility.systemCopyBuffer = note.id;
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawInfoOverlay(Rect rect, PungentStickyNoteOverlayState state)
        {
            Rect bodyRect;
            Rect footerRect;
            DrawOverlayChrome(rect, UtilityWindowTheme.Neutral, state.infoTitle, state.infoDetail, false, out bodyRect, out footerRect);
            GUILayout.BeginArea(bodyRect);
            try
            {
                if (!string.IsNullOrWhiteSpace(state.infoBody))
                    EditorGUILayout.LabelField(TrimPreview(state.infoBody, 260), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinHeight(42f));
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawStackOverlay(Rect rect, PungentStickyNoteOverlayState state)
        {
            Rect bodyRect;
            Rect footerRect;
            DrawOverlayChrome(
                rect,
                UtilityWindowTheme.Teal,
                string.IsNullOrWhiteSpace(state.sourceLabel) ? "Sticky Notes" : state.sourceLabel,
                state.noteIds.Count + " linked notes",
                true,
                out bodyRect,
                out footerRect);

            GUILayout.BeginArea(bodyRect);
            try
            {
                state.stackScroll = EditorGUILayout.BeginScrollView(state.stackScroll, false, true, GUILayout.ExpandHeight(true));
                foreach (string id in state.noteIds.ToList())
                {
                    PungentNote note = PungentStickyNoteOverlayController.FindNote(id);
                    if (note == null)
                        continue;

                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(PungentNoteGUI.PriorityTint(note.priority), 0.12f, 0.05f, 4, 2)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(note.title, UtilityWindowTheme.CardLabelStyle))
                                PungentStickyNoteOverlayController.OpenEdit(note.id, state.anchorRect, state.owner, state.sourceLabel);
                            UtilityWindowTheme.CountPill(note.status.ToString(), PungentNoteGUI.StatusTint(note.status), 92f);
                        }
                        EditorGUILayout.LabelField(TrimPreview(note.body, 150), UtilityWindowTheme.MutedMiniLabelStyle);
                        PungentNoteGUI.DrawTags(note.tags);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            finally
            {
                GUILayout.EndArea();
            }

            GUILayout.BeginArea(footerRect);
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open Browser", EditorStyles.miniButton, GUILayout.Width(92f)))
                        PungentNotesRoadmapWindow.Open();
                    if (GUILayout.Button("Done", EditorStyles.miniButton, GUILayout.Width(48f)))
                        PungentStickyNoteOverlayController.Close(state.owner);
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawEditOverlay(Rect rect, PungentStickyNoteOverlayState state)
        {
            PungentNote note = PungentStickyNoteOverlayController.FindNote(state.noteId);
            Rect bodyRect;
            Rect footerRect;
            DrawOverlayChrome(
                rect,
                note == null ? UtilityWindowTheme.Amber : PungentNoteGUI.PriorityTint(note.priority),
                "Sticky Note",
                note == null ? "Missing target" : "Updated " + ShortDate(note.updatedUtc),
                false,
                out bodyRect,
                out footerRect);

            GUILayout.BeginArea(bodyRect);
            try
            {
                if (note == null)
                {
                    EditorGUILayout.LabelField("Sticky Note Missing", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("The selected sticky note could not be found.", UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Done", EditorStyles.miniButton, GUILayout.Width(56f)))
                        PungentStickyNoteOverlayController.Close(state.owner);
                }
                else
                {
                    PungentStickyNoteQuickEditorGUI.Draw(state, note);
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawAuthoringOverlay(Rect rect, PungentStickyNoteOverlayState state)
        {
            PungentAuthoringReference reference = state.authoringReference;
            PungentAuthoringPreview preview = state.authoringPreview ?? PungentAuthoringPreview.Missing("Authoring Item", "Preview data is not available.");
            bool locked = state.mode == PungentStickyNoteOverlayMode.AuthoringLocked;
            Color tint = preview.missing || preview.warning ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral;
            Rect bodyRect;
            Rect footerRect;
            DrawOverlayChrome(
                rect,
                tint,
                OverlayTitle(preview, reference),
                AuthoringDetail(preview, reference),
                true,
                out bodyRect,
                out footerRect);

            GUILayout.BeginArea(bodyRect);
            try
            {
                bool drewConcretePreview = locked &&
                                           PungentAuthoringOverlayPreviewRegistry.TryDraw(reference, preview, state);
                if (!drewConcretePreview)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (!string.IsNullOrWhiteSpace(state.sourceLabel))
                            EditorGUILayout.LabelField(state.sourceLabel, ItalicMiniLabel(), GUILayout.MaxWidth(128f));
                        if (preview.targetCount > 0)
                            EditorGUILayout.LabelField(preview.targetCount + " targets", UtilityWindowTheme.PathLabelStyle);
                    }

                    string body = !string.IsNullOrWhiteSpace(preview.bodyPreview) ? preview.bodyPreview : preview.subtitle;
                    if (!string.IsNullOrWhiteSpace(body))
                        EditorGUILayout.LabelField(TrimPreview(body, locked ? 420 : 280), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinHeight(locked ? 58f : 42f));

                    if (preview.tags != null && preview.tags.Count > 0)
                        EditorGUILayout.LabelField("#" + string.Join(", #", preview.tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Take(8).ToArray()), UtilityWindowTheme.PathLabelStyle);

                    if (!string.IsNullOrWhiteSpace(preview.warningLabel))
                        EditorGUILayout.LabelField(preview.warningLabel, UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
            finally
            {
                GUILayout.EndArea();
            }

            GUILayout.BeginArea(footerRect);
            try
            {
                bool compactFooter = footerRect.width < 410f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!compactFooter && !string.IsNullOrWhiteSpace(state.authoringStatus))
                        EditorGUILayout.LabelField(FitText(state.authoringStatus, UtilityWindowTheme.MutedMiniLabelStyle, 120f), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(120f));

                    using (new EditorGUI.DisabledScope(!CanOpen(reference)))
                    {
                        if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(54f)))
                            state.authoringStatus = PungentAuthoringProviderRegistry.TryOpen(reference) ? "Opened item." : "No provider could open this item.";
                    }

                    using (new EditorGUI.DisabledScope(!CanEdit(reference)))
                    {
                        if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(48f)))
                            state.authoringStatus = PungentAuthoringProviderRegistry.TryEdit(reference) ? "Opened full editor." : "No provider could edit this item.";
                    }

                    if (compactFooter)
                    {
                        if (GUILayout.Button(new GUIContent("Actions", "More authoring item actions."), EditorStyles.miniButton, GUILayout.Width(58f)))
                            ShowAuthoringOverlayActionsMenu(reference, preview, state);
                    }
                    else
                    {
                        if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(64f)))
                            CopyAuthoringReference(reference, state);

                        if (GUILayout.Button("Linked Note", EditorStyles.miniButton, GUILayout.Width(86f)))
                        {
                            CreateLinkedNote(reference, preview, state);
                            GUIUtility.ExitGUI();
                        }
                    }

                    GUILayout.FlexibleSpace();
                    if (!compactFooter)
                        EditorGUILayout.LabelField(ReferenceId(reference), UtilityWindowTheme.PathLabelStyle, GUILayout.MaxWidth(140f));
                    if (locked && GUILayout.Button(new GUIContent("Close", "Close this preview tray."), EditorStyles.miniButton, GUILayout.Width(54f)))
                    {
                        PungentStickyNoteOverlayController.Close(state.owner);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void ShowAuthoringOverlayActionsMenu(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy ID"), false, () => CopyAuthoringReference(reference, state));
            menu.AddItem(new GUIContent("Create Linked Note"), false, () => CreateLinkedNote(reference, preview, state));
            menu.AddSeparator(string.Empty);
            if (CanOpen(reference))
                menu.AddItem(new GUIContent("Open"), false, () => state.authoringStatus = PungentAuthoringProviderRegistry.TryOpen(reference) ? "Opened item." : "No provider could open this item.");
            else
                menu.AddDisabledItem(new GUIContent("Open"));
            if (CanEdit(reference))
                menu.AddItem(new GUIContent("Edit"), false, () => state.authoringStatus = PungentAuthoringProviderRegistry.TryEdit(reference) ? "Opened full editor." : "No provider could edit this item.");
            else
                menu.AddDisabledItem(new GUIContent("Edit"));
            if (!string.IsNullOrWhiteSpace(ReferenceId(reference)))
                menu.AddDisabledItem(new GUIContent("Reference: " + ReferenceId(reference)));
            menu.ShowAsContext();
        }

        private static void DrawOverlayChrome(Rect rect, Color accent, string title, string detail, bool hasFooter, out Rect bodyRect, out Rect footerRect)
        {
            EnsureOverlayStyles();
            Color background = EditorGUIUtility.isProSkin
                ? new Color(0.115f, 0.12f, 0.13f, 0.985f)
                : new Color(0.92f, 0.93f, 0.94f, 0.995f);
            Color header = EditorGUIUtility.isProSkin
                ? new Color(0.155f, 0.16f, 0.17f, 0.995f)
                : new Color(0.82f, 0.84f, 0.86f, 0.995f);
            Color border = EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.62f)
                : new Color(0f, 0f, 0f, 0.22f);

            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.26f : 0.14f));
            EditorGUI.DrawRect(rect, background);
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, OverlayHeaderHeight);
            EditorGUI.DrawRect(headerRect, header);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), new Color(accent.r, accent.g, accent.b, 0.95f));
            DrawRectBorder(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x, headerRect.yMax - 1f, rect.width, 1f), border);

            Rect titleRect = new Rect(rect.x + 12f, rect.y + 3f, Mathf.Max(40f, rect.width - 24f), 16f);
            Rect detailRect = new Rect(rect.x + 12f, rect.y + 17f, Mathf.Max(40f, rect.width - 24f), 12f);
            GUI.Label(titleRect, FitText(title, _overlayTitleStyle, titleRect.width), _overlayTitleStyle);
            if (!string.IsNullOrWhiteSpace(detail))
                GUI.Label(detailRect, FitText(detail, _overlayDetailStyle, detailRect.width), _overlayDetailStyle);

            float footerHeight = hasFooter ? OverlayFooterHeight : 0f;
            bodyRect = new Rect(
                rect.x + 10f,
                rect.y + OverlayHeaderHeight + 8f,
                Mathf.Max(10f, rect.width - 20f),
                Mathf.Max(18f, rect.height - OverlayHeaderHeight - footerHeight - 18f));
            footerRect = hasFooter
                ? new Rect(rect.x + 10f, rect.yMax - OverlayFooterHeight - 7f, Mathf.Max(10f, rect.width - 20f), OverlayFooterHeight)
                : Rect.zero;
        }

        private static void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static void EnsureOverlayStyles()
        {
            if (_overlayTitleStyle != null)
                return;

            _overlayTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = UtilityWindowTheme.TitleText }
            };
            _overlayDetailStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = UtilityWindowTheme.MutedText }
            };
        }

        private static Rect Inner(Rect rect)
        {
            return new Rect(rect.x + 10f, rect.y + 8f, Mathf.Max(10f, rect.width - 20f), Mathf.Max(10f, rect.height - 16f));
        }

        private static CachedPreview GetPreview(PungentNote note)
        {
            string cacheKey = string.IsNullOrWhiteSpace(note.id) ? note.title ?? string.Empty : note.id;
            string fingerprint = string.Join("|",
                note.id ?? string.Empty,
                note.updatedUtc ?? string.Empty,
                ContentFingerprint(note.title),
                ContentFingerprint(note.body),
                note.targets == null ? "0" : note.targets.Count.ToString(),
                note.tags == null ? "0" : note.tags.Count.ToString(),
                note.linkedUtilityId ?? string.Empty,
                note.linkedFutureUtilityId ?? string.Empty);

            if (PreviewCache.TryGetValue(cacheKey, out CachedPreview cached) && string.Equals(cached.fingerprint, fingerprint, StringComparison.Ordinal))
            {
                PungentStickyNoteOverlayController.SetLastCacheStatus("hit");
                return cached;
            }

            cached = new CachedPreview
            {
                fingerprint = fingerprint,
                bodyPreview = TrimPreview(note.body, 260),
                tags = note.tags == null ? string.Empty : string.Join(", ", note.tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Take(8).ToArray()),
                linkLabel = GetLinkLabel(note),
                targetCount = note.targets == null ? 0 : note.targets.Count(target => target != null),
                tokenCount = PungentTokenParser.Parse(note.body ?? string.Empty).Count
            };
            if (PreviewCache.Count >= MaxPreviewCacheEntries)
                PreviewCache.Remove(PreviewCache.Keys.First());
            PreviewCache[cacheKey] = cached;
            PungentStickyNoteOverlayController.SetLastCacheStatus("miss");
            return cached;
        }

        private static bool CanOpen(PungentAuthoringReference reference)
        {
            return TryGetLauncher(reference, true, out _);
        }

        private static bool CanEdit(PungentAuthoringReference reference)
        {
            return TryGetLauncher(reference, false, out _);
        }

        private static bool TryGetLauncher(PungentAuthoringReference reference, bool open, out string reason)
        {
            reason = string.Empty;
            if (reference == null)
            {
                reason = "Authoring reference is missing.";
                return false;
            }

            IEnumerable<IPungentAuthoringProvider> providers;
            if (!string.IsNullOrWhiteSpace(reference.providerId))
                providers = new[] { PungentAuthoringProviderRegistry.FindProvider(reference.providerId) }.Where(provider => provider != null);
            else
                providers = PungentAuthoringProviderRegistry.GetProvidersForKind(reference.itemKind);

            foreach (IPungentAuthoringProvider provider in providers)
            {
                if (provider is IPungentAuthoringEditorLauncher launcher)
                {
                    bool ok = open ? launcher.CanOpen(reference, out reason) : launcher.CanEdit(reference, out reason);
                    if (ok)
                        return true;
                }
            }

            reason = string.IsNullOrWhiteSpace(reason) ? PungentAuthoringProviderRegistry.MissingProviderMessage(reference) : reason;
            return false;
        }

        private static void CopyAuthoringReference(PungentAuthoringReference reference, PungentStickyNoteOverlayState state)
        {
            if (reference == null)
                return;

            string copiedValue;
            string error;
            if (PungentAuthoringProviderRegistry.TryCopy(reference, out copiedValue, out error))
            {
                EditorGUIUtility.systemCopyBuffer = string.IsNullOrWhiteSpace(copiedValue) ? reference.itemId ?? string.Empty : copiedValue;
                state.authoringStatus = "Copied item reference.";
                return;
            }

            EditorGUIUtility.systemCopyBuffer = reference.itemId ?? string.Empty;
            state.authoringStatus = string.IsNullOrWhiteSpace(error) ? "Copied fallback item ID." : error;
        }

        private static void CreateLinkedNote(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state)
        {
            if (reference == null)
                return;

            string title = OverlayTitle(preview, reference);
            PungentNote note = PungentNoteStorage.Database.CreateNote("Note: " + title, PungentNoteKind.ProjectNote);
            note.body = "Linked authoring item: " + title +
                        "\nKind: " + reference.KindLabel +
                        "\nProvider: " + (reference.providerId ?? string.Empty) +
                        "\nID: " + (reference.itemId ?? string.Empty);
            note.stableKey = "authoring:" + (reference.providerId ?? string.Empty) + ":" + reference.itemKind + ":" + (reference.itemId ?? string.Empty);
            if (note.targets == null)
                note.targets = new List<PungentNoteTargetLink>();
            note.targets.Add(new PungentNoteTargetLink
            {
                type = PungentNoteTargetType.ExternalPath,
                label = reference.KindLabel + ": " + title,
                externalPathOrUrl = "authoring://" + (reference.providerId ?? string.Empty) + "/" + reference.itemKind + "/" + (reference.itemId ?? string.Empty)
            });
            PungentNoteStorage.Save();
            PungentStickyNoteOverlayController.OpenEdit(note.id, state.anchorRect, state.owner, "Linked Note");
        }

        private static string OverlayTitle(PungentAuthoringPreview preview, PungentAuthoringReference reference)
        {
            if (preview != null && !string.IsNullOrWhiteSpace(preview.title))
                return preview.title;
            if (reference != null && !string.IsNullOrWhiteSpace(reference.label))
                return reference.label;
            if (reference != null && !string.IsNullOrWhiteSpace(reference.itemId))
                return reference.itemId;
            return "Authoring Item";
        }

        private static string AuthoringDetail(PungentAuthoringPreview preview, PungentAuthoringReference reference)
        {
            List<string> parts = new List<string>();
            if (preview != null && !string.IsNullOrWhiteSpace(preview.kindLabel))
                parts.Add(preview.kindLabel);
            else if (reference != null)
                parts.Add(reference.KindLabel);
            if (preview != null && !string.IsNullOrWhiteSpace(preview.statusLabel))
                parts.Add(preview.statusLabel);
            if (preview != null && preview.targetCount > 0)
                parts.Add(preview.targetCount + " targets");
            return string.Join("  |  ", parts.ToArray());
        }

        private static string ReferenceId(PungentAuthoringReference reference)
        {
            return reference == null || string.IsNullOrWhiteSpace(reference.itemId) ? string.Empty : reference.itemId;
        }

        private static GUIStyle ItalicMiniLabel()
        {
            return new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle)
            {
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
        }

        private static string GetLinkLabel(PungentNote note)
        {
            if (!string.IsNullOrWhiteSpace(note.linkedUtilityId))
                return PungentNoteGUI.UtilityDisplayName(note.linkedUtilityId);
            if (!string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
                return PungentNoteGUI.FutureUtilityDisplayName(note.linkedFutureUtilityId);
            return string.Empty;
        }

        private static string TrimPreview(string text, int max)
        {
            string clean = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length > max ? clean.Substring(0, Mathf.Max(0, max - 3)) + "..." : clean;
        }

        private static string FitText(string value, GUIStyle style, float width)
        {
            string text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (string.IsNullOrEmpty(text) || style == null || width <= 16f || style.CalcSize(new GUIContent(text)).x <= width)
                return text;

            const string suffix = "...";
            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid).TrimEnd() + suffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low <= 0 ? suffix : text.Substring(0, low).TrimEnd() + suffix;
        }

        private static string ContentFingerprint(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "0:0";

            unchecked
            {
                int hash = 37;
                int stride = Mathf.Max(1, value.Length / 96);
                for (int i = 0; i < value.Length; i += stride)
                    hash = hash * 31 + value[i];
                hash = hash * 31 + value[value.Length - 1];
                return value.Length + ":" + hash;
            }
        }

        private static string ShortDate(string utc)
        {
            return DateTime.TryParse(utc, out DateTime parsed) ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "unknown";
        }
    }
#endif
}
