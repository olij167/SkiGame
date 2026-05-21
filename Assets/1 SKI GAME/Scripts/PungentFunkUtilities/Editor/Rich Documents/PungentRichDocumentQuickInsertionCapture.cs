using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentQuickInsertionCapture
    {
        public static PungentRichDocumentInsertionDefinition CreateDefinition(
            IEnumerable<PungentRichDocumentCanvasSegment> segments,
            string selectedText,
            bool preferInlineChip)
        {
            List<PungentRichDocumentCanvasSegment> safeSegments = (segments ?? Enumerable.Empty<PungentRichDocumentCanvasSegment>())
                .Where(segment => segment != null)
                .ToList();
            string sourceText = !string.IsNullOrWhiteSpace(selectedText)
                ? selectedText.Trim()
                : string.Join(Environment.NewLine, safeSegments.Select(segment => segment.ReadableText()).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(sourceText))
                sourceText = "Custom insertion";

            bool chip = preferInlineChip && safeSegments.Count <= 1 && sourceText.IndexOf('\n') < 0;
            string label = MakeLabel(sourceText);
            PungentRichDocumentInsertionDefinition definition = chip
                ? PungentRichDocumentInsertionDefinition.CreateChip(label)
                : new PungentRichDocumentInsertionDefinition
                {
                    id = "custom-" + PungentAuthoringId.Normalize(label),
                    displayName = label,
                    category = "Custom",
                    syntaxAlias = label,
                    renderMode = PungentRichDocumentInsertionRenderMode.SlimBlock,
                    layoutMode = safeSegments.Count > 1 ? PungentRichDocumentInsertionLayoutMode.HorizontalWrap : PungentRichDocumentInsertionLayoutMode.Vertical,
                    tintHex = "6FA8DC",
                    defaultText = sourceText
                };

            if (!chip)
            {
                definition.fields = BuildFieldsFromSegments(safeSegments);
                if (definition.fields.Count == 0)
                    definition.fields.Add(PungentRichDocumentInsertionFieldDefinition.Create("text", "Text", PungentRichDocumentInsertionFieldKind.LongText, sourceText));
            }

            definition.NormalizeInPlace();
            return definition;
        }

        private static List<PungentRichDocumentInsertionFieldDefinition> BuildFieldsFromSegments(List<PungentRichDocumentCanvasSegment> segments)
        {
            List<PungentRichDocumentInsertionFieldDefinition> fields = new List<PungentRichDocumentInsertionFieldDefinition>();
            foreach (PungentRichDocumentCanvasSegment segment in segments)
            {
                if (segment == null)
                    continue;

                string key = KeyForSegment(segment, fields.Count);
                string label = LabelForSegment(segment, fields.Count);
                string value = segment.ReadableText();
                fields.Add(PungentRichDocumentInsertionFieldDefinition.Create(key, label, PungentRichDocumentInsertionFieldKind.LongText, value));
            }

            return fields;
        }

        private static string MakeLabel(string sourceText)
        {
            string clean = sourceText ?? string.Empty;
            clean = clean.Replace("\r", " ").Replace("\n", " ").Trim();
            if (clean.Length > 32)
                clean = clean.Substring(0, 32).Trim();
            clean = string.IsNullOrWhiteSpace(clean) ? "Custom Insertion" : clean;
            return clean;
        }

        private static string KeyForSegment(PungentRichDocumentCanvasSegment segment, int index)
        {
            string prefix = segment == null ? "field" : segment.kind.ToString();
            return PungentAuthoringId.Normalize(prefix + "-" + (index + 1));
        }

        private static string LabelForSegment(PungentRichDocumentCanvasSegment segment, int index)
        {
            if (segment == null)
                return "Field " + (index + 1);
            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine: return "Dialogue Line";
                case PungentRichDocumentStreamNodeKind.DialogueChoice: return "Choice";
                case PungentRichDocumentStreamNodeKind.QuestObjective: return "Objective";
                case PungentRichDocumentStreamNodeKind.TutorialStep: return "Tutorial Step";
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder: return "Command";
                case PungentRichDocumentStreamNodeKind.Heading: return "Heading";
                default: return "Text " + (index + 1);
            }
        }
    }
#endif
}
