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

    public partial class PaletteDesignerWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.PaletteDesigner.";
        private const float SwatchMinWidth = 188f;
        private const float SwatchPreferredWidth = 232f;
        private const float SwatchMaxWidth = 340f;
        private const float SwatchGap = 8f;
        private const float WorkbenchInlineInspectorMinWidth = 726f;
        private const float WorkbenchInlineInspectorMinHeight = 360f;

        private const string PrefPaletteBoardHeight = PrefPrefix + "PaletteBoardHeight";
        private const string PrefSelectedHeight = PrefPrefix + "SelectedHeight";
        private const string PrefAnalysisHeight = PrefPrefix + "AnalysisHeight";
        private const string PrefApplyHeight = PrefPrefix + "ApplyHeight";
        private const string PrefAutoContextEnabled = PrefPrefix + "AutoContextEnabled";
        private const string PrefSwatchLayoutMode = PrefPrefix + "SwatchLayoutMode";
        private const string PrefActiveWorkflowInspector = PrefPrefix + "ActiveWorkflowInspector";
        private const string PrefLastWorkflowInspector = PrefPrefix + "LastWorkflowInspector";
        private const string PrefWorkflowInspectorWidth = PrefPrefix + "WorkflowInspectorWidth";
        private const string PrefWorkflowPopupWidth = PrefPrefix + "WorkflowPopupWidth";
        private const string PrefWorkflowPopupHeight = PrefPrefix + "WorkflowPopupHeight";
        private const string PrefLastPaletteGuid = PrefPrefix + "LastPaletteGuid";
        private const string PrefGuideForegroundRole = PrefPrefix + "GuideForegroundRole";
        private const string PrefGuideBackgroundRole = PrefPrefix + "GuideBackgroundRole";
        private const string PrefGuideTargetContrast = PrefPrefix + "GuideTargetContrast";
        private const string PrefHybridOverridePrefix = PrefPrefix + "HybridOverride.";
        private const float WorkflowInspectorDefaultWidth = 340f;
        private const float WorkflowInspectorMinWidth = 360f;
        private const float WorkflowInspectorMaxWidth = 520f;
        private const float WorkflowInspectorHandleWidth = 6f;
        private const float WorkflowInspectorScrollbarAllowance = 20f;
        private const float PaletteCanvasMinWidth = 360f;
        private const float UtilityPopupMinWidth = 360f;
        private const float UtilityPopupMaxWidth = 620f;
        private const float WorkflowPopupWidth = 420f;
        private const float WorkflowPopupHeight = 560f;
        private const float WorkflowPopupMinWidth = 380f;
        private const float WorkflowPopupMinHeight = 220f;
        private const float OverlayHeaderHeight = 30f;

        private enum PaletteOverlayKind
        {
            None,
            Generate,
            Guide,
            Apply,
            History,
            Source,
            Settings,
            PalettePicker
        }

        private enum PaletteSwatchLayoutMode
        {
            AdaptiveGrid,
            HorizontalRow,
            VerticalStack
        }

        private enum PaletteApplyMappingMode
        {
            Auto,
            Role,
            Swatch
        }

        private sealed class PaletteApplyTargetMappingState
        {
            public string key;
            public bool included = true;
            public PaletteApplyMappingMode mode = PaletteApplyMappingMode.Auto;
            public PaletteSwatchRole role = PaletteSwatchRole.Accent;
            public int swatchIndex = -1;
        }

        private PungentColourPaletteSO _activePalette;
        private PungentColourPaletteSO _lastPalette;
        private PaletteGenerationSettings _workingSettings = new PaletteGenerationSettings();
        private readonly PaletteGenerationHistory _generationHistory = new PaletteGenerationHistory();
        private const int MaxAutoCoverageMemory = 24;
        private readonly List<PaletteSwatch> _recentAutoCoverageSwatches = new List<PaletteSwatch>();
        private readonly List<int> _recentAutoCoverageSwatchIndices = new List<int>();
        private PaletteGenerationSuggestion _autoContextSuggestion;
        private PaletteAnalysisReport _analysisReport;
        private Rect _lastOverlayRect;
        private Rect _lastCanvasRect;
        private Rect _utilityPopupAnchorRect;
        private Rect _activeUtilityPopupRect;
        private Rect _lastSwatchCanvasRect;
        private readonly Dictionary<int, Rect> _lastSwatchRects = new Dictionary<int, Rect>();
        private readonly HashSet<int> _selectedSwatches = new HashSet<int>();
        private int _selectionAnchor = -1;
        private int _dragSwatchIndex = -1;
        private int _dragInsertIndex = -1;
        private int _dragSwatchControl = 0;
        private Vector2 _dragSwatchStart;
        private bool _draggingSwatches;
        private bool _suppressWorkbenchInputThisEvent;
        private bool _analysisDirty = true;
        private bool _autoContextEnabled = true;
        private bool _autoContextDirty = true;
        private bool _workflowInspectorsInline;

        private Vector2 _mainScroll;
        private Vector2 _overlayScroll;
        private Vector2 _workflowInspectorScroll;
        private Vector2 _swatchStripScroll;
        private int _selectedSwatch = -1;
        private int _autoIterationSerial;
        private PaletteOverlayKind _activeOverlay = PaletteOverlayKind.None;
        private PaletteOverlayKind _activeWorkflowInspector = PaletteOverlayKind.None;
        private PaletteOverlayKind _lastWorkflowInspector = PaletteOverlayKind.Generate;
        private PaletteSwatchLayoutMode _swatchLayoutMode = PaletteSwatchLayoutMode.AdaptiveGrid;
        private PaletteSwatchLayoutMode _resolvedSwatchLayoutMode = PaletteSwatchLayoutMode.AdaptiveGrid;

        private bool _showPaletteSettings = true;
        private bool _showAdvancedGeneration = false;
        private bool _showApply = false;
        private bool _showProblemDetails = true;
        private bool _showContrastDetails = false;
        private bool _showGuideActionTools = false;
        private bool _showGuidePreviewTools = false;
        private bool _showGuideManualPair = false;
        private bool _showAutoContextDetails = false;
        private bool _showLockedAnchorFactors = false;
        private bool _showRecentColourFactors = false;
        private bool _showGenerationRuleSettings = false;

        private bool _overrideHarmonyMode = false;
        private bool _overrideTargetCount = false;
        private bool _overrideHueRange = false;
        private bool _overrideSaturationRange = false;
        private bool _overrideValueRange = false;
        private bool _overrideHarmonyInfluence = false;
        private bool _overrideVariation = false;
        private bool _overrideContrast = false;

        private bool _manualHarmonyModeEdited = false;
        private bool _manualTargetCountEdited = false;
        private bool _manualHueRangeEdited = false;
        private bool _manualSaturationRangeEdited = false;
        private bool _manualValueRangeEdited = false;
        private bool _manualHarmonyInfluenceEdited = false;
        private bool _manualVariationEdited = false;
        private bool _manualContrastEdited = false;

        private ColourHarmonyMode _manualHarmonyMode = ColourHarmonyMode.Contextual;
        private int _manualTargetCount = 8;
        private Vector2 _manualHueRange = new Vector2(0f, 1f);
        private Vector2 _manualSaturationRange = new Vector2(0.35f, 0.95f);
        private Vector2 _manualValueRange = new Vector2(0.18f, 0.95f);
        private float _manualHarmonyInfluence = 0.75f;
        private float _manualVariation = 0.18f;
        private float _manualContrast = 0.55f;

        private float _paletteBoardHeight = 430f;
        private float _selectedHeight = 118f;
        private float _analysisHeight = 180f;
        private float _applyHeight = 220f;
        private float _contentWidthOverride = -1f;
        private float _workflowInspectorWidth = WorkflowInspectorDefaultWidth;
        private float _workflowPopupWidth = WorkflowPopupWidth;
        private float _workflowPopupHeight = WorkflowPopupHeight;
        private Rect _workflowPopupOwnerScreenRect;
        private bool _workflowPopupOwnerScreenRectValid;

        private ColourDeficiencyPreviewMode _deficiencyPreview;

        private PaletteSwatchRole _applyRole = PaletteSwatchRole.Accent;
        private PaletteSwatchRole _guideForegroundRole = PaletteSwatchRole.Text;
        private PaletteSwatchRole _guideBackgroundRole = PaletteSwatchRole.Background;
        private int _guideForegroundIndex = -1;
        private int _guideBackgroundIndex = -1;
        private float _guideTargetContrast = 4.5f;
        private string _guideActiveContrastSource;
        private string _guideLastContrastSummary;
        private string _autoContextHoverReadout;
        private int _hoveredAutoContextSwatchIndex = -1;
        private bool _applyRenderers = true;
        private bool _applySelectedMaterials = true;
        private bool _applySpriteRenderers = true;
        private bool _applyUiGraphics = true;
        private bool _applyTmpText = true;
        private bool _modifySharedMaterials = false;
        private string _materialColorProperty = "_BaseColor";
        private readonly List<PaletteApplyTarget> _applyTargets = new List<PaletteApplyTarget>();
        private readonly List<PaletteApplyTargetMappingState> _applyMappings = new List<PaletteApplyTargetMappingState>();
        private readonly List<PungentPaletteStorageUtility.PaletteAssetInfo> _paletteAssetCache = new List<PungentPaletteStorageUtility.PaletteAssetInfo>();
        private PaletteApplyReport _lastApplyReport;
        private string _rememberedPaletteGuid;
        private bool _paletteAssetCacheDirty = true;
        private string _lastApplyScanSummary = "No scan yet";
        private string _lastStatus = "Ready";

        public static void Open()
        {
            PaletteDesignerWindow window = GetWindow<PaletteDesignerWindow>();
            window.titleContent = new GUIContent("Palette Designer");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        //// Legacy compatibility alias. Keep until the final public menu root is locked for release.
        //public static void OpenLegacyAlias()
        //{
        //    Open();
        //}

        private void OnEnable()
        {
            _showPaletteSettings = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowPaletteSettings", true);
            _showAdvancedGeneration = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowAdvancedGeneration", false);
            _showApply = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowApply", false);
            _showProblemDetails = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowProblemDetails", true);
            _showContrastDetails = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowContrastDetails", false);
            _deficiencyPreview = (ColourDeficiencyPreviewMode)UtilityWindowPrefs.GetInt(PrefPrefix + "DeficiencyPreview", 0);
            _materialColorProperty = UtilityWindowPrefs.GetString(PrefPrefix + "MaterialColorProperty", "_BaseColor");
            _applyRole = (PaletteSwatchRole)UtilityWindowPrefs.GetInt(PrefPrefix + "ApplyRole", (int)PaletteSwatchRole.Accent);
            _guideForegroundRole = (PaletteSwatchRole)UtilityWindowPrefs.GetInt(PrefGuideForegroundRole, (int)PaletteSwatchRole.Text);
            _guideBackgroundRole = (PaletteSwatchRole)UtilityWindowPrefs.GetInt(PrefGuideBackgroundRole, (int)PaletteSwatchRole.Background);
            _guideTargetContrast = UtilityWindowPrefs.GetFloat(PrefGuideTargetContrast, 4.5f);

            _paletteBoardHeight = UtilityWindowPrefs.GetFloat(PrefPaletteBoardHeight, _paletteBoardHeight);
            _selectedHeight = UtilityWindowPrefs.GetFloat(PrefSelectedHeight, _selectedHeight);
            _analysisHeight = UtilityWindowPrefs.GetFloat(PrefAnalysisHeight, _analysisHeight);
            _applyHeight = UtilityWindowPrefs.GetFloat(PrefApplyHeight, _applyHeight);
            _autoContextEnabled = UtilityWindowPrefs.GetBool(PrefAutoContextEnabled, true);
            if (!_autoContextEnabled)
                _autoContextEnabled = true;
            LoadHybridOverridePrefs();
            _swatchLayoutMode = (PaletteSwatchLayoutMode)UtilityWindowPrefs.GetInt(PrefSwatchLayoutMode, (int)PaletteSwatchLayoutMode.AdaptiveGrid);
            _activeWorkflowInspector = (PaletteOverlayKind)UtilityWindowPrefs.GetInt(PrefActiveWorkflowInspector, (int)PaletteOverlayKind.None);
            _lastWorkflowInspector = (PaletteOverlayKind)UtilityWindowPrefs.GetInt(PrefLastWorkflowInspector, (int)PaletteOverlayKind.Generate);
            _workflowInspectorWidth = UtilityWindowPrefs.GetFloat(PrefWorkflowInspectorWidth, WorkflowInspectorDefaultWidth);
            _workflowPopupWidth = UtilityWindowPrefs.GetFloat(PrefWorkflowPopupWidth, WorkflowPopupWidth);
            _workflowPopupHeight = UtilityWindowPrefs.GetFloat(PrefWorkflowPopupHeight, WorkflowPopupHeight);
            _rememberedPaletteGuid = UtilityWindowPrefs.GetString(PrefLastPaletteGuid, string.Empty);
            _paletteAssetCacheDirty = true;
            if (!IsWorkflowInspector(_activeWorkflowInspector))
                _activeWorkflowInspector = PaletteOverlayKind.Generate;
            if (!IsWorkflowInspector(_lastWorkflowInspector))
                _lastWorkflowInspector = PaletteOverlayKind.Generate;

            if (Selection.activeObject is PungentColourPaletteSO selected)
                SetActivePalette(selected);
            else if (TryLoadLastPaletteFromPrefs(out PungentColourPaletteSO restored))
                SetActivePalette(restored);
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowPaletteSettings", _showPaletteSettings);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowAdvancedGeneration", _showAdvancedGeneration);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowApply", _showApply);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowProblemDetails", _showProblemDetails);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowContrastDetails", _showContrastDetails);
            UtilityWindowPrefs.SetInt(PrefPrefix + "DeficiencyPreview", (int)_deficiencyPreview);
            UtilityWindowPrefs.SetString(PrefPrefix + "MaterialColorProperty", _materialColorProperty);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ApplyRole", (int)_applyRole);
            UtilityWindowPrefs.SetInt(PrefGuideForegroundRole, (int)_guideForegroundRole);
            UtilityWindowPrefs.SetInt(PrefGuideBackgroundRole, (int)_guideBackgroundRole);
            UtilityWindowPrefs.SetFloat(PrefGuideTargetContrast, _guideTargetContrast);
            UtilityWindowPrefs.SetFloat(PrefPaletteBoardHeight, _paletteBoardHeight);
            UtilityWindowPrefs.SetFloat(PrefSelectedHeight, _selectedHeight);
            UtilityWindowPrefs.SetFloat(PrefAnalysisHeight, _analysisHeight);
            UtilityWindowPrefs.SetFloat(PrefApplyHeight, _applyHeight);
            _autoContextEnabled = true;
            UtilityWindowPrefs.SetBool(PrefAutoContextEnabled, _autoContextEnabled);
            SaveHybridOverridePrefs();
            UtilityWindowPrefs.SetInt(PrefSwatchLayoutMode, (int)_swatchLayoutMode);
            UtilityWindowPrefs.SetInt(PrefActiveWorkflowInspector, (int)_activeWorkflowInspector);
            UtilityWindowPrefs.SetInt(PrefLastWorkflowInspector, (int)_lastWorkflowInspector);
            UtilityWindowPrefs.SetFloat(PrefWorkflowInspectorWidth, _workflowInspectorWidth);
            UtilityWindowPrefs.SetFloat(PrefWorkflowPopupWidth, _workflowPopupWidth);
            UtilityWindowPrefs.SetFloat(PrefWorkflowPopupHeight, _workflowPopupHeight);
        }

        private void LoadHybridOverridePrefs()
        {
            _overrideHarmonyMode = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "HarmonyMode.Override", false);
            _overrideTargetCount = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "TargetCount.Override", false);
            _overrideHueRange = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "HueRange.Override", false);
            _overrideSaturationRange = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "SaturationRange.Override", false);
            _overrideValueRange = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "ValueRange.Override", false);
            _overrideHarmonyInfluence = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "HarmonyInfluence.Override", false);
            _overrideVariation = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "Variation.Override", false);
            _overrideContrast = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "Contrast.Override", false);

            _manualHarmonyModeEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "HarmonyMode.Edited", false);
            _manualTargetCountEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "TargetCount.Edited", false);
            _manualHueRangeEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "HueRange.Edited", false);
            _manualSaturationRangeEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "SaturationRange.Edited", false);
            _manualValueRangeEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "ValueRange.Edited", false);
            _manualHarmonyInfluenceEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "HarmonyInfluence.Edited", false);
            _manualVariationEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "Variation.Edited", false);
            _manualContrastEdited = UtilityWindowPrefs.GetBool(PrefHybridOverridePrefix + "Contrast.Edited", false);

            _manualHarmonyMode = (ColourHarmonyMode)UtilityWindowPrefs.GetInt(PrefHybridOverridePrefix + "HarmonyMode.Value", (int)ColourHarmonyMode.Contextual);
            _manualTargetCount = UtilityWindowPrefs.GetInt(PrefHybridOverridePrefix + "TargetCount.Value", 8);
            _manualHueRange = LoadVector2Pref(PrefHybridOverridePrefix + "HueRange.Value", new Vector2(0f, 1f));
            _manualSaturationRange = LoadVector2Pref(PrefHybridOverridePrefix + "SaturationRange.Value", new Vector2(0.35f, 0.95f));
            _manualValueRange = LoadVector2Pref(PrefHybridOverridePrefix + "ValueRange.Value", new Vector2(0.18f, 0.95f));
            _manualHarmonyInfluence = UtilityWindowPrefs.GetFloat(PrefHybridOverridePrefix + "HarmonyInfluence.Value", 0.75f);
            _manualVariation = UtilityWindowPrefs.GetFloat(PrefHybridOverridePrefix + "Variation.Value", 0.18f);
            _manualContrast = UtilityWindowPrefs.GetFloat(PrefHybridOverridePrefix + "Contrast.Value", 0.55f);
        }

        private void SaveHybridOverridePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "HarmonyMode.Override", _overrideHarmonyMode);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "TargetCount.Override", _overrideTargetCount);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "HueRange.Override", _overrideHueRange);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "SaturationRange.Override", _overrideSaturationRange);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "ValueRange.Override", _overrideValueRange);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "HarmonyInfluence.Override", _overrideHarmonyInfluence);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "Variation.Override", _overrideVariation);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "Contrast.Override", _overrideContrast);

            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "HarmonyMode.Edited", _manualHarmonyModeEdited);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "TargetCount.Edited", _manualTargetCountEdited);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "HueRange.Edited", _manualHueRangeEdited);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "SaturationRange.Edited", _manualSaturationRangeEdited);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "ValueRange.Edited", _manualValueRangeEdited);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "HarmonyInfluence.Edited", _manualHarmonyInfluenceEdited);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "Variation.Edited", _manualVariationEdited);
            UtilityWindowPrefs.SetBool(PrefHybridOverridePrefix + "Contrast.Edited", _manualContrastEdited);

            UtilityWindowPrefs.SetInt(PrefHybridOverridePrefix + "HarmonyMode.Value", (int)_manualHarmonyMode);
            UtilityWindowPrefs.SetInt(PrefHybridOverridePrefix + "TargetCount.Value", _manualTargetCount);
            SaveVector2Pref(PrefHybridOverridePrefix + "HueRange.Value", _manualHueRange);
            SaveVector2Pref(PrefHybridOverridePrefix + "SaturationRange.Value", _manualSaturationRange);
            SaveVector2Pref(PrefHybridOverridePrefix + "ValueRange.Value", _manualValueRange);
            UtilityWindowPrefs.SetFloat(PrefHybridOverridePrefix + "HarmonyInfluence.Value", _manualHarmonyInfluence);
            UtilityWindowPrefs.SetFloat(PrefHybridOverridePrefix + "Variation.Value", _manualVariation);
            UtilityWindowPrefs.SetFloat(PrefHybridOverridePrefix + "Contrast.Value", _manualContrast);
        }

        private Vector2 LoadVector2Pref(string key, Vector2 fallback)
        {
            return new Vector2(
                UtilityWindowPrefs.GetFloat(key + ".x", fallback.x),
                UtilityWindowPrefs.GetFloat(key + ".y", fallback.y));
        }

        private void SaveVector2Pref(string key, Vector2 value)
        {
            UtilityWindowPrefs.SetFloat(key + ".x", value.x);
            UtilityWindowPrefs.SetFloat(key + ".y", value.y);
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
                : $"{_activePalette.Count} swatches / {CountLockedSwatches()} locked / {_lastStatus}";

            UtilityWindowTheme.UtilityToolbar(new UtilityWindowTheme.UtilityHeaderOptions
            {
                UtilityId = "palette-designer",
                Title = "Palette Designer",
                Description = "Palette-first colour generation, inline swatch editing, accessibility checks, and scene/material application.",
                Status = status,
                CompactStatus = _lastStatus,
                ShowHelp = true,
                ShowMinimizeTray = true,
                ShowMinimizeButton = true,
                Tint = UtilityWindowTheme.HeaderTint
            });

            _suppressWorkbenchInputThisEvent = false;
            DrawToolbar();
            HandleActiveUtilityPopupInput();

            if (_activePalette == null)
            {
                DrawEmptyState();
                DrawActiveUtilityPopup();
                return;
            }

            EnsurePaletteState();
            RefreshAnalysisIfNeeded();
            RefreshAutoContextIfNeeded();

            DrawPaletteWorkbenchRoot();
            DrawActiveUtilityPopup();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawPaletteWorkbenchRoot()
        {
            DrawPaletteWorkbenchShell();
        }

        private void DrawPaletteWorkbenchShell()
        {
            HandleWorkflowKeyboard();

            if (!IsWorkflowInspector(_activeWorkflowInspector))
                _activeWorkflowInspector = DefaultWorkflowInspectorKind();

            _workflowInspectorsInline = ShouldDrawInlineWorkflowInspectors();
            if (_workflowInspectorsInline)
            {
                _workflowPopupOwnerScreenRectValid = false;
                PaletteWorkflowInspectorPopupWindow.CloseForOwner(this);
            }
            else if (IsWorkflowInspector(_activeWorkflowInspector))
            {
                CaptureWorkflowPopupOwnerScreenRect();
                PaletteWorkflowInspectorPopupWindow.ShowFor(this, _activeWorkflowInspector, WorkflowPopupAnchorRect());
            }

            float inspectorWidth = _workflowInspectorsInline
                ? ClampWorkflowInspectorWidth(_workflowInspectorWidth)
                : 0f;
            _workflowInspectorWidth = inspectorWidth > 0f ? inspectorWidth : _workflowInspectorWidth;
            float handleWidth = _workflowInspectorsInline ? WorkflowInspectorHandleWidth : 0f;
            float canvasWidth = Mathf.Max(PaletteCanvasMinWidth, position.width - inspectorWidth - handleWidth);
            Rect canvasRect = default;

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                canvasRect = EditorGUILayout.BeginVertical(GUILayout.Width(canvasWidth), GUILayout.ExpandHeight(true));
                try
                {
                    _contentWidthOverride = canvasWidth;
                    DrawPaletteBoardPanel();
                }
                finally
                {
                    _contentWidthOverride = -1f;
                    EditorGUILayout.EndVertical();
                }

                if (_workflowInspectorsInline)
                {
                    DrawWorkflowInspectorResizeHandle();
                    DrawWorkflowInspectorColumn(inspectorWidth);
                }
            }

            _lastCanvasRect = canvasRect;
        }

        private float ClampWorkflowInspectorWidth(float width)
        {
            float maxFromCanvas = Mathf.Max(WorkflowInspectorMinWidth, position.width - WorkflowInspectorHandleWidth - PaletteCanvasMinWidth);
            float max = Mathf.Min(WorkflowInspectorMaxWidth, maxFromCanvas);
            float min = Mathf.Min(WorkflowInspectorMinWidth, max);
            return Mathf.Clamp(width, min, max);
        }

        private void DrawWorkflowInspectorResizeHandle()
        {
            Rect rect = GUILayoutUtility.GetRect(WorkflowInspectorHandleWidth, 10f, GUILayout.Width(WorkflowInspectorHandleWidth), GUILayout.ExpandHeight(true));
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;

            if (current.type == EventType.Repaint)
            {
                Color fill = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.19f, 0.92f) : new Color(0.62f, 0.63f, 0.66f, 0.92f);
                Color line = EditorGUIUtility.isProSkin ? new Color(0.36f, 0.38f, 0.42f, 0.72f) : new Color(0.38f, 0.40f, 0.44f, 0.52f);
                EditorGUI.DrawRect(rect, fill);
                EditorGUI.DrawRect(new Rect(rect.center.x - 0.5f, rect.y + 10f, 1f, Mathf.Max(1f, rect.height - 20f)), line);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button == 0 && rect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        _workflowInspectorWidth = ClampWorkflowInspectorWidth(_workflowInspectorWidth - current.delta.x);
                        SavePrefs();
                        Repaint();
                        current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }
        }

        private void HandleWorkflowKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
                return;

            if (_activeOverlay != PaletteOverlayKind.None && current.keyCode == KeyCode.Escape)
            {
                CloseUtilityPopup();
                GUI.FocusControl(null);
                Repaint();
                current.Use();
                return;
            }

            if (EditorGUIUtility.editingTextField)
                return;

            if ((current.control || current.command) && current.keyCode == KeyCode.A)
            {
                SelectAllSwatches();
                current.Use();
                return;
            }

            if (current.alt && (current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.UpArrow))
            {
                MoveSelectedSwatchesByKeyboard(-1);
                current.Use();
                return;
            }

            if (current.alt && (current.keyCode == KeyCode.RightArrow || current.keyCode == KeyCode.DownArrow))
            {
                MoveSelectedSwatchesByKeyboard(1);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Space &&
                _activePalette != null &&
                _activeOverlay == PaletteOverlayKind.None &&
                GUIUtility.hotControl == 0)
            {
                IteratePalette();
                current.Use();
            }
        }

        private void OpenOverlay(PaletteOverlayKind kind)
        {
            OpenOverlay(kind, Rect.zero);
        }

        private void OpenOverlay(PaletteOverlayKind kind, Rect anchorRect)
        {
            if (IsWorkflowInspector(kind))
            {
                OpenWorkflowInspector(kind, anchorRect);
                return;
            }

            OpenUtilityPopup(kind, anchorRect, false);
        }

        private void OpenUtilityPopup(PaletteOverlayKind kind, Rect anchorRect, bool forceOpen)
        {
            if (!IsUtilityPopup(kind))
                return;

            bool opening = forceOpen || _activeOverlay != kind;
            if (opening && kind == PaletteOverlayKind.PalettePicker)
                RefreshPaletteAssetCache();

            _activeOverlay = !forceOpen && _activeOverlay == kind ? PaletteOverlayKind.None : kind;
            _utilityPopupAnchorRect = _activeOverlay == PaletteOverlayKind.None ? Rect.zero : anchorRect;
            _overlayScroll = Vector2.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private void OpenWorkflowInspector(PaletteOverlayKind kind, Rect anchorRect)
        {
            if (!IsWorkflowInspector(kind))
                return;

            bool inline = ShouldDrawInlineWorkflowInspectors();
            _workflowInspectorsInline = inline;
            _activeOverlay = PaletteOverlayKind.None;
            _overlayScroll = Vector2.zero;
            bool switchingInspector = _activeWorkflowInspector != kind;
            if (switchingInspector)
                _workflowInspectorScroll = Vector2.zero;
            _lastWorkflowInspector = kind;

            if (inline)
            {
                PaletteWorkflowInspectorPopupWindow.CloseForOwner(this);
                if (switchingInspector)
                    _activeWorkflowInspector = kind;
            }
            else
            {
                CaptureWorkflowPopupOwnerScreenRect();
                if (_activeWorkflowInspector == kind && PaletteWorkflowInspectorPopupWindow.IsOpenFor(this, kind))
                {
                    PaletteWorkflowInspectorPopupWindow.FocusFor(this, kind);
                }
                else
                {
                    _activeWorkflowInspector = kind;
                    PaletteWorkflowInspectorPopupWindow.ShowFor(this, kind, anchorRect);
                }
            }

            SavePrefs();
            Repaint();
        }

        private void FocusWorkflowInspector(PaletteOverlayKind kind)
        {
            if (!IsWorkflowInspector(kind))
                return;

            bool inline = ShouldDrawInlineWorkflowInspectors();
            _workflowInspectorsInline = inline;
            _activeOverlay = PaletteOverlayKind.None;
            _activeWorkflowInspector = kind;
            _lastWorkflowInspector = kind;
            _workflowInspectorScroll = Vector2.zero;

            if (inline)
                PaletteWorkflowInspectorPopupWindow.CloseForOwner(this);
            else
            {
                CaptureWorkflowPopupOwnerScreenRect();
                PaletteWorkflowInspectorPopupWindow.ShowFor(this, kind, Rect.zero);
            }

            SavePrefs();
            Repaint();
        }

        private bool ShouldDrawInlineWorkflowInspectors()
        {
            float requiredWidth = PaletteCanvasMinWidth + WorkflowInspectorHandleWidth + WorkflowInspectorMinWidth;
            return position.width >= Mathf.Max(WorkbenchInlineInspectorMinWidth, requiredWidth) && position.height >= WorkbenchInlineInspectorMinHeight;
        }

        private Rect WorkflowPopupAnchorRect()
        {
            return new Rect(position.width, 0f, 1f, position.height);
        }

        private void CaptureWorkflowPopupOwnerScreenRect()
        {
            Rect rect = position;
            if (Event.current != null)
            {
                Vector2 topLeft = GUIUtility.GUIToScreenPoint(Vector2.zero);
                Rect captured = new Rect(topLeft.x, topLeft.y, position.width, position.height);
                if (IsUsableScreenRect(captured))
                    rect = captured;
            }

            _workflowPopupOwnerScreenRect = rect;
            _workflowPopupOwnerScreenRectValid = IsUsableScreenRect(rect);
        }

        private Rect WorkflowPopupOwnerScreenRect()
        {
            if (_workflowPopupOwnerScreenRectValid && IsUsableScreenRect(_workflowPopupOwnerScreenRect))
                return _workflowPopupOwnerScreenRect;

            Rect rect = position;
            return IsUsableScreenRect(rect)
                ? rect
                : new Rect(0f, 0f, Mathf.Max(1f, position.width), Mathf.Max(1f, position.height));
        }

        private static bool IsUsableScreenRect(Rect rect)
        {
            return rect.width > 1f &&
                   rect.height > 1f &&
                   !float.IsNaN(rect.x) &&
                   !float.IsNaN(rect.y) &&
                   !float.IsNaN(rect.width) &&
                   !float.IsNaN(rect.height) &&
                   !float.IsInfinity(rect.x) &&
                   !float.IsInfinity(rect.y) &&
                   !float.IsInfinity(rect.width) &&
                   !float.IsInfinity(rect.height);
        }

        private static bool IsWorkflowInspector(PaletteOverlayKind kind)
        {
            return kind == PaletteOverlayKind.Generate ||
                   kind == PaletteOverlayKind.Guide ||
                   kind == PaletteOverlayKind.Apply ||
                   kind == PaletteOverlayKind.History;
        }

        private PaletteOverlayKind DefaultWorkflowInspectorKind()
        {
            if (IsWorkflowInspector(_activeWorkflowInspector))
                return _activeWorkflowInspector;
            if (IsWorkflowInspector(_lastWorkflowInspector))
                return _lastWorkflowInspector;
            return PaletteOverlayKind.Generate;
        }

        private void SelectWorkflowInspector(PaletteOverlayKind kind, bool resetScroll)
        {
            if (!IsWorkflowInspector(kind) || _activeWorkflowInspector == kind)
                return;

            _activeWorkflowInspector = kind;
            _lastWorkflowInspector = kind;
            if (resetScroll)
                _workflowInspectorScroll = Vector2.zero;
            GUI.FocusControl(null);
            SavePrefs();
            Repaint();
        }

        private PaletteOverlayKind DrawWorkflowInspectorSelector(PaletteOverlayKind activeKind, float availableWidth)
        {
            PaletteOverlayKind selected = activeKind;
            float width = Mathf.Floor(Mathf.Max(78f, (availableWidth - 6f) / 4f));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawWorkflowInspectorSelectorButton(PaletteOverlayKind.Generate, "Controls", "Generation controls and next-pass ranges.", activeKind, width))
                    selected = PaletteOverlayKind.Generate;
                if (DrawWorkflowInspectorSelectorButton(PaletteOverlayKind.Guide, "Refine", "Accessibility, roles, contrast, and repairs.", activeKind, width))
                    selected = PaletteOverlayKind.Guide;
                if (DrawWorkflowInspectorSelectorButton(PaletteOverlayKind.Apply, "Apply", "Scan and apply palette colours explicitly.", activeKind, width))
                    selected = PaletteOverlayKind.Apply;
                if (DrawWorkflowInspectorSelectorButton(PaletteOverlayKind.History, "History", "Previous palette iterations for inspection and restore.", activeKind, width))
                    selected = PaletteOverlayKind.History;
            }

            return selected;
        }

        private bool DrawWorkflowInspectorSelectorButton(PaletteOverlayKind kind, string label, string tooltip, PaletteOverlayKind activeKind, float width)
        {
            bool active = activeKind == kind;
            GUIContent content = new GUIContent(label, $"{tooltip} {OverlayStatus(kind)}");
            if (!active)
                return GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(width));

            Rect rect = GUILayoutUtility.GetRect(width, 20f, EditorStyles.toolbarButton, GUILayout.Width(width), GUILayout.Height(20f));
            Color fill = OverlayTint(kind);
            Event current = Event.current;
            if (current.type == EventType.Repaint)
            {
                Color border = new Color(fill.r, fill.g, fill.b, rect.Contains(current.mousePosition) ? 1f : 0.86f);
                DrawStudioBox(rect, fill, border);
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(4, 4, 0, 1),
                    normal = { textColor = Color.white },
                    hover = { textColor = Color.white },
                    active = { textColor = Color.white },
                    focused = { textColor = Color.white },
                    onNormal = { textColor = Color.white },
                    onHover = { textColor = Color.white },
                    onActive = { textColor = Color.white },
                    onFocused = { textColor = Color.white }
                };
                GUI.Label(rect, content, labelStyle);
            }

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static bool IsUtilityPopup(PaletteOverlayKind kind)
        {
            return kind == PaletteOverlayKind.Source ||
                   kind == PaletteOverlayKind.Settings ||
                   kind == PaletteOverlayKind.PalettePicker;
        }

        private void HandleActiveUtilityPopupInput()
        {
            if (!IsUtilityPopup(_activeOverlay))
                return;

            CalculateUtilityPopupRect();
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CloseUtilityPopup();
                current.Use();
                return;
            }

            if (current.type != EventType.MouseDown)
                return;

            if (_activeUtilityPopupRect.Contains(current.mousePosition) ||
                _utilityPopupAnchorRect.Contains(current.mousePosition))
            {
                _suppressWorkbenchInputThisEvent = true;
                return;
            }

            CloseUtilityPopup();
            current.Use();
        }

        private void CloseUtilityPopup()
        {
            _activeOverlay = PaletteOverlayKind.None;
            _utilityPopupAnchorRect = Rect.zero;
            _activeUtilityPopupRect = Rect.zero;
            GUI.FocusControl(null);
            Repaint();
        }

        private Rect CalculateUtilityPopupRect()
        {
            if (!IsUtilityPopup(_activeOverlay))
            {
                _activeUtilityPopupRect = Rect.zero;
                return _activeUtilityPopupRect;
            }

            float width = Mathf.Clamp(position.width - 48f, UtilityPopupMinWidth, UtilityPopupMaxWidth);
            float height;
            switch (_activeOverlay)
            {
                case PaletteOverlayKind.Source:
                    height = Mathf.Min(430f, Mathf.Max(240f, position.height - 96f));
                    break;
                case PaletteOverlayKind.PalettePicker:
                    height = Mathf.Min(460f, Mathf.Max(220f, position.height - 96f));
                    break;
                default:
                    height = 230f;
                    break;
            }

            float x = _utilityPopupAnchorRect.width > 0f
                ? _utilityPopupAnchorRect.center.x - width * 0.5f
                : Mathf.Max(12f, position.width - width - 12f);
            x = Mathf.Clamp(x, 12f, Mathf.Max(12f, position.width - width - 12f));

            float y = _utilityPopupAnchorRect.height > 0f
                ? Mathf.Max(52f, _utilityPopupAnchorRect.yMax + 4f)
                : 52f;
            y = Mathf.Min(y, Mathf.Max(52f, position.height - height - 12f));

            _activeUtilityPopupRect = new Rect(x, y, width, height);
            return _activeUtilityPopupRect;
        }

        private void DrawActiveUtilityPopup()
        {
            if (!IsUtilityPopup(_activeOverlay))
                return;

            Rect popupRect = CalculateUtilityPopupRect();
            _lastOverlayRect = popupRect;
            DrawPopupChrome(popupRect);

            GUILayout.BeginArea(popupRect, EditorStyles.helpBox);
            DrawUtilityPopupContents(popupRect.width - 12f, popupRect.height - 12f);
            GUILayout.EndArea();
        }

        private static void DrawPopupChrome(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color shadow = new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.26f : 0.14f);
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 0.98f)
                : new Color(0.82f, 0.82f, 0.84f, 0.98f);
            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), shadow);
            EditorGUI.DrawRect(rect, fill);
        }

        private void DrawUtilityPopupContents(float contentWidth, float maxBodyHeight)
        {
            DrawUtilityPopupHeader();

            _overlayScroll = EditorGUILayout.BeginScrollView(_overlayScroll, false, true, GUILayout.MaxHeight(Mathf.Max(120f, maxBodyHeight - OverlayHeaderHeight - 10f)));
            float previousWidth = _contentWidthOverride;
            _contentWidthOverride = Mathf.Max(260f, contentWidth);
            switch (_activeOverlay)
            {
                case PaletteOverlayKind.Source:
                    DrawSourceTrayContent();
                    break;
                case PaletteOverlayKind.Settings:
                    DrawSettingsOverlayContent();
                    break;
                case PaletteOverlayKind.PalettePicker:
                    DrawPalettePickerContent();
                    break;
            }
            _contentWidthOverride = previousWidth;
            EditorGUILayout.EndScrollView();
        }

        private void DrawUtilityPopupHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                GUILayout.Label(new GUIContent(OverlayTitle(_activeOverlay), OverlayStatus(_activeOverlay)), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Close", "Close this popup. Escape also closes it."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                    CloseUtilityPopup();
            }
        }

        private void DrawGenerateInspectorContent()
        {
            using (BeginInspectorSection("Next Pass", ControlsTint(), null, PaletteDesignerSectionTone.Summary))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_activePalette == null))
                    {
                        if (StudioButton(PrimaryIterationLabel(), ControlsTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(30f), GUILayout.MinWidth(120f)))
                            IteratePalette();
                    }
                    GUILayout.FlexibleSpace();
                }

                DrawInspectorBodyText("Uses locked swatches as anchors, then applies any manual field overrides to guide the next generated pass.");
            }

            using (new EditorGUI.DisabledScope(_activePalette == null))
                DrawAdvancedGenerationPanel();
        }

        private void DrawWorkflowInspectorColumn(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
                {
                    PaletteOverlayKind selected = DrawWorkflowInspectorSelector(_activeWorkflowInspector, Mathf.Max(320f, width - 24f));
                    if (selected != _activeWorkflowInspector)
                        SelectWorkflowInspector(selected, true);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(new GUIContent("||", "Drag the left edge to resize this inspector."), EditorStyles.miniLabel, GUILayout.Width(14f));
                }

                if (!IsWorkflowInspector(_activeWorkflowInspector))
                    _activeWorkflowInspector = DefaultWorkflowInspectorKind();

                float contentWidth = InspectorBodyContentWidth(width);
                _workflowInspectorScroll = GUILayout.BeginScrollView(_workflowInspectorScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(contentWidth), GUILayout.ExpandWidth(false)))
                    DrawWorkflowInspectorBody(_activeWorkflowInspector, contentWidth);
                GUILayout.EndScrollView();
            }
        }

        private static float InspectorBodyContentWidth(float outerWidth)
        {
            return Mathf.Max(260f, Mathf.Floor(outerWidth - WorkflowInspectorScrollbarAllowance - 8f));
        }

        private void DrawWorkflowInspectorBody(PaletteOverlayKind kind, float contentWidth)
        {
            float previousWidth = _contentWidthOverride;
            _contentWidthOverride = Mathf.Max(260f, contentWidth);
            if (kind != PaletteOverlayKind.Generate)
            {
                _hoveredAutoContextSwatchIndex = -1;
                _autoContextHoverReadout = null;
            }

            switch (kind)
            {
                case PaletteOverlayKind.Generate:
                    DrawGenerateInspectorContent();
                    break;
                case PaletteOverlayKind.Guide:
                    DrawAnalysisPanel();
                    break;
                case PaletteOverlayKind.Apply:
                    DrawApplyPanel();
                    break;
                case PaletteOverlayKind.History:
                    DrawHistoryTrayContent();
                    break;
            }
            _contentWidthOverride = previousWidth;
        }

        private Color OverlayTint(PaletteOverlayKind kind)
        {
            switch (kind)
            {
                case PaletteOverlayKind.Generate:
                    return ControlsTint();
                case PaletteOverlayKind.History:
                    return HistoryTint();
                case PaletteOverlayKind.Guide:
                    return RefineTint();
                case PaletteOverlayKind.Apply:
                    return ApplyTint();
                case PaletteOverlayKind.Source:
                    return UtilityWindowTheme.Blue;
                case PaletteOverlayKind.Settings:
                    return UtilityWindowTheme.Neutral;
                case PaletteOverlayKind.PalettePicker:
                    return UtilityWindowTheme.Blue;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private string OverlayTitle(PaletteOverlayKind kind)
        {
            switch (kind)
            {
                case PaletteOverlayKind.Generate:
                    return "Controls";
                case PaletteOverlayKind.Guide:
                    return "Refine";
                case PaletteOverlayKind.Apply:
                    return "Apply";
                case PaletteOverlayKind.History:
                    return "History";
                case PaletteOverlayKind.Source:
                    return "Source";
                case PaletteOverlayKind.Settings:
                    return "View Settings";
                case PaletteOverlayKind.PalettePicker:
                    return "Palette";
                default:
                    return "Palette";
            }
        }

        private string OverlayStatus(PaletteOverlayKind kind)
        {
            switch (kind)
            {
                case PaletteOverlayKind.Generate:
                    return "Contextual";
                case PaletteOverlayKind.Guide:
                    return AnalysisProblemCount() == 0 ? "OK" : $"{AnalysisProblemCount()} issue(s)";
                case PaletteOverlayKind.Apply:
                    return _applyTargets.Count > 0 ? $"{_applyTargets.Count} target(s)" : "not scanned";
                case PaletteOverlayKind.History:
                    return _generationHistory.Count.ToString();
                case PaletteOverlayKind.Source:
                    return _activePalette != null ? _activePalette.name : "none";
                case PaletteOverlayKind.Settings:
                    return CurrentSwatchLayoutLabel();
                case PaletteOverlayKind.PalettePicker:
                    return _activePalette != null ? _activePalette.name : "select";
                default:
                    return null;
            }
        }

        private sealed class PaletteWorkflowInspectorPopupWindow : EditorWindow
        {
            private static PaletteWorkflowInspectorPopupWindow _instance;

            private PaletteDesignerWindow _owner;
            private PaletteOverlayKind _kind;
            private Vector2 _scroll;
            private bool _collapsed;

            public static void ShowFor(PaletteDesignerWindow owner, PaletteOverlayKind kind, Rect activatorRect)
            {
                if (owner == null || !IsWorkflowInspector(kind))
                    return;

                if (owner.ShouldDrawInlineWorkflowInspectors())
                {
                    CloseForOwner(owner);
                    return;
                }

                bool sameInspector = _instance != null && _instance._owner == owner && _instance._kind == kind;
                if (_instance == null)
                {
                    _instance = CreateInstance<PaletteWorkflowInspectorPopupWindow>();
                    _instance.titleContent = new GUIContent("Palette Inspector");
                    _instance.ShowPopup();
                }

                _instance._owner = owner;
                _instance._kind = kind;
                _instance._collapsed = false;
                if (!sameInspector)
                    _instance._scroll = Vector2.zero;
                _instance.position = CalculatePopupRect(owner, activatorRect, false);
                _instance.minSize = new Vector2(_instance.position.width, _instance.position.height);
                _instance.maxSize = new Vector2(_instance.position.width, _instance.position.height);
                _instance.Repaint();
            }

            public static void CloseForOwner(PaletteDesignerWindow owner)
            {
                if (_instance != null && (_instance._owner == owner || owner == null))
                    _instance.Close();
            }

            public static bool IsOpenFor(PaletteDesignerWindow owner, PaletteOverlayKind kind)
            {
                return _instance != null && _instance._owner == owner && _instance._kind == kind;
            }

            public static void FocusFor(PaletteDesignerWindow owner, PaletteOverlayKind kind)
            {
                if (_instance == null || _instance._owner != owner || _instance._kind != kind)
                    return;

                _instance.Focus();
                _instance.Repaint();
            }

            private void OnDisable()
            {
                if (_instance == this)
                    _instance = null;
            }

            private void OnGUI()
            {
                UtilityWindowTheme.EnsureStyles();

                if (_owner == null)
                {
                    Close();
                    return;
                }

                Event evt = Event.current;
                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    GUI.FocusControl(null);
                    _owner.Repaint();
                    evt.Use();
                }

                Rect full = new Rect(0f, 0f, position.width, position.height);
                EditorGUI.DrawRect(full, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.13f, 0.985f) : new Color(0.82f, 0.82f, 0.84f, 0.985f));
                DrawHeader(new Rect(0f, 0f, position.width, OverlayHeaderHeight));

                if (_collapsed)
                    return;

                Rect bodyRect = new Rect(6f, OverlayHeaderHeight + 6f, position.width - 12f, Mathf.Max(20f, position.height - OverlayHeaderHeight - 12f));
                GUILayout.BeginArea(bodyRect);
                float contentWidth = InspectorBodyContentWidth(bodyRect.width);
                _scroll = GUILayout.BeginScrollView(_scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(contentWidth), GUILayout.ExpandWidth(false)))
                    _owner.DrawWorkflowInspectorBody(_kind, contentWidth);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                DrawResizeGrip();

                if (GUI.changed)
                {
                    _owner.SavePrefs();
                    _owner.Repaint();
                }
            }

            private void DrawHeader(Rect rect)
            {
                GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);

                Rect selectorRect = new Rect(rect.x + 4f, rect.y + 4f, Mathf.Max(320f, rect.width - 8f), rect.height - 8f);

                GUILayout.BeginArea(selectorRect);
                PaletteOverlayKind selected = _owner.DrawWorkflowInspectorSelector(_kind, selectorRect.width);
                GUILayout.EndArea();
                if (selected != _kind)
                {
                    _kind = selected;
                    _scroll = Vector2.zero;
                    _owner._activeWorkflowInspector = selected;
                    _owner._lastWorkflowInspector = selected;
                    _owner.SavePrefs();
                    _owner.Repaint();
                }
            }

            private void DrawResizeGrip()
            {
                Rect grip = new Rect(position.width - 18f, position.height - 18f, 14f, 14f);
                int controlId = GUIUtility.GetControlID(FocusType.Passive, grip);
                Event current = Event.current;

                if (current.type == EventType.Repaint)
                {
                    Color line = EditorGUIUtility.isProSkin ? new Color(0.54f, 0.56f, 0.60f, 0.72f) : new Color(0.30f, 0.32f, 0.36f, 0.62f);
                    EditorGUI.DrawRect(new Rect(grip.x + 8f, grip.y + 12f, 5f, 1f), line);
                    EditorGUI.DrawRect(new Rect(grip.x + 4f, grip.y + 12f, 1f, 1f), line);
                    EditorGUI.DrawRect(new Rect(grip.x + 12f, grip.y + 8f, 1f, 5f), line);
                    EditorGUI.DrawRect(new Rect(grip.x + 12f, grip.y + 4f, 1f, 1f), line);
                }

                EditorGUIUtility.AddCursorRect(grip, MouseCursor.ResizeHorizontal);
                switch (current.GetTypeForControl(controlId))
                {
                    case EventType.MouseDown:
                        if (current.button == 0 && grip.Contains(current.mousePosition))
                        {
                            GUIUtility.hotControl = controlId;
                            current.Use();
                        }
                        break;
                    case EventType.MouseDrag:
                        if (GUIUtility.hotControl == controlId)
                        {
                            float width = Mathf.Max(WorkflowPopupMinWidth, position.width + current.delta.x);
                            Rect next = ClampPopupRect(new Rect(position.x, position.y, width, position.height));
                            position = next;
                            minSize = new Vector2(next.width, next.height);
                            maxSize = new Vector2(next.width, next.height);
                            if (_owner != null)
                            {
                                _owner._workflowPopupWidth = next.width;
                                _owner.SavePrefs();
                            }
                            current.Use();
                        }
                        break;
                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == controlId)
                        {
                            GUIUtility.hotControl = 0;
                            current.Use();
                        }
                        break;
                }
            }

            private void OnLostFocus() { }

            private void OnInspectorUpdate()
            {
                if (_owner == null)
                    return;

                if (_owner.ShouldDrawInlineWorkflowInspectors())
                {
                    Close();
                    _owner.Repaint();
                    return;
                }

                SyncPopupToOwnerBounds();
                Repaint();
            }

            private void SyncPopupToOwnerBounds()
            {
                if (_owner == null)
                    return;

                Rect next = CalculatePopupRect(_owner, Rect.zero, _collapsed);
                if (Mathf.Abs(position.x - next.x) < 0.5f &&
                    Mathf.Abs(position.y - next.y) < 0.5f &&
                    Mathf.Abs(position.width - next.width) < 0.5f &&
                    Mathf.Abs(position.height - next.height) < 0.5f)
                    return;

                position = next;
                minSize = new Vector2(next.width, next.height);
                maxSize = new Vector2(next.width, next.height);
            }

            private static Rect CalculatePopupRect(PaletteDesignerWindow owner, Rect activatorRect, bool collapsed)
            {
                Rect main = PungentUtilityMinimizer.GetMainEditorWindowRectForMinimizedUtilities();
                Rect ownerRect = owner.WorkflowPopupOwnerScreenRect();

                float maxWidth = Mathf.Max(280f, main.width - 24f);
                float minWidth = Mathf.Min(WorkflowPopupMinWidth, maxWidth);
                float width = Mathf.Clamp(owner._workflowPopupWidth, minWidth, maxWidth);
                float maxHeight = Mathf.Max(OverlayHeaderHeight, main.height - 24f);
                float minHeight = Mathf.Min(WorkflowPopupMinHeight, maxHeight);
                float desiredHeight = Mathf.Max(OverlayHeaderHeight, ownerRect.height);
                float height = collapsed ? OverlayHeaderHeight : Mathf.Clamp(desiredHeight, minHeight, maxHeight);
                float x = ownerRect.xMax + 4f;
                if (x + width > main.xMax - 12f)
                    x = ownerRect.x - width - 4f;
                x = Mathf.Clamp(x, main.x + 12f, Mathf.Max(main.x + 12f, main.xMax - width - 12f));

                float y = ownerRect.height > 0f ? ownerRect.y : main.y + 78f;
                y = Mathf.Clamp(y, main.y + 12f, Mathf.Max(main.y + 12f, main.yMax - height - 12f));
                return new Rect(x, y, width, height);
            }

            private static Rect ClampPopupRect(Rect rect)
            {
                Rect main = PungentUtilityMinimizer.GetMainEditorWindowRectForMinimizedUtilities();
                float maxWidth = Mathf.Max(280f, main.width - 24f);
                float minWidth = Mathf.Min(WorkflowPopupMinWidth, maxWidth);
                float width = Mathf.Clamp(rect.width, minWidth, maxWidth);
                float maxHeight = Mathf.Max(OverlayHeaderHeight, main.height - 24f);
                float minHeight = Mathf.Min(WorkflowPopupMinHeight, maxHeight);
                float height = Mathf.Clamp(rect.height, minHeight, maxHeight);
                float x = Mathf.Clamp(rect.x, main.x + 12f, Mathf.Max(main.x + 12f, main.xMax - width - 12f));
                float y = Mathf.Clamp(rect.y, main.y + 12f, Mathf.Max(main.y + 12f, main.yMax - height - 12f));
                return new Rect(x, y, width, height);
            }
        }

        private int AnalysisProblemCount()
        {
            return _analysisReport != null && _analysisReport.problems != null ? _analysisReport.problems.Count : 0;
        }

        private int AnalysisPairCount()
        {
            return _analysisReport != null && _analysisReport.pairs != null ? _analysisReport.pairs.Count : 0;
        }

        private bool IsSwatchSelected(int index)
        {
            return index >= 0 && _selectedSwatches.Contains(index);
        }

        private int SelectedSwatchCount()
        {
            PruneSelection();
            return _selectedSwatches.Count;
        }

        private List<int> SelectedSwatchIndices(bool includeLocked)
        {
            PruneSelection();
            var indices = new List<int>(_selectedSwatches);
            indices.Sort();
            if (includeLocked)
                return indices;

            for (int i = indices.Count - 1; i >= 0; i--)
            {
                PaletteSwatch swatch = _activePalette != null && _activePalette.swatches != null && indices[i] < _activePalette.swatches.Count
                    ? _activePalette.swatches[indices[i]]
                    : null;
                if (swatch == null || swatch.locked)
                    indices.RemoveAt(i);
            }

            return indices;
        }

        private void SelectSingleSwatch(int index)
        {
            _selectedSwatches.Clear();
            if (_activePalette == null || _activePalette.swatches == null || index < 0 || index >= _activePalette.swatches.Count)
            {
                _selectedSwatch = -1;
                _selectionAnchor = -1;
                return;
            }

            _selectedSwatches.Add(index);
            _selectedSwatch = index;
            _selectionAnchor = index;
        }

        private void SelectSwatchFromEvent(int index, Event current)
        {
            if (_activePalette == null || _activePalette.swatches == null || index < 0 || index >= _activePalette.swatches.Count)
                return;

            bool additive = current != null && (current.control || current.command);
            bool range = current != null && current.shift && _selectionAnchor >= 0;

            if (range)
            {
                _selectedSwatches.Clear();
                int min = Mathf.Min(_selectionAnchor, index);
                int max = Mathf.Max(_selectionAnchor, index);
                for (int i = min; i <= max; i++)
                    _selectedSwatches.Add(i);
            }
            else if (additive)
            {
                if (_selectedSwatches.Contains(index) && _selectedSwatches.Count > 1)
                    _selectedSwatches.Remove(index);
                else
                    _selectedSwatches.Add(index);
                _selectionAnchor = index;
            }
            else
            {
                _selectedSwatches.Clear();
                _selectedSwatches.Add(index);
                _selectionAnchor = index;
            }

            _selectedSwatch = index;
            PruneSelection();
            Repaint();
        }

        private void SelectAllSwatches()
        {
            _selectedSwatches.Clear();
            if (_activePalette == null || _activePalette.swatches == null)
                return;

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                if (_activePalette.swatches[i] != null)
                    _selectedSwatches.Add(i);
            }

            if (_selectedSwatches.Count > 0)
            {
                _selectedSwatch = 0;
                _selectionAnchor = 0;
                _lastStatus = $"Selected {_selectedSwatches.Count} swatches.";
            }
            Repaint();
        }

        private void ClearSwatchSelection()
        {
            _selectedSwatches.Clear();
            _selectedSwatch = -1;
            _selectionAnchor = -1;
            Repaint();
        }

        private void PruneSelection()
        {
            if (_activePalette == null || _activePalette.swatches == null)
            {
                _selectedSwatches.Clear();
                _selectedSwatch = -1;
                _selectionAnchor = -1;
                return;
            }

            _selectedSwatches.RemoveWhere(index => index < 0 || index >= _activePalette.swatches.Count || _activePalette.swatches[index] == null);
            if (_selectedSwatches.Count == 0)
            {
                _selectedSwatch = -1;
                _selectionAnchor = -1;
                return;
            }

            if (_selectedSwatch < 0 || _selectedSwatch >= _activePalette.swatches.Count || !_selectedSwatches.Contains(_selectedSwatch))
            {
                var indices = new List<int>(_selectedSwatches);
                indices.Sort();
                _selectedSwatch = indices[0];
            }

            if (_selectionAnchor < 0 || _selectionAnchor >= _activePalette.swatches.Count)
                _selectionAnchor = _selectedSwatch;
        }

        private void SetSelectedSwatchesLocked(bool locked)
        {
            List<int> indices = SelectedSwatchIndices(true);
            if (indices.Count == 0)
                return;

            ChangePalette(locked ? "Lock Palette Swatches" : "Unlock Palette Swatches", () =>
            {
                for (int i = 0; i < indices.Count; i++)
                    _activePalette.swatches[indices[i]].locked = locked;
            });
            _lastStatus = locked ? $"Locked {indices.Count} swatches." : $"Unlocked {indices.Count} swatches.";
        }

        private void CopySelectedHexValues()
        {
            List<int> indices = SelectedSwatchIndices(true);
            if (indices.Count == 0)
                return;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < indices.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[indices[i]];
                if (swatch == null)
                    continue;

                builder.Append(string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {indices[i] + 1}" : swatch.name);
                builder.Append(": ");
                builder.AppendLine(ColourConversionUtility.ToHexRGB(swatch.color));
            }

            CopyText(builder.ToString(), $"Copied {indices.Count} selected HEX values.");
        }

        private void RegenerateSelectedSwatches()
        {
            List<int> indices = SelectedSwatchIndices(false);
            if (indices.Count == 0)
            {
                _lastStatus = "No unlocked selected swatches to regenerate.";
                return;
            }

            PaletteGenerationSettings settings = CurrentGenerationSettingsForPalette(false);
            settings.targetSwatchCount = _activePalette.swatches.Count;
            PaletteGenerationSuggestion suggestion = CurrentAutoContextSuggestionForExecution("selected swatch regeneration");
            PaletteGenerationResult result = PaletteGeneratorUtility.GenerateResult(settings, _activePalette.swatches, "Regenerated Selection", suggestion);
            List<PaletteSwatch> generatedSwatches = result.CloneSwatches();

            RecordPaletteSnapshot(NextSnapshotLabel(), $"Palette state before regenerating {indices.Count} selected swatch(es).");
            ChangePalette("Regenerate Selected Swatches", () =>
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    int index = indices[i];
                    PaletteSwatch existing = _activePalette.swatches[index];
                    PaletteSwatch generated = index < generatedSwatches.Count ? generatedSwatches[index] : null;
                    if (existing == null || existing.locked || generated == null)
                        continue;

                    existing.color = generated.color;
                    if (existing.role == PaletteSwatchRole.None || existing.role == PaletteSwatchRole.Custom)
                        existing.role = generated.role;
                    if (string.IsNullOrWhiteSpace(existing.name) || existing.name.StartsWith("Swatch"))
                        existing.name = generated.name;
                }
            });

            RememberAutoCoverageFromPalette(indices);
            _lastStatus = $"Regenerated {indices.Count} selected swatch(es).";
        }

        private void MoveSelectedSwatchesToIndex(int insertIndex)
        {
            List<int> indices = SelectedSwatchIndices(true);
            if (_activePalette == null || _activePalette.swatches == null || indices.Count == 0)
                return;

            insertIndex = Mathf.Clamp(insertIndex, 0, _activePalette.swatches.Count);
            int removedBeforeTarget = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] < insertIndex)
                    removedBeforeTarget++;
            }

            int adjustedInsertIndex = Mathf.Clamp(insertIndex - removedBeforeTarget, 0, _activePalette.swatches.Count - indices.Count);
            var moving = new List<PaletteSwatch>();
            for (int i = 0; i < indices.Count; i++)
                moving.Add(_activePalette.swatches[indices[i]]);

            ChangePalette("Reorder Palette Swatches", () =>
            {
                for (int i = indices.Count - 1; i >= 0; i--)
                    _activePalette.swatches.RemoveAt(indices[i]);

                _activePalette.swatches.InsertRange(adjustedInsertIndex, moving);
                RecalculatePriorities();
                _selectedSwatches.Clear();
                for (int i = 0; i < moving.Count; i++)
                    _selectedSwatches.Add(adjustedInsertIndex + i);
                _selectedSwatch = adjustedInsertIndex;
                _selectionAnchor = adjustedInsertIndex;
            });

            _lastStatus = moving.Count > 1 ? $"Moved {moving.Count} swatches." : "Moved swatch.";
        }

        private void MoveSelectedSwatchesByKeyboard(int delta)
        {
            List<int> indices = SelectedSwatchIndices(true);
            if (_activePalette == null || _activePalette.swatches == null || indices.Count == 0 || delta == 0)
                return;

            if (delta < 0)
            {
                if (indices[0] <= 0)
                    return;

                MoveSelectedSwatchesToIndex(indices[0] - 1);
                _lastStatus = indices.Count > 1 ? $"Moved {indices.Count} selected swatches earlier." : "Moved swatch earlier.";
                return;
            }

            int last = indices[indices.Count - 1];
            if (last >= _activePalette.swatches.Count - 1)
                return;

            MoveSelectedSwatchesToIndex(last + 2);
            _lastStatus = indices.Count > 1 ? $"Moved {indices.Count} selected swatches later." : "Moved swatch later.";
        }

        private void AddSwatch()
        {
            ChangePalette("Add Palette Swatch", () =>
            {
                _activePalette.swatches.Add(new PaletteSwatch($"Swatch {_activePalette.swatches.Count + 1}", Color.white, PaletteSwatchRole.Custom, false, _activePalette.swatches.Count));
                SelectSingleSwatch(_activePalette.swatches.Count - 1);
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
                SelectSingleSwatch(_selectedSwatch + 1);
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
                SelectSingleSwatch(Mathf.Clamp(_selectedSwatch, -1, _activePalette.swatches.Count - 1));
                RecalculatePriorities();
            });
        }

        private void MoveSelectedSwatch(int delta)
        {
            if (!IsValidSelectedSwatch())
                return;

            MoveSwatch(_selectedSwatch, delta);
        }

        private void MoveSwatch(int index, int delta)
        {
            if (_activePalette == null || _activePalette.swatches == null || index < 0 || index >= _activePalette.swatches.Count || delta == 0)
                return;

            int next = Mathf.Clamp(index + delta, 0, _activePalette.swatches.Count - 1);
            if (next == index)
                return;

            ChangePalette("Move Palette Swatch", () =>
            {
                PaletteSwatch swatch = _activePalette.swatches[index];
                _activePalette.swatches.RemoveAt(index);
                _activePalette.swatches.Insert(next, swatch);
                SelectSingleSwatch(next);
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
            IteratePalette();
        }

        private string PrimaryIterationLabel()
        {
            return "Generate";
        }

        private void IteratePalette()
        {
            if (_activePalette == null)
                return;

            if (_activePalette.swatches == null || _activePalette.swatches.Count == 0)
            {
                PaletteGenerationSettings starter = CurrentGenerationSettingsForPalette(false);
                PaletteGenerationSuggestion starterSuggestion = CurrentAutoContextSuggestionForExecution("starter palette generation");
                PaletteGenerationResult starterResult = PaletteGeneratorUtility.GenerateResult(starter, null, "Starter Palette", starterSuggestion);
                List<PaletteSwatch> generated = starterResult.CloneSwatches();
                ChangePalette("Generate Starter Palette", () =>
                {
                    _activePalette.swatches.Clear();
                    for (int i = 0; i < generated.Count; i++)
                        _activePalette.swatches.Add(generated[i].Clone());
                    SelectSingleSwatch(_activePalette.swatches.Count > 0 ? 0 : -1);
                    RecalculatePriorities();
                });
                RememberAutoCoverageFromPalette(null);
                _lastStatus = "Generated starter palette.";
                return;
            }

            PaletteGenerationSettings settings = CurrentGenerationSettingsForPalette(false);
            settings.targetSwatchCount = _activePalette.swatches.Count;
            PaletteGenerationSuggestion regenerateSuggestion = CurrentAutoContextSuggestionForExecution("palette iteration");
            RecordPaletteSnapshot(NextSnapshotLabel(), "Palette state before regenerating unlocked swatches.");
            PaletteGenerationResult regenerateResult = PaletteGeneratorUtility.RegenerateUnlockedResult(settings, _activePalette.swatches, "Regenerated Unlocked", regenerateSuggestion);
            List<PaletteSwatch> generatedSwatches = regenerateResult.CloneSwatches();

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

            RememberAutoCoverageFromPalette(null);
            _lastStatus = "Generated a new unlocked swatch pass.";
        }

        private void RecordPaletteSnapshot(string label, string reason)
        {
            if (_activePalette == null || _activePalette.swatches == null || _activePalette.swatches.Count == 0)
                return;

            PaletteGenerationSettings snapshotSettings = CurrentGenerationSettingsForPalette(false);
            var snapshot = new PaletteGenerationVariant
            {
                id = System.Guid.NewGuid().ToString("N"),
                label = string.IsNullOrWhiteSpace(label) ? NextSnapshotLabel() : label,
                createdUtc = System.DateTime.UtcNow.ToString("u"),
                settings = snapshotSettings,
                diagnostics = new PaletteGenerationDiagnostics
                {
                    requestedCount = _activePalette.swatches.Count,
                    generatedCount = _activePalette.swatches.Count,
                    preservedLockedCount = CountLockedSwatches(),
                    usedSeed = snapshotSettings != null && snapshotSettings.useSeed,
                    seed = snapshotSettings != null ? snapshotSettings.seed : 0,
                    harmonyMode = snapshotSettings != null ? snapshotSettings.harmonyMode : ColourHarmonyMode.Contextual
                }
            };

            if (!string.IsNullOrWhiteSpace(reason))
                snapshot.diagnostics.AddNote(reason);

            if (_autoContextSuggestion != null)
            {
                snapshot.diagnostics.harmonyMode = _autoContextSuggestion.chosenHarmonyMode;
                if (_autoContextSuggestion.executionSeed != 0)
                {
                    snapshot.diagnostics.usedSeed = true;
                    snapshot.diagnostics.seed = _autoContextSuggestion.executionSeed;
                    snapshot.diagnostics.AddNote($"Contextual pass {_autoContextSuggestion.iterationSerial} prepared execution seed {_autoContextSuggestion.executionSeed}.");
                }
                if (_autoContextSuggestion.softCoverageCount > 0)
                    snapshot.diagnostics.AddNote($"Soft coverage considered {_autoContextSuggestion.softCoverageCount} unlocked/recent colour lane(s).");
                if (_autoContextSuggestion.targetHueBands != null && _autoContextSuggestion.targetHueBands.Count > 0)
                    snapshot.diagnostics.AddNote($"Next generation hue bands: {_autoContextSuggestion.targetHueBands.Count}.");
            }

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch != null)
                    snapshot.swatches.Add(swatch.Clone());
            }

            _generationHistory.Add(snapshot);
        }

        private string NextSnapshotLabel()
        {
            return $"Iteration {_generationHistory.Count + 1}";
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
            PaletteGenerationSuggestion suggestion = CurrentAutoContextSuggestionForExecution($"regenerating {existing.name}");
            PaletteGenerationResult result = PaletteGeneratorUtility.GenerateResult(settings, _activePalette.swatches, $"Regenerated {existing.name}", suggestion);
            List<PaletteSwatch> generatedSwatches = result.CloneSwatches();
            PaletteSwatch generated = index < generatedSwatches.Count ? generatedSwatches[index] : null;
            if (generated == null)
                return;

            RecordPaletteSnapshot(NextSnapshotLabel(), $"Palette state before regenerating {existing.name}.");
            ChangePalette("Regenerate Swatch", () =>
            {
                existing.color = generated.color;
                if (existing.role == PaletteSwatchRole.None || existing.role == PaletteSwatchRole.Custom)
                    existing.role = generated.role;
                if (string.IsNullOrWhiteSpace(existing.name) || existing.name.StartsWith("Swatch"))
                    existing.name = generated.name;
            });

            RememberAutoCoverageFromPalette(new List<int> { index });
            SelectSingleSwatch(index);
            _lastStatus = $"Regenerated {existing.name}.";
        }

        private PaletteGenerationSettings CurrentGenerationSettingsForPalette(bool preserveLocked)
        {
            PaletteGenerationSettings settings = GetEffectiveGenerationSettings();
            settings.targetSwatchCount = _activePalette != null && _activePalette.swatches != null && _activePalette.swatches.Count > 0
                ? _activePalette.swatches.Count
                : Mathf.Max(1, settings.targetSwatchCount);

            if (preserveLocked)
                settings.preserveLockedSwatches = true;

            settings.Clamp();
            return settings;
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
            bool group = IsSwatchSelected(index) && SelectedSwatchCount() > 1;
            menu.AddItem(new GUIContent(group ? $"Selected Group/{SelectedSwatchCount()} Swatches" : "Select"), index == _selectedSwatch, () => { SelectSingleSwatch(index); Repaint(); });
            if (group)
            {
                menu.AddItem(new GUIContent("Selected Group/Lock"), false, () => SetSelectedSwatchesLocked(true));
                menu.AddItem(new GUIContent("Selected Group/Unlock"), false, () => SetSelectedSwatchesLocked(false));
                menu.AddItem(new GUIContent("Selected Group/Regenerate Unlocked"), false, RegenerateSelectedSwatches);
                menu.AddItem(new GUIContent("Selected Group/Copy HEX List"), false, CopySelectedHexValues);
                menu.AddItem(new GUIContent("Selected Group/Move Earlier"), false, () => MoveSelectedSwatchesByKeyboard(-1));
                menu.AddItem(new GUIContent("Selected Group/Move Later"), false, () => MoveSelectedSwatchesByKeyboard(1));
                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent(swatch.locked ? "Unlock" : "Lock"), false, () => ChangePalette("Toggle Swatch Lock", () => swatch.locked = !swatch.locked));
            if (swatch.locked)
                menu.AddDisabledItem(new GUIContent("Regenerate"));
            else
                menu.AddItem(new GUIContent("Regenerate"), false, () => RegenerateSwatch(index));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Copy/Hex RGB"), false, () => CopyText(ColourConversionUtility.ToHexRGB(swatch.color), "Copied Hex RGB."));
            menu.AddItem(new GUIContent("Copy/Hex RGBA"), false, () => CopyText(ColourConversionUtility.ToHexRGBA(swatch.color), "Copied Hex RGBA."));
            menu.AddItem(new GUIContent("Copy/RGB"), false, () => CopyText(ColourConversionUtility.FormatRGB(swatch.color), "Copied RGB."));
            menu.AddItem(new GUIContent("Copy/HSV"), false, () => CopyText(ColourConversionUtility.FormatHSV(swatch.color), "Copied HSV."));
            menu.AddSeparator("");
            if (index > 0)
                menu.AddItem(new GUIContent("Move Earlier"), false, () => MoveSwatch(index, -1));
            else
                menu.AddDisabledItem(new GUIContent("Move Earlier"));
            if (_activePalette.swatches != null && index < _activePalette.swatches.Count - 1)
                menu.AddItem(new GUIContent("Move Later"), false, () => MoveSwatch(index, 1));
            else
                menu.AddDisabledItem(new GUIContent("Move Later"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Duplicate"), false, () => { _selectedSwatch = index; DuplicateSelectedSwatch(); });
            menu.AddItem(new GUIContent("Remove"), false, () => { _selectedSwatch = index; RemoveSelectedSwatch(); });
            menu.ShowAsContext();
        }

        private void ShowSelectedSwatchBulkMenu()
        {
            int count = SelectedSwatchCount();
            GenericMenu menu = new GenericMenu();

            if (count <= 1)
            {
                menu.AddDisabledItem(new GUIContent("Select multiple swatches first"));
                menu.ShowAsContext();
                return;
            }

            menu.AddDisabledItem(new GUIContent($"{count} swatches selected"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Lock Selected"), false, () => SetSelectedSwatchesLocked(true));
            menu.AddItem(new GUIContent("Unlock Selected"), false, () => SetSelectedSwatchesLocked(false));
            menu.AddItem(new GUIContent("Regenerate Unlocked Selected"), false, RegenerateSelectedSwatches);
            menu.AddItem(new GUIContent("Copy Selected HEX List"), false, CopySelectedHexValues);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Move Earlier / Alt+Left"), false, () => MoveSelectedSwatchesByKeyboard(-1));
            menu.AddItem(new GUIContent("Move Later / Alt+Right"), false, () => MoveSelectedSwatchesByKeyboard(1));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Clear Selection"), false, ClearSwatchSelection);
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
            _lastStatus = "Improved unlocked Text/Muted Text roles; locked swatches were skipped.";
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
            _lastStatus = "Added or repaired an unlocked Panel role; locked Panel swatches were skipped.";
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
            _lastStatus = "Added or repaired an unlocked Text role; locked Text swatches were skipped.";
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
            _lastStatus = $"Repaired {selected.name} against {background.name}.";
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
                SaveLastPaletteGuid(_activePalette);
                _paletteAssetCacheDirty = true;
                _lastStatus = "Loaded palette.";
            }
            else
            {
                _lastStatus = "Ready";
            }

            _generationHistory.Clear();
            _recentAutoCoverageSwatches.Clear();
            _recentAutoCoverageSwatchIndices.Clear();
            _autoIterationSerial = 0;
            _selectedSwatch = -1;
            _selectedSwatches.Clear();
            _selectionAnchor = -1;
            _analysisDirty = true;
            _autoContextSuggestion = null;
            _autoContextDirty = true;
            _lastPalette = _activePalette;
        }

        private void SaveLastPaletteGuid(PungentColourPaletteSO palette)
        {
            string guid = PungentPaletteStorageUtility.GetPaletteGuid(palette);
            if (string.IsNullOrEmpty(guid))
                return;

            _rememberedPaletteGuid = guid;
            UtilityWindowPrefs.SetString(PrefLastPaletteGuid, guid);
        }

        private bool TryLoadLastPaletteFromPrefs(out PungentColourPaletteSO palette)
        {
            palette = null;
            string guid = !string.IsNullOrWhiteSpace(_rememberedPaletteGuid)
                ? _rememberedPaletteGuid
                : UtilityWindowPrefs.GetString(PrefLastPaletteGuid, string.Empty);
            if (string.IsNullOrWhiteSpace(guid))
                return false;

            palette = PungentPaletteStorageUtility.LoadPaletteByGuid(guid);
            if (palette != null)
            {
                _rememberedPaletteGuid = guid;
                return true;
            }

            if (_rememberedPaletteGuid == guid)
                _rememberedPaletteGuid = string.Empty;
            UtilityWindowPrefs.SetString(PrefLastPaletteGuid, string.Empty);
            _lastStatus = "Remembered palette asset was missing. Choose or create a palette.";
            return false;
        }

        private void RefreshPaletteAssetCache()
        {
            _paletteAssetCache.Clear();
            _paletteAssetCache.AddRange(PungentPaletteStorageUtility.FindPaletteAssets());
            SortPaletteAssetCache();
            _paletteAssetCacheDirty = false;
        }

        private void EnsurePaletteAssetCache()
        {
            if (_paletteAssetCacheDirty)
                RefreshPaletteAssetCache();
        }

        private void MarkPaletteAssetCacheDirty()
        {
            _paletteAssetCacheDirty = true;
        }

        private void SortPaletteAssetCache()
        {
            string activeGuid = PungentPaletteStorageUtility.GetPaletteGuid(_activePalette);
            string rememberedGuid = _rememberedPaletteGuid;
            _paletteAssetCache.Sort((a, b) =>
            {
                int priority = PaletteAssetPriority(a, activeGuid, rememberedGuid).CompareTo(PaletteAssetPriority(b, activeGuid, rememberedGuid));
                if (priority != 0)
                    return priority;

                int name = string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase);
                return name != 0 ? name : string.Compare(a.path, b.path, System.StringComparison.OrdinalIgnoreCase);
            });
        }

        private static int PaletteAssetPriority(PungentPaletteStorageUtility.PaletteAssetInfo info, string activeGuid, string rememberedGuid)
        {
            if (!string.IsNullOrEmpty(activeGuid) && info.guid == activeGuid)
                return 0;
            if (!string.IsNullOrEmpty(rememberedGuid) && info.guid == rememberedGuid)
                return 1;
            return 2;
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

            PruneSelection();
        }

        private void RefreshAnalysisIfNeeded()
        {
            if (!_analysisDirty && _analysisReport != null)
                return;

            _analysisReport = PaletteAnalysisUtility.Analyse(_activePalette);
            _analysisDirty = false;
        }

        private void RefreshAutoContextIfNeeded()
        {
            _autoContextEnabled = true;
            if (!_autoContextDirty && _autoContextSuggestion != null)
                return;

            PaletteGenerationSettings baseSettings = BuildHybridBaseSettingsForSuggestion();
            _autoContextSuggestion = PaletteGeneratorUtility.SuggestFromLockedSwatches(baseSettings, AutoContextSwatchesForSuggestion(true));
            ApplyHybridOverridesToSuggestion(_autoContextSuggestion);
            if (_autoContextSuggestion != null && _recentAutoCoverageSwatches.Count > 0)
                _autoContextSuggestion.diagnostics.AddNote($"Recent exploration memory is steering away from {_recentAutoCoverageSwatches.Count} generated colour lane(s).");
            _autoContextDirty = false;
        }

        private PaletteGenerationSuggestion CurrentAutoContextSuggestion()
        {
            RefreshAutoContextIfNeeded();
            return _autoContextSuggestion;
        }

        private PaletteGenerationSuggestion CurrentAutoContextSuggestionForExecution(string reason)
        {
            _autoContextEnabled = true;
            _autoIterationSerial++;
            PaletteGenerationSettings baseSettings = BuildHybridBaseSettingsForSuggestion();
            PaletteGenerationSuggestion suggestion = PaletteGeneratorUtility.SuggestFromLockedSwatches(baseSettings, AutoContextSwatchesForSuggestion(true));
            ApplyHybridOverridesToSuggestion(suggestion);
            ApplyAutoIterationSeed(suggestion, reason);
            _autoContextSuggestion = suggestion;
            _autoContextDirty = false;
            return suggestion;
        }

        private void ApplyAutoIterationSeed(PaletteGenerationSuggestion suggestion, string reason)
        {
            if (suggestion == null || suggestion.effectiveSettings == null)
                return;

            int baseSeed = _workingSettings != null && _workingSettings.useSeed
                ? _workingSettings.seed
                : unchecked(System.Environment.TickCount * 397);
            int signature = BuildPaletteIterationSignature();
            int seed = unchecked(baseSeed + _autoIterationSerial * 1009 + signature * 37);

            suggestion.iterationSerial = _autoIterationSerial;
            suggestion.executionSeed = seed;
            suggestion.effectiveSettings.useSeed = true;
            suggestion.effectiveSettings.seed = seed;
            suggestion.diagnostics.usedSeed = true;
            suggestion.diagnostics.seed = seed;
            suggestion.diagnostics.AddNote($"Contextual pass {_autoIterationSerial} uses execution seed {seed} for {reason}.");
            if (_recentAutoCoverageSwatches.Count > 0)
                suggestion.diagnostics.AddNote($"Contextual generation is avoiding {_recentAutoCoverageSwatches.Count} recently generated unlocked swatch lane(s).");
        }

        private IList<PaletteSwatch> AutoContextSwatchesForSuggestion(bool includeRecentCoverage)
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return null;

            if (!includeRecentCoverage || _recentAutoCoverageSwatches.Count == 0)
                return _activePalette.swatches;

            var combined = new List<PaletteSwatch>(_activePalette.swatches.Count + _recentAutoCoverageSwatches.Count);
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch != null)
                    combined.Add(swatch);
            }

            for (int i = 0; i < _recentAutoCoverageSwatches.Count; i++)
            {
                PaletteSwatch recent = _recentAutoCoverageSwatches[i];
                if (recent == null)
                    continue;

                PaletteSwatch coverage = recent.Clone();
                coverage.locked = false;
                combined.Add(coverage);
            }

            return combined;
        }

        private void RememberAutoCoverageFromPalette(IList<int> indices)
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return;

            if (indices == null)
            {
                for (int i = 0; i < _activePalette.swatches.Count; i++)
                    RememberAutoCoverageSwatch(_activePalette.swatches[i], i);
            }
            else
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    int index = indices[i];
                    if (index >= 0 && index < _activePalette.swatches.Count)
                        RememberAutoCoverageSwatch(_activePalette.swatches[index], index);
                }
            }

            while (_recentAutoCoverageSwatches.Count > MaxAutoCoverageMemory)
            {
                _recentAutoCoverageSwatches.RemoveAt(0);
                if (_recentAutoCoverageSwatchIndices.Count > 0)
                    _recentAutoCoverageSwatchIndices.RemoveAt(0);
            }

            MarkAutoContextDirty();
        }

        private void RememberAutoCoverageSwatch(PaletteSwatch swatch, int sourceIndex)
        {
            if (swatch == null || swatch.locked)
                return;

            _recentAutoCoverageSwatches.Add(swatch.Clone());
            _recentAutoCoverageSwatchIndices.Add(sourceIndex);
        }

        private int BuildPaletteIterationSignature()
        {
            unchecked
            {
                int hash = 17;
                if (_activePalette == null || _activePalette.swatches == null)
                    return hash;

                for (int i = 0; i < _activePalette.swatches.Count; i++)
                {
                    PaletteSwatch swatch = _activePalette.swatches[i];
                    if (swatch == null)
                        continue;

                    Color color = swatch.color;
                    hash = hash * 31 + i;
                    hash = hash * 31 + (int)swatch.role;
                    hash = hash * 31 + (swatch.locked ? 1 : 0);
                    hash = hash * 31 + Mathf.RoundToInt(color.r * 255f);
                    hash = hash * 31 + Mathf.RoundToInt(color.g * 255f);
                    hash = hash * 31 + Mathf.RoundToInt(color.b * 255f);
                }

                for (int i = 0; i < _recentAutoCoverageSwatches.Count; i++)
                {
                    PaletteSwatch swatch = _recentAutoCoverageSwatches[i];
                    if (swatch == null)
                        continue;

                    Color color = swatch.color;
                    hash = hash * 31 + 7919 + i;
                    hash = hash * 31 + (int)swatch.role;
                    hash = hash * 31 + Mathf.RoundToInt(color.r * 255f);
                    hash = hash * 31 + Mathf.RoundToInt(color.g * 255f);
                    hash = hash * 31 + Mathf.RoundToInt(color.b * 255f);
                }

                return hash;
            }
        }

        private PaletteGenerationSettings BuildHybridBaseSettingsForSuggestion()
        {
            PaletteGenerationSettings settings = new PaletteGenerationSettings();
            if (_workingSettings != null)
            {
                settings.preserveLockedSwatches = _workingSettings.preserveLockedSwatches;
                settings.enforceTextContrast = _workingSettings.enforceTextContrast;
                settings.avoidNearDuplicates = _workingSettings.avoidNearDuplicates;
                settings.preferReadableAccentPairs = _workingSettings.preferReadableAccentPairs;
                settings.useSeed = _workingSettings.useSeed;
                settings.seed = _workingSettings.seed;
            }

            settings.harmonyMode = _overrideHarmonyMode && _manualHarmonyModeEdited
                ? _manualHarmonyMode
                : ColourHarmonyMode.Contextual;

            if (_overrideTargetCount && _manualTargetCountEdited)
                settings.targetSwatchCount = Mathf.Clamp(_manualTargetCount, 1, 64);
            else if (_activePalette != null && _activePalette.swatches != null && _activePalette.swatches.Count > 0)
                settings.targetSwatchCount = _activePalette.swatches.Count;

            if (_overrideHueRange && _manualHueRangeEdited)
                settings.hueRange = _manualHueRange;
            if (_overrideSaturationRange && _manualSaturationRangeEdited)
                settings.saturationRange = _manualSaturationRange;
            if (_overrideValueRange && _manualValueRangeEdited)
                settings.valueRange = _manualValueRange;
            if (_overrideHarmonyInfluence && _manualHarmonyInfluenceEdited)
                settings.harmonyInfluence = _manualHarmonyInfluence;
            if (_overrideVariation && _manualVariationEdited)
                settings.randomVariation = _manualVariation;
            if (_overrideContrast && _manualContrastEdited)
                settings.contrastInfluence = _manualContrast;

            settings.Clamp();
            return settings;
        }

        private void ApplyHybridOverridesToSuggestion(PaletteGenerationSuggestion suggestion)
        {
            if (suggestion == null || suggestion.effectiveSettings == null)
                return;

            PaletteGenerationSettings settings = suggestion.effectiveSettings;
            var applied = new List<string>();

            if (_overrideHarmonyMode && _manualHarmonyModeEdited)
            {
                settings.harmonyMode = _manualHarmonyMode;
                suggestion.chosenHarmonyMode = _manualHarmonyMode;
                suggestion.requestedHarmonyMode = _manualHarmonyMode;
                applied.Add($"harmony {_manualHarmonyMode}");
            }

            if (_overrideTargetCount && _manualTargetCountEdited)
            {
                settings.targetSwatchCount = Mathf.Clamp(_manualTargetCount, 1, 64);
                applied.Add($"count {settings.targetSwatchCount}");
            }

            if (_overrideHueRange && _manualHueRangeEdited)
            {
                settings.hueRange = _manualHueRange;
                applied.Add("hue range");
            }

            if (_overrideSaturationRange && _manualSaturationRangeEdited)
            {
                settings.saturationRange = _manualSaturationRange;
                applied.Add("saturation range");
            }

            if (_overrideValueRange && _manualValueRangeEdited)
            {
                settings.valueRange = _manualValueRange;
                applied.Add("value range");
            }

            if (_overrideHarmonyInfluence && _manualHarmonyInfluenceEdited)
            {
                settings.harmonyInfluence = _manualHarmonyInfluence;
                applied.Add("harmony influence");
            }

            if (_overrideVariation && _manualVariationEdited)
            {
                settings.randomVariation = _manualVariation;
                AdjustSuggestionBandWidthsForVariation(suggestion, _manualVariation);
                applied.Add("variation");
            }

            if (_overrideContrast && _manualContrastEdited)
            {
                settings.contrastInfluence = _manualContrast;
                applied.Add("contrast");
            }

            settings.Clamp();
            if (applied.Count > 0)
                suggestion.diagnostics.AddNote("Manual override(s) guiding this contextual pass: " + string.Join(", ", applied) + ".");
        }

        private void AdjustSuggestionBandWidthsForVariation(PaletteGenerationSuggestion suggestion, float variation)
        {
            if (suggestion == null || suggestion.targetHueBands == null || suggestion.openContext)
                return;

            float width = Mathf.Lerp(0.055f, 0.14f, Mathf.Clamp01(variation));
            for (int i = 0; i < suggestion.targetHueBands.Count; i++)
            {
                PaletteHueBand band = suggestion.targetHueBands[i];
                if (band != null)
                    band.width = Mathf.Clamp(width, 0.025f, 1f);
            }
        }

        private PaletteGenerationSettings GetEffectiveGenerationSettings()
        {
            RefreshAutoContextIfNeeded();
            if (_autoContextSuggestion != null && _autoContextSuggestion.effectiveSettings != null)
                return _autoContextSuggestion.effectiveSettings.Clone();

            return BuildHybridBaseSettingsForSuggestion();
        }

        private void MarkAutoContextDirty()
        {
            _autoContextDirty = true;
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
            _autoContextDirty = true;
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
