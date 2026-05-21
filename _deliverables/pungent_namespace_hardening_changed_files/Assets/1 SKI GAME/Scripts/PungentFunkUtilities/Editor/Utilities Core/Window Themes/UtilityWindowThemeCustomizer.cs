namespace PungentFunk.Utilities.Editor.Theme
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor window for customising the shared theme used by generic utility windows.
    /// </summary>
    public sealed class UtilityWindowThemeCustomizer : EditorWindow
    {
        private enum HarmonyMode
        {
            Monochromatic,
            Analogous,
            Complementary,
            SplitComplementary,
            Triadic,
            Tetradic,
            Square
        }

        [Serializable]
        private sealed class CustomPresetLibrary
        {
            public List<CustomPresetRecord> presets = new List<CustomPresetRecord>();
        }

        [Serializable]
        private sealed class CustomPresetRecord
        {
            public string name;
            public Color[] colors;
            public float panelAlphaDark;
            public float panelAlphaLight;
        }

        private const string PrefPrefix = "GenericUtility.WindowTheme.Customizer.";
        private const string PrefScroll = PrefPrefix + "ScrollY";
        private const string PrefBaseColor = PrefPrefix + "GeneratorBase";
        private const string PrefHarmony = PrefPrefix + "Harmony";
        private const string PrefSatMin = PrefPrefix + "SatMin";
        private const string PrefSatMax = PrefPrefix + "SatMax";
        private const string PrefValMin = PrefPrefix + "ValMin";
        private const string PrefValMax = PrefPrefix + "ValMax";
        private const string PrefHueJitter = PrefPrefix + "HueJitter";
        private const string PrefMinContrast = PrefPrefix + "MinContrast";
        private const string PrefRepairContrast = PrefPrefix + "RepairContrast";
        private const string PrefShowManual = PrefPrefix + "ShowManual";
        private const string PrefShowGenerator = PrefPrefix + "ShowGenerator";
        private const string PrefShowAnalysis = PrefPrefix + "ShowAnalysis";
        private const string PrefCustomPresetName = PrefPrefix + "CustomPresetName";
        private const string PrefCustomPresetLibrary = PrefPrefix + "CustomPresetLibrary";

        private Vector2 _scroll;
        private Color _generatorBaseColor;
        private HarmonyMode _harmony;
        private float _satMin;
        private float _satMax;
        private float _valMin;
        private float _valMax;
        private float _hueJitter;
        private float _targetContrast;
        private bool _repairContrast;
        private bool _showManual;
        private bool _showGenerator;
        private bool _showAnalysis;
        private bool[] _roleLocks;
        private string _customPresetName;

        [MenuItem("Tools/Utilities/Appearance/Utility Window Theme")]
        public static void ShowWindow()
        {
            UtilityWindowThemeCustomizer window = GetWindow<UtilityWindowThemeCustomizer>("Utility Theme");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _scroll = new Vector2(0f, UtilityWindowPrefs.GetFloat(PrefScroll, 0f));
            _generatorBaseColor = UtilityWindowPrefs.GetColor(PrefBaseColor, UtilityWindowTheme.Blue);
            _harmony = (HarmonyMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefHarmony, (int)HarmonyMode.Analogous), 0, Enum.GetValues(typeof(HarmonyMode)).Length - 1);
            _satMin = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefSatMin, 0.42f));
            _satMax = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefSatMax, 0.90f));
            _valMin = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefValMin, 0.32f));
            _valMax = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefValMax, 0.96f));
            _hueJitter = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefHueJitter, 0.025f));
            _targetContrast = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefMinContrast, 4.5f), 1f, 10f);
            _repairContrast = UtilityWindowPrefs.GetBool(PrefRepairContrast, true);
            _showManual = UtilityWindowPrefs.GetBool(PrefShowManual, true);
            _showGenerator = UtilityWindowPrefs.GetBool(PrefShowGenerator, true);
            _showAnalysis = UtilityWindowPrefs.GetBool(PrefShowAnalysis, true);
            _customPresetName = UtilityWindowPrefs.GetString(PrefCustomPresetName, "My Utility Theme");
            LoadRoleLocks();
        }

        private void OnDisable()
        {
            SaveTransientPrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Utility Window Theme",
                "Customise the shared visual theme used by generic IMGUI utility windows. Existing windows update through UtilityWindowTheme without needing per-window style code.",
                UtilityWindowTheme.ActivePreset.ToString());

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawPresetSection();
            DrawAccessibilitySection();
            DrawGeneratorSection();
            DrawManualSection();
            DrawPreviewSection();
            DrawAnalysisSection();

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
                UtilityWindowPrefs.SetFloat(PrefScroll, _scroll.y);
        }

        private void DrawPresetSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Theme Presets", UtilityWindowTheme.Blue, "shared");
                EditorGUILayout.LabelField("Presets are a safe starting point. Manual colours, generated themes, and custom presets can be applied after selecting one.", UtilityWindowTheme.MutedMiniLabelStyle);

                UtilityWindowTheme.ThemePreset[] builtIns = GetVisibleBuiltInPresets();
                string[] labels = GetVisibleBuiltInPresetLabels();
                UtilityWindowTheme.ThemePreset selected = UtilityWindowTheme.ActivePreset;
                int selectedIndex = Array.IndexOf(builtIns, selected);
                if (selectedIndex < 0)
                    selectedIndex = 0;

                EditorGUI.BeginChangeCheck();
                int nextIndex = EditorGUILayout.Popup("Built-in Preset", selectedIndex, labels);
                if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < builtIns.Length)
                    UtilityWindowTheme.ApplyPreset(builtIns[nextIndex]);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Debug Default", UtilityWindowTheme.Blue, GUILayout.Height(24f)))
                        UtilityWindowTheme.ApplyPreset(UtilityWindowTheme.ThemePreset.DebugControl);
                    if (UtilityWindowTheme.TintedButton("High Contrast Dark", UtilityWindowTheme.Amber, GUILayout.Height(24f)))
                        UtilityWindowTheme.ApplyPreset(UtilityWindowTheme.ThemePreset.HighContrastDark);
                    if (UtilityWindowTheme.TintedButton("Clean Light", UtilityWindowTheme.Teal, GUILayout.Height(24f)))
                        UtilityWindowTheme.ApplyPreset(UtilityWindowTheme.ThemePreset.CleanLight);
                }

                EditorGUILayout.Space(6f);
                DrawCustomPresetSection();
            }
        }

        private static UtilityWindowTheme.ThemePreset[] GetVisibleBuiltInPresets()
        {
            return new[]
            {
                UtilityWindowTheme.ThemePreset.DebugControl,
                UtilityWindowTheme.ThemePreset.OceanGlass,
                UtilityWindowTheme.ThemePreset.Graphite,
                UtilityWindowTheme.ThemePreset.ForestNight,
                UtilityWindowTheme.ThemePreset.WarmSlate,
                UtilityWindowTheme.ThemePreset.HighContrastDark,
                UtilityWindowTheme.ThemePreset.CleanLight
            };
        }

        private static string[] GetVisibleBuiltInPresetLabels()
        {
            return new[]
            {
                "Debug Control",
                "Ocean Glass",
                "Graphite",
                "Forest Night",
                "Warm Slate",
                "High Contrast Dark",
                "Clean Light"
            };
        }

        private void DrawCustomPresetSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, padding: 7, margin: 3)))
            {
                UtilityWindowTheme.SectionTitle("Custom Presets", UtilityWindowTheme.Cyan, "local");
                EditorGUILayout.LabelField("Save the current shared theme into EditorPrefs, then apply or delete it later. This is intentionally local-only for now, so it does not create project assets while the utility style is still evolving.", UtilityWindowTheme.MutedMiniLabelStyle);

                EditorGUI.BeginChangeCheck();
                _customPresetName = EditorGUILayout.TextField(new GUIContent("Preset Name", "Name used when saving the current role colours as a reusable custom preset."), _customPresetName);
                if (EditorGUI.EndChangeCheck())
                    UtilityWindowPrefs.SetString(PrefCustomPresetName, _customPresetName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Save / Update Custom Preset", UtilityWindowTheme.Green, GUILayout.Height(24f)))
                        SaveCurrentAsCustomPreset();

                    if (UtilityWindowTheme.TintedButton("Refresh List", UtilityWindowTheme.Neutral, GUILayout.Height(24f)))
                        Repaint();
                }

                CustomPresetLibrary library = LoadCustomPresetLibrary();
                if (library.presets == null || library.presets.Count == 0)
                {
                    EditorGUILayout.LabelField("No custom presets saved yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                for (int i = 0; i < library.presets.Count; i++)
                {
                    CustomPresetRecord preset = library.presets[i];
                    if (preset == null)
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect swatchRect = GUILayoutUtility.GetRect(44f, 18f, GUILayout.Width(44f), GUILayout.Height(18f));
                        DrawPresetMiniSwatches(swatchRect, preset);

                        EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(preset.name) ? "Unnamed Preset" : preset.name, GUILayout.MinWidth(120f));
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Apply", EditorStyles.miniButtonLeft, GUILayout.Width(54f)))
                            ApplyCustomPreset(preset);

                        if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(54f)))
                        {
                            if (EditorUtility.DisplayDialog("Delete Custom Preset", $"Delete custom utility theme preset '{preset.name}'?", "Delete", "Cancel"))
                            {
                                library.presets.RemoveAt(i);
                                SaveCustomPresetLibrary(library);
                                Repaint();
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }
            }
        }

        private void DrawPresetMiniSwatches(Rect rect, CustomPresetRecord preset)
        {
            if (preset == null || preset.colors == null || preset.colors.Length == 0)
            {
                EditorGUI.DrawRect(rect, UtilityWindowTheme.Neutral);
                return;
            }

            int count = Mathf.Min(5, preset.colors.Length);
            float width = rect.width / count;
            for (int i = 0; i < count; i++)
            {
                Rect swatch = new Rect(rect.x + i * width, rect.y, width, rect.height);
                EditorGUI.DrawRect(swatch, preset.colors[i]);
            }
        }

        private void SaveCurrentAsCustomPreset()
        {
            string presetName = string.IsNullOrWhiteSpace(_customPresetName)
                ? $"Utility Theme {DateTime.Now:yyyy-MM-dd HH-mm}"
                : _customPresetName.Trim();

            CustomPresetLibrary library = LoadCustomPresetLibrary();
            if (library.presets == null)
                library.presets = new List<CustomPresetRecord>();

            CustomPresetRecord record = CaptureCurrentPreset(presetName);
            int existingIndex = library.presets.FindIndex(p => p != null && string.Equals(p.name, presetName, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
                library.presets[existingIndex] = record;
            else
                library.presets.Add(record);

            SaveCustomPresetLibrary(library);
            _customPresetName = presetName;
            UtilityWindowPrefs.SetString(PrefCustomPresetName, _customPresetName);
            Repaint();
        }

        private CustomPresetRecord CaptureCurrentPreset(string presetName)
        {
            string[] roles = UtilityWindowTheme.EditableColorRoles;
            var colors = new Color[roles.Length];
            for (int i = 0; i < roles.Length; i++)
                colors[i] = UtilityWindowTheme.GetColor(roles[i]);

            return new CustomPresetRecord
            {
                name = presetName,
                colors = colors,
                panelAlphaDark = UtilityWindowTheme.PanelAlphaDark,
                panelAlphaLight = UtilityWindowTheme.PanelAlphaLight
            };
        }

        private void ApplyCustomPreset(CustomPresetRecord preset)
        {
            if (preset == null || preset.colors == null)
                return;

            string[] roles = UtilityWindowTheme.EditableColorRoles;
            int count = Mathf.Min(roles.Length, preset.colors.Length);
            for (int i = 0; i < count; i++)
                UtilityWindowTheme.SetColor(roles[i], preset.colors[i]);

            UtilityWindowTheme.PanelAlphaDark = preset.panelAlphaDark <= 0f ? UtilityWindowTheme.PanelAlphaDark : preset.panelAlphaDark;
            UtilityWindowTheme.PanelAlphaLight = preset.panelAlphaLight <= 0f ? UtilityWindowTheme.PanelAlphaLight : preset.panelAlphaLight;
            _customPresetName = string.IsNullOrWhiteSpace(preset.name) ? _customPresetName : preset.name;
            UtilityWindowPrefs.SetString(PrefCustomPresetName, _customPresetName);
            UtilityWindowTheme.InvalidateStyles();
            UtilityWindowTheme.RepaintUtilityWindows();
            Repaint();
        }

        private CustomPresetLibrary LoadCustomPresetLibrary()
        {
            string json = UtilityWindowPrefs.GetString(PrefCustomPresetLibrary, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    CustomPresetLibrary loaded = JsonUtility.FromJson<CustomPresetLibrary>(json);
                    if (loaded != null)
                    {
                        if (loaded.presets == null)
                            loaded.presets = new List<CustomPresetRecord>();
                        loaded.presets.RemoveAll(p => p == null);
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UtilityTheme] Failed to load custom theme presets: {ex.Message}");
                }
            }

            return new CustomPresetLibrary();
        }

        private void SaveCustomPresetLibrary(CustomPresetLibrary library)
        {
            if (library == null)
                library = new CustomPresetLibrary();
            if (library.presets == null)
                library.presets = new List<CustomPresetRecord>();

            UtilityWindowPrefs.SetString(PrefCustomPresetLibrary, JsonUtility.ToJson(library));
        }

        private void DrawAccessibilitySection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Accessibility & Readability", UtilityWindowTheme.Teal, "contrast");

                EditorGUI.BeginChangeCheck();
                UtilityWindowTheme.DeficiencyPreview preview = (UtilityWindowTheme.DeficiencyPreview)EditorGUILayout.EnumPopup(
                    new GUIContent("Colour-blind Preview", "Simulates how the current palette reads in this tool's preview and analysis sections. It does not distort the actual theme globally."),
                    UtilityWindowTheme.ActiveDeficiencyPreview);
                float target = EditorGUILayout.Slider(new GUIContent("Target Text Contrast", "AA body text is 4.5:1. AAA body text is 7:1."), _targetContrast, 3f, 7f);
                bool repair = EditorGUILayout.Toggle(new GUIContent("Repair Generated Text", "When generating a theme, push text colours toward readable values if contrast is too low."), _repairContrast);
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowTheme.ActiveDeficiencyPreview = preview;
                    _targetContrast = target;
                    _repairContrast = repair;
                    SaveTransientPrefs();
                    Repaint();
                }

                EditorGUILayout.LabelField("Use the high-contrast presets for production-safe utility themes. The preview mode is mainly for checking whether role colours collapse into each other.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawGeneratorSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showGenerator = EditorGUILayout.Foldout(_showGenerator, "Palette Generator", true);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill("iterative", UtilityWindowTheme.Purple);
                }

                if (!_showGenerator)
                {
                    SaveTransientPrefs();
                    return;
                }

                EditorGUILayout.LabelField("Generate unlocked theme roles from a base colour, harmony rule, and saturation/value range. Lock roles below to protect existing swatches during regeneration.", UtilityWindowTheme.MutedMiniLabelStyle);

                EditorGUI.BeginChangeCheck();
                _generatorBaseColor = EditorGUILayout.ColorField(new GUIContent("Base Colour", "The hue anchor used for generated roles."), _generatorBaseColor);
                _harmony = (HarmonyMode)EditorGUILayout.EnumPopup("Harmony", _harmony);

                MinMax01("Saturation Range", ref _satMin, ref _satMax);
                MinMax01("Value Range", ref _valMin, ref _valMax);
                _hueJitter = EditorGUILayout.Slider(new GUIContent("Hue Jitter", "Small per-role variation so generated swatches do not feel too mechanical."), _hueJitter, 0f, 0.08f);
                if (EditorGUI.EndChangeCheck())
                    SaveTransientPrefs();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Generate Unlocked Roles", UtilityWindowTheme.Purple, GUILayout.Height(25f)))
                        GenerateTheme(false);

                    if (UtilityWindowTheme.TintedButton("Randomise Base + Generate", UtilityWindowTheme.Cyan, GUILayout.Height(25f)))
                        GenerateTheme(true);
                }

                if (UtilityWindowTheme.TintedButton("Unlock All Theme Roles", UtilityWindowTheme.Neutral, GUILayout.Height(22f)))
                {
                    for (int i = 0; i < _roleLocks.Length; i++)
                        _roleLocks[i] = false;
                    SaveRoleLocks();
                }
            }
        }

        private void DrawManualSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showManual = EditorGUILayout.Foldout(_showManual, "Manual Theme Roles", true);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(UtilityWindowTheme.EditableColorRoles.Length.ToString(), UtilityWindowTheme.Cyan);
                }

                if (!_showManual)
                {
                    SaveTransientPrefs();
                    return;
                }

                string[] roles = UtilityWindowTheme.EditableColorRoles;
                for (int i = 0; i < roles.Length; i++)
                {
                    DrawRoleRow(i, roles[i]);
                }

                EditorGUI.BeginChangeCheck();
                float darkAlpha = EditorGUILayout.Slider(new GUIContent("Panel Alpha / Pro Skin", "Default panel tint opacity when Unity is using the dark editor skin."), UtilityWindowTheme.PanelAlphaDark, 0.05f, 0.50f);
                float lightAlpha = EditorGUILayout.Slider(new GUIContent("Panel Alpha / Light Skin", "Default panel tint opacity when Unity is using the light editor skin."), UtilityWindowTheme.PanelAlphaLight, 0.04f, 0.28f);
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowTheme.PanelAlphaDark = darkAlpha;
                    UtilityWindowTheme.PanelAlphaLight = lightAlpha;
                    UtilityWindowTheme.RepaintUtilityWindows();
                }
            }
        }

        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.HeaderTint)))
            {
                UtilityWindowTheme.SectionTitle("Live Preview", UtilityWindowTheme.HeaderTint, UtilityWindowTheme.ActiveDeficiencyPreview.ToString());
                EditorGUILayout.LabelField("This preview uses the same shared styles and helper methods as the utility windows.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawPreviewSwatch("Primary", UtilityWindowTheme.Blue);
                    DrawPreviewSwatch("Cyan", UtilityWindowTheme.Cyan);
                    DrawPreviewSwatch("Teal", UtilityWindowTheme.Teal);
                    DrawPreviewSwatch("Success", UtilityWindowTheme.Green);
                    DrawPreviewSwatch("Warning", UtilityWindowTheme.Amber);
                    DrawPreviewSwatch("Danger", UtilityWindowTheme.Red);
                    DrawPreviewSwatch("Purple", UtilityWindowTheme.Purple);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("42", UtilityWindowTheme.Blue);
                    UtilityWindowTheme.CountPill("Ready", UtilityWindowTheme.Green);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.TintedButton("Tinted Button", UtilityWindowTheme.Blue, GUILayout.Width(140f));
                    UtilityWindowTheme.TintedButton("Warning", UtilityWindowTheme.Amber, GUILayout.Width(100f));
                }

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, padding: 7, margin: 3)))
                {
                    EditorGUILayout.LabelField("Section Header", UtilityWindowTheme.SectionHeaderStyle);
                    EditorGUILayout.LabelField("This is muted helper copy. It should remain readable without becoming visually dominant.", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Card Title", UtilityWindowTheme.CardLabelStyle);
                    EditorGUILayout.LabelField("Assets/Example/Long/Utility/Path.asset", UtilityWindowTheme.PathLabelStyle);
                }
            }
        }

        private void DrawAnalysisSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _showAnalysis = EditorGUILayout.Foldout(_showAnalysis, "Contrast Analysis", true);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill("AA target", UtilityWindowTheme.Green);
                }

                if (!_showAnalysis)
                {
                    SaveTransientPrefs();
                    return;
                }

                DrawContrastPair("Title vs Header", UtilityWindowTheme.TitleText, UtilityWindowTheme.HeaderTint);
                DrawContrastPair("Subtitle vs Header", UtilityWindowTheme.SubtitleText, UtilityWindowTheme.HeaderTint);
                DrawContrastPair("Muted vs Header", UtilityWindowTheme.MutedText, UtilityWindowTheme.HeaderTint);
                DrawContrastPair("Card vs Primary Panel", UtilityWindowTheme.CardText, UtilityWindowTheme.Blue);
                DrawContrastPair("Path vs Primary Panel", UtilityWindowTheme.PathText, UtilityWindowTheme.Blue);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Role separation under selected preview", UtilityWindowTheme.SectionHeaderStyle);
                DrawRoleDistanceWarning(UtilityWindowTheme.Blue, UtilityWindowTheme.Cyan, "Primary / Cyan");
                DrawRoleDistanceWarning(UtilityWindowTheme.Green, UtilityWindowTheme.Amber, "Success / Warning");
                DrawRoleDistanceWarning(UtilityWindowTheme.Amber, UtilityWindowTheme.Red, "Warning / Danger");
                DrawRoleDistanceWarning(UtilityWindowTheme.Blue, UtilityWindowTheme.Purple, "Primary / Purple");
            }
        }

        private void DrawRoleRow(int index, string role)
        {
            Color current = UtilityWindowTheme.GetColor(role);
            using (new EditorGUILayout.HorizontalScope())
            {
                _roleLocks[index] = GUILayout.Toggle(_roleLocks[index], new GUIContent(string.Empty, "Lock this role so palette generation will not overwrite it."), GUILayout.Width(18f));

                Rect swatch = GUILayoutUtility.GetRect(28f, 18f, GUILayout.Width(28f), GUILayout.Height(18f));
                Color display = Preview(current);
                EditorGUI.DrawRect(swatch, display);
                EditorGUI.DrawRect(new Rect(swatch.x, swatch.y, swatch.width, 1f), UtilityWindowTheme.GetReadableTextColor(display));

                EditorGUILayout.LabelField(UtilityWindowTheme.GetRoleDisplayName(role), GUILayout.Width(150f));

                EditorGUI.BeginChangeCheck();
                Color next = EditorGUILayout.ColorField(current);
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowTheme.SetColor(role, next);
                    UtilityWindowTheme.RepaintUtilityWindows();
                }

                string hex = "#" + ColorUtility.ToHtmlStringRGB(current);
                EditorGUILayout.SelectableLabel(hex, EditorStyles.miniLabel, GUILayout.Width(72f), GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(44f)))
                    EditorGUIUtility.systemCopyBuffer = hex;
            }

            SaveRoleLocks();
        }

        private void DrawPreviewSwatch(string label, Color color)
        {
            Color display = Preview(color);
            Rect rect = GUILayoutUtility.GetRect(50f, 42f, GUILayout.ExpandWidth(true), GUILayout.Height(42f));
            EditorGUI.DrawRect(rect, display);
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = UtilityWindowTheme.GetReadableTextColor(display) }
            };
            GUI.Label(rect, label, style);
        }

        private void DrawContrastPair(string label, Color fg, Color bg)
        {
            Color previewFg = Preview(fg);
            Color previewBg = Preview(bg);
            float ratio = UtilityWindowTheme.GetContrastRatio(previewFg, previewBg);
            string rating = UtilityWindowTheme.GetContrastRating(ratio);
            Color pill = ratio >= _targetContrast ? UtilityWindowTheme.Green : UtilityWindowTheme.Red;

            using (new EditorGUILayout.HorizontalScope())
            {
                Rect sample = GUILayoutUtility.GetRect(90f, 22f, GUILayout.Width(90f), GUILayout.Height(22f));
                EditorGUI.DrawRect(sample, previewBg);
                GUIStyle sampleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = previewFg }
                };
                GUI.Label(sample, "Sample", sampleStyle);

                EditorGUILayout.LabelField(label, GUILayout.MinWidth(150f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{ratio:0.00}:1", GUILayout.Width(64f));
                UtilityWindowTheme.CountPill(rating, pill, 72f);
            }
        }

        private void DrawRoleDistanceWarning(Color a, Color b, string label)
        {
            Color pa = Preview(a);
            Color pb = Preview(b);
            float distance = Mathf.Sqrt(
                Mathf.Pow(pa.r - pb.r, 2f) +
                Mathf.Pow(pa.g - pb.g, 2f) +
                Mathf.Pow(pa.b - pb.b, 2f));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.MinWidth(150f));
                GUILayout.FlexibleSpace();
                string note = distance < 0.18f ? "too similar" : distance < 0.30f ? "close" : "distinct";
                Color tint = distance < 0.18f ? UtilityWindowTheme.Red : distance < 0.30f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green;
                UtilityWindowTheme.CountPill(note, tint, 84f);
            }
        }

        private void GenerateTheme(bool randomiseBase)
        {
            if (randomiseBase)
            {
                _generatorBaseColor = Color.HSVToRGB(UnityEngine.Random.value, UnityEngine.Random.Range(_satMin, _satMax), UnityEngine.Random.Range(_valMin, _valMax));
                UtilityWindowPrefs.SetColor(PrefBaseColor, _generatorBaseColor);
            }

            List<Color> harmony = BuildHarmony(_generatorBaseColor, _harmony);
            string[] roles = UtilityWindowTheme.EditableColorRoles;

            for (int i = 0; i < roles.Length; i++)
            {
                if (_roleLocks[i])
                    continue;

                string role = roles[i];
                Color generated = GenerateRoleColor(role, i, harmony);
                UtilityWindowTheme.SetColor(role, generated);
            }

            if (_repairContrast)
            {
                RepairTextRole(UtilityWindowTheme.RoleTitleText, UtilityWindowTheme.HeaderTint);
                RepairTextRole(UtilityWindowTheme.RoleSubtitleText, UtilityWindowTheme.HeaderTint);
                RepairTextRole(UtilityWindowTheme.RoleMutedText, UtilityWindowTheme.HeaderTint, Mathf.Max(3f, _targetContrast - 1.0f));
                RepairTextRole(UtilityWindowTheme.RoleCardText, UtilityWindowTheme.Blue);
                RepairTextRole(UtilityWindowTheme.RolePathText, UtilityWindowTheme.Blue, Mathf.Max(3f, _targetContrast - 1.0f));
            }

            SaveTransientPrefs();
            UtilityWindowTheme.InvalidateStyles();
            UtilityWindowTheme.RepaintUtilityWindows();
            Repaint();
        }

        private Color GenerateRoleColor(string role, int index, List<Color> harmony)
        {
            Color basis = harmony[Mathf.Abs(index) % harmony.Count];
            Color.RGBToHSV(basis, out float h, out float s, out float v);
            h = Repeat01(h + UnityEngine.Random.Range(-_hueJitter, _hueJitter));
            s = Mathf.Clamp(UnityEngine.Random.Range(_satMin, _satMax), 0.05f, 1f);
            v = Mathf.Clamp(UnityEngine.Random.Range(_valMin, _valMax), 0.08f, 1f);

            switch (role)
            {
                case UtilityWindowTheme.RoleHeader:
                    s = Mathf.Clamp(s * 0.82f, 0.18f, 0.80f);
                    v = EditorGUIUtility.isProSkin ? Mathf.Clamp(v * 0.45f, 0.14f, 0.36f) : Mathf.Clamp(v * 1.05f, 0.72f, 0.98f);
                    break;
                case UtilityWindowTheme.RoleNeutral:
                    s = Mathf.Clamp(s * 0.22f, 0.04f, 0.28f);
                    v = Mathf.Clamp(v, 0.34f, 0.82f);
                    break;
                case UtilityWindowTheme.RoleSuccess:
                    h = UnityEngine.Random.Range(0.28f, 0.42f);
                    break;
                case UtilityWindowTheme.RoleWarning:
                    h = UnityEngine.Random.Range(0.10f, 0.16f);
                    s = Mathf.Max(s, 0.58f);
                    break;
                case UtilityWindowTheme.RoleDanger:
                    h = UnityEngine.Random.Range(0.00f, 0.03f);
                    s = Mathf.Max(s, 0.58f);
                    break;
                case UtilityWindowTheme.RoleTitleText:
                case UtilityWindowTheme.RoleCardText:
                    s *= 0.45f;
                    v = EditorGUIUtility.isProSkin ? UnityEngine.Random.Range(0.84f, 1f) : UnityEngine.Random.Range(0.03f, 0.22f);
                    break;
                case UtilityWindowTheme.RoleSubtitleText:
                case UtilityWindowTheme.RoleMutedText:
                case UtilityWindowTheme.RolePathText:
                    s *= 0.34f;
                    v = EditorGUIUtility.isProSkin ? UnityEngine.Random.Range(0.62f, 0.86f) : UnityEngine.Random.Range(0.16f, 0.34f);
                    break;
                case UtilityWindowTheme.RoleResizeHandle:
                    s = Mathf.Max(s, 0.55f);
                    v = Mathf.Max(v, 0.58f);
                    break;
            }

            Color result = Color.HSVToRGB(h, s, v);
            result.a = role == UtilityWindowTheme.RoleResizeHandle ? 0.62f : 1f;
            return result;
        }

        private void RepairTextRole(string role, Color background, float? contrastOverride = null)
        {
            int index = Array.IndexOf(UtilityWindowTheme.EditableColorRoles, role);
            if (index >= 0 && _roleLocks[index])
                return;

            Color current = UtilityWindowTheme.GetColor(role);
            Color repaired = UtilityWindowTheme.ImproveContrast(current, background, contrastOverride ?? _targetContrast);
            UtilityWindowTheme.SetColor(role, repaired);
        }

        private List<Color> BuildHarmony(Color baseColor, HarmonyMode mode)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            List<float> offsets = mode switch
            {
                HarmonyMode.Monochromatic => new List<float> { 0f, 0f, 0f, 0f, 0f },
                HarmonyMode.Analogous => new List<float> { 0f, -1f / 12f, 1f / 12f, -1f / 8f, 1f / 8f },
                HarmonyMode.Complementary => new List<float> { 0f, 0.5f, -1f / 24f, 0.5f + 1f / 24f, 0.5f - 1f / 24f },
                HarmonyMode.SplitComplementary => new List<float> { 0f, 5f / 12f, 7f / 12f, -1f / 16f, 1f / 16f },
                HarmonyMode.Triadic => new List<float> { 0f, 1f / 3f, 2f / 3f, 1f / 24f, -1f / 24f },
                HarmonyMode.Tetradic => new List<float> { 0f, 1f / 4f, 0.5f, 0.75f, 1f / 24f },
                HarmonyMode.Square => new List<float> { 0f, 0.25f, 0.5f, 0.75f, 0.125f },
                _ => new List<float> { 0f }
            };

            var result = new List<Color>();
            foreach (float offset in offsets)
            {
                float nh = Repeat01(h + offset);
                result.Add(Color.HSVToRGB(nh, s, v));
            }
            return result;
        }

        private void MinMax01(string label, ref float min, ref float max)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                min = EditorGUILayout.FloatField(min, GUILayout.Width(42f));
                EditorGUILayout.MinMaxSlider(ref min, ref max, 0f, 1f);
                max = EditorGUILayout.FloatField(max, GUILayout.Width(42f));
            }

            min = Mathf.Clamp01(min);
            max = Mathf.Clamp01(max);
            if (max < min)
                max = min;
        }

        private Color Preview(Color color)
        {
            return UtilityWindowTheme.SimulateDeficiency(UtilityWindowTheme.ActiveDeficiencyPreview, color);
        }

        private float Repeat01(float value)
        {
            value %= 1f;
            if (value < 0f)
                value += 1f;
            return value;
        }

        private void LoadRoleLocks()
        {
            string[] roles = UtilityWindowTheme.EditableColorRoles;
            _roleLocks = new bool[roles.Length];
            for (int i = 0; i < roles.Length; i++)
                _roleLocks[i] = UtilityWindowPrefs.GetBool(RoleLockPref(roles[i]), false);
        }

        private void SaveRoleLocks()
        {
            if (_roleLocks == null)
                return;

            string[] roles = UtilityWindowTheme.EditableColorRoles;
            for (int i = 0; i < roles.Length && i < _roleLocks.Length; i++)
                UtilityWindowPrefs.SetBool(RoleLockPref(roles[i]), _roleLocks[i]);
        }

        private string RoleLockPref(string role) => PrefPrefix + "Lock." + role;

        private void SaveTransientPrefs()
        {
            UtilityWindowPrefs.SetFloat(PrefScroll, _scroll.y);
            UtilityWindowPrefs.SetColor(PrefBaseColor, _generatorBaseColor);
            UtilityWindowPrefs.SetInt(PrefHarmony, (int)_harmony);
            UtilityWindowPrefs.SetFloat(PrefSatMin, _satMin);
            UtilityWindowPrefs.SetFloat(PrefSatMax, _satMax);
            UtilityWindowPrefs.SetFloat(PrefValMin, _valMin);
            UtilityWindowPrefs.SetFloat(PrefValMax, _valMax);
            UtilityWindowPrefs.SetFloat(PrefHueJitter, _hueJitter);
            UtilityWindowPrefs.SetFloat(PrefMinContrast, _targetContrast);
            UtilityWindowPrefs.SetBool(PrefRepairContrast, _repairContrast);
            UtilityWindowPrefs.SetBool(PrefShowManual, _showManual);
            UtilityWindowPrefs.SetBool(PrefShowGenerator, _showGenerator);
            UtilityWindowPrefs.SetBool(PrefShowAnalysis, _showAnalysis);
            SaveRoleLocks();
        }
    }
    #endif

}