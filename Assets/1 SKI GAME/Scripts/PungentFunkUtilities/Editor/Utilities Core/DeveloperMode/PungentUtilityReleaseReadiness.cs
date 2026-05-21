using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// Shared release-readiness guidance used by the launcher and design audit window.
    /// This keeps next-pass recommendations consistent without running expensive scans during Control Panel repaint.
    /// </summary>
    public static class PungentUtilityReleaseReadiness
    {
        public enum Priority
        {
            Now,
            Next,
            Later
        }

        public sealed class UpdatePass
        {
            public readonly string title;
            public readonly Priority priority;
            public readonly string summary;
            public readonly string[] fileTargets;
            public readonly string[] gates;

            public UpdatePass(string title, Priority priority, string summary, string[] fileTargets, string[] gates)
            {
                this.title = string.IsNullOrWhiteSpace(title) ? "Update Pass" : title;
                this.priority = priority;
                this.summary = summary ?? string.Empty;
                this.fileTargets = fileTargets ?? Array.Empty<string>();
                this.gates = gates ?? Array.Empty<string>();
            }
        }

        public sealed class Assessment
        {
            public string headline;
            public string details;
            public Color tint;
            public readonly List<UpdatePass> recommendedPasses = new List<UpdatePass>();
        }

        private static readonly UpdatePass[] DefaultPasses =
        {
            new UpdatePass(
                "Split Debug Control Center",
                Priority.Now,
                "Extract discovery, reflected fields, scheduler, router panel, and component inspection into focused files while preserving the current UI.",
                new[]
                {
                    "Editor/Debug Control Window/DebugControlWindow.cs",
                    "Editor/Debug Control Window/DebugRouter.cs",
                    "Editor/Debug Control Window/DebugChannels.cs"
                },
                new[]
                {
                    "No UI redesign during the split.",
                    "No scene rescans during idle repaint.",
                    "Router panel remains opt-in/throttled."
                }),

            new UpdatePass(
                "Create Shared Scan/Cache Primitives",
                Priority.Next,
                "Introduce reusable scan result, severity, scope, session, and cache models before refactoring scanner-heavy windows.",
                new[]
                {
                    "Editor/Utilities Core/PungentEditorPerformanceUtility.cs",
                    "Editor/ReferenceAssignmentScannerWindow.cs",
                    "Editor/TerrainUsageScannerWindow.cs",
                    "Editor/Audio Coverage/AudioCoverageContextWindow.cs",
                    "Editor/Audio Coverage/AudioCoverageWindow.cs"
                },
                new[]
                {
                    "Scans run on explicit user action or throttled sessions only.",
                    "Results expose consistent counts, severities, and copy/export summaries.",
                    "Existing scanner behaviour remains intact while migration is staged."
                }),

            new UpdatePass(
                "Deepen Asset Placement Lab Core",
                Priority.Next,
                "Add the first high-value placement-depth module after the core/editor infrastructure is stable: Surface Brush, group repair, or path scatter integration.",
                new[]
                {
                    "Editor/Asset Placement Lab/AssetPlacementLabWindow.cs",
                    "Asset Placement Lab/PungentPlacementRuleSetSO.cs",
                    "Asset Placement Lab/PungentPlacementScatterUtility.cs",
                    "Editor/Asset Placement Lab/PungentPlacementApplyUtility.cs"
                },
                new[]
                {
                    "Preview-first workflow remains responsive.",
                    "Scene drawing is capped/throttled.",
                    "Generated groups can be validated and repaired."
                }),

            new UpdatePass(
                "Split Palette Designer Panels",
                Priority.Later,
                "Separate palette session state, swatch grid, generation, analysis, application, and selected-swatch inspector before adding more colour features.",
                new[]
                {
                    "Editor/Palette Designer/PaletteDesignerWindow.cs",
                    "Palette Designer/PaletteGeneratorUtility.cs",
                    "Palette Designer/PaletteAnalysisUtility.cs",
                    "Editor/Palette Designer/PaletteApplyUtility.cs"
                },
                new[]
                {
                    "Palette board stays the primary workflow.",
                    "Advanced generation remains collapsed by default.",
                    "No regression to locked-swatch regeneration behaviour."
                })
        };

        public static IReadOnlyList<UpdatePass> GetDefaultPasses()
        {
            return DefaultPasses;
        }

        public static Assessment Assess(PungentUtilityDesignAudit.Report report)
        {
            Assessment assessment = new Assessment();

            if (report == null)
            {
                assessment.headline = "Run the design audit before selecting the next implementation pass.";
                assessment.details = "The package has known next-pass priorities, but the audit gives the current blocking/warning state for this checkout.";
                assessment.tint = UtilityWindowTheme.Cyan;
                assessment.recommendedPasses.AddRange(DefaultPasses.Take(3));
                return assessment;
            }

            if (report.ErrorCount > 0)
            {
                assessment.headline = "Resolve blocking release-readiness issues first.";
                assessment.details = report.ErrorCount + " blocking issue(s) remain. Prioritise namespace, asmdef, registry, CreateAssetMenu, and monolithic-window blockers before feature work.";
                assessment.tint = UtilityWindowTheme.Red;
                assessment.recommendedPasses.Add(new UpdatePass(
                    "Resolve Design Audit Blockers",
                    Priority.Now,
                    "Fix every error shown in the audit window before adding new utility modules.",
                    new[]
                    {
                        "Editor/Debug Control Window/DebugControlWindow.cs",
                        "Editor/Palette Designer/PaletteDesignerWindow.cs",
                        "Editor/Audio Coverage/AudioCoverageWindow.cs",
                        "Editor/Utilities Core/PungentUtilityRegistry.cs"
                    },
                    new[] { "Zero audit errors.", "Registry opens all descriptors.", "Runtime/editor boundary remains clean.", "Large-window blockers have an extraction plan or first split." }));
                assessment.recommendedPasses.AddRange(DefaultPasses.Take(2));
                return assessment;
            }

            if (report.largeEditorWindowCount > 0)
            {
                assessment.headline = "Core hardening is ready; split the largest window next.";
                assessment.details = report.largeEditorWindowCount + " large EditorWindow(s) still need panel/service/state extraction. Start with Debug Control Center unless current work points to another blocker.";
                assessment.tint = UtilityWindowTheme.Amber;
                assessment.recommendedPasses.AddRange(DefaultPasses.Take(3));
                return assessment;
            }

            if (report.WarningCount > 0)
            {
                assessment.headline = "No blocking errors; reduce warnings before expanding labs.";
                assessment.details = report.WarningCount + " warning(s) remain. Prefer scanner/cache and layout-readiness work over brand-new labs.";
                assessment.tint = UtilityWindowTheme.Amber;
                assessment.recommendedPasses.AddRange(DefaultPasses.Skip(1).Take(3));
                return assessment;
            }

            assessment.headline = "Release-readiness checks are clean for this audit scope.";
            assessment.details = "Proceed with planned feature-depth work while keeping preview/apply, cached scans, and optional integration boundaries intact.";
            assessment.tint = UtilityWindowTheme.Green;
            assessment.recommendedPasses.AddRange(DefaultPasses.Skip(2));
            return assessment;
        }

        public static string BuildNextPassBrief(PungentUtilityDesignAudit.Report report, int maxPasses = 3)
        {
            Assessment assessment = Assess(report);
            var sb = new StringBuilder();
            sb.AppendLine("PungentFunk Utilities - Recommended Next Update Passes");
            sb.AppendLine();
            sb.AppendLine(assessment.headline);
            sb.AppendLine(assessment.details);
            sb.AppendLine();

            int count = Mathf.Clamp(maxPasses, 1, 8);
            for (int i = 0; i < assessment.recommendedPasses.Count && i < count; i++)
            {
                UpdatePass pass = assessment.recommendedPasses[i];
                sb.AppendLine((i + 1) + ". " + pass.title + " [" + pass.priority + "]");
                sb.AppendLine("   " + pass.summary);

                if (pass.fileTargets.Length > 0)
                {
                    sb.AppendLine("   Primary files:");
                    for (int j = 0; j < pass.fileTargets.Length; j++)
                        sb.AppendLine("   - " + pass.fileTargets[j]);
                }

                if (pass.gates.Length > 0)
                {
                    sb.AppendLine("   Validation gates:");
                    for (int j = 0; j < pass.gates.Length; j++)
                        sb.AppendLine("   - " + pass.gates[j]);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static string GetPriorityLabel(Priority priority)
        {
            switch (priority)
            {
                case Priority.Now: return "Now";
                case Priority.Next: return "Next";
                default: return "Later";
            }
        }

        public static Color GetPriorityTint(Priority priority)
        {
            switch (priority)
            {
                case Priority.Now: return UtilityWindowTheme.Amber;
                case Priority.Next: return UtilityWindowTheme.Cyan;
                default: return UtilityWindowTheme.Neutral;
            }
        }
    }
    #endif
}
