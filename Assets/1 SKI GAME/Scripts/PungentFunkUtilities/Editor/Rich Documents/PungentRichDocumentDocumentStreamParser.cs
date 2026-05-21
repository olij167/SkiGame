using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public enum PungentRichDocumentStreamNodeKind
    {
        Blank = 0,
        Text = 10,
        Heading = 20,
        Quote = 30,
        Code = 40,
        Divider = 50,
        DialogueLine = 100,
        DialogueChoice = 110,
        QuestObjective = 120,
        TutorialStep = 130,
        CommandPlaceholder = 140,
        GameCopy = 150
    }

    public sealed class PungentRichDocumentStreamTokenRun
    {
        public int startIndex;
        public int length;
        public string key = string.Empty;
    }

    public sealed class PungentRichDocumentStreamNode
    {
        public PungentRichDocumentStreamNodeKind kind = PungentRichDocumentStreamNodeKind.Text;
        public string rawText = string.Empty;
        public string text = string.Empty;
        public string label = string.Empty;
        public string speaker = string.Empty;
        public string commandKey = string.Empty;
        public string language = string.Empty;
        public int headingLevel = 2;
        public int sourceLine;

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
                    return (string.IsNullOrWhiteSpace(speaker) ? "Speaker" : speaker.Trim()) + ": " + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                    return "- [choice] " + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                    return "- [objective] " + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.TutorialStep:
                    return "1. [step] " + (text ?? string.Empty).Trim();
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    return "<command:" + (commandKey ?? text ?? string.Empty).Trim().Trim('<', '>') + ">";
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    return (string.IsNullOrWhiteSpace(label) ? "Copy" : label.Trim()) + ": " + (text ?? string.Empty).Trim();
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
    }

    public sealed class PungentRichDocumentStream
    {
        public string sourceBody = string.Empty;
        public List<PungentRichDocumentStreamNode> nodes = new List<PungentRichDocumentStreamNode>();

        public string Serialize()
        {
            return PungentRichDocumentDocumentStreamParser.Serialize(nodes);
        }
    }

    public static class PungentRichDocumentDocumentStreamParser
    {
        private static readonly Regex TokenRegex = new Regex("\\{([^{}]+)\\}", RegexOptions.Compiled);

        public static PungentRichDocumentStream Parse(string bodyText)
        {
            string normalized = (bodyText ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            PungentRichDocumentStream stream = new PungentRichDocumentStream
            {
                sourceBody = normalized
            };

            string[] lines = normalized.Length == 0 ? Array.Empty<string>() : normalized.Split('\n');
            bool inCode = false;
            string codeLanguage = string.Empty;
            int codeStartLine = 0;
            StringBuilder code = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                string trimmed = line.Trim();

                if (inCode)
                {
                    if (trimmed.StartsWith("```", StringComparison.Ordinal))
                    {
                        stream.nodes.Add(new PungentRichDocumentStreamNode
                        {
                            kind = PungentRichDocumentStreamNodeKind.Code,
                            language = codeLanguage,
                            text = code.ToString().TrimEnd(),
                            sourceLine = codeStartLine
                        });
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

                stream.nodes.Add(ParseLine(line, i + 1));
            }

            if (inCode)
            {
                stream.nodes.Add(new PungentRichDocumentStreamNode
                {
                    kind = PungentRichDocumentStreamNodeKind.Code,
                    language = codeLanguage,
                    text = code.ToString().TrimEnd(),
                    sourceLine = codeStartLine
                });
            }

            return stream;
        }

        public static string Serialize(IEnumerable<PungentRichDocumentStreamNode> nodes)
        {
            return string.Join(Environment.NewLine, (nodes ?? Enumerable.Empty<PungentRichDocumentStreamNode>())
                .Where(node => node != null)
                .Select(node => node.ToRawText())
                .ToArray());
        }

        public static List<PungentRichDocumentStreamTokenRun> ExtractTokenRuns(string text)
        {
            List<PungentRichDocumentStreamTokenRun> runs = new List<PungentRichDocumentStreamTokenRun>();
            foreach (Match match in TokenRegex.Matches(text ?? string.Empty))
            {
                string key = PungentRichDocumentParser.NormalizeTokenKey(match.Groups[1].Value);
                if (!PungentRichDocumentParser.IsValidTokenKey(key))
                    continue;

                runs.Add(new PungentRichDocumentStreamTokenRun
                {
                    startIndex = match.Index,
                    length = match.Length,
                    key = key
                });
            }

            return runs;
        }

        public static PungentRichDocumentStreamNode CreateNodeFromRaw(string rawText)
        {
            PungentRichDocumentStream parsed = Parse(rawText);
            return parsed.nodes.FirstOrDefault() ?? new PungentRichDocumentStreamNode
            {
                kind = PungentRichDocumentStreamNodeKind.Text,
                rawText = rawText ?? string.Empty,
                text = rawText ?? string.Empty
            };
        }

        public static PungentRichDocumentStreamNode CreateIntegration(PungentRichDocumentStreamNodeKind kind, string text = null)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                    return new PungentRichDocumentStreamNode { kind = kind, speaker = "NPC", text = text ?? "Dialogue line." };
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                    return new PungentRichDocumentStreamNode { kind = kind, text = text ?? "Choice text" };
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                    return new PungentRichDocumentStreamNode { kind = kind, text = text ?? "Objective text" };
                case PungentRichDocumentStreamNodeKind.TutorialStep:
                    return new PungentRichDocumentStreamNode { kind = kind, text = text ?? "Tutorial instruction." };
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    return new PungentRichDocumentStreamNode { kind = kind, commandKey = text ?? "command_key" };
                case PungentRichDocumentStreamNodeKind.Code:
                    return new PungentRichDocumentStreamNode { kind = kind, text = text ?? "code" };
                case PungentRichDocumentStreamNodeKind.Quote:
                    return new PungentRichDocumentStreamNode { kind = kind, text = text ?? "Quote" };
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    return new PungentRichDocumentStreamNode { kind = kind, label = "Hint", text = text ?? "Loading screen hint text." };
                default:
                    return new PungentRichDocumentStreamNode { kind = PungentRichDocumentStreamNodeKind.Text, rawText = text ?? string.Empty, text = text ?? string.Empty };
            }
        }

        public static PungentRichDocumentStreamNodeKind KindForBlock(PungentRichDocumentBlockType blockType)
        {
            switch (blockType)
            {
                case PungentRichDocumentBlockType.Heading: return PungentRichDocumentStreamNodeKind.Heading;
                case PungentRichDocumentBlockType.Quote: return PungentRichDocumentStreamNodeKind.Quote;
                case PungentRichDocumentBlockType.Code: return PungentRichDocumentStreamNodeKind.Code;
                case PungentRichDocumentBlockType.Divider: return PungentRichDocumentStreamNodeKind.Divider;
                case PungentRichDocumentBlockType.SpeakerLine: return PungentRichDocumentStreamNodeKind.DialogueLine;
                case PungentRichDocumentBlockType.DialogueChoice: return PungentRichDocumentStreamNodeKind.DialogueChoice;
                case PungentRichDocumentBlockType.QuestObjective: return PungentRichDocumentStreamNodeKind.QuestObjective;
                case PungentRichDocumentBlockType.TutorialStep: return PungentRichDocumentStreamNodeKind.TutorialStep;
                case PungentRichDocumentBlockType.CommandPlaceholder: return PungentRichDocumentStreamNodeKind.CommandPlaceholder;
                default: return PungentRichDocumentStreamNodeKind.Text;
            }
        }

        public static PungentRichDocumentBlockType BlockTypeForKind(PungentRichDocumentStreamNodeKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.Heading: return PungentRichDocumentBlockType.Heading;
                case PungentRichDocumentStreamNodeKind.Quote: return PungentRichDocumentBlockType.Quote;
                case PungentRichDocumentStreamNodeKind.Code: return PungentRichDocumentBlockType.Code;
                case PungentRichDocumentStreamNodeKind.Divider: return PungentRichDocumentBlockType.Divider;
                case PungentRichDocumentStreamNodeKind.DialogueLine: return PungentRichDocumentBlockType.SpeakerLine;
                case PungentRichDocumentStreamNodeKind.DialogueChoice: return PungentRichDocumentBlockType.DialogueChoice;
                case PungentRichDocumentStreamNodeKind.QuestObjective: return PungentRichDocumentBlockType.QuestObjective;
                case PungentRichDocumentStreamNodeKind.TutorialStep: return PungentRichDocumentBlockType.TutorialStep;
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder: return PungentRichDocumentBlockType.CommandPlaceholder;
                default: return PungentRichDocumentBlockType.Paragraph;
            }
        }

        private static PungentRichDocumentStreamNode ParseLine(string raw, int lineNumber)
        {
            string safeRaw = raw ?? string.Empty;
            if (string.IsNullOrWhiteSpace(safeRaw))
                return new PungentRichDocumentStreamNode { kind = PungentRichDocumentStreamNodeKind.Blank, rawText = string.Empty, sourceLine = lineNumber };

            PungentRichDocument temp = PungentRichDocument.Create("Line", "document-stream-parser");
            temp.bodyText = safeRaw;
            PungentRichDocumentParsedLine parsed = PungentRichDocumentParser.Parse(temp).lines.FirstOrDefault();
            if (parsed == null)
                return new PungentRichDocumentStreamNode { kind = PungentRichDocumentStreamNodeKind.Text, rawText = safeRaw, text = safeRaw, sourceLine = lineNumber };

            PungentRichDocumentStreamNode node = new PungentRichDocumentStreamNode
            {
                rawText = safeRaw,
                text = parsed.text,
                label = parsed.label,
                speaker = parsed.speaker,
                commandKey = parsed.commandKey,
                headingLevel = parsed.headingLevel <= 0 ? 2 : parsed.headingLevel,
                sourceLine = lineNumber
            };

            switch (parsed.kind)
            {
                case PungentRichDocumentParsedLineKind.Heading:
                    node.kind = PungentRichDocumentStreamNodeKind.Heading;
                    break;
                case PungentRichDocumentParsedLineKind.Quote:
                    node.kind = PungentRichDocumentStreamNodeKind.Quote;
                    break;
                case PungentRichDocumentParsedLineKind.Divider:
                    node.kind = PungentRichDocumentStreamNodeKind.Divider;
                    break;
                case PungentRichDocumentParsedLineKind.SpeakerLine:
                    node.kind = PungentRichDocumentStreamNodeKind.DialogueLine;
                    break;
                case PungentRichDocumentParsedLineKind.DialogueChoice:
                    node.kind = PungentRichDocumentStreamNodeKind.DialogueChoice;
                    break;
                case PungentRichDocumentParsedLineKind.QuestObjective:
                    node.kind = PungentRichDocumentStreamNodeKind.QuestObjective;
                    break;
                case PungentRichDocumentParsedLineKind.TutorialStep:
                    node.kind = PungentRichDocumentStreamNodeKind.TutorialStep;
                    break;
                case PungentRichDocumentParsedLineKind.CommandPlaceholder:
                    node.kind = PungentRichDocumentStreamNodeKind.CommandPlaceholder;
                    break;
                case PungentRichDocumentParsedLineKind.GameCopy:
                    node.kind = PungentRichDocumentStreamNodeKind.GameCopy;
                    break;
                default:
                    node.kind = PungentRichDocumentStreamNodeKind.Text;
                    node.text = safeRaw;
                    break;
            }

            return node;
        }
    }
#endif
}
