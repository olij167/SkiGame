namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Session-only GUIStyle text/font bridge. It is intentionally reversible and does not wrap IMGUI containers or patch Unity resources.
    /// </summary>
    [InitializeOnLoad]
    public static class PungentEditorGuiStyleBridge
    {
        private const string PrefPrefix = "GenericUtility.WindowTheme.ExperimentalEditorSkin.GuiStyle.";
        private const string PrefEnabled = PrefPrefix + "Enabled";
        private const string PrefLastAction = PrefPrefix + "LastAction";

        private static readonly Dictionary<GUIStyle, StyleSnapshot> _snapshots = new Dictionary<GUIStyle, StyleSnapshot>();
        private static readonly HashSet<Texture2D> _ownedTextures = new HashSet<Texture2D>();

        static PungentEditorGuiStyleBridge()
        {
            if (SessionOverridesEnabled)
                EditorApplication.delayCall += () => ApplySessionThemeToAllStyles(false);
        }

        public static bool SessionOverridesEnabled
        {
            get => UtilityWindowPrefs.GetBool(PrefEnabled, false);
            set => UtilityWindowPrefs.SetBool(PrefEnabled, value);
        }

        public static string LastAction
        {
            get => UtilityWindowPrefs.GetString(PrefLastAction, "No GUIStyle session overrides have been applied.");
            private set => UtilityWindowPrefs.SetString(PrefLastAction, value);
        }

        public static void ApplySessionThemeToKnownStyles()
        {
            ApplySessionThemeToAllStyles(false);
        }

        public static void ApplySessionThemeToAllStyles(bool includeBackgrounds)
        {
            SessionOverridesEnabled = true;
            int touched = 0;

            foreach (GUIStyle style in EnumerateStyles())
            {
                if (style == null)
                    continue;

                string styleName = string.IsNullOrEmpty(style.name) ? string.Empty : style.name;
                StyleRoleMapping mapping = ResolveMapping(styleName);
                if (!mapping.apply)
                    continue;

                bool includeStyleBackground = includeBackgrounds && mapping.backgroundSafe;
                ApplyThemeToStyle(style, mapping.textRole, mapping.colorRole, true, includeStyleBackground);
                touched++;
            }

            LastAction = $"Applied session GUIStyle text/font overrides to {touched} styles at {DateTime.Now:HH:mm:ss}. Font sizes were preserved.";
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public static void ApplyNativeHierarchyGutterFallback()
        {
            PungentEditorTextRoleMap.NativeHierarchyGutterGuiStyleFallbackEnabled = true;
            SessionOverridesEnabled = true;

            int touched = 0;

            foreach (GUIStyle style in EnumerateStyles())
            {
                if (style == null)
                    continue;

                string styleName = string.IsNullOrEmpty(style.name) ? string.Empty : style.name;
                PungentEditorTextTarget target = PungentEditorTextRoleMap.ResolveTargetFromGuiStyleName(styleName);

                if (target != PungentEditorTextTarget.HierarchyItems && target != PungentEditorTextTarget.HierarchyGutter)
                    continue;

                if (!PungentEditorTextRoleMap.GetApplyGuiStyle(target))
                    continue;

                ApplyThemeToStyle(
                    style,
                    PungentEditorTextRoleMap.GetTextRole(target),
                    PungentEditorTextRoleMap.GetColorRole(target),
                    true,
                    true);

                touched++;
            }

            LastAction = $"Applied native hierarchy/tree GUIStyle fallback to {touched} styles at {DateTime.Now:HH:mm:ss}. Font sizes were preserved.";
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public static bool ApplyThemeToStyle(GUIStyle style, UtilityWindowTheme.TextRole textRole, string colorRole, bool includeFont, bool includeBackground)
        {
            if (style == null)
                return false;

            Capture(style);
            ApplyText(style, UtilityWindowTheme.GetColor(colorRole), includeFont ? UtilityWindowTheme.GetFont(textRole) : null);
            if (includeBackground)
                ApplySubtleBackground(style, UtilityWindowTheme.GetColor(UtilityWindowTheme.RoleHeader), UtilityWindowTheme.GetColor(UtilityWindowTheme.RolePrimary));
            LastAction = "Applied session override to GUIStyle: " + (string.IsNullOrEmpty(style.name) ? "<unnamed>" : style.name);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            return true;
        }

        public static void DisableAndRestore()
        {
            foreach (KeyValuePair<GUIStyle, StyleSnapshot> pair in _snapshots)
            {
                if (pair.Key != null)
                    pair.Value.Restore(pair.Key);
            }

            _snapshots.Clear();
            foreach (Texture2D texture in _ownedTextures)
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
            _ownedTextures.Clear();
            SessionOverridesEnabled = false;
            LastAction = $"Restored session GUIStyle overrides at {DateTime.Now:HH:mm:ss}.";
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static IEnumerable<GUIStyle> EnumerateStyles()
        {
            if (GUI.skin == null)
                yield break;

            if (GUI.skin.customStyles != null)
            {
                for (int i = 0; i < GUI.skin.customStyles.Length; i++)
                    yield return GUI.skin.customStyles[i];
            }

            yield return GUI.skin.box;
            yield return GUI.skin.button;
            yield return GUI.skin.label;
            yield return GUI.skin.textField;
            yield return GUI.skin.textArea;
            yield return GUI.skin.toggle;
            yield return GUI.skin.window;
            yield return GUI.skin.horizontalScrollbar;
            yield return GUI.skin.verticalScrollbar;
        }

        private static StyleRoleMapping ResolveMapping(string styleName)
        {
            string lower = (styleName ?? string.Empty).ToLowerInvariant();

            if (PungentEditorTextRoleMap.IsProbablyNonTextGuiStyle(lower))
                return new StyleRoleMapping(false, UtilityWindowTheme.TextRole.Body, UtilityWindowTheme.RoleCardText, false);

            if (lower.Contains("error"))
                return new StyleRoleMapping(true, UtilityWindowTheme.TextRole.Body, UtilityWindowTheme.RoleDanger, false);

            if (lower.Contains("warning") || lower.Contains("alert"))
                return new StyleRoleMapping(true, UtilityWindowTheme.TextRole.Body, UtilityWindowTheme.RoleWarning, false);

            PungentEditorTextTarget target = PungentEditorTextRoleMap.ResolveTargetFromGuiStyleName(styleName);

            if (!PungentEditorTextRoleMap.GetApplyGuiStyle(target))
                return new StyleRoleMapping(false, UtilityWindowTheme.TextRole.Body, UtilityWindowTheme.RoleCardText, false);

            bool backgroundSafe = PungentEditorTextRoleMap.IsBackgroundSafe(target);

            if (target == PungentEditorTextTarget.HierarchyGutter)
                backgroundSafe = PungentEditorTextRoleMap.NativeHierarchyGutterGuiStyleFallbackEnabled;

            return new StyleRoleMapping(
                true,
                PungentEditorTextRoleMap.GetTextRole(target),
                PungentEditorTextRoleMap.GetColorRole(target),
                backgroundSafe);
        }


        private static bool IsHierarchyOrTreeStyle(string lower)
        {
            return PungentEditorTextRoleMap.IsHierarchyOrTreeStyleName(lower);
        }

        private static void Capture(GUIStyle style)
        {
            if (style == null || _snapshots.ContainsKey(style))
                return;
            _snapshots.Add(style, new StyleSnapshot(style));
        }

        private static void ApplyText(GUIStyle style, Color textColor, Font font)
        {
            Capture(style);

            ApplyStateText(style.normal, textColor);
            ApplyStateText(style.hover, Color.Lerp(textColor, UtilityWindowTheme.GetColor(UtilityWindowTheme.RoleSecondary), 0.20f));
            ApplyStateText(style.active, UtilityWindowTheme.GetColor(UtilityWindowTheme.RoleTitleText));
            ApplyStateText(style.focused, textColor);
            ApplyStateText(style.onNormal, textColor);
            ApplyStateText(style.onHover, Color.Lerp(textColor, UtilityWindowTheme.GetColor(UtilityWindowTheme.RoleSecondary), 0.20f));
            ApplyStateText(style.onActive, UtilityWindowTheme.GetColor(UtilityWindowTheme.RoleTitleText));
            ApplyStateText(style.onFocused, textColor);

            if (font != null)
                style.font = font;

            // Deliberately do not change style.fontSize here.
            // Unity's editor GUIStyles already encode hierarchy/inspector/console sizing.
            // Scaling them in-session causes repeated applications to compound and makes inactive hierarchy rows oversized.
        }

        private static void ApplySubtleBackground(GUIStyle style, Color header, Color primary)
        {
            string lower = string.IsNullOrEmpty(style.name) ? string.Empty : style.name.ToLowerInvariant();
            bool hierarchyOrTree = IsHierarchyOrTreeStyle(lower);

            Color normal = hierarchyOrTree
                ? Color.Lerp(header, primary, EditorGUIUtility.isProSkin ? 0.12f : 0.08f)
                : EditorGUIUtility.isProSkin
                    ? new Color(header.r, header.g, header.b, 0.24f)
                    : new Color(primary.r, primary.g, primary.b, 0.10f);

            normal.a = hierarchyOrTree ? 1f : Mathf.Clamp01(normal.a);

            Color active = Color.Lerp(header, primary, EditorGUIUtility.isProSkin ? 0.42f : 0.28f);
            active.a = hierarchyOrTree ? 1f : EditorGUIUtility.isProSkin ? 0.34f : 0.20f;

            Texture2D normalTexture = MakeTexture(normal);
            Texture2D activeTexture = MakeTexture(active);

            style.normal.background = normalTexture;
            style.hover.background = normalTexture;
            style.focused.background = normalTexture;
            style.onNormal.background = normalTexture;
            style.onHover.background = normalTexture;
            style.onFocused.background = normalTexture;

            style.active.background = activeTexture;
            style.onActive.background = activeTexture;
        }

        private static void ApplyStateText(GUIStyleState state, Color textColor)
        {
            if (state != null)
                state.textColor = textColor;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            _ownedTextures.Add(texture);
            return texture;
        }

        private readonly struct StyleRoleMapping
        {
            public readonly bool apply;
            public readonly UtilityWindowTheme.TextRole textRole;
            public readonly string colorRole;
            public readonly bool backgroundSafe;

            public StyleRoleMapping(bool apply, UtilityWindowTheme.TextRole textRole, string colorRole, bool backgroundSafe)
            {
                this.apply = apply;
                this.textRole = textRole;
                this.colorRole = colorRole;
                this.backgroundSafe = backgroundSafe;
            }
        }

        private sealed class StyleSnapshot
        {
            private readonly Font _font;
            private readonly int _fontSize;
            private readonly FontStyle _fontStyle;
            private readonly StateSnapshot _normal;
            private readonly StateSnapshot _hover;
            private readonly StateSnapshot _active;
            private readonly StateSnapshot _focused;
            private readonly StateSnapshot _onNormal;
            private readonly StateSnapshot _onHover;
            private readonly StateSnapshot _onActive;
            private readonly StateSnapshot _onFocused;

            public StyleSnapshot(GUIStyle style)
            {
                _font = style.font;
                _fontSize = style.fontSize;
                _fontStyle = style.fontStyle;
                _normal = new StateSnapshot(style.normal);
                _hover = new StateSnapshot(style.hover);
                _active = new StateSnapshot(style.active);
                _focused = new StateSnapshot(style.focused);
                _onNormal = new StateSnapshot(style.onNormal);
                _onHover = new StateSnapshot(style.onHover);
                _onActive = new StateSnapshot(style.onActive);
                _onFocused = new StateSnapshot(style.onFocused);
            }

            public void Restore(GUIStyle style)
            {
                style.font = _font;
                style.fontSize = _fontSize;
                style.fontStyle = _fontStyle;
                _normal.Restore(style.normal);
                _hover.Restore(style.hover);
                _active.Restore(style.active);
                _focused.Restore(style.focused);
                _onNormal.Restore(style.onNormal);
                _onHover.Restore(style.onHover);
                _onActive.Restore(style.onActive);
                _onFocused.Restore(style.onFocused);
            }
        }

        private readonly struct StateSnapshot
        {
            private readonly Color _textColor;
            private readonly Texture2D _background;

            public StateSnapshot(GUIStyleState state)
            {
                _textColor = state != null ? state.textColor : Color.white;
                _background = state != null ? state.background : null;
            }

            public void Restore(GUIStyleState state)
            {
                if (state == null)
                    return;
                state.textColor = _textColor;
                state.background = _background;
            }
        }
    }
#endif
}
