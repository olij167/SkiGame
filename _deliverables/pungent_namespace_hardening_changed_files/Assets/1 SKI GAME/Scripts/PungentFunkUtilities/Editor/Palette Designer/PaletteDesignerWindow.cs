using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Colour;

namespace PungentFunk.Utilities.Editor.Colour
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class PaletteDesignerWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.PaletteDesigner.";
        private const float SwatchMinWidth = 144f;
        private const float SwatchPreferredWidth = 160f;
        private const float SwatchMaxWidth = 218f;
        private const float SwatchGap = 6f;
        private const float WindowContentPadding = 64f;

        private static readonly PaletteSwatchRole[] GenerationRoleOrder =
        {
            PaletteSwatchRole.Background,
            PaletteSwatchRole.Panel,
            PaletteSwatchRole.Text,
            PaletteSwatchRole.MutedText,
            PaletteSwatchRole.Accent,
            PaletteSwatchRole.AccentSecondary,
            PaletteSwatchRole.Highlight,
            PaletteSwatchRole.Warning,
            PaletteSwatchRole.Success,
            PaletteSwatchRole.Error,
            PaletteSwatchRole.Outline,
            PaletteSwatchRole.Shadow
        };

        private const string PrefPaletteBoardHeight = PrefPrefix + "PaletteBoardHeight";
        private const string PrefSelectedHeight = PrefPrefix + "SelectedHeight";
        private const string PrefAnalysisHeight = PrefPrefix + "AnalysisHeight";
        private const string PrefApplyHeight = PrefPrefix + "ApplyHeight";

        private PungentColourPaletteSO _activePalette;
        private PungentColourPaletteSO _lastPalette;
        private PaletteGenerationSettings _workingSettings = new PaletteGenerationSettings();
        private readonly List<PaletteSwatch> _generatedPreview = new List<PaletteSwatch>();
        private PaletteAnalysisReport _analysisReport;
        private bool _analysisDirty = true;

        private Vector2 _mainScroll;
        private int _selectedSwatch = -1;

        private bool _showPaletteSettings = true;
        private bool _showAdvancedGeneration = false;
        private bool _showSelectedSwatch = true;
        private bool _showAnalysis = true;
        private bool _showApply = false;
        private bool _showProblemDetails = true;
        private bool _showContrastDetails = false;

        private float _paletteBoardHeight = 430f;
        private float _selectedHeight = 118f;
        private float _analysisHeight = 180f;
        private float _applyHeight = 220f;

        private ColourDeficiencyPreviewMode _deficiencyPreview;

        private PaletteSwatchRole _applyRole = PaletteSwatchRole.Accent;
        private bool _applyRenderers = true;
        private bool _applySelectedMaterials = true;
        private bool _applySpriteRenderers = true;
        private bool _applyUiGraphics = true;
        private bool _applyTmpText = true;
        private bool _modifySharedMaterials = false;
        private string _materialColorProperty = "_BaseColor";
        private readonly List<PaletteApplyTarget> _applyTargets = new List<PaletteApplyTarget>();
        private string _lastStatus = "Ready";

        [MenuItem("Tools/Utilities/Colour/Palette Designer")]
        public static void Open()
        {
            PaletteDesignerWindow window = GetWindow<PaletteDesignerWindow>();
            window.titleContent = new GUIContent("Palette Designer");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        [MenuItem("Tools/Pungent Colour Suite/Open Palette Designer")]
        public static void OpenLegacyAlias()
        {
            Open();
        }

        private void OnEnable()
        {
            _showPaletteSettings = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowPaletteSettings", true);
            _showAdvancedGeneration = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowAdvancedGeneration", false);
            _showSelectedSwatch = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowSelectedSwatch", true);
            _showAnalysis = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowAnalysis", true);
            _showApply = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowApply", false);
            _showProblemDetails = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowProblemDetails", true);
            _showContrastDetails = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowContrastDetails", false);
            _deficiencyPreview = (ColourDeficiencyPreviewMode)UtilityWindowPrefs.GetInt(PrefPrefix + "DeficiencyPreview", 0);
            _materialColorProperty = UtilityWindowPrefs.GetString(PrefPrefix + "MaterialColorProperty", "_BaseColor");
            _applyRole = (PaletteSwatchRole)UtilityWindowPrefs.GetInt(PrefPrefix + "ApplyRole", (int)PaletteSwatchRole.Accent);

            _paletteBoardHeight = UtilityWindowPrefs.GetFloat(PrefPaletteBoardHeight, _paletteBoardHeight);
            _selectedHeight = UtilityWindowPrefs.GetFloat(PrefSelectedHeight, _selectedHeight);
            _analysisHeight = UtilityWindowPrefs.GetFloat(PrefAnalysisHeight, _analysisHeight);
            _applyHeight = UtilityWindowPrefs.GetFloat(PrefApplyHeight, _applyHeight);

            if (Selection.activeObject is PungentColourPaletteSO selected)
                SetActivePalette(selected);
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowPaletteSettings", _showPaletteSettings);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowAdvancedGeneration", _showAdvancedGeneration);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowSelectedSwatch", _showSelectedSwatch);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowAnalysis", _showAnalysis);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowApply", _showApply);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowProblemDetails", _showProblemDetails);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowContrastDetails", _showContrastDetails);
            UtilityWindowPrefs.SetInt(PrefPrefix + "DeficiencyPreview", (int)_deficiencyPreview);
            UtilityWindowPrefs.SetString(PrefPrefix + "MaterialColorProperty", _materialColorProperty);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ApplyRole", (int)_applyRole);
            UtilityWindowPrefs.SetFloat(PrefPaletteBoardHeight, _paletteBoardHeight);
            UtilityWindowPrefs.SetFloat(PrefSelectedHeight, _selectedHeight);
            UtilityWindowPrefs.SetFloat(PrefAnalysisHeight, _analysisHeight);
            UtilityWindowPrefs.SetFloat(PrefApplyHeight, _applyHeight);
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is PungentColourPaletteSO selected && selected != _activePalette)
            {
                SetActivePalette(selected);
                Repaint();
            }
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();

            string status = _activePalette == null
                ? "No active palette"
                : $"{_activePalette.Count} swatches • {CountLockedSwatches()} locked • {_lastStatus}";

            UtilityWindowTheme.Header(
                "Palette Designer",
                "Palette-first colour generation, inline swatch editing, accessibility checks, and scene/material application.",
                status);

            DrawToolbar();

            if (_activePalette == null)
            {
                DrawEmptyState();
                return;
            }

            EnsurePaletteState();
            RefreshAnalysisIfNeeded();

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll, false, true);
            DrawPaletteInfoPanel();
            DrawPaletteBoardPanel();
            UtilityWindowTheme.VerticalResizeHandle(ref _paletteBoardHeight, 260f, 780f, SavePrefs, "Drag to reserve more or less visual space for the palette board");

            DrawAdvancedGenerationPanel();

            if (_showSelectedSwatch)
            {
                DrawSelectedSwatchPanel();
                UtilityWindowTheme.VerticalResizeHandle(ref _selectedHeight, 86f, 260f, SavePrefs, "Drag to resize the selected swatch detail panel");
            }

            if (_showAnalysis)
            {
                DrawAnalysisPanel();
                UtilityWindowTheme.VerticalResizeHandle(ref _analysisHeight, 110f, 420f, SavePrefs, "Drag to resize the analysis panel");
            }

            if (_showApply)
            {
                DrawApplyPanel();
                UtilityWindowTheme.VerticalResizeHandle(ref _applyHeight, 150f, 500f, SavePrefs, "Drag to resize the apply panel");
            }

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.12f, 0.07f, 7, 4)))
            {
                bool compact = CurrentContentWidth() < 820f;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    PungentColourPaletteSO nextPalette = (PungentColourPaletteSO)EditorGUILayout.ObjectField("Active Palette", _activePalette, typeof(PungentColourPaletteSO), false, GUILayout.MinWidth(180f));
                    if (EditorGUI.EndChangeCheck())
                        SetActivePalette(nextPalette);

                    if (!compact)
                        DrawPrimaryPaletteButtons();
                }

                if (compact)
                    DrawPrimaryPaletteButtons();

                DrawUtilityToolbar(compact);
            }
        }

        private void DrawPrimaryPaletteButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (UtilityWindowTheme.TintedButton("New", UtilityWindowTheme.Green, GUILayout.MinWidth(54f), GUILayout.MaxWidth(70f)))
                    SetActivePalette(PungentPaletteStorageUtility.CreatePaletteAsset());

                if (UtilityWindowTheme.TintedButton("Load Selected", UtilityWindowTheme.Cyan, GUILayout.MinWidth(94f), GUILayout.MaxWidth(118f)))
                {
                    if (Selection.activeObject is PungentColourPaletteSO selected)
                        SetActivePalette(selected);
                    else
                        _lastStatus = "Select a PungentColourPaletteSO asset first.";
                }

                GUI.enabled = _activePalette != null;
                if (UtilityWindowTheme.TintedButton("Save", UtilityWindowTheme.Blue, GUILayout.MinWidth(52f), GUILayout.MaxWidth(68f)))
                {
                    PungentPaletteStorageUtility.SaveExisting(_activePalette);
                    _lastStatus = "Saved.";
                }

                if (UtilityWindowTheme.TintedButton("Save As", UtilityWindowTheme.Purple, GUILayout.MinWidth(66f), GUILayout.MaxWidth(86f)))
                {
                    PungentColourPaletteSO saved = PungentPaletteStorageUtility.SaveAsAsset(_activePalette);
                    if (saved != null)
                        SetActivePalette(saved);
                }

                if (UtilityWindowTheme.TintedButton("Duplicate", UtilityWindowTheme.Amber, GUILayout.MinWidth(74f), GUILayout.MaxWidth(96f)))
                {
                    PungentColourPaletteSO duplicate = PungentPaletteStorageUtility.DuplicateAsset(_activePalette);
                    if (duplicate != null)
                        SetActivePalette(duplicate);
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawUtilityToolbar(bool compact)
        {
            GUI.enabled = _activePalette != null;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("Copy Values", "Copy all swatch values as text."), EditorStyles.toolbarButton, GUILayout.Width(92f)))
                {
                    PungentPaletteStorageUtility.CopySwatchValuesToClipboard(_activePalette);
                    _lastStatus = "Copied swatch values.";
                }

                if (GUILayout.Button(new GUIContent("Copy JSON", "Copy this palette as JSON."), EditorStyles.toolbarButton, GUILayout.Width(82f)))
                {
                    PungentPaletteStorageUtility.CopyPaletteJsonToClipboard(_activePalette);
                    _lastStatus = "Copied JSON.";
                }

                if (!compact && GUILayout.Button(new GUIContent("Ping Asset", "Ping the active palette asset in the Project window."), EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    EditorGUIUtility.PingObject(_activePalette);

                GUILayout.FlexibleSpace();

                if (!compact)
                {
                    ToolbarFoldoutToggle(ref _showSelectedSwatch, "Selected", 76f);
                    ToolbarFoldoutToggle(ref _showAnalysis, "Analysis", 76f);
                    ToolbarFoldoutToggle(ref _showApply, "Apply", 58f);
                    GUILayout.Space(8f);
                }

                EditorGUILayout.LabelField("Preview", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(48f));
                EditorGUI.BeginChangeCheck();
                _deficiencyPreview = (ColourDeficiencyPreviewMode)EditorGUILayout.EnumPopup(_deficiencyPreview, EditorStyles.toolbarPopup, GUILayout.Width(compact ? 132f : 158f));
                if (EditorGUI.EndChangeCheck())
                    Repaint();
            }

            if (compact)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    ToolbarFoldoutToggle(ref _showSelectedSwatch, "Selected", 76f);
                    ToolbarFoldoutToggle(ref _showAnalysis, "Analysis", 76f);
                    ToolbarFoldoutToggle(ref _showApply, "Apply", 58f);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Ping", "Ping the active palette asset in the Project window."), EditorStyles.toolbarButton, GUILayout.Width(54f)))
                        EditorGUIUtility.PingObject(_activePalette);
                }
            }
            GUI.enabled = true;
        }

        private static void ToolbarFoldoutToggle(ref bool value, string label, float width)
        {
            bool next = GUILayout.Toggle(value, label, EditorStyles.toolbarButton, GUILayout.Width(width));
            if (next != value)
                value = next;
        }

        private void DrawEmptyState()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.16f, 0.08f)))
            {
                EditorGUILayout.LabelField("No palette selected", UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.HelpBox("Create a new palette asset or select an existing PungentColourPaletteSO in the Project window, then click Load Selected.", MessageType.Info);
                if (UtilityWindowTheme.TintedButton("Create Starter Palette", UtilityWindowTheme.Green, GUILayout.Height(30f)))
                    SetActivePalette(PungentPaletteStorageUtility.CreatePaletteAsset());
            }
        }

        private void DrawPaletteInfoPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle(string.IsNullOrWhiteSpace(_activePalette.paletteName) ? _activePalette.name : _activePalette.paletteName, UtilityWindowTheme.Blue, _activePalette.Count.ToString());
                    GUILayout.FlexibleSpace();
                    DrawAnalysisSummaryPills();
                }

                DrawPaletteRibbon(_activePalette.swatches, 22f);

                _showPaletteSettings = EditorGUILayout.Foldout(_showPaletteSettings, "Palette metadata", true);
                if (!_showPaletteSettings)
                    return;

                EditorGUI.BeginChangeCheck();
                string paletteName = EditorGUILayout.TextField("Name", _activePalette.paletteName);
                string notes = EditorGUILayout.TextArea(_activePalette.notes, GUILayout.MinHeight(30f));
                if (EditorGUI.EndChangeCheck())
                {
                    ChangePalette("Edit Palette Metadata", () =>
                    {
                        _activePalette.paletteName = paletteName;
                        _activePalette.notes = notes;
                    });
                }
            }
        }

        private void DrawPaletteBoardPanel()
        {
            float contentHeight = EstimatePaletteBoardHeight();
            float reservedHeight = Mathf.Min(_paletteBoardHeight, contentHeight);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.14f, 0.075f, 6, 4), GUILayout.MinHeight(reservedHeight)))
            {
                DrawPaletteGenerationHeader();

                if (_activePalette.swatches == null || _activePalette.swatches.Count == 0)
                {
                    EditorGUILayout.HelpBox("This palette has no swatches yet. Add a swatch or generate a starter palette from the header controls.", MessageType.Info);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UtilityWindowTheme.TintedButton("Add Swatch", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                            AddSwatch();
                        if (UtilityWindowTheme.TintedButton("Generate Palette", UtilityWindowTheme.Purple, GUILayout.Height(28f)))
                            RegeneratePaletteUnlocked();
                    }
                    return;
                }

                DrawPaletteActionRow();
                DrawPaletteSwatchGrid();
            }
        }

        private void DrawPaletteGenerationHeader()
        {
            bool compact = CurrentContentWidth() < 720f;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.SectionTitle("Palette", UtilityWindowTheme.Blue, $"{_activePalette.Count} swatches");
                GUILayout.FlexibleSpace();

                if (!compact)
                    DrawHarmonyAndRegenerateControls();
            }

            if (compact)
                DrawHarmonyAndRegenerateControls();

            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, _showAdvancedGeneration ? 0.18f : 0.095f, _showAdvancedGeneration ? 0.11f : 0.045f, 4, 2)))
            {
                EditorGUILayout.LabelField("Inline swatch controls: edit, copy, lock, regenerate, duplicate, or remove without leaving the palette grid.", UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();
                DrawAdvancedGenerationToggle(compact);
            }
        }

        private void DrawHarmonyAndRegenerateControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Harmony", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(54f));
                EditorGUI.BeginChangeCheck();
                _workingSettings.harmonyMode = (ColourHarmonyMode)EditorGUILayout.EnumPopup(_workingSettings.harmonyMode, GUILayout.MinWidth(128f), GUILayout.MaxWidth(170f));
                if (EditorGUI.EndChangeCheck())
                {
                    _workingSettings.Clamp();
                    SaveWorkingSettingsAsDefaults(false);
                }

                if (UtilityWindowTheme.TintedButton("Regenerate", UtilityWindowTheme.Purple, GUILayout.MinWidth(104f), GUILayout.MaxWidth(128f), GUILayout.Height(24f)))
                    RegeneratePaletteUnlocked();
            }
        }

        private void DrawAdvancedGenerationToggle(bool compact)
        {
            string label = _showAdvancedGeneration ? "▲ Hide Advanced Generation" : "⚙ Advanced Generation";
            string tooltip = _showAdvancedGeneration
                ? "Collapse detailed seed, range, contrast, variation, and preview controls."
                : "Open detailed seed, range, contrast, variation, and preview controls.";

            Color tint = _showAdvancedGeneration ? UtilityWindowTheme.Amber : UtilityWindowTheme.Purple;
            GUIContent content = new GUIContent(label, tooltip);

            using (UtilityWindowTheme.Background(tint))
            {
                if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(compact ? 176f : 214f), GUILayout.Height(24f)))
                    _showAdvancedGeneration = !_showAdvancedGeneration;
            }
        }

        private void DrawPaletteActionRow()
        {
            bool compact = CurrentContentWidth() < 720f;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.075f, 0.035f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (TintedMiniButton(new GUIContent("+ Swatch", "Add a new editable swatch."), UtilityWindowTheme.Green, GUILayout.Width(78f)))
                        AddSwatch();

                    GUI.enabled = IsValidSelectedSwatch();
                    if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate the selected swatch."), EditorStyles.miniButton, GUILayout.Width(76f)))
                        DuplicateSelectedSwatch();
                    if (GUILayout.Button(new GUIContent("Remove", "Remove the selected swatch."), EditorStyles.miniButton, GUILayout.Width(68f)))
                        RemoveSelectedSwatch();
                    GUI.enabled = true;

                    GUILayout.Space(compact ? 4f : 10f);
                    if (!compact)
                        DrawSortButtons();

                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"{CountLockedSwatches()} locked", CountLockedSwatches() > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 82f);
                }

                if (compact)
                    DrawSortButtons();
            }
        }

        private void DrawSortButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Sort", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(28f));
                if (GUILayout.Button(new GUIContent("Role", "Sort by swatch role."), EditorStyles.miniButton, GUILayout.Width(46f)))
                    SortSwatches(0);
                if (GUILayout.Button(new GUIContent("Hue", "Sort by hue."), EditorStyles.miniButton, GUILayout.Width(44f)))
                    SortSwatches(1);
                if (GUILayout.Button(new GUIContent("Value", "Sort by brightness/value."), EditorStyles.miniButton, GUILayout.Width(52f)))
                    SortSwatches(2);
                if (GUILayout.Button(new GUIContent("Names", "Rename empty/default swatches from their roles."), EditorStyles.miniButton, GUILayout.Width(56f)))
                    NormalizeNames();
                GUILayout.FlexibleSpace();
            }
        }

        private float CurrentContentWidth()
        {
            return Mathf.Max(320f, position.width - WindowContentPadding);
        }

        private float EstimatePaletteBoardHeight()
        {
            int count = _activePalette != null && _activePalette.swatches != null ? _activePalette.swatches.Count : 0;
            if (count <= 0)
                return 180f;

            float available = CurrentContentWidth();
            int columns = Mathf.Clamp(Mathf.FloorToInt((available + SwatchGap) / (SwatchPreferredWidth + SwatchGap)), 1, 12);
            float tileWidth = Mathf.Floor((available - ((columns - 1) * SwatchGap)) / columns);
            while (columns > 1 && tileWidth < SwatchMinWidth)
            {
                columns--;
                tileWidth = Mathf.Floor((available - ((columns - 1) * SwatchGap)) / columns);
            }

            int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
            return 92f + rows * 158f;
        }

        private static bool TintedMiniButton(GUIContent content, Color tint, params GUILayoutOption[] options)
        {
            using (UtilityWindowTheme.Background(tint))
                return GUILayout.Button(content, EditorStyles.miniButton, options);
        }

        private void DrawPaletteSwatchGrid()
        {
            float available = CurrentContentWidth();
            int columns = Mathf.Clamp(Mathf.FloorToInt((available + SwatchGap) / (SwatchPreferredWidth + SwatchGap)), 1, 12);
            float tileWidth = Mathf.Floor((available - ((columns - 1) * SwatchGap)) / columns);

            while (columns > 1 && tileWidth < SwatchMinWidth)
            {
                columns--;
                tileWidth = Mathf.Floor((available - ((columns - 1) * SwatchGap)) / columns);
            }

            tileWidth = Mathf.Clamp(tileWidth, SwatchMinWidth, SwatchMaxWidth);

            for (int i = 0; i < _activePalette.swatches.Count; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int index = i + col;
                        if (index >= _activePalette.swatches.Count)
                            break;

                        DrawSwatchTile(index, tileWidth);
                        if (col < columns - 1 && index < _activePalette.swatches.Count - 1)
                            GUILayout.Space(SwatchGap);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawSwatchTile(int index, float width)
        {
            PaletteSwatch swatch = _activePalette.swatches[index];
            if (swatch == null)
                return;

            bool selected = index == _selectedSwatch;
            Color panelTint = selected ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral;
            float previewHeight = Mathf.Clamp(width * 0.43f, 54f, 76f);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(panelTint, selected ? 0.24f : 0.11f, selected ? 0.15f : 0.055f, 4, 2), GUILayout.Width(width)))
            {
                Rect preview = GUILayoutUtility.GetRect(width - 8f, previewHeight, GUILayout.ExpandWidth(true), GUILayout.Height(previewHeight));
                DrawSwatchPreview(index, swatch, preview, selected);

                EditorGUI.BeginChangeCheck();
                string nextName = EditorGUILayout.TextField(swatch.name, GUILayout.MinWidth(40f));
                if (EditorGUI.EndChangeCheck())
                    ChangePalette("Rename Swatch", () => swatch.name = nextName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    Color nextColor = EditorGUILayout.ColorField(GUIContent.none, swatch.color, true, true, false, GUILayout.Height(18f), GUILayout.MinWidth(42f));
                    if (EditorGUI.EndChangeCheck())
                        ChangePalette("Edit Swatch Colour", () => swatch.color = nextColor);

                    string hex = ColourConversionUtility.ToHexRGB(swatch.color);
                    if (GUILayout.Button(new GUIContent(hex, "Copy hex RGB."), EditorStyles.miniButton, GUILayout.Width(Mathf.Min(70f, width * 0.42f))))
                    {
                        EditorGUIUtility.systemCopyBuffer = ColourConversionUtility.ToHexRGBA(swatch.color);
                        _lastStatus = $"Copied {swatch.name} hex.";
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = !swatch.locked;
                    if (TintedMiniButton(new GUIContent("↻", "Regenerate this swatch."), UtilityWindowTheme.Purple, GUILayout.Width(24f)))
                        RegenerateSwatch(index);
                    GUI.enabled = true;

                    EditorGUI.BeginChangeCheck();
                    bool locked = GUILayout.Toggle(swatch.locked, new GUIContent(swatch.locked ? "Lock" : "Free", swatch.locked ? "Unlock this swatch." : "Lock this swatch so palette regeneration preserves it."), EditorStyles.miniButton, GUILayout.Width(42f));
                    if (EditorGUI.EndChangeCheck())
                        ChangePalette("Toggle Swatch Lock", () => swatch.locked = locked);

                    if (GUILayout.Button(new GUIContent("Copy", "Copy this swatch in several formats."), EditorStyles.miniButton, GUILayout.Width(38f)))
                        ShowCopyMenu(swatch);

                    if (GUILayout.Button(new GUIContent("⋯", "More swatch actions."), EditorStyles.miniButton, GUILayout.Width(22f)))
                        ShowSwatchMenu(index);
                }

                EditorGUI.BeginChangeCheck();
                PaletteSwatchRole nextRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup(swatch.role, GUILayout.MinWidth(40f));
                if (EditorGUI.EndChangeCheck())
                    ChangePalette("Edit Swatch Role", () => swatch.role = nextRole);
            }
        }

        private void DrawSwatchPreview(int index, PaletteSwatch swatch, Rect preview, bool selected)
        {
            Color previewColor = ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color);
            EditorGUI.DrawRect(preview, previewColor);

            Color readable = ColourContrastUtility.GetReadableTextColor(swatch.color);
            Color shade = new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.28f : 0.16f);
            EditorGUI.DrawRect(new Rect(preview.x, preview.y, preview.width, 18f), shade);
            EditorGUI.DrawRect(new Rect(preview.x, preview.yMax - 8f, preview.width, 8f), readable);

            GUIStyle overlay = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(preview.x + 5f, preview.y + 1f, Mathf.Max(40f, preview.width - 10f), 16f), $"#{index + 1}  {swatch.role}", overlay);

            if (swatch.locked)
            {
                GUIStyle lockStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(preview.x + preview.width - 56f, preview.yMax - 24f, 50f, 16f), "LOCK", lockStyle);
            }

            if (selected && Event.current.type == EventType.Repaint)
                Handles.DrawSolidRectangleWithOutline(preview, Color.clear, UtilityWindowTheme.Cyan);

            if (Event.current.type == EventType.MouseDown && preview.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 1)
                {
                    ShowSwatchMenu(index);
                }
                else
                {
                    _selectedSwatch = index;
                    GUI.FocusControl(null);
                    Repaint();
                }
                Event.current.Use();
            }
        }

        private void DrawAdvancedGenerationPanel()
        {
            if (!_showAdvancedGeneration)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.12f, 0.065f, 7, 4)))
            {
                UtilityWindowTheme.SectionTitle("Advanced Generation", UtilityWindowTheme.Purple, _workingSettings.harmonyMode.ToString());

                EditorGUI.BeginChangeCheck();

                bool compact = CurrentContentWidth() < 760f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    _workingSettings.targetSwatchCount = EditorGUILayout.IntSlider("Target Swatches", _workingSettings.targetSwatchCount, 1, 32);
                    _workingSettings.useSeed = EditorGUILayout.ToggleLeft("Seed", _workingSettings.useSeed, GUILayout.Width(54f));
                    if (_workingSettings.useSeed)
                        _workingSettings.seed = EditorGUILayout.IntField(_workingSettings.seed, GUILayout.Width(84f));
                }

                DrawGenerationSliders(compact);
                DrawGenerationToggles(compact);

                _workingSettings.hueRange = DrawRange("Hue", _workingSettings.hueRange, 0f, 1f);
                _workingSettings.saturationRange = DrawRange("Saturation", _workingSettings.saturationRange, 0f, 1f);
                _workingSettings.valueRange = DrawRange("Value", _workingSettings.valueRange, 0f, 1f);

                if (EditorGUI.EndChangeCheck())
                {
                    _workingSettings.Clamp();
                    SaveWorkingSettingsAsDefaults(false);
                }

                DrawAdvancedGenerationActions(compact);
                DrawGeneratedPreviewTools();
            }
        }

        private void DrawGenerationSliders(bool compact)
        {
            if (compact)
            {
                _workingSettings.harmonyInfluence = EditorGUILayout.Slider("Harmony", _workingSettings.harmonyInfluence, 0f, 1f);
                _workingSettings.randomVariation = EditorGUILayout.Slider("Variation", _workingSettings.randomVariation, 0f, 1f);
                _workingSettings.contrastInfluence = EditorGUILayout.Slider("Contrast", _workingSettings.contrastInfluence, 0f, 1f);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _workingSettings.harmonyInfluence = EditorGUILayout.Slider("Harmony", _workingSettings.harmonyInfluence, 0f, 1f);
                _workingSettings.randomVariation = EditorGUILayout.Slider("Variation", _workingSettings.randomVariation, 0f, 1f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _workingSettings.contrastInfluence = EditorGUILayout.Slider("Contrast", _workingSettings.contrastInfluence, 0f, 1f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawGenerationToggles(bool compact)
        {
            if (compact)
            {
                _workingSettings.preserveLockedSwatches = EditorGUILayout.ToggleLeft("Use locked anchors", _workingSettings.preserveLockedSwatches);
                _workingSettings.enforceTextContrast = EditorGUILayout.ToggleLeft("Enforce text contrast", _workingSettings.enforceTextContrast);
                _workingSettings.avoidNearDuplicates = EditorGUILayout.ToggleLeft("Avoid near-duplicates", _workingSettings.avoidNearDuplicates);
                _workingSettings.preferReadableAccentPairs = EditorGUILayout.ToggleLeft("Prefer readable accents", _workingSettings.preferReadableAccentPairs);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _workingSettings.preserveLockedSwatches = EditorGUILayout.ToggleLeft("Use locked anchors", _workingSettings.preserveLockedSwatches, GUILayout.Width(142f));
                _workingSettings.enforceTextContrast = EditorGUILayout.ToggleLeft("Enforce text contrast", _workingSettings.enforceTextContrast, GUILayout.Width(160f));
                _workingSettings.avoidNearDuplicates = EditorGUILayout.ToggleLeft("Avoid duplicates", _workingSettings.avoidNearDuplicates, GUILayout.Width(130f));
                _workingSettings.preferReadableAccentPairs = EditorGUILayout.ToggleLeft("Readable accents", _workingSettings.preferReadableAccentPairs, GUILayout.Width(126f));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawAdvancedGenerationActions(bool compact)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Tighten Ranges From Palette"))
                {
                    _workingSettings = PaletteGeneratorUtility.TightenRangesFromSwatches(_activePalette.swatches, _workingSettings);
                    _workingSettings.Clamp();
                    SaveWorkingSettingsAsDefaults(false);
                    _lastStatus = "Updated generation ranges from palette.";
                }

                if (!compact)
                    DrawRangePresetButtons();
            }

            if (compact)
                DrawRangePresetButtons();
        }

        private void DrawRangePresetButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Ranges", GUILayout.Width(110f)))
                {
                    _workingSettings.hueRange = new Vector2(0f, 1f);
                    _workingSettings.saturationRange = new Vector2(0.35f, 0.95f);
                    _workingSettings.valueRange = new Vector2(0.18f, 0.95f);
                    SaveWorkingSettingsAsDefaults(false);
                    _lastStatus = "Reset generation ranges.";
                }

                if (GUILayout.Button("Load Defaults", GUILayout.Width(108f)))
                {
                    _workingSettings = _activePalette.defaultGenerationSettings != null ? _activePalette.defaultGenerationSettings.Clone() : new PaletteGenerationSettings();
                    _workingSettings.Clamp();
                }

                if (GUILayout.Button("Save Defaults", GUILayout.Width(112f)))
                    SaveWorkingSettingsAsDefaults(true);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawGeneratedPreviewTools()
        {
            bool compact = CurrentContentWidth() < 700f;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Preview New Set", "Generate a temporary preview without changing the active palette.")))
                {
                    _generatedPreview.Clear();
                    PaletteGenerationSettings settings = CurrentGenerationSettingsForPalette(false);
                    _generatedPreview.AddRange(GenerateWindowPalette(settings, _activePalette.swatches));
                    _lastStatus = "Generated preview.";
                }

                if (!compact)
                    DrawPreviewCommitButtons();
            }

            if (compact)
                DrawPreviewCommitButtons();

            if (_generatedPreview.Count > 0)
                DrawPreviewSwatchGrid(_generatedPreview, 82f, 38f);
        }

        private void DrawPreviewCommitButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _generatedPreview.Count > 0;
                if (GUILayout.Button(new GUIContent("Replace Unlocked", "Replace unlocked palette swatches from the current preview.")))
                    CommitPreviewToUnlocked();
                if (GUILayout.Button(new GUIContent("Append Preview", "Append the preview colours to the active palette."), GUILayout.Width(112f)))
                    CommitPreviewAppend();
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSelectedSwatchPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.11f, 0.06f, 7, 4), GUILayout.MinHeight(_selectedHeight)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Selected Swatch", UtilityWindowTheme.Teal, IsValidSelectedSwatch() ? (_selectedSwatch + 1).ToString() : null);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Hide", EditorStyles.miniButton, GUILayout.Width(48f)))
                        _showSelectedSwatch = false;
                }

                if (!IsValidSelectedSwatch())
                {
                    EditorGUILayout.HelpBox("Click a swatch in the palette grid to edit it here. Common edits are also available inline on each swatch tile.", MessageType.Info);
                    return;
                }

                PaletteSwatch swatch = _activePalette.swatches[_selectedSwatch];
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect preview = GUILayoutUtility.GetRect(74f, 68f, GUILayout.Width(74f), GUILayout.Height(68f));
                    EditorGUI.DrawRect(preview, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                    EditorGUI.DrawRect(new Rect(preview.x, preview.yMax - 10f, preview.width, 10f), ColourContrastUtility.GetReadableTextColor(swatch.color));

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        string name = EditorGUILayout.TextField("Name", swatch.name);
                        Color color = EditorGUILayout.ColorField(new GUIContent("Colour"), swatch.color, true, true, false);
                        PaletteSwatchRole role = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Role", swatch.role);
                        if (EditorGUI.EndChangeCheck())
                        {
                            ChangePalette("Edit Swatch", () =>
                            {
                                swatch.name = name;
                                swatch.color = color;
                                swatch.role = role;
                            });
                        }
                    }

                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(132f)))
                    {
                        GUI.enabled = !swatch.locked;
                        if (UtilityWindowTheme.TintedButton("Regenerate", UtilityWindowTheme.Purple, GUILayout.Height(24f)))
                            RegenerateSwatch(_selectedSwatch);
                        GUI.enabled = true;

                        if (GUILayout.Button("Copy Values", EditorStyles.miniButton))
                            ShowCopyMenu(swatch);
                        if (GUILayout.Button(swatch.locked ? "Unlock" : "Lock", EditorStyles.miniButton))
                            ChangePalette("Toggle Swatch Lock", () => swatch.locked = !swatch.locked);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    int priority = EditorGUILayout.IntField("Priority", swatch.priority, GUILayout.MaxWidth(220f));
                    string notes = EditorGUILayout.TextField("Notes", swatch.notes);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ChangePalette("Edit Swatch Metadata", () =>
                        {
                            swatch.priority = priority;
                            swatch.notes = notes;
                        });
                    }
                }
            }
        }

        private void DrawAnalysisPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.12f, 0.065f, 7, 4), GUILayout.MinHeight(_analysisHeight)))
            {
                int problems = _analysisReport != null && _analysisReport.problems != null ? _analysisReport.problems.Count : 0;
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Analysis / Accessibility", UtilityWindowTheme.Cyan, problems == 0 ? "OK" : problems.ToString());
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Hide", EditorStyles.miniButton, GUILayout.Width(48f)))
                        _showAnalysis = false;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Preview Mode", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(84f));
                    EditorGUI.BeginChangeCheck();
                    _deficiencyPreview = (ColourDeficiencyPreviewMode)EditorGUILayout.EnumPopup(_deficiencyPreview, GUILayout.MaxWidth(190f));
                    if (EditorGUI.EndChangeCheck())
                        Repaint();

                    GUILayout.FlexibleSpace();
                    DrawAnalysisSummaryPills();
                }

                DrawPaletteRibbon(_activePalette.swatches, 18f);
                DrawProblemSummary();
                DrawContrastSummary();
                DrawRepairButtons();
            }
        }

        private void DrawApplyPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green, 0.12f, 0.065f, 7, 4), GUILayout.MinHeight(_applyHeight)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Apply Palette", UtilityWindowTheme.Green, _applyTargets.Count > 0 ? _applyTargets.Count.ToString() : null);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Hide", EditorStyles.miniButton, GUILayout.Width(48f)))
                        _showApply = false;
                }

                _applyRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Swatch Role", _applyRole);
                PaletteSwatch applySwatch = PaletteAnalysisUtility.FindRole(_activePalette.swatches, _applyRole);
                if (applySwatch == null && IsValidSelectedSwatch())
                    applySwatch = _activePalette.swatches[_selectedSwatch];

                if (applySwatch != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect preview = GUILayoutUtility.GetRect(38f, 28f, GUILayout.Width(38f), GUILayout.Height(28f));
                        EditorGUI.DrawRect(preview, applySwatch.color);
                        EditorGUILayout.LabelField($"{applySwatch.name} • {ColourConversionUtility.ToHexRGB(applySwatch.color)}", UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No swatch found for this role. Select a swatch in the palette to use it as a fallback.", MessageType.Info);
                }

                DrawApplyTargetToggles();

                _materialColorProperty = EditorGUILayout.TextField("Material Property", _materialColorProperty);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Scan Selection"))
                    {
                        _applyTargets.Clear();
                        _applyTargets.AddRange(PaletteApplyUtility.ScanSelection(_applyRenderers, _applySelectedMaterials, _applySpriteRenderers, _applyUiGraphics, _applyTmpText));
                        _lastStatus = $"Scanned {_applyTargets.Count} apply targets.";
                    }

                    GUI.enabled = applySwatch != null && _applyTargets.Count > 0;
                    if (UtilityWindowTheme.TintedButton("Apply", UtilityWindowTheme.Green, GUILayout.Width(88f)))
                    {
                        if (_modifySharedMaterials || EditorUtility.DisplayDialog("Apply Palette Colour", "This will apply the chosen swatch colour to scanned targets. Renderer materials use instance materials unless shared-material modification is enabled.", "Apply", "Cancel"))
                        {
                            int changed = PaletteApplyUtility.ApplyColor(_applyTargets, applySwatch.color, _modifySharedMaterials, _materialColorProperty);
                            _lastStatus = $"Applied colour to {changed} target(s).";
                        }
                    }
                    GUI.enabled = true;
                }

                if (_applyTargets.Count > 0)
                {
                    EditorGUILayout.LabelField("Preview Targets", UtilityWindowTheme.SectionHeaderStyle);
                    int max = Mathf.Min(10, _applyTargets.Count);
                    for (int i = 0; i < max; i++)
                    {
                        PaletteApplyTarget target = _applyTargets[i];
                        if (target == null)
                            continue;
                        EditorGUILayout.ObjectField(target.description, target.targetObject, typeof(Object), true);
                    }
                    if (_applyTargets.Count > max)
                        EditorGUILayout.LabelField($"...and {_applyTargets.Count - max} more.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawApplyTargetToggles()
        {
            bool compact = CurrentContentWidth() < 820f;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.065f, 0.035f, 4, 2)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _applyRenderers = EditorGUILayout.ToggleLeft("Renderer materials", _applyRenderers, GUILayout.Width(138f));
                    _applySelectedMaterials = EditorGUILayout.ToggleLeft("Selected material assets", _applySelectedMaterials, GUILayout.Width(170f));
                    if (!compact)
                    {
                        _applySpriteRenderers = EditorGUILayout.ToggleLeft("Sprite renderers", _applySpriteRenderers, GUILayout.Width(128f));
                        _applyUiGraphics = EditorGUILayout.ToggleLeft("UI Graphics", _applyUiGraphics, GUILayout.Width(100f));
                        _applyTmpText = EditorGUILayout.ToggleLeft("TMP Text", _applyTmpText, GUILayout.Width(88f));
                        _modifySharedMaterials = EditorGUILayout.ToggleLeft("Shared materials", _modifySharedMaterials, GUILayout.Width(122f));
                    }
                    GUILayout.FlexibleSpace();
                }

                if (compact)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _applySpriteRenderers = EditorGUILayout.ToggleLeft("Sprite renderers", _applySpriteRenderers, GUILayout.Width(128f));
                        _applyUiGraphics = EditorGUILayout.ToggleLeft("UI Graphics", _applyUiGraphics, GUILayout.Width(100f));
                        _applyTmpText = EditorGUILayout.ToggleLeft("TMP Text", _applyTmpText, GUILayout.Width(88f));
                        _modifySharedMaterials = EditorGUILayout.ToggleLeft("Shared materials", _modifySharedMaterials, GUILayout.Width(122f));
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private void DrawAnalysisSummaryPills()
        {
            int problems = _analysisReport != null && _analysisReport.problems != null ? _analysisReport.problems.Count : 0;
            int pairs = _analysisReport != null && _analysisReport.pairs != null ? _analysisReport.pairs.Count : 0;
            UtilityWindowTheme.CountPill(problems == 0 ? "Accessible" : $"{problems} issues", problems == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 90f);
            UtilityWindowTheme.CountPill($"{pairs} pairs", UtilityWindowTheme.Cyan, 70f);
        }

        private void DrawPaletteRibbon(List<PaletteSwatch> swatches, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.84f, 0.84f, 0.86f));

            if (swatches == null || swatches.Count == 0)
                return;

            int count = 0;
            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null)
                    count++;
            }
            if (count == 0)
                return;

            float x = rect.x;
            float segmentWidth = rect.width / count;
            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                Rect segment = new Rect(x, rect.y, Mathf.Ceil(segmentWidth), rect.height);
                EditorGUI.DrawRect(segment, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                x += segmentWidth;
            }
        }

        private void DrawProblemSummary()
        {
            if (_analysisReport == null)
                return;

            _showProblemDetails = EditorGUILayout.Foldout(_showProblemDetails, "Problems and recommendations", true);
            if (!_showProblemDetails)
                return;

            if (_analysisReport.problems.Count == 0 && _analysisReport.recommendations.Count == 0)
            {
                EditorGUILayout.HelpBox("No major palette issues detected.", MessageType.Info);
                return;
            }

            int problemMax = Mathf.Min(4, _analysisReport.problems.Count);
            for (int i = 0; i < problemMax; i++)
                DrawCompactMessage(_analysisReport.problems[i], UtilityWindowTheme.Amber);
            if (_analysisReport.problems.Count > problemMax)
                EditorGUILayout.LabelField($"...and {_analysisReport.problems.Count - problemMax} more issues.", UtilityWindowTheme.MutedMiniLabelStyle);

            int recommendationMax = Mathf.Min(3, _analysisReport.recommendations.Count);
            for (int i = 0; i < recommendationMax; i++)
                DrawCompactMessage(_analysisReport.recommendations[i], UtilityWindowTheme.Cyan);
        }

        private void DrawCompactMessage(string message, Color tint)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.08f, 0.04f, 5, 2)))
                EditorGUILayout.LabelField(message, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawContrastSummary()
        {
            if (_analysisReport == null)
                return;

            _showContrastDetails = EditorGUILayout.Foldout(_showContrastDetails, "Contrast pairs", true);
            if (!_showContrastDetails)
            {
                if (_analysisReport.pairs.Count > 0)
                    DrawPairRow(_analysisReport.pairs[0]);
                return;
            }

            if (_analysisReport.rolePairs.Count > 0)
            {
                EditorGUILayout.LabelField("Role Pair Checks", UtilityWindowTheme.SectionHeaderStyle);
                for (int i = 0; i < _analysisReport.rolePairs.Count; i++)
                    DrawPairRow(_analysisReport.rolePairs[i]);
            }

            if (_analysisReport.pairs.Count > 0)
            {
                EditorGUILayout.LabelField("Lowest Contrast Pairs", UtilityWindowTheme.SectionHeaderStyle);
                int max = Mathf.Min(6, _analysisReport.pairs.Count);
                for (int i = 0; i < max; i++)
                    DrawPairRow(_analysisReport.pairs[i]);
            }
        }

        private void DrawPairRow(PaletteContrastPair pair)
        {
            if (pair == null)
                return;

            Color tint = pair.contrast >= 4.5f ? UtilityWindowTheme.Green : pair.contrast >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(tint, 0.08f, 0.05f, 5, 2)))
            {
                EditorGUILayout.LabelField(pair.label, GUILayout.MinWidth(80f));
                EditorGUILayout.LabelField(pair.otherLabel, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(70f));
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill($"{pair.contrast:0.00}:1", tint, 64f);
                EditorGUILayout.LabelField(pair.WCAG, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(62f));
            }
        }

        private void DrawRepairButtons()
        {
            EditorGUILayout.LabelField("Quick Repairs", UtilityWindowTheme.SectionHeaderStyle);
            bool compact = CurrentContentWidth() < 700f;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Improve Text Roles"))
                    ImproveTextRoles();
                if (GUILayout.Button("Add Neutral Panel"))
                    AddOrRepairPanelRole();

                if (!compact)
                {
                    if (GUILayout.Button("Add Readable Text"))
                        AddOrRepairTextRole();
                    GUI.enabled = IsValidSelectedSwatch();
                    if (GUILayout.Button("Repair Selected"))
                        RepairSelectedAgainstBackground();
                    GUI.enabled = true;
                }
            }

            if (compact)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Readable Text"))
                        AddOrRepairTextRole();
                    GUI.enabled = IsValidSelectedSwatch();
                    if (GUILayout.Button("Repair Selected"))
                        RepairSelectedAgainstBackground();
                    GUI.enabled = true;
                }
            }
        }

        private void DrawPreviewSwatchGrid(List<PaletteSwatch> swatches, float preferredWidth, float height)
        {
            if (swatches == null || swatches.Count == 0)
                return;

            float available = Mathf.Max(300f, position.width - 54f);
            int columns = Mathf.Max(1, Mathf.FloorToInt(available / preferredWidth));
            float width = Mathf.Max(62f, (available - ((columns - 1) * 4f)) / columns);

            for (int i = 0; i < swatches.Count; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int index = i + col;
                        if (index < swatches.Count)
                        {
                            PaletteSwatch swatch = swatches[index];
                            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.04f, 3, 2), GUILayout.Width(width)))
                            {
                                Rect rect = GUILayoutUtility.GetRect(width - 6f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
                                EditorGUI.DrawRect(rect, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                                EditorGUILayout.LabelField(ColourConversionUtility.ToHexRGB(swatch.color), UtilityWindowTheme.MutedMiniLabelStyle);
                            }
                        }
                        else
                        {
                            GUILayout.Space(width + 4f);
                        }
                    }
                }
            }
        }

        private Vector2 DrawRange(string label, Vector2 value, float min, float max)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(78f));
                value.x = EditorGUILayout.FloatField(value.x, GUILayout.Width(48f));
                EditorGUILayout.MinMaxSlider(ref value.x, ref value.y, min, max);
                value.y = EditorGUILayout.FloatField(value.y, GUILayout.Width(48f));
            }
            value.x = Mathf.Clamp(value.x, min, max);
            value.y = Mathf.Clamp(value.y, min, max);
            if (value.y < value.x)
            {
                float temp = value.x;
                value.x = value.y;
                value.y = temp;
            }
            return value;
        }

        private int CountLockedSwatches()
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return 0;

            int count = 0;
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                if (_activePalette.swatches[i] != null && _activePalette.swatches[i].locked)
                    count++;
            }
            return count;
        }

        private void AddSwatch()
        {
            ChangePalette("Add Palette Swatch", () =>
            {
                _activePalette.swatches.Add(new PaletteSwatch($"Swatch {_activePalette.swatches.Count + 1}", Color.white, PaletteSwatchRole.Custom, false, _activePalette.swatches.Count));
                _selectedSwatch = _activePalette.swatches.Count - 1;
            });
        }

        private void DuplicateSelectedSwatch()
        {
            if (!IsValidSelectedSwatch())
                return;

            ChangePalette("Duplicate Palette Swatch", () =>
            {
                PaletteSwatch clone = _activePalette.swatches[_selectedSwatch].Clone();
                clone.name += " Copy";
                clone.locked = false;
                _activePalette.swatches.Insert(_selectedSwatch + 1, clone);
                _selectedSwatch++;
                RecalculatePriorities();
            });
        }

        private void RemoveSelectedSwatch()
        {
            if (!IsValidSelectedSwatch())
                return;

            ChangePalette("Remove Palette Swatch", () =>
            {
                _activePalette.swatches.RemoveAt(_selectedSwatch);
                _selectedSwatch = Mathf.Clamp(_selectedSwatch, -1, _activePalette.swatches.Count - 1);
                RecalculatePriorities();
            });
        }

        private void SortSwatches(int mode)
        {
            ChangePalette("Sort Palette Swatches", () =>
            {
                _activePalette.swatches.Sort((a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;

                    switch (mode)
                    {
                        case 0:
                            int role = a.role.CompareTo(b.role);
                            return role != 0 ? role : a.priority.CompareTo(b.priority);
                        case 1:
                            Color.RGBToHSV(a.color, out float ah, out _, out _);
                            Color.RGBToHSV(b.color, out float bh, out _, out _);
                            return ah.CompareTo(bh);
                        case 2:
                            Color.RGBToHSV(a.color, out _, out _, out float av);
                            Color.RGBToHSV(b.color, out _, out _, out float bv);
                            return av.CompareTo(bv);
                        default:
                            return a.priority.CompareTo(b.priority);
                    }
                });

                RecalculatePriorities();
            });
        }

        private void NormalizeNames()
        {
            ChangePalette("Normalize Palette Names", () =>
            {
                for (int i = 0; i < _activePalette.swatches.Count; i++)
                {
                    PaletteSwatch swatch = _activePalette.swatches[i];
                    if (swatch == null)
                        continue;

                    if (swatch.role != PaletteSwatchRole.None && swatch.role != PaletteSwatchRole.Custom)
                        swatch.name = Nicify(swatch.role.ToString());
                    else if (string.IsNullOrWhiteSpace(swatch.name))
                        swatch.name = $"Swatch {i + 1}";
                }
            });
        }

        private void RegeneratePaletteUnlocked()
        {
            if (_activePalette == null)
                return;

            if (_activePalette.swatches == null || _activePalette.swatches.Count == 0)
            {
                PaletteGenerationSettings starter = CurrentGenerationSettingsForPalette(false);
                starter.targetSwatchCount = Mathf.Max(1, _workingSettings.targetSwatchCount);
                List<PaletteSwatch> generated = GenerateWindowPalette(starter, null);
                ChangePalette("Generate Starter Palette", () =>
                {
                    _activePalette.swatches.Clear();
                    for (int i = 0; i < generated.Count; i++)
                        _activePalette.swatches.Add(generated[i].Clone());
                    _selectedSwatch = _activePalette.swatches.Count > 0 ? 0 : -1;
                    RecalculatePriorities();
                });
                _lastStatus = "Generated starter palette.";
                return;
            }

            PaletteGenerationSettings settings = CurrentGenerationSettingsForPalette(false);
            settings.targetSwatchCount = _activePalette.swatches.Count;
            List<PaletteSwatch> generatedSwatches = GenerateWindowPalette(settings, _activePalette.swatches);

            ChangePalette("Regenerate Palette", () =>
            {
                for (int i = 0; i < _activePalette.swatches.Count; i++)
                {
                    PaletteSwatch existing = _activePalette.swatches[i];
                    if (existing == null || existing.locked)
                        continue;

                    PaletteSwatch generated = i < generatedSwatches.Count ? generatedSwatches[i] : null;
                    if (generated == null)
                        continue;

                    existing.color = generated.color;
                    if (existing.role == PaletteSwatchRole.None || existing.role == PaletteSwatchRole.Custom)
                        existing.role = generated.role;
                    if (string.IsNullOrWhiteSpace(existing.name) || existing.name.StartsWith("Swatch"))
                        existing.name = generated.name;
                }
                RecalculatePriorities();
            });

            _lastStatus = "Regenerated palette.";
        }

        private void RegenerateSwatch(int index)
        {
            if (_activePalette == null || _activePalette.swatches == null || index < 0 || index >= _activePalette.swatches.Count)
                return;

            PaletteSwatch existing = _activePalette.swatches[index];
            if (existing == null)
                return;

            if (existing.locked)
            {
                _lastStatus = "Cannot regenerate a locked swatch.";
                return;
            }

            PaletteGenerationSettings settings = CurrentGenerationSettingsForPalette(false);
            settings.targetSwatchCount = Mathf.Max(_activePalette.swatches.Count, index + 1);
            List<PaletteSwatch> generatedSwatches = GenerateWindowPalette(settings, _activePalette.swatches);
            PaletteSwatch generated = index < generatedSwatches.Count ? generatedSwatches[index] : null;
            if (generated == null)
                return;

            ChangePalette("Regenerate Swatch", () =>
            {
                existing.color = generated.color;
                if (existing.role == PaletteSwatchRole.None || existing.role == PaletteSwatchRole.Custom)
                    existing.role = generated.role;
                if (string.IsNullOrWhiteSpace(existing.name) || existing.name.StartsWith("Swatch"))
                    existing.name = generated.name;
            });

            _selectedSwatch = index;
            _lastStatus = $"Regenerated {existing.name}.";
        }

        private PaletteGenerationSettings CurrentGenerationSettingsForPalette(bool preserveLocked)
        {
            PaletteGenerationSettings settings = _workingSettings != null ? _workingSettings.Clone() : new PaletteGenerationSettings();
            settings.targetSwatchCount = _activePalette != null && _activePalette.swatches != null && _activePalette.swatches.Count > 0
                ? _activePalette.swatches.Count
                : Mathf.Max(1, settings.targetSwatchCount);

            if (preserveLocked)
                settings.preserveLockedSwatches = true;

            settings.Clamp();
            return settings;
        }

        private static List<PaletteSwatch> GenerateWindowPalette(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches)
        {
            settings = settings != null ? settings.Clone() : new PaletteGenerationSettings();
            settings.Clamp();

            int count = Mathf.Clamp(settings.targetSwatchCount, 1, 64);
            var result = new List<PaletteSwatch>(count);
            System.Random rng = settings.useSeed
                ? new System.Random(settings.seed + ((int)settings.harmonyMode * 1009) + count * 37)
                : new System.Random(unchecked(System.Environment.TickCount * 31 + System.Guid.NewGuid().GetHashCode()));

            float baseHue = ResolveGenerationAnchorHue(settings, currentSwatches, rng);
            List<float> familyHues = BuildHarmonyHueFamily(settings.harmonyMode, baseHue, rng, count);

            for (int i = 0; i < count; i++)
            {
                PaletteSwatch existing = currentSwatches != null && i < currentSwatches.Count ? currentSwatches[i] : null;
                if (settings.preserveLockedSwatches && existing != null && existing.locked)
                {
                    result.Add(existing.Clone());
                    continue;
                }

                PaletteSwatchRole role = ResolveGenerationRole(existing, i);
                Color color = GenerateHarmonyRoleColor(role, settings, familyHues, rng, i, count, result);

                if (settings.avoidNearDuplicates && !IsNeutralGenerationRole(role))
                {
                    int guard = 0;
                    while (ContainsSimilarGeneratedColor(result, color) && guard < 10)
                    {
                        color = GenerateHarmonyRoleColor(role, settings, familyHues, rng, i + guard + 1, count, result);
                        guard++;
                    }
                }

                string name = existing != null && !string.IsNullOrWhiteSpace(existing.name)
                    ? existing.name
                    : GetDefaultGeneratedName(role, i + 1);

                PaletteSwatchRole outputRole = existing != null && existing.role != PaletteSwatchRole.None
                    ? existing.role
                    : role;

                result.Add(new PaletteSwatch(name, color, outputRole, false, existing != null ? existing.priority : i)
                {
                    notes = existing != null ? existing.notes : string.Empty,
                    tags = existing != null && existing.tags != null ? new List<string>(existing.tags) : new List<string>()
                });
            }

            if (settings.enforceTextContrast)
                EnforceGeneratedTextContrast(result);

            return result;
        }

        private static PaletteSwatchRole ResolveGenerationRole(PaletteSwatch existing, int index)
        {
            if (existing != null && existing.role != PaletteSwatchRole.None && existing.role != PaletteSwatchRole.Custom)
                return existing.role;

            if (GenerationRoleOrder.Length == 0)
                return PaletteSwatchRole.Accent;

            return GenerationRoleOrder[Mathf.Abs(index) % GenerationRoleOrder.Length];
        }

        private static string GetDefaultGeneratedName(PaletteSwatchRole role, int index)
        {
            if (role == PaletteSwatchRole.None || role == PaletteSwatchRole.Custom)
                return $"Swatch {index}";

            return Nicify(role.ToString());
        }

        private static float ResolveGenerationAnchorHue(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, System.Random rng)
        {
            PaletteSwatch anchor = FindGenerationAnchor(currentSwatches, true) ?? FindGenerationAnchor(currentSwatches, false);
            float rangeHue = RandomHueInRange(settings.hueRange, rng);

            if (anchor == null)
                return rangeHue;

            Color.RGBToHSV(anchor.color, out float anchorHue, out float anchorSaturation, out float anchorValue);
            float anchorStrength = Mathf.Clamp01(anchorSaturation * 1.15f + Mathf.Abs(anchorValue - 0.5f) * 0.15f);
            float rangePull = settings.randomVariation > 0.01f ? Mathf.Lerp(0.18f, 0.55f, settings.randomVariation) : 0.08f;
            float resolved = ColourHarmonyUtility.LerpHue(anchorHue, rangeHue, rangePull * (1f - anchorStrength * 0.45f));

            return FitHueToRange(resolved, settings.hueRange, settings.harmonyInfluence);
        }

        private static PaletteSwatch FindGenerationAnchor(IList<PaletteSwatch> swatches, bool requireColourful)
        {
            if (swatches == null)
                return null;

            PaletteSwatch best = null;
            float bestScore = -1f;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                Color.RGBToHSV(swatch.color, out _, out float saturation, out float value);
                if (requireColourful && saturation < 0.22f)
                    continue;

                float score = saturation * 2.2f + value * 0.25f;
                if (swatch.locked)
                    score += 0.55f;
                if (IsPrimaryHueRole(swatch.role))
                    score += 0.8f;
                if (IsNeutralGenerationRole(swatch.role))
                    score -= 0.65f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = swatch;
                }
            }

            return best;
        }

        private static List<float> BuildHarmonyHueFamily(ColourHarmonyMode mode, float baseHue, System.Random rng, int count)
        {
            var hues = new List<float>();

            switch (mode)
            {
                case ColourHarmonyMode.Monochromatic:
                    hues.Add(baseHue);
                    break;
                case ColourHarmonyMode.Analogous:
                    AddHueOffsets(hues, baseHue, 0f, -1f / 12f, 1f / 12f, -1f / 6f, 1f / 6f);
                    break;
                case ColourHarmonyMode.Complementary:
                    AddHueOffsets(hues, baseHue, 0f, 0.5f, -1f / 24f, 0.5f + 1f / 24f);
                    break;
                case ColourHarmonyMode.SplitComplementary:
                    AddHueOffsets(hues, baseHue, 0f, 5f / 12f, 7f / 12f, -1f / 12f, 1f / 12f);
                    break;
                case ColourHarmonyMode.Triadic:
                    AddHueOffsets(hues, baseHue, 0f, 1f / 3f, 2f / 3f, 1f / 3f + 1f / 24f, 2f / 3f - 1f / 24f);
                    break;
                case ColourHarmonyMode.Tetradic:
                    AddHueOffsets(hues, baseHue, 0f, 1f / 6f, 0.5f, 2f / 3f);
                    break;
                case ColourHarmonyMode.Square:
                    AddHueOffsets(hues, baseHue, 0f, 0.25f, 0.5f, 0.75f);
                    break;
                case ColourHarmonyMode.RandomBalanced:
                    float h = baseHue;
                    for (int i = 0; i < Mathf.Max(4, count); i++)
                    {
                        h = Mathf.Repeat(h + 0.61803398875f + RandomSigned(rng) * 0.055f, 1f);
                        hues.Add(h);
                    }
                    break;
                case ColourHarmonyMode.Contextual:
                default:
                    AddHueOffsets(hues, baseHue, 0f, -1f / 12f, 1f / 12f, 0.5f, 1f / 3f, 2f / 3f);
                    break;
            }

            if (hues.Count == 0)
                hues.Add(baseHue);

            return hues;
        }

        private static void AddHueOffsets(List<float> hues, float baseHue, params float[] offsets)
        {
            for (int i = 0; i < offsets.Length; i++)
                hues.Add(Mathf.Repeat(baseHue + offsets[i], 1f));
        }

        private static Color GenerateHarmonyRoleColor(PaletteSwatchRole role, PaletteGenerationSettings settings, List<float> familyHues, System.Random rng, int index, int count, IList<PaletteSwatch> existing)
        {
            int slot = ResolveHarmonySlot(role, index);
            float harmonyHue = familyHues[Mathf.Abs(slot) % familyHues.Count];
            float freeHue = RandomHueInRange(settings.hueRange, rng);
            float hue = settings.harmonyMode == ColourHarmonyMode.RandomBalanced
                ? harmonyHue
                : ColourHarmonyUtility.LerpHue(freeHue, harmonyHue, Mathf.Clamp01(0.45f + settings.harmonyInfluence * 0.55f));

            hue = FitHueToRange(hue, settings.hueRange, settings.harmonyInfluence);

            float s = Mathf.Lerp(Range(settings.saturationRange, rng), 0.72f, 0.32f + settings.harmonyInfluence * 0.24f);
            float v = Mathf.Lerp(Range(settings.valueRange, rng), 0.78f, 0.22f + settings.contrastInfluence * 0.18f);
            float jitter = Mathf.Clamp01(settings.randomVariation);

            hue = Mathf.Repeat(hue + RandomSigned(rng) * jitter * GetHueJitterAmount(settings.harmonyMode, role), 1f);
            s = Mathf.Clamp01(s + RandomSigned(rng) * jitter * 0.16f);
            v = Mathf.Clamp01(v + RandomSigned(rng) * jitter * 0.18f);

            switch (role)
            {
                case PaletteSwatchRole.Background:
                    hue = familyHues[0];
                    s = Mathf.Clamp(s * 0.28f, 0.035f, settings.harmonyMode == ColourHarmonyMode.Monochromatic ? 0.32f : 0.24f);
                    v = Mathf.Lerp(0.08f, 0.23f, 1f - settings.contrastInfluence);
                    break;
                case PaletteSwatchRole.Panel:
                    hue = familyHues[Mathf.Min(1, familyHues.Count - 1)];
                    s = Mathf.Clamp(s * 0.34f, 0.04f, 0.30f);
                    v = Mathf.Lerp(0.16f, 0.36f, 1f - settings.contrastInfluence);
                    break;
                case PaletteSwatchRole.Text:
                    hue = familyHues[0];
                    s = Mathf.Clamp(s * 0.10f, 0f, 0.16f);
                    v = 0.94f;
                    break;
                case PaletteSwatchRole.MutedText:
                    hue = familyHues[0];
                    s = Mathf.Clamp(s * 0.15f, 0.02f, 0.22f);
                    v = Mathf.Lerp(0.60f, 0.76f, settings.contrastInfluence);
                    break;
                case PaletteSwatchRole.Accent:
                    s = Mathf.Clamp(s * 1.18f, 0.48f, 1f);
                    v = Mathf.Clamp(v * 1.05f, 0.52f, 0.98f);
                    break;
                case PaletteSwatchRole.AccentSecondary:
                    s = Mathf.Clamp(s * 1.12f, 0.42f, 1f);
                    v = Mathf.Clamp(v * 0.98f, 0.45f, 0.94f);
                    break;
                case PaletteSwatchRole.Highlight:
                    s = Mathf.Clamp(s * 1.05f, 0.36f, 0.95f);
                    v = Mathf.Clamp(v * 1.18f, 0.64f, 1f);
                    break;
                case PaletteSwatchRole.Warning:
                    hue = ColourHarmonyUtility.LerpHue(0.105f, hue, settings.harmonyInfluence * 0.35f);
                    s = Mathf.Clamp(s * 1.1f, 0.55f, 1f);
                    v = Mathf.Clamp(v * 1.08f, 0.58f, 1f);
                    break;
                case PaletteSwatchRole.Success:
                    hue = ColourHarmonyUtility.LerpHue(0.34f, hue, settings.harmonyInfluence * 0.38f);
                    s = Mathf.Clamp(s, 0.42f, 0.95f);
                    v = Mathf.Clamp(v, 0.42f, 0.92f);
                    break;
                case PaletteSwatchRole.Error:
                    hue = ColourHarmonyUtility.LerpHue(0f, hue, settings.harmonyInfluence * 0.34f);
                    s = Mathf.Clamp(s * 1.05f, 0.54f, 1f);
                    v = Mathf.Clamp(v, 0.42f, 0.95f);
                    break;
                case PaletteSwatchRole.Outline:
                    hue = familyHues[0];
                    s = Mathf.Clamp(s * 0.26f, 0.02f, 0.28f);
                    v = Mathf.Lerp(0.34f, 0.54f, 1f - settings.contrastInfluence * 0.35f);
                    break;
                case PaletteSwatchRole.Shadow:
                    hue = familyHues[0];
                    s = Mathf.Clamp(s * 0.22f, 0.01f, 0.22f);
                    v = 0.035f;
                    break;
                case PaletteSwatchRole.None:
                case PaletteSwatchRole.Custom:
                    s = Mathf.Clamp(s, 0.32f, 0.96f);
                    v = Mathf.Clamp(v, 0.28f, 0.96f);
                    break;
            }

            if (settings.harmonyMode == ColourHarmonyMode.Monochromatic && !IsNeutralGenerationRole(role))
            {
                float t = count <= 1 ? 0.5f : Mathf.Repeat(index * 0.37f, 1f);
                s = Mathf.Clamp01(Mathf.Lerp(0.36f, 0.92f, t) + RandomSigned(rng) * jitter * 0.08f);
                v = Mathf.Clamp01(Mathf.Lerp(0.42f, 0.98f, 1f - t * 0.65f) + RandomSigned(rng) * jitter * 0.08f);
            }

            Color color = Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(s), Mathf.Clamp01(v));
            color.a = 1f;
            return color;
        }

        private static int ResolveHarmonySlot(PaletteSwatchRole role, int index)
        {
            switch (role)
            {
                case PaletteSwatchRole.Background:
                case PaletteSwatchRole.Panel:
                case PaletteSwatchRole.Text:
                case PaletteSwatchRole.MutedText:
                case PaletteSwatchRole.Outline:
                case PaletteSwatchRole.Shadow:
                    return 0;
                case PaletteSwatchRole.Accent:
                    return 0;
                case PaletteSwatchRole.AccentSecondary:
                    return 1;
                case PaletteSwatchRole.Highlight:
                    return 2;
                case PaletteSwatchRole.Warning:
                    return 3;
                case PaletteSwatchRole.Success:
                    return 4;
                case PaletteSwatchRole.Error:
                    return 5;
                default:
                    return Mathf.Max(0, index);
            }
        }

        private static float GetHueJitterAmount(ColourHarmonyMode mode, PaletteSwatchRole role)
        {
            if (IsNeutralGenerationRole(role))
                return 0.018f;

            switch (mode)
            {
                case ColourHarmonyMode.Monochromatic:
                    return 0.012f;
                case ColourHarmonyMode.Analogous:
                    return 0.025f;
                case ColourHarmonyMode.Complementary:
                case ColourHarmonyMode.SplitComplementary:
                case ColourHarmonyMode.Triadic:
                case ColourHarmonyMode.Tetradic:
                case ColourHarmonyMode.Square:
                    return 0.018f;
                case ColourHarmonyMode.RandomBalanced:
                    return 0.045f;
                default:
                    return 0.032f;
            }
        }

        private static bool IsPrimaryHueRole(PaletteSwatchRole role)
        {
            return role == PaletteSwatchRole.Accent ||
                   role == PaletteSwatchRole.AccentSecondary ||
                   role == PaletteSwatchRole.Highlight ||
                   role == PaletteSwatchRole.Custom;
        }

        private static bool IsNeutralGenerationRole(PaletteSwatchRole role)
        {
            return role == PaletteSwatchRole.Background ||
                   role == PaletteSwatchRole.Panel ||
                   role == PaletteSwatchRole.Text ||
                   role == PaletteSwatchRole.MutedText ||
                   role == PaletteSwatchRole.Outline ||
                   role == PaletteSwatchRole.Shadow;
        }

        private static float RandomHueInRange(Vector2 range, System.Random rng)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
            if (Mathf.Approximately(min, max))
                return Mathf.Repeat(min, 1f);

            return Mathf.Repeat(Mathf.Lerp(min, max, (float)rng.NextDouble()), 1f);
        }

        private static float FitHueToRange(float hue, Vector2 range, float harmonyInfluence)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
            if (max - min > 0.985f || HueInRange(hue, min, max))
                return Mathf.Repeat(hue, 1f);

            float nearest = Mathf.Abs(Mathf.DeltaAngle(hue * 360f, min * 360f)) < Mathf.Abs(Mathf.DeltaAngle(hue * 360f, max * 360f)) ? min : max;
            float pull = Mathf.Clamp01(0.35f + (1f - harmonyInfluence) * 0.45f);
            return ColourHarmonyUtility.LerpHue(hue, nearest, pull);
        }

        private static bool HueInRange(float hue, float min, float max)
        {
            hue = Mathf.Repeat(hue, 1f);
            return hue >= min && hue <= max;
        }

        private static float Range(Vector2 range, System.Random rng)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        private static float RandomSigned(System.Random rng)
        {
            return ((float)rng.NextDouble() * 2f) - 1f;
        }

        private static bool ContainsSimilarGeneratedColor(IList<PaletteSwatch> swatches, Color color)
        {
            if (swatches == null)
                return false;

            Color.RGBToHSV(color, out float h, out float s, out float v);
            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch other = swatches[i];
                if (other == null || IsNeutralGenerationRole(other.role))
                    continue;

                Color.RGBToHSV(other.color, out float oh, out float os, out float ov);
                float hueDistance = Mathf.Abs(Mathf.DeltaAngle(h * 360f, oh * 360f)) / 360f;
                if (hueDistance < 0.035f && Mathf.Abs(s - os) < 0.12f && Mathf.Abs(v - ov) < 0.14f)
                    return true;
            }

            return false;
        }

        private static void EnforceGeneratedTextContrast(IList<PaletteSwatch> swatches)
        {
            PaletteSwatch background = FindGeneratedRole(swatches, PaletteSwatchRole.Background) ?? FindGeneratedRole(swatches, PaletteSwatchRole.Panel);
            PaletteSwatch panel = FindGeneratedRole(swatches, PaletteSwatchRole.Panel) ?? background;
            if (background == null)
                return;

            ImproveGeneratedRoleAgainst(PaletteSwatchRole.Text, background.color, swatches, 4.5f);
            ImproveGeneratedRoleAgainst(PaletteSwatchRole.MutedText, background.color, swatches, 3f);
            if (panel != null)
            {
                ImproveGeneratedRoleAgainst(PaletteSwatchRole.Text, panel.color, swatches, 4.5f);
                ImproveGeneratedRoleAgainst(PaletteSwatchRole.MutedText, panel.color, swatches, 3f);
            }
        }

        private static void ImproveGeneratedRoleAgainst(PaletteSwatchRole role, Color background, IList<PaletteSwatch> swatches, float target)
        {
            PaletteSwatch swatch = FindGeneratedRole(swatches, role);
            if (swatch == null || swatch.locked)
                return;

            if (ColourContrastUtility.GetContrastRatio(swatch.color, background) < target)
                swatch.color = ColourContrastUtility.ImproveContrast(swatch.color, background, target);
        }

        private static PaletteSwatch FindGeneratedRole(IList<PaletteSwatch> swatches, PaletteSwatchRole role)
        {
            if (swatches == null)
                return null;

            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null && swatches[i].role == role)
                    return swatches[i];
            }

            return null;
        }

        private void SaveWorkingSettingsAsDefaults(bool status)
        {
            if (_activePalette == null)
                return;

            Undo.RecordObject(_activePalette, "Save Palette Generation Defaults");
            _workingSettings.Clamp();
            _activePalette.defaultGenerationSettings = _workingSettings.Clone();
            EditorUtility.SetDirty(_activePalette);
            if (status)
                _lastStatus = "Saved generation defaults.";
        }

        private void CommitPreviewToUnlocked()
        {
            if (_generatedPreview.Count == 0 || _activePalette == null || _activePalette.swatches == null)
                return;

            ChangePalette("Replace Unlocked From Preview", () =>
            {
                for (int i = 0; i < _activePalette.swatches.Count; i++)
                {
                    PaletteSwatch existing = _activePalette.swatches[i];
                    if (existing == null || existing.locked || i >= _generatedPreview.Count)
                        continue;

                    existing.color = _generatedPreview[i].color;
                    if (existing.role == PaletteSwatchRole.None || existing.role == PaletteSwatchRole.Custom)
                        existing.role = _generatedPreview[i].role;
                }
            });
            _lastStatus = "Applied preview to unlocked swatches.";
        }

        private void CommitPreviewAppend()
        {
            if (_generatedPreview.Count == 0 || _activePalette == null)
                return;

            ChangePalette("Append Generated Preview", () =>
            {
                for (int i = 0; i < _generatedPreview.Count; i++)
                    _activePalette.swatches.Add(_generatedPreview[i].Clone());
                RecalculatePriorities();
            });
            _lastStatus = "Appended preview swatches.";
        }

        private void ShowCopyMenu(PaletteSwatch swatch)
        {
            if (swatch == null)
                return;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Hex RGB"), false, () => CopyText(ColourConversionUtility.ToHexRGB(swatch.color), "Copied Hex RGB."));
            menu.AddItem(new GUIContent("Copy Hex RGBA"), false, () => CopyText(ColourConversionUtility.ToHexRGBA(swatch.color), "Copied Hex RGBA."));
            menu.AddItem(new GUIContent("Copy RGB"), false, () => CopyText(ColourConversionUtility.FormatRGB(swatch.color), "Copied RGB."));
            menu.AddItem(new GUIContent("Copy HSV"), false, () => CopyText(ColourConversionUtility.FormatHSV(swatch.color), "Copied HSV."));
            menu.ShowAsContext();
        }

        private void ShowSwatchMenu(int index)
        {
            if (_activePalette == null || _activePalette.swatches == null || index < 0 || index >= _activePalette.swatches.Count)
                return;

            PaletteSwatch swatch = _activePalette.swatches[index];
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Select"), index == _selectedSwatch, () => { _selectedSwatch = index; Repaint(); });
            menu.AddItem(new GUIContent(swatch.locked ? "Unlock" : "Lock"), false, () => ChangePalette("Toggle Swatch Lock", () => swatch.locked = !swatch.locked));
            if (swatch.locked)
                menu.AddDisabledItem(new GUIContent("Regenerate"));
            else
                menu.AddItem(new GUIContent("Regenerate"), false, () => RegenerateSwatch(index));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Duplicate"), false, () => { _selectedSwatch = index; DuplicateSelectedSwatch(); });
            menu.AddItem(new GUIContent("Remove"), false, () => { _selectedSwatch = index; RemoveSelectedSwatch(); });
            menu.ShowAsContext();
        }

        private void CopyText(string value, string status)
        {
            EditorGUIUtility.systemCopyBuffer = value;
            _lastStatus = status;
            Repaint();
        }

        private void RecalculatePriorities()
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return;

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                if (_activePalette.swatches[i] != null)
                    _activePalette.swatches[i].priority = i;
            }
        }

        private void ImproveTextRoles()
        {
            PaletteSwatch background = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Background) ?? PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Panel);
            if (background == null)
            {
                _lastStatus = "Add a Background or Panel role before improving text.";
                return;
            }

            ChangePalette("Improve Text Contrast", () =>
            {
                ImproveRole(PaletteSwatchRole.Text, background.color, 4.5f, "Text");
                ImproveRole(PaletteSwatchRole.MutedText, background.color, 3f, "Muted Text");
            });
        }

        private void AddOrRepairPanelRole()
        {
            ChangePalette("Add Neutral Panel", () =>
            {
                PaletteSwatch background = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Background);
                Color panel = background != null ? Color.Lerp(background.color, ColourContrastUtility.GetReadableTextColor(background.color), 0.12f) : new Color(0.14f, 0.16f, 0.22f);
                PaletteSwatch existing = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Panel);
                if (existing != null && !existing.locked)
                {
                    existing.color = panel;
                    existing.name = "Panel";
                }
                else if (existing == null)
                {
                    _activePalette.swatches.Add(new PaletteSwatch("Panel", panel, PaletteSwatchRole.Panel, false, _activePalette.swatches.Count));
                }
            });
        }

        private void AddOrRepairTextRole()
        {
            ChangePalette("Add Readable Text", () =>
            {
                PaletteSwatch background = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Background) ?? PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Panel);
                Color text = background != null ? ColourContrastUtility.GetReadableTextColor(background.color) : Color.white;
                PaletteSwatch existing = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Text);
                if (existing != null && !existing.locked)
                {
                    existing.color = text;
                    existing.name = "Text";
                }
                else if (existing == null)
                {
                    _activePalette.swatches.Add(new PaletteSwatch("Text", text, PaletteSwatchRole.Text, false, _activePalette.swatches.Count));
                }
            });
        }

        private void RepairSelectedAgainstBackground()
        {
            if (!IsValidSelectedSwatch())
                return;

            PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
            if (selected.locked)
            {
                _lastStatus = "Selected swatch is locked.";
                return;
            }

            PaletteSwatch background = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Background) ?? PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Panel);
            if (background == null)
            {
                _lastStatus = "No Background or Panel role to repair against.";
                return;
            }

            ChangePalette("Repair Selected Swatch", () => selected.color = ColourContrastUtility.ImproveContrast(selected.color, background.color, 4.5f));
        }

        private void ImproveRole(PaletteSwatchRole role, Color background, float target, string fallbackName)
        {
            PaletteSwatch swatch = PaletteAnalysisUtility.FindRole(_activePalette.swatches, role);
            if (swatch == null)
            {
                Color color = ColourContrastUtility.GetReadableTextColor(background);
                _activePalette.swatches.Add(new PaletteSwatch(fallbackName, color, role, false, _activePalette.swatches.Count));
            }
            else if (!swatch.locked)
            {
                swatch.color = ColourContrastUtility.ImproveContrast(swatch.color, background, target);
            }
        }

        private void SetActivePalette(PungentColourPaletteSO palette)
        {
            _activePalette = palette;
            if (_activePalette != null)
            {
                _activePalette.EnsureMetadata();
                _workingSettings = _activePalette.defaultGenerationSettings != null ? _activePalette.defaultGenerationSettings.Clone() : new PaletteGenerationSettings();
                _lastStatus = "Loaded palette.";
            }
            else
            {
                _lastStatus = "Ready";
            }

            _generatedPreview.Clear();
            _selectedSwatch = -1;
            _analysisDirty = true;
            _lastPalette = _activePalette;
        }

        private void EnsurePaletteState()
        {
            if (_activePalette == null)
                return;

            if (_activePalette != _lastPalette)
                SetActivePalette(_activePalette);

            if (_activePalette.swatches == null)
                _activePalette.swatches = new List<PaletteSwatch>();

            if (_activePalette.defaultGenerationSettings == null)
                _activePalette.defaultGenerationSettings = new PaletteGenerationSettings();

            _selectedSwatch = Mathf.Clamp(_selectedSwatch, -1, _activePalette.swatches.Count - 1);
        }

        private void RefreshAnalysisIfNeeded()
        {
            if (!_analysisDirty && _analysisReport != null)
                return;

            _analysisReport = PaletteAnalysisUtility.Analyse(_activePalette);
            _analysisDirty = false;
        }

        private void ChangePalette(string undoName, System.Action change)
        {
            if (_activePalette == null || change == null)
                return;

            Undo.RecordObject(_activePalette, undoName);
            change();
            _activePalette.EnsureMetadata();
            EditorUtility.SetDirty(_activePalette);
            _analysisDirty = true;
            Repaint();
        }

        private bool IsValidSelectedSwatch()
        {
            return _activePalette != null && _activePalette.swatches != null && _selectedSwatch >= 0 && _selectedSwatch < _activePalette.swatches.Count && _activePalette.swatches[_selectedSwatch] != null;
        }

        private static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Swatch";

            var chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                    chars.Add(' ');
                chars.Add(c);
            }
            return new string(chars.ToArray());
        }
    }
    #endif

}