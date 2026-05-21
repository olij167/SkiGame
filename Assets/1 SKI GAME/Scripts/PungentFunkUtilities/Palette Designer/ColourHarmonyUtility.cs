using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    public static class ColourHarmonyUtility
    {
        public static List<float> GetHarmonyHues(ColourHarmonyMode mode, float baseHue)
        {
            baseHue = Mathf.Repeat(baseHue, 1f);
            var hues = new List<float> { baseHue };

            switch (mode)
            {
                case ColourHarmonyMode.Monochromatic:
                    break;
                case ColourHarmonyMode.Analogous:
                    hues.Add(WrapHue(baseHue - 1f / 12f));
                    hues.Add(WrapHue(baseHue + 1f / 12f));
                    break;
                case ColourHarmonyMode.Complementary:
                    hues.Add(WrapHue(baseHue + 0.5f));
                    break;
                case ColourHarmonyMode.SplitComplementary:
                    hues.Add(WrapHue(baseHue + 5f / 12f));
                    hues.Add(WrapHue(baseHue + 7f / 12f));
                    break;
                case ColourHarmonyMode.Triadic:
                    hues.Add(WrapHue(baseHue + 1f / 3f));
                    hues.Add(WrapHue(baseHue + 2f / 3f));
                    break;
                case ColourHarmonyMode.Tetradic:
                    hues.Add(WrapHue(baseHue + 1f / 4f));
                    hues.Add(WrapHue(baseHue + 1f / 2f));
                    hues.Add(WrapHue(baseHue + 3f / 4f));
                    break;
                case ColourHarmonyMode.Square:
                    hues.Add(WrapHue(baseHue + 1f / 4f));
                    hues.Add(WrapHue(baseHue + 1f / 2f));
                    hues.Add(WrapHue(baseHue + 3f / 4f));
                    break;
                case ColourHarmonyMode.Contextual:
                    hues.Add(WrapHue(baseHue + 1f / 12f));
                    hues.Add(WrapHue(baseHue - 1f / 12f));
                    hues.Add(WrapHue(baseHue + 0.5f));
                    break;
                case ColourHarmonyMode.RandomBalanced:
                    hues.Add(WrapHue(baseHue + 0.29f));
                    hues.Add(WrapHue(baseHue + 0.57f));
                    hues.Add(WrapHue(baseHue + 0.81f));
                    break;
            }

            return hues;
        }

        public static List<Color> GetHarmony(ColourHarmonyMode mode, Color baseColor, int count, float saturationBias = 1f, float valueBias = 1f)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            List<float> hues = GetHarmonyHues(mode, h);
            var result = new List<Color>();
            count = Mathf.Max(1, count);

            for (int i = 0; i < count; i++)
            {
                float hue = hues[i % hues.Count];
                float sat = Mathf.Clamp01(s * saturationBias);
                float val = Mathf.Clamp01(v * valueBias);
                if (mode == ColourHarmonyMode.Monochromatic)
                {
                    float t = count == 1 ? 0.5f : i / (float)(count - 1);
                    sat = Mathf.Lerp(Mathf.Clamp01(s * 0.55f), Mathf.Clamp01(s * 1.12f), t);
                    val = Mathf.Lerp(0.18f, 0.94f, t);
                }

                Color c = Color.HSVToRGB(hue, sat, val);
                c.a = baseColor.a;
                result.Add(c);
            }

            return result;
        }

        public static float WrapHue(float hue) => Mathf.Repeat(hue, 1f);

        public static float ShortestHueDistance(float a, float b)
        {
            float delta = Mathf.Abs(Mathf.Repeat(a, 1f) - Mathf.Repeat(b, 1f));
            return Mathf.Min(delta, 1f - delta);
        }

        public static float LerpHue(float from, float to, float t)
        {
            from = Mathf.Repeat(from, 1f);
            to = Mathf.Repeat(to, 1f);
            float delta = Mathf.Repeat(to - from + 0.5f, 1f) - 0.5f;
            return Mathf.Repeat(from + delta * Mathf.Clamp01(t), 1f);
        }
    }

}