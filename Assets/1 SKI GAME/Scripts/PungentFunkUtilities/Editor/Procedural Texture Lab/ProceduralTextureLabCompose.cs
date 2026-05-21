using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow
    {
        private const int InfluenceFeatureSize = 64;

        private sealed class InfluenceFeatureEntry
        {
            public string label;
            public ProceduralTextureFeaturePreview preview;
            public float guideStrength01;
            public float similarityTarget01;
            public float coverage01;
            public float contrast01;
            public float detail01;
            public float scale01;
            public float seamScore01;
        }

        private readonly List<InfluenceFeatureEntry> _influenceFeatureEntries = new List<InfluenceFeatureEntry>();
        private Texture2D _randomnessFeatureStrip;
        private InfluenceFeatureEntry _guidedRefineFeatureEntry;
        private int _guidedRefineFeatureCandidateIndex = -1;
        private bool _influenceFeaturePreviewDirty = true;
        private bool _guidedRefineFeaturePreviewDirty = true;

        private void DrawComposeInspector(float width)
        {
            if (_activeInspector == TextureInspectorTab.Compose && _activeWorkflow != TextureDesignWorkflow.ManualCompose)
                _activeWorkflow = TextureDesignWorkflow.ManualCompose;

            if (_activeWorkflow == TextureDesignWorkflow.ManualCompose)
            {
                DrawManualComposeInspector();
                return;
            }

            if (_activeWorkflow == TextureDesignWorkflow.GuidedRefine)
            {
                DrawGuidedRefineInspector();
                return;
            }

            if (_activeWorkflow == TextureDesignWorkflow.ConstraintMatch)
            {
                DrawConstraintMatchInspector();
                return;
            }

            if (_activeWorkflow == TextureDesignWorkflow.BlendLab)
            {
                DrawBlendLabInspector();
                return;
            }

            if (_activeWorkflow == TextureDesignWorkflow.ReferenceMatch)
            {
                DrawReferenceMatchInspector();
                return;
            }

            if (_activeWorkflow == TextureDesignWorkflow.PresetBrowser)
            {
                DrawPresetBrowserInspector();
                return;
            }

            if (_activeWorkflow == TextureDesignWorkflow.MaterialMapPrep)
            {
                DrawMaterialMapPrepInspector();
                return;
            }

            if (_activeWorkflow != TextureDesignWorkflow.RandomExplore)
            {
                DrawWorkflowInspectorPlaceholder();
                return;
            }

            using (BeginInspectorSection("Random Explore", ComposeTint(), $"{_combination.candidateCount}/16", TextureButtonTone.Secondary))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"{_selectedCandidates.Count} selected", _selectedCandidates.Count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 96f);
                    UtilityWindowTheme.CountPill($"{_influences.Count} guides", _influences.Count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 84f);
                    GUILayout.FlexibleSpace();
                }

                EditorGUI.BeginChangeCheck();
                _combination.candidateCount = EditorGUILayout.IntSlider("Textures", _combination.candidateCount, 1, 16);
                if (EditorGUI.EndChangeCheck())
                {
                    _combination.Clamp();
                    SaveSession();
                }

                if (_combination.candidateCount > ProceduralTextureCombinationUtility.SoftBaseWarningCount)
                    DrawInlineStatus("Large grids are useful for exploration, but many influences can flatten contrast.", UtilityWindowTheme.Amber);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton("Generate", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(24f)))
                        GenerateCandidates();
                    if (StudioButton("Randomize", UtilityWindowTheme.Amber, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                        RandomizeUnlockedVariants();
                    using (new EditorGUI.DisabledScope(_selectedCandidates.Count == 0))
                        if (StudioButton("Guide", UtilityWindowTheme.Green, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                            ToggleSelectedVariantLocks();
                }

                DrawInverseGenerationSliders();
                DrawGenerationRangeControls();
                DrawTileabilityGenerationControls();
                DrawInlineStatus("Guided grid textures move to Guides on Generate. Guides drive future random exploration.", UtilityWindowTheme.Neutral);
            }

            DrawGenerationInfluence();
        }

        private void DrawManualComposeInspector()
        {
            using (BeginInspectorSection("Manual Compose", ComposeTint(), $"{_bases.Count}/{ProceduralTextureCombinationUtility.MaxBaseCount}", TextureButtonTone.Secondary))
            {
                DrawInlineStatus("Compose layers here; the workspace shows the combined output and contribution analysis.", UtilityWindowTheme.Neutral);
                EditorGUI.BeginChangeCheck();
                _combination.width = EditorGUILayout.IntField("Width", _combination.width);
                _combination.height = EditorGUILayout.IntField("Height", _combination.height);
                _tileabilityMode = (TextureTileabilityMode)EditorGUILayout.EnumPopup(new GUIContent("Tileable", "Preview repeats the output. Generate Seamless affects procedural generation."), _tileabilityMode);
                if (EditorGUI.EndChangeCheck())
                {
                    _combination.Clamp();
                    SyncTileabilitySettings();
                    MarkDirty("Updated manual compose output.");
                }

                EditorGUI.BeginChangeCheck();
                _manualComposeAutoPreview = EditorGUILayout.Toggle(new GUIContent("Auto Preview", "Refresh the combined output shortly after layer edits."), _manualComposeAutoPreview);
                _composeMode = EditorGUILayout.Toggle(new GUIContent("Guided Mode", "Edit the selected layer through Shape, Placement, Texture Inputs, and Blend Review steps."), _composeMode == TextureComposeMode.Guided)
                    ? TextureComposeMode.Guided
                    : TextureComposeMode.Unguided;
                if (EditorGUI.EndChangeCheck())
                {
                    if (_manualComposeAutoPreview && _previewDirty)
                        QueueManualComposeAutoPreview("Manual compose changed.");
                    RequestSessionSave();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton("Add Layer", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(24f)))
                        AddBase();
                    using (new EditorGUI.DisabledScope(_selectedBase < 0 || _selectedBase >= _bases.Count))
                    {
                        if (StudioButton("Duplicate", ComposeTint(), TextureButtonTone.Secondary, GUILayout.Height(24f)))
                            DuplicateSelectedBase();
                        if (StudioButton("Delete", UtilityWindowTheme.Red, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                            DeleteSelectedBase();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _manualTextureLayerSource = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Texture Layer", "Add an imported texture as a baked layer with blend and opacity controls."), _manualTextureLayerSource, typeof(Texture2D), false);
                    using (new EditorGUI.DisabledScope(_manualTextureLayerSource == null || _bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount))
                    {
                        if (GUILayout.Button(new GUIContent("Add", "Add this texture as a baked layer."), EditorStyles.miniButton, GUILayout.Width(42f)))
                            AddTextureLayer(_manualTextureLayerSource);
                    }
                }
            }

            DrawManualLayerStackInspector();
            if (_composeMode == TextureComposeMode.Guided)
                DrawGuidedManualComposeEditor();
            else
                DrawSelectedBaseEditor();
        }

        private void DrawGuidedManualComposeEditor()
        {
            if (_selectedBase < 0 || _selectedBase >= _bases.Count || _bases[_selectedBase] == null)
                return;

            ProceduralTextureBaseSettings textureBase = _bases[_selectedBase];
            using (BeginInspectorSection("Guided Layer Edit", ComposeTint(), _manualComposeGuidedStep.ToString(), TextureButtonTone.Secondary))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(_manualComposeGuidedStep == ManualComposeGuidedStep.Shape, "Shape", EditorStyles.miniButtonLeft))
                        _manualComposeGuidedStep = ManualComposeGuidedStep.Shape;
                    if (GUILayout.Toggle(_manualComposeGuidedStep == ManualComposeGuidedStep.Placement, "Placement", EditorStyles.miniButtonMid))
                        _manualComposeGuidedStep = ManualComposeGuidedStep.Placement;
                    if (GUILayout.Toggle(_manualComposeGuidedStep == ManualComposeGuidedStep.TextureInputs, "Inputs", EditorStyles.miniButtonMid))
                        _manualComposeGuidedStep = ManualComposeGuidedStep.TextureInputs;
                    if (GUILayout.Toggle(_manualComposeGuidedStep == ManualComposeGuidedStep.BlendReview, "Blend", EditorStyles.miniButtonRight))
                        _manualComposeGuidedStep = ManualComposeGuidedStep.BlendReview;
                }

                DrawInlineStatus("Guided Mode uses the same layer data as expert mode; it only narrows the visible controls.", UtilityWindowTheme.Neutral);
                EditorGUI.BeginChangeCheck();
                switch (_manualComposeGuidedStep)
                {
                    case ManualComposeGuidedStep.Shape:
                        if (textureBase.useBakedValues)
                            DrawInlineStatus("Baked texture layers do not have procedural shape controls.", UtilityWindowTheme.Amber);
                        else
                            DrawStampPad(textureBase);
                        break;
                    case ManualComposeGuidedStep.Placement:
                        if (textureBase.useBakedValues)
                            DrawInlineStatus("Baked texture layers use their imported placement.", UtilityWindowTheme.Amber);
                        else
                        {
                            DrawPatternStrip(textureBase);
                            if (HasPlacementDetails(textureBase.generation.pattern))
                            {
                                using (new EditorGUI.DisabledScope(textureBase.locks.patternSpecific))
                                    DrawPatternSpecificControls(textureBase.generation);
                                textureBase.locks.patternSpecific = GUILayout.Toggle(textureBase.locks.patternSpecific, new GUIContent("Lock Placement Details", "Lock mode-specific placement controls."), EditorStyles.miniButton);
                            }
                        }
                        break;
                    case ManualComposeGuidedStep.TextureInputs:
                        using (new EditorGUI.DisabledScope(textureBase.locks.source))
                        {
                            if (textureBase.useBakedValues)
                                DrawInlineStatus($"Texture layer {textureBase.bakedWidth}x{textureBase.bakedHeight}. Blend and opacity remain editable.", UtilityWindowTheme.Green);
                            else
                                DrawStampSpecificControls(textureBase.generation);
                        }
                        textureBase.locks.source = GUILayout.Toggle(textureBase.locks.source, new GUIContent("Lock Texture Inputs", "Lock texture source and baked source settings."), EditorStyles.miniButton);
                        break;
                    case ManualComposeGuidedStep.BlendReview:
                        DrawBlendRow(textureBase);
                        DrawInlineStatus("Use the layer stack above to review ordering, visibility, and diagnostic contribution.", UtilityWindowTheme.Neutral);
                        break;
                }
                if (EditorGUI.EndChangeCheck())
                    MarkDirty("Updated guided layer controls.");

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_manualComposeGuidedStep == ManualComposeGuidedStep.Shape))
                    {
                        if (GUILayout.Button("Previous", EditorStyles.miniButton))
                            _manualComposeGuidedStep = (ManualComposeGuidedStep)((int)_manualComposeGuidedStep - 1);
                    }
                    using (new EditorGUI.DisabledScope(_manualComposeGuidedStep == ManualComposeGuidedStep.BlendReview))
                    {
                        if (GUILayout.Button("Next", EditorStyles.miniButton))
                            _manualComposeGuidedStep = (ManualComposeGuidedStep)((int)_manualComposeGuidedStep + 1);
                    }
                }
            }
        }

        private void DrawManualLayerStackInspector()
        {
            using (BeginInspectorSection("Layer Stack", UtilityWindowTheme.Blue, EnabledBaseCount() > 0 ? $"{EnabledBaseCount()} enabled" : "muted", TextureButtonTone.Secondary))
            {
                float width = Mathf.Max(280f, EditorGUIUtility.currentViewWidth - _inspectorWidth > 0f ? _inspectorWidth - 32f : EditorGUIUtility.currentViewWidth - 42f);
                float cardHeight = 104f;
                float contentHeight = Mathf.Max(cardHeight, _bases.Count * (cardHeight + BaseGap) - BaseGap);
                float viewportHeight = Mathf.Clamp(contentHeight, 118f, 360f);
                Rect viewport = GUILayoutUtility.GetRect(width, viewportHeight, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(viewport, EditorGUIUtility.isProSkin ? new Color(0.06f, 0.06f, 0.065f, 0.42f) : new Color(0.76f, 0.77f, 0.79f, 0.28f));

                _lastBaseRects.Clear();
                Rect view = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 16f), Mathf.Max(viewport.height, contentHeight));
                _manualLayerStackScroll = GUI.BeginScrollView(viewport, _manualLayerStackScroll, view, false, contentHeight > viewport.height);
                for (int i = 0; i < _bases.Count; i++)
                {
                    Rect rect = new Rect(0f, i * (cardHeight + BaseGap), view.width, cardHeight);
                    _lastBaseRects[i] = rect;
                    DrawManualLayerStackCard(i, rect);
                }
                DrawBaseDragMarker();
                GUI.EndScrollView();
            }
        }

        private void DrawManualLayerStackCard(int index, Rect rect)
        {
            if (index < 0 || index >= _bases.Count || _bases[index] == null)
                return;

            ProceduralTextureBaseSettings textureBase = _bases[index];
            bool selected = IsBaseSelected(index);
            bool primary = index == _selectedBase;
            bool hover = rect.Contains(Event.current.mousePosition);
            Color tint = textureBase.enabled ? (selected ? ComposeTint() : UtilityWindowTheme.Neutral) : UtilityWindowTheme.Neutral;
            if (Event.current.type == EventType.Repaint)
            {
                DrawStudioBox(rect, PanelFill(tint, selected ? 0.12f : 0.055f), PanelBorder(tint, selected ? 0.78f : hover ? 0.38f : 0.20f));
                if (primary)
                    DrawStudioBox(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), Color.clear, PanelBorder(ComposeTint(), 0.50f));
                if (!textureBase.enabled)
                    EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.30f) : new Color(1f, 1f, 1f, 0.34f));
            }

            Rect handleRect = new Rect(rect.x + 6f, rect.y + 8f, 18f, rect.height - 16f);
            GUI.Label(handleRect, new GUIContent("||", "Drag to reorder."), EditorStyles.miniBoldLabel);
            Rect previewRect = new Rect(handleRect.xMax + 5f, rect.y + 9f, 56f, 56f);
            DrawBasePreview(index, previewRect);
            DrawStudioBox(previewRect, Color.clear, PanelBorder(UtilityWindowTheme.Neutral, 0.22f));

            Rect enabledRect = new Rect(rect.xMax - 54f, rect.y + 8f, 48f, 18f);
            bool enabled = GUI.Toggle(enabledRect, textureBase.enabled, textureBase.enabled ? "On" : "Off", EditorStyles.miniButton);
            if (enabled != textureBase.enabled)
            {
                textureBase.enabled = enabled;
                MarkDirty(enabled ? "Enabled layer." : "Muted layer.");
            }

            Rect labelRect = new Rect(previewRect.xMax + 8f, rect.y + 7f, enabledRect.x - previewRect.xMax - 14f, 18f);
            GUI.Label(labelRect, $"{index + 1}. {textureBase.name}", EditorStyles.miniBoldLabel);
            string sourceLabel = textureBase.useBakedValues ? "Texture Layer" : $"{ObjectNames.NicifyVariableName(textureBase.generation.pattern.ToString())} / {ObjectNames.NicifyVariableName(textureBase.generation.stampShape.ToString())}";
            GUI.Label(new Rect(labelRect.x, labelRect.yMax + 1f, labelRect.width, 14f), sourceLabel, UtilityWindowTheme.MutedMiniLabelStyle);

            Rect placementRect = Rect.zero;
            Rect shapeRect = Rect.zero;
            if (!textureBase.useBakedValues)
            {
                placementRect = new Rect(labelRect.x, labelRect.yMax + 17f, Mathf.Max(80f, labelRect.width * 0.50f - 4f), 18f);
                shapeRect = new Rect(placementRect.xMax + 8f, placementRect.y, Mathf.Max(74f, labelRect.xMax - placementRect.xMax - 8f), 18f);
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(textureBase.locks != null && textureBase.locks.pattern))
                    textureBase.generation.pattern = (ProceduralTexturePattern)EditorGUI.EnumPopup(placementRect, GUIContent.none, textureBase.generation.pattern);
                using (new EditorGUI.DisabledScope(textureBase.locks != null && textureBase.locks.stamp))
                    textureBase.generation.stampShape = (ProceduralTextureStampShape)EditorGUI.EnumPopup(shapeRect, GUIContent.none, textureBase.generation.stampShape);
                if (EditorGUI.EndChangeCheck())
                    MarkDirty("Updated layer placement or shape.");
            }

            Rect blendRect = new Rect(labelRect.x, rect.yMax - 27f, Mathf.Min(112f, labelRect.width * 0.42f), 18f);
            Rect weightRect = new Rect(blendRect.xMax + 8f, blendRect.y + 2f, Mathf.Max(70f, rect.xMax - blendRect.xMax - 18f), 14f);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(textureBase.locks != null && textureBase.locks.blend))
            {
                textureBase.blendMode = DrawBlendModePopup(blendRect, GUIContent.none, textureBase.blendMode);
                textureBase.weight = GUI.HorizontalSlider(weightRect, Mathf.Clamp01(textureBase.weight), 0f, 1f);
            }
            if (EditorGUI.EndChangeCheck())
                MarkDirty("Updated layer blend.");
            GUI.Label(new Rect(weightRect.xMax - 32f, weightRect.y - 13f, 34f, 14f), textureBase.weight.ToString("0.00"), UtilityWindowTheme.MutedMiniLabelStyle);

            HandleBaseCardInput(index, rect, enabledRect, placementRect, shapeRect, blendRect, weightRect);
        }

        private void DrawGuidedRefineInspector()
        {
            if (!IsValidActiveCandidate() && _candidates.Count > 0)
                SetActiveCandidate(0, false);

            using (BeginInspectorSection("Guided Refine", ComposeTint(), IsValidActiveCandidate() ? $"#{_activeCandidateIndex + 1}" : "no target", TextureButtonTone.Secondary))
            {
                if (!IsValidActiveCandidate())
                {
                    DrawInlineStatus("Generate textures, then choose one as the target for guided refinement.", UtilityWindowTheme.Neutral);
                    if (StudioButton("Generate Textures", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(24f)))
                        GenerateCandidates();
                    return;
                }

                ProceduralTextureCandidate target = _candidates[_activeCandidateIndex];
                DrawInlineStatus("Guided Refine uses the active texture as a temporary target. Trait chips decide what the next batch should preserve.", UtilityWindowTheme.Neutral);

                EditorGUI.BeginChangeCheck();
                target.guidanceMode = ProceduralTextureGuidanceMode.TargetSimilarity;
                target.guideStrength01 = EditorGUILayout.Slider(new GUIContent("Target Strength", "How strongly the active texture contributes to the next generated batch."), Mathf.Clamp01(target.guideStrength01 <= 0f ? 1f : target.guideStrength01), 0f, 1f);
                target.influenceWeight = target.guideStrength01;
                target.similarityTarget01 = EditorGUILayout.Slider(new GUIContent("Similarity", "Loose to near-clone mutation radius around the active texture."), Mathf.Clamp01(target.similarityTarget01 <= 0f ? 0.55f : target.similarityTarget01), 0f, 1f);
                target.userRating = EditorGUILayout.IntSlider(new GUIContent("Preference", "A lightweight rating used to weight this target during guided generation."), Mathf.Clamp(target.userRating <= 0 ? 3 : target.userRating, 0, 5), 0, 5);
                DrawInverseGenerationSliders();
                DrawTileabilityGenerationControls();
                if (EditorGUI.EndChangeCheck())
                {
                    QueueGuidedRefineFeaturePreviewRebuild();
                    RequestSessionSave();
                    Repaint();
                }

                DrawSimilarityLabel(target.similarityTarget01);
                DrawPreserveTraitChips(target);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton("Generate Similar", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(24f)))
                        GenerateSimilarFromActiveCandidate();
                    if (StudioButton("Preserve Core", UtilityWindowTheme.Green, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                    {
                        SetCorePreserveTraits(target, true);
                        QueueGuidedRefineFeaturePreviewRebuild();
                        RequestSessionSave();
                    }
                    if (StudioButton("Clear Traits", UtilityWindowTheme.Neutral, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                    {
                        SetAllPreserveTraits(target, false);
                        QueueGuidedRefineFeaturePreviewRebuild();
                        RequestSessionSave();
                    }
                }
            }
        }

        private void DrawConstraintMatchInspector()
        {
            using (BeginInspectorSection("Constraint Match", UtilityWindowTheme.Amber, _candidates.Count == 0 ? "empty" : $"{ConstraintPassCount()}/{_candidates.Count} pass", TextureButtonTone.Secondary))
            {
                DrawInlineStatus("Set production targets, generate candidates, then sort or relax based on where the batch misses.", UtilityWindowTheme.Neutral);

                EditorGUI.BeginChangeCheck();
                _combination.candidateCount = EditorGUILayout.IntSlider("Textures", _combination.candidateCount, 1, 16);
                _combination.constraintSortMode = (ProceduralTextureConstraintSortMode)EditorGUILayout.EnumPopup(new GUIContent("Sort Mode", "Choose which score Sort by Score prioritizes."), _combination.constraintSortMode);
                _combination.seamScoreThreshold = EditorGUILayout.Slider(new GUIContent("Seam Minimum", "Minimum acceptable edge continuity score."), _combination.seamScoreThreshold, 0f, 1f);
                DrawRange("Coverage", ref _combination.constraintCoverageMin01, ref _combination.constraintCoverageMax01, 0f, 1f);
                DrawRange("Contrast", ref _combination.constraintContrastMin01, ref _combination.constraintContrastMax01, 0f, 1f);
                DrawRange("Scale", ref _combination.constraintScaleMin01, ref _combination.constraintScaleMax01, 0f, 1f);
                _tileabilityMode = (TextureTileabilityMode)EditorGUILayout.EnumPopup(new GUIContent("Tileable", "Generate Seamless gives seam scoring real generation support."), _tileabilityMode);
                if (EditorGUI.EndChangeCheck())
                {
                    _combination.Clamp();
                    SyncTileabilitySettings();
                    MarkDirty("Updated constraint targets.");
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton("Generate", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(24f)))
                        GenerateCandidates();
                    using (new EditorGUI.DisabledScope(_candidates.Count <= 1))
                    {
                        if (StudioButton("Sort", UtilityWindowTheme.Amber, TextureButtonTone.Secondary, GUILayout.Height(24f)))
                            SortCandidatesByConstraintScore();
                    }
                    if (StudioButton("Relax", UtilityWindowTheme.Neutral, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                        RelaxConstraintTarget();
                }

                if (_candidates.Count > 0)
                {
                    Color tint = ConstraintPassCount() > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber;
                    DrawInlineStatus($"{ConstraintPassCount()} passing. Best score {BestConstraintScore():0.00}. Sorting by {ObjectNames.NicifyVariableName(_combination.constraintSortMode.ToString()).ToLowerInvariant()}.", tint);
                }
            }

            using (BeginInspectorSection("Generation Envelope", UtilityWindowTheme.Blue, "ranges", TextureButtonTone.Ghost))
            {
                DrawInlineStatus("These ranges shape what gets generated; the targets above decide what passes.", UtilityWindowTheme.Neutral);
                EditorGUI.BeginChangeCheck();
                _combination.generationPatternVariety01 = EditorGUILayout.Slider("Pattern Variety", _combination.generationPatternVariety01, 0f, 1f);
                DrawIntRange("Density", ref _combination.generationDensityMin, ref _combination.generationDensityMax, 1, 512);
                DrawRange("Radius", ref _combination.generationRadiusMin01, ref _combination.generationRadiusMax01, 0.001f, 0.5f);
                DrawRange("Intensity", ref _combination.generationIntensityMin, ref _combination.generationIntensityMax, 0f, 2f);
                DrawRange("Tone Contrast", ref _combination.generationContrastMin, ref _combination.generationContrastMax, 0.1f, 8f);
                if (EditorGUI.EndChangeCheck())
                {
                    _combination.Clamp();
                    MarkDirty("Updated constraint generation envelope.");
                }
            }

            DrawGenerationInfluence();
        }

        private void DrawSimilarityLabel(float similarity)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill($"Similarity {SimilarityBand(similarity)}", UtilityWindowTheme.Blue, 132f);
                UtilityWindowTheme.CountPill($"Mutation {_combination.randomness01:0.00}", UtilityWindowTheme.Cyan, 104f);
                UtilityWindowTheme.CountPill($"Adherence {_combination.lockedInfluence01:0.00}", UtilityWindowTheme.Green, 112f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPreserveTraitChips(ProceduralTextureCandidate target)
        {
            using (BeginInspectorSection("Preserve Traits", UtilityWindowTheme.Green, PreservedTraitCount(target).ToString(), TextureButtonTone.Ghost))
            {
                DrawInlineStatus("Enabled traits narrow the mutation range for that quality while the remaining traits stay exploratory.", UtilityWindowTheme.Neutral);
                DrawTraitChipRow(target,
                    ("Structure", ProceduralTextureTrait.Structure),
                    ("Density", ProceduralTextureTrait.Density),
                    ("Scale", ProceduralTextureTrait.Scale));
                DrawTraitChipRow(target,
                    ("Detail", ProceduralTextureTrait.Detail),
                    ("Tone", ProceduralTextureTrait.Tone),
                    ("Colour", ProceduralTextureTrait.Colour));
                DrawTraitChipRow(target,
                    ("Stamp", ProceduralTextureTrait.StampShape),
                    ("Blend", ProceduralTextureTrait.BlendOrder),
                    ("Seam", ProceduralTextureTrait.Seam));
            }
        }

        private void DrawTraitChipRow(ProceduralTextureCandidate target, params (string label, ProceduralTextureTrait trait)[] traits)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < traits.Length; i++)
                    DrawTraitChip(target, traits[i].label, traits[i].trait);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawTraitChip(ProceduralTextureCandidate target, string label, ProceduralTextureTrait trait)
        {
            bool current = GetPreserveTrait(target, trait);
            GUIContent content = new GUIContent(label, current ? $"Preserve {label} during guided generation." : $"Allow {label} to explore during guided generation.");
            bool next = GUILayout.Toggle(current, content, EditorStyles.miniButton, GUILayout.Height(22f), GUILayout.MinWidth(72f));
            if (next == current)
                return;

            SetPreserveTrait(target, trait, next);
            target.guidanceMode = ProceduralTextureGuidanceMode.TargetSimilarity;
            QueueGuidedRefineFeaturePreviewRebuild();
            RequestSessionSave();
            Repaint();
        }

        private void DrawWorkflowInspectorPlaceholder()
        {
            using (BeginInspectorSection(ObjectNames.NicifyVariableName(_activeWorkflow.ToString()), ComposeTint(), "routed", TextureButtonTone.Secondary))
            {
                DrawInlineStatus("This workflow has a reachable workbench route. Detailed controls will be implemented in a focused follow-up pass.", UtilityWindowTheme.Neutral);
                if (StudioButton("Use Random Explore", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(24f)))
                    _activeWorkflow = TextureDesignWorkflow.RandomExplore;
            }
        }

        private void DrawInverseGenerationSliders()
        {
            EditorGUI.BeginChangeCheck();
            float randomness = EditorGUILayout.Slider("Mutation Amount", _combination.randomness01, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _combination.randomness01 = Mathf.Clamp01(randomness);
                _combination.lockedInfluence01 = 1f - _combination.randomness01;
                MarkDirty("Updated generation balance.");
            }

            EditorGUI.BeginChangeCheck();
            float influence = EditorGUILayout.Slider("Guide Adherence", _combination.lockedInfluence01, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _combination.lockedInfluence01 = Mathf.Clamp01(influence);
                _combination.randomness01 = 1f - _combination.lockedInfluence01;
                MarkDirty("Updated generation balance.");
            }
        }

        private void DrawGenerationRangeControls()
        {
            _combination.generationPatternVariety01 = EditorGUILayout.Slider("Pattern Variety", _combination.generationPatternVariety01, 0f, 1f);
            DrawIntRange("Density", ref _combination.generationDensityMin, ref _combination.generationDensityMax, 1, 512);
            DrawRange("Radius", ref _combination.generationRadiusMin01, ref _combination.generationRadiusMax01, 0.001f, 0.5f);
            DrawRange("Intensity", ref _combination.generationIntensityMin, ref _combination.generationIntensityMax, 0f, 2f);
            DrawRange("Tone Contrast", ref _combination.generationContrastMin, ref _combination.generationContrastMax, 0.1f, 8f);
            _combination.Clamp();
        }

        private void DrawTileabilityGenerationControls()
        {
            using (BeginInspectorSection("Tileability", UtilityWindowTheme.Blue, _tileabilityMode.ToString(), TextureButtonTone.Ghost))
            {
                EditorGUI.BeginChangeCheck();
                _tileabilityMode = (TextureTileabilityMode)EditorGUILayout.EnumPopup(new GUIContent("Mode", "Off, tiled preview only, or generation-time seamless wrapping."), _tileabilityMode);
                using (new EditorGUI.DisabledScope(_tileabilityMode != TextureTileabilityMode.GenerateSeamless))
                {
                    _combination.wrapStampsAcrossEdges = EditorGUILayout.Toggle(new GUIContent("Wrap Stamps", "Draw stamp counterparts across texture edges."), _combination.wrapStampsAcrossEdges);
                    _combination.toroidalSpacing = EditorGUILayout.Toggle(new GUIContent("Toroidal Spacing", "Measure scatter spacing across opposite edges."), _combination.toroidalSpacing);
                    _combination.seamRepairStrength = EditorGUILayout.Slider(new GUIContent("Seam Repair", "Lightly blends opposing edge bands after refinement."), _combination.seamRepairStrength, 0f, 1f);
                    _combination.edgeMatchWeight = EditorGUILayout.Slider(new GUIContent("Edge Match", "How strongly edge bands are pulled together."), _combination.edgeMatchWeight, 0f, 1f);
                    _combination.seamScoreThreshold = EditorGUILayout.Slider(new GUIContent("Target Score", "Reference threshold for future constraint matching."), _combination.seamScoreThreshold, 0f, 1f);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    SyncTileabilitySettings();
                    MarkDirty("Updated tileability settings.");
                }

                DrawInlineStatus(_tileabilityMode == TextureTileabilityMode.GenerateSeamless
                    ? "New textures use wrapped stamps, toroidal spacing, and seam repair."
                    : "Preview mode repeats the texture without changing generation.", UtilityWindowTheme.Neutral);
            }
        }

        private void DrawSelectedVariantInspector(float width)
        {
            if (_candidates.Count == 0)
            {
                DrawInlineStatus("Generate a texture grid to edit variant parameters.", UtilityWindowTheme.Neutral);
                return;
            }

            if (_selectedCandidates.Count != 1)
            {
                using (BeginInspectorSection("Selection", UtilityWindowTheme.Purple, _selectedCandidates.Count == 0 ? "none" : "multi", TextureButtonTone.Secondary))
                {
                    if (_selectedCandidates.Count == 0)
                    {
                        DrawInlineStatus("Select one texture for detailed controls, or select multiple to lock, export, or influence generation.", UtilityWindowTheme.Neutral);
                    }
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        float influence = EditorGUILayout.Slider("Guide Strength", AverageSelectedInfluence(), 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                            SetSelectedInfluence(influence);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (StudioButton("Use as Guide", UtilityWindowTheme.Green, TextureButtonTone.Secondary, GUILayout.Height(24f)))
                                ToggleSelectedVariantLocks();
                            if (StudioButton("Export", ExportTint(), TextureButtonTone.Secondary, GUILayout.Height(24f)))
                                ExportMaps();
                        }
                    }
                }
                return;
            }

            int index = FirstSelectedVariantIndex();
            if (index < 0 || index >= _candidates.Count)
                return;

            ProceduralTextureCandidate variant = _candidates[index];
            ProceduralTextureBaseSettings editableBase = FirstEditableBase(variant);
            if (editableBase == null)
                return;

            using (BeginInspectorSection(variant.label, ComposeTint(), variant.locked ? "locked" : "unlocked", TextureButtonTone.Secondary))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool locked = GUILayout.Toggle(variant.locked, new GUIContent("Use as Guide", "Promote this texture into the Guide shelf on Generate."), EditorStyles.miniButton, GUILayout.Height(22f));
                    if (locked != variant.locked)
                    {
                        variant.locked = locked;
                        variant.guidanceMode = locked ? ProceduralTextureGuidanceMode.Guide : ProceduralTextureGuidanceMode.None;
                        _lastStatus = locked ? "Texture will become a Guide on Generate." : "Texture removed from Guide state.";
                        SaveSession();
                    }
                    EditorGUILayout.LabelField("Strength", GUILayout.Width(58f));
                    variant.influenceWeight = EditorGUILayout.Slider(variant.influenceWeight, 0f, 1f);
                    variant.guideStrength01 = Mathf.Clamp01(variant.influenceWeight);
                }
            }

            DrawCompactBaseControls(editableBase);
        }

        private void DrawCompactBaseControls(ProceduralTextureBaseSettings textureBase)
        {
            if (textureBase.locks == null)
                textureBase.locks = new ProceduralTextureBaseLockSettings();

            EditorGUI.BeginChangeCheck();
            DrawBlendRow(textureBase);
            DrawPatternStrip(textureBase);
            DrawStampPad(textureBase);
            DrawAdvancedBaseFoldout(textureBase);
            if (EditorGUI.EndChangeCheck())
            {
                textureBase.Clamp();
                RegenerateSelectedVariant("Updated texture parameters.");
            }
        }

        private int FirstSelectedVariantIndex()
        {
            foreach (int index in _selectedCandidates)
                return index;
            return IsValidActiveCandidate() ? _activeCandidateIndex : -1;
        }

        private ProceduralTextureBaseSettings FirstEditableBase(ProceduralTextureCandidate variant)
        {
            if (variant == null)
                return null;

            if (variant.bases == null || variant.bases.Length == 0)
                variant.bases = ProceduralTextureCombinationUtility.CloneBases(_bases);
            if (variant.bases == null || variant.bases.Length == 0)
                variant.bases = new[] { ProceduralTextureBaseSettings.CreateDefault(0) };
            return variant.bases[0];
        }

        private float AverageSelectedInfluence()
        {
            if (_selectedCandidates.Count == 0)
                return 1f;

            float total = 0f;
            int count = 0;
            foreach (int index in _selectedCandidates)
            {
                if (index < 0 || index >= _candidates.Count)
                    continue;
                total += Mathf.Clamp01(_candidates[index].influenceWeight);
                count++;
            }
            return count > 0 ? total / count : 1f;
        }

        private void SetSelectedInfluence(float influence)
        {
            foreach (int index in _selectedCandidates)
            {
                if (index >= 0 && index < _candidates.Count)
                {
                    _candidates[index].influenceWeight = Mathf.Clamp01(influence);
                    _candidates[index].guideStrength01 = Mathf.Clamp01(influence);
                }
            }
            SaveSession();
            Repaint();
        }

        private static int PreservedTraitCount(ProceduralTextureCandidate candidate)
        {
            if (candidate == null)
                return 0;
            int count = 0;
            if (candidate.preserveStructure) count++;
            if (candidate.preserveDensity) count++;
            if (candidate.preserveScale) count++;
            if (candidate.preserveDetail) count++;
            if (candidate.preserveTone) count++;
            if (candidate.preserveColourOrPalette) count++;
            if (candidate.preserveStampShape) count++;
            if (candidate.preserveBlendOrder) count++;
            if (candidate.preserveSeam) count++;
            return count;
        }

        private static bool GetPreserveTrait(ProceduralTextureCandidate candidate, ProceduralTextureTrait trait)
        {
            if (candidate == null)
                return false;
            switch (trait)
            {
                case ProceduralTextureTrait.Structure:
                    return candidate.preserveStructure;
                case ProceduralTextureTrait.Density:
                    return candidate.preserveDensity;
                case ProceduralTextureTrait.Scale:
                    return candidate.preserveScale;
                case ProceduralTextureTrait.Detail:
                    return candidate.preserveDetail;
                case ProceduralTextureTrait.Tone:
                    return candidate.preserveTone;
                case ProceduralTextureTrait.Colour:
                    return candidate.preserveColourOrPalette;
                case ProceduralTextureTrait.StampShape:
                    return candidate.preserveStampShape;
                case ProceduralTextureTrait.BlendOrder:
                    return candidate.preserveBlendOrder;
                case ProceduralTextureTrait.Seam:
                    return candidate.preserveSeam;
                default:
                    return false;
            }
        }

        private static void SetPreserveTrait(ProceduralTextureCandidate candidate, ProceduralTextureTrait trait, bool value)
        {
            if (candidate == null)
                return;
            switch (trait)
            {
                case ProceduralTextureTrait.Structure:
                    candidate.preserveStructure = value;
                    break;
                case ProceduralTextureTrait.Density:
                    candidate.preserveDensity = value;
                    break;
                case ProceduralTextureTrait.Scale:
                    candidate.preserveScale = value;
                    break;
                case ProceduralTextureTrait.Detail:
                    candidate.preserveDetail = value;
                    break;
                case ProceduralTextureTrait.Tone:
                    candidate.preserveTone = value;
                    break;
                case ProceduralTextureTrait.Colour:
                    candidate.preserveColourOrPalette = value;
                    break;
                case ProceduralTextureTrait.StampShape:
                    candidate.preserveStampShape = value;
                    break;
                case ProceduralTextureTrait.BlendOrder:
                    candidate.preserveBlendOrder = value;
                    break;
                case ProceduralTextureTrait.Seam:
                    candidate.preserveSeam = value;
                    break;
            }
        }

        private static void SetCorePreserveTraits(ProceduralTextureCandidate candidate, bool value)
        {
            if (candidate == null)
                return;
            candidate.preserveStructure = value;
            candidate.preserveDensity = value;
            candidate.preserveScale = value;
            candidate.preserveTone = value;
        }

        private static void SetAllPreserveTraits(ProceduralTextureCandidate candidate, bool value)
        {
            if (candidate == null)
                return;
            candidate.preserveStructure = value;
            candidate.preserveDensity = value;
            candidate.preserveScale = value;
            candidate.preserveDetail = value;
            candidate.preserveTone = value;
            candidate.preserveColourOrPalette = value;
            candidate.preserveStampShape = value;
            candidate.preserveBlendOrder = value;
            candidate.preserveSeam = value;
        }

        private void DrawSelectedCandidateInfluenceStrip(float width)
        {
            if (_selectedCandidates.Count == 0)
                return;

            using (BeginInspectorSection("Selected Textures", UtilityWindowTheme.Green, $"{_selectedCandidates.Count}", TextureButtonTone.Ghost))
            {
                float tileSize = 46f;
                float columnWidth = tileSize + BaseGap;
                int columns = Mathf.Max(1, Mathf.FloorToInt(width / columnWidth));
                int visibleColumns = Mathf.Min(columns, Mathf.CeilToInt(_selectedCandidates.Count / 2f));
                Rect area = GUILayoutUtility.GetRect(width, tileSize * 2f + BaseGap, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color(0.06f, 0.06f, 0.065f, 0.34f) : new Color(0.76f, 0.77f, 0.79f, 0.22f));

                int order = 0;
                foreach (int candidateIndex in _selectedCandidates)
                {
                    if (candidateIndex < 0 || candidateIndex >= _candidates.Count)
                        continue;

                    int pairColumn = order / 2;
                    int pairRow = order % 2;
                    if (pairColumn >= visibleColumns)
                        break;

                    Rect tile = new Rect(area.x + 4f + pairColumn * columnWidth, area.y + 4f + pairRow * (tileSize + 2f), tileSize, tileSize);
                    DrawCandidateInfluenceTile(candidateIndex, tile);
                    order++;
                }
            }
        }

        private void DrawCandidateInfluenceTile(int candidateIndex, Rect tile)
        {
            ProceduralTextureCandidate candidate = _candidates[candidateIndex];
            DrawCheckerBackground(tile);
            if (candidate.preview != null)
                GUI.DrawTexture(FitRect(tile, candidate.preview.width, candidate.preview.height), candidate.preview, ScaleMode.ScaleToFit, true);
            DrawStudioBox(tile, Color.clear, PanelBorder(candidateIndex == _activeCandidateIndex ? UtilityWindowTheme.Blue : UtilityWindowTheme.Green, 0.82f));
        }

        private void DrawBaseGrid(float width)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + BaseGap) / (BaseMinWidth + BaseGap)));
            float tileWidth = Mathf.Floor((width - (columns - 1) * BaseGap) / columns);
            float tileHeight = Mathf.Clamp(tileWidth * 0.92f, 136f, 260f);
            int rows = Mathf.CeilToInt(_bases.Count / (float)columns);
            float contentHeight = Mathf.Max(140f, rows * tileHeight + Mathf.Max(0, rows - 1) * BaseGap);
            float reservedInfluence = position.height < 640f ? 0f : 190f;
            float viewportHeight = Mathf.Min(Mathf.Max(180f, contentHeight), Mathf.Max(220f, position.height - reservedInfluence - 250f));

            Rect viewport = GUILayoutUtility.GetRect(width, viewportHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(viewport, EditorGUIUtility.isProSkin ? new Color(0.06f, 0.06f, 0.065f, 0.42f) : new Color(0.76f, 0.77f, 0.79f, 0.28f));

            _lastBaseRects.Clear();
            Rect view = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 16f), Mathf.Max(viewport.height, contentHeight));
            _baseGridScroll = GUI.BeginScrollView(viewport, _baseGridScroll, view, false, contentHeight > viewport.height);
            for (int index = 0; index < _bases.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                Rect rect = new Rect(column * (tileWidth + BaseGap), row * (tileHeight + BaseGap), tileWidth, tileHeight);
                _lastBaseRects[index] = rect;
                DrawBaseCard(index, rect);
            }
            DrawBaseDragMarker();
            GUI.EndScrollView();
        }

        private void DrawBaseCard(int index, Rect rect)
        {
            ProceduralTextureBaseSettings textureBase = _bases[index];
            bool selected = IsBaseSelected(index);
            bool primary = index == _selectedBase;
            bool hover = rect.Contains(Event.current.mousePosition);
            Color tint = textureBase.enabled ? (selected ? ComposeTint() : UtilityWindowTheme.Neutral) : UtilityWindowTheme.Neutral;

            if (Event.current.type == EventType.Repaint)
            {
                DrawStudioBox(rect, PanelFill(tint, selected ? 0.12f : 0.055f), PanelBorder(tint, selected ? 0.78f : hover ? 0.38f : 0.20f));
                if (primary)
                    DrawStudioBox(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), Color.clear, PanelBorder(ComposeTint(), 0.48f));
                if (!textureBase.enabled)
                    EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.34f) : new Color(1f, 1f, 1f, 0.32f));
            }

            Rect preview = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
            DrawBasePreview(index, preview);

            Rect enabledRect = Rect.zero;
            Rect modeRect = Rect.zero;
            Rect lockRect = Rect.zero;
            Rect randomRect = Rect.zero;
            Rect menuRect = Rect.zero;
            if (hover || selected)
            {
                DrawBaseCardOverlay(index, rect, out enabledRect, out modeRect, out lockRect, out randomRect, out menuRect);
            }

            HandleBaseCardInput(index, rect, enabledRect, modeRect, lockRect, randomRect, menuRect);
        }

        private void DrawBaseCardOverlay(int index, Rect rect, out Rect enabledRect, out Rect modeRect, out Rect lockRect, out Rect randomRect, out Rect menuRect)
        {
            ProceduralTextureBaseSettings textureBase = _bases[index];
            Color shade = EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.46f) : new Color(1f, 1f, 1f, 0.54f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 30f), shade);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 56f, rect.width, 56f), shade);

            GUI.Label(new Rect(rect.x + 7f, rect.y + 6f, 18f, 18f), new GUIContent("||", "Drag to reorder."), EditorStyles.miniBoldLabel);
            enabledRect = new Rect(rect.xMax - 58f, rect.y + 5f, 52f, 20f);
            bool enabled = GUI.Toggle(enabledRect, textureBase.enabled, textureBase.enabled ? "On" : "Off", EditorStyles.miniButton);
            if (enabled != textureBase.enabled)
            {
                textureBase.enabled = enabled;
                MarkDirty(enabled ? "Enabled base." : "Disabled base.");
            }

            modeRect = new Rect(rect.x + 28f, rect.y + 5f, Mathf.Max(86f, rect.width - 148f), 20f);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(textureBase.locks != null && textureBase.locks.pattern))
                textureBase.generation.pattern = (ProceduralTexturePattern)EditorGUI.EnumPopup(modeRect, GUIContent.none, textureBase.generation.pattern);
            if (EditorGUI.EndChangeCheck())
                MarkDirty("Changed base placement.");

            float y = rect.yMax - 50f;
            GUI.Label(new Rect(rect.x + 7f, y, rect.width - 14f, 18f), textureBase.name, EditorStyles.miniBoldLabel);
            float compactWidth = rect.width < 230f ? 48f : 58f;
            lockRect = new Rect(rect.x + 7f, y + 23f, compactWidth, 20f);
            randomRect = new Rect(lockRect.xMax + 4f, lockRect.y, compactWidth, 20f);
            menuRect = new Rect(rect.xMax - compactWidth - 7f, lockRect.y, compactWidth, 20f);
            Rect blendRect = new Rect(randomRect.xMax + 4f, lockRect.y, Mathf.Max(58f, menuRect.x - randomRect.xMax - 8f), 20f);

            if (GUI.Button(lockRect, new GUIContent("Locks", "Toggle lock groups for this base."), EditorStyles.miniButton))
                ShowBaseLockMenu(index);
            if (GUI.Button(randomRect, new GUIContent("Rand", "Randomize unlocked parameters on this base."), EditorStyles.miniButton))
                RandomizeBaseParameters(textureBase);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(textureBase.locks != null && textureBase.locks.blend))
                textureBase.blendMode = DrawBlendModePopup(blendRect, GUIContent.none, textureBase.blendMode);
            if (EditorGUI.EndChangeCheck())
                MarkDirty("Changed base blend.");

            if (GUI.Button(menuRect, new GUIContent("More", "Base actions."), EditorStyles.miniButton))
                ShowBaseMenu(index);
        }

        private void DrawSelectedBaseEditor()
        {
            if (_selectedBase < 0 || _selectedBase >= _bases.Count)
                return;

            ProceduralTextureBaseSettings textureBase = _bases[_selectedBase];
            using (BeginInspectorSection("Selected Layer", ComposeTint(), textureBase.enabled ? "enabled" : "muted", TextureButtonTone.Secondary))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool allLocked = AreAllBaseLocksEnabled(textureBase);
                    EditorGUI.BeginChangeCheck();
                    bool nextAllLocked = GUILayout.Toggle(allLocked, new GUIContent("Lock All", "Lock or unlock every editable group on this layer."), EditorStyles.miniButton, GUILayout.Height(22f), GUILayout.Width(74f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetAllBaseLocks(_selectedBase, nextAllLocked);
                        MarkDirty(nextAllLocked ? "Locked selected layer." : "Unlocked selected layer.");
                    }
                    bool sectionsOpen = AreManualLayerFoldoutsOpen();
                    if (GUILayout.Button(new GUIContent(sectionsOpen ? "Close All" : "Open All", sectionsOpen ? "Close all selected-layer sections." : "Open all selected-layer sections."), EditorStyles.miniButton, GUILayout.Height(22f), GUILayout.Width(74f)))
                        SetManualLayerFoldouts(!sectionsOpen);
                    if (GUILayout.Button(new GUIContent("Randomize Unlocked", "Randomize only unlocked parameters on this layer."), EditorStyles.miniButton, GUILayout.Height(22f)))
                        RandomizeBaseParameters(textureBase);
                    if (GUILayout.Button(new GUIContent("Reset Layer", "Reset this layer to a default procedural layer."), EditorStyles.miniButton, GUILayout.Height(22f), GUILayout.Width(78f)))
                    {
                        _bases[_selectedBase] = ProceduralTextureBaseSettings.CreateDefault(_selectedBase);
                        SelectSingleBase(_selectedBase);
                        MarkDirty("Reset selected layer.");
                        return;
                    }
                }

                EditorGUI.BeginChangeCheck();
                DrawManualBlendSection(textureBase);
                if (!textureBase.useBakedValues)
                {
                    DrawManualStampSection(textureBase);
                    DrawManualPlacementSection(textureBase);
                }
                DrawManualSourceSection(textureBase);

                if (EditorGUI.EndChangeCheck())
                    MarkDirty("Layer changed.");
            }
        }

        private void DrawManualBlendSection(ProceduralTextureBaseSettings textureBase)
        {
            if (!DrawManualSectionHeader("Blend", ref _manualFoldoutBlend, ref textureBase.locks.blend, "Lock layer blend mode and opacity."))
                return;

            DrawManualLockedTextField("Name", ref textureBase.name, false);
            DrawManualLockedBlendMode("Blend", ref textureBase.blendMode, ref textureBase.locks.blend);
            DrawManualLockedSlider("Opacity", ref textureBase.weight, 0f, 1f, ref textureBase.locks.blend);
        }

        private void DrawManualPlacementSection(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureGenerationSettings settings = textureBase.generation;
            bool sectionLock = textureBase.locks.pattern && textureBase.locks.seed;
            if (!DrawManualSectionHeader("Placement", ref _manualFoldoutPattern, ref sectionLock, "Lock placement mode and seed."))
                return;
            if (sectionLock != (textureBase.locks.pattern && textureBase.locks.seed))
            {
                textureBase.locks.pattern = sectionLock;
                textureBase.locks.seed = sectionLock;
            }

            DrawManualLockedEnum("Placement", ref settings.pattern, ref textureBase.locks.pattern);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.seed))
                    settings.seed = EditorGUILayout.IntField("Seed", settings.seed);
                using (new EditorGUI.DisabledScope(textureBase.locks.seed))
                {
                    if (GUILayout.Button(new GUIContent("Random", "Randomize this layer seed."), EditorStyles.miniButton, GUILayout.Width(58f)))
                        settings.seed = NewEditorSeed(settings.seed);
                }
                textureBase.locks.seed = EditorGUILayout.Toggle(textureBase.locks.seed, GUILayout.Width(18f));
            }

            DrawManualLockedIntSlider("Count", ref settings.count, 1, 512, ref textureBase.locks.density);
            DrawManualLockedSlider("Spacing", ref settings.minSpacing01, 0f, 0.5f, ref textureBase.locks.density);
            DrawManualLockedSlider("Jitter", ref settings.globalJitter01, 0f, 1f, ref textureBase.locks.density);

            if (HasPlacementDetails(settings.pattern))
                DrawManualPlacementDetailsSubsection(textureBase);
        }

        private void DrawManualStampSection(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureGenerationSettings settings = textureBase.generation;
            if (!DrawManualSectionHeader("Shape", ref _manualFoldoutStamp, ref textureBase.locks.stamp, "Lock shape, radius, intensity, contrast, and edge."))
                return;

            DrawManualLockedEnum("Shape", ref settings.stampShape, ref textureBase.locks.stamp);
            using (new EditorGUI.DisabledScope(textureBase.locks.stamp))
            {
                DrawRange("Radius", ref settings.radiusMin01, ref settings.radiusMax01, 0.001f, 0.5f);
                DrawRange("Intensity", ref settings.intensityMin, ref settings.intensityMax, 0f, 2f);
            }
            DrawManualLockedSlider("Contrast", ref settings.contrast, 0.1f, 8f, ref textureBase.locks.stamp);
            DrawManualLockedSlider("Edge", ref settings.edgeFalloff, 0f, 1f, ref textureBase.locks.stamp);
        }

        private void DrawManualPlacementDetailsSubsection(ProceduralTextureBaseSettings textureBase)
        {
            EditorGUILayout.Space(2f);
            if (!DrawManualSectionHeader("Placement Details", ref _manualFoldoutPatternDetails, ref textureBase.locks.patternSpecific, "Lock placement-specific controls."))
                return;

            using (new EditorGUI.DisabledScope(textureBase.locks.patternSpecific))
                DrawPatternSpecificControls(textureBase.generation);
        }

        private void DrawManualSourceSection(ProceduralTextureBaseSettings textureBase)
        {
            if (!DrawManualSectionHeader("Texture Inputs", ref _manualFoldoutSource, ref textureBase.locks.source, "Lock texture input and baked source settings."))
                return;

            using (new EditorGUI.DisabledScope(textureBase.locks.source))
            {
                if (textureBase.useBakedValues)
                    DrawInlineStatus($"Texture layer {textureBase.bakedWidth}x{textureBase.bakedHeight}. Blend and opacity remain editable.", UtilityWindowTheme.Green);
                else
                    DrawStampSpecificControls(textureBase.generation);
            }
        }

        private bool DrawManualSectionHeader(string label, ref bool foldout, ref bool sectionLock, string tooltip)
        {
            Rect rect = GUILayoutUtility.GetRect(18f, 22f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Neutral, 0.055f), PanelBorder(UtilityWindowTheme.Neutral, 0.18f));
            Rect foldRect = new Rect(rect.x + 5f, rect.y + 2f, rect.width - 34f, 18f);
            Rect lockRect = new Rect(rect.xMax - 24f, rect.y + 3f, 18f, 18f);
            foldout = EditorGUI.Foldout(foldRect, foldout, label, true);
            sectionLock = GUI.Toggle(lockRect, sectionLock, new GUIContent(string.Empty, tooltip));
            return foldout;
        }

        private void DrawManualLockedTextField(string label, ref string value, bool locked)
        {
            using (new EditorGUI.DisabledScope(locked))
                value = EditorGUILayout.TextField(label, value);
        }

        private void DrawManualLockedSlider(string label, ref float value, float min, float max, ref bool locked)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(locked))
                    value = EditorGUILayout.Slider(label, value, min, max);
                locked = EditorGUILayout.Toggle(locked, GUILayout.Width(18f));
            }
        }

        private void DrawManualLockedIntSlider(string label, ref int value, int min, int max, ref bool locked)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(locked))
                    value = EditorGUILayout.IntSlider(label, value, min, max);
                locked = EditorGUILayout.Toggle(locked, GUILayout.Width(18f));
            }
        }

        private void DrawManualLockedEnum<TEnum>(string label, ref TEnum value, ref bool locked) where TEnum : Enum
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(locked))
                    value = (TEnum)EditorGUILayout.EnumPopup(label, value);
                locked = EditorGUILayout.Toggle(locked, GUILayout.Width(18f));
            }
        }

        private void DrawManualLockedBlendMode(string label, ref ProceduralTextureBlendMode value, ref bool locked)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(locked))
                    value = DrawBlendModePopup(EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight), new GUIContent(label, BlendModeTooltip(value)), value);
                locked = EditorGUILayout.Toggle(locked, GUILayout.Width(18f));
            }
        }

        private ProceduralTextureBlendMode DrawBlendModePopup(Rect rect, GUIContent label, ProceduralTextureBlendMode value)
        {
            ProceduralTextureBlendMode[] values =
            {
                ProceduralTextureBlendMode.Replace,
                ProceduralTextureBlendMode.Max,
                ProceduralTextureBlendMode.Add,
                ProceduralTextureBlendMode.Multiply,
                ProceduralTextureBlendMode.Subtract,
                ProceduralTextureBlendMode.Overlay
            };
            GUIContent[] labels =
            {
                new GUIContent("Normal", "Draw this layer over the previous result."),
                new GUIContent("Max", "Keep the brighter value from this layer or the previous result."),
                new GUIContent("Add", "Add this layer onto the previous result."),
                new GUIContent("Multiply", "Darken by multiplying this layer with the previous result."),
                new GUIContent("Subtract", "Subtract this layer from the previous result."),
                new GUIContent("Overlay", "Blend this layer with contrast-sensitive overlay behaviour.")
            };

            int index = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                {
                    index = i;
                    break;
                }
            }

            int next = EditorGUI.Popup(rect, label, index, labels);
            return values[Mathf.Clamp(next, 0, values.Length - 1)];
        }

        private static string BlendModeTooltip(ProceduralTextureBlendMode mode)
        {
            switch (mode)
            {
                case ProceduralTextureBlendMode.Replace:
                    return "Normal: draw this layer over the previous result.";
                case ProceduralTextureBlendMode.Max:
                    return "Max: keep the brighter value from this layer or the previous result.";
                case ProceduralTextureBlendMode.Add:
                    return "Add: add this layer onto the previous result.";
                case ProceduralTextureBlendMode.Multiply:
                    return "Multiply: darken by multiplying this layer with the previous result.";
                case ProceduralTextureBlendMode.Subtract:
                    return "Subtract: subtract this layer from the previous result.";
                case ProceduralTextureBlendMode.Overlay:
                    return "Overlay: blend this layer with contrast-sensitive overlay behaviour.";
                default:
                    return string.Empty;
            }
        }

        private static string BlendModeLabel(ProceduralTextureBlendMode mode)
        {
            return mode == ProceduralTextureBlendMode.Replace ? "Normal" : ObjectNames.NicifyVariableName(mode.ToString());
        }

        private static bool HasPlacementDetails(ProceduralTexturePattern pattern)
        {
            switch (pattern)
            {
                case ProceduralTexturePattern.StratifiedJitterGrid:
                case ProceduralTexturePattern.HexGrid:
                case ProceduralTexturePattern.PoissonDisk:
                case ProceduralTexturePattern.DensityMap:
                case ProceduralTexturePattern.PolarPattern:
                    return true;
                default:
                    return false;
            }
        }

        private static bool AreAllBaseLocksEnabled(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureBaseLockSettings locks = textureBase?.locks;
            return locks != null
                && locks.seed
                && locks.pattern
                && locks.density
                && locks.stamp
                && locks.patternSpecific
                && locks.source
                && locks.blend;
        }

        private void SetManualLayerFoldouts(bool open)
        {
            _manualFoldoutBlend = open;
            _manualFoldoutStamp = open;
            _manualFoldoutPattern = open;
            _manualFoldoutPatternDetails = open;
            _manualFoldoutSource = open;
            Repaint();
        }

        private bool AreManualLayerFoldoutsOpen()
        {
            return _manualFoldoutBlend
                && _manualFoldoutStamp
                && _manualFoldoutPattern
                && _manualFoldoutSource
                && (!(_selectedBase >= 0 && _selectedBase < _bases.Count && _bases[_selectedBase] != null && !_bases[_selectedBase].useBakedValues && HasPlacementDetails(_bases[_selectedBase].generation.pattern)) || _manualFoldoutPatternDetails);
        }

        private void DrawBlendRow(ProceduralTextureBaseSettings textureBase)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                textureBase.name = EditorGUILayout.TextField(textureBase.name);
                textureBase.enabled = EditorGUILayout.Toggle(textureBase.enabled, GUILayout.Width(18f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.blend))
                {
                    textureBase.blendMode = DrawBlendModePopup(EditorGUILayout.GetControlRect(GUILayout.Width(98f)), GUIContent.none, textureBase.blendMode);
                    EditorGUILayout.LabelField("Opacity", GUILayout.Width(48f));
                    textureBase.weight = EditorGUILayout.Slider(textureBase.weight, 0f, 1f);
                }
                textureBase.locks.blend = GUILayout.Toggle(textureBase.locks.blend, new GUIContent("L", "Lock blend and opacity."), EditorStyles.miniButton, GUILayout.Width(24f));
            }
        }

        private void DrawPatternStrip(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureGenerationSettings settings = textureBase.generation;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.pattern))
                    settings.pattern = (ProceduralTexturePattern)EditorGUILayout.EnumPopup(settings.pattern);
                textureBase.locks.pattern = GUILayout.Toggle(textureBase.locks.pattern, new GUIContent("L", "Lock placement mode."), EditorStyles.miniButton, GUILayout.Width(24f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.seed))
                    settings.seed = EditorGUILayout.IntField(settings.seed);
                if (GUILayout.Button(new GUIContent("Seed", "Randomize seed."), EditorStyles.miniButton, GUILayout.Width(48f)) && !textureBase.locks.seed)
                    settings.seed = NewEditorSeed(settings.seed);
                textureBase.locks.seed = GUILayout.Toggle(textureBase.locks.seed, new GUIContent("L", "Lock seed."), EditorStyles.miniButton, GUILayout.Width(24f));
            }

            using (new EditorGUI.DisabledScope(textureBase.locks.density))
            {
                settings.count = EditorGUILayout.IntSlider("Count", settings.count, 1, 512);
                settings.minSpacing01 = EditorGUILayout.Slider("Spacing", settings.minSpacing01, 0f, 0.5f);
                settings.globalJitter01 = EditorGUILayout.Slider("Jitter", settings.globalJitter01, 0f, 1f);
            }
            textureBase.locks.density = GUILayout.Toggle(textureBase.locks.density, new GUIContent("Lock Amount / Spacing", "Lock count, spacing, and jitter."), EditorStyles.miniButton);
        }

        private void DrawStampPad(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureGenerationSettings settings = textureBase.generation;
            using (new EditorGUI.DisabledScope(textureBase.locks.stamp))
            {
                settings.stampShape = (ProceduralTextureStampShape)EditorGUILayout.EnumPopup("Shape", settings.stampShape);
                DrawRange("Radius", ref settings.radiusMin01, ref settings.radiusMax01, 0.001f, 0.5f);
                DrawRange("Intensity", ref settings.intensityMin, ref settings.intensityMax, 0f, 2f);
                settings.contrast = EditorGUILayout.Slider("Contrast", settings.contrast, 0.1f, 8f);
                settings.edgeFalloff = EditorGUILayout.Slider("Edge", settings.edgeFalloff, 0f, 1f);
            }
            textureBase.locks.stamp = GUILayout.Toggle(textureBase.locks.stamp, new GUIContent("Lock Shape", "Lock shape, radius, intensity, contrast, and edge."), EditorStyles.miniButton);
        }

        private void DrawAdvancedBaseFoldout(ProceduralTextureBaseSettings textureBase)
        {
            using (new EditorGUI.DisabledScope(textureBase.locks.patternSpecific))
                DrawPatternSpecificControls(textureBase.generation);
            textureBase.locks.patternSpecific = GUILayout.Toggle(textureBase.locks.patternSpecific, new GUIContent("Lock Placement Details", "Lock mode-specific parameters."), EditorStyles.miniButton);

            using (new EditorGUI.DisabledScope(textureBase.locks.source))
            {
                DrawStampSpecificControls(textureBase.generation);
                if (textureBase.useBakedValues)
                    DrawInlineStatus($"Baked candidate source {textureBase.bakedWidth}x{textureBase.bakedHeight}.", UtilityWindowTheme.Green);
            }
            textureBase.locks.source = GUILayout.Toggle(textureBase.locks.source, new GUIContent("Lock Source", "Lock texture source and baked source settings."), EditorStyles.miniButton);
        }

        private void DrawBaseCoreControls(ProceduralTextureGenerationSettings settings)
        {
            settings.count = EditorGUILayout.IntSlider("Count", settings.count, 1, 512);
            settings.minSpacing01 = EditorGUILayout.Slider("Min Spacing", settings.minSpacing01, 0f, 0.5f);
            settings.globalJitter01 = EditorGUILayout.Slider("Jitter", settings.globalJitter01, 0f, 1f);
            settings.stampShape = (ProceduralTextureStampShape)EditorGUILayout.EnumPopup("Shape", settings.stampShape);
            DrawRange("Radius", ref settings.radiusMin01, ref settings.radiusMax01, 0.001f, 0.5f);
            DrawRange("Intensity", ref settings.intensityMin, ref settings.intensityMax, 0f, 2f);
            settings.contrast = EditorGUILayout.Slider("Shape Contrast", settings.contrast, 0.1f, 8f);
            settings.edgeFalloff = EditorGUILayout.Slider("Edge Falloff", settings.edgeFalloff, 0f, 1f);
        }

        private void DrawPatternSpecificControls(ProceduralTextureGenerationSettings settings)
        {
            switch (settings.pattern)
            {
                case ProceduralTexturePattern.StratifiedJitterGrid:
                    settings.cellsX = EditorGUILayout.IntSlider("Cells X", settings.cellsX, 1, 64);
                    settings.cellsY = EditorGUILayout.IntSlider("Cells Y", settings.cellsY, 1, 64);
                    settings.cellJitter01 = EditorGUILayout.Slider("Cell Jitter", settings.cellJitter01, 0f, 1f);
                    break;
                case ProceduralTexturePattern.HexGrid:
                    settings.hexRadius01 = EditorGUILayout.Slider("Hex Radius", settings.hexRadius01, 0.005f, 0.25f);
                    settings.hexJitter01 = EditorGUILayout.Slider("Hex Jitter", settings.hexJitter01, 0f, 1f);
                    settings.hexPointyTop = EditorGUILayout.Toggle("Pointy Top", settings.hexPointyTop);
                    break;
                case ProceduralTexturePattern.PoissonDisk:
                    settings.poissonRadius01 = EditorGUILayout.Slider("Poisson Radius", settings.poissonRadius01, 0.005f, 0.5f);
                    settings.poissonAttempts = EditorGUILayout.IntSlider("Attempts", settings.poissonAttempts, 1, 80);
                    settings.poissonHardCap = EditorGUILayout.IntSlider("Hard Cap", settings.poissonHardCap, 0, 1024);
                    break;
                case ProceduralTexturePattern.DensityMap:
                    settings.densityMap = (Texture2D)EditorGUILayout.ObjectField("Density Map", settings.densityMap, typeof(Texture2D), false);
                    settings.densityChannel = (ProceduralTextureChannel)EditorGUILayout.EnumPopup("Channel", settings.densityChannel);
                    settings.densityCurve = EditorGUILayout.CurveField("Density Curve", settings.densityCurve);
                    settings.densityTiling = EditorGUILayout.Vector2Field("Tiling", settings.densityTiling);
                    settings.densityOffset = EditorGUILayout.Vector2Field("Offset", settings.densityOffset);
                    settings.densityThreshold01 = EditorGUILayout.Slider("Threshold", settings.densityThreshold01, 0f, 1f);
                    break;
                case ProceduralTexturePattern.PolarPattern:
                    settings.polarRings = EditorGUILayout.IntSlider("Rings", settings.polarRings, 1, 32);
                    settings.polarSpokesPerRing = EditorGUILayout.IntSlider("Spokes", settings.polarSpokesPerRing, 1, 128);
                    settings.polarRingStep01 = EditorGUILayout.Slider("Ring Step", settings.polarRingStep01, 0.005f, 0.5f);
                    settings.polarRadialJitter01 = EditorGUILayout.Slider("Radial Jitter", settings.polarRadialJitter01, 0f, 1f);
                    settings.polarAngularJitter01 = EditorGUILayout.Slider("Angular Jitter", settings.polarAngularJitter01, 0f, 1f);
                    break;
            }
        }

        private void DrawStampSpecificControls(ProceduralTextureGenerationSettings settings)
        {
            settings.rotationJitterDegrees = EditorGUILayout.Vector2Field("Rotation Jitter", settings.rotationJitterDegrees);
            if (settings.stampShape == ProceduralTextureStampShape.Ring)
                settings.ringThickness01 = EditorGUILayout.Slider("Ring Thickness", settings.ringThickness01, 0f, 1f);
            if (settings.stampShape == ProceduralTextureStampShape.TextureSource)
            {
                settings.stampTexture = (Texture2D)EditorGUILayout.ObjectField("Shape Texture", settings.stampTexture, typeof(Texture2D), false);
                settings.stampTextureChannel = (ProceduralTextureChannel)EditorGUILayout.EnumPopup("Shape Channel", settings.stampTextureChannel);
                settings.invertStampTexture = EditorGUILayout.Toggle("Invert Shape", settings.invertStampTexture);
                settings.stampTextureTiling = EditorGUILayout.Slider("Shape Tiling", settings.stampTextureTiling, 0.05f, 8f);
            }
        }

        private void DrawGenerationInfluence()
        {
            DrawTextureInfluenceQuilt();
        }

        private void DrawGuidedRefineSimilarityRing(Rect rect, ProceduralTextureCandidate target)
        {
            if (target == null)
                return;

            if (_guidedRefineFeaturePreviewDirty || _guidedRefineFeatureCandidateIndex != _activeCandidateIndex)
                QueueGuidedRefineFeaturePreviewRebuild();

            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Blue, 0.055f), PanelBorder(UtilityWindowTheme.Blue, 0.26f));

            Rect content = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);
            float ringSize = Mathf.Min(Mathf.Min(content.width, content.height - 74f), 310f);
            ringSize = Mathf.Max(190f, ringSize);
            Rect ringRect = new Rect(content.center.x - ringSize * 0.5f, content.y + 4f, ringSize, ringSize);
            Vector3 center = new Vector3(ringRect.center.x, ringRect.center.y, 0f);
            float outerRadius = ringSize * 0.48f;
            float innerSize = ringSize * 0.46f;
            Rect textureRect = new Rect(ringRect.center.x - innerSize * 0.5f, ringRect.center.y - innerSize * 0.5f, innerSize, innerSize);

            DrawSimilarityArcs(center, outerRadius, target);
            DrawCheckerBackground(textureRect);
            if (target.preview != null)
                GUI.DrawTexture(FitRect(textureRect, target.preview.width, target.preview.height), target.preview, ScaleMode.ScaleToFit, true);
            DrawStudioBox(textureRect, PanelFill(UtilityWindowTheme.Neutral, 0.08f), PanelBorder(UtilityWindowTheme.Blue, 0.72f));

            GUI.Label(new Rect(textureRect.x, textureRect.yMax + 6f, textureRect.width, 18f), $"{target.label} · {SimilarityBand(target.similarityTarget01)}", CenteredMiniLabel());
            DrawSimilarityRingLabels(ringRect, target);

            Rect stripArea = new Rect(content.x, content.yMax - 58f, content.width, 50f);
            if (_guidedRefineFeaturePreviewDirty || _guidedRefineFeatureEntry == null)
                DrawInlineCenteredMessage(stripArea, "Target trait previews are rebuilding outside repaint.");
            else
                DrawGuidedTraitFeatureStrips(stripArea, target, _guidedRefineFeatureEntry);
        }

        private void DrawSimilarityArcs(Vector3 center, float radius, ProceduralTextureCandidate target)
        {
            Handles.BeginGUI();
            DrawSimilarityArc(center, radius, 0f, UtilityWindowTheme.Cyan, GetPreserveTrait(target, ProceduralTextureTrait.Structure));
            DrawSimilarityArc(center, radius, 40f, UtilityWindowTheme.Green, GetPreserveTrait(target, ProceduralTextureTrait.Density));
            DrawSimilarityArc(center, radius, 80f, UtilityWindowTheme.Blue, GetPreserveTrait(target, ProceduralTextureTrait.Scale));
            DrawSimilarityArc(center, radius, 120f, UtilityWindowTheme.Purple, GetPreserveTrait(target, ProceduralTextureTrait.Detail));
            DrawSimilarityArc(center, radius, 160f, UtilityWindowTheme.Amber, GetPreserveTrait(target, ProceduralTextureTrait.Tone));
            DrawSimilarityArc(center, radius, 200f, UtilityWindowTheme.Cyan, GetPreserveTrait(target, ProceduralTextureTrait.Colour));
            DrawSimilarityArc(center, radius, 240f, UtilityWindowTheme.Green, GetPreserveTrait(target, ProceduralTextureTrait.StampShape));
            DrawSimilarityArc(center, radius, 280f, UtilityWindowTheme.Blue, GetPreserveTrait(target, ProceduralTextureTrait.BlendOrder));
            DrawSimilarityArc(center, radius, 320f, UtilityWindowTheme.Purple, GetPreserveTrait(target, ProceduralTextureTrait.Seam));
            Handles.EndGUI();
        }

        private static void DrawSimilarityArc(Vector3 center, float radius, float startDegrees, Color tint, bool preserved)
        {
            Color color = tint;
            color.a = preserved ? 0.62f : 0.18f;
            Handles.color = color;
            Vector3 from = Quaternion.Euler(0f, 0f, startDegrees) * Vector3.up;
            Handles.DrawSolidArc(center, Vector3.forward, from, 34f, radius);
        }

        private void DrawSimilarityRingLabels(Rect ringRect, ProceduralTextureCandidate target)
        {
            DrawRingLabel(ringRect, "Structure", ProceduralTextureTrait.Structure, target, 0.50f, 0.00f);
            DrawRingLabel(ringRect, "Density", ProceduralTextureTrait.Density, target, 0.82f, 0.11f);
            DrawRingLabel(ringRect, "Scale", ProceduralTextureTrait.Scale, target, 0.96f, 0.44f);
            DrawRingLabel(ringRect, "Detail", ProceduralTextureTrait.Detail, target, 0.83f, 0.80f);
            DrawRingLabel(ringRect, "Tone", ProceduralTextureTrait.Tone, target, 0.50f, 0.93f);
            DrawRingLabel(ringRect, "Colour", ProceduralTextureTrait.Colour, target, 0.16f, 0.80f);
            DrawRingLabel(ringRect, "Stamp", ProceduralTextureTrait.StampShape, target, 0.03f, 0.44f);
            DrawRingLabel(ringRect, "Blend", ProceduralTextureTrait.BlendOrder, target, 0.16f, 0.11f);
            DrawRingLabel(ringRect, "Seam", ProceduralTextureTrait.Seam, target, 0.50f, 0.17f);
        }

        private void DrawRingLabel(Rect ringRect, string label, ProceduralTextureTrait trait, ProceduralTextureCandidate target, float x01, float y01)
        {
            bool preserved = GetPreserveTrait(target, trait);
            Rect labelRect = new Rect(ringRect.x + ringRect.width * x01 - 38f, ringRect.y + ringRect.height * y01 - 9f, 76f, 18f);
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(labelRect, PanelFill(preserved ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, preserved ? 0.20f : 0.08f), PanelBorder(preserved ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, preserved ? 0.55f : 0.20f));
            GUI.Label(labelRect, label, CenteredMiniLabel());
        }

        private void DrawGuidedTraitFeatureStrips(Rect rect, ProceduralTextureCandidate target, InfluenceFeatureEntry entry)
        {
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Neutral, 0.05f), PanelBorder(UtilityWindowTheme.Neutral, 0.18f));

            float gap = 5f;
            float tileWidth = Mathf.Max(44f, (rect.width - gap * 6f) / 7f);
            DrawGuidedFeatureStrip(new Rect(rect.x, rect.y + 4f, tileWidth, rect.height - 8f), "Structure", entry.preview.structureMap, GetPreserveTrait(target, ProceduralTextureTrait.Structure));
            DrawGuidedFeatureStrip(new Rect(rect.x + (tileWidth + gap), rect.y + 4f, tileWidth, rect.height - 8f), "Density", entry.preview.densityMask, GetPreserveTrait(target, ProceduralTextureTrait.Density));
            DrawGuidedFeatureStrip(new Rect(rect.x + (tileWidth + gap) * 2f, rect.y + 4f, tileWidth, rect.height - 8f), "Scale", entry.preview.lowFrequencyMap, GetPreserveTrait(target, ProceduralTextureTrait.Scale));
            DrawGuidedFeatureStrip(new Rect(rect.x + (tileWidth + gap) * 3f, rect.y + 4f, tileWidth, rect.height - 8f), "Detail", entry.preview.highFrequencyMap, GetPreserveTrait(target, ProceduralTextureTrait.Detail));
            DrawGuidedFeatureStrip(new Rect(rect.x + (tileWidth + gap) * 4f, rect.y + 4f, tileWidth, rect.height - 8f), "Tone", entry.preview.toneHistogram, GetPreserveTrait(target, ProceduralTextureTrait.Tone));
            DrawGuidedFeatureStrip(new Rect(rect.x + (tileWidth + gap) * 5f, rect.y + 4f, tileWidth, rect.height - 8f), "Colour", entry.preview.colourRamp, GetPreserveTrait(target, ProceduralTextureTrait.Colour));
            DrawGuidedFeatureStrip(new Rect(rect.x + (tileWidth + gap) * 6f, rect.y + 4f, tileWidth, rect.height - 8f), "Seam", entry.preview.seamEdgeStrip, GetPreserveTrait(target, ProceduralTextureTrait.Seam));
        }

        private void DrawGuidedFeatureStrip(Rect rect, string label, Texture2D texture, bool preserved)
        {
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            DrawStudioBox(rect, Color.clear, PanelBorder(preserved ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, preserved ? 0.60f : 0.22f));
            GUI.Label(new Rect(rect.x + 3f, rect.yMax - 16f, rect.width - 6f, 14f), label, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static GUIStyle CenteredMiniLabel()
        {
            GUIStyle style = new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle);
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private static string SimilarityBand(float similarity)
        {
            similarity = Mathf.Clamp01(similarity);
            if (similarity < 0.25f)
                return "Loose";
            if (similarity < 0.58f)
                return "Balanced";
            if (similarity < 0.84f)
                return "Close";
            return "Near Clone";
        }

        private void QueueGuidedRefineFeaturePreviewRebuild()
        {
            _guidedRefineFeaturePreviewDirty = true;
            EditorApplication.delayCall -= RebuildGuidedRefineFeaturePreview;
            EditorApplication.delayCall += RebuildGuidedRefineFeaturePreview;
        }

        private void RebuildGuidedRefineFeaturePreview()
        {
            if (this == null)
                return;

            DestroyGuidedRefineFeaturePreviewCache();
            _guidedRefineFeatureCandidateIndex = _activeCandidateIndex;
            if (!IsValidActiveCandidate())
            {
                _guidedRefineFeaturePreviewDirty = false;
                Repaint();
                return;
            }

            ProceduralTextureCandidate candidate = _candidates[_activeCandidateIndex];
            if ((candidate.values == null || candidate.values.Length == 0) && candidate.bases != null && candidate.bases.Length > 0)
                GenerateVariantPreviewData(candidate, ActivePreviewSize);
            if (candidate.values != null && candidate.values.Length > 0)
                _guidedRefineFeatureEntry = BuildInfluenceFeatureEntry(candidate);
            _guidedRefineFeaturePreviewDirty = false;
            Repaint();
        }

        private void DestroyGuidedRefineFeaturePreviewCache()
        {
            EditorApplication.delayCall -= RebuildGuidedRefineFeaturePreview;
            if (_guidedRefineFeatureEntry != null)
                DestroyFeaturePreview(_guidedRefineFeatureEntry.preview);
            _guidedRefineFeatureEntry = null;
            _guidedRefineFeatureCandidateIndex = -1;
        }

        private void QueueInfluenceFeaturePreviewRebuild()
        {
            _influenceFeaturePreviewDirty = true;
            EditorApplication.delayCall -= RebuildInfluenceFeaturePreviews;
            EditorApplication.delayCall += RebuildInfluenceFeaturePreviews;
        }

        private void RebuildInfluenceFeaturePreviews()
        {
            if (this == null)
                return;

            DestroyInfluenceFeaturePreviewCache();
            for (int i = 0; i < _influences.Count; i++)
            {
                ProceduralTextureCandidate candidate = _influences[i];
                if (candidate == null)
                    continue;
                if ((candidate.values == null || candidate.values.Length == 0) && candidate.bases != null && candidate.bases.Length > 0)
                    GenerateVariantPreviewData(candidate, GridPreviewSize);
                if (candidate.values == null || candidate.values.Length == 0)
                    continue;

                _influenceFeatureEntries.Add(BuildInfluenceFeatureEntry(candidate));
            }

            _randomnessFeatureStrip = CreateRandomnessFeatureStrip(InfluenceFeatureSize, 18);
            _influenceFeaturePreviewDirty = false;
            Repaint();
        }

        private InfluenceFeatureEntry BuildInfluenceFeatureEntry(ProceduralTextureCandidate candidate)
        {
            float[] values = ResampleFeatureValues(candidate.values, candidate.valuesWidth, candidate.valuesHeight, InfluenceFeatureSize, InfluenceFeatureSize);
            MeasureFeatureStats(values, InfluenceFeatureSize, InfluenceFeatureSize, out float coverage, out float contrast, out float detail, out float scale, out float seam);
            return new InfluenceFeatureEntry
            {
                label = string.IsNullOrWhiteSpace(candidate.label) ? "Guide" : candidate.label,
                guideStrength01 = Mathf.Clamp01(candidate.guideStrength01 <= 0f ? candidate.influenceWeight : candidate.guideStrength01),
                similarityTarget01 = Mathf.Clamp01(candidate.similarityTarget01 <= 0f ? 0.55f : candidate.similarityTarget01),
                coverage01 = coverage,
                contrast01 = contrast,
                detail01 = detail,
                scale01 = scale,
                seamScore01 = seam,
                preview = new ProceduralTextureFeaturePreview
                {
                    structureMap = CreateStructureMap(values, InfluenceFeatureSize, InfluenceFeatureSize, $"{candidate.label} Structure"),
                    densityMask = CreateDensityMask(values, InfluenceFeatureSize, InfluenceFeatureSize, $"{candidate.label} Density"),
                    lowFrequencyMap = CreateFrequencyMap(values, InfluenceFeatureSize, InfluenceFeatureSize, false, $"{candidate.label} Scale"),
                    highFrequencyMap = CreateFrequencyMap(values, InfluenceFeatureSize, InfluenceFeatureSize, true, $"{candidate.label} Detail"),
                    toneStrip = CreateToneStrip(values, InfluenceFeatureSize, 18, $"{candidate.label} Tone"),
                    toneHistogram = CreateToneHistogram(values, InfluenceFeatureSize, 18, $"{candidate.label} Histogram"),
                    colourRamp = CreateToneStrip(values, InfluenceFeatureSize, 18, $"{candidate.label} Colour"),
                    seamEdgeStrip = CreateSeamStrip(values, InfluenceFeatureSize, InfluenceFeatureSize, $"{candidate.label} Seam"),
                    tilePreview2x = CreateTilePreview(values, InfluenceFeatureSize, InfluenceFeatureSize, $"{candidate.label} Tile")
                }
            };
        }

        private void DestroyInfluenceFeaturePreviewCache()
        {
            EditorApplication.delayCall -= RebuildInfluenceFeaturePreviews;
            for (int i = 0; i < _influenceFeatureEntries.Count; i++)
                DestroyFeaturePreview(_influenceFeatureEntries[i].preview);
            _influenceFeatureEntries.Clear();
            if (_randomnessFeatureStrip != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_randomnessFeatureStrip);
            _randomnessFeatureStrip = null;
        }

        private static void DestroyFeaturePreview(ProceduralTextureFeaturePreview preview)
        {
            if (preview == null)
                return;
            DestroyFeatureTexture(preview.structureMap);
            DestroyFeatureTexture(preview.densityMask);
            DestroyFeatureTexture(preview.lowFrequencyMap);
            DestroyFeatureTexture(preview.highFrequencyMap);
            DestroyFeatureTexture(preview.toneStrip);
            DestroyFeatureTexture(preview.toneHistogram);
            DestroyFeatureTexture(preview.colourRamp);
            DestroyFeatureTexture(preview.seamEdgeStrip);
            DestroyFeatureTexture(preview.tilePreview2x);
        }

        private static void DestroyFeatureTexture(Texture2D texture)
        {
            if (texture != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(texture);
        }

        private void DrawTextureInfluenceQuilt()
        {
            if (_influenceFeaturePreviewDirty)
                QueueInfluenceFeaturePreviewRebuild();

            using (BeginInspectorSection("Texture Influence Quilt", UtilityWindowTheme.Purple, _influences.Count > 0 ? $"{_influences.Count} guide" : "fresh", TextureButtonTone.Secondary))
            {
                DrawQuiltSummary();
                if (_influences.Count == 0)
                {
                    DrawInlineStatus("No guides. Generate will create fully random textures.", UtilityWindowTheme.Neutral);
                    DrawQuiltBalanceGraph();
                    return;
                }

                if (_influenceFeaturePreviewDirty || _influenceFeatureEntries.Count == 0)
                {
                    DrawInlineStatus("Guide feature previews are rebuilding outside repaint.", UtilityWindowTheme.Amber);
                    DrawQuiltBalanceGraph();
                    return;
                }

                DrawQuiltConflictSummary();
                DrawGuideSourceStrip();
                DrawTraitLane("Structure", entry => entry.preview.structureMap, entry => entry.guideStrength01 * Mathf.Lerp(0.65f, 1f, entry.scale01));
                DrawTraitLane("Density", entry => entry.preview.densityMask, entry => entry.guideStrength01 * Mathf.Lerp(0.55f, 1f, entry.coverage01));
                DrawTraitLane("Scale", entry => entry.preview.lowFrequencyMap, entry => entry.guideStrength01 * entry.scale01);
                DrawTraitLane("Detail", entry => entry.preview.highFrequencyMap, entry => entry.guideStrength01 * entry.detail01);
                DrawTraitLane("Tone", entry => entry.preview.toneHistogram, entry => entry.guideStrength01 * Mathf.Lerp(0.55f, 1f, entry.contrast01));
                DrawTraitLane("Colour", entry => entry.preview.colourRamp, entry => entry.guideStrength01 * 0.65f);
                if (_tileabilityMode != TextureTileabilityMode.Off || _combination.tilePreview)
                    DrawTraitLane("Seam", entry => entry.preview.seamEdgeStrip, entry => entry.guideStrength01 * entry.seamScore01);
                DrawQuiltBalanceGraph();
            }
        }

        private void DrawQuiltSummary()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill($"Guides {_influences.Count}", _influences.Count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 78f);
                UtilityWindowTheme.CountPill($"Mutation {_combination.randomness01:0.00}", UtilityWindowTheme.Cyan, 104f);
                UtilityWindowTheme.CountPill($"Adherence {_combination.lockedInfluence01:0.00}", UtilityWindowTheme.Green, 112f);
                UtilityWindowTheme.CountPill(_tileabilityMode.ToString(), UtilityWindowTheme.Blue, 118f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawQuiltConflictSummary()
        {
            if (_influenceFeatureEntries.Count < 2)
                return;
            MeasureEntryRange(entry => entry.coverage01, out float densityDelta);
            MeasureEntryRange(entry => entry.contrast01, out float toneDelta);
            MeasureEntryRange(entry => entry.detail01, out float detailDelta);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (densityDelta > 0.42f)
                    UtilityWindowTheme.CountPill("Density conflict", UtilityWindowTheme.Amber, 112f);
                if (toneDelta > 0.36f)
                    UtilityWindowTheme.CountPill("Tone conflict", UtilityWindowTheme.Amber, 98f);
                if (detailDelta > 0.42f)
                    UtilityWindowTheme.CountPill("Detail conflict", UtilityWindowTheme.Amber, 104f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawGuideSourceStrip()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 58f, GUILayout.ExpandWidth(true), GUILayout.Height(58f));
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Green, 0.08f), PanelBorder(UtilityWindowTheme.Green, 0.22f));

            float x = rect.x + 6f;
            for (int i = 0; i < _influenceFeatureEntries.Count && x < rect.xMax - 48f; i++)
            {
                InfluenceFeatureEntry entry = _influenceFeatureEntries[i];
                Rect tile = new Rect(x, rect.y + 6f, 44f, 44f);
                DrawCheckerBackground(tile);
                if (entry.preview?.tilePreview2x != null)
                    GUI.DrawTexture(tile, entry.preview.tilePreview2x, ScaleMode.ScaleAndCrop, true);
                DrawStudioBox(tile, Color.clear, PanelBorder(UtilityWindowTheme.Green, 0.72f));
                GUI.Label(new Rect(tile.xMax + 5f, tile.y + 2f, 86f, 16f), entry.label, UtilityWindowTheme.MutedMiniLabelStyle);
                GUI.Label(new Rect(tile.xMax + 5f, tile.y + 22f, 86f, 16f), $"strength {entry.guideStrength01:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
                x += 136f;
            }
        }

        private void DrawTraitLane(string label, Func<InfluenceFeatureEntry, Texture2D> textureSelector, Func<InfluenceFeatureEntry, float> weightSelector)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(78f));
                Rect lane = GUILayoutUtility.GetRect(60f, 24f, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(lane, EditorGUIUtility.isProSkin ? new Color(0.075f, 0.075f, 0.083f) : new Color(0.72f, 0.72f, 0.74f));

                float randomWeight = Mathf.Max(0.05f, _combination.randomness01);
                float total = randomWeight;
                for (int i = 0; i < _influenceFeatureEntries.Count; i++)
                    total += Mathf.Max(0.02f, weightSelector(_influenceFeatureEntries[i]) * _combination.lockedInfluence01);

                float x = lane.x;
                for (int i = 0; i < _influenceFeatureEntries.Count; i++)
                {
                    InfluenceFeatureEntry entry = _influenceFeatureEntries[i];
                    float weight = Mathf.Max(0.02f, weightSelector(entry) * _combination.lockedInfluence01);
                    float width = Mathf.Max(3f, lane.width * weight / total);
                    Rect segment = new Rect(x, lane.y, Mathf.Min(width, lane.xMax - x), lane.height);
                    Texture2D texture = textureSelector(entry);
                    if (texture != null)
                        GUI.DrawTexture(segment, texture, ScaleMode.StretchToFill, true);
                    DrawStudioBox(segment, Color.clear, PanelBorder(UtilityWindowTheme.Green, 0.30f));
                    x += width;
                    if (x >= lane.xMax - 4f)
                        break;
                }

                if (x < lane.xMax)
                {
                    Rect randomSegment = new Rect(x, lane.y, lane.xMax - x, lane.height);
                    if (_randomnessFeatureStrip != null)
                        GUI.DrawTexture(randomSegment, _randomnessFeatureStrip, ScaleMode.StretchToFill, true);
                    DrawStudioBox(randomSegment, Color.clear, PanelBorder(UtilityWindowTheme.Cyan, 0.32f));
                }
            }
        }

        private void DrawQuiltBalanceGraph()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, PanelFill(UtilityWindowTheme.Neutral, 0.08f));
                float split = rect.x + rect.width * Mathf.Clamp01(_combination.randomness01);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, split - rect.x, rect.height), new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.58f));
                EditorGUI.DrawRect(new Rect(split, rect.y, rect.xMax - split, rect.height), new Color(UtilityWindowTheme.Green.r, UtilityWindowTheme.Green.g, UtilityWindowTheme.Green.b, 0.58f));
                DrawStudioBox(rect, Color.clear, PanelBorder(UtilityWindowTheme.Purple, 0.36f));
            }
            GUI.Label(new Rect(rect.x + 7f, rect.y + 5f, rect.width * 0.5f - 10f, 16f), "Mutation", UtilityWindowTheme.MutedMiniLabelStyle);
            GUI.Label(new Rect(rect.center.x, rect.y + 5f, rect.width * 0.5f - 7f, 16f), "Guide Adherence", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void MeasureEntryRange(Func<InfluenceFeatureEntry, float> selector, out float delta)
        {
            float min = 1f;
            float max = 0f;
            for (int i = 0; i < _influenceFeatureEntries.Count; i++)
            {
                float value = Mathf.Clamp01(selector(_influenceFeatureEntries[i]));
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
            delta = Mathf.Max(0f, max - min);
        }

        private static float[] ResampleFeatureValues(float[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            var result = new float[width * height];
            if (source == null || source.Length == 0)
                return result;
            sourceWidth = Mathf.Max(1, sourceWidth);
            sourceHeight = Mathf.Max(1, sourceHeight);
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) / height * sourceHeight - 0.5f), 0, sourceHeight - 1);
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) / width * sourceWidth - 0.5f), 0, sourceWidth - 1);
                    int sourceIndex = sourceY * sourceWidth + sourceX;
                    result[y * width + x] = sourceIndex >= 0 && sourceIndex < source.Length ? Mathf.Clamp01(source[sourceIndex]) : 0f;
                }
            }
            return result;
        }

        private static void MeasureFeatureStats(float[] values, int width, int height, out float coverage, out float contrast, out float detail, out float scale, out float seamScore)
        {
            float min = 1f;
            float max = 0f;
            float covered = 0f;
            float highPass = 0f;
            float lowFrequency = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                float value = Mathf.Clamp01(values[i]);
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
                if (value >= 0.5f)
                    covered += 1f;
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = SampleFeature(values, width, height, x, y);
                    float blur = AverageFeature(values, width, height, x, y, 3);
                    highPass += Mathf.Abs(value - blur);
                    lowFrequency += Mathf.Abs(blur - 0.5f);
                }
            }
            coverage = covered / Mathf.Max(1, values.Length);
            contrast = Mathf.Clamp01(max - min);
            detail = Mathf.Clamp01(highPass / Mathf.Max(1, values.Length) * 4f);
            scale = Mathf.Clamp01(lowFrequency / Mathf.Max(1, values.Length) * 2f);
            seamScore = MeasureSeamScore(values, width, height);
        }

        private static Texture2D CreateStructureMap(float[] values, int width, int height, string name)
        {
            return CreateFeatureTexture(width, height, name, (x, y) =>
            {
                float gx = SampleFeature(values, width, height, x + 1, y) - SampleFeature(values, width, height, x - 1, y);
                float gy = SampleFeature(values, width, height, x, y + 1) - SampleFeature(values, width, height, x, y - 1);
                float edge = Mathf.Clamp01(Mathf.Sqrt(gx * gx + gy * gy) * 3f);
                return new Color(edge, edge, edge, 1f);
            });
        }

        private static Texture2D CreateDensityMask(float[] values, int width, int height, string name)
        {
            return CreateFeatureTexture(width, height, name, (x, y) =>
            {
                float value = SampleFeature(values, width, height, x, y) >= 0.5f ? 1f : 0f;
                return new Color(value, value, value, 1f);
            });
        }

        private static Texture2D CreateFrequencyMap(float[] values, int width, int height, bool highPass, string name)
        {
            return CreateFeatureTexture(width, height, name, (x, y) =>
            {
                float value = SampleFeature(values, width, height, x, y);
                float blur = AverageFeature(values, width, height, x, y, highPass ? 3 : 5);
                float output = highPass ? Mathf.Clamp01(Mathf.Abs(value - blur) * 4f) : blur;
                return new Color(output, output, output, 1f);
            });
        }

        private static Texture2D CreateToneStrip(float[] values, int width, int height, string name)
        {
            float[] sorted = (float[])values.Clone();
            Array.Sort(sorted);
            return CreateFeatureTexture(width, height, name, (x, y) =>
            {
                int index = Mathf.Clamp(Mathf.RoundToInt((x / (float)Mathf.Max(1, width - 1)) * (sorted.Length - 1)), 0, sorted.Length - 1);
                float value = Mathf.Clamp01(sorted[index]);
                return new Color(value, value, value, 1f);
            });
        }

        private static Texture2D CreateToneHistogram(float[] values, int width, int height, string name)
        {
            int[] bins = new int[width];
            int max = 1;
            for (int i = 0; i < values.Length; i++)
            {
                int bin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(values[i]) * (width - 1)), 0, width - 1);
                bins[bin]++;
                max = Mathf.Max(max, bins[bin]);
            }
            return CreateFeatureTexture(width, height, name, (x, y) =>
            {
                float bar = bins[x] / (float)max;
                float lit = y >= height - Mathf.CeilToInt(bar * height) ? 1f : 0.16f;
                return new Color(lit, lit, lit, 1f);
            });
        }

        private static Texture2D CreateSeamStrip(float[] values, int width, int height, string name)
        {
            return CreateFeatureTexture(width, 18, name, (x, y) =>
            {
                float u = x / (float)Mathf.Max(1, width - 1);
                int sourceX = Mathf.Clamp(Mathf.RoundToInt(u * (width - 1)), 0, width - 1);
                int sourceY = Mathf.Clamp(Mathf.RoundToInt(u * (height - 1)), 0, height - 1);
                float value = y < 4
                    ? SampleFeature(values, width, height, 0, sourceY)
                    : y < 8
                        ? SampleFeature(values, width, height, width - 1, sourceY)
                        : y < 13
                            ? SampleFeature(values, width, height, sourceX, 0)
                            : SampleFeature(values, width, height, sourceX, height - 1);
                return new Color(value, value, value, 1f);
            });
        }

        private static Texture2D CreateTilePreview(float[] values, int width, int height, string name)
        {
            int halfWidth = Mathf.Max(1, width / 2);
            int halfHeight = Mathf.Max(1, height / 2);
            return CreateFeatureTexture(width, height, name, (x, y) =>
            {
                float value = SampleFeature(values, width, height, x % halfWidth, y % halfHeight);
                return new Color(value, value, value, 1f);
            });
        }

        private static Texture2D CreateRandomnessFeatureStrip(int width, int height)
        {
            unchecked
            {
                uint state = 0xA511E9B3u;
                return CreateFeatureTexture(width, height, "Randomness Feature Strip", (x, y) =>
                {
                    state ^= state << 13;
                    state ^= state >> 17;
                    state ^= state << 5;
                    float value = ((state + (uint)(x * 97 + y * 193)) & 255u) / 255f;
                    value = Mathf.Lerp(value, Mathf.PerlinNoise(x * 0.12f, y * 0.31f), 0.45f);
                    return new Color(value, value, value, 1f);
                });
            }
        }

        private static Texture2D CreateFeatureTexture(int width, int height, string name, Func<int, int, Color> pixel)
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y);
            return ProceduralTextureCombinationUtility.CreateTexture(width, height, TextureFormat.RGBA32, false, true, pixels, name);
        }

        private static float SampleFeature(float[] values, int width, int height, int x, int y)
        {
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            return Mathf.Clamp01(values[y * width + x]);
        }

        private static float AverageFeature(float[] values, int width, int height, int x, int y, int radius)
        {
            float total = 0f;
            int count = 0;
            for (int yy = y - radius; yy <= y + radius; yy++)
            {
                for (int xx = x - radius; xx <= x + radius; xx++)
                {
                    total += SampleFeature(values, width, height, xx, yy);
                    count++;
                }
            }
            return count > 0 ? total / count : 0f;
        }

        private static float MeasureSeamScore(float[] values, int width, int height)
        {
            float total = 0f;
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                total += Mathf.Abs(SampleFeature(values, width, height, 0, y) - SampleFeature(values, width, height, width - 1, y));
                count++;
            }
            for (int x = 0; x < width; x++)
            {
                total += Mathf.Abs(SampleFeature(values, width, height, x, 0) - SampleFeature(values, width, height, x, height - 1));
                count++;
            }
            return Mathf.Clamp01(1f - total / Mathf.Max(1, count));
        }

        private void DrawLockedTextureInfluenceLane(int index, ProceduralTextureCandidate variant)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 30f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, PanelFill(UtilityWindowTheme.Green, 0.18f));
                if (variant.preview != null)
                {
                    Color oldColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.18f);
                    GUI.DrawTexture(rect, variant.preview, ScaleMode.ScaleAndCrop, true);
                    GUI.color = oldColor;
                }
                DrawStudioBox(rect, Color.clear, PanelBorder(UtilityWindowTheme.Green, 0.42f));
            }

            GUI.Label(new Rect(rect.x + 7f, rect.y + 6f, Mathf.Max(60f, rect.width - 86f), 18f), $"{index + 1}. {variant.label}", UtilityWindowTheme.MutedMiniLabelStyle);
            GUI.Label(new Rect(rect.xMax - 72f, rect.y + 6f, 66f, 18f), $"{variant.influenceWeight:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static void DrawIntRange(string label, ref int min, ref int max, int minLimit, int maxLimit)
        {
            float minFloat = min;
            float maxFloat = max;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(92f));
                min = EditorGUILayout.IntField(min, GUILayout.Width(46f));
                minFloat = min;
                maxFloat = max;
                EditorGUILayout.MinMaxSlider(ref minFloat, ref maxFloat, minLimit, maxLimit);
                max = EditorGUILayout.IntField(max, GUILayout.Width(46f));
            }

            min = Mathf.Clamp(Mathf.RoundToInt(minFloat), minLimit, maxLimit);
            max = Mathf.Clamp(Mathf.RoundToInt(maxFloat), min, maxLimit);
        }

        private int EnabledBaseCount()
        {
            int count = 0;
            for (int i = 0; i < _bases.Count; i++)
            {
                if (_bases[i] != null && _bases[i].enabled)
                    count++;
            }
            return count;
        }

        private void DrawFavouriteBiasMarkers()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int shown = 0;
                foreach (int index in _selectedCandidates)
                {
                    if (index < 0 || index >= _candidates.Count)
                        continue;
                    UtilityWindowTheme.CountPill(_candidates[index].label.Replace("Candidate ", "#"), UtilityWindowTheme.Green, 54f);
                    shown++;
                    if (shown >= 5)
                        break;
                }
                if (_selectedCandidates.Count > shown)
                    UtilityWindowTheme.CountPill($"+{_selectedCandidates.Count - shown}", UtilityWindowTheme.Green, 42f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawBasePreview(int index, Rect rect)
        {
            DrawCheckerBackground(rect);
            if (index < 0 || index >= _basePreviewTextures.Count || _basePreviewTextures[index] == null)
                return;
            GUI.DrawTexture(rect, _basePreviewTextures[index], ScaleMode.ScaleToFit, true);
        }
    }
#endif
}
