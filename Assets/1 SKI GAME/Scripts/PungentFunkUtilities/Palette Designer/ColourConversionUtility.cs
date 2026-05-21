using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    public static class ColourConversionUtility
    {
        public struct ColourModeValues
        {
            public float v1;
            public float v2;
            public float v3;

            public ColourModeValues(float v1, float v2, float v3)
            {
                this.v1 = v1;
                this.v2 = v2;
                this.v3 = v3;
            }
        }

        public enum ColourValueMode
        {
            RGB,
            HSV,
            CMYK,
            LAB,
            Grayscale
        }

        public static string ToHexRGB(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
        public static string ToHexRGBA(Color color) => "#" + ColorUtility.ToHtmlStringRGBA(color);

        public static bool TryParseHex(string hex, out Color color)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                color = Color.white;
                return false;
            }

            string normalized = hex.Trim();
            if (!normalized.StartsWith("#"))
                normalized = "#" + normalized;

            return ColorUtility.TryParseHtmlString(normalized, out color);
        }

        public static ColourModeValues GetModeValues(Color rgb, ColourValueMode mode)
        {
            switch (mode)
            {
                case ColourValueMode.RGB:
                    return new ColourModeValues(rgb.r, rgb.g, rgb.b);
                case ColourValueMode.HSV:
                    Color.RGBToHSV(rgb, out float h, out float s, out float v);
                    return new ColourModeValues(h, s, v);
                case ColourValueMode.CMYK:
                    RGBToCMYK(rgb, out float c, out float m, out float y, out _);
                    return new ColourModeValues(c, m, y);
                case ColourValueMode.LAB:
                    RGBToLAB(rgb, out float l, out float a, out float bLab);
                    return new ColourModeValues(l, a, bLab);
                case ColourValueMode.Grayscale:
                    float gray = (rgb.r + rgb.g + rgb.b) / 3f;
                    return new ColourModeValues(gray, gray, gray);
                default:
                    return new ColourModeValues(rgb.r, rgb.g, rgb.b);
            }
        }

        public static Color ApplyModeValuesToRGB(ColourValueMode mode, float v1, float v2, float v3, Color currentRGB)
        {
            switch (mode)
            {
                case ColourValueMode.RGB:
                    return new Color(Mathf.Clamp01(v1), Mathf.Clamp01(v2), Mathf.Clamp01(v3), currentRGB.a);
                case ColourValueMode.HSV:
                    Color hsv = Color.HSVToRGB(Mathf.Repeat(v1, 1f), Mathf.Clamp01(v2), Mathf.Clamp01(v3));
                    hsv.a = currentRGB.a;
                    return hsv;
                case ColourValueMode.CMYK:
                    Color cmyk = CMYKToRGB(v1, v2, v3, 0f);
                    cmyk.a = currentRGB.a;
                    return cmyk;
                case ColourValueMode.LAB:
                    Color lab = LABToRGB(v1, v2, v3);
                    lab.a = currentRGB.a;
                    return lab;
                case ColourValueMode.Grayscale:
                    float gray = Mathf.Clamp01(v1);
                    return new Color(gray, gray, gray, currentRGB.a);
                default:
                    return currentRGB;
            }
        }

        public static string FormatRGB(Color color)
        {
            return $"RGB {Mathf.RoundToInt(color.r * 255f)}, {Mathf.RoundToInt(color.g * 255f)}, {Mathf.RoundToInt(color.b * 255f)}";
        }

        public static string FormatHSV(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            return $"HSV {Mathf.RoundToInt(h * 360f)}°, {Mathf.RoundToInt(s * 100f)}%, {Mathf.RoundToInt(v * 100f)}%";
        }

        public static void RGBToCMYK(Color rgb, out float c, out float m, out float y, out float k)
        {
            float r = Mathf.Clamp01(rgb.r);
            float g = Mathf.Clamp01(rgb.g);
            float b = Mathf.Clamp01(rgb.b);
            k = 1f - Mathf.Max(r, Mathf.Max(g, b));
            if (Mathf.Approximately(k, 1f))
            {
                c = 0f;
                m = 0f;
                y = 0f;
            }
            else
            {
                c = (1f - r - k) / (1f - k);
                m = (1f - g - k) / (1f - k);
                y = (1f - b - k) / (1f - k);
            }
        }

        public static Color CMYKToRGB(float c, float m, float y, float k)
        {
            c = Mathf.Clamp01(c);
            m = Mathf.Clamp01(m);
            y = Mathf.Clamp01(y);
            k = Mathf.Clamp01(k);
            return new Color((1f - c) * (1f - k), (1f - m) * (1f - k), (1f - y) * (1f - k), 1f);
        }

        public static void RGBToLAB(Color color, out float l, out float a, out float b)
        {
            float r = PivotRGB(color.r);
            float g = PivotRGB(color.g);
            float blue = PivotRGB(color.b);

            float x = r * 0.4124f + g * 0.3576f + blue * 0.1805f;
            float y = r * 0.2126f + g * 0.7152f + blue * 0.0722f;
            float z = r * 0.0193f + g * 0.1192f + blue * 0.9505f;

            x /= 0.95047f;
            y /= 1.00000f;
            z /= 1.08883f;

            x = PivotXYZ(x);
            y = PivotXYZ(y);
            z = PivotXYZ(z);

            l = Mathf.Clamp01((116f * y - 16f) / 100f);
            a = Mathf.Clamp01(((500f * (x - y)) + 128f) / 255f);
            b = Mathf.Clamp01(((200f * (y - z)) + 128f) / 255f);
        }

        public static Color LABToRGB(float l, float a, float b)
        {
            float y = ((Mathf.Clamp01(l) * 100f) + 16f) / 116f;
            float x = (Mathf.Clamp01(a) * 255f - 128f) / 500f + y;
            float z = y - (Mathf.Clamp01(b) * 255f - 128f) / 200f;

            x = InversePivotXYZ(x) * 0.95047f;
            y = InversePivotXYZ(y) * 1.00000f;
            z = InversePivotXYZ(z) * 1.08883f;

            float r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
            float g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
            float blue = x * 0.0557f + y * -0.2040f + z * 1.0570f;

            return new Color(Mathf.Clamp01(InversePivotRGB(r)), Mathf.Clamp01(InversePivotRGB(g)), Mathf.Clamp01(InversePivotRGB(blue)), 1f);
        }

        private static float PivotRGB(float n)
        {
            n = Mathf.Clamp01(n);
            return n > 0.04045f ? Mathf.Pow((n + 0.055f) / 1.055f, 2.4f) : n / 12.92f;
        }

        private static float InversePivotRGB(float n)
        {
            return n > 0.0031308f ? 1.055f * Mathf.Pow(n, 1f / 2.4f) - 0.055f : 12.92f * n;
        }

        private static float PivotXYZ(float n)
        {
            return n > 0.008856f ? Mathf.Pow(n, 1f / 3f) : (7.787f * n) + 16f / 116f;
        }

        private static float InversePivotXYZ(float n)
        {
            float n3 = n * n * n;
            return n3 > 0.008856f ? n3 : (n - 16f / 116f) / 7.787f;
        }
    }

}