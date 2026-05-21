using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PungentFunk.Utilities.Generation
{
    public readonly struct ProceduralTextureSite
    {
        public readonly Vector2 Position01;
        public readonly float Radius01;
        public readonly float Intensity;
        public readonly float RotationRadians;

        public ProceduralTextureSite(Vector2 position01, float radius01, float intensity, float rotationRadians)
        {
            Position01 = position01;
            Radius01 = radius01;
            Intensity = intensity;
            RotationRadians = rotationRadians;
        }
    }

    public static class ProceduralTextureGenerator
    {
        private const float Epsilon = 0.000001f;

        public static Texture2D GenerateTexture(ProceduralTextureGenerationSettings settings, List<ProceduralTextureSite> reusableSites = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.Clamp();
            var values = GenerateValues(settings, reusableSites);
            var texture = new Texture2D(settings.width, settings.height, settings.textureFormat, settings.mipChain, settings.linear)
            {
                name = "Generated Procedural Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[settings.width * settings.height];
            for (int i = 0; i < values.Length; i++)
                pixels[i] = EvaluateColor(settings, values[i]);

            texture.SetPixels(pixels);
            texture.Apply(settings.mipChain, false);
            return texture;
        }

        public static float[] GenerateValues(ProceduralTextureGenerationSettings settings, List<ProceduralTextureSite> reusableSites = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.Clamp();
            int width = settings.width;
            int height = settings.height;
            float[] values = new float[width * height];
            Fill(values, settings.backgroundValue);

            List<ProceduralTextureSite> sites = reusableSites ?? new List<ProceduralTextureSite>();
            GenerateSites(settings, sites);
            RasterizeSites(settings, sites, values);
            return values;
        }

        public static void GenerateSites(ProceduralTextureGenerationSettings settings, List<ProceduralTextureSite> results)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            settings.Clamp();
            results.Clear();
            int seed = settings.randomizeSeedOnGenerate ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : settings.seed;
            System.Random rng = new System.Random(seed);

            switch (settings.pattern)
            {
                case ProceduralTexturePattern.StratifiedJitterGrid:
                    GenerateStratified(settings, rng, results);
                    break;
                case ProceduralTexturePattern.HexGrid:
                    GenerateHex(settings, rng, results);
                    break;
                case ProceduralTexturePattern.PoissonDisk:
                    GeneratePoisson(settings, rng, results);
                    break;
                case ProceduralTexturePattern.DensityMap:
                    GenerateDensity(settings, rng, results);
                    break;
                case ProceduralTexturePattern.PolarPattern:
                    GeneratePolar(settings, rng, results);
                    break;
                case ProceduralTexturePattern.HaltonSequence:
                    GenerateHalton(settings, rng, results);
                    break;
                case ProceduralTexturePattern.HammersleySequence:
                    GenerateHammersley(settings, rng, results);
                    break;
                case ProceduralTexturePattern.SpiralPattern:
                    GenerateSpiral(settings, rng, results);
                    break;
                case ProceduralTexturePattern.RandomMinSpacing:
                default:
                    GenerateRandomMinSpacing(settings, rng, results);
                    break;
            }
        }

        public static Color EvaluateColor(ProceduralTextureGenerationSettings settings, float value)
        {
            value = Mathf.Clamp01(value);
            Color result = settings.colorMode switch
            {
                ProceduralTextureColorMode.ForegroundBackground => Color.Lerp(settings.backgroundColor, settings.foregroundColor, value),
                ProceduralTextureColorMode.Gradient => settings.gradient != null ? settings.gradient.Evaluate(value) : Color.Lerp(Color.black, Color.white, value),
                _ => new Color(value, value, value, 1f)
            };
            result.a *= settings.outputOpacity;
            return result;
        }

        private static void RasterizeSites(ProceduralTextureGenerationSettings settings, List<ProceduralTextureSite> sites, float[] values)
        {
            int width = settings.width;
            int height = settings.height;
            bool seamless = settings.generateSeamless && settings.wrapStampsAcrossEdges;

            for (int i = 0; i < sites.Count; i++)
            {
                ProceduralTextureSite site = sites[i];
                if (seamless)
                {
                    RasterizeSite(settings, site, values, -1f, -1f);
                    RasterizeSite(settings, site, values, 0f, -1f);
                    RasterizeSite(settings, site, values, 1f, -1f);
                    RasterizeSite(settings, site, values, -1f, 0f);
                    RasterizeSite(settings, site, values, 0f, 0f);
                    RasterizeSite(settings, site, values, 1f, 0f);
                    RasterizeSite(settings, site, values, -1f, 1f);
                    RasterizeSite(settings, site, values, 0f, 1f);
                    RasterizeSite(settings, site, values, 1f, 1f);
                }
                else
                {
                    RasterizeSite(settings, site, values, 0f, 0f);
                }
            }
        }

        private static void RasterizeSite(ProceduralTextureGenerationSettings settings, ProceduralTextureSite site, float[] values, float offsetX01, float offsetY01)
        {
            int width = settings.width;
            int height = settings.height;
                float radiusPixels = Mathf.Max(1f, site.Radius01 * Mathf.Min(width, height));
                float siteX = site.Position01.x + offsetX01;
                float siteY = site.Position01.y + offsetY01;
                float centerX = siteX * (width - 1);
                float centerY = siteY * (height - 1);
                int minX = Mathf.Clamp(Mathf.FloorToInt(centerX - radiusPixels - 1), 0, width - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(centerX + radiusPixels + 1), 0, width - 1);
                int minY = Mathf.Clamp(Mathf.FloorToInt(centerY - radiusPixels - 1), 0, height - 1);
                int maxY = Mathf.Clamp(Mathf.CeilToInt(centerY + radiusPixels + 1), 0, height - 1);
            if (maxX < minX || maxY < minY)
                return;

                float cos = Mathf.Cos(site.RotationRadians);
                float sin = Mathf.Sin(site.RotationRadians);
                float inverseRadius = 1f / Mathf.Max(Epsilon, site.Radius01);

                for (int y = minY; y <= maxY; y++)
                {
                    float py = height <= 1 ? 0f : y / (float)(height - 1);
                    float localY = py - siteY;
                    for (int x = minX; x <= maxX; x++)
                    {
                        float px = width <= 1 ? 0f : x / (float)(width - 1);
                        float localX = px - siteX;
                        float distanceSq = localX * localX + localY * localY;
                        float radiusSq = site.Radius01 * site.Radius01;
                        if (distanceSq > radiusSq)
                            continue;

                        var local = new Vector2(localX, localY);
                        float distance01 = Mathf.Sqrt(distanceSq) * inverseRadius;
                        if (distance01 > 1f)
                            continue;

                        float contribution = EvaluateStamp(settings, site, local, distance01, cos, sin);
                        if (contribution <= 0f && settings.blendMode != ProceduralTextureBlendMode.Replace)
                            continue;

                        int index = y * width + x;
                        values[index] = Blend(values[index], contribution, settings.blendMode);
                    }
                }
        }

        private static float EvaluateStamp(ProceduralTextureGenerationSettings settings, ProceduralTextureSite site, Vector2 local01, float distance01, float cos, float sin)
        {
            float t = Mathf.Clamp01(1f - distance01);
            float value;
            switch (settings.stampShape)
            {
                case ProceduralTextureStampShape.SoftCircle:
                    value = Mathf.SmoothStep(0f, 1f, t);
                    break;
                case ProceduralTextureStampShape.Gaussian:
                    value = Mathf.Exp(-4f * distance01 * distance01);
                    break;
                case ProceduralTextureStampShape.Cone:
                    value = t;
                    break;
                case ProceduralTextureStampShape.Ring:
                    {
                        float ringCenter = 1f - Mathf.Clamp01(settings.ringThickness01 * 0.5f);
                        float ringWidth = Mathf.Max(0.01f, settings.ringThickness01);
                        value = 1f - Mathf.Clamp01(Mathf.Abs(distance01 - ringCenter) / ringWidth);
                        value = Mathf.SmoothStep(0f, 1f, value);
                        break;
                    }
                case ProceduralTextureStampShape.Square:
                    {
                        float normalized = Mathf.Max(Mathf.Abs(local01.x), Mathf.Abs(local01.y)) / Mathf.Max(Epsilon, site.Radius01);
                        value = normalized <= 1f ? Mathf.SmoothStep(0f, 1f, 1f - normalized) : 0f;
                        break;
                    }
                case ProceduralTextureStampShape.Diamond:
                    {
                        float normalized = (Mathf.Abs(local01.x) + Mathf.Abs(local01.y)) / Mathf.Max(Epsilon, site.Radius01);
                        value = normalized <= 1f ? Mathf.SmoothStep(0f, 1f, 1f - normalized) : 0f;
                        break;
                    }
                case ProceduralTextureStampShape.TextureSource:
                    value = SampleStampTexture(settings, local01, site.Radius01, cos, sin);
                    break;
                default:
                    value = t;
                    break;
            }

            float edge = Mathf.Lerp(1f, t, settings.edgeFalloff);
            value = Mathf.Pow(Mathf.Clamp01(value * edge), Mathf.Max(0.1f, settings.contrast));
            return Mathf.Clamp01(value * site.Intensity);
        }

        private static float SampleStampTexture(ProceduralTextureGenerationSettings settings, Vector2 local01, float radius01, float cos, float sin)
        {
            Texture2D source = settings.stampTexture;
            if (source == null || source.width <= 0 || source.height <= 0)
                return 0f;

            float rx = cos * local01.x - sin * local01.y;
            float ry = sin * local01.x + cos * local01.y;
            float u = 0.5f + rx / Mathf.Max(Epsilon, radius01 * 2f) * settings.stampTextureTiling;
            float v = 0.5f + ry / Mathf.Max(Epsilon, radius01 * 2f) * settings.stampTextureTiling;
            Color c;
            try
            {
                c = source.GetPixelBilinear(ApplySampling(u, settings.stampTextureSampling), ApplySampling(v, settings.stampTextureSampling));
            }
            catch (UnityException)
            {
                return 0f;
            }

            float value = ReadChannel(c, settings.stampTextureChannel);
            return settings.invertStampTexture ? 1f - value : value;
        }

        private static float Blend(float current, float contribution, ProceduralTextureBlendMode blendMode)
        {
            contribution = Mathf.Clamp01(contribution);
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

        private static void GenerateRandomMinSpacing(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            int tries = Mathf.Max(settings.count * 12, 64);
            while (results.Count < settings.count && tries-- > 0)
            {
                Vector2 position = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                    continue;
                AddSite(settings, rng, position, results);
            }
        }

        private static void GenerateStratified(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            for (int y = 0; y < settings.cellsY; y++)
            {
                for (int x = 0; x < settings.cellsX; x++)
                {
                    Vector2 cell = new Vector2(1f / settings.cellsX, 1f / settings.cellsY);
                    Vector2 position = new Vector2((x + 0.5f) * cell.x, (y + 0.5f) * cell.y);
                    position.x += ((float)rng.NextDouble() - 0.5f) * settings.cellJitter01 * cell.x;
                    position.y += ((float)rng.NextDouble() - 0.5f) * settings.cellJitter01 * cell.y;
                    position.x = Mathf.Clamp01(position.x);
                    position.y = Mathf.Clamp01(position.y);
                    if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                        continue;
                    AddSite(settings, rng, position, results);
                    if (results.Count >= settings.count)
                        return;
                }
            }
        }

        private static void GenerateHex(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            float radius = Mathf.Max(0.005f, settings.hexRadius01);
            float w;
            float h;
            float horizontal;
            float vertical;
            if (settings.hexPointyTop)
            {
                w = Mathf.Sqrt(3f) * radius;
                h = 2f * radius;
                horizontal = w;
                vertical = 0.75f * h;
            }
            else
            {
                w = 2f * radius;
                h = Mathf.Sqrt(3f) * radius;
                horizontal = 0.75f * w;
                vertical = h;
            }

            int cols = Mathf.CeilToInt(1f / horizontal) + 3;
            int rows = Mathf.CeilToInt(1f / vertical) + 3;
            for (int row = -1; row <= rows; row++)
            {
                for (int col = -1; col <= cols; col++)
                {
                    float x;
                    float y;
                    if (settings.hexPointyTop)
                    {
                        x = col * horizontal + (row % 2 == 0 ? 0f : horizontal * 0.5f);
                        y = row * vertical;
                    }
                    else
                    {
                        x = col * horizontal;
                        y = row * vertical + (col % 2 == 0 ? 0f : vertical * 0.5f);
                    }

                    x += ((float)rng.NextDouble() - 0.5f) * settings.hexJitter01 * horizontal;
                    y += ((float)rng.NextDouble() - 0.5f) * settings.hexJitter01 * vertical;
                    Vector2 position = new Vector2(x, y);
                    if (!Inside01(position))
                        continue;
                    if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                        continue;
                    AddSite(settings, rng, position, results);
                    if (results.Count >= settings.count)
                        return;
                }
            }
        }

        private static void GeneratePoisson(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            float minRadius = Mathf.Max(0.0025f, settings.poissonRadius01);
            float cell = minRadius / Mathf.Sqrt(2f);
            int gridWidth = Mathf.Max(1, Mathf.CeilToInt(1f / cell));
            int gridHeight = Mathf.Max(1, Mathf.CeilToInt(1f / cell));
            int[] grid = new int[gridWidth * gridHeight];
            Array.Fill(grid, -1);

            List<Vector2> samples = new List<Vector2>(settings.count);
            List<Vector2> active = new List<Vector2>(settings.count);
            RegisterSample(new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()));

            while (active.Count > 0)
            {
                int activeIndex = rng.Next(active.Count);
                Vector2 origin = active[activeIndex];
                bool accepted = false;

                for (int attempt = 0; attempt < settings.poissonAttempts; attempt++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float radius = minRadius * (1f + (float)rng.NextDouble());
                    Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!Inside01(candidate))
                        continue;
                    if (!PoissonValid(candidate))
                        continue;

                    RegisterSample(candidate);
                    accepted = true;
                    break;
                }

                if (!accepted)
                    active.RemoveAt(activeIndex);

                int hardCap = settings.poissonHardCap > 0 ? settings.poissonHardCap : settings.count;
                if (samples.Count >= hardCap || samples.Count >= settings.count)
                    break;
            }

            for (int i = 0; i < samples.Count && results.Count < settings.count; i++)
                AddSite(settings, rng, samples[i], results);

            void RegisterSample(Vector2 sample)
            {
                int index = samples.Count;
                samples.Add(sample);
                active.Add(sample);
                int gx = Mathf.Clamp((int)(sample.x / cell), 0, gridWidth - 1);
                int gy = Mathf.Clamp((int)(sample.y / cell), 0, gridHeight - 1);
                grid[gy * gridWidth + gx] = index;
            }

            bool PoissonValid(Vector2 candidate)
            {
                int gx = Mathf.Clamp((int)(candidate.x / cell), 0, gridWidth - 1);
                int gy = Mathf.Clamp((int)(candidate.y / cell), 0, gridHeight - 1);
                float minRadiusSq = minRadius * minRadius;
                for (int y = -2; y <= 2; y++)
                {
                    for (int x = -2; x <= 2; x++)
                    {
                        int nx = gx + x;
                        int ny = gy + y;
                        if (nx < 0 || ny < 0 || nx >= gridWidth || ny >= gridHeight)
                            continue;
                        int sampleIndex = grid[ny * gridWidth + nx];
                        if (sampleIndex >= 0 && DistanceSquared(settings, candidate, samples[sampleIndex]) < minRadiusSq)
                            return false;
                    }
                }
                if (settings.generateSeamless && settings.toroidalSpacing)
                {
                    for (int i = 0; i < samples.Count; i++)
                    {
                        if (DistanceSquared(settings, candidate, samples[i]) < minRadiusSq)
                            return false;
                    }
                }
                return true;
            }
        }

        private static void GenerateDensity(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            int tries = Mathf.Max(settings.count * 16, 128);
            AnimationCurve curve = settings.densityCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            while (results.Count < settings.count && tries-- > 0)
            {
                Vector2 position = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                float density = SampleDensity(settings, position);
                density = Mathf.Clamp01(curve.Evaluate(density));
                if (density < settings.densityThreshold01)
                    continue;
                if ((float)rng.NextDouble() > density)
                    continue;
                if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                    continue;
                AddSite(settings, rng, position, results);
            }
        }

        private static void GeneratePolar(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            for (int ring = 1; ring <= settings.polarRings; ring++)
            {
                float baseRadius = ring * settings.polarRingStep01;
                for (int spoke = 0; spoke < settings.polarSpokesPerRing; spoke++)
                {
                    float normalized = spoke / (float)settings.polarSpokesPerRing;
                    float angle = normalized * Mathf.PI * 2f;
                    angle += ((float)rng.NextDouble() - 0.5f) * settings.polarAngularJitter01 * Mathf.PI * 2f;
                    float radius = baseRadius * (1f + ((float)rng.NextDouble() - 0.5f) * settings.polarRadialJitter01);
                    Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!Inside01(position))
                        continue;
                    if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                        continue;
                    AddSite(settings, rng, position, results);
                    if (results.Count >= settings.count)
                        return;
                }
            }
        }

        private static void GenerateHalton(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            for (int i = 0; i < settings.count; i++)
            {
                Vector2 position = new Vector2(Halton(i + 1, 2), Halton(i + 1, 3));
                ApplyGlobalJitter(settings, rng, ref position);
                if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                    continue;
                AddSite(settings, rng, position, results);
            }
        }

        private static void GenerateHammersley(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            int n = Mathf.Max(1, settings.count);
            for (int i = 0; i < n; i++)
            {
                Vector2 position = new Vector2((i + 0.5f) / n, Halton(i + 1, 2));
                ApplyGlobalJitter(settings, rng, ref position);
                if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                    continue;
                AddSite(settings, rng, position, results);
            }
        }

        private static void GenerateSpiral(ProceduralTextureGenerationSettings settings, System.Random rng, List<ProceduralTextureSite> results)
        {
            int n = Mathf.Max(1, settings.count);
            Vector2 center = new Vector2(0.5f, 0.5f);
            float scale = 0.33f / Mathf.Sqrt(n);
            for (int i = 0; i < n; i++)
            {
                float t = i * 0.9f;
                float r = scale * t;
                Vector2 position = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r;
                ApplyGlobalJitter(settings, rng, ref position);
                if (settings.generateSeamless)
                {
                    position.x = Mathf.Repeat(position.x, 1f);
                    position.y = Mathf.Repeat(position.y, 1f);
                }
                else
                {
                    position.x = Mathf.Clamp01(position.x);
                    position.y = Mathf.Clamp01(position.y);
                }
                if (!HasMinSpacing(settings, position, settings.minSpacing01, results))
                    continue;
                AddSite(settings, rng, position, results);
            }
        }

        private static void AddSite(ProceduralTextureGenerationSettings settings, System.Random rng, Vector2 position, List<ProceduralTextureSite> results)
        {
            if (settings.generateSeamless)
            {
                position.x = Mathf.Repeat(position.x, 1f);
                position.y = Mathf.Repeat(position.y, 1f);
            }
            else
            {
                position.x = Mathf.Clamp01(position.x);
                position.y = Mathf.Clamp01(position.y);
            }
            float radius = Lerp(settings.radiusMin01, settings.radiusMax01, rng);
            float intensity = Lerp(settings.intensityMin, settings.intensityMax, rng);
            float rotation = Lerp(settings.rotationJitterDegrees.x, settings.rotationJitterDegrees.y, rng) * Mathf.Deg2Rad;
            results.Add(new ProceduralTextureSite(position, radius, intensity, rotation));
        }

        private static void ApplyGlobalJitter(ProceduralTextureGenerationSettings settings, System.Random rng, ref Vector2 position)
        {
            if (settings.globalJitter01 <= 0f)
                return;
            float amount = settings.globalJitter01 / Mathf.Sqrt(Mathf.Max(1, settings.count));
            position.x += ((float)rng.NextDouble() - 0.5f) * amount;
            position.y += ((float)rng.NextDouble() - 0.5f) * amount;
            if (settings.generateSeamless)
            {
                position.x = Mathf.Repeat(position.x, 1f);
                position.y = Mathf.Repeat(position.y, 1f);
            }
        }

        private static bool HasMinSpacing(ProceduralTextureGenerationSettings settings, Vector2 position, float minSpacing, List<ProceduralTextureSite> sites)
        {
            if (minSpacing <= 0f)
                return true;
            float minSq = minSpacing * minSpacing;
            for (int i = 0; i < sites.Count; i++)
            {
                if (DistanceSquared(settings, sites[i].Position01, position) < minSq)
                    return false;
            }
            return true;
        }

        private static float SampleDensity(ProceduralTextureGenerationSettings settings, Vector2 position01)
        {
            if (settings.densityMap == null)
                return 1f;

            float u = ApplySampling(position01.x * settings.densityTiling.x + settings.densityOffset.x, settings.densitySampling);
            float v = ApplySampling(position01.y * settings.densityTiling.y + settings.densityOffset.y, settings.densitySampling);
            try
            {
                return ReadChannel(settings.densityMap.GetPixelBilinear(u, v), settings.densityChannel);
            }
            catch (UnityException)
            {
                return 0f;
            }
        }

        private static float ReadChannel(Color color, ProceduralTextureChannel channel)
        {
            return channel switch
            {
                ProceduralTextureChannel.R => color.r,
                ProceduralTextureChannel.G => color.g,
                ProceduralTextureChannel.B => color.b,
                ProceduralTextureChannel.A => color.a,
                _ => color.grayscale
            };
        }

        private static bool Inside01(Vector2 position)
        {
            return position.x >= 0f && position.x <= 1f && position.y >= 0f && position.y <= 1f;
        }

        private static float DistanceSquared(ProceduralTextureGenerationSettings settings, Vector2 a, Vector2 b)
        {
            float dx = Mathf.Abs(a.x - b.x);
            float dy = Mathf.Abs(a.y - b.y);
            if (settings != null && settings.generateSeamless && settings.toroidalSpacing)
            {
                dx = Mathf.Min(dx, 1f - dx);
                dy = Mathf.Min(dy, 1f - dy);
            }
            return dx * dx + dy * dy;
        }

        private static float ApplySampling(float value, ProceduralTextureSourceSamplingMode mode)
        {
            switch (mode)
            {
                case ProceduralTextureSourceSamplingMode.Clamp:
                    return Mathf.Clamp01(value);
                case ProceduralTextureSourceSamplingMode.Mirror:
                    {
                        float repeated = Mathf.Repeat(value, 2f);
                        return repeated <= 1f ? repeated : 2f - repeated;
                    }
                case ProceduralTextureSourceSamplingMode.Wrap:
                default:
                    return Mathf.Repeat(value, 1f);
            }
        }

        private static float Lerp(float min, float max, System.Random rng)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        private static float Halton(int index, int b)
        {
            float f = 1f;
            float r = 0f;
            while (index > 0)
            {
                f /= b;
                r += f * (index % b);
                index /= b;
            }
            return r;
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

}
