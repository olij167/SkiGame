namespace PungentFunk.Utilities.Editor.Theme
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Shared IMGUI styling helpers for generic project utilities.
    /// The active colour set is stored in EditorPrefs so every utility window can share one editable theme.
    /// </summary>
    public static class UtilityWindowTheme
    {
        public enum ThemePreset
        {
            DebugControl,
            OceanGlass,
            Graphite,
            ForestNight,
            WarmSlate,
            HighContrastDark,
            CleanLight
        }

        public enum DeficiencyPreview
        {
            None,
            Protanopia,
            Deuteranopia,
            Tritanopia,
            Achromatopsia
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
        public const string RoleResizeHandle = "resizeHandle";

        private const string PrefPrefix = "GenericUtility.WindowTheme.";
        private const string PrefPreset = PrefPrefix + "Preset";
        private const string PrefDeficiencyPreview = PrefPrefix + "DeficiencyPreview";
        private const string PrefPanelAlphaDark = PrefPrefix + "PanelAlphaDark";
        private const string PrefPanelAlphaLight = PrefPrefix + "PanelAlphaLight";

        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _mutedMiniLabelStyle;
        private static GUIStyle _countPillStyle;
        private static GUIStyle _toolbarSearchStyle;
        private static GUIStyle _cardLabelStyle;
        private static GUIStyle _pathLabelStyle;
        private static readonly Dictionary<string, GUIStyle> _tintedBoxStyleCache = new();
        private static readonly Dictionary<string, Texture2D> _backgroundTextureCache = new();

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
        public static Color ResizeHandleTint => GetColor(RoleResizeHandle);

        public static float PanelAlphaDark
        {
            get => Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefPanelAlphaDark, 0.22f));
            set
            {
                UtilityWindowPrefs.SetFloat(PrefPanelAlphaDark, Mathf.Clamp01(value));
                InvalidateStyles();
            }
        }

        public static float PanelAlphaLight
        {
            get => Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefPanelAlphaLight, 0.12f));
            set
            {
                UtilityWindowPrefs.SetFloat(PrefPanelAlphaLight, Mathf.Clamp01(value));
                InvalidateStyles();
            }
        }

        public static ThemePreset ActivePreset
        {
            get => (ThemePreset)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefPreset, (int)ThemePreset.DebugControl), 0, Enum.GetValues(typeof(ThemePreset)).Length - 1);
            set => UtilityWindowPrefs.SetInt(PrefPreset, (int)value);
        }

        public static DeficiencyPreview ActiveDeficiencyPreview
        {
            get => (DeficiencyPreview)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefDeficiencyPreview, 0), 0, Enum.GetValues(typeof(DeficiencyPreview)).Length - 1);
            set => UtilityWindowPrefs.SetInt(PrefDeficiencyPreview, (int)value);
        }

        public static GUIStyle TitleStyle
        {
            get
            {
                EnsureStyles();
                return _titleStyle;
            }
        }

        public static GUIStyle SubtitleStyle
        {
            get
            {
                EnsureStyles();
                return _subtitleStyle;
            }
        }

        public static GUIStyle SectionHeaderStyle
        {
            get
            {
                EnsureStyles();
                return _sectionHeaderStyle;
            }
        }

        public static GUIStyle MutedMiniLabelStyle
        {
            get
            {
                EnsureStyles();
                return _mutedMiniLabelStyle;
            }
        }

        public static GUIStyle CountPillStyle
        {
            get
            {
                EnsureStyles();
                return _countPillStyle;
            }
        }

        public static GUIStyle ToolbarSearchStyle
        {
            get
            {
                EnsureStyles();
                return _toolbarSearchStyle;
            }
        }

        public static GUIStyle CardLabelStyle
        {
            get
            {
                EnsureStyles();
                return _cardLabelStyle;
            }
        }

        public static GUIStyle PathLabelStyle
        {
            get
            {
                EnsureStyles();
                return _pathLabelStyle;
            }
        }

        public static void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TitleText }
            };

            _subtitleStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                normal = { textColor = SubtitleText },
                margin = new RectOffset(2, 2, 0, 5)
            };

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = TitleText },
                wordWrap = true
            };

            _mutedMiniLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = MutedText },
                wordWrap = true
            };

            _countPillStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeBackgroundTexture(Color.white) },
                onNormal = { textColor = Color.white, background = MakeBackgroundTexture(Color.white) },
                padding = new RectOffset(7, 7, 2, 2),
                margin = new RectOffset(2, 2, 1, 1)
            };

            _toolbarSearchStyle = GUI.skin != null
                ? new GUIStyle(GUI.skin.FindStyle("ToolbarSearchTextField") ?? GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.textField)
                : new GUIStyle(EditorStyles.textField);

            _cardLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = CardText },
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _pathLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = PathText },
                wordWrap = false,
                clipping = TextClipping.Clip
            };
        }

        public static GUIStyle PanelStyle(Color tint, float proAlpha = -1f, float personalAlpha = -1f, int padding = 8, int margin = 6)
        {
            EnsureStyles();

            float alpha = EditorGUIUtility.isProSkin
                ? (proAlpha >= 0f ? proAlpha : PanelAlphaDark)
                : (personalAlpha >= 0f ? personalAlpha : PanelAlphaLight);

            string key = $"{tint.r:0.000}:{tint.g:0.000}:{tint.b:0.000}:{alpha:0.000}:{padding}:{margin}:{EditorGUIUtility.isProSkin}";
            if (_tintedBoxStyleCache.TryGetValue(key, out GUIStyle cached))
                return cached;

            Color background = EditorGUIUtility.isProSkin
                ? new Color(tint.r, tint.g, tint.b, alpha)
                : new Color(tint.r, tint.g, tint.b, Mathf.Min(alpha * 0.70f, 0.18f));

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(padding, padding, padding, padding),
                margin = new RectOffset(margin, margin, margin, margin)
            };
            style.normal.background = MakeBackgroundTexture(background);
            _tintedBoxStyleCache[key] = style;
            return style;
        }

        public static IDisposable Background(Color color)
        {
            return new GuiBackgroundScope(color);
        }

        public static bool TintedButton(string label, Color tint, params GUILayoutOption[] options)
        {
            using (Background(tint))
                return GUILayout.Button(label, options);
        }

        public static void ToolbarToggle(ref bool value, string label, Color activeTint)
        {
            Color inactive = EditorGUIUtility.isProSkin ? new Color(0.42f, 0.43f, 0.48f) : new Color(0.75f, 0.76f, 0.80f);
            using (Background(value ? activeTint : inactive))
                value = GUILayout.Toggle(value, label, EditorStyles.toolbarButton);
        }

        public static void CountPill(string text, Color tint, float width = 0f)
        {
            EnsureStyles();
            using (Background(ReadablePillTint(tint)))
            {
                if (width > 0f)
                    GUILayout.Label(text, _countPillStyle, GUILayout.Width(width));
                else
                    GUILayout.Label(text, _countPillStyle);
            }
        }

        public static void Header(string title, string subtitle, string status = null)
        {
            using (new EditorGUILayout.VerticalScope(PanelStyle(HeaderTint, 0.24f, 0.12f, 8, 6)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, TitleStyle);
                    if (!string.IsNullOrEmpty(status))
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(status, MutedMiniLabelStyle, GUILayout.MinWidth(160f));
                    }
                }

                if (!string.IsNullOrEmpty(subtitle))
                    EditorGUILayout.LabelField(subtitle, SubtitleStyle);
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
            return role switch
            {
                RolePrimary => "Primary / Blue",
                RoleSecondary => "Secondary / Cyan",
                RoleTertiary => "Tertiary / Teal",
                RoleSuccess => "Success / Green",
                RoleWarning => "Warning / Amber",
                RoleDanger => "Danger / Red",
                RoleAccentAlt => "Alt Accent / Purple",
                RoleNeutral => "Neutral",
                RoleHeader => "Header Panel Tint",
                RoleTitleText => "Title Text",
                RoleSubtitleText => "Subtitle Text",
                RoleMutedText => "Muted Text",
                RoleCardText => "Card Text",
                RolePathText => "Path Text",
                RoleResizeHandle => "Resize Handle",
                _ => ObjectNames.NicifyVariableName(role)
            };
        }

        public static Color GetColor(string role)
        {
            return UtilityWindowPrefs.GetColor(ColorPrefKey(role), GetPresetColor(ActivePreset, role));
        }

        public static void SetColor(string role, Color color)
        {
            UtilityWindowPrefs.SetColor(ColorPrefKey(role), ClampColor(color));
            InvalidateStyles();
        }

        public static void ApplyPreset(ThemePreset preset)
        {
            ActivePreset = preset;
            foreach (string role in EditableColorRoles)
                UtilityWindowPrefs.SetColor(ColorPrefKey(role), GetPresetColor(preset, role));

            switch (preset)
            {
                case ThemePreset.HighContrastDark:
                    UtilityWindowPrefs.SetFloat(PrefPanelAlphaDark, 0.34f);
                    UtilityWindowPrefs.SetFloat(PrefPanelAlphaLight, 0.18f);
                    break;
                case ThemePreset.CleanLight:
                    UtilityWindowPrefs.SetFloat(PrefPanelAlphaDark, 0.26f);
                    UtilityWindowPrefs.SetFloat(PrefPanelAlphaLight, 0.14f);
                    break;
                default:
                    UtilityWindowPrefs.SetFloat(PrefPanelAlphaDark, 0.22f);
                    UtilityWindowPrefs.SetFloat(PrefPanelAlphaLight, 0.12f);
                    break;
            }

            InvalidateStyles();
            RepaintUtilityWindows();
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
            _tintedBoxStyleCache.Clear();
        }

        public static void RepaintUtilityWindows()
        {
            EditorApplication.delayCall += () =>
            {
                EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (EditorWindow window in windows)
                    window.Repaint();
            };
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
            Color result = type switch
            {
                DeficiencyPreview.Protanopia => new Color(
                    0.566f * color.r + 0.433f * color.g,
                    0.558f * color.r + 0.442f * color.g,
                    0.242f * color.g + 0.758f * color.b,
                    color.a),
                DeficiencyPreview.Deuteranopia => new Color(
                    0.625f * color.r + 0.375f * color.g,
                    0.700f * color.r + 0.300f * color.g,
                    0.300f * color.g + 0.700f * color.b,
                    color.a),
                DeficiencyPreview.Tritanopia => new Color(
                    0.950f * color.r + 0.050f * color.g,
                    0.433f * color.g + 0.567f * color.b,
                    0.475f * color.g + 0.525f * color.b,
                    color.a),
                DeficiencyPreview.Achromatopsia => ToGray(color),
                _ => color
            };

            result.r = Mathf.Clamp01(result.r);
            result.g = Mathf.Clamp01(result.g);
            result.b = Mathf.Clamp01(result.b);
            result.a = color.a;
            return result;
        }

        public static Color ImproveContrast(Color foreground, Color background, float targetContrast)
        {
            Color result = foreground;
            bool lighten = GetRelativeLuminance(foreground) >= GetRelativeLuminance(background);

            for (int i = 0; i < 24 && GetContrastRatio(result, background) < targetContrast; i++)
            {
                Color.RGBToHSV(result, out float h, out float s, out float v);
                if (lighten)
                    v = Mathf.Clamp01(v + 0.045f);
                else
                    v = Mathf.Clamp01(v - 0.045f);

                if (v <= 0.02f || v >= 0.98f)
                    s = Mathf.Clamp01(s - 0.035f);

                result = Color.HSVToRGB(h, s, v);
                result.a = foreground.a;
            }

            if (GetContrastRatio(result, background) < targetContrast)
            {
                Color white = Color.white;
                Color black = Color.black;
                result = GetContrastRatio(white, background) >= GetContrastRatio(black, background) ? white : black;
                result.a = foreground.a;
            }

            return result;
        }

        public static void VerticalResizeHandle(ref float height, float minHeight, float maxHeight, Action onChanged = null, string tooltip = "Drag to resize this section")
        {
            maxHeight = Mathf.Max(minHeight, maxHeight);
            height = Mathf.Clamp(height, minHeight, maxHeight);

            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(7f), GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

            int controlId = GUIUtility.GetControlID("UtilityVerticalResizeHandle".GetHashCode(), FocusType.Passive, rect);
            Event evt = Event.current;

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        float previous = height;
                        height = Mathf.Clamp(height + evt.delta.y, minHeight, maxHeight);
                        if (!Mathf.Approximately(previous, height))
                        {
                            onChanged?.Invoke();
                            GUI.changed = true;
                        }
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        onChanged?.Invoke();
                        evt.Use();
                    }
                    break;

                case EventType.Repaint:
                    DrawVerticalResizeHandle(rect, tooltip);
                    break;
            }
        }

        public static void HorizontalResizeHandle(ref float width, float minWidth, float maxWidth, Action onChanged = null, string tooltip = "Drag to resize these panels")
        {
            maxWidth = Mathf.Max(minWidth, maxWidth);
            width = Mathf.Clamp(width, minWidth, maxWidth);

            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Width(7f), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            int controlId = GUIUtility.GetControlID("UtilityHorizontalResizeHandle".GetHashCode(), FocusType.Passive, rect);
            Event evt = Event.current;

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        float previous = width;
                        width = Mathf.Clamp(width + evt.delta.x, minWidth, maxWidth);
                        if (!Mathf.Approximately(previous, width))
                        {
                            onChanged?.Invoke();
                            GUI.changed = true;
                        }
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        onChanged?.Invoke();
                        evt.Use();
                    }
                    break;

                case EventType.Repaint:
                    DrawHorizontalResizeHandle(rect, tooltip);
                    break;
            }
        }

        private static string ColorPrefKey(string role) => PrefPrefix + "Color." + role;

        private static Color GetPresetColor(ThemePreset preset, string role)
        {
            return preset switch
            {
                ThemePreset.OceanGlass => role switch
                {
                    RolePrimary => new Color(0.10f, 0.55f, 0.95f),
                    RoleSecondary => new Color(0.25f, 0.82f, 1.00f),
                    RoleTertiary => new Color(0.18f, 0.88f, 0.75f),
                    RoleSuccess => new Color(0.35f, 0.90f, 0.55f),
                    RoleWarning => new Color(1.00f, 0.72f, 0.28f),
                    RoleDanger => new Color(1.00f, 0.38f, 0.32f),
                    RoleAccentAlt => new Color(0.66f, 0.48f, 1.00f),
                    RoleNeutral => new Color(0.56f, 0.64f, 0.70f),
                    RoleHeader => new Color(0.05f, 0.36f, 0.54f),
                    RoleTitleText => new Color(0.76f, 0.96f, 1.00f),
                    RoleSubtitleText => new Color(0.70f, 0.82f, 0.88f),
                    RoleMutedText => new Color(0.62f, 0.72f, 0.78f),
                    RoleCardText => new Color(0.80f, 0.95f, 1.00f),
                    RolePathText => new Color(0.64f, 0.74f, 0.80f),
                    RoleResizeHandle => new Color(0.48f, 0.88f, 1.00f, 0.60f),
                    _ => Color.white
                },
                ThemePreset.Graphite => role switch
                {
                    RolePrimary => new Color(0.48f, 0.62f, 0.82f),
                    RoleSecondary => new Color(0.62f, 0.72f, 0.84f),
                    RoleTertiary => new Color(0.42f, 0.72f, 0.68f),
                    RoleSuccess => new Color(0.45f, 0.80f, 0.50f),
                    RoleWarning => new Color(0.95f, 0.70f, 0.32f),
                    RoleDanger => new Color(0.88f, 0.44f, 0.38f),
                    RoleAccentAlt => new Color(0.70f, 0.60f, 0.90f),
                    RoleNeutral => new Color(0.58f, 0.60f, 0.66f),
                    RoleHeader => new Color(0.24f, 0.28f, 0.34f),
                    RoleTitleText => new Color(0.86f, 0.90f, 0.96f),
                    RoleSubtitleText => new Color(0.72f, 0.76f, 0.82f),
                    RoleMutedText => new Color(0.62f, 0.65f, 0.70f),
                    RoleCardText => new Color(0.88f, 0.90f, 0.94f),
                    RolePathText => new Color(0.66f, 0.70f, 0.76f),
                    RoleResizeHandle => new Color(0.66f, 0.76f, 0.90f, 0.58f),
                    _ => Color.white
                },
                ThemePreset.ForestNight => role switch
                {
                    RolePrimary => new Color(0.28f, 0.70f, 0.42f),
                    RoleSecondary => new Color(0.45f, 0.82f, 0.62f),
                    RoleTertiary => new Color(0.22f, 0.72f, 0.70f),
                    RoleSuccess => new Color(0.50f, 0.90f, 0.42f),
                    RoleWarning => new Color(0.95f, 0.72f, 0.28f),
                    RoleDanger => new Color(0.92f, 0.42f, 0.32f),
                    RoleAccentAlt => new Color(0.78f, 0.60f, 0.92f),
                    RoleNeutral => new Color(0.58f, 0.66f, 0.58f),
                    RoleHeader => new Color(0.16f, 0.34f, 0.25f),
                    RoleTitleText => new Color(0.80f, 1.00f, 0.86f),
                    RoleSubtitleText => new Color(0.72f, 0.84f, 0.74f),
                    RoleMutedText => new Color(0.62f, 0.72f, 0.64f),
                    RoleCardText => new Color(0.84f, 0.96f, 0.86f),
                    RolePathText => new Color(0.66f, 0.76f, 0.68f),
                    RoleResizeHandle => new Color(0.48f, 0.88f, 0.62f, 0.58f),
                    _ => Color.white
                },
                ThemePreset.WarmSlate => role switch
                {
                    RolePrimary => new Color(0.85f, 0.52f, 0.32f),
                    RoleSecondary => new Color(0.95f, 0.70f, 0.38f),
                    RoleTertiary => new Color(0.60f, 0.76f, 0.72f),
                    RoleSuccess => new Color(0.52f, 0.82f, 0.48f),
                    RoleWarning => new Color(1.00f, 0.76f, 0.30f),
                    RoleDanger => new Color(0.92f, 0.38f, 0.30f),
                    RoleAccentAlt => new Color(0.72f, 0.58f, 0.92f),
                    RoleNeutral => new Color(0.66f, 0.60f, 0.56f),
                    RoleHeader => new Color(0.45f, 0.30f, 0.25f),
                    RoleTitleText => new Color(1.00f, 0.88f, 0.72f),
                    RoleSubtitleText => new Color(0.86f, 0.76f, 0.66f),
                    RoleMutedText => new Color(0.76f, 0.68f, 0.62f),
                    RoleCardText => new Color(0.98f, 0.90f, 0.82f),
                    RolePathText => new Color(0.78f, 0.70f, 0.64f),
                    RoleResizeHandle => new Color(1.00f, 0.70f, 0.36f, 0.60f),
                    _ => Color.white
                },
                ThemePreset.HighContrastDark => role switch
                {
                    RolePrimary => new Color(0.00f, 0.70f, 1.00f),
                    RoleSecondary => new Color(0.00f, 0.95f, 1.00f),
                    RoleTertiary => new Color(0.00f, 0.95f, 0.72f),
                    RoleSuccess => new Color(0.35f, 1.00f, 0.42f),
                    RoleWarning => new Color(1.00f, 0.86f, 0.18f),
                    RoleDanger => new Color(1.00f, 0.28f, 0.22f),
                    RoleAccentAlt => new Color(0.88f, 0.66f, 1.00f),
                    RoleNeutral => new Color(0.78f, 0.80f, 0.86f),
                    RoleHeader => new Color(0.00f, 0.18f, 0.28f),
                    RoleTitleText => Color.white,
                    RoleSubtitleText => new Color(0.88f, 0.94f, 1.00f),
                    RoleMutedText => new Color(0.78f, 0.84f, 0.90f),
                    RoleCardText => Color.white,
                    RolePathText => new Color(0.86f, 0.90f, 0.96f),
                    RoleResizeHandle => new Color(0.00f, 0.90f, 1.00f, 0.90f),
                    _ => Color.white
                },
                ThemePreset.CleanLight => role switch
                {
                    RolePrimary => new Color(0.18f, 0.42f, 0.76f),
                    RoleSecondary => new Color(0.12f, 0.58f, 0.72f),
                    RoleTertiary => new Color(0.20f, 0.58f, 0.48f),
                    RoleSuccess => new Color(0.24f, 0.56f, 0.24f),
                    RoleWarning => new Color(0.72f, 0.48f, 0.10f),
                    RoleDanger => new Color(0.76f, 0.20f, 0.16f),
                    RoleAccentAlt => new Color(0.48f, 0.30f, 0.76f),
                    RoleNeutral => new Color(0.48f, 0.52f, 0.58f),
                    RoleHeader => new Color(0.80f, 0.88f, 0.94f),
                    RoleTitleText => new Color(0.06f, 0.12f, 0.18f),
                    RoleSubtitleText => new Color(0.20f, 0.25f, 0.31f),
                    RoleMutedText => new Color(0.34f, 0.38f, 0.44f),
                    RoleCardText => new Color(0.06f, 0.12f, 0.18f),
                    RolePathText => new Color(0.34f, 0.38f, 0.44f),
                    RoleResizeHandle => new Color(0.20f, 0.48f, 0.72f, 0.70f),
                    _ => Color.black
                },
                _ => role switch
                {
                    RolePrimary => new Color(0.28f, 0.58f, 0.98f),
                    RoleSecondary => new Color(0.30f, 0.78f, 0.95f),
                    RoleTertiary => new Color(0.35f, 0.85f, 0.75f),
                    RoleSuccess => new Color(0.35f, 0.85f, 0.45f),
                    RoleWarning => new Color(1.00f, 0.65f, 0.25f),
                    RoleDanger => new Color(0.95f, 0.45f, 0.35f),
                    RoleAccentAlt => new Color(0.70f, 0.55f, 0.95f),
                    RoleNeutral => new Color(0.55f, 0.57f, 0.65f),
                    RoleHeader => new Color(0.12f, 0.42f, 0.58f),
                    RoleTitleText => EditorGUIUtility.isProSkin ? new Color(0.72f, 0.95f, 1f) : new Color(0.06f, 0.22f, 0.35f),
                    RoleSubtitleText => EditorGUIUtility.isProSkin ? new Color(0.72f, 0.78f, 0.84f) : new Color(0.25f, 0.30f, 0.36f),
                    RoleMutedText => EditorGUIUtility.isProSkin ? new Color(0.68f, 0.72f, 0.78f) : new Color(0.34f, 0.36f, 0.40f),
                    RoleCardText => EditorGUIUtility.isProSkin ? new Color(0.72f, 0.95f, 1f) : new Color(0.06f, 0.22f, 0.35f),
                    RolePathText => EditorGUIUtility.isProSkin ? new Color(0.68f, 0.72f, 0.78f) : new Color(0.34f, 0.36f, 0.40f),
                    RoleResizeHandle => EditorGUIUtility.isProSkin ? new Color(0.55f, 0.86f, 1f, 0.55f) : new Color(0.05f, 0.28f, 0.44f, 0.45f),
                    _ => Color.white
                }
            };
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
            string key = $"{color.r:0.000}:{color.g:0.000}:{color.b:0.000}:{color.a:0.000}";
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