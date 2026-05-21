using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    internal sealed class PungentRichDocumentOverlayPreviewDrawer : IPungentAuthoringOverlayPreviewDrawer
    {
        private static GUIStyle _displayTextStyle;
        private static GUIStyle _headingStyle;
        private static GUIStyle _codeStyle;
        private static GUIStyle _rawStyle;
        private static GUIStyle _semanticLabelStyle;
        private static string _cachedParsedDocumentId;
        private static string _cachedParsedBody;
        private static PungentRichDocumentParsedDocument _cachedParsedDocument;
        private static readonly RichDocumentPreviewCache PreviewCache = new RichDocumentPreviewCache();

        private sealed class PreviewRow
        {
            public string text = string.Empty;
            public GUIStyle style;
            public float height;
            public bool divider;
            public Color accent;
        }

        private sealed class RichDocumentPreviewCache
        {
            public string key = string.Empty;
            public readonly List<PreviewRow> rows = new List<PreviewRow>();
            public float contentHeight = 140f;
        }

        static PungentRichDocumentOverlayPreviewDrawer()
        {
            PungentAuthoringOverlayPreviewRegistry.Register(new PungentRichDocumentOverlayPreviewDrawer());
        }

        public bool CanDraw(PungentAuthoringReference reference)
        {
            return reference != null &&
                   (reference.itemKind == PungentAuthoringItemKind.RichDocument ||
                    string.Equals(reference.providerId, PungentRichDocumentProvider.Id, System.StringComparison.OrdinalIgnoreCase));
        }

        public void Draw(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state)
        {
            long drawSample = PungentAuthoringOverlayPerformance.BeginSample();
            PungentRichDocument document = PungentRichDocumentStorage.Database.Find(reference.itemId);
            if (document == null)
            {
                EditorGUILayout.LabelField("Rich document missing", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The linked rich document could not be found.", UtilityWindowTheme.MutedMiniLabelStyle);
                PungentAuthoringOverlayPerformance.EndSample("Rich Document Overlay Draw", drawSample);
                return;
            }

            EnsureStyles();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int mode = GUILayout.Toolbar(state.richDocumentRawMode ? 1 : 0, new[] { "Display", "Raw" }, EditorStyles.toolbarButton, GUILayout.Width(132f));
                state.richDocumentRawMode = mode == 1;
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(document.kind, UtilityWindowTheme.PathLabelStyle, GUILayout.MaxWidth(120f));
            }

            Rect scrollRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.MinWidth(1f), GUILayout.MinHeight(120f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (scrollRect.width <= 1f || scrollRect.height <= 1f)
            {
                PungentAuthoringOverlayPerformance.EndSample("Rich Document Overlay Draw", drawSample);
                return;
            }

            float contentWidth = Mathf.Max(120f, scrollRect.width - 18f);
            RichDocumentPreviewCache cache = GetPreviewCache(document, state.richDocumentRawMode, contentWidth);
            float contentHeight = cache.contentHeight;
            Rect viewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(scrollRect.height, contentHeight));
            state.authoringScroll = GUI.BeginScrollView(scrollRect, state.authoringScroll, viewRect, false, true);
            try
            {
                EditorGUI.DrawRect(new Rect(0f, 0f, viewRect.width, viewRect.height), EditorGUIUtility.isProSkin ? new Color(0.105f, 0.11f, 0.118f, 1f) : new Color(0.93f, 0.94f, 0.95f, 1f));
                Rect visibleRect = new Rect(0f, state.authoringScroll.y, contentWidth, scrollRect.height + 48f);
                if (state.richDocumentRawMode)
                    DrawRaw(document, new Rect(0f, 0f, contentWidth, viewRect.height), visibleRect);
                else
                    DrawDisplay(cache.rows, new Rect(0f, 0f, contentWidth, viewRect.height), visibleRect);
            }
            finally
            {
                GUI.EndScrollView();
                PungentAuthoringOverlayPerformance.EndSample("Rich Document Overlay Draw", drawSample, cache.rows.Count);
            }
        }

        private static void DrawRaw(PungentRichDocument document, Rect rect, Rect visibleRect)
        {
            string raw = string.IsNullOrWhiteSpace(document.bodyText) ? "(No raw document body text.)" : document.bodyText;
            Rect textRect = new Rect(rect.x + 8f, rect.y + 8f, Mathf.Max(80f, rect.width - 16f), Mathf.Max(48f, rect.height - 16f));
            if (!textRect.Overlaps(visibleRect, true))
                return;
            EditorGUI.SelectableLabel(textRect, raw, _rawStyle);
        }

        private static void DrawDisplay(List<PreviewRow> rows, Rect rect, Rect visibleRect)
        {
            float width = Mathf.Max(80f, rect.width - 16f);
            float y = rect.y + 8f;
            Rect panelRect = new Rect(rect.x + 4f, rect.y + 4f, Mathf.Max(80f, rect.width - 8f), Mathf.Max(80f, rect.height - 8f));
            EditorGUI.DrawRect(panelRect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.135f, 0.145f, 1f) : new Color(0.97f, 0.975f, 0.98f, 1f));

            if (rows == null || rows.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 12f, y, width, 22f), "Start writing this document...", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                PreviewRow row = rows[i];
                Rect rowRect = new Rect(rect.x + 12f, y, width, row.height);
                if (!rowRect.Overlaps(visibleRect, true))
                {
                    y += row.height + 2f;
                    continue;
                }

                if (row.divider)
                {
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y + 6f, rowRect.width, 1f), EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.18f));
                }
                else
                {
                    if (row.accent.a > 0f)
                    {
                        Rect accentRect = new Rect(rowRect.x, rowRect.y, rowRect.width, rowRect.height);
                        EditorGUI.DrawRect(accentRect, row.accent);
                    }

                    GUI.Label(rowRect, row.text, row.style ?? _displayTextStyle);
                }

                y += row.height + 2f;
            }
        }

        private static List<PreviewRow> BuildDisplayRows(PungentRichDocument document, float width)
        {
            List<PreviewRow> rows = new List<PreviewRow>();
            PungentRichDocumentParsedDocument parsed = GetParsedDocument(document);
            if (parsed != null && parsed.HasLines)
            {
                for (int i = 0; i < parsed.lines.Count; i++)
                    AddDisplayLine(rows, parsed.lines[i], width);
            }

            AddStructuredCueRows(rows, document, width);
            return rows;
        }

        private static void AddDisplayLine(List<PreviewRow> rows, PungentRichDocumentParsedLine line, float width)
        {
            if (line == null)
                return;

            if (line.kind == PungentRichDocumentParsedLineKind.Blank)
            {
                rows.Add(new PreviewRow { height = 8f });
                return;
            }

            string text = !string.IsNullOrWhiteSpace(line.text) ? line.text : line.rawText;
            GUIStyle style = _displayTextStyle;
            if (line.kind == PungentRichDocumentParsedLineKind.Heading)
                style = _headingStyle;
            else if (line.kind == PungentRichDocumentParsedLineKind.Code || line.isInsideCodeBlock)
                style = _codeStyle;

            if (line.kind == PungentRichDocumentParsedLineKind.Divider)
            {
                rows.Add(new PreviewRow { divider = true, height = 12f });
                return;
            }

            Color accent = Color.clear;
            if (line.kind == PungentRichDocumentParsedLineKind.Bullet)
                text = "- " + text;
            else if (line.kind == PungentRichDocumentParsedLineKind.Checklist)
                text = (line.isChecked ? "[x] " : "[ ] ") + text;
            else if (line.kind == PungentRichDocumentParsedLineKind.Quote)
            {
                text = "> " + text;
                accent = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.035f);
            }
            else if (line.kind == PungentRichDocumentParsedLineKind.SpeakerLine)
            {
                text = string.IsNullOrWhiteSpace(line.speaker) ? text : line.speaker + ": " + text;
                accent = new Color(UtilityWindowTheme.Purple.r, UtilityWindowTheme.Purple.g, UtilityWindowTheme.Purple.b, 0.08f);
            }
            else if (line.kind == PungentRichDocumentParsedLineKind.CommandPlaceholder)
            {
                text = string.IsNullOrWhiteSpace(line.commandKey) ? text : "Command: " + line.commandKey;
                accent = new Color(UtilityWindowTheme.Amber.r, UtilityWindowTheme.Amber.g, UtilityWindowTheme.Amber.b, 0.08f);
            }
            else if (!string.IsNullOrWhiteSpace(line.label) &&
                     (line.kind == PungentRichDocumentParsedLineKind.DialogueChoice ||
                      line.kind == PungentRichDocumentParsedLineKind.QuestObjective ||
                      line.kind == PungentRichDocumentParsedLineKind.TutorialStep ||
                      line.kind == PungentRichDocumentParsedLineKind.GameCopy))
            {
                text = line.label + ": " + text;
                accent = new Color(UtilityWindowTheme.Teal.r, UtilityWindowTheme.Teal.g, UtilityWindowTheme.Teal.b, 0.07f);
            }

            float height = Mathf.Max(20f, style.CalcHeight(new GUIContent(text), Mathf.Max(80f, width)) + 2f);
            rows.Add(new PreviewRow { text = text, style = style, height = height, accent = accent });
        }

        private static void AddStructuredCueRows(List<PreviewRow> rows, PungentRichDocument document, float width)
        {
            if (document == null || document.blocks == null)
                return;

            int drawn = 0;
            for (int i = 0; i < document.blocks.Count && drawn < 18; i++)
            {
                PungentRichDocumentBlock block = document.blocks[i];
                if (!IsStructuredCueBlock(block))
                    continue;

                if (drawn == 0)
                {
                    rows.Add(new PreviewRow { height = 8f });
                    rows.Add(new PreviewRow { text = "Structured cues", style = _semanticLabelStyle, height = 20f });
                }

                string text = BlockChipLabel(block) + ": " + BlockTextLabel(block);
                rows.Add(new PreviewRow
                {
                    text = text,
                    style = _displayTextStyle,
                    height = Mathf.Max(22f, _displayTextStyle.CalcHeight(new GUIContent(text), Mathf.Max(80f, width)) + 4f),
                    accent = new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.07f)
                });
                drawn++;
            }
        }

        private static RichDocumentPreviewCache GetPreviewCache(PungentRichDocument document, bool rawMode, float width)
        {
            if (document == null)
            {
                PreviewCache.key = "missing";
                PreviewCache.rows.Clear();
                PreviewCache.contentHeight = 80f;
                return PreviewCache;
            }

            int widthBucket = Mathf.RoundToInt(Mathf.Max(80f, width) / 24f);
            string body = document.bodyText ?? string.Empty;
            string key = string.Join("|",
                document.id ?? string.Empty,
                document.updatedUtc ?? string.Empty,
                body.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture),
                document.blocks == null ? "b0" : "b" + document.blocks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rawMode ? "raw" : "display",
                widthBucket.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (string.Equals(PreviewCache.key, key, StringComparison.Ordinal))
                return PreviewCache;

            PreviewCache.key = key;
            PreviewCache.rows.Clear();
            if (rawMode)
            {
                string raw = string.IsNullOrWhiteSpace(document.bodyText) ? "(No raw document body text.)" : document.bodyText;
                PreviewCache.contentHeight = Mathf.Max(80f, _rawStyle.CalcHeight(new GUIContent(raw), Mathf.Max(80f, width - 16f)) + 28f);
                return PreviewCache;
            }

            List<PreviewRow> rows = BuildDisplayRows(document, Mathf.Max(80f, width - 16f));
            PreviewCache.rows.AddRange(rows);
            float height = 20f;
            for (int i = 0; i < rows.Count; i++)
                height += rows[i].height + 2f;
            PreviewCache.contentHeight = Mathf.Max(140f, height);
            return PreviewCache;
        }

        private static PungentRichDocumentParsedDocument GetParsedDocument(PungentRichDocument document)
        {
            if (document == null)
                return null;

            string id = document.id ?? string.Empty;
            string body = document.bodyText ?? string.Empty;
            if (string.Equals(_cachedParsedDocumentId, id, StringComparison.Ordinal) &&
                string.Equals(_cachedParsedBody, body, StringComparison.Ordinal))
                return _cachedParsedDocument;

            _cachedParsedDocumentId = id;
            _cachedParsedBody = body;
            _cachedParsedDocument = PungentRichDocumentParser.Parse(document);
            return _cachedParsedDocument;
        }

        private static bool IsStructuredCueBlock(PungentRichDocumentBlock block)
        {
            return block != null &&
                   (block.type == PungentRichDocumentBlockType.TokenReference ||
                    block.type == PungentRichDocumentBlockType.LinkReference ||
                    block.type == PungentRichDocumentBlockType.SpeakerLine ||
                    block.type == PungentRichDocumentBlockType.DialogueChoice ||
                    block.type == PungentRichDocumentBlockType.QuestObjective ||
                    block.type == PungentRichDocumentBlockType.TutorialStep ||
                    block.type == PungentRichDocumentBlockType.CommandPlaceholder ||
                    block.type == PungentRichDocumentBlockType.VariablePlaceholder ||
                    block.convention != PungentRichDocumentBlockConvention.None);
        }

        private static string BlockChipLabel(PungentRichDocumentBlock block)
        {
            if (block == null)
                return "Block";
            if (block.convention != PungentRichDocumentBlockConvention.None)
                return block.convention.ToString();
            return block.type.ToString();
        }

        private static string BlockTextLabel(PungentRichDocumentBlock block)
        {
            if (block == null)
                return string.Empty;
            if (block.type == PungentRichDocumentBlockType.TokenReference || block.type == PungentRichDocumentBlockType.VariablePlaceholder)
                return "{" + block.tokenKey + "}";
            if (block.type == PungentRichDocumentBlockType.SpeakerLine && !string.IsNullOrWhiteSpace(block.speaker))
                return block.speaker + ": " + block.text;
            if (!string.IsNullOrWhiteSpace(block.text))
                return block.text;
            return block.type.ToString();
        }

        private static void EnsureStyles()
        {
            if (_displayTextStyle != null)
                return;

            _displayTextStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                wordWrap = true,
                richText = false,
                padding = new RectOffset(8, 8, 4, 4)
            };
            _headingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true,
                richText = false,
                fontSize = 14,
                padding = new RectOffset(8, 8, 6, 4)
            };
            _codeStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                padding = new RectOffset(8, 8, 5, 5)
            };
            _rawStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                padding = new RectOffset(8, 8, 6, 6)
            };
            _semanticLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                wordWrap = true,
                richText = false,
                normal = { textColor = UtilityWindowTheme.MutedText }
            };
        }
    }
#endif
}
