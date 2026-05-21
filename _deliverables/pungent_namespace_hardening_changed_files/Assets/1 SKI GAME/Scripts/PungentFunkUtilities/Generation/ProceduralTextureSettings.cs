using System;
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
        Replace
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

}