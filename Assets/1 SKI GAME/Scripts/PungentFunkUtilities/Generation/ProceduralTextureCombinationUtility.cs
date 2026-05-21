using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PungentFunk.Utilities.Generation
{
    public static class ProceduralTextureCombinationUtility
    {
        public const int MinBaseCount = 1;
        public const int MaxBaseCount = 16;
        public const int SoftBaseWarningCount = 8;

        public static float[] GenerateCombinedValues(IList<ProceduralTextureBaseSettings> bases, ProceduralTextureCombinationSettings settings, int seedOffset = 0)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.Clamp();
            int length = settings.width * settings.height;
            float[] combined = new float[length];
            bool hasAny = false;

            if (bases != null)
            {
                int count = Mathf.Min(bases.Count, MaxBaseCount);
                for (int i = 0; i < count; i++)
                {
                    ProceduralTextureBaseSettings textureBase = bases[i];
                    if (textureBase == null || !textureBase.enabled)
                        continue;

                    textureBase.Clamp();
                    float[] values = textureBase.useBakedValues
                        ? ResampleBakedValues(textureBase, settings.width, settings.height)
                        : GenerateBaseValues(textureBase, settings, seedOffset, i);
                    float weight = Mathf.Clamp01(textureBase.weight);
                    if (weight <= 0.0001f)
                        continue;

                    for (int p = 0; p < length; p++)
                    {
                        float layerValue = Mathf.Clamp01(values[p]);
                        float blended = hasAny ? BlendCombined(combined[p], layerValue, textureBase.blendMode) : layerValue;
                        combined[p] = hasAny
                            ? Mathf.Lerp(combined[p], blended, weight)
                            : Mathf.Lerp(0f, blended, weight);
                    }

                    hasAny = true;
                }
            }

            if (!hasAny)
                Fill(combined, 0f);

            ApplyRefine(combined, settings);
            if (settings.generateSeamless && settings.seamRepairStrength > 0f)
                RepairSeams(combined, settings.width, settings.height, settings.seamRepairStrength, settings.edgeMatchWeight);
            return combined;
        }

        public static Texture2D GenerateTexture(IList<ProceduralTextureBaseSettings> bases, ProceduralTextureCombinationSettings settings, Color[] paletteColors = null, int seedOffset = 0)
        {
            float[] values = GenerateCombinedValues(bases, settings, seedOffset);
            Color[] pixels = ValuesToPixels(values, settings, paletteColors);
            return CreateTexture(settings.width, settings.height, settings.textureFormat, settings.mipChain, settings.linear, pixels, "Generated Procedural Texture");
        }

        public static Color[] ValuesToPixels(float[] values, ProceduralTextureCombinationSettings settings, Color[] paletteColors = null)
        {
            if (values == null)
                return Array.Empty<Color>();
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.Clamp();
            Color[] pixels = new Color[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                Color color = EvaluateColor(values[i], settings, paletteColors);
                color.a *= settings.outputOpacity;
                pixels[i] = color;
            }
            return pixels;
        }

        public static Texture2D CreateTexture(int width, int height, TextureFormat format, bool mipChain, bool linear, Color[] pixels, string name)
        {
            var texture = new Texture2D(width, height, format, mipChain, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels);
            texture.Apply(mipChain, false);
            return texture;
        }

        public static Texture2D CreatePreviewTexture(int sourceWidth, int sourceHeight, Color[] pixels, int maxSize, string name)
        {
            if (pixels == null || pixels.Length == 0)
                return CreateTexture(1, 1, TextureFormat.RGBA32, false, true, new[] { Color.clear }, name);

            maxSize = Mathf.Clamp(maxSize, 32, 512);
            int largest = Mathf.Max(1, Mathf.Max(sourceWidth, sourceHeight));
            if (largest <= maxSize)
                return CreateTexture(sourceWidth, sourceHeight, TextureFormat.RGBA32, false, true, pixels, name);

            float scale = maxSize / (float)largest;
            int width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
            Color[] previewPixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) / height * sourceHeight - 0.5f), 0, sourceHeight - 1);
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) / width * sourceWidth - 0.5f), 0, sourceWidth - 1);
                    previewPixels[y * width + x] = pixels[sourceY * sourceWidth + sourceX];
                }
            }

            return CreateTexture(width, height, TextureFormat.RGBA32, false, true, previewPixels, name);
        }

        public static Texture2D GenerateMapTexture(float[] values, ProceduralTextureCombinationSettings settings, ProceduralTextureMapExportSettings exportSettings, ProceduralTextureMapType mapType, Color[] diffusePixels = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (exportSettings == null)
                exportSettings = new ProceduralTextureMapExportSettings();

            settings.Clamp();
            exportSettings.Clamp();
            Color[] pixels = BuildMapPixels(values, settings.width, settings.height, exportSettings, mapType, diffusePixels);
            return CreateTexture(settings.width, settings.height, TextureFormat.RGBA32, settings.mipChain, mapType != ProceduralTextureMapType.Diffuse, pixels, $"Generated {mapType} Map");
        }

        public static Color[] BuildMapPixels(float[] values, int width, int height, ProceduralTextureMapExportSettings exportSettings, ProceduralTextureMapType mapType, Color[] diffusePixels = null)
        {
            if (values == null)
                return Array.Empty<Color>();

            if (exportSettings == null)
                exportSettings = new ProceduralTextureMapExportSettings();
            exportSettings.Clamp();
            Color[] pixels = new Color[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float v = Mathf.Clamp01(values[i]);
                switch (mapType)
                {
                    case ProceduralTextureMapType.Diffuse:
                        Color diffuse = diffusePixels != null && i < diffusePixels.Length ? diffusePixels[i] : new Color(v, v, v, 1f);
                        if (exportSettings.transparentPng && v >= exportSettings.alphaMin && v <= exportSettings.alphaMax)
                            diffuse.a = 0f;
                        pixels[i] = diffuse;
                        break;
                    case ProceduralTextureMapType.Normal:
                        pixels[i] = NormalFromHeight(values, width, height, i, exportSettings.normalStrength);
                        break;
                    case ProceduralTextureMapType.Roughness:
                        pixels[i] = new Color(1f - v, 1f - v, 1f - v, 1f);
                        break;
                    case ProceduralTextureMapType.Smoothness:
                    case ProceduralTextureMapType.Metallic:
                    case ProceduralTextureMapType.Height:
                    case ProceduralTextureMapType.Alpha:
                    default:
                        pixels[i] = new Color(v, v, v, 1f);
                        break;
                }
            }
            return pixels;
        }

        public static ProceduralTextureBaseSettings[] CloneBases(IList<ProceduralTextureBaseSettings> bases)
        {
            if (bases == null)
                return Array.Empty<ProceduralTextureBaseSettings>();

            int count = Mathf.Min(bases.Count, MaxBaseCount);
            var result = new ProceduralTextureBaseSettings[count];
            for (int i = 0; i < count; i++)
                result[i] = bases[i] != null ? bases[i].Clone() : ProceduralTextureBaseSettings.CreateDefault(i);
            return result;
        }

        private static float[] GenerateBaseValues(ProceduralTextureBaseSettings textureBase, ProceduralTextureCombinationSettings settings, int seedOffset, int index)
        {
            ProceduralTextureGenerationSettings generation = textureBase.generation.Clone();
            generation.width = settings.width;
            generation.height = settings.height;
            generation.textureFormat = settings.textureFormat;
            generation.mipChain = settings.mipChain;
            generation.linear = settings.linear;
            generation.seed = HashSeed(generation.seed + seedOffset, index);
            generation.randomizeSeedOnGenerate = false;
            generation.generateSeamless = settings.generateSeamless;
            generation.wrapStampsAcrossEdges = settings.wrapStampsAcrossEdges;
            generation.toroidalSpacing = settings.toroidalSpacing;
            return ProceduralTextureGenerator.GenerateValues(generation);
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

        private static float[] ResampleBakedValues(ProceduralTextureBaseSettings textureBase, int width, int height)
        {
            int length = width * height;
            float[] result = new float[length];
            if (textureBase.bakedValues == null || textureBase.bakedValues.Length == 0 || textureBase.bakedWidth <= 0 || textureBase.bakedHeight <= 0)
                return result;

            for (int y = 0; y < height; y++)
            {
                float v = height <= 1 ? 0f : y / (float)(height - 1);
                int sourceY = Mathf.Clamp(Mathf.RoundToInt(v * (textureBase.bakedHeight - 1)), 0, textureBase.bakedHeight - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : x / (float)(width - 1);
                    int sourceX = Mathf.Clamp(Mathf.RoundToInt(u * (textureBase.bakedWidth - 1)), 0, textureBase.bakedWidth - 1);
                    int sourceIndex = sourceY * textureBase.bakedWidth + sourceX;
                    result[y * width + x] = sourceIndex >= 0 && sourceIndex < textureBase.bakedValues.Length
                        ? Mathf.Clamp01(textureBase.bakedValues[sourceIndex])
                        : 0f;
                }
            }
            return result;
        }

        public static float MeasureSeamScore(float[] values, int width, int height)
        {
            if (values == null || values.Length == 0 || width <= 1 || height <= 1)
                return 1f;

            float total = 0f;
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                total += Mathf.Abs(SampleSeam(values, width, height, 0, y) - SampleSeam(values, width, height, width - 1, y));
                count++;
            }

            for (int x = 0; x < width; x++)
            {
                total += Mathf.Abs(SampleSeam(values, width, height, x, 0) - SampleSeam(values, width, height, x, height - 1));
                count++;
            }

            return Mathf.Clamp01(1f - total / Mathf.Max(1, count));
        }

        public static Color[] CreateSeamHeatmapPixels(float[] values, int width, int height)
        {
            if (values == null || values.Length == 0 || width <= 0 || height <= 0)
                return Array.Empty<Color>();

            var pixels = new Color[width * height];
            int band = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(width, height) * 0.045f), 2, Mathf.Max(2, Mathf.Min(width, height) / 5));
            for (int y = 0; y < height; y++)
            {
                float horizontalError = Mathf.Abs(SampleSeam(values, width, height, 0, y) - SampleSeam(values, width, height, width - 1, y));
                for (int i = 0; i < band; i++)
                {
                    float alpha = Mathf.Clamp01(horizontalError * 3.4f) * (1f - i / (float)band);
                    Color color = SeamHeatColor(horizontalError, alpha);
                    BlendHeat(pixels, y * width + i, color);
                    BlendHeat(pixels, y * width + (width - 1 - i), color);
                }
            }

            for (int x = 0; x < width; x++)
            {
                float verticalError = Mathf.Abs(SampleSeam(values, width, height, x, 0) - SampleSeam(values, width, height, x, height - 1));
                for (int i = 0; i < band; i++)
                {
                    float alpha = Mathf.Clamp01(verticalError * 3.4f) * (1f - i / (float)band);
                    Color color = SeamHeatColor(verticalError, alpha);
                    BlendHeat(pixels, i * width + x, color);
                    BlendHeat(pixels, (height - 1 - i) * width + x, color);
                }
            }

            return pixels;
        }

        private static Color SeamHeatColor(float error, float alpha)
        {
            Color low = new Color(1f, 0.78f, 0.10f, 0f);
            Color high = new Color(1f, 0.12f, 0.06f, 0f);
            Color color = Color.Lerp(low, high, Mathf.Clamp01(error * 3f));
            color.a = Mathf.Clamp01(alpha * 0.78f);
            return color;
        }

        private static void BlendHeat(Color[] pixels, int index, Color color)
        {
            if (index < 0 || index >= pixels.Length || color.a <= pixels[index].a)
                return;
            pixels[index] = color;
        }

        private static void RepairSeams(float[] values, int width, int height, float strength, float edgeMatchWeight)
        {
            if (values == null || values.Length != width * height || width <= 1 || height <= 1)
                return;

            strength = Mathf.Clamp01(strength);
            edgeMatchWeight = Mathf.Clamp01(edgeMatchWeight);
            if (strength <= 0f || edgeMatchWeight <= 0f)
                return;

            int band = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(width, height) * Mathf.Lerp(0.012f, 0.055f, strength)), 1, Mathf.Max(1, Mathf.Min(width, height) / 4));
            for (int y = 0; y < height; y++)
            {
                for (int i = 0; i < band; i++)
                {
                    float t = (1f - i / (float)band) * strength * edgeMatchWeight;
                    int left = y * width + i;
                    int right = y * width + (width - 1 - i);
                    float average = (values[left] + values[right]) * 0.5f;
                    values[left] = Mathf.Lerp(values[left], average, t);
                    values[right] = Mathf.Lerp(values[right], average, t);
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int i = 0; i < band; i++)
                {
                    float t = (1f - i / (float)band) * strength * edgeMatchWeight;
                    int bottom = i * width + x;
                    int top = (height - 1 - i) * width + x;
                    float average = (values[bottom] + values[top]) * 0.5f;
                    values[bottom] = Mathf.Lerp(values[bottom], average, t);
                    values[top] = Mathf.Lerp(values[top], average, t);
                }
            }
        }

        private static float SampleSeam(float[] values, int width, int height, int x, int y)
        {
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            int index = y * width + x;
            return index >= 0 && index < values.Length ? Mathf.Clamp01(values[index]) : 0f;
        }

        private static Color EvaluateColor(float value, ProceduralTextureCombinationSettings settings, Color[] paletteColors)
        {
            value = Mathf.Clamp01(value);
            if (settings.paletteMode != ProceduralTexturePaletteMode.None && paletteColors != null && paletteColors.Length > 0)
            {
                if (settings.paletteMode == ProceduralTexturePaletteMode.FirstTwoSwatches)
                {
                    Color a = paletteColors[0];
                    Color b = paletteColors.Length > 1 ? paletteColors[1] : Color.white;
                    return Color.Lerp(a, b, value);
                }

                if (paletteColors.Length == 1)
                    return Color.Lerp(Color.black, paletteColors[0], value);

                float scaled = value * (paletteColors.Length - 1);
                int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, paletteColors.Length - 2);
                return Color.Lerp(paletteColors[index], paletteColors[index + 1], scaled - index);
            }

            return settings.colorMode switch
            {
                ProceduralTextureColorMode.ForegroundBackground => Color.Lerp(settings.backgroundColor, settings.foregroundColor, value),
                ProceduralTextureColorMode.Gradient => settings.gradient != null ? settings.gradient.Evaluate(value) : Color.Lerp(Color.black, Color.white, value),
                _ => new Color(value, value, value, 1f)
            };
        }

        private static void ApplyRefine(float[] values, ProceduralTextureCombinationSettings settings)
        {
            if (settings.autoBalance && settings.autoBalanceStrength > 0.001f)
                AutoBalance(values, settings.autoBalanceStrength);

            if (settings.blur > 0.001f)
                BoxBlur(values, settings.width, settings.height, Mathf.RoundToInt(Mathf.Lerp(1f, 3f, settings.blur)));

            float inputRange = Mathf.Max(0.0001f, settings.inputMax - settings.inputMin);
            for (int i = 0; i < values.Length; i++)
            {
                float v = Mathf.Clamp01((values[i] - settings.inputMin) / inputRange);
                v = Mathf.Pow(v, 1f / Mathf.Max(0.1f, settings.gamma));
                v = Mathf.Clamp01((v - 0.5f) * settings.contrast + 0.5f + settings.brightness);
                if (settings.threshold > 0f)
                    v = v >= settings.threshold ? 1f : 0f;
                if (settings.invert)
                    v = 1f - v;
                values[i] = v;
            }

            if (settings.sharpen > 0.001f)
                Sharpen(values, settings.width, settings.height, settings.sharpen);
        }

        private static float BlendCombined(float current, float contribution, ProceduralTextureBlendMode blendMode)
        {
            return blendMode switch
            {
                ProceduralTextureBlendMode.Add => Mathf.Clamp01(current + contribution),
                ProceduralTextureBlendMode.Multiply => Mathf.Clamp01(current * contribution),
                ProceduralTextureBlendMode.Replace => contribution,
                ProceduralTextureBlendMode.Subtract => Mathf.Clamp01(current - contribution),
                ProceduralTextureBlendMode.Overlay => current < 0.5f
                    ? Mathf.Clamp01(2f * current * contribution)
                    : Mathf.Clamp01(1f - 2f * (1f - current) * (1f - contribution)),
                _ => Mathf.Max(current, contribution)
            };
        }

        private static Color NormalFromHeight(float[] values, int width, int height, int index, float strength)
        {
            int x = index % width;
            int y = index / width;
            float left = Sample(values, width, height, x - 1, y);
            float right = Sample(values, width, height, x + 1, y);
            float down = Sample(values, width, height, x, y - 1);
            float up = Sample(values, width, height, x, y + 1);
            Vector3 normal = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
            return new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
        }

        private static float Sample(float[] values, int width, int height, int x, int y)
        {
            x = (x % width + width) % width;
            y = (y % height + height) % height;
            return values[y * width + x];
        }

        private static void BoxBlur(float[] values, int width, int height, int radius)
        {
            radius = Mathf.Clamp(radius, 1, 4);
            float[] source = (float[])values.Clone();
            float[] horizontal = new float[values.Length];
            int diameter = radius * 2 + 1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float total = 0f;
                    for (int ox = -radius; ox <= radius; ox++)
                        total += Sample(source, width, height, x + ox, y);
                    horizontal[y * width + x] = total / diameter;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float total = 0f;
                    for (int oy = -radius; oy <= radius; oy++)
                        total += Sample(horizontal, width, height, x, y + oy);
                    values[y * width + x] = total / diameter;
                }
            }
        }

        private static void AutoBalance(float[] values, float strength)
        {
            if (values == null || values.Length == 0)
                return;

            ProceduralTextureToneStats stats = MeasureToneStats(values);
            float min = stats.lowPercentile;
            float max = stats.highPercentile;

            float range = max - min;
            if (range < 0.035f)
            {
                min = stats.min;
                max = stats.max;
                range = max - min;
            }
            if (range < 0.0001f)
                return;

            float inverseRange = 1f / range;
            strength = Mathf.Clamp01(strength);
            for (int i = 0; i < values.Length; i++)
            {
                float balanced = Mathf.Clamp01((values[i] - min) * inverseRange);
                values[i] = Mathf.Lerp(values[i], balanced, strength);
            }
        }

        public static ProceduralTextureToneStats MeasureToneStats(float[] values)
        {
            var stats = new ProceduralTextureToneStats();
            if (values == null || values.Length == 0)
                return stats;

            float[] sorted = (float[])values.Clone();
            Array.Sort(sorted);
            stats.min = sorted[0];
            stats.max = sorted[sorted.Length - 1];
            stats.lowPercentile = sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * 0.02f), 0, sorted.Length - 1)];
            stats.highPercentile = sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * 0.98f), 0, sorted.Length - 1)];

            int dark = 0;
            int mid = 0;
            int light = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                if (value <= 0.18f)
                    dark++;
                if (value >= 0.35f && value <= 0.65f)
                    mid++;
                if (value >= 0.82f)
                    light++;
            }

            float inverseLength = 1f / values.Length;
            stats.darkCoverage = dark * inverseLength;
            stats.midCoverage = mid * inverseLength;
            stats.lightCoverage = light * inverseLength;
            stats.range = stats.max - stats.min;
            stats.percentileRange = stats.highPercentile - stats.lowPercentile;
            return stats;
        }

        private static void Sharpen(float[] values, int width, int height, float amount)
        {
            float[] blurred = (float[])values.Clone();
            BoxBlur(blurred, width, height, 1);
            for (int i = 0; i < values.Length; i++)
                values[i] = Mathf.Clamp01(values[i] + (values[i] - blurred[i]) * amount);
        }

        private static void Fill(float[] values, float value)
        {
            value = Mathf.Clamp01(value);
            for (int i = 0; i < values.Length; i++)
                values[i] = value;
        }

        public static void DestroyGeneratedTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);
        }
    }

    public enum ProceduralTextureGenerationResolution
    {
        Preview,
        Full
    }

    public struct ProceduralTextureToneStats
    {
        public float min;
        public float max;
        public float lowPercentile;
        public float highPercentile;
        public float range;
        public float percentileRange;
        public float darkCoverage;
        public float midCoverage;
        public float lightCoverage;
    }

    public static class ProceduralTextureRecipeRandomizer
    {
        private struct InfluenceRecipeProfile
        {
            public float guideStrength01;
            public float similarity01;
            public float mutation01;
            public float adherence01;
            public float coverage01;
            public float contrast01;
            public float averageRadius01;
            public float density01;
            public float detail01;
            public bool preserveStructure;
            public bool preserveDensity;
            public bool preserveScale;
            public bool preserveDetail;
            public bool preserveTone;
            public bool preserveColourOrPalette;
            public bool preserveStampShape;
            public bool preserveBlendOrder;
            public bool preserveSeam;
        }

        public static ProceduralTextureBaseSettings[] CreateFreshVariantRecipe(int seed, ProceduralTextureCombinationSettings outputSettings)
        {
            var rng = new System.Random(seed);
            int layerCount = rng.Next(1, 4);
            var layers = new ProceduralTextureBaseSettings[layerCount];
            for (int i = 0; i < layerCount; i++)
                layers[i] = CreateRandomLayer(rng, i, false, outputSettings);
            return layers;
        }

        public static ProceduralTextureBaseSettings[] CreateInfluencedVariantRecipe(int seed, IList<ProceduralTextureBaseSettings> lockedSources, ProceduralTextureCombinationSettings outputSettings)
        {
            var rng = new System.Random(seed);
            InfluenceRecipeProfile profile = BuildInfluenceProfile(lockedSources, outputSettings);
            var layers = new List<ProceduralTextureBaseSettings>(MaxInfluenceCount(lockedSources) + 3);

            if (lockedSources != null)
            {
                int influenceCount = Mathf.Min(lockedSources.Count, ProceduralTextureCombinationUtility.MaxBaseCount - 1);
                for (int i = 0; i < influenceCount; i++)
                {
                    if (lockedSources[i] == null)
                        continue;

                    ProceduralTextureBaseSettings clone = lockedSources[i].Clone();
                    clone.name = $"Guide {layers.Count + 1}";
                    float influence = outputSettings != null ? Mathf.Clamp01(outputSettings.lockedInfluence01) : 1f;
                    float sourceStrength = Mathf.Clamp01(clone.guideStrength01 <= 0f ? 1f : clone.guideStrength01);
                    float sourceSimilarity = Mathf.Clamp01(clone.similarityTarget01 <= 0f ? profile.similarity01 : clone.similarityTarget01);
                    float sourceRating = Mathf.Clamp01((clone.userRating <= 0 ? 3 : clone.userRating) / 5f);
                    float parentWeight = sourceStrength * Mathf.Lerp(0.45f, 1.15f, sourceSimilarity) * Mathf.Lerp(0.75f, 1.15f, sourceRating);
                    clone.weight = Mathf.Clamp(clone.weight * influence * parentWeight * Range(rng, 0.72f, 1.08f), 0.02f, 1f);
                    clone.blendMode = layers.Count == 0 || clone.preserveBlendOrder ? ProceduralTextureBlendMode.Replace : PickInfluenceBlend(rng, profile);
                    layers.Add(clone);
                }
            }

            float randomness = outputSettings != null ? Mathf.Clamp01(outputSettings.randomness01) : 1f;
            float similarity = Mathf.Clamp01(profile.similarity01);
            int maxDetails = profile.preserveDetail && similarity > 0.55f ? 2 : 3;
            int detailLayers = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, maxDetails, randomness)), 1, maxDetails);
            while (detailLayers-- > 0 && layers.Count < ProceduralTextureCombinationUtility.MaxBaseCount)
                layers.Add(CreateGuidedRandomLayer(rng, layers.Count, outputSettings, profile));

            return layers.Count > 0 ? layers.ToArray() : CreateFreshVariantRecipe(seed, outputSettings);
        }

        public static void RandomizeUnlockedGroups(ProceduralTextureBaseSettings recipe, ProceduralTextureBaseLockSettings locks, System.Random rng)
        {
            if (recipe == null || rng == null)
                return;

            locks = locks ?? new ProceduralTextureBaseLockSettings();
            ProceduralTextureGenerationSettings settings = recipe.generation ?? ProceduralTextureGenerationSettings.CreateDefault();
            recipe.generation = settings;
            if (!locks.seed)
                settings.seed = rng.Next(1, int.MaxValue);
            if (!locks.pattern)
                settings.pattern = PickPattern(rng, 1f, settings.generateSeamless);
            if (!locks.density)
            {
                settings.count = rng.Next(28, 320);
                settings.minSpacing01 = Range(rng, 0.002f, 0.075f);
                settings.globalJitter01 = Range(rng, 0.02f, 0.9f);
            }
            if (!locks.stamp)
                RandomizeStamp(settings, rng, false, new ProceduralTextureCombinationSettings());
            if (!locks.blend)
            {
                recipe.weight = Range(rng, 0.22f, 1f);
                recipe.blendMode = PickBlend(rng);
            }
            recipe.Clamp();
        }

        public static int EstimatePreviewCost(ProceduralTextureBaseSettings[] recipe, int previewSize)
        {
            int layers = recipe != null ? recipe.Length : 1;
            int sites = 0;
            if (recipe != null)
            {
                for (int i = 0; i < recipe.Length; i++)
                    sites += recipe[i]?.generation != null ? recipe[i].generation.count : 64;
            }
            return Mathf.Max(1, layers * Mathf.Max(1, sites) * Mathf.Max(1, previewSize / 64));
        }

        public static int HashSeed(int seed, int index)
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

        private static ProceduralTextureBaseSettings CreateRandomLayer(System.Random rng, int index, bool detail, ProceduralTextureCombinationSettings outputSettings)
        {
            outputSettings = outputSettings ?? new ProceduralTextureCombinationSettings();
            outputSettings.Clamp();
            ProceduralTextureBaseSettings layer = ProceduralTextureBaseSettings.CreateDefault(index);
            layer.name = detail ? $"Detail {index + 1}" : $"Layer {index + 1}";
            layer.weight = detail ? Range(rng, 0.16f, 0.54f) : Range(rng, 0.62f, 1f);
            layer.blendMode = index == 0 ? ProceduralTextureBlendMode.Replace : PickBlend(rng);

            ProceduralTextureGenerationSettings settings = layer.generation;
            settings.seed = rng.Next(1, int.MaxValue);
            settings.generateSeamless = outputSettings.generateSeamless;
            settings.wrapStampsAcrossEdges = outputSettings.wrapStampsAcrossEdges;
            settings.toroidalSpacing = outputSettings.toroidalSpacing;
            settings.pattern = detail ? PickDetailPattern(rng, outputSettings.generationPatternVariety01, outputSettings.generateSeamless) : PickPattern(rng, outputSettings.generationPatternVariety01, outputSettings.generateSeamless);
            int densityMin = Mathf.Clamp(outputSettings.generationDensityMin, 1, 512);
            int densityMax = Mathf.Clamp(outputSettings.generationDensityMax, densityMin, 512);
            settings.count = rng.Next(densityMin, densityMax + 1);
            settings.minSpacing01 = detail ? Range(rng, 0.002f, 0.035f) : Range(rng, 0.008f, 0.09f);
            settings.globalJitter01 = Range(rng, 0.04f, 0.85f);
            settings.backgroundValue = 0f;
            RandomizeStamp(settings, rng, detail, outputSettings);
            RandomizePatternSpecific(settings, rng);
            layer.Clamp();
            return layer;
        }

        private static ProceduralTextureBaseSettings CreateGuidedRandomLayer(System.Random rng, int index, ProceduralTextureCombinationSettings outputSettings, InfluenceRecipeProfile profile)
        {
            outputSettings = outputSettings ?? new ProceduralTextureCombinationSettings();
            outputSettings.Clamp();
            float similarity = Mathf.Clamp01(profile.similarity01 * profile.adherence01);
            float freedom = Mathf.Clamp01(profile.mutation01 * (1f - similarity * 0.82f));

            ProceduralTextureBaseSettings layer = ProceduralTextureBaseSettings.CreateDefault(index);
            layer.name = $"Guided Detail {index + 1}";
            layer.weight = Range(rng, Mathf.Lerp(0.10f, 0.28f, freedom), Mathf.Lerp(0.30f, 0.72f, freedom));
            layer.blendMode = profile.preserveBlendOrder && similarity > 0.6f ? ProceduralTextureBlendMode.Overlay : PickBlend(rng);
            if (profile.preserveDetail && similarity > 0.55f)
                layer.weight *= Mathf.Lerp(0.82f, 1.06f, profile.detail01);

            ProceduralTextureGenerationSettings settings = layer.generation;
            settings.seed = rng.Next(1, int.MaxValue);
            settings.generateSeamless = outputSettings.generateSeamless;
            settings.wrapStampsAcrossEdges = outputSettings.wrapStampsAcrossEdges;
            settings.toroidalSpacing = outputSettings.toroidalSpacing;
            settings.pattern = profile.preserveStructure && similarity > 0.52f ? PickGuidedPattern(rng, outputSettings.generationPatternVariety01, outputSettings.generateSeamless) : PickDetailPattern(rng, outputSettings.generationPatternVariety01, outputSettings.generateSeamless);

            int densityMin = Mathf.Clamp(outputSettings.generationDensityMin, 1, 512);
            int densityMax = Mathf.Clamp(outputSettings.generationDensityMax, densityMin, 512);
            int guidedDensity = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(densityMin, densityMax, profile.density01)), densityMin, densityMax);
            int spread = Mathf.Max(8, Mathf.RoundToInt(Mathf.Lerp(18f, densityMax - densityMin, freedom)));
            if (profile.preserveDensity && similarity > 0.45f)
                settings.count = rng.Next(Mathf.Max(densityMin, guidedDensity - spread), Mathf.Min(densityMax, guidedDensity + spread) + 1);
            else
                settings.count = rng.Next(densityMin, densityMax + 1);

            float guidedRadius = Mathf.Clamp(profile.averageRadius01, outputSettings.generationRadiusMin01, outputSettings.generationRadiusMax01);
            if (profile.preserveScale && similarity > 0.45f)
            {
                float radiusSpread = Mathf.Lerp(0.01f, 0.09f, freedom);
                settings.radiusMin01 = Mathf.Clamp(guidedRadius - radiusSpread * Range(rng, 0.35f, 0.9f), outputSettings.generationRadiusMin01, outputSettings.generationRadiusMax01);
                settings.radiusMax01 = Mathf.Clamp(guidedRadius + radiusSpread * Range(rng, 0.45f, 1.1f), settings.radiusMin01, outputSettings.generationRadiusMax01);
            }
            else
            {
                settings.radiusMin01 = Range(rng, outputSettings.generationRadiusMin01, Mathf.Lerp(outputSettings.generationRadiusMin01, outputSettings.generationRadiusMax01, 0.5f));
                settings.radiusMax01 = Range(rng, settings.radiusMin01, outputSettings.generationRadiusMax01);
            }

            float contrastMin = profile.preserveTone ? Mathf.Lerp(outputSettings.generationContrastMin, outputSettings.generationContrastMax, Mathf.Clamp01(profile.contrast01 - 0.18f)) : outputSettings.generationContrastMin;
            float contrastMax = profile.preserveTone ? Mathf.Lerp(outputSettings.generationContrastMin, outputSettings.generationContrastMax, Mathf.Clamp01(profile.contrast01 + 0.28f)) : outputSettings.generationContrastMax;
            settings.contrast = Range(rng, Mathf.Min(contrastMin, contrastMax), Mathf.Max(contrastMin, contrastMax));
            settings.minSpacing01 = Range(rng, 0.002f, Mathf.Lerp(0.035f, 0.09f, 1f - profile.density01));
            float jitterMax = profile.preserveDetail && similarity > 0.45f ? Mathf.Lerp(0.34f, 0.62f, freedom) : Mathf.Lerp(0.48f, 0.92f, freedom);
            settings.globalJitter01 = Range(rng, Mathf.Lerp(0.04f, 0.22f, similarity), jitterMax);
            settings.backgroundValue = 0f;
            settings.stampShape = profile.preserveStampShape && similarity > 0.5f ? ProceduralTextureStampShape.Gaussian : PickStamp(rng);
            settings.intensityMin = Range(rng, outputSettings.generationIntensityMin, Mathf.Lerp(outputSettings.generationIntensityMin, outputSettings.generationIntensityMax, 0.45f));
            settings.intensityMax = Mathf.Max(settings.intensityMin, Range(rng, settings.intensityMin, outputSettings.generationIntensityMax));
            float edgeMax = profile.preserveSeam && similarity > 0.45f ? Mathf.Lerp(0.42f, 0.68f, freedom) : Mathf.Lerp(0.55f, 0.86f, freedom);
            settings.edgeFalloff = Range(rng, 0.05f, edgeMax);
            settings.ringThickness01 = Range(rng, 0.06f, 0.34f);
            RandomizePatternSpecific(settings, rng);
            layer.Clamp();
            return layer;
        }

        private static InfluenceRecipeProfile BuildInfluenceProfile(IList<ProceduralTextureBaseSettings> lockedSources, ProceduralTextureCombinationSettings outputSettings)
        {
            outputSettings = outputSettings ?? new ProceduralTextureCombinationSettings();
            float total = 0f;
            var profile = new InfluenceRecipeProfile
            {
                mutation01 = Mathf.Clamp01(outputSettings.randomness01),
                adherence01 = Mathf.Clamp01(outputSettings.lockedInfluence01),
                guideStrength01 = 0.65f,
                similarity01 = 0.55f,
                coverage01 = 0.5f,
                contrast01 = 0.65f,
                averageRadius01 = Mathf.Lerp(outputSettings.generationRadiusMin01, outputSettings.generationRadiusMax01, 0.5f),
                density01 = 0.5f,
                detail01 = 0.5f
            };

            if (lockedSources == null || lockedSources.Count == 0)
                return profile;

            float guide = 0f;
            float similarity = 0f;
            float coverage = 0f;
            float contrast = 0f;
            float radius = 0f;
            float density = 0f;
            float detail = 0f;
            for (int i = 0; i < lockedSources.Count; i++)
            {
                ProceduralTextureBaseSettings source = lockedSources[i];
                if (source == null)
                    continue;
                float sourceGuide = Mathf.Clamp01(source.guideStrength01 <= 0f ? Mathf.Clamp01(source.weight) : source.guideStrength01);
                float sourceSimilarity = Mathf.Clamp01(source.similarityTarget01 <= 0f ? 0.55f : source.similarityTarget01);
                float rating = Mathf.Clamp01((source.userRating <= 0 ? 3 : source.userRating) / 5f);
                float weight = Mathf.Max(0.02f, sourceGuide * Mathf.Lerp(0.65f, 1.25f, sourceSimilarity) * Mathf.Lerp(0.75f, 1.15f, rating));
                total += weight;
                guide += sourceGuide * weight;
                similarity += sourceSimilarity * weight;
                float sourceCoverage;
                float sourceContrast;
                if (source.useBakedValues && source.bakedValues != null && source.bakedValues.Length > 0)
                {
                    MeasureBakedSource(source.bakedValues, out sourceCoverage, out sourceContrast);
                }
                else
                {
                    sourceCoverage = source.generation != null ? Mathf.Clamp01(source.generation.count / 512f) : 0.5f;
                    sourceContrast = source.generation != null ? Mathf.Clamp01(source.generation.contrast / 4f) : 0.65f;
                }
                coverage += sourceCoverage * weight;
                contrast += sourceContrast * weight;
                radius += (source.generation != null ? (source.generation.radiusMin01 + source.generation.radiusMax01) * 0.5f : profile.averageRadius01) * weight;
                density += (source.generation != null ? Mathf.Clamp01(source.generation.count / 512f) : sourceCoverage) * weight;
                detail += (source.generation != null ? Mathf.Clamp01(source.generation.globalJitter01) : 0.5f) * weight;
                profile.preserveStructure |= source.preserveStructure || source.guidanceMode == ProceduralTextureGuidanceMode.Preserve;
                profile.preserveDensity |= source.preserveDensity || source.guidanceMode == ProceduralTextureGuidanceMode.Preserve;
                profile.preserveScale |= source.preserveScale || source.guidanceMode == ProceduralTextureGuidanceMode.Preserve;
                profile.preserveDetail |= source.preserveDetail || source.guidanceMode == ProceduralTextureGuidanceMode.Preserve;
                profile.preserveTone |= source.preserveTone || source.guidanceMode == ProceduralTextureGuidanceMode.Preserve;
                profile.preserveColourOrPalette |= source.preserveColourOrPalette || source.guidanceMode == ProceduralTextureGuidanceMode.Preserve;
                profile.preserveStampShape |= source.preserveStampShape;
                profile.preserveBlendOrder |= source.preserveBlendOrder;
                profile.preserveSeam |= source.preserveSeam;
            }

            if (total <= 0f)
                return profile;

            profile.guideStrength01 = guide / total;
            profile.similarity01 = similarity / total;
            profile.coverage01 = coverage / total;
            profile.contrast01 = contrast / total;
            profile.averageRadius01 = Mathf.Clamp(radius / total, outputSettings.generationRadiusMin01, outputSettings.generationRadiusMax01);
            profile.density01 = Mathf.Clamp01(density / total);
            profile.detail01 = Mathf.Clamp01(detail / total);
            return profile;
        }

        private static void MeasureBakedSource(float[] values, out float coverage, out float contrast)
        {
            float min = 1f;
            float max = 0f;
            int covered = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float value = Mathf.Clamp01(values[i]);
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
                if (value >= 0.5f)
                    covered++;
            }
            coverage = values.Length > 0 ? covered / (float)values.Length : 0.5f;
            contrast = Mathf.Clamp01(max - min);
        }

        private static void RandomizeStamp(ProceduralTextureGenerationSettings settings, System.Random rng, bool detail, ProceduralTextureCombinationSettings outputSettings)
        {
            settings.stampShape = PickStamp(rng);
            float radiusMin = outputSettings.generationRadiusMin01;
            float radiusMax = Mathf.Max(radiusMin, outputSettings.generationRadiusMax01);
            settings.radiusMin01 = detail ? Range(rng, radiusMin, Mathf.Lerp(radiusMin, radiusMax, 0.35f)) : Range(rng, radiusMin, Mathf.Lerp(radiusMin, radiusMax, 0.55f));
            settings.radiusMax01 = Mathf.Max(settings.radiusMin01, detail ? Range(rng, settings.radiusMin01, Mathf.Lerp(radiusMin, radiusMax, 0.55f)) : Range(rng, settings.radiusMin01, radiusMax));
            settings.intensityMin = detail ? Range(rng, outputSettings.generationIntensityMin, Mathf.Lerp(outputSettings.generationIntensityMin, outputSettings.generationIntensityMax, 0.35f)) : Range(rng, outputSettings.generationIntensityMin, Mathf.Lerp(outputSettings.generationIntensityMin, outputSettings.generationIntensityMax, 0.55f));
            settings.intensityMax = Mathf.Max(settings.intensityMin, detail ? Range(rng, settings.intensityMin, Mathf.Lerp(outputSettings.generationIntensityMin, outputSettings.generationIntensityMax, 0.82f)) : Range(rng, settings.intensityMin, outputSettings.generationIntensityMax));
            settings.contrast = Range(rng, outputSettings.generationContrastMin, outputSettings.generationContrastMax);
            settings.edgeFalloff = Range(rng, 0.05f, 0.78f);
            settings.ringThickness01 = Range(rng, 0.06f, 0.34f);
        }

        private static void RandomizePatternSpecific(ProceduralTextureGenerationSettings settings, System.Random rng)
        {
            settings.cellsX = rng.Next(5, 28);
            settings.cellsY = rng.Next(5, 28);
            settings.cellJitter01 = Range(rng, 0.08f, 0.9f);
            settings.hexRadius01 = Range(rng, 0.012f, 0.09f);
            settings.hexJitter01 = Range(rng, 0f, 0.46f);
            settings.poissonRadius01 = Range(rng, 0.01f, 0.075f);
            settings.poissonAttempts = rng.Next(12, 40);
            settings.polarRings = rng.Next(2, 10);
            settings.polarSpokesPerRing = rng.Next(8, 56);
            settings.polarRingStep01 = Range(rng, 0.035f, 0.13f);
            settings.polarRadialJitter01 = Range(rng, 0.05f, 0.55f);
            settings.polarAngularJitter01 = Range(rng, 0.03f, 0.35f);
        }

        private static int MaxInfluenceCount(IList<ProceduralTextureBaseSettings> lockedSources)
        {
            return lockedSources != null ? Mathf.Min(lockedSources.Count, ProceduralTextureCombinationUtility.MaxBaseCount) : 0;
        }

        private static ProceduralTexturePattern PickPattern(System.Random rng, float variety, bool seamless)
        {
            ProceduralTexturePattern[] core =
            {
                ProceduralTexturePattern.RandomMinSpacing,
                ProceduralTexturePattern.StratifiedJitterGrid,
                ProceduralTexturePattern.PoissonDisk
            };
            ProceduralTexturePattern[] seamlessExpanded =
            {
                ProceduralTexturePattern.RandomMinSpacing,
                ProceduralTexturePattern.StratifiedJitterGrid,
                ProceduralTexturePattern.HexGrid,
                ProceduralTexturePattern.PoissonDisk,
                ProceduralTexturePattern.HaltonSequence,
                ProceduralTexturePattern.HammersleySequence
            };
            ProceduralTexturePattern[] expanded =
            {
                ProceduralTexturePattern.RandomMinSpacing,
                ProceduralTexturePattern.StratifiedJitterGrid,
                ProceduralTexturePattern.HexGrid,
                ProceduralTexturePattern.PoissonDisk,
                ProceduralTexturePattern.PolarPattern,
                ProceduralTexturePattern.HaltonSequence,
                ProceduralTexturePattern.HammersleySequence,
                ProceduralTexturePattern.SpiralPattern
            };
            if (variety < 0.35f)
                return core[rng.Next(core.Length)];
            ProceduralTexturePattern[] patterns = seamless ? seamlessExpanded : expanded;
            return patterns[rng.Next(patterns.Length)];
        }

        private static ProceduralTexturePattern PickDetailPattern(System.Random rng, float variety, bool seamless)
        {
            ProceduralTexturePattern[] patterns =
            {
                ProceduralTexturePattern.PoissonDisk,
                ProceduralTexturePattern.HaltonSequence,
                ProceduralTexturePattern.HammersleySequence,
                ProceduralTexturePattern.RandomMinSpacing
            };
            int count = variety < 0.35f ? 2 : patterns.Length;
            return patterns[rng.Next(count)];
        }

        private static ProceduralTexturePattern PickGuidedPattern(System.Random rng, float variety, bool seamless)
        {
            ProceduralTexturePattern[] structured =
            {
                ProceduralTexturePattern.PoissonDisk,
                ProceduralTexturePattern.RandomMinSpacing,
                ProceduralTexturePattern.StratifiedJitterGrid,
                ProceduralTexturePattern.HaltonSequence,
                ProceduralTexturePattern.HammersleySequence
            };
            ProceduralTexturePattern[] seamlessExpressive =
            {
                ProceduralTexturePattern.PoissonDisk,
                ProceduralTexturePattern.RandomMinSpacing,
                ProceduralTexturePattern.HexGrid,
                ProceduralTexturePattern.HaltonSequence,
                ProceduralTexturePattern.HammersleySequence
            };
            ProceduralTexturePattern[] expressive =
            {
                ProceduralTexturePattern.PoissonDisk,
                ProceduralTexturePattern.RandomMinSpacing,
                ProceduralTexturePattern.HexGrid,
                ProceduralTexturePattern.PolarPattern,
                ProceduralTexturePattern.SpiralPattern
            };
            ProceduralTexturePattern[] source = seamless ? (variety < 0.45f ? structured : seamlessExpressive) : (variety < 0.45f ? structured : expressive);
            return source[rng.Next(source.Length)];
        }

        private static ProceduralTextureStampShape PickStamp(System.Random rng)
        {
            ProceduralTextureStampShape[] stamps =
            {
                ProceduralTextureStampShape.SoftCircle,
                ProceduralTextureStampShape.Gaussian,
                ProceduralTextureStampShape.Cone,
                ProceduralTextureStampShape.Ring,
                ProceduralTextureStampShape.Square,
                ProceduralTextureStampShape.Diamond
            };
            return stamps[rng.Next(stamps.Length)];
        }

        private static ProceduralTextureBlendMode PickBlend(System.Random rng)
        {
            ProceduralTextureBlendMode[] blends =
            {
                ProceduralTextureBlendMode.Multiply,
                ProceduralTextureBlendMode.Overlay,
                ProceduralTextureBlendMode.Max,
                ProceduralTextureBlendMode.Overlay,
                ProceduralTextureBlendMode.Multiply
            };
            return blends[rng.Next(blends.Length)];
        }

        private static ProceduralTextureBlendMode PickInfluenceBlend(System.Random rng, InfluenceRecipeProfile profile)
        {
            if (profile.preserveTone && profile.similarity01 > 0.6f)
                return rng.NextDouble() < 0.65 ? ProceduralTextureBlendMode.Overlay : ProceduralTextureBlendMode.Multiply;

            ProceduralTextureBlendMode[] blends =
            {
                ProceduralTextureBlendMode.Overlay,
                ProceduralTextureBlendMode.Max,
                ProceduralTextureBlendMode.Add,
                ProceduralTextureBlendMode.Multiply
            };
            return blends[rng.Next(blends.Length)];
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }
    }
}
