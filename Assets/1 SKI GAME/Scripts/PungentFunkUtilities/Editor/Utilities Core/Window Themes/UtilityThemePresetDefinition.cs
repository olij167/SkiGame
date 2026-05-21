namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public enum UtilityThemePresetCategory
    {
        Base,
        Aesthetics,
        Elements,
        Nature,
        Seasons,
        Skies,
        Weather,
        Colours,
        Legacy
    }

    public enum UtilityThemePresetTone
    {
        Dark,
        Light,
        Mixed
    }

    public enum UtilityThemePresetColourFamily
    {
        Neutral,
        Blue,
        Green,
        Warm,
        Pastel,
        Vibrant,
        Retro,
        HighContrast,
        Natural,
        Professional
    }

    /// <summary>
    /// Immutable built-in appearance preset definition used by the Appearance Lab and shared theme API.
    /// </summary>
    public sealed class UtilityThemePresetDefinition
    {
        private readonly Dictionary<string, Color> _colors;

        public UtilityWindowTheme.ThemePreset Preset { get; }
        public UtilityThemePresetCategory Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string[] Tags { get; }
        public UtilityThemePresetTone Tone { get; }
        public UtilityThemePresetColourFamily ColourFamily { get; }
        public float PanelAlphaDark { get; }
        public float PanelAlphaLight { get; }
        public float PanelBorderOpacity { get; }
        public int PanelBorderWidth { get; }
        public int PanelCornerRadius { get; }
        public float Density { get; }
        public bool HiddenFromGallery { get; }
        public IReadOnlyDictionary<UtilityWindowTheme.TextRole, string[]> FontHints { get; }

        public UtilityThemePresetDefinition(
            UtilityWindowTheme.ThemePreset preset,
            UtilityThemePresetCategory category,
            string displayName,
            string description,
            string[] tags,
            UtilityThemePresetTone tone,
            UtilityThemePresetColourFamily colourFamily,
            Dictionary<string, Color> colors,
            float panelAlphaDark = 0.22f,
            float panelAlphaLight = 0.12f,
            float panelBorderOpacity = 0.55f,
            int panelBorderWidth = 1,
            int panelCornerRadius = 6,
            float density = 1f,
            bool hiddenFromGallery = false,
            Dictionary<UtilityWindowTheme.TextRole, string[]> fontHints = null)
        {
            Preset = preset;
            Category = category;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ObjectNames.NicifyVariableName(preset.ToString()) : displayName;
            Description = description ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
            Tone = tone;
            ColourFamily = colourFamily;
            _colors = colors ?? new Dictionary<string, Color>();
            PanelAlphaDark = Mathf.Clamp01(panelAlphaDark);
            PanelAlphaLight = Mathf.Clamp01(panelAlphaLight);
            PanelBorderOpacity = Mathf.Clamp01(panelBorderOpacity);
            PanelBorderWidth = Mathf.Clamp(panelBorderWidth, 0, 2);
            PanelCornerRadius = Mathf.Clamp(panelCornerRadius, 0, 10);
            Density = Mathf.Clamp(density, 0.75f, 1.35f);
            HiddenFromGallery = hiddenFromGallery;
            FontHints = fontHints ?? new Dictionary<UtilityWindowTheme.TextRole, string[]>();
        }

        public Color GetColor(string role, Color fallback)
        {
            if (!string.IsNullOrEmpty(role) && _colors.TryGetValue(role, out Color color))
                return color;
            return fallback;
        }

        public IReadOnlyDictionary<string, Color> Colors => _colors;

        public string[] GetFontHints(UtilityWindowTheme.TextRole role)
        {
            if (FontHints != null && FontHints.TryGetValue(role, out string[] hints) && hints != null)
                return hints;
            return Array.Empty<string>();
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (string.Equals(Tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            string needle = search.Trim();
            if (DisplayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (Description.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (!string.IsNullOrEmpty(Tags[i]) && Tags[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
#endif
}
