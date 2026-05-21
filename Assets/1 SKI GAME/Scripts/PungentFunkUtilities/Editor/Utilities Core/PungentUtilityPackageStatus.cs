using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using UnityEngine;

    /// <summary>
    /// Shared package-status labels used by the PungentFunk utility registry, launcher, and docs.
    /// Keep these string values stable because they may be serialized in user preferences or docs.
    /// </summary>
    public static class PungentUtilityPackageStatus
    {
        public const string Stable = "Stable";
        public const string Refinement = "Refinement";
        public const string Experimental = "Experimental";
        public const string InProgress = "In Progress";
        public const string Placeholder = "Placeholder";
        public const string Deprecated = "Deprecated";
        public const string ProjectAdapter = "Project Adapter";

        public static readonly string[] All =
        {
            Stable,
            Refinement,
            Experimental,
            InProgress,
            Placeholder,
            Deprecated,
            ProjectAdapter
        };

        public static string Normalize(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return Experimental;

            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], status.Trim(), StringComparison.OrdinalIgnoreCase))
                    return All[i];
            }

            return status.Trim();
        }

        public static string GetDescription(string status)
        {
            switch (Normalize(status))
            {
                case Stable:
                    return "Ready for regular use. Changes should preserve existing workflows and serialized data.";
                case Refinement:
                    return "Mostly finished, just needs final UI and feature polish";
                case Experimental:
                    return "Useful, but still expected to evolve. Review settings before using on important assets.";
                case InProgress:
                    return "Actively being built or consolidated. Expect missing modules or temporary UI gaps.";
                case Placeholder:
                    return "Scaffolded, but still needs to be developed. Don't expect it to do anything.";
                case Deprecated:
                    return "Kept only for compatibility. Prefer the newer generic utility or lab route.";
                case ProjectAdapter:
                    return "Thin project-specific bridge. It should not contain core generic package logic.";
                default:
                    return "Custom status label. Confirm the utility's package expectations before distribution.";
            }
        }

        public static Color GetTint(string status)
        {
            switch (Normalize(status))
            {
                case Stable:
                    return UtilityWindowTheme.Green;
                case Refinement:
                    return UtilityWindowTheme.Blue;
                case Experimental:
                    return UtilityWindowTheme.Purple;
                case InProgress:
                    return UtilityWindowTheme.Amber;
                case Placeholder:
                    return UtilityWindowTheme.Neutral;
                case Deprecated:
                    return UtilityWindowTheme.Red;
                case ProjectAdapter:
                    return UtilityWindowTheme.Cyan;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        public static int SortKey(string status)
        {
            switch (Normalize(status))
            {
                case Stable: return 0;
                case Refinement: return 5;
                case Experimental: return 10;
                case InProgress: return 20;
                case Placeholder: return 25;
                case ProjectAdapter: return 30;
                case Deprecated: return 90;
                default: return 100;
            }
        }
    }
    #endif

}