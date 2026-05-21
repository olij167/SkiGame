using PungentFunk.Utilities.Colour;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed partial class ProceduralTextureLabWindow
    {
        private void RunPrimaryGenerateAction()
        {
            if (_activeInspector == TextureInspectorTab.Explore)
            {
                ApplyExploreStrategyToLegacyWorkflow();
                GenerateCandidates();
                return;
            }

            if (_activeInspector == TextureInspectorTab.Compose && _activeWorkflow == TextureDesignWorkflow.ManualCompose)
            {
                GeneratePreview(true);
                _lastStatus = "Refreshed manual composition preview.";
                return;
            }

            if (_activeInspector == TextureInspectorTab.Compose && _activeWorkflow == TextureDesignWorkflow.GuidedRefine)
            {
                GenerateSimilarFromActiveCandidate();
                return;
            }

            if (_activeInspector == TextureInspectorTab.Compose && _activeWorkflow == TextureDesignWorkflow.BlendLab)
            {
                GenerateBlendCandidates();
                return;
            }

            if (_activeInspector == TextureInspectorTab.Compose && _activeWorkflow == TextureDesignWorkflow.ReferenceMatch)
            {
                GenerateReferenceMatches();
                return;
            }

            GenerateCandidates();
        }

        private void ApplyExploreStrategyToLegacyWorkflow()
        {
            switch (_exploreStrategy)
            {
                case TextureExploreStrategy.Constrained:
                    _activeWorkflow = TextureDesignWorkflow.ConstraintMatch;
                    break;
                case TextureExploreStrategy.Presets:
                    _activeWorkflow = TextureDesignWorkflow.PresetBrowser;
                    break;
                case TextureExploreStrategy.Guided:
                case TextureExploreStrategy.Random:
                default:
                    _activeWorkflow = TextureDesignWorkflow.RandomExplore;
                    break;
            }
            _activeDesignerPhase = TextureDesignerPhase.Explore;
        }

        private void GeneratePreview(bool recordHistory)
        {
            if (recordHistory)
                AddHistorySnapshot(_baseHistory, "Before Refresh", true);

            _manualComposeAutoPreviewQueued = false;
            _manualComposeAutoPreviewAt = -1d;
            _manualComposeAutoPreviewReason = string.Empty;
            DestroyCurrentPreview();
            _combination.Clamp();
            ClampBases();
            _previewValues = ProceduralTextureCombinationUtility.GenerateCombinedValues(ManualCompositionBasesForOutput(), _combination);
            _previewPixels = ProceduralTextureCombinationUtility.ValuesToPixels(_previewValues, _combination, PaletteColors());
            _previewTexture = ProceduralTextureCombinationUtility.CreateTexture(_combination.width, _combination.height, _combination.textureFormat, _combination.mipChain, _combination.linear, _previewPixels, "Texture Generator Preview");
            UpdateManualOutputMetrics();
            _previewDirty = false;
            _lastStatus = "Preview refreshed.";
            QueueBasePreviewRebuild();
            SaveSession();
            if (recordHistory)
                AutosaveTextureIteration("Manual preview refresh");
            Repaint();
        }

        private ProceduralTextureBaseSettings[] ManualCompositionBasesForOutput()
        {
            if (_bases.Count == 0)
                return Array.Empty<ProceduralTextureBaseSettings>();

            var result = new ProceduralTextureBaseSettings[_bases.Count];
            for (int i = 0; i < _bases.Count; i++)
                result[i] = _bases[_bases.Count - 1 - i];
            return result;
        }

        private void GenerateCandidates()
        {
            GenerateCandidates(null, null);
        }

        private void GenerateCandidates(ProceduralTextureBaseSettings[] overrideInfluences, string statusPrefix)
        {
            AddHistorySnapshot(_combinationHistory, "Before Generate", false);
            _generationNumber++;
            _combination.Clamp();
            ClampBases();
            CancelGenerationJobs(false);

            int count = Mathf.Clamp(_combination.candidateCount, 1, 16);
            var previous = new List<ProceduralTextureCandidate>(_candidates);
            int promoted = PromoteLockedCandidates(previous);
            _candidates.Clear();
            _selectedCandidates.Clear();
            CloseTextureEditOverlay();
            ProceduralTextureBaseSettings[] lockedInfluences = overrideInfluences ?? BuildInfluenceBases();
            int queued = 0;
            var batchRng = new System.Random(Environment.TickCount);

            for (int i = 0; i < count; i++)
            {
                if (i < previous.Count && previous[i] != null)
                    DestroyVariantGeneratedData(previous[i]);

                _candidates.Add(CreatePendingVariant(i));
                _generationJobs.Enqueue(new TextureGenerationJob
                {
                    slotIndex = i,
                    seed = ProceduralTextureRecipeRandomizer.HashSeed(batchRng.Next(1, int.MaxValue), i),
                    lockedInfluences = lockedInfluences,
                    activePreview = i == _activeCandidateIndex
                });
                queued++;
            }

            for (int i = count; i < previous.Count; i++)
            {
                if (previous[i] != null)
                    DestroyVariantGeneratedData(previous[i]);
            }

            _generationRunning = queued > 0;
            _generationTotal = queued;
            _generationCompleted = 0;
            _activeCandidateIndex = _candidates.Count > 0 ? Mathf.Clamp(_activeCandidateIndex, 0, _candidates.Count - 1) : -1;
            string sourceLabel = overrideInfluences != null ? $"{lockedInfluences.Length} guided target(s)" : $"{_influences.Count} influence(s)";
            _lastStatus = queued > 0
                ? $"{(string.IsNullOrEmpty(statusPrefix) ? "Generating" : statusPrefix)} 0/{queued}... {promoted} promoted, {sourceLabel}."
                : $"{promoted} promoted. Nothing to regenerate.";
            if (!_generationRunning)
                RequestSessionSave();
            Repaint();
        }

        private void GenerateSimilarFromActiveCandidate()
        {
            if (!IsValidActiveCandidate())
            {
                _lastStatus = "Select a texture before generating similar variants.";
                Repaint();
                return;
            }

            ProceduralTextureCandidate target = _candidates[_activeCandidateIndex];
            ProceduralTextureBaseSettings guide = CreateInfluenceBaseFromCandidate(target, 0, Mathf.Clamp01(_combination.lockedInfluence01));
            if (guide == null)
            {
                _lastStatus = "Active texture is still building. Try again after its preview finishes.";
                Repaint();
                return;
            }

            guide.guidanceMode = ProceduralTextureGuidanceMode.TargetSimilarity;
            GenerateCandidates(new[] { guide }, "Generating similar");
        }

        private void MutateFromSelection()
        {
            GenerateCandidates();
        }

        private void SetActiveCandidate(int index, bool record)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].isActive = i == index;
            _activeCandidateIndex = index;
            AdoptCandidate(index, record);
        }

        private bool IsValidActiveCandidate()
        {
            return _activeCandidateIndex >= 0 && _activeCandidateIndex < _candidates.Count;
        }

        private void EnsureActiveVariant()
        {
            if (_candidates.Count == 0)
            {
                _activeCandidateIndex = -1;
                return;
            }

            if (_activeCandidateIndex < 0 || _activeCandidateIndex >= _candidates.Count)
                _activeCandidateIndex = 0;
            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].isActive = i == _activeCandidateIndex;
            AdoptCandidate(_activeCandidateIndex, false);
        }

        private void AdoptCandidate(int index, bool record = true)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            if (record)
                AddHistorySnapshot(_combinationHistory, "Before Adopt", true);

            ProceduralTextureCandidate candidate = _candidates[index];
            ProceduralTextureCombinationSettings previousGlobal = _combination != null ? _combination.Clone() : new ProceduralTextureCombinationSettings();
            DestroyCurrentPreview();
            _previewValues = candidate.values != null ? (float[])candidate.values.Clone() : null;
            _previewPixels = candidate.pixels != null ? (Color[])candidate.pixels.Clone() : null;
            int previewWidth = candidate.valuesWidth > 0 ? candidate.valuesWidth : (candidate.preview != null ? candidate.preview.width : 1);
            int previewHeight = candidate.valuesHeight > 0 ? candidate.valuesHeight : (candidate.preview != null ? candidate.preview.height : 1);
            _previewTexture = candidate.preview != null && _previewPixels != null
                ? ProceduralTextureCombinationUtility.CreateTexture(previewWidth, previewHeight, TextureFormat.RGBA32, false, true, _previewPixels, "Adopted Texture Preview")
                : null;
            _combination = candidate.settings != null ? candidate.settings.Clone() : previousGlobal.Clone();
            PreserveGlobalGenerationControls(previousGlobal, _combination);
            _activeCandidateIndex = index;
            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].isActive = i == index;
            _previewDirty = false;
            _lastStatus = $"Adopted {candidate.label}.";
            QueueGuidedRefineFeaturePreviewRebuild();
            QueueMapPreviewRebuild();
            SaveSession();
            AutosaveTextureIteration($"Viewed {candidate.label}");
        }

        private void AddCandidateAsBase(int index)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            if (_bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount)
            {
                _lastStatus = "Maximum 16 bases. Disable or merge bases before adding more.";
                return;
            }

            ProceduralTextureCandidate candidate = _candidates[index];
            if (candidate.values == null || candidate.values.Length == 0)
            {
                _lastStatus = "Candidate has no value map to bake.";
                return;
            }

            AddHistorySnapshot(_baseHistory, "Before Add Candidate Base", false);
            var textureBase = ProceduralTextureBaseSettings.CreateDefault(_bases.Count);
            textureBase.name = $"{candidate.label} Base";
            textureBase.useBakedValues = true;
            textureBase.bakedWidth = candidate.valuesWidth > 0 ? candidate.valuesWidth : GridPreviewSize;
            textureBase.bakedHeight = candidate.valuesHeight > 0 ? candidate.valuesHeight : GridPreviewSize;
            textureBase.bakedValues = (float[])candidate.values.Clone();
            textureBase.blendMode = _bases.Count == 0 ? ProceduralTextureBlendMode.Replace : ProceduralTextureBlendMode.Overlay;
            textureBase.weight = 1f;
            textureBase.locks.source = true;
            _bases.Add(textureBase);
            SelectSingleBase(_bases.Count - 1);
            MarkDirty("Added candidate as baked base.");
            OpenInspector(TextureInspectorTab.Compose);
            AutosaveTextureIteration($"Added {candidate.label} as baked layer");
        }

        private void OpenCandidateInManualComposer(int index, bool append)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            ProceduralTextureCandidate candidate = _candidates[index];
            if (candidate == null)
                return;

            AddHistorySnapshot(_baseHistory, append ? "Before Append Candidate Recipe" : "Before Edit Candidate Recipe", true);

            if (candidate.bases != null && candidate.bases.Length > 0)
            {
                if (!append)
                    _bases.Clear();

                _bases.AddRange(ProceduralTextureCombinationUtility.CloneBases(candidate.bases));
                if (candidate.settings != null)
                {
                    ProceduralTextureCombinationSettings previousGlobal = _combination != null ? _combination.Clone() : new ProceduralTextureCombinationSettings();
                    _combination = candidate.settings.Clone();
                    PreserveGlobalGenerationControls(previousGlobal, _combination);
                }

                EnsureBaseBounds();
                SelectSingleBase(Mathf.Clamp(append ? _bases.Count - candidate.bases.Length : 0, 0, _bases.Count - 1));
                _activeInspector = TextureInspectorTab.Compose;
                _activeWorkflow = TextureDesignWorkflow.ManualCompose;
                _previewDirty = true;
                _lastStatus = append
                    ? $"Appended {candidate.label} recipe layers."
                    : $"Opened {candidate.label} recipe in Manual Compose.";
                GeneratePreview(false);
                AutosaveTextureIteration(append ? $"Appended {candidate.label} recipe" : $"Editing {candidate.label} recipe");
                RequestSessionSave();
                Repaint();
                return;
            }

            AddCandidateAsBase(index);
        }

        private void UseCandidateAsGuide(int index)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            ProceduralTextureCandidate source = _candidates[index];
            ProceduralTextureCandidate guide = CloneVariant(source, true);
            if (guide == null)
                return;

            guide.locked = true;
            guide.selected = false;
            guide.isActive = false;
            guide.guidanceMode = ProceduralTextureGuidanceMode.Guide;
            guide.guideStrength01 = Mathf.Clamp01(guide.influenceWeight <= 0f ? 1f : guide.influenceWeight);
            guide.label = string.IsNullOrWhiteSpace(guide.label) ? $"Guide {_influences.Count + 1}" : guide.label;
            _influences.Add(guide);
            QueueInfluenceFeaturePreviewRebuild();
            _lastStatus = $"Added {guide.label} as a Guide.";
            RequestSessionSave();
            Repaint();
        }

        private void ExportCandidate(int index)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            _selectedCandidates.Clear();
            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].selected = false;
            _selectedCandidates.Add(index);
            _candidates[index].selected = true;
            SetActiveCandidate(index, false);
            ExportMaps();
        }

        private void AddTextureLayer(Texture2D source)
        {
            if (source == null)
            {
                _lastStatus = "Choose a texture before adding a texture layer.";
                Repaint();
                return;
            }

            if (_bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount)
            {
                _lastStatus = "Maximum 16 layers. Delete or merge a layer before adding more.";
                Repaint();
                return;
            }

            int bakedWidth = Mathf.Clamp(source.width, 8, 512);
            int bakedHeight = Mathf.Clamp(source.height, 8, 512);
            float[] values = ReadTextureValuesForBakedLayer(source, bakedWidth, bakedHeight);
            if (values == null || values.Length == 0)
            {
                _lastStatus = "Could not read texture layer values.";
                Repaint();
                return;
            }

            AddHistorySnapshot(_baseHistory, "Before Add Texture Layer", false);
            var textureBase = ProceduralTextureBaseSettings.CreateDefault(_bases.Count);
            textureBase.name = string.IsNullOrWhiteSpace(source.name) ? $"Texture Layer {_bases.Count + 1}" : $"{source.name} Layer";
            textureBase.useBakedValues = true;
            textureBase.bakedWidth = bakedWidth;
            textureBase.bakedHeight = bakedHeight;
            textureBase.bakedValues = values;
            textureBase.blendMode = _bases.Count == 0 ? ProceduralTextureBlendMode.Replace : ProceduralTextureBlendMode.Overlay;
            textureBase.weight = 1f;
            _bases.Add(textureBase);
            SelectSingleBase(_bases.Count - 1);
            MarkDirty("Added texture layer.");
            OpenInspector(TextureInspectorTab.Compose);
            AutosaveTextureIteration($"Added texture layer {textureBase.name}");
        }

        private static float[] ReadTextureValuesForBakedLayer(Texture2D texture, int width, int height)
        {
            var values = new float[width * height];
            if (texture == null)
                return values;

            RenderTexture previous = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, renderTexture);
                RenderTexture.active = renderTexture;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply(false, false);
                Color[] pixels = readable.GetPixels();
                for (int i = 0; i < values.Length && i < pixels.Length; i++)
                    values[i] = pixels[i].grayscale;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                if (readable != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(readable);
            }

            return values;
        }


        private ProceduralTextureCandidate CreatePendingVariant(int index)
        {
            return new ProceduralTextureCandidate
            {
                label = $"Texture {index + 1}",
                influenceWeight = 1f,
                guideStrength01 = 1f,
                similarityTarget01 = 0.55f,
                settings = _combination.Clone(),
                bases = Array.Empty<ProceduralTextureBaseSettings>(),
                createdUtc = DateTime.UtcNow.ToString("u")
            };
        }

        private ProceduralTextureCandidate CreateGeneratedVariant(TextureGenerationJob job)
        {
            ProceduralTextureBaseSettings[] bases = job.lockedInfluences != null && job.lockedInfluences.Length > 0
                ? ProceduralTextureRecipeRandomizer.CreateInfluencedVariantRecipe(job.seed, job.lockedInfluences, _combination)
                : ProceduralTextureRecipeRandomizer.CreateFreshVariantRecipe(job.seed, _combination);

            var candidate = new ProceduralTextureCandidate
            {
                label = $"Texture {job.slotIndex + 1}",
                seedOffset = job.seed,
                influenceWeight = 1f,
                guideStrength01 = 1f,
                similarityTarget01 = 0.55f,
                settings = CreateGeneratedCombinationSettings(_combination),
                bases = bases,
                generationDepth = job.lockedInfluences != null && job.lockedInfluences.Length > 0 ? 1 : 0,
                contributorIndices = CollectLockedContributorIndices(),
                createdUtc = DateTime.UtcNow.ToString("u")
            };

            GenerateVariantPreviewData(candidate, job.activePreview ? ActivePreviewSize : GridPreviewSize);
            EnsureMinimumPreviewContrast(candidate, job.lockedInfluences, job.activePreview ? ActivePreviewSize : GridPreviewSize);
            return candidate;
        }

        private int PromoteLockedCandidates(List<ProceduralTextureCandidate> source)
        {
            if (source == null || source.Count == 0)
                return 0;

            int promoted = 0;
            for (int i = 0; i < source.Count; i++)
            {
                ProceduralTextureCandidate candidate = source[i];
                if (candidate == null || !candidate.locked)
                    continue;

                ProceduralTextureCandidate influence = CloneVariant(candidate, true);
                influence.label = string.IsNullOrEmpty(influence.label) ? $"Guide {_influences.Count + 1}" : influence.label;
                influence.locked = true;
                influence.guidanceMode = ProceduralTextureGuidanceMode.Guide;
                influence.guideStrength01 = Mathf.Clamp01(influence.influenceWeight);
                influence.selected = false;
                influence.isActive = false;
                if (influence.values == null && influence.bases != null && influence.bases.Length > 0)
                    GenerateVariantPreviewData(influence, GridPreviewSize);
                _influences.Add(influence);
                promoted++;
            }

            if (promoted > 0)
            {
                AddHistorySnapshot(_baseHistory, $"Promoted {promoted} Guide{(promoted == 1 ? string.Empty : "s")}", false);
                QueueInfluenceFeaturePreviewRebuild();
            }
            return promoted;
        }

        private ProceduralTextureBaseSettings[] BuildInfluenceBases()
        {
            var result = new List<ProceduralTextureBaseSettings>();
            float lockedInfluence = _combination != null ? Mathf.Clamp01(_combination.lockedInfluence01) : 1f;
            if (_influences.Count == 0 || lockedInfluence <= 0.001f)
                return Array.Empty<ProceduralTextureBaseSettings>();

            for (int i = 0; i < _influences.Count && result.Count < ProceduralTextureCombinationUtility.MaxBaseCount; i++)
            {
                ProceduralTextureCandidate candidate = _influences[i];
                if (candidate == null)
                    continue;

                if ((candidate.values == null || candidate.values.Length == 0) && candidate.bases != null && candidate.bases.Length > 0)
                    GenerateVariantPreviewData(candidate, GridPreviewSize);

                if (candidate.values == null || candidate.values.Length == 0)
                    continue;

                ProceduralTextureBaseSettings textureBase = CreateInfluenceBaseFromCandidate(candidate, result.Count, lockedInfluence);
                if (textureBase != null)
                    result.Add(textureBase);
            }

            return result.ToArray();
        }

        private ProceduralTextureBaseSettings CreateInfluenceBaseFromCandidate(ProceduralTextureCandidate candidate, int index, float influenceScale)
        {
            if (candidate == null)
                return null;

            if ((candidate.values == null || candidate.values.Length == 0) && candidate.bases != null && candidate.bases.Length > 0)
                GenerateVariantPreviewData(candidate, GridPreviewSize);

            if (candidate.values == null || candidate.values.Length == 0)
                return null;

            var textureBase = ProceduralTextureBaseSettings.CreateDefault(index);
            textureBase.name = candidate.label;
            textureBase.useBakedValues = true;
            textureBase.bakedWidth = candidate.valuesWidth > 0 ? candidate.valuesWidth : GridPreviewSize;
            textureBase.bakedHeight = candidate.valuesHeight > 0 ? candidate.valuesHeight : GridPreviewSize;
            textureBase.bakedValues = (float[])candidate.values.Clone();
            textureBase.blendMode = index == 0 ? ProceduralTextureBlendMode.Replace : ProceduralTextureBlendMode.Overlay;
            float guideStrength = Mathf.Clamp01(candidate.guideStrength01 <= 0f ? candidate.influenceWeight : candidate.guideStrength01);
            textureBase.weight = Mathf.Clamp(guideStrength * Mathf.Max(0.02f, influenceScale), 0.02f, 1f);
            textureBase.guidanceMode = candidate.guidanceMode == ProceduralTextureGuidanceMode.None ? ProceduralTextureGuidanceMode.Guide : candidate.guidanceMode;
            textureBase.guideStrength01 = guideStrength;
            textureBase.similarityTarget01 = Mathf.Clamp01(candidate.similarityTarget01 <= 0f ? 0.55f : candidate.similarityTarget01);
            textureBase.userRating = Mathf.Clamp(candidate.userRating <= 0 ? 3 : candidate.userRating, 0, 5);
            textureBase.preserveStructure = candidate.preserveStructure;
            textureBase.preserveDensity = candidate.preserveDensity;
            textureBase.preserveScale = candidate.preserveScale;
            textureBase.preserveDetail = candidate.preserveDetail;
            textureBase.preserveTone = candidate.preserveTone;
            textureBase.preserveColourOrPalette = candidate.preserveColourOrPalette;
            textureBase.preserveStampShape = candidate.preserveStampShape;
            textureBase.preserveBlendOrder = candidate.preserveBlendOrder;
            textureBase.preserveSeam = candidate.preserveSeam;
            textureBase.locks.source = true;
            return textureBase;
        }

        private int[] CollectLockedContributorIndices()
        {
            var contributors = new List<int>();
            for (int i = 0; i < _influences.Count; i++)
            {
                if (_influences[i] != null)
                    contributors.Add(i);
            }
            return contributors.ToArray();
        }

        private void ExportMaps()
        {
            List<ProceduralTextureCandidate> targets = SelectedExportTargets();
            if (targets.Count == 0 && _previewValues == null)
            {
                _lastStatus = "Generate or select a texture before exporting.";
                return;
            }

            string folder = Directory.Exists(_lastSaveFolder) ? _lastSaveFolder : "Assets";
            string path = EditorUtility.SaveFilePanelInProject("Export Generated Texture Maps", "Generated_Texture", "png", "Choose the base PNG name for exported maps.", folder);
            if (string.IsNullOrEmpty(path))
                return;

            _lastSaveFolder = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
            string directory = _lastSaveFolder;
            string baseName = Path.GetFileNameWithoutExtension(path);
            int exported = 0;
            Object firstAsset = null;

            if (targets.Count == 0)
            {
                exported += ExportAllEnabledMaps(_previewValues, _previewPixels, _combination, directory, baseName, ref firstAsset);
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    ProceduralTextureCandidate target = targets[i];
                    EnsureFullVariantData(target);
                    string variantName = targets.Count == 1 ? baseName : $"{baseName}_{SanitizeFileName(target.label)}";
                    exported += ExportAllEnabledMaps(target.fullValues, target.fullPixels, target.settings ?? _combination, directory, variantName, ref firstAsset);
                }
            }
            AssetDatabase.Refresh();

            if (firstAsset != null)
            {
                Selection.activeObject = firstAsset;
                EditorGUIUtility.PingObject(firstAsset);
            }

            _lastStatus = exported > 0 ? $"Exported {exported} map(s)." : "No maps selected for export.";
            SavePrefs();
            if (exported > 0)
                AutosaveTextureIteration("Exported texture maps", true, path);
        }

        private List<ProceduralTextureCandidate> SelectedExportTargets()
        {
            var targets = new List<ProceduralTextureCandidate>();
            foreach (int index in _selectedCandidates)
            {
                if (index >= 0 && index < _candidates.Count && _candidates[index].bases != null && _candidates[index].bases.Length > 0)
                    targets.Add(_candidates[index]);
            }

            if (targets.Count == 0 && IsValidActiveCandidate() && _candidates[_activeCandidateIndex].bases != null && _candidates[_activeCandidateIndex].bases.Length > 0)
                targets.Add(_candidates[_activeCandidateIndex]);
            return targets;
        }

        private int ExportAllEnabledMaps(float[] values, Color[] pixels, ProceduralTextureCombinationSettings settings, string directory, string baseName, ref Object firstAsset)
        {
            int exported = 0;
            exported += ExportMapIfEnabled(_export.diffuse, ProceduralTextureMapType.Diffuse, values, pixels, settings, directory, baseName, ref firstAsset);
            exported += ExportMapIfEnabled(_export.height, ProceduralTextureMapType.Height, values, pixels, settings, directory, baseName, ref firstAsset);
            exported += ExportMapIfEnabled(_export.normal, ProceduralTextureMapType.Normal, values, pixels, settings, directory, baseName, ref firstAsset);
            exported += ExportMapIfEnabled(_export.roughness, ProceduralTextureMapType.Roughness, values, pixels, settings, directory, baseName, ref firstAsset);
            exported += ExportMapIfEnabled(_export.smoothness, ProceduralTextureMapType.Smoothness, values, pixels, settings, directory, baseName, ref firstAsset);
            exported += ExportMapIfEnabled(_export.metallic, ProceduralTextureMapType.Metallic, values, pixels, settings, directory, baseName, ref firstAsset);
            exported += ExportMapIfEnabled(_export.alpha, ProceduralTextureMapType.Alpha, values, pixels, settings, directory, baseName, ref firstAsset);
            return exported;
        }

        private int ExportMapIfEnabled(bool enabled, ProceduralTextureMapType mapType, float[] values, Color[] pixels, ProceduralTextureCombinationSettings settings, string directory, string baseName, ref Object firstAsset)
        {
            if (!enabled || values == null || settings == null)
                return 0;

            Texture2D texture = ProceduralTextureCombinationUtility.GenerateMapTexture(values, settings, _export, mapType, pixels);
            string suffix = mapType == ProceduralTextureMapType.Diffuse ? "Diffuse" : mapType.ToString();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{baseName}_{suffix}.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            ProceduralTextureCombinationUtility.DestroyGeneratedTexture(texture);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = mapType == ProceduralTextureMapType.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.mipmapEnabled = _combination.mipChain;
                importer.sRGBTexture = mapType == ProceduralTextureMapType.Diffuse;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }

            Object asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (firstAsset == null)
                firstAsset = asset;
            return 1;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Texture";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }

        private void AddBase()
        {
            if (_bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount)
            {
                _lastStatus = "Maximum 16 bases. Disable or merge bases before adding more.";
                return;
            }

            AddHistorySnapshot(_baseHistory, "Before Add Base", false);
            _bases.Add(ProceduralTextureBaseSettings.CreateDefault(_bases.Count));
            SelectSingleBase(_bases.Count - 1);
            MarkDirty("Added base.");
        }

        private void DuplicateSelectedBase()
        {
            if (_bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount)
            {
                _lastStatus = "Maximum 16 bases. Disable or merge bases before adding more.";
                return;
            }

            AddHistorySnapshot(_baseHistory, "Before Duplicate Base", false);
            ProceduralTextureBaseSettings clone = _bases[Mathf.Clamp(_selectedBase, 0, _bases.Count - 1)].Clone();
            clone.name = $"{clone.name} Copy";
            clone.generation.seed += 1337;
            _bases.Insert(Mathf.Clamp(_selectedBase + 1, 0, _bases.Count), clone);
            SelectSingleBase(Mathf.Clamp(_selectedBase + 1, 0, _bases.Count - 1));
            MarkDirty("Duplicated base.");
        }

        private void DeleteSelectedBase()
        {
            if (_bases.Count <= ProceduralTextureCombinationUtility.MinBaseCount)
            {
                _lastStatus = "At least one base is required.";
                return;
            }

            AddHistorySnapshot(_baseHistory, "Before Delete Base", false);
            _bases.RemoveAt(Mathf.Clamp(_selectedBase, 0, _bases.Count - 1));
            SelectSingleBase(Mathf.Clamp(_selectedBase, 0, _bases.Count - 1));
            MarkDirty("Deleted base.");
        }

        private void RandomizeSeeds()
        {
            AddHistorySnapshot(_baseHistory, "Before Randomize", false);
            for (int i = 0; i < _bases.Count; i++)
            {
                if (_bases[i].locks == null || !_bases[i].locks.seed)
                    _bases[i].generation.seed = NewEditorSeed(i);
            }
            GenerateCandidates();
            _lastStatus = "Randomized base seeds.";
        }

        private void RandomizeSelectedBaseParameters()
        {
            AddHistorySnapshot(_baseHistory, "Before Randomize Selected Bases", false);
            foreach (int index in _selectedBases)
            {
                if (index >= 0 && index < _bases.Count)
                    RandomizeBaseParameters(_bases[index], false);
            }
            MarkDirty("Randomized unlocked base parameters.");
        }

        private void RandomizeUnlockedVariants()
        {
            AddHistorySnapshot(_combinationHistory, "Before Randomize Unlocked Textures", true);
            GenerateCandidates();
            _lastStatus = "Randomizing unlocked textures.";
        }

        private void ToggleVariantLock(int index)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            _candidates[index].locked = !_candidates[index].locked;
            _candidates[index].guidanceMode = _candidates[index].locked ? ProceduralTextureGuidanceMode.Guide : ProceduralTextureGuidanceMode.None;
            _candidates[index].guideStrength01 = Mathf.Clamp01(_candidates[index].influenceWeight);
            _lastStatus = _candidates[index].locked ? "Texture will become a Guide on Generate." : "Texture will stay unguided in the grid on Generate.";
            RequestSessionSave();
            Repaint();
        }

        private void ToggleSelectedVariantLocks()
        {
            if (_selectedCandidates.Count == 0)
                return;

            bool shouldLock = false;
            foreach (int index in _selectedCandidates)
            {
                if (index >= 0 && index < _candidates.Count && !_candidates[index].locked)
                {
                    shouldLock = true;
                    break;
                }
            }

            foreach (int index in _selectedCandidates)
            {
                if (index >= 0 && index < _candidates.Count)
                {
                    _candidates[index].locked = shouldLock;
                    _candidates[index].guidanceMode = shouldLock ? ProceduralTextureGuidanceMode.Guide : ProceduralTextureGuidanceMode.None;
                    _candidates[index].guideStrength01 = Mathf.Clamp01(_candidates[index].influenceWeight);
                }
            }

            _lastStatus = shouldLock ? "Selected textures will become Guides on Generate." : "Selected textures will stay unguided in the grid on Generate.";
            SaveSession();
            Repaint();
        }

        private int LockedVariantCount()
        {
            return _influences.Count + GridLockedVariantCount();
        }

        private int GridLockedVariantCount()
        {
            int count = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i] != null && _candidates[i].locked)
                    count++;
            }
            return count;
        }

        private void DuplicateSelectedVariant()
        {
            if (_selectedCandidates.Count == 0)
                return;

            foreach (int index in _selectedCandidates)
            {
                DuplicateVariant(index);
                return;
            }
        }

        private void DuplicateVariant(int index)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            if (_candidates.Count >= 16)
            {
                _lastStatus = "Maximum 16 textures. Increase quality by refining or export before duplicating more.";
                return;
            }

            AddHistorySnapshot(_combinationHistory, "Before Duplicate Texture", true);
            ProceduralTextureCandidate clone = CloneVariant(_candidates[index], true);
            clone.label = $"Texture {_candidates.Count + 1}";
            clone.locked = false;
            clone.selected = true;
            _candidates.Add(clone);
            _combination.candidateCount = Mathf.Clamp(_candidates.Count, 1, 16);
            _selectedCandidates.Clear();
            _selectedCandidates.Add(_candidates.Count - 1);
            SetActiveCandidate(_candidates.Count - 1, false);
            _lastStatus = "Duplicated texture.";
            SaveSession();
            Repaint();
        }

        private void SortCandidatesByConstraintScore()
        {
            if (_candidates.Count <= 1)
                return;

            AddHistorySnapshot(_combinationHistory, "Before Sort by Score", true);
            ProceduralTextureConstraintSortMode sortMode = _combination != null ? _combination.constraintSortMode : ProceduralTextureConstraintSortMode.Overall;
            _candidates.Sort((left, right) => ConstraintSortValue(right, sortMode).CompareTo(ConstraintSortValue(left, sortMode)));
            _selectedCandidates.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i] == null)
                    continue;
                _candidates[i].label = $"Texture {i + 1}";
                _candidates[i].selected = i == 0;
                _candidates[i].isActive = i == 0;
                if (i == 0)
                    _selectedCandidates.Add(i);
            }
            _activeCandidateIndex = _candidates.Count > 0 ? 0 : -1;
            if (IsValidActiveCandidate())
                AdoptCandidate(_activeCandidateIndex, false);
            _lastStatus = $"Sorted textures by {ObjectNames.NicifyVariableName(sortMode.ToString()).ToLowerInvariant()}.";
            SaveSession();
            Repaint();
        }

        private void RelaxConstraintTarget()
        {
            _combination.seamScoreThreshold = Mathf.Clamp01(_combination.seamScoreThreshold - 0.06f);
            Relax01Range(ref _combination.constraintCoverageMin01, ref _combination.constraintCoverageMax01, 0.05f);
            Relax01Range(ref _combination.constraintContrastMin01, ref _combination.constraintContrastMax01, 0.06f);
            Relax01Range(ref _combination.constraintScaleMin01, ref _combination.constraintScaleMax01, 0.05f);
            _lastStatus = "Relaxed seam, coverage, contrast, and scale constraints.";
            MarkDirty("Relaxed constraints.");
        }

        private static void Relax01Range(ref float min, ref float max, float amount)
        {
            min = Mathf.Clamp01(min - amount);
            max = Mathf.Clamp01(max + amount);
            if (max < min)
                max = min;
        }

        private void ProcessGenerationQueue()
        {
            if (!_generationRunning)
                return;

            if (_generationJobs.Count == 0)
            {
                FinishGenerationQueue();
                return;
            }

            TextureGenerationJob job = _generationJobs.Dequeue();
            List<ProceduralTextureCandidate> target = job.targetInfluence ? _influences : _candidates;
            if (job.slotIndex >= 0 && job.slotIndex < target.Count)
            {
                ProceduralTextureCandidate previous = target[job.slotIndex];
                if (job.rebuildExisting && previous != null)
                {
                    DestroyVariantGeneratedData(previous);
                    GenerateVariantPreviewData(previous, job.activePreview ? ActivePreviewSize : GridPreviewSize);
                    previous.isActive = !job.targetInfluence && job.slotIndex == _activeCandidateIndex;
                    if (previous.selected)
                        _selectedCandidates.Add(job.slotIndex);
                }
                else
                {
                    DestroyVariantGeneratedData(previous);
                    ProceduralTextureCandidate generated = CreateGeneratedVariant(job);
                    generated.selected = !job.targetInfluence && previous != null && previous.selected && !previous.locked;
                    generated.locked = false;
                    generated.isActive = !job.targetInfluence && job.slotIndex == _activeCandidateIndex;
                    target[job.slotIndex] = generated;
                    if (generated.selected)
                        _selectedCandidates.Add(job.slotIndex);
                }
            }

            _generationCompleted++;
            _lastStatus = $"Generating {Mathf.Min(_generationCompleted, _generationTotal)}/{_generationTotal}...";
            Repaint();

            if (_generationJobs.Count == 0)
                FinishGenerationQueue();
        }

        private void FinishGenerationQueue()
        {
            _generationRunning = false;
            EnsureActiveVariant();
            _lastStatus = $"Generated {_generationCompleted} texture(s).";
            RequestSessionSave();
            Repaint();
        }

        private void CancelGenerationJobs(bool repaint)
        {
            _generationJobs.Clear();
            _generationRunning = false;
            _generationTotal = 0;
            _generationCompleted = 0;
            if (repaint)
            {
                _lastStatus = "Generation cancelled.";
                Repaint();
            }
        }

        private void QueueMissingVariantPreviews()
        {
            if (_candidates.Count == 0)
                return;

            CancelGenerationJobs(false);
            for (int i = 0; i < _candidates.Count; i++)
            {
                ProceduralTextureCandidate candidate = _candidates[i];
                if (candidate == null || candidate.preview != null || candidate.bases == null || candidate.bases.Length == 0)
                    continue;

                _generationJobs.Enqueue(new TextureGenerationJob
                {
                    slotIndex = i,
                    seed = candidate.seedOffset != 0 ? candidate.seedOffset : ProceduralTextureRecipeRandomizer.HashSeed(Environment.TickCount, i),
                    activePreview = i == _activeCandidateIndex,
                    rebuildExisting = true,
                    lockedInfluences = Array.Empty<ProceduralTextureBaseSettings>()
                });
            }

            for (int i = 0; i < _influences.Count; i++)
            {
                ProceduralTextureCandidate influence = _influences[i];
                if (influence == null || influence.preview != null || influence.bases == null || influence.bases.Length == 0)
                    continue;

                _generationJobs.Enqueue(new TextureGenerationJob
                {
                    slotIndex = i,
                    seed = influence.seedOffset != 0 ? influence.seedOffset : ProceduralTextureRecipeRandomizer.HashSeed(Environment.TickCount, i + 1000),
                    rebuildExisting = true,
                    targetInfluence = true,
                    lockedInfluences = Array.Empty<ProceduralTextureBaseSettings>()
                });
            }

            _generationRunning = _generationJobs.Count > 0;
            _generationTotal = _generationJobs.Count;
            _generationCompleted = 0;
            if (_generationRunning)
                _lastStatus = $"Rebuilding previews 0/{_generationTotal}...";
        }

        private void RegenerateSelectedVariant(string status)
        {
            int index = FirstSelectedVariantIndex();
            if (index < 0 || index >= _candidates.Count)
                return;

            RegenerateVariant(index, status);
        }

        private void RegenerateVariant(int index, string status)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            ProceduralTextureCandidate variant = _candidates[index];
            if (variant == null)
                return;

            if (variant.bases == null || variant.bases.Length == 0)
                variant.bases = ProceduralTextureRecipeRandomizer.CreateFreshVariantRecipe(ProceduralTextureRecipeRandomizer.HashSeed(Environment.TickCount, index), _combination);
            variant.settings = _combination.Clone();
            DestroyVariantGeneratedData(variant);
            GenerateVariantPreviewData(variant, index == _activeCandidateIndex ? ActivePreviewSize : GridPreviewSize);

            if (index == _activeCandidateIndex)
            {
                AdoptCandidate(index, false);
                QueueGuidedRefineFeaturePreviewRebuild();
            }
            _lastStatus = status;
            SaveSession();
            Repaint();
        }

        private void GenerateVariantPreviewData(ProceduralTextureCandidate candidate, int previewSize)
        {
            if (candidate == null)
                return;

            ProceduralTextureCombinationSettings previewSettings = CreateSizedSettings(candidate.settings ?? _combination, previewSize);
            candidate.values = ProceduralTextureCombinationUtility.GenerateCombinedValues(candidate.bases, previewSettings, candidate.seedOffset);
            candidate.pixels = ProceduralTextureCombinationUtility.ValuesToPixels(candidate.values, previewSettings, PaletteColors());
            candidate.valuesWidth = previewSettings.width;
            candidate.valuesHeight = previewSettings.height;
            candidate.seamScore01 = ProceduralTextureCombinationUtility.MeasureSeamScore(candidate.values, candidate.valuesWidth, candidate.valuesHeight);
            MeasureVariantConstraints(candidate);
            candidate.preview = ProceduralTextureCombinationUtility.CreateTexture(previewSettings.width, previewSettings.height, TextureFormat.RGBA32, false, true, candidate.pixels, candidate.label);
            candidate.seamHeatmap = CreateSeamHeatmapTexture(candidate);
        }

        private void MeasureVariantConstraints(ProceduralTextureCandidate candidate)
        {
            if (candidate == null || candidate.values == null || candidate.values.Length == 0)
                return;

            ProceduralTextureToneStats tone = ProceduralTextureCombinationUtility.MeasureToneStats(candidate.values);
            int covered = 0;
            for (int i = 0; i < candidate.values.Length; i++)
            {
                if (candidate.values[i] >= 0.5f)
                    covered++;
            }

            candidate.coverage01 = covered / (float)Mathf.Max(1, candidate.values.Length);
            candidate.contrast01 = Mathf.Clamp01(tone.percentileRange);
            candidate.scale01 = EstimateVariantScale(candidate);
            candidate.constraintScore01 = ScoreVariantConstraints(candidate);
        }

        private float EstimateVariantScale(ProceduralTextureCandidate candidate)
        {
            if (candidate?.bases == null || candidate.bases.Length == 0)
                return 0.5f;

            float total = 0f;
            int count = 0;
            for (int i = 0; i < candidate.bases.Length; i++)
            {
                ProceduralTextureGenerationSettings generation = candidate.bases[i]?.generation;
                if (generation == null)
                    continue;
                total += Mathf.Clamp01((generation.radiusMin01 + generation.radiusMax01) * 0.5f / 0.5f);
                count++;
            }
            return count > 0 ? Mathf.Clamp01(total / count) : 0.5f;
        }

        private float ScoreVariantConstraints(ProceduralTextureCandidate candidate)
        {
            if (candidate == null)
                return 0f;

            float seamScore = ScoreMinimum(candidate.seamScore01, _combination.seamScoreThreshold);
            float coverageScore = ScoreRange(candidate.coverage01, _combination.constraintCoverageMin01, _combination.constraintCoverageMax01);
            float contrastScore = ScoreRange(candidate.contrast01, _combination.constraintContrastMin01, _combination.constraintContrastMax01);
            float scaleScore = ScoreRange(candidate.scale01, _combination.constraintScaleMin01, _combination.constraintScaleMax01);
            float seamWeight = _tileabilityMode == TextureTileabilityMode.GenerateSeamless || _combination.generateSeamless ? 0.42f : 0.22f;
            float total = seamScore * seamWeight
                + coverageScore * 0.22f
                + contrastScore * 0.24f
                + scaleScore * 0.12f;
            return Mathf.Clamp01(total);
        }

        private static float ScoreMinimum(float value, float minimum)
        {
            minimum = Mathf.Clamp01(minimum);
            value = Mathf.Clamp01(value);
            if (value >= minimum)
                return 1f;
            return Mathf.Clamp01(value / Mathf.Max(0.001f, minimum));
        }

        private static float ScoreRange(float value, float min, float max)
        {
            value = Mathf.Clamp01(value);
            min = Mathf.Clamp01(min);
            max = Mathf.Clamp01(max);
            if (max < min)
                max = min;
            if (value >= min && value <= max)
                return 1f;

            float center = (min + max) * 0.5f;
            float tolerance = Mathf.Max(0.08f, Mathf.Abs(max - min) * 0.75f);
            float distance = value < min ? min - value : value - max;
            float edgeFalloff = 1f - distance / tolerance;
            float broadFalloff = 1f - Mathf.Abs(value - center) / Mathf.Max(0.001f, Mathf.Max(center, 1f - center));
            return Mathf.Clamp01(Mathf.Max(edgeFalloff, broadFalloff * 0.55f));
        }

        private float ConstraintSortValue(ProceduralTextureCandidate candidate, ProceduralTextureConstraintSortMode sortMode)
        {
            if (candidate == null)
                return 0f;

            switch (sortMode)
            {
                case ProceduralTextureConstraintSortMode.Seam:
                    return candidate.seamScore01;
                case ProceduralTextureConstraintSortMode.Coverage:
                    return ScoreRange(candidate.coverage01, _combination.constraintCoverageMin01, _combination.constraintCoverageMax01);
                case ProceduralTextureConstraintSortMode.Contrast:
                    return ScoreRange(candidate.contrast01, _combination.constraintContrastMin01, _combination.constraintContrastMax01);
                case ProceduralTextureConstraintSortMode.Scale:
                    return ScoreRange(candidate.scale01, _combination.constraintScaleMin01, _combination.constraintScaleMax01);
                default:
                    return candidate.constraintScore01;
            }
        }

        private bool CandidatePassesConstraints(ProceduralTextureCandidate candidate)
        {
            if (candidate == null || _combination == null)
                return false;

            return candidate.seamScore01 >= _combination.seamScoreThreshold
                && candidate.coverage01 >= _combination.constraintCoverageMin01
                && candidate.coverage01 <= _combination.constraintCoverageMax01
                && candidate.contrast01 >= _combination.constraintContrastMin01
                && candidate.contrast01 <= _combination.constraintContrastMax01
                && candidate.scale01 >= _combination.constraintScaleMin01
                && candidate.scale01 <= _combination.constraintScaleMax01;
        }

        private int ConstraintPassCount()
        {
            int count = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (CandidatePassesConstraints(_candidates[i]))
                    count++;
            }
            return count;
        }

        private Texture2D CreateSeamHeatmapTexture(ProceduralTextureCandidate candidate)
        {
            if (candidate == null || candidate.values == null || candidate.valuesWidth <= 0 || candidate.valuesHeight <= 0)
                return null;

            Color[] heatmap = ProceduralTextureCombinationUtility.CreateSeamHeatmapPixels(candidate.values, candidate.valuesWidth, candidate.valuesHeight);
            if (heatmap == null || heatmap.Length == 0)
                return null;
            return ProceduralTextureCombinationUtility.CreateTexture(candidate.valuesWidth, candidate.valuesHeight, TextureFormat.RGBA32, false, true, heatmap, $"{candidate.label} Seam Heatmap");
        }

        private void EnsureMinimumPreviewContrast(ProceduralTextureCandidate candidate, ProceduralTextureBaseSettings[] lockedInfluences, int previewSize)
        {
            if (candidate == null || candidate.values == null || HasUsableToneRange(candidate.values))
                return;

            for (int attempt = 0; attempt < 3 && !HasUsableToneRange(candidate.values); attempt++)
            {
                candidate.seedOffset = ProceduralTextureRecipeRandomizer.HashSeed(candidate.seedOffset, attempt + 31);
                candidate.bases = lockedInfluences != null && lockedInfluences.Length > 0
                    ? ProceduralTextureRecipeRandomizer.CreateInfluencedVariantRecipe(candidate.seedOffset, lockedInfluences, candidate.settings ?? _combination)
                    : ProceduralTextureRecipeRandomizer.CreateFreshVariantRecipe(candidate.seedOffset, candidate.settings ?? _combination);
                DestroyVariantGeneratedData(candidate);
                GenerateVariantPreviewData(candidate, previewSize);
            }
        }

        private static bool HasUsableToneRange(float[] values)
        {
            if (values == null || values.Length == 0)
                return false;

            ProceduralTextureToneStats stats = ProceduralTextureCombinationUtility.MeasureToneStats(values);
            return stats.range >= 0.72f
                && stats.percentileRange >= 0.42f
                && stats.darkCoverage >= 0.025f
                && stats.midCoverage >= 0.04f
                && stats.lightCoverage >= 0.025f;
        }

        private static ProceduralTextureCombinationSettings CreateGeneratedCombinationSettings(ProceduralTextureCombinationSettings source)
        {
            ProceduralTextureCombinationSettings settings = source != null ? source.Clone() : new ProceduralTextureCombinationSettings();
            settings.autoBalance = true;
            settings.autoBalanceStrength = Mathf.Max(settings.autoBalanceStrength, 1f);
            settings.contrast = Mathf.Max(settings.contrast, 1.15f);
            settings.brightness = 0f;
            settings.gamma = 1f;
            settings.Clamp();
            return settings;
        }

        private static void PreserveGlobalGenerationControls(ProceduralTextureCombinationSettings source, ProceduralTextureCombinationSettings target)
        {
            if (source == null || target == null)
                return;

            target.candidateCount = source.candidateCount;
            target.randomness01 = source.randomness01;
            target.lockedInfluence01 = source.lockedInfluence01;
            target.generationPatternVariety01 = source.generationPatternVariety01;
            target.generationDensityMin = source.generationDensityMin;
            target.generationDensityMax = source.generationDensityMax;
            target.generationRadiusMin01 = source.generationRadiusMin01;
            target.generationRadiusMax01 = source.generationRadiusMax01;
            target.generationIntensityMin = source.generationIntensityMin;
            target.generationIntensityMax = source.generationIntensityMax;
            target.generationContrastMin = source.generationContrastMin;
            target.generationContrastMax = source.generationContrastMax;
            target.tilePreview = source.tilePreview;
            target.generateSeamless = source.generateSeamless;
            target.wrapStampsAcrossEdges = source.wrapStampsAcrossEdges;
            target.toroidalSpacing = source.toroidalSpacing;
            target.seamRepairStrength = source.seamRepairStrength;
            target.edgeMatchWeight = source.edgeMatchWeight;
            target.seamScoreThreshold = source.seamScoreThreshold;
            target.constraintCoverageMin01 = source.constraintCoverageMin01;
            target.constraintCoverageMax01 = source.constraintCoverageMax01;
            target.constraintContrastMin01 = source.constraintContrastMin01;
            target.constraintContrastMax01 = source.constraintContrastMax01;
            target.constraintScaleMin01 = source.constraintScaleMin01;
            target.constraintScaleMax01 = source.constraintScaleMax01;
            target.constraintSortMode = source.constraintSortMode;
            target.Clamp();
        }

        private static int NewEditorSeed(int salt)
        {
            return ProceduralTextureRecipeRandomizer.HashSeed(Environment.TickCount, salt);
        }

        private ProceduralTextureCombinationSettings CreateSizedSettings(ProceduralTextureCombinationSettings source, int maxSize)
        {
            ProceduralTextureCombinationSettings settings = (source ?? _combination).Clone();
            int largest = Mathf.Max(1, Mathf.Max(settings.width, settings.height));
            float scale = Mathf.Min(1f, maxSize / (float)largest);
            settings.width = Mathf.Max(8, Mathf.RoundToInt(settings.width * scale));
            settings.height = Mathf.Max(8, Mathf.RoundToInt(settings.height * scale));
            settings.mipChain = false;
            settings.textureFormat = TextureFormat.RGBA32;
            settings.Clamp();
            return settings;
        }

        private void EnsureFullVariantData(ProceduralTextureCandidate candidate)
        {
            if (candidate == null)
                return;
            ProceduralTextureCombinationSettings fullSettings = candidate.settings ?? _combination;
            int expectedLength = Mathf.Max(8, fullSettings.width) * Mathf.Max(8, fullSettings.height);
            if (candidate.fullValues != null && candidate.fullValues.Length == expectedLength && candidate.fullPixels != null)
                return;

            candidate.fullValues = ProceduralTextureCombinationUtility.GenerateCombinedValues(candidate.bases, fullSettings, candidate.seedOffset);
            candidate.fullPixels = ProceduralTextureCombinationUtility.ValuesToPixels(candidate.fullValues, fullSettings, PaletteColors());
        }

        private void DestroyVariantGeneratedData(ProceduralTextureCandidate candidate)
        {
            if (candidate == null)
                return;

            if (candidate.preview != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(candidate.preview);
            if (candidate.seamHeatmap != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(candidate.seamHeatmap);
            candidate.preview = null;
            candidate.seamHeatmap = null;
            candidate.values = null;
            candidate.pixels = null;
            candidate.valuesWidth = 0;
            candidate.valuesHeight = 0;
            candidate.fullValues = null;
            candidate.fullPixels = null;
        }

        private void RandomizeBaseParameters(ProceduralTextureBaseSettings textureBase, bool markDirty = true)
        {
            if (textureBase == null)
                return;

            ProceduralTextureBaseLockSettings locks = textureBase.locks ?? new ProceduralTextureBaseLockSettings();
            var rng = new System.Random(ProceduralTextureRecipeRandomizer.HashSeed(Environment.TickCount, textureBase.generation != null ? textureBase.generation.seed : 0));
            ProceduralTextureRecipeRandomizer.RandomizeUnlockedGroups(textureBase, locks, rng);

            textureBase.Clamp();
            if (markDirty)
                MarkDirty("Randomized unlocked base parameters.");
        }

        private void ResetRecipe()
        {
            AddHistorySnapshot(_baseHistory, "Before Reset Recipe", true);
            _bases.Clear();
            _bases.Add(ProceduralTextureBaseSettings.CreateDefault(0));
            _combination = new ProceduralTextureCombinationSettings();
            _activeCandidateIndex = -1;
            SelectSingleBase(0);
            ClearCandidates();
            ClearInfluences();
            GeneratePreview(false);
            _lastStatus = "Reset recipe.";
        }

        private void RestoreSnapshot(TextureHistorySnapshot snapshot)
        {
            if (snapshot == null)
                return;

            AddHistorySnapshot(_combinationHistory, "Before Restore", true);
            _bases.Clear();
            if (snapshot.bases != null)
                _bases.AddRange(ProceduralTextureCombinationUtility.CloneBases(snapshot.bases));
            EnsureBaseBounds();
            if (snapshot.settings != null)
                _combination = snapshot.settings.Clone();
            RestoreSessionInfluences(snapshot.influences);

            DestroyCurrentPreview();
            _previewValues = snapshot.values != null ? (float[])snapshot.values.Clone() : null;
            _previewPixels = snapshot.pixels != null ? (Color[])snapshot.pixels.Clone() : null;
            if (_previewPixels != null)
                _previewTexture = ProceduralTextureCombinationUtility.CreateTexture(_combination.width, _combination.height, _combination.textureFormat, _combination.mipChain, _combination.linear, _previewPixels, "Restored Texture Preview");
            else
                GeneratePreview(false);
            _previewDirty = false;
            SelectSingleBase(Mathf.Clamp(_selectedBase, 0, _bases.Count - 1));
            QueueBasePreviewRebuild();
            QueueMissingVariantPreviews();
            _lastStatus = $"Restored {snapshot.label}.";
            SaveSession();
        }

        private void AddHistorySnapshot(List<TextureHistorySnapshot> target, string label, bool includePreview)
        {
            var snapshot = new TextureHistorySnapshot
            {
                label = label,
                createdUtc = DateTime.UtcNow.ToString("u"),
                settings = _combination.Clone(),
                bases = ProceduralTextureCombinationUtility.CloneBases(_bases),
                influences = CloneVariantSession(_influences),
                values = includePreview && _previewValues != null ? (float[])_previewValues.Clone() : null,
                pixels = includePreview && _previewPixels != null ? (Color[])_previewPixels.Clone() : null,
                workflow = _activeWorkflow,
                generationNumber = _generationNumber,
                parentSummary = _influences.Count > 0 ? $"{_influences.Count} guide(s)" : "Fresh generation",
                inheritedTraits = BuildSnapshotTraitSummary(),
                mutationAmount = _combination.randomness01,
                score01 = IsValidActiveCandidate() ? _candidates[_activeCandidateIndex].constraintScore01 : 0f
            };
            if (snapshot.pixels != null)
            {
                int width = _previewTexture != null ? _previewTexture.width : snapshot.settings.width;
                int height = _previewTexture != null ? _previewTexture.height : snapshot.settings.height;
                snapshot.preview = ProceduralTextureCombinationUtility.CreateTexture(width, height, TextureFormat.RGBA32, false, true, snapshot.pixels, label);
            }

            target.Insert(0, snapshot);
            while (target.Count > 18)
            {
                TextureHistorySnapshot last = target[target.Count - 1];
                if (last.preview != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(last.preview);
                target.RemoveAt(target.Count - 1);
            }
        }

        private void RemoveInfluence(int index)
        {
            if (index < 0 || index >= _influences.Count)
                return;

            AddHistorySnapshot(_baseHistory, $"Before Remove {_influences[index].label}", false);
            DestroyVariantGeneratedData(_influences[index]);
            _influences.RemoveAt(index);
            QueueInfluenceFeaturePreviewRebuild();
            _lastStatus = "Removed guide. Restore it from History if needed.";
            RequestSessionSave();
            Repaint();
        }

        private void MarkDirty(string status)
        {
            ClampBases();
            _previewDirty = true;
            _basePreviewsDirty = true;
            _lastStatus = status;
            QueueBasePreviewRebuild();
            QueueManualComposeAutoPreview(status);
            RequestSessionSave();
            Repaint();
        }

        private void QueueManualComposeAutoPreview(string reason)
        {
            if (_activeWorkflow != TextureDesignWorkflow.ManualCompose || !_manualComposeAutoPreview)
                return;

            _manualComposeAutoPreviewQueued = true;
            _manualComposeAutoPreviewAt = EditorApplication.timeSinceStartup + ManualComposeAutoPreviewDebounceSeconds;
            _manualComposeAutoPreviewReason = reason;
        }

        private void ProcessManualComposeAutoPreview()
        {
            if (!_manualComposeAutoPreviewQueued || _generationRunning)
                return;
            if (EditorApplication.timeSinceStartup < _manualComposeAutoPreviewAt)
                return;
            if (_activeWorkflow != TextureDesignWorkflow.ManualCompose || !_manualComposeAutoPreview)
            {
                _manualComposeAutoPreviewQueued = false;
                return;
            }

            GeneratePreview(false);
            _lastStatus = string.IsNullOrEmpty(_manualComposeAutoPreviewReason)
                ? "Auto preview refreshed."
                : $"Auto preview refreshed after {_manualComposeAutoPreviewReason.ToLowerInvariant()}";
        }

        private void ClampBases()
        {
            EnsureBaseBounds();
            for (int i = 0; i < _bases.Count; i++)
                _bases[i].Clamp();
        }

        private void EnsureBaseBounds()
        {
            if (_bases.Count == 0)
                _bases.Add(ProceduralTextureBaseSettings.CreateDefault(0));
            while (_bases.Count > ProceduralTextureCombinationUtility.MaxBaseCount)
                _bases.RemoveAt(_bases.Count - 1);
            _selectedBase = Mathf.Clamp(_selectedBase, 0, _bases.Count - 1);
            _selectedBases.RemoveWhere(index => index < 0 || index >= _bases.Count);
            if (_selectedBases.Count == 0)
                _selectedBases.Add(_selectedBase);
        }

        private void QueueBasePreviewRebuild()
        {
            if (!_basePreviewsDirty)
                return;

            EditorApplication.delayCall -= RebuildBasePreviews;
            EditorApplication.delayCall += RebuildBasePreviews;
        }

        private void RebuildBasePreviews()
        {
            if (this == null)
                return;

            DestroyBasePreviews();
            DestroyManualContributionCache();
            for (int index = 0; index < _bases.Count; index++)
            {
                ProceduralTextureBaseSettings textureBase = _bases[index];
                if (textureBase == null)
                {
                    _basePreviewTextures.Add(null);
                    _manualLayerContributions.Add(null);
                    continue;
                }

                float[] values;
                int previewWidth = 64;
                int previewHeight = 64;
                if (textureBase.useBakedValues)
                {
                    var previewSettings = new ProceduralTextureCombinationSettings
                    {
                        width = previewWidth,
                        height = previewHeight,
                        textureFormat = TextureFormat.RGBA32,
                        mipChain = false,
                        linear = true
                    };
                    values = ProceduralTextureCombinationUtility.GenerateCombinedValues(new[] { textureBase }, previewSettings);
                }
                else
                {
                    ProceduralTextureGenerationSettings settings = textureBase.generation.Clone();
                    settings.width = previewWidth;
                    settings.height = previewHeight;
                    settings.textureFormat = TextureFormat.RGBA32;
                    settings.mipChain = false;
                    settings.linear = true;
                    values = ProceduralTextureGenerator.GenerateValues(settings);
                }
                Color[] pixels = new Color[values.Length];
                for (int i = 0; i < values.Length; i++)
                    pixels[i] = new Color(values[i], values[i], values[i], textureBase.enabled ? 1f : 0.46f);
                _basePreviewTextures.Add(ProceduralTextureCombinationUtility.CreateTexture(previewWidth, previewHeight, TextureFormat.RGBA32, false, true, pixels, $"Base Preview {index + 1}"));
                _manualLayerContributions.Add(CreateManualLayerContribution(index, textureBase, values, previewWidth, previewHeight));
            }
            _basePreviewsDirty = false;
            Repaint();
        }

        private ManualLayerContributionEntry CreateManualLayerContribution(int index, ProceduralTextureBaseSettings textureBase, float[] values, int width, int height)
        {
            if (textureBase == null || values == null || values.Length == 0)
                return null;

            MeasureValues(values, out float min, out float max, out float average, out float coverage);
            return new ManualLayerContributionEntry
            {
                index = index,
                label = string.IsNullOrEmpty(textureBase.name) ? $"Layer {index + 1}" : textureBase.name,
                blendMode = textureBase.blendMode,
                weight = Mathf.Clamp01(textureBase.weight),
                coverage01 = coverage,
                contrast01 = Mathf.Clamp01(max - min),
                contribution01 = textureBase.enabled ? Mathf.Clamp01(coverage * Mathf.Clamp01(textureBase.weight) * Mathf.Max(0.15f, max - min)) : 0f,
                strip = CreateManualLayerStrip(values, width, height, $"Layer {index + 1} Contribution"),
                highlight = CreateManualLayerHighlight(values, width, height, $"Layer {index + 1} Highlight")
            };
        }

        private Texture2D CreateManualLayerStrip(float[] values, int width, int height, string name)
        {
            const int stripWidth = 96;
            const int stripHeight = 18;
            var pixels = new Color[stripWidth * stripHeight];
            for (int y = 0; y < stripHeight; y++)
            {
                int sourceY = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) / stripHeight * height), 0, height - 1);
                for (int x = 0; x < stripWidth; x++)
                {
                    int sourceX = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) / stripWidth * width), 0, width - 1);
                    float value = values[sourceY * width + sourceX];
                    pixels[y * stripWidth + x] = new Color(value, value, value, 1f);
                }
            }
            return ProceduralTextureCombinationUtility.CreateTexture(stripWidth, stripHeight, TextureFormat.RGBA32, false, true, pixels, name);
        }

        private Texture2D CreateManualLayerHighlight(float[] values, int width, int height, string name)
        {
            if (values == null || values.Length == 0 || width <= 0 || height <= 0)
                return null;

            var pixels = new Color[width * height];
            Color tint = ComposeTint();
            for (int i = 0; i < pixels.Length && i < values.Length; i++)
            {
                float alpha = Mathf.Clamp01(values[i]) * 0.46f;
                pixels[i] = new Color(tint.r, tint.g, tint.b, alpha);
            }
            return ProceduralTextureCombinationUtility.CreateTexture(width, height, TextureFormat.RGBA32, false, true, pixels, name);
        }

        private void UpdateManualOutputMetrics()
        {
            if (_previewValues == null || _previewValues.Length == 0)
            {
                _manualOutputCoverage01 = 0f;
                _manualOutputContrast01 = 0f;
                _manualOutputToneMin01 = 0f;
                _manualOutputToneMax01 = 0f;
                _manualOutputSeamScore01 = 0f;
                return;
            }

            MeasureValues(_previewValues, out float min, out float max, out float average, out float coverage);
            _manualOutputCoverage01 = coverage;
            _manualOutputContrast01 = Mathf.Clamp01(max - min);
            _manualOutputToneMin01 = min;
            _manualOutputToneMax01 = max;
            _manualOutputSeamScore01 = CalculateSeamScore(_previewValues, _combination.width, _combination.height);
        }

        private static void MeasureValues(float[] values, out float min, out float max, out float average, out float coverage)
        {
            min = 1f;
            max = 0f;
            double total = 0d;
            int covered = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float value = Mathf.Clamp01(values[i]);
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
                total += value;
                if (value > 0.08f)
                    covered++;
            }
            average = values.Length > 0 ? (float)(total / values.Length) : 0f;
            coverage = values.Length > 0 ? covered / (float)values.Length : 0f;
        }

        private static float CalculateSeamScore(float[] values, int width, int height)
        {
            if (values == null || width <= 1 || height <= 1 || values.Length < width * height)
                return 0f;

            double difference = 0d;
            for (int y = 0; y < height; y++)
                difference += Mathf.Abs(values[y * width] - values[y * width + width - 1]);
            for (int x = 0; x < width; x++)
                difference += Mathf.Abs(values[x] - values[(height - 1) * width + x]);

            return Mathf.Clamp01(1f - (float)(difference / Math.Max(1, width + height)));
        }

        private void DestroyPreviewState()
        {
            EditorApplication.delayCall -= RebuildBasePreviews;
            DestroyCurrentPreview();
            ClearCandidates();
            ClearInfluences();
            DestroyInfluenceFeaturePreviewCache();
            DestroyGuidedRefineFeaturePreviewCache();
            DestroyReferenceFeaturePreview();
            DestroyPresetPreviews();
            DestroyMapPreviews();
            if (_blendOutputPreview != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_blendOutputPreview);
            _blendOutputPreview = null;
            DestroyManualContributionCache();
            DestroyBasePreviews();
            DestroyHistory(_baseHistory);
            DestroyHistory(_combinationHistory);
        }

        private string BuildSnapshotTraitSummary()
        {
            if (IsValidActiveCandidate())
            {
                ProceduralTextureCandidate candidate = _candidates[_activeCandidateIndex];
                var traits = new List<string>();
                if (candidate.preserveStructure) traits.Add("Structure");
                if (candidate.preserveDensity) traits.Add("Density");
                if (candidate.preserveScale) traits.Add("Scale");
                if (candidate.preserveDetail) traits.Add("Detail");
                if (candidate.preserveTone) traits.Add("Tone");
                if (candidate.preserveSeam) traits.Add("Seam");
                if (traits.Count > 0)
                    return string.Join(" · ", traits);
            }
            if (_activeWorkflow == TextureDesignWorkflow.BlendLab)
                return "A/B traits";
            if (_activeWorkflow == TextureDesignWorkflow.ReferenceMatch)
                return "Reference traits";
            if (_activeWorkflow == TextureDesignWorkflow.ConstraintMatch)
                return "Constraint score";
            return "Detail mutated";
        }

        private void DestroyCurrentPreview()
        {
            if (_previewTexture != null)
                ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_previewTexture);
            _previewTexture = null;
        }

        private void ClearCandidates()
        {
            for (int i = 0; i < _candidates.Count; i++)
                DestroyVariantGeneratedData(_candidates[i]);
            _candidates.Clear();
            _selectedCandidates.Clear();
            _activeCandidateIndex = -1;
            DestroyGuidedRefineFeaturePreviewCache();
        }

        private void ClearInfluences()
        {
            for (int i = 0; i < _influences.Count; i++)
                DestroyVariantGeneratedData(_influences[i]);
            _influences.Clear();
            DestroyInfluenceFeaturePreviewCache();
        }

        private ProceduralTextureCandidate[] CloneVariantSession(List<ProceduralTextureCandidate> variants)
        {
            if (variants == null || variants.Count == 0)
                return Array.Empty<ProceduralTextureCandidate>();

            var result = new ProceduralTextureCandidate[variants.Count];
            for (int i = 0; i < variants.Count; i++)
                result[i] = CloneVariant(variants[i], false);
            return result;
        }

        private void RestoreSessionVariants(ProceduralTextureCandidate[] variants)
        {
            ClearCandidates();
            if (variants == null || variants.Length == 0)
                return;

            for (int i = 0; i < variants.Length; i++)
            {
                ProceduralTextureCandidate restored = CloneVariant(variants[i], false);
                if (restored == null)
                    continue;
                restored.label = string.IsNullOrEmpty(restored.label) ? $"Texture {i + 1}" : restored.label;
                restored.preview = null;
                _candidates.Add(restored);
                if (restored.selected)
                    _selectedCandidates.Add(i);
            }
        }

        private void RestoreSessionInfluences(ProceduralTextureCandidate[] influences)
        {
            ClearInfluences();
            if (influences == null || influences.Length == 0)
                return;

            for (int i = 0; i < influences.Length; i++)
            {
                ProceduralTextureCandidate restored = CloneVariant(influences[i], false);
                if (restored == null)
                    continue;
                restored.label = string.IsNullOrEmpty(restored.label) ? $"Guide {i + 1}" : restored.label;
                restored.locked = true;
                restored.guidanceMode = ProceduralTextureGuidanceMode.Guide;
                restored.selected = false;
                restored.isActive = false;
                restored.preview = null;
                _influences.Add(restored);
            }
            QueueInfluenceFeaturePreviewRebuild();
        }

        private ProceduralTextureCandidate CloneVariant(ProceduralTextureCandidate source, bool includePreviewTexture)
        {
            if (source == null)
                return null;

            var clone = new ProceduralTextureCandidate
            {
                label = source.label,
                seedOffset = source.seedOffset,
                locked = source.locked,
                selected = source.selected,
                isActive = source.isActive,
                guidanceMode = source.guidanceMode,
                generationDepth = source.generationDepth,
                seamScore01 = source.seamScore01,
                coverage01 = source.coverage01,
                contrast01 = source.contrast01,
                scale01 = source.scale01,
                constraintScore01 = source.constraintScore01,
                influenceWeight = source.influenceWeight,
                guideStrength01 = source.guideStrength01,
                similarityTarget01 = source.similarityTarget01,
                userRating = source.userRating,
                preserveStructure = source.preserveStructure,
                preserveDensity = source.preserveDensity,
                preserveScale = source.preserveScale,
                preserveDetail = source.preserveDetail,
                preserveTone = source.preserveTone,
                preserveColourOrPalette = source.preserveColourOrPalette,
                preserveStampShape = source.preserveStampShape,
                preserveBlendOrder = source.preserveBlendOrder,
                preserveSeam = source.preserveSeam,
                contributorIndices = source.contributorIndices != null ? (int[])source.contributorIndices.Clone() : null,
                valuesWidth = includePreviewTexture ? source.valuesWidth : 0,
                valuesHeight = includePreviewTexture ? source.valuesHeight : 0,
                values = includePreviewTexture && source.values != null ? (float[])source.values.Clone() : null,
                pixels = includePreviewTexture && source.pixels != null ? (Color[])source.pixels.Clone() : null,
                fullValues = includePreviewTexture && source.fullValues != null ? (float[])source.fullValues.Clone() : null,
                fullPixels = includePreviewTexture && source.fullPixels != null ? (Color[])source.fullPixels.Clone() : null,
                settings = source.settings != null ? source.settings.Clone() : _combination.Clone(),
                bases = source.bases != null ? ProceduralTextureCombinationUtility.CloneBases(source.bases) : ProceduralTextureCombinationUtility.CloneBases(_bases),
                createdUtc = source.createdUtc
            };

            if (includePreviewTexture && clone.pixels != null && clone.settings != null)
            {
                clone.preview = CreateVariantPreview(clone);
                clone.seamHeatmap = CreateSeamHeatmapTexture(clone);
            }
            return clone;
        }

        private static int HashSeed(int seed, int index)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)(index + 1) * 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                value *= 0xC2B2AE35u;
                value ^= value >> 16;
                return (int)(value & 0x7FFFFFFF);
            }
        }

        private Texture2D CreateVariantPreview(ProceduralTextureCandidate candidate)
        {
            if (candidate == null || candidate.settings == null || candidate.pixels == null)
                return null;

            int width = candidate.valuesWidth > 0 ? candidate.valuesWidth : candidate.settings.width;
            int height = candidate.valuesHeight > 0 ? candidate.valuesHeight : candidate.settings.height;
            int maxSize = _activeInspector == TextureInspectorTab.Refine && candidate.isActive ? ActivePreviewSize : GridPreviewSize;
            return ProceduralTextureCombinationUtility.CreatePreviewTexture(width, height, candidate.pixels, maxSize, candidate.label);
        }

        private void DestroyBasePreviews()
        {
            for (int i = 0; i < _basePreviewTextures.Count; i++)
            {
                if (_basePreviewTextures[i] != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(_basePreviewTextures[i]);
            }
            _basePreviewTextures.Clear();
        }

        private void DestroyManualContributionCache()
        {
            for (int i = 0; i < _manualLayerContributions.Count; i++)
            {
                ManualLayerContributionEntry entry = _manualLayerContributions[i];
                if (entry != null && entry.strip != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(entry.strip);
                if (entry != null && entry.highlight != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(entry.highlight);
            }
            _manualLayerContributions.Clear();
        }

        private void DestroyHistory(List<TextureHistorySnapshot> history)
        {
            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].preview != null)
                    ProceduralTextureCombinationUtility.DestroyGeneratedTexture(history[i].preview);
            }
            history.Clear();
        }

        private Color[] PaletteColors()
        {
            if (_palette == null || _palette.swatches == null || _palette.swatches.Count == 0)
                return null;

            var colors = new List<Color>();
            for (int i = 0; i < _palette.swatches.Count; i++)
            {
                if (_palette.swatches[i] != null)
                    colors.Add(_palette.swatches[i].color);
            }
            return colors.Count > 0 ? colors.ToArray() : null;
        }
    }
#endif
}
