using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Generation
{
    public enum ProceduralTexturePattern
    {
        RandomMinSpacing,
        StratifiedJitterGrid,
        HexGrid,
        PoissonDisk,
        DensityMap,
        PolarPattern,
        HaltonSequence,
        HammersleySequence,
        SpiralPattern
    }

    public enum ProceduralTextureStampShape
    {
        SoftCircle,
        Gaussian,
        Cone,
        Ring,
        Square,
        Diamond,
        TextureSource
    }

    public enum ProceduralTextureBlendMode
    {
        Add,
        Max,
        Multiply,
        Replace,
        Subtract,
        Overlay
    }

    public enum ProceduralTextureChannel
    {
        R,
        G,
        B,
        A,
        Luminance
    }

    public enum ProceduralTextureColorMode
    {
        Grayscale,
        ForegroundBackground,
        Gradient
    }

    public enum TextureDesignWorkflow
    {
        ManualCompose,
        RandomExplore,
        GuidedRefine,
        BlendLab,
        ReferenceMatch,
        ConstraintMatch,
        PresetBrowser,
        MaterialMapPrep
    }

    public enum TextureDesignerPhase
    {
        Explore,
        Compose,
        Refine,
        Export,
        History
    }

    public enum TextureExploreStrategy
    {
        Random,
        Guided,
        Constrained,
        Presets
    }

    public enum TextureRefineMode
    {
        Polish,
        Similar,
        MixSources,
        MatchReference
    }

    public enum TextureComposeMode
    {
        Unguided,
        Guided
    }

    public enum ProceduralTextureEditSourceKind
    {
        Recipe,
        BakedValues,
        ImportedTexture,
        Snapshot,
        Unknown
    }

    public enum TextureWorkbenchLayout
    {
        CandidateGrid,
        FocusCanvas,
        LayerStack,
        PresetGrid,
        BlendBench,
        ReferenceSplit,
        ConstraintGrid,
        InfluenceMap,
        MapStrip,
        RecipeStack,
        LineageGraph
    }

    public enum TextureTileabilityMode
    {
        Off,
        PreviewOnly,
        GenerateSeamless
    }

    public enum TextureSeamOverlayMode
    {
        Off,
        Edges,
        Heatmap
    }

    public enum ProceduralTextureConstraintSortMode
    {
        Overall,
        Seam,
        Coverage,
        Contrast,
        Scale
    }

    public enum ProceduralTextureMapIntent
    {
        Mask,
        Height,
        Roughness,
        AlphaDissolve,
        DiffuseDetail,
        NormalDetail
    }

    public enum ProceduralTexturePresetCategory
    {
        Mask,
        Organic,
        Structure,
        Height,
        Roughness,
        Alpha
    }

    public enum ProceduralTextureSourceSamplingMode
    {
        Wrap,
        Mirror,
        Clamp
    }

    public enum ProceduralTextureGuidanceMode
    {
        None,
        Guide,
        Preserve,
        TargetSimilarity
    }

    public enum ProceduralTextureTrait
    {
        Structure,
        Density,
        Scale,
        Detail,
        Tone,
        Colour,
        StampShape,
        BlendOrder,
        Seam
    }

    public enum ProceduralTextureInfluenceKind
    {
        Candidate,
        Preset,
        ReferenceTexture,
        ManualLayer,
        Randomness,
        Constraint
    }

    public enum ProceduralTextureTraitInfluenceMode
    {
        Ignore,
        Explore,
        Guide,
        Preserve,
        Match
    }

    [Serializable]
    public sealed class ProceduralTextureTraitWeight
    {
        public ProceduralTextureTrait trait;
        [Range(0f, 1f)] public float weight01 = 1f;
        public ProceduralTextureTraitInfluenceMode mode = ProceduralTextureTraitInfluenceMode.Guide;
        public bool preserve;
    }

    [Serializable]
    public sealed class ProceduralTextureFeaturePreview
    {
        [NonSerialized] public Texture2D structureMap;
        [NonSerialized] public Texture2D densityMask;
        [NonSerialized] public Texture2D lowFrequencyMap;
        [NonSerialized] public Texture2D highFrequencyMap;
        [NonSerialized] public Texture2D toneStrip;
        [NonSerialized] public Texture2D toneHistogram;
        [NonSerialized] public Texture2D colourRamp;
        [NonSerialized] public Texture2D stampShapePreview;
        [NonSerialized] public Texture2D blendStackPreview;
        [NonSerialized] public Texture2D seamEdgeStrip;
        [NonSerialized] public Texture2D tilePreview2x;
    }

    [Serializable]
    public sealed class ProceduralTextureInfluenceSource
    {
        public string id;
        public string label;
        public ProceduralTextureInfluenceKind kind;
        [NonSerialized] public Texture2D rawPreview;
        public ProceduralTextureBaseSettings[] recipeOrLayers;
        [Range(0f, 1f)] public float guideStrength01 = 1f;
        [Range(0f, 1f)] public float similarityTarget01 = 0.55f;
        [Range(0, 5)] public int userRating = 3;
        public ProceduralTextureFeaturePreview featurePreview;
        public List<ProceduralTextureTraitWeight> traitWeights = new List<ProceduralTextureTraitWeight>();
    }

    [Serializable]
    public sealed class ProceduralTextureBlendLabSettings
    {
        public int sourceAIndex = -1;
        public bool sourceAIsGuide;
        public int sourceBIndex = -1;
        public bool sourceBIsGuide;
        [Range(0f, 1f)] public float structureFromB01 = 0.5f;
        [Range(0f, 1f)] public float densityFromB01 = 0.5f;
        [Range(0f, 1f)] public float scaleFromB01 = 0.5f;
        [Range(0f, 1f)] public float detailFromB01 = 0.5f;
        [Range(0f, 1f)] public float toneFromB01 = 0.5f;
        [Range(0f, 1f)] public float colourFromB01 = 0.5f;
        [Range(0f, 1f)] public float blendOrderFromB01 = 0.5f;
        [Range(0f, 1f)] public float seamFromB01 = 0.5f;
        [Range(0f, 1f)] public float randomDetail01 = 0.35f;

        public ProceduralTextureBlendLabSettings Clone()
        {
            return (ProceduralTextureBlendLabSettings)MemberwiseClone();
        }

        public void Clamp()
        {
            structureFromB01 = Mathf.Clamp01(structureFromB01);
            densityFromB01 = Mathf.Clamp01(densityFromB01);
            scaleFromB01 = Mathf.Clamp01(scaleFromB01);
            detailFromB01 = Mathf.Clamp01(detailFromB01);
            toneFromB01 = Mathf.Clamp01(toneFromB01);
            colourFromB01 = Mathf.Clamp01(colourFromB01);
            blendOrderFromB01 = Mathf.Clamp01(blendOrderFromB01);
            seamFromB01 = Mathf.Clamp01(seamFromB01);
            randomDetail01 = Mathf.Clamp01(randomDetail01);
        }
    }

    [Serializable]
    public sealed class ProceduralTextureReferenceMatchSettings
    {
        public string referenceAssetPath;
        public bool matchStructure = true;
        public bool matchDensity = true;
        public bool matchTone = true;
        public bool matchColour = false;
        public bool matchSeam = true;
        public bool useAsStampSource = false;
        [Range(0f, 1f)] public float referenceStrength01 = 0.72f;

        public ProceduralTextureReferenceMatchSettings Clone()
        {
            return (ProceduralTextureReferenceMatchSettings)MemberwiseClone();
        }

        public void Clamp()
        {
            referenceStrength01 = Mathf.Clamp01(referenceStrength01);
        }
    }

    [Serializable]
    public sealed class ProceduralTexturePresetDefinition
    {
        public string id;
        public string name;
        public ProceduralTexturePresetCategory category;
        public ProceduralTextureMapIntent intent;
        public string tags;
        public ProceduralTextureBaseSettings[] layers;

        public ProceduralTexturePresetDefinition Clone()
        {
            return new ProceduralTexturePresetDefinition
            {
                id = id,
                name = name,
                category = category,
                intent = intent,
                tags = tags,
                layers = layers != null ? ProceduralTextureCombinationUtility.CloneBases(layers) : Array.Empty<ProceduralTextureBaseSettings>()
            };
        }
    }

    [Serializable]
    public sealed class ProceduralTextureGenerationSettings
    {
        [Header("Output")]
        [Min(8)] public int width = 512;
        [Min(8)] public int height = 512;
        public TextureFormat textureFormat = TextureFormat.RGBA32;
        public bool mipChain = true;
        public bool linear = false;

        [Header("Seed")]
        public int seed = 12345;
        public bool randomizeSeedOnGenerate = false;

        [Header("Pattern")]
        public ProceduralTexturePattern pattern = ProceduralTexturePattern.PoissonDisk;
        [Min(1)] public int count = 96;
        [Range(0f, 1f)] public float minSpacing01 = 0.045f;
        [Range(0f, 1f)] public float globalJitter01 = 0.12f;
        public bool generateSeamless = false;
        public bool wrapStampsAcrossEdges = true;
        public bool toroidalSpacing = true;

        [Header("Stamp")]
        public ProceduralTextureStampShape stampShape = ProceduralTextureStampShape.Gaussian;
        [Range(0.001f, 0.5f)] public float radiusMin01 = 0.018f;
        [Range(0.001f, 0.5f)] public float radiusMax01 = 0.055f;
        [Range(0f, 2f)] public float intensityMin = 0.45f;
        [Range(0f, 2f)] public float intensityMax = 1.0f;
        [Range(0.1f, 8f)] public float contrast = 1.0f;
        [Range(0f, 1f)] public float edgeFalloff = 0.35f;
        [Range(0f, 1f)] public float ringThickness01 = 0.18f;
        public Vector2 rotationJitterDegrees = Vector2.zero;
        public ProceduralTextureBlendMode blendMode = ProceduralTextureBlendMode.Max;

        [Header("Colour")]
        public ProceduralTextureColorMode colorMode = ProceduralTextureColorMode.Grayscale;
        public Color backgroundColor = Color.black;
        public Color foregroundColor = Color.white;
        public Gradient gradient;
        [Range(0f, 1f)] public float backgroundValue = 0f;
        [Range(0f, 1f)] public float outputOpacity = 1f;

        [Header("Stratified Grid")]
        [Min(1)] public int cellsX = 12;
        [Min(1)] public int cellsY = 12;
        [Range(0f, 1f)] public float cellJitter01 = 0.65f;

        [Header("Hex Grid")]
        [Range(0.005f, 0.25f)] public float hexRadius01 = 0.05f;
        [Range(0f, 1f)] public float hexJitter01 = 0.1f;
        public bool hexPointyTop = true;

        [Header("Poisson Disk")]
        [Range(0.005f, 0.5f)] public float poissonRadius01 = 0.045f;
        [Min(1)] public int poissonAttempts = 30;
        [Min(0)] public int poissonHardCap = 0;

        [Header("Density Map")]
        public Texture2D densityMap;
        public ProceduralTextureChannel densityChannel = ProceduralTextureChannel.Luminance;
        public AnimationCurve densityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public Vector2 densityTiling = Vector2.one;
        public Vector2 densityOffset = Vector2.zero;
        public ProceduralTextureSourceSamplingMode densitySampling = ProceduralTextureSourceSamplingMode.Wrap;
        [Range(0f, 1f)] public float densityThreshold01 = 0.2f;

        [Header("Polar")]
        [Min(1)] public int polarRings = 4;
        [Min(1)] public int polarSpokesPerRing = 16;
        [Range(0.005f, 0.5f)] public float polarRingStep01 = 0.1f;
        [Range(0f, 1f)] public float polarRadialJitter01 = 0.2f;
        [Range(0f, 1f)] public float polarAngularJitter01 = 0.1f;

        [Header("Texture Source Stamp")]
        public Texture2D stampTexture;
        public ProceduralTextureChannel stampTextureChannel = ProceduralTextureChannel.Luminance;
        public bool invertStampTexture = false;
        public ProceduralTextureSourceSamplingMode stampTextureSampling = ProceduralTextureSourceSamplingMode.Wrap;
        [Range(0.05f, 8f)] public float stampTextureTiling = 1f;

        public static ProceduralTextureGenerationSettings CreateDefault()
        {
            var settings = new ProceduralTextureGenerationSettings();
            settings.gradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.black, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            };
            return settings;
        }

        public ProceduralTextureGenerationSettings Clone()
        {
            var settings = (ProceduralTextureGenerationSettings)MemberwiseClone();
            if (gradient != null)
            {
                settings.gradient = new Gradient
                {
                    colorKeys = gradient.colorKeys,
                    alphaKeys = gradient.alphaKeys,
                    mode = gradient.mode
                };
            }
            else
            {
                settings.gradient = CreateDefault().gradient;
            }

            settings.Clamp();
            return settings;
        }

        public void Clamp()
        {
            width = Mathf.Max(8, width);
            height = Mathf.Max(8, height);
            count = Mathf.Max(1, count);
            minSpacing01 = Mathf.Clamp01(minSpacing01);
            globalJitter01 = Mathf.Clamp01(globalJitter01);
            radiusMin01 = Mathf.Clamp(radiusMin01, 0.001f, 0.5f);
            radiusMax01 = Mathf.Clamp(radiusMax01, 0.001f, 0.5f);
            if (radiusMax01 < radiusMin01)
                radiusMax01 = radiusMin01;
            contrast = Mathf.Max(0.1f, contrast);
            outputOpacity = Mathf.Clamp01(outputOpacity);
            cellsX = Mathf.Max(1, cellsX);
            cellsY = Mathf.Max(1, cellsY);
            poissonAttempts = Mathf.Max(1, poissonAttempts);
            polarRings = Mathf.Max(1, polarRings);
            polarSpokesPerRing = Mathf.Max(1, polarSpokesPerRing);
            densityTiling.x = Mathf.Approximately(densityTiling.x, 0f) ? 1f : densityTiling.x;
            densityTiling.y = Mathf.Approximately(densityTiling.y, 0f) ? 1f : densityTiling.y;

            if (gradient == null)
                gradient = CreateDefault().gradient;
        }
    }

    public enum ProceduralTextureMapType
    {
        Diffuse,
        Height,
        Normal,
        Roughness,
        Smoothness,
        Metallic,
        Alpha
    }

    public enum ProceduralTexturePaletteMode
    {
        None,
        FirstTwoSwatches,
        GradientFromPalette
    }

    [Serializable]
    public sealed class ProceduralTextureBaseLockSettings
    {
        public bool seed;
        public bool pattern;
        public bool density;
        public bool stamp;
        public bool patternSpecific;
        public bool source;
        public bool blend;

        public ProceduralTextureBaseLockSettings Clone()
        {
            return (ProceduralTextureBaseLockSettings)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class ProceduralTextureBaseSettings
    {
        public string name = "Base";
        public bool enabled = true;
        [Range(0f, 1f)] public float weight = 1f;
        public ProceduralTextureBlendMode blendMode = ProceduralTextureBlendMode.Max;
        public ProceduralTextureGenerationSettings generation = ProceduralTextureGenerationSettings.CreateDefault();
        public ProceduralTextureBaseLockSettings locks = new ProceduralTextureBaseLockSettings();
        public bool useBakedValues = false;
        public int bakedWidth = 0;
        public int bakedHeight = 0;
        public float[] bakedValues;
        public ProceduralTextureGuidanceMode guidanceMode;
        [Range(0f, 1f)] public float guideStrength01 = 1f;
        [Range(0f, 1f)] public float similarityTarget01 = 0.55f;
        [Range(0, 5)] public int userRating = 3;
        public bool preserveStructure;
        public bool preserveDensity;
        public bool preserveScale;
        public bool preserveDetail;
        public bool preserveTone;
        public bool preserveColourOrPalette;
        public bool preserveStampShape;
        public bool preserveBlendOrder;
        public bool preserveSeam;

        public static ProceduralTextureBaseSettings CreateDefault(int index)
        {
            var result = new ProceduralTextureBaseSettings
            {
                name = $"Base {index + 1}",
                weight = 1f,
                blendMode = index == 0 ? ProceduralTextureBlendMode.Replace : ProceduralTextureBlendMode.Max,
                generation = ProceduralTextureGenerationSettings.CreateDefault()
            };
            result.generation.seed = 12345 + index * 7919;
            return result;
        }

        public ProceduralTextureBaseSettings Clone()
        {
            return new ProceduralTextureBaseSettings
            {
                name = name,
                enabled = enabled,
                weight = weight,
                blendMode = blendMode,
                generation = generation != null ? generation.Clone() : ProceduralTextureGenerationSettings.CreateDefault(),
                locks = locks != null ? locks.Clone() : new ProceduralTextureBaseLockSettings(),
                useBakedValues = useBakedValues,
                bakedWidth = bakedWidth,
                bakedHeight = bakedHeight,
                bakedValues = bakedValues != null ? (float[])bakedValues.Clone() : null,
                guidanceMode = guidanceMode,
                guideStrength01 = guideStrength01,
                similarityTarget01 = similarityTarget01,
                userRating = userRating,
                preserveStructure = preserveStructure,
                preserveDensity = preserveDensity,
                preserveScale = preserveScale,
                preserveDetail = preserveDetail,
                preserveTone = preserveTone,
                preserveColourOrPalette = preserveColourOrPalette,
                preserveStampShape = preserveStampShape,
                preserveBlendOrder = preserveBlendOrder,
                preserveSeam = preserveSeam
            };
        }

        public void Clamp()
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Base";
            weight = Mathf.Clamp01(weight);
            if (generation == null)
                generation = ProceduralTextureGenerationSettings.CreateDefault();
            if (locks == null)
                locks = new ProceduralTextureBaseLockSettings();
            guideStrength01 = Mathf.Clamp01(guideStrength01);
            similarityTarget01 = Mathf.Clamp01(similarityTarget01);
            userRating = Mathf.Clamp(userRating, 0, 5);
            generation.Clamp();
            if (bakedValues == null || bakedValues.Length == 0 || bakedWidth <= 0 || bakedHeight <= 0)
            {
                useBakedValues = false;
                bakedWidth = 0;
                bakedHeight = 0;
            }
        }
    }

    [Serializable]
    public sealed class ProceduralTextureCombinationSettings
    {
        [Min(8)] public int width = 512;
        [Min(8)] public int height = 512;
        public TextureFormat textureFormat = TextureFormat.RGBA32;
        public bool mipChain = true;
        public bool linear = false;
        public int candidateCount = 6;
        public bool tilePreview = false;
        public bool generateSeamless = false;
        public bool wrapStampsAcrossEdges = true;
        public bool toroidalSpacing = true;
        [Range(0f, 1f)] public float seamRepairStrength = 0.18f;
        [Range(0f, 1f)] public float edgeMatchWeight = 0.65f;
        [Range(0f, 1f)] public float seamScoreThreshold = 0.82f;
        [Range(0f, 1f)] public float constraintCoverageMin01 = 0.24f;
        [Range(0f, 1f)] public float constraintCoverageMax01 = 0.62f;
        [Range(0f, 1f)] public float constraintContrastMin01 = 0.38f;
        [Range(0f, 1f)] public float constraintContrastMax01 = 0.95f;
        [Range(0f, 1f)] public float constraintScaleMin01 = 0.06f;
        [Range(0f, 1f)] public float constraintScaleMax01 = 0.42f;
        public ProceduralTextureConstraintSortMode constraintSortMode = ProceduralTextureConstraintSortMode.Overall;
        [Range(0f, 1f)] public float randomness01 = 1f;
        [Range(0f, 1f)] public float lockedInfluence01 = 0f;
        [Range(0f, 1f)] public float generationPatternVariety01 = 1f;
        [Range(1, 512)] public int generationDensityMin = 32;
        [Range(1, 512)] public int generationDensityMax = 260;
        [Range(0.001f, 0.5f)] public float generationRadiusMin01 = 0.003f;
        [Range(0.001f, 0.5f)] public float generationRadiusMax01 = 0.14f;
        [Range(0f, 2f)] public float generationIntensityMin = 0.08f;
        [Range(0f, 2f)] public float generationIntensityMax = 1.38f;
        [Range(0.1f, 8f)] public float generationContrastMin = 1f;
        [Range(0.1f, 8f)] public float generationContrastMax = 4.2f;
        public ProceduralTextureColorMode colorMode = ProceduralTextureColorMode.Grayscale;
        public ProceduralTexturePaletteMode paletteMode = ProceduralTexturePaletteMode.None;
        public Color backgroundColor = Color.black;
        public Color foregroundColor = Color.white;
        public Gradient gradient = ProceduralTextureGenerationSettings.CreateDefault().gradient;
        [Range(0f, 1f)] public float outputOpacity = 1f;

        [Header("Refine")]
        [Range(0f, 1f)] public float inputMin = 0f;
        [Range(0f, 1f)] public float inputMax = 1f;
        [Range(0.1f, 4f)] public float gamma = 1f;
        [Range(0f, 2f)] public float contrast = 1.15f;
        [Range(-1f, 1f)] public float brightness = 0f;
        public bool autoBalance = true;
        [Range(0f, 1f)] public float autoBalanceStrength = 1f;
        [Range(0f, 1f)] public float threshold = 0f;
        public bool invert = false;
        [Range(0f, 1f)] public float blur = 0f;
        [Range(0f, 1f)] public float sharpen = 0f;

        public void Clamp()
        {
            width = Mathf.Max(8, width);
            height = Mathf.Max(8, height);
            candidateCount = Mathf.Clamp(candidateCount, 1, 16);
            seamRepairStrength = Mathf.Clamp01(seamRepairStrength);
            edgeMatchWeight = Mathf.Clamp01(edgeMatchWeight);
            seamScoreThreshold = Mathf.Clamp01(seamScoreThreshold);
            constraintCoverageMin01 = Mathf.Clamp01(constraintCoverageMin01);
            constraintCoverageMax01 = Mathf.Clamp01(constraintCoverageMax01);
            if (constraintCoverageMax01 < constraintCoverageMin01)
                constraintCoverageMax01 = constraintCoverageMin01;
            constraintContrastMin01 = Mathf.Clamp01(constraintContrastMin01);
            constraintContrastMax01 = Mathf.Clamp01(constraintContrastMax01);
            if (constraintContrastMax01 < constraintContrastMin01)
                constraintContrastMax01 = constraintContrastMin01;
            constraintScaleMin01 = Mathf.Clamp01(constraintScaleMin01);
            constraintScaleMax01 = Mathf.Clamp01(constraintScaleMax01);
            if (constraintScaleMax01 < constraintScaleMin01)
                constraintScaleMax01 = constraintScaleMin01;
            randomness01 = Mathf.Clamp01(randomness01);
            lockedInfluence01 = Mathf.Clamp01(lockedInfluence01);
            generationPatternVariety01 = Mathf.Clamp01(generationPatternVariety01);
            generationDensityMin = Mathf.Clamp(generationDensityMin, 1, 512);
            generationDensityMax = Mathf.Clamp(generationDensityMax, 1, 512);
            if (generationDensityMax < generationDensityMin)
                generationDensityMax = generationDensityMin;
            generationRadiusMin01 = Mathf.Clamp(generationRadiusMin01, 0.001f, 0.5f);
            generationRadiusMax01 = Mathf.Clamp(generationRadiusMax01, 0.001f, 0.5f);
            if (generationRadiusMax01 < generationRadiusMin01)
                generationRadiusMax01 = generationRadiusMin01;
            generationIntensityMin = Mathf.Clamp(generationIntensityMin, 0f, 2f);
            generationIntensityMax = Mathf.Clamp(generationIntensityMax, 0f, 2f);
            if (generationIntensityMax < generationIntensityMin)
                generationIntensityMax = generationIntensityMin;
            generationContrastMin = Mathf.Clamp(generationContrastMin, 0.1f, 8f);
            generationContrastMax = Mathf.Clamp(generationContrastMax, 0.1f, 8f);
            if (generationContrastMax < generationContrastMin)
                generationContrastMax = generationContrastMin;
            outputOpacity = Mathf.Clamp01(outputOpacity);
            inputMin = Mathf.Clamp01(inputMin);
            inputMax = Mathf.Clamp01(inputMax);
            if (inputMax < inputMin)
                inputMax = inputMin;
            gamma = Mathf.Clamp(gamma, 0.1f, 4f);
            contrast = Mathf.Clamp(contrast, 0f, 2f);
            brightness = Mathf.Clamp(brightness, -1f, 1f);
            autoBalanceStrength = Mathf.Clamp01(autoBalanceStrength);
            threshold = Mathf.Clamp01(threshold);
            blur = Mathf.Clamp01(blur);
            sharpen = Mathf.Clamp01(sharpen);
            if (gradient == null)
                gradient = ProceduralTextureGenerationSettings.CreateDefault().gradient;
        }

        public ProceduralTextureCombinationSettings Clone()
        {
            var settings = (ProceduralTextureCombinationSettings)MemberwiseClone();
            if (gradient != null)
            {
                settings.gradient = new Gradient
                {
                    colorKeys = gradient.colorKeys,
                    alphaKeys = gradient.alphaKeys,
                    mode = gradient.mode
                };
            }
            settings.Clamp();
            return settings;
        }
    }

    [Serializable]
    public sealed class ProceduralTextureMapExportSettings
    {
        public bool diffuse = true;
        public bool height = true;
        public bool normal = false;
        public bool roughness = false;
        public bool smoothness = false;
        public bool metallic = false;
        public bool alpha = false;
        public bool transparentPng = false;
        [Range(0f, 1f)] public float alphaMin = 0f;
        [Range(0f, 1f)] public float alphaMax = 0.08f;
        [Range(0.1f, 8f)] public float normalStrength = 2f;

        public void Clamp()
        {
            alphaMin = Mathf.Clamp01(alphaMin);
            alphaMax = Mathf.Clamp01(alphaMax);
            if (alphaMax < alphaMin)
                alphaMax = alphaMin;
            normalStrength = Mathf.Clamp(normalStrength, 0.1f, 8f);
        }
    }

    [Serializable]
    public sealed class ProceduralTextureCandidate
    {
        public string label;
        public int seedOffset;
        public bool locked;
        public bool selected;
        public bool isActive;
        public ProceduralTextureGuidanceMode guidanceMode;
        public int generationDepth;
        [Range(0f, 1f)] public float seamScore01 = 1f;
        [Range(0f, 1f)] public float coverage01 = 0.5f;
        [Range(0f, 1f)] public float contrast01 = 0.5f;
        [Range(0f, 1f)] public float scale01 = 0.5f;
        [Range(0f, 1f)] public float constraintScore01 = 0.5f;
        [Range(0f, 1f)] public float influenceWeight = 1f;
        [Range(0f, 1f)] public float guideStrength01 = 1f;
        [Range(0f, 1f)] public float similarityTarget01 = 0.55f;
        [Range(0, 5)] public int userRating = 3;
        public bool preserveStructure;
        public bool preserveDensity;
        public bool preserveScale;
        public bool preserveDetail;
        public bool preserveTone;
        public bool preserveColourOrPalette;
        public bool preserveStampShape;
        public bool preserveBlendOrder;
        public bool preserveSeam;
        public int[] contributorIndices;
        public int valuesWidth;
        public int valuesHeight;
        [NonSerialized]
        public float[] values;
        [NonSerialized]
        public Color[] pixels;
        [NonSerialized]
        public Texture2D preview;
        [NonSerialized]
        public Texture2D seamHeatmap;
        [NonSerialized]
        public float[] fullValues;
        [NonSerialized]
        public Color[] fullPixels;
        public ProceduralTextureCombinationSettings settings;
        public ProceduralTextureBaseSettings[] bases;
        public string createdUtc;

        public ProceduralTextureEditSourceKind EditSourceKind
        {
            get
            {
                if (bases != null && bases.Length > 0)
                    return ProceduralTextureEditSourceKind.Recipe;
                if (values != null && values.Length > 0)
                    return ProceduralTextureEditSourceKind.BakedValues;
                return ProceduralTextureEditSourceKind.Unknown;
            }
        }
    }

}
