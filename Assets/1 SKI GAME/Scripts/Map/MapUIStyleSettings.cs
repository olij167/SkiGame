using UnityEngine;

namespace SkiGame.Map.UI
{
    [CreateAssetMenu(menuName = "SkiGame/Map/Map UI Style Settings", fileName = "MapUIStyleSettings")]
    public sealed class MapUIStyleSettings : ScriptableObject
    {
        [Header("Colour Sources")]
        public bool usePOIRegistryMarkerColors = true;

        [Header("Markers")]
        [Min(1f)] public float markerSize = 14f;
        [Min(1f)] public float markerSizeSelected = 18f;

        [Min(0f)] public float markerBorderWidth = 2f;
        public Color markerBorderColor = new Color(0f, 0f, 0f, 0.65f);

        [Header("Player Marker")]
        [Min(1f)] public float playerMarkerSize = 14f;
        public Color playerMarkerColor = new Color(0.25f, 0.90f, 1f, 0.95f);

        [Min(0f)] public float playerBorderWidth = 2f;
        public Color playerBorderColor = new Color(0f, 0f, 0f, 0.65f);

        [Header("Polylines (screen px)")]
        [Min(0.1f)] public float runWidthPx = 2.8f;
        [Min(0.1f)] public float liftWidthPx = 2.1f;

        [Min(1f)] public float selectedWidthMultiplier = 1.35f;

        [Min(0f)] public float outlineExtraPx = 2.0f;
        [Min(0f)] public float outlineExtraSelectedPx = 3.0f;

        public Color outlineColor = new Color(0f, 0f, 0f, 0.35f);
        public Color outlineColorSelected = new Color(0f, 0f, 0f, 0.55f);

        [Header("Polylines (zoom response)")]
        [Range(0f, 1f)]
        public float polylineWidthZoomExponent = 0.35f; // 0 = constant screen px, 1 = linear with zoom

        [Min(0.1f)] public float polylineWidthZoomMinMul = 0.70f;
        [Min(0.1f)] public float polylineWidthZoomMaxMul = 1.60f;

        [Header("Labels (default)")]
        [Min(6)] public int labelFontSize = 12;
        public FontStyle labelFontStyle = FontStyle.Bold;
        public Color labelColor = new Color(1f, 1f, 1f, 0.95f);

        [Header("Labels (selected)")]
        [Min(6)] public int labelFontSizeSelected = 13;
        public FontStyle labelFontStyleSelected = FontStyle.Bold;
        public Color labelColorSelected = new Color(1f, 1f, 1f, 1f);

        // Legacy fields (kept for backwards compatibility; no longer used)
        [HideInInspector] public int markerLabelFontSize = 12;
        [HideInInspector] public FontStyle markerLabelFontStyle = FontStyle.Normal;
        [HideInInspector] public Color markerLabelColor = new Color(1f, 1f, 1f, 0.95f);

        [HideInInspector] public int markerLabelFontSizeSelected = 13;
        [HideInInspector] public FontStyle markerLabelFontStyleSelected = FontStyle.Bold;
        [HideInInspector] public Color markerLabelColorSelected = new Color(1f, 1f, 1f, 1f);

        [HideInInspector] public int polylineLabelFontSize = 12;
        [HideInInspector] public FontStyle polylineLabelFontStyle = FontStyle.Bold;
        [HideInInspector] public Color polylineLabelColor = new Color(1f, 1f, 1f, 0.95f);

        [HideInInspector] public int polylineLabelFontSizeSelected = 13;
        [HideInInspector] public FontStyle polylineLabelFontStyleSelected = FontStyle.Bold;
        [HideInInspector] public Color polylineLabelColorSelected = new Color(1f, 1f, 1f, 1f);

        [Header("Labels (zoom behavior)")]
        [Range(0f, 1f)]
        [Tooltip("0 = labels scale fully with map zoom. 1 = labels try to keep a roughly constant on-screen size.\n" +
         "Technically we compute: localFont = baseFont / (zoom^compensation).\n" +
         "This prevents huge labels when zoomed in, while still allowing subtle scaling.")]
        public float labelZoomCompensation = 0.65f;

        [Min(6)]
        [Tooltip("Clamp the computed per-label local font size (in UI Toolkit points).")]
        public int labelFontMin = 8;

        [Min(6)]
        [Tooltip("Clamp the computed per-label local font size (in UI Toolkit points).")]
        public int labelFontMax = 18;

        [Header("Label Plates")]
        [Range(0f, 1f)]
        [Tooltip("Background alpha for the label pill/plate (final color is set in USS).")]
        public float labelPlateAlpha = 0.78f;

        [Min(0f)]
        [Tooltip("Left accent stripe width (px) used to visually link a label to its line/marker color.")]
        public float labelAccentStripeWidth = 3f;

        [Range(0f, 1f)]
        [Tooltip("Opacity applied to labels that had to be placed via fallback (overlap unavoidable). Selected labels stay at 1.")]
        public float labelClutteredOpacity = 0.65f;

    }
}
