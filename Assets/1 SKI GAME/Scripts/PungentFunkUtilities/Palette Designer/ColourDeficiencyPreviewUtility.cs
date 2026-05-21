using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    public enum ColourDeficiencyPreviewMode
    {
        None,
        Protanopia,
        Protanomaly,
        Deuteranopia,
        Deuteranomaly,
        Tritanopia,
        Tritanomaly,
        Achromatopsia
    }

    public static class ColourDeficiencyPreviewUtility
    {
        public static Color Apply(ColourDeficiencyPreviewMode type, Color color)
        {
            Color result = type switch
            {
                ColourDeficiencyPreviewMode.Protanopia => new Color(
                    0.566f * color.r + 0.433f * color.g,
                    0.558f * color.r + 0.442f * color.g,
                    0.242f * color.g + 0.758f * color.b,
                    color.a),
                ColourDeficiencyPreviewMode.Protanomaly => new Color(
                    0.817f * color.r + 0.183f * color.g,
                    0.333f * color.r + 0.667f * color.g,
                    color.b,
                    color.a),
                ColourDeficiencyPreviewMode.Deuteranopia => new Color(
                    0.625f * color.r + 0.375f * color.g,
                    0.700f * color.r + 0.300f * color.g,
                    0.300f * color.g + 0.700f * color.b,
                    color.a),
                ColourDeficiencyPreviewMode.Deuteranomaly => new Color(
                    0.800f * color.r + 0.200f * color.g,
                    0.258f * color.r + 0.742f * color.g,
                    color.b,
                    color.a),
                ColourDeficiencyPreviewMode.Tritanopia => new Color(
                    0.950f * color.r + 0.050f * color.g,
                    0.433f * color.g + 0.567f * color.b,
                    0.475f * color.g + 0.525f * color.b,
                    color.a),
                ColourDeficiencyPreviewMode.Tritanomaly => new Color(
                    color.r,
                    0.500f * color.g + 0.500f * color.b,
                    0.800f * color.b + 0.200f * color.g,
                    color.a),
                ColourDeficiencyPreviewMode.Achromatopsia => ToGray(color),
                _ => color
            };

            result.r = Mathf.Clamp01(result.r);
            result.g = Mathf.Clamp01(result.g);
            result.b = Mathf.Clamp01(result.b);
            result.a = color.a;
            return result;
        }

        private static Color ToGray(Color color)
        {
            float gray = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            return new Color(gray, gray, gray, color.a);
        }
    }

}