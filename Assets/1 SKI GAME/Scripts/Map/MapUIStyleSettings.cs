using System;
using System.Collections.Generic;
using UnityEngine;
using SkiGame.POI;

namespace SkiGame.Map.UI
{
    [CreateAssetMenu(menuName = "SkiGame/Map/Map UI Style Settings", fileName = "MapUIStyleSettings")]
    public sealed partial class MapUIStyleSettings : ScriptableObject
    {
        public enum MapLabelDisplayMode
        {
            Always = 0,
            Contextual = 1,
            SelectedOnly = 2
        }

        public enum MapRevealDirection
        {
            Always = 0,
            ZoomInReveal = 1,
            ZoomOutReveal = 2
        }

        public enum RunLabelAnchorMode
        {
            PolylineMidpoint = 0,
            LinkedMarker = 1
        }

        public enum MapElementSemantic
        {
            Any = 0,
            Region = 1,
            SkiRun = 10,
            SkiLift = 20,
            Resort = 30,
            Shop = 40,
            Kiosk = 50,
            Service = 60,
            Landmark = 70,
            Race = 80,
            Medical = 90,
            Vehicle = 100,
            CustomPoi = 110,
            GenericPoi = 120
        }

        [Serializable]
        public struct MapMarkerVisualStyle
        {
            [Min(1f)] public float size;
            [Min(1f)] public float selectedSize;
            [Min(0f)] public float borderWidth;
            [Min(0f)] public float activeBorderWidth;
            public Color borderColor;
            public Color activeBorderColor;
            public Color fillColor;
            public Sprite sprite;
            public float spriteRotationOffsetDegrees;
        }

        [Serializable]
        public struct MapArrowVisualStyle
        {
            [Min(1f)] public float size;
            public Sprite sprite;
            public float spriteRotationOffsetDegrees;
        }

        [Serializable]
        public struct MapLabelVisualStyle
        {
            [Min(6)] public int fontSize;
            [Min(6)] public int selectedFontSize;
            public FontStyle fontStyle;
            public FontStyle selectedFontStyle;
            public Color color;
            public Color selectedColor;
            [Range(0f, 1f)] public float zoomCompensation;
            [Min(6)] public int fontMin;
            [Min(6)] public int fontMax;
            [Range(0f, 1f)] public float plateAlpha;
            [Min(0f)] public float accentStripeWidth;
            [Range(0f, 1f)] public float clutteredOpacity;
            [Min(0f)] public float paddingX;
            [Min(0f)] public float paddingY;
            public float offsetY;
            [Min(0f)] public float cornerRadius;
        }

        [Serializable]
        public struct MapLineVisualStyle
        {
            [Min(0.1f)] public float widthPx;
            [Min(1f)] public float selectedWidthMultiplier;
            [Min(0f)] public float outlineExtraPx;
            [Min(0f)] public float outlineExtraSelectedPx;
            public Color outlineColor;
            public Color outlineColorSelected;
            [Range(0f, 1f)] public float widthZoomExponent;
            [Min(0.1f)] public float widthZoomMinMul;
            [Min(0.1f)] public float widthZoomMaxMul;
        }

        [Serializable]
        public struct MapAreaVisualStyle
        {
            [Min(0f)] public float outlineWidthPx;
            [Min(0f)] public float outlineWidthSelectedPx;
            [Range(0f, 1f)] public float fillOpacity;
            [Range(0f, 1f)] public float fillOpacitySelected;
            [Range(0f, 1f)] public float outlineOpacity;
            [Range(0f, 1f)] public float outlineOpacitySelected;
        }

        [Serializable]
        public struct MapLabelStyleProfile
        {
            public string id;
            public string displayName;

            [Header("Behavior")]
            public MapLabelDisplayMode labelDisplayMode;
            public MapRevealDirection revealDirection;
            [Min(0f)] public float fadeStartZoom;
            [Min(0f)] public float fadeEndZoom;
            public int labelPriority;
            [Min(0)] public int maxContextCount;

            [Header("Visuals")]
            public MapLabelVisualStyle style;
        }

        [Serializable]
        public struct MapElementVisibilityRule
        {
            [HideInInspector] public string elementName;

            public MapElementSemantic semantic;
            public POIType poiType;
            public POICategory poiCategory;
            public bool matchAnySemantic;
            public bool matchAnyType;
            public bool matchAnyCategory;

            [Header("Marker Behavior")]
            public MapLabelDisplayMode markerDisplayMode;

            [Header("Label Mapping")]
            public string labelStyleId;
            [Min(0f)] public float labelSizeMultiplier;

