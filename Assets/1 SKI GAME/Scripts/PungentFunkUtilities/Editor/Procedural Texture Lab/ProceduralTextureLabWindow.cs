using PungentFunk.Utilities.Colour;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.ProceduralTextureLab.";
        private const string PrefSessionJson = PrefPrefix + "SessionJson";
        private const string PrefPaletteAssetPath = PrefPrefix + "PaletteAssetPath";
        private const float InspectorDefaultWidth = 360f;
        private const float InspectorMinWidth = 340f;
        private const float InspectorMaxWidth = 540f;
        private const float InspectorHandleWidth = 6f;
        private const float WorkspaceMinWidth = 420f;
        private const float InlineInspectorMinWidth = 980f;
        private const float InlineInspectorMinHeight = 560f;
        private const float BaseGap = 8f;
        private const float BaseMinWidth = 152f;
        private const float CandidateMinWidth = 168f;
        private const int GridPreviewSize = 192;
        private const int ActivePreviewSize = 384;
        private const double SessionSaveDebounceSeconds = 0.65d;
        private const double ManualComposeAutoPreviewDebounceSeconds = 0.25d;

        private enum TextureInspectorTab
        {
            Compose,
            Refine,
            Export,
            History,
            Explore
        }

        private enum TextureHistoryTab
        {
            Combinations,
            Bases
        }

        private enum TextureButtonTone
        {
            Primary,
            Secondary,
            Ghost,
            Danger
        }

        private enum ManualComposeGuidedStep
        {
            Shape,
            Placement,
            TextureInputs,
            BlendReview
        }

        [Serializable]
        private sealed class TextureLabSessionState
        {
            public ProceduralTextureCombinationSettings combination;
            public ProceduralTextureMapExportSettings export;
            public ProceduralTextureBaseSettings[] bases;
            public ProceduralTextureCandidate[] variants;
            public ProceduralTextureCandidate[] influences;
            public int selectedBase;
            public int candidateDensity = 0;
            public bool checkerboard = true;
            public bool tilePreview = false;
            public bool inspectorOpen = true;
            public bool manualComposeAutoPreview = true;
            public int activeCandidateIndex = -1;
            public TextureDesignWorkflow activeWorkflow = TextureDesignWorkflow.RandomExplore;
            public TextureDesignerPhase activeDesignerPhase = TextureDesignerPhase.Explore;
            public TextureExploreStrategy exploreStrategy = TextureExploreStrategy.Random;
            public TextureRefineMode refineMode = TextureRefineMode.Polish;
            public TextureComposeMode composeMode = TextureComposeMode.Unguided;
            public int manualComposeGuidedStep = 0;
            public TextureTileabilityMode tileabilityMode = TextureTileabilityMode.Off;
            public TextureSeamOverlayMode seamOverlayMode = TextureSeamOverlayMode.Off;
            public ProceduralTextureBlendLabSettings blendLab;
            public ProceduralTextureReferenceMatchSettings referenceMatch;
            public ProceduralTextureMapIntent mapIntent = ProceduralTextureMapIntent.Mask;
            public int selectedPresetIndex = 0;
            public string presetSearch = string.Empty;
            public string activeTextureProjectId = string.Empty;
        }

        private sealed class TextureHistorySnapshot
        {
            public string label;
            public string createdUtc;
            public ProceduralTextureCombinationSettings settings;
            public ProceduralTextureBaseSettings[] bases;
            public ProceduralTextureCandidate[] influences;
            public float[] values;
            public Color[] pixels;
            public Texture2D preview;
            public TextureDesignWorkflow workflow;
            public int generationNumber;
            public string parentSummary;
            public string inheritedTraits;
            public float mutationAmount;
            public float score01;
        }

        private sealed class TextureGenerationJob
        {
            public int slotIndex;
            public int seed;
            public bool activePreview;
            public bool rebuildExisting;
            public bool targetInfluence;
            public ProceduralTextureBaseSettings[] lockedInfluences;
        }

        private sealed class ManualLayerContributionEntry
        {
            public int index;
            public string label;
            public ProceduralTextureBlendMode blendMode;
            public float weight;
            public float coverage01;
            public float contrast01;
            public float contribution01;
            public Texture2D strip;
            public Texture2D highlight;
        }

        private readonly List<ProceduralTextureBaseSettings> _bases = new List<ProceduralTextureBaseSettings>();
        private readonly List<ProceduralTextureCandidate> _candidates = new List<ProceduralTextureCandidate>();
        private readonly List<ProceduralTextureCandidate> _influences = new List<ProceduralTextureCandidate>();
        private readonly List<TextureHistorySnapshot> _baseHistory = new List<TextureHistorySnapshot>();
        private readonly List<TextureHistorySnapshot> _combinationHistory = new List<TextureHistorySnapshot>();
        private readonly List<Texture2D> _basePreviewTextures = new List<Texture2D>();
        private readonly List<ManualLayerContributionEntry> _manualLayerContributions = new List<ManualLayerContributionEntry>();
        private readonly Dictionary<int, Rect> _lastBaseRects = new Dictionary<int, Rect>();
        private readonly HashSet<int> _selectedBases = new HashSet<int>();
        private readonly HashSet<int> _selectedCandidates = new HashSet<int>();
        private readonly Queue<TextureGenerationJob> _generationJobs = new Queue<TextureGenerationJob>();

        private ProceduralTextureCombinationSettings _combination = new ProceduralTextureCombinationSettings();
        private ProceduralTextureMapExportSettings _export = new ProceduralTextureMapExportSettings();
        private ProceduralTextureBlendLabSettings _blendLab = new ProceduralTextureBlendLabSettings();
        private ProceduralTextureReferenceMatchSettings _referenceMatch = new ProceduralTextureReferenceMatchSettings();
        private PungentColourPaletteSO _palette;
        private Texture2D _referenceTexture;
        private Texture2D _manualTextureLayerSource;
        private Texture2D _previewTexture;
        private Texture2D _referenceSourcePreview;
        private Texture2D _blendOutputPreview;
        private float[] _previewValues;
        private Color[] _previewPixels;
        private float[] _referenceValues;
        private ProceduralTextureFeaturePreview _referenceFeaturePreview;
        private readonly List<ProceduralTexturePresetDefinition> _texturePresets = new List<ProceduralTexturePresetDefinition>();
        private readonly List<Texture2D> _presetPreviewTextures = new List<Texture2D>();
        private readonly List<Texture2D> _mapPreviewTextures = new List<Texture2D>();
        private readonly List<ProceduralTextureMapType> _mapPreviewTypes = new List<ProceduralTextureMapType>();
        private Vector2 _candidateScroll;
        private Vector2 _influenceScroll;
        private Vector2 _presetScroll;
        private Vector2 _mapStripScroll;
        private Vector2 _historyWorkspaceScroll;
        private Vector2 _textureEditOverlayScroll;
        private Vector2 _inspectorScroll;
        private Vector2 _baseGridScroll;
        private Vector2 _manualLayerStackScroll;
        private Vector2 _historyScroll;
        private TextureInspectorTab _activeInspector = TextureInspectorTab.Compose;
        private TextureHistoryTab _historyTab = TextureHistoryTab.Combinations;
        private TextureDesignWorkflow _activeWorkflow = TextureDesignWorkflow.RandomExplore;
        private TextureDesignerPhase _activeDesignerPhase = TextureDesignerPhase.Explore;
        private TextureExploreStrategy _exploreStrategy = TextureExploreStrategy.Random;
        private TextureRefineMode _refineMode = TextureRefineMode.Polish;
        private TextureComposeMode _composeMode = TextureComposeMode.Unguided;
        private ManualComposeGuidedStep _manualComposeGuidedStep = ManualComposeGuidedStep.Shape;
        private TextureTileabilityMode _tileabilityMode = TextureTileabilityMode.Off;
        private TextureSeamOverlayMode _seamOverlayMode = TextureSeamOverlayMode.Off;
        private ProceduralTextureMapIntent _mapIntent = ProceduralTextureMapIntent.Mask;
        private int _selectedBase;
        private int _manualContributionHighlightIndex = -1;
        private int _manualContributionHoverIndex = -1;
        private int _selectedPresetIndex;
        private int _generationNumber;
        private int _selectionAnchor = -1;
        private int _dragBaseIndex = -1;
        private int _dragInsertIndex = -1;
        private int _dragBaseControl;
        private Vector2 _dragBaseStart;
        private bool _draggingBase;
        private bool _suppressWorkbenchInputThisEvent;
        private bool _inspectorInline;
        private bool _inspectorOpen = true;
        private bool _previewDirty = true;
        private bool _basePreviewsDirty = true;
        private bool _showViewPopup;
        private bool _showMetricsPopup;
        private bool _checkerboard = true;
        private int _candidateDensity;
        private int _activeCandidateIndex = -1;
        private int _textureEditOverlayIndex = -1;
        private Rect _textureEditOverlayAnchorRect;
        private Rect _textureEditOverlayRect;
        private int _generationTotal;
        private int _generationCompleted;
        private float _inspectorWidth = InspectorDefaultWidth;
        private float _manualOutputCoverage01;
        private float _manualOutputContrast01;
        private float _manualOutputToneMin01;
        private float _manualOutputToneMax01 = 1f;
        private float _manualOutputSeamScore01 = 1f;
        private double _sessionSaveRequestedAt = -1d;
        private double _manualComposeAutoPreviewAt = -1d;
        private string _lastStatus = "Ready";
        private string _manualComposeAutoPreviewReason = string.Empty;
        private string _lastSaveFolder = "Assets";
        private string _presetSearch = string.Empty;
        private string _assetProductionBridgeStatus = "Bridge not checked.";
        private bool _generationRunning;
        private bool _sessionSaveQueued;
        private bool _manualComposeAutoPreview = true;
        private bool _manualComposeAutoPreviewQueued;
        private bool _textureEditOverlayOpen;
        private bool _referenceFeaturePreviewDirty = true;
        private bool _presetPreviewsDirty = true;
        private bool _mapPreviewsDirty = true;
        private bool _foldoutBlend;
        private bool _foldoutPattern;
        private bool _foldoutDensity;
        private bool _foldoutStamp;
        private bool _foldoutPatternDetails;
        private bool _foldoutSource;
        private bool _manualFoldoutBlend = true;
        private bool _manualFoldoutPattern = true;
        private bool _manualFoldoutDensity;
        private bool _manualFoldoutStamp;
        private bool _manualFoldoutPatternDetails;
        private bool _manualFoldoutSource;

        public static void Open()
        {
            ProceduralTextureLabWindow window = GetWindow<ProceduralTextureLabWindow>();
            window.titleContent = new GUIContent("Texture Designer");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _inspectorWidth = UtilityWindowPrefs.GetFloat(PrefPrefix + "InspectorWidth", InspectorDefaultWidth);
            _activeInspector = (TextureInspectorTab)UtilityWindowPrefs.GetInt(PrefPrefix + "ActiveInspector", (int)TextureInspectorTab.Explore);
            _historyTab = (TextureHistoryTab)UtilityWindowPrefs.GetInt(PrefPrefix + "HistoryTab", (int)TextureHistoryTab.Combinations);
            _activeWorkflow = (TextureDesignWorkflow)UtilityWindowPrefs.GetInt(PrefPrefix + "ActiveWorkflow", (int)TextureDesignWorkflow.RandomExplore);
            _activeDesignerPhase = (TextureDesignerPhase)UtilityWindowPrefs.GetInt(PrefPrefix + "ActiveDesignerPhase", (int)TextureDesignerPhase.Explore);
            _exploreStrategy = (TextureExploreStrategy)UtilityWindowPrefs.GetInt(PrefPrefix + "ExploreStrategy", (int)TextureExploreStrategy.Random);
            _refineMode = (TextureRefineMode)UtilityWindowPrefs.GetInt(PrefPrefix + "RefineMode", (int)TextureRefineMode.Polish);
            _composeMode = (TextureComposeMode)UtilityWindowPrefs.GetInt(PrefPrefix + "ComposeMode", (int)TextureComposeMode.Unguided);
            _tileabilityMode = (TextureTileabilityMode)UtilityWindowPrefs.GetInt(PrefPrefix + "TileabilityMode", (int)TextureTileabilityMode.Off);
            _seamOverlayMode = (TextureSeamOverlayMode)UtilityWindowPrefs.GetInt(PrefPrefix + "SeamOverlayMode", (int)TextureSeamOverlayMode.Off);
            _lastSaveFolder = UtilityWindowPrefs.GetString(PrefPrefix + "LastSaveFolder", "Assets");
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
            LoadTextureLibrary();
            LoadSession();
            EnsureBaseBounds();
            SelectSingleBase(Mathf.Clamp(_selectedBase, 0, _bases.Count - 1));
            QueueMissingVariantPreviews();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            CancelGenerationJobs(false);
            SavePrefs();
            SaveSession();
            SaveTextureLibrary();
            DestroyPreviewState();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            EnsureBaseBounds();
            HandleKeyboard();
            HandleTextureEditOverlayInput();

            string status = _previewTexture == null
                ? _lastStatus
                : $"{_combination.width}x{_combination.height} / {_candidates.Count} texture(s) / {_selectedCandidates.Count} selected / {_lastStatus}";

            UtilityWindowTheme.Header(
                "Texture Designer",
                "Compose, guide, refine, and export procedural texture variants.",
                status);

            DrawToolbar();
            DrawWorkbench();
            DrawViewPopup();
            DrawMetricsPopup();

            if (GUI.changed)
            {
                SavePrefs();
                RequestSessionSave();
            }
        }

        private void EditorUpdate()
        {
            ProcessGenerationQueue();
            ProcessManualComposeAutoPreview();
            ProcessQueuedSessionSave();
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetFloat(PrefPrefix + "InspectorWidth", _inspectorWidth);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ActiveInspector", (int)_activeInspector);
            UtilityWindowPrefs.SetInt(PrefPrefix + "HistoryTab", (int)_historyTab);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ActiveWorkflow", (int)_activeWorkflow);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ActiveDesignerPhase", (int)_activeDesignerPhase);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ExploreStrategy", (int)_exploreStrategy);
            UtilityWindowPrefs.SetInt(PrefPrefix + "RefineMode", (int)_refineMode);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ComposeMode", (int)_composeMode);
            UtilityWindowPrefs.SetInt(PrefPrefix + "TileabilityMode", (int)_tileabilityMode);
            UtilityWindowPrefs.SetInt(PrefPrefix + "SeamOverlayMode", (int)_seamOverlayMode);
            UtilityWindowPrefs.SetString(PrefPrefix + "LastSaveFolder", _lastSaveFolder);
        }

        private void LoadSession()
        {
            string json = UtilityWindowPrefs.GetString(PrefSessionJson, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    TextureLabSessionState session = JsonUtility.FromJson<TextureLabSessionState>(json);
                    if (session != null)
                    {
                        if (session.combination != null)
                            _combination = session.combination;
                        if (session.export != null)
                            _export = session.export;
                        if (session.bases != null && session.bases.Length > 0)
                        {
                            _bases.Clear();
                            _bases.AddRange(ProceduralTextureCombinationUtility.CloneBases(session.bases));
                        }
                        RestoreSessionVariants(session.variants);
                        RestoreSessionInfluences(session.influences);
                        _selectedBase = Mathf.Max(0, session.selectedBase);
                        _candidateDensity = Mathf.Clamp(session.candidateDensity, 0, 2);
                        _checkerboard = session.checkerboard;
                        _combination.tilePreview = session.tilePreview;
                        _inspectorOpen = session.inspectorOpen;
                        _manualComposeAutoPreview = session.manualComposeAutoPreview;
                        _activeCandidateIndex = session.activeCandidateIndex;
                        _activeWorkflow = session.activeWorkflow;
                        _activeDesignerPhase = session.activeDesignerPhase;
                        _exploreStrategy = session.exploreStrategy;
                        _refineMode = session.refineMode;
                        _composeMode = session.composeMode;
                        _manualComposeGuidedStep = (ManualComposeGuidedStep)Mathf.Clamp(session.manualComposeGuidedStep, 0, 3);
                        _tileabilityMode = session.tileabilityMode;
                        _seamOverlayMode = session.seamOverlayMode;
                        if (session.blendLab != null)
                            _blendLab = session.blendLab;
                        if (session.referenceMatch != null)
                            _referenceMatch = session.referenceMatch;
                        _mapIntent = session.mapIntent;
                        _selectedPresetIndex = Mathf.Max(0, session.selectedPresetIndex);
                        _presetSearch = session.presetSearch ?? string.Empty;
                        _activeTextureProjectId = session.activeTextureProjectId ?? _activeTextureProjectId;
                        if (_tileabilityMode == TextureTileabilityMode.Off && session.tilePreview)
                            _tileabilityMode = TextureTileabilityMode.PreviewOnly;
                        SyncTileabilitySettings();
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Texture Designer session could not be restored: {exception.Message}");
                }
            }

            string palettePath = UtilityWindowPrefs.GetString(PrefPaletteAssetPath, string.Empty);
            if (!string.IsNullOrEmpty(palettePath))
                _palette = AssetDatabase.LoadAssetAtPath<PungentColourPaletteSO>(palettePath);
            if (!string.IsNullOrEmpty(_referenceMatch.referenceAssetPath))
                _referenceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(_referenceMatch.referenceAssetPath);

            if (_bases.Count == 0)
                _bases.Add(ProceduralTextureBaseSettings.CreateDefault(0));
            EnsureTexturePresets();
            DetectAssetProductionBridge();
            _combination.Clamp();
            _export.Clamp();
            _blendLab.Clamp();
            _referenceMatch.Clamp();
        }

        private void SyncTileabilitySettings()
        {
            if (_combination == null)
                return;
            _combination.tilePreview = _tileabilityMode == TextureTileabilityMode.PreviewOnly || _tileabilityMode == TextureTileabilityMode.GenerateSeamless;
            _combination.generateSeamless = _tileabilityMode == TextureTileabilityMode.GenerateSeamless;
            if (_combination.generateSeamless && !_combination.wrapStampsAcrossEdges && !_combination.toroidalSpacing)
            {
                _combination.wrapStampsAcrossEdges = true;
                _combination.toroidalSpacing = true;
            }
        }

        private void SaveSession()
        {
            var session = new TextureLabSessionState
            {
                combination = _combination != null ? _combination.Clone() : new ProceduralTextureCombinationSettings(),
                export = _export,
                bases = ProceduralTextureCombinationUtility.CloneBases(_bases),
                variants = CloneVariantSession(_candidates),
                influences = CloneVariantSession(_influences),
                selectedBase = _selectedBase,
                candidateDensity = _candidateDensity,
                checkerboard = _checkerboard,
                tilePreview = _combination != null && _combination.tilePreview,
                inspectorOpen = _inspectorOpen,
                manualComposeAutoPreview = _manualComposeAutoPreview,
                activeCandidateIndex = _activeCandidateIndex,
                activeWorkflow = _activeWorkflow,
                activeDesignerPhase = _activeDesignerPhase,
                exploreStrategy = _exploreStrategy,
                refineMode = _refineMode,
                composeMode = _composeMode,
                manualComposeGuidedStep = (int)_manualComposeGuidedStep,
                tileabilityMode = _tileabilityMode,
                seamOverlayMode = _seamOverlayMode,
                blendLab = _blendLab != null ? _blendLab.Clone() : new ProceduralTextureBlendLabSettings(),
                referenceMatch = _referenceMatch != null ? _referenceMatch.Clone() : new ProceduralTextureReferenceMatchSettings(),
                mapIntent = _mapIntent,
                selectedPresetIndex = _selectedPresetIndex,
                presetSearch = _presetSearch,
                activeTextureProjectId = _activeTextureProjectId
            };
            UtilityWindowPrefs.SetString(PrefSessionJson, JsonUtility.ToJson(session));
            UtilityWindowPrefs.SetString(PrefPaletteAssetPath, _palette != null ? AssetDatabase.GetAssetPath(_palette) : string.Empty);
            _sessionSaveQueued = false;
            _sessionSaveRequestedAt = -1d;
        }

        private void RequestSessionSave()
        {
            _sessionSaveQueued = true;
            _sessionSaveRequestedAt = EditorApplication.timeSinceStartup;
        }

        private void ProcessQueuedSessionSave()
        {
            if (!_sessionSaveQueued)
                return;
            if (EditorApplication.timeSinceStartup - _sessionSaveRequestedAt < SessionSaveDebounceSeconds)
                return;
            SaveSession();
        }
    }
#endif
}
