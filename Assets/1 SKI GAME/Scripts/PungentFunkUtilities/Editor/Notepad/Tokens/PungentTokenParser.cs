using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public sealed class PungentParsedToken
    {
        public string key;
        public int occurrenceCount;
        public List<string> contexts = new List<string>();
    }

    public static class PungentTokenParser
    {
        private static readonly Regex TokenRegex = new Regex("\\{([^{}]+)\\}", RegexOptions.Compiled);
        private static readonly Regex ValidKeyRegex = new Regex("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

        public static List<PungentParsedToken> Parse(string text)
        {
            Dictionary<string, PungentParsedToken> result = new Dictionary<string, PungentParsedToken>(System.StringComparer.OrdinalIgnoreCase);
            foreach (Match match in TokenRegex.Matches(text ?? string.Empty))
            {
                string key = NormalizeKey(match.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (!result.TryGetValue(key, out PungentParsedToken parsed))
                {
                    parsed = new PungentParsedToken { key = key };
                    result[key] = parsed;
                }
                parsed.occurrenceCount++;
                parsed.contexts.Add(ExtractContext(text, match.Index, match.Length));
            }
            return result.Values.OrderBy(t => t.key).ToList();
        }

        public static string NormalizeKey(string key) => PungentTokenDatabase.NormalizeKey(key);

        public static bool IsValidKey(string key)
        {
            string clean = NormalizeKey(key);
            return !string.IsNullOrWhiteSpace(clean) && ValidKeyRegex.IsMatch(clean);
        }

        public static string ResolvePreview(string text)
        {
            string result = text ?? string.Empty;
            foreach (PungentTokenDefinition token in PungentTokenStorage.Database.tokens)
            {
                if (token == null || string.IsNullOrWhiteSpace(token.key))
                    continue;
                result = result.Replace("{" + NormalizeKey(token.key) + "}", token.previewValue ?? string.Empty);
            }
            return result;
        }

        private static string ExtractContext(string text, int index, int length)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            int start = UnityEngine.Mathf.Max(0, index - 32);
            int end = UnityEngine.Mathf.Min(text.Length, index + length + 32);
            return text.Substring(start, end - start).Replace("\r", " ").Replace("\n", " ");
        }
    }
#endif
}
