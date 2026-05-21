namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Appearance Lab for browsing, applying, composing, and exporting the shared PungentFunk utility/editor theme.
    /// Layout priority: active theme workbench first, slim preset stack second.
    /// </summary>
    public sealed partial class UtilityWindowThemeCustomizer : EditorWindow
    {
        private enum ThemeHarmonyMode
        {
            Contextual,
            Monochromatic,
            Analogous,
            Complementary,
            SplitComplementary,
            Triadic,
            Tetradic,
            Square,
            RandomBalanced
        }

        private enum PresetCategoryFilter
        {
            All,
            Base,
            Aesthetics,
            Elements,
            Nature,
            Seasons,
            Skies,
            Weather,
            Colours
        }

        private enum PresetToneFilter
        {
            All,
            Dark,
            Light,
            Mixed
        }

        private enum PresetFamilyFilter
        {
            All,
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

        private enum PaletteThemeBuildMode
        {
            PreserveRoles,
            BuildDark,
            BuildLight,
            BuildHighContrast,
            BuildSoftPastel,
            BuildVibrant
        }

        private enum PaletteSourceMode
        {
            None,
            SelectedProjectAsset,
            DiscoveredPalette
        }
        private enum RecipeMood
        {
            Professional,
            Cyber,
            Retro,
            Natural,
            Cinematic
        }

        private struct PaletteRoleColor
        {
            public string role;
            public Color color;
        }

        private struct PaletteSourceInfo
        {
            public UnityEngine.Object asset;
            public string displayName;
            public string assetPath;
            public List<PaletteRoleColor> colours;
        }

        private const string PrefPrefix = "GenericUtility.WindowTheme.AppearanceLab.";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefCategory = PrefPrefix + "Category";
        private const string PrefToneFilter = PrefPrefix + "ToneFilter";
        private const string PrefFamilyFilter = PrefPrefix + "FamilyFilter";
        private const string PrefLeftWidth = PrefPrefix + "LeftWidth";
        private const string PrefRightWidth = PrefPrefix + "RightWidth";
        private const string PrefShowTypography = PrefPrefix + "ShowTypography";
        private const string PrefShowPanels = PrefPrefix + "ShowPanels";
        private const string PrefShowManual = PrefPrefix + "ShowManual";
        private const string PrefShowExperimental = PrefPrefix + "ShowExperimental";
        private const string PrefGeneratorBase = PrefPrefix + "GeneratorBase";
        private const string PrefGeneratorHarmony = PrefPrefix + "GeneratorHarmony";
        private const string PrefGeneratorContrast = PrefPrefix + "GeneratorContrast";
        private const string PrefGeneratorSeed = PrefPrefix + "GeneratorSeed";
        private const string PrefGeneratorInfluence = PrefPrefix + "GeneratorInfluence";
        private const string PrefUsePaletteDesigner = PrefPrefix + "UsePaletteDesigner";
        private const string PrefPaletteBuildMode = PrefPrefix + "PaletteBuildMode";
        private const string PrefComposerSaturation = PrefPrefix + "ComposerSaturation";
        private const string PrefComposerAccentIntensity = PrefPrefix + "ComposerAccentIntensity";
        private const string PrefComposerSurfaceDepth = PrefPrefix + "ComposerSurfaceDepth";
        private const string PrefComposerBorderStrength = PrefPrefix + "ComposerBorderStrength";
        private const string PrefComposerMutedStrength = PrefPrefix + "ComposerMutedStrength";
        private const string PrefComposerPreferLight = PrefPrefix + "ComposerPreferLight";
        private const string PrefShowAdvancedComposer = PrefPrefix + "ShowAdvancedComposer";
        private const string PrefShowReadabilityAudit = PrefPrefix + "ShowReadabilityAudit";
        private const string PrefPaletteSourceMode = PrefPrefix + "PaletteSourceMode";
        private const string PrefSelectedPalettePath = PrefPrefix + "SelectedPalettePath";

        private Vector2 _presetScroll;
        private Vector2 _starterThemeScroll;
        private Vector2 _workbenchScroll;
        private Vector2 _rightScroll;
        private string _search;
        private PresetCategoryFilter _presetCategoryFilter;
        private PresetToneFilter _presetToneFilter;
        private PresetFamilyFilter _presetFamilyFilter;
        private float _leftWidth;
        private float _rightWidth;
        private bool _showTypography;
        private bool _showPanels;
        private bool _showManual;
        private bool _showExperimental;
        private Color _generatorBase;
        private ThemeHarmonyMode _generatorHarmony;
        private float _generatorTargetContrast;
        private float _generatorInfluence;
        private int _generatorSeed;
        private bool _usePaletteDesignerIfAvailable;
        private PaletteThemeBuildMode _paletteBuildMode;
        private float _composerSaturation;
        private float _composerAccentIntensity;
        private float _composerSurfaceDepth;
        private float _composerBorderStrength;
        private float _composerMutedStrength;
        private bool _composerPreferLight;
        private bool _showAdvancedComposer;
        private bool _showReadabilityAudit;
        private PaletteSourceMode _paletteSourceMode;
        private string _selectedPalettePath;
        private int _selectedPaletteIndex = -1;
        private readonly List<PaletteSourceInfo> _availablePalettes = new List<PaletteSourceInfo>();
        private readonly Dictionary<string, double> _fieldFlashUntil = new Dictionary<string, double>();
        private const double FieldFlashDuration = 0.85d;
        private RecipeMood _activeRecipeMood = RecipeMood.Professional;

        public static void ShowWindow()
        {
            UtilityWindowThemeCustomizer window = GetWindow<UtilityWindowThemeCustomizer>("Appearance Lab");
            window.minSize = new Vector2(860f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Appearance Lab");
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            _presetCategoryFilter = (PresetCategoryFilter)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefCategory, 0), 0, Enum.GetValues(typeof(PresetCategoryFilter)).Length - 1);
            _presetToneFilter = (PresetToneFilter)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefToneFilter, 0), 0, Enum.GetValues(typeof(PresetToneFilter)).Length - 1);
            _presetFamilyFilter = (PresetFamilyFilter)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefFamilyFilter, 0), 0, Enum.GetValues(typeof(PresetFamilyFilter)).Length - 1);
            _leftWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefLeftWidth, 176f), 132f, 260f);
            _rightWidth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefRightWidth, 286f), 240f, 380f);
            _showTypography = UtilityWindowPrefs.GetBool(PrefShowTypography, true);
            _showPanels = UtilityWindowPrefs.GetBool(PrefShowPanels, true);
            _showManual = UtilityWindowPrefs.GetBool(PrefShowManual, false);
            _showExperimental = UtilityWindowPrefs.GetBool(PrefShowExperimental, true);
            _generatorBase = UtilityWindowPrefs.GetColor(PrefGeneratorBase, UtilityWindowTheme.Blue);
            _generatorHarmony = (ThemeHarmonyMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefGeneratorHarmony, (int)ThemeHarmonyMode.Contextual), 0, Enum.GetValues(typeof(ThemeHarmonyMode)).Length - 1);
            _generatorTargetContrast = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefGeneratorContrast, 5.0f), 4.5f, 10f);
            _generatorSeed = UtilityWindowPrefs.GetInt(PrefGeneratorSeed, 12345);
            _generatorInfluence = Mathf.Clamp01(UtilityWindowPrefs.GetFloat(PrefGeneratorInfluence, 0.72f));
            _usePaletteDesignerIfAvailable = UtilityWindowPrefs.GetBool(PrefUsePaletteDesigner, true);
            _paletteBuildMode = (PaletteThemeBuildMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefPaletteBuildMode, 0), 0, Enum.GetValues(typeof(PaletteThemeBuildMode)).Length - 1);
            _composerSaturation = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefComposerSaturation, 0.55f), 0f, 1f);
            _composerAccentIntensity = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefComposerAccentIntensity, 0.62f), 0f, 1f);
            _composerSurfaceDepth = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefComposerSurfaceDepth, 0.56f), 0f, 1f);
            _composerBorderStrength = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefComposerBorderStrength, 0.50f), 0f, 1f);
            _composerMutedStrength = Mathf.Clamp(UtilityWindowPrefs.GetFloat(PrefComposerMutedStrength, 0.48f), 0f, 1f);
            _composerPreferLight = UtilityWindowPrefs.GetBool(PrefComposerPreferLight, false);
            _showAdvancedComposer = UtilityWindowPrefs.GetBool(PrefShowAdvancedComposer, false);
            _showReadabilityAudit = UtilityWindowPrefs.GetBool(PrefShowReadabilityAudit, false);
            _paletteSourceMode = (PaletteSourceMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefPaletteSourceMode, (int)PaletteSourceMode.DiscoveredPalette), 0, Enum.GetValues(typeof(PaletteSourceMode)).Length - 1);
            _selectedPalettePath = UtilityWindowPrefs.GetString(PrefSelectedPalettePath, string.Empty);
            RefreshAvailablePalettes(false);
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions
            {
                UtilityId = "theme-customizer",
                Title = "Appearance Lab",
                Description = "Compose readable utility/editor themes. Presets are curated starting points; the main workbench is for active theme iteration, typography, surfaces, and the experimental editor skin bridge.",
                Status = UtilityWindowTheme.ActiveThemeDisplayName,
                ShowHelp = true,
                ShowMinimizeTray = true,
                ShowMinimizeButton = true,
                Tint = UtilityWindowTheme.HeaderTint
            });

            DrawTopToolbar();
            DrawMainLayout();

            if (_fieldFlashUntil.Count > 0)
                Repaint();
        }

        private void DrawTopToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.HeaderTint, 0.20f, 0.07f, 6, 3)))
            {
                EditorGUILayout.LabelField("Quick Actions", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(86f));

                if (ThemedButton(new GUIContent("Readable Default", "Load the curated default utility theme."), UtilityWindowTheme.Blue, GUILayout.Width(118f), GUILayout.Height(22f)))
                    UtilityWindowTheme.ApplyPreset(UtilityWindowTheme.ThemePreset.PungentDefault);

                if (ThemedButton(new GUIContent("High Contrast", "Load the high-contrast dark theme."), UtilityWindowTheme.Cyan, GUILayout.Width(104f), GUILayout.Height(22f)))
                    UtilityWindowTheme.ApplyPreset(UtilityWindowTheme.ThemePreset.HighContrastDark);

                if (ThemedButton(new GUIContent("Try Variation", "Advance the current recipe seed and regenerate a nearby working-copy variation."), UtilityWindowTheme.Green, GUILayout.Width(98f), GUILayout.Height(22f)))
                    IterateGeneratedTheme();

                using (new EditorGUI.DisabledScope(!UtilityWindowTheme.ActiveThemeModified))
                {
                    if (ThemedButton(new GUIContent("Reset", "Discard working-copy edits and reload the selected preset."), UtilityWindowTheme.Neutral, GUILayout.Width(62f), GUILayout.Height(22f)))
                        UtilityWindowTheme.ResetActiveWorkingThemeToPreset();
                }

                GUILayout.FlexibleSpace();

                if (ThemedButton(new GUIContent("Open Style Explorer", "Open the experimental Theme Editor accessory for editor fonts, icons, and GUIStyle mappings."), UtilityWindowTheme.Purple, GUILayout.Width(106f), GUILayout.Height(22f)))
                    PungentEditorStyleExplorerWindow.ShowWindow();

                if (ThemedButton(new GUIContent("Apply Text/Fonts", "Apply reversible session-only GUIStyle text/font styling. Background hover styling is intentionally avoided."), UtilityWindowTheme.Amber, GUILayout.Width(116f), GUILayout.Height(22f)))
                {
                    if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Apply session GUIStyle text/font styling from the Theme Editor."))
                        PungentEditorGuiStyleBridge.ApplySessionThemeToAllStyles(false);
                }

                if (ThemedButton(new GUIContent("Repaint", "Repaint PungentFunk utility windows."), UtilityWindowTheme.Neutral, GUILayout.Width(70f), GUILayout.Height(22f)))
                    UtilityWindowTheme.RepaintUtilityWindows();
            }
        }

        private void DrawMainLayout()
        {
            float desiredRight = Mathf.Min(_rightWidth, Mathf.Max(240f, position.width * 0.32f));
            if (position.width - desiredRight < 560f)
                desiredRight = Mathf.Max(230f, position.width * 0.26f);

            _rightWidth = desiredRight;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawActiveWorkbench();
                UtilityWindowTheme.HorizontalResizeHandle(ref _rightWidth, 240f, Mathf.Min(380f, position.width * 0.40f), SavePrefs, "Resize settings/bridge rail", invertDelta: true);
                DrawSettingsRail();
            }
        }

        private void DrawPresetStack()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, padding: 5), GUILayout.Width(_leftWidth), GUILayout.ExpandHeight(true)))
            {
                UtilityThemePresetDefinition[] visiblePresets = GetFilteredVisiblePresets();
                UtilityWindowTheme.SectionTitle("Presets", UtilityWindowTheme.Neutral, visiblePresets.Length.ToString());

                EditorGUI.BeginChangeCheck();
                _search = EditorGUILayout.TextField(GUIContent.none, _search ?? string.Empty, UtilityWindowTheme.ToolbarSearchStyle);
                _presetCategoryFilter = (PresetCategoryFilter)EditorGUILayout.EnumPopup(new GUIContent("Category", "Optional inline grouping filter. Browsing does not require hard category switching."), _presetCategoryFilter);
                _presetToneFilter = (PresetToneFilter)EditorGUILayout.EnumPopup(new GUIContent("Tone", "Filter by light/dark/mixed presentation."), _presetToneFilter);
                _presetFamilyFilter = (PresetFamilyFilter)EditorGUILayout.EnumPopup(new GUIContent("Colour Family", "Optional mood/family filter for faster scanning."), _presetFamilyFilter);
                EditorGUILayout.LabelField("Judge raw preset readability with the experimental editor bridge and session GUIStyle overrides disabled; bridge output can distort this lab preview.", UtilityWindowTheme.MutedMiniLabelStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowPrefs.SetInt(PrefCategory, (int)_presetCategoryFilter);
                    UtilityWindowPrefs.SetInt(PrefToneFilter, (int)_presetToneFilter);
                    UtilityWindowPrefs.SetInt(PrefFamilyFilter, (int)_presetFamilyFilter);
                    UtilityWindowPrefs.SetString(PrefSearch, _search ?? string.Empty);
                }

                EditorGUILayout.Space(4f);
                _presetScroll = EditorGUILayout.BeginScrollView(_presetScroll);
                if (visiblePresets.Length == 0)
                {
                    EditorGUILayout.LabelField("No visible presets match this filter.", UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField("Archived presets are hidden until they pass readability/name-accuracy review.", UtilityWindowTheme.MutedMiniLabelStyle);
                }

                UtilityThemePresetCategory? currentCategory = null;
                for (int i = 0; i < visiblePresets.Length; i++)
                {
                    UtilityThemePresetDefinition preset = visiblePresets[i];
                    if (currentCategory != preset.Category)
                    {
                        currentCategory = preset.Category;
                        DrawPresetCategoryHeader(preset.Category);
                    }

                    DrawPresetRow(preset);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPresetRow(UtilityThemePresetDefinition preset)
        {
            bool active = preset.Preset == UtilityWindowTheme.ActivePreset;
            bool modified = active && UtilityWindowTheme.ActiveThemeModified;
            Color tint = active ? (modified ? UtilityWindowTheme.Amber : UtilityWindowTheme.Cyan) : preset.GetColor(UtilityWindowTheme.RolePrimary, UtilityWindowTheme.Blue);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, active ? 0.30f : 0.14f, active ? 0.12f : 0.06f, 4, 3)))
            {
                Rect swatchRect = GUILayoutUtility.GetRect(1f, 8f, GUILayout.ExpandWidth(true));
                DrawPresetSwatches(swatchRect, preset, compact: true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = preset.DisplayName + (modified ? " *" : string.Empty);
                    string tooltip = modified
                        ? "This preset is the source for the active working copy. Click to reload the original preset values."
                        : preset.Description;

                    if (GUILayout.Button(new GUIContent(label, tooltip), active ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft, GUILayout.Height(20f)))
                        UtilityWindowTheme.ApplyPreset(preset.Preset);

                    if (modified && GUILayout.Button(new GUIContent("Reset", "Discard the current working-copy edits and reload this preset exactly as defined in the preset library."), EditorStyles.miniButtonRight, GUILayout.Width(48f), GUILayout.Height(20f)))
                        UtilityWindowTheme.ResetActiveWorkingThemeToPreset();

                    string pill = active ? (modified ? "modified" : "on") : UtilityWindowTheme.GetContrastRating(GetPresetBodyContrast(preset));
                    Color pillColor = modified ? UtilityWindowTheme.Amber : active ? UtilityWindowTheme.Cyan : GetPresetBodyContrast(preset) >= 4.5f ? UtilityWindowTheme.Green : UtilityWindowTheme.Red;
                    UtilityWindowTheme.CountPill(pill, pillColor, modified ? 60f : 38f);
                }

                if (!string.IsNullOrEmpty(preset.Description))
                    EditorGUILayout.LabelField(preset.Description, UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(preset.Tone.ToString().ToLowerInvariant(), active ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 52f);
                    UtilityWindowTheme.CountPill(ObjectNames.NicifyVariableName(preset.ColourFamily.ToString()), tint, 88f);
                    string tags = string.Join(" � ", preset.Tags.Take(2).ToArray());
                    if (!string.IsNullOrEmpty(tags))
                        EditorGUILayout.LabelField(tags, UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private UtilityThemePresetDefinition[] GetFilteredVisiblePresets()
        {
            IEnumerable<UtilityThemePresetDefinition> presets = UtilityThemePresetLibrary.VisiblePresets;
            AdjustPresetBrowserSource(ref presets);

            return presets
                .Where(MatchesPresetFilters)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.DisplayName)
                .ToArray();
        }

        private bool MatchesPresetFilters(UtilityThemePresetDefinition preset)
        {
            if (preset == null || !preset.Matches(_search))
                return false;

            if (_presetCategoryFilter != PresetCategoryFilter.All && preset.Category != (UtilityThemePresetCategory)((int)_presetCategoryFilter - 1))
                return false;

            if (_presetToneFilter != PresetToneFilter.All && preset.Tone != (UtilityThemePresetTone)((int)_presetToneFilter - 1))
                return false;

            if (_presetFamilyFilter != PresetFamilyFilter.All && preset.ColourFamily != (UtilityThemePresetColourFamily)((int)_presetFamilyFilter - 1))
                return false;

            return true;
        }

        private void DrawPresetCategoryHeader(UtilityThemePresetCategory category)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(category.ToString()), UtilityWindowTheme.SubtitleStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("inline group", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(62f));
            }
        }

        private void DrawActiveWorkbench()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                _workbenchScroll = EditorGUILayout.BeginScrollView(_workbenchScroll);
                DrawActiveThemeHero();
                DrawWorkingCopyStatus();
                DrawThemeComposer();
                DrawPaletteSourcePanel();
                DrawAdvancedComposerPanel();
                DrawManualRoleWorkbench();
                DrawReadabilityWorkbench();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawActiveThemeHero()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.HeaderTint, 0.30f, 0.12f, 10, 5)))
            {
                UtilityWindowTheme.SectionTitle("Theme Preview Stage", UtilityWindowTheme.HeaderTint, UtilityWindowTheme.ActiveThemeDisplayName);
                EditorGUILayout.LabelField("A compact mock utility window. Use this as the primary judge for surface depth, text clarity, fields, states, and code/path styling.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.HeaderTint, 0.22f, 0.08f, 8, 4)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Pungent Utility Window", UtilityWindowTheme.TitleStyle, GUILayout.MinWidth(180f));
                        GUILayout.FlexibleSpace();
                        UtilityWindowTheme.CountPill(UtilityWindowTheme.ActiveThemeModified ? "modified" : "preset", UtilityWindowTheme.ActiveThemeModified ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 72f);
                    }

                    DrawPreviewToolbar();

                    using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Blue, 0.10f), 0.22f, 0.08f, 7, 4)))
                    {
                        EditorGUILayout.LabelField("Section Header", UtilityWindowTheme.SectionHeaderStyle);
                        EditorGUILayout.LabelField("Body text sample for normal descriptions and instructions. This should remain readable before you inspect individual colour roles.", UtilityWindowTheme.BodyStyle);
                        EditorGUILayout.LabelField("Muted helper text should be quieter, but never invisible or muddy.", UtilityWindowTheme.MutedMiniLabelStyle);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            DrawPreviewButton("Primary", UtilityWindowTheme.Blue);
                            DrawPreviewButton("Secondary", UtilityWindowTheme.Cyan);
                            DrawPreviewButton("Success", UtilityWindowTheme.Green);
                            DrawPreviewButton("Warning", UtilityWindowTheme.Amber);
                            DrawPreviewButton("Danger", UtilityWindowTheme.Red);
                        }

                        DrawPreviewRow("Selected row", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Blue, 0.42f), UtilityWindowTheme.TitleText);
                        DrawPreviewRow("Hover row", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Cyan, 0.24f), UtilityWindowTheme.TitleText);
                        DrawPreviewRow("Normal row", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Neutral, 0.12f), UtilityWindowTheme.CardText);
                    }
                }

                Rect swatchRect = GUILayoutUtility.GetRect(1f, 20f, GUILayout.ExpandWidth(true));
                DrawActiveRoleStrip(swatchRect);
            }
        }

        private void DrawPreviewToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Blue, 0.10f), 0.22f, 0.08f, 5, 2)))
            {
                DrawPreviewChip("Toolbar", UtilityWindowTheme.Blue, 70f);
                DrawPreviewChip("Toggle On", UtilityWindowTheme.Green, 82f);
                DrawPreviewField("Search / field", 145f);

                GUILayout.FlexibleSpace();

                GUIContent path = new GUIContent("/Assets/Editor/Theme.cs", "Preview of path/code typography.");
                EditorGUILayout.LabelField(path, UtilityWindowTheme.PathLabelStyle, GUILayout.Width(190f));
            }
        }

        private void DrawPreviewChip(string label, Color tint, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 22f, GUILayout.Width(width), GUILayout.Height(22f));
            EditorGUI.DrawRect(rect, Color.Lerp(UtilityWindowTheme.HeaderTint, tint, 0.42f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Color.Lerp(tint, UtilityWindowTheme.TitleText, 0.16f));

            Color previous = GUI.contentColor;
            GUI.contentColor = UtilityWindowTheme.TitleText;
            GUI.Label(new Rect(rect.x + 7f, rect.y + 2f, rect.width - 14f, rect.height - 4f), label, UtilityWindowTheme.MutedMiniLabelStyle);
            GUI.contentColor = previous;
        }

        private void DrawPreviewField(string label, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 22f, GUILayout.Width(width), GUILayout.Height(22f));
            Color field = Color.Lerp(UtilityWindowTheme.HeaderTint, Color.black, EditorGUIUtility.isProSkin ? 0.18f : 0.04f);
            EditorGUI.DrawRect(rect, field);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Color.Lerp(UtilityWindowTheme.Neutral, UtilityWindowTheme.Cyan, 0.24f));

            Color previous = GUI.contentColor;
            GUI.contentColor = UtilityWindowTheme.MutedText;
            GUI.Label(new Rect(rect.x + 7f, rect.y + 2f, rect.width - 14f, rect.height - 4f), label, UtilityWindowTheme.MutedMiniLabelStyle);
            GUI.contentColor = previous;
        }

        private void DrawPreviewButton(string label, Color tint)
        {
            DrawPreviewChip(label, tint, 82f);
        }

        private void DrawPreviewRow(string label, Color background, Color foreground)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, background);
            Color previous = GUI.contentColor;
            GUI.contentColor = foreground;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, rect.height - 4f), label, UtilityWindowTheme.BodyStyle);
            GUI.contentColor = previous;
        }

        private void DrawWorkingCopyStatus()
        {
            Color tint = UtilityWindowTheme.ActiveThemeModified ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green;
            string message = UtilityWindowTheme.ActiveThemeModified
                ? "Working copy active. Repair, composer, palette, and manual edits are changing only the active theme values stored in EditorPrefs; the built-in preset definition is unchanged."
                : "Preset source loaded. Repair/composer actions will create a modified working copy; reload/reset returns to the original preset values.";

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(tint, 0.16f, 0.07f, 5, 3)))
            {
                EditorGUILayout.LabelField(message, UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!UtilityWindowTheme.ActiveThemeModified))
                {
                    if (GUILayout.Button(new GUIContent("Reset to Preset", "Discard working-copy edits and reload the selected preset from UtilityThemePresetLibrary."), GUILayout.Width(112f), GUILayout.Height(20f)))
                        UtilityWindowTheme.ResetActiveWorkingThemeToPreset();
                }
            }
        }

        private void DrawThemeComposer()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green, 0.24f, 0.10f, 9, 5)))
            {
                UtilityWindowTheme.SectionTitle("Theme Recipe", UtilityWindowTheme.Green, "starter ? refine");
                EditorGUILayout.LabelField("Start from a theme, then refine it into your own. Starter themes now act as both preset selection and design direction.", UtilityWindowTheme.MutedMiniLabelStyle);

                DrawRecipeStarterSection();

                EditorGUILayout.Space(5f);

                DrawRecipeSettingSummary();

                EditorGUILayout.Space(5f);

                DrawRecipeRefinementControls();
                DrawRecipeActionButtons();

                DrawDeveloperPresetAuthoringPanel();
            }
        }

        partial void DrawDeveloperPresetAuthoringPanel();
        partial void AdjustPresetBrowserSource(ref IEnumerable<UtilityThemePresetDefinition> presets);

        private void DrawRecipeMoodCard(RecipeMood mood, string title, string subtitle, Color tint)
        {
            bool active = _activeRecipeMood == mood;
            Color panelTint = active ? tint : Color.Lerp(tint, UtilityWindowTheme.Neutral, 0.45f);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(panelTint, active ? 0.30f : 0.14f, active ? 0.12f : 0.05f, active ? 7 : 4, 3), GUILayout.MinWidth(112f)))
            {
                if (GUILayout.Button(new GUIContent(title, subtitle), UtilityWindowTheme.CardLabelStyle, GUILayout.Height(23f)))
                    ApplyRecipeMood(mood);

                EditorGUILayout.LabelField(subtitle, UtilityWindowTheme.MutedMiniLabelStyle);

                if (active)
                    UtilityWindowTheme.CountPill("active", tint, 52f);
                else
                    UtilityWindowTheme.CountPill("recipe", UtilityWindowTheme.Neutral, 52f);
            }
        }

        private void DrawRecipeRefinementControls()
        {
            float workbenchWidth = GetRecipeWorkbenchWidth();
            bool stackPanels = ShouldStackRecipeRefinementPanels(workbenchWidth);

            if (stackPanels)
            {
                float stackedPanelWidth = Mathf.Max(340f, workbenchWidth - 8f);

                using (DrawFieldFlashScope("recipe.core", UtilityWindowTheme.Cyan))
                    DrawCoreRefinementPanel(stackedPanelWidth, true);

                EditorGUILayout.Space(4f);

                using (DrawFieldFlashScope("recipe.surface", UtilityWindowTheme.Amber))
                    DrawReadabilityShapePanel(stackedPanelWidth, true);

                return;
            }

            float halfWidth = Mathf.Max(360f, (workbenchWidth - 10f) * 0.5f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (DrawFieldFlashScope("recipe.core", UtilityWindowTheme.Cyan))
                    DrawCoreRefinementPanel(halfWidth, true);

                GUILayout.Space(6f);

                using (DrawFieldFlashScope("recipe.surface", UtilityWindowTheme.Amber))
                    DrawReadabilityShapePanel(halfWidth, true);
            }
        }

        private float GetRecipeWorkbenchWidth()
        {
            return Mathf.Max(0f, position.width - _rightWidth - 54f);
        }

        private bool ShouldStackRecipeRefinementPanels(float workbenchWidth)
        {
            return workbenchWidth < 820f;
        }

        private GUILayoutOption[] GetRecipeRefinementPanelOptions(float width, bool fixedWidth)
        {
            return fixedWidth
                ? new[] { GUILayout.Width(width), GUILayout.MinWidth(340f) }
                : new[] { GUILayout.MinWidth(340f), GUILayout.ExpandWidth(true) };
        }

        private float GetRecipePanelContentWidth(float panelWidth)
        {
            // Accounts for panel padding, border, and the small gap between paired tiles.
            return Mathf.Max(280f, panelWidth - 24f);
        }

        private bool ShouldStackRecipeTiles(float panelContentWidth)
        {
            // Below this, two side-by-side tiles become cramped. Stack the tiles inside the panel.
            return panelContentWidth < 430f;
        }

        private float GetRecipeTileWidth(float panelContentWidth, bool stackTiles)
        {
            return stackTiles
                ? Mathf.Max(240f, panelContentWidth)
                : Mathf.Max(130f, (panelContentWidth - 6f) * 0.5f);
        }

        private GUILayoutOption[] GetRecipeTileOptions(float width)
        {
            return new[]
            {
        GUILayout.Width(width),
        GUILayout.MinWidth(120f)
    };
        }

        private void DrawCoreRefinementPanel(float panelWidth, bool fixedWidth)
        {
            EditorGUI.BeginChangeCheck();

            Color accent = UtilityWindowTheme.Blue;
            GUILayoutOption[] panelLayout = GetRecipeRefinementPanelOptions(panelWidth, fixedWidth);
            float contentWidth = GetRecipePanelContentWidth(panelWidth);
            bool stackTiles = ShouldStackRecipeTiles(contentWidth);
            float tileWidth = GetRecipeTileWidth(contentWidth, stackTiles);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.10f, 0.04f, 5, 3), panelLayout))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Core Refinements", UtilityWindowTheme.SubtitleStyle, GUILayout.Width(120f));
                    EditorGUILayout.LabelField("Colour family, action colour, depth, and energy.", UtilityWindowTheme.MutedMiniLabelStyle);
                }

                if (stackTiles)
                {
                    _generatorBase = DrawRecipeColourTile("Core Colour", "Main colour family used by Try Variation and Apply Refinements.", _generatorBase, UtilityWindowTheme.Cyan, tileWidth);
                    accent = DrawRecipeColourTile("Accent", "Primary action, selection, and interaction colour.", UtilityWindowTheme.Blue, UtilityWindowTheme.Blue, tileWidth);
                    _composerSurfaceDepth = DrawRecipeSliderTile("Surface Depth", "Flat", "Deep", "Controls how strongly panels, headers, and cards separate from the background.", _composerSurfaceDepth, 0f, 1f, UtilityWindowTheme.Blue, tileWidth);
                    _composerAccentIntensity = DrawRecipeSliderTile("Accent Energy", "Calm", "Vivid", "Controls how bright and assertive action colours feel.", _composerAccentIntensity, 0f, 1f, UtilityWindowTheme.Green, tileWidth);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _generatorBase = DrawRecipeColourTile("Core Colour", "Main colour family used by Try Variation and Apply Refinements.", _generatorBase, UtilityWindowTheme.Cyan, tileWidth);
                        accent = DrawRecipeColourTile("Accent", "Primary action, selection, and interaction colour.", UtilityWindowTheme.Blue, UtilityWindowTheme.Blue, tileWidth);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _composerSurfaceDepth = DrawRecipeSliderTile("Surface Depth", "Flat", "Deep", "Controls how strongly panels, headers, and cards separate from the background.", _composerSurfaceDepth, 0f, 1f, UtilityWindowTheme.Blue, tileWidth);
                        _composerAccentIntensity = DrawRecipeSliderTile("Accent Energy", "Calm", "Vivid", "Controls how bright and assertive action colours feel.", _composerAccentIntensity, 0f, 1f, UtilityWindowTheme.Green, tileWidth);
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                _generatorInfluence = _composerSurfaceDepth;
                UtilityWindowTheme.SetColor(UtilityWindowTheme.RolePrimary, accent);
                SaveGeneratorPrefs();
                ApplyComposerTuning();
            }
        }

        private void DrawReadabilityShapePanel(float panelWidth, bool fixedWidth)
        {
            EditorGUI.BeginChangeCheck();

            float density = UtilityWindowTheme.Density;
            GUILayoutOption[] panelLayout = GetRecipeRefinementPanelOptions(panelWidth, fixedWidth);
            float contentWidth = GetRecipePanelContentWidth(panelWidth);
            bool stackTiles = ShouldStackRecipeTiles(contentWidth);
            float tileWidth = GetRecipeTileWidth(contentWidth, stackTiles);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.10f, 0.04f, 5, 3), panelLayout))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Readability & Shape", UtilityWindowTheme.SubtitleStyle, GUILayout.Width(132f));
                    EditorGUILayout.LabelField("Contrast, helper text, borders, and spacing.", UtilityWindowTheme.MutedMiniLabelStyle);
                }

                if (stackTiles)
                {
                    _generatorTargetContrast = DrawRecipeSliderTile("Text Contrast", "Soft", "Strong", "Target contrast used by readability repair and composer balancing.", _generatorTargetContrast, 4.5f, 10f, UtilityWindowTheme.Cyan, tileWidth, "0.0");
                    _composerMutedStrength = DrawRecipeSliderTile("Helper Text", "Subtle", "Clear", "Controls how readable muted/helper text should remain.", _composerMutedStrength, 0f, 1f, UtilityWindowTheme.Neutral, tileWidth);
                    _composerBorderStrength = DrawRecipeSliderTile("Borders", "Soft", "Strong", "Controls border visibility and separation.", _composerBorderStrength, 0f, 1f, UtilityWindowTheme.Amber, tileWidth);
                    density = DrawRecipeSliderTile("Density", "Compact", "Spacious", "Controls spacing scale across utility windows.", density, 0.75f, 1.35f, UtilityWindowTheme.Purple, tileWidth, "0.00");
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _generatorTargetContrast = DrawRecipeSliderTile("Text Contrast", "Soft", "Strong", "Target contrast used by readability repair and composer balancing.", _generatorTargetContrast, 4.5f, 10f, UtilityWindowTheme.Cyan, tileWidth, "0.0");
                        _composerMutedStrength = DrawRecipeSliderTile("Helper Text", "Subtle", "Clear", "Controls how readable muted/helper text should remain.", _composerMutedStrength, 0f, 1f, UtilityWindowTheme.Neutral, tileWidth);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _composerBorderStrength = DrawRecipeSliderTile("Borders", "Soft", "Strong", "Controls border visibility and separation.", _composerBorderStrength, 0f, 1f, UtilityWindowTheme.Amber, tileWidth);
                        density = DrawRecipeSliderTile("Density", "Compact", "Spacious", "Controls spacing scale across utility windows.", density, 0.75f, 1.35f, UtilityWindowTheme.Purple, tileWidth, "0.00");
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                UtilityWindowTheme.Density = density;
                SaveGeneratorPrefs();
                ApplyComposerTuning();
            }
        }

        private Color DrawRecipeColourTile(string label, string tooltip, Color value, Color tint, float tileWidth)
        {
            using (new EditorGUILayout.VerticalScope(
                UtilityWindowTheme.PanelStyle(tint, 0.12f, 0.04f, 4, 2),
                GetRecipeTileOptions(tileWidth)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(label, tooltip), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(74f));
                    GUILayout.FlexibleSpace();

                    Rect swatch = GUILayoutUtility.GetRect(28f, 14f, GUILayout.Width(28f), GUILayout.Height(14f));
                    EditorGUI.DrawRect(swatch, value);
                    EditorGUI.DrawRect(new Rect(swatch.x, swatch.yMax - 1f, swatch.width, 1f), Color.Lerp(value, Color.white, 0.35f));
                }

                Rect colourRect = EditorGUILayout.GetControlRect(false, 20f, GUILayout.ExpandWidth(true));
                return EditorGUI.ColorField(colourRect, GUIContent.none, value, true, false, false);
            }
        }

        private float DrawRecipeSliderTile(string label, string lowLabel, string highLabel, string tooltip, float value, float min, float max, Color tint, float tileWidth, string valueFormat = "0%")
        {
            using (new EditorGUILayout.VerticalScope(
                UtilityWindowTheme.PanelStyle(tint, 0.10f, 0.035f, 4, 2),
                GetRecipeTileOptions(tileWidth)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(label, tooltip), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(74f));
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(FormatRecipeSliderValue(value, min, max, valueFormat), tint, 54f);
                }

                Rect sliderRect = EditorGUILayout.GetControlRect(false, 18f, GUILayout.ExpandWidth(true));
                value = EditorGUI.Slider(sliderRect, GUIContent.none, value, min, max);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(lowLabel, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(62f));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(highLabel, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(70f));
                }

                return value;
            }
        }

        private string FormatRecipeSliderValue(float value, float min, float max, string format)
        {
            if (format == "0%")
                return Mathf.RoundToInt(Mathf.InverseLerp(min, max, value) * 100f) + "%";

            return value.ToString(format);
        }

        private void DrawRecipeActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.04f, 5, 2)))
            {
                if (ThemedButton(new GUIContent("Apply Refinements", "Apply the current refinement values and typography to the active working copy."), UtilityWindowTheme.Green, GUILayout.Height(27f)))
                {
                    ApplyComposerTuning();
                    ApplyRecipeTypography(_activeRecipeMood);
                }

                if (ThemedButton(new GUIContent("Try Variation", "Advance the seed and generate a nearby variation from the current recipe."), UtilityWindowTheme.Cyan, GUILayout.Height(27f)))
                    IterateGeneratedTheme();

                if (ThemedButton(new GUIContent("Rebalance Readability", "Repair text and semantic contrast in the active working copy."), UtilityWindowTheme.Amber, GUILayout.Height(27f)))
                    RepairCurrentThemeReadability();

                using (new EditorGUI.DisabledScope(!UtilityWindowTheme.ActiveThemeModified))
                {
                    if (ThemedButton(new GUIContent("Reset to Starter", "Discard working-copy edits and reload the selected starter theme."), UtilityWindowTheme.Neutral, GUILayout.Height(27f)))
                        UtilityWindowTheme.ResetActiveWorkingThemeToPreset();
                }
            }
        }

        private void DrawRecipeStarterSection()
        {
            UtilityThemePresetDefinition[] visiblePresets = GetFilteredVisiblePresets();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.14f, 0.06f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Start From", UtilityWindowTheme.SubtitleStyle, GUILayout.Width(90f));
                    EditorGUILayout.LabelField("Choose a starter theme. It applies colours, surfaces, density, radius, typography, and the internal design direction for future variations.", UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(visiblePresets.Length + " shown", UtilityWindowTheme.Cyan, 70f);
                }

                EditorGUI.BeginChangeCheck();

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStarterFilterTextField(
                        "Search",
                        new GUIContent("Search starter themes by name, description, or tag."),
                        ref _search,
                        170f,
                        260f);

                    _presetCategoryFilter = DrawStarterFilterEnum(
                        "Category",
                        new GUIContent("Filter starter themes by category."),
                        _presetCategoryFilter,
                        106f);

                    _presetToneFilter = DrawStarterFilterEnum(
                        "Tone",
                        new GUIContent("Filter starter themes by tone."),
                        _presetToneFilter,
                        82f);

                    _presetFamilyFilter = DrawStarterFilterEnum(
                        "Family",
                        new GUIContent("Filter starter themes by colour family."),
                        _presetFamilyFilter,
                        112f);

                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(52f)))
                    {
                        GUILayout.Label(GUIContent.none, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Height(14f));

                        if (GUILayout.Button(new GUIContent("Clear", "Clear starter theme filters."), EditorStyles.miniButton, GUILayout.Height(20f)))
                        {
                            _search = string.Empty;
                            _presetCategoryFilter = PresetCategoryFilter.All;
                            _presetToneFilter = PresetToneFilter.All;
                            _presetFamilyFilter = PresetFamilyFilter.All;
                            GUI.changed = true;
                        }
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowPrefs.SetInt(PrefCategory, (int)_presetCategoryFilter);
                    UtilityWindowPrefs.SetInt(PrefToneFilter, (int)_presetToneFilter);
                    UtilityWindowPrefs.SetInt(PrefFamilyFilter, (int)_presetFamilyFilter);
                    UtilityWindowPrefs.SetString(PrefSearch, _search ?? string.Empty);
                }

                EditorGUILayout.Space(4f);

                if (visiblePresets.Length == 0)
                {
                    EditorGUILayout.LabelField("No visible starter themes match the current filters.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                _starterThemeScroll.y = 0f;

                _starterThemeScroll = EditorGUILayout.BeginScrollView(
                    _starterThemeScroll,
                    true,
                    false,
                    GUI.skin.horizontalScrollbar,
                    GUIStyle.none,
                    GUIStyle.none,
                    GUILayout.Height(132f));

                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(104f)))
                {
                    for (int i = 0; i < visiblePresets.Length; i++)
                        DrawRecipeStarterCard(visiblePresets[i]);
                }

                EditorGUILayout.EndScrollView();

                _starterThemeScroll.y = 0f;
            }
        }

        private void DrawStarterFilterTextField(string label, GUIContent tooltip, ref string value, float minWidth, float maxWidth)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(minWidth), GUILayout.MaxWidth(maxWidth)))
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip.tooltip), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Height(14f));
                value = EditorGUILayout.TextField(GUIContent.none, value ?? string.Empty, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.Height(20f));
            }
        }

        private T DrawStarterFilterEnum<T>(string label, GUIContent tooltip, T value, float width) where T : Enum
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip.tooltip), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Height(14f));
                return (T)EditorGUILayout.EnumPopup(GUIContent.none, value, GUILayout.Height(20f));
            }
        }

        private struct ThemeRecipeSnapshot
        {
            public Color primary;
            public Color secondary;
            public Color header;
            public Color success;
            public Color warning;
            public Color danger;
            public float density;
            public int radius;
            public float borderOpacity;
        }

        private void ApplyRecipeStarterPreset(UtilityThemePresetDefinition preset)
        {
            if (preset == null)
                return;

            ThemeRecipeSnapshot before = CaptureThemeRecipeSnapshot();

            UtilityWindowTheme.ApplyPreset(preset.Preset);
            _activeRecipeMood = InferRecipeMoodFromPreset(preset);
            SyncRecipeControlsFromPreset(preset);
            SaveGeneratorPrefs();

            ThemeRecipeSnapshot after = CaptureThemeRecipeSnapshot();
            FlashChangedRecipeFields(before, after);
        }

        private ThemeRecipeSnapshot CaptureThemeRecipeSnapshot()
        {
            return new ThemeRecipeSnapshot
            {
                primary = UtilityWindowTheme.Blue,
                secondary = UtilityWindowTheme.Cyan,
                header = UtilityWindowTheme.HeaderTint,
                success = UtilityWindowTheme.Green,
                warning = UtilityWindowTheme.Amber,
                danger = UtilityWindowTheme.Red,
                density = UtilityWindowTheme.Density,
                radius = UtilityWindowTheme.PanelCornerRadius,
                borderOpacity = UtilityWindowTheme.PanelBorderOpacity
            };
        }

        private void SyncRecipeControlsFromPreset(UtilityThemePresetDefinition preset)
        {
            Color primary = preset.GetColor(UtilityWindowTheme.RolePrimary, UtilityWindowTheme.Blue);
            Color secondary = preset.GetColor(UtilityWindowTheme.RoleSecondary, UtilityWindowTheme.Cyan);
            Color header = preset.GetColor(UtilityWindowTheme.RoleHeader, UtilityWindowTheme.HeaderTint);

            _generatorBase = primary;
            _generatorInfluence = Mathf.Clamp01(preset.PanelAlphaDark);
            _composerSurfaceDepth = Mathf.InverseLerp(0.18f, 0.42f, preset.PanelAlphaDark);
            _composerBorderStrength = Mathf.InverseLerp(0.12f, 0.88f, preset.PanelBorderOpacity);
            _composerMutedStrength = 0.54f;
            _composerPreferLight = preset.Tone == UtilityThemePresetTone.Light;

            Color.RGBToHSV(primary, out _, out float primarySaturation, out float primaryValue);
            Color.RGBToHSV(secondary, out _, out float secondarySaturation, out _);
            Color.RGBToHSV(header, out _, out _, out float headerValue);

            _composerSaturation = Mathf.Clamp01((primarySaturation + secondarySaturation) * 0.5f);
            _composerAccentIntensity = Mathf.Clamp01(Mathf.Lerp(primaryValue, 1f - headerValue, 0.35f));

            switch (_activeRecipeMood)
            {
                case RecipeMood.Cyber:
                    _generatorHarmony = ThemeHarmonyMode.Monochromatic;
                    break;
                case RecipeMood.Retro:
                    _generatorHarmony = ThemeHarmonyMode.SplitComplementary;
                    break;
                case RecipeMood.Natural:
                    _generatorHarmony = ThemeHarmonyMode.Analogous;
                    break;
                case RecipeMood.Cinematic:
                    _generatorHarmony = ThemeHarmonyMode.Monochromatic;
                    break;
                default:
                    _generatorHarmony = ThemeHarmonyMode.Contextual;
                    break;
            }
        }

        private RecipeMood InferRecipeMoodFromPreset(UtilityThemePresetDefinition preset)
        {
            if (preset == null)
                return RecipeMood.Professional;

            string haystack = (preset.DisplayName + " " + preset.Category + " " + preset.ColourFamily + " " + string.Join(" ", preset.Tags ?? Array.Empty<string>())).ToLowerInvariant();

            if (ContainsRecipeToken(haystack, "cyber", "terminal", "console", "computer", "tech", "neon"))
                return RecipeMood.Cyber;

            if (ContainsRecipeToken(haystack, "retro", "arcade", "vapor", "synth", "pixel", "80s"))
                return RecipeMood.Retro;

            if (ContainsRecipeToken(haystack, "forest", "earth", "spring", "natural", "moss", "garden", "water", "glacier", "alpine", "winter", "autumn"))
                return RecipeMood.Natural;

            if (ContainsRecipeToken(haystack, "noir", "cinematic", "vintage", "sepia", "golden", "film", "archive", "midnight"))
                return RecipeMood.Cinematic;

            return RecipeMood.Professional;
        }

        private static bool ContainsRecipeToken(string haystack, params string[] tokens)
        {
            if (string.IsNullOrEmpty(haystack) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && haystack.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private void FlashChangedRecipeFields(ThemeRecipeSnapshot before, ThemeRecipeSnapshot after)
        {
            FlashField("recipe.summary");
            FlashField("recipe.starter");

            if (ColourDistance(before.primary, after.primary) > 0.025f || ColourDistance(before.secondary, after.secondary) > 0.025f)
                FlashField("recipe.core");

            if (ColourDistance(before.header, after.header) > 0.025f || Mathf.Abs(before.density - after.density) > 0.01f || before.radius != after.radius || Mathf.Abs(before.borderOpacity - after.borderOpacity) > 0.01f)
                FlashField("recipe.surface");

            if (ColourDistance(before.success, after.success) > 0.025f || ColourDistance(before.warning, after.warning) > 0.025f || ColourDistance(before.danger, after.danger) > 0.025f)
                FlashField("recipe.semantic");

            FlashField("recipe.preview");
            FlashField("recipe.typography");
        }

        private void FlashField(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _fieldFlashUntil[key] = EditorApplication.timeSinceStartup + FieldFlashDuration;
            Repaint();
        }

        private bool IsFieldFlashing(string key, out float t)
        {
            t = 0f;

            if (string.IsNullOrEmpty(key) || !_fieldFlashUntil.TryGetValue(key, out double until))
                return false;

            double remaining = until - EditorApplication.timeSinceStartup;
            if (remaining <= 0d)
            {
                _fieldFlashUntil.Remove(key);
                return false;
            }

            t = Mathf.Clamp01((float)(remaining / FieldFlashDuration));
            return true;
        }

        private IDisposable DrawFieldFlashScope(string key, Color tint)
        {
            if (IsFieldFlashing(key, out float t))
            {
                float fill = Mathf.Lerp(0.08f, 0.34f, t);
                float border = Mathf.Lerp(0.04f, 0.16f, t);
                return new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, fill, border, 6, 2));
            }

            return new EditorGUILayout.VerticalScope(GUIStyle.none);
        }

        private void DrawRecipeStarterCard(UtilityThemePresetDefinition preset)
        {
            bool active = preset.Preset == UtilityWindowTheme.ActivePreset;
            bool modified = active && UtilityWindowTheme.ActiveThemeModified;
            Color tint = active ? (modified ? UtilityWindowTheme.Amber : UtilityWindowTheme.Cyan) : preset.GetColor(UtilityWindowTheme.RolePrimary, UtilityWindowTheme.Blue);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, active ? 0.30f : 0.13f, active ? 0.12f : 0.05f, active ? 7 : 4, 3), GUILayout.Width(154f), GUILayout.Height(88f)))
            {
                Rect swatchRect = GUILayoutUtility.GetRect(1f, 9f, GUILayout.ExpandWidth(true));
                DrawPresetSwatches(swatchRect, preset, compact: true);

                string title = preset.DisplayName + (modified ? " *" : string.Empty);
                string tooltip = string.IsNullOrEmpty(preset.Description)
                    ? "Start from this theme."
                    : preset.Description;

                if (GUILayout.Button(new GUIContent(title, tooltip), active ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Height(22f)))
                    ApplyRecipeStarterPreset(preset);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(active ? (modified ? "modified" : "active") : ObjectNames.NicifyVariableName(preset.ColourFamily.ToString()), active ? tint : UtilityWindowTheme.Neutral, active ? 70f : 92f);
                    UtilityWindowTheme.CountPill(preset.Tone.ToString().ToLowerInvariant(), active ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 48f);
                }

                if (!string.IsNullOrEmpty(preset.Description))
                {
                    string compactDescription = preset.Description.Length > 54 ? preset.Description.Substring(0, 51) + "..." : preset.Description;
                    EditorGUILayout.LabelField(compactDescription, UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawRecipeSettingSummary()
        {
            using (DrawFieldFlashScope("recipe.summary", UtilityWindowTheme.Cyan))
            {
                using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.05f, 5, 2)))
                {
                    UtilityWindowTheme.CountPill("Starter: " + UtilityWindowTheme.GetPresetDisplayName(UtilityWindowTheme.ActivePreset), UtilityWindowTheme.Cyan, 172f);
                    UtilityWindowTheme.CountPill(ObjectNames.NicifyVariableName(_activeRecipeMood.ToString()), UtilityWindowTheme.Purple, 104f);
                    UtilityWindowTheme.CountPill("Depth " + Mathf.RoundToInt(_composerSurfaceDepth * 100f) + "%", UtilityWindowTheme.Blue, 78f);
                    UtilityWindowTheme.CountPill("Energy " + Mathf.RoundToInt(_composerAccentIntensity * 100f) + "%", UtilityWindowTheme.Green, 82f);
                    UtilityWindowTheme.CountPill("Borders " + Mathf.RoundToInt(_composerBorderStrength * 100f) + "%", UtilityWindowTheme.Amber, 88f);
                    UtilityWindowTheme.CountPill("Density " + UtilityWindowTheme.Density.ToString("0.00"), UtilityWindowTheme.Neutral, 88f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void ApplyRecipeMood(RecipeMood mood)
        {
            _activeRecipeMood = mood;

            switch (mood)
            {
                case RecipeMood.Cyber:
                    _generatorBase = new Color(0.22f, 1.00f, 0.10f);
                    _generatorHarmony = ThemeHarmonyMode.Monochromatic;
                    _composerSaturation = 0.94f;
                    _composerAccentIntensity = 0.94f;
                    _composerSurfaceDepth = 0.82f;
                    _composerBorderStrength = 0.88f;
                    _composerMutedStrength = 0.62f;
                    UtilityWindowTheme.Density = 0.90f;
                    UtilityWindowTheme.PanelCornerRadius = 1;
                    break;

                case RecipeMood.Retro:
                    _generatorBase = new Color(0.00f, 0.78f, 1.00f);
                    _generatorHarmony = ThemeHarmonyMode.SplitComplementary;
                    _composerSaturation = 0.86f;
                    _composerAccentIntensity = 0.88f;
                    _composerSurfaceDepth = 0.72f;
                    _composerBorderStrength = 0.72f;
                    _composerMutedStrength = 0.58f;
                    UtilityWindowTheme.Density = 0.96f;
                    UtilityWindowTheme.PanelCornerRadius = 3;
                    break;

                case RecipeMood.Natural:
                    _generatorBase = new Color(0.36f, 0.72f, 0.38f);
                    _generatorHarmony = ThemeHarmonyMode.Analogous;
                    _composerSaturation = 0.60f;
                    _composerAccentIntensity = 0.62f;
                    _composerSurfaceDepth = 0.62f;
                    _composerBorderStrength = 0.52f;
                    _composerMutedStrength = 0.54f;
                    UtilityWindowTheme.Density = 1.04f;
                    UtilityWindowTheme.PanelCornerRadius = 6;
                    break;

                case RecipeMood.Cinematic:
                    _generatorBase = new Color(0.76f, 0.62f, 0.39f);
                    _generatorHarmony = ThemeHarmonyMode.Monochromatic;
                    _composerSaturation = 0.48f;
                    _composerAccentIntensity = 0.60f;
                    _composerSurfaceDepth = 0.82f;
                    _composerBorderStrength = 0.58f;
                    _composerMutedStrength = 0.50f;
                    UtilityWindowTheme.Density = 1.02f;
                    UtilityWindowTheme.PanelCornerRadius = 2;
                    break;

                default:
                    _generatorBase = new Color(0.27f, 0.47f, 0.82f);
                    _generatorHarmony = ThemeHarmonyMode.Contextual;
                    _composerSaturation = 0.54f;
                    _composerAccentIntensity = 0.58f;
                    _composerSurfaceDepth = 0.56f;
                    _composerBorderStrength = 0.48f;
                    _composerMutedStrength = 0.52f;
                    UtilityWindowTheme.Density = 1.00f;
                    UtilityWindowTheme.PanelCornerRadius = 5;
                    break;
            }

            _generatorInfluence = _composerSurfaceDepth;
            _composerPreferLight = false;

            SaveGeneratorPrefs();
            GenerateTheme(false);
            ApplyComposerTuning();
            ApplyRecipeTypography(mood);
        }

        private void ApplyRecipeTypography(RecipeMood mood)
        {
            UtilityWindowTheme.ApplyFontHints(BuildRecipeFontHints(mood));
        }

        private Dictionary<UtilityWindowTheme.TextRole, string[]> BuildRecipeFontHints(RecipeMood mood)
        {
            var hints = new Dictionary<UtilityWindowTheme.TextRole, string[]>();

            string[] heading;
            string[] subheading;
            string[] body;
            string[] path;

            switch (mood)
            {
                case RecipeMood.Cyber:
                    heading = new[] { "Perfect DOS VGA 437", "PC Senior", "Flexi_IBM_VGA_True_437", "Flexi IBM VGA True", "Terminal Grotesque", "Manaspace" };
                    subheading = new[] { "PC Senior", "Perfect DOS VGA 437", "Terminal Grotesque", "Manaspace", "Open Sans", "Roboto" };
                    body = new[] { "Open Sans", "Roboto", "Lato", "Spartan MB", "Montserrat" };
                    path = new[] { "Perfect DOS VGA 437", "PC Senior", "Flexi_IBM_VGA_True_437", "Flexi IBM VGA True", "Manaspace", "Terminal Grotesque" };
                    break;

                case RecipeMood.Retro:
                    heading = new[] { "Kongtext", "Yoster Island", "Riffic Free", "Slackey", "Bangers", "Perfect DOS VGA 437" };
                    subheading = new[] { "Yoster Island", "Kongtext", "Riffic Free", "Slackey", "Bangers", "Open Sans" };
                    body = new[] { "Open Sans", "Roboto", "Lato", "Spartan MB", "Montserrat" };
                    path = new[] { "Kongtext", "Yoster Island", "Perfect DOS VGA 437", "PC Senior", "Manaspace" };
                    break;

                case RecipeMood.Natural:
                    heading = new[] { "Patua One", "Chelsea Market", "Spartan MB", "Montserrat", "Open Sans" };
                    subheading = new[] { "Spartan MB", "Patua One", "Chelsea Market", "Open Sans", "Lato" };
                    body = new[] { "Lato", "Open Sans", "Roboto", "Spartan MB", "Montserrat" };
                    path = new[] { "PC Senior", "Perfect DOS VGA 437", "Terminal Grotesque", "Manaspace" };
                    break;

                case RecipeMood.Cinematic:
                    heading = new[] { "GFS Didot", "Libre Baskerville", "Special Elite", "IM FELL DW Pica", "Montserrat" };
                    subheading = new[] { "Libre Baskerville", "GFS Didot", "Special Elite", "Open Sans", "Lato" };
                    body = new[] { "Lato", "Open Sans", "Roboto", "Libre Baskerville", "Montserrat" };
                    path = new[] { "Special Elite", "PC Senior", "Perfect DOS VGA 437", "Terminal Grotesque" };
                    break;

                default:
                    heading = new[] { "Montserrat", "Spartan MB", "Open Sans", "Roboto", "Lato" };
                    subheading = new[] { "Spartan MB", "Montserrat", "Open Sans", "Roboto", "Lato" };
                    body = new[] { "Open Sans", "Roboto", "Lato", "Spartan MB", "Montserrat" };
                    path = new[] { "PC Senior", "Perfect DOS VGA 437", "Flexi_IBM_VGA_True_437", "Manaspace", "Terminal Grotesque" };
                    break;
            }

            AddFontHint(hints, "Heading", heading);
            AddFontHint(hints, "Subheading", subheading);
            AddFontHint(hints, "Body", body);
            AddFontHint(hints, "Muted", body);
            AddFontHint(hints, "Link", body);
            AddFontHint(hints, "Field", body);
            AddFontHint(hints, "Path", path);
            AddFontHint(hints, "Code", path);

            return hints;
        }

        private void AddFontHint(Dictionary<UtilityWindowTheme.TextRole, string[]> hints, string roleName, string[] fonts)
        {
            if (Enum.TryParse(roleName, out UtilityWindowTheme.TextRole role))
                hints[role] = fonts;
        }

        private void DrawAdvancedComposerPanel()
        {
            _showAdvancedComposer = EditorGUILayout.Foldout(_showAdvancedComposer, "Advanced Colour Math", true);
            UtilityWindowPrefs.SetBool(PrefShowAdvancedComposer, _showAdvancedComposer);
            if (!_showAdvancedComposer)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.16f, 0.07f, 7, 5)))
            {
                UtilityWindowTheme.SectionTitle("Advanced Colour Math", UtilityWindowTheme.Blue, "optional");
                EditorGUILayout.LabelField("Use these when you want exact control over harmony mode, seed, generation bias, and palette-hook fallback behaviour.", UtilityWindowTheme.MutedMiniLabelStyle);

                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _generatorHarmony = (ThemeHarmonyMode)EditorGUILayout.EnumPopup(new GUIContent("Colour Relationship", "Harmony rule for controlled iteration."), _generatorHarmony, GUILayout.MinWidth(170f));
                    _generatorSeed = EditorGUILayout.IntField(new GUIContent("Seed", "Deterministic variation seed."), _generatorSeed, GUILayout.MinWidth(120f));
                    _generatorInfluence = EditorGUILayout.Slider(new GUIContent("Harmony Influence", "How strongly the harmony relationship affects support colours."), _generatorInfluence, 0f, 1f, GUILayout.MinWidth(170f));
                    _composerPreferLight = EditorGUILayout.ToggleLeft(new GUIContent("Allow Light Build", "Advanced option: allow generation/palette mapping to bias toward light surfaces."), _composerPreferLight, GUILayout.Width(124f));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _composerSaturation = EditorGUILayout.Slider(new GUIContent("Colour Strength", "Muted to vivid generated/support colours."), _composerSaturation, 0f, 1f, GUILayout.MinWidth(170f));
                    _usePaletteDesignerIfAvailable = EditorGUILayout.ToggleLeft(new GUIContent("Use Palette Generator Hook", "Uses the Palette Designer generation utility if the package is present; otherwise falls back to built-in harmony generation."), _usePaletteDesignerIfAvailable, GUILayout.Width(190f));
                }
                if (EditorGUI.EndChangeCheck())
                    SaveGeneratorPrefs();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ThemedButton(new GUIContent("Generate From Advanced", "Generate from the advanced harmony/seed settings."), UtilityWindowTheme.Green, GUILayout.Height(24f)))
                        GenerateTheme(false);
                    if (ThemedButton(new GUIContent("Random Balanced", "Choose a readable random core colour and balanced harmony."), UtilityWindowTheme.Purple, GUILayout.Height(24f)))
                        RandomiseBalancedTheme();
                }

                DrawComposerSurfacePreviewGrid();
            }
        }

        private void DrawReadabilityWorkbench()
        {
            _showReadabilityAudit = EditorGUILayout.Foldout(_showReadabilityAudit, "Readability Audit", true);
            UtilityWindowPrefs.SetBool(PrefShowReadabilityAudit, _showReadabilityAudit);
            if (!_showReadabilityAudit)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.18f, 0.08f, 8, 5)))
            {
                UtilityWindowTheme.SectionTitle("Readability Audit", UtilityWindowTheme.Amber, "contextual text");
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawTextPairBadge("Title/Header", UtilityWindowTheme.TitleText, UtilityWindowTheme.HeaderTint);
                    DrawTextPairBadge("Body/Header", UtilityWindowTheme.CardText, UtilityWindowTheme.HeaderTint);
                    DrawTextPairBadge("Muted/Header", UtilityWindowTheme.MutedText, UtilityWindowTheme.HeaderTint);
                    DrawTextPairBadge("Light/Header", UtilityWindowTheme.LightText, UtilityWindowTheme.HeaderTint);
                    DrawTextPairBadge("Dark/Header", UtilityWindowTheme.DarkText, UtilityWindowTheme.HeaderTint);
                }

                EditorGUILayout.Space(4f);
                DrawContrastPair("Success vs Warning", UtilityWindowTheme.Green, UtilityWindowTheme.Amber, compareColours: true);
                DrawContrastPair("Warning vs Danger", UtilityWindowTheme.Amber, UtilityWindowTheme.Red, compareColours: true);
                DrawContrastPair("Primary vs Header", UtilityWindowTheme.Blue, UtilityWindowTheme.HeaderTint, compareColours: false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Repair Text Roles (Copy)", UtilityWindowTheme.Amber, GUILayout.Height(24f)))
                        RepairTextRolesForCurrentHeader();

                    if (UtilityWindowTheme.TintedButton("Repair Core Roles (Copy)", UtilityWindowTheme.Green, GUILayout.Height(24f)))
                        RepairCurrentThemeReadability();
                }
            }
        }

        private void DrawComposerSurfacePreviewGrid()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.14f, 0.06f, 6, 4)))
            {
                UtilityWindowTheme.SectionTitle("Live Surface Preview", UtilityWindowTheme.Neutral, "window ? status");
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSurfacePreviewChip("Window", UtilityWindowTheme.HeaderTint, UtilityWindowTheme.TitleText);
                    DrawSurfacePreviewChip("Panel", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Neutral, 0.16f), UtilityWindowTheme.CardText);
                    DrawSurfacePreviewChip("Header", UtilityWindowTheme.HeaderTint, UtilityWindowTheme.TitleText);
                    DrawSurfacePreviewChip("Card", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Blue, 0.10f), UtilityWindowTheme.CardText);
                    DrawSurfacePreviewChip("Field", Color.Lerp(UtilityWindowTheme.Neutral, UtilityWindowTheme.HeaderTint, 0.40f), UtilityWindowTheme.CardText);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSurfacePreviewChip("Toolbar", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Blue, 0.08f), UtilityWindowTheme.TitleText);
                    DrawSurfacePreviewChip("Selected", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Blue, 0.42f), UtilityWindowTheme.TitleText);
                    DrawSurfacePreviewChip("Hover", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Cyan, 0.24f), UtilityWindowTheme.TitleText);
                    DrawSurfacePreviewChip("Code / Path", Color.Lerp(UtilityWindowTheme.HeaderTint, UtilityWindowTheme.Neutral, 0.22f), UtilityWindowTheme.PathText);
                    DrawSurfacePreviewChip("Muted Text", UtilityWindowTheme.HeaderTint, UtilityWindowTheme.MutedText);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSurfacePreviewChip("Success", UtilityWindowTheme.Green, UtilityWindowTheme.GetContrastRatio(UtilityWindowTheme.DarkText, UtilityWindowTheme.Green) >= UtilityWindowTheme.GetContrastRatio(UtilityWindowTheme.LightText, UtilityWindowTheme.Green) ? UtilityWindowTheme.DarkText : UtilityWindowTheme.LightText);
                    DrawSurfacePreviewChip("Warning", UtilityWindowTheme.Amber, UtilityWindowTheme.GetContrastRatio(UtilityWindowTheme.DarkText, UtilityWindowTheme.Amber) >= UtilityWindowTheme.GetContrastRatio(UtilityWindowTheme.LightText, UtilityWindowTheme.Amber) ? UtilityWindowTheme.DarkText : UtilityWindowTheme.LightText);
                    DrawSurfacePreviewChip("Danger", UtilityWindowTheme.Red, UtilityWindowTheme.GetContrastRatio(UtilityWindowTheme.DarkText, UtilityWindowTheme.Red) >= UtilityWindowTheme.GetContrastRatio(UtilityWindowTheme.LightText, UtilityWindowTheme.Red) ? UtilityWindowTheme.DarkText : UtilityWindowTheme.LightText);
                }
            }
        }

        private void DrawSurfacePreviewChip(string label, Color background, Color foreground)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(background, 0.28f, 0.08f, 5, 3), GUILayout.MinWidth(92f)))
            {
                Color previous = GUI.contentColor;
                GUI.contentColor = foreground;
                EditorGUILayout.LabelField(label, UtilityWindowTheme.BodyStyle);
                GUI.contentColor = previous;
            }
        }

        private void DrawPaletteSourcePanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.15f, 0.08f, 7, 5)))
            {
                UtilityWindowTheme.SectionTitle("Palette Source", UtilityWindowTheme.Amber, _availablePalettes.Count > 0 ? _availablePalettes.Count + " found" : "manual");
                EditorGUILayout.LabelField("Choose a palette source, preview its swatches, then map it into theme roles. Project scanning is preferred over requiring the palette asset to be selected.", UtilityWindowTheme.MutedMiniLabelStyle);

                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _paletteSourceMode = (PaletteSourceMode)EditorGUILayout.EnumPopup(new GUIContent("Source", "None uses the manual recipe. Selected Project Asset reads the current selection. Discovered Palette uses the project scan dropdown."), _paletteSourceMode, GUILayout.MinWidth(180f));
                    _paletteBuildMode = (PaletteThemeBuildMode)EditorGUILayout.EnumPopup(new GUIContent("Mapping", "How palette swatches are translated into theme roles."), _paletteBuildMode, GUILayout.MinWidth(170f));
                    if (GUILayout.Button(new GUIContent("Refresh Palettes", "Scan project assets for PungentColourPaletteSO-style assets."), GUILayout.Width(116f)))
                        RefreshAvailablePalettes(true);
                    if (GUILayout.Button(new GUIContent("Open Palette Designer", "Open the Palette Designer window if it is present."), GUILayout.Width(130f)))
                        OpenPaletteDesignerIfAvailable();
                }
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                List<PaletteRoleColor> palette = null;
                string sourceLabel = string.Empty;

                if (_paletteSourceMode == PaletteSourceMode.DiscoveredPalette)
                {
                    if (_availablePalettes.Count > 0)
                    {
                        string[] names = _availablePalettes.Select(p => p.displayName).ToArray();
                        int nextIndex = EditorGUILayout.Popup(new GUIContent("Palette", "Project palettes discovered by AssetDatabase scan."), Mathf.Clamp(_selectedPaletteIndex, 0, _availablePalettes.Count - 1), names);
                        if (nextIndex != _selectedPaletteIndex)
                        {
                            _selectedPaletteIndex = nextIndex;
                            _selectedPalettePath = _availablePalettes[_selectedPaletteIndex].assetPath;
                            UtilityWindowPrefs.SetString(PrefSelectedPalettePath, _selectedPalettePath ?? string.Empty);
                        }

                        PaletteSourceInfo source = _availablePalettes[Mathf.Clamp(_selectedPaletteIndex, 0, _availablePalettes.Count - 1)];
                        palette = source.colours;
                        sourceLabel = source.displayName;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No palette assets found. Create one in Palette Designer or use the selected-asset fallback.", UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
                else if (_paletteSourceMode == PaletteSourceMode.SelectedProjectAsset)
                {
                    if (TryReadSelectedPalette(out palette) && palette.Count > 0)
                        sourceLabel = Selection.activeObject != null ? Selection.activeObject.name : "Selected Palette";
                    else
                        EditorGUILayout.LabelField("Select a PungentColourPaletteSO-style asset with swatches, or switch to Discovered Palette.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                else
                {
                    EditorGUILayout.LabelField("Palette mapping is disabled. Use the Theme Recipe above for direct theme design.", UtilityWindowTheme.MutedMiniLabelStyle);
                }

                if (palette != null && palette.Count > 0)
                {
                    EditorGUILayout.LabelField(sourceLabel + "  �  " + palette.Count + " swatches", UtilityWindowTheme.SectionHeaderStyle);
                    DrawPaletteSwatchStrip(palette, 28f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (ThemedButton(new GUIContent("Map Dark", "Use palette colours to build a readable dark utility theme."), UtilityWindowTheme.Cyan, GUILayout.Height(24f)))
                            ApplyPaletteRoleColours(palette, PaletteThemeBuildMode.BuildDark);
                        if (ThemedButton(new GUIContent("Map Vivid", "Push selected palette accents toward vivid dark-theme controls."), UtilityWindowTheme.Purple, GUILayout.Height(24f)))
                            ApplyPaletteRoleColours(palette, PaletteThemeBuildMode.BuildVibrant);
                        if (ThemedButton(new GUIContent("High Contrast", "Map the palette with stronger borders and contrast targets."), UtilityWindowTheme.Green, GUILayout.Height(24f)))
                            ApplyPaletteRoleColours(palette, PaletteThemeBuildMode.BuildHighContrast);
                        if (ThemedButton(new GUIContent("Preserve Roles", "Use explicit swatch roles where available and rebalance text afterwards."), UtilityWindowTheme.Amber, GUILayout.Height(24f)))
                            ApplyPaletteRoleColours(palette, PaletteThemeBuildMode.PreserveRoles);
                    }

                    DrawPaletteRolePreview(palette);
                }
            }
        }

        private void DrawPaletteSwatchStrip(List<PaletteRoleColor> palette, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            if (palette == null || palette.Count == 0)
                return;

            int count = Mathf.Min(16, palette.Count);
            float width = rect.width / count;
            for (int i = 0; i < count; i++)
            {
                Rect swatch = new Rect(rect.x + i * width, rect.y, Mathf.Ceil(width), rect.height);
                EditorGUI.DrawRect(swatch, UtilityWindowTheme.SimulateDeficiency(UtilityWindowTheme.ActiveDeficiencyPreview, palette[i].color));
                GUI.Label(swatch, new GUIContent(string.Empty, string.IsNullOrEmpty(palette[i].role) ? "Unassigned" : palette[i].role));
            }
        }

        private void DrawPaletteRolePreview(List<PaletteRoleColor> palette)
        {
            string[] roles = { "Background", "Panel", "Accent", "AccentSecondary", "Highlight", "Success", "Warning", "Error", "Outline" };
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f, 5, 3)))
            {
                EditorGUILayout.LabelField("Role Preview", UtilityWindowTheme.SectionHeaderStyle);
                for (int i = 0; i < roles.Length; i += 3)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int j = i; j < Mathf.Min(i + 3, roles.Length); j++)
                        {
                            Color colour = FindRole(palette, roles[j], j < palette.Count ? palette[j].color : UtilityWindowTheme.Neutral);
                            Rect swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
                            EditorGUI.DrawRect(swatch, colour);
                            EditorGUILayout.LabelField(roles[j], UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(88f));
                        }
                    }
                }
            }
        }

        private void DrawManualRoleWorkbench()
        {
            _showManual = EditorGUILayout.Foldout(_showManual, "Manual Role Tuning", true);
            UtilityWindowPrefs.SetBool(PrefShowManual, _showManual);
            if (!_showManual)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.16f, 0.08f, 7, 5)))
            {
                for (int i = 0; i < UtilityWindowTheme.EditableColorRoles.Length; i += 2)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawRoleColorField(UtilityWindowTheme.EditableColorRoles[i]);
                        if (i + 1 < UtilityWindowTheme.EditableColorRoles.Length)
                            DrawRoleColorField(UtilityWindowTheme.EditableColorRoles[i + 1]);
                    }
                }
            }
        }

        private void DrawSettingsRail()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, padding: 6), GUILayout.Width(_rightWidth), GUILayout.ExpandHeight(true)))
            {
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                DrawScopeSection();
                DrawTypographySection();
                DrawSurfaceSection();
                DrawExperimentalEditorSkinSection();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawScopeSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, padding: 6, margin: 3)))
            {
                UtilityWindowTheme.SectionTitle("Scope", UtilityWindowTheme.Cyan, "target");
                EditorGUI.BeginChangeCheck();
                UtilityWindowTheme.ThemeScope scope = (UtilityWindowTheme.ThemeScope)EditorGUILayout.EnumPopup(new GUIContent("Theme Scope", "Experimental Unity Editor Skin Bridge emits USS extension files and optional session-only GUIStyle text/font overrides."), UtilityWindowTheme.ActiveScope);
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowTheme.ActiveScope = scope;
                    if (scope != UtilityWindowTheme.ThemeScope.ExperimentalUnityEditorUssBridge && PungentEditorSkinBridge.ExperimentalBridgeEnabled)
                        PungentEditorSkinBridge.DisableAndRestore();
                }

                EditorGUILayout.LabelField(GetScopeDescription(UtilityWindowTheme.ActiveScope), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawTypographySection()
        {
            _showTypography = EditorGUILayout.Foldout(_showTypography, "Typography", true);
            UtilityWindowPrefs.SetBool(PrefShowTypography, _showTypography);
            if (!_showTypography)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, padding: 6, margin: 3)))
            {
                EditorGUI.BeginChangeCheck();
                float textScale = EditorGUILayout.Slider(new GUIContent("Text Scale", "Scales shared utility styles and generated editor USS font-size rules."), UtilityWindowTheme.TextScale, 0.80f, 1.35f);
                if (EditorGUI.EndChangeCheck())
                    UtilityWindowTheme.TextScale = textScale;

                DrawPresetFontHintSummary();

                foreach (UtilityWindowTheme.TextRole role in Enum.GetValues(typeof(UtilityWindowTheme.TextRole)))
                {
                    EditorGUI.BeginChangeCheck();
                    Font next = (Font)EditorGUILayout.ObjectField(new GUIContent(ObjectNames.NicifyVariableName(role.ToString()), "Optional Font asset for this semantic text role."), UtilityWindowTheme.GetFont(role), typeof(Font), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        UtilityWindowTheme.SetFont(role, next);
                        if (PungentEditorSkinBridge.ExperimentalBridgeEnabled)
                        {
                            if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Regenerate experimental editor USS bridge files for the active theme change."))
                                PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
                        }
                    }
                }
            }
        }

        private void DrawSurfaceSection()
        {
            _showPanels = EditorGUILayout.Foldout(_showPanels, "Surfaces", true);
            UtilityWindowPrefs.SetBool(PrefShowPanels, _showPanels);
            if (!_showPanels)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, padding: 6, margin: 3)))
            {
                EditorGUI.BeginChangeCheck();
                float density = EditorGUILayout.Slider(new GUIContent("Density", "Shared padding/spacing scale."), UtilityWindowTheme.Density, 0.75f, 1.35f);
                int borderWidth = DrawChoice("Border", UtilityWindowTheme.PanelBorderWidth, new[] { 0, 1, 2 }, new[] { "Off", "Crisp", "Heavy" });
                float borderOpacity = EditorGUILayout.Slider(new GUIContent("Border Opacity", "Opacity of generated panel/editor USS borders."), UtilityWindowTheme.PanelBorderOpacity, 0f, 1f);
                int radius = DrawChoice("Radius", UtilityWindowTheme.PanelCornerRadius, new[] { 0, 2, 4, 6, 8, 10 }, new[] { "Square", "Subtle", "Soft", "Rounded", "Bubble", "Max" });
                float darkAlpha = EditorGUILayout.Slider(new GUIContent("Dark Alpha", "Panel tint alpha used in Unity dark skin."), UtilityWindowTheme.PanelAlphaDark, 0f, 0.60f);
                float lightAlpha = EditorGUILayout.Slider(new GUIContent("Light Alpha", "Panel tint alpha used in Unity light skin."), UtilityWindowTheme.PanelAlphaLight, 0f, 0.35f);
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowTheme.Density = density;
                    UtilityWindowTheme.PanelBorderWidth = borderWidth;
                    UtilityWindowTheme.PanelBorderOpacity = borderOpacity;
                    UtilityWindowTheme.PanelCornerRadius = radius;
                    UtilityWindowTheme.PanelAlphaDark = darkAlpha;
                    UtilityWindowTheme.PanelAlphaLight = lightAlpha;
                    UtilityWindowTheme.RepaintUtilityWindows();
                    if (PungentEditorSkinBridge.ExperimentalBridgeEnabled)
                    {
                        if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Regenerate experimental editor USS bridge files for the active theme change."))
                            PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
                    }
                }
            }
        }

        private void DrawExperimentalEditorSkinSection()
        {
            _showExperimental = EditorGUILayout.Foldout(_showExperimental, "Experimental Editor Skin", true);
            UtilityWindowPrefs.SetBool(PrefShowExperimental, _showExperimental);
            if (!_showExperimental)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Red, padding: 6, margin: 3)))
            {
                EditorGUILayout.LabelField("USS bridge covers compatible UI Toolkit/editor selectors. Session GUIStyle overrides improve IMGUI text/font coverage and reset on demand.", UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField("Some Unity internals, especially certain script/asset preview hosts, may not expose stable USS hooks in every version. Those limits are documented in the generated bridge comments instead of being silently over-promised.", UtilityWindowTheme.MutedMiniLabelStyle);
                if (PungentEditorSkinBridge.ExperimentalBridgeEnabled || PungentEditorGuiStyleBridge.SessionOverridesEnabled || PungentEditorSkinBridge.ApplyOnLoad)
                {
                    EditorGUILayout.HelpBox("Experimental editor styling is active. If Unity editor styles look broken, use Disable + Restore, then repaint, refresh the domain, or restart the Unity Editor.", MessageType.Warning);
                }

                bool currentEnabled = PungentEditorSkinBridge.ExperimentalBridgeEnabled;
                bool currentApplyOnLoad = PungentEditorSkinBridge.ApplyOnLoad;
                bool currentSession = PungentEditorGuiStyleBridge.SessionOverridesEnabled;
                bool enabled = currentEnabled;
                bool applyOnLoad = currentApplyOnLoad;
                bool session = currentSession;

                EditorGUI.BeginChangeCheck();
                enabled = EditorGUILayout.Toggle(new GUIContent("Enable USS Bridge", "Generate Editor/StyleSheets/Extensions USS files."), enabled);
                applyOnLoad = EditorGUILayout.Toggle(new GUIContent("Apply On Launch", "Regenerate USS after domain reload/editor launch."), applyOnLoad);
                session = EditorGUILayout.Toggle(new GUIContent("Session GUIStyle Text/Fonts", "Reversible in-memory GUIStyle text/font overrides for IMGUI editor controls. Background styling is intentionally excluded here because hover backgrounds can make the editor look like UI Toolkit Pick Element mode."), session);
                if (EditorGUI.EndChangeCheck())
                {
                    bool enablingRiskyStyling = (enabled && !currentEnabled) || (session && !currentSession) || (applyOnLoad && !currentApplyOnLoad && enabled);
                    if (enablingRiskyStyling && !PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Turn on experimental Unity editor styling from the Theme Editor."))
                        return;

                    PungentEditorSkinBridge.ApplyOnLoad = applyOnLoad;
                    if (enabled)
                    {
                        UtilityWindowTheme.ActiveScope = UtilityWindowTheme.ThemeScope.ExperimentalUnityEditorUssBridge;
                        PungentEditorSkinBridge.EnableAndGenerate(applyOnLoad);
                    }
                    else
                    {
                        PungentEditorSkinBridge.DisableAndRestore();
                    }

                    if (session)
                        PungentEditorGuiStyleBridge.ApplySessionThemeToAllStyles(false);
                    else
                        PungentEditorGuiStyleBridge.DisableAndRestore();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ThemedButton(new GUIContent("Generate USS", "Writes generated common.uss/dark.uss/light.uss extension files for compatible Unity Editor UI Toolkit selectors."), UtilityWindowTheme.Cyan, GUILayout.Height(24f)))
                    {
                        if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Generate experimental Unity editor USS bridge files."))
                        {
                            PungentEditorSkinBridge.ExperimentalBridgeEnabled = true;
                            PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
                        }
                    }
                    if (ThemedButton(new GUIContent("Apply All Styles", "Applies reversible session-only text/font/background styling to every known compatible GUIStyle. Backgrounds are limited to safe hierarchy/tree/field/code-style controls. Use Disable + Restore to revert."), UtilityWindowTheme.Purple, GUILayout.Height(24f)))
                    {
                        if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Apply session GUIStyle styling to every known compatible editor style."))
                            PungentEditorGuiStyleBridge.ApplySessionThemeToAllStyles(false);
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ThemedButton(new GUIContent("Disable + Restore", "Clears generated USS files and restores reversible GUIStyle session overrides."), UtilityWindowTheme.Red, GUILayout.Height(24f)))
                        PungentEditorSkinBridge.DisableAndRestore();
                    if (GUILayout.Button(new GUIContent("Open Style Explorer", "Open the experimental Theme Editor accessory for editor fonts, icons, and GUIStyle mappings."), GUILayout.Height(24f)))
                        PungentEditorStyleExplorerWindow.ShowWindow();
                }

                PungentEditorSkinDiagnostics diagnostics = PungentEditorSkinDiagnostics.Capture();
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Diagnostics", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.LabelField("Base Unity Theme", diagnostics.baseTheme, UtilityWindowTheme.BodyStyle);
                EditorGUILayout.LabelField("USS Selectors", diagnostics.selectorCount.ToString(), UtilityWindowTheme.BodyStyle);
                EditorGUILayout.LabelField("GUIStyle", PungentEditorGuiStyleBridge.LastAction, UtilityWindowTheme.MutedMiniLabelStyle);
                EditorGUILayout.LabelField(diagnostics.extensionFolder, UtilityWindowTheme.PathLabelStyle);
                EditorGUILayout.LabelField(diagnostics.lastAction, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }


        private bool ThemedButton(GUIContent content, Color tint, params GUILayoutOption[] options)
        {
            using (UtilityWindowTheme.Background(tint))
                return GUILayout.Button(content, options);
        }

        private void DrawPresetFontHintSummary()
        {
            UtilityThemePresetDefinition preset = UtilityThemePresetLibrary.Get(UtilityWindowTheme.ActivePreset);
            if (preset == null)
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(new GUIContent("Preset Font Hints", "Presets include readable font-role hints. They are applied only when a matching Font asset has a nearby commercial/open license file such as OFL or Apache."), UtilityWindowTheme.SectionHeaderStyle);
            EditorGUILayout.LabelField("Decorative fonts are only suggested for Heading/Subheading. Body, Muted, Link, Field, Path, and Code roles prefer clean sans/mono fonts.", UtilityWindowTheme.MutedMiniLabelStyle);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f, 4, 2)))
            {
                foreach (UtilityWindowTheme.TextRole role in Enum.GetValues(typeof(UtilityWindowTheme.TextRole)))
                {
                    string[] hints = preset.GetFontHints(role);
                    string active = UtilityWindowTheme.GetFont(role) != null ? UtilityWindowTheme.GetFont(role).name : "Default / unresolved";
                    EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(role.ToString()), active + (hints.Length > 0 ? "  �  " + string.Join(", ", hints.Take(Mathf.Min(3, hints.Length)).ToArray()) : string.Empty), UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawRoleColorField(string role)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(170f)))
            {
                EditorGUI.BeginChangeCheck();
                Color next = EditorGUILayout.ColorField(new GUIContent(UtilityWindowTheme.GetRoleDisplayName(role), role), UtilityWindowTheme.GetColor(role));
                if (EditorGUI.EndChangeCheck())
                {
                    UtilityWindowTheme.SetColor(role, next);
                    UtilityWindowTheme.RepaintUtilityWindows();
                    if (PungentEditorSkinBridge.ExperimentalBridgeEnabled)
                    {
                        if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Regenerate experimental editor USS bridge files for the active theme change."))
                            PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
                    }
                }
            }
        }

        private int DrawChoice(string label, int current, int[] values, string[] labels)
        {
            int index = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == current)
                {
                    index = i;
                    break;
                }
            }

            index = EditorGUILayout.Popup(label, index, labels);
            return values[Mathf.Clamp(index, 0, values.Length - 1)];
        }

        private void DrawPresetSwatches(Rect rect, UtilityThemePresetDefinition preset, bool compact)
        {
            string[] roles = compact
                ? new[] { UtilityWindowTheme.RoleHeader, UtilityWindowTheme.RolePrimary, UtilityWindowTheme.RoleSecondary, UtilityWindowTheme.RoleTertiary }
                : new[] { UtilityWindowTheme.RoleHeader, UtilityWindowTheme.RolePrimary, UtilityWindowTheme.RoleSecondary, UtilityWindowTheme.RoleTertiary, UtilityWindowTheme.RoleSuccess, UtilityWindowTheme.RoleWarning, UtilityWindowTheme.RoleDanger, UtilityWindowTheme.RoleAccentAlt };

            float width = rect.width / roles.Length;
            for (int i = 0; i < roles.Length; i++)
            {
                Rect swatch = new Rect(rect.x + i * width, rect.y, Mathf.Ceil(width), rect.height);
                Color color = UtilityWindowTheme.SimulateDeficiency(UtilityWindowTheme.ActiveDeficiencyPreview, preset.GetColor(roles[i], Color.magenta));
                EditorGUI.DrawRect(swatch, color);
            }
        }

        private void DrawActiveRoleStrip(Rect rect)
        {
            string[] roles =
            {
                UtilityWindowTheme.RoleHeader,
                UtilityWindowTheme.RolePrimary,
                UtilityWindowTheme.RoleSecondary,
                UtilityWindowTheme.RoleTertiary,
                UtilityWindowTheme.RoleSuccess,
                UtilityWindowTheme.RoleWarning,
                UtilityWindowTheme.RoleDanger,
                UtilityWindowTheme.RoleAccentAlt,
                UtilityWindowTheme.RoleNeutral
            };

            float width = rect.width / roles.Length;
            for (int i = 0; i < roles.Length; i++)
            {
                Rect swatch = new Rect(rect.x + i * width, rect.y, Mathf.Ceil(width), rect.height);
                EditorGUI.DrawRect(swatch, UtilityWindowTheme.SimulateDeficiency(UtilityWindowTheme.ActiveDeficiencyPreview, UtilityWindowTheme.GetColor(roles[i])));
                GUI.Label(swatch, new GUIContent(string.Empty, UtilityWindowTheme.GetRoleDisplayName(roles[i])));
            }
        }

        private void DrawTextPairBadge(string label, Color fg, Color bg)
        {
            float ratio = UtilityWindowTheme.GetContrastRatio(fg, bg);
            Color tint = ratio >= 4.5f ? UtilityWindowTheme.Green : ratio >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(88f)))
            {
                Rect rect = GUILayoutUtility.GetRect(76f, 28f);
                EditorGUI.DrawRect(rect, bg);
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = fg } };
                GUI.Label(rect, label, style);
                UtilityWindowTheme.CountPill(ratio.ToString("0.0") + " " + UtilityWindowTheme.GetContrastRating(ratio), tint, 86f);
            }
        }

        private void DrawContrastPair(string label, Color a, Color b, bool compareColours)
        {
            float value = compareColours ? ColourDistance(a, b) : UtilityWindowTheme.GetContrastRatio(a, b);
            Color tint = compareColours
                ? value > 0.70f ? UtilityWindowTheme.Green : value > 0.38f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red
                : value >= 4.5f ? UtilityWindowTheme.Green : value >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, UtilityWindowTheme.BodyStyle);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill(compareColours ? (value > 0.70f ? "distinct" : value > 0.38f ? "close" : "similar") : value.ToString("0.0"), tint, 78f);
            }
        }

        private float GetPresetBodyContrast(UtilityThemePresetDefinition preset)
        {
            return UtilityWindowTheme.GetContrastRatio(
                preset.GetColor(UtilityWindowTheme.RoleCardText, Color.white),
                preset.GetColor(UtilityWindowTheme.RoleHeader, Color.black));
        }

        private static float ColourDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }

        private void ApplyComposerTuning()
        {
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RolePrimary, TuneColour(UtilityWindowTheme.Blue, _composerSaturation, _composerAccentIntensity, true));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleSecondary, TuneColour(UtilityWindowTheme.Cyan, _composerSaturation, Mathf.Clamp01(_composerAccentIntensity * 0.92f), true));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleTertiary, TuneColour(UtilityWindowTheme.Teal, Mathf.Clamp01(_composerSaturation * 0.92f), Mathf.Clamp01(_composerAccentIntensity * 0.84f), true));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleAccentAlt, TuneColour(UtilityWindowTheme.Purple, _composerSaturation, _composerAccentIntensity, true));

            int radius = _composerBorderStrength < 0.18f ? 0 : _composerBorderStrength < 0.38f ? 2 : _composerBorderStrength < 0.60f ? 4 : _composerBorderStrength < 0.82f ? 6 : 8;
            UtilityWindowTheme.PanelBorderWidth = _composerBorderStrength > 0.74f ? 2 : _composerBorderStrength > 0.16f ? 1 : 0;
            UtilityWindowTheme.PanelBorderOpacity = Mathf.Lerp(0.12f, 0.88f, _composerBorderStrength);
            UtilityWindowTheme.PanelCornerRadius = radius;
            UtilityWindowTheme.PanelAlphaDark = Mathf.Lerp(0.18f, 0.42f, _composerSurfaceDepth);
            UtilityWindowTheme.PanelAlphaLight = Mathf.Lerp(0.05f, 0.18f, _composerSurfaceDepth);

            Color header = ResolveHeaderFromBase(UtilityWindowTheme.Blue, !_composerPreferLight);
            header = Color.Lerp(header, UtilityWindowTheme.HeaderTint, 0.28f);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleHeader, header);
            RepairTextRolesForHeader(header);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleMutedText, UtilityWindowTheme.ImproveContrast(Color.Lerp(UtilityWindowTheme.CardText, header, Mathf.Lerp(0.50f, 0.24f, _composerMutedStrength)), header, 3.25f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RolePathText, UtilityWindowTheme.ImproveContrast(Color.Lerp(UtilityWindowTheme.CardText, UtilityWindowTheme.Cyan, 0.16f + (_composerAccentIntensity * 0.18f)), header, 4.25f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleResizeHandle, UtilityWindowTheme.Cyan);
            FinalizeThemeChange();
        }

        private static Color TuneColour(Color input, float saturationControl, float intensityControl, bool brighten)
        {
            Color.RGBToHSV(input, out float hue, out float saturation, out float value);
            saturation = Mathf.Lerp(0.20f, 0.96f, Mathf.Lerp(saturationControl, saturationControl * 0.82f, 0.25f));
            value = Mathf.Clamp01(Mathf.Lerp(value, brighten ? Mathf.Lerp(0.56f, 0.96f, intensityControl) : Mathf.Lerp(0.30f, 0.82f, intensityControl), 0.55f));
            return Color.HSVToRGB(hue, saturation, value);
        }

        private static Color AdjustSaturationValue(Color input, float targetSaturation, float targetValue)
        {
            Color.RGBToHSV(input, out float hue, out _, out _);
            return Color.HSVToRGB(hue, Mathf.Clamp01(targetSaturation), Mathf.Clamp01(targetValue));
        }

        private static Color ForceSurfaceTone(Color input, bool light, float valueTarget)
        {
            Color.RGBToHSV(input, out float hue, out float saturation, out _);
            float targetSaturation = light ? Mathf.Clamp(saturation * 0.28f, 0.04f, 0.22f) : Mathf.Clamp(saturation * 0.58f, 0.12f, 0.42f);
            return Color.HSVToRGB(hue, targetSaturation, Mathf.Clamp01(valueTarget));
        }

        private void GenerateTheme(bool iterate)
        {
            if (iterate)
            {
                _generatorSeed = unchecked(_generatorSeed * 1103515245 + 12345);
                UtilityWindowPrefs.SetInt(PrefGeneratorSeed, _generatorSeed);
            }

            if (_usePaletteDesignerIfAvailable && TryGenerateWithPaletteDesigner(out List<PaletteRoleColor> generated))
            {
                ApplyPaletteRoleColours(generated);
                return;
            }

            ApplyFallbackHarmonyGeneration(_generatorBase, _generatorHarmony, _generatorInfluence, _generatorSeed);
        }

        private void IterateGeneratedTheme()
        {
            GenerateTheme(true);
            ApplyRecipeTypography(_activeRecipeMood);
            FlashField("recipe.core");
            FlashField("recipe.surface");
            FlashField("recipe.preview");
        }

        private void RandomiseBalancedTheme()
        {
            _generatorSeed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
            System.Random rng = new System.Random(_generatorSeed);
            _generatorBase = Color.HSVToRGB((float)rng.NextDouble(), Mathf.Lerp(0.38f, 0.72f, (float)rng.NextDouble()), Mathf.Lerp(0.55f, 0.88f, (float)rng.NextDouble()));
            _generatorHarmony = ThemeHarmonyMode.RandomBalanced;
            _composerPreferLight = rng.NextDouble() > 0.55d;
            SaveGeneratorPrefs();
            GenerateTheme(false);
        }

        private void ApplyFallbackHarmonyGeneration(Color baseColor, ThemeHarmonyMode harmony, float influence, int seed)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            System.Random rng = new System.Random(seed);
            float jitter = ((float)rng.NextDouble() - 0.5f) * 0.035f;
            float[] offsets = GetHarmonyOffsets(harmony, rng);

            Color primary = GenerateHarmonyColor(h + offsets[0] * influence + jitter, s, v, 0.00f, 0.04f);
            Color secondary = GenerateHarmonyColor(h + offsets[1] * influence - jitter, s, v, -0.06f, 0.10f);
            Color tertiary = GenerateHarmonyColor(h + offsets[2] * influence + jitter * 0.5f, s, v, -0.12f, 0.02f);
            Color accent = GenerateHarmonyColor(h + offsets[3] * Mathf.Max(0.35f, influence), s, v, -0.08f, 0.08f);
            Color header = ResolveHeaderFromBase(primary, !_composerPreferLight);

            UtilityWindowTheme.SetColor(UtilityWindowTheme.RolePrimary, primary);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleSecondary, secondary);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleTertiary, tertiary);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleSuccess, GenerateHarmonyColor(0.36f, 0.62f, 0.78f, 0f, 0f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleWarning, GenerateHarmonyColor(0.115f, 0.70f, 0.92f, 0f, 0f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleDanger, GenerateHarmonyColor(0.00f, 0.70f, 0.90f, 0f, 0f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleAccentAlt, accent);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleNeutral, EditorGUIUtility.isProSkin ? new Color(0.52f, 0.56f, 0.62f) : new Color(0.43f, 0.47f, 0.54f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleHeader, header);
            RepairTextRolesForHeader(header);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleResizeHandle, secondary);
            UtilityWindowTheme.PanelAlphaDark = Mathf.Lerp(0.18f, 0.42f, _composerSurfaceDepth);
            UtilityWindowTheme.PanelAlphaLight = Mathf.Lerp(0.05f, 0.18f, _composerSurfaceDepth);
            UtilityWindowTheme.PanelBorderOpacity = Mathf.Lerp(0.16f, 0.88f, _composerBorderStrength);
            FinalizeThemeChange();
        }

        private float[] GetHarmonyOffsets(ThemeHarmonyMode mode, System.Random rng)
        {
            switch (mode)
            {
                case ThemeHarmonyMode.Monochromatic: return new[] { 0f, 0.015f, -0.015f, 0.03f };
                case ThemeHarmonyMode.Analogous: return new[] { 0f, 0.075f, -0.075f, 0.14f };
                case ThemeHarmonyMode.Complementary: return new[] { 0f, 0.5f, 0.08f, -0.08f };
                case ThemeHarmonyMode.SplitComplementary: return new[] { 0f, 0.42f, -0.42f, 0.08f };
                case ThemeHarmonyMode.Triadic: return new[] { 0f, 0.333f, -0.333f, 0.08f };
                case ThemeHarmonyMode.Tetradic: return new[] { 0f, 0.25f, 0.50f, 0.75f };
                case ThemeHarmonyMode.Square: return new[] { 0f, 0.25f, 0.50f, -0.25f };
                case ThemeHarmonyMode.RandomBalanced:
                    return new[] { 0f, (float)rng.NextDouble() * 0.18f + 0.05f, -((float)rng.NextDouble() * 0.18f + 0.05f), (float)rng.NextDouble() * 0.5f + 0.25f };
                default: return new[] { 0f, 0.08f, -0.10f, 0.50f };
            }
        }

        private Color GenerateHarmonyColor(float hue, float saturation, float value, float saturationDelta, float valueDelta)
        {
            float s = Mathf.Clamp01(Mathf.Clamp(saturation, 0.35f, 0.82f) + saturationDelta);
            float v = Mathf.Clamp01(Mathf.Clamp(value, 0.48f, 0.90f) + valueDelta);
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), s, v);
        }

        private Color ResolveHeaderFromBase(Color baseColor, bool dark)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            return Color.HSVToRGB(h, dark ? Mathf.Clamp(s * 0.58f, 0.18f, 0.48f) : Mathf.Clamp(s * 0.18f, 0.06f, 0.24f), dark ? 0.16f : 0.92f);
        }

        private void RepairTextRolesForCurrentHeader()
        {
            RepairTextRolesForHeader(UtilityWindowTheme.HeaderTint);
            FinalizeThemeChange();
        }

        private void RepairCurrentThemeReadability()
        {
            RepairTextRolesForHeader(UtilityWindowTheme.HeaderTint);
            if (ColourDistance(UtilityWindowTheme.Green, UtilityWindowTheme.Amber) < 0.38f)
                UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleSuccess, new Color(0.42f, 0.78f, 0.42f));
            if (ColourDistance(UtilityWindowTheme.Amber, UtilityWindowTheme.Red) < 0.38f)
                UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleDanger, new Color(0.86f, 0.30f, 0.30f));
            FinalizeThemeChange();
        }

        private void RepairTextRolesForHeader(Color header)
        {
            Color light = UtilityWindowTheme.LightText;
            Color dark = UtilityWindowTheme.DarkText;
            Color title = UtilityWindowTheme.GetContrastRatio(light, header) >= UtilityWindowTheme.GetContrastRatio(dark, header) ? light : dark;
            Color body = title;
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleTitleText, UtilityWindowTheme.ImproveContrast(title, header, Mathf.Max(7f, _generatorTargetContrast)));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleCardText, UtilityWindowTheme.ImproveContrast(body, header, _generatorTargetContrast));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleSubtitleText, UtilityWindowTheme.ImproveContrast(Color.Lerp(body, header, 0.18f), header, 4.5f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleMutedText, UtilityWindowTheme.ImproveContrast(Color.Lerp(body, header, 0.38f), header, 3.25f));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RolePathText, UtilityWindowTheme.ImproveContrast(Color.Lerp(body, UtilityWindowTheme.Cyan, 0.18f), header, 4.25f));
        }

        private void FinalizeThemeChange()
        {
            UtilityWindowTheme.RepaintUtilityWindows();
            if (PungentEditorSkinBridge.ExperimentalBridgeEnabled)
            {
                if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Regenerate experimental editor USS bridge files for the active theme change."))
                    PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
            }
            if (PungentEditorGuiStyleBridge.SessionOverridesEnabled)
            {
                if (PungentEditorStyleExplorerWindow.ConfirmExperimentalStyleChange("Reapply session GUIStyle overrides for the active theme change."))
                    PungentEditorGuiStyleBridge.ApplySessionThemeToAllStyles(false);
            }
        }

        private bool TryGenerateWithPaletteDesigner(out List<PaletteRoleColor> colours)
        {
            colours = null;
            Type settingsType = FindType("PungentFunk.Utilities.Colour.PaletteGenerationSettings");
            Type generatorType = FindType("PungentFunk.Utilities.Colour.PaletteGeneratorUtility");
            if (settingsType == null || generatorType == null)
                return false;

            try
            {
                object settings = Activator.CreateInstance(settingsType);
                SetFieldOrProperty(settings, "targetSwatchCount", 12);
                SetFieldOrProperty(settings, "seed", _generatorSeed);
                SetFieldOrProperty(settings, "useSeed", true);
                SetFieldOrProperty(settings, "harmonyInfluence", _generatorInfluence);
                SetFieldOrProperty(settings, "contrastInfluence", 0.72f);
                SetFieldOrProperty(settings, "randomVariation", 0.12f);
                SetFieldOrProperty(settings, "enforceTextContrast", true);
                SetFieldOrProperty(settings, "avoidNearDuplicates", true);

                object harmony = ParseHarmonyEnum(settingsType, _generatorHarmony.ToString());
                if (harmony != null)
                    SetFieldOrProperty(settings, "harmonyMode", harmony);

                MethodInfo generate = generatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "Generate" && m.GetParameters().Length >= 1);
                if (generate == null)
                    return false;

                object result = generate.GetParameters().Length == 1 ? generate.Invoke(null, new[] { settings }) : generate.Invoke(null, new object[] { settings, null });
                colours = ExtractPaletteRoleColours(result);
                return colours != null && colours.Count > 0;
            }
            catch
            {
                colours = null;
                return false;
            }
        }

        private void ApplySelectedPaletteOrNotify()
        {
            if (TryGetCurrentPaletteSource(out List<PaletteRoleColor> palette, out _))
            {
                ApplyPaletteRoleColours(palette, _paletteBuildMode);
                return;
            }

            ShowNotification(new GUIContent("Choose a palette in Palette Source or select a PungentColourPaletteSO-style asset."));
        }

        private bool TryGetCurrentPaletteSource(out List<PaletteRoleColor> palette, out string label)
        {
            palette = null;
            label = string.Empty;

            if (_paletteSourceMode == PaletteSourceMode.DiscoveredPalette && _availablePalettes.Count > 0)
            {
                int index = Mathf.Clamp(_selectedPaletteIndex, 0, _availablePalettes.Count - 1);
                palette = _availablePalettes[index].colours;
                label = _availablePalettes[index].displayName;
                return palette != null && palette.Count > 0;
            }

            if (_paletteSourceMode == PaletteSourceMode.SelectedProjectAsset && TryReadSelectedPalette(out palette) && palette.Count > 0)
            {
                label = Selection.activeObject != null ? Selection.activeObject.name : "Selected Palette";
                return true;
            }

            return false;
        }

        private void RefreshAvailablePalettes(bool notify)
        {
            _availablePalettes.Clear();

            var seen = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets("t:PungentColourPaletteSO");
            if (guids == null || guids.Length == 0)
                guids = AssetDatabase.FindAssets("Palette t:ScriptableObject");

            if (guids != null)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path) || !seen.Add(path))
                        continue;

                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset == null || !TryReadPaletteAsset(asset, out List<PaletteRoleColor> colours) || colours.Count == 0)
                        continue;

                    _availablePalettes.Add(new PaletteSourceInfo
                    {
                        asset = asset,
                        displayName = GetPaletteDisplayName(asset, path, colours.Count),
                        assetPath = path,
                        colours = colours
                    });
                }
            }

            _availablePalettes.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            _selectedPaletteIndex = 0;
            if (!string.IsNullOrEmpty(_selectedPalettePath))
            {
                for (int i = 0; i < _availablePalettes.Count; i++)
                {
                    if (string.Equals(_availablePalettes[i].assetPath, _selectedPalettePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedPaletteIndex = i;
                        break;
                    }
                }
            }

            if (_availablePalettes.Count > 0)
            {
                _selectedPaletteIndex = Mathf.Clamp(_selectedPaletteIndex, 0, _availablePalettes.Count - 1);
                _selectedPalettePath = _availablePalettes[_selectedPaletteIndex].assetPath;
                UtilityWindowPrefs.SetString(PrefSelectedPalettePath, _selectedPalettePath ?? string.Empty);
            }
            else
            {
                _selectedPaletteIndex = -1;
            }

            if (notify)
                ShowNotification(new GUIContent(_availablePalettes.Count > 0 ? "Found " + _availablePalettes.Count + " palettes." : "No palette assets found."));
        }

        private bool TryReadPaletteAsset(UnityEngine.Object asset, out List<PaletteRoleColor> palette)
        {
            palette = null;
            if (asset == null)
                return false;

            FieldInfo swatchesField = asset.GetType().GetField("swatches", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo swatchesProperty = asset.GetType().GetProperty("swatches", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object swatches = swatchesField != null ? swatchesField.GetValue(asset) : swatchesProperty != null ? swatchesProperty.GetValue(asset, null) : null;
            palette = ExtractPaletteRoleColours(swatches);
            return palette != null && palette.Count > 0;
        }

        private string GetPaletteDisplayName(UnityEngine.Object asset, string path, int count)
        {
            string display = asset != null ? asset.name : System.IO.Path.GetFileNameWithoutExtension(path);
            FieldInfo paletteNameField = asset != null ? asset.GetType().GetField("paletteName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
            PropertyInfo paletteNameProperty = asset != null ? asset.GetType().GetProperty("paletteName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
            object value = paletteNameField != null ? paletteNameField.GetValue(asset) : paletteNameProperty != null ? paletteNameProperty.GetValue(asset, null) : null;
            string paletteName = value != null ? Convert.ToString(value) : string.Empty;
            if (!string.IsNullOrWhiteSpace(paletteName))
                display = paletteName;
            return display + " (" + count + ")";
        }

        private bool TryReadSelectedPalette(out List<PaletteRoleColor> palette)
        {
            UnityEngine.Object selected = Selection.activeObject;
            return TryReadPaletteAsset(selected, out palette);
        }

        private List<PaletteRoleColor> ExtractPaletteRoleColours(object swatches)
        {
            var result = new List<PaletteRoleColor>();
            if (swatches is IEnumerable enumerable)
            {
                foreach (object swatch in enumerable)
                {
                    if (swatch == null)
                        continue;
                    Type swatchType = swatch.GetType();
                    FieldInfo colorField = swatchType.GetField("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    PropertyInfo colorProperty = swatchType.GetProperty("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    FieldInfo roleField = swatchType.GetField("role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    PropertyInfo roleProperty = swatchType.GetProperty("role", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    object colorValue = colorField != null ? colorField.GetValue(swatch) : colorProperty != null ? colorProperty.GetValue(swatch, null) : null;
                    if (!(colorValue is Color))
                        continue;
                    object roleValue = roleField != null ? roleField.GetValue(swatch) : roleProperty != null ? roleProperty.GetValue(swatch, null) : null;
                    result.Add(new PaletteRoleColor
                    {
                        color = (Color)colorValue,
                        role = roleValue != null ? Convert.ToString(roleValue) : string.Empty
                    });
                }
            }
            return result;
        }

        private void ApplyPaletteRoleColours(List<PaletteRoleColor> colours, PaletteThemeBuildMode buildMode = PaletteThemeBuildMode.PreserveRoles)
        {
            if (colours == null || colours.Count == 0)
                return;

            Color fallback = colours[0].color;
            bool lightBuild = buildMode == PaletteThemeBuildMode.BuildLight || (buildMode == PaletteThemeBuildMode.PreserveRoles && _composerPreferLight);
            bool highContrastBuild = buildMode == PaletteThemeBuildMode.BuildHighContrast;
            bool pastelBuild = buildMode == PaletteThemeBuildMode.BuildSoftPastel;
            bool vibrantBuild = buildMode == PaletteThemeBuildMode.BuildVibrant;

            Color background = FindRole(colours, "Background", ResolveHeaderFromBase(fallback, !lightBuild));
            Color panel = FindRole(colours, "Panel", background);
            Color accent = FindRole(colours, "Accent", fallback);
            Color secondary = FindRole(colours, "AccentSecondary", colours.Count > 1 ? colours[1].color : accent);
            Color highlight = FindRole(colours, "Highlight", colours.Count > 2 ? colours[2].color : secondary);

            if (buildMode == PaletteThemeBuildMode.BuildDark)
            {
                panel = ForceSurfaceTone(panel, false, 0.16f);
                background = ForceSurfaceTone(background, false, 0.10f);
            }
            else if (lightBuild)
            {
                panel = ForceSurfaceTone(panel, true, 0.93f);
                background = ForceSurfaceTone(background, true, 0.97f);
            }

            if (pastelBuild)
            {
                accent = AdjustSaturationValue(accent, 0.48f, 0.92f);
                secondary = AdjustSaturationValue(secondary, 0.42f, 0.94f);
                highlight = AdjustSaturationValue(highlight, 0.38f, 0.96f);
                if (_composerPreferLight)
                {
                    panel = ForceSurfaceTone(panel, true, 0.94f);
                    background = ForceSurfaceTone(background, true, 0.98f);
                }
                else
                {
                    panel = ForceSurfaceTone(panel, false, 0.18f);
                    background = ForceSurfaceTone(background, false, 0.11f);
                }
            }
            else if (vibrantBuild)
            {
                accent = AdjustSaturationValue(accent, 0.88f, 0.94f);
                secondary = AdjustSaturationValue(secondary, 0.78f, 0.94f);
                highlight = AdjustSaturationValue(highlight, 0.72f, 0.92f);
            }

            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleHeader, panel);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RolePrimary, accent);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleSecondary, secondary);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleTertiary, highlight);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleSuccess, FindRole(colours, "Success", new Color(0.43f, 0.78f, 0.46f)));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleWarning, FindRole(colours, "Warning", new Color(0.86f, 0.66f, 0.25f)));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleDanger, FindRole(colours, "Error", new Color(0.84f, 0.32f, 0.30f)));
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleAccentAlt, highlight);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleNeutral, FindRole(colours, "Outline", new Color(0.50f, 0.55f, 0.62f)));
            UtilityWindowTheme.PanelBorderWidth = highContrastBuild ? 2 : UtilityWindowTheme.PanelBorderWidth;
            UtilityWindowTheme.PanelBorderOpacity = highContrastBuild ? 0.88f : Mathf.Lerp(0.22f, 0.74f, _composerBorderStrength);
            _generatorTargetContrast = highContrastBuild ? 7.5f : _generatorTargetContrast;
            RepairTextRolesForHeader(panel);
            UtilityWindowTheme.SetColor(UtilityWindowTheme.RoleResizeHandle, secondary);
            FinalizeThemeChange();
        }

        private Color FindRole(List<PaletteRoleColor> colours, string role, Color fallback)
        {
            for (int i = 0; i < colours.Count; i++)
            {
                if (string.Equals(colours[i].role, role, StringComparison.OrdinalIgnoreCase))
                    return colours[i].color;
            }
            return fallback;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static void SetFieldOrProperty(object target, string name, object value)
        {
            if (target == null)
                return;
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
                property.SetValue(target, value, null);
        }

        private static object ParseHarmonyEnum(Type settingsType, string value)
        {
            FieldInfo field = settingsType.GetField("harmonyMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Type enumType = field != null ? field.FieldType : null;
            if (enumType == null || !enumType.IsEnum)
                return null;
            try { return Enum.Parse(enumType, value, true); }
            catch { return Enum.Parse(enumType, "Contextual", true); }
        }

        private string GetPaletteDesignerStatus()
        {
            if (FindType("PungentFunk.Utilities.Colour.PaletteGeneratorUtility") != null)
                return "Palette Designer hook available.";
            return "Palette Designer not detected; using fallback harmony generator.";
        }

        private void OpenPaletteDesignerIfAvailable()
        {
            Type windowType = FindType("PungentFunk.Utilities.Editor.Colour.PaletteDesignerWindow") ?? FindType("PungentFunk.Utilities.Editor.PaletteDesignerWindow") ?? FindType("PungentFunk.Utilities.Colour.Editor.PaletteDesignerWindow");
            if (windowType == null)
            {
                ShowNotification(new GUIContent("Palette Designer window type not found."));
                return;
            }

            MethodInfo getWindow = typeof(EditorWindow).GetMethod("GetWindow", new[] { typeof(Type), typeof(bool), typeof(string) });
            getWindow?.Invoke(null, new object[] { windowType, false, "Palette Designer" });
        }

        private void SaveGeneratorPrefs()
        {
            UtilityWindowPrefs.SetColor(PrefGeneratorBase, _generatorBase);
            UtilityWindowPrefs.SetInt(PrefGeneratorHarmony, (int)_generatorHarmony);
            UtilityWindowPrefs.SetFloat(PrefGeneratorContrast, _generatorTargetContrast);
            UtilityWindowPrefs.SetInt(PrefGeneratorSeed, _generatorSeed);
            UtilityWindowPrefs.SetFloat(PrefGeneratorInfluence, _generatorInfluence);
            UtilityWindowPrefs.SetBool(PrefUsePaletteDesigner, _usePaletteDesignerIfAvailable);
            UtilityWindowPrefs.SetInt(PrefPaletteBuildMode, (int)_paletteBuildMode);
            UtilityWindowPrefs.SetFloat(PrefComposerSaturation, _composerSaturation);
            UtilityWindowPrefs.SetFloat(PrefComposerAccentIntensity, _composerAccentIntensity);
            UtilityWindowPrefs.SetFloat(PrefComposerSurfaceDepth, _composerSurfaceDepth);
            UtilityWindowPrefs.SetFloat(PrefComposerBorderStrength, _composerBorderStrength);
            UtilityWindowPrefs.SetFloat(PrefComposerMutedStrength, _composerMutedStrength);
            UtilityWindowPrefs.SetBool(PrefComposerPreferLight, _composerPreferLight);
            UtilityWindowPrefs.SetInt(PrefPaletteSourceMode, (int)_paletteSourceMode);
            UtilityWindowPrefs.SetString(PrefSelectedPalettePath, _selectedPalettePath ?? string.Empty);
        }

        private static string GetScopeDescription(UtilityWindowTheme.ThemeScope scope)
        {
            switch (scope)
            {
                case UtilityWindowTheme.ThemeScope.PungentFunkUtilitiesInspectorsAndOverlays:
                    return "Targets PungentFunk windows plus Pungent-owned inspectors/overlays as they adopt shared theme helpers.";
                case UtilityWindowTheme.ThemeScope.ExperimentalUnityEditorUssBridge:
                    return "Experimental: generates Unity editor USS plus optional reversible session GUIStyle text/font overrides.";
                default:
                    return "Stable: affects PungentFunk utility windows that use UtilityWindowTheme.";
            }
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSearch, _search ?? string.Empty);
            UtilityWindowPrefs.SetInt(PrefCategory, (int)_presetCategoryFilter);
            UtilityWindowPrefs.SetInt(PrefToneFilter, (int)_presetToneFilter);
            UtilityWindowPrefs.SetInt(PrefFamilyFilter, (int)_presetFamilyFilter);
            UtilityWindowPrefs.SetFloat(PrefLeftWidth, _leftWidth);
            UtilityWindowPrefs.SetFloat(PrefRightWidth, _rightWidth);
            UtilityWindowPrefs.SetBool(PrefShowTypography, _showTypography);
            UtilityWindowPrefs.SetBool(PrefShowPanels, _showPanels);
            UtilityWindowPrefs.SetBool(PrefShowManual, _showManual);
            UtilityWindowPrefs.SetBool(PrefShowAdvancedComposer, _showAdvancedComposer);
            UtilityWindowPrefs.SetBool(PrefShowReadabilityAudit, _showReadabilityAudit);
            UtilityWindowPrefs.SetBool(PrefShowExperimental, _showExperimental);
            SaveGeneratorPrefs();
        }
    }
#endif
}
