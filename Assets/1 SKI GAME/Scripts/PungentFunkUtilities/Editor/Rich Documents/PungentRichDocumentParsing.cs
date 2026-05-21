using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public enum PungentRichDocumentParsedLineKind
    {
        Blank = 0,
        Paragraph = 10,
        Heading = 20,
        Bullet = 30,
        Checklist = 40,
        Quote = 50,
        Code = 60,
        Divider = 70,
        SpeakerLine = 100,
        DialogueChoice = 110,
        QuestObjective = 120,
        TutorialStep = 130,
        CommandPlaceholder = 140,
        GameCopy = 150
    }

    public sealed class PungentRichDocumentParsedLine
    {
        public int lineNumber;
        public string rawText = string.Empty;
        public string text = string.Empty;
        public string label = string.Empty;
        public string speaker = string.Empty;
        public string commandKey = string.Empty;
        public string semanticId = string.Empty;
        public PungentRichDocumentSemanticKind semanticKind = PungentRichDocumentSemanticKind.None;
        public List<PungentRichDocumentSemanticField> semanticFields = new List<PungentRichDocumentSemanticField>();
        public int headingLevel;
        public bool isChecked;
        public bool isInsideCodeBlock;
        public bool isSemanticSpan;
        public PungentRichDocumentParsedLineKind kind = PungentRichDocumentParsedLineKind.Paragraph;
        public PungentRichDocumentBlockType blockType = PungentRichDocumentBlockType.Paragraph;
        public PungentRichDocumentBlockConvention convention = PungentRichDocumentBlockConvention.None;
        public List<string> tokenKeys = new List<string>();
        public List<PungentRichDocumentBindingExpression> bindingExpressions = new List<PungentRichDocumentBindingExpression>();
    }

    public sealed class PungentRichDocumentParsedDocument
    {
        public string documentId = string.Empty;
        public string title = string.Empty;
        public string sourceBody = string.Empty;
        public int lineCount;
        public int wordCount;
        public List<PungentRichDocumentParsedLine> lines = new List<PungentRichDocumentParsedLine>();
        public List<string> tokenKeys = new List<string>();

        public bool HasLines => lines != null && lines.Count > 0;
    }

    public sealed class PungentRichDocumentExtractedEntry
    {
        public string id = Guid.NewGuid().ToString("N");
        public string category = string.Empty;
        public string targetType = string.Empty;
        public string key = string.Empty;
        public string title = string.Empty;
        public string value = string.Empty;
        public string sourceText = string.Empty;
        public int sourceLine;
        public int occurrenceCount = 1;
        public bool selected = true;
        public bool canApply;
        public string disabledReason = string.Empty;
        public string semanticBindingId = string.Empty;
        public string adapterId = string.Empty;
        public string adapterDisplayName = string.Empty;
        public string currentValue = string.Empty;
        public string currentValueStatus = string.Empty;
        public PungentAuthoringTarget target;
        public string bindingExpression = string.Empty;
        public PungentAuthoringBindingSlot bindingSlot;
        public PungentAuthoringBindingPath bindingPath;
    }

    public sealed class PungentRichDocumentExtractionResult
    {
        public string documentId = string.Empty;
        public string documentTitle = string.Empty;
        public string createdUtc = DateTime.UtcNow.ToString("o");
        public List<PungentRichDocumentExtractedEntry> entries = new List<PungentRichDocumentExtractedEntry>();
        public List<string> warnings = new List<string>();

        public bool HasEntries => entries != null && entries.Count > 0;
    }

    public sealed class PungentRichDocumentPropagationProposal
    {
        public string documentId = string.Empty;
        public string documentTitle = string.Empty;
        public string createdUtc = DateTime.UtcNow.ToString("o");
        public List<PungentRichDocumentExtractedEntry> entries = new List<PungentRichDocumentExtractedEntry>();

        public int SelectedApplyCount =>
            entries == null ? 0 : entries.Count(entry => entry != null && entry.selected && entry.canApply);
    }

    public static class PungentRichDocumentParser
    {
        public const string TargetTokenPreviewValue = "TokenPreviewValue";
        public const string TargetDialogueLine = "DialogueLine";
        public const string TargetDialogueChoice = "DialogueChoice";
        public const string TargetQuestObjective = "QuestObjective";
        public const string TargetTutorialStep = "TutorialStep";
        public const string TargetCommand = "Command";
        public const string TargetGameCopy = "GameCopy";
        public const string TargetCustomInsertion = "CustomInsertion";

        private static readonly Regex TokenRegex = new Regex("\\{([^{}]+)\\}", RegexOptions.Compiled);
        private static readonly Regex ValidTokenKeyRegex = new Regex("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
        private static readonly Regex HeadingRegex = new Regex("^(#{1,6})\\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex ChecklistRegex = new Regex("^[-*]\\s+\\[(x|X|\\s)\\]\\s*(.*)$", RegexOptions.Compiled);
        private static readonly Regex ChoiceRegex = new Regex("^[-*]\\s+\\[choice\\]\\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ObjectiveRegex = new Regex("^[-*]\\s+\\[objective\\]\\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TutorialStepRegex = new Regex("^\\d+\\.\\s+\\[step\\]\\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CommandRegex = new Regex("^<command:([^>]*)>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex GameCopyRegex = new Regex("^(Item|Character|Description|Hint|Loading Hint|Quest|Reward|Prompt):\\s*(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SpeakerRegex = new Regex("^([A-Za-z][A-Za-z0-9 _.'-]{0,40}):\\s*(.*)$", RegexOptions.Compiled);

        public static PungentRichDocumentParsedDocument Parse(PungentRichDocument document)
        {
            string body = document == null ? string.Empty : document.bodyText ?? string.Empty;
            PungentRichDocumentParsedDocument parsed = new PungentRichDocumentParsedDocument
            {
                documentId = document == null ? string.Empty : document.id ?? string.Empty,
                title = document == null ? string.Empty : document.title ?? string.Empty,
                sourceBody = body,
                wordCount = WordCount(body)
            };

            string normalized = body.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Length == 0 ? Array.Empty<string>() : normalized.Split('\n');
            parsed.lineCount = lines.Length;

            bool codeBlock = false;
            for (int i = 0; i < lines.Length; i++)
            {
                PungentRichDocumentParsedLine line = ParseLine(lines[i], i + 1, codeBlock);
                if ((lines[i] ?? string.Empty).TrimStart().StartsWith("```", StringComparison.Ordinal))
                    codeBlock = !codeBlock;
                parsed.lines.Add(line);
            }

            parsed.tokenKeys = parsed.lines
                .SelectMany(line => line.tokenKeys ?? new List<string>())
                .Where(IsValidTokenKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return parsed;
        }

        public static List<string> ExtractTokenKeys(string text)
        {
            return TokenRegex.Matches(text ?? string.Empty)
                .Cast<Match>()
                .Select(match => NormalizeTokenKey(match.Groups[1].Value))
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Where(token => !IsCustomChipToken(token))
                .Where(token => !IsBindingExpressionToken(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool IsValidTokenKey(string key)
        {
            string clean = NormalizeTokenKey(key);
            return !string.IsNullOrWhiteSpace(clean) && ValidTokenKeyRegex.IsMatch(clean);
        }

        public static string NormalizeTokenKey(string key)
        {
            return (key ?? string.Empty).Trim().Trim('{', '}').Trim();
        }

        private static bool IsCustomChipToken(string token)
        {
            return (token ?? string.Empty).Trim().StartsWith("chip:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBindingExpressionToken(string token)
        {
            string clean = (token ?? string.Empty).Trim();
            return clean.StartsWith("ref ", StringComparison.OrdinalIgnoreCase) ||
                   clean.StartsWith("custom ", StringComparison.OrdinalIgnoreCase);
        }

        private static PungentRichDocumentParsedLine ParseLine(string raw, int lineNumber, bool codeBlock)
        {
            string safeRaw = raw ?? string.Empty;
            string trimmed = safeRaw.Trim();
            PungentRichDocumentParsedLine line = new PungentRichDocumentParsedLine
            {
                lineNumber = lineNumber,
                rawText = safeRaw,
                text = trimmed,
                isInsideCodeBlock = codeBlock,
                tokenKeys = ExtractTokenKeys(safeRaw),
                bindingExpressions = PungentRichDocumentBindingExpressionParser.ExtractExpressions(safeRaw, false)
            };

            if (string.IsNullOrEmpty(trimmed))
            {
                line.kind = PungentRichDocumentParsedLineKind.Blank;
                return line;
            }

            if (codeBlock)
            {
                line.kind = PungentRichDocumentParsedLineKind.Code;
                line.blockType = PungentRichDocumentBlockType.Code;
                return line;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                line.kind = PungentRichDocumentParsedLineKind.Code;
                line.blockType = PungentRichDocumentBlockType.Code;
                return line;
            }

            if (PungentRichDocumentSemanticParser.TryParseWholeSemanticTag(trimmed, out PungentRichDocumentSemanticSpan semantic))
            {
                ApplySemanticSpan(line, semantic);
                return line;
            }

            Match heading = HeadingRegex.Match(trimmed);
            if (heading.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.Heading;
                line.blockType = PungentRichDocumentBlockType.Heading;
                line.headingLevel = heading.Groups[1].Value.Length;
                line.text = heading.Groups[2].Value.Trim();
                return line;
            }

            if (trimmed == "---" || trimmed == "***")
            {
                line.kind = PungentRichDocumentParsedLineKind.Divider;
                line.blockType = PungentRichDocumentBlockType.Divider;
                return line;
            }

            Match command = CommandRegex.Match(trimmed);
            if (command.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.CommandPlaceholder;
                line.blockType = PungentRichDocumentBlockType.CommandPlaceholder;
                line.convention = PungentRichDocumentBlockConvention.CommandPlaceholder;
                line.commandKey = command.Groups[1].Value.Trim();
                line.text = line.commandKey;
                return line;
            }

            Match choice = ChoiceRegex.Match(trimmed);
            if (choice.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.DialogueChoice;
                line.blockType = PungentRichDocumentBlockType.DialogueChoice;
                line.convention = PungentRichDocumentBlockConvention.DialogueChoice;
                line.text = choice.Groups[1].Value.Trim();
                return line;
            }

            Match objective = ObjectiveRegex.Match(trimmed);
            if (objective.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.QuestObjective;
                line.blockType = PungentRichDocumentBlockType.QuestObjective;
                line.convention = PungentRichDocumentBlockConvention.QuestObjective;
                line.text = objective.Groups[1].Value.Trim();
                return line;
            }

            Match tutorial = TutorialStepRegex.Match(trimmed);
            if (tutorial.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.TutorialStep;
                line.blockType = PungentRichDocumentBlockType.TutorialStep;
                line.convention = PungentRichDocumentBlockConvention.TutorialStep;
                line.text = tutorial.Groups[1].Value.Trim();
                return line;
            }

            Match checklist = ChecklistRegex.Match(trimmed);
            if (checklist.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.Checklist;
                line.blockType = PungentRichDocumentBlockType.Checklist;
                line.isChecked = string.Equals(checklist.Groups[1].Value, "x", StringComparison.OrdinalIgnoreCase);
                line.text = checklist.Groups[2].Value.Trim();
                return line;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                line.kind = PungentRichDocumentParsedLineKind.Bullet;
                line.blockType = PungentRichDocumentBlockType.BulletList;
                line.text = trimmed.Substring(2).Trim();
                return line;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                line.kind = PungentRichDocumentParsedLineKind.Quote;
                line.blockType = PungentRichDocumentBlockType.Quote;
                line.text = trimmed.TrimStart('>').Trim();
                return line;
            }

            Match gameCopy = GameCopyRegex.Match(trimmed);
            if (gameCopy.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.GameCopy;
                line.label = gameCopy.Groups[1].Value.Trim();
                line.text = gameCopy.Groups[2].Value.Trim();
                return line;
            }

            Match speaker = SpeakerRegex.Match(trimmed);
            if (speaker.Success)
            {
                line.kind = PungentRichDocumentParsedLineKind.SpeakerLine;
                line.blockType = PungentRichDocumentBlockType.SpeakerLine;
                line.convention = PungentRichDocumentBlockConvention.SpeakerLine;
                line.speaker = speaker.Groups[1].Value.Trim();
                line.text = speaker.Groups[2].Value.Trim();
                return line;
            }

            line.kind = PungentRichDocumentParsedLineKind.Paragraph;
            line.blockType = PungentRichDocumentBlockType.Paragraph;
            line.text = trimmed;
            return line;
        }

        private static void ApplySemanticSpan(PungentRichDocumentParsedLine line, PungentRichDocumentSemanticSpan semantic)
        {
            if (line == null || semantic == null)
                return;

            line.isSemanticSpan = true;
            line.semanticId = semantic.id;
            line.semanticKind = semantic.kind;
            line.semanticFields = semantic.fields ?? new List<PungentRichDocumentSemanticField>();
            line.rawText = semantic.sourceText;
            line.text = semantic.innerText ?? string.Empty;
            line.blockType = PungentRichDocumentSemanticParser.BlockTypeForSemanticKind(semantic.kind);

            switch (semantic.kind)
            {
                case PungentRichDocumentSemanticKind.DialogueLine:
                    line.kind = PungentRichDocumentParsedLineKind.SpeakerLine;
                    line.convention = PungentRichDocumentBlockConvention.SpeakerLine;
                    line.speaker = semantic.GetField("speaker");
                    break;
                case PungentRichDocumentSemanticKind.DialogueChoice:
                    line.kind = PungentRichDocumentParsedLineKind.DialogueChoice;
                    line.convention = PungentRichDocumentBlockConvention.DialogueChoice;
                    break;
                case PungentRichDocumentSemanticKind.QuestObjective:
                    line.kind = PungentRichDocumentParsedLineKind.QuestObjective;
                    line.convention = PungentRichDocumentBlockConvention.QuestObjective;
                    break;
                case PungentRichDocumentSemanticKind.TutorialStep:
                    line.kind = PungentRichDocumentParsedLineKind.TutorialStep;
                    line.convention = PungentRichDocumentBlockConvention.TutorialStep;
                    break;
                case PungentRichDocumentSemanticKind.Command:
                    line.kind = PungentRichDocumentParsedLineKind.CommandPlaceholder;
                    line.convention = PungentRichDocumentBlockConvention.CommandPlaceholder;
                    line.commandKey = semantic.GetField("key");
                    if (string.IsNullOrWhiteSpace(line.commandKey))
                        line.commandKey = line.text;
                    break;
                case PungentRichDocumentSemanticKind.HintCopy:
                case PungentRichDocumentSemanticKind.ItemCopy:
                case PungentRichDocumentSemanticKind.CharacterCopy:
                case PungentRichDocumentSemanticKind.GameCopy:
                case PungentRichDocumentSemanticKind.CustomInsertion:
                    line.kind = PungentRichDocumentParsedLineKind.GameCopy;
                    line.label = semantic.kind == PungentRichDocumentSemanticKind.CustomInsertion
                        ? semantic.GetField(PungentRichDocumentInsertionDefinitionRegistry.DefinitionLabelFieldKey)
                        : semantic.GetField("category");
                    if (string.IsNullOrWhiteSpace(line.label))
                        line.label = PungentRichDocumentSemanticParser.DisplayName(semantic.kind);
                    break;
                case PungentRichDocumentSemanticKind.CustomChip:
                    line.kind = PungentRichDocumentParsedLineKind.Paragraph;
                    line.blockType = PungentRichDocumentBlockType.Paragraph;
                    break;
                default:
                    line.kind = PungentRichDocumentParsedLineKind.Paragraph;
                    line.blockType = PungentRichDocumentBlockType.Paragraph;
                    break;
            }
        }

        private static int WordCount(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? 0
                : text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    public static class PungentRichDocumentExtractor
    {
        public static PungentRichDocumentExtractionResult Extract(PungentRichDocument document, PungentRichDocumentParsedDocument parsed = null)
        {
            parsed = parsed ?? PungentRichDocumentParser.Parse(document);
            PungentRichDocumentExtractionResult result = new PungentRichDocumentExtractionResult
            {
                documentId = document == null ? string.Empty : document.id ?? string.Empty,
                documentTitle = document == null ? string.Empty : document.title ?? string.Empty
            };

            Dictionary<string, PungentRichDocumentExtractedEntry> tokenEntries = new Dictionary<string, PungentRichDocumentExtractedEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentRichDocumentParsedLine line in parsed.lines ?? new List<PungentRichDocumentParsedLine>())
            {
                if (line == null)
                    continue;

                AddTokenEntries(line, tokenEntries);
                AddBindingExpressionEntries(document, line, result.entries);
                AddGameTextEntry(document, line, result.entries);
            }

            result.entries.InsertRange(0, tokenEntries.Values.OrderBy(entry => entry.key, StringComparer.OrdinalIgnoreCase));
            if (!result.entries.Any(entry => entry != null && entry.canApply))
                result.warnings.Add("No directly applicable token preview values or bound adapter integrations were found. Game-text entries remain visible for review and copy until they are bound or bridged.");
            return result;
        }

        public static PungentRichDocumentPropagationProposal CreateProposal(PungentRichDocument document, PungentRichDocumentParsedDocument parsed = null)
        {
            PungentRichDocumentExtractionResult extraction = Extract(document, parsed);
            return new PungentRichDocumentPropagationProposal
            {
                documentId = extraction.documentId,
                documentTitle = extraction.documentTitle,
                entries = extraction.entries ?? new List<PungentRichDocumentExtractedEntry>()
            };
        }

        private static void AddTokenEntries(PungentRichDocumentParsedLine line, Dictionary<string, PungentRichDocumentExtractedEntry> entries)
        {
            foreach (string token in line.tokenKeys ?? new List<string>())
            {
                string clean = PungentRichDocumentParser.NormalizeTokenKey(token);
                if (string.IsNullOrWhiteSpace(clean))
                    continue;

                if (!entries.TryGetValue(clean, out PungentRichDocumentExtractedEntry entry))
                {
                    string proposed = TryExtractTokenPreviewValue(line.rawText, clean, out string value) ? value : string.Empty;
                    entry = new PungentRichDocumentExtractedEntry
                    {
                        category = "Token",
                        targetType = PungentRichDocumentParser.TargetTokenPreviewValue,
                        key = clean,
                        title = "Token: " + clean,
                        value = proposed,
                        sourceText = line.rawText,
                        sourceLine = line.lineNumber,
                        canApply = !string.IsNullOrWhiteSpace(proposed),
                        disabledReason = string.IsNullOrWhiteSpace(proposed) ? "No proposed token preview value was found on this line." : string.Empty
                    };
                    entries[clean] = entry;
                    continue;
                }

                entry.occurrenceCount++;
                if (!entry.canApply && TryExtractTokenPreviewValue(line.rawText, clean, out string laterValue))
                {
                    entry.value = laterValue;
                    entry.sourceText = line.rawText;
                    entry.sourceLine = line.lineNumber;
                    entry.canApply = true;
                    entry.disabledReason = string.Empty;
                }
            }
        }

        private static void AddBindingExpressionEntries(PungentRichDocument document, PungentRichDocumentParsedLine line, List<PungentRichDocumentExtractedEntry> entries)
        {
            foreach (PungentRichDocumentBindingExpression expression in line.bindingExpressions ?? new List<PungentRichDocumentBindingExpression>())
            {
                if (expression == null)
                    continue;

                PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.FindExpressionBinding(document, expression);
                PungentRichDocumentBindingPreview preview = PungentRichDocumentBindingApplicationService.Preview(binding);
                PungentAuthoringBindingLinkPreview bridgePreview = PungentAuthoringBindingBridgeService.PreviewLink(
                    PungentRichDocumentSemanticBindingService.CreateBindingLink(document, binding));
                bool bindingCanApply = PungentRichDocumentBindingApplicationService.CanApply(binding, out string disabledReason);
                PungentAuthoringBindingPreview endpointPreview = bridgePreview.endpointPreview;
                bool canApply = false;

                string title = expression.kind == PungentRichDocumentBindingExpressionKind.Reference
                    ? "Reference: " + expression.targetName
                    : "Custom: " + expression.customId;
                string value = expression.kind == PungentRichDocumentBindingExpressionKind.CustomDefinition
                    ? expression.displayText
                    : expression.rawText;

                entries.Add(new PungentRichDocumentExtractedEntry
                {
                    category = expression.kind == PungentRichDocumentBindingExpressionKind.Reference ? "Reference Token" : "Custom Insertion",
                    targetType = expression.kind.ToString(),
                    key = expression.kind == PungentRichDocumentBindingExpressionKind.Reference ? expression.targetName : expression.customId,
                    title = title,
                    value = value ?? string.Empty,
                    sourceText = line.rawText,
                    sourceLine = line.lineNumber,
                    semanticBindingId = binding == null ? string.Empty : binding.id,
                    target = binding == null ? null : binding.target,
                    adapterId = endpointPreview == null ? preview == null ? string.Empty : preview.adapterId : endpointPreview.adapterId,
                    adapterDisplayName = endpointPreview == null ? preview == null ? string.Empty : preview.adapterDisplayName : endpointPreview.adapterDisplayName,
                    currentValue = endpointPreview == null ? preview == null ? string.Empty : preview.currentValue : endpointPreview.currentValue,
                    currentValueStatus = string.IsNullOrWhiteSpace(bridgePreview.readiness)
                        ? preview == null ? string.Empty : preview.warning
                        : bridgePreview.readiness,
                    canApply = canApply,
                    disabledReason = canApply
                        ? string.Empty
                        : expression.kind == PungentRichDocumentBindingExpressionKind.Reference
                            ? (bindingCanApply
                                ? "Reference token is resolved for inline context. Bind semantic text or token preview output to apply document-authored values."
                                : (string.IsNullOrWhiteSpace(disabledReason) ? "Reference token is parsed but has not been resolved to an applicable target." : disabledReason))
                            : "Custom insertion token is parsed and ready for insertion definition resolution.",
                    bindingExpression = expression.rawText,
                    bindingSlot = binding == null ? expression.bindingSlot : binding.bindingSlot,
                    bindingPath = binding == null ? expression.bindingPath : binding.bindingPath
                });
            }
        }

        private static void AddGameTextEntry(PungentRichDocument document, PungentRichDocumentParsedLine line, List<PungentRichDocumentExtractedEntry> entries)
        {
            string category;
            string targetType;
            string key = string.Empty;
            string title;
            string value;

            switch (line.kind)
            {
                case PungentRichDocumentParsedLineKind.SpeakerLine:
                    category = "Dialogue";
                    targetType = PungentRichDocumentParser.TargetDialogueLine;
                    key = line.speaker;
                    title = string.IsNullOrWhiteSpace(line.speaker) ? "Speaker Line" : line.speaker;
                    value = line.text;
                    break;
                case PungentRichDocumentParsedLineKind.DialogueChoice:
                    category = "Dialogue";
                    targetType = PungentRichDocumentParser.TargetDialogueChoice;
                    title = "Dialogue Choice";
                    value = line.text;
                    break;
                case PungentRichDocumentParsedLineKind.QuestObjective:
                    category = "Quest";
                    targetType = PungentRichDocumentParser.TargetQuestObjective;
                    title = "Quest Objective";
                    value = line.text;
                    break;
                case PungentRichDocumentParsedLineKind.TutorialStep:
                    category = "Tutorial";
                    targetType = PungentRichDocumentParser.TargetTutorialStep;
                    title = "Tutorial Step";
                    value = line.text;
                    break;
                case PungentRichDocumentParsedLineKind.CommandPlaceholder:
                    category = "Command";
                    targetType = PungentRichDocumentParser.TargetCommand;
                    key = line.commandKey;
                    title = "Command";
                    value = line.commandKey;
                    break;
                case PungentRichDocumentParsedLineKind.GameCopy:
                    category = line.semanticKind == PungentRichDocumentSemanticKind.CustomInsertion ? "Custom Insertion" : "Game Copy";
                    targetType = line.semanticKind == PungentRichDocumentSemanticKind.CustomInsertion
                        ? PungentRichDocumentParser.TargetCustomInsertion
                        : PungentRichDocumentParser.TargetGameCopy;
                    key = line.label;
                    title = string.IsNullOrWhiteSpace(line.label) ? category : line.label;
                    value = line.text;
                    break;
                default:
                    return;
            }

            PungentRichDocumentSemanticBinding binding = string.IsNullOrWhiteSpace(line.semanticId)
                ? null
                : PungentRichDocumentSemanticBindingService.FindBinding(document, line.semanticId);
            PungentRichDocumentBindingPreview preview = PungentRichDocumentBindingApplicationService.Preview(binding);
            PungentAuthoringBindingLinkPreview bridgePreview = PungentAuthoringBindingBridgeService.PreviewLink(
                PungentRichDocumentSemanticBindingService.CreateBindingLink(document, binding));
            bool canApply = PungentRichDocumentBindingApplicationService.CanApply(binding, out string disabledReason);
            PungentAuthoringBindingPreview endpointPreview = bridgePreview.endpointPreview;
            string adapterId = endpointPreview == null ? preview == null ? string.Empty : preview.adapterId : endpointPreview.adapterId;
            string adapterDisplayName = endpointPreview == null ? preview == null ? string.Empty : preview.adapterDisplayName : endpointPreview.adapterDisplayName;
            string currentValue = endpointPreview == null ? preview == null ? string.Empty : preview.currentValue : endpointPreview.currentValue;
            string currentValueStatus = string.IsNullOrWhiteSpace(bridgePreview.readiness)
                ? preview == null ? string.Empty : preview.warning
                : bridgePreview.readiness;

            entries.Add(new PungentRichDocumentExtractedEntry
            {
                category = category,
                targetType = targetType,
                key = key ?? string.Empty,
                title = title ?? string.Empty,
                value = value ?? string.Empty,
                sourceText = line.rawText,
                sourceLine = line.lineNumber,
                semanticBindingId = binding == null ? string.Empty : binding.id,
                target = binding == null ? null : binding.target,
                bindingSlot = binding == null ? null : binding.bindingSlot,
                bindingPath = binding == null ? null : binding.bindingPath,
                bindingExpression = binding == null ? string.Empty : binding.bindingExpression,
                adapterId = adapterId,
                adapterDisplayName = adapterDisplayName,
                currentValue = currentValue,
                currentValueStatus = currentValueStatus,
                canApply = canApply,
                disabledReason = canApply
                    ? string.Empty
                    : (string.IsNullOrWhiteSpace(disabledReason) && preview != null ? preview.disabledReason : disabledReason)
            });
        }

        private static bool TryExtractTokenPreviewValue(string line, string tokenKey, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(tokenKey))
                return false;

            string escaped = Regex.Escape(tokenKey.Trim());
            string tokenPattern = "\\{" + escaped + "\\}";
            Regex[] patterns =
            {
                new Regex("^[\\s\\-*]*" + tokenPattern + "\\s*[:=\\-]\\s*(.+)$", RegexOptions.IgnoreCase),
                new Regex("^[\\s\\-*]*" + escaped + "\\s*[:=\\-]\\s*(.+)$", RegexOptions.IgnoreCase)
            };

            foreach (Regex pattern in patterns)
            {
                Match match = pattern.Match(line.Trim());
                if (!match.Success)
                    continue;

                value = match.Groups[1].Value.Trim();
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }
    }
#endif
}
