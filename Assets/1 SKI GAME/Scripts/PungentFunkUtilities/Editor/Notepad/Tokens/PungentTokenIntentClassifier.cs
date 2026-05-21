using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public sealed class PungentTokenIntentFileContext
    {
        public string text = string.Empty;
        public string lowerText = string.Empty;
        public string sourcePath = string.Empty;
        public string lowerPath = string.Empty;
        public string extension = string.Empty;
        public bool hasPungentOptIn;
        public bool hasPungentOptOut;
        public bool hasKnownToken;
        public int braceCount;
        public int textLength;

        public float BraceDensity
        {
            get
            {
                return textLength <= 0 ? 0f : (float)braceCount / textLength;
            }
        }
    }

    public static class PungentTokenIntentClassifier
    {
        private static readonly Regex NumericOrFormatRegex = new Regex("^\\d+(?::[^{}]+)?$", RegexOptions.Compiled);
        private static readonly Regex NamespaceRegex = new Regex("^[A-Za-z][A-Za-z0-9_-]*(\\.[A-Za-z][A-Za-z0-9_-]*)+$", RegexOptions.Compiled);
        private static readonly Regex NamingStyleRegex = new Regex("^[A-Za-z][A-Za-z0-9]*(?:[._-][A-Za-z0-9]+)*$", RegexOptions.Compiled);
        private static readonly Regex JsonLikeContextRegex = new Regex("\"[^\"]*\"\\s*:\\s*|:\\s*\"?[A-Za-z0-9_.-]+\"?\\s*[,}]", RegexOptions.Compiled);
        private static readonly Regex CodeLikeContextRegex = new Regex("[;=<>\\[\\]()]|\\b(class|struct|void|return|if|else|for|while)\\b", RegexOptions.Compiled);
        private static readonly Regex ProseLikeContextRegex = new Regex("[A-Za-z][A-Za-z ,.'\"!?-]{12,}", RegexOptions.Compiled);

        public static PungentTokenIntentFileContext CreateContext(string text, string sourcePath)
        {
            string safeText = text ?? string.Empty;
            string lowerText = safeText.ToLowerInvariant();
            string safePath = sourcePath ?? string.Empty;

            PungentTokenIntentFileContext context = new PungentTokenIntentFileContext
            {
                text = safeText,
                lowerText = lowerText,
                sourcePath = safePath,
                lowerPath = safePath.Replace('\\', '/').ToLowerInvariant(),
                extension = Path.GetExtension(safePath).ToLowerInvariant(),
                hasPungentOptIn = ContainsAny(lowerText, "@pungent-tokens", "@pfu-tokens"),
                hasPungentOptOut = ContainsAny(lowerText, "@pungent-ignore-tokens", "@pfu-ignore-tokens"),
                hasKnownToken = ContainsKnownToken(safeText),
                braceCount = safeText.Count(c => c == '{' || c == '}'),
                textLength = safeText.Length
            };

            return context;
        }

        public static PungentTokenCandidate Classify(
            PungentTokenUsage usage,
            PungentTokenIntentFileContext context,
            PungentTokenScanSettings settings)
        {
            settings = settings ?? new PungentTokenScanSettings();
            settings.EnsureDefaults();

            string key = PungentTokenParser.NormalizeKey(usage == null ? string.Empty : usage.tokenKey);
            PungentTokenCandidate candidate = new PungentTokenCandidate
            {
                tokenKey = key,
                rawText = "{" + key + "}",
                sourceLabel = usage == null ? string.Empty : usage.sourceLabel,
                sourcePath = usage == null ? string.Empty : usage.sourcePath,
                assetGuid = usage == null ? string.Empty : usage.assetGuid,
                occurrenceCount = usage == null ? 0 : usage.occurrenceCount,
                contexts = usage == null || usage.contexts == null ? new List<string>() : new List<string>(usage.contexts)
            };

            if (usage == null || string.IsNullOrWhiteSpace(key))
                return Complete(candidate, 0, settings, "No token key.");

            List<string> reasons = candidate.reasons;
            int score = 0;

            if (context != null && context.hasPungentOptOut && settings.ignoreOptOutMarkedFiles)
                return Complete(candidate, -100, settings, "File has token opt-out marker.");

            if (settings.projectScanMode == PungentTokenProjectScanMode.StrictOptIn ||
                settings.scanOnlyOptInMarkedFiles)
            {
                if (context == null || !context.hasPungentOptIn)
                    return Complete(candidate, 0, settings, "Strict opt-in mode: file has no token marker.");
            }

            if (context != null && context.hasPungentOptIn)
                AddScore(ref score, reasons, 40, "File has @pungent-tokens/@pfu-tokens marker.");

            if (context != null && PathHasAny(context.lowerPath, settings.includePathHints))
                AddScore(ref score, reasons, 25, "Path suggests authored text.");

            if (context != null && context.hasKnownToken)
                AddScore(ref score, reasons, 20, "File also contains a known token.");

            if (NamespaceRegex.IsMatch(key))
                AddScore(ref score, reasons, 15, "Token key uses namespace form.");

            if (NamingStyleRegex.IsMatch(key))
                AddScore(ref score, reasons, 10, "Token key matches common naming style.");

            if (AppearsInProse(candidate.contexts))
                AddScore(ref score, reasons, 10, "Token appears in prose-like text.");

            if (context != null && IsAuthoredTextExtension(context.extension))
                AddScore(ref score, reasons, 10, "Source extension is authored text.");

            if (NumericOrFormatRegex.IsMatch(key))
                AddScore(ref score, reasons, -50, "Token key is numeric or format-like.");

            if (context != null && PathHasAny(context.lowerPath, settings.excludePathHints))
                AddScore(ref score, reasons, -45, "Path suggests generated/cache/package content.");

            if (context != null && IsCodeStyleConfigExtension(context.extension))
                AddScore(ref score, reasons, -40, "Source extension is code/style/config.");

            if (HasCodePunctuation(key))
                AddScore(ref score, reasons, -30, "Token key contains code-like punctuation.");

            if (context != null && context.braceCount >= 16 && context.BraceDensity > 0.015f)
                AddScore(ref score, reasons, -30, "High brace density suggests code/config.");

            if (LooksJsonLike(candidate.contexts))
                AddScore(ref score, reasons, -25, "Nearby context looks JSON-like.");

            if (LooksCodeLike(candidate.contexts))
                AddScore(ref score, reasons, -20, "Nearby context looks code/style-like.");

            if (LooksJsonAdjacent(candidate.contexts))
                AddScore(ref score, reasons, -15, "Token is adjacent to JSON-like punctuation.");

            return Complete(candidate, score, settings, reasons.Count == 0 ? "No intent signals matched." : reasons[0]);
        }

        public static bool IsIgnored(PungentTokenCandidate candidate, PungentTokenScanSettings settings)
        {
            if (candidate == null || settings == null)
                return false;

            settings.EnsureDefaults();
            string key = PungentTokenParser.NormalizeKey(candidate.tokenKey);
            string path = NormalizePath(candidate.sourcePath);
            string hash = candidate.Hash;

            candidate.ignoredByKey = settings.ignoredTokenKeys.Any(k => string.Equals(PungentTokenParser.NormalizeKey(k), key, StringComparison.OrdinalIgnoreCase));
            candidate.ignoredByFile = settings.ignoredSourcePaths.Any(p => string.Equals(NormalizePath(p), path, StringComparison.OrdinalIgnoreCase));
            bool ignoredByHash = settings.ignoredCandidateHashes.Any(h => string.Equals(h, hash, StringComparison.OrdinalIgnoreCase));
            candidate.ignored = candidate.ignoredByKey || candidate.ignoredByFile || ignoredByHash;
            return candidate.ignored;
        }

        private static PungentTokenCandidate Complete(PungentTokenCandidate candidate, int score, PungentTokenScanSettings settings, string fallbackReason)
        {
            candidate.confidence = UnityEngine.Mathf.Clamp(score, 0, 100);

            if (score <= 0)
                candidate.intentLevel = PungentTokenIntentLevel.NotToken;
            else if (candidate.confidence >= settings.likelyThreshold)
                candidate.intentLevel = PungentTokenIntentLevel.Likely;
            else if (candidate.confidence >= settings.possibleThreshold)
                candidate.intentLevel = PungentTokenIntentLevel.Possible;
            else
                candidate.intentLevel = PungentTokenIntentLevel.Unlikely;

            if (candidate.reasons.Count == 0 && !string.IsNullOrWhiteSpace(fallbackReason))
                candidate.reasons.Add(fallbackReason);
            candidate.reason = candidate.reasons.Count == 0 ? string.Empty : candidate.reasons[0];
            return candidate;
        }

        private static void AddScore(ref int score, List<string> reasons, int delta, string reason)
        {
            score += delta;
            if (!string.IsNullOrWhiteSpace(reason))
                reasons.Add((delta > 0 ? "+" : string.Empty) + delta + " " + reason);
        }

        private static bool ContainsKnownToken(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (PungentTokenDefinition token in PungentTokenStorage.Database.tokens)
            {
                if (token == null || string.IsNullOrWhiteSpace(token.key))
                    continue;
                if (text.IndexOf("{" + PungentTokenParser.NormalizeKey(token.key) + "}", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrEmpty(text) || values == null)
                return false;
            return values.Any(v => !string.IsNullOrWhiteSpace(v) && text.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool PathHasAny(string lowerPath, IEnumerable<string> hints)
        {
            if (string.IsNullOrEmpty(lowerPath) || hints == null)
                return false;

            return hints.Any(h =>
                !string.IsNullOrWhiteSpace(h) &&
                lowerPath.IndexOf(h.Replace('\\', '/').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static bool IsAuthoredTextExtension(string extension)
        {
            switch (extension)
            {
                case ".txt":
                case ".md":
                case ".csv":
                case ".tsv":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCodeStyleConfigExtension(string extension)
        {
            switch (extension)
            {
                case ".cs":
                case ".shader":
                case ".hlsl":
                case ".uss":
                case ".uxml":
                case ".asmdef":
                case ".asmref":
                case ".meta":
                case ".json":
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasCodePunctuation(string key)
        {
            return !string.IsNullOrEmpty(key) && key.Any(c => "=;()<>+*/\\[]|&%!?,:".IndexOf(c) >= 0);
        }

        private static bool AppearsInProse(IEnumerable<string> contexts)
        {
            return contexts != null && contexts.Any(c => !string.IsNullOrWhiteSpace(c) && ProseLikeContextRegex.IsMatch(c));
        }

        private static bool LooksJsonLike(IEnumerable<string> contexts)
        {
            return contexts != null && contexts.Any(c => !string.IsNullOrWhiteSpace(c) && JsonLikeContextRegex.IsMatch(c));
        }

        private static bool LooksCodeLike(IEnumerable<string> contexts)
        {
            return contexts != null && contexts.Any(c => !string.IsNullOrWhiteSpace(c) && CodeLikeContextRegex.IsMatch(c));
        }

        private static bool LooksJsonAdjacent(IEnumerable<string> contexts)
        {
            if (contexts == null)
                return false;

            return contexts.Any(c =>
                !string.IsNullOrWhiteSpace(c) &&
                (c.Contains("\":") || c.Contains("\",") || c.Contains(":{") || c.Contains("},")));
        }
    }
#endif
}
