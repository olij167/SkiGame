using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentBlockSync
    {
        public static bool EnsureBlocksFromBody(PungentRichDocument document)
        {
            if (document == null)
                return false;

            if (document.blocks != null && document.blocks.Any(block => block != null && block.HasReadableContent))
                return false;

            if (string.IsNullOrWhiteSpace(document.bodyText))
                return false;

            SyncRawToBlocks(document);
            return true;
        }

        public static void SyncRawToBlocks(PungentRichDocument document)
        {
            if (document == null)
                return;

            document.blocks = ParseBodyToBlocks(document.bodyText);
            document.NormalizeInPlace();
        }

        public static void RegenerateBodyFromBlocks(PungentRichDocument document)
        {
            if (document == null)
                return;

            document.bodyText = GenerateBodyText(document.blocks);
        }

        public static string GenerateBodyText(IEnumerable<PungentRichDocumentBlock> blocks)
        {
            StringBuilder builder = new StringBuilder();
            foreach (PungentRichDocumentBlock block in blocks ?? Enumerable.Empty<PungentRichDocumentBlock>())
            {
                if (block == null)
                    continue;

                string text = GenerateBlockText(block);
                if (string.IsNullOrWhiteSpace(text) && block.type != PungentRichDocumentBlockType.Divider)
                    continue;

                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();
                builder.Append(text);
            }

            return builder.ToString().TrimEnd();
        }

        public static string GenerateBlockText(PungentRichDocumentBlock block)
        {
            if (block == null)
                return string.Empty;

            switch (block.type)
            {
                case PungentRichDocumentBlockType.Heading:
                    return new string('#', Math.Max(1, Math.Min(6, block.headingLevel))) + " " + (block.text ?? string.Empty).Trim();
                case PungentRichDocumentBlockType.BulletList:
                    return GenerateListText(block, false);
                case PungentRichDocumentBlockType.Checklist:
                    return GenerateListText(block, true);
                case PungentRichDocumentBlockType.Quote:
                    return "> " + (block.text ?? string.Empty).Trim();
                case PungentRichDocumentBlockType.Code:
                    return "```" + (block.language ?? string.Empty).Trim() + Environment.NewLine + (block.text ?? string.Empty).TrimEnd() + Environment.NewLine + "```";
                case PungentRichDocumentBlockType.Divider:
                    return "---";
                case PungentRichDocumentBlockType.TokenReference:
                case PungentRichDocumentBlockType.VariablePlaceholder:
                    return "{" + NormalizeTokenKey(string.IsNullOrWhiteSpace(block.tokenKey) ? block.text : block.tokenKey) + "}";
                case PungentRichDocumentBlockType.SpeakerLine:
                    return (string.IsNullOrWhiteSpace(block.speaker) ? "Speaker" : block.speaker.Trim()) + ": " + (block.text ?? string.Empty).Trim();
                case PungentRichDocumentBlockType.DialogueChoice:
                    return "- [choice] " + (block.text ?? string.Empty).Trim();
                case PungentRichDocumentBlockType.QuestObjective:
                    return "- [objective] " + (block.text ?? string.Empty).Trim();
                case PungentRichDocumentBlockType.TutorialStep:
                    return "1. [step] " + (block.text ?? string.Empty).Trim();
                case PungentRichDocumentBlockType.CommandPlaceholder:
                    return "<command:" + (block.text ?? block.tokenKey ?? string.Empty).Trim().Trim('<', '>') + ">";
                case PungentRichDocumentBlockType.LinkReference:
                    return (block.text ?? string.Empty).Trim();
                default:
                    return (block.text ?? string.Empty).TrimEnd();
            }
        }

        public static List<PungentRichDocumentBlock> ParseBodyToBlocks(string bodyText)
        {
            List<PungentRichDocumentBlock> blocks = new List<PungentRichDocumentBlock>();
            string normalized = (bodyText ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Length == 0 ? Array.Empty<string>() : normalized.Split('\n');
            bool inCode = false;
            string codeLanguage = string.Empty;
            StringBuilder code = new StringBuilder();

            foreach (string rawLine in lines)
            {
                string line = rawLine ?? string.Empty;
                string trimmed = line.Trim();

                if (inCode)
                {
                    if (trimmed.StartsWith("```", StringComparison.Ordinal))
                    {
                        blocks.Add(PungentRichDocumentBlock.Code(code.ToString().TrimEnd(), codeLanguage));
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
                    codeLanguage = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                    code.Length = 0;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                PungentRichDocumentBlock block = CreateBlockFromLine(line);
                if (block == null)
                    continue;

                AddOrMergeBlock(blocks, block);
            }

            if (inCode)
                blocks.Add(PungentRichDocumentBlock.Code(code.ToString().TrimEnd(), codeLanguage));

            return blocks;
        }

        public static bool TryCreateBlockFromConvention(string text, out PungentRichDocumentBlock block)
        {
            block = null;
            List<PungentRichDocumentBlock> blocks = ParseBodyToBlocks(text);
            if (blocks.Count == 0)
                return false;

            block = blocks[0];
            return block != null;
        }

        public static void ConvertBlock(PungentRichDocumentBlock block, PungentRichDocumentBlockType nextType)
        {
            if (block == null)
                return;

            string readable = ReadableText(block);
            block.type = nextType;
            block.convention = PungentRichDocumentBlock.ConventionForType(nextType);
            block.headingLevel = nextType == PungentRichDocumentBlockType.Heading ? Math.Max(1, block.headingLevel) : block.headingLevel;
            block.speaker = string.Empty;
            block.tokenKey = string.Empty;
            block.language = string.Empty;
            block.items = new List<PungentRichDocumentBlockItem>();

            switch (nextType)
            {
                case PungentRichDocumentBlockType.Heading:
                    block.text = StripLeadingConvention(readable);
                    block.headingLevel = block.headingLevel <= 0 ? 2 : block.headingLevel;
                    break;
                case PungentRichDocumentBlockType.BulletList:
                    block.text = string.Empty;
                    AddTextLinesAsItems(block, readable, false);
                    break;
                case PungentRichDocumentBlockType.Checklist:
                    block.text = string.Empty;
                    AddTextLinesAsItems(block, readable, true);
                    break;
                case PungentRichDocumentBlockType.Quote:
                case PungentRichDocumentBlockType.Code:
                case PungentRichDocumentBlockType.DialogueChoice:
                case PungentRichDocumentBlockType.QuestObjective:
                case PungentRichDocumentBlockType.TutorialStep:
                case PungentRichDocumentBlockType.LinkReference:
                case PungentRichDocumentBlockType.Paragraph:
                    block.text = StripLeadingConvention(readable);
                    break;
                case PungentRichDocumentBlockType.SpeakerLine:
                    SplitSpeakerText(readable, out block.speaker, out block.text);
                    break;
                case PungentRichDocumentBlockType.CommandPlaceholder:
                    block.text = ExtractCommandKey(readable);
                    break;
                case PungentRichDocumentBlockType.TokenReference:
                case PungentRichDocumentBlockType.VariablePlaceholder:
                    block.tokenKey = FirstTokenKey(readable);
                    block.text = string.IsNullOrWhiteSpace(block.tokenKey) ? string.Empty : "{" + block.tokenKey + "}";
                    break;
                case PungentRichDocumentBlockType.Divider:
                    block.text = string.Empty;
                    break;
            }

            block.NormalizeInPlace();
        }

        public static void RemoveFormatter(PungentRichDocumentBlock block)
        {
            ConvertBlock(block, PungentRichDocumentBlockType.Paragraph);
        }

        public static string ReadableText(PungentRichDocumentBlock block)
        {
            if (block == null)
                return string.Empty;

            switch (block.type)
            {
                case PungentRichDocumentBlockType.BulletList:
                case PungentRichDocumentBlockType.Checklist:
                    return string.Join(Environment.NewLine, (block.items ?? new List<PungentRichDocumentBlockItem>())
                        .Where(item => item != null)
                        .Select(item => item.text ?? string.Empty));
                case PungentRichDocumentBlockType.SpeakerLine:
                    return (string.IsNullOrWhiteSpace(block.speaker) ? string.Empty : block.speaker.Trim() + ": ") + (block.text ?? string.Empty).Trim();
                case PungentRichDocumentBlockType.TokenReference:
                case PungentRichDocumentBlockType.VariablePlaceholder:
                    return "{" + NormalizeTokenKey(string.IsNullOrWhiteSpace(block.tokenKey) ? block.text : block.tokenKey) + "}";
                case PungentRichDocumentBlockType.CommandPlaceholder:
                    return ExtractCommandKey(block.text ?? block.tokenKey);
                default:
                    return block.text ?? string.Empty;
            }
        }

        public static void CopyBlockData(PungentRichDocumentBlock source, PungentRichDocumentBlock destination)
        {
            if (source == null || destination == null)
                return;

            destination.type = source.type;
            destination.convention = source.convention;
            destination.text = source.text ?? string.Empty;
            destination.speaker = source.speaker ?? string.Empty;
            destination.headingLevel = source.headingLevel;
            destination.tokenKey = source.tokenKey ?? string.Empty;
            destination.language = source.language ?? string.Empty;
            destination.collapsed = source.collapsed;
            destination.reference = source.reference;
            destination.target = source.target;
            destination.items = (source.items ?? new List<PungentRichDocumentBlockItem>())
                .Where(item => item != null)
                .Select(item => new PungentRichDocumentBlockItem
                {
                    text = item.text ?? string.Empty,
                    isChecked = item.isChecked,
                    metadata = item.metadata ?? string.Empty
                })
                .ToList();
        }

        private static PungentRichDocumentBlock CreateBlockFromLine(string line)
        {
            PungentRichDocument temp = PungentRichDocument.Create("Line", "inline-parser");
            temp.bodyText = line ?? string.Empty;
            PungentRichDocumentParsedLine parsed = PungentRichDocumentParser.Parse(temp).lines.FirstOrDefault();
            if (parsed == null || parsed.kind == PungentRichDocumentParsedLineKind.Blank)
                return null;

            switch (parsed.kind)
            {
                case PungentRichDocumentParsedLineKind.Heading:
                    return PungentRichDocumentBlock.Heading(parsed.text, parsed.headingLevel <= 0 ? 2 : parsed.headingLevel);
                case PungentRichDocumentParsedLineKind.Bullet:
                    return PungentRichDocumentBlock.List(PungentRichDocumentBlockType.BulletList, new[] { parsed.text });
                case PungentRichDocumentParsedLineKind.Checklist:
                    return new PungentRichDocumentBlock
                    {
                        type = PungentRichDocumentBlockType.Checklist,
                        items = new List<PungentRichDocumentBlockItem>
                        {
                            new PungentRichDocumentBlockItem { text = parsed.text, isChecked = parsed.isChecked }
                        }
                    };
                case PungentRichDocumentParsedLineKind.Quote:
                    return PungentRichDocumentBlock.Quote(parsed.text);
                case PungentRichDocumentParsedLineKind.Code:
                    return PungentRichDocumentBlock.Code(parsed.text);
                case PungentRichDocumentParsedLineKind.Divider:
                    return PungentRichDocumentBlock.Divider();
                case PungentRichDocumentParsedLineKind.SpeakerLine:
                    return PungentRichDocumentBlock.SpeakerLine(parsed.speaker, parsed.text);
                case PungentRichDocumentParsedLineKind.DialogueChoice:
                    return PungentRichDocumentBlock.DialogueChoice(parsed.text);
                case PungentRichDocumentParsedLineKind.QuestObjective:
                    return PungentRichDocumentBlock.QuestObjective(parsed.text);
                case PungentRichDocumentParsedLineKind.TutorialStep:
                    return PungentRichDocumentBlock.TutorialStep(parsed.text);
                case PungentRichDocumentParsedLineKind.CommandPlaceholder:
                    return PungentRichDocumentBlock.CommandPlaceholder(parsed.commandKey);
                case PungentRichDocumentParsedLineKind.GameCopy:
                    return PungentRichDocumentBlock.Paragraph(parsed.label + ": " + parsed.text);
                default:
                    return PungentRichDocumentBlock.Paragraph(parsed.text);
            }
        }

        private static void AddOrMergeBlock(List<PungentRichDocumentBlock> blocks, PungentRichDocumentBlock block)
        {
            PungentRichDocumentBlock previous = blocks.Count == 0 ? null : blocks[blocks.Count - 1];
            if (previous != null &&
                block.items != null &&
                block.items.Count > 0 &&
                previous.type == block.type &&
                (block.type == PungentRichDocumentBlockType.BulletList || block.type == PungentRichDocumentBlockType.Checklist))
            {
                previous.items.AddRange(block.items);
                return;
            }

            blocks.Add(block);
        }

        private static string GenerateListText(PungentRichDocumentBlock block, bool checklist)
        {
            IEnumerable<PungentRichDocumentBlockItem> items = block.items ?? Enumerable.Empty<PungentRichDocumentBlockItem>();
            return string.Join(Environment.NewLine, items
                .Where(item => item != null)
                .Select(item => checklist
                    ? "- [" + (item.isChecked ? "x" : " ") + "] " + (item.text ?? string.Empty).Trim()
                    : "- " + (item.text ?? string.Empty).Trim())
                .ToArray());
        }

        private static void AddTextLinesAsItems(PungentRichDocumentBlock block, string text, bool checklist)
        {
            string[] lines = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string clean = StripLeadingConvention(line);
                if (!string.IsNullOrWhiteSpace(clean))
                    block.items.Add(new PungentRichDocumentBlockItem { text = clean, isChecked = checklist && line.Contains("[x]") });
            }

            if (block.items.Count == 0)
                block.items.Add(new PungentRichDocumentBlockItem { text = StripLeadingConvention(text), isChecked = false });
        }

        private static void SplitSpeakerText(string readable, out string speaker, out string text)
        {
            speaker = "Speaker";
            text = StripLeadingConvention(readable);
            int colon = (readable ?? string.Empty).IndexOf(':');
            if (colon <= 0)
                return;

            speaker = readable.Substring(0, colon).Trim();
            text = readable.Substring(colon + 1).Trim();
            if (string.IsNullOrWhiteSpace(speaker))
                speaker = "Speaker";
        }

        private static string FirstTokenKey(string text)
        {
            List<string> tokens = PungentRichDocumentParser.ExtractTokenKeys(text);
            return tokens.Count > 0 ? NormalizeTokenKey(tokens[0]) : NormalizeTokenKey(text);
        }

        private static string ExtractCommandKey(string text)
        {
            string clean = (text ?? string.Empty).Trim();
            if (clean.StartsWith("<command:", StringComparison.OrdinalIgnoreCase) && clean.EndsWith(">", StringComparison.Ordinal))
                return clean.Substring(9, clean.Length - 10).Trim();
            return StripLeadingConvention(clean).Trim('<', '>');
        }

        private static string NormalizeTokenKey(string key)
        {
            return PungentRichDocumentParser.NormalizeTokenKey(key);
        }

        private static string StripLeadingConvention(string text)
        {
            string clean = (text ?? string.Empty).Trim();
            if (clean.StartsWith("- [choice]", StringComparison.OrdinalIgnoreCase))
                return clean.Substring(10).Trim();
            if (clean.StartsWith("- [objective]", StringComparison.OrdinalIgnoreCase))
                return clean.Substring(13).Trim();
            if (clean.StartsWith("1. [step]", StringComparison.OrdinalIgnoreCase))
                return clean.Substring(9).Trim();
            if (clean.StartsWith("- [ ]", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
                return clean.Substring(5).Trim();
            if (clean.StartsWith("- ", StringComparison.Ordinal) || clean.StartsWith("* ", StringComparison.Ordinal))
                return clean.Substring(2).Trim();
            if (clean.StartsWith(">", StringComparison.Ordinal))
                return clean.TrimStart('>').Trim();
            if (clean.StartsWith("#", StringComparison.Ordinal))
                return clean.TrimStart('#').Trim();
            return clean;
        }
    }
#endif
}
