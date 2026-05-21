using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    public static class PaletteGeneratorUtility
    {
        private static readonly PaletteSwatchRole[] DefaultRoleOrder =
        {
            PaletteSwatchRole.Background,
            PaletteSwatchRole.Panel,
            PaletteSwatchRole.Text,
            PaletteSwatchRole.MutedText,
            PaletteSwatchRole.Accent,
            PaletteSwatchRole.AccentSecondary,
            PaletteSwatchRole.Highlight,
            PaletteSwatchRole.Warning,
            PaletteSwatchRole.Success,
            PaletteSwatchRole.Error,
            PaletteSwatchRole.Outline,
            PaletteSwatchRole.Shadow
        };

        public static List<PaletteSwatch> Generate(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches = null)
        {
            settings ??= new PaletteGenerationSettings();
            settings = settings.Clone();
            settings.Clamp();

            var result = new List<PaletteSwatch>();
            if (settings.preserveLockedSwatches && currentSwatches != null)
            {
                for (int i = 0; i < currentSwatches.Count && result.Count < settings.targetSwatchCount; i++)
                {
                    PaletteSwatch swatch = currentSwatches[i];
                    if (swatch != null && swatch.locked)
                        result.Add(swatch.Clone());
                }
            }

            System.Random rng = settings.useSeed ? new System.Random(settings.seed) : new System.Random(unchecked(Environment.TickCount * 31 + Guid.NewGuid().GetHashCode()));
            Color anchor = ResolveAnchorColor(settings, currentSwatches, result, rng);
            Color.RGBToHSV(anchor, out float anchorHue, out float anchorSaturation, out float anchorValue);
            List<float> harmonyHues = ColourHarmonyUtility.GetHarmonyHues(settings.harmonyMode, anchorHue);

            int guard = 0;
            while (result.Count < settings.targetSwatchCount && guard < settings.targetSwatchCount * 64)
            {
                int index = result.Count;
                PaletteSwatchRole role = GetDefaultRole(index);
                Color color = GenerateRoleColor(role, settings, result, harmonyHues, anchorSaturation, anchorValue, rng, index);

                if (settings.avoidNearDuplicates && ContainsNearDuplicate(result, color))
                {
                    guard++;
                    continue;
                }

                result.Add(new PaletteSwatch(GetDefaultName(role, result.Count + 1), color, role, false, index));
                guard++;
            }

            while (result.Count < settings.targetSwatchCount)
            {
                int index = result.Count;
                PaletteSwatchRole role = GetDefaultRole(index);
                Color fallback = RandomColor(settings, rng);
                result.Add(new PaletteSwatch(GetDefaultName(role, index + 1), fallback, role, false, index));
            }

            if (settings.enforceTextContrast)
                EnforceTextContrast(result);

            return result;
        }

        public static List<PaletteSwatch> RegenerateUnlocked(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches)
        {
            settings ??= new PaletteGenerationSettings();
            var clone = settings.Clone();
            clone.preserveLockedSwatches = true;
            clone.targetSwatchCount = currentSwatches != null && currentSwatches.Count > 0 ? currentSwatches.Count : settings.targetSwatchCount;
            return Generate(clone, currentSwatches);
        }

        public static PaletteGenerationSettings TightenRangesFromSwatches(IList<PaletteSwatch> swatches, PaletteGenerationSettings fallback = null, float padding = 0.08f)
        {
            var settings = fallback != null ? fallback.Clone() : new PaletteGenerationSettings();
            if (swatches == null || swatches.Count == 0)
                return settings;

            float minS = 1f;
            float maxS = 0f;
            float minV = 1f;
            float maxV = 0f;
            bool any = false;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;
                Color.RGBToHSV(swatch.color, out _, out float s, out float v);
                minS = Mathf.Min(minS, s);
                maxS = Mathf.Max(maxS, s);
                minV = Mathf.Min(minV, v);
                maxV = Mathf.Max(maxV, v);
                any = true;
            }

            if (!any)
                return settings;

            settings.saturationRange = new Vector2(Mathf.Clamp01(minS - padding), Mathf.Clamp01(maxS + padding));
            settings.valueRange = new Vector2(Mathf.Clamp01(minV - padding), Mathf.Clamp01(maxV + padding));
            return settings;
        }

        private static Color ResolveAnchorColor(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, IList<PaletteSwatch> lockedSwatches, System.Random rng)
        {
            if (lockedSwatches != null && lockedSwatches.Count > 0)
                return lockedSwatches[0].color;

            if (currentSwatches != null && currentSwatches.Count > 0)
            {
                for (int i = 0; i < currentSwatches.Count; i++)
                {
                    if (currentSwatches[i] != null)
                        return currentSwatches[i].color;
                }
            }

            float h = Range(settings.hueRange, rng);
            float s = Range(settings.saturationRange, rng);
            float v = Range(settings.valueRange, rng);
            return Color.HSVToRGB(h, s, v);
        }

        private static Color GenerateRoleColor(PaletteSwatchRole role, PaletteGenerationSettings settings, IList<PaletteSwatch> existing, IList<float> harmonyHues, float anchorSaturation, float anchorValue, System.Random rng, int index)
        {
            float randomHue = Range(settings.hueRange, rng);
            float harmonyHue = harmonyHues.Count > 0 ? harmonyHues[index % harmonyHues.Count] : randomHue;
            float hue = settings.harmonyMode == ColourHarmonyMode.RandomBalanced
                ? randomHue
                : ColourHarmonyUtility.LerpHue(randomHue, harmonyHue, settings.harmonyInfluence);

            float s = Mathf.Lerp(Range(settings.saturationRange, rng), Mathf.Clamp01(anchorSaturation), settings.harmonyInfluence * 0.45f);
            float v = Mathf.Lerp(Range(settings.valueRange, rng), Mathf.Clamp01(anchorValue), settings.harmonyInfluence * 0.22f);

            float jitter = settings.randomVariation;
            hue = Mathf.Repeat(hue + RandomSigned(rng) * jitter * 0.08f, 1f);
            s = Mathf.Clamp01(s + RandomSigned(rng) * jitter * 0.18f);
            v = Mathf.Clamp01(v + RandomSigned(rng) * jitter * 0.22f);

            switch (role)
            {
                case PaletteSwatchRole.Background:
                    s = Mathf.Clamp(s * 0.55f, 0.04f, 0.45f);
                    v = Mathf.Lerp(0.07f, 0.22f, 1f - settings.contrastInfluence);
                    break;
                case PaletteSwatchRole.Panel:
                    s = Mathf.Clamp(s * 0.48f, 0.04f, 0.50f);
                    v = Mathf.Lerp(0.14f, 0.34f, 1f - settings.contrastInfluence);
                    break;
                case PaletteSwatchRole.Text:
                    s = Mathf.Clamp(s * 0.15f, 0f, 0.22f);
                    v = 0.94f;
                    break;
                case PaletteSwatchRole.MutedText:
                    s = Mathf.Clamp(s * 0.22f, 0f, 0.32f);
                    v = 0.68f;
                    break;
                case PaletteSwatchRole.Accent:
                case PaletteSwatchRole.AccentSecondary:
                case PaletteSwatchRole.Highlight:
                    s = Mathf.Clamp(s * 1.15f, 0.45f, 1f);
                    v = Mathf.Clamp(v * 1.08f, 0.52f, 1f);
                    break;
                case PaletteSwatchRole.Warning:
                    hue = Mathf.Lerp(hue, 0.11f, 0.65f);
                    s = Mathf.Clamp(s, 0.55f, 1f);
                    v = Mathf.Clamp(v, 0.55f, 1f);
                    break;
                case PaletteSwatchRole.Success:
                    hue = Mathf.Lerp(hue, 0.34f, 0.65f);
                    s = Mathf.Clamp(s, 0.42f, 1f);
                    v = Mathf.Clamp(v, 0.42f, 0.92f);
                    break;
                case PaletteSwatchRole.Error:
                    hue = Mathf.Lerp(hue, 0.0f, 0.72f);
                    s = Mathf.Clamp(s, 0.52f, 1f);
                    v = Mathf.Clamp(v, 0.42f, 0.95f);
                    break;
                case PaletteSwatchRole.Outline:
                    s = Mathf.Clamp(s * 0.35f, 0.02f, 0.35f);
                    v = 0.46f;
                    break;
                case PaletteSwatchRole.Shadow:
                    s = Mathf.Clamp(s * 0.35f, 0.02f, 0.30f);
                    v = 0.04f;
                    break;
            }

            Color color = Color.HSVToRGB(hue, Mathf.Clamp01(s), Mathf.Clamp01(v));
            color.a = 1f;
            return color;
        }

        private static void EnforceTextContrast(IList<PaletteSwatch> swatches)
        {
            PaletteSwatch background = FindRole(swatches, PaletteSwatchRole.Background) ?? FindRole(swatches, PaletteSwatchRole.Panel);
            PaletteSwatch panel = FindRole(swatches, PaletteSwatchRole.Panel) ?? background;
            if (background == null)
                return;

            ImproveRoleAgainst(PaletteSwatchRole.Text, background.color, swatches, 4.5f);
            ImproveRoleAgainst(PaletteSwatchRole.MutedText, background.color, swatches, 3f);
            if (panel != null)
            {
                ImproveRoleAgainst(PaletteSwatchRole.Text, panel.color, swatches, 4.5f);
                ImproveRoleAgainst(PaletteSwatchRole.MutedText, panel.color, swatches, 3f);
            }
        }

        private static void ImproveRoleAgainst(PaletteSwatchRole role, Color background, IList<PaletteSwatch> swatches, float target)
        {
            PaletteSwatch swatch = FindRole(swatches, role);
            if (swatch == null || swatch.locked)
                return;

            if (ColourContrastUtility.GetContrastRatio(swatch.color, background) < target)
                swatch.color = ColourContrastUtility.ImproveContrast(swatch.color, background, target);
        }

        private static PaletteSwatch FindRole(IList<PaletteSwatch> swatches, PaletteSwatchRole role)
        {
            if (swatches == null)
                return null;
            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null && swatches[i].role == role)
                    return swatches[i];
            }
            return null;
        }

        private static bool ContainsNearDuplicate(IList<PaletteSwatch> swatches, Color color)
        {
            if (swatches == null)
                return false;
            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null && ColourContrastUtility.AreColorsSimilar(swatches[i].color, color, 0.05f))
                    return true;
            }
            return false;
        }

        private static Color RandomColor(PaletteGenerationSettings settings, System.Random rng)
        {
            return Color.HSVToRGB(Range(settings.hueRange, rng), Range(settings.saturationRange, rng), Range(settings.valueRange, rng));
        }

        private static PaletteSwatchRole GetDefaultRole(int index)
        {
            if (index >= 0 && index < DefaultRoleOrder.Length)
                return DefaultRoleOrder[index];
            return PaletteSwatchRole.Custom;
        }

        private static string GetDefaultName(PaletteSwatchRole role, int index)
        {
            if (role == PaletteSwatchRole.None || role == PaletteSwatchRole.Custom)
                return $"Swatch {index}";
            return ObjectNamesLike(role.ToString());
        }

        private static string ObjectNamesLike(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Swatch";

            var chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                    chars.Add(' ');
                chars.Add(c);
            }
            return new string(chars.ToArray());
        }

        private static float Range(Vector2 range, System.Random rng)
        {
            return Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
        }

        private static float RandomSigned(System.Random rng)
        {
            return (float)rng.NextDouble() * 2f - 1f;
        }
    }

}