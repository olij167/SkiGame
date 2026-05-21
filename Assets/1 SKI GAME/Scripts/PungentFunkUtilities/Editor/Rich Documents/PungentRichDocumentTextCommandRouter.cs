using System.Collections.Generic;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentTextCommandRouter
    {
        public static bool WrapSelection(
            PungentRichDocumentCanvasDocument canvas,
            PungentRichDocumentTextSelectionState selection,
            PungentRichDocumentSemanticKind kind,
            IEnumerable<PungentRichDocumentSemanticField> fields,
            out string status)
        {
            status = string.Empty;
            if (canvas == null || selection == null || !selection.HasTextSelection)
            {
                status = "Select text before applying an integration.";
                return false;
            }

            if (selection.segmentIndex < 0 || selection.segmentIndex >= canvas.segments.Count)
            {
                status = "Selection is no longer available.";
                return false;
            }

            PungentRichDocumentCanvasSegment segment = canvas.segments[selection.segmentIndex];
            if (segment == null || !segment.IsPlainTextEditable)
            {
                status = "This content is not editable as inline text.";
                return false;
            }

            string source = SegmentEditableSource(segment);
            if (selection.startIndex < 0 || selection.endIndex > source.Length || selection.endIndex <= selection.startIndex)
            {
                status = "Selection range is no longer valid.";
                return false;
            }

            string selected = source.Substring(selection.startIndex, selection.endIndex - selection.startIndex);
            string tagged = PungentRichDocumentSemanticParser.CreateSemanticTag(kind, selected, fields);
            string next = source.Substring(0, selection.startIndex) + tagged + source.Substring(selection.endIndex);
            ApplySegmentEditableSource(segment, next);
            status = "Applied " + PungentRichDocumentSemanticParser.DisplayName(kind) + " integration.";
            return true;
        }

        public static bool ReplaceSemanticSpan(
            PungentRichDocumentCanvasDocument canvas,
            int segmentIndex,
            PungentRichDocumentSemanticSpan span,
            string replacement,
            out string status)
        {
            status = string.Empty;
            if (canvas == null || span == null || segmentIndex < 0 || segmentIndex >= canvas.segments.Count)
                return false;

            PungentRichDocumentCanvasSegment segment = canvas.segments[segmentIndex];
            string source = SegmentEditableSource(segment);
            if (span.startIndex < 0 || span.startIndex + span.length > source.Length)
            {
                status = "Semantic span is no longer valid.";
                return false;
            }

            string next = source.Substring(0, span.startIndex) + (replacement ?? string.Empty) + source.Substring(span.startIndex + span.length);
            ApplySegmentEditableSource(segment, next);
            status = "Updated semantic text.";
            return true;
        }

        public static string SegmentEditableSource(PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return string.Empty;

            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.Heading:
                case PungentRichDocumentStreamNodeKind.Quote:
                    return segment.text ?? string.Empty;
                default:
                    return segment.rawText ?? segment.text ?? string.Empty;
            }
        }

        public static void ApplySegmentEditableSource(PungentRichDocumentCanvasSegment segment, string value)
        {
            if (segment == null)
                return;

            string safe = value ?? string.Empty;
            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.Heading:
                case PungentRichDocumentStreamNodeKind.Quote:
                    segment.text = safe;
                    break;
                default:
                    segment.kind = PungentRichDocumentStreamNodeKind.Text;
                    segment.rawText = safe;
                    segment.text = safe;
                    segment.isSemanticSpan = false;
                    break;
            }
        }
    }
#endif
}
