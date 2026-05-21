using System;
using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    [Serializable]
    public class PaletteGenerationSettings
    {
        public ColourHarmonyMode harmonyMode = ColourHarmonyMode.Contextual;
        [Min(1)] public int targetSwatchCount = 8;
        public Vector2 hueRange = new Vector2(0f, 1f);
        public Vector2 saturationRange = new Vector2(0.35f, 0.95f);
        public Vector2 valueRange = new Vector2(0.18f, 0.95f);
        [Range(0f, 1f)] public float harmonyInfluence = 0.75f;
        [Range(0f, 1f)] public float contrastInfluence = 0.55f;
        [Range(0f, 1f)] public float randomVariation = 0.18f;
        public bool preserveLockedSwatches = true;
        public bool enforceTextContrast = true;
        public bool avoidNearDuplicates = true;
        public bool preferReadableAccentPairs = true;
        public int seed = 12345;
        public bool useSeed;

        public void Clamp()
        {
            targetSwatchCount = Mathf.Clamp(targetSwatchCount, 1, 64);
            hueRange = ClampRange01(hueRange, new Vector2(0f, 1f));
            saturationRange = ClampRange01(saturationRange, new Vector2(0f, 1f));
            valueRange = ClampRange01(valueRange, new Vector2(0f, 1f));
            harmonyInfluence = Mathf.Clamp01(harmonyInfluence);
            contrastInfluence = Mathf.Clamp01(contrastInfluence);
            randomVariation = Mathf.Clamp01(randomVariation);
        }

        public PaletteGenerationSettings Clone()
        {
            return new PaletteGenerationSettings
            {
                harmonyMode = harmonyMode,
                targetSwatchCount = targetSwatchCount,
                hueRange = hueRange,
                saturationRange = saturationRange,
                valueRange = valueRange,
                harmonyInfluence = harmonyInfluence,
                contrastInfluence = contrastInfluence,
                randomVariation = randomVariation,
                preserveLockedSwatches = preserveLockedSwatches,
                enforceTextContrast = enforceTextContrast,
                avoidNearDuplicates = avoidNearDuplicates,
                preferReadableAccentPairs = preferReadableAccentPairs,
                seed = seed,
                useSeed = useSeed
            };
        }

        private static Vector2 ClampRange01(Vector2 value, Vector2 fallback)
        {
            float x = Mathf.Clamp01(value.x);
            float y = Mathf.Clamp01(value.y);
            if (y < x)
                (x, y) = (y, x);
            if (Mathf.Approximately(x, y))
                return fallback;
            return new Vector2(x, y);
        }
    }

    public enum ColourHarmonyMode
    {
        Monochromatic,
        Analogous,
        Complementary,
        SplitComplementary,
        Triadic,
        Tetradic,
        Square,
        RandomBalanced,
        Contextual
    }

}