using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public enum TextureArraySourceMode
    {
        TextureAssets,
        MaterialAlbedo,
        MaterialNormal,
        MaterialCustomProperty
    }

    public sealed class TextureArrayBakerWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.TextureArrayBaker.";
        private const float SourcePanelMinHeight = 160f;
        private const float SourcePanelMaxHeight = 520f;

        private TextureArraySourceMode _sourceMode = TextureArraySourceMode.TextureAssets;
        private readonly List<Object> _sources = new List<Object>();
        private Vector2 _scroll;
        private float _sourcePanelHeight = 260f;

        private int _width = 1024;
        private int _height = 1024;
        private bool _sRgb = true;
        private bool _mipChain = true;
        private TextureWrapMode _wrapMode = TextureWrapMode.Repeat;
        private FilterMode _filterMode = FilterMode.Bilinear;
        private int _anisoLevel = 2;
        private TextureArrayFallbackMode _fallbackMode = TextureArrayFallbackMode.White;
        private string _customProperties = "_BaseMap, _MainTex";
        private string _assetPath = "Assets/Generated_TextureArray.asset";
        private string _lastStatus = "Ready";
        private Texture2DArray _lastResult;
        private int _selectedPreviewLayer;
        private Texture2D _previewLayerTexture;
        private bool _showBakeSettings = true;
        private bool _showSources = true;
        private bool _showPreview = true;
        private bool _showHelp = false;

        [MenuItem("Tools/Utilities/Texture/Texture Array Baker")]
        public static void Open()
        {
            TextureArrayBakerWindow window = GetWindow<TextureArrayBakerWindow>();
            window.titleContent = new GUIContent("Texture Array Baker");
            window.minSize = new Vector2(740f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _width = UtilityWindowPrefs.GetInt(PrefPrefix + "Width", _width);
            _height = UtilityWindowPrefs.GetInt(PrefPrefix + "Height", _height);
            _sRgb = UtilityWindowPrefs.GetBool(PrefPrefix + "SRgb", _sRgb);
            _mipChain = UtilityWindowPrefs.GetBool(PrefPrefix + "MipChain", _mipChain);
            _sourceMode = (TextureArraySourceMode)UtilityWindowPrefs.GetInt(PrefPrefix + "SourceMode", (int)_sourceMode);
            _fallbackMode = (TextureArrayFallbackMode)UtilityWindowPrefs.GetInt(PrefPrefix + "FallbackMode", (int)_fallbackMode);
            _customProperties = UtilityWindowPrefs.GetString(PrefPrefix + "CustomProperties", _customProperties);
            _assetPath = UtilityWindowPrefs.GetString(PrefPrefix + "AssetPath", _assetPath);
            _sourcePanelHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "SourcePanelHeight", _sourcePanelHeight);
            _showBakeSettings = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowBakeSettings", true);
            _showSources = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowSources", true);
            _showPreview = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowPreview", true);
            _showHelp = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowHelp", false);
        }

        private void OnDisable()
        {
            UtilityWindowPrefs.SetInt(PrefPrefix + "Width", _width);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Height", _height);
            UtilityWindowPrefs.SetBool(PrefPrefix + "SRgb", _sRgb);
            UtilityWindowPrefs.SetBool(PrefPrefix + "MipChain", _mipChain);
            UtilityWindowPrefs.SetInt(PrefPrefix + "SourceMode", (int)_sourceMode);
            UtilityWindowPrefs.SetInt(PrefPrefix + "FallbackMode", (int)_fallbackMode);
            UtilityWindowPrefs.SetString(PrefPrefix + "CustomProperties", _customProperties);
            UtilityWindowPrefs.SetString(PrefPrefix + "AssetPath", _assetPath);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "SourcePanelHeight", _sourcePanelHeight);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowBakeSettings", _showBakeSettings);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowSources", _showSources);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowPreview", _showPreview);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowHelp", _showHelp);
            DestroyPreviewLayer();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            UtilityWindowTheme.Header(
                "Texture Array Baker",
                "Bake selected textures or material texture properties into Texture2DArray assets without depending on any terrain or LevelGen runtime code.",
                _lastResult == null ? _lastStatus : $"{_lastResult.width}×{_lastResult.height}×{_lastResult.depth} • {_lastStatus}");

            DrawToolbar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawBakeSettings();
            DrawSources();
            DrawPreview();
            DrawHelp();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.12f, 0.07f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Add Selection", UtilityWindowTheme.Green, GUILayout.Width(116f)))
                        AddSelection();

                    if (UtilityWindowTheme.TintedButton("Clear Sources", UtilityWindowTheme.Amber, GUILayout.Width(112f)))
                    {
                        _sources.Clear();
                        _lastStatus = "Sources cleared.";
                    }

                    GUI.enabled = _sources.Count > 0;
                    if (UtilityWindowTheme.TintedButton("Bake Array", UtilityWindowTheme.Blue, GUILayout.Width(104f)))
                        Bake();
                    GUI.enabled = true;

                    if (UtilityWindowTheme.TintedButton("Choose Path", UtilityWindowTheme.Purple, GUILayout.Width(104f)))
                        ChooseAssetPath();

                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"{_sources.Count} sources", UtilityWindowTheme.Cyan);
                }

                EditorGUILayout.LabelField(_assetPath, UtilityWindowTheme.PathLabelStyle);
            }
        }

        private void DrawBakeSettings()
        {
            _showBakeSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showBakeSettings, "Bake Settings");
            if (_showBakeSettings)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan, 0.10f, 0.05f, 8, 4)))
                {
                    _sourceMode = (TextureArraySourceMode)EditorGUILayout.EnumPopup(new GUIContent("Source Mode", "Textures are used directly, or extracted from material properties."), _sourceMode);
                    _width = EditorGUILayout.IntField(new GUIContent("Width", "Output slice width."), _width);
                    _height = EditorGUILayout.IntField(new GUIContent("Height", "Output slice height."), _height);
                    _sRgb = EditorGUILayout.Toggle(new GUIContent("sRGB", "Use sRGB texture sampling for albedo-like arrays. Disable for normal/data arrays."), _sRgb);
                    _mipChain = EditorGUILayout.Toggle(new GUIContent("Mip Chain", "Generate mipmaps for the output array."), _mipChain);
                    _wrapMode = (TextureWrapMode)EditorGUILayout.EnumPopup(new GUIContent("Wrap Mode", "Wrap mode assigned to the Texture2DArray asset."), _wrapMode);
                    _filterMode = (FilterMode)EditorGUILayout.EnumPopup(new GUIContent("Filter Mode", "Filter mode assigned to the Texture2DArray asset."), _filterMode);
                    _anisoLevel = EditorGUILayout.IntSlider(new GUIContent("Aniso Level", "Anisotropic filtering level."), _anisoLevel, 0, 16);
                    _fallbackMode = (TextureArrayFallbackMode)EditorGUILayout.EnumPopup(new GUIContent("Missing Fallback", "Slice generated when a texture source is missing."), _fallbackMode);

                    if (_sourceMode == TextureArraySourceMode.MaterialCustomProperty)
                        _customProperties = EditorGUILayout.TextField(new GUIContent("Properties", "Comma/space separated material texture property names, searched in order."), _customProperties);

                    if (_sourceMode == TextureArraySourceMode.MaterialAlbedo)
                        EditorGUILayout.HelpBox("Searches common albedo properties: _BaseMap, _MainTex, _BaseColorMap, _Albedo, _Diffuse.", MessageType.None);
                    if (_sourceMode == TextureArraySourceMode.MaterialNormal)
                        EditorGUILayout.HelpBox("Searches common normal properties and defaults to FlatNormal fallback if missing.", MessageType.None);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSources()
        {
            _showSources = EditorGUILayout.BeginFoldoutHeaderGroup(_showSources, "Sources");
            if (_showSources)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.12f, 0.06f, 8, 4)))
                {
                    Rect dropRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(40f), GUILayout.ExpandWidth(true));
                    DrawDropArea(dropRect);
                    HandleDropArea(dropRect);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Source List", UtilityWindowTheme.SectionHeaderStyle);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("+", GUILayout.Width(28f)))
                            _sources.Add(null);
                    }

                    float startHeight = _sourcePanelHeight;
                    using (new EditorGUILayout.VerticalScope(GUILayout.Height(_sourcePanelHeight)))
                    {
                        for (int i = 0; i < _sources.Count; i++)
                            DrawSourceRow(i);
                    }
                    UtilityWindowTheme.VerticalResizeHandle(ref _sourcePanelHeight, SourcePanelMinHeight, SourcePanelMaxHeight, () => Repaint(), "Drag to resize source list");
                    if (!Mathf.Approximately(startHeight, _sourcePanelHeight))
                        Repaint();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSourceRow(int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Object previous = _sources[index];
                Object next = EditorGUILayout.ObjectField(previous, ExpectedSourceType(), false);
                if (next != previous)
                    _sources[index] = next;

                if (GUILayout.Button("Ping", GUILayout.Width(44f)) && _sources[index] != null)
                    EditorGUIUtility.PingObject(_sources[index]);

                if (GUILayout.Button("×", GUILayout.Width(26f)))
                {
                    _sources.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawPreview()
        {
            _showPreview = EditorGUILayout.BeginFoldoutHeaderGroup(_showPreview, "Last Result Preview");
            if (_showPreview)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.10f, 0.05f, 8, 4)))
                {
                    if (_lastResult == null)
                    {
                        EditorGUILayout.HelpBox("Bake an array to preview its first layer and ping the generated asset.", MessageType.Info);
                    }
                    else
                    {
                        int maxLayer = Mathf.Max(0, _lastResult.depth - 1);
                        EditorGUI.BeginChangeCheck();
                        _selectedPreviewLayer = EditorGUILayout.IntSlider(new GUIContent("Layer", "Preview layer index."), Mathf.Clamp(_selectedPreviewLayer, 0, maxLayer), 0, maxLayer);
                        if (EditorGUI.EndChangeCheck())
                            RebuildPreviewLayer();

                        if (_previewLayerTexture == null)
                            RebuildPreviewLayer();

                        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(240f), GUILayout.ExpandWidth(true));
                        if (_previewLayerTexture != null)
                        {
                            GUI.DrawTexture(rect, _previewLayerTexture, ScaleMode.ScaleToFit, true);
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (UtilityWindowTheme.TintedButton("Ping Asset", UtilityWindowTheme.Cyan, GUILayout.Width(96f)))
                                EditorGUIUtility.PingObject(_lastResult);
                            if (UtilityWindowTheme.TintedButton("Select Asset", UtilityWindowTheme.Blue, GUILayout.Width(96f)))
                                Selection.activeObject = _lastResult;
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawHelp()
        {
            _showHelp = EditorGUILayout.BeginFoldoutHeaderGroup(_showHelp, "Help / Notes");
            if (_showHelp)
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.10f, 0.05f, 8, 4)))
                {
                    EditorGUILayout.HelpBox("Use Texture Assets when you already have ordered slices. Use Material modes to extract common properties from a material list while preserving source order.", MessageType.Info);
                    EditorGUILayout.HelpBox("This utility intentionally does not create terrain-specific region materials. Project-specific adapters can gather terrain/material sources and call TextureArrayBakerUtility.BakeTextureArray().", MessageType.None);
                    EditorGUILayout.HelpBox("For normal maps, set Source Mode to MaterialNormal, disable sRGB, and use FlatNormal fallback.", MessageType.None);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void AddSelection()
        {
            Object[] selection = Selection.objects;
            int added = 0;
            for (int i = 0; i < selection.Length; i++)
            {
                Object obj = selection[i];
                if (obj == null)
                    continue;
                if (!ExpectedSourceType().IsInstanceOfType(obj))
                    continue;
                if (_sources.Contains(obj))
                    continue;
                _sources.Add(obj);
                added++;
            }
            _lastStatus = added == 0 ? "No compatible selected assets were added." : $"Added {added} selected source(s).";
        }

        private void Bake()
        {
            _width = Mathf.Max(4, _width);
            _height = Mathf.Max(4, _height);
            if (string.IsNullOrWhiteSpace(_assetPath) || !_assetPath.StartsWith("Assets/"))
            {
                _lastStatus = "Choose an asset path inside Assets.";
                return;
            }

            List<Texture2D> textures = ResolveTextures();
            if (textures.Count == 0)
            {
                _lastStatus = "No textures resolved from sources.";
                return;
            }

            TextureArrayFallbackMode fallback = _sourceMode == TextureArraySourceMode.MaterialNormal ? TextureArrayFallbackMode.FlatNormal : _fallbackMode;
            try
            {
                TextureArrayBakeResult result = TextureArrayBakerUtility.BakeTextureArray(textures, _assetPath, _width, _height, _sRgb, _mipChain, fallback, _wrapMode, _filterMode, _anisoLevel);
                _lastResult = result.TextureArray;
                _selectedPreviewLayer = 0;
                RebuildPreviewLayer();
                _lastStatus = $"Baked {result.SourceCount} slice(s), {result.MissingCount} fallback(s).";
                if (_lastResult != null)
                    EditorGUIUtility.PingObject(_lastResult);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                _lastStatus = "Bake failed. See Console.";
            }
        }

        private List<Texture2D> ResolveTextures()
        {
            var textures = new List<Texture2D>();
            if (_sourceMode == TextureArraySourceMode.TextureAssets)
            {
                for (int i = 0; i < _sources.Count; i++)
                    textures.Add(_sources[i] as Texture2D);
                return textures;
            }

            var materials = new List<Material>();
            for (int i = 0; i < _sources.Count; i++)
                materials.Add(_sources[i] as Material);

            string propertyList = _customProperties;
            string[] fallbackProperties = TextureArrayBakerUtility.DefaultAlbedoProperties;
            if (_sourceMode == TextureArraySourceMode.MaterialNormal)
            {
                propertyList = null;
                fallbackProperties = TextureArrayBakerUtility.DefaultNormalProperties;
            }
            else if (_sourceMode == TextureArraySourceMode.MaterialAlbedo)
            {
                propertyList = null;
                fallbackProperties = TextureArrayBakerUtility.DefaultAlbedoProperties;
            }

            return TextureArrayBakerUtility.GatherTexturesFromMaterials(materials, propertyList, fallbackProperties);
        }

        private void ChooseAssetPath()
        {
            string folder = "Assets";
            string file = "Generated_TextureArray.asset";
            if (!string.IsNullOrWhiteSpace(_assetPath))
            {
                folder = Path.GetDirectoryName(_assetPath)?.Replace("\\", "/") ?? folder;
                file = Path.GetFileName(_assetPath);
            }

            string path = EditorUtility.SaveFilePanelInProject("Save Texture2DArray", Path.GetFileNameWithoutExtension(file), "asset", "Choose where to save the Texture2DArray asset.", folder);
            if (!string.IsNullOrWhiteSpace(path))
                _assetPath = path.Replace("\\", "/");
        }

        private void RebuildPreviewLayer()
        {
            DestroyPreviewLayer();
            if (_lastResult == null || _lastResult.depth <= 0)
                return;

            int layer = Mathf.Clamp(_selectedPreviewLayer, 0, _lastResult.depth - 1);
            var texture = new Texture2D(_lastResult.width, _lastResult.height, TextureFormat.RGBA32, false, !_sRgb)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = $"PreviewLayer_{layer}"
            };
            try
            {
                texture.SetPixels(_lastResult.GetPixels(layer, 0));
                texture.Apply(false, false);
                _previewLayerTexture = texture;
            }
            catch
            {
                Object.DestroyImmediate(texture);
                _previewLayerTexture = null;
            }
        }

        private void DestroyPreviewLayer()
        {
            if (_previewLayerTexture != null)
                Object.DestroyImmediate(_previewLayerTexture);
            _previewLayerTexture = null;
        }

        private System.Type ExpectedSourceType()
        {
            return _sourceMode == TextureArraySourceMode.TextureAssets ? typeof(Texture2D) : typeof(Material);
        }

        private void DrawDropArea(Rect rect)
        {
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.15f, 0.17f) : new Color(0.82f, 0.86f, 0.90f));
            GUI.Label(rect, $"Drag {ExpectedSourceType().Name} assets here", UtilityWindowTheme.CountPillStyle);
        }

        private void HandleDropArea(Rect rect)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    int added = 0;
                    Object[] dragged = DragAndDrop.objectReferences;
                    for (int i = 0; i < dragged.Length; i++)
                    {
                        Object obj = dragged[i];
                        if (obj == null || !ExpectedSourceType().IsInstanceOfType(obj) || _sources.Contains(obj))
                            continue;
                        _sources.Add(obj);
                        added++;
                    }
                    _lastStatus = added == 0 ? "No compatible dragged sources were added." : $"Added {added} dragged source(s).";
                }
                evt.Use();
            }
        }
    }
    #endif

}