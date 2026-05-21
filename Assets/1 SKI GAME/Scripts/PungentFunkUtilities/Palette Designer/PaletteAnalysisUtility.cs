using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    public static class PaletteAnalysisUtility
    {
        public static PaletteAnalysisReport Analyse(PungentColourPaletteSO palette)
        {
            return Analyse(palette != null ? palette.swatches : null);
        }

        public static PaletteAnalysisReport Analyse(IList<PaletteSwatch> swatches)
        {
            var report = new PaletteAnalysisReport();
            if (swatches == null || swatches.Count == 0)
            {
                report.problems.Add("Palette is empty.");
                report.recommendations.Add("Add at least a Background, Text, and Accent swatch.");
                return report;
            }

            PaletteSwatch background = FindRole(swatches, PaletteSwatchRole.Background);
            PaletteSwatch panel = FindRole(swatches, PaletteSwatchRole.Panel);
            PaletteSwatch text = FindRole(swatches, PaletteSwatchRole.Text);
            PaletteSwatch mutedText = FindRole(swatches, PaletteSwatchRole.MutedText);

            if (background == null)
                report.problems.Add("Missing Background role.");
            if (text == null)
                report.problems.Add("Missing Text role.");
            if (panel == null)
                report.recommendations.Add("Add a Panel role to check UI surfaces separately from the background.");
            if (mutedText == null)
                report.recommendations.Add("Add a Muted Text role for secondary labels and helper text.");

            AddRolePair(report, text, background, 4.5f, "Text on Background");
            AddRolePair(report, text, panel, 4.5f, "Text on Panel");
            AddRolePair(report, mutedText, background, 3f, "Muted Text on Background");
            AddRolePair(report, mutedText, panel, 3f, "Muted Text on Panel");

            PaletteSwatch warning = FindRole(swatches, PaletteSwatchRole.Warning);
            PaletteSwatch success = FindRole(swatches, PaletteSwatchRole.Success);
            PaletteSwatch error = FindRole(swatches, PaletteSwatchRole.Error);
            AddRolePair(report, text, warning, 4.5f, "Text on Warning");
            AddRolePair(report, text, success, 4.5f, "Text on Success");
            AddRolePair(report, text, error, 4.5f, "Text on Error");

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch a = swatches[i];
                if (a == null)
                    continue;

                for (int j = i + 1; j < swatches.Count; j++)
                {
                    PaletteSwatch b = swatches[j];
                    if (b == null)
                        continue;

                    float contrast = ColourContrastUtility.GetContrastRatio(a.color, b.color);
                    report.pairs.Add(new PaletteContrastPair(a.name, b.name, a.role, b.role, contrast));

                    if (ColourContrastUtility.AreColorsSimilar(a.color, b.color, 0.045f))
                        report.problems.Add($"Near duplicate colours: {SafeName(a)} and {SafeName(b)}.");
                }
            }

            int accentCount = CountRoles(swatches, PaletteSwatchRole.Accent, PaletteSwatchRole.AccentSecondary, PaletteSwatchRole.Highlight);
            if (accentCount > Mathf.Max(3, swatches.Count / 2))
                report.recommendations.Add("Palette has many accent/highlight swatches. Consider adding more neutral support colours.");

            if (report.problems.Count == 0)
                report.recommendations.Add("No major palette problems detected.");

            report.pairs.Sort((a, b) => a.contrast.CompareTo(b.contrast));
            return report;
        }

        public static PaletteSwatch FindRole(IList<PaletteSwatch> swatches, PaletteSwatchRole role)
        {
            if (swatches == null)
                return null;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch != null && swatch.role == role)
                    return swatch;
            }

            return null;
        }

        private static void AddRolePair(PaletteAnalysisReport report, PaletteSwatch foreground, PaletteSwatch background, float target, string label)
        {
            if (foreground == null || background == null)
                return;

            float contrast = ColourContrastUtility.GetContrastRatio(foreground.color, background.color);
            report.rolePairs.Add(new PaletteContrastPair(label, foreground.name, foreground.role, background.role, contrast));
            if (contrast < target)
            {
                report.problems.Add($"{label} contrast is {contrast:0.00}:1, below target {target:0.0}:1.");
                if (foreground.locked || background.locked)
                    report.recommendations.Add($"{label} cannot be auto-repaired safely while a required swatch is locked.");
                else
                    report.recommendations.Add($"Improve {foreground.name} or {background.name} to increase {label} readability.");
            }
        }

        private static int CountRoles(IList<PaletteSwatch> swatches, params PaletteSwatchRole[] roles)
        {
            int count = 0;
            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                for (int r = 0; r < roles.Length; r++)
                {
                    if (swatch.role == roles[r])
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        private static string SafeName(PaletteSwatch swatch)
        {
            return swatch == null || string.IsNullOrWhiteSpace(swatch.name) ? "Unnamed" : swatch.name;
        }
    }

    [Serializable]
    public class PaletteAnalysisReport
    {
        public List<string> problems = new List<string>();
        public List<string> recommendations = new List<string>();
        public List<PaletteContrastPair> rolePairs = new List<PaletteContrastPair>();
        public List<PaletteContrastPair> pairs = new List<PaletteContrastPair>();
    }

    [Serializable]
    public class PaletteContrastPair
    {
        public string label;
        public string otherLabel;
        public PaletteSwatchRole foregroundRole;
        public PaletteSwatchRole backgroundRole;
        public float contrast;

        public PaletteContrastPair(string label, string otherLabel, PaletteSwatchRole foregroundRole, PaletteSwatchRole backgroundRole, float contrast)
        {
            this.label = label;
            this.otherLabel = otherLabel;
            this.foregroundRole = foregroundRole;
            this.backgroundRole = backgroundRole;
            this.contrast = contrast;
        }

        public string Rating => ColourContrastUtility.GetReadableRating(contrast);
        public string WCAG => ColourContrastUtility.GetWCAGRating(contrast);
    }

}