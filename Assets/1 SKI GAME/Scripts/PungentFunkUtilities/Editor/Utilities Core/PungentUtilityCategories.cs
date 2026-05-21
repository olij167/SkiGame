using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Broad product-facing categories used by the Utilities Browser.
    /// Stable IDs are serialized in preferences; display names are resolved at the UI edge.
    /// </summary>
    public static class PungentUtilityCategories
    {
        public const string Core = "core-workflow";
        public const string AppearanceStyling = "appearance-styling";
        public const string SceneTools = "scene-authoring";
        public const string Assets = "asset-production";
        public const string Creation = "generation-design";
        public const string Audio = "audio-authoring";
        public const string Audit = "audit-validation";
        public const string BatchRepair = "batch-repair";
        public const string Debug = "debug-diagnostics";
        public const string DocumentationPlanning = "documentation-planning";
        public const string UI = "ui-input-feedback";
        public const string ScanningCoverage = "scanning-coverage";
        public const string Other = "other";

        public static readonly string[] CurrentCategories =
        {
            Core,
            AppearanceStyling,
            SceneTools,
            Assets,
            Creation,
            Audio,
            Audit,
            BatchRepair,
            Debug,
            DocumentationPlanning,
            UI,
            ScanningCoverage
        };

        private sealed class CategoryDefinition
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly int SortOrder;
            public readonly Color Tint;

            public CategoryDefinition(string id, string displayName, string description, int sortOrder, Color tint)
            {
                Id = id;
                DisplayName = displayName;
                Description = description;
                SortOrder = sortOrder;
                Tint = tint;
            }
        }

        private static readonly CategoryDefinition[] Definitions =
        {
            new CategoryDefinition(Core, "Core & Workflow", "Browser, package workflow, design audit, minimizer, shared UX, settings.", 0, UtilityWindowTheme.Blue),
            new CategoryDefinition(AppearanceStyling, "Appearance & Styling", "Themes, palettes, fonts, visual styling, style presets.", 10, UtilityWindowTheme.Purple),
            new CategoryDefinition(SceneTools, "Scene Authoring", "Scene navigation, gizmos, paths, sockets, alignment, placement helpers.", 20, UtilityWindowTheme.Green),
            new CategoryDefinition(Assets, "Asset Production", "Prefab export, icon generation, font review, texture arrays, asset packaging.", 30, UtilityWindowTheme.Teal),
            new CategoryDefinition(Creation, "Generation & Design", "Name, palette, texture, path, and placement generation workflows.", 40, UtilityWindowTheme.Purple),
            new CategoryDefinition(Audio, "Audio Authoring", "Audio setup, cue/catalog coverage, surface/contact audio authoring.", 50, UtilityWindowTheme.Cyan),
            new CategoryDefinition(Audit, "Audit & Validation", "Coverage checks, scanners, validators, missing references, design compliance.", 60, UtilityWindowTheme.Amber),
            new CategoryDefinition(BatchRepair, "Batch & Repair", "Bulk rename, reference repair, component copy, group repair, batch actions.", 70, UtilityWindowTheme.Amber),
            new CategoryDefinition(Debug, "Debug & Diagnostics", "Debug controls, signal routing, diagnostics, visibility toggles.", 80, UtilityWindowTheme.Amber),
            new CategoryDefinition(DocumentationPlanning, "Documentation & Planning", "Linked docs, notes, roadmap, inventory, audit follow-up.", 90, UtilityWindowTheme.Cyan),
            new CategoryDefinition(UI, "UI & Input Feedback", "Input prompts, UI feedback utilities, runtime/editor presentation helpers.", 100, UtilityWindowTheme.Purple),
            new CategoryDefinition(ScanningCoverage, "Scanning & Coverage", "Project scans, coverage maps, result matrices, catalogue coverage.", 110, UtilityWindowTheme.Teal),
        };

        public static int SortKey(string category)
        {
            string normalized = Normalize(category);
            CategoryDefinition definition = Definitions.FirstOrDefault(d => string.Equals(d.Id, normalized, StringComparison.OrdinalIgnoreCase));
            return definition != null ? definition.SortOrder : (string.Equals(normalized, Other, StringComparison.OrdinalIgnoreCase) ? 1000 : 900);
        }

        public static bool IsFactoryCategory(string category)
        {
            string normalized = Normalize(category);
            return Definitions.Any(d => string.Equals(d.Id, normalized, StringComparison.OrdinalIgnoreCase)) ||
                   string.Equals(normalized, Other, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return Other;

            string trimmed = category.Trim();
            for (int i = 0; i < CurrentCategories.Length; i++)
            {
                if (string.Equals(CurrentCategories[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return CurrentCategories[i];
            }

            string key = trimmed.ToLowerInvariant().Replace("_", "-");
            switch (key)
            {
                case "core":
                case "utility core":
                case "core lab":
                    return Core;
                case "appearance":
                case "styling":
                case "appearance & styling":
                case "appearance and styling":
                    return AppearanceStyling;
                case "creation":
                case "asset placement lab":
                case "colour lab":
                case "color lab":
                case "texture lab":
                case "content generation lab":
                    return Creation;
                case "scene tools":
                case "scenetools":
                case "scene authoring":
                case "scene workflow lab":
                    return SceneTools;
                case "assets":
                case "asset preview & export lab":
                case "asset preview and export lab":
                    return Assets;
                case "audio":
                case "audio lab":
                    return Audio;
                case "ui":
                case "ui & feedback lab":
                case "ui and feedback lab":
                case "ui & input feedback":
                case "ui and input feedback":
                    return UI;
                case "debug":
                case "debug diagnostics":
                case "debug & diagnostics":
                case "debug and diagnostics":
                case "debug lab":
                    return Debug;
                case "audit":
                case "audit validation":
                case "audit & validation":
                case "audit and validation":
                case "project audit & authoring lab":
                case "project audit and authoring lab":
                    return Audit;
                case "batch repair":
                case "batch & repair":
                case "batch and repair":
                    return BatchRepair;
                case "documentation planning":
                case "documentation & planning":
                case "documentation and planning":
                    return DocumentationPlanning;
                case "scanning coverage":
                case "scanning & coverage":
                case "scanning and coverage":
                    return ScanningCoverage;
                case "other":
                    return Other;
            }

            return trimmed;
        }

        public static string GetFactoryDisplayName(string category)
        {
            string normalized = Normalize(category);
            CategoryDefinition definition = Definitions.FirstOrDefault(d => string.Equals(d.Id, normalized, StringComparison.OrdinalIgnoreCase));
            return definition != null ? definition.DisplayName : normalized;
        }

        public static string GetDisplayName(string category)
        {
            string normalized = Normalize(category);
            return PungentUtilityCategoryOverrides.instance.GetDisplayName(normalized, GetFactoryDisplayName(normalized));
        }

        public static string GetDescription(string category)
        {
            string normalized = Normalize(category);
            CategoryDefinition definition = Definitions.FirstOrDefault(d => string.Equals(d.Id, normalized, StringComparison.OrdinalIgnoreCase));
            return definition != null ? definition.Description : "Unclassified utility area.";
        }

        public static Color GetFactoryTint(string category)
        {
            string normalized = Normalize(category);
            CategoryDefinition definition = Definitions.FirstOrDefault(d => string.Equals(d.Id, normalized, StringComparison.OrdinalIgnoreCase));
            return definition != null ? definition.Tint : UtilityWindowTheme.Neutral;
        }

        public static Color GetTint(string category)
        {
            string normalized = Normalize(category);
            return PungentUtilityCategoryOverrides.instance.GetTint(normalized, GetFactoryTint(normalized));
        }

        public static bool HasAppearanceOverride(string category)
        {
            return PungentUtilityCategoryOverrides.instance.HasOverride(category);
        }

        public static bool Matches(string category, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string normalized = Normalize(category);
            string q = query.Trim();
            return normalized.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetDisplayName(normalized).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetDescription(normalized).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string[] NormalizeMany(IEnumerable<string> categories)
        {
            if (categories == null)
                return Array.Empty<string>();

            return categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(Normalize)
                .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, Other, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(SortKey)
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
#endif
}
