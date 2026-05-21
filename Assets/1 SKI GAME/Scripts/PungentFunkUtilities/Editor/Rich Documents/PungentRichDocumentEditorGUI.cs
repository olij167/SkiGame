using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentEditorGUI
    {
        private static readonly Regex TokenRegex = new Regex("\\{([^{}]+)\\}", RegexOptions.Compiled);
        private static readonly Regex ValidTokenKeyRegex = new Regex("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

        public static Color StatusTint(string status)
        {
            if (Contains(status, "complete") || Contains(status, "published"))
                return UtilityWindowTheme.Green;
            if (Contains(status, "progress") || Contains(status, "review"))
                return UtilityWindowTheme.Cyan;
            if (Contains(status, "blocked") || Contains(status, "stale"))
                return UtilityWindowTheme.Red;
            if (Contains(status, "draft"))
                return UtilityWindowTheme.Amber;
            return UtilityWindowTheme.Blue;
        }

        public static Color PriorityTint(string priority)
        {
            if (Contains(priority, "crucial") || Contains(priority, "important"))
                return UtilityWindowTheme.Amber;
            if (Contains(priority, "low") || Contains(priority, "nice"))
                return UtilityWindowTheme.Teal;
            return UtilityWindowTheme.Neutral;
        }

        public static void DrawDocumentPills(PungentRichDocument document)
        {
            if (document == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(document.kind, UtilityWindowTheme.Blue, 92f);
                UtilityWindowTheme.CountPill(document.status, StatusTint(document.status), 104f);
                UtilityWindowTheme.CountPill(document.priority, PriorityTint(document.priority), 100f);
                if (document.archived)
                    UtilityWindowTheme.CountPill("Archived", UtilityWindowTheme.Neutral, 78f);
                if (document.developerOnly)
                    UtilityWindowTheme.CountPill("Developer", UtilityWindowTheme.Purple, 88f);
                GUILayout.FlexibleSpace();
            }
        }

        public static string DrawTagField(string label, List<string> tags)
        {
            string current = tags == null ? string.Empty : string.Join(", ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray());
            return EditorGUILayout.TextField(label, current);
        }

        public static List<string> ParseTags(string text)
        {
            return PungentAuthoringMetadata.NormalizeTags((text ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static void DrawTags(List<string> tags)
        {
            List<string> visible = PungentAuthoringMetadata.NormalizeTags(tags).Take(6).ToList();
            if (visible.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < visible.Count; i++)
                    UtilityWindowTheme.CountPill("#" + visible[i], UtilityWindowTheme.Teal, Mathf.Clamp(48f + visible[i].Length * 7f, 62f, 150f));
                GUILayout.FlexibleSpace();
            }
        }

        public static void DrawPreview(PungentRichDocument document, GUIStyle previewStyle, float minHeight)
        {
            DrawParsedPreview(document, PungentRichDocumentParser.Parse(document), previewStyle, minHeight);
        }

        public static void DrawParsedPreview(PungentRichDocument document, PungentRichDocumentParsedDocument parsed, GUIStyle previewStyle, float minHeight)
        {
            using (new EditorGUILayout.VerticalScope(previewStyle, GUILayout.MinHeight(minHeight), GUILayout.ExpandHeight(true)))
            {
                if (parsed == null || !parsed.HasLines)
                {
                    DrawStructuredBlocks(document);
                    if (document == null || document.blocks == null || document.blocks.Count == 0)
                        EditorGUILayout.LabelField("Start writing this document...", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                foreach (PungentRichDocumentParsedLine line in parsed.lines)
                    DrawParsedPreviewLine(line, previewStyle);

                DrawStructuredBlocks(document);
            }
        }

        public static void DrawStructuredBlocks(PungentRichDocument document)
        {
            if (document == null || document.blocks == null)
                return;

            List<PungentRichDocumentBlock> referenceBlocks = document.blocks
                .Where(IsStructuredCueBlock)
                .Take(18)
                .ToList();
            if (referenceBlocks.Count == 0)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Structured cues", UtilityWindowTheme.SectionHeaderStyle);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.08f, 0.04f, 5, 2)))
            {
                foreach (PungentRichDocumentBlock block in referenceBlocks)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string label = BlockTextLabel(block);
                        UtilityWindowTheme.CountPill(BlockChipLabel(block), UtilityWindowTheme.Purple, 132f);
                        EditorGUILayout.LabelField(label, UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
            }
        }

        public static void DrawOutline(PungentRichDocument document)
        {
            if (document == null)
            {
                EditorGUILayout.HelpBox("Select or create a document.", MessageType.Info);
                return;
            }

            List<string> headings = ExtractHeadings(document).Take(40).ToList();
            if (headings.Count == 0)
            {
                EditorGUILayout.LabelField("No headings yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            foreach (string heading in headings)
                EditorGUILayout.LabelField(heading, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        public static PungentAuthoringValidationResult ValidateLocal(PungentRichDocument document, string providerId)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = providerId ?? string.Empty,
                itemId = document == null ? string.Empty : document.id,
                status = PungentAuthoringValidationStatus.Valid
            };

            if (document == null)
            {
                result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Error, "Document is missing.", providerId, null, null, null, null, "MISSING_DOCUMENT"));
                return result;
            }

            if (string.IsNullOrWhiteSpace(document.title))
                result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Error, "Document title is missing.", providerId, document.id, null, "Add a title.", null, "MISSING_TITLE"));

            if (!document.HasBody)
                result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Document body is empty.", providerId, document.id, null, "Add body text or blocks.", null, "EMPTY_BODY"));

            foreach (PungentAuthoringTarget target in document.targets ?? new List<PungentAuthoringTarget>())
            {
                if (target == null)
                    continue;

                if (!target.HasTarget)
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "A target reference is missing its value.", providerId, document.id, target, "Fill or remove this target.", null, "MISSING_TARGET"));
                else if (target.targetKind == PungentAuthoringTargetKind.AssetGuid && string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(target.rawValue)))
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Asset GUID target could not be resolved.", providerId, document.id, target, "Check the linked asset.", target.rawValue, "STALE_TARGET"));
            }

            foreach (PungentRichDocumentSemanticBinding binding in document.semanticBindings ?? new List<PungentRichDocumentSemanticBinding>())
            {
                if (binding == null)
                    continue;

                if (binding.target != null &&
                    binding.target.targetKind == PungentAuthoringTargetKind.SerializedPropertyPath &&
                    string.IsNullOrWhiteSpace(binding.target.propertyPath))
                {
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Semantic binding is missing its string property path.", providerId, document.id, binding.target, "Bind a string field in the Properties tray.", binding.id, "MISSING_SEMANTIC_TARGET_PROPERTY"));
                }
                else if (binding.target != null &&
                         binding.target.HasTarget &&
                         !PungentRichDocumentBindingApplicationService.CanApply(binding, out string bindingDisabledReason))
                {
                    result.AddIssue(PungentAuthoringValidationIssue.Create(
                        PungentAuthoringValidationSeverity.Warning,
                        "Semantic binding cannot currently apply to its target. " + bindingDisabledReason,
                        providerId,
                        document.id,
                        binding.target,
                        "Review the binding in the Properties tray.",
                        binding.id,
                        "UNSUPPORTED_SEMANTIC_BINDING_TARGET"));
                }
            }

            foreach (string invalidToken in InvalidTokenKeys(document.bodyText))
            {
                string source = "{" + invalidToken + "}";
                if (!IsSuppressed(document, "INVALID_TOKEN_SYNTAX", source))
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Invalid token syntax: {" + invalidToken + "}", providerId, document.id, null, "Use letters, numbers, underscore, dot, or dash.", invalidToken, "INVALID_TOKEN_SYNTAX"));
            }

            foreach (PungentRichDocumentParsedLine line in PungentRichDocumentParser.Parse(document).lines)
            {
                if (line == null)
                    continue;

                if (line.kind == PungentRichDocumentParsedLineKind.SpeakerLine && string.IsNullOrWhiteSpace(line.text) && !IsSuppressed(document, "EMPTY_SPEAKER_LINE", line.rawText))
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Speaker line is missing dialogue text.", providerId, document.id, null, "Add dialogue after the speaker name.", line.rawText, "EMPTY_SPEAKER_LINE"));
                if (line.kind == PungentRichDocumentParsedLineKind.DialogueChoice && string.IsNullOrWhiteSpace(line.text) && !IsSuppressed(document, "EMPTY_DIALOGUE_CHOICE", line.rawText))
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Dialogue choice is empty.", providerId, document.id, null, "Add choice text after [choice].", line.rawText, "EMPTY_DIALOGUE_CHOICE"));
                if (line.kind == PungentRichDocumentParsedLineKind.QuestObjective && string.IsNullOrWhiteSpace(line.text) && !IsSuppressed(document, "EMPTY_QUEST_OBJECTIVE", line.rawText))
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Quest objective is empty.", providerId, document.id, null, "Add objective text after [objective].", line.rawText, "EMPTY_QUEST_OBJECTIVE"));
                if (line.kind == PungentRichDocumentParsedLineKind.CommandPlaceholder && string.IsNullOrWhiteSpace(line.commandKey) && !IsSuppressed(document, "EMPTY_COMMAND_PLACEHOLDER", line.rawText))
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Command placeholder is missing its command key.", providerId, document.id, null, "Use <command:command_key>.", line.rawText, "EMPTY_COMMAND_PLACEHOLDER"));
            }

            HashSet<string> blockIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentRichDocumentBlock block in document.blocks ?? new List<PungentRichDocumentBlock>())
            {
                if (block == null)
                    continue;

                if (string.IsNullOrWhiteSpace(block.id))
                {
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "A structured block is missing its ID.", providerId, document.id, null, "Save the document to normalize block IDs.", null, "MISSING_BLOCK_ID"));
                }
                else if (!blockIds.Add(block.id))
                {
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Duplicate structured block ID: " + block.id, providerId, document.id, null, "Save the document to refresh duplicate block IDs.", block.id, "DUPLICATE_BLOCK_ID"));
                }

                foreach (string invalidToken in InvalidTokenKeys(block.text))
                {
                    string source = "{" + invalidToken + "}";
                    if (!IsSuppressed(document, "INVALID_TOKEN_SYNTAX", source))
                        result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Invalid block token syntax: {" + invalidToken + "}", providerId, document.id, null, "Use letters, numbers, underscore, dot, or dash.", invalidToken, "INVALID_TOKEN_SYNTAX"));
                }

                string tokenKey = (block.tokenKey ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(tokenKey) && !IsValidTokenKey(tokenKey) && !IsSuppressed(document, "INVALID_TOKEN_SYNTAX", "{" + tokenKey + "}"))
                    result.AddIssue(PungentAuthoringValidationIssue.Create(PungentAuthoringValidationSeverity.Warning, "Invalid token reference key: {" + tokenKey + "}", providerId, document.id, null, "Use letters, numbers, underscore, dot, or dash.", tokenKey, "INVALID_TOKEN_SYNTAX"));
            }

            result.RefreshStatus();
            return result;
        }

        public static List<string> ExtractTokenKeys(string text)
        {
            return TokenRegex.Matches(text ?? string.Empty)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Where(token => !IsCustomChipToken(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int BodyWordCount(string body)
        {
            return string.IsNullOrWhiteSpace(body)
                ? 0
                : body.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public static string ShortDate(string utc)
        {
            if (DateTime.TryParse(utc, out DateTime parsed))
                return parsed.ToLocalTime().ToString("MMM d, HH:mm");
            return string.IsNullOrWhiteSpace(utc) ? "Never" : utc;
        }

        private static void DrawPreviewLine(string line, bool codeBlock, GUIStyle previewStyle)
        {
            string trimmed = (line ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                EditorGUILayout.Space(6f);
                return;
            }

            if (codeBlock)
            {
                EditorGUILayout.SelectableLabel(line, EditorStyles.helpBox, GUILayout.MinHeight(20f));
                return;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                int level = trimmed.TakeWhile(ch => ch == '#').Count();
                string label = trimmed.Substring(level).Trim();
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = level <= 1 ? 18 : 14,
                    wordWrap = true
                };
                EditorGUILayout.LabelField(label, style);
                DrawInlineTokenChips(trimmed);
                return;
            }

            if (trimmed.StartsWith("- [ ]", StringComparison.Ordinal) || trimmed.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
            {
                bool isDone = trimmed.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.Toggle(isDone, GUILayout.Width(18f));
                    EditorGUILayout.LabelField(trimmed.Substring(5).Trim(), previewStyle);
                }
                DrawInlineTokenChips(trimmed);
                return;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                EditorGUILayout.LabelField("- " + trimmed.Substring(2), previewStyle);
                DrawInlineTokenChips(trimmed);
                return;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 5, 2)))
                    EditorGUILayout.LabelField(trimmed.TrimStart('>').Trim(), previewStyle);
                DrawInlineTokenChips(trimmed);
                return;
            }

            EditorGUILayout.LabelField(trimmed, previewStyle);
            DrawInlineTokenChips(trimmed);
        }

        private static void DrawParsedPreviewLine(PungentRichDocumentParsedLine line, GUIStyle previewStyle)
        {
            if (line == null)
                return;

            if (line.kind == PungentRichDocumentParsedLineKind.Blank)
            {
                EditorGUILayout.Space(6f);
                return;
            }

            switch (line.kind)
            {
                case PungentRichDocumentParsedLineKind.SpeakerLine:
                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.08f, 0.04f, 5, 3)))
                    {
                        EditorGUILayout.LabelField(line.speaker, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(line.text, previewStyle);
                    }
                    DrawInlineTokenChips(line.rawText);
                    return;
                case PungentRichDocumentParsedLineKind.DialogueChoice:
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.06f, 0.03f, 4, 2)))
                    {
                        UtilityWindowTheme.CountPill("Choice", UtilityWindowTheme.Purple, 72f);
                        EditorGUILayout.LabelField(line.text, previewStyle);
                    }
                    DrawInlineTokenChips(line.rawText);
                    return;
                case PungentRichDocumentParsedLineKind.QuestObjective:
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green, 0.06f, 0.03f, 4, 2)))
                    {
                        UtilityWindowTheme.CountPill("Objective", UtilityWindowTheme.Green, 86f);
                        EditorGUILayout.LabelField(line.text, previewStyle);
                    }
                    DrawInlineTokenChips(line.rawText);
                    return;
                case PungentRichDocumentParsedLineKind.TutorialStep:
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.06f, 0.03f, 4, 2)))
                    {
                        UtilityWindowTheme.CountPill("Step", UtilityWindowTheme.Blue, 58f);
                        EditorGUILayout.LabelField(line.text, previewStyle);
                    }
                    DrawInlineTokenChips(line.rawText);
                    return;
                case PungentRichDocumentParsedLineKind.CommandPlaceholder:
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.06f, 0.03f, 4, 2)))
                    {
                        UtilityWindowTheme.CountPill("Command", UtilityWindowTheme.Amber, 82f);
                        EditorGUILayout.LabelField(line.commandKey, previewStyle);
                    }
                    return;
                case PungentRichDocumentParsedLineKind.GameCopy:
                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.06f, 0.03f, 4, 2)))
                    {
                        UtilityWindowTheme.CountPill(line.label, UtilityWindowTheme.Teal, Mathf.Clamp(58f + line.label.Length * 7f, 72f, 140f));
                        EditorGUILayout.LabelField(line.text, previewStyle);
                    }
                    DrawInlineTokenChips(line.rawText);
                    return;
                default:
                    DrawPreviewLine(line.rawText, line.isInsideCodeBlock, previewStyle);
                    return;
            }
        }

        private static void DrawInlineTokenChips(string line)
        {
            List<string> tokens = ExtractTokenKeys(line);
            if (tokens.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (string token in tokens.Take(8))
                    UtilityWindowTheme.CountPill("{" + token + "}", IsValidTokenKey(token) ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Amber, Mathf.Clamp(48f + token.Length * 7f, 72f, 170f));
                GUILayout.FlexibleSpace();
            }
        }

        private static IEnumerable<string> ExtractHeadings(PungentRichDocument document)
        {
            if (!string.IsNullOrWhiteSpace(document.bodyText))
            {
                string[] lines = document.bodyText.Replace("\r\n", "\n").Split('\n');
                foreach (string raw in lines)
                {
                    string trimmed = (raw ?? string.Empty).Trim();
                    if (!trimmed.StartsWith("#", StringComparison.Ordinal))
                        continue;
                    int level = trimmed.TakeWhile(ch => ch == '#').Count();
                    string title = trimmed.Substring(level).Trim();
                    if (!string.IsNullOrWhiteSpace(title))
                        yield return new string(' ', Math.Max(0, level - 1) * 2) + title;
                }
            }

            foreach (PungentRichDocumentBlock block in document.blocks ?? new List<PungentRichDocumentBlock>())
            {
                if (block != null && block.type == PungentRichDocumentBlockType.Heading && !string.IsNullOrWhiteSpace(block.text))
                    yield return new string(' ', Math.Max(0, block.headingLevel - 1) * 2) + block.text.Trim();
            }
        }

        private static IEnumerable<string> InvalidTokenKeys(string text)
        {
            return ExtractTokenKeys(text).Where(token => !IsValidTokenKey(token));
        }

        private static bool IsCustomChipToken(string token)
        {
            return (token ?? string.Empty).Trim().StartsWith("chip:", StringComparison.OrdinalIgnoreCase);
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

        private static bool IsValidTokenKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && ValidTokenKeyRegex.IsMatch(key.Trim());
        }

        private static bool IsSuppressed(PungentRichDocument document, string issueCode, string source)
        {
            return document != null && document.IsValidationSuppressed(issueCode, source);
        }

        private static bool Contains(string source, string needle)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
#endif
}
