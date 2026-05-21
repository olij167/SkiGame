using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public enum PungentRichDocumentScriptLineKind
    {
        None = 0,
        YarnOption = 10,
        YarnCommand = 20,
        InkChoice = 30,
        InkTag = 40,
        InkKnot = 50,
        InkDivert = 60,
        AuthorComment = 70,
        Todo = 80
    }

    public sealed class PungentRichDocumentCanvasTokenRun
    {
        public int startIndex;
        public int length;
        public string rawText = string.Empty;
        public string key = string.Empty;
        public string chipLabel = string.Empty;
        public bool isCustomChip;
        public bool validSyntax;
        public PungentRichDocumentBindingExpression bindingExpression;
        public PungentRichDocumentBindingExpressionKind expressionKind = PungentRichDocumentBindingExpressionKind.Unknown;
        public bool isReferenceExpression;
        public bool isCustomDefinitionExpression;
        public bool isCustomReferenceExpression;
    }

    public sealed class PungentRichDocumentCanvasSegment
    {
        public PungentRichDocumentStreamNodeKind kind = PungentRichDocumentStreamNodeKind.Text;
        public string rawText = string.Empty;
        public string text = string.Empty;
        public string label = string.Empty;
        public string speaker = string.Empty;
        public string commandKey = string.Empty;
        public string language = string.Empty;
        public string semanticId = string.Empty;
        public PungentRichDocumentSemanticKind semanticKind = PungentRichDocumentSemanticKind.None;
        public List<PungentRichDocumentSemanticField> semanticFields = new List<PungentRichDocumentSemanticField>();
        public int headingLevel = 2;
        public int sourceLine;
        public bool isSemanticSpan;
        public PungentRichDocumentScriptLineKind scriptLineKind = PungentRichDocumentScriptLineKind.None;

        public bool IsPlainTextEditable =>
            kind == PungentRichDocumentStreamNodeKind.Blank ||
            kind == PungentRichDocumentStreamNodeKind.Text ||
            kind == PungentRichDocumentStreamNodeKind.Heading ||
            kind == PungentRichDocumentStreamNodeKind.Quote;

        public bool IsEmbeddedIntegration =>
            kind == PungentRichDocumentStreamNodeKind.Code ||
            kind == PungentRichDocumentStreamNodeKind.DialogueLine ||
            kind == PungentRichDocumentStreamNodeKind.DialogueChoice ||
            kind == PungentRichDocumentStreamNodeKind.QuestObjective ||
            kind == PungentRichDocumentStreamNodeKind.TutorialStep ||
            kind == PungentRichDocumentStreamNodeKind.CommandPlaceholder ||
            kind == PungentRichDocumentStreamNodeKind.GameCopy;

        public string ToRawText()
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.Blank:
                    return string.Empty;
                case PungentRichDocumentStreamNodeKind.Heading:
                    return new string('#', Math.Max(1, Math.Min(6, headingLevel))) + " " + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.Quote:
                    return "> " + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.Code:
                    return "```" + (language ?? string.Empty).Trim() + Environment.NewLine + (text ?? string.Empty).TrimEnd() + Environment.NewLine + "```";
                case PungentRichDocumentStreamNodeKind.Divider:
                    return "---";
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                    if (isSemanticSpan)
                        return ToSemanticRawText();
                    return WithContinuation((string.IsNullOrWhiteSpace(speaker) ? "Speaker" : speaker.Trim()) + ": ", text);
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                    if (isSemanticSpan)
                        return ToSemanticRawText();
                    return WithContinuation("- [choice] ", text);
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                    if (isSemanticSpan)
                        return ToSemanticRawText();
                    return WithContinuation("- [objective] ", text);
                case PungentRichDocumentStreamNodeKind.TutorialStep:
                    if (isSemanticSpan)
                        return ToSemanticRawText();
                    return WithContinuation("1. [step] ", text);
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    if (isSemanticSpan)
                        return ToSemanticRawText();
                    return "<command:" + (commandKey ?? text ?? string.Empty).Trim().Trim('<', '>') + ">";
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    if (isSemanticSpan)
                        return ToSemanticRawText();
                    return WithContinuation((string.IsNullOrWhiteSpace(label) ? "Copy" : label.Trim()) + ": ", text);
                default:
                    return rawText ?? text ?? string.Empty;
            }
        }

        public string ReadableText()
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                    return (string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.Trim() + ": ") + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    return commandKey ?? text ?? string.Empty;
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    return (string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim() + ": ") + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.Code:
                case PungentRichDocumentStreamNodeKind.Quote:
                case PungentRichDocumentStreamNodeKind.Heading:
                    return text ?? string.Empty;
                default:
                    return rawText ?? text ?? string.Empty;
            }
        }

        private static string WithContinuation(string prefix, string value)
        {
            string[] lines = (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            if (lines.Length == 0)
                return prefix.TrimEnd();

            StringBuilder builder = new StringBuilder(prefix + (lines[0] ?? string.Empty).Trim());
            for (int i = 1; i < lines.Length; i++)
                builder.AppendLine().Append("  ").Append((lines[i] ?? string.Empty).TrimEnd());
            return builder.ToString().TrimEnd();
        }

        private string ToSemanticRawText()
        {
            PungentRichDocumentSemanticKind kind = semanticKind == PungentRichDocumentSemanticKind.None
                ? SemanticKindForStreamKind(this.kind, label)
                : semanticKind;
            List<PungentRichDocumentSemanticField> fields = semanticFields ?? new List<PungentRichDocumentSemanticField>();
            if (kind == PungentRichDocumentSemanticKind.DialogueLine && !string.IsNullOrWhiteSpace(speaker))
                fields = fields.Concat(new[] { PungentRichDocumentSemanticField.Create("speaker", speaker) }).ToList();
            if (kind == PungentRichDocumentSemanticKind.Command && !string.IsNullOrWhiteSpace(commandKey))
                fields = fields.Concat(new[] { PungentRichDocumentSemanticField.Create("key", commandKey) }).ToList();
            if ((kind == PungentRichDocumentSemanticKind.GameCopy ||
                 kind == PungentRichDocumentSemanticKind.HintCopy ||
                 kind == PungentRichDocumentSemanticKind.ItemCopy ||
                 kind == PungentRichDocumentSemanticKind.CharacterCopy) &&
                !string.IsNullOrWhiteSpace(label))
                fields = fields.Concat(new[] { PungentRichDocumentSemanticField.Create("category", label) }).ToList();

            return PungentRichDocumentSemanticParser.CreateSemanticTag(kind, text, fields, semanticId);
        }

        private static PungentRichDocumentSemanticKind SemanticKindForStreamKind(PungentRichDocumentStreamNodeKind kind, string label)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine: return PungentRichDocumentSemanticKind.DialogueLine;
                case PungentRichDocumentStreamNodeKind.DialogueChoice: return PungentRichDocumentSemanticKind.DialogueChoice;
                case PungentRichDocumentStreamNodeKind.QuestObjective: return PungentRichDocumentSemanticKind.QuestObjective;
                case PungentRichDocumentStreamNodeKind.TutorialStep: return PungentRichDocumentSemanticKind.TutorialStep;
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder: return PungentRichDocumentSemanticKind.Command;
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    if (string.Equals(label, "Hint", StringComparison.OrdinalIgnoreCase))
                        return PungentRichDocumentSemanticKind.HintCopy;
                    if (string.Equals(label, "Item", StringComparison.OrdinalIgnoreCase))
                        return PungentRichDocumentSemanticKind.ItemCopy;
                    if (string.Equals(label, "Character", StringComparison.OrdinalIgnoreCase))
                        return PungentRichDocumentSemanticKind.CharacterCopy;
                    return PungentRichDocumentSemanticKind.GameCopy;
                default:
                    return PungentRichDocumentSemanticKind.None;
            }
        }
    }

    public sealed class PungentRichDocumentCanvasDocument
    {
        public string sourceBody = string.Empty;
        public List<PungentRichDocumentCanvasSegment> segments = new List<PungentRichDocumentCanvasSegment>();

        public string Serialize()
        {
            return PungentRichDocumentDocumentCanvasParser.Serialize(segments);
        }
    }

    public static class PungentRichDocumentDocumentCanvasParser
    {
        private static readonly Regex TokenRegex = new Regex("\\{([^{}]*)\\}", RegexOptions.Compiled);
        private static readonly Regex InkKnotRegex = new Regex("^={2,}\\s*[A-Za-z_][A-Za-z0-9_.-]*\\s*=*$", RegexOptions.Compiled);
        private static readonly Regex InkDivertRegex = new Regex("^->\\s*[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.Compiled);
        private static readonly Regex InkTagRegex = new Regex("(^|\\s)#[A-Za-z][A-Za-z0-9_.-]*", RegexOptions.Compiled);
        private static readonly Regex YarnCommandRegex = new Regex("^<<.+>>$", RegexOptions.Compiled);

        public static PungentRichDocumentCanvasDocument Parse(string bodyText)
        {
            string normalized = (bodyText ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            PungentRichDocumentCanvasDocument canvas = new PungentRichDocumentCanvasDocument
            {
                sourceBody = normalized
            };

            string[] lines = normalized.Length == 0 ? Array.Empty<string>() : normalized.Split('\n');
            bool inCode = false;
            string codeLanguage = string.Empty;
            int codeStartLine = 0;
            StringBuilder code = new StringBuilder();
            PungentRichDocumentCanvasSegment previous = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                string trimmed = line.Trim();

                if (inCode)
                {
                    if (trimmed.StartsWith("```", StringComparison.Ordinal))
                    {
                        previous = new PungentRichDocumentCanvasSegment
                        {
                            kind = PungentRichDocumentStreamNodeKind.Code,
                            language = codeLanguage,
                            text = code.ToString().TrimEnd(),
                            sourceLine = codeStartLine
                        };
                        canvas.segments.Add(previous);
                        code.Length = 0;
                        codeLanguage = string.Empty;
                        inCode = false;
                    }
                    else
                    {
                        code.AppendLine(line);
                    }

                    continue;
                }

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    inCode = true;
                    codeStartLine = i + 1;
                    codeLanguage = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                    code.Length = 0;
                    continue;
                }

                if (IsContinuation(line) && previous != null && previous.IsEmbeddedIntegration && previous.kind != PungentRichDocumentStreamNodeKind.CommandPlaceholder)
                {
                    previous.text = AppendContinuation(previous.text, line);
                    previous.rawText = AppendRawContinuation(previous.rawText, line);
                    continue;
                }

                PungentRichDocumentCanvasSegment segment = CreateSegmentFromRaw(line, i + 1);
                if (ShouldMergeParagraph(previous, segment))
                {
                    previous.rawText = string.IsNullOrEmpty(previous.rawText) ? segment.rawText : previous.rawText + Environment.NewLine + segment.rawText;
                    previous.text = previous.rawText;
                    continue;
                }

                canvas.segments.Add(segment);
                previous = segment.kind == PungentRichDocumentStreamNodeKind.Blank ? null : segment;
            }

            if (inCode)
            {
                canvas.segments.Add(new PungentRichDocumentCanvasSegment
                {
                    kind = PungentRichDocumentStreamNodeKind.Code,
                    language = codeLanguage,
                    text = code.ToString().TrimEnd(),
                    sourceLine = codeStartLine
                });
            }

            return canvas;
        }

        public static string Serialize(IEnumerable<PungentRichDocumentCanvasSegment> segments)
        {
            return string.Join(Environment.NewLine, (segments ?? Enumerable.Empty<PungentRichDocumentCanvasSegment>())
                .Where(segment => segment != null)
                .Select(segment => segment.ToRawText())
                .ToArray()).TrimEnd();
        }

        public static List<PungentRichDocumentCanvasTokenRun> ExtractTokenRuns(string text)
        {
            List<PungentRichDocumentCanvasTokenRun> runs = new List<PungentRichDocumentCanvasTokenRun>();
            foreach (Match match in TokenRegex.Matches(text ?? string.Empty))
            {
                string raw = match.Value;
                string inner = (match.Groups[1].Value ?? string.Empty).Trim();
                PungentRichDocumentBindingExpression expression;
                if (PungentRichDocumentBindingExpressionParser.TryParseToken(raw, out expression) &&
                    ShouldUseBindingExpression(expression))
                {
                    runs.Add(new PungentRichDocumentCanvasTokenRun
                    {
                        startIndex = match.Index,
                        length = match.Length,
                        rawText = raw,
                        key = expression.IsReference ? expression.targetName : expression.customId,
                        chipLabel = expression.displayText,
                        validSyntax = true,
                        bindingExpression = expression,
                        expressionKind = expression.kind,
                        isReferenceExpression = expression.kind == PungentRichDocumentBindingExpressionKind.Reference,
                        isCustomDefinitionExpression = expression.kind == PungentRichDocumentBindingExpressionKind.CustomDefinition,
                        isCustomReferenceExpression = expression.kind == PungentRichDocumentBindingExpressionKind.CustomReference
                    });
                    continue;
                }

                bool customChip = inner.StartsWith("chip:", StringComparison.OrdinalIgnoreCase);
                string chipLabel = customChip ? inner.Substring(5).Trim() : string.Empty;
                string key = customChip ? chipLabel : PungentRichDocumentParser.NormalizeTokenKey(inner);
                runs.Add(new PungentRichDocumentCanvasTokenRun
                {
                    startIndex = match.Index,
                    length = match.Length,
                    rawText = raw,
                    key = key,
                    chipLabel = chipLabel,
                    isCustomChip = customChip,
                    validSyntax = customChip ? !string.IsNullOrWhiteSpace(chipLabel) : PungentRichDocumentParser.IsValidTokenKey(key)
                });
            }

            return runs;
        }

        private static bool ShouldUseBindingExpression(PungentRichDocumentBindingExpression expression)
        {
            if (expression == null)
                return false;
            if (expression.kind == PungentRichDocumentBindingExpressionKind.Reference ||
                expression.kind == PungentRichDocumentBindingExpressionKind.CustomDefinition)
                return true;
            if (expression.kind == PungentRichDocumentBindingExpressionKind.CustomReference)
                return PungentRichDocumentInsertionDefinitionRegistry.Find(expression.customId) != null;
            return false;
        }

        public static PungentRichDocumentCanvasSegment CreateSegmentFromRaw(string rawText, int sourceLine = 0)
        {
            if (PungentRichDocumentSemanticParser.TryParseWholeSemanticTag(rawText, out PungentRichDocumentSemanticSpan semantic))
                return CreateSegmentFromSemanticSpan(rawText, semantic, sourceLine);

            PungentRichDocumentScriptLineKind scriptKind = DetectScriptLineKind(rawText);
            if (scriptKind != PungentRichDocumentScriptLineKind.None)
            {
                return new PungentRichDocumentCanvasSegment
                {
                    kind = PungentRichDocumentStreamNodeKind.Text,
                    rawText = rawText ?? string.Empty,
                    text = rawText ?? string.Empty,
                    sourceLine = sourceLine,
                    scriptLineKind = scriptKind
                };
            }

            PungentRichDocument temp = PungentRichDocument.Create("Line", "document-canvas-parser");
            temp.bodyText = rawText ?? string.Empty;
            PungentRichDocumentParsedLine parsed = PungentRichDocumentParser.Parse(temp).lines.FirstOrDefault();
            if (parsed == null)
            {
                return new PungentRichDocumentCanvasSegment
                {
                    kind = PungentRichDocumentStreamNodeKind.Text,
                    rawText = rawText ?? string.Empty,
                    text = rawText ?? string.Empty,
                    sourceLine = sourceLine,
                    scriptLineKind = DetectScriptLineKind(rawText)
                };
            }

            PungentRichDocumentCanvasSegment segment = new PungentRichDocumentCanvasSegment
            {
                rawText = rawText ?? string.Empty,
                text = parsed.text,
                label = parsed.label,
                speaker = parsed.speaker,
                commandKey = parsed.commandKey,
                semanticId = parsed.semanticId,
                semanticKind = parsed.semanticKind,
                semanticFields = parsed.semanticFields,
                isSemanticSpan = parsed.isSemanticSpan,
                headingLevel = parsed.headingLevel <= 0 ? 2 : parsed.headingLevel,
                sourceLine = sourceLine,
                scriptLineKind = scriptKind
            };

            switch (parsed.kind)
            {
                case PungentRichDocumentParsedLineKind.Blank:
                    segment.kind = PungentRichDocumentStreamNodeKind.Blank;
                    segment.text = string.Empty;
                    segment.rawText = string.Empty;
                    break;
                case PungentRichDocumentParsedLineKind.Heading:
                    segment.kind = PungentRichDocumentStreamNodeKind.Heading;
                    break;
                case PungentRichDocumentParsedLineKind.Quote:
                    segment.kind = PungentRichDocumentStreamNodeKind.Quote;
                    break;
                case PungentRichDocumentParsedLineKind.Divider:
                    segment.kind = PungentRichDocumentStreamNodeKind.Divider;
                    break;
                case PungentRichDocumentParsedLineKind.SpeakerLine:
                    segment.kind = PungentRichDocumentStreamNodeKind.DialogueLine;
                    break;
                case PungentRichDocumentParsedLineKind.DialogueChoice:
                    segment.kind = PungentRichDocumentStreamNodeKind.DialogueChoice;
                    break;
                case PungentRichDocumentParsedLineKind.QuestObjective:
                    segment.kind = PungentRichDocumentStreamNodeKind.QuestObjective;
                    break;
                case PungentRichDocumentParsedLineKind.TutorialStep:
                    segment.kind = PungentRichDocumentStreamNodeKind.TutorialStep;
                    break;
                case PungentRichDocumentParsedLineKind.CommandPlaceholder:
                    segment.kind = PungentRichDocumentStreamNodeKind.CommandPlaceholder;
                    break;
                case PungentRichDocumentParsedLineKind.GameCopy:
                    segment.kind = PungentRichDocumentStreamNodeKind.GameCopy;
                    break;
                default:
                    segment.kind = PungentRichDocumentStreamNodeKind.Text;
                    segment.text = rawText ?? string.Empty;
                    break;
            }

            return segment;
        }

        public static PungentRichDocumentCanvasSegment CreateIntegration(PungentRichDocumentStreamNodeKind kind, string text = null)
        {
            PungentRichDocumentSemanticKind semanticKind = SemanticKindForKind(kind);
            if (semanticKind != PungentRichDocumentSemanticKind.None && kind != PungentRichDocumentStreamNodeKind.Code)
                return CreateSemanticIntegration(kind, semanticKind, text);

            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                    return new PungentRichDocumentCanvasSegment { kind = kind, speaker = "NPC", text = text ?? "Dialogue line." };
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                    return new PungentRichDocumentCanvasSegment { kind = kind, text = text ?? "Choice text" };
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                    return new PungentRichDocumentCanvasSegment { kind = kind, text = text ?? "Objective text" };
                case PungentRichDocumentStreamNodeKind.TutorialStep:
                    return new PungentRichDocumentCanvasSegment { kind = kind, text = text ?? "Tutorial instruction." };
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    return new PungentRichDocumentCanvasSegment { kind = kind, commandKey = text ?? "command_key" };
                case PungentRichDocumentStreamNodeKind.Code:
                    return new PungentRichDocumentCanvasSegment { kind = kind, text = text ?? "code" };
                case PungentRichDocumentStreamNodeKind.Quote:
                    return new PungentRichDocumentCanvasSegment { kind = kind, text = text ?? "Quote" };
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    return new PungentRichDocumentCanvasSegment { kind = kind, label = "Hint", text = text ?? "Loading screen hint text." };
                default:
                    return new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Text, rawText = text ?? string.Empty, text = text ?? string.Empty };
            }
        }

        private static PungentRichDocumentCanvasSegment CreateSegmentFromSemanticSpan(string rawText, PungentRichDocumentSemanticSpan semantic, int sourceLine)
        {
            PungentRichDocumentStreamNodeKind kind = PungentRichDocumentSemanticParser.StreamKindForSemanticKind(semantic.kind);
            PungentRichDocumentCanvasSegment segment = new PungentRichDocumentCanvasSegment
            {
                kind = kind,
                rawText = rawText ?? string.Empty,
                text = semantic.innerText ?? string.Empty,
                semanticId = semantic.id,
                semanticKind = semantic.kind,
                semanticFields = semantic.fields ?? new List<PungentRichDocumentSemanticField>(),
                isSemanticSpan = true,
                sourceLine = sourceLine
            };
            segment.speaker = semantic.GetField("speaker");
            segment.commandKey = semantic.GetField("key");
            segment.label = semantic.GetField("category");
            if (semantic.kind == PungentRichDocumentSemanticKind.CustomInsertion)
            {
                string definitionId = PungentRichDocumentInsertionDefinitionRegistry.GetDefinitionId(semantic.fields);
                PungentRichDocumentInsertionDefinition definition = PungentRichDocumentInsertionDefinitionRegistry.Find(definitionId);
                segment.label = definition == null
                    ? semantic.GetField(PungentRichDocumentInsertionDefinitionRegistry.DefinitionLabelFieldKey)
                    : definition.displayName;
            }
            return segment;
        }

        private static PungentRichDocumentCanvasSegment CreateSemanticIntegration(PungentRichDocumentStreamNodeKind kind, PungentRichDocumentSemanticKind semanticKind, string text)
        {
            List<PungentRichDocumentSemanticField> fields = PungentRichDocumentSemanticParser.DefaultFieldsForKind(semanticKind);
            PungentRichDocumentCanvasSegment segment = new PungentRichDocumentCanvasSegment
            {
                kind = kind,
                semanticKind = semanticKind,
                semanticId = Guid.NewGuid().ToString("N"),
                semanticFields = fields,
                isSemanticSpan = true,
                text = DefaultTextForSemanticKind(semanticKind, text)
            };
            segment.speaker = FieldValue(fields, "speaker");
            segment.commandKey = FieldValue(fields, "key");
            segment.label = FieldValue(fields, "category");
            return segment;
        }

        private static PungentRichDocumentSemanticKind SemanticKindForKind(PungentRichDocumentStreamNodeKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine: return PungentRichDocumentSemanticKind.DialogueLine;
                case PungentRichDocumentStreamNodeKind.DialogueChoice: return PungentRichDocumentSemanticKind.DialogueChoice;
                case PungentRichDocumentStreamNodeKind.QuestObjective: return PungentRichDocumentSemanticKind.QuestObjective;
                case PungentRichDocumentStreamNodeKind.TutorialStep: return PungentRichDocumentSemanticKind.TutorialStep;
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder: return PungentRichDocumentSemanticKind.Command;
                case PungentRichDocumentStreamNodeKind.GameCopy: return PungentRichDocumentSemanticKind.GameCopy;
                default: return PungentRichDocumentSemanticKind.None;
            }
        }

        public static PungentRichDocumentCanvasSegment CreateCustomInsertion(PungentRichDocumentInsertionDefinition definition)
        {
            if (definition == null)
                return new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Text, rawText = string.Empty, text = string.Empty };

            List<PungentRichDocumentSemanticField> fields = PungentRichDocumentInsertionDefinitionRegistry.CreateDefaultFields(definition);
            return new PungentRichDocumentCanvasSegment
            {
                kind = PungentRichDocumentStreamNodeKind.GameCopy,
                semanticKind = PungentRichDocumentSemanticKind.CustomInsertion,
                semanticId = Guid.NewGuid().ToString("N"),
                semanticFields = fields,
                isSemanticSpan = true,
                label = definition.displayName,
                text = string.IsNullOrWhiteSpace(definition.defaultText) ? definition.displayName + " text." : definition.defaultText
            };
        }

        private static string DefaultTextForSemanticKind(PungentRichDocumentSemanticKind kind, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            switch (kind)
            {
                case PungentRichDocumentSemanticKind.DialogueLine: return "Dialogue line.";
                case PungentRichDocumentSemanticKind.DialogueChoice: return "Choice text";
                case PungentRichDocumentSemanticKind.QuestObjective: return "Objective text";
                case PungentRichDocumentSemanticKind.TutorialStep: return "Tutorial instruction.";
                case PungentRichDocumentSemanticKind.Command: return "command_key";
                default: return "Game copy text.";
            }
        }

        private static string FieldValue(IEnumerable<PungentRichDocumentSemanticField> fields, string key)
        {
            if (fields == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            PungentRichDocumentSemanticField field = fields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return field == null ? string.Empty : field.value ?? string.Empty;
        }

        private static bool ShouldMergeParagraph(PungentRichDocumentCanvasSegment previous, PungentRichDocumentCanvasSegment segment)
        {
            return previous != null &&
                   segment != null &&
                   previous.kind == PungentRichDocumentStreamNodeKind.Text &&
                   segment.kind == PungentRichDocumentStreamNodeKind.Text &&
                   previous.scriptLineKind == PungentRichDocumentScriptLineKind.None &&
                   segment.scriptLineKind == PungentRichDocumentScriptLineKind.None &&
                   !string.IsNullOrWhiteSpace(previous.rawText) &&
                   !string.IsNullOrWhiteSpace(segment.rawText);
        }

        private static PungentRichDocumentScriptLineKind DetectScriptLineKind(string rawText)
        {
            string trimmed = (rawText ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return PungentRichDocumentScriptLineKind.None;

            if (trimmed.StartsWith("TODO:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("TODO ", StringComparison.OrdinalIgnoreCase))
                return PungentRichDocumentScriptLineKind.Todo;

            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                trimmed.StartsWith("*/", StringComparison.Ordinal))
                return PungentRichDocumentScriptLineKind.AuthorComment;

            if (YarnCommandRegex.IsMatch(trimmed))
                return PungentRichDocumentScriptLineKind.YarnCommand;

            if (InkKnotRegex.IsMatch(trimmed))
                return PungentRichDocumentScriptLineKind.InkKnot;

            if (InkDivertRegex.IsMatch(trimmed))
                return PungentRichDocumentScriptLineKind.InkDivert;

            if (trimmed.StartsWith("->", StringComparison.Ordinal))
                return PungentRichDocumentScriptLineKind.YarnOption;

            if (trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed.StartsWith("*\t", StringComparison.Ordinal))
                return PungentRichDocumentScriptLineKind.InkChoice;

            if (!trimmed.StartsWith("#", StringComparison.Ordinal) && InkTagRegex.IsMatch(trimmed))
                return PungentRichDocumentScriptLineKind.InkTag;

            return PungentRichDocumentScriptLineKind.None;
        }

        private static bool IsContinuation(string line)
        {
            return !string.IsNullOrEmpty(line) &&
                   (line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal));
        }

        private static string AppendContinuation(string text, string line)
        {
            string clean = (line ?? string.Empty).Trim();
            return string.IsNullOrEmpty(text) ? clean : text.TrimEnd() + Environment.NewLine + clean;
        }

        private static string AppendRawContinuation(string rawText, string line)
        {
            return string.IsNullOrEmpty(rawText) ? line ?? string.Empty : rawText + Environment.NewLine + (line ?? string.Empty);
        }
    }
#endif
}
