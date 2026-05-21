namespace PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring
{
#if UNITY_EDITOR && PUNGENTFUNK_INTERNAL_DEVTOOLS
    using System;
    using System.Collections.Generic;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEngine;

    [Serializable]
    public sealed class UtilityThemePresetSnapshot
    {
        public UtilityWindowTheme.ThemePreset preset;
        public UtilityThemePresetCategory category;
        public string displayName;
        public string description;
        public string[] tags;
        public UtilityThemePresetTone tone;
        public UtilityThemePresetColourFamily colourFamily;
        public bool hiddenFromGallery;
        public float panelAlphaDark;
        public float panelAlphaLight;
        public float panelBorderOpacity;
        public int panelBorderWidth;
        public int panelCornerRadius;
        public float density;
        public Dictionary<string, Color> colors = new Dictionary<string, Color>();
        public Dictionary<UtilityWindowTheme.TextRole, string[]> fontHints = new Dictionary<UtilityWindowTheme.TextRole, string[]>();

        public static UtilityThemePresetSnapshot CaptureCurrent(UtilityWindowTheme.ThemePreset targetPreset, string displayNameOverride = null, bool? hiddenOverride = null)
        {
            UtilityThemePresetDefinition source = UtilityThemePresetLibrary.Get(targetPreset);
            var snapshot = new UtilityThemePresetSnapshot
            {
                preset = targetPreset,
                category = source.Category,
                displayName = string.IsNullOrWhiteSpace(displayNameOverride) ? source.DisplayName : displayNameOverride,
                description = source.Description,
                tags = source.Tags,
                tone = source.Tone,
                colourFamily = source.ColourFamily,
                hiddenFromGallery = hiddenOverride ?? source.HiddenFromGallery,
                panelAlphaDark = UtilityWindowTheme.PanelAlphaDark,
                panelAlphaLight = UtilityWindowTheme.PanelAlphaLight,
                panelBorderOpacity = UtilityWindowTheme.PanelBorderOpacity,
                panelBorderWidth = UtilityWindowTheme.PanelBorderWidth,
                panelCornerRadius = UtilityWindowTheme.PanelCornerRadius,
                density = UtilityWindowTheme.Density
            };

            foreach (string role in UtilityWindowTheme.EditableColorRoles)
                snapshot.colors[role] = UtilityWindowTheme.GetColor(role);

            foreach (UtilityWindowTheme.TextRole role in Enum.GetValues(typeof(UtilityWindowTheme.TextRole)))
                snapshot.fontHints[role] = source.GetFontHints(role);

            return snapshot;
        }

        public static UtilityThemePresetSnapshot FromDefinition(UtilityThemePresetDefinition definition)
        {
            var snapshot = new UtilityThemePresetSnapshot
            {
                preset = definition.Preset,
                category = definition.Category,
                displayName = definition.DisplayName,
                description = definition.Description,
                tags = definition.Tags,
                tone = definition.Tone,
                colourFamily = definition.ColourFamily,
                hiddenFromGallery = definition.HiddenFromGallery,
                panelAlphaDark = definition.PanelAlphaDark,
                panelAlphaLight = definition.PanelAlphaLight,
                panelBorderOpacity = definition.PanelBorderOpacity,
                panelBorderWidth = definition.PanelBorderWidth,
                panelCornerRadius = definition.PanelCornerRadius,
                density = definition.Density,
                colors = new Dictionary<string, Color>(definition.Colors),
                fontHints = new Dictionary<UtilityWindowTheme.TextRole, string[]>()
            };

            foreach (UtilityWindowTheme.TextRole role in Enum.GetValues(typeof(UtilityWindowTheme.TextRole)))
                snapshot.fontHints[role] = definition.GetFontHints(role);

            return snapshot;
        }
    }
#endif
}
