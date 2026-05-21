using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    public static class ColourContrastUtility
    {
        public static Color GetReadableTextColor(Color background)
        {
            return GetRelativeLuminance(background) > 0.5f ? Color.black : Color.white;
        }

        public static bool AreColorsSimilar(Color a, Color b, float tolerance = 0.035f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance;
        }

        public static float GetContrastRatio(Color a, Color b)
        {
            float l1 = GetRelativeLuminance(a);
            float l2 = GetRelativeLuminance(b);
            return (Mathf.Max(l1, l2) + 0.05f) / (Mathf.Min(l1, l2) + 0.05f);
        }

        public static float GetRelativeLuminance(Color color)
        {
            float Linear(float x) => x <= 0.03928f ? x / 12.92f : Mathf.Pow((x + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        public static string GetReadableRating(float contrast)
        {
            if (contrast >= 7f) return "Excellent";
            if (contrast >= 4.5f) return "Good";
            if (contrast >= 3f) return "Marginal";
            return "Poor";
        }

        public static string GetWCAGRating(float contrast)
        {
            if (contrast >= 7f) return "AAA";
            if (contrast >= 4.5f) return "AA";
            if (contrast >= 3f) return "Large AA";
            return "Fail";
        }

        public static bool PassesAA(float contrast) => contrast >= 4.5f;
        public static bool PassesAAA(float contrast) => contrast >= 7f;

        public static Color ImproveContrast(Color foreground, Color background, float targetContrast = 4.5f)
        {
            Color result = foreground;
            bool lighten = GetRelativeLuminance(foreground) >= GetRelativeLuminance(background);

            for (int i = 0; i < 32 && GetContrastRatio(result, background) < targetContrast; i++)
            {
                Color.RGBToHSV(result, out float h, out float s, out float v);
                v = lighten ? Mathf.Clamp01(v + 0.04f) : Mathf.Clamp01(v - 0.04f);
                if (v <= 0.03f || v >= 0.97f)
                    s = Mathf.Clamp01(s - 0.025f);
                result = Color.HSVToRGB(h, s, v);
                result.a = foreground.a;
            }

            if (GetContrastRatio(result, background) < targetContrast)
            {
                Color white = Color.white;
                Color black = Color.black;
                result = GetContrastRatio(white, background) >= GetContrastRatio(black, background) ? white : black;
                result.a = foreground.a;
            }

            return result;
        }
    }

}