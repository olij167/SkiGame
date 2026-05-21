using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Core.Help;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public static class PungentStickyNoteQuickEditorGUI
    {
        private const double InlinePreviewRefreshDelaySeconds = 0.35d;

        private static GUIStyle _titleStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _previewStyle;
        private static string _pendingDocumentationLinkId = string.Empty;
        private static readonly StickyNoteDraftMetrics DraftMetrics = new StickyNoteDraftMetrics();
        private static readonly PopupOptionCache UtilityOptions = new PopupOptionCache();
        private static readonly PopupOptionCache FutureUtilityOptions = new PopupOptionCache();
        private static readonly PopupOptionCache DocumentationLinkOptions = new PopupOptionCache();

        private sealed class StickyNoteDraftMetrics
        {
            public string noteId = string.Empty;
            public int bodyHash = int.MinValue;
            public int wordCount;
            public List<PungentParsedToken> tokens = new List<PungentParsedToken>();
            public bool tokensFresh;
            public double lastBodyChangeTime;
        }

        private sealed class PopupOptionCache
        {
            public string key = string.Empty;
            public string[] ids = new string[0];
            public string[] labels = new string[0];
        }

        public static void Draw(PungentStickyNoteOverlayState state, PungentNote note)
        {
            if (state == null || note == null)
                return;

            EnsureStyles();
            if (!string.Equals(state.draftNoteId, note.id, StringComparison.OrdinalIgnoreCase))
                state.LoadDraft(note);

            bool supportRequest = PungentSupportRequestBridge.IsSupportRequest(note);
            bool sentRequest = supportRequest && PungentSupportRequestBridge.IsSent(note);
            bool readOnly = state.mode == PungentStickyNoteOverlayMode.PreviewLocked || note.locked || sentRequest;
            DrawTitleRow(state, note, readOnly);
            if (readOnly && !(PungentSupportRequestBridge.IsSupportRequest(note) && PungentSupportRequestBridge.IsSent(note)))
                state.noteOverlayTab = 0;
            else
                DrawOverlayTabs(state);

            if (state.noteOverlayTab == 0)
                DrawNoteTab(state, note, readOnly);
            else if (!supportRequest)
                DrawDetailsTab(state, note);
            else
                DrawDetailsTab(state, note, readOnly);
        }

        private static void DrawTitleRow(PungentStickyNoteOverlayState state, PungentNote note, bool readOnly)
        {
            float width = OverlayWidth(state);
            bool compact = width < 430f;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (readOnly)
                {
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(state.draftTitle) ? "Untitled Note" : state.draftTitle, _titleStyle, GUILayout.MinHeight(30f), GUILayout.MinWidth(80f));
                }
                else
                {
                    GUI.SetNextControlName("PungentStickyNoteOverlayTitle");
                    EditorGUI.BeginChangeCheck();
                    string nextTitle = EditorGUILayout.TextField(state.draftTitle, _titleStyle, GUILayout.MinHeight(30f), GUILayout.MinWidth(80f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        state.draftTitle = nextTitle;
                        state.MarkDraftDirty("Unsaved changes");
                    }
                }

                bool sentSupportRequest = PungentSupportRequestBridge.IsSupportRequest(note) && PungentSupportRequestBridge.IsSent(note);
                if (readOnly && !sentSupportRequest)
                {
                    if (GUILayout.Button(new GUIContent("Edit", "Switch this floating tray to editable mode."), EditorStyles.miniButton, GUILayout.Width(48f)))
                        PungentStickyNoteOverlayController.OpenEdit(note.id, state.anchorRect, state.owner, state.sourceLabel);
                }
                bool draftSupportRequest = PungentSupportRequestBridge.IsSupportRequest(note) && !PungentSupportRequestBridge.IsSent(note);
                if (!readOnly && !compact && !draftSupportRequest && GUILayout.Button(new GUIContent("Preview", "Switch this floating tray to read-only preview mode."), EditorStyles.miniButton, GUILayout.Width(64f)))
                {
                    PungentStickyNoteOverlayController.OpenPreview(note.id, state.anchorRect, state.owner, state.sourceLabel);
                }

                string doneLabel = readOnly ? "Close" : compact ? "Save" : "Save and Close";
                string doneTooltip = readOnly ? "Close this preview." : "Save and close this note overlay.";
                if (GUILayout.Button(new GUIContent(doneLabel, doneTooltip), EditorStyles.miniButton, GUILayout.Width(readOnly ? 54f : compact ? 54f : 104f)))
                    PungentStickyNoteOverlayController.DoneEditing(state.owner);
                if (!compact && GUILayout.Button(new GUIContent("Open Browser", "Open the Sticky Notes browser filtered to this note."), EditorStyles.miniButton, GUILayout.Width(92f)))
                    PungentNotesRoadmapWindow.OpenAndSelect(note.id);
                if (GUILayout.Button(new GUIContent(compact ? "More" : "Actions", "More note actions."), EditorStyles.miniButton, GUILayout.Width(compact ? 48f : 62f)))
                    ShowMoreMenu(state, note);
            }
        }

        private static void DrawOverlayTabs(PungentStickyNoteOverlayState state)
        {
            string[] tabs = { "Note", "Details" };
            int next = GUILayout.Toolbar(Mathf.Clamp(state.noteOverlayTab, 0, 1), tabs, GUILayout.Height(22f));
            if (next != state.noteOverlayTab)
                state.noteOverlayTab = Mathf.Clamp(next, 0, 1);
        }

        private static void DrawNoteTab(PungentStickyNoteOverlayState state, PungentNote note, bool readOnly)
        {
            DrawSaveSummaryRow(state, note, readOnly);
            if (PungentSupportRequestBridge.IsSupportRequest(note))
                DrawSupportRequestOverlayPanel(state, note, readOnly);
            if (!readOnly)
                DrawMiniToolbar(state, note);

            state.scroll = EditorGUILayout.BeginScrollView(state.scroll, false, true, GUILayout.ExpandHeight(true));
            DrawBodyEditor(state, note, readOnly);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawDetailsTab(PungentStickyNoteOverlayState state, PungentNote note)
        {
            DrawDetailsTab(state, note, false);
        }

        private static void DrawDetailsTab(PungentStickyNoteOverlayState state, PungentNote note, bool readOnly)
        {
            bool supportRequest = PungentSupportRequestBridge.IsSupportRequest(note);
            if (supportRequest)
                DrawSupportRequestOverlayPanel(state, note, readOnly);

            using (new EditorGUI.DisabledScope(readOnly))
            {
                DrawMetadataRow(state, note);
                DrawChips(state, note);
            }

            state.scroll = EditorGUILayout.BeginScrollView(state.scroll, false, true, GUILayout.ExpandHeight(true));
            using (new EditorGUI.DisabledScope(readOnly))
            {
                DrawQuickMetadataFields(state, note);
                DrawTargetsSection(state, note);
                DrawTokenSection(state, note);
                DrawRelatedNotesSection(state, note);
            }
            if (!supportRequest)
                DrawActions(state, note);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSaveSummaryRow(PungentStickyNoteOverlayState state, PungentNote note, bool readOnly)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(readOnly ? "Preview" : state.draftSaveState, state.draftDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 92f);
                if (note != null)
                    EditorGUILayout.LabelField("Updated " + ShortDate(note.updatedUtc), UtilityWindowTheme.PathLabelStyle, GUILayout.MaxWidth(132f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(GetDraftMetrics(state, false).wordCount + " words", UtilityWindowTheme.PathLabelStyle, GUILayout.Width(72f));
            }
        }

        private static void DrawMetadataRow(PungentStickyNoteOverlayState state, PungentNote note)
        {
            float width = OverlayWidth(state);
            bool compact = width < 470f;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (compact)
                {
                    if (GUILayout.Button(new GUIContent("Metadata", "Edit note kind, status, priority, and visibility."), EditorStyles.toolbarDropDown, GUILayout.Width(78f)))
                        ShowMetadataMenu(state, note);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    note.kind = (PungentNoteKind)EditorGUILayout.EnumPopup(note.kind, GUILayout.Width(116f));
                    note.status = (PungentNoteStatus)EditorGUILayout.EnumPopup(note.status, GUILayout.Width(112f));
                    note.priority = (PungentNotePriority)EditorGUILayout.EnumPopup(note.priority, GUILayout.Width(126f));
                    if (EditorGUI.EndChangeCheck())
                        TouchAndSave(note, state, "Saved note properties.");
                }

                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(state.draftSaveState, state.draftDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 104f);
                if (width >= 360f)
                    EditorGUILayout.LabelField(GetDraftMetrics(state, false).wordCount + " words", UtilityWindowTheme.PathLabelStyle, GUILayout.Width(72f));
            }
        }

        private static void DrawMiniToolbar(PungentStickyNoteOverlayState state, PungentNote note)
        {
            float width = OverlayWidth(state);
            bool compact = width < 500f;
            bool veryCompact = width < 390f;
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.08f, 0.04f, 4, 2)))
            {
                using (new EditorGUI.DisabledScope(!state.draftDirty))
                {
                    if (GUILayout.Button(new GUIContent("Save", "Commit the current title/body draft."), EditorStyles.miniButton, GUILayout.Width(52f)))
                        state.CommitDraftIfDirty("Saved note.");
                }

                if (compact)
                {
                    if (GUILayout.Button(new GUIContent("Insert", "Insert lightweight formatting."), EditorStyles.toolbarDropDown, GUILayout.Width(62f)))
                        ShowInsertMenu(state);
                    if (veryCompact)
                    {
                        if (GUILayout.Button(new GUIContent("Attach", "Insert tokens, links, or targets."), EditorStyles.toolbarDropDown, GUILayout.Width(62f)))
                            ShowCompactAttachMenu(state, note);
                    }
                    else
                    {
                        if (GUILayout.Button(new GUIContent("Token", "Insert or link token syntax."), EditorStyles.toolbarDropDown, GUILayout.Width(62f)))
                            ShowTokenMenu(state, note);
                        if (GUILayout.Button(new GUIContent("Link", "Insert note, utility, or future-utility link syntax."), EditorStyles.toolbarDropDown, GUILayout.Width(56f)))
                            ShowLinkMenu(state, note);
                        if (GUILayout.Button(new GUIContent("Target", "Attach a note target."), EditorStyles.toolbarDropDown, GUILayout.Width(66f)))
                            ShowTargetMenu(state, note);
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("B", "Append bold markup."), EditorStyles.miniButton, GUILayout.Width(26f)))
                        AppendDraft(state, "**bold**");
                    if (GUILayout.Button(new GUIContent("I", "Append italic markup."), EditorStyles.miniButton, GUILayout.Width(24f)))
                        AppendDraft(state, "*italic*");
                    if (GUILayout.Button(new GUIContent("Checklist", "Append a checklist item."), EditorStyles.miniButton, GUILayout.Width(72f)))
                        AppendDraft(state, "\n- [ ] ");
                    if (GUILayout.Button(new GUIContent("Heading", "Append a section heading."), EditorStyles.miniButton, GUILayout.Width(68f)))
                        AppendDraft(state, "\n\n## New Section\n");
                    if (GUILayout.Button(new GUIContent("Token", "Insert or link token syntax."), EditorStyles.toolbarDropDown, GUILayout.Width(62f)))
                        ShowTokenMenu(state, note);
                    if (GUILayout.Button(new GUIContent("Link", "Insert note, utility, or future-utility link syntax."), EditorStyles.toolbarDropDown, GUILayout.Width(56f)))
                        ShowLinkMenu(state, note);
                    if (GUILayout.Button(new GUIContent("Target", "Attach a note target."), EditorStyles.toolbarDropDown, GUILayout.Width(66f)))
                        ShowTargetMenu(state, note);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawBodyEditor(PungentStickyNoteOverlayState state, PungentNote note, bool readOnly)
        {
            if (readOnly)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(PungentNoteGUI.PriorityTint(note.priority), 0.08f, 0.035f, 5, 2), GUILayout.MinHeight(150f), GUILayout.ExpandHeight(true)))
                    DrawPreview(state.draftBody);
            }
            else
            {
                GUI.SetNextControlName("PungentStickyNoteOverlayBody");
                EditorGUI.BeginChangeCheck();
                string nextBody = EditorGUILayout.TextArea(state.draftBody, _bodyStyle, GUILayout.MinHeight(170f), GUILayout.ExpandHeight(true));
                if (EditorGUI.EndChangeCheck())
                {
                    state.draftBody = nextBody;
                    state.MarkDraftDirty("Unsaved changes");
                }
            }

            if (!readOnly)
            {
                bool typing = IsBodyActivelyTyping(state);
                GetDraftMetrics(state, !typing);
                if (typing)
                    EditorGUILayout.LabelField("Links update after typing stops.", UtilityWindowTheme.MutedMiniLabelStyle);
                else
                    PungentNoteGUI.DrawInlineLinkPills(state.draftBody);
            }
        }

        private static void DrawChips(PungentStickyNoteOverlayState state, PungentNote note)
        {
            float width = OverlayWidth(state);
            bool compact = width < 420f;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!compact && note.tags != null)
                    {
                        foreach (string tag in note.tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Take(5))
                            UtilityWindowTheme.CountPill("#" + tag, UtilityWindowTheme.Teal, Mathf.Clamp(42f + tag.Length * 6f, 58f, 130f));
                    }

                    if (!compact && !string.IsNullOrWhiteSpace(note.linkedUtilityId))
                        UtilityWindowTheme.CountPill(PungentNoteGUI.UtilityDisplayName(note.linkedUtilityId), UtilityWindowTheme.Blue, 132f);
                    if (!compact && !string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
                        UtilityWindowTheme.CountPill(PungentNoteGUI.FutureUtilityDisplayName(note.linkedFutureUtilityId), UtilityWindowTheme.Cyan, 132f);
                    UtilityWindowTheme.CountPill((note.targets == null ? 0 : note.targets.Count) + " targets", UtilityWindowTheme.Purple, 78f);
                    UtilityWindowTheme.CountPill(GetDraftMetrics(state, !IsBodyActivelyTyping(state)).tokens.Count + " tokens", UtilityWindowTheme.Cyan, 76f);
                    if (compact)
                    {
                        int hidden = (note.tags == null ? 0 : note.tags.Count(tag => !string.IsNullOrWhiteSpace(tag))) +
                                     (string.IsNullOrWhiteSpace(note.linkedUtilityId) ? 0 : 1) +
                                     (string.IsNullOrWhiteSpace(note.linkedFutureUtilityId) ? 0 : 1);
                        if (hidden > 0)
                            UtilityWindowTheme.CountPill("+" + hidden + " links", UtilityWindowTheme.Teal, 66f);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void DrawQuickMetadataFields(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.showLinks = EditorGUILayout.Foldout(state.showLinks, "Metadata, Tags, And Links", true);
            if (!state.showLinks)
                return;

            EditorGUI.BeginChangeCheck();
            note.tags = PungentNoteGUI.ParseTags(PungentNoteGUI.DrawTagField("Tags", note.tags));
            note.linkedUtilityId = DrawUtilityPopup("Registered Utility", note.linkedUtilityId, true);
            note.linkedFutureUtilityId = DrawFutureUtilityPopup("Future Utility", note.linkedFutureUtilityId, true);
            note.auditIssueCode = EditorGUILayout.TextField("Audit Issue Code", note.auditIssueCode);
            note.visibility = (PungentNoteVisibility)EditorGUILayout.EnumPopup("Visibility", note.visibility);
            note.developerOnly = EditorGUILayout.Toggle("Developer Only", note.developerOnly);
            if (EditorGUI.EndChangeCheck())
                TouchAndSave(note, state, "Saved note metadata.");
        }

        private static void DrawTargetsSection(PungentStickyNoteOverlayState state, PungentNote note)
        {
            if (note.targets == null)
                note.targets = new List<PungentNoteTargetLink>();

            state.showTargets = EditorGUILayout.Foldout(state.showTargets, "Targets, Documentation Links, And References (" + note.targets.Count + ")", true);
            if (!state.showTargets)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Target", EditorStyles.miniButton, GUILayout.Width(82f)))
                    ShowTargetMenu(state, note);
                _pendingDocumentationLinkId = DrawDocumentationLinkPopup("Attach Doc Link", _pendingDocumentationLinkId, true);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_pendingDocumentationLinkId)))
                {
                    if (GUILayout.Button("Attach", EditorStyles.miniButton, GUILayout.Width(58f)))
                        AttachDocumentationLink(note, state);
                }
            }

            for (int i = 0; i < note.targets.Count; i++)
            {
                PungentNoteTargetLink target = note.targets[i];
                if (target == null)
                    continue;

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(TargetTint(target), 0.10f, 0.04f, 4, 2)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(TargetDisplayName(target), EditorStyles.boldLabel);
                        UtilityWindowTheme.CountPill(TargetGroupLabel(target.type), TargetTint(target), 108f);
                    }

                    EditorGUI.BeginChangeCheck();
                    target.type = (PungentNoteTargetType)EditorGUILayout.EnumPopup("Type", target.type);
                    target.label = EditorGUILayout.TextField("Label", target.label);
                    DrawTargetSpecificFields(target);
                    target.propertyPath = EditorGUILayout.TextField("Property", target.propertyPath);
                    target.componentType = EditorGUILayout.TextField("Component", target.componentType);
                    if (EditorGUI.EndChangeCheck())
                        TouchAndSave(note, state, "Saved note target.");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(!CanOpenTarget(target)))
                        {
                            if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(52f)))
                                OpenTarget(target, state);
                        }
                        using (new EditorGUI.DisabledScope(!CanPingTarget(target)))
                        {
                            if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(48f)))
                                PingTarget(target, state);
                        }
                        string copyValue = GetTargetCopyValue(target);
                        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(copyValue)))
                        {
                            if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(50f)))
                            {
                                EditorGUIUtility.systemCopyBuffer = copyValue;
                                state.status = "Copied target reference.";
                            }
                        }
                        if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(64f)))
                        {
                            note.targets.RemoveAt(i);
                            TouchAndSave(note, state, "Removed note target.");
                            GUIUtility.ExitGUI();
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private static void DrawTokenSection(PungentStickyNoteOverlayState state, PungentNote note)
        {
            if (note.linkedTokenKeys == null)
                note.linkedTokenKeys = new List<string>();

            state.showAdvanced = EditorGUILayout.Foldout(state.showAdvanced, "Related Notes, Tokens, And Advanced Actions", true);
            if (!state.showAdvanced)
                return;

            bool typing = IsBodyActivelyTyping(state);
            StickyNoteDraftMetrics metrics = GetDraftMetrics(state, !typing);
            List<PungentParsedToken> parsedTokens = metrics.tokens;
            UtilityWindowTheme.SectionTitle("Body Tokens", UtilityWindowTheme.Cyan, parsedTokens.Count.ToString());
            if (typing && !metrics.tokensFresh)
                EditorGUILayout.LabelField("Token preview updates after typing stops.", UtilityWindowTheme.MutedMiniLabelStyle);
            if (parsedTokens.Count == 0)
                EditorGUILayout.LabelField("No brace tokens found in the note body.", UtilityWindowTheme.MutedMiniLabelStyle);
            foreach (PungentParsedToken token in parsedTokens.Take(10))
                PungentTokenGUI.DrawTokenChip(token.key, PungentTokenStorage.Database.ContainsToken(token.key));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Token Validator", EditorStyles.miniButton, GUILayout.Width(132f)))
                    PungentTokenValidatorWindow.Open();
                if (GUILayout.Button("Sync Body Tokens", EditorStyles.miniButton, GUILayout.Width(118f)))
                    SyncBodyTokens(note, state, parsedTokens);
                GUILayout.FlexibleSpace();
            }

            UtilityWindowTheme.SectionTitle("Manual Token Links", UtilityWindowTheme.Cyan, note.linkedTokenKeys.Count.ToString());
            for (int i = 0; i < note.linkedTokenKeys.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.TextField(note.linkedTokenKeys[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        note.linkedTokenKeys[i] = PungentTokenParser.NormalizeKey(next);
                        TouchAndSave(note, state, "Saved token link.");
                    }
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(48f)))
                        EditorGUIUtility.systemCopyBuffer = PungentTokenParser.NormalizeKey(note.linkedTokenKeys[i]);
                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        note.linkedTokenKeys.RemoveAt(i);
                        TouchAndSave(note, state, "Removed token link.");
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("Add Token Key", EditorStyles.miniButton, GUILayout.Width(106f)))
            {
                note.linkedTokenKeys.Add(string.Empty);
                TouchAndSave(note, state, "Added token link.");
            }
        }

        private static void DrawRelatedNotesSection(PungentStickyNoteOverlayState state, PungentNote note)
        {
            if (note.relatedNoteIds == null)
                note.relatedNoteIds = new List<string>();

            UtilityWindowTheme.SectionTitle("Related Notes", UtilityWindowTheme.Neutral, note.relatedNoteIds.Count.ToString());
            for (int i = 0; i < note.relatedNoteIds.Count; i++)
            {
                PungentNote related = PungentStickyNoteOverlayController.FindNote(note.relatedNoteIds[i]);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.TextField(related != null ? related.title : note.relatedNoteIds[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        note.relatedNoteIds[i] = next;
                        TouchAndSave(note, state, "Saved related note reference.");
                    }
                    using (new EditorGUI.DisabledScope(related == null))
                    {
                        if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(48f)))
                            PungentStickyNoteOverlayController.OpenEdit(related.id, state.anchorRect, state.owner, "Related Note");
                    }
                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        note.relatedNoteIds.RemoveAt(i);
                        TouchAndSave(note, state, "Removed related note reference.");
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("Create Related Note", EditorStyles.miniButton, GUILayout.Width(132f)))
                CreateRelatedNote(note, state);
        }

        private static void DrawActions(PungentStickyNoteOverlayState state, PungentNote note)
        {
            if (PungentSupportRequestBridge.IsSupportRequest(note))
            {
                DrawSupportRequestActions(state, note);
                return;
            }

            float width = OverlayWidth(state);
            bool compact = width < 455f;
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 4, 2)))
            {
                UtilityWindowTheme.CountPill(state.draftSaveState, state.draftDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 102f);
                if (compact)
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Actions", "Archive, duplicate, copy, handoff, or delete this note."), EditorStyles.toolbarDropDown, GUILayout.Width(70f)))
                        ShowMoreMenu(state, note);
                }
                else
                {
                    if (GUILayout.Button(note.archived ? "Unarchive" : "Archive", EditorStyles.miniButton, GUILayout.Width(78f)))
                        ToggleArchive(state, note);
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(78f)))
                        DuplicateNote(state, note);
                    if (GUILayout.Button("Rich Doc", EditorStyles.miniButton, GUILayout.Width(68f)))
                        RunRichDocumentHandoff(note, state);
                    if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(64f)))
                        CopyNoteId(state, note);
                    if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(58f)))
                        DeleteNoteWithConfirmation(state, note);
                    GUILayout.FlexibleSpace();
                }
            }

            if (!string.IsNullOrWhiteSpace(state.status))
                EditorGUILayout.LabelField(state.status, UtilityWindowTheme.PathLabelStyle);
        }

        private static void ShowMoreMenu(PungentStickyNoteOverlayState state, PungentNote note)
        {
            GenericMenu menu = new GenericMenu();
            if (PungentSupportRequestBridge.IsSupportRequest(note))
            {
                PopulateSupportRequestMoreMenu(menu, state, note);
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("Copy ID"), false, () => CopyNoteId(state, note));
            menu.AddItem(new GUIContent("Copy Title"), false, () => EditorGUIUtility.systemCopyBuffer = note.title ?? string.Empty);
            menu.AddItem(new GUIContent("Open In Browser"), false, () => PungentNotesRoadmapWindow.OpenAndSelect(note.id));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(note.archived ? "Unarchive" : "Archive"), false, () => ToggleArchive(state, note));
            menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateNote(state, note));
            menu.AddItem(new GUIContent("Rich Document Handoff"), false, () => RunRichDocumentHandoff(note, state));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, () => DeleteNoteWithConfirmation(state, note));
            menu.ShowAsContext();
        }

        private static void DrawSupportRequestOverlayPanel(PungentStickyNoteOverlayState state, PungentNote note, bool readOnly)
        {
            PungentSupportRequestRecord record = PungentSupportRequestBridge.GetOrCreateRecord(note);
            if (record == null)
                return;

            if (state.supportRequestSendReview && record.state != PungentSupportRequestRelayState.Sent)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.14f, 0.06f, 5, 2)))
                {
                    EditorGUILayout.LabelField("Review Before Sending", UtilityWindowTheme.SectionHeaderStyle);
                    EditorGUILayout.LabelField("Double-check the request details, contact info, and context. Press the highlighted Send button below when ready.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(SupportRequestTint(record), 0.08f, 0.035f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Support Request", UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(record.state.ToString(), SupportRequestTint(record), 92f);
                    if (!string.IsNullOrWhiteSpace(record.remoteReportId))
                        UtilityWindowTheme.CountPill("ID " + record.remoteReportId, UtilityWindowTheme.Green, 118f);
                }

                using (new EditorGUI.DisabledScope(readOnly || record.state == PungentSupportRequestRelayState.Sent))
                {
                    string previousEmail = record.contactEmail;
                    string previousDiscord = record.contactDiscord;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField("Category", UtilityWindowTheme.PathLabelStyle);
                    int requestKind = GUILayout.Toolbar(PungentSupportRequestBridge.IsFeatureRequest(record) ? 1 : 0, new[] { "Bug Report", "Feature Request" }, GUILayout.Height(22f));
                    record.category = requestKind == 1 ? PungentBugReportCategory.MissingFeature : PungentBugReportCategory.Bug;
                    EditorGUILayout.LabelField("Priority", UtilityWindowTheme.PathLabelStyle);
                    int priority = GUILayout.Toolbar(PungentSupportRequestBridge.PriorityIndex(record.severity), PungentSupportRequestBridge.PriorityLabels, GUILayout.Height(22f));
                    record.severity = PungentSupportRequestBridge.PriorityFromIndex(priority);
                    record.contactEmail = EditorGUILayout.TextField("Email", record.contactEmail);
                    record.contactDiscord = EditorGUILayout.TextField("Discord", record.contactDiscord);
                    record.includeDiagnostics = EditorGUILayout.ToggleLeft("Include safe diagnostics", record.includeDiagnostics);
                    record.includeConsoleSummary = EditorGUILayout.ToggleLeft("Include console summary", record.includeConsoleSummary);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!string.Equals(previousEmail, record.contactEmail, StringComparison.Ordinal) ||
                            !string.Equals(previousDiscord, record.contactDiscord, StringComparison.Ordinal))
                            PungentSupportRequestBridge.UpdateSharedContact(record, record.contactEmail, record.contactDiscord);
                        else
                            PungentBugReportStorage.instance.TouchSupportRequest(record);
                        state.status = "Saved support request metadata.";
                    }
                }

                if (!string.IsNullOrWhiteSpace(record.parentNoteId))
                    EditorGUILayout.LabelField("Follow-up to: " + record.parentNoteId + (string.IsNullOrWhiteSpace(record.parentRemoteReportId) ? string.Empty : " / " + record.parentRemoteReportId), UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(record.lastError))
                    EditorGUILayout.HelpBox(record.lastError, MessageType.Warning);
            }

            DrawSupportRequestActions(state, note);
        }

        private static void DrawSupportRequestActions(PungentStickyNoteOverlayState state, PungentNote note)
        {
            PungentSupportRequestRecord record = PungentSupportRequestBridge.GetOrCreateRecord(note);
            bool sent = record != null && record.state == PungentSupportRequestRelayState.Sent;
            float width = OverlayWidth(state);
            bool compact = width < 455f;

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(sent ? UtilityWindowTheme.Green : UtilityWindowTheme.Teal, 0.10f, 0.04f, 4, 2)))
            {
                UtilityWindowTheme.CountPill(record == null ? "Draft" : record.state.ToString(), SupportRequestTint(record), 92f);
                if (compact)
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Actions", "Support request actions."), EditorStyles.toolbarDropDown, GUILayout.Width(70f)))
                        ShowMoreMenu(state, note);
                }
                else if (sent)
                {
                    if (GUILayout.Button("Follow-up", EditorStyles.miniButton, GUILayout.Width(82f)))
                        CreateSupportRequestFollowUp(state, note);
                    if (GUILayout.Button("Archive", EditorStyles.miniButton, GUILayout.Width(72f)))
                        ShowSupportRequestArchiveMenu(state, note);
                    if (GUILayout.Button("Copy JSON", EditorStyles.miniButton, GUILayout.Width(84f)))
                        CopySupportRequestJson(state, note);
                    if (GUILayout.Button("Open Browser", EditorStyles.miniButton, GUILayout.Width(92f)))
                        PungentNotesRoadmapWindow.OpenAndSelect(note.id);
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    bool highlightSend = state.supportRequestSendReview;
                    if (highlightSend
                            ? UtilityWindowTheme.StudioButton(new GUIContent("Send", "Submit this support request to the configured Wix relay."), UtilityWindowTheme.Teal, UtilityWindowTheme.PungentButtonRole.Primary, GUILayout.Width(72f))
                            : GUILayout.Button(new GUIContent(PungentSupportRequestBridge.BrowserSendLabel(note), "Submit this support request to the configured Wix relay."), EditorStyles.miniButton, GUILayout.Width(86f)))
                        SendSupportRequest(state, note);
                    if (GUILayout.Button("Queue", EditorStyles.miniButton, GUILayout.Width(62f)))
                        QueueSupportRequest(state, note);
                    if (GUILayout.Button("Copy JSON", EditorStyles.miniButton, GUILayout.Width(84f)))
                        CopySupportRequestJson(state, note);
                    if (GUILayout.Button("Archive", EditorStyles.miniButton, GUILayout.Width(72f)))
                        ShowSupportRequestArchiveMenu(state, note);
                    GUILayout.FlexibleSpace();
                }
            }

            if (ShouldDrawSupportRequestStatus(state, record))
                EditorGUILayout.LabelField(state.status, UtilityWindowTheme.PathLabelStyle);
        }

        private static bool ShouldDrawSupportRequestStatus(PungentStickyNoteOverlayState state, PungentSupportRequestRecord record)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.status))
                return false;
            if (record == null || string.IsNullOrWhiteSpace(record.lastError))
                return true;
            return !string.Equals(state.status.Trim(), record.lastError.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void PopulateSupportRequestMoreMenu(GenericMenu menu, PungentStickyNoteOverlayState state, PungentNote note)
        {
            PungentSupportRequestRecord record = PungentSupportRequestBridge.GetOrCreateRecord(note);
            bool sent = record != null && record.state == PungentSupportRequestRelayState.Sent;
            if (sent)
            {
                menu.AddItem(new GUIContent("Follow-up"), false, () => CreateSupportRequestFollowUp(state, note));
            }
            else
            {
                menu.AddItem(new GUIContent(PungentSupportRequestBridge.BrowserSendLabel(note)), false, () => SendSupportRequest(state, note));
                menu.AddItem(new GUIContent("Queue"), false, () => QueueSupportRequest(state, note));
            }

            menu.AddItem(new GUIContent("Copy JSON"), false, () => CopySupportRequestJson(state, note));
            menu.AddItem(new GUIContent("Open In Browser"), false, () => PungentNotesRoadmapWindow.OpenAndSelect(note.id));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Archive/Archive Locally"), false, () => ArchiveSupportRequestLocally(state, note));
            menu.AddItem(new GUIContent("Archive/Archive Support Request"), false, () => ConfirmAndArchiveSupportTicket(state, note));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy ID"), false, () => CopyNoteId(state, note));
            if (!sent)
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateNote(state, note));
        }

        private static void SendSupportRequest(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.CommitDraftIfDirty("Saved request before send.");
            state.status = "Submitting support request...";
            PungentSupportRequestBridge.Send(note, (success, message) =>
            {
                state.supportRequestSendReview = false;
                state.status = message;
            });
        }

        private static void QueueSupportRequest(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.CommitDraftIfDirty("Saved request before queue.");
            PungentSupportRequestBridge.QueueLocally(note);
            state.status = "Queued support request locally.";
        }

        private static void CopySupportRequestJson(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.CommitDraftIfDirty("Saved request before copying JSON.");
            PungentSupportRequestBridge.CopyJson(note);
            state.status = "Copied support request payload.";
        }

        private static void CreateSupportRequestFollowUp(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.CommitDraftIfDirty("Saved request before follow-up.");
            PungentNote followUp = PungentSupportRequestBridge.CreateFollowUp(note);
            if (followUp == null)
                return;
            PungentStickyNoteOverlayController.OpenEdit(followUp.id, state.anchorRect, state.owner, "Support Request Follow-up");
            GUIUtility.ExitGUI();
        }

        private static void ShowSupportRequestArchiveMenu(PungentStickyNoteOverlayState state, PungentNote note)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Archive Locally"), false, () => ArchiveSupportRequestLocally(state, note));
            menu.AddItem(new GUIContent("Archive Support Request"), false, () => ConfirmAndArchiveSupportTicket(state, note));
            menu.ShowAsContext();
        }

        private static void ArchiveSupportRequestLocally(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.CommitDraftIfDirty("Saved request before local archive.");
            PungentSupportRequestBridge.ArchiveLocally(note);
            state.status = "Archived support request locally.";
        }

        private static void ConfirmAndArchiveSupportTicket(PungentStickyNoteOverlayState state, PungentNote note)
        {
            if (!EditorUtility.DisplayDialog(
                    "Archive Support Request",
                    "This will send an automatic follow-up asking support to archive the backend ticket. It is not just hiding the request from this browser.",
                    "Archive Backend Ticket",
                    "Cancel"))
                return;

            state.CommitDraftIfDirty("Saved request before backend archive.");
            state.status = "Sending archive follow-up...";
            PungentSupportRequestBridge.ArchiveSupportTicket(note, (success, message) => state.status = message);
        }

        private static Color SupportRequestTint(PungentSupportRequestRecord record)
        {
            if (record == null)
                return UtilityWindowTheme.Teal;
            switch (record.state)
            {
                case PungentSupportRequestRelayState.Sent:
                    return UtilityWindowTheme.Green;
                case PungentSupportRequestRelayState.Failed:
                    return UtilityWindowTheme.Amber;
                case PungentSupportRequestRelayState.Queued:
                    return UtilityWindowTheme.Cyan;
                default:
                    return UtilityWindowTheme.Teal;
            }
        }

        private static void ShowMetadataMenu(PungentStickyNoteOverlayState state, PungentNote note)
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentNoteKind value in Enum.GetValues(typeof(PungentNoteKind)))
            {
                PungentNoteKind captured = value;
                menu.AddItem(new GUIContent("Kind/" + captured), note.kind == captured, () =>
                {
                    note.kind = captured;
                    TouchAndSave(note, state, "Saved note kind.");
                });
            }

            foreach (PungentNoteStatus value in Enum.GetValues(typeof(PungentNoteStatus)))
            {
                PungentNoteStatus captured = value;
                menu.AddItem(new GUIContent("Status/" + captured), note.status == captured, () =>
                {
                    note.status = captured;
                    TouchAndSave(note, state, "Saved note status.");
                });
            }

            foreach (PungentNotePriority value in Enum.GetValues(typeof(PungentNotePriority)))
            {
                PungentNotePriority captured = value;
                menu.AddItem(new GUIContent("Priority/" + captured), note.priority == captured, () =>
                {
                    note.priority = captured;
                    TouchAndSave(note, state, "Saved note priority.");
                });
            }

            foreach (PungentNoteVisibility value in Enum.GetValues(typeof(PungentNoteVisibility)))
            {
                PungentNoteVisibility captured = value;
                menu.AddItem(new GUIContent("Visibility/" + captured), note.visibility == captured, () =>
                {
                    note.visibility = captured;
                    TouchAndSave(note, state, "Saved note visibility.");
                });
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Show Tags And Links"), state.showLinks, () => state.showLinks = true);
            menu.ShowAsContext();
        }

        private static void ShowInsertMenu(PungentStickyNoteOverlayState state)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Bold"), false, () => AppendDraft(state, "**bold**"));
            menu.AddItem(new GUIContent("Italic"), false, () => AppendDraft(state, "*italic*"));
            menu.AddItem(new GUIContent("Checklist Item"), false, () => AppendDraft(state, "\n- [ ] "));
            menu.AddItem(new GUIContent("Heading"), false, () => AppendDraft(state, "\n\n## New Section\n"));
            menu.ShowAsContext();
        }

        private static void ShowCompactAttachMenu(PungentStickyNoteOverlayState state, PungentNote note)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Token/Insert Empty Token"), false, () => AppendDraft(state, " {token.key} "));
            if (note.linkedTokenKeys != null)
            {
                foreach (string key in note.linkedTokenKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Take(16))
                {
                    string captured = PungentTokenParser.NormalizeKey(key);
                    menu.AddItem(new GUIContent("Token/Linked/" + captured), false, () => AppendDraft(state, " {" + captured + "} "));
                }
            }

            if (!string.IsNullOrWhiteSpace(note.linkedUtilityId))
                menu.AddItem(new GUIContent("Link/Utility"), false, () => AppendDraft(state, " @utility:" + note.linkedUtilityId + " "));
            else
                menu.AddDisabledItem(new GUIContent("Link/Utility"));
            if (!string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
                menu.AddItem(new GUIContent("Link/Future Utility"), false, () => AppendDraft(state, " @future:" + note.linkedFutureUtilityId + " "));
            else
                menu.AddDisabledItem(new GUIContent("Link/Future Utility"));
            menu.AddItem(new GUIContent("Link/This Note"), false, () => AppendDraft(state, " @note:" + note.id + " "));
            menu.AddItem(new GUIContent("Link/Create Related Note"), false, () => CreateRelatedNote(note, state));

            foreach (PungentNoteTargetType type in Enum.GetValues(typeof(PungentNoteTargetType)))
            {
                PungentNoteTargetType captured = type;
                menu.AddItem(new GUIContent("Target/" + TargetGroupLabel(captured)), false, () =>
                {
                    if (note.targets == null)
                        note.targets = new List<PungentNoteTargetLink>();
                    note.targets.Add(new PungentNoteTargetLink { type = captured, label = TargetGroupLabel(captured) });
                    TouchAndSave(note, state, "Added note target.");
                });
            }

            menu.ShowAsContext();
        }

        private static void ToggleArchive(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.CommitDraftIfDirty("Saved draft before archive change.");
            PungentNoteStorage.Archive(note, !note.archived);
            state.status = note.archived ? "Archived note." : "Unarchived note.";
        }

        private static void DuplicateNote(PungentStickyNoteOverlayState state, PungentNote note)
        {
            state.CommitDraftIfDirty("Saved draft before duplicate.");
            PungentNote copy = PungentNoteStorage.Database.Duplicate(note);
            if (copy == null)
                return;
            PungentSupportRequestBridge.CopyRequestMetadata(note, copy);
            PungentStickyNoteOverlayController.OpenEdit(copy, state.anchorRect, state.owner, state.sourceLabel);
            GUIUtility.ExitGUI();
        }

        private static void CopyNoteId(PungentStickyNoteOverlayState state, PungentNote note)
        {
            EditorGUIUtility.systemCopyBuffer = note.id;
            state.status = "Copied note ID.";
        }

        private static void DeleteNoteWithConfirmation(PungentStickyNoteOverlayState state, PungentNote note)
        {
            if (!EditorUtility.DisplayDialog("Delete Note", "Delete this note?", "Delete", "Cancel"))
                return;

            PungentNoteStorage.Delete(note);
            PungentStickyNoteOverlayController.Close(state.owner);
            GUIUtility.ExitGUI();
        }

        private static void ShowTokenMenu(PungentStickyNoteOverlayState state, PungentNote note)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Insert Empty Token"), false, () => AppendDraft(state, " {token.key} "));
            if (note.linkedTokenKeys != null)
            {
                foreach (string key in note.linkedTokenKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Take(16))
                {
                    string captured = PungentTokenParser.NormalizeKey(key);
                    menu.AddItem(new GUIContent("Linked/" + captured), false, () => AppendDraft(state, " {" + captured + "} "));
                }
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Token Validator"), false, PungentTokenValidatorWindow.Open);
            menu.ShowAsContext();
        }

        private static void ShowLinkMenu(PungentStickyNoteOverlayState state, PungentNote note)
        {
            GenericMenu menu = new GenericMenu();
            if (!string.IsNullOrWhiteSpace(note.linkedUtilityId))
                menu.AddItem(new GUIContent("Insert Utility Link"), false, () => AppendDraft(state, " @utility:" + note.linkedUtilityId + " "));
            else
                menu.AddDisabledItem(new GUIContent("Insert Utility Link"));
            if (!string.IsNullOrWhiteSpace(note.linkedFutureUtilityId))
                menu.AddItem(new GUIContent("Insert Future Utility Link"), false, () => AppendDraft(state, " @future:" + note.linkedFutureUtilityId + " "));
            else
                menu.AddDisabledItem(new GUIContent("Insert Future Utility Link"));
            menu.AddItem(new GUIContent("Insert Note Link"), false, () => AppendDraft(state, " @note:" + note.id + " "));
            menu.AddItem(new GUIContent("Create Related Note"), false, () => CreateRelatedNote(note, state));
            menu.ShowAsContext();
        }

        private static void ShowTargetMenu(PungentStickyNoteOverlayState state, PungentNote note)
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentNoteTargetType type in Enum.GetValues(typeof(PungentNoteTargetType)))
            {
                PungentNoteTargetType captured = type;
                menu.AddItem(new GUIContent(TargetGroupLabel(captured)), false, () =>
                {
                    if (note.targets == null)
                        note.targets = new List<PungentNoteTargetLink>();
                    note.targets.Add(new PungentNoteTargetLink { type = captured, label = TargetGroupLabel(captured) });
                    TouchAndSave(note, state, "Added note target.");
                });
            }
            menu.ShowAsContext();
        }

        private static void AppendDraft(PungentStickyNoteOverlayState state, string snippet)
        {
            state.draftBody = (state.draftBody ?? string.Empty).TrimEnd() + snippet;
            state.MarkDraftDirty("Unsaved changes");
        }

        private static void DrawPreview(string body)
        {
            string[] lines = (body ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines.All(string.IsNullOrWhiteSpace))
            {
                EditorGUILayout.LabelField("Start writing this note...", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            bool codeBlock = false;
            foreach (string raw in lines)
            {
                string line = raw ?? string.Empty;
                string trimmed = line.Trim();
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    codeBlock = !codeBlock;
                    EditorGUILayout.Space(2f);
                    continue;
                }

                if (string.IsNullOrEmpty(trimmed))
                {
                    EditorGUILayout.Space(5f);
                }
                else if (codeBlock)
                {
                    EditorGUILayout.SelectableLabel(line, EditorStyles.helpBox, GUILayout.MinHeight(18f));
                }
                else if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                {
                    EditorGUILayout.LabelField(trimmed.Substring(2), EditorStyles.boldLabel);
                }
                else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    EditorGUILayout.LabelField(trimmed.Substring(3), EditorStyles.boldLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(trimmed, _previewStyle);
                }
            }
        }

        private static void DrawTargetSpecificFields(PungentNoteTargetLink target)
        {
            if (target == null)
                return;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility:
                    target.utilityId = DrawUtilityPopup("Utility", target.utilityId, true);
                    break;
                case PungentNoteTargetType.FutureUtility:
                    target.futureUtilityId = DrawFutureUtilityPopup("Future Utility", target.futureUtilityId, true);
                    break;
                case PungentNoteTargetType.DocumentationLink:
                    target.documentationLinkId = DrawDocumentationLinkPopup("Documentation Link", target.documentationLinkId, true);
                    break;
                case PungentNoteTargetType.ExternalPath:
                    target.externalPathOrUrl = EditorGUILayout.TextField("External Path / Web URL", target.externalPathOrUrl);
                    break;
                case PungentNoteTargetType.Asset:
                    target.assetGuid = EditorGUILayout.TextField("Asset GUID", target.assetGuid);
                    break;
                case PungentNoteTargetType.SceneObject:
                case PungentNoteTargetType.ComponentInstance:
                case PungentNoteTargetType.SerializedProperty:
                    target.sceneObjectGlobalId = EditorGUILayout.TextField("Scene Object ID", target.sceneObjectGlobalId);
                    target.assetGuid = EditorGUILayout.TextField("Asset GUID", target.assetGuid);
                    break;
                case PungentNoteTargetType.ScriptPath:
                    target.scriptPath = EditorGUILayout.TextField("Script Path", target.scriptPath);
                    break;
                case PungentNoteTargetType.Token:
                    target.tokenKey = EditorGUILayout.TextField("Token Key", target.tokenKey);
                    break;
                case PungentNoteTargetType.AuditIssue:
                    target.auditIssueCode = EditorGUILayout.TextField("Audit Issue", target.auditIssueCode);
                    break;
                case PungentNoteTargetType.Note:
                    target.noteId = EditorGUILayout.TextField("Note ID", target.noteId);
                    break;
            }
        }

        private static void AttachDocumentationLink(PungentNote note, PungentStickyNoteOverlayState state)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(_pendingDocumentationLinkId);
            if (PungentNoteStorage.AddDocumentationLinkTarget(note, link))
                state.status = "Attached documentation link.";
            else
                state.status = "Documentation link was already attached or could not be resolved.";
        }

        private static void SyncBodyTokens(PungentNote note, PungentStickyNoteOverlayState state, List<PungentParsedToken> parsedTokens)
        {
            if (note.linkedTokenKeys == null)
                note.linkedTokenKeys = new List<string>();

            int before = note.linkedTokenKeys.Count;
            foreach (string key in parsedTokens.Select(token => PungentTokenParser.NormalizeKey(token.key)).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!note.linkedTokenKeys.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                    note.linkedTokenKeys.Add(key);
            }

            PungentNoteStorage.Database.Touch(note);
            PungentNoteStorage.Save();
            state.status = "Synced " + Mathf.Max(0, note.linkedTokenKeys.Count - before) + " token link(s).";
        }

        private static void CreateRelatedNote(PungentNote source, PungentStickyNoteOverlayState state)
        {
            state.CommitDraftIfDirty("Saved draft before creating related note.");
            PungentNote related = PungentNoteStorage.Database.CreateNote("Related: " + source.title, source.kind);
            related.status = PungentNoteStatus.ToDo;
            related.priority = source.priority;
            related.linkedUtilityId = source.linkedUtilityId;
            related.linkedFutureUtilityId = source.linkedFutureUtilityId;
            related.tags = new List<string>(source.tags ?? new List<string>());
            related.relatedNoteIds.Add(source.id);
            if (source.relatedNoteIds == null)
                source.relatedNoteIds = new List<string>();
            if (!source.relatedNoteIds.Any(id => string.Equals(id, related.id, StringComparison.OrdinalIgnoreCase)))
                source.relatedNoteIds.Add(related.id);
            PungentNoteStorage.Database.Touch(source);
            PungentNoteStorage.Save();
            PungentStickyNoteOverlayController.OpenEdit(related, state.anchorRect, state.owner, "Related Note");
        }

        private static void RunRichDocumentHandoff(PungentNote note, PungentStickyNoteOverlayState state)
        {
            if (note == null)
                return;

            state.CommitDraftIfDirty("Saved note draft before rich document handoff.");
            PungentAuthoringReference reference = PungentAuthoringReference.Create(PungentAuthoringItemKind.LegacyNote, note.id, PungentAuthoringLegacyNoteProvider.Id, note.title);
            List<PungentAuthoringAction> actions = PungentAuthoringProviderRegistry.GetConversionActions(reference).ToList();
            PungentAuthoringAction action = actions.FirstOrDefault(item => item != null && item.enabled && item.kind == PungentAuthoringActionKind.Open) ??
                                           actions.FirstOrDefault(item => item != null && item.enabled && item.kind == PungentAuthoringActionKind.ConvertLegacyNoteToRichDocument);

            if (action == null)
            {
                PungentAuthoringAction disabled = actions.FirstOrDefault(item => item != null && !item.enabled);
                state.status = disabled != null && !string.IsNullOrWhiteSpace(disabled.disabledReason)
                    ? disabled.disabledReason
                    : "Rich Document Editor provider is not installed.";
                return;
            }

            if (PungentAuthoringProviderRegistry.TryRunConversionAction(action, out _, out string error))
                state.status = action.kind == PungentAuthoringActionKind.Open
                    ? "Opened converted rich document."
                    : "Created linked rich document copy. Legacy note body was left unchanged.";
            else
                state.status = string.IsNullOrWhiteSpace(error) ? "Rich document handoff failed." : error;
        }

        private static void TouchAndSave(PungentNote note, PungentStickyNoteOverlayState state, string status)
        {
            PungentNoteStorage.Database.Touch(note);
            PungentNoteStorage.Save();
            state.status = status;
        }

        private static string DrawUtilityPopup(string label, string currentId, bool includeNone)
        {
            EnsureUtilityOptions(includeNone);
            return DrawCachedPopup(label, currentId, UtilityOptions);
        }

        private static string DrawFutureUtilityPopup(string label, string currentId, bool includeNone)
        {
            EnsureFutureUtilityOptions(includeNone);
            return DrawCachedPopup(label, currentId, FutureUtilityOptions);
        }

        private static string DrawDocumentationLinkPopup(string label, string currentId, bool includeNone)
        {
            EnsureDocumentationLinkOptions(includeNone, currentId);
            return DrawCachedPopup(label, currentId, DocumentationLinkOptions);
        }

        private static bool CanOpenTarget(PungentNoteTargetLink target)
        {
            if (target == null)
                return false;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility:
                    return PungentUtilityRegistry.Find(target.utilityId) != null;
                case PungentNoteTargetType.FutureUtility:
                    return PungentNoteStorage.Database.futureUtilities.Any(record => record != null && string.Equals(record.id, target.futureUtilityId, StringComparison.OrdinalIgnoreCase));
                case PungentNoteTargetType.Token:
                case PungentNoteTargetType.AuditIssue:
                case PungentNoteTargetType.DocumentationLink:
                case PungentNoteTargetType.ExternalPath:
                    return true;
                case PungentNoteTargetType.Note:
                    return PungentStickyNoteOverlayController.FindNote(target.noteId) != null;
                default:
                    return PungentNoteContextResolver.ResolveTarget(target) != null;
            }
        }

        private static bool CanPingTarget(PungentNoteTargetLink target)
        {
            return target != null && PungentNoteContextResolver.ResolveTarget(target) != null;
        }

        private static void OpenTarget(PungentNoteTargetLink target, PungentStickyNoteOverlayState state)
        {
            if (target == null)
                return;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility:
                    PungentUtilityRegistry.Open(target.utilityId);
                    state.status = "Opened linked utility.";
                    return;
                case PungentNoteTargetType.FutureUtility:
                    state.status = "Future utility record is visible from More > Legacy Advanced Workspace.";
                    return;
                case PungentNoteTargetType.Token:
                    PungentTokenValidatorWindow.Open();
                    state.status = "Opened Token Validator.";
                    return;
                case PungentNoteTargetType.AuditIssue:
                    PungentUtilityDesignAuditWindow.Open();
                    state.status = "Opened Design Audit.";
                    return;
                case PungentNoteTargetType.Note:
                    PungentStickyNoteOverlayController.OpenEdit(target.noteId, state.anchorRect, state.owner, "Related Note");
                    return;
                case PungentNoteTargetType.DocumentationLink:
                    OpenDocumentationLink(target.documentationLinkId, state);
                    return;
                case PungentNoteTargetType.ExternalPath:
                    OpenExternalTarget(target.externalPathOrUrl, state);
                    return;
            }

            UnityEngine.Object obj = PungentNoteContextResolver.ResolveTarget(target);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
                AssetDatabase.OpenAsset(obj);
                state.status = "Opened Unity target.";
            }
        }

        private static void PingTarget(PungentNoteTargetLink target, PungentStickyNoteOverlayState state)
        {
            UnityEngine.Object obj = PungentNoteContextResolver.ResolveTarget(target);
            if (obj == null)
            {
                state.status = "Target could not be resolved.";
                return;
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            state.status = "Pinged Unity target.";
        }

        private static void OpenDocumentationLink(string documentationLinkId, PungentStickyNoteOverlayState state)
        {
            PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(documentationLinkId);
            if (PungentUtilityDocumentationLinks.instance.Open(link, out string error))
                state.status = "Opened documentation link target.";
            else
                state.status = string.IsNullOrWhiteSpace(error) ? "Documentation link target could not be opened." : error;
        }

        private static void OpenExternalTarget(string externalPathOrUrl, PungentStickyNoteOverlayState state)
        {
            if (PungentUtilityDocumentationLinks.TryOpenTarget(string.Empty, externalPathOrUrl, out string error))
                state.status = "Opened external target.";
            else
                state.status = string.IsNullOrWhiteSpace(error) ? "External target could not be opened." : error;
        }

        private static string GetTargetCopyValue(PungentNoteTargetLink target)
        {
            if (target == null)
                return string.Empty;

            switch (target.type)
            {
                case PungentNoteTargetType.RegisteredUtility: return target.utilityId ?? string.Empty;
                case PungentNoteTargetType.FutureUtility: return target.futureUtilityId ?? string.Empty;
                case PungentNoteTargetType.Asset:
                    return string.IsNullOrWhiteSpace(target.assetGuid) ? string.Empty : AssetDatabase.GUIDToAssetPath(target.assetGuid);
                case PungentNoteTargetType.SceneObject:
                case PungentNoteTargetType.ComponentInstance:
                case PungentNoteTargetType.SerializedProperty:
                    return !string.IsNullOrWhiteSpace(target.sceneObjectGlobalId) ? target.sceneObjectGlobalId : target.assetGuid;
                case PungentNoteTargetType.ComponentType: return target.componentType ?? string.Empty;
                case PungentNoteTargetType.ScriptPath: return target.scriptPath ?? string.Empty;
                case PungentNoteTargetType.Token: return PungentTokenParser.NormalizeKey(target.tokenKey);
                case PungentNoteTargetType.AuditIssue: return target.auditIssueCode ?? string.Empty;
                case PungentNoteTargetType.DocumentationLink: return target.documentationLinkId ?? string.Empty;
                case PungentNoteTargetType.Note: return target.noteId ?? string.Empty;
                case PungentNoteTargetType.ExternalPath: return target.externalPathOrUrl ?? string.Empty;
                default: return target.label ?? string.Empty;
            }
        }

        private static Color TargetTint(PungentNoteTargetLink target)
        {
            if (target == null)
                return UtilityWindowTheme.Neutral;
            switch (target.type)
            {
                case PungentNoteTargetType.Asset:
                case PungentNoteTargetType.ScriptPath:
                    return UtilityWindowTheme.Green;
                case PungentNoteTargetType.SceneObject:
                case PungentNoteTargetType.ComponentInstance:
                case PungentNoteTargetType.SerializedProperty:
                    return UtilityWindowTheme.Blue;
                case PungentNoteTargetType.Token:
                case PungentNoteTargetType.DocumentationLink:
                    return UtilityWindowTheme.Cyan;
                case PungentNoteTargetType.AuditIssue:
                    return UtilityWindowTheme.Purple;
                case PungentNoteTargetType.RegisteredUtility:
                case PungentNoteTargetType.FutureUtility:
                    return UtilityWindowTheme.Teal;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static string TargetDisplayName(PungentNoteTargetLink target)
        {
            if (target == null)
                return "Target";
            if (!string.IsNullOrWhiteSpace(target.label))
                return target.label;
            if (target.type == PungentNoteTargetType.DocumentationLink)
            {
                PungentUtilityDocumentationLinks.DocumentationLink link = PungentUtilityDocumentationLinks.instance.FindById(target.documentationLinkId);
                return link == null ? "Missing documentation link" : PungentUtilityDocumentationLinks.GetDisplayName(link);
            }
            if (target.type == PungentNoteTargetType.ExternalPath)
                return string.IsNullOrWhiteSpace(target.externalPathOrUrl) ? "External Path / Web URL" : target.externalPathOrUrl;
            return TargetGroupLabel(target.type);
        }

        private static string TargetGroupLabel(PungentNoteTargetType type)
        {
            switch (type)
            {
                case PungentNoteTargetType.RegisteredUtility: return "Utility";
                case PungentNoteTargetType.FutureUtility: return "Future Utility";
                case PungentNoteTargetType.Asset: return "Asset";
                case PungentNoteTargetType.SceneObject: return "Scene Object";
                case PungentNoteTargetType.ComponentType: return "Component Type";
                case PungentNoteTargetType.ComponentInstance: return "Component";
                case PungentNoteTargetType.SerializedProperty: return "Property";
                case PungentNoteTargetType.ScriptPath: return "Script Path";
                case PungentNoteTargetType.Token: return "Token";
                case PungentNoteTargetType.AuditIssue: return "Audit Issue";
                case PungentNoteTargetType.DocumentationLink: return "Documentation Link";
                case PungentNoteTargetType.Note: return "Related Note";
                case PungentNoteTargetType.ExternalPath: return "External Path / Web URL";
                default: return "Unspecified Target";
            }
        }

        private static void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(7, 7, 5, 5)
            };
            _bodyStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            _previewStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(6, 6, 3, 3)
            };
        }

        private static StickyNoteDraftMetrics GetDraftMetrics(PungentStickyNoteOverlayState state, bool allowTokenRefresh)
        {
            string noteId = state == null ? string.Empty : state.draftNoteId ?? string.Empty;
            string body = state == null ? string.Empty : state.draftBody ?? string.Empty;
            int hash = body.GetHashCode();
            double now = EditorApplication.timeSinceStartup;

            if (!string.Equals(DraftMetrics.noteId, noteId, StringComparison.OrdinalIgnoreCase) || DraftMetrics.bodyHash != hash)
            {
                DraftMetrics.noteId = noteId;
                DraftMetrics.bodyHash = hash;
                DraftMetrics.wordCount = BodyWordCount(body);
                DraftMetrics.tokensFresh = false;
                DraftMetrics.lastBodyChangeTime = now;
            }

            if (allowTokenRefresh &&
                (!DraftMetrics.tokensFresh || now - DraftMetrics.lastBodyChangeTime >= InlinePreviewRefreshDelaySeconds))
            {
                DraftMetrics.tokens = PungentTokenParser.Parse(body);
                DraftMetrics.tokensFresh = true;
            }

            return DraftMetrics;
        }

        private static bool IsBodyActivelyTyping(PungentStickyNoteOverlayState state)
        {
            if (state == null)
                return false;
            if (!string.Equals(GUI.GetNameOfFocusedControl(), "PungentStickyNoteOverlayBody", StringComparison.Ordinal))
                return false;
            return EditorApplication.timeSinceStartup - state.lastDraftEditTime < InlinePreviewRefreshDelaySeconds;
        }

        private static int BodyWordCount(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return 0;

            int count = 0;
            bool inWord = false;
            for (int i = 0; i < body.Length; i++)
            {
                if (char.IsWhiteSpace(body[i]))
                {
                    inWord = false;
                }
                else if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }

            return count;
        }

        private static string DrawCachedPopup(string label, string currentId, PopupOptionCache cache)
        {
            if (cache == null || cache.ids == null || cache.labels == null || cache.ids.Length == 0 || cache.labels.Length == 0)
                return string.Empty;

            int index = 0;
            for (int i = 0; i < cache.ids.Length; i++)
            {
                if (string.Equals(cache.ids[i], currentId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            int next = EditorGUILayout.Popup(label, index, cache.labels);
            return next >= 0 && next < cache.ids.Length ? cache.ids[next] : string.Empty;
        }

        private static void EnsureUtilityOptions(bool includeNone)
        {
            int count = PungentUtilityRegistry.All == null ? 0 : PungentUtilityRegistry.All.Count;
            int hash = 17;
            for (int i = 0; i < count; i++)
            {
                PungentUtilityDescriptor descriptor = PungentUtilityRegistry.All[i];
                if (descriptor != null)
                    hash = hash * 31 + ((descriptor.Id ?? string.Empty) + "|" + (descriptor.DisplayName ?? string.Empty)).GetHashCode();
            }
            string key = "none:" + includeNone + "|count:" + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|hash:" + hash.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (string.Equals(UtilityOptions.key, key, StringComparison.Ordinal))
                return;

            List<PungentUtilityDescriptor> utilities = PungentUtilityRegistry.All.Where(u => u != null).OrderBy(u => u.DisplayName).ToList();
            List<string> ids = new List<string>();
            List<string> labels = new List<string>();
            if (includeNone)
            {
                ids.Add(string.Empty);
                labels.Add("None");
            }
            for (int i = 0; i < utilities.Count; i++)
            {
                ids.Add(utilities[i].Id);
                labels.Add(utilities[i].DisplayName + " (" + utilities[i].Id + ")");
            }
            UtilityOptions.key = key;
            UtilityOptions.ids = ids.ToArray();
            UtilityOptions.labels = labels.ToArray();
        }

        private static void EnsureFutureUtilityOptions(bool includeNone)
        {
            PungentNoteDatabase database = PungentNoteStorage.Database;
            int count = database.futureUtilities == null ? 0 : database.futureUtilities.Count;
            string key = "none:" + includeNone + "|saved:" + (database.lastSavedUtc ?? string.Empty) + "|count:" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (string.Equals(FutureUtilityOptions.key, key, StringComparison.Ordinal))
                return;

            List<PungentFutureUtilityRecord> records = database.futureUtilities.Where(f => f != null).OrderBy(f => f.displayName).ToList();
            List<string> ids = new List<string>();
            List<string> labels = new List<string>();
            if (includeNone)
            {
                ids.Add(string.Empty);
                labels.Add("None");
            }
            for (int i = 0; i < records.Count; i++)
            {
                ids.Add(records[i].id);
                labels.Add(records[i].displayName);
            }
            FutureUtilityOptions.key = key;
            FutureUtilityOptions.ids = ids.ToArray();
            FutureUtilityOptions.labels = labels.ToArray();
        }

        private static void EnsureDocumentationLinkOptions(bool includeNone, string currentId)
        {
            List<PungentUtilityDocumentationLinks.DocumentationLink> allLinks = PungentUtilityDocumentationLinks.instance.GetAll().ToList();
            int hash = 17;
            for (int i = 0; i < allLinks.Count; i++)
                if (allLinks[i] != null)
                    hash = hash * 31 + (allLinks[i].id ?? string.Empty).GetHashCode();

            string key = "none:" + includeNone + "|count:" + allLinks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|hash:" + hash.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|current:" + (currentId ?? string.Empty);
            if (string.Equals(DocumentationLinkOptions.key, key, StringComparison.Ordinal))
                return;

            List<PungentUtilityDocumentationLinks.DocumentationLink> links = allLinks
                .Where(link => link != null)
                .OrderBy(PungentUtilityDocumentationLinks.GetDisplayName)
                .ToList();
            List<string> ids = new List<string>();
            List<string> labels = new List<string>();
            if (includeNone)
            {
                ids.Add(string.Empty);
                labels.Add("None");
            }
            for (int i = 0; i < links.Count; i++)
            {
                ids.Add(links[i].id);
                labels.Add(PungentUtilityDocumentationLinks.GetDisplayName(links[i]));
            }

            if (!string.IsNullOrWhiteSpace(currentId) && !ids.Any(id => string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                ids.Add(currentId);
                labels.Add(currentId + " (missing)");
            }

            DocumentationLinkOptions.key = key;
            DocumentationLinkOptions.ids = ids.ToArray();
            DocumentationLinkOptions.labels = labels.ToArray();
        }

        private static string ShortDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";
            return DateTime.TryParse(value, out DateTime parsed)
                ? parsed.ToLocalTime().ToString("g")
                : value;
        }

        private static float OverlayWidth(PungentStickyNoteOverlayState state)
        {
            if (state != null && state.activeRect.width > 1f)
                return state.activeRect.width;
            return EditorGUIUtility.currentViewWidth;
        }
    }
#endif
}
