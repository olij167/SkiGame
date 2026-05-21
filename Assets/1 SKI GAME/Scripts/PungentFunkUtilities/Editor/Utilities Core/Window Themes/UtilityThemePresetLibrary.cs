namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Stable API and compatibility layer for built-in Appearance presets.
    /// Generated curated definitions live in UtilityThemePresetLibrary.Generated.cs.
    /// </summary>
    public static partial class UtilityThemePresetLibrary
    {
        private static readonly HashSet<UtilityWindowTheme.ThemePreset> _generatedPresetIds = new HashSet<UtilityWindowTheme.ThemePreset>();
        private static readonly List<UtilityThemePresetDefinition> _presets = BuildPresets();
        private static readonly Dictionary<UtilityWindowTheme.ThemePreset, UtilityThemePresetDefinition> _byPreset = _presets.ToDictionary(p => p.Preset, p => p);

        public static IReadOnlyList<UtilityThemePresetDefinition> Presets => _presets;
        public static IEnumerable<UtilityThemePresetDefinition> VisiblePresets => _presets.Where(p => !p.HiddenFromGallery);
        public static IEnumerable<UtilityThemePresetDefinition> GeneratedPresets => _presets.Where(p => _generatedPresetIds.Contains(p.Preset));

        public static UtilityThemePresetDefinition Get(UtilityWindowTheme.ThemePreset preset)
        {
            if (_byPreset.TryGetValue(preset, out UtilityThemePresetDefinition definition))
                return definition;

            return _byPreset[UtilityWindowTheme.ThemePreset.PungentDefault];
        }

        public static bool TryGet(UtilityWindowTheme.ThemePreset preset, out UtilityThemePresetDefinition definition)
        {
            return _byPreset.TryGetValue(preset, out definition);
        }

        public static Color GetPresetColor(UtilityWindowTheme.ThemePreset preset, string role, Color fallback)
        {
            return Get(preset).GetColor(role, fallback);
        }

        public static bool IsGeneratedPreset(UtilityWindowTheme.ThemePreset preset)
        {
            return _generatedPresetIds.Contains(preset);
        }

        public static string GetPresetSourceLabel(UtilityWindowTheme.ThemePreset preset)
        {
            if (_generatedPresetIds.Contains(preset))
                return "Generated";

            return _byPreset.ContainsKey(preset) ? "Fallback" : "Unknown";
        }

        private static List<UtilityThemePresetDefinition> BuildPresets()
        {
            var list = new List<UtilityThemePresetDefinition>();
            BuildGeneratedPresets(list);

            _generatedPresetIds.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    _generatedPresetIds.Add(list[i].Preset);
            }

            AddLegacyAliases(list);
            AddArchivedDefinitionsForMissingPresets(list);
            return DeduplicatePresets(list);
        }

        static partial void BuildGeneratedPresets(List<UtilityThemePresetDefinition> list);

        private static List<UtilityThemePresetDefinition> DeduplicatePresets(List<UtilityThemePresetDefinition> list)
        {
            var deduped = new List<UtilityThemePresetDefinition>();
            var used = new HashSet<UtilityWindowTheme.ThemePreset>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null || !used.Add(list[i].Preset))
                    continue;

                deduped.Add(list[i]);
            }

            return deduped;
        }

        private static UtilityThemePresetDefinition Dark(UtilityWindowTheme.ThemePreset preset, UtilityThemePresetCategory category, UtilityThemePresetColourFamily family, string displayName, string description, string[] tags, string primary, string secondary, string tertiary, string header, float darkAlpha, float lightAlpha, float borderOpacity, int borderWidth, int radius, float density, string success = null, string warning = null, string danger = null, string accentAlt = null, string neutral = null, bool hidden = false)
        {
            return Make(preset, category, displayName, description, tags, UtilityThemePresetTone.Dark, family, primary, secondary, tertiary, success, warning, danger, accentAlt, neutral, header, darkAlpha, lightAlpha, borderOpacity, borderWidth, radius, density, hidden);
        }

        private static UtilityThemePresetDefinition Light(UtilityWindowTheme.ThemePreset preset, UtilityThemePresetCategory category, UtilityThemePresetColourFamily family, string displayName, string description, string[] tags, string primary, string secondary, string tertiary, string header, float darkAlpha, float lightAlpha, float borderOpacity, int borderWidth, int radius, float density, string success = null, string warning = null, string danger = null, string accentAlt = null, string neutral = null, bool hidden = false)
        {
            return Make(preset, category, displayName, description, tags, UtilityThemePresetTone.Light, family, primary, secondary, tertiary, success, warning, danger, accentAlt, neutral, header, darkAlpha, lightAlpha, borderOpacity, borderWidth, radius, density, hidden);
        }

        private static UtilityThemePresetDefinition Make(UtilityWindowTheme.ThemePreset preset, UtilityThemePresetCategory category, string displayName, string description, string[] tags, UtilityThemePresetTone tone, UtilityThemePresetColourFamily family, string primary, string secondary, string tertiary, string success, string warning, string danger, string accentAlt, string neutral, string header, float darkAlpha, float lightAlpha, float borderOpacity, int borderWidth, int radius, float density, bool hidden)
        {
            bool light = tone == UtilityThemePresetTone.Light;
            Color headerColor = Hex(header);
            Color primaryColor = Hex(primary);
            Color secondaryColor = Hex(secondary);
            Color tertiaryColor = Hex(tertiary);
            Color lightText = Hex(light ? "#FAFCFF" : "#EEF4FA");
            Color darkText = light ? UtilityWindowTheme.ImproveContrast(Hex("#263241"), headerColor, 5.5f) : Hex("#18202C");
            Color titleText = light ? UtilityWindowTheme.ImproveContrast(darkText, headerColor, 7.0f) : ChooseReadableText(headerColor, 7.0f);
            Color cardText = light ? UtilityWindowTheme.ImproveContrast(darkText, headerColor, 5.2f) : ChooseReadableText(headerColor, 5.2f);
            Color subtitleText = UtilityWindowTheme.ImproveContrast(Color.Lerp(cardText, headerColor, light ? 0.30f : 0.24f), headerColor, light ? 4.35f : 4.5f);
            Color mutedText = UtilityWindowTheme.ImproveContrast(Color.Lerp(cardText, headerColor, light ? 0.44f : 0.42f), headerColor, light ? 3.65f : 3.25f);
            Color pathText = UtilityWindowTheme.ImproveContrast(Color.Lerp(cardText, secondaryColor, 0.20f), headerColor, 4.25f);

            var colors = new Dictionary<string, Color>
            {
                { UtilityWindowTheme.RolePrimary, primaryColor },
                { UtilityWindowTheme.RoleSecondary, secondaryColor },
                { UtilityWindowTheme.RoleTertiary, tertiaryColor },
                { UtilityWindowTheme.RoleSuccess, Hex(success ?? (light ? "#4F9D5D" : "#6FCF97")) },
                { UtilityWindowTheme.RoleWarning, Hex(warning ?? (light ? "#A87218" : "#E4B84A")) },
                { UtilityWindowTheme.RoleDanger, Hex(danger ?? (light ? "#B94D45" : "#E06464")) },
                { UtilityWindowTheme.RoleAccentAlt, Hex(accentAlt ?? tertiary) },
                { UtilityWindowTheme.RoleNeutral, Hex(neutral ?? (light ? "#6F7782" : "#8A94A6")) },
                { UtilityWindowTheme.RoleHeader, headerColor },
                { UtilityWindowTheme.RoleLightText, lightText },
                { UtilityWindowTheme.RoleDarkText, darkText },
                { UtilityWindowTheme.RoleTitleText, titleText },
                { UtilityWindowTheme.RoleSubtitleText, subtitleText },
                { UtilityWindowTheme.RoleMutedText, mutedText },
                { UtilityWindowTheme.RoleCardText, cardText },
                { UtilityWindowTheme.RolePathText, pathText },
                { UtilityWindowTheme.RoleResizeHandle, WithAlpha(secondaryColor, 0.72f) },
            };

            return new UtilityThemePresetDefinition(preset, category, displayName, description, tags, tone, family, colors, darkAlpha, lightAlpha, borderOpacity, borderWidth, radius, density, hidden, BuildFontHints(preset, category, tags));
        }

        private static Dictionary<UtilityWindowTheme.TextRole, string[]> BuildFontHints(UtilityWindowTheme.ThemePreset preset, UtilityThemePresetCategory category, string[] tags)
        {
            string name = preset.ToString().ToLowerInvariant();
            string tagText = tags == null ? string.Empty : string.Join(" ", tags).ToLowerInvariant();
            string[] uiSans = { "Open Sans", "Roboto", "Lato", "Spartan MB", "Montserrat" };
            string[] compact = { "Montserrat", "Spartan MB", "Open Sans", "Roboto", "Lato" };
            string[] mono = { "Perfect DOS VGA 437", "PC Senior", "Flexi IBM VGA True", "Manaspace", "Kongtext", "Terminal Grotesque", "Roboto Mono" };
            string[] heading = ContainsAny(name, tagText, "retro", "arcade", "cyber", "terminal") ? mono : compact;

            return new Dictionary<UtilityWindowTheme.TextRole, string[]>
            {
                { UtilityWindowTheme.TextRole.Heading, heading },
                { UtilityWindowTheme.TextRole.Subheading, compact },
                { UtilityWindowTheme.TextRole.Body, uiSans },
                { UtilityWindowTheme.TextRole.Muted, uiSans },
                { UtilityWindowTheme.TextRole.Link, uiSans },
                { UtilityWindowTheme.TextRole.Field, uiSans },
                { UtilityWindowTheme.TextRole.Path, mono },
                { UtilityWindowTheme.TextRole.Code, mono },
            };
        }

        private static bool ContainsAny(string name, string tagText, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                string needle = needles[i];
                if (string.IsNullOrEmpty(needle))
                    continue;
                if ((!string.IsNullOrEmpty(name) && name.Contains(needle)) || (!string.IsNullOrEmpty(tagText) && tagText.Contains(needle)))
                    return true;
            }

            return false;
        }

        private static void AddLegacyAliases(List<UtilityThemePresetDefinition> list)
        {
            list.Add(Dark(UtilityWindowTheme.ThemePreset.DebugControl, UtilityThemePresetCategory.Legacy, UtilityThemePresetColourFamily.Blue, "Debug Control Legacy", "Archived compatibility alias for the original debug-control blue preset.", Tags("legacy", "debug", "blue"), "#4A93FA", "#46C9F2", "#5DDCC6", "#1E5C76", 0.24f, 0.12f, 0.48f, 1, 6, 1f, hidden: true));
            list.Add(Dark(UtilityWindowTheme.ThemePreset.OceanGlass, UtilityThemePresetCategory.Legacy, UtilityThemePresetColourFamily.Blue, "Ocean Glass Legacy", "Archived compatibility alias for the previous ocean-glass preset.", Tags("legacy", "ocean", "glass"), "#2EA7FF", "#74D7FF", "#4DD7B9", "#0A3B59", 0.24f, 0.12f, 0.48f, 1, 6, 1f, hidden: true));
            list.Add(Dark(UtilityWindowTheme.ThemePreset.ForestNight, UtilityThemePresetCategory.Legacy, UtilityThemePresetColourFamily.Natural, "Forest Night Legacy", "Archived compatibility alias for the previous forest-night preset.", Tags("legacy", "forest", "night"), "#47B565", "#72D290", "#38B8B0", "#24563D", 0.24f, 0.12f, 0.48f, 1, 6, 1f, hidden: true));
            list.Add(Dark(UtilityWindowTheme.ThemePreset.WarmSlate, UtilityThemePresetCategory.Legacy, UtilityThemePresetColourFamily.Warm, "Warm Slate Legacy", "Archived compatibility alias for the previous warm-slate preset.", Tags("legacy", "warm", "slate"), "#D88552", "#F2B361", "#99C2B8", "#734D40", 0.24f, 0.12f, 0.48f, 1, 6, 1f, hidden: true));
        }

        private static void AddArchivedDefinitionsForMissingPresets(List<UtilityThemePresetDefinition> list)
        {
            var used = new HashSet<UtilityWindowTheme.ThemePreset>(list.Select(p => p.Preset));
            foreach (UtilityWindowTheme.ThemePreset preset in Enum.GetValues(typeof(UtilityWindowTheme.ThemePreset)))
            {
                if (used.Contains(preset))
                    continue;

                UtilityThemePresetCategory category = ResolveCategory(preset);
                bool light = ShouldArchiveAsLight(preset);
                Color.RGBToHSV(HashColor(preset), out float h, out float s, out float v);
                s = Mathf.Clamp(s, 0.32f, 0.56f);
                v = Mathf.Clamp(v, 0.48f, 0.76f);
                Color primary = Color.HSVToRGB(h, s, v);
                Color secondary = Color.HSVToRGB(Mathf.Repeat(h + 0.08f, 1f), Mathf.Clamp(s * 0.85f, 0.24f, 0.50f), Mathf.Clamp(v + 0.08f, 0.52f, 0.84f));
                Color tertiary = Color.HSVToRGB(Mathf.Repeat(h - 0.11f, 1f), Mathf.Clamp(s * 0.75f, 0.22f, 0.48f), Mathf.Clamp(v + 0.02f, 0.46f, 0.78f));
                Color header = light ? Color.HSVToRGB(h, 0.14f, 0.92f) : Color.HSVToRGB(h, Mathf.Clamp(s * 0.70f, 0.20f, 0.45f), 0.16f);
                string name = ObjectNames.NicifyVariableName(preset.ToString());
                list.Add(Make(preset, category, name + " (Archived)", "Hidden compatibility placeholder. Add a generated preset definition when this enum value is curated for release.", Tags("archived", category.ToString().ToLowerInvariant(), name.ToLowerInvariant().Replace(" ", "-")), light ? UtilityThemePresetTone.Light : UtilityThemePresetTone.Dark, InferColourFamily(preset, category), Hex(primary), Hex(secondary), Hex(tertiary), null, null, null, null, null, Hex(header), light ? 0.16f : 0.24f, light ? 0.10f : 0.12f, light ? 0.34f : 0.44f, 1, light ? 5 : 4, 1f, true));
            }
        }

        private static UtilityThemePresetCategory ResolveCategory(UtilityWindowTheme.ThemePreset preset)
        {
            switch (preset)
            {
                case UtilityWindowTheme.ThemePreset.Y2KFuturism:
                case UtilityWindowTheme.ThemePreset.Cybernetic:
                case UtilityWindowTheme.ThemePreset.FrutigerAero:
                case UtilityWindowTheme.ThemePreset.Noir:
                case UtilityWindowTheme.ThemePreset.VintageSepia:
                case UtilityWindowTheme.ThemePreset.RetroArcade:
                case UtilityWindowTheme.ThemePreset.Psychedelic:
                case UtilityWindowTheme.ThemePreset.GirlyPop:
                case UtilityWindowTheme.ThemePreset.Minimalist:
                case UtilityWindowTheme.ThemePreset.Maximalist:
                case UtilityWindowTheme.ThemePreset.Vaporwave:
                case UtilityWindowTheme.ThemePreset.Dreamcore:
                case UtilityWindowTheme.ThemePreset.Weirdcore:
                case UtilityWindowTheme.ThemePreset.Solarpunk:
                case UtilityWindowTheme.ThemePreset.DarkAcademia:
                case UtilityWindowTheme.ThemePreset.Cottagecore:
                case UtilityWindowTheme.ThemePreset.Brutalist:
                case UtilityWindowTheme.ThemePreset.Chromecore:
                    return UtilityThemePresetCategory.Aesthetics;
                case UtilityWindowTheme.ThemePreset.Earth:
                case UtilityWindowTheme.ThemePreset.Air:
                case UtilityWindowTheme.ThemePreset.Fire:
                case UtilityWindowTheme.ThemePreset.Water:
                    return UtilityThemePresetCategory.Elements;
                case UtilityWindowTheme.ThemePreset.Forest:
                case UtilityWindowTheme.ThemePreset.Jungle:
                case UtilityWindowTheme.ThemePreset.Underwater:
                case UtilityWindowTheme.ThemePreset.Alpine:
                case UtilityWindowTheme.ThemePreset.Arctic:
                case UtilityWindowTheme.ThemePreset.Glacier:
                case UtilityWindowTheme.ThemePreset.Volcano:
                case UtilityWindowTheme.ThemePreset.Celestial:
                case UtilityWindowTheme.ThemePreset.OilSlick:
                case UtilityWindowTheme.ThemePreset.Floral:
                case UtilityWindowTheme.ThemePreset.Icy:
                case UtilityWindowTheme.ThemePreset.Desert:
                case UtilityWindowTheme.ThemePreset.Tropical:
                case UtilityWindowTheme.ThemePreset.Cave:
                case UtilityWindowTheme.ThemePreset.Moss:
                case UtilityWindowTheme.ThemePreset.CoralReef:
                case UtilityWindowTheme.ThemePreset.Aurora:
                case UtilityWindowTheme.ThemePreset.DeepSea:
                    return UtilityThemePresetCategory.Nature;
                case UtilityWindowTheme.ThemePreset.Spring:
                case UtilityWindowTheme.ThemePreset.Summer:
                case UtilityWindowTheme.ThemePreset.Autumn:
                case UtilityWindowTheme.ThemePreset.Winter:
                    return UtilityThemePresetCategory.Seasons;
                case UtilityWindowTheme.ThemePreset.Dawn:
                case UtilityWindowTheme.ThemePreset.Sunrise:
                case UtilityWindowTheme.ThemePreset.Midday:
                case UtilityWindowTheme.ThemePreset.GoldenHour:
                case UtilityWindowTheme.ThemePreset.Evening:
                case UtilityWindowTheme.ThemePreset.Sunset:
                case UtilityWindowTheme.ThemePreset.Dusk:
                case UtilityWindowTheme.ThemePreset.Twilight:
                case UtilityWindowTheme.ThemePreset.Midnight:
                case UtilityWindowTheme.ThemePreset.Eclipse:
                    return UtilityThemePresetCategory.Skies;
                case UtilityWindowTheme.ThemePreset.Sunny:
                case UtilityWindowTheme.ThemePreset.Stormy:
                case UtilityWindowTheme.ThemePreset.Cloudy:
                case UtilityWindowTheme.ThemePreset.Heatwave:
                case UtilityWindowTheme.ThemePreset.Lightning:
                case UtilityWindowTheme.ThemePreset.Blizzard:
                case UtilityWindowTheme.ThemePreset.Rainy:
                case UtilityWindowTheme.ThemePreset.Foggy:
                case UtilityWindowTheme.ThemePreset.Overcast:
                case UtilityWindowTheme.ThemePreset.Hailstorm:
                    return UtilityThemePresetCategory.Weather;
                case UtilityWindowTheme.ThemePreset.Red:
                case UtilityWindowTheme.ThemePreset.Yellow:
                case UtilityWindowTheme.ThemePreset.Pink:
                case UtilityWindowTheme.ThemePreset.Green:
                case UtilityWindowTheme.ThemePreset.Purple:
                case UtilityWindowTheme.ThemePreset.Orange:
                case UtilityWindowTheme.ThemePreset.Blue:
                case UtilityWindowTheme.ThemePreset.Cyan:
                case UtilityWindowTheme.ThemePreset.Magenta:
                case UtilityWindowTheme.ThemePreset.Monochrome:
                    return UtilityThemePresetCategory.Colours;
                default:
                    return UtilityThemePresetCategory.Base;
            }
        }

        private static UtilityThemePresetColourFamily InferColourFamily(UtilityWindowTheme.ThemePreset preset, UtilityThemePresetCategory category)
        {
            switch (preset)
            {
                case UtilityWindowTheme.ThemePreset.HighContrastDark:
                case UtilityWindowTheme.ThemePreset.AccessibilityAmber:
                case UtilityWindowTheme.ThemePreset.AccessibilityCyan:
                    return UtilityThemePresetColourFamily.HighContrast;
                case UtilityWindowTheme.ThemePreset.RetroArcade:
                    return UtilityThemePresetColourFamily.Retro;
                case UtilityWindowTheme.ThemePreset.Cybernetic:
                case UtilityWindowTheme.ThemePreset.Vaporwave:
                case UtilityWindowTheme.ThemePreset.Purple:
                case UtilityWindowTheme.ThemePreset.OilSlick:
                    return UtilityThemePresetColourFamily.Vibrant;
                case UtilityWindowTheme.ThemePreset.FrutigerAero:
                case UtilityWindowTheme.ThemePreset.Spring:
                case UtilityWindowTheme.ThemePreset.Air:
                    return UtilityThemePresetColourFamily.Pastel;
                case UtilityWindowTheme.ThemePreset.Forest:
                case UtilityWindowTheme.ThemePreset.ForestNight:
                case UtilityWindowTheme.ThemePreset.Earth:
                case UtilityWindowTheme.ThemePreset.Green:
                    return UtilityThemePresetColourFamily.Natural;
                case UtilityWindowTheme.ThemePreset.Autumn:
                case UtilityWindowTheme.ThemePreset.GoldenHour:
                case UtilityWindowTheme.ThemePreset.WarmSlate:
                case UtilityWindowTheme.ThemePreset.Fire:
                case UtilityWindowTheme.ThemePreset.Sunny:
                case UtilityWindowTheme.ThemePreset.VintageSepia:
                    return UtilityThemePresetColourFamily.Warm;
                case UtilityWindowTheme.ThemePreset.Alpine:
                case UtilityWindowTheme.ThemePreset.Glacier:
                case UtilityWindowTheme.ThemePreset.Winter:
                case UtilityWindowTheme.ThemePreset.Midnight:
                case UtilityWindowTheme.ThemePreset.Celestial:
                case UtilityWindowTheme.ThemePreset.OceanGlass:
                case UtilityWindowTheme.ThemePreset.Blue:
                case UtilityWindowTheme.ThemePreset.Water:
                case UtilityWindowTheme.ThemePreset.Stormy:
                    return UtilityThemePresetColourFamily.Blue;
                case UtilityWindowTheme.ThemePreset.Graphite:
                case UtilityWindowTheme.ThemePreset.CleanLight:
                case UtilityWindowTheme.ThemePreset.PungentDefault:
                case UtilityWindowTheme.ThemePreset.SoftDark:
                    return UtilityThemePresetColourFamily.Professional;
            }

            switch (category)
            {
                case UtilityThemePresetCategory.Elements:
                case UtilityThemePresetCategory.Nature:
                    return UtilityThemePresetColourFamily.Natural;
                case UtilityThemePresetCategory.Seasons:
                case UtilityThemePresetCategory.Weather:
                    return UtilityThemePresetColourFamily.Warm;
                case UtilityThemePresetCategory.Aesthetics:
                    return UtilityThemePresetColourFamily.Vibrant;
                default:
                    return UtilityThemePresetColourFamily.Neutral;
            }
        }

        private static bool ShouldArchiveAsLight(UtilityWindowTheme.ThemePreset preset)
        {
            switch (preset)
            {
                case UtilityWindowTheme.ThemePreset.UnityDefault:
                case UtilityWindowTheme.ThemePreset.CleanLight:
                case UtilityWindowTheme.ThemePreset.Air:
                case UtilityWindowTheme.ThemePreset.Yellow:
                case UtilityWindowTheme.ThemePreset.Pink:
                    return true;
                default:
                    return false;
            }
        }

        private static Color ChooseReadableText(Color background, float targetContrast)
        {
            Color light = Hex("#F4F7FB");
            Color dark = Hex("#111827");
            Color start = UtilityWindowTheme.GetContrastRatio(light, background) >= UtilityWindowTheme.GetContrastRatio(dark, background) ? light : dark;
            return UtilityWindowTheme.ImproveContrast(start, background, targetContrast);
        }

        private static Color HashColor(UtilityWindowTheme.ThemePreset preset)
        {
            int hash = preset.ToString().GetHashCode();
            float h = ((hash & 0xFFFF) / 65535f + 0.13f) % 1f;
            return Color.HSVToRGB(h, 0.45f, 0.65f);
        }

        private static string[] Tags(params string[] tags)
        {
            return tags ?? Array.Empty<string>();
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;

            return Color.white;
        }

        private static string Hex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
#endif
}
