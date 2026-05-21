namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using UnityEngine;

    public enum PungentEditorSkinCompatibility
    {
        Both,
        DarkOnly,
        LightOnly
    }

    public enum PungentEditorSkinProperty
    {
        BackgroundColor,
        Color,
        BorderColor,
        BorderWidth,
        BorderRadius,
        UnityFont,
        FontSize,
        ImageTintColor
    }

    [Serializable]
    public sealed class PungentEditorSkinProfile
    {
        public string displayName = "Pungent Editor Skin";
        public PungentEditorSkinCompatibility compatibility = PungentEditorSkinCompatibility.Both;
        public bool includeCommonUss = true;
        public bool includeDarkUss = true;
        public bool includeLightUss = true;
        public bool excludeSceneView = true;
        public bool excludeGameView = true;
        public bool excludePungentWindows = true;
        public bool logDiagnostics = true;

        public static PungentEditorSkinProfile Default => new PungentEditorSkinProfile();
    }

    public readonly struct PungentEditorStyleOverride
    {
        public readonly string Selector;
        public readonly PungentEditorSkinProperty Property;
        public readonly string ThemeRole;
        public readonly float AlphaMultiplier;
        public readonly bool EnabledByDefault;
        public readonly string Description;
        public readonly string RawValue;

        public PungentEditorStyleOverride(string selector, PungentEditorSkinProperty property, string themeRole, float alphaMultiplier = 1f, bool enabledByDefault = true, string description = null, string rawValue = null)
        {
            Selector = selector ?? string.Empty;
            Property = property;
            ThemeRole = themeRole ?? UtilityWindowTheme.RolePrimary;
            AlphaMultiplier = Mathf.Clamp01(alphaMultiplier <= 0f ? 1f : alphaMultiplier);
            EnabledByDefault = enabledByDefault;
            Description = description ?? string.Empty;
            RawValue = rawValue ?? string.Empty;
        }
    }
#endif
}
