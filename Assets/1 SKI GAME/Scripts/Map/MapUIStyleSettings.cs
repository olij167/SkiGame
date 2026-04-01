using UnityEngine;

namespace SkiGame.Map.UI
{
    [CreateAssetMenu(menuName = "SkiGame/Map/Map UI Style Settings", fileName = "MapUIStyleSettings")]
    public sealed class MapUIStyleSettings : ScriptableObject
    {
        [Header("Colour Sources")]
        [Tooltip("If enabled, built-in POI markers use colours from the POI registry/category definitions instead of relying purely on map-side defaults.")]
        public bool usePOIRegistryMarkerColors = true;

        [Header("Markers")]
        [Min(1f)]
        [Tooltip("Base diameter in screen pixels for normal map markers.")]
        public float markerSize = 14f;

        [Min(1f)]
        [Tooltip("Diameter in screen pixels for the currently selected marker.")]
        public float markerSizeSelected = 18f;

        [Min(0f)]
        [Tooltip("Outline thickness in screen pixels applied to map markers.")]
        public float markerBorderWidth = 2f;

        [Tooltip("Default outline colour used for normal map markers.")]
        public Color markerBorderColor = new Color(0f, 0f, 0f, 0.65f);

        [Header("Player Marker")]
        [Min(1f)]
        [Tooltip("Diameter in screen pixels for the player marker.")]
        public float playerMarkerSize = 14f;

        [Tooltip("Fill colour used for the player marker.")]
        public Color playerMarkerColor = new Color(0.25f, 0.90f, 1f, 0.95f);

        [Min(0f)]
        [Tooltip("Outline thickness in screen pixels for the player marker.")]
        public float playerBorderWidth = 2f;

        [Tooltip("Outline colour used for the player marker.")]
        public Color playerBorderColor = new Color(0f, 0f, 0f, 0.65f);

        [Header("Waypoint Markers")]
        [Min(1f)]
        [Tooltip("Base diameter in screen pixels for standalone waypoint markers.")]
        public float waypointMarkerSize = 16f;

        [Min(1f)]
        [Tooltip("Diameter in screen pixels for the currently selected standalone waypoint marker.")]
        public float waypointMarkerSizeSelected = 20f;

        [Min(0f)]
        [Tooltip("Outline thickness in screen pixels applied to standalone waypoint markers.")]
        public float waypointMarkerBorderWidth = 2f;

        [Min(0f)]
        [Tooltip("Outline thickness in screen pixels applied to the active standalone waypoint marker.")]
        public float waypointMarkerBorderWidthActive = 2f;

        [Tooltip("Default outline colour used for standalone waypoint markers.")]
        public Color waypointMarkerBorderColor = new Color(0f, 0f, 0f, 0.70f);

        [Tooltip("Outline colour used for the active standalone waypoint marker.")]
        public Color waypointMarkerBorderColorActive = Color.white;

        [Header("Polylines (screen px)")]
        [Min(0.1f)]
        [Tooltip("Base on-screen thickness for ski run polylines.")]
        public float runWidthPx = 2.8f;

        [Min(0.1f)]
        [Tooltip("Base on-screen thickness for ski lift polylines.")]
        public float liftWidthPx = 2.1f;

        [Min(1f)]
        [Tooltip("Multiplier applied to polyline thickness when a run or lift is selected.")]
        public float selectedWidthMultiplier = 1.35f;

        [Min(0f)]
        [Tooltip("Extra outline thickness added around normal polylines.")]
        public float outlineExtraPx = 2.0f;

        [Min(0f)]
        [Tooltip("Extra outline thickness added around selected polylines.")]
        public float outlineExtraSelectedPx = 3.0f;

        [Tooltip("Outline colour used behind normal polylines.")]
        public Color outlineColor = new Color(0f, 0f, 0f, 0.35f);

        [Tooltip("Outline colour used behind selected polylines.")]
        public Color outlineColorSelected = new Color(0f, 0f, 0f, 0.55f);

        [Header("Polylines (zoom response)")]
        [Range(0f, 1f)]
        [Tooltip("Controls how much polyline thickness responds to zoom. 0 keeps a near-constant screen width; 1 scales more directly with zoom.")]
        public float polylineWidthZoomExponent = 0.35f;

        [Min(0.1f)]
        [Tooltip("Minimum multiplier allowed after zoom-response scaling is applied to polyline thickness.")]
        public float polylineWidthZoomMinMul = 0.70f;

        [Min(0.1f)]
        [Tooltip("Maximum multiplier allowed after zoom-response scaling is applied to polyline thickness.")]
        public float polylineWidthZoomMaxMul = 1.60f;

        [Header("Labels (default)")]
        [Min(6)]
        [Tooltip("Base font size for normal map labels.")]
        public int labelFontSize = 12;

        [Tooltip("Font style used for normal map labels.")]
        public FontStyle labelFontStyle = FontStyle.Bold;

        [Tooltip("Text colour used for normal map labels.")]
        public Color labelColor = new Color(1f, 1f, 1f, 0.95f);

        [Header("Labels (selected)")]
        [Min(6)]
        [Tooltip("Base font size for selected map labels.")]
        public int labelFontSizeSelected = 13;

        [Tooltip("Font style used for selected map labels.")]
        public FontStyle labelFontStyleSelected = FontStyle.Bold;

        [Tooltip("Text colour used for selected map labels.")]
        public Color labelColorSelected = new Color(1f, 1f, 1f, 1f);

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
        [Tooltip("0 = labels scale more with the map. 1 = labels try to stay closer to a constant on-screen size.")]
        public float labelZoomCompensation = 0.65f;

        [Min(6)]
        [Tooltip("Minimum clamped font size that map labels may resolve to after zoom compensation.")]
        public int labelFontMin = 8;

        [Min(6)]
        [Tooltip("Maximum clamped font size that map labels may resolve to after zoom compensation.")]
        public int labelFontMax = 18;

        [Header("Label Plates")]
        [Range(0f, 1f)]
        [Tooltip("Alpha applied to the background plate/pill rendered behind labels.")]
        public float labelPlateAlpha = 0.78f;

        [Min(0f)]
        [Tooltip("Width in pixels of the accent stripe shown on label plates.")]
        public float labelAccentStripeWidth = 3f;

        [Range(0f, 1f)]
        [Tooltip("Opacity applied to labels when they are forced into a cluttered fallback placement.")]
        public float labelClutteredOpacity = 0.65f;
    }
}