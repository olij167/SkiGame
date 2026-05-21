using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public enum PungentRichDocumentTextRunKind
    {
        Text = 0,
        Token = 10,
        Semantic = 20
    }

    public sealed class PungentRichDocumentSemanticSpan
    {
        public string id = string.Empty;
        public PungentRichDocumentSemanticKind kind = PungentRichDocumentSemanticKind.None;
        public string tagName = string.Empty;
        public int startIndex;
        public int length;
        public string sourceText = string.Empty;
        public string innerText = string.Empty;
        public List<PungentRichDocumentSemanticField> fields = new List<PungentRichDocumentSemanticField>();

        public string Fingerprint => PungentRichDocumentValidationSuppression.ComputeFingerprint(sourceText);

        public string GetField(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || fields == null)
                return string.Empty;

            PungentRichDocumentSemanticField field = fields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return field == null ? string.Empty : field.value ?? string.Empty;
        }
    }

    public sealed class PungentRichDocumentTextRun
    {
        public PungentRichDocumentTextRunKind kind = PungentRichDocumentTextRunKind.Text;
        public int startIndex;
        public int length;
        public string text = string.Empty;
        public PungentRichDocumentCanvasTokenRun token;
        public PungentRichDocumentSemanticSpan semantic;
    }

    public static class PungentRichDocumentSemanticParser
    {
        private static readonly Regex SemanticTagRegex = new Regex(
            "\\[\\[([A-Za-z0-9_-]+)([^\\]]*)\\]\\](.*?)\\[\\[/\\1\\]\\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex AttributeRegex = new Regex(
            "([A-Za-z0-9_.-]+)\\s*=\\s*\"([^\"]*)\"|([A-Za-z0-9_.-]+)\\s*=\\s*'([^']*)'",
            RegexOptions.Compiled);

        public static List<PungentRichDocumentSemanticSpan> ExtractSemanticSpans(string text)
        {
            List<PungentRichDocumentSemanticSpan> spans = new List<PungentRichDocumentSemanticSpan>();
            foreach (Match match in SemanticTagRegex.Matches(text ?? string.Empty))
            {
                PungentRichDocumentSemanticKind kind = KindForTag(match.Groups[1].Value);
                if (kind == PungentRichDocumentSemanticKind.None)
                    continue;

                List<PungentRichDocumentSemanticField> fields = ParseFields(match.Groups[2].Value);
                string id = FieldValue(fields, "id");
                if (string.IsNullOrWhiteSpace(id))
                    id = Guid.NewGuid().ToString("N");

                spans.Add(new PungentRichDocumentSemanticSpan
                {
                    id = id,
                    kind = kind,
                    tagName = NormalizeTagName(match.Groups[1].Value),
                    startIndex = match.Index,
                    length = match.Length,
                    sourceText = match.Value,
                    innerText = match.Groups[3].Value ?? string.Empty,
                    fields = fields.Where(field => field != null && !string.Equals(field.key, "id", StringComparison.OrdinalIgnoreCase)).ToList()
                });
            }

            return spans;
        }

        public static List<PungentRichDocumentTextRun> ExtractRuns(string text)
        {
            string safe = text ?? string.Empty;
            List<PungentRichDocumentTextRun> runs = new List<PungentRichDocumentTextRun>();
            List<PungentRichDocumentSemanticSpan> semanticSpans = ExtractSemanticSpans(safe);
            int cursor = 0;

            foreach (PungentRichDocumentSemanticSpan span in semanticSpans.OrderBy(span => span.startIndex))
            {
                if (span.startIndex < cursor)
                    continue;

                AddPlainAndTokenRuns(runs, safe, cursor, span.startIndex - cursor);
                runs.Add(new PungentRichDocumentTextRun
                {
                    kind = PungentRichDocumentTextRunKind.Semantic,
                    startIndex = span.startIndex,
                    length = span.length,
                    text = span.innerText,
                    semantic = span
                });
                cursor = span.startIndex + span.length;
            }

            AddPlainAndTokenRuns(runs, safe, cursor, safe.Length - cursor);
            return runs;
        }

        public static bool TryParseWholeSemanticTag(string rawText, out PungentRichDocumentSemanticSpan span)
        {
            span = null;
            string safe = rawText ?? string.Empty;
            string trimmed = safe.Trim();
            if (trimmed.Length == 0)
                return false;

            List<PungentRichDocumentSemanticSpan> spans = ExtractSemanticSpans(trimmed);
            if (spans.Count != 1 || spans[0].startIndex != 0 || spans[0].length != trimmed.Length)
                return false;

            span = spans[0];
            return true;
        }

        public static string CreateSemanticTag(PungentRichDocumentSemanticKind kind, string innerText, IEnumerable<PungentRichDocumentSemanticField> fields = null, string id = null)
        {
            string tag = TagForKind(kind);
            if (string.IsNullOrWhiteSpace(tag))
                return innerText ?? string.Empty;

            List<PungentRichDocumentSemanticField> mergedFields = DefaultFieldsForKind(kind)
                .Concat(fields ?? Enumerable.Empty<PungentRichDocumentSemanticField>())
                .Where(field => field != null && !string.IsNullOrWhiteSpace(field.key))
                .GroupBy(field => field.key.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => PungentRichDocumentSemanticField.Create(group.Key, group.Last().value))
                .ToList();

            string cleanId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
            StringBuilder attributes = new StringBuilder();
            attributes.Append(" id=\"").Append(EscapeAttribute(cleanId)).Append('"');
            foreach (PungentRichDocumentSemanticField field in mergedFields)
            {
                if (string.Equals(field.key, "id", StringComparison.OrdinalIgnoreCase))
                    continue;

                attributes.Append(' ')
                    .Append(field.key.Trim())
                    .Append("=\"")
                    .Append(EscapeAttribute(field.value))
                    .Append('"');
            }

            return "[[" + tag + attributes + "]]" + (innerText ?? string.Empty) + "[[/" + tag + "]]";
        }

        public static string TagForKind(PungentRichDocumentSemanticKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentSemanticKind.Token: return "token";
                case PungentRichDocumentSemanticKind.DialogueLine: return "dialogue";
                case PungentRichDocumentSemanticKind.DialogueChoice: return "choice";
                case PungentRichDocumentSemanticKind.QuestObjective: return "quest-objective";
                case PungentRichDocumentSemanticKind.TutorialStep: return "tutorial-step";
                case PungentRichDocumentSemanticKind.GameCopy: return "game-copy";
                case PungentRichDocumentSemanticKind.Command: return "command";
                case PungentRichDocumentSemanticKind.HintCopy: return "hint";
                case PungentRichDocumentSemanticKind.ItemCopy: return "item-copy";
                case PungentRichDocumentSemanticKind.CharacterCopy: return "character-copy";
                case PungentRichDocumentSemanticKind.CustomChip: return "custom-chip";
                case PungentRichDocumentSemanticKind.CustomInsertion: return "custom-insertion";
                default: return string.Empty;
            }
        }

        public static PungentRichDocumentSemanticKind KindForTag(string tagName)
        {
            switch (NormalizeTagName(tagName))
            {
                case "token": return PungentRichDocumentSemanticKind.Token;
                case "dialogue":
                case "dialogue-line":
                case "speaker-line": return PungentRichDocumentSemanticKind.DialogueLine;
                case "choice":
                case "dialogue-choice": return PungentRichDocumentSemanticKind.DialogueChoice;
                case "objective":
                case "quest-objective": return PungentRichDocumentSemanticKind.QuestObjective;
                case "step":
                case "tutorial-step": return PungentRichDocumentSemanticKind.TutorialStep;
                case "copy":
                case "game-copy": return PungentRichDocumentSemanticKind.GameCopy;
                case "command": return PungentRichDocumentSemanticKind.Command;
                case "hint": return PungentRichDocumentSemanticKind.HintCopy;
                case "item":
                case "item-copy": return PungentRichDocumentSemanticKind.ItemCopy;
                case "character":
                case "character-copy": return PungentRichDocumentSemanticKind.CharacterCopy;
                case "chip":
                case "custom-chip": return PungentRichDocumentSemanticKind.CustomChip;
                case "custom":
                case "custom-insertion": return PungentRichDocumentSemanticKind.CustomInsertion;
                default: return PungentRichDocumentSemanticKind.None;
            }
        }

        public static PungentRichDocumentStreamNodeKind StreamKindForSemanticKind(PungentRichDocumentSemanticKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentSemanticKind.DialogueLine: return PungentRichDocumentStreamNodeKind.DialogueLine;
                case PungentRichDocumentSemanticKind.DialogueChoice: return PungentRichDocumentStreamNodeKind.DialogueChoice;
                case PungentRichDocumentSemanticKind.QuestObjective: return PungentRichDocumentStreamNodeKind.QuestObjective;
                case PungentRichDocumentSemanticKind.TutorialStep: return PungentRichDocumentStreamNodeKind.TutorialStep;
                case PungentRichDocumentSemanticKind.Command: return PungentRichDocumentStreamNodeKind.CommandPlaceholder;
                case PungentRichDocumentSemanticKind.GameCopy:
                case PungentRichDocumentSemanticKind.HintCopy:
                case PungentRichDocumentSemanticKind.ItemCopy:
                case PungentRichDocumentSemanticKind.CharacterCopy:
                case PungentRichDocumentSemanticKind.CustomInsertion:
                    return PungentRichDocumentStreamNodeKind.GameCopy;
                default:
                    return PungentRichDocumentStreamNodeKind.Text;
            }
        }

        public static PungentRichDocumentBlockType BlockTypeForSemanticKind(PungentRichDocumentSemanticKind kind)
        {
            return PungentRichDocumentDocumentStreamParser.BlockTypeForKind(StreamKindForSemanticKind(kind));
        }

        public static string DisplayName(PungentRichDocumentSemanticKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentSemanticKind.Token: return "Token";
                case PungentRichDocumentSemanticKind.DialogueLine: return "Dialogue";
                case PungentRichDocumentSemanticKind.DialogueChoice: return "Choice";
                case PungentRichDocumentSemanticKind.QuestObjective: return "Objective";
                case PungentRichDocumentSemanticKind.TutorialStep: return "Tutorial";
                case PungentRichDocumentSemanticKind.GameCopy: return "Game Copy";
                case PungentRichDocumentSemanticKind.Command: return "Command";
                case PungentRichDocumentSemanticKind.HintCopy: return "Hint";
                case PungentRichDocumentSemanticKind.ItemCopy: return "Item Copy";
                case PungentRichDocumentSemanticKind.CharacterCopy: return "Character Copy";
                case PungentRichDocumentSemanticKind.CustomChip: return "Chip";
                case PungentRichDocumentSemanticKind.CustomInsertion: return "Custom Insertion";
                default: return "Text";
            }
        }

        public static List<PungentRichDocumentSemanticField> DefaultFieldsForKind(PungentRichDocumentSemanticKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentSemanticKind.DialogueLine:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("speaker", "NPC"),
                        PungentRichDocumentSemanticField.Create("characterRef", string.Empty),
                        PungentRichDocumentSemanticField.Create("voiceKey", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.DialogueChoice:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("choiceKey", string.Empty),
                        PungentRichDocumentSemanticField.Create("destination", string.Empty),
                        PungentRichDocumentSemanticField.Create("condition", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.QuestObjective:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("questId", string.Empty),
                        PungentRichDocumentSemanticField.Create("objectiveId", string.Empty),
                        PungentRichDocumentSemanticField.Create("state", "Active")
                    };
                case PungentRichDocumentSemanticKind.TutorialStep:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("tutorialId", string.Empty),
                        PungentRichDocumentSemanticField.Create("stepId", string.Empty),
                        PungentRichDocumentSemanticField.Create("trigger", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.Command:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("key", "command_key"),
                        PungentRichDocumentSemanticField.Create("args", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.HintCopy:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("category", "Hint"),
                        PungentRichDocumentSemanticField.Create("copyKey", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.ItemCopy:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("category", "Item"),
                        PungentRichDocumentSemanticField.Create("itemId", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.CharacterCopy:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("category", "Character"),
                        PungentRichDocumentSemanticField.Create("characterId", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.GameCopy:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create("category", "General"),
                        PungentRichDocumentSemanticField.Create("copyKey", string.Empty)
                    };
                case PungentRichDocumentSemanticKind.CustomChip:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create(PungentRichDocumentInsertionDefinitionRegistry.DefinitionFieldKey, "custom-chip"),
                        PungentRichDocumentSemanticField.Create(PungentRichDocumentInsertionDefinitionRegistry.DefinitionLabelFieldKey, "Custom Chip")
                    };
                case PungentRichDocumentSemanticKind.CustomInsertion:
                    return new List<PungentRichDocumentSemanticField>
                    {
                        PungentRichDocumentSemanticField.Create(PungentRichDocumentInsertionDefinitionRegistry.DefinitionFieldKey, "custom-insertion"),
                        PungentRichDocumentSemanticField.Create(PungentRichDocumentInsertionDefinitionRegistry.DefinitionLabelFieldKey, "Custom Insertion")
                    };
                default:
                    return new List<PungentRichDocumentSemanticField>();
            }
        }

        private static void AddPlainAndTokenRuns(List<PungentRichDocumentTextRun> runs, string source, int start, int length)
        {
            if (length <= 0)
                return;

            string slice = source.Substring(start, length);
            List<PungentRichDocumentCanvasTokenRun> tokenRuns = PungentRichDocumentDocumentCanvasParser.ExtractTokenRuns(slice);
            int cursor = 0;
            foreach (PungentRichDocumentCanvasTokenRun token in tokenRuns)
            {
                if (token.startIndex > cursor)
                {
                    runs.Add(new PungentRichDocumentTextRun
                    {
                        kind = PungentRichDocumentTextRunKind.Text,
                        startIndex = start + cursor,
                        length = token.startIndex - cursor,
                        text = slice.Substring(cursor, token.startIndex - cursor)
                    });
                }

                token.startIndex += start;
                runs.Add(new PungentRichDocumentTextRun
                {
                    kind = PungentRichDocumentTextRunKind.Token,
                    startIndex = token.startIndex,
                    length = token.length,
                    text = token.rawText,
                    token = token
                });
                cursor = token.startIndex - start + token.length;
            }

            if (cursor < slice.Length)
            {
                runs.Add(new PungentRichDocumentTextRun
                {
                    kind = PungentRichDocumentTextRunKind.Text,
                    startIndex = start + cursor,
                    length = slice.Length - cursor,
                    text = slice.Substring(cursor)
                });
            }
        }

        private static List<PungentRichDocumentSemanticField> ParseFields(string attributeSource)
        {
            List<PungentRichDocumentSemanticField> fields = new List<PungentRichDocumentSemanticField>();
            foreach (Match match in AttributeRegex.Matches(attributeSource ?? string.Empty))
            {
                string key = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;
                fields.Add(PungentRichDocumentSemanticField.Create(key, value));
            }

            return fields;
        }

        private static string FieldValue(IEnumerable<PungentRichDocumentSemanticField> fields, string key)
        {
            if (fields == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            PungentRichDocumentSemanticField field = fields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return field == null ? string.Empty : field.value ?? string.Empty;
        }

        private static string NormalizeTagName(string tagName)
        {
            return string.IsNullOrWhiteSpace(tagName) ? string.Empty : tagName.Trim().ToLowerInvariant();
        }

        private static string EscapeAttribute(string value)
        {
            return (value ?? string.Empty).Replace("\"", "'");
        }
    }
#endif
}
