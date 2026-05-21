using System;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentTextSelectionState
    {
        public int segmentIndex = -1;
        public int startIndex;
        public int endIndex;
        public string selectedText = string.Empty;
        public string semanticBindingId = string.Empty;
        public string annotationId = string.Empty;
        public string tokenRawText = string.Empty;
        public string tokenKey = string.Empty;
        public string tokenKind = string.Empty;

        public bool HasSegment => segmentIndex >= 0;
        public bool HasTextSelection => HasSegment && endIndex > startIndex && !string.IsNullOrEmpty(selectedText);
        public bool HasSemanticSelection => !string.IsNullOrWhiteSpace(semanticBindingId);
        public bool HasAnnotationSelection => !string.IsNullOrWhiteSpace(annotationId);
        public bool HasTokenSelection => !string.IsNullOrWhiteSpace(tokenRawText);

        public int Length => Math.Max(0, endIndex - startIndex);

        public void Clear()
        {
            segmentIndex = -1;
            startIndex = 0;
            endIndex = 0;
            selectedText = string.Empty;
            semanticBindingId = string.Empty;
            annotationId = string.Empty;
            tokenRawText = string.Empty;
            tokenKey = string.Empty;
            tokenKind = string.Empty;
        }

        public void SetTextSelection(int segment, int start, int end, string source)
        {
            segmentIndex = segment;
            startIndex = Math.Max(0, Math.Min(start, end));
            endIndex = Math.Max(startIndex, Math.Max(start, end));
            string safe = source ?? string.Empty;
            if (startIndex > safe.Length)
                startIndex = safe.Length;
            if (endIndex > safe.Length)
                endIndex = safe.Length;
            selectedText = endIndex > startIndex ? safe.Substring(startIndex, endIndex - startIndex) : safe;
            semanticBindingId = string.Empty;
            annotationId = string.Empty;
            tokenRawText = string.Empty;
            tokenKey = string.Empty;
            tokenKind = string.Empty;
        }

        public void SetSemanticSelection(int segment, string bindingId, string text)
        {
            segmentIndex = segment;
            startIndex = 0;
            endIndex = 0;
            selectedText = text ?? string.Empty;
            semanticBindingId = bindingId ?? string.Empty;
            annotationId = string.Empty;
            tokenRawText = string.Empty;
            tokenKey = string.Empty;
            tokenKind = string.Empty;
        }

        public void SetAnnotationSelection(int segment, string annotation, string text)
        {
            segmentIndex = segment;
            startIndex = 0;
            endIndex = 0;
            selectedText = text ?? string.Empty;
            semanticBindingId = string.Empty;
            annotationId = annotation ?? string.Empty;
            tokenRawText = string.Empty;
            tokenKey = string.Empty;
            tokenKind = string.Empty;
        }

        public void SetTokenSelection(int segment, string rawText, string key, string kind, string bindingId = null)
        {
            segmentIndex = segment;
            startIndex = 0;
            endIndex = 0;
            selectedText = rawText ?? string.Empty;
            semanticBindingId = bindingId ?? string.Empty;
            annotationId = string.Empty;
            tokenRawText = rawText ?? string.Empty;
            tokenKey = key ?? string.Empty;
            tokenKind = kind ?? string.Empty;
        }

        public PungentRichDocumentTextSelectionState Copy()
        {
            return new PungentRichDocumentTextSelectionState
            {
                segmentIndex = segmentIndex,
                startIndex = startIndex,
                endIndex = endIndex,
                selectedText = selectedText,
                semanticBindingId = semanticBindingId,
                annotationId = annotationId,
                tokenRawText = tokenRawText,
                tokenKey = tokenKey,
                tokenKind = tokenKind
            };
        }
    }
#endif
}