            [Header("Marker Overrides")]
            public bool overrideMarkerSprite;
            public Sprite markerSprite;
            public bool overrideMarkerSizeMultiplier;
            public float markerSizeMultiplier;
            public bool overrideMarkerDefaultColor;
            public Color markerDefaultColor;
            public bool markerColorIsFallbackOnly;
        }

        public event Action<MapUIStyleSettings> RuntimeStyleChanged;

        [SerializeField, HideInInspector] private int revision;
        [SerializeField, HideInInspector] private int dataVersion;
        public int Revision => revision;

        private const int CurrentDataVersion = 2;

        [Header("Colour Sources")]
        public bool usePOIRegistryMarkerColors = true;

        [Header("Markers")]
        public MapMarkerVisualStyle poiMarkers = CreateDefaultPoiMarkerStyle();
        public MapMarkerVisualStyle waypointMarkers = CreateDefaultWaypointMarkerStyle();
        public MapMarkerVisualStyle playerMarker = CreateDefaultPlayerMarkerStyle();
        public MapMarkerVisualStyle legendMarkers = CreateDefaultLegendMarkerStyle();
        public MapArrowVisualStyle navigationArrows = CreateDefaultNavigationArrowStyle();

        [Header("Labels")]
        public List<MapLabelStyleProfile> standardLabelStyles = new();

        [Header("Lines")]
        public MapLineVisualStyle poiPolylines = CreateDefaultPolylineStyle();
        public MapLineVisualStyle trailLine = CreateDefaultTrailLineStyle();

        [Header("Areas")]
        public MapAreaVisualStyle runAreas = CreateDefaultRunAreaStyle();
        public MapAreaVisualStyle regions = CreateDefaultRegionAreaStyle();

        [Header("Visibility Rules")]
        public RunLabelAnchorMode runLabelAnchorMode = RunLabelAnchorMode.PolylineMidpoint;
        public List<MapElementVisibilityRule> visibilityRules = new();

        public MapMarkerVisualStyle GetPoiMarkerStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeMarkerStyle(
                poiMarkers,
                CreateDefaultPoiMarkerStyle());
        }

        public MapMarkerVisualStyle GetWaypointMarkerStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeMarkerStyle(
                waypointMarkers,
                CreateDefaultWaypointMarkerStyle());
        }

        public MapMarkerVisualStyle GetPlayerMarkerStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeMarkerStyle(
                playerMarker,
                CreateDefaultPlayerMarkerStyle());
        }

        public MapMarkerVisualStyle GetLegendMarkerStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeMarkerStyle(
                legendMarkers,
                CreateDefaultLegendMarkerStyle());
        }

        public MapArrowVisualStyle GetNavigationArrowStyle()
        {
            UpgradeSerializedDataIfNeeded();

            MapArrowVisualStyle style = navigationArrows;
            MapArrowVisualStyle fallback = CreateDefaultNavigationArrowStyle();

            style.size = style.size > 0f ? style.size : fallback.size;
            if (style.sprite == null)
                style.sprite = fallback.sprite;

            return style;
        }

        public MapLabelVisualStyle GetStandardLabelStyle() => GetFallbackStandardLabelStyle();

       
        public MapLineVisualStyle GetPoiPolylineStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeLineStyle(
                poiPolylines,
                CreateDefaultPolylineStyle());
        }

        public MapLineVisualStyle GetTrailLineStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeLineStyle(
                trailLine,
                CreateDefaultTrailLineStyle());
        }

        public MapAreaVisualStyle GetRunAreaStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeAreaStyle(
                runAreas,
                CreateDefaultRunAreaStyle());
        }

        public MapAreaVisualStyle GetRegionAreaStyle()
        {
            UpgradeSerializedDataIfNeeded();
            return SanitizeAreaStyle(
                regions,
                CreateDefaultRegionAreaStyle());
        }

        public MapLabelStyleProfile GetStandardLabelProfileById(string id)
        {
            UpgradeSerializedDataIfNeeded();
            EnsureDefaultStandardLabelProfiles();

            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int i = 0; i < standardLabelStyles.Count; i++)
                {
                    if (string.Equals(standardLabelStyles[i].id, id, StringComparison.OrdinalIgnoreCase))
                        return SanitizeLabelProfile(standardLabelStyles[i]);
                }
            }

            return GetFallbackStandardLabelProfile();
        }

        public MapLabelStyleProfile GetFallbackStandardLabelProfile()
        {
            UpgradeSerializedDataIfNeeded();
            EnsureDefaultStandardLabelProfiles();
            return SanitizeLabelProfile(standardLabelStyles[0]);
        }

        public MapLabelVisualStyle GetStandardLabelStyleById(string id)
        {
            return GetStandardLabelProfileById(id).style;
        }

        public MapLabelVisualStyle GetFallbackStandardLabelStyle()
        {
            return GetFallbackStandardLabelProfile().style;
        }

        public bool TryResolveVisibilityRule(MapElementSemantic semantic, POIType poiType, POICategory poiCategory, out MapElementVisibilityRule rule)
        {
            return TryFindBestVisibilityRule(semantic, poiType, poiCategory, out rule);
        }

        public bool TryFindBestVisibilityRule(POIType type, POICategory category, out MapElementVisibilityRule rule)
        {
            return TryFindBestVisibilityRule(ResolveSemantic(type, category), type, category, out rule);
        }

        public bool TryFindBestVisibilityRule(MapElementSemantic semantic, POIType type, POICategory category, out MapElementVisibilityRule rule)
        {
            UpgradeSerializedDataIfNeeded();

            rule = default;
            if (visibilityRules == null || visibilityRules.Count == 0)
                return false;

            bool found = false;
            int bestScore = int.MinValue;

            for (int i = 0; i < visibilityRules.Count; i++)
            {
                MapElementVisibilityRule candidate = visibilityRules[i];
                if (!MatchesRule(candidate, semantic, type, category))
                    continue;

                int score = GetRuleSpecificityScore(candidate, semantic, type, category);
                if (!found || score > bestScore)
                {
                    bestScore = score;
                    rule = candidate;
                    found = true;
                }
            }

            return found;
        }

        public void NotifyRuntimeStyleChanged()
        {
            revision++;
            RuntimeStyleChanged?.Invoke(this);
        }

        private void OnEnable()
        {
            UpgradeSerializedDataIfNeeded();
            SyncVisibilityRuleElementNames();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpgradeSerializedDataIfNeeded();
            SyncVisibilityRuleElementNames();
            NotifyRuntimeStyleChanged();
        }
