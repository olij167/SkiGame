using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow
    {
        private string BlendSourceLabel()
        {
            return HasBlendSources() ? "A/B ready" : "pick A/B";
        }

        private bool HasBlendSources()
        {
            return ResolveBlendSource(false) != null && ResolveBlendSource(true) != null;
        }

        private ProceduralTextureCandidate ResolveBlendSource(bool sourceB)
        {
            int index = sourceB ? _blendLab.sourceBIndex : _blendLab.sourceAIndex;
            bool guide = sourceB ? _blendLab.sourceBIsGuide : _blendLab.sourceAIsGuide;
            List<ProceduralTextureCandidate> source = guide ? _influences : _candidates;
            return index >= 0 && index < source.Count ? source[index] : null;
        }

        private string BlendSourceName(bool sourceB)
        {
            ProceduralTextureCandidate candidate = ResolveBlendSource(sourceB);
            return candidate != null ? candidate.label : "-";
        }

        private void PickBlendSource(int index, bool sourceB)
        {
            if (index < 0 || index >= _candidates.Count)
                return;
            if (sourceB)
            {
                _blendLab.sourceBIndex = index;
                _blendLab.sourceBIsGuide = false;
            }
            else
            {
                _blendLab.sourceAIndex = index;
                _blendLab.sourceAIsGuide = false;
            }
            _activeWorkflow = TextureDesignWorkflow.BlendLab;
            _activeInspector = TextureInspectorTab.Compose;
            RebuildBlendOutputPreview();
            RequestSessionSave();
            Repaint();
        }

        private void PickBlendSourcesFromSelection()
        {
            int[] selected = _selectedCandidates.OrderBy(index => index).ToArray();
            if (selected.Length > 0)
                PickBlendSource(selected[0], false);
            if (selected.Length > 1)
                PickBlendSource(selected[1], true);
            _lastStatus = selected.Length >= 2 ? "Picked selected textures as Blend A/B." : "Select two grid textures to pick A/B quickly.";
        }

        private void DrawBlendSourcePicker(string label, bool sourceB)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(72f));
                ProceduralTextureCandidate candidate = ResolveBlendSource(sourceB);
                EditorGUILayout.LabelField(candidate != null ? candidate.label : "None", UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUI.DisabledScope(_selectedCandidates.Count == 0))
                {
                    if (GUILayout.Button("Selected", EditorStyles.miniButton, GUILayout.Width(72f)))
                        PickBlendSource(FirstSelectedVariantIndex(), sourceB);
                }
            }
        }

        private void GenerateBlendCandidates()
        {
            ProceduralTextureCandidate a = ResolveBlendSource(false);
            ProceduralTextureCandidate b = ResolveBlendSource(true);
            if (a == null || b == null)
            {
                _lastStatus = "Pick Texture A and Texture B before generating blends.";
                Repaint();
                return;
            }

            ProceduralTextureBaseSettings[] guides = BuildBlendInfluenceBases(a, b);
            GenerateCandidates(guides, "Generating blends");
        }

        private ProceduralTextureBaseSettings[] BuildBlendInfluenceBases(ProceduralTextureCandidate a, ProceduralTextureCandidate b)
        {
            float aWeight = Mathf.Clamp01(1f - AverageBlendFromB());
            float bWeight = Mathf.Clamp01(AverageBlendFromB());
            var result = new List<ProceduralTextureBaseSettings>();
            ProceduralTextureBaseSettings aBase = CreateInfluenceBaseFromCandidate(a, 0, Mathf.Lerp(0.2f, 1f, aWeight));
            ProceduralTextureBaseSettings bBase = CreateInfluenceBaseFromCandidate(b, 1, Mathf.Lerp(0.2f, 1f, bWeight));
            if (aBase != null)
            {
                aBase.preserveStructure = _blendLab.structureFromB01 < 0.45f;
                aBase.preserveDensity = _blendLab.densityFromB01 < 0.45f;
                aBase.preserveScale = _blendLab.scaleFromB01 < 0.45f;
                aBase.preserveDetail = _blendLab.detailFromB01 < 0.45f;
                aBase.preserveTone = _blendLab.toneFromB01 < 0.45f;
                aBase.preserveColourOrPalette = _blendLab.colourFromB01 < 0.45f;
                aBase.preserveBlendOrder = _blendLab.blendOrderFromB01 < 0.45f;
                aBase.preserveSeam = _blendLab.seamFromB01 < 0.45f;
                result.Add(aBase);
            }
            if (bBase != null)
            {
                bBase.blendMode = ProceduralTextureBlendMode.Overlay;
                bBase.preserveStructure = _blendLab.structureFromB01 > 0.55f;
                bBase.preserveDensity = _blendLab.densityFromB01 > 0.55f;
                bBase.preserveScale = _blendLab.scaleFromB01 > 0.55f;
                bBase.preserveDetail = _blendLab.detailFromB01 > 0.55f;
                bBase.preserveTone = _blendLab.toneFromB01 > 0.55f;
                bBase.preserveColourOrPalette = _blendLab.colourFromB01 > 0.55f;
                bBase.preserveBlendOrder = _blendLab.blendOrderFromB01 > 0.55f;
                bBase.preserveSeam = _blendLab.seamFromB01 > 0.55f;
                result.Add(bBase);
            }
            _combination.randomness01 = Mathf.Clamp01(_blendLab.randomDetail01);
            _combination.lockedInfluence01 = 1f - _combination.randomness01;
            return result.ToArray();
        }

        private float AverageBlendFromB()
        {
            return Mathf.Clamp01((_blendLab.structureFromB01 + _blendLab.densityFromB01 + _blendLab.scaleFromB01 + _blendLab.detailFromB01 + _blendLab.toneFromB01 + _blendLab.colourFromB01 + _blendLab.blendOrderFromB01 + _blendLab.seamFromB01) / 8f);
        }

        private void RebuildBlendOutputPreview()
        {
            if (_blendOutputPreview != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_blendOutputPreview);
            _blendOutputPreview = null;
            ProceduralTextureCandidate a = ResolveBlendSource(false);
            ProceduralTextureCandidate b = ResolveBlendSource(true);
            if (a?.values == null || b?.values == null)
                return;
            int width = Mathf.Min(a.valuesWidth > 0 ? a.valuesWidth : GridPreviewSize, b.valuesWidth > 0 ? b.valuesWidth : GridPreviewSize);
            int height = Mathf.Min(a.valuesHeight > 0 ? a.valuesHeight : GridPreviewSize, b.valuesHeight > 0 ? b.valuesHeight : GridPreviewSize);
            float[] aValues = ResampleFeatureValues(a.values, a.valuesWidth, a.valuesHeight, width, height);
            float[] bValues = ResampleFeatureValues(b.values, b.valuesWidth, b.valuesHeight, width, height);
            var pixels = new Color[width * height];
            float mix = AverageBlendFromB();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = Mathf.Lerp(aValues[i], bValues[i], mix);
                pixels[i] = new Color(value, value, value, 1f);
            }
            _blendOutputPreview = ProceduralTextureCombinationUtility.CreateTexture(width, height, TextureFormat.RGBA32, false, true, pixels, "Blend Preview");
        }

        private void QueueReferenceFeaturePreviewRebuild()
        {
            _referenceFeaturePreviewDirty = true;
            EditorApplication.delayCall -= RebuildReferenceFeaturePreview;
            EditorApplication.delayCall += RebuildReferenceFeaturePreview;
        }

        private void RebuildReferenceFeaturePreview()
        {
            if (this == null)
                return;
            DestroyReferenceFeaturePreview();
            if (_referenceTexture == null)
            {
                _referenceFeaturePreviewDirty = false;
                Repaint();
                return;
            }
            _referenceValues = ReadTextureValues(_referenceTexture, InfluenceFeatureSize, InfluenceFeatureSize);
            Color[] previewPixels = new Color[_referenceValues.Length];
            for (int i = 0; i < previewPixels.Length; i++)
                previewPixels[i] = new Color(_referenceValues[i], _referenceValues[i], _referenceValues[i], 1f);
            _referenceSourcePreview = ProceduralTextureCombinationUtility.CreateTexture(InfluenceFeatureSize, InfluenceFeatureSize, TextureFormat.RGBA32, false, true, previewPixels, "Reference Preview");
            _referenceFeaturePreview = new ProceduralTextureFeaturePreview
            {
                structureMap = CreateStructureMap(_referenceValues, InfluenceFeatureSize, InfluenceFeatureSize, "Reference Structure"),
                densityMask = CreateDensityMask(_referenceValues, InfluenceFeatureSize, InfluenceFeatureSize, "Reference Density"),
                lowFrequencyMap = CreateFrequencyMap(_referenceValues, InfluenceFeatureSize, InfluenceFeatureSize, false, "Reference Scale"),
                highFrequencyMap = CreateFrequencyMap(_referenceValues, InfluenceFeatureSize, InfluenceFeatureSize, true, "Reference Detail"),
                toneStrip = CreateToneStrip(_referenceValues, InfluenceFeatureSize, 18, "Reference Tone"),
                toneHistogram = CreateToneHistogram(_referenceValues, InfluenceFeatureSize, 18, "Reference Histogram"),
                colourRamp = CreateToneStrip(_referenceValues, InfluenceFeatureSize, 18, "Reference Colour"),
                stampShapePreview = CreateTilePreview(_referenceValues, InfluenceFeatureSize, InfluenceFeatureSize, "Reference Stamp"),
                seamEdgeStrip = CreateSeamStrip(_referenceValues, InfluenceFeatureSize, InfluenceFeatureSize, "Reference Seam"),
                tilePreview2x = CreateTilePreview(_referenceValues, InfluenceFeatureSize, InfluenceFeatureSize, "Reference Tile")
            };
            _referenceFeaturePreviewDirty = false;
            Repaint();
        }

        private static float[] ReadTextureValues(Texture2D texture, int width, int height)
        {
            var values = new float[width * height];
            if (texture == null)
                return values;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color;
                    try
                    {
                        color = texture.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                    }
                    catch
                    {
                        color = Color.black;
                    }
                    values[y * width + x] = color.grayscale;
                }
            }
            return values;
        }

        private void GenerateReferenceMatches()
        {
            if (_referenceTexture == null)
            {
                _lastStatus = "Assign a reference texture before generating matches.";
                Repaint();
                return;
            }
            if (_referenceValues == null || _referenceValues.Length == 0)
                RebuildReferenceFeaturePreview();
            ProceduralTextureBaseSettings referenceBase = ProceduralTextureBaseSettings.CreateDefault(0);
            referenceBase.name = $"{_referenceTexture.name} Reference";
            referenceBase.useBakedValues = true;
            referenceBase.bakedWidth = InfluenceFeatureSize;
            referenceBase.bakedHeight = InfluenceFeatureSize;
            referenceBase.bakedValues = _referenceValues != null ? (float[])_referenceValues.Clone() : ReadTextureValues(_referenceTexture, InfluenceFeatureSize, InfluenceFeatureSize);
            referenceBase.weight = Mathf.Lerp(0.2f, 1f, _referenceMatch.referenceStrength01);
            referenceBase.blendMode = ProceduralTextureBlendMode.Replace;
            referenceBase.guidanceMode = ProceduralTextureGuidanceMode.Guide;
            referenceBase.guideStrength01 = _referenceMatch.referenceStrength01;
            referenceBase.preserveStructure = _referenceMatch.matchStructure;
            referenceBase.preserveDensity = _referenceMatch.matchDensity;
            referenceBase.preserveTone = _referenceMatch.matchTone;
            referenceBase.preserveColourOrPalette = _referenceMatch.matchColour;
            referenceBase.preserveSeam = _referenceMatch.matchSeam;
            if (_referenceMatch.useAsStampSource)
                referenceBase.generation.stampTexture = _referenceTexture;
            GenerateCandidates(new[] { referenceBase }, "Generating reference matches");
        }

        private void EnsureTexturePresets()
        {
            if (_texturePresets.Count > 0)
                return;
            _texturePresets.Add(CreatePreset("speckled-mask", "Speckled Mask", ProceduralTexturePresetCategory.Mask, ProceduralTextureMapIntent.Mask, ProceduralTexturePattern.PoissonDisk, ProceduralTextureStampShape.Gaussian, 220, 0.006f, 0.028f, ProceduralTextureBlendMode.Max));
            _texturePresets.Add(CreatePreset("stone-scatter", "Stone Scatter", ProceduralTexturePresetCategory.Organic, ProceduralTextureMapIntent.Height, ProceduralTexturePattern.RandomMinSpacing, ProceduralTextureStampShape.SoftCircle, 96, 0.026f, 0.085f, ProceduralTextureBlendMode.Overlay));
            _texturePresets.Add(CreatePreset("organic-pores", "Organic Pores", ProceduralTexturePresetCategory.Organic, ProceduralTextureMapIntent.Roughness, ProceduralTexturePattern.PoissonDisk, ProceduralTextureStampShape.Ring, 180, 0.012f, 0.04f, ProceduralTextureBlendMode.Subtract));
            _texturePresets.Add(CreatePreset("stylized-dots", "Stylized Dots", ProceduralTexturePresetCategory.Mask, ProceduralTextureMapIntent.AlphaDissolve, ProceduralTexturePattern.StratifiedJitterGrid, ProceduralTextureStampShape.SoftCircle, 144, 0.018f, 0.046f, ProceduralTextureBlendMode.Max));
            _texturePresets.Add(CreatePreset("grid-chips", "Grid Chips", ProceduralTexturePresetCategory.Structure, ProceduralTextureMapIntent.Mask, ProceduralTexturePattern.HexGrid, ProceduralTextureStampShape.Square, 110, 0.014f, 0.044f, ProceduralTextureBlendMode.Overlay));
            _texturePresets.Add(CreatePreset("radial-burst", "Radial Burst", ProceduralTexturePresetCategory.Structure, ProceduralTextureMapIntent.DiffuseDetail, ProceduralTexturePattern.PolarPattern, ProceduralTextureStampShape.Cone, 88, 0.012f, 0.055f, ProceduralTextureBlendMode.Max));
            _texturePresets.Add(CreatePreset("cracks-mask", "Cracks / Noise Mask", ProceduralTexturePresetCategory.Mask, ProceduralTextureMapIntent.Mask, ProceduralTexturePattern.HaltonSequence, ProceduralTextureStampShape.Diamond, 210, 0.004f, 0.02f, ProceduralTextureBlendMode.Subtract));
            _texturePresets.Add(CreatePreset("height-bumps", "Height Bumps", ProceduralTexturePresetCategory.Height, ProceduralTextureMapIntent.Height, ProceduralTexturePattern.HammersleySequence, ProceduralTextureStampShape.Gaussian, 128, 0.02f, 0.075f, ProceduralTextureBlendMode.Overlay));
            _texturePresets.Add(CreatePreset("roughness-breakup", "Roughness Breakup", ProceduralTexturePresetCategory.Roughness, ProceduralTextureMapIntent.Roughness, ProceduralTexturePattern.RandomMinSpacing, ProceduralTextureStampShape.Cone, 150, 0.01f, 0.06f, ProceduralTextureBlendMode.Multiply));
            _texturePresets.Add(CreatePreset("alpha-dissolve", "Alpha Dissolve", ProceduralTexturePresetCategory.Alpha, ProceduralTextureMapIntent.AlphaDissolve, ProceduralTexturePattern.SpiralPattern, ProceduralTextureStampShape.Gaussian, 160, 0.008f, 0.04f, ProceduralTextureBlendMode.Max));
            QueuePresetPreviewRebuild();
        }

        private ProceduralTexturePresetDefinition CreatePreset(string id, string name, ProceduralTexturePresetCategory category, ProceduralTextureMapIntent intent, ProceduralTexturePattern pattern, ProceduralTextureStampShape stamp, int count, float radiusMin, float radiusMax, ProceduralTextureBlendMode blend)
        {
            ProceduralTextureBaseSettings layer = ProceduralTextureBaseSettings.CreateDefault(0);
            layer.name = name;
            layer.blendMode = blend;
            layer.generation.pattern = pattern;
            layer.generation.stampShape = stamp;
            layer.generation.count = count;
            layer.generation.radiusMin01 = radiusMin;
            layer.generation.radiusMax01 = radiusMax;
            layer.generation.seed = ProceduralTextureRecipeRandomizer.HashSeed(id.GetHashCode(), count);
            layer.generation.contrast = 1.25f;
            return new ProceduralTexturePresetDefinition
            {
                id = id,
                name = name,
                category = category,
                intent = intent,
                tags = $"{category} {intent} {pattern} {stamp}",
                layers = new[] { layer }
            };
        }

        private List<int> VisiblePresetIndices()
        {
            EnsureTexturePresets();
            string query = (_presetSearch ?? string.Empty).Trim().ToLowerInvariant();
            var result = new List<int>();
            for (int i = 0; i < _texturePresets.Count; i++)
            {
                ProceduralTexturePresetDefinition preset = _texturePresets[i];
                if (string.IsNullOrEmpty(query) || preset.name.ToLowerInvariant().Contains(query) || preset.tags.ToLowerInvariant().Contains(query))
                    result.Add(i);
            }
            return result;
        }

        private void QueuePresetPreviewRebuild()
        {
            _presetPreviewsDirty = true;
            EditorApplication.delayCall -= RebuildPresetPreviews;
            EditorApplication.delayCall += RebuildPresetPreviews;
        }

        private void RebuildPresetPreviews()
        {
            DestroyPresetPreviews();
            var settings = CreateSizedSettings(_combination, 96);
            settings.width = 96;
            settings.height = 96;
            for (int i = 0; i < _texturePresets.Count; i++)
            {
                float[] values = ProceduralTextureCombinationUtility.GenerateCombinedValues(_texturePresets[i].layers, settings, i * 37);
                Color[] pixels = ProceduralTextureCombinationUtility.ValuesToPixels(values, settings, PaletteColors());
                _presetPreviewTextures.Add(ProceduralTextureCombinationUtility.CreateTexture(96, 96, TextureFormat.RGBA32, false, true, pixels, $"{_texturePresets[i].name} Preview"));
            }
            _presetPreviewsDirty = false;
            Repaint();
        }

        private void DrawPresetGrid(Rect rect)
        {
            if (_presetPreviewsDirty || _presetPreviewTextures.Count != _texturePresets.Count)
                QueuePresetPreviewRebuild();
            List<int> visible = VisiblePresetIndices();
            int columns = Mathf.Max(1, Mathf.FloorToInt(rect.width / 140f));
            int rows = Mathf.CeilToInt(visible.Count / (float)columns);
            float tile = Mathf.Floor((rect.width - (columns - 1) * BaseGap) / columns);
            float contentHeight = Mathf.Max(rect.height, rows * 132f + Mathf.Max(0, rows - 1) * BaseGap);
            Rect view = new Rect(0, 0, rect.width - 16f, contentHeight);
            _presetScroll = GUI.BeginScrollView(rect, _presetScroll, view, false, contentHeight > rect.height);
            for (int i = 0; i < visible.Count; i++)
            {
                int presetIndex = visible[i];
                Rect card = new Rect((i % columns) * (tile + BaseGap), (i / columns) * (132f + BaseGap), tile, 132f);
                DrawPresetCard(card, presetIndex);
            }
            GUI.EndScrollView();
        }

        private void DrawPresetCard(Rect rect, int presetIndex)
        {
            ProceduralTexturePresetDefinition preset = _texturePresets[presetIndex];
            bool selected = presetIndex == _selectedPresetIndex;
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(selected ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, selected ? 0.13f : 0.055f), PanelBorder(selected ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, selected ? 0.74f : 0.24f));
            Rect image = new Rect(rect.x + 6f, rect.y + 24f, rect.width - 12f, 72f);
            DrawCheckerBackground(image);
            if (presetIndex < _presetPreviewTextures.Count && _presetPreviewTextures[presetIndex] != null)
                GUI.DrawTexture(FitRect(image, _presetPreviewTextures[presetIndex].width, _presetPreviewTextures[presetIndex].height), _presetPreviewTextures[presetIndex], ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x + 7f, rect.y + 5f, rect.width - 14f, 16f), preset.name, EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(rect.x + 7f, rect.yMax - 31f, rect.width - 14f, 14f), $"{preset.category} - {preset.intent}", UtilityWindowTheme.MutedMiniLabelStyle);
            if (GUI.Button(new Rect(rect.x + 7f, rect.yMax - 19f, 42f, 16f), "Use", EditorStyles.miniButton))
                UsePreset(presetIndex);
            if (GUI.Button(new Rect(rect.x + 52f, rect.yMax - 19f, 42f, 16f), "Guide", EditorStyles.miniButton))
                UsePresetAsGuide(presetIndex);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                _selectedPresetIndex = presetIndex;
                RequestSessionSave();
                Event.current.Use();
            }
        }

        private void DrawPresetRecipeStack()
        {
            if (_texturePresets.Count == 0)
                return;
            _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, 0, _texturePresets.Count - 1);
            ProceduralTexturePresetDefinition preset = _texturePresets[_selectedPresetIndex];
            using (BeginInspectorSection("Recipe Stack", UtilityWindowTheme.Green, preset.intent.ToString(), TextureButtonTone.Ghost, GUILayout.ExpandHeight(true)))
            {
                if (_selectedPresetIndex < _presetPreviewTextures.Count && _presetPreviewTextures[_selectedPresetIndex] != null)
                {
                    Rect preview = GUILayoutUtility.GetRect(120f, 140f, GUILayout.ExpandWidth(true));
                    DrawCheckerBackground(preview);
                    GUI.DrawTexture(FitRect(preview, 96, 96), _presetPreviewTextures[_selectedPresetIndex], ScaleMode.ScaleToFit, true);
                }
                for (int i = 0; i < preset.layers.Length; i++)
                {
                    ProceduralTextureBaseSettings layer = preset.layers[i];
                    EditorGUILayout.LabelField($"{i + 1}. {layer.name} - {layer.generation.pattern} / {layer.blendMode}", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                DrawPresetActionRow(_selectedPresetIndex);
            }
        }

        private void DrawPresetActionRow(int presetIndex)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(presetIndex < 0 || presetIndex >= _texturePresets.Count))
                {
                    if (GUILayout.Button("Use", EditorStyles.miniButton))
                        UsePreset(presetIndex);
                    if (GUILayout.Button("Edit", EditorStyles.miniButton))
                        EditPreset(presetIndex);
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton))
                        DuplicatePreset(presetIndex);
                    if (GUILayout.Button("Guide", EditorStyles.miniButton))
                        UsePresetAsGuide(presetIndex);
                    if (GUILayout.Button("Layer", EditorStyles.miniButton))
                        AddPresetAsLayer(presetIndex);
                }
            }
        }

        private void UsePreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= _texturePresets.Count)
                return;
            AddHistorySnapshot(_baseHistory, "Before Use Preset", false);
            _bases.Clear();
            _bases.AddRange(ProceduralTextureCombinationUtility.CloneBases(_texturePresets[presetIndex].layers));
            _mapIntent = _texturePresets[presetIndex].intent;
            GenerateCandidates(ProceduralTextureCombinationUtility.CloneBases(_bases), "Generating preset");
        }

        private void EditPreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= _texturePresets.Count)
                return;
            AddHistorySnapshot(_baseHistory, "Before Edit Preset", false);
            _bases.Clear();
            _bases.AddRange(ProceduralTextureCombinationUtility.CloneBases(_texturePresets[presetIndex].layers));
            EnsureBaseBounds();
            SelectSingleBase(0);
            _activeWorkflow = TextureDesignWorkflow.ManualCompose;
            MarkDirty("Loaded preset for manual editing.");
        }

        private void DuplicatePreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= _texturePresets.Count)
                return;
            ProceduralTexturePresetDefinition clone = _texturePresets[presetIndex].Clone();
            clone.id = $"{clone.id}-copy-{_texturePresets.Count}";
            clone.name = $"{clone.name} Copy";
            _texturePresets.Add(clone);
            _selectedPresetIndex = _texturePresets.Count - 1;
            QueuePresetPreviewRebuild();
            RequestSessionSave();
        }

        private void UsePresetAsGuide(int presetIndex)
        {
            ProceduralTextureCandidate candidate = CreatePresetCandidate(presetIndex, true);
            if (candidate == null)
                return;
            candidate.locked = true;
            candidate.guidanceMode = ProceduralTextureGuidanceMode.Guide;
            _influences.Add(candidate);
            QueueInfluenceFeaturePreviewRebuild();
            _lastStatus = "Added preset as guide.";
            RequestSessionSave();
        }

        private void AddPresetAsLayer(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= _texturePresets.Count)
                return;
            AddHistorySnapshot(_baseHistory, "Before Add Preset Layers", false);
            ProceduralTextureBaseSettings[] layers = ProceduralTextureCombinationUtility.CloneBases(_texturePresets[presetIndex].layers);
            for (int i = 0; i < layers.Length && _bases.Count < ProceduralTextureCombinationUtility.MaxBaseCount; i++)
                _bases.Add(layers[i]);
            _activeWorkflow = TextureDesignWorkflow.ManualCompose;
            MarkDirty("Added preset layers.");
        }

        private ProceduralTextureCandidate CreatePresetCandidate(int presetIndex, bool includePreview)
        {
            if (presetIndex < 0 || presetIndex >= _texturePresets.Count)
                return null;
            ProceduralTexturePresetDefinition preset = _texturePresets[presetIndex];
            var candidate = new ProceduralTextureCandidate
            {
                label = preset.name,
                bases = ProceduralTextureCombinationUtility.CloneBases(preset.layers),
                settings = _combination.Clone(),
                seedOffset = ProceduralTextureRecipeRandomizer.HashSeed(preset.id.GetHashCode(), presetIndex),
                createdUtc = DateTime.UtcNow.ToString("u")
            };
            if (includePreview)
                GenerateVariantPreviewData(candidate, GridPreviewSize);
            return candidate;
        }

        private void ApplyMapIntent(ProceduralTextureMapIntent intent)
        {
            if (_mapIntent == intent)
                return;
            _mapIntent = intent;
            switch (intent)
            {
                case ProceduralTextureMapIntent.Height:
                    _export.height = true;
                    _export.normal = true;
                    _combination.blur = Mathf.Max(_combination.blur, 0.08f);
                    break;
                case ProceduralTextureMapIntent.Roughness:
                    _export.roughness = true;
                    _combination.contrast = Mathf.Clamp(_combination.contrast, 0.8f, 1.35f);
                    break;
                case ProceduralTextureMapIntent.AlphaDissolve:
                    _export.alpha = true;
                    _combination.threshold = Mathf.Max(_combination.threshold, 0.35f);
                    break;
                case ProceduralTextureMapIntent.NormalDetail:
                    _export.height = true;
                    _export.normal = true;
                    _export.normalStrength = Mathf.Max(_export.normalStrength, 2f);
                    break;
                default:
                    _export.diffuse = true;
                    break;
            }
            QueueMapPreviewRebuild();
            RequestSessionSave();
        }

        private void QueueMapPreviewRebuild()
        {
            _mapPreviewsDirty = true;
            EditorApplication.delayCall -= RebuildMapPreviews;
            EditorApplication.delayCall += RebuildMapPreviews;
        }

        private void RebuildMapPreviews()
        {
            DestroyMapPreviews();
            ProceduralTextureCandidate active = IsValidActiveCandidate() ? _candidates[_activeCandidateIndex] : null;
            float[] values = active?.values ?? _previewValues;
            Color[] pixels = active?.pixels ?? _previewPixels;
            if (active != null && (active.values == null || active.values.Length == 0) && active.bases != null)
            {
                GenerateVariantPreviewData(active, GridPreviewSize);
                values = active.values;
                pixels = active.pixels;
            }
            if (values != null && values.Length > 0)
            {
                ProceduralTextureCombinationSettings settings = (active?.settings ?? _combination).Clone();
                settings.width = active != null && active.valuesWidth > 0 ? active.valuesWidth : (_previewTexture != null ? _previewTexture.width : settings.width);
                settings.height = active != null && active.valuesHeight > 0 ? active.valuesHeight : (_previewTexture != null ? _previewTexture.height : settings.height);
                settings.mipChain = false;
                settings.Clamp();
                foreach (ProceduralTextureMapType type in Enum.GetValues(typeof(ProceduralTextureMapType)))
                {
                    Texture2D texture = ProceduralTextureCombinationUtility.GenerateMapTexture(values, settings, _export, type, pixels);
                    _mapPreviewTextures.Add(texture);
                    _mapPreviewTypes.Add(type);
                }
            }
            _mapPreviewsDirty = false;
            Repaint();
        }

        private void DrawMapPrepPreview(Rect rect, ProceduralTextureCandidate active)
        {
            if (active == null && _previewTexture == null)
            {
                DrawInlineCenteredMessage(rect, "Generate or select a texture to prepare material maps.");
                return;
            }
            DrawCheckerBackground(rect);
            Texture2D texture = active?.preview ?? _previewTexture;
            if (texture != null)
                GUI.DrawTexture(FitRect(rect, texture.width, texture.height), texture, ScaleMode.ScaleToFit, true);
            DrawPreviewBorder(rect);
            if (_mapPreviewsDirty)
                QueueMapPreviewRebuild();
        }

        private void DrawMapPreviewStrip()
        {
            if (_mapPreviewsDirty)
                QueueMapPreviewRebuild();
            Rect viewport = GUILayoutUtility.GetRect(1f, 118f, GUILayout.ExpandWidth(true), GUILayout.Height(118f));
            if (_mapPreviewTextures.Count == 0)
            {
                DrawInlineCenteredMessage(viewport, "Map previews rebuild after selecting a generated texture.");
                return;
            }
            float tile = 96f;
            Rect view = new Rect(0, 0, _mapPreviewTextures.Count * (tile + BaseGap), 104f);
            _mapStripScroll = GUI.BeginScrollView(viewport, _mapStripScroll, view, view.width > viewport.width, false);
            for (int i = 0; i < _mapPreviewTextures.Count; i++)
            {
                Rect rect = new Rect(i * (tile + BaseGap), 0, tile, 104f);
                DrawTextureMiniPreview(new Rect(rect.x, rect.y, tile, 82f), _mapPreviewTextures[i], _mapPreviewTypes[i].ToString());
            }
            GUI.EndScrollView();
        }

        private void DetectAssetProductionBridge()
        {
            bool found = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            {
                try
                {
                    return assembly.GetTypes().Any(type => type.Name.IndexOf("TextureArray", StringComparison.OrdinalIgnoreCase) >= 0
                        || type.Name.IndexOf("AssetProduction", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                catch (ReflectionTypeLoadException)
                {
                    return false;
                }
            });
            _assetProductionBridgeStatus = found
                ? "Asset Production bridge candidates detected. Handoff remains optional for this MVP."
                : "Asset Production bridge unavailable. Export PNG maps remains fully usable.";
        }

        private List<TextureHistorySnapshot> CombinedSnapshots()
        {
            var snapshots = new List<TextureHistorySnapshot>();
            snapshots.AddRange(_combinationHistory);
            snapshots.AddRange(_baseHistory);
            return snapshots.OrderByDescending(snapshot => snapshot.createdUtc).ToList();
        }

        private void DrawLineageGraph(List<TextureHistorySnapshot> snapshots)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 150f, GUILayout.ExpandWidth(true), GUILayout.Height(150f));
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(HistoryTint(), 0.06f), PanelBorder(HistoryTint(), 0.26f));
            float x = rect.x + 12f;
            for (int i = 0; i < snapshots.Count && i < 6; i++)
            {
                Rect node = new Rect(x, rect.y + 20f + (i % 2) * 48f, 122f, 42f);
                if (Event.current.type == EventType.Repaint)
                    DrawStudioBox(node, PanelFill(HistoryTint(), 0.12f), PanelBorder(HistoryTint(), 0.42f));
                GUI.Label(new Rect(node.x + 6f, node.y + 4f, node.width - 12f, 15f), snapshots[i].label, UtilityWindowTheme.MutedMiniLabelStyle);
                GUI.Label(new Rect(node.x + 6f, node.y + 22f, node.width - 12f, 15f), snapshots[i].inheritedTraits, UtilityWindowTheme.MutedMiniLabelStyle);
                if (i < snapshots.Count - 1 && Event.current.type == EventType.Repaint)
                    Handles.DrawLine(new Vector3(node.xMax, node.center.y), new Vector3(node.xMax + 20f, rect.y + 41f + ((i + 1) % 2) * 48f));
                x += 146f;
            }
        }

        private void DrawSnapshotGrid(List<TextureHistorySnapshot> snapshots)
        {
            for (int i = 0; i < snapshots.Count; i++)
                DrawHistorySnapshotRow(snapshots[i]);
        }

        private void DrawHistorySnapshotRow(TextureHistorySnapshot snapshot)
        {
            using (BeginInspectorSection(snapshot.label, HistoryTint(), ShortTime(snapshot.createdUtc), TextureButtonTone.Ghost))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (snapshot.preview != null)
                    {
                        Rect preview = GUILayoutUtility.GetRect(72f, 72f, GUILayout.Width(72f), GUILayout.Height(72f));
                        DrawCheckerBackground(preview);
                        GUI.DrawTexture(FitRect(preview, snapshot.preview.width, snapshot.preview.height), snapshot.preview, ScaleMode.ScaleToFit, true);
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField($"{snapshot.workflow} - Gen {snapshot.generationNumber}", UtilityWindowTheme.MutedMiniLabelStyle);
                        EditorGUILayout.LabelField(snapshot.parentSummary, UtilityWindowTheme.MutedMiniLabelStyle);
                        EditorGUILayout.LabelField(snapshot.inheritedTraits, UtilityWindowTheme.MutedMiniLabelStyle);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Restore", EditorStyles.miniButton))
                                RestoreSnapshot(snapshot);
                            if (GUILayout.Button("Use as Guide", EditorStyles.miniButton))
                                UseSnapshotAsGuide(snapshot);
                            if (GUILayout.Button("Add as Layer", EditorStyles.miniButton))
                                AddSnapshotAsLayer(snapshot);
                            if (GUILayout.Button("Export", EditorStyles.miniButton))
                                ExportSnapshot(snapshot);
                        }
                    }
                }
            }
        }

        private void UseSnapshotAsGuide(TextureHistorySnapshot snapshot)
        {
            if (snapshot?.values == null)
            {
                RestoreSnapshot(snapshot);
                return;
            }
            var candidate = new ProceduralTextureCandidate
            {
                label = snapshot.label,
                values = (float[])snapshot.values.Clone(),
                pixels = snapshot.pixels != null ? (Color[])snapshot.pixels.Clone() : null,
                valuesWidth = snapshot.preview != null ? snapshot.preview.width : snapshot.settings.width,
                valuesHeight = snapshot.preview != null ? snapshot.preview.height : snapshot.settings.height,
                settings = snapshot.settings.Clone(),
                bases = snapshot.bases != null ? ProceduralTextureCombinationUtility.CloneBases(snapshot.bases) : Array.Empty<ProceduralTextureBaseSettings>(),
                locked = true,
                guidanceMode = ProceduralTextureGuidanceMode.Guide,
                guideStrength01 = 1f,
                influenceWeight = 1f
            };
            if (candidate.pixels != null)
                candidate.preview = CreateVariantPreview(candidate);
            _influences.Add(candidate);
            QueueInfluenceFeaturePreviewRebuild();
            RequestSessionSave();
        }

        private void AddSnapshotAsLayer(TextureHistorySnapshot snapshot)
        {
            if (snapshot?.bases == null || snapshot.bases.Length == 0)
                return;
            for (int i = 0; i < snapshot.bases.Length && _bases.Count < ProceduralTextureCombinationUtility.MaxBaseCount; i++)
                _bases.Add(snapshot.bases[i].Clone());
            _activeWorkflow = TextureDesignWorkflow.ManualCompose;
            MarkDirty("Added snapshot layers.");
        }

        private void ExportSnapshot(TextureHistorySnapshot snapshot)
        {
            if (snapshot?.values == null || snapshot.settings == null)
            {
                _lastStatus = "Snapshot has no cached output to export.";
                return;
            }
            string folder = System.IO.Directory.Exists(_lastSaveFolder) ? _lastSaveFolder : "Assets";
            string path = EditorUtility.SaveFilePanelInProject("Export Snapshot Maps", SanitizeFileName(snapshot.label), "png", "Choose the base PNG name for exported snapshot maps.", folder);
            if (string.IsNullOrEmpty(path))
                return;
            string directory = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
            string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
            UnityEngine.Object first = null;
            int count = ExportAllEnabledMaps(snapshot.values, snapshot.pixels, snapshot.settings, directory, baseName, ref first);
            AssetDatabase.Refresh();
            _lastStatus = $"Exported {count} snapshot map(s).";
        }

        private void DrawCandidateMiniPreview(Rect rect, ProceduralTextureCandidate candidate, string label)
        {
            DrawTextureMiniPreview(rect, candidate?.preview, label);
        }

        private void DrawTextureMiniPreview(Rect rect, Texture2D texture, string label)
        {
            DrawCheckerBackground(rect);
            if (texture != null)
                GUI.DrawTexture(FitRect(rect, texture.width, texture.height), texture, ScaleMode.ScaleToFit, true);
            DrawStudioBox(rect, Color.clear, PanelBorder(UtilityWindowTheme.Neutral, 0.36f));
            GUI.Label(new Rect(rect.x + 5f, rect.yMax - 18f, rect.width - 10f, 14f), label, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawFeatureTile(Rect rect, string label, Texture2D texture, bool enabled)
        {
            Color old = GUI.color;
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            DrawTextureMiniPreview(rect, texture, label);
            GUI.color = old;
        }

        private void DestroyReferenceFeaturePreview()
        {
            EditorApplication.delayCall -= RebuildReferenceFeaturePreview;
            DestroyFeaturePreview(_referenceFeaturePreview);
            _referenceFeaturePreview = null;
            if (_referenceSourcePreview != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_referenceSourcePreview);
            _referenceSourcePreview = null;
            _referenceValues = null;
        }

        private void DestroyPresetPreviews()
        {
            EditorApplication.delayCall -= RebuildPresetPreviews;
            for (int i = 0; i < _presetPreviewTextures.Count; i++)
                if (_presetPreviewTextures[i] != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_presetPreviewTextures[i]);
            _presetPreviewTextures.Clear();
        }

        private void DestroyMapPreviews()
        {
            EditorApplication.delayCall -= RebuildMapPreviews;
            for (int i = 0; i < _mapPreviewTextures.Count; i++)
                if (_mapPreviewTextures[i] != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_mapPreviewTextures[i]);
            _mapPreviewTextures.Clear();
            _mapPreviewTypes.Clear();
        }
    }
#endif
}
