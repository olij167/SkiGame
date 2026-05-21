using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Small shared IMGUI helpers for drawing explicit scan summaries and issue lists.
    /// </summary>
    public static class PungentScanGUI
    {
        public static void DrawSummary(PungentScanResult result)
        {
            if (result == null)
            {
                EditorGUILayout.HelpBox("No scan has been run yet. Results are generated only when you press a scan/refresh action.", MessageType.Info);
                return;
            }

            Color tint = GetDominantTint(result);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.16f, 0.08f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Scan Summary", tint, result.IsComplete ? "Cached" : "Running");
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(result.ScopeLabel, tint);
                    UtilityWindowTheme.CountPill(result.Summary.DurationLabel, UtilityWindowTheme.Neutral);
                }

                EditorGUILayout.LabelField(result.StatusMessage ?? string.Empty, UtilityWindowTheme.CardLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Scanned: " + result.TotalScanned, UtilityWindowTheme.Blue);
                    UtilityWindowTheme.CountPill("Matched: " + result.TotalMatched, UtilityWindowTheme.Green);
                    UtilityWindowTheme.CountPill("Skipped: " + result.TotalSkipped, UtilityWindowTheme.Amber);
                    if (result.TotalChanged > 0)
                        UtilityWindowTheme.CountPill("Changed: " + result.TotalChanged, UtilityWindowTheme.Purple);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Errors: " + result.Summary.ErrorCount, result.Summary.ErrorCount > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Neutral);
                    UtilityWindowTheme.CountPill("Warnings: " + result.Summary.WarningCount, result.Summary.WarningCount > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral);
                    UtilityWindowTheme.CountPill("Info: " + result.Summary.InfoCount, result.Summary.InfoCount > 0 ? UtilityWindowTheme.Teal : UtilityWindowTheme.Neutral);
                }
            }
        }

        public static void DrawResultHeader(PungentScanResult result, string sourceBanner = null)
        {
            if (!string.IsNullOrWhiteSpace(sourceBanner))
                DrawResultSourceBanner(sourceBanner);
            DrawFreshnessStrip(result != null ? result.ToolId : null, result);
            DrawSummary(result);
        }

        public static void DrawFreshnessStrip(string toolId, PungentScanResult result = null)
        {
            string freshness = PungentScanSnapshotStore.GetFreshnessLabel(toolId);
            string age = result != null && result.IsComplete
                ? PungentScanFindingActions.FormatAge(result.CompletedAtUtc)
                : PungentScanSnapshotStore.GetLastScanAgeLabel(toolId);
            Color tint = freshness == "Fresh" ? UtilityWindowTheme.Green : freshness == "Stale" ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral;
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(freshness, tint, 74f);
                UtilityWindowTheme.CountPill("scanned " + age, UtilityWindowTheme.Blue, 126f);
                if (result != null)
                    UtilityWindowTheme.CountPill(result.ScopeLabel, UtilityWindowTheme.Teal, 120f);
                GUILayout.FlexibleSpace();
            }
        }

        public static void DrawFindingDigest(PungentScanFindingDigest digest)
        {
            if (digest == null)
                return;
            Color tint = GetTint(digest.dominantSeverity);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(digest.dominantSeverity.ToString(), tint, 82f);
                    string countLabel = digest.issueCount <= 0 ? "No findings" : digest.issueCount == 1 ? "1 finding" : digest.issueCount + " findings";
                    EditorGUILayout.LabelField(countLabel, UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                }
                DrawWrappedLabel(digest.headline, UtilityWindowTheme.CardLabelStyle);
                if (!string.IsNullOrWhiteSpace(digest.impact))
                    DrawWrappedLabel(digest.impact, UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(digest.recommendedAction))
                    DrawWrappedLabel("Next: " + digest.recommendedAction, UtilityWindowTheme.PathLabelStyle);
            }
        }

        public static void DrawIssueList(PungentScanResult result, ref Vector2 scroll, float height, string emptyMessage = null)
        {
            if (result == null || result.Issues == null || result.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(emptyMessage) ? "No scan issues to display." : emptyMessage, MessageType.None);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, false, true, GUILayout.Height(Mathf.Max(60f, height)));
            for (int i = 0; i < result.Issues.Count; i++)
                DrawIssue(result.Issues[i]);
            EditorGUILayout.EndScrollView();
        }

        public static void DrawIssueTriageList(PungentScanResult result, ref Vector2 scroll, float height, Action<PungentScanIssue> onSelect = null)
        {
            if (result == null || result.Issues == null || result.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No scan findings to triage.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, false, true, GUILayout.Height(Mathf.Max(80f, height)));
            for (int i = 0; i < result.Issues.Count; i++)
                DrawIssueRowCompact(result.Issues[i], result.ToolId, result.DisplayName, onSelect);
            EditorGUILayout.EndScrollView();
        }

        public static void DrawIssueRowCompact(PungentScanIssue issue, string toolId, string toolName, Action<PungentScanIssue> onSelect = null)
        {
            if (issue == null)
                return;
            Color tint = GetTint(issue.Severity);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.04f, 5, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(issue.Severity.ToString(), tint, 70f);
                    UtilityWindowTheme.CountPill(PungentScanFindingActions.GetIssueTypeBadge(issue.Code, issue.Title, toolId), UtilityWindowTheme.Teal, 76f);
                    UtilityWindowTheme.CountPill(string.IsNullOrWhiteSpace(toolName) ? "Scan" : toolName, UtilityWindowTheme.Neutral, 128f);
                    GUILayout.FlexibleSpace();
                    if (onSelect != null && GUILayout.Button("Details", EditorStyles.miniButton, GUILayout.Width(58f)))
                        onSelect(issue);
                }
                EditorGUILayout.LabelField(issue.Title, UtilityWindowTheme.CardLabelStyle);
                if (!string.IsNullOrWhiteSpace(issue.Message))
                    EditorGUILayout.LabelField(issue.Message, UtilityWindowTheme.MutedMiniLabelStyle);
                string action = PungentScanFindingActions.GetRecommendedAction(toolId, issue.Code, issue.Severity);
                if (!string.IsNullOrWhiteSpace(action))
                    EditorGUILayout.LabelField("Next: " + action, UtilityWindowTheme.PathLabelStyle);
                if (!string.IsNullOrWhiteSpace(issue.Path))
                    EditorGUILayout.LabelField(issue.Path, UtilityWindowTheme.PathLabelStyle);
            }
        }

        public static void DrawIssue(PungentScanIssue issue)
        {
            if (issue == null)
                return;

            Color tint = GetTint(issue.Severity);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.12f, 0.06f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(issue.Severity.ToString(), tint);
                    EditorGUILayout.LabelField(issue.Title, UtilityWindowTheme.CardLabelStyle);
                    GUILayout.FlexibleSpace();
                    if (issue.Context != null && GUILayout.Button("Ping", GUILayout.Width(48f)))
                        EditorGUIUtility.PingObject(issue.Context);
                }

                if (!string.IsNullOrWhiteSpace(issue.Message))
                    EditorGUILayout.LabelField(issue.Message, UtilityWindowTheme.MutedMiniLabelStyle);

                if (!string.IsNullOrWhiteSpace(issue.Path))
                    EditorGUILayout.LabelField(issue.Path, UtilityWindowTheme.PathLabelStyle);
            }
        }

        public static void DrawIssueDetail(PungentScanIssue issue, string toolId, string toolName)
        {
            if (issue == null)
            {
                EditorGUILayout.HelpBox("Select a scan finding to inspect details.", MessageType.Info);
                return;
            }

            DrawResultSourceBanner(string.IsNullOrWhiteSpace(toolName) ? toolId : toolName);
            UtilityWindowTheme.CountPill(issue.Severity.ToString(), GetTint(issue.Severity), 82f);
            EditorGUILayout.LabelField("What happened", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(issue.Title, UtilityWindowTheme.CardLabelStyle);
            EditorGUILayout.LabelField(issue.Message, UtilityWindowTheme.BodyStyle);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Why it matters", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(PungentScanFindingActions.GetImpact(toolId, issue.Code, issue.Severity), UtilityWindowTheme.MutedMiniLabelStyle);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Recommended next step", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(PungentScanFindingActions.GetRecommendedAction(toolId, issue.Code, issue.Severity), UtilityWindowTheme.MutedMiniLabelStyle);
            if (!string.IsNullOrWhiteSpace(issue.Path))
                EditorGUILayout.LabelField(issue.Path, UtilityWindowTheme.PathLabelStyle);
        }

        public static void DrawResultSourceBanner(string banner)
        {
            if (string.IsNullOrWhiteSpace(banner))
                return;
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.08f, 0.04f, 4, 1)))
            {
                UtilityWindowTheme.CountPill("Result", UtilityWindowTheme.Blue, 58f);
                EditorGUILayout.LabelField(banner, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        public static Color GetTint(PungentScanSeverity severity)
        {
            switch (severity)
            {
                case PungentScanSeverity.Success:
                    return UtilityWindowTheme.Green;
                case PungentScanSeverity.Warning:
                    return UtilityWindowTheme.Amber;
                case PungentScanSeverity.Error:
                    return UtilityWindowTheme.Red;
                case PungentScanSeverity.Info:
                    return UtilityWindowTheme.Teal;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static Color GetDominantTint(PungentScanResult result)
        {
            if (result == null)
                return UtilityWindowTheme.Neutral;
            if (result.Summary.ErrorCount > 0)
                return UtilityWindowTheme.Red;
            if (result.Summary.WarningCount > 0)
                return UtilityWindowTheme.Amber;
            if (result.TotalMatched > 0)
                return UtilityWindowTheme.Green;
            return UtilityWindowTheme.Blue;
        }

        private static void DrawWrappedLabel(string text, GUIStyle style)
        {
            GUIStyle wrapped = new GUIStyle(style ?? EditorStyles.wordWrappedLabel)
            {
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            EditorGUILayout.LabelField(text ?? string.Empty, wrapped, GUILayout.ExpandWidth(true));
        }
    }

    public sealed class PungentScanFindingDigest
    {
        public string providerId;
        public string providerDisplayName;
        public PungentScanSeverity dominantSeverity = PungentScanSeverity.Info;
        public int issueCount;
        public int errorCount;
        public int warningCount;
        public int infoCount;
        public int successCount;
        public string headline;
        public string impact;
        public string recommendedAction;
        public string primaryIssueCode;
        public string primaryAssetPath;
        public string actionKind;
        public string freshnessLabel;
        public string lastScannedAgeLabel;
        public string scanScopeLabel;
        public bool isStale;
        public bool isNotConfigured;
        public bool isActionable;
        public bool isConfigurationIssue;
        public bool isInformationalOnly;
    }

    public static class PungentScanFindingDigestBuilder
    {
        public static PungentScanFindingDigest Build(string providerId, string providerDisplayName, PungentScanResult result, PungentProjectAuditIndexEntry indexEntry)
        {
            PungentScanFindingDigest digest = new PungentScanFindingDigest
            {
                providerId = providerId ?? string.Empty,
                providerDisplayName = string.IsNullOrWhiteSpace(providerDisplayName) ? providerId : providerDisplayName,
                freshnessLabel = PungentScanSnapshotStore.GetFreshnessLabel(providerId),
                lastScannedAgeLabel = result != null && result.IsComplete ? PungentScanFindingActions.FormatAge(result.CompletedAtUtc) : PungentScanSnapshotStore.GetLastScanAgeLabel(providerId),
                scanScopeLabel = result != null ? result.ScopeLabel : "No scan",
                isStale = indexEntry != null && indexEntry.Stale
            };

            if (result == null)
            {
                digest.headline = "No scan result yet.";
                digest.impact = "This provider has no cached findings to review.";
                digest.recommendedAction = "Run this provider or refresh cache.";
                digest.freshnessLabel = "No cache";
                return digest;
            }

            digest.errorCount = result.Summary.ErrorCount;
            digest.warningCount = result.Summary.WarningCount;
            digest.infoCount = result.Summary.InfoCount;
            digest.successCount = result.Summary.SuccessCount;
            digest.issueCount = digest.errorCount + digest.warningCount + digest.infoCount;
            PungentScanIssue top = BuildTopIssue(result);
            digest.dominantSeverity = digest.errorCount > 0 ? PungentScanSeverity.Error : digest.warningCount > 0 ? PungentScanSeverity.Warning : PungentScanSeverity.Info;
            if (top != null)
            {
                digest.headline = top.Title;
                digest.impact = PungentScanFindingActions.GetImpact(providerId, top.Code, top.Severity);
                digest.recommendedAction = GetRecommendedAction(providerId, top.Code, top.Severity);
                digest.primaryIssueCode = top.Code;
                digest.primaryAssetPath = top.Path;
                digest.actionKind = PungentScanFindingActions.GetIssueTypeBadge(top.Code, top.Title, providerId);
                digest.isConfigurationIssue = string.Equals(digest.actionKind, "Config", StringComparison.OrdinalIgnoreCase);
                digest.isInformationalOnly = top.Severity == PungentScanSeverity.Info || top.Severity == PungentScanSeverity.Success;
                digest.isActionable = top.Severity == PungentScanSeverity.Warning || top.Severity == PungentScanSeverity.Error;
            }
            else
            {
                digest.dominantSeverity = PungentScanSeverity.Success;
                digest.headline = "No warnings or errors in latest scan.";
                digest.impact = "The latest cached scan did not report actionable findings.";
                digest.recommendedAction = "No action needed. Rerun when relevant project data changes.";
                digest.isInformationalOnly = true;
            }

            return digest;
        }

        public static PungentScanIssue BuildTopIssue(PungentScanResult result)
        {
            if (result == null || result.Issues == null)
                return null;
            PungentScanIssue best = null;
            int bestRank = -1;
            for (int i = 0; i < result.Issues.Count; i++)
            {
                PungentScanIssue issue = result.Issues[i];
                if (issue == null || issue.Severity == PungentScanSeverity.Success || issue.Severity == PungentScanSeverity.None)
                    continue;
                int rank = issue.Severity == PungentScanSeverity.Error ? 4 : issue.Severity == PungentScanSeverity.Warning ? 3 : 2;
                if (rank > bestRank)
                {
                    best = issue;
                    bestRank = rank;
                }
            }
            return best;
        }

        public static string GetRecommendedAction(string toolId, string issueCode, PungentScanSeverity severity)
        {
            return PungentScanFindingActions.GetRecommendedAction(toolId, issueCode, severity);
        }
    }

    public static class PungentScanFindingActions
    {
        public static string GetRecommendedAction(string toolId, string issueCode, PungentScanSeverity severity)
        {
            string code = issueCode ?? string.Empty;
            if (code.StartsWith("SCENE_MISSING_SCRIPT", StringComparison.OrdinalIgnoreCase))
                return "Open the scene, inspect the object, then restore or remove the missing script component through a deliberate scene edit.";
            if (code.StartsWith("SCENE_MISSING_MATERIAL", StringComparison.OrdinalIgnoreCase))
                return "Open Scene Issue Scanner, inspect the renderer, then assign an intended material or leave the slot empty deliberately.";
            if (code.StartsWith("SCENE_MISSING_SHADER", StringComparison.OrdinalIgnoreCase))
                return "Open the material and assign a valid shader or replacement material after review.";
            if (code.StartsWith("SCENE_MISSING_MESH", StringComparison.OrdinalIgnoreCase) || code.StartsWith("SCENE_MISSING_SKINNED_MESH", StringComparison.OrdinalIgnoreCase))
                return "Open the scene object and assign the expected mesh, or confirm the renderer is configured at runtime.";
            if (code.StartsWith("SCENE_MISSING_SPRITE", StringComparison.OrdinalIgnoreCase))
                return "Review the SpriteRenderer and confirm whether the sprite is assigned later at runtime.";
            if (code.StartsWith("SCENE_DUPLICATE_", StringComparison.OrdinalIgnoreCase))
                return "Open the scene and choose the intended active service instance before disabling or removing anything.";
            if (code.StartsWith("SCENE_MANY_", StringComparison.OrdinalIgnoreCase))
                return "Review the advisory threshold in Scene Issue Scanner and tune it if this scene intentionally exceeds the default.";
            if (code.StartsWith("TERRAIN_USAGE_UNUSED", StringComparison.OrdinalIgnoreCase))
                return "Review unused TerrainData before deleting or archiving.";
            if (code.StartsWith("REFSCAN_", StringComparison.OrdinalIgnoreCase))
                return "Open Reference Scanner to inspect suggested assignments.";
            if (code.StartsWith("AUDIO_CONTEXT_", StringComparison.OrdinalIgnoreCase) || string.Equals(toolId, "audio-setup-coverage", StringComparison.OrdinalIgnoreCase))
                return "Open Audio Setup Coverage and review the configured profile.";
            if (code.StartsWith("AUDIO_CATALOG_", StringComparison.OrdinalIgnoreCase) || string.Equals(toolId, "audio-catalog-coverage", StringComparison.OrdinalIgnoreCase))
                return "Open Audio Catalog Coverage and review cue mappings.";
            if (code.StartsWith("COVERAGE_", StringComparison.OrdinalIgnoreCase))
                return "Open Coverage Matrix and inspect affected cells.";
            if (code.StartsWith("TOKEN_", StringComparison.OrdinalIgnoreCase))
                return "Open Token Validator and review token scan filters or token definitions.";
            if (severity == PungentScanSeverity.Error)
                return "Open the owning scanner and resolve the blocking configuration or data issue.";
            if (severity == PungentScanSeverity.Warning)
                return "Open the owning scanner and review the warning in context.";
            return "Review the owning scanner when convenient.";
        }

        public static string GetImpact(string toolId, string issueCode, PungentScanSeverity severity)
        {
            string code = issueCode ?? string.Empty;
            if (code.StartsWith("SCENE_MISSING_SCRIPT", StringComparison.OrdinalIgnoreCase))
                return "Missing scripts leave scene objects with broken component data and can hide runtime behaviour loss.";
            if (code.StartsWith("SCENE_MISSING_MATERIAL", StringComparison.OrdinalIgnoreCase) || code.StartsWith("SCENE_MISSING_SHADER", StringComparison.OrdinalIgnoreCase))
                return "Broken renderer setup can produce invisible, pink, or unintentionally unlit scene content.";
            if (code.StartsWith("SCENE_MISSING_MESH", StringComparison.OrdinalIgnoreCase) || code.StartsWith("SCENE_MISSING_SKINNED_MESH", StringComparison.OrdinalIgnoreCase) || code.StartsWith("SCENE_MISSING_SPRITE", StringComparison.OrdinalIgnoreCase))
                return "Missing visual asset references can make scene objects invisible or incomplete unless they are intentionally assigned at runtime.";
            if (code.StartsWith("SCENE_DUPLICATE_", StringComparison.OrdinalIgnoreCase))
                return "Duplicate active scene services can create ambiguous runtime input, camera, or audio behaviour.";
            if (code.StartsWith("SCENE_MANY_", StringComparison.OrdinalIgnoreCase))
                return "This is an advisory threshold intended to guide review, not proof of a defect.";
            if (code.Contains("UNUSED"))
                return "Unused assets can create cleanup risk and make project ownership harder to understand.";
            if (code.Contains("MISSING"))
                return "Missing configuration or references can hide broken coverage until runtime or release review.";
            if (code.Contains("DUPLICATE"))
                return "Duplicate entries make audit results ambiguous and can mask the intended source of truth.";
            if (code.Contains("INVALID"))
                return "Invalid data can block reliable scanning or downstream tooling.";
            if (code.Contains("SUGGESTION"))
                return "The scanner found a likely setup improvement that still needs human review.";
            if (code.StartsWith("TOKEN_", StringComparison.OrdinalIgnoreCase))
                return "Token drift makes notes, bindings, and generated text harder to trust.";
            if (severity == PungentScanSeverity.Error)
                return "This issue can block reliable audit results until it is resolved.";
            if (severity == PungentScanSeverity.Warning)
                return "This warning may become a production issue if left unreviewed.";
            return "This finding is informational and helps keep the audit trail understandable.";
        }

        public static string GetIssueTypeBadge(string issueCode, string title, string toolId)
        {
            string text = ((issueCode ?? string.Empty) + " " + (title ?? string.Empty)).ToUpperInvariant();
            if (text.Contains("CONFIG") || text.Contains("PROFILE") || text.Contains("NOT_CONFIGURED"))
                return "Config";
            if (text.Contains("MISSING") || text.Contains("NO_"))
                return "Missing";
            if (text.Contains("UNUSED"))
                return "Unused";
            if (text.Contains("INVALID"))
                return "Invalid";
            if (text.Contains("DUPLICATE"))
                return "Duplicate";
            if (text.Contains("SUGGESTION"))
                return "Suggestion";
            if (text.Contains("COVERAGE") || string.Equals(toolId, "coverage-matrix", StringComparison.OrdinalIgnoreCase))
                return "Coverage";
            if (text.Contains("TOKEN") || string.Equals(toolId, "token-validator", StringComparison.OrdinalIgnoreCase))
                return "Token";
            if (text.Contains("ASSET"))
                return "Asset";
            return "Info";
        }

        public static string FormatAge(DateTime completedUtc)
        {
            if (completedUtc == default(DateTime))
                return "never";
            TimeSpan age = DateTime.UtcNow - completedUtc.ToUniversalTime();
            if (age.TotalMinutes < 1d)
                return "just now";
            if (age.TotalHours < 1d)
                return Mathf.Max(1, Mathf.FloorToInt((float)age.TotalMinutes)) + " min ago";
            if (age.TotalDays < 1d)
                return Mathf.Max(1, Mathf.FloorToInt((float)age.TotalHours)) + " hr ago";
            return Mathf.Max(1, Mathf.FloorToInt((float)age.TotalDays)) + " day(s) ago";
        }
    }
#endif
}
