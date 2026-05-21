namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using PungentFunk.Utilities.Editor.Core;

    /// <summary>
    /// Shared IMGUI styling helpers for PungentFunk utility windows.
    /// The active appearance settings are stored in EditorPrefs so every utility window can share one editable theme.
    /// </summary>
    public static class UtilityWindowTheme
    {
        public enum ThemePreset
        {
            PungentDefault,
            UnityDefault,
            HighContrastDark,
            CleanLight,
            Graphite,
            SoftDark,
            AccessibilityAmber,
            AccessibilityCyan,

            Y2KFuturism,
            Cybernetic,
            FrutigerAero,
            Noir,
            VintageSepia,
            RetroArcade,
            Psychedelic,
            GirlyPop,
            Minimalist,
            Maximalist,
            Vaporwave,
            Dreamcore,
            Weirdcore,
            Solarpunk,
            DarkAcademia,
            Cottagecore,
            Brutalist,
            Chromecore,

            Earth,
            Air,
            Fire,
            Water,

            Forest,
            Jungle,
            Underwater,
            Alpine,
            Arctic,
            Glacier,
            Volcano,
            Celestial,
            OilSlick,
            Floral,
            Icy,
            Desert,
            Tropical,
            Cave,
            Moss,
            CoralReef,
            Aurora,
            DeepSea,

            Spring,
            Summer,
            Autumn,
            Winter,

            Dawn,
            Sunrise,
            Midday,
            GoldenHour,
            Evening,
            Sunset,
            Dusk,
            Twilight,
            Midnight,
            Eclipse,

            Sunny,
            Stormy,
            Cloudy,
            Heatwave,
            Lightning,
            Blizzard,
            Rainy,
            Foggy,
            Overcast,
            Hailstorm,

            Red,
            Yellow,
            Pink,
            Green,
            Purple,
            Orange,
            Blue,
            Cyan,
            Magenta,
            Monochrome,

            // Compatibility aliases kept so external editor code that referenced the first appearance pass still compiles.
            DebugControl,
            OceanGlass,
            ForestNight,
            WarmSlate
        }

        public enum DeficiencyPreview
        {
            None,
            Protanopia,
            Deuteranopia,
            Tritanopia,
            Achromatopsia
        }

        public enum ThemeScope
        {
            PungentFunkUtilitiesOnly,
            PungentFunkUtilitiesInspectorsAndOverlays,
            ExperimentalUnityEditorUssBridge
        }

        public enum TextRole
        {
            Heading,
            Subheading,
            Body,
            Muted,
            Link,
            Field,
            Path,
            Code
        }

        public enum PungentButtonRole
        {
            Primary,
            Secondary,
            Subtle,
            Toolbar,
            Chip,
            Danger,
            Warning,
            Disabled,
            Link
        }

        public struct UtilityHeaderOptions
        {
            public string UtilityId;
            public string Title;
            public string Description;
            public string Status;
            public string CompactStatus;
            public string HelpSectionId;
            public string HelpTopicId;
            public bool ShowHelp;
            public bool ShowMinimizeTray;
            public bool ShowMinimizeButton;
            public Color Tint;
        }

        public const string RolePrimary = "primary";
        public const string RoleSecondary = "secondary";
        public const string RoleTertiary = "tertiary";
        public const string RoleSuccess = "success";
        public const string RoleWarning = "warning";
        public const string RoleDanger = "danger";
        public const string RoleAccentAlt = "accentAlt";
        public const string RoleNeutral = "neutral";
        public const string RoleHeader = "header";
        public const string RoleTitleText = "titleText";
        public const string RoleSubtitleText = "subtitleText";
        public const string RoleMutedText = "mutedText";
        public const string RoleCardText = "cardText";
        public const string RolePathText = "pathText";
        public const string RoleLightText = "lightText";
        public const string RoleDarkText = "darkText";
        public const string RoleResizeHandle = "resizeHandle";

        private const string PrefPrefix = "GenericUtility.WindowTheme.";
        private const string PrefPreset = PrefPrefix + "Preset";
        private const string PrefScope = PrefPrefix + "Scope";
        private const string PrefDeficiencyPreview = PrefPrefix + "DeficiencyPreview";
        private const string PrefPanelAlphaDark = PrefPrefix + "PanelAlphaDark";
        private const string PrefPanelAlphaLight = PrefPrefix + "PanelAlphaLight";
        private const string PrefTextScale = PrefPrefix + "TextScale";
        private const string PrefDensity = PrefPrefix + "Density";
        private const string PrefPanelBorderWidth = PrefPrefix + "PanelBorderWidth";
        private const string PrefPanelBorderOpacity = PrefPrefix + "PanelBorderOpacity";
        private const string PrefPanelCornerRadius = PrefPrefix + "PanelCornerRadius";
        private const string PrefActiveThemeModified = PrefPrefix + "ActiveThemeModified";

        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _mutedMiniLabelStyle;
        private static GUIStyle _countPillStyle;
        private static GUIStyle _toolbarSearchStyle;
        private static GUIStyle _cardLabelStyle;
        private static GUIStyle _pathLabelStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _linkStyle;
        private static GUIStyle _welcomeHeroTitleStyle;
        private static GUIStyle _welcomeSectionTitleStyle;
        private static GUIStyle _welcomeBodyStyle;
        private static GUIStyle _welcomeMetaStyle;
        private static GUIStyle _welcomeStepLabelStyle;
        private static readonly Dictionary<string, GUIStyle> _tintedBoxStyleCache = new Dictionary<string, GUIStyle>();
        private static readonly Dictionary<string, GUIStyle> _buttonStyleCache = new Dictionary<string, GUIStyle>();
        private static readonly Dictionary<string, Texture2D> _backgroundTextureCache = new Dictionary<string, Texture2D>();
        private static bool _repaintQueued;

        public static readonly string[] EditableColorRoles =
        {
            RolePrimary,
            RoleSecondary,
            RoleTertiary,
            RoleSuccess,
            RoleWarning,
            RoleDanger,
            RoleAccentAlt,
            RoleNeutral,
            RoleHeader,
            RoleTitleText,
            RoleSubtitleText,
            RoleMutedText,
            RoleCardText,
            RolePathText,
            RoleLightText,
            RoleDarkText,
            RoleResizeHandle
        };

        public static Color Blue => GetColor(RolePrimary);
        public static Color Cyan => GetColor(RoleSecondary);
        public static Color Teal => GetColor(RoleTertiary);
        public static Color Green => GetColor(RoleSuccess);
        public static Color Amber => GetColor(RoleWarning);
        public static Color Red => GetColor(RoleDanger);
        public static Color Purple => GetColor(RoleAccentAlt);
        public static Color Neutral => GetColor(RoleNeutral);
        public static Color HeaderTint => GetColor(RoleHeader);
        public static Color TitleText => GetColor(RoleTitleText);
        public static Color SubtitleText => GetColor(RoleSubtitleText);
        public static Color MutedText => GetColor(RoleMutedText);
        public static Color CardText => GetColor(RoleCardText);
        public static Color PathText => GetColor(RolePathText);
        public static Color LightText => GetColor(RoleLightText);
        public static Color DarkText => GetColor(RoleDarkText);
        public static Color ResizeHandleTint => GetColor(RoleResizeHandle);

        public static float PanelAlphaDark
        {
            get => Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefPanelAlphaDark, UtilityThemePresetLibrary.Get(ActivePreset).PanelAlphaDark));
            set
            {
                UtilityWindowPrefs.SetFloat(PrefPanelAlphaDark, Mathf.Clamp01(value));
                MarkActiveThemeModified();
                InvalidateStyles();
            }
        }

        public static float PanelAlphaLight
        {
            get => Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefPanelAlphaLight, UtilityThemePresetLibrary.Get(ActivePreset).PanelAlphaLight));
            set
            {
                UtilityWindowPrefs.SetFloat(PrefPanelAlphaLight, Mathf.Clamp01(value));
                MarkActiveThemeModified();
                InvalidateStyles();
            }
        }

        public static float TextScale
        {
            get => Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefTextScale, 1f), 0.80f, 1.35f);
            set
            {
                UtilityWindowPrefs.SetFloat(PrefTextScale, Mathf.Clamp(value, 0.80f, 1.35f));
                MarkActiveThemeModified();
                InvalidateStyles();
            }
        }

        public static float Density
        {
            get => Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefDensity, UtilityThemePresetLibrary.Get(ActivePreset).Density), 0.75f, 1.35f);
            set
            {
                UtilityWindowPrefs.SetFloat(PrefDensity, Mathf.Clamp(value, 0.75f, 1.35f));
                MarkActiveThemeModified();
                InvalidateStyles();
            }
        }

        public static int PanelBorderWidth
        {
            get => Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefPanelBorderWidth, UtilityThemePresetLibrary.Get(ActivePreset).PanelBorderWidth), 0, 2);
            set
            {
                UtilityWindowPrefs.SetInt(PrefPanelBorderWidth, Mathf.Clamp(value, 0, 2));
                MarkActiveThemeModified();
                InvalidateStyles();
            }
        }

        public static float PanelBorderOpacity
        {
            get => Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefPanelBorderOpacity, UtilityThemePresetLibrary.Get(ActivePreset).PanelBorderOpacity));
            set
            {
                UtilityWindowPrefs.SetFloat(PrefPanelBorderOpacity, Mathf.Clamp01(value));
                MarkActiveThemeModified();
                InvalidateStyles();
            }
        }

        public static int PanelCornerRadius
        {
            get => Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefPanelCornerRadius, UtilityThemePresetLibrary.Get(ActivePreset).PanelCornerRadius), 0, 10);
            set
            {
                UtilityWindowPrefs.SetInt(PrefPanelCornerRadius, Mathf.Clamp(value, 0, 10));
                MarkActiveThemeModified();
                InvalidateStyles();
            }
        }

        public static ThemePreset ActivePreset
        {
            get
            {
                int stored = UtilityWindowPrefs.GetInt(PrefPreset, (int)ThemePreset.PungentDefault);
                if (!Enum.IsDefined(typeof(ThemePreset), stored))
                    return ThemePreset.PungentDefault;
                return (ThemePreset)stored;
            }
            set => UtilityWindowPrefs.SetInt(PrefPreset, (int)value);
        }

        public static ThemeScope ActiveScope
        {
            get
            {
                int stored = UtilityWindowPrefs.GetInt(PrefScope, (int)ThemeScope.PungentFunkUtilitiesOnly);
                if (!Enum.IsDefined(typeof(ThemeScope), stored))
                    return ThemeScope.PungentFunkUtilitiesOnly;
                return (ThemeScope)stored;
            }
            set => UtilityWindowPrefs.SetInt(PrefScope, (int)value);
        }

        public static DeficiencyPreview ActiveDeficiencyPreview
        {
            get
            {
                int stored = UtilityWindowPrefs.GetInt(PrefDeficiencyPreview, 0);
                if (!Enum.IsDefined(typeof(DeficiencyPreview), stored))
                    return DeficiencyPreview.None;
                return (DeficiencyPreview)stored;
            }
            set => UtilityWindowPrefs.SetInt(PrefDeficiencyPreview, (int)value);
        }

        public static bool ActiveThemeModified => UtilityWindowPrefs.GetBool(PrefActiveThemeModified, false);

        public static string ActiveThemeDisplayName => GetPresetDisplayName(ActivePreset) + (ActiveThemeModified ? " — modified" : string.Empty);

        public static string ActiveThemeStatusLabel => ActiveThemeModified
            ? "Working copy — the selected preset definition is unchanged."
            : "Preset source — no working-copy edits.";

        public static void MarkActiveThemeModified()
        {
            UtilityWindowPrefs.SetBool(PrefActiveThemeModified, true);
        }

        public static void ClearActiveThemeModified()
        {
            UtilityWindowPrefs.SetBool(PrefActiveThemeModified, false);
        }

        public static void ResetActiveWorkingThemeToPreset()
        {
            ApplyPreset(ActivePreset);
        }
        public static GUIStyle TitleStyle { get { EnsureStyles(); return _titleStyle; } }
        public static GUIStyle SubtitleStyle { get { EnsureStyles(); return _subtitleStyle; } }
        public static GUIStyle SectionHeaderStyle { get { EnsureStyles(); return _sectionHeaderStyle; } }
        public static GUIStyle MutedMiniLabelStyle { get { EnsureStyles(); return _mutedMiniLabelStyle; } }
        public static GUIStyle CountPillStyle { get { EnsureStyles(); return _countPillStyle; } }
        public static GUIStyle ToolbarSearchStyle { get { EnsureStyles(); return _toolbarSearchStyle; } }
        public static GUIStyle CardLabelStyle { get { EnsureStyles(); return _cardLabelStyle; } }
        public static GUIStyle PathLabelStyle { get { EnsureStyles(); return _pathLabelStyle; } }
        public static GUIStyle BodyStyle { get { EnsureStyles(); return _bodyStyle; } }
        public static GUIStyle LinkStyle { get { EnsureStyles(); return _linkStyle; } }
        public static GUIStyle WelcomeHeroTitleStyle { get { EnsureStyles(); return _welcomeHeroTitleStyle; } }
        public static GUIStyle WelcomeSectionTitleStyle { get { EnsureStyles(); return _welcomeSectionTitleStyle; } }
        public static GUIStyle WelcomeBodyStyle { get { EnsureStyles(); return _welcomeBodyStyle; } }
        public static GUIStyle WelcomeMetaStyle { get { EnsureStyles(); return _welcomeMetaStyle; } }
        public static GUIStyle WelcomeStepLabelStyle { get { EnsureStyles(); return _welcomeStepLabelStyle; } }

        public static void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            float scale = TextScale;
            Font headingFont = GetFont(TextRole.Heading);
            Font subheadingFont = GetFont(TextRole.Subheading);
            Font bodyFont = GetFont(TextRole.Body);
            Font mutedFont = GetFont(TextRole.Muted);
            Font linkFont = GetFont(TextRole.Link);
            Font fieldFont = GetFont(TextRole.Field);
            Font pathFont = GetFont(TextRole.Path);
            Font codeFont = GetFont(TextRole.Code);

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TitleText }
            };
            ApplyFont(_titleStyle, headingFont);

            _subtitleStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                normal = { textColor = SubtitleText },
                margin = new RectOffset(2, 2, 0, ScaledInt(5))
            };
            ApplyFont(_subtitleStyle, subheadingFont);

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                normal = { textColor = TitleText },
                wordWrap = true
            };
            ApplyFont(_sectionHeaderStyle, subheadingFont);

            _bodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                normal = { textColor = CardText },
                wordWrap = true
            };
            ApplyFont(_bodyStyle, bodyFont);

            _mutedMiniLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = Mathf.RoundToInt(10f * scale),
                normal = { textColor = MutedText },
                wordWrap = true
            };
            ApplyFont(_mutedMiniLabelStyle, mutedFont);

            _linkStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = Mathf.RoundToInt(11f * scale),
                normal = { textColor = Cyan },
                hover = { textColor = GetReadableTextColor(Cyan) },
                wordWrap = false
            };
            ApplyFont(_linkStyle, linkFont);

            _countPillStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeBackgroundTexture(Color.white) },
                onNormal = { textColor = Color.white, background = MakeBackgroundTexture(Color.white) },
                padding = new RectOffset(ScaledInt(7), ScaledInt(7), ScaledInt(2), ScaledInt(2)),
                margin = new RectOffset(2, 2, 1, 1)
            };
            ApplyFont(_countPillStyle, mutedFont);

            _toolbarSearchStyle = GUI.skin != null
                ? new GUIStyle(GUI.skin.FindStyle("ToolbarSearchTextField") ?? GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.textField)
                : new GUIStyle(EditorStyles.textField);
            ApplyFont(_toolbarSearchStyle, fieldFont ?? bodyFont);

            _cardLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                normal = { textColor = CardText },
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            ApplyFont(_cardLabelStyle, bodyFont);

            _pathLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = Mathf.RoundToInt(10f * scale),
                normal = { textColor = PathText },
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            ApplyFont(_pathLabelStyle, pathFont ?? codeFont);

            _welcomeHeroTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = TitleText },
                wordWrap = true,
                margin = new RectOffset(2, 2, 0, ScaledInt(3))
            };
            ApplyFont(_welcomeHeroTitleStyle, headingFont);

            _welcomeSectionTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = TitleText },
                wordWrap = true,
                margin = new RectOffset(2, 2, ScaledInt(2), ScaledInt(2))
            };
            ApplyFont(_welcomeSectionTitleStyle, subheadingFont);

            _welcomeBodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = Mathf.RoundToInt(12f * scale),
                fontStyle = FontStyle.Normal,
                font = null,
                normal = { textColor = CardText },
                wordWrap = true,
                margin = new RectOffset(2, 2, ScaledInt(1), ScaledInt(2))
            };

            _welcomeMetaStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = Mathf.RoundToInt(10f * scale),
                fontStyle = FontStyle.Normal,
                font = null,
                normal = { textColor = MutedText },
                wordWrap = true,
                margin = new RectOffset(2, 2, ScaledInt(1), ScaledInt(2))
            };

            _welcomeStepLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = Mathf.RoundToInt(10f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = MutedText },
                wordWrap = false,
                margin = new RectOffset(2, ScaledInt(6), ScaledInt(2), 0)
            };
            ApplyFont(_welcomeStepLabelStyle, mutedFont);
        }

        public static GUIStyle PanelStyle(Color tint, float proAlpha = -1f, float personalAlpha = -1f, int padding = -1, int margin = 6)
        {
            EnsureStyles();

            int resolvedPadding = padding < 0 ? ScaledInt(8) : ScaledInt(padding);
            int resolvedMargin = ScaledInt(margin);
            int borderWidth = PanelBorderWidth;
            int radius = PanelCornerRadius;
            float borderOpacity = PanelBorderOpacity;
            float alpha = EditorGUIUtility.isProSkin
                ? (proAlpha >= 0f ? proAlpha : PanelAlphaDark)
                : (personalAlpha >= 0f ? personalAlpha : PanelAlphaLight);

            string key = $"{tint.r:0.000}:{tint.g:0.000}:{tint.b:0.000}:{alpha:0.000}:{resolvedPadding}:{resolvedMargin}:{borderWidth}:{radius}:{borderOpacity:0.000}:{EditorGUIUtility.isProSkin}";
            if (_tintedBoxStyleCache.TryGetValue(key, out GUIStyle cached))
                return cached;

            Color background = EditorGUIUtility.isProSkin
                ? new Color(tint.r, tint.g, tint.b, alpha)
                : new Color(tint.r, tint.g, tint.b, Mathf.Min(alpha * 0.70f, 0.18f));

            Color border = new Color(tint.r, tint.g, tint.b, borderOpacity);
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(resolvedPadding, resolvedPadding, resolvedPadding, resolvedPadding),
                margin = new RectOffset(resolvedMargin, resolvedMargin, resolvedMargin, resolvedMargin)
            };
            style.normal.background = MakePanelTexture(background, border, radius, borderWidth);
            if (radius > 0 || borderWidth > 0)
            {
                int borderInset = Mathf.Clamp(radius + borderWidth + 2, 2, 16);
                style.border = new RectOffset(borderInset, borderInset, borderInset, borderInset);
            }

            _tintedBoxStyleCache[key] = style;
            return style;
        }

        public static IDisposable Background(Color color)
        {
            return new GuiBackgroundScope(color);
        }

        public static bool TintedButton(string label, Color tint, params GUILayoutOption[] options)
        {
            return StudioButton(new GUIContent(label), tint, PungentButtonRole.Secondary, options);
        }

        public static bool StudioButton(GUIContent content, Color tint, PungentButtonRole role = PungentButtonRole.Secondary, params GUILayoutOption[] options)
        {
            EnsureStyles();

            bool disabled = role == PungentButtonRole.Disabled;
            using (new EditorGUI.DisabledScope(disabled))
                return GUILayout.Button(content ?? GUIContent.none, GetStudioButtonStyle(role, tint, false), options);
        }

        public static bool StyledButton(GUIContent content, PungentButtonRole role, params GUILayoutOption[] options)
        {
            EnsureStyles();

            GUIContent resolved = content ?? GUIContent.none;
            if (role == PungentButtonRole.Link)
                return LinkButton(resolved, options);

            Color tint = ResolveButtonTint(role);
            bool disabled = role == PungentButtonRole.Disabled;
            using (new EditorGUI.DisabledScope(disabled))
                return GUILayout.Button(resolved, GetStudioButtonStyle(role, tint, false), options);
        }

        public static bool ToolbarButton(GUIContent content, bool active, Color tint, params GUILayoutOption[] options)
        {
            EnsureStyles();
            return GUILayout.Button(content ?? GUIContent.none, GetStudioButtonStyle(PungentButtonRole.Toolbar, active ? tint : Neutral, active), options);
        }

        public static bool IconButton(GUIContent content, Color tint, params GUILayoutOption[] options)
        {
            EnsureStyles();
            return GUILayout.Button(content ?? GUIContent.none, GetStudioButtonStyle(PungentButtonRole.Subtle, tint, false), options);
        }

        public static bool LinkButton(GUIContent content, params GUILayoutOption[] options)
        {
            EnsureStyles();
            return GUILayout.Button(content ?? GUIContent.none, LinkStyle, options);
        }

        public static void DrawPopupChrome(Rect rect, Color tint)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            Color shadow = new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.28f : 0.14f);
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(0.11f, 0.11f, 0.12f, 0.98f)
                : new Color(0.86f, 0.86f, 0.88f, 0.98f);
            Color accent = new Color(tint.r, tint.g, tint.b, EditorGUIUtility.isProSkin ? 0.62f : 0.42f);

            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), shadow);
            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), accent);
        }

        public static void ToolbarToggle(ref bool value, string label, Color activeTint)
        {
            if (ToolbarButton(new GUIContent(label), value, activeTint))
                value = !value;
        }

        public static void CountPill(string text, Color tint, float width = 0f)
        {
            InfoPill(new GUIContent(text), tint, width);
        }

        public sealed class PillSpec
        {
            public GUIContent content;
            public Color tint;
            public Action action;
            public float width;

            public PillSpec(GUIContent content, Color tint, Action action = null, float width = 0f)
            {
                this.content = content ?? GUIContent.none;
                this.tint = tint;
                this.action = action;
                this.width = width;
            }
        }

        public static void InfoPill(GUIContent content, Color tint, float width = 0f)
        {
            EnsureStyles();
            using (Background(ReadablePillTint(tint)))
            {
                if (width > 0f)
                    GUILayout.Label(content ?? GUIContent.none, _countPillStyle, GUILayout.Width(width));
                else
                    GUILayout.Label(content ?? GUIContent.none, _countPillStyle);
            }
        }

        public static bool ActionPill(GUIContent content, Color tint, Action onClick, float width = 0f)
        {
            EnsureStyles();
            bool clicked;
            using (Background(ReadablePillTint(tint)))
            {
                clicked = width > 0f
                    ? GUILayout.Button(content ?? GUIContent.none, _countPillStyle, GUILayout.Width(width))
                    : GUILayout.Button(content ?? GUIContent.none, _countPillStyle);
            }

            if (clicked)
                onClick?.Invoke();
            return clicked;
        }

        public static void DrawWrappedPillGrid(IReadOnlyList<PillSpec> pills, float rowWidth)
        {
            if (pills == null || pills.Count == 0)
                return;

            float used = 0f;
            bool rowOpen = false;
            float available = Mathf.Max(180f, rowWidth);

            try
            {
                for (int i = 0; i < pills.Count; i++)
                {
                    PillSpec pill = pills[i];
                    if (pill == null)
                        continue;

                    string text = pill.content == null ? string.Empty : pill.content.text ?? string.Empty;
                    float width = pill.width > 0f ? pill.width : Mathf.Clamp(text.Length * 7f + 28f, 76f, 180f);
                    if (!rowOpen)
                    {
                        EditorGUILayout.BeginHorizontal();
                        rowOpen = true;
                        used = 0f;
                    }

                    if (used > 0f && used + width > available)
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        used = 0f;
                    }

                    if (pill.action == null)
                        InfoPill(pill.content, pill.tint, width);
                    else
                        ActionPill(pill.content, pill.tint, pill.action, width);
                    used += width + 4f;
                }
            }
            finally
            {
                if (rowOpen)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        public static void Header(string title, string subtitle, string status = null)
        {
            using (new EditorGUILayout.VerticalScope(PanelStyle(HeaderTint, 0.24f, 0.12f, 8, 6)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, TitleStyle);

                    GUILayout.FlexibleSpace();

                    if (!string.IsNullOrEmpty(status))
                        EditorGUILayout.LabelField(status, MutedMiniLabelStyle, GUILayout.MinWidth(160f));

                    PungentMinimizedUtilitiesButton.DrawHeaderPill();
                    PungentUtilityMinimizer.DrawHeaderMinimizeButton();
                }

                if (!string.IsNullOrEmpty(subtitle))
                    EditorGUILayout.LabelField(subtitle, SubtitleStyle);
            }
        }

        public static void UtilityToolbar(UtilityHeaderOptions options)
        {
            EnsureStyles();

            Color tint = options.Tint == default(Color) ? HeaderTint : options.Tint;
            string title = string.IsNullOrWhiteSpace(options.Title) ? "PungentFunk Utility" : options.Title.Trim();
            string status = FirstNonEmpty(options.CompactStatus, options.Status);
            GUIContent titleContent = new GUIContent(title, options.Description ?? string.Empty);

            using (new EditorGUILayout.HorizontalScope(PanelStyle(tint, 0.16f, 0.07f, 5, 4), GUILayout.MinHeight(28f)))
            {
                EditorGUILayout.LabelField(titleContent, TitleStyle, GUILayout.MinWidth(132f), GUILayout.MaxWidth(260f));

                if (!string.IsNullOrWhiteSpace(status))
                    EditorGUILayout.LabelField(new GUIContent(status, options.Status ?? status), MutedMiniLabelStyle, GUILayout.MinWidth(80f), GUILayout.MaxWidth(320f));

                GUILayout.FlexibleSpace();

                if (options.ShowHelp && HasHelpRoute(options))
                {
                    PungentFunk.Utilities.Editor.Core.Help.PungentUtilityHelpButton.Draw(
                        FirstNonEmpty(options.UtilityId, "help-browser"),
                        FirstNonEmpty(options.HelpSectionId, "overview"),
                        FirstNonEmpty(options.HelpTopicId, "overview"),
                        "Open contextual help.",
                        title + " toolbar");
                }

                if (options.ShowMinimizeTray)
                    PungentMinimizedUtilitiesButton.DrawHeaderPill();

                if (options.ShowMinimizeButton)
                    PungentUtilityMinimizer.DrawHeaderMinimizeButton();
            }
        }
        public static void SectionTitle(string title, Color tint, string pill = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(pill))
                    CountPill(pill, tint);
            }
        }

        public static string GetRoleDisplayName(string role)
        {
            switch (role)
            {
                case RolePrimary: return "Primary / Action";
                case RoleSecondary: return "Secondary / Link";
                case RoleTertiary: return "Tertiary / Support";
                case RoleSuccess: return "Success";
                case RoleWarning: return "Warning";
                case RoleDanger: return "Danger";
                case RoleAccentAlt: return "Alt Accent";
                case RoleNeutral: return "Neutral";
                case RoleHeader: return "Header Panel Tint";
                case RoleTitleText: return "Title Text";
                case RoleSubtitleText: return "Subtitle Text";
                case RoleMutedText: return "Muted Text";
                case RoleCardText: return "Card / Body Text";
                case RolePathText: return "Path / Mono Text";
                case RoleLightText: return "Light Text";
                case RoleDarkText: return "Dark Text";
                case RoleResizeHandle: return "Resize Handle";
                default: return ObjectNames.NicifyVariableName(role);
            }
        }

        private static GUIStyle GetStudioButtonStyle(PungentButtonRole role, Color tint, bool active)
        {
            EnsureStyles();

            Color baseTint = role == PungentButtonRole.Chip ? ReadablePillTint(tint) : tint;
            Color.RGBToHSV(baseTint, out float h, out float s, out float v);
            s = Mathf.Clamp(s, role == PungentButtonRole.Subtle || role == PungentButtonRole.Toolbar ? 0.18f : 0.34f, 0.78f);
            v = Mathf.Clamp(v, EditorGUIUtility.isProSkin ? 0.22f : 0.70f, EditorGUIUtility.isProSkin ? 0.46f : 0.92f);

            float alpha;
            switch (role)
            {
                case PungentButtonRole.Primary:
                case PungentButtonRole.Danger:
                case PungentButtonRole.Warning:
                    alpha = EditorGUIUtility.isProSkin ? 0.92f : 0.84f;
                    break;
                case PungentButtonRole.Toolbar:
                    alpha = active ? (EditorGUIUtility.isProSkin ? 0.82f : 0.72f) : (EditorGUIUtility.isProSkin ? 0.34f : 0.24f);
                    break;
                case PungentButtonRole.Chip:
                    alpha = 0.95f;
                    break;
                case PungentButtonRole.Disabled:
                    alpha = EditorGUIUtility.isProSkin ? 0.22f : 0.18f;
                    break;
                default:
                    alpha = EditorGUIUtility.isProSkin ? 0.48f : 0.34f;
                    break;
            }

            Color normal = Color.HSVToRGB(h, s, v);
            normal.a = alpha;
            Color hover = Color.HSVToRGB(h, Mathf.Min(0.88f, s + 0.08f), Mathf.Clamp01(v + (EditorGUIUtility.isProSkin ? 0.08f : 0.04f)));
            hover.a = Mathf.Clamp01(alpha + 0.08f);
            Color pressed = Color.HSVToRGB(h, Mathf.Min(0.90f, s + 0.12f), Mathf.Clamp01(v - 0.06f));
            pressed.a = Mathf.Clamp01(alpha + 0.12f);
            Color border = new Color(baseTint.r, baseTint.g, baseTint.b, role == PungentButtonRole.Subtle || role == PungentButtonRole.Toolbar ? 0.28f : 0.44f);
            string key = $"button:{role}:{active}:{EditorGUIUtility.isProSkin}:{normal.r:0.000}:{normal.g:0.000}:{normal.b:0.000}:{normal.a:0.000}:{PanelCornerRadius}:{TextScale:0.00}";
            if (_buttonStyleCache.TryGetValue(key, out GUIStyle cached))
                return cached;

            GUIStyle source = role == PungentButtonRole.Toolbar ? EditorStyles.miniLabel : EditorStyles.label;
            var style = new GUIStyle(source)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontSize = Mathf.RoundToInt(11f * TextScale),
                fontStyle = role == PungentButtonRole.Primary || active ? FontStyle.Bold : FontStyle.Normal,
                padding = new RectOffset(ScaledInt(8), ScaledInt(8), ScaledInt(2), ScaledInt(2)),
                margin = new RectOffset(2, 2, 1, 1),
                border = new RectOffset(8, 8, 8, 8)
            };

            style.normal.background = MakePanelTexture(normal, border, Mathf.Clamp(PanelCornerRadius, 2, 8), 1);
            style.hover.background = MakePanelTexture(hover, border, Mathf.Clamp(PanelCornerRadius, 2, 8), 1);
            style.active.background = MakePanelTexture(pressed, border, Mathf.Clamp(PanelCornerRadius, 2, 8), 1);
            style.focused.background = style.hover.background;
            style.onNormal.background = style.normal.background;
            style.onHover.background = style.hover.background;
            style.onActive.background = style.active.background;
            style.onFocused.background = style.focused.background;

            Color text = role == PungentButtonRole.Primary || role == PungentButtonRole.Danger || role == PungentButtonRole.Warning || active
                ? GetReadableTextColor(baseTint)
                : CardText;
            if (role == PungentButtonRole.Disabled)
                text = MutedText;
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            style.focused.textColor = text;
            style.onNormal.textColor = text;
            style.onHover.textColor = text;
            style.onActive.textColor = text;
            style.onFocused.textColor = text;

            _buttonStyleCache[key] = style;
            return style;
        }

        private static Color ResolveButtonTint(PungentButtonRole role)
        {
            switch (role)
            {
                case PungentButtonRole.Primary:
                    return Blue;
                case PungentButtonRole.Secondary:
                    return Teal;
                case PungentButtonRole.Danger:
                    return Red;
                case PungentButtonRole.Warning:
                    return Amber;
                case PungentButtonRole.Chip:
                    return Cyan;
                case PungentButtonRole.Toolbar:
                case PungentButtonRole.Disabled:
                case PungentButtonRole.Subtle:
                default:
                    return Neutral;
            }
        }

        private static bool HasHelpRoute(UtilityHeaderOptions options)
        {
            return !string.IsNullOrWhiteSpace(options.UtilityId) ||
                   !string.IsNullOrWhiteSpace(options.HelpSectionId) ||
                   !string.IsNullOrWhiteSpace(options.HelpTopicId);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i].Trim();
            }

            return string.Empty;
        }
        public static string GetPresetDisplayName(ThemePreset preset)
        {
            return UtilityThemePresetLibrary.Get(preset).DisplayName;
        }

        public static Color GetColor(string role)
        {
            return UtilityWindowPrefs.GetColor(ColorPrefKey(role), GetPresetColor(ActivePreset, role));
        }

        public static void SetColor(string role, Color color)
        {
            UtilityWindowPrefs.SetColor(ColorPrefKey(role), ClampColor(color));
            MarkActiveThemeModified();
            InvalidateStyles();
        }

        public static void ApplyPreset(ThemePreset preset)
        {
            UtilityThemePresetDefinition definition = UtilityThemePresetLibrary.Get(preset);
            ActivePreset = definition.Preset;
            foreach (string role in EditableColorRoles)
                UtilityWindowPrefs.SetColor(ColorPrefKey(role), definition.GetColor(role, GetPresetColor(ThemePreset.PungentDefault, role)));

            UtilityWindowPrefs.SetFloat(PrefPanelAlphaDark, definition.PanelAlphaDark);
            UtilityWindowPrefs.SetFloat(PrefPanelAlphaLight, definition.PanelAlphaLight);
            UtilityWindowPrefs.SetFloat(PrefPanelBorderOpacity, definition.PanelBorderOpacity);
            UtilityWindowPrefs.SetInt(PrefPanelBorderWidth, definition.PanelBorderWidth);
            UtilityWindowPrefs.SetInt(PrefPanelCornerRadius, definition.PanelCornerRadius);
            UtilityWindowPrefs.SetFloat(PrefDensity, definition.Density);
            ApplyPresetFonts(definition);
            ClearActiveThemeModified();

            InvalidateStyles();
            RepaintUtilityWindows();
            if (PungentEditorSkinBridge.ExperimentalBridgeEnabled)
                PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
        }


        private static void ApplyPresetFonts(UtilityThemePresetDefinition definition)
        {
            if (definition == null || definition.FontHints == null)
                return;

            foreach (TextRole role in Enum.GetValues(typeof(TextRole)))
            {
                Font font = ResolveLicensedPresetFont(definition.GetFontHints(role));
                UtilityWindowPrefs.SetString(FontPrefKey(role), font == null ? string.Empty : AssetDatabase.GetAssetPath(font));
            }
        }

        public static void ApplyFontHints(IReadOnlyDictionary<TextRole, string[]> fontHints)
        {
            if (fontHints == null)
                return;

            foreach (TextRole role in Enum.GetValues(typeof(TextRole)))
            {
                if (!fontHints.TryGetValue(role, out string[] hints) || hints == null || hints.Length == 0)
                    continue;

                Font font = ResolveLicensedPresetFont(hints);
                if (font != null)
                    SetFont(role, font);
            }

            InvalidateStyles();
            RepaintUtilityWindows();
        }

        private static Font ResolveLicensedPresetFont(string[] hints)
        {
            if (hints == null || hints.Length == 0)
                return null;

            string[] guids = AssetDatabase.FindAssets("t:Font");
            for (int h = 0; h < hints.Length; h++)
            {
                string hint = hints[h];
                if (string.IsNullOrWhiteSpace(hint))
                    continue;

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    string fileName = Path.GetFileNameWithoutExtension(path);
                    if (fileName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!LooksCommerciallySafeFontFolder(path))
                        continue;

                    Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
                    if (font != null)
                        return font;
                }
            }

            return null;
        }

        private static bool LooksCommerciallySafeFontFolder(string assetPath)
        {
            string absolute = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
            string directory = Path.GetDirectoryName(absolute);
            for (int depth = 0; depth < 4 && !string.IsNullOrEmpty(directory); depth++)
            {
                if (Directory.Exists(directory))
                {
                    string[] files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string name = Path.GetFileName(files[i]).ToLowerInvariant();
                        if (!name.Contains("license") && !name.Contains("ofl") && !name.Contains("readme") && !name.Contains("notice"))
                            continue;

                        try
                        {
                            string text = File.ReadAllText(files[i]).ToLowerInvariant();
                            if (LooksLikePermissiveFontLicense(text))
                                return true;
                        }
                        catch
                        {
                            // Non-text readme/license assets are ignored; only clearly readable free/open font licenses are accepted.
                        }
                    }
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            return false;
        }


        private static bool LooksLikePermissiveFontLicense(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            text = text.ToLowerInvariant();

            return text.Contains("sil open font license") ||
                   text.Contains("open font license") ||
                   text.Contains("ofl") ||
                   text.Contains("apache license") ||
                   text.Contains("apache license, version 2.0") ||
                   text.Contains("ubuntu font licence") ||
                   text.Contains("ubuntu font license") ||
                   text.Contains("mit license") ||
                   text.Contains("bsd license") ||
                   text.Contains("permission is hereby granted, free of charge") ||
                   text.Contains("1001fonts free for commercial use") ||
                   text.Contains("free for commercial use") ||
                   text.Contains("free for personal and commercial use") ||
                   text.Contains("commercial-free use") ||
                   text.Contains("ok for commercial use") ||
                   text.Contains("commercial use permitted") ||
                   text.Contains("permits commercial use") ||
                   text.Contains("creative commons attribution-sharealike 4.0") ||
                   text.Contains("cc-by-sa 4.0") ||
                   text.Contains("cc by-sa 4.0") ||
                   text.Contains("design science license") ||
                   text.Contains("design science licence");
        }

        public static void InvalidateStyles()
        {
            _titleStyle = null;
            _subtitleStyle = null;
            _sectionHeaderStyle = null;
            _mutedMiniLabelStyle = null;
            _countPillStyle = null;
            _toolbarSearchStyle = null;
            _cardLabelStyle = null;
            _pathLabelStyle = null;
            _bodyStyle = null;
            _linkStyle = null;
            _tintedBoxStyleCache.Clear();
        }

        public static void RepaintUtilityWindows()
        {
            if (_repaintQueued)
                return;

            _repaintQueued = true;
            EditorApplication.delayCall += () =>
            {
                _repaintQueued = false;
                EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                for (int i = 0; i < windows.Length; i++)
                {
                    EditorWindow window = windows[i];
                    if (window == null)
                        continue;

                    string fullName = window.GetType().FullName ?? string.Empty;
                    if (!fullName.StartsWith("PungentFunk.Utilities.Editor.", StringComparison.Ordinal))
                        continue;

                    window.Repaint();
                }
            };
        }

        public static Font GetFont(TextRole role)
        {
            string path = UtilityWindowPrefs.GetString(FontPrefKey(role), string.Empty);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        public static void SetFont(TextRole role, Font font)
        {
            string path = font == null ? string.Empty : AssetDatabase.GetAssetPath(font);
            UtilityWindowPrefs.SetString(FontPrefKey(role), path);
            MarkActiveThemeModified();
            InvalidateStyles();
            RepaintUtilityWindows();
        }

        public static Color GetReadableTextColor(Color background)
        {
            return GetRelativeLuminance(background) > 0.5f ? Color.black : Color.white;
        }

        public static float GetRelativeLuminance(Color color)
        {
            float Linear(float x) => x <= 0.03928f ? x / 12.92f : Mathf.Pow((x + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        public static float GetContrastRatio(Color a, Color b)
        {
            float l1 = GetRelativeLuminance(a);
            float l2 = GetRelativeLuminance(b);
            return (Mathf.Max(l1, l2) + 0.05f) / (Mathf.Min(l1, l2) + 0.05f);
        }

        public static string GetContrastRating(float contrast)
        {
            if (contrast >= 7f)
                return "AAA";
            if (contrast >= 4.5f)
                return "AA";
            if (contrast >= 3f)
                return "Large AA";
            return "Fail";
        }

        public static Color SimulateDeficiency(DeficiencyPreview type, Color color)
        {
            switch (type)
            {
                case DeficiencyPreview.Protanopia:
                    return Matrix(color, 0.567f, 0.433f, 0f, 0.558f, 0.442f, 0f, 0f, 0.242f, 0.758f);
                case DeficiencyPreview.Deuteranopia:
                    return Matrix(color, 0.625f, 0.375f, 0f, 0.70f, 0.30f, 0f, 0f, 0.30f, 0.70f);
                case DeficiencyPreview.Tritanopia:
                    return Matrix(color, 0.95f, 0.05f, 0f, 0f, 0.433f, 0.567f, 0f, 0.475f, 0.525f);
                case DeficiencyPreview.Achromatopsia:
                    return ToGray(color);
                default:
                    return color;
            }
        }

        public static Color ImproveContrast(Color foreground, Color background, float targetContrast)
        {
            foreground = ClampColor(foreground);
            background = ClampColor(background);
            if (GetContrastRatio(foreground, background) >= targetContrast)
                return foreground;

            Color towardWhite = foreground;
            Color towardBlack = foreground;
            for (int i = 0; i < 24; i++)
            {
                float t = (i + 1) / 24f;
                towardWhite = Color.Lerp(foreground, Color.white, t);
                towardBlack = Color.Lerp(foreground, Color.black, t);

                float whiteRatio = GetContrastRatio(towardWhite, background);
                float blackRatio = GetContrastRatio(towardBlack, background);
                if (whiteRatio >= targetContrast || blackRatio >= targetContrast)
                    return whiteRatio >= blackRatio ? towardWhite : towardBlack;
            }

            return GetContrastRatio(Color.white, background) >= GetContrastRatio(Color.black, background) ? Color.white : Color.black;
        }

        public static void VerticalResizeHandle(ref float height, float minHeight, float maxHeight, Action onChanged = null, string tooltip = "Drag to resize this section")
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 9f, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            DrawVerticalResizeHandle(rect, tooltip);

            Event evt = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(evt.mousePosition) && evt.button == 0)
                    {
                        GUIUtility.hotControl = id;
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        height = Mathf.Clamp(height + evt.delta.y, minHeight, maxHeight);
                        onChanged?.Invoke();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
            }
        }

        public static void HorizontalResizeHandle(ref float width, float minWidth, float maxWidth, Action onChanged = null, string tooltip = "Drag to resize these panels", bool invertDelta = false)
        {
            Rect rect = GUILayoutUtility.GetRect(9f, 1f, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            DrawHorizontalResizeHandle(rect, tooltip);

            Event evt = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(evt.mousePosition) && evt.button == 0)
                    {
                        GUIUtility.hotControl = id;
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        width = Mathf.Clamp(width + (invertDelta ? -evt.delta.x : evt.delta.x), minWidth, maxWidth);
                        onChanged?.Invoke();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
            }
        }

        private static string ColorPrefKey(string role) => PrefPrefix + "Color." + role;
        private static string FontPrefKey(TextRole role) => PrefPrefix + "Font." + role;

        private static Color GetPresetColor(ThemePreset preset, string role)
        {
            Color fallback;
            if (role == RoleLightText)
                fallback = new Color(0.94f, 0.96f, 0.99f, 1f);
            else if (role == RoleDarkText)
                fallback = new Color(0.07f, 0.08f, 0.10f, 1f);
            else if (role == RoleTitleText || role == RoleCardText)
                fallback = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            else
                fallback = Color.gray;
            return UtilityThemePresetLibrary.GetPresetColor(preset, role, fallback);
        }

        private static int ScaledInt(int value)
        {
            return Mathf.Max(0, Mathf.RoundToInt(value * Density));
        }

        private static void ApplyFont(GUIStyle style, Font font)
        {
            if (style != null && font != null)
                style.font = font;
        }

        private static Color ClampColor(Color color)
        {
            color.r = Mathf.Clamp01(color.r);
            color.g = Mathf.Clamp01(color.g);
            color.b = Mathf.Clamp01(color.b);
            color.a = Mathf.Clamp01(color.a <= 0f ? 1f : color.a);
            return color;
        }

        private static Color ToGray(Color color)
        {
            float gray = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            return new Color(gray, gray, gray, color.a);
        }

        private static Color Matrix(Color color, float rr, float rg, float rb, float gr, float gg, float gb, float br, float bg, float bb)
        {
            return new Color(
                Mathf.Clamp01(color.r * rr + color.g * rg + color.b * rb),
                Mathf.Clamp01(color.r * gr + color.g * gg + color.b * gb),
                Mathf.Clamp01(color.r * br + color.g * bg + color.b * bb),
                color.a);
        }

        private static void DrawVerticalResizeHandle(Rect rect, string tooltip)
        {
            Color line = ResizeHandleTint;
            Rect lineRect = new Rect(rect.x + 8f, rect.center.y - 0.5f, Mathf.Max(1f, rect.width - 16f), 1f);
            Rect gripRect = new Rect(rect.center.x - 18f, rect.center.y - 1.5f, 36f, 3f);
            EditorGUI.DrawRect(lineRect, new Color(line.r, line.g, line.b, line.a * 0.45f));
            EditorGUI.DrawRect(gripRect, line);

            if (!string.IsNullOrEmpty(tooltip))
                GUI.Label(rect, new GUIContent(string.Empty, tooltip));
        }

        private static void DrawHorizontalResizeHandle(Rect rect, string tooltip)
        {
            Color line = ResizeHandleTint;
            Rect lineRect = new Rect(rect.center.x - 0.5f, rect.y + 8f, 1f, Mathf.Max(1f, rect.height - 16f));
            Rect gripRect = new Rect(rect.center.x - 1.5f, rect.center.y - 18f, 3f, 36f);
            EditorGUI.DrawRect(lineRect, new Color(line.r, line.g, line.b, line.a * 0.45f));
            EditorGUI.DrawRect(gripRect, line);

            if (!string.IsNullOrEmpty(tooltip))
                GUI.Label(rect, new GUIContent(string.Empty, tooltip));
        }

        private static Color ReadablePillTint(Color tint)
        {
            Color.RGBToHSV(tint, out float h, out float s, out float v);
            s = Mathf.Clamp(s, 0.35f, 0.78f);
            v = Mathf.Clamp(v, EditorGUIUtility.isProSkin ? 0.26f : 0.22f, EditorGUIUtility.isProSkin ? 0.48f : 0.40f);
            Color result = Color.HSVToRGB(h, s, v);
            result.a = 1f;
            return result;
        }

        private static Texture2D MakeBackgroundTexture(Color color)
        {
            string key = $"flat:{color.r:0.000}:{color.g:0.000}:{color.b:0.000}:{color.a:0.000}";
            if (_backgroundTextureCache.TryGetValue(key, out Texture2D cached) && cached != null)
                return cached;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            _backgroundTextureCache[key] = tex;
            return tex;
        }

        private static Texture2D MakePanelTexture(Color background, Color border, int radius, int borderWidth)
        {
            if (borderWidth <= 0 && radius <= 0)
                return MakeBackgroundTexture(background);

            int size = 64;
            string key = $"panel:{background.r:0.000}:{background.g:0.000}:{background.b:0.000}:{background.a:0.000}:{border.r:0.000}:{border.g:0.000}:{border.b:0.000}:{border.a:0.000}:{radius}:{borderWidth}";
            if (_backgroundTextureCache.TryGetValue(key, out Texture2D cached) && cached != null)
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            float r = Mathf.Clamp(radius, 0, size / 2 - 1);
            float innerR = Mathf.Max(0f, r - borderWidth);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = InsideRoundedRect(x + 0.5f, y + 0.5f, size, size, r);
                    if (!inside)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    bool inner = borderWidth <= 0 || InsideRoundedRect(x + 0.5f, y + 0.5f, size - borderWidth * 2, size - borderWidth * 2, innerR, borderWidth, borderWidth);
                    tex.SetPixel(x, y, inner ? background : border);
                }
            }

            tex.Apply();
            _backgroundTextureCache[key] = tex;
            return tex;
        }

        private static bool InsideRoundedRect(float x, float y, float width, float height, float radius, float offsetX = 0f, float offsetY = 0f)
        {
            x -= offsetX;
            y -= offsetY;
            if (radius <= 0f)
                return x >= 0f && y >= 0f && x <= width && y <= height;

            float clampedX = Mathf.Clamp(x, radius, width - radius);
            float clampedY = Mathf.Clamp(y, radius, height - radius);
            float dx = x - clampedX;
            float dy = y - clampedY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private struct GuiBackgroundScope : IDisposable
        {
            private readonly Color _previous;

            public GuiBackgroundScope(Color color)
            {
                _previous = GUI.backgroundColor;
                GUI.backgroundColor = color;
            }

            public void Dispose()
            {
                GUI.backgroundColor = _previous;
            }
        }
    }
#endif
}
