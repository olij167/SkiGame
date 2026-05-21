using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public sealed class ProceduralTextureLabWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.ProceduralTextureLab.";
        private const float PreviewMinHeight = 220f;
        private const float PreviewMaxHeight = 720f;

        private ProceduralTextureGenerationSettings _settings;
        private Texture2D _previewTexture;
        private readonly List<ProceduralTextureSite> _sites = new List<ProceduralTextureSite>();
        private Vector2 _scroll;
        private float _previewHeight;
        private bool _showOutput = true;
        private bool _showPattern = true;
        private bool _showStamp = true;
        private bool _showColour = true;
        private bool _showAdvanced = false;
        private string _lastStatus = "Ready";
        private string _lastSaveFolder = "Assets";

        [MenuItem("Tools/Utilities/Texture/Procedural Texture Lab")]
        public static void Open()
        {
            ProceduralTextureLabWindow window = GetWindow<ProceduralTextureLabWindow>();
            window.titleContent = new GUIContent("Procedural Texture Lab");
            window.minSize = new Vector2(720f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = ProceduralTextureGenerationSettings.CreateDefault();
            _previewHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "PreviewHeight", 340f);
            _showOutput = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowOutput", true);
            _showPattern = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowPattern", true);
            _showStamp = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowStamp", true);
            _showColour = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowColour", true);
            _showAdvanced = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowAdvanced", false);
            _lastSaveFolder = UtilityWindowPrefs.GetString(PrefPrefix + "LastSaveFolder", "Assets");
            LoadCompactPrefs();
            GeneratePreview();
        }

        private void OnDisable()
        {
            UtilityWindowPrefs.SetFloat(PrefPrefix + "PreviewHeight", _previewHeight);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowOutput", _showOutput);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowPattern", _showPattern);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowStamp", _showStamp);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowColour", _showColour);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowAdvanced", _showAdvanced);
            UtilityWindowPrefs.SetString(PrefPrefix + "LastSaveFolder", _lastSaveFolder);
            SaveCompactPrefs();
            ProceduralTextureGenerator.DestroyGeneratedTexture(_previewTexture);
            _previewTexture = null;
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            string status = _previewTexture == null ? _lastStatus : $"{_settings.width}×{_settings.height} • {_sites.Count} stamps • {_lastStatus}";
            UtilityWindowTheme.Header(
                "Procedural Texture Lab",
                "Generate tileable masks, scatter patterns, stamp textures, and grayscale/colour texture assets from reusable procedural placement modes.",
                status);

            DrawToolbar();
            DrawPreviewPanel();
            UtilityWindowTheme.VerticalResizeHandle(ref _previewHeight, PreviewMinHeight, Mathf.Max(PreviewMaxHeight, position.height - 220f), () => Repaint());

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            DrawOutputSection();
            DrawPatternSection();
            DrawStampSection();
            DrawColourSection();
            DrawAdvancedSection();
            if (EditorGUI.EndChangeCheck())
            {
                _settings.Clamp();
                _lastStatus = "Settings changed. Refresh preview to apply.";
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.12f, 0.07f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Refresh Preview", UtilityWindowTheme.Green, GUILayout.Width(128f)))
                        GeneratePreview();

                    if (UtilityWindowTheme.TintedButton("Randomize Seed", UtilityWindowTheme.Cyan, GUILayout.Width(116f)))
                    {
                        _settings.seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                        GeneratePreview();
                    }

                    GUI.enabled = _previewTexture != null;
                    if (UtilityWindowTheme.TintedButton("Save PNG", UtilityWindowTheme.Blue, GUILayout.Width(92f)))
                        SavePreviewPng();

                    if (UtilityWindowTheme.TintedButton("Copy Summary", UtilityWindowTheme.Purple, GUILayout.Width(112f)))
                        CopySummaryToClipboard();
                    GUI.enabled = true;

                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(_settings.pattern.ToString(), UtilityWindowTheme.Cyan);
                    UtilityWindowTheme.CountPill(_settings.stampShape.ToString(), UtilityWindowTheme.Amber);
                }

                EditorGUILayout.LabelField("This is a standalone utility derived from the old LevelGen stamp placement patterns. It does not depend on terrain, maps, regions, or gameplay code.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.16f, 0.08f, 8, 4), GUILayout.Height(_previewHeight)))
            {
                UtilityWindowTheme.SectionTitle("Preview", UtilityWindowTheme.Blue, _previewTexture == null ? "None" : $"{_previewTexture.width}×{_previewTexture.height}");
                Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawCheckerBackground(rect);

                if (_previewTexture != null)
                {
                    Rect fit = FitRect(rect, _previewTexture.width, _previewTexture.height);
                    GUI.DrawTexture(fit, _previewTexture, ScaleMode.ScaleToFit, true);
                    DrawPreviewBorder(fit);
                }
                else
                {
                    EditorGUI.LabelField(rect, "No preview generated.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawOutputSection()
        {
            _showOutput = EditorGUILayout.BeginFoldoutHeaderGroup(_showOutput, "Output");
            if (_showOutput)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.10f, 0.05f, 8, 4)))
                {
                    _settings.width = EditorGUILayout.IntField(new GUIContent("Width", "Output texture width in pixels."), _settings.width);
                    _settings.height = EditorGUILayout.IntField(new GUIContent("Height", "Output texture height in pixels."), _settings.height);
                    _settings.mipChain = EditorGUILayout.Toggle(new GUIContent("Mip Chain", "Generate mipmaps for the output texture."), _settings.mipChain);
                    _settings.linear = EditorGUILayout.Toggle(new GUIContent("Linear", "Create the texture in linear colour space."), _settings.linear);
                    _settings.seed = EditorGUILayout.IntField(new GUIContent("Seed", "Deterministic seed for site placement."), _settings.seed);
                    _settings.randomizeSeedOnGenerate = EditorGUILayout.Toggle(new GUIContent("Randomize On Generate", "Use a random seed each time the texture is generated."), _settings.randomizeSeedOnGenerate);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPatternSection()
        {
            _showPattern = EditorGUILayout.BeginFoldoutHeaderGroup(_showPattern, "Pattern Placement");
            if (_showPattern)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.10f, 0.05f, 8, 4)))
                {
                    _settings.pattern = (ProceduralTexturePattern)EditorGUILayout.EnumPopup(new GUIContent("Pattern", "Placement algorithm used to distribute stamps."), _settings.pattern);
                    _settings.count = EditorGUILayout.IntField(new GUIContent("Count", "Maximum number of generated stamp sites."), _settings.count);
                    _settings.minSpacing01 = EditorGUILayout.Slider(new GUIContent("Min Spacing", "Minimum normalized distance between stamp centers."), _settings.minSpacing01, 0f, 0.5f);
                    _settings.globalJitter01 = EditorGUILayout.Slider(new GUIContent("Global Jitter", "Small normalized jitter applied to ordered patterns like Halton, Hammersley, and Spiral."), _settings.globalJitter01, 0f, 1f);

                    switch (_settings.pattern)
                    {
                        case ProceduralTexturePattern.StratifiedJitterGrid:
                            _settings.cellsX = EditorGUILayout.IntField(new GUIContent("Cells X", "Number of grid columns."), _settings.cellsX);
                            _settings.cellsY = EditorGUILayout.IntField(new GUIContent("Cells Y", "Number of grid rows."), _settings.cellsY);
                            _settings.cellJitter01 = EditorGUILayout.Slider(new GUIContent("Cell Jitter", "How far points can wander within each grid cell."), _settings.cellJitter01, 0f, 1f);
                            break;
                        case ProceduralTexturePattern.HexGrid:
                            _settings.hexRadius01 = EditorGUILayout.Slider(new GUIContent("Hex Radius", "Normalized radius used to space hex points."), _settings.hexRadius01, 0.005f, 0.25f);
                            _settings.hexPointyTop = EditorGUILayout.Toggle(new GUIContent("Pointy Top", "Use pointy-top hex orientation. Disable for flat-top orientation."), _settings.hexPointyTop);
                            _settings.hexJitter01 = EditorGUILayout.Slider(new GUIContent("Hex Jitter", "How far each hex point can jitter from its ideal lattice position."), _settings.hexJitter01, 0f, 1f);
                            break;
                        case ProceduralTexturePattern.PoissonDisk:
                            _settings.poissonRadius01 = EditorGUILayout.Slider(new GUIContent("Poisson Radius", "Minimum normalized Poisson sample radius."), _settings.poissonRadius01, 0.005f, 0.5f);
                            _settings.poissonAttempts = EditorGUILayout.IntField(new GUIContent("Attempts", "Attempts per active Poisson sample before retiring it."), _settings.poissonAttempts);
                            _settings.poissonHardCap = EditorGUILayout.IntField(new GUIContent("Hard Cap", "Optional hard cap for generated points. 0 uses Count."), _settings.poissonHardCap);
                            break;
                        case ProceduralTexturePattern.DensityMap:
                            _settings.densityMap = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Density Map", "Readable texture used to bias point placement."), _settings.densityMap, typeof(Texture2D), false);
                            _settings.densityChannel = (ProceduralTextureChannel)EditorGUILayout.EnumPopup(new GUIContent("Density Channel", "Texture channel used for density lookup."), _settings.densityChannel);
                            _settings.densityCurve = EditorGUILayout.CurveField(new GUIContent("Density Curve", "Remaps sampled density before thresholding."), _settings.densityCurve);
                            _settings.densityTiling = EditorGUILayout.Vector2Field(new GUIContent("Density Tiling", "UV tiling for density sampling."), _settings.densityTiling);
                            _settings.densityOffset = EditorGUILayout.Vector2Field(new GUIContent("Density Offset", "UV offset for density sampling."), _settings.densityOffset);
                            _settings.densityThreshold01 = EditorGUILayout.Slider(new GUIContent("Threshold", "Minimum density required before a point can be accepted."), _settings.densityThreshold01, 0f, 1f);
                            EditorGUILayout.HelpBox("Density and source stamp textures must be readable if sampled on the CPU.", MessageType.Info);
                            break;
                        case ProceduralTexturePattern.PolarPattern:
                            _settings.polarRings = EditorGUILayout.IntField(new GUIContent("Rings", "Number of concentric rings."), _settings.polarRings);
                            _settings.polarSpokesPerRing = EditorGUILayout.IntField(new GUIContent("Spokes Per Ring", "Number of stamps attempted per ring."), _settings.polarSpokesPerRing);
                            _settings.polarRingStep01 = EditorGUILayout.Slider(new GUIContent("Ring Step", "Normalized distance between rings."), _settings.polarRingStep01, 0.005f, 0.5f);
                            _settings.polarRadialJitter01 = EditorGUILayout.Slider(new GUIContent("Radial Jitter", "Radius variation per polar point."), _settings.polarRadialJitter01, 0f, 1f);
                            _settings.polarAngularJitter01 = EditorGUILayout.Slider(new GUIContent("Angular Jitter", "Angle variation per polar point."), _settings.polarAngularJitter01, 0f, 1f);
                            break;
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawStampSection()
        {
            _showStamp = EditorGUILayout.BeginFoldoutHeaderGroup(_showStamp, "Stamp");
            if (_showStamp)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.10f, 0.05f, 8, 4)))
                {
                    _settings.stampShape = (ProceduralTextureStampShape)EditorGUILayout.EnumPopup(new GUIContent("Shape", "Shape drawn at every generated site."), _settings.stampShape);
                    _settings.blendMode = (ProceduralTextureBlendMode)EditorGUILayout.EnumPopup(new GUIContent("Blend Mode", "How each stamp contributes to the existing texture value."), _settings.blendMode);
                    _settings.radiusMin01 = EditorGUILayout.Slider(new GUIContent("Radius Min", "Minimum normalized stamp radius."), _settings.radiusMin01, 0.001f, 0.5f);
                    _settings.radiusMax01 = EditorGUILayout.Slider(new GUIContent("Radius Max", "Maximum normalized stamp radius."), _settings.radiusMax01, 0.001f, 0.5f);
                    _settings.intensityMin = EditorGUILayout.Slider(new GUIContent("Intensity Min", "Minimum stamp contribution."), _settings.intensityMin, 0f, 2f);
                    _settings.intensityMax = EditorGUILayout.Slider(new GUIContent("Intensity Max", "Maximum stamp contribution."), _settings.intensityMax, 0f, 2f);
                    _settings.contrast = EditorGUILayout.Slider(new GUIContent("Contrast", "Exponent applied to each stamp contribution."), _settings.contrast, 0.1f, 8f);
                    _settings.edgeFalloff = EditorGUILayout.Slider(new GUIContent("Edge Falloff", "Blends stamp shape toward zero at the radius edge."), _settings.edgeFalloff, 0f, 1f);
                    _settings.rotationJitterDegrees = EditorGUILayout.Vector2Field(new GUIContent("Rotation Jitter", "Random rotation range for each stamp in degrees."), _settings.rotationJitterDegrees);

                    if (_settings.stampShape == ProceduralTextureStampShape.Ring)
                        _settings.ringThickness01 = EditorGUILayout.Slider(new GUIContent("Ring Thickness", "Normalized thickness of the generated ring."), _settings.ringThickness01, 0f, 1f);

                    if (_settings.stampShape == ProceduralTextureStampShape.TextureSource)
                    {
                        _settings.stampTexture = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Stamp Texture", "Readable texture sampled inside each stamp radius."), _settings.stampTexture, typeof(Texture2D), false);
                        _settings.stampTextureChannel = (ProceduralTextureChannel)EditorGUILayout.EnumPopup(new GUIContent("Stamp Channel", "Texture channel used as stamp intensity."), _settings.stampTextureChannel);
                        _settings.invertStampTexture = EditorGUILayout.Toggle(new GUIContent("Invert", "Invert the sampled stamp texture value."), _settings.invertStampTexture);
                        _settings.stampTextureTiling = EditorGUILayout.Slider(new GUIContent("Stamp Tiling", "How many times the source texture repeats inside the stamp radius."), _settings.stampTextureTiling, 0.05f, 8f);
                        EditorGUILayout.HelpBox("Texture source stamps require a readable source texture.", MessageType.Info);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawColourSection()
        {
            _showColour = EditorGUILayout.BeginFoldoutHeaderGroup(_showColour, "Colour Output");
            if (_showColour)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.10f, 0.05f, 8, 4)))
                {
                    _settings.colorMode = (ProceduralTextureColorMode)EditorGUILayout.EnumPopup(new GUIContent("Mode", "How normalized texture values are converted into colours."), _settings.colorMode);
                    _settings.backgroundValue = EditorGUILayout.Slider(new GUIContent("Background Value", "Initial value before stamps are applied."), _settings.backgroundValue, 0f, 1f);
                    _settings.outputOpacity = EditorGUILayout.Slider(new GUIContent("Opacity", "Alpha multiplier for generated pixels."), _settings.outputOpacity, 0f, 1f);

                    if (_settings.colorMode == ProceduralTextureColorMode.ForegroundBackground)
                    {
                        _settings.backgroundColor = EditorGUILayout.ColorField(new GUIContent("Background", "Colour used when the texture value is 0."), _settings.backgroundColor);
                        _settings.foregroundColor = EditorGUILayout.ColorField(new GUIContent("Foreground", "Colour used when the texture value is 1."), _settings.foregroundColor);
                    }
                    else if (_settings.colorMode == ProceduralTextureColorMode.Gradient)
                    {
                        _settings.gradient = EditorGUILayout.GradientField(new GUIContent("Gradient", "Gradient used to colour normalized texture values."), _settings.gradient);
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAdvancedSection()
        {
            _showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(_showAdvanced, "Notes / Future Extensions");
            if (_showAdvanced)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 8, 4)))
                {
                    EditorGUILayout.HelpBox(
                        "This utility intentionally excludes Voronoi/region masks for now. Region-aware generation should be integrated later with the existing map-region systems instead of being duplicated here.",
                        MessageType.Info);
                    EditorGUILayout.HelpBox(
                        "Good follow-up additions: normal-map output, channel packing, tile seam preview, material preview, and using generated points to scatter scene prefabs through a separate placement utility.",
                        MessageType.None);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void GeneratePreview()
        {
            ProceduralTextureGenerator.DestroyGeneratedTexture(_previewTexture);
            _settings.Clamp();
            _previewTexture = ProceduralTextureGenerator.GenerateTexture(_settings, _sites);
            _lastStatus = "Preview refreshed.";
            Repaint();
        }

        private void SavePreviewPng()
        {
            if (_previewTexture == null)
            {
                _lastStatus = "Generate a preview first.";
                return;
            }

            string folder = Directory.Exists(_lastSaveFolder) ? _lastSaveFolder : "Assets";
            string path = EditorUtility.SaveFilePanelInProject("Save Generated Texture", "Generated_ProceduralTexture", "png", "Choose where to save the generated PNG.", folder);
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllBytes(path, _previewTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = _settings.mipChain;
                importer.sRGBTexture = !_settings.linear;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }

            _lastSaveFolder = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            _lastStatus = $"Saved {Path.GetFileName(path)}.";
        }

        private void CopySummaryToClipboard()
        {
            EditorGUIUtility.systemCopyBuffer =
                $"Procedural Texture Lab\n" +
                $"Pattern: {_settings.pattern}\n" +
                $"Stamp: {_settings.stampShape}\n" +
                $"Size: {_settings.width}x{_settings.height}\n" +
                $"Seed: {_settings.seed}\n" +
                $"Sites: {_sites.Count}";
            _lastStatus = "Summary copied.";
        }

        private void LoadCompactPrefs()
        {
            _settings.width = UtilityWindowPrefs.GetInt(PrefPrefix + "Width", _settings.width);
            _settings.height = UtilityWindowPrefs.GetInt(PrefPrefix + "Height", _settings.height);
            _settings.seed = UtilityWindowPrefs.GetInt(PrefPrefix + "Seed", _settings.seed);
            _settings.count = UtilityWindowPrefs.GetInt(PrefPrefix + "Count", _settings.count);
            _settings.pattern = (ProceduralTexturePattern)UtilityWindowPrefs.GetInt(PrefPrefix + "Pattern", (int)_settings.pattern);
            _settings.stampShape = (ProceduralTextureStampShape)UtilityWindowPrefs.GetInt(PrefPrefix + "StampShape", (int)_settings.stampShape);
            _settings.blendMode = (ProceduralTextureBlendMode)UtilityWindowPrefs.GetInt(PrefPrefix + "BlendMode", (int)_settings.blendMode);
            _settings.minSpacing01 = UtilityWindowPrefs.GetFloat(PrefPrefix + "MinSpacing", _settings.minSpacing01);
            _settings.radiusMin01 = UtilityWindowPrefs.GetFloat(PrefPrefix + "RadiusMin", _settings.radiusMin01);
            _settings.radiusMax01 = UtilityWindowPrefs.GetFloat(PrefPrefix + "RadiusMax", _settings.radiusMax01);
            _settings.intensityMin = UtilityWindowPrefs.GetFloat(PrefPrefix + "IntensityMin", _settings.intensityMin);
            _settings.intensityMax = UtilityWindowPrefs.GetFloat(PrefPrefix + "IntensityMax", _settings.intensityMax);
            _settings.colorMode = (ProceduralTextureColorMode)UtilityWindowPrefs.GetInt(PrefPrefix + "ColorMode", (int)_settings.colorMode);
            _settings.backgroundColor = UtilityWindowPrefs.GetColor(PrefPrefix + "BackgroundColor", _settings.backgroundColor);
            _settings.foregroundColor = UtilityWindowPrefs.GetColor(PrefPrefix + "ForegroundColor", _settings.foregroundColor);
            _settings.Clamp();
        }

        private void SaveCompactPrefs()
        {
            UtilityWindowPrefs.SetInt(PrefPrefix + "Width", _settings.width);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Height", _settings.height);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Seed", _settings.seed);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Count", _settings.count);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Pattern", (int)_settings.pattern);
            UtilityWindowPrefs.SetInt(PrefPrefix + "StampShape", (int)_settings.stampShape);
            UtilityWindowPrefs.SetInt(PrefPrefix + "BlendMode", (int)_settings.blendMode);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "MinSpacing", _settings.minSpacing01);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "RadiusMin", _settings.radiusMin01);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "RadiusMax", _settings.radiusMax01);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "IntensityMin", _settings.intensityMin);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "IntensityMax", _settings.intensityMax);
            UtilityWindowPrefs.SetInt(PrefPrefix + "ColorMode", (int)_settings.colorMode);
            UtilityWindowPrefs.SetColor(PrefPrefix + "BackgroundColor", _settings.backgroundColor);
            UtilityWindowPrefs.SetColor(PrefPrefix + "ForegroundColor", _settings.foregroundColor);
        }

        private static Rect FitRect(Rect container, float sourceWidth, float sourceHeight)
        {
            float sourceAspect = sourceWidth / Mathf.Max(1f, sourceHeight);
            float containerAspect = container.width / Mathf.Max(1f, container.height);
            if (containerAspect > sourceAspect)
            {
                float width = container.height * sourceAspect;
                return new Rect(container.center.x - width * 0.5f, container.y, width, container.height);
            }

            float height = container.width / sourceAspect;
            return new Rect(container.x, container.center.y - height * 0.5f, container.width, height);
        }

        private static void DrawCheckerBackground(Rect rect)
        {
            Color a = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.78f, 0.78f, 0.78f);
            Color b = EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.88f, 0.88f, 0.88f);
            const float size = 16f;
            int rows = Mathf.CeilToInt(rect.height / size);
            int cols = Mathf.CeilToInt(rect.width / size);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Rect tile = new Rect(rect.x + x * size, rect.y + y * size, size, size);
                    EditorGUI.DrawRect(tile, ((x + y) & 1) == 0 ? a : b);
                }
            }
        }

        private static void DrawPreviewBorder(Rect rect)
        {
            Color line = UtilityWindowTheme.ResizeHandleTint;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), line);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);
        }
    }
    #endif

}