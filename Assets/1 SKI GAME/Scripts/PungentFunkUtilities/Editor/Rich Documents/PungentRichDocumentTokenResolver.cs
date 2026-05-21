using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Editor.ProjectAudit;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentTokenResolution
    {
        public string key = string.Empty;
        public string displayText = string.Empty;
        public string technicalText = string.Empty;
        public string tooltip = string.Empty;
        public bool known;
        public bool hasPreviewValue;
    }

    public static class PungentRichDocumentTokenResolver
    {
        public static List<PungentRichDocumentTokenResolution> ResolveInText(string text)
        {
            return PungentRichDocumentParser.ExtractTokenKeys(text)
                .Select(Resolve)
                .ToList();
        }

        public static PungentRichDocumentTokenResolution Resolve(string tokenKey)
        {
            string key = PungentRichDocumentParser.NormalizeTokenKey(tokenKey);
            PungentRichDocumentTokenResolution fallback = Unknown(key, "Token system has no matching token for this key.");
            if (string.IsNullOrWhiteSpace(key))
                return Unknown(string.Empty, "Token key is empty.");

            try
            {
                PungentTokenDatabase database = PungentTokenDatabase.instance;
                if (database == null || database.tokens == null)
                    return Unknown(key, "Token database is not loaded.");

                PungentTokenDefinition token = database.tokens.FirstOrDefault(item =>
                    item != null &&
                    string.Equals(PungentTokenDatabase.NormalizeKey(item.key), key, StringComparison.OrdinalIgnoreCase));
                if (token == null)
                    return fallback;

                string preview = string.IsNullOrWhiteSpace(token.previewValue) ? token.displayName : token.previewValue;
                if (string.IsNullOrWhiteSpace(preview))
                    preview = key;

                return new PungentRichDocumentTokenResolution
                {
                    key = key,
                    known = true,
                    hasPreviewValue = !string.IsNullOrWhiteSpace(token.previewValue),
                    displayText = preview,
                    technicalText = "{" + key + "}",
                    tooltip = (string.IsNullOrWhiteSpace(token.description) ? "Known token" : token.description.Trim()) + Environment.NewLine + "{" + key + "}"
                };
            }
            catch (Exception ex)
            {
                return Unknown(key, "Unable to read token preview data: " + ex.Message);
            }
        }

        private static PungentRichDocumentTokenResolution Unknown(string key, string reason)
        {
            string clean = PungentRichDocumentParser.NormalizeTokenKey(key);
            string tokenText = string.IsNullOrWhiteSpace(clean) ? "{token}" : "{" + clean + "}";
            return new PungentRichDocumentTokenResolution
            {
                key = clean,
                known = false,
                hasPreviewValue = false,
                displayText = tokenText,
                technicalText = tokenText,
                tooltip = reason ?? "Unknown token."
            };
        }
    }
#endif
}