#endif

        private void UpgradeSerializedDataIfNeeded()
        {
            if (dataVersion >= CurrentDataVersion)
                return;

            poiMarkers = SanitizeMarkerStyle(poiMarkers, CreateDefaultPoiMarkerStyle());
            waypointMarkers = SanitizeMarkerStyle(waypointMarkers, CreateDefaultWaypointMarkerStyle());
            playerMarker = SanitizeMarkerStyle(playerMarker, CreateDefaultPlayerMarkerStyle());
            legendMarkers = SanitizeMarkerStyle(legendMarkers, CreateDefaultLegendMarkerStyle());

            navigationArrows.size = navigationArrows.size > 0f
                ? navigationArrows.size
                : CreateDefaultNavigationArrowStyle().size;

            EnsureDefaultStandardLabelProfiles();

            if (standardLabelStyles != null)
            {
                for (int i = 0; i < standardLabelStyles.Count; i++)
                {
                    MapLabelStyleProfile profile = standardLabelStyles[i];
                    standardLabelStyles[i] = SanitizeLabelProfile(profile);
                }
            }

            poiPolylines = SanitizeLineStyle(poiPolylines, CreateDefaultPolylineStyle());
            trailLine = SanitizeLineStyle(trailLine, CreateDefaultTrailLineStyle());
            runAreas = SanitizeAreaStyle(runAreas, CreateDefaultRunAreaStyle());
            regions = SanitizeAreaStyle(regions, CreateDefaultRegionAreaStyle());

            if (visibilityRules == null || visibilityRules.Count == 0)
            {
                visibilityRules = CreateDefaultVisibilityRules();
            }
            else
            {
                for (int i = 0; i < visibilityRules.Count; i++)
                {
                    MapElementVisibilityRule rule = visibilityRules[i];

                    if (rule.markerSizeMultiplier <= 0f)
                        rule.markerSizeMultiplier = 1f;

                    if (rule.labelSizeMultiplier <= 0f)
                        rule.labelSizeMultiplier = 1f;

                    if (string.IsNullOrWhiteSpace(rule.labelStyleId))
                        rule.labelStyleId = "standard";

                    rule.elementName = GetSemanticDisplayName(rule.semantic);
                    visibilityRules[i] = rule;
                }
            }

            SyncVisibilityRuleElementNames();
            dataVersion = CurrentDataVersion;
        }

        private void SyncVisibilityRuleElementNames()
        {
            if (visibilityRules == null)
                return;

            for (int i = 0; i < visibilityRules.Count; i++)
            {
                MapElementVisibilityRule rule = visibilityRules[i];
                string semanticName = GetSemanticDisplayName(rule.semantic);

                if (!string.Equals(rule.elementName, semanticName, StringComparison.Ordinal))
                {
                    rule.elementName = semanticName;
                    visibilityRules[i] = rule;
                }
            }
        }

        private static string GetSemanticDisplayName(MapElementSemantic semantic)
        {
            return semantic switch
            {
                MapElementSemantic.Any => "Any",
                MapElementSemantic.Region => "Region",
                MapElementSemantic.SkiRun => "Ski Run",
                MapElementSemantic.SkiLift => "Ski Lift",
                MapElementSemantic.Resort => "Resort",
                MapElementSemantic.Shop => "Shop",
                MapElementSemantic.Kiosk => "Kiosk",
                MapElementSemantic.Service => "Service",
                MapElementSemantic.Landmark => "Landmark",
                MapElementSemantic.Race => "Race",
                MapElementSemantic.Medical => "Medical",
                MapElementSemantic.Vehicle => "Vehicle",
                MapElementSemantic.CustomPoi => "Custom POI",
                MapElementSemantic.GenericPoi => "Generic POI",
                _ => semantic.ToString()
            };
        }

        private void EnsureDefaultStandardLabelProfiles()
        {
            if (standardLabelStyles != null && standardLabelStyles.Count > 0)
                return;

            standardLabelStyles = new List<MapLabelStyleProfile>
    {
        new MapLabelStyleProfile
        {
            id = "minor",
            displayName = "Minor",
            labelDisplayMode = MapLabelDisplayMode.SelectedOnly,
            revealDirection = MapRevealDirection.ZoomInReveal,
            fadeStartZoom = 2.05f,
            fadeEndZoom = 2.60f,
            labelPriority = 12,
            maxContextCount = 4,
            style = new MapLabelVisualStyle
            {
                fontSize = 10,
                selectedFontSize = 11,
                fontStyle = FontStyle.Bold,
                selectedFontStyle = FontStyle.Bold,
                color = new Color(1f, 1f, 1f, 0.95f),
                selectedColor = Color.white,
                zoomCompensation = 0.65f,
                fontMin = 8,
                fontMax = 14,
                plateAlpha = 0.76f,
                accentStripeWidth = 2f,
                clutteredOpacity = 0.60f,
                paddingX = 5f,
                paddingY = 2f,
                offsetY = 18f,
                cornerRadius = 5f,
            }
        },
        new MapLabelStyleProfile
        {
            id = "standard",
            displayName = "Standard",
            labelDisplayMode = MapLabelDisplayMode.Contextual,
            revealDirection = MapRevealDirection.ZoomInReveal,
            fadeStartZoom = 1.85f,
            fadeEndZoom = 2.40f,
            labelPriority = 8,
            maxContextCount = 8,
            style = new MapLabelVisualStyle
            {
                fontSize = 12,
                selectedFontSize = 13,
                fontStyle = FontStyle.Bold,
                selectedFontStyle = FontStyle.Bold,
                color = new Color(1f, 1f, 1f, 0.96f),
                selectedColor = Color.white,
                zoomCompensation = 0.65f,
                fontMin = 8,
                fontMax = 18,
                plateAlpha = 0.78f,
                accentStripeWidth = 3f,
                clutteredOpacity = 0.65f,
                paddingX = 6f,
                paddingY = 2.5f,
                offsetY = 18f,
                cornerRadius = 6f,
            }
        },
        new MapLabelStyleProfile
        {
            id = "major",
            displayName = "Major",
            labelDisplayMode = MapLabelDisplayMode.Contextual,
            revealDirection = MapRevealDirection.ZoomInReveal,
            fadeStartZoom = 1.20f,
            fadeEndZoom = 1.90f,
            labelPriority = 4,
            maxContextCount = 6,
            style = new MapLabelVisualStyle
            {
                fontSize = 14,
                selectedFontSize = 15,
                fontStyle = FontStyle.Bold,
                selectedFontStyle = FontStyle.Bold,
                color = new Color(1f, 1f, 1f, 0.98f),
                selectedColor = Color.white,
                zoomCompensation = 0.62f,
                fontMin = 10,
                fontMax = 22,
                plateAlpha = 0.82f,
                accentStripeWidth = 4f,
                clutteredOpacity = 0.72f,
                paddingX = 8f,
                paddingY = 3f,
                offsetY = 20f,
                cornerRadius = 7f,
            }
        },
        new MapLabelStyleProfile
        {
            id = "region-major",
            displayName = "Region Major",
            labelDisplayMode = MapLabelDisplayMode.Contextual,
            revealDirection = MapRevealDirection.ZoomOutReveal,
            fadeStartZoom = 1.10f,
            fadeEndZoom = 2.20f,
            labelPriority = 2,
            maxContextCount = 12,
            style = new MapLabelVisualStyle
            {
                fontSize = 14,
                selectedFontSize = 15,
                fontStyle = FontStyle.Bold,
                selectedFontStyle = FontStyle.Bold,
                color = new Color(1f, 1f, 1f, 0.98f),
                selectedColor = Color.white,
                zoomCompensation = 0.62f,
                fontMin = 10,
                fontMax = 22,
                plateAlpha = 0.82f,
                accentStripeWidth = 4f,
                clutteredOpacity = 0.72f,
                paddingX = 8f,
                paddingY = 3f,
                offsetY = 20f,
                cornerRadius = 7f,
            }
        },
        new MapLabelStyleProfile
        {
            id = "waypoint-editable",
            displayName = "Waypoint Editable",
            labelDisplayMode = MapLabelDisplayMode.Always,
            revealDirection = MapRevealDirection.Always,
            fadeStartZoom = 0f,
            fadeEndZoom = 0f,
            labelPriority = 1,
            maxContextCount = 0,
            style = new MapLabelVisualStyle
            {
                fontSize = 12,
                selectedFontSize = 13,
                fontStyle = FontStyle.Bold,
                selectedFontStyle = FontStyle.Bold,
                color = new Color(1f, 1f, 1f, 0.98f),
                selectedColor = Color.white,
                zoomCompensation = 0.65f,
                fontMin = 8,
                fontMax = 18,
                plateAlpha = 0.82f,
                accentStripeWidth = 3f,
                clutteredOpacity = 0.80f,
                paddingX = 6f,
                paddingY = 2.5f,
                offsetY = 30f,
                cornerRadius = 6f,
            }
        }
    };
        }

        private static bool MatchesRule(MapElementVisibilityRule rule, MapElementSemantic semantic, POIType poiType, POICategory poiCategory)
        {
            if (!rule.matchAnySemantic && rule.semantic != semantic)
                return false;

            if (!rule.matchAnyType && rule.poiType != poiType)
                return false;

            if (!rule.matchAnyCategory && rule.poiCategory != poiCategory)
                return false;

            return true;
        }

        private static int GetRuleSpecificityScore(MapElementVisibilityRule rule, MapElementSemantic semantic, POIType poiType, POICategory poiCategory)
        {
            int score = 0;

            if (!rule.matchAnySemantic && rule.semantic == semantic)
                score += 100;

            if (!rule.matchAnyType && rule.poiType == poiType)
                score += 10;

            if (!rule.matchAnyCategory && rule.poiCategory == poiCategory)
                score += 5;

            return score;
        }

        public static MapElementSemantic ResolveSemantic(POIType type, POICategory category)
        {
            if (type == POIType.SkiRun)
                return MapElementSemantic.SkiRun;
            if (type == POIType.SkiLift)
                return MapElementSemantic.SkiLift;

            return category switch
            {
                POICategory.Resort => MapElementSemantic.Resort,
                POICategory.Shop => MapElementSemantic.Shop,
                POICategory.Kiosk => MapElementSemantic.Kiosk,
                POICategory.Service => MapElementSemantic.Service,
                POICategory.Landmark => MapElementSemantic.Landmark,
                POICategory.Race => MapElementSemantic.Race,
                POICategory.Medical => MapElementSemantic.Medical,
                POICategory.Vehicle => MapElementSemantic.Vehicle,
                POICategory.Custom => MapElementSemantic.CustomPoi,
                _ => MapElementSemantic.GenericPoi
            };
        }

        private MapLabelVisualStyle SanitizeStandardProfile(MapLabelVisualStyle style)
        {
            return SanitizeLabelStyle(style, CreateDefaultStandardLabelStyle());
        }

        private MapLabelStyleProfile SanitizeLabelProfile(MapLabelStyleProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.id))
                profile.id = "standard";

            if (string.IsNullOrWhiteSpace(profile.displayName))
                profile.displayName = profile.id;

            profile.fadeEndZoom = Mathf.Max(profile.fadeEndZoom, profile.fadeStartZoom);
            profile.maxContextCount = Mathf.Max(0, profile.maxContextCount);
            profile.style = SanitizeLabelStyle(profile.style, CreateDefaultStandardLabelStyle());

            return profile;
        }

        private static MapMarkerVisualStyle SanitizeMarkerStyle(MapMarkerVisualStyle style, MapMarkerVisualStyle fallback)
        {
            style.size = style.size > 0f ? style.size : fallback.size;
            style.selectedSize = style.selectedSize > 0f ? style.selectedSize : Mathf.Max(style.size, fallback.selectedSize);
            style.borderWidth = style.borderWidth > 0f ? style.borderWidth : fallback.borderWidth;
            style.activeBorderWidth = style.activeBorderWidth > 0f ? style.activeBorderWidth : fallback.activeBorderWidth;

            if (style.borderColor.a <= 0f)
                style.borderColor = fallback.borderColor;
            if (style.activeBorderColor.a <= 0f)
                style.activeBorderColor = fallback.activeBorderColor;
            if (style.fillColor.a <= 0f)
                style.fillColor = fallback.fillColor;
            if (style.sprite == null)
                style.sprite = fallback.sprite;

            return style;
        }

        private static MapLabelVisualStyle SanitizeLabelStyle(MapLabelVisualStyle style, MapLabelVisualStyle fallback)
        {
            bool missingBase = style.fontSize <= 0;
            bool missingSelected = style.selectedFontSize <= 0;

            style.fontSize = Mathf.Max(6, missingBase ? fallback.fontSize : style.fontSize);
            style.selectedFontSize = Mathf.Max(6, missingSelected ? fallback.selectedFontSize : style.selectedFontSize);

            if (missingBase)
                style.fontStyle = fallback.fontStyle;
            if (missingSelected)
                style.selectedFontStyle = fallback.selectedFontStyle;

            if (style.color.a <= 0f)
                style.color = fallback.color;
            if (style.selectedColor.a <= 0f)
                style.selectedColor = fallback.selectedColor;

            style.zoomCompensation = style.zoomCompensation > 0f ? style.zoomCompensation : fallback.zoomCompensation;
            style.fontMin = Mathf.Max(6, style.fontMin > 0 ? style.fontMin : fallback.fontMin);
            style.fontMax = Mathf.Max(style.fontMin, style.fontMax > 0 ? style.fontMax : fallback.fontMax);
            style.plateAlpha = style.plateAlpha > 0f ? style.plateAlpha : fallback.plateAlpha;
            style.accentStripeWidth = style.accentStripeWidth > 0f ? style.accentStripeWidth : fallback.accentStripeWidth;
            style.clutteredOpacity = style.clutteredOpacity > 0f ? style.clutteredOpacity : fallback.clutteredOpacity;
            style.paddingX = style.paddingX > 0f ? style.paddingX : fallback.paddingX;
            style.paddingY = style.paddingY > 0f ? style.paddingY : fallback.paddingY;

            if (Mathf.Approximately(style.offsetY, 0f))
                style.offsetY = fallback.offsetY;

            style.cornerRadius = style.cornerRadius > 0f ? style.cornerRadius : fallback.cornerRadius;

            return style;
        }

        private static MapLineVisualStyle SanitizeLineStyle(MapLineVisualStyle style, MapLineVisualStyle fallback)
        {
            style.widthPx = style.widthPx > 0f ? style.widthPx : fallback.widthPx;
            style.selectedWidthMultiplier = style.selectedWidthMultiplier > 0f ? style.selectedWidthMultiplier : fallback.selectedWidthMultiplier;
            style.outlineExtraPx = style.outlineExtraPx > 0f ? style.outlineExtraPx : fallback.outlineExtraPx;
            style.outlineExtraSelectedPx = style.outlineExtraSelectedPx > 0f ? style.outlineExtraSelectedPx : fallback.outlineExtraSelectedPx;

            if (style.outlineColor.a <= 0f)
                style.outlineColor = fallback.outlineColor;
            if (style.outlineColorSelected.a <= 0f)
                style.outlineColorSelected = fallback.outlineColorSelected;

            style.widthZoomExponent = style.widthZoomExponent > 0f ? style.widthZoomExponent : fallback.widthZoomExponent;
            style.widthZoomMinMul = style.widthZoomMinMul > 0f ? style.widthZoomMinMul : fallback.widthZoomMinMul;
            style.widthZoomMaxMul = style.widthZoomMaxMul > 0f ? style.widthZoomMaxMul : fallback.widthZoomMaxMul;

            return style;
        }

        private static MapAreaVisualStyle SanitizeAreaStyle(MapAreaVisualStyle style, MapAreaVisualStyle fallback)
        {
            style.outlineWidthPx = style.outlineWidthPx > 0f ? style.outlineWidthPx : fallback.outlineWidthPx;
            style.outlineWidthSelectedPx = style.outlineWidthSelectedPx > 0f ? style.outlineWidthSelectedPx : fallback.outlineWidthSelectedPx;
            style.fillOpacity = style.fillOpacity > 0f ? style.fillOpacity : fallback.fillOpacity;
            style.fillOpacitySelected = style.fillOpacitySelected > 0f ? style.fillOpacitySelected : fallback.fillOpacitySelected;
            style.outlineOpacity = style.outlineOpacity > 0f ? style.outlineOpacity : fallback.outlineOpacity;
            style.outlineOpacitySelected = style.outlineOpacitySelected > 0f ? style.outlineOpacitySelected : fallback.outlineOpacitySelected;

            return style;
        }

        private static List<MapElementVisibilityRule> CreateDefaultVisibilityRules()
        {
            return new List<MapElementVisibilityRule>
    {
        CreateRule(MapElementSemantic.Region,   POIType.Unknown, POICategory.None,    false, true,  true,  MapLabelDisplayMode.Contextual, "region-major"),
        CreateRule(MapElementSemantic.SkiRun,   POIType.SkiRun,  POICategory.None,    false, false, true,  MapLabelDisplayMode.Contextual, "standard"),
        CreateRule(MapElementSemantic.SkiLift,  POIType.SkiLift, POICategory.None,    false, false, true,  MapLabelDisplayMode.Contextual, "standard"),
        CreateRule(MapElementSemantic.Resort,   POIType.Custom,  POICategory.Resort,  false, true,  false, MapLabelDisplayMode.Contextual, "major"),
        CreateRule(MapElementSemantic.Shop,     POIType.Custom,  POICategory.Shop,    false, true,  false, MapLabelDisplayMode.Contextual, "minor"),
        CreateRule(MapElementSemantic.Kiosk,    POIType.Custom,  POICategory.Kiosk,   false, true,  false, MapLabelDisplayMode.Contextual, "minor"),
        CreateRule(MapElementSemantic.Service,  POIType.Custom,  POICategory.Service, false, true,  false, MapLabelDisplayMode.Contextual, "minor"),
        CreateRule(MapElementSemantic.Landmark, POIType.Custom,  POICategory.Landmark,false, true,  false, MapLabelDisplayMode.Contextual, "standard"),
        CreateRule(MapElementSemantic.Race,     POIType.Custom,  POICategory.Race,    false, true,  false, MapLabelDisplayMode.Contextual, "standard"),
        CreateRule(MapElementSemantic.Medical,  POIType.Custom,  POICategory.Medical, false, true,  false, MapLabelDisplayMode.Contextual, "standard"),
        CreateRule(MapElementSemantic.Vehicle,  POIType.Custom,  POICategory.Vehicle, false, true,  false, MapLabelDisplayMode.Contextual, "minor"),
        CreateRule(MapElementSemantic.CustomPoi,POIType.Custom,  POICategory.Custom,  false, true,  false, MapLabelDisplayMode.Contextual, "minor"),
        CreateRule(MapElementSemantic.GenericPoi,POIType.Unknown,POICategory.None,    true,  true,  true,  MapLabelDisplayMode.Contextual, "minor")
    };
        }

        private static MapElementVisibilityRule CreateRule(
    MapElementSemantic semantic,
    POIType poiType,
    POICategory poiCategory,
    bool anySemantic,
    bool anyType,
    bool anyCategory,
    MapLabelDisplayMode markerDisplayMode,
    string labelStyleId,
    float labelSizeMultiplier = 1f)
        {
            return new MapElementVisibilityRule
            {
                elementName = GetSemanticDisplayName(semantic),
                semantic = semantic,
                poiType = poiType,
                poiCategory = poiCategory,
                matchAnySemantic = anySemantic,
                matchAnyType = anyType,
                matchAnyCategory = anyCategory,
                markerDisplayMode = markerDisplayMode,
                labelStyleId = string.IsNullOrWhiteSpace(labelStyleId) ? "standard" : labelStyleId,
                labelSizeMultiplier = Mathf.Max(0.1f, labelSizeMultiplier),
                markerSizeMultiplier = 1f,
                markerColorIsFallbackOnly = true
            };
        }

        private static MapMarkerVisualStyle CreateDefaultPoiMarkerStyle() => new MapMarkerVisualStyle
        {
            size = 14f,
            selectedSize = 18f,
            borderWidth = 2f,
            activeBorderWidth = 2f,
            borderColor = new Color(0f, 0f, 0f, 0.65f),
            activeBorderColor = new Color(0f, 0f, 0f, 0.65f),
            fillColor = Color.white
        };

        private static MapMarkerVisualStyle CreateDefaultWaypointMarkerStyle() => new MapMarkerVisualStyle
        {
            size = 16f,
            selectedSize = 20f,
            borderWidth = 2f,
            activeBorderWidth = 2f,
            borderColor = new Color(0f, 0f, 0f, 0.70f),
            activeBorderColor = Color.white,
            fillColor = Color.white
        };

        private static MapMarkerVisualStyle CreateDefaultPlayerMarkerStyle() => new MapMarkerVisualStyle
        {
            size = 14f,
            selectedSize = 14f,
            borderWidth = 2f,
            activeBorderWidth = 2f,
            borderColor = new Color(0f, 0f, 0f, 0.65f),
            activeBorderColor = new Color(0f, 0f, 0f, 0.65f),
            fillColor = new Color(0.25f, 0.90f, 1f, 0.95f)
        };

        private static MapMarkerVisualStyle CreateDefaultLegendMarkerStyle() => new MapMarkerVisualStyle
        {
            size = 14f,
            selectedSize = 14f,
            borderWidth = 2f,
            activeBorderWidth = 2f,
            borderColor = new Color(0f, 0f, 0f, 0.65f),
            activeBorderColor = new Color(0f, 0f, 0f, 0.65f),
            fillColor = Color.white
        };

        private static MapArrowVisualStyle CreateDefaultNavigationArrowStyle() => new MapArrowVisualStyle
        {
            size = 22f
        };

        private static MapLabelVisualStyle CreateDefaultStandardLabelStyle() => new MapLabelVisualStyle
        {
            fontSize = 12,
            selectedFontSize = 13,
            fontStyle = FontStyle.Bold,
            selectedFontStyle = FontStyle.Bold,
            color = new Color(1f, 1f, 1f, 0.95f),
            selectedColor = new Color(1f, 1f, 1f, 1f),
            zoomCompensation = 0.65f,
            fontMin = 8,
            fontMax = 18,
            plateAlpha = 0.78f,
            accentStripeWidth = 3f,
            clutteredOpacity = 0.65f,
            cornerRadius = 6f
        };

        private static MapLabelVisualStyle CreateDefaultWaypointLabelStyle()
        {
            MapLabelVisualStyle style = CreateDefaultStandardLabelStyle();
            style.fontSize = 10;
            style.selectedFontSize = 11;
            style.plateAlpha = 0.42f;
            style.accentStripeWidth = 2f;
            style.paddingX = 5f;
            style.paddingY = 2f;
            style.offsetY = 24f;
            style.cornerRadius = 5f;
            return style;
        }

        private static MapLabelVisualStyle CreateDefaultRegionLabelStyle() => CreateDefaultStandardLabelStyle();

        private static MapLineVisualStyle CreateDefaultPolylineStyle() => new MapLineVisualStyle
        {
            widthPx = 2.8f,
            selectedWidthMultiplier = 1.35f,
            outlineExtraPx = 2f,
            outlineExtraSelectedPx = 3f,
            outlineColor = new Color(0f, 0f, 0f, 0.35f),
            outlineColorSelected = new Color(0f, 0f, 0f, 0.55f),
            widthZoomExponent = 0.35f,
            widthZoomMinMul = 0.70f,
            widthZoomMaxMul = 1.60f
        };

        private static MapLineVisualStyle CreateDefaultTrailLineStyle() => new MapLineVisualStyle
        {
            widthPx = 2f,
            selectedWidthMultiplier = 1f,
            outlineExtraPx = 2f,
            outlineExtraSelectedPx = 2f,
            outlineColor = new Color(0f, 0f, 0f, 0.40f),
            outlineColorSelected = new Color(0f, 0f, 0f, 0.40f),
            widthZoomMinMul = 1f,
            widthZoomMaxMul = 1f
        };

        private static MapAreaVisualStyle CreateDefaultRunAreaStyle() => new MapAreaVisualStyle
        {
            outlineWidthPx = 1.8f,
            outlineWidthSelectedPx = 3.1f,
            fillOpacity = 0.24f,
            fillOpacitySelected = 0.42f,
            outlineOpacity = 0.92f,
            outlineOpacitySelected = 1f
        };

        private static MapAreaVisualStyle CreateDefaultRegionAreaStyle() => CreateDefaultRunAreaStyle();
    }
}
