using UnityEngine;
using UnityEngine.UIElements;

namespace SkiGame.Progression
{
    internal static class MountainHudMapRootFactory
    {
        public static VisualElement BuildMiniMapRoot(string rootName, float size = 250f)
        {
            var root = BuildBaseRoot(rootName, interactive: false, includeControls: false, includeLayerBar: false);

            // Size the OUTER container, but style the actual viewport with the minimap class.
            root.style.width = size;
            root.style.height = size;
            root.style.minWidth = size;
            root.style.minHeight = size;
            root.style.flexGrow = 0;
            root.style.flexShrink = 0;

            var viewport = root.Q<VisualElement>("MapViewport");
            if (viewport != null)
            {
                viewport.AddToClassList("mini-map-viewport");
                viewport.style.width = size;
                viewport.style.height = size;
                viewport.style.minWidth = size;
                viewport.style.minHeight = size;
            }

            return root;
        }

        public static VisualElement BuildOverlayMapRoot(string rootName)
        {
            var root = BuildBaseRoot(rootName, interactive: true, includeControls: true, includeLayerBar: true);
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.style.flexGrow = 1;
            return root;
        }

        private static VisualElement BuildBaseRoot(
            string rootName,
            bool interactive,
            bool includeControls,
            bool includeLayerBar)
        {
            var root = new VisualElement { name = rootName };
            root.style.position = Position.Relative;
            root.style.overflow = Overflow.Hidden;

            var viewport = new VisualElement { name = "MapViewport" };
            viewport.AddToClassList("map-viewport");
            viewport.style.position = Position.Absolute;
            viewport.style.left = 0;
            viewport.style.top = 0;
            viewport.style.right = 0;
            viewport.style.bottom = 0;
            viewport.style.overflow = Overflow.Hidden;

            var content = new VisualElement { name = "MapContent" };
            content.AddToClassList("map-content");
            content.style.position = Position.Absolute;
            content.style.left = 0;
            content.style.top = 0;

            var bg = new VisualElement { name = "MapBackground" };
            bg.AddToClassList("map-background");

            var polys = new VisualElement { name = "MapPolylines" };
            polys.AddToClassList("map-polylines");

            var markers = new VisualElement { name = "MapMarkers" };
            markers.AddToClassList("map-markers");

            bg.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

            content.Add(bg);
            content.Add(polys);
            content.Add(markers);
            viewport.Add(content);

            var missing = new Label("Map unavailable") { name = "Lbl_MapMissing" };
            missing.AddToClassList("is-hidden");
            viewport.Add(missing);

            var resetBtn = new Button { name = "Btn_MapReset", text = "Reset" };
            var centerBtn = new Button { name = "Btn_MapCenterPlayer", text = "Me" };

            if (includeControls)
            {
                resetBtn.AddToClassList("map-button");
                resetBtn.AddToClassList("map-button-reset");

                centerBtn.AddToClassList("map-button");
                centerBtn.AddToClassList("map-button-center");
            }
            else
            {
                resetBtn.AddToClassList("is-hidden");
                centerBtn.AddToClassList("is-hidden");
            }

            viewport.Add(resetBtn);
            viewport.Add(centerBtn);

            var layerBar = new VisualElement { name = "MapLayerBar" };
            if (includeLayerBar)
            {
                layerBar.AddToClassList("map-layer-bar");

                var btnRuns = new Button { name = "Btn_MapLayerRuns", text = "Runs" };
                var btnLifts = new Button { name = "Btn_MapLayerLifts", text = "Lifts" };
                var btnPois = new Button { name = "Btn_MapLayerPOIs", text = "POIs" };

                btnRuns.AddToClassList("map-layer-button");
                btnRuns.AddToClassList("is-selected");

                btnLifts.AddToClassList("map-layer-button");
                btnLifts.AddToClassList("is-selected");

                btnPois.AddToClassList("map-layer-button");
                btnPois.AddToClassList("is-selected");

                layerBar.Add(btnRuns);
                layerBar.Add(btnLifts);
                layerBar.Add(btnPois);
            }
            else
            {
                layerBar.AddToClassList("is-hidden");
            }

            viewport.Add(layerBar);

            var bottomDock = new VisualElement { name = "MapBottomDock" };
            bottomDock.AddToClassList("is-hidden");

            var infoPanel = new VisualElement { name = "MapInfoPanel" };
            infoPanel.AddToClassList("is-hidden");

            root.Add(viewport);
            root.Add(bottomDock);
            root.Add(infoPanel);

            if (!interactive)
            {
                root.pickingMode = PickingMode.Ignore;
                viewport.pickingMode = PickingMode.Ignore;
                content.pickingMode = PickingMode.Ignore;
                bg.pickingMode = PickingMode.Ignore;
                polys.pickingMode = PickingMode.Ignore;
                markers.pickingMode = PickingMode.Ignore;
            }

            return root;
        }
    }
}